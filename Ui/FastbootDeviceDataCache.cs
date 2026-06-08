using System;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Caché de FastbootData por serial para evitar getvar all repetidos en la misma sesión.
    /// </summary>
    internal static class FastbootDeviceDataCache
    {
        static string? cachedSerial;
        static FastbootData? cachedData;

        public static FastbootData? Current => cachedData;

        public static FastbootData GetOrLoad(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException("Serial is required.", nameof(serial));

            if (cachedData != null
                && cachedSerial != null
                && cachedSerial.Equals(serial, StringComparison.OrdinalIgnoreCase))
                return cachedData;

            lock (FastbootGate.Sync)
            {
                if (cachedData != null
                    && cachedSerial != null
                    && cachedSerial.Equals(serial, StringComparison.OrdinalIgnoreCase))
                    return cachedData;

                using (Fastboot fastboot = new Fastboot(serial, "getvar all"))
                    cachedData = new FastbootData(fastboot.stderr.ReadToEnd());

                cachedSerial = serial;
                return cachedData;
            }
        }

        public static void Invalidate(string? serial = null)
        {
            if (serial == null
                || cachedSerial == null
                || cachedSerial.Equals(serial, StringComparison.OrdinalIgnoreCase))
            {
                cachedSerial = null;
                cachedData = null;
            }
        }
    }
}
