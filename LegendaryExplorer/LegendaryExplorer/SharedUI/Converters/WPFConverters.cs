using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(int), typeof(string))]
    public class CountToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int num)
            {
                if (parameter is string obj)
                {
                    //eg: num = 3, obj = "tool"; return  "3 tools"
                    return $"{num} {obj}{(num != 1 ? "s" : "")}";
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }

    //Converts DateTime to String
    //Is only a MultiValueConverter so that second binding can cause updates periodically
    public class RelativeDateTimeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] is DateTime d)
            {
                DateTime now = DateTime.Now;
                TimeSpan t = now - d;
                return d.ToString(t.TotalDays < 1 ? "h:mm tt" : "dd MMM yy");
            }
            return "";
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }
}