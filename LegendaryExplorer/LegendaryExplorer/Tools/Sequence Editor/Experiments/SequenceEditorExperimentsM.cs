using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.Sequence_Editor.Experiments
{

    /// <summary>
    /// Experiments in Sequence Editor (Mgamerz' stuff)
    /// </summary>
    public static class SequenceEditorExperimentsM
    {
        public static void CheckSequenceSets(SequenceEditorWPF seqEd)
        {
            if (seqEd.CurrentObjects.Any)
            {
                foreach (var seqObj in seqEd.CurrentObjects)
                {
                    //if (seqObj.Export.ClassName == "SeqAct_SetInt")
                    //Debug.WriteLine("hi");
                    if (seqObj.Export.IsA("SeqAct_SetSequenceVariable"))
                    {
                        var varLinks = SeqTools.GetVariableLinksOfNode(seqObj.Export);
                        foreach (var link in varLinks)
                        {
                            //link.

                            foreach (var linkedNode in link.LinkedNodes.OfType<ExportEntry>())
                            {
                                if (!linkedNode.IsA(link.ExpectedTypeName))
                                {
                                    Debug.WriteLine("NOT A THING");
                                }

                                var propertyInfo = GlobalUnrealObjectInfo.GetPropertyInfo(seqEd.Pcc.Game, link.PropertyName, linkedNode.ClassName, containingExport: linkedNode);
                                if (propertyInfo == null)
                                {
                                    Debug.WriteLine($"SEQCHECK: {seqObj.Export.UIndex} {seqObj.Export.ObjectName.Instanced} writes a property named {link.PropertyName}, but it doesn't exist on class {linkedNode.ClassName}!");
                                }

                            }

                        }
                    }
                }
            }
        }
    }
}
