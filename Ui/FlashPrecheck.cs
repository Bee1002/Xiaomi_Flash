using System;
using System.Text;
using System.Text.RegularExpressions;
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

        static readonly Regex AntiVersionInName = new Regex(
            @"^anti(?:[_-]?v?(\d+)|_rollback[_-]?(\d+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

            if (TryParseAntiVersion(GetDeviceAntiRollbackCached(serial), out int deviceAnti))
            {
                summary.AppendLine("Device anti-RB: " + deviceAnti);
                if (TryResolveRomAntiVersion(romRoot, plan, out int romAnti))
                {
                    summary.AppendLine("ROM anti-RB: " + romAnti);
                    if (romAnti < deviceAnti)
                    {
                        summary.AppendLine("! Anti-rollback downgrade risk");
                        if (bypassAntiRb)
                            summary.AppendLine("  (Bypass anti_RB is enabled)");
                    }
                }
                else if (PlanFlashesAntiPartition(plan))
                {
                    summary.AppendLine("ROM anti-RB: not verified from package");
                }
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

        public static void LogFlashConfirmWarnings(string serial, string romRoot, RomFlashPlan plan)
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

            if (TryParseAntiVersion(GetDeviceAntiRollbackCached(serial), out int deviceAnti)
                && TryResolveRomAntiVersion(romRoot, plan, out int romAnti)
                && romAnti < deviceAnti)
            {
                TerminalLog.Error("Anti-rollback downgrade override — device: " + deviceAnti + ", ROM: " + romAnti);
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

        static string? GetDeviceAntiRollbackCached(string serial)
        {
            if (FastbootAutoProbe.TryGetCached(serial, out DeviceHardwareSnapshot? cached)
                && cached != null
                && !string.IsNullOrWhiteSpace(cached.AntiRollback))
                return cached.AntiRollback.Trim();

            FastbootData? data = TryGetCachedFastbootData(serial);
            if (data != null)
            {
                string? fromVars = FastbootVarHelper.FirstVar(data.bootloader_vars, "anti", "rollback_ver", "rollback-index");
                if (!string.IsNullOrWhiteSpace(fromVars))
                    return fromVars.Trim();
            }

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

        static bool TryResolveRomAntiVersion(string romRoot, RomFlashPlan plan, out int version)
        {
            version = 0;

            string antiImage = RomFlashScanner.FindAntiImage(romRoot);
            if (!string.IsNullOrEmpty(antiImage)
                && TryParseAntiVersionFromFileName(System.IO.Path.GetFileNameWithoutExtension(antiImage), out version))
                return true;

            if (plan != null)
            {
                foreach (FlashScriptStep step in plan.Steps)
                {
                    if (step.Kind != FlashScriptStepKind.Flash
                        || !step.Partition.Equals("anti", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrEmpty(step.ImagePath))
                        continue;

                    if (TryParseAntiVersionFromFileName(
                            System.IO.Path.GetFileNameWithoutExtension(step.ImagePath), out version))
                        return true;
                }
            }

            return false;
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

        internal static bool TryParseAntiVersionFromFileName(string fileNameWithoutExtension, out int version)
        {
            version = 0;
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
                return false;

            string name = fileNameWithoutExtension.Trim();
            if (name.Equals("anti", StringComparison.OrdinalIgnoreCase))
                return false;

            Match match = AntiVersionInName.Match(name);
            if (!match.Success)
            {
                Match trailing = Regex.Match(name, @"(\d+)$", RegexOptions.CultureInvariant);
                if (!trailing.Success || !name.StartsWith("anti", StringComparison.OrdinalIgnoreCase))
                    return false;

                return int.TryParse(trailing.Groups[1].Value, out version);
            }

            string raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return int.TryParse(raw, out version);
        }

        static bool TryParseAntiVersion(string raw, out int version)
        {
            version = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string trimmed = raw.Trim();
            if (int.TryParse(trimmed, out version))
                return true;

            Match hex = Regex.Match(trimmed, @"^0x([0-9a-fA-F]+)$", RegexOptions.CultureInvariant);
            if (hex.Success)
            {
                try
                {
                    version = Convert.ToInt32(hex.Groups[1].Value, 16);
                    return true;
                }
                catch (OverflowException)
                {
                    return false;
                }
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
