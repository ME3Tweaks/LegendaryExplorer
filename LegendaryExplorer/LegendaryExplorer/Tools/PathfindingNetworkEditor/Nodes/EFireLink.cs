using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// A single entry in a cover slot's FireLinks array.
    /// </summary>
    public class EFireLink
    {
        // Interactions are Packed in Game3
        /// <summary>Raw interaction bytes for this link.</summary>
        public List<byte> Interactions { get; set; } = [];

        /// <summary>Whether this is a fallback link used when no primary link is available.</summary>
        public bool bFallbackLink { get; set; }

        /// <summary>Whether the dynamic index has been initialized.</summary>
        public bool bDynamicIndexInited { get; set; }

        /// <summary>The target CoverLink export referenced by this fire link (from the TargetActor struct, if present).</summary>
        public ExportEntry? TargetActorExport { get; set; }

        /// <summary>
        /// Guid of the target actor if the reference is cross-level or otherwise unresolvable at load time. This is used for resolution in a post-load pass
        /// </summary>
        public Guid TargetActorGuid { get; set; }

        /// <summary>The slot index within the target CoverLink that this fire link targets.</summary>
        public int TargetSlotIdx { get; set; }

        /// <summary>Resolved at post-load time: the CoverSlotMarker node that this fire link targets, or <see langword="null"/> if unresolvable.</summary>
        public CoverSlotMarker? ResolvedTargetMarker { get; set; }

        public static EFireLink FromStruct(StructProperty structProp, IMEPackage package, Level level)
        {
            var props = structProp.Properties;

            ExportEntry? targetActorExport = null;
            int targetSlotIdx = 0;
            Guid targetActorGuid = Guid.Empty;

            if (!package.Game.IsGame3())
            {
                // Todo: needs verified on non LE3.
                if (props.GetProp<StructProperty>("TargetActor") is { } targetActorProp)
                {
                    var actorProp = targetActorProp.GetProp<ObjectProperty>("Actor");
                    if (actorProp?.Value > 0)
                        targetActorExport = package.GetUExport(actorProp.Value);
                    targetSlotIdx = targetActorProp.GetProp<IntProperty>("SlotIdx")?.Value ?? 0;
                }
            }
            else
            {
                // Data is packed.
                var packedCoverRefPairAndDynamicInfo = props.GetProp<IntProperty>("PackedProperties_CoverPairRefAndDynamicInfo")?.Value ?? 0;

                var covRefIdx = packedCoverRefPairAndDynamicInfo & 0x0000FFFF;

                if (covRefIdx < level.CoverIndexPairs.Count)
                {
                    var cover = level.CoverIndexPairs[covRefIdx];
                    targetSlotIdx = cover.SlotIdx;
                    var coverRef = level.CoverLinkRefs[(int)cover.CoverIndexIdx];
                    if (coverRef > 0)
                    {
                        targetActorExport = package.GetUExport(coverRef);
                    }
                    else
                    {
                        // This is likely bad performance since this probably should be a Dictionary.
                        targetActorGuid = level.CrossLevelCoverGuidRefs.FirstOrDefault(x => x.CoverIndexIdx == (int)cover.CoverIndexIdx).Guid;
                    }
                }
            }

            return new EFireLink
            {
                TargetActorExport = targetActorExport,
                TargetActorGuid = targetActorGuid,
                TargetSlotIdx = targetSlotIdx,
                Interactions = props.GetProp<ArrayProperty<ByteProperty>>("Interactions")
                                    ?.Select(b => b.Value).ToList() ?? [],
                bFallbackLink = props.GetProp<BoolProperty>("bFallbackLink")?.Value ?? false,
                bDynamicIndexInited = props.GetProp<BoolProperty>("bDynamicIndexInited")?.Value ?? false,
            };
        }

        internal void ResolvePostLoad(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            if (TargetActorExport == null && TargetActorGuid != Guid.Empty)
            {
                // Attempt to resolve cross-level reference via GUID.
                if (crossLevelLookup.TryGetValue(TargetActorGuid, out var resolvedCoverLink))
                {
                    var targetLink = resolvedCoverLink as CoverLink;
                    var targetSlot = targetLink.Slots[TargetSlotIdx];
                    if (exportLookupMap.TryGetValue(targetSlot.SlotMarker, out var resolvedMarker))
                    {
                        ResolvedTargetMarker = resolvedMarker as CoverSlotMarker;
                    }
                    else
                    {
                        Debug.WriteLine("Failed to cross level target marker - invalid slot ref?");
                    }
                }
                else
                {
                    Debug.WriteLine("Failed to resolve cross level target marker.");
                }
            }
            else if (TargetActorExport != null)
            {
                if (exportLookupMap.TryGetValue(TargetActorExport, out var resolvedCoverLink))
                {
                    var targetLink = resolvedCoverLink as CoverLink;
                    var targetSlot = targetLink.Slots[TargetSlotIdx];
                    if (exportLookupMap.TryGetValue(targetSlot.SlotMarker, out var resolvedMarker))
                    {
                        ResolvedTargetMarker = resolvedMarker as CoverSlotMarker;
                    }
                    else
                    {
                        Debug.WriteLine("Failed to in-file target marker - invalid slot ref?");
                    }
                }
                else
                {
                    Debug.WriteLine("Failed to resolve in-file target marker?");
                }
            }
        }
    }
}
