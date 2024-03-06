using Gdk;
using Gtk;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.UI;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Ryujinx.Modules
{
    public class UpdateDialog : Gtk.Window
    {
#pragma warning disable CS0649, IDE0044 // Field is never assigned to, Add readonly modifier
        [Builder.Object] public Label MainText;
        [Builder.Object] public Label SecondaryText;
        [Builder.Object] public LevelBar ProgressBar;
        [Builder.Object] public Button YesButton;
        [Builder.Object] public Button NoButton;
#pragma warning restore CS0649, IDE0044

        private readonly MainWindow _mainWindow;
        private readonly string _buildUrl;
        private bool _restartQuery;

        public UpdateDialog(MainWindow mainWindow, Version newVersion, string buildUrl) : this(new Builder("Ryujinx.Gtk3.Modules.Updater.UpdateDialog.glade"), mainWindow, newVersion, buildUrl) { }

        private UpdateDialog(Builder builder, MainWindow mainWindow, Version newVersion, string buildUrl) : base(builder.GetRawOwnedObject("UpdateDialog"))
        {
            builder.Autoconnect(this);

            _mainWindow = mainWindow;
            _buildUrl = buildUrl;

            Icon = new Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.Gtk3.UI.Common.Resources.Logo_Ryujinx.png");
            MainText.Text = "Do you want to update Ryujinx to the latest version?";
            SecondaryText.Text = $"{Program.Version} -> {newVersion}";

            ProgressBar.Hide();

            YesButton.Clicked += YesButton_Clicked;
            NoButton.Clicked += NoButton_Clicked;
        }

        private void YesButton_Clicked(object sender, EventArgs args)
        {
            if (_restartQuery)
            {
                string ryuName = OperatingSystem.IsWindows() ? "Ryujinx.exe" : "Ryujinx";

                ProcessStartInfo processStart = new(ryuName)
                {
                    UseShellExecute = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                foreach (string argument in CommandLineState.Arguments)
                {
                    processStart.ArgumentList.Add(argument);
                }

                Process.Start(processStart);

                Environment.Exit(0);
            }
            else
            {
                Window.Functions = _mainWindow.Window.Functions = WMFunction.All & WMFunction.Close;
                _mainWindow.ExitMenuItem.Sensitive = false;

                YesButton.Hide();
                NoButton.Hide();
                ProgressBar.Show();

                SecondaryText.Text = "";
                _restartQuery = true;

                Updater.UpdateRyujinx(this, _buildUrl);
            }
        }

        private void NoButton_Clicked(object sender, EventArgs args)
        {
            Updater.Running = false;
            _mainWindow.Window.Functions = WMFunction.All;

            _mainWindow.ExitMenuItem.Sensitive = true;
            _mainWindow.UpdateMenuItem.Sensitive = true;

            Dispose();
        }
    }
}
