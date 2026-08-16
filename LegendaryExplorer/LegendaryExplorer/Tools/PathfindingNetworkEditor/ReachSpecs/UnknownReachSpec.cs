using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorerCore.Packages;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.ReachSpecs
{
    public class UnknownReachSpec : ReachSpec
    {
        public UnknownReachSpec(ExportEntry export) : base(export)
        {

        }

        protected override GraphConnection InternalGenerateConnection()
        {
            var baseConnection = base.InternalGenerateConnection();
            baseConnection.LineColor = Colors.Red;
            return baseConnection;
        }
    }
}
