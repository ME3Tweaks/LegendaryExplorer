using LegendaryExplorer.Misc;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LegendaryExplorer.Tools.LevelEditor;

public sealed class OutlinerGroupState : NotifyPropertyChangedBase
{
    public OutlinerCategoryState Category { get; init; }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

public sealed class OutlinerCategoryState(string name) : NotifyPropertyChangedBase
{
    public string Name { get; } = name;

    private bool _isVisible = name is not ("Volumes" or "Volumetric meshes");
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}

public sealed class OutlinerGroupStateConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values is [CollectionViewGroup group, LevelEditor editor]
            ? editor.GetOutlinerGroupState(group)
            : DependencyProperty.UnsetValue;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
