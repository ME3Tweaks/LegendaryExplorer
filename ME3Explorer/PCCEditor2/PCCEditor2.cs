using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using Be.Windows.Forms;
using ME3Explorer.Unreal.Classes;
using AmaroK86.ImageFormat;
using KFreonLib.MEDirectories;
using KFreonLib.PCCObjects;
using KFreonLib.Textures;
using UsefulThings;
using System.Diagnostics;

namespace ME3Explorer
{
    public partial class PCCEditor2 : Form
    {
        public PCCObject pcc;
        public int CurrentView; //0 = Names, 1 = Imports, 2 = Exports
        public const int NAMES_VIEW = 0;
        public const int IMPORTS_VIEW = 1;
        public const int EXPORTS_VIEW = 2;
        public const int TREE_VIEW = 3;


        public int PreviewStyle; //0 = raw, 1 = properties, 2 = Script
        public int NameIdx, ClassIdx, LinkIdx;

        public PropGrid pg;
        private TabPage scriptTab;
        private string currentFile;

        public List<int> ClassNames;

        public bool IsFromDLC = false;
        public string DLCPath;
        public string inDLCFilename;



        public PCCEditor2()
        {
            InitializeComponent();
            LoadRecentList();
            RefreshRecent();
            if (RFiles != null && RFiles.Count != 0)
            {
                int index = RFiles.Count - 1;
                if (File.Exists(RFiles[index]))
                    LoadFile(RFiles[index]);
            }

            scriptTab = tabControl1.TabPages["Script"];
            tabControl1.TabPages.Remove(scriptTab);

            saveHexChangesToolStripMenuItem.Enabled = false;
            tabControl1.SelectedIndexChanged += tabControl1_SelectedIndexChanged;

        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                LoadFile(d.FileName);
        }

        public void LoadFile(string s, bool isfromdlc = false)
        {
            try
            {
                currentFile = s;
                AddRecent(s);
                SaveRecentList();
                pcc = new PCCObject(s);
                SetView(2);
                RefreshView();
                InitStuff();
                if (!isfromdlc)
                    status2.Text = "@" + Path.GetFileName(s);
                else
                    status2.Text = "@" + inDLCFilename;
                RefreshCombos();
                IsFromDLC = isfromdlc;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error:\n" + e.Message);
            }
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

        public void InitStuff()
        {
            if (pcc == null)
                return;
            ClassNames = new List<int>();
            bool found;
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                found = false;
                for (int j = 0; j < ClassNames.Count; j++)
                    if (ClassNames[j] == pcc.Exports[i].idxClassName)
                        found = true;
                if (!found)
                    ClassNames.Add(pcc.Exports[i].idxClassName);
            }
            bool finished = false;
            while (!finished)
            {
                finished = true;
                for (int j = 0; j < ClassNames.Count - 1; j++)
                    if (String.Compare(pcc.getClassName(ClassNames[j]), pcc.getClassName(ClassNames[j + 1])) > 0)
                    {
                        finished = false;
                        int t = ClassNames[j];
                        ClassNames[j] = ClassNames[j + 1];
                        ClassNames[j + 1] = t;
                    }
            }
            combo1.Items.Clear();
            for (int i = 0; i < ClassNames.Count; i++)
                combo1.Items.Add(pcc.getClassName(ClassNames[i]));

        }

