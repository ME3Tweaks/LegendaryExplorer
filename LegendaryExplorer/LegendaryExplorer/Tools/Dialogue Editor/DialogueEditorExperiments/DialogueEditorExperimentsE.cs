using LegendaryExplorer.Dialogs;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LegendaryExplorer.DialogueEditor.DialogueEditorExperiments
{
    /// <summary>
    /// Class for Exkywor's preset buttons and stuff
    /// </summary>
    class DialogueEditorExperimentsE
    {
        #region Update Native Node String Ref
        /// <summary>
        /// Change the node's lineref and the parts of the FXA, WwiseStream, and WwwiseEvents
        /// that include it so it doesn't break
        /// </summary>
        /// <param name="dew">Current DE window.</param>
        public static void UpdateNativeNodeStringRef(DialogueEditorWindow dew)
        {
            DialogueNodeExtended selectedDialogueNode = dew.SelectedDialogueNode;

            if (dew.Pcc == null || selectedDialogueNode == null) { return; }

            if (dew.Pcc.Game.IsGame1())
            {
                MessageBox.Show("Not available for Mass Effect 1.", "Warning", MessageBoxButton.OK);
                return;
            }

            // Need to check if currStringRef exists
            string currStringRef = selectedDialogueNode.LineStrRef.ToString();
            if (string.IsNullOrEmpty(currStringRef))
            {
                MessageBox.Show("The selected node does not have a Line String Ref, which is required in order to programatically replace the required elements.", "Warning", MessageBoxButton.OK);
                return;
            }

            string newStringRef = promptForRef("New line string ref:", "Not a valid line string ref.");
            if (string.IsNullOrEmpty(newStringRef))
            {
                return;
            }

            if (currStringRef == newStringRef)
            {
                MessageBox.Show("New StringRef matches the existing one.", "Warning", MessageBoxButton.OK);
                return;
            }

            IMEPackage pcc = dew.Pcc;
            FaceFXAnimSetEditorControl femaleFXA = dew.FaceFXAnimSetEditorControl_F;
            FaceFXAnimSetEditorControl maleFXA = dew.FaceFXAnimSetEditorControl_M;

            ExportEntry femaleEvent = GetWwiseEvent(pcc, femaleFXA);
            ExportEntry maleEvent = GetWwiseEvent(pcc, maleFXA);
            ExportEntry femaleStream = GetWwiseStream(pcc, femaleEvent, currStringRef, "_f_");
            ExportEntry maleStream = GetWwiseStream(pcc, maleEvent, currStringRef, "_m_");

            UpdateWwiseEvent(pcc, femaleEvent, currStringRef, newStringRef);
            UpdateWwiseEvent(pcc, maleEvent, currStringRef, newStringRef);

            UpdateWwiseStream(femaleStream, currStringRef, newStringRef);
            UpdateWwiseStream(maleStream, currStringRef, newStringRef);

            UpdateFaceFX(femaleFXA, currStringRef, newStringRef);
            UpdateFaceFX(maleFXA, currStringRef, newStringRef);

            if (int.TryParse(newStringRef, out int intRef))
            {
                selectedDialogueNode.LineStrRef = intRef;
            }

            MessageBox.Show($"The node now points to {newStringRef}.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Update the name of the WwiseEvents referencing an input WwiseStream.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="wwiseEvent">WwiseStream to use to find the WwiseEvents.</param>
        /// <param name="oldRef">Part of the name to replace.</param>
        /// <param name="newRef">String to replace with.</param>
        private static void UpdateWwiseEvent(IMEPackage pcc, ExportEntry wwiseEvent, string oldRef, string newRef)
        {
            if (wwiseEvent == null) { return; }

            wwiseEvent.ObjectNameString = wwiseEvent.ObjectNameString.Replace(oldRef, newRef);
        }

        /// <summary>
        /// Update the name of the given WwiseStream.
        /// </summary>
        /// <param name="wwiseStream">WwiseStream to update.</param>
        /// <param name="wwiseEvent">WwiseStream to use to find the WwiseStreams.</param>
        /// <param name="oldRef">Part of the name to replace.</param>
        /// <param name="newRef">String to replace with.</param>
        private static void UpdateWwiseStream(ExportEntry wwiseStream, string oldRef, string newRef)
        {
            if (wwiseStream == null) { return; }

            // Pads the string refs so they have the required minimum length
            newRef = newRef.PadLeft(8, '0');
            oldRef = oldRef.PadLeft(8, '0');

            wwiseStream.ObjectNameString = wwiseStream.ObjectNameString.Replace(oldRef, newRef);
        }

        /// <summary>
        /// Update the path, ID, and name of a given FXA.
        /// </summary>
        /// <param name="fxa">FXA to update.</param>
        /// <param name="oldRef">Part of the name to replace.</param>
        /// <param name="newRef">String to replace with.</param>
        private static void UpdateFaceFX(FaceFXAnimSetEditorControl fxa, string oldRef, string newRef)
        {
            if (fxa.SelectedLine == null || fxa == null) { return; }

            FaceFXAnimSetEditorControl.IFaceFXBinary FaceFX = fxa.FaceFX;
            FaceFXLine SelectedLine = fxa.SelectedLine;

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

        /// <summary>
        /// Get the WwiseEvent listed in the Path of a given FXA, if it exists.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="fxa">FXA to get the path from.</param>
        /// <returns>WwiseEvent if found, null otherwise.</returns>
        private static ExportEntry GetWwiseEvent(IMEPackage pcc, FaceFXAnimSetEditorControl fxa)
        {
            if (pcc == null || fxa.SelectedLine == null || fxa == null) { return null; }

            FaceFXLine SelectedLine = fxa.SelectedLine;

            if (SelectedLine.Path == null) { return null; }

            return pcc.FindExport(SelectedLine.Path);
        }

        /// <summary>
        /// Get the WwiseStream referenced in the WwiseEvent that contains the stringRef and the suffic.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="wwiseEvent">WwiseEvent that references ther WwiseStream.</param>
        /// <param name="stringRef">StringRef to search for.</param>
        /// <param name="suffix">_m_ or _f_.</param>
        /// <returns>WwiseStream that matches the criteria, if any.</returns>
        private static ExportEntry GetWwiseStream(IMEPackage pcc, ExportEntry wwiseEvent, string stringRef, string suffix)
        {
            if (pcc == null || (pcc.Game is MEGame.ME1) || wwiseEvent == null) { return null; }

            if (!pcc.Game.IsGame1())
            {
                if (pcc.Game is MEGame.LE2)
                {
                    ArrayProperty<StructProperty> references = wwiseEvent.GetProperty<ArrayProperty<StructProperty>>("References");
                    if ((references == null) || (references.Count == 0)) { return null; }
                    StructProperty relationships = references[0].GetProp<StructProperty>("Relationships");
                    if (relationships == null) { return null; }
                    ArrayProperty<ObjectProperty> streams = relationships.GetProp<ArrayProperty<ObjectProperty>>("Streams");
                    if ((streams == null) || (streams.Count == 0)) { return null; }

                    foreach (ObjectProperty streamRef in streams)
                    {
                        if (streamRef.Value == 0) { continue; }

                        ExportEntry wwiseStream = pcc.GetUExport(streamRef.Value);
                        if (wwiseStream == null) { continue; }

                        // Check that it contains the gender and the ref
                        if (wwiseStream.ObjectName.Name.Contains(stringRef, System.StringComparison.OrdinalIgnoreCase)
                            && wwiseStream.ObjectName.Name.Contains(suffix, System.StringComparison.OrdinalIgnoreCase))
                        {
                            return wwiseStream;
                        }
                    }
                }
                else
                {
                    WwiseEvent wwiseEventBin = wwiseEvent.GetBinaryData<WwiseEvent>();
                    foreach (WwiseEvent.WwiseEventLink link in wwiseEventBin.Links)
                    {
                        foreach (int stream in link.WwiseStreams)
                        {
                            if (stream == 0) { continue; }

                            ExportEntry wwiseStream = pcc.GetUExport(stream);
                            if (wwiseStream == null) { continue; }

                            // Check that it contains the gender and the ref
                            if (wwiseStream.ObjectName.Name.Contains(stringRef, System.StringComparison.OrdinalIgnoreCase)
                                && wwiseStream.ObjectName.Name.Contains(suffix, System.StringComparison.OrdinalIgnoreCase))
                            {
                                return wwiseStream;
                            }
                        }
                    }
                }
            }

            return null;
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
        public static void CloneNodeAndSequence(DialogueEditorWindow dew)
        {
            DialogueNodeExtended selectedDialogueNode = dew.SelectedDialogueNode;

            if (dew.Pcc != null && selectedDialogueNode != null)
            {
                // Need to check if the node has associated data
                if (selectedDialogueNode.Interpdata == null)
                {
                    MessageBox.Show("The selected node does not have an InterpData associated with it.", "Warning", MessageBoxButton.OK);
                    return;
                }

                int newID = promptForID("New node ExportID:", "Not a valid ExportID.");
                if (newID == 0) { return; }

                if (selectedDialogueNode.ExportID.Equals(newID))
                {
                    MessageBox.Show("New ExportID matches the existing one.", "Warning", MessageBoxButton.OK);
                    return;
                }

                ExportEntry oldInterpData = selectedDialogueNode.Interpdata;

                // Get the Interp linked to the InterpData
                IEnumerable<KeyValuePair<IEntry, List<string>>> interpDataReferences = oldInterpData.GetEntriesThatReferenceThisOne()
                    .Where(entry => entry.Key.ClassName == "SeqAct_Interp");
                if (interpDataReferences.Count() > 1)
                {
                    MessageBox.Show("The selected Node's InterpData is linked to Interps. Please ensure it's only linked to one.", "Warning", MessageBoxButton.OK);
                }
                ExportEntry oldInterp = (ExportEntry)interpDataReferences.First().Key;

                // Get the/a ConvNode linked to the Interp
                ExportEntry oldConvNode = SeqTools.FindOutboundConnectionsToNode(oldInterp, SeqTools.GetAllSequenceElements(oldInterp).OfType<ExportEntry>())
                    .FirstOrDefault(entry => entry.ClassName == "BioSeqEvt_ConvNode");

                // Get the/a EndCurrentConvNode that the Interp outputs to
                ExportEntry oldEndNode = SeqTools.GetOutboundLinksOfNode(oldInterp).Select(outboundLink =>
                {
                    IEnumerable<SeqTools.OutboundLink> links = outboundLink.Where(link => link.LinkedOp.ClassName == "BioSeqAct_EndCurrentConvNode");
                    if (links.Any()) { return (ExportEntry)links.First().LinkedOp; } else { return null; }
                }).ToList().FirstOrDefault();

                ExportEntry sequence = SeqTools.GetParentSequence(oldInterpData);

                // Clone the Intero and Interpdata objects
                ExportEntry newInterp = cloneObject(oldInterp, sequence);
                ExportEntry newInterpData = EntryCloner.CloneTree(oldInterpData);
                KismetHelper.AddObjectToSequence(newInterpData, sequence, true);

                // Clone and link the Conv and End objects, if they exist
                ExportEntry newConvNode = null;
                if (oldConvNode != null)
                {
                    newConvNode = cloneObject(oldConvNode, sequence);
                    KismetHelper.CreateOutputLink(newConvNode, "Started", newInterp, 0);
                }

                if (oldEndNode != null)
                {
                    ExportEntry newEndNode = cloneObject(oldEndNode, sequence);
                    KismetHelper.CreateOutputLink(newInterp, "Completed", newEndNode, 0);
                    KismetHelper.CreateOutputLink(newInterp, "Reversed", newEndNode, 0);
                }

                // Save existing varLinks, minus the Data one
                List<SeqTools.VarLinkInfo> varLinks = SeqTools.GetVariableLinksOfNode(oldInterp);
                foreach (SeqTools.VarLinkInfo link in varLinks)
                {
                    if (link.LinkDesc == "Data") { link.LinkedNodes = new(); }
                }
                SeqTools.WriteVariableLinksToNode(newInterp, varLinks);
                KismetHelper.CreateVariableLink(newInterp, "Data", newInterpData);

                // Write the new nodeID
                if (newConvNode != null)
                {
                    IntProperty m_nNodeID = new(newID, "m_nNodeID");
                    newConvNode.WriteProperty(m_nNodeID);
                }

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

        private static int promptForID(string msg, string err)
        {
            if (PromptDialog.Prompt(null, msg) is string strID)
            {
                int ID;
                if (!int.TryParse(strID, out ID))
                {
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
