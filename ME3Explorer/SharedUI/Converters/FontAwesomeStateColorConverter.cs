using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ME3Explorer.SharedUI.Converters
{
    /// <summary>
    /// Returns a color for setting to the foreground of a fontawesome icon to indiciate it's state (black = enabled, grey = off)
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class FontAwesomeStateColorConverter : IValueConverter
    {
        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value) { return new SolidColorBrush(Colors.Black); }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
