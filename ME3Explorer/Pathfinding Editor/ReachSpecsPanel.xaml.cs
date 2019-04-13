using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ME3Explorer.Pathfinding_Editor
{
    /// <summary>
    /// Interaction logic for ReachSpecsPanel.xaml
    /// </summary>
    public partial class ReachSpecsPanel : ExportLoaderControl
    {
        public PathfindingEditorWPF ParentWindow;

        private string _reachSpecSizeToText;
        public string ReachSpecSizeToText
        {
            get => _reachSpecSizeToText;
            set => SetProperty(ref _reachSpecSizeToText, value);
        }

        public ObservableCollectionExtended<ReachSpec> ReachSpecs { get; } = new ObservableCollectionExtended<ReachSpec>();
        public ObservableCollectionExtended<NodeSize> AvailableNodeSizes { get; } = new ObservableCollectionExtended<NodeSize>();
        public ObservableCollectionExtended<ReachSpecSize> AvailableReachSpecSizes { get; } = new ObservableCollectionExtended<ReachSpecSize>();
        public ObservableCollectionExtended<ReachSpecSize> AvailableCreateReachSpecSizes { get; } = new ObservableCollectionExtended<ReachSpecSize>();
        public ObservableCollectionExtended<string> AvailableReachspecTypes { get; } = new ObservableCollectionExtended<string>();

        private string _destinationNodeName;
        private string _newReachSpecDistance;
        private string _newReachSpecDirectionX;
        private string _newReachSpecDirectionY;
        private string _newReachSpecDirectionZ;

        public string DestinationNodeName { get => _destinationNodeName; private set => SetProperty(ref _destinationNodeName, value); }
        public string NewReachSpecDistance { get => _newReachSpecDistance; private set => SetProperty(ref _newReachSpecDistance, value); }
        public string NewReachSpecDirectionX { get => _newReachSpecDirectionX; private set => SetProperty(ref _newReachSpecDirectionX, value); }
        public string NewReachSpecDirectionY { get => _newReachSpecDirectionY; private set => SetProperty(ref _newReachSpecDirectionY, value); }
        public string NewReachSpecDirectionZ { get => _newReachSpecDirectionZ; private set => SetProperty(ref _newReachSpecDirectionZ, value); }

        private bool _createReturningReachSpec = true;
        public bool CreateReturningReachSpec { get => _createReturningReachSpec; set => SetProperty(ref _createReturningReachSpec, value); }


        public ReachSpecsPanel()
        {
            DataContext = this;
            ReachSpecSizeToText = "Select a reachspec above";
            DestinationNodeName = "Enter a node index above";
            NewReachSpecDistance = "No node selected";
            NewReachSpecDirectionX = "No node selected";
            NewReachSpecDirectionY = "No node selected";
            NewReachSpecDirectionZ = "No node selected";
            AvailableReachspecTypes.AddRange(new string[] {
            "Engine.ReachSpec",
            "Engine.CoverSlipReachSpec",
            "Engine.CoverTurnReachSpec",
            "Engine.MantleReachSpec",
            "SFXGame.SFXBoostReachSpec",
            "SFXGame.SFXClimbWallReachSpec",
            "SFXGame.SFXLadderReachSpec",
            "SFXGame.SFXJumpDownReachSpec",
            "SFXGame.SFXLargeBoostReachSpec",
            "SFXGame.SFXLargeMantleReachSpec",
            "Engine.SlotToSlotReachSpec" });

            AvailableReachSpecSizes.Add(new ReachSpecSize("Banshee", ReachSpecSize.BANSHEE_HEIGHT, ReachSpecSize.BANSHEE_RADIUS));
            AvailableReachSpecSizes.Add(new ReachSpecSize("Bosses", ReachSpecSize.BOSS_HEIGHT, ReachSpecSize.BOSS_RADIUS));
            AvailableReachSpecSizes.Add(new ReachSpecSize("Minibosses", ReachSpecSize.MINIBOSS_HEIGHT, ReachSpecSize.MINIBOSS_RADIUS));
            AvailableReachSpecSizes.Add(new ReachSpecSize("Mooks", ReachSpecSize.MOOK_HEIGHT, ReachSpecSize.MOOK_RADIUS));

            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Banshee", ReachSpecSize.BANSHEE_HEIGHT, ReachSpecSize.BANSHEE_RADIUS));
            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Bosses", ReachSpecSize.BOSS_HEIGHT, ReachSpecSize.BOSS_RADIUS));
            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Minibosses", ReachSpecSize.MINIBOSS_HEIGHT, ReachSpecSize.MINIBOSS_RADIUS));
            AvailableCreateReachSpecSizes.Add(new ReachSpecSize("Mooks", ReachSpecSize.MOOK_HEIGHT, ReachSpecSize.MOOK_RADIUS));

            InitializeComponent();
            CreateReachspecType_ComboBox.SelectedIndex = 0;
            CreateReachSpecSize_ComboBox.SelectedIndex = 0;
            RefreshSelectedReachSpec();
        }

        public override bool CanParse(IExportEntry export)
        {
            return true;
        }

        public override void LoadExport(IExportEntry export)
        {
            CurrentLoadedExport = export;
            var props = export.GetProperties();

            //Node size
            AvailableNodeSizes.ClearEx();
            StructProperty maxPathSize = props.GetProp<StructProperty>("MaxPathSize");
            if (maxPathSize != null)
            {
                float height = maxPathSize.GetProp<FloatProperty>("Height");
                float radius = maxPathSize.GetProp<FloatProperty>("Radius");
                NodeSize nodeSize = new NodeSize
                {
                    Header = "Current size",
                    NodeWidth = (int)radius,
                    NodeHeight = (int)height
                };
                AvailableNodeSizes.Add(nodeSize);
                NodeSize_ComboBox.SelectedItem = nodeSize;
                //exportTitleLabel.Text += " - " + radius + "x" + height;
                //pathNodeSizeComboBox.SelectedIndex = findClosestNextSizeIndex((int)radius, (int)height);
                //pathNodeSizeComboBox.Enabled = true;
            }


            //Reachspecs
            ReachSpecs.ClearEx();

            ArrayProperty<ObjectProperty> PathList = props.GetProp<ArrayProperty<ObjectProperty>>("PathList");
            if (PathList != null)
            {
                foreach (ObjectProperty prop in PathList)
                {
                    if (prop.Value == 0) { continue; } //unassigned, will cause issue in game, but will be better for editor to not throw errors
                    ReachSpec spec = new ReachSpec();
                    IExportEntry outgoingSpec = export.FileRef.getUExport(prop.Value);

                    spec.SpecExport = outgoingSpec;
                    spec.StartNode = export;
                    StructProperty outgoingEndStructProp = outgoingSpec.GetProperty<StructProperty>("End"); //Embeds END
                    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(outgoingSpec)); //END                    
                    if (outgoingSpecEndProp != null && outgoingSpecEndProp.Value - 1 > 0)
                    {
                        spec.EndNode = export.FileRef.getUExport(outgoingSpecEndProp.Value);
                    }
                    else
                    {
                        spec.ExternalTarget = true;
                    }
                    ReachSpecs.Add(spec);
                }
            }
        }


        public override void UnloadExport()
        {
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
            ReachSpec selectedSpec = ReachableNodes_ComboBox.SelectedItem as ReachSpec;
            if (selectedSpec != null && !selectedSpec.ExternalTarget)
            {
                if (ParentWindow != null)
                {
                    //Parse
                    ParentWindow.FocusNode(selectedSpec.EndNode, false, 500);
                }
            }
        }

        private void RefreshSelectedReachSpec()
        {
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
                    string destNode = selectedSpec.ExternalTarget ? "External node" : $"{selectedSpec.EndNode.ObjectName}_{ selectedSpec.EndNode.UIndex}";
                    ReachSpecSizeToText = "ReachSpec size to " + destNode;
                    ReachSpecSize specSize = new ReachSpecSize
                    {
                        Header = "Current size",
                        SpecRadius = radius,
                        SpecHeight = height,
                        CustomSized = true
                    };
                    AvailableReachSpecSizes.Insert(0,specSize);
                    ReachSpecSize_ComboBox.SelectedItem = specSize;
                }
            }
            else
            {
                ReachSpecSizeToText = "Select a reachspec above";
            }
        }

        private void CreateReachSpecDestination_OnKeyUp(object sender, KeyEventArgs e)
        {
            RecalculateDestinationUI();
        }

        private void RecalculateDestinationUI()
        {
            if (int.TryParse(CreateReachSpecDestination_TextBox.Text, out int destIndex) && CurrentLoadedExport.FileRef.isUExport(destIndex))
            {
                //Parse
                IExportEntry destExport = CurrentLoadedExport.FileRef.getUExport(destIndex);
                if (ParentWindow != null && ParentWindow.ActiveNodes.Contains(destExport))
                {
                    var destPoint = PathfindingEditorWPF.GetLocation(destExport);

                    if (destPoint != null)
                    {
                        var sourcePoint = PathfindingEditorWPF.GetLocation(CurrentLoadedExport);
                        double distance = sourcePoint.getDistanceToOtherPoint(destPoint);

                        //Calculate direction vectors
                        if (distance != 0)
                        {
                            ReachSpecDestinationNode_TextBlock.Text = $"{destExport.ObjectName}_{destExport.indexValue}";

                            float dirX = (float)((destPoint.X - sourcePoint.X) / distance);
                            float dirY = (float)((destPoint.Y - sourcePoint.Y) / distance);
                            float dirZ = (float)((destPoint.Z - sourcePoint.Z) / distance);

                            DestinationNodeName = $"{destExport.ObjectName}_{destExport.indexValue}";
                            NewReachSpecDistance = "Distance: " + distance.ToString("0.##");
                            NewReachSpecDirectionX = "Direction X: " + dirX.ToString("0.#####");
                            NewReachSpecDirectionY = "Direction Y: " + dirY.ToString("0.#####");
                            NewReachSpecDirectionZ = "Direction Z: " + dirZ.ToString("0.#####");
                        }
                        else
                        {
                            //Distance 0
                            DestinationNodeName = $"{destExport.ObjectName}_{destExport.indexValue}";
                            NewReachSpecDistance = "Distance: 0 - Move node";
                            NewReachSpecDirectionX = "Direction X: N/A";
                            NewReachSpecDirectionY = "Direction Y: N/A";
                            NewReachSpecDirectionZ = "Direction Z: N/A";
                        }
                    }
                    else
                    {
                        //Does not have location
                        DestinationNodeName = $"Not a valid node";
                        NewReachSpecDistance = "Distance: N/A";
                        NewReachSpecDirectionX = "Direction X: N/A";
                        NewReachSpecDirectionY = "Direction Y: N/A";
                        NewReachSpecDirectionZ = "Direction Z: N/A";
                    }
                }
                else
                {
                    //Not in level
                    DestinationNodeName = $"Export not part of level";
                    NewReachSpecDistance = "Distance: N/A";
                    NewReachSpecDirectionX = "Direction X: N/A";
                    NewReachSpecDirectionY = "Direction Y: N/A";
                    NewReachSpecDirectionZ = "Direction Z: N/A";
                }
            }
            else
            {
                //invalid input
                DestinationNodeName = $"Not a valid node";
                NewReachSpecDistance = "Distance: N/A";
                NewReachSpecDirectionX = "Direction X: N/A";
                NewReachSpecDirectionY = "Direction Y: N/A";
                NewReachSpecDirectionZ = "Direction Z: N/A";
            }
        }

        private void CreateReachSpecDestination_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            // Use SelectionStart property to find the caret position.
            // Insert the previewed text into the existing text in the textbox.
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            // If parsing is successful, set Handled to false
            e.Handled = !(int.TryParse(fullText, out int val) && val > 0 && val <= CurrentLoadedExport.FileRef.ExportCount);
        }

        private void ReachSpecsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (ParentWindow == null)
            {
                ParentWindow = Window.GetWindow(this) as PathfindingEditorWPF;
            }
        }

        private void FocusDestinationNode_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(CreateReachSpecDestination_TextBox.Text, out int destIndex) && CurrentLoadedExport.FileRef.isUExport(destIndex))
            {
                IExportEntry destExport = CurrentLoadedExport.FileRef.getUExport(destIndex);
                if (ParentWindow != null && ParentWindow.ActiveNodes.Contains(destExport))
                {
                    //Parse
                    ParentWindow.FocusNode(destExport, false);
                }
            }
        }

        private void createReachSpec(IExportEntry startNode, bool createTwoWay, int destinationIndex, string reachSpecClass, int size, int destinationType)
        {
            IExportEntry reachSpectoClone = CurrentLoadedExport.FileRef.Exports.FirstOrDefault(x => x.ClassName == "ReachSpec");


            /*if (destinationType == 1) //EXTERNAL
            {
                //external node

                //Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
                int outgoingSpec = pcc.ExportCount;
                int incomingSpec = pcc.ExportCount + 1;


                if (reachSpectoClone != null)
                {
                    pcc.addExport(reachSpectoClone.Clone()); //outgoing

                    IExportEntry outgoingSpecExp = pcc.Exports[outgoingSpec]; //cloned outgoing
                    ImportEntry reachSpecClassImp = getOrAddImport(reachSpecClass); //new class type.

                    outgoingSpecExp.idxClass = reachSpecClassImp.UIndex;
                    outgoingSpecExp.idxObjectName = reachSpecClassImp.idxObjectName;

                    ObjectProperty outgoingSpecStartProp = outgoingSpecExp.GetProperty<ObjectProperty>("Start"); //START
                    StructProperty outgoingEndStructProp = outgoingSpecExp.GetProperty<StructProperty>("End"); //Embeds END
                    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(outgoingSpecExp)); //END
                    outgoingSpecStartProp.Value = startNode.UIndex;
                    outgoingSpecEndProp.Value = 0; //we will have to set the GUID - maybe through form or something


                    //Add to source node prop
                    ArrayProperty<ObjectProperty> PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    byte[] memory = startNode.Data;
                    memory = addObjectArrayLeaf(memory, (int)PathList.ValueOffset, outgoingSpecExp.UIndex);
                    startNode.Data = memory;
                    outgoingSpecExp.WriteProperty(outgoingSpecStartProp);
                    outgoingSpecExp.WriteProperty(outgoingEndStructProp);

                    //Write Spec Size
                    int radVal = -1;
                    int heightVal = -1;

                    System.Drawing.Point sizePair = PathfindingNodeInfoPanel.getDropdownSizePair(size);
                    radVal = sizePair.X;
                    heightVal = sizePair.Y;
                    setReachSpecSize(outgoingSpecExp, radVal, heightVal);

                    //Reindex reachspecs.
                    reindexObjectsWithName(reachSpecClass);
                }
            }
            else
            {*/
            //Debug.WriteLine("Source Node: " + startNode.Index);

            //Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
            //int outgoingSpec = pcc.ExportCount;
            //int incomingSpec = pcc.ExportCount + 1;


            if (reachSpectoClone != null)
            {
                IExportEntry destNode = CurrentLoadedExport.FileRef.getUExport(destinationIndex);
                IExportEntry outgoingSpec = reachSpectoClone.Clone();
                CurrentLoadedExport.FileRef.addExport(outgoingSpec);
                IExportEntry incomingSpec = null;
                if (createTwoWay)
                {
                    incomingSpec = reachSpectoClone.Clone();
                    CurrentLoadedExport.FileRef.addExport(incomingSpec);
                }

                ImportEntry reachSpecClassImp = getOrAddImport(reachSpecClass); //new class type.

                outgoingSpec.idxClass = reachSpecClassImp.UIndex;
                outgoingSpec.idxObjectName = reachSpecClassImp.idxObjectName;

                if (reachSpecClass == "Engine.SlotToSlotReachSpec")
                {
                    var props = outgoingSpec.GetProperties();
                    props.Add(new ByteProperty(1, "SpecDirection")); //We might need to find a way to support this edit
                    outgoingSpec.WriteProperties(props);
                }

                //Debug.WriteLine("Outgoing UIndex: " + outgoingSpecExp.UIndex);

                ObjectProperty outgoingSpecStartProp = outgoingSpec.GetProperty<ObjectProperty>("Start"); //START
                StructProperty outgoingEndStructProp = outgoingSpec.GetProperty<StructProperty>("End"); //Embeds END
                ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(outgoingSpec)); //END
                outgoingSpecStartProp.Value = startNode.UIndex;
                outgoingSpecEndProp.Value = destNode.UIndex;


                //Add to source node prop
                ArrayProperty<ObjectProperty> PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                PathList.Add(new ObjectProperty(outgoingSpec.UIndex));
                startNode.WriteProperty(PathList);

                //Write Spec Size
                int radVal = -1;
                int heightVal = -1;

                System.Drawing.Point sizePair = PathfindingNodeInfoPanel.getDropdownSizePair(size);
                radVal = sizePair.X;
                heightVal = sizePair.Y;
                //setReachSpecSize(outgoingSpecExp, radVal, heightVal);


                //if (createTwoWay)
                //{
                //    IExportEntry incomingSpecExp = CurrentLoadedExport.FileRef.[incomingSpec];
                //    incomingSpecExp.idxClass = reachSpecClassImp.UIndex;
                //    incomingSpecExp.idxObjectName = reachSpecClassImp.idxObjectName;

                //    if (reachSpecClass == "Engine.SlotToSlotReachSpec")
                //    {
                //        var props = incomingSpecExp.GetProperties();
                //        props.Add(new ByteProperty(2, "SpecDirection"));
                //        incomingSpecExp.WriteProperties(props);
                //    }

                //    ObjectProperty incomingSpecStartProp = incomingSpecExp.GetProperty<ObjectProperty>("Start"); //START
                //    StructProperty incomingEndStructProp = incomingSpecExp.GetProperty<StructProperty>("End"); //Embeds END
                //    ObjectProperty incomingSpecEndProp = incomingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(incomingSpecExp)); //END

                //    incomingSpecStartProp.Value = destNode.UIndex;//Uindex
                //    incomingSpecEndProp.Value = startNode.UIndex;


                //    //Add to source node prop
                //    ArrayProperty<ObjectProperty> DestPathList = pcc.Exports[destinationIndex].GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                //    memory = destNode.Data;
                //    memory = addObjectArrayLeaf(memory, (int)DestPathList.ValueOffset, incomingSpecExp.UIndex);
                //    destNode.Data = memory;
                //    //destNode.WriteProperty(DestPathList);
                //    incomingSpecExp.WriteProperty(incomingSpecStartProp);
                //    incomingSpecExp.WriteProperty(incomingEndStructProp);
                //    setReachSpecSize(incomingSpecExp, radVal, heightVal);


                //    //verify
                //    destNode.GetProperties();
                //}
                //incomingSpecStartProp.Value =

                //Reindex reachspecs.
                SharedPathfinding.ReindexMatchingObjects(outgoingSpec);
            }
        }

        private ImportEntry getOrAddImport(string importFullName)
        {
            foreach (ImportEntry imp in CurrentLoadedExport.FileRef.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    return imp;
                }
            }

            //Import doesn't exist, so we're gonna need to add it
            //But first we need to figure out what needs to be added.
            string[] importParts = importFullName.Split('.');
            List<int> upstreamLinks = new List<int>(); //0 = top level, 1 = next level... n = what we wanted to import
            int upstreamCount = 1;

            ImportEntry upstreamImport = null;
            while (upstreamCount < importParts.Count())
            {
                string upstream = string.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                foreach (ImportEntry imp in CurrentLoadedExport.FileRef.Imports)
                {
                    if (imp.GetFullPath == upstream)
                    {
                        upstreamImport = imp;
                        break;
                    }
                }

                if (upstreamImport != null)
                {
                    break;
                }
                upstreamCount++;
            }

            if (upstreamImport == null)
            {
                //There is no top level import, which is very unlikely (engine, sfxgame)
                return null;
            }

            //Have an upstream import, now we need to add downstream imports.
            ImportEntry mostdownstreamimport = null;

            while (upstreamCount > 0)
            {
                upstreamCount--;
                string fullobjectname = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                Dictionary<string, string> importdbinfo = SharedPathfinding.ImportClassDB[fullobjectname];

                int downstreamName = CurrentLoadedExport.FileRef.FindNameOrAdd(importParts[importParts.Count() - upstreamCount - 1]);
                Debug.WriteLine(CurrentLoadedExport.FileRef.Names[downstreamName]);
                int downstreamLinkIdx = upstreamImport.UIndex;
                Debug.WriteLine(upstreamImport.GetFullPath);

                int downstreamPackageName = CurrentLoadedExport.FileRef.FindNameOrAdd(importdbinfo["packagefile"]);
                int downstreamClassName = CurrentLoadedExport.FileRef.FindNameOrAdd(importdbinfo["class"]);

                //ImportEntry classImport = getOrAddImport();
                //int downstreamClass = 0;
                //if (classImport != null) {
                //    downstreamClass = classImport.UIndex; //no recursion pls
                //} else
                //{
                //    throw new Exception("No class was found for importing");
                //}

                mostdownstreamimport = new ImportEntry(CurrentLoadedExport.FileRef);
                mostdownstreamimport.idxLink = downstreamLinkIdx;
                mostdownstreamimport.idxClassName = downstreamClassName;
                mostdownstreamimport.idxObjectName = downstreamName;
                mostdownstreamimport.idxPackageFile = downstreamPackageName;
                CurrentLoadedExport.FileRef.addImport(mostdownstreamimport);
                upstreamImport = mostdownstreamimport;
            }
            return mostdownstreamimport;
        }

        public class ReachSpec
        {
            public IExportEntry SpecExport { get; internal set; }
            public IExportEntry StartNode { get; internal set; }
            public IExportEntry EndNode { get; internal set; }
            public bool ExternalTarget { get; internal set; }
            public string DestinationTextUI
            {
                get
                {
                    return ExternalTarget ? "Ext" : EndNode.UIndex.ToString();
                }
            }
            public string DestinationTypeTextUI
            {
                get
                {
                    return ExternalTarget ? "External Node" : $"{EndNode.ObjectName}_{ EndNode.indexValue}";
                }
            }
        }

        public class NodeSize : NotifyPropertyChangedBase
        {
            private string _header;
            public string Header
            {
                get => _header;
                set => SetProperty(ref _header, value);
            }

            private int _nodeWidth;
            public int NodeWidth
            {
                get => _nodeWidth;
                set => SetProperty(ref _nodeWidth, value);
            }

            private int _nodeHeight;
            public int NodeHeight
            {
                get => _nodeHeight;
                set => SetProperty(ref _nodeHeight, value);
            }
        }

        public class ReachSpecSize : NotifyPropertyChangedBase
        {
            public const int MOOK_RADIUS = 34;
            public const int MOOK_HEIGHT = 90;
            public const int MINIBOSS_RADIUS = 105;
            public const int MINIBOSS_HEIGHT = 145;
            public const int BOSS_RADIUS = 140;
            public const int BOSS_HEIGHT = 195;
            public const int BANSHEE_RADIUS = 50;
            public const int BANSHEE_HEIGHT = 125;

            public bool CustomSized;

            public ReachSpecSize()
            {

            }

            public ReachSpecSize(string header, int height, int radius, bool customsized = false)
            {
                Header = header;
                SpecHeight = height;
                SpecRadius = radius;
                CustomSized = customsized;
            }

            private string _header;
            public string Header
            {
                get => _header;
                set => SetProperty(ref _header, value);
            }

            private int _specRadius;
            public int SpecRadius
            {
                get => _specRadius;
                set => SetProperty(ref _specRadius, value);
            }

            private int _specHeight;
            public int SpecHeight
            {
                get => _specHeight;
                set => SetProperty(ref _specHeight, value);
            }
        }
    }
}