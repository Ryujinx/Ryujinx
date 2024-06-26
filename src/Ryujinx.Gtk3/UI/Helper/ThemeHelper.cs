using Gtk;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.UI.Common.Configuration;
using System.IO;

namespace Ryujinx.UI.Helper
{
    static class ThemeHelper
    {
        public static void ApplyTheme()
        {
            if (!ConfigurationState.Shared.UI.EnableCustomTheme)
            {
                return;
            }

            if (File.Exists(ConfigurationState.Shared.UI.CustomThemePath) && (Path.GetExtension(ConfigurationState.Shared.UI.CustomThemePath) == ".css"))
            {
                CssProvider cssProvider = new();

                cssProvider.LoadFromPath(ConfigurationState.Shared.UI.CustomThemePath);

                StyleContext.AddProviderForScreen(Gdk.Screen.Default, cssProvider, 800);
            }
            else
            {
                Logger.Warning?.Print(LogClass.Application, $"The \"custom_theme_path\" section in \"{ReleaseInformation.ConfigName}\" contains an invalid path: \"{ConfigurationState.Shared.UI.CustomThemePath}\".");

                ConfigurationState.Shared.UI.CustomThemePath.Value = "";
                ConfigurationState.Shared.UI.EnableCustomTheme.Value = false;
                ConfigurationState.Shared.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
        }
    }
}
