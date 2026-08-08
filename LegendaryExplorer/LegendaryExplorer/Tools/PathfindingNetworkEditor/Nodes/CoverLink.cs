using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// CoverLink is the entrypoint for AI to use cover
    /// </summary>
    public class CoverLink : NavigationPoint
    {
        public CoverLink(ExportEntry export) : base(export) { }
        public List<CoverSlot> Slots { get; internal set; } = new();
        public List<CoverSlotMarker> Markers { get; internal set; }

        internal override void ReadData(ExportEntry export, PropertyCollection props, LegendaryExplorerCore.Unreal.BinaryConverters.Level level)
        {
            base.ReadData(export, props, level);
            Shape = NodeShape.CoverLink;
            BorderColor = Colors.Brown;
            BackgroundColor = Colors.Bisque;

            if (props.GetProp<ArrayProperty<StructProperty>>("Slots") is { } slotsProp)
            {
                foreach (var slotStruct in slotsProp)
                {
                    Slots.Add(CoverSlot.FromStruct(slotStruct, export.FileRef, level));
                }
            }

            Markers = new(Slots.Count);
        }

        internal override void ResolvePostLoad(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            base.ResolvePostLoad(exportLookupMap, crossLevelLookup);

            // Resolve nodes
            foreach(var s in Slots)
            {
                if (exportLookupMap.TryGetValue(s.SlotMarker, out var slotMarker) && slotMarker is CoverSlotMarker csm)
                {
                    Markers.Add(csm);
                }
            }

            // Resolve fire link targets, exposed covers, and danger navs for each slot
            foreach (var slot in Slots)
            {
                foreach (var fireLink in slot.FireLinks)
                {
                    fireLink.ResolvePostLoad(exportLookupMap, crossLevelLookup);
                }
                foreach (var exposedLink in slot.ExposedCovers)
                {
                    exposedLink.ResolvePostLoad(exportLookupMap, crossLevelLookup);
                }
                foreach (var dangerNav in slot.DangerNavs)
                {
                    dangerNav.ResolvePostLoad(exportLookupMap, crossLevelLookup);
                }
            }
        }
    }
}
