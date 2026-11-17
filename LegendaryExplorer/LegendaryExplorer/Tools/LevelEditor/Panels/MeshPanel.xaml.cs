using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class MeshPanel : UserControl
{
    public MeshPanel() => InitializeComponent();

    //the editor window does the work: it owns the selection, the asset picker and the undo history
    private LevelEditor Editor => (LevelEditor)Window.GetWindow(this);

    private void ChooseMesh_Click(object sender, RoutedEventArgs e) => Editor.ChooseMesh_Click(sender, e);
    private void RevertMesh_Click(object sender, RoutedEventArgs e) => Editor.RevertMesh_Click(sender, e);
    private void ChooseMaterial_Click(object sender, RoutedEventArgs e) => Editor.ChooseMaterial_Click(sender, e);
    private void ClearMaterial_Click(object sender, RoutedEventArgs e) => Editor.ClearMaterial_Click(sender, e);
    private void DeleteExtraMaterial_Click(object sender, RoutedEventArgs e) => Editor.DeleteExtraMaterial_Click(sender, e);
    private void RevertMaterials_Click(object sender, RoutedEventArgs e) => Editor.RevertMaterials_Click(sender, e);
}
