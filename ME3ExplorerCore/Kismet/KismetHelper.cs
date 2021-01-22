using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;

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

        public static void RemoveAllLinks(ExportEntry export)
        {
            var props = export.GetProperties();
            var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    prop.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
                }
            }

            var varLinksProp = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Clear();
                }
            }

            var eventLinksProp = props.GetProp<ArrayProperty<StructProperty>>("EventLinks");
            if (eventLinksProp != null)
            {
                foreach (var prop in eventLinksProp)
                {
                    prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents").Clear();
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
    }
}
