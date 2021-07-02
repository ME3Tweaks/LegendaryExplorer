using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LegendaryExplorer.SharedUI.Converters
{
    public class NullVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string str)
            {
                if (str == "Reversed")
                {
                    return value is null ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return value is null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
