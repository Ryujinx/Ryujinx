using LibHac.Ns;
using Ryujinx.Ava.Common.Locale;

namespace Ryujinx.Ava.UI.Models
{
    public class TitleUpdateModel
    {
        public ApplicationControlProperty Control { get; }
        public string Path { get; }

        private bool IsXci => System.IO.Path.GetExtension(Path)?.ToLower() == ".xci";

        public string Label => LocaleManager.Instance.UpdateAndGetDynamicValue(
            IsXci ? LocaleKeys.TitleBundledUpdateVersionLabel : LocaleKeys.TitleUpdateVersionLabel,
            Control.DisplayVersionString.ToString()
        );

        public TitleUpdateModel(ApplicationControlProperty control, string path)
        {
            Control = control;
            Path = path;
        }
    }
}
