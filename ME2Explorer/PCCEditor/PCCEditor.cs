using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ME2Explorer.Unreal;
using ME2Explorer.Unreal.Classes;
using ME2Explorer.Helper;
using KFreonLib.MEDirectories;

namespace ME2Explorer
{
    public partial class PCCEditor : Form
    {
        public int CurrentView = 2;
        public int PreviewStyle = 0;
        public PCCObject pcc;
        public List<int> classes;
        public PropGrid pg;

        public PCCEditor()
        {
            InitializeComponent();
        }

        public void RefreshLists()
        {
            toolStripButton1.Checked = false;
            toolStripButton2.Checked = false;
            toolStripButton3.Checked = false;
            if (pcc == null)
                return;
            int count = 0;
            if (CurrentView == 0)
            {
                toolStripButton1.Checked = true;
                listBox1.BeginUpdate();
                listBox1.Items.Clear();
                foreach (string name in pcc.Names)
                    listBox1.Items.Add((count++).ToString("d6") + " : " + name);
                listBox1.EndUpdate();
            }
            if (CurrentView == 1)
            {
                toolStripButton2.Checked = true;
                listBox1.BeginUpdate();
                listBox1.Items.Clear();
                foreach (PCCObject.ImportEntry imp in pcc.Imports)
                    listBox1.Items.Add((count++).ToString("d6") + " : " + pcc.FollowLink(imp.link) + imp.Name);
                listBox1.EndUpdate();
            }
            if (CurrentView == 2)
            {
                toolStripButton3.Checked = true;
                listBox1.BeginUpdate();
                listBox1.Items.Clear();
                foreach (PCCObject.ExportEntry exp in pcc.Exports)
                    listBox1.Items.Add((count++).ToString("d6") + " : " + exp.PackageFullName + "." + exp.ObjectName);
                listBox1.EndUpdate();

            }
            toolStripComboBox1.Items.Clear();
            foreach (int index in classes)
                toolStripComboBox1.Items.Add(pcc.GetClass(index));

        }

