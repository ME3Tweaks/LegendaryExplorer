using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer.Pathfinding_Editor
{
    public partial class DuplicateGUIDWindow : Form
    {
        private IMEPackage pcc;
        List<NavGUID> navGuids;
        List<NavGUID> duplicateGuids;

        public DuplicateGUIDWindow(IMEPackage pcc)
        {
            InitializeComponent();
            this.pcc = pcc;
            navGuids = new List<NavGUID>();
            duplicateGuids = new List<NavGUID>();
            populateDuplicateGUIDs();
        }

        private void populateDuplicateGUIDs()
        {



            IExportEntry level = null;
            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Level" && exp.ObjectName == "PersistentLevel")
                {
                    level = exp;
                    break;
                }
            }
            int start = 0x4;
            if (level != null)
            {
                start = PathfindingEditor.findEndOfProps(level);
            }
            //Read persistent level binary
            byte[] data = level.Data;

            uint exportid = BitConverter.ToUInt32(data, start);
            start += 4;
            uint numberofitems = BitConverter.ToUInt32(data, start);
            int countoffset = start;
            int itemcount = 0;
            int numUpdated = 0;
            while (itemcount < numberofitems)
            {
                //get header.
                uint itemexportid = BitConverter.ToUInt32(data, start);
                if (itemexportid - 1 < pcc.Exports.Count && itemexportid > 0)
                {
                    IExportEntry exportEntry = pcc.Exports[(int)itemexportid - 1];
                    StructProperty navguid = exportEntry.GetProperty<StructProperty>("NavGuid");
                    if (navguid != null)
                    {
                        int a = navguid.GetProp<IntProperty>("A");
                        int b = navguid.GetProp<IntProperty>("B");
                        int c = navguid.GetProp<IntProperty>("C");
                        int d = navguid.GetProp<IntProperty>("D");
                        NavGUID nav = new NavGUID();
                        nav.A = a;
                        nav.B = b;
                        nav.C = c;
                        nav.D = d;
                        nav.export = exportEntry;

                        if (navGuids.Contains(nav))
                        {
                            //Debug.WriteLine("DUPLICATE FOUDN!");
                            duplicateGuids.Add(nav);
                        }
                        else
                        {
                            navGuids.Add(nav);
                        }
                    }
                    start += 4;
                    itemcount++;
                }
                else
                {
                    //INVALID or empty item encountered. We don't care right now though.
                    start += 4;
                    itemcount++;
                }
            }


            duplicatesListBox.Items.Clear();
            foreach (NavGUID guid in duplicateGuids)
            {
                duplicatesListBox.Items.Add(guid.export.Index + " " + guid.export.ObjectName + "_" + guid.export.indexValue);
            }
        }

        private void generateButton_Click(object sender, EventArgs e)
        {

        }

        private void duplicateGuidList_SelectionChanged(object sender, EventArgs e)
        {
            if (duplicatesListBox.SelectedIndex > 0)
            {


                NavGUID guid = duplicateGuids[duplicatesListBox.SelectedIndex];
                aLabel.Text = "A: " + guid.A;
                bLabel.Text = "B: " + guid.B;
                cLabel.Text = "C: " + guid.C;
                dLabel.Text = "D: " + guid.D;

            }
        }
    }
}


