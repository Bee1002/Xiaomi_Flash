using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Xiaomi_Flash.Ui;

namespace Xiaomi_Flash
{
    /// <summary>
    /// Acciones del menú ADVANCED en la UI v2. Reutiliza el ejecutor de comandos fastboot legacy.
    /// </summary>
    static class FastbootAdvanced
    {
        static readonly string[] QualcommEfsPartitions = { "modemst1", "modemst2", "fsg", "fsc" };

        public static void Init()
        {
            if (MainWindow.THIS.ui_advanced_reset_efs != null)
                MainWindow.THIS.ui_advanced_reset_efs.Click += delegate { RunEraseEfsQualcomm(); HideMenu(); };

            if (MainWindow.THIS.ui_advanced_fix_brick != null)
                MainWindow.THIS.ui_advanced_fix_brick.Click += delegate { RunFixBrick(); HideMenu(); };

            if (MainWindow.THIS.ui_advanced_fix_oem != null)
                MainWindow.THIS.ui_advanced_fix_oem.Click += delegate { RunFixOem(); HideMenu(); };

            if (MainWindow.THIS.ui_advanced_slot_ab != null)
                MainWindow.THIS.ui_advanced_slot_ab.Click += delegate { RunSwitchSlot(); HideMenu(); };
        }

        public static void HideMenu()
        {
            if (MainWindow.THIS.ui_advanced_menu != null)
                MainWindow.THIS.ui_advanced_menu.Visibility = Visibility.Collapsed;
        }

