using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Xiaomi_Flash.Ui
{
    internal static class XiaomiCodenameMap
    {
        const string EmbeddedResourceName = "Xiaomi_Flash.Data.xiaomi_codenames.json";
        const string ExternalRelativePath = "Data/xiaomi_codenames.json";

        static readonly object LoadSync = new object();
        static Dictionary<string, string>? displayNames;

        static readonly Dictionary<string, string> FallbackDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["joyeuse"] = "Redmi Note 9 Pro",
            ["curtana"] = "Redmi Note 9S",
            ["excalibur"] = "Redmi Note 9 Pro Max",
            ["gram"] = "POCO M2 Pro",
            ["sweet"] = "Redmi Note 10 Pro",
            ["sweetin"] = "Redmi Note 10 Lite",
            ["toco"] = "Mi Note 10 Lite",
            ["monet"] = "Mi 10 Lite 5G",
            ["alioth"] = "POCO F3",
            ["haydn"] = "Mi 11i",
            ["venus"] = "Mi 11",
            ["star"] = "Mi 11 Ultra",
            ["frost"] = "POCO C40",
            ["spes"] = "Redmi Note 11",
            ["miel"] = "Redmi 10",
            ["garnet"] = "Redmi Note 13 Pro",
            ["sapphire"] = "Redmi Note 13",
            ["aristotle"] = "Xiaomi 13T",
            ["tapas"] = "Redmi Note 12",
        };

        public static string? ResolveDisplayName(string? codename)
        {
            if (string.IsNullOrWhiteSpace(codename))
                return null;

            EnsureLoaded();
            if (displayNames!.TryGetValue(codename.Trim(), out string? name))
                return name;

            return null;
        }

        public static bool IsKnownCodename(string? codename)
        {
            if (string.IsNullOrWhiteSpace(codename))
                return false;

            EnsureLoaded();
            return displayNames!.ContainsKey(codename.Trim());
        }

        static void EnsureLoaded()
        {
            if (displayNames != null)
                return;

            lock (LoadSync)
            {
                if (displayNames != null)
                    return;

                displayNames = TryLoadDictionary() ?? new Dictionary<string, string>(FallbackDisplayNames, StringComparer.OrdinalIgnoreCase);
            }
        }

        static Dictionary<string, string>? TryLoadDictionary()
        {
            string? json = TryReadExternalJson() ?? TryReadEmbeddedJson();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                Dictionary<string, string>? raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (raw == null || raw.Count == 0)
                    return null;

                Dictionary<string, string> normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> entry in raw)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
                        continue;

                    string key = entry.Key.Trim();
                    if (!normalized.ContainsKey(key))
                        normalized[key] = entry.Value.Trim();
                }

                return normalized.Count > 0 ? normalized : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static string? TryReadExternalJson()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, ExternalRelativePath);
                if (!File.Exists(path))
                    return null;

                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        static string? TryReadEmbeddedJson()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (stream == null)
                return null;

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
