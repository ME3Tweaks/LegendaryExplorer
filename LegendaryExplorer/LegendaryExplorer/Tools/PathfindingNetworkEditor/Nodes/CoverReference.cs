using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// A reference to a cover slot, used in MantleTarget, SlipTarget, SlipRefs, and OverlapClaims.
    /// </summary>
    public class CoverReference
    {
        /// <summary>Packed slot index.</summary>
        public int SlotIdx { get; set; }

        /// <summary>Direction value associated with the reference.</summary>
        public int Direction { get; set; }

        /// <summary>Cross-level GUID (A/B/C/D).</summary>
        public Guid Guid { get; set; }

        /// <summary>The actor this reference points to, or <see langword="null"/> for None.</summary>
        public ExportEntry? Actor { get; set; }

        public static CoverReference FromStruct(StructProperty structProp, IMEPackage package)
        {
            var props = structProp.Properties;

            ExportEntry? actor = null;
            var actorProp = props.GetProp<ObjectProperty>("Actor");
            if (actorProp?.Value > 0)
                actor = package.GetUExport(actorProp.Value);

            return new CoverReference
            {
                SlotIdx    = props.GetProp<IntProperty>("SlotIdx")?.Value ?? 0,
                Direction  = props.GetProp<IntProperty>("Direction")?.Value ?? 0,
                Guid       = CommonStructs.GetGuid(props.GetProp<StructProperty>("Guid")),
                Actor      = actor,
            };
        }
    }
}
