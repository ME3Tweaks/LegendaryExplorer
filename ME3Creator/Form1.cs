using Be.Windows.Forms;
using ME3LibWV;
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using ME3LibWV.UnrealClasses;
using UDKLibWV;

namespace ME3Creator
{
    public partial class Form1 : Form
    {
        public PCCPackage pcc;
        public PCCPackage importpcc;
        public UDKObject importudk;
        public List<int> Classes;
        public List<int> Objects;
        private bool isSelectOpen = false;
        private bool MouseDownRight = false;
        private bool MouseDownLeft = false;
        private Point MouseLast;
        private Vector3 lastcam, lastdir;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            DebugLog.SetBox(DebugOut);
            DebugLog.SetDebugToFile(true);
            DebugLog.PrintLn("Initialized.");
        }

        private void openPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadBaseGameFile(d.FileName);
                RefreshAll();
            }
        }

        public void LoadBaseGameFile(string path)
        {
            if (pcc != null && pcc.Source != null)
                pcc.Source.Close();
            pcc = new PCCPackage(path, true, false);
        }

        public void LoadDLCFile(string path, int index)
        {
            pcc = new PCCPackage(new DLCPackage(path), index); 
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            DebugLog.Update();
            if (!DXHelper.init && !DXHelper.error)
            {
                DXHelper.Init(pb1);
                timer1.Enabled = true;
            }
            else if (DXHelper.error)
                timer1.Enabled = false;
        }

        public void Reload()
        {
            timer1.Enabled = false;
            if (pcc == null)
                return;
            if (pcc.GeneralInfo.inDLC)
            {
                LoadDLCFile(pcc.GeneralInfo.filepath, pcc.GeneralInfo.inDLCIndex);
            }
            else
            {
                LoadBaseGameFile(pcc.GeneralInfo.filepath);
            }
            RefreshAll();
            timer1.Enabled = true;
        }

        public void RefreshAll()
        {
            if (pcc == null)
                return;
            timer1.Enabled = false;
            try
            {
                RefreshContentView();
                RefreshLevelView();
                RefreshImportView();
                RefreshCreatorView();
                RefreshKismetView();
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("ERROR Refreshing : \n" + ex.Message);
            }
            timer1.Enabled = true;
        }

        public void RefreshContentView()
        {
            if (pcc == null)
                return;
            #region Header
            pcc.Source.Seek(0, 0);
            byte[] buff = new byte[pcc.Header._offsetCompFlagEnd];
            pcc.Source.Read(buff, 0, buff.Length);
            hb1.ByteProvider = new DynamicByteProvider(buff);
            treeView1.Nodes.Clear();
            TreeNode t;
            if (pcc.GeneralInfo.inDLC)
                t = new TreeNode(Path.GetFileName(pcc.GeneralInfo.inDLCPath.Replace("/", "\\")));
            else
                t = new TreeNode(Path.GetFileName(pcc.GeneralInfo.filepath));
            t.Nodes.Add(new TreeNode("Magic : 0x" + pcc.Header.magic.ToString("X8")));
            t.Nodes.Add(new TreeNode("Ver1 : 0x" + pcc.Header.ver1.ToString("X4")));
            t.Nodes.Add(new TreeNode("Ver2 : 0x" + pcc.Header.ver2.ToString("X4")));
            t.Nodes.Add(new TreeNode("Full Header Size : 0x" + pcc.Header.HeaderLength.ToString("X8")));
            t.Nodes.Add(new TreeNode("Group : " + pcc.Header.Group));
            string s = "Flags : 0x" + pcc.Header.Flags.ToString("X8");
            if (pcc.GeneralInfo.compressed)
                s += "(IsCompressed)";
            else
                s += "(IsNotCompressed)";
            t.Nodes.Add(new TreeNode(s));
            t.Nodes.Add(new TreeNode("Unknown1 : 0x" + pcc.Header.unk1.ToString("X8")));
            t.Nodes.Add(new TreeNode("Name Count : " + pcc.Header.NameCount));
            t.Nodes.Add(new TreeNode("Name Offset : 0x" + pcc.Header.NameOffset.ToString("X8")));
            t.Nodes.Add(new TreeNode("Import Count : " + pcc.Header.ImportCount));
            t.Nodes.Add(new TreeNode("Import Offset : 0x" + pcc.Header.ImportOffset.ToString("X8")));
            t.Nodes.Add(new TreeNode("Export Count : " + pcc.Header.ExportCount));
            t.Nodes.Add(new TreeNode("Export Offset : 0x" + pcc.Header.ExportOffset.ToString("X8")));
            t.Nodes.Add(new TreeNode("Freezone Start : 0x" + pcc.Header.FreeZoneStart.ToString("X8")));
            t.Nodes.Add(new TreeNode("Freezone End : 0x" + pcc.Header.FreeZoneEnd.ToString("X8")));
            t.Nodes.Add(new TreeNode("Unknown2 : 0x" + pcc.Header.unk2.ToString("X8")));
            t.Nodes.Add(new TreeNode("Unknown3 : 0x" + pcc.Header.unk3.ToString("X8")));
            t.Nodes.Add(new TreeNode("Unknown4 : 0x" + pcc.Header.unk4.ToString("X8")));
            s = "GUID : ";
            foreach (byte b in pcc.Header.GUID)
                s += b.ToString("X2");
            t.Nodes.Add(new TreeNode(s));
            TreeNode t2 = new TreeNode("Generations");
            for(int i=0;i<pcc.Header.Generations.Count;i++)
            {
                PCCPackage.Generation g = pcc.Header.Generations[i];
                t2.Nodes.Add(new TreeNode(i.ToString("d4") + " : Export Count (" + g.ExportCount + ") Import Count (" + g.ImportCount + ") Net Object Count (" + g.NetObjCount + ")"));
            }
            t.Nodes.Add(t2);
            t.Nodes.Add(new TreeNode("Engine Version : 0x" + pcc.Header.EngineVersion.ToString("X8")));
            t.Nodes.Add(new TreeNode("Cooker Version : 0x" + pcc.Header.CookerVersion.ToString("X8")));
            t.Nodes.Add(new TreeNode("Unknown5 : 0x" + pcc.Header.unk5.ToString("X8")));
            t.Nodes.Add(new TreeNode("Unknown6 : 0x" + pcc.Header.unk6.ToString("X8")));
            s = "Compression Algorithm : " + pcc.Header.CompressionFlag;
            switch (pcc.Header.CompressionFlag)
            {
                case 1:
                    s += " (Zlib)";
                    break;
                case 2:
                    s += " (LZO)";
                    break;
                case 4:
                    s += " (LZX)";
                    break;
                default:
                    s += " (Unknown)";
                    break;
            }
            t.Nodes.Add(new TreeNode(s));
            t.ExpandAll();
            treeView1.Nodes.Add(t);
            #endregion
            #region NameList
            listBox1.Items.Clear();
            listBox1.Visible = false;
            for (int i = 0; i < pcc.Header.NameCount; i++)
                listBox1.Items.Add(i.ToString("d8") + " : " + pcc.Names[i]);
            listBox1.Visible = true;
            #endregion
            #region Imports
            listBox2.Items.Clear();
            listBox2.Visible = false;
            for (int i = 0; i < pcc.Header.ImportCount; i++)
                listBox2.Items.Add(i.ToString("d8") + " : " + pcc.GetObjectPath(-i - 1) + pcc.GetObject(-i - 1));
            listBox2.Visible = true;
            treeView2.Nodes.Clear();
            #endregion
            #region Exports1
            listBox3.Items.Clear();
            listBox3.Visible = false;
            for (int i = 0; i < pcc.Header.ExportCount; i++)
                listBox3.Items.Add(i.ToString("d8") + " : " + pcc.GetObjectPath(i + 1) + pcc.GetObject(i + 1));
            listBox3.Visible = true;
            treeView3.Nodes.Clear();
            #endregion
            #region Exports2
            listBox4.Items.Clear();
            listBox4.Visible = false;
            for (int i = 0; i < pcc.Header.ExportCount; i++)
                listBox4.Items.Add(i.ToString("d8") + " : " + pcc.GetObjectPath(i + 1) + pcc.GetObject(i + 1));
            listBox4.Visible = true;
            toolStripComboBox1.Items.Clear();
            Classes = new List<int>();
            foreach (PCCPackage.ExportEntry e in pcc.Exports)
            {
                int idx = e.idxClass;
                bool found = false;
                for (int i = 0; i < Classes.Count; i++)
                    if (Classes[i] == idx)
                    {
                        found = true;
                        break;
                    }
                if (!found)
                    Classes.Add(idx);
            }
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < Classes.Count - 1; i++)
                {
                    if (pcc.GetObject(Classes[i]).CompareTo(pcc.GetObject(Classes[i + 1])) > 0)
                    {
                        int tmp = Classes[i];
                        Classes[i] = Classes[i + 1];
                        Classes[i + 1] = tmp;
                        run = true;
                    }
                }
            }
            for (int i = 0; i < Classes.Count; i++)
                toolStripComboBox1.Items.Add(pcc.GetObject(Classes[i]));
            toolStripComboBox1.SelectedIndex = 0;
            #endregion
            #region Exports3
            treeView4.Nodes.Clear();
            if (pcc.GeneralInfo.inDLC)
                t = new TreeNode(Path.GetFileName(pcc.GeneralInfo.inDLCPath));
            else
                t = new TreeNode(Path.GetFileName(pcc.GeneralInfo.filepath));
            t.Name = "0";
            for (int i = 0; i < pcc.Header.ExportCount; i++)
            {
                int[] list = pcc.GetLinkList(pcc.Exports[i].idxLink);
                t2 = t;
                for (int j = 0; j < list.Length; j++)
                {
                    s = pcc.GetObject(list[j]);
                    bool f = false;
                    foreach(TreeNode t3 in t2.Nodes)
                        if (t3.Text == s)
                        {
                            f = true;
                            t2 = t3;
                            break;
                        }
                    if (!f)
                    {
                        TreeNode t3 = new TreeNode(s);
                        t3.Name = list[j].ToString();
                        if (list[j] >= 0)
                            t3.BackColor = Color.LightBlue;
                        else
                            t3.BackColor = Color.Orange;
                        t2.Nodes.Add(t3);
                        t2 = t3;
                    }
                }
                string s2 = pcc.GetName(pcc.Exports[i].idxName);
                bool f2 = false;
                foreach (TreeNode t3 in t2.Nodes)
                    if (t3.Text == s2 && t3.Name == (i + 1).ToString())
                    {
                        f2 = true;
                        break;
                    }
                if (!f2)
                {
                    TreeNode t4 = new TreeNode(s2);
                    t4.Name = (i + 1).ToString();
                    t4.BackColor = Color.LightBlue;
                    t2.Nodes.Add(t4);
                }
            }
            for (int i = 0; i < pcc.Header.ImportCount; i++)
            {
                int[] list = pcc.GetLinkList(pcc.Imports[i].idxLink);
                t2 = t;
                for (int j = 0; j < list.Length; j++)
                {
                    s = pcc.GetObject(list[j]);
                    bool f = false;
                    foreach (TreeNode t3 in t2.Nodes)
                        if (t3.Text == s)
                        {
                            f = true;
                            t2 = t3;
                            break;
                        }
                    if (!f)
                    {
                        TreeNode t3 = new TreeNode(s);
                        t3.Name = list[j].ToString();
                        if (list[j] >= 0)
                            t3.BackColor = Color.LightBlue;
                        else
                            t3.BackColor = Color.Orange;
                        t2.Nodes.Add(t3);
                        t2 = t3;
                    }
                }
                string s2 = pcc.GetName(pcc.Imports[i].idxName);
                bool f2 = false;
                foreach (TreeNode t3 in t2.Nodes)
                    if (t3.Text == s2 && t3.Name == (-i - 1).ToString())
                    {
                        f2 = true;
                        break;
                    }
                if (!f2)
                {
                    TreeNode t4 = new TreeNode(s2);
                    t4.Name = (-i - 1).ToString();
                    t4.BackColor = Color.Orange;
                    t2.Nodes.Add(t4);
                }
            }
            t = SortTree(t);
            t.Expand();
            treeView4.Nodes.Add(t);
            #endregion
        }

        public TreeNode SortTree(TreeNode t)
        {
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < t.Nodes.Count - 1; i++)
                    if (t.Nodes[i].Text.CompareTo(t.Nodes[i + 1].Text) > 0) 
                    {
                        run = true;
                        TreeNode tmp = t.Nodes[i];
                        t.Nodes[i] = t.Nodes[i + 1];
                        t.Nodes[i + 1] = tmp;
                    }
            }
            for (int i = 0; i < t.Nodes.Count; i++)
                t.Nodes[i] = SortTree(t.Nodes[i]);
            return t;
        }

        public void RefreshLevelView()
        {
            treeView6.Nodes.Clear();
            DXHelper.cam = new Vector3(0, 0, 0);
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.GetObject(pcc.Exports[i].idxClass) == "Level")
                {
                    
                    DXHelper.level = new ME3LibWV.UnrealClasses.Level(pcc, i);                    
                    treeView6.Nodes.Add(DXHelper.level.ToTree());
                    break;
                }
        }

        public void RefreshImportView()
        {
            listBox5.Items.Clear();
            listBox6.Items.Clear();
            toolStripComboBox2.Items.Clear();
            toolStripComboBox2.Items.Add("Add Import");
            toolStripComboBox2.Items.Add("Write Import over ...");
            toolStripComboBox2.SelectedIndex = 0;
            if (importpcc != null && importpcc.Source != null)
                importpcc.Source.Close();
            if (importpcc != null)
                importpcc = null;
        }

        public void RefreshCreatorView()
        {
            textBox1.Text = "0";
            textBox2.Text = "0";
            comboBox1.Items.Clear();
            for (int i = 0; i < pcc.Names.Count; i++)
                comboBox1.Items.Add(i.ToString("d6") + " : " + pcc.Names[i]);
            comboBox2.Items.Clear();
            for (int i = 0; i < pcc.Exports.Count; i++)
                comboBox2.Items.Add(i.ToString("d6") + " : " + pcc.GetObjectPath(i + 1) + pcc.GetObject(i + 1));
            comboBox3.Items.Clear();
            for (int i = 0; i < pcc.Imports.Count; i++)
                comboBox3.Items.Add(i.ToString("d6") + " : " + pcc.GetObjectPath(-i - 1) + pcc.GetObject(-i - 1));
            if (comboBox1.Items.Count != 0)
                comboBox1.SelectedIndex = 0;
            if (comboBox2.Items.Count != 0)
                comboBox2.SelectedIndex = 0;
            if (comboBox3.Items.Count != 0)
                comboBox3.SelectedIndex = 0;
        }

        public void RefreshKismetView()
        {
            treeView7.Nodes.Clear();
            if (pcc == null)
                return;
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                if (pcc.GetObjectClass(i + 1) == "Sequence")
                {
                    Sequence s = new Sequence(pcc, i);
                    treeView7.Nodes.Add(s.ToTree());
                }
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Reload();
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox2.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            PCCPackage.ImportEntry imp = pcc.Imports[n];
            treeView2.Nodes.Clear();
            treeView2.Nodes.Add("Package: Index(" + imp.idxPackage + ") = \"" + pcc.GetName(imp.idxPackage) + "\"");
            treeView2.Nodes.Add("Unknown 1: 0x" + imp.Unk1.ToString("X8"));
            treeView2.Nodes.Add("Class: Index(" + imp.idxClass + ") = \"" + pcc.GetName(imp.idxClass) + "\"");
            treeView2.Nodes.Add("Unknown 2: 0x" + imp.Unk2.ToString("X8"));
            treeView2.Nodes.Add("Link: Index(" + imp.idxLink + ") = \"" + pcc.GetObject(imp.idxLink) + "\"");
            treeView2.Nodes.Add("Name: Index(" + imp.idxName + ") = \"" + pcc.GetName(imp.idxName) + "\"");
            treeView2.Nodes.Add("Unknown 3: 0x" + imp.Unk3.ToString("X8"));
        }

        private void listBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox3.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            PCCPackage.ExportEntry exp = pcc.Exports[n];
            treeView3.Nodes.Clear();
            treeView3.Nodes.Add("Class: Index(" + exp.idxClass + ") = \"" + pcc.GetObject(exp.idxClass) + "\"");
            treeView3.Nodes.Add("Parent: Index(" + exp.idxParent + ") = \"" + pcc.GetObject(exp.idxParent) + "\"");
            treeView3.Nodes.Add("Link: Index(" + exp.idxLink + ") = \"" + pcc.GetObject(exp.idxLink) + "\"");
            treeView3.Nodes.Add("Name: Index(" + exp.idxName + ") = \"" + pcc.GetName(exp.idxName) + "\"");
            treeView3.Nodes.Add("Index: 0x" + exp.Index.ToString("X8") + " = " + pcc.GetName(exp.Index));
            treeView3.Nodes.Add("Archetype: Index(" + exp.idxArchetype + ")");
            treeView3.Nodes.Add("Unknown1: 0x" + exp.Unk1.ToString("X8"));
            treeView3.Nodes.Add("Object Flags: 0x" + exp.ObjectFlags.ToString("X8"));
            treeView3.Nodes.Add("Datasize: 0x" + exp.Datasize.ToString("X8"));
            treeView3.Nodes.Add("Dataoffset: 0x" + exp.Dataoffset.ToString("X8"));
            treeView3.Nodes.Add("Unknown2: 0x" + exp.Unk2.ToString("X8"));
            TreeNode t = new TreeNode("Unknown3");
            foreach (int i in exp.Unk3)
                t.Nodes.Add(i.ToString("X8"));
            treeView3.Nodes.Add(t);
            treeView3.Nodes.Add("Unknown4: 0x" + exp.Unk4.ToString("X8"));
            treeView3.Nodes.Add("Unknown5: 0x" + exp.Unk5.ToString("X8"));
            treeView3.Nodes.Add("Unknown6: 0x" + exp.Unk6.ToString("X8"));
            treeView3.Nodes.Add("Unknown7: 0x" + exp.Unk7.ToString("X8"));
            treeView3.Nodes.Add("Unknown8: 0x" + exp.Unk8.ToString("X8"));
            treeView3.ExpandAll();
        }

        private void listBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox4.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            PCCPackage.ExportEntry exp = pcc.Exports[n];
            hb2.ByteProvider = new DynamicByteProvider(pcc.GetObjectData(n));            
            string s = "Flags (0x" + pcc.Exports[n].ObjectFlags.ToString("X8") + ") ";
            s += "Class (" + pcc.GetObject(pcc.Exports[n].idxClass) + ")";
            labstate.Text = s;
            ReadProperties(n);
        }

        #region PROPERTIES DISPLAY
        public byte[] PropMemory;
        public int PropReadPos;
        public struct PropHeader
        {
            public int name;
            public int type;
            public int size;
            public int index;
            public int offset;
        }
