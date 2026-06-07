using System;
using System.Collections.Generic;

namespace Xiaomi_Flash.Ui
{
    internal static class XiaomiCodenameMap
    {
        static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["joyeuse"] = "Redmi Note 9 Pro",
            ["curtana"] = "Redmi Note 9 Pro",
            ["excalibur"] = "Redmi Note 9 Pro",
            ["gram"] = "Redmi Note 10 Pro",
            ["sweet"] = "Redmi Note 10",
            ["sweetin"] = "Redmi Note 10 Lite",
            ["toco"] = "Mi Note 10 Lite",
            ["monet"] = "Mi Note 10 Lite",
            ["alioth"] = "POCO F3",
            ["haydn"] = "Redmi K40 Pro",
            ["venus"] = "Mi 11",
            ["star"] = "Mi 11 Ultra",
            ["frost"] = "Redmi 10C",
            ["spes"] = "Redmi Note 11",
            ["miel"] = "Redmi 10",
            ["garnet"] = "Redmi Note 13 Pro",
            ["sapphire"] = "Redmi Note 13",
            ["aristotle"] = "Redmi Note 12 Pro",
            ["tapas"] = "Redmi Note 12",
        };

        public static string? ResolveDisplayName(string? codename)
        {
            if (string.IsNullOrWhiteSpace(codename))
                return null;

            if (DisplayNames.TryGetValue(codename.Trim(), out string? name))
                return name;

            return null;
        }
    }
}
