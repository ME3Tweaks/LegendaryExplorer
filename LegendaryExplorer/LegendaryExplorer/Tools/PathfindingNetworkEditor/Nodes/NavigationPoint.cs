using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.ReachSpecs;
using LegendaryExplorerCore.Misc;
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
    /// Base class for all navigation points in the pathfinding network. This is a very basic class that can be extended with additional properties and functionality as needed.
    /// </summary>
    public class NavigationPoint : GraphNode
    {
        /// <summary>
        /// The export associated with this node.
        /// </summary>
        public ExportEntry Export;

        /// <summary>
        /// Guid associated with this node.
        /// </summary>
        public Guid NavigationGuid;

        /// <summary>
        /// Cylinder collision radius read from the actor's properties.
        /// </summary>
        public float MaxPathRadius { get; private set; }

        /// <summary>
        /// Cylinder collision height read from the actor's properties.
        /// </summary>
        public float MaxPathHeight { get; private set; }

        /// <summary>
        /// Actor rotation yaw in Unreal rotation units (0–65535 maps to 0°–360°).
        /// </summary>
        public virtual int RotationYaw { get; protected set; }

        private Color _rotationArrowColor = Color.FromArgb(110, 155, 155, 255);
        /// <summary>
        /// Color of the rotation arrow.
        /// </summary>
        public Color RotationArrowColor
        {
            get => _rotationArrowColor;
            set { if (_rotationArrowColor != value) { _rotationArrowColor = value; OnPropertyChanged(); } }
        }

        public NavigationPoint() { }
        public NavigationPoint(ExportEntry export)
        {
            Export = export;
        }

        /// <summary>
        /// List of outbound connections from this node.
        /// </summary>
        public ObservableCollectionExtended<ReachSpec> ReachSpecs { get; } = new ObservableCollectionExtended<ReachSpec>();

        internal virtual void ReadData(ExportEntry export, PropertyCollection props, Level level)
        {
            var location = props.GetProp<StructProperty>("Location");
            X = location?.Properties.GetProp<FloatProperty>("X").Value ?? 0;
            Y = location?.Properties.GetProp<FloatProperty>("Y").Value ?? 0;
            Z = location?.Properties.GetProp<FloatProperty>("Z").Value ?? 0;

            NavigationGuid = CommonStructs.GetGuid(props.GetProp<StructProperty>("NavGuid"));
            var maxPathSize = CommonStructs.GetCylinder(props.GetProp<StructProperty>("MaxPathSize"));
            MaxPathRadius = maxPathSize.Radius;
            MaxPathHeight = maxPathSize.Height;


            var rotationProp = props.GetProp<StructProperty>("Rotation");
            RotationYaw = rotationProp?.GetProp<IntProperty>("Yaw")?.Value ?? 0;

            var pathlist = props.GetProp<ArrayProperty<ObjectProperty>>("PathList");
            if (pathlist != null)
            {
                foreach (var connection in pathlist)
                {
                    var rsExport = export.FileRef.GetUExport(connection.Value);
                    // rsExport is the ReachSpec export.
                    var spec = ReachSpec.Generate(rsExport);

                    ReachSpecs.Add(spec);
                }
            }

            Label = export.UIndex.ToString();
        }

        /// <summary>
        /// Invoked once all nodes have been generated
        /// </summary>
        internal virtual void ResolvePostLoad(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            // Base does nothing.
        }

        internal virtual void Highlight() { }

        internal virtual void Unhighlight()
        {
            TemporaryBorderColor = null;
            TemporaryBackgroundColor = null;
        }


        // GRAPH EDITOR CODE
        /// <summary>
        /// List of connections on the graph.
        /// </summary>
        public List<GraphConnection> Connections = new List<GraphConnection>();

        /// <summary>
        /// Temporary connections added to the graph when this node is selected (e.g., fire link visualizations on a CoverSlotMarker).
        /// </summary>
        public List<GraphConnection> TemporaryConnections { get; } = new();

        /// <summary>
        /// Adds a temporary connection for this node.
        /// </summary>
        public void AddTemporaryConnection(GraphConnection connection)
        {
            TemporaryConnections.Add(connection);
        }

        /// <summary>
        /// Clears all temporary connections, invoking <paramref name="removeFromGraph"/> for each to remove it from the graph.
        /// </summary>
        public void ClearTemporaryConnections(Action<GraphConnection> removeFromGraph)
        {
            foreach (var conn in TemporaryConnections)
                removeFromGraph(conn);
            TemporaryConnections.Clear();
        }
    }
}
