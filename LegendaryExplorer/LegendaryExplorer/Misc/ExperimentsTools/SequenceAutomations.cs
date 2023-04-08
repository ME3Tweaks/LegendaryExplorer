using LegendaryExplorer.DialogueEditor.DialogueEditorExperiments;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml.Linq;

using static LegendaryExplorer.Misc.ExperimentsTools.SharedMethods;

namespace LegendaryExplorer.Misc.ExperimentsTools
{
    public static class SequenceAutomations
    {
        /// <summary>
        /// Add a streaming handshake to the given sequence, which runs after being triggered by the input, and continues to the output.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="sequence">Sequence to add the handshake to.</param>
        /// <param name="outEvtName">Name of the event to activate.</param>
        /// <param name="inEvtName">Name of the activated event to react to.</param>
        /// <returns>(outEvent, gate) The event to activate and the output gate.</returns>
        public static (ExportEntry, ExportEntry) AddEventHandshake(IMEPackage pcc, ExportEntry sequence, string outEvtName, string inEvtName)
        {
            // Create the handshake objects
            ExportEntry outEvent = CreateSequenceObjectWithProps(pcc, "SeqAct_ActivateRemoteEvent", new() { new NameProperty(outEvtName, "EventName") });
            ExportEntry inEvent = CreateSequenceObjectWithProps(pcc, "SeqEvent_RemoteEvent", new() { new NameProperty(inEvtName, "EventName") });
            ExportEntry gate = CreateSequenceObjectWithProps(pcc, "SeqAct_Gate", new() { new BoolProperty(false, "bOpen") });

            KismetHelper.AddObjectsToSequence(sequence, false, new[] { outEvent, inEvent, gate });

            // Connect the handshake objects
            KismetHelper.CreateOutputLink(outEvent, "Out", gate, 1); // outEvent.Out to gate.Open
            KismetHelper.CreateOutputLink(gate, "Out", gate, 2); // gate.Out to gate.Close
            KismetHelper.CreateOutputLink(inEvent, "Out", gate, 0); // inEvent.Out to gate.In

            return (outEvent, gate);
        }

        /// <summary>
        /// Inserts a remote event handshake, connected between the source seqObject and all of the objets its outputs link to.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="sequence">Sequence to operate on.</param>
        /// <param name="seqObj">Object to insert the handshake after.</param>
        /// <param name="outEvtName">Name of the event to activate.</param>
        /// <param name="inEvtName">Name of the activated event to react to.</param>
        /// <param name="skipOutIdxs">Output indexes to skip. Specially used when wanting to manually connect failed outputs of conversations.</param>
        /// <returns>(outEvent, gate) The event to activate and the output gate.</returns>
        public static (ExportEntry, ExportEntry) InsertEventHandshake(IMEPackage pcc, ExportEntry sequence, ExportEntry seqObj, string outEvtName, string inEvtName, int[] skipOutIdxs = null)
        {
            (ExportEntry outEvent, ExportEntry gate) = AddEventHandshake(pcc, sequence, outEvtName, inEvtName); // Craete the event handshake

            // Connect the output of the gate to all the links of the seqObj
            List<List<OutputLink>> outboundLinks = KismetHelper.GetOutputLinksOfNode(seqObj);
            List<(int, int)> linked = new();
            for (int i = 0; i < outboundLinks.Count; i++)
            {
                if (skipOutIdxs != null && skipOutIdxs.Contains(i)) { continue; }

                foreach (OutputLink link in outboundLinks[i])
                {
                    if (linked.Exists(el => link.LinkedOp.UIndex == el.Item1 && link.InputLinkIdx == el.Item2)) { continue; }
                    KismetHelper.CreateOutputLink(gate, "Out", (ExportEntry)link.LinkedOp, link.InputLinkIdx);
                    linked.Add((link.LinkedOp.UIndex, link.InputLinkIdx));
                }
            }

            // Remove the current links of the objet, and link it to the handshake.
            // We do this to ensure that when we skip the object later, the inputs are linked to the handshake only,
            // instead of other things, as the handshake takes care of that.
            KismetHelper.RemoveOutputLinks(seqObj);
            KismetHelper.CreateOutputLink(seqObj, "Out", outEvent, 0);

            return (outEvent, gate);
        }

