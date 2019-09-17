using ME3Explorer.ActorNodes;
using ME3Explorer.Packages;
using ME3Explorer.PathfindingNodes;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.SplineNodes;
using ME3Explorer.Unreal;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using static ME3Explorer.PathfindingNodes.PathfindingNode;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using Point = System.Windows.Point;

namespace ME3Explorer.Pathfinding_Editor
{
    /// <summary>
    /// Interaction logic for PathfindingEditorWPF.xaml
    /// </summary>
    public partial class PathfindingEditorWPF : WPFBase, IBusyUIHost
    {

        public static string[] pathfindingNodeClasses =
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

            //ME1
            "BioWp_DefensePoint", "BioWp_AssaultPoint", "BioWp_ActionStation"
        };

        public static string[] actorNodeClasses =
        {
            "BlockingVolume", "BioPlaypenVolumeAdditive", "DynamicBlockingVolume", "DynamicTriggerVolume", "SFXMedkit",
            "StaticMeshActor", "SFXMedStation", "InterpActor", "SFXDoor", "BioTriggerVolume", "TargetPoint",
            "SFXArmorNode", "BioTriggerStream", "SFXTreasureNode", "SFXPointOfInterest", "SFXPlaceable_Generator",
            "SFXPlaceable_ShieldGenerator", "SFXBlockingVolume_Ledge", "SFXAmmoContainer_Simulator", "SFXAmmoContainer",
            "SFXGrenadeContainer", "SFXCombatZone", "BioStartLocation", "BioStartLocationMP", "SFXStuntActor",
            "SkeletalMeshActor", "WwiseAmbientSound", "WwiseAudioVolume", "SFXOperation_ObjectiveSpawnPoint",
            "BioPawn"
        };

        public static string[] splineNodeClasses = { "SplineActor" };

        public static string[] ignoredobjectnames =
        {
            "PREFAB_Ladders_3M_Arc0", "PREFAB_Ladders_3M_Arc1"
        }; //These come up as parsed classes but aren't actually part of the level, only prefabs. They should be ignored

        //Layers
        private List<PathfindingNodeMaster> GraphNodes;
        private bool ChangingSelectionByGraphClick;
        private ExportEntry PersistentLevelExport;

        private readonly PathingGraphEditor graphEditor;
        private bool AllowRefresh;
        public PathingZoomController zoomController;

        private string FileQueuedForLoad;

        public ObservableCollectionExtended<ExportEntry> ActiveNodes { get; set; } = new ObservableCollectionExtended<ExportEntry>();
        public ObservableCollectionExtended<ExportEntry> ActiveOverlayNodes { get; set; } = new ObservableCollectionExtended<ExportEntry>();

        public ObservableCollectionExtended<string> TagsList { get; set; } = new ObservableCollectionExtended<string>();

        public ObservableCollectionExtended<StaticMeshCollection> StaticMeshCollections { get; set; } = new ObservableCollectionExtended<StaticMeshCollection>();

        public ObservableCollectionExtended<Zone> CombatZones { get; } = new ObservableCollectionExtended<Zone>();

        public ObservableCollectionExtended<Zone> CurrentNodeCombatZones { get; } = new ObservableCollectionExtended<Zone>();

        private readonly List<ExportEntry> AllLevelObjects = new List<ExportEntry>();
        private readonly List<ExportEntry> AllOverlayObjects = new List<ExportEntry>();
        public string CurrentFile;
        private readonly PathfindingMouseListener pathfindingMouseListener;

        private string _currentNodeXY = "Undefined";

        public string CurrentNodeXY
        {
            get => _currentNodeXY;
            set => SetProperty(ref _currentNodeXY, value);
        }

