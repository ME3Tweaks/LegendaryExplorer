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
using System.Diagnostics;
using System.Collections;

namespace ME1Explorer
{
    public partial class PCCEditor : Form
    {
        public int CurrentView = 2;
        public const int NAMES_VIEW = 0;
        public const int IMPORTS_VIEW = 1;
        public const int EXPORTS_VIEW = 2;
        public const int TREE_VIEW = 3;
        public PCCObject pcc;
        public List<int> ClassNames;
        public PropGrid pg;
        public List<string> RFiles;
        private int headerdiff;

        public PCCEditor()
        {
            InitializeComponent();
            LoadRecentList();
            RefreshRecent();
            if (RFiles != null && RFiles.Count != 0)
            {
                int index = RFiles.Count - 1;
                if (File.Exists(RFiles[index]))
                {
                    pcc = new PCCObject(RFiles[index]);
                    loadPCC();
                }
            }
            /*List<String> items = new List<String>();
            string[] files = Directory.GetFiles(@"D:\Origin Games\Mass Effect\BioGame\", "*.*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                string file2 = file.ToLower();
                if (!file2.EndsWith("u") && !file2.EndsWith("upk") && !file2.EndsWith("sfm")) {
                    continue;
                }
                Console.WriteLine(file2);

                pcc = new PCCObject(file2);
                //loadPCC();
                foreach (PCCObject.ExportEntry exp in pcc.Exports)
                {
                    if (exp.ClassName == "Function")
                    {
                        Function f = new Function(exp.Data,
                            pcc);
                        if ((f.GetFlagInt() & 0x00000200) > 0)
                        {
                            items.Add(exp.PackageFullName + "." + exp.ObjectName);
                        }
                    }
                }
            }

            string s = "Nothing...";
            items = new List<String>(items.Distinct().ToArray());
            foreach (String item in items)
            {
                s += item + "\n";
            }
            Clipboard.SetText(s);
            dumpBytecodeTable();*/
        }

        public new void Show()
        {
            base.Show();
            this.Text = "Package Editor (" + Path.GetFileName(pcc?.fullname) + ")";
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.u;*.upk;*sfm|*.u;*.upk;*sfm";
            if (d.ShowDialog() == DialogResult.OK)
            {
                AddRecent(d.FileName);
                SaveRecentList();
                pcc = new PCCObject(d.FileName);
                loadPCC();
            }
        }

        public void loadPCC()
        {
            CurrentView = EXPORTS_VIEW;
            ClassNames = new List<int>();
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                ClassNames.Add(pcc.Exports[i].idxClass);
            }
            List<string> names = ClassNames.Distinct().Select(x => pcc.getClassName(x)).ToList();
            names.Sort();
            classCombo.BeginUpdate();
            classCombo.Items.Clear();
            classCombo.Items.AddRange(names.ToArray());
            classCombo.EndUpdate();
            RefreshView();
            status2.Text = "@" + Path.GetFileName(Path.GetFileName(pcc.fullname));
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
            if (CurrentView == NAMES_VIEW)
            {
                for (int i = 0; i < pcc.Names.Count; i++)
                    listBox1.Items.Add(i.ToString() + " : " + pcc.Names[i]);
            }
            if (CurrentView == IMPORTS_VIEW)
            {
                for (int i = 0; i < pcc.Imports.Count; i++)
                    listBox1.Items.Add(i.ToString() + " : " + pcc.Imports[i].ObjectName);
            }
            string s;
            if (CurrentView == EXPORTS_VIEW)
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
            if (CurrentView == TREE_VIEW)
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
                    int Link = e.idxLink;
                    while (Link != 0)
                    {
                        LinkList.Add(Link);
                        if (Link > 0)
                            Link = pcc.Exports[Link - 1].idxLink;
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
                            Link = pcc.Exports[Link - 1].idxLink;
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

        public void dumpBytecodeTable()
        {
            string output = "";
            foreach (PCCObject.ExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Function")
                {
                    Function f = new Function(exp.Data,
                        pcc);
                    if (f.GetNatIdx() > 0)
                    {
                        output += "NATIVE_" + exp.ObjectName;
                        output += " = 0x" + f.GetNatIdx().ToString("X2") + ",\n";
                    }
                }
            }
            File.WriteAllText("natives.txt", output);
        }


        private TreeNode AddPathToTree(TreeNode t, List<int> LinkList)
        {
            string s = "";
            int idx, f;
            idx = LinkList[LinkList.Count() - 1];
            if (idx > 0)
                s = "(Exp)" + (idx - 1) + " : " + pcc.Exports[idx - 1].ObjectName + "(" + pcc.Exports[idx - 1].ClassName + ")";
            else
                s = "(Imp)" + (-idx - 1) + " : " + pcc.Imports[-idx - 1].ObjectName; // +"(" + pcc.Imports[-idx - 1].ClassName + ")";
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
            if (CurrentView == TREE_VIEW && treeView1.SelectedNode != null && treeView1.SelectedNode.Name != "")
                n = Convert.ToInt32(treeView1.SelectedNode.Name);
            if (CurrentView == EXPORTS_VIEW)
                n = listBox1.SelectedIndex;
            return n;
        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {
            CurrentView = NAMES_VIEW;
            RefreshView();
        }

        private void toolStripButton2_Click_1(object sender, EventArgs e)
        {
            CurrentView = IMPORTS_VIEW;
            RefreshView();
        }

        private void toolStripButton3_Click_1(object sender, EventArgs e)
        {
            CurrentView = EXPORTS_VIEW;
            RefreshView();
        }

        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            CurrentView = TREE_VIEW;
            RefreshView();
        }

        private void savePccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "ME1 Package File|*." + pcc.pccFileName.Split('.')[pcc.pccFileName.Split('.').Length - 1];
            if (d.ShowDialog() == DialogResult.OK)
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
            PreviewScript(n);

            if ((CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW) && n != -1)
            {
                PreviewInfo(n);
                int off = pcc.Imports.Count;
                RefreshCombos();
                comboBox1.SelectedIndex = pcc.Exports[n].idxObjectName;
                comboBox2.SelectedIndex = pcc.Exports[n].idxClass + off;
                comboBox3.SelectedIndex = pcc.Exports[n].idxLink + off;
                hb2.ByteProvider = new DynamicByteProvider(pcc.Exports[n].header);
            }
        }

