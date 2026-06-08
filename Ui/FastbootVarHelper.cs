using System;
using System.Collections.Generic;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Utilidades compartidas para leer variables del diccionario getvar all.
    /// </summary>
    internal static class FastbootVarHelper
    {
        public static string? FirstVar(Dictionary<string, string> vars, params string[] keys)
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
