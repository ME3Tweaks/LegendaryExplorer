using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.ReachSpecs
{
    /// <summary>
    /// Base clas for all ReachSpecs
    /// </summary>
    public class ReachSpec
    {
        public ReachSpec() { }

        /// <summary>
        /// This ReachSpec export
        /// </summary>
        public ExportEntry SpecExport;

        /// <summary>
        ///Starting node export for the reachspec
        /// </summary>
        public ExportEntry StartExport;

        /// <summary>
        /// Destination node export for the reachspec
        /// </summary>
        public ExportEntry TargetExport;

        /// <summary>
        /// Starting Node. Resolved once the pathfinding network's contents are populated.
        /// </summary>
        public NavigationPoint SourceNode;

        /// <summary>
        /// Destination Node. Resolved once the pathfinding network's contents are populated.
        /// </summary>
        public NavigationPoint DestNode;

        /// <summary>
        /// Destination Node Guid. Will be empty if the target is an existing export in the local package. Will resolved to DestNode later.
        /// </summary>
        public Guid DestGuid;

        /// <summary>
        /// Distance to connect to the target node
        /// </summary>
        public float Distance;

        /// <summary>
        /// Direction for this reach spec
        /// </summary>
        public Vector3 Direction;

        /// <summary>
        /// Radial width that can move through this reachspec
        /// </summary>
        public int CollisionRadius;

        /// <summary>
        /// Height that can move through this reachspec.
        /// </summary>
        public int CollisionHeight;

        public ReachSpec(ExportEntry export)
        {
            SpecExport = export;

            var props = export.GetProperties();
            var start = props.GetProp<ObjectProperty>("Start");
            StartExport = export.FileRef.GetUExport(start.Value);
            var end = props.GetProp<StructProperty>("End");
            if (end != null)
            {
                if (export.Game == MEGame.ME1)
                {
                    // NavReference
                    var endActor = end.GetProp<ObjectProperty>("Nav");
                    if (endActor != null && endActor.Value != 0)
                    {
                        // Should be local export. It should never be an import.
                        TargetExport = export.FileRef.GetUExport(endActor.Value);
                        // Should we read DestGuid here anyway for faster lookup later?
                    }
                    else
                    {
                        // It's a cross level guid, which we resolve later.
                        DestGuid = CommonStructs.GetGuid(end.Properties.GetProp<StructProperty>("Guid"));
                    }
                }
                else
                {
                    //ActorReference
                    var endActor = end.GetProp<ObjectProperty>("Actor");
                    if (endActor.Value != 0)
                    {
                        // Should be local export. It should never be an import.
                        TargetExport = export.FileRef.GetUExport(endActor.Value);
                        // Should we read DestGuid here anyway for faster lookup later?
                    }
                    else
                    {
                        // It's a cross level guid, which we resolve later.
                        DestGuid = CommonStructs.GetGuid(end.Properties.GetProp<StructProperty>("Guid"));
                    }
                }
            }
            else
            {
                Debug.WriteLine($"Invalid ReachSpec: {export.MemoryFullPath}");
            }

            CollisionRadius = props.GetProp<IntProperty>("CollisionRadius");
            CollisionHeight = props.GetProp<IntProperty>("CollisionHeight");
            Distance = props.GetProp<IntProperty>("Distance");
        }

        /// <summary>
        /// Computes the direction and distance for the ReachSpec.
        /// </summary>
        public void ComputeReachSpec()
        {
            // Calculate distance.
            double deltaX = SourceNode.X - DestNode.X;
            double deltaY = SourceNode.Y - DestNode.Y;
            double deltaZ = SourceNode.Z - DestNode.Z;

            Distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);

            if (Distance != 0)
            {
                float dirX = (float)((DestNode.X - SourceNode.X) / Distance);
                float dirY = (float)((DestNode.Y - SourceNode.Y) / Distance);
                float dirZ = (float)((DestNode.Z - SourceNode.Z) / Distance);
                Direction = new Vector3(dirX, dirY, dirZ);
            }
            else
            {
                Direction = new Vector3();
            }
        }

        /// <summary>
        /// Indicates if there is a identical returning reachspec.
        /// </summary>
        /// <returns></returns>
        public bool IsTwoWayReachSpec()
        {
            return true;
        }

        /// <summary>
        /// Constructs a ReachSpec for an export.
        /// </summary>
        /// <param name="rsExport"></param>
        /// <returns></returns>
        internal static ReachSpec Generate(ExportEntry rsExport)
        {
            ReachSpec gen = null;
            switch (rsExport.ClassName)
            {
                case "ReachSpec":
                    gen = new ReachSpec(rsExport);
                    break;
                case "SlotToSlotReachSpec":
                    gen = new SlotToSlotReachSpec(rsExport);
                    break;
                case "MantleReachSpec":
                    gen = new MantleReachSpec(rsExport);
                    break;
                case "CoverSlipReachSpec":
                    gen = new CoverSlipReachSpec(rsExport);
                    break;
                default:
                    gen = new UnknownReachSpec(rsExport);
                    break;
            }

            return gen;
        }

        /// <summary>
        /// Generates the connection to the destination node and places it on the source node's connections list.
        /// </summary>
        /// <param name="crossLevelLookup"></param>
        public void GenerateConnection(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            if (SourceNode == null)
            {
                // SourceExport should always be set
                SourceNode = exportLookupMap[StartExport];
            }

            if (DestNode == null)
            {
                if (TargetExport != null)
                {
                    DestNode = exportLookupMap[TargetExport];
                }
                else
                {
                    DestNode = crossLevelLookup[DestGuid];
                }
            }

            if (SourceNode != null && DestNode != null)
            {
                SourceNode.Connections.Add(InternalGenerateConnection());
            }
        }

        protected virtual GraphConnection InternalGenerateConnection()
        {
            // Basic connection.
            return new GraphConnection(SourceNode, DestNode);
        }
    }
}
