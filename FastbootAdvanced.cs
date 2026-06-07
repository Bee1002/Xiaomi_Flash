using System;
using System.Windows;
using Xiaomi_Flash.Ui;

namespace Xiaomi_Flash
{
    /// <summary>
    /// Acciones del menú ADVANCED (UI nueva). Reutiliza el runner fastboot legacy.
    /// </summary>
    static class FastbootAdvanced
    {
        public static void Init()
        {
            if (MainWindow.THIS.ui_advanced_reset_efs != null)
                MainWindow.THIS.ui_advanced_reset_efs.Click += delegate { RunResetEfs(); HideMenu(); };

            if (MainWindow.THIS.ui_advanced_fix_brick != null)
                MainWindow.THIS.ui_advanced_fix_brick.Click += delegate { RunFixBrick(); HideMenu(); };

            if (MainWindow.THIS.ui_advanced_slot_ab != null)
                MainWindow.THIS.ui_advanced_slot_ab.Click += delegate { RunSwitchSlot(); HideMenu(); };
        }

        public static void HideMenu()
        {
            if (MainWindow.THIS.ui_advanced_menu != null)
                MainWindow.THIS.ui_advanced_menu.Visibility = Visibility.Collapsed;
        }

        static void RunResetEfs()
        {
            if (!FastbootUI.EnsureFastbootDevice(out string serial))
                return;

            MessageBoxResult confirm = MessageBox.Show(
                "Se borrará la partición persist (EFS / radio).\n" +
                "Puede perder señal, IMEI o datos de red.\n\n¿Continuar?",
                "Reset EFS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            TerminalLog.Action("Reset EFS: erase persist");
            FastbootUI.RunStepCommand(serial, "erase persist", 2, true, true);
        }

        static void RunFixBrick()
        {
            if (!FastbootUI.EnsureFastbootDevice(out string serial))
                return;

            MessageBoxResult confirm = MessageBox.Show(
                "Intento de recuperación: erase misc + reboot bootloader.\n" +
                "No sustituye un flash completo si el equipo está brickeado.\n\n¿Continuar?",
                "Fix / Brick",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            TerminalLog.Action("Fix/Brick: erase misc");
            FastbootUI.RunStepCommand(serial, "erase misc", 2, false, true, delegate
            {
                FastbootUI.RunStepCommand(serial, "reboot bootloader", 0, false, true);
            });
        }

        static void RunSwitchSlot()
        {
            if (!FastbootUI.EnsureFastbootDevice(out string serial))
                return;

            string? current = null;
            if (FastbootAutoProbe.TryGetCached(serial, out DeviceHardwareSnapshot? cached) && cached != null)
                current = cached.BootSlot;

            if (string.IsNullOrWhiteSpace(current))
                current = BootSlotResolver.FormatSlot(FastbootVarReader.GetVar(serial, "current-slot"));

            string cmd;
            string label;
            if (current == "A")
            {
                cmd = "set_active b";
                label = "B";
            }
            else if (current == "B")
            {
                cmd = "set_active a";
                label = "A";
            }
            else
            {
                TerminalLog.Error("Slot A/B no disponible en este dispositivo");
                MessageBox.Show("Este dispositivo no reporta slots A/B activos.");
                return;
            }

            TerminalLog.Action("Switch active slot -> " + label);
            FastbootUI.RunStepCommand(serial, cmd, 2, false, true, delegate
            {
                FastbootAutoProbe.PatchCachedBootSlot(serial, label);
                FastbootUI.RefreshConnectionPanel();
            });
        }
    }
}
