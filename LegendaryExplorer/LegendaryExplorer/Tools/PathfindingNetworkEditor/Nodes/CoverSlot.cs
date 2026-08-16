using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// Represents a single cover slot within a <see cref="CoverLink"/>.
    /// </summary>
    public class CoverSlot
    {
        // ---------------------------------------------------------------
        //  AI action and fire-link data
        // ---------------------------------------------------------------

        /// <summary>Cover actions available from this slot.</summary>
        public List<ECoverAction> Actions { get; set; } = [];

        /// <summary>Fire links targeting other cover from this slot.</summary>
        public List<EFireLink> FireLinks { get; set; } = [];

        /// <summary>Packed exposure values used for cover-quality evaluation.</summary>
        public List<ExposedLink> ExposedCovers { get; set; } = [];

        /// <summary>Packed danger values for evaluating nearby threats.</summary>
        public List<DangerNav> DangerNavs { get; set; } = [];

        // ---------------------------------------------------------------
        //  Cover references
        // ---------------------------------------------------------------

        /// <summary>Slip/mantle target slots reachable from here.</summary>
        public List<CoverReference> SlipTarget { get; set; } = [];

        /// <summary>Incoming slip references into this slot.</summary>
        public List<CoverReference> SlipRefs { get; set; } = [];

        /// <summary>Other slots that overlap with this one.</summary>
        public List<CoverReference> OverlapClaims { get; set; } = [];

        /// <summary>Target slot for a mantle move, or <see langword="null"/> if none.</summary>
        public CoverReference? MantleTarget { get; set; }

        // ---------------------------------------------------------------
        //  Spatial data
        // ---------------------------------------------------------------

        /// <summary>Position offset from the owning CoverLink's location.</summary>
        public Vector3 LocationOffset { get; set; }

        /// <summary>Rotation offset from the owning CoverLink's rotation.</summary>
        public Rotator RotationOffset { get; set; }

        // ---------------------------------------------------------------
        //  Actor references
        // ---------------------------------------------------------------

        /// <summary>Pawn currently claiming this slot, or <see langword="null"/>. This is transient and should always be null outside of the game.</summary>
        public ExportEntry? SlotOwner { get; set; }

        /// <summary>The CoverSlotMarker actor associated with this slot.</summary>
        public ExportEntry? SlotMarker { get; set; }

        // ---------------------------------------------------------------
        //  Packed properties
        // ---------------------------------------------------------------

        public int TurnTargetPackedProperties { get; set; }
        public int CoverTurnTargetPackedProperties { get; set; }

        // ---------------------------------------------------------------
        // Integers
        // ---------------------------------------------------------------
        public int ExtraCost { get; set; }
        public float LeanTraceDist { get; set; }

        // ---------------------------------------------------------------
        //  Capability flags
        // ---------------------------------------------------------------

        public bool bLeanLeft { get; set; }
        public bool bLeanRight { get; set; }
        public bool bForceCanPopUp { get; set; }
        public bool bCanPopUp { get; set; }
        public bool bCanMantle { get; set; }
        public bool bCanClimbUp { get; set; }
        public bool bForceCanCoverSlip_Left { get; set; }
        public bool bForceCanCoverSlip_Right { get; set; }
        public bool bCanCoverSlip_Left { get; set; }
        public bool bCanCoverSlip_Right { get; set; }
        public bool bCanSwatTurn_Left { get; set; }
        public bool bCanSwatTurn_Right { get; set; }
        public bool bCanCoverTurn_Left { get; set; }
        public bool bCanCoverTurn_Right { get; set; }

        // ---------------------------------------------------------------
        //  Allow flags
        // ---------------------------------------------------------------

        public bool bEnabled { get; set; }
        public bool bAllowPopup { get; set; }
        public bool bAllowMantle { get; set; }
        public bool bAllowCoverSlip { get; set; }
        public bool bAllowClimbUp { get; set; }
        public bool bAllowSwatTurn { get; set; }
        public bool bAllowCoverTurn { get; set; }
        public bool bForceNoGroundAdjust { get; set; }
        public bool bPlayerOnly { get; set; }
        public bool bUnsafeCover { get; set; }
        public bool bFailedToFindSurface { get; set; }

        // ---------------------------------------------------------------
        //  Cover type classification
        // ---------------------------------------------------------------

        public ECoverType ForceCoverType { get; set; }
        public ECoverType CoverType { get; set; }
        public ECoverLocationDescription LocationDescription { get; set; }

        // ---------------------------------------------------------------
        //  Factory
        // ---------------------------------------------------------------

        public static CoverSlot FromStruct(StructProperty slotStruct, IMEPackage package, Level level)
        {
            var p = slotStruct.Properties;

            // Local helpers to keep the initializer below compact.
            static bool Bool(PropertyCollection props, string name) =>
                props.GetProp<BoolProperty>(name)?.Value ?? false;

            static T ParseEnum<T>(PropertyCollection props, string name) where T : struct, Enum
            {
                Enum.TryParse<T>(props.GetProp<EnumProperty>(name)?.Value.Name ?? string.Empty, out var val);
                return val;
            }

            static ExportEntry? Actor(PropertyCollection props, string name, IMEPackage pkg)
            {
                var op = props.GetProp<ObjectProperty>(name);
                return op?.Value > 0 ? pkg.GetUExport(op.Value) : null;
            }

            static List<CoverReference> CoverRefs(PropertyCollection props, string name, IMEPackage pkg)
            {
                var result = new List<CoverReference>();
                if (props.GetProp<ArrayProperty<StructProperty>>(name) is { } arr)
                    foreach (var s in arr)
                        result.Add(CoverReference.FromStruct(s, pkg));
                return result;
            }

            // Actions
            var actions = new List<ECoverAction>();
            if (p.GetProp<ArrayProperty<EnumProperty>>("Actions") is { } actionsProp)
                foreach (var e in actionsProp)
                    if (Enum.TryParse<ECoverAction>(e.Value.Name, out var action))
                        actions.Add(action);

            // FireLinks
            var fireLinks = new List<EFireLink>();
            if (p.GetProp<ArrayProperty<StructProperty>>("FireLinks") is { } fireLinksProp)
            {
                foreach (var s in fireLinksProp)
                {
                    fireLinks.Add(EFireLink.FromStruct(s, package, level));
                }
            }

            // MantleTarget (single struct, not an array)
            CoverReference? mantleTarget = null;
            if (p.GetProp<StructProperty>("MantleTarget") is { } mtProp)
                mantleTarget = CoverReference.FromStruct(mtProp, package);

            // Spatial
            var locationOffset = p.GetProp<StructProperty>("LocationOffset") is { } loProp
                ? CommonStructs.GetVector3(loProp) : Vector3.Zero;
            var rotationOffset = p.GetProp<StructProperty>("RotationOffset") is { } roProp
                ? CommonStructs.GetRotator(roProp) : default;

            // Exposed cover
            var exposedCovers = new List<ExposedLink>();
            if (p.GetProp<ArrayProperty<IntProperty>>("ExposedCoverPackedProperties") is { } exposedCoverPackedProperties)
            {
                foreach (var s in exposedCoverPackedProperties)
                {
                    exposedCovers.Add(ExposedLink.FromStruct(s, package, level));
                }
            }

            var dangerNavs = new List<DangerNav>();
            if (p.GetProp<ArrayProperty<IntProperty>>("DangerCoverPackedProperties") is { } dangerNavsPackedProperties)
            {
                foreach (var s in dangerNavsPackedProperties)
                {
                    dangerNavs.Add(DangerNav.FromStruct(s, package, level));
                }
            }

            return new CoverSlot
            {
                Actions = actions,
                FireLinks = fireLinks,
                ExposedCovers = exposedCovers,
                DangerNavs = dangerNavs,
                SlipTarget = CoverRefs(p, "SlipTarget", package),
                SlipRefs = CoverRefs(p, "SlipRefs", package),
                OverlapClaims = CoverRefs(p, "OverlapClaims", package),
                MantleTarget = mantleTarget,
                LocationOffset = locationOffset,
                RotationOffset = rotationOffset,
                SlotOwner = Actor(p, "SlotOwner", package),
                SlotMarker = Actor(p, "SlotMarker", package),
                TurnTargetPackedProperties = p.GetProp<IntProperty>("TurnTargetPackedProperties")?.Value ?? 0,
                CoverTurnTargetPackedProperties = p.GetProp<IntProperty>("CoverTurnTargetPackedProperties")?.Value ?? 0,
                ExtraCost = p.GetProp<IntProperty>("ExtraCost")?.Value ?? 0,
                LeanTraceDist = p.GetProp<FloatProperty>("LeanTraceDist")?.Value ?? 0f,
                bLeanLeft = Bool(p, "bLeanLeft"),
                bLeanRight = Bool(p, "bLeanRight"),
                bForceCanPopUp = Bool(p, "bForceCanPopUp"),
                bCanPopUp = Bool(p, "bCanPopUp"),
                bCanMantle = Bool(p, "bCanMantle"),
                bCanClimbUp = Bool(p, "bCanClimbUp"),
                bForceCanCoverSlip_Left = Bool(p, "bForceCanCoverSlip_Left"),
                bForceCanCoverSlip_Right = Bool(p, "bForceCanCoverSlip_Right"),
                bCanCoverSlip_Left = Bool(p, "bCanCoverSlip_Left"),
                bCanCoverSlip_Right = Bool(p, "bCanCoverSlip_Right"),
                bCanSwatTurn_Left = Bool(p, "bCanSwatTurn_Left"),
                bCanSwatTurn_Right = Bool(p, "bCanSwatTurn_Right"),
                bCanCoverTurn_Left = Bool(p, "bCanCoverTurn_Left"),
                bCanCoverTurn_Right = Bool(p, "bCanCoverTurn_Right"),
                bEnabled = Bool(p, "bEnabled"),
                bAllowPopup = Bool(p, "bAllowPopup"),
                bAllowMantle = Bool(p, "bAllowMantle"),
                bAllowCoverSlip = Bool(p, "bAllowCoverSlip"),
                bAllowClimbUp = Bool(p, "bAllowClimbUp"),
                bAllowSwatTurn = Bool(p, "bAllowSwatTurn"),
                bAllowCoverTurn = Bool(p, "bAllowCoverTurn"),
                bForceNoGroundAdjust = Bool(p, "bForceNoGroundAdjust"),
                bPlayerOnly = Bool(p, "bPlayerOnly"),
                bUnsafeCover = Bool(p, "bUnsafeCover"),
                bFailedToFindSurface = Bool(p, "bFailedToFindSurface"),
                ForceCoverType = ParseEnum<ECoverType>(p, "ForceCoverType"),
                CoverType = ParseEnum<ECoverType>(p, "CoverType"),
                LocationDescription = ParseEnum<ECoverLocationDescription>(p, "LocationDescription"),
            };
        }
    }
}
