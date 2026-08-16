using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.ReachSpecs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Controls
{
    /// <summary>
    /// Displays detailed information about the selected navigation node, including
    /// collision size, reach specs, and cover-specific slot data.
    /// </summary>
    public partial class NodeInfoPanel : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ICommand NavigateToExportCommand { get; }

        private NavigationPoint? _node;

        public bool HasNode => _node != null;
        public string NodeTitle => _node != null ? $"{_node.Export.ClassName} [{_node.Export.UIndex}]" : string.Empty;
        public string ClassName => _node?.Export.ClassName ?? string.Empty;
        public string PackageName => _node?.Export.FileRef.FileNameNoExtension ?? string.Empty;
        public string FullPath => _node?.Export.InstancedFullPath ?? string.Empty;
        public string PositionText => _node != null ? $"X={_node.X:F0}  Y={_node.Y:F0}  Z={_node.Z:F0}" : string.Empty;
        public string CollisionText => _node != null ? $"R={_node.MaxPathRadius}  H={_node.MaxPathHeight}" : string.Empty;
        public string ReachSpecsHeader => _node != null ? $"Reach Specs ({_node.ReachSpecs.Count})" : "Reach Specs";
        public string CoverSlotsHeader => _node is CoverLink cl ? $"Cover Slots ({cl.Slots.Count})" : "Cover Slots";
        public bool IsCoverLink => _node is CoverLink;
        public bool IsCoverSlotMarker => _node is CoverSlotMarker;
        public string OwningCoverLinkInfo => _node is CoverSlotMarker csm && csm.OwningNode != null
            ? $"CoverLink [{csm.OwningNode.Export.UIndex}] — Slot {csm.OwningLinkSlotIdx}"
            : "Owning CoverLink not resolved";

        private IReadOnlyList<ReachSpecDisplayItem> _reachSpecItems = [];
        public IReadOnlyList<ReachSpecDisplayItem> ReachSpecItems => _reachSpecItems;

        private IReadOnlyList<CoverSlotDisplayItem> _coverSlotItems = [];
        public IReadOnlyList<CoverSlotDisplayItem> CoverSlotItems => _coverSlotItems;

        private CoverSlotDisplayItem? _owningSlotItem;
        public CoverSlotDisplayItem? OwningSlotItem => _owningSlotItem;

        public NodeInfoPanel()
        {
            NavigateToExportCommand = new RelayCommand(_ => NavigateToExport());
            InitializeComponent();
        }

        private void NavigateToExport()
        {
            if (_node?.Export != null)
            {
                var pe = new PackageEditorWindow();
                pe.Show();
                pe.LoadEntry(_node.Export);
            }
        }

        public void LoadNode(NavigationPoint? node)
        {
            _node = node;
            var reachSpecItems = new List<ReachSpecDisplayItem>();
            var coverSlotItems = new List<CoverSlotDisplayItem>();
            _owningSlotItem = null;

            if (node != null)
            {
                foreach (var rs in node.ReachSpecs)
                    reachSpecItems.Add(new ReachSpecDisplayItem(rs));

                if (node is CoverLink cl)
                {
                    for (int i = 0; i < cl.Slots.Count; i++)
                        coverSlotItems.Add(new CoverSlotDisplayItem(i, cl.Slots[i]));
                }
                else if (node is CoverSlotMarker csm && csm.OwningNode != null)
                {
                    var slots = csm.OwningNode.Slots;
                    if (csm.OwningLinkSlotIdx < slots.Count)
                        _owningSlotItem = new CoverSlotDisplayItem(csm.OwningLinkSlotIdx, slots[csm.OwningLinkSlotIdx]);
                }
            }

            _reachSpecItems = reachSpecItems;
            _coverSlotItems = coverSlotItems;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public sealed class ReachSpecDisplayItem
    {
        public string SpecType { get; }
        public string Target { get; }
        public string SizeText { get; }

        public ReachSpecDisplayItem(ReachSpec spec)
        {
            SpecType = spec.SpecExport.ClassName;
            Target = spec.DestNode != null
                ? $"{spec.DestNode.Export.UIndex} ({spec.DestNode.Export.ClassName})"
                : spec.DestGuid != Guid.Empty
                    ? $"Cross-level [{spec.DestGuid:N}]"
                    : "Unknown";
            SizeText = $"R={spec.CollisionRadius}  H={spec.CollisionHeight}  Dist={spec.Distance:F0}";
        }
    }

    public sealed class CoverSlotDisplayItem
    {
        public string Header { get; }
        public string CoverTypeText { get; }
        public string LocationDescriptionText { get; }
        public string ActionsText { get; }
        public string CapabilityFlagsText { get; }
        public string AllowFlagsText { get; }
        public string LocationOffsetText { get; }
        public string LinksText { get; }

        public CoverSlotDisplayItem(int index, CoverSlot slot)
        {
            Header = $"Slot {index} — {slot.CoverType}";
            CoverTypeText = slot.CoverType.ToString();
            LocationDescriptionText = slot.LocationDescription.ToString();
            ActionsText = slot.Actions.Count > 0 ? string.Join(", ", slot.Actions) : "None";

            var caps = new List<string>();
            if (slot.bLeanLeft) caps.Add("LeanLeft");
            if (slot.bLeanRight) caps.Add("LeanRight");
            if (slot.bCanPopUp) caps.Add("PopUp");
            if (slot.bForceCanPopUp) caps.Add("ForcePopUp");
            if (slot.bCanMantle) caps.Add("Mantle");
            if (slot.bCanClimbUp) caps.Add("ClimbUp");
            if (slot.bCanCoverSlip_Left) caps.Add("SlipLeft");
            if (slot.bCanCoverSlip_Right) caps.Add("SlipRight");
            if (slot.bForceCanCoverSlip_Left) caps.Add("ForceSlipLeft");
            if (slot.bForceCanCoverSlip_Right) caps.Add("ForceSlipRight");
            if (slot.bCanSwatTurn_Left) caps.Add("SwatLeft");
            if (slot.bCanSwatTurn_Right) caps.Add("SwatRight");
            if (slot.bCanCoverTurn_Left) caps.Add("TurnLeft");
            if (slot.bCanCoverTurn_Right) caps.Add("TurnRight");
            CapabilityFlagsText = caps.Count > 0 ? string.Join(", ", caps) : "None";

            var allows = new List<string>();
            if (slot.bEnabled) allows.Add("Enabled");
            if (slot.bAllowPopup) allows.Add("Popup");
            if (slot.bAllowMantle) allows.Add("Mantle");
            if (slot.bAllowCoverSlip) allows.Add("CoverSlip");
            if (slot.bAllowClimbUp) allows.Add("ClimbUp");
            if (slot.bAllowSwatTurn) allows.Add("SwatTurn");
            if (slot.bAllowCoverTurn) allows.Add("CoverTurn");
            if (slot.bPlayerOnly) allows.Add("PlayerOnly");
            if (slot.bUnsafeCover) allows.Add("Unsafe");
            AllowFlagsText = allows.Count > 0 ? string.Join(", ", allows) : "None";

            LocationOffsetText = $"X={slot.LocationOffset.X:F0}  Y={slot.LocationOffset.Y:F0}  Z={slot.LocationOffset.Z:F0}";

            var links = new List<string>();
            if (slot.FireLinks.Count > 0) links.Add($"{slot.FireLinks.Count} FireLink(s)");
            if (slot.SlipTarget.Count > 0) links.Add($"{slot.SlipTarget.Count} SlipTarget(s)");
            if (slot.SlipRefs.Count > 0) links.Add($"{slot.SlipRefs.Count} SlipRef(s)");
            if (slot.MantleTarget != null) links.Add("MantleTarget");
            LinksText = links.Count > 0 ? string.Join(", ", links) : "None";
        }
    }
}
