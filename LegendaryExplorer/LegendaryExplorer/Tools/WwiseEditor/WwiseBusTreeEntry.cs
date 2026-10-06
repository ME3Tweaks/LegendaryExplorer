using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using WwiseStreamHelper = LegendaryExplorer.UnrealExtensions.AudioStreamHelper;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    /// <summary>
    /// Node in the bus parent/child hierarchy tree shown alongside the graph.
    /// Wwise bus names are not stored in cooked banks (only their FNV-1 ID is), so buses are labelled by ID.
    /// </summary>
    public sealed class WwiseBusTreeEntry
    {
        public uint ID { get; }
        public HIRCType Type { get; }
        public string DisplayName { get; }
        public ObservableCollectionExtended<WwiseBusTreeEntry> Children { get; } = new();

        public WwiseBusTreeEntry(WwiseBankParsed.Bus bus, string resolvedName)
        {
            ID = bus.ID;
            Type = bus.Type;
            string typeStr = WwiseStreamHelper.GetHircObjTypeString(bus.Type);
            DisplayName = resolvedName != null
                ? $"{resolvedName} [{typeStr}] ({WwiseHircObjNode.GetIDString(bus.ID)})"
                : $"{typeStr} ({WwiseHircObjNode.GetIDString(bus.ID)})";
        }
    }
}
