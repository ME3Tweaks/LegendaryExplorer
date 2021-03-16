using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ME3ExplorerCore.Kismet
{
    public static class KismetHelper
    {
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

        public static void CreateVariableLink(ExportEntry src, string linkDescription, ExportEntry dest)
        {
            if (src.GetProperty<ArrayProperty<StructProperty>>("VariableLinks") is { } varLinksProp)
            {
                foreach (var prop in varLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDescription)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Add(new ObjectProperty(dest));
                        src.WriteProperty(varLinksProp);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a list of non-null objects in the sequence. Returns IEntry, as some sequences are referenced as imports.
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static List<IEntry> GetSequenceObjects(ExportEntry sequence)
        {
            var objects = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (objects == null)
                return new List<IEntry>();

            return objects.Where(x => x.Value != 0).Select(x => x.ResolveToEntry(sequence.FileRef)).ToList();
        }

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

        public static void RemoveOutputLinks(ExportEntry export)
        {
            RemoveAllLinks(export, true, false, false);
        }

        public static void RemoveVariableLinks(ExportEntry export)
        {
            RemoveAllLinks(export, false, true, false);
        }

        public static void RemoveEventLinks(ExportEntry export)
        {
            RemoveAllLinks(export, false, false, true);
        }

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

        public static void AddObjectToSequence(ExportEntry newObject, ExportEntry sequenceExport, bool removeLinks = false)
        {
            ArrayProperty<ObjectProperty> seqObjs = sequenceExport.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects") ?? new ArrayProperty<ObjectProperty>("SequenceObjects");
            seqObjs.Add(new ObjectProperty(newObject));
            sequenceExport.WriteProperty(seqObjs);

            PropertyCollection newObjectProps = newObject.GetProperties();
            newObjectProps.AddOrReplaceProp(new ObjectProperty(sequenceExport, "ParentSequence"));
            newObject.WriteProperties(newObjectProps);
            if (removeLinks)
            {
                RemoveAllLinks(newObject);
            }
            newObject.Parent = sequenceExport;
        }

        #region Links

        /// <summary>
        /// Builds a list of OutputLinkIdx => [List of nodes pointed to]
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
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

        public class OutboundLink
        {
            public IEntry? LinkedOp { get; set; }
            public int InputLinkIdx { get; set; }

            public static OutboundLink FromStruct(StructProperty sp, IMEPackage package)
            {
                return new OutboundLink()
                {
                    LinkedOp = sp.GetProp<ObjectProperty>("LinkedOp")?.ResolveToEntry(package),
                    InputLinkIdx = sp.GetProp<IntProperty>("InputLinkIdx")
                };
            }

            public StructProperty GenerateStruct()
            {
                return new StructProperty("SeqOpOutputInputLink", false,
                    new ObjectProperty(LinkedOp.UIndex, "LinkedOp"),
                    new IntProperty(InputLinkIdx, "InputLInkIdx"),
                    new NoneProperty());
            }
        }

        #endregion

        /// <summary>
        /// Gets list of link names for the outbound links of the node
        /// </summary>
        /// <returns></returns>
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
    }
}
