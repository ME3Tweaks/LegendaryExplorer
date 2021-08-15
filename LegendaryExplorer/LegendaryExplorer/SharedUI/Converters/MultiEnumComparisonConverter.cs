using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace LegendaryExplorer.SharedUI.Converters
{
    public class MultiEnumComparisonConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (values.All(v => v.ToString() == values[0].ToString()));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}