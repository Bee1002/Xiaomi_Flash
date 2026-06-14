using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Xiaomi_Flash.Ui;

namespace Xiaomi_Flash
{
    /// <summary>
    /// Code-behind de la ventana principal de la UI v2 del flasher fastboot.
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow THIS = null!;

        public MainWindow()
        {
            InitializeComponent();
            THIS = this;
            InitPreviewUi();

            string mutexName = "Xiaomi_Flash";
            bool createdNew;
            Mutex singleInstanceWatcher = new Mutex(false, mutexName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(Properties.Resources.program_already_running, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                Process.GetCurrentProcess().Kill();
            }

            try
            {
                new DirectoryInfo(PayloadUI.PAYLOAD_TMP).Delete(true);
            }
            catch (DirectoryNotFoundException) { }

            try
            {
                new DirectoryInfo(FastbootDeviceService.PayloadTmp).Delete(true);
            }
            catch (DirectoryNotFoundException) { }

            PayloadUI.init();
            FastbootDeviceService.Initialize();

            AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_PreviewMouseDownDismissMenus), true);

            Closed += delegate
            {
                if (PayloadUI.payload != null)
                    PayloadUI.payload.Dispose();

                try
                {
                    new DirectoryInfo(PayloadUI.PAYLOAD_TMP).Delete(true);
                }
                catch (DirectoryNotFoundException) { }
                catch (IOException) { }

                try
                {
                    new DirectoryInfo(FastbootDeviceService.PayloadTmp).Delete(true);
                }
                catch (DirectoryNotFoundException) { }
                catch (IOException) { }

                Process.GetCurrentProcess().Kill();
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                MaximizeButton_Click(sender, e);
            else
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void CloseButton_Click(object sender, RoutedEventArgs e) =>
            Close();

        private void AdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            FastbootRebootMenu.HideMenu();
            if (ui_advanced_menu != null)
                ui_advanced_menu.Visibility = ui_advanced_menu.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RebootButton_Click(object sender, RoutedEventArgs e)
        {
            if (!FastbootDeviceService.HasFastbootDevice())
            {
                MessageBox.Show(
                    Properties.Resources.fastboot_device_not_exist,
                    "[ REBOOT ]",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            FastbootAdvanced.HideMenu();
            if (ui_reboot_menu != null)
                ui_reboot_menu.Visibility = ui_reboot_menu.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
        }

        void Window_PreviewMouseDownDismissMenus(object sender, MouseButtonEventArgs e)
        {
            if (!IsDropdownMenuOpen())
                return;

            if (e.OriginalSource is DependencyObject source && IsInsideDropdownAnchor(source))
                return;

            HideDropdownMenus();
        }

        static bool IsDropdownMenuOpen()
        {
            return MainWindow.THIS.ui_advanced_menu?.Visibility == Visibility.Visible
                || MainWindow.THIS.ui_reboot_menu?.Visibility == Visibility.Visible;
        }

        static void HideDropdownMenus()
        {
            FastbootAdvanced.HideMenu();
            FastbootRebootMenu.HideMenu();
        }

        static bool IsInsideDropdownAnchor(DependencyObject source)
        {
            while (source != null)
            {
                if (source == MainWindow.THIS.ui_advanced_menu
                    || source == MainWindow.THIS.ui_reboot_menu
                    || source == MainWindow.THIS.ui_advanced
                    || source == MainWindow.THIS.ui_reboot)
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void InitPreviewUi()
        {
            ui_partition_table.ItemsSource = Array.Empty<PartitionDisplayRow>();
            if (ui_terminal_log != null)
            {
                ui_terminal_log.Clear();
                TerminalLog.Reset();
            }

            if (ui_main_progress_text != null)
                ui_main_progress_text.Text = "OVERALL FLASH PROGRESS  [----------------------------------] 0%";
            if (ui_main_progress_bar != null)
            {
                ui_main_progress_bar.Value = 0;
                ui_main_progress_bar.IsIndeterminate = false;
            }
            if (ui_current_operation != null)
                ui_current_operation.Text = "Current Operation: Idle";
        }
    }
}
