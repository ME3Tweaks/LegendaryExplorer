using LegendaryExplorer.Dialogs;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Packages;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Piccolo;
using Piccolo.Event;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using RectangleF = System.Drawing.RectangleF;

namespace LegendaryExplorer.Tools.PathfindingEditor
{
    /// <summary>
    /// Interaction logic for PathfindingEditorWindow.xaml
    /// </summary>
    public partial class PathfindingEditorWindow : WPFBase, IBusyUIHost, IRecents
    {
        #region ActorCategories
        public static readonly string[] pathfindingNodeClasses =
        {
            "PathNode", "SFXEnemySpawnPoint", "PathNode_Dynamic", "SFXNav_HarvesterMoveNode", "SFXNav_LeapNodeHumanoid",
            "MantleMarker", "SFXDynamicPathNode", "BioPathPoint", "SFXNav_LargeBoostNode", "SFXNav_LargeMantleNode",
            "SFXNav_InteractionStandGuard", "SFXNav_TurretPoint", "CoverLink", "SFXDynamicCoverLink",
            "SFXDynamicCoverSlotMarker", "SFXNav_SpawnEntrance", "SFXNav_LadderNode", "SFXDoorMarker",
            "SFXNav_JumpNode", "SFXNav_JumpDownNode", "NavigationPoint", "CoverSlotMarker", "SFXNav_BoostNode",
            "SFXNav_LargeClimbNode", "SFXNav_LargeMantleNode", "SFXNav_ClimbWallNode",
            "SFXNav_InteractionHenchOmniTool", "SFXNav_InteractionHenchOmniToolCrouch",
            "SFXNav_InteractionHenchBeckonFront", "SFXNav_InteractionHenchBeckonRear", "SFXNav_InteractionHenchCustom",
            "SFXNav_InteractionHenchCover", "SFXNav_InteractionHenchCrouch", "SFXNav_InteractionHenchInteractLow",
            "SFXNav_InteractionHenchManual", "SFXNav_InteractionHenchStandIdle", "SFXNav_InteractionHenchStandTyping",
            "SFXNav_InteractionUseConsole", "SFXNav_InteractionStandGuard", "SFXNav_InteractionHenchOmniToolCrouch",
            "SFXNav_InteractionInspectWeapon", "SFXNav_InteractionOmniToolScan","SFXNav_InteractionCannibal", "PlayerStart",
            "SFXNav_InteractionCenturion","SFXNav_InteractionGuardPose", "SFXNav_InteractionHusk","SFXNav_InteractionInspectOmniTool",
            "SFXNav_InteractionListening","SFXNav_InteractionListening2", "SFXNav_InteractionRavager","SFXNav_InteractionTalking",
            "SFXNav_InteractionTalking2","SFXNav_InteractionTalking3", "SFXNav_KaiLengShield",

            // ME2
            "SFXNav_WayPoint",

            //ME1
            "BioWp_DefensePoint", "BioWp_AssaultPoint", "BioWp_ActionStation"
        };

        public static readonly string[] actorNodeClasses =
        {
            "BioPlaypenVolumeAdditive", "DynamicBlockingVolume", "DynamicPhysicsVolume", "BioTriggerStream", "SFXCombatZone", "BioTriggerVolume", "TriggerVolume", "DynamicTriggerVolume", "PhysicsVolume", "SFXKillRagdollVolume",
            "BioTrigger", "Trigger", "Trigger_Dynamic", "Trigger_LOS",
            "SFXMedkit", "SFXMedStation", "SFXArmorNode", "SFXTreasureNode", "SFXPointOfInterest", "SFXWeaponFactory",
            "InterpActor", "KActor", "SFXKActor", "SFXDoor", "BioUseable",
            "TargetPoint", "Note", "BioMapNote", "BioStartLocation", "BioStartLocationMP",
            "SFXPlaceable_Generator", "SFXPlaceable_ShieldGenerator", "SFXPlaceable_RachniEgg", "SFXPlaceable_RottenRachniEgg", "SFXPlaceable_GethTripMine", "SFXPlaceable_Generic", "SFXPlaceable_CerberusShield", "SFXPlaceable_IndoctrinationDevice", "BioInert",
            "SFXAmmoContainer_Simulator", "SFXAmmoContainer", "SFXGrenadeContainer",
            "SFXStuntActor", "BioPawn",
            "PointLightToggleable", "PointLightMovable", "SpotLightToggleable", "DirectionalLightToggleable", "SkyLightToggleable",
            "SkeletalMeshActor",  "SkeletalMeshCinematicActor", "SkeletalMeshActorMAT", "SFXSkeletalMeshActor",  "SFXSkeletalMeshCinematicActor", "SFXSkeletalMeshActorMAT",
            "SFXOperation_ObjectiveSpawnPoint", "BioMusicVolume"
        };

        public static readonly string[] splineNodeClasses = { "SplineActor", "SplineLoftActor" };

        public static readonly string[] artNodeClasses =
        {
            "StaticMeshActor", "StaticMeshCollectionActor","StaticLightCollectionActor",
            "PointLight", "SpotLight", "DirectionalLight", "SkyLight", "LensFlareSource", "BioSunActor",
            "PostProcessVolume", "LightVolume", "LightmassImportanceVolume", "FogVolumeHalfspaceDensityInfo", "FogVolumeSphericalDensityInfo",
            "DecalActor", "Emitter", "Terrain", "HeightFog",
            "BlockingVolume", "BioBlockingVolume", "SFXBlockingVolume_Ledge",
            "WwiseAmbientSound", "WwiseAudioVolume", "WwiseEnvironmentVolume", "WwiseMusicVolume"
        };

        public static readonly string[] ignoredobjectnames =
        {
            "PREFAB_Ladders_3M_Arc0", "PREFAB_Ladders_3M_Arc1"
        }; //These come up as parsed classes but aren't actually part of the level, only prefabs. They should be ignored
        #endregion

        #region Properties and Bindings
        public static readonly string PathfindingEditorDataFolder = Path.Combine(AppDirectories.AppDataFolder, @"PathfindingEditor\");
        private bool IsCombatZonesSingleSelecting;
        private bool IsReadingLevel;

        //Layers
        private List<PathfindingNodeMaster> GraphNodes;
        private bool ChangingSelectionByGraphClick;
        private ExportEntry PersistentLevelExport;

        private readonly PathingGraphEditor graphEditor;
        private bool AllowRefresh;
        public readonly PathingZoomController zoomController;

        private string FileQueuedForLoad;
        private IMEPackage PackageQueuedForLoad;
        private ExportEntry ExportQueuedForFocus;
        public ObservableCollectionExtended<ExportEntry> ActiveNodes { get; } = new();
        public ObservableCollectionExtended<ExportEntry> ActiveOverlayNodes { get; } = new();

        public ObservableCollectionExtended<string> TagsList { get; } = new();

        public ObservableCollectionExtended<CollectionActor> CollectionActors { get; } = new();
        public ObservableCollectionExtended<Zone> CombatZones { get; } = new();

        public ObservableCollectionExtended<Zone> CurrentNodeCombatZones { get; } = new();

        private readonly List<ExportEntry> AllLevelObjects = new();
        private readonly List<ExportEntry> AllOverlayObjects = new();
        public string CurrentFile;
        private readonly PathfindingMouseListener pathfindingMouseListener;
        public static bool CanParseStatic(ExportEntry exportEntry)
        {
            return !exportEntry.IsDefaultObject && (pathfindingNodeClasses.Contains(exportEntry.ClassName)
                   || actorNodeClasses.Contains(exportEntry.ClassName) || splineNodeClasses.Contains(exportEntry.ClassName)
                   || artNodeClasses.Contains(exportEntry.ClassName) || exportEntry.ClassName == "Level");
        }
        private string _currentNodeXY = "Undefined";
        public string CurrentNodeXY
        {
            get => _currentNodeXY;
            set => SetProperty(ref _currentNodeXY, value);
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, $"{CurrentFile} {value}");
        }

        private bool _showVolumes_BioTriggerVolumes;
        private bool _showVolumes_BioTriggerStreams;
        private bool _showVolumes_BlockingVolumes;
        private bool _showVolumes_DynamicBlockingVolumes;
        private bool _showVolumes_SFXBlockingVolume_Ledges;
        private bool _showVolumes_SFXCombatZones;
        private bool _showVolumes_WwiseAudioVolumes;
        private bool _showVolumes_GenericVolumes;
        private bool _showCylinders_Triggers;
        private bool _showSeqeunceReferences;
        public bool ShowSequenceReferences
        {
            get => _showSeqeunceReferences;
            set => SetProperty(ref _showSeqeunceReferences, value);
        }
        public bool ShowVolumes_BioTriggerVolumes
        {
            get => _showVolumes_BioTriggerVolumes;
            set => SetProperty(ref _showVolumes_BioTriggerVolumes, value);
        }
        public bool ShowVolumes_BioTriggerStreams
        {
            get => _showVolumes_BioTriggerStreams;
            set => SetProperty(ref _showVolumes_BioTriggerStreams, value);
        }
        public bool ShowVolumes_BlockingVolumes
        {
            get => _showVolumes_BlockingVolumes;
            set => SetProperty(ref _showVolumes_BlockingVolumes, value);
        }
        public bool ShowVolumes_DynamicVolumes
        {
            get => _showVolumes_DynamicBlockingVolumes;
            set => SetProperty(ref _showVolumes_DynamicBlockingVolumes, value);
        }
        public bool ShowVolumes_SFXBlockingVolume_Ledges
        {
            get => _showVolumes_SFXBlockingVolume_Ledges;
            set => SetProperty(ref _showVolumes_SFXBlockingVolume_Ledges, value);
        }
        public bool ShowVolumes_SFXCombatZones
        {
            get => _showVolumes_SFXCombatZones;
            set => SetProperty(ref _showVolumes_SFXCombatZones, value);
        }
        public bool ShowVolumes_WwiseAudioVolumes
        {
            get => _showVolumes_WwiseAudioVolumes;
            set => SetProperty(ref _showVolumes_WwiseAudioVolumes, value);
        }
        public bool ShowVolumes_GenericVolumes
        {
            get => _showVolumes_GenericVolumes;
            set => SetProperty(ref _showVolumes_GenericVolumes, value);
        }
        public bool ShowCylinders_Triggers
        {
            get => _showCylinders_Triggers;
            set => SetProperty(ref _showCylinders_Triggers, value);
        }

        private bool _showActorsLayer = Settings.PathfindingEditor_ShowActorsLayer;
        private bool _showSplinesLayer = Settings.PathfindingEditor_ShowSplinesLayer;
        private bool _showPathfindingNodesLayer = Settings.PathfindingEditor_ShowPathfindingNodesLayer;
        private bool _showArtLayer = Settings.PathfindingEditor_ShowArtLayer;
        private bool _showEverythingElseLayer = Settings.PathfindingEditor_ShowEverythingElseLayer;

        public bool ShowActorsLayer
        {
            get => _showActorsLayer;
            set => SetProperty(ref _showActorsLayer, value);
        }

        public bool ShowArtLayer
        {
            get => _showArtLayer;
            set => SetProperty(ref _showArtLayer, value);
        }

        public bool ShowSplinesLayer
        {
            get => _showSplinesLayer;
            set => SetProperty(ref _showSplinesLayer, value);
        }

        public bool ShowPathfindingNodesLayer
        {
            get => _showPathfindingNodesLayer;
            set => SetProperty(ref _showPathfindingNodesLayer, value);
        }

        public bool ShowEverythingElseLayer
        {
            get => _showEverythingElseLayer;
            set => SetProperty(ref _showEverythingElseLayer, value);
        }

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

        private void LoadCommands()
        {
            RefreshCommand = new GenericCommand(RefreshGraph, PackageIsLoaded);
            FocusGotoCommand = new GenericCommand(FocusGoto, PackageIsLoaded);
            FocusFindCommand = new GenericCommand(FocusFind, PackageIsLoaded);
            OpenCommand = new GenericCommand(OpenPackage);
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
            AddAllPathnodesToBioSquadCombatCommand = new GenericCommand(AddAllPathnodesToBioSquadCombat);
        }

        private bool IsSplineActorSelected() => ActiveNodes_ListBox.SelectedItem is ExportEntry exp && exp.IsA("SplineActor");

        private bool IsActorSelected() => ActiveNodes_ListBox.SelectedItem is ExportEntry exp && (exp.IsA("Actor") || exp.IsA("Component"));

        private bool NodeIsSelected(object obj)
        {
            return ActiveNodes_ListBox.SelectedItem is ExportEntry;
        }

        private void ToggleNodeSizesDisplay()
        {
            ShowNodeSizes_MenuItem.IsChecked = !ShowNodeSizes_MenuItem.IsChecked;
            Settings.PathfindingEditor_ShowNodeSizes = ShowNodeSizes_MenuItem.IsChecked;
            RefreshGraph();
        }
        private void ShowTriggerCylinders()
        {
            foreach (var node in GraphNodes.OfType<GenericTriggerNode>())
            {
                node.SetShape(false, ShowCylinders_Triggers);
            }
            graphEditor.Refresh();
        }
        private void ShowGenericVolumes()
        {
            foreach (var node in GraphNodes.OfType<GenericVolumeNode>())
            {
                node.SetShape(ShowVolumes_GenericVolumes);
            }
            graphEditor.Refresh();
        }
        private void ShowWwiseAudioVolumes()
        {
            foreach (var node in GraphNodes.OfType<WwiseAudioVolume>())
            {
                node.SetShape(ShowVolumes_WwiseAudioVolumes);
            }
            graphEditor.Refresh();
        }

        private void ShowSFXCombatZones()
        {
            foreach (var node in GraphNodes.OfType<SFXCombatZone>())
            {
                node.SetShape(ShowVolumes_SFXCombatZones);
            }
            graphEditor.Refresh();
        }

        private void ShowSFXBlockingVolumeLedges()
        {
            foreach (var x in GraphNodes.OfType<SFXBlockingVolume_Ledge>())
            {
                x.SetShape(ShowVolumes_SFXBlockingVolume_Ledges);
            }
            graphEditor.Refresh();
        }

        private void ShowDynamicVolumes()
        {
            foreach (var x in GraphNodes.OfType<DynamicBlockingVolume>())
            {
                x.SetShape(ShowVolumes_DynamicVolumes);
            }
            foreach (var x in GraphNodes.OfType<DynamicTriggerVolume>())
            {
                x.SetShape(ShowVolumes_DynamicVolumes);
            }
            graphEditor.Refresh();
        }

        private void ShowBlockingVolumes()
        {
            foreach (var x in GraphNodes.OfType<BlockingVolume>())
            {
                x.SetShape(ShowVolumes_BlockingVolumes);
            }
            graphEditor.Refresh();
        }

        private void ShowBioTriggerVolumes()
        {
            foreach (var x in GraphNodes.OfType<BioTriggerVolume>())
            {
                x.SetShape(ShowVolumes_BioTriggerVolumes);
            }
            graphEditor.Refresh();
        }

        private void ShowBioTriggerStreams()
        {
            foreach (var x in GraphNodes.OfType<BioTriggerStream>())
            {
                x.SetShape(ShowVolumes_BioTriggerStreams);
            }
            graphEditor.Refresh();
        }

        private void ToggleActors()
        {
            ShowActorsLayer = !ShowActorsLayer;
            Settings.PathfindingEditor_ShowActorsLayer = ShowActorsLayer;
            RefreshGraph();
        }
        private void ToggleArt()
        {
            ShowArtLayer = !ShowArtLayer;
            Settings.PathfindingEditor_ShowArtLayer = ShowArtLayer;
            RefreshGraph();
        }
        private void ToggleSplines()
        {
            ShowSplinesLayer = !ShowSplinesLayer;
            Settings.PathfindingEditor_ShowSplinesLayer = ShowSplinesLayer;
            RefreshGraph();
        }
        private void ToggleEverythingElse()
        {
            ShowEverythingElseLayer = !ShowEverythingElseLayer;
            Settings.PathfindingEditor_ShowEverythingElseLayer = ShowEverythingElseLayer;
            RefreshGraph();
        }
        private void TogglePathfindingNodes()
        {
            ShowPathfindingNodesLayer = !ShowPathfindingNodesLayer;
            Settings.PathfindingEditor_ShowPathfindingNodesLayer = ShowPathfindingNodesLayer;
            RefreshGraph();
        }

        private bool PackageIsLoaded() => Pcc != null;

        private void FocusGoto() => FindByNumber_TextBox.Focus();

        private void FocusFind() => FindByTag_ComboBox.Focus();

        private string _nodeName = "Loading...";
        public string NodeName
        {
            get => _nodeName;
            set => SetProperty(ref _nodeName, value);
        }

        private string _nodeNameSubText;
        public string NodeNameSubText
        {
            get => _nodeNameSubText;
            set => SetProperty(ref _nodeNameSubText, value);
        }

        private string _lastSavedAtText;
        public string LastSavedAtText
        {
            get => _lastSavedAtText;
            set => SetProperty(ref _lastSavedAtText, value);
        }

        public string CurrentFilteringText =>
            ZFilteringMode switch
            {
                EZFilterIncludeDirection.None => "Showing all nodes",
                EZFilterIncludeDirection.Above => $"Showing all nodes above Z={ZFilteringValue}",
                EZFilterIncludeDirection.AboveEquals => $"Showing all nodes at or above Z={ZFilteringValue}",
                EZFilterIncludeDirection.Below => $"Showing all nodes below Z={ZFilteringValue}",
                EZFilterIncludeDirection.BelowEquals => $"Showing all nodes at or below Z={ZFilteringValue}",
                _ => "Unknown"
            };

        public string NodeTypeDescriptionText
        {
            get
            {
                if (ActiveNodes_ListBox?.SelectedItem is ExportEntry currentLoadedExport)
                {
                    if (PathEdUtils.ExportClassDB.FirstOrDefault(x => x.nodetypename == currentLoadedExport.ClassName) is PathfindingDB_ExportType classinfo)
                    {
                        return classinfo.description;
                    }

                    return "This node type does not have any information detailed about its purpose.";
                }

                return "No node is currently selected";
            }
        }

        private bool _splineNodeSelected;

        public bool SplineNodeSelected
        {
            get => _splineNodeSelected;
            set => SetProperty(ref _splineNodeSelected, value);
        }

        #endregion

        #region Load+I/O
        private static readonly System.Drawing.Color GraphEditorBackColor = System.Drawing.Color.FromArgb(130, 130, 130);
        public PathfindingEditorWindow() : base("Pathfinding Editor")
        {
            DataContext = this;
            StatusText = "Select package file to load";
            LoadCommands();
            InitializeComponent();
            var contextMenu = (ContextMenu)FindResource("nodeContextMenu");
            contextMenu.DataContext = this;
            graphEditor = (PathingGraphEditor)GraphHost.Child;
            graphEditor.BackColor = GraphEditorBackColor;
            AllowRefresh = true;
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, x => LoadFile(x));
            zoomController = new PathingZoomController(graphEditor);
            PathEdUtils.LoadClassesDB();
            List<PathfindingDB_ExportType> types = PathEdUtils.ExportClassDB.Where(x => x.pathnode).ToList();
            foreach (var type in types)
            {
                var nt = new NodeType(type, false);
                AvailableNodeChangeableTypes.Add(nt);
                if (type.usesbtop)
                {
                    nt = new NodeType(type, true);
                    AvailableNodeChangeableTypes.Add(nt);
                }
            }

            AvailableNodeChangeableTypes.Sort(x => x.DisplayString);
            InitializeComponent();
            pathfindingMouseListener = new PathfindingMouseListener(this); //Must be member so we can release reference
            graphEditor.AddInputEventListener(pathfindingMouseListener);
        }

        private PlayerGPSNode PlayerGPSObject;

        private DateTime LastGPSUpdate = DateTime.Now;

        private void ReceivedGameMessage(string obj)
        {
            if (PlayerGPSObject != null && (DateTime.Now - LastGPSUpdate) > TimeSpan.FromSeconds(1) && obj.StartsWith("PATHFINDING_GPS "))
            {
                obj = obj.Substring(16); // Remove prefix.
                //LastGPSUpdate = DateTime.Now;
                if (obj.StartsWith("PLAYERLOC="))
                {
                    var pos = obj.Substring(10).Split(',');
                    //Debug.WriteLine($"Updating player position to {pos[0]}, {pos[1]}");
                    PlayerGPSObject.SetOffset(new PointF(float.Parse(pos[0]), float.Parse(pos[1])));

                    var newPos = new PointF(float.Parse(pos[0]), float.Parse(pos[1]));
                    var panToRectangle = new RectangleF(newPos, new SizeF(200, 200));
                    //PlayerGPSObject.Position(new PointF(PlayerGPSObject.X, PlayerGPSObject.Y), newPos, graphEditor.Bounds, 100);
                    graphEditor.Camera.AnimateViewToCenterBounds(panToRectangle, false, 1);
                    PlayerGPSObject.SetOffset(newPos);
                    graphEditor.nodeLayer.ChildPaintInvalid = true;
                    //RefreshGraph();
                }
                else if (obj.StartsWith("PLAYERROT="))
                {
                    var rot = obj.Substring(10).Split(',');
                    //Debug.WriteLine($"Updating player rotation (yaw) to {rot[1]}");
                    PlayerGPSObject.SetYaw(int.Parse(rot[1]).UnrealRotationUnitsToDegrees());
                    graphEditor.nodeLayer.ChildPaintInvalid = true;
                }
            }
        }

        public PathfindingEditorWindow(string fileName) : this()
        {
            FileQueuedForLoad = fileName;
        }

        public PathfindingEditorWindow(IMEPackage package) : this()
        {
            PackageQueuedForLoad = package;
        }

        public PathfindingEditorWindow(ExportEntry export) : this()
        {
            FileQueuedForLoad = export.FileRef.FilePath;
            ExportQueuedForFocus = export;
        }

