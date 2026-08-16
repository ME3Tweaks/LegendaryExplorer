using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// SFXDoorMarker indicate where doors are at on the level.
    /// </summary>
    public class SFXDoorMarker : NavigationPoint
    {
        public SFXDoorMarker(ExportEntry export) : base(export) { }
        internal override void ReadData(ExportEntry export, PropertyCollection props, LegendaryExplorerCore.Unreal.BinaryConverters.Level level)
        {
            base.ReadData(export, props, level);
            Shape = NodeShape.SFXDoor;
            BorderColor = Colors.Brown;
            BackgroundColor = Colors.MediumSeaGreen;
        }
    }
}
