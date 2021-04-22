using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ME3ExplorerCore.Unreal;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(Property), typeof(Color))]
    public class ColorCodeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                return (Color)ColorConverter.ConvertFromString((string)value);
            }
            return Color.FromRgb(0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((Color)value).ToString();
        }
    }
}
