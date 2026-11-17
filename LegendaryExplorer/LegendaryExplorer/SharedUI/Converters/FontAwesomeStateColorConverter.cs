using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LegendaryExplorer.Misc.Themes;

namespace LegendaryExplorer.SharedUI.Converters
{
    /// <summary>
    /// Returns a color for setting to the foreground of a fontawesome icon to indiciate it's state (black = enabled, grey = off)
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class FontAwesomeStateColorConverter : IValueConverter
    {
        // Same hex values used for TreeViewEntry's dark-mode brushes elsewhere in this app - black-on-dark
        // is invisible, so dark mode needs its own "on" (light, matches Theme.Foreground.Primary) and
        // "off" (mid-gray, matches Theme.Foreground.Secondary) pair rather than the light-theme literals.
        private static readonly SolidColorBrush DarkOnBrush = FrozenBrush(0xDC, 0xDC, 0xDC);
        private static readonly SolidColorBrush DarkOffBrush = FrozenBrush(0x9D, 0x9D, 0x9D);

        private static SolidColorBrush FrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value) { return ThemeManager.IsDarkMode ? DarkOnBrush : new SolidColorBrush(Colors.Black); }
            return ThemeManager.IsDarkMode ? DarkOffBrush : new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
