using System;

namespace Xiaomi_Flash
{
    static class FastbootVarReader
    {
        public static string? GetVar(string serial, string varName)
        {
            lock (FastbootGate.Sync)
            {
                using (Fastboot fastboot = new Fastboot(serial, "getvar " + varName))
                {
                    string stderr = fastboot.stderr.ReadToEnd();
                    return ParseVar(stderr, varName);
                }
            }
        }

        static string? ParseVar(string stderr, string varName)
        {
            foreach (string line in stderr.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("(bootloader)", StringComparison.OrdinalIgnoreCase))
                    trimmed = trimmed.Substring("(bootloader)".Length).Trim();

                int idx = trimmed.IndexOf(varName + ":", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    continue;

                string value = trimmed.Substring(idx + varName.Length + 1).Trim();
                if (value.Length > 0)
                    return value;
            }

            return null;
        }
    }
}