        public void SetView(int n)
        {
            CurrentView = n;
            switch (n)
            {
                case NAMES_VIEW:
                    Button1.Checked = true;
                    Button2.Checked = false;
                    Button3.Checked = false;
                    Button5.Checked = false;
                    break;
                case IMPORTS_VIEW:
                    Button1.Checked = false;
                    Button2.Checked = true;
                    Button3.Checked = false;
                    Button5.Checked = false;
                    break;
                case TREE_VIEW:
                    Button1.Checked = false;
                    Button2.Checked = false;
                    Button3.Checked = false;
                    Button5.Checked = true;
                    break;
                case EXPORTS_VIEW:
                default:
                    Button1.Checked = false;
                    Button2.Checked = false;
                    Button3.Checked = true;
                    Button5.Checked = false;
                    break;
            }
            
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
            cloneObjectToolStripMenuItem.Enabled = false;
            if (CurrentView == NAMES_VIEW)
            {
                for (int i = 0; i < pcc.Names.Count; i++)
                {
                    listBox1.Items.Add(i.ToString() + " : " + pcc.Names[i]);
                }
            }
            if (CurrentView == IMPORTS_VIEW)
            {
                for (int i = 0; i < pcc.Imports.Count; i++)
                {
                    string importStr = i.ToString() + " (0x" + (pcc.ImportOffset + (i * PCCObject.ImportEntry.
                        byteSize)).ToString("X4") + "): (" + pcc.Imports[i].PackageFile + ") ";
                    if (pcc.Imports[i].PackageFullName != "Class" && pcc.Imports[i].PackageFullName != "Package")
                    {
                        importStr += pcc.Imports[i].PackageFullName + ".";
                    }
                    importStr += pcc.Imports[i].ObjectName;
                    listBox1.Items.Add(importStr);
                }
            }
            string s;
            if (CurrentView == EXPORTS_VIEW)
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    cloneObjectToolStripMenuItem.Enabled = true;
                    s = "";
                    if (scanningCoalescedBits && pcc.Exports[i].likelyCoalescedVal)
                    {
                        s += "[C] ";
                    }
                    if (pcc.Exports[i].PackageFullName != "Class" && pcc.Exports[i].PackageFullName != "Package")
                        s += pcc.Exports[i].PackageFullName + ".";
                    s += pcc.Exports[i].ObjectName;
                    if (pcc.Exports[i].ClassName == "ObjectProperty" || pcc.Exports[i].ClassName == "StructProperty")
                    {
                        //attempt to find type
                        byte[] data = pcc.Exports[i].Data;
                        int importindex = BitConverter.ToInt32(data, data.Length - 4);
                        if (importindex < 0)
                        {
                            //import
                            importindex *= -1;
                            if (importindex > 0) importindex--;
                            if (importindex <= pcc.Imports.Count)
                            {
                                s += " (" + pcc.Imports[importindex].ObjectName + ")";
                            }
                        }
                        else
                        {
                            //export
                            if (importindex > 0) importindex--;
                            if (importindex <= pcc.Exports.Count)
                            {
                                s += " [" + pcc.Exports[importindex].ObjectName + "]";
                            }
                        }
                    }
                    listBox1.Items.Add(i.ToString() + " : " + s);
                }
            if (CurrentView == TREE_VIEW)
            {
                for (int i = 0; i < pcc.Exports.Count; i++)
                {

                    cloneObjectToolStripMenuItem.Enabled = true;
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

                    cloneObjectToolStripMenuItem.Enabled = true;
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
                            Link = pcc.Imports[-Link - 1].idxLink;
                    }
                    t = AddPathToTree(t, LinkList);
                }
                for (int i = 0; i < pcc.Imports.Count; i++)
                {

                    cloneObjectToolStripMenuItem.Enabled = true;
                    PCCObject.ImportEntry e = pcc.Imports[i];
                    List<int> LinkList = new List<int>();
                    LinkList.Add(-(i + 1));
                    int Link = e.idxLink;
                    while (Link != 0)
                    {
                        LinkList.Add(Link);
                        if (Link > 0)
                            Link = pcc.Exports[Link - 1].idxLink;
                        else
                            Link = pcc.Imports[-Link - 1].idxLink;
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
        }

        private TreeNode AddPathToTree(TreeNode t, List<int> LinkList)
        {
            string s = "";
            int idx, f;
            idx = LinkList[LinkList.Count() - 1];
            if (idx > 0)
                s = "(Exp)" + (idx - 1) + " : " + pcc.Exports[idx - 1].ObjectName + "(" + pcc.Exports[idx - 1].ClassName + ")";
            else
                s = "(Imp)" + (-idx - 1) + " : " + pcc.Imports[-idx - 1].ObjectName + "(" + pcc.Imports[-idx - 1].ClassName + ")";
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
                
        private void Button3_Click(object sender, EventArgs e)
        {
            SetView(2);
            RefreshView();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            SetView(0);
            RefreshView();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            SetView(1);
            RefreshView();
        }

        private void tabControl1_SelectedIndexChanged(Object sender, EventArgs e)
        {
            // keep disabled unless we're on the hex tab:
            if (tabControl1.SelectedIndex == 0 && listBox1.SelectedItem != null)
                saveHexChangesToolStripMenuItem.Enabled = true;
            else
                saveHexChangesToolStripMenuItem.Enabled = false;
        }

        public void Preview()
        {
            PreviewInfo();
            PreviewRaw();
            PreviewProps();
            PreviewImport();
            int n = -1;
            if (CurrentView == TREE_VIEW && treeView1.SelectedNode != null && treeView1.SelectedNode.Name != "")
                n = Convert.ToInt32(treeView1.SelectedNode.Name);
            else
                n = listBox1.SelectedIndex;

            if (CurrentView == IMPORTS_VIEW)
            {
                hb2.ByteProvider = new DynamicByteProvider(pcc.Imports[n].data);
                status2.Text = pcc.Imports[n].Link.ToString();
            }
            if ((CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW) && n!=-1)
            {
                if (tabControl1.TabPages.ContainsKey("Script"))
                {
                    scriptTab = tabControl1.TabPages["Script"];
                    tabControl1.TabPages.Remove(scriptTab);
                }

                if (pcc.Exports[n].ClassName == "Function")
                {
                    tabControl1.TabPages.Add(scriptTab);
                    PreviewSript();
                }
                int off = pcc.Imports.Count;
                NameIdx = pcc.Exports[n].idxObjectName;
                ClassIdx = pcc.Exports[n].idxClassName;
                LinkIdx = pcc.Exports[n].idxLink;
                RefreshCombos();
                comboBox1.SelectedIndex = NameIdx;
                comboBox2.SelectedIndex = ClassIdx + off;
                comboBox3.SelectedIndex = LinkIdx + off;
                hb2.ByteProvider = new DynamicByteProvider(pcc.Exports[n].info);
            }
        }

        public void PreviewImport()
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || CurrentView != IMPORTS_VIEW)
                return;
            UpdateStatusIm(n);
        }

