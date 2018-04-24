using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Be.Windows.Forms;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using UsefulThings;
using static ME3Explorer.Unreal.PropertyReader;
using System.Text;
using ME3Explorer.SharedUI;

namespace ME3Explorer
{
    public partial class PackageEditor : WinFormsBase
    {
        enum View
        {
            Names,
            Imports,
            Exports,
            Tree
        }

        View CurrentView;
        public PropGrid pg;

        public static readonly string PackageEditorDataFolder = Path.Combine(App.AppDataFolder, @"PackageEditor\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        private string currentFile;
        private List<int> ClassNames;

        public PackageEditor()
        {
            InitializeComponent();
            LoadRecentList();
            RefreshRecent(false);
            //tabs are removed on showing the window so they are part of document tree for dpi scaling.

            SetView(View.Tree);
            interpreterControl.saveHexButton.Click += saveHexChangesButton_Click;
            binaryInterpreterControl.saveHexButton.Click += binarySaveHexChangesButton_Click;
        }

        private SizeF scale;
        private void PackageEditor_Shown(object sender, EventArgs e)
        {
            //This can only be done on shown for some reason. It will not work in load
            scale = new SizeF((float)MainWindow.dpiScaleX, (float)MainWindow.dpiScaleY);
            Fix(this);

            interpreterControl.HEXBOX_MAX_WIDTH = (int)Math.Round(interpreterControl.HEXBOX_MAX_WIDTH * MainWindow.dpiScaleX);
            binaryInterpreterControl.HEXBOX_MAX_WIDTH = (int)Math.Round(binaryInterpreterControl.HEXBOX_MAX_WIDTH * MainWindow.dpiScaleX);

            packageEditorTabPane.TabPages.Remove(scriptTab);
            packageEditorTabPane.TabPages.Remove(binaryEditorTab);
            packageEditorTabPane.TabPages.Remove(bio2daEditorTab);
        }

        // Save the current scale value
        // ScaleControl() is called during the Form's constructor
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            scale = new SizeF(scale.Width * factor.Width, scale.Height * factor.Height);
            base.ScaleControl(factor, specified);
        }

        // Recursively search for SplitContainer controls
        private void Fix(Control c)
        {
            foreach (Control child in c.Controls)
            {
                if (child is SplitContainer)
                {
                    SplitContainer sp = (SplitContainer)child;
                    Fix(sp);
                    Fix(sp.Panel1);
                    Fix(sp.Panel2);
                }
                else
                {
                    Fix(child);
                }
            }
        }

