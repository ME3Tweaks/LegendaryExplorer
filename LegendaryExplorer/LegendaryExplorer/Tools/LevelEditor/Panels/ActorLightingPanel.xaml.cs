using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class ActorLightingPanel : UserControl
{
    public ActorLightingPanel() => InitializeComponent();

    //the editor window does the work: it owns the selection, the asset picker and the undo history
    private LevelEditor Editor => (LevelEditor)Window.GetWindow(this);

    private void EditLightEnvironment_Click(object sender, RoutedEventArgs e) => Editor.EditLightEnvironment_Click(sender, e);
    private void CreateLightEnvironment_Click(object sender, RoutedEventArgs e) => Editor.CreateLightEnvironment_Click(sender, e);
}
