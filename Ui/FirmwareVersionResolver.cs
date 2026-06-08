using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xiaomi_Flash.Ui
{
    internal static class FirmwareVersionResolver
    {
        static readonly Regex MiuiFromFingerprint = new Regex(
            @"(V\d+\.\d+\.\d+\.\d+\.[A-Z0-9._-]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex HyperOsFromFingerprint = new Regex(
            @"(OS\d+\.\d+\.\d+\.\d+\.[A-Z0-9._-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string? Resolve(Dictionary<string, string> vars)
        {
            string? fromFingerprint = FromFingerprint(FastbootVarHelper.FirstVar(vars,
                "system-fingerprint", "vendor-fingerprint", "build-fingerprint"));
            if (!string.IsNullOrWhiteSpace(fromFingerprint))
                return fromFingerprint;

            string? explicitVersion = FastbootVarHelper.FirstVar(vars,
                "rom_version", "miui_version", "hyperos_version", "version-incremental");
            if (LooksLikeRomVersion(explicitVersion))
                return explicitVersion;

            string? os = FastbootVarHelper.FirstVar(vars, "version-os");
            string? incremental = FastbootVarHelper.FirstVar(vars, "version-incremental");
            if (!string.IsNullOrWhiteSpace(os) && !string.IsNullOrWhiteSpace(incremental)
                && LooksLikeRomVersion(incremental))
                return incremental;

            return null;
        }

        static string? FromFingerprint(string? fingerprint)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
                return null;

            Match hyper = HyperOsFromFingerprint.Match(fingerprint);
            if (hyper.Success)
                return hyper.Groups[1].Value;

            Match miui = MiuiFromFingerprint.Match(fingerprint);
            if (miui.Success)
                return miui.Groups[1].Value;

            return null;
        }

        static bool LooksLikeRomVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.StartsWith("V", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("OS", StringComparison.OrdinalIgnoreCase);
        }

    }
}
