using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Infiere el codename del paquete ROM Xiaomi (carpeta / script fastboot).
    /// Aplica a cualquier modelo; no depende del SoC.
    /// </summary>
    internal static class RomCodenameResolver
    {
        static readonly Regex ProductToken = new Regex(
            @"(?:product|codename|device)\s*(?:==|NEQ|neq|!=|=|:)\s*[""']?([a-zA-Z][a-zA-Z0-9_-]{1,20})[""']?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string? ResolveFromRomRoot(string romRoot)
        {
            if (string.IsNullOrWhiteSpace(romRoot) || !Directory.Exists(romRoot))
                return null;

            string? fromFolder = ResolveFromFolderName(Path.GetFileName(romRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            if (!string.IsNullOrWhiteSpace(fromFolder))
                return fromFolder;

            return ResolveFromFlashScripts(romRoot);
        }

        public static bool CodenamesMatch(string? deviceCodename, string? romCodename)
        {
            if (string.IsNullOrWhiteSpace(deviceCodename) || string.IsNullOrWhiteSpace(romCodename))
                return true;

            string device = Normalize(deviceCodename);
            string rom = Normalize(romCodename);

            if (device == rom)
                return true;

            if (device.StartsWith(rom, StringComparison.Ordinal)
                || rom.StartsWith(device, StringComparison.Ordinal))
                return true;

            return false;
        }

        static string? ResolveFromFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return null;

            foreach (string token in Tokenize(folderName))
            {
                if (XiaomiCodenameMap.IsKnownCodename(token))
                    return Normalize(token);
            }

            return null;
        }

        static string? ResolveFromFlashScripts(string romRoot)
        {
            foreach (string batName in new[] { "flash_all.bat", "flash_all_lock.bat", "flash_all_except_storage.bat" })
            {
                string batPath = Path.Combine(romRoot, batName);
                if (!File.Exists(batPath))
                    continue;

                string? fromScript = ResolveFromScriptLines(File.ReadAllLines(batPath));
                if (!string.IsNullOrWhiteSpace(fromScript))
                    return fromScript;
            }

            return null;
        }

        static string? ResolveFromScriptLines(string[] lines)
        {
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("::", StringComparison.Ordinal))
                    continue;

                Match match = ProductToken.Match(line);
                if (!match.Success)
                    continue;

                string token = match.Groups[1].Value.Trim();
                if (XiaomiCodenameMap.IsKnownCodename(token))
                    return Normalize(token);
            }

            return null;
        }

        static IEnumerable<string> Tokenize(string text)
        {
            string[] parts = text.Split(new[] { '_', '-', ' ', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length >= 2)
                    yield return trimmed;
            }
        }

        static string Normalize(string codename) =>
            codename.Trim().ToLowerInvariant();
    }
}
