using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.ReachSpecs
{
    public class MantleReachSpec : ReachSpec
    {
        public MantleReachSpec(ExportEntry export) : base(export)
        {
        }

        protected override GraphConnection InternalGenerateConnection()
        {
            var baseConnection = base.InternalGenerateConnection();
            baseConnection.LineColor = Colors.OrangeRed;
            baseConnection.LineStyle = ConnectionLineStyle.DashDot;
            return baseConnection;
        }
    }
}
