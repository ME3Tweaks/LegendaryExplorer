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
using System.Diagnostics;
using System.Runtime.InteropServices;
//using ME3LibWV;
using UDKExplorer.UDK;
using UDKExplorer.UDK.Classes;

namespace UDKExplorer
{
    public partial class PackageEditor : Form
    {
        public UDKFile udk;
        public int CurrentView;
        public const int NAMES_VIEW = 0;
        public const int IMPORTS_VIEW = 1;
        public const int EXPORTS_VIEW = 2;
        public const int TREE_VIEW = 3;

        public ME3LibWV.PropGrid pg;
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
            d.Filter = "*.udk;*.upk|*.udk;*.upk";
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
                udk = new UDKFile(s);
                haveCloned = false;
                appendSaveMenuItem.Enabled = true;
                appendSaveMenuItem.ToolTipText = "Save by appending changes to the end of the file";
                interpreterControl.Pcc = udk;
                treeView1.Tag = udk;
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
            for (int i = udk.Imports.Count - 1; i >= 0; i--)
            {
                Classes.Add(-(i + 1) + " : " + udk.Imports[i].ObjectName);
            }
            Classes.Add("0 : Class");
            int count = 1;
            foreach (UDKFile.ExportEntry exp in udk.Exports)
            {
                Classes.Add((count++) + " : " + exp.ObjectName);
            }
            count = 0;
            
