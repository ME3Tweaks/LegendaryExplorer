using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AudioStreamHelper = LegendaryExplorer.UnrealExtensions.AudioStreamHelper;
using HIRCDisplayObject = LegendaryExplorer.UserControls.ExportLoaderControls.HIRCDisplayObject;

namespace LegendaryExplorer.SharedUI.Converters
{
    /// <summary>
    /// Shows/hides options based on WwiseStream or WwiseBank.
    /// </summary>
    [ValueConversion(typeof(uint), typeof(string))]
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

    [ValueConversion(typeof(uint), typeof(string))]
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

    [ValueConversion(typeof(byte), typeof(string))]
    public class HIRCObjectTypeConverter : IValueConverter
    {
        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return AudioStreamHelper.GetHircObjTypeString((byte)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(int), typeof(Visibility))]
    public class HIRCObjectTypeVisibilityConverter : IValueConverter
    {
        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && value != null)
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
