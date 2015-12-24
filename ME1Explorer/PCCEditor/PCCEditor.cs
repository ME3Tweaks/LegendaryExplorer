using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ME1Explorer.Unreal;
using System.IO;
using Gibbed.IO;
using ME1Explorer.Unreal.Classes;

namespace ME1Explorer
{
    public partial class PCCEditor : Form
    {
        public int CurrentView = 2;
        public int NameIdx, ClassIdx, LinkIdx;
        public PCCObject pcc;
        public PropGrid pg;
        private TabPage scriptTab;
        public List<int> classes;

        public PCCEditor()
        {
            InitializeComponent();

        }

        public void RefreshView()
        {
            listBox1.BeginUpdate();
            listBox1.Items.Clear();
            if (pcc == null)
                return;
            int count = 0;
            if (CurrentView == 0)
            {
                foreach (string name in pcc.Names)
                    listBox1.Items.Add((count++).ToString("d6") + " : " + name);
            }
            if (CurrentView == 1)
            {
                foreach (PCCObject.ImportEntry imp in pcc.Imports)
                    listBox1.Items.Add((count++).ToString("d6") + " : " + pcc.FollowLink(imp.link) + imp.Name);
            }
            string s;
            if (CurrentView == 2)
            {
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    s = "";
                    if (pcc.Exports[i].PackageFullName != "Class" && pcc.Exports[i].PackageFullName != "Package")
                        s += pcc.Exports[i].PackageFullName + ".";
                    s += pcc.Exports[i].ObjectName;
                    listBox1.Items.Add(i.ToString() + " : " + s);
                }
            }
            if (CurrentView == 3)
            {
                treeView1.BeginUpdate();
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
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
                treeView1.EndUpdate();
            }
            else
            {
                treeView1.Visible = false;
                listBox1.Visible = true;
            }
            listBox1.EndUpdate();
            toolStripComboBox1.Items.Clear();
            foreach (int index in classes)
                toolStripComboBox1.Items.Add(pcc.GetClass(index));

        }