            int off = udk.Imports.Count;
            if (n >= 0)
            {
                foreach (UDKFile.NameEntry nameEntry in udk.Names)
                    nameComboBox.Items.Add((count++) + " : " + nameEntry.name);
                foreach (string s in Classes)
                {
                    classComboBox.Items.Add(s);
                    linkComboBox.Items.Add(s);
                    archetypeComboBox.Items.Add(s);
                }
                NameIdx = udk.Exports[n].idxObjectName;
                ClassIdx = udk.Exports[n].idxClass;
                LinkIdx = udk.Exports[n].idxLink;
                IndexIdx = udk.Exports[n].indexValue;
                ArchetypeIdx = udk.Exports[n].idxArchtype;

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
                foreach (UDKFile.NameEntry nameEntry in udk.Names)
                {
                    nameComboBox.Items.Add(count + " : " + nameEntry.name);
                    classComboBox.Items.Add(count + " : " + nameEntry.name);
                    archetypeComboBox.Items.Add(count + " : " + nameEntry.name);
                    count++;
                }
                NameIdx = udk.Imports[n].idxObjectName;
                ClassIdx = udk.Imports[n].idxClassName;
                LinkIdx = udk.Imports[n].idxLink;
                ArchetypeIdx = udk.Imports[n].idxPackageFile;

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
            if (udk == null)
                return;
            ClassNames = new List<int>();
            for (int i = 0; i < udk.Exports.Count; i++)
            {
                ClassNames.Add(udk.Exports[i].idxClass);
            }
            List<string> names = ClassNames.Distinct().Select(x => udk.getClassName(x)).ToList();
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
            if (udk == null)
            {
                listBox1.Visible = true;
                listBox1.EndUpdate();
                treeView1.EndUpdate();
                return;
            }
            if (CurrentView == NAMES_VIEW)
            {
                for (int i = 0; i < udk.Names.Count; i++)
                {
                    listBox1.Items.Add(i + " : " + udk.getName(i));
                }
            }
            if (CurrentView == IMPORTS_VIEW)
            {
                for (int i = 0; i < udk.Imports.Count; i++)
                {
                    string importStr = i + " (0x" + (udk.ImportOffset + (i * UDKFile.ImportEntry.
                        byteSize)).ToString("X4") + "): (" + udk.Imports[i].PackageFile + ") ";
                    if (udk.Imports[i].PackageFullName != "Class" && udk.Imports[i].PackageFullName != "Package")
                    {
                        importStr += udk.Imports[i].PackageFullName + ".";
                    }
                    importStr += udk.Imports[i].ObjectName;
                    listBox1.Items.Add(importStr);
                }
            }
            string s;
            if (CurrentView == EXPORTS_VIEW)
            {
                string PackageFullName, ClassName;
                List<string> exports = new List<string>(udk.Exports.Count);
                for (int i = 0; i < udk.Exports.Count; i++)
                {
                    s = "";
                    if (scanningCoalescedBits && udk.Exports[i].likelyCoalescedVal)
                    {
                        s += "[C] ";
                    }
                    PackageFullName = udk.Exports[i].PackageFullName;
                    if (PackageFullName != "Class" && PackageFullName != "Package")
                        s += PackageFullName + ".";
                    s += udk.Exports[i].ObjectName;
                    ClassName = udk.Exports[i].ClassName;
                    if (ClassName == "ObjectProperty" || ClassName == "StructProperty")
                    {
                        //attempt to find type
                        byte[] data = udk.Exports[i].Data;
                        int importindex = BitConverter.ToInt32(data, data.Length - 4);
                        if (importindex < 0)
                        {
                            //import
                            importindex *= -1;
                            if (importindex > 0) importindex--;
                            if (importindex <= udk.Imports.Count)
                            {
                                s += " (" + udk.Imports[importindex].ObjectName + ")";
                            }
                        }
                        else
                        {
                            //export
                            if (importindex > 0) importindex--;
                            if (importindex <= udk.Exports.Count)
                            {
                                s += " [" + udk.Exports[importindex].ObjectName + "]";
                            }
                        }
                    }
                    exports.Add(i + " : " + s);
                }
                listBox1.Items.AddRange(exports.ToArray());
            }
            if (CurrentView == TREE_VIEW)
            {
                listBox1.Visible = false;
                treeView1.Visible = true;
                treeView1.Nodes.Clear();
                int importsOffset = udk.Exports.Count;
                int link;
                List<TreeNode> nodeList = new List<TreeNode>(udk.Exports.Count + udk.Imports.Count + 1);
                TreeNode node = new TreeNode(udk.FileName);
                node.Tag = true;
                nodeList.Add(node);
                for (int i = 0; i < udk.Exports.Count; i++)
                {
                    node = new TreeNode($"(Exp){i} : {udk.Exports[i].ObjectName}({udk.Exports[i].ClassName})");
                    node.Name = i.ToString();
                    nodeList.Add(node);
                }
                for (int i = 0; i < udk.Imports.Count; i++)
                {
                    node = new TreeNode($"(Imp){i} : {udk.Imports[i].ObjectName}({udk.Imports[i].ClassName})");
                    node.Name = (-i - 1).ToString();
                    nodeList.Add(node);
                }
                int curIndex;
                for (int i = 1; i <= udk.Exports.Count; i++)
                {
                    node = nodeList[i];
                    curIndex = i;
                    while (node.Tag as bool? != true)
                    {
                        node.Tag = true;
                        curIndex = udk.getEntry(curIndex).idxLink;
                        link = curIndex >= 0 ? curIndex : (-curIndex + importsOffset);
                        nodeList[link].Nodes.Add(node);
                        node = nodeList[link];
                    }
                }
                for (int i = 1; i <= udk.Imports.Count; i++)
                {
                    node = nodeList[i + importsOffset];
                    curIndex = -i;
                    while (node.Tag as bool? != true)
                    {
                        node.Tag = true;
                        curIndex = udk.getEntry(curIndex).idxLink;
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
                    hb2.ByteProvider = new DynamicByteProvider(udk.Exports[n].header);
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
                    hb2.ByteProvider = new DynamicByteProvider(udk.Imports[n].header);
                    UpdateStatusIm(n);
                    if (tabControl1.TabPages.ContainsKey(nameof(interpreterTab)))
                    {
                        tabControl1.TabPages.Remove(interpreterTab);
                    }
                    if (tabControl1.TabPages.ContainsKey(nameof(propertiesTab)))
                    {
                        tabControl1.TabPages.Remove(propertiesTab);
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
                textBox1.Text = udk.Exports[n].ObjectName;
                textBox2.Text = udk.Exports[n].ClassName;
                superclassTextBox.Text = udk.Exports[n].ClassParent;
                textBox3.Text = udk.Exports[n].PackageFullName;
                textBox4.Text = udk.Exports[n].header.Length + " bytes";
                textBox5.Text = udk.Exports[n].indexValue.ToString();
                textBox6.Text = udk.Exports[n].ArchtypeName;
                if (udk.Exports[n].idxArchtype != 0)
                    textBox6.Text += " (" + ((udk.Exports[n].idxArchtype < 0) ? "imported" : "local") + " class) " + udk.Exports[n].idxArchtype;
                textBox10.Text = "0x" + udk.Exports[n].ObjectFlags.ToString("X16");
                textBox7.Text = udk.Exports[n].DataSize + " bytes";
                textBox8.Text = "0x" + udk.Exports[n].DataOffset.ToString("X8");
                textBox9.Text = udk.Exports[n].DataOffset.ToString();
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
                textBox1.Text = udk.Imports[n].ObjectName;
                textBox2.Text = udk.Imports[n].ClassName;
                textBox3.Text = udk.Imports[n].PackageFullName;
                textBox4.Text = udk.Imports[n].header.Length + " bytes";
            }
        }

        public void UpdateStatusEx(int n)        
        {
            toolStripStatusLabel1.Text = $"Class:{udk.Exports[n].ClassName} Flags: 0x{udk.Exports[n].ObjectFlags.ToString("X16")}";
            toolStripStatusLabel1.ToolTipText = "";
            foreach (string row in UnrealFlags.flagdesc)
            {
                string[] t = row.Split(',');
                ulong l = ulong.Parse(t[1].Trim(), System.Globalization.NumberStyles.HexNumber);
                if ((l & udk.Exports[n].ObjectFlags) != 0)
                {
                    toolStripStatusLabel1.Text += "[" + t[0].Trim() + "] ";
                    toolStripStatusLabel1.ToolTipText += "[" + t[0].Trim() + "] : " + t[2].Trim() + "\n";
                }
            }
        }

        public void UpdateStatusIm(int n)
        {
            toolStripStatusLabel1.Text = $"Class:{udk.Imports[n].ClassName} Link: {udk.Imports[n].idxLink} ";
            toolStripStatusLabel1.ToolTipText = "";
        }

        public void PreviewProps(int n)
        {
            List<PropertyReader.Property> p = PropertyReader.getPropList(udk, udk.Exports[n]);
            pg = new ME3LibWV.PropGrid();
            propGrid.SelectedObject = pg;
            pg.Add(new ME3LibWV.CustomProperty("Name", "_Meta", udk.Exports[n].ObjectName, typeof(string), true, true));
            pg.Add(new ME3LibWV.CustomProperty("Class", "_Meta", udk.Exports[n].ClassName, typeof(string), true, true));
            pg.Add(new ME3LibWV.CustomProperty("Data Offset", "_Meta", udk.Exports[n].DataOffset, typeof(int), true, true));
            pg.Add(new ME3LibWV.CustomProperty("Data Size", "_Meta", udk.Exports[n].DataSize, typeof(int), true, true));
            for (int l = 0; l < p.Count; l++)
                pg.Add(PropertyReader.PropertyToGrid(p[l], udk));            
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
            if (name == "nameindex" || name == "index" ||  parentVal == typeof(ColorProp) || parentVal == typeof(VectorProp) || parentVal == typeof(RotatorProp) || parentVal == typeof(LinearColorProp))
            {
                name = parent.Label;
            }
            UDKFile.ExportEntry ent = udk.Exports[n];
            List<PropertyReader.Property> p = PropertyReader.getPropList(udk, ent);
            int m = -1;
            for (int i = 0; i < p.Count; i++)
                if (udk.getName(p[i].Name) == name)
                    m = i;
            if (m == -1)
                return;
            byte[] buff2;
            switch (p[m].TypeVal)
            {
                case PropertyReader.Type.BoolProperty:
                    byte res = 0;
                    if ((bool)e.ChangedItem.Value == true)
                        res = 1;
                    ent.Data[p[m].offsetval] = res;
                    break;
                case PropertyReader.Type.FloatProperty:
                    buff2 = BitConverter.GetBytes((float)e.ChangedItem.Value);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case PropertyReader.Type.IntProperty:            
                    int newv = Convert.ToInt32(e.ChangedItem.Value);
                    int oldv = Convert.ToInt32(e.OldValue);
                    buff2 = BitConverter.GetBytes(newv);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case PropertyReader.Type.StrProperty:
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
                case PropertyReader.Type.StructProperty:
                    if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(ColorProp))
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
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(VectorProp))
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
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(RotatorProp))
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
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(LinearColorProp))
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
                    else if (e.ChangedItem.Value is int)
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
                case PropertyReader.Type.ByteProperty:
                case PropertyReader.Type.NameProperty:
                    if (e.ChangedItem.Value is int)
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
                case PropertyReader.Type.ObjectProperty:
                    if (e.ChangedItem.Value is int)
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
            udk.Exports[n] = ent;
            propGrid.ExpandAllGridItems();
            Preview();
        }

