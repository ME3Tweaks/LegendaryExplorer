using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Text;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// PathNode is just the standard pathfinding node. It's effectively the same thing as NavigationPoint.
    /// </summary>
    public class PathNode : NavigationPoint
    {
        public PathNode(ExportEntry export) : base(export) { }
    }
}
