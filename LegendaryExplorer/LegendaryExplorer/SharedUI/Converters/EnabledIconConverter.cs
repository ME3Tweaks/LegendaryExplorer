using System;
using System.Globalization;
using System.Windows.Data;
using FontAwesome5;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(bool), typeof(EFontAwesomeIcon))]
    public class EnabledIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value switch
            {
                true => EFontAwesomeIcon.Solid_TimesCircle,
                _ => EFontAwesomeIcon.Solid_CheckCircle
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}