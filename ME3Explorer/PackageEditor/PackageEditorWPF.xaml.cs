using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for PackageEditorWPF.xaml
    /// </summary>
    public partial class PackageEditorWPF : WPFBase
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

        public static readonly string PackageEditorDataFolder = System.IO.Path.Combine(App.AppDataFolder, @"PackageEditor\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        private string currentFile;
        private List<int> ClassNames;

        public PackageEditorWPF()
        {
            CurrentView = View.Exports;
            InitializeComponent();
        }

        private void OpenFile_Clicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                try
                {
                    LoadFile(d.FileName);
                    //AddRecent(d.FileName, false);
                    //SaveRecentList();
                    //RefreshRecent(true, RFiles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }

        private void LoadFile(string s)
        {
            try
            {
                currentFile = s;
                StatusBar_LeftMostText.Text = "Loading " + System.IO.Path.GetFileName(s);
                LoadMEPackage(s);

                /*interpreterControl.Pcc = pcc;
                binaryInterpreterControl.Pcc = pcc;
                bio2DAEditor1.Pcc = pcc;
                treeView1.Tag = pcc;*/
                RefreshView();
                InitStuff();
                StatusBar_LeftMostText.Text = System.IO.Path.GetFileName(s);
            }
            catch (Exception e)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + System.IO.Path.GetFileName(s);
                MessageBox.Show("Error loading " + System.IO.Path.GetFileName(s) + ":\n" + e.Message);
            }
        }

        private void RefreshView()
        {
            if (pcc == null)
            {
                return;
            }
            //listBox1.BeginUpdate();
            //treeView1.BeginUpdate();
            LeftSide_ListView.ItemsSource = null;
            LeftSide_ListView.Items.Clear();
            IReadOnlyList<ImportEntry> imports = pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            if (CurrentView == View.Names)
            {
                LeftSide_ListView.ItemsSource = pcc.Names;
            }

            if (CurrentView == View.Imports)
            {
                List<string> importsList = new List<string>();
                for (int i = 0; i < imports.Count; i++)
                {
                    string importStr = i + " (0x" + (pcc.ImportOffset + (i * ImportEntry.byteSize)).ToString("X4") + "): (" + imports[i].PackageFile + ") ";
                    if (imports[i].PackageFullName != "Class" && imports[i].PackageFullName != "Package")
                    {
                        importStr += imports[i].PackageFullName + ".";
                    }
                    importStr += imports[i].ObjectName;
                    importsList.Add(importStr);
                }
                LeftSide_ListView.Items.Add(importsList);
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
                LeftSide_ListView.ItemsSource = exps.ToArray();
            }
            if (CurrentView == View.Tree)
            {
                LeftSide_ListView.Visibility = Visibility.Collapsed;
                LeftSide_TreeView.Visibility = Visibility.Visible;
                LeftSide_TreeView.ItemsSource = null;
                LeftSide_TreeView.Items.Clear();

                int importsOffset = Exports.Count;
                int link;
                List<TreeViewItem> nodeList = new List<TreeViewItem>(Exports.Count + imports.Count + 1)
                {
                    new TreeViewItem() { Header = pcc.FileName, Tag = true }
                };


                for (int i = 0; i < Exports.Count; i++)
                {
                    nodeList.Add(new TreeViewItem()
                    {
                        Header = $"(Exp) {i} : {Exports[i].ObjectName}({Exports[i].ClassName})",
                        Name = $"_{i}" //must start letter or _
                    });
                }

                for (int i = 0; i < imports.Count; i++)
                {
                    nodeList.Add(new TreeViewItem()
                    {
                        Header = $"(Imp) {i} : {imports[i].ObjectName}({imports[i].ClassName})",
                        Name = $"_n{i + 1}" //must start letter or _
                    });
                }

                TreeViewItem node;
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
                        nodeList[link].Items.Add(node);
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
                        nodeList[link].Items.Add(node);
                        node = nodeList[link];
                    }
                }
                LeftSide_TreeView.Items.Add(nodeList[0]);
                nodeList[0].IsExpanded = true;
                /*LeftSide_TreeView.Items[0].Expand();
                */
            }
            else
            {
                LeftSide_ListView.Visibility = Visibility.Visible;
                LeftSide_TreeView.Visibility = Visibility.Collapsed;
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
            List<string> names = ClassNames.Distinct().Select(pcc.getObjectName).ToList();
            names.Sort();
            ClassDropdown_Combobox.Items.Clear();
            ClassDropdown_Combobox.ItemsSource = names.ToArray();
        }


        private void TreeView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Tree);
            RefreshView();
        }
        private void NamesView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Names);
            RefreshView();
        }
        private void ImportsView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Imports);
            RefreshView();
        }
        private void ExportsView_Click(object sender, RoutedEventArgs e)
        {
            SetView(View.Exports);
            RefreshView();
        }

        void SetView(View n)
        {
            CurrentView = n;
            /*switch (n)
            {
                case View.Names:
                    Names_Button.Checked = true;
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
            }*/
        }

        public void PreviewInfo(int n)
        {
            if (n >= 0)
            {
                try
                {
                    /*infoHeaderBox.Text = "Export Header";
                    superclassTextBox.Visible = superclassLabel.Visible = true;
                    archetypeBox.Visible = label6.Visible = true;
                    indexBox.Visible = label5.Visible = true;
                    flagsBox.Visible = label11.Visible = false;
                    infoExportDataBox.Visible = true;*/
                    IExportEntry exportEntry = pcc.getExport(n);
                    InfoTab_Objectname_TextBox.Text = exportEntry.ObjectName;
                    //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                    //
                    if (exportEntry.idxClass != 0)
                    {
                        IEntry _class = pcc.getEntry(exportEntry.idxClass);
                        InfoTab_Class_TextBox.Text = _class.ClassName;
                    }
                    else
                    {
                        InfoTab_Class_TextBox.Text = "Class";
                    }
                    //classNameBox.Text = exportEntry.ClassName; //this seems to override the code directly above?
                    InfoTab_Superclass_TextBox.Text = exportEntry.ClassParent;
                    InfoTab_Packagename_TextBox.Text = exportEntry.PackageFullName;
                    InfoTab_Headersize_TextBox.Text = exportEntry.header.Length + " bytes";
                    InfoTab_Index_TextBox.Text = exportEntry.indexValue.ToString();
                    InfoTab_Archetypename_TextBox.Text = exportEntry.ArchtypeName;

                    if (exportEntry.idxArchtype != 0)
                    {
                        IEntry archetype = pcc.getEntry(exportEntry.idxArchtype);
                        InfoTab_Archetypename_TextBox.Text = archetype.PackageFullName + "." + archetype.ObjectName;
                        InfoTab_Archetypename_TextBox.Text += " (" + (exportEntry.idxArchtype < 0 ? "imported" : "local") + " class) " + exportEntry.idxArchtype;
                    }
                    InfoTab_Flags_TextBox.Text = "0x" + exportEntry.ObjectFlags.ToString("X16");
                    //textBox7.Text = exportEntry.DataSize + " bytes";
                    //textBox8.Text = "0x" + exportEntry.DataOffset.ToString("X8");
                    //textBox9.Text = exportEntry.DataOffset.ToString();
                }
                catch (Exception e)
                {
                    MessageBox.Show("An error occured while attempting to read the header for this export. This indicates there is likely something wrong with the header or its parent header.");
                }
            }
            else
            {
                /* n = -n - 1;
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
                 headerSizeBox.Text = ImportEntry.byteSize + " bytes";*/
            }
        }

        private bool GetSelected(out int n)
        {
            if (CurrentView == View.Tree && LeftSide_TreeView.SelectedItem != null && ((TreeViewItem)LeftSide_TreeView.SelectedItem).Name.StartsWith("_"))
            {
                string name = ((TreeViewItem)LeftSide_TreeView.SelectedItem).Name.Substring(1); //get rid of _
                if (name.StartsWith("n"))
                {
                    //its negative
                    name = $"-{name.Substring(1)}";
                }
                n = Convert.ToInt32(name);
                return true;
            }
            else if (CurrentView == View.Exports && LeftSide_ListView.SelectedItem != null)
            {
                n = LeftSide_ListView.SelectedIndex;
                return true;
            }
            else if (CurrentView == View.Imports && LeftSide_ListView.SelectedItem != null)
            {
                n = -LeftSide_ListView.SelectedIndex - 1;
                return true;
            }
            else
            {
                n = 0;
                return false;
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Tree view selected item changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftSide_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Preview();
        }

        /// <summary>
        /// Listbox selected item changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftSide_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            Preview();
        }

        private void Preview()
        {
            if (!GetSelected(out int n))
            {
                return;
            }

            if (CurrentView == View.Imports || CurrentView == View.Exports || CurrentView == View.Tree)
            {
                //tabControl1_SelectedIndexChanged(null, null);
                PreviewInfo(n);
                Info_HeaderRaw_Hexbox.Stream = new System.IO.MemoryStream(pcc.getEntry(n.ToUnrealIdx()).header);
                //RefreshMetaData();
                //export
                if (n >= 0)
                {
                    /*PreviewProps(n);
                     if (!packageEditorTabPane.TabPages.ContainsKey(nameof(propertiesTab)))
                     {
                         packageEditorTabPane.TabPages.Insert(0, propertiesTab);
                     }
                     if (!packageEditorTabPane.TabPages.ContainsKey(nameof(interpreterTab)))
                     {
                         packageEditorTabPane.TabPages.Insert(1, interpreterTab);
                     }*/

                    IExportEntry exportEntry = pcc.getExport(n);
                    Script_Tab.Visibility = exportEntry.ClassName == "Function" ? Visibility.Visible : Visibility.Collapsed;
                    if (exportEntry.ClassName == "Function")
                    {
                        /*
                                                if (!Script_Tab.TabPages.ContainsKey(nameof(scriptTab)))
                                                {
                                                    packageEditorTabPane.TabPages.Add(scriptTab);
                                                }*/
                        if (pcc.Game == MEGame.ME3)
                        {
                            Function func = new Function(exportEntry.Data, pcc);
                            Script_TextBox.Text = func.ToRawText();
                        }
                        else if (pcc.Game == MEGame.ME1)
                        {
                            ME1Explorer.Unreal.Classes.Function func = new ME1Explorer.Unreal.Classes.Function(exportEntry.Data, pcc as ME1Package);
                            try
                            {
                                Script_TextBox.Text = func.ToRawText();
                            }
                            catch (Exception e)
                            {
                                Script_TextBox.Text = "Error parsing function: " + e.Message;
                            }
                        }
                        else
                        {
                            Script_TextBox.Text = "Parsing UnrealScript Functions for this game is not supported.";
                        }
                    }
                }
                /*
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

                //headerRawHexBox.ByteProvider = new DynamicByteProvider(exportEntry.header);
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
            }*/
            }
        }


    }
}