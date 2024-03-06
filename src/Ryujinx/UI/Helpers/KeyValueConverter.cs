using Avalonia.Data.Converters;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using System;
using System.Globalization;

namespace Ryujinx.Ava.UI.Helpers
{
    internal class KeyValueConverter : IValueConverter
    {
        public static KeyValueConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            object key = null;

            if (value != null)
            {
                if (targetType == typeof(Key))
                {
                    key = Enum.Parse<Key>(value.ToString());
                }
                else if (targetType == typeof(GamepadInputId))
                {
                    key = Enum.Parse<GamepadInputId>(value.ToString());
                }
                else if (targetType == typeof(StickInputId))
                {
                    key = Enum.Parse<StickInputId>(value.ToString());
                }
            }

            return key;
        }
    }
}
