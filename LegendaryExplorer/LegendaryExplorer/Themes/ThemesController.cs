using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using Be.Windows.Forms;
using LegendaryExplorer.Libraries;
using Color = System.Drawing.Color;

namespace LegendaryExplorer.Themes
{
    public static class ThemesController
    {
        public static ThemeType CurrentTheme { get; set; }

        private static ResourceDictionary ThemeDictionary
        {
            get => Application.Current.Resources.MergedDictionaries[0];
            set => Application.Current.Resources.MergedDictionaries[0] = value;
        }

        private static ResourceDictionary ControlColours
        {
            get => Application.Current.Resources.MergedDictionaries[1];
            set => Application.Current.Resources.MergedDictionaries[1] = value;
        }

        private static ResourceDictionary Controls
        {
            get => Application.Current.Resources.MergedDictionaries[2];
            set => Application.Current.Resources.MergedDictionaries[2] = value;
        }

        public static void SetTheme(ThemeType theme)
        {
            string themeName = theme.GetName();
            CurrentTheme = theme;
            if (string.IsNullOrEmpty(themeName))
            {
                return;
            }

            ThemeDictionary = new ResourceDictionary() { Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative) };
            ControlColours = new ResourceDictionary() { Source = new Uri("Themes/ControlColours.xaml", UriKind.Relative) };
            Controls = new ResourceDictionary() { Source = new Uri("Themes/Controls.xaml", UriKind.Relative) };


            SetLEXTheme();
        }


        public static object GetResource(object key)
        {
            return ThemeDictionary[key];
        }

        public static SolidColorBrush GetBrush(string name)
        {
            return GetResource(name) is SolidColorBrush brush ? brush : new SolidColorBrush(Colors.White);
        }

        // LEX SPECIFIC CODE

        private static void SetLEXTheme()
        {
            if (CurrentTheme == ThemeType.Light)
            {
                // Light
                HexBox.SetColors(System.Drawing.Color.White, System.Drawing.Color.Black);
            }
            else
            {
                // Dark 
                HexBox.SetColors(Color.FromArgb(255, 28, 28, 28), System.Drawing.Color.White);
            }
        }
    }
}