using System;
using System.IO;
using System.Text.RegularExpressions;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Emula el bloque anti-rollback de flash_all.bat (anti_version.txt + getvar anti).
    /// </summary>
    internal static class AntiRollbackCheck
    {
        internal const int DefaultRomAntiWhenPackageMissing = 1;

        internal enum Status
        {
            Pass,
            DowngradeBlocked,
            NotApplicable
        }

        internal enum RomAntiSource
        {
            None,
            AntiVersionTxt,
            PackageDefault,
            AntiImageName
        }

        internal sealed class Evaluation
        {
            public Status Status { get; init; }
            public int DeviceAnti { get; init; }
            public int RomAnti { get; init; }
            public RomAntiSource RomSource { get; init; }
            public bool DeviceAntiLiveQuery { get; init; }
            public bool AppliesScriptCheck { get; init; }
        }

        static readonly Regex AntiVersionInName = new Regex(
            @"^anti(?:[_-]?v?(\d+)|_rollback[_-]?(\d+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static Evaluation Evaluate(string serial, string romRoot, RomFlashPlan? plan)
        {
            bool appliesScriptCheck = plan != null && plan.Kind == RomFlashKind.Script;
            ResolveDeviceAnti(serial, out int deviceAnti, out bool deviceLiveQuery);
            ResolveRomAnti(romRoot, plan, appliesScriptCheck, out int romAnti, out RomAntiSource romSource);

            Status status;
            if (!appliesScriptCheck)
                status = Status.NotApplicable;
            else if (deviceAnti > romAnti)
                status = Status.DowngradeBlocked;
            else
                status = Status.Pass;

            return new Evaluation
            {
                Status = status,
                DeviceAnti = deviceAnti,
                RomAnti = romAnti,
                RomSource = romSource,
                DeviceAntiLiveQuery = deviceLiveQuery,
                AppliesScriptCheck = appliesScriptCheck
            };
        }

        public static bool TryEnsureCanStartFlash(
            string serial,
            string romRoot,
            RomFlashPlan plan,
            bool bypassAntiRb,
            out Evaluation evaluation,
            out string blockMessage)
        {
            evaluation = Evaluate(serial, romRoot, plan);
            blockMessage = "";

            if (bypassAntiRb || evaluation.Status != Status.DowngradeBlocked)
                return true;

            blockMessage =
                "Anti-rollback check failed (same rule as flash_all.bat).\n\n"
                + "Device anti-RB: " + evaluation.DeviceAnti + "\n"
                + "ROM anti-RB: " + FormatRomAntiForDisplay(evaluation) + "\n\n"
                + "Flashing this ROM can hard-brick the device.\n\n"
                + "Use a ROM with anti-RB equal or higher than the device,\n"
                + "or enable \"Expert: allow downgrade\" only if you accept the risk.";
            return false;
        }

        public static bool ShouldApplyBypassFlash(bool bypassAntiRb, Evaluation evaluation)
        {
            return bypassAntiRb && evaluation.Status == Status.DowngradeBlocked;
        }

        public static string FormatRomAntiForDisplay(Evaluation evaluation)
        {
            switch (evaluation.RomSource)
            {
                case RomAntiSource.AntiVersionTxt:
                    return evaluation.RomAnti + " (anti_version.txt)";
                case RomAntiSource.PackageDefault:
                    return evaluation.RomAnti + " (default, no anti_version.txt)";
                case RomAntiSource.AntiImageName:
                    return evaluation.RomAnti + " (from anti image name)";
                default:
                    return evaluation.RomAnti.ToString();
            }
        }

        public static string FormatDeviceAntiForDisplay(Evaluation evaluation)
        {
            string suffix = evaluation.DeviceAntiLiveQuery ? " (live query)" : "";
            return evaluation.DeviceAnti + suffix;
        }

        static void ResolveDeviceAnti(string serial, out int deviceAnti, out bool liveQuery)
        {
            liveQuery = false;
            deviceAnti = 0;

            string? raw = GetDeviceAntiRollbackCached(serial);
            if (TryParseAntiVersion(raw, out deviceAnti))
                return;

            try
            {
                raw = QueryDeviceAntiLive(serial);
                liveQuery = true;
            }
            catch (Exception)
            {
                return;
            }

            if (!TryParseAntiVersion(raw, out deviceAnti))
                deviceAnti = 0;
        }

        static void ResolveRomAnti(
            string romRoot,
            RomFlashPlan? plan,
            bool appliesScriptCheck,
            out int romAnti,
            out RomAntiSource source)
        {
            romAnti = 0;
            source = RomAntiSource.None;

            if (TryReadAntiVersionTxt(romRoot, out romAnti))
            {
                source = RomAntiSource.AntiVersionTxt;
                return;
            }

            if (appliesScriptCheck)
            {
                romAnti = DefaultRomAntiWhenPackageMissing;
                source = RomAntiSource.PackageDefault;
                return;
            }

            if (TryResolveRomAntiFromImageName(romRoot, plan, out romAnti))
                source = RomAntiSource.AntiImageName;
        }

        static bool TryReadAntiVersionTxt(string romRoot, out int version)
        {
            version = 0;
            if (string.IsNullOrWhiteSpace(romRoot))
                return false;

            string path = Path.Combine(RomPackageResolver.GetImagesDir(romRoot), "anti_version.txt");
            if (!File.Exists(path))
                return false;

            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)
                    || trimmed.StartsWith(";", StringComparison.Ordinal))
                    continue;

                if (TryParseAntiVersion(trimmed, out version))
                    return true;

                int eq = trimmed.IndexOf('=');
                if (eq >= 0)
                {
                    string right = trimmed.Substring(eq + 1).Trim();
                    if (TryParseAntiVersion(right, out version))
                        return true;
                }
            }

            return false;
        }

        static bool TryResolveRomAntiFromImageName(string romRoot, RomFlashPlan? plan, out int version)
        {
            version = 0;

            string antiImage = RomFlashScanner.FindAntiImage(romRoot);
            if (!string.IsNullOrEmpty(antiImage)
                && TryParseAntiVersionFromFileName(Path.GetFileNameWithoutExtension(antiImage), out version))
                return true;

            if (plan == null)
                return false;

            foreach (FlashScriptStep step in plan.Steps)
            {
                if (step.Kind != FlashScriptStepKind.Flash
                    || !step.Partition.Equals("anti", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(step.ImagePath))
                    continue;

                if (TryParseAntiVersionFromFileName(
                        Path.GetFileNameWithoutExtension(step.ImagePath), out version))
                    return true;
            }

            return false;
        }

        static string? QueryDeviceAntiLive(string serial)
        {
            lock (FastbootGate.Sync)
            {
                string? anti = FastbootVarReader.GetVar(serial, "anti");
                if (!string.IsNullOrWhiteSpace(anti))
                    return anti.Trim();

                string? rollbackVer = FastbootVarReader.GetVar(serial, "rollback_ver");
                if (!string.IsNullOrWhiteSpace(rollbackVer))
                    return rollbackVer.Trim();
            }

            return null;
        }

        static string? GetDeviceAntiRollbackCached(string serial)
        {
            if (FastbootAutoProbe.TryGetCached(serial, out DeviceHardwareSnapshot? cached)
                && cached != null
                && !string.IsNullOrWhiteSpace(cached.AntiRollback))
                return cached.AntiRollback.Trim();

            if (FastbootDeviceDataCache.TryGetCached(serial, out FastbootData? data)
                && data != null)
            {
                string? fromVars = FastbootVarHelper.FirstVar(
                    data.bootloader_vars, "anti", "rollback_ver", "rollback-index");
                if (!string.IsNullOrWhiteSpace(fromVars))
                    return fromVars.Trim();
            }

            return null;
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

        internal static bool TryParseAntiVersion(string? raw, out int version)
        {
            version = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string trimmed = raw.Trim();
            if (int.TryParse(trimmed, out version))
                return true;

            Match hex = Regex.Match(trimmed, @"^0x([0-9a-fA-F]+)$", RegexOptions.CultureInvariant);
            if (!hex.Success)
                return false;

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
    }
}
