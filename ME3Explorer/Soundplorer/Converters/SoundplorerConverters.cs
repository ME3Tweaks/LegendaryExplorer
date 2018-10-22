using ME3Explorer.Unreal.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

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
            int b = (int)value;
            switch (b)
            {
                case 0:
                    return $"Sound SFX({value})";

                default:
                    return $"Sound Voice({value})";
            }
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
            int i = (int)value;
            switch (i)
            {
                case 0:
                    return $"Embedded";
                case 1:
                    return $"Streamed";
                case 2:
                    return $"Streamed with prefetch";
                default:
                    return $"Unknown playback fetch type: {value}";
            }
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
            return WwiseBank.GetHircObjType((byte)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
