using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Xiaomi_Flash.Ui
{
    internal static class TerminalCompletionBanner
    {
        const int MsPerChar = 18;
        const string SuccessColor = "#00FF66";
        const string FailColor = "#FF5555";

        static DispatcherTimer? typeTimer;
        static string? currentText;
        static int charIndex;
        static TextBlock? targetBlock;

        public static void ShowSuccess(string message)
        {
            Show(message, SuccessColor);
        }

        public static void ShowFailure(string message)
        {
            Show(message, FailColor);
        }

        public static void Hide()
        {
            StopTimer();
            currentText = null;
            charIndex = 0;
            targetBlock = null;

            if (MainWindow.THIS?.ui_terminal_banner == null)
                return;

            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
            {
                MainWindow.THIS.ui_terminal_banner.Text = "";
                MainWindow.THIS.ui_terminal_banner.Visibility = Visibility.Collapsed;
            }));
        }

        static void Show(string message, string colorHex)
        {
            if (MainWindow.THIS?.ui_terminal_banner == null)
                return;

            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
            {
                TextBlock block = MainWindow.THIS.ui_terminal_banner;
                block.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                block.Visibility = Visibility.Visible;
                block.Text = "";

                StopTimer();
                currentText = message;
                charIndex = 0;
                targetBlock = block;

                typeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(MsPerChar) };
                typeTimer.Tick += OnTick;
                typeTimer.Start();
            }));
        }

        static void OnTick(object? sender, EventArgs e)
        {
            if (targetBlock == null || currentText == null)
            {
                StopTimer();
                return;
            }

            if (charIndex >= currentText.Length)
            {
                StopTimer();
                return;
            }

            targetBlock.Text += currentText[charIndex];
            charIndex++;
        }

        static void StopTimer()
        {
            if (typeTimer == null)
                return;
            typeTimer.Stop();
            typeTimer.Tick -= OnTick;
            typeTimer = null;
        }
    }
}
