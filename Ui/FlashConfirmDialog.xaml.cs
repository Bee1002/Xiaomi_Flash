using System.Windows;
using System.Windows.Input;

namespace Xiaomi_Flash.Ui
{
    public partial class FlashConfirmDialog : Window
    {
        FlashConfirmDialog(int stepCount, string romPath, string optionsLine)
        {
            InitializeComponent();
            Owner = MainWindow.THIS;
            ui_headline.Text = "FLASH " + stepCount + " STEP(S)?";
            ui_rom_path.Text = romPath ?? "";
            ui_options.Text = optionsLine ?? "";
        }

        public static bool Show(int stepCount, string romPath, string optionsLine)
        {
            var dialog = new FlashConfirmDialog(stepCount, romPath, optionsLine);
            return dialog.ShowDialog() == true;
        }

        void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
    }
}