        private void Fix(SplitContainer sp)
        {
            // Scale factor depends on orientation
            float sc = (sp.Orientation == Orientation.Vertical) ? scale.Width : scale.Height;
            if (sp.FixedPanel == FixedPanel.Panel1)
            {
                sp.SplitterDistance = (int)Math.Round((float)sp.SplitterDistance * sc);
                sp.Panel1MinSize = (int)Math.Round((float)sp.Panel1MinSize * sc);
            }
            else if (sp.FixedPanel == FixedPanel.Panel2)
            {
                int cs = (sp.Orientation == Orientation.Vertical) ? sp.Panel2.ClientSize.Width : sp.Panel2.ClientSize.Height;
                int newcs = (int)((float)cs * sc);
                sp.SplitterDistance -= (newcs - cs);
            }
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
                try
                {
                    LoadFile(d.FileName);
                    AddRecent(d.FileName, false);
                    SaveRecentList();
                    RefreshRecent(true, RFiles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }

        public void LoadFile(string s)
        {
            try
            {
                currentFile = s;
                LoadMEPackage(s);
                interpreterControl.Pcc = pcc;
                binaryInterpreterControl.Pcc = pcc;
                bio2DAEditor1.Pcc = pcc;
                treeView1.Tag = pcc;
                RefreshView();
                InitStuff();
                status2.Text = "@" + Path.GetFileName(s);
            }
            catch (Exception e)
            {
                Debugger.Break();
                MessageBox.Show("Error loading file:\n" + e.Message);
            }
        }

        bool pendingMetaDataUpdate;
        public void RefreshMetaData()
        {
            int NameIdx, ClassIdx, LinkIdx, IndexIdx, ArchetypeIdx;
            if (packageEditorTabPane.SelectedTab != metaDataPage)
            {
                return;
            }
            if (!this.IsForegroundWindow())
            {
                pendingMetaDataUpdate = true;
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
            IReadOnlyList<ImportEntry> imports = pcc.Imports;
            for (int i = imports.Count - 1; i >= 0; i--)
            {
                string text = -(i + 1) + " : " + imports[i].ObjectName;
                if (showFullPathsCheckbox.Checked)
                {
                    text += " (" + imports[i].GetFullPath + ")";
                }
                Classes.Add(text);
            }
            Classes.Add("0 : Class");
            int count = 1;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            foreach (IExportEntry exp in Exports)
            {

                string text = (count++) + " : " + exp.ObjectName;
                if (showFullPathsCheckbox.Checked)
                {
                    text += " (" + exp.GetFullPath + ")";
                }
                Classes.Add(text);
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
                ArchetypeIdx = imports[n].idxPackageName;

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

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            recentToolStripMenuItem.Enabled = true;
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
            List<string> names = ClassNames.Distinct().Select(x => pcc.getObjectName(x)).ToList();
            names.Sort();
            combo1.BeginUpdate();
            combo1.Items.Clear();
            combo1.Items.AddRange(names.ToArray());
            combo1.EndUpdate();
        }

        void SetView(View n)
        {
            CurrentView = n;
            switch (n)
            {
                case View.Names:
                    Button1.Checked = true;
                    Button2.Checked = false;
                    Button3.Checked = false;
                    Button5.Checked = false;
                    break;
                case View.Imports:
                    Button1.Checked = false;
                    Button2.Checked = true;
                    Button3.Checked = false;
                    Button5.Checked = false;
                    break;
                case View.Tree:
                    Button1.Checked = false;
                    Button2.Checked = false;
                    Button3.Checked = false;
                    Button5.Checked = true;
                    break;
                case View.Exports:
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
            IReadOnlyList<ImportEntry> imports = pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            if (CurrentView == View.Names)
            {
                for (int i = 0; i < pcc.Names.Count; i++)
                {
                    listBox1.Items.Add(i + " : " + pcc.getNameEntry(i));
                }
            }
            if (CurrentView == View.Imports)
            {
                for (int i = 0; i < imports.Count; i++)
                {
                    string importStr = i + " (0x" + (pcc.ImportOffset + (i * ImportEntry.byteSize)).ToString("X4") + "): (" + imports[i].PackageFile + ") ";
                    if (imports[i].PackageFullName != "Class" && imports[i].PackageFullName != "Package")
                    {
                        importStr += imports[i].PackageFullName + ".";
                    }
                    importStr += imports[i].ObjectName;
                    listBox1.Items.Add(importStr);
                }
            }
            if (CurrentView == View.Exports)
            {
                List<string> exps = new List<string>(Exports.Count);
                for (int i = 0; i < Exports.Count; i++)
                {
                    string s = $"{i}:";
                    IExportEntry exp = pcc.getExport(i);
                    string PackageFullName = exp.PackageFullName;
                    if (PackageFullName != "Class" && PackageFullName != "Package")
                        s += PackageFullName + ".";
                    s += exp.ObjectName;
                    string ClassName = exp.ClassName;
                    if (ClassName == "ObjectProperty" || ClassName == "StructProperty")
                    {
                        //attempt to find type
                        byte[] data = exp.Data;
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
                    exps.Add(s);
                }
                listBox1.Items.AddRange(exps.ToArray());
            }
            if (CurrentView == View.Tree)
            {
                listBox1.Visible = false;
                treeView1.Visible = true;
                treeView1.Nodes.Clear();
                int importsOffset = Exports.Count;
                int link;
                List<TreeNode> nodeList = new List<TreeNode>(Exports.Count + imports.Count + 1);
                TreeNode node = new TreeNode(pcc.FileName);
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
                        //Debug.WriteLine(curIndex);
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
            SetView(View.Exports);
            RefreshView();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            SetView(View.Names);
            RefreshView();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            SetView(View.Imports);
            RefreshView();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // keep disabled unless we're on the hex tab:
            int n;
            if (packageEditorTabPane.SelectedTab == interpreterTab && GetSelected(out n) && n >= 0)
            {
                if (interpreterControl.treeView1.Nodes.Count > 0)
                {
                    interpreterControl.treeView1.Nodes[0].Expand();
                }
            }

            if (packageEditorTabPane.SelectedTab == metaDataPage)
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
            if (CurrentView == View.Imports || CurrentView == View.Exports || CurrentView == View.Tree)
            {
                tabControl1_SelectedIndexChanged(null, null);
                PreviewInfo(n);
                RefreshMetaData();
                //export
                if (n >= 0)
                {
                    PreviewProps(n);
                    if (!packageEditorTabPane.TabPages.ContainsKey(nameof(propertiesTab)))
                    {
                        packageEditorTabPane.TabPages.Insert(0, propertiesTab);
                    }
                    if (!packageEditorTabPane.TabPages.ContainsKey(nameof(interpreterTab)))
                    {
                        packageEditorTabPane.TabPages.Insert(1, interpreterTab);
                    }

                    IExportEntry exportEntry = pcc.getExport(n);
                    if (exportEntry.ClassName == "Function")
                    {
                        if (!packageEditorTabPane.TabPages.ContainsKey(nameof(scriptTab)))
                        {
                            packageEditorTabPane.TabPages.Add(scriptTab);
                        }
                        if (pcc.Game == MEGame.ME3)
                        {
                            Function func = new Function(exportEntry.Data, pcc);
                            rtb1.Text = func.ToRawText();
                        }
                        else if (pcc.Game == MEGame.ME1)
                        {
                            ME1Explorer.Unreal.Classes.Function func = new ME1Explorer.Unreal.Classes.Function(exportEntry.Data, pcc as ME1Package);
                            try
                            {
                                rtb1.Text = func.ToRawText();
                            }
                            catch (Exception e)
                            {
                                rtb1.Text = "Error parsing function: " + e.Message;
                            }
                        }
                        else
                        {
                            rtb1.Text = "Parsing UnrealScript Functions for this game is not supported.";
                        }
                    }
                    else if (packageEditorTabPane.TabPages.ContainsKey(nameof(scriptTab)))
                    {
                        packageEditorTabPane.TabPages.Remove(scriptTab);
                    }

                    if (BinaryInterpreter.ParsableBinaryClasses.Contains(exportEntry.ClassName))
                    {
                        if (!packageEditorTabPane.TabPages.ContainsKey(nameof(binaryEditorTab)))
                        {
                            packageEditorTabPane.TabPages.Add(binaryEditorTab);
                        }
                    }
                    else
                    {
                        removeBinaryTabPane();
                    }

                    if (Bio2DAEditor.ParsableBinaryClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith("Default__"))
                    {
                        if (!packageEditorTabPane.TabPages.ContainsKey(nameof(bio2daEditorTab)))
                        {
                            packageEditorTabPane.TabPages.Add(bio2daEditorTab);
                        }
                    }
                    else
                    {
                        if (packageEditorTabPane.TabPages.ContainsKey(nameof(bio2daEditorTab)))
                        {
                            packageEditorTabPane.TabPages.Remove(bio2daEditorTab);
                        }
                    }

                    headerRawHexBox.ByteProvider = new DynamicByteProvider(exportEntry.header);
                    if (!isRefresh)
                    {
                        interpreterControl.export = exportEntry;
                        interpreterControl.InitInterpreter();

                        if (BinaryInterpreter.ParsableBinaryClasses.Contains(exportEntry.ClassName))
                        {
                            if (exportEntry.ClassName == "Class" && exportEntry.ObjectName.StartsWith("Default__"))
                            {
                                //do nothing, this class is not actually a class.
                                removeBinaryTabPane();
                            }
                            else
                            {
                                binaryInterpreterControl.export = exportEntry;
                                binaryInterpreterControl.InitInterpreter();
                            }
                        }
                        if (Bio2DAEditor.ParsableBinaryClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith("Default__"))
                        {
                            bio2DAEditor1.export = exportEntry;
                            bio2DAEditor1.InitInterpreter();
                        }
                    }
                    UpdateStatusEx(n);
                }
                //import
                else
                {
                    n = -n - 1;
                    headerRawHexBox.ByteProvider = new DynamicByteProvider(pcc.getImport(n).header);
                    UpdateStatusIm(n);
                    if (packageEditorTabPane.TabPages.ContainsKey(nameof(interpreterTab)))
                    {
                        packageEditorTabPane.TabPages.Remove(interpreterTab);
                    }
                    if (packageEditorTabPane.TabPages.ContainsKey(nameof(propertiesTab)))
                    {
                        packageEditorTabPane.TabPages.Remove(propertiesTab);
                    }
                    if (packageEditorTabPane.TabPages.ContainsKey(nameof(scriptTab)))
                    {
                        packageEditorTabPane.TabPages.Remove(scriptTab);
                    }
                    if (packageEditorTabPane.TabPages.ContainsKey(nameof(binaryEditorTab)))
                    {
                        packageEditorTabPane.TabPages.Remove(binaryEditorTab);
                    }
                }
            }
        }

        private void removeBinaryTabPane()
        {
            if (packageEditorTabPane.TabPages.ContainsKey(nameof(binaryEditorTab)))
            {
                packageEditorTabPane.TabPages.Remove(binaryEditorTab);
            }
        }

        public void PreviewInfo(int n)
        {
            if (n >= 0)
            {
                try
                {
                    infoHeaderBox.Text = "Export Header";
                    superclassTextBox.Visible = superclassLabel.Visible = true;
                    archetypeBox.Visible = label6.Visible = true;
                    indexBox.Visible = label5.Visible = true;
                    flagsBox.Visible = label11.Visible = false;
                    infoExportDataBox.Visible = true;
                    IExportEntry exportEntry = pcc.getExport(n);
                    objectNameBox.Text = exportEntry.ObjectName;
                    //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                    //
                    if (exportEntry.idxClass != 0)
                    {
                        IEntry _class = pcc.getEntry(exportEntry.idxClass);
                        classNameBox.Text = _class.ClassName;

                    }
                    else
                    {
                        classNameBox.Text = "Class";
                    }
                    classNameBox.Text = exportEntry.ClassName;
                    superclassTextBox.Text = exportEntry.ClassParent;
                    packageNameBox.Text = exportEntry.PackageFullName;
                    headerSizeBox.Text = exportEntry.header.Length + " bytes";
                    indexBox.Text = exportEntry.indexValue.ToString();
                    archetypeBox.Text = exportEntry.ArchtypeName;

                    if (exportEntry.idxArchtype != 0)
                    {
                        IEntry archetype = pcc.getEntry(exportEntry.idxArchtype);
                        archetypeBox.Text = archetype.PackageFullName + "." + archetype.ObjectName;
                        archetypeBox.Text += " (" + (exportEntry.idxArchtype < 0 ? "imported" : "local") + " class) " + exportEntry.idxArchtype;
                    }
                    flagsBox.Text = "0x" + exportEntry.ObjectFlags.ToString("X16");
                    textBox7.Text = exportEntry.DataSize + " bytes";
                    textBox8.Text = "0x" + exportEntry.DataOffset.ToString("X8");
                    textBox9.Text = exportEntry.DataOffset.ToString();
                }
                catch (Exception e)
                {
                    MessageBox.Show("An error occured while attempting to read the header for this export. This indicates there is likely something wrong with the header or its parent header.");
                }
            }
            else
            {
                n = -n - 1;
                infoHeaderBox.Text = "Import Header";
                superclassTextBox.Visible = superclassLabel.Visible = false;
                archetypeBox.Visible = label6.Visible = false;
                indexBox.Visible = label5.Visible = false;
                flagsBox.Visible = label11.Visible = false;
                infoExportDataBox.Visible = false;
                ImportEntry importEntry = pcc.getImport(n);
                objectNameBox.Text = importEntry.ObjectName;
                classNameBox.Text = importEntry.ClassName;
                packageNameBox.Text = importEntry.PackageFullName;
                headerSizeBox.Text = ImportEntry.byteSize + " bytes";
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
            pg = new PropGrid();
            propGrid.SelectedObject = pg;
            pg.Add(new CustomProperty("Name", "_Meta", pcc.getExport(n).ObjectName, typeof(string), true, true));
            pg.Add(new CustomProperty("Class", "_Meta", pcc.getExport(n).ClassName, typeof(string), true, true));
            pg.Add(new CustomProperty("Data Offset", "_Meta", pcc.getExport(n).DataOffset, typeof(int), true, true));
            pg.Add(new CustomProperty("Data Size", "_Meta", pcc.getExport(n).DataSize, typeof(int), true, true));
            IExportEntry export = pcc.getExport(n);

            if (export.ClassName != "Class")
            {
                List<PropertyReader.Property> p = PropertyReader.getPropList(export);
                for (int l = 0; l < p.Count; l++)
                    pg.Add(PropertyReader.PropertyToGrid(p[l], pcc));
            }
            propGrid.Refresh();
            propGrid.ExpandAllGridItems();
        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            Preview();
        }

        private bool GetSelected(out int n)
        {
            if (CurrentView == View.Tree && treeView1.SelectedNode != null && treeView1.SelectedNode.Name != "")
            {
                n = Convert.ToInt32(treeView1.SelectedNode.Name);
                return true;
            }
            else if (CurrentView == View.Exports && listBox1.SelectedItem != null)
            {
                n = listBox1.SelectedIndex;
                return true;
            }
            else if (CurrentView == View.Imports && listBox1.SelectedItem != null)
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

        private void propGrid_PropertyValueChanged(object o, PropertyValueChangedEventArgs e)
        {
            int n;
            if (!GetSelected(out n) || n < 0)
            {
                return;
            }
            PropGrid.propGridPropertyValueChanged(e, n, pcc);
        }

        private void appendSaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            if (pcc.Game == MEGame.UDK)
            {
                MessageBox.Show(this, "Cannot save UDK UPK files, support for them is read only.", "Unsupported operation");
                return;
            }
            SaveFileDialog d = new SaveFileDialog();
            string extension = Path.GetExtension(pcc.FileName);
            d.Filter = $"*{extension}|*{extension}";
            if (d.ShowDialog() == DialogResult.OK)
            {
                pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            if (pcc.Game == MEGame.UDK)
            {
                MessageBox.Show(this, "Cannot save UDK UPK files, support for them is read only.", "Unsupported operation");
                return;
            }
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
        }
        private void binarySaveHexChangesButton_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || !GetSelected(out n) || n < 0)
            {
                return;
            }
            MemoryStream m = new MemoryStream();
            IByteProvider provider = binaryInterpreterControl.hb1.ByteProvider;
            for (int i = 0; i < provider.Length; i++)
                m.WriteByte(provider.ReadByte(i));
            pcc.getExport(n).Data = m.ToArray();
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


            string searchTerm = searchBox.Text.ToLower();
            if (CurrentView == View.Names)
            {
                for (int i = start; i < pcc.Names.Count; i++)
                    if (pcc.getNameEntry(i).ToLower().Contains(searchTerm))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == View.Imports)
            {
                IReadOnlyList<ImportEntry> imports = pcc.Imports;
                for (int i = start; i < imports.Count; i++)
                    if (imports[i].ObjectName.ToLower().Contains(searchTerm))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == View.Exports)
            {
                IReadOnlyList<IExportEntry> Exports = pcc.Exports;
                for (int i = start; i < Exports.Count; i++)
                    if (Exports[i].ObjectName.ToLower().Contains(searchTerm))
                    {
                        listBox1.SelectedIndex = i;
                        break;
                    }
            }
            if (CurrentView == View.Tree)
            {
                List<TreeNode> flattenedTree = treeView1.FlattenTree().ToList();
                int pos = treeView1.SelectedNode == null ? -1 : flattenedTree.IndexOf(treeView1.SelectedNode);
                pos++; //search only 1 forward
                for (int i = pos; i < flattenedTree.Count; i++)
                {
                    TreeNode node = flattenedTree[i];
                    int index = Convert.ToInt32(node.Name);
                    IEntry entry;
                    if (index < 0)
                    {
                        entry = pcc.Imports[Math.Abs(index) - 1];
                    }
                    else
                    {
                        entry = pcc.Exports[index];
                    }
                    if (entry.ObjectName.ToLower().Contains(searchTerm))
                    {
                        treeView1.SelectedNode = node;
                        break;
                    }
                }
            }
        }



        private void Find()
        {
            if (pcc == null)
                return;
            int n = listBox1.SelectedIndex;
            if (CurrentView != View.Exports)
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
        private SortedDictionary<int, int> crossPCCObjectMapping;

        private void LoadRecentList()
        {
            recentToolStripMenuItem.Enabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = PackageEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(PackageEditorDataFolder))
            {
                Directory.CreateDirectory(PackageEditorDataFolder);
            }
            string path = PackageEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        private void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of packed
                var forms = Application.OpenForms;
                foreach (Form form in forms)
                {
                    if (form is PackageEditor && this != form)
                    {
                        ((PackageEditor)form).RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            recentToolStripMenuItem.DropDownItems.Clear();
            if (RFiles.Count <= 0)
            {
                recentToolStripMenuItem.Enabled = false;
                return;
            }
            recentToolStripMenuItem.Enabled = true;


            foreach (string filepath in RFiles)
            {
                ToolStripMenuItem fr = new ToolStripMenuItem(filepath, null, RecentFile_click);
                recentToolStripMenuItem.DropDownItems.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = sender.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
            }
        }

        private void addNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name", "ME3 Explorer", "", 0, 0);
            if (result != "")
            {
                int idx = pcc.FindNameOrAdd(result);
                byte[] buff = BitConverter.GetBytes(idx);
                string s = "";
                for (int i = 0; i < 4; i++)
                {
                    s += buff[i].ToString("X2");
                }
                MessageBox.Show("\"" + result + "\" at index " + (idx) + " (" + s + ")");
            }
        }

        private void searchButton_Clicked(object sender, EventArgs e)
        {
            Search();
        }

        private void searchBar_KeyPressed(object sender, KeyPressEventArgs e)
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
            List<PropertyReader.Property> prop = PropertyReader.getPropList(pcc.getExport(n));
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
            InterpreterHost ip = new InterpreterHost(pcc.FileName, n);
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
            if (CurrentView == View.Tree)
            {
                if (n >= -pcc.ImportCount && n < pcc.ExportCount)
                {
                    TreeNode[] nodes = treeView1.Nodes.Find(n.ToString(), true);
                    if (nodes.Length > 0)
                    {
                        try
                        {
                            treeView1.SelectedNode = nodes[0];
                        }
                        catch (AccessViolationException e)
                        {
                            //can't do much here... just avoid the error.
                            //This is thrown due to 
                        }
                        //treeView1.Focus();
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
                string currfile = Path.GetFileName(pcc.FileName);
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
            KFreonLib.Scripting.ModMaker.ModJob mj = KFreonLib.Scripting.ModMaker.GenerateMeshModJob(null, n, pcc.FileName, pcc.getExport(n).Data);
            KFreonLib.Scripting.ModMaker.JobList.Add(mj);
            MessageBox.Show("Done");
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

                IExportEntry exportEntry = pcc.getExport(n);
                exportEntry.idxObjectName = NameIdx;
                exportEntry.idxClass = ClassIdx;
                exportEntry.idxLink = LinkIdx;
                exportEntry.indexValue = IndexIdx;
                exportEntry.idxArchtype = ArchetypeIdx;
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
                ImportEntry importEntry = pcc.getImport(n);
                importEntry.idxObjectName = NameIdx;
                importEntry.idxClassName = ClassIdx;
                importEntry.idxLink = LinkIdx;
                importEntry.idxPackageName = ArchetypeIdx;
                n = -n - 1;
            }
        }

        private void Button5_Click(object sender, EventArgs e)
        {
            SetView(View.Tree);
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
                AddRecent(DroppedFiles[0], false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
        }

        private void listBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                listBox1.SelectedIndex = listBox1.IndexFromPoint(e.X, e.Y);
                if (CurrentView == View.Names)
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
            if (CurrentView == View.Names && listBox1.SelectedIndex != -1)
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
            if (pcc == null || !GetSelected(out n))
            {
                return;
            }
            MemoryStream m = new MemoryStream();
            IByteProvider provider = headerRawHexBox.ByteProvider;
            int requiredheaderlength = n > 0 ? 0x44 : 0x1C; //0x44 for exports, 0x1B for imports
            if (provider.Length != requiredheaderlength)
            {
                MessageBox.Show("Invalid hex length");
                return;
            }
            for (int i = 0; i < provider.Length; i++)
                m.WriteByte(provider.ReadByte(i));
            if (n > 0)
            {
                pcc.getExport(n).setHeader(m.ToArray());
            }
            else if (n < 0)
            {
                pcc.getImport(Math.Abs(n) - 1).setHeader(m.ToArray());
            }
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
            int n = 0;
            if (GetSelected(out n))
            {
                if (n >= 0)
                {
                    IExportEntry ent = pcc.getExport(n).Clone();
                    pcc.addExport(ent);
                    RefreshView();
                    goToNumber(pcc.ExportCount - 1);
                }
                else
                {
                    ImportEntry ent = pcc.getImport(-n - 1).Clone();
                    pcc.addImport(ent);
                    RefreshView();
                    goToNumber(CurrentView == View.Tree ? -pcc.ImportCount : pcc.ImportCount - 1);
                }
            }
        }

        private void cloneTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = 0;
            if (GetSelected(out n))
            {
                int nextIndex;

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
                    ImportEntry imp = pcc.getImport(-n - 1).Clone();
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
                        ImportEntry imp = pcc.getImport(-index - 1).Clone();
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
            crossPCCObjectMapping = new SortedDictionary<int, int>();
            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
                TreeNode DestinationNode = ((TreeView)sender).GetNodeAt(pt);
                sourceNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
                if (DestinationNode.TreeView != sourceNode.TreeView)
                {
                    IMEPackage importpcc = sourceNode.TreeView.Tag as IMEPackage;
                    if (importpcc == null)
                    {
                        return;
                    }

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
                        if (!importExport(importpcc, n, link))
                        {
                            return;
                        }
                        nextIndex = pcc.ExportCount;
                    }
                    else
                    {
                        getOrAddCrossImport(importpcc.getImport(Math.Abs(n) - 1).GetFullPath, importpcc, pcc);
                        //importImport(importpcc, -n - 1, link);
                        nextIndex = -pcc.ImportCount;
                    }

                    //if this node has children
                    if (sourceNode.Nodes.Count > 0)
                    {
                        importTree(sourceNode, importpcc, nextIndex);
                    }

                    //relinkObjects(importpcc);
                    List<string> relinkResults = new List<string>();
                    relinkResults.AddRange(relinkObjects2(importpcc));
                    relinkResults.AddRange(relinkBinaryObjects(importpcc));
                    crossPCCObjectMapping = null;

                    RefreshView();
                    goToNumber(n >= 0 ? pcc.ExportCount - 1 : -pcc.ImportCount);
                    if (relinkResults.Count > 0)
                    {
                        ListWindow lw = new ListWindow(relinkResults, "Relink report", "The following items failed to relink.", 800, 600);
                        lw.ShowDialog(this);
                    }
                    else
                    {
                        MessageBox.Show("Items have been ported and relinked with no reported issues.\nNote that this does not mean all binary properties were relinked, only supported ones were.");
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to relink unreal binary data to object pointers if they are part of the clone tree.
        /// It's gonna be an ugly mess.
        /// </summary>
        /// <param name="importpcc">PCC being imported from</param>
        private List<string> relinkBinaryObjects(IMEPackage importpcc)
        {
            List<string> relinkFailedReport = new List<string>();
            foreach (KeyValuePair<int, int> entry in crossPCCObjectMapping)
            {
                if (entry.Key > 0)
                {
                    IExportEntry exp = pcc.getExport(entry.Value);
                    byte[] binarydata = exp.getBinaryData();
                    if (binarydata.Length > 0)
                    {
                        //has binary data
                        //This is temporary until I find a more permanent style for relinking binary
                        try
                        {
                            switch (exp.ClassName)
                            {
                                case "WwiseEvent":
                                    {
                                        int count = BitConverter.ToInt32(binarydata, 0);
                                        for (int i = 0; i < count; i++)
                                        {
                                            int originalValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                            int currentObjectRef = unrealIndexToME3ExpIndexing(originalValue); //count + i * intsize
                                            int mapped;
                                            bool isMapped = crossPCCObjectMapping.TryGetValue(currentObjectRef, out mapped);
                                            if (isMapped)
                                            {
                                                mapped = me3ExpIndexingToUnreal(mapped, originalValue < 0); //if the value is 0, it would have an error anyways.
                                                Debug.WriteLine("Binary relink hit for WwiseEvent Export " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + originalValue + " -> " + (mapped + 1));
                                                WriteMem(4 + (i * 4), binarydata, BitConverter.GetBytes(mapped));
                                                int newValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                                Debug.WriteLine(originalValue + " -> " + newValue);
                                            }
                                            else
                                            {
                                                Debug.WriteLine("Binary relink missed WwiseEvent Export " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + originalValue);
                                            }
                                        }
                                        exp.setBinaryData(binarydata);
                                    }
                                    break;
                                case "Class":
                                    {
                                        if (exp.FileRef.Game != importpcc.Game)
                                        {
                                            //Cannot relink against a different game.
                                            continue;
                                        }
                                        IExportEntry importingExp = importpcc.getExport(entry.Key);
                                        if (importingExp.ClassName != "Class")
                                        {
                                            continue; //the class was not actually set, so this is not really class.
                                        }

                                        //This is going to be pretty ugly
                                        try
                                        {
                                            byte[] newdata = importpcc.Exports[entry.Key].Data; //may need to rewrite first unreal header
                                            byte[] data = importpcc.Exports[entry.Key].Data;

                                            int offset = 0;
                                            int unrealExportIndex = BitConverter.ToInt32(data, offset);
                                            offset += 4;


                                            int superclassIndex = BitConverter.ToInt32(data, offset);
                                            if (superclassIndex < 0)
                                            {
                                                //its an import
                                                ImportEntry superclassImportEntry = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(superclassIndex))];
                                                ImportEntry newSuperclassValue = getOrAddCrossImport(superclassImportEntry.GetFullPath, importpcc, exp.FileRef);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newSuperclassValue.UIndex));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Superclass is an export in the source package and was not relinked.");
                                            }


                                            int unknown1 = BitConverter.ToInt32(data, offset);
                                            offset += 4;

                                            int classObjTree = BitConverter.ToInt32(data, offset); //Don't know if I need to do this.
                                            offset += 4;


                                            //I am not sure what these mean. However if Pt1&2 are 33/25, the following bytes that follow are extended.
                                            int headerUnknown1 = BitConverter.ToInt32(data, offset);
                                            Int64 ignoreMask = BitConverter.ToInt64(data, offset);
                                            offset += 8;

                                            Int16 labelOffset = BitConverter.ToInt16(data, offset);
                                            offset += 2;
                                            int skipAmount = 0x6;
                                            //Find end of script block. Seems to be 10 FF's.
                                            while (offset + skipAmount + 10 < data.Length)
                                            {
                                                //Debug.WriteLine("Cheecking at 0x"+(offset + skipAmount + 10).ToString("X4"));
                                                bool isEnd = true;
                                                for (int i = 0; i < 10; i++)
                                                {
                                                    byte b = data[offset + skipAmount + i];
                                                    if (b != 0xFF)
                                                    {
                                                        isEnd = false;
                                                        break;
                                                    }
                                                }
                                                if (isEnd)
                                                {
                                                    break;
                                                }
                                                else
                                                {
                                                    skipAmount++;
                                                }
                                            }

                                            int offsetEnd = offset + skipAmount + 10;
                                            offset += skipAmount + 10; //heuristic to find end of script
                                            uint stateMask = BitConverter.ToUInt32(data, offset);
                                            offset += 4;

                                            int localFunctionsTableCount = BitConverter.ToInt32(data, offset);
                                            offset += 4;
                                            bool isMapped;
                                            for (int i = 0; i < localFunctionsTableCount; i++)
                                            {
                                                int nameTableIndex = BitConverter.ToInt32(data, offset);
                                                int nameIndex = BitConverter.ToInt32(data, offset + 4);
                                                NameReference importingName = importpcc.getNameEntry(nameTableIndex);
                                                int newFuncName = exp.FileRef.FindNameOrAdd(importingName);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newFuncName));
                                                offset += 8;

                                                int functionObjectIndex = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                                int mapped;
                                                isMapped = crossPCCObjectMapping.TryGetValue(functionObjectIndex, out mapped);
                                                if (isMapped)
                                                {
                                                    mapped = me3ExpIndexingToUnreal(mapped, functionObjectIndex < 0); //if the value is 0, it would have an error anyways.
                                                    WriteMem(offset, newdata, BitConverter.GetBytes(mapped));
                                                }
                                                else
                                                {
                                                    relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Local function[" + i + "] could not be remapped during porting: " + functionObjectIndex + " is not in the mapping tree and could not be relinked");
                                                }
                                                offset += 4;
                                            }

                                            int classMask = BitConverter.ToInt32(data, offset);
                                            offset += 4;
                                            if (importpcc.Game != MEGame.ME3)
                                            {
                                                offset += 1; //seems to be a blank byte here
                                            }

                                            int coreReference = BitConverter.ToInt32(data, offset);
                                            if (coreReference < 0)
                                            {
                                                //its an import
                                                ImportEntry outerclassReferenceImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(coreReference))];
                                                ImportEntry outerclassNewImport = getOrAddCrossImport(outerclassReferenceImport.GetFullPath, importpcc, exp.FileRef);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(outerclassNewImport.UIndex));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Outerclass is an export in the original package, not relinked.");
                                            }
                                            offset += 4;


                                            if (importpcc.Game == MEGame.ME3)
                                            {
                                                offset = ClassParser_RelinkComponentsTable(importpcc, exp, relinkFailedReport, ref newdata, offset);
                                                offset = ClassParser_ReadImplementsTable(importpcc, exp, relinkFailedReport, ref newdata, offset);
                                                int postComponentsNoneNameIndex = BitConverter.ToInt32(data, offset);
                                                int postComponentNoneIndex = BitConverter.ToInt32(data, offset + 4);
                                                string postCompName = importpcc.getNameEntry(postComponentsNoneNameIndex); //This appears to be unused in ME3, it is always None it seems.
                                                int newFuncName = exp.FileRef.FindNameOrAdd(postCompName);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newFuncName));
                                                offset += 8;

                                                int unknown4 = BitConverter.ToInt32(data, offset);
                                                offset += 4;
                                            }
                                            else
                                            {
                                                offset = ClassParser_ReadImplementsTable(importpcc, exp, relinkFailedReport, ref data, offset);
                                                offset = ClassParser_RelinkComponentsTable(importpcc, exp, relinkFailedReport, ref data, offset);

                                                int me12unknownend1 = BitConverter.ToInt32(data, offset);
                                                offset += 4;

                                                int me12unknownend2 = BitConverter.ToInt32(data, offset);
                                                offset += 4;
                                            }

                                            int defaultsClassLink = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                            int defClassLink;
                                            isMapped = crossPCCObjectMapping.TryGetValue(defaultsClassLink, out defClassLink);
                                            if (isMapped)
                                            {
                                                defClassLink = me3ExpIndexingToUnreal(defClassLink, defaultsClassLink < 0); //if the value is 0, it would have an error anyways.
                                                WriteMem(offset, newdata, BitConverter.GetBytes(defClassLink));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: DefaultsClassLink cannot be currently automatically relinked by Binary Relinker. Please manually set this in Binary Editor");
                                            }
                                            offset += 4;

                                            if (importpcc.Game == MEGame.ME3)
                                            {
                                                int functionsTableCount = BitConverter.ToInt32(data, offset);
                                                offset += 4;

                                                for (int i = 0; i < functionsTableCount; i++)
                                                {
                                                    int functionsTableIndex = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                                    int mapped;
                                                    isMapped = crossPCCObjectMapping.TryGetValue(functionsTableIndex, out mapped);
                                                    if (isMapped)
                                                    {
                                                        mapped = me3ExpIndexingToUnreal(mapped, functionsTableIndex < 0); //if the value is 0, it would have an error anyways.
                                                        WriteMem(offset, newdata, BitConverter.GetBytes(mapped));
                                                    }
                                                    else
                                                    {
                                                        if (functionsTableIndex < 0)
                                                        {
                                                            ImportEntry functionObjIndex = importpcc.Imports[Math.Abs(functionsTableIndex)];
                                                            ImportEntry newFunctionObjIndex = getOrAddCrossImport(functionObjIndex.GetFullPath, importpcc, exp.FileRef);
                                                            WriteMem(offset, newdata, BitConverter.GetBytes(newFunctionObjIndex.UIndex));
                                                        }
                                                        else
                                                        {
                                                            relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Full Functions List function[" + i + "] could not be remapped during porting: " + functionsTableIndex + " is not in the mapping tree and could not be relinked");

                                                        }
                                                    }
                                                    offset += 4;
                                                }
                                            }
                                            exp.Data = newdata;
                                        }
                                        catch (Exception ex)
                                        {
                                            relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Exception relinking: " + ex.Message);
                                        }
                                    }
                                    break;
                                default:
                                    continue;
                            }
                        }
                        catch (Exception e)
                        {
                            relinkFailedReport.Add("Binary relinking failed for " + exp.Index + " " + exp.GetFullPath + ":" + e.Message);
                        }
                        //Run an interpreter pass over it - we will find objectleafnodes and attempt to update the same offset in the destination file.
                        //BinaryInterpreter binaryrelinkInterpreter = new ME3Explorer.BinaryInterpreter(importpcc, importpcc.Exports[entry.Key], pcc, pcc.Exports[entry.Value], crossPCCObjectMapping);
                    }
                }
            }
            return relinkFailedReport;
        }

