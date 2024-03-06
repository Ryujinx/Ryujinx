using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Globalization;
using System.IO;

namespace Ryujinx.Ava.UI.Helpers
{
    internal class BitmapArrayValueConverter : IValueConverter
    {
        public static BitmapArrayValueConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            if (value is byte[] buffer && targetType == typeof(IImage))
            {
                MemoryStream mem = new(buffer);

                return new Bitmap(mem);
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
