using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ME3Explorer.Matinee
{
    [ValueConversion(typeof(float), typeof(double))]
    public class TimeToPositionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            float time = (float)values[0];
            double scale = (double)values[1];
            double offset = (double)values[2];
            return (time + offset) * scale;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new InvalidOperationException();
        }
    }
}
