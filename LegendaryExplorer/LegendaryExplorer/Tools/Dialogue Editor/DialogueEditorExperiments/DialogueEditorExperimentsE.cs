using LegendaryExplorer.Dialogs;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using static LegendaryExplorer.Misc.ExperimentsTools.SharedMethods;
using static LegendaryExplorer.Misc.ExperimentsTools.DialogueAutomations;
using static LegendaryExplorer.Misc.ExperimentsTools.SequenceAutomations;

namespace LegendaryExplorer.DialogueEditor.DialogueEditorExperiments
{
    /// <summary>
    /// Class for Exkywor's preset buttons and stuff
    /// </summary>
    static class DialogueEditorExperimentsE
    {
        #region Update Native Node String Ref
        /// <summary>
        /// Change the node's LineRef and the references to it in the FXA and audio elements.
        /// that include it so it doesn't break
        /// </summary>
        /// <param name="dew">Current DE window.</param>
        public static void UpdateAudioNodeStrRef(DialogueEditorWindow dew)
        {
            DialogueNodeExtended node = dew.SelectedDialogueNode;

            if (dew.Pcc == null || node == null) { return; }

            // Need to check if currStringRef exists
            string currStringRef = node.LineStrRef.ToString();
            if (string.IsNullOrEmpty(currStringRef))
            {
                MessageBox.Show("The selected node does not have a Line String Ref, which is required in order to programatically replace the required elements.", "Warning", MessageBoxButton.OK);
                return;
            }

            string newStringRef = PromptForRef("New line string ref:", "Not a valid line string ref.");
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

            FaceFXAnimSetEditorControl FXAControl_F = dew.FaceFXAnimSetEditorControl_F;
            FaceFXAnimSetEditorControl FXAControl_M = dew.FaceFXAnimSetEditorControl_M;

            if (pcc.Game.IsGame1())
            {
                // Remove the _F from the femaleFXA name, as it appears without it for female FXA lines.
                string femaleFXAName = node.FaceFX_Female[..^2];

                // Manually find the selected female line, as LEX doesn't load it into the SelectedLine of the
                // FaceFXAnimSetEditorControl_F for ME1.
                FaceFXLineEntry femaleLineEntry = GetSelectedLineEntry(femaleFXAName, FXAControl_F.Lines);

                ExportEntry femaleCue = GetSoundCue(pcc, femaleLineEntry.Line);
                ExportEntry maleCue = GetSoundCue(pcc, FXAControl_M.SelectedLine);
                ExportEntry femaleNodeWave = GetSoundNodeWave(pcc, femaleLineEntry);
                ExportEntry maleNodeWave = GetSoundNodeWave(pcc, FXAControl_M.SelectedLineEntry);

                UpdateME1SoundExport(femaleCue, currStringRef, newStringRef);
                UpdateME1SoundExport(maleCue, currStringRef, newStringRef);
                UpdateME1SoundExport(femaleNodeWave, currStringRef, newStringRef);
                UpdateME1SoundExport(maleNodeWave, currStringRef, newStringRef);

                UpdateFaceFX(FXAControl_F, currStringRef, newStringRef, femaleLineEntry.Line);
                UpdateFaceFX(FXAControl_M, currStringRef, newStringRef);
            }
            else
            {
                ExportEntry femaleEvent = GetWwiseEvent(pcc, FXAControl_F.SelectedLine);
                ExportEntry maleEvent = GetWwiseEvent(pcc, FXAControl_M.SelectedLine);
                ExportEntry femaleStream = GetWwiseStream(pcc, femaleEvent, currStringRef, "_f_");
                ExportEntry maleStream = GetWwiseStream(pcc, maleEvent, currStringRef, "_m_");

                UpdateWwiseEvent(pcc, femaleEvent, currStringRef, newStringRef);
                UpdateWwiseEvent(pcc, maleEvent, currStringRef, newStringRef);

                UpdateWwiseStream(femaleStream, currStringRef, newStringRef);
                UpdateWwiseStream(maleStream, currStringRef, newStringRef);

                UpdateFaceFX(FXAControl_F, currStringRef, newStringRef);
                UpdateFaceFX(FXAControl_M, currStringRef, newStringRef);
            }


            if (int.TryParse(newStringRef, out int intRef)) { node.LineStrRef = intRef; }

            MessageBox.Show($"The node now points to {newStringRef}.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Update the name of the WwiseEvents referencing an input WwiseStream.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="wwiseEvent">WwiseStream to use to find the WwiseEvents.</param>
        /// <param name="oldRef">Part of the name to replace.</param>
        /// <param name="newRef">String to replace with.</param>
        public static void UpdateWwiseEvent(IMEPackage pcc, ExportEntry wwiseEvent, string oldRef, string newRef)
        {
            if (wwiseEvent == null) { return; }

            wwiseEvent.ObjectNameString = wwiseEvent.ObjectNameString.Replace(oldRef, newRef);
        }

        /// <summary>
        /// Update the name of the given WwiseStream.
        /// </summary>
        /// <param name="wwiseStream">WwiseStream to update.</param>
        /// <param name="oldRef">Part of the name to replace.</param>
        /// <param name="newRef">String to replace with.</param>
        public static void UpdateWwiseStream(ExportEntry wwiseStream, string oldRef, string newRef)
        {
            if (wwiseStream == null) { return; }

            // Pads the string refs so they have the required minimum length
            newRef = newRef.PadLeft(8, '0');
            oldRef = oldRef.PadLeft(8, '0');

            wwiseStream.ObjectNameString = wwiseStream.ObjectNameString.Replace(oldRef, newRef);
        }

        /// <summary>
        /// Update the TLK reference of the SoundCue/SoundNodeWave.
        /// </summary>
        /// <param name="soundExp">SoundCue/SoundNodeWave to update.</param>
        /// <param name="oldRef">Old TLK reference.</param>
        /// <param name="newRef">New TLK reference.</param>
        private static void UpdateME1SoundExport(ExportEntry soundExp, string oldRef, string newRef)
        {
            if (soundExp == null) { return; }

            if (soundExp.ObjectName.Name.EndsWith("_M"))
            {
                string name = soundExp.ObjectName.Name.Replace(oldRef, newRef, System.StringComparison.OrdinalIgnoreCase);
                soundExp.ObjectName = new NameReference(name, soundExp.ObjectName.Number);
            }
            else
            {
                soundExp.ObjectName = new NameReference(soundExp.ObjectName.Name, int.Parse(newRef) + 1);
            }
        }

        /// <summary>
        /// Update the path, ID, and name of a given FXA.
        /// </summary>
        /// <param name="FXAControl">FXA control containing the FXA to update.</param>
        /// <param name="oldRef">Part of the name to replace.</param>
        /// <param name="newRef">String to replace with.</param>
        /// <param name="femaleLine">Manually provided female SelectedLine. Used for ME1.</param>
        public static void UpdateFaceFX(FaceFXAnimSetEditorControl FXAControl, string oldRef, string newRef, FaceFXLine femaleLine = null)
        {
            if (FXAControl == null) { return; }

            FaceFXAnimSetEditorControl.IFaceFXBinary FaceFX = FXAControl.FaceFX;

            FaceFXLine selectedLine = null;
            if (femaleLine != null)
            {
                selectedLine = femaleLine;
            }
            else
            {
                selectedLine = FXAControl.SelectedLine;
            }

            if (selectedLine == null) { return; }

            if (selectedLine.Path != null)
            {
                selectedLine.Path = selectedLine.Path.Replace(oldRef, newRef);
            }
            if (selectedLine.ID != null)
            {
                selectedLine.ID = selectedLine.ID.Replace(oldRef, newRef);
            }

            // Change FaceFX name
            if (selectedLine.NameAsString != null)
            {
                string newName = selectedLine.NameAsString.Replace(oldRef, newRef);
                if (FaceFX.Names.Contains(newName))
                {
                    selectedLine.NameIndex = FaceFX.Names.IndexOf(newName);
                    selectedLine.NameAsString = newName;
                }
                else
                {
                    FaceFX.Names.Add(newName);
                    selectedLine.NameIndex = FaceFX.Names.Count - 1;
                    selectedLine.NameAsString = newName;
                }
            }

            FXAControl.SaveChanges();
        }

        /// <summary>
        /// Get the WwiseEvent listed in the Path of a given FXA, if it exists.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="selectedLine">Selected FXA line.</param>
        /// <returns>WwiseEvent if found, null otherwise.</returns>
        public static ExportEntry GetWwiseEvent(IMEPackage pcc, FaceFXLine selectedLine)
        {
            if (pcc == null || selectedLine == null) { return null; }

            if (string.IsNullOrEmpty(selectedLine.Path)) { return null; }

            return pcc.FindExport(selectedLine.Path);
        }

        /// <summary>
        /// Get the WwiseStream referenced in the WwiseEvent that contains the stringRef and the suffic.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="wwiseEvent">WwiseEvent that references ther WwiseStream.</param>
        /// <param name="stringRef">StringRef to search for.</param>
        /// <param name="suffix">_m_ or _f_.</param>
        /// <returns>WwiseStream that matches the criteria, if any.</returns>
        public static ExportEntry GetWwiseStream(IMEPackage pcc, ExportEntry wwiseEvent, string stringRef, string suffix)
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

        /// <summary>
        /// Get the SoundCue listed in the Path of a given FXA, if it exists.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="selectedLine">Selected FXA line.</param>
        /// <returns>SoundCue if found, null otherwise.</returns>
        private static ExportEntry GetSoundCue(IMEPackage pcc, FaceFXLine selectedLine)
        {
            if (pcc == null || selectedLine == null) { return null; }

            if (string.IsNullOrEmpty(selectedLine.Path)) { return null; }

            return pcc.FindExport(selectedLine.Path);
        }

        /// <summary>
        /// Get the SoundNodeWave listed in the Path of a given FXA, if it exists.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="selectedLineEntry">Selected FXA line entry.</param>
        /// <returns>SoundNodeWave if found, null otherwise.</returns>
        private static ExportEntry GetSoundNodeWave(IMEPackage pcc, FaceFXLineEntry selectedLineEntry)
        {
            if (pcc == null || selectedLineEntry == null) { return null; }

            FaceFXLine selectedLine = selectedLineEntry.Line;

            if (string.IsNullOrEmpty(selectedLine.ID)) { return null; }

            // Manually determine if the line is male or female.
            // Cannot use the IsMale property, as it seems to be true for both.
            bool isMale = selectedLine.NameAsString.EndsWith("_M");
            List<IEntry> references;

            if (isMale)
            {
                references = pcc.FindUsagesOfName(selectedLine.ID)
                    .Where(e => string.Equals(selectedLine.ID, e.Key.ObjectName.Instanced, System.StringComparison.OrdinalIgnoreCase)
                          && e.Key.ObjectName.Instanced.EndsWith("_M"))
                    .Select(e => e.Key).ToList();
            }
            else
            {
                // something:VO_1235 -> something:VO
                string referenceName = selectedLine.ID.Replace($"_{selectedLineEntry.TLKID}", "");
                references = pcc.FindUsagesOfName(referenceName)
                    .Where(e => e.Key.ObjectName.Number == (selectedLineEntry.TLKID + 1))
                    .Select(e => e.Key).ToList();
            }

            if (references == null || references.Count == 0) { return null; }

            return (ExportEntry)references.First();
        }

        /// <summary>
        /// Get the selectd line based on the FXA name. Used for ME1, as female lines are not automatically loaded
        /// into the FaceFXAnimSetEditorControl.
        /// </summary>
        /// <param name="fxaName">Name of the line entry to find.</param>
        /// <param name="fxaLines">FXA lines.</param>
        /// <returns>Selected FaceFXLineEntry</returns>
        private static FaceFXLineEntry GetSelectedLineEntry(string fxaName, ObservableCollectionExtended<FaceFXLineEntry> fxaLines)
        {
            if (fxaLines == null) { return null; }

            List<FaceFXLineEntry> lines = fxaLines.Where(l =>
            {
                if (l == null || l.Line == null || string.IsNullOrEmpty(l.Line.NameAsString)) { return false; }
                return string.Equals(l.Line.NameAsString, fxaName, System.StringComparison.OrdinalIgnoreCase);
            }).ToList();
            if (lines.Count == 0) { return null; }

            return lines.First();
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

                int newID = PromptForInt("New node ExportID:", "Not a valid ExportID.", 0);
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
                ExportEntry oldConvNode = KismetHelper.FindOutputConnectionsToNode(oldInterp, KismetHelper.GetAllSequenceElements(oldInterp).OfType<ExportEntry>())
                    .FirstOrDefault(entry => entry.ClassName == "BioSeqEvt_ConvNode");

                // Get the/a EndCurrentConvNode that the Interp outputs to
                ExportEntry oldEndNode = KismetHelper.GetOutputLinksOfNode(oldInterp).Select(outboundLink =>
                {
                    IEnumerable<OutputLink> links = outboundLink.Where(link => link.LinkedOp.ClassName == "BioSeqAct_EndCurrentConvNode");
                    if (links.Any()) { return (ExportEntry)links.First().LinkedOp; } else { return null; }
                }).ToList().FirstOrDefault();

                ExportEntry sequence = KismetHelper.GetParentSequence(oldInterpData);

                // Clone the Intero and Interpdata objects
                ExportEntry newInterp = CloneObject(oldInterp, sequence);
                ExportEntry newInterpData = EntryCloner.CloneTree(oldInterpData);
                KismetHelper.AddObjectToSequence(newInterpData, sequence, true);

                // Clone and link the Conv and End objects, if they exist
                ExportEntry newConvNode = null;
                if (oldConvNode != null)
                {
                    newConvNode = CloneObject(oldConvNode, sequence);
                    KismetHelper.CreateOutputLink(newConvNode, "Started", newInterp, 0);
                }

                if (oldEndNode != null)
                {
                    ExportEntry newEndNode = CloneObject(oldEndNode, sequence);
                    KismetHelper.CreateOutputLink(newInterp, "Completed", newEndNode, 0);
                    KismetHelper.CreateOutputLink(newInterp, "Reversed", newEndNode, 0);
                }

                // Save existing varLinks, minus the Data one
                List<VarLinkInfo> varLinks = KismetHelper.GetVariableLinksOfNode(oldInterp);
                foreach (VarLinkInfo link in varLinks)
                {
                    if (link.LinkDesc == "Data") { link.LinkedNodes = []; }
                }
                KismetHelper.WriteVariableLinksToNode(newInterp, varLinks);
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
        #endregion

        #region Link Nodes and Create Sequence
        /// <summary>
        /// Links all audio nodes in the conversation without an ExportID to the free ConvNodes in the sequence.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void LinkNodesFree(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedConv == null) { return; }

            int convNodeIDBase = PromptForInt("New ExportIDs base for extra IDs that may be need to be created:",
                "Not a valid base. It must be positive integer", -1, "New NodeID range");
            if (convNodeIDBase == -1) { return; }

            ConversationExtended conversation = dew.SelectedConv;

            HashSet<int> usedIDs = new();
            List<DialogueNodeExtended> nodes = new();
            List<DialogueNodeExtended> remainingNodes = new();

            List<DialogueNodeExtended> entryNodes = FilterAudioNodes(conversation.EntryList, el => el.ExportID < 1, usedIDs);
            List<DialogueNodeExtended> replyNodes = FilterAudioNodes(conversation.ReplyList, el => el.ExportID < 1, usedIDs);

            nodes.AddRange(entryNodes);
            nodes.AddRange(replyNodes);

            List<(int, ExportEntry, ExportEntry, ExportEntry, int)> elements = GetConvNodeElements((ExportEntry)dew.SelectedConv.Sequence, conversation, usedIDs, false);

            // Assign ExportIDs to the dialogue nodes, and write the new StrRefIDs to the VOElements tracks
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i >= elements.Count)
                {
                    // Store a list of nodes that couldn't get an ExportID
                    int remainingCount = nodes.Count - elements.Count;
                    if (remainingCount > 0)
                    {
                        remainingNodes.AddRange(nodes.GetRange(i, remainingCount));
                    }
                    break;
                }

                DialogueNodeExtended node = nodes[i];
                (int exportID, ExportEntry VOElements, ExportEntry interpData, ExportEntry interp, int _) = elements[i];

                // Write the new ExportID
                node.NodeProp.Properties.AddOrReplaceProp(new IntProperty(exportID, "nExportID"));
                // Update the Interp comment
                interp.WriteProperty(GenerateObjComment(node.Line));
                // Write the StringRef
                UpdateInterpDataStrRefID(interpData, node.LineStrRef, VOElements);

                usedIDs.Add(exportID); // Mark the ExportID as used
            }

            // Create the sequence objects for any nodes that are left without an ExportID
            if (remainingNodes.Any())
            {
                CreateNodesSequence(dew.Pcc, conversation, convNodeIDBase, nodes, usedIDs);
            }

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show("Linked all nodes without an ExportID.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Links all audio nodes in the conversation without an ExportID to the free ConvNodes that have a matching StringRef in the sequence.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void LinkNodesStrRef(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedConv == null) { return; }

            int convNodeIDBase = 0;

            bool createObjsForNotMatched = MessageBoxResult.Yes == MessageBox.Show(
                "Generate new sequence objects with a basic VO track and new ExportIDs for nodes that don't have a match?",
                "Generate new sequence objects", MessageBoxButton.YesNo);

            if (createObjsForNotMatched)
            {
                convNodeIDBase = PromptForInt("New ExportIDs base for new IDs that may be needed:",
                    "Not a valid base. It must be positive integer", -1, "New NodeID range");
                if (convNodeIDBase == -1) { return; }
            }

            ConversationExtended conversation = dew.SelectedConv;

            HashSet<int> usedIDs = new();
            List<DialogueNodeExtended> nodes = new();
            List<DialogueNodeExtended> notMatchedNodes = new();
            List<string> notMatchedNodesNames = new(); // Used for the result message

            List<DialogueNodeExtended> entryNodes = FilterAudioNodes(conversation.EntryList, el => el.ExportID < 1, usedIDs);
            List<DialogueNodeExtended> replyNodes = FilterAudioNodes(conversation.ReplyList, el => el.ExportID < 1, usedIDs);

            nodes.AddRange(entryNodes);
            nodes.AddRange(replyNodes);

            // Key: StrRefID, Val: (ExportID, Interp, VOElements)
            Dictionary<int, (int, ExportEntry, ExportEntry)> exportIDs = new();
            foreach (var el in GetConvNodeElements((ExportEntry)dew.SelectedConv.Sequence, conversation, usedIDs))
            {
                // We do it like this instead of using ToDictionary to avoid errors with duplicate keys
                (int ExportID, ExportEntry VOElements, ExportEntry _, ExportEntry interp, int StrRefID) = el;
                exportIDs[StrRefID] = (ExportID, interp, VOElements);
            }

            // Assign ExportIDs to the dialogue nodes that match the StrRefID
            foreach (DialogueNodeExtended node in nodes)
            {
                if (exportIDs.TryGetValue(node.LineStrRef, out (int, ExportEntry, ExportEntry) el))
                {
                    (int exportID, ExportEntry interp, ExportEntry VOElements) = el;

                    // Write the new ExportID
                    node.NodeProp.Properties.AddOrReplaceProp(new IntProperty(exportID, "nExportID"));
                    // Insert a defualt key if needed
                    AddDefaultTrackKey(VOElements, true, 0, VOElements.GetProperties());
                    // Update the Interp comment
                    interp.WriteProperty(GenerateObjComment(node.Line));

                    usedIDs.Add(exportID); // Mark the ExportID as used
                }
                else
                {
                    notMatchedNodes.Add(node);
                    notMatchedNodesNames.Add(node.IsReply ? $"R{node.NodeCount}" : $"E{node.NodeCount}");
                }
            }

            // Create the sequence objects for any nodes that are left without an ExportID
            if (createObjsForNotMatched)
            {
                CreateNodesSequence(dew.Pcc, conversation, convNodeIDBase, notMatchedNodes, usedIDs);
                // Clear the not matched nodes
                notMatchedNodes = new();
                notMatchedNodesNames = new();
            }

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            string message = $"{nodes.Count - notMatchedNodes.Count} nodes matched.";
            if (notMatchedNodesNames.Any())
            {
                message = $"{message} The following nodes' StrRefIDs were not found in any InterpData: \n{string.Join(", ", notMatchedNodesNames)}";
            }

            MessageBox.Show(message, "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Wrapper for CreateNodesSequence so it can used as an experiment.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void BatchCreateNodesSequenceExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedConv == null) { return; }

            int convNodeIDBase = PromptForInt("New ExportIDs base:", "Not a valid base. It must be positive integer", -1, "New NodeID range");
            if (convNodeIDBase == -1) { return; }

            HashSet<int> usedIDs = new();
            List<DialogueNodeExtended> nodes = new();

            List<DialogueNodeExtended> entryNodes = FilterAudioNodes(dew.SelectedConv.EntryList, el => el.ExportID < 1, usedIDs);
            List<DialogueNodeExtended> replyNodes = FilterAudioNodes(dew.SelectedConv.ReplyList, el => el.ExportID < 1, usedIDs);

            nodes.AddRange(entryNodes);
            nodes.AddRange(replyNodes);

            CreateNodesSequence(dew.Pcc, dew.SelectedConv, convNodeIDBase, nodes, usedIDs);

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            string txtCount = nodes.Count == 1 ? "one audio node" : $"{nodes.Count} nodes";
            MessageBox.Show($"Successfully created the sequence objects for {txtCount}.", "Success", MessageBoxButton.OK);
        }


        /// <summary>
        /// Create the basic sequence objects for the selected audio node, if it doesn't have an ExportID.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void CreateNodeSequenceExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedDialogueNode == null) { return; }

            int exportID = PromptForInt("New ExportID. If you input 0, a new ID will be generated:", "Not a valid ID. It must be positive integer", -1, "New NodeID");
            if (exportID == -1) { return; }

            DialogueNodeExtended node = dew.SelectedDialogueNode;

            if (!IsAudioNode(node, out string errMsg))
            {
                MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
                return;
            }
            if (node.Interpdata != null)
            {
                MessageBox.Show("The selected node already points to an InterpData.", "Warning", MessageBoxButton.OK);
                return;
            }

            // If the provided ID is 0, generate an ID not in use in the conversation
            List<int> newExportIDs = new();
            if (exportID == 0)
            {
                HashSet<int> usedIDs = new();
                List<DialogueNodeExtended> nodes = new();

                List<DialogueNodeExtended> entryNodes = FilterAudioNodes(dew.SelectedConv.EntryList, el => el.ExportID < 1, usedIDs);
                List<DialogueNodeExtended> replyNodes = FilterAudioNodes(dew.SelectedConv.ReplyList, el => el.ExportID < 1, usedIDs);

                nodes.AddRange(entryNodes);
                nodes.AddRange(replyNodes);

                newExportIDs = GenerateIDs(100, 1, usedIDs);
                exportID = newExportIDs.First();
            }

            // Write the new ExportID
            node.NodeProp.Properties.AddOrReplaceProp(new IntProperty(exportID, "nExportID"));

            // Create the required sequence elements and add it to the new exports list
            List<ExportEntry> newExports = CreateDialogueNodeSequence(dew.Pcc, exportID, dew.SelectedConv.BioConvo.GetProp<IntProperty>("m_nResRefID").Value,
                node.LineStrRef, node.Line);

            if (newExports.Any())
            {
                KismetHelper.AddObjectsToSequence((ExportEntry)dew.SelectedConv.Sequence, false, newExports.ToArray());
            }

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show($"Successfully created the sequence objects.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Create the basic sequence objects for all the audio nodes that don't have an ExportID.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="conversation">Conversation to create the objects for.</param>
        /// <param name="convNodeIDBase">Base ID for the new ExportIDs.</param>
        /// <param name="nodes">Nodes to generate the sequence objects for.</param>
        /// <param name="usedIDs">ExportIDs in use.</param>
        public static void CreateNodesSequence(IMEPackage pcc, ConversationExtended conversation, int convNodeIDBase,
            List<DialogueNodeExtended> nodes, HashSet<int> usedIDs)
        {
            List<int> newExportIDs = GenerateIDs(convNodeIDBase, nodes.Count, usedIDs);
            List<ExportEntry> newExports = new(); // Sequence objects to add

            for (int i = 0; i < nodes.Count; i++)
            {
                DialogueNodeExtended node = nodes[i];
                int exportID = newExportIDs[i];

                // Write the new ExportID
                node.NodeProp.Properties.AddOrReplaceProp(new IntProperty(exportID, "nExportID"));

                // Create the required sequence elements and add it to the new exports list
                newExports.AddRange(CreateDialogueNodeSequence(pcc, exportID, conversation.BioConvo.GetProp<IntProperty>("m_nResRefID").Value,
                    node.LineStrRef, node.Line));
            }

            if (newExports.Any())
            {
                KismetHelper.AddObjectsToSequence((ExportEntry)conversation.Sequence, false, newExports.ToArray());
            }
        }

        /// <summary>
        /// Create all the required sequence elements for a dialogue node. IT DOES NOT ADD THE EXPORTS TO THE SEQUENCE.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="nodeID">Node's ExportID.</param>
        /// <param name="convResRefID">Conversation's ID.</param>
        /// <param name="strRefID">Node's StrRefID.</param>
        /// <param name="line">Text of the Node's StrRefID.</param>
        /// <returns>List of created exports.</returns>
        private static List<ExportEntry> CreateDialogueNodeSequence(IMEPackage pcc, int nodeID, int convResRefID, int strRefID, string line)
        {
            List<ExportEntry> exports = new();

            // Create ConvNode
            ExportEntry convNode = SequenceObjectCreator.CreateSequenceObject(pcc, "BioSeqEvt_ConvNode");
            PropertyCollection convNodeProps = SequenceObjectCreator.GetSequenceObjectDefaults(pcc, "BioSeqEvt_ConvNode", pcc.Game);
            convNodeProps.AddOrReplaceProp(new IntProperty(nodeID, "m_nNodeID"));
            convNodeProps.AddOrReplaceProp(new IntProperty(convResRefID, "m_nConvResRefID"));
            convNode.WriteProperties(convNodeProps);
            exports.Add(convNode);

            // Create Interp
            ExportEntry interp = SequenceObjectCreator.CreateSequenceObject(pcc, "SeqAct_Interp");
            PropertyCollection interpProps = SequenceObjectCreator.GetSequenceObjectDefaults(pcc, "SeqAct_Interp", pcc.Game);
            interpProps.AddOrReplaceProp(new ArrayProperty<StrProperty>("m_aObjComment")
            {
                new StrProperty(line == "No Data" ? "" : line.Length <= 32 ? line : $"{line.AsSpan(0, 29)}...")
            });
            interpProps.AddOrReplaceProp(new BoolProperty(true, "bRewindOnPlay"));
            // Add Conversation variable link
            ArrayProperty<StructProperty> variableLinks = interpProps.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
            PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(pcc.Game, "SeqVarLink", true);
            props.AddOrReplaceProp(new StrProperty("Conversation", "LinkDesc"));
            int index = pcc.FindImport("Engine.SeqVar_Object").UIndex;
            props.AddOrReplaceProp(new ObjectProperty(index, "ExpectedType"));
            props.AddOrReplaceProp(new IntProperty(1, "MinVars"));
            props.AddOrReplaceProp(new IntProperty(255, "MaxVars"));
            variableLinks.Add(new StructProperty("SeqVarLink", props));
            interpProps.AddOrReplaceProp(variableLinks);
            interp.WriteProperties(interpProps);
            exports.Add(interp);

            // Create EndCurrentConvNode
            ExportEntry endNode = SequenceObjectCreator.CreateSequenceObject(pcc, "BioSeqAct_EndCurrentConvNode");
            PropertyCollection endNodeProps = SequenceObjectCreator.GetSequenceObjectDefaults(pcc, "BioSeqAct_EndCurrentConvNode", pcc.Game);
            endNode.WriteProperties(endNodeProps);
            exports.Add(endNode);

            // Create InterpData
            ExportEntry interpData = SequenceObjectCreator.CreateSequenceObject(pcc, "InterpData");
            PropertyCollection interpDataProps = SequenceObjectCreator.GetSequenceObjectDefaults(pcc, "InterpData", pcc.Game);
            interpDataProps.AddOrReplaceProp(new FloatProperty(3, "InterpLength"));
            interpData.WriteProperties(interpDataProps);
            // Add Conversation group and VOElements track with its StrRefID
            ExportEntry conversationGroup = MatineeHelper.AddNewGroupToInterpData(interpData, "Conversation");
            ExportEntry VOElements = MatineeHelper.AddNewTrackToGroup(conversationGroup, "BioEvtSysTrackVOElements");
            VOElements.WriteProperty(new IntProperty(strRefID, "m_nStrRefID"));
            VOElements.WriteProperty(new ArrayProperty<StructProperty>("m_aTrackKeys")
            {
                new StructProperty("BioTrackKey", new PropertyCollection()
                {
                    new NameProperty("None", "KeyName"),
                    new FloatProperty(0, "fTime")
                }, "BioTrackKey")
            });

            exports.Add(interpData);

            // Connect elements
            KismetHelper.CreateOutputLink(convNode, "Started", interp, 0);
            KismetHelper.CreateOutputLink(interp, "Completed", endNode, 0);
            KismetHelper.CreateOutputLink(interp, "Reversed", endNode, 0);
            KismetHelper.CreateVariableLink(interp, "Data", interpData);

            return exports;
        }

        /// <summary>
        /// Get a list of ExportIDs, VOElements track, Interp, and StrRefIDs of all the ConvNodes in the sequence.
        /// </summary>
        /// <param name="sequence">Sequence to get the elements from.</param>
        /// <param name="conversation">BioConversation to operate on.</param>
        /// <param name="usedIDs">List of ExportIDs that are already in use.</param>
        /// <param name="ignoredNonVOs">Whether to ignore nodes that don't have a VOElements track.</param>
        /// <returns>List of (ExportID, VOElements track, InterpData, Interp, StrRefID)</returns>
        private static List<(int, ExportEntry, ExportEntry, ExportEntry, int)> GetConvNodeElements(ExportEntry sequence, ConversationExtended conversation, HashSet<int> usedIDs, bool ignoredNonVOs = true)
        {
            IMEPackage pcc = sequence.FileRef;

            List<(int, ExportEntry, ExportEntry, ExportEntry, int)> elements = new();

            List<IEntry> convNodes = KismetHelper.GetAllSequenceElements(sequence)
                .Where(el => el.ClassName == "BioSeqEvt_ConvNode").ToList();

            foreach (ExportEntry node in convNodes)
            {
                IntProperty m_nNodeID = node.GetProperty<IntProperty>("m_nNodeID");
                // Skip nodes that don't have an ExportID, or an ExportID that is already in use
                if (m_nNodeID == null || usedIDs.Contains(m_nNodeID.Value)) { continue; }

                // Find the interp data
                ExportEntry interpData = null;
                List<ExportEntry> searchingExports = new() { node };

                ExportEntry seqActInterp = conversation.recursiveFindSeqActInterp(searchingExports, new List<ExportEntry>(), 10);
                if (seqActInterp == null) { continue; }

                ArrayProperty<StructProperty> varLinksProp = seqActInterp.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");

                if (varLinksProp != null)
                {
                    foreach (StructProperty prop in varLinksProp)
                    {
                        string desc = prop.GetProp<StrProperty>("LinkDesc").Value; //ME3/ME2/ME1
                        if (desc == "Data") //ME3/ME1
                        {
                            ArrayProperty<ObjectProperty> linkedVars = prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                            if (linkedVars != null && linkedVars.Any())
                            {
                                int datalink = linkedVars[0].Value;
                                interpData = sequence.FileRef.GetUExport(datalink);
                            }
                            break;
                        }
                    }
                }

                // Only consider as valid ExportIDs that lead to InterpDatas
                if (interpData == null) { continue; }

                // Store the StrRefID in the VOElements track, if one exists
                int strRefID = GetVOStrRefID(interpData, out ExportEntry VOElements);

                if (ignoredNonVOs)
                {
                    // Only consider as valid InterpDatas that contain a VOElements track
                    if (VOElements == null) { continue; }
                }

                elements.Add((m_nNodeID.Value, VOElements, interpData, seqActInterp, strRefID));
            }

            return elements;
        }

        /// <summary>
        /// Generate a list of IDs starting at base, of the given length, and skip IDs that are in the usedIDs list.
        /// </summary>
        /// <param name="baseID">Base num for the list.</param>
        /// <param name="length">Target length of the list.</param>
        /// <param name="usedIDs">IDs to skip.</param>
        /// <returns>Generated IDs.</returns>
        private static List<int> GenerateIDs(int baseID, int length, HashSet<int> usedIDs)
        {
            List<int> ids = new();

            int count = 0;
            while (count < length)
            {
                if (usedIDs != null && !usedIDs.Contains(baseID))
                {
                    ids.Add(baseID);
                    count++;
                }
                baseID++;
            }

            return ids;
        }
        #endregion

        #region Update VOs and Comments
        /// <summary>
        /// Update all the Interp comments and VOElements' StrRefIDs that are linked to the audio nodes of the selected conversation.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void BatchUpdateVOsAndCommentsExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedConv == null) { return; }

            List<DialogueNodeExtended> nodes = new();
            int updateCount = 0;

            nodes.AddRange(dew.SelectedConv.EntryList);
            nodes.AddRange(dew.SelectedConv.ReplyList);

            foreach (DialogueNodeExtended node in nodes)
            {
                if (IsAudioNode(node) && node.ExportID > 0)
                {
                    UpdateVOAndComment(node);
                    updateCount += 1;
                }
            }

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            string txtCount = updateCount == 1 ? "one audio node" : $"{updateCount} nodes";
            MessageBox.Show($"Successfully updated the StrRefID and Interp comment for {txtCount}.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Update the Interp comment and VOElement' StrRefID that are linked to the selected audio node.
        /// Wrapper for UpdateVOAndComment so it can used as an experiment.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void UpdateVOAndCommentExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedDialogueNode == null) { return; }

            DialogueNodeExtended node = dew.SelectedDialogueNode;

            if (!IsAudioNode(node, out string errMsg))
            {
                MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
                return;
            }
            if (node.Interpdata == null)
            {
                MessageBox.Show("The selected node doesn't point to an InterpData.", "Warning", MessageBoxButton.OK);
                return;
            }

            UpdateVOAndComment(node);

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show($"Successfully updated the StrRefID and Interp comment for the selected node.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Update the StrRefID of the node's InterpData and the comment of the Interp linking to it.
        /// </summary>
        /// <param name="node">Node to update</param>
        private static void UpdateVOAndComment(DialogueNodeExtended node)
        {
            ExportEntry interpData = node.Interpdata;

            if (interpData != null && TryGetInterp(interpData, out ExportEntry interp))
            {
                UpdateInterpDataStrRefID(interpData, node.LineStrRef);
                // Update the Interp comment
                interp.WriteProperty(GenerateObjComment(node.Line));
            }
        }
        #endregion

        #region Add Conversation Defaults
        /// <summary>
        /// Add a Conversation group, and VOElements and SwitchCamera tracks to all audio nodes in the selected conversation.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void BatchAddConversationDefaultsExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedConv == null) { return; }

            List<DialogueNodeExtended> nodes = new();

            nodes.AddRange(dew.SelectedConv.EntryList);
            nodes.AddRange(dew.SelectedConv.ReplyList);

            foreach (DialogueNodeExtended node in nodes)
            {
                if (IsAudioNode(node) && node.ExportID > 0 && node.Interpdata != null)
                {
                    AddConversationDefaultsToInterpData(node.Interpdata, node.LineStrRef);
                }
            }

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show($"Successfully added the default conversation elements to all audio nodes in the conversation.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Add a Conversation group, and VOElements and SwitchCamera tracks to the selected node.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void AddConversationDefaultsExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedDialogueNode == null) { return; }

            DialogueNodeExtended node = dew.SelectedDialogueNode;

            if (!IsAudioNode(node, out string errMsg))
            {
                MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
                return;
            }
            if (node.Interpdata == null)
            {
                MessageBox.Show("The selected node doesn't point to an InterpData.", "Warning", MessageBoxButton.OK);
                return;
            }

            AddConversationDefaultsToInterpData(node.Interpdata, node.LineStrRef);

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show($"Successfully added the default conversation elements to the selected node.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Add a Conversation group, and VOElements and SwitchCamera tracks to interpData.
        /// </summary>
        /// <param name="interpData">InterpData to add the elements to.</param>
        /// <param name="strRefID">StrRefID for the VOElements track</param>
        private static void AddConversationDefaultsToInterpData(ExportEntry interpData, int strRefID = 0)
        {
            if (!MatineeHelper.TryGetInterpGroup(interpData, "Conversation", out ExportEntry interpGroup))
            {
                interpGroup = MatineeHelper.AddNewGroupToInterpData(interpData, "Conversation");
            }

            if (!MatineeHelper.TryGetInterpTrack(interpGroup, "BioEvtSysTrackVOElements", out ExportEntry VOElements))
            {
                VOElements = MatineeHelper.AddNewTrackToGroup(interpGroup, "BioEvtSysTrackVOElements");
            }
            PropertyCollection props = VOElements.GetProperties();
            props.AddOrReplaceProp(new IntProperty(strRefID, "m_nStrRefID"));
            AddDefaultTrackKey(VOElements, false, 0, props);
            VOElements.WriteProperties(props);

            if (!MatineeHelper.TryGetInterpTrack(interpGroup, "BioEvtSysTrackSwitchCamera", out _))
            {
                ExportEntry SwitchCamera = MatineeHelper.AddNewTrackToGroup(interpGroup, "BioEvtSysTrackSwitchCamera");
                MatineeHelper.AddDefaultPropertiesToTrack(SwitchCamera);
            }
        }
        #endregion

        #region Update Interp Lengths
        /// <summary>
        /// Update the InterpLength of all the audio nodes in the selectd conversation, based either on the FXA or the audio length.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void BatchUpdateInterpLengthsExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedConv == null) { return; }

            bool byFXA = MessageBoxResult.Yes == MessageBox.Show(
                "Calculate the InterpLengths by the FXA length? If not, the audio length will be used.",
                "Calculate by FXA", MessageBoxButton.YesNo);

            List<DialogueNodeExtended> nodes = new();

            nodes.AddRange(dew.SelectedConv.EntryList);
            nodes.AddRange(dew.SelectedConv.ReplyList);

            foreach (DialogueNodeExtended node in nodes)
            {
                if (IsAudioNode(node) && node.ExportID > 0 && node.Interpdata != null)
                {
                    UpdateInterpLength(node, byFXA, dew.FaceFXAnimSetEditorControl_F, dew.FaceFXAnimSetEditorControl_M);
                }
            }

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show($"Successfully updated the InterpLength of all audio nodes in the conversation.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Wrapper for UpdateInterpLength so it can be used on its own.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void UpdateInterpLengthExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedDialogueNode == null) { return; }

            DialogueNodeExtended node = dew.SelectedDialogueNode;

            if (!IsAudioNode(node, out string errMsg))
            {
                MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
                return;
            }
            if (node.Interpdata == null)
            {
                MessageBox.Show("The selected node doesn't point to an InterpData.", "Warning", MessageBoxButton.OK);
                return;
            }

            bool byFXA = MessageBoxResult.Yes == MessageBox.Show(
                "Calculate the InterpLengths by the FXA length? If not, the audio length will be used.",
                "Calculate by FXA", MessageBoxButton.YesNo);

            UpdateInterpLength(node, byFXA, dew.FaceFXAnimSetEditorControl_F, dew.FaceFXAnimSetEditorControl_M);

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show($"Successfully updated the InterpLength of the selected audio node.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Update the InterpLength of the Node, based either on the FXA or the audio length.
        /// </summary>
        /// <param name="node">Node to update.</param>
        /// <param name="byFXA">Whether to update the length by the FXA length, or the audio one.</param>
        private static void UpdateInterpLength(DialogueNodeExtended node, bool byFXA, FaceFXAnimSetEditorControl animControlF, FaceFXAnimSetEditorControl animControlM)
        {
            float interpLength = 0;
            IMEPackage pcc = node.Interpdata.FileRef;

            if (byFXA)
            {
                // Refresh the export of the FaceFX control. This is done so that the batch version can get the lengths
                // without having to select the converastion first, because otherwise you get the previously loaded export.
                // Without this, it also causes issues when you need different sets from the same file.
                //
                // How to improve this to avoid refreshing on EVERY node? We could probably split the nodes by set, and only
                // refresh once per set, or we could redo the animation Points loading and length calculation code and avoid using the
                // FaceFXAnimSetEditorControl altogether, but both would take more time to implement than the 2 to 5 seconds the
                // batch experiment takes on long conversations.
                if (node.SpeakerTag?.FaceFX_Female is ExportEntry faceFX_f)
                {
                    animControlF.LoadExport(faceFX_f);
                }
                else
                {
                    animControlF.UnloadExport();
                }

                if (node.SpeakerTag?.FaceFX_Male is ExportEntry faceFX_m)
                {
                    animControlM.LoadExport(faceFX_m);
                }
                else
                {
                    animControlM.UnloadExport();
                }

                FaceFXLineEntry femaleLine = null;
                if (animControlF != null && animControlF.Lines != null)
                {
                    femaleLine = animControlF.Lines.FirstOrDefault(line => line.TLKID == node.LineStrRef);
                }

                FaceFXLineEntry maleLine = null;
                if (animControlM != null && animControlM.Lines != null)
                {
                    maleLine = animControlM.Lines.FirstOrDefault(line => line.TLKID == node.LineStrRef);
                }

                float lengthF = 0;
                float lengthM = 0;
                if (femaleLine != null) { lengthF = femaleLine.Length; }
                if (maleLine != null) { lengthM = maleLine.Length; }

                interpLength = lengthF > lengthM ? lengthF : lengthM;
                interpLength += 0.22F; // Add fadeout time
            }
            else
            {
                string lineRef = $"{node.LineStrRef}";
                IEnumerable<ExportEntry> references = pcc.Exports.Where(exp => exp.ObjectName.Name.Contains(lineRef)
                    && exp.ClassName == (pcc.Game.IsGame1() ? "SoundNodeWave" : "WwiseEvent"));

                if (references != null)
                {
                    // We'll record the duration of the longest reference
                    foreach (ExportEntry reference in references)
                    {
                        FloatProperty duration = reference.GetProperty<FloatProperty>(pcc.Game.IsGame1() ? "Duration" : "DurationSeconds");
                        if (duration != null) { interpLength = duration.Value > interpLength ? duration.Value : interpLength; }
                    }
                }
            }

            // If the first VO starts beyond zero, add that offset to the length
            if (MatineeHelper.TryGetInterpGroup(node.Interpdata, "Conversation", out ExportEntry conversation))
            {
                if (MatineeHelper.TryGetInterpTrack(conversation, "BioEvtSysTrackVOElements", out ExportEntry VOElements))
                {
                    ArrayProperty<StructProperty> m_aTrackKeys = VOElements.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");

                    if (m_aTrackKeys != null && m_aTrackKeys.Any())
                    {
                        interpLength += m_aTrackKeys.First().GetProp<FloatProperty>("fTime").Value;
                    }
                }
            }

            node.Interpdata.WriteProperty(new FloatProperty(interpLength, "InterpLength"));
        }
        #endregion

        #region Generate LE1 Audio Links
        /// <summary>
        /// Create SoundCues and SoundNodeWaves, and link them to the FaceFX for all audio nodes that don't have one.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void BatchGenerateLE1AudioLinksExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.Pcc.Game != MEGame.LE1 || dew.SelectedConv == null) { return; }

            int bioStreamingDataID = PromptForInt("BioStreamingData export number:", "Not a valid export number. It must be positive integer", 0, "BioStreamingData");
            if (!dew.Pcc.TryGetEntry(bioStreamingDataID, out IEntry entry) || entry.ClassName != "BioSoundNodeWaveStreamingData")
            {
                MessageBox.Show("The provided export is not a BioStreamingData.", "Warning", MessageBoxButton.OK);
                return;
            }

            string baseName = GetBaseConversationName(dew.SelectedConv.Export);
            if (string.IsNullOrEmpty(baseName))
            {
                MessageBox.Show("Could not find a common base name between the conversation and the tlk file set.\n" +
                    "Ensure they both have a common prefix, which will be used as the based of the SoundNodeWave names.", "Warning", MessageBoxButton.OK);
                return;
            }

            List<DialogueNodeExtended> nodes = new();

            nodes.AddRange(dew.SelectedConv.EntryList);
            nodes.AddRange(dew.SelectedConv.ReplyList);

            foreach (DialogueNodeExtended node in nodes)
            {
                if (IsAudioNode(node) && node.ExportID > 0 && node.Interpdata != null)
                {
                    GenerateLE1AudioLinks(node, (ExportEntry)dew.Pcc.GetEntry(dew.SelectedConv.Sequence.idxLink),
                        baseName, bioStreamingDataID);
                }
            }

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show($"Successfully created the SoundCue, SoundNodeWave, and linked it to the FaceFX for all the audio nodes.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Create SoundCues and SoundNodeWaves, and link them to the FaceFX for the selected audio node.
        /// Wrapper for GenerateLE1AudioLinks so it can be used on its own.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void GenerateLE1AudioLinksExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.Pcc.Game != MEGame.LE1 || dew.SelectedDialogueNode == null) { return; }

            DialogueNodeExtended node = dew.SelectedDialogueNode;

            if (!IsAudioNode(node, out string errMsg))
            {
                MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
                return;
            }
            if (node.Interpdata == null)
            {
                MessageBox.Show("The selected node doesn't point to an InterpData.", "Warning", MessageBoxButton.OK);
                return;
            }

            int bioStreamingDataID = PromptForInt("BioStreamingData export number:", "Not a valid export number. It must be positive integer", 0, "BioStreamingData");
            if (!dew.Pcc.TryGetEntry(bioStreamingDataID, out IEntry entry) || entry.ClassName != "BioSoundNodeWaveStreamingData")
            {
                MessageBox.Show("The provided export is not a BioStreamingData.", "Warning", MessageBoxButton.OK);
                return;
            }

            string baseName = GetBaseConversationName(dew.SelectedConv.Export);
            if (string.IsNullOrEmpty(baseName))
            {
                MessageBox.Show("Could not find a common base name between the conversation and the tlk file set.\n" +
                    "Ensure they both have a common prefix, which will be used as the based of the SoundNodeWave names.", "Warning", MessageBoxButton.OK);
                return;
            }

            GenerateLE1AudioLinks(node, (ExportEntry)dew.Pcc.GetEntry(dew.SelectedConv.Sequence.idxLink),
                baseName, bioStreamingDataID);

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show($"Successfully created the SoundCue, SoundNodeWave, and linked it to the FaceFX for the selected audio node.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Create SoundCues and SoundNodeWaves, and link them to the FaceFX for the selected audio node.
        /// </summary>
        /// <param name="node">Node to generate the links for.</param>
        /// <param name="audioPackage">Package containing the audio elements.</param>
        /// <param name="baseName">Base name for the new elements.</param>
        /// <param name="bioStreamingDataID">BioStreamingData to link to the SoundNodeWaves.</param>
        private static void GenerateLE1AudioLinks(DialogueNodeExtended node, ExportEntry audioPackage, string baseName, int bioStreamingDataID)
        {
            IMEPackage pcc = node.Interpdata.FileRef;
            string strRefAsString = $"{node.LineStrRef}";
            ExportEntry soundNodeWaveF = null;
            ExportEntry soundCueF = null;
            ExportEntry soundNodeWaveM = null;
            ExportEntry soundCueM = null;

            // Try to find any SoundNodeWaves or SoundCues, in case we need to only generate one or the other
            foreach (ExportEntry exp in audioPackage.GetAllDescendants())
            {
                if (exp.ClassName == "SoundCue" && exp.ObjectName.Name.Contains(strRefAsString))
                {
                    if (exp.ObjectName.Name.EndsWith("_M", StringComparison.CurrentCultureIgnoreCase)) { soundCueM = exp; }
                    else { soundCueF = exp; }
                }
                else if (exp.ClassName == "SoundNodeWave" && exp.ObjectName.Name.Contains(strRefAsString))
                {
                    if (exp.ObjectName.Name.EndsWith("_M", StringComparison.CurrentCultureIgnoreCase)) { soundNodeWaveM = exp; }
                    else { soundNodeWaveF = exp; }
                }

                if (soundCueM != null && soundNodeWaveM != null && soundCueF != null && soundNodeWaveF != null) // Stop if we found all the elements
                {
                    break;
                }
            }

            ExportEntry FXSetM = node.SpeakerTag.FaceFX_Male as ExportEntry;
            ExportEntry FXSetF = node.SpeakerTag.FaceFX_Female as ExportEntry;

            if (FXSetM != null)
            {
                soundNodeWaveM ??= GenerateSoundNodeWave(pcc, audioPackage, baseName, node.LineStrRef, bioStreamingDataID, true);
                soundCueM ??= GenerateSoundCue(pcc, audioPackage, soundNodeWaveM.UIndex, node.LineStrRef, true);

                LinkLE1AudioToFaceFX(FXSetM, soundCueM, soundNodeWaveM.ObjectName.Name, node.LineStrRef);
            }
            if (FXSetF != null)
            {
                soundNodeWaveF ??= GenerateSoundNodeWave(pcc, audioPackage, baseName, node.LineStrRef, bioStreamingDataID, false);
                soundCueF ??= GenerateSoundCue(pcc, audioPackage, soundNodeWaveF.UIndex, node.LineStrRef, false);

                LinkLE1AudioToFaceFX(FXSetF, soundCueF, soundNodeWaveF.ObjectName.Name, node.LineStrRef);
            }
        }

        /// <summary>
        /// Link audio, passed by id, to a new line and add it to the FaceFX set editor control.
        /// </summary>
        /// <param name="faceFXAnimSet">FaceFX anim set to link to.</param>
        /// <param name="soundCue">SoundCue to link.</param>
        /// <param name="id">SoundNodeWave name.</param>
        /// <param name="strRefID">TLK StrRefID, for the FXA name.</param>
        private static void LinkLE1AudioToFaceFX(ExportEntry faceFXAnimSet, ExportEntry soundCue, string id, int strRefID)
        {
            if (faceFXAnimSet == null) { return; }

            FaceFXAnimSet faceFX = faceFXAnimSet.GetBinaryData<FaceFXAnimSet>();
            if (faceFX == null) { return; }

            string lineName = $"FXA_{strRefID}{(faceFXAnimSet.ObjectName.Name.EndsWith("_M", StringComparison.CurrentCultureIgnoreCase) ? "_M" : "")}";

            if (faceFX.Lines.Any(l => l.NameAsString == lineName)) { return; } // No need to add a new line

            FaceFXLine line = new()
            {
                NameIndex = faceFX.Names.Count,
                NameAsString = lineName,
                AnimationNames = new(),
                Points = new(),
                NumKeys = new(),
                FadeInTime = 0.16F,
                FadeOutTime = 0.22F,
                Path = soundCue.InstancedFullPath,
                ID = id,
                Index = faceFX.Lines.Count
            };

            faceFX.Names.Add(line.NameAsString);
            faceFX.Lines.Add(line);

            PropertyCollection props = faceFXAnimSet.GetProperties();
            ArrayProperty<ObjectProperty> referencedSoundCues = props.GetProp<ArrayProperty<ObjectProperty>>("ReferencedSoundCues")
                ?? new ArrayProperty<ObjectProperty>("ReferencedSoundCues");

            referencedSoundCues.Add(new ObjectProperty(soundCue.UIndex)); // ASSUMES: If the line wasn't in the binary, it also doesn't referenced the SoundCue
            faceFXAnimSet.WritePropertiesAndBinary(props, faceFX);
        }

        /// <summary>
        /// Generate a SoundNodeWave based on the conversation name and strRefID.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="parent">Parent package for the node</param>
        /// <param name="baseName">Base name for the node.</param>.
        /// <param name="strRefID">Node's StrRefID.</param>
        /// <param name="bioStreamingData">UIndex of the BioStreamingData.</param>
        /// <param name="isMale">Whether the SoundCue is for a male.</param>
        /// <returns>Generated SoundNodeWave.</returns>
        private static ExportEntry GenerateSoundNodeWave(IMEPackage pcc, IEntry parent, string baseName, int strRefID, int bioStreamingData, bool isMale)
        {
            string name = $"{baseName}:VO_{strRefID}{(isMale ? "_M" : "")}";
            PropertyCollection props = new()
            {
                new FloatProperty(1, "Volume"),
                new ObjectProperty(bioStreamingData, "BioStreamingData")
            };
            return CreateExport(pcc, new NameReference(name), "SoundNodeWave", parent, props, SoundNodeWave.Create());
        }

        /// <summary>
        /// Generate a SoundNodeWave based on the conversation name and strRefID.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="parent">Parent package for the node</param>
        /// <param name="soundNodeWave">SoundNodeWave to reference.</param>
        /// <param name="strRefID">Node's StrRefID.</param>
        /// <param name="isMale">Whether the SoundCue is for a male.</param>
        /// <returns>Generated SoundCue.</returns>
        private static ExportEntry GenerateSoundCue(IMEPackage pcc, IEntry parent, int soundNodeWave, int strRefID, bool isMale)
        {
            string name = $"VO_{strRefID}{(isMale ? "_M" : "")}";
            PropertyCollection props = new()
            {
                new NameProperty("DLG", "SoundGroup"),
                new ObjectProperty(soundNodeWave, "FirstNode")
            };
            return CreateExport(pcc, new NameReference(name), "SoundCue", parent, props, SoundCue.Create());
        }

        /// <summary>
        /// Get the base name of the bioConversation.
        /// </summary>
        /// <param name="bioConversation">BioConversation to get the name from.</param>
        /// <returns>Base conversation name. Empty string if couldn't get it.</returns>
        private static string GetBaseConversationName(ExportEntry bioConversation)
        {
            IMEPackage pcc = bioConversation.FileRef;
            string baseName = "";

            ObjectProperty m_oTlkFileSet = bioConversation.GetProperty<ObjectProperty>("m_oTlkFileSet");
            if (m_oTlkFileSet != null && m_oTlkFileSet.Value != 0)
            {
                ExportEntry tlkFileSet = pcc.GetUExport(m_oTlkFileSet.Value);
                // All Tlk file sets begin with TlkSet_, followed by the name they have in common with the package
                baseName = GetCommonPrefix(tlkFileSet.ObjectName.Name[7..], bioConversation.ObjectName.Name);
            }

            return baseName;
        }
        #endregion

        /// <summary>
        /// Assign ExportIDs and TLK refs to Autocontinues.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void FixAutocontinues(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedConv == null) { return; }

            int exportID = PromptForInt("Base ExportID for Autocontinues:",
                "Not a valid base. It must be positive integer", -1, "New ExportID base");
            if (exportID == -1) { return; }

            int tlkRef = PromptForInt("Base TLK Ref for Autocontinues:",
                "Not a valid base. It must be positive integer", -1, "New TLK Ref base");
            if (tlkRef == -1) { return; }

            ConversationExtended conversation = dew.SelectedConv;

            List<DialogueNodeExtended> nodes = new();
            nodes.AddRange(conversation.EntryList);
            nodes.AddRange(conversation.ReplyList);

            foreach (DialogueNodeExtended node in nodes)
            {
                if (node.ReplyType == EReplyTypes.REPLY_AUTOCONTINUE)
                {
                    if (node.ExportID < 0) { node.NodeProp.Properties.AddOrReplaceProp(new IntProperty(exportID++, "nExportID")); }

                    if (node.LineStrRef < 0) { node.NodeProp.Properties.AddOrReplaceProp(new StringRefProperty(tlkRef++, "srText")); }
                }
            }

            dew.RecreateNodesToProperties(dew.SelectedConv);
            dew.ForceRefreshCommand.Execute(null);

            MessageBox.Show("Assigned an ExportID and TLK Ref to all Autoncontinue nodes that were missing it.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Remove the given InterpTrack from all matching InterpGroups in the conversation's InterpDatas.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void BatchUnlistTrackFromGroupExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedConv == null) { return; }

            string groupName = PromptForStr("InterpGroup's name to unlist the track from:", "Invalid group name.");
            string trackName = PromptForStr("InterpTrack class name to unlist:", "Invalid track name.");

            List<DialogueNodeExtended> nodes = new();
            nodes.AddRange(dew.SelectedConv.EntryList);
            nodes.AddRange(dew.SelectedConv.ReplyList);

            int updateCount = 0;

            foreach (DialogueNodeExtended node in nodes)
            {
                if (node.Interpdata == null) { continue; }

                if (UnlistTrackFromGroup(dew.Pcc, groupName, trackName, node.Interpdata))
                {
                    updateCount++;
                }
            }

            MessageBox.Show($"Successfully unlisted {trackName} InterpTracks from {updateCount} {groupName} InterpGroups.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Wrapper for UnlistTrackFromGroup.
        /// </summary>
        /// <param name="dew">Current Dialogue Editor instance.</param>
        public static void UnlistTrackFromGroupExperiment(DialogueEditorWindow dew)
        {
            if (dew.Pcc == null || dew.SelectedDialogueNode == null) { return; }

            string groupName = PromptForStr("InterpGroup's name to unlist the track from:", "Invalid group name.");
            string trackName = PromptForStr("InterpTrack class name to unlist:", "Invalid track name.");

            DialogueNodeExtended node = dew.SelectedDialogueNode;

            if (node.Interpdata == null)
            {
                MessageBox.Show("The selected node does not contain an InterpData.", "Warning", MessageBoxButton.OK);
                return;
            }

            if (UnlistTrackFromGroup(dew.Pcc, groupName, trackName, node.Interpdata))
            {
                MessageBox.Show($"Successfully unlisted {trackName} InterpTrack from {groupName} InterpGroup.", "Success", MessageBoxButton.OK);
            }
            else
            {
                MessageBox.Show($"Could not find a matching {trackName} InterpTrack and/or {groupName} InterpGroup.", "Warning", MessageBoxButton.OK);
            
            }
        }

        /// <summary>
        /// Remove the given InterpTrack from the matching InterpGroups in Node's InterpData.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="groupName">InterpGroup to search on.</param>
        /// <param name="trackName">Class of track to unlist.</param>
        /// <param name="interpData">InterpData to search on.</param>
        /// <returns></returns>
        private static bool UnlistTrackFromGroup(IMEPackage pcc, string groupName, string trackName, ExportEntry interpData)
        {
            if (!MatineeHelper.TryGetInterpGroup(interpData, groupName, out ExportEntry interpGroup)) { return false; }

            ArrayProperty<ObjectProperty> interpTracks = interpGroup.GetProperty<ArrayProperty<ObjectProperty>>("InterpTracks");
            if (interpTracks == null) { return false; }

            interpGroup.WriteProperty(
                new ArrayProperty<ObjectProperty>(
                interpTracks.Where(trackRef =>
                {
                    if (!pcc.TryGetUExport(trackRef.Value, out ExportEntry track)) { return false; }
                    return (track.ClassName != trackName);
                }), "InterpTracks"));

            return true;
        }
    }
}
