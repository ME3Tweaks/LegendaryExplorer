using System.Collections.Generic;
using System.IO;
using ME3Tweaks.Wwiser;
using ME3Tweaks.Wwiser.Model;
using ME3Tweaks.Wwiser.Model.Hierarchy;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    /// <summary>
    /// Resolves, for each "node" HIRC object (ActorMixer, Sound, RandomSequenceContainer, SwitchContainer,
    /// LayerContainer), which bus ultimately plays it - either its own override, or one it inherits from the
    /// nearest ancestor (walking up via DirectParentId) that sets one.
    /// </summary>
    /// <remarks>
    /// Uses the ME3Tweaks.Wwiser submodule for accurate, version-aware parsing of NodeBaseParameters, since
    /// LegendaryExplorerCore's own WwiseBank binary converter doesn't parse these object types.
    /// </remarks>
    public sealed class WwiseBusRouting
    {
        public sealed class NodeInfo
        {
            public uint ID;
            public uint OverrideBusId;
            public uint DirectParentId;
            public List<uint> Children = new();
        }

        public static readonly WwiseBusRouting Empty = new(new Dictionary<uint, NodeInfo>());

        public IReadOnlyDictionary<uint, NodeInfo> Nodes { get; }

        private WwiseBusRouting(Dictionary<uint, NodeInfo> nodes) => Nodes = nodes;

        /// <summary>
        /// Parses the given bank's raw bytes for actor-mixer-hierarchy routing info. Returns <see cref="Empty"/>
        /// if the bank can't be parsed by ME3Tweaks.Wwiser (e.g. an unsupported/very old Wwise version).
        /// </summary>
        public static WwiseBusRouting Parse(byte[] bnkFile)
        {
            if (bnkFile == null || bnkFile.Length == 0)
            {
                return Empty;
            }

            var nodes = new Dictionary<uint, NodeInfo>();
            try
            {
                using var ms = new MemoryStream(bnkFile);
                WwiseBank bank = WwiseBankParser.Deserialize(ms);
                if (bank.HIRC == null)
                {
                    return Empty;
                }

                foreach (HircItemContainer item in bank.HIRC.Items)
                {
                    if (item.Item is not IAkIdentifiable identifiable)
                    {
                        continue;
                    }

                    var info = new NodeInfo { ID = identifiable.Id };

                    if (item.Item is IHasNode hasNode)
                    {
                        info.OverrideBusId = hasNode.NodeBaseParameters.OverrideBusId;
                        info.DirectParentId = hasNode.NodeBaseParameters.DirectParentId;
                    }

                    switch (item.Item)
                    {
                        case ActorMixer actorMixer:
                            info.Children.AddRange(actorMixer.Children);
                            break;
                        case RandSeqContainer randSeq:
                            info.Children.AddRange(randSeq.Children);
                            break;
                        case SwitchContainer switchContainer:
                            info.Children.AddRange(switchContainer.Children);
                            break;
                        case LayerContainer layerContainer:
                            info.Children.AddRange(layerContainer.Children);
                            break;
                    }

                    nodes[info.ID] = info;
                }
            }
            catch
            {
                // Bank isn't parseable by ME3Tweaks.Wwiser (unsupported version, OT-era format, etc.) - bus
                // routing just won't be available for this bank.
                return Empty;
            }

            return new WwiseBusRouting(nodes);
        }

        /// <summary>
        /// Resolves the bus that ultimately plays the given node: its own override if set, otherwise the nearest
        /// ancestor's override, walking up via DirectParentId.
        /// </summary>
        /// <returns>The resolved bus ID (0 if nothing overrides - implicitly the Master Bus, or the node/its
        /// ancestry isn't known), and whether it's this node's own override rather than an inherited one.</returns>
        public (uint busId, bool isOwnOverride) ResolveBus(uint nodeId)
        {
            var visited = new HashSet<uint>();
            uint currentId = nodeId;
            bool isOwn = true;
            while (Nodes.TryGetValue(currentId, out NodeInfo node) && visited.Add(currentId))
            {
                if (node.OverrideBusId != 0)
                {
                    return (node.OverrideBusId, isOwn);
                }

                isOwn = false;
                currentId = node.DirectParentId;
            }

            return (0, false);
        }
    }
}
