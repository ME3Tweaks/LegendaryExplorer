using System;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.Win32;

namespace LegendaryExplorer.Tools.PathfindingEditor
{
    /// <summary>
    /// Interaction logic for ReachSpecsPanel.xaml
    /// </summary>
    public partial class ReachSpecsPanel : ExportLoaderControl
    {
        public PathfindingEditorWindow ParentWindow;

        private string _reachSpecSizeToText;
        public string ReachSpecSizeToText
        {
            get => _reachSpecSizeToText;
            set => SetProperty(ref _reachSpecSizeToText, value);
        }

        private string _externalFileShortNameText;

        private IMEPackage _externalFile;
        public IMEPackage ExternalFile
        {
            get => _externalFile;
            set => SetProperty(ref _externalFile, value);
        }

        private ExportEntry _externalPersisentLevelExport;
        public ExportEntry ExternalPersisentLevelExport
        {
            get => _externalPersisentLevelExport;
            set => SetProperty(ref _externalPersisentLevelExport, value);
        }

        public string ExternalFileShortNameText
        {
            get => _externalFileShortNameText;
            set => SetProperty(ref _externalFileShortNameText, value);
        }

        public bool ReachSpecSelectedInList => ReachableNodes_ComboBox.SelectedItem is ReachSpec;

        public override void PopOut()
        {
            //This loader is coupled with Pathfinding Editor's UI and cannot be popped out.
            throw new Exception("ReachSpecsPanel cannot be popped out. You shouldn't see this error!");
        }

        private bool _toExternalNodeChecked;
        public bool ToExternalNodeChecked
        {
            get => _toExternalNodeChecked;
            set => SetProperty(ref _toExternalNodeChecked, value);
        }

        public ObservableCollectionExtended<ReachSpec> ReachSpecs { get; } = new();
        public ObservableCollectionExtended<NodeSize> AvailableNodeSizes { get; } = new();
        public ObservableCollectionExtended<ReachSpecSize> AvailableReachSpecSizes { get; } = new();
        public ObservableCollectionExtended<ReachSpecSize> AvailableCreateReachSpecSizes { get; } = new();
        public ObservableCollectionExtended<string> AvailableReachspecTypes { get; } = new();

        private string _destinationNodeName;
        private string _newReachSpecDistance;
        private string _newReachSpecDirectionX;
        private string _newReachSpecDirectionY;
        private string _newReachSpecDirectionZ;
        private string _destinationNavGUIDText;
        public string DestinationNavGUIDText { get => _destinationNavGUIDText; private set => SetProperty(ref _destinationNavGUIDText, value); }
        public string DestinationNodeName { get => _destinationNodeName; private set => SetProperty(ref _destinationNodeName, value); }
        public string NewReachSpecDistance { get => _newReachSpecDistance; private set => SetProperty(ref _newReachSpecDistance, value); }
        public string NewReachSpecDirectionX { get => _newReachSpecDirectionX; private set => SetProperty(ref _newReachSpecDirectionX, value); }
        public string NewReachSpecDirectionY { get => _newReachSpecDirectionY; private set => SetProperty(ref _newReachSpecDirectionY, value); }
        public string NewReachSpecDirectionZ { get => _newReachSpecDirectionZ; private set => SetProperty(ref _newReachSpecDirectionZ, value); }

        private bool _createReturningReachSpec = true;
        private bool AllowChanges;

        public bool CreateReturningReachSpec { get => _createReturningReachSpec; set => SetProperty(ref _createReturningReachSpec, value); }


