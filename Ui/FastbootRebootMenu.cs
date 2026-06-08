using System;
using System.Windows;
using Xiaomi_Flash.Ui;

namespace Xiaomi_Flash
{
    /// <summary>
    /// Menú REBOOT de la UI nueva (system / fastboot / recovery).
    /// </summary>
    static class FastbootRebootMenu
    {
        public static void Init()
        {
            if (MainWindow.THIS.ui_reboot_system != null)
                MainWindow.THIS.ui_reboot_system.Click += delegate { RunRebootSystem(); HideMenu(); };

            if (MainWindow.THIS.ui_reboot_fastboot != null)
                MainWindow.THIS.ui_reboot_fastboot.Click += delegate { RunRebootFastboot(); HideMenu(); };

            if (MainWindow.THIS.ui_reboot_recovery != null)
                MainWindow.THIS.ui_reboot_recovery.Click += delegate { RunRebootRecovery(); HideMenu(); };
        }

        public static void HideMenu()
        {
            if (MainWindow.THIS.ui_reboot_menu != null)
                MainWindow.THIS.ui_reboot_menu.Visibility = Visibility.Collapsed;
        }

        static void RunRebootSystem()
        {
            if (!FastbootUI.EnsureFastbootDevice(out string serial))
                return;

            TerminalLog.Action("Reboot -> system");
            FastbootUI.RunStepCommand(serial, "reboot", 0, false, true);
        }

        static void RunRebootRecovery()
        {
            if (!FastbootUI.EnsureFastbootDevice(out string serial))
                return;

            TerminalLog.Action("Reboot -> recovery");
            FastbootUI.RunStepCommand(serial, "reboot recovery", 0, false, true);
        }

        static void RunRebootFastboot()
        {
            if (!FastbootUI.EnsureFastbootDevice(out string serial))
                return;

            bool fastbootd = FastbootUI.IsDeviceFastbootd(serial);
            string cmd = fastbootd ? "reboot bootloader" : "reboot fastboot";
            string label = fastbootd ? "bootloader" : "fastboot";

            TerminalLog.Action("Reboot -> " + label);
            FastbootUI.RunStepCommand(serial, cmd, fastbootd ? 2 : 3, false, true);
        }
    }
}
