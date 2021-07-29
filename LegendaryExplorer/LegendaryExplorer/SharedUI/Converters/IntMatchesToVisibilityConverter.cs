using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(int), typeof(Visibility))]
    public class IntMatchesToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string str)
            {
                var items = str.Split(';');
                bool inverse = items.Length == 2 && items[1].Equals("not", StringComparison.InvariantCultureIgnoreCase);
                if (int.TryParse(items[0], out var val))
                {
                    if (inverse)
                    {
                        return (int)value != val ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        return (int)value == val ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); //we don't care
        }
    }
}