        public ReachSpecsPanel() : base("ReachSpecsPanel")
        {
            DataContext = this;
            ReachSpecSizeToText = "Select a reachspec above";
            DestinationNodeName = "Enter a node index above";
            NewReachSpecDistance = "No node selected";
            NewReachSpecDirectionX = "No node selected";
            NewReachSpecDirectionY = "No node selected";
            NewReachSpecDirectionZ = "No node selected";
            AvailableReachspecTypes.AddRange(new[] {
            "Engine.ReachSpec",
            "Engine.CoverSlipReachSpec",
            "Engine.CoverTurnReachSpec",
            "Engine.MantleReachSpec",
            "SFXGame.SFXBoostReachSpec",
            "SFXGame.SFXClimbWallReachSpec",
            "SFXGame.SFXLadderReachSpec",
            "SFXGame.SFXJumpDownReachSpec",
            "SFXGame.SFXLargeBoostReachSpec",
            "SFXGame.SFXLargeClimbReachSpec",
            "SFXGame.SFXLargeMantleReachSpec",
            "Engine.SlotToSlotReachSpec" });

            AvailableReachSpecSizes.Add(new ReachSpecSize("Bosses", ReachSpecSize.BOSS_HEIGHT, ReachSpecSize.BOSS_RADIUS));
            AvailableReachSpecSizes.Add(new ReachSpecSize("Banshee", ReachSpecSize.BANSHEE_HEIGHT, ReachSpecSize.BANSHEE_RADIUS));
            AvailableReachSpecSizes.Add(new ReachSpecSize("Harvester", ReachSpecSize.HARVESTER_HEIGHT, ReachSpecSize.HARVESTER_RADIUS));
            AvailableReachSpecSizes.Add(new ReachSpecSize("Minibosses", ReachSpecSize.MINIBOSS_HEIGHT, ReachSpecSize.MINIBOSS_RADIUS));
            AvailableReachSpecSizes.Add(new ReachSpecSize("Brutes", ReachSpecSize.MINIBOSS_HEIGHT, ReachSpecSize.BRUTE_RADIUS));
            AvailableReachSpecSizes.Add(new ReachSpecSize("Mooks", ReachSpecSize.MOOK_HEIGHT, ReachSpecSize.MOOK_RADIUS));

            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Bosses", ReachSpecSize.BOSS_HEIGHT, ReachSpecSize.BOSS_RADIUS));
            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Banshee", ReachSpecSize.BANSHEE_HEIGHT, ReachSpecSize.BANSHEE_RADIUS));
            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Harvester", ReachSpecSize.HARVESTER_HEIGHT, ReachSpecSize.HARVESTER_RADIUS));
            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Minibosses", ReachSpecSize.MINIBOSS_HEIGHT, ReachSpecSize.MINIBOSS_RADIUS));
            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Brutes", ReachSpecSize.MINIBOSS_HEIGHT, ReachSpecSize.BRUTE_RADIUS));
            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Mooks", ReachSpecSize.MOOK_HEIGHT, ReachSpecSize.MOOK_RADIUS));

            AvailableNodeSizes.Add(new NodeSize("Bosses", ReachSpecSize.BOSS_HEIGHT, ReachSpecSize.BOSS_RADIUS));
            AvailableNodeSizes.Add(new NodeSize("Banshee", ReachSpecSize.BANSHEE_HEIGHT, ReachSpecSize.BANSHEE_RADIUS));
            AvailableNodeSizes.Add(new NodeSize("Harvester", ReachSpecSize.HARVESTER_HEIGHT, ReachSpecSize.HARVESTER_RADIUS));
            AvailableNodeSizes.Add(new NodeSize("Minibosses", ReachSpecSize.MINIBOSS_HEIGHT, ReachSpecSize.MINIBOSS_RADIUS));
            AvailableNodeSizes.Add(new NodeSize("Brutes", ReachSpecSize.MINIBOSS_HEIGHT, ReachSpecSize.BRUTE_RADIUS));

            AvailableNodeSizes.Add(new NodeSize("Mooks", ReachSpecSize.MOOK_HEIGHT, ReachSpecSize.MOOK_RADIUS));

            LoadCommands();
            InitializeComponent();
            CreateReachspecType_ComboBox.SelectedIndex = 0;
            CreateReachSpecSize_ComboBox.SelectedIndex = 0;
            RefreshSelectedReachSpec();
        }

        public ICommand CreateReachSpecCommand { get; set; }
        public ICommand ChangeExternalFileCommand { get; set; }
        public ICommand ToExternalNodeCommand { get; set; }
        public ICommand DeleteSelectedReachSpecCommand { get; set; }


