using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.DialogueEditor;
using LegendaryExplorer.DialogueEditor.DialogueEditorExperiments;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.UserControls.DialogueEditorControls
{
    /// <summary>
    /// Class that holds toolset development experiments. Actual experiment code should be in the Experiments classes
    /// </summary>
    public partial class DialogueExperimentsMenuControl : MenuItem
    {
        public DialogueExperimentsMenuControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands() { }

    }
}
