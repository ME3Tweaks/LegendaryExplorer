using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class SplinePanel : UserControl
{
    public SplinePanel() => InitializeComponent();

    //the editor window does the work: it owns the selection, the asset picker and the undo history
    private LevelEditor Editor => (LevelEditor)Window.GetWindow(this);

    private void SplineConnection_Click(object sender, RoutedEventArgs e) => Editor.SplineConnection_Click(sender, e);
    private void SplineBreakAll_Click(object sender, RoutedEventArgs e) => Editor.SplineBreakAll_Click(sender, e);
    private void SplineReverseAll_Click(object sender, RoutedEventArgs e) => Editor.SplineReverseAll_Click(sender, e);
    private void SplineMesh_Click(object sender, RoutedEventArgs e) => Editor.SplineMesh_Click(sender, e);
    private void SplineClearMesh_Click(object sender, RoutedEventArgs e) => Editor.SplineClearMesh_Click(sender, e);
    private void SplineMaterialSlot_Click(object sender, RoutedEventArgs e) => Editor.SplineMaterialSlot_Click(sender, e);
    private void SplineMaterials_Click(object sender, RoutedEventArgs e) => Editor.SplineMaterials_Click(sender, e);
    private void SplineClearMaterials_Click(object sender, RoutedEventArgs e) => Editor.SplineClearMaterials_Click(sender, e);
}
