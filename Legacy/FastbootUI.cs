// UI legacy fastboot: poll de dispositivos, handlers del host oculto en MainWindow.xaml.
// La API pública v2 está en Ui/FastbootDeviceService.cs.
#nullable disable
using ChromeosUpdateEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using Xiaomi_Flash.Ui;

namespace Xiaomi_Flash
{
    internal class FastbootUI
    {
        public const string PAYLOAD_TMP = ".\\payload.tmp.fastboot";
        static List<fastboot_devices_row> devices;
        static string cur_serial;
        static FastbootData fastbootData;

        static void appendLog(string logs)
        {
            TerminalLog.Append("FASTBOOT", logs);
        }

        enum FastbootStatus
        {
            show_devices,
            show_actions
        }

        static FastbootStatus cur_status;
        static string lastTerminalLoggedSerial;
        const int DisconnectGracePolls = 3;
        static int consecutiveEmptyPolls;
        static void refreshDeviceList()
        {
            MainWindow.THIS.Dispatcher.Invoke(new Action(delegate
            {
                MainWindow.THIS.fastboot_devices_list.Items.Clear();
                foreach (fastboot_devices_row row in devices)
                {
                    MainWindow.THIS.fastboot_devices_list.Items.Add(row);
                }
            }));
            updateConnectionPanelFromDevices();
        }

        static bool IsFastbootMode(string mode)
        {
            return mode.Equals("fastboot", StringComparison.OrdinalIgnoreCase)
                || mode.Equals("fastbootd", StringComparison.OrdinalIgnoreCase);
        }

        static void updateConnectionPanelFromDevices()
        {
            if (devices == null || devices.Count == 0)
            {
                if (FastbootGate.BlocksBackgroundPoll || consecutiveEmptyPolls < DisconnectGracePolls)
                    return;

                if (lastTerminalLoggedSerial != null)
                {
                    TerminalLog.Info("Device: disconnected");
                    lastTerminalLoggedSerial = null;
                }

                FastbootAutoProbe.Reset();
                FastbootDeviceDataCache.Invalidate();
                DeviceConnectionUi.SetNoDevice();
                FastbootFlashSession.UpdateButtonState();
                return;
            }

            consecutiveEmptyPolls = 0;

            fastboot_devices_row fbDevice = null;
            if (cur_serial != null)
            {
                foreach (fastboot_devices_row row in devices)
                {
                    if (row.serial == cur_serial && IsFastbootMode(row.name))
                    {
                        fbDevice = row;
                        break;
                    }
                }
            }

            if (fbDevice == null)
            {
                foreach (fastboot_devices_row row in devices)
                {
                    if (IsFastbootMode(row.name))
                    {
                        fbDevice = row;
                        break;
                    }
                }
            }

            if (fbDevice == null)
            {
                if (lastTerminalLoggedSerial != null)
                {
                    TerminalLog.Info("Device: disconnected");
                    lastTerminalLoggedSerial = null;
                }

                FastbootAutoProbe.Reset();
                FastbootDeviceDataCache.Invalidate();
                DeviceConnectionUi.SetWrongMode(devices[0].name);
                FastbootFlashSession.UpdateButtonState();
                return;
            }

            if (lastTerminalLoggedSerial != fbDevice.serial)
            {
                lastTerminalLoggedSerial = fbDevice.serial;
                TerminalLog.Info("Device: connected");
            }

            bool fastbootd = fbDevice.name.Equals("fastbootd", StringComparison.OrdinalIgnoreCase);
            DeviceHardwareSnapshot snapshot = null;

            if (cur_serial == fbDevice.serial && fastbootData != null)
                snapshot = DeviceInfoMapper.FromFastbootData(fastbootData, fbDevice.serial, UsbDeviceLocator.FindPortBySerial(fbDevice.serial));

            if (snapshot == null && FastbootAutoProbe.TryGetCached(fbDevice.serial, out DeviceHardwareSnapshot cached))
                snapshot = cached;

            if (snapshot == null)
                snapshot = new DeviceHardwareSnapshot { Serial = fbDevice.serial };

            DeviceConnectionUi.SetFastbootDetected(snapshot, fastbootd);

            bool needsProbe = !snapshot.HasCoreInfo || string.IsNullOrWhiteSpace(snapshot.Port);
            if (needsProbe && cur_status == FastbootStatus.show_devices)
                FastbootAutoProbe.Request(fbDevice.serial, updateConnectionPanelFromDevices);

            FastbootFlashSession.UpdateButtonState();
        }

        public static bool HasFastbootDevice()
        {
            return findFirstFastbootDevice() != null;
        }

        public static bool IsDeviceFastbootd(string serial)
        {
            if (devices == null || string.IsNullOrWhiteSpace(serial))
                return false;

            foreach (fastboot_devices_row row in devices)
            {
                if (row.serial.Equals(serial, StringComparison.OrdinalIgnoreCase)
                    && IsFastbootMode(row.name))
                    return row.name.Equals("fastbootd", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public static void AppendTerminalLog(string line)
        {
            TerminalLog.FastbootOutput(line);
        }

        static fastboot_devices_row findFirstFastbootDevice()
        {
            foreach (fastboot_devices_row row in devices)
            {
                if (IsFastbootMode(row.name))
                    return row;
            }
            return null;
        }

        static void connectToDevice(fastboot_devices_row row)
        {
            cur_serial = row.serial;
            if (!checkCurDevExist())
                return;

            cur_status = FastbootStatus.show_actions;
            MainWindow.THIS.fastboot_cur_device.Content = Properties.Resources.fastboot_current_device + row.serial;
            TerminalLog.Action("Connected to " + row.serial);
            change_page();
        }

        static bool checkCurDevExist()
        {
            lock (FastbootGate.Sync)
            {
                using (Fastboot fastboot = new Fastboot(null, "devices"))
                {
                    while (true)
                    {
                        string line = fastboot.stdout.ReadLine();
                        if (line == null)
                            break;

                        string[] param = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (param.Length >= 1 && cur_serial == param[0])
                            return true;
                    }
                }
            }

            MessageBox.Show(Properties.Resources.fastboot_device_not_exist);
            cur_status = FastbootStatus.show_devices;
            change_page();
            return false;
        }

        static void devicesListRefresher()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (cur_status == FastbootStatus.show_actions)
                    continue;

                if (FastbootGate.BlocksBackgroundPoll)
                    continue;

                List<fastboot_devices_row> tmp = new List<fastboot_devices_row>();

                lock (FastbootGate.Sync)
                {
                    using (Fastboot fastboot = new Fastboot(null, "devices"))
                    {
                        while (true)
                        {
                            string line = fastboot.stdout.ReadLine();
                            if (line == null)
                                break;

                            string[] param = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (param.Length < 2)
                                continue;
                            tmp.Add(new fastboot_devices_row(param[0], param[1]));
                        }
                    }
                }

                if (tmp.Count == 0)
                    consecutiveEmptyPolls++;
                else
                    consecutiveEmptyPolls = 0;

                if (tmp.Count != devices.Count)
                {
                    devices = tmp;
                    refreshDeviceList();
                }
                else
                {
                    int i;
                    for (i = 0; i < tmp.Count; i++)
                    {
                        if (devices[i].name != tmp[i].name || devices[i].serial != tmp[i].serial)
                            break;
                    }
                    if (i != tmp.Count)
                    {
                        devices = tmp;
                        refreshDeviceList();
                    }
                    else
                    {
                        updateConnectionPanelFromDevices();
                    }
                }
            }
        }

        class fastboot_devices_row
        {
            public string serial { get; }
            public string name { get; }
            public fastboot_devices_row(string serial, string name)
            {
                this.serial = serial;
                this.name = name;
            }
        }

        class fastboot_info_row
        {
            public string name { get; }
            public string value { get; }
            public fastboot_info_row(string name, string value)
            {
                this.name = name;
                this.value = value;
            }
        }

        class fastboot_partition_row
        {
            public string name { get; }
            public string size { get; }
            public string is_logical { get; }
            public fastboot_partition_row(string name, string size, string is_logical)
            {
                this.name = name;
                this.size = size;
                this.is_logical = is_logical;
            }
        }

        static void action_lock()
        {
            MainWindow.THIS.fastboot_progress_bar.Value = 0;
            MainWindow.THIS.fastboot_action_bar.Visibility = Visibility.Hidden;
            MainWindow.THIS.fastboot_progress_bar.Visibility = Visibility.Visible;
            MainWindow.THIS.fastboot_progress_bar.IsIndeterminate = false;
            Helper.TaskbarItemHelper.start();
            MainWindow.THIS.fastboot_single_part_op.IsEnabled = false;
            MainWindow.THIS.fastboot_flash_payload.IsEnabled = false;
        }

        static void action_unlock()
        {
            MainWindow.THIS.fastboot_progress_bar.Visibility = Visibility.Hidden;
            MainWindow.THIS.fastboot_action_bar.Visibility = Visibility.Visible;
            Helper.TaskbarItemHelper.stop();
            MainWindow.THIS.fastboot_single_part_op.IsEnabled = true;
            MainWindow.THIS.fastboot_flash_payload.IsEnabled = true;
        }

        static Helper.ListHelper<fastboot_partition_row> listHelper;

        static void load_fastboot_vars()
        {
            // Reiniciar estado antes de cargar variables fastboot
            FastbootDeviceDataCache.Invalidate(cur_serial);
            fastbootData = null;
            listHelper.clear();
            MainWindow.THIS.fastboot_partition_name_textbox.Text = "";
            MainWindow.THIS.fastboot_info_list.Items.Clear();
            action_lock();
            MainWindow.THIS.fastboot_progress_bar.IsIndeterminate = true;

            TerminalLog.Action("Reading device properties (getvar all)...");
            MainWindow.THIS.Dispatcher.Invoke(delegate
            {
                if (MainWindow.THIS.ui_current_operation != null)
                    MainWindow.THIS.ui_current_operation.Text = "Reading device info...";
                if (MainWindow.THIS.ui_main_progress_bar != null)
                    MainWindow.THIS.ui_main_progress_bar.IsIndeterminate = true;
            });

            new Thread(new ThreadStart(delegate
            {
                FastbootGate.EnterCritical();
                try
                {
                    fastbootData = FastbootDeviceDataCache.GetOrLoad(cur_serial);
                }
                finally
                {
                    FastbootGate.ExitCritical();
                }

                MainWindow.THIS.Dispatcher.Invoke(delegate
                {
                    // Inicializar lista de particiones

                    foreach (string key in fastbootData.partition_size.Keys)
                    {
                        long raw_size = fastbootData.partition_size[key];
                        string size_str = raw_size >= 0 ? Helper.byte2AUnit((ulong)raw_size) : Properties.Resources.fastboot_0_size;
                        bool? raw_logical = null;
                        fastbootData.partition_is_logical.TryGetValue(key, out raw_logical);
                        string logical_str = raw_logical != null && raw_logical == true ? Properties.Resources.yes : Properties.Resources.no;
                        listHelper.addItem(new fastboot_partition_row(key, size_str, logical_str));
                    }
                    listHelper.render();

                    // Inicializar lista de propiedades del dispositivo

                    MainWindow.THIS.fastboot_info_list.Items.Add(new fastboot_info_row(Properties.Resources.fastboot_device, fastbootData.product));

                    MainWindow.THIS.fastboot_info_list.Items.Add(new fastboot_info_row(Properties.Resources.fastboot_secure_boot,
                        fastbootData.secure ? Properties.Resources.enabled : Properties.Resources.disabled));

                    MainWindow.THIS.fastboot_info_list.Items.Add(new fastboot_info_row(Properties.Resources.fastboot_seamless_update,
                        fastbootData.current_slot != null ? Properties.Resources.yes : Properties.Resources.no));

                    if (fastbootData.current_slot != null)
                        MainWindow.THIS.fastboot_info_list.Items.Add(new fastboot_info_row(Properties.Resources.fastboot_current_slot,
                            fastbootData.current_slot));

                    MainWindow.THIS.fastboot_info_list.Items.Add(new fastboot_info_row(Properties.Resources.fastboot_is_userspace,
                        fastbootData.fastbootd ? Properties.Resources.yes : Properties.Resources.no));

                    string vab_status_str = null;
                    switch (fastbootData.snapshot_update_status)
                    {
                        case "none":
                            vab_status_str = Properties.Resources.fastboot_update_status_none;
                            break;
                        case "snapshotted":
                            vab_status_str = Properties.Resources.fastboot_update_status_snapshotted;
                            break;
                        case "merging":
                            vab_status_str = Properties.Resources.fastboot_update_status_merging;
                            break;
                        default:
                            vab_status_str = fastbootData.snapshot_update_status;
                            break;
                    }

                    if (vab_status_str != null)
                        MainWindow.THIS.fastboot_info_list.Items.Add(new fastboot_info_row(Properties.Resources.fastboot_update_status,
                            vab_status_str));

                    // Inicializar botones según capacidades del dispositivo

                    MainWindow.THIS.fastboot_logical_create.IsEnabled = fastbootData.fastbootd;
                    MainWindow.THIS.fastboot_reboot_d.Content = fastbootData.fastbootd ?
                    Properties.Resources.fastboot_reboot_bootloader : Properties.Resources.fastboot_reboot_fastbootd;
                    if (fastbootData.current_slot != null)
                    {
                        MainWindow.THIS.fastboot_ab_switch.Visibility = Visibility.Visible;
                        if (fastbootData.current_slot == "a")
                        {
                            MainWindow.THIS.fastboot_ab_switch.Content = Properties.Resources.fastboot_setactive_b;
                        }
                        else if (fastbootData.current_slot == "b")
                        {
                            MainWindow.THIS.fastboot_ab_switch.Content = Properties.Resources.fastboot_setactive_a;
                        }
                    }
                    else
                    {
                        MainWindow.THIS.fastboot_ab_switch.Visibility = Visibility.Hidden;
                    }

                    // Comprobar si debe mostrarse el botón "Cancelar actualización pendiente"
                    if (fastbootData.snapshot_update_status == "none")
                    {
                        MainWindow.THIS.fastboot_cancel_update.Visibility = Visibility.Hidden;
                    }

                    MainWindow.THIS.fastboot_progress_bar.IsIndeterminate = false;
                    action_unlock();

                    DeviceHardwareSnapshot snapshot = DeviceInfoMapper.FromFastbootData(
                        fastbootData, cur_serial, UsbDeviceLocator.FindPortBySerial(cur_serial));
                    DeviceConnectionUi.SetFastbootDetected(snapshot, fastbootData.fastbootd);
                    TerminalLog.Action("Device info loaded");
                    if (MainWindow.THIS.ui_current_operation != null)
                        MainWindow.THIS.ui_current_operation.Text = "Device info loaded";
                    if (MainWindow.THIS.ui_main_progress_bar != null)
                    {
                        MainWindow.THIS.ui_main_progress_bar.IsIndeterminate = false;
                        MainWindow.THIS.ui_main_progress_bar.Value = 0;
                    }
                    if (MainWindow.THIS.ui_main_progress_text != null)
                        MainWindow.THIS.ui_main_progress_text.Text = "READY";
                });
            })).Start();
        }

        static void change_page()
        {
            switch (cur_status)
            {
                case FastbootStatus.show_devices:
                    MainWindow.THIS.fastboot_actions_page.Visibility = Visibility.Hidden;
                    MainWindow.THIS.fastboot_devices_page.Visibility = Visibility.Visible;
                    updateConnectionPanelFromDevices();
                    break;
                case FastbootStatus.show_actions:
                    load_fastboot_vars();
                    MainWindow.THIS.fastboot_devices_page.Visibility = Visibility.Hidden;
                    MainWindow.THIS.fastboot_actions_page.Visibility = Visibility.Visible;
                    break;
            }
        }

        class StepCmdRunnerParam
        {
            public string serial;
            public string cmd;
            public int step_count;
            public bool show_dialog_on_done;
            public bool skip_var_refresh;
            public Action on_complete;
            public StepCmdRunnerParam(string serial, string cmd, int step_count, bool hint_on_done, bool skip_var_refresh, Action on_complete)
            {
                this.serial = serial;
                this.cmd = cmd;
                this.step_count = step_count;
                this.show_dialog_on_done = hint_on_done;
                this.skip_var_refresh = skip_var_refresh;
                this.on_complete = on_complete;
            }
        }

        static void step_cmd_runner_err(object raw_param)
        {
            StepCmdRunnerParam param = (StepCmdRunnerParam)raw_param;

            FastbootGate.EnterCritical();
            try
            {
                MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
                {
                    action_lock();
                    if (param.step_count <= 0)
                        MainWindow.THIS.fastboot_progress_bar.IsIndeterminate = true;
                }));

                lock (FastbootGate.Sync)
                {
                    int count = 0;
                    Fastboot.Run(param.serial, param.cmd, delegate (string err)
                    {
                        appendLog(err);

                        if (param.step_count > 0)
                            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                MainWindow.THIS.fastboot_progress_bar.Value = ++count * 100 / param.step_count;
                                Helper.TaskbarItemHelper.update(count * 100 / param.step_count);
                            }));
                    }, Fastboot.GetRebootTimeoutMs(param.cmd));
                }
            }
            finally
            {
                FastbootGate.ExitCritical();
            }

            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (!param.skip_var_refresh)
                    load_fastboot_vars();
                if (param.show_dialog_on_done)
                    MessageBox.Show(Properties.Resources.operation_completed);
                param.on_complete?.Invoke();
            }));
        }

        public static void RefreshConnectionPanel()
        {
            updateConnectionPanelFromDevices();
        }

        public static bool EnsureFastbootDevice(out string serial)
        {
            serial = null;
            fastboot_devices_row row = findFirstFastbootDevice();
            if (row == null)
            {
                TerminalLog.Error("No fastboot device detected");
                MessageBox.Show(Properties.Resources.fastboot_device_not_exist);
                return false;
            }

            serial = row.serial;
            cur_serial = row.serial;
            return true;
        }

        public static void RunStepCommand(string serial, string cmd, int stepCount, bool showDialogOnDone, bool skipVarRefresh, Action onComplete = null)
        {
            new Thread(new ParameterizedThreadStart(step_cmd_runner_err))
                .Start(new StepCmdRunnerParam(serial, cmd, stepCount, showDialogOnDone, skipVarRefresh, onComplete));
        }

        static void runStep(string cmd, int stepCount, bool showDialogOnDone, bool skipVarRefresh = false)
        {
            RunStepCommand(cur_serial, cmd, stepCount, showDialogOnDone, skipVarRefresh);
        }

        static bool singlePartitionCheck()
        {
            if (MainWindow.THIS.fastboot_partition_list.SelectedItems.Count == 0)
            {
                MessageBox.Show(Properties.Resources.fastboot_target_partition_not_selected);
                return true;
            }

            if (MainWindow.THIS.fastboot_partition_list.SelectedItems.Count > 1)
            {
                MessageBox.Show(Properties.Resources.fastboot_not_support_multiselect);
                return true;
            }

            return false;
        }

        static bool logicalCheck()
        {
            bool? ret = null;
            fastbootData.partition_is_logical.TryGetValue(
                ((fastboot_partition_row)
                MainWindow.THIS.fastboot_partition_list.SelectedItem).name, out ret);
            if (ret == null || ret == false)
            {
                MessageBox.Show(Properties.Resources.fastboot_only_logical);
                return true;
            }

            return false;
        }

        static bool BlocksFlashForVab()
        {
            return fastbootData == null || FastbootVabGuard.BlocksFlash(fastbootData);
        }

        public static void init()
        {
            devices = new List<fastboot_devices_row>();
            cur_status = FastbootStatus.show_devices;
            DeviceConnectionUi.SetScanning();
            change_page();

            new Thread(new ThreadStart(devicesListRefresher)).Start();

            FastbootAdvanced.Init();
            FastbootRebootMenu.Init();
            FastbootFlashSession.Init();

            MainWindow.THIS.fastboot_devices_list.MouseDoubleClick += delegate
            {
                if (MainWindow.THIS.fastboot_devices_list.SelectedItems.Count == 0)
                    return;

                if (MainWindow.THIS.fastboot_devices_list.SelectedItems.Count > 1)
                {
                    MainWindow.THIS.fastboot_devices_list.SelectedItems.Clear();
                    return;
                }

                fastboot_devices_row cur = (fastboot_devices_row)MainWindow.THIS.fastboot_devices_list.SelectedItem;
                connectToDevice(cur);
            };

            MainWindow.THIS.fastboot_remove.Click += delegate
            {
                cur_serial = null;
                cur_status = FastbootStatus.show_devices;
                change_page();
            };

            MainWindow.THIS.fastboot_reboot_d.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                if (fastbootData.fastbootd)
                    runStep("reboot bootloader", 2, false);
                else
                    runStep("reboot fastboot", 3, false);
            };

            MainWindow.THIS.fastboot_reboot_system.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                runStep("reboot", 0, false, true);

                cur_serial = null;
                cur_status = FastbootStatus.show_devices;
                change_page();
            };

            MainWindow.THIS.fastboot_reboot_recovery.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                runStep("reboot recovery", 0, false, true);

                cur_serial = null;
                cur_status = FastbootStatus.show_devices;
                change_page();
            };

            MainWindow.THIS.fastboot_ab_switch.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                if (fastbootData.current_slot == "a")
                    runStep("set_active b", 2, false);
                else if (fastbootData.current_slot == "b")
                    runStep("set_active a", 2, false);
                else
                {
                    MessageBox.Show(Properties.Resources.operation_not_supported);
                }
            };

            // Escuchar clic del botón "Cancelar actualización pendiente"
            MainWindow.THIS.fastboot_cancel_update.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                runStep("snapshot-update cancel", 2, true);
            };

            MainWindow.THIS.fastboot_flash.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                if (singlePartitionCheck())
                    return;

                if (BlocksFlashForVab())
                    return;

                string target = ((fastboot_partition_row)MainWindow.THIS.fastboot_partition_list.SelectedItem).name;

                Helper.fileSelect(new Helper.PathSelectCallback(delegate (string path)
                {
                    string ext_arg = "";

                    if (target == "vbmeta" || target == "vbmeta_" + fastbootData.current_slot
                    || target == "vbmeta_a" || target == "vbmeta_b")
                    {
                        MessageBoxResult result = MessageBox.Show(
                            Properties.Resources.fastboot_vbmeta_disable_verify,
                            Properties.Resources.fastboot_vbmeta_disable_verify_title,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            ext_arg += "--disable-verity --disable-verification";
                        }
                    }
                    runStep("flash " + ext_arg + " \"" + target + "\" \"" + path + "\"", -1, true);
                }), "Image File|*.img;*.image");
            };

            MainWindow.THIS.fastboot_erase.Click += delegate
            {
                if (singlePartitionCheck())
                    return;

                string target = ((fastboot_partition_row)MainWindow.THIS.fastboot_partition_list.SelectedItem).name;

                runStep("erase \"" + target + "\"", 2, false);
            };

            MainWindow.THIS.fastboot_partition_list.SelectionChanged += delegate
            {
                MainWindow.THIS.fastboot_flash.IsEnabled = true;
                MainWindow.THIS.fastboot_erase.IsEnabled = true;
                MainWindow.THIS.fastboot_logical_delete.IsEnabled = true;
                MainWindow.THIS.fastboot_logical_resize.IsEnabled = true;

                if (MainWindow.THIS.fastboot_partition_list.SelectedItems.Count > 1)
                {
                    MainWindow.THIS.fastboot_flash.IsEnabled = false;
                    MainWindow.THIS.fastboot_erase.IsEnabled = false;
                    MainWindow.THIS.fastboot_logical_delete.IsEnabled = false;
                    MainWindow.THIS.fastboot_logical_resize.IsEnabled = false;
                    return;
                }

                if (fastbootData == null)
                    return;

                if (!fastbootData.fastbootd)
                {
                    MainWindow.THIS.fastboot_logical_delete.IsEnabled = false;
                    MainWindow.THIS.fastboot_logical_resize.IsEnabled = false;
                    return;
                }

                bool? ret = null;

                if (MainWindow.THIS.fastboot_partition_list.SelectedItem == null)
                {
                    MainWindow.THIS.fastboot_logical_delete.IsEnabled = true;
                    MainWindow.THIS.fastboot_logical_resize.IsEnabled = true;
                    return;
                }
                fastbootData.partition_is_logical.TryGetValue(
                    ((fastboot_partition_row)
                    MainWindow.THIS.fastboot_partition_list.SelectedItem).name, out ret);
                if (ret == null || ret == false)
                {
                    MainWindow.THIS.fastboot_logical_delete.IsEnabled = false;
                    MainWindow.THIS.fastboot_logical_resize.IsEnabled = false;
                }
            };

            MainWindow.THIS.fastboot_logical_delete.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                if (singlePartitionCheck() || logicalCheck())
                    return;

                string target = ((fastboot_partition_row)MainWindow.THIS.fastboot_partition_list.SelectedItem).name;

                runStep("delete-logical-partition \"" + target + "\"", 2, false);
            };

            MainWindow.THIS.fastboot_logical_create.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                new FastbootActionWindow(FastbootActionWindow.StartType.CREATE, "", 0,
                    delegate (string name, ulong size)
                   {
                       runStep("create-logical-partition \"" + name + "\" \"" + size.ToString() + "\"", 2, false);
                   }).ShowDialog();
            };

            MainWindow.THIS.fastboot_logical_resize.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                if (singlePartitionCheck() || logicalCheck())
                    return;

                string target = ((fastboot_partition_row)MainWindow.THIS.fastboot_partition_list.SelectedItem).name;

                new FastbootActionWindow(FastbootActionWindow.StartType.RESIZE, target,
                    fastbootData.partition_size[target],
                    delegate (string name, ulong size)
                    {
                        runStep("resize-logical-partition \"" + name + "\" \"" + size.ToString() + "\"", 2, false);
                    }).ShowDialog();
            };

            MainWindow.THIS.fastboot_flash_payload.Click += delegate
            {
                if (!checkCurDevExist())
                    return;

                if (BlocksFlashForVab())
                    return;

                Helper.fileSelect(new Helper.PathSelectCallback(delegate (string path)
                {
                    Payload payload = null;
                    Exception exception = null;

                    Action beforeLoad = new Action(delegate
                    {
                        try
                        {
                            payload = new Payload(path, PAYLOAD_TMP);
                        }
                        catch (Exception e)
                        {
                            exception = e;
                        }
                    });

                    Action afterLoad = new Action(delegate
                    {
                        action_unlock();
                        MainWindow.THIS.fastboot_progress_bar.IsIndeterminate = false;

                        if (exception != null)
                        {
                            MessageBox.Show(exception.Message);
                            return;
                        }
                        Payload.PayloadInitException exc = payload.init();
                        if (exc != null)
                        {
                            payload.Dispose();
                            payload = null;
                            MessageBox.Show(Properties.Resources.payload_unsupported_format + "\n" + exc.Message);
                            return;
                        }

                        // Comprobar que todas las particiones del payload existen en el dispositivo
                        string unknown_partition_list = "";
                        foreach (PartitionUpdate partitionUpdate in payload.manifest.Partitions)
                        {
                            long size;
                            if (MainWindow.THIS.ignore_unknown_part.IsChecked == false
                            && !fastbootData.partition_size.TryGetValue(partitionUpdate.PartitionName, out size)
                            && !fastbootData.partition_size.TryGetValue(partitionUpdate.PartitionName + "_" + fastbootData.current_slot, out size))
                            {
                                unknown_partition_list += partitionUpdate.PartitionName + " ";
                            }
                        }

                        if (unknown_partition_list != "")
                        {
                            string message_append = fastbootData.fastbootd ?
                            "\n" + Properties.Resources.fastboot_unknown_partition_str1 : "\n" + Properties.Resources.fastboot_unknown_partition_str2;
                            MessageBox.Show(Properties.Resources.fastboot_unknown_partition_str0 + "\n" + unknown_partition_list + message_append);
                            payload.Dispose();
                            return;
                        }

                        action_lock();
                        new Thread(new ThreadStart(delegate
                        {
                            bool ok = PayloadFlashExecutor.Run(
                                cur_serial, payload, PAYLOAD_TMP, appendLog,
                                onError: msg => MainWindow.THIS.Dispatcher.Invoke(
                                    () => MessageBox.Show(msg)));
                            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                action_unlock();
                                if (ok)
                                {
                                    load_fastboot_vars();
                                    MessageBox.Show(Properties.Resources.operation_completed);
                                }
                            }));
                            payload.Dispose();
                        })).Start();
                    });
                    action_lock();
                    MainWindow.THIS.fastboot_progress_bar.IsIndeterminate = true;
                    Helper.offloadAndRun(beforeLoad, afterLoad);
                }), "Payload|*.bin;*.zip");
            };

            listHelper = new Helper.ListHelper<fastboot_partition_row>(MainWindow.THIS.fastboot_partition_list,
                new Helper.ListHelper<fastboot_partition_row>.Filter(delegate (fastboot_partition_row row)
                {
                    if (MainWindow.THIS.fastboot_partition_name_textbox.Text == "")
                        return true;

                    if (row.name.Contains(MainWindow.THIS.fastboot_partition_name_textbox.Text))
                        return true;
                    return false;
                }));

            MainWindow.THIS.fastboot_partition_name_textbox.TextChanged += delegate
            {
                listHelper.doFilter();
            };

        }

        public static void RunPayloadFlash(string path, Action<bool> onComplete, bool bypassAntiRb = false, string romRoot = null)
        {
            if (!EnsureFastbootDevice(out string serial))
            {
                onComplete?.Invoke(false);
                return;
            }

            Action beforeLoad = new Action(delegate
            {
                ensureFastbootDataLoaded(serial);
            });

            Action afterLoad = new Action(delegate
            {
                if (fastbootData == null)
                {
                    onComplete?.Invoke(false);
                    return;
                }

                if (BlocksFlashForVab())
                {
                    onComplete?.Invoke(false);
                    return;
                }

                Payload payload = null;
                Exception exception = null;
                try
                {
                    payload = new Payload(path, PAYLOAD_TMP);
                }
                catch (Exception e)
                {
                    exception = e;
                }

                if (exception != null)
                {
                    MessageBox.Show(exception.Message);
                    onComplete?.Invoke(false);
                    return;
                }

                Payload.PayloadInitException exc = payload.init();
                if (exc != null)
                {
                    payload.Dispose();
                    MessageBox.Show(Properties.Resources.payload_unsupported_format + "\n" + exc.Message);
                    onComplete?.Invoke(false);
                    return;
                }

                TerminalLog.Action("Payload flash: " + payload.manifest.Partitions.Count + " partitions");

                new Thread(new ThreadStart(delegate
                {
                    bool ok = PayloadFlashExecutor.Run(
                        serial, payload, PAYLOAD_TMP, appendLog, bypassAntiRb, romRoot,
                        onError: msg => MainWindow.THIS.Dispatcher.Invoke(
                            () => MessageBox.Show(msg)));
                    payload.Dispose();
                    MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (ok)
                            MessageBox.Show(Properties.Resources.operation_completed);
                        onComplete?.Invoke(ok);
                    }));
                })).Start();
            });

            Helper.offloadAndRun(beforeLoad, afterLoad);
        }

        static void ensureFastbootDataLoaded(string serial)
        {
            cur_serial = serial;
            fastbootData = FastbootDeviceDataCache.GetOrLoad(serial);
        }
    }
}
