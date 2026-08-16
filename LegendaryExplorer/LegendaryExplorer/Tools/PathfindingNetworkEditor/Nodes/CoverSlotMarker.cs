using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// CoverSlotMarker are the nodes where AI and players can take cover.
    /// </summary>
    public class CoverSlotMarker : NavigationPoint
    {
        public CoverSlotMarker(ExportEntry export) : base(export) { }

        internal CoverLink OwningNode;

        private ExportEntry OwningLink;
        public int OwningLinkSlotIdx;

        /*
        public override int RotationYaw
        {
            get
            {
                var yaw = base.RotationYaw;
                if (OwningNode != null && OwningLinkSlotIdx >= 0 && OwningLinkSlotIdx < OwningNode.Slots.Count)
                {
                    yaw -= OwningNode.Slots[OwningLinkSlotIdx].RotationOffset.Yaw;
                }
                return yaw;
            }
        }*/

        internal override void ReadData(ExportEntry export, PropertyCollection props, Level level)
        {
            base.ReadData(export, props, level);
            Shape = NodeShape.CoverSlotMarker;
            BorderColor = Colors.BlanchedAlmond;
            BackgroundColor = Colors.LightCyan;

            var owningSlot = props.GetProp<StructProperty>("OwningSlot")?.Properties;
            if (owningSlot != null)
            {
                var linkIdx = owningSlot.GetProp<ObjectProperty>("Link")?.Value ?? 0;
                if (linkIdx > 0)
                {
                    OwningLink = export.FileRef.GetUExport(linkIdx);
                }
                OwningLinkSlotIdx = owningSlot.GetProp<IntProperty>("SlotIdx")?.Value ?? 0;
            }
        }

        internal override void ResolvePostLoad(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            base.ResolvePostLoad(exportLookupMap, crossLevelLookup);

            if (OwningLink != null && exportLookupMap.TryGetValue(OwningLink, out var owner))
            {
                OwningNode = owner as CoverLink;
            }

            OnPropertyChanged(nameof(RotationYaw));
            // SlotIdx is OwningNode's index into its Slots array.
        }

        internal override void Highlight()
        {
            base.Highlight();

            TemporaryBackgroundColor = Colors.Orange;
            TemporaryBorderColor = Colors.Goldenrod;
        }
    }
}
