using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Kismet
{
    /// <summary>
    /// Static methods to obtain information on sequence objects, and perform common sequence editing operations
    /// </summary>
    /// <remarks>
    /// All methods from this class have been merged with KismetHelper. You should not use this class for any future projects.
    /// </remarks>
    [Obsolete("Class has been merged with KismetHelper. Use that instead!")]
    public class SeqTools
    {
        /// <summary>
        /// Changes a single output link to a new target and commits the properties.
        /// </summary>
        /// <param name="export">Export to operate on</param>
        /// <param name="outputLinkIndex">The index of the item in 'OutputLinks'</param>
        /// <param name="linksIndex">The index of the item in the Links array</param>
        /// <param name="newTarget">The UIndex of the new target</param>
        [Obsolete("Duplication: Use KismetHelper.ChangeOutputLink instead")]
        public static void ChangeOutlink(ExportEntry export, int outputLinkIndex, int linksIndex, int newTarget)
        {
            KismetHelper.ChangeOutputLink(export, outputLinkIndex, linksIndex, newTarget);
        }

        /// <summary>
        /// Changes a single output link to a new target.
        /// </summary>
        /// <param name="props">The export properties list to operate on</param>
        /// <param name="outputLinkIndex">The index of the item in 'OutputLinks'</param>
        /// <param name="linksIndex">The index of the item in the Links array</param>
        /// <param name="newTarget">The UIndex of the new target</param>
        [Obsolete("Duplication: Use KismetHelper.ChangeOutputLink instead")]
        public static void ChangeOutlink(PropertyCollection props, int outputLinkIndex, int linksIndex, int newTarget)
        {
            KismetHelper.ChangeOutputLink(props, outputLinkIndex, linksIndex, newTarget);
        }

        /// <summary>
        /// Removes a sequence element from the graph, by repointing incoming references to the ones referenced by outgoing items on this export.
        /// This is a very basic utility, only use it for items with one input and potentially multiple outputs.
        /// </summary>
        /// <param name="elementToSkip">The sequence object to skip</param>
        /// <param name="outboundLinkName">The name of the outbound link that should be attached to the preceding entry element, must have either this or the next argument</param>
        /// <param name="outboundLinkIdx">The 0-indexed outbound link that should be attached the preceding entry element, as if this one had fired that link.</param>
        [Obsolete("Duplication: Use KismetHelper.SkipSequenceElement instead")]
        public static void SkipSequenceElement(ExportEntry elementToSkip, string outboundLinkName = null, int outboundLinkIdx = -1)
        {
            KismetHelper.SkipSequenceElement(elementToSkip, outboundLinkName, outboundLinkIdx);
        }

        /// <summary>
        /// Builds a jagged 2D list of OutboundLinks for each output link.
        /// </summary>
        /// <param name="node">Sequence object to get outbound links from</param>
        /// <returns>Outer list represents OutputLinks, inner lists represent the different sequence objects that link goes to</returns>
        [Obsolete("Duplication: Use KismetHelper.GetOutputLinksOfNode instead")]
        public static List<List<OutputLink>> GetOutboundLinksOfNode(ExportEntry node)
        {
            return KismetHelper.GetOutputLinksOfNode(node);
        }

        /// <summary>
        /// Writes a list of outbound links to a sequence node. Note that this cannot add output link points (like an additional output param), but only existing connections.
        /// </summary>
        /// <param name="node">Sequence object node to write outbound links to</param>
        /// <param name="linkSet">Link set to write to the export</param>
        [Obsolete("Duplication: Use KismetHelper.GetOutputLinksToNode instead")]
        public static void WriteOutboundLinksToNode(ExportEntry node, List<List<OutputLink>> linkSet)
        {
            KismetHelper.WriteOutputLinksToNode(node, linkSet);
        }

        /// <summary>
        /// Writes a set of outbound links to a property collection. This cannot be used to add output link points, only to overwrite the links of existing outputs.
        /// </summary>
        /// <remarks>Returns early if <see cref="linkSet"/> is not of correct length. 'Links' ArrayProperty must already exist in collection.</remarks>
        /// <param name="linkSet">Link set to write to properties</param>
        /// <param name="props">Properties to write links to</param>
        [Obsolete("Duplication: Use KismetHelper.GetOutputLinksToProperties instead")]
        public static void WriteOutboundLinksToProperties(List<List<OutputLink>> linkSet, PropertyCollection props)
        {
            KismetHelper.WriteOutputLinksToProperties(linkSet, props);
        }

        /// <summary>
        /// Finds sequence objects with outbound connections that come to this node
        /// </summary>
        /// <param name="node">Node to find outbound connections to</param>
        /// <param name="sequenceElements">Sequence objects to search for connections</param>
        /// <returns>List of any sequence objects that link to this node</returns>
        [Obsolete("Use KismetHelper.FindOutputConnectionsToNode instead")]
        public static List<ExportEntry> FindOutboundConnectionsToNode(ExportEntry node, IEnumerable<ExportEntry> sequenceElements, List<int> linkIdxsToMatchOn = null, List<string> filteredInputNames = null)
        {
            return KismetHelper.FindOutputConnectionsToNode(node, sequenceElements, linkIdxsToMatchOn,
                filteredInputNames);
        }


        /// <summary>
        /// Finds sequence objects with variable connections that come to this node
        /// </summary>
        /// <param name="node">Sequence variable to find connections to</param>
        /// <param name="sequenceElements">Sequence objects to search for connections</param>
        /// <returns>List of any sequence objects that link to this node</returns>
        [Obsolete("Use KismetHelper.FindVariableConnectionsToNode instead")]
        public static List<ExportEntry> FindVariableConnectionsToNode(ExportEntry node, List<ExportEntry> sequenceElements)
        {
            return KismetHelper.FindVariableConnectionsToNode(node, sequenceElements);
        }

        /// <summary>
        /// Gets a list of all sequence elements that are referenced by this sequence's SequenceObjects property
        /// If the passed in object is not a Sequence, the parent sequence is used. Returns null if there is no parent sequence.
        /// </summary>
        /// <param name="export">Export to get referenced elements for</param>
        /// <returns>List of referenced sequence elements</returns>
        [Obsolete("Use KismetHelper.GetAllSequenceElements instead")]
        public static List<IEntry> GetAllSequenceElements(ExportEntry export)
        {
            return KismetHelper.GetAllSequenceElements(export);
        }

        /// <summary>
        /// Gets the list of VarLinks that can be attached to by the specified sequence object export.
        /// </summary>
        /// <remarks>You cannot ADD new varlinks this way as the serialization does not include all types.</remarks>
        /// <param name="export">Export to get variable links for</param>
        /// <returns>List of variable link infos</returns>
        [Obsolete("Use KismetHelper.GetVariableLinksOfNode instead")]
        public static List<VarLinkInfo> GetVariableLinksOfNode(ExportEntry export)
        {
            return KismetHelper.GetVariableLinksOfNode(export);
        }

        /// <summary>
        /// Gets the list of variable links from a collection of properties. If there is no VariableLinks property, this returns an empty list.
        /// </summary>
        /// <param name="props">Properties to get variable links from</param>
        /// <param name="pcc">Package containing the property collection</param>
        /// <returns>List of any variable links</returns>
        [Obsolete("Use KismetHelper.GetVariableLinks instead")]
        public static List<VarLinkInfo> GetVariableLinks(PropertyCollection props, IMEPackage pcc)
        {
            return KismetHelper.GetVariableLinks(props, pcc);
        }

        /// <summary>
        /// Writes the list of variable links to the node. Only the linked objects are written.
        /// The list MUST be in the same order and be the same length as the current variable links on the export.
        /// </summary>
        /// <remarks>This can't be used to add any new variable link points.</remarks>
        /// <param name="export">Export to write links to</param>
        /// <param name="varLinks">Variable links to write</param>
        [Obsolete("Use KismetHelper.WriteVariableLinksToNode instead")]
        public static void WriteVariableLinksToNode(ExportEntry export, List<VarLinkInfo> varLinks)
        {
            KismetHelper.WriteVariableLinksToNode(export, varLinks);
        }

        /// <summary>
        /// Writes the list of variable links to the given property collection. Only writes the linked variables.
        /// The list MUST be in the same order and be the same length as the current variable links in the collection.
        /// </summary>
        /// <remarks>This can't be used to add any new variable link points. LinkedVariables property must already exist.</remarks>
        /// <param name="varLinks">Variable links to write</param>
        /// <param name="props">Properties to write links to</param>
        [Obsolete("Use KismetHelper.WriteVariableLinksToProperties instead")]
        public static void WriteVariableLinksToProperties(List<VarLinkInfo> varLinks, PropertyCollection props)
        {
            KismetHelper.WriteVariableLinksToProperties(varLinks, props);
        }

        /// <summary>
        /// Gets the containing sequence of the specified export.
        /// Performed by looking for ParentSequence object property.
        /// </summary>
        /// <remarks>Use the <see cref="recurseUp"/> parameter to get the top level sequence.</remarks>
        /// <param name="export">Export to get containing sequence of</param>
        /// <param name="recurseUp">If true, will continue getting parent sequences until it reaches the top of the chain.</param>
        /// <returns>Parent sequence export</returns>
        [Obsolete("Use KismetHelper.GetParentSequence instead")]
        public static ExportEntry GetParentSequence(ExportEntry export, bool recurseUp = false)
        {
            return KismetHelper.GetParentSequence(export, recurseUp);
        }

        /// <summary>
        /// Writes the Originator property on a sequence object
        /// </summary>
        /// <param name="export">Sequence object to write property on</param>
        /// <param name="originator">Originator entry to write</param>
        [Obsolete("Use KismetHelper.WriteOriginator instead")]
        public static void WriteOriginator(ExportEntry export, IEntry originator)
        {
            KismetHelper.WriteOriginator(export, originator);
        }

        /// <summary>
        /// Writes the ObjValue property on a sequence object
        /// </summary>
        /// <param name="export">Sequence object to write property on</param>
        /// <param name="objValue">ObjValue entry to write</param>
        [Obsolete("Use KismetHelper.ObjValue instead")]
        public static void WriteObjValue(ExportEntry export, IEntry objValue)
        {
            KismetHelper.WriteObjValue(export, objValue);
        }

#if DEBUG
        /// <summary>
        /// DEBUG: Writes info about a series of <see cref="VarLinkInfo"/>s to the debug console
        /// </summary>
        /// <param name="seqLinks">Variable links to write</param>
        [Obsolete("Use KismetHelper.PrintVarLinkInfo instead")]
        public static void PrintVarLinkInfo(List<VarLinkInfo> seqLinks)
        {
            KismetHelper.PrintVarLinkInfo(seqLinks);
        }
#endif
        /// <summary>
        /// Gets a list of Outlink LinkDesc names, in order.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        [Obsolete("Use KismetHelper.GetOutputLinkNames instead")]
        public static List<string> GetOutlinkNames(ExportEntry node)
        {
            return KismetHelper.GetOutputLinkNames(node);
        }
    }
}
