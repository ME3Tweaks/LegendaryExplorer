using ME3Explorer.ActorNodes;
using ME3Explorer.Packages;
using ME3Explorer.PathfindingNodes;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.SplineNodes;
using ME3Explorer.Unreal;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using static ME3Explorer.PathfindingNodes.PathfindingNode;

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
            "SFXNav_InteractionInspectWeapon", "SFXNav_InteractionOmniToolScan"
        };

        public static string[] actorNodeClasses =
        {
            "BlockingVolume", "BioPlaypenVolumeAdditive", "DynamicBlockingVolume", "DynamicTriggerVolume", "SFXMedkit",
            "StaticMeshActor", "SFXMedStation", "InterpActor", "SFXDoor", "BioTriggerVolume", "TargetPoint",
            "SFXArmorNode", "BioTriggerStream", "SFXTreasureNode", "SFXPointOfInterest", "SFXPlaceable_Generator",
            "SFXPlaceable_ShieldGenerator", "SFXBlockingVolume_Ledge", "SFXAmmoContainer_Simulator", "SFXAmmoContainer",
            "SFXGrenadeContainer", "SFXCombatZone", "BioStartLocation", "BioStartLocationMP", "SFXStuntActor",
            "SkeletalMeshActor", "WwiseAmbientSound", "WwiseAudioVolume", "SFXOperation_ObjectiveSpawnPoint"
        };

        public static string[] splineNodeClasses = { "SplineActor" };

        public static string[]
            ignoredobjectnames =
            {
                "PREFAB_Ladders_3M_Arc0", "PREFAB_Ladders_3M_Arc1"
            }; //These come up as parsed classes but aren't actually part of the level, only prefabs. They should be ignored

        //Layers
        private List<PathfindingNodeMaster> GraphNodes;
        private bool ChangingSelectionByGraphClick;
        private IExportEntry PersisentLevelExport;

        private PathingGraphEditor graphEditor;
        private bool AllowRefresh;
        private PathingZoomController zoomController;

        private string FileQueuedForLoad;

        public ObservableCollectionExtended<IExportEntry> ActiveNodes { get; set; } =
            new ObservableCollectionExtended<IExportEntry>();

        public ObservableCollectionExtended<string> TagsList { get; set; } = new ObservableCollectionExtended<string>();

        public ObservableCollectionExtended<StaticMeshCollection> StaticMeshCollections { get; set; } =
            new ObservableCollectionExtended<StaticMeshCollection>();

        public ObservableCollectionExtended<Zone> CombatZones { get; } = new ObservableCollectionExtended<Zone>();

        public ObservableCollectionExtended<Zone> CurrentNodeCombatZones { get; } =
            new ObservableCollectionExtended<Zone>();

        private List<IExportEntry> AllLevelObjects = new List<IExportEntry>();
        public string CurrentFile;
        private PathfindingMouseListener pathfindingMouseListener;

        private string _currentNodeXY = "Undefined";

        public string CurrentNodeXY
        {
            get => _currentNodeXY;
            set => SetProperty(ref _currentNodeXY, value);
        }

        public PathfindingEditorWPF()
        {
            Initialize();
        }

        public PathfindingEditorWPF(string fileName)
        {
            FileQueuedForLoad = fileName;
            Initialize();
        }

        private void Initialize()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Pathfinding Editor WPF", new WeakReference(this));
            DataContext = this;
            StatusText = "Select package file to load";
            LoadCommands();
            InitializeComponent();
            ContextMenu contextMenu = this.FindResource("nodeContextMenu") as ContextMenu;
            contextMenu.DataContext = this;
            graphEditor = (PathingGraphEditor)GraphHost.Child;
            graphEditor.BackColor = System.Drawing.Color.FromArgb(130, 130, 130);
            AllowRefresh = true;
            LoadRecentList();
            RefreshRecent(false);
            zoomController = new PathingZoomController(graphEditor);
            SharedPathfinding.LoadClassesDB();
            var types = SharedPathfinding.ExportClassDB.Where(x => x.pathnode).ToList();
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
        private void LoadCommands()
        {
            RefreshCommand = new RelayCommand(RefreshGraph, PackageIsLoaded);
            FocusGotoCommand = new RelayCommand(FocusGoto, PackageIsLoaded);
            FocusFindCommand = new RelayCommand(FocusFind, PackageIsLoaded);
            OpenCommand = new RelayCommand(OpenPackage, (o) => { return true; });
            SaveCommand = new RelayCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new RelayCommand(SavePackageAs, PackageIsLoaded);

            TogglePathfindingCommand = new RelayCommand(TogglePathfindingNodes, PackageIsLoaded);
            ToggleEverythingElseCommand = new RelayCommand(ToggleEverythingElse, PackageIsLoaded);
            ToggleActorsCommand = new RelayCommand(ToggleActors, PackageIsLoaded);
            ToggleSplinesCommand = new RelayCommand(ToggleSplines, PackageIsLoaded);
            ToggleSequenceReferencesCommand = new RelayCommand(ToggleSequenceReferences, PackageIsLoaded);
            ShowBioTriggerVolumesCommand = new RelayCommand(ShowBioTriggerVolumes, PackageIsLoaded);
            ShowBioTriggerStreamsCommand = new RelayCommand(ShowBioTriggerStreams, PackageIsLoaded);
            ShowBlockingVolumesCommand = new RelayCommand(ShowBlockingVolumes, PackageIsLoaded);
            ShowDynamicBlockingVolumesCommand = new RelayCommand(ShowDynamicBlockingVolumes, PackageIsLoaded);
            ShowSFXBlockingVolumeLedgesCommand = new RelayCommand(ShowSFXBlockingVolumeLedges, PackageIsLoaded);
            ShowSFXCombatZonesCommand = new RelayCommand(ShowSFXCombatZones, PackageIsLoaded);
            ShowWwiseAudioVolumesCommand = new RelayCommand(ShowWwiseAudioVolumes, PackageIsLoaded);

            FlipLevelCommand = new RelayCommand(FlipLevel, PackageIsLoaded);
            BuildPathfindingChainCommand = new RelayCommand(BuildPathfindingChainExperiment, PackageIsLoaded);

            ShowNodeSizesCommand = new RelayCommand(ToggleNodeSizesDisplay, (o) => { return true; });
            AddExportToLevelCommand = new RelayCommand(AddExportToLevel, PackageIsLoaded);

            PopoutInterpreterCommand = new RelayCommand(PopoutInterpreterWPF, NodeIsSelected);
            NodeTypeChangeCommand = new RelayCommand(ChangeNodeType, CanChangeNodetype);
            OpenRefInSequenceEditorCommand = new RelayCommand(OpenRefInSequenceEditor, NodeIsSelected);
        }

        private void OpenRefInSequenceEditor(object obj)
        {
            //Will change to sequence editor wpf on merge
            if (obj is IExportEntry exp)
            {
                SequenceEditor seqed = new SequenceEditor(exp);
                seqed.Show();
            }
        }

        private void ToggleSequenceReferences(object obj)
        {
            ShowSequenceReferences = !ShowSequenceReferences;
            RefreshGraph();
        }

        private void ChangeNodeType(object obj)
        {
            Debug.WriteLine("Changing node type");
            if (ActiveNodes_ListBox.SelectedItem is IExportEntry exp &&
                AvailableNodeTypes_ListBox.SelectedItem is NodeType type)
            {
                changeNodeType(exp, type);
            }
        }


        private bool CanChangeNodetype(object obj)
        {
            return true;
        }

        /// <summary>
        /// This method changes a node's type. It does many steps:
        /// It checks that the node type imports exists as well as it's collision cylinder and reach spec imports.
        /// It will scan all nodes for incoming reachspecs to this node and change them to the appropriate class. 
        /// It changes the collision cylinder archetype
        /// It changes the node class and object name
        /// </summary>
        /// <param name="nodeEntry"></param>
        /// <param name="newType"></param>
        private void changeNodeType(IExportEntry nodeEntry, NodeType newNodeType)
        {
            var exportTypeInfo = newNodeType.TypeInfo;
            PropertyCollection nodeProperties = nodeEntry.GetProperties();

            if (nodeEntry.ClassName == exportTypeInfo.nodetypename)
            {
                if (!exportTypeInfo.usesbtop || (nodeProperties.FirstOrDefault(x => x.Name == "bTopNode") is BoolProperty bTop
                        && bTop.Value == newNodeType.Top))
                {
                    return; //same, not changing.
                }
            }

            if (exportTypeInfo != null)
            {
                List<UProperty> ensuredProperties = new List<UProperty>();
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
                IExportEntry cylindercomponentexp = Pcc.getUExport(cylindercomponent.Value);

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
                    ArrayProperty<ObjectProperty> pathList = nodeProperties.GetProp<ArrayProperty<ObjectProperty>>("PathList");
                    if (pathList != null)
                    {
                        foreach (ObjectProperty pathObj in pathList)
                        {
                            IExportEntry spec = Pcc.getUExport(pathObj.Value);
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
                            IExportEntry inboundSpec = Pcc.getUExport(otherNodePathlist[on].Value);
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
        IExportEntry cylindercomponentexp = Pcc.getUExport(cylindercomponent.Value);

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
                            IExportEntry spec = Pcc.getUExport(pathList[i].Value);
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
                                IExportEntry specDest = Pcc.getUExport(othernodeidx);
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
                                            IExportEntry inboundSpec =
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

                    //    foreach (IExportEntry spec in ReachSpecs)
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
        private void EnsureLargeAndReturning(IExportEntry spec, PathfindingDB_ExportType exportTypeInfo, bool outbound = true)
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
                    IExportEntry specDest = Pcc.getUExport(othernodeidx);

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
                            ArrayProperty<ObjectProperty> pathList = specDest.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
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
            IExportEntry export = ActiveNodes_ListBox.SelectedItem as IExportEntry;
            ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new InterpreterWPF(), export);
            elhw.Title = $"Interpreter - {export.UIndex} {export.GetNetIndexedFullPath} - {Pcc.FileName}";
            elhw.Show();
        }

        private bool NodeIsSelected(object obj)
        {
            return ActiveNodes_ListBox.SelectedItem is IExportEntry;
        }

        private void AddExportToLevel(object obj)
        {
            using (EntrySelectorDialogWPF dialog = new EntrySelectorDialogWPF(this, Pcc, EntrySelectorDialogWPF.SupportedTypes.Exports))
            {
                if (dialog.ShowDialog().Value && dialog.ChosenEntry is IExportEntry selectedEntry)
                {

                    if (!AllLevelObjects.Contains(selectedEntry))
                    {
                        byte[] leveldata = PersisentLevelExport.Data;
                        int start = PersisentLevelExport.propsEnd();
                        //Console.WriteLine("Found start of binary at {start.ToString("X8"));

                        uint exportid = BitConverter.ToUInt32(leveldata, start);
                        start += 4;
                        uint numberofitems = BitConverter.ToUInt32(leveldata, start);
                        numberofitems++;
                        SharedPathfinding.WriteMem(leveldata, start, BitConverter.GetBytes(numberofitems));

                        //Debug.WriteLine("Size before: {memory.Length);
                        //memory = RemoveIndices(memory, offset, size);
                        int offset = (int)(start + numberofitems * 4); //will be at the very end of the list as it is now +1
                        List<byte> memList = leveldata.ToList();
                        memList.InsertRange(offset, BitConverter.GetBytes(selectedEntry.UIndex));
                        leveldata = memList.ToArray();
                        PersisentLevelExport.Data = leveldata;
                        RefreshGraph();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"{selectedEntry.UIndex} {selectedEntry.GetNetIndexedFullPath} is already in the level.");
                    }
                }
            }
        }

        private void ToggleNodeSizesDisplay(object obj)
        {
            ShowNodeSizes_MenuItem.IsChecked = !ShowNodeSizes_MenuItem.IsChecked;
            Properties.Settings.Default.PathfindingEditorShowNodeSizes = ShowNodeSizes_MenuItem.IsChecked;
            Properties.Settings.Default.Save();
            RefreshGraph();
        }

        private void BuildPathfindingChainExperiment(object obj)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Point Logger ASI file output (txt)|*txt";
            string pathfindingChainFile = null;
            var result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                pathfindingChainFile = d.FileName;


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
                IExportEntry firstNode = null;
                IExportEntry previousNode = null;


                foreach (var point in points)
                {
                    IExportEntry newNode = cloneNode(basePathNode);
                    StructProperty prop = newNode.GetProperty<StructProperty>("location");
                    if (prop != null)
                    {
                        PropertyCollection nodelocprops = (prop as StructProperty).Properties;
                        foreach (var locprop in nodelocprops)
                        {
                            switch (locprop.Name)
                            {
                                case "X":
                                    (locprop as FloatProperty).Value = (float)point.X;
                                    break;
                                case "Y":
                                    (locprop as FloatProperty).Value = (float)point.Y;
                                    break;
                                case "Z":
                                    (locprop as FloatProperty).Value = (float)point.Z;
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

        private void FlipLevel(object obj)
        {
            foreach (IExportEntry exp in Pcc.Exports)
            {
                switch (exp.ObjectName)
                {
                    case "StaticMeshCollectionActor":
                        {
                            //This is going to get ugly.

                            byte[] data = exp.Data;
                            //get a list of staticmesh stuff from the props.
                            int listsize = System.BitConverter.ToInt32(data, 28);
                            List<IExportEntry> smacitems = new List<IExportEntry>();
                            for (int i = 0; i < listsize; i++)
                            {
                                int offset = (32 + i * 4);
                                //fetch exports
                                int entryval = BitConverter.ToInt32(data, offset);
                                if (entryval > 0 && entryval < Pcc.ExportCount)
                                {
                                    IExportEntry export = (IExportEntry)Pcc.getEntry(entryval);
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
                                data = SharedPathfinding.WriteMem(data, start + smcaindex * 64 + (12 * 4), BitConverter.GetBytes(x * -1));
                                data = SharedPathfinding.WriteMem(data, start + smcaindex * 64 + (13 * 4), BitConverter.GetBytes(y * -1));
                                data = SharedPathfinding.WriteMem(data, start + smcaindex * 64 + (14 * 4), BitConverter.GetBytes(z * -1));

                                InvertScalingOnExport(smacitems[smcaindex], "Scale3D");
                                smcaindex++;
                                Debug.WriteLine(exp.Index + " " + smcaindex + " SMAC Flipping " + x + "," + y + "," + z);
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
                                Debug.WriteLine(exp.Index + " " + exp.ObjectName + "Flipping " + xProp.Value + "," + yProp.Value + "," + zProp.Value);

                                xProp.Value = xProp.Value * -1;
                                yProp.Value = yProp.Value * -1;
                                zProp.Value = zProp.Value * -1;

                                exp.WriteProperty(locationProp);
                                InvertScalingOnExport(exp, "DrawScale3D");
                            }
                            break;
                        }
                }
            }
            MessageBox.Show("Items flipped.", "Flipping complete");
        }

        private void InvertScalingOnExport(IExportEntry exp, string propname)
        {
            var drawScale3D = exp.GetProperty<StructProperty>(propname);
            bool hasDrawScale = drawScale3D != null;
            if (drawScale3D == null)
            {

                //What in god's name is this still doing here
                PropertyCollection threeDProps = new PropertyCollection();
                threeDProps.Add(new FloatProperty(0, "X"));
                threeDProps.Add(new FloatProperty(0, "Y"));
                threeDProps.Add(new FloatProperty(0, "Z"));
                drawScale3D = new StructProperty("Vector", threeDProps, "DrawScale3D", true);
                exp.WriteProperty(drawScale3D);
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

        private void ShowWwiseAudioVolumes(object obj)
        {
            var WwiseAudioVolumes = GraphNodes.Where(x => x is WwiseAudioVolume).Select(x => x as WwiseAudioVolume).ToList();
            WwiseAudioVolumes.ForEach(x => x.SetShape(ShowVolumes_WwiseAudioVolumes));
            graphEditor.Refresh();
        }

        private void ShowSFXCombatZones(object obj)
        {
            var CombatZones = GraphNodes.Where(x => x is SFXCombatZone).Select(x => x as SFXCombatZone).ToList();
            CombatZones.ForEach(x => x.SetShape(ShowVolumes_SFXCombatZones));
            graphEditor.Refresh();
        }

        private void ShowSFXBlockingVolumeLedges(object obj)
        {
            var Ledges = GraphNodes.Where(x => x is SFXBlockingVolume_Ledge).Select(x => x as SFXBlockingVolume_Ledge).ToList();
            Ledges.ForEach(x => x.SetShape(ShowVolumes_SFXBlockingVolume_Ledges));
            graphEditor.Refresh();
        }

        private void ShowDynamicBlockingVolumes(object obj)
        {
            var DynamicBlockingVolumes = GraphNodes.Where(x => x is DynamicBlockingVolume).Select(x => x as DynamicBlockingVolume).ToList();
            DynamicBlockingVolumes.ForEach(x => x.SetShape(ShowVolumes_DynamicBlockingVolumes));
            graphEditor.Refresh();
        }

        private void ShowBlockingVolumes(object obj)
        {
            var BlockingVolumes = GraphNodes.Where(x => x is BlockingVolume).Select(x => x as BlockingVolume).ToList();
            BlockingVolumes.ForEach(x => x.SetShape(ShowVolumes_BlockingVolumes));
            graphEditor.Refresh();
        }

        private void ShowBioTriggerVolumes(object obj)
        {
            var BioTriggerVolumes = GraphNodes.Where(x => x is BioTriggerVolume).Select(x => x as BioTriggerVolume).ToList();
            BioTriggerVolumes.ForEach(x => x.SetShape(ShowVolumes_BioTriggerVolumes));
            graphEditor.Refresh();
        }

        private void ShowBioTriggerStreams(object obj)
        {
            var BioTriggerStreams = GraphNodes.Where(x => x is BioTriggerStream).Select(x => x as BioTriggerStream).ToList();
            BioTriggerStreams.ForEach(x => x.SetShape(ShowVolumes_BioTriggerStreams));
            graphEditor.Refresh();
        }

        private void ToggleActors(object obj)
        {
            ShowActorsLayer = !ShowActorsLayer;
            RefreshGraph();
        }

        private void ToggleSplines(object obj)
        {
            ShowSplinesLayer = !ShowSplinesLayer;
            RefreshGraph();
        }

        private void ToggleEverythingElse(object obj)
        {
            ShowEverythingElseLayer = !ShowEverythingElseLayer;
            RefreshGraph();
        }

        private void TogglePathfindingNodes(object obj)
        {
            ShowPathfindingNodesLayer = !ShowPathfindingNodesLayer;
            RefreshGraph();
        }

        private void SavePackageAs(object obj)
        {
            string extension = System.IO.Path.GetExtension(Pcc.FileName);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        public void FocusNode(IExportEntry node, bool select, long duration = 1000)
        {
            PathfindingNodeMaster s = GraphNodes.FirstOrDefault(o => o.UIndex == node.UIndex); //Will change to uindex eventually
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

        private void SavePackage(object obj)
        {
            Pcc.save();
        }

        private void OpenPackage(object obj)
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

        private bool PackageIsLoaded(object obj)
        {
            return Pcc != null;
        }

        private void RefreshGraph(object obj)
        {
            RefreshGraph();
        }

        private void RefreshGraph()
        {
            if (AllowRefresh)
            {
                IExportEntry currentSelectedItem = (IExportEntry)ActiveNodes_ListBox.SelectedItem;
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                ActiveNodes.ClearEx();
                GraphNodes.Clear();
                StaticMeshCollections.ClearEx();
                CombatZones.ClearEx();
                LoadPathingNodesFromLevel();
                GenerateGraph();
                graphEditor.Refresh();
            }
        }

        private void FocusGoto(object obj)
        {
            FindByNumber_TextBox.Focus();
        }

        private void FocusFind(object obj)
        {
            FindByTag_ComboBox.Focus();
        }

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
                        return "Showing all nodes above Z=" + ZFilteringValue;
                    case EZFilterIncludeDirection.AboveEquals:
                        return "Showing all nodes at or above Z=" + ZFilteringValue;
                    case EZFilterIncludeDirection.Below:
                        return "Showing all nodes below Z=" + ZFilteringValue;
                    case EZFilterIncludeDirection.BelowEquals:
                        return "Showing all nodes at or below Z=" + ZFilteringValue;
                }
                return "Unknown";
            }
        }

        public string NodeTypeDescriptionText
        {
            get
            {
                if (ActiveNodes_ListBox != null && ActiveNodes_ListBox.SelectedItem is IExportEntry CurrentLoadedExport)
                {
                    if (SharedPathfinding.ExportClassDB.FirstOrDefault(x => x.nodetypename == CurrentLoadedExport.ClassName) is PathfindingDB_ExportType classinfo)
                    {
                        return classinfo.description;
                    }
                    else
                    {
                        return "This node type does not have any information detailed about its purpose.";
                    }
                    /*switch (CurrentLoadedExport.ClassName)
                    {
                        case "PathNode": return "A basic pathing node that all basic movement can use.";
                        case "SFXNav_LargeBoostNode": return "A node that allows large creatures to boost to another LargeBoostNode, such as a Banshee floating up or down vertical distances.";
                        case "SFXNav_TurretPoint": return "A basic pathing node that a Cerberus Engineer can place a turret at.";

                        case "CoverSlotMarker": return "A node where AI can take cover. It is owned by a CoverLink and is part of a chain of continuous CoverSlotMarkers.";
                        case "BioPathPoint": return "A basic pathing node that can be enabled or disabled in Kismet.";
                        case "SFXEnemySpawnPoint": return "A basic pathing node that can be used as a spawn point for Mass Effect 3 Multiplayer enemies. It contains a list of required sync actions that using this spawn point will require to enter the main area of the map.";
                        case "SFXNav_LargeMantleNode": return "A node that can be large mantled over to reach another large mantle node. This action is used when climbing over large cover by AI.";
                    }*/
                }
                else
                {
                    return "No node is currently selected";
                }
            }
        }

        #endregion

        /// <summary>
        /// Called from winforms graph
        /// </summary>
        public void OpenContextMenu()
        {
            ContextMenu contextMenu = this.FindResource("nodeContextMenu") as ContextMenu;
            IExportEntry export = ActiveNodes_ListBox.SelectedItem as IExportEntry;
            if (export != null)
            {
                PathfindingNodeMaster s = GraphNodes.FirstOrDefault(o => o.UIndex == export.UIndex);
                var debug = s.Tag;
                var currentlocation = GetLocation(export);
                CurrentNodeXY = s.GlobalBounds.X + "," + s.GlobalBounds.Y;

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
            StatusBar_GameID_Container.Visibility = Visibility.Collapsed;
            StatusText = "Loading " + System.IO.Path.GetFileName(fileName);
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);

            LoadMEPackage(fileName);
            PersisentLevelExport = Pcc.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
            if (PersisentLevelExport == null)
            {
                Pcc.Release();
                Pcc = null;
                StatusText = "Select a package file to load";
                PathfindingEditorWPF_ReachSpecsPanel.UnloadExport();
                PathfindingEditorWPF_ValidationPanel.UnloadPackage();
                MessageBox.Show("This file does not contain a Level export.");
                return;
            }
            Mouse.OverrideCursor = Cursors.Wait;

            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    StatusBar_GameID_Text.Text = "ME1";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                    break;
                case MEGame.ME2:
                    StatusBar_GameID_Text.Text = "ME2";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Maroon);
                    break;
                case MEGame.ME3:
                    StatusBar_GameID_Text.Text = "ME3";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkSeaGreen);
                    break;
                case MEGame.UDK:
                    StatusBar_GameID_Text.Text = "UDK";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.IndianRed);
                    break;
            }
            StatusBar_GameID_Container.Visibility = Visibility.Visible;
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
                CurrentFile = System.IO.Path.GetFileName(fileName);
                AddRecent(fileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
                Title = "Pathfinding Editor WPF - " + fileName;
                StatusText = null; //Nothing to prepend.
                PathfindingEditorWPF_ValidationPanel.SetLevel(PersisentLevelExport);
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

        private bool LoadPathingNodesFromLevel()
        {
            if (Pcc == null || PersisentLevelExport == null)
            {
                return false;
            }

            IsReadingLevel = true;
            graphEditor.UseWaitCursor = true;

            /*staticMeshCollectionActorsToolStripMenuItem.DropDownItems.Clear();
            staticMeshCollectionActorsToolStripMenuItem.Enabled = false;
            staticMeshCollectionActorsToolStripMenuItem.ToolTipText = "No StaticMeshCollectionActors found in this file";
            sFXCombatZonesToolStripMenuItem.DropDownItems.Clear();
            sFXCombatZonesToolStripMenuItem.Enabled = false;
            sFXCombatZonesToolStripMenuItem.ToolTipText = "No SFXCombatZones found in this file";
            sfxCombatZones = new List<int>();
            CurrentObjects = new List<int>();
            activeExportsListbox.Items.Clear();*/
            AllLevelObjects.Clear();
            //Read persistent level binary
            byte[] data = PersisentLevelExport.Data;

            //find start of class binary (end of props)
            int start = PersisentLevelExport.propsEnd();

            //Console.WriteLine("Found start of binary at " + start.ToString("X8"));

            uint exportid = BitConverter.ToUInt32(data, start);
            start += 4;
            uint numberofitems = BitConverter.ToUInt32(data, start);
            int countoffset = start;

            start += 4;
            int bioworldinfoexportid = BitConverter.ToInt32(data, start);

            IExportEntry bioworldinfo = Pcc.getUExport(bioworldinfoexportid);
            if (bioworldinfo.ObjectName != "BioWorldInfo")
            {
                //INVALID!!
                return false;
            }
            AllLevelObjects.Add(bioworldinfo);

            start += 4;
            uint shouldbezero = BitConverter.ToUInt32(data, start);
            if (shouldbezero != 0 && Pcc.Game != MEGame.ME1)
            {
                //INVALID!!!
                return false;
            }
            int itemcount = 1; //Skip bioworldinfo and Class
            if (Pcc.Game != MEGame.ME1)
            {
                start += 4;
                itemcount = 2;
            }
            List<IExportEntry> bulkActiveNodes = new List<IExportEntry>();
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
                if (Pcc.isUExport(itemexportid))
                {
                    IExportEntry exportEntry = Pcc.getUExport(itemexportid);
                    AllLevelObjects.Add(exportEntry);

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
                            ArrayProperty<StructProperty> connectionsProp = exportEntry.GetProperty<ArrayProperty<StructProperty>>("Connections");
                            if (connectionsProp != null)
                            {
                                foreach (StructProperty connectionProp in connectionsProp)
                                {
                                    ObjectProperty splinecomponentprop = connectionProp.GetProp<ObjectProperty>("SplineComponent");
                                    bulkActiveNodes.Add(Pcc.getUExport(splinecomponentprop.Value));
                                }
                            }
                        }
                    }

                    if (exportEntry.ClassName == "StaticMeshCollectionActor")
                    {
                        StaticMeshCollections.Add(new StaticMeshCollection(exportEntry));
                    }
                    else if (exportEntry.ClassName == "SFXCombatZone" || exportEntry.ClassName == "BioPlaypenVolumeAdditive")
                    {
                        CombatZones.Add(new Zone(exportEntry));
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
                    /*
                    Console.WriteLine("0x" + start.ToString("X8") + "\t0x" + itemexportid.ToString("X8") + "\tInvalid item. Ensure the list is the correct length. (Export " + itemexportid + ")");
                    TreeNode node = new TreeNode();
                    node.Tag = nodeType.ArrayLeafObject;
                    node.Text = start.ToString("X4") + " Invalid item.Ensure the list is the correct length. (Export " + itemexportid + ")";
                    node.Name = start.ToString();
                    topLevelTree.Nodes.Add(node);*/
                    start += 4;
                    itemcount++;
                }
            }

            ActiveNodes.ReplaceAll(bulkActiveNodes);

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

        private bool isAllowedVisibleByZFiltering(IExportEntry exportEntry)
        {
            if (ZFilteringMode == EZFilterIncludeDirection.None) { return true; }
            Point3D position = GetLocation(exportEntry);
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
            int currentcount = ActiveNodes.Count(); //Some objects load additional objects. We need to count before we iterate over the graphsnode list as it may be appended to during this loop.
            for (int i = 0; i < currentcount; i++)
            {
                PointF pos = LoadObject(ActiveNodes[i]);
                fullx += pos.X;
                fully += pos.Y;
            }
            PointF centerpoint = new PointF((float)(fullx / GraphNodes.Count), (float)(fully / GraphNodes.Count));
            CreateConnections();

            #region Sequence References to nodes
            if (ShowSequenceReferences)
            {

                var referencemap = new Dictionary<int, List<IExportEntry>>(); //node index mapped to list of things referencing it
                foreach (IExportEntry export in Pcc.Exports)
                {
                    if (export.ClassName == "SFXSeqEvt_Touch" || export.ClassName.StartsWith("SeqVar") || export.ClassName.StartsWith("SFXSeq"))
                    {
                        var props = export.GetProperties();
                        var originator = props.GetProp<ObjectProperty>("Originator");
                        var objvalue = props.GetProp<ObjectProperty>("ObjValue");

                        if (originator != null)
                        {
                            var uindex = originator.Value; //0-based indexing is used here
                            List<IExportEntry> list;
                            if (!referencemap.TryGetValue(uindex, out list))
                            {
                                list = new List<IExportEntry>();
                                referencemap[uindex] = list;
                            }
                            list.Add(export);
                        }
                        if (objvalue != null)
                        {
                            var uindex = objvalue.Value;
                            List<IExportEntry> list;
                            if (!referencemap.TryGetValue(uindex, out list))
                            {
                                list = new List<IExportEntry>();
                                referencemap[uindex] = list;
                            }
                            list.Add(export);
                        }
                    }
                }

                //Add references to nodes
                foreach (PathfindingNodeMaster pnm in GraphNodes)
                {
                    if (referencemap.TryGetValue(pnm.UIndex, out List<IExportEntry> list))
                    {
                        //node is referenced
                        pnm.SequenceReferences.AddRange(list);
                        pnm.comment.Text += "\nReferenced in " + list.Count() + " sequence object" + (list.Count() != 1 ? "s" : "") + ":";
                        foreach (IExportEntry x in list)
                        {
                            string shortpath = x.GetFullPath;
                            if (shortpath.StartsWith("TheWorld.PersistentLevel."))
                            {
                                shortpath = shortpath.Substring("TheWorld.PersistentLevel.".Length);
                            }
                            pnm.comment.Text += "\n  " + x.UIndex + " " + shortpath + "_" + x.indexValue;
                        }
                    }
                }
            }
            #endregion


            TagsList.ClearEx();
            foreach (var node in GraphNodes)
            {
                node.MouseDown += node_MouseDown;
                if (node.NodeTag != null && node.NodeTag != "" && !TagsList.Contains(node.NodeTag))
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

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                PathfindingEditorWPF_ReachSpecsPanel.SetDestinationNode(node.UIndex);
                return;
            }

            ChangingSelectionByGraphClick = true;

            ActiveNodes_ListBox.SelectedItem = node.export;
            if ((node is SplinePoint0Node) || (node is SplinePoint1Node))
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

        public PointF LoadObject(IExportEntry exporttoLoad)
        {
            string s = exporttoLoad.ObjectName;
            int uindex = exporttoLoad.UIndex;
            int x = 0, y = 0, z = int.MinValue;
            var props = exporttoLoad.GetProperties();
            Point3D position = GetLocation(exporttoLoad);
            if (position != null)
            {
                x = (int)position.X;
                y = (int)position.Y;
                z = (int)position.Z;

                //if (ZFilteringMode != EZFilterIncludeDirection.None)
                //{
                //    bool includedInView = false;
                //    switch (ZFilteringMode)
                //    {
                //        case EZFilterIncludeDirection.Above:
                //            includedInView = z > ZFilteringValue;
                //            break;
                //        case EZFilterIncludeDirection.AboveEquals:
                //            includedInView = z >= ZFilteringValue;
                //            break;
                //        case EZFilterIncludeDirection.Below:
                //            includedInView = z < ZFilteringValue;
                //            break;
                //        case EZFilterIncludeDirection.BelowEquals:
                //            includedInView = z <= ZFilteringValue;
                //            break;
                //    }
                //    //Don't add as graph node, but add average point anyways.
                //    if (!includedInView)
                //    {
                //        return new PointF(x, y);
                //    }
                //}
            }

            //if (CurrentFilterType != HeightFilterForm.FILTER_Z_NONE)
            //{
            //    if (CurrentFilterType == HeightFilterForm.FILTER_Z_BELOW && z < CurrentZFilterValue)
            //    {
            //        return;
            //    }
            //    else if (CurrentFilterType == HeightFilterForm.FILTER_Z_ABOVE && z > CurrentZFilterValue)
            //    {
            //        return;
            //    }
            //}

            //IExportEntry export = pcc.getExport(index);
            if (pathfindingNodeClasses.Contains(exporttoLoad.ClassName))
            {
                PathfindingNode pathNode = null;
                switch (exporttoLoad.ClassName)
                {
                    case "PathNode":
                        pathNode = new PathfindingNodes.PathNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXEnemySpawnPoint":
                        pathNode = new PathfindingNodes.SFXEnemySpawnPoint(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_JumpNode":
                        pathNode = new PathfindingNodes.SFXNav_JumpNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LeapNodeHumanoid":
                        pathNode = new PathfindingNodes.SFXNav_LeapNodeHumanoid(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXDoorMarker":
                        pathNode = new PathfindingNodes.SFXDoorMarker(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LargeMantleNode":
                        pathNode = new PathfindingNodes.SFXNav_LargeMantleNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXDynamicPathNode":
                    case "BioPathPoint":
                        pathNode = new PathfindingNodes.BioPathPoint(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "PathNode_Dynamic":
                        pathNode = new PathfindingNodes.PathNode_Dynamic(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LargeBoostNode":
                        pathNode = new PathfindingNodes.SFXNav_LargeBoostNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_TurretPoint":
                        pathNode = new PathfindingNodes.SFXNav_TurretPoint(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "CoverLink":
                        pathNode = new PathfindingNodes.CoverLink(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_JumpDownNode":
                        pathNode = new PathfindingNodes.SFXNav_JumpDownNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXNav_LadderNode":
                        pathNode = new PathfindingNodes.SFXNav_LadderNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXDynamicCoverLink":
                        pathNode = new PathfindingNodes.SFXDynamicCoverLink(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;



                    case "CoverSlotMarker":
                        pathNode = new PathfindingNodes.CoverSlotMarker(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXDynamicCoverSlotMarker":
                        pathNode = new PathfindingNodes.SFXDynamicCoverSlotMarker(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "MantleMarker":
                        pathNode = new PathfindingNodes.MantleMarker(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;


                    case "SFXNav_HarvesterMoveNode":
                        pathNode = new PathfindingNodes.SFXNav_HarvesterMoveNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;

                    case "SFXNav_BoostNode":
                        pathNode = new PathfindingNodes.SFXNav_BoostNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    default:
                        pathNode = new PathfindingNodes.PendingNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                }

                //if (ActiveCombatZoneExportIndex >= 0 && exporttoLoad.ClassName == "CoverSlotMarker")
                //{
                //    ArrayProperty<StructProperty> volumes = props.GetProp<ArrayProperty<StructProperty>>("Volumes");
                //    if (volumes != null)
                //    {
                //        foreach (StructProperty volume in volumes)
                //        {
                //            ObjectProperty actorRef = volume.GetProp<ObjectProperty>("Actor");
                //            if (actorRef != null)
                //            {
                //                if (actorRef.Value == ActiveCombatZoneExportIndex + 1)
                //                {
                //                    Debug.WriteLine("FOUND ACTIVE COMBAT NODE!");
                //                    pathNode.shape.Brush = PathfindingNodeMaster.sfxCombatZoneBrush;
                //                    break;
                //                }
                //            }
                //        }
                //    }
                //}
                GraphNodes.Add(pathNode);
                return new PointF(x, y);
            } //End if Pathnode Class 

            else if (actorNodeClasses.Contains(exporttoLoad.ClassName))
            {
                ActorNode actorNode = null;
                switch (exporttoLoad.ClassName)
                {
                    case "BlockingVolume":
                        actorNode = new BlockingVolume(uindex, x, y, exporttoLoad.FileRef, graphEditor, ShowVolumes_BlockingVolumes);
                        break;
                    case "BioPlaypenVolumeAdditive":
                        actorNode = new BioPlaypenVolumeAdditive(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "DynamicBlockingVolume":
                        actorNode = new DynamicBlockingVolume(uindex, x, y, exporttoLoad.FileRef, graphEditor, ShowVolumes_DynamicBlockingVolumes);
                        break;
                    case "DynamicTriggerVolume":
                        actorNode = new DynamicTriggerVolume(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "InterpActor":
                        actorNode = new InterpActorNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "BioTriggerVolume":
                        actorNode = new ActorNodes.BioTriggerVolume(uindex, x, y, exporttoLoad.FileRef, graphEditor, ShowVolumes_BioTriggerVolumes);
                        break;
                    case "BioTriggerStream":
                        actorNode = new ActorNodes.BioTriggerStream(uindex, x, y, exporttoLoad.FileRef, graphEditor, ShowVolumes_BioTriggerStreams);
                        break;
                    case "SFXGrenadeContainer":
                        actorNode = new ActorNodes.SFXGrenadeContainer(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXAmmoContainer":
                        actorNode = new ActorNodes.SFXAmmoContainer(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXAmmoContainer_Simulator":
                        actorNode = new ActorNodes.SFXAmmoContainer_Simulator(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXBlockingVolume_Ledge":
                        actorNode = new ActorNodes.SFXBlockingVolume_Ledge(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXCombatZone":
                        actorNode = new ActorNodes.SFXCombatZone(uindex, x, y, exporttoLoad.FileRef, graphEditor, ShowVolumes_SFXCombatZones);
                        break;
                    case "BioStartLocation":
                    case "BioStartLocationMP":
                        actorNode = new ActorNodes.BioStartLocation(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "StaticMeshActor":
                        actorNode = new ActorNodes.StaticMeshActorNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXStuntActor":
                        actorNode = new ActorNodes.SFXStuntActor(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SkeletalMeshActor":
                        actorNode = new ActorNodes.SkeletalMeshActor(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXPlaceable_Generator":
                    case "SFXPlaceable_ShieldGenerator":
                        actorNode = new ActorNodes.SFXPlaceable(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "WwiseAmbientSound":
                        actorNode = new ActorNodes.WwiseAmbientSound(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "WwiseAudioVolume":
                        actorNode = new ActorNodes.WwiseAudioVolume(uindex, x, y, exporttoLoad.FileRef, graphEditor, ShowVolumes_WwiseAudioVolumes);
                        break;
                    case "SFXArmorNode":
                    case "SFXTreasureNode":
                        actorNode = new ActorNodes.SFXTreasureNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXMedStation":
                        actorNode = new ActorNodes.SFXMedStation(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "TargetPoint":
                        actorNode = new ActorNodes.TargetPoint(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    case "SFXOperation_ObjectiveSpawnPoint":
                        actorNode = new ActorNodes.SFXObjectiveSpawnPoint(uindex, x, y, exporttoLoad.FileRef, graphEditor);

                        //Create annex node if required
                        var annexZoneLocProp = props.GetProp<ObjectProperty>("AnnexZoneLocation");
                        if (annexZoneLocProp != null)
                        {
                            if (!Pcc.isUExport(annexZoneLocProp.Value))
                            {
                                actorNode.comment.Text += "\nBAD ANNEXZONELOC!";
                                actorNode.comment.TextBrush = new SolidBrush(System.Drawing.Color.Red);
                            }
                        }
                        break;
                    case "SFXMedkit":
                        actorNode = new ActorNodes.SFXMedKit(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                    default:
                        actorNode = new PendingActorNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                }
                actorNode.DoubleClick += actornode_DoubleClick;
                GraphNodes.Add(actorNode);
                return new PointF(x, y);
            }

            else if (splineNodeClasses.Contains(exporttoLoad.ClassName))
            {
                SplineNode splineNode = null;
                switch (exporttoLoad.ClassName)
                {
                    case "SplineActor":
                        splineNode = new SplineActorNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);

                        ArrayProperty<StructProperty> connectionsProp = exporttoLoad.GetProperty<ArrayProperty<StructProperty>>("Connections");
                        if (connectionsProp != null)
                        {
                            foreach (StructProperty connectionProp in connectionsProp)
                            {
                                ObjectProperty splinecomponentprop = connectionProp.GetProp<ObjectProperty>("SplineComponent");
                                IExportEntry splineComponentExport = Pcc.getUExport(splinecomponentprop.Value);
                                //Debug.WriteLine(splineComponentExport.GetFullPath + " " + splinecomponentprop.Value);
                                StructProperty splineInfo = splineComponentExport.GetProperty<StructProperty>("SplineInfo");
                                if (splineInfo != null)
                                {
                                    ArrayProperty<StructProperty> pointsProp = splineInfo.GetProp<ArrayProperty<StructProperty>>("Points");
                                    StructProperty point1 = pointsProp[0].GetProp<StructProperty>("OutVal");
                                    double xf = point1.GetProp<FloatProperty>("X");
                                    double yf = point1.GetProp<FloatProperty>("Y");
                                    //double zf = point1.GetProp<FloatProperty>("Z");
                                    //Point3D point1_3d = new Point3D(xf, yf, zf);
                                    SplinePoint0Node point0node = new SplinePoint0Node(splinecomponentprop.Value, Convert.ToInt32(xf), Convert.ToInt32(yf), exporttoLoad.FileRef, graphEditor);
                                    StructProperty point2 = pointsProp[1].GetProp<StructProperty>("OutVal");
                                    xf = point2.GetProp<FloatProperty>("X");
                                    yf = point2.GetProp<FloatProperty>("Y");
                                    //zf = point2.GetProp<FloatProperty>("Z");
                                    //Point3D point2_3d = new Point3D(xf, yf, zf);
                                    SplinePoint1Node point1node = new SplinePoint1Node(splinecomponentprop.Value, Convert.ToInt32(xf), Convert.ToInt32(yf), exporttoLoad.FileRef, graphEditor);
                                    point0node.SetDestinationPoint(point1node);

                                    GraphNodes.Add(point0node);
                                    GraphNodes.Add(point1node);

                                    StructProperty reparamProp = splineComponentExport.GetProperty<StructProperty>("SplineReparamTable");
                                    ArrayProperty<StructProperty> reparamPoints = reparamProp.GetProp<ArrayProperty<StructProperty>>("Points");
                                }
                            }
                        }
                        break;
                    default:
                        splineNode = new PendingSplineNode(uindex, x, y, exporttoLoad.FileRef, graphEditor);
                        break;
                }
                GraphNodes.Add(splineNode);
                return new PointF(x, y);
            }
            else
            {
                //everything else
                GraphNodes.Add(new EverythingElseNode(uindex, x, y, exporttoLoad.FileRef, graphEditor));
                return new PointF(x, y);
            }
            //Hopefully we don't see this.
            return new PointF(0, 0);
        }

        private void actornode_DoubleClick(object sender, PInputEventArgs e)
        {
            ActorNode an = sender as ActorNode;
            if (an != null)
            {
                an.SetShape(!an.ShowAsPolygon);
            }
            an.InvalidateFullBounds();
            graphEditor.Refresh();
            graphEditor.Camera.AnimateViewToCenterBounds(an.GlobalFullBounds, false, 500);
        }

        public void CreateConnections()
        {
            if (GraphNodes != null && GraphNodes.Count != 0)
            {
                for (int i = 0; i < GraphNodes.Count; i++)
                {
                    graphEditor.addNode(GraphNodes[i]);
                }
                foreach (PathfindingNodeMaster o in graphEditor.nodeLayer)
                {
                    o.CreateConnections(ref GraphNodes);
                }

                foreach (PPath edge in graphEditor.edgeLayer)
                {
                    //if (edge.BezierPoints != null)
                    //{
                    //    //Currently not implemented, will hopefully come in future update
                    //    PathingGraphEditor.UpdateEdgeBezier(edge);
                    //}
                    //else
                    //{
                    PathingGraphEditor.UpdateEdgeStraight(edge as PathfindingEditorEdge);
                    //}
                }
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);
            bool exportsAdded = changes.Contains(PackageChange.ExportAdd);

            var activeNode = ActiveNodes_ListBox.SelectedItem as IExportEntry;
            //we might need to identify parent depths and add those first
            List<PackageUpdate> addedChanges = updates.Where(x => x.change == PackageChange.ExportAdd || x.change == PackageChange.ImportAdd).OrderBy(x => x.index).ToList();
            List<int> headerChanges = updates.Where(x => x.change == PackageChange.ExportHeader).Select(x => x.index).OrderBy(x => x).ToList();
            if (exportsAdded || exportNonDataChanges) //may optimize by checking if chagnes include anything we care about
            {
                //Do a full refresh
                IExportEntry selectedExport = ActiveNodes_ListBox.SelectedItem as IExportEntry;
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
                        var newlocation = GetLocation(node);
                        PathfindingNodeMaster s = GraphNodes.FirstOrDefault(o => o.UIndex == node.UIndex);
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
            set => SetProperty(ref _statusText, CurrentFile + " " + value);
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
        public static readonly string PathfindingEditorDataFolder = System.IO.Path.Combine(App.AppDataFolder, @"PathfindingEditor\");
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
                //we are posting an update to other instances of packed

                //This code can be removed when non-WPF package editor is removed.
                foreach (var form in App.Current.Windows)
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
            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                RecentButtons[i].Visibility = Visibility.Visible;
                RecentButtons[i].Content = System.IO.Path.GetFileName(filepath.Replace("_", "__"));
                RecentButtons[i].Click -= RecentFile_click;
                RecentButtons[i].Click += RecentFile_click;
                RecentButtons[i].Tag = filepath;
                RecentButtons[i].ToolTip = filepath;
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
                MessageBox.Show("File does not exist: " + s);
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
            }

            if (ActiveNodes_ListBox.SelectedItem != null)
            {
                CombatZonesLoading = true;

                IExportEntry export = (IExportEntry)ActiveNodes_ListBox.SelectedItem;
                NodeName = $"{export.ObjectName}_{export.indexValue}";
                NodeNameSubText = $"Export {export.UIndex}";
                ActiveNodes_ListBox.ScrollIntoView(export);
                Properties_InterpreterWPF.LoadExport(export);
                CurrentNodeCombatZones.ClearEx();

                PathfindingNodeMaster selectedNode = GraphNodes.FirstOrDefault(o => o.UIndex == export.UIndex);
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
                }

                CombatZonesLoading = false;

                PathfindingEditorWPF_ReachSpecsPanel.LoadExport(export);

#if DEBUG
                //Populate the export/import database

                /*
                if (!(SharedPathfinding.ExportClassDB.FirstOrDefault(x=>x.nodetypename == export.ClassName) is PathfindingDB_ExportType))
                {
                    Dictionary<string, string> data = new Dictionary<string, string>();
                    data["class"] = export.FileRef.getEntry(export.idxClass).GetFullPath;
                    data["name"] = export.ClassName;
                    var collisioncomponent = export.GetProperty<ObjectProperty>("CollisionComponent");
                    if (collisioncomponent != null)
                    {
                        IExportEntry collisionComp = export.FileRef.getUExport(collisioncomponent.Value);

                        data["cylindercomponentarchetype"] = collisionComp.FileRef.getEntry(collisionComp.idxArchtype).GetFullPath;

                        ////Add imports
                        if (!SharedPathfinding.ImportClassDB.ContainsKey(data["cylindercomponentarchetype"]) && collisionComp.idxArchtype < 0)
                        {
                            //X.Default.CollisionCylinder
                            Dictionary<string, string> cylindercompimp = new Dictionary<string, string>();
                            ImportEntry collisionCylinderArchetype = collisionComp.FileRef.getEntry(collisionComp.idxArchtype) as ImportEntry;

                            cylindercompimp["class"] = collisionCylinderArchetype.ClassName == "Class" ? "Class" : collisionCylinderArchetype.PackageFileNoExtension + "." + collisionCylinderArchetype.ClassName;
                            cylindercompimp["packagefile"] = collisionCylinderArchetype.PackageFileNoExtension;
                            SharedPathfinding.ImportClassDB[data["cylindercomponentarchetype"]] = cylindercompimp;

                            //X.Default
                            Dictionary<string, string> nodetypeimp = new Dictionary<string, string>();
                            ImportEntry collisionCylinderArchetypeDefault = collisionCylinderArchetype.FileRef.getEntry(collisionCylinderArchetype.idxLink) as ImportEntry;
                            nodetypeimp["class"] = collisionCylinderArchetypeDefault.ClassName == "Class" ? "Class" : collisionCylinderArchetypeDefault.PackageFileNoExtension + "." + collisionCylinderArchetypeDefault.ClassName;
                            nodetypeimp["packagefile"] = collisionCylinderArchetypeDefault.PackageFileNoExtension;
                            SharedPathfinding.ImportClassDB[collisionCylinderArchetypeDefault.GetFullPath] = nodetypeimp;
                        }
                    }
                    data["description"] = "No data about this node type has been entered yet";
                    SharedPathfinding.ExportClassDB[export.ClassName] = data;

                    Dictionary<string, string> nodeclassimport = new Dictionary<string, string>();
                    ImportEntry classImport = export.FileRef.getEntry(export.idxClass) as ImportEntry;

                    if (classImport != null)
                    {
                        nodeclassimport["class"] = classImport.ClassName == "Class" ? "Class" : classImport.PackageFileNoExtension + "." + classImport.ClassName;
                        nodeclassimport["packagefile"] = classImport.PackageFileNoExtension;

                        SharedPathfinding.ImportClassDB[classImport.GetFullPath] = nodeclassimport;

                        //Rename vars - debug only
                        var exporttypes = SharedPathfinding.ExportClassDB;
                        var importtypes = SharedPathfinding.ImportClassDB;

                        Debug.WriteLine("Adding to pathfinding database file: " + export.ClassName);
                        File.WriteAllText(SharedPathfinding.ClassesDatabasePath,
                    JsonConvert.SerializeObject(new { exporttypes, importtypes }, Formatting.Indented));
                    }
                }*/

#endif
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

                    Point3D position = GetLocation(export);
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
                    //switch (s.export.ClassName)
                    //{
                    //    case "CoverLink":
                    //        HighlightCoverlinkSlots(s.export);
                    //        break;
                    //    case "CoverSlotMarker":
                    //        StructProperty sp = s.export.GetProperty<StructProperty>("OwningSlot");
                    //        if (sp != null)
                    //        {
                    //            ObjectProperty op = sp.GetProp<ObjectProperty>("Link");
                    //            if (op != null && op.Value - 1 < pcc.ExportCount)
                    //            {
                    //                HighlightCoverlinkSlots(pcc.Exports[op.Value - 1]);
                    //            }
                    //        }
                    //        break;
                    //}
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

        private List<Zone> CloneCombatZonesForSelections()
        {
            List<Zone> clones = new List<Zone>();
            foreach (Zone z in CombatZones)
            {
                clones.Add(new Zone(z));
            }

            return clones;
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            graphEditor.AllowDragging();
        }

        private void FindByTag_Click(object sender, RoutedEventArgs e)
        {
            FindByTag();
        }

        private void FindByTag()
        {
            int currentIndex = ActiveNodes_ListBox.SelectedIndex;
            var currnentSelectedItem = ActiveNodes_ListBox.SelectedItem as IExportEntry;
            if (currentIndex < 0 || currentIndex >= ActiveNodes.Count - 1) currentIndex = -1; //nothing selected or the final item is selected
            currentIndex++; //search next item

            string nodeTagToFind = FindByTag_ComboBox.SelectedItem as string;
            if (nodeTagToFind is null) return; //empty

            for (int i = 0; i < ActiveNodes.Count(); i++) //activenodes size should match graphnodes size... in theory of course.
            {
                PathfindingNodeMaster ci = GraphNodes[(i + currentIndex) % GraphNodes.Count()];
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

        private void FindByNumber_Click(object sender, RoutedEventArgs e)
        {
            FindByNumber();
        }

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
            if (e.Key == Key.Return)
            {
                IExportEntry export = ActiveNodes_ListBox.SelectedItem as IExportEntry;
                if (export != null && int.TryParse(NodePositionX_TextBox.Text, out int x) && int.TryParse(NodePositionY_TextBox.Text, out int y) && int.TryParse(NodePositionZ_TextBox.Text, out int z))
                {
                    SetLocation(export, x, y, z);
                    PathfindingNodeMaster s = GraphNodes.FirstOrDefault(o => o.UIndex == export.UIndex);
                    s.SetOffset(x, y);

                    //TODO: Figure out what this does

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
        }

        public void UpdateEdgesForCurrentNode(PathfindingNodeMaster node = null)
        {
            PathfindingNodeMaster nodeToUpdate = node;
            if (nodeToUpdate == null && ActiveNodes_ListBox.SelectedItem is IExportEntry export)
            {
                nodeToUpdate = GraphNodes.FirstOrDefault(o => o.UIndex == export.UIndex);
            }

            if (nodeToUpdate != null)
            {
                graphEditor.edgeLayer.RemoveChildrenList(new List<PNode>(nodeToUpdate.Edges.Cast<PNode>()));
                nodeToUpdate.Edges.Clear();
                nodeToUpdate.CreateConnections(ref GraphNodes);
                foreach (PathfindingEditorEdge edge in nodeToUpdate.Edges)
                {
                    PathingGraphEditor.UpdateEdgeStraight(edge);
                }
                graphEditor.Refresh();
            }
        }

        private void SetLocation(IExportEntry export, float x, float y, float z)
        {
            StructProperty prop = export.GetProperty<StructProperty>("location");
            prop.GetProp<FloatProperty>("X").Value = x;
            prop.GetProp<FloatProperty>("Y").Value = y;
            prop.GetProp<FloatProperty>("Z").Value = z;
            export.WriteProperty(prop);
        }

        private void SetLocation(IExportEntry export, int x, int y, int z)
        {
            StructProperty prop = export.GetProperty<StructProperty>("location");
            prop.GetProp<FloatProperty>("X").Value = x;
            prop.GetProp<FloatProperty>("Y").Value = y;
            prop.GetProp<FloatProperty>("Z").Value = z;
            export.WriteProperty(prop);
        }

        public static Point3D GetLocation(IExportEntry export)
        {
            int x = 0, y = 0, z = int.MinValue;
            var props = export.GetProperties();
            StructProperty prop = props.GetProp<StructProperty>("location");
            if (prop != null)
            {
                PropertyCollection nodelocprops = (prop as StructProperty).Properties;
                foreach (var locprop in nodelocprops)
                {
                    switch (locprop.Name)
                    {
                        case "X":
                            x = Convert.ToInt32((locprop as FloatProperty).Value);
                            break;
                        case "Y":
                            y = Convert.ToInt32((locprop as FloatProperty).Value);
                            break;
                        case "Z":
                            z = Convert.ToInt32((locprop as FloatProperty).Value);
                            break;
                    }
                }
                return new Point3D(x, y, z);
            }
            return null;
        }

        private void SetGraphXY_Clicked(object sender, RoutedEventArgs e)
        {
            //Find node
            IExportEntry export = ActiveNodes_ListBox.SelectedItem as IExportEntry;
            if (export != null)
            {
                PathfindingNodeMaster s = GraphNodes.FirstOrDefault(o => o.UIndex == export.UIndex);
                var currentlocation = GetLocation(export);
                SetLocation(export, s.GlobalBounds.X, s.GlobalBounds.Y, (float)currentlocation.Z);
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
            IExportEntry export = ActiveNodes_ListBox.SelectedItem as IExportEntry;
            if (export != null)
            {
                PackageEditorWPF p = new PackageEditorWPF();
                p.Show();
                p.LoadFile(export.FileRef.FileName);
                p.GoToNumber(export.UIndex);
            }
        }

        private void CloneNode_Clicked(object sender, RoutedEventArgs e)
        {
            var newNode = cloneNode(ActiveNodes_ListBox.SelectedItem as IExportEntry);
            if (newNode != null)
            {
                ActiveNodes_ListBox.SelectedItem = newNode;
            }
        }

        private IExportEntry cloneNode(IExportEntry nodeEntry)
        {
            if (nodeEntry != null)
            {
                ObjectProperty collisionComponentProperty = nodeEntry.GetProperty<ObjectProperty>("CollisionComponent");
                IExportEntry collisionEntry = nodeEntry.FileRef.getUExport(collisionComponentProperty.Value);


                IExportEntry newNodeEntry = nodeEntry.Clone();
                nodeEntry.FileRef.addExport(newNodeEntry);
                IExportEntry newCollisionEntry = collisionEntry.Clone();
                nodeEntry.FileRef.addExport(newCollisionEntry);
                newCollisionEntry.idxLink = newNodeEntry.UIndex;

                //Update the cloned nodes to be new items
                bool changed = false;

                //empty the pathlist
                PropertyCollection newExportProps = newNodeEntry.GetProperties();

                ArrayProperty<ObjectProperty> PathList = newExportProps.GetProp<ArrayProperty<ObjectProperty>>("PathList");
                if (PathList != null && PathList.Count > 0)
                {
                    changed = true;
                    PathList.Clear();
                }

                foreach (UProperty prop in newExportProps)
                {
                    if (prop is ObjectProperty)
                    {
                        var objProp = prop as ObjectProperty;
                        if (objProp.Value == collisionEntry.UIndex)
                        {
                            objProp.Value = newCollisionEntry.UIndex;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    newNodeEntry.WriteProperties(newExportProps);
                }

                var oldloc = GetLocation(newNodeEntry);
                SetLocation(newNodeEntry, (float)oldloc.X + 50, (float)oldloc.Y + 50, (float)oldloc.Z);
                /*
                collisionComponentProperty = newNodeEntry.GetProperty<ObjectProperty>("CollisionComponent");
                if (collisionComponentProperty != null)
                {
                    collisionComponentProperty.Value = newCollisionEntry.UIndex;
                    newNodeEntry.WriteProperty(collisionComponentProperty);
                }

                collisionComponentProperty = newNodeEntry.GetProperty<ObjectProperty>("CylinderComponent");
                if (collisionComponentProperty != null)
                {
                    collisionComponentProperty.Value = newCollisionEntry.UIndex;
                    newNodeEntry.WriteProperty(collisionComponentProperty);

                }*/

                SharedPathfinding.GenerateNewRandomGUID(newNodeEntry);
                //Add cloned node to persistentlevel
                //Read persistent level binary
                byte[] data = PersisentLevelExport.Data;

                //find start of class binary (end of props)
                int start = PersisentLevelExport.propsEnd();

                uint exportid = BitConverter.ToUInt32(data, start);
                start += 4;
                uint numberofitems = BitConverter.ToUInt32(data, start);
                numberofitems++;
                SharedPathfinding.WriteMem(data, start, BitConverter.GetBytes(numberofitems));
                int insertoffset = (int)(numberofitems * 4) + start;
                List<byte> memList = data.ToList();
                memList.InsertRange(insertoffset, BitConverter.GetBytes(newNodeEntry.UIndex));
                data = memList.ToArray();
                PersisentLevelExport.Data = data;

                SharedPathfinding.ReindexMatchingObjects(newNodeEntry);
                SharedPathfinding.ReindexMatchingObjects(newCollisionEntry);
                return newNodeEntry;
            }
            return null;
        }

        [DebuggerDisplay("{export.UIndex} Static Mesh Collection")]
        public class StaticMeshCollection : NotifyPropertyChangedBase
        {
            private bool _active;
            public bool Active { get => _active; set => SetProperty(ref _active, value); }

            public List<IExportEntry> CollectionItems = new List<IExportEntry>();
            public IExportEntry export { get; private set; }
            public string DisplayString { get => $"{export.UIndex}\t{CollectionItems.Count} items"; }
            public StaticMeshCollection(IExportEntry smac)
            {
                export = smac;
                ArrayProperty<ObjectProperty> smacItems = smac.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");
                if (smacItems != null)
                {
                    //Read exports...
                    foreach (ObjectProperty obj in smacItems)
                    {
                        if (obj.Value > 0)
                        {
                            IExportEntry item = smac.FileRef.getUExport(obj.Value);
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
            public List<System.Windows.Point> GetLocationData()
            {
                byte[] smacData = export.Data;
                int binarypos = export.propsEnd();
                List<System.Windows.Point> positions = new List<System.Windows.Point>();
                foreach (var item in CollectionItems)
                {
                    if (item != null)
                    {
                        //Read location and put in position map
                        int offset = binarypos + 12 * 4;
                        float x = BitConverter.ToSingle(smacData, offset);
                        float y = BitConverter.ToSingle(smacData, offset + 4);
                        //Debug.WriteLine(offset.ToString("X4") + " " + x + "," + y);
                        positions.Add(new System.Windows.Point(x, y));
                    }
                    else
                    {
                        positions.Add(new System.Windows.Point(double.MinValue, double.MinValue));
                    }
                    binarypos += 64;
                }
                return positions;
            }
        }


        [DebuggerDisplay("{export.UIndex} Combat Zone (Active: {Active})")]
        public class Zone : NotifyPropertyChangedBase
        {
            public IExportEntry export { get; private set; }

            private bool _active;
            public bool Active { get => _active; set => SetProperty(ref _active, value); }
            public string DisplayString { get => $"{export.UIndex} {export.ObjectName}_{export.indexValue}"; }

            public Zone(IExportEntry combatZone)
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
                        zone.Active = false;
                    }
                    else
                    {
                        zone.Active = e.IsSelected;
                    }
                }
                IsCombatZonesSingleSelecting = false;

                //Highlight active combat zone
                Zone activeZone = CombatZones.FirstOrDefault(x => x.Active);

                //These statements are split into two groups for optimization purposes
                if (activeZone != null)
                {
                    foreach (var item in GraphNodes)
                    {
                        if (item is PathfindingNode node && node.Volumes.Count() > 0)
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
                var locations = smc.GetLocationData();
                for (int i = 0; i < smc.CollectionItems.Count(); i++)
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
                var activeNodesToRemove = ActiveNodes.Where(x => !smc.CollectionItems.Contains(x)).ToList();
                ActiveNodes.ReplaceAll(activeNodesToRemove);

                var graphNodesToRemove = GraphNodes.Where(x => smc.CollectionItems.Contains(x.export)).ToList();
                GraphNodes = GraphNodes.Except(graphNodesToRemove).ToList();
                graphEditor.nodeLayer.RemoveChildrenList(graphNodesToRemove.ToList<PNode>()); // sigh.
            }
            graphEditor.Refresh();
        }

        /// <summary>
        /// The current Z filtering value. This only is used if ZFilteringMode is not equal to None.
        /// </summary>
        public double ZFilteringValue { get => _zfilteringvalue; set => SetProperty(ref _zfilteringvalue, value); }
        private double _zfilteringvalue = 0;

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


        private void ShowNodes_Below_Click(object sender, RoutedEventArgs e)
        {
            SetFilteringMode(EZFilterIncludeDirection.Below);
        }

        private void ShowNodes_Above_Click(object sender, RoutedEventArgs e)
        {
            SetFilteringMode(EZFilterIncludeDirection.Above);
        }

        private void ShowNodes_BelowEqual_Click(object sender, RoutedEventArgs e)
        {
            SetFilteringMode(EZFilterIncludeDirection.BelowEquals);
        }

        private void ShowNodes_AboveEqual_Click(object sender, RoutedEventArgs e)
        {
            SetFilteringMode(EZFilterIncludeDirection.AboveEquals);
        }

        private void SetFilteringMode(EZFilterIncludeDirection newfilter)
        {
            bool shouldRefresh = newfilter != ZFilteringMode;
            IExportEntry export = ActiveNodes_ListBox.SelectedItem as IExportEntry;
            if (export != null && newfilter != EZFilterIncludeDirection.None)
            {
                PathfindingNodeMaster s = GraphNodes.FirstOrDefault(o => o.UIndex == export.UIndex);
                var currentlocation = GetLocation(export);
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
                ActiveNodes_ListBox.SelectedItem is IExportEntry nodeExport)
            {
                ArrayProperty<StructProperty> volumesList =
                    nodeExport.GetProperty<ArrayProperty<StructProperty>>("Volumes");
                if (e.IsSelected && volumesList == null)
                {
                    volumesList = new ArrayProperty<StructProperty>(ArrayType.Struct, "Volumes");
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
                    if (s != null)
                    {
                        s.Volumes.Add(new Volume(actorReference));
                        //todo: update and active combat zone pen for this node
                    }
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
                        PathfindingNode s = GraphNodes.FirstOrDefault(o => o.UIndex == nodeExport.UIndex) as PathfindingNode;
                        if (s != null)
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
                    Debug.WriteLine("prop removed: " + removed);
                }
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = System.IO.Path.GetExtension(files[0]).ToLower();
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
                string ext = System.IO.Path.GetExtension(files[0]).ToLower();
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
                string ext = System.IO.Path.GetExtension(files[0]).ToLower();
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
                string ext = System.IO.Path.GetExtension(files[0]).ToLower();
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

        public ObservableCollectionExtended<IExportEntry> CurrentNodeSequenceReferences { get; } = new ObservableCollectionExtended<IExportEntry>();
        public ObservableCollectionExtended<NodeType> AvailableNodeChangeableTypes { get; } = new ObservableCollectionExtended<NodeType>();
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
            IExportEntry nodeEntry = ActiveNodes_ListBox.SelectedItem as IExportEntry;

            //Read persistent level binary
            byte[] data = PersisentLevelExport.Data;

            //find start of class binary (end of props)
            int start = PersisentLevelExport.propsEnd();

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
                    PersisentLevelExport.Data = destarray;
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
            if (ActiveNodes_ListBox.SelectedItem is IExportEntry nodeEntry)
            {
                SharedPathfinding.GenerateNewRandomGUID(nodeEntry);
            }
        }
    }
}