using System;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.Misc.Themes;

namespace LegendaryExplorer.SharedUI.Controls
{
    public class ListBoxScroll : ListBox
    {
        public ListBoxScroll()
        {
            SelectionChanged += ListBoxScroll_SelectionChanged;
            // For reasons not fully understood, implicit style resolution doesn't pick up the dark ListBox
            // style for this subclass the way it does for plain <ListBox> elements elsewhere (same
            // unexplained issue worked around in PackageEditorWindow.xaml.cs for LeftSide_ListView, before
            // this control had its own fix). A markup-level Style="{DynamicResource {x:Type ListBox}}"
            // was tried there and reverted - it coincided with a WPF-internal crash during resource-
            // dictionary invalidation (cause unconfirmed) - so push the resolved style from code instead,
            // matching that proven-safe pattern, here so every ListBoxScroll instance gets it for free.
            Loaded += ListBoxScroll_Loaded;
            Unloaded += ListBoxScroll_Unloaded;
        }

        void ListBoxScroll_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ScrollIntoView(SelectedItem);
        }

        private void ListBoxScroll_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        }

        private void ListBoxScroll_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e) => ApplyTheme();

        private void ApplyTheme()
        {
            Style = TryFindResource(typeof(ListBox)) as Style;
        }
    }
}
