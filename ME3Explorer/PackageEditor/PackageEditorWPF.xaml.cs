using Be.Windows.Forms;
using ME3Explorer.CurveEd;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
using System.Windows.Threading;
using Xceed.Wpf.Toolkit.Primitives;
using static ME3Explorer.Unreal.UnrealFlags;

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
        public List<string> RFiles;
        private string currentFile;
        private List<int> ClassNames;
        //private HexBox Header_Hexbox;
        private bool loadingNewData = false;
        private HexBox Header_Hexbox;
        private IEntry CurrentlyLoadedEntry;

        private const int HEADER_OFFSET_EXP_IDXCLASS = 0;
        private const int HEADER_OFFSET_EXP_IDXSUPERCLASS = 4;
        private const int HEADER_OFFSET_EXP_IDXLINK = 8;
        private const int HEADER_OFFSET_EXP_IDXOBJECTNAME = 12;
        private const int HEADER_OFFSET_EXP_INDEXVALUE = 16;
        private const int HEADER_OFFSET_EXP_IDXARCHETYPE = 20;
        private const int HEADER_OFFSET_EXP_OBJECTFLAGS = 24;

        private const int HEADER_OFFSET_IMP_IDXCLASSNAME = 8;
        private const int HEADER_OFFSET_IMP_IDXLINK = 12;
        private const int HEADER_OFFSET_IMP_IDXOBJECTNAME = 20;
        private const int HEADER_OFFSET_IMP_IDXPACKAGEFILE = 0;
        private bool Visible_ObjectNameRow { get; set; }
        private List<AdvancedTreeViewItem<TreeViewItem>> AllTreeViewNodes = new List<AdvancedTreeViewItem<TreeViewItem>>();

        public PackageEditorWPF()
        {
            CurrentView = View.Tree;
            InitializeComponent();

            LoadRecentList();
            RefreshRecent(false);
        }

        private void OpenFile_Clicked(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void OpenFile()
        {

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

        private void LoadRecentList()
        {
            Recents_MenuItem.IsEnabled = false;
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

                //This code can be removed when non-WPF package editor is removed.
                var forms = System.Windows.Forms.Application.OpenForms;
                foreach (System.Windows.Forms.Form form in forms)
                {
                    if (form is PackageEditor) //it will never be "this"
                    {
                        ((PackageEditor)form).RefreshRecent(false, RFiles);
                    }
                }
                foreach (var form in App.Current.Windows)
                {
                    if (form is PackageEditorWPF && this != form)
                    {
                        ((PackageEditorWPF)form).RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }
            Recents_MenuItem.IsEnabled = true;


            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((MenuItem)sender).Tag.ToString();
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
            Recents_MenuItem.IsEnabled = true;
        }

        private void RefreshView()
        {
            if (pcc == null)
            {
                return;
            }
            //listBox1.BeginUpdate();
            //treeView1.BeginUpdate();
            ClearList(LeftSide_ListView);
            IReadOnlyList<ImportEntry> imports = pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            if (CurrentView == View.Names)
            {
                LeftSide_ListView.ItemsSource = pcc.Names;
            }

            if (CurrentView == View.Imports)
            {
                List<string> importsList = new List<string>();
                int padding = imports.Count.ToString().Length;

                for (int i = 0; i < imports.Count; i++)
                {
                    //" (0x" + (pcc.ImportOffset + (i * ImportEntry.byteSize)).ToString("X4") + ")\
                    string importStr = $"{ i.ToString().PadLeft(padding, '0')}: {imports[i].GetFullPath}";
                    /*if (imports[i].PackageFullName != "Class" && imports[i].PackageFullName != "Package")
                    {
                        importStr += imports[i].PackageFullName + ".";
                    }
                    importStr += imports[i].ObjectName;*/
                    importsList.Add(importStr);
                }
                LeftSide_ListView.ItemsSource = importsList;
            }

            if (CurrentView == View.Exports)
            {
                List<string> exps = new List<string>(Exports.Count);
                int padding = Exports.Count.ToString().Length;
                for (int i = 0; i < Exports.Count; i++)
                {
                    string s = $"{i.ToString().PadLeft(padding, '0')}: ";
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
                LeftSide_ListView.ItemsSource = exps;
            }
            if (CurrentView == View.Tree)
            {
                LeftSide_ListView.Visibility = Visibility.Collapsed;
                LeftSide_TreeView.Visibility = Visibility.Visible;
                ClearList(LeftSide_TreeView);
                int importsOffset = Exports.Count;
                int link;
                AllTreeViewNodes = new List<AdvancedTreeViewItem<TreeViewItem>>(Exports.Count + imports.Count + 1)
                {
                    new AdvancedTreeViewItem<TreeViewItem>() { Header = pcc.FileName, Tag = true, Name="Root" }
                };


                for (int i = 0; i < Exports.Count; i++)
                {
                    AllTreeViewNodes.Add(new AdvancedTreeViewItem<TreeViewItem>()
                    {
                        Header = $"(Exp) {i + 1} : {Exports[i].ObjectName}({Exports[i].ClassName})",
                        Name = $"_{i + 1}", //must start letter or _
                        Foreground = Brushes.Black,
                        Background = (Exports[i].DataChanged || Exports[i].HeaderChanged) ? Brushes.Yellow : null
                    });
                }

                for (int i = 0; i < imports.Count; i++)
                {
                    AllTreeViewNodes.Add(new AdvancedTreeViewItem<TreeViewItem>()
                    {
                        Header = $"(Imp) {-i - 1} : {imports[i].ObjectName}({imports[i].ClassName})",
                        Name = $"_n{i + 1}", //must start letter or _,
                        Foreground = Brushes.Gray,
                        Background = (imports[i].HeaderChanged) ? Brushes.Yellow : null
                    });
                }

                AdvancedTreeViewItem<TreeViewItem> node;
                int curIndex;
                for (int i = 1; i <= Exports.Count; i++)
                {
                    node = AllTreeViewNodes[i];
                    curIndex = i;
                    while (node.Tag as bool? != true)
                    {
                        node.Tag = true;
                        //Debug.WriteLine(curIndex);
                        curIndex = pcc.getEntry(curIndex).idxLink;
                        link = curIndex >= 0 ? curIndex : (-curIndex + importsOffset);
                        AllTreeViewNodes[link].Items.Add(node);
                        node.ParentNodeValue = AllTreeViewNodes[link];
                        node = AllTreeViewNodes[link];
                    }
                }

                for (int i = 1; i <= imports.Count; i++)
                {
                    node = AllTreeViewNodes[i + importsOffset];
                    curIndex = -i;
                    while (node.Tag as bool? != true)
                    {
                        node.Tag = true;
                        curIndex = pcc.getEntry(curIndex).idxLink;
                        link = curIndex >= 0 ? curIndex : (-curIndex + importsOffset);
                        AllTreeViewNodes[link].Items.Add(node);
                        node.ParentNodeValue = AllTreeViewNodes[link];
                        node = AllTreeViewNodes[link];
                    }
                }
                LeftSide_TreeView.Items.Add(AllTreeViewNodes[0]);
                AllTreeViewNodes[0].IsExpanded = true;
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
            ComparePackageMenuItem.IsEnabled = true;
            ClassNames = new List<int>();
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            for (int i = 0; i < Exports.Count; i++)
            {
                ClassNames.Add(Exports[i].idxClass); //This can probably be linq'd
            }
            List<string> names = ClassNames.Distinct().Select(pcc.getObjectName).ToList();
            names.Sort();
            ClearList(ClassDropdown_Combobox);
            ClassDropdown_Combobox.ItemsSource = names.ToArray();
            InfoTab_Objectname_ComboBox.ItemsSource = pcc.Names;
            InfoTab_ImpClass_ComboBox.ItemsSource = pcc.Names;
            InfoTab_PackageFile_ComboBox.ItemsSource = pcc.Names;
        }

        /// <summary>
        /// Clears the itemsource or items property of the passed in control.
        /// </summary>
        /// <param name="control">ItemsControl to clear all entries from.</param>
        private void ClearList(ItemsControl control)
        {
            if (control.ItemsSource != null)
            {
                control.ItemsSource = null;
            }
            else
            {
                control.Items.Clear();
            }
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
            IReadOnlyList<ImportEntry> imports = pcc.Imports;
            List<string> Classes = new List<string>();
            for (int i = imports.Count - 1; i >= 0; i--)
            {
                Classes.Add($"{-i + 1}: {imports[i].GetFullPath}");
            }
            Classes.Add("0 : Class");
            int count = 1;
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            foreach (IExportEntry exp in Exports)
            {
                Classes.Add($"{count++}: {exp.GetFullPath}");
            }
            InfoTab_PackageLink_ComboBox.ItemsSource = Classes;

            if (n > 0)
            {
                n--; //convert to 0 based indexing
                try
                {
                    Row_Archetype.Height = new GridLength(24);
                    Row_ExpClass.Height = new GridLength(24);
                    Row_Superclass.Height = new GridLength(24);
                    Row_ImpClass.Height = new GridLength(0);
                    Row_ExpClass.Height = new GridLength(24);
                    Row_Packagefile.Height = new GridLength(0);
                    Row_ObjectFlags.Height = new GridLength(24);
                    Row_ExportDataSize.Height = new GridLength(24);
                    Row_ExportDataOffsetDec.Height = new GridLength(24);
                    Row_ExportDataOffsetHex.Height = new GridLength(24);
                    InfoTab_Link_TextBlock.Text = "0x08 Link:";
                    InfoTab_ObjectName_TextBlock.Text = "0x0C Object name:";

                    IExportEntry exportEntry = pcc.getExport(n);
                    InfoTab_Objectname_ComboBox.SelectedItem = exportEntry.ObjectName;

                    if (exportEntry.idxClass != 0)
                    {
                        //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                        InfoTab_Class_ComboBox.ItemsSource = Classes;
                        InfoTab_Class_ComboBox.SelectedIndex = exportEntry.idxClass + imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Class_ComboBox.SelectedIndex = imports.Count; //Class, 0
                    }
                    InfoTab_Superclass_ComboBox.ItemsSource = Classes;
                    if (exportEntry.idxClassParent != 0)
                    {
                        InfoTab_Superclass_ComboBox.SelectedIndex = exportEntry.idxClassParent + imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Superclass_ComboBox.SelectedIndex = imports.Count; //Class, 0
                    }

                    if (exportEntry.idxLink != 0)
                    {
                        InfoTab_PackageLink_ComboBox.SelectedIndex = exportEntry.idxLink + imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_PackageLink_ComboBox.SelectedIndex = imports.Count; //Class, 0
                    }
                    InfoTab_Headersize_TextBox.Text = exportEntry.Header.Length + " bytes";
                    InfoTab_ObjectnameIndex_TextBox.Text = BitConverter.ToInt32(exportEntry.Header, HEADER_OFFSET_EXP_IDXOBJECTNAME + 4).ToString();
                    InfoTab_Archetype_ComboBox.ItemsSource = Classes;
                    if (exportEntry.idxArchtype != 0)
                    {
                        InfoTab_Archetype_ComboBox.SelectedIndex = exportEntry.idxArchtype + imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Archetype_ComboBox.SelectedIndex = imports.Count; //Class, 0
                    }
                    var flagsList = Enum.GetValues(typeof(EObjectFlags)).Cast<EObjectFlags>().Distinct().ToList();
                    //Don't even get me started on how dumb it is that SelectedItems is read only...
                    string selectedFlags = "";
                    foreach (EObjectFlags flag in flagsList)
                    {
                        bool selected = (exportEntry.ObjectFlags & (ulong)flag) != 0;
                        if (selected)
                        {
                            if (selectedFlags != "")
                            {
                                selectedFlags += " ";
                            }
                            selectedFlags += flag;
                        }
                    }

                    InfoTab_Flags_ComboBox.ItemsSource = flagsList;
                    InfoTab_Flags_ComboBox.SelectedValue = selectedFlags;

                    InfoTab_ExportDataSize_TextBox.Text = exportEntry.DataSize + " bytes";
                    InfoTab_ExportOffsetHex_TextBox.Text = "0x" + exportEntry.DataOffset.ToString("X8");
                    InfoTab_ExportOffsetDec_TextBox.Text = exportEntry.DataOffset.ToString();
                }
                catch (Exception e)
                {
                    MessageBox.Show("An error occured while attempting to read the header for this export. This indicates there is likely something wrong with the header or its parent header.\n\n" + e.Message);
                }
            }
            else
            {
                n = -n - 1; //convert to 0 based indexing (imports list)
                ImportEntry importEntry = pcc.getImport(n);
                InfoTab_Headersize_TextBox.Text = importEntry.Header.Length + " bytes";
                Row_Archetype.Height = new GridLength(0);
                Row_ExpClass.Height = new GridLength(0);
                Row_ImpClass.Height = new GridLength(24);
                Row_ExportDataSize.Height = new GridLength(0);
                Row_ExportDataOffsetDec.Height = new GridLength(0);
                Row_ExportDataOffsetHex.Height = new GridLength(0);

                Row_Superclass.Height = new GridLength(0);
                Row_ObjectFlags.Height = new GridLength(0);
                Row_Packagefile.Height = new GridLength(24);
                InfoTab_Link_TextBlock.Text = "0x0C Link:";
                InfoTab_ObjectName_TextBlock.Text = "0x14 Object name:";

                InfoTab_Objectname_ComboBox.SelectedItem = importEntry.ObjectName;
                InfoTab_ImpClass_ComboBox.SelectedItem = importEntry.ClassName;
                if (importEntry.idxLink != 0)
                {
                    InfoTab_PackageLink_ComboBox.SelectedIndex = importEntry.idxLink + imports.Count; //make positive
                }
                else
                {
                    InfoTab_PackageLink_ComboBox.SelectedIndex = imports.Count; //Class, 0
                }

                InfoTab_PackageFile_ComboBox.SelectedItem = importEntry.PackageFile;
                InfoTab_ObjectnameIndex_TextBox.Text = BitConverter.ToInt32(importEntry.Header, HEADER_OFFSET_IMP_IDXOBJECTNAME + 4).ToString();
                /*infoHeaderBox.Text = "Import Header";
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

        public void RefreshExportChangedStatus()
        {
            if (pcc != null)
            {
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    if (pcc.Exports[i].DataChanged)
                    {
                        Debug.WriteLine("DataChanged for " + i + " " + pcc.Exports[i].GetFullPath);
                    }
                    object o = AllTreeViewNodes[i + 1];
                    AllTreeViewNodes[i + 1].Background = (pcc.Exports[i].DataChanged || pcc.Exports[i].HeaderChanged) ? Brushes.Yellow : null;
                    AllTreeViewNodes[i + 1].ToolTip = (pcc.Exports[i].DataChanged || pcc.Exports[i].HeaderChanged) ? "This entry has been modified but has not been commited to disk yet" : null;

                }
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
            List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool importChanges = changes.Contains(PackageChange.Import) || changes.Contains(PackageChange.ImportAdd);
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);
            int n = 0;
            bool hasSelection = GetSelected(out n);
            if (CurrentView == View.Names && changes.Contains(PackageChange.Names))
            {
                //int scrollTo = LeftSide_ListView..TopIndex + 1;
                //int selected = listBox1.SelectedIndex;
                RefreshView();
                //listBox1.SelectedIndex = selected;
                //listBox1.TopIndex = scrollTo;
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
                     updates.Contains(new PackageUpdate { index = n - 1, change = PackageChange.ExportData }))
            {
                //interpreterControl.memory = pcc.getExport(n).Data;
                //interpreterControl.RefreshMem();
                //binaryInterpreterControl.memory = pcc.getExport(n).Data;
                //binaryInterpreterControl.RefreshMem();
                Preview(true);
            }
            RefreshExportChangedStatus();
        }


        private delegate void NoArgDelegate();
        /// <summary>
        /// Tree view selected item changed. This runs in a delegate due to how multithread bubble-up items work with treeview.
        /// Without this delegate, the item selected will randomly be a parent item instead.
        /// From https://www.codeproject.com/Tips/208896/WPF-TreeView-SelectedItemChanged-called-twice
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftSide_SelectedItemChanged(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
        (NoArgDelegate)delegate
        {
            loadingNewData = true;

            Preview();
            loadingNewData = false;
        });
        }

        /// <summary>
        /// Listbox selected item changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftSide_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            loadingNewData = true;
            Preview();
            e.Handled = true;
            loadingNewData = false;
        }

        private void Preview(bool isRefresh = false)
        {
            if (!GetSelected(out int n))
            {
                return;
            }
            Debug.WriteLine("New selection: " + n);

            if (CurrentView == View.Imports || CurrentView == View.Exports || CurrentView == View.Tree)
            {
                //tabControl1_SelectedIndexChanged(null, null);
                PreviewInfo(n);
                //Info_HeaderRaw_Hexbox.Stream = new System.IO.MemoryStream(pcc.getEntry(n.ToUnrealIdx()).header);
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

                    IExportEntry exportEntry = pcc.getExport(n - 1);
                    CurrentlyLoadedEntry = exportEntry;
                    Header_Hexbox.ByteProvider = new DynamicByteProvider(CurrentlyLoadedEntry.Header);
                    Header_Hexbox.ByteProvider.Changed += InfoTab_Header_ByteProvider_InternalChanged;
                    Info_Header_UnsavedChanges.Visibility = Visibility.Hidden;
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

                    if (Pcc.Game == MEGame.ME3)
                    {

                        //Curve Editor
                        var props = exportEntry.GetProperties();
                        bool showCurveEd = false;
                        foreach (var prop in props)
                        {
                            if (prop is StructProperty structProp)
                            {
                                if (Enum.TryParse(structProp.StructType, out CurveType _))
                                {
                                    showCurveEd = true;
                                    break;
                                }
                            }
                        }

                        if (showCurveEd)
                        {
                            CurveEditor_Tab.Visibility = Visibility.Visible;
                            CurveEditor_SubModule.LoadExport(exportEntry);
                        }
                        else
                        {
                            //Change to interpreter if this tab is being hidden but is currently visible.
                            //Interpreter is always available
                            if (EditorTabs.SelectedItem == CurveEditor_Tab)
                            {
                                EditorTabs.SelectedItem = Interpreter_Tab;
                            }
                            CurveEditor_Tab.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        CurveEditor_Tab.Visibility = Visibility.Collapsed;
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

                    headerRawHexBox.ByteProvider = new DynamicByteProvider(exportEntry.header);*/
                    if (!isRefresh)
                    {
                        InterpreterTab_Interpreter.loadNewExport(exportEntry);
                        Interpreter_Tab.Visibility = Visibility.Visible;

                        //interpreterControl.export = exportEntry;
                        //interpreterControl.InitInterpreter();

                        if (BinaryInterpreter.ParsableBinaryClasses.Contains(exportEntry.ClassName))
                        {
                            if (exportEntry.ClassName == "Class" && exportEntry.ObjectName.StartsWith("Default__"))
                            {
                                //do nothing, this class is not actually a class.
                                BinaryInterpreter_Tab.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                //binaryInterpreterControl.export = exportEntry;
                                //binaryInterpreterControl.InitInterpreter();
                                BinaryInterpreter_Tab.Visibility = Visibility.Collapsed;

                            }
                        }
                        if (Bio2DAEditor.ParsableBinaryClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith("Default__"))
                        {
                            Bio2DAViewer_Tab.Visibility = Visibility.Visible;
                            //bio2DAEditor1.export = exportEntry;
                            //bio2DAEditor1.InitInterpreter();
                        }
                        else
                        {
                            Bio2DAViewer_Tab.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                //import
                else
                {
                    Visible_ObjectNameRow = false;
                    ImportEntry importEntry = pcc.getImport(-n - 1);
                    CurrentlyLoadedEntry = importEntry;
                    Header_Hexbox.ByteProvider = new DynamicByteProvider(CurrentlyLoadedEntry.Header);
                    Header_Hexbox.ByteProvider.Changed += InfoTab_Header_ByteProvider_InternalChanged;
                    Script_Tab.Visibility = BinaryInterpreter_Tab.Visibility = Interpreter_Tab.Visibility = Bio2DAViewer_Tab.Visibility = Visibility.Collapsed;
                    Metadata_Tab.IsSelected = true;
                    PreviewInfo(n);
                    /*   n = -n - 1;
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
                   */
                }
            }
        }

        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
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

        private void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //save();
            foreach (var item in AllTreeViewNodes)
            {
                item.Background = null;
                item.ToolTip = null;
            }
        }

        private void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //saveAs();

            foreach (var item in AllTreeViewNodes)
            {
                item.Background = null;
                item.ToolTip = null;
            }
        }

        private void PackageEditorWPF_Loaded(object sender, RoutedEventArgs e)
        {
            Header_Hexbox = (HexBox)Header_Hexbox_Host.Child;
            InterpreterTab_Interpreter.SaveHexChange_Button.Click += Interpreter_SaveHexChanges_Clicked;
            //            // Create the interop host control.
            //            System.Windows.Forms.Integration.WindowsFormsHost host =
            //                new System.Windows.Forms.Integration.WindowsFormsHost();

            //            // Create the MaskedTextBox control.
            //            Header_Hexbox = new Be.Windows.Forms.HexBox();
            //            this.Header_Hexbox.BoldFont = null;
            //            this.Header_Hexbox.BytesPerLine = 16;
            ////            this.Header_Hexbox.Dock = System.Windows.Forms.DockStyle.Fill;
            //            this.Header_Hexbox.Dock = System.Windows.Forms.DockStyle.Left;

            //            this.Header_Hexbox.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            //            this.Header_Hexbox.LineInfoForeColor = System.Drawing.Color.Empty;
            //            this.Header_Hexbox.LineInfoVisible = true;
            //            this.Header_Hexbox.ColumnInfoVisible = true;
            //            this.Header_Hexbox.Location = new System.Drawing.Point(0, 0);
            //            this.Header_Hexbox.MinBytesPerLine = 4;
            //            this.Header_Hexbox.Name = "Header_Hexbox";
            //            this.Header_Hexbox.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            //            this.Header_Hexbox.StringViewVisible = true;
            //            this.Header_Hexbox.TabIndex = 0;
            //            this.Header_Hexbox.VScrollBarVisible = true;
            //            //this.Header_Hexbox.SelectionStartChanged += new System.EventHandler(this.Header_Hexbox_SelectionChanged);
            //            //this.Header_Hexbox.SelectionLengthChanged += new System.EventHandler(this.Header_Hexbox_SelectionChanged);

            //            host.Child = Header_Hexbox;

            //            // Add the interop host control to the Grid
            //            // control's collection of child controls.
            //            WpfHosted_BeHexbox.Children.Add(host);
        }

        private void Interpreter_SaveHexChanges_Clicked(object sender, RoutedEventArgs e)
        {
            if (InterpreterTab_Interpreter.CurrentLoadedExport != null)
            {
                //This method only listens for save event when clicking save hex changes button, it does not execute the actual save
                var nodeName = InterpreterTab_Interpreter.CurrentLoadedExport.UIndex.ToString().Replace("-", "n");
                var updatedNode = AllTreeViewNodes.FirstOrDefault(s => s.Name.Substring(1) == nodeName);
                if (updatedNode != null)
                {
                    updatedNode.Background = Brushes.Yellow;
                    updatedNode.ToolTip = "This entry has been modified but has not been commited to disk yet";
                }
            }
        }

        private void Info_ClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedClassIndex = InfoTab_Class_ComboBox.SelectedIndex;
                var unrealIndex = selectedClassIndex - pcc.ImportCount;
                Header_Hexbox.ByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXCLASS, BitConverter.GetBytes(unrealIndex));
            }
        }

        private void InfoTab_Header_ByteProvider_InternalChanged(object sender, EventArgs e)
        {
            Info_Header_UnsavedChanges.Visibility = Header_Hexbox.ByteProvider.HasChanges() ? Visibility.Visible : Visibility.Hidden;
            Header_Hexbox.Refresh();
        }

        private void Info_HeaderHexSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            MemoryStream m = new MemoryStream();
            IByteProvider provider = Header_Hexbox.ByteProvider;
            for (int i = 0; i < provider.Length; i++)
                m.WriteByte(provider.ReadByte(i));
            CurrentlyLoadedEntry.Header = m.ToArray();
            if (Header_Hexbox.ByteProvider != null)
            {
                Header_Hexbox.ByteProvider.ApplyChanges();
            }
            var nodeName = CurrentlyLoadedEntry.UIndex.ToString().Replace("-", "n");
            var updatedNode = AllTreeViewNodes.FirstOrDefault(s => s.Name.Substring(1) == nodeName);
            if (updatedNode != null)
            {
                updatedNode.Background = Brushes.Yellow;
                updatedNode.ToolTip = "This entry has been modified but has not been commited to disk yet";
            }
            Info_Header_UnsavedChanges.Visibility = Header_Hexbox.ByteProvider.HasChanges() ? Visibility.Visible : Visibility.Hidden;
        }

        private void Info_PackageLinkClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedImpExp = InfoTab_PackageLink_ComboBox.SelectedIndex;
                var unrealIndex = selectedImpExp - pcc.ImportCount;
                Header_Hexbox.ByteProvider.WriteBytes(CurrentlyLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXLINK : HEADER_OFFSET_IMP_IDXLINK, BitConverter.GetBytes(unrealIndex));
            }
        }

        private void Info_SuperClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedClassIndex = InfoTab_Superclass_ComboBox.SelectedIndex;
                var unrealIndex = selectedClassIndex - pcc.ImportCount;
                Header_Hexbox.ByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXSUPERCLASS, BitConverter.GetBytes(unrealIndex));
            }
        }

        private void Info_ObjectNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedNameIndex = InfoTab_Objectname_ComboBox.SelectedIndex;
                Header_Hexbox.ByteProvider.WriteBytes(CurrentlyLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME : HEADER_OFFSET_IMP_IDXOBJECTNAME, BitConverter.GetBytes(selectedNameIndex));
            }
        }

        private void Info_IndexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                int x;
                if (int.TryParse(InfoTab_ObjectnameIndex_TextBox.Text, out x))
                {
                    Header_Hexbox.ByteProvider.WriteBytes(CurrentlyLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME + 4 : HEADER_OFFSET_IMP_IDXOBJECTNAME + 4, BitConverter.GetBytes(x));
                }
            }
        }

        private void Info_ArchetypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedArchetTypeIndex = InfoTab_Archetype_ComboBox.SelectedIndex;
                var unrealIndex = selectedArchetTypeIndex - pcc.ImportCount;
                Header_Hexbox.ByteProvider.WriteBytes(HEADER_OFFSET_EXP_IDXARCHETYPE, BitConverter.GetBytes(selectedArchetTypeIndex));
            }
        }

        private void Info_PackageFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedNameIndex = InfoTab_PackageFile_ComboBox.SelectedIndex;
                Header_Hexbox.ByteProvider.WriteBytes(HEADER_OFFSET_IMP_IDXOBJECTNAME, BitConverter.GetBytes(selectedNameIndex));
            }
        }

        private void Info_ImpClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedNameIndex = InfoTab_PackageFile_ComboBox.SelectedIndex;
                Header_Hexbox.ByteProvider.WriteBytes(HEADER_OFFSET_IMP_IDXCLASSNAME, BitConverter.GetBytes(selectedNameIndex));
            }
        }

        private void InfoTab_Objectname_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = CurrentlyLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME : HEADER_OFFSET_IMP_IDXOBJECTNAME;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_Class_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_EXP_IDXCLASS;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_ImpClass_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_IMP_IDXCLASSNAME;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_Superclass_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_EXP_IDXSUPERCLASS;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_PackageLink_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = CurrentlyLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXLINK : HEADER_OFFSET_IMP_IDXLINK;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_PackageFile_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_IMP_IDXPACKAGEFILE;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_Archetype_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_EXP_IDXARCHETYPE;
            Header_Hexbox.SelectionLength = 4;
        }

        private void InfoTab_Flags_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = HEADER_OFFSET_EXP_OBJECTFLAGS;
            Header_Hexbox.SelectionLength = 8;
        }

        private void InfoTab_ObjectNameIndex_ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Header_Hexbox.SelectionStart = CurrentlyLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXOBJECTNAME + 4 : HEADER_OFFSET_IMP_IDXOBJECTNAME + 4;
            Header_Hexbox.SelectionLength = 4;
        }

        private void GotoButton_Clicked(object sender, RoutedEventArgs e)
        {
            int n;
            if (int.TryParse(Goto_TextBox.Text, out n))
            {
                goToNumber(n);
            }
        }
        private void goToNumber(int n)
        {
            if (n == 0)
            {
                return; //PackageEditorWPF uses Unreal Indexing for entries
            }
            if (CurrentView == View.Tree)
            {
                if (n >= -pcc.ImportCount && n < pcc.ExportCount)
                {
                    List<AdvancedTreeViewItem<TreeViewItem>> noNameNodes = AllTreeViewNodes.Where(s => s.Name.Length == 0).ToList();
                    var nodeName = n.ToString().Replace("-", "n");
                    List<AdvancedTreeViewItem<TreeViewItem>> nodes = AllTreeViewNodes.Where(s => s.Name.Length > 0 && s.Name.Substring(1) == nodeName).ToList();
                    if (nodes.Count > 0)
                    {
                        nodes[0].BringIntoView();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, (NoArgDelegate)delegate { nodes[0].ParentNodeValue.SelectItem(nodes[0]); });
                    }
                }
            }
            else
            {
                if (n >= 0 && n < LeftSide_ListView.Items.Count)
                {
                    LeftSide_ListView.SelectedIndex = n;
                }
            }
        }
        private void Goto_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                GotoButton_Clicked(null, null);
            }
        }

        public class AdvancedTreeViewItem<T> : TreeViewItem
        {
            public T ParentNodeValue { get; set; }
            public T RootParentNodeValue { get; set; }
        }

        private void FindCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Search_TextBox.Focus();
        }

        private void GotoCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Goto_TextBox.Focus();
        }

        private void InfoTab_Flags_ComboBox_ItemSelectionChanged(object sender, Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                EPropertyFlags newFlags = 0U;
                foreach (var flag in InfoTab_Flags_ComboBox.Items)
                {
                    var selectorItem = InfoTab_Flags_ComboBox.ItemContainerGenerator.ContainerFromItem(flag) as SelectorItem;
                    if ((selectorItem != null) && !selectorItem.IsSelected)
                    {
                        newFlags |= (EPropertyFlags)flag;
                    }
                }
                Debug.WriteLine(newFlags);
                Header_Hexbox.ByteProvider.WriteBytes(HEADER_OFFSET_EXP_OBJECTFLAGS, BitConverter.GetBytes((UInt64)newFlags));
            }
        }

        private void ComparePackageBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (pcc != null)
            {
                string extension = System.IO.Path.GetExtension(pcc.FileName);
                OpenFileDialog d = new OpenFileDialog { Filter = "*" + extension + "|*" + extension };
                if (d.ShowDialog().Value)
                {
                    if (pcc.FileName == d.FileName)
                    {
                        MessageBox.Show("You selected the same file as the one already open.");
                        return;
                    }
                    IMEPackage compareFile = MEPackageHandler.OpenMEPackage(d.FileName);
                    if (pcc.Game != compareFile.Game)
                    {
                        MessageBox.Show("Files are for different games.");
                        return;
                    }

                    int numExportsToEnumerate = Math.Min(pcc.ExportCount, compareFile.ExportCount);

                    List<string> changedExports = new List<string>();
                    Stopwatch sw = Stopwatch.StartNew();
                    for (int i = 0; i < numExportsToEnumerate; i++)
                    {
                        IExportEntry exp1 = pcc.Exports[i];
                        IExportEntry exp2 = compareFile.Exports[i];

                        //make data offset and data size the same, as the exports could be the same even if it was appended later.
                        //The datasize being different is a data difference not a true header difference so we won't list it here.
                        byte[] header1 = exp1.Header.TypedClone();
                        byte[] header2 = exp2.Header.TypedClone();
                        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, header1, 32, sizeof(long));
                        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, header2, 32, sizeof(long));

                        //if (!StructuralComparisons.StructuralEqualityComparer.Equals(header1, header2))
                        if (!header1.SequenceEqual(header2))

                        {
                            foreach (byte b in header1)
                            {
                                Debug.Write(" " + b.ToString("X2"));
                            }
                            Debug.WriteLine("");
                            foreach (byte b in header2)
                            {
                                Debug.Write(" " + b.ToString("X2"));
                            }
                            Debug.WriteLine("");
                            changedExports.Add("Export header has changed: " + i + " " + exp1.GetFullPath);
                        }
                        if (!exp1.Data.SequenceEqual(exp2.Data))
                        {
                            changedExports.Add("Export data has changed: " + i + " " + exp1.GetFullPath);
                        }
                    }

                    IMEPackage enumerateExtras = pcc;
                    string file = "this file";
                    if (compareFile.ExportCount < numExportsToEnumerate)
                    {
                        file = "other file";
                        enumerateExtras = compareFile;
                    }

                    for (int i = numExportsToEnumerate; i < compareFile.ExportCount; i++)
                    {
                        changedExports.Add("Export only exists in " + file + ": " + i + " " + enumerateExtras.Exports[i].GetFullPath);
                    }

                    sw.Stop();
                    Debug.WriteLine("Time: " + sw.ElapsedMilliseconds + "ms");

                    ListDialog ld = new ListDialog(changedExports, "Changed exports between files", "The following exports are different between the files.");
                    ld.Show();
                }
            }
        }
    }
}