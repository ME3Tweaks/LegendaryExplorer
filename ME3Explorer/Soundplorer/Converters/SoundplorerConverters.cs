using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using ME3ExplorerCore.Unreal;
using WwiseStreamHelper = ME3Explorer.Unreal.WwiseStreamHelper;

namespace ME3Explorer.Soundplorer
{
    /// <summary>
    /// Shows/hides options based on WwiseStream or WwiseBank.
    /// </summary>
    public class HIRCSoundTypeConverter : IValueConverter
    {
        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            uint b = (uint)value;
            return b switch
            {
                0 => $"Sound SFX({value})",
                _ => $"Sound Voice({value})"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HIRCMediaFetchTypeConverter : IValueConverter
    {
        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            uint i = (uint)value;
            return i switch
            {
                0 => $"Embedded",
                1 => $"Streamed",
                2 => $"Streamed with prefetch",
                _ => $"Unknown playback fetch type: {value}"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HIRCObjectTypeConverter : IValueConverter
    {
        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return WwiseStreamHelper.GetHircObjTypeString((byte)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HIRCObjectTypeVisibilityConverter : IValueConverter
    {
        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null)
            {
                int iparameter = int.Parse((string)parameter);
                HIRCDisplayObject ho = (HIRCDisplayObject)value;
                return iparameter == ho.ObjType ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
