using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    internal static class DeviceInfoMapper
    {
        static readonly Regex HexId = new Regex(
            @"0x[0-9a-fA-F]{6,}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static DeviceHardwareSnapshot FromVars(
            Dictionary<string, string> vars,
            string serial,
            string? port,
            string? antiRollbackOverride)
        {
            string? codename = FirstVar(vars, "product");
            DeviceHardwareSnapshot snapshot = new DeviceHardwareSnapshot
            {
                Serial = FirstVar(vars, "serialno") ?? serial,
                Port = port,
                Codename = codename,
                Model = ResolveMarketingName(vars, codename),
                Storage = StorageTypeResolver.Resolve(vars),
                CpuId = FirstVar(vars, "soc-id", "chip-id", "cpuid", "cpu-id", "platform"),
                HwRevision = FirstVar(vars, "hw-revision", "hwversion", "hw_version"),
                SecureBoot = FormatSecure(FirstVar(vars, "secure")),
                Bootloader = FormatBootloader(FirstVar(vars, "unlocked", "device-state")),
                AntiRollback = antiRollbackOverride ?? FirstVar(vars, "anti", "rollback_ver", "rollback-index"),
                FirmwareVersion = FirmwareVersionResolver.Resolve(vars),
                BootSlot = BootSlotResolver.Resolve(vars),
            };

            return snapshot;
        }

        public static void ApplyOemDeviceInfo(DeviceHardwareSnapshot snapshot, string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return;

            Match unlocked = Regex.Match(output,
                @"Device\s+unlocked\s*:\s*(true|false)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (unlocked.Success)
            {
                snapshot.Bootloader = unlocked.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase)
                    ? "unlocked"
                    : "locked";
            }
        }

        public static void ApplyOemHwid(DeviceHardwareSnapshot snapshot, string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return;

            if (string.IsNullOrWhiteSpace(snapshot.CpuId))
            {
                Match cpu = HexId.Match(output);
                if (cpu.Success)
                    snapshot.CpuId = cpu.Value;
            }

            if (string.IsNullOrWhiteSpace(snapshot.HwRevision))
            {
                Match rev = Regex.Match(output, @"(?:HW\s*revision|hw-revision|revision)\s*[:=]\s*(\S+)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (rev.Success)
                    snapshot.HwRevision = rev.Groups[1].Value;
            }
        }

        static string? ResolveMarketingName(Dictionary<string, string> vars, string? codename)
        {
            string? market = FirstVar(vars, "marketname", "market-name", "friendly-product-name", "device-name");
            if (!string.IsNullOrWhiteSpace(market))
                return market;

            return XiaomiCodenameMap.ResolveDisplayName(codename);
        }

        static string? FormatSecure(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return "enabled";
            if (value.Equals("no", StringComparison.OrdinalIgnoreCase))
                return "disabled";
            return value;
        }

        static string? FormatBootloader(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (value.Equals("no", StringComparison.OrdinalIgnoreCase)
                || value.Equals("locked", StringComparison.OrdinalIgnoreCase))
                return "locked";

            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
                return "unlocked";

            return value;
        }

        public static DeviceHardwareSnapshot FromFastbootData(FastbootData data, string serial, string? port)
        {
            DeviceHardwareSnapshot snapshot = FromVars(data.bootloader_vars, serial, port, null);
            if (!string.IsNullOrWhiteSpace(data.product))
            {
                snapshot.Codename = data.product;
                if (string.IsNullOrWhiteSpace(snapshot.Model))
                    snapshot.Model = ResolveMarketingName(data.bootloader_vars, data.product);
            }

            if (string.IsNullOrWhiteSpace(snapshot.Storage))
                snapshot.Storage = StorageTypeResolver.Resolve(data.bootloader_vars);

            if (string.IsNullOrWhiteSpace(snapshot.FirmwareVersion))
                snapshot.FirmwareVersion = FirmwareVersionResolver.Resolve(data.bootloader_vars);

            if (string.IsNullOrWhiteSpace(snapshot.BootSlot) && data.current_slot != null)
                snapshot.BootSlot = BootSlotResolver.FormatSlot(data.current_slot);

            snapshot.SecureBoot = data.secure ? "enabled" : "disabled";
            return snapshot;
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
