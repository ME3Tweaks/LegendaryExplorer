using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ME3ExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(IEntry), typeof(Visibility))]
    public class EntryTypeVisibiltyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string classType)
            {
                if (classType == "ImportEntry" && value is ImportEntry)
                {
                    return Visibility.Visible;
                }
                if (classType == "ExportEntry" && value is ExportEntry)
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
