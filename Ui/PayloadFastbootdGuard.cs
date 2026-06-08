using System.Windows;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// payload.bin suele requerir fastbootd; advierte si el dispositivo está en fastboot clásico.
    /// Devuelve true si el usuario canceló y no debe continuar.
    /// </summary>
    internal static class PayloadFastbootdGuard
    {
        public static bool BlocksPayloadFlash(string serial)
        {
            if (FastbootDeviceService.IsDeviceFastbootd(serial))
                return false;

            MessageBoxResult result = MessageBox.Show(
                "payload.bin flashing usually requires fastbootd (userspace fastboot).\n\n"
                + "Your device is in classic fastboot (bootloader) mode.\n"
                + "Use Reboot → Fastbootd from the menu, wait for reconnection, then start again.\n\n"
                + "Flash anyway? (not recommended — may fail on many devices)",
                "Fastbootd recommended",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return result != MessageBoxResult.Yes;
        }
    }
}
