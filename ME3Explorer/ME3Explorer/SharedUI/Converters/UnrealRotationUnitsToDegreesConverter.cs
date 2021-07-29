using System;
using System.Globalization;
using System.Windows.Data;
using ME3ExplorerCore.Helpers;

namespace ME3Explorer.SharedUI.Converters
{
    [ValueConversion(typeof(int), typeof(double))]
    public class UnrealRotationUnitsToDegreesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
            {
                return i.UnrealRotationUnitsToDegrees();
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return ((float)d).DegreesToUnrealRotationUnits();
            }

            return 0;
        }
    }
}
