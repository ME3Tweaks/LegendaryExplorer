using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class EnabledBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value switch
            {
                true => Brushes.Red,
                _ => Brushes.Green
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}