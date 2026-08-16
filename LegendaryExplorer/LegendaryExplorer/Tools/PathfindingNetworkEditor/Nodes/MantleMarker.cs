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
    /// MantleMarker are the nodes where AI and players can mantle.
    /// </summary>
    public class MantleMarker : NavigationPoint
    {
        public MantleMarker(ExportEntry export) : base(export)
        {
        }

        internal override void ReadData(ExportEntry export, PropertyCollection props, LegendaryExplorerCore.Unreal.BinaryConverters.Level level)
        {
            base.ReadData(export, props, level);
            Shape = NodeShape.Mantle;
            BorderColor = Colors.Red;
            BackgroundColor = Colors.Orange;
        }
    }
}