        private void LoadCommands()
        {
            CreateReachSpecCommand = new GenericCommand(CreateReachSpec, CanCreateReachSpec);
            ChangeExternalFileCommand = new GenericCommand(ChangeExternalFile, CanChangeExternalFile);
            ToExternalNodeCommand = new GenericCommand(ToExternalCommandChanging, ExportIsLoaded);
            DeleteSelectedReachSpecCommand = new RelayCommand(DeleteSelectedReachSpec);
        }

        private void DeleteSelectedReachSpec(object obj)
        {
            if (obj is ReachSpec spec)
            {
                var speclist = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                speclist.Remove(new ObjectProperty(spec.SpecExport.UIndex));
                CurrentLoadedExport.WriteProperty(speclist);
            }
        }

        private void ToExternalCommandChanging()
        {
            bool returnSetting = CreateReturningReachSpec;

            if (ExternalFile == null)
            {
                LoadExternalFile();
            }

            if (ExternalFile == null)
            {
                ToExternalNodeChecked = false;
                CreateReturningReachSpec = returnSetting;
            }
            else
            {
                CreateReturningReachSpec = false;
            }
        }

        private bool ExportIsLoaded() => CurrentLoadedExport != null;

        private bool CanChangeExternalFile() => ToExternalNodeChecked;

        private void ChangeExternalFile()
        {
            bool returnSetting = CreateReturningReachSpec;
            LoadExternalFile();
            if (ExternalFile == null)
            {
                ToExternalNodeChecked = false;
                CreateReturningReachSpec = returnSetting;
            }
            else
            {
                CreateReturningReachSpec = false; //Can't create returning from external, for now.
            }
        }

        private void LoadExternalFile()
        {
            var d = AppDirectories.GetOpenPackageDialog();
            if (d.ShowDialog() == true)
            {
                try
                {
                    ExternalFile?.Release();
                    ExternalFile = MEPackageHandler.OpenMEPackage(d.FileName);
                    ExternalPersisentLevelExport = ExternalFile.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
                    if (ExternalPersisentLevelExport == null)
                    {
                        ExternalFile.Release();
                        ExternalFile = null;
                        MessageBox.Show($"{d.FileName} does not have a PersistentLevel export.");
                        return;
                    }

                    ExternalFileShortNameText = System.IO.Path.GetFileName(d.FileName);

                    //LoadFile(d.FileName);
                }
                catch (Exception ex)
                {
                    ExternalFile = null;
                    ExternalFileShortNameText = "Error opening file";
                    MessageBox.Show($"Unable to open file:\n{ex.Message}");

                }
            }
        }

        private bool CanCreateReachSpec()
        {
            //Validation
            if (CurrentLoadedExport == null) return false;
            IMEPackage packageToValidateAgainst = ToExternalNodeChecked ? ExternalFile : CurrentLoadedExport.FileRef;
            if (CurrentLoadedExport != null && int.TryParse(CreateReachSpecDestination_TextBox.Text, out int destIndex) &&
                packageToValidateAgainst != null && packageToValidateAgainst.IsUExport(destIndex))
            {
                //Parse
                ExportEntry destExport = packageToValidateAgainst.GetUExport(destIndex);
                var uguid = PathEdUtils.GetNavGUID(destExport);

                if (ToExternalNodeChecked || (ParentWindow != null && ParentWindow.ActiveNodes.Contains(destExport)))
                {
                    var destPoint = PathEdUtils.GetLocation(destExport);

                    if (destPoint != null)
                    {
                        var pathlist = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                        if (pathlist != null)
                        {
                            foreach (ObjectProperty specref in pathlist)
                            {
                                if (specref.Value != 0) //0 might be there if user manually added item to the list. we should ignore it.
                                {
                                    ExportEntry spec = CurrentLoadedExport.FileRef.GetUExport(specref.Value);
                                    StructProperty outgoingEndStructProp = spec.GetProperty<StructProperty>("End"); //Embeds END
                                    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.GetProp<ObjectProperty>(PathEdUtils.GetReachSpecEndName(spec)); //END  

                                    if (outgoingSpecEndProp.Value != 0)
                                    {
                                        if (outgoingSpecEndProp.Value == destIndex)
                                        {
                                            return false; //DUPLICATE SPEC
                                        }
                                    }
                                    else
                                    {
                                        //COMPARE GUIDS
                                        var guid = outgoingEndStructProp.GetProp<StructProperty>("Guid");
                                        var endGuid = new FGuid(guid);
                                        if (endGuid == uguid)
                                        {
                                            return false; //DUPLICATE SPEC
                                        }
                                    }
                                }


                            }
                        }
                        var sourcePoint = PathEdUtils.GetLocation(CurrentLoadedExport);
                        double distance = sourcePoint.getDistanceToOtherPoint(destPoint);

                        if (distance > 0.01)
                        {
                            return CreateReachSpecSize_ComboBox.SelectedIndex >= 0 && CreateReachspecType_ComboBox.SelectedIndex >= 0;
                        }
                    }
                }
            }
            return false;
        }




