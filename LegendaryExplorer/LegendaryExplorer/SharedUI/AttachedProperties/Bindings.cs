using System.Windows;
using System.Windows.Data;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Converters;

namespace LegendaryExplorer.SharedUI;

public static class Bindings
{
    public static bool GetVisibilityToEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(VisibilityToEnabledProperty);
    }

    public static void SetVisibilityToEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(VisibilityToEnabledProperty, value);
    }
    public static readonly DependencyProperty VisibilityToEnabledProperty =
        DependencyProperty.RegisterAttached("VisibilityToEnabled", typeof(bool), typeof(Bindings), new PropertyMetadata(false, OnVisibilityToEnabledChanged));

    private static void OnVisibilityToEnabledChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is FrameworkElement element)
        {
            if ((bool)args.NewValue)
            {
                element.bind(UIElement.VisibilityProperty, element, nameof(FrameworkElement.IsEnabled), new BoolToVisibilityConverter());
            }
            else
            {
                BindingOperations.ClearBinding(element, UIElement.VisibilityProperty);
            }
        }
    }
}