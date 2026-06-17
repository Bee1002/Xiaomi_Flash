using System;
using System.Text;
using System.Windows;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Comprobaciones previas al flash en cualquier Xiaomi vía fastboot (sin lógica por SoC).
    /// </summary>
    internal static class FlashPrecheck
    {
        internal enum BootloaderState
        {
            Unknown,
            Locked,
            Unlocked
        }

        public static string BuildFlashDeviceSummary(string serial, string romRoot, RomFlashPlan plan, bool bypassAntiRb)
        {
            StringBuilder summary = new StringBuilder();

            BootloaderState bootloader = ResolveBootloaderStateCached(serial);
            summary.AppendLine("Bootloader: " + FormatBootloaderDisplay(bootloader));

            string deviceCodename = GetDeviceCodenameCached(serial);
            string romCodename = RomCodenameResolver.ResolveFromRomRoot(romRoot);
            if (!string.IsNullOrWhiteSpace(deviceCodename))
                summary.AppendLine("Device codename: " + deviceCodename);
            if (!string.IsNullOrWhiteSpace(romCodename))
                summary.AppendLine("ROM codename: " + romCodename);

            if (!string.IsNullOrWhiteSpace(deviceCodename)
                && !string.IsNullOrWhiteSpace(romCodename)
                && !RomCodenameResolver.CodenamesMatch(deviceCodename, romCodename))
            {
                summary.AppendLine("! Codename mismatch — wrong ROM can brick the device");
            }

            AntiRollbackCheck.Evaluation antiEval = AntiRollbackCheck.Evaluate(serial, romRoot, plan);
            summary.AppendLine("Device anti-RB: " + AntiRollbackCheck.FormatDeviceAntiForDisplay(antiEval));

            if (antiEval.RomSource != AntiRollbackCheck.RomAntiSource.None)
                summary.AppendLine("ROM anti-RB: " + AntiRollbackCheck.FormatRomAntiForDisplay(antiEval));
            else if (PlanFlashesAntiPartition(plan))
                summary.AppendLine("ROM anti-RB: not verified from package");

            if (antiEval.Status == AntiRollbackCheck.Status.DowngradeBlocked)
            {
                summary.AppendLine("! Anti-rollback check FAILED (flash_all.bat would abort)");
                if (bypassAntiRb)
                    summary.AppendLine("  Expert downgrade enabled — flash anti first, high brick risk");
            }
            else if (antiEval.Status == AntiRollbackCheck.Status.Pass && antiEval.AppliesScriptCheck)
            {
                summary.AppendLine("Anti-rollback check: pass");
            }

            AppendVabWarningsCached(summary, serial);

            if (plan != null && plan.Kind == RomFlashKind.Payload
                && !FastbootDeviceService.IsDeviceFastbootd(serial))
            {
                summary.AppendLine("! payload.bin — device in bootloader mode (fastbootd may be required)");
            }

            if (bootloader == BootloaderState.Locked)
                summary.AppendLine("! Bootloader LOCKED — flash will likely fail");

            return summary.ToString().TrimEnd();
        }

        public static void LogFlashConfirmWarnings(string serial, string romRoot, RomFlashPlan plan, bool bypassAntiRb)
        {
            if (ResolveBootloaderStateCached(serial) == BootloaderState.Locked)
                TerminalLog.Error("Flash started with bootloader LOCKED (user confirmed)");

            string deviceCodename = GetDeviceCodenameCached(serial);
            string romCodename = RomCodenameResolver.ResolveFromRomRoot(romRoot);
            if (!string.IsNullOrWhiteSpace(deviceCodename)
                && !string.IsNullOrWhiteSpace(romCodename)
                && !RomCodenameResolver.CodenamesMatch(deviceCodename, romCodename))
            {
                TerminalLog.Error("Codename mismatch override — device: " + deviceCodename + ", ROM: " + romCodename);
            }

            AntiRollbackCheck.Evaluation antiEval = AntiRollbackCheck.Evaluate(serial, romRoot, plan);
            if (antiEval.Status == AntiRollbackCheck.Status.DowngradeBlocked && bypassAntiRb)
            {
                TerminalLog.Error(
                    "Expert downgrade override — device anti: " + antiEval.DeviceAnti
                    + ", ROM anti: " + AntiRollbackCheck.FormatRomAntiForDisplay(antiEval));
            }
        }

        static void AppendVabWarningsCached(StringBuilder summary, string serial)
        {
            FastbootData? data = TryGetCachedFastbootData(serial);
            if (data == null)
                return;

            if (data.snapshot_update_status != null
                && data.snapshot_update_status != "none")
            {
                summary.AppendLine("! VAB snapshot staging: " + data.snapshot_update_status);
            }

            foreach (string key in data.partition_size.Keys)
            {
                if (key.EndsWith("cow", StringComparison.OrdinalIgnoreCase))
                {
                    summary.AppendLine("! VAB COW partitions detected on device");
                    break;
                }
            }
        }

        static FastbootData? TryGetCachedFastbootData(string serial)
        {
            if (FastbootDeviceDataCache.TryGetCached(serial, out FastbootData? cached))
                return cached;
            return null;
        }

        static BootloaderState ResolveBootloaderStateCached(string serial)
        {
            if (FastbootAutoProbe.TryGetCached(serial, out DeviceHardwareSnapshot? snapshot)
                && snapshot != null
                && !string.IsNullOrWhiteSpace(snapshot.Bootloader))
            {
                BootloaderState fromPanel = ParseBootloaderText(snapshot.Bootloader);
                if (fromPanel != BootloaderState.Unknown)
                    return fromPanel;
            }

            FastbootData? data = TryGetCachedFastbootData(serial);
            if (data != null)
            {
                BootloaderState fromVars = ParseBootloaderVars(data.bootloader_vars);
                if (fromVars != BootloaderState.Unknown)
                    return fromVars;
            }

            return BootloaderState.Unknown;
        }

        static string? GetDeviceCodenameCached(string serial)
        {
            if (FastbootAutoProbe.TryGetCached(serial, out DeviceHardwareSnapshot? cached)
                && cached != null
                && !string.IsNullOrWhiteSpace(cached.Codename))
                return cached.Codename;

            FastbootData? data = TryGetCachedFastbootData(serial);
            if (data != null && !string.IsNullOrWhiteSpace(data.product))
                return data.product;

            return null;
        }

        static string FormatBootloaderDisplay(BootloaderState state)
        {
            switch (state)
            {
                case BootloaderState.Unlocked:
                    return "unlocked";
                case BootloaderState.Locked:
                    return "locked";
                default:
                    return "unknown";
            }
        }

        public static BootloaderState ResolveBootloaderState(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return BootloaderState.Unknown;

            if (FastbootAutoProbe.TryGetCached(serial, out DeviceHardwareSnapshot cached)
                && cached != null
                && !string.IsNullOrWhiteSpace(cached.Bootloader))
            {
                BootloaderState fromPanel = ParseBootloaderText(cached.Bootloader);
                if (fromPanel != BootloaderState.Unknown)
                    return fromPanel;
            }

            try
            {
                FastbootData data = FastbootDeviceDataCache.GetOrLoad(serial);
                BootloaderState fromVars = ParseBootloaderVars(data.bootloader_vars);
                if (fromVars != BootloaderState.Unknown)
                    return fromVars;
            }
            catch (Exception) { }

            try
            {
                string unlocked = FastbootVarReader.GetVar(serial, "unlocked");
                BootloaderState fromVar = ParseBootloaderText(unlocked);
                if (fromVar != BootloaderState.Unknown)
                    return fromVar;
            }
            catch (Exception) { }

            return BootloaderState.Unknown;
        }

        public static bool ConfirmBootloaderUnlocked(string serial, string operationLabel)
        {
            BootloaderState state = ResolveBootloaderState(serial);

            if (state == BootloaderState.Unlocked || state == BootloaderState.Unknown)
                return true;

            MessageBoxResult result = MessageBox.Show(
                "Bootloader appears LOCKED.\n\n"
                + "Operation: " + operationLabel + "\n\n"
                + "Most Xiaomi fastboot flash steps (erase, flash, vbmeta fix) require an unlocked bootloader.\n"
                + "Continuing will likely fail or leave the device in an unstable state.\n\n"
                + "Unlock the bootloader first, then retry.\n\nContinue anyway?",
                "Bootloader locked",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                TerminalLog.Action("Cancelled: bootloader locked");
                return false;
            }

            TerminalLog.Error("Bootloader locked — user chose to continue (" + operationLabel + ")");
            return true;
        }

        static bool PlanFlashesAntiPartition(RomFlashPlan plan)
        {
            if (plan == null)
                return false;

            foreach (FlashScriptStep step in plan.Steps)
            {
                if (step.Kind == FlashScriptStepKind.Flash
                    && step.Partition.Equals("anti", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static BootloaderState ParseBootloaderText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return BootloaderState.Unknown;

            string text = value.Trim();

            if (text.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || text.Equals("true", StringComparison.OrdinalIgnoreCase)
                || text.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
                return BootloaderState.Unlocked;

            if (text.Equals("no", StringComparison.OrdinalIgnoreCase)
                || text.Equals("false", StringComparison.OrdinalIgnoreCase)
                || text.Equals("locked", StringComparison.OrdinalIgnoreCase))
                return BootloaderState.Locked;

            return BootloaderState.Unknown;
        }

        static BootloaderState ParseBootloaderVars(System.Collections.Generic.Dictionary<string, string> vars)
        {
            if (vars == null || vars.Count == 0)
                return BootloaderState.Unknown;

            string? unlocked = FastbootVarHelper.FirstVar(vars, "unlocked");
            BootloaderState fromUnlocked = ParseBootloaderText(unlocked);
            if (fromUnlocked != BootloaderState.Unknown)
                return fromUnlocked;

            string? deviceState = FastbootVarHelper.FirstVar(vars, "device-state");
            if (!string.IsNullOrWhiteSpace(deviceState))
            {
                if (deviceState.IndexOf("unlocked", StringComparison.OrdinalIgnoreCase) >= 0)
                    return BootloaderState.Unlocked;
                if (deviceState.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0)
                    return BootloaderState.Locked;
            }

            return BootloaderState.Unknown;
        }
    }
}