        public PathfindingEditorWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Pathfinding Editor WPF", new WeakReference(this));
            DataContext = this;
            StatusText = "Select package file to load";
            LoadCommands();
            InitializeComponent();
            ContextMenu contextMenu = (ContextMenu)FindResource("nodeContextMenu");
            contextMenu.DataContext = this;
            graphEditor = (PathingGraphEditor)GraphHost.Child;
            graphEditor.BackColor = System.Drawing.Color.FromArgb(130, 130, 130);
            AllowRefresh = true;
            LoadRecentList();
            RefreshRecent(false);
            zoomController = new PathingZoomController(graphEditor);
            SharedPathfinding.LoadClassesDB();
            List<PathfindingDB_ExportType> types = SharedPathfinding.ExportClassDB.Where(x => x.pathnode).ToList();
            foreach (var type in types)
            {
                NodeType nt = new NodeType(type, false);
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

        public PathfindingEditorWPF(string fileName) : this()
        {
            FileQueuedForLoad = fileName;
        }

        #region Properties and Bindings

        private bool _showVolumes_BioTriggerVolumes;
        private bool _showVolumes_BioTriggerStreams;
        private bool _showVolumes_BlockingVolumes;
        private bool _showVolumes_DynamicBlockingVolumes;
        private bool _showVolumes_SFXBlockingVolume_Ledges;
        private bool _showVolumes_SFXCombatZones;
        private bool _showVolumes_WwiseAudioVolumes;
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

        public bool ShowVolumes_DynamicBlockingVolumes
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

        private bool _showActorsLayer;
        private bool _showSplinesLayer;
        private bool _showPathfindingNodesLayer = true;
        private bool _showEverythingElseLayer;

        public bool ShowActorsLayer
        {
            get => _showActorsLayer;
            set => SetProperty(ref _showActorsLayer, value);
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

        public ICommand TogglePathfindingCommand { get; set; }
        public ICommand ToggleEverythingElseCommand { get; set; }
        public ICommand ToggleActorsCommand { get; set; }
        public ICommand ToggleSplinesCommand { get; set; }
        public ICommand ToggleSequenceReferencesCommand { get; set; }
        public ICommand ShowBioTriggerVolumesCommand { get; set; }
        public ICommand ShowBioTriggerStreamsCommand { get; set; }
        public ICommand ShowBlockingVolumesCommand { get; set; }
        public ICommand ShowDynamicBlockingVolumesCommand { get; set; }

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
            ToggleSplinesCommand = new GenericCommand(ToggleSplines, PackageIsLoaded);
            ToggleSequenceReferencesCommand = new GenericCommand(ToggleSequenceReferences, PackageIsLoaded);
            ShowBioTriggerVolumesCommand = new GenericCommand(ShowBioTriggerVolumes, PackageIsLoaded);
            ShowBioTriggerStreamsCommand = new GenericCommand(ShowBioTriggerStreams, PackageIsLoaded);
            ShowBlockingVolumesCommand = new GenericCommand(ShowBlockingVolumes, PackageIsLoaded);
            ShowDynamicBlockingVolumesCommand = new GenericCommand(ShowDynamicBlockingVolumes, PackageIsLoaded);
            ShowSFXBlockingVolumeLedgesCommand = new GenericCommand(ShowSFXBlockingVolumeLedges, PackageIsLoaded);
            ShowSFXCombatZonesCommand = new GenericCommand(ShowSFXCombatZones, PackageIsLoaded);
            ShowWwiseAudioVolumesCommand = new GenericCommand(ShowWwiseAudioVolumes, PackageIsLoaded);

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
        }

        private void CalculateInterpStartEndTargetpoint()
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry targetpointAnchorEnd && targetpointAnchorEnd.ClassName == "TargetPoint")
            {
                var movingObject = EntrySelector.GetEntry(this, Pcc, EntrySelector.SupportedTypes.Exports, "Select a level object that will be moved along the curve. This will be the starting point.");
                if (movingObject == null) return;

                ActiveNodes_ListBox.SelectedItem = movingObject as ExportEntry;

                var interpTrack = (ExportEntry)EntrySelector.GetEntry(this, Pcc, EntrySelector.SupportedTypes.Exports, "Select the interptrackmove data that we will modify for these points.");
                if (interpTrack == null) return;

                var locationTarget = SharedPathfinding.GetLocation(targetpointAnchorEnd);
                var locationStart = SharedPathfinding.GetLocation(movingObject as ExportEntry);

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
                SharedPathfinding.SetLocation(posTrackPoints[0].GetProp<StructProperty>("OutVal"), (float)deltaX, (float)deltaY, (float)deltaZ);
                SharedPathfinding.SetLocation(posTrackPoints[posTrackPoints.Count - 1].GetProp<StructProperty>("OutVal"), 0, 0, 0);

                interpTrack.WriteProperty(posTrack);
            }
        }

        private bool TargetPointIsSelected()
        {
            return ActiveNodes_ListBox.SelectedItem is ExportEntry exp && exp.ClassName == "TargetPoint";
        }

        private void LoadOverlay()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
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

            using (var overlayPackage = MEPackageHandler.OpenMEPackage(fileName))
            {
                OverlayPersistentLevelExport = overlayPackage.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
                if (OverlayPersistentLevelExport == null)
                {
                    MessageBox.Show("This file does not contain a Level export.");
                    return;
                }
                RefreshGraph();
            }
        }

        public ExportEntry OverlayPersistentLevelExport { get; set; }

        private void CheckNetIndexes()
        {
            List<int> indexes = new List<int>();
            foreach (PathfindingNodeMaster m in GraphNodes)
            {
                int nindex = m.export.NetIndex;
                if (indexes.Contains(nindex))
                {
                    Debug.WriteLine("Duplicate netindex " + nindex + ": Found a duplicate on " + m.export.GetIndexedFullPath);
                }
                else
                {
                    indexes.Add(nindex);
                }
            }
        }

        private void OpenRefInSequenceEditor(object obj)
        {
            if (obj is ExportEntry exp)
            {
                AllowWindowRefocus = false;
                SequenceEditorWPF seqed = new SequenceEditorWPF(exp);
                seqed.Show();
                seqed.Activate();
            }
        }

        private void ToggleSequenceReferences()
        {
            ShowSequenceReferences = !ShowSequenceReferences;
            RefreshGraph();
        }

        private void ChangeNodeType()
        {
            Debug.WriteLine("Changing node type");
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry exp &&
                AvailableNodeTypes_ListBox.SelectedItem is NodeType type)
            {
                changeNodeType(exp, type);
            }
        }


        private bool CanChangeNodetype() => true;

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
                var ensuredProperties = new List<UProperty>();
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
                foreach (UProperty prop in ensuredProperties)
                {
                    if (!nodeProperties.ContainsNamedProp(prop.Name))
                    {
                        nodeProperties.Add(prop);
                    }
                }

                //Change collision cylinder
                ObjectProperty cylindercomponent = nodeProperties.GetProp<ObjectProperty>("CollisionComponent");
                ExportEntry cylindercomponentexp = Pcc.getUExport(cylindercomponent.Value);

                //Ensure all classes are imported.
                IEntry newnodeclassimp = SharedPathfinding.GetEntryOrAddImport(Pcc, exportTypeInfo.fullclasspath);
                IEntry newcylindercomponentimp = SharedPathfinding.GetEntryOrAddImport(Pcc, exportTypeInfo.cylindercomponentarchetype);

                if (newnodeclassimp != null)
                {
                    nodeEntry.idxClass = newnodeclassimp.UIndex;
                    nodeEntry.idxObjectName = Pcc.FindNameOrAdd(exportTypeInfo.nodetypename);
                    SharedPathfinding.ReindexMatchingObjects(nodeEntry);
                    cylindercomponentexp.idxArchtype = newcylindercomponentimp.UIndex;
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
                            ExportEntry spec = Pcc.getUExport(pathObj.Value);
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
                                            SharedPathfinding.SetReachSpecSize(inboundSpec,
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
        ImportEntry newnodeclassimp = SharedPathfinding.GetOrAddImport(Pcc, newclass);
        ImportEntry newcylindercomponentimp =
            SharedPathfinding.GetOrAddImport(Pcc, newcylindercomponentarchetype);

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
                                        if (rprop.Name == SharedPathfinding.GetReachSpecEndName(spec))
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
                                        SharedPathfinding.GetOrAddImport(Pcc,
                                            "SFXGame.SFXLargeBoostReachSpec");

                                    if (newReachSpecClass != null)
                                    {
                                        spec.idxClass = newReachSpecClass.UIndex;
                                        spec.idxObjectName = Pcc.FindNameOrAdd("SFXLargeBoostReachSpec");
                                        //set spec to banshee sized
                                        SharedPathfinding.SetReachSpecSize(spec,
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
                                                        SharedPathfinding.GetReachSpecEndName(inboundSpec))
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
                                                            SharedPathfinding.SetReachSpecSize(inboundSpec,
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
            var reachspecendname = SharedPathfinding.GetReachSpecEndName(spec);

            //Get destination
            PropertyCollection specprops = spec.GetProperties();
            int start = specprops.GetProp<ObjectProperty>("Start").Value;
            if (specprops.FirstOrDefault(x => x.Name == "End") is StructProperty reachspecendprop)
            {
                PropertyCollection reachspecprops = reachspecendprop.Properties;
                if (reachspecprops.FirstOrDefault(x => x.Name == reachspecendname) is ObjectProperty otherNodeIdxProp)
                {
                    int othernodeidx = otherNodeIdxProp.Value;

                    if (!Pcc.isUExport(othernodeidx)) return; //skip as this is not proper data
                    ExportEntry specDest = Pcc.getUExport(othernodeidx);

                    //Check for same as changing to type, ensure spec type is correct
                    if (specDest.ClassName == exportTypeInfo.nodetypename && spec.ClassName != exportTypeInfo.inboundspectype)
                    {
                        //Change the reachspec info outgoing to this node...
                        IEntry newReachSpecClass = SharedPathfinding.GetEntryOrAddImport(Pcc, exportTypeInfo.inboundspectype);

                        if (newReachSpecClass != null)
                        {
                            spec.idxClass = newReachSpecClass.UIndex;
                            spec.idxObjectName = Pcc.FindNameOrAdd(exportTypeInfo.nodetypename);
                            //set spec to banshee sized
                            SharedPathfinding.SetReachSpecSize(spec,
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
                                        spec = Pcc.getUExport(pathObj.Value);
                                        EnsureLargeAndReturning(spec, exportTypeInfo, false);
                                        break; //this will only need to run once since there is only 1:1 reach specs
                                    }
                                }
                            }
                        }

                        SharedPathfinding.ReindexMatchingObjects(spec);
                    }
                }
            }
        }

        private void PopoutInterpreterWPF(object obj)
        {
            ExportEntry export = (ExportEntry)ActiveNodes_ListBox.SelectedItem;
            ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new InterpreterWPF(), export)
            {
                Title = $"Interpreter - {export.UIndex} {export.GetIndexedFullPath} - {Pcc.FilePath}"
            };
            elhw.Show();
        }

        private bool NodeIsSelected(object obj)
        {
            return ActiveNodes_ListBox.SelectedItem is ExportEntry;
        }

        private void AddExportToLevel()
        {
            if (EntrySelector.GetEntry(this, Pcc, EntrySelector.SupportedTypes.Exports) is ExportEntry selectedEntry)
            {

                if (!AllLevelObjects.Contains(selectedEntry))
                {
                    byte[] leveldata = PersistentLevelExport.Data;
                    int start = PersistentLevelExport.propsEnd();
                    //Console.WriteLine("Found start of binary at {start.ToString("X8"));

                    uint exportid = BitConverter.ToUInt32(leveldata, start);
                    start += 4;
                    uint numberofitems = BitConverter.ToUInt32(leveldata, start);
                    numberofitems++;
                    leveldata.OverwriteRange(start, BitConverter.GetBytes(numberofitems));

                    //Debug.WriteLine("Size before: {memory.Length);
                    //memory = RemoveIndices(memory, offset, size);
                    int offset = (int)(start + numberofitems * 4); //will be at the very end of the list as it is now +1
                    List<byte> memList = leveldata.ToList();
                    memList.InsertRange(offset, BitConverter.GetBytes(selectedEntry.UIndex));
                    leveldata = memList.ToArray();
                    PersistentLevelExport.Data = leveldata;
                    RefreshGraph();
                }
                else
                {
                    MessageBox.Show($"{selectedEntry.UIndex} {selectedEntry.GetIndexedFullPath} is already in the level.");
                }
            }
        }

        private void ToggleNodeSizesDisplay()
        {
            ShowNodeSizes_MenuItem.IsChecked = !ShowNodeSizes_MenuItem.IsChecked;
            Properties.Settings.Default.PathfindingEditorShowNodeSizes = ShowNodeSizes_MenuItem.IsChecked;
            Properties.Settings.Default.Save();
            RefreshGraph();
        }

        private void BuildPathfindingChainExperiment()
        {
            OpenFileDialog d = new OpenFileDialog
            {
                Filter = "Point Logger ASI file output (txt)|*txt"
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
                            SharedPathfinding.CreateReachSpec(previousNode, true, newNode, "Engine.ReachSpec", new ReachSpecSize(null, ReachSpecSize.BOSS_HEIGHT, ReachSpecSize.BOSS_RADIUS));
                        }
                        if (firstNode == null)
                        {
                            firstNode = newNode;
                        }
                        previousNode = newNode;
                    }
                }
                //createReachSpec(previousNode, true, firstNode.Index, "Engine.ReachSpec", 1, 0);

                PathfindingEditorWPF_ValidationPanel.fixStackHeaders();
                PathfindingEditorWPF_ValidationPanel.relinkPathfindingChain();
                PathfindingEditorWPF_ValidationPanel.recalculateReachspecs();
                //ReachSpecRecalculator rsr = new ReachSpecRecalculator(this);
                //rsr.ShowDialog(this);
                Debug.WriteLine("Done");
            }
        }

        private void FlipLevel()
        {
            foreach (ExportEntry exp in Pcc.Exports)
            {
                switch (exp.ObjectName)
                {
                    case "StaticMeshCollectionActor":
                        {
                            //This is going to get ugly.

                            byte[] data = exp.Data;
                            //get a list of staticmesh stuff from the props.
                            int listsize = BitConverter.ToInt32(data, 28);
                            var smacitems = new List<ExportEntry>();
                            for (int i = 0; i < listsize; i++)
                            {
                                int offset = 32 + i * 4;
                                //fetch exports
                                int entryval = BitConverter.ToInt32(data, offset);
                                if (entryval > 0 && entryval < Pcc.ExportCount)
                                {
                                    ExportEntry export = (ExportEntry)Pcc.getEntry(entryval);
                                    smacitems.Add(export);
                                }
                                else if (entryval == 0)
                                {
                                    smacitems.Add(null);
                                }
                            }

                            //find start of class binary (end of props)
                            int start = exp.propsEnd();

                            if (data.Length - start < 4)
                            {
                                return;
                            }

                            //Lets make sure this binary is divisible by 64.
                            if ((data.Length - start) % 64 != 0)
                            {
                                return;
                            }

                            int smcaindex = 0;
                            while (start < data.Length && smcaindex < listsize - 1)
                            {
                                float x = BitConverter.ToSingle(data, start + smcaindex * 64 + (12 * 4));
                                float y = BitConverter.ToSingle(data, start + smcaindex * 64 + (13 * 4));
                                float z = BitConverter.ToSingle(data, start + smcaindex * 64 + (14 * 4));
                                data.OverwriteRange(start + smcaindex * 64 + (12 * 4), BitConverter.GetBytes(x * -1));
                                data.OverwriteRange(start + smcaindex * 64 + (13 * 4), BitConverter.GetBytes(y * -1));
                                data.OverwriteRange(start + smcaindex * 64 + (14 * 4), BitConverter.GetBytes(z * -1));

                                InvertScalingOnExport(smacitems[smcaindex], "Scale3D");
                                smcaindex++;
                                Debug.WriteLine($"{exp.Index} {smcaindex} SMAC Flipping {x},{y},{z}");
                            }
                            exp.Data = data;
                        }
                        break;
                    default:
                        {
                            var props = exp.GetProperties();
                            StructProperty locationProp = props.GetProp<StructProperty>("location");
                            if (locationProp != null)
                            {
                                FloatProperty xProp = locationProp.Properties.GetProp<FloatProperty>("X");
                                FloatProperty yProp = locationProp.Properties.GetProp<FloatProperty>("Y");
                                FloatProperty zProp = locationProp.Properties.GetProp<FloatProperty>("Z");
                                Debug.WriteLine($"{exp.Index} {exp.ObjectName}Flipping {xProp.Value},{yProp.Value},{zProp.Value}");

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
            bool hasDrawScale = drawScale3D != null;
            if (drawScale3D == null)
            {

                //What in god's name is this still doing here
                drawScale3D = new StructProperty("Vector", new PropertyCollection
                {
                    new FloatProperty(0, "X"),
                    new FloatProperty(0, "Y"),
                    new FloatProperty(0, "Z")
                }, "DrawScale3D", true);
            }
            var drawScaleX = drawScale3D.GetProp<FloatProperty>("X");
            var drawScaleY = drawScale3D.GetProp<FloatProperty>("Y");
            var drawScaleZ = drawScale3D.GetProp<FloatProperty>("Z");
            if (!hasDrawScale)
            {
                drawScaleX.Value = -1;
                drawScaleY.Value = -1;
                drawScaleZ.Value = -1;
            }
            else
            {
                drawScaleX.Value = -drawScaleX.Value;
                drawScaleY.Value = -drawScaleY.Value;
                drawScaleZ.Value = -drawScaleZ.Value;
            }
            exp.WriteProperty(drawScale3D);
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

        private void ShowDynamicBlockingVolumes()
        {
            foreach (var x in GraphNodes.OfType<DynamicBlockingVolume>())
            {
                x.SetShape(ShowVolumes_DynamicBlockingVolumes);
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
            RefreshGraph();
        }

        private void ToggleSplines()
        {
            ShowSplinesLayer = !ShowSplinesLayer;
            RefreshGraph();
        }

        private void ToggleEverythingElse()
        {
            ShowEverythingElseLayer = !ShowEverythingElseLayer;
            RefreshGraph();
        }

        private void TogglePathfindingNodes()
        {
            ShowPathfindingNodesLayer = !ShowPathfindingNodesLayer;
            RefreshGraph();
        }

        private void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done.");
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

        private void SavePackage() => Pcc.save();

        private void OpenPackage()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
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

        private bool PackageIsLoaded() => Pcc != null;

        private void RefreshGraph()
        {
            if (AllowRefresh)
            {
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                ActiveNodes.ClearEx();
                ActiveOverlayNodes.ClearEx();
                GraphNodes.Clear();
                StaticMeshCollections.ClearEx();
                CombatZones.ClearEx();
                LoadPathingNodesFromLevel();
                GenerateGraph();
                graphEditor.Refresh();
            }
        }

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


        public string CurrentFilteringText
        {
            get
            {
                switch (ZFilteringMode)
                {
                    case EZFilterIncludeDirection.None:
                        return "Showing all nodes";
                    case EZFilterIncludeDirection.Above:
                        return $"Showing all nodes above Z={ZFilteringValue}";
                    case EZFilterIncludeDirection.AboveEquals:
                        return $"Showing all nodes at or above Z={ZFilteringValue}";
                    case EZFilterIncludeDirection.Below:
                        return $"Showing all nodes below Z={ZFilteringValue}";
                    case EZFilterIncludeDirection.BelowEquals:
                        return $"Showing all nodes at or below Z={ZFilteringValue}";
                    default:
                        return "Unknown";
                }
            }
        }

        public string NodeTypeDescriptionText
        {
            get
            {
                if (ActiveNodes_ListBox?.SelectedItem is ExportEntry CurrentLoadedExport)
                {
                    if (SharedPathfinding.ExportClassDB.FirstOrDefault(x => x.nodetypename == CurrentLoadedExport.ClassName) is PathfindingDB_ExportType classinfo)
                    {
                        return classinfo.description;
                    }

                    return "This node type does not have any information detailed about its purpose.";
                }

                return "No node is currently selected";
            }
        }

        #endregion

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
                var currentlocation = SharedPathfinding.GetLocation(export);
                CurrentNodeXY = $"{s.GlobalBounds.X},{s.GlobalBounds.Y}";

            }
            contextMenu.IsOpen = true;
            graphEditor.DisableDragging();
        }

        private void LoadFile(string fileName)
        {
            CurrentFile = null;
            ActiveNodes.ClearEx();
            StaticMeshCollections.ClearEx();
            CombatZones.ClearEx();
            StatusText = $"Loading {Path.GetFileName(fileName)}";
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);

            LoadMEPackage(fileName);
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
                    RectangleF panToRectangle = new RectangleF(graphcenter, new SizeF(200, 200));
                    graphEditor.Camera.AnimateViewToCenterBounds(panToRectangle, false, 1000);
                    ChangingSelectionByGraphClick = false;
                }
                else
                {
                    NodeName = "No node selected";
                }
                CurrentFile = Path.GetFileName(fileName);
                AddRecent(fileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
                Title = $"Pathfinding Editor WPF - {fileName}";
                StatusText = null; //Nothing to prepend.
                PathfindingEditorWPF_ValidationPanel.SetLevel(PersistentLevelExport);
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
        /// Reads the persistent level export and loads the pathfindingnodemasters that will be used in the graph.
        /// This method will recursively call itself - do not pass in a parameter from an external call.
        /// </summary>
        /// <param name="isOverlay"></param>
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
            var AllObjectsList = isOverlay ? AllOverlayObjects : AllLevelObjects;
            var ActiveObjectsList = isOverlay ? ActiveOverlayNodes : ActiveNodes;

            AllObjectsList.Clear();

            //Read persistent level binary
            byte[] data = levelToRead.Data;

            //find start of class binary (end of props)
            int start = levelToRead.propsEnd();

            //Console.WriteLine("Found start of binary at " + start.ToString("X8"));

            uint exportid = BitConverter.ToUInt32(data, start);
            start += 4;
            uint numberofitems = BitConverter.ToUInt32(data, start);
            int countoffset = start;

            start += 4;
            int bioworldinfoexportid = BitConverter.ToInt32(data, start);

            ExportEntry bioworldinfo = levelToRead.FileRef.getUExport(bioworldinfoexportid);
            if (bioworldinfo.ObjectName != "BioWorldInfo")
            {
                //INVALID!!
                return false;
            }
            AllObjectsList.Add(bioworldinfo);

            start += 4;
            uint shouldbezero = BitConverter.ToUInt32(data, start);
            if (shouldbezero != 0 && levelToRead.FileRef.Game != MEGame.ME1)
            {
                //INVALID!!!
                return false;
            }
            int itemcount = 1; //Skip bioworldinfo and Class
            if (levelToRead.FileRef.Game != MEGame.ME1)
            {
                start += 4;
                itemcount = 2;
            }
            List<ExportEntry> bulkActiveNodes = new List<ExportEntry>();
            //bool hasPathNode = false;
            //bool hasActorNode = false;
            //bool hasSplineNode = false;
            //bool hasEverythingElseNode = false;
            //todo: figure out a way to activate a layer if file is loading and the current views don't show anything to avoid modal dialog "nothing in this file".
            //seems like it would require two passes unless each level object type was put into a specific list and then the lists were appeneded to form the final list.
            //That would ruin ordering of exports, but does that really matter?

            while (itemcount < numberofitems)
            {
                //get header.
                int itemexportid = BitConverter.ToInt32(data, start);
                if (levelToRead.FileRef.isUExport(itemexportid))
                {
                    ExportEntry exportEntry = levelToRead.FileRef.getUExport(itemexportid);
                    AllObjectsList.Add(exportEntry);

                    if (ignoredobjectnames.Contains(exportEntry.ObjectName))
                    {
                        start += 4;
                        itemcount++;
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

                    if (splineNodeClasses.Contains(exportEntry.ClassName))
                    {
                        isParsedByExistingLayer = true;

                        if (ShowSplinesLayer && isAllowedVisibleByZFiltering(exportEntry))
                        {
                            bulkActiveNodes.Add(exportEntry);
                            var connectionsProp = exportEntry.GetProperty<ArrayProperty<StructProperty>>("Connections");
                            if (connectionsProp != null)
                            {
                                foreach (StructProperty connectionProp in connectionsProp)
                                {
                                    ObjectProperty splinecomponentprop = connectionProp.GetProp<ObjectProperty>("SplineComponent");
                                    bulkActiveNodes.Add(levelToRead.FileRef.getUExport(splinecomponentprop.Value));
                                }
                            }
                        }
                    }

                    //Don't parse SMCA or combat zones from overlays.
                    if (overlayPersistentLevel == null)
                    {
                        if (exportEntry.ClassName == "StaticMeshCollectionActor" || exportEntry.ClassName == "StaticLightCollectionActor")
                        {
                            StaticMeshCollections.Add(new StaticMeshCollection(exportEntry));
                        }
                        else if (exportEntry.ClassName == "SFXCombatZone" || exportEntry.ClassName == "BioPlaypenVolumeAdditive")
                        {
                            CombatZones.Add(new Zone(exportEntry));
                        }
                    }

                    if (ShowEverythingElseLayer && !isParsedByExistingLayer && isAllowedVisibleByZFiltering(exportEntry))
                    {
                        bulkActiveNodes.Add(exportEntry);
                    }

                    start += 4;
                    itemcount++;
                }
                else
                {
                    //INVALID ITEM ENCOUNTERED!
                    start += 4;
                    itemcount++;
                }
            }

            ActiveObjectsList.ReplaceAll(bulkActiveNodes);

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

        private bool isAllowedVisibleByZFiltering(ExportEntry exportEntry)
        {
            if (ZFilteringMode == EZFilterIncludeDirection.None) { return true; }
            Point3D position = SharedPathfinding.GetLocation(exportEntry);
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
            PointF centerpoint = new PointF((float)(fullx / GraphNodes.Count), (float)(fully / GraphNodes.Count));

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

                    if (export.ClassName == "SeqEvent_Touch"  ||export.ClassName == "SFXSeqEvt_Touch" || export.ClassName.StartsWith("SeqVar") || export.ClassName.StartsWith("SFXSeq"))
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
                            string shortpath = x.GetFullPath;
                            if (shortpath.StartsWith("TheWorld.PersistentLevel."))
                            {
                                shortpath = shortpath.Substring("TheWorld.PersistentLevel.".Length);
                            }
                            pnm.comment.Text += $"\n  {x.UIndex} {shortpath}_{x.indexValue}";
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

        private void node_MouseDown(object sender, PInputEventArgs e)
        {
            PathfindingNodeMaster node = (PathfindingNodeMaster)sender;
            //int n = node.Index;

            if (e.Shift)
            {
                PathfindingEditorWPF_ReachSpecsPanel.SetDestinationNode(node.UIndex);
                return;
            }

            ChangingSelectionByGraphClick = true;

            ActiveNodes_ListBox.SelectedItem = node.export;
            if (node is SplinePoint0Node || node is SplinePoint1Node)
            {
                node.Select();
            }
            //CurrentlySelectedSplinePoint = null;
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                Debug.WriteLine("Opening right mouse menu");
                OpenContextMenu();
            }
            ChangingSelectionByGraphClick = false;

        }

        public PointF LoadObject(ExportEntry exportToLoad, bool isFromOverlay = false)
        {
            string s = exportToLoad.ObjectName;
            int uindex = exportToLoad.UIndex;
            int x = 0, y = 0, z = int.MinValue;
            var props = exportToLoad.GetProperties();
            Point3D position = SharedPathfinding.GetLocation(exportToLoad);
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
                        pathNode = new PathfindingNodes.SFXDoorMarker(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LargeMantleNode":
                        pathNode = new SFXNav_LargeMantleNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXDynamicPathNode":
                    case "BioPathPoint":
                        pathNode = new BioPathPoint(uindex, x, y, exportToLoad.FileRef, graphEditor);
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
                    case "BlockingVolume":
                        actorNode = new BlockingVolume(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_BlockingVolumes);
                        break;
                    case "BioPlaypenVolumeAdditive":
                        actorNode = new BioPlaypenVolumeAdditive(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "DynamicBlockingVolume":
                        actorNode = new DynamicBlockingVolume(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_DynamicBlockingVolumes);
                        break;
                    case "DynamicTriggerVolume":
                        actorNode = new DynamicTriggerVolume(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "InterpActor":
                        actorNode = new InterpActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "BioTriggerVolume":
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
                    case "SFXBlockingVolume_Ledge":
                        actorNode = new SFXBlockingVolume_Ledge(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXCombatZone":
                        actorNode = new SFXCombatZone(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_SFXCombatZones);
                        break;
                    case "BioStartLocation":
                    case "BioStartLocationMP":
                        actorNode = new BioStartLocation(uindex, x, y, exportToLoad.FileRef, graphEditor, showRotation: true);
                        break;
                    case "StaticMeshActor":
                        actorNode = new StaticMeshActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXStuntActor":
                        actorNode = new SFXStuntActor(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "BioPawn":
                        actorNode = new BioPawn(uindex, x, y, exportToLoad.FileRef, graphEditor, showRotation: true);
                        break;
                    case "SkeletalMeshActor":
                        actorNode = new SkeletalMeshActor(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXPlaceable_Generator":
                    case "SFXPlaceable_ShieldGenerator":
                        actorNode = new SFXPlaceable(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "WwiseAmbientSound":
                        actorNode = new WwiseAmbientSound(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "WwiseAudioVolume":
                        actorNode = new WwiseAudioVolume(uindex, x, y, exportToLoad.FileRef, graphEditor, ShowVolumes_WwiseAudioVolumes);
                        break;
                    case "SFXArmorNode":
                    case "SFXTreasureNode":
                        actorNode = new SFXTreasureNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "SFXMedStation":
                        actorNode = new SFXMedStation(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                    case "TargetPoint":
                        actorNode = new TargetPoint(uindex, x, y, exportToLoad.FileRef, graphEditor, true);
                        break;
                    case "SFXOperation_ObjectiveSpawnPoint":
                        actorNode = new SFXObjectiveSpawnPoint(uindex, x, y, exportToLoad.FileRef, graphEditor);

                        //Create annex node if required
                        if (props.GetProp<ObjectProperty>("AnnexZoneLocation") is ObjectProperty annexZoneLocProp)
                        {
                            if (exportToLoad.FileRef.isUExport(annexZoneLocProp.Value))
                            {
                                ExportEntry targetPoint = exportToLoad.FileRef.getUExport(annexZoneLocProp.Value);
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
                            if (exportToLoad.FileRef.isUExport(combatZoneProp.Value))
                            {
                                ExportEntry combatZoneExp = exportToLoad.FileRef.getUExport(combatZoneProp.Value);
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

            if (splineNodeClasses.Contains(exportToLoad.ClassName))
            {
                SplineNode splineNode;
                switch (exportToLoad.ClassName)
                {
                    case "SplineActor":
                        splineNode = new SplineActorNode(uindex, x, y, exportToLoad.FileRef, graphEditor);

                        var connectionsProp = exportToLoad.GetProperty<ArrayProperty<StructProperty>>("Connections");
                        if (connectionsProp != null)
                        {
                            foreach (StructProperty connectionProp in connectionsProp)
                            {
                                ObjectProperty splinecomponentprop = connectionProp.GetProp<ObjectProperty>("SplineComponent");
                                ExportEntry splineComponentExport = Pcc.getUExport(splinecomponentprop.Value);
                                //Debug.WriteLine(splineComponentExport.GetFullPath + " " + splinecomponentprop.Value);
                                StructProperty splineInfo = splineComponentExport.GetProperty<StructProperty>("SplineInfo");
                                if (splineInfo != null)
                                {
                                    var pointsProp = splineInfo.GetProp<ArrayProperty<StructProperty>>("Points");
                                    var point1 = pointsProp[0].GetProp<StructProperty>("OutVal");
                                    double xf = point1.GetProp<FloatProperty>("X");
                                    double yf = point1.GetProp<FloatProperty>("Y");
                                    //double zf = point1.GetProp<FloatProperty>("Z");
                                    //Point3D point1_3d = new Point3D(xf, yf, zf);
                                    SplinePoint0Node point0node = new SplinePoint0Node(splinecomponentprop.Value, Convert.ToInt32(xf), Convert.ToInt32(yf), exportToLoad.FileRef, graphEditor);
                                    StructProperty point2 = pointsProp[1].GetProp<StructProperty>("OutVal");
                                    xf = point2.GetProp<FloatProperty>("X");
                                    yf = point2.GetProp<FloatProperty>("Y");
                                    //zf = point2.GetProp<FloatProperty>("Z");
                                    //Point3D point2_3d = new Point3D(xf, yf, zf);
                                    SplinePoint1Node point1node = new SplinePoint1Node(splinecomponentprop.Value, Convert.ToInt32(xf), Convert.ToInt32(yf), exportToLoad.FileRef, graphEditor);
                                    point0node.SetDestinationPoint(point1node);

                                    GraphNodes.Add(point0node);
                                    GraphNodes.Add(point1node);

                                    var reparamProp = splineComponentExport.GetProperty<StructProperty>("SplineReparamTable");
                                    var reparamPoints = reparamProp.GetProp<ArrayProperty<StructProperty>>("Points");
                                }
                            }
                        }
                        break;
                    default:
                        splineNode = new PendingSplineNode(uindex, x, y, exportToLoad.FileRef, graphEditor);
                        break;
                }

                splineNode.IsOverlay = isFromOverlay;
                GraphNodes.Add(splineNode);
                return new PointF(x, y);
            }

            //everything else
            GraphNodes.Add(new EverythingElseNode(uindex, x, y, exportToLoad.FileRef, graphEditor));
            return new PointF(x, y);
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

        public void CreateConnections()
        {
            if (GraphNodes != null && GraphNodes.Count != 0)
            {
                foreach (PathfindingNodeMaster node in GraphNodes)
                {
                    graphEditor.addNode(node);
                }
                foreach (PathfindingNodeMaster node in graphEditor.nodeLayer)
                {
                    node.CreateConnections(GraphNodes);
                }

                foreach (PPath edge in graphEditor.edgeLayer)
                {
                    PathingGraphEditor.UpdateEdgeStraight(edge as PathfindingEditorEdge);
                }
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);
            bool exportsAdded = changes.Contains(PackageChange.ExportAdd);

            var activeNode = ActiveNodes_ListBox.SelectedItem as ExportEntry;
            //we might need to identify parent depths and add those first
            List<PackageUpdate> addedChanges = updates.Where(x => x.change == PackageChange.ExportAdd || x.change == PackageChange.ImportAdd).OrderBy(x => x.index).ToList();
            List<int> headerChanges = updates.Where(x => x.change == PackageChange.ExportHeader).Select(x => x.index).OrderBy(x => x).ToList();
            if (exportsAdded || exportNonDataChanges) //may optimize by checking if chagnes include anything we care about
            {
                //Do a full refresh
                ExportEntry selectedExport = ActiveNodes_ListBox.SelectedItem as ExportEntry;
                RefreshGraph();
                ActiveNodes_ListBox.SelectedItem = selectedExport;
                return;
            }

            var loadedincices = ActiveNodes.Select(x => x.Index).ToList(); //Package updates are 0 based
            var nodesToUpdate = updates.Where(x => x.change == PackageChange.ExportData && loadedincices.Contains(x.index)).Select(x => x.index).ToList();

            if (nodesToUpdate.Count > 0)
            {
                foreach (var node in ActiveNodes)
                {
                    if (nodesToUpdate.Contains(node.Index))
                    {
                        //Reposition the node
                        var newlocation = SharedPathfinding.GetLocation(node);
                        PathfindingNodeMaster s = GraphNodes.First(o => o.UIndex == node.UIndex);
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

        #region Bindings
        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, $"{CurrentFile} {value}");
        }

        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get => _isBusyTaskbar;
            set => SetProperty(ref _isBusyTaskbar, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion
        #endregion

        #region Recents
        private readonly List<Button> RecentButtons = new List<Button>();
        public List<string> RFiles;
        private bool IsCombatZonesSingleSelecting;
        private bool IsReadingLevel;
        public static readonly string PathfindingEditorDataFolder = Path.Combine(App.AppDataFolder, @"PathfindingEditor\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";

        private void LoadRecentList()
        {
            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = PathfindingEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(PathfindingEditorDataFolder))
            {
                Directory.CreateDirectory(PathfindingEditorDataFolder);
            }
            string path = PathfindingEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of PathEd
                foreach (var form in Application.Current.Windows)
                {
                    if (form is PathfindingEditorWPF wpf && this != wpf)
                    {
                        wpf.RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }
            Recents_MenuItem.IsEnabled = true;

            int i = 0;
            foreach ((string filepath, Button recentButton) in RFiles.ZipTuple(RecentButtons))
            {
                MenuItem fr = new MenuItem
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                recentButton.Visibility = Visibility.Visible;
                recentButton.Content = Path.GetFileName(filepath.Replace("_", "__"));
                recentButton.Click -= RecentFile_click;
                recentButton.Click += RecentFile_click;
                recentButton.Tag = filepath;
                recentButton.ToolTip = filepath;
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
                i++;
            }
            while (i < 10)
            {
                RecentButtons[i].Visibility = Visibility.Collapsed;
                i++;
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((FrameworkElement)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
            }
            else
            {
                MessageBox.Show($"File does not exist: {s}");
            }
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            Recents_MenuItem.IsEnabled = true;
        }

        #endregion

        private void PathfinderEditorWPF_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                graphEditor.RemoveInputEventListener(pathfindingMouseListener);
                graphEditor.DragDrop -= GraphEditor_DragDrop;
                graphEditor.DragEnter -= GraphEditor_DragEnter;
#if DEBUG
                graphEditor.DebugEventHandlers();
#endif
                graphEditor.Dispose();
                GraphHost.Child = null; //This seems to be required to clear OnChildGotFocus handler from WinFormsHost
                GraphHost.Dispose();
                ActiveNodes.ClearEx();
                CurrentNodeSequenceReferences.ClearEx();
                StaticMeshCollections.ClearEx();
                CombatZones.ClearEx();
                if (GraphNodes != null)
                {
                    foreach (var node in GraphNodes)
                    {
                        node.MouseDown -= node_MouseDown;
                    }
                }

                GraphNodes?.Clear();
                graphEditor.edgeLayer.RemoveAllChildren();
                graphEditor.nodeLayer.RemoveAllChildren();
                Properties_InterpreterWPF.Dispose();
                PathfindingEditorWPF_ReachSpecsPanel.Dispose();
                zoomController.Dispose();
#if DEBUG
                graphEditor.DebugEventHandlers();
#endif
            }
        }

        private void ActiveNodesList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
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

                NodeName = $"{export.ObjectName}_{export.indexValue}";
                NodeNameSubText = $"Export {export.UIndex}";
                ActiveNodes_ListBox.ScrollIntoView(export);
                Properties_InterpreterWPF.LoadExport(export);
                CurrentNodeCombatZones.ClearEx();

                PathfindingNodeMaster selectedNode = GraphNodes.First(o => o.UIndex == export.UIndex);
                CurrentNodeSequenceReferences.ReplaceAll(selectedNode.SequenceReferences);
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
                    PathfindingNodeTabControl.SelectedItem = ValidationPanel_Tab;
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

                    Point3D position = SharedPathfinding.GetLocation(export);
                    if (position != null)
                    {
                        NodePositionX_TextBox.Text = position.X.ToString();
                        NodePositionY_TextBox.Text = position.Y.ToString();
                        NodePositionZ_TextBox.Text = position.Z.ToString();
                    }
                    else
                    {
                        //Todo: Looking via SMAC
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
                            if (sp != null)
                            {
                                ObjectProperty op = sp.GetProp<ObjectProperty>("Link");
                                if (op != null && op.Value - 1 < Pcc.ExportCount)
                                {
                                    HighlightCoverlinkSlots(Pcc.getUExport(op.Value));
                                }
                            }
                            break;
                        case "BioWaypointSet":
                            HighlightBioWaypointSet(selectedNode.export);
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
                Properties_InterpreterWPF.UnloadExport();
                PathfindingEditorWPF_ReachSpecsPanel.UnloadExport();
                NodeName = "No node selected";
                NodeNameSubText = "N/A";
                NodePosition_Panel.IsEnabled = false;
            }
            OnPropertyChanged(nameof(NodeTypeDescriptionText));
        }

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

            ArrayProperty<StructProperty> props = coverlink.GetProperty<ArrayProperty<StructProperty>>("Slots");
            if (props != null)
            {
                CurrentlyHighlightedCoverlinkNodes = new List<int>();
                CurrentlyHighlightedCoverlinkNodes.Add(coverlink.UIndex);

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
                    } else if (pnm.export.ClassName == "CoverLink" || pnm.export.ClassName == "CoverSlotMarker")
                    {
                        pnm.shape.Brush = PathfindingNodeMaster.pathfindingNodeBrush;
                    }
                }
            }
        }

        private List<Zone> CloneCombatZonesForSelections()
        {
            var clones = new List<Zone>();
            foreach (Zone z in CombatZones)
            {
                clones.Add(new Zone(z));
            }

            return clones;
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
            if (int.TryParse(FindByNumber_TextBox.Text, out int result) && Pcc.isUExport(result))
            {
                var export = Pcc.getUExport(result);
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

        private void PositionBoxes_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && ActiveNodes_ListBox.SelectedItem is ExportEntry export &&
                float.TryParse(NodePositionX_TextBox.Text, out float x) && float.TryParse(NodePositionY_TextBox.Text, out float y) && float.TryParse(NodePositionZ_TextBox.Text, out float z))
            {
                SharedPathfinding.SetLocation(export, x, y, z);
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

        public void UpdateEdgesForCurrentNode(PathfindingNodeMaster node = null)
        {
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
                var newReachSpecs = SharedPathfinding.GetReachspecExports(pn.export);
                if (existingSpecs.Count > newReachSpecs.Count)
                {
                    //We have deleted at least one outbound spec.
                    //we need to either turn the link dashed or remove it (don't save in newOneWayEdge list)
                    var removedSpecs = existingSpecs.Except(newReachSpecs);
                    var endpointsToCheck = removedSpecs.Select(x => SharedPathfinding.GetReachSpecEndExport(x)).ToList();
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




        private void SetGraphXY_Clicked(object sender, RoutedEventArgs e)
        {
            //Find node
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry export)
            {
                PathfindingNodeMaster s = GraphNodes.First(o => o.UIndex == export.UIndex);
                var currentlocation = SharedPathfinding.GetLocation(export);
                SharedPathfinding.SetLocation(export, s.GlobalBounds.X, s.GlobalBounds.Y, (float)currentlocation.Z);
                MessageBox.Show($"Location set to {s.GlobalBounds.X}, { s.GlobalBounds.Y}");
            }
            else
            {
                MessageBox.Show("No location property on this export.");

            }
            //Need to update
        }

        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry export)
            {
                AllowWindowRefocus = false; //prevents flicker effect when windows try to focus and then package editor activates
                PackageEditorWPF p = new PackageEditorWPF();
                p.Show();
                p.LoadFile(export.FileRef.FilePath, export.UIndex);
                p.Activate(); //bring to front
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

        private ExportEntry cloneNode(ExportEntry nodeEntry)
        {
            if (nodeEntry != null)
            {
                ExportEntry subComponentEntry = null;
                if (nodeEntry.GetProperty<ObjectProperty>("CollisionComponent") is ObjectProperty collisionComponentProperty)
                {
                    subComponentEntry = nodeEntry.FileRef.getUExport(collisionComponentProperty.Value);
                }
                else if (nodeEntry.GetProperty<ObjectProperty>("ParticleSystemComponent") is ObjectProperty psComponentProperty)
                {
                    subComponentEntry = nodeEntry.FileRef.getUExport(psComponentProperty.Value);
                }

                if (subComponentEntry != null)
                {
                    ExportEntry newNodeEntry = nodeEntry.Clone();
                    nodeEntry.FileRef.addExport(newNodeEntry);
                    ExportEntry newSubcomponent = subComponentEntry.Clone();
                    nodeEntry.FileRef.addExport(newSubcomponent);
                    newSubcomponent.idxLink = newNodeEntry.UIndex;

                    //Update the cloned nodes to be new items
                    bool changed = false;

                    //empty the pathlist
                    PropertyCollection newExportProps = newNodeEntry.GetProperties();

                    var PathList = newExportProps.GetProp<ArrayProperty<ObjectProperty>>("PathList");
                    if (PathList != null && PathList.Count > 0)
                    {
                        changed = true;
                        PathList.Clear();
                    }

                    foreach (UProperty prop in newExportProps)
                    {
                        if (prop is ObjectProperty objProp)
                        {
                            if (objProp.Value == subComponentEntry.UIndex)
                            {
                                objProp.Value = newSubcomponent.UIndex;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        newNodeEntry.WriteProperties(newExportProps);
                    }

                    var oldloc = SharedPathfinding.GetLocation(newNodeEntry);
                    SharedPathfinding.SetLocation(newNodeEntry, (float)oldloc.X + 50, (float)oldloc.Y + 50, (float)oldloc.Z);

                    SharedPathfinding.GenerateNewRandomGUID(newNodeEntry);
                    //Add cloned node to persistentlevel
                    var level = Unreal.BinaryConverters.ObjectBinary.From<Unreal.BinaryConverters.Level>(PersistentLevelExport);
                    level.Actors.Add(newNodeEntry.UIndex);
                    PersistentLevelExport.Data = level.ToBytes(Pcc);

                    SharedPathfinding.ReindexMatchingObjects(newNodeEntry);
                    SharedPathfinding.ReindexMatchingObjects(newSubcomponent);
                    return newNodeEntry;
                }

                MessageBox.Show("Can't clone this type type yet.");
            }
            return null;
        }

        [DebuggerDisplay("{export.UIndex} Static Mesh Collection Actor")]
        public class StaticMeshCollection : NotifyPropertyChangedBase
        {
            private bool _active;
            public bool Active { get => _active; set => SetProperty(ref _active, value); }

            public List<ExportEntry> CollectionItems = new List<ExportEntry>();
            public ExportEntry export { get; }
            public string DisplayString => $"{export.UIndex}\t{CollectionItems.Count} items";

            public StaticMeshCollection(ExportEntry smac)
            {
                export = smac;
                var smacItems = smac.GetProperty<ArrayProperty<ObjectProperty>>(export.ClassName == "StaticMeshCollectionActor" ? "StaticMeshComponents" : "LightComponents");
                if (smacItems != null)
                {
                    //Read exports...
                    foreach (ObjectProperty obj in smacItems)
                    {
                        if (obj.Value > 0)
                        {
                            ExportEntry item = smac.FileRef.getUExport(obj.Value);
                            CollectionItems.Add(item);
                        }
                        else
                        {
                            //this is a blank entry, or an import, somehow.
                            CollectionItems.Add(null);
                        }
                    }
                }
            }

            /// <summary>
            /// Retreives a list of position data, in order, of all items. Null items return a point at double.min, double.min
            /// </summary>
            /// <returns></returns>
            public List<Point> GetLocationData()
            {
                byte[] smacData = export.Data;
                int binarypos = export.propsEnd();
                var positions = new List<Point>();
                foreach (var item in CollectionItems)
                {
                    if (item != null)
                    {
                        //Read location and put in position map
                        int offset = binarypos + 12 * 4;
                        float x = BitConverter.ToSingle(smacData, offset);
                        float y = BitConverter.ToSingle(smacData, offset + 4);
                        //Debug.WriteLine(offset.ToString("X4") + " " + x + "," + y);
                        positions.Add(new Point(x, y));
                    }
                    else
                    {
                        positions.Add(new Point(double.MinValue, double.MinValue));
                    }
                    binarypos += 64;
                }
                return positions;
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
            public string DisplayString => $"{export.UIndex} {export.ObjectName}_{export.indexValue}";

            public Zone(ExportEntry combatZone)
            {
                this.export = combatZone;
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

        private void StaticMeshCollectionActors_ItemSelectionChanged(object sender, Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
        {
            if (IsReadingLevel) return;
            StaticMeshCollection smc = (StaticMeshCollection)e.Item;
            if (e.IsSelected)
            {
                List<Point> locations = smc.GetLocationData();
                for (int i = 0; i < smc.CollectionItems.Count; i++)
                {
                    var item = smc.CollectionItems[i];
                    var location = locations[i];

                    if (item != null)
                    {
                        SMAC_ActorNode smac = new SMAC_ActorNode(item.UIndex, (int)location.X, (int)location.Y, Pcc, graphEditor);
                        ActiveNodes.Add(item);
                        GraphNodes.Add(smac);
                        smac.MouseDown += node_MouseDown;
                        graphEditor.addNode(smac);
                    }
                }
            }
            else
            {
                var activeNodesToRemove = ActiveNodes.Where(x => !smc.CollectionItems.Contains(x));
                ActiveNodes.ReplaceAll(activeNodesToRemove);

                var graphNodesToRemove = GraphNodes.Where(x => smc.CollectionItems.Contains(x.export)).ToList();
                GraphNodes = GraphNodes.Except(graphNodesToRemove).ToList();
                graphEditor.nodeLayer.RemoveChildren(graphNodesToRemove);
            }
            graphEditor.Refresh();
        }

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
                var currentlocation = SharedPathfinding.GetLocation(export);
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

        private void CurrentCombatZones_SelectionChanged(object sender,
            Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
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
                    PropertyCollection actorRefProps = new PropertyCollection();

                    //Get GUID
                    var guid = itemChanging.export.GetProperty<StructProperty>("CombatZoneGuid");
                    guid.Name = "Guid";
                    actorRefProps.AddOrReplaceProp(guid);
                    actorRefProps.AddOrReplaceProp(new ObjectProperty(itemChanging.export.UIndex, "Actor"));
                    StructProperty actorReference = new StructProperty("ActorReference", actorRefProps, isImmutable: true);
                    volumesList.Add(actorReference);

                    PathfindingNode s = GraphNodes.FirstOrDefault(o => o.UIndex == nodeExport.UIndex) as PathfindingNode;
                    s?.Volumes.Add(new Volume(actorReference));
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
                            foreach (Volume v in s.Volumes)
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
                if (ext == ".upk" || ext == ".pcc" || ext == ".sfm")
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
                if (ext == ".upk" || ext == ".pcc" || ext == ".sfm")
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

        public ObservableCollectionExtended<ExportEntry> CurrentNodeSequenceReferences { get; } = new ObservableCollectionExtended<ExportEntry>();
        public ObservableCollectionExtended<NodeType> AvailableNodeChangeableTypes { get; } = new ObservableCollectionExtended<NodeType>();
        public List<int> CurrentlyHighlightedCoverlinkNodes { get; private set; }

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

        public class PathingZoomController : IDisposable
        {
            public static float MIN_SCALE = .005f;
            public static float MAX_SCALE = 15;
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
            private PathfindingEditorWPF pathfinderWPF;

            public PathfindingMouseListener(PathfindingEditorWPF pathfinderWPF)
            {
                this.pathfinderWPF = pathfinderWPF;
            }

            public void Dispose()
            {
                pathfinderWPF = null;
            }

            public override void OnMouseMove(object sender, PInputEventArgs e)
            {
                PointF pos = e.Position;
                int X = Convert.ToInt32(pos.X);
                int Y = Convert.ToInt32(pos.Y);
                pathfinderWPF.StatusText = $"[{X},{Y}]";
            }
        }

        private void RemoveFromLevel_Clicked(object sender, RoutedEventArgs e)
        {
            AllowRefresh = false;
            ExportEntry nodeEntry = (ExportEntry)ActiveNodes_ListBox.SelectedItem;

            //Read persistent level binary
            byte[] data = PersistentLevelExport.Data;

            //find start of class binary (end of props)
            int start = PersistentLevelExport.propsEnd();

            start += 4; //skip export id
            uint numberofitems = BitConverter.ToUInt32(data, start);
            int countoffset = start;

            start += 12; //skip bioworldinfo, 0;

            int itemcount = 2; //Skip bioworldinfo and class (0) objects

            //if the node we are removing is a pathfinding node, I am not sure if we are supposed to remove inbound reachspecs to it.
            //This may be something to test to see how game handles reachspecs to nodes not in the level
            while (itemcount < numberofitems)
            {
                //get header.
                uint itemexportid = BitConverter.ToUInt32(data, start);
                if (itemexportid == nodeEntry.UIndex)
                {
                    SharedPathfinding.WriteMem(data, countoffset, BitConverter.GetBytes(numberofitems - 1));
                    byte[] destarray = new byte[data.Length - 4];
                    Buffer.BlockCopy(data, 0, destarray, 0, start);
                    Buffer.BlockCopy(data, start + 4, destarray, start, data.Length - (start + 4));
                    //Debug.WriteLine(data.Length);
                    //Debug.WriteLine("DA " + destarray.Length);
                    PersistentLevelExport.Data = destarray;
                    AllowRefresh = true;
                    RefreshGraph();
                    MessageBox.Show("Removed item from level.");
                    return;
                }
                itemcount++;
                start += 4;
            }
        }

        private void PathfindingEditorWPF_Loaded(object sender, RoutedEventArgs e)
        {
            if (FileQueuedForLoad != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    LoadFile(FileQueuedForLoad);
                    FileQueuedForLoad = null;
                    Activate();
                }));
            }
        }

        private void RegenerateGUID_Clicked(object sender, RoutedEventArgs e)
        {
            if (ActiveNodes_ListBox.SelectedItem is ExportEntry nodeEntry)
            {
                SharedPathfinding.GenerateNewRandomGUID(nodeEntry);
            }
        }

        private void CheckGameFileNavs_Clicked(object sender, RoutedEventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                StatusText = "Getting file information from game directory";
                FileInfo[] files = new DirectoryInfo(ME3Directory.BIOGamePath).EnumerateFiles("*.pcc", SearchOption.AllDirectories).ToArray();
                int numScanned = 1;
                SortedSet<string> navsNotAccountedFor = new SortedSet<string>();
                foreach (var file in files)
                {
                    //GC.Collect();
                    using (var package = MEPackageHandler.OpenMEPackage(file.FullName))
                    {
                        var persistenLevelExp = package.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
                        if (persistenLevelExp == null) continue;
                        //Scan
                        byte[] data = persistenLevelExp.Data;

                        //find start of class binary (end of props)
                        int start = persistenLevelExp.propsEnd();

                        //Console.WriteLine("Found start of binary at " + start.ToString("X8"));

                        uint exportid = BitConverter.ToUInt32(data, start);
                        start += 4;
                        uint numberofitems = BitConverter.ToUInt32(data, start);
                        int countoffset = start;

                        start += 4;
                        int bioworldinfoexportid = BitConverter.ToInt32(data, start);
                        ExportEntry bioworldinfo = package.getUExport(bioworldinfoexportid);
                        if (bioworldinfo.ObjectName != "BioWorldInfo")
                        {
                            //INVALID!!
                            Debug.WriteLine("Error: BioworldInfo object is not bioworldinfo name");
                            continue;
                        }

                        start += 4;
                        uint shouldbezero = BitConverter.ToUInt32(data, start);
                        if (shouldbezero != 0 && package.Game != MEGame.ME1)
                        {
                            //INVALID!!!
                            Debug.WriteLine("Error: should be zero, not zero in " + package.FilePath);
                            continue;
                        }
                        int itemcount = 1; //Skip bioworldinfo and Class
                        if (package.Game != MEGame.ME1)
                        {
                            start += 4;
                            itemcount = 2;
                        }
                        while (itemcount < numberofitems)
                        {
                            //get header.
                            int itemexportid = BitConverter.ToInt32(data, start);
                            if (package.isUExport(itemexportid))
                            {
                                ExportEntry exportEntry = package.getUExport(itemexportid);
                                //AllLevelObjects.Add(exportEntry);

                                if (ignoredobjectnames.Contains(exportEntry.ObjectName))
                                {
                                    start += 4;
                                    itemcount++;
                                    continue;
                                }

                                if (!pathfindingNodeClasses.Contains(exportEntry.ClassName))
                                {
                                    if (exportEntry.GetProperty<ArrayProperty<ObjectProperty>>("PathList") is ArrayProperty<ObjectProperty>)
                                    {
                                        if (!navsNotAccountedFor.TryGetValue(exportEntry.ClassName, out string _))
                                        {
                                            Debug.WriteLine("Found new nav type: " + exportEntry.ClassName + " in " + exportEntry.FileRef.FilePath);
                                            navsNotAccountedFor.Add(exportEntry.GetFullPath);
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

                                start += 4;
                                itemcount++;
                            }
                            else
                            {
                                //INVALID ITEM ENCOUNTERED!
                                start += 4;
                                itemcount++;
                            }
                        }

                        numScanned++;
                        StatusText = "Scanning files " + ((int)(numScanned * 100.0 / files.Length)) + "%";
                    }
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
    }
}