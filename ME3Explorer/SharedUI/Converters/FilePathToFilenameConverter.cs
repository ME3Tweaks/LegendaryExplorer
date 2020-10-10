using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace ME3Explorer.SharedUI.Converters
{
    public class FilePathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return Path.GetFileName(str);
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}