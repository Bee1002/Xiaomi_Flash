using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Xiaomi_Flash.Ui
{
    internal static class RomBatScriptParser
    {
        static readonly Regex FastbootLine = new Regex(
            @"fastboot\s+(?:%\*\s+)?(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex FlashQuoted = new Regex(
            @"^flash\s+(?:(--disable-verity\s+--disable-verification)\s+)?""([^""]+)""\s+""([^""]+)""$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex FlashUnquoted = new Regex(
            @"^flash\s+(?:(--disable-verity\s+--disable-verification)\s+)?(\S+)\s+(\S+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex EraseQuoted = new Regex(
            @"^erase\s+""([^""]+)""$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex EraseUnquoted = new Regex(
            @"^erase\s+(\S+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static List<FlashScriptStep> Parse(string batPath, string romRoot, List<string>? skipped = null)
        {
            List<FlashScriptStep> steps = new List<FlashScriptStep>();
            string batDir = Path.GetDirectoryName(Path.GetFullPath(batPath)) ?? romRoot;
            string imagesDir = RomPackageResolver.GetImagesDir(romRoot, batDir);

            foreach (string rawLine in File.ReadAllLines(batPath))
            {
                string line = NormalizeLine(rawLine);
                if (line == null)
                    continue;

                Match fb = FastbootLine.Match(line);
                if (!fb.Success)
                    continue;

                string command = fb.Groups[1].Value.Trim();
                if (IsSkippedCheckCommand(command))
                    continue;

                FlashScriptStep step = ParseCommand(command, batDir, romRoot, imagesDir, skipped);
                if (step != null)
                    steps.Add(step);
            }

            return steps;
        }

        static string NormalizeLine(string rawLine)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                return null;
            if (line.StartsWith("::", StringComparison.Ordinal))
                return null;
            if (line.StartsWith("@", StringComparison.Ordinal))
                line = line.TrimStart('@').Trim();
            if (line.StartsWith("echo", StringComparison.OrdinalIgnoreCase))
                return null;
            if (line.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
                return null;
            if (line.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
                return null;
            if (line.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
                return null;
            if (line.StartsWith("pause", StringComparison.OrdinalIgnoreCase))
                return null;
            if (line.StartsWith("exit", StringComparison.OrdinalIgnoreCase))
                return null;

            int cut = line.IndexOf("||", StringComparison.Ordinal);
            if (cut >= 0)
                line = line.Substring(0, cut).Trim();

            return line.Length > 0 ? line : null;
        }

        static bool IsSkippedCheckCommand(string command)
        {
            if (command.StartsWith("getvar", StringComparison.OrdinalIgnoreCase))
                return true;
            if (command.StartsWith("oem device-info", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        static FlashScriptStep ParseCommand(string command, string batDir, string romRoot, string imagesDir, List<string>? skipped)
        {
            if (command.StartsWith("flash ", StringComparison.OrdinalIgnoreCase))
                return ParseFlash(command, batDir, romRoot, imagesDir, skipped);

            if (command.StartsWith("erase ", StringComparison.OrdinalIgnoreCase))
                return ParseErase(command);

            if (command.StartsWith("reboot", StringComparison.OrdinalIgnoreCase))
                return ParseReboot(command);

            if (command.StartsWith("set_active ", StringComparison.OrdinalIgnoreCase))
                return ParseSetActive(command);

            if (command.Equals("oem lock", StringComparison.OrdinalIgnoreCase)
                || command.StartsWith("oem lock ", StringComparison.OrdinalIgnoreCase))
            {
                return new FlashScriptStep
                {
                    Kind = FlashScriptStepKind.OemLock,
                    DisplayName = "oem lock"
                };
            }

            return null;
        }

        static FlashScriptStep ParseFlash(string command, string batDir, string romRoot, string imagesDir, List<string>? skipped)
        {
            Match match = FlashQuoted.Match(command);
            if (!match.Success)
                match = FlashUnquoted.Match(command);
            if (!match.Success)
                return null;

            string extra = match.Groups[1].Success ? match.Groups[1].Value.Trim() : "";
            string partition = match.Groups[2].Value.Trim();
            string imageRaw = match.Groups[3].Value.Trim();
            string imagePath = ResolveImagePath(imageRaw, batDir, romRoot, imagesDir);
            if (!File.Exists(imagePath))
            {
                string resolvedHint = ExpandBatchDrivePath(imageRaw, batDir);
                skipped?.Add(partition + " (missing: " + Path.GetFileName(resolvedHint) + " in " + imagesDir + ")");
                return null;
            }

            return new FlashScriptStep
            {
                Kind = FlashScriptStepKind.Flash,
                DisplayName = partition,
                Partition = partition,
                ImagePath = imagePath,
                ExtraArgs = extra
            };
        }

        static FlashScriptStep ParseErase(string command)
        {
            Match match = EraseQuoted.Match(command);
            if (!match.Success)
                match = EraseUnquoted.Match(command);
            if (!match.Success)
                return null;

            string partition = match.Groups[1].Value.Trim();
            return new FlashScriptStep
            {
                Kind = FlashScriptStepKind.Erase,
                DisplayName = "erase " + partition,
                Partition = partition
            };
        }

        static FlashScriptStep ParseReboot(string command)
        {
            string target = command.Length > 6 ? command.Substring(6).Trim() : "";
            return new FlashScriptStep
            {
                Kind = FlashScriptStepKind.Reboot,
                DisplayName = string.IsNullOrEmpty(target) ? "reboot" : "reboot " + target,
                RebootTarget = target
            };
        }

        static FlashScriptStep ParseSetActive(string command)
        {
            string slot = command.Substring("set_active".Length).Trim();
            return new FlashScriptStep
            {
                Kind = FlashScriptStepKind.SetActive,
                DisplayName = "set_active " + slot,
                ActiveSlot = slot
            };
        }

        static string ResolveImagePath(string raw, string batDir, string romRoot, string imagesDir)
        {
            string path = ExpandBatchDrivePath(raw, batDir);
            if (File.Exists(path))
                return Path.GetFullPath(path);

            string fileName = Path.GetFileName(path);
            foreach (string dir in RomPackageResolver.GetImageSearchDirs(romRoot, batDir, imagesDir))
            {
                string candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }

            string? sparseSuper = FindSparseSuperImage(romRoot, batDir, imagesDir);
            if (sparseSuper != null
                && fileName.Equals("super.img", StringComparison.OrdinalIgnoreCase))
                return sparseSuper;

            return Path.GetFullPath(path);
        }

        static string ExpandBatchDrivePath(string raw, string batDir)
        {
            string path = raw.Trim().Trim('"');
            string batPrefix = EnsureTrailingSeparator(batDir);

            path = ReplaceOrdinalIgnoreCase(path, "%~dp0", batPrefix);
            path = ReplaceOrdinalIgnoreCase(path, "%dp0", batPrefix);
            path = ReplaceOrdinalIgnoreCase(path, "%~d0", batPrefix);

            string batPrefixNoSlash = batPrefix.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            path = ReplaceOrdinalIgnoreCase(path, "%CD%", batPrefixNoSlash);
            path = ReplaceOrdinalIgnoreCase(path, "%cd%", batPrefixNoSlash);

            path = path.Replace('/', Path.DirectorySeparatorChar);
            return path;
        }

        static string? FindSparseSuperImage(string romRoot, string batDir, string imagesDir)
        {
            foreach (string dir in RomPackageResolver.GetImageSearchDirs(romRoot, batDir, imagesDir))
            {
                if (!Directory.Exists(dir))
                    continue;

                string direct = Path.Combine(dir, "super.img");
                if (File.Exists(direct))
                    return Path.GetFullPath(direct);
            }

            return null;
        }

        static string EnsureTrailingSeparator(string dir)
        {
            string full = Path.GetFullPath(dir);
            if (!full.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !full.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                full += Path.DirectorySeparatorChar;
            return full;
        }

        static string ReplaceOrdinalIgnoreCase(string input, string oldValue, string newValue)
        {
            int idx = input.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                input = input.Substring(0, idx) + newValue + input.Substring(idx + oldValue.Length);
                idx = input.IndexOf(oldValue, idx + newValue.Length, StringComparison.OrdinalIgnoreCase);
            }

            return input;
        }
    }
}
