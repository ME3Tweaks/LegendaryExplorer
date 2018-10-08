using Be.Windows.Forms;
using ByteSizeLib;
using GongSolutions.Wpf.DragDrop;
using ME1Explorer.Unreal;
using ME3Explorer.CurveEd;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
    public partial class PackageEditorWPF : WPFBase, GongSolutions.Wpf.DragDrop.IDropTarget
    {
        enum View
        {
            Names,
            Imports,
            Exports,
            Tree
        }

        int _treeViewMargin = 2;
        public int TreeViewMargin
        {
            get
            {
                return _treeViewMargin;
            }
            set
            {
                if (_treeViewMargin != value)
                {
                    _treeViewMargin = value;
                    OnPropertyChanged("TreeViewMargin");
                }
            }
        }

        /// <summary>
        /// Used to populate the metadata editor values so the list does not constantly need to rebuilt, which can slow down the program on large files like SFXGame or BIOC_Base.
        /// </summary>
        List<string> AllEntriesList;

        Dictionary<ExportLoaderControl, TabItem> ExportLoaders = new Dictionary<ExportLoaderControl, TabItem>();
        View CurrentView;
        public PropGrid pg;

        public static readonly string PackageEditorDataFolder = System.IO.Path.Combine(App.AppDataFolder, @"PackageEditor\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;
        private SortedDictionary<int, int> crossPCCObjectMap;
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
        //private List<AdvancedTreeViewItem<TreeViewItem>> AllTreeViewNodes = new List<AdvancedTreeViewItem<TreeViewItem>>();
        public ObservableCollectionExtended<TreeViewEntry> AllTreeViewNodesX { get; set; } = new ObservableCollectionExtended<TreeViewEntry>();
        #region Commands
        public ICommand ComparePackagesCommand { get; set; }
        public ICommand ExportAllDataCommand { get; set; }
        public ICommand ExportBinaryDataCommand { get; set; }
        public ICommand ImportAllDataCommand { get; set; }
        public ICommand ImportBinaryDataCommand { get; set; }
        public ICommand CloneCommand { get; set; }
        public ICommand CloneTreeCommand { get; set; }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get { return _isBusyTaskbar; }
            set { if (_isBusyTaskbar != value) { _isBusyTaskbar = value; OnPropertyChanged(); } }
        }

        private string _busyText;
        private List<string> Classes;

        public string BusyText
        {
            get { return _busyText; }
            set { if (_busyText != value) { _busyText = value; OnPropertyChanged(); } }
        }

        private void LoadCommands()
        {
            ComparePackagesCommand = new RelayCommand(ComparePackages, PackageIsLoaded);
            ExportAllDataCommand = new RelayCommand(ExportAllData, ExportIsSelected);
            ExportBinaryDataCommand = new RelayCommand(ExportBinaryData, ExportIsSelected);
            ImportAllDataCommand = new RelayCommand(ImportAllData, ExportIsSelected);
            ImportBinaryDataCommand = new RelayCommand(ImportBinaryData, ExportIsSelected);
            CloneCommand = new RelayCommand(CloneEntry, EntryIsSelected);
            CloneTreeCommand = new RelayCommand(CloneTree, EntryIsSelected);
        }

        private void CloneTree(object obj)
        {
            if (GetSelected(out int n))
            {
                int nextIndex; //used to select the final node
                /*crossPCCObjectMap = new SortedDictionary<int, int>();
                TreeViewEntry rootNode = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                if (n >= 0)
                {
                    nextIndex = Pcc.ExportCount;
                    IExportEntry exp = Pcc.getExport(n).Clone();
                    Pcc.addExport(exp);
                    crossPCCObjectMap[n] = Pcc.ExportCount - 1; //0 based.
                    n = ;
                }
                else
                {
                    nextIndex = -Pcc.ImportCount - 1;
                    ImportEntry imp = Pcc.getImport(-n - 1).Clone();
                    Pcc.addImport(imp);
                    n = nextIndex;
                    //We do not relink imports in same-pcc.
                }*/

                crossPCCObjectMap = new SortedDictionary<int, int>();
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                TreeViewEntry newEntry = null;
                if (n >= 0)
                {

                    IExportEntry ent = (selected.Entry as IExportEntry).Clone();
                    Pcc.addExport(ent);
                    newEntry = new TreeViewEntry(ent);
                    crossPCCObjectMap[n] = ent.Index; //0 based. map old index to new index
                }
                else
                {
                    ImportEntry imp = (selected.Entry as ImportEntry).Clone();
                    Pcc.addImport(imp);
                    newEntry = new TreeViewEntry(imp);
                    //Imports are not relinked when locally cloning a tree
                }
                nextIndex = newEntry.UIndex;
                newEntry.Parent = selected.Parent;
                selected.Parent.Sublinks.Add(newEntry);
                selected.Parent.SortChildren();
                //goToNumber(newEntry.UIndex);

                cloneTree(selected, newEntry);
                relinkObjects2(Pcc);
                relinkBinaryObjects(Pcc);
                crossPCCObjectMap = null;
                RefreshView();
                goToNumber(nextIndex);
            }
        }

        private void CloneEntry(object obj)
        {
            if (GetSelected(out int n))
            {
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                TreeViewEntry newEntry = null;
                if (n >= 0)
                {

                    IExportEntry ent = (selected.Entry as IExportEntry).Clone();
                    Pcc.addExport(ent);
                    newEntry = new TreeViewEntry(ent);
                }
                else
                {
                    ImportEntry imp = (selected.Entry as ImportEntry).Clone();
                    Pcc.addImport(imp);
                    newEntry = new TreeViewEntry(imp);
                }
                newEntry.Parent = selected.Parent;
                selected.Parent.Sublinks.Add(newEntry);
                selected.Parent.SortChildren();
                goToNumber(newEntry.UIndex);

            }
        }

        private void cloneTree(TreeViewEntry originalRootNode, TreeViewEntry newRootNode)
        {
            if (originalRootNode.Sublinks.Count > 0)
            {
                foreach (TreeViewEntry node in originalRootNode.Sublinks)
                {
                    TreeViewEntry newEntry = null;
                    if (node.UIndex > 0)
                    {
                        IExportEntry ent = (node.Entry as IExportEntry).Clone();
                        Pcc.addExport(ent);
                        newEntry = new TreeViewEntry(ent);
                        crossPCCObjectMap[node.Entry.Index] = ent.Index; //map old node index to new node index
                    }
                    else if (node.UIndex < 0)
                    {
                        ImportEntry imp = (node.Entry as ImportEntry).Clone();
                        Pcc.addImport(imp);
                        newEntry = new TreeViewEntry(imp);
                    }
                    newEntry.Entry.idxLink = newRootNode.Entry.UIndex;
                    newEntry.Parent = newRootNode;
                    newRootNode.Sublinks.Add(newEntry);

                    /*if (node.UIndex > 0)
                    {
                        nextIndex = Pcc.ExportCount + 1;
                        IExportEntry exp = (node.Entry as IExportEntry).Clone();
                        exp.idxLink = link;
                        Pcc.addExport(exp);
                        crossPCCObjectMap[node.UIndex - 1] = Pcc.ExportCount - 1; //0 based. Just how the code was written.
                    }
                    else if (node.UIndex < 0)
                    {
                        nextIndex = -Pcc.ImportCount - 1;
                        ImportEntry imp = (node.Entry as ImportEntry).Clone();
                        imp.idxLink = link;
                        Pcc.addImport(imp);
                        //we do not relink imports in same-pcc
                    }*/
                    if (node.Sublinks.Count > 0)
                    {
                        cloneTree(node, newEntry);
                    }
                }
            }
            newRootNode.SortChildren();
        }

        private void ImportBinaryData(object obj)
        {
            ImportExpData(true);
        }

        private void ImportAllData(object obj)
        {
            ImportExpData(false);
        }

        private void ImportExpData(bool binaryOnly)
        {
            if (!GetSelected(out int n))
            {
                return;
            }
            IExportEntry export = Pcc.getExport(n);
            OpenFileDialog d = new OpenFileDialog
            {
                Filter = "*.bin|*.bin",
                FileName = export.ObjectName + ".bin"
            };
            var result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                byte[] data = File.ReadAllBytes(d.FileName);
                if (binaryOnly)
                {
                    export.setBinaryData(data);
                }
                else
                {
                    export.Data = data;
                }
                MessageBox.Show("Done.");
            }
        }

        private void ExportBinaryData(object obj)
        {
            ExportExpData(true);
        }

        private void ExportAllData(object obj)
        {
            ExportExpData(false);
        }

        private void ExportExpData(bool binaryOnly)
        {
            if (!GetSelected(out int n))
            {
                return;
            }
            IExportEntry export = Pcc.getExport(n);
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "*.bin|*.bin",
                FileName = export.ObjectName + ".bin"
            };
            var result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                if (binaryOnly)
                {
                    File.WriteAllBytes(d.FileName, export.getBinaryData());
                }
                else
                {
                    File.WriteAllBytes(d.FileName, export.Data);
                }
                MessageBox.Show("Done.");
            }
        }

        private bool ExportIsSelected(object obj)
        {
            int n;
            if (GetSelected(out n))
            {
                return n > 0;
            }
            else return false;
        }

        private bool ImportIsSelected(object obj)
        {
            int n;
            if (GetSelected(out n))
            {
                return n < 0;
            }
            else return false;
        }

        private bool EntryIsSelected(object obj)
        {
            int n;
            if (GetSelected(out n))
            {
                return n != 0;
            }
            else return false;
        }

        private bool PackageIsLoaded(object obj)
        {
            return Pcc != null;
        }

        private void ComparePackages(object obj)
        {
            if (Pcc != null)
            {
                string extension = System.IO.Path.GetExtension(Pcc.FileName);
                OpenFileDialog d = new OpenFileDialog { Filter = "*" + extension + "|*" + extension };
                if (d.ShowDialog().Value)
                {
                    if (Pcc.FileName == d.FileName)
                    {
                        MessageBox.Show("You selected the same file as the one already open.");
                        return;
                    }
                    IMEPackage compareFile = MEPackageHandler.OpenMEPackage(d.FileName);
                    if (Pcc.Game != compareFile.Game)
                    {
                        MessageBox.Show("Files are for different games.");
                        return;
                    }

                    int numExportsToEnumerate = Math.Min(Pcc.ExportCount, compareFile.ExportCount);

                    List<string> changedExports = new List<string>();
                    Stopwatch sw = Stopwatch.StartNew();
                    for (int i = 0; i < numExportsToEnumerate; i++)
                    {
                        IExportEntry exp1 = Pcc.Exports[i];
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

                    IMEPackage enumerateExtras = Pcc;
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
        #endregion

        public PackageEditorWPF()
        {
            //ME3UnrealObjectInfo.generateInfo();
            CurrentView = View.Tree;
            LoadCommands();

            InitializeComponent();
            //map export loaders to their tabs
            ExportLoaders[InterpreterTab_Interpreter] = Interpreter_Tab;
            ExportLoaders[SoundTab_Soundpanel] = Sound_Tab;
            ExportLoaders[CurveTab_CurveEditor] = CurveEditor_Tab;
            ExportLoaders[Bio2DATab_Bio2DAEditor] = Bio2DAViewer_Tab;
            ExportLoaders[ScriptTab_UnrealScriptEditor] = Script_Tab;
            ExportLoaders[BinaryInterpreterTab_BinaryInterpreter] = BinaryInterpreter_Tab;

            LoadRecentList();
            RefreshRecent(false);
        }

        private void LoadFile(string s)
        {
            //  try
            //{
            IsBusy = true;
            foreach (KeyValuePair<ExportLoaderControl, TabItem> entry in ExportLoaders)
            {
                entry.Value.Visibility = Visibility.Collapsed;
            }
            Metadata_Tab.Visibility = Visibility.Collapsed;
            Intro_Tab.Visibility = Visibility.Visible;
            Intro_Tab.IsSelected = true;

            AllTreeViewNodesX.Clear();
            currentFile = s;
            StatusBar_GameID_Container.Visibility = Visibility.Collapsed;
            StatusBar_LeftMostText.Text = "Loading " + System.IO.Path.GetFileName(s) + " (" + ByteSize.FromBytes(new System.IO.FileInfo(s).Length) + ")";
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
            LoadMEPackage(s);
            StatusBar_GameID_Container.Visibility = Visibility.Visible;
            //Metadata_Tab.IsSelected = true; //due to winforms interop thread issues
            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    StatusBar_GameID_Text.Text = "ME1";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                    break;
                case MEGame.ME2:
                    StatusBar_GameID_Text.Text = "ME2";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Maroon);
                    break;
                case MEGame.ME3:
                    StatusBar_GameID_Text.Text = "ME3";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkSeaGreen);
                    break;
                case MEGame.UDK:
                    StatusBar_GameID_Text.Text = "UDK";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.IndianRed);
                    break;
            }
            /*interpreterControl.Pcc = pcc;
            binaryInterpreterControl.Pcc = pcc;
            bio2DAEditor1.Pcc = pcc;
            treeView1.Tag = pcc;*/
            RefreshView();
            InitStuff();
            StatusBar_LeftMostText.Text = System.IO.Path.GetFileName(s);
            InterpreterTab_Interpreter.UnloadExport();
            //InitializeTreeView();

            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += InitializeTreeViewBackground;
            bg.RunWorkerCompleted += InitializeTreeViewBackground_Completed;
            bg.RunWorkerAsync();
            ////            InitializeTreeView();

            //}
            //catch (Exception e)
            //{
            //StatusBar_LeftMostText.Text = "Failed to load " + System.IO.Path.GetFileName(s);
            //MessageBox.Show("Error loading " + System.IO.Path.GetFileName(s) + ":\n" + e.Message);
            //  throw e;
            //}
        }

        private void InitializeTreeViewBackground_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result != null)
            {
                AllTreeViewNodesX.Clear();
                AllTreeViewNodesX.AddRange(e.Result as ObservableCollectionExtended<TreeViewEntry>);
            }
            IsBusy = false;

        }

        private void InitializeTreeViewBackground(object sender, DoWorkEventArgs e)
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "PackageEditorWPF TreeViewInitialization";

            BusyText = "Loading " + System.IO.Path.GetFileName(Pcc.FileName);
            if (Pcc == null)
            {
                return;
            }
            IReadOnlyList<ImportEntry> Imports = Pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = Pcc.Exports;
            int importsOffset = Exports.Count;

            TreeViewEntry rootEntry = new TreeViewEntry(null, Pcc.FileName);
            rootEntry.IsExpanded = true;

            List<TreeViewEntry> rootNodes = new List<TreeViewEntry>();
            rootNodes.Add(rootEntry);
            for (int i = 0; i < Exports.Count; i++)
            {
                rootNodes.Add(new TreeViewEntry(Exports[i]));
            }

            for (int i = 0; i < Imports.Count; i++)
            {
                rootNodes.Add(new TreeViewEntry(Imports[i]));
            }

            //configure links
            //Order: 0 = Root, [Exports], [Imports], <extra, new stuff>
            List<TreeViewEntry> itemsToRemove = new List<TreeViewEntry>();
            foreach (TreeViewEntry entry in rootNodes)
            {
                if (entry.Entry != null)
                {
                    int tvLink = entry.Entry.idxLink;
                    if (tvLink < 0)
                    {
                        //import
                        //Debug.WriteLine("import tvlink " + tvLink);

                        tvLink = Exports.Count + Math.Abs(tvLink);
                        //Debug.WriteLine("Linking " + entry.Entry.GetFullPath + " to index " + tvLink);
                    }

                    TreeViewEntry parent = rootNodes[tvLink];
                    parent.Sublinks.Add(entry);
                    entry.Parent = parent;
                    itemsToRemove.Add(entry); //remove from this level as we have added it to another already
                }
            }
            e.Result = new ObservableCollectionExtended<TreeViewEntry>(rootNodes.Except(itemsToRemove).ToList());
        }

        private void InitializeTreeView()
        {

            IsBusy = true;
            //IsBusyText = "Loading " + System.IO.Path.GetFileName(Pcc.FileName);
            if (Pcc == null)
            {
                return;
            }
            IReadOnlyList<ImportEntry> Imports = Pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = Pcc.Exports;
            AllEntriesList = new List<string>();
            int importsOffset = Exports.Count;

            TreeViewEntry rootEntry = new TreeViewEntry(null, Pcc.FileName);
            rootEntry.IsExpanded = true;
            AllTreeViewNodesX.Add(rootEntry);

            for (int i = 0; i < Exports.Count; i++)
            {
                AllTreeViewNodesX.Add(new TreeViewEntry(Exports[i]));
            }

            for (int i = 0; i < Imports.Count; i++)
            {
                AllTreeViewNodesX.Add(new TreeViewEntry(Imports[i]));
            }

            //configure links
            //Order: 0 = Root, [Exports], [Imports], <extra, new stuff>
            List<TreeViewEntry> itemsToRemove = new List<TreeViewEntry>();
            foreach (TreeViewEntry entry in AllTreeViewNodesX)
            {
                if (entry.Entry != null)
                {
                    int tvLink = entry.Entry.idxLink;
                    if (tvLink < 0)
                    {
                        //import
                        //Debug.WriteLine("import tvlink " + tvLink);

                        tvLink = Exports.Count + Math.Abs(tvLink);
                        //Debug.WriteLine("Linking " + entry.Entry.GetFullPath + " to index " + tvLink);
                    }

                    TreeViewEntry parent = AllTreeViewNodesX[tvLink];
                    parent.Sublinks.Add(entry);
                    entry.Parent = parent;
                    itemsToRemove.Add(entry); //remove from this level as we have added it to another already
                }
            }
            var rootNodes = new ObservableCollectionExtended<TreeViewEntry>(AllTreeViewNodesX.Except(itemsToRemove).ToList());
            AllTreeViewNodesX.ClearEx();
            AllTreeViewNodesX.AddRange(rootNodes);
            IsBusy = false;
        }

        #region Recents
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

        public void RefreshRecent(bool propogate, List<string> recents = null)
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

        #endregion

        private void RefreshView()
        {
            if (Pcc == null)
            {
                return;
            }
            ClearList(LeftSide_ListView);
            IReadOnlyList<ImportEntry> imports = Pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = Pcc.Exports;
            if (CurrentView == View.Names)
            {
                LeftSide_ListView.ItemsSource = Pcc.Names;
            }

            if (CurrentView == View.Imports)
            {
                List<string> importsList = new List<string>();
                int padding = imports.Count.ToString().Length;

                for (int i = 0; i < imports.Count; i++)
                {
                    //" (0x" + (Pcc.ImportOffset + (i * ImportEntry.byteSize)).ToString("X4") + ")\
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
                    IExportEntry exp = Pcc.getExport(i);
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
            }
            else
            {
                LeftSide_ListView.Visibility = Visibility.Visible;
                LeftSide_TreeView.Visibility = Visibility.Collapsed;
            }

        }

        public void InitStuff()
        {
            if (Pcc == null)
                return;
            ComparePackageMenuItem.IsEnabled = true;
            ClassNames = new List<int>();
            IReadOnlyList<IExportEntry> Exports = Pcc.Exports;
            for (int i = 0; i < Exports.Count; i++)
            {
                ClassNames.Add(Exports[i].idxClass); //This can probably be linq'd
            }

            ReloadClassesListForMetadata(); //can be optimized with above statement

            List<string> names = ClassNames.Distinct().Select(Pcc.getObjectName).ToList();
            names.Sort();
            ClearList(ClassDropdown_Combobox);
            ClassDropdown_Combobox.ItemsSource = names.ToArray();
            InfoTab_Objectname_ComboBox.ItemsSource = Pcc.Names;
            InfoTab_ImpClass_ComboBox.ItemsSource = Pcc.Names;
            InfoTab_PackageFile_ComboBox.ItemsSource = Pcc.Names;
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

        private void ReloadClassesListForMetadata()
        {
            IReadOnlyList<ImportEntry> imports = Pcc.Imports;
            Classes = new List<string>();
            for (int i = imports.Count - 1; i >= 0; i--)
            {
                Classes.Add($"{-i + 1}: {imports[i].GetFullPath}");
            }
            Classes.Add("0 : Class");
            int count = 1;
            IReadOnlyList<IExportEntry> Exports = Pcc.Exports;
            foreach (IExportEntry exp in Exports)
            {
                Classes.Add($"{count++}: {exp.GetFullPath}");
            }
            InfoTab_PackageLink_ComboBox.ItemsSource = Classes;
        }

        public void PreviewInfo(int n)
        {


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

                    IExportEntry exportEntry = Pcc.getExport(n);
                    InfoTab_Objectname_ComboBox.SelectedItem = exportEntry.ObjectName;

                    if (exportEntry.idxClass != 0)
                    {
                        //IEntry _class = pcc.getEntry(exportEntry.idxClass);
                        InfoTab_Class_ComboBox.ItemsSource = Classes;
                        InfoTab_Class_ComboBox.SelectedIndex = exportEntry.idxClass + exportEntry.FileRef.Imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Class_ComboBox.SelectedIndex = exportEntry.FileRef.Imports.Count; //Class, 0
                    }
                    InfoTab_Superclass_ComboBox.ItemsSource = Classes;
                    if (exportEntry.idxClassParent != 0)
                    {
                        InfoTab_Superclass_ComboBox.SelectedIndex = exportEntry.idxClassParent + exportEntry.FileRef.Imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Superclass_ComboBox.SelectedIndex = exportEntry.FileRef.Imports.Count; //Class, 0
                    }

                    if (exportEntry.idxLink != 0)
                    {
                        InfoTab_PackageLink_ComboBox.SelectedIndex = exportEntry.idxLink + exportEntry.FileRef.Imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_PackageLink_ComboBox.SelectedIndex = exportEntry.FileRef.Imports.Count; //Class, 0
                    }
                    InfoTab_Headersize_TextBox.Text = exportEntry.Header.Length + " bytes";
                    InfoTab_ObjectnameIndex_TextBox.Text = BitConverter.ToInt32(exportEntry.Header, HEADER_OFFSET_EXP_IDXOBJECTNAME + 4).ToString();
                    InfoTab_Archetype_ComboBox.ItemsSource = Classes;
                    if (exportEntry.idxArchtype != 0)
                    {
                        InfoTab_Archetype_ComboBox.SelectedIndex = exportEntry.idxArchtype + exportEntry.FileRef.Imports.Count; //make positive
                    }
                    else
                    {
                        InfoTab_Archetype_ComboBox.SelectedIndex = exportEntry.FileRef.Imports.Count; //Class, 0
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
                ImportEntry importEntry = Pcc.getImport(n);
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
                    InfoTab_PackageLink_ComboBox.SelectedIndex = importEntry.idxLink + importEntry.FileRef.Imports.Count; //make positive
                }
                else
                {
                    InfoTab_PackageLink_ComboBox.SelectedIndex = importEntry.FileRef.Imports.Count; //Class, 0
                }

                InfoTab_PackageFile_ComboBox.SelectedItem = System.IO.Path.GetFileNameWithoutExtension(importEntry.PackageFile);
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

        /// <summary>
        /// Gets the selected entry uindex in the left side view.
        /// </summary>
        /// <param name="n">int that will be updated to point to the selected entry index. Will return 0 if nothing was selected (check the return value for false).</param>
        /// <returns>True if an item was selected, false if nothing was selected.</returns>
        private bool GetSelected(out int n)
        {
            /*if (CurrentView == View.Tree && LeftSide_TreeView.SelectedItem != null && ((TreeViewItem)LeftSide_TreeView.SelectedItem).Name.StartsWith("_"))
            {
                string name = ((TreeViewItem)LeftSide_TreeView.SelectedItem).Name.Substring(1); //get rid of _
                if (name.StartsWith("n"))
                {
                    //its negative
                    name = $"-{name.Substring(1)}";
                }
                n = Convert.ToInt32(name);
                return true;
            }*/
            if (CurrentView == View.Tree && LeftSide_TreeView.SelectedItem != null)
            {
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                n = Convert.ToInt32(selected.UIndex);
                return true;
            }
            else if (CurrentView == View.Exports && LeftSide_ListView.SelectedItem != null)
            {
                n = LeftSide_ListView.SelectedIndex + 1; //to unreal indexing
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
            /* Dispatcher.BeginInvoke(DispatcherPriority.Background,
         (NoArgDelegate)delegate
         {*/
            loadingNewData = true;
            Preview();
            //TreeViewItem item = LeftSide_TreeView.ItemContainerGenerator.ContainerFromItem(sender as TreeViewItem) as TreeViewItem;

            //if (item != null)
            //{
            //    item.BringIntoView();
            //    e.Handled = true;
            //}
            loadingNewData = false;
            //});
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

        /// <summary>
        /// Prepares the right side of PackageEditorWPF for the current selected entry.
        /// This may take a moment if the data that is being loaded is large or complex.
        /// </summary>
        /// <param name="isRefresh">(needs testing what this does)/param>
        private void Preview(bool isRefresh = false)
        {
            Info_Header_UnsavedChanges.Visibility = Visibility.Collapsed;
            if (!GetSelected(out int n))
            {
                InterpreterTab_Interpreter.UnloadExport();
                return;
            }
            if (n == 0)
            {
                foreach (KeyValuePair<ExportLoaderControl, TabItem> e in ExportLoaders)
                {
                    e.Key.UnloadExport();
                    e.Value.Visibility = Visibility.Collapsed;
                }
                EditorTabs.IsEnabled = false;
                Metadata_Tab.Visibility = Visibility.Collapsed;
                ClearMetadataPane();
                Intro_Tab.Visibility = Visibility.Visible;
                Intro_Tab.IsSelected = true;

                return;
            }
            EditorTabs.IsEnabled = true;
            Metadata_Tab.Visibility = Visibility.Visible;
            Intro_Tab.Visibility = Visibility.Collapsed;
            //Debug.WriteLine("New selection: " + n);

            if (CurrentView == View.Imports || CurrentView == View.Exports || CurrentView == View.Tree)
            {
                //tabControl1_SelectedIndexChanged(null, null);
                PreviewInfo(n);
                //Info_HeaderRaw_Hexbox.Stream = new System.IO.MemoryStream(Pcc.getEntry(n.ToUnrealIdx()).header);
                //RefreshMetaData();
                //export
                Interpreter_Tab.IsEnabled = n >= 0;
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

                    IExportEntry exportEntry = Pcc.getExport(n - 1);
                    CurrentlyLoadedEntry = exportEntry;
                    Header_Hexbox.ByteProvider = new DynamicByteProvider(CurrentlyLoadedEntry.Header);
                    Header_Hexbox.ByteProvider.Changed += InfoTab_Header_ByteProvider_InternalChanged;
                    Info_Header_UnsavedChanges.Visibility = Visibility.Hidden;
                    /*Script_Tab.Visibility = exportEntry.ClassName == "Function" ? Visibility.Visible : Visibility.Collapsed;
                    if (exportEntry.ClassName == "Function")
                    {
                        /*
                                                if (!Script_Tab.TabPages.ContainsKey(nameof(scriptTab)))
                                                {
                                                    packageEditorTabPane.TabPages.Add(scriptTab);
                                                }
                        if (Pcc.Game == MEGame.ME3)
                        {
                            Function func = new Function(exportEntry.Data, pcc);
                            Script_TextBox.Text = func.ToRawText();
                        }
                        else if (Pcc.Game == MEGame.ME1)
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
                    }*/

                    foreach (KeyValuePair<ExportLoaderControl, TabItem> entry in ExportLoaders)
                    {
                        if (entry.Key.CanParse(exportEntry))
                        {
                            entry.Key.LoadExport(exportEntry);
                            entry.Value.Visibility = Visibility.Visible;

                        }
                        else
                        {
                            entry.Value.Visibility = Visibility.Collapsed;
                            entry.Key.UnloadExport();
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
                }*/

                //if (Bio2DAEditorWPF.ParsableBinaryClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith("Default__"))
                //{
                //    Bio2DAViewer_Tab.Visibility = Visibility.Visible;
                //    Bio2DATab_Bio2DAEditor.LoadExport(exportEntry);
                //}
                //else
                //{
                //    Bio2DAViewer_Tab.Visibility = Visibility.Collapsed;
                //    Bio2DATab_Bio2DAEditor.UnloadExport();
                //}

                /*

                headerRawHexBox.ByteProvider = new DynamicByteProvider(exportEntry.header);*/
                /*if (!isRefresh)
                {
                    //InterpreterTab_Interpreter.LoadExport(exportEntry);
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
                }*/
            }
            //import
            else
            {
                Visible_ObjectNameRow = false;
                ImportEntry importEntry = Pcc.getImport(-n - 1);
                CurrentlyLoadedEntry = importEntry;
                Header_Hexbox.ByteProvider = new DynamicByteProvider(CurrentlyLoadedEntry.Header);
                Header_Hexbox.ByteProvider.Changed += InfoTab_Header_ByteProvider_InternalChanged;
                Script_Tab.Visibility = BinaryInterpreter_Tab.Visibility = Bio2DAViewer_Tab.Visibility = Sound_Tab.Visibility = Visibility.Collapsed;
                Metadata_Tab.IsSelected = true;
                PreviewInfo(n);
                /*   n = -n - 1;
                   headerRawHexBox.ByteProvider = new DynamicByteProvider(Pcc.getImport(n).header);
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
            //CHECK THE CURRENT TAB IS VISIBLE/ENABLED. IF NOT, CHOOSE FIRST TAB THAT IS 
            TabItem currentTab = (TabItem)EditorTabs.Items[EditorTabs.SelectedIndex];
            if (!currentTab.IsEnabled || !currentTab.IsVisible)
            {
                int index = 0;
                while (index < EditorTabs.Items.Count)
                {
                    TabItem ti = (TabItem)EditorTabs.Items[index];
                    if (ti.IsEnabled && ti.IsVisible)
                    {
                        EditorTabs.SelectedIndex = index;
                        break;
                    }
                    index++;
                }
            }
        }

        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                //try
                // {
                LoadFile(d.FileName);
                AddRecent(d.FileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
                //}
                //catch (Exception ex)
                //{
                //    MessageBox.Show("Unable to open file:\n" + ex.Message);
                // }
            }
        }

        private void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pcc.save();
        }

        private void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            string extension = System.IO.Path.GetExtension(Pcc.FileName);
            d.Filter = $"*{extension}|*{extension}";
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void PackageEditorWPF_Loaded(object sender, RoutedEventArgs e)
        {
            // Get reference to hexbox winforms control
            Header_Hexbox = (HexBox)Header_Hexbox_Host.Child;
        }

        private void Info_ClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedClassIndex = InfoTab_Class_ComboBox.SelectedIndex;
                var unrealIndex = selectedClassIndex - Pcc.ImportCount;
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
            TreeViewEntry tve = GetTreeViewEntryByUIndex(CurrentlyLoadedEntry.UIndex);
            if (tve != null)
            {
                tve.RefreshDisplayName();
            }
            //todo: mvvm-ize this
            if (Header_Hexbox.ByteProvider != null)
            {
                Header_Hexbox.ByteProvider.ApplyChanges();
            }
            Info_Header_UnsavedChanges.Visibility = Header_Hexbox.ByteProvider.HasChanges() ? Visibility.Visible : Visibility.Hidden;
        }

        private void Info_PackageLinkClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedImpExp = InfoTab_PackageLink_ComboBox.SelectedIndex;
                var unrealIndex = selectedImpExp - Pcc.ImportCount;
                Header_Hexbox.ByteProvider.WriteBytes(CurrentlyLoadedEntry is ExportEntry ? HEADER_OFFSET_EXP_IDXLINK : HEADER_OFFSET_IMP_IDXLINK, BitConverter.GetBytes(unrealIndex));
            }
        }

        private void Info_SuperClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                var selectedClassIndex = InfoTab_Superclass_ComboBox.SelectedIndex;
                var unrealIndex = selectedClassIndex - Pcc.ImportCount;
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
                var unrealIndex = selectedArchetTypeIndex - Pcc.ImportCount;
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

        /// <summary>
        /// Handler for when the Goto button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GotoButton_Clicked(object sender, RoutedEventArgs e)
        {
            int n;
            if (int.TryParse(Goto_TextBox.Text, out n))
            {
                goToNumber(n);
            }
        }

        /// <summary>
        /// Selects the entry that corresponds to the given index
        /// </summary>
        /// <param name="entryIndex">Unreal-indexed entry number</param>
        private void goToNumber(int entryIndex)
        {
            if (entryIndex == 0)
            {
                return; //PackageEditorWPF uses Unreal Indexing for entries
            }
            if (CurrentView == View.Tree)
            {
                /*if (entryIndex >= -pcc.ImportCount && entryIndex < pcc.ExportCount)
                {
                    //List<AdvancedTreeViewItem<TreeViewItem>> noNameNodes = AllTreeViewNodes.Where(s => s.Name.Length == 0).ToList();
                    var nodeName = entryIndex.ToString().Replace("-", "n");
                    List<AdvancedTreeViewItem<TreeViewItem>> nodes = AllTreeViewNodes.Where(s => s.Name.Length > 0 && s.Name.Substring(1) == nodeName).ToList();
                    if (nodes.Count > 0)
                    {
                        nodes[0].BringIntoView();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, (NoArgDelegate)delegate { nodes[0].ParentNodeValue.SelectItem(nodes[0]); });
                    }
                }*/
                var list = AllTreeViewNodesX[0].FlattenTree();
                List<TreeViewEntry> selectNode = list.Where(s => s.Entry != null && s.UIndex == entryIndex).ToList();
                if (selectNode.Count() > 0)
                {
                    //selectNode[0].ExpandParents();
                    selectNode[0].IsSelected = true;
                    FocusTreeViewNodeOld(selectNode[0]);

                    //selectNode[0].Focus(LeftSide_TreeView);
                }
            }
            else
            {
                if (entryIndex >= 0 && entryIndex < LeftSide_ListView.Items.Count)
                {
                    LeftSide_ListView.SelectedIndex = entryIndex;
                }
            }
        }

        private TreeViewEntry GetTreeViewEntryByUIndex(int uindex)
        {
            var nodes = AllTreeViewNodesX[0].FlattenTree();
            return nodes.FirstOrDefault(x => x.UIndex == uindex);
        }

        /// <summary>
        /// Handler for the keydown event while the Goto Textbox is focused. It will issue the Goto button function when the enter key is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Goto_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                GotoButton_Clicked(null, null);
            }
        }



        /// <summary>
        /// Command binding for when the Find command binding is issued (CTRL F)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FindCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Search_TextBox.Focus();
            Search_TextBox.SelectAll();
        }

        /// <summary>
        /// Command binding for when the Goto command is issued (CTRL G)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GotoCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Goto_TextBox.Focus();
            Goto_TextBox.SelectAll();
        }

        /// <summary>
        /// Handler for when the flags combobox item changes values in the metadata tab
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InfoTab_Flags_ComboBox_ItemSelectionChanged(object sender, Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
        {
            if (!loadingNewData)
            {
                EPropertyFlags newFlags = 0U;
                foreach (var flag in InfoTab_Flags_ComboBox.Items)
                {
                    var selectorItem = InfoTab_Flags_ComboBox.ItemContainerGenerator.ContainerFromItem(flag) as SelectorItem;
                    if ((selectorItem != null) && !selectorItem.IsSelected.Value)
                    {
                        newFlags |= (EPropertyFlags)flag;
                    }
                }
                Debug.WriteLine(newFlags);
                Header_Hexbox.ByteProvider.WriteBytes(HEADER_OFFSET_EXP_OBJECTFLAGS, BitConverter.GetBytes((UInt64)newFlags));
            }
        }

        /// <summary>
        /// Command binding for opening the ComparePackage tool
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ComparePackageBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {

        }

        /// <summary>
        /// Drag/drop dragover handler for the entry list treeview
        /// </summary>
        /// <param name="dropInfo"></param>
        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if ((dropInfo.Data as TreeViewEntry) != null && (dropInfo.Data as TreeViewEntry).Parent != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
        }

        /// <summary>
        /// Drop handler for the entry list treeview
        /// </summary>
        /// <param name="dropInfo"></param>
        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo.TargetItem is TreeViewEntry && (dropInfo.Data as TreeViewEntry).Parent != null)
            {
                crossPCCObjectMap = new SortedDictionary<int, int>();

                TreeViewEntry sourceItem = dropInfo.Data as TreeViewEntry;
                TreeViewEntry targetItem = dropInfo.TargetItem as TreeViewEntry;
                if (sourceItem == targetItem || (targetItem.Entry != null && sourceItem.Entry.FileRef == targetItem.Entry.FileRef))
                {
                    return; //ignore
                }
                //Debug.WriteLine("Adding source item: " + sourceItem.Tag.ToString());

                //if (DestinationNode.TreeView != sourceNode.TreeView)
                //{
                IEntry entry = sourceItem.Entry;
                IEntry targetLinkEntry = targetItem.Entry;

                IMEPackage importpcc = entry.FileRef;
                if (importpcc == null)
                {
                    return;
                }



                int n = entry.UIndex;
                int link;
                if (targetItem.Parent == null) //dropped on a first level node
                {
                    link = 0;
                }
                else
                {
                    link = targetLinkEntry.UIndex;
                    //link = link >= 0 ? link + 1 : link;
                }
                int nextIndex;
                TreeViewEntry newItem = null;
                if (n >= 0)
                {
                    if (!importExport(entry as IExportEntry, link))
                    {
                        return;
                    }
                    nextIndex = Pcc.ExportCount;
                    IExportEntry newExport = Pcc.Exports[nextIndex - 1]; //0 based
                    newItem = new TreeViewEntry(newExport);
                }
                else
                {
                    getOrAddCrossImport(importpcc.getImport(Math.Abs(n) - 1).GetFullPath, importpcc, Pcc, sourceItem.Sublinks.Count == 0 ? link : (int?)null);
                    //importImport(importpcc, -n - 1, link);
                    nextIndex = -Pcc.ImportCount;
                    ImportEntry newImport = Pcc.Imports[nextIndex - 1]; //0 based
                    newItem = new TreeViewEntry(newImport);
                }
                newItem.Parent = targetItem;
                targetItem.Sublinks.Add(newItem); //TODO: Resort the children so they display in the proper order

                //if this node has children
                if (sourceItem.Sublinks.Count > 0)
                {
                    importTree(sourceItem, importpcc, newItem);
                }

                targetItem.SortChildren();

                //relinkObjects(importpcc);
                List<string> relinkResults = new List<string>();
                relinkResults.AddRange(relinkObjects2(importpcc));
                relinkResults.AddRange(relinkBinaryObjects(importpcc));
                crossPCCObjectMap = null;

                RefreshView();
                goToNumber(n >= 0 ? Pcc.ExportCount : -Pcc.ImportCount);
                if (relinkResults.Count > 0)
                {
                    ListDialog ld = new ListDialog(relinkResults, "Relink report", "The following items failed to relink.");
                    ld.Show();
                }
                else
                {
                    MessageBox.Show("Items have been ported and relinked with no reported issues.\nNote that this does not mean all binary properties were relinked, only supported ones were.");
                }
            }
        }

        /// <summary>
        /// Recursive importing function for importing items from another PCC.
        /// </summary>
        /// <param name="sourceNode">Source node from the importing instance of PackageEditorWPF</param>
        /// <param name="importpcc">PCC to import from</param>
        /// <param name="link">The entry link the tree will be imported under</param>
        /// <returns></returns>
        private bool importTree(TreeViewEntry sourceNode, IMEPackage importpcc, TreeViewEntry newItemParent)
        {
            int nextIndex;
            int index;
            foreach (TreeViewEntry node in sourceNode.Sublinks)
            {
                index = node.Entry.UIndex;
                TreeViewEntry newEntry = null;
                if (index >= 0)
                {
                    index--; //code is written for 0-based indexing, while UIndex is not 0 based
                    if (!importExport(node.Entry as IExportEntry, newItemParent.UIndex))
                    {
                        return false;
                    }
                    nextIndex = Pcc.ExportCount;
                    IExportEntry newExport = Pcc.Exports[nextIndex - 1]; //0 based
                    newEntry = new TreeViewEntry(newExport);
                }
                else
                {
                    getOrAddCrossImport(importpcc.getImport(Math.Abs(index) - 1).GetFullPath, importpcc, Pcc);
                    nextIndex = -Pcc.ImportCount;

                    ImportEntry newImport = Pcc.Imports[nextIndex - 1]; //0 based
                    newEntry = new TreeViewEntry(newImport);
                }
                newEntry.Parent = newItemParent;
                newItemParent.Sublinks.Add(newEntry); //TODO: Resort the children so they display in the proper order

                if (node.Sublinks.Count > 0)
                {
                    if (!importTree(node, importpcc, newEntry))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Imports an export from another PCC into this editor's active one.
        /// </summary>
        /// <param name="importpcc">PCC to import from</param>
        /// <param name="n">Export index in the importing PCC</param>
        /// <param name="link">Export/Import index in the local PCC that will be used as the parent to attach to.</param>
        /// <returns></returns>
        private bool importExport(IExportEntry ex, int link)
        {
            IExportEntry nex = null;
            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    nex = new ME1ExportEntry(Pcc as ME1Package);
                    break;
                case MEGame.ME2:
                    nex = new ME2ExportEntry(Pcc as ME2Package);
                    break;
                case MEGame.ME3:
                    nex = new ME3ExportEntry(Pcc as ME3Package);
                    break;
                case MEGame.UDK:
                    nex = new UDKExportEntry(Pcc as UDKPackage);
                    break;
            }
            byte[] idata = ex.Data;
            PropertyCollection props = ex.GetProperties();
            int start = ex.GetPropertyStart();
            int end = props.endOffset;
            MemoryStream res = new MemoryStream();
            if ((ex.ObjectFlags & (ulong)EObjectFlags.HasStack) != 0)
            {
                //ME1, ME2 stack
                byte[] stackdummy =        { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, //Lets hope for the best :D
                                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00};

                if (Pcc.Game != MEGame.ME3)
                {
                    stackdummy = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00};
                }
                res.Write(stackdummy, 0, stackdummy.Length);
            }
            else
            {
                res.Write(new byte[start], 0, start);
            }
            //store copy of names list in case something goes wrong
            List<string> names = Pcc.Names.ToList();
            try
            {
                props.WriteTo(res, Pcc);
            }
            catch (Exception exception)
            {
                //restore namelist
                Pcc.setNames(names);
                MessageBox.Show("Error occured while trying to import " + ex.ObjectName + " : " + exception.Message);
                return false;
            }

            //set header so addresses are set
            var header = (byte[])ex.Header.Clone();
            if ((ex.FileRef.Game == MEGame.ME1 || ex.FileRef.Game == MEGame.ME2) && Pcc.Game == MEGame.ME3)
            {
                //we need to clip some bytes out of the header
                byte[] clippedHeader = new byte[header.Length - 4];
                Buffer.BlockCopy(header, 0, clippedHeader, 0, 0x27);
                Buffer.BlockCopy(header, 0x2B, clippedHeader, 0x27, header.Length - 0x2B);

                header = clippedHeader;
            }
            nex.Header = header;
            bool dataAlreadySet = false;
            if (ex.FileRef.Game == MEGame.ME3)
            {
                switch (ex.FileRef.getObjectName(ex.idxClass))
                {
                    //Todo: Figure out how to fix this for orikon.
                    case "SkeletalMesh":
                        {
                            SkeletalMesh skl = new SkeletalMesh(ex);
                            SkeletalMesh.BoneStruct bone;
                            for (int i = 0; i < skl.Bones.Count; i++)
                            {
                                bone = skl.Bones[i];
                                string s = ex.FileRef.getNameEntry(bone.Name);
                                bone.Name = Pcc.FindNameOrAdd(s);
                                skl.Bones[i] = bone;
                            }
                            SkeletalMesh.TailNamesStruct tailName;
                            for (int i = 0; i < skl.TailNames.Count; i++)
                            {
                                tailName = skl.TailNames[i];
                                string s = ex.FileRef.getNameEntry(tailName.Name);
                                tailName.Name = Pcc.FindNameOrAdd(s);
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
            else if (ex.FileRef.Game == MEGame.UDK)
            {
                switch (ex.FileRef.getObjectName(ex.idxClass))
                {
                    case "StaticMesh":
                        {
                            //res.Write(idata, end, idata.Length - end);
                            //rewrite data
                            nex.Data = res.ToArray();
                            UDKStaticMesh usm = new UDKStaticMesh(ex.FileRef as UDKPackage, ex.Index);
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
                ImportEntry portingFromClassImport = ex.FileRef.getImport(Math.Abs(ex.idxClass) - 1);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, ex.FileRef, Pcc);
                classValue = newClassImport.UIndex;
            }

            //Check archetype.
            if (ex.idxArchtype < 0)
            {
                ImportEntry portingFromClassImport = ex.FileRef.getImport(Math.Abs(ex.idxArchtype) - 1);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, ex.FileRef, Pcc);
                archetype = newClassImport.UIndex;
            }

            if (!dataAlreadySet)
            {
                nex.Data = res.ToArray();
            }
            nex.idxClass = classValue;
            nex.idxObjectName = Pcc.FindNameOrAdd(ex.FileRef.getNameEntry(ex.idxObjectName));
            nex.idxLink = link;
            nex.idxArchtype = archetype;
            nex.idxClassParent = 0;
            Pcc.addExport(nex);

            crossPCCObjectMap[ex.Index] = Pcc.ExportCount - 1; //0 based.
            return true;
        }

        /// <summary>
        /// Handles pressing the enter key when the class dropdown is active. Automatically will attempt to find the next object by class.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClassDropdown_Combobox_OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                FindNextObjectByClass();
            }
        }

        /// <summary>
        /// Finds the next entry that has the selected class from the dropdown.
        /// </summary>
        private void FindNextObjectByClass()
        {
            if (Pcc == null)
                return;
            int n = LeftSide_ListView.SelectedIndex;
            if (ClassDropdown_Combobox.SelectedItem == null)
                return;
            int start;
            if (n == -1)
                start = 0;
            else
                start = n + 1;


            string searchClass = ClassDropdown_Combobox.SelectedItem.ToString();
            /*if (CurrentView == View.Names)
            {
                for (int i = start; i < pcc.Names.Count; i++)
                    if (Pcc.getNameEntry(i).ToLower().Contains(searchTerm))
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
            }*/
            if (CurrentView == View.Tree)
            {
                //this needs fixed as for some rason its way out of order...
                TreeViewEntry selectedNode = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                var items = AllTreeViewNodesX[0].FlattenTree();
                int pos = selectedNode == null ? 0 : items.IndexOf(selectedNode);
                pos += 1; //search this and 1 forward
                for (int i = 0; i < items.Count; i++)
                {
                    int curIndex = (i + pos) % items.Count;
                    TreeViewEntry node = items[(i + pos) % items.Count];
                    if (node.Entry == null)
                    {
                        continue;
                    }
                    //Debug.WriteLine(curIndex + " " + node.Entry.ObjectName);

                    if (node.Entry.ClassName.Equals(searchClass))
                    {
                        //node.ExpandParents();
                        node.IsSelected = true;
                        FocusTreeViewNodeOld(node);
                        //                        node.Focus(LeftSide_TreeView);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Find object by class button click handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FindObjectByClass_Click(object sender, RoutedEventArgs e)
        {
            FindNextObjectByClass();
        }

        /// <summary>
        /// Click handler for the search button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchButton_Clicked(object sender, RoutedEventArgs e)
        {
            Search();
        }

        /// <summary>
        /// Key handler for the search box. This listens for the enter key.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Searchbox_OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                Search();
            }
        }

        /// <summary>
        /// Takes the contents of the search box and finds the next instance of it.
        /// </summary>
        private void Search()
        {
            if (Pcc == null)
                return;
            int n = LeftSide_ListView.SelectedIndex;
            if (Search_TextBox.Text == "")
                return;
            int start;
            if (n == -1)
                start = 0;
            else
                start = n + 1;


            string searchTerm = Search_TextBox.Text.ToLower();
            /*if (CurrentView == View.Names)
            {
                for (int i = start; i < pcc.Names.Count; i++)
                    if (Pcc.getNameEntry(i).ToLower().Contains(searchTerm))
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
            */
            if (CurrentView == View.Exports)
            {
                IReadOnlyList<IExportEntry> Exports = Pcc.Exports;
                for (int i = start; i < Exports.Count; i++)
                {
                    if (Exports[i].ObjectName.ToLower().Contains(searchTerm))
                    {
                        LeftSide_ListView.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (CurrentView == View.Tree)
            {
                //this needs fixed as for some rason its way out of order...
                TreeViewEntry selectedNode = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                var items = AllTreeViewNodesX[0].FlattenTree();
                int pos = selectedNode == null ? -1 : items.IndexOf(selectedNode);
                pos += 1; //search this and 1 forward
                for (int i = 0; i < items.Count; i++)
                {
                    int curIndex = (i + pos) % items.Count;
                    TreeViewEntry node = items[(i + pos) % items.Count];
                    if (node.Entry == null)
                    {
                        continue;
                    }
                    //Debug.WriteLine(curIndex + " " + node.Entry.ObjectName);
                    if (node.Entry.ObjectName.ToLower().Contains(searchTerm))
                    {
                        //node.ExpandParents();
                        node.IsSelected = true;
                        FocusTreeViewNodeOld(node);
                        //                        node.Focus(LeftSide_TreeView);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Binding for moving to the next visible and enabled tab.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NextTabBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int index = EditorTabs.SelectedIndex + 1;
            while (index < EditorTabs.Items.Count)
            {
                TabItem ti = (TabItem)EditorTabs.Items[index];
                if (ti.IsEnabled && ti.IsVisible)
                {
                    EditorTabs.SelectedIndex = index;
                    break;
                }
                index++;
            }
        }

        /// <summary>
        /// Binding to move to the previous visible and enabled tab.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PreviousTabBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int index = EditorTabs.SelectedIndex - 1;
            while (index >= 0)
            {
                TabItem ti = (TabItem)EditorTabs.Items[index];
                if (ti.IsEnabled && ti.IsVisible)
                {
                    EditorTabs.SelectedIndex = index;
                    break;
                }
                index--;
            }
        }

        private void BuildME1TLKDB_Clicked(object sender, RoutedEventArgs e)
        {
            string myBasePath = @"D:\Origin Games\Mass Effect\";
            string bioBase = @"BioGame\CookedPC";
            string[] extensions = new[] { ".u", ".upk" };
            FileInfo[] files =
    new DirectoryInfo(System.IO.Path.Combine(myBasePath, bioBase)).EnumerateFiles("*", SearchOption.AllDirectories)
         .Where(f => extensions.Contains(f.Extension.ToLower()))
         .ToArray();
            int i = 1;
            SortedDictionary<int, KeyValuePair<string, List<string>>> stringMapping = new SortedDictionary<int, KeyValuePair<string, List<string>>>();
            foreach (FileInfo f in files)
            {
                StatusBar_LeftMostText.Text = "[" + i + "/" + files.Count() + "] Scanning " + f.FullName;
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                int basePathLen = myBasePath.Length;
                using (IMEPackage pack = MEPackageHandler.OpenMEPackage(f.FullName))
                {
                    List<IExportEntry> tlkExports = pack.Exports.Where(x => (x.ObjectName == "tlk" || x.ObjectName == "tlk_M") && x.ClassName == "BioTlkFile").ToList();
                    if (tlkExports.Count > 0)
                    {
                        string subPath = f.FullName.Substring(basePathLen);
                        Debug.WriteLine("Found exports in " + f.FullName.Substring(basePathLen));
                        foreach (IExportEntry exp in tlkExports)
                        {
                            ME1Explorer.Unreal.Classes.TalkFile talkFile = new ME1Explorer.Unreal.Classes.TalkFile(exp);
                            foreach (var sref in talkFile.StringRefs)
                            {
                                if (sref.StringID == 0) continue; //skip blank
                                if (sref.Data == null || sref.Data == "-1" || sref.Data == "") continue; //skip blank

                                KeyValuePair<string, List<string>> dictEntry;
                                if (!stringMapping.TryGetValue(sref.StringID, out dictEntry))
                                {
                                    dictEntry = new KeyValuePair<string, List<string>>(sref.Data, new List<string>());
                                    stringMapping[sref.StringID] = dictEntry;
                                }
                                if (sref.StringID == 158104)
                                {
                                    Debugger.Break();
                                }
                                dictEntry.Value.Add(subPath + " in uindex " + exp.UIndex + " \"" + exp.ObjectName + "\"");
                            }
                        }
                    }
                    i++;
                }
            }

            int done = 0;
            int total = stringMapping.Count();
            using (System.IO.StreamWriter file =
        new System.IO.StreamWriter(@"C:\Users\Public\SuperTLK.txt"))
            {
                StatusBar_LeftMostText.Text = "Writing... ";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                foreach (KeyValuePair<int, KeyValuePair<string, List<string>>> entry in stringMapping)
                {
                    // do something with entry.Value or entry.Key
                    file.WriteLine(entry.Key);
                    file.WriteLine(entry.Value.Key);
                    foreach (string fi in entry.Value.Value)
                    {
                        file.WriteLine(" - " + fi);
                    }
                    file.WriteLine();
                }
            }

            StatusBar_LeftMostText.Text = "Done";
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                LoadFile(files[0]);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = System.IO.Path.GetExtension(files[0]).ToLower();
                if (ext != ".u" && ext != ".upk" && ext != ".pcc" && ext != ".sfm")
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        internal void ClearMetadataPane()
        {
            InfoTab_Objectname_ComboBox.SelectedItem = null;
            InfoTab_Class_ComboBox.SelectedItem = null;
            InfoTab_Superclass_ComboBox.SelectedItem = null;
            InfoTab_PackageLink_ComboBox.SelectedItem = null;
            InfoTab_Headersize_TextBox.Text = null;
            InfoTab_ObjectnameIndex_TextBox.Text = null;
            InfoTab_Archetype_ComboBox.ItemsSource = null;
            InfoTab_Archetype_ComboBox.Items.Clear();
            InfoTab_Archetype_ComboBox.SelectedItem = null;
            InfoTab_Flags_ComboBox.ItemsSource = null;
            InfoTab_Flags_ComboBox.SelectedItem = null;
            InfoTab_ExportDataSize_TextBox.Text = null;
            InfoTab_ExportOffsetHex_TextBox.Text = null;
            InfoTab_ExportOffsetDec_TextBox.Text = null;
            Header_Hexbox.ByteProvider = new DynamicByteProvider(new byte[] { });

        }

        private void BuildME1ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME1UnrealObjectInfo.generateInfo();
        }

        private void FocusTreeViewNodeOld(TreeViewEntry node)
        {
            if (node == null) return;
            var nodes = (IEnumerable<TreeViewEntry>)LeftSide_TreeView.ItemsSource;
            if (nodes == null) return;

            var stack = new Stack<TreeViewEntry>();
            stack.Push(node);
            var parent = node.Parent;
            while (parent != null)
            {
                stack.Push(parent);
                parent = parent.Parent;
            }

            var generator = LeftSide_TreeView.ItemContainerGenerator;
            while (stack.Count > 0)
            {
                var dequeue = stack.Pop();
                LeftSide_TreeView.UpdateLayout();

                var treeViewItem = (TreeViewItem)generator.ContainerFromItem(dequeue);
                if (stack.Count > 0)
                {
                    treeViewItem.IsExpanded = true;
                }
                else
                {
                    if (treeViewItem == null)
                    {
                        //This is being triggered when it shouldn't be
                        Debug.WriteLine("FocusNode has triggered null item - CANNOT FOCUS! ");
                        //Debugger.Break();
                    }
                    else
                    {
                        treeViewItem.IsSelected = true;
                    }
                }
                if (treeViewItem != null)
                {
                    treeViewItem.BringIntoView();
                    generator = treeViewItem.ItemContainerGenerator;
                }
            }
        }

        private void FocusTreeViewNode(TreeViewEntry node)
        {
            if (node == null)
                return;

            var treeViewItem = GetTreeViewItemNEW(LeftSide_TreeView, node);
            treeViewItem?.BringIntoView();
        }

        public TreeViewItem GetTreeViewItemNEW(ItemsControl container, TreeViewEntry item)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (container.DataContext == item)
                return container as TreeViewItem;

            //get parent tree
            var stack = new Stack<TreeViewEntry>();
            stack.Push(item);
            var parent = item.Parent;
            while (parent != null)
            {
                stack.Push(parent);
                parent = parent.Parent;
            }

            var generator = LeftSide_TreeView.ItemContainerGenerator;
            while (stack.Count > 0)
            {
                var dequeue = stack.Pop();
                LeftSide_TreeView.UpdateLayout();
                int indexInGenerator = generator.Items.IndexOf(dequeue);
                Debug.WriteLine("Index in generator: " + indexInGenerator);

                var treeViewItem = (TreeViewItem)generator.ContainerFromItem(dequeue);
                if (stack.Count > 0)
                {
                    treeViewItem.IsExpanded = true;
                }
                else
                {
                    //if (treeViewItem == null)
                    //{
                    //    //This is being triggered when it shouldn't be
                    //    Debugger.Break();
                    //}
                    //treeViewItem.IsSelected = true;
                }
                treeViewItem.BringIntoView();
                generator = treeViewItem.ItemContainerGenerator;
            }

            //container starts as treeview
            //if (container is TreeViewItem && !((TreeViewItem)container).IsExpanded)
            //{
            //    container.SetValue(TreeViewItem.IsExpandedProperty, true);
            //}

            container.ApplyTemplate();
            if (container.Template.FindName("ItemsHost", container) is ItemsPresenter itemsPresenter)
            {
                itemsPresenter.ApplyTemplate();
            }
            else
            {
                itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                if (itemsPresenter == null)
                {
                    container.UpdateLayout();
                    itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                }
            }

            var itemsHostPanel = (Panel)VisualTreeHelper.GetChild(itemsPresenter, 0);
            var children = itemsHostPanel.Children;
            var virtualizingPanel = itemsHostPanel as VirtualizingPanel;
            for (int i = 0, count = container.Items.Count; i < count; i++)
            {
                /*TreeViewItem subContainer;
                if (virtualizingPanel != null)
                {
                    // this is the part that requires .NET 4.5+
                    virtualizingPanel.BringIndexIntoViewPublic(i);
                    subContainer = (TreeViewItem)container.ItemContainerGenerator.ContainerFromIndex(i);
                }
                else
                {
                    subContainer = (TreeViewItem)container.ItemContainerGenerator.ContainerFromIndex(i);
                    subContainer.BringIntoView();
                }

                if (subContainer != null)
                {
                    TreeViewItem resultContainer = GetTreeViewItem(subContainer, item);
                    if (resultContainer != null)
                        return resultContainer;

                    subContainer.IsExpanded = false;
                }*/
                Debug.WriteLine(container.Items[i]);
            }
            return null;
        }


        public static TreeViewItem GetTreeViewItem(ItemsControl container, object item)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (container.DataContext == item)
                return container as TreeViewItem;

            if (container is TreeViewItem && !((TreeViewItem)container).IsExpanded)
            {
                container.SetValue(TreeViewItem.IsExpandedProperty, true);
            }

            container.ApplyTemplate();
            if (container.Template.FindName("ItemsHost", container) is ItemsPresenter itemsPresenter)
            {
                itemsPresenter.ApplyTemplate();
            }
            else
            {
                itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                if (itemsPresenter == null)
                {
                    container.UpdateLayout();
                    itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                }
            }

            var itemsHostPanel = (Panel)VisualTreeHelper.GetChild(itemsPresenter, 0);
            var children = itemsHostPanel.Children;
            var virtualizingPanel = itemsHostPanel as VirtualizingPanel;
            for (int i = 0, count = container.Items.Count; i < count; i++)
            {
                TreeViewItem subContainer;
                if (virtualizingPanel != null)
                {
                    // this is the part that requires .NET 4.5+
                    virtualizingPanel.BringIndexIntoViewPublic(i);
                    subContainer = (TreeViewItem)container.ItemContainerGenerator.ContainerFromIndex(i);
                }
                else
                {
                    subContainer = (TreeViewItem)container.ItemContainerGenerator.ContainerFromIndex(i);
                    subContainer.BringIntoView();
                }

                if (subContainer != null)
                {
                    TreeViewItem resultContainer = GetTreeViewItem(subContainer, item);
                    if (resultContainer != null)
                        return resultContainer;

                    subContainer.IsExpanded = false;
                }
            }
            return null;
        }

        private static T FindVisualChild<T>(Visual visual) where T : Visual
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
            {
                if (VisualTreeHelper.GetChild(visual, i) is Visual child)
                {
                    if (child is T item)
                        return item;

                    item = FindVisualChild<T>(child);
                    if (item != null)
                        return item;
                }
            }
            return null;
        }

        private void TouchComfyMode_Clicked(object sender, RoutedEventArgs e)
        {
            TouchComfyMode_MenuItem.IsChecked = !TouchComfyMode_MenuItem.IsChecked;
            TreeViewMargin = TouchComfyMode_MenuItem.IsChecked ? 5 : 2;
        }

        private void PackageEditorWPF_Closing(object sender, CancelEventArgs e)
        {
            SoundTab_Soundpanel.FreeAudioResources();

        }

        private void OpenIn_Clicked(object sender, RoutedEventArgs e)
        {
            var myValue = (string)((MenuItem)sender).Tag;
            switch (myValue)
            {
                case "DialogueEditor":
                    var diaEditor = new DialogEditor.DialogEditor();
                    diaEditor.LoadFile(Pcc.FileName);
                    diaEditor.Show();
                    break;
                case "FaceFXEditor":
                    var facefxEditor = new FaceFX.FaceFXEditor();
                    facefxEditor.LoadFile(Pcc.FileName);
                    facefxEditor.Show();
                    break;
                case "PathfindingEditor":
                    var pathEditor = new PathfindingEditor();
                    pathEditor.LoadFile(Pcc.FileName);
                    pathEditor.Show();
                    break;
                case "SoundplorerWPF":
                    var soundplorerWPF = new Soundplorer.SoundplorerWPF();
                    soundplorerWPF.LoadFile(Pcc.FileName);
                    soundplorerWPF.Show();
                    break;
                case "SequenceEditor":
                    var seqEditor = new SequenceEditor();
                    seqEditor.LoadFile(Pcc.FileName);
                    seqEditor.Show();
                    break;
            }
        }
    }
    [DebuggerDisplay("TreeViewEntry {DisplayName}")]
    public class TreeViewEntry : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            var temp = PropertyChanged;
            if (temp != null)
                temp(this, new PropertyChangedEventArgs(propName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private System.Windows.Media.Brush _foregroundColor = System.Windows.Media.Brushes.DarkSeaGreen;
        private bool isSelected;
        public bool IsSelected
        {
            get { return this.isSelected; }
            set
            {
                if (value != this.isSelected)
                {
                    this.isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        private bool isExpanded;
        public bool IsExpanded
        {
            get { return this.isExpanded; }
            set
            {
                if (value != this.isExpanded)
                {
                    this.isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                }
            }
        }

        public void ExpandParents()
        {
            if (Parent != null)
            {
                Parent.ExpandParents();
                Parent.IsExpanded = true;
            }
        }

        //public void Focus(TreeView tView)
        //{
        //    TreeViewItem tvItem = (TreeViewItem)tView.ItemContainerGenerator.ContainerFromItem(this);
        //    tvItem?.Focus();
        //}

        /// <summary>
        /// Flattens the tree into depth first order. Use this method for searching the list.
        /// </summary>
        /// <returns></returns>
        public List<TreeViewEntry> FlattenTree()
        {
            List<TreeViewEntry> nodes = new List<TreeViewEntry>();
            nodes.Add(this);
            foreach (TreeViewEntry tve in Sublinks)
            {
                nodes.AddRange(tve.FlattenTree());
            }
            return nodes;
        }

        public TreeViewEntry Parent { get; set; }

        /// <summary>
        /// The entry object from the file that this node represents
        /// </summary>
        public IEntry Entry { get; set; }
        /// <summary>
        /// List of entries that link to this node
        /// </summary>
        public ObservableCollectionExtended<TreeViewEntry> Sublinks { get; set; }
        public TreeViewEntry(IEntry entry, string displayName = null)
        {
            Entry = entry;
            DisplayName = displayName;
            Sublinks = new ObservableCollectionExtended<TreeViewEntry>();
        }

        public void RefreshDisplayName()
        {
            OnPropertyChanged("DisplayName");
        }

        private string _displayName;
        public string DisplayName
        {
            get
            {
                if (_displayName != null) return _displayName;
                string type = UIndex < 0 ? "Imp" : "Exp";
                return $"({type}) {UIndex} {Entry.ObjectName}({Entry.ClassName})";
            }
            set { _displayName = value; OnPropertyChanged(); }
        }

        public int UIndex { get { return Entry != null ? Entry.UIndex : 0; } }
        public System.Windows.Media.Brush ForegroundColor
        {
            get { return Entry == null ? System.Windows.Media.Brushes.Black : UIndex > 0 ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.Gray; }
            set
            {
                _foregroundColor = value;
                OnPropertyChanged("ForegroundColor");
            }
        }

        public override string ToString()
        {
            return "TreeViewEntry " + DisplayName;
        }

        /// <summary>
        /// Sorts this node's children in ascending positives first, then descending negatives
        /// </summary>
        internal void SortChildren()
        {
            var exportNodes = Sublinks.Where(x => x.Entry.UIndex > 0).OrderBy(x => x.UIndex).ToList();
            var importNodes = Sublinks.Where(x => x.Entry.UIndex < 0).OrderBy(x => x.UIndex).Reverse().ToList(); //we want this in descending order

            exportNodes.AddRange(importNodes);
            Sublinks.Clear();
            Sublinks.AddRange(exportNodes);
        }
    }
}