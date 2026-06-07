using System;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Xiaomi_Flash.Ui
{
    internal static class UsbDeviceLocator
    {
        const string UsbEnumPath = @"SYSTEM\CurrentControlSet\Enum\USB";

        public static string? FindPortBySerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return null;

            using RegistryKey? usbRoot = Registry.LocalMachine.OpenSubKey(UsbEnumPath);
            if (usbRoot == null)
                return null;

            foreach (string vidPid in usbRoot.GetSubKeyNames())
            {
                using RegistryKey? vidKey = usbRoot.OpenSubKey(vidPid);
                if (vidKey == null)
                    continue;

                foreach (string instance in vidKey.GetSubKeyNames())
                {
                    if (!instance.Equals(serial, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string? location = ReadLocation(vidKey.OpenSubKey(instance));
                    if (location != null)
                        return FormatLocation(location);
                }
            }

            return null;
        }

        static string? ReadLocation(RegistryKey? deviceKey)
        {
            if (deviceKey == null)
                return null;

            string? location = deviceKey.GetValue("LocationInformation") as string;
            if (!string.IsNullOrWhiteSpace(location))
                return location;

            using RegistryKey? parameters = deviceKey.OpenSubKey("Device Parameters");
            location = parameters?.GetValue("LocationInformation") as string;
            if (!string.IsNullOrWhiteSpace(location))
                return location;

            return null;
        }

        static string FormatLocation(string raw)
        {
            Match portMatch = Regex.Match(raw, @"Port_#0*(\d+)", RegexOptions.IgnoreCase);
            Match hubMatch = Regex.Match(raw, @"Hub_#0*(\d+)", RegexOptions.IgnoreCase);

            if (portMatch.Success)
            {
                string hub = hubMatch.Success ? $" / HUB {hubMatch.Groups[1].Value}" : "";
                return $"USB / PORT {portMatch.Groups[1].Value}{hub}";
            }

            return raw.Trim();
        }
    }
}