        private TreeNode AddPathToTree(TreeNode t, List<int> LinkList)
        {
            string s = "";
            int idx, f;
            idx = LinkList[LinkList.Count() - 1];
            if (idx > 0)
                s = "(Exp)" + (idx - 1) + " : " + pcc.Exports[idx - 1].ObjectName + "(" + pcc.Exports[idx - 1].ClassName + ")";
            else
                s = "(Imp)" + (-idx - 1) + " : " + pcc.Imports[-idx - 1].Name; //+ "(" + pcc.Imports[-idx - 1].ClassName + ")";
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

        private void openPccToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.u;*.upk;*sfm|*.u;*.upk;*sfm";
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

        public void RefreshCombos()
        {
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            comboBox3.Items.Clear();
            List<string> Classes = new List<string>();
            for (int i = pcc.Imports.Count - 1; i >= 0; i--)
                Classes.Add(-(i + 1) + " : " + pcc.Imports[i].ObjectName);
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

        public void SetView(int n)
        {
            CurrentView = n;
            switch (n)
            {
                case 0:
                    toolStripButton1.Checked = true;
                    toolStripButton2.Checked = false;
                    toolStripButton3.Checked = false;
                    Button5.Checked = false;
                    break;
                case 1:
                    toolStripButton1.Checked = false;
                    toolStripButton2.Checked = true;
                    toolStripButton3.Checked = false;
                    Button5.Checked = false;
                    break;
                case 3:
                    toolStripButton1.Checked = false;
                    toolStripButton2.Checked = false;
                    toolStripButton3.Checked = false;
                    Button5.Checked = true;
                    break;
                default:
                case 2:
                    toolStripButton1.Checked = false;
                    toolStripButton2.Checked = false;
                    toolStripButton3.Checked = true;
                    Button5.Checked = false;
                    break;
            }

        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {
            SetView(0);
            RefreshView();
        }

        private void toolStripButton2_Click_1(object sender, EventArgs e)
        {
            SetView(1);
            RefreshView();
        }

        private void toolStripButton3_Click_1(object sender, EventArgs e)
        {
            SetView(2);
            RefreshView();
        }

        private void Button5_Click(object sender, EventArgs e)
        {
            SetView(3);
            RefreshView();
        }

        private void savePccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            //d.Filter = "*.u;*.upk;*sfm|*.u;*.upk;*sfm";
            d.Filter = "ME1 Package File|*." + pcc.pccFileName.Split('.')[pcc.pccFileName.Split('.').Length - 1];
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

        private void breakToolStripMenuItem_Click(object sender, EventArgs e)
        {
            throw new Exception();
        }

        private void Preview()
        {
            PreviewProps();
            PreviewRaw();
            PreviewInfo();
            int n = -1;
            if (CurrentView == 3 && treeView1.SelectedNode != null && treeView1.SelectedNode.Name != "")
                n = Convert.ToInt32(treeView1.SelectedNode.Name);
            else
                n = listBox1.SelectedIndex;
            if (CurrentView == 1)
            {
                hb2.ByteProvider = new DynamicByteProvider(pcc.Imports[n].raw);
            }
            if ((CurrentView == 2 || CurrentView == 3) && n != -1)
        {
                int off = pcc.Imports.Count;
                NameIdx = pcc.Exports[n].ObjectNameID;
                ClassIdx = pcc.Exports[n].ClassNameID;
                LinkIdx = pcc.Exports[n].LinkID;
                RefreshCombos();
                comboBox1.SelectedIndex = NameIdx;
                comboBox2.SelectedIndex = ClassIdx + off;
                comboBox3.SelectedIndex = LinkIdx + off;
                hb2.ByteProvider = new DynamicByteProvider(pcc.Exports[n].info);
        }
            if (n >= 0)
        {
            Status.Text = "Class: " + pcc.Exports[n].ClassName + " Flags: 0x" + pcc.Exports[n].flagint.ToString("X8");
            }
            else
            {
                Status.Text = "";
            }

        }

        public void PreviewProps()
                {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == 2 || CurrentView == 3))
                return;
            List<PropertyReader.Property> p;
            switch (pcc.Exports[n].ClassName)
                    {
                default:
                    byte[] buff = pcc.Exports[n].Data;
                    p = PropertyReader.getPropList(pcc, buff);
                    break;
            }
            pg = new PropGrid();
            propGrid.SelectedObject = pg;
            pg.Add(new CustomProperty("Name", "_Meta", pcc.Exports[n].ObjectName, typeof(string), true, true));
            pg.Add(new CustomProperty("Class", "_Meta", pcc.Exports[n].ClassName, typeof(string), true, true));
            pg.Add(new CustomProperty("Data Offset", "_Meta", pcc.Exports[n].DataOffset, typeof(int), true, true));
            pg.Add(new CustomProperty("Data Size", "_Meta", pcc.Exports[n].DataSize, typeof(int), true, true));
            for (int l = 0; l < p.Count; l++)
                pg.Add(PropertyReader.PropertyToGrid(p[l], pcc));
            propGrid.Refresh();
            propGrid.ExpandAllGridItems();
        }

        private void PreviewRaw()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == 2 || CurrentView == 3))
                return;  
            PCCObject.ExportEntry ent = pcc.Exports[n];
            hb1.ByteProvider = new DynamicByteProvider(ent.Data);
            //Status.Text = ent.ClassName + " Offset: " + ent.DataOffset + " - " + (ent.DataOffset + ent.DataSize);
        }

        public void PreviewInfo()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == 2 || CurrentView == 3))
                return;
            textBox1.Text = pcc.Exports[n].ObjectName;
            textBox2.Text = pcc.Exports[n].ClassName;
            textBox3.Text = pcc.Exports[n].PackageFullName;
            textBox4.Text = pcc.Exports[n].info.Length + " bytes";
            textBox5.Text = pcc.Exports[n].indexValue.ToString();
            textBox6.Text = pcc.Exports[n].ArchtypeName;
            if (pcc.Exports[n].idxArchtypeName != 0)
                textBox6.Text += " (" + ((pcc.Exports[n].idxArchtypeName < 0) ? "imported" : "local") + " class)";
            textBox10.Text = "0x" + pcc.Exports[n].ObjectFlags.ToString("X16");
            textBox7.Text = pcc.Exports[n].DataSize + " bytes";
            textBox8.Text = "0x" + pcc.Exports[n].DataOffset.ToString("X8");
            textBox9.Text = pcc.Exports[n].DataOffset.ToString();
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

        private void dumpRawExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n < 0)
                return;
            PCCObject.ExportEntry exp = pcc.Exports[n];
            SaveFileDialog d = new SaveFileDialog();
            //d.Filter = "*.u;*.upk;*sfm|*.u;*.upk;*sfm";
            d.Filter = "Binary File|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using (FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write))
                {
                    fs.WriteBytes(exp.Data);
                }
                MessageBox.Show("Done.");
            }
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

        private void saveHexChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n < 0)
                return;
            PCCObject.ExportEntry exp = pcc.Exports[n];
            MemoryStream m = new MemoryStream();
            for (int i = 0; i < hb1.ByteProvider.Length; i++)
                m.WriteByte(hb1.ByteProvider.ReadByte(i));
            exp.Data = m.ToArray();
            exp.DataSize = (int)m.Length;
            pcc.Exports[n] = exp;
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            Interpreter();
        }

        public void Interpreter()
        {
            int n = GetSelected();
            if (n == -1)
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

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            Preview();
        }

        private void rawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Preview();
        }

        private void propertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Preview();
        }
    }
}