        public void PreviewInfo()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            textBox1.Text = pcc.Exports[n].ObjectName;
            textBox2.Text = pcc.Exports[n].ClassName;
            superclassTextBox.Text = pcc.Exports[n].ClassParent;
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

        public void Previewtest()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            //propGrid.Visible = false;
            //hb1.Visible = false;
            //rtb1.Visible = true;
            //treeView1.Visible = false;
            if (pcc.Exports[n].ClassName.Contains("BlockingVolume") || pcc.Exports[n].ClassName.Contains("SFXDoor"))
            {
                List<ME3Explorer.Unreal.PropertyReader.Property> props = ME3Explorer.Unreal.PropertyReader.getPropList(pcc, pcc.Exports[n].Data);
                foreach (ME3Explorer.Unreal.PropertyReader.Property p in props)
                {
                    if (pcc.getNameEntry(p.Name) == "location")
                    {
                        BitConverter.IsLittleEndian = true;
                        float x = BitConverter.ToSingle(p.raw, 32);
                        float y = BitConverter.ToSingle(p.raw, 36);
                        float z = BitConverter.ToSingle(p.raw, 40);
                        rtb1.Text = "Location : (" + x + "; " + y + "; " + z + ")";
                    }
                }
            }
            else
                PreviewRaw();
        }
        public void PreviewSript()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            //propGrid.Visible = false;
            //hb1.Visible = false;
            //rtb1.Visible = true;
            //treeView1.Visible = false;
            if (pcc.Exports[n].ClassName == "Function")
            {
                Function func = new Function(pcc.Exports[n].Data, pcc);
                rtb1.Text = func.ToRawText();
                UpdateStatusEx(n);
            }
            else
                PreviewRaw();            
        }

        public void PreviewRaw()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;           
            //propGrid.Visible = false;
            //hb1.Visible = true;
            //rtb1.Visible = false;
            //treeView1.Visible = false;
            //byte[] total = pcc.Exports[n].info.Concat(pcc.Exports[n].Data).ToArray();
            hb1.ByteProvider = new DynamicByteProvider(pcc.Exports[n].Data);
            UpdateStatusEx(n);
        }

        public void UpdateStatusEx(int n)        
        {
            toolStripStatusLabel1.Text = "Class:" + pcc.Exports[n].ClassName + " Flags: 0x" + pcc.Exports[n].ObjectFlags.ToString("X16") + " ";
            toolStripStatusLabel1.ToolTipText = "";
            foreach (string row in UnrealFlags.flagdesc)
            {
                string[] t = row.Split(',');
                long l = long.Parse(t[1].Trim(), System.Globalization.NumberStyles.HexNumber);
                if ((l & pcc.Exports[n].ObjectFlags) != 0)
                {
                    toolStripStatusLabel1.Text += "[" + t[0].Trim() + "] ";
                    toolStripStatusLabel1.ToolTipText += "[" + t[0].Trim() + "] : " + t[2].Trim() + "\n";
                }
            }
        }

        public void UpdateStatusIm(int n)
        {
            toolStripStatusLabel1.Text = "Class:" + pcc.Imports[n].ClassName + " Flags: 0x" + pcc.Imports[n].ObjectFlags.ToString("X16") + " ";
            toolStripStatusLabel1.ToolTipText = "";
            foreach (string row in UnrealFlags.flagdesc)
            {
                string[] t = row.Split(',');
                long l = long.Parse(t[1].Trim(), System.Globalization.NumberStyles.HexNumber);
                if ((l & pcc.Imports[n].ObjectFlags) != 0)
                {
                    toolStripStatusLabel1.Text += "[" + t[0].Trim() + "] ";
                    toolStripStatusLabel1.ToolTipText += "[" + t[0].Trim() + "] : " + t[2].Trim() + "\n";
                }
            }
        }

        public void PreviewProps()
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            List<ME3Explorer.Unreal.PropertyReader.Property> p;
            //propGrid.Visible = true;
            //hb1.Visible = false;
            //rtb1.Visible = false;
            //treeView1.Visible = false;
            switch (pcc.Exports[n].ClassName)
            {
                default:
                    byte[] buff = pcc.Exports[n].Data;
                    p = ME3Explorer.Unreal.PropertyReader.getPropList(pcc, buff);
                    break;
            }
            pg = new PropGrid();
            propGrid.SelectedObject = pg;
            pg.Add(new CustomProperty("Name", "_Meta", pcc.Exports[n].ObjectName, typeof(string), true, true));
            pg.Add(new CustomProperty("Class", "_Meta", pcc.Exports[n].ClassName, typeof(string), true, true));
            pg.Add(new CustomProperty("Data Offset", "_Meta", pcc.Exports[n].DataOffset, typeof(int), true, true));
            pg.Add(new CustomProperty("Data Size", "_Meta", pcc.Exports[n].DataSize, typeof(int), true, true));
            for (int l = 0; l < p.Count; l++)
                pg.Add(ME3Explorer.Unreal.PropertyReader.PropertyToGrid(p[l], pcc));            
            propGrid.Refresh();
            propGrid.ExpandAllGridItems();
            UpdateStatusEx(n);
        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            tabControl1_SelectedIndexChanged(null, null);
            Preview();
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

        private void SetSelected(int n)
        {
            if (CurrentView == EXPORTS_VIEW)
                listBox1.SelectedIndex = n;
            else if (CurrentView == TREE_VIEW)
                treeView1.SelectedNode = treeView1.Nodes[n];
        }

        private void propGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return; 
            string name = e.ChangedItem.Label;
            GridItem parent = e.ChangedItem.Parent;
            //if (parent != null) name = parent.Label;
            if (parent.Label == "data")
            {
                GridItem parent2 = parent.Parent;
                if (parent2 != null) name = parent2.Label;
            }
            if (name == "nameindex")
            {
                name = parent.Label;
            }
            byte[] buff = pcc.Exports[n].Data;
            List<ME3Explorer.Unreal.PropertyReader.Property> p = ME3Explorer.Unreal.PropertyReader.getPropList(pcc, buff);
            int m = -1;
            for (int i = 0; i < p.Count; i++)
                if (pcc.Names[p[i].Name] == name)
                    m = i;
            if (m == -1)
                return;
            PCCObject.ExportEntry ent = pcc.Exports[n];
            byte[] buff2;
            switch (p[m].TypeVal)
            {
                case ME3Explorer.Unreal.PropertyReader.Type.BoolProperty:
                    byte res = 0;
                    if ((bool)e.ChangedItem.Value == true)
                        res = 1;
                    ent.Data[p[m].offsetval] = res;
                    break;
                case ME3Explorer.Unreal.PropertyReader.Type.FloatProperty:
                    buff2 = BitConverter.GetBytes((float)e.ChangedItem.Value);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case ME3Explorer.Unreal.PropertyReader.Type.IntProperty:
                case ME3Explorer.Unreal.PropertyReader.Type.StringRefProperty:                
                    int newv = Convert.ToInt32(e.ChangedItem.Value);
                    int oldv = Convert.ToInt32(e.OldValue);
                    buff2 = BitConverter.GetBytes(newv);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case ME3Explorer.Unreal.PropertyReader.Type.StructProperty:
                    if (e.ChangedItem.Value.GetType() == typeof(int))
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        if (e.ChangedItem.Label == "nameindex")
                        {
                            int val1 = Convert.ToInt32(e.ChangedItem.Value);
                            buff2 = BitConverter.GetBytes(val1);
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + i] = buff2[i];
                            int t = listBox1.SelectedIndex;
                            listBox1.SelectedIndex = -1;
                            listBox1.SelectedIndex = t;
                        }
                        else
                        {
                            string sidx = e.ChangedItem.Label.Replace("[", "");
                            sidx = sidx.Replace("]", "");
                            int index = Convert.ToInt32(sidx);
                            buff2 = BitConverter.GetBytes(val);
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + i + index * 4 + 8] = buff2[i]; 
                            int t = listBox1.SelectedIndex;
                            listBox1.SelectedIndex = -1;
                            listBox1.SelectedIndex = t;
                        }
                    }
                    break;
                case ME3Explorer.Unreal.PropertyReader.Type.ByteProperty:
                case ME3Explorer.Unreal.PropertyReader.Type.NameProperty:
                    if (e.ChangedItem.Value.GetType() == typeof(int))
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        buff2 = BitConverter.GetBytes(val);
                        for (int i = 0; i < 4; i++)
                            ent.Data[p[m].offsetval + i] = buff2[i];
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    break;
                case ME3Explorer.Unreal.PropertyReader.Type.ObjectProperty:
                    if (e.ChangedItem.Value.GetType() == typeof(int))
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        buff2 = BitConverter.GetBytes(val);
                        for (int i = 0; i < 4; i++)
                            ent.Data[p[m].offsetval + i] = buff2[i];
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    break;
                default:
                    return;
            }
            pcc.Exports[n] = ent;
            propGrid.ExpandAllGridItems();
            Preview();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc.altSaveToFile(d.FileName, true);
                MessageBox.Show("Done");
            }
        }

        private void saveHexChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW)) 
                return;
            MemoryStream m = new MemoryStream();
            for (int i = 0; i < hb1.ByteProvider.Length; i++)
                m.WriteByte(hb1.ByteProvider.ReadByte(i));
            PCCObject.ExportEntry ent = pcc.Exports[n];
            ent.Data = m.ToArray();
            pcc.Exports[n] = ent;

            Preview();
        }

        private void extractToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void autoRawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PreviewStyle = 0;
            Preview();
        }

        private void autoPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PreviewStyle = 1;
            Preview();
        }

        private void Search()
        {
            if (pcc == null)
                return;
            int n = listBox1.SelectedIndex;
            if (toolStripTextBox1.Text == "")
                return;
            int start;
            if (n == -1)
                start = 0;
            else
                start = n + 1;
            
            if (CurrentView == NAMES_VIEW)
            {
                for (int i = start; i < pcc.Names.Count; i++)
                    if (pcc.Names[i].ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == IMPORTS_VIEW)
            {
                for (int i = start; i < pcc.Exports.Count; i++)
                    if (pcc.Imports[i].ObjectName.ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == EXPORTS_VIEW)
            {
                for (int i = start; i < pcc.Exports.Count; i++)
                    if (pcc.Exports[i].ObjectName.ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
        }

        private void Find()
        {
            if (pcc == null)
                return;
            int n = listBox1.SelectedIndex;
            if (CurrentView != 2)
                return;
            if (combo1.SelectedIndex == -1)
                return;
            int cls = ClassNames[combo1.SelectedIndex];
            int start;
            if (n == -1)
                start = 0;
            else
                start = n + 1;
            for (int i = start; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].idxClassName == cls)
                {
                    listBox1.SelectedIndex = i;
                    break;
                }
        }

        private void scriptsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PreviewStyle = 2;
            Preview();
        }

        private void combo1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                Find();
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Find();
        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {            
            Find();
        }

        public List<string> RFiles;
        private bool scanningCoalescedBits;

        private void LoadRecentList()
        {
            RFiles = new List<string>();
            RFiles.Clear();
            string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\history.log";
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
        private void SaveRecentList()
        {
            string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\history.log";
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

        private void RecentFile_click(object sender, EventArgs e)
        {
            //just load a file
            string s = sender.ToString();
            pcc = new PCCObject(s);
            SetView(2);
            RefreshView();
            InitStuff();
            this.Text = "PCC Editor 2.0 (" + Path.GetFileName(s) + ")";
        }

        private void addNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name", "ME3 Explorer", "", 0, 0);
            if (result != "")
            {
                pcc.Names.Add(result);
                MessageBox.Show("Done.");
            }
        }

        private void recentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshRecent();
        }

        private void sequenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PreviewStyle = 3;
            Preview();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            Search();
        }

        private void toolStripTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                Search();
        }

        private void PCCEditor2_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }

        private void getDumpToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            DumpBin();
        }

        public void DumpBin()
        {
            if (pcc == null)
                return;
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW)) 
                return;
            List<ME3Explorer.Unreal.PropertyReader.Property> prop = ME3Explorer.Unreal.PropertyReader.getPropList(pcc, pcc.Exports[n].Data);
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            d.FileName = pcc.Exports[n].ObjectName + ".bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                byte[] buff = pcc.Exports[n].Data;
                int start = 0;
                if (prop.Count > 0)
                    start = prop[prop.Count - 1].offend;
                for (int i = start; i < buff.Length; i++)
                    fs.WriteByte(buff[i]);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        private void editInInterpreterToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            Interpreter();
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            if (gotonumber.Text == "")
                return;
            int i = Convert.ToInt32(gotonumber.Text);
            if (i >= 0 && i < listBox1.Items.Count)
                listBox1.SelectedIndex = i;
        }

        private void gotonumber_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)0xD)
            {
                int i = Convert.ToInt32(gotonumber.Text);
                if (i >= 0 && i < listBox1.Items.Count)
                    listBox1.SelectedIndex = i;
            }
        }

        private void getDumpToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.BIN|*.BIN";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                fs.Write(pcc.Exports[n].Data, 0, pcc.Exports[n].Data.Length);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        private void replaceWithBINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW)) 
                return;
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.BIN|*.BIN";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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

        private void cloneObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            CloneDialog cl = new CloneDialog();
            cl.pcc = pcc;
            cl.refForm = this;
            cl.ObjectIndex = n;
            cl.MdiParent = this.MdiParent;
            cl.Show();
            cl.WindowState = FormWindowState.Maximized;
        }

        private void altSavetestingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc.saveToFile(d.FileName, true);
                MessageBox.Show("Done");
            }
        }

        private void testToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PreviewStyle = 3;
            Preview();
        }

        private void editBlockingVolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            //propGrid.Visible = false;
            //hb1.Visible = false;
            //rtb1.Visible = true;
            //treeView1.Visible = false;
            if (pcc.Exports[n].ClassName.Contains("BlockingVolume") || pcc.Exports[n].ClassName.Contains("SFXDoor"))
            {
                List<ME3Explorer.Unreal.PropertyReader.Property> props = ME3Explorer.Unreal.PropertyReader.getPropList(pcc, pcc.Exports[n].Data);
                foreach (ME3Explorer.Unreal.PropertyReader.Property p in props)
                {
                    if (pcc.getNameEntry(p.Name) == "location")
                    {
                        BitConverter.IsLittleEndian = true;
                        float x = BitConverter.ToSingle(p.raw, 32);
                        float y = BitConverter.ToSingle(p.raw, 36);
                        float z = BitConverter.ToSingle(p.raw, 40);
                        rtb1.Text = "Location : (" + x + "; " + y + "; " + z + ")";
                        Application.DoEvents();
                        string nx = Microsoft.VisualBasic.Interaction.InputBox("New X Value:", "Edit Location", x.ToString());
                        string ny = Microsoft.VisualBasic.Interaction.InputBox("New Y Value:", "Edit Location", y.ToString());
                        string nz = Microsoft.VisualBasic.Interaction.InputBox("New Z Value:", "Edit Location", z.ToString());
                        if (nx == "" || ny == "" || nz == "")
                            return;
                        x = Convert.ToSingle(nx);
                        y = Convert.ToSingle(ny);
                        z = Convert.ToSingle(nz);
                        byte[] buff = pcc.Exports[n].Data;
                        int offset = p.offend - 12;
                        byte[] tmp = BitConverter.GetBytes(x);
                        for (int i = 0; i < 4; i++)
                            buff[offset + i] = tmp[i];
                        tmp = BitConverter.GetBytes(y);
                        for (int i = 0; i < 4; i++)
                            buff[offset + i + 4] = tmp[i];
                        tmp = BitConverter.GetBytes(z);
                        for (int i = 0; i < 4; i++)
                            buff[offset + i + 8] = tmp[i];
                        pcc.Exports[n].Data = buff;
                        MessageBox.Show("Done.");
                    }
                }
            }
        }

        private void createBinaryReplaceJobFromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            if (!IsFromDLC)
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.bin|*.bin";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                    byte[] buff = new byte[fs.Length];
                    int cnt;
                    int sum = 0;
                    while ((cnt = fs.Read(buff, sum, buff.Length - sum)) > 0) sum += cnt;
                    fs.Close();
                    KFreonLib.Scripting.ModMaker.ModJob mj = new KFreonLib.Scripting.ModMaker.ModJob();
                    string currfile = Path.GetFileName(pcc.pccFileName);
                    mj.data = buff;
                    mj.Name = "Binary Replacement for file \"" + currfile + "\" in Object #" + n + " with " + buff.Length + " bytes of data";
                    string loc = Path.GetDirectoryName(Application.ExecutablePath);
                    string template = System.IO.File.ReadAllText(loc + "\\exec\\JobTemplate_Binary2.txt");
                    template = template.Replace("**m1**", n.ToString());
                    template = template.Replace("**m2**", currfile);
                    mj.Script = template;
                    KFreonLib.Scripting.ModMaker.JobList.Add(mj);
                    MessageBox.Show("Done");
                }
            }
            else
            {
                if (DLCPath == null || DLCPath == "" || inDLCFilename == null || inDLCFilename == "")
                    return;
                string s1 = DLCPath;
                string s2 = Path.GetDirectoryName(s1);
                string s3 = Path.GetDirectoryName(s2);
                string s4 = Path.GetDirectoryName(s3);
                string DLCp = DLCPath.Substring(s4.Length + 1);                
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.bin|*.bin";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                    byte[] buff = new byte[fs.Length];
                    int cnt;
                    int sum = 0;
                    while ((cnt = fs.Read(buff, sum, buff.Length - sum)) > 0) sum += cnt;
                    fs.Close();
                    KFreonLib.Scripting.ModMaker.ModJob mj = new KFreonLib.Scripting.ModMaker.ModJob();
                    mj.data = buff;
                    mj.Name = "Binary Replacement for file \"" + inDLCFilename + "\" in Object #" + n + " with " + buff.Length + " bytes of data";
                    string loc = Path.GetDirectoryName(Application.ExecutablePath);
                    string template = System.IO.File.ReadAllText(loc + "\\exec\\JobTemplate_Binary3DLC.txt");
                    template = template.Replace("**m1**", n.ToString());
                    template = template.Replace("**m2**", inDLCFilename);
                    template = template.Replace("**m3**", DLCp.Replace("\\", "\\\\"));
                    mj.Script = template;
                    KFreonLib.Scripting.ModMaker.JobList.Add(mj);
                    MessageBox.Show("Done");
                }
            }
        }

        private void createBinaryReplaceJobFromObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            if (!IsFromDLC)
            {
                KFreonLib.Scripting.ModMaker.ModJob mj = KFreonLib.Scripting.ModMaker.GenerateMeshModJob(null, n, pcc.pccFileName, CopyArray(pcc.Exports[n].Data));
                KFreonLib.Scripting.ModMaker.JobList.Add(mj);
                MessageBox.Show("Done");
            }
            else
            {
                if (DLCPath == null || DLCPath == "" || inDLCFilename == null || inDLCFilename == "")
                    return;
                string s1 = DLCPath;
                string s2 = Path.GetDirectoryName(s1);
                string s3 = Path.GetDirectoryName(s2);
                string s4 = Path.GetDirectoryName(s3);
                string DLCp = DLCPath.Substring(s4.Length + 1);
                KFreonLib.Scripting.ModMaker.ModJob mj = new KFreonLib.Scripting.ModMaker.ModJob();
                string currfile = Path.GetFileName(pcc.pccFileName);
                mj.data = CopyArray(pcc.Exports[n].Data);
                mj.Name = "Binary Replacement for file \"" + currfile + "\" in Object #" + n + " with " + pcc.Exports[n].Data.Length + " bytes of data";
                string loc = Path.GetDirectoryName(Application.ExecutablePath);
                string template = System.IO.File.ReadAllText(loc + "\\exec\\JobTemplate_Binary3DLC.txt");
                template = template.Replace("**m1**", n.ToString());
                template = template.Replace("**m2**", inDLCFilename);
                template = template.Replace("**m3**", DLCp.Replace("\\", "\\\\"));
                mj.Script = template;
                KFreonLib.Scripting.ModMaker.JobList.Add(mj);
                MessageBox.Show("Done");
            }
        }


        /// <summary>
        /// Generates script for replacing objects with DLC pathing fix
        /// </summary>
        /// <param name="n">Index in object to replace.</param>
        /// <param name="currfile">Name of pcc to replace.</param>
        /// <param name="pccPath">Full path of pcc to replace.</param>
        /// <returns>Script for Modmaker.</returns>
        public static string GenerateObjectReplaceScript(int n, string currfile, string pccPath)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            string template = System.IO.File.ReadAllText(loc + "\\exec\\JobTemplate_Binary2.txt");
            template = template.Replace("**m1**", n.ToString());
            template = template.Replace("**m2**", currfile);


            //KFreon
            
            return template;
        }

        public byte[] CopyArray(byte[] raw)
        {
            byte[] buff = new byte[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                buff[i] = raw[i];
            return buff;
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {
            if (toolStripStatusLabel1.ToolTipText != "")
                MessageBox.Show(toolStripStatusLabel1.ToolTipText);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            int off = pcc.Imports.Count;
            if (pcc == null)
                return;
            int n = GetSelected();
            if (n == -1 ||
                !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW) ||
                comboBox1.SelectedIndex == -1 ||
                comboBox2.SelectedIndex == -1 ||
                comboBox3.SelectedIndex == -1)
                return;
            NameIdx = comboBox1.SelectedIndex;
            ClassIdx = comboBox2.SelectedIndex - off;
            LinkIdx = comboBox3.SelectedIndex - off;
            pcc.Exports[n].idxObjectName = NameIdx;
            pcc.Exports[n].idxClassName = ClassIdx;
            pcc.Exports[n].idxLink = LinkIdx;
            RefreshView();
            SetSelected(n);
        }

        private void headerEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HeaderEditor.HeaderEditor h = new HeaderEditor.HeaderEditor();
            h.MdiParent = this.MdiParent;
            h.Show();
            h.WindowState = FormWindowState.Maximized;
        }

        private void exportFaceFXToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            if (pcc.Exports[n].ClassName == "FaceFXAsset" || pcc.Exports[n].ClassName == "FaceFXAnimSet")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.fxa|*.fxa";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    byte[] buff = pcc.Exports[n].Data;
                    BitConverter.IsLittleEndian = true;
                    List<ME3Explorer.Unreal.PropertyReader.Property> props = ME3Explorer.Unreal.PropertyReader.getPropList(pcc, buff);
                    int start = props[props.Count - 1].offend;
                    int len = BitConverter.ToInt32(buff, start);
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    fs.Write(buff, start + 4, len);
                    fs.Close();
                    MessageBox.Show("Done.");
                }
            }
        }

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = GetSelected();
            if (n == -1 || !(CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW))
                return;
            if (pcc.Exports[n].ClassName == "FaceFXAsset" || pcc.Exports[n].ClassName == "FaceFXAnimSet")
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.fxa|*.fxa";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    byte[] buff = pcc.Exports[n].Data;
                    BitConverter.IsLittleEndian = true;
                    List<ME3Explorer.Unreal.PropertyReader.Property> props = ME3Explorer.Unreal.PropertyReader.getPropList(pcc, buff);
                    int start = props[props.Count - 1].offend;
                    MemoryStream m = new MemoryStream();
                    m.Write(buff, 0, start);
                    byte[] import = File.ReadAllBytes(d.FileName);
                    m.Write(BitConverter.GetBytes((int)import.Length), 0, 4);
                    m.Write(import, 0, import.Length);
                    pcc.Exports[n].Data = m.ToArray();
                    pcc.altSaveToFile(pcc.pccFileName, true);
                    Preview();
                    MessageBox.Show("Done.");
                }
            }
        }

        private void cloneDialog2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloneDialog2 cl = new CloneDialog2();
            cl.MdiParent = this.MdiParent;
            cl.Show();
            cl.WindowState = FormWindowState.Maximized;
        }

        private void loadFromDLCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.sfar|*.sfar";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DLCDialog dlc = new DLCDialog();
                dlc.Init(d.FileName);
                dlc.Show();
                while (dlc != null && dlc.Result == null)
                    Application.DoEvents();
                int result = (int)dlc.Result;
                if (result != -1)
                {
                    DLCPackage p = dlc.dlc;
                    string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\data.pcc";
                    DLCPath = d.FileName;
                    inDLCFilename = dlc.listBox1.Items[result].ToString();
                    FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                    MemoryStream mem = p.DecompressEntry(dlc.Objects[result]);
                    fs.Write(mem.ToArray(), 0, (int)mem.Length);
                    fs.Close();
                    
                    LoadFile(path, true);
                }
                dlc.Close();
                
            }
        }

        private void saveIntoDLCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!IsFromDLC || inDLCFilename == null || inDLCFilename.Length == 0 || DLCPath == null || DLCPath.Length == 0)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.sfar|*.sfar";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DLCDialog dlc = new DLCDialog();
                dlc.Init(d.FileName);
                dlc.Show();
                while (dlc != null && dlc.Result == null)
                    Application.DoEvents();
                int result = (int)dlc.Result;
                if (result != -1)
                {
                    DLCPackage p = dlc.dlc;
                    string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\data.pcc";
                    pcc.altSaveToFile(path, true);
                    byte[] buff = File.ReadAllBytes(path);
                    p.ReplaceEntry(buff, dlc.Objects[result]);
                    MessageBox.Show("Done.");
                }
                dlc.Close();
            }
        }

        private void Button5_Click(object sender, EventArgs e)
        {
            SetView(TREE_VIEW);
            RefreshView();
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            Preview();
        }

        private void scanForCoalescedValuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            scanningCoalescedBits = !scanningCoalescedBits;
            scanForCoalescedValuesToolStripMenuItem.Checked = scanningCoalescedBits;
            RefreshView();
        }

        //unused
        private void addBiggerImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n <= 0)
                return;

            if (pcc.Exports[n].ClassName != "Texture2D")
                MessageBox.Show("Not a texture.");
            else
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "DirectX images|*.dds";
                    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string path = Path.GetDirectoryName(pcc.pccFileName);
                        ME3PCCObject temp = new ME3PCCObject(pcc.pccFileName);
                        KFreonLib.Textures.ME3SaltTexture2D tex2D = new KFreonLib.Textures.ME3SaltTexture2D(temp, n, path);
                        ImageFile im = KFreonLib.Textures.Creation.LoadAKImageFile(null, ofd.FileName);
                        if (tex2D.imgList.Count <= 1)
                            tex2D.singleImageUpscale(im, path);
                        else
                            tex2D.OneImageToRuleThemAll(im, path, im.imgData);

                        ME3ExportEntry expEntry = temp.Exports[tex2D.pccExpIdx];
                        expEntry.SetData(tex2D.ToArray(expEntry.DataOffset, temp));
                        temp.saveToFile(temp.pccFileName);
                    }
                }
            } 
            // Reload pcc? TOC update?
        }

        //unused
        private void replaceImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n <= 0)
                return;

            if (pcc.Exports[n].ClassName != "Texture2D")
                MessageBox.Show("Not a texture.");
            else
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "DirectX images|*.dds";
                    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string path = Path.GetDirectoryName(pcc.pccFileName);
                        ME3PCCObject temp = new ME3PCCObject(pcc.pccFileName);
                        ME3SaltTexture2D tex2D = new ME3SaltTexture2D(temp, n, path);
                        string test = tex2D.imgList.Max(t => t.imgSize).ToString();
                        ImageFile im = KFreonLib.Textures.Creation.LoadAKImageFile(null, ofd.FileName);
                        tex2D.replaceImage(test, im, path);

                        ME3ExportEntry expEntry = temp.Exports[tex2D.pccExpIdx];
                        expEntry.SetData(tex2D.ToArray(expEntry.DataOffset, temp));
                        temp.saveToFile(temp.pccFileName);
                    }
                }
            }
            // Reload pcc?
        }

    }


}
