using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class DecalPanel : UserControl
{
    public DecalPanel() => InitializeComponent();

    //the editor window does the work: it owns the selection, the asset picker and the undo history
    private LevelEditor Editor => (LevelEditor)Window.GetWindow(this);

    private void DecalMaterial_Click(object sender, RoutedEventArgs e) => Editor.DecalMaterial_Click(sender, e);
    private void RefreshDecalFilter_Click(object sender, RoutedEventArgs e) => Editor.RefreshDecalFilter_Click(sender, e);
    private void ClearDecalFilter_Click(object sender, RoutedEventArgs e) => Editor.ClearDecalFilter_Click(sender, e);
}
