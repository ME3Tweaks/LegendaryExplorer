using System;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorer.SharedUI.Converters
{
    public class FilesizeToHumanSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l)
            {
                return FileSize.FormatSize(l);
            }
            return $"Invalid value {value}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
