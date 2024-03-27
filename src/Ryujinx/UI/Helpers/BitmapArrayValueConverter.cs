using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Globalization;
using System.IO;
using System.IO.Hashing;

namespace Ryujinx.Ava.UI.Helpers
{
    internal class BitmapArrayValueConverter : IValueConverter
    {
        public static readonly BitmapArrayValueConverter Instance = new();

        private readonly MemoryCache cache = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 24, CompactionPercentage = 0.25 });
        private readonly MemoryCacheEntryOptions options = new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromSeconds(10), Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            if (value is byte[] buffer && targetType == typeof(IImage))
            {
                var hash = XxHash3.Hash(buffer, 1);
                cache.TryGetValue(hash, out Bitmap result);

                if (result == null)
                {
                    using MemoryStream mem = new(buffer);
                    var bitmap = new Bitmap(mem);
                    if (bitmap.Size.Width > 256)
                    {
                        bitmap = bitmap.CreateScaledBitmap(new PixelSize(256, 256));
                    }
                    return cache.Set(hash, bitmap, options);
                }
                else
                {
                    return result;
                }
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
