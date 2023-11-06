using System;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
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
        /// Adds an event link from a source sequence object to an event.
        /// This will not create a new event link, only adding an event to an existing link.
        /// </summary>
        /// <param name="source">Source sequence object, this is the SeqAct the link will be added to</param>
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

        /// <summary>
        /// Builds a jagged 2D list of OutboundLinks for each output link.
        /// </summary>
        /// <param name="node">Sequence object to get outbound links from</param>
        /// <returns>Outer list represents OutputLinks, inner lists represent the different sequence objects that link goes to</returns>
        [Obsolete("Duplication: Use SeqTools.GetOutboundLinksOfNode instead")]
        public static List<List<SeqTools.OutboundLink>> GetOutboundLinksOfNode(ExportEntry node) =>
            SeqTools.GetOutboundLinksOfNode(node);

        #endregion

        /// <summary>
        /// Gets a list of non-null objects in the sequence. Returns IEntry, as some sequences are referenced as imports.
        /// This only gets the immediate children objects of the given sequence.
        /// </summary>
        /// <param name="sequence">Sequence export to get elements from</param>
        /// <returns>List of IEntrys in the sequence</returns>
        public static List<IEntry> GetSequenceObjects(ExportEntry sequence)
        {
            var objects = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (objects == null)
                return new List<IEntry>();

            return objects.Where(x => x.Value != 0).Select(x => x.ResolveToEntry(sequence.FileRef)).ToList();
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
        /// Gets a list of link names for the outbound links of the node
        /// </summary>
        /// <returns>A list of LinkDesc values</returns>
        public static List<string> GetOutboundLinkNames(ExportEntry export)
        {
            var props = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            var names = new List<string>();
            if (props != null)
            {
                names.AddRange(props.Select(x => x.Properties.GetProp<StrProperty>("LinkDesc").Value));
            }
            return names;
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
        /// <param name="comments">Object comment lines</param>
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
        /// Removes variable links that have no defined values. Can be dangerous if the class is not designed to lookup by name (will break Idx based classes)
        /// </summary>
        /// <param name="export"></param>
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
        /// Clones a sequence object and places it into the given sequence.
        /// </summary>
        /// <param name="itemToClone"></param>
        /// <param name="sequence"></param>
        /// <param name="topLevel"></param>
        /// <param name="incrementIndex"></param>
        /// <returns></returns>
        public static ExportEntry CloneObject(ExportEntry itemToClone, ExportEntry sequence = null, bool topLevel = true, bool incrementIndex = true, bool cloneChildren = false)
        {
            //SeqVar_External needs to have the same index to work properly
            ExportEntry exp = cloneChildren ? EntryCloner.CloneTree(itemToClone) : EntryCloner.CloneEntry(itemToClone, incrementIndex: incrementIndex && itemToClone.ClassName != "SeqVar_External");
            KismetHelper.AddObjectToSequence(exp, sequence ?? SeqTools.GetParentSequence(itemToClone), topLevel);
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
    }
}