#region types
        public string[] Types =
        {
            "StructProperty", //0
            "IntProperty",
            "FloatProperty",
            "ObjectProperty",
            "NameProperty",
            "BoolProperty",  //5
            "ByteProperty",
            "ArrayProperty",
            "StrProperty",
            "StringRefProperty",
            "DelegateProperty"//10
        };
#endregion
        private void ReadProperties(int index)
        {
            treeView5.Nodes.Clear();
            try
            {
                PropMemory = pcc.GetObjectData(index);
                PropReadPos = PropertyReader.detectStart(pcc, PropMemory, (uint)pcc.Exports[index].ObjectFlags);
                List<PropHeader> l = ReadHeadersTillNone();
                TreeNode t = new TreeNode("0000 : " + pcc.GetObject(index + 1));
                t = GenerateTree(t, l);
                t.Expand();
                treeView5.Nodes.Add(t);
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("ERROR READPROPERTIES:\n" + ex.Message);
            }
        }
        public TreeNode GenerateTree(TreeNode input, List<PropHeader> l)
        {
            TreeNode ret = input;
            foreach (PropHeader p in l)
            {
                int type = isType(pcc.GetName(p.type));
                string typename = pcc.GetName(p.type);
                string namename = pcc.GetName(p.name);
                if (type != 7 && type != 0)
                    ret.Nodes.Add(GenerateNode(p));
                else
                {
                    if (type == 7)
                    {
                        TreeNode t = GenerateNode(p);
                        int count = BitConverter.ToInt32(PropMemory, p.offset + 24);
                        PropReadPos = p.offset + 28;
                        int tmp = PropReadPos;
                        List<PropHeader> ll = ReadHeadersTillNone();
                        if (ll.Count != 0 && count > 0)
                        {
                            if (count == 1)
                            {
                                PropReadPos = tmp;
                                t = GenerateTree(t, ll);
                            }
                            else
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    PropReadPos = tmp;
                                    List<PropHeader> ll2 = ReadHeadersTillNone();
                                    tmp = PropReadPos;
                                    TreeNode n = new TreeNode(i.ToString());
                                    n = GenerateTree(n, ll2);
                                    t.Nodes.Add(n);
                                }
                            }
                            ret.Nodes.Add(t);
                        }
                        else
                        {
                            for (int i = 0; i < (p.size - 4) / 4; i++)
                            {
                                int val = BitConverter.ToInt32(PropMemory, p.offset + 28 + i * 4);
                                string s = (p.offset + 28 + i * 4).ToString("X4") + " : " + val.ToString();
                                t.Nodes.Add(s);
                            }
                            ret.Nodes.Add(t);
                        }
                    }
                    if (type == 0)
                    {
                        TreeNode t = GenerateNode(p);
                        int name = BitConverter.ToInt32(PropMemory, p.offset + 24);
                        PropReadPos = p.offset + 32;
                        List<PropHeader> ll = ReadHeadersTillNone();
                        if (ll.Count != 0)
                        {
                            t = GenerateTree(t, ll);
                            ret.Nodes.Add(t);
                        }
                        else
                        {
                            for (int i = 0; i < p.size / 4; i++)
                            {
                                int val = BitConverter.ToInt32(PropMemory, p.offset + 32 + i * 4);
                                string s = (p.offset + 32 + i * 4).ToString("X4") + " : " + val.ToString();
                                t.Nodes.Add(s);
                            }
                            ret.Nodes.Add(t);
                        }
                    }

                }
            }
            return ret;
        }
        public TreeNode GenerateNode(PropHeader p)
        {
            string s = p.offset.ToString("X4") + " : ";
            s += "Name: \"" + pcc.GetName(p.name) + "\" ";
            s += "Type: \"" + pcc.GetName(p.type) + "\" ";
            s += "Size: " + p.size.ToString() + " Value: ";
            switch (isType(pcc.GetName(p.type)))
            {
                case 1:
                case 3:
                    int idx = BitConverter.ToInt32(PropMemory, p.offset + 24);
                    s += idx.ToString();
                    break;
                case 8:
                    int count = BitConverter.ToInt32(PropMemory, p.offset + 24);
                    s += "\"";
                    for (int i = 0; i < count * -1 - 1; i++)
                        s += (char)PropMemory[p.offset + 28 + i * 2];
                    s += "\"";
                    break;
                case 5:
                    byte val = PropMemory[p.offset + 24];
                    s += (val == 1).ToString();
                    break;
                case 2:
                    float f = BitConverter.ToSingle(PropMemory, p.offset + 24);
                    s += f.ToString() + "f";
                    break;
                case 0:
                case 4:
                    idx = BitConverter.ToInt32(PropMemory, p.offset + 24);
                    s += "\"" + pcc.GetName(idx) + "\"";
                    break;
                case 6:
                    idx = BitConverter.ToInt32(PropMemory, p.offset + 24);
                    int idx2 = BitConverter.ToInt32(PropMemory, p.offset + 32);
                    s += "\"" + pcc.GetName(idx) + "\",\"" + pcc.GetName(idx2) + "\"";
                    break;
                case 7:
                    idx = BitConverter.ToInt32(PropMemory, p.offset + 24);
                    s += idx.ToString() + "(count)";
                    break;
                case 9:
                    idx = BitConverter.ToInt32(PropMemory, p.offset + 24);
                    s += "#" + idx.ToString() ;
                    break;
            }
            TreeNode ret = new TreeNode(s);
            ret.Name = p.offset.ToString();
            return ret;
        }
        public int isType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return ret;
        }
        public List<PropHeader> ReadHeadersTillNone()
        {
            List<PropHeader> ret = new List<PropHeader>();
            bool run = true;
            while (run)
            {
                PropHeader p = new PropHeader();
                p.name = BitConverter.ToInt32(PropMemory, PropReadPos);
                int test = BitConverter.ToInt32(PropMemory, PropReadPos + 4);     
                if (!pcc.isName(p.name) || test != 0)
                    run = false;
                else
                {
                    string namename = pcc.GetName(p.name);
                    if (pcc.GetName(p.name) != "None")
                    {
                        p.type = BitConverter.ToInt32(PropMemory, PropReadPos + 8);
                        int test2 = BitConverter.ToInt32(PropMemory, PropReadPos + 12);
                        int test3 = BitConverter.ToInt32(PropMemory, PropReadPos + 20);
                        if (!pcc.isName(p.type) || isType(pcc.GetName(p.type)) == -1 || test2 != 0 || test3 != 0)
                            run = false;
                        else
                        {
                            p.size = BitConverter.ToInt32(PropMemory, PropReadPos + 16);
                            p.index = BitConverter.ToInt32(PropMemory, PropReadPos + 20);
                            p.offset = PropReadPos;
                            ret.Add(p);
                            PropReadPos += p.size + 24;
                            if (isType(pcc.GetName(p.type)) == 5)//Boolbyte
                                PropReadPos++;
                            if (isType(pcc.GetName(p.type)) == 0 ||//StructName
                                isType(pcc.GetName(p.type)) == 6)//byteprop
                                PropReadPos += 8;
                        }
                    }
                    else
                    {
                        p.type = p.name;
                        p.size = 0;
                        p.index = 0;
                        p.offset = PropReadPos;
                        ret.Add(p);
                        PropReadPos += 8;
                        run = false;
                    }
                }
            }
            return ret;
        }
        #endregion

        private void treeView4_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode t = treeView4.SelectedNode;
            if (t != null && t.Name != "" && t.Name != "0")
            {
                int n = Convert.ToInt32(t.Name);
                if (n > 0)
                {
                    hb3.ByteProvider = new DynamicByteProvider(pcc.GetObjectData(n - 1));
                    return;
                }
            }            
            hb3.ByteProvider = new DynamicByteProvider(new byte[0]);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            pcc.Save();
            MessageBox.Show("Done.");
        }

        private void treeView4_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                TreeNode t = treeView4.SelectedNode;
                if (t == null || t.Name == "0" || t.Name == "")
                {
                    findInTableToolStripMenuItem.Enabled =
                    cloneToolStripMenuItem.Enabled = false;
                }
                else
                {
                    findInTableToolStripMenuItem.Enabled =
                    cloneToolStripMenuItem.Enabled = true;
                }
            }
        }

        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView4.SelectedNode;
            if (t == null || t.Name == "0" || t.Name == "")
                return;
            int n = Convert.ToInt32(t.Name);
            pcc.CloneEntry(n);
            RefreshAll();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            int n = toolStripComboBox1.SelectedIndex;
            int m = listBox4.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            for (int i = m + 1; i < pcc.Exports.Count; i++) 
                if (pcc.Exports[i].idxClass == Classes[n])
                {
                    listBox4.SelectedIndex = i;
                    return;
                }
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            int m = listBox4.SelectedIndex;
            string input = toolStripTextBox2.Text.ToLower();
            for (int i = m + 1; i < pcc.Exports.Count; i++)
                if (pcc.GetName(pcc.Exports[i].idxName).ToLower().Contains(input))
                {
                    listBox4.SelectedIndex = i;
                    return;
                }
        }

        private void toolStripTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 0xd)
            {
                int m = listBox4.SelectedIndex;
                string input = toolStripTextBox2.Text.ToLower();
                for (int i = m + 1; i < pcc.Exports.Count; i++)
                    if (pcc.GetName(pcc.Exports[i].idxName).ToLower().Contains(input))
                    {
                        listBox4.SelectedIndex = i;
                        return;
                    }
            }
        }

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
            int n = listBox4.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            MemoryStream m = new MemoryStream();
            for (int i = 0; i < hb2.ByteProvider.Length; i++)
                m.WriteByte(hb2.ByteProvider.ReadByte(i));
            PCCPackage.ExportEntry ent = pcc.Exports[n];
            ent.Data = m.ToArray();
            ent.Datasize = ent.Data.Length;
            pcc.Exports[n] = ent;
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            int n = listBox4.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            string path = pcc.GetObjectPath(pcc.Exports[n].idxLink) + pcc.GetName(pcc.Exports[n].idxName) + ".bin";
            d.FileName = path;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                File.WriteAllBytes(d.FileName, pcc.GetObjectData(n));
                MessageBox.Show("Done.");
            }
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            int n = listBox4.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                byte[] buff = File.ReadAllBytes(d.FileName);
                PCCPackage.ExportEntry ent = pcc.Exports[n];
                ent.Data = buff;
                ent.Datasize = ent.Data.Length;
                pcc.Exports[n] = ent;
                RefreshContentView();
            }
        }

        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            int n = listBox4.SelectedIndex;
            if (n == -1 || pcc == null)
                return;
            pcc.CloneEntry(n + 1);
            RefreshAll();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (DXHelper.init && tabControl1.SelectedTab == tabView1) 
                DXHelper.Render();
        }

        private void findInTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView4.SelectedNode;
            if (t == null || t.Name == "0" || t.Name == "")
                return;
            int n = Convert.ToInt32(t.Name);
            if (n > 0)
            {
                tabControl2.SelectedTab = tabContent5;
                listBox4.SelectedIndex = n - 1;
            }
            else
            {
                tabControl2.SelectedTab = tabContent3;
                listBox2.SelectedIndex = -n - 1;
            }
        }
        
        private void pb1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                MouseDownLeft = true;
                DXHelper.Process3DClick(e.Location);
                FindSelected();
            }
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
                MouseDownRight = true;
            MouseLast = new Point(e.X, e.Y);
            lastcam = DXHelper.cam;
            lastdir = DXHelper.dir;
            lastdir.Normalize();
        }

        private void FindSelected()
        {
            if(DXHelper.level == null)
                return;
            for (int i = 0; i < DXHelper.level.Objects.Count; i++)
            {
                int idx = DXHelper.level.Objects[i] - 1;
                foreach (ME3LibWV.UnrealClasses._DXRenderableObject o in DXHelper.level.RenderObjects)
                    if (idx == o.MyIndex)
                    {
                        string c = pcc.GetObject(pcc.Exports[idx].idxClass);
                        switch (c)
                        {
                            case "StaticMeshActor":
                            case "InterpActor":
                                ME3LibWV.UnrealClasses.StaticMeshActor sma = (ME3LibWV.UnrealClasses.StaticMeshActor)o;
                                if (sma.STMC != null && sma.STMC.STM != null && sma.STMC.STM.Selected)
                                {
                                    TreeNode t = treeView6.Nodes[0];
                                    t = t.Nodes[i];
                                    t = t.Nodes[0];
                                    t = t.Nodes[0];
                                    t.Expand();
                                    treeView6.SelectedNode = t;
                                    return;
                                }
                                break;
                            case "StaticMeshCollectionActor":
                                ME3LibWV.UnrealClasses.StaticMeshCollectionActor smca = (ME3LibWV.UnrealClasses.StaticMeshCollectionActor)o;
                                if (smca.STMC != null)
                                {
                                    int count = 0;
                                    for (int j = 0; j < smca.STMC.Count; j++)
                                    {                                        
                                        if (smca.STMC[j] != null && smca.STMC[j].STM != null && smca.STMC[j].STM.Selected)
                                        {
                                            TreeNode t = treeView6.Nodes[0];
                                            t = t.Nodes[i];
                                            t = t.Nodes[count];
                                            t = t.Nodes[0];
                                            t.Expand();
                                            treeView6.SelectedNode = t;
                                            return;
                                        }
                                        if (smca.STMC[j] != null)
                                            count++;
                                    }
                                }
                                break;
                        }
                    }
            }
        }

        private void pb1_MouseMove(object sender, MouseEventArgs e)
        {
            if (MouseDownRight)
            {
                Point MouseCurrent = new Point(e.X, e.Y);
                Vector3 t = Vector3.Cross(DXHelper.dir, new Vector3(0, 0, 1));
                t.Normalize();
                DXHelper.dir = Vector3.TransformCoordinate(lastdir, Matrix.RotationZ(-(MouseLast.X - MouseCurrent.X) * (3.1415f / 360f)));
                DXHelper.dir = Vector3.TransformCoordinate(DXHelper.dir, Matrix.RotationAxis(t, (MouseLast.Y - MouseCurrent.Y) * (3.1415f / 360f)));
                DXHelper.dir.Normalize();
                toolStripStatusLabel1.Text = "Camera : X(" + DXHelper.cam.X + ")  Y(" + DXHelper.cam.Y + ") Z(" + DXHelper.cam.Z + ")";
            }
        }

        private void pb1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                MouseDownLeft = false;
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
                MouseDownRight = false;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (tabControl1.SelectedTab == tabView1)
            {
                Vector3 t = Vector3.Cross(DXHelper.dir, new Vector3(0, 0, 1));
                t.Normalize();
                if (e.KeyCode == Keys.W)
                    DXHelper.cam += DXHelper.dir * DXHelper.speed;
                if (e.KeyCode == Keys.S)
                    DXHelper.cam -= DXHelper.dir * DXHelper.speed;
                if (e.KeyCode == Keys.A)
                    DXHelper.cam += t * DXHelper.speed;
                if (e.KeyCode == Keys.D)
                    DXHelper.cam -= t * DXHelper.speed;
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            toolStripMenuItem2.Checked =
                toolStripMenuItem3.Checked =
                toolStripMenuItem4.Checked =
                toolStripMenuItem5.Checked =
                toolStripMenuItem6.Checked = false;
            toolStripMenuItem2.Checked = true;
            DXHelper.speed = 1.0f;
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            toolStripMenuItem2.Checked =
                toolStripMenuItem3.Checked =
                toolStripMenuItem4.Checked =
                toolStripMenuItem5.Checked =
                toolStripMenuItem6.Checked = false;
            toolStripMenuItem3.Checked = true;
            DXHelper.speed = 10.0f;
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            toolStripMenuItem2.Checked =
                toolStripMenuItem3.Checked =
                toolStripMenuItem4.Checked =
                toolStripMenuItem5.Checked =
                toolStripMenuItem6.Checked = false;
            toolStripMenuItem4.Checked = true;
            DXHelper.speed = 100.0f;
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            toolStripMenuItem2.Checked =
                toolStripMenuItem3.Checked =
                toolStripMenuItem4.Checked =
                toolStripMenuItem5.Checked =
                toolStripMenuItem6.Checked = false;
            toolStripMenuItem5.Checked = true;
            DXHelper.speed = 1000.0f;
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            toolStripMenuItem2.Checked =
                toolStripMenuItem3.Checked =
                toolStripMenuItem4.Checked =
                toolStripMenuItem5.Checked =
                toolStripMenuItem6.Checked = false;
            toolStripMenuItem6.Checked = true;
            DXHelper.speed = 10000.0f;
        }

        private void treeView6_MouseDown(object sender, MouseEventArgs e)
        {
            TreeNode t = treeView6.SelectedNode;
            if (t == null || t.Name == "" || t.Name == "0")
            {
                moveToolStripMenuItem.Enabled =
                findInTableToolStripMenuItem1.Enabled = false;
            }
            else
            {
                cloneAndAddToLevelToolStripMenuItem.Enabled =
                moveToolStripMenuItem.Enabled = false;
                findInTableToolStripMenuItem1.Enabled = true;
                int n = Convert.ToInt32(t.Name);
                if (n > 0)
                {
                    string s = pcc.GetObject(pcc.Exports[n - 1].idxClass);
                    switch(s)
                    {
                        case"StaticMeshActor":
                        case "InterpActor":
                            cloneAndAddToLevelToolStripMenuItem.Enabled =
                            moveToolStripMenuItem.Enabled = true;
                            break;
                        default:
                            cloneAndAddToLevelToolStripMenuItem.Enabled =
                            moveToolStripMenuItem.Enabled = false;
                            break;
                    }
                }
            }
        }

        private void findInTableToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView6.SelectedNode;
            if (t == null || t.Name == "" || t.Name == "0")
                return;
            int n = Convert.ToInt32(t.Name);
            if (n > 0)
            {
                tabControl1.SelectedTab = tabView3;
                tabControl2.SelectedTab = tabContent5;
                listBox4.SelectedIndex = n - 1;
            }
            else
            {
                tabControl1.SelectedTab = tabView3;
                tabControl2.SelectedTab = tabContent3;
                listBox2.SelectedIndex = -n - 1;
            }
        }

        private void treeView3_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            TreeNode t = treeView3.SelectedNode;
            int n = listBox3.SelectedIndex;
            if (t == null || n == -1 || isSelectOpen)
                return;
            PCCPackage.ExportEntry ex;
            string result;
            switch (t.Index)
            {
                case 0://Class
                case 1://Parent
                case 2://Link
                case 5://Archetype
                    isSelectOpen = true;
                    Objectselect osel = new Objectselect();
                    if (t.Index == 0)
                        osel.Init(pcc, pcc.Exports[n].idxClass);
                    if (t.Index == 1)
                        osel.Init(pcc, pcc.Exports[n].idxParent);
                    if (t.Index == 2)
                        osel.Init(pcc, pcc.Exports[n].idxLink);
                    if (t.Index == 5)
                        osel.Init(pcc, pcc.Exports[n].idxArchetype);
                    osel.Show();
                    while (!osel.PressedOK && !osel.Aborted) Application.DoEvents();
                    isSelectOpen = false;
                    if (osel.Aborted)
                        return;
                    osel.Close();
                    ex = pcc.Exports[n];
                    if(t.Index == 0)
                        ex.idxClass = osel.Result;
                    if (t.Index == 1)
                        ex.idxParent = osel.Result;
                    if (t.Index == 2)
                        ex.idxLink = osel.Result;
                    if (t.Index == 5)
                        ex.idxArchetype = osel.Result;
                    pcc.Exports[n] = ex;
                    RefreshAll();
                    break;
                case 3://Name
                    isSelectOpen = true;
                    Nameselect nsel = new Nameselect();
                    nsel.Init(pcc, pcc.Exports[n].idxName);
                    nsel.Show();
                    while (nsel.Result == -1 && !nsel.IsDisposed) Application.DoEvents();
                    isSelectOpen = false;
                    if (nsel.Result != -2 && nsel.Result != -1)
                    {
                        ex = pcc.Exports[n];
                        ex.idxName = nsel.Result;
                        pcc.Exports[n] = ex;
                        RefreshAll();
                    }
                    nsel.Close();
                    break;
                case 4://Index
                    ex = pcc.Exports[n];
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", ex.Index.ToString(), 0, 0);
                    if (result != "")
                    {
                        int i = 0;
                        if (Int32.TryParse(result, out i))
                        {
                            ex.Index = i;
                            pcc.Exports[n] = ex;
                            RefreshAll();
                        }
                    }
                    break;
                case 7://Flags
                    ex = pcc.Exports[n];
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new hex value", "ME3Explorer", ex.ObjectFlags.ToString("X8"), 0, 0);
                    if (result != "")
                    {
                        int i = 0;
                        if (Int32.TryParse(result, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out i))
                        {
                            ex.ObjectFlags = i;
                            pcc.Exports[n] = ex;
                            RefreshAll();
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private void treeView2_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            int n = listBox2.SelectedIndex;
            if (t == null || n == -1 || isSelectOpen)
                return;
            PCCPackage.ImportEntry imp;
            switch (t.Index)
            {
                case 4://Link
                    isSelectOpen = true;
                    Objectselect osel = new Objectselect();
                    osel.Init(pcc, pcc.Exports[n].idxLink);
                    osel.Show();
                    while (!osel.PressedOK && !osel.Aborted) Application.DoEvents();
                    isSelectOpen = false;
                    if (osel.Aborted)
                        return;
                    osel.Close();
                    imp = pcc.Imports[n];
                    imp.idxLink = osel.Result;
                    pcc.Imports[n] = imp;
                    RefreshAll();
                    break;
                case 0://Package
                case 2://Class
                case 5://Name
                    isSelectOpen = true;
                    Nameselect nsel = new Nameselect();
                    if (t.Index == 0)
                        nsel.Init(pcc, pcc.Imports[n].idxPackage);
                    if (t.Index == 2)
                        nsel.Init(pcc, pcc.Imports[n].idxClass);
                    if (t.Index == 5)
                        nsel.Init(pcc, pcc.Imports[n].idxName);
                    nsel.Show();
                    while (nsel.Result == -1 && !nsel.IsDisposed) Application.DoEvents();
                    isSelectOpen = false;
                    if (nsel.Result != -2 && nsel.Result != -1)
                    {
                        imp = pcc.Imports[n];
                        if (t.Index == 0)
                            imp.idxPackage = nsel.Result;
                        if (t.Index == 2)
                            imp.idxClass = nsel.Result;
                        if (t.Index == 5)
                            imp.idxName = nsel.Result;
                        pcc.Imports[n] = imp;
                        RefreshAll();
                    }
                    nsel.Close();
                    break;
                default:
                    break;
            }
        }

        private void moveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView6.SelectedNode;
            if (t == null || t.Name == "" || t.Name == "0" || isSelectOpen)
                return;
            int n = Convert.ToInt32(t.Name);
            if (n < 0)
                return;
            string s = pcc.GetObject(pcc.Exports[n - 1].idxClass);
            switch (s)
            {
                case "StaticMeshActor":
                case "InterpActor":
                    TreeNode t2 = t.Parent;
                    if (t2 == null || t2.Name == "" || t2.Name == "0")
                        return;
                    int m = Convert.ToInt32(t2.Name);
                    if (m < 0)
                        return;
                    string s2 = pcc.GetObject(pcc.Exports[m - 1].idxClass);
                    if (s2 != "Level")
                        return;
                    foreach(_DXRenderableObject o in DXHelper.level.RenderObjects)
                        if (o.MyIndex == n - 1)
                        {
                            StaticMeshActor stma = (StaticMeshActor)o;
                            int f = -1;
                            for (int i = 0; i < stma.Props.Count; i++)
                                if (pcc.GetName(stma.Props[i].Name) == "location")
                                {
                                    f = i;
                                    break;
                                }
                            if (f == -1)
                            {
                                MessageBox.Show("This object has no \"location\" property! Please add one in order to edit it!");
                                return;
                            }
                            isSelectOpen = true;
                            MoveStuff ms = new MoveStuff();
                            ms.stma = stma;
                            ms.pcc = pcc;
                            ms.Show();
                            while (!ms.PressedOK && !ms.Aborted) Application.DoEvents();
                            isSelectOpen = false;
                            if (ms.PressedOK)
                            {
                                Vector3 v = new Vector3(Convert.ToSingle(ms.textBox2.Text), Convert.ToSingle(ms.textBox3.Text), Convert.ToSingle(ms.textBox4.Text));
                                int len = stma.Props[f].raw.Length;
                                Vector3 org = new Vector3(BitConverter.ToSingle(stma.Props[f].raw, len - 12), BitConverter.ToSingle(stma.Props[f].raw, len - 8), BitConverter.ToSingle(stma.Props[f].raw, len - 4));
                                org += v;
                                int off = stma.Props[f].offend - 12;
                                PCCPackage.ExportEntry ex = pcc.Exports[o.MyIndex];
                                MemoryStream mem = new MemoryStream(pcc.GetObjectData(o.MyIndex));
                                mem.Seek(off, 0);
                                mem.Write(BitConverter.GetBytes(org.X), 0, 4);
                                mem.Write(BitConverter.GetBytes(org.Y), 0, 4);
                                mem.Write(BitConverter.GetBytes(org.Z), 0, 4);
                                ex.Data = mem.ToArray();
                                pcc.Exports[o.MyIndex] = ex;
                            }
                            ms.Close();
                            RefreshAll();
                        }
                    break;
                default:
                    moveToolStripMenuItem.Enabled = false;
                    break;
            }
        }

        private void toolStripButton8_Click(object sender, EventArgs e)
        {
            RefreshAll();
        }

        private void cloneAndAddToLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView6.SelectedNode;
            if (t == null || t.Name == "" || t.Name == "0" || isSelectOpen)
                return;
            int n = Convert.ToInt32(t.Name);
            if (n < 0)
                return;
            TreeNode t2 = t.Parent;
            if (t2 == null || t2.Name == "" || t2.Name == "0")
                return;
            int m = Convert.ToInt32(t2.Name);
            if (m < 0)
                return;
            string s2 = pcc.GetObject(pcc.Exports[m - 1].idxClass);
            if (s2 != "Level")
                return;
            string s = pcc.GetObject(pcc.Exports[n - 1].idxClass);
            timer1.Enabled = false;
            switch (s)
            {
                case "StaticMeshActor":
                case "InterpActor":
                    
                    foreach (_DXRenderableObject o in DXHelper.level.RenderObjects)
                        if (o.MyIndex == n - 1)
                        {
                            StaticMeshActor stma = (StaticMeshActor)o;
                            //Get Indexes
                            int idxstma = n;
                            int idxstmc = stma.STMC.MyIndex + 1;
                            int idxlevel = m;
                            int idxnstma = pcc.Exports.Count + 1;
                            int idxnstmc = pcc.Exports.Count + 2;
                            //Clone entries
                            pcc.CloneEntry(idxstma);
                            pcc.CloneEntry(idxstmc);
                            //update staticmeshactor
                            PCCPackage.ExportEntry ex = pcc.Exports[idxnstma - 1];
                            StaticMeshActor nstma = new StaticMeshActor(pcc, idxnstma - 1);
                            int f = -1;
                            for (int i = 0; i < nstma.Props.Count; i++)
                                if (pcc.GetName(nstma.Props[i].Name) == "StaticMeshComponent")
                                {
                                    f = i;
                                    break;
                                }
                            int off = nstma.Props[f].offend - 4;
                            MemoryStream mem = new MemoryStream(pcc.GetObjectData(idxnstma - 1));
                            mem.Seek(off, 0);
                            mem.Write(BitConverter.GetBytes(idxnstmc), 0, 4);
                            ex.Data = mem.ToArray();
                            ex.Datasize = ex.Data.Length;
                            pcc.Exports[idxnstma - 1] = ex;
                            //update staticmeshcomponent
                            ex = pcc.Exports[idxnstmc - 1];
                            ex.idxLink = idxnstma;
                            pcc.Exports[idxnstmc - 1] = ex;
                            //update level
                            ex = pcc.Exports[idxlevel - 1];
                            byte[] buff = pcc.GetObjectData(idxlevel - 1);
                            Level l = DXHelper.level;
                            off = l.Props[l.Props.Count - 1].offend + 4;
                            mem = new MemoryStream();
                            mem.Write(buff, 0, off);
                            int count = BitConverter.ToInt32(buff, off);
                            mem.Write(BitConverter.GetBytes(count + 1), 0, 4);
                            mem.Write(buff, off + 4, 8);
                            mem.Write(BitConverter.GetBytes(idxnstma), 0, 4);
                            mem.Write(buff, off + 12, buff.Length - off - 12);
                            ex.Data = mem.ToArray();
                            ex.Datasize = ex.Data.Length;
                            pcc.Exports[idxlevel - 1] = ex;
                        }
                    break;
            }
            RefreshAll();
            timer1.Enabled = true;
        }

        private void toolStripButton9_Click(object sender, EventArgs e)
        {
            Hexconverter h = new Hexconverter();
            h.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (importpcc != null && importpcc.Source != null)
                    importpcc.Source.Close();
                importpcc = new PCCPackage(d.FileName, true);
                listBox5.Items.Clear();
                listBox5.Visible=false;
                for (int i = 0; i < importpcc.Exports.Count; i++)
                    listBox5.Items.Add(i.ToString("d6") + " : " + importpcc.GetObjectPath(i + 1) + importpcc.GetObject(i + 1));
                listBox5.Visible = true;
            }
        }

        private void listBox5_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox5.SelectedIndex;
            if (n == -1)
                return;
            hb4.ByteProvider = new DynamicByteProvider(importpcc.GetObjectData(n));
        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (importpcc != null && importpcc.Source != null)
                    importpcc.Source.Close();
                importpcc = new PCCPackage(d.FileName, true);
                listBox6.Items.Clear();
                listBox6.Visible = false;
                for (int i = 0; i < importpcc.Imports.Count; i++)
                    listBox6.Items.Add(i.ToString("d6") + " : " + importpcc.GetObjectPath(-i - 1) + importpcc.GetObject(-i - 1));
                listBox6.Visible = true;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            int n = listBox6.SelectedIndex;
            if (n == -1)
                return;
            PCCPackage.ImportEntry imp = importpcc.Imports[n];
            PCCPackage.ImportEntry nimp = new PCCPackage.ImportEntry();
            nimp.Unk1 = imp.Unk1;
            nimp.Unk2 = imp.Unk2;
            nimp.Unk3 = imp.Unk3;
            nimp.idxLink = 0;
            nimp.idxClass = pcc.FindNameOrAdd(importpcc.GetName(imp.idxClass));
            nimp.idxName = pcc.FindNameOrAdd(importpcc.GetName(imp.idxName));
            nimp.idxPackage = pcc.FindNameOrAdd(importpcc.GetName(imp.idxPackage));
            pcc.Imports.Add(nimp);
            pcc.Header.ImportCount++;
            RefreshAll();
        }        

        private void button2_Click(object sender, EventArgs e)
        {
            int n = listBox5.SelectedIndex;
            if (n == -1)
                return;
            PCCPackage.ExportEntry ex = importpcc.Exports[n];
            PCCPackage.ExportEntry nex = new PCCPackage.ExportEntry();
            byte[] idata = importpcc.GetObjectData(n);
            List <PropertyReader.Property> Props = PropertyReader.getPropList(importpcc, idata);
            int start = PropertyReader.detectStart(importpcc, idata, (uint)importpcc.Exports[n].ObjectFlags);
            int end = start;
            if (Props.Count == 0)
            {
                DialogResult dialogResult = MessageBox.Show("This object contains no properties! Still import as binary data?", "No Properties", MessageBoxButtons.YesNo);
                if (dialogResult == System.Windows.Forms.DialogResult.No)
                {
                    RefreshAll();
                    return;
                }
            }
            else
            {
                end = Props[Props.Count - 1].offend;
                if (end < idata.Length)
                {
                    int diff = idata.Length - end;
                    DialogResult dialogResult = MessageBox.Show("This object contains " + diff + " bytes of binary data after the properties! Still continue to import binary data aswell?", "Binary data", MessageBoxButtons.YesNo);
                    if (dialogResult == System.Windows.Forms.DialogResult.No)
                    {
                        RefreshAll();
                        return;
                    }
                }
            }
            MemoryStream res = new MemoryStream();
            if (((uint)importpcc.Exports[n].ObjectFlags & 0x02000000) != 0)
            {
                byte[] stackdummy = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, //Lets hope for the best :D
                                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,};
                res.Write(stackdummy, 0, stackdummy.Length);
            }
            else
            {
                res.Write(new byte[start], 0, start);
            }
            uint namecount = pcc.Header.NameCount;
            try
            {
                foreach (PropertyReader.Property p in Props)
                {
                    ImportProperty(pcc, importpcc, p, res);
                }
            }
            catch (Exception exe) 
            {
                List<string> temp = new List<string>();
                for (int i = 0; i < namecount; i++)
                    temp.Add(pcc.Names[i]);
                pcc.Names = temp;
                pcc.Header.NameCount = namecount;
                MessageBox.Show("Error occured while trying to importing : " + exe.Message);
            }
            for (int i = end; i < idata.Length; i++)
                res.WriteByte(idata[i]);
            nex.DataLoaded = true;
            nex.Data = res.ToArray();
            nex.Datasize = nex.Data.Length;
            nex.Unk1 = ex.Unk1;
            nex.Unk2 = ex.Unk2;
            nex.Unk3 = new int[ex.Unk3.Length];
            for (int i = 0; i < ex.Unk3.Length; i++)
                nex.Unk3[i] = ex.Unk3[i];
            nex.Unk4 = ex.Unk4;
            nex.Unk5 = ex.Unk5;
            nex.Unk6 = ex.Unk6;
            nex.Unk7 = ex.Unk7;
            nex.Unk8 = ex.Unk8;
            nex.ObjectFlags = ex.ObjectFlags;
            nex.Index = ex.Index;
            nex.idxName = pcc.FindNameOrAdd(importpcc.GetName(ex.idxName));
            nex.idxArchetype = nex.idxClass = nex.idxLink = nex.idxParent = 0;
            pcc.Exports.Add(nex);
            pcc.Header.ExportCount++;
            RefreshAll();
            MessageBox.Show("Done.");
        }

        public void ImportProperty(PCCPackage pcc, PCCPackage importpcc, PropertyReader.Property p, MemoryStream m)
        {
            string name = importpcc.GetName(p.Name);
            int idxname = pcc.FindNameOrAdd(name);
            m.Write(BitConverter.GetBytes(idxname), 0, 4);
            m.Write(new byte[4], 0, 4);
            if (name == "None")
                return;
            string type = importpcc.GetName(BitConverter.ToInt32(p.raw, 8));
            int idxtype = pcc.FindNameOrAdd(type);
            m.Write(BitConverter.GetBytes(idxtype), 0, 4);
            m.Write(new byte[4], 0, 4);
            string name2;
            int idxname2;
            int size, count, pos;
            List<PropertyReader.Property> Props;
            switch (type)
            {
                case "IntProperty":
                case "FloatProperty":
                case "ObjectProperty":
                case "StringRefProperty":
                    m.Write(BitConverter.GetBytes(4), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(p.Value.IntValue), 0, 4);
                    break;
                case "NameProperty":
                    m.Write(BitConverter.GetBytes(8), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(p.Value.IntValue), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    break;
                case "BoolProperty":
                    m.Write(new byte[8], 0, 8);
                    m.WriteByte((byte)p.Value.IntValue);
                    break;
                case "ByteProperty": 
                    name2 = importpcc.GetName(BitConverter.ToInt32(p.raw, 24));
                    idxname2 = pcc.FindNameOrAdd(name2);
                    m.Write(BitConverter.GetBytes(8), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(idxname2), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(p.Value.IntValue), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    break;
                case "DelegateProperty":                    
                    size = BitConverter.ToInt32(p.raw, 16);
                    if (size == 0xC)
                    {
                        name2 = importpcc.GetName(BitConverter.ToInt32(p.raw, 28));
                        idxname2 = pcc.FindNameOrAdd(name2);
                        m.Write(BitConverter.GetBytes(0xC), 0, 4);
                        m.Write(new byte[4], 0, 4);
                        m.Write(new byte[4], 0, 4);
                        m.Write(BitConverter.GetBytes(idxname2), 0, 4);
                        m.Write(new byte[4], 0, 4);
                    }
                    else
                    {
                        m.Write(BitConverter.GetBytes(size), 0, 4);
                        m.Write(new byte[4], 0, 4);
                        for (int i = 0; i < size; i++)
                            m.WriteByte(p.raw[24 + i]);
                    }
                    break;
                case "StrProperty":
                    name2 = p.Value.StringValue;
                    m.Write(BitConverter.GetBytes(4 + name2.Length * 2), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(-name2.Length), 0, 4);
                    foreach (char c in name2)
                    {
                        m.WriteByte((byte)c);
                        m.WriteByte(0);
                    }
                    break;
                case "StructProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    name2 = importpcc.GetName(BitConverter.ToInt32(p.raw, 24));
                    idxname2 = pcc.FindNameOrAdd(name2);
                    pos = 32;
                    Props = new List<PropertyReader.Property>();
                    try
                    {
                        Props = PropertyReader.ReadProp(importpcc, p.raw, pos);
                    }
                    catch (Exception)
                    {
                    }
                    m.Write(BitConverter.GetBytes(size), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(idxname2), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    if (Props.Count == 0)
                    {
                        for (int i = 0; i < size; i++)
                            m.WriteByte(p.raw[32 + i]);
                    }
                    else if (Props[0].TypeVal == PropertyReader.Type.Unknown)
                    {
                        for (int i = 0; i < size; i++)
                            m.WriteByte(p.raw[32 + i]);
                    }
                    else
                    {
                        foreach (PropertyReader.Property pp in Props)
                            ImportProperty(pcc, importpcc, pp, m);
                    }
                    break;
                case "ArrayProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    count = BitConverter.ToInt32(p.raw, 24);
                    pos = 28;
                    List<PropertyReader.Property> AllProps = new List<PropertyReader.Property>();
                    for (int i = 0; i < count; i++)
                    {
                        Props = new List<PropertyReader.Property>();
                        int test1 = BitConverter.ToInt32(p.raw, pos);
                        int test2 = BitConverter.ToInt32(p.raw, pos + 4);
                        if (!importpcc.isName(test1) || test2 != 0)
                            break;
                        if (importpcc.GetName(test1) != "None")
                            if (BitConverter.ToInt32(p.raw, pos + 12) != 0)
                                break;
                        try
                        {
                            Props = PropertyReader.ReadProp(importpcc, p.raw, pos);
                        }
                        catch (Exception)
                        {
                        }
                        AllProps.AddRange(Props);
                        if (Props.Count != 0)
                        {
                            pos = Props[Props.Count - 1].offend;
                        }
                    }
                    m.Write(BitConverter.GetBytes(size), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(count), 0, 4);
                    if (AllProps.Count != 0)
                        foreach (PropertyReader.Property pp in AllProps)
                            ImportProperty(pcc, importpcc, pp, m);
                    else
                        m.Write(p.raw, 28, size - 4);
                    break;
                default:
                    throw new Exception(type);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            MemoryStream m = new MemoryStream();
            if(hb5.ByteProvider != null)
                for (int i = 0; i < hb5.ByteProvider.Length; i++)
                    m.WriteByte(hb5.ByteProvider.ReadByte(i));
            if (radioButton1.Checked)
            {
                int i = 0;
                if(!int.TryParse(textBox1.Text,out i))
                {
                    MessageBox.Show("Invalid input!");
                    return;
                }
                m.Write(BitConverter.GetBytes(i), 0, 4);
            }
            if (radioButton2.Checked)
            {
                float f = 0;
                if (!float.TryParse(textBox2.Text.Replace(".",","), out f))
                {
                    MessageBox.Show("Invalid input!");
                    return;
                }
                m.Write(BitConverter.GetBytes(f), 0, 4);
            }
            if (radioButton3.Checked)
            {
                if(comboBox1.SelectedIndex == -1)
                {
                    MessageBox.Show("Please select a name!");
                    return;
                }
                m.Write(BitConverter.GetBytes(comboBox1.SelectedIndex), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
            }
            if (radioButton4.Checked)
            {
                if (comboBox2.SelectedIndex == -1)
                {
                    MessageBox.Show("Please select an export!");
                    return;
                }
                m.Write(BitConverter.GetBytes(comboBox2.SelectedIndex + 1), 0, 4);
            }
            if (radioButton5.Checked)
            {
                if (comboBox3.SelectedIndex == -1)
                {
                    MessageBox.Show("Please select an import!");
                    return;
                }
                m.Write(BitConverter.GetBytes(-comboBox3.SelectedIndex - 1), 0, 4);
            }
            hb5.ByteProvider = new DynamicByteProvider(m.ToArray());
        }

        private void toolStripButton10_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                byte[] buff = File.ReadAllBytes(d.FileName);
                hb5.ByteProvider = new DynamicByteProvider(buff);
            }
        }

        private void toolStripButton11_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK && hb5.ByteProvider != null)
            {
                MemoryStream m = new MemoryStream();
                for (int i = 0; i < hb5.ByteProvider.Length; i++)
                    m.WriteByte(hb5.ByteProvider.ReadByte(i));
                File.WriteAllBytes(d.FileName, m.ToArray());
                MessageBox.Show("Done.");
            }
        }

        private void toolStripButton12_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            string t = newname.Text;
            pcc.FindNameOrAdd(t);
            RefreshAll();
        }

        private void toolStripButton13_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            int idx = -1;
            if (!int.TryParse(gototext.Text, out idx) || idx < 0 || idx >= pcc.Exports.Count)
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            listBox4.SelectedIndex = idx;
        }

        private void gototext_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 0xd)
                toolStripButton13.PerformClick();
        }

        private void findInExportTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView7.SelectedNode;
            int n = 0;
            if (int.TryParse(t.Name, out n))
            {
                tabControl1.SelectedTab = tabView3;
                if (n > 0)
                {
                    tabControl2.SelectedTab = tabContent5;
                    listBox4.SelectedIndex = n - 1;
                }
                else
                {
                    tabControl2.SelectedTab = tabContent3;
                    listBox2.SelectedIndex = -n - 1;
                }
            }
        }

        private void treeView7_MouseDown_1(object sender, MouseEventArgs e)
        {
            TreeNode t = treeView7.SelectedNode;
            if (t == null)
            {
                findInExportTableToolStripMenuItem.Enabled = false;
                return;
            }
            if (t.Name == "" || t.Name == "0")
            {
                findInExportTableToolStripMenuItem.Enabled = false;
                return;
            }
            findInExportTableToolStripMenuItem.Enabled = true;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            listBox7.Items.Clear();
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.upk|*.upk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Objects = new List<int>();
                importudk = new UDKObject(d.FileName);
                for (int i = 0; i < importudk.Exports.Count; i++)
                    if (importudk.GetClass(importudk.Exports[i].clas) == "StaticMesh")
                    {
                        Objects.Add(i);
                        listBox7.Items.Add("#" + i.ToString("d6") + " : " + importudk.GetName(importudk.Exports[i].name));
                    }
            }
        }

        private void listBox7_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox7.SelectedIndex;
            if (n == -1 || importudk == null)
                return;
            int idx = Objects[n];
            UDKLibWV.Classes.StaticMesh stm = new UDKLibWV.Classes.StaticMesh(importudk, idx);
            string s = "Bound Size 1 = " + stm.Bounds1.Length + " bytes \n";
            s += "Bound Size 2 = " + stm.Bounds2.Length + " bytes \n";
            s += "Surfaces Size = " + stm.Surfs.Length + " bytes \n";
            s += "Unk1 Size = " + stm.Unk1.Length + " bytes \n";
            s += "Unk2 Size = " + stm.Unk2.Length + " bytes \n";
            s += "Mats Size = " + stm.Mats.Length + " bytes \n";
            s += "Tris Size = " + stm.Tris.Length + " bytes \n";
            s += "Unk3 Size = " + stm.Unk3.Length + " bytes \n";
            s += "UVs Size = " + stm.UVs.Length + " bytes \n";
            s += "Unk4 Size = " + stm.Unk4.Length + " bytes \n";
            s += "Indexes 1 Size = " + stm.Indexes1.Length + " bytes \n";
            s += "Indexes 2 Size = " + stm.Indexes2.Length + " bytes \n";
            s += "Indexes 3 Size = " + stm.Indexes3.Length + " bytes \n";
            s += "Rest Size = " + stm.Rest.Length + " bytes \n";
            richTextBox1.Text = s;
        }

        private void button7_Click(object sender, EventArgs ea)
        {
            int n = listBox7.SelectedIndex;
            if (pcc == null || n == -1 || importudk == null)
                return;
            PCCPackage.ExportEntry e;
            bool overwrite = toolStripComboBox2.SelectedIndex == 1;
            int i = 0;
            if(overwrite)
            {
                if (Int32.TryParse(toolStripTextBox1.Text, out i))
                {
                    e = pcc.Exports[i];
                }
                else
                    return;
            }
            else
                e = new PCCPackage.ExportEntry();
            UDKObject.ExportEntry im = importudk.Exports[Objects[n]];            
            e.idxArchetype = 0;
            if (!overwrite)
            {
                e.idxLink = 0;
                e.idxName = pcc.FindNameOrAdd(importudk.GetName(im.name));
            }
            e.idxParent = 0;
            
            e.idxClass = pcc.FindClass("StaticMesh");
            e.ObjectFlags = 0x000F0004;
            e.Unk2 = 1;
            e.Unk3 = new int[0];
            MemoryStream m = new MemoryStream();
            CopySTMfromUDK(m, importudk, Objects[n]);
            e.Data = m.ToArray();
            e.DataLoaded = true;
            e.Datasize = e.Data.Length;
            if (overwrite)
                pcc.Exports[i] = e;
            else
            {
                pcc.Exports.Add(e);
                pcc.Header.ExportCount++;
            }
            listBox7.Items.Clear();
            richTextBox1.Text = "";
            importudk = null;
            RefreshAll();
            MessageBox.Show("Done");
        }

        private void CopySTMfromUDK(MemoryStream m, UDKObject u, int idx)
        {
            UDKLibWV.Classes.StaticMesh stm = new UDKLibWV.Classes.StaticMesh(u, idx);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            WriteName(m, pcc.FindNameOrAdd("BodySetup"));
            WriteName(m, pcc.FindNameOrAdd("ObjectProperty"));
            WriteInt(m, 4);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteName(m, pcc.FindNameOrAdd("UseSimpleBoxCollision"));
            WriteName(m, pcc.FindNameOrAdd("BoolProperty"));
            WriteInt(m, 0);
            WriteInt(m, 0);
            m.WriteByte(1);
            WriteName(m, pcc.FindNameOrAdd("None"));
            m.Write(stm.Bounds1, 0, stm.Bounds1.Length);
            WriteInt(m, 0);
            m.Write(stm.Bounds2, 0, stm.Bounds2.Length);
            m.Write(stm.Surfs, 0, stm.Surfs.Length);
            m.Write(stm.Faces, 0, stm.Faces.Length);
            WriteInt(m, 18);
            WriteInt(m, 1);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            m.Write(stm.Mats, 0, stm.Mats.Length);
            m.Write(stm.Tris, 0, 8);
            WriteInt(m, 1);
            m.Write(stm.Tris, 0, stm.Tris.Length);            
            m.Write(stm.Unk3, 0, stm.Unk3.Length);
            WriteInt(m, 0);
            m.Write(stm.UVs, 0, stm.UVs.Length);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 4);
            WriteInt(m, 0);
            WriteInt(m, 4);
            m.Write(stm.Unk4, 4, 8);
            m.Write(stm.Indexes1, 0, stm.Indexes1.Length);
            WriteInt(m, 2);
            WriteInt(m, 0);
            WriteInt(m, 0x10);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 1);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
        }

        private void WriteInt(MemoryStream m, int idx)
        {
            m.Write(BitConverter.GetBytes(idx), 0, 4);
        }

        private void WriteName(MemoryStream m, int idx)
        {
            WriteInt(m, idx);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
        }

        private void hb2_SelectionLengthChanged(object sender, EventArgs e)
        {
            int start = (int)hb2.SelectionStart;
            int len = (int)hb2.SelectionLength;
            int size = (int)hb2.ByteProvider.Length;
            if (start != -1 && start + len < size && len > 0)
            {
                string s = "Start=0x" + start.ToString("X8") + " ";
                s += "Length=0x" + len.ToString("X8") + " ";
                s += "End=0x" + (start + len - 1).ToString("X8");
                status4.Text = s;
            }
            else
            {
                status4.Text = "Nothing Selected";
            }
        }

        private void toolStripComboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = toolStripComboBox2.SelectedIndex;
            if (n == 1)
            {
                toolStripLabel1.Visible = toolStripTextBox1.Visible = true;
            }
            else
            {
                toolStripLabel1.Visible = toolStripTextBox1.Visible = false;
            }
        }
    }
}