        private void CreateReachSpec()
        {
            if (int.TryParse(CreateReachSpecDestination_TextBox.Text, out int destIndex))
            {
                if (ToExternalNodeChecked)
                {
                    //nested because we don't want to switch to the other case if this is null
                    if (ExternalFile != null)
                    {
                        ExportEntry destExport = ExternalFile.GetUExport(destIndex);

                        var navguid = destExport.GetProperty<StructProperty>("NavGuid");

                        PathEdUtils.CreateReachSpec(CurrentLoadedExport, CreateReturningReachSpec, destExport, (string)CreateReachspecType_ComboBox.SelectedItem, (ReachSpecSize)CreateReachSpecSize_ComboBox.SelectedItem, navguid.Properties);
                    }

                }
                else
                {
                    //Parse
                    ExportEntry destExport = CurrentLoadedExport.FileRef.GetUExport(destIndex);
                    PathEdUtils.CreateReachSpec(CurrentLoadedExport, CreateReturningReachSpec, destExport, (string)CreateReachspecType_ComboBox.SelectedItem, (ReachSpecSize)CreateReachSpecSize_ComboBox.SelectedItem);
                }
            }
        }

        public override bool CanParse(ExportEntry export) => true;

        public override void LoadExport(ExportEntry export)
        {
            AllowChanges = false;
            CurrentLoadedExport = export;
            var props = export.GetProperties();

            //Node size
            AvailableNodeSizes.RemoveRange(AvailableNodeSizes.Where(x => x.CustomSized).ToList());
            StructProperty maxPathSize = props.GetProp<StructProperty>("MaxPathSize");
            if (maxPathSize != null)
            {
                float height = maxPathSize.GetProp<FloatProperty>("Height");
                float radius = maxPathSize.GetProp<FloatProperty>("Radius");
                var nodeSize = new NodeSize
                {
                    Header = "Current size",
                    NodeRadius = (int)radius,
                    NodeHeight = (int)height
                };
                if (!AvailableNodeSizes.Contains(nodeSize))
                {
                    AvailableNodeSizes.Insert(0, nodeSize);
                }
                NodeSize_ComboBox.SelectedIndex = AvailableNodeSizes.IndexOf(nodeSize);
                //exportTitleLabel.Text += " - " + radius + "x" + height;
                //pathNodeSizeComboBox.SelectedIndex = findClosestNextSizeIndex((int)radius, (int)height);
                //pathNodeSizeComboBox.Enabled = true;
            }


            //Reachspecs
            ReachSpecs.ClearEx();

            var PathList = props.GetProp<ArrayProperty<ObjectProperty>>("PathList");
            if (PathList != null)
            {
                foreach (ObjectProperty prop in PathList)
                {
                    if (prop.Value == 0) { continue; } //unassigned, will cause issue in game, but will be better for editor to not throw errors
                    var spec = new ReachSpec();
                    ExportEntry outgoingSpec = export.FileRef.GetUExport(prop.Value);



                    spec.SpecExport = outgoingSpec;
                    spec.StartNode = export;
                    StructProperty outgoingEndStructProp = outgoingSpec.GetProperty<StructProperty>("End"); //Embeds END
                    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(PathEdUtils.GetReachSpecEndName(outgoingSpec)); //END                    
                    if (outgoingSpecEndProp != null && outgoingSpecEndProp.Value - 1 > 0)
                    {
                        spec.EndNode = export.FileRef.GetUExport(outgoingSpecEndProp.Value);
                    }
                    else
                    {
                        spec.ExternalTarget = true;
                    }

                    IntProperty radius = outgoingSpec.GetProperty<IntProperty>("CollisionRadius");
                    IntProperty height = outgoingSpec.GetProperty<IntProperty>("CollisionHeight");

                    spec.SpecSize = new ReachSpecSize
                    {
                        Header = null,
                        SpecRadius = radius,
                        SpecHeight = height
                    };
                    ReachSpecs.Add(spec);
                }
            }
            RecalculateDestinationUI();
            AllowChanges = true;
        }


