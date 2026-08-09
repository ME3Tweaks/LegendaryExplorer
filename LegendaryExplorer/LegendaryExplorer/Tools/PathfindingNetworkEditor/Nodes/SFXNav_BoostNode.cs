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
    /// SFXNav_BoostNode are the nodes where AI and players can mantle.
    /// </summary>
    public class SFXNav_BoostNode : NavigationPoint
    {
        public SFXNav_BoostNode(ExportEntry export) : base(export) { }

        /// <summary>
        /// If this is the top or bottom of the boost paths.
        /// </summary>
        public bool IsTopNode { get; set; }
        internal override void ReadData(ExportEntry export, PropertyCollection props, LegendaryExplorerCore.Unreal.BinaryConverters.Level level)
        {
            base.ReadData(export, props, level);
            IsTopNode = props.GetProp<BoolProperty>("bTopNode")?.Value ?? false;
            Shape = IsTopNode ? NodeShape.BoostDown : NodeShape.BoostUp;
            BorderColor = Colors.Red;
            BackgroundColor = Colors.Orange;
        }
    }
}
