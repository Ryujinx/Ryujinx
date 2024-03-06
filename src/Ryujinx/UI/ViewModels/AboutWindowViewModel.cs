using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class AboutWindowViewModel : BaseModel
    {
        private Bitmap _githubLogo;
        private Bitmap _discordLogo;
        private Bitmap _patreonLogo;
        private Bitmap _twitterLogo;

        private string _version;
        private string _supporters;

        public Bitmap GithubLogo
        {
            get => _githubLogo;
            set
            {
                _githubLogo = value;
                OnPropertyChanged();
            }
        }

        public Bitmap DiscordLogo
        {
            get => _discordLogo;
            set
            {
                _discordLogo = value;
                OnPropertyChanged();
            }
        }

        public Bitmap PatreonLogo
        {
            get => _patreonLogo;
            set
            {
                _patreonLogo = value;
                OnPropertyChanged();
            }
        }

        public Bitmap TwitterLogo
        {
            get => _twitterLogo;
            set
            {
                _twitterLogo = value;
                OnPropertyChanged();
            }
        }

        public string Supporters
        {
            get => _supporters;
            set
            {
                _supporters = value;
                OnPropertyChanged();
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged();
            }
        }

        public string Developers => LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.AboutPageDeveloperListMore, "gdkchan, Ac_K, marysaka, rip in peri peri, LDj3SNuD, emmaus, Thealexbarney, GoffyDude, TSRBerry, IsaacMarovitz");

        public AboutWindowViewModel()
        {
            Version = Program.Version;

            if (ConfigurationState.Instance.UI.BaseStyle.Value == "Light")
            {
                GithubLogo = new Bitmap(AssetLoader.Open(new Uri("resm:Ryujinx.UI.Common.Resources.Logo_GitHub_Light.png?assembly=Ryujinx.UI.Common")));
                DiscordLogo = new Bitmap(AssetLoader.Open(new Uri("resm:Ryujinx.UI.Common.Resources.Logo_Discord_Light.png?assembly=Ryujinx.UI.Common")));
                PatreonLogo = new Bitmap(AssetLoader.Open(new Uri("resm:Ryujinx.UI.Common.Resources.Logo_Patreon_Light.png?assembly=Ryujinx.UI.Common")));
                TwitterLogo = new Bitmap(AssetLoader.Open(new Uri("resm:Ryujinx.UI.Common.Resources.Logo_Twitter_Light.png?assembly=Ryujinx.UI.Common")));
            }
            else
            {
                GithubLogo = new Bitmap(AssetLoader.Open(new Uri("resm:Ryujinx.UI.Common.Resources.Logo_GitHub_Dark.png?assembly=Ryujinx.UI.Common")));
                DiscordLogo = new Bitmap(AssetLoader.Open(new Uri("resm:Ryujinx.UI.Common.Resources.Logo_Discord_Dark.png?assembly=Ryujinx.UI.Common")));
                PatreonLogo = new Bitmap(AssetLoader.Open(new Uri("resm:Ryujinx.UI.Common.Resources.Logo_Patreon_Dark.png?assembly=Ryujinx.UI.Common")));
                TwitterLogo = new Bitmap(AssetLoader.Open(new Uri("resm:Ryujinx.UI.Common.Resources.Logo_Twitter_Dark.png?assembly=Ryujinx.UI.Common")));
            }

            Dispatcher.UIThread.InvokeAsync(DownloadPatronsJson);
        }

        private async Task DownloadPatronsJson()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                Supporters = LocaleManager.Instance[LocaleKeys.ConnectionError];

                return;
            }

            HttpClient httpClient = new();

            try
            {
                string patreonJsonString = await httpClient.GetStringAsync("https://patreon.ryujinx.org/");

                Supporters = string.Join(", ", JsonHelper.Deserialize(patreonJsonString, CommonJsonContext.Default.StringArray)) + "\n\n";
            }
            catch
            {
                Supporters = LocaleManager.Instance[LocaleKeys.ApiError];
            }
        }
    }
}
