using System;
using System.Windows.Data;

namespace LegendaryExplorer.SharedUI.Converters
{
    // TODO: Name this something more descriptive
    [ValueConversion(typeof(double), typeof(double))]
    public class YConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)parameter - (double)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)parameter - (double)value;
        }
    }
}