        private void appendSaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (udk == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.udk;*.upk|*.udk;*.upk";
            if (d.ShowDialog() == DialogResult.OK)
            {
                udk.appendSave(d.FileName, true);
                MessageBox.Show("Done");
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (udk == null)
                return;
            udk.save();
            MessageBox.Show("Done");
        }

        private void saveHexChangesButton_Click(object sender, EventArgs e)
        {
            int n;
            if (udk == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            MemoryStream m = new MemoryStream();
            IByteProvider provider = interpreterControl.hb1.ByteProvider;
            for (int i = 0; i < provider.Length; i++)
                m.WriteByte(provider.ReadByte(i));
            udk.Exports[n].Data = m.ToArray();

            Preview();
        }

        private void Search()
        {
            if (udk == null)
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
                for (int i = start; i < udk.Names.Count; i++)
                    if (udk.getName(i).ToLower().Contains(searchBox.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == IMPORTS_VIEW)
            {
                for (int i = start; i < udk.Imports.Count; i++)
                    if (udk.Imports[i].ObjectName.ToLower().Contains(searchBox.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == EXPORTS_VIEW)
            {
                for (int i = start; i < udk.Exports.Count; i++)
                    if (udk.Exports[i].ObjectName.ToLower().Contains(searchBox.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
        }

        private void Find()
        {
            if (udk == null)
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
            for (int i = start; i < udk.Exports.Count; i++)
                if (udk.Exports[i].ClassName == cls)
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
            string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\UDKhistory.log";
            if (File.Exists(path))
            {
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
            string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\UDKhistory.log";
            if (File.Exists(path))
                File.Delete(path);
            Directory.CreateDirectory(Directory.GetParent(path).ToString());
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
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
            if (result != "" || udk.findName(result) == -1)
            {
                udk.FindNameOrAdd(result);
                if (CurrentView == NAMES_VIEW)
                {
                    int scrollTo = listBox1.TopIndex + 1;
                    int selected = listBox1.SelectedIndex;
                    RefreshView();
                    listBox1.SelectedIndex = selected;
                    listBox1.TopIndex = scrollTo;
                }
                byte[] buff = BitConverter.GetBytes(udk.Names.Count - 1);
                string s = "";
                for (int i = 0; i < 4; i++)
                {
                    s += buff[i].ToString("X2");
                }
                MessageBox.Show("\"" + result + "\" added at index " + (udk.Names.Count - 1) + " (" + s + ")");
            }
            else if(udk.findName(result) != -1)
            {
                MessageBox.Show($"\"{result}\" already exists at index {udk.findName(result)}");
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
            if (udk == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            List<PropertyReader.Property> prop = PropertyReader.getPropList(udk, udk.Exports[n]);
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            d.FileName = udk.Exports[n].ObjectName + ".bin";
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                byte[] buff = udk.Exports[n].Data;
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
            if (udk == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            InterpreterHost ip = new InterpreterHost(udk, n);
            ip.Text = "Interpreter (Package Editor)";
            ip.MdiParent = this.MdiParent;
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
                if(n >= -udk.Imports.Count && n < udk.Exports.Count)
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
            if (udk == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.BIN|*.BIN";
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                fs.Write(udk.Exports[n].Data, 0, udk.Exports[n].Data.Length);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        private void replaceWithBINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (udk == null || !GetSelected(out n) || n < 0)
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
                udk.Exports[n].Data = buff;
                MessageBox.Show("Done.");
            }
        }

        private void reconstructionSave_Click(object sender, EventArgs e)
        {
            if (udk == null)
                return;
            if (udk.Exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"))
            {
                var res = MessageBox.Show("This file contains a SeekFreeShaderCache. Performing a reconstruction save will cause a crash when ME3 attempts to load this file.\n" +
                    "Do you want to visit a forum thread with more information and a possible solution?",
                    "I'm sorry, Dave. I'm afraid I can't do that.", MessageBoxButtons.YesNo, MessageBoxIcon.Stop);
                if (res == DialogResult.Yes)
                {
                    Process.Start("http://me3explorer.freeforums.org/research-how-to-turn-your-dlc-udk-into-a-vanilla-one-t2264.html");
                }
                return;
            }
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.udk;*.upk|*.udk;*.upk";
            if (d.ShowDialog() == DialogResult.OK)
            {
                udk.saveByReconstructing(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void editBlockingVolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (udk == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            if (udk.Exports[n].ClassName.Contains("BlockingVolume") || udk.Exports[n].ClassName.Contains("SFXDoor"))
            {
                List<PropertyReader.Property> props = PropertyReader.getPropList(udk, udk.Exports[n]);
                foreach (PropertyReader.Property p in props)
                {
                    if (udk.getName(p.Name) == "location")
                    {
                        float x = BitConverter.ToSingle(p.raw, 32);
                        float y = BitConverter.ToSingle(p.raw, 36);
                        float z = BitConverter.ToSingle(p.raw, 40);
                        Application.DoEvents();
                        string nx = Microsoft.VisualBasic.Interaction.InputBox("New X Value:", "Edit Location", x.ToString());
                        string ny = Microsoft.VisualBasic.Interaction.InputBox("New Y Value:", "Edit Location", y.ToString());
                        string nz = Microsoft.VisualBasic.Interaction.InputBox("New Z Value:", "Edit Location", z.ToString());
                        if (nx == "" || ny == "" || nz == "")
                            return;
                        x = Convert.ToSingle(nx);
                        y = Convert.ToSingle(ny);
                        z = Convert.ToSingle(nz);
                        byte[] buff = udk.Exports[n].Data;
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
                        udk.Exports[n].Data = buff;
                        MessageBox.Show("Done.");
                    }
                }
            }
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
            int off = udk.Imports.Count;

            int n;
            if (udk == null || !GetSelected(out n) ||
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
                udk.Exports[n].idxObjectName = NameIdx;
                udk.Exports[n].idxClass = ClassIdx;
                udk.Exports[n].idxLink = LinkIdx;
                udk.Exports[n].indexValue = IndexIdx;
                udk.Exports[n].idxArchtype = ArchetypeIdx;
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
                udk.Imports[n].idxObjectName = NameIdx;
                udk.Imports[n].idxClassName = ClassIdx;
                udk.Imports[n].idxLink = LinkIdx;
                udk.Imports[n].idxPackageFile = ArchetypeIdx;
                n = -n - 1;
            }
            RefreshView();
            goToNumber(n);
        }
        
        private void exportFaceFXToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            int n;
            if (udk == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            if (udk.Exports[n].ClassName == "FaceFXAsset" || udk.Exports[n].ClassName == "FaceFXAnimSet")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.fxa|*.fxa";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    byte[] buff = udk.Exports[n].Data;
                    List<PropertyReader.Property> props = PropertyReader.getPropList(udk, udk.Exports[n]);
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
            if (udk == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            if (udk.Exports[n].ClassName == "FaceFXAsset" || udk.Exports[n].ClassName == "FaceFXAnimSet")
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.fxa|*.fxa";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    byte[] buff = udk.Exports[n].Data;
                    List<PropertyReader.Property> props = PropertyReader.getPropList(udk, udk.Exports[n]);
                    int start = props[props.Count - 1].offend;
                    MemoryStream m = new MemoryStream();
                    m.Write(buff, 0, start);
                    byte[] import = File.ReadAllBytes(d.FileName);
                    m.Write(BitConverter.GetBytes(import.Length), 0, 4);
                    m.Write(import, 0, import.Length);
                    udk.Exports[n].Data = m.ToArray();
                    udk.save();
                    Preview();
                    MessageBox.Show("Done.");
                }
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
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList().Where(f => f.EndsWith(".udk") || f.EndsWith(".upk")).ToList();
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
                Clipboard.SetText(udk.getName(listBox1.SelectedIndex));
            }
        }

        private void hexConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + @"\HexConverterWPF.exe"))
            {
                Process.Start(loc + @"\HexConverterWPF.exe");
            }
        }

        private void saveHeaderHexChangesBtn_Click(object sender, EventArgs e)
        {
            int n;
            if (udk == null || !GetSelected(out n) || n < 0)
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
            udk.Exports[n].header = m.ToArray();

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
            if (!udk.canClone())
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
                    UDKFile.ExportEntry ent = udk.Exports[n].Clone();
                    udk.addExport(ent);
                    RefreshView();
                    goToNumber(udk.Exports.Count - 1); 
                }
                else
                {
                    UDKFile.ImportEntry ent = udk.Imports[-n - 1].Clone();
                    udk.addImport(ent);
                    RefreshView();
                    goToNumber(CurrentView == TREE_VIEW ? -udk.Imports.Count : udk.Imports.Count - 1);
                }
            }
        }

        private void cloneTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!udk.canClone())
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
                    nextIndex = udk.Exports.Count;
                    UDKFile.ExportEntry exp = udk.Exports[n].Clone();
                    udk.addExport(exp);

                    n = nextIndex + 1;
                }
                else
                {
                    nextIndex = -udk.Imports.Count - 1;
                    UDKFile.ImportEntry imp = udk.Imports[-n - 1].Clone();
                    udk.addImport(imp);

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
                        nextIndex = udk.Exports.Count + 1;
                        UDKFile.ExportEntry exp = udk.Exports[index].Clone();
                        exp.idxLink = n;
                        udk.addExport(exp);
                    }
                    else
                    {
                        nextIndex = -udk.Imports.Count - 1;
                        UDKFile.ImportEntry imp = udk.Imports[-index - 1].Clone();
                        imp.idxLink = n;
                        udk.addImport(imp);
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
                    if (!udk.canClone())
                    {
                        return;
                    }
                    haveCloned = true;
                    appendSaveMenuItem.Enabled = false;
                    appendSaveMenuItem.ToolTipText = "This method cannot be used if importing has occured.";

                    UDKFile importudk = sourceNode.TreeView.Tag as UDKFile;
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
                        if(!importExport(importudk, n, link))
                        {
                            return;
                        }
                        nextIndex = udk.Exports.Count;
                    }
                    else
                    {
                        importImport(importudk, -n - 1, link);
                        nextIndex = -udk.Imports.Count;
                    }
                    if (sourceNode.Nodes.Count > 0)
                    {
                        importTree(sourceNode, importudk, nextIndex);
                    }

                    RefreshView();
                    goToNumber(n >= 0 ? udk.Exports.Count - 1 : -udk.Imports.Count);
                }
            }
        }

        private bool importTree(TreeNode sourceNode, UDKFile importudk, int n)
        {
            int nextIndex;
            int index;
            foreach (TreeNode node in sourceNode.Nodes)
            {
                index = Convert.ToInt32(node.Name);
                if (index >= 0)
                {
                    if(!importExport(importudk, index, n))
                    {
                        return false;
                    }
                    nextIndex = udk.Exports.Count;
                }
                else
                {
                    importImport(importudk, -index - 1, n);
                    nextIndex = -udk.Imports.Count;
                }
                if (node.Nodes.Count > 0)
                {
                    if(!importTree(node, importudk, nextIndex))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void importImport(UDKFile importudk, int n, int link)
        {
            UDKFile.ImportEntry imp = importudk.Imports[n];
            UDKFile.ImportEntry nimp = new UDKFile.ImportEntry(udk, imp.header);
            nimp.idxLink = link;
            nimp.idxClassName = udk.FindNameOrAdd(importudk.getName(imp.idxClassName));
            nimp.idxObjectName = udk.FindNameOrAdd(importudk.getName(imp.idxObjectName));
            nimp.idxPackageFile = udk.FindNameOrAdd(importudk.getName(imp.idxPackageFile));
            udk.addImport(nimp);
        }

        private bool importExport(UDKFile importudk, int n, int link)
        {
            UDKFile.ExportEntry ex = importudk.Exports[n];
            UDKFile.ExportEntry nex = new UDKFile.ExportEntry();
            byte[] idata = ex.Data;
            List<PropertyReader.Property> Props = PropertyReader.getPropList(importudk, ex);
            int start = PropertyReader.detectStart(importudk, idata, importudk.Exports[n].ObjectFlags);
            int end = start;
            if (Props.Count != 0)
            {
                end = Props[Props.Count - 1].offend;
            }
            MemoryStream res = new MemoryStream();
            if ((importudk.Exports[n].ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
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
            List<UDKFile.NameEntry> names = udk.Names.ToList();
            try
            {
                foreach (PropertyReader.Property p in Props)
                {
                    PropertyReader.ImportProperty(udk, importudk, p, importudk.getObjectName(ex.idxClass), res);
                }
            }
            catch (Exception exception)
            {
                //restore namelist
                udk.Names = names;
                MessageBox.Show("Error occured while trying to import " + ex.ObjectName + " : " + exception.Message);
                return false;
            }

            for (int i = end; i < idata.Length; i++)
                res.WriteByte(idata[i]);

            nex.header = (byte[])ex.header.Clone();
            nex.Data = res.ToArray();
            nex.DataSize = nex.Data.Length;
            nex.idxObjectName = udk.FindNameOrAdd(importudk.getName(ex.idxObjectName));
            nex.idxLink = link;
            nex.idxArchtype = nex.idxClass = nex.idxClassParent = 0;
            nex.udkRef = udk;
            udk.addExport(nex);
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
