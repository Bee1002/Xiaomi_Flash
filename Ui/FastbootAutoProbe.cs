using System;
using System.Threading;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Lee getvar all (+ anti / oem hwid) al detectar fastboot para el panel de hardware.
    /// </summary>
    internal static class FastbootAutoProbe
    {
        static string? cachedSerial;
        static DeviceHardwareSnapshot? cachedSnapshot;
        static volatile bool probing;

        public static bool IsProbing => probing;

        public static void Reset()
        {
            cachedSerial = null;
            cachedSnapshot = null;
            probing = false;
        }

        public static bool TryGetCached(string serial, out DeviceHardwareSnapshot? snapshot)
        {
            if (cachedSerial != null
                && cachedSerial.Equals(serial, StringComparison.OrdinalIgnoreCase)
                && cachedSnapshot != null)
            {
                snapshot = cachedSnapshot;
                return true;
            }

            snapshot = null;
            return false;
        }

        public static void PatchCachedBootSlot(string serial, string? slot)
        {
            if (cachedSnapshot == null || cachedSerial == null)
                return;
            if (!cachedSerial.Equals(serial, StringComparison.OrdinalIgnoreCase))
                return;
            cachedSnapshot.BootSlot = slot;
        }

        public static void Request(string serial, Action onComplete)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return;

            if (FastbootGate.BlocksBackgroundPoll && !probing)
                return;

            if (cachedSerial != null
                && cachedSerial.Equals(serial, StringComparison.OrdinalIgnoreCase)
                && cachedSnapshot != null
                && cachedSnapshot.HasCoreInfo
                && !string.IsNullOrWhiteSpace(cachedSnapshot.Port))
            {
                onComplete();
                return;
            }

            if (probing
                && cachedSerial != null
                && cachedSerial.Equals(serial, StringComparison.OrdinalIgnoreCase))
                return;

            if (cachedSerial == null || !cachedSerial.Equals(serial, StringComparison.OrdinalIgnoreCase))
                cachedSnapshot = null;

            cachedSerial = serial;
            probing = true;

            new Thread(new ThreadStart(delegate
            {
                DeviceHardwareSnapshot? snapshot = null;

                try
                {
                    string? port = UsbDeviceLocator.FindPortBySerial(serial);
                    string stderr = FastbootAllVars.ReadAll(serial);
                    var vars = FastbootAllVars.Parse(stderr);

                    string? anti = vars.ContainsKey("anti")
                        ? vars["anti"]
                        : FastbootVarReader.GetVar(serial, "anti");

                    snapshot = DeviceInfoMapper.FromVars(vars, serial, port, anti);

                    if (string.IsNullOrWhiteSpace(snapshot.Storage))
                    {
                        string? variant = FastbootVarReader.GetVar(serial, "variant");
                        snapshot.Storage = StorageTypeResolver.FromVariant(variant);
                    }

                    if (string.IsNullOrWhiteSpace(snapshot.BootSlot))
                        snapshot.BootSlot = BootSlotResolver.FormatSlot(FastbootVarReader.GetVar(serial, "current-slot"));

                    try
                    {
                        string hwid = FastbootAllVars.TryReadOemCommand(serial, "oem hwid");
                        DeviceInfoMapper.ApplyOemHwid(snapshot, hwid);
                    }
                    catch (Exception) { }

                    try
                    {
                        string deviceInfo = FastbootAllVars.TryReadOemCommand(serial, "oem device-info");
                        DeviceInfoMapper.ApplyOemDeviceInfo(snapshot, deviceInfo);
                    }
                    catch (Exception) { }
                }
                catch (Exception) { }
                finally
                {
                    cachedSerial = serial;
                    cachedSnapshot = snapshot;
                    probing = false;

                    MainWindow.THIS?.Dispatcher.BeginInvoke(new Action(onComplete));
                }
            })).Start();
        }
    }
}