        public override void UnloadExport()
        {
            AllowChanges = false;
            NewReachSpecDistance = "No node selected";
            NewReachSpecDirectionX = "No node selected";
            NewReachSpecDirectionY = "No node selected";
            NewReachSpecDirectionZ = "No node selected";
            ReachSpecs.ClearEx();
            CurrentLoadedExport = null;
        }

        public override void Dispose()
        {
            ParentWindow = null;
            ExternalFile?.Release();
        }

        public void SetDestinationNode(int destinationUIndex)
        {
            if (CurrentLoadedExport != null && CurrentLoadedExport.UIndex != destinationUIndex)
            {
                CreateReachSpecDestination_TextBox.Text = destinationUIndex.ToString();
                RecalculateDestinationUI();
            }
        }


        private void ReachSpecs_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshSelectedReachSpec();
            if (ReachableNodes_ComboBox.SelectedItem is ReachSpec selectedSpec)
            {
                if (ParentWindow != null && !selectedSpec.ExternalTarget)
                {
                    //Parse
                    ParentWindow.FocusNode(selectedSpec.EndNode, false, 500);
                }
            }
            else
            {
                ReachableNodes_ComboBox.SelectedItem = null;
            }
            OnPropertyChanged(nameof(ReachSpecSelectedInList)); //fire off update for this property
        }

        private void RefreshSelectedReachSpec()
        {
            bool preAllow = AllowChanges;
            AllowChanges = false;
            ReachSpec selectedSpec = ReachableNodes_ComboBox.SelectedItem as ReachSpec;
            AvailableReachSpecSizes.RemoveRange(AvailableReachSpecSizes.Where(x => x.CustomSized).ToList());
            ReachSpecConnection_Panel.IsEnabled = selectedSpec != null;
            if (selectedSpec != null)
            {
                var props = selectedSpec.SpecExport.GetProperties();
                IntProperty radius = props.GetProp<IntProperty>("CollisionRadius");
                IntProperty height = props.GetProp<IntProperty>("CollisionHeight");

                if (radius != null && height != null)
                {
                    string destNode = selectedSpec.ExternalTarget ? "External node" : $"{selectedSpec.EndNode.ObjectName.Instanced}";
                    ReachSpecSizeToText = "ReachSpec size to " + destNode;
                    var specSize = new ReachSpecSize
                    {
                        Header = "Current size",
                        SpecRadius = radius,
                        SpecHeight = height,
                        CustomSized = true
                    };

                    if (!AvailableReachSpecSizes.Contains(specSize))
                    {
                        AvailableReachSpecSizes.Insert(0, specSize);
                    }

                    ReachSpecSize_ComboBox.SelectedIndex = AvailableReachSpecSizes.IndexOf(specSize);
                }

                StructProperty outgoingEndStructProp = props.GetProp<StructProperty>("End"); //Embeds END
                DestinationNavGUIDText = new FGuid(outgoingEndStructProp.GetProp<StructProperty>("Guid")).ToString();
            }
            else
            {
                ReachSpecSizeToText = "Select a reachspec above";
            }
            AllowChanges = preAllow;
        }

