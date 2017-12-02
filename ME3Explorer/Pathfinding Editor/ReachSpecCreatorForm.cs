using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer.Pathfinding_Editor
{
    public partial class ReachSpecCreatorForm : Form
    {
        private IMEPackage pcc;
        private IExportEntry sourceExport;
        private Point3D sourcePoint;
        public int DestinationNode = -1;
        public bool CreateTwoWaySpec = true;
        public string SpecClass = "";
        public int SpecSize;

        public ReachSpecCreatorForm(IMEPackage package, int sourceExportIndex)
        {
            InitializeComponent();
            specSizeCombobox.Items.Clear();
            specSizeCombobox.Items.AddRange(PathfindingNodeInfoPanel.getDropdownItems().ToArray());
            this.pcc = package;
            this.sourceExport = pcc.Exports[sourceExportIndex];

            string sourceExportClass = sourceExport.ClassName;
            sourceNodeLabel.Text = "Export " + sourceExportIndex + " | " + sourceExportClass;

            //Get Source Location
            StructProperty locationProp = sourceExport.GetProperty<StructProperty>("location");
            Unreal.PropertyCollection nodelocprops = locationProp.Properties;
            //X offset is 0x20
            //Y offset is 0x24
            //Z offset is 0x28
            float sourceX = 0, sourceY = 0, sourceZ = 0;

            foreach (var locprop in nodelocprops)
            {
                switch (locprop.Name)
                {
                    case "X":
                        sourceX = Convert.ToInt32((locprop as FloatProperty).Value);
                        break;
                    case "Y":
                        sourceY = Convert.ToInt32((locprop as FloatProperty).Value);
                        break;
                    case "Z":
                        sourceZ = Convert.ToInt32((locprop as FloatProperty).Value);
                        break;
                }
            }

            sourcePoint = new Point3D(sourceX, sourceY, sourceZ);
            reachSpecTypeComboBox.SelectedIndex = 0;
            specSizeCombobox.SelectedIndex = 1;

        }

        private void destinationNodeTextBox_KeyPressed(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar == Convert.ToChar(Keys.Enter)) && createSpecButton.Enabled)
            {
                e.Handled = true;
                createSpecButton.PerformClick();
                return;
            }
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar); //prevent non digit entry
        }

        private void destinationNodeTextBox_TextChanged(object sender, EventArgs e)
        {
            int destIndex = -1;
            Int32.TryParse(destinationNodeTextBox.Text, out destIndex);
            if (destIndex >= 0 && destIndex < pcc.Exports.Count)
            {
                //Parse
                IExportEntry destExport = pcc.Exports[destIndex];

                float destX = 0, destY = 0, destZ = 0;

                StructProperty locationProp = destExport.GetProperty<StructProperty>("location");
                if (locationProp != null)
                {
                    Unreal.PropertyCollection nodelocprops = locationProp.Properties;
                    //X offset is 0x20
                    //Y offset is 0x24
                    //Z offset is 0x28
                    foreach (var locprop in nodelocprops)
                    {
                        switch (locprop.Name)
                        {
                            case "X":
                                destX = Convert.ToInt32((locprop as FloatProperty).Value);
                                break;
                            case "Y":
                                destY = Convert.ToInt32((locprop as FloatProperty).Value);
                                break;
                            case "Z":
                                destZ = Convert.ToInt32((locprop as FloatProperty).Value);
                                break;
                        }
                    }

                    Point3D destPoint = new Point3D(destX, destY, destZ);

                    double distance = sourcePoint.getDistanceToOtherPoint(destPoint);

                    //Calculate direction vectors
                    if (distance != 0)
                    {

                        float dirX = (float)((destPoint.X - sourcePoint.X) / distance);
                        float dirY = (float)((destPoint.Y - sourcePoint.Y) / distance);
                        float dirZ = (float)((destPoint.Z - sourcePoint.Z) / distance);


                        distanceLabel.Text = "Distance: " + distance.ToString("0.##");
                        directionX.Text = "X: " + dirX.ToString("0.#####");
                        directionY.Text = "Y: " + dirY.ToString("0.#####");
                        directionZ.Text = "Z: " + dirZ.ToString("0.#####");

                        destinationLabel.Text = "| " + destExport.ClassName;

                        if ((string)reachSpecTypeComboBox.SelectedItem != "")
                        {
                            createSpecButton.Enabled = true;
                        }
                    }
                }
            }
            else
            {
                emptyAutoFields();
            }
        }

        private void emptyAutoFields()
        {
            destinationLabel.Text = "| Invalid Export #";
            distanceLabel.Text = "Distance:";
            directionLabel.Text = "Direction Vector";
            directionX.Text = "X: ";
            directionY.Text = "Y: ";
            directionZ.Text = "Z: ";
            createSpecButton.Enabled = false;
        }

        private void createSpecButton_Click(object sender, EventArgs e)
        {
            int destIndex = -1;
            Int32.TryParse(destinationNodeTextBox.Text, out destIndex);
            if (destIndex >= 0)
            {
                DestinationNode = destIndex;
                CreateTwoWaySpec = createReturningSpecCheckbox.Checked;
                SpecClass = (string)reachSpecTypeComboBox.SelectedItem;
                SpecSize = specSizeCombobox.SelectedIndex;
                this.DialogResult = System.Windows.Forms.DialogResult.Yes;
                this.Close();
            }
        }
    }

    class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D()
        {

        }

        public Point3D(double X, double Y, double Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public double getDistanceToOtherPoint(Point3D other)
        {
            double deltaX = X - other.X;
            double deltaY = Y - other.Y;
            double deltaZ = Z - other.Z;

            return (double)Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
        }

        public override string ToString()
        {
            return X + "," + Y + "," + Z;
        }
    }
}
