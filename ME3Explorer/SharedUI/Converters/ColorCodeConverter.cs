using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace ME3Explorer.SharedUI.Converters
{
    [ValueConversion(typeof(UProperty), typeof(Color))]
    public class ColorCodeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                return (Color)ColorConverter.ConvertFromString((string)value);
            }
            return Color.FromRgb(0, 0, 0);
            ////Debug.WriteLine("booltocollapsed: " + ((bool)value == true).ToString());
            //if (value is StructProperty sp)
            //{
            //    if (sp.StructType == "Color")
            //    {
            //        PropertyCollection colorProps = sp.Properties;
            //        var color = System.Windows.Media.Color.FromArgb(colorProps.GetProp<ByteProperty>("A").Value, colorProps.GetProp<ByteProperty>("R").Value, colorProps.GetProp<ByteProperty>("G").Value, colorProps.GetProp<ByteProperty>("B").Value);
            //        return color;
            //    }
            //}
            //return System.Windows.Media.Color.FromArgb(255, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((Color)value).ToString();
        }
    }
}
