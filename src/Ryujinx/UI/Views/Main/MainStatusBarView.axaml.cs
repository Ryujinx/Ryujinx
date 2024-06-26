using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.UI.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.Views.Main
{
    public partial class MainStatusBarView : UserControl
    {
        public MainWindow Window;

        public MainStatusBarView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (VisualRoot is MainWindow window)
            {
                Window = window;
            }

            DataContext = Window.ViewModel;
        }

        private void VsyncStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            ConfigurationState config = ConfigurationState.Instance(Window.ViewModel.SelectedApplication != null);
            Window.ViewModel.AppHost.ToggleVSync();
            config.Graphics.EnableVsync.Value = Window.ViewModel.AppHost.Device.EnableDeviceVsync;

            Logger.Info?.Print(LogClass.Application, $"VSync toggled to: {Window.ViewModel.AppHost.Device.EnableDeviceVsync}");
        }

        private void DockedStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            ConfigurationState config = ConfigurationState.Instance(Window.ViewModel.SelectedApplication != null);
            config.System.EnableDockedMode.Value = !config.System.EnableDockedMode.Value;
        }

        private void AspectRatioStatus_OnClick(object sender, RoutedEventArgs e)
        {
            ConfigurationState config = ConfigurationState.Instance(Window.ViewModel.SelectedApplication != null);
            AspectRatio aspectRatio = config.Graphics.AspectRatio.Value;
            config.Graphics.AspectRatio.Value = (int)aspectRatio + 1 > Enum.GetNames(typeof(AspectRatio)).Length - 1 ? AspectRatio.Fixed4x3 : aspectRatio + 1;
        }

        private void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            Window.LoadApplications();
        }

        private void VolumeStatus_OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            ConfigurationState config = ConfigurationState.Instance(Window.ViewModel.SelectedApplication != null);

            // Change the volume by 5% at a time
            float newValue = Window.ViewModel.Volume + (float)e.Delta.Y * 0.05f;

            Window.ViewModel.Volume = newValue switch
            {
                < 0 => 0,
                > 1 => 1,
                _ => newValue,
            };

            config.System.AudioVolume.Value = Window.ViewModel.Volume;

            e.Handled = true;
        }
    }
}
