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
using KFreonLib.MEDirectories;
using UsefulThings;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ME3Explorer
{
    public partial class PackageEditor : Form
    {
        public PCCObject pcc;
        public int CurrentView;
        public const int NAMES_VIEW = 0;
        public const int IMPORTS_VIEW = 1;
        public const int EXPORTS_VIEW = 2;
        public const int TREE_VIEW = 3;

        public PropGrid pg;
        private string currentFile;
        private bool haveCloned;

        private List<int> ClassNames;

        public bool IsFromDLC = false;
        public string DLCPath;
        public string inDLCFilename;


        public PackageEditor()
        {
            InitializeComponent();
            LoadRecentList();
            RefreshRecent();
            tabControl1.TabPages.Remove(scriptTab);
            
            SetView(EXPORTS_VIEW);
            interpreterControl.PropertyValueChanged += InterpreterControl_PropertyValueChanged;
            interpreterControl.saveHexButton.Click += saveHexChangesButton_Click;
        }

        public void LoadMostRecent()
        {
            if (RFiles != null && RFiles.Count != 0)
            {
                int index = RFiles.Count - 1;
                if (File.Exists(RFiles[index]))
                    LoadFile(RFiles[index]);
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadFile(d.FileName);
                AddRecent(d.FileName);
                SaveRecentList();
            }
        }

        public void LoadFile(string s, bool isfromdlc = false)
        {
            try
            {
                currentFile = s;
                pcc = new PCCObject(s);
                haveCloned = false;
                appendSaveMenuItem.Enabled = true;
                appendSaveMenuItem.ToolTipText = "Save by appending changes to the end of the file";
                interpreterControl.Pcc = pcc;
                treeView1.Tag = pcc;
                RefreshView();
                InitStuff();
                if (!isfromdlc)
                    status2.Text = "@" + Path.GetFileName(s);
                else
                    status2.Text = "@" + inDLCFilename;
                IsFromDLC = isfromdlc;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error:\n" + e.Message);
            }
        }

        public void RefreshMetaData()
        {
            int NameIdx, ClassIdx, LinkIdx, IndexIdx, ArchetypeIdx;
            if (tabControl1.SelectedTab != metaDataPage)
            {
                return;
            }
            int n;
            if (!GetSelected(out n))
            {
                return;
            }
            nameComboBox.BeginUpdate();
            classComboBox.BeginUpdate();
            linkComboBox.BeginUpdate();

            nameComboBox.Items.Clear();
            classComboBox.Items.Clear();
            linkComboBox.Items.Clear();
            archetypeComboBox.Items.Clear();
            List<string> Classes = new List<string>();
            for (int i = pcc.Imports.Count - 1; i >= 0; i--)
            {
                Classes.Add(-(i + 1) + " : " + pcc.Imports[i].ObjectName);
            }
            Classes.Add("0 : Class");
            int count = 1;
            foreach (PCCObject.ExportEntry exp in pcc.Exports)
            {
                Classes.Add((count++) + " : " + exp.ObjectName);
            }
            count = 0;
            
            int off = pcc.Imports.Count;
            if (n >= 0)
            {
                foreach (string s in pcc.Names)
                    nameComboBox.Items.Add((count++) + " : " + s);
                foreach (string s in Classes)
                {
                    classComboBox.Items.Add(s);
                    linkComboBox.Items.Add(s);
                    archetypeComboBox.Items.Add(s);
                }
                NameIdx = pcc.Exports[n].idxObjectName;
                ClassIdx = pcc.Exports[n].idxClass;
                LinkIdx = pcc.Exports[n].idxLink;
                IndexIdx = pcc.Exports[n].indexValue;
                ArchetypeIdx = pcc.Exports[n].idxArchtype;

                archetypeLabel.Text = "Archetype";
                indexTextBox.Visible = indexLabel.Visible = true;

                classComboBox.SelectedIndex = ClassIdx + off;
                archetypeComboBox.SelectedIndex = ArchetypeIdx + off;
                indexTextBox.Text = IndexIdx.ToString();
            }
            else
            {
                n = -n - 1;
                foreach (string s in Classes)
                {
                    linkComboBox.Items.Add(s);
                }
                count = 0;
                foreach (string s in pcc.Names)
                {
                    nameComboBox.Items.Add(count + " : " + s);
                    classComboBox.Items.Add(count + " : " + s);
                    archetypeComboBox.Items.Add(count + " : " + s);
                    count++;
                }
                NameIdx = pcc.Imports[n].idxObjectName;
                ClassIdx = pcc.Imports[n].idxClassName;
                LinkIdx = pcc.Imports[n].idxLink;
                ArchetypeIdx = pcc.Imports[n].idxPackageFile;

                archetypeLabel.Text = "Package File";
                indexTextBox.Visible = indexLabel.Visible = false;

                classComboBox.SelectedIndex = ClassIdx;
                archetypeComboBox.SelectedIndex = ArchetypeIdx;
            }
            nameComboBox.SelectedIndex = NameIdx;
            linkComboBox.SelectedIndex = LinkIdx + off;

            nameComboBox.EndUpdate();
            classComboBox.EndUpdate();
            linkComboBox.EndUpdate();
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
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                ClassNames.Add(pcc.Exports[i].idxClass);
            }
            List<string> names = ClassNames.Distinct().Select(x => pcc.getClassName(x)).ToList();
            names.Sort();
            combo1.BeginUpdate();
            combo1.Items.Clear();
            combo1.Items.AddRange(names.ToArray());
            combo1.EndUpdate();
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
            listBox1.BeginUpdate();
            treeView1.BeginUpdate();
            listBox1.Items.Clear();
            if (pcc == null)
            {
                listBox1.Visible = true;
                listBox1.EndUpdate();
                treeView1.EndUpdate();
                return;
            }
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
            {
                string PackageFullName, ClassName;
                List<string> exports = new List<string>(pcc.Exports.Count);
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    s = "";
                    if (scanningCoalescedBits && pcc.Exports[i].likelyCoalescedVal)
                    {
                        s += "[C] ";
                    }
                    PackageFullName = pcc.Exports[i].PackageFullName;
                    if (PackageFullName != "Class" && PackageFullName != "Package")
                        s += PackageFullName + ".";
                    s += pcc.Exports[i].ObjectName;
                    ClassName = pcc.Exports[i].ClassName;
                    if (ClassName == "ObjectProperty" || ClassName == "StructProperty")
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
                    exports.Add(i.ToString() + " : " + s);
                }
                listBox1.Items.AddRange(exports.ToArray());
            }
            if (CurrentView == TREE_VIEW)
            {
                listBox1.Visible = false;
                treeView1.Visible = true;
                treeView1.Nodes.Clear();
                int importsOffset = pcc.Exports.Count;
                int link;
                List<TreeNode> nodeList = new List<TreeNode>(pcc.Exports.Count + pcc.Imports.Count + 1);
                TreeNode node = new TreeNode(pcc.pccFileName);
                node.Tag = true;
                nodeList.Add(node);
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    node = new TreeNode($"(Exp){i} : {pcc.Exports[i].ObjectName}({pcc.Exports[i].ClassName})");
                    node.Name = i.ToString();
                    nodeList.Add(node);
                }
                for (int i = 0; i < pcc.Imports.Count; i++)
                {
                    node = new TreeNode($"(Imp){i} : {pcc.Imports[i].ObjectName}({pcc.Imports[i].ClassName})");
                    node.Name = (-i - 1).ToString();
                    nodeList.Add(node);
                }
                int curIndex;
                for (int i = 1; i <= pcc.Exports.Count; i++)
                {
                    node = nodeList[i];
                    curIndex = i;
                    while (node.Tag as bool? != true)
                    {
                        node.Tag = true;
                        curIndex = pcc.getEntry(curIndex).idxLink;
                        link = curIndex >= 0 ? curIndex : (-curIndex + importsOffset);
                        nodeList[link].Nodes.Add(node);
                        node = nodeList[link];
                    }
                }
                for (int i = 1; i <= pcc.Imports.Count; i++)
                {
                    node = nodeList[i + importsOffset];
                    curIndex = -i;
                    while (node.Tag as bool? != true)
                    {
                        node.Tag = true;
                        curIndex = pcc.getEntry(curIndex).idxLink;
                        link = curIndex >= 0 ? curIndex : (-curIndex + importsOffset);
                        nodeList[link].Nodes.Add(node);
                        node = nodeList[link];
                    }
                }
                treeView1.Nodes.Add(nodeList[0]);
                treeView1.Nodes[0].Expand();
            }
            else
            {
                treeView1.Visible = false;
                listBox1.Visible = true;
            }
            treeView1.EndUpdate();
            listBox1.EndUpdate();
        }
                
        private void Button3_Click(object sender, EventArgs e)
        {
            SetView(EXPORTS_VIEW);
            RefreshView();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            SetView(NAMES_VIEW);
            RefreshView();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            SetView(IMPORTS_VIEW);
            RefreshView();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // keep disabled unless we're on the hex tab:
            int n;
            if (tabControl1.SelectedTab == interpreterTab && GetSelected(out n) && n >= 0)
            {
                if (interpreterControl.treeView1.Nodes.Count > 0)
                {
                    interpreterControl.treeView1.Nodes[0].Expand();
                }
            }

            if (tabControl1.SelectedTab == metaDataPage)
            {
                RefreshMetaData();
            }
        }

        public void Preview(bool isRefresh = false)
        {
            int n;
            if (!GetSelected(out n))
            {
                return;
            }
            if (CurrentView == IMPORTS_VIEW || CurrentView == EXPORTS_VIEW || CurrentView == TREE_VIEW)
            {
                tabControl1_SelectedIndexChanged(null, null);
                PreviewInfo(n);
                RefreshMetaData();
                //export
                if (n >= 0)
                {
                    PreviewProps(n);
                    if (!tabControl1.TabPages.ContainsKey(nameof(propertiesTab)))
                    {
                        tabControl1.TabPages.Insert(0, propertiesTab);
                    }
                    if (!tabControl1.TabPages.ContainsKey(nameof(interpreterTab)))
                    {
                        tabControl1.TabPages.Insert(1, interpreterTab);
                    }
                    if (pcc.Exports[n].ClassName == "Function")
                    {
                        if (!tabControl1.TabPages.ContainsKey(nameof(scriptTab)))
                        {
                            tabControl1.TabPages.Add(scriptTab);
                        }
                        Function func = new Function(pcc.Exports[n].Data, pcc);
                        rtb1.Text = func.ToRawText();
                    }
                    else if (tabControl1.TabPages.ContainsKey(nameof(scriptTab)))
                    {
                        tabControl1.TabPages.Remove(scriptTab);
                    }
                    hb2.ByteProvider = new DynamicByteProvider(pcc.Exports[n].header);
                    if (!isRefresh)
                    {
                        interpreterControl.Index = n;
                        interpreterControl.InitInterpreter();
                    }
                    UpdateStatusEx(n); 
                }
                //import
                else
                {
                    n = -n - 1;
                    hb2.ByteProvider = new DynamicByteProvider(pcc.Imports[n].header);
                    UpdateStatusIm(n);
                    if (tabControl1.TabPages.ContainsKey(nameof(interpreterTab)))
                    {
                        tabControl1.TabPages.Remove(interpreterTab);
                    }
                    if (tabControl1.TabPages.ContainsKey(nameof(propertiesTab)))
                    {
                        tabControl1.TabPages.Remove(propertiesTab);
                    }
                    if (tabControl1.TabPages.ContainsKey(nameof(scriptTab)))
                    {
                        tabControl1.TabPages.Remove(scriptTab);
                    }
                }
            }
        }

        public void PreviewInfo(int n)
        {
            if (n >= 0)
            {
                infoHeaderBox.Text = "Export Header";
                superclassTextBox.Visible = superclassLabel.Visible = true;
                textBox6.Visible = label6.Visible = true;
                textBox5.Visible = label5.Visible = true;
                textBox10.Visible = label11.Visible = false;
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
            else
            {
                n = -n - 1;
                infoHeaderBox.Text = "Import Header";
                superclassTextBox.Visible = superclassLabel.Visible = false;
                textBox6.Visible = label6.Visible = false;
                textBox5.Visible = label5.Visible = false;
                textBox10.Visible = label11.Visible = false;
                infoExportDataBox.Visible = false;
                textBox1.Text = pcc.Imports[n].ObjectName;
                textBox2.Text = pcc.Imports[n].ClassName;
                textBox3.Text = pcc.Imports[n].PackageFullName;
                textBox4.Text = pcc.Imports[n].header.Length + " bytes";
            }
        }

        public void UpdateStatusEx(int n)        
        {
            toolStripStatusLabel1.Text = $"Class:{pcc.Exports[n].ClassName} Flags: 0x{pcc.Exports[n].ObjectFlags.ToString("X16")}";
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
            toolStripStatusLabel1.Text = $"Class:{pcc.Imports[n].ClassName} Link: {pcc.Imports[n].idxLink} ";
            toolStripStatusLabel1.ToolTipText = "";
        }

        public void PreviewProps(int n)
        {
            List<Unreal.PropertyReader.Property> p = Unreal.PropertyReader.getPropList(pcc, pcc.Exports[n]);
            pg = new PropGrid();
            propGrid.SelectedObject = pg;
            pg.Add(new CustomProperty("Name", "_Meta", pcc.Exports[n].ObjectName, typeof(string), true, true));
            pg.Add(new CustomProperty("Class", "_Meta", pcc.Exports[n].ClassName, typeof(string), true, true));
            pg.Add(new CustomProperty("Data Offset", "_Meta", pcc.Exports[n].DataOffset, typeof(int), true, true));
            pg.Add(new CustomProperty("Data Size", "_Meta", pcc.Exports[n].DataSize, typeof(int), true, true));
            for (int l = 0; l < p.Count; l++)
                pg.Add(Unreal.PropertyReader.PropertyToGrid(p[l], pcc));            
            propGrid.Refresh();
            propGrid.ExpandAllGridItems();
        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            Preview();
        }

        private bool GetSelected(out int n)
        {
            if (CurrentView == TREE_VIEW && treeView1.SelectedNode != null && treeView1.SelectedNode.Name != "")
            {
                n = Convert.ToInt32(treeView1.SelectedNode.Name);
                return true;
            }
            else if (CurrentView == EXPORTS_VIEW && listBox1.SelectedItem != null)
            {
                n = listBox1.SelectedIndex;
                return true;
            }
            else if (CurrentView == IMPORTS_VIEW && listBox1.SelectedItem != null)
            {
                n = -listBox1.SelectedIndex - 1;
                return true;
            }
            else
            {
                n = 0;
                return false;
            }
        }

        private void InterpreterControl_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            Preview(true);
        }

        private void propGrid_PropertyValueChanged(object o, PropertyValueChangedEventArgs e)
        {
            int n;
            if (!GetSelected(out n) || n < 0)
            {
                return;
            }
            string name = e.ChangedItem.Label;
            GridItem parent = e.ChangedItem.Parent;
            //if (parent != null) name = parent.Label;
            if (parent.Label == "data")
            {
                GridItem parent2 = parent.Parent;
                if (parent2 != null) name = parent2.Label;
            }
            Type parentVal = null;
            if (parent.Value != null)
            {
                parentVal = parent.Value.GetType();
            }
            if (name == "nameindex" || name == "index" ||  parentVal == typeof(Unreal.ColorProp) || parentVal == typeof(Unreal.VectorProp) || parentVal == typeof(Unreal.RotatorProp) || parentVal == typeof(Unreal.LinearColorProp))
            {
                name = parent.Label;
            }
            PCCObject.ExportEntry ent = pcc.Exports[n];
            List<Unreal.PropertyReader.Property> p = Unreal.PropertyReader.getPropList(pcc, ent);
            int m = -1;
            for (int i = 0; i < p.Count; i++)
                if (pcc.Names[p[i].Name] == name)
                    m = i;
            if (m == -1)
                return;
            byte[] buff2;
            switch (p[m].TypeVal)
            {
                case Unreal.PropertyReader.Type.BoolProperty:
                    byte res = 0;
                    if ((bool)e.ChangedItem.Value == true)
                        res = 1;
                    ent.Data[p[m].offsetval] = res;
                    break;
                case Unreal.PropertyReader.Type.FloatProperty:
                    buff2 = BitConverter.GetBytes((float)e.ChangedItem.Value);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case Unreal.PropertyReader.Type.IntProperty:
                case Unreal.PropertyReader.Type.StringRefProperty:                
                    int newv = Convert.ToInt32(e.ChangedItem.Value);
                    int oldv = Convert.ToInt32(e.OldValue);
                    buff2 = BitConverter.GetBytes(newv);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case Unreal.PropertyReader.Type.StrProperty:
                    string s = Convert.ToString(e.ChangedItem.Value);
                    int oldLength = -(int)BitConverter.ToInt64(ent.Data, p[m].offsetval);
                    List<byte> stringBuff = new List<byte>(s.Length * 2);
                    for (int i = 0; i < s.Length; i++)
                    {
                        stringBuff.AddRange(BitConverter.GetBytes(s[i]));
                    }
                    stringBuff.Add(0);
                    stringBuff.Add(0);
                    buff2 = BitConverter.GetBytes((s.LongCount() + 1) * 2 + 4);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval -8 + i] = buff2[i];
                    buff2 = BitConverter.GetBytes(-(s.LongCount() + 1));
                    for (int i = 0; i < 8; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    buff2 = new byte[ent.Data.Length - (oldLength * 2) + stringBuff.Count];
                    int startLength = p[m].offsetval + 4;
                    int startLength2 = startLength + (oldLength * 2);
                    for (int i = 0; i < startLength; i++)
                    {
                        buff2[i] = ent.Data[i];
                    }
                    for (int i = 0; i < stringBuff.Count; i++)
                    {
                        buff2[i + startLength] = stringBuff[i];
                    }
                    startLength += stringBuff.Count;
                    for (int i = 0; i < ent.Data.Length - startLength2; i++)
                    {
                        buff2[i + startLength] = ent.Data[i + startLength2];
                    }
                    ent.Data = buff2;
                    break;
                case Unreal.PropertyReader.Type.StructProperty:
                    if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.ColorProp))
                    {
                        switch (e.ChangedItem.Label)
                        {
                            case "Alpha":
                                ent.Data[p[m].offsetval + 11] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Red":
                                ent.Data[p[m].offsetval + 10] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Green":
                                ent.Data[p[m].offsetval + 9] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Blue":
                                ent.Data[p[m].offsetval + 8] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            default:
                                break;
                        }
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.VectorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "X":
                                offset = 8;
                                break;
                            case "Y":
                                offset = 12;
                                break;
                            case "Z":
                                offset = 16;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            buff2 = BitConverter.GetBytes(Convert.ToSingle(e.ChangedItem.Value));
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + offset + i] = buff2[i];
                        }
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.RotatorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "Pitch":
                                offset = 8;
                                break;
                            case "Yaw":
                                offset = 12;
                                break;
                            case "Roll":
                                offset = 16;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            int val = Convert.ToInt32(Convert.ToSingle(e.ChangedItem.Value) * 65536f / 360f);
                            buff2 = BitConverter.GetBytes(val);
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + offset + i] = buff2[i];
                        }
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.LinearColorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "Red":
                                offset = 8;
                                break;
                            case "Green":
                                offset = 12;
                                break;
                            case "Blue":
                                offset = 16;
                                break;
                            case "Alpha":
                                offset = 20;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            buff2 = BitConverter.GetBytes(Convert.ToSingle(e.ChangedItem.Value));
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + offset + i] = buff2[i];
                        }
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    else if (e.ChangedItem.Value.GetType() == typeof(int))
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
                case Unreal.PropertyReader.Type.ByteProperty:
                case Unreal.PropertyReader.Type.NameProperty:
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
                case Unreal.PropertyReader.Type.ObjectProperty:
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

        private void appendSaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                pcc.altSaveToFile(d.FileName, true);
                MessageBox.Show("Done");
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            if (haveCloned)
            {
                pcc.saveByReconstructing(pcc.pccFileName);
            }
            else
            {
                pcc.altSaveToFile(pcc.pccFileName, true); 
            }
            MessageBox.Show("Done");
        }

        private void saveHexChangesButton_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            MemoryStream m = new MemoryStream();
            IByteProvider provider = interpreterControl.hb1.ByteProvider;
            for (int i = 0; i < provider.Length; i++)
                m.WriteByte(provider.ReadByte(i));
            pcc.Exports[n].Data = m.ToArray();

            Preview();
        }

        private void Search()
        {
            if (pcc == null)
                return;
            int n = listBox1.SelectedIndex;
            if (searchBox.Text == "")
                return;
            int start;
            if (n == -1)
                start = 0;
            else
                start = n + 1;
            
            if (CurrentView == NAMES_VIEW)
            {
                for (int i = start; i < pcc.Names.Count; i++)
                    if (pcc.Names[i].ToLower().Contains(searchBox.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == IMPORTS_VIEW)
            {
                for (int i = start; i < pcc.Imports.Count; i++)
                    if (pcc.Imports[i].ObjectName.ToLower().Contains(searchBox.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == EXPORTS_VIEW)
            {
                for (int i = start; i < pcc.Exports.Count; i++)
                    if (pcc.Exports[i].ObjectName.ToLower().Contains(searchBox.Text.ToLower()))
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
            if (CurrentView != EXPORTS_VIEW)
                return;
            if (combo1.SelectedIndex == -1)
                return;
            string cls = combo1.SelectedItem as string;
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

        private void combo1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                Find();
        }

        private void findClassButtonClick(object sender, EventArgs e)
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
            string s = sender.ToString();
            LoadFile(s);
            RFiles.Remove(s);
            AddRecent(s);
            SaveRecentList();
        }

        private void addNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
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

        private void recentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshRecent();
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

        private void getDumpToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            DumpBin();
        }

        public void DumpBin()
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            List<Unreal.PropertyReader.Property> prop = Unreal.PropertyReader.getPropList(pcc, pcc.Exports[n]);
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            d.FileName = pcc.Exports[n].ObjectName + ".bin";
            if (d.ShowDialog() == DialogResult.OK)
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
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            InterpreterHost ip = new InterpreterHost(pcc, n);
            ip.Text = "Interpreter (Package Editor)";
            ip.MdiParent = this.MdiParent;
            ip.Show();
            taskbar.AddTool(ip, Properties.Resources.interpreter_icon_64x64);
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            Interpreter();
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            if (gotonumber.Text == "")
                return;
            int n = 0;
            if (int.TryParse(gotonumber.Text, out n))
            {
                goToNumber(n);
            }
        }

        private void gotonumber_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)0xD)
            {
                if (gotonumber.Text == "")
                    return;
                int n = 0;
                if (int.TryParse(gotonumber.Text, out n))
                {
                    goToNumber(n);
                }
            }
        }

        private void goToNumber(int n)
        {
            if (CurrentView == TREE_VIEW)
            {
                if(n >= -pcc.Imports.Count && n < pcc.Exports.Count)
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

        private void getDumpToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.BIN|*.BIN";
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                fs.Write(pcc.Exports[n].Data, 0, pcc.Exports[n].Data.Length);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        private void replaceWithBINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
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

        private void reconstructionSave_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            if (pcc.Exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"))
            {
                var res = MessageBox.Show("This file contains a SeekFreeShaderCache. Performing a reconstruction save will cause a crash when ME3 attempts to load this file.\n" +
                    "Do you want to visit a forum thread with more information and a possible solution?",
                    "I'm sorry, Dave. I'm afraid I can't do that.", MessageBoxButtons.YesNo, MessageBoxIcon.Stop);
                if (res == DialogResult.Yes)
                {
                    Process.Start("http://me3explorer.freeforums.org/research-how-to-turn-your-dlc-pcc-into-a-vanilla-one-t2264.html");
                }
                return;
            }
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                pcc.saveByReconstructing(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void editBlockingVolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            if (pcc.Exports[n].ClassName.Contains("BlockingVolume") || pcc.Exports[n].ClassName.Contains("SFXDoor"))
            {
                List<Unreal.PropertyReader.Property> props = Unreal.PropertyReader.getPropList(pcc, pcc.Exports[n]);
                foreach (Unreal.PropertyReader.Property p in props)
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
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            if (!IsFromDLC)
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.bin|*.bin";
                if (d.ShowDialog() == DialogResult.OK)
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
                    string template = File.ReadAllText(loc + "\\exec\\JobTemplate_Binary2.txt");
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
                if (d.ShowDialog() == DialogResult.OK)
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
                    string template = File.ReadAllText(loc + "\\exec\\JobTemplate_Binary3DLC.txt");
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
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
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
                string template = File.ReadAllText(loc + "\\exec\\JobTemplate_Binary3DLC.txt");
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
            string template = File.ReadAllText(loc + "\\exec\\JobTemplate_Binary2.txt");
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
            int NameIdx, ClassIdx, LinkIdx, IndexIdx, ArchetypeIdx;
            int off = pcc.Imports.Count;

            int n;
            if (pcc == null || !GetSelected(out n) ||
                nameComboBox.SelectedIndex == -1 ||
                classComboBox.SelectedIndex == -1 ||
                linkComboBox.SelectedIndex == -1 ||
                archetypeComboBox.SelectedIndex == -1)
                return;
            LinkIdx = linkComboBox.SelectedIndex - off;
            NameIdx = nameComboBox.SelectedIndex;
            if (n >= 0)
            {
                ClassIdx = classComboBox.SelectedIndex - off;
                ArchetypeIdx = archetypeComboBox.SelectedIndex - off;
                if (!int.TryParse(indexTextBox.Text, out IndexIdx))
                {
                    MessageBox.Show("Index must be a number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (LinkIdx == n + 1)
                {
                    MessageBox.Show("Cannot link an object to itself!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                pcc.Exports[n].idxObjectName = NameIdx;
                pcc.Exports[n].idxClass = ClassIdx;
                pcc.Exports[n].idxLink = LinkIdx;
                pcc.Exports[n].indexValue = IndexIdx;
                pcc.Exports[n].idxArchtype = ArchetypeIdx;
            }
            else
            {
                ClassIdx = classComboBox.SelectedIndex;
                ArchetypeIdx = archetypeComboBox.SelectedIndex;
                if (LinkIdx == n)
                {
                    MessageBox.Show("Cannot link an object to itself!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                n = -n - 1;
                pcc.Imports[n].idxObjectName = NameIdx;
                pcc.Imports[n].idxClassName = ClassIdx;
                pcc.Imports[n].idxLink = LinkIdx;
                pcc.Imports[n].idxPackageFile = ArchetypeIdx;
                n = -n - 1;
            }
            RefreshView();
            goToNumber(n);
        }
        
        private void exportFaceFXToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            if (pcc.Exports[n].ClassName == "FaceFXAsset" || pcc.Exports[n].ClassName == "FaceFXAnimSet")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.fxa|*.fxa";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    byte[] buff = pcc.Exports[n].Data;
                    BitConverter.IsLittleEndian = true;
                    List<Unreal.PropertyReader.Property> props = Unreal.PropertyReader.getPropList(pcc, pcc.Exports[n]);
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
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            if (pcc.Exports[n].ClassName == "FaceFXAsset" || pcc.Exports[n].ClassName == "FaceFXAnimSet")
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.fxa|*.fxa";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    byte[] buff = pcc.Exports[n].Data;
                    BitConverter.IsLittleEndian = true;
                    List<Unreal.PropertyReader.Property> props = Unreal.PropertyReader.getPropList(pcc, pcc.Exports[n]);
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

        private void loadFromDLCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.sfar|*.sfar";
            if (d.ShowDialog() == DialogResult.OK)
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
            if (d.ShowDialog() == DialogResult.OK)
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

        private void PackageEditor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void PackageEditor_DragDrop(object sender, DragEventArgs e)
        {
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList().Where(f => f.EndsWith(".pcc")).ToList();
            if (DroppedFiles.Count > 0)
            {
                LoadFile(DroppedFiles[0]);
                AddRecent(DroppedFiles[0]);
                SaveRecentList();
            }
        }

        private void listBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (CurrentView == NAMES_VIEW)
                {
                    nameContextMenuStrip1.Show(MousePosition);
                }
                else
                {
                    contextMenuStrip1.Show(MousePosition);
                }
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentView == NAMES_VIEW && listBox1.SelectedIndex != -1)
            {
                Clipboard.SetText(pcc.Names[listBox1.SelectedIndex]);
            }
        }

        private void hexConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new ME3Creator.Hexconverter()).Show();
        }

        private void saveHeaderHexChangesBtn_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            MemoryStream m = new MemoryStream();
            IByteProvider provider = hb2.ByteProvider;
            if (provider.Length != 0x44)
            {
                MessageBox.Show("Invalid hex length");
                return;
            }
            for (int i = 0; i < provider.Length; i++)
                m.WriteByte(provider.ReadByte(i));
            pcc.Exports[n].header = m.ToArray();

            RefreshView();
            goToNumber(n);
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                treeView1.SelectedNode = e.Node;
                //isn't root
                if (e.Node.Name.Length > 0)
                {
                    //disable clone tree on nodes with no children
                    cloneTreeToolStripMenuItem.Enabled = e.Node.Nodes.Count != 0;
                    nodeContextMenuStrip1.Show(MousePosition);
                }
            }
        }

        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!pcc.canClone())
            {
                return;
            }
            int n = 0;
            if (GetSelected(out n))
            {
                haveCloned = true;
                appendSaveMenuItem.Enabled = false;
                appendSaveMenuItem.ToolTipText = "This method cannot be used if cloning has occured.";

                if (n >= 0)
                {
                    PCCObject.ExportEntry ent = pcc.Exports[n].Clone();
                    pcc.addExport(ent);
                    RefreshView();
                    goToNumber(pcc.Exports.Count - 1); 
                }
                else
                {
                    PCCObject.ImportEntry ent = pcc.Imports[-n - 1].Clone();
                    pcc.addImport(ent);
                    RefreshView();
                    goToNumber(CurrentView == TREE_VIEW ? -pcc.Imports.Count : pcc.Imports.Count - 1);
                }
            }
        }

        private void cloneTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!pcc.canClone())
            {
                return;
            }
            int n = 0;
            if (GetSelected(out n))
            {
                int nextIndex;
                haveCloned = true;
                appendSaveMenuItem.Enabled = false;
                appendSaveMenuItem.ToolTipText = "This method cannot be used if cloning or importing has occured.";

                TreeNode rootNode = treeView1.SelectedNode;
                if (n >= 0)
                {
                    nextIndex = pcc.Exports.Count;
                    PCCObject.ExportEntry exp = pcc.Exports[n].Clone();
                    pcc.addExport(exp);

                    n = nextIndex + 1;
                }
                else
                {
                    nextIndex = -pcc.Imports.Count - 1;
                    PCCObject.ImportEntry imp = pcc.Imports[-n - 1].Clone();
                    pcc.addImport(imp);

                    n = nextIndex;
                }
                cloneTree(n, rootNode);

                RefreshView();
                goToNumber(nextIndex);
            }
        }

        private void cloneTree(int n, TreeNode rootNode)
        {
            int index;
            int nextIndex;
            if (rootNode.Nodes.Count > 0)
            {
                foreach (TreeNode node in rootNode.Nodes)
                {
                    index = Convert.ToInt32(node.Name);
                    if (index >= 0)
                    {
                        nextIndex = pcc.Exports.Count + 1;
                        PCCObject.ExportEntry exp = pcc.Exports[index].Clone();
                        exp.idxLink = n;
                        pcc.addExport(exp);
                    }
                    else
                    {
                        nextIndex = -pcc.Imports.Count - 1;
                        PCCObject.ImportEntry imp = pcc.Imports[-index - 1].Clone();
                        imp.idxLink = n;
                        pcc.addImport(imp);
                    }
                    if (node.Nodes.Count > 0)
                    {
                        cloneTree(nextIndex, node);
                    }
                }
            }
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Copy);
        }

        private void treeView1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
            TreeNode sourceNode;

            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
                TreeNode DestinationNode = ((TreeView)sender).GetNodeAt(pt);
                sourceNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
                if (DestinationNode.TreeView != sourceNode.TreeView)
                {
                    if (!pcc.canClone())
                    {
                        return;
                    }
                    haveCloned = true;
                    appendSaveMenuItem.Enabled = false;
                    appendSaveMenuItem.ToolTipText = "This method cannot be used if importing has occured.";

                    PCCObject importpcc = sourceNode.TreeView.Tag as PCCObject;
                    int n = Convert.ToInt32(sourceNode.Name);
                    int link;
                    if (DestinationNode.Name == "")
                    {
                        link = 0;
                    }
                    else
                    {
                        link = Convert.ToInt32(DestinationNode.Name);
                        link = link >= 0 ? link + 1 : link;
                    }
                    int nextIndex;
                    if (n >= 0)
                    {
                        if(!importExport(importpcc, n, link))
                        {
                            return;
                        }
                        nextIndex = pcc.Exports.Count;
                    }
                    else
                    {
                        importImport(importpcc, -n - 1, link);
                        nextIndex = -pcc.Imports.Count;
                    }
                    if (sourceNode.Nodes.Count > 0)
                    {
                        importTree(sourceNode, importpcc, nextIndex);
                    }

                    RefreshView();
                    goToNumber(n >= 0 ? pcc.Exports.Count - 1 : -pcc.Imports.Count);
                }
            }
        }

        private bool importTree(TreeNode sourceNode, PCCObject importpcc, int n)
        {
            int nextIndex;
            int index;
            foreach (TreeNode node in sourceNode.Nodes)
            {
                index = Convert.ToInt32(node.Name);
                if (index >= 0)
                {
                    if(!importExport(importpcc, index, n))
                    {
                        return false;
                    }
                    nextIndex = pcc.Exports.Count;
                }
                else
                {
                    importImport(importpcc, -index - 1, n);
                    nextIndex = -pcc.Imports.Count;
                }
                if (node.Nodes.Count > 0)
                {
                    if(!importTree(node, importpcc, nextIndex))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void importImport(PCCObject importpcc, int n, int link)
        {
            PCCObject.ImportEntry imp = importpcc.Imports[n];
            PCCObject.ImportEntry nimp = new PCCObject.ImportEntry(pcc, imp.header);
            nimp.idxLink = link;
            nimp.idxClassName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxClassName));
            nimp.idxObjectName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxObjectName));
            nimp.idxPackageFile = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxPackageFile));
            pcc.addImport(nimp);
        }

        private bool importExport(PCCObject importpcc, int n, int link)
        {
            PCCObject.ExportEntry ex = importpcc.Exports[n];
            PCCObject.ExportEntry nex = new PCCObject.ExportEntry();
            byte[] idata = ex.Data;
            List<PropertyReader.Property> Props = PropertyReader.getPropList(importpcc, ex);
            int start = PropertyReader.detectStart(importpcc, idata, (uint)importpcc.Exports[n].ObjectFlags);
            int end = start;
            if (Props.Count != 0)
            {
                end = Props[Props.Count - 1].offend;
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
            //store copy of names list in case something goes wrong
            List<string> names = pcc.Names.ToList();
            try
            {
                foreach (PropertyReader.Property p in Props)
                {
                    PropertyReader.ImportProperty(pcc, importpcc, p, importpcc.getObjectName(ex.idxClass), res);
                }
            }
            catch (Exception exception)
            {
                //restore namelist
                pcc.Names = names;
                MessageBox.Show("Error occured while trying to import " + ex.ObjectName + " : " + exception.Message);
                return false;
            }
            if (importpcc.getObjectName(ex.idxClass) == "SkeletalMesh")
            {
                SkeletalMesh skl = new SkeletalMesh(importpcc, n);
                SkeletalMesh.BoneStruct bone;
                for (int i = 0; i < skl.Bones.Count; i++)
                {
                    bone = skl.Bones[i];
                    string s = importpcc.getNameEntry(bone.Name);
                    bone.Name = pcc.FindNameOrAdd(s);
                    skl.Bones[i] = bone;
                }
                SkeletalMesh.TailNamesStruct tailName;
                for (int i = 0; i < skl.TailNames.Count; i++)
                {
                    tailName = skl.TailNames[i];
                    string s = importpcc.getNameEntry(tailName.Name);
                    tailName.Name = pcc.FindNameOrAdd(s);
                    skl.TailNames[i] = tailName;
                }
                SerializingContainer container = new SerializingContainer(res);
                container.isLoading = false;
                skl.Serialize(container);
            }
            else
            {
                for (int i = end; i < idata.Length; i++)
                    res.WriteByte(idata[i]);
            }
            nex.header = (byte[])ex.header.Clone();
            nex.Data = res.ToArray();
            nex.DataSize = nex.Data.Length;
            nex.idxObjectName = pcc.FindNameOrAdd(importpcc.getNameEntry(ex.idxObjectName));
            nex.idxLink = link;
            nex.idxArchtype = nex.idxClass = nex.idxClassParent = 0;
            nex.pccRef = pcc;
            pcc.addExport(nex);
            return true;
        }

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
                TreeNode DestinationNode = ((TreeView)sender).GetNodeAt(pt);
                TreeNode NewNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
                if (DestinationNode != null && DestinationNode.TreeView != NewNode.TreeView)
                {
                    treeView1.SelectedNode = DestinationNode;
                    e.Effect = DragDropEffects.Copy;
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                }
            }
        }
    }
}
