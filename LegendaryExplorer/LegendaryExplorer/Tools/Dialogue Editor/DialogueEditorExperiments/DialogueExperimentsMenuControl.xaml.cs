using LegendaryExplorer.DialogueEditor;
using LegendaryExplorer.DialogueEditor.DialogueEditorExperiments;
using System.Windows;
using System.Windows.Controls;

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
        private void UpdateAudioNodeStrRef_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.UpdateAudioNodeStrRef(GetDEWindow());
        }

        private void CloneNodeAndSequence_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.CloneNodeAndSequence(GetDEWindow());
        }

        private void LinkNodesFree_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.LinkNodesFree(GetDEWindow());
        }

        private void LinkNodesStrRef_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.LinkNodesStrRef(GetDEWindow());
        }

        private void BatchCreateNodesSequence_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.BatchCreateNodesSequenceExperiment(GetDEWindow());
        }

        private void CreateNodeSequence_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.CreateNodeSequenceExperiment(GetDEWindow());
        }

        private void BatchUpdateVOsAndComments_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.BatchUpdateVOsAndCommentsExperiment(GetDEWindow());
        }
        
        private void UpdateVOAndComment_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.UpdateVOAndCommentExperiment(GetDEWindow());
        }

        private void BatchAddConversationDefaults_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.BatchAddConversationDefaultsExperiment(GetDEWindow());
        }

        private void AddConversationDefaults_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.AddConversationDefaultsExperiment(GetDEWindow());
        }

        private void BatchUpdateInterpLengths_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.BatchUpdateInterpLengthsExperiment(GetDEWindow());
        }

        private void UpdateInterpLength_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.UpdateInterpLengthExperiment(GetDEWindow());
        }

        private void BatchGenerateLE1AudioLinks_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.BatchGenerateLE1AudioLinksExperiment(GetDEWindow());
        }

        private void GenerateLE1AudioLinks_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.GenerateLE1AudioLinksExperiment(GetDEWindow());
        }

        private void FixAutocontinues_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.FixAutocontinues(GetDEWindow());
        }

        private void BatchUnlistTrackFromGroup_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.BatchUnlistTrackFromGroupExperiment(GetDEWindow());
        }
        
        private void UnlistTrackFromGroup_Click(object sender, RoutedEventArgs e)
        {
            DialogueEditorExperimentsE.UnlistTrackFromGroupExperiment(GetDEWindow());
        }
        #endregion
    }
}
