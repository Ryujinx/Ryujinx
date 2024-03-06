using Gtk;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Helper;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;

namespace Ryujinx.UI.Windows
{
    public partial class AboutWindow : Window
    {
        public AboutWindow() : base($"Ryujinx {Program.Version} - About")
        {
            Icon = new Gdk.Pixbuf(Assembly.GetAssembly(typeof(OpenHelper)), "Ryujinx.UI.Common.Resources.Logo_Ryujinx.png");
            InitializeComponent();

            _ = DownloadPatronsJson();
        }

        private async Task DownloadPatronsJson()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                _patreonNamesText.Buffer.Text = "Connection Error.";
            }

            HttpClient httpClient = new();

            try
            {
                string patreonJsonString = await httpClient.GetStringAsync("https://patreon.ryujinx.org/");

                _patreonNamesText.Buffer.Text = string.Join(", ", JsonHelper.Deserialize(patreonJsonString, CommonJsonContext.Default.StringArray));
            }
            catch
            {
                _patreonNamesText.Buffer.Text = "API Error.";
            }
        }

        //
        // Events
        //
        private void RyujinxButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenHelper.OpenUrl("https://ryujinx.org");
        }

        private void AmiiboApiButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenHelper.OpenUrl("https://amiiboapi.com");
        }

        private void PatreonButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenHelper.OpenUrl("https://www.patreon.com/ryujinx");
        }

        private void GitHubButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenHelper.OpenUrl("https://github.com/Ryujinx/Ryujinx");
        }

        private void DiscordButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenHelper.OpenUrl("https://discordapp.com/invite/N2FmfVc");
        }

        private void TwitterButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenHelper.OpenUrl("https://twitter.com/RyujinxEmu");
        }

        private void ContributorsButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenHelper.OpenUrl("https://github.com/Ryujinx/Ryujinx/graphs/contributors?type=a");
        }

        private void ChangelogButton_Pressed(object sender, ButtonPressEventArgs args)
        {
            OpenHelper.OpenUrl("https://github.com/Ryujinx/Ryujinx/wiki/Changelog#ryujinx-changelog");
        }
    }
}
