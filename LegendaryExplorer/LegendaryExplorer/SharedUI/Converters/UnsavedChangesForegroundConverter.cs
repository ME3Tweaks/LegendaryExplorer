using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LegendaryExplorer.Misc.Themes;

namespace LegendaryExplorer.SharedUI.Converters
{
    /// <summary>
    /// Returns a color for setting to the foreground of a button for Save Hex Changes
    /// </summary>
    [ValueConversion(typeof(bool), typeof(SolidColorBrush))]
    public class UnsavedChangesForegroundConverter : IValueConverter
    {
        // Same hex value used for Theme.Foreground.Primary elsewhere - black is invisible on the dark
        // theme's toolbar background, so dark mode needs its own "no unsaved changes" color.
        private static readonly SolidColorBrush DarkNoChangesBrush = FrozenBrush(0xDC, 0xDC, 0xDC);

        private static SolidColorBrush FrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // parameter is allowed class type for visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value) { return new SolidColorBrush(Colors.Red); }
            return ThemeManager.IsDarkMode ? DarkNoChangesBrush : new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
