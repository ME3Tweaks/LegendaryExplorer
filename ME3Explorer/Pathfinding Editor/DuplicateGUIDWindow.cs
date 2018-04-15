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
        Dictionary<string, List<UnrealGUID>> navGuidLists = new Dictionary<string, List<UnrealGUID>>();
        List<UnrealGUID> duplicateGuids = new List<UnrealGUID>();
        public DuplicateGUIDWindow(IMEPackage pcc)
        {
            InitializeComponent();
            this.pcc = pcc;
            populateDuplicateGUIDs();
        }

        private void populateDuplicateGUIDs()
        {
            navGuidLists = new Dictionary<string, List<UnrealGUID>>();
            duplicateGuids = new List<UnrealGUID>();

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
            int itemcount = 2;
            start += 8;
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
                        UnrealGUID nav = new UnrealGUID();
                        nav.A = a;
                        nav.B = b;
                        nav.C = c;
                        nav.D = d;
                        nav.export = exportEntry;
                        nav.levelListIndex = itemcount;

                        List<UnrealGUID> list;
                        if (navGuidLists.TryGetValue(nav.ToString(), out list))
                        {
                            list.Add(nav);
                        }
                        else
                        {
                            list = new List<UnrealGUID>();
                            navGuidLists[nav.ToString()] = list;
                            list.Add(nav);
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
            foreach (List<UnrealGUID> guidList in navGuidLists.Values)
            {
                if (guidList.Count > 1)
                {
                    Debug.WriteLine("Number of duplicates: " + guidList.Count);
                    //contains duplicates
                    foreach (UnrealGUID guid in guidList)
                    {
                        //Debug.WriteLine(guid.levelListIndex + " Duplicate: " + guid.export.ObjectName);
                        duplicateGuids.Add(guid);
                        duplicatesListBox.Items.Add(guid.levelListIndex + " " + guid.export.Index + " " + guid.export.ObjectName + "_" + guid.export.indexValue);
                    }
                } else
                {
                    Debug.WriteLine("Not a duplicate ");

                }
            }
        }

        private void generateButton_Click(object sender, EventArgs e)
        {
            if (duplicatesListBox.SelectedIndex >= 0)
            {
                UnrealGUID guid = duplicateGuids[duplicatesListBox.SelectedIndex];
                SharedPathfinding.GenerateNewRandomGUID(guid.export);
                populateDuplicateGUIDs();
            }
        }

        private void duplicateGuidList_SelectionChanged(object sender, EventArgs e)
        {
            if (duplicatesListBox.SelectedIndex >= 0)
            {
                UnrealGUID guid = duplicateGuids[duplicatesListBox.SelectedIndex];
                aLabel.Text = "A: " + guid.A;
                bLabel.Text = "B: " + guid.B;
                cLabel.Text = "C: " + guid.C;
                dLabel.Text = "D: " + guid.D;
                generateButton.Enabled = true;

                BoolProperty bHasCrossLevelPaths = guid.export.GetProperty<BoolProperty>("bHasCrossLevelPaths");
                if (bHasCrossLevelPaths != null && bHasCrossLevelPaths == true)
                {
                    crossLevelPathsLabel.Visible = true;
                }
                else
                {
                    crossLevelPathsLabel.Visible = false;
                }
            }
            else
            {
                generateButton.Enabled = false;
                aLabel.Text = "A: ";
                bLabel.Text = "B: ";
                cLabel.Text = "C: ";
                dLabel.Text = "D: ";
                crossLevelPathsLabel.Visible = false;
            }
        }
    }
}


