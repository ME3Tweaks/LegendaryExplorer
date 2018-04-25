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
using static ME3Explorer.Unreal.PropertyReader;
using System.Text;
using ME3Explorer.SharedUI;
using System.Threading.Tasks;
using Gibbed.IO;

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
                if (child is SplitContainer sp)
                {
                    FixSplitterWidthForDPI(sp);
                    Fix(sp.Panel1);
                    Fix(sp.Panel2);
                }
                else
                {
                    Fix(child);
                }
            }
        }

        /// <summary>
        /// Fixes the splitter components in children so they work in different DPI
        /// </summary>
        /// <param name="sp">Splitter to update</param>
        private void FixSplitterWidthForDPI(SplitContainer sp)
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
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
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
                filenameLabel.Text = "Loading " + Path.GetFileName(s);
                statusLabel.Text = "";
                Application.DoEvents();
                LoadMEPackage(s);
                interpreterControl.Pcc = pcc;
                binaryInterpreterControl.Pcc = pcc;
                bio2DAEditor1.Pcc = pcc;
                treeView1.Tag = pcc;
                RefreshView();
                InitStuff();
                filenameLabel.Text = Path.GetFileName(s);
            }
            catch (Exception e)
            {
                filenameLabel.Text = "Failed to load " + Path.GetFileName(s);
                MessageBox.Show("Error loading " + Path.GetFileName(s) + ":\n" + e.Message);
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
            if (!GetSelected(out int n))
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
            List<string> names = ClassNames.Distinct().Select(pcc.getObjectName).ToList();
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
                List<TreeNode> nodeList = new List<TreeNode>(Exports.Count + imports.Count + 1)
                {
                    new TreeNode(pcc.FileName) { Tag = true }
                };
                for (int i = 0; i < Exports.Count; i++)
                {
                    nodeList.Add(new TreeNode($"(Exp){i} : {Exports[i].ObjectName}({Exports[i].ClassName})")
                    {
                        Name = i.ToString()
                    });
                }
                for (int i = 0; i < imports.Count; i++)
                {
                    nodeList.Add(new TreeNode($"(Imp){i} : {imports[i].ObjectName}({imports[i].ClassName})")
                    {
                        Name = (-i - 1).ToString()
                    });
                }
                TreeNode node;
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

        private void ExportsViewButton_Clicked(object sender, EventArgs e)
        {
            SetView(View.Exports);
            RefreshView();
        }

        private void NamesViewButton_Clicked(object sender, EventArgs e)
        {
            SetView(View.Names);
            RefreshView();
        }

        private void ImportsViewButton_Clicked(object sender, EventArgs e)
        {
            SetView(View.Imports);
            RefreshView();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // keep disabled unless we're on the hex tab:
            if (packageEditorTabPane.SelectedTab == interpreterTab && GetSelected(out int n) && n >= 0)
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
            if (!GetSelected(out int n))
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
            statusLabel.Text = $"Class: {pcc.getExport(n).ClassName}";
            //$"Flags: 0x{pcc.getExport(n).ObjectFlags.ToString("X16")}";
            statusLabel.ToolTipText = "";
            //This is left in case someone (kinkojiro) wants it back. Otherwise in future remove it.
            //04/24/2018
            /*foreach (string row in UnrealFlags.flagdesc)
            {
                string[] t = row.Split(',');
                ulong l = ulong.Parse(t[1].Trim(), System.Globalization.NumberStyles.HexNumber);
                if ((l & pcc.getExport(n).ObjectFlags) != 0)
                {
                    statusLabel.Text += "[" + t[0].Trim() + "] ";
                    statusLabel.ToolTipText += "[" + t[0].Trim() + "] : " + t[2].Trim() + "\n";
                }
            }*/
        }

        public void UpdateStatusIm(int n)
        {
            statusLabel.Text = $"Class:{pcc.getImport(n).ClassName} Link: {pcc.getImport(n).idxLink} ";
            statusLabel.ToolTipText = "";
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
            if (!GetSelected(out int n) || n < 0)
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
            if (pcc == null || !GetSelected(out int n) || n < 0)
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
            if (pcc == null || !GetSelected(out int n) || n < 0)
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
                List<TreeNode> flattenedTree = treeView1.FlattenTreeView().ToList();
                int pos = treeView1.SelectedNode == null ? 0 : flattenedTree.IndexOf(treeView1.SelectedNode);
                pos++; //search only 1 forward
                for (int i = pos; i < flattenedTree.Count; i++)
                {
                    TreeNode node = flattenedTree[i];
                    if (node.Name == "")
                    {
                        continue;
                    }
                    int index = Convert.ToInt32(node.Name);
                    if (pcc.getObjectName(index.ToUnrealIdx()).ToLower().Contains(searchTerm))
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
            if (combo1.SelectedIndex == -1)
                return;
            int n = listBox1.SelectedIndex;
            string cls = combo1.SelectedItem as string;
            if (CurrentView == View.Exports)
            {
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
            else if (CurrentView == View.Tree)
            {
                List<TreeNode> flattenedTree = treeView1.FlattenTreeView().ToList();
                int pos = treeView1.SelectedNode == null ? 0 : flattenedTree.IndexOf(treeView1.SelectedNode);
                pos++; //search only 1 forward
                for (int i = pos; i < flattenedTree.Count; i++)
                {
                    TreeNode node = flattenedTree[i];
                    int index = Convert.ToInt32(node.Name);
                    if (pcc.getObjectClass(index.ToUnrealIdx()) == cls)
                    {
                        treeView1.SelectedNode = node;
                        break;
                    }
                }
            }

        }

        private void findClassComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                Find();
        }

        private void findClassButtonClick(object sender, EventArgs e)
        {
            Find();
        }

        public List<string> RFiles;
        private SortedDictionary<int, int> crossPCCObjectMap;

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
            if (pcc == null || !GetSelected(out int n) || n < 0)
            {
                return;
            }
            IExportEntry export = pcc.getExport(n);
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "*.bin|*.bin",
                FileName = export.ObjectName + ".bin"
            };
            if (d.ShowDialog() == DialogResult.OK)
            {
                using (FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write))
                {
                    if (export.GetProperties().Any())
                    {
                        fs.WriteBytes(export.getBinaryData());
                    }
                    else
                    {
                        fs.WriteBytes(export.Data);
                    }
                }
                MessageBox.Show("Done.");
            }
        }

        private void editInInterpreterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenInterpreter();
        }

        public void OpenInterpreter()
        {
            if (pcc == null || !GetSelected(out int n) || n < 0)
            {
                return;
            }
            InterpreterHost ip = new InterpreterHost(pcc.FileName, n)
            {
                Text = "Interpreter (Package Editor)",
                MdiParent = this.MdiParent
            };
            ip.Show();
        }

        private void interpretButton_Clicked(object sender, EventArgs e)
        {
            OpenInterpreter();
        }

        private void gotoNumberButton_Clicked(object sender, EventArgs e)
        {
            if (gotonumber.Text == "")
                return;
            if (int.TryParse(gotonumber.Text, out int n))
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
                if (int.TryParse(gotonumber.Text, out int n))
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
            if (pcc == null || !GetSelected(out int n) || n < 0)
            {
                return;
            }
            SaveFileDialog d = new SaveFileDialog { Filter = "*.BIN|*.BIN" };
            if (d.ShowDialog() == DialogResult.OK)
            {
                using (FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write))
                {
                    fs.WriteBytes(pcc.getExport(n).Data);
                }
                MessageBox.Show("Done.");
            }
        }

        private void replaceWithBINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || !GetSelected(out int n) || n < 0)
            {
                return;
            }
            OpenFileDialog d = new OpenFileDialog { Filter = "*.BIN|*.BIN" };
            if (d.ShowDialog() == DialogResult.OK)
            {
                using (FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read))
                {
                    pcc.getExport(n).Data = fs.ReadBytes((int)fs.Length);
                }
                MessageBox.Show("Done.");
            }
        }

        private void createBinaryReplaceJobFromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || !GetSelected(out int n) || n < 0)
            {
                return;
            }
            OpenFileDialog d = new OpenFileDialog { Filter = "*.bin|*.bin" };
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
            if (pcc == null || !GetSelected(out int n) || n < 0)
            {
                return;
            }
            KFreonLib.Scripting.ModMaker.ModJob mj = KFreonLib.Scripting.ModMaker.GenerateMeshModJob(null, n, pcc.FileName, pcc.getExport(n).Data);
            KFreonLib.Scripting.ModMaker.JobList.Add(mj);
            MessageBox.Show("Done");
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {
            if (statusLabel.ToolTipText != "")
                MessageBox.Show(statusLabel.ToolTipText);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            int NameIdx, ClassIdx, LinkIdx, IndexIdx, ArchetypeIdx;
            int off = pcc.ImportCount;

            if (pcc == null || !GetSelected(out int n) ||
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

        private void TreeView_Button_Click(object sender, EventArgs e)
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
            if (pcc == null || !GetSelected(out int n))
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
            if (GetSelected(out int n))
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
            if (GetSelected(out int n))
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
            crossPCCObjectMap = new SortedDictionary<int, int>();
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
                    crossPCCObjectMap = null;

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

            //Set class. This will only work if the class is an import, as we can't reliably pull in exports without lots of other stuff.
            if (ex.idxClass < 0)
            {
                //The class of the export we are importing is an import. We should attempt to relink this.
                ImportEntry portingFromClassImport = importpcc.getImport(Math.Abs(ex.idxClass) - 1);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, importpcc, pcc);
                classValue = newClassImport.UIndex;
            }

            //Check archetype.
            if (ex.idxArchtype < 0)
            {
                ImportEntry portingFromClassImport = importpcc.getImport(Math.Abs(ex.idxArchtype) - 1);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, importpcc, pcc);
                archetype = newClassImport.UIndex;
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

            crossPCCObjectMap[n] = pcc.ExportCount - 1; //0 based.
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