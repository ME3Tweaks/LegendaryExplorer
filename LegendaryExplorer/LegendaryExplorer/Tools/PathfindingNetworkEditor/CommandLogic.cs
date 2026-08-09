using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Editor;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor
{
    public partial class PathfindingNetworkEditorWindow
    {

        // Commands
        public ICommand RefreshCommand { get; set; }
        public ICommand FocusGotoCommand { get; set; }
        public ICommand FocusFindCommand { get; set; }
        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand OpenOtherVersionCommand { get; set; }
        public ICommand TogglePathfindingCommand { get; set; }
        public ICommand ToggleEverythingElseCommand { get; set; }
        public ICommand ToggleActorsCommand { get; set; }
        public ICommand ToggleArtCommand { get; set; }
        public ICommand ToggleSplinesCommand { get; set; }
        public ICommand ToggleSequenceReferencesCommand { get; set; }
        public ICommand ToggleAllCollectionsCommand { get; set; }
        public ICommand ShowBioTriggerVolumesCommand { get; set; }
        public ICommand ShowBioTriggerStreamsCommand { get; set; }
        public ICommand ShowBlockingVolumesCommand { get; set; }
        public ICommand ShowDynamicVolumesCommand { get; set; }
        public ICommand ShowGenericVolumesCommand { get; set; }
        public ICommand ShowSFXBlockingVolumeLedgesCommand { get; set; }
        public ICommand ShowSFXCombatZonesCommand { get; set; }
        public ICommand ShowWwiseAudioVolumesCommand { get; set; }
        public ICommand FlipLevelCommand { get; set; }
        public ICommand BuildPathfindingChainCommand { get; set; }
        public ICommand ShowNodeSizesCommand { get; set; }
        public ICommand AddExportToLevelCommand { get; set; }
        public ICommand PopoutInterpreterCommand { get; set; }
        public ICommand NodeTypeChangeCommand { get; set; }
        public ICommand OpenRefInSequenceEditorCommand { get; set; }
        public ICommand CheckNetIndexesCommand { get; set; }
        public ICommand LoadOverlayFileCommand { get; set; }
        public ICommand CalculateInterpAgainstTargetPointCommand { get; set; }
        public ICommand RemoveAllSpotlightsCommand { get; set; }
        public ICommand TrashAndRemoveFromLevelCommand { get; set; }
        public ICommand RemoveFromLevelCommand { get; set; }
        public ICommand AddNewSplineActorToChainCommand { get; set; }
        public ICommand EditLevelLightingCommand { get; set; }
        public ICommand CommitLevelShiftsCommand { get; set; }
        public ICommand CommitLevelRotationCommand { get; set; }
        public ICommand RecookLevelCommand { get; set; }
        public ICommand TrashGroupCommand { get; set; }
        public ICommand AddAllToGroupCommand { get; set; }
        public ICommand AddToGroupCommand { get; set; }
        public ICommand RemoveFromGroupCommand { get; set; }
        public ICommand RemoveFromGroupBoxCommand { get; set; }
        public ICommand ClearGroupCommand { get; set; }
        public ICommand LoadGroupCommand { get; set; }
        public ICommand SaveGroupCommand { get; set; }
        public ICommand ShowTriggerCylindersCommand { get; set; }
        public ICommand AddAllPathnodesToBioSquadCombatCommand { get; set; }

        public ICommand ToggleLevelCommand { get; set; }
        public ICommand LoadTilesCommand { get; set; }
        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenPackage);
            ToggleLevelCommand = new RelayCommand(ToggleLevel);
            LoadTilesCommand = new GenericCommand(LoadTilesFromFolder);
            /*
            RefreshCommand = new GenericCommand(RefreshGraph, PackageIsLoaded);
            FocusGotoCommand = new GenericCommand(FocusGoto, PackageIsLoaded);
            FocusFindCommand = new GenericCommand(FocusFind, PackageIsLoaded);
            SaveCommand = new GenericCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, PackageIsLoaded);

            TogglePathfindingCommand = new GenericCommand(TogglePathfindingNodes, PackageIsLoaded);
            ToggleEverythingElseCommand = new GenericCommand(ToggleEverythingElse, PackageIsLoaded);
            ToggleActorsCommand = new GenericCommand(ToggleActors, PackageIsLoaded);
            ToggleArtCommand = new GenericCommand(ToggleArt, PackageIsLoaded);
            ToggleSplinesCommand = new GenericCommand(ToggleSplines, PackageIsLoaded);
            ToggleSequenceReferencesCommand = new GenericCommand(ToggleSequenceReferences, PackageIsLoaded);
            ToggleAllCollectionsCommand = new GenericCommand(ToggleAllCollections, PackageIsLoaded);
            ShowBioTriggerVolumesCommand = new GenericCommand(ShowBioTriggerVolumes, PackageIsLoaded);
            ShowBioTriggerStreamsCommand = new GenericCommand(ShowBioTriggerStreams, PackageIsLoaded);
            ShowBlockingVolumesCommand = new GenericCommand(ShowBlockingVolumes, PackageIsLoaded);
            ShowDynamicVolumesCommand = new GenericCommand(ShowDynamicVolumes, PackageIsLoaded);
            ShowSFXBlockingVolumeLedgesCommand = new GenericCommand(ShowSFXBlockingVolumeLedges, PackageIsLoaded);
            ShowSFXCombatZonesCommand = new GenericCommand(ShowSFXCombatZones, PackageIsLoaded);
            ShowWwiseAudioVolumesCommand = new GenericCommand(ShowWwiseAudioVolumes, PackageIsLoaded);
            ShowGenericVolumesCommand = new GenericCommand(ShowGenericVolumes, PackageIsLoaded);
            ShowTriggerCylindersCommand = new GenericCommand(ShowTriggerCylinders, PackageIsLoaded);

            FlipLevelCommand = new GenericCommand(FlipLevel, PackageIsLoaded);
            BuildPathfindingChainCommand = new GenericCommand(BuildPathfindingChainExperiment, PackageIsLoaded);

            ShowNodeSizesCommand = new GenericCommand(ToggleNodeSizesDisplay);
            AddExportToLevelCommand = new GenericCommand(AddExportToLevel, PackageIsLoaded);

            PopoutInterpreterCommand = new RelayCommand(PopoutInterpreterWPF, NodeIsSelected);
            NodeTypeChangeCommand = new GenericCommand(ChangeNodeType, CanChangeNodetype);
            OpenRefInSequenceEditorCommand = new RelayCommand(OpenRefInSequenceEditor, NodeIsSelected);
            CheckNetIndexesCommand = new GenericCommand(CheckNetIndexes, PackageIsLoaded);
            LoadOverlayFileCommand = new GenericCommand(LoadOverlay, PackageIsLoaded);
            CalculateInterpAgainstTargetPointCommand = new GenericCommand(CalculateInterpStartEndTargetpoint, TargetPointIsSelected);
            RemoveAllSpotlightsCommand = new GenericCommand(RemoveAllSpotLights, PackageIsLoaded);
            TrashAndRemoveFromLevelCommand = new GenericCommand(TrashAndRemoveFromLevel);
            RemoveFromLevelCommand = new GenericCommand(RemoveFromLevel, IsActorSelected);
            AddNewSplineActorToChainCommand = new GenericCommand(AddSplineActorToChain, IsSplineActorSelected);
            EditLevelLightingCommand = new GenericCommand(EditLevelLighting, PackageIsLoaded);
            CommitLevelShiftsCommand = new GenericCommand(CommitLevelShifts, PackageIsLoaded);
            CommitLevelRotationCommand = new GenericCommand(CommitLevelRotation, PackageIsLoaded);
            RecookLevelCommand = new GenericCommand(RecookPersistantLevel, PackageIsLoaded);
            TrashGroupCommand = new GenericCommand(TrashActorGroup, PackageIsLoaded);
            AddAllToGroupCommand = new GenericCommand(AddAllActorsToGroup, PackageIsLoaded);
            ClearGroupCommand = new GenericCommand(() => ActorGroup.ClearEx(), () => !ActorGroup.IsEmpty());
            AddToGroupCommand = new RelayCommand(AddToGroup, SelectedNodeIsNotInGroup);
            RemoveFromGroupCommand = new RelayCommand(RemoveFromGroup, SelectedNodeIsInGroup);
            RemoveFromGroupBoxCommand = new RelayCommand(RemoveFromGroup);
            LoadGroupCommand = new GenericCommand(LoadActorGroup, PackageIsLoaded);
            SaveGroupCommand = new GenericCommand(SaveActorGroup, () => !ActorGroup.IsEmpty());
            OpenOtherVersionCommand = new GenericCommand(OpenOtherVersion, () => Pcc != null && Pcc.Game.IsMEGame());
            AddAllPathnodesToBioSquadCombatCommand = new GenericCommand(AddAllPathnodesToBioSquadCombat);*/
        }

        private void ToggleLevel(object obj)
        {
            if (obj is not LevelData data)
                return;

            if (data.IsEnabled)
            {
                // Nodes must be added before connection lookups so GetNodeControl works correctly.
                GraphEditor.AddNodes(data.PathfindingNodes.Values);

                // Snapshot already-rendered pairs so we deduplicate the same way LoadFile does.
                var seenPairs = new HashSet<(GraphNode, GraphNode)>(
                    GraphEditor.ConnectionRenderer.Connections.Select(c => (c.Source, c.Target)),
                    new UnorderedNodePairComparer());

                var levelNodes = new HashSet<GraphNode>(data.PathfindingNodes.Values);
                var connectionsToAdd = new List<GraphConnection>();

                // Outgoing from this level — skip if the target node isn't in the graph yet.
                foreach (var conn in data.GetConnections())
                {
                    if (GraphEditor.GetNodeControl(conn.Target) != null && seenPairs.Add((conn.Source, conn.Target)))
                        connectionsToAdd.Add(conn);
                }

                // Incoming from other enabled levels whose target is now back in the graph.
                foreach (var other in PackageHandler.OpenLevelsList.Where(l => l.IsEnabled && l != data))
                {
                    foreach (var conn in other.GetConnections())
                    {
                        if (levelNodes.Contains(conn.Target) && seenPairs.Add((conn.Source, conn.Target)))
                            connectionsToAdd.Add(conn);
                    }
                }

                GraphEditor.AddConnections(connectionsToAdd);
            }
            else
            {
                // Materialise before clearing so the lazy LINQ query doesn't evaluate against a cleared list.
                var levelNodes = new HashSet<GraphNode>(data.PathfindingNodes.Values);
                var connectionsToKeep = GraphEditor.ConnectionRenderer.Connections
                    .Where(c => !levelNodes.Contains(c.Source) && !levelNodes.Contains(c.Target))
                    .ToList();

                // Replace in one pass → single redraw.
                GraphEditor.SetConnections(connectionsToKeep);

                foreach (var node in data.PathfindingNodes.Values)
                    GraphEditor.RemoveNode(node);
            }
        }

        /// <summary>
        /// Handles loading and storage of the level packages and their data
        /// </summary>
        public LevelMultiPackageHandler PackageHandler { get; } = new LevelMultiPackageHandler();

        /// <summary>Maps every loaded ExportEntry to its NavigationPoint node. Populated by LoadFile and kept for fast lookups after load.</summary>
        private Dictionary<ExportEntry, NavigationPoint> _exportLookupMap = new();

        /// <summary>Maps every loaded NavigationGuid to its NavigationPoint node. Populated by LoadFile and kept for fast lookups after load.</summary>
        private Dictionary<Guid, NavigationPoint> _crossLevelLookup = new();

        private void OpenRecentsFile(string filePath)
        {
            LoadFile(filePath);
        }

        private void OpenPackage()
        {
            var d = AppDirectories.GetOpenPackageDialog();
            if (d.ShowDialog() == true)
            {
#if !DEBUG
                try
                {
#endif
                LoadFile(d.FileName);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }

        /// <summary>
        /// Loads a level master and all sublevels and adds them to the network graph.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="loadPackageDelegate"></param>
        private void LoadFile(string fileName, Action loadPackageDelegate = null)
        {
            GraphEditor.ClearAll();
            PackageHandler.OpenLevelMaster(fileName, ln => BusyText = $"Loading {ln}");

            // Now that all nodes have been read, we need to have it link up all the connections, including cross-level.
            var fullCrossLevelMap = PackageHandler.OpenLevelsList.Select(x => x.PathfindingNodes);
            _crossLevelLookup = new Dictionary<Guid, NavigationPoint>(fullCrossLevelMap.Sum(x => x.Count)); // Preallocate the dictionary.

            _exportLookupMap = new Dictionary<ExportEntry, NavigationPoint>(_crossLevelLookup.Count);
            foreach (var item in fullCrossLevelMap.SelectMany(x => x.Values))
            {
                _exportLookupMap[item.Export] = item;
                _crossLevelLookup[item.NavigationGuid] = item;
            }

            foreach (var levelData in PackageHandler.OpenLevelsList)
            {
                // Resolve things that only could be done after evertyhing loaded.
                levelData.ResolvePostLoad(_exportLookupMap, _crossLevelLookup);

                // Build connections
                levelData.BuildConnections(_exportLookupMap, _crossLevelLookup);
            }


            // Add nodes to the graph...
            foreach (var levelData in PackageHandler.OpenLevelsList)
            {
                GraphEditor.AddNodes(levelData.PathfindingNodes.Values);
            }

            if (_currentCameraNode != null)
            {
                _currentCameraNode.PropertyChanged -= OnCameraNodePropertyChanged;
                _currentCameraNode = null;
            }

            if (PackageHandler.Game.HasValue)
            {
                var ipcState = PathfindingIPCHandler.GetState(PackageHandler.Game.Value);
                GraphEditor.AddNode(ipcState.Player);
                GraphEditor.AddNode(ipcState.Camera);

                _currentCameraNode = ipcState.Camera;
                _currentCameraNode.PropertyChanged += OnCameraNodePropertyChanged;
            }

            // Deduplicate connections across all levels: treat (A→B) and (B→A) as the same visual edge.
            var seenPairs = new HashSet<(GraphNode, GraphNode)>(new UnorderedNodePairComparer());
            var deduplicatedConnections = new List<GraphConnection>();
            foreach (var connection in PackageHandler.OpenLevelsList.SelectMany(l => l.GetConnections()))
            {
                if (seenPairs.Add((connection.Source, connection.Target)))
                {
                    deduplicatedConnections.Add(connection);
                }
            }
            GraphEditor.AddConnections(deduplicatedConnections);

            // Compute the Z range of all loaded nodes for the Z-filter slider.
            double minZ = double.MaxValue, maxZ = double.MinValue;
            foreach (var node in PackageHandler.OpenLevelsList.SelectMany(l => l.PathfindingNodes.Values))
            {
                if (node.Z < minZ) minZ = node.Z;
                if (node.Z > maxZ) maxZ = node.Z;
            }
            if (minZ > maxZ) { minZ = 0; maxZ = 1; }
            else if (maxZ - minZ < 1.0) { maxZ = minZ + 1; }

            // Batch-set all four Z properties and apply the filter once, avoiding
            // intermediate ApplyZFilter calls while values are partially updated.
            _graphZMin   = minZ;
            _graphZMax   = maxZ;
            _zFilterMin  = minZ;
            _zFilterMax  = maxZ;
            OnPropertyChanged(nameof(GraphZMin));
            OnPropertyChanged(nameof(GraphZMax));
            OnPropertyChanged(nameof(ZFilterMin));
            OnPropertyChanged(nameof(ZFilterMax));
            ApplyZFilter();

            GraphEditor.AnimatedZoomToFit(TimeSpan.FromMilliseconds(600));

            Recents_Control.AddRecent(fileName, false, PackageHandler.Game);
        }

        private sealed class UnorderedNodePairComparer : IEqualityComparer<(GraphNode, GraphNode)>
        {
            public bool Equals((GraphNode, GraphNode) x, (GraphNode, GraphNode) y)
            {
                return (ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2))
                    || (ReferenceEquals(x.Item1, y.Item2) && ReferenceEquals(x.Item2, y.Item1));
            }

            public int GetHashCode((GraphNode, GraphNode) obj)
            {
                // XOR is commutative so (A,B) and (B,A) hash identically.
                return obj.Item1.GetHashCode() ^ obj.Item2.GetHashCode();
            }
        }
    }
}