        private void PathfindingEditorWPF_Loaded(object sender, RoutedEventArgs e)
        {
            if (FileQueuedForLoad != null || PackageQueuedForLoad != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    if (FileQueuedForLoad != null)
                    {
                        LoadFile(FileQueuedForLoad);
                        FileQueuedForLoad = null;
                    }
                    else if (PackageQueuedForLoad != null)
                    {
                        LoadFile(PackageQueuedForLoad.FilePath, () => RegisterPackage(PackageQueuedForLoad));
                        PackageQueuedForLoad = null;
                    }

                    if (ExportQueuedForFocus != null && ExportQueuedForFocus.ClassName != "Level")
                    {
                        if (pathfindingNodeClasses.Contains(ExportQueuedForFocus.ClassName)) { ShowPathfindingNodesLayer = true; }
                        else if (actorNodeClasses.Contains(ExportQueuedForFocus.ClassName)) { ShowActorsLayer = true; }
                        else if (artNodeClasses.Contains(ExportQueuedForFocus.ClassName)) { ShowArtLayer = true; }
                        else if (splineNodeClasses.Contains(ExportQueuedForFocus.ClassName)) { ShowSplinesLayer = true; }
                        FocusNode(ExportQueuedForFocus, true, 0);
                    }
                    ExportQueuedForFocus = null;
                    Activate();
                }));
            }
        }
        private void PathfinderEditorWPF_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                Settings.Save();

                graphEditor.RemoveInputEventListener(pathfindingMouseListener);
                graphEditor.DragDrop -= GraphEditor_DragDrop;
                graphEditor.DragEnter -= GraphEditor_DragEnter;
#if DEBUG
                //graphEditor.DebugEventHandlers();
#endif
                graphEditor.Dispose();
                GraphHost.Child = null; //This seems to be required to clear OnChildGotFocus handler from WinFormsHost
                GraphHost.Dispose();
                ActiveNodes.ClearEx();
                CurrentNodeSequenceReferences.ClearEx();
                CollectionActors.ClearEx();
                CombatZones.ClearEx();
                if (GraphNodes != null)
                {
                    foreach (var node in GraphNodes)
                    {
                        node.MouseDown -= node_MouseDown;
                    }
                }
                CollectionActorEditorTab_CollectionActorEditor.UnloadExport();
                CollectionActorEditorTab_CollectionActorEditor.Dispose();
                GraphNodes?.Clear();
                graphEditor.edgeLayer.RemoveAllChildren();
                graphEditor.nodeLayer.RemoveAllChildren();
                interpreterControl.Dispose();
                PathfindingEditorWPF_ReachSpecsPanel.Dispose();
                zoomController.Dispose();
                RecentsController?.Dispose();
#if DEBUG
                //graphEditor.DebugEventHandlers();
