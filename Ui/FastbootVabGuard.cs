using System.Windows;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    /// <summary>
    /// Comprueba estado Virtual A/B antes de flashear (snapshot staging / particiones cow).
    /// Devuelve true si el usuario canceló o no debe continuar.
    /// </summary>
    internal static class FastbootVabGuard
    {
        public static bool BlocksFlash(FastbootData data)
        {
            if (data.snapshot_update_status != null
                && data.snapshot_update_status != "none")
            {
                MessageBoxResult result = MessageBox.Show(
                    Properties.Resources.fastboot_vab_staging_str1 + "\n"
                    + Properties.Resources.fastboot_vab_staging_str2 + "\n"
                    + Properties.Resources.fastboot_vab_staging_str3,
                    Properties.Resources.fastboot_vab_staging_str0,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return true;
            }

            bool cowExist = false;
            foreach (string key in data.partition_size.Keys)
            {
                if (key.EndsWith("cow", StringComparison.OrdinalIgnoreCase))
                {
                    cowExist = true;
                    break;
                }
            }

            if (!cowExist)
                return false;

            MessageBoxResult cowResult = MessageBox.Show(
                Properties.Resources.fastboot_cow_exist_str1 + "\n"
                + Properties.Resources.fastboot_cow_exist_str2 + "\n"
                + Properties.Resources.fastboot_cow_exist_str3,
                Properties.Resources.fastboot_cow_exist_str0,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return cowResult != MessageBoxResult.Yes;
        }
    }
}
