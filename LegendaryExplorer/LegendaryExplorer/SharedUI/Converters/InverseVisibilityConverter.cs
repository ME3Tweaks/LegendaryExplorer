using System;
using System.Windows;
using System.Windows.Data;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(Visibility), typeof(Visibility))]
    public class InverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
                              System.Globalization.CultureInfo culture)
        {
            if (value is Visibility v)
            {
                return v switch
                {
                    Visibility.Visible => Visibility.Collapsed,
                    _ => Visibility.Visible,
                };
            }
            throw new InvalidOperationException("The target must be a Visibility");
        }

        public object ConvertBack(object value, Type targetType, object parameter,
                                  System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
