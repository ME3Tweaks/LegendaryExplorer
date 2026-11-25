using System.Windows;
using System.Windows.Media;
using LegendaryExplorer.Misc.Themes;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Colours a Level Editor window's title bar like its toolbar, from LevelEditorStyles.xaml's brushes.
/// </summary>
internal static class EditorTitleBar
{
    /// <summary>Call after InitializeComponent, so the window's resources are loaded.</summary>
    public static void Apply(Window window)
    {
        ThemeManager.UseDarkTitleBar(window,
            ((SolidColorBrush)window.FindResource("EditorChrome")).Color,
            ((SolidColorBrush)window.FindResource("EditorText")).Color);
    }
}
