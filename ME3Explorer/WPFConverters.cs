using System;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    [ValueConversion(typeof(MEGame), typeof(ImageSource))]
    public class GameToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            MEGame game;
            if (value is MEGame)
            {
                game = (MEGame)value;
                switch (game)
                {
                    case MEGame.ME1:
                        return Application.Current.FindResource("iconME1") as ImageSource;
                    case MEGame.ME2:
                        return Application.Current.FindResource("iconME2") as ImageSource;
                    case MEGame.ME3:
                        return Application.Current.FindResource("iconME3") as ImageSource;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }

    [ValueConversion(typeof(string), typeof(string))]
    public class PathToFilenameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string)
            {
                return System.IO.Path.GetFileName((string)value);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }

    [ValueConversion(typeof(string), typeof(string))]
    public class PathToLocationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string)
            {
                string path = (string)value;
                if (path.StartsWith(ME3Directory.cookedPath) || path.StartsWith(ME2Directory.cookedPath) || path.StartsWith(ME1Directory.cookedPath))
                {
                    return "Basegame";
                }
                else if (path.StartsWith(ME3Directory.DLCPath) || path.StartsWith(ME2Directory.DLCPath))
                {
                    return System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path)));
                }
                else if (path.StartsWith(ME1Directory.DLCPath))
                {
                    int startIndex = path.IndexOf("DLC_");
                    return path.Substring(startIndex, path.Skip(startIndex).ToList().IndexOf('\\'));
                }

                return "External";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }

    [ValueConversion(typeof(long), typeof(string))]
    public class FileSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long)
            {
                return UsefulThings.General.GetFileSizeAsString((long)value);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }

    [ValueConversion(typeof(int), typeof(string))]
    public class CountToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int)
            {
                int val = (int)value;
                if (parameter is string)
                {
                    return val + " " + (string)parameter + (val != 1 ? "s" : "");
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }

    //Converts DateTime to String
    //Is only a MultiValueConverter so that second binding can cause updates periodically
    public class RelativeDateTimeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] is DateTime)
            {
                DateTime d = (DateTime)values[0];
                DateTime now = DateTime.Now;
                TimeSpan t = now - d;
                if (t.TotalDays < 1)
                {
                    return d.ToString("h:mm tt");
                }
                else
                {
                    return d.ToString("dd MMM yy");
                }
            }
            return "";
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }
}