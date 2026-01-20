using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LegendaryExplorer.Tools.Dialogue_Editor.DialogueEditorExperiments
{
    /// <summary>
    /// Mgamerz Experiments in Dialogue Editor.
    /// </summary>
    static class DialogueEditorExperimentsM
    {
        public static void AddSpeakerWithSharedFXAToAllConvos(WPFBase window)
        {
            if (window.Pcc == null)
            {
                return;
            }

            var conversations = window.Pcc.Exports.Where(e => e.ClassName == "BioConversation").ToList();

            if (conversations.Count == 0)
            {
                MessageBox.Show("This file doesn't contain any converations.");
                return;
            }

            var newTag = PromptDialog.Prompt(window, "Enter the tag of the speaker you want to add.", "Enter speaker tag");
            if (newTag == null)
            {
                return; // Nothing
            }

            var fxaM = EntrySelector.GetEntry<ExportEntry>(window, window.Pcc, "Select the male FXA to assign to the new speaker.", e => e is ExportEntry && e.ClassName == "FaceFXAnimSet"); ;
            if (fxaM == null)
            {
                return;
            }
            var fxaF = EntrySelector.GetEntry<ExportEntry>(window, window.Pcc, "Select the female FXA to assign to the new speaker.", e => e is ExportEntry && e.ClassName == "FaceFXAnimSet"); ;
            if (fxaF == null)
            {
                return;
            }

            foreach(var convo in conversations)
            {
                var bioconvo = convo.GetProperties();
                var speakerList = bioconvo.GetProp<ArrayProperty<NameProperty>>("m_aSpeakerList");
                var fxaMs = bioconvo.GetProp<ArrayProperty<ObjectProperty>>("m_aMaleFaceSets");
                var fxaFs = bioconvo.GetProp<ArrayProperty<ObjectProperty>>("m_aFemaleFaceSets");

                speakerList.Add(new NameProperty(newTag));
                fxaMs.Add(new ObjectProperty(fxaM));
                fxaFs.Add(new ObjectProperty(fxaF));
                convo.WriteProperties(bioconvo);
            }
         
            MessageBox.Show("Done.");
        }
    }
}
