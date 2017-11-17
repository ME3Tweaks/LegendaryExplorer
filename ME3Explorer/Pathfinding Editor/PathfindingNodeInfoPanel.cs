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

        public const int MOOK_RADIUS = 34;
        public const int MOOK_HEIGHT = 90;
        public const int MINIBOSS_RADIUS = 105;
        public const int MINIBOSS_HEIGHT = 145;
        public const int BOSS_RADIUS = 140;
        public const int BOSS_HEIGHT = 195;
        public const int BANSHEE_RADIUS = 50;
        public const int BANSHEE_HEIGHT = 125;



        public PathfindingNodeInfoPanel()
        {
            InitializeComponent();
            reachSpecSizeSelector.Items.Clear();
            pathNodeSizeComboBox.Items.Clear();
            reachSpecSizeSelector.Items.AddRange(getDropdownItems().ToArray());
            pathNodeSizeComboBox.Items.AddRange(getDropdownItems().ToArray());
        }

        public void PassPathfindingNodeEditorIn(PathfindingEditor PathfindingEditorInstance)
        {
            this.PathfindingEditorInstance = PathfindingEditorInstance;
        }

        public void LoadExport(IExportEntry export)
        {
            AllowChanges = false;
            this.export = export;
            reachableNodesList.Items.Clear();
            reachSpecSizeSelector.Enabled = false;
            sfxCombatZoneList.Items.Clear();

            combatZones = new List<int>();
            reachSpecs = new List<int>();

            var props = export.GetProperties();

            exportTitleLabel.Text = export.ObjectName + "_" + export.indexValue;

            //Get Location
            StructProperty location = props.GetProp<StructProperty>("location");
            if (location != null)
            {
                float x = location.GetProp<FloatProperty>("X");
                float y = location.GetProp<FloatProperty>("Y");
                float z = location.GetProp<FloatProperty>("Z");

                xLabel.Text = "X: " + x;
                yLabel.Text = "Y: " + y;
                zLabel.Text = "Z: " + z;

            }
            else
            {
                xLabel.Text = "X: ";
                yLabel.Text = "Y: ";
                zLabel.Text = "Z: ";
            }

            //Calculate size
            StructProperty maxPathSize = props.GetProp<StructProperty>("MaxPathSize");
            if (maxPathSize != null)
            {
                float height = maxPathSize.GetProp<FloatProperty>("Height");
                float radius = maxPathSize.GetProp<FloatProperty>("Radius");
                exportTitleLabel.Text += " - " + radius + "x" + height;
                pathNodeSizeComboBox.SelectedIndex = findClosestNextSizeIndex((int)radius, (int)height);
                pathNodeSizeComboBox.Enabled = true;
            }
            else
            {
                pathNodeSizeComboBox.Enabled = false;
            }

            //Calculate reachspecs
            ArrayProperty<ObjectProperty> PathList = props.GetProp<ArrayProperty<ObjectProperty>>("PathList");
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
            AllowChanges = true;
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
                connectionToLabel.Text = "Connection to";
                reachSpecSizeSelector.Enabled = false;
                return;
            }

            AllowChanges = false;
            reachSpecSizeSelector.Enabled = true;

            IExportEntry reachSpec = export.FileRef.Exports[reachSpecs[n]];
            Unreal.PropertyCollection props = reachSpec.GetProperties();


            StructProperty outgoingEndStructProp = props.GetProp<StructProperty>("End"); //Embeds END
            ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>("Actor"); //END                    
            if (outgoingSpecEndProp.Value - 1 > 0)
            {

                IExportEntry endNode = export.FileRef.Exports[outgoingSpecEndProp.Value - 1];
                reachSpecDestLabel.Text = endNode.ObjectName + "_" + endNode.indexValue;
                connectionToLabel.Text = "Connection to " + endNode.Index;
            }

            IntProperty radius = props.GetProp<IntProperty>("CollisionRadius");
            IntProperty height = props.GetProp<IntProperty>("CollisionHeight");

            if (radius != null && height != null)
            {
                reachSpecSizeLabel.Text = "ReachSpec Size: " + radius.Value + "x" + height.Value;

                reachSpecSizeSelector.SelectedIndex = findClosestNextSizeIndex(radius, height);
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

                    Point size = getDropdownSizePair(selectedIndex);
                    radVal = size.X;
                    heightVal = size.Y;

                    radius.Value = radVal;
                    height.Value = heightVal;
                    reachSpec.WriteProperties(props);
                    reachSpecSelection_Changed(null, null);
                }
            }
        }

        private void pathNodeSize_DropdownChanged(object sender, EventArgs e)
        {
            if (AllowChanges)
            {
                int selectedIndex = pathNodeSizeComboBox.SelectedIndex;

                Unreal.PropertyCollection props = export.GetProperties();
                StructProperty maxPathSize = props.GetProp<StructProperty>("MaxPathSize");
                if (maxPathSize != null)
                {
                    FloatProperty height = maxPathSize.GetProp<FloatProperty>("Height");
                    FloatProperty radius = maxPathSize.GetProp<FloatProperty>("Radius");
                    if (radius != null && height != null)
                    {
                        int radVal = -1;
                        int heightVal = -1;

                        Point size = getDropdownSizePair(selectedIndex);
                        radVal = size.X;
                        heightVal = size.Y;

                        long heightOffset = height.Offset;
                        long radiusOffset = radius.Offset;

                        //Manually write it to avoid property writing errors with cover stuff
                        byte[] data = export.Data;
                        WriteMem((int)heightOffset, data, BitConverter.GetBytes(Convert.ToSingle(heightVal)));
                        WriteMem((int)radiusOffset, data, BitConverter.GetBytes(Convert.ToSingle(radVal)));
                        export.Data = data;
                    }
                }
            }
        }

        private void WriteMem(int pos, byte[] memory, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];
        }

        public static List<string> getDropdownItems()
        {
            List<string> items = new List<string>();
            items.Add("Mooks " + MOOK_RADIUS + "x" + MOOK_HEIGHT);
            items.Add("Minibosses " + MINIBOSS_RADIUS + "x" + MINIBOSS_HEIGHT);
            items.Add("Boss - Banshee " + BANSHEE_RADIUS + "x" + BANSHEE_HEIGHT);
            items.Add("Bosses - All " + BOSS_RADIUS + "x" + BOSS_HEIGHT);
            return items;
        }

        public static Point getDropdownSizePair(int index)
        {
            switch (index)
            {
                case 0:
                    return new Point(MOOK_RADIUS, MOOK_HEIGHT);
                case 1:
                    return new Point(MINIBOSS_RADIUS, MINIBOSS_HEIGHT);
                case 2:
                    return new Point(BANSHEE_RADIUS, BANSHEE_HEIGHT);
                case 3:
                    return new Point(BOSS_RADIUS, BOSS_HEIGHT);
                default:
                    return new Point(MOOK_RADIUS, MOOK_HEIGHT);
            }
        }

        private int findClosestNextSizeIndex(int radius, int height)
        {
            if (radius == BANSHEE_RADIUS && height == BANSHEE_HEIGHT)
            {
                //BANSHEES
                return 2;
            }

            if (radius < MINIBOSS_RADIUS || height < MINIBOSS_HEIGHT)
            {
                //MOOKS
                return 0;
            }
            else if (radius >= BOSS_RADIUS && height >= BOSS_HEIGHT)
            {
                //ALL BOSSES
                return 3;
            }
            else
            {
                //MINIBOSSES
                return 1;
            }
        }
    }
}