using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.UI.Common.Helper;
using System;
using System.Globalization;

namespace Ryujinx.Ava.UI.Helpers
{
    /// <summary>
    /// This <see cref="IValueConverter"/> makes sure that the string "Never" that's returned by <see cref="ValueFormatUtils.FormatDateTime"/> is properly localized in the Avalonia UI.
    /// After the Avalonia UI has been made the default and the GTK UI is removed, <see cref="ValueFormatUtils"/> should be updated to directly return a localized string.
    /// </summary>
    internal class LocalizedNeverConverter : MarkupExtension, IValueConverter
    {
        private static readonly LocalizedNeverConverter _instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string valStr)
            {
                return "";
            }

            if (valStr == "Never")
            {
                return LocaleManager.Instance[LocaleKeys.Never];
            }

            return valStr;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return _instance;
        }
    }
}
