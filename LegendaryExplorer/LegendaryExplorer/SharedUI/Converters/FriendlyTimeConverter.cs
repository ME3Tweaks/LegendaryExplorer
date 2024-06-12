using System;
using System.Globalization;
using System.Windows.Data;

namespace LegendaryExplorer.SharedUI.Converters
{
    public class FriendlyTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TimeSpan ts = TimeSpan.FromSeconds((double)value);
            if (parameter is string str && str == "IncludeFractionalSeconds")
            {
                return $"{ts.Minutes}:{ts.Seconds:D2}:{ts.Milliseconds:D3}";
            }
            return $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
