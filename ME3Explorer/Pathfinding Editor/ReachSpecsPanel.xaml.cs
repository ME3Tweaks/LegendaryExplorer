using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public ReachSpecsPanel()
        {
            DataContext = this;
            InitializeComponent();
        }

        private string _nodeName;
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

        private string _reachSpecSizeToText;
        public string ReachSpecSizeToText
        {
            get => _reachSpecSizeToText;
            set => SetProperty(ref _reachSpecSizeToText, value);
        }

        public ObservableCollectionExtended<ReachSpec> ReachSpecs { get; set; } = new ObservableCollectionExtended<ReachSpec>();

        //Todo: change to proper types
        public ObservableCollectionExtended<NodeSize> AvailableNodeSizes { get; set; } = new ObservableCollectionExtended<NodeSize>();
        public ObservableCollectionExtended<ReachSpecSize> AvailableReachSpecSizes { get; set; } = new ObservableCollectionExtended<ReachSpecSize>();

        public override bool CanParse(IExportEntry export)
        {
            return true;
        }

        public override void LoadExport(IExportEntry export)
        {
            CurrentLoadedExport = export;
            var props = export.GetProperties();

            NodeName = $"{export.ObjectName}_{export.indexValue}";
            NodeNameSubText = $"Export {export.UIndex}";

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
            CurrentLoadedExport = null;
        }

        public override void Dispose()
        {

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

            private string _header;
            public string Header
            {
                get => _header;
                set => SetProperty(ref _header, value);
            }

            private int _specWidth;
            public int SpecWidth
            {
                get => _specWidth;
                set => SetProperty(ref _specWidth, value);
            }

            private int _specHeight;
            public int SpecHeight
            {
                get => _specHeight;
                set => SetProperty(ref _specHeight, value);
            }
        }

        private void ReachSpecs_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshSelectedReachSpec();
        }

        private void RefreshSelectedReachSpec()
        {
            AvailableReachSpecSizes.ClearEx();
            ReachSpec selectedSpec = ReachableNodes_ComboBox.SelectedItem as ReachSpec;
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
                        SpecWidth = radius,
                        SpecHeight = height
                    };
                    AvailableReachSpecSizes.Add(specSize);
                    ReachSpecSize_ComboBox.SelectedItem = specSize;
                }
            }
        }
    }
}
