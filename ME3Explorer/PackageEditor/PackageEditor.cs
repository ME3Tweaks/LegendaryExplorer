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
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public partial class PackageEditor : Form
    {
        public IMEPackage pcc;
        public int CurrentView;
        public const int NAMES_VIEW = 0;
        public const int IMPORTS_VIEW = 1;
        public const int Exports_VIEW = 2;
        public const int TREE_VIEW = 3;
        public PropGrid pg;

        public static readonly string PackageEditorDataFolder = Path.Combine(App.AppDataFolder, @"PackageEditor\");

        private string currentFile;

        private List<int> ClassNames;


        public PackageEditor()
        {
            InitializeComponent();
            LoadRecentList();
            RefreshRecent();
            tabControl1.TabPages.Remove(scriptTab);
            
            SetView(TREE_VIEW) ;
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
            d.Filter = App.FileFilter;
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadFile(d.FileName);
                AddRecent(d.FileName);
                SaveRecentList();
            }
        }

        public void LoadFile(string s)
        {
            try
            {
                currentFile = s;
                pcc = MEPackageHandler.OpenMEPackage(s);
                appendSaveMenuItem.Enabled = true;
                appendSaveMenuItem.ToolTipText = "Save by appending changes to the end of the file";
                interpreterControl.Pcc = pcc;
                treeView1.Tag = pcc;
                RefreshView();
                InitStuff();
                status2.Text = "@" + Path.GetFileName(s);
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
            IReadOnlyList<IImportEntry> imports = pcc.Imports;
            for (int i = imports.Count - 1; i >= 0; i--)
            {
                Classes.Add(-(i + 1) + " : " + imports[i].ObjectName);
            }
            Classes.Add("0 : Class");
            int count = 1;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            foreach (IExportEntry exp in Exports)
            {
                Classes.Add((count++) + " : " + exp.ObjectName);
            }
            count = 0;
            
            int off = imports.Count;
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
                NameIdx = pcc.getExport(n).idxObjectName;
                ClassIdx = pcc.getExport(n).idxClass;
                LinkIdx = pcc.getExport(n).idxLink;
                IndexIdx = pcc.getExport(n).indexValue;
                ArchetypeIdx = pcc.getExport(n).idxArchtype;

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
                NameIdx = imports[n].idxObjectName;
                ClassIdx = imports[n].idxClassName;
                LinkIdx = imports[n].idxLink;
                ArchetypeIdx = imports[n].idxPackageFile;

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
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            for (int i = 0; i < Exports.Count; i++)
            {
                ClassNames.Add(Exports[i].idxClass);
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
                case Exports_VIEW:
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
            if (pcc == null)
            {
                return;
            }
            listBox1.BeginUpdate();
            treeView1.BeginUpdate();
            listBox1.Items.Clear();
            IReadOnlyList<IImportEntry> imports = pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            if (CurrentView == NAMES_VIEW)
            {
                for (int i = 0; i < pcc.Names.Count; i++)
                {
                    listBox1.Items.Add(i.ToString() + " : " + pcc.getNameEntry(i));
                }
            }
            if (CurrentView == IMPORTS_VIEW)
            {
                for (int i = 0; i < imports.Count; i++)
                {
                    string importStr = i.ToString() + " (0x" + (pcc.ImportOffset + (i * ME3ImportEntry.
                        byteSize)).ToString("X4") + "): (" + imports[i].PackageFile + ") ";
                    if (imports[i].PackageFullName != "Class" && imports[i].PackageFullName != "Package")
                    {
                        importStr += imports[i].PackageFullName + ".";
                    }
                    importStr += imports[i].ObjectName;
                    listBox1.Items.Add(importStr);
                }
            }
            string s;
            if (CurrentView == Exports_VIEW)
            {
                string PackageFullName, ClassName;
                List<string> exps = new List<string>(Exports.Count);
                for (int i = 0; i < Exports.Count; i++)
                {
                    s = "";
                    PackageFullName = Exports[i].PackageFullName;
                    if (PackageFullName != "Class" && PackageFullName != "Package")
                        s += PackageFullName + ".";
                    s += Exports[i].ObjectName;
                    ClassName = Exports[i].ClassName;
                    if (ClassName == "ObjectProperty" || ClassName == "StructProperty")
                    {
                        //attempt to find type
                        byte[] data = Exports[i].Data;
                        int importindex = BitConverter.ToInt32(data, data.Length - 4);
                        if (importindex < 0)
                        {
                            //import
                            importindex *= -1;
                            if (importindex > 0) importindex--;
                            if (importindex <= imports.Count)
                            {
                                s += " (" + imports[importindex].ObjectName + ")";
                            }
                        }
                        else
                        {
                            //export
                            if (importindex > 0) importindex--;
                            if (importindex <= Exports.Count)
                            {
                                s += " [" + Exports[importindex].ObjectName + "]";
                            }
                        }
                    }
                    exps.Add(i.ToString() + " : " + s);
                }
                listBox1.Items.AddRange(exps.ToArray());
            }
            if (CurrentView == TREE_VIEW)
            {
                listBox1.Visible = false;
                treeView1.Visible = true;
                treeView1.Nodes.Clear();
                int importsOffset = Exports.Count;
                int link;
                List<TreeNode> nodeList = new List<TreeNode>(Exports.Count + imports.Count + 1);
                TreeNode node = new TreeNode(pcc.fileName);
                node.Tag = true;
                nodeList.Add(node);
                for (int i = 0; i < Exports.Count; i++)
                {
                    node = new TreeNode($"(Exp){i} : {Exports[i].ObjectName}({Exports[i].ClassName})");
                    node.Name = i.ToString();
                    nodeList.Add(node);
                }
                for (int i = 0; i < imports.Count; i++)
                {
                    node = new TreeNode($"(Imp){i} : {imports[i].ObjectName}({imports[i].ClassName})");
                    node.Name = (-i - 1).ToString();
                    nodeList.Add(node);
                }
                int curIndex;
                for (int i = 1; i <= Exports.Count; i++)
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
                for (int i = 1; i <= imports.Count; i++)
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
            SetView(Exports_VIEW);
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
            if (CurrentView == IMPORTS_VIEW || CurrentView == Exports_VIEW || CurrentView == TREE_VIEW)
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
                    if (pcc.getExport(n).ClassName == "Function" && pcc.game != MEGame.ME2)
                    {
                        if (!tabControl1.TabPages.ContainsKey(nameof(scriptTab)))
                        {
                            tabControl1.TabPages.Add(scriptTab);
                        }
                        if (pcc.game == MEGame.ME3)
                        {
                            Function func = new Function(pcc.getExport(n).Data, pcc as ME3Package);
                            rtb1.Text = func.ToRawText();
                        }
                        else
                        {
                            ME1Explorer.Unreal.Classes.Function func = new ME1Explorer.Unreal.Classes.Function(pcc.getExport(n).Data, pcc as ME1Package);
                            rtb1.Text = func.ToRawText();
                        }
                    }
                    else if (tabControl1.TabPages.ContainsKey(nameof(scriptTab)))
                    {
                        tabControl1.TabPages.Remove(scriptTab);
                    }
                    hb2.ByteProvider = new DynamicByteProvider(pcc.getExport(n).header);
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
                    hb2.ByteProvider = new DynamicByteProvider(pcc.getImport(n).header);
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
                textBox1.Text = pcc.getExport(n).ObjectName;
                textBox2.Text = pcc.getExport(n).ClassName;
                superclassTextBox.Text = pcc.getExport(n).ClassParent;
                textBox3.Text = pcc.getExport(n).PackageFullName;
                textBox4.Text = pcc.getExport(n).header.Length + " bytes";
                textBox5.Text = pcc.getExport(n).indexValue.ToString();
                textBox6.Text = pcc.getExport(n).ArchtypeName;
                if (pcc.getExport(n).idxArchtype != 0)
                    textBox6.Text += " (" + ((pcc.getExport(n).idxArchtype < 0) ? "imported" : "local") + " class) " + pcc.getExport(n).idxArchtype;
                textBox10.Text = "0x" + pcc.getExport(n).ObjectFlags.ToString("X16");
                textBox7.Text = pcc.getExport(n).DataSize + " bytes";
                textBox8.Text = "0x" + pcc.getExport(n).DataOffset.ToString("X8");
                textBox9.Text = pcc.getExport(n).DataOffset.ToString();
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
                textBox1.Text = pcc.getImport(n).ObjectName;
                textBox2.Text = pcc.getImport(n).ClassName;
                textBox3.Text = pcc.getImport(n).PackageFullName;
                textBox4.Text = pcc.getImport(n).header.Length + " bytes";
            }
        }

        public void UpdateStatusEx(int n)        
        {
            toolStripStatusLabel1.Text = $"Class:{pcc.getExport(n).ClassName} Flags: 0x{pcc.getExport(n).ObjectFlags.ToString("X16")}";
            toolStripStatusLabel1.ToolTipText = "";
            foreach (string row in UnrealFlags.flagdesc)
            {
                string[] t = row.Split(',');
                ulong l = ulong.Parse(t[1].Trim(), System.Globalization.NumberStyles.HexNumber);
                if ((l & pcc.getExport(n).ObjectFlags) != 0)
                {
                    toolStripStatusLabel1.Text += "[" + t[0].Trim() + "] ";
                    toolStripStatusLabel1.ToolTipText += "[" + t[0].Trim() + "] : " + t[2].Trim() + "\n";
                }
            }
        }

        public void UpdateStatusIm(int n)
        {
            toolStripStatusLabel1.Text = $"Class:{pcc.getImport(n).ClassName} Link: {pcc.getImport(n).idxLink} ";
            toolStripStatusLabel1.ToolTipText = "";
        }

        public void PreviewProps(int n)
        {
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.getExport(n));
            pg = new PropGrid();
            propGrid.SelectedObject = pg;
            pg.Add(new CustomProperty("Name", "_Meta", pcc.getExport(n).ObjectName, typeof(string), true, true));
            pg.Add(new CustomProperty("Class", "_Meta", pcc.getExport(n).ClassName, typeof(string), true, true));
            pg.Add(new CustomProperty("Data Offset", "_Meta", pcc.getExport(n).DataOffset, typeof(int), true, true));
            pg.Add(new CustomProperty("Data Size", "_Meta", pcc.getExport(n).DataSize, typeof(int), true, true));
            for (int l = 0; l < p.Count; l++)
                pg.Add(PropertyReader.PropertyToGrid(p[l], pcc));            
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
            else if (CurrentView == Exports_VIEW && listBox1.SelectedItem != null)
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
            if (name == "nameindex" || name == "index" ||  parentVal == typeof(ColorProp) || parentVal == typeof(VectorProp) || parentVal == typeof(Unreal.RotatorProp) || parentVal == typeof(Unreal.LinearColorProp))
            {
                name = parent.Label;
            }
            IExportEntry ent = pcc.getExport(n);
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, ent);
            int m = -1;
            for (int i = 0; i < p.Count; i++)
                if (pcc.getNameEntry(p[i].Name) == name)
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
                case PropertyReader.Type.StringRefProperty:                
                    int newv = Convert.ToInt32(e.ChangedItem.Value);
                    int oldv = Convert.ToInt32(e.OldValue);
                    buff2 = BitConverter.GetBytes(newv);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case PropertyReader.Type.StrProperty:
                    string s = Convert.ToString(e.ChangedItem.Value);
                    int stringMultiplier = 1;
                    int oldLength = BitConverter.ToInt32(ent.Data, p[m].offsetval);
                    if (oldLength < 0)
                    {
                        stringMultiplier = 2;
                        oldLength *= -2;
                    }
                    int oldSize = 4 + oldLength;
                    List<byte> stringBuff = new List<byte>(s.Length * stringMultiplier);
                    if (stringMultiplier == 2)
                    {
                        for (int j = 0; j < s.Length; j++)
                        {
                            stringBuff.AddRange(BitConverter.GetBytes(s[j]));
                        }
                        stringBuff.Add(0);
                    }
                    else
                    {
                        for (int j = 0; j < s.Length; j++)
                        {
                            stringBuff.Add(BitConverter.GetBytes(s[j])[0]);
                        }
                    }
                    stringBuff.Add(0);
                    buff2 = BitConverter.GetBytes((s.Count() + 1) * stringMultiplier + 4);
                    for (int j = 0; j < 4; j++)
                        ent.Data[p[m].offsetval - 8 + j] = buff2[j];
                    buff2 = BitConverter.GetBytes((s.Count() + 1) * stringMultiplier == 1 ? 1 : -1);
                    for (int j = 0; j < 4; j++)
                        ent.Data[p[m].offsetval + j] = buff2[j];
                    buff2 = new byte[ent.Data.Length - oldLength + stringBuff.Count];
                    int startLength = p[m].offsetval + 4;
                    int startLength2 = startLength + oldLength;
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
                case PropertyReader.Type.ByteProperty:
                case PropertyReader.Type.NameProperty:
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
                case PropertyReader.Type.ObjectProperty:
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
            propGrid.ExpandAllGridItems();
            Preview();
        }

        private void appendSaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            string extension = Path.GetExtension(pcc.fileName);
            d.Filter = $"*{extension}|*{extension}";
            if (d.ShowDialog() == DialogResult.OK)
            {
                pcc.appendSave(d.FileName, true);
                MessageBox.Show("Done");
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            pcc.save();
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
            pcc.getExport(n).Data = m.ToArray();

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
                    if (pcc.getNameEntry(i).ToLower().Contains(searchBox.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == IMPORTS_VIEW)
            {
                IReadOnlyList<IImportEntry> imports = pcc.Imports;
                for (int i = start; i < imports.Count; i++)
                    if (imports[i].ObjectName.ToLower().Contains(searchBox.Text.ToLower()))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == Exports_VIEW)
            {
                IReadOnlyList<IExportEntry> Exports = pcc.Exports;
                for (int i = start; i < Exports.Count; i++)
                    if (Exports[i].ObjectName.ToLower().Contains(searchBox.Text.ToLower()))
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
            if (CurrentView != Exports_VIEW)
                return;
            if (combo1.SelectedIndex == -1)
                return;
            string cls = combo1.SelectedItem as string;
            int start;
            if (n == -1)
                start = 0;
            else
                start = n + 1;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            for (int i = start; i < Exports.Count; i++)
                if (Exports[i].ClassName == cls)
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

        private void LoadRecentList()
        {
            RFiles = new List<string>();
            RFiles.Clear();
            string path = PackageEditorDataFolder + "recentFiles.log";
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
            if (!Directory.Exists(PackageEditorDataFolder))
            {
                Directory.CreateDirectory(PackageEditorDataFolder);
            }
            string path = PackageEditorDataFolder + "recentFiles.log";
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
                int idx = pcc.FindNameOrAdd(result);
                if (CurrentView == NAMES_VIEW)
                {
                    int scrollTo = listBox1.TopIndex + 1;
                    int selected = listBox1.SelectedIndex;
                    RefreshView();
                    listBox1.SelectedIndex = selected;
                    listBox1.TopIndex = scrollTo;
                }
                byte[] buff = BitConverter.GetBytes(idx);
                string s = "";
                for (int i = 0; i < 4; i++)
                {
                    s += buff[i].ToString("X2");
                }
                MessageBox.Show("\"" + result + "\" at index " + (idx) + " (" + s + ")");
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
            List<PropertyReader.Property> prop = PropertyReader.getPropList(pcc, pcc.getExport(n));
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            d.FileName = pcc.getExport(n).ObjectName + ".bin";
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                byte[] buff = pcc.getExport(n).Data;
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

        public void goToNumber(int n)
        {
            if (CurrentView == TREE_VIEW)
            {
                if(n >= -pcc.ImportCount && n < pcc.ExportCount)
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
                fs.Write(pcc.getExport(n).Data, 0, pcc.getExport(n).Data.Length);
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
                pcc.getExport(n).Data = buff;
                MessageBox.Show("Done.");
            }
        }

        private void reconstructionSave_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            if (!pcc.canClone())
            {
                return;
            }
            SaveFileDialog d = new SaveFileDialog();
            string extension = Path.GetExtension(pcc.fileName);
            d.Filter = $"*{extension}|*{extension}";
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
            if (pcc.getExport(n).ClassName.Contains("BlockingVolume") || pcc.getExport(n).ClassName.Contains("SFXDoor"))
            {
                List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.getExport(n));
                foreach (PropertyReader.Property p in props)
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
                        byte[] buff = pcc.getExport(n).Data;
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
                        pcc.getExport(n).Data = buff;
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
                string currfile = Path.GetFileName(pcc.fileName);
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

        private void createBinaryReplaceJobFromObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            KFreonLib.Scripting.ModMaker.ModJob mj = KFreonLib.Scripting.ModMaker.GenerateMeshModJob(null, n, pcc.fileName, CopyArray(pcc.getExport(n).Data));
            KFreonLib.Scripting.ModMaker.JobList.Add(mj);
            MessageBox.Show("Done");
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
            int off = pcc.ImportCount;

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
                pcc.getExport(n).idxObjectName = NameIdx;
                pcc.getExport(n).idxClass = ClassIdx;
                pcc.getExport(n).idxLink = LinkIdx;
                pcc.getExport(n).indexValue = IndexIdx;
                pcc.getExport(n).idxArchtype = ArchetypeIdx;
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
                pcc.getImport(n).idxObjectName = NameIdx;
                pcc.getImport(n).idxClassName = ClassIdx;
                pcc.getImport(n).idxLink = LinkIdx;
                pcc.getImport(n).idxPackageFile = ArchetypeIdx;
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
            if (pcc.getExport(n).ClassName == "FaceFXAsset" || pcc.getExport(n).ClassName == "FaceFXAnimSet")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.fxa|*.fxa";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    byte[] buff = pcc.getExport(n).Data;
                    BitConverter.IsLittleEndian = true;
                    List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.getExport(n));
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
            if (pcc.getExport(n).ClassName == "FaceFXAsset" || pcc.getExport(n).ClassName == "FaceFXAnimSet")
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.fxa|*.fxa";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    byte[] buff = pcc.getExport(n).Data;
                    BitConverter.IsLittleEndian = true;
                    List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.getExport(n));
                    int start = props[props.Count - 1].offend;
                    MemoryStream m = new MemoryStream();
                    m.Write(buff, 0, start);
                    byte[] import = File.ReadAllBytes(d.FileName);
                    m.Write(BitConverter.GetBytes((int)import.Length), 0, 4);
                    m.Write(import, 0, import.Length);
                    pcc.getExport(n).Data = m.ToArray();
                    pcc.save();
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

        private void PackageEditor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void PackageEditor_DragDrop(object sender, DragEventArgs e)
        {
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList();
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
                Clipboard.SetText(pcc.getNameEntry(listBox1.SelectedIndex));
            }
        }

        private void hexConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + @"\HexConverter.exe"))
            {
                Process.Start(loc + @"\HexConverter.exe");
            }
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
            pcc.getExport(n).header = m.ToArray();

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
                appendSaveMenuItem.Enabled = false;
                appendSaveMenuItem.ToolTipText = "This method cannot be used if cloning has occured.";

                if (n >= 0)
                {
                    IExportEntry ent = pcc.getExport(n).Clone();
                    pcc.addExport(ent);
                    RefreshView();
                    goToNumber(pcc.ExportCount - 1); 
                }
                else
                {
                    IImportEntry ent = pcc.getImport(-n - 1).Clone();
                    pcc.addImport(ent);
                    RefreshView();
                    goToNumber(CurrentView == TREE_VIEW ? -pcc.ImportCount : pcc.ImportCount - 1);
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
                appendSaveMenuItem.Enabled = false;
                appendSaveMenuItem.ToolTipText = "This method cannot be used if cloning or importing has occured.";

                TreeNode rootNode = treeView1.SelectedNode;
                if (n >= 0)
                {
                    nextIndex = pcc.ExportCount;
                    IExportEntry exp = pcc.getExport(n).Clone();
                    pcc.addExport(exp);

                    n = nextIndex + 1;
                }
                else
                {
                    nextIndex = -pcc.ImportCount - 1;
                    IImportEntry imp = pcc.getImport(-n - 1).Clone();
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
                        nextIndex = pcc.ExportCount + 1;
                        IExportEntry exp = pcc.getExport(index).Clone();
                        exp.idxLink = n;
                        pcc.addExport(exp);
                    }
                    else
                    {
                        nextIndex = -pcc.ImportCount - 1;
                        IImportEntry imp = pcc.getImport(-index - 1).Clone();
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
                    IMEPackage importpcc = sourceNode.TreeView.Tag as IMEPackage;
                    if (!pcc.canClone() || importpcc == null || importpcc.game != pcc.game)
                    {
                        return;
                    }
                    appendSaveMenuItem.Enabled = false;
                    appendSaveMenuItem.ToolTipText = "This method cannot be used if importing has occured.";
                    
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
                        nextIndex = pcc.ExportCount;
                    }
                    else
                    {
                        importImport(importpcc, -n - 1, link);
                        nextIndex = -pcc.ImportCount;
                    }
                    if (sourceNode.Nodes.Count > 0)
                    {
                        importTree(sourceNode, importpcc, nextIndex);
                    }

                    RefreshView();
                    goToNumber(n >= 0 ? pcc.ExportCount - 1 : -pcc.ImportCount);
                }
            }
        }

        private bool importTree(TreeNode sourceNode, IMEPackage importpcc, int n)
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
                    nextIndex = pcc.ExportCount;
                }
                else
                {
                    importImport(importpcc, -index - 1, n);
                    nextIndex = -pcc.ImportCount;
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

        private void importImport(IMEPackage importpcc, int n, int link)
        {
            IImportEntry imp = importpcc.getImport(n);
            IImportEntry nimp = null;
            switch (pcc.game)
            {
                case MEGame.ME1:
                    nimp = new ME1ImportEntry(pcc as ME1Package, imp.header);
                    break;
                case MEGame.ME2:
                    nimp = new ME2ImportEntry(pcc as ME2Package, imp.header);
                    break;
                case MEGame.ME3:
                    nimp = new ME3ImportEntry(pcc as ME3Package, imp.header);
                    break;
            }
            nimp.idxLink = link;
            nimp.idxClassName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxClassName));
            nimp.idxObjectName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxObjectName));
            nimp.idxPackageFile = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxPackageFile));
            pcc.addImport(nimp);
        }

        private bool importExport(IMEPackage importpcc, int n, int link)
        {
            IExportEntry ex = importpcc.getExport(n);
            IExportEntry nex = null;
            switch (pcc.game)
            {
                case MEGame.ME1:
                    nex = new ME1ExportEntry(pcc as ME1Package);
                    break;
                case MEGame.ME2:
                    nex = new ME2ExportEntry(pcc as ME2Package);
                    break;
                case MEGame.ME3:
                    nex = new ME3ExportEntry(pcc as ME3Package);
                    break;
            }
            byte[] idata = ex.Data;
            List<PropertyReader.Property> Props = PropertyReader.getPropList(importpcc, ex);
            int start = PropertyReader.detectStart(importpcc, idata, importpcc.getExport(n).ObjectFlags);
            int end = start;
            if (Props.Count != 0)
            {
                end = Props[Props.Count - 1].offend;
            }
            MemoryStream res = new MemoryStream();
            if ((importpcc.getExport(n).ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
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
                pcc.setNames(names);
                MessageBox.Show("Error occured while trying to import " + ex.ObjectName + " : " + exception.Message);
                return false;
            }
            if (importpcc.game == MEGame.ME3 && importpcc.getObjectName(ex.idxClass) == "SkeletalMesh")
            {
                SkeletalMesh skl = new SkeletalMesh(importpcc as ME3Package, n);
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
            nex.idxObjectName = pcc.FindNameOrAdd(importpcc.getNameEntry(ex.idxObjectName));
            nex.idxLink = link;
            nex.idxArchtype = nex.idxClass = nex.idxClassParent = 0;
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

        private void editInCurveEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = 0;
            if (GetSelected(out n) && n >= 0 && pcc.game == MEGame.ME3)
            {
                CurveEd.CurveEditor c = new CurveEd.CurveEditor(pcc as ME3Package, n);
                c.Show();
            }
        }
    }
}
