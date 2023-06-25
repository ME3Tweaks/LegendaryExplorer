using System;
using System.Windows.Controls;
using System.Windows;

namespace LegendaryExplorer.SharedUI;

//from https://stackoverflow.com/a/39438710
public static class Filter
{
    public static readonly DependencyProperty ByProperty = DependencyProperty.RegisterAttached(
        "By",
        typeof(Predicate<object>),
        typeof(Filter),
        new PropertyMetadata(default(Predicate<object>), OnByChanged));

    public static void SetBy(ItemsControl element, Predicate<object> value)
    {
        element.SetValue(ByProperty, value);
    }

    public static Predicate<object> GetBy(ItemsControl element)
    {
        return (Predicate<object>)element.GetValue(ByProperty);
    }

    private static void OnByChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ItemsControl itemsControl)
        {
            if (itemsControl.Items.CanFilter)
            {
                itemsControl.Items.Filter = (Predicate<object>)e.NewValue;
            }
            else
            {
                itemsControl.Items.Filter = null;
            }
        }
    }
}