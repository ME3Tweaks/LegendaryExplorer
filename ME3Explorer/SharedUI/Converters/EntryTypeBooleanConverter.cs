using ME3Explorer.Packages;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ME3Explorer.SharedUI.Converters
{
    [ValueConversion(typeof(IEntry), typeof(bool))]
    public class EntryTypeBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string classType && value is IEntry entry)
            {
                return entry.ClassName == classType;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
