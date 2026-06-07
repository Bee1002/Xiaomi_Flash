using System;
using System.Windows;
using System.Windows.Media;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Actualiza el panel [ DEVICE CONNECTION ] de la UI nueva.
    /// </summary>
    internal static class DeviceConnectionUi
    {
        public static void SetScanning()
        {
            RunOnUi(delegate
            {
                SetStatus("SCANNING...", Brushes.White);
                SetPort(null);
                SetHardwareDetails(null);
            });
        }

        public static void SetNoDevice()
        {
            RunOnUi(delegate
            {
                SetStatus("NO DEVICE DETECTED", new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)));
                SetPort(null);
                SetHardwareDetails(null);
            });
        }

        public static void SetWrongMode(string mode)
        {
            RunOnUi(delegate
            {
                SetStatus(mode.ToUpperInvariant() + " (NOT FASTBOOT)", new SolidColorBrush(Color.FromRgb(0xCC, 0xAA, 0x44)));
                SetPort(null);
                SetHardwareDetails(null);
            });
        }

        public static void SetFastbootDetected(DeviceHardwareSnapshot? snapshot, bool fastbootd)
        {
            RunOnUi(delegate
            {
                string status = fastbootd ? "FASTBOOTD MODE DETECTED" : "FASTBOOT MODE DETECTED";
                SetStatus(status, Brushes.White);
                SetPort(snapshot?.Port);
                SetHardwareDetails(snapshot);
            });
        }

        static void SetStatus(string value, Brush foreground)
        {
            if (MainWindow.THIS.ui_device_status == null)
                return;
            MainWindow.THIS.ui_device_status.Text = "STATUS: " + value;
            MainWindow.THIS.ui_device_status.Foreground = foreground;
        }

        static void SetPort(string? port)
        {
            if (MainWindow.THIS.ui_device_port == null)
                return;
            MainWindow.THIS.ui_device_port.Text = string.IsNullOrWhiteSpace(port)
                ? "PORT:   [ — ]"
                : $"PORT:   [ {port} ]";
        }

        static void SetHardwareDetails(DeviceHardwareSnapshot? snapshot)
        {
            if (MainWindow.THIS.ui_device_details == null)
                return;

            if (snapshot == null)
            {
                MainWindow.THIS.ui_device_details.Text =
                    FormatRow("Serial", null)
                    + FormatRow("Modelo", null)
                    + FormatRow("Codename", null)
                    + FormatRow("Almacenamiento", null)
                    + FormatRow("CPU ID", null)
                    + FormatRow("HW revision", null)
                    + FormatRow("Secure boot", null)
                    + FormatRow("Bootloader", null)
                    + FormatRow("Boot slot", null)
                    + FormatRow("Anti-rollback", null)
                    + FormatRow("Firmware", null);
                return;
            }

            MainWindow.THIS.ui_device_details.Text =
                FormatRow("Serial", snapshot.Serial)
                + FormatRow("Modelo", snapshot.Model)
                + FormatRow("Codename", snapshot.Codename)
                + FormatRow("Almacenamiento", snapshot.Storage)
                + FormatRow("CPU ID", snapshot.CpuId)
                + FormatRow("HW revision", snapshot.HwRevision)
                + FormatRow("Secure boot", snapshot.SecureBoot)
                + FormatRow("Bootloader", snapshot.Bootloader)
                + FormatRow("Boot slot", snapshot.BootSlot)
                + FormatRow("Anti-rollback", snapshot.AntiRollback)
                + FormatRow("Firmware", snapshot.FirmwareVersion);
        }

        static string FormatRow(string label, string? value)
        {
            string display = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
            return label.PadRight(18) + display + Environment.NewLine;
        }

        static void RunOnUi(Action action)
        {
            if (MainWindow.THIS == null)
                return;
            if (MainWindow.THIS.Dispatcher.CheckAccess())
                action();
            else
                MainWindow.THIS.Dispatcher.Invoke(action);
        }
    }
}
