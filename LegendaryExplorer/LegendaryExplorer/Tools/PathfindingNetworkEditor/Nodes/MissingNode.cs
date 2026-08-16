using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// A wild MissingNo appears!
    /// </summary>
    public class MissingNode : NavigationPoint
    {
        public MissingNode(ExportEntry export) : base(export) { }

        internal override void ReadData(ExportEntry export, PropertyCollection props, LegendaryExplorerCore.Unreal.BinaryConverters.Level level)
        {
            base.ReadData(export, props, level);

            Shape = NodeShape.Hexagon;
            BackgroundColor = Colors.Red;
            Label += $" ({export.ClassName})";
        }
    }
}