        private void PreviewScript(int n)
        {
            if (pcc.Exports[n].ClassName == "Function")
            {
                if (!tabControl1.TabPages.ContainsKey(nameof(scriptTab)))
                {
                    tabControl1.TabPages.Add(scriptTab);
                }
                Function func = new Function(pcc.Exports[n].Data, pcc);
                try { scriptTextBox.Text = "ME1 Script decompiling is not fully functional. The below script may not be fully correct.\n\n"+func.ToRawText(); }
                catch (Exception e)
                {
                    scriptTextBox.Text = "Error decompiling script. ME1 Script decompiling is still under development...\n\n" + e.ToString();
                }
            }
            else if (tabControl1.TabPages.ContainsKey(nameof(scriptTab)))
            {
                tabControl1.TabPages.Remove(scriptTab);
            }
        }

        public void PreviewInfo(int n)
        {
            infoHeaderBox.Text = "Export Header";
            superclassTextBox.Visible = superclassLabel.Visible = true;
            textBox6.Visible = label6.Visible = true;
            textBox5.Visible = label5.Visible = true;
            infoExportDataBox.Visible = true;
            textBox1.Text = pcc.Exports[n].ObjectName;
            textBox2.Text = pcc.Exports[n].ClassName;
            superclassTextBox.Text = pcc.Exports[n].ClassParent;
            textBox3.Text = pcc.Exports[n].PackageFullName;
            textBox4.Text = pcc.Exports[n].header.Length + " bytes";
            textBox5.Text = pcc.Exports[n].indexValue.ToString();
            textBox6.Text = pcc.Exports[n].ArchtypeName;
            if (pcc.Exports[n].idxArchtype != 0)
                textBox6.Text += " (" + ((pcc.Exports[n].idxArchtype < 0) ? "imported" : "local") + " class) " + pcc.Exports[n].idxArchtype;
            textBox10.Text = "0x" + pcc.Exports[n].ObjectFlags.ToString("X16");
            textBox7.Text = pcc.Exports[n].DataSize + " bytes";
            textBox8.Text = "0x" + pcc.Exports[n].DataOffset.ToString("X8");
            textBox9.Text = pcc.Exports[n].DataOffset.ToString();
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

        public void PreviewProps()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
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
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            PCCObject.ExportEntry ent = pcc.Exports[n];
            hexBox1.ByteProvider = new DynamicByteProvider(ent.Data);
            Status.Text = ent.ClassName + " Offset: " + ent.DataOffset + " - " + (ent.DataOffset + ent.DataSize);
        }

        private void findClassButton_Click(object sender, EventArgs e)
        {
            if (CurrentView != EXPORTS_VIEW)
                return;
            int n = listBox1.SelectedIndex;
            string cls = classCombo.SelectedItem as string;
            int start;
            if (n == -1)
                start = 0;
            else
                start = n + 1;
            for (int i = start; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].ClassName == cls)
                {
                    listBox1.SelectedIndex = i;
                    break;
                }
        }

        private void exportBINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
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

