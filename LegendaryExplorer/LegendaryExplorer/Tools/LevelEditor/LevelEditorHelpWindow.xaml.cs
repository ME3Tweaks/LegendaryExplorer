using System.Windows;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>The Level Editor's introduction for new users</summary>
public partial class LevelEditorHelpWindow : Window
{
    public LevelEditorHelpWindow()
    {
        InitializeComponent();
        EditorTitleBar.Apply(this);
    }
}
