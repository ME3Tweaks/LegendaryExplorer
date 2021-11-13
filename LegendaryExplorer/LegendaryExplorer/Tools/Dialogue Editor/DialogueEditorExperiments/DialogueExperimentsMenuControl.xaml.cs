using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.DialogueEditor;
using LegendaryExplorer.DialogueEditor.DialogueEditorExperiments;

namespace LegendaryExplorer.Tools.Dialogue_Editor.DialogueEditorExperiments
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
        
        public DialogueEditorWindow GetDEWindow()
        {
            if (Window.GetWindow(this) is DialogueEditorWindow dew)
            {
                return dew;
            }

            return null;
        }

        // EXPERIMENTS: EXKYWOR------------------------------------------------------------
        #region Exkywor's experiments
        private void UpdateNativeNodeStringRef_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.UpdateNativeNodeStringRef(GetDEWindow());
        }

        #endregion

    }
}
