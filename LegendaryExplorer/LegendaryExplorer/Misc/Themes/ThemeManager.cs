using System;
using System.ComponentModel;
using System.Windows;
using Be.Windows.Forms;
using Dark.Net;
using LegendaryExplorer.Misc.AppSettings;

namespace LegendaryExplorer.Misc.Themes
{
    /// <summary>
    /// Applies/removes the dark-theme resource dictionaries on individual tool windows and keeps them
    /// live-updated when <see cref="Settings.Global_DarkModeEnabled"/> changes. Dictionaries are only ever
    /// merged into a window's own <see cref="FrameworkElement.Resources"/> - never at Application scope -
    /// so untracked windows (the hub, <see cref="LegendaryExplorer.MainWindow.LEXMainWindow"/>) are never affected.
    /// </summary>
    public static class ThemeManager
    {
        private const string PaletteDarkUri = "/LegendaryExplorer;component/Misc/Themes/Palette.Dark.xaml";
        private const string PaletteLightUri = "/LegendaryExplorer;component/Misc/Themes/Palette.Light.xaml";
        private const string ControlsSharedUri = "/LegendaryExplorer;component/Misc/Themes/Controls.Shared.xaml";
        private const string ControlsXceedUri = "/LegendaryExplorer;component/Misc/Themes/Controls.Xceed.xaml";
        private static readonly string[] AllThemeDictionaryUris = { PaletteDarkUri, PaletteLightUri, ControlsSharedUri, ControlsXceedUri };

        public static event EventHandler ThemeChanged;

        public static bool IsDarkMode => Settings.Global_DarkModeEnabled;

        static ThemeManager()
        {
            Settings.StaticPropertyChanged += Settings_StaticPropertyChanged;
        }

