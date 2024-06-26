using LibHac.Ns;
using Ryujinx.Ava.Common.Locale;

namespace Ryujinx.Ava.UI.Models
{
    public class TitleUpdateModel
    {
        public ApplicationControlProperty Control { get; }
        public string Path { get; }

        public string Label => LocaleManager.Instance.UpdateAndGetDynamicValue(
            System.IO.Path.GetExtension(Path)?.ToLower() == ".xci" ? LocaleKeys.TitleBundledUpdateVersionLabel : LocaleKeys.TitleUpdateVersionLabel,
            Control.DisplayVersionString.ToString()
        );

        public TitleUpdateModel(ApplicationControlProperty control, string path)
        {
            Control = control;
            Path = path;
        }
    }
}