        private void openPccToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc = new PCCObject(d.FileName);
                CurrentView = 2;
                classes = new List<int>();
                foreach (PCCObject.ExportEntry ent in pcc.Exports)
                {
                    int f = -1;
                    for (int i = 0; i < classes.Count(); i++)
                        if (classes[i] == ent.ClassNameID)
                            f = i;
                    if (f == -1)
                        classes.Add(ent.ClassNameID);
                }
                bool run = true;
                while (run)
                {
                    run = false;
                    for (int i = 0; i < classes.Count() - 1; i++)
                        if (pcc.GetName(classes[i]).CompareTo(pcc.GetName(classes[i + 1])) > 0)
                        {
                            int t = classes[i];
                            classes[i] = classes[i + 1];
                            classes[i + 1] = t;
                            run = true;
                        }
                }
                RefreshView();
            }
        }

        public void listBox1SelectIndex(int i)
        {
            listBox1.SelectedIndex = i;
        }

        public void RefreshView()
        {
            listBox1.Visible = false;
            listBox1.Items.Clear();
            if (pcc == null)
            {
                listBox1.Visible = true;
                return;
            }
            //cloneObjectToolStripMenuItem.Enabled = false;
            listBox1.BeginUpdate();
            treeView1.BeginUpdate();
            if (CurrentView == 0)
            {
                for (int i = 0; i < pcc.Names.Count; i++)
                    listBox1.Items.Add(i.ToString() + " : " + pcc.Names[i]);
            }
            if (CurrentView == 1)
            {
                for (int i = 0; i < pcc.Imports.Count; i++)
                    listBox1.Items.Add(i.ToString() + " : " + pcc.Imports[i].Name);
            }
            string s;
            if (CurrentView == 2)
            {
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    //cloneObjectToolStripMenuItem.Enabled = true;
                    s = "";
                    if (pcc.Exports[i].PackageFullName != "Class" && pcc.Exports[i].PackageFullName != "Package")
                        s += pcc.Exports[i].PackageFullName + ".";
                    s += pcc.Exports[i].ObjectName;
                    listBox1.Items.Add(i.ToString() + " : " + s);
                }
            }
            if (CurrentView == 3)
            {
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    //cloneObjectToolStripMenuItem.Enabled = true;
                    s = "";
                    if (pcc.Exports[i].PackageFullName != "Class" && pcc.Exports[i].PackageFullName != "Package")
                        s += pcc.Exports[i].PackageFullName + ".";
                    s += pcc.Exports[i].ObjectName;
                    listBox1.Items.Add(i.ToString() + " : " + s);
                }
                listBox1.Visible = false;
                treeView1.Visible = true;
                treeView1.Nodes.Clear();
                TreeNode t = new TreeNode(pcc.pccFileName);
                for (int i = 0; i < pcc.Exports.Count; i++)
                {

                    //cloneObjectToolStripMenuItem.Enabled = true;
                    PCCObject.ExportEntry e = pcc.Exports[i];
                    List<int> LinkList = new List<int>();
                    LinkList.Add(i + 1);
                    int Link = e.LinkID;
                    while (Link != 0)
                    {
                        LinkList.Add(Link);
                        if (Link > 0)
                            Link = pcc.Exports[Link - 1].LinkID;
                        else
                            Link = pcc.Imports[-Link - 1].link;
                    }
                    t = AddPathToTree(t, LinkList);
                }
                for (int i = 0; i < pcc.Imports.Count; i++)
                {

                    //cloneObjectToolStripMenuItem.Enabled = true;
                    PCCObject.ImportEntry e = pcc.Imports[i];
                    List<int> LinkList = new List<int>();
                    LinkList.Add(-(i + 1));
                    int Link = e.link;
                    while (Link != 0)
                    {
                        LinkList.Add(Link);
                        if (Link > 0)
                            Link = pcc.Exports[Link - 1].LinkID;
                        else
                            Link = pcc.Imports[-Link - 1].link;
                    }
                    t = AddPathToTree(t, LinkList);
                }
                treeView1.Nodes.Add(t);
            }
            else
            {
                treeView1.Visible = false;
                listBox1.Visible = true;
            }
            listBox1.EndUpdate();
            treeView1.EndUpdate();
        }


        private TreeNode AddPathToTree(TreeNode t, List<int> LinkList)
        {
            string s = "";
            int idx, f;
            idx = LinkList[LinkList.Count() - 1];
            if (idx > 0)
                s = "(Exp)" + (idx - 1) + " : " + pcc.Exports[idx - 1].ObjectName + "(" + pcc.Exports[idx - 1].ClassName + ")";
            else
                s = "(Imp)" + (-idx - 1) + " : " + pcc.Imports[-idx - 1].Name; // +"(" + pcc.Imports[-idx - 1].ClassName + ")";
            f = -1;
            for (int i = 0; i < t.Nodes.Count; i++)
                if (t.Nodes[i].Text == s)
                {
                    f = i;
                    break;
                }
            if (f == -1)
            {
                if (idx > 0)
                    t.Nodes.Add((idx - 1).ToString(), s);
                else
                    t.Nodes.Add(s);
                if (LinkList.Count() > 1)
                    t.Nodes[t.Nodes.Count - 1] = AddPathToTree(t.Nodes[t.Nodes.Count - 1], LinkList.GetRange(0, LinkList.Count - 1));
            }
            else
            {
                if (LinkList.Count() > 1)
                    t.Nodes[f] = AddPathToTree(t.Nodes[f], LinkList.GetRange(0, LinkList.Count - 1));
            }
            return t;
        }

        private int GetSelected()
        {
            int n = -1;
            if (CurrentView == 3 && treeView1.SelectedNode != null && treeView1.SelectedNode.Name != "")
                n = Convert.ToInt32(treeView1.SelectedNode.Name);
            if (CurrentView == 2)
                n = listBox1.SelectedIndex;
            return n;
        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {
            CurrentView = 0;
            RefreshView();
        }

        private void toolStripButton2_Click_1(object sender, EventArgs e)
        {
            CurrentView = 1;
            RefreshView();
        }

        private void toolStripButton3_Click_1(object sender, EventArgs e)
        {
            CurrentView = 2;
            RefreshView();
        }

        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            CurrentView = 3;
            RefreshView();
        }

        private void savePccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc.SaveToFile(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Preview();
        }

        private void Preview()
        {

            PreviewRaw();
            PreviewProps();
            int n = GetSelected();

            if ((CurrentView == 2 || CurrentView == 3) && n != -1)
            {
                int off = pcc.Imports.Count;
                RefreshCombos();
                comboBox1.SelectedIndex = pcc.Exports[n].ObjectNameID;
                comboBox2.SelectedIndex = pcc.Exports[n].ClassNameID + off;
                comboBox3.SelectedIndex = pcc.Exports[n].LinkID + off;
                hb2.ByteProvider = new DynamicByteProvider(pcc.Exports[n].info);
            }
        }

        public void RefreshCombos()
        {
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            comboBox3.Items.Clear();
            List<string> Classes = new List<string>();
            for (int i = pcc.Imports.Count - 1; i >= 0; i--)
                Classes.Add(-(i + 1) + " : " + pcc.Imports[i].Name);
            Classes.Add("0 : Class");
            int count = 1;
            foreach (PCCObject.ExportEntry exp in pcc.Exports)
                Classes.Add((count++) + " : " + exp.ObjectName);
            count = 0;
            foreach (string s in pcc.Names)
                comboBox1.Items.Add((count++) + " : " + s);
            foreach (string s in Classes)
            {
                comboBox2.Items.Add(s);
                comboBox3.Items.Add(s);
            }
        }

        //private void PreviewProps(int n)
        //{
        //    PCCObject.ExportEntry ent = pcc.Exports[n];
        //    List<SaltPropertyReader.Property> props = SaltPropertyReader.getPropList(pcc, ent.Data);
        //    rtb1.Visible = true;
        //    string s = "";
        //    s += "ObjectName : " + ent.ObjectName + "\n";
        //    s += "Class : " + ent.ClassName + "\n";
        //    s += "Data size : 0x" + ent.DataSize.ToString("X8") + "\n";
        //    s += "Data offset : 0x" + ent.DataOffset.ToString("X8") + "\n\nProperties: \n";
        //    foreach (SaltPropertyReader.Property p in props)
        //        s += SaltPropertyReader.PropertyToText(p, pcc) + "\n";

        //    if (ent.ClassName == "Texture2D" || ent.ClassName == "LightMapTexture2D" || ent.ClassName == "TextureFlipBook")
        //    {
        //        s += "\nImage Info: \n";
        //        try
        //        {
        //            Texture2D tex2D = new Texture2D(pcc, n, Path.Combine(ME2Directory.gamePath, "BIOGame"));
        //            for (int i = 0; i < tex2D.imgList.Count; i++)
        //            {
        //                s += i + ": Location: " + tex2D.imgList[i].storageType + ", Offset: " + tex2D.imgList[i].offset + ", ImgSize: " + tex2D.imgList[i].imgSize.ToString() + ", CprSize = " + tex2D.imgList[i].cprSize + ", UncSize = " + tex2D.imgList[i].uncSize + "\n";
        //            }
        //        }
        //        catch { }
        //    }

        //    rtb1.Text = s;
        //    Status.Text = ent.ClassName + " Offset: " + ent.DataOffset + " - " + (ent.DataOffset + ent.DataSize);
        //}

        public void PreviewProps()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == 2 || CurrentView == 3))
                return;
            List<SaltPropertyReader.Property> p;
            //propGrid.Visible = true;
            //hb1.Visible = false;
            //rtb1.Visible = false;
            //treeView1.Visible = false;
            switch (pcc.Exports[n].ClassName)
            {
                default:
                    byte[] buff = pcc.Exports[n].Data;
                    p = SaltPropertyReader.getPropList(pcc, buff);
                    break;
            }
            pg = new PropGrid();
            propGrid.SelectedObject = pg;
            pg.Add(new CustomProperty("Name", "_Meta", pcc.Exports[n].ObjectName, typeof(string), true, true));
            pg.Add(new CustomProperty("Class", "_Meta", pcc.Exports[n].ClassName, typeof(string), true, true));
            pg.Add(new CustomProperty("Data Offset", "_Meta", pcc.Exports[n].DataOffset, typeof(int), true, true));
            pg.Add(new CustomProperty("Data Size", "_Meta", pcc.Exports[n].DataSize, typeof(int), true, true));
            for (int l = 0; l < p.Count; l++)
                pg.Add(SaltPropertyReader.PropertyToGrid(p[l], pcc));
            propGrid.Refresh();
            propGrid.ExpandAllGridItems();
            //UpdateStatusEx(n);
        }

        private void PreviewRaw()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == 2 || CurrentView == 3))
                return;   
            PCCObject.ExportEntry ent = pcc.Exports[n];
            hexBox1.ByteProvider = new DynamicByteProvider(ent.Data);
            Status.Text = ent.ClassName + " Offset: " + ent.DataOffset + " - " + (ent.DataOffset + ent.DataSize);
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                n = 0;
            else
                n++;
            int m = toolStripComboBox1.SelectedIndex;
            if (m == -1)
                return;
            if (CurrentView != 2)
                return;
            int clas = classes[m];
            for (int i = n; i < pcc.Exports.Count(); i++)
                if (pcc.Exports[i].ClassNameID == clas)
                {
                    listBox1.SelectedIndex = i;
                    return;
                }
        }

        private void exportBINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == 2 || CurrentView == 3))
                return;
            PCCObject.ExportEntry ent = pcc.Exports[n];
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            d.FileName = ent.ObjectName + ".bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                fs.Write(ent.Data, 0, ent.DataSize);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }
        private void dumpWholePCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc.DumpPCC(d.FileName);
            }
            MessageBox.Show("Done");
        }

        private void addNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Enter the name to add");

            if (String.IsNullOrEmpty(result))
                return;

            pcc.AddName(result);
            MessageBox.Show(result + ": has been added to the namelist", "Done", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void getNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string result = Microsoft.VisualBasic.Interaction.InputBox("Enter number of name (hex is fine too)", "Get Name", "", 0, 0);
            if (String.IsNullOrEmpty(result))
                return;

            if (result.Length <= 2 || (result[0] != '0' && result[0] != 'x'))
            {
                // regular int
                int val;
                try
                {
                    val = Convert.ToInt32(result);
                }
                catch
                {
                    return;
                }
                if (val < 0 || val > pcc.NameCount)
                    return;

                MessageBox.Show(pcc.GetName(val));
            }
            else
            {
                // hex input
                int val;
                try
                {
                    val = Convert.ToInt32(result.Substring(2), 16);
                }
                catch
                {
                    return;
                }
                if (val < 0 || val > pcc.NameCount)
                    return;
                MessageBox.Show(pcc.GetName(val));
            }
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            Interpreter();
        }

        public void Interpreter()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == 2 || CurrentView == 3))
                return;
            Interpreter2.Interpreter2 ip = new Interpreter2.Interpreter2();
            ip.MdiParent = this.MdiParent;
            ip.pcc = pcc;
            ip.Index = n;
            ip.InitInterpreter();
            ip.Show();
        }

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
            FindByString();   
        }

        private void FindByString()
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                n = 0;
            else
                n++;
            if (CurrentView != 2)
                return;
            string name = toolStripTextBox1.Text.ToLower();
            if (name == "")
                return;
            for (int i = n; i < pcc.Exports.Count(); i++)
                if (pcc.Exports[i].ObjectName.ToLower().Contains(name))
                {
                    listBox1.SelectedIndex = i;
                    return;
                }
        }

        private void toolStripTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)0xd)
                FindByString();
        }

        private void saveHexChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            int n = GetSelected();
            if (n == -1 || !(CurrentView == 2 || CurrentView == 3))
                return;
            MemoryStream m = new MemoryStream();
            for (int i = 0; i < hexBox1.ByteProvider.Length; i++)
                m.WriteByte(hexBox1.ByteProvider.ReadByte(i));
            PCCObject.ExportEntry ent = pcc.Exports[n];
            ent.Data = m.ToArray();
            ent.hasChanged = true;
            pcc.Exports[n] = ent;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            Preview();
        }

        private void button4_Click(object sender, EventArgs e)
        {

        }
    }
}
