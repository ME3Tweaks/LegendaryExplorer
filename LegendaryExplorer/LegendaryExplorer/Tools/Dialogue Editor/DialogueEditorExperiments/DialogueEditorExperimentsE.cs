using System.Collections.Generic;
using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.DialogueEditor.DialogueEditorExperiments
{
    /// <summary>
    /// Class for Exkywor's preset buttons and stuff
    /// </summary>
    class DialogueEditorExperimentsE
    {
        #region Update Native Node String Ref
        // Changes the node's lineref and the parts of the FXA, WwiseStream, and referencing VOs that include it so it doesn't break
        public static void UpdateNativeNodeStringRef(DialogueEditorWindow dew)
        {
            DialogueNodeExtended selectedDialogueNode = dew.SelectedDialogueNode;

            if (dew.Pcc != null && selectedDialogueNode != null)
            {
                // Need to check if currStringRef exists
                var currStringRef = selectedDialogueNode.LineStrRef.ToString();
                if (string.IsNullOrEmpty(currStringRef))
                {
                    MessageBox.Show("The selected node does not have a Line String Ref, which is required in order to programatically replace the required elements.", "Warning", MessageBoxButton.OK);
                    return;
                }

                var newStringRef = promptForRef("New line string ref:", "Not a valid line string ref.");
                if (string.IsNullOrEmpty(newStringRef))
                {
                    return;
                }

                if (currStringRef == newStringRef)
                {
                    MessageBox.Show("New StringRef matches the existing one.", "Warning", MessageBoxButton.OK);
                    return;
                }

                updateFaceFX(dew.FaceFXAnimSetEditorControl_M, currStringRef, newStringRef);
                updateFaceFX(dew.FaceFXAnimSetEditorControl_F, currStringRef, newStringRef);

                updateWwiseStream(selectedDialogueNode.WwiseStream_Male, currStringRef, newStringRef);
                updateWwiseStream(selectedDialogueNode.WwiseStream_Female, currStringRef, newStringRef);

                var pcc = dew.Pcc;
                updateVOReferences(pcc, selectedDialogueNode.WwiseStream_Male, currStringRef, newStringRef);
                updateVOReferences(pcc, selectedDialogueNode.WwiseStream_Female, currStringRef, newStringRef);

                int intRef;
                int.TryParse(newStringRef, out intRef);
                selectedDialogueNode.LineStrRef = intRef;

                MessageBox.Show($"The node now points to {newStringRef}.", "Success", MessageBoxButton.OK);
            }
        }

        private static void updateVOReferences(IMEPackage pcc, ExportEntry wwiseStream, string oldRef, string newRef)
        {
            if (wwiseStream == null)
            {
                return;
            }

            var entry = pcc.GetEntry(wwiseStream.UIndex);

            var references = entry.GetEntriesThatReferenceThisOne();
            foreach (KeyValuePair<IEntry, List<string>> reference in references)
            {
                if (reference.Key.ClassName != "WwiseEvent")
                {
                    continue;
                }
                ExportEntry refEntry = (ExportEntry) pcc.GetEntry(reference.Key.UIndex);
                refEntry.ObjectNameString = refEntry.ObjectNameString.Replace(oldRef, newRef);
            }

        }

        private static void updateWwiseStream(ExportEntry wwiseStream, string oldRef, string newRef)
        {
            if (wwiseStream is null)
            {
               return;
            }
            
            // Pads the string refs so they have the required minimum length
            newRef = newRef.PadLeft(8, '0');
            oldRef = oldRef.PadLeft(8, '0');

            wwiseStream.ObjectNameString = wwiseStream.ObjectNameString.Replace(oldRef, newRef);
        }

        private static void updateFaceFX(FaceFXAnimSetEditorControl fxa, string oldRef, string newRef)
        {
            if (fxa.SelectedLine == null || fxa == null)
            {
                return;
            }

            var FaceFX = fxa.FaceFX;
            var SelectedLine = fxa.SelectedLine;
            
            if (SelectedLine.Path != null)
            {
                SelectedLine.Path = SelectedLine.Path.Replace(oldRef, newRef);
            }
            if (SelectedLine.ID != null)
            {
                SelectedLine.ID = newRef;
            }
            // Change FaceFX name
            if (SelectedLine.NameAsString != null)
            {
                string newName = SelectedLine.NameAsString.Replace(oldRef, newRef);
                if (FaceFX.Names.Contains(newName))
                {
                    SelectedLine.NameIndex = FaceFX.Names.IndexOf(newName);
                    SelectedLine.NameAsString = newName;
                }
                else 
                {
                    FaceFX.Names.Add(newName);
                    SelectedLine.NameIndex = FaceFX.Names.Count - 1;
                    SelectedLine.NameAsString = newName;
                }
            }

            fxa.SaveChanges();
        }

        private static string promptForRef(string msg, string err)
        {
            if (PromptDialog.Prompt(null, msg) is string stringRef)
            {
                int intRef;
                if (string.IsNullOrEmpty(stringRef) || !int.TryParse(stringRef, out intRef))
                {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return null;
                }
                return intRef.ToString();
            }
            return null;
        }
        #endregion

        #region Clone Node And Sequence
        /// <summary>
        /// Clones a Dialogue Node and its related Sequence, while giving it a unique id.
        /// </summary>
        /// <param name="dew">Dialogue Editor Window instance.</param>
        public static void CloneNodeAndSequence(DialogueEditorWindow dew) {
            MessageBox.Show("Hello", "test", MessageBoxButton.OK);
        }
        #endregion
    }
}
