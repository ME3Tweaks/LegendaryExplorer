using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class NullOrEmptyStringVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //Debug.WriteLine("booltocollapsed: " + ((bool)value == true).ToString());
            if (value is string str)
            {
                return (str.Trim() != "") ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