        private void CreateReachSpecDestination_OnKeyUp(object sender, KeyEventArgs e)
        {
            RecalculateDestinationUI();
        }

        private void RecalculateDestinationUI()
        {
            var DestinationPackageToUse = ToExternalNodeChecked ? ExternalFile : CurrentLoadedExport.FileRef;
            if (int.TryParse(CreateReachSpecDestination_TextBox.Text, out int destIndex) && DestinationPackageToUse.IsUExport(destIndex))
            {
                //Parse
                ExportEntry destExport = DestinationPackageToUse.GetUExport(destIndex);
                if (ToExternalNodeChecked || (ParentWindow != null && ParentWindow.ActiveNodes.Contains(destExport)))
                {
                    var destPoint = PathEdUtils.GetLocation(destExport);

                    if (destPoint != null)
                    {
                        var sourcePoint = PathEdUtils.GetLocation(CurrentLoadedExport);
                        double distance = sourcePoint.getDistanceToOtherPoint(destPoint);

                        //Calculate direction vectors
                        if (distance != 0)
                        {
                            DestinationNodeName = $"{destExport.ObjectName.Instanced}";

                            float dirX = (float)((destPoint.X - sourcePoint.X) / distance);
                            float dirY = (float)((destPoint.Y - sourcePoint.Y) / distance);
                            float dirZ = (float)((destPoint.Z - sourcePoint.Z) / distance);

                            DestinationNodeName = $"{destExport.ObjectName.Instanced}";
                            NewReachSpecDistance = "Distance: " + distance.ToString("0.##");
                            NewReachSpecDirectionX = "Direction X: " + dirX.ToString("0.#####");
                            NewReachSpecDirectionY = "Direction Y: " + dirY.ToString("0.#####");
                            NewReachSpecDirectionZ = "Direction Z: " + dirZ.ToString("0.#####");
                        }
                        else
                        {
                            //Distance 0
                            DestinationNodeName = $"{destExport.ObjectName.Instanced}";
                            NewReachSpecDistance = "Distance: 0 - Move node";
                            SetDirectionsNA();
                        }
                    }
                    else
                    {
                        //Does not have location
                        DestinationNodeName = "Not a valid node";
                        NewReachSpecDistance = "Distance: N/A";
                        SetDirectionsNA();
                    }
                }
                else
                {
                    //Not in level
                    DestinationNodeName = "Export not part of level";
                    NewReachSpecDistance = "Distance: N/A";
                    SetDirectionsNA();
                }
            }
            else
            {
                //invalid input
                DestinationNodeName = "Not a valid node";
                NewReachSpecDistance = "Distance: N/A";
                SetDirectionsNA();
            }

            void SetDirectionsNA()
            {
                NewReachSpecDirectionX = "Direction X: N/A";
                NewReachSpecDirectionY = "Direction Y: N/A";
                NewReachSpecDirectionZ = "Direction Z: N/A";
            }
        }