        /// <summary>
        /// Replaces an object in a sequence with a remote event handshake, relinking its input and output links.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="sequence">Sequence to operate on.</param>
        /// <param name="seqObj">Object to replace.</param>
        /// <param name="outEvtName">Name of the event to activate.</param>
        /// <param name="inEvtName">Name of the activated event to react to.</param>
        /// <param name="skipOutIdxs">Output indexes to skip. Specially used when wanting to manually connect failed outputs of conversations.</param>
        /// <returns>(outEvent, gate) The event to activate and the output gate.</returns>
        public static (ExportEntry, ExportEntry) ReplaceObjectWithEventHandshake(IMEPackage pcc, ExportEntry sequence, ExportEntry seqObj, string outEvtName, string inEvtName, int[] skipOutIdxs = null)
        {
            (ExportEntry outEvent, ExportEntry gate) = AddEventHandshake(pcc, sequence, outEvtName, inEvtName); // Craete the event handshake

            // Connect the output of the gate to all the links of the seqObj
            List<List<OutputLink>> outboundLinks = KismetHelper.GetOutputLinksOfNode(seqObj);
            List<(int, int)> linked = new();
            for (int i = 0; i < outboundLinks.Count; i++)
            {
                if (skipOutIdxs != null && skipOutIdxs.Contains(i)) { continue; }

                foreach (OutputLink link in outboundLinks[i])
                {
                    if (linked.Exists(el => link.LinkedOp.UIndex == el.Item1 && link.InputLinkIdx == el.Item2)) { continue; }
                    KismetHelper.CreateOutputLink(gate, "Out", (ExportEntry)link.LinkedOp, link.InputLinkIdx);
                    linked.Add((link.LinkedOp.UIndex, link.InputLinkIdx));
                }
            }

            // Remove the current links of the objet, and link it to the handshake.
            // We do this to ensure that when we skip the object later, the inputs are linked to the handshake only,
            // instead of other things, as the handshake takes care of that.
            KismetHelper.RemoveOutputLinks(seqObj);
            KismetHelper.CreateOutputLink(seqObj, "Out", outEvent, 0);

            // Skip the element so the inputs point to our handshake
            KismetHelper.SkipSequenceElement(seqObj, null, 0);

            // Skipping DOES NOT remove output links, so we clean the skipped object
            KismetHelper.RemoveOutputLinks(seqObj);
            KismetHelper.RemoveVariableLinks(seqObj);

            return (outEvent, gate);
        }

        /// <summary>
        /// Swaps the output links of a PMCheckState.
        /// </summary>
        /// <param name="obj">PMCheckState object.</param>
        public static void SwapCheckStateOutputs(ExportEntry obj)
        {
            List<List<OutputLink>> outLinks = KismetHelper.GetOutputLinksOfNode(obj);
            List<OutputLink> tempLinks = outLinks[0];
            outLinks[0] = outLinks[1];
            outLinks[1] = tempLinks;
            KismetHelper.WriteOutputLinksToNode(obj, outLinks);
        }

        /// <summary>
        /// Skips a sequence element, and removes its output and variable links to completely disconnect it.
        /// </summary>
        /// <param name="seqObj">The sequence object to skip</param>
        /// <param name="outboundLinkName">The name of the outbound link that should be attached to the preceding entry element, must have either this or the next argument</param>
        /// <param name="outboundLinkIdx">The 0-indexed outbound link that should be attached the preceding entry element, as if this one had fired that link.</param>
        public static void SkipAndCleanSequenceElement(ExportEntry seqObj, string outboundLinkName = null, int outboundLinkIdx = -1)
        {
            KismetHelper.SkipSequenceElement(seqObj, null, 0);
            KismetHelper.RemoveOutputLinks(seqObj);
            KismetHelper.RemoveVariableLinks(seqObj);
        }

        /// <summary>
        /// Creates a sequence object with default props plus the ones passed as customProps;
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="className">Name of the object's class.</param>
        /// <param name="customProps">Props to add to the object.</param>
        /// <returns></returns>
        public static ExportEntry CreateSequenceObjectWithProps(IMEPackage pcc, string className, PropertyCollection customProps)
        {
            ExportEntry seqObj = SequenceObjectCreator.CreateSequenceObject(pcc, className);
            PropertyCollection props = SequenceObjectCreator.GetSequenceObjectDefaults(pcc, className, pcc.Game);

            foreach (Property prop in customProps) { props.AddOrReplaceProp(prop); }

            seqObj.WriteProperties(props);

            return seqObj;
        }

        /// <summary>
        /// Create a var link with custom properties.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="name">LinkDesc.</param>
        /// <returns>The varLink StructProperty.</returns>
        public static StructProperty CreateVarLink(IMEPackage pcc, string name)
        {
            PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(pcc.Game, "SeqVarLink", true);

            int minVars = name == "Anchor" ? 1 : 0;
            int maxVars = name == "Anchor" ? 1 : 255;

            props.AddOrReplaceProp(new StrProperty(name, "LinkDesc"));
            int index = pcc.FindImport("Engine.SeqVar_Object").UIndex;
            props.AddOrReplaceProp(new ObjectProperty(index, "ExpectedType"));
            props.AddOrReplaceProp(new IntProperty(minVars, "MinVars"));
            props.AddOrReplaceProp(new IntProperty(maxVars, "MaxVars"));
            return new StructProperty("SeqVarLink", props);
        }

        // From SequenceEditorWPF.xaml.cs
        /// <summary>
        /// Clones the old object and add it to the given sequence..
        /// </summary>
        /// <param name="old">Object to clone.</param>
        /// <param name="sequence">Sequence to add the object to.</param>
        /// <param name="topLevel"></param>
        /// <param name="incrementIndex"></param>
        /// <returns>Cloned object.</returns>
        public static ExportEntry CloneObject(ExportEntry old, ExportEntry sequence, bool topLevel = true, bool incrementIndex = true)
        {
            //SeqVar_External needs to have the same index to work properly
            ExportEntry exp = EntryCloner.CloneEntry(old, incrementIndex: incrementIndex && old.ClassName != "SeqVar_External");

            KismetHelper.AddObjectToSequence(exp, sequence, topLevel);
            // cloneSequence(exp);
            return exp;
        }
    }
}
