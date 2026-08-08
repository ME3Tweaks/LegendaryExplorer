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
    /// SFXNav_LadderNode are the nodes where AI and players can mantle.
    /// </summary>
    public class SFXNav_LadderNode : NavigationPoint
    {
        public SFXNav_LadderNode(ExportEntry export) : base(export) { }

        public bool IsTopNode { get; private set; }

        internal override void ReadData(ExportEntry export, PropertyCollection props, LegendaryExplorerCore.Unreal.BinaryConverters.Level level)
        {
            base.ReadData(export, props, level);
            IsTopNode = props.GetProp<BoolProperty>("bTopNode")?.Value ?? false;
            Shape = IsTopNode ? NodeShape.LadderDown : NodeShape.LadderUp;
            BorderColor = Colors.Blue;
            BackgroundColor = Colors.Aqua;
        }
    }
}
