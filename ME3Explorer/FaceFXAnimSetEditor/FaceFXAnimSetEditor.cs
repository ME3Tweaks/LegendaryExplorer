using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Be.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer.FaceFX
{
    public partial class FaceFXAnimSetEditor : WinFormsBase
    {
        public List<int> Objects;
        public ME3FaceFXAnimSet FaceFX;

        public FaceFXAnimSetEditor()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    LoadME3Package(d.FileName);
                    ListRefresh();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error:\n" + ex.Message);
                }
            }
        }

        public void ListRefresh()
        {
            Objects = new List<int>();
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            for (int i = 0; i < Exports.Count; i++)
                if (Exports[i].ClassName == "FaceFXAnimSet")
                    Objects.Add(i);
            listBox1.Items.Clear();
            foreach(int n in Objects)
                listBox1.Items.Add("#" + n + " : " + pcc.Exports[n].GetFullPath);
        }

        private void FaceFXRefresh(int n)
        {
            IEnumerable<string> expandedNodes = null;
            string topNodeName = null;
            if (FaceFX != null && n == FaceFX.export.Index)
            {
                List<TreeNode> allNodes = treeView2.Nodes.Cast<TreeNode>().ToList();
                //flatten tree of nodes into list.
                for (int j = 0; j < allNodes.Count(); j++)
                {
                    allNodes.AddRange(allNodes[j].Nodes.Cast<TreeNode>());
                }
                expandedNodes = allNodes.Where(x => x.IsExpanded).Select(x => x.Name);
                topNodeName = treeView2.TopNode.Name;

            }
            IExportEntry exportEntry = pcc.Exports[n];
            FaceFX = new ME3FaceFXAnimSet(pcc, exportEntry);
            hb1.ByteProvider = new DynamicByteProvider(exportEntry.Data);
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(FaceFX.HeaderToTree());
            nameAllNodes(treeView1.Nodes);
            treeView2.Nodes.Clear();
            treeView2.Nodes.Add(FaceFX.DataToTree());
            nameAllNodes(treeView2.Nodes);
            TreeNode[] nodes;
            if (expandedNodes != null)
            {
                foreach (string item in expandedNodes)
                {
                    nodes = treeView2.Nodes.Find(item, true);
                    if (nodes.Length > 0)
                    {
                        foreach (var node in nodes)
                        {
                            node.Expand();
                        }
                    }
                }
            }
            nodes = treeView2.Nodes.Find(topNodeName, true);
            if (nodes.Length > 0)
            {
                treeView2.TopNode = nodes[0];
            }
        }

        private void nameAllNodes(TreeNodeCollection Nodes)
        {
            List<TreeNode> allNodes = Nodes.Cast<TreeNode>().ToList();
            //flatten tree of nodes into list.
            for (int i = 0; i < allNodes.Count(); i++)
            {
                allNodes[i].Name = i.ToString();
                allNodes.AddRange(allNodes[i].Nodes.Cast<TreeNode>());
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            FaceFXRefresh(Objects[n]);
        }

        private void recreateAndDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FaceFX == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.FileName = FaceFX.export.ObjectName + ".fxa";
            d.Filter = "*.fxa|*.fxa";
            if(d.ShowDialog() == DialogResult.OK)
            {
                FaceFX.DumpToFile(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            pcc.save();
            MessageBox.Show("Done.");
        }

        private void treeView2_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode t1 = t.Parent;
            if (t1 == null || t1.Parent == null)
                return;
            TreeNode t2 = t1.Parent;
            if (t2 == null || t2.Parent == null)
                return;
            string result; int i; float f = 0;
            if (t2.Text == "Entries")
            {
                int entidx = t1.Index;
                int subidx = t.Index;
                ME3FaceFXLine d = FaceFX.Data.Data[entidx];
                switch (subidx)
                {
                    case 0://unk1
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", d.Name.ToString(), 0, 0);
                        i = -1;
                        if (int.TryParse(result, out i) && i >= 0 && i < FaceFX.Header.Names.Length)
                            d.Name = i;
                        break;
                    case 4://FadeInTime
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", d.FadeInTime.ToString(), 0, 0);
                        if (float.TryParse(result, out f))
                            d.FadeInTime = f;
                        break;
                    case 5://FadeInTime
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", d.FadeOutTime.ToString(), 0, 0);
                        if (float.TryParse(result, out f))
                            d.FadeOutTime = f;
                        break;
                    case 6://unk2
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", d.unk2.ToString(), 0, 0);
                        i = -1;
                        if (int.TryParse(result, out i) && i >= 0 && i < FaceFX.Header.Names.Length)
                            d.unk2 = i;
                        break;
                    case 7://Path
                        d.path = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", d.path, 0, 0);
                        break;
                    case 8://ID
                        d.ID = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", d.ID, 0, 0);
                        break;
                    case 9://unk3
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", d.index.ToString(), 0, 0);
                        i = -1;
                        if (int.TryParse(result, out i) && i >= 0 && i < FaceFX.Header.Names.Length)
                            d.index = i;
                        break;
                    default:
                        return;
                }
                FaceFX.Data.Data[entidx] = d;
                FaceFX.Save();
            }
            else if(t2.Parent.Text == "Entries")
            {
                int entidx = t2.Index;
                int subidx = t1.Index;
                int subsubidx = t.Index;
                ME3FaceFXLine d = FaceFX.Data.Data[entidx];
                switch (subidx)
                {
                    case 1:
                        ME3NameRef u = d.animations[subsubidx];
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", u.index + " ; " + u.unk2, 0, 0);
                        string[] reslist = result.Split(';');
                        if (reslist.Length != 2)
                            return;
                        if (int.TryParse(reslist[0].Trim(), out i))
                            u.index = i;
                        else
                            return;
                        if (int.TryParse(reslist[1].Trim(), out i))
                            u.unk2 = i;
                        else
                            return;
                        d.animations[subsubidx] = u;
                        break;
                    case 2:
                        ControlPoint u2 = d.points[subsubidx];
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", u2.time + " ; " + u2.weight + " ; " + u2.inTangent + " ; " + u2.leaveTangent, 0, 0);
                        reslist = result.Split(';');
                        if (reslist.Length != 4)
                            return;
                        if (float.TryParse(reslist[0].Trim(), out f))
                            u2.time = f;
                        else
                            return;
                        if (float.TryParse(reslist[1].Trim(), out f))
                            u2.weight = f;
                        else
                            return;
                        if (float.TryParse(reslist[2].Trim(), out f))
                            u2.inTangent = f;
                        else
                            return;
                        if (float.TryParse(reslist[3].Trim(), out f))
                            u2.leaveTangent = f;
                        else
                            return;
                        d.points[subsubidx] = u2;
                        break;
                    case 3:
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", d.numKeys[subsubidx].ToString(), 0, 0);
                        if (int.TryParse(result.Trim(), out i))
                            d.numKeys[subsubidx] = i;
                        else
                            return;
                        break;
                }
                FaceFX.Data.Data[entidx] = d;
                FaceFX.Save();
            }
        }

        private void cloneEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode t1 = t.Parent;
            if (t1 == null || t1.Text != "Entries" || FaceFX == null)
                return;
            FaceFX.CloneEntry(t.Index);
            FaceFX.Save();
        }

        private void deleteEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode t1 = t.Parent;
            if (t1 == null || t1.Text != "Entries" || FaceFX == null)
                return;
            FaceFX.RemoveEntry(t.Index);
            FaceFX.Save();
        }

        private void moveEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode t1 = t.Parent;
            if (t1 == null || t1.Text != "Entries" || FaceFX == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new index", "ME3Explorer", t.Index.ToString(), 0, 0);
            int i = 0;
            if (int.TryParse(result, out i))
            {
                FaceFX.MoveEntry(t.Index, i);
                FaceFX.Save();
            }
        }

        private void addNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FaceFX == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name to add", "ME3Explorer", "", 0, 0);
            if (result != "")
            {
                FaceFX.AddName(result);
                FaceFX.Save();
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (FaceFX != null && updatedExports.Contains(FaceFX.export.Index))
            {
                int index = FaceFX.export.Index;
                //loaded FaceFXAnimset is no longer a FaceFXAnimset
                if (FaceFX.export.ClassName != "FaceFXAnimSet")
                {
                    FaceFX = null;
                    treeView1.Nodes.Clear();
                    treeView2.Nodes.Clear();
                    hb1.ByteProvider = new DynamicByteProvider(new List<byte>());
                    ListRefresh();
                }
                else
                {
                    FaceFXRefresh(index);
                }
                updatedExports.Remove(index);
            }
            if (updatedExports.Intersect(Objects).Count() > 0)
            {
                ListRefresh();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    if (pcc.getExport(i).ClassName == "FaceFXAnimSet")
                    {
                        ListRefresh();
                        break;
                    }
                }
            }
        }
    }
}
