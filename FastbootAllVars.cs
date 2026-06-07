using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xiaomi_Flash
{
    static class FastbootAllVars
    {
        static readonly Regex BootloaderLine = new Regex(
            @"^\(bootloader\)\s+([^:]+):\s*(.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static Dictionary<string, string> Parse(string stderr)
        {
            Dictionary<string, string> vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(stderr))
                return vars;

            foreach (string rawLine in stderr.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match match = BootloaderLine.Match(rawLine.Trim());
                if (!match.Success)
                    continue;

                string key = match.Groups[1].Value.Trim();
                string value = match.Groups[2].Value.Trim();
                if (key.Length == 0)
                    continue;

                vars[key] = value;
            }

            return vars;
        }

        public static string ReadAll(string serial)
        {
            lock (FastbootGate.Sync)
            {
                using (Fastboot fastboot = new Fastboot(serial, "getvar all"))
                    return fastboot.stderr.ReadToEnd();
            }
        }

        public static string? TryReadVar(string serial, string varName)
        {
            return FastbootVarReader.GetVar(serial, varName);
        }

        public static string TryReadOemCommand(string serial, string command)
        {
            lock (FastbootGate.Sync)
            {
                using (Fastboot fastboot = new Fastboot(serial, command))
                {
                    string stderr = fastboot.stderr.ReadToEnd();
                    string stdout = fastboot.stdout.ReadToEnd();
                    return stderr + stdout;
                }
            }
        }
    }
}