#endif
                if (Pcc is not null && GameController.GetInteropTargetForGame(Pcc.Game) is InteropTarget interopTarget)
                {
                    GameController.GetInteropTargetForGame(Pcc.Game).GameReceiveMessage -= ReceivedGameMessage;
                }
            }
        }
        private void LoadFile(string fileName, Action loadPackageDelegate = null)
        {
            CurrentFile = null;
            ActiveNodes.ClearEx();
            CollectionActors.ClearEx();
            CombatZones.ClearEx();
            ActorGroup.ClearEx();
            GroupTag = "Tag";
            if (Pcc != null && GameController.GetInteropTargetForGame(Pcc.Game) is InteropTarget interopTarget)
            {
                // Remove existing handler in case game changes
                GameController.GetInteropTargetForGame(Pcc.Game).GameReceiveMessage -= ReceivedGameMessage;
            }

            StatusText = $"Loading {Path.GetFileName(fileName)}";
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);

            // Hack: Just use our delegate to register package so we don't have to split all of this out into pre and post load
            if (loadPackageDelegate != null)
            {
                loadPackageDelegate();
            }
            else
            {
                // Default behavior
                LoadMEPackage(fileName);
            }
            PersistentLevelExport = Pcc.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
            if (PersistentLevelExport == null)
            {
                UnLoadMEPackage();
                StatusText = "Select a package file to load";
                PathfindingEditorWPF_ReachSpecsPanel.UnloadExport();
                PathfindingEditorWPF_ValidationPanel.UnloadPackage();
                MessageBox.Show("This file does not contain a Level export.");
                return;
            }
            Mouse.OverrideCursor = Cursors.Wait;
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);

            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();

            //Update the "Loading file..." text, since drawing has to be done on the UI thread.
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Render,
                                      new Action(delegate { }));
            if (LoadPathingNodesFromLevel())
            {
                PointF graphcenter = GenerateGraph();
                if (GraphNodes.Count > 0)
                {
                    ChangingSelectionByGraphClick = true;
                    ActiveNodes_ListBox.SelectedIndex = 0;
                    var panToRectangle = new RectangleF(graphcenter, new SizeF(2000, 2000));
                    graphEditor.Camera.AnimateViewToCenterBounds(panToRectangle, true, 1000);
                    ChangingSelectionByGraphClick = false;
                }
                else
                {
                    NodeName = "No node selected";
                }
                CurrentFile = Path.GetFileName(fileName);
                RecentsController.AddRecent(fileName, false, Pcc?.Game);
                RecentsController.SaveRecentList(true);
                Title = $"Pathfinding Editor - {fileName}";
                StatusText = null; //Nothing to prepend.
                PathfindingEditorWPF_ValidationPanel.SetLevel(PersistentLevelExport);
                if (GameController.GetInteropTargetForGame(Pcc.Game) is InteropTarget interopTarget2)
                {
                    interopTarget2.GameReceiveMessage += ReceivedGameMessage;
                }
            }
            else
            {
                CurrentFile = null; //may need to expand this. idk if any level's have nothing though.
            }

            //Force cursor to change back after app is idle again
            Dispatcher.Invoke(new Action(() =>
            {
                Mouse.OverrideCursor = null;
            }), DispatcherPriority.ContextIdle, null);
        }

        /// <summary>
        /// Opens an existing package object.
        /// </summary>
        /// <param name="package"></param>
        public void LoadPackage(IMEPackage package)
        {
            // This is a hack so we don't have to do preload/load/postload
            LoadFile(package.FilePath, () => RegisterPackage(package));
        }

        private async void SavePackage()
        {
            await Pcc.SaveAsync();
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
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }
        private async void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            var d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                await Pcc.SaveAsync(d.FileName);
                MessageBox.Show("Done.");
            }
        }
        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry export)
            {
                AllowWindowRefocus = false; //prevents flicker effect when windows try to focus and then package editor activates
                var p = new PackageEditor.PackageEditorWindow();
                p.Show();
                p.LoadFile(export.FileRef.FilePath, export.UIndex);
                p.Activate(); //bring to front
            }
        }
        #endregion

        #region GraphGeneration


        /// <summary>
        /// Reads the persistent level export and loads the pathfindingnodemasters that will be used in the graph.
        /// This method will recursively call itself - do not pass in a parameter from an external call.
        /// </summary>
        /// <param name="overlayPersistentLevel"></param>
        /// <returns></returns>
        private bool LoadPathingNodesFromLevel(ExportEntry overlayPersistentLevel = null)
        {
            if (Pcc == null || PersistentLevelExport == null)
            {
                return false;
            }

            bool isOverlay = overlayPersistentLevel != null;
            ExportEntry levelToRead = overlayPersistentLevel ?? PersistentLevelExport;

            IsReadingLevel = true;
            graphEditor.UseWaitCursor = true;
            var allObjectsList = isOverlay ? AllOverlayObjects : AllLevelObjects;
            var activeObjectsList = isOverlay ? ActiveOverlayNodes : ActiveNodes;

            allObjectsList.Clear();

            var bulkActiveNodes = new List<ExportEntry>();
            //bool hasPathNode = false;
            //bool hasActorNode = false;
            //bool hasSplineNode = false;
            //bool hasEverythingElseNode = false;
            //todo: figure out a way to activate a layer if file is loading and the current views don't show anything to avoid modal dialog "nothing in this file".
            //seems like it would require two passes unless each level object type was put into a specific list and then the lists were appeneded to form the final list.
            //That would ruin ordering of exports, but does that really matter?

            Level level = levelToRead.GetBinaryData<Level>();
            foreach (int actorUIndex in level.Actors)
            {
                if (levelToRead.FileRef.IsUExport(actorUIndex))
                {
                    ExportEntry exportEntry = levelToRead.FileRef.GetUExport(actorUIndex);
                    allObjectsList.Add(exportEntry);

                    if (exportEntry.ClassName == "BioWorldInfo" || ignoredobjectnames.Contains(exportEntry.ObjectName.Name) || (HideGroup && ActorGroup.Contains(exportEntry)) || (ShowOnlyGroup && !ActorGroup.Contains(exportEntry)))
                    {
                        continue;
                    }

                    bool isParsedByExistingLayer = false;

                    if (pathfindingNodeClasses.Contains(exportEntry.ClassName))
                    {
                        isParsedByExistingLayer = true;
                        if (ShowPathfindingNodesLayer && isAllowedVisibleByZFiltering(exportEntry))
                        {
                            bulkActiveNodes.Add(exportEntry);
                        }
                    }

                    if (actorNodeClasses.Contains(exportEntry.ClassName))
                    {
                        isParsedByExistingLayer = true;
                        if (ShowActorsLayer && isAllowedVisibleByZFiltering(exportEntry))
                        {
                            bulkActiveNodes.Add(exportEntry);
                        }
                    }

                    if (artNodeClasses.Contains(exportEntry.ClassName))
                    {
                        isParsedByExistingLayer = true;
                        if (ShowArtLayer && isAllowedVisibleByZFiltering(exportEntry))
                        {
                            bulkActiveNodes.Add(exportEntry);
                        }
                    }

                    if (splineNodeClasses.Contains(exportEntry.ClassName))
                    {
                        isParsedByExistingLayer = true;

                        if (ShowSplinesLayer && isAllowedVisibleByZFiltering(exportEntry))
                        {
                            bulkActiveNodes.Add(exportEntry);
                            //var connectionsProp = exportEntry.GetProperty<ArrayProperty<StructProperty>>("Connections");
                            //if (connectionsProp != null)
                            //{
                            //    foreach (StructProperty connectionProp in connectionsProp)
                            //    {
                            //        ObjectProperty splinecomponentprop = connectionProp.GetProp<ObjectProperty>("SplineComponent");
                            //        bulkActiveNodes.Add(levelToRead.FileRef.GetUExport(splinecomponentprop.Value));
                            //    }
                            //}
                        }
                    }

                    //Don't parse SMCA or combat zones from overlays.
                    if (overlayPersistentLevel == null)
                    {
                        switch (exportEntry.ClassName)
                        {
                            case "StaticMeshCollectionActor":
                            case "StaticLightCollectionActor":
                                CollectionActors.Add(new CollectionActor(exportEntry));
                                break;
                            case "SFXCombatZone":
                            case "BioPlaypenVolumeAdditive":
                                CombatZones.Add(new Zone(exportEntry));
                                break;
                        }
                    }

                    if (ShowEverythingElseLayer && !isParsedByExistingLayer && isAllowedVisibleByZFiltering(exportEntry))
                    {
                        bulkActiveNodes.Add(exportEntry);
                    }
                }
            }

            activeObjectsList.ReplaceAll(bulkActiveNodes);

            if (OverlayPersistentLevelExport != null && overlayPersistentLevel == null)
            {
                //Recursive call of this function. It will only execute once
                LoadPathingNodesFromLevel(OverlayPersistentLevelExport);
            }

            if (overlayPersistentLevel != null)
            {
                return true; //Don't execute the rest of this function.
            }

            bool oneViewActive = ShowPathfindingNodesLayer || ShowActorsLayer || ShowEverythingElseLayer;
            if (oneViewActive && ActiveNodes.Count == 0)
            {
                //MessageBox.Show("No nodes visible with current view options.\nChange view options to see if there are any viewable nodes.");
                graphEditor.Enabled = true;
                graphEditor.UseWaitCursor = false;
                return true; //file still loaded.
            }



            graphEditor.Enabled = true;
            graphEditor.UseWaitCursor = false;
            IsReadingLevel = false;
            return true;
        }
        public PointF GenerateGraph()
        {
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            GraphNodes = new List<PathfindingNodeMaster>();

            double fullx = 0;
            double fully = 0;
            int currentcount = ActiveNodes.Count; //Some objects load additional objects. We need to count before we iterate over the graphsnode list as it may be appended to during this loop.
            for (int i = 0; i < currentcount; i++)
            {
                PointF pos = LoadObject(ActiveNodes[i]);
                fullx += pos.X;
                fully += pos.Y;
            }
            var centerpoint = new PointF((float)(fullx / GraphNodes.Count), (float)(fully / GraphNodes.Count));

            //Overlay file
            currentcount = ActiveOverlayNodes.Count;
            for (int i = 0; i < currentcount; i++)
            {
                LoadObject(ActiveOverlayNodes[i], true);
            }
            CreateConnections();


            #region Sequence References to nodes
            if (ShowSequenceReferences)
            {

                var referencemap = new Dictionary<int, List<ExportEntry>>(); //node index mapped to list of things referencing it
                foreach (ExportEntry export in Pcc.Exports)
                {

                    if (export.ClassName == "SeqEvent_Touch" || export.ClassName == "SFXSeqEvt_Touch" || export.ClassName.StartsWith("SeqVar") || export.ClassName.StartsWith("SFXSeq"))
                    {
                        var props = export.GetProperties();

                        var originator = props.GetProp<ObjectProperty>("Originator");
                        if (originator != null)
                        {
                            var uindex = originator.Value; //0-based indexing is used here
                            referencemap.AddToListAt(uindex, export);
                        }

                        var objvalue = props.GetProp<ObjectProperty>("ObjValue");
                        if (objvalue != null)
                        {
                            var uindex = objvalue.Value;
                            referencemap.AddToListAt(uindex, export);
                        }
                    }
                }

                //Add references to nodes
                foreach (PathfindingNodeMaster pnm in GraphNodes)
                {
                    if (referencemap.TryGetValue(pnm.UIndex, out List<ExportEntry> list))
                    {
                        //node is referenced
                        pnm.SequenceReferences.AddRange(list);
                        pnm.comment.Text += $"\nReferenced in {list.Count} sequence object{(list.Count != 1 ? "s" : "")}:";
                        foreach (ExportEntry x in list)
                        {
                            string shortpath = x.InstancedFullPath;
                            if (shortpath.StartsWith("TheWorld.PersistentLevel."))
                            {
                                shortpath = shortpath.Substring("TheWorld.PersistentLevel.".Length);
                            }
                            pnm.comment.Text += $"\n  {x.UIndex} {shortpath}";
                        }
                    }
                }
            }
            #endregion


            TagsList.ClearEx();
            foreach (var node in GraphNodes)
            {
                if (!node.IsOverlay)
                {
                    node.MouseDown += node_MouseDown;
                }
                if (!string.IsNullOrEmpty(node.NodeTag) && !TagsList.Contains(node.NodeTag))
                {
                    TagsList.Add(node.NodeTag);
                }
            }
            TagsList.Sort(x => x);
            if (TagsList.Count > 0)
            {
                FindByTag_ComboBox.SelectedIndex = 0;
            }
            return centerpoint;
        }
        public PointF LoadObject(ExportEntry exportToLoad, bool isFromOverlay = false)
        {
            int uindex = exportToLoad.UIndex;
            int x = 0, y = 0, z = int.MinValue;
            var props = exportToLoad.GetProperties();
            Point3D position = PathEdUtils.GetLocation(exportToLoad);
            if (position != null)
            {
                x = (int)position.X;
                y = (int)position.Y;
                z = (int)position.Z;
            }

            if (pathfindingNodeClasses.Contains(exportToLoad.ClassName))
            {
                PathfindingNode pathNode;
                switch (exportToLoad.ClassName)
                {
                    case "PathNode":
                        pathNode = new PathNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXEnemySpawnPoint":
                        pathNode = new SFXEnemySpawnPoint(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_JumpNode":
                        pathNode = new SFXNav_JumpNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LeapNodeHumanoid":
                        pathNode = new SFXNav_LeapNodeHumanoid(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXDoorMarker":
                        pathNode = new SFXDoorMarker(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LargeMantleNode":
                        pathNode = new SFXNav_LargeMantleNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXDynamicPathNode":
                    case "BioPathPoint":
                        pathNode = new BioPathPoint(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_WayPoint":
                        pathNode = new SFXNav_WayPoint(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "PathNode_Dynamic":
                        pathNode = new PathNode_Dynamic(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LargeBoostNode":
                        pathNode = new SFXNav_LargeBoostNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_TurretPoint":
                        pathNode = new SFXNav_TurretPoint(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "CoverLink":
                        pathNode = new CoverLink(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_JumpDownNode":
                        pathNode = new SFXNav_JumpDownNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LadderNode":
                        pathNode = new SFXNav_LadderNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXDynamicCoverLink":
                        pathNode = new SFXDynamicCoverLink(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "CoverSlotMarker":
                        pathNode = new CoverSlotMarker(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXDynamicCoverSlotMarker":
                        pathNode = new SFXDynamicCoverSlotMarker(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "MantleMarker":
                        pathNode = new MantleMarker(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_HarvesterMoveNode":
                        pathNode = new SFXNav_HarvesterMoveNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_BoostNode":
                        pathNode = new SFXNav_BoostNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    default:
                        pathNode = new PendingNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                }
                pathNode.IsOverlay = isFromOverlay;
                GraphNodes.Add(pathNode);
                return new PointF(x, y);
            } //End if Pathnode Class 

            if (actorNodeClasses.Contains(exportToLoad.ClassName))
            {
                ActorNode actorNode;
                switch (exportToLoad.ClassName)
                {
                    case "BioPlaypenVolumeAdditive":
                        actorNode = new BioPlaypenVolumeAdditive(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "DynamicBlockingVolume":
                        actorNode = new DynamicBlockingVolume(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_DynamicVolumes);
                        break;
                    case "DynamicTriggerVolume":
                        actorNode = new DynamicTriggerVolume(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_DynamicVolumes);
                        break;
                    case "InterpActor":
                    case "KActor":
                    case "SFXKActor":
                    case "BioUseable":
                        actorNode = new InterpActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "BioTriggerVolume":
                    case "TriggerVolume":
                        actorNode = new BioTriggerVolume(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_BioTriggerVolumes);
                        break;
                    case "BioTriggerStream":
                        actorNode = new BioTriggerStream(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_BioTriggerStreams);
                        break;
                    case "SFXGrenadeContainer":
                        actorNode = new SFXGrenadeContainer(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXAmmoContainer":
                        actorNode = new SFXAmmoContainer(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXAmmoContainer_Simulator":
                        actorNode = new SFXAmmoContainer_Simulator(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXCombatZone":
                        actorNode = new SFXCombatZone(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_SFXCombatZones);
                        break;
                    case "BioStartLocation":
                    case "BioStartLocationMP":
                        actorNode = new BioStartLocation(uindex, x, y, exportToLoad.FileRef, graphEditor, showRotation: true);
                        break;
                    case "SFXStuntActor":
                        actorNode = new SFXStuntActor(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "BioPawn":
                        actorNode = new BioPawn(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SkeletalMeshActor":
                    case "SkeletalMeshCinematicActor":
                    case "SFXSkeletalMeshActor":
                    case "SFXSkeletalMeshCinematicActor":
                        actorNode = new SkeletalMeshActor(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXSkeletalMeshActorMAT":
                    case "SkeletalMeshActorMAT":
                        actorNode = new SkeletalMeshActorArchetyped(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXPlaceable_Generator":
                    case "SFXPlaceable_ShieldGenerator":
                    case "SFXPlaceable_RottenRachniEgg":
                    case "SFXPlaceable_RachniEgg":
                    case "SFXPlaceable_GethTripMine":
                    case "SFXPlaceable_Generic":
                    case "SFXPlaceable_CerberusShield":
                    case "SFXPlaceable_IndoctrinationDevice":
                    case "BioInert":
                        actorNode = new SFXPlaceable(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXArmorNode":
                    case "SFXTreasureNode":
                    case "SFXWeaponFactory":
                        actorNode = new SFXTreasureNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXMedStation":
                        actorNode = new SFXMedStation(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "TargetPoint":
                    case "Note":
                    case "BioMapNote":
                        actorNode = new TargetPoint(uindex, x, y, exportToLoad.FileRef, graphEditor, true);
                        break;
                    case "SpotLightToggleable":
                    case "PointLightToggleable":
                    case "PointLightMovable":
                    case "DirectionalLightToggleable":
                    case "SkyLightToggleable":
                        actorNode = new LightActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXKillRagdollVolume":
                    case "PhysicsVolume":
                    case "DynamicPhysicsVolume":
                        actorNode = new GenericVolumeNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXOperation_ObjectiveSpawnPoint":
                        actorNode = new SFXObjectiveSpawnPoint(uindex, x, y, exportToLoad.FileRef, graphEditor);

                        //Create annex node if required
                        if (props.GetProp<ObjectProperty>("AnnexZoneLocation") is ObjectProperty annexZoneLocProp)
                        {
                            if (exportToLoad.FileRef.IsUExport(annexZoneLocProp.Value))
                            {
                                ExportEntry targetPoint = exportToLoad.FileRef.GetUExport(annexZoneLocProp.Value);
                                if (targetPoint.ClassName != "TargetPoint")
                                {
                                    actorNode.comment.Text += "\nAnnex Zone Location not a target point!";
                                    actorNode.comment.TextBrush = new SolidBrush(System.Drawing.Color.Red);
                                }
                            }
                            else
                            {
                                actorNode.comment.Text += "\nAnnex Zone Location export out of bounds!";
                                actorNode.comment.TextBrush = new SolidBrush(System.Drawing.Color.Red);
                            }
                        }
                        if (props.GetProp<ObjectProperty>("CombatZone") is ObjectProperty combatZoneProp)
                        {
                            if (exportToLoad.FileRef.IsUExport(combatZoneProp.Value))
                            {
                                ExportEntry combatZoneExp = exportToLoad.FileRef.GetUExport(combatZoneProp.Value);
                                if (combatZoneExp.ClassName != "SFXCombatZone")
                                {
                                    actorNode.comment.Text += "\nAnnex Zone combat zone not a combat zone!";
                                    actorNode.comment.TextBrush = new SolidBrush(System.Drawing.Color.Red);
                                }
                            }
                            else
                            {
                                actorNode.comment.Text += "\nCombat Zone export out of bounds!";
                                actorNode.comment.TextBrush = new SolidBrush(System.Drawing.Color.Red);
                            }
                        }
                        break;
                    case "SFXMedkit":
                        actorNode = new SFXMedKit(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "Trigger":
                    case "BioTrigger":
                    case "Trigger_Dynamic":
                    case "Trigger_LOS":
                        actorNode = new GenericTriggerNode(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowCylinders_Triggers);
                        break;
                    default:
                        actorNode = new PendingActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                }
                actorNode.IsOverlay = isFromOverlay;
                if (!isFromOverlay)
                {
                    actorNode.DoubleClick += actornode_DoubleClick;
                }

                GraphNodes.Add(actorNode);
                return new PointF(x, y);
            }

            if (artNodeClasses.Contains(exportToLoad.ClassName))
            {
                ActorNode artNode;
                switch (exportToLoad.ClassName)
                {
                    case "BlockingVolume":
                    case "BioBlockingVolume":
                        artNode = new BlockingVolume(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_BlockingVolumes);
                        break;
                    case "SFXBlockingVolume_Ledge":
                        artNode = new SFXBlockingVolume_Ledge(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "StaticMeshActor":
                        artNode = new StaticMeshActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "LensFlareSource":
                    case "BioSunActor":
                        artNode = new LensFlareNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "DirectionalLight":
                    case "SkyLight":
                    case "PointLight":
                    case "SpotLight":
                        artNode = new LightActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "Emitter":
                        artNode = new EmitterNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "DecalActor":
                        artNode = new DecalActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "PostProcessVolume":
                    case "LightVolume":
                    case "LightmassImportanceVolume":
                    case "FogVolumeHalfspaceDensityInfo":
                    case "FogVolumeSphericalDensityInfo":
                        artNode = new GenericVolumeNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "WwiseAmbientSound":
                        artNode = new WwiseAmbientSound(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "WwiseAudioVolume":
                    case "WwiseEnvironmentVolume":
                    case "WwiseMusicVolume":
                        artNode = new WwiseAudioVolume(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_WwiseAudioVolumes);
                        break;
                    default:
                        artNode = new PendingActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                }
                artNode.IsOverlay = isFromOverlay;
                if (!isFromOverlay)
                {
                    artNode.DoubleClick += actornode_DoubleClick;
                }

                GraphNodes.Add(artNode);
                return new PointF(x, y);
            }

            if (splineNodeClasses.Contains(exportToLoad.ClassName))
            {
                SplineNode splineNode = new SplineActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);

                splineNode.IsOverlay = isFromOverlay;
                GraphNodes.Add(splineNode);
                return new PointF(x, y);
            }

            //everything else
            GraphNodes.Add(new EverythingElseNode(uindex, x, y, exportToLoad.FileRef, graphEditor));
            return new PointF(x, y);
        }
        public void CreateConnections()
        {
            if (GraphNodes != null && GraphNodes.Count != 0)
            {
                foreach (PathfindingNodeMaster node in GraphNodes)
                {
                    graphEditor.addNode(node);
                }
                foreach (var node in graphEditor.nodeLayer)
                {
                    (node as PathfindingNodeMaster)?.CreateConnections(GraphNodes);
                }

                foreach (var edge in graphEditor.edgeLayer)
                {
                    PathingGraphEditor.UpdateEdgeStraight(edge as PathfindingEditorEdge);
                }
            }

            if (PlayerGPSObject != null)
            {
                graphEditor.addNode(PlayerGPSObject);
            }
        }

        #endregion

        #region Graph+UI
        private void RefreshGraph()
        {
            if (AllowRefresh)
            {
                var oldselections = new List<int>();
                foreach (CollectionActor c in Collections_chkbx.SelectedItems)
                {
                    oldselections.Add(c.export.UIndex);
                }
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                ActiveNodes.ClearEx();
                ActiveOverlayNodes.ClearEx();
                GraphNodes.Clear();
                CollectionActors.ClearEx();
                CombatZones.ClearEx();
                LoadPathingNodesFromLevel();
                GenerateGraph();
                graphEditor.Refresh();
                foreach (var ac in oldselections)
                {
                    var smac = CollectionActors.FirstOrDefault(ca => ca.export.UIndex == ac);
                    if (smac != null)
                        Collections_chkbx.SelectedItems.Add(smac);
                }
            }
        }
        public void FocusNode(ExportEntry node, bool select, long duration = 1000)
        {
            PathfindingNodeMaster s = GraphNodes.FirstOrDefault(o => o.UIndex == node.UIndex);
            if (s != null)
            {
                if (select)
                {
                    var selectedNodeCurrently = ActiveNodes_ListBox.SelectedItem;
                    ActiveNodes_ListBox.SelectedItem = node;
                    if (selectedNodeCurrently == node)
                    {
                        ActiveNodesList_SelectedItemChanged(null, null); //Animate
                    }
                }
                else
                {
                    graphEditor.Camera.AnimateViewToCenterBounds(s.GlobalFullBounds, false, duration);
                }
            }
        }

        /// <summary>
        /// Called from winforms graph
        /// </summary>
        public void OpenContextMenu()
        {
            ContextMenu contextMenu = (ContextMenu)FindResource("nodeContextMenu");
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry export)
            {
                PathfindingNodeMaster s = GraphNodes.First(o => o.UIndex == export.UIndex);
                var debug = s.Tag;
                var currentlocation = PathEdUtils.GetLocation(export);
                CurrentNodeXY = $"{s.GlobalBounds.X},{s.GlobalBounds.Y}";

            }
            contextMenu.IsOpen = true;
            graphEditor.DisableDragging();
        }
        private void node_MouseDown(object sender, PInputEventArgs e)
        {
            var node = (PathfindingNodeMaster)sender;
            //int n = node.Index;

            if (e.Shift)
            {
                PathfindingEditorWPF_ReachSpecsPanel.SetDestinationNode(node.UIndex);
                return;
            }

            ChangingSelectionByGraphClick = true;

            ActiveNodes_ListBox.SelectedItem = node.export;
            //if (node is SplineActorNode)
            //{
            //    node.Select();
            //}

            //CurrentlySelectedSplinePoint = null;
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                Debug.WriteLine("Opening right mouse menu");
                OpenContextMenu();
            }
            ChangingSelectionByGraphClick = false;

        }
        private void actornode_DoubleClick(object sender, PInputEventArgs e)
        {
            if (sender is ActorNode an)
            {
                an.SetShape(!an.ShowAsPolygon);
                an.InvalidateFullBounds();
                graphEditor.Refresh();
                graphEditor.Camera.AnimateViewToCenterBounds(an.GlobalFullBounds, false, 500);
            }
        }
        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            graphEditor.AllowDragging();
            if (AllowWindowRefocus)
            {
                Focus(); //this will make window bindings work, as context menu is not part of the visual tree, and focus will be on there if the user clicked it.
            }

            AllowWindowRefocus = true;
        }

        public bool AllowWindowRefocus = true;

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".upk" && ext != ".pcc" && ext != ".sfm")
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext is ".upk" or ".pcc" or ".sfm")
                {
                    LoadFile(files[0]);
                }
            }
        }

        private void GraphEditor_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext is ".upk" or ".pcc" or ".sfm")
                {
                    LoadFile(files[0]);
                }
            }
        }

        private void GraphEditor_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".upk" && ext != ".pcc" && ext != ".sfm")
                {
                    e.Effect = System.Windows.Forms.DragDropEffects.None;
                }
                else
                {
                    e.Effect = System.Windows.Forms.DragDropEffects.All;
                }
            }
            else
            {
                e.Effect = System.Windows.Forms.DragDropEffects.None;
            }
        }
        public void UpdateEdgesForCurrentNode(PathfindingNodeMaster node = null)
        {
            if (node is SplineActorNode splineActorNode)
            {
                PathingGraphEditor.UpdateEdgeStraight(splineActorNode.ArriveTangentControlNode.Edge);
                PathingGraphEditor.UpdateEdgeStraight(splineActorNode.LeaveTangentControlNode.Edge);
                return;
            }
            PathfindingNodeMaster nodeToUpdate = node;
            if (nodeToUpdate == null && ActiveNodes_ListBox.SelectedItem is ExportEntry export)
            {
                nodeToUpdate = GraphNodes.FirstOrDefault(o => o.UIndex == export.UIndex);
            }

            if (nodeToUpdate == null)
            {
                throw new ArgumentNullException(nameof(node), "No Selected Node!");
            }

            var edgesToRemove = new List<PathfindingEditorEdge>();
            var newOneWayEdges = new List<PathfindingEditorEdge>();

            if (nodeToUpdate is PathfindingNode pn)
            {
                var existingSpecs = pn.ReachSpecs;
                var newReachSpecs = PathEdUtils.GetReachspecExports(pn.export);
                if (existingSpecs.Count > newReachSpecs.Count)
                {
                    //We have deleted at least one outbound spec.
                    //we need to either turn the link dashed or remove it (don't save in newOneWayEdge list)
                    var removedSpecs = existingSpecs.Except(newReachSpecs);
                    var endpointsToCheck = removedSpecs.Select(x => PathEdUtils.GetReachSpecEndExport(x)).ToList();
                    foreach (var edge in nodeToUpdate.Edges)
                    {
                        if (edge.GetOtherEnd(nodeToUpdate) is PathfindingNode othernode && endpointsToCheck.Contains(othernode.export))
                        {
                            //the link to this node has been removed
                            edge.RemoveOutboundFrom(pn);

                            if (edge.HasAnyOutboundConnections())
                            {
                                edge.Pen.DashStyle = DashStyle.Dash;
                                newOneWayEdges.Add(edge);
                            }
                        }
                    }
                }
            }

            //Remove reference to our edges from connected nodes via our edges.
            foreach (var edge in nodeToUpdate.Edges)
            {
                if (edge.GetOtherEnd(nodeToUpdate) is PathfindingNode othernode)
                {
                    othernode.Edges.Remove(edge); //we will regenerate this link from our current one
                }
            }

            //remove all edges except new one ways (so they don't have to be re-added to the graph)
            graphEditor.edgeLayer.RemoveChildren(nodeToUpdate.Edges.Where(x => !newOneWayEdges.Contains(x)));
            nodeToUpdate.Edges.Clear();
            nodeToUpdate.CreateConnections(GraphNodes);
            foreach (var onewayedge in newOneWayEdges)
            {
                //reattach edge back to endpoints
                onewayedge.ReAttachEdgesToEndpoints();
            }
            foreach (PathfindingEditorEdge edge in nodeToUpdate.Edges)
            {
                PathingGraphEditor.UpdateEdgeStraight(edge);
            }
        }

        private void PopoutInterpreterWPF(object obj)
        {
            var export = (ExportEntry)ActiveNodes_ListBox.SelectedItem;
            var elhw = new ExportLoaderHostedWindow(new InterpreterExportLoader(), export)
            {
                Title = $"Properties - {export.UIndex} {export.InstancedFullPath} - {Pcc.FilePath}"
            };
            elhw.Show();
        }
        #endregion

        #region EditNodes
        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            List<PackageChange> changes = updates.Select(x => x.Change).ToList();
            bool hasExportNonDataChanges = changes.Any(x => x != PackageChange.ExportData && x.HasFlag(PackageChange.Export));

            var activeNode = ActiveNodes_ListBox.SelectedItem as ExportEntry;

            if (hasExportNonDataChanges) //may optimize by checking if chagnes include anything we care about
            {
                //Do a full refresh
                ExportEntry selectedExport = ActiveNodes_ListBox.SelectedItem as ExportEntry;
                RefreshGraph();
                ActiveNodes_ListBox.SelectedItem = selectedExport;
                return;
            }

            List<int> loadedincices = ActiveNodes.Select(exp => exp.UIndex).ToList();
            List<int> nodesToUpdate = updates.Where(x => x.Change == PackageChange.ExportData && loadedincices.Contains(x.Index)).Select(x => x.Index).ToList();

            if (nodesToUpdate.Count > 0)
            {
                foreach (var node in ActiveNodes)
                {
                    if (nodesToUpdate.Contains(node.UIndex))
                    {
                        PathfindingNodeMaster s = GraphNodes.First(o => o.UIndex == node.UIndex);
                        if (s is SplineActorNode splineActorNode)
                        {
                            ValidationPanel.RecalculateSplineComponents(Pcc);
                            //Do a full refresh
                            ExportEntry selectedExport = ActiveNodes_ListBox.SelectedItem as ExportEntry;
                            RefreshGraph();
                            ChangingSelectionByGraphClick = true;
                            ActiveNodes_ListBox.SelectedItem = selectedExport;
                            ChangingSelectionByGraphClick = false;
                            return;
                        }

                        if (node.ClassName.Contains("CollectionActor"))
                        {
                            ExportEntry selectedExport = ActiveNodes_ListBox.SelectedItem as ExportEntry;
                            //Do a full refresh
                            RefreshGraph();
                            ChangingSelectionByGraphClick = true;
                            ActiveNodes_ListBox.SelectedItem = selectedExport;
                            ChangingSelectionByGraphClick = false;
                            return;
                        }

                        //Reposition the node
                        var newlocation = PathEdUtils.GetLocation(node);
                        s.SetOffset((float)newlocation.X, (float)newlocation.Y);

                        UpdateEdgesForCurrentNode(s);
                        //foreach (PNode i in s.AllNodes)
                        //{
                        //    ArrayList edges = (ArrayList)i.Tag;
                        //    if (edges != null)
                        //    {
                        //        foreach (PPath edge in edges)
                        //        {
                        //            PathingGraphEditor.UpdateEdgeStraight(edge);
                        //        }
                        //    }
                        //}

                        if (node == activeNode)
                        {
                            ActiveNodesList_SelectedItemChanged(null, null); //Reselect object
                        }
                    }
                }
                graphEditor.Refresh(); //repaint invalidated areas
            }
        }
        private void PositionBoxes_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && ActiveNodes_ListBox.SelectedItem is ExportEntry export &&
                float.TryParse(NodePositionX_TextBox.Text, out float x) && float.TryParse(NodePositionY_TextBox.Text, out float y) && float.TryParse(NodePositionZ_TextBox.Text, out float z))
            {

                PathEdUtils.SetLocation(export, x, y, z);
                PathfindingNodeMaster s = GraphNodes.First(o => o.UIndex == export.UIndex);
                s.SetOffset(x, y);

                //TODO: Figure out what this does
                if (s is PathfindingNode pn)
                {
                    UpdateEdgesForCurrentNode(pn);
                }
                //foreach (PNode node in s.AllNodes)
                //{
                //    ArrayList edges = (ArrayList)node.Tag;
                //    if (edges != null)
                //        foreach (PPath edge in edges)
                //        {
                //            PathingGraphEditor.UpdateEdgeStraight(edge);
                //        }
                //}
                graphEditor.Refresh(); //repaint invalidated areas
            }
        }
        private void SetGraphXY_Clicked(object sender, RoutedEventArgs e)
        {
            //Find node
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry export && (export.IsA("Actor") || export.ClassName.Contains("Component")))
            {
                PathfindingNodeMaster s = GraphNodes.First(o => o.UIndex == export.UIndex);
                var currentlocation = PathEdUtils.GetLocation(export) ?? new Point3D(0, 0, 0);
                PathEdUtils.SetLocation(export, s.GlobalBounds.X, s.GlobalBounds.Y, (float)currentlocation.Z);
                MessageBox.Show($"Location set to {s.GlobalBounds.X}, {s.GlobalBounds.Y}");
            }
            else
            {
                MessageBox.Show("No location property on this export.");

            }
        }
        private void CoordinateEditor_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Keyboard.PrimaryDevice.IsKeyDown(Key.Tab))
            {
                if (sender is TextBox tb)
                {
                    tb.SelectAll();
                }
            }
        }
        #endregion

        #region PathingLayer
        private void HighlightBioWaypointSet(ExportEntry export)
        {
            var waypointReferences = export.GetProperty<ArrayProperty<StructProperty>>("WaypointReferences");
            var waypointUIndexes = new List<int>();
            if (waypointReferences != null)
            {
                foreach (var waypoint in waypointReferences)
                {
                    var nav = waypoint.GetProp<ObjectProperty>("Nav");
                    if (nav != null && nav.Value > 0)
                    {
                        waypointUIndexes.Add(nav.Value);
                    }
                }
            }
            if (waypointUIndexes.Count > 0)
            {
                foreach (PathfindingNodeMaster pnm in GraphNodes)
                {
                    if (waypointUIndexes.Contains(pnm.export.UIndex))
                    {
                        pnm.shape.Brush = PathfindingNodeMaster.highlightedCoverSlotBrush;
                    }
                }
            }
        }

        private void HighlightCoverlinkSlots(ExportEntry coverlink)
        {

            var props = coverlink.GetProperty<ArrayProperty<StructProperty>>("Slots");
            if (props != null)
            {
                CurrentlyHighlightedCoverlinkNodes = new List<int>
                {
                    coverlink.UIndex
                };

                foreach (StructProperty slot in props)
                {
                    ObjectProperty coverslot = slot.GetProp<ObjectProperty>("SlotMarker");
                    if (coverslot != null)
                    {
                        CurrentlyHighlightedCoverlinkNodes.Add(coverslot.Value);
                    }
                }
                foreach (PathfindingNodeMaster pnm in GraphNodes)
                {
                    if (pnm.export == coverlink)
                    {
                        pnm.shape.Brush = PathfindingNodeMaster.sfxCombatZoneBrush;
                        continue;
                    }
                    if (CurrentlyHighlightedCoverlinkNodes.Contains(pnm.export.UIndex))
                    {
                        pnm.shape.Brush = PathfindingNodeMaster.highlightedCoverSlotBrush;
                    }
                    else if (pnm.export.ClassName == "CoverLink" || pnm.export.ClassName == "CoverSlotMarker")
                    {
                        pnm.shape.Brush = PathfindingNodeMaster.pathfindingNodeBrush;
                    }
                }
            }
        }
        public List<int> CurrentlyHighlightedCoverlinkNodes { get; private set; }


        private void ChangeNodeType()
        {
            Debug.WriteLine("Changing node type");
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry exp &&
                AvailableNodeTypes_ListBox.SelectedItem is NodeType type)
            {
                changeNodeType(exp, type);
            }
        }
        private static bool CanChangeNodetype() => true;

        /// <summary>
        /// This method changes a node's type. It does many steps:
        /// It checks that the node type imports exists as well as it's collision cylinder and reach spec imports.
        /// It will scan all nodes for incoming reachspecs to this node and change them to the appropriate class. 
        /// It changes the collision cylinder archetype
        /// It changes the node class and object name
        /// </summary>
        /// <param name="nodeEntry"></param>
        /// <param name="newNodeType"></param>
        private void changeNodeType(ExportEntry nodeEntry, NodeType newNodeType)
        {
            var exportTypeInfo = newNodeType.TypeInfo;
            PropertyCollection nodeProperties = nodeEntry.GetProperties();

            if (nodeEntry.ClassName == exportTypeInfo.nodetypename)
            {
                if (!exportTypeInfo.usesbtop || (nodeProperties.FirstOrDefault(x => x.Name == "bTopNode") is BoolProperty bTop && bTop.Value == newNodeType.Top))
                {
                    return; //same, not changing.
                }
            }

            if (exportTypeInfo != null)
            {
                var ensuredProperties = new List<Property>();
                foreach (var ensuredProp in exportTypeInfo.ensuredproperties)
                {
                    switch (ensuredProp.type)
                    {
                        case "BoolProperty":
                            ensuredProperties.Add(new BoolProperty(bool.Parse(ensuredProp.defaultvalue),
                                ensuredProp.name));
                            break;
                        case "ObjectProperty":
                            ensuredProperties.Add(new ObjectProperty(int.Parse(ensuredProp.defaultvalue),
                                ensuredProp.name));
                            break;
                    }
                }

                if (newNodeType.TypeInfo.usesbtop)
                {
                    if (newNodeType.Top)
                    {
                        ensuredProperties.Add(new BoolProperty(true, "bTopNode"));
                    }
                    else
                    {
                        var bTop = nodeProperties.FirstOrDefault(x => x.Name == "bTopNode");
                        if (bTop != null)
                        {
                            nodeProperties.Remove(bTop);
                        }
                    }
                }

                //Add ensured properties
                foreach (Property prop in ensuredProperties)
                {
                    if (!nodeProperties.ContainsNamedProp(prop.Name))
                    {
                        nodeProperties.Add(prop);
                    }
                }

                //Change collision cylinder
                ObjectProperty cylindercomponent = nodeProperties.GetProp<ObjectProperty>("CollisionComponent");
                ExportEntry cylindercomponentexp = Pcc.GetUExport(cylindercomponent.Value);

                //Ensure all classes are imported.
                IEntry newnodeclassimp = PathEdUtils.GetEntryOrAddImport(Pcc, exportTypeInfo.fullclasspath);
                IEntry newcylindercomponentimp = PathEdUtils.GetEntryOrAddImport(Pcc, exportTypeInfo.cylindercomponentarchetype);

                if (newnodeclassimp != null)
                {
                    nodeEntry.Class = newnodeclassimp;
                    nodeEntry.ObjectName = exportTypeInfo.nodetypename;
                    PathEdUtils.ReindexMatchingObjects(nodeEntry);
                    cylindercomponentexp.Archetype = newcylindercomponentimp;
                }

                if (exportTypeInfo.upgradetomaxpathsize)
                {
                    StructProperty maxpathsize = nodeProperties.GetProp<StructProperty>("MaxPathSize");

                    //Upgrade node size
                    if (maxpathsize != null)
                    {
                        FloatProperty radius = maxpathsize.GetProp<FloatProperty>("Radius");
                        FloatProperty height = maxpathsize.GetProp<FloatProperty>("Height");

                        if (radius != null)
                        {
                            radius.Value = 140;
                        }

                        if (height != null)
                        {
                            height.Value = 195;
                        }
                    }

                    //If items on the other end of a reachspec are also the same type,
                    //Ensure outbound and returning reachspec type and sizes are correct.
                    var pathList = nodeProperties.GetProp<ArrayProperty<ObjectProperty>>("PathList");
                    if (pathList != null)
                    {
                        foreach (ObjectProperty pathObj in pathList)
                        {
                            ExportEntry spec = Pcc.GetUExport(pathObj.Value);
                            EnsureLargeAndReturning(spec, exportTypeInfo);
                        }
                    }
                }
                nodeEntry.WriteProperties(nodeProperties);
            }
        }
        /*

        //Change the reachspec incoming to this node...
                    ArrayProperty<ObjectProperty> otherNodePathlist =  specDest.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    if (otherNodePathlist != null)
                    {
                        for (int on = 0; on < otherNodePathlist.Count; on++)
                        {
                            ExportEntry inboundSpec = Pcc.getUExport(otherNodePathlist[on].Value);
                            //Get end
                            if (inboundSpec.GetProperty<StructProperty>("End") is StructProperty prop)
                            {
                                PropertyCollection inboundSpecProps = prop.Properties;
                                foreach (var rprop in inboundSpecProps)
                                {
                                    if (rprop.Name ==
                                    )
                                    {
                                        int inboundSpecDest = (rprop as ObjectProperty).Value;
                                        if (inboundSpecDest == nodeEntry.UIndex)
                                        {
                                            //The node is inbound to me.
                                            inboundSpec.idxClass = newReachSpecClass.UIndex;
                                            inboundSpec.idxObjectName =
                                                Pcc.FindNameOrAdd("SFXLargeBoostReachSpec");
                                            //widen spec
                                            PathEdUtils.SetReachSpecSize(inboundSpec,
                                                PathfindingNodeInfoPanel.BANSHEE_RADIUS,
                                                PathfindingNodeInfoPanel.BANSHEE_HEIGHT);
                                            keepParsing = false; //stop the outer loop
                                            break;
                                        }
                                    }
                                }
                            }

                            if (!keepParsing)
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }

        //Todo: Strip unused properties
        nodeEntry.WriteProperties(nodeProperties);
    }*/

        /*switch (newType)
        {
            case NODETYPE_SFXENEMYSPAWNPOINT:
                exportclassdbkey = "SFXEnemySpawnPoint";
                break;
            case NODETYPE_PATHNODE:
                exportclassdbkey = "PathNode";
                break;
            case NODETYPE_SFXNAV_LAREGEBOOSTNODE:
                exportclassdbkey = "SFXNav_LargeBoostNode";
                propertiesToRemoveIfPresent.Add("bTopNode"); //if coming from boost node
                break;
            case NODETYPE_SFXNAV_TURRETPOINT:
                exportclassdbkey = "SFXNav_TurretPoint";
                break;
            case NODETYPE_SFXNAV_BOOSTNODE_TOP:
                {
                    exportclassdbkey = "SFXNav_BoostNode";
                    BoolProperty bTopNode = new BoolProperty(true, "bTopNode");
                    propertiesToAdd.Add(bTopNode);
                    propertiesToRemoveIfPresent.Add("JumpDownDest");
                }
                break;
            case NODETYPE_SFXNAV_BOOSTNODE_BOTTOM:
                exportclassdbkey = "SFXNav_BoostNode";
                propertiesToRemoveIfPresent.Add("bTopNode");
                propertiesToRemoveIfPresent.Add("JumpDownDest");
                break;
            case NODETYPE_SFXNAV_JUMPDOWNNODE_TOP:
                {
                    exportclassdbkey = "SFXNav_JumpDownNode";
                    BoolProperty bTopNode = new BoolProperty(true, "bTopNode");
                    propertiesToAdd.Add(bTopNode);
                    propertiesToRemoveIfPresent.Add("BoostDest");
                }
                break;
            case NODETYPE_SFXNAV_JUMPDOWNNODE_BOTTOM:
                exportclassdbkey = "SFXNav_JumpDownNode";
                propertiesToRemoveIfPresent.Add("bTopNode");
                propertiesToRemoveIfPresent.Add("BoostDest");
                break;
            case NODETYPE_SFXNAV_LARGEMANTLENODE:
                exportclassdbkey = "SFXNav_LargeMantleNode";
                ObjectProperty mantleDest = new ObjectProperty(0, "MantleDest");
                propertiesToAdd.Add(mantleDest);
                break;
            case NODETYPE_SFXNAV_CLIMBWALLNODE:
                exportclassdbkey = "SFXNav_ClimbWallNode";
                //propertiesToRemoveIfPresent.Add("ClimbDest");
                break;
            case NODETYPE_BIOPATHPOINT:
                {
                    exportclassdbkey = "BioPathPoint";
                    BoolProperty bEnabled = new BoolProperty(true, "bEnabled");
                    propertiesToAdd.Add(bEnabled);
                    break;
                }
            case NODETYPE_SFXDYNAMICCOVERLINK:
                {
                    exportclassdbkey = "SFXDynamicCoverLink";
                    //BoolProperty bEnabled = new BoolProperty(true, "bEnabled");
                    //propertiesToAdd.Add(bEnabled);
                    break;
                }
            case NODETYPE_SFXDYNAMICCOVERSLOTMARKER:
                {
                    exportclassdbkey = "SFXDynamicCoverSlotMarker";
                    //coverslot property blows up adding properties
                    BoolProperty bEnabled = new BoolProperty(true, "bEnabled");
                    propertiesToAdd.Add(bEnabled);
                    break;
                }
            default:
                return;
        }
    }

    return;

    //lookup requirements in DB
    {
        Dictionary<string, string> exportclassinfo = null; // exportclassdb[exportclassdbkey];
        string newclass = exportclassinfo["class"];
        string newname = exportclassinfo["name"];
        string newcylindercomponentarchetype = exportclassinfo["cylindercomponentarchetype"];

        //Get current cylinder component export.
        PropertyCollection props = nodeEntry.GetProperties();

        ObjectProperty cylindercomponent = props.GetProp<ObjectProperty>("CollisionComponent");
        ExportEntry cylindercomponentexp = Pcc.getUExport(cylindercomponent.Value);

        //Ensure all classes are imported.
        ImportEntry newnodeclassimp = PathEdUtils.GetOrAddImport(Pcc, newclass);
        ImportEntry newcylindercomponentimp =
            PathEdUtils.GetOrAddImport(Pcc, newcylindercomponentarchetype);

        if (newnodeclassimp != null)
        {
            nodeEntry.idxClass = newnodeclassimp.UIndex;
            nodeEntry.idxObjectName = Pcc.FindNameOrAdd(newname);
            cylindercomponentexp.idxArchtype = newcylindercomponentimp.UIndex;
        }

        //Write new properties
        /*if (propertiesToAdd.Count() > 0 || propertiesToRemoveIfPresent.Count() > 0)
        {
            foreach (UProperty prop in propertiesToAdd)
            {
                nodeEntry.WriteProperty(prop);
            }

            //Remove specific properties
            if (propertiesToRemoveIfPresent.Count > 0)
            {
                PropertyCollection properties = nodeEntry.GetProperties();
                List<UProperty> propertiesToRemove = new List<UProperty>();
                foreach (UProperty prop in properties)
                {
                    if (propertiesToRemoveIfPresent.Contains(prop.Name))
                    {
                        propertiesToRemove.Add(prop);
                    }
                }

                foreach (UProperty prop in propertiesToRemove)
                {
                    properties.Remove(prop);
                }

                nodeEntry.WriteProperties(properties);
            }
        }

        //perform special tasks here.
        switch (newType)
        {
            case /*NODETYPE_SFXNAV_LAREGEBOOSTNODE "-1":
                {
                    //Maximize MaxPathSize
                    StructProperty maxpathsize = nodeEntry.GetProperty<StructProperty>("MaxPathSize");
                    if (maxpathsize != null)
                    {
                        FloatProperty radius = maxpathsize.GetProp<FloatProperty>("Radius");
                        FloatProperty height = maxpathsize.GetProp<FloatProperty>("Height");

                        if (radius != null)
                        {
                            radius.Value = 140;
                        }

                        if (height != null)
                        {
                            height.Value = 195;
                        }

                        nodeEntry.WriteProperty(maxpathsize);
                    }


                    //If items on the other end of a reachspec are also SFXNav_LargeBoostNode,
                    //Ensure the reachspec is SFXLargeBoostReachSpec.
                    //Ensure maxpath sizes are set to max size.
                    ArrayProperty<ObjectProperty> pathList =
                        nodeEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    if (pathList != null)
                    {
                        for (int i = 0; i < pathList.Count; i++)
                        {
                            ExportEntry spec = Pcc.getUExport(pathList[i].Value);
                            //Get ending
                            int othernodeidx = 0;
                            PropertyCollection specprops = spec.GetProperties();
                            foreach (var prop in specprops)
                            {
                                if (prop.Name == "End")
                                {
                                    PropertyCollection reachspecprops = (prop as StructProperty).Properties;
                                    foreach (var rprop in reachspecprops)
                                    {
                                        if (rprop.Name == PathEdUtils.GetReachSpecEndName(spec))
                                        {
                                            othernodeidx = (rprop as ObjectProperty).Value;
                                            break;
                                        }
                                    }
                                }

                                if (othernodeidx != 0)
                                {
                                    break;
                                }
                            }

                            if (othernodeidx != 0)
                            {
                                bool keepParsing = true;
                                ExportEntry specDest = Pcc.getUExport(othernodeidx);
                                if (specDest.ClassName == "SFXNav_LargeBoostNode" &&
                                    spec.ClassName != "SFXLargeBoostReachSpec")
                                {
                                    //Change the reachspec info outgoing to this node...
                                    ImportEntry newReachSpecClass =
                                        PathEdUtils.GetOrAddImport(Pcc,
                                            "SFXGame.SFXLargeBoostReachSpec");

                                    if (newReachSpecClass != null)
                                    {
                                        spec.idxClass = newReachSpecClass.UIndex;
                                        spec.idxObjectName = Pcc.FindNameOrAdd("SFXLargeBoostReachSpec");
                                        //set spec to banshee sized
                                        PathEdUtils.SetReachSpecSize(spec,
                                            PathfindingNodeInfoPanel.BANSHEE_RADIUS,
                                            PathfindingNodeInfoPanel.BANSHEE_HEIGHT);
                                    }

                                    //Change the reachspec incoming to this node...
                                    ArrayProperty<ObjectProperty> otherNodePathlist =
                                        specDest.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                                    if (otherNodePathlist != null)
                                    {
                                        for (int on = 0; on < otherNodePathlist.Count; on++)
                                        {
                                            ExportEntry inboundSpec =
                                                Pcc.getUExport(otherNodePathlist[on].Value);
                                            //Get ending
                                            //PropertyCollection inboundProps = inboundSpec.GetProperties();
                                            var prop = inboundSpec.GetProperty<StructProperty>("End");
                                            if (prop != null)
                                            {
                                                PropertyCollection reachspecprops =
                                                    (prop as StructProperty).Properties;
                                                foreach (var rprop in reachspecprops)
                                                {
                                                    if (rprop.Name ==
                                                        PathEdUtils.GetReachSpecEndName(inboundSpec))
                                                    {
                                                        int inboundSpecDest =
                                                            (rprop as ObjectProperty).Value;
                                                        if (inboundSpecDest == nodeEntry.UIndex)
                                                        {
                                                            //The node is inbound to me.
                                                            inboundSpec.idxClass = newReachSpecClass.UIndex;
                                                            inboundSpec.idxObjectName =
                                                                Pcc.FindNameOrAdd("SFXLargeBoostReachSpec");
                                                            //widen spec
                                                            PathEdUtils.SetReachSpecSize(inboundSpec,
                                                                PathfindingNodeInfoPanel.BANSHEE_RADIUS,
                                                                PathfindingNodeInfoPanel.BANSHEE_HEIGHT);
                                                            keepParsing = false; //stop the outer loop
                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            if (!keepParsing)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }


                    //var outLinksProp = nodeEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    //if (outLinksProp != null)
                    //{
                    //    foreach (var prop in outLinksProp)
                    //    {
                    //        int reachspecexport = prop.Value;
                    //        ReachSpecs.Add(pcc.Exports[reachspecexport - 1]);
                    //    }

                    //    foreach (ExportEntry spec in ReachSpecs)
                    //    {

                    //    }
                    //}
                }

                break;

        }

    }
}
}
}*/

        /// <summary>
        /// Ensures the specified spec and returning spec are of full size and of the specified type if the end node matches the information in the exportTypeInfo
        /// </summary>
        /// <param name="spec"></param>
        /// <param name="exportTypeInfo"></param>
        /// <param name="outbound">If this is the initial outbound connection. Prevents infinite loops.</param>
        private void EnsureLargeAndReturning(ExportEntry spec, PathfindingDB_ExportType exportTypeInfo, bool outbound = true)
        {
            var reachspecendname = PathEdUtils.GetReachSpecEndName(spec);

            //Get destination
            PropertyCollection specprops = spec.GetProperties();
            int start = specprops.GetProp<ObjectProperty>("Start").Value;
            if (specprops.FirstOrDefault(x => x.Name == "End") is StructProperty reachspecendprop)
            {
                PropertyCollection reachspecprops = reachspecendprop.Properties;
                if (reachspecprops.FirstOrDefault(x => x.Name == reachspecendname) is ObjectProperty otherNodeIdxProp)
                {
                    int othernodeidx = otherNodeIdxProp.Value;

                    if (!Pcc.IsUExport(othernodeidx)) return; //skip as this is not proper data
                    ExportEntry specDest = Pcc.GetUExport(othernodeidx);

                    //Check for same as changing to type, ensure spec type is correct
                    if (specDest.ClassName == exportTypeInfo.nodetypename && spec.ClassName != exportTypeInfo.inboundspectype)
                    {
                        //Change the reachspec info outgoing to this node...
                        IEntry newReachSpecClass = PathEdUtils.GetEntryOrAddImport(Pcc, exportTypeInfo.inboundspectype);

                        if (newReachSpecClass != null)
                        {
                            spec.Class = newReachSpecClass;
                            spec.ObjectName = exportTypeInfo.nodetypename;
                            //set spec to banshee sized
                            PathEdUtils.SetReachSpecSize(spec,
                                ReachSpecSize.BANSHEE_RADIUS,
                                ReachSpecSize.BANSHEE_HEIGHT);
                        }

                        if (outbound)
                        {
                            var pathList = specDest.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                            if (pathList != null)
                            {
                                foreach (ObjectProperty pathObj in pathList)
                                {
                                    if (pathObj.Value == start)
                                    {
                                        spec = Pcc.GetUExport(pathObj.Value);
                                        EnsureLargeAndReturning(spec, exportTypeInfo, false);
                                        break; //this will only need to run once since there is only 1:1 reach specs
                                    }
                                }
                            }
                        }

                        PathEdUtils.ReindexMatchingObjects(spec);
                    }
                }
            }
        }

        public ObservableCollectionExtended<NodeType> AvailableNodeChangeableTypes { get; } = new();

        public class NodeType : NotifyPropertyChangedBase
        {
            private bool _active;
            public bool Active
            {
                get => _active;
                set => SetProperty(ref _active, value);
            }

            private PathfindingDB_ExportType _typeInfo;
            public PathfindingDB_ExportType TypeInfo
            {
                get => _typeInfo;
                set
                {
                    SetProperty(ref _typeInfo, value);
                    OnPropertyChanged(nameof(DisplayString));
                }
            }

            public string DisplayString
            {
                get
                {
                    string retval = TypeInfo.nodetypename;
                    if (TypeInfo.usesbtop)
                    {
                        if (Top)
                            retval += " (Top)";
                        else
                            retval += " (Bottom)";
                    }
                    return retval;
                }
            }

            public bool Top;
            public NodeType(PathfindingDB_ExportType TypeInfo, bool Top)
            {
                this.TypeInfo = TypeInfo;
                this.Top = Top;
            }
        }
        private void RegenerateGUID_Clicked(object sender, RoutedEventArgs e)
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry nodeEntry)
            {
                PathEdUtils.GenerateNewNavGUID(nodeEntry);
            }
        }
        #endregion

        #region SplineLayer
        private void AddSplineActorToChain()
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry export)
            {
                ExportEntry newNode = cloneNode(export);
                CreateSplineConnection(export, newNode);
            }
        }
        private void CreateSplineConnection_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry { ClassName: "SplineActor" } sourceActor)
            {
                var sourceAndConnections = new List<int> { sourceActor.UIndex };
                var connections = sourceActor.GetProperty<ArrayProperty<StructProperty>>("Connections") ?? new ArrayProperty<StructProperty>("Connections");
                foreach (StructProperty connection in connections)
                {
                    if (connection.GetProp<ObjectProperty>("ConnectTo") is { } connectTo)
                    {
                        sourceAndConnections.Add(connectTo.Value);
                    }
                }
                ExportEntry destActor = EntrySelector.GetEntry<ExportEntry>(this, Pcc, "Select a SplineActor to connect to.", exp => exp.ClassName == "SplineActor" && !sourceAndConnections.Contains(exp.UIndex));
                if (destActor == null)
                {
                    return;
                }

                CreateSplineConnection(sourceActor, destActor);
            }
        }
        private void CreateSplineConnection(ExportEntry sourceActor, ExportEntry destActor)
        {
            ArrayProperty<StructProperty> connections = sourceActor.GetProperty<ArrayProperty<StructProperty>>("Connections") ?? new ArrayProperty<StructProperty>("Connections");
            var rop = new RelinkerOptionsPackage();
            var splineComponentClass = EntryImporter.EnsureClassIsInFile(Pcc, "SplineComponent", rop);
            if (rop.RelinkReport.Any()) EntryImporterExtended.ShowRelinkResults(rop.RelinkReport);

            var splineComponent = new ExportEntry(Pcc, sourceActor, Pcc.GetNextIndexedName("SplineComponent"), new byte[8])
            {
                Class = splineComponentClass,
            };
            Pcc.AddExport(splineComponent);
            connections.Add(new StructProperty("SplineConnection", new PropertyCollection
            {
                new ObjectProperty(splineComponent, "SplineComponent"),
                new ObjectProperty(destActor, "ConnectTo")
            }));
            sourceActor.WriteProperty(connections);
            var linksFrom = destActor.GetProperty<ArrayProperty<ObjectProperty>>("LinksFrom") ?? new ArrayProperty<ObjectProperty>("LinksFrom");
            linksFrom.Add(new ObjectProperty(sourceActor));
            destActor.WriteProperty(linksFrom);
            ValidationPanel.RecalculateSplineComponents(Pcc);
        }
        private void BreakSplineConnectionClick(object sender, RoutedEventArgs e)
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry { ClassName: "SplineActor" } sourceActor)
            {
                var connectionUIndexes = new List<int>();
                var connections = sourceActor.GetProperty<ArrayProperty<StructProperty>>("Connections") ?? new ArrayProperty<StructProperty>("Connections");
                foreach (StructProperty connection in connections)
                {
                    if (connection.GetProp<ObjectProperty>("ConnectTo") is { } connectTo)
                    {
                        connectionUIndexes.Add(connectTo.Value);
                    }
                }

                if (connectionUIndexes.IsEmpty())
                {
                    MessageBox.Show(this, "This SplineActor is not connected to any others!");
                    return;
                }
                ExportEntry destActor = EntrySelector.GetEntry<ExportEntry>(this, Pcc, "Select a SplineActor to break connection to.", exp => exp.ClassName == "SplineActor" && connectionUIndexes.Contains(exp.UIndex));
                if (destActor == null)
                {
                    return;
                }

                for (int i = 0; i < connections.Count; i++)
                {
                    StructProperty connection = connections[i];
                    if (connection.GetProp<ObjectProperty>("ConnectTo")?.Value == destActor.UIndex)
                    {
                        if (Pcc.TryGetUExport(connection.GetProp<ObjectProperty>("SplineComponent")?.Value ?? 0, out ExportEntry splineComponent))
                        {
                            EntryPruner.TrashEntryAndDescendants(splineComponent);
                        }
                        connections.RemoveAt(i);
                        break;
                    }
                }

                sourceActor.WriteProperty(connections);
                ArrayProperty<ObjectProperty> linksFrom = destActor.GetProperty<ArrayProperty<ObjectProperty>>("LinksFrom") ?? new ArrayProperty<ObjectProperty>("LinksFrom");
                linksFrom.RemoveAll(objProp => objProp.Value == sourceActor.UIndex);
                destActor.WriteProperty(linksFrom);
                //ValidationPanel.RecalculateSplineComponents(Pcc);
            }
        }

        #endregion

        #region Collections/CombatZones
        [DebuggerDisplay("{export.UIndex} Collection Actor")]
        public class CollectionActor : NotifyPropertyChangedBase
        {
            private bool _active;
            public bool Active { get => _active; set => SetProperty(ref _active, value); }

            public List<ExportEntry> CollectionItems = new();
            public ExportEntry export { get; }
            private readonly string _dtype = "meshes";
            public string DisplayString => $"{export.UIndex}\t{CollectionItems.Count} {_dtype}";

            public CollectionActorType CollectionType = CollectionActorType.Collection_StaticMeshes;

            public CollectionActor(ExportEntry ac)
            {
                export = ac;
                if (ac.ClassName == "StaticLightCollectionActor")
                {
                    CollectionType = CollectionActorType.Collection_Lights;
                    _dtype = "lights";
                }
                CollectionItems.AddRange(PathEdUtils.GetCollectionItems(ac));
            }

            /// <summary>
            /// Retreives a list of position data, in order, of all items. Null items return a point at double.min, double.min
            /// </summary>
            /// <returns></returns>
            public List<Point3D> GetLocationData()
            {
                return PathEdUtils.GetCollectionLocationData(export);
            }

            /// <summary>
            /// Sets a component location. Requires component of this collection and new location.
            /// </summary>
            /// <returns></returns>
            public void SetComponentLocation(ExportEntry component, float x, float y, float z)
            {
                PathEdUtils.SetCollectionActorLocation(component, x, y, z, CollectionItems, export);
            }

            public enum CollectionActorType
            {
                Collection_Unknown,
                Collection_StaticMeshes,
                Collection_Lights
            }
        }

        [DebuggerDisplay("{export.UIndex} Combat Zone (Active: {Active})")]
        public class Zone : NotifyPropertyChangedBase
        {
            public ExportEntry export { get; }

            private bool _active;
            public bool Active { get => _active; set => SetProperty(ref _active, value); }

            private bool _displayactive;
            public bool DisplayActive { get => _displayactive; set => SetProperty(ref _displayactive, value); }
            public string DisplayString => $"{export.UIndex} {export.ObjectName.Instanced}";

            public Zone(ExportEntry combatZone)
            {
                export = combatZone;
            }

            /// <summary>
            /// Copy constructor
            /// </summary>
            /// <param name="combatZone"></param>
            public Zone(Zone combatZone)
            {
                export = combatZone.export;
                Active = combatZone.Active;
                DisplayActive = combatZone.DisplayActive;
            }
        }

        private void CombatZones_ItemSelectionChanged(object sender, Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
        {
            //Lock to single instance
            if (!IsCombatZonesSingleSelecting)
            {
                IsCombatZonesSingleSelecting = true;
                foreach (var zone in CombatZones)
                {
                    if (zone != e.Item)
                    {
                        zone.DisplayActive = false;
                    }
                    else
                    {
                        zone.DisplayActive = e.IsSelected;
                    }
                }
                IsCombatZonesSingleSelecting = false;

                //Highlight active combat zone
                Zone activeZone = CombatZones.FirstOrDefault(x => x.DisplayActive);

                //These statements are split into two groups for optimization purposes
                if (activeZone != null)
                {
                    foreach (var item in GraphNodes)
                    {
                        if (item is PathfindingNode node && node.Volumes.Any())
                        {
                            if (node.Volumes.Any(x => x.ActorUIndex == activeZone.export.UIndex))
                            {
                                //Set to combat zone background.
                                node.shape.Brush = PathfindingNodeMaster.sfxCombatZoneBrush;
                            }
                            else
                            {
                                //Set to default background.
                                node.shape.Brush = PathfindingNodeMaster.pathfindingNodeBrush;
                            }
                        }
                    }
                }
                else
                {
                    //Deselect.
                    foreach (var item in GraphNodes)
                    {
                        if (item is PathfindingNode node)
                        {
                            node.shape.Brush = PathfindingNodeMaster.pathfindingNodeBrush;
                        }
                    }
                }
                graphEditor.Refresh();
            }
        }

        private void CollectionActors_ItemSelectionChanged(object sender, Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
        {
            if (IsReadingLevel) return;
            CollectionActor smc = (CollectionActor)e.Item;
            if (e.IsSelected && ShowArtLayer)
            {
                StaticCollectionActor sca = null;
                if (smc.export.ObjectName == "StaticLightCollectionActor")
                {
                    sca = smc.export.GetBinaryData<StaticLightCollectionActor>();
                }
                else if (smc.export.ObjectName == "StaticMeshCollectionActor")
                {
                    sca = smc.export.GetBinaryData<StaticMeshCollectionActor>();
                }
                List<Point3D> locations = smc.GetLocationData();
                if (sca.LocalToWorldTransforms != null)
                {
                    for (int i = 0; i < sca.LocalToWorldTransforms.Count; i++)
                    {
                        var item = smc.CollectionItems[i];
                        var location = locations[i];

                        if (item != null)
                        {
                            switch (ZFilteringMode)
                            {
                                case EZFilterIncludeDirection.Above:
                                    if (location.Z <= ZFilteringValue)
                                        continue;
                                    break;
                                case EZFilterIncludeDirection.AboveEquals:
                                    if (location.Z < ZFilteringValue)
                                        continue;
                                    break;
                                case EZFilterIncludeDirection.BelowEquals:
                                    if (location.Z > ZFilteringValue)
                                        continue;
                                    break;
                                case EZFilterIncludeDirection.Below:
                                    if (location.Z >= ZFilteringValue)
                                        continue;
                                    break;
                            }

                            ActorNode actorNode = null;
                            switch (smc.CollectionType)
                            {
                                case CollectionActor.CollectionActorType.Collection_Lights:
                                    actorNode = new LAC_ActorNode(item.UIndex, (int)location.X, (int)location.Y, Pcc, graphEditor, (int)location.Z);
                                    break;
                                default:
                                    actorNode = new SMAC_ActorNode(item.UIndex, (int)location.X, (int)location.Y, Pcc, graphEditor, (int)location.Z);
                                    break;
                            }

                            ActiveNodes.Add(item);
                            GraphNodes.Add(actorNode);
                            actorNode.MouseDown += node_MouseDown;
                            graphEditor.addNode(actorNode);
                        }
                    }
                }
            }
            else
            {
                var activeNodesToKeep = ActiveNodes.Where(x => !smc.CollectionItems.Contains(x)).ToList();
                ActiveNodes.ReplaceAll(activeNodesToKeep);

                var graphNodesToRemove = GraphNodes.Where(x => smc.CollectionItems.Contains(x.export)).ToList();
                GraphNodes = GraphNodes.Except(graphNodesToRemove).ToList();
                graphEditor.nodeLayer.RemoveChildren(graphNodesToRemove);
            }

            graphEditor.Refresh();
        }

        private void CurrentCombatZones_SelectionChanged(object sender, Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
        {
            if (!CombatZonesLoading && e.Item is Zone itemChanging &&
                ActiveNodes_ListBox.SelectedItem is ExportEntry nodeExport)
            {
                var volumesList = nodeExport.GetProperty<ArrayProperty<StructProperty>>("Volumes");
                if (e.IsSelected && volumesList == null)
                {
                    volumesList = new ArrayProperty<StructProperty>("Volumes");
                }

                if (e.IsSelected)
                {
                    var actorRefProps = new PropertyCollection();

                    //Get GUID
                    var guid = itemChanging.export.GetProperty<StructProperty>("CombatZoneGuid");
                    guid.Name = "Guid";
                    actorRefProps.AddOrReplaceProp(guid);
                    actorRefProps.AddOrReplaceProp(new ObjectProperty(itemChanging.export.UIndex, "Actor"));
                    var actorReference = new StructProperty("ActorReference", actorRefProps, isImmutable: true);
                    volumesList.Add(actorReference);

                    PathfindingNode s = GraphNodes.FirstOrDefault(o => o.UIndex == nodeExport.UIndex) as PathfindingNode;
                    s?.Volumes.Add(new PathfindingNode.Volume(actorReference));
                    //todo: update and active combat zone pen for this node
                }
                else
                {
                    //Removing
                    StructProperty itemToRemove = null;
                    foreach (StructProperty actorReference in volumesList)
                    {
                        var actor = actorReference.GetProp<ObjectProperty>("Actor");
                        if (actor.Value == itemChanging.export.UIndex)
                        {
                            itemToRemove = actorReference;
                            break;
                        }
                    }

                    if (itemToRemove != null)
                    {
                        volumesList.Remove(itemToRemove);
                        if (GraphNodes.FirstOrDefault(o => o.UIndex == nodeExport.UIndex) is PathfindingNode s)
                        {
                            foreach (PathfindingNode.Volume v in s.Volumes)
                            {
                                if (v.ActorUIndex == itemChanging.export.UIndex)
                                {
                                    s.Volumes.Remove(v);
                                    //todo: update and active combat zone pen for this node
                                    break;
                                }
                            }
                        }

                    }
                }

                if (volumesList.Count > 0)
                {
                    nodeExport.WriteProperty(volumesList);
                }
                else
                {
                    var removed = nodeExport.RemoveProperty("Volumes");
                    Debug.WriteLine($"prop removed: {removed}");
                }
            }
        }

        private void ToggleAllCollections()
        {
            if (Collections_chkbx.SelectedItems.Count < CollectionActors.Count)
            {
                Collections_chkbx.SelectAll();
            }
            else
            {
                Collections_chkbx.SelectedItems.Clear();
            }
        }
        private IEnumerable<Zone> CloneCombatZonesForSelections() => CombatZones.Select(z => new Zone(z));

        #endregion

        #region Selection
        private void ActiveNodesList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            SplineNodeSelected = false;
            foreach (PathfindingNodeMaster pfm in GraphNodes)
            {
                pfm.Deselect();
                if (pfm.export.ClassName == "CoverLink" || pfm.export.ClassName == "CoverSlotMarker")
                {
                    pfm.shape.Brush = PathfindingNodeMaster.pathfindingNodeBrush;
                }
            }

            if (ActiveNodes_ListBox.SelectedItem is ExportEntry export)
            {
                CombatZonesLoading = true;

                NodeName = $"{export.ObjectName.Instanced}";
                NodeNameSubText = $"Export {export.UIndex}";
                ActiveNodes_ListBox.ScrollIntoView(export);
                interpreterControl.LoadExport(export);
                CurrentNodeCombatZones.ClearEx();
                CollectionActorEditorTab_CollectionActorEditor.UnloadExport();
                CollectionActor_Tab.IsEnabled = false;

                PathfindingNodeMaster selectedNode = GraphNodes.First(o => o.UIndex == export.UIndex);
                CurrentNodeSequenceReferences.ReplaceAll(selectedNode.SequenceReferences);
                var currentTab = PathfindingNodeTabControl.SelectedIndex;
                if (selectedNode is PathfindingNode)
                {
                    ReachSpecs_TabItem.IsEnabled = true;
                    CombatZones_TabItem.IsEnabled = true;
                    NodeType_TabItem.IsEnabled = true;
                    foreach (var availableNodeChangeableType in AvailableNodeChangeableTypes)
                    {
                        bool sameClass = availableNodeChangeableType.TypeInfo.nodetypename == export.ClassName;
                        if (sameClass)
                        {
                            if (availableNodeChangeableType.TypeInfo.usesbtop)
                            {
                                var b = export.GetProperty<BoolProperty>("bTopNode");
                                availableNodeChangeableType.Active = (b == null && !availableNodeChangeableType.Top) || (b != null && b == availableNodeChangeableType.Top);

                            }
                            else
                            {
                                availableNodeChangeableType.Active = true;
                            }
                        }
                        else
                        {
                            availableNodeChangeableType.Active = false;
                        }
                    }

                    CurrentNodeCombatZones.AddRange(CloneCombatZonesForSelections());

                    var participatingCombatZones = export.GetProperty<ArrayProperty<StructProperty>>("Volumes");
                    if (participatingCombatZones != null)
                    {
                        foreach (StructProperty volume in participatingCombatZones)
                        {
                            ObjectProperty actorRef = volume.GetProp<ObjectProperty>("Actor");
                            if (actorRef != null && actorRef.Value > 0)
                            {
                                Zone clonedZone = CurrentNodeCombatZones.FirstOrDefault(x => x.export.UIndex == actorRef.Value);
                                if (clonedZone != null) clonedZone.Active = true;
                            }
                        }
                    }
                    else
                    {
                        //No participation in any combat zones
                    }
                }
                else
                {
                    ReachSpecs_TabItem.IsEnabled = false;
                    CombatZones_TabItem.IsEnabled = false;
                    NodeType_TabItem.IsEnabled = false;
                    PathfindingNodeTabControl.SelectedItem = Math.Max(3, currentTab);
                }

                CombatZonesLoading = false;

                PathfindingEditorWPF_ReachSpecsPanel.LoadExport(export);

                //Clear coverlinknode highlighting.
                /*foreach (PathfindingNodeMaster pnm in Objects)
                {
                    if (CurrentlyHighlightedCoverlinkNodes.Contains(pnm.export.Index))
                    {
                        pnm.shape.Brush = pathfindingNodeBrush;
                    }
                }*/
                if (selectedNode != null)
                {
                    //if (selectedIndex != -1)
                    //{
                    //    PathfindingNodeMaster d = Objects.FirstOrDefault(o => o.Index == CurrentObjects[selectedIndex]);
                    //    if (d != null)
                    //        d.Deselect();
                    //}
                    selectedNode.Select();
                    if (!ChangingSelectionByGraphClick)
                    {
                        graphEditor.Camera.AnimateViewToCenterBounds(selectedNode.GlobalFullBounds, false, 1000);
                    }

                    Point3D position = PathEdUtils.GetLocation(export);
                    if (position != null)
                    {
                        NodePositionX_TextBox.Text = position.X.ToString();
                        NodePositionY_TextBox.Text = position.Y.ToString();
                        NodePositionZ_TextBox.Text = position.Z.ToString();
                    }
                    else
                    {
                        NodePositionX_TextBox.Text = 0.ToString();
                        NodePositionY_TextBox.Text = 0.ToString();
                        NodePositionZ_TextBox.Text = 0.ToString();
                    }

                    switch (selectedNode.export.ClassName)
                    {
                        case "CoverLink":
                            HighlightCoverlinkSlots(selectedNode.export);
                            break;
                        case "CoverSlotMarker":
                            StructProperty sp = selectedNode.export.GetProperty<StructProperty>("OwningSlot");
                            ObjectProperty op = sp?.GetProp<ObjectProperty>("Link");
                            if (op != null && op.Value - 1 < Pcc.ExportCount)
                            {
                                HighlightCoverlinkSlots(Pcc.GetUExport(op.Value));
                            }
                            break;
                        case "BioWaypointSet":
                            HighlightBioWaypointSet(selectedNode.export);
                            break;
                        case "SplineActor":
                            SplineNodeSelected = true;
                            break;
                        default:
                            if (selectedNode.export.ClassName.EndsWith("Component"))
                            {
                                CollectionActor_Tab.IsEnabled = true;
                                CollectionActorEditorTab_CollectionActorEditor.LoadExport(selectedNode.export);
                            }
                            break;
                    }


                }

                //GetProperties(pcc.getExport(CurrentObjects[n]));
                //selectedIndex = n;
                //selectedByNode = false;

                //Refresh binding
                NodePosition_Panel.IsEnabled = true;
                graphEditor.Refresh();
            }
            else
            {
                interpreterControl.UnloadExport();
                PathfindingEditorWPF_ReachSpecsPanel.UnloadExport();
                NodeName = "No node selected";
                NodeNameSubText = "N/A";
                NodePosition_Panel.IsEnabled = false;
            }
            OnPropertyChanged(nameof(NodeTypeDescriptionText));
        }

        private void FindByTag_Click(object sender, RoutedEventArgs e) => FindByTag();

        private void FindByTag()
        {
            int currentIndex = ActiveNodes_ListBox.SelectedIndex;
            var currnentSelectedItem = ActiveNodes_ListBox.SelectedItem as ExportEntry;
            if (currentIndex < 0 || currentIndex >= ActiveNodes.Count - 1) currentIndex = -1; //nothing selected or the final item is selected
            currentIndex++; //search next item

            if (FindByTag_ComboBox.SelectedItem is string nodeTagToFind)
            {
                for (int i = 0; i < ActiveNodes.Count; i++) //activenodes size should match graphnodes size... in theory of course.
                {
                    PathfindingNodeMaster ci = GraphNodes[(i + currentIndex) % GraphNodes.Count];
                    if (ci.NodeTag == nodeTagToFind)
                    {
                        ActiveNodes_ListBox.SelectedItem = ci.export;
                        if (ci.export == currnentSelectedItem)
                        {
                            ActiveNodesList_SelectedItemChanged(null, null); //refocus node
                        }

                        break;
                    }
                }
            }
        }

        private void FindByNumber_Click(object sender, RoutedEventArgs e) => FindByNumber();

        private void FindByNumber()
        {
            if (int.TryParse(FindByNumber_TextBox.Text, out int result) && Pcc.IsUExport(result))
            {
                var export = Pcc.GetUExport(result);
                int index = ActiveNodes.IndexOf(export);
                if (index >= 0)
                {
                    ActiveNodes_ListBox.SelectedIndex = index;
                }
            }
        }

        private void FindByNumber_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                FindByNumber();
            }
        }

        private void FindByTag_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                FindByTag();
            }
        }
        #endregion

        #region ZFiltering
        /// <summary>
        /// The current Z filtering value. This only is used if ZFilteringMode is not equal to None.
        /// </summary>
        public double ZFilteringValue { get => _zfilteringvalue; set => SetProperty(ref _zfilteringvalue, value); }
        private double _zfilteringvalue;

        private EZFilterIncludeDirection _zfilteringmode = EZFilterIncludeDirection.None;
        private bool CombatZonesLoading;

        /// <summary>
        /// The current Z filtering mode
        /// </summary>
        public EZFilterIncludeDirection ZFilteringMode { get => _zfilteringmode; set => SetProperty(ref _zfilteringmode, value); }

        /// <summary>
        /// Enum containing different INCLUSION criteria for Z filtering. 
        /// </summary>
        public enum EZFilterIncludeDirection
        {
            None,
            Above,
            Below,
            AboveEquals,
            BelowEquals
        }

        private bool isAllowedVisibleByZFiltering(ExportEntry exportEntry)
        {
            if (ZFilteringMode == EZFilterIncludeDirection.None) { return true; }
            Point3D position = PathEdUtils.GetLocation(exportEntry);
            if (position != null)
            {
                switch (ZFilteringMode)
                {
                    case EZFilterIncludeDirection.Above:
                        return position.Z > ZFilteringValue;
                    case EZFilterIncludeDirection.AboveEquals:
                        return position.Z >= ZFilteringValue;
                    case EZFilterIncludeDirection.Below:
                        return position.Z < ZFilteringValue;
                    case EZFilterIncludeDirection.BelowEquals:
                        return position.Z <= ZFilteringValue;
                }
            }
            return false;
        }

        private void ShowNodes_Below_Click(object sender, RoutedEventArgs e) => SetFilteringMode(EZFilterIncludeDirection.Below);

        private void ShowNodes_Above_Click(object sender, RoutedEventArgs e) => SetFilteringMode(EZFilterIncludeDirection.Above);

        private void ShowNodes_BelowEqual_Click(object sender, RoutedEventArgs e) => SetFilteringMode(EZFilterIncludeDirection.BelowEquals);

        private void ShowNodes_AboveEqual_Click(object sender, RoutedEventArgs e) => SetFilteringMode(EZFilterIncludeDirection.AboveEquals);

        private void SetFilteringMode(EZFilterIncludeDirection newfilter)
        {
            bool shouldRefresh = newfilter != ZFilteringMode;
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry export && newfilter != EZFilterIncludeDirection.None)
            {
                PathfindingNodeMaster s = GraphNodes.FirstOrDefault(o => o.UIndex == export.UIndex);
                var currentlocation = new Point3D();
                if (s is SMAC_ActorNode smc)
                {
                    currentlocation = new Point3D(smc.X, smc.Y, smc.Z);
                }
                else
                {
                    currentlocation = PathEdUtils.GetLocation(export);
                }
                shouldRefresh |= currentlocation.Z == ZFilteringValue;
                ZFilteringValue = currentlocation.Z;
            }
            ZFilteringMode = newfilter;
            OnPropertyChanged(nameof(CurrentFilteringText));
            if (shouldRefresh)
            {
                RefreshGraph();
            }
        }

        private void ShowNodes_All_Click(object sender, RoutedEventArgs e)
        {
            SetFilteringMode(EZFilterIncludeDirection.None);
        }
        #endregion

        #region SequenceRefs
        public ObservableCollectionExtended<ExportEntry> CurrentNodeSequenceReferences { get; } = new ObservableCollectionExtended<ExportEntry>();
        private void OpenRefInSequenceEditor(object obj)
        {
            if (obj is ExportEntry exp)
            {
                AllowWindowRefocus = false;
                var seqed = new SequenceEditorWPF(exp);
                seqed.Show();
                seqed.Activate();
            }
        }

        private void ToggleSequenceReferences()
        {
            ShowSequenceReferences = !ShowSequenceReferences;
            RefreshGraph();
        }

        #endregion

        public class PathingZoomController : IDisposable
        {
            public const float MIN_SCALE = .005f;
            public const float MAX_SCALE = 15;
            PathingGraphEditor graphEditor;
            PCamera camera;

            public PathingZoomController(PathingGraphEditor graphEditor)
            {
                this.graphEditor = graphEditor;
                this.camera = graphEditor.Camera;
                camera.Canvas.ZoomEventHandler = null;
                camera.ViewScale = 0.5f;
                camera.MouseWheel += OnMouseWheel;
                graphEditor.KeyDown += OnKeyDown;
            }

            public void Dispose()
            {
                //Remove event handlers for memory cleanup
                camera.MouseWheel -= OnMouseWheel;
                graphEditor.KeyDown -= OnKeyDown;
                camera = null;
                graphEditor = null;
            }

            public void OnKeyDown(object o, System.Windows.Forms.KeyEventArgs e)
            {
                if (e.Control)
                {
                    if (e.KeyCode == System.Windows.Forms.Keys.OemMinus)
                    {
                        scaleView(0.8f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                    }
                    else if (e.KeyCode == System.Windows.Forms.Keys.Oemplus)
                    {
                        scaleView(1.2f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                    }
                }
            }

            public void OnMouseWheel(object o, PInputEventArgs ea)
            {
                scaleView(1.0f + (0.001f * ea.WheelDelta), ea.Position);
            }

            public void scaleView(float scaleDelta, PointF p)
            {
                float currentScale = camera.ViewScale;
                float newScale = currentScale * scaleDelta;
                if (newScale < MIN_SCALE)
                {
                    camera.ViewScale = MIN_SCALE;
                    return;
                }
                if ((MAX_SCALE > 0) && (newScale > MAX_SCALE))
                {
                    camera.ViewScale = MAX_SCALE;
                    return;
                }
                camera.ScaleViewBy(scaleDelta, p.X, p.Y);
            }
        }

        public class PathfindingMouseListener : PBasicInputEventHandler, IDisposable
        {
            private PathfindingEditorWindow _pathfinderWindow;

            public PathfindingMouseListener(PathfindingEditorWindow pathfinderWindow)
            {
                this._pathfinderWindow = pathfinderWindow;
            }

            public void Dispose()
            {
                _pathfinderWindow = null;
            }

            public override void OnMouseMove(object sender, PInputEventArgs e)
            {
                PointF pos = e.Position;
                int X = 0;
                int Y = 0;
                try
                {
                    X = Convert.ToInt32(pos.X);
                    Y = Convert.ToInt32(pos.Y);
                }
                catch { }
                _pathfinderWindow.StatusText = $"[{X},{Y}]";
            }
        }

        #region LevelOperations
        private void AddExportToLevel()
        {
            if (EntrySelector.GetEntry<ExportEntry>(this, Pcc) is ExportEntry selectedEntry)
            {

                if (Pcc.AddToLevelActorsIfNotThere(selectedEntry))
                {
                    RefreshGraph();
                }
                else
                {
                    MessageBox.Show($"{selectedEntry.UIndex} {selectedEntry.InstancedFullPath} is already in the level.");
                }
            }
        }

        private void CloneNode_Clicked(object sender, RoutedEventArgs e)
        {
            var newNode = cloneNode(ActiveNodes_ListBox.SelectedItem as ExportEntry);
            if (newNode != null)
            {
                ActiveNodes_ListBox.SelectedItem = newNode;
            }
        }
        private ExportEntry cloneNode(ExportEntry nodeEntry, int locationSetOffset = 0)
        {
            if (nodeEntry != null)
            {
                ExportEntry newNodeEntry;
                if (nodeEntry.IsA("SplineActor"))
                {
                    newNodeEntry = EntryCloner.CloneEntry(nodeEntry);
                    newNodeEntry.RemoveProperty("Connections");
                    newNodeEntry.RemoveProperty("LinksFrom");
                }
                else if (nodeEntry.ClassName.Contains("Component"))
                {

                    var parent = nodeEntry.Parent as ExportEntry;
                    StaticCollectionActor sca;
                    if (parent.IsA("StaticMeshCollectionActor"))
                    {
                        sca = parent.GetBinaryData<StaticMeshCollectionActor>();
                    }
                    else
                    {
                        sca = parent.GetBinaryData<StaticLightCollectionActor>();
                    }

                    var components = parent.GetProperty<ArrayProperty<ObjectProperty>>(sca.ComponentPropName);
                    int i = components.IndexOf(new ObjectProperty(nodeEntry));
                    var clonedloc = sca.LocalToWorldTransforms[i];

                    if (components.Count >= 100)
                    {
                        MessageBox.Show("Collection is full. Finding alternative.", "Clone Node");
                        var collectionactors = new List<ExportEntry>();
                        if (parent.IsA("StaticMeshCollectionActor"))
                        {
                            collectionactors.AddRange(Pcc.Exports.Where(x => x.ClassName == "StaticMeshCollectionActor").ToList());
                        }
                        else
                        {
                            collectionactors.AddRange(Pcc.Exports.Where(x => x.ClassName == "StaticLightCollectionActor").ToList());
                        }
                        bool foundnewca = false;
                        foreach (var ca in collectionactors)
                        {
                            components = ca.GetProperty<ArrayProperty<ObjectProperty>>(sca.ComponentPropName);
                            if (components.Count < 100)
                            {
                                parent = ca;
                                foundnewca = true;
                                break;
                            }
                        }
                        if (foundnewca)
                        {
                            MessageBox.Show($"Adding cloned component to {parent.UIndex} {parent.ObjectName.Instanced}.", "Clone Node");
                            if (parent.IsA("StaticMeshCollectionActor"))
                            {
                                sca = parent.GetBinaryData<StaticMeshCollectionActor>();
                            }
                            else
                            {
                                sca = parent.GetBinaryData<StaticLightCollectionActor>();
                            }
                        }
                        else
                        {
                            var npdlg = MessageBox.Show("No alternative collections found. Creating new one.", "Clone Node", MessageBoxButton.OKCancel);
                            if (npdlg == MessageBoxResult.Cancel)
                                return null;
                            parent = EntryCloner.CloneEntry(nodeEntry.Parent) as ExportEntry;
                            components = parent.GetProperty<ArrayProperty<ObjectProperty>>(sca.ComponentPropName);
                            components.Clear();
                            if (parent.IsA("StaticMeshCollectionActor"))
                            {
                                sca = parent.GetBinaryData<StaticMeshCollectionActor>();
                            }
                            else
                            {
                                sca = parent.GetBinaryData<StaticLightCollectionActor>();
                            }
                            sca.LocalToWorldTransforms.Clear();
                            Pcc.AddToLevelActorsIfNotThere(parent);
                        }
                    }
                    AllowRefresh = false;
                    newNodeEntry = EntryCloner.CloneTree(nodeEntry);
                    newNodeEntry.idxLink = parent.UIndex;
                    components.Add(new ObjectProperty(newNodeEntry));
                    sca.LocalToWorldTransforms.Add(clonedloc);
                    parent.WriteProperty(components);
                    parent.WriteBinary(sca);
                    AllowRefresh = true;
                }
                else
                {
                    newNodeEntry = EntryCloner.CloneTree(nodeEntry);
                }

                if (newNodeEntry.IsA("Actor"))
                {
                    //empty the pathlist
                    PropertyCollection newExportProps = newNodeEntry.GetProperties();
                    var pathList = newExportProps.GetProp<ArrayProperty<ObjectProperty>>("PathList");
                    if (pathList is { Count: > 0 })
                    {
                        pathList.Clear();
                        newNodeEntry.WriteProperties(newExportProps);
                    }

                    var oldloc = PathEdUtils.GetLocation(newNodeEntry);
                    PathEdUtils.SetLocation(newNodeEntry, (float)oldloc.X + 50 + locationSetOffset, (float)oldloc.Y + 50 + locationSetOffset, (float)oldloc.Z);

                    PathEdUtils.GenerateNewNavGUID(newNodeEntry);
                    //Add cloned node to persistentlevel
                    var level = ObjectBinary.From<Level>(PersistentLevelExport);
                    level.Actors.Add(newNodeEntry.UIndex);
                    PersistentLevelExport.WriteBinary(level);
                }

                return newNodeEntry;
            }
            return null;
        }

        private void RemoveFromLevel()
        {
            var nodeEntry = (ExportEntry)ActiveNodes_ListBox.SelectedItem;

            var levelBin = PersistentLevelExport.GetBinaryData<Level>();

            if (levelBin.Actors.Contains(nodeEntry.UIndex))
            {
                AllowRefresh = false;
                levelBin.Actors.Remove(nodeEntry.UIndex);
                PersistentLevelExport.WriteBinary(levelBin);
                AllowRefresh = true;
                RefreshGraph();
                MessageBox.Show("Removed item from level.");
            }
            else
            {
                var parent = (ExportEntry)nodeEntry.Parent;
                if (parent.IsA("StaticLightCollectionActor") || parent.IsA("StaticMeshCollectionActor"))
                {
                    StaticCollectionActor sca;
                    if (parent.IsA("StaticMeshCollectionActor"))
                    {
                        sca = parent.GetBinaryData<StaticMeshCollectionActor>();
                    }
                    else
                    {
                        sca = parent.GetBinaryData<StaticLightCollectionActor>();
                    }

                    AllowRefresh = false;
                    var components = parent.GetProperty<ArrayProperty<ObjectProperty>>(sca.ComponentPropName);
                    int i = components.IndexOf(new ObjectProperty(nodeEntry));
                    components.RemoveAt(i);
                    sca.LocalToWorldTransforms.RemoveAt(i);
                    parent.WriteProperty(components);
                    parent.WriteBinary(sca);
                    AllowRefresh = true;
                    RefreshGraph();
                    MessageBox.Show("Removed item from level.");
                }
            }
        }
        private void TrashAndRemoveFromLevel()
        {
            var nodeEntry = (ExportEntry)ActiveNodes_ListBox.SelectedItem;
            AllowRefresh = false;
            TrashActor(nodeEntry);
            AllowRefresh = true;
            ActiveNodes_ListBox.SelectedIndex = -1; //Reset selection and will force refresh
            MessageBox.Show("Removed item from level.");
        }

        private void TrashActor(ExportEntry nodeEntry)
        {
            if (nodeEntry is null)
            {
                return;
            }
            if (nodeEntry.Parent is ExportEntry parent && (parent.IsA("StaticMeshCollectionActor") || parent.IsA("StaticLightCollectionActor")))
            {

                StaticCollectionActor sca;
                if (parent.IsA("StaticMeshCollectionActor"))
                {
                    sca = parent.GetBinaryData<StaticMeshCollectionActor>();
                }
                else
                {
                    sca = parent.GetBinaryData<StaticLightCollectionActor>();
                }

                var components = parent.GetProperty<ArrayProperty<ObjectProperty>>(sca.ComponentPropName);
                int i = components.IndexOf(new ObjectProperty(nodeEntry));
                components.RemoveAt(i);
                sca.LocalToWorldTransforms.RemoveAt(i);
                parent.WriteProperty(components);
                parent.WriteBinary(sca);
                EntryPruner.TrashEntryAndDescendants(nodeEntry);
            }
            else
            {
                var levelBin = PersistentLevelExport.GetBinaryData<Level>();
                if (levelBin.Actors.Remove(nodeEntry.UIndex))
                {
                    PersistentLevelExport.WriteBinary(levelBin);
                    EntryPruner.TrashEntryAndDescendants(nodeEntry);
                }
            }
        }
        #endregion

        #region Experiments
        private void LoadOverlay()
        {
            var d = AppDirectories.GetOpenPackageDialog();
            if (d.ShowDialog() == true)
            {
#if !DEBUG
                try
                {
#endif
                LoadOverlayFile(d.FileName);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }

        private void LoadOverlayFile(string fileName)
        {
            ActiveOverlayNodes.ClearEx();

            using var overlayPackage = MEPackageHandler.OpenMEPackage(fileName);
            OverlayPersistentLevelExport = overlayPackage.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
            if (OverlayPersistentLevelExport == null)
            {
                MessageBox.Show("This file does not contain a Level export.");
                return;
            }
            RefreshGraph();
        }

        public ExportEntry OverlayPersistentLevelExport { get; set; }

        private void BuildPathfindingChainExperiment()
        {
            var d = new OpenFileDialog
            {
                Filter = "Point Logger ASI file output (txt)|*txt",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (d.ShowDialog() == true)
            {
                string pathfindingChainFile = d.FileName;


                var pointsStrs = File.ReadAllLines(pathfindingChainFile);
                var points = new List<Point3D>();
                int lineIndex = 0;
                foreach (var point in pointsStrs)
                {
                    lineIndex++;
                    if (lineIndex <= 4)
                    {
                        continue; //skip header of file
                    }
                    string[] coords = point.Split(',');
                    points.Add(new Point3D(float.Parse(coords[0]), float.Parse(coords[1]), float.Parse(coords[2])));
                }
                var basePathNode = Pcc.Exports.First(x => x.ObjectName == "PathNode" && x.ClassName == "PathNode");
                ExportEntry firstNode = null;
                ExportEntry previousNode = null;


                foreach (var point in points)
                {
                    ExportEntry newNode = cloneNode(basePathNode);
                    StructProperty prop = newNode.GetProperty<StructProperty>("location");
                    if (prop != null)
                    {
                        PropertyCollection nodelocprops = prop.Properties;
                        foreach (var locprop in nodelocprops)
                        {
                            switch (locprop)
                            {
                                case FloatProperty fltProp when fltProp.Name == "X":
                                    fltProp.Value = (float)point.X;
                                    break;
                                case FloatProperty fltProp when fltProp.Name == "Y":
                                    fltProp.Value = (float)point.Y;
                                    break;
                                case FloatProperty fltProp when fltProp.Name == "Z":
                                    fltProp.Value = (float)point.Z;
                                    break;
                            }
                        }
                        newNode.WriteProperty(prop);

                        if (previousNode != null)
                        {
                            PathEdUtils.CreateReachSpec(previousNode, true, newNode, "Engine.ReachSpec", new ReachSpecSize(null, ReachSpecSize.BOSS_HEIGHT, ReachSpecSize.BOSS_RADIUS));
                        }

                        firstNode ??= newNode;
                        previousNode = newNode;
                    }
                }
                //createReachSpec(previousNode, true, firstNode.Index, "Engine.ReachSpec", 1, 0);

                PathfindingEditorWPF_ValidationPanel.FixStackHeaders();
                PathfindingEditorWPF_ValidationPanel.RelinkPathfindingChain();
                PathfindingEditorWPF_ValidationPanel.RecalculateReachspecs();
                //ReachSpecRecalculator rsr = new ReachSpecRecalculator(this);
                //rsr.ShowDialog(this);
                Debug.WriteLine("Done");
            }
        }

        private void FlipLevel()
        {
            foreach (ExportEntry exp in Pcc.Exports)
            {
                switch (exp.ObjectName.Name)
                {
                    case "StaticMeshCollectionActor":
                    {
                        var smca = exp.GetBinaryData<StaticMeshCollectionActor>();
                        foreach (int uIndex in smca.Components)
                        {
                            if (exp.FileRef.TryGetUExport(uIndex, out ExportEntry smComponent))
                            {
                                InvertScalingOnExport(smComponent, "Scale3D");
                            }
                        }

                        for (int i = 0; i < smca.LocalToWorldTransforms.Count; i++)
                        {
                            Matrix4x4 m = smca.LocalToWorldTransforms[i];
                            m.Translation *= -1;
                            smca.LocalToWorldTransforms[i] = m;
                        }
                        exp.WriteBinary(smca);
                        break;
                    }
                    default:
                    {
                        var props = exp.GetProperties();
                        StructProperty locationProp = props.GetProp<StructProperty>("location");
                        if (locationProp != null)
                        {
                            FloatProperty xProp = locationProp.Properties.GetProp<FloatProperty>("X");
                            FloatProperty yProp = locationProp.Properties.GetProp<FloatProperty>("Y");
                            FloatProperty zProp = locationProp.Properties.GetProp<FloatProperty>("Z");
                            Debug.WriteLine($"{exp.UIndex} {exp.ObjectName.Instanced} Flipping {xProp.Value},{yProp.Value},{zProp.Value}");

                            xProp.Value *= -1;
                            yProp.Value *= -1;
                            zProp.Value *= -1;

                            exp.WriteProperty(locationProp);
                            InvertScalingOnExport(exp, "DrawScale3D");
                        }
                        break;
                    }
                }
            }
            MessageBox.Show("Items flipped.", "Flipping complete");
        }

        private static void InvertScalingOnExport(ExportEntry exp, string propname)
        {
            var drawScale3D = exp.GetProperty<StructProperty>(propname);
            if (drawScale3D == null)
            {
                drawScale3D = CommonStructs.Vector3Prop(-1, -1, -1, propname);
            }
            else
            {
                drawScale3D.GetProp<FloatProperty>("X").Value *= -1;
                drawScale3D.GetProp<FloatProperty>("Y").Value *= -1;
                drawScale3D.GetProp<FloatProperty>("Z").Value *= -1;
            }
            exp.WriteProperty(drawScale3D);
        }
        private void CheckNetIndexes()
        {
            var indexes = new List<int>();
            foreach (PathfindingNodeMaster m in GraphNodes)
            {
                int nindex = m.export.NetIndex;
                if (indexes.Contains(nindex))
                {
                    Debug.WriteLine("Duplicate netindex " + nindex + ": Found a duplicate on " + m.export.InstancedFullPath);
                }
                else
                {
                    indexes.Add(nindex);
                }
            }
        }
        private void CheckGameFileNavs_Clicked(object sender, RoutedEventArgs e)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                StatusText = "Getting file information from game directory";
                FileInfo[] files = new DirectoryInfo(ME3Directory.BioGamePath).EnumerateFiles("*.pcc", SearchOption.AllDirectories).ToArray();
                int numScanned = 1;
                var navsNotAccountedFor = new SortedSet<string>();
                foreach (var file in files)
                {
                    //GC.Collect();
                    using var package = MEPackageHandler.OpenMEPackage(file.FullName);
                    var persistenLevelExp = package.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
                    if (persistenLevelExp == null) continue;

                    Level level = persistenLevelExp.GetBinaryData<Level>();
                    foreach (int actorUIndex in level.Actors)
                    {
                        if (package.IsUExport(actorUIndex))
                        {
                            ExportEntry exportEntry = package.GetUExport(actorUIndex);
                            //AllLevelObjects.Add(exportEntry);

                            if (exportEntry.ClassName == "BioWorldInfo" || ignoredobjectnames.Contains(exportEntry.ObjectName.Name))
                            {
                                continue;
                            }

                            if (!pathfindingNodeClasses.Contains(exportEntry.ClassName))
                            {
                                if (exportEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList") is not null)
                                {
                                    if (!navsNotAccountedFor.TryGetValue(exportEntry.ClassName, out string _))
                                    {
                                        Debug.WriteLine("Found new nav type: " + exportEntry.ClassName + " in " + exportEntry.FileRef.FilePath);
                                        navsNotAccountedFor.Add(exportEntry.FullPath);
                                    }
                                }
                            }

                            //if (splineNodeClasses.Contains(exportEntry.ClassName))
                            //{
                            //    isParsedByExistingLayer = true;

                            //    if (ShowSplinesLayer && isAllowedVisibleByZFiltering(exportEntry))
                            //    {
                            //        bulkActiveNodes.Add(exportEntry);
                            //        var connectionsProp = exportEntry.GetProperty<ArrayProperty<StructProperty>>("Connections");
                            //        if (connectionsProp != null)
                            //        {
                            //            foreach (StructProperty connectionProp in connectionsProp)
                            //            {
                            //                ObjectProperty splinecomponentprop = connectionProp.GetProp<ObjectProperty>("SplineComponent");
                            //                bulkActiveNodes.Add(Pcc.getUExport(splinecomponentprop.Value));
                            //            }
                            //        }
                            //    }
                            //}

                            //if (exportEntry.ClassName == "StaticMeshCollectionActor")
                            //{
                            //    StaticMeshCollections.Add(new StaticMeshCollection(exportEntry));
                            //}
                            //else if (exportEntry.ClassName == "SFXCombatZone" || exportEntry.ClassName == "BioPlaypenVolumeAdditive")
                            //{
                            //    CombatZones.Add(new Zone(exportEntry));
                            //}

                            //if (ShowEverythingElseLayer && !isParsedByExistingLayer && isAllowedVisibleByZFiltering(exportEntry))
                            //{
                            //    bulkActiveNodes.Add(exportEntry);
                            //}
                        }
                    }

                    numScanned++;
                    StatusText = "Scanning files " + (int)(numScanned * 100.0 / files.Length) + "%";
                }
                Debug.WriteLine("Navs not accounted for:");
                foreach (var s in navsNotAccountedFor)
                {
                    Debug.WriteLine(s);
                }
                StatusText = "Scanning completed";
            };
            worker.RunWorkerAsync();
        }

        private void RemoveAllSpotLights()
        {
            //var files = Directory.GetFiles(@"D:\Origin Games\Mass Effect\BioGame\CookedPC\Maps\LOS", "*.SFM", SearchOption.AllDirectories);
            //foreach (var v in files)
            //{
            //    using var p = MEPackageHandler.OpenMEPackage(v);
            //    if (p.Exports.Any(x => x.ClassName == "DirectionalLightComponent"))
            //    {
            //        Debug.WriteLine($"Dlc in {p.FilePath}");
            //    }
            //}
            //return;
            AllowRefresh = false;

            bool removed = false;
            Level levelBin = PersistentLevelExport.GetBinaryData<Level>();
            for (int i = levelBin.Actors.Count - 1; i >= 0; i--)
            {
                var actor = levelBin.Actors[i];
                if (actor > 0)
                {
                    var export = Pcc.GetUExport(actor);
                    if (export.ObjectName == "SpotLight")
                    {
                        Debug.WriteLine("Remove " + export.UIndex + " " + export.InstancedFullPath + " from level");
                        levelBin.Actors.RemoveAt(i);
                        removed = true;
                    }
                    else
                    if (export.ObjectName == "StaticLightCollectionActor")
                    {
                        var lightCollection = export.GetBinaryData<StaticLightCollectionActor>();
                        if (lightCollection.Components != null)
                        {
                            for (int j = lightCollection.Components.Count - 1; j >= 0; j--)
                            {
                                var subSLCAexport = Pcc.GetUExport(lightCollection.Components[j]);
                                if (subSLCAexport.ObjectName.Name.Contains("SpotLight"))
                                // ME1 AMD Lighting Fix test code
                                //subSLCAexport.ObjectName.Name.Contains("DirectionalLight_2_LC") //Beginning area
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_0_LC") //beginning area
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_6_LC") //beginning area

                                ////|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_4_LC") //
                                ////|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_22_LC") //

                                //// removing these did nothing for midsection near vigil
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_28_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_27_LC") //
                                ////|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_8_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_20_LC") //
                                ////|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_24_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_25_LC") //
                                ////|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_43_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_21_LC") //

                                ////did nothign for middle
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_22_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_3_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_26_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_4_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_28_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_8_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_20_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_24_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_27_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_10_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_23_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_25_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_21_LC") //

                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_15_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_319_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_5_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_17_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_18_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_7_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_1_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_13_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_14_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_9_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_11_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_19_LC") //
                                //|| subSLCAexport.ObjectName.Name.Contains("DirectionalLight_12_LC") //

                                // ART 40
                                //subSLCAexport.ObjectName.Name.Contains("DirectionalLight_0_LC")
                                //subSLCAexport.ObjectName.Name.Contains("DirectionalLight") && lightCollection.Export.indexValue == 118

                                //|| true
                                // )
                                {
                                    Debug.WriteLine("Remove " + subSLCAexport.UIndex + " " + subSLCAexport.InstancedFullPath + " from " + export.InstancedFullPath);
                                    lightCollection.Components.RemoveAt(j);
                                    lightCollection.LocalToWorldTransforms.RemoveAt(j);
                                    removed = true;
                                }
                            }

                            lightCollection.Export.WriteProperty(new ArrayProperty<ObjectProperty>(lightCollection.Components.Select(x => new ObjectProperty(x)).ToList(), lightCollection.ComponentPropName));
                            lightCollection.Export.WriteBinary(lightCollection);
                        }
                    }
                }
            }

            if (!removed)
            {
                AllowRefresh = true;
            }
            else
            {
                PersistentLevelExport.WriteBinary(levelBin);
                RefreshGraph();
                MessageBox.Show("Removed item(s) from level. See debug log");
            }
        }

        private void CalculateInterpStartEndTargetpoint()
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry { ClassName: "TargetPoint" } targetpointAnchorEnd)
            {
                var movingObject = EntrySelector.GetEntry<ExportEntry>(this, Pcc, "Select a level object that will be moved along the curve. This will be the starting point.");
                if (movingObject == null) return;

                ActiveNodes_ListBox.SelectedItem = movingObject;

                var interpTrack = EntrySelector.GetEntry<ExportEntry>(this, Pcc, "Select the interptrackmove data that we will modify for these points.");
                if (interpTrack == null) return;

                var locationTarget = PathEdUtils.GetLocation(targetpointAnchorEnd);
                var locationStart = PathEdUtils.GetLocation(movingObject);

                if (locationStart == null)
                {
                    MessageBox.Show("Start point doesn't have a location property. Ensure you picked the correct export.");
                    return;
                }

                double deltaX = locationTarget.X - locationStart.X;
                double deltaY = locationTarget.Y - locationStart.Y;
                double deltaZ = locationTarget.Z - locationStart.Z;

                var posTrack = interpTrack.GetProperty<StructProperty>("PosTrack");
                if (posTrack == null)
                {
                    MessageBox.Show("Selected interpdata doesn't have a postrack.");
                    return;
                }

                var posTrackPoints = posTrack.GetProp<ArrayProperty<StructProperty>>("Points");
                PathEdUtils.SetLocation(posTrackPoints[0].GetProp<StructProperty>("OutVal"), (float)deltaX, (float)deltaY, (float)deltaZ);
                PathEdUtils.SetLocation(posTrackPoints[^1].GetProp<StructProperty>("OutVal"), 0, 0, 0);

                interpTrack.WriteProperty(posTrack);
            }
        }
        private bool TargetPointIsSelected()
        {
            return ActiveNodes_ListBox.SelectedItem is ExportEntry { ClassName: "TargetPoint" };
        }

        #endregion

        #region ArtLevelTools
        private bool _showArtTools;
        public bool ShowArtTools { get => _showArtTools; set => SetProperty(ref _showArtTools, value); }
        public ObservableCollectionExtended<LightChannel> rgbChannels { get; } = new() { LightChannel.Red, LightChannel.Green, LightChannel.Blue };
        public enum LightChannel
        {
            Red,
            Green,
            Blue
        }
        private float _adjustRed;
        public float AdjustRed { get => _adjustRed; set => SetProperty(ref _adjustRed, value); }
        private LightChannel _switchRedTo = LightChannel.Red;
        public LightChannel SwitchRedTo { get => _switchRedTo; set => SetProperty(ref _switchRedTo, value); }
        private bool _switchIgnoreRed;
        public bool SwitchIgnoreRed { get => _switchIgnoreRed; set => SetProperty(ref _switchIgnoreRed, value); }
        private float _adjustGreen;
        public float AdjustGreen { get => _adjustGreen; set => SetProperty(ref _adjustGreen, value); }
        private LightChannel _switchGreenTo = LightChannel.Green;
        public LightChannel SwitchGreenTo { get => _switchGreenTo; set => SetProperty(ref _switchGreenTo, value); }
        private bool _switchIgnoreGreen;
        public bool SwitchIgnoreGreen { get => _switchIgnoreGreen; set => SetProperty(ref _switchIgnoreGreen, value); }
        private float _adjustBlue;
        public float AdjustBlue { get => _adjustBlue; set => SetProperty(ref _adjustBlue, value); }
        private LightChannel _switchBlueTo = LightChannel.Blue;
        public LightChannel SwitchBlueTo { get => _switchBlueTo; set => SetProperty(ref _switchBlueTo, value); }
        private bool _switchIgnoreBlue;
        public bool SwitchIgnoreBlue { get => _switchIgnoreBlue; set => SetProperty(ref _switchIgnoreBlue, value); }
        private float _brightnessAdjustment;
        public float BrightnessAdjustment { get => _brightnessAdjustment; set => SetProperty(ref _brightnessAdjustment, value); }

        public ObservableCollectionExtended<ExportEntry> ActorGroup { get; } = new();
        private bool _showonlyGroup;
        public bool ShowOnlyGroup
        {
            get => _showonlyGroup;
            set
            {
                SetProperty(ref _showonlyGroup, value);
                if (value && HideGroup)
                {
                    HideGroup = false;
                }
                RefreshGraph();
            }
        }
        private bool _hideGroup;
        public bool HideGroup
        {
            get => _hideGroup;
            set
            {
                SetProperty(ref _hideGroup, value);
                if (value && ShowOnlyGroup)
                {
                    ShowOnlyGroup = false;
                }
                RefreshGraph();
            }
        }
        private string _groupTag = "Tag";
        public string GroupTag { get => _groupTag; set => SetProperty(ref _groupTag, value); }
        private void AddToGroup(object obj)
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry node)
            {
                ActorGroup.Add(node);
                PathfindingNodeTabControl.SelectedIndex = 5;
            }
        }
        private void RemoveFromGroup(object obj)
        {
            var param = (string)obj;
            PathfindingNodeTabControl.SelectedIndex = 5;
            switch (param)
            {
                case "grpbox":
                    ActorGroup.RemoveRange(Group_ListBox.SelectedItems.Cast<ExportEntry>().ToList());
                    break;
                case "graph":
                    if (ActiveNodes_ListBox.SelectedItem is ExportEntry node)
                    {
                        ActorGroup.Remove(node);
                    }
                    break;
            }
        }
        private bool SelectedNodeIsNotInGroup(object obj)
        {
            return ShowArtTools && ActiveNodes_ListBox.SelectedItem is ExportEntry node && !ActorGroup.Contains(node);
        }
        private bool SelectedNodeIsInGroup(object obj)
        {
            return ShowArtTools && ActiveNodes_ListBox.SelectedItem is ExportEntry node && ActorGroup.Contains(node);
        }
        private void SaveActorGroup()
        {
            string pccname = Path.GetFileNameWithoutExtension(Pcc.FilePath);
            string tag = "";
            if (GroupTag != "Tag")
                tag = $"_{GroupTag}";
            var d = new SaveFileDialog
            {
                Filter = "*.txt|*.txt",
                InitialDirectory = PathfindingEditorDataFolder,
                FileName = $"{Pcc.Game}_{pccname}{tag}_group",
                AddExtension = true
            };
            if (d.ShowDialog() == true)
            {
                if (GroupTag == "Tag")
                {
                    var tagdlg = new PromptDialog("Do you want to give this group a memorable tag?", "Saving Group", $"{GroupTag}", true);
                    tagdlg.ShowDialog();
                    if (tagdlg.ResponseText != null)
                        GroupTag = tagdlg.ResponseText;
                }
                TextWriter tw = new StreamWriter(d.FileName);

                tw.WriteLine(Pcc.Game.ToString());
                tw.WriteLine(pccname);
                tw.WriteLine(GroupTag);
                foreach (var actor in ActorGroup)
                {
                    tw.WriteLine(actor.UIndex);
                }
                tw.Close();
                MessageBox.Show("Done.");
            }
        }
        private void LoadActorGroup()
        {
            string pccname = Path.GetFileNameWithoutExtension(Pcc.FilePath);
            var d = new OpenFileDialog
            {
                Filter = $"*.txt|*.txt",
                InitialDirectory = PathfindingEditorDataFolder,
                FileName = $"{Pcc.Game}_{pccname}_group",
                AddExtension = true,
                CustomPlaces = AppDirectories.GameCustomPlaces

            };
            if (d.ShowDialog() == true)
            {
                TextReader tr = new StreamReader(d.FileName);

                string game = tr.ReadLine();
                string file = tr.ReadLine();
                string grouptag = tr.ReadLine();
                var xplist = new List<string>();
                while (tr.ReadLine() is string uidx)
                {
                    xplist.Add(uidx);
                }

                var cdlg = MessageBox.Show($"Group file read:\n\nGame: {game}\nFile: {file}\nGroupTag: {grouptag}\n\nAdd this group?", "Pathfinding Editor", MessageBoxButton.YesNo);
                if (cdlg == MessageBoxResult.No)
                    return;
                ActorGroup.ClearEx();
                GroupTag = grouptag;
                var errorlist = new List<string>();
                foreach (string s in xplist)
                {
                    if (int.TryParse(s, out int uid) && uid < Pcc.ExportCount && uid > 0)
                    {
                        var actor = Pcc.GetUExport(uid);
                        if (actor != null && (actor.IsA("Actor") || actor.Parent.ClassName.Contains("CollectionActor")))
                        {
                            ActorGroup.Add(actor);
                            continue;
                        }
                    }
                    errorlist.Add(s);
                }

                if (!errorlist.IsEmpty())
                {
                    MessageBox.Show($"The following object indices were not found or are not actors: {string.Join(", ", errorlist)}");
                }
            }
        }
        private void AddAllActorsToGroup()
        {
            ActorGroup.ClearEx();
            //Add all shown actors
            ActorGroup.AddRange(ActiveNodes);
        }
        private void EditLevelLighting()
        {
            if (ActorGroup.IsEmpty())
            {
                var agdlg = MessageBox.Show("Adding all actors in the level to the actor group.", "Experimental Tools", MessageBoxButton.OKCancel);
                if (agdlg == MessageBoxResult.Cancel)
                    return;
                AddAllActorsToGroup();
            }

            var dlg = MessageBox.Show("Warning: Please confirm you wish to change the lighting across everything in the actor group.\n" +
                "Note that you will need to manually remake all texture lightmaps.\n" +
                "Make sure you have backups.", "Experimental Tools", MessageBoxButton.OKCancel);
            if (dlg == MessageBoxResult.Cancel)
                return;

            float brightscalar = BrightnessAdjustment;

            //Set all lights
            var lightComponentClasses = new List<string> { "PointLightComponent", "SpotLightComponent", "SkyLightComponent", "DirectionalLightComponent" };
            int n = 0;

            var allComponents = new List<ExportEntry>();
            foreach (var actor in ActorGroup)
            {
                if (actor.IsA("PrimitiveComponent"))
                {
                    allComponents.Add(actor);
                }
                else if (actor.IsA("StaticMeshActor") || actor.IsA("DynamicSMActor"))
                {
                    var comp = actor.GetProperty<ObjectProperty>("StaticMeshComponent");
                    if (comp != null)
                    {
                        allComponents.Add(Pcc.GetUExport(comp.Value));
                    }
                }
            }

            List<ExportEntry> allLightComponents = allComponents.Where(x => lightComponentClasses.Any(l => l == x.ClassName)).ToList();
            foreach (var exp in allLightComponents)
            {
                float oldred = 0;
                float oldgreen = 0;
                float oldblue = 0;
                float newred = oldred;
                float newgreen = oldgreen;
                float newblue = oldblue;
                var colorprop = exp.GetProperty<StructProperty>("LightColor");
                if (colorprop is { IsImmutable: true })
                {
                    foreach (var clrs in colorprop.Properties)
                    {
                        switch (clrs)
                        {
                            case ByteProperty fltProp when clrs.Name == "R":
                                oldred = float.Parse(fltProp.Value.ToString());
                                break;
                            case ByteProperty fltProp when clrs.Name == "G":
                                oldgreen = float.Parse(fltProp.Value.ToString());
                                break;
                            case ByteProperty fltProp when clrs.Name == "B":
                                oldblue = float.Parse(fltProp.Value.ToString());
                                break;
                        }
                    }
                }

                var newColor = AdjustColors(new LegendaryExplorerCore.SharpDX.Color(oldred, oldgreen, oldblue));
                newred = newColor.R;
                newgreen = newColor.G;
                newblue = newColor.B;

                if (!(oldblue == newblue && oldgreen == newgreen && oldred == newred))
                {
                    n++;
                    colorprop.Properties.AddOrReplaceProp(new ByteProperty(byte.Parse(newblue.ToString()), "B"));
                    colorprop.Properties.AddOrReplaceProp(new ByteProperty(byte.Parse(newgreen.ToString()), "G"));
                    colorprop.Properties.AddOrReplaceProp(new ByteProperty(byte.Parse(newred.ToString()), "R"));
                    exp.WriteProperty(colorprop);
                }
                //Adjust light brightness
                FloatProperty brightness = exp.GetProperty<FloatProperty>("Brightness");
                if (brightness == null)
                {
                    brightness = new FloatProperty(1, "Brightness");
                }
                float newbrightness = brightness.Value * (1 + brightscalar);
                if (newbrightness != 1)
                {
                    exp.WriteProperty(new FloatProperty(newbrightness, "Brightness"));
                }
            }

            MessageBox.Show($"{n} LightComponents adjusted.\n\nRecalculating static lightmaps.");

            List<ExportEntry> AllStaticMeshComponents = allComponents.Where(x => x.ClassName == "StaticMeshComponent").ToList();
            var TextureMaps = new HashSet<int>();
            foreach (var comp in AllStaticMeshComponents)
            {
                if (ObjectBinary.From(comp) is StaticMeshComponent sc)
                {
                    foreach (var lod in sc.LODData)
                    {
                        if (lod.LightMap.LightMapType == ELightMapType.LMT_2D && lod.LightMap is LightMap_2D lm2)
                        {
                            TextureMaps.Add(lm2.Texture1);
                            TextureMaps.Add(lm2.Texture2);
                            TextureMaps.Add(lm2.Texture3);
                            TextureMaps.Add(lm2.Texture4);
                        }
                        else if (lod.LightMap.LightMapType == ELightMapType.LMT_1D && lod.LightMap is LightMap_1D lm1)
                        {
                            foreach (var qds in lm1.DirectionalSamples)
                            {
                                if (qds.Coefficient1 != default)
                                {
                                    qds.Coefficient1 = AdjustColors(qds.Coefficient1, brightscalar);
                                }
                                if (qds.Coefficient2 != default)
                                {
                                    qds.Coefficient2 = AdjustColors(qds.Coefficient2, brightscalar);
                                }
                                if (qds.Coefficient3 != default)
                                {
                                    qds.Coefficient3 = AdjustColors(qds.Coefficient3, brightscalar);
                                }
                            }
                        }
                        else if (lod.LightMap.LightMapType == ELightMapType.LMT_3 && lod.LightMap is LightMap_3 lm3)
                        {
                            foreach (var qds in lm3.DirectionalSamples)
                            {
                                if (qds.Coefficient1 != default)
                                {
                                    qds.Coefficient1 = AdjustColors(qds.Coefficient1, brightscalar);
                                }
                                if (qds.Coefficient2 != default)
                                {
                                    qds.Coefficient2 = AdjustColors(qds.Coefficient2, brightscalar);
                                }
                                if (qds.Coefficient3 != default)
                                {
                                    qds.Coefficient3 = AdjustColors(qds.Coefficient3, brightscalar);
                                }
                            }
                        }
                    }
                    comp.WriteBinary(sc);
                }
            }

            MessageBox.Show($"{AllStaticMeshComponents.Count} StaticMeshComponents adjusted.");

            if (!TextureMaps.IsEmpty())
            {
                var mapdata = new List<string>();
                foreach (var e in TextureMaps.Where(e => e != 0))
                {
                    Pcc.TryGetEntry(e, out IEntry xp);
                    string tm = $"#{e} : {xp?.ObjectName.Instanced}";
                    mapdata.Add(tm);
                }
                var tdlg = new ListDialog(mapdata, "Manual changes required", "The following referenced texture maps need to be manually adjusted for lighting changes", this);
                tdlg.Show();
            }
        }
        private LegendaryExplorerCore.SharpDX.Color AdjustColors(LegendaryExplorerCore.SharpDX.Color oldcolor, float brightnesscorrectionfactor = 0)
        {
            float oldred = oldcolor.R;
            float oldgreen = oldcolor.G;
            float oldblue = oldcolor.B;
            float oldalpha = oldcolor.A;
            float newred = oldred;
            float newgreen = oldgreen;
            float newblue = oldblue;

            if (!((SwitchIgnoreRed && oldred > oldgreen && oldred > oldblue) ||  //Do switch if not in favour
                    (SwitchIgnoreGreen && oldgreen > oldred && oldgreen > oldblue) ||
                    (SwitchIgnoreBlue && oldblue < oldred && oldblue < oldgreen)))
            {
                switch (SwitchRedTo)
                {
                    case LightChannel.Red:
                        break;
                    case LightChannel.Green:
                        newred = oldgreen;
                        break;
                    case LightChannel.Blue:
                        newred = oldblue;
                        break;
                }
                switch (SwitchBlueTo)
                {
                    case LightChannel.Red:
                        newblue = oldred;
                        break;
                    case LightChannel.Green:
                        newblue = oldgreen;
                        break;
                    case LightChannel.Blue:
                        break;
                }
                switch (SwitchGreenTo)
                {
                    case LightChannel.Red:
                        newgreen = oldred;
                        break;
                    case LightChannel.Green:
                        break;
                    case LightChannel.Blue:
                        newgreen = oldblue;
                        break;
                }
            }

            brightnesscorrectionfactor = 1 + brightnesscorrectionfactor;
            newred = brightnesscorrectionfactor * (1 + AdjustRed) * newred;
            newgreen = brightnesscorrectionfactor * (1 + AdjustGreen) * newgreen;
            newblue = brightnesscorrectionfactor * (1 + AdjustBlue) * newgreen;
            if (newred > 255)
                newred = 255;
            if (newgreen > 255)
                newgreen = 255;
            if (newblue > 255)
                newblue = 255;
            var vector = new Vector4(newred / 255, newgreen / 255, newblue / 255, oldalpha / 255);
            var newColor = new LegendaryExplorerCore.SharpDX.Color(vector);
            return newColor;
        }
        private void CommitLevelShifts()
        {
            var shiftx = (float)lvlShift_X.Value;
            var shifty = (float)lvlShift_Y.Value;
            var shiftz = (float)lvlShift_Z.Value;

            if (lvlShift_X.Value == 0 && lvlShift_Y.Value == 0 && lvlShift_Z.Value == 0)
            {
                return;
            }

            if (ActorGroup.IsEmpty())
            {
                var agdlg = MessageBox.Show("Adding all actors in the level to the actor group.", "Experimental Tools", MessageBoxButton.OKCancel);
                if (agdlg == MessageBoxResult.Cancel)
                    return;
                AddAllActorsToGroup();
            }

            var chkdlg = MessageBox.Show($"WARNING: Confirm you wish to shift every actor in the group?\n" +
                $"\nX: {shiftx:+0;-0;0}\nY: {shifty}\nZ: {shiftz}\n\nThis is an experimental tool. Make backups.", "Pathfinding Editor", MessageBoxButton.OKCancel);
            if (chkdlg == MessageBoxResult.Cancel)
                return;

            ShiftActorGroup(new Vector3(shiftx, shifty, shiftz));

            MessageBox.Show("Done");
        }

        private void ShiftActorGroup(Vector3 shifts)
        {
            (List<ExportEntry> collectionActors, List<ExportEntry> otherActors) = ActorGroup.Split(actor => actor.ClassName.Contains("CollectionActor"));

            foreach (ExportEntry actor in collectionActors)
            {
                if (ObjectBinary.From(actor) is StaticCollectionActor sca
                    && actor.GetProperty<ArrayProperty<ObjectProperty>>(sca.ComponentPropName) is { } components
                    && sca.LocalToWorldTransforms.Count >= components.Count)
                {
                    for (int index = 0; index < components.Count; index++)
                    {

                        ((float posX, float posY, float posZ), Vector3 scale, Rotator rotation) = sca.LocalToWorldTransforms[index].UnrealDecompose();

                        Matrix4x4 newm = ActorUtils.ComposeLocalToWorld(new Vector3(posX + shifts.X, posY + shifts.Y, posZ + shifts.Z), rotation, scale);
                        sca.LocalToWorldTransforms[index] = newm;
                    }
                    actor.WriteBinary(sca);
                }
            }
            foreach (ExportEntry actor in otherActors)
            {
                if (actor.HasStack)
                {
                    var locationprop = actor.GetProperty<StructProperty>("location");
                    if (locationprop is { IsImmutable: true })
                    {
                        var oldx = locationprop.GetProp<FloatProperty>("X").Value;
                        var oldy = locationprop.GetProp<FloatProperty>("Y").Value;
                        var oldz = locationprop.GetProp<FloatProperty>("Z").Value;

                        float newx = oldx + shifts.X;
                        float newy = oldy + shifts.Y;
                        float newz = oldz + shifts.Z;

                        locationprop.Properties.AddOrReplaceProp(new FloatProperty(newx, "X"));
                        locationprop.Properties.AddOrReplaceProp(new FloatProperty(newy, "Y"));
                        locationprop.Properties.AddOrReplaceProp(new FloatProperty(newz, "Z"));
                        actor.WriteProperty(locationprop);
                    }
                }
                //is component without entire SCA
                else if (actor.HasParent && actor.Parent.ClassName.Contains("CollectionActor") && actor.Parent is ExportEntry actorCollection)
                {
                    if (collectionActors.Contains(actorCollection))
                    {
                        //we've already shifted all the components of this collection
                        continue;
                    }
                    if (StaticCollectionActor.TryGetStaticCollectionActorAndIndex(actor, out StaticCollectionActor sca, out int index))
                    {
                        (Vector3 translation, Vector3 scale, Rotator rotation) = sca.GetDecomposedTransformationForIndex(index);

                        sca.UpdateTransformationForIndex(index, translation + shifts, scale, rotation);
                        sca.Export.WriteBinary(sca);
                    }
                }
            }
        }

        private void CommitLevelRotation()
        {
            float rotateYawDegrees = lvlRotationYaw.Value.GetValueOrDefault();
            float rotatePitchDegrees = lvlRotationPitch.Value.GetValueOrDefault();
            float rotateYawRadians = MathF.PI * (rotateYawDegrees / 180); //Convert to radians
            float rotatePitchRadians = MathF.PI * (rotatePitchDegrees / 180); //Convert to radians
            float sinCalcYaw = MathF.Sin(rotateYawRadians);
            float cosCalcYaw = MathF.Cos(rotateYawRadians);

            if (lvlRotationYaw.Value == 0)
            {
                return;
            }

            if (ActorGroup.IsEmpty())
            {
                MessageBoxResult agdlg = MessageBox.Show("Adding all actors in the level to the actor group.", "Experimental Tools", MessageBoxButton.OKCancel);
                if (agdlg == MessageBoxResult.Cancel)
                    return;
                AddAllActorsToGroup();
            }
            MessageBoxResult chkdlg = MessageBox.Show($"WARNING: Confirm you wish to rotate the entire actor group?\n" +
                                                      $"\nHorizontal Yaw: {rotateYawDegrees} degrees\n\nThis is an experimental tool. Make backups.", "Pathfinding Editor", MessageBoxButton.OKCancel);
            if (chkdlg == MessageBoxResult.Cancel)
                return;

            Vector3 centrePivot = GetGroupCenter();

            (List<ExportEntry> collectionActors, List<ExportEntry> otherActors) = ActorGroup.Split(actor => actor.ClassName.Contains("CollectionActor"));

            foreach (ExportEntry actor in collectionActors)
            {
                if (ObjectBinary.From(actor) is StaticCollectionActor sca
                    && actor.GetProperty<ArrayProperty<ObjectProperty>>(sca.ComponentPropName) is { } components
                    && sca.LocalToWorldTransforms.Count >= components.Count)
                {

                    for (int index = 0; index < components.Count; index++)
                    {

                        ((float posX, float posY, float posZ), Vector3 scale, (int uuPitch, int uuYaw, int uuRoll)) = sca.LocalToWorldTransforms[index].UnrealDecompose();

                        float calcX = posX * cosCalcYaw - posY * sinCalcYaw;
                        float calcY = posX * sinCalcYaw + posY * cosCalcYaw;

                        int newYaw = uuYaw + rotateYawDegrees.DegreesToUnrealRotationUnits();

                        Matrix4x4 newm = ActorUtils.ComposeLocalToWorld(new Vector3(calcX, calcY, posZ),
                            new Rotator(uuPitch, newYaw, uuRoll),
                            scale);
                        sca.LocalToWorldTransforms[index] = newm;
                    }
                    actor.WriteBinary(sca);
                }
            }

            foreach (ExportEntry actor in otherActors)
            {
                if (actor == null || actor.ClassName == "BioWorldInfo")
                    continue;

                if (actor.HasStack)
                {
                    //Get existing props
                    StructProperty locationprop = actor.GetProperty<StructProperty>("location") ?? new StructProperty("location", true);

                    float oldX = locationprop.GetProp<FloatProperty>("X").Value;
                    float oldY = locationprop.GetProp<FloatProperty>("Y").Value;
                    float oldZ = locationprop.GetProp<FloatProperty>("Z").Value;

                    StructProperty rotationprop = actor.GetProperty<StructProperty>("Rotation") ?? CommonStructs.RotatorProp(new Rotator(0, 0, 0));

                    (int oldPitch, int oldYaw, int oldRoll) = CommonStructs.GetRotator(rotationprop);

                    //Get new rotation x' = x cos θ − y sin θ
                    //y' = x sin θ + y cos θ
                    float newX = oldX * cosCalcYaw - oldY * sinCalcYaw;
                    float newY = oldX * sinCalcYaw + oldY * cosCalcYaw;

                    int newYaw = oldYaw + rotateYawDegrees.DegreesToUnrealRotationUnits();

                    //Write props
                    locationprop.Properties.AddOrReplaceProp(new FloatProperty(newX, "X"));
                    locationprop.Properties.AddOrReplaceProp(new FloatProperty(newY, "Y"));
                    locationprop.Properties.AddOrReplaceProp(new FloatProperty(oldZ, "Z")); //as purely 2d rotation
                    actor.WriteProperty(locationprop);

                    var newRot = new Rotator(oldPitch, newYaw, oldRoll);
                    rotationprop = CommonStructs.RotatorProp(newRot, "Rotation");
                    actor.WriteProperty(rotationprop);
                }
                //is component without entire SCA
                else if (actor.HasParent && actor.Parent.ClassName.Contains("CollectionActor") && actor.Parent is ExportEntry actorCollection)
                {
                    if (collectionActors.Contains(actorCollection))
                    {
                        //we've already shifted all the components of this collection
                        continue;
                    }
                    if (StaticCollectionActor.TryGetStaticCollectionActorAndIndex(actor, out StaticCollectionActor sca, out int index))
                    {
                        ((float oldX, float oldY, float oldZ), Vector3 scale, (int oldPitch, int oldYaw, int oldRoll)) = sca.GetDecomposedTransformationForIndex(index);

                        float newX = oldX * cosCalcYaw - oldY * sinCalcYaw;
                        float newY = oldX * sinCalcYaw + oldY * cosCalcYaw;

                        int newYaw = oldYaw + rotateYawDegrees.DegreesToUnrealRotationUnits();

                        sca.UpdateTransformationForIndex(index, new Vector3(newX, newY, oldZ), scale, new Rotator(oldPitch, newYaw, oldRoll));
                        sca.Export.WriteBinary(sca);
                    }
                }
            }

            Vector3 newCenterPivot = GetGroupCenter();
            Vector3 shiftback = centrePivot - newCenterPivot;
            ShiftActorGroup(shiftback);
            MessageBox.Show("Done");
        }

        private Vector3 GetGroupCenter()
        {
            if (ActorGroup.IsEmpty())
            {
                return Vector3.Zero;
            }
            float groupX = 0;
            float groupY = 0;
            float groupZ = 0;
            int actorcount = 0;

            (List<ExportEntry> collectionActors, List<ExportEntry> otherActors) = ActorGroup.Split(actor => actor.ClassName.Contains("CollectionActor"));

            foreach (ExportEntry actor in collectionActors)
            {
                if (ObjectBinary.From(actor) is StaticCollectionActor sca &&
                    actor.GetProperty<ArrayProperty<ObjectProperty>>(sca.ComponentPropName) is { } components && sca.LocalToWorldTransforms.Count >= components.Count)
                {

                    for (int index = 0; index < components.Count; index++)
                    {

                        ((float posX, float posY, float posZ), Vector3 _, Rotator _) = sca.LocalToWorldTransforms[index].UnrealDecompose();

                        groupX += posX;
                        groupY += posY;
                        groupZ += posZ;
                        actorcount++;
                    }
                }
            }

            foreach (var actor in otherActors)
            {
                if (actor == null || actor.ClassName == "BioWorldInfo")
                    continue;

                if (actor.HasStack)
                {
                    var locationprop = actor.GetProperty<StructProperty>("location");
                    if (locationprop is { IsImmutable: true })
                    {
                        var oldx = locationprop.GetProp<FloatProperty>("X").Value;
                        var oldy = locationprop.GetProp<FloatProperty>("Y").Value;
                        var oldz = locationprop.GetProp<FloatProperty>("Z").Value;

                        groupX += oldx;
                        groupY += oldy;
                        groupZ += oldz;
                        actorcount++;
                    }
                }
                //is component without entire SCA
                else if (actor.HasParent && actor.Parent.ClassName.Contains("CollectionActor") && actor.Parent is ExportEntry actorCollection)
                {
                    if (collectionActors.Contains(actorCollection))
                    {
                        //we've already shifted all the components of this collection
                        continue;
                    }
                    if (StaticCollectionActor.TryGetStaticCollectionActorAndIndex(actor, out StaticCollectionActor sca, out int index))
                    {
                        ((float x, float y, float z), Vector3 _, Rotator _) = sca.GetDecomposedTransformationForIndex(index);

                        groupX += x;
                        groupY += y;
                        groupZ += z;
                        actorcount++;
                    }
                }
            }

            return new Vector3(groupX / actorcount, groupY / actorcount, groupZ / actorcount);
        }

        public async void RecookPersistantLevel()
        {
            var chkdlg = MessageBox.Show($"WARNING: Confirm you wish to recook this file?\n" +
                         $"\nThis will remove all references that current actors do not need.\nIt will then trash any entry that isn't being used.\n\n" +
                         $"This is an experimental tool. Make backups.", "Pathfinding Editor", MessageBoxButton.OKCancel);
            if (chkdlg == MessageBoxResult.Cancel)
                return;
            SetBusy("Finding unreferenced entries");
            AllowRefresh = false;
            //Find all level references
            if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is ExportEntry levelExport)
            {
                Level level = ObjectBinary.From<Level>(levelExport);
                HashSet<int> norefsList = await Task.Run(() => Pcc.GetReferencedEntries(false));
                BusyText = "Recooking the Persistant Level";
                //Get all items in the persistent level not actors
                var references = new List<int>();
                foreach (var t in level.TextureToInstancesMap)
                {
                    references.Add(t.Key);
                }
                foreach (var txtref in references)
                {
                    if (norefsList.Contains(txtref) && txtref > 0)
                    {
                        level.TextureToInstancesMap.Remove(txtref);
                    }
                }
                references.Clear();

                //Clean up Cached PhysSM Data && Rebuild Data Store
                var newPhysSMmap = new UMultiMap<int, CachedPhysSMData>();
                var newPhysSMstore = new List<KCachedConvexData>();
                foreach (var r in level.CachedPhysSMDataMap)
                {
                    references.Add(r.Key);
                }
                foreach (int reference in references)
                {
                    if (!norefsList.Contains(reference) || reference < 0)
                    {
                        var map = level.CachedPhysSMDataMap[reference];
                        var oldidx = map.CachedDataIndex;
                        var kvp = level.CachedPhysSMDataStore[oldidx];
                        map.CachedDataIndex = newPhysSMstore.Count;
                        newPhysSMstore.Add(level.CachedPhysSMDataStore[oldidx]);
                        newPhysSMmap.Add(reference, map);
                    }
                }
                level.CachedPhysSMDataMap = newPhysSMmap;
                level.CachedPhysSMDataStore = newPhysSMstore;
                references.Clear();

                //Clean up Cached PhysPerTri Data
                var newPhysPerTrimap = new UMultiMap<int, CachedPhysSMData>();
                var newPhysPerTristore = new List<KCachedPerTriData>();
                foreach (var s in level.CachedPhysPerTriSMDataMap)
                {
                    references.Add(s.Key);
                }
                foreach (int reference in references)
                {
                    if (!norefsList.Contains(reference) || reference < 0)
                    {
                        var map = level.CachedPhysPerTriSMDataMap[reference];
                        var oldidx = map.CachedDataIndex;
                        var kvp = level.CachedPhysPerTriSMDataStore[oldidx];
                        map.CachedDataIndex = newPhysPerTristore.Count;
                        newPhysPerTristore.Add(level.CachedPhysPerTriSMDataStore[oldidx]);
                        newPhysPerTrimap.Add(reference, map);
                    }
                }
                level.CachedPhysPerTriSMDataMap = newPhysPerTrimap;
                level.CachedPhysPerTriSMDataStore = newPhysPerTristore;
                references.Clear();


                //Clean up NAV data - how to clean up Nav ints?  [Just null unwanted refs]
                if (norefsList.Contains(level.NavListStart))
                {
                    level.NavListStart = 0;
                }
                if (norefsList.Contains(level.NavListEnd))
                {
                    level.NavListEnd = 0;
                }
                var newNavArray = new List<int>();
                newNavArray.AddRange(level.NavRefs);

                for (int n = 0; n < level.NavRefs.Count; n++)
                {
                    if (norefsList.Contains(newNavArray[n]))
                    {
                        newNavArray[n] = 0;
                    }
                }
                level.NavRefs = newNavArray;

                //Clean up Coverlink Lists => pare down guid2byte? table [Just null unwanted refs]
                if (norefsList.Contains(level.CoverListStart))
                {
                    level.CoverListStart = 0;
                }
                if (norefsList.Contains(level.CoverListEnd))
                {
                    level.CoverListEnd = 0;
                }
                var newCLArray = new List<int>();
                newCLArray.AddRange(level.CoverLinkRefs);
                for (int l = 0; l < level.CoverLinkRefs.Count; l++)
                {
                    if (norefsList.Contains(newCLArray[l]))
                    {
                        newCLArray[l] = 0;
                    }
                }
                level.CoverLinkRefs = newCLArray;

                if (Pcc.Game.IsGame3())
                {
                    //Clean up Pylon List
                    if (norefsList.Contains(level.PylonListStart))
                    {
                        level.PylonListStart = 0;
                    }
                    if (norefsList.Contains(level.PylonListEnd))
                    {
                        level.PylonListEnd = 0;
                    }
                }

                //Cross Level Actors
                level.CoverLinkRefs = newCLArray;
                var newXLArray = new List<int>();
                newXLArray.AddRange(level.CrossLevelActors);
                foreach (int xlvlactor in level.CrossLevelActors)
                {
                    if (norefsList.Contains(xlvlactor) || xlvlactor == 0)
                    {
                        newXLArray.Remove(xlvlactor);
                    }
                }
                level.CrossLevelActors = newXLArray;

                //Clean up int lists if empty of NAV points
                if (level.NavRefs.IsEmpty() && level.CoverLinkRefs.IsEmpty() && level.CrossLevelActors.IsEmpty() && (!Pcc.Game.IsGame3() || level.PylonListStart == 0))
                {
                    level.CrossLevelCoverGuidRefs.Clear();
                    level.CoverIndexPairs.Clear();
                    level.CoverIndexPairs.Clear();
                    level.NavRefIndicies.Clear();
                }

                levelExport.WriteBinary(level);

                var tdlg = MessageBox.Show("The recooker will now trash any entries that are not being used.\n\nThis is experimental. Keep backups.", "WARNING", MessageBoxButton.OKCancel);
                if (tdlg == MessageBoxResult.Cancel)
                    return;
                BusyText = "Trashing unwanted items";
                var itemsToTrash = new List<IEntry>();
                foreach (var export in Pcc.Exports)
                {
                    if (norefsList.Contains(export.UIndex))
                    {
                        itemsToTrash.Add(export);
                    }
                }
                //foreach (var import in Pcc.Imports)  //Don't trash imports until UnrealScript functions can be fully parsed.
                //{
                //    if (norefsList.Contains(import.UIndex))
                //    {
                //        itemsToTrash.Add(import);
                //    }
                //}

                EntryPruner.TrashEntries(Pcc, itemsToTrash);
            }
            AllowRefresh = true;
            EndBusy();
            MessageBox.Show("Trash Compactor Done");
        }
        private void TrashActorGroup()
        {
            if (ActorGroup.IsEmpty())
            {
                MessageBox.Show("No actors in the current group", "Pathfinding Editor", MessageBoxButton.OK);
                return;
            }

            var chkdlg = MessageBox.Show($"WARNING: Do you want to trash all the actors in the group?\n" +
             $"\nThis will remove every actor selected in the current group and trash them.\nThis will not remove assets they use - such as textures or meshes.\n\n" +
             $"This is an experimental tool. Make backups.", "Pathfinding Editor", MessageBoxButton.OKCancel);
            if (chkdlg == MessageBoxResult.Cancel)
                return;
            SetBusy("Trashing Actor Group and removing from level");
            AllowRefresh = false;
            var trashcollections = new List<ExportEntry>();
            foreach (var trashactor in ActorGroup)
            {
                if (trashactor.ClassName.Contains("CollectionActor"))
                {
                    trashcollections.Add(trashactor); //Only remove collections once components have been deleted.
                }
                else
                {
                    TrashActor(trashactor);
                }
            }

            foreach (var trashcollection in trashcollections)
            {
                if (PathEdUtils.GetCollectionItems(trashcollection).Count is int tcObjCnt && tcObjCnt > 0)
                {
                    var tcdlg = MessageBox.Show($"You are trashing #{trashcollection.UIndex} {trashcollection.ObjectName.Instanced} which has {tcObjCnt} components that have not been marked for trashing." +
                        $"\n\nDo you want to trash the entire collection", "Pathfinding Editor", MessageBoxButton.YesNo);
                    if (tcdlg == MessageBoxResult.No)
                        continue;
                }
                TrashActor(trashcollection);
            }

            ActorGroup.ClearEx();
            AllowRefresh = true;
            ActiveNodes_ListBox.SelectedIndex = -1; //Reset selection and will force refresh
            EndBusy();
            MessageBox.Show("Trashed selected actors and removed them from level.");
        }
        #endregion


        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "PathfindingEditor";

        private void MultiCloneNode_Clicked(object sender, RoutedEventArgs e)
        {
            string result = PromptDialog.Prompt(this, "How many times do you want to clone this node?", "Multiple node cloning", "2", true);
            if (int.TryParse(result, out int howManyTimes) && howManyTimes > 0)
            {
                var nodeToClone = ActiveNodes_ListBox.SelectedItem as ExportEntry;
                ExportEntry lastNewNode = null;
                for (int i = 0; i < howManyTimes; i++)
                {
                    lastNewNode = cloneNode(nodeToClone, i * 50);
                }

                if (lastNewNode != null)
                {
                    ActiveNodes_ListBox.SelectedItem = lastNewNode;
                }
            }
        }

        private bool gpsActive;
        private void ConnectGPS_Clicked(object sender, RoutedEventArgs e)
        {
            if (Pcc == null)
            {
                MessageBox.Show("You must load a package first, which determines which game this feature will be used on.");
                return;
            }
            if (!InteropHelper.IsASILoaderInstalled(Pcc.Game))
            {
                if (MessageBoxResult.Yes == MessageBox.Show(this, $"The latest asi loader for {Pcc.Game} is not installed! You must install it and restart the game for this feature to work." +
                                                                  $" Would you like to install the asi loader now?", "ASI loader not installed!", MessageBoxButton.YesNo))
                {
                    InteropHelper.OpenASILoaderDownload(Pcc.Game);
                }
                return;
            }
            if (!InteropHelper.IsInteropASIInstalled(Pcc.Game))
            {
                if (MessageBoxResult.Yes == MessageBox.Show(this, $"The latest interop asi for {Pcc.Game} is not installed! You must install it and restart the game for this feature to work." +
                                                                  $" Would you like to install the asi now?", "Interop ASI not installed!", MessageBoxButton.YesNo))
                {
                    InteropHelper.OpenInteropASIDownload(Pcc.Game);
                }
                return;
            }
            if (InteropHelper.IsGameClosed(Pcc.Game))
            {
                MessageBox.Show(this, $"{Pcc.Game} must be running for this feature to have an effect!");
                return;
            }
            Task.Run(() =>
            {
                if (!gpsActive)
                {
                    if (PlayerGPSObject == null)
                    {
                        PlayerGPSObject = new PlayerGPSNode(0, 0, graphEditor);
                        graphEditor.addNode(PlayerGPSObject);
                    }

                    InteropHelper.SendMessageToGame("ACTIVATE_PLAYERGPS", Pcc.Game);
                    gpsActive = true;
                }
                else
                {
                    InteropHelper.SendMessageToGame("DEACTIVATE_PLAYERGPS", Pcc.Game);
                    gpsActive = false;
                }
            });
        }

        private void OpenOtherVersion()
        {
            var result = CrossGenHelpers.FetchOppositeGenPackage(Pcc, out var otherGen);
            if (result != null)
            {
                MessageBox.Show(result);
            }
            else
            {
                var nodeEntry = (ExportEntry)ActiveNodes_ListBox.SelectedItem;
                PathfindingEditorWindow pe = new PathfindingEditorWindow(otherGen);
                if (nodeEntry != null)
                {
                    pe.ExportQueuedForFocus = otherGen.FindExport(nodeEntry.InstancedFullPath);
                }
                pe.Show();
            }
        }

        /// <summary>
        /// Adds all the PathNodes in the package to the selected BioSquadCombat's AssignedPathNodes array.
        /// </summary>
        private void AddAllPathnodesToBioSquadCombat()
        {
            if (Pcc == null || ActiveNodes_ListBox?.SelectedItem is not ExportEntry bioSquadCombat) { return; }

            if (bioSquadCombat.ClassName != "BioSquadCombat")
            {
                MessageBox.Show("Selected export is not a BioSquadCombat", "Warning", MessageBoxButton.OK);
                return;
            }

            ArrayProperty<StructProperty> m_aoAssignedPathNodes = new("m_aoAssignedPathNodes");
            foreach (IEntry entry in Pcc.Exports)
            {
                if (entry.ClassName != "PathNode") { continue; } // Faster than filtering and then doing something

                PropertyCollection props = new()
                {
                    new ObjectProperty(entry.UIndex, "oPoint"),
                    new ObjectProperty(0, "oLockedBy")
                };
                m_aoAssignedPathNodes.Add(new StructProperty("LockedPoint", props, "LockedPoint", false));
            }

            int assignedCount = m_aoAssignedPathNodes.Count;
            if (assignedCount == 0)
            {
                MessageBox.Show("No PathNodes were found on the file", "Warning", MessageBoxButton.OK);
                return;
            }

            bioSquadCombat.WriteProperty(m_aoAssignedPathNodes);

            string message = (assignedCount == 1) ? "one path node" : $"{assignedCount} path nodes";
            MessageBox.Show($"Added {message}.", "Success", MessageBoxButton.OK);
        }

        #region Busy

        public override void SetBusy(string text = null)
        {
            var graphImage = graphEditor.Camera.ToImage((int)graphEditor.Camera.GlobalFullWidth, (int)graphEditor.Camera.GlobalFullHeight, new SolidBrush(GraphEditorBackColor));
            graphImageSub.Source = graphImage.ToBitmapImage();
            graphImageSub.Width = GraphHost.ActualWidth;
            graphImageSub.Height = GraphHost.ActualHeight;
            graphImageSub.Visibility = Visibility.Visible;
            GraphHost.Visibility = Visibility.Collapsed;
            BusyText = text;
            IsBusy = true;
        }

        public override void EndBusy()
        {
            IsBusy = false;
            graphImageSub.Visibility = Visibility.Collapsed;
            GraphHost.Visibility = Visibility.Visible;
        }

        #endregion
    }
}