        static void RunEraseEfsQualcomm()
        {
            if (FastbootFlashSession.IsFlashing)
                return;

            if (!FastbootDeviceService.EnsureFastbootDevice(out string serial))
                return;

            MessageBoxResult confirm = MessageBox.Show(
                "WARNING — Qualcomm EFS / IMEI data will be erased.\n\n"
                + "For Qualcomm Xiaomi devices only. MTK models do not use these partitions.\n\n"
                + "This runs:\n"
                + "  erase modemst1\n"
                + "  erase modemst2\n"
                + "  erase fsg\n"
                + "  erase fsc\n"
                + "  reboot\n\n"
                + "You may lose IMEI, signal, and network until restored from backup.\n"
                + "Requires unlocked bootloader.\n\n"
                + "Are you sure you want to continue?",
                "Erase EFS / Qualcomm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (!FlashPrecheck.ConfirmBootloaderUnlocked(serial, "Erase EFS / Qualcomm"))
                return;

            TerminalLog.Action("Erase EFS/Qualcomm: modem partition erase chain");
            RunEraseEfsQualcommChain(serial, 0);
        }

        static void RunEraseEfsQualcommChain(string serial, int index)
        {
            if (index >= QualcommEfsPartitions.Length)
            {
                TerminalLog.Action("Erase EFS/Qualcomm: reboot");
                FastbootDeviceService.RunStepCommandChecked(serial, "reboot", 0, true, true, delegate (bool success)
                {
                    if (!success)
                        TerminalLog.Error("Erase EFS/Qualcomm: reboot failed");
                });
                return;
            }

            string partition = QualcommEfsPartitions[index];
            TerminalLog.Action("Erase EFS/Qualcomm: erase " + partition);
            FastbootDeviceService.RunStepCommandChecked(serial, "erase \"" + partition + "\"", 1, false, true, delegate (bool success)
            {
                if (!success)
                {
                    TerminalLog.Error("Erase EFS/Qualcomm failed at " + partition + " — chain stopped");
                    return;
                }

                RunEraseEfsQualcommChain(serial, index + 1);
            });
        }

        static void RunFixBrick()
        {
            if (FastbootFlashSession.IsFlashing)
                return;

            if (!FastbootDeviceService.EnsureFastbootDevice(out string serial))
                return;

            MessageBoxResult confirm = MessageBox.Show(
                "Recovery attempt: erase misc + reboot bootloader.\n\n"
                + "Clears pending OTA / recovery flags. Does not replace a full ROM flash.\n"
                + "Works best with unlocked bootloader.\n\nContinue?",
                "Fix / Brick",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (!FlashPrecheck.ConfirmBootloaderUnlocked(serial, "Fix / Brick"))
                return;

            TerminalLog.Action("Fix/Brick: erase misc");
            FastbootDeviceService.RunStepCommandChecked(serial, "erase misc", 2, false, true, delegate (bool success)
            {
                if (!success)
                {
                    TerminalLog.Error("Fix/Brick: erase misc failed — reboot skipped");
                    return;
                }

                TerminalLog.Action("Fix/Brick: reboot bootloader");
                FastbootDeviceService.RunStepCommandChecked(serial, "reboot bootloader", 0, false, true, delegate (bool rebootOk)
                {
                    if (!rebootOk)
                        TerminalLog.Error("Fix/Brick: reboot bootloader failed");
                });
            });
        }

        static void RunFixOem()
        {
            if (FastbootFlashSession.IsFlashing)
                return;

            if (!FastbootDeviceService.EnsureFastbootDevice(out string serial))
                return;

            MessageBoxResult confirm = MessageBox.Show(
                "Fix AVB / \"Your device is corrupted\":\n\n"
                + "Flashes vbmeta with --disable-verity --disable-verification.\n"
                + "Requires unlocked bootloader.\n"
                + "On A/B devices, vbmeta_a and vbmeta_b are flashed.\n\nContinue?",
                "Fix / OEM",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (!FlashPrecheck.ConfirmBootloaderUnlocked(serial, "Fix / OEM"))
                return;

            string vbmetaPath = RomFlashScanner.FindVbmetaImage(FastbootFlashSession.LoadedRomRoot);
            if (string.IsNullOrEmpty(vbmetaPath) || !File.Exists(vbmetaPath))
            {
                Helper.fileSelect(delegate (string path)
                {
                    if (!File.Exists(path))
                        return;
                    RunFixOemFlash(serial, path);
                }, "vbmeta image|vbmeta.img;*.img");
                return;
            }

            RunFixOemFlash(serial, vbmetaPath);
        }

        static void RunFixOemFlash(string serial, string vbmetaPath)
        {
            List<string> targets = ResolveVbmetaTargets(serial);
            TerminalLog.Action("Fix/OEM: vbmeta disable-verity (" + targets.Count + " partition(s))");
            FlashVbmetaChain(serial, vbmetaPath, targets, 0);
        }

        static List<string> ResolveVbmetaTargets(string serial)
        {
            FastbootData data = null;
            if (!FastbootDeviceDataCache.TryGetCached(serial, out data))
            {
                try
                {
                    data = FastbootDeviceDataCache.GetOrLoad(serial);
                }
                catch (Exception)
                {
                    data = null;
                }
            }

            if (data != null)
            {
                bool hasSlotA = data.partition_size.ContainsKey("vbmeta_a");
                bool hasSlotB = data.partition_size.ContainsKey("vbmeta_b");
                if (hasSlotA && hasSlotB)
                    return new List<string> { "vbmeta_a", "vbmeta_b" };

                if (data.current_slot != null)
                {
                    string slot = data.current_slot.Trim().ToLowerInvariant();
                    if (data.partition_size.ContainsKey("vbmeta_" + slot))
                        return new List<string> { "vbmeta_" + slot };
                }
            }

            return new List<string> { "vbmeta" };
        }

        static void FlashVbmetaChain(string serial, string vbmetaPath, List<string> targets, int index)
        {
            if (index >= targets.Count)
            {
                TerminalLog.Action("Fix/OEM: reboot");
                FastbootDeviceService.RunStepCommandChecked(serial, "reboot", 0, true, true, delegate (bool rebootOk)
                {
                    if (!rebootOk)
                        TerminalLog.Error("Fix/OEM: reboot failed");
                });
                return;
            }

            string partition = targets[index];
            string cmd = "flash --disable-verity --disable-verification \""
                + partition + "\" \"" + vbmetaPath + "\"";

            FastbootDeviceService.RunStepCommandChecked(serial, cmd, 1, false, true, delegate (bool success)
            {
                if (!success)
                {
                    TerminalLog.Error("Fix/OEM failed at " + partition + " — chain stopped");
                    return;
                }

                TerminalLog.StepResult(partition, true);
                FlashVbmetaChain(serial, vbmetaPath, targets, index + 1);
            });
        }

        static void RunSwitchSlot()
        {
            if (FastbootFlashSession.IsFlashing)
                return;

            if (!FastbootDeviceService.EnsureFastbootDevice(out string serial))
                return;

            string current = null;
            if (FastbootAutoProbe.TryGetCached(serial, out DeviceHardwareSnapshot cached) && cached != null)
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
                TerminalLog.Error("Slot A/B not available on this device");
                MessageBox.Show(
                    "This device does not report active A/B slots.",
                    "Slot A/B",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                "Switch active boot slot from " + current + " to " + label + "?\n\n"
                + "The device will use slot " + label + " on the next reboot.\n"
                + "Requires unlocked bootloader on most Xiaomi devices.\n\nContinue?",
                "Slot A/B",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (!FlashPrecheck.ConfirmBootloaderUnlocked(serial, "Slot A/B"))
                return;

            TerminalLog.Action("Switch active slot -> " + label);
            FastbootDeviceService.RunStepCommandChecked(serial, cmd, 2, false, true, delegate (bool success)
            {
                if (!success)
                {
                    TerminalLog.Error("Slot A/B: switch failed — check bootloader state");
                    return;
                }

                FastbootAutoProbe.PatchCachedBootSlot(serial, label);
                FastbootDeviceService.RefreshConnectionPanel();
            });
        }
    }
}
