using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Converters
{
    // Ported from ME3Tweaks Mod Manager
    [Localizable(false)]
    public class GameToImageIconConverter : IValueConverter
    {
        private static BitmapImage me1Icon;
        private static BitmapImage me2Icon;
        private static BitmapImage me3Icon;
        private static BitmapImage le1Icon;
        private static BitmapImage le2Icon;
        private static BitmapImage le3Icon;
        private static BitmapImage unknownIcon;
        private static BitmapImage udkIcon;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return StaticConvert(value, parameter);
        }

        public static BitmapImage StaticConvert(object value, object parameter = null)
        {
            if (value == null) return null;
            init();
            var game = (MEGame)value;
            switch (game)
            {
                case MEGame.ME1:
                    return me1Icon;
                case MEGame.ME2:
                    return me2Icon;
                case MEGame.ME3:
                    return me3Icon;
                case MEGame.LE1:
                    return le1Icon;
                case MEGame.LE2:
                    return le2Icon;
                case MEGame.LE3:
                    return le3Icon;
                case MEGame.UDK:
                    return udkIcon;
                case MEGame.Unknown:
                    return unknownIcon;
                default:
                    return null;
            }
        }

        private static bool initialized;
        private static void init()
        {
            if (initialized) return;
            me1Icon = (BitmapImage)Application.Current.Resources[@"me1gameicon"];
            me2Icon = (BitmapImage)Application.Current.Resources[@"me2gameicon"];
            me3Icon = (BitmapImage)Application.Current.Resources[@"me3gameicon"];
            le1Icon = (BitmapImage)Application.Current.Resources[@"le1gameicon"];
            le2Icon = (BitmapImage)Application.Current.Resources[@"le2gameicon"];
            le3Icon = (BitmapImage)Application.Current.Resources[@"le3gameicon"];
            udkIcon = (BitmapImage)Application.Current.Resources[@"udkgameicon"];
            unknownIcon = (BitmapImage)Application.Current.Resources[@"unknowngameicon"];
            initialized = true;
        }

        private static object FindMergedResource(string mergedFilename, string key)
        {
            foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
            {
                var dictUri = dictionary.Source.ToString();
                if (dictUri.EndsWith(mergedFilename))
                {
                    return dictionary[key];
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}