using ByteSizeLib;
using GongSolutions.Wpf.DragDrop;
using ME1Explorer.Unreal;
using ME3Explorer.PackageEditorWPFControls;
using ME3Explorer.Packages;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.PeregrineTreeView;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using StreamHelpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static ME3Explorer.Unreal.UnrealFlags;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for PackageEditorWPF.xaml
    /// </summary>
    public partial class PackageEditorWPF : WPFBase, GongSolutions.Wpf.DragDrop.IDropTarget
    {
        public enum CurrentViewMode
        {
            Names,
            Imports,
            Exports,
            Tree
        }
        public static readonly string[] ExportFileTypes = { "GFxMovieInfo", "BioSWF", "Texture2D", "WwiseStream" };

        #region TouchComfyMode
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
        #endregion

        /// <summary>
        /// Used to populate the metadata editor values so the list does not constantly need to rebuilt, which can slow down the program on large files like SFXGame or BIOC_Base.
        /// </summary>
        List<string> AllEntriesList;
        List<Button> RecentButtons = new List<Button>();
        //Objects in this collection are displayed on the left list view (names, imports, exports)

        Dictionary<ExportLoaderControl, TabItem> ExportLoaders = new Dictionary<ExportLoaderControl, TabItem>();
        private CurrentViewMode _currentView;
        public CurrentViewMode CurrentView
        {
            get { return _currentView; }
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;
                    OnPropertyChanged();
                    switch (value)
                    {
                        case CurrentViewMode.Names:
                            TextSearch.SetTextPath(LeftSide_ListView, "Name");
                            break;
                        case CurrentViewMode.Imports:
                            TextSearch.SetTextPath(LeftSide_ListView, "ObjectName");
                            break;
                        case CurrentViewMode.Exports:
                            TextSearch.SetTextPath(LeftSide_ListView, "ObjectName");
                            break;
                    }
                }
            }
        }

        public ObservableCollectionExtended<object> LeftSideList_ItemsSource { get; set; } = new ObservableCollectionExtended<object>();
        public ObservableCollectionExtended<IndexedName> NamesList { get; set; } = new ObservableCollectionExtended<IndexedName>();
        public ObservableCollectionExtended<string> ClassDropdownList { get; set; } = new ObservableCollectionExtended<string>();
        public ObservableCollectionExtended<TreeViewEntry> AllTreeViewNodesX { get; set; } = new ObservableCollectionExtended<TreeViewEntry>();
        private TreeViewEntry _selectedItem;
        public TreeViewEntry SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    OnPropertyChanged();
                    Preview();
                }
            }
        }


        public static readonly string PackageEditorDataFolder = System.IO.Path.Combine(App.AppDataFolder, @"PackageEditor\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;
        private SortedDictionary<int, int> crossPCCObjectMap;
        private string currentFile;
        private int QueuedGotoNumber;
        private bool IsLoadingFile;

        #region Busy variables
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

        public string BusyText
        {
            get { return _busyText; }
            set { if (_busyText != value) { _busyText = value; OnPropertyChanged(); } }
        }
        #endregion

        #region Commands
        public ICommand ComparePackagesCommand { get; set; }
        public ICommand ExportAllDataCommand { get; set; }
        public ICommand ExportBinaryDataCommand { get; set; }
        public ICommand ImportAllDataCommand { get; set; }
        public ICommand ImportBinaryDataCommand { get; set; }
        public ICommand CloneCommand { get; set; }
        public ICommand CloneTreeCommand { get; set; }
        public ICommand FindEntryViaOffsetCommand { get; set; }
        public ICommand CheckForDuplicateIndexesCommand { get; set; }
        public ICommand EditNameCommand { get; set; }
        public ICommand ExportImportDataVisibilityCommand { get; set; }
        public ICommand AddNameCommand { get; set; }
        public ICommand CopyNameCommand { get; set; }
        public ICommand RebuildStreamingLevelsCommand { get; set; }
        public ICommand ExportEmbeddedFileCommand { get; set; }
        public ICommand ImportEmbeddedFileCommand { get; set; }
        public ICommand ReindexCommand { get; set; }
        public ICommand TrashCommand { get; set; }
        public ICommand PackageHeaderViewerCommand { get; set; }
        public ICommand CreateNewPackageGUIDCommand { get; set; }
        public ICommand SetPackageAsFilenamePackageCommand { get; set; }
        private void LoadCommands()
        {
            ComparePackagesCommand = new RelayCommand(ComparePackages, PackageIsLoaded);
            ExportAllDataCommand = new RelayCommand(ExportAllData, ExportIsSelected);
            ExportBinaryDataCommand = new RelayCommand(ExportBinaryData, ExportIsSelected);
            ImportAllDataCommand = new RelayCommand(ImportAllData, ExportIsSelected);
            ImportBinaryDataCommand = new RelayCommand(ImportBinaryData, ExportIsSelected);
            CloneCommand = new RelayCommand(CloneEntry, EntryIsSelected);
            CloneTreeCommand = new RelayCommand(CloneTree, TreeEntryIsSelected);
            FindEntryViaOffsetCommand = new RelayCommand(FindEntryViaOffset, PackageIsLoaded);
            CheckForDuplicateIndexesCommand = new RelayCommand(CheckForDuplicateIndexes, PackageIsLoaded);
            EditNameCommand = new RelayCommand(EditName, NameIsSelected);
            AddNameCommand = new RelayCommand(AddName, CanAddName);
            CopyNameCommand = new RelayCommand(CopyName, NameIsSelected);
            ExportImportDataVisibilityCommand = new RelayCommand((o) => { }, ExportIsSelected); //no execution command
            RebuildStreamingLevelsCommand = new RelayCommand(RebuildStreamingLevels, PackageIsLoaded);
            ExportEmbeddedFileCommand = new RelayCommand(ExportEmbeddedFile, DoesSelectedItemHaveEmbeddedFile);
            ImportEmbeddedFileCommand = new RelayCommand(ImportEmbeddedFile, DoesSelectedItemHaveEmbeddedFile);
            ReindexCommand = new RelayCommand(ReindexObjectByName, ExportIsSelected);
            TrashCommand = new RelayCommand(TrashEntryAndChildren, EntryIsSelected);
            PackageHeaderViewerCommand = new RelayCommand(ViewPackageInfo, PackageIsLoaded);
            CreateNewPackageGUIDCommand = new RelayCommand(GenerateNewGUIDForSelected, PackageExportIsSelected);
            SetPackageAsFilenamePackageCommand = new RelayCommand(SetSelectedAsFilenamePackage, PackageExportIsSelected);
        }

        private void SetSelectedAsFilenamePackage(object obj)
        {
            TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
            IExportEntry export = selected.Entry as IExportEntry;
            byte[] fileGUID = export.FileRef.getHeader().Skip(0x4E).Take(16).ToArray();
            string fname = Path.GetFileNameWithoutExtension(export.FileRef.FileName);

            //Write GUID
            byte[] header = export.GetHeader();
            int preguidcountoffset = Pcc.Game == MEGame.ME3 ? 0x2C : 0x30;
            int count = BitConverter.ToInt32(header, preguidcountoffset);
            int headerguidoffset = (preguidcountoffset + 4) + (count * 4);
            SharedPathfinding.WriteMem(header, headerguidoffset, fileGUID);
            export.Header = header;

            export.idxObjectName = export.FileRef.FindNameOrAdd(fname);
        }

        private void GenerateNewGUIDForSelected(object obj)
        {
            TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
            IExportEntry export = selected.Entry as IExportEntry;
            Guid newGuid = Guid.NewGuid();
            byte[] header = export.GetHeader();
            int preguidcountoffset = Pcc.Game == MEGame.ME3 ? 0x2C : 0x30;
            int count = BitConverter.ToInt32(header, preguidcountoffset);
            int headerguidoffset = (preguidcountoffset + 4) + (count * 4);
            SharedPathfinding.WriteMem(header, headerguidoffset, newGuid.ToByteArray());
            export.Header = header;
        }

        private void ViewPackageInfo(object obj)
        {
            var items = new List<string>();
            byte[] header = Pcc.getHeader();
            MemoryStream ms = new MemoryStream(header);

            uint magicnum = ms.ReadUInt32();
            items.Add($"0x{(ms.Position - 4):X2} Magic number: 0x{magicnum:X8}");
            ushort unrealVer = ms.ReadUInt16();
            items.Add($"0x{(ms.Position - 2):X2} Unreal version: {unrealVer} (0x{unrealVer:X4})");
            int licenseeVer = ms.ReadUInt16();
            items.Add($"0x{(ms.Position - 2):X2} Licensee version:  {licenseeVer} (0x{licenseeVer:X4})");
            uint fullheadersize = ms.ReadUInt32();
            items.Add($"0x{(ms.Position - 4):X2} Full header size:  {fullheadersize} (0x{fullheadersize:X8})");
            int foldernameStrLen = ms.ReadInt32();
            items.Add($"0x{(ms.Position - 4):X2} Folder name string length: {foldernameStrLen} (0x{foldernameStrLen:X8}) (Negative means Unicode)");
            long currentPosition = ms.Position;
            if (foldernameStrLen > 0)
            {
                string str = ms.ReadStringASCII(foldernameStrLen);
                items.Add($"0x{currentPosition:X2} Folder name:  {str}");
            }
            else
            {
                string str = ms.ReadStringUnicodeNull((foldernameStrLen * -2));
                items.Add($"0x{currentPosition:X2} Folder name:  {str}");
            }
            uint flags = ms.ReadUInt32();
            string flagsStr = $"0x{(ms.Position - 4):X2} Flags: 0x{flags:X8} ";
            EPackageFlags flagEnum = (EPackageFlags)flags;
            var setFlags = EnumHelper<EPackageFlags>.MaskToList(flagEnum);
            foreach (var setFlag in setFlags)
            {
                flagsStr += " " + setFlag.ToString();
            }
            items.Add(flagsStr);
            new SharedUI.ListDialog(items, Path.GetFileName(Pcc.FileName) + " header information", "Below is information about this package from the header.", this).Show();
        }

        private void TrashEntryAndChildren(object obj)
        {
            if (GetSelected(out int n))
            {
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;

                List<TreeViewEntry> itemsToTrash = selected.FlattenTree();
                itemsToTrash.OrderByDescending(x => x.UIndex);

                if (itemsToTrash[0].Entry is ImportEntry)
                {
                    MessageBox.Show("Cannot trash a tree only containing imports.\nTrashing only works if there is at least one export in the subtree.");
                    return;
                }

                IExportEntry existingTrashTopLevel = Pcc.Exports.FirstOrDefault(x => x.idxLink == 0 && x.ObjectName == "ME3ExplorerTrashPackage");
                ImportEntry packageImport = Pcc.Imports.First(x => x.GetFullPath == "Core.Package");
                foreach (TreeViewEntry entry in itemsToTrash)
                {
                    IExportEntry newTrash = TrashEntry(entry.Entry, existingTrashTopLevel, packageImport.UIndex);
                    if (existingTrashTopLevel == null) existingTrashTopLevel = newTrash;
                }
            }
        }

        /// <summary>
        /// Trashes an entry.
        /// </summary>
        /// <param name="entry">Entry to trash</param>
        /// <param name="trashContainer">Container for trash. Pass null if you want to create the trash container from the passed in value.</param>
        /// <param name="packageClassIdx">Idx for package class. Prevents multiple calls to find it</param>
        /// <returns>New trash container, otherwise will be null</returns>
        private IExportEntry TrashEntry(IEntry entry, IExportEntry trashContainer, int packageClassIdx)
        {
            if (entry is ImportEntry imp)
            {
                imp.idxClassName = packageClassIdx;
                imp.idxPackageFile = Pcc.FindNameOrAdd("Core");
                imp.idxLink = trashContainer.UIndex;
                imp.idxObjectName = Pcc.FindNameOrAdd("Trash");
                imp.indexValue = 0;
            }
            if (entry is IExportEntry exp)
            {
                exp.Data = new byte[exp.Data.Length]; //Write all zeros to nullify the existing data. For DLC this will allow it to compress better in 7z
                MemoryStream trashData = new MemoryStream();
                trashData.WriteInt32(-1);
                trashData.WriteInt32(Pcc.findName("None"));
                trashData.WriteInt32(0);
                exp.Data = trashData.ToArray();
                exp.idxArchtype = 0;
                exp.idxClassParent = 0;
                exp.indexValue = 0;
                exp.idxClass = packageClassIdx;
                if (trashContainer == null)
                {
                    exp.idxObjectName = Pcc.FindNameOrAdd("ME3ExplorerTrashPackage");
                    exp.idxLink = 0;
                    if (exp.idxLink == exp.UIndex)
                    {
                        Debugger.Break();
                    }
                    //Write trash GUID
                    exp.ObjectFlags &= (ulong)~EObjectFlags.HasStack;
                    Guid trashGuid = ToGuid("ME3ExpTrashPackage"); //DO NOT EDIT THIS!!
                    byte[] header = exp.GetHeader();
                    int preguidcountoffset = Pcc.Game == MEGame.ME3 ? 0x2C : 0x30;
                    int count = BitConverter.ToInt32(header, preguidcountoffset);
                    int headerguidoffset = (preguidcountoffset + 4) + (count * 4);
                    SharedPathfinding.WriteMem(header, headerguidoffset, trashGuid.ToByteArray());
                    exp.Header = header;
                    return exp;
                }
                else
                {
                    exp.idxLink = trashContainer.UIndex;
                    if (exp.idxLink == exp.UIndex)
                    {
                        Debugger.Break();
                    }
                    exp.idxObjectName = Pcc.FindNameOrAdd("Trash");
                    exp.ObjectFlags &= (ulong)~EObjectFlags.HasStack;

                    byte[] header = exp.GetHeader();
                    int preguidcountoffset = Pcc.Game == MEGame.ME3 ? 0x2C : 0x30;
                    int count = BitConverter.ToInt32(header, preguidcountoffset);
                    int headerguidoffset = (preguidcountoffset + 4) + (count * 4);

                    SharedPathfinding.WriteMem(header, headerguidoffset, new byte[16]); //erase guid
                    exp.Header = header;
                }
            }
            return null;
        }

        public static Guid ToGuid(string src)
        {
            byte[] stringbytes = Encoding.UTF8.GetBytes(src);
            byte[] hashedBytes = new System.Security.Cryptography
                .SHA1CryptoServiceProvider()
                .ComputeHash(stringbytes);
            Array.Resize(ref hashedBytes, 16);
            return new Guid(hashedBytes);
        }

        private void ReindexObjectByName(object obj)
        {
            IExportEntry exp = null;
            if (CurrentView == CurrentViewMode.Exports && LeftSide_ListView.SelectedItem is IExportEntry)
            {
                exp = LeftSide_ListView.SelectedItem as IExportEntry;
            }
            if (CurrentView == CurrentViewMode.Tree && LeftSide_TreeView.SelectedItem is TreeViewEntry tvi && tvi.Entry is IExportEntry)
            {
                exp = (LeftSide_TreeView.SelectedItem as TreeViewEntry).Entry as IExportEntry;
            }
            ReindexObjectsByName(exp, true);
        }

        private void ReindexObjectsByName(IExportEntry exp, bool showUI)
        {
            if (exp != null)
            {
                bool trueShowUI = showUI;
                string objectname = exp.ObjectName;
                if (showUI)
                {
                    showUI = MessageBox.Show("Confirm reindexing of all exports with object name:\n" + objectname + "\n\nEnsure this file has a backup - this operation will make many changes to export indexes!",
                                         "Confirm Reindexing",
                                         MessageBoxButton.YesNo) == MessageBoxResult.Yes;
                }
                if (!showUI)
                {
                    // Get list of all exports with that object name.
                    //List<IExportEntry> exports = new List<IExportEntry>();
                    //Could use LINQ... meh.

                    int index = 1; //we'll start at 1.
                    foreach (IExportEntry export in Pcc.Exports)
                    {
                        if (objectname == export.ObjectName && export.ClassName != "Class")
                        {
                            export.indexValue = index;
                            index++;
                        }
                    }
                }
                if (showUI)
                {
                    MessageBox.Show("Objects named \"" + objectname + "\" have been reindexed.", "Reindexing completed");
                }
            }
        }

        private void CopyName(object obj)
        {
            try
            {
                Clipboard.SetText((LeftSide_ListView.SelectedItem as IndexedName).Name.Name);
            }
            catch (Exception)
            {
                //don't bother, clippy is not having it today
            }
        }

        private bool DoesSelectedItemHaveEmbeddedFile(object obj)
        {
            IExportEntry exp = null;
            if (CurrentView == CurrentViewMode.Exports && LeftSide_ListView.SelectedItem is IExportEntry)
            {
                exp = LeftSide_ListView.SelectedItem as IExportEntry;
            }
            if (CurrentView == CurrentViewMode.Tree && LeftSide_TreeView.SelectedItem is TreeViewEntry tvi && tvi.Entry is IExportEntry)
            {
                exp = (LeftSide_TreeView.SelectedItem as TreeViewEntry).Entry as IExportEntry;
            }

            if (exp != null)
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        return true;
                }
            }
            return false;
        }

        private void ExportEmbeddedFile(object obj)
        {
            IExportEntry exp = null;
            if (CurrentView == CurrentViewMode.Exports && LeftSide_ListView.SelectedItem is IExportEntry)
            {
                exp = LeftSide_ListView.SelectedItem as IExportEntry;
            }
            if (CurrentView == CurrentViewMode.Tree && LeftSide_TreeView.SelectedItem is TreeViewEntry tvi && tvi.Entry is IExportEntry)
            {
                exp = (LeftSide_TreeView.SelectedItem as TreeViewEntry).Entry as IExportEntry;
            }

            if (exp != null)
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        try
                        {
                            var props = exp.GetProperties();
                            string dataPropName = exp.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
                            ArrayProperty<ByteProperty> rawData = props.GetProp<ArrayProperty<ByteProperty>>(dataPropName);
                            byte[] data = new byte[rawData.Count];
                            for (int i = 0; i < rawData.Count; i++)
                            {
                                data[i] = rawData[i].Value;
                            }
                            SaveFileDialog d = new SaveFileDialog();
                            d.Title = "Save SWF";
                            d.FileName = exp.GetFullPath + ".swf";
                            string extension = Path.GetExtension(".swf");
                            d.Filter = $"*{extension}|*{extension}";
                            var result = d.ShowDialog();
                            if (result.HasValue && result.Value)
                            {
                                File.WriteAllBytes(d.FileName, data);
                                MessageBox.Show("Done");
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error reading/saving SWF data:\n\n" + ExceptionHandlerDialogWPF.FlattenException(ex));
                        }
                        break;
                }
            }
        }

        private void ImportEmbeddedFile(object obj)
        {

            IExportEntry exp = null;
            if (CurrentView == CurrentViewMode.Exports && LeftSide_ListView.SelectedItem is IExportEntry)
            {
                exp = LeftSide_ListView.SelectedItem as IExportEntry;
            }
            if (CurrentView == CurrentViewMode.Tree && LeftSide_TreeView.SelectedItem is TreeViewEntry tvi && tvi.Entry is IExportEntry)
            {
                exp = (LeftSide_TreeView.SelectedItem as TreeViewEntry).Entry as IExportEntry;
            }

            if (exp != null)
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        try
                        {
                            OpenFileDialog d = new OpenFileDialog();
                            d.Title = "Replace SWF";
                            d.FileName = exp.GetFullPath + ".swf";
                            string extension = Path.GetExtension(".swf");
                            d.Filter = $"*{extension}|*{extension}";
                            var result = d.ShowDialog();
                            if (result.HasValue && result.Value)
                            {
                                var bytes = File.ReadAllBytes(d.FileName);
                                var props = exp.GetProperties();

                                string dataPropName = exp.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
                                ArrayProperty<ByteProperty> rawData = props.GetProp<ArrayProperty<ByteProperty>>(dataPropName);
                                rawData.Clear();

                                //Write SWF data
                                for (int i = 0; i < bytes.Count(); i++)
                                {
                                    rawData.Add(new ByteProperty(bytes[i])); //wonder if there is a faster way to do this - it seems kind of slow.
                                }

                                //Write SWF metadata
                                if (exp.FileRef.Game == MEGame.ME1 || exp.FileRef.Game == MEGame.ME2)
                                {
                                    string sourceFilePropName = exp.FileRef.Game != MEGame.ME1 ? "SourceFile" : "SourceFilePath";
                                    StrProperty sourceFilePath = props.GetProp<StrProperty>(sourceFilePropName);
                                    if (sourceFilePath == null)
                                    {
                                        sourceFilePath = new StrProperty(d.FileName, sourceFilePropName);
                                        props.Add(sourceFilePath);
                                    }
                                    sourceFilePath.Value = d.FileName;
                                }

                                if (exp.FileRef.Game == MEGame.ME1)
                                {
                                    StrProperty sourceFileTimestamp = props.GetProp<StrProperty>("SourceFileTimestamp");
                                    sourceFileTimestamp = File.GetLastWriteTime(d.FileName).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                }
                                exp.WriteProperties(props);
                                MessageBox.Show("Done");
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error reading/setting SWF data:\n\n" + ExceptionHandlerDialogWPF.FlattenException(ex));
                        }
                        break;
                }
            }
        }

        private void RebuildStreamingLevels(object obj)
        {
            try
            {
                var levelStreamingKismets = new List<IExportEntry>();
                IExportEntry bioworldinfo = null;
                foreach (IExportEntry exp in Pcc.Exports)
                {
                    if (exp.ClassName == "BioWorldInfo" && exp.ObjectName == "BioWorldInfo")
                    {
                        bioworldinfo = exp;
                        continue;
                    }
                    if (exp.ClassName == "LevelStreamingKismet" && exp.ObjectName == "LevelStreamingKismet")
                    {
                        levelStreamingKismets.Add(exp);
                        continue;
                    }
                }
                levelStreamingKismets = levelStreamingKismets.OrderBy(o => o.GetProperty<NameProperty>("PackageName").ToString()).ToList();
                if (bioworldinfo != null)
                {
                    var streamingLevelsProp = bioworldinfo.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels");
                    if (streamingLevelsProp == null)
                    {
                        //couldn't find...
                        streamingLevelsProp = new ArrayProperty<ObjectProperty>(ArrayType.Object, "StreamingLevels");
                    }
                    streamingLevelsProp.Clear();
                    foreach (IExportEntry exp in levelStreamingKismets)
                    {
                        streamingLevelsProp.Add(new ObjectProperty(exp.UIndex));
                    }
                    bioworldinfo.WriteProperty(streamingLevelsProp);
                    MessageBox.Show("Done.");
                }
                else
                {
                    MessageBox.Show("No BioWorldInfo object found in this file.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error setting streaming levels:\n" + ex.Message);
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

        private void AddName(object obj)
        {
            string input = "Enter a new name.";
            string result = PromptDialog.Prompt(this, input, "Enter new name");
            if (result != null && result != "")
            {
                int idx = Pcc.FindNameOrAdd(result);
                if (idx != Pcc.Names.Count - 1)
                {
                    //not the last
                    if (CurrentView == CurrentViewMode.Names)
                    {
                        LeftSide_ListView.SelectedIndex = idx;
                    }
                    MessageBox.Show($"{result} already exists in this package file.\nName index: {idx} (0x{idx:X8})", "Name already exists");
                }
                else
                {
                    if (CurrentView == CurrentViewMode.Names)
                    {
                        LeftSide_ListView.SelectedIndex = idx;
                    }
                    MessageBox.Show($"{result} has been added as a name.\nName index: {idx} (0x{idx:X8})", "Name added");
                }
            }
        }

        private bool CanAddName(object obj)
        {
            if (obj is string parameter)
            {
                if (parameter == "FromTreeView")
                {
                    //Ensure we are on names view - used for menu item
                    return PackageIsLoaded(null) && CurrentView == CurrentViewMode.Names;
                }
            }
            return PackageIsLoaded(null);
        }

        private bool TreeEntryIsSelected(object obj)
        {
            return CurrentView == CurrentViewMode.Tree && EntryIsSelected(null);
        }

        private bool NameIsSelected(object obj)
        {
            return (CurrentView == CurrentViewMode.Names && LeftSide_ListView.SelectedItem is IndexedName);
        }

        private void EditName(object obj)
        {
            string input = $"Enter a new name to replace this name ({(LeftSide_ListView.SelectedItem as IndexedName).Name.Name}) with.";
            string result = PromptDialog.Prompt(this, input, "Enter new name", defaultValue: (LeftSide_ListView.SelectedItem as IndexedName).Name.Name, selectText: true);
            if (result != null && result != "")
            {
                Pcc.replaceName(LeftSide_ListView.SelectedIndex, result);
            }
        }

        private void CheckForDuplicateIndexes(object obj)
        {
            if (Pcc == null)
            {
                return;
            }
            List<string> duplicates = new List<string>();
            Dictionary<string, List<int>> nameIndexDictionary = new Dictionary<string, List<int>>();
            foreach (IExportEntry exp in Pcc.Exports)
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
                ListDialog lw = new ListDialog(duplicates, "Duplicate indexes", "The following items have duplicate indexes.", this);
                lw.Show();
            }
            else
            {
                MessageBox.Show("No duplicate indexes were found.", "Indexing OK");
            }
        }

        private void FindEntryViaOffset(object obj)
        {
            if (Pcc == null)
            {
                return;
            }
            string input = "Enter an offset (in hex, e.g. 2FA360) to find what entry contains that offset.";
            string result = PromptDialog.Prompt(this, input, "Enter offset");
            if (result != null)
            {
                try
                {
                    int offsetDec = int.Parse(result, System.Globalization.NumberStyles.HexNumber);

                    //TODO: Fix offset selection code, it seems off by a bit, not sure why yet
                    for (int i = 0; i < Pcc.ImportCount; i++)
                    {
                        ImportEntry imp = Pcc.Imports[i];
                        if (offsetDec >= imp.HeaderOffset && offsetDec < imp.HeaderOffset + imp.Header.Length)
                        {
                            GoToNumber(imp.UIndex);
                            Metadata_Tab.IsSelected = true;
                            MetadataTab_MetadataEditor.SetHexboxSelectedOffset(imp.HeaderOffset + imp.Header.Length - offsetDec);
                            return;
                        }
                    }
                    for (int i = 0; i < Pcc.ExportCount; i++)
                    {
                        IExportEntry exp = Pcc.Exports[i];
                        //header
                        if (offsetDec >= exp.HeaderOffset && offsetDec < exp.HeaderOffset + exp.Header.Length)
                        {
                            GoToNumber(exp.UIndex);
                            Metadata_Tab.IsSelected = true;
                            MetadataTab_MetadataEditor.SetHexboxSelectedOffset(exp.HeaderOffset + exp.Header.Length - offsetDec);
                            return;
                        }

                        //data
                        if (offsetDec >= exp.DataOffset && offsetDec < exp.DataOffset + exp.DataSize)
                        {
                            GoToNumber(exp.UIndex);
                            int inExportDataOffset = exp.DataOffset + exp.DataSize - offsetDec;
                            int propsEnd = exp.propsEnd();

                            if (inExportDataOffset > propsEnd && exp.DataSize > propsEnd && BinaryInterpreterTab_BinaryInterpreter.CanParse(exp))
                            {
                                BinaryInterpreterTab_BinaryInterpreter.SetHexboxSelectedOffset(inExportDataOffset);
                                BinaryInterpreter_Tab.IsSelected = true;
                            }
                            else
                            {
                                InterpreterTab_Interpreter.SetHexboxSelectedOffset(inExportDataOffset);
                                Interpreter_Tab.IsSelected = true;
                            }
                            return;
                        }
                    }
                    MessageBox.Show($"No entry or header containing offset 0x{result} was found.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
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
                GoToNumber(nextIndex);
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
                GoToNumber(newEntry.UIndex);
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
            IExportEntry export = Pcc.getEntry(n) as IExportEntry;
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
            IExportEntry export = Pcc.getEntry(n) as IExportEntry;
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
            return false;
        }

        private bool PackageExportIsSelected(object obj)
        {
            TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
            if (selected != null && selected.Entry != null && selected.Entry.ClassName == "Package")
            {
                return true;
            }
            return false;
        }

        private bool ImportIsSelected(object obj)
        {
            int n;
            if (GetSelected(out n))
            {
                return n < 0;
            }
            return false;
        }

        private bool EntryIsSelected(object obj)
        {
            int n;
            if (GetSelected(out n))
            {
                return n != 0;
            }
            return false;
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
                            //foreach (byte b in header1)
                            //{
                            //    Debug.Write(" " + b.ToString("X2"));
                            //}
                            //Debug.WriteLine("");
                            //foreach (byte b in header2)
                            //{
                            //    //Debug.Write(" " + b.ToString("X2"));
                            //}
                            //Debug.WriteLine("");
                            changedExports.Add("Export header has changed: " + exp1.UIndex + " " + exp1.GetFullPath);
                        }
                        if (!exp1.Data.SequenceEqual(exp2.Data))
                        {
                            changedExports.Add("Export data has changed: " + exp1.UIndex + " " + exp1.GetFullPath);
                        }
                    }

                    IMEPackage enumerateExtras = Pcc;
                    string file = "this file";
                    if (compareFile.ExportCount > numExportsToEnumerate)
                    {
                        file = "other file";
                        enumerateExtras = compareFile;
                    }

                    for (int i = numExportsToEnumerate; i < enumerateExtras.ExportCount; i++)
                    {
                        Debug.WriteLine("Export only exists in " + (file + ": " + (i + 1)) + " " + enumerateExtras.Exports[i].GetFullPath);
                        changedExports.Add("Export only exists in " + file + ": " + (i + 1) + " " + enumerateExtras.Exports[i].GetFullPath);
                    }

                    sw.Stop();
                    Debug.WriteLine("Time: " + sw.ElapsedMilliseconds + "ms");

                    ListDialog ld = new ListDialog(changedExports, "Changed exports between files", "The following exports are different between the files.", this);
                    ld.Show();
                }
            }
        }
        #endregion

        public PackageEditorWPF()
        {
            //ME3UnrealObjectInfo.generateInfo();
            CurrentView = CurrentViewMode.Tree;
            LoadCommands();

            InitializeComponent();
            //map export loaders to their tabs
            ExportLoaders[InterpreterTab_Interpreter] = Interpreter_Tab;
            ExportLoaders[MetadataTab_MetadataEditor] = Metadata_Tab;
            ExportLoaders[SoundTab_Soundpanel] = Sound_Tab;
            ExportLoaders[CurveTab_CurveEditor] = CurveEditor_Tab;
            ExportLoaders[Bio2DATab_Bio2DAEditor] = Bio2DAViewer_Tab;
            ExportLoaders[ScriptTab_UnrealScriptEditor] = Script_Tab;
            ExportLoaders[BinaryInterpreterTab_BinaryInterpreter] = BinaryInterpreter_Tab;
            InterpreterTab_Interpreter.SetParentNameList(NamesList); //reference to this control for name editor set
            BinaryInterpreterTab_BinaryInterpreter.SetParentNameList(NamesList); //reference to this control for name editor set
            RecentButtons.AddRange(new Button[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10, });
            LoadRecentList();
            RefreshRecent(false);
        }

        public void LoadFile(string s)
        {
            try
            {
                BusyText = "Loading " + System.IO.Path.GetFileName(s);
                IsBusy = true;
                IsLoadingFile = true;
                foreach (KeyValuePair<ExportLoaderControl, TabItem> entry in ExportLoaders)
                {
                    entry.Value.Visibility = Visibility.Collapsed;
                }
                Metadata_Tab.Visibility = Visibility.Collapsed;
                Intro_Tab.Visibility = Visibility.Visible;
                Intro_Tab.IsSelected = true;

                AllTreeViewNodesX.ClearEx();
                NamesList.ClearEx();
                ClassDropdownList.ClearEx();

                currentFile = s;
                StatusBar_GameID_Container.Visibility = Visibility.Collapsed;
                StatusBar_LeftMostText.Text = "Loading " + System.IO.Path.GetFileName(s) + " (" + ByteSize.FromBytes(new System.IO.FileInfo(s).Length) + ")";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadMEPackage(s);
                StatusBar_GameID_Container.Visibility = Visibility.Visible;
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

                RefreshView();
                InitStuff();
                StatusBar_LeftMostText.Text = System.IO.Path.GetFileName(s);
                Title = "Package Editor WPF - " + System.IO.Path.GetFileName(s);
                InterpreterTab_Interpreter.UnloadExport();
                //InitializeTreeView();

                BackgroundWorker bg = new BackgroundWorker();
                bg.DoWork += InitializeTreeViewBackground;
                bg.RunWorkerCompleted += InitializeTreeViewBackground_Completed;
                bg.RunWorkerAsync();

                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            catch (Exception e)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + System.IO.Path.GetFileName(s);
                MessageBox.Show("Error loading " + System.IO.Path.GetFileName(s) + ":\n" + e.Message);
                IsBusy = false;
                IsBusyTaskbar = false;
                //throw e;
            }
        }

        private void InitializeTreeViewBackground_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result != null)
            {
                AllTreeViewNodesX.ClearEx();
                AllTreeViewNodesX.AddRange(e.Result as ObservableCollectionExtended<TreeViewEntry>);
            }
            IsLoadingFile = false;
            if (QueuedGotoNumber != 0)
            {
                //Wait for UI to render
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Background, null);
                BusyText = $"Navigating to {QueuedGotoNumber}";
                GoToNumber(QueuedGotoNumber);
                if (QueuedGotoNumber > 0)
                {
                    Interpreter_Tab.IsSelected = true;
                }
                QueuedGotoNumber = 0;
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

            int i = 0;
            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                RecentButtons[i].Visibility = Visibility.Visible;
                RecentButtons[i].Content = System.IO.Path.GetFileName(filepath.Replace("_", "__"));
                RecentButtons[i].Click -= RecentFile_click;
                RecentButtons[i].Click += RecentFile_click;
                RecentButtons[i].Tag = filepath;
                RecentButtons[i].ToolTip = filepath;
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
                i++;
            }
            while (i < 10)
            {
                RecentButtons[i].Visibility = Visibility.Collapsed;
                i++;
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((FrameworkElement)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);

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

        /// <summary>
        /// Updates the data bindings for tree/list view and chagnes visibility of the tree/list view depending on what the currentview mode is. Also forces refresh of all treeview display names
        /// </summary>
        private void RefreshView()
        {
            if (Pcc == null)
            {
                return;
            }

            if (CurrentView == CurrentViewMode.Names)
            {
                LeftSideList_ItemsSource.ReplaceAll(NamesList);
            }

            if (CurrentView == CurrentViewMode.Imports)
            {
                LeftSideList_ItemsSource.ReplaceAll(Pcc.Imports);
            }

            if (CurrentView == CurrentViewMode.Exports)
            {
                LeftSideList_ItemsSource.ReplaceAll(Pcc.Exports);
            }

            if (CurrentView == CurrentViewMode.Tree)
            {
                if (AllTreeViewNodesX.Count > 0)
                {
                    foreach (TreeViewEntry tv in AllTreeViewNodesX[0].FlattenTree())
                    {
                        tv.RefreshDisplayName();
                    }
                }
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

            //Get a list of all classes for objects
            //Filter out duplicates
            //Get their objectnames from the name list
            //Order it ascending
            ClassDropdownList.ReplaceAll(Pcc.Exports.Select(x => x.idxClass).Distinct().Select(Pcc.getObjectName).ToList().OrderBy(p => p));
            MetadataTab_MetadataEditor.LoadPccData(Pcc);
            RefreshNames();
            if (CurrentView != CurrentViewMode.Tree)
            {
                RefreshView(); //Tree will initialize itself in thread
            }
        }

        private void TreeView_Click(object sender, RoutedEventArgs e)
        {
            CurrentView = CurrentViewMode.Tree;
            RefreshView();
        }
        private void NamesView_Click(object sender, RoutedEventArgs e)
        {
            CurrentView = CurrentViewMode.Names;
            RefreshView();
        }
        private void ImportsView_Click(object sender, RoutedEventArgs e)
        {
            CurrentView = CurrentViewMode.Imports;
            RefreshView();
        }
        private void ExportsView_Click(object sender, RoutedEventArgs e)
        {
            CurrentView = CurrentViewMode.Exports;
            RefreshView();
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
            if (CurrentView == CurrentViewMode.Tree && LeftSide_TreeView.SelectedItem != null)
            {
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                n = Convert.ToInt32(selected.UIndex);
                return true;
            }
            else if (CurrentView == CurrentViewMode.Exports && LeftSide_ListView.SelectedItem != null)
            {
                n = LeftSide_ListView.SelectedIndex + 1; //to unreal indexing
                return true;
            }
            else if (CurrentView == CurrentViewMode.Imports && LeftSide_ListView.SelectedItem != null)
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

            //we might need to identify parent depths and add those first
            List<PackageUpdate> addedChanges = updates.Where(x => x.change == PackageChange.ExportAdd || x.change == PackageChange.ImportAdd).OrderBy(x => x.index).ToList();
            List<int> headerChanges = updates.Where(x => x.change == PackageChange.ExportHeader).Select(x => x.index).OrderBy(x => x).ToList();
            if (addedChanges.Count > 0)
            {
                //Find nodes that haven't been generated and added yet
                List<PackageUpdate> addedChangesByUIndex = new List<PackageUpdate>();
                foreach (PackageUpdate u in addedChanges)
                {
                    //convert to uindex
                    addedChangesByUIndex.Add(new PackageUpdate { change = u.change, index = u.index >= 0 ? u.index + 1 : u.index });
                }
                var treeViewItems = AllTreeViewNodesX[0].FlattenTree();

                //filter to only nodes that don't exist yet (created by external tools)
                foreach (TreeViewEntry tvi in treeViewItems)
                {
                    addedChangesByUIndex.RemoveAll(x => x.index == tvi.UIndex);
                }

                //Generate new nodes
                var nodesToSortChildrenFor = new HashSet<TreeViewEntry>();
                foreach (PackageUpdate newItem in addedChangesByUIndex)
                {
                    int idx = newItem.change == PackageChange.ExportAdd ? newItem.index : -newItem.index; //make UIndex based
                    IEntry entry = Pcc.getEntry(idx);
                    if (entry == null)
                    {
                        Debugger.Break();
                    }

                    //TreeViewEntry parent = null;
                    //foreach (TreeViewEntry tve in treeViewItems)
                    //{
                    //    Debug.WriteLine(tve.UIndex + " vs " + entry.idxLink);
                    //    if (tve.UIndex == entry.idxLink)
                    //    {
                    //        Debug.WriteLine("FOUND!");
                    //        parent = tve;
                    //        break;
                    //    }
                    //}
                    TreeViewEntry parent = treeViewItems.FirstOrDefault(x => x.UIndex == entry.idxLink);
                    if (parent != null)
                    {
                        TreeViewEntry newEntry = new TreeViewEntry(entry);
                        newEntry.Parent = parent;
                        parent.Sublinks.Add(newEntry);
                        treeViewItems.Add(newEntry); //used to find parents
                        nodesToSortChildrenFor.Add(parent);
                    }
                    //newItem.Parent = targetItem;
                    //targetItem.Sublinks.Add(newItem);
                }

                nodesToSortChildrenFor.ToList().ForEach(x => x.SortChildren());

                int currentLeftSideListMaxCount = LeftSideList_ItemsSource.Count - 1;
                if (CurrentView == CurrentViewMode.Imports)
                {
                    foreach (PackageUpdate update in addedChangesByUIndex)
                    {
                        if (update.index < 0)
                        {
                            LeftSideList_ItemsSource.Add(Pcc.getEntry(update.index));
                        }
                    }
                }

                if (CurrentView == CurrentViewMode.Exports)
                {
                    foreach (PackageUpdate update in addedChangesByUIndex)
                    {
                        if (update.index > 0)
                        {
                            LeftSideList_ItemsSource.Add(Pcc.getEntry(update.index));
                        }
                    }
                }
            }
            if (headerChanges.Count > 0)
            {
                var tree = AllTreeViewNodesX[0].FlattenTree();
                var nodesNeedingResort = new List<TreeViewEntry>();
                List<TreeViewEntry> tviWithChangedHeaders = tree.Where(x => x.UIndex > 0 && headerChanges.Contains(x.Entry.Index)).ToList();
                foreach (TreeViewEntry tvi in tviWithChangedHeaders)
                {
                    if (tvi.Parent.UIndex != tvi.Entry.idxLink)
                    {
                        Debug.WriteLine("Reorder req for " + tvi.UIndex);
                        TreeViewEntry newParent = tree.FirstOrDefault(x => x.UIndex == tvi.Entry.idxLink);
                        if (newParent == null)
                        {
                            Debugger.Break();
                        }
                        else
                        {
                            tvi.Parent.Sublinks.Remove(tvi);
                            tvi.Parent = newParent;
                            newParent.Sublinks.Add(tvi);
                            nodesNeedingResort.Add(newParent);
                        }
                    }
                }
                nodesNeedingResort = nodesNeedingResort.Distinct().ToList();
                nodesNeedingResort.ForEach(x => x.SortChildren());
            }

            if (changes.Contains(PackageChange.Names))
            {
                //reloads names - used by metadata editor control as well as names list
                RefreshNames(updates.Where(x => x.change == PackageChange.Names).ToList());
            }

            if (CurrentView == CurrentViewMode.Imports && importChanges ||
                     CurrentView == CurrentViewMode.Exports && exportNonDataChanges ||
                     CurrentView == CurrentViewMode.Tree && (importChanges || exportNonDataChanges))
            {
                RefreshView();
                if (hasSelection)
                {
                    GoToNumber(n);
                }
            }
            else if ((CurrentView == CurrentViewMode.Exports || CurrentView == CurrentViewMode.Tree) && hasSelection &&
                     updates.Contains(new PackageUpdate { index = n - 1, change = PackageChange.ExportData }))
            {
                Preview(true);
            }
        }

        private void RefreshNames(List<PackageUpdate> updates = null)
        {
            if (updates == null)
            {
                //initial loading
                //we don't update the left side with this
                var indexedList = new List<IndexedName>();
                for (int i = 0; i < Pcc.Names.Count; i++)
                {
                    NameReference nr = Pcc.Names[i];
                    indexedList.Add(new IndexedName(i, nr));
                }
                NamesList.ReplaceAll(indexedList); //we replaceall so we don't add one by one and trigger tons of notifications
            }
            else
            {
                bool shouldUpdateLeftside = CurrentView == CurrentViewMode.Names;

                //only modify the list
                updates = updates.OrderBy(x => x.index).ToList(); //ensure ascending order
                foreach (PackageUpdate update in updates)
                {
                    if (update.index > NamesList.Count - 1) //names are 0 indexed
                    {
                        NameReference nr = Pcc.Names[update.index];
                        NamesList.Add(new IndexedName(update.index, nr));
                        LeftSideList_ItemsSource.Add(new IndexedName(update.index, nr));
                    }
                    else
                    {
                        IndexedName indexed = new IndexedName(update.index, Pcc.Names[update.index]);
                        NamesList[update.index] = indexed;
                        LeftSideList_ItemsSource[update.index] = indexed;

                    }
                }
            }
        }

        /// <summary>
        /// Listbox selected item changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftSide_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            Preview();
        }

        /// <summary>
        /// Prepares the right side of PackageEditorWPF for the current selected entry.
        /// This may take a moment if the data that is being loaded is large or complex.
        /// </summary>
        /// <param name="isRefresh">(needs testing what this does)/param>
        private void Preview(bool isRefresh = false)
        {
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
                MetadataTab_MetadataEditor.ClearMetadataPane();
                Intro_Tab.Visibility = Visibility.Visible;
                Intro_Tab.IsSelected = true;
                return;
            }
            EditorTabs.IsEnabled = true;
            Metadata_Tab.Visibility = Visibility.Visible;
            Intro_Tab.Visibility = Visibility.Collapsed;
            //Debug.WriteLine("New selection: " + n);

            if (CurrentView == CurrentViewMode.Imports || CurrentView == CurrentViewMode.Exports || CurrentView == CurrentViewMode.Tree)
            {
                Interpreter_Tab.IsEnabled = n >= 0;
                if (n >= 0)
                {
                    IExportEntry exportEntry = Pcc.getExport(n - 1);
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


                //import
                else
                {
                    ImportEntry importEntry = Pcc.getImport(-n - 1);
                    MetadataTab_MetadataEditor.LoadImport(importEntry);
                    foreach (KeyValuePair<ExportLoaderControl, TabItem> entry in ExportLoaders)
                    {
                        if (entry.Key != MetadataTab_MetadataEditor)
                        {
                            entry.Value.Visibility = Visibility.Collapsed;
                            entry.Key.UnloadExport();
                        }
                    }
                    Metadata_Tab.IsSelected = true;
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
        }

        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
#if !DEBUG
                try
                {
#endif
                LoadFile(d.FileName);
                AddRecent(d.FileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
#if !DEBUG
            }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
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
                GoToNumber(n);
            }
        }

        /// <summary>
        /// Selects the entry that corresponds to the given index
        /// </summary>
        /// <param name="entryIndex">Unreal-indexed entry number</param>
        public void GoToNumber(int entryIndex)
        {
            if (entryIndex == 0)
            {
                return; //PackageEditorWPF uses Unreal Indexing for entries
            }
            if (IsLoadingFile)
            {
                QueuedGotoNumber = entryIndex;
                return;
            }
            if (CurrentView == CurrentViewMode.Tree)
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
                    selectNode[0].IsProgramaticallySelecting = true;
                    selectNode[0].IsSelected = true;
                    //FocusTreeViewNodeOld(selectNode[0]);

                    //selectNode[0].Focus(LeftSide_TreeView);
                }
                else
                {
                    Debug.WriteLine("Could not find node");
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
        /// Handler for the keyup event while the Goto Textbox is focused. It will issue the Goto button function when the enter key is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Goto_TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && !e.IsRepeat)
            {
                GotoButton_Clicked(null, null);
            }
            else
            {
                if (Goto_TextBox.Text.Length == 0)
                {
                    Goto_Preview_TextBox.Text = "";
                    return;
                }
                if (int.TryParse(Goto_TextBox.Text, out int index))
                {
                    if (index == 0)
                    {
                        Goto_Preview_TextBox.Text = "Invalid value";
                    }
                    else
                    {
                        var entry = Pcc.getEntry(index);
                        if (entry != null)
                        {
                            Goto_Preview_TextBox.Text = entry.GetFullPath;
                        }
                        else
                        {
                            Goto_Preview_TextBox.Text = "Index out of bounds of entry list";
                        }
                    }
                }
                else
                {
                    Goto_Preview_TextBox.Text = "Invalid value";
                }
            }
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
                //Check if the path of the target and the source is the same. If so, offer to merge instead
                crossPCCObjectMap = new SortedDictionary<int, int>();

                TreeViewEntry sourceItem = dropInfo.Data as TreeViewEntry;
                TreeViewEntry targetItem = dropInfo.TargetItem as TreeViewEntry;


                if (sourceItem == targetItem || (targetItem.Entry != null && sourceItem.Entry.FileRef == targetItem.Entry.FileRef))
                {
                    return; //ignore
                }
                var portingOption = TreeMergeDialog.GetMergeType(this, sourceItem, targetItem);

                if (portingOption == TreeMergeDialog.PortingOption.Cancel)
                {
                    return;
                }


                //Debug.WriteLine("Adding source item: " + sourceItem.TagzToString());

                //if (DestinationNode.TreeView != sourceNode.TreeView)
                //{
                IEntry sourceEntry = sourceItem.Entry;
                IEntry targetLinkEntry = targetItem.Entry;

                IMEPackage importpcc = sourceEntry.FileRef;
                if (importpcc == null)
                {
                    return;
                }

                if (portingOption == TreeMergeDialog.PortingOption.ReplaceSingular)
                {
                    //replace data only
                    if (sourceEntry is IExportEntry)
                    {
                        ReplaceExportDataWithAnother(sourceEntry as IExportEntry, targetLinkEntry as IExportEntry);
                    }
                    return;
                }

                int n = sourceEntry.UIndex;
                int link;
                if (targetItem.Parent == null) //dropped on a first level node (root)
                {
                    link = 0;
                }
                else
                {
                    link = targetLinkEntry.UIndex;
                    //link = link >= 0 ? link + 1 : link;
                }
                TreeViewEntry newItem = null;
                if (n >= 0)
                {
                    //importing an export
                    IExportEntry newExport;
                    if (importExport(sourceEntry as IExportEntry, link, out newExport))
                    {
                        newItem = new TreeViewEntry(newExport);
                        crossPCCObjectMap[n - 1] = newExport.Index; //0 based. map old index to new index
                    }
                    else
                    {
                        //import failed!
                        //Todo: Throw error message or something
                        return;
                    }
                }
                else
                {
                    ImportEntry newImport = getOrAddCrossImport(importpcc.getImport(Math.Abs(n) - 1).GetFullPath, importpcc, Pcc, sourceItem.Sublinks.Count == 0 ? link : (int?)null);
                    newItem = new TreeViewEntry(newImport);
                    crossPCCObjectMap[n] = newImport.UIndex; //0 based. map old index to new index
                }
                newItem.Parent = targetItem;
                targetItem.Sublinks.Add(newItem);

                //if this node has children
                if (sourceItem.Sublinks.Count > 0 && portingOption == TreeMergeDialog.PortingOption.CloneTreeAsChild || portingOption == TreeMergeDialog.PortingOption.MergeTreeChildren)
                {
                    importTree(sourceItem, importpcc, newItem, portingOption);
                }

                targetItem.SortChildren();

                //relinkObjects(importpcc);
                List<string> relinkResults = new List<string>();
                relinkResults.AddRange(relinkObjects2(importpcc));
                relinkResults.AddRange(relinkBinaryObjects(importpcc));
                crossPCCObjectMap = null;

                RefreshView();
                GoToNumber(n >= 0 ? Pcc.ExportCount : -Pcc.ImportCount);
                if (relinkResults.Count > 0)
                {
                    ListDialog ld = new ListDialog(relinkResults, "Relink report", "The following items failed to relink.", this);
                    ld.Show();
                }
                else
                {
                    MessageBox.Show("Items have been ported and relinked with no reported issues.\nNote that this does not mean all binary properties were relinked, only supported ones were.");
                }
            }
        }

        private void ReplaceExportDataWithAnother(IExportEntry ex, IExportEntry targetLinkEntry)
        {
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
                //restore namelist in event of failure.
                Pcc.setNames(names);
                MessageBox.Show("Error occured while replacing data in " + ex.ObjectName + " : " + exception.Message);
                return;
            }
            res.Write(idata, end, idata.Length - end);
            targetLinkEntry.Data = res.ToArray();
            MessageBox.Show("Done. Check the resulting export to ensure accuracy of this experimental feature.");
        }


        /// <summary>
        /// Recursive importing function for importing items from another PCC.
        /// </summary>
        /// <param name="sourceNode">Source node from the importing instance of PackageEditorWPF</param>
        /// <param name="importpcc">PCC to import from</param>
        /// <param name="link">The entry link the tree will be imported under</param>
        /// <returns></returns>
        private bool importTree(TreeViewEntry sourceNode, IMEPackage importpcc, TreeViewEntry newItemParent, TreeMergeDialog.PortingOption portingOption)
        {
            int index;
            foreach (TreeViewEntry node in sourceNode.Sublinks)
            {
                index = node.Entry.UIndex;
                TreeViewEntry newEntry = null;

                if (portingOption == TreeMergeDialog.PortingOption.MergeTreeChildren)
                {
                    //we must check to see if there is an item already matching what we are trying to port.

                    //Todo: We may need to enhance target checking here as getfullpath may not be reliable enough. Maybe have to do indexing, or something.
                    TreeViewEntry sameObjInTarget = newItemParent.Sublinks.FirstOrDefault(x => node.Entry.GetFullPath == x.Entry.GetFullPath);
                    if (sameObjInTarget != null)
                    {
                        crossPCCObjectMap[node.Entry.Index] = sameObjInTarget.Entry.Index; //0 based. Make the relink map know about this entry

                        //merge children to this node instead
                        if (node.Sublinks.Count > 0)
                        {
                            if (!importTree(node, importpcc, sameObjInTarget, portingOption))
                            {
                                return false;
                            }
                        }
                        continue;
                    }
                }

                if (index >= 0)
                {
                    index--; //code is written for 0-based indexing, while UIndex is not 0 based
                    IExportEntry importedEntry;
                    if (importExport(node.Entry as IExportEntry, newItemParent.UIndex, out importedEntry))
                    {
                        newEntry = new TreeViewEntry(importedEntry);
                        crossPCCObjectMap[node.Entry.UIndex - 1] = importedEntry.Index; //0 based. map old index to new index
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    //todo: ensure relink works with this
                    ImportEntry newImport = getOrAddCrossImport(importpcc.getImport(Math.Abs(index) - 1).GetFullPath, importpcc, Pcc);

                    //nextIndex = -Pcc.ImportCount;

                    //ImportEntry newImport = Pcc.Imports[nextIndex - 1]; //0 based
                    newEntry = new TreeViewEntry(newImport);
                    crossPCCObjectMap[index] = newImport.UIndex; //0 based. map old index to new index
                }
                newEntry.Parent = newItemParent;
                newItemParent.Sublinks.Add(newEntry); //TODO: Resort the children so they display in the proper order

                if (node.Sublinks.Count > 0)
                {
                    if (!importTree(node, importpcc, newEntry, portingOption))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Imports an export from another package file.
        /// </summary>
        /// <param name="ex">Export object from the other package to import</param>
        /// <param name="link">Local parent node UIndex</param>
        /// <param name="outputEntry">Newly generated export entry reference</param>
        /// <returns></returns>
        private bool importExport(IExportEntry ex, int link, out IExportEntry outputEntry)
        {
            outputEntry = null; //required assignemnt
            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    outputEntry = new ME1ExportEntry(Pcc as ME1Package);
                    break;
                case MEGame.ME2:
                    outputEntry = new ME2ExportEntry(Pcc as ME2Package);
                    break;
                case MEGame.ME3:
                    outputEntry = new ME3ExportEntry(Pcc as ME3Package);
                    break;
                case MEGame.UDK:
                    outputEntry = new UDKExportEntry(Pcc as UDKPackage);
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
                //restore namelist in event of failure.
                Pcc.setNames(names);
                MessageBox.Show("Error occured while trying to import " + ex.ObjectName + " : " + exception.Message);
                outputEntry = null;
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
            outputEntry.Header = header;
            bool dataAlreadySet = false;
           
            if (ex.FileRef.Game == MEGame.UDK)
            {
                //todo: move to binary relinker
                switch (ex.FileRef.getObjectName(ex.idxClass))
                {
                    case "StaticMesh":
                        {
                            //res.Write(idata, end, idata.Length - end);
                            //rewrite data
                            outputEntry.Data = res.ToArray();
                            UDKStaticMesh usm = new UDKStaticMesh(ex.FileRef as UDKPackage, ex.Index);
                            usm.PortToME3Export(outputEntry);
                            dataAlreadySet = true;
                            break;
                        }
                    default:
                        //Write binary
                        res.Write(idata, end, idata.Length - end);
                        break;
                }
            } else {
                switch (ex.FileRef.getObjectName(ex.idxClass))
                {
                    //Todo: Move this to binary relinker
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
                outputEntry.Data = res.ToArray();
            }
            outputEntry.idxClass = classValue;
            outputEntry.idxObjectName = Pcc.FindNameOrAdd(ex.FileRef.getNameEntry(ex.idxObjectName));
            outputEntry.idxLink = link;
            outputEntry.idxArchtype = archetype;
            outputEntry.idxClassParent = 0;
            Pcc.addExport(outputEntry);

            return true;
        }

        /// <summary>
        /// Handles pressing the enter key when the class dropdown is active. Automatically will attempt to find the next object by class.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClassDropdown_Combobox_OnKeyUpHandler(object sender, KeyEventArgs e)
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
            //TODO: Implement for Imports, Exports

            if (CurrentView == CurrentViewMode.Tree)
            {
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

                    if (node.Entry.ClassName.Equals(searchClass))
                    {
                        node.IsProgramaticallySelecting = true;
                        node.IsSelected = true;
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
        private void Searchbox_OnKeyUpHandler(object sender, KeyEventArgs e)
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
            if (CurrentView == CurrentViewMode.Names)
            {
                for (int i = start, numSearched = 0; numSearched < Pcc.Names.Count; i++, numSearched++)
                {
                    if (Pcc.Names[i].ToLower().Contains(searchTerm))
                    {
                        LeftSide_ListView.SelectedIndex = i;
                        break;
                    }
                    if (i >= Pcc.Names.Count - 1)
                    {
                        i = -1;
                    }
                }
            }
            if (CurrentView == CurrentViewMode.Imports)
            {
                IReadOnlyList<ImportEntry> Imports = Pcc.Imports;
                for (int i = start, numSearched = 0; numSearched < Imports.Count; i++, numSearched++)
                {
                    if (Imports[i].ObjectName.ToLower().Contains(searchTerm))
                    {
                        LeftSide_ListView.SelectedIndex = i;
                        break;
                    }
                    if (i >= Imports.Count - 1)
                    {
                        i = -1;
                    }
                }
            }
            if (CurrentView == CurrentViewMode.Exports)
            {
                IReadOnlyList<IExportEntry> Exports = Pcc.Exports;
                for (int i = start, numSearched = 0; numSearched < Exports.Count; i++, numSearched++)
                {
                    if (Exports[i].ObjectName.ToLower().Contains(searchTerm))
                    {
                        LeftSide_ListView.SelectedIndex = i;
                        break;
                    }
                    if (i >= Exports.Count - 1)
                    {
                        i = -1;
                    }
                }
            }
            if (CurrentView == CurrentViewMode.Tree)
            {
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
                    if (node.Entry.ObjectName.ToLower().Contains(searchTerm))
                    {
                        node.IsProgramaticallySelecting = true;
                        node.IsSelected = true;
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
            TreeViewItem container = null;
            var generator = LeftSide_TreeView.ItemContainerGenerator;
            while (stack.Count > 0)
            {
                //pop the next child off the stack
                var dequeue = stack.Pop();
                var index = generator.Items.IndexOf(dequeue);

                //see if the container is already loaded. If not, we will have to generate them until we get it (why microsoft...)
                TreeViewItem treeViewItem = generator.ContainerFromIndex(index) as TreeViewItem;
                //if (treeViewItem == null && container != null) treeViewItem = GetTreeViewItem(container, dequeue);
                Action action = () => { treeViewItem?.BringIntoView(); };
                //This needs to be stress tested - this can cause deadlock, but if it doesn't return fast enough the code
                //may continue to null and not work.
                //Sigh, treeview.
                Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
                if (treeViewItem == null)
                {
                    Debug.WriteLine("This shoudln't be null");
                }

                if (stack.Count > 0)
                {
                    action = () => { if (treeViewItem != null) treeViewItem.IsExpanded = true; };
                    Dispatcher.Invoke(action, DispatcherPriority.ContextIdle);
                }
                else
                {
                    if (treeViewItem == null)
                    {
                        //Hope this doesn't happen anymore.
                        Debug.WriteLine("FocusNode has triggered null item - CANNOT FOCUS!");
                        //Debugger.Break();
                    }
                    else
                    {
                        treeViewItem.BringIntoView();
                    }
                }
                if (treeViewItem != null)
                {
                    container = treeViewItem;
                    generator = treeViewItem.ItemContainerGenerator;
                }
            }
        }
        /*

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
                            subContainer = (TreeViewItem)container.ItemContainerGenerator.ContainerFromIndex(i); //find item
                            if (subContainer.DataContext == item) return subContainer;
                        }
                        else
                        {
                            subContainer = (TreeViewItem)container.ItemContainerGenerator.ContainerFromIndex(i);
                            subContainer.BringIntoView();
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
                } */

        private void TouchComfyMode_Clicked(object sender, RoutedEventArgs e)
        {
            TouchComfyMode_MenuItem.IsChecked = !TouchComfyMode_MenuItem.IsChecked;
            TreeViewMargin = TouchComfyMode_MenuItem.IsChecked ? 5 : 2;
        }

        private void PackageEditorWPF_Closing(object sender, CancelEventArgs e)
        {
            SoundTab_Soundpanel.FreeAudioResources();
            //System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            //GC.Collect();
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

        private void TLKManager_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            TlkManager tlk = new TlkManager();
            tlk.Show();
        }

        private void HexConverterMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string loc =
                System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            if (File.Exists(loc + @"\HexConverterWPF.exe"))
            {
                Process.Start(loc + @"\HexConverterWPF.exe");
            }
        }

        public class IndexedName
        {
            public int Index { get; set; }
            public NameReference Name { get; set; }

            public IndexedName(int index, NameReference name)
            {
                Index = index;
                Name = name;
            }
        }

        private void BinaryInterpreterWPF_AlwaysAutoParse_Click(object sender, RoutedEventArgs e)
        {
            //BinaryInterpreterWPF_AlwaysAutoParse_MenuItem.IsChecked = !BinaryInterpreterWPF_AlwaysAutoParse_MenuItem.IsChecked;
            Properties.Settings.Default.BinaryInterpreterWPFAutoScanAlways = !Properties.Settings.Default.BinaryInterpreterWPFAutoScanAlways;
            Properties.Settings.Default.Save();
        }

        private void Port_SFXObjectives_Click(object sender, RoutedEventArgs e)
        {
            /*int offsetx = -9990 - 38713;
            int standingz = 803;
            int offsety = -809 - 1589;
            foreach (IExportEntry exp in Pcc.Exports)
            {
                StructProperty locationProp = exp.GetProperty<StructProperty>("location");
                if (locationProp != null)
                {
                    FloatProperty xProp = locationProp.GetProp<FloatProperty>("X");
                    FloatProperty yProp = locationProp.GetProp<FloatProperty>("Y");
                    //FloatProperty zProp = locationProp.GetProp<FloatProperty>("Z");
                    //Debug.WriteLine("Original coordinate of objective: " + xProp.Value + "," + yProp.Value + "," + zProp.Value);

                    xProp.Value += -600;
                    yProp.Value += 3000;
                    //zProp.Value = standingz;

                    //Debug.WriteLine("--New coordinate for positioning: " + xPos + "," + y + "," + z);

                    //xPos += 55;
                    exp.WriteProperty(locationProp);
                }
            }

            return;*/
            if (Pcc == null)
            {
                return;
            }
            OpenFileDialog d = new OpenFileDialog { Title = "Select source file", Filter = "*.pcc|*.pcc" };
            bool? result = d.ShowDialog();
            IMEPackage sourceFile = null;

            if (result.HasValue && result.Value)
            {
                if (d.FileName == Pcc.FileName)
                {
                    Debug.WriteLine("Same input/target file");
                    return;
                }
                sourceFile = MEPackageHandler.OpenMEPackage(d.FileName);
            }

            var targetPersistentLevel = Pcc.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");

            if (targetPersistentLevel == null)
            {
                Debug.WriteLine("Could not find persistent level in current file");
                return;
            }

            var pathnodeForPositioning = Pcc.Exports.FirstOrDefault(x => x.ClassName == "PathNode" && x.ObjectName == "PathNode");
            if (pathnodeForPositioning == null)
            {

                Debug.WriteLine("Could not find pathnode to position objectives around");
                return;
            }
            StructProperty pathnodePos = pathnodeForPositioning.GetProperty<StructProperty>("location");
            float xPos = pathnodePos.GetProp<FloatProperty>("X");
            float y = pathnodePos.GetProp<FloatProperty>("Y") + 80;
            float z = pathnodePos.GetProp<FloatProperty>("Z");

            Debug.WriteLine("Base coordinate for positioning: " + xPos + "," + y + "," + z);

            crossPCCObjectMap = new SortedDictionary<int, int>();

            var itemsToAddToLevel = new List<IExportEntry>();
            foreach (IExportEntry export in sourceFile.Exports)
            {
                if (export.ObjectName == "SFXOperation_ObjectiveSpawnPoint")
                {
                    Debug.WriteLine("Porting " + export.GetFullPath + "_" + export.indexValue);
                    importExport(export, targetPersistentLevel.UIndex, out IExportEntry portedObjective);
                    crossPCCObjectMap[export.Index] = portedObjective.Index; //0 based. map old index to new index
                    itemsToAddToLevel.Add(portedObjective);
                    var child = export.GetProperty<ObjectProperty>("CollisionComponent");
                    IExportEntry collCyl = sourceFile.Exports[child.Value - 1];
                    Debug.WriteLine("Porting " + collCyl.GetFullPath + "_" + collCyl.indexValue);
                    importExport(collCyl, portedObjective.UIndex, out IExportEntry portedCollisionCylinder);
                    crossPCCObjectMap[collCyl.Index] = portedCollisionCylinder.Index; //0 based. map old index to new index
                }
            }

            relinkObjects2(sourceFile);

            xPos -= (itemsToAddToLevel.Count / 2) * 55.0f;
            foreach (IExportEntry addingExport in itemsToAddToLevel)
            {
                StructProperty locationProp = addingExport.GetProperty<StructProperty>("location");
                if (locationProp != null)
                {
                    FloatProperty xProp = locationProp.GetProp<FloatProperty>("X");
                    FloatProperty yProp = locationProp.GetProp<FloatProperty>("Y");
                    FloatProperty zProp = locationProp.GetProp<FloatProperty>("Z");
                    Debug.WriteLine("Original coordinate of objective: " + xProp.Value + "," + yProp.Value + "," + zProp.Value);

                    xProp.Value = xPos;
                    yProp.Value = y;
                    zProp.Value = z;

                    Debug.WriteLine("--New coordinate for positioning: " + xPos + "," + y + "," + z);

                    xPos += 55;
                    addingExport.WriteProperty(locationProp);
                }
            }

            byte[] leveldata = targetPersistentLevel.Data;
            int start = targetPersistentLevel.propsEnd();
            //Console.WriteLine("Found start of binary at {start.ToString("X8"));

            uint exportid = BitConverter.ToUInt32(leveldata, start);
            start += 4;
            uint numberofitems = BitConverter.ToUInt32(leveldata, start);
            SharedPathfinding.WriteMem(leveldata, start, BitConverter.GetBytes(numberofitems + ((uint)itemsToAddToLevel.Count)));
            var readback = BitConverter.ToUInt32(leveldata, start);

            //Debug.WriteLine("Size before: {memory.Length);
            //memory = RemoveIndices(memory, offset, size);
            int offset = (int)(start + (numberofitems + 1) * 4); //will be at the very end of the list as it is now +1

            List<byte> memList = leveldata.ToList();
            foreach (IExportEntry addingExport in itemsToAddToLevel)
            {
                memList.InsertRange(offset, BitConverter.GetBytes(addingExport.UIndex));
                offset += 4;
            }
            leveldata = memList.ToArray();
            targetPersistentLevel.Data = leveldata;

            sourceFile.Release();
            Debug.WriteLine("Done");
            crossPCCObjectMap = null;
            GoToNumber(targetPersistentLevel.UIndex);
        }

        private void GenerateGUIDCacheForFolder_Clicked(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog();
            m.IsFolderPicker = true;
            m.EnsurePathExists = true;
            m.Title = "Select folder to generate GUID cache on";
            if (m.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string dir = m.FileName;
                string[] files = Directory.GetFiles(dir, "*.pcc");
                if (files.Count() > 0)
                {
                    var packageGuidMap = new Dictionary<string, Guid>();
                    var GuidPackageMap = new Dictionary<Guid, string>();

                    IsBusy = true;
                    string guidcachefile = null;
                    foreach (string file in files)
                    {
                        string fname = Path.GetFileNameWithoutExtension(file);
                        if (fname.StartsWith("GuidCache"))
                        {
                            guidcachefile = file;
                            continue;
                        }
                        if (fname.Contains("_LOC_"))
                        {
                            Debug.WriteLine("--> Skipping " + fname);
                            continue; //skip localizations
                        }
                        Debug.WriteLine(Path.GetFileName(file));
                        var package = MEPackageHandler.OpenMEPackage(file);
                        bool hasPackageNamingItself = false;
                        var filesToSkip = new string[] { "BioD_Cit004_270ShuttleBay1", "BioD_Cit003_600MechEvent", "CAT6_Executioner", "SFXPawn_Demo", "SFXPawn_Sniper", "SFXPawn_Heavy", "GethAssassin", "BioD_OMG003_125LitExtra" };
                        foreach (IExportEntry exp in package.Exports)
                        {
                            if (exp.ClassName == "Package" && exp.idxLink == 0 && !filesToSkip.Contains(exp.ObjectName))
                            {
                                if (string.Equals(exp.ObjectName, fname, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    hasPackageNamingItself = true;
                                }

                                int preguidcountoffset = package.Game == MEGame.ME3 ? 0x2C : 0x30;
                                int count = BitConverter.ToInt32(exp.Header, preguidcountoffset);
                                byte[] guidbytes = exp.Header.Skip((preguidcountoffset + 4) + (count * 4)).Take(16).ToArray();
                                if (!guidbytes.All(singleByte => singleByte == 0))
                                {
                                    Guid guid = new Guid(guidbytes);
                                    GuidPackageMap.TryGetValue(guid, out string packagename);
                                    if (packagename != null && packagename != exp.ObjectName)
                                    {
                                        Debug.WriteLine($"-> {exp.UIndex} {exp.ObjectName} has a guid different from already found one ({packagename})! " + guid.ToString());
                                    }
                                    if (packagename == null)
                                    {
                                        GuidPackageMap[guid] = exp.ObjectName;
                                    }
                                }
                            }
                        }
                        package.Release();
                        if (!hasPackageNamingItself)
                        {
                            Debug.WriteLine("----HAS NO SELF NAMING EXPORT");
                        }
                    }
                    foreach (KeyValuePair<Guid, string> entry in GuidPackageMap)
                    {
                        // do something with entry.Value or entry.Key
                        Debug.WriteLine($"  {entry.Value} {entry.Key}");
                    }
                    if (guidcachefile != null)
                    {
                        Debug.WriteLine("Opening GuidCache file " + guidcachefile);
                        var package = MEPackageHandler.OpenMEPackage(guidcachefile);
                        var cacheExp = package.Exports.FirstOrDefault(x => x.ObjectName == "GuidCache");
                        if (cacheExp != null)
                        {
                            var data = new MemoryStream();
                            var expPre = cacheExp.Data.Take(12).ToArray();
                            data.Write(expPre, 0, 12); //4 byte header, None
                            data.WriteInt32(GuidPackageMap.Count);
                            foreach (KeyValuePair<Guid, string> entry in GuidPackageMap)
                            {
                                int nametableIndex = cacheExp.FileRef.FindNameOrAdd(entry.Value);
                                data.WriteInt32(nametableIndex);
                                data.WriteInt32(0);
                                data.Write(entry.Key.ToByteArray(), 0, 16);
                            }
                            cacheExp.Data = data.ToArray();
                        }
                        package.save();
                        package.Release();
                    }
                    Debug.WriteLine("Done. Cache size: " + GuidPackageMap.Count);

                    IsBusy = false;
                }
            }
        }

        private void GenerateNewGUIDForPackageFile_Clicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This process applies immediately and cannot be undone.\nEnsure the file you are going to regenerate is not open in ME3Explorer in any tools.\nBe absolutely sure you know what you're doing before you use this!");
            OpenFileDialog d = new OpenFileDialog { Title = "Select file to regen guid for", Filter = "*.pcc|*.pcc" };
            bool? result = d.ShowDialog();
            IMEPackage sourceFile = null;

            if (result.HasValue && result.Value)
            {
                sourceFile = MEPackageHandler.OpenMEPackage(d.FileName);
                string fname = Path.GetFileNameWithoutExtension(d.FileName);
                Guid newGuid = Guid.NewGuid();
                IExportEntry selfNamingExport = null;
                foreach (IExportEntry exp in sourceFile.Exports)
                {
                    if (exp.ClassName == "Package" && exp.idxLink == 0)
                    {
                        if (string.Equals(exp.ObjectName, fname, StringComparison.InvariantCultureIgnoreCase))
                        {
                            selfNamingExport = exp;
                            break;
                        }
                    }
                }

                if (selfNamingExport == null)
                {
                    sourceFile.Release();
                    MessageBox.Show("Selected package does not contain a self-naming package export.\nCannot regenerate package file-level GUID if it doesn't contain self-named export.");
                    return;
                }
                byte[] header = selfNamingExport.GetHeader();
                int preguidcountoffset = selfNamingExport.FileRef.Game == MEGame.ME3 ? 0x2C : 0x30;
                int count = BitConverter.ToInt32(header, preguidcountoffset);
                int headerguidoffset = (preguidcountoffset + 4) + (count * 4);

                SharedPathfinding.WriteMem(header, headerguidoffset, newGuid.ToByteArray());
                selfNamingExport.Header = header;
                sourceFile.save();
                sourceFile.Release();
                var fileAsBytes = File.ReadAllBytes(d.FileName);
                SharedPathfinding.WriteMem(fileAsBytes, 0x4E, newGuid.ToByteArray());
                File.WriteAllBytes(d.FileName, fileAsBytes);
                MessageBox.Show("Generated a new GUID for package.");
            }
        }

        private void MakeAllGrenadesAmmoRespawn_Click(object sender, RoutedEventArgs e)
        {
            var ammoGrenades = Pcc.Exports.Where(x => x.ClassName != "Class" && !x.ObjectName.StartsWith("Default") && (x.ObjectName == "SFXAmmoContainer" || x.ObjectName == "SFXGrenadeContainer" || x.ObjectName == "SFXAmmoContainer_Simulator"));
            foreach (var container in ammoGrenades)
            {
                BoolProperty repawns = new BoolProperty(true, "bRespawns");
                float respawnTimeVal = 20;
                if (container.ObjectName == "SFXGrenadeContainer") { respawnTimeVal = 8; }
                if (container.ObjectName == "SFXAmmoContainer") { respawnTimeVal = 3; }
                if (container.ObjectName == "SFXAmmoContainer_Simulator") { respawnTimeVal = 5; }
                FloatProperty respawnTime = new FloatProperty(respawnTimeVal, "RespawnTime");
                var currentprops = container.GetProperties();
                currentprops.AddOrReplaceProp(repawns);
                currentprops.AddOrReplaceProp(respawnTime);
                container.WriteProperties(currentprops);
            }

        }
    }

    [DebuggerDisplay("TreeViewEntry {DisplayName}")]
    public class TreeViewEntry : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private System.Windows.Media.Brush _foregroundColor = System.Windows.Media.Brushes.DarkSeaGreen;
        private bool isSelected;
        //public bool IsSelected
        //{
        //    get { return this.isSelected; }
        //    set
        //    {
        //        if (value != this.isSelected)
        //        {
        //            this.isSelected = value;
        //            OnPropertyChanged("IsSelected");
        //        }
        //    }
        //}

        public bool IsProgramaticallySelecting;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (!IsProgramaticallySelecting && isSelected != value)
                {
                    //user is selecting
                    isSelected = value;
                    OnPropertyChanged();
                    return;
                }
                // build a priority queue of dispatcher operations

                // All operations relating to tree item expansion are added with priority = DispatcherPriority.ContextIdle, so that they are
                // sorted before any operations relating to selection (which have priority = DispatcherPriority.ApplicationIdle).
                // This ensures that the visual container for all items are created before any selection operation is carried out.
                // First expand all ancestors of the selected item - those closest to the root first
                // Expanding a node will scroll as many of its children as possible into view - see perTreeViewItemHelper, but these scrolling
                // operations will be added to the queue after all of the parent expansions.
                if (value)
                {
                    var ancestorsToExpand = new Stack<TreeViewEntry>();

                    var parent = Parent;
                    while (parent != null)
                    {
                        if (!parent.IsExpanded)
                            ancestorsToExpand.Push(parent);

                        parent = parent.Parent;
                    }

                    while (ancestorsToExpand.Any())
                    {
                        var parentToExpand = ancestorsToExpand.Pop();
                        DispatcherHelper.AddToQueue(() => parentToExpand.IsExpanded = true, DispatcherPriority.ContextIdle);
                    }
                }

                //cancel if we're currently selected.
                if (isSelected == value)
                    return;

                // Set the item's selected state - use DispatcherPriority.ApplicationIdle so this operation is executed after all
                // expansion operations, no matter when they were added to the queue.
                // Selecting a node will also scroll it into view - see perTreeViewItemHelper
                DispatcherHelper.AddToQueue(() =>
                {
                    if (value != isSelected)
                    {
                        this.isSelected = value;
                        OnPropertyChanged("IsSelected");
                        IsProgramaticallySelecting = false;
                    }
                }, DispatcherPriority.ApplicationIdle);

                // note that by rule, a TreeView can only have one selected item, but this is handled automatically by 
                // the control - we aren't required to manually unselect the previously selected item.

                // execute all of the queued operations in descending DipatecherPriority order (expansion before selection)
                var unused = DispatcherHelper.ProcessQueueAsync();
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
            Sublinks.ClearEx();
            Sublinks.AddRange(exportNodes);
        }
    }

}