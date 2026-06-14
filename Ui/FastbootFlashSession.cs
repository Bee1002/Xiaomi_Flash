#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Xiaomi_Flash;

namespace Xiaomi_Flash.Ui
{
    internal static class FastbootFlashSession
    {
        static volatile bool flashing;
        static volatile bool cancelRequested;
        static List<PartitionDisplayRow> rows = new List<PartitionDisplayRow>();
        static RomFlashPlan loadedPlan;
        static RomPackageInfo loadedPackage;
        static string loadedRomRoot = "";
        static string activeFlashSerial = "";

        public static string LoadedRomRoot => loadedRomRoot;

        public static bool IsFlashing => flashing;

        static bool sessionBypassAntiRb;
        static bool sessionContinueOnAntiRbFail;
        static bool sessionAutoReboot;
        static bool rebootStepExecuted;
        static bool suppressMethodChange;
        static DateTime flashStartedAtUtc;
        static DateTime currentStepStartedUtc;
        static DispatcherTimer stepProgressTimer;
        static int stepProgressPercent;
        static string stepProgressOperation = "";
        static int stepProgressFrame;
        static volatile bool stepProgressBusy;

        public static void Init()
        {
            if (MainWindow.THIS.ui_load_firmware != null)
                MainWindow.THIS.ui_load_firmware.Click += delegate { OnLoadFirmwareClicked(); };

            if (MainWindow.THIS.ui_start_flashing != null)
                MainWindow.THIS.ui_start_flashing.Click += delegate { OnStartClicked(); };

            if (MainWindow.THIS.ui_stop_flashing != null)
                MainWindow.THIS.ui_stop_flashing.Click += delegate { RequestStop(); };

            if (MainWindow.THIS.ui_flash_method != null)
                MainWindow.THIS.ui_flash_method.SelectionChanged += OnFlashMethodComboChanged;

            if (MainWindow.THIS.ui_opt_bypass_anti_rb != null)
            {
                MainWindow.THIS.ui_opt_bypass_anti_rb.Checked += delegate { SyncAntiRbContinueOption(); };
                MainWindow.THIS.ui_opt_bypass_anti_rb.Unchecked += delegate { SyncAntiRbContinueOption(); };
            }

            SyncAntiRbContinueOption();
            UpdateButtonState();
        }

        static void SyncAntiRbContinueOption()
        {
            if (MainWindow.THIS?.ui_opt_anti_rb_continue == null)
                return;

            bool bypass = MainWindow.THIS.ui_opt_bypass_anti_rb?.IsChecked == true;
            MainWindow.THIS.ui_opt_anti_rb_continue.IsEnabled = bypass && !flashing;
            if (!bypass)
                MainWindow.THIS.ui_opt_anti_rb_continue.IsChecked = false;
        }

        static bool HasLoadedFirmware()
        {
            return loadedPlan != null && loadedPlan.Steps.Count > 0;
        }