        private static void Settings_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.Global_DarkModeEnabled))
            {
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Applies the current theme to <paramref name="window"/> and subscribes it to future theme changes
        /// for the rest of its lifetime.
        /// </summary>
        /// <remarks>
        /// Two different timing constraints are in play here, so the initial apply is split in two:
        /// - The WPF resource-dictionary merge must happen on <see cref="FrameworkElement.Loaded"/>, since it
        ///   runs after the derived class's InitializeComponent() - windows that declare their own local
        ///   &lt;Window.Resources&gt; would otherwise silently discard an earlier merge.
        /// - DarkNet's title-bar theming must happen in/before <see cref="Window.SourceInitialized"/> (its own
        ///   constructor or OnSourceInitialized) - calling it as late as Loaded throws
        ///   InvalidOperationException("Called SetWindowThemeWpf() too late...").
        /// Once the window is alive, later theme-change notifications call both together - by then both
        /// timing constraints are already satisfied.
        /// </remarks>
        public static void RegisterWindow(Window window)
        {
            window.SourceInitialized += (_, _) => ApplyTitleBarTheme(window);
            window.Loaded += (_, _) => ApplyResourceDictionaries(window);

            EventHandler handler = null;
            handler = (_, _) =>
            {
                ApplyResourceDictionaries(window);
                ApplyTitleBarTheme(window);
            };
            window.Closed += (_, _) => ThemeChanged -= handler;
            ThemeChanged += handler;
        }

        private static void ApplyResourceDictionaries(Window window)
        {
            RemoveThemeDictionaries(window.Resources.MergedDictionaries);
            // The palette is merged in both modes (Dark or Light) so that any `DynamicResource Theme.X`
            // used in ordinary window XAML - not just the dark-only dictionaries below - always resolves,
            // even when dark mode is off. Palette.Light's values mirror today's existing look, so merging
            // it has no visible effect until a hardcoded literal is deliberately replaced with a Theme.X
            // reference during remediation.
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(IsDarkMode ? PaletteDarkUri : PaletteLightUri, UriKind.Relative) });
            if (IsDarkMode)
            {
                window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(ControlsSharedUri, UriKind.Relative) });
                window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(ControlsXceedUri, UriKind.Relative) });
            }
        }

        /// <summary>
        /// Themes the window's native (non-client) title bar via DarkNet/DWM - separate from the WPF content,
        /// which is why it's otherwise stuck matching whatever the OS's own system theme happens to be,
        /// independent of this app's own dark-mode toggle.
        /// </summary>
        /// <remarks>
        /// Known gap: the title bar doesn't repaint live when the theme is toggled after the window is
        /// already open (DarkNet docs say re-calling SetWindowThemeWpf later is supported, but changing
        /// DWMWA_USE_IMMERSIVE_DARK_MODE on an already-visible window doesn't force a non-client repaint on
        /// its own). Not fixing that here for now - wrapped in try/catch since this whole feature is
        /// cosmetic and must never be able to take down window opening if DWM/DarkNet misbehaves.
        /// </remarks>
        private static void ApplyTitleBarTheme(Window window)
        {
            try
            {
                if (IsDarkMode)
                {
                    DarkNet.Instance.SetWindowThemeWpf(window, Theme.Dark, new ThemeOptions
                    {
                        TitleBarBackgroundColor = System.Drawing.Color.FromArgb(0xFF, 0x25, 0x25, 0x26),
                        TitleBarTextColor = System.Drawing.Color.FromArgb(0xFF, 0xDC, 0xDC, 0xDC)
                    });
                }
                else
                {
                    DarkNet.Instance.SetWindowThemeWpf(window, Theme.Auto, new ThemeOptions());
                }
            }
            catch (Exception)
            {
                // Never let title-bar theming take the app down - it's a cosmetic nice-to-have, not core
                // functionality. See remarks above re: known live-update limitation.
            }
        }

        private static void RemoveThemeDictionaries(System.Collections.Generic.ICollection<ResourceDictionary> mergedDictionaries)
        {
            foreach (var uri in AllThemeDictionaryUris)
            {
                var absoluteUri = new Uri(uri, UriKind.Relative);
                foreach (var dict in new System.Collections.Generic.List<ResourceDictionary>(mergedDictionaries))
                {
                    if (dict.Source != null && Uri.Compare(dict.Source, absoluteUri, UriComponents.Path, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        mergedDictionaries.Remove(dict);
                    }
                }
            }
        }

        /// <summary>
        /// Pushes the current theme's colors onto a WinForms <see cref="HexBox"/> hosted via WindowsFormsHost.
        /// HexBox sits outside the WPF resource system, so this must be called explicitly (typically from a
        /// ThemeChanged subscription on the hosting UserControl) rather than relying on DynamicResource.
        /// BackColor/ForeColor are pushed via HexBox's own static <see cref="HexBox.SetColors"/> (they're
        /// backed by static fields shared by every HexBox instance in the app - see HexBox.cs's
        /// "DarkModeSupport" region); everything else here is a normal per-instance property.
        /// </summary>
        public static void ApplyHexBoxTheme(HexBox hexBox)
        {
            if (hexBox == null)
                return;

            if (IsDarkMode)
            {
                HexBox.SetColors(System.Drawing.Color.FromArgb(0x3C, 0x3C, 0x3C), System.Drawing.Color.FromArgb(0xDC, 0xDC, 0xDC));
                hexBox.BackColorDisabled = System.Drawing.Color.FromArgb(0x2D, 0x2D, 0x30);
                hexBox.InfoForeColor = System.Drawing.Color.FromArgb(0x9D, 0x9D, 0x9D);
                hexBox.LineInfoForeColor = System.Drawing.Color.FromArgb(0x9D, 0x9D, 0x9D);
                hexBox.SelectionBackColor = System.Drawing.Color.FromArgb(0x09, 0x47, 0x71);
                hexBox.SelectionForeColor = System.Drawing.Color.White;
            }
            else
            {
                HexBox.SetColors(System.Drawing.SystemColors.Window, System.Drawing.SystemColors.ControlText);
                hexBox.BackColorDisabled = System.Drawing.Color.WhiteSmoke;
                hexBox.InfoForeColor = System.Drawing.Color.Gray;
                hexBox.LineInfoForeColor = System.Drawing.Color.Gray;
                hexBox.SelectionBackColor = System.Drawing.Color.Blue;
                hexBox.SelectionForeColor = System.Drawing.Color.White;
            }

            hexBox.Invalidate();
        }
    }
}