        private int ClassParser_RelinkComponentsTable(IMEPackage importpcc, IExportEntry exp, List<string> relinkFailedReport, ref byte[] data, int offset)
        {
            if (importpcc.Game == MEGame.ME3)
            {
                int componentTableNameIndex = BitConverter.ToInt32(data, offset);
                int componentTableIndex = BitConverter.ToInt32(data, offset + 4);
                NameReference importingName = importpcc.getNameEntry(componentTableNameIndex);
                int newComponentTableName = exp.FileRef.FindNameOrAdd(importingName);
                WriteMem(offset, data, BitConverter.GetBytes(newComponentTableName));
                offset += 8;

                int componentTableCount = BitConverter.ToInt32(data, offset);
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    importingName = importpcc.getNameEntry(nameTableIndex);
                    int componentName = exp.FileRef.FindNameOrAdd(importingName);
                    WriteMem(offset, data, BitConverter.GetBytes(componentName));
                    offset += 8;

                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    int mapped;
                    bool isMapped = crossPCCObjectMapping.TryGetValue(componentObjectIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, componentObjectIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        if (componentObjectIndex < 0)
                        {
                            ImportEntry componentObjectImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(componentObjectIndex))];
                            ImportEntry newComponentObjectImport = getOrAddCrossImport(componentObjectImport.GetFullPath, importpcc, exp.FileRef);
                            WriteMem(offset, data, BitConverter.GetBytes(newComponentObjectImport.UIndex));
                        }
                        else
                        {
                            relinkFailedReport.Add("Binary Class Component[" + i + "] could not be remapped during porting: " + componentObjectIndex + " is not in the mapping tree");
                        }
                    }
                    offset += 4;
                }
            }
            else
            {
                int componentTableCount = BitConverter.ToInt32(data, offset);
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);

                    offset += 8;

                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    if (componentObjectIndex != 0)
                    {
                        int mapped;
                        bool isMapped = crossPCCObjectMapping.TryGetValue(componentObjectIndex, out mapped);
                        if (isMapped)
                        {
                            mapped = me3ExpIndexingToUnreal(mapped, componentObjectIndex < 0); //if the value is 0, it would have an error anyways.
                            WriteMem(offset, data, BitConverter.GetBytes(mapped));
                        }
                        else
                        {
                            if (componentObjectIndex < 0)
                            {
                                ImportEntry componentObjectImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(componentObjectIndex))];
                                ImportEntry newComponentObjectImport = getOrAddCrossImport(componentObjectImport.GetFullPath, importpcc, exp.FileRef);
                                WriteMem(offset, data, BitConverter.GetBytes(newComponentObjectImport.UIndex));
                            }
                            else
                            {
                                relinkFailedReport.Add("Binary Class Component[" + i + "] could not be remapped during porting: " + componentObjectIndex + " is not in the mapping tree");
                            }
                        }
                    }
                    offset += 4;
                }
            }
            return offset;
        }

        private int ClassParser_ReadImplementsTable(IMEPackage importpcc, IExportEntry exp, List<string> relinkFailedReport, ref byte[] data, int offset)
        {
            if (importpcc.Game == MEGame.ME3)
            {
                int interfaceCount = BitConverter.ToInt32(data, offset);
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceIndex = BitConverter.ToInt32(data, offset);
                    int mapped;
                    bool isMapped = crossPCCObjectMapping.TryGetValue(interfaceIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, interfaceIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        relinkFailedReport.Add("Binary Class Interface Index[" + i + "] could not be remapped during porting: " + interfaceIndex + " is not in the mapping tree");
                    }
                    offset += 4;

                    //propertypointer
                    interfaceIndex = BitConverter.ToInt32(data, offset);
                    isMapped = crossPCCObjectMapping.TryGetValue(interfaceIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, interfaceIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        relinkFailedReport.Add("Binary Class Interface Index[" + i + "] could not be remapped during porting: " + interfaceIndex + " is not in the mapping tree");
                    }
                    offset += 4;
                }
            }
            else
            {
                int interfaceTableName = BitConverter.ToInt32(data, offset); //????
                NameReference importingName = importpcc.getNameEntry(interfaceTableName);
                int interfaceName = exp.FileRef.FindNameOrAdd(importingName);
                WriteMem(offset, data, BitConverter.GetBytes(interfaceName));
                offset += 8;

                int interfaceCount = BitConverter.ToInt32(data, offset);
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceNameIndex = BitConverter.ToInt32(data, offset);
                    importingName = importpcc.getNameEntry(interfaceNameIndex);
                    interfaceName = exp.FileRef.FindNameOrAdd(importingName);
                    WriteMem(offset, data, BitConverter.GetBytes(interfaceName));
                    offset += 8;
                }
            }
            return offset;
        }

        private int me3ExpIndexingToUnreal(int sourceObjReference, bool isImport = false)
        {
            if (sourceObjReference > 0)
            {
                sourceObjReference++; //make 1 based for mapping.
            }

            if (sourceObjReference < 0)
            {
                sourceObjReference--; //make 1 based for mapping.
            }

            //is 0: ???????
            if (sourceObjReference == 0)
            {
                if (isImport)
                {
                    sourceObjReference--;
                }
                else
                {
                    sourceObjReference++;
                }
            }

            return sourceObjReference;
        }

        /// <summary>
        /// Attempts to relink unreal property data if object pointers pointed to items within the subtree.
        /// </summary>
        private void relinkObjects(IMEPackage importpcc)
        {
            foreach (KeyValuePair<int, int> entry in crossPCCObjectMapping)
            {
                if (entry.Key > 0)
                {
                    //Run an interpreter pass over it - we will find objectleafnodes and attempt to update the same offset in the destination file.
                    Interpreter relinkInterpreter = new ME3Explorer.Interpreter(importpcc, importpcc.Exports[entry.Key], pcc, pcc.Exports[entry.Value], crossPCCObjectMapping);
                }
            }

            foreach (KeyValuePair<int, int> entry in crossPCCObjectMapping)
            {
                string debug = "Cross mapping: ";
                if (entry.Value >= 0)
                {

                    debug += "EXP ";
                    IExportEntry exp = importpcc.Exports[entry.Key];
                    IExportEntry destexp = pcc.Exports[entry.Value];
                    byte[] exportdata = destexp.Data;

                    debug += exp.PackageFullName + "." + exp.ObjectName;
                    debug += " -> ";
                    debug += destexp.PackageFullName + "." + destexp.ObjectName;

                    //Relinking objects.
                    //PropertyCollection sourceprops = exp.GetProperties();
                    //PropertyCollection destprops = destexp.GetProperties();
                    List<PropertyReader.Property> sourceprops = PropertyReader.getPropList(exp);
                    List<PropertyReader.Property> destprops = PropertyReader.getPropList(destexp);

                    for (int i = 0; i < sourceprops.Count; i++)
                    {
                        Property prop = sourceprops[i];
                        switch (prop.TypeVal)
                        {
                            case PropertyType.ObjectProperty:
                                {
                                    //ObjectProperty oprop = (ObjectProperty)prop;
                                    int sourceObjReference = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(exp.Data, prop.offsetval));
                                    int mapped; //may want to change this...
                                    bool isMapped = crossPCCObjectMapping.TryGetValue(sourceObjReference, out mapped);

                                    if (isMapped)
                                    {
                                        Property destprop = destprops[i];
                                        //ObjectProperty destoprop = (ObjectProperty)destprops[i];
                                        byte[] buff2 = BitConverter.GetBytes(mapped + 1); //+1 unreal indexing.
                                        for (int o = 0; o < 4; o++)
                                        {
                                            //Write object property value
                                            byte preval = exportdata[destprop.offsetval + o];
                                            exportdata[destprop.offsetval + o] = buff2[o];
                                            byte postval = exportdata[destprop.offsetval + o];

                                            Debug.WriteLine("Updating Byte at 0x" + (destprop.offsetval + o).ToString("X4") + " from " + preval + " to " + postval + ". It should have been set to " + buff2[o]);
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Object property was not mapped during cross-porting: " + sourceObjReference + " " + exp.PackageFullName + "." + exp.ObjectName);
                                    }
                                    //attempt relinkage
                                }
                                break;
                            case PropertyType.ArrayProperty:
                                //Todo: needs implementation (needs object detection code).
                                //May not be necessary since "arrays" have objectproperties.
                                break;
                        }
                    }
                    destexp.Data = exportdata;
                }
                else
                {
                    //ImportEntry imp = pcc.Imports[-entry.Value/* + 1*/];
                    debug += "IMP ";

                    //ImportEntry imp = importpcc.Imports[-entry.Key];
                    //ImportEntry destimp = pcc.Imports[-entry.Value - 1];
                    //debug += imp.PackageFullName + "." + imp.ObjectName;
                    //debug += " -> ";
                    //debug += destimp.PackageFullName + "." + destimp.ObjectName;
                }
                //                Debug.WriteLine(debug);

                //Read Props.
            }
        }

        private void WriteMem(int pos, byte[] memory, byte[] dataToWrite)
        {
            for (int i = 0; i < dataToWrite.Length; i++)
                memory[pos + i] = dataToWrite[i];
        }

        private int unrealIndexToME3ExpIndexing(int sourceObjReference)
        {
            if (sourceObjReference > 0)
            {
                sourceObjReference--; //make 0 based for mapping.
            }

            if (sourceObjReference < 0)
            {
                sourceObjReference++; //make 0 based for mapping.
            }

            return sourceObjReference;
        }

        /// <summary>
        /// Attempts to relink unreal property data using propertycollection when cross porting an export
        /// </summary>
        private List<string> relinkObjects2(IMEPackage importpcc)
        {
            List<string> relinkResults = new List<string>();
            //relink each modified export

            //We must convert this to a list, as this list will be updated as imports are cross mapped during relinking.
            //This process speeds up same-relinks later.
            //This is a list because otherwise we would get a concurrent modification exception.
            //Since we only enumerate exports and append imports to this list we will not need to worry about recursive links
            //I am sure this won't come back to be a pain for me.
            List<KeyValuePair<int, int>> crossMappingList = crossPCCObjectMapping.ToList();
            for (int i = 0; i < crossMappingList.Count; i++)
            {
                KeyValuePair<int, int> entry = crossMappingList[i];
                if (entry.Key > 0)
                {
                    PropertyCollection transplantProps = importpcc.Exports[entry.Key].GetProperties();
                    relinkResults.AddRange(relinkPropertiesRecursive(importpcc, pcc, transplantProps, crossMappingList));
                    pcc.getExport(entry.Value).WriteProperties(transplantProps);
                    //Run an interpreter pass over it - we will find objectleafnodes and attempt to update the same offset in the destination file.
                    //Interpreter relinkInterpreter = new ME3Explorer.Interpreter(importpcc, importpcc.Exports[entry.Key], pcc, pcc.Exports[entry.Value], crossPCCObjectMapping);
                }
            }

            return relinkResults;
        }

        private List<string> relinkPropertiesRecursive(IMEPackage importingPCC, IMEPackage destinationPCC, PropertyCollection transplantProps, List<KeyValuePair<int, int>> crossPCCObjectMapping)
        {
            List<string> relinkResults = new List<string>();
            foreach (UProperty prop in transplantProps)
            {
                if (prop is StructProperty)
                {
                    relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, destinationPCC, (prop as StructProperty).Properties, crossPCCObjectMapping));
                }
                else if (prop is ArrayProperty<StructProperty>)
                {
                    foreach (StructProperty arrayStructProperty in prop as ArrayProperty<StructProperty>)
                    {
                        relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, destinationPCC, arrayStructProperty.Properties, crossPCCObjectMapping));
                    }
                }
                else if (prop is ArrayProperty<ObjectProperty>)
                {
                    foreach (ObjectProperty objProperty in prop as ArrayProperty<ObjectProperty>)
                    {
                        string result = relinkObjectProperty(importingPCC, destinationPCC, objProperty, crossPCCObjectMapping);
                        if (result != null)
                        {
                            relinkResults.Add(result);
                        }
                    }
                }
                if (prop is ObjectProperty)
                {
                    //relink
                    string result = relinkObjectProperty(importingPCC, destinationPCC, prop as ObjectProperty, crossPCCObjectMapping);
                    if (result != null)
                    {
                        relinkResults.Add(result);
                    }
                }
            }
            return relinkResults;
        }

        private string relinkObjectProperty(IMEPackage importingPCC, IMEPackage destinationPCC, ObjectProperty objProperty, List<KeyValuePair<int, int>> crossPCCObjectMapping)
        {
            if (objProperty.Value == 0)
            {
                return null; //do not relink 0
            }
            int sourceObjReference = objProperty.Value;

            if (sourceObjReference > 0)
            {
                sourceObjReference--; //make 0 based for mapping.
            }
            if (sourceObjReference < 0)
            {
                sourceObjReference++; //make 0 based for mapping.
            }

            KeyValuePair<int, int> mapping = crossPCCObjectMapping.Where(pair => pair.Key == sourceObjReference).FirstOrDefault();
            var defaultKVP = default(KeyValuePair<int, int>); //struct comparison

            if (!mapping.Equals(defaultKVP))
            {
                //relink
                objProperty.Value = (mapping.Value + 1);
                Debug.WriteLine("Relink hit: " + sourceObjReference);
            }
            else if (objProperty.Value < 0) //It's an unmapped import
            {
                //objProperty is currently pointing to importingPCC as that is where we read the properties from
                int n = objProperty.Value;
                int importZeroIndex = Math.Abs(n) - 1;
                //Debug.WriteLine("Relink miss, attempting JIT relink on " + n + " " + rootNode.Text);
                if (n < 0 && importZeroIndex < importingPCC.ImportCount)
                {
                    //Get the original import
                    ImportEntry origImport = importingPCC.getImport(importZeroIndex);
                    string origImportFullName = origImport.GetFullPath;
                    //Debug.WriteLine("We should import " + origImport.GetFullPath);

                    ImportEntry crossImport = getOrAddCrossImport(origImportFullName, importingPCC, destinationPCC);

                    if (crossImport != null)
                    {
                        crossPCCObjectMapping.Add(new KeyValuePair<int, int>(sourceObjReference, crossImport.UIndex)); //add to mapping to speed up future relinks
                        objProperty.Value = crossImport.UIndex;
                    }
                }
            }
            else
            {
                Debug.WriteLine("Relink failed: " + objProperty.Name + " " + objProperty.Value + " " + importingPCC.getEntry(objProperty.Value).GetFullPath);
                return "Relink failed: " + objProperty.Name + " " + objProperty.Value + " " + importingPCC.getEntry(objProperty.Value).GetFullPath;
            }
            return null;
        }

        /// <summary>
        /// Adds an import from the importingPCC to the destinationPCC with the specified importFullName, or returns the existing one if it can be found. 
        /// This method will look at importingPCC's import upstream chain and check for the most downstream one's existence in destinationPCC, 
        /// including if none can be founc (in which case the entire upstream is copied). It will then create new imports to match the remaining 
        /// downstream ones and return the originally named import, however now located in destinationPCC.
        /// </summary>
        /// <param name="importFullName">GetFullPath() of an import from ImportingPCC</param>
        /// <param name="importingPCC">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <returns></returns>
        private ImportEntry getOrAddCrossImport(string importFullName, IMEPackage importingPCC, IMEPackage destinationPCC)
        {
            //This code is kind of ugly, sorry.

            //see if this import exists locally
            foreach (ImportEntry imp in destinationPCC.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    return imp;
                }
            }

            //Import doesn't exist, so we're gonna need to add it
            //But first we need to figure out what needs to be added upstream as links
            //Search upstream until we find something, or we can't get any more upstreams
            string[] importParts = importFullName.Split('.');
            List<int> upstreamLinks = new List<int>(); //0 = top level, 1 = next level... n = what we wanted to import
            int upstreamCount = 1;

            ImportEntry upstreamImport = null;
            //get number of required upstream imports that do not yet exist
            while (upstreamCount < importParts.Count())
            {
                string upstream = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                foreach (ImportEntry imp in destinationPCC.Imports)
                {
                    if (imp.GetFullPath == upstream)
                    {
                        upstreamImport = imp;
                        break;
                    }
                }

                if (upstreamImport != null)
                {
                    //We found an upsteam import that already exists
                    break;
                }
                upstreamCount++;
            }

            IExportEntry donorUpstreamExport = null;
            ImportEntry mostdownstreamimport = null;
            if (upstreamImport == null)
            {
                //We have to import the entire upstream chain
                string fullobjectname = importParts[0];
                //if (fullobjectname == "BioVFX_Z_GLOBAL")
                //{
                //    Debugger.Break();
                //}
                ImportEntry donorTopLevelImport = null;
                foreach (ImportEntry imp in importingPCC.Imports) //importing side info we will move to our dest pcc
                {
                    //if (imp.GetFullPath.StartsWith("BioVFX"))
                    //{
                    //    Console.WriteLine(imp.GetFullPath);
                    //}
                    if (imp.GetFullPath == fullobjectname)
                    {
                        donorTopLevelImport = imp;
                        break;
                    }
                }

                if (donorTopLevelImport == null)
                {
                    //This is issue KinkoJiro had. It is aborting relinking at this step. Will need to find a way to
                    //work with exports as parents for imports which i 
                    Debug.WriteLine("No upstream import was found in the source file. It's probably an export: " + importFullName);
                    foreach (IExportEntry exp in destinationPCC.Exports) //importing side info we will move to our dest pcc
                    {
                        //Console.WriteLine(exp.GetFullPath);
                        if (exp.GetFullPath == fullobjectname)
                        {
                            // = imp;
                            //We will need to find a way to cross map this as this will block cross import mapping unless these exports already exist.
                            Debug.WriteLine("FOUND UPSTREAM, AS EXPORT!");
                            donorUpstreamExport = exp;
                            upstreamCount--; //level 1 now from the top down
                                             //Create new import with this as higher IDK
                            break;
                        }
                    }
                    if (donorUpstreamExport == null)
                    {
                        Debug.WriteLine("An error has occured. Could not find an upstream import or export for relinking: " + fullobjectname + " from " + pcc.FileName);
                        return null;
                    }
                }

                if (donorUpstreamExport == null)
                {
                    //Create new toplevel import and set that as the most downstream one. (top = bottom at this point)
                    int downstreamPackageName = destinationPCC.FindNameOrAdd(donorTopLevelImport.PackageFile);
                    int downstreamClassName = destinationPCC.FindNameOrAdd(donorTopLevelImport.ClassName);
                    int downstreamName = destinationPCC.FindNameOrAdd(fullobjectname);

                    mostdownstreamimport = new ImportEntry(destinationPCC);
                    // mostdownstreamimport.idxLink = downstreamLinkIdx; ??
                    mostdownstreamimport.idxClassName = downstreamClassName;
                    mostdownstreamimport.idxObjectName = downstreamName;
                    mostdownstreamimport.idxPackageName = downstreamPackageName;
                    destinationPCC.addImport(mostdownstreamimport); //Add new top level downstream import
                    upstreamImport = mostdownstreamimport;
                    upstreamCount--; //level 1 now from the top down
                                     //return null;
                }
            }

            //Have an upstream import, now we need to add downstream imports.
            while (upstreamCount > 0)
            {
                upstreamCount--;
                string fullobjectname = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                ImportEntry donorImport = null;

                //Get or create names for creating import and get upstream linkIdx
                int downstreamName = destinationPCC.FindNameOrAdd(importParts[importParts.Count() - upstreamCount - 1]);
                foreach (ImportEntry imp in importingPCC.Imports) //importing side info we will move to our dest pcc
                {
                    if (imp.GetFullPath == fullobjectname)
                    {
                        donorImport = imp;
                        break;
                    }
                }
                int downstreamPackageName = destinationPCC.FindNameOrAdd(Path.GetFileNameWithoutExtension(donorImport.PackageFile));
                int downstreamClassName = destinationPCC.FindNameOrAdd(donorImport.ClassName);

                mostdownstreamimport = new ImportEntry(destinationPCC);
                mostdownstreamimport.idxLink = donorUpstreamExport == null ? upstreamImport.UIndex : donorUpstreamExport.UIndex;
                mostdownstreamimport.idxClassName = downstreamClassName;
                mostdownstreamimport.idxObjectName = downstreamName;
                mostdownstreamimport.idxPackageName = downstreamPackageName;
                destinationPCC.addImport(mostdownstreamimport);
                upstreamImport = mostdownstreamimport;
            }
            return mostdownstreamimport;
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
                    if (!importExport(importpcc, index, n))
                    {
                        return false;
                    }
                    nextIndex = pcc.ExportCount;
                }
                else
                {
                    getOrAddCrossImport(importpcc.getImport(Math.Abs(index) - 1).GetFullPath, importpcc, pcc);
                    nextIndex = -pcc.ImportCount;
                }
                if (node.Nodes.Count > 0)
                {
                    if (!importTree(node, importpcc, nextIndex))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        //Use this method to import an import that is linked to another import in the source pcc.
        //For example, Engine.Level is Level is linked to Engine, both of which are imports themselves.
        //This will recursively add all higher up links as imports if necessary

        /// <summary>
        /// Use this method to import an import that is linked to another import in the source pcc.
        /// For example, Engine.Level is Level is linked to Engine, both of which are imports themselves.
        /// This will recursively add all higher up links as imports if necessary
        /// </summary>
        /// <param name="importpcc"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        /*private int importImportedLinkedImport(IMEPackage importpcc, int n)
        {
            ImportEntry imp = importpcc.getImport(n);
            Debug.WriteLine("Checking for upper imports on id " + n + " " + imp.PackageFullName + "." + imp.ObjectName);
            int importingLink = imp.idxLink;

            if (importingLink > 0)
            {
                return 0; //We can't deal with exports right now.
            }
            if (importingLink == 0)
            {
                //
                Debug.WriteLine("Found a top level import: " + imp.PackageFullName + "." + imp.ObjectName);
                return FindOrAddCrossImport(importpcc, imp, importingLink);
            }

            ImportEntry importingLinkImport = importpcc.getImport(Math.Abs(importingLink) - 1);
            //Debug.WriteLine("Import Link: " + importingLinkImport.PackageFullName + "." + importingLinkImport.ObjectName);
            int importLink = importImportedLinkedImport(importpcc, Math.Abs(importingLink) - 1);
            return FindOrAddCrossImport(importpcc, imp, (importLink + 1) * -1);
            ////Upper linked imports now exist. We should check to see if our import exists - if it doesn't, we should add it and link it to the proper one.
            //string requiredImportObject = imp.PackageFullName + "." + imp.ObjectName;

            //ImportEntry localImport = null;
            //int localImportIndex = 0;
            //bool foundLocalImport = false;


            //foreach (ImportEntry impentry in pcc.Imports)
            //{
            //    //This is 0 based - but the game uses 1 based!
            //    string fullname = impentry.PackageName + "." + impentry.ObjectName;
            //    if (fullname == requiredImportObject)
            //    {
            //        //Already Imported. 
            //        foundLocalImport = true;
            //        classValue = -1 * (localImportIndex + 1);
            //        Debug.WriteLine("Set class to existing import " + requiredImportObject);
            //        break;
            //    }
            //    localImportIndex++;
            //}



            //ImportEntry nimp = new ImportEntry(pcc, new MemoryStream(imp.header));
            ////nimp.idxLink = link;
            //nimp.idxClassName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxClassName));
            //nimp.idxObjectName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxObjectName));
            //nimp.idxPackageName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxPackageName));
            //pcc.addImport(nimp);
        }

        /// <summary>
        /// Used to remap export metadata and object properties across PCCs.
        /// </summary>
        /// <param name="importpcc"></param>
        /// <param name="import"></param>
        /// <param name="link"></param>
        /// <returns></returns>
        /* private int FindOrAddCrossImport(IMEPackage importpcc, ImportEntry import, int link)
        {
            string requiredImportObject = import.GetFullPath;
            int localImportIndex = 0;
            foreach (ImportEntry impentry in pcc.Imports)
            {
                //This is 0 based - but the game uses 1 based!
                string fullname = impentry.GetFullPath;
                //Debug.WriteLine(fullname +" vs "+requiredImportObject);
                if (fullname.Equals(requiredImportObject))
                {
                    //Already Imported.
                    crossPCCObjectMapping[-import.Index] = -(localImportIndex); //0 based.
                    return localImportIndex; //Returns 0 based positive array index.
                }
                localImportIndex++;
            }

            //Didn't find it.
            //Debug.WriteLine(import.GetFullPath+" Importing and linking to " + link);
            importImport(importpcc, import.Index, link);
            crossPCCObjectMapping[import.UIndex] = -pcc.ImportCount - 1; //0 based.
                                                                         //Debug.WriteLine("Added import: " + pcc.Imports[pcc.ImportCount - 1].PackageFullName + "." + pcc.Imports[pcc.ImportCount - 1].PackageFullName + " at " + pcc.Imports[pcc.ImportCount - 1]);
            return pcc.ImportCount - 1;
        }

        private void importImport(IMEPackage importpcc, int n, int link)
        {
            ImportEntry imp = importpcc.getImport(n);
            ImportEntry nimp = new ImportEntry(pcc, new MemoryStream(imp.header));
            nimp.idxLink = link;
            nimp.idxClassName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxClassName));
            nimp.idxObjectName = pcc.FindNameOrAdd(importpcc.getNameEntry(imp.idxObjectName));
            NameReference name = importpcc.getNameEntry(imp.idxPackageName);
            nimp.idxPackageName = pcc.FindNameOrAdd(name);
            pcc.addImport(nimp);
            if (crossPCCObjectMapping != null)
            {
                crossPCCObjectMapping[-n] = -(pcc.ImportCount - 1); //0 based.

            }
        }*/

        private bool importExport(IMEPackage importpcc, int n, int link)
        {
            IExportEntry ex = importpcc.getExport(n);
            IExportEntry nex = null;
            switch (pcc.Game)
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
                case MEGame.UDK:
                    nex = new UDKExportEntry(pcc as UDKPackage);
                    break;
            }
            byte[] idata = ex.Data;
            PropertyCollection props = ex.GetProperties();
            int start = ex.GetPropertyStart();
            int end = props.endOffset;
            MemoryStream res = new MemoryStream();
            if ((importpcc.getExport(n).ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                byte[] stackdummy = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, //Lets hope for the best :D
                                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,};
                if (pcc.Game != MEGame.ME3)
                {
                    stackdummy = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,};
                }
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
                props.WriteTo(res, pcc);
            }
            catch (Exception exception)
            {
                //restore namelist
                pcc.setNames(names);
                MessageBox.Show("Error occured while trying to import " + ex.ObjectName + " : " + exception.Message);
                return false;
            }

            //set header so addresses are set
            nex.setHeader((byte[])ex.header.Clone());
            bool dataAlreadySet = false;
            if (importpcc.Game == MEGame.ME3)
            {
                switch (importpcc.getObjectName(ex.idxClass))
                {
                    case "SkeletalMesh":
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
                            break;
                        }
                    default:
                        //Write binary
                        res.Write(idata, end, idata.Length - end);
                        break;
                }
            }
            else if (importpcc.Game == MEGame.UDK)
            {
                switch (importpcc.getObjectName(ex.idxClass))
                {
                    case "StaticMesh":
                        {
                            //res.Write(idata, end, idata.Length - end);
                            //rewrite data
                            nex.Data = res.ToArray();
                            UDKStaticMesh usm = new UDKStaticMesh(importpcc as UDKPackage, n);
                            usm.PortToME3Export(nex);
                            dataAlreadySet = true;
                            break;
                        }
                    default:
                        //Write binary
                        res.Write(idata, end, idata.Length - end);
                        break;
                }
            }
            else
            {
                //Write binary
                res.Write(idata, end, idata.Length - end);
            }

            int classValue = 0;
            int archetype = 0;

            //Debug.WriteLine("Importing on ME3 game...");
            //Set class. This will only work if the class is an import, as we can't reliably pull in exports without lots of other stuff.
            if (ex.idxClass < 0)
            {
                //The class of the export we are importing is an import. We should attempt to relink this.
                ImportEntry portingFromClassImport = importpcc.getImport(Math.Abs(ex.idxClass) - 1);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, importpcc, pcc);
                classValue = newClassImport.UIndex;
                //Debug.WriteLine("IMPORTING A CLASS THAT IS AN IMPORT!");
                /*int iImportIndex = Math.Abs(ex.idxClass) - 1;
                ImportEntry classType = importpcc.Imports[iImportIndex];
                string requiredImportObject = classType.PackageFullName + "." + classType.ObjectName;
                //Debug.WriteLine("CLASSTYPE: " + classType.PackageFullName + "." + classType.ObjectName);

                //Check if this is an available import

                //ImportEntry localImport = null;
                int localImportIndex = 0;
                bool foundLocalImport = false;
                foreach (ImportEntry imp in pcc.Imports)
                {
                    //This is 0 based - but the game uses 1 based!
                    string fullname = imp.PackageFullName + "." + imp.ObjectName;
                    if (fullname == requiredImportObject)
                    {
                        //Already Imported. 
                        foundLocalImport = true;
                        classValue = -1 * (localImportIndex + 1);
                        //Debug.WriteLine("Set class to existing import " + requiredImportObject);
                        break;
                    }
                    localImportIndex++;
                }

                if (!foundLocalImport)
                {
                    //We need to add an import for this and recursively add required ones
                    // importImport(importpcc, int n, int link)
                    importImport(importpcc, iImportIndex, localLink);
                }*/
            }

            //Check archetype.
            if (ex.idxArchtype < 0)
            {
                ImportEntry portingFromClassImport = importpcc.getImport(Math.Abs(ex.idxArchtype) - 1);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, importpcc, pcc);
                archetype = newClassImport.UIndex;
                //The class of the export we are importing is an import. We should attempt to relink this.
                /* int localArchetype = 1 + (-1 * importImportedLinkedImport(importpcc, Math.Abs(ex.idxArchtype) - 1)); //Will ensure all upper links are available

                 //Debug.WriteLine("IMPORTING A CLASS THAT IS AN IMPORT!");
                 int iImportIndex = Math.Abs(ex.idxArchtype) - 1;
                 ImportEntry classType = importpcc.Imports[iImportIndex];
                 string requiredImportObject = classType.PackageFullName + "." + classType.ObjectName;
                 //Debug.WriteLine("CLASSTYPE: " + classType.PackageFullName + "." + classType.ObjectName);

                 //Check if this is an available import

                 //ImportEntry localImport = null;
                 int localImportIndex = 0;
                 bool foundLocalImport = false;
                 foreach (ImportEntry imp in pcc.Imports)
                 {
                     //This is 0 based - but the game uses 1 based!
                     string fullname = imp.PackageFullName + "." + imp.ObjectName;
                     if (fullname == requiredImportObject)
                     {
                         //Already Imported. 
                         foundLocalImport = true;
                         archetype = -1 * (localImportIndex + 1);
                         //Debug.WriteLine("Set class to existing import " + requiredImportObject);
                         break;
                     }
                     localImportIndex++;
                 }

                 if (!foundLocalImport)
                 {
                     //We need to add an import for this and recursively add required ones
                     // importImport(importpcc, int n, int link)
                     importImport(importpcc, n, localArchetype);
                 }*/
            }



            if (!dataAlreadySet)
            {
                nex.Data = res.ToArray();
            }
            nex.idxClass = classValue;
            nex.idxObjectName = pcc.FindNameOrAdd(importpcc.getNameEntry(ex.idxObjectName));
            nex.idxLink = link;
            nex.idxArchtype = archetype;
            nex.idxClassParent = 0;
            pcc.addExport(nex);

            crossPCCObjectMapping[n] = pcc.ExportCount - 1; //0 based.
            return true;
        }

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data is System.Windows.Forms.DataObject)
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
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void editInCurveEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = 0;
            if (GetSelected(out n) && n >= 0 && pcc.Game == MEGame.ME3)
            {
                CurveEd.CurveEditor c = new CurveEd.CurveEditor(pcc.getExport(n));
                c.Show();
            }
        }

        private void PackageEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            Dispose(true);
        }

        private void PackageEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool importChanges = changes.Contains(PackageChange.Import) || changes.Contains(PackageChange.ImportAdd);
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);
            int n = 0;
            bool hasSelection = GetSelected(out n);
            if (CurrentView == View.Names && changes.Contains(PackageChange.Names))
            {
                int scrollTo = listBox1.TopIndex + 1;
                int selected = listBox1.SelectedIndex;
                RefreshView();
                listBox1.SelectedIndex = selected;
                listBox1.TopIndex = scrollTo;
            }
            else if (CurrentView == View.Imports && importChanges ||
                     CurrentView == View.Exports && exportNonDataChanges ||
                     CurrentView == View.Tree && (importChanges || exportNonDataChanges))
            {
                RefreshView();
                if (hasSelection)
                {
                    goToNumber(n);
                }
            }
            else if ((CurrentView == View.Exports || CurrentView == View.Tree) &&
                     hasSelection &&
                     updates.Contains(new PackageUpdate { index = n, change = PackageChange.ExportData }))
            {
                interpreterControl.memory = pcc.getExport(n).Data;
                interpreterControl.RefreshMem();
                Preview(true);
            }
        }

        private void editToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (CurrentView == View.Names && listBox1.SelectedIndex != -1)
            {
                int idx = listBox1.SelectedIndex;
                string result = Microsoft.VisualBasic.Interaction.InputBox("", "Rename", pcc.getNameEntry(idx));
                if (result != "")
                {
                    pcc.replaceName(idx, result);
                }
            }
        }

        private void PackageEditor_Activated(object sender, EventArgs e)
        {
            if (pendingMetaDataUpdate)
            {
                pendingMetaDataUpdate = false;
                RefreshMetaData();
            }
        }

        private void reindexClassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = 0;
            if (GetSelected(out n))
            {
                IExportEntry exp = pcc.Exports[n];
                string objectname = exp.ObjectName;
                var confirmResult = MessageBox.Show("Confirming reindexing of all exports with object name:\n" + objectname + "\n\nEnsure this file has a backup - this operation will make many changes to export indexes!",
                                     "Confirm Reindexing",
                                     MessageBoxButtons.YesNo);
                if (confirmResult == DialogResult.Yes)
                {
                    // Get list of all exports with that object name.
                    //List<IExportEntry> exports = new List<IExportEntry>();
                    //Could use LINQ... meh.

                    int index = 1; //we'll start at 1.
                    foreach (IExportEntry export in pcc.Exports)
                    {
                        if (objectname == export.ObjectName && export.ClassName != "Class")
                        {
                            export.indexValue = index;
                            index++;
                        }
                    }

                    RefreshView();
                    goToNumber(n);
                }
            }
        }

        private void setAllIndexesInThisTreeTo0ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = 0;
            if (GetSelected(out n))
            {
                IExportEntry exp = pcc.Exports[n];
                string objectname = exp.ObjectName;
                var confirmResult = MessageBox.Show("Confirming setting of all indexes to 0 on " + objectname + " and its children.\n\nEnsure this file has a backup - this operation will make many changes to export indexes!",
                                     "Confirm Reindexing",
                                     MessageBoxButtons.YesNo);
                if (confirmResult == DialogResult.Yes)
                {
                    // Get list of all exports with that object name.
                    //List<IExportEntry> exports = new List<IExportEntry>();
                    //Could use LINQ... meh.

                    if (treeView1.SelectedNode != null)
                    {
                        recursiveSetIndexesToZero(treeView1.SelectedNode);
                        RefreshView();
                        goToNumber(n);
                    }


                }
            }
        }

        private void recursiveSetIndexesToZero(TreeNode rootNode)
        {
            int index;
            if (rootNode.Nodes.Count > 0)
            {
                foreach (TreeNode node in rootNode.Nodes)
                {
                    index = Convert.ToInt32(node.Name);
                    if (index >= 0)
                    {
                        IExportEntry exportEntry = pcc.getExport(index);
                        exportEntry.indexValue = 0;
                        //pcc.;
                    }
                    recursiveSetIndexesToZero(node);
                }
            }
            index = Convert.ToInt32(rootNode.Name);
            if (index >= 0)
            {
                IExportEntry exportEntry = pcc.getExport(index);
                exportEntry.indexValue = 0;
            }
        }

        private void showFullPaths_CheckChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.UseMetadataFullPaths = showFullPathsCheckbox.Checked;
            RefreshMetaData();
        }

        private void findImportexportViaOffsetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string input = "Enter an offset (in hex, e.g. 2fa360) to find what export or import contains that offset";
            DialogResult response = WinFormInputDialog.ShowInputDialog(ref input);
            if (response == DialogResult.OK)
            {
                int idx = int.Parse(input, System.Globalization.NumberStyles.HexNumber);
                for (int i = 0; i < pcc.ExportCount; i++)
                {
                    IExportEntry exp = pcc.Exports[i];
                    if (idx > exp.DataOffset && idx < exp.DataOffset + exp.DataSize)
                    {
                        goToNumber(exp.Index);
                        break;
                    }
                }
            }
        }

        private void checkIndexingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
            {
                return;
            }
            List<string> duplicates = new List<string>();
            Dictionary<string, List<int>> nameIndexDictionary = new Dictionary<string, List<int>>();
            foreach (IExportEntry exp in pcc.Exports)
            {
                string key = exp.GetFullPath;
                List<int> indexList;
                bool hasExistingList = nameIndexDictionary.TryGetValue(key, out indexList);
                if (!hasExistingList)
                {
                    indexList = new List<int>();
                    nameIndexDictionary[key] = indexList;
                }
                bool isDuplicate = indexList.Contains(exp.indexValue);
                if (isDuplicate)
                {
                    duplicates.Add(exp.Index + " " + exp.GetFullPath + " has duplicate index (index value " + exp.indexValue + ")");
                }
                else
                {
                    indexList.Add(exp.indexValue);
                }
            }

            if (duplicates.Count > 0)
            {
                string copy = "";
                foreach (string str in duplicates)
                {

                    copy += str + "\n";
                }
                //Clipboard.SetText(copy);
                MessageBox.Show(duplicates.Count + " duplicate indexes were found.", "BAD INDEXING");
                ListWindow lw = new ListWindow(duplicates, "Duplicate indexes", "The following items have duplicate indexes.");
                lw.ShowDialog(this);
            }
            else
            {
                MessageBox.Show("No duplicate indexes were found.", "Indexing OK");
            }
        }

        private void dEBUGCallReadPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (!GetSelected(out n))
            {
                return;
            }
            if (n >= 0)
            {
                try
                {
                    IExportEntry exp = pcc.Exports[n];
                    exp.GetProperties(true); //force properties to reload
                }
                catch (Exception ex)
                {

                }
            }
        }

        private void pathfindingEditorToolstripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {
                PathfindingEditor editor = new PathfindingEditor();
                editor.LoadFile(pcc.FileName);
                editor.BringToFront();
                editor.Show();
            }
        }

        private void sequenceEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {
                SequenceEditor editor = new SequenceEditor();
                editor.LoadFile(pcc.FileName);
                editor.BringToFront();
                editor.Show();
            }
        }

        private void faceFXEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {
                FaceFX.FaceFXEditor fxeditor = new FaceFX.FaceFXEditor();
                fxeditor.LoadFile(pcc.FileName);
                fxeditor.Show();
            }
        }

        private void wWiseBankEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {
                WwiseBankEditor.WwiseEditor fxeditor = new WwiseBankEditor.WwiseEditor();
                fxeditor.LoadFile(pcc.FileName);
                fxeditor.Show();
            }
        }

        private void reloadTLKsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ME3TalkFiles.ReloadTLKData();
            MessageBox.Show(this, "TLKs have been reloaded.", "TLK list reloaded");
        }

        private void dEBUGCopyConfigurablePropsToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fpath = @"X:\Mass Effect Games HDD\Mass Effect";
            var ext = new List<string> { "u", "upk", "sfm" };
            var files = Directory.GetFiles(fpath, "*.*", SearchOption.AllDirectories)
              .Where(file => new string[] { ".sfm", ".upk", ".u" }
              .Contains(Path.GetExtension(file).ToLower()))
              .ToList();
            StringBuilder sb = new StringBuilder();

            int threads = Environment.ProcessorCount;
            string[] results = files.AsParallel().WithDegreeOfParallelism(threads).WithExecutionMode(ParallelExecutionMode.ForceParallelism).Select(ScanForConfigValues).ToArray();


            foreach (string res in results)
            {
                sb.Append(res);
            }
            try
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Finished");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string ScanForConfigValues(string file)
        {
            StringBuilder sb = new StringBuilder();
            bool fileHasConfig = false;
            IMEPackage pack = MEPackageHandler.OpenMEPackage(file);
            foreach (IExportEntry exp in pack.Exports)
            {
                if (exp.ClassName == "Bio2DA" || exp.ClassName == "Bio2DANumberedRows")
                {
                    if (!fileHasConfig)
                    {
                        sb.AppendLine();
                        sb.Append(pack.FileName);
                        sb.AppendLine();
                        fileHasConfig = true;
                    }
                    sb.Append(exp.ClassName + "\t" + exp.GetFullPath);
                    sb.AppendLine();
                }
            }
            pack.Release();
            if (sb.Length == 0)
            {
                return "";
            }
            else
            {
                return sb.ToString();
            }
        }

        private void dEBUGCopyAllBIOGItemsToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fpath = @"X:\Mass Effect Games HDD\Mass Effect";
            var ext = new List<string> { "u", "upk", "sfm" };
            var files = Directory.GetFiles(fpath, "*.*", SearchOption.AllDirectories)
              .Where(file => new string[] { ".sfm", ".upk", ".u" }
              .Contains(Path.GetExtension(file).ToLower()))
              .ToList();
            StringBuilder sb = new StringBuilder();

            int threads = Environment.ProcessorCount;
            List<string>[] results = files.AsParallel().WithDegreeOfParallelism(threads).WithExecutionMode(ParallelExecutionMode.ForceParallelism).Select(ScanForBioG).ToArray();

            HashSet<string> items = new HashSet<string>();
            foreach (List<string> res in results)
            {
                items.UnionWith(res);
            }

            foreach (string str in items)
            {
                sb.AppendLine(str);
            }
            try
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Finished");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private List<string> ScanForBioG(string file)
        {
            //Console.WriteLine(file);
            try
            {
                List<string> biopawnscaled = new List<string>();
                IMEPackage pack = MEPackageHandler.OpenMEPackage(file);
                foreach (IExportEntry exp in pack.Exports)
                {
                    if (exp.ClassName == "BioPawnChallengeScaledType")
                    {
                        biopawnscaled.Add(exp.GetFullPath);
                    }
                }
                pack.Release();
                return biopawnscaled;
            }
            catch (Exception e)
            {
                Debugger.Break();
            }
            return null;
        }

        private void dEBUGExport2DAToExcelFileToolStripMenuItem_Click(object sender, EventArgs e)
        {




        }

        private void findExportsWithSerialSizeMismatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> serialexportsbad = new List<string>();
            foreach (IExportEntry entry in pcc.Exports)
            {
                Console.WriteLine(entry.Index + " " + entry.Data.Length + " " + entry.DataSize);
                if (entry.Data.Length != entry.DataSize)
                {
                    serialexportsbad.Add(entry.GetFullPath + " Header lists: " + entry.DataSize + ", Actual data size: " + entry.Data.Length);
                }
            }

            if (serialexportsbad.Count > 0)
            {
                ListWindow lw = new ListWindow(serialexportsbad, "Serial Size Mismatches", "The following exports had serial size mismatches.");
            }
            else
            {
                MessageBox.Show("No exports have serial size mismatches.");
            }
        }

        private void dEBUGAccessME3AppendsTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ME3Package me3 = (ME3Package)pcc;
            var offset = me3.DependsOffset;
        }

        private void dEBUGEnumerateAllClassesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {
                foreach (IExportEntry exp in pcc.Exports)
                {
                    if (exp.ClassName == "Class")
                    {
                        Debug.WriteLine("Testing " + exp.Index + " " + exp.GetFullPath);
                        binaryInterpreterControl.export = exp;
                        binaryInterpreterControl.InitInterpreter();
                    }
                }
            }
        }

        private void dialogueEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {

                DialogEditor.DialogEditor dialogEditor = new DialogEditor.DialogEditor();
                dialogEditor.LoadFile(pcc.FileName);
                dialogEditor.Show();
            }
        }
    }
}