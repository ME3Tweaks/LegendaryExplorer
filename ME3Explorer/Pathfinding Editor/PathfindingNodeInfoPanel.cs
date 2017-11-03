using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME3Explorer.Pathfinding_Editor
{
    public partial class PathfindingNodeInfoPanel : UserControl
    {

        private IExportEntry export;
        private List<int> combatZones;
        private List<int> reachSpecs;
        private bool AllowChanges = true;
        public PathfindingEditor PathfindingEditorInstance { get; private set; }

        public PathfindingNodeInfoPanel()
        {
            InitializeComponent();
        }

        public void PassPathfindingNodeEditorIn(PathfindingEditor PathfindingEditorInstance)
        {
            this.PathfindingEditorInstance = PathfindingEditorInstance;
        }

        public void LoadExport(IExportEntry export)
        {
            this.export = export;
            reachableNodesList.Items.Clear();
            sfxCombatZoneList.Items.Clear();

            combatZones = new List<int>();
            reachSpecs = new List<int>();

            var props = export.GetProperties();

            exportTitleLabel.Text = export.ObjectName + "_" + export.indexValue;

            //Calculate reachspecs
            ArrayProperty<ObjectProperty> PathList = export.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
            if (PathList != null)
            {
                foreach (ObjectProperty prop in PathList)
                {
                    IExportEntry outgoingSpec = export.FileRef.Exports[prop.Value - 1];
                    StructProperty outgoingEndStructProp = outgoingSpec.GetProperty<StructProperty>("End"); //Embeds END
                    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>("Actor"); //END                    
                    if (outgoingSpecEndProp.Value - 1 > 0)
                    {

                        IExportEntry endNode = export.FileRef.Exports[outgoingSpecEndProp.Value - 1];
                        string targetNodeName = endNode.ObjectName + "_" + endNode.indexValue;
                        reachableNodesList.Items.Add(endNode.Index + " " + targetNodeName + " via " + outgoingSpec.ObjectName + "_" + outgoingSpec.indexValue + " (" + outgoingSpec.Index + ")");
                        reachSpecs.Add(outgoingSpec.Index);
                    }
                    else
                    {
                        reachableNodesList.Items.Add("External File Node via " + outgoingSpec.ObjectName + "_" + outgoingSpec.indexValue + " (" + outgoingSpec.Index + ")");
                        reachSpecs.Add(outgoingSpec.Index);
                    }
                }
            }


            //Calculate SFXCombatZones
            ArrayProperty<StructProperty> volumes = props.GetProp<ArrayProperty<StructProperty>>("Volumes");
            if (volumes != null)
            {
                foreach (StructProperty volume in volumes)
                {
                    ObjectProperty actorRef = volume.GetProp<ObjectProperty>("Actor");
                    if (actorRef != null && actorRef.Value > 0)
                    {
                        IExportEntry combatZoneExport = export.FileRef.Exports[actorRef.Value - 1];
                        combatZones.Add(combatZoneExport.Index);
                        sfxCombatZoneList.Items.Add(combatZoneExport.ObjectName + "_" + combatZoneExport.indexValue + "(" + combatZoneExport.Index + ")");

                    }
                }
            }
        }

        private void sfxCombatZoneSelectionChanged(object sender, EventArgs e)
        {
            int n = sfxCombatZoneList.SelectedIndex;
            if (n < 0 || n >= combatZones.Count)
                return;

            if (PathfindingEditorInstance != null)
            {
                PathfindingEditorInstance.ActiveCombatZoneExportIndex = combatZones[n];
                PathfindingEditorInstance.RefreshView();
                PathfindingEditorInstance.graphEditor.Invalidate(); //force repaint
            }

        }

        private void reachSpecSelection_Changed(object sender, EventArgs e)
        {
            int n = reachableNodesList.SelectedIndex;
            if (n < 0 || n >= reachSpecs.Count)
            {
                reachSpecDestLabel.Text = "No ReachSpec selected";
                reachSpecSizeLabel.Text = "ReachSpec Size";

                return;
            }

            AllowChanges = false;
            IExportEntry reachSpec = export.FileRef.Exports[reachSpecs[n]];
            Unreal.PropertyCollection props = reachSpec.GetProperties();


            StructProperty outgoingEndStructProp = props.GetProp<StructProperty>("End"); //Embeds END
            ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>("Actor"); //END                    
            if (outgoingSpecEndProp.Value - 1 > 0)
            {

                IExportEntry endNode = export.FileRef.Exports[outgoingSpecEndProp.Value - 1];
                reachSpecDestLabel.Text = endNode.ObjectName + "_" + endNode.indexValue;
            }

            IntProperty radius = props.GetProp<IntProperty>("CollisionRadius");
            IntProperty height = props.GetProp<IntProperty>("CollisionHeight");

            if (radius != null && height != null)
            {
                reachSpecSizeLabel.Text = "ReachSpec Size: " + radius.Value + "x" + height.Value;

                if (radius >= 34 && height >= 64)
                {
                    reachSpecSizeSelector.SelectedIndex = 0;
                }

                if (radius >= 90 && height >= 130)
                {
                    reachSpecSizeSelector.SelectedIndex = 1;
                }

                if (radius >= 135 && height >= 190)
                {
                    reachSpecSizeSelector.SelectedIndex = 2;
                }
            }
            AllowChanges = true;
        }

        private void reachspecSizeBox_Changed(object sender, EventArgs e)
        {
            int n = reachableNodesList.SelectedIndex;
            if (n < 0 || n >= reachSpecs.Count)
            {
                return;
            }

            if (AllowChanges)
            {
                int selectedIndex = reachSpecSizeSelector.SelectedIndex;

                IExportEntry reachSpec = export.FileRef.Exports[reachSpecs[n]];
                Unreal.PropertyCollection props = reachSpec.GetProperties();

                IntProperty radius = props.GetProp<IntProperty>("CollisionRadius");
                IntProperty height = props.GetProp<IntProperty>("CollisionHeight");

                if (radius != null && height != null)
                {
                    int radVal = -1;
                    int heightVal = -1;

                    switch (selectedIndex)
                    {
                        case 0:
                            radVal = 45;
                            heightVal = 64;
                            break;
                        case 1:
                            radVal = 105;
                            heightVal = 140;
                            break;
                        case 2:
                            radVal = 145;
                            heightVal = 190;
                            break;
                    }

                    radius.Value = radVal;
                    height.Value = heightVal;
                    reachSpec.WriteProperties(props);
                    reachSpecSelection_Changed(null, null);
                }
            }
        }
    }
}
