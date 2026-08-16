using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.Dialogs;

public class CheckedListItem
{
    public string DisplayName { get; set; }
    public bool IsSelected { get; set; }
    public object Tag { get; set; }
}

public partial class CheckedListDialog : TrackingNotifyPropertyChangedWindowBase
{
    public ObservableCollectionExtended<CheckedListItem> Items { get; } = [];

    private string topText;
    public string TopText
    {
        get => topText;
        set => SetProperty(ref topText, value);
    }

    public CheckedListDialog(IEnumerable<CheckedListItem> items, string title, string message, Window owner)
        : base("Checked List Dialog", false)
    {
        DataContext = this;
        TopText = message;
        Items.AddRange(items);
        InitializeComponent();
        Title = title;
        Owner = owner;
    }

    public List<CheckedListItem> GetSelectedItems() => Items.Where(i => i.IsSelected).ToList();

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items) item.IsSelected = true;
        CheckList.ItemsSource = null;
        CheckList.ItemsSource = Items;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items) item.IsSelected = false;
        CheckList.ItemsSource = null;
        CheckList.ItemsSource = Items;
    }
}
