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
    /// Represents a packed exposed-cover entry for a <see cref="CoverSlot"/>.
    /// The data is stored as a packed int: upper 16 bits = ExposureScale, lower 16 bits = CoverIndexPairs index.
    /// </summary>
    public class ExposedLink
    {
        /// <summary>Exposure scale value (upper 16 bits of the packed int).</summary>
        public int ExposureScale { get; set; }

        /// <summary>The target CoverLink export, or <see langword="null"/> if unresolved or cross-level.</summary>
        public ExportEntry? TargetCoverLinkExport { get; set; }

        /// <summary>Guid of the target actor for cross-level references.</summary>
        public Guid TargetActorGuid { get; set; }

        /// <summary>The slot index within the target CoverLink.</summary>
        public int TargetSlotIdx { get; set; }

        /// <summary>Resolved at post-load time: the <see cref="CoverSlotMarker"/> node for the target slot, or <see langword="null"/> if unresolvable.</summary>
        public CoverSlotMarker? ResolvedTargetMarker { get; set; }

        public static ExposedLink FromStruct(int value, IMEPackage package, Level level)
        {
            var exposureScale = (value & unchecked((int)0xFFFF0000)) >> 16;
            var covRefIdx = value & 0x0000FFFF;

            ExportEntry? targetCoverLinkExport = null;
            Guid targetActorGuid = Guid.Empty;
            int targetSlotIdx = 0;

            if (covRefIdx < level.CoverIndexPairs.Count)
            {
                var cover = level.CoverIndexPairs[covRefIdx];
                targetSlotIdx = cover.SlotIdx;
                var coverLinkRef = level.CoverLinkRefs[(int)cover.CoverIndexIdx];
                if (coverLinkRef > 0)
                {
                    targetCoverLinkExport = package.GetUExport(coverLinkRef);
                }
                else
                {
                    targetActorGuid = level.CrossLevelCoverGuidRefs
                        .FirstOrDefault(x => x.CoverIndexIdx == (int)cover.CoverIndexIdx).Guid;
                }
            }

            return new ExposedLink
            {
                ExposureScale = exposureScale,
                TargetCoverLinkExport = targetCoverLinkExport,
                TargetActorGuid = targetActorGuid,
                TargetSlotIdx = targetSlotIdx,
            };
        }

        internal void ResolvePostLoad(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            if (TargetCoverLinkExport == null && TargetActorGuid != Guid.Empty)
            {
                if (crossLevelLookup.TryGetValue(TargetActorGuid, out var resolvedNode) && resolvedNode is CoverLink targetLink)
                {
                    if (TargetSlotIdx < targetLink.Slots.Count)
                    {
                        var targetSlot = targetLink.Slots[TargetSlotIdx];
                        if (targetSlot.SlotMarker != null && exportLookupMap.TryGetValue(targetSlot.SlotMarker, out var resolvedMarker))
                        {
                            ResolvedTargetMarker = resolvedMarker as CoverSlotMarker;
                        }
                        else
                        {
                            Debug.WriteLine("ExposedLink: Failed to resolve cross-level target marker.");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("ExposedLink: Failed to resolve cross-level cover link.");
                }
            }
            else if (TargetCoverLinkExport != null)
            {
                if (exportLookupMap.TryGetValue(TargetCoverLinkExport, out var resolvedNode) && resolvedNode is CoverLink targetLink)
                {
                    if (TargetSlotIdx < targetLink.Slots.Count)
                    {
                        var targetSlot = targetLink.Slots[TargetSlotIdx];
                        if (targetSlot.SlotMarker != null && exportLookupMap.TryGetValue(targetSlot.SlotMarker, out var resolvedMarker))
                        {
                            ResolvedTargetMarker = resolvedMarker as CoverSlotMarker;
                        }
                        else
                        {
                            Debug.WriteLine("ExposedLink: Failed to resolve in-file target marker.");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("ExposedLink: Failed to resolve in-file cover link.");
                }
            }
        }
    }
}
