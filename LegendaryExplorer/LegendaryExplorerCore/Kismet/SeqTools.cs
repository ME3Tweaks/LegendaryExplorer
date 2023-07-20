using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Kismet
{
    /// <summary>
    /// Static methods to obtain information on sequence objects, and perform common sequence editing operations
    /// </summary>
    /// <remarks>
    /// Ported from ME2Randomizer, and may have significant overlap with <see cref="KismetHelper"/>.
    /// This should be merged with KismetHelper and integrated with the toolset in the future, but doing so would break M3 and ME2R
    /// </remarks>
    // TODO: INTEGRATE BETTER WITH LEX
    public class SeqTools
    {
        /// <summary>
        /// Changes a single output link to a new target and commits the properties.
        /// </summary>
        /// <param name="export">Export to operate on</param>
        /// <param name="outputLinkIndex">The index of the item in 'OutputLinks'</param>
        /// <param name="linksIndex">The index of the item in the Links array</param>
        /// <param name="newTarget">The UIndex of the new target</param>
        public static void ChangeOutlink(ExportEntry export, int outputLinkIndex, int linksIndex, int newTarget)
        {
            var props = export.GetProperties();
            ChangeOutlink(props, outputLinkIndex, linksIndex, newTarget);
            export.WriteProperties(props);
        }

        /// <summary>
        /// Changes a single output link to a new target.
        /// </summary>
        /// <param name="props">The export properties list to operate on</param>
        /// <param name="outputLinkIndex">The index of the item in 'OutputLinks'</param>
        /// <param name="linksIndex">The index of the item in the Links array</param>
        /// <param name="newTarget">The UIndex of the new target</param>
        public static void ChangeOutlink(PropertyCollection props, int outputLinkIndex, int linksIndex, int newTarget)
        {
            props.GetProp<ArrayProperty<StructProperty>>("OutputLinks")[outputLinkIndex].GetProp<ArrayProperty<StructProperty>>("Links")[linksIndex].GetProp<ObjectProperty>("LinkedOp").Value = newTarget;
        }

        /// <summary>
        /// Removes a sequence element from the graph, by repointing incoming references to the ones referenced by outgoing items on this export.
        /// This is a very basic utility, only use it for items with one input and potentially multiple outputs.
        /// </summary>
        /// <param name="elementToSkip">The sequence object to skip</param>
        /// <param name="outboundLinkName">The name of the outbound link that should be attached to the preceding entry element, must have either this or the next argument</param>
        /// <param name="outboundLinkIdx">The 0-indexed outbound link that should be attached the preceding entry element, as if this one had fired that link.</param>
        public static void SkipSequenceElement(ExportEntry elementToSkip, string outboundLinkName = null, int outboundLinkIdx = -1)
        {
            if (outboundLinkIdx == -1 && outboundLinkName == null)
                throw new Exception(@"SkipSequenceElement() must have an outboundLinkName or an outboundLinkIdx!");

            if (outboundLinkIdx == -1)
            {
                var outboundLinkNames = KismetHelper.GetOutboundLinkNames(elementToSkip);
                outboundLinkIdx = outboundLinkNames.IndexOf(outboundLinkName);
            }


            // List of outbound link elements on the specified item we want to skip. These will be placed into the inbound item
            Debug.WriteLine($@"Attempting to skip {elementToSkip.UIndex} in {elementToSkip.FileRef.FilePath}");
            var outboundLinkLists = SeqTools.GetOutboundLinksOfNode(elementToSkip);
            var inboundToSkippedNode = SeqTools.FindOutboundConnectionsToNode(elementToSkip, SeqTools.GetAllSequenceElements(elementToSkip).OfType<ExportEntry>());

            var newTargetNodes = outboundLinkLists[outboundLinkIdx];

            foreach (var preNode in inboundToSkippedNode)
            {
                // For every node that links to the one we want to skip...
                var preNodeLinks = GetOutboundLinksOfNode(preNode);

                foreach (var ol in preNodeLinks)
                {
                    var numRemoved = ol.RemoveAll(x => x.LinkedOp == elementToSkip);

                    if (numRemoved > 0)
                    {
                        // At least one was removed. Repoint it
                        ol.AddRange(newTargetNodes);
                    }
                }

                WriteOutboundLinksToNode(preNode, preNodeLinks);
            }
        }

        /// <summary>
        /// Builds a jagged 2D list of OutboundLinks for each output link.
        /// </summary>
        /// <param name="node">Sequence object to get outbound links from</param>
        /// <returns>Outer list represents OutputLinks, inner lists represent the different sequence objects that link goes to</returns>
        public static List<List<OutboundLink>> GetOutboundLinksOfNode(ExportEntry node)
        {
            var outputLinksMapping = new List<List<OutboundLink>>();
            var outlinksProp = node.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outlinksProp != null)
            {
                int i = 0;
                foreach (var ol in outlinksProp)
                {
                    List<OutboundLink> oLinks = new List<OutboundLink>();
                    outputLinksMapping.Add(oLinks);

                    var links = ol.GetProp<ArrayProperty<StructProperty>>("Links");
                    if (links != null)
                    {
                        foreach (var l in links)
                        {
                            oLinks.Add(OutboundLink.FromStruct(l, node.FileRef));
                        }
                    }

                    i++;
                }
            }

            return outputLinksMapping;
        }

        /// <summary>
        /// Writes a list of outbound links to a sequence node. Note that this cannot add output link points (like an additional output param), but only existing connections.
        /// </summary>
        /// <param name="node">Sequence object node to write outbound links to</param>
        /// <param name="linkSet">Link set to write to the export</param>
        public static void WriteOutboundLinksToNode(ExportEntry node, List<List<OutboundLink>> linkSet)
        {
            var properties = node.GetProperties();
            WriteOutboundLinksToProperties(linkSet, properties);
            node.WriteProperties(properties);
        }

        /// <summary>
        /// Writes a set of outbound links to a property collection. This cannot be used to add output link points, only to overwrite the links of existing outputs.
        /// </summary>
        /// <remarks>Returns early if <see cref="linkSet"/> is not of correct length. 'Links' ArrayProperty must already exist in collection.</remarks>
        /// <param name="linkSet">Link set to write to properties</param>
        /// <param name="props">Properties to write links to</param>
        public static void WriteOutboundLinksToProperties(List<List<OutboundLink>> linkSet, PropertyCollection props)
        {
            var outlinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
            if (linkSet.Count != outlinksProp.Count)
            {
                Debug.WriteLine("Sets are out of sync for WriteOutboundLinksToProperties()! You can't add a new outbound named link using this method.");
                return; // Sets are not compatible with this code
            }

            for (int i = 0; i < linkSet.Count; i++)
            {
                var oldL = outlinksProp[i].GetProp<ArrayProperty<StructProperty>>("Links");
                var newL = linkSet[i];
                oldL.ReplaceAll(newL.Select(x => x.GenerateStruct()));
            }
        }

        /// <summary>
        /// Represents an outbound link from a sequence object
        /// </summary>
        public class OutboundLink
        {
            /// <summary>The sequence object that this links to</summary>
            public IEntry LinkedOp { get; set; }
            /// <summary>The InputLinkIdx property of this link</summary>
            public int InputLinkIdx { get; set; }

            /// <summary>
            /// Generates a SeqOpInputOutputLink StructProperty from this OutboundLink
            /// </summary>
            /// <returns>Created StructProperty</returns>
            public StructProperty GenerateStruct()
            {
                return new StructProperty("SeqOpOutputInputLink", false,
                    new ObjectProperty(LinkedOp.UIndex, "LinkedOp"),
                    new IntProperty(InputLinkIdx, "InputLInkIdx"),
                    new NoneProperty());
            }

            /// <summary>
            /// Factory method to create an <see cref="OutboundLink"/> from a SeqOpOutputInputLink StructProperty
            /// </summary>
            /// <param name="sp">SeqOpOutputInputLink StructProperty</param>
            /// <param name="package">Package file that contains this sequence</param>
            /// <returns>New OutboundLink</returns>
            public static OutboundLink FromStruct(StructProperty sp, IMEPackage package)
            {
                return new OutboundLink()
                {
                    LinkedOp = sp.GetProp<ObjectProperty>("LinkedOp")?.ResolveToEntry(package),
                    InputLinkIdx = sp.GetProp<IntProperty>("InputLinkIdx")
                };
            }

            /// <summary>
            /// Factory method to create an OutboundLink
            /// </summary>
            /// <param name="exportEntry">Sequence object to create link to</param>
            /// <param name="inputLinkIdx">Link index</param>
            /// <returns>New OutboundLink</returns>
            public static OutboundLink FromTargetExport(ExportEntry exportEntry, int inputLinkIdx)
            {
                //HB 12/14/21: Why is this not just a constructor?
                return new OutboundLink()
                {
                    LinkedOp = exportEntry,
                    InputLinkIdx = inputLinkIdx
                };
            }
        }

        /// <summary>
        /// Finds sequence objects with outbound connections that come to this node
        /// </summary>
        /// <param name="node">Node to find outbound connections to</param>
        /// <param name="sequenceElements">Sequence objects to search for connections</param>
        /// <returns>List of any sequence objects that link to this node</returns>
        public static List<ExportEntry> FindOutboundConnectionsToNode(ExportEntry node, IEnumerable<ExportEntry> sequenceElements, List<int> linkIdxsToMatchOn = null, List<string> filteredInputNames = null)
        {
            List<ExportEntry> referencingNodes = new List<ExportEntry>();

            foreach (var seqObj in sequenceElements)
            {
                if (seqObj == node) continue; // Skip node pointing to itself
                var linkSet = GetOutboundLinksOfNode(seqObj);
                var matchingLinks = linkSet.Where(x => x.Any(y => y.LinkedOp != null && y.LinkedOp.UIndex == node.UIndex)).ToList();
                if (filteredInputNames == null && linkIdxsToMatchOn == null && matchingLinks.Any())
                {
                    referencingNodes.Add(seqObj);
                    continue;
                }

                // Check if the name matches the filters
                // Determine if it comes in on our named input idx
                if (filteredInputNames != null)
                {
                    // Build the list of allowed input idxs
                    // This is not that reliable, as the inputs will be defined on the class, not the instance
                    // oops
                    var linkInputNamesArray = node.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
                    linkIdxsToMatchOn = new List<int>();
                    for (int i = 0; i < linkInputNamesArray.Count; i++)
                    {
                        if (filteredInputNames.Contains(linkInputNamesArray[i].GetProp<NameProperty>("LinkDesc").Value.Instanced))
                        {
                            // Match on this input idx
                            linkIdxsToMatchOn.Add(i);
                        }
                    }
                }

                if (matchingLinks.Any(x => x.Any(y => y.LinkedOp == node && linkIdxsToMatchOn.Contains(y.InputLinkIdx))))
                {
                    // We have an input on a filtered input we want
                    referencingNodes.Add(seqObj);
                    continue; // Here just in case code is added later
                }
            }

            return referencingNodes.Distinct().ToList();
        }


        /// <summary>
        /// Finds sequence objects with variable connections that come to this node
        /// </summary>
        /// <param name="node">Sequence variable to find connections to</param>
        /// <param name="sequenceElements">Sequence objects to search for connections</param>
        /// <returns>List of any sequence objects that link to this node</returns>
        public static List<ExportEntry> FindVariableConnectionsToNode(ExportEntry node, List<ExportEntry> sequenceElements)
        {
            List<ExportEntry> referencingNodes = new List<ExportEntry>();

            foreach (var seqObj in sequenceElements)
            {
                if (seqObj == node) continue; // Skip node pointing to itself
                var linkSet = GetVariableLinksOfNode(seqObj);
                if (linkSet.Any(x => x.LinkedNodes.Any(y => y == node)))
                {
                    referencingNodes.Add(seqObj);
                }
            }

            return referencingNodes.Distinct().ToList();
        }

        /// <summary>
        /// Gets a list of all sequence elements that are referenced by this sequence's SequenceObjects property
        /// If the passed in object is not a Sequence, the parent sequence is used. Returns null if there is no parent sequence.
        /// </summary>
        /// <param name="export">Export to get referenced elements for</param>
        /// <returns>List of referenced sequence elements</returns>
        public static List<IEntry> GetAllSequenceElements(ExportEntry export)
        {
            if (export.ClassName == "Sequence")
            {
                var seqObjs = export.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                if (seqObjs != null)
                {
                    return seqObjs.Where(x => export.FileRef.IsEntry(x.Value)).Select(x => x.ResolveToEntry(export.FileRef)).ToList();
                }
            }
            else
            {
                // Pull from parent sequence.
                var pSeqObj = export.GetProperty<ObjectProperty>("ParentSequence");
                if (pSeqObj != null && pSeqObj.ResolveToEntry(export.FileRef) is ExportEntry pSeq && pSeq.ClassName == "Sequence")
                {
                    return GetAllSequenceElements(pSeq);
                }
            }

            return null;
        }

        /// <summary>
        /// Basic description of a single VarLink (bottom of kismet action - this includes all links)
        /// </summary>
        [DebuggerDisplay("VarLink {LinkDesc}, ExpectedType: {ExpectedTypeName}")]
        public class VarLinkInfo
        {
            /// <summary>LinkDesc property value</summary>
            public string LinkDesc { get; set; }
            /// <summary>PropertyName property value</summary>
            public string PropertyName { get; set; }
            /// <summary>Expected type of variable</summary>
            public IEntry ExpectedType { get; set; }
            /// <summary>Expected type name of variable</summary>
            public string ExpectedTypeName => ExpectedType.ObjectName;
            /// <summary>Sequence objects that are linked to this var link</summary>
            public List<IEntry> LinkedNodes { get; set; }

            /// <summary>
            /// Factory method to create a <see cref="VarLinkInfo"/> from a SeqVarLink struct
            /// </summary>
            /// <param name="sp">SeqVarLink struct property</param>
            /// <param name="package">Package containing sequence object</param>
            /// <returns>New VarLinkInfo</returns>
            public static VarLinkInfo FromStruct(StructProperty sp, IMEPackage package)
            {
                return new VarLinkInfo()
                {
                    LinkDesc = sp.GetProp<StrProperty>("LinkDesc"),
                    PropertyName = sp.GetProp<NameProperty>("PropertyName")?.Value,
                    ExpectedType = sp.GetProp<ObjectProperty>("ExpectedType").ResolveToEntry(package),
                    LinkedNodes = sp.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables")?.Select(x => x.ResolveToEntry(package)).ToList() ?? new List<IEntry>()
                };
            }
        }

        /// <summary>
        /// Gets the list of VarLinks that can be attached to by the specified sequence object export.
        /// </summary>
        /// <remarks>You cannot ADD new varlinks this way as the serialization does not include all types.</remarks>
        /// <param name="export">Export to get variable links for</param>
        /// <returns>List of variable link infos</returns>
        public static List<VarLinkInfo> GetVariableLinksOfNode(ExportEntry export)
        {
            var props = export.GetProperties();
            return GetVariableLinks(props, export.FileRef);
        }

        /// <summary>
        /// Gets the list of variable links from a collection of properties. If there is no VariableLinks property, this returns an empty list.
        /// </summary>
        /// <param name="props">Properties to get variable links from</param>
        /// <param name="pcc">Package containing the property collection</param>
        /// <returns>List of any variable links</returns>
        public static List<VarLinkInfo> GetVariableLinks(PropertyCollection props, IMEPackage pcc)
        {
            var varLinks = new List<VarLinkInfo>();
            var variableLinks = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
            if (variableLinks != null)
            {
                foreach (var vl in variableLinks)
                {
                    varLinks.Add(VarLinkInfo.FromStruct(vl, pcc));
                }
            }

            return varLinks;
        }

        /// <summary>
        /// Writes the list of variable links to the node. Only the linked objects are written.
        /// The list MUST be in the same order and be the same length as the current variable links on the export.
        /// </summary>
        /// <remarks>This can't be used to add any new variable link points.</remarks>
        /// <param name="export">Export to write links to</param>
        /// <param name="varLinks">Variable links to write</param>
        public static void WriteVariableLinksToNode(ExportEntry export, List<VarLinkInfo> varLinks)
        {
            var properties = export.GetProperties();
            WriteVariableLinksToProperties(varLinks, properties);
            export.WriteProperties(properties);
        }

        /// <summary>
        /// Writes the list of variable links to the given property collection. Only writes the linked variables.
        /// The list MUST be in the same order and be the same length as the current variable links in the collection.
        /// </summary>
        /// <remarks>This can't be used to add any new variable link points. LinkedVariables property must already exist.</remarks>
        /// <param name="varLinks">Variable links to write</param>
        /// <param name="props">Properties to write links to</param>
        public static void WriteVariableLinksToProperties(List<VarLinkInfo> varLinks, PropertyCollection props)
        {
            var variableLinks = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
            if (variableLinks != null && varLinks.Count == variableLinks.Count)
            {
                for (int i = 0; i < variableLinks.Count; i++)
                {
                    var linkedVarList = variableLinks[i].GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                    linkedVarList?.ReplaceAll(varLinks[i].LinkedNodes.Select(x => new ObjectProperty(x)));
                }
            }
        }

        /// <summary>
        /// Gets the containing sequence of the specified export.
        /// Performed by looking for ParentSequence object property.
        /// </summary>
        /// <remarks>Use the <see cref="recurseUp"/> parameter to get the top level sequence.</remarks>
        /// <param name="export">Export to get containing sequence of</param>
        /// <param name="recurseUp">If true, will continue getting parent sequences until it reaches the top of the chain.</param>
        /// <returns>Parent sequence export</returns>
        public static ExportEntry GetParentSequence(ExportEntry export, bool recurseUp = false)
        {
            var result = export?.GetProperty<ObjectProperty>("ParentSequence")?.ResolveToEntry(export.FileRef) as ExportEntry;
            while (recurseUp && result != null && result.ClassName != "Sequence")
            {
                result = result.GetProperty<ObjectProperty>("ParentSequence")?.ResolveToEntry(export.FileRef) as ExportEntry;
            }

            return result;
        }

        /// <summary>
        /// Writes the Originator property on a sequence object
        /// </summary>
        /// <param name="export">Sequence object to write property on</param>
        /// <param name="originator">Originator entry to write</param>
        public static void WriteOriginator(ExportEntry export, IEntry originator)
        {
            export.WriteProperty(new ObjectProperty(originator.UIndex, "Originator"));
        }

        /// <summary>
        /// Writes the ObjValue property on a sequence object
        /// </summary>
        /// <param name="export">Sequence object to write property on</param>
        /// <param name="objValue">ObjValue entry to write</param>
        public static void WriteObjValue(ExportEntry export, IEntry objValue)
        {
            export.WriteProperty(new ObjectProperty(objValue.UIndex, "ObjValue"));
        }

#if DEBUG
        /// <summary>
        /// DEBUG: Writes info about a series of <see cref="VarLinkInfo"/>s to the debug console
        /// </summary>
        /// <param name="seqLinks">Variable links to write</param>
        public static void PrintVarLinkInfo(List<VarLinkInfo> seqLinks)
        {
            foreach (var link in seqLinks)
            {
                Debug.WriteLine($"VarLink {link.LinkDesc}, expected type: {link.ExpectedTypeName}");
                foreach (var linkedNode in link.LinkedNodes.OfType<ExportEntry>())
                {
                    var findTag = linkedNode.GetProperty<StrProperty>("m_sObjectTagToFind");
                    var objValue = linkedNode.GetProperty<ObjectProperty>("ObjValue");
                    Debug.WriteLine($"   {linkedNode.UIndex} {linkedNode.ObjectName.Instanced} {findTag?.Value} {objValue?.ResolveToEntry(linkedNode.FileRef).ObjectName.Instanced}");
                }
            }
        }
#endif
    }
}
