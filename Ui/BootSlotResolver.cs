using System;
using System.Collections.Generic;

namespace Xiaomi_Flash.Ui
{
    internal static class BootSlotResolver
    {
        public static string? Resolve(Dictionary<string, string> vars)
        {
            string? slot = FirstVar(vars, "current-slot");
            if (!string.IsNullOrWhiteSpace(slot))
                return FormatSlot(slot);

            return null;
        }

        public static string? FormatSlot(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            string slot = raw.Trim().ToLowerInvariant();
            if (slot == "a")
                return "A";
            if (slot == "b")
                return "B";

            return raw.Trim().ToUpperInvariant();
        }

        static string? FirstVar(Dictionary<string, string> vars, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (vars.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return null;
        }
    }
}
