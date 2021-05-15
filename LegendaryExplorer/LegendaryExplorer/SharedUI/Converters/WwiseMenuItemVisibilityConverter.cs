using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorer.Tools.Soundplorer;

namespace LegendaryExplorer.SharedUI.Converters
{
    /// <summary>
    /// Shows/hides options based on WwiseStream or WwiseBank.
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class WwiseMenuItemVisibilityConverter : IValueConverter
    {
        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SoundplorerExport && parameter is string)
            {
                return (value as SoundplorerExport).Export.ClassName == (string)parameter ? Visibility.Visible : Visibility.Collapsed;
            }
            if (value is ISACTFileEntry && parameter is string) return (parameter as string) == "ISACTFileEntry" ? Visibility.Visible : Visibility.Collapsed;
            if (value is AFCFileEntry && parameter is string) return (parameter as string) == "AFCFileEntry" ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