        private void addNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
            {
                return;
            }
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name", "ME3 Explorer", "", 0, 0);
            if (result != "")
            {
                pcc.Names.Add(result);
                if (CurrentView == NAMES_VIEW)
                {
                    int scrollTo = listBox1.TopIndex + 1;
                    int selected = listBox1.SelectedIndex;
                    RefreshView();
                    listBox1.SelectedIndex = selected;
                    listBox1.TopIndex = scrollTo;
                }
                byte[] buff = BitConverter.GetBytes(pcc.Names.Count - 1);
                string s = "";
                for (int i = 0; i < 4; i++)
                {
                    s += buff[i].ToString("X2");
                }
                MessageBox.Show("\"" + result + "\" added at index " + (pcc.Names.Count - 1) + " (" + s + ")");
            }
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            Interpreter();
        }

        public void Interpreter()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
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
            if (CurrentView != EXPORTS_VIEW)
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

        private void recentToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            RefreshRecent();
        }

        private void RefreshRecent()
        {
            recentToolStripMenuItem.DropDownItems.Clear();
            if (RFiles.Count <= 0)
            {
                recentToolStripMenuItem.Enabled = false;
                return;
            }

            for (int i = 0; i < RFiles.Count; i++)
            {
                ToolStripMenuItem fr = new ToolStripMenuItem(RFiles[RFiles.Count() - i - 1], null, RecentFile_click);
                recentToolStripMenuItem.DropDownItems.Add(fr);
            }

        }

        private void LoadRecentList()
        {
            RFiles = new List<string>();
            RFiles.Clear();
            string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\ME1PCCEditorHistory.log";
            if (File.Exists(path))
            {
                BitConverter.IsLittleEndian = true;
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                byte[] buff = new byte[4]; ;
                fs.Read(buff, 0, 4);
                int count = BitConverter.ToInt32(buff, 0);
                for (int i = 0; i < count; i++)
                {
                    fs.Read(buff, 0, 4);
                    int len = BitConverter.ToInt32(buff, 0);
                    string s = "";
                    for (int j = 0; j < len; j++)
                        s += (char)fs.ReadByte();
                    AddRecent(s);
                }
                fs.Close();
            }
        }

        public void AddRecent(string s)
        {
            if (RFiles.Count < 10)
                RFiles.Add(s);
            else
            {
                RFiles.RemoveAt(0);
                RFiles.Add(s);
            }

        }

        private void SaveRecentList()
        {
            string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\ME1PCCEditorHistory.log";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
                File.Delete(path);
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            BitConverter.IsLittleEndian = true;
            byte[] buff = BitConverter.GetBytes(RFiles.Count);
            fs.Write(buff, 0, 4);
            for (int i = 0; i < RFiles.Count; i++)
            {
                buff = BitConverter.GetBytes(RFiles[i].Length);
                fs.Write(buff, 0, 4);
                for (int j = 0; j < RFiles[i].Length; j++)
                    fs.WriteByte((byte)RFiles[i][j]);
            }
            fs.Close();
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            //just load a file
            string s = sender.ToString();
            try
            {
                pcc = new PCCObject(s);
                loadPCC();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
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
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
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

        private void goToButton_Click(object sender, EventArgs e)
        {
            if (goToTextBox.Text == "")
                return;
            int n = Convert.ToInt32(goToTextBox.Text);
            goToNumber(n);
        }

        private void goToTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)0xD)
            {
                if (goToTextBox.Text == "")
                    return;
                int n = Convert.ToInt32(goToTextBox.Text);
                goToNumber(n);
            }
        }

        private void goToNumber(int n)
        {
            if (CurrentView == TREE_VIEW)
            {
                if (n >= -pcc.Imports.Count && n < pcc.Exports.Count)
                {
                    TreeNode[] nodes = treeView1.Nodes.Find(n.ToString(), true);
                    if (nodes.Length > 0)
                    {
                        treeView1.SelectedNode = nodes[0];
                        treeView1.Focus();
                    }
                }
            }
            else
            {
                if (n >= 0 && n < listBox1.Items.Count)
                {
                    listBox1.SelectedIndex = n;
                }
            }
        }

        private void replaceWithBINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (pcc == null || n < 0)
            {
                return;
            }
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.BIN|*.BIN";
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                byte[] buff = new byte[fs.Length];
                for (int i = 0; i < fs.Length; i++)
                    buff[i] = (byte)fs.ReadByte();
                fs.Close();
                pcc.Exports[n].Data = buff;
                MessageBox.Show("Done.");
            }
        }

        private void decreaseHeaderSizeButton_Click(object sender, EventArgs e)
        {
            PreviewScriptReduceHeader(GetSelected());
        }

        private void PreviewScriptReduceHeader(int n)
        {
            if (pcc.Exports[n].ClassName == "Function")
            {

                Function func = new Function(pcc.Exports[n].Data, pcc);
                headerdiff--;
                func.headerdiff = headerdiff;
                func.GetFlagInt();
                Debug.WriteLine("Header diff is now " + headerdiff);
                try { scriptTextBox.Text = func.ToRawText(); }
                catch (Exception e)
                {
                    scriptTextBox.Text = "Error decompiling script. ME1 Script decompiling is still under development...\n\n" + e.ToString();
                }
            }
            else if (tabControl1.TabPages.ContainsKey(nameof(scriptTab)))
            {
                tabControl1.TabPages.Remove(scriptTab);
            }
        }
    }
}
