using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ME3Explorer.SharedUI.Converters
{
    [ValueConversion(typeof(IExportEntry), typeof(bool))]
    public class WwiseDataExchangeEnabledConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is IExportEntry))
            {
                return false;
            }
            return ((IExportEntry)value).ClassName == "WwiseStream";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
