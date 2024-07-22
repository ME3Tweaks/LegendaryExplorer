using System;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(ExportEntry), typeof(bool))]
    public class WwiseDataExchangeEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ExportEntry))
            {
                return false;
            }
            return ((ExportEntry)value).ClassName == "WwiseStream";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
