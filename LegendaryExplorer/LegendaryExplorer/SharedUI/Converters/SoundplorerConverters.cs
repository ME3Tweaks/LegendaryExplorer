using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ME3Tweaks.Wwiser.Model.Hierarchy;
using ME3Tweaks.Wwiser.Model.Hierarchy.Enums;
using AudioStreamHelper = LegendaryExplorer.UnrealExtensions.AudioStreamHelper;
using HIRCDisplayObject = LegendaryExplorer.UserControls.ExportLoaderControls.Soundpanel.HIRCDisplayObject;

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
            if (value is HircItemContainer { Type.Value: HircType.Sound, Item: Sound snd})
            {
                return Enum.GetName(typeof(StreamType.StreamTypeInner), snd.BankSourceData.StreamType.Value);
            }
            return "No media";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(byte), typeof(string))]
    public class HIRCObjectTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HircItemContainer { Type: { } hircType })
            {
                return Enum.GetName(typeof(HircType), hircType.Value);
            }

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(int), typeof(Visibility))]
    public class HIRCObjectTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is HircType parameterType && value is HIRCDisplayObject hdo)
            {
                return parameterType == hdo.Item.Type.Value ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
