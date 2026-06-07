using System;
using System.Windows.Controls;

namespace Xiaomi_Flash.Ui
{
    internal static class TerminalLog
    {
        public static bool SummaryMode { get; set; } = true;

        public static void Info(string message) => Append("INFO", message);
        public static void Action(string message) => Append("ACTION", message);
        public static void Error(string message) => Append("ERROR", message);

        public static void Line(string message)
        {
            if (MainWindow.THIS?.ui_terminal_log == null || string.IsNullOrEmpty(message))
                return;

            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
            {
                TextBox terminal = MainWindow.THIS.ui_terminal_log;
                if (terminal == null)
                    return;

                terminal.AppendText(message + "\r\n");
                terminal.ScrollToEnd();
            }));
        }

        public static void Append(string category, string message)
        {
            if (MainWindow.THIS?.ui_terminal_log == null)
                return;

            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
            {
                TextBox terminal = MainWindow.THIS.ui_terminal_log;
                if (terminal == null)
                    return;

                terminal.AppendText($"{category}: {message}\r\n");
                terminal.ScrollToEnd();
            }));
        }

        public static void FastbootOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (!SummaryMode || ShouldShowFastbootLine(line))
                Line(line);
        }

        public static void StepResult(string stepName, bool success)
        {
            Line($"{stepName}: {(success ? "OK" : "FAIL")}");
        }

        static bool ShouldShowFastbootLine(string line)
        {
            if (line.IndexOf("FAILED", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (line.IndexOf("not allowed", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        public static void FlashCompleted(TimeSpan elapsed)
        {
            Line("");
            TerminalCompletionBanner.ShowSuccess(
                "All Task Is Completed - - - Elapsed Time : " + FormatElapsed(elapsed));
        }

        public static void FlashFailed(TimeSpan elapsed)
        {
            Line("");
            TerminalCompletionBanner.ShowFailure(
                "Task not completed - - - Elapsed Time : " + FormatElapsed(elapsed));
        }

        public static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";

            if (elapsed.TotalMinutes >= 1)
                return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds:D2}s";

            if (elapsed.TotalSeconds >= 1)
                return $"{(int)elapsed.TotalSeconds}s";

            return $"{elapsed.Milliseconds}ms";
        }

        public static void Reset()
        {
            TerminalCompletionBanner.Hide();

            if (MainWindow.THIS?.ui_terminal_log == null)
                return;

            MainWindow.THIS.Dispatcher.BeginInvoke(new Action(delegate
            {
                MainWindow.THIS.ui_terminal_log?.Clear();
            }));
        }
    }
}