        private void CreateReachSpecDestination_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = (TextBox)sender;
            // Use SelectionStart property to find the caret position.
            // Insert the previewed text into the existing text in the textbox.
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            // If parsing is successful, set Handled to false
            if (ToExternalNodeChecked)
            {
                e.Handled = !(int.TryParse(fullText, out int val) && ExternalFile != null && ExternalFile.IsUExport(val));
            }
            else
            {
                e.Handled = !(int.TryParse(fullText, out int val) && CurrentLoadedExport.FileRef.IsUExport(val));
            }
        }

        private void ReachSpecsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            ParentWindow ??= Window.GetWindow(this) as PathfindingEditorWindow;
        }

        private void FocusDestinationNode_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(CreateReachSpecDestination_TextBox.Text, out int destIndex) && CurrentLoadedExport.FileRef.IsUExport(destIndex))
            {
                ExportEntry destExport = CurrentLoadedExport.FileRef.GetUExport(destIndex);
                if (ParentWindow != null && ParentWindow.ActiveNodes.Contains(destExport))
                {
                    //Parse
                    ParentWindow.FocusNode(destExport, false);
                }
            }
        }



        [DebuggerDisplay("ReachSpec | {SpecExport.ObjectName.Instanced} outbound from {StartNode.UIndex}")]
        public class ReachSpec
        {
            public ReachSpecSize SpecSize { get; internal set; }
            public ExportEntry SpecExport { get; internal set; }
            public ExportEntry StartNode { get; internal set; }
            public ExportEntry EndNode { get; internal set; }
            public bool ExternalTarget { get; internal set; }
            public string DestinationTextUI => ExternalTarget ? "Ext" : EndNode.UIndex.ToString();

            public string DestinationTypeTextUI => ExternalTarget ? "External Node" : $"{EndNode.ObjectName.Instanced}";
        }

        [DebuggerDisplay("NodeSize | {Header} {NodeHeight}x{NodeRadius}")]
        public class NodeSize : NotifyPropertyChangedBase, IEquatable<NodeSize>
        {
            private string _header;
            public string Header
            {
                get => _header;
                set => SetProperty(ref _header, value);
            }

            private int _nodeRadius;
            public int NodeRadius
            {
                get => _nodeRadius;
                set => SetProperty(ref _nodeRadius, value);
            }

            private int _nodeHeight;
            public int NodeHeight
            {
                get => _nodeHeight;
                set => SetProperty(ref _nodeHeight, value);
            }

            public bool CustomSized;

            public NodeSize()
            {

            }

            public NodeSize(string header, int height, int radius, bool customsized = false)
            {
                Header = header;
                NodeHeight = height;
                NodeRadius = radius;
                CustomSized = customsized;
            }

            public bool Equals(NodeSize other)
            {
                if (other == null) return false;
                return NodeRadius == other.NodeRadius && NodeHeight == other.NodeHeight;
            }
        }

        private void ReachSpecSizeCombobox_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AllowChanges)
            {
                ReachSpecSize newSize = (ReachSpecSize)ReachSpecSize_ComboBox.SelectedItem;
                ReachSpec spec = (ReachSpec)ReachableNodes_ComboBox.SelectedItem;
                PropertyCollection props = spec.SpecExport.GetProperties();

                IntProperty radius = props.GetProp<IntProperty>("CollisionRadius");
                IntProperty height = props.GetProp<IntProperty>("CollisionHeight");

                if (radius != null && height != null)
                {
                    radius.Value = spec.SpecSize.SpecRadius = newSize.SpecRadius;
                    height.Value = spec.SpecSize.SpecHeight = newSize.SpecHeight;
                    spec.SpecExport.WriteProperties(props);
                    ParentWindow?.UpdateEdgesForCurrentNode();
                }
            }
        }

        private void NodeSizeComboBox_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AllowChanges)
            {
                NodeSize size = (NodeSize)NodeSize_ComboBox.SelectedItem;

                PropertyCollection props = CurrentLoadedExport.GetProperties();
                StructProperty maxPathSize = props.GetProp<StructProperty>("MaxPathSize");
                if (maxPathSize != null)
                {
                    FloatProperty height = maxPathSize.GetProp<FloatProperty>("Height");
                    FloatProperty radius = maxPathSize.GetProp<FloatProperty>("Radius");
                    if (radius != null && height != null)
                    {
                        height.Value = size.NodeHeight;
                        radius.Value = size.NodeRadius;
                        CurrentLoadedExport.WriteProperties(props);
                        RefreshSelectedReachSpec();

                    }
                }
            }
        }

        private void ExternalFileName_TextBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //Used to double click to open in a new pathfinding editor window
            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2)
            {
                if (ExternalFile != null)
                {
                    var pe = new PathfindingEditorWindow(ExternalFile.FilePath);
                    pe.Show();
                }
            }
        }
    }
}