        public static void UpdateButtonState()
        {
            if (MainWindow.THIS == null)
                return;

            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
            {
                bool deviceReady = FastbootDeviceService.HasFastbootDevice();
                bool firmwareReady = HasLoadedFirmware();

                if (MainWindow.THIS.ui_load_firmware != null)
                    MainWindow.THIS.ui_load_firmware.IsEnabled = !flashing;

                if (MainWindow.THIS.ui_start_flashing != null)
                    MainWindow.THIS.ui_start_flashing.IsEnabled = deviceReady && firmwareReady && !flashing;

                if (MainWindow.THIS.ui_stop_flashing != null)
                    MainWindow.THIS.ui_stop_flashing.IsEnabled = flashing;

                if (MainWindow.THIS.ui_reboot != null)
                    MainWindow.THIS.ui_reboot.IsEnabled = deviceReady && !flashing;

                if (MainWindow.THIS.ui_opt_bypass_anti_rb != null)
                    MainWindow.THIS.ui_opt_bypass_anti_rb.IsEnabled = !flashing;

                if (MainWindow.THIS.ui_opt_anti_rb_continue != null)
                {
                    bool bypass = MainWindow.THIS.ui_opt_bypass_anti_rb?.IsChecked == true;
                    MainWindow.THIS.ui_opt_anti_rb_continue.IsEnabled = bypass && !flashing;
                    if (!bypass)
                        MainWindow.THIS.ui_opt_anti_rb_continue.IsChecked = false;
                }

                if (MainWindow.THIS.ui_opt_autoreboot != null)
                    MainWindow.THIS.ui_opt_autoreboot.IsEnabled = !flashing;

                if (MainWindow.THIS.ui_flash_method != null)
                    MainWindow.THIS.ui_flash_method.IsEnabled = !flashing && loadedPackage != null;

                if (MainWindow.THIS.ui_advanced != null)
                    MainWindow.THIS.ui_advanced.IsEnabled = deviceReady && !flashing;

                if (flashing && MainWindow.THIS.ui_advanced_menu != null)
                    MainWindow.THIS.ui_advanced_menu.Visibility = Visibility.Collapsed;
            }));
        }

        static bool ReadBypassAntiRbOption()
        {
            if (MainWindow.THIS?.ui_opt_bypass_anti_rb == null)
                return false;
            return MainWindow.THIS.ui_opt_bypass_anti_rb.IsChecked == true;
        }

        static bool ReadContinueOnAntiRbFailOption()
        {
            if (!ReadBypassAntiRbOption())
                return false;
            if (MainWindow.THIS?.ui_opt_anti_rb_continue == null)
                return false;
            return MainWindow.THIS.ui_opt_anti_rb_continue.IsChecked == true;
        }

        static bool ReadAutoRebootOption()
        {
            if (loadedPlan != null && PlanIncludesReboot(loadedPlan))
                return true;

            if (MainWindow.THIS?.ui_opt_autoreboot == null)
                return true;
            return MainWindow.THIS.ui_opt_autoreboot.IsChecked == true;
        }

        static bool PlanIncludesReboot(RomFlashPlan plan)
        {
            foreach (FlashScriptStep step in plan.Steps)
            {
                if (step.Kind == FlashScriptStepKind.Reboot)
                    return true;
            }
            return false;
        }

        static void UpdateAutoRebootOptionUi(RomFlashPlan plan)
        {
            if (MainWindow.THIS == null)
                return;

            bool scriptHasReboot = PlanIncludesReboot(plan);

            if (MainWindow.THIS.ui_opt_autoreboot != null)
            {
                if (scriptHasReboot)
                {
                    MainWindow.THIS.ui_opt_autoreboot.Visibility = Visibility.Collapsed;
                    MainWindow.THIS.ui_opt_autoreboot.IsChecked = true;
                }
                else
                {
                    MainWindow.THIS.ui_opt_autoreboot.Visibility = Visibility.Visible;
                }
            }

            if (MainWindow.THIS.ui_autoreboot_note != null)
                MainWindow.THIS.ui_autoreboot_note.Visibility = scriptHasReboot
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        static void OnLoadFirmwareClicked()
        {
            if (flashing)
                return;

            Helper.pathSelect(delegate (string selectedPath)
            {
                RomPackageInfo package = RomPackageResolver.Resolve(selectedPath);
                if (package.Methods.Count == 0)
                {
                    MessageBox.Show(
                        "No valid ROM package found.\n\n" +
                        "Select the ROM root folder (containing flash_all.bat, flash_all_lock.bat, or payload.bin).\n" +
                        "If you only have loose images, place them in an images\\ subfolder.",
                        "LOAD FIRMWARE",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                loadedPackage = package;
                loadedRomRoot = package.RomRoot;

                if (!selectedPath.Equals(package.RomRoot, StringComparison.OrdinalIgnoreCase))
                    TerminalLog.Info("ROM detected at: " + package.RomRoot);

                PopulateFlashMethods(package);
                ApplySelectedFlashMethod();
            });
        }

        static void OnFlashMethodComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressMethodChange || flashing || loadedPackage == null)
                return;

            if (!(MainWindow.THIS?.ui_flash_method?.SelectedItem is RomFlashMethodOption))
                return;

            ApplySelectedFlashMethod();
        }

        static void PopulateFlashMethods(RomPackageInfo package)
        {
            MainWindow.THIS.Dispatcher.Invoke(delegate
            {
                ComboBox combo = MainWindow.THIS.ui_flash_method;
                if (combo == null)
                    return;

                suppressMethodChange = true;
                combo.ItemsSource = package.Methods;

                RomFlashMethodOption preferred = null;
                foreach (RomFlashMethodOption option in package.Methods)
                {
                    if (option.Method == RomFlashMethod.ScriptFlashAll)
                    {
                        preferred = option;
                        break;
                    }
                }
                combo.SelectedItem = preferred ?? package.Methods[0];
                suppressMethodChange = false;
            });
        }

        static void ApplySelectedFlashMethod()
        {
            if (loadedPackage == null)
                return;

            RomFlashMethodOption option = MainWindow.THIS.ui_flash_method?.SelectedItem as RomFlashMethodOption;
            if (option == null)
                return;

            RomFlashPlan plan = RomFlashScanner.Scan(loadedPackage, option);
            if (plan.Kind == RomFlashKind.None || plan.Steps.Count == 0)
            {
                MessageBox.Show(
                    "Could not read flash steps for: " + option.DisplayName,
                    "LOAD FIRMWARE",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            loadedPlan = plan;
            BuildPartitionTable(plan);
            ResetRowsForFlash();
            UpdateAutoRebootOptionUi(plan);
            LogSkippedScriptSteps(plan);
            LogFlashMethodHint(option);
            TerminalLog.Line("Firmware loaded [" + option.DisplayName + "]: " + plan.Steps.Count + " step(s)");
            SetMainProgress(0, option.DisplayName + " — " + option.Description);
            UpdateButtonState();
        }

        static void LogFlashMethodHint(RomFlashMethodOption option)
        {
            if (loadedPackage == null)
                return;

            bool hasFlashAll = File.Exists(Path.Combine(loadedPackage.RomRoot, "flash_all.bat"));
            bool hasPayload = File.Exists(Path.Combine(loadedPackage.RomRoot, "payload.bin"));

            if (option.Method == RomFlashMethod.Payload && hasFlashAll)
            {
                TerminalLog.Info(
                    "Tip: payload.bin is OTA-style. For a full clean install on any Xiaomi device, prefer flash_all.bat.");
            }
            else if (hasFlashAll && hasPayload && option.Method != RomFlashMethod.Payload)
            {
                TerminalLog.Info("This package also contains payload.bin (OTA update mode).");
            }
        }

        static void LogSkippedScriptSteps(RomFlashPlan plan)
        {
            if (plan.SkippedSteps.Count == 0)
                return;

            TerminalLog.Error("Script: " + plan.SkippedSteps.Count + " step(s) skipped (image not found):");
            foreach (string entry in plan.SkippedSteps)
                TerminalLog.Line("  - " + entry);
        }

        static void OnStartClicked()
        {
            if (flashing)
                return;

            if (!HasLoadedFirmware())
            {
                MessageBox.Show(
                    "Load firmware first with [ LOAD FIRMWARE ].",
                    "START",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!FastbootDeviceService.EnsureFastbootDevice(out string serial))
                return;

            sessionBypassAntiRb = ReadBypassAntiRbOption();
            sessionContinueOnAntiRbFail = ReadContinueOnAntiRbFailOption();
            sessionAutoReboot = ReadAutoRebootOption();
            activeFlashSerial = serial;
            rebootStepExecuted = false;

            string rebootLine = PlanIncludesReboot(loadedPlan)
                ? "Auto reboot: included in script"
                : "Auto reboot: " + (sessionAutoReboot ? "Yes" : "No");

            string deviceSummary = FlashPrecheck.BuildFlashDeviceSummary(
                serial, loadedRomRoot, loadedPlan, sessionBypassAntiRb);

            string optionsLine = "Mode: " + loadedPlan.ScriptFileName + "\n"
                + loadedPlan.MethodDescription + "\n"
                + "Bypass anti_RB: " + (sessionBypassAntiRb ? "Yes" : "No") + "\n"
                + (sessionBypassAntiRb
                    ? "Continue if anti-RB fails: " + (sessionContinueOnAntiRbFail ? "Yes" : "No") + "\n"
                    : "")
                + rebootLine;

            if (!FlashConfirmDialog.Show(loadedPlan.Steps.Count, loadedRomRoot, deviceSummary, optionsLine))
                return;

            FlashPrecheck.LogFlashConfirmWarnings(serial, loadedRomRoot, loadedPlan);

            ResetRowsForFlash();
            TerminalCompletionBanner.Hide();

            if (loadedPlan.Kind == RomFlashKind.Payload)
            {
                flashing = true;
                cancelRequested = false;
                flashStartedAtUtc = DateTime.UtcNow;
                UpdateButtonState();
                TerminalLog.Action("Payload flash started");
                SetMainProgress(0, "Preparing payload...");
                FastbootDeviceService.RunPayloadFlash(
                    loadedPlan.PayloadPath!,
                    OnFlashFinished,
                    sessionBypassAntiRb,
                    loadedRomRoot,
                    sessionContinueOnAntiRbFail,
                    ApplyPayloadPartitionPlan,
                    OnPayloadPartitionProgress);
                return;
            }

            BeginScriptFlash(serial, loadedPlan.Steps, sessionBypassAntiRb, sessionContinueOnAntiRbFail, sessionAutoReboot);
        }

        static void ApplyPayloadPartitionPlan(List<string> partitionNames)
        {
            if (loadedPlan == null || partitionNames == null || partitionNames.Count == 0)
                return;

            loadedPlan.Steps.Clear();
            foreach (string name in partitionNames)
            {
                loadedPlan.Steps.Add(new FlashScriptStep
                {
                    Kind = FlashScriptStepKind.Flash,
                    DisplayName = name,
                    Partition = name
                });
            }

            MainWindow.THIS?.Dispatcher.Invoke(delegate
            {
                BuildPartitionTable(loadedPlan);
                ResetRowsForFlash();
            });
        }

        static void OnPayloadPartitionProgress(string partitionName, string phase)
        {
            int rowIndex = FindRowIndexByPartition(partitionName);
            if (rowIndex < 0)
                return;

            switch (phase)
            {
                case "extract":
                    UpdateRow(rowIndex, "[ EXTRACT ]", "[====-------] --");
                    break;
                case "flash":
                    UpdateRow(rowIndex, "[ RUNNING ]", "[=======----] --");
                    break;
                case "ok":
                    UpdateRow(rowIndex, "[ OK ]", "[##########] 100%", true);
                    break;
                case "failed":
                    UpdateRow(rowIndex, "[ FAILED ]", "[##########] ERR", true);
                    break;
            }
        }

        static void ResetRowsForFlash()
        {
            foreach (PartitionDisplayRow row in rows)
            {
                row.Status = "[ PENDING ]";
                row.ProgressStr = "[-----------] --";
            }

            if (rows.Count == 0)
                return;

            MainWindow.THIS?.Dispatcher.BeginInvoke(new Action(delegate
            {
                MainWindow.THIS.ui_partition_table.ItemsSource = null;
                MainWindow.THIS.ui_partition_table.ItemsSource = rows;
                ScrollPartitionTableToTop();
            }));
        }

        static void BuildPartitionTable(RomFlashPlan plan)
        {
            rows.Clear();
            int index = 1;
            foreach (FlashScriptStep step in plan.Steps)
            {
                string size = "—";
                if (step.Kind == FlashScriptStepKind.Flash && !string.IsNullOrEmpty(step.ImagePath))
                {
                    try
                    {
                        long bytes = new FileInfo(step.ImagePath).Length;
                        size = Helper.byte2AUnit((ulong)bytes);
                    }
                    catch (IOException) { }
                }

                rows.Add(new PartitionDisplayRow
                {
                    Index = index.ToString("00"),
                    Name = FormatStepName(step),
                    ImageFile = step.ImagePath ?? "",
                    Size = size,
                    Status = "[ PENDING ]",
                    ProgressStr = "[-----------] --"
                });
                index++;
            }

            MainWindow.THIS.Dispatcher.Invoke(delegate
            {
                MainWindow.THIS.ui_partition_table.ItemsSource = null;
                MainWindow.THIS.ui_partition_table.ItemsSource = rows;
                ScrollPartitionTableToTop();
            });
        }

        static string FormatStepName(FlashScriptStep step)
        {
            switch (step.Kind)
            {
                case FlashScriptStepKind.Erase:
                    return "[erase] " + step.Partition;
                case FlashScriptStepKind.Reboot:
                    return "[reboot] " + (string.IsNullOrEmpty(step.RebootTarget) ? "system" : step.RebootTarget);
                case FlashScriptStepKind.SetActive:
                    return "[slot] " + step.ActiveSlot;
                case FlashScriptStepKind.OemLock:
                    return "[lock] bootloader";
                default:
                    return step.DisplayName;
            }
        }

        static void BeginScriptFlash(string serial, List<FlashScriptStep> steps, bool bypassAntiRb, bool continueOnAntiRbFail, bool autoReboot)
        {
            cancelRequested = false;
            flashing = true;
            flashStartedAtUtc = DateTime.UtcNow;
            UpdateButtonState();
            TerminalLog.Line("--- Flash started (" + steps.Count + " steps) ---");
            SetMainProgress(0, "Starting flash...");

            List<FlashScriptStep> queue = new List<FlashScriptStep>(steps);
            if (bypassAntiRb)
                queue = AntiRollbackBypass.FilterScriptSteps(queue);

            new Thread(new ThreadStart(delegate
            {
                bool allOk = true;
                FastbootGate.EnterCritical();
                try
                {
                    if (bypassAntiRb)
                    {
                        BeginStepProgress(0, "anti");
                        AntiRollbackBypass.ApplyResult antiResult = AntiRollbackBypass.Apply(
                            serial,
                            loadedRomRoot,
                            true,
                            continueOnAntiRbFail);
                        EndStepProgress();
                        TerminalLog.StepResult("anti", antiResult.FlashSucceeded);
                        int antiRow = FindRowIndexByPartition("anti");
                        if (antiRow >= 0)
                            UpdateRow(antiRow, antiResult.FlashSucceeded ? "[ OK ]" : "[ FAILED ]",
                                antiResult.FlashSucceeded ? "[##########] 100%" : "[##########] ERR", true);
                        if (!antiResult.ShouldProceed)
                            allOk = false;
                    }

                    if (!allOk)
                        return;

                    for (int i = 0; i < queue.Count; i++)
                    {
                        FlashScriptStep step = queue[i];
                        int rowIndex = FindRowIndex(step);

                        if (cancelRequested)
                        {
                            allOk = false;
                            if (rowIndex >= 0)
                                UpdateRow(rowIndex, "[ CANCELLED ]", "[-----------] --", true);
                            break;
                        }

                        if (step.Kind == FlashScriptStepKind.Reboot && !autoReboot)
                        {
                            if (rowIndex >= 0)
                                UpdateRow(rowIndex, "[ SKIPPED ]", "[-----------] --", true);
                            continue;
                        }

                        if (rowIndex >= 0)
                            UpdateRow(rowIndex, "[ RUNNING ]", "[====-------] --");

                        int stepPercent = queue.Count > 0 ? i * 100 / queue.Count : 0;
                        BeginStepProgress(stepPercent, FormatStepName(step));
                        bool ok = ExecuteStep(serial, step, autoReboot);
                        EndStepProgress();
                        TerminalLog.StepResult(FormatStepName(step), ok);
                        if (rowIndex >= 0)
                            UpdateRow(rowIndex, ok ? "[ OK ]" : "[ FAILED ]",
                                ok ? "[##########] 100%" : "[##########] ERR", true);
                        if (!ok)
                            allOk = false;
                    }
                }
                finally
                {
                    FastbootGate.ExitCritical();
                    OnFlashFinished(allOk);
                }
            })).Start();
        }

        static int FindRowIndex(FlashScriptStep step)
        {
            for (int i = 0; i < loadedPlan.Steps.Count; i++)
            {
                FlashScriptStep original = loadedPlan.Steps[i];
                if (original.Kind == step.Kind
                    && string.Equals(original.DisplayName, step.DisplayName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(original.Partition, step.Partition, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(original.ImagePath, step.ImagePath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(original.RebootTarget, step.RebootTarget, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(original.ActiveSlot, step.ActiveSlot, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        static int FindRowIndexByPartition(string partition)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Name.Equals(partition, StringComparison.OrdinalIgnoreCase)
                    || rows[i].Name.Equals("[erase] " + partition, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        static bool ExecuteStep(string serial, FlashScriptStep step, bool autoReboot)
        {
            switch (step.Kind)
            {
                case FlashScriptStepKind.Flash:
                    return ExecuteFlashStep(serial, step);
                case FlashScriptStepKind.Erase:
                    return ExecuteFastbootCommand(serial, "erase \"" + step.Partition + "\"");
                case FlashScriptStepKind.Reboot:
                    if (!autoReboot)
                        return true;
                    rebootStepExecuted = true;
                    string rebootCmd = string.IsNullOrEmpty(step.RebootTarget)
                        ? "reboot"
                        : "reboot " + step.RebootTarget;
                    return ExecuteFastbootCommand(serial, rebootCmd);
                case FlashScriptStepKind.SetActive:
                    return ExecuteFastbootCommand(serial, "set_active " + step.ActiveSlot);
                case FlashScriptStepKind.OemLock:
                    return ExecuteFastbootCommand(serial, "oem lock");
                default:
                    return true;
            }
        }

        static bool ExecuteFlashStep(string serial, FlashScriptStep step)
        {
            string cmd = "flash " + step.ExtraArgs;
            if (cmd.Length > 6 && !cmd.EndsWith(" "))
                cmd += " ";
            cmd += "\"" + step.Partition + "\" \"" + step.ImagePath + "\"";
            return ExecuteFastbootCommand(serial, cmd);
        }

        static bool ExecuteFastbootCommand(string serial, string cmd)
        {
            try
            {
                lock (FastbootGate.Sync)
                {
                    return Fastboot.Run(serial, cmd, FastbootDeviceService.AppendTerminalLog, Fastboot.GetRebootTimeoutMs(cmd));
                }
            }
            catch (Exception ex)
            {
                FastbootDeviceService.AppendTerminalLog("ERROR: " + ex.Message);
                return false;
            }
        }

        static void OnFlashFinished(bool success)
        {
            flashing = false;
            cancelRequested = false;

            TimeSpan elapsed = flashStartedAtUtc != default
                ? DateTime.UtcNow - flashStartedAtUtc
                : TimeSpan.Zero;
            flashStartedAtUtc = default;

            MainWindow.THIS?.Dispatcher.BeginInvoke(new Action(delegate
            {
                SetMainProgress(success ? 100 : 0, success ? "Flash completed" : "Flash stopped or completed with errors");
                if (success)
                {
                    TerminalLog.FlashCompleted(elapsed);
                    if (sessionAutoReboot && !rebootStepExecuted && !string.IsNullOrEmpty(activeFlashSerial))
                        RunAutoReboot(activeFlashSerial);
                }
                else
                    TerminalLog.FlashFailed(elapsed);

                UpdateButtonState();
                FastbootDeviceService.RefreshConnectionPanel();
            }));
        }

        static void RunAutoReboot(string serial)
        {
            TerminalLog.Action("Auto reboot...");
            FastbootDeviceService.RunStepCommand(serial, "reboot", 0, false, true);
            rebootStepExecuted = true;
        }

        static void RequestStop()
        {
            if (!flashing)
                return;
            cancelRequested = true;
            TerminalLog.Action("Stopping flash...");
        }

        static void UpdateRow(int index, string status, string progress, bool scrollAfterComplete = false)
        {
            if (index < 0 || index >= rows.Count)
                return;

            rows[index].Status = status;
            rows[index].ProgressStr = progress;

            if (scrollAfterComplete)
                ScrollPartitionTableAfterRowCompleted(index);
        }

        const double PartitionRowScrollStep = 24;

        static void ScrollPartitionTableAfterRowCompleted(int index)
        {
            if (index < 0 || index >= rows.Count)
                return;

            MainWindow.THIS?.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (MainWindow.THIS?.ui_partition_scroll == null)
                    return;

                ScrollViewer scroll = MainWindow.THIS.ui_partition_scroll;
                scroll.UpdateLayout();

                double max = Math.Max(0, scroll.ExtentHeight - scroll.ViewportHeight);
                double rowTop = index * PartitionRowScrollStep;
                double rowBottom = rowTop + PartitionRowScrollStep;
                double viewTop = scroll.VerticalOffset;
                double viewBottom = viewTop + scroll.ViewportHeight;

                if (rowBottom > viewBottom)
                {
                    double target = rowBottom - scroll.ViewportHeight * 0.4;
                    scroll.ScrollToVerticalOffset(Math.Clamp(target, 0, max));
                }
                else if (rowTop < viewTop)
                {
                    scroll.ScrollToVerticalOffset(rowTop);
                }
                else if (index + 1 < rows.Count)
                {
                    double nextRowBottom = (index + 2) * PartitionRowScrollStep;
                    if (nextRowBottom > viewBottom)
                        scroll.ScrollToVerticalOffset(Math.Min(scroll.VerticalOffset + PartitionRowScrollStep, max));
                }
            }), DispatcherPriority.Loaded);
        }

        static void ScrollPartitionTableToTop()
        {
            MainWindow.THIS?.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (MainWindow.THIS?.ui_partition_scroll == null)
                    return;
                MainWindow.THIS.ui_partition_scroll.ScrollToVerticalOffset(0);
            }));
        }

        static void BeginStepProgress(int percent, string operation)
        {
            stepProgressPercent = percent;
            stepProgressOperation = operation ?? "";
            stepProgressBusy = true;
            currentStepStartedUtc = DateTime.UtcNow;
            stepProgressFrame = 0;

            MainWindow.THIS?.Dispatcher.BeginInvoke(new Action(delegate
            {
                EnsureStepProgressTimer();
                ApplyMainProgressDisplay(true);
                stepProgressTimer.Start();
            }));
        }

        static void EndStepProgress()
        {
            stepProgressBusy = false;

            MainWindow.THIS?.Dispatcher.BeginInvoke(new Action(delegate
            {
                stepProgressTimer?.Stop();
                ApplyMainProgressDisplay(false);
            }));
        }

        static void SetMainProgress(int percent, string operation)
        {
            stepProgressPercent = percent;
            stepProgressOperation = operation ?? "";
            stepProgressBusy = false;

            MainWindow.THIS?.Dispatcher.BeginInvoke(new Action(delegate
            {
                stepProgressTimer?.Stop();
                ApplyMainProgressDisplay(false);
            }));
        }

        static void EnsureStepProgressTimer()
        {
            if (stepProgressTimer != null)
                return;

            stepProgressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(220)
            };
            stepProgressTimer.Tick += delegate
            {
                if (!stepProgressBusy)
                    return;

                stepProgressFrame++;
                ApplyMainProgressDisplay(true);
            };
        }

        static void ApplyMainProgressDisplay(bool busy)
        {
            if (MainWindow.THIS == null)
                return;

            int percent = stepProgressPercent;
            string bar = busy ? BuildBusyBar(percent, stepProgressFrame) : BuildBar(percent);
            string suffix = busy ? " " + BusySpinner(stepProgressFrame) : "";

            if (MainWindow.THIS.ui_main_progress_bar != null)
            {
                MainWindow.THIS.ui_main_progress_bar.Visibility = Visibility.Visible;
                MainWindow.THIS.ui_main_progress_bar.IsIndeterminate = false;
                MainWindow.THIS.ui_main_progress_bar.Value = percent;
            }

            if (MainWindow.THIS.ui_main_progress_text != null)
                MainWindow.THIS.ui_main_progress_text.Text =
                    $"OVERALL FLASH PROGRESS  [{bar}] {percent}%{suffix}";

            if (MainWindow.THIS.ui_current_operation != null)
            {
                string operation = stepProgressOperation;
                if (busy && currentStepStartedUtc != default)
                {
                    TimeSpan elapsed = DateTime.UtcNow - currentStepStartedUtc;
                    operation += " (" + TerminalLog.FormatElapsed(elapsed) + ")";
                }

                MainWindow.THIS.ui_current_operation.Text = operation;
            }
        }

        static string BuildBar(int percent)
        {
            int filled = Math.Clamp(percent / 5, 0, 20);
            return new string('=', filled) + new string('-', 20 - filled);
        }

        static string BuildBusyBar(int percent, int frame)
        {
            int filled = Math.Clamp(percent / 5, 0, 20);
            char[] bar = new char[20];
            for (int i = 0; i < 20; i++)
                bar[i] = i < filled ? '=' : '-';

            int tail = 20 - filled;
            if (tail > 0)
            {
                int pos = filled + (frame % tail);
                bar[pos] = '>';
            }
            else if (filled > 0)
                bar[filled - 1] = frame % 2 == 0 ? '>' : '=';

            return new string(bar);
        }

        static string BusySpinner(int frame)
        {
            char[] spin = { '|', '/', '-', '\\' };
            return spin[frame % spin.Length].ToString();
        }
    }
}
