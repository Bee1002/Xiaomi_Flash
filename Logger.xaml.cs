using System;
using System.Windows;

namespace Xiaomi_Flash
{
    /// <summary>
    /// Lógica de interacción de Logger.xaml (ventana de log legacy, oculta en la UI v2).
    /// </summary>
    public partial class Logger : Window
    {
        public Logger()
        {
            InitializeComponent();
        }

        public Logger(Action onClose)
        {
            InitializeComponent();
            Closed += (a, b) => onClose();
        }

        public void appendLog(string logs)
        {
            Dispatcher.Invoke(new Action(delegate
            {
                log.Text += logs + "\n";
                scroller.ScrollToEnd();
            }));
        }
    }
}
