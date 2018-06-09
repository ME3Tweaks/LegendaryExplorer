using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ME3Explorer.SharedUI
{
    class Conveters
    {
        [ValueConversion(typeof(bool), typeof(GridLength))]
        public class BoolToGridRowHeightConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return ((bool)value == true) ? new GridLength(25, GridUnitType.Pixel) : new GridLength(0);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {    // Don't need any convert back
                return null;
            }
        }
    }
}
