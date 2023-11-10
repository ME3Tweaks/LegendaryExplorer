using System;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorerCore.Kismet
{
    /// <summary>
    /// Static methods to perform common sequence editing operations
    /// </summary>
    public static class KismetHelper
    {
        #region Links
        /// <summary>
        /// Builds a jagged 2D list of OutboundLinks for each output link.
        /// </summary>
        /// <param name="node">Sequence object to get outbound links from</param>
        /// <returns>Outer list represents OutputLinks, inner lists represent the different sequence objects that link goes to</returns>
        public static List<List<OutputLink>> GetOutputLinksOfNode(ExportEntry node)
        {
            var outputLinksMapping = new List<List<OutputLink>>();
            var outlinksProp = node.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outlinksProp != null)
            {
                int i = 0;
                foreach (var ol in outlinksProp)
                {
                    List<OutputLink> oLinks = new List<OutputLink>();
                    outputLinksMapping.Add(oLinks);

                    var links = ol.GetProp<ArrayProperty<StructProperty>>("Links");
                    if (links != null)
                    {
                        foreach (var l in links)
                        {
                            oLinks.Add(OutputLink.FromStruct(l, node.FileRef));
                        }
                    }

                    i++;
                }
            }

            return outputLinksMapping;
        }
        
        /// <summary>
        /// Gets a list of Outlink LinkDesc names, in order.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static List<string> GetOutputLinkNames(ExportEntry node)
        {
            var outlinkNames = new List<string>();
            var outlinksProp = node.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outlinksProp != null)
            {
                foreach (var ol in outlinksProp)
                {
                    outlinkNames.Add(ol.GetProp<StrProperty>("LinkDesc"));
                }

            }
            return outlinkNames;
        }
        
        /// <summary>
        /// Adds an output link from one sequence object to another.
        /// Will not create a new output link, will only add to an existing output
        /// </summary>
        /// <param name="source">Source sequence export</param>
        /// <param name="outLinkDescription">Description of existing link</param>
        /// <param name="destExport">Export to create new link to</param>
        /// <param name="inputIndex">InputLinkIdx property value of the new link</param>
        public static void CreateOutputLink(ExportEntry source, string outLinkDescription, ExportEntry destExport, int inputIndex = 0)
        {
            if (source.GetProperty<ArrayProperty<StructProperty>>("OutputLinks") is { } outLinksProp)
            {
                foreach (var prop in outLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == outLinkDescription)
                    {
                        var linksProp = prop.GetProp<ArrayProperty<StructProperty>>("Links");
                        linksProp.Add(new StructProperty("SeqOpOutputInputLink", false,
                                                         new ObjectProperty(destExport, "LinkedOp"),
                                                         new IntProperty(inputIndex, "InputLinkIdx")));
                        source.WriteProperty(outLinksProp);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new output link from one sequence object to another,
        /// does not overwrite or add to any existing output link. If the destExport is null, only a new output link is made, no connection is created.
        /// </summary>
        /// <param name="source">Source sequence export</param>
        /// <param name="outLinkDescription">Description of new link</param>
        /// <param name="destExport">Export to create new link to</param>
        /// <param name="inputIndex">InputLinkIdx property value of new link</param>
        public static void CreateNewOutputLink(ExportEntry source, string outLinkDescription, ExportEntry destExport,
            int inputIndex = 0)
        {
            PropertyCollection opOutputLinkProperties = null;
            ArrayProperty<StructProperty> outLinksProp =
                source.GetProperty<ArrayProperty<StructProperty>>("OutputLinks") ??
                new ArrayProperty<StructProperty>("OutputLinks");
            if (destExport != null)
            {
                // Create new input and output
                var inputLink = new StructProperty("SeqOpOutputInputLink", false,
                    new ObjectProperty(destExport, "LinkedOp"),
                    new IntProperty(inputIndex, "InputLinkIdx"));

                opOutputLinkProperties = new PropertyCollection
                {
                    new StrProperty(outLinkDescription, "LinkDesc"),
                    new BoolProperty(false, "bHasImpulse"),
                    new BoolProperty(false, "bDisabled"),
                    new NameProperty("None", "LinkAction"),
                    new ObjectProperty(0, "LinkedOp"),
                    new FloatProperty(0, "ActivateDelay"),
                    new ArrayProperty<StructProperty>(new List<StructProperty>() { inputLink }, "Links")
                };
            }
            else
            {
                // Just create a new output with no links
                opOutputLinkProperties = new PropertyCollection
                {
                    new StrProperty(outLinkDescription, "LinkDesc"),
                    new BoolProperty(false, "bHasImpulse"),
                    new BoolProperty(false, "bDisabled"),
                    new NameProperty("None", "LinkAction"),
                    new ObjectProperty(0, "LinkedOp"),
                    new FloatProperty(0, "ActivateDelay"),
                    new ArrayProperty<StructProperty>(new List<StructProperty>(), "Links")
                };
            }

            outLinksProp.Add(new StructProperty("SeqOpOutputLink", opOutputLinkProperties));


            source.WriteProperty(outLinksProp);
        }
        
        /// <summary>
        /// Changes a single output link to a new target and commits the properties.
        /// </summary>
        /// <param name="export">Export to operate on</param>
        /// <param name="outputLinkIndex">The index of the item in 'OutputLinks'</param>
        /// <param name="linksIndex">The index of the item in the Links array</param>
        /// <param name="newTarget">The UIndex of the new target</param>
        public static void ChangeOutputLink(ExportEntry export, int outputLinkIndex, int linksIndex, int newTarget)
        {
            var props = export.GetProperties();
            ChangeOutputLink(props, outputLinkIndex, linksIndex, newTarget);
            export.WriteProperties(props);
        }

        /// <summary>
        /// Changes a single output link to a new target.
        /// </summary>
        /// <param name="props">The export properties list to operate on</param>
        /// <param name="outputLinkIndex">The index of the item in 'OutputLinks'</param>
        /// <param name="linksIndex">The index of the item in the Links array</param>
        /// <param name="newTarget">The UIndex of the new target</param>
        public static void ChangeOutputLink(PropertyCollection props, int outputLinkIndex, int linksIndex, int newTarget)
        {
            props.GetProp<ArrayProperty<StructProperty>>("OutputLinks")[outputLinkIndex].GetProp<ArrayProperty<StructProperty>>("Links")[linksIndex].GetProp<ObjectProperty>("LinkedOp").Value = newTarget;
        }
        
        /// <summary>
        /// Writes a list of outbound links to a sequence node. Note that this cannot add output link points (like an additional output param), but only existing connections.
        /// </summary>
        /// <param name="node">Sequence object node to write outbound links to</param>
        /// <param name="linkSet">Link set to write to the export</param>
        public static void WriteOutputLinksToNode(ExportEntry node, List<List<OutputLink>> linkSet)
        {
            var properties = node.GetProperties();
            WriteOutputLinksToProperties(linkSet, properties);
            node.WriteProperties(properties);
        }
        
        /// <summary>
        /// Writes a set of output links to a property collection. This cannot be used to add output link points, only to overwrite the links of existing outputs.
        /// </summary>
        /// <remarks>Returns early if <see cref="linkSet"/> is not of correct length. 'Links' ArrayProperty must already exist in collection.</remarks>
        /// <param name="linkSet">Link set to write to properties</param>
        /// <param name="props">Properties to write links to</param>
        public static void WriteOutputLinksToProperties(List<List<OutputLink>> linkSet, PropertyCollection props)
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
        /// Adds a variable link from a source sequence object to a variable.
        /// This will not create a new variable link, only adding a new variable to an existing link.
        /// </summary>
        /// <param name="source">Source sequence object, this is the SeqAct the link will be added to</param>
        /// <param name="linkDescription">Variable link description</param>
        /// <param name="dest">Variable sequence object</param>
        public static void CreateVariableLink(ExportEntry source, string linkDescription, ExportEntry dest)
        {
            if (source.GetProperty<ArrayProperty<StructProperty>>("VariableLinks") is { } varLinksProp)
            {
                foreach (var prop in varLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDescription)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Add(new ObjectProperty(dest));
                        source.WriteProperty(varLinksProp);
                    }
                }
            }
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
        /// Removes variable links that have no defined values. Can be dangerous if the class is not designed to lookup by name (will break Idx based classes)
        /// </summary>
        /// <param name="source">Export to remove variable links from</param>
        public static void TrimVariableLinks(ExportEntry source)
        {
            if (source.GetProperty<ArrayProperty<StructProperty>>("VariableLinks") is { } varLinksProp)
            {
                for (int i = varLinksProp.Count - 1; i >= 0; i--)
                {
                    var prop = varLinksProp[i];
                    if (prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Count == 0)
                    {
                        // Trim
                        varLinksProp.RemoveAt(i);
                    }
                }
                source.WriteProperty(varLinksProp);
            }
        }

        /// <summary>
        /// Adds an event link from a source sequence object to an event.
        /// This will not create a new event link, only adding an event to an existing link.
        /// </summary>
        /// <param name="src">Source sequence object, this is the SeqAct the link will be added to</param>
        /// <param name="linkDescription">Event link description</param>
        /// <param name="dest">Event sequence object</param>
        public static void CreateEventLink(ExportEntry src, string linkDescription, ExportEntry dest)
        {
            if (src.GetProperty<ArrayProperty<StructProperty>>("EventLinks") is { } eventLinksProp)
            {
                foreach (var prop in eventLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDescription)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents").Add(new ObjectProperty(dest));
                        src.WriteProperty(eventLinksProp);
                    }
                }
            }
        }

        /// <summary>
        /// Removes all output links from the given sequence object.
        /// This leaves all link slots intact, it just removes the actual links to other sequence objects.
        /// </summary>
        /// <param name="export">Sequence object to remove links from</param>
        /// <param name="replaceWithBlank">Use if you want to ensure no output links are available even from superclass</param>
        public static void RemoveOutputLinks(ExportEntry export, bool replaceWithBlank = false)
        {
            if (replaceWithBlank)
            {
                export.WriteProperty(new ArrayProperty<StructProperty>("OutputLinks"));
            }
            else
            {
                RemoveAllLinks(export, true, false, false);
            }
        }

        /// <summary>
        /// Removes all variable links from the given sequence object.
        /// </summary>
        /// <param name="export">Sequence object to remove links from</param>
        public static void RemoveVariableLinks(ExportEntry export)
        {
            RemoveAllLinks(export, false, true, false);
        }

        /// <summary>
        /// Removes all event links from the given sequence object.
        /// </summary>
        /// <param name="export">Sequence object to remove links from</param>
        public static void RemoveEventLinks(ExportEntry export)
        {
            RemoveAllLinks(export, false, false, true);
        }

        /// <summary>
        /// Removes all links to other sequence objects from the given object.
        /// Use the optional parameters to specify which types of links can be removed.
        /// </summary>
        /// <param name="export">Sequence object to remove all links from</param>
        /// <param name="outlinks">If true, output links will be removed. Default: True</param>
        /// <param name="variablelinks">If true, variable links will be removed. Default: True</param>
        /// <param name="eventlinks">If true, event links will be removed. Default: True</param>
        public static void RemoveAllLinks(ExportEntry export, bool outlinks = true, bool variablelinks = true, bool eventlinks = true)
        {
            var props = export.GetProperties();
            if (outlinks)
            {
                var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outLinksProp != null)
                {
                    foreach (var prop in outLinksProp)
                    {
                        prop.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
                    }
                }
            }

            if (variablelinks)
            {
                var varLinksProp = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
                if (varLinksProp != null)
                {
                    foreach (var prop in varLinksProp)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Clear();
                    }
                }
            }

            if (eventlinks)
            {
                var eventLinksProp = props.GetProp<ArrayProperty<StructProperty>>("EventLinks");
                if (eventLinksProp != null)
                {
                    foreach (var prop in eventLinksProp)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents").Clear();
                    }
                }
            }

            export.WriteProperties(props);
        }
        

        #endregion

        /// <summary>
        /// Gets a list of non-null objects in the sequence. Returns IEntry, as some sequences are referenced as imports.
        /// This only gets the immediate children objects of the given sequence.
        /// </summary>
        /// <param name="sequence">Sequence export to get elements from</param>
        /// <returns>List of IEntrys in the sequence</returns>
        //TODO: Merge this with GetAllSequenceElements - they both do the same thing with slightly different parameters. Leaving here as other projects may depend on behavior.
        public static List<IEntry> GetSequenceObjects(ExportEntry sequence)
        {
            var objects = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (objects == null)
                return new List<IEntry>();

            return objects.Where(x => x.Value != 0).Select(x => x.ResolveToEntry(sequence.FileRef)).ToList();
        }
        
        /// <summary>
        /// Gets a list of all sequence elements that are referenced by this sequence's SequenceObjects property
        /// If the passed in object is not a Sequence, the parent sequence is used. Returns null if there is no parent sequence.
        /// </summary>
        /// <param name="export">Export to get referenced elements for</param>
        /// <returns>List of referenced sequence elements</returns>
        public static List<IEntry> GetAllSequenceElements(ExportEntry export)
        {
            if (export.ClassName is "Sequence" or "PrefabSequence")
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
        /// Adds a sequence object export to the given sequence, handling the ParentSequence and SequenceObjects properties.
        /// </summary>
        /// <remarks>This method will change the parent of the new export to the parent sequence.</remarks>
        /// <param name="newObject">Sequence object to add to a sequence</param>
        /// <param name="sequenceExport">Sequence to add it to</param>
        /// <param name="removeLinks">If true, all links will be removed from the new object after adding</param>
        public static void AddObjectToSequence(ExportEntry newObject, ExportEntry sequenceExport, bool removeLinks = false)
        {
            if (sequenceExport.ClassName is not "SequenceReference")
            {
                ArrayProperty<ObjectProperty> seqObjs = sequenceExport.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects") ?? new ArrayProperty<ObjectProperty>("SequenceObjects");
                // Only add if not already in the list
                if (seqObjs.All(x => x.Value != newObject.UIndex))
                {
                    seqObjs.Add(new ObjectProperty(newObject));
                    sequenceExport.WriteProperty(seqObjs);
                }
            }

            PropertyCollection newObjectProps = newObject.GetProperties();
            newObjectProps.AddOrReplaceProp(new ObjectProperty(sequenceExport, "ParentSequence"));
            newObjectProps.RemoveNamedProperty("ObjPosX");
            newObjectProps.RemoveNamedProperty("ObjPosY");
            newObject.WriteProperties(newObjectProps);
            if (removeLinks)
            {
                RemoveAllLinks(newObject);
            }
            newObject.Parent = sequenceExport;
        }

        /// <summary>
        /// Adds multiple sequence objects to the given sequence.
        /// </summary>
        /// <remarks>Handles the ParentSequence and SequenceObjects properties, and sets the parent of all added objects.</remarks>
        /// <param name="sequenceExport">Sequence export to add sequence objects to</param>
        /// <param name="removeLinks">If true, all links will be removed from the new objects after adding</param>
        /// <param name="exports">Sequence objects to add to the sequence</param>
        public static void AddObjectsToSequence(ExportEntry sequenceExport, bool removeLinks, params ExportEntry[] exports)
        {
            if (sequenceExport.ClassName is not "SequenceReference")
            {
                ArrayProperty<ObjectProperty> seqObjs = sequenceExport.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects") ?? new ArrayProperty<ObjectProperty>("SequenceObjects");
                foreach (var export in exports)
                {
                    // Only add if not already in the list
                    if (seqObjs.All(x => x.Value != export.UIndex))
                    {
                        seqObjs.Add(new ObjectProperty(export));
                        sequenceExport.WriteProperty(seqObjs);
                    }
                }
                sequenceExport.WriteProperty(seqObjs);
            }

            foreach (var export in exports)
            {
                PropertyCollection newObjectProps = export.GetProperties();
                newObjectProps.AddOrReplaceProp(new ObjectProperty(sequenceExport, "ParentSequence"));
                newObjectProps.RemoveNamedProperty("ObjPosX");
                newObjectProps.RemoveNamedProperty("ObjPosY");
                export.WriteProperties(newObjectProps);
                if (removeLinks)
                {
                    RemoveAllLinks(export);
                }

                export.Parent = sequenceExport;
            }
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
                var outboundLinkNames = GetOutputLinkNames(elementToSkip);
                outboundLinkIdx = outboundLinkNames.IndexOf(outboundLinkName);
            }


            // List of outbound link elements on the specified item we want to skip. These will be placed into the inbound item
            Debug.WriteLine($@"Attempting to skip {elementToSkip.UIndex} in {elementToSkip.FileRef.FilePath}");
            var outboundLinkLists = GetOutputLinksOfNode(elementToSkip);
            var inboundToSkippedNode = FindOutputConnectionsToNode(elementToSkip, GetAllSequenceElements(elementToSkip).OfType<ExportEntry>());

            var newTargetNodes = outboundLinkLists[outboundLinkIdx];

            foreach (var preNode in inboundToSkippedNode)
            {
                // For every node that links to the one we want to skip...
                var preNodeLinks = GetOutputLinksOfNode(preNode);

                foreach (var ol in preNodeLinks)
                {
                    var numRemoved = ol.RemoveAll(x => x.LinkedOp == elementToSkip);

                    if (numRemoved > 0)
                    {
                        // At least one was removed. Repoint it
                        ol.AddRange(newTargetNodes);
                    }
                }
                WriteOutputLinksToNode(preNode, preNodeLinks);
            }
        }

        /// <summary>
        /// Gets a list of link names for the outbound links of the node
        /// </summary>
        /// <returns>A list of LinkDesc values</returns>
        [Obsolete("Duplication: Use GetOutputLinkNames instead")]
        public static List<string> GetOutboundLinkNames(ExportEntry export)
        {
            return GetOutputLinkNames(export);
        }

        /// <summary>
        /// Sets the m_aObjComment for a sequence object
        /// </summary>
        /// <param name="export">Sequence object to set comments for</param>
        /// <param name="comments">Object comment lines</param>
        public static void SetComment(ExportEntry export, IEnumerable<string> comments)
        {
            export.WriteProperty(new ArrayProperty<StrProperty>(comments.Select(c => new StrProperty(c)), "m_aObjComment"));
        }

        /// <summary>
        /// Gets the first value in the m_aObjComment array for a sequence object. If empty or no comment, this returns null
        /// </summary>
        /// <param name="export">Sequence object to set comments for</param>
        public static string GetComment(ExportEntry export)
        {
            var m_aObjComment = export.GetProperty<ArrayProperty<StrProperty>>("m_aObjComment");
            if (m_aObjComment == null || m_aObjComment.Count == 0) return null;
            return m_aObjComment[0];
        }

        /// <summary>
        /// Sets the m_aObjComment for a sequence object
        /// </summary>
        /// <param name="export">Sequence object to set comments for</param>
        /// <param name="comment">Object comment</param>
        public static void SetComment(ExportEntry export, string comment)
        {
            SetComment(export, new List<string>() { comment });
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

        /// <summary>
        /// Clones a sequence object and places it into the given sequence.
        /// </summary>
        /// <param name="itemToClone"></param>
        /// <param name="sequence"></param>
        /// <param name="topLevel"></param>
        /// <param name="incrementIndex"></param>
        /// <param name="cloneChildren">Whether or not to clone any children objects as well</param>
        /// <returns></returns>
        public static ExportEntry CloneObject(ExportEntry itemToClone, ExportEntry sequence = null, bool topLevel = true, bool incrementIndex = true, bool cloneChildren = false)
        {
            //SeqVar_External needs to have the same index to work properly
            ExportEntry exp = cloneChildren ? EntryCloner.CloneTree(itemToClone) : EntryCloner.CloneEntry(itemToClone, incrementIndex: incrementIndex && itemToClone.ClassName != "SeqVar_External");
            AddObjectToSequence(exp, sequence ?? GetParentSequence(itemToClone), topLevel);
            CloneSequence(exp);
            return exp;
        }

        /// <summary>
        /// Clones an entire Kismet sequence
        /// </summary>
        /// <param name="sequence"></param>
        public static void CloneSequence(ExportEntry sequence)
        {
            IMEPackage pcc = sequence.FileRef;
            if (sequence.ClassName == "Sequence")
            {
                //sequence names need to be unique I think?
                sequence.ObjectName = pcc.GetNextIndexedName(sequence.ObjectName.Name);

                var seqObjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                if (seqObjs == null || seqObjs.Count == 0)
                {
                    return;
                }

                //store original list of sequence objects;
                List<int> oldObjectUindices = seqObjs.Select(x => x.Value).ToList();

                //clear original sequence objects
                seqObjs.Clear();
                sequence.WriteProperty(seqObjs);

                //clone all children
                foreach (var obj in oldObjectUindices)
                {
                    CloneObject(pcc.GetUExport(obj), sequence, false, false);
                }

                //re-point children's links to new objects
                seqObjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                foreach (var seqObj in seqObjs)
                {
                    ExportEntry obj = pcc.GetUExport(seqObj.Value);
                    var props = obj.GetProperties();
                    var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                    if (outLinksProp != null)
                    {
                        foreach (var outLinkStruct in outLinksProp)
                        {
                            var links = outLinkStruct.GetProp<ArrayProperty<StructProperty>>("Links");
                            foreach (var link in links)
                            {
                                var linkedOp = link.GetProp<ObjectProperty>("LinkedOp");
                                linkedOp.Value = seqObjs[oldObjectUindices.IndexOf(linkedOp.Value)].Value;
                            }
                        }
                    }

                    var varLinksProp = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
                    if (varLinksProp != null)
                    {
                        foreach (var varLinkStruct in varLinksProp)
                        {
                            var links = varLinkStruct.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                            foreach (var link in links)
                            {
                                link.Value = seqObjs[oldObjectUindices.IndexOf(link.Value)].Value;
                            }
                        }
                    }

                    var eventLinksProp = props.GetProp<ArrayProperty<StructProperty>>("EventLinks");
                    if (eventLinksProp != null)
                    {
                        foreach (var eventLinkStruct in eventLinksProp)
                        {
                            var links = eventLinkStruct.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents");
                            foreach (var link in links)
                            {
                                var idx = oldObjectUindices.IndexOf(link.Value);
                                if (idx >= 0)
                                {
                                    link.Value = seqObjs[idx].Value;
                                }
                                else
                                {
                                    // Uh oh
                                    Debugger.Break();
                                }
                            }
                        }
                    }

                    obj.WriteProperties(props);
                }

                //re-point sequence links to new objects
                int oldObj;
                int newObj;
                var propCollection = sequence.GetProperties();
                var inputLinksProp = propCollection.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inputLinksProp != null)
                {
                    foreach (var inLinkStruct in inputLinksProp)
                    {
                        var linkedOp = inLinkStruct.GetProp<ObjectProperty>("LinkedOp");
                        oldObj = linkedOp.Value;
                        if (oldObj != 0)
                        {
                            newObj = seqObjs[oldObjectUindices.IndexOf(oldObj)].Value;
                            linkedOp.Value = newObj;

                            NameProperty linkAction = inLinkStruct.GetProp<NameProperty>("LinkAction");
                            linkAction.Value =
                                new NameReference(linkAction.Value.Name, pcc.GetUExport(newObj).indexValue);
                        }
                    }
                }

                var outputLinksProp = propCollection.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outputLinksProp != null)
                {
                    foreach (var outLinkStruct in outputLinksProp)
                    {
                        var linkedOp = outLinkStruct.GetProp<ObjectProperty>("LinkedOp");
                        oldObj = linkedOp.Value;
                        if (oldObj != 0)
                        {
                            newObj = seqObjs[oldObjectUindices.IndexOf(oldObj)].Value;
                            linkedOp.Value = newObj;

                            NameProperty linkAction = outLinkStruct.GetProp<NameProperty>("LinkAction");
                            linkAction.Value =
                                new NameReference(linkAction.Value.Name, pcc.GetUExport(newObj).indexValue);
                        }
                    }
                }

                sequence.WriteProperties(propCollection);
            }
            else if (sequence.ClassName == "SequenceReference")
            {
                //set OSequenceReference to new sequence
                var oSeqRefProp = sequence.GetProperty<ObjectProperty>("oSequenceReference");
                if (oSeqRefProp == null || oSeqRefProp.Value == 0)
                {
                    return;
                }

                int oldSeqIndex = oSeqRefProp.Value;
                oSeqRefProp.Value = sequence.UIndex + 1;
                sequence.WriteProperty(oSeqRefProp);

                //clone sequence
                ExportEntry newSequence = CloneObject(pcc.GetUExport(oldSeqIndex), sequence, false);
                //set SequenceReference's linked name indices
                var inputIndices = new List<int>();
                var outputIndices = new List<int>();

                var props = newSequence.GetProperties();
                var inLinksProp = props.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inLinksProp != null)
                {
                    foreach (var inLink in inLinksProp)
                    {
                        inputIndices.Add(inLink.GetProp<NameProperty>("LinkAction").Value.Number);
                    }
                }

                var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outLinksProp != null)
                {
                    foreach (var outLinks in outLinksProp)
                    {
                        outputIndices.Add(outLinks.GetProp<NameProperty>("LinkAction").Value.Number);
                    }
                }

                props = sequence.GetProperties();
                inLinksProp = props.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inLinksProp != null)
                {
                    for (int i = 0; i < inLinksProp.Count; i++)
                    {
                        NameProperty linkAction = inLinksProp[i].GetProp<NameProperty>("LinkAction");
                        linkAction.Value = new NameReference(linkAction.Value.Name, inputIndices[i]);
                    }
                }

                outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outLinksProp != null)
                {
                    for (int i = 0; i < outLinksProp.Count; i++)
                    {
                        NameProperty linkAction = outLinksProp[i].GetProp<NameProperty>("LinkAction");
                        linkAction.Value = new NameReference(linkAction.Value.Name, outputIndices[i]);
                    }
                }

                sequence.WriteProperties(props);
            }
        }

        /// <summary>
        /// Finds sequence objects with outbound connections that come to this node
        /// </summary>
        /// <param name="node">Node to find outbound connections to</param>
        /// <param name="sequenceElements">Sequence objects to search for connections</param>
        /// <param name="linkIdxsToMatchOn">Optional: Idx of input link that any sequence objects must match</param>
        /// <param name="filteredInputNames">Optional: Link desc of input link that any sequence objects must match</param>
        /// <returns>List of any sequence objects that link to this node</returns>
        public static List<ExportEntry> FindOutputConnectionsToNode(ExportEntry node, IEnumerable<ExportEntry> sequenceElements, List<int> linkIdxsToMatchOn = null, List<string> filteredInputNames = null)
        {
            List<ExportEntry> referencingNodes = new List<ExportEntry>();

            foreach (var seqObj in sequenceElements)
            {
                if (seqObj == node) continue; // Skip node pointing to itself
                var linkSet = GetOutputLinksOfNode(seqObj);
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

                if (matchingLinks.Any(x => x.Any(y => linkIdxsToMatchOn != null && y.LinkedOp == node && linkIdxsToMatchOn.Contains(y.InputLinkIdx))))
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
    }
}
