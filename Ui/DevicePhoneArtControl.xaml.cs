using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Xiaomi_Flash.Ui
{
    public partial class DevicePhoneArtControl : UserControl
    {
        const double CableConnectedY = 0;
        const double CableDisconnectedY = 22;
        const double CableConnectedWireHeight = 28;
        const double CableDisconnectedWireHeight = 14;

        bool cableConnected;

        public DevicePhoneArtControl()
        {
            InitializeComponent();
        }

        public void SetCableConnected(bool connected, bool animate)
        {
            if (cableConnected == connected && animate)
            {
                double currentY = usbCableTransform?.Y ?? CableDisconnectedY;
                double targetY = connected ? CableConnectedY : CableDisconnectedY;
                if (Math.Abs(currentY - targetY) < 0.5)
                    return;
            }

            cableConnected = connected;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ApplyCableState(connected, animate)));
                return;
            }

            ApplyCableState(connected, animate);
        }

        void ApplyCableState(bool connected, bool animate)
        {
            double targetY = connected ? CableConnectedY : CableDisconnectedY;
            double targetWireHeight = connected ? CableConnectedWireHeight : CableDisconnectedWireHeight;

            if (!animate)
            {
                usbCableTransform.Y = targetY;
                usbCableWire.Height = targetWireHeight;
                return;
            }

            DoubleAnimation moveAnim = new DoubleAnimation
            {
                From = usbCableTransform.Y,
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(420),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            usbCableTransform.BeginAnimation(TranslateTransform.YProperty, moveAnim);

            DoubleAnimation wireAnim = new DoubleAnimation
            {
                From = usbCableWire.Height,
                To = targetWireHeight,
                Duration = TimeSpan.FromMilliseconds(420),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            usbCableWire.BeginAnimation(FrameworkElement.HeightProperty, wireAnim);
        }
    }
}
