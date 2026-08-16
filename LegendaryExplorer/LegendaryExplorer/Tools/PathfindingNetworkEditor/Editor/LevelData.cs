using LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Text;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal;
using System.Diagnostics;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using System.Linq;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Editor
{
    /// <summary>
    /// Contains a single level's dataset
    /// </summary>
    [DebuggerDisplay("LevelData | {Package.FileNameNoExtension} | {PathfindingNodes.Count} nodes")]
    public class LevelData
    {
        /// <summary>
        /// The name of the level, from the pacakge.
        /// </summary>
        public string LevelName { get; }

        public string UILevelName { get; }

        /// <summary>
        /// If the level is enabled for use in the tool
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Maps all navigation points in the level by their unique identifier. This includes all nodes that are part of the pathfinding network, as well as any additional nodes that may be needed for editing purposes (e.g. missing nodes, temporary nodes, etc.). The key is the unique identifier (GUID) of the node, and the value is the corresponding NavigationPoint instance containing its properties and data. This allows for efficient lookup and management of all nodes in the level during editing operations.
        /// </summary>
        public Dictionary<Guid, NavigationPoint> PathfindingNodes { get; set; } = new Dictionary<Guid, NavigationPoint>();

        /// <summary>
        /// Package this loaded from
        /// </summary>
        public IMEPackage Package;



        public LevelData(IMEPackage package)
        {
            Package = package;
            LevelName = Package.FileNameNoExtension;
            UILevelName = LevelName.Replace("_", "__"); // So it works in menu items
            var levelBin = package.GetLevelBinary();
            LoadPathfinding(levelBin);
            IsEnabled = true;
        }

        private void LoadPathfinding(Level levelBin)
        {
            //To ensure things are correct we should load the pathfinding chain and ensure those actors are also in the level's actor list.

            var pathfindingStart = levelBin.NavListStart;
            if (pathfindingStart == 0)
            {
                //No pathfinding data in this level, just return.
                return;
            }

            var firstNode = Package.GetUExport(pathfindingStart);
            ReadPathfindingChain(firstNode, levelBin);
        }

        private void ReadPathfindingChain(ExportEntry node, Level level)
        {
            var props = node.GetProperties();
            GenerateNode(node, props, level);
            var nextNavigationPoint = props.GetProp<ObjectProperty>("nextNavigationPoint");
            if (nextNavigationPoint != null && nextNavigationPoint.Value > 0)
            {
                ReadPathfindingChain(node.FileRef.GetUExport(nextNavigationPoint.Value), level);
            }
        }

        private void GenerateNode(ExportEntry node, PropertyCollection props, Level level)
        {
            NavigationPoint np = null;
            switch (node.ClassName)
            {
                case "NavigationPoint":
                    np = new NavigationPoint(node);
                    break;
                case "PathNode":
                    np = new PathNode(node);
                    break;
                case "CoverLink":
                    np = new CoverLink(node);
                    break;
                case "CoverSlotMarker":
                    np = new CoverSlotMarker(node);
                    break;
                case "MantleMarker":
                    np = new MantleMarker(node);
                    break;
                case "SFXDoorMarker":
                    np = new SFXDoorMarker(node);
                    break;
                case "SFXNav_BoostNode":
                    np = new SFXNav_BoostNode(node);
                    break;
                case "SFXLadderNode":
                    np = new SFXNav_LadderNode(node);
                    break;
                default:
                    np = new MissingNode(node);
                    break;
            }

            np.ReadData(node, props, level);

            // Set the node.
            PathfindingNodes[np.NavigationGuid] = np;
        }

        internal IEnumerable<GraphConnection> GetConnections()
        {
            return PathfindingNodes.Values.SelectMany(x => x.Connections);
        }

        internal void BuildConnections(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            foreach(var node in PathfindingNodes.Values)
            {
                foreach(var spec in node.ReachSpecs)
                {
                    spec.GenerateConnection(exportLookupMap, crossLevelLookup);
                }
            }
        }

        internal void ResolvePostLoad(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            foreach(var np in PathfindingNodes.Values)
            {
                np.ResolvePostLoad(exportLookupMap, crossLevelLookup);
            }
        }
    }
}
