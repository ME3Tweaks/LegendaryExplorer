using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// From https://stackoverflow.com/questions/42297032/why-does-wpf-textbox-not-support-triple-click-to-select-all-text
    /// </summary>
    public static class TextBoxBehavior
    {
        public static readonly DependencyProperty TripleClickSelectAllProperty = DependencyProperty.RegisterAttached(
            "TripleClickSelectAll", typeof(bool), typeof(TextBoxBehavior), new PropertyMetadata(false, OnPropertyChanged));

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tb = d as TextBox;
            if (tb != null)
            {
                var enable = (bool)e.NewValue;
                if (enable)
                {
                    tb.PreviewMouseLeftButtonDown += OnTextBoxMouseDown;
                }
                else
                {
                    tb.PreviewMouseLeftButtonDown -= OnTextBoxMouseDown;
                }
            }
        }

        private static void OnTextBoxMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 3)
            {
                ((TextBox)sender).SelectAll();
            }
        }

        public static void SetTripleClickSelectAll(DependencyObject element, bool value)
        {
            element.SetValue(TripleClickSelectAllProperty, value);
        }

        public static bool GetTripleClickSelectAll(DependencyObject element)
        {
            return (bool)element.GetValue(TripleClickSelectAllProperty);
        }
    }
}
