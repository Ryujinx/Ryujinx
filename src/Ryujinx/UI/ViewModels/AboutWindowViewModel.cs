using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class AboutWindowViewModel : BaseModel, IDisposable
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
            UpdateLogoTheme(ConfigurationState.Shared.UI.BaseStyle.Value);
            Dispatcher.UIThread.InvokeAsync(DownloadPatronsJson);

            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() => UpdateLogoTheme(ConfigurationState.Shared.UI.BaseStyle.Value));
        }

        private void UpdateLogoTheme(string theme)
        {
            bool isDarkTheme = theme == "Dark" || (theme == "Auto" && App.DetectSystemTheme() == ThemeVariant.Dark);

            string basePath = "resm:Ryujinx.UI.Common.Resources.";
            string themeSuffix = isDarkTheme ? "Dark.png" : "Light.png";

            GithubLogo = LoadBitmap($"{basePath}Logo_GitHub_{themeSuffix}?assembly=Ryujinx.UI.Common");
            DiscordLogo = LoadBitmap($"{basePath}Logo_Discord_{themeSuffix}?assembly=Ryujinx.UI.Common");
            PatreonLogo = LoadBitmap($"{basePath}Logo_Patreon_{themeSuffix}?assembly=Ryujinx.UI.Common");
            TwitterLogo = LoadBitmap($"{basePath}Logo_Twitter_{themeSuffix}?assembly=Ryujinx.UI.Common");
        }

        private Bitmap LoadBitmap(string uri)
        {
            return new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri(uri)));
        }

        public void Dispose()
        {
            ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
            GC.SuppressFinalize(this);
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
