using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace ME3Explorer.SharedUI.Converters
{
    [ValueConversion(typeof(PackageEditorWPF.CurrentViewMode), typeof(SolidColorBrush))]
    public class PackageEditorWPFActiveViewHighlightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is PackageEditorWPF.CurrentViewMode))
            {
                return Brushes.Transparent; ;
            }
            if (parameter == null || !(parameter is string))
            {
                return Brushes.Transparent;
            }
            if ((string)value == (string)parameter)
            {
                return Brushes.LightBlue;
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
