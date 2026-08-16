using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorer.Tools.LevelEditor;

namespace LegendaryExplorer.Tools.InterpEditor;

public partial class ActorPickerDialog : Window
{
    public ActorProxy SelectedActor { get; private set; }

    private readonly ICollectionView _actorsView;
    private string _searchText = "";

    public static ActorProxy PickActor(Window owner, LevelEditor.LevelEditor levelEditor)
    {
        var dlg = new ActorPickerDialog(levelEditor) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.SelectedActor : null;
    }

    private ActorPickerDialog(LevelEditor.LevelEditor levelEditor)
    {
        InitializeComponent();
        _actorsView = CollectionViewSource.GetDefaultView(levelEditor.Actors);
        _actorsView.Filter = FilterActor;
        ActorGrid.ItemsSource = _actorsView;
    }

    private bool FilterActor(object obj) =>
        obj is ActorProxy a && (
            string.IsNullOrEmpty(_searchText)
            || a.DisplayText.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || a.Export.ClassName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || a.OwningFileName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || a.Tag.Instanced.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        _actorsView.Refresh();
    }

    private void ActorGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OKButton.IsEnabled = ActorGrid.SelectedItem is ActorProxy;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        SelectedActor = ActorGrid.SelectedItem as ActorProxy;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ActorGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ActorGrid.SelectedItem is ActorProxy)
            OK_Click(sender, e);
    }
}
