using LegendaryExplorer.Dialogs;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LegendaryExplorer.DialogueEditor.DialogueEditorExperiments {
    /// <summary>
    /// Class for Exkywor's preset buttons and stuff
    /// </summary>
    class DialogueEditorExperimentsE {
        #region Update Native Node String Ref
        // Changes the node's lineref and the parts of the FXA, WwiseStream, and referencing VOs that include it so it doesn't break
        public static void UpdateNativeNodeStringRef(DialogueEditorWindow dew) {
            DialogueNodeExtended selectedDialogueNode = dew.SelectedDialogueNode;

            if (dew.Pcc != null && selectedDialogueNode != null) {
                // Need to check if currStringRef exists
                var currStringRef = selectedDialogueNode.LineStrRef.ToString();
                if (string.IsNullOrEmpty(currStringRef)) {
                    MessageBox.Show("The selected node does not have a Line String Ref, which is required in order to programatically replace the required elements.", "Warning", MessageBoxButton.OK);
                    return;
                }

                var newStringRef = promptForRef("New line string ref:", "Not a valid line string ref.");
                if (string.IsNullOrEmpty(newStringRef)) {
                    return;
                }

                if (currStringRef == newStringRef) {
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

        private static void updateVOReferences(IMEPackage pcc, ExportEntry wwiseStream, string oldRef, string newRef) {
            if (wwiseStream == null) {
                return;
            }

            var entry = pcc.GetEntry(wwiseStream.UIndex);

            var references = entry.GetEntriesThatReferenceThisOne();
            foreach (KeyValuePair<IEntry, List<string>> reference in references) {
                if (reference.Key.ClassName != "WwiseEvent") {
                    continue;
                }
                ExportEntry refEntry = (ExportEntry)pcc.GetEntry(reference.Key.UIndex);
                refEntry.ObjectNameString = refEntry.ObjectNameString.Replace(oldRef, newRef);
            }

        }

        private static void updateWwiseStream(ExportEntry wwiseStream, string oldRef, string newRef) {
            if (wwiseStream is null) {
                return;
            }

            // Pads the string refs so they have the required minimum length
            newRef = newRef.PadLeft(8, '0');
            oldRef = oldRef.PadLeft(8, '0');

            wwiseStream.ObjectNameString = wwiseStream.ObjectNameString.Replace(oldRef, newRef);
        }

        private static void updateFaceFX(FaceFXAnimSetEditorControl fxa, string oldRef, string newRef) {
            if (fxa.SelectedLine == null || fxa == null) {
                return;
            }

            var FaceFX = fxa.FaceFX;
            var SelectedLine = fxa.SelectedLine;

            if (SelectedLine.Path != null) {
                SelectedLine.Path = SelectedLine.Path.Replace(oldRef, newRef);
            }
            if (SelectedLine.ID != null) {
                SelectedLine.ID = newRef;
            }
            // Change FaceFX name
            if (SelectedLine.NameAsString != null) {
                string newName = SelectedLine.NameAsString.Replace(oldRef, newRef);
                if (FaceFX.Names.Contains(newName)) {
                    SelectedLine.NameIndex = FaceFX.Names.IndexOf(newName);
                    SelectedLine.NameAsString = newName;
                } else {
                    FaceFX.Names.Add(newName);
                    SelectedLine.NameIndex = FaceFX.Names.Count - 1;
                    SelectedLine.NameAsString = newName;
                }
            }

            fxa.SaveChanges();
        }

        private static string promptForRef(string msg, string err) {
            if (PromptDialog.Prompt(null, msg) is string stringRef) {
                int intRef;
                if (string.IsNullOrEmpty(stringRef) || !int.TryParse(stringRef, out intRef)) {
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
            DialogueNodeExtended selectedDialogueNode = dew.SelectedDialogueNode;

            if (dew.Pcc != null && selectedDialogueNode != null) {
                // Need to check if the node has associated data
                if (selectedDialogueNode.Interpdata == null) {
                    MessageBox.Show("The selected node does not have an InterpData associated with it.", "Warning", MessageBoxButton.OK);
                    return;
                }

                int newID = promptForID("New node ExportID:", "Not a valid ExportID.");
                if (newID == 0) { return; }

                if (selectedDialogueNode.ExportID.Equals(newID)) {
                    MessageBox.Show("New ExportID matches the existing one.", "Warning", MessageBoxButton.OK);
                    return;
                }

                ExportEntry oldInterpData = selectedDialogueNode.Interpdata;

                // Get the Interp linked to the InterpData
                IEnumerable<KeyValuePair<IEntry, List<string>>> interpDataReferences = oldInterpData.GetEntriesThatReferenceThisOne()
                    .Where(entry => entry.Key.ClassName == "SeqAct_Interp");
                if (interpDataReferences.Count() > 1) {
                    MessageBox.Show("The selected Node's InterpData is references by more than one Interp. Please ensure it's only references by one.", "Warning", MessageBoxButton.OK);
                }
                ExportEntry oldInterp = (ExportEntry)interpDataReferences.First().Key;

                // Get the/a ConvNode linked to the Interp
                ExportEntry oldConvNode = SeqTools.FindOutboundConnectionsToNode(oldInterp, SeqTools.GetAllSequenceElements(oldInterp).OfType<ExportEntry>())
                    .Where(entry => entry.ClassName == "BioSeqEvt_ConvNode").ToList().First();

                // Get the/a EndCurrentConvNode that the Interp outputs to
                ExportEntry oldEndNode = SeqTools.GetOutboundLinksOfNode(oldInterp).Select(outboundLink => {
                    IEnumerable<SeqTools.OutboundLink> links = outboundLink.Where(link => link.LinkedOp.ClassName == "BioSeqAct_EndCurrentConvNode");
                    if (links.Any()) { return (ExportEntry)links.First().LinkedOp; }
                    else { return null; }
                }).ToList().First();

                ExportEntry sequence = SeqTools.GetParentSequence(oldInterpData);

                // Clone the sequence objects
                ExportEntry newInterp = cloneObject(oldInterp, sequence);
                ExportEntry newInterpData = EntryCloner.CloneTree(oldInterpData);
                KismetHelper.AddObjectToSequence(newInterpData, sequence, true);
                ExportEntry newConvNode = cloneObject(oldConvNode, sequence);
                ExportEntry newEndNode = cloneObject(oldEndNode, sequence);

                // Link the new objects
                KismetHelper.CreateOutputLink(newConvNode, "Started", newInterp, 0);
                KismetHelper.CreateOutputLink(newInterp, "Completed", newEndNode, 0);
                KismetHelper.CreateOutputLink(newInterp, "Reversed", newEndNode, 0);

                // Save existing varLinks, minus the Data one
                List<SeqTools.VarLinkInfo> varLinks = SeqTools.GetVariableLinksOfNode(oldInterp);
                foreach (SeqTools.VarLinkInfo link in varLinks) {
                    if (link.LinkDesc == "Data") { link.LinkedNodes = new(); }
                }
                SeqTools.WriteVariableLinksToNode(newInterp, varLinks);
                KismetHelper.CreateVariableLink(newInterp, "Data", newInterpData);

                // Write the new nodeID
                IntProperty m_nNodeID = new(newID, "m_nNodeID");
                newConvNode.WriteProperty(m_nNodeID);

                // Clone and select the cloned node
                dew.NodeAddCommand.Execute(selectedDialogueNode.IsReply ? "CloneReply" : "CloneEntry");
                int index = selectedDialogueNode.IsReply ? dew.SelectedConv.ReplyList.Count : dew.SelectedConv.EntryList.Count;
                DialogueNodeExtended node = selectedDialogueNode.IsReply ? dew.SelectedConv.ReplyList[index - 1] : dew.SelectedConv.EntryList[index - 1];

                // Set the ExportID
                StructProperty prop = node.NodeProp;
                var nExportID = new IntProperty(newID, "nExportID");
                prop.Properties.AddOrReplaceProp(nExportID);
                dew.RecreateNodesToProperties(dew.SelectedConv);
                dew.ForceRefreshCommand.Execute(null);

                MessageBox.Show($"Node cloned and given the ExportID: {newID}.", "Success", MessageBoxButton.OK);
            }
        }

        private static int promptForID(string msg, string err) {
            if (PromptDialog.Prompt(null, msg) is string strID) {
                int ID;
                if (!int.TryParse(strID, out ID)) {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return 0;
                }
                return ID;
            }
            return 0;
        }

        // From SequenceEditorWPF.xaml.cs
        private static ExportEntry cloneObject(ExportEntry old, ExportEntry sequence, bool topLevel = true, bool incrementIndex = true)
        {
            //SeqVar_External needs to have the same index to work properly
            ExportEntry exp = EntryCloner.CloneEntry(old, incrementIndex: incrementIndex && old.ClassName != "SeqVar_External");

            KismetHelper.AddObjectToSequence(exp, sequence, topLevel);
            // cloneSequence(exp);
            return exp;
        }

        #endregion
    }
}
