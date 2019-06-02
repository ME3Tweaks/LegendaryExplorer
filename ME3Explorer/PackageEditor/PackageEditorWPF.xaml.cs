using ByteSizeLib;
using GongSolutions.Wpf.DragDrop;
using ME1Explorer.Unreal;
using ME3Explorer.ME1.Unreal.UnhoodBytecode;
using ME3Explorer.PackageEditorWPFControls;
using ME3Explorer.Packages;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.SharedUI.PeregrineTreeView;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ME3Explorer.CurveEd;
using static ME3Explorer.Packages.MEPackage;
using static ME3Explorer.Unreal.UnrealFlags;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for PackageEditorWPF.xaml
    /// </summary>
    public partial class PackageEditorWPF : WPFBase, IDropTarget, IBusyUIHost
    {
        public enum CurrentViewMode
        {
            Names,
            Imports,
            Exports,
            Tree
        }
        public static readonly string[] ExportFileTypes = { "GFxMovieInfo", "BioSWF", "Texture2D", "WwiseStream" };

        /// <summary>
        /// Used to populate the metadata editor values so the list does not constantly need to rebuilt, which can slow down the program on large files like SFXGame or BIOC_Base.
        /// </summary>
        List<string> AllEntriesList;
        readonly List<Button> RecentButtons = new List<Button>();
        //Objects in this collection are displayed on the left list view (names, imports, exports)

        readonly Dictionary<ExportLoaderControl, TabItem> ExportLoaders = new Dictionary<ExportLoaderControl, TabItem>();
        private CurrentViewMode _currentView;
        public CurrentViewMode CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
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
                    RefreshView();
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
                if (SetProperty(ref _selectedItem, value) && !SuppressSelectionEvent)
                {
                    Preview();
                }
            }
        }

        private bool _multiRelinkingModeActive;
        public bool MultiRelinkingModeActive
        {
            get => _multiRelinkingModeActive;
            set => SetProperty(ref _multiRelinkingModeActive, value);
        }


        public static readonly string PackageEditorDataFolder = Path.Combine(App.AppDataFolder, @"PackageEditor\");
        private const string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;
        /// <summary>
        /// PCC map that maps values from a source PCC to values in this PCC. Used extensively during relinking.
        /// </summary>
        private readonly Dictionary<IEntry, IEntry> crossPCCObjectMap = new Dictionary<IEntry, IEntry>();
        private string currentFile;
        private int QueuedGotoNumber;
        private bool IsLoadingFile;

        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get => _isBusyTaskbar;
            set => SetProperty(ref _isBusyTaskbar, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

        private string _searchHintText = "Object name";
        public string SearchHintText { get => _searchHintText; set => SetProperty(ref _searchHintText, value); }

        private string _gotoHintText = "UIndex";
        private bool SuppressSelectionEvent;
        public string GotoHintText { get => _gotoHintText; set => SetProperty(ref _gotoHintText, value); }

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
        public ICommand OpenInInterpViewerCommand { get; set; }
        public ICommand FindEntryViaTagCommand { get; set; }
        public ICommand PopoutCurrentViewCommand { get; set; }
        public ICommand MultidropRelinkingCommand { get; set; }
        public ICommand PerformMultiRelinkCommand { get; set; }

        public ICommand OpenFileCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand FindCommand { get; set; }
        public ICommand GotoCommand { get; set; }
        public ICommand TabRightCommand { get; set; }
        public ICommand TabLeftCommand { get; set; }

        private void LoadCommands()
        {
            ComparePackagesCommand = new GenericCommand(ComparePackages, PackageIsLoaded);
            ExportAllDataCommand = new GenericCommand(ExportAllData, ExportIsSelected);
            ExportBinaryDataCommand = new GenericCommand(ExportBinaryData, ExportIsSelected);
            ImportAllDataCommand = new GenericCommand(ImportAllData, ExportIsSelected);
            ImportBinaryDataCommand = new GenericCommand(ImportBinaryData, ExportIsSelected);
            CloneCommand = new GenericCommand(CloneEntry, EntryIsSelected);
            CloneTreeCommand = new GenericCommand(CloneTree, TreeEntryIsSelected);
            FindEntryViaOffsetCommand = new GenericCommand(FindEntryViaOffset, PackageIsLoaded);
            CheckForDuplicateIndexesCommand = new GenericCommand(CheckForDuplicateIndexes, PackageIsLoaded);
            EditNameCommand = new GenericCommand(EditName, NameIsSelected);
            AddNameCommand = new RelayCommand(AddName, CanAddName);
            CopyNameCommand = new GenericCommand(CopyName, NameIsSelected);
            RebuildStreamingLevelsCommand = new GenericCommand(RebuildStreamingLevels, PackageIsLoaded);
            ExportEmbeddedFileCommand = new GenericCommand(ExportEmbeddedFile, DoesSelectedItemHaveEmbeddedFile);
            ImportEmbeddedFileCommand = new GenericCommand(ImportEmbeddedFile, DoesSelectedItemHaveEmbeddedFile);
            ReindexCommand = new GenericCommand(ReindexObjectByName, ExportIsSelected);
            TrashCommand = new GenericCommand(TrashEntryAndChildren, TreeEntryIsSelected);
            PackageHeaderViewerCommand = new GenericCommand(ViewPackageInfo, PackageIsLoaded);
            CreateNewPackageGUIDCommand = new GenericCommand(GenerateNewGUIDForSelected, PackageExportIsSelected);
            SetPackageAsFilenamePackageCommand = new GenericCommand(SetSelectedAsFilenamePackage, PackageExportIsSelected);
            OpenInInterpViewerCommand = new GenericCommand(OpenInInterpViewer, CanOpenInInterpViewer);
            FindEntryViaTagCommand = new GenericCommand(FindEntryViaTag, PackageIsLoaded);
            PopoutCurrentViewCommand = new GenericCommand(PopoutCurrentView, ExportIsSelected);
            MultidropRelinkingCommand = new GenericCommand(EnableMultirelinkingMode, PackageIsLoaded);
            PerformMultiRelinkCommand = new GenericCommand(PerformMultiRelink, CanPerformMultiRelink);

            OpenFileCommand = new GenericCommand(OpenFile);
            SaveFileCommand = new GenericCommand(SaveFile, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SaveFileAs, PackageIsLoaded);
            FindCommand = new GenericCommand(FocusSearch, PackageIsLoaded);
            GotoCommand = new GenericCommand(FocusGoto, PackageIsLoaded);
            TabRightCommand = new GenericCommand(TabRight, PackageIsLoaded);
            TabLeftCommand = new GenericCommand(TabLeft, PackageIsLoaded);

        }

        private void TabRight()
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

        private void TabLeft()
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

        private void FocusSearch()
        {
            Search_TextBox.Focus();
            Search_TextBox.SelectAll();
        }

        private void FocusGoto()
        {
            Goto_TextBox.Focus();
            Goto_TextBox.SelectAll();
        }


        private void SaveFileAs()
        {
            string extension = Path.GetExtension(Pcc.FileName);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void SaveFile()
        {
            Pcc.save();
        }

        private void OpenFile()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            if (d.ShowDialog() == true)
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

        private bool CanPerformMultiRelink() => MultiRelinkingModeActive && crossPCCObjectMap.Count > 0;

        private void EnableMultirelinkingMode()
        {
            MultiRelinkingModeActive = true;
        }

        private void PerformMultiRelink()
        {
            Debug.WriteLine("Performing multi-relink");
            var entry = crossPCCObjectMap.Keys.FirstOrDefault();
            var relinkResults = new List<string>();
            relinkResults.AddRange(relinkObjects2(entry.FileRef));
            relinkResults.AddRange(relinkBinaryObjects(entry.FileRef));
            crossPCCObjectMap.Clear();


            if (relinkResults.Count > 0)
            {
                ListDialog ld = new ListDialog(relinkResults, "Relink report", "The following items failed to relink.", this);
                ld.Show();
            }
            else
            {
                MessageBox.Show("Items have been ported and relinked with no reported issues.\nNote that this does not mean all binary properties were relinked, only supported ones were.");
            }
            MultiRelinkingModeActive = false;
        }


        private void PopoutCurrentView()
        {
            if (EditorTabs.SelectedItem is TabItem tab && tab.Content is ExportLoaderControl exportLoader)
            {
                exportLoader.PopOut();
            }
        }

        private void FindEntryViaTag()
        {
            List<IndexedName> indexedList = Pcc.Names.Select((nr, i) => new IndexedName(i, nr)).ToList();

            const string input = "Select the name of the tag you are trying to find.";
            IndexedName result = NamePromptDialog.Prompt(this, input, "Select tag name", indexedList);

            if (result != null)
            {
                var searchTerm = result.Name.Name.ToLower();
                var found = Pcc.Names.Any(x => x.ToLower() == searchTerm);
                if (found)
                {
                    foreach (IExportEntry exp in Pcc.Exports)
                    {
                        try
                        {
                            var tag = exp.GetProperty<NameProperty>("Tag");
                            if (tag != null && tag.Value.Name.Equals(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                            {
                                GoToNumber(exp.UIndex);
                                return;
                            }
                        }
                        catch
                        {
                            //skip
                        }
                    }
                }
                else
                {
                    MessageBox.Show(result + " is not a name in the name table.");
                    return;
                }
                MessageBox.Show("Could not find export with Tag property with value: " + result);
            }
        }

        private void OpenInInterpViewer()
        {
            if (!TryGetSelectedExport(out IExportEntry export)) return;
            Matinee.InterpEditor p = new Matinee.InterpEditor();
            p.Show();
            p.LoadPCC(export.FileRef.FileName); //hmm...
            p.toolStripComboBox1.SelectedIndex = p.objects.IndexOf(export.Index);
            p.loadInterpData(export.Index);
        }

        private bool CanOpenInInterpViewer()
            => TryGetSelectedExport(out IExportEntry export) && export.FileRef.Game == MEGame.ME3 && export.ClassName == "InterpData" && !export.ObjectName.Contains("Default__");

        private void SetSelectedAsFilenamePackage()
        {
            if (!TryGetSelectedExport(out IExportEntry export)) return;
            byte[] fileGUID = export.FileRef.getHeader().Skip(0x4E).Take(16).ToArray();
            string fname = Path.GetFileNameWithoutExtension(export.FileRef.FileName);

            //Write GUID
            byte[] header = export.GetHeader();
            int preguidcountoffset = Pcc.Game == MEGame.ME3 ? 0x2C : 0x30;
            int count = BitConverter.ToInt32(header, preguidcountoffset);
            int headerguidoffset = preguidcountoffset + 4 + (count * 4);
            header.OverwriteRange(headerguidoffset, fileGUID);
            export.Header = header;

            export.idxObjectName = export.FileRef.FindNameOrAdd(fname);
        }

        private void GenerateNewGUIDForSelected()
        {
            if (!TryGetSelectedExport(out IExportEntry export)) return;
            Guid newGuid = Guid.NewGuid();
            byte[] header = export.GetHeader();
            int preguidcountoffset = Pcc.Game == MEGame.ME3 ? 0x2C : 0x30;
            int count = BitConverter.ToInt32(header, preguidcountoffset);
            int headerguidoffset = preguidcountoffset + 4 + (count * 4);
            header.OverwriteRange(headerguidoffset, newGuid.ToByteArray());
            export.Header = header;
        }

        private void ViewPackageInfo()
        {
            var items = new List<string>();
            try
            {
                byte[] header = Pcc.getHeader();
                MemoryStream ms = new MemoryStream(header);

                uint magicnum = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Magic number: 0x{magicnum:X8}");
                ushort unrealVer = ms.ReadUInt16();
                items.Add($"0x{ms.Position - 2:X2} Unreal version: {unrealVer} (0x{unrealVer:X4})");
                int licenseeVer = ms.ReadUInt16();
                items.Add($"0x{ms.Position - 2:X2} Licensee version:  {licenseeVer} (0x{licenseeVer:X4})");
                uint fullheadersize = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Full header size:  {fullheadersize} (0x{fullheadersize:X8})");
                int foldernameStrLen = ms.ReadInt32();
                items.Add($"0x{ms.Position - 4:X2} Folder name string length: {foldernameStrLen} (0x{foldernameStrLen:X8}) (Negative means Unicode)");
                long currentPosition = ms.Position;
                if (foldernameStrLen > 0)
                {
                    string str = ms.ReadStringASCII(foldernameStrLen - 1);
                    items.Add($"0x{currentPosition:X2} Folder name:  {str}");
                    ms.ReadByte();
                }
                else
                {
                    string str = ms.ReadStringUnicodeNull(foldernameStrLen * -2);
                    items.Add($"0x{currentPosition:X2} Folder name:  {str}");
                }
                uint flags = ms.ReadUInt32();
                string flagsStr = $"0x{ms.Position - 4:X2} Flags: 0x{flags:X8} ";
                EPackageFlags flagEnum = (EPackageFlags)flags;
                var setFlags = flagEnum.MaskToList();
                foreach (var setFlag in setFlags)
                {
                    flagsStr += " " + setFlag;
                }
                items.Add(flagsStr);

                if (Pcc.Game == MEGame.ME3)
                {
                    uint unknown1 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 1: {unknown1} (0x{unknown1:X8})");
                }

                uint nameCount = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Name Table Count: {nameCount}");

                uint nameOffset = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Name Table Offset: 0x{nameOffset:X8}");

                uint exportCount = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Export Count: {exportCount}");

                uint exportOffset = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Export Metadata Table Offset: 0x{exportOffset:X8}");

                uint importCount = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Import Count: {importCount}");

                uint importOffset = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Import Metadata Table Offset: 0x{importOffset:X8}");

                uint dependencyTableCount = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Dependency Table Start Offset: 0x{dependencyTableCount:X8} (Not used in Mass Effect games)");

                if (Pcc.Game == MEGame.ME3)
                {
                    uint dependencyTableOffset = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Dependency Table End Offset: 0x{dependencyTableOffset:X8} (Not used in Mass Effect games)");

                    uint unknown2 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 2: {unknown2} (0x{unknown2:X8})");

                    uint unknown3 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 3: {unknown3} (0x{unknown3:X8})");
                    uint unknown4 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 4: {unknown4} (0x{unknown4:X8})");
                }

                var guidBytes = new byte[16];
                ms.Read(guidBytes, 0, 16);
                items.Add($"0x{ms.Position - 16:X2} Package File GUID: {new Guid(guidBytes).ToString()}");

                uint generationsTableCount = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Generations Count: {generationsTableCount}");

                for (int i = 0; i < generationsTableCount; i++)
                {
                    uint generationExportcount = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2}   Generation #{i}: Export count: {generationExportcount}");

                    uint generationImportcount = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2}   Generation #{i}: Nametable count: {generationImportcount}");

                    uint generationNetcount = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2}   Generation #{i}: Net(worked) object count: {generationNetcount}");
                }

                uint engineVersion = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Engine Version: {generationsTableCount}");

                uint cookerVersion = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Cooker Version: {generationsTableCount}");

                if (Pcc.Game == MEGame.ME2)
                {
                    uint dependencyTableOffset = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Dependency Table Offset: 0x{dependencyTableOffset:X8} (Not used in Mass Effect games)");

                    uint unknown2 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 2: {unknown2} (0x{unknown2:X8})");

                    uint unknown3 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 3: {unknown3} (0x{unknown3:X8})");
                    uint unknown4 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 4: {unknown4} (0x{unknown4:X8})");
                }

                uint unknown5 = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Unknown 5: {unknown5} (0x{unknown5:X8})");

                uint unknown6 = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Unknown 6: {unknown6} (0x{unknown6:X8})");

                CompressionType compressionType = (CompressionType)ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Package Compression Type: {compressionType.ToString()}");
            }
            catch (Exception e)
            {

            }
            new ListDialog(items, Path.GetFileName(Pcc.FileName) + " header information", "Below is information about this package from the header.", this).Show();
        }

        private void TrashEntryAndChildren()
        {
            if (TreeEntryIsSelected())
            {
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;

                List<TreeViewEntry> itemsToTrash = selected.FlattenTree().OrderByDescending(x => x.UIndex).ToList();

                if (itemsToTrash[0].Entry is ImportEntry)
                {
                    MessageBox.Show("Cannot trash a tree only containing imports.\nTrashing only works if there is at least one export in the subtree.");
                    return;
                }

                if (selected.Entry is IEntry ent && ent.GetFullPath.StartsWith("ME3ExplorerTrashPackage"))
                {
                    MessageBox.Show("Cannot trash an already trashed item.");
                    return;
                }

                IExportEntry existingTrashTopLevel = Pcc.Exports.FirstOrDefault(x => x.idxLink == 0 && x.ObjectName == "ME3ExplorerTrashPackage");
                ImportEntry packageImport = Pcc.Imports.FirstOrDefault(x => x.GetFullPath == "Core.Package");
                if (packageImport == null)
                {
                    ImportEntry coreImport = Pcc.Imports.FirstOrDefault(x => x.GetFullPath == "Core");
                    if (coreImport == null)
                    {
                        //really small file
                        coreImport = new ImportEntry(Pcc)
                        {
                            idxObjectName = Pcc.FindNameOrAdd("Core"),
                            idxClassName = Pcc.FindNameOrAdd("Package"),
                            idxLink = 0,
                            idxPackageFile = Pcc.FindNameOrAdd("Core")
                        };
                        Pcc.addImport(coreImport);
                    }
                    //Package isn't an import, could be one of the 2DA files or other small ones
                    packageImport = new ImportEntry(Pcc)
                    {
                        idxObjectName = Pcc.FindNameOrAdd("Package"),
                        idxClassName = Pcc.FindNameOrAdd("Class"),
                        idxLink = coreImport.UIndex,
                        idxPackageFile = Pcc.FindNameOrAdd("Core")
                    };
                    Pcc.addImport(packageImport);
                }
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
                imp.idxClassName = Pcc.FindNameOrAdd("Package");
                imp.idxPackageFile = Pcc.FindNameOrAdd("Core");
                imp.idxLink = trashContainer.UIndex;
                imp.idxObjectName = Pcc.FindNameOrAdd("Trash");
                imp.indexValue = 0;
            }
            else if (entry is IExportEntry exp)
            {
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
                    exp.ObjectFlags &= ~EObjectFlags.HasStack;
                    Guid trashGuid = ToGuid("ME3ExpTrashPackage"); //DO NOT EDIT THIS!!
                    byte[] header = exp.GetHeader();
                    int preguidcountoffset = Pcc.Game == MEGame.ME3 ? 0x2C : 0x30;
                    int count = BitConverter.ToInt32(header, preguidcountoffset);
                    int headerguidoffset = (preguidcountoffset + 4) + (count * 4);
                    header.OverwriteRange(headerguidoffset, trashGuid.ToByteArray());
                    exp.Header = header;
                    return exp;
                }
                else
                {
                    exp.idxLink = trashContainer.UIndex;
                    if (exp.idxLink == exp.UIndex)
                    {
                        //This should not occur
                        Debugger.Break();
                    }
                    exp.idxObjectName = Pcc.FindNameOrAdd("Trash");
                    exp.ObjectFlags &= ~EObjectFlags.HasStack;

                    byte[] header = exp.GetHeader();
                    int preguidcountoffset = Pcc.Game == MEGame.ME3 ? 0x2C : 0x30;
                    int count = BitConverter.ToInt32(header, preguidcountoffset);
                    int headerguidoffset = header.Length - 20;// + 4) + (count * 4);

                    header.OverwriteRange(headerguidoffset, new byte[16]); //erase guid
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

        private void ReindexObjectByName()
        {
            if (!TryGetSelectedExport(out IExportEntry export)) return;
            if (export.GetFullPath.StartsWith("ME3ExplorerTrashPackage"))
            {
                MessageBox.Show("Cannot reindex exports that are part of ME3ExplorerTrashPackage. All items in this package should have an object index of 0.");
                return;
            }
            ReindexObjectsByName(export, true);
        }

        private void ReindexObjectsByName(IExportEntry exp, bool showUI)
        {
            if (exp != null)
            {
                bool uiConfirm = false;
                string prefixToReindex = exp.PackageFullNameInstanced;
                //if (numItemsInFullPath > 0)
                //{
                //    prefixToReindex = prefixToReindex.Substring(0, prefixToReindex.LastIndexOf('.'));
                //}
                string objectname = exp.ObjectName;
                if (showUI)
                {
                    uiConfirm = MessageBox.Show($"Confirm reindexing of all exports named {objectname} within the following package path:\n{(prefixToReindex == "Package" ? "Package file root" : prefixToReindex)}\n\n" +
                        $"Only use this reindexing feature for items that are meant to be indexed 1 and above (and not 0) as this tool will force all items to be indexed at 1 or above.\n\n" +
                        $"Ensure this file has a backup, this operation may cause the file to stop working if you use it improperly.",
                                         "Confirm Reindexing",
                                         MessageBoxButton.YesNo) == MessageBoxResult.Yes;
                }
                if (!showUI || uiConfirm)
                {
                    // Get list of all exports with that object name.
                    //List<IExportEntry> exports = new List<IExportEntry>();
                    //Could use LINQ... meh.

                    int index = 1; //we'll start at 1.
                    foreach (IExportEntry export in Pcc.Exports)
                    {
                        //Check object name is the same, the package path count is the same, the package prefix is the same, and the item is not of type Class
                        if (objectname == export.ObjectName && export.PackageFullNameInstanced == prefixToReindex && export.PackageFullNameInstanced != "Class")
                        {
                            export.indexValue = index;
                            index++;
                        }
                    }
                }
                if (showUI && uiConfirm)
                {
                    MessageBox.Show($"Objects named \"{objectname}\" under {prefixToReindex} have been reindexed.", "Reindexing completed");
                }
            }
        }

        private void CopyName()
        {
            try
            {
                if (LeftSide_ListView.SelectedItem is IndexedName iName)
                {
                    Clipboard.SetText(iName.Name);
                }
            }
            catch (Exception)
            {
                //don't bother, clippy is not having it today
            }
        }

        private bool DoesSelectedItemHaveEmbeddedFile()
        {
            if (TryGetSelectedExport(out IExportEntry export))
            {
                switch (export.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        return true;
                }
            }
            return false;
        }

        private void ExportEmbeddedFile()
        {
            if (TryGetSelectedExport(out IExportEntry exp))
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        try
                        {
                            var props = exp.GetProperties();
                            string dataPropName = exp.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
                            byte[] data = props.GetProp<ArrayProperty<ByteProperty>>(dataPropName).Select(x => x.Value).ToArray();
                            string extension = Path.GetExtension(".swf");
                            SaveFileDialog d = new SaveFileDialog
                            {
                                Title = "Save SWF",
                                FileName = exp.GetFullPath + ".swf",
                                Filter = $"*{extension}|*{extension}"
                            };
                            if (d.ShowDialog() == true)
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

        private void ImportEmbeddedFile()
        {
            if (TryGetSelectedExport(out IExportEntry exp))
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        try
                        {
                            string extension = Path.GetExtension(".swf");
                            OpenFileDialog d = new OpenFileDialog
                            {
                                Title = "Replace SWF",
                                FileName = exp.GetFullPath + ".swf",
                                Filter = $"*{extension}|*{extension}"
                            };
                            if (d.ShowDialog() == true)
                            {
                                var bytes = File.ReadAllBytes(d.FileName);
                                var props = exp.GetProperties();

                                string dataPropName = exp.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
                                var rawData = props.GetProp<ArrayProperty<ByteProperty>>(dataPropName);
                                //Write SWF data
                                rawData.Values = bytes.Select(b => new ByteProperty(b)).ToList();

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

        private void RebuildStreamingLevels()
        {
            try
            {
                var levelStreamingKismets = new List<IExportEntry>();
                IExportEntry bioworldinfo = null;
                foreach (IExportEntry exp in Pcc.Exports)
                {
                    switch (exp.ClassName)
                    {
                        case "BioWorldInfo" when exp.ObjectName == "BioWorldInfo":
                            bioworldinfo = exp;
                            continue;
                        case "LevelStreamingKismet" when exp.ObjectName == "LevelStreamingKismet":
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


        private void AddName(object obj)
        {
            const string input = "Enter a new name.";
            string result = PromptDialog.Prompt(this, input, "Enter new name");
            if (!string.IsNullOrEmpty(result))
            {
                int idx = Pcc.FindNameOrAdd(result);
                if (CurrentView == CurrentViewMode.Names)
                {
                    LeftSide_ListView.SelectedIndex = idx;
                }
                if (idx != Pcc.Names.Count - 1)
                {
                    //not the last
                    MessageBox.Show($"{result} already exists in this package file.\nName index: {idx} (0x{idx:X8})", "Name already exists");
                }
                else
                {

                    MessageBox.Show($"{result} has been added as a name.\nName index: {idx} (0x{idx:X8})", "Name added");
                }
            }
        }

        private bool CanAddName(object obj)
        {
            if (obj is string parameter)
            {
                if (parameter == "FromContextMenu")
                {
                    //Ensure we are on names view - used for menu item
                    return PackageIsLoaded() && CurrentView == CurrentViewMode.Names;
                }
            }
            return PackageIsLoaded();
        }

        private bool TreeEntryIsSelected()
        {
            return CurrentView == CurrentViewMode.Tree && EntryIsSelected();
        }

        private bool NameIsSelected() => CurrentView == CurrentViewMode.Names && LeftSide_ListView.SelectedItem is IndexedName;

        private void EditName()
        {
            if (LeftSide_ListView.SelectedItem is IndexedName iName)
            {
                var name = iName.Name;
                string input = $"Enter a new name to replace this name ({name}) with.";
                string result = PromptDialog.Prompt(this, input, "Enter new name", defaultValue: name, selectText: true);
                if (!string.IsNullOrEmpty(result))
                {
                    Pcc.replaceName(LeftSide_ListView.SelectedIndex, result);
                }
            }
        }

        private void CheckForDuplicateIndexes()
        {
            if (Pcc == null)
            {
                return;
            }
            var duplicates = new List<string>();
            var duplicatesPackagePathIndexMapping = new Dictionary<string, List<int>>();
            foreach (IExportEntry exp in Pcc.Exports)
            {
                string key = exp.GetInstancedFullPath;
                if (key.StartsWith("ME3ExplorerTrashPackage")) continue; //Do not report these as requiring re-indexing.
                if (!duplicatesPackagePathIndexMapping.TryGetValue(key, out List<int> indexList))
                {
                    indexList = new List<int>();
                    duplicatesPackagePathIndexMapping[key] = indexList;
                } else
                {
                    duplicates.Add($"{exp.UIndex} {exp.GetInstancedFullPath} has duplicate index (index value {exp.indexValue})");
                }

                indexList.Add(exp.UIndex);
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
                ListDialog lw = new ListDialog(duplicates, "Duplicate indexes", "The following items have duplicate indexes. The game may choose to use the first occurance of the index it finds, or may crash if indexing is checked internally (such as pathfinding). You can reindex an object to force all same named items to be reindexed in the given unique path. You should reindex from the topmost duplicate entry first if one is found, as it may resolve lower item duplicates.", this);
                lw.Show();
            }
            else
            {
                MessageBox.Show("No duplicate indexes were found.", "Indexing OK");
            }
        }

        private void FindEntryViaOffset()
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
                    int offsetDec = int.Parse(result, NumberStyles.HexNumber);

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

        private void CloneTree()
        {
            if (CurrentView == CurrentViewMode.Tree && TryGetSelectedEntry(out IEntry entry))
            {
                int nextIndex; //used to select the final node
                crossPCCObjectMap.Clear();
                TreeViewEntry newEntry;
                if (entry is IExportEntry exp)
                {

                    IExportEntry ent = exp.Clone();
                    Pcc.addExport(ent);
                    newEntry = new TreeViewEntry(ent);
                    crossPCCObjectMap[exp] = ent;
                }
                else
                {
                    ImportEntry imp = ((ImportEntry)entry).Clone();
                    Pcc.addImport(imp);
                    newEntry = new TreeViewEntry(imp);
                    //Imports are not relinked when locally cloning a tree
                }
                nextIndex = newEntry.UIndex;
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                newEntry.Parent = selected.Parent;
                selected.Parent.Sublinks.Add(newEntry);
                SuppressSelectionEvent = true;
                selected.Parent.SortChildren();
                SuppressSelectionEvent = false;
                cloneTree(selected, newEntry);
                relinkObjects2(Pcc);
                relinkBinaryObjects(Pcc);
                crossPCCObjectMap.Clear(); //Don't support keeping things in memory
                RefreshView();
                GoToNumber(nextIndex);
            }
        }

        private void CloneEntry()
        {
            if (TryGetSelectedEntry(out IEntry entry))
            {
                TreeViewEntry newEntry;
                if (entry is IExportEntry export)
                {

                    IExportEntry ent = export.Clone();
                    Pcc.addExport(ent);
                    newEntry = new TreeViewEntry(ent);
                }
                else
                {
                    ImportEntry imp = ((ImportEntry)entry).Clone();
                    Pcc.addImport(imp);
                    newEntry = new TreeViewEntry(imp);
                }
                TreeViewEntry selected;
                if (CurrentView == CurrentViewMode.Tree)
                {
                    selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                }
                else
                {
                    selected = GetTreeViewEntryByUIndex(entry.UIndex);
                }
                newEntry.Parent = selected.Parent;
                selected.Parent.Sublinks.Add(newEntry);
                SuppressSelectionEvent = true;
                selected.Parent.SortChildren();
                SuppressSelectionEvent = false;
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
                        crossPCCObjectMap[node.Entry] = ent;
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
            SuppressSelectionEvent = true;
            newRootNode.SortChildren();
            SuppressSelectionEvent = false;
        }

        private void ImportBinaryData() => ImportExpData(true);

        private void ImportAllData() => ImportExpData(false);

        private void ImportExpData(bool binaryOnly)
        {
            if (!TryGetSelectedExport(out IExportEntry export))
            {
                return;
            }
            OpenFileDialog d = new OpenFileDialog
            {
                Filter = "*.bin|*.bin",
                FileName = export.ObjectName + ".bin"
            };
            if (d.ShowDialog() == true)
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

        private void ExportBinaryData() => ExportExpData(true);

        private void ExportAllData() => ExportExpData(false);

        private void ExportExpData(bool binaryOnly)
        {
            if (!TryGetSelectedExport(out IExportEntry export))
            {
                return;
            }
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "*.bin|*.bin",
                FileName = export.ObjectName + ".bin"
            };
            if (d.ShowDialog() == true)
            {
                File.WriteAllBytes(d.FileName, binaryOnly ? export.getBinaryData() : export.Data);
                MessageBox.Show("Done.");
            }
        }

        private bool ExportIsSelected() => TryGetSelectedExport(out _);

        private bool PackageExportIsSelected()
        {
            TryGetSelectedEntry(out IEntry entry);
            return entry?.ClassName == "Package";
        }

        private bool ImportIsSelected() => TryGetSelectedImport(out _);

        private bool EntryIsSelected() => TryGetSelectedEntry(out _);

        private bool PackageIsLoaded() => Pcc != null;

        private void ComparePackages()
        {
            if (Pcc != null)
            {
                string extension = Path.GetExtension(Pcc.FileName);
                OpenFileDialog d = new OpenFileDialog { Filter = "*" + extension + "|*" + extension };
                if (d.ShowDialog() == true)
                {
                    if (Pcc.FileName == d.FileName)
                    {
                        MessageBox.Show("You selected the same file as the one already open.");
                        return;
                    }

                    using (IMEPackage compareFile = MEPackageHandler.OpenMEPackage(d.FileName))
                    {
                        if (Pcc.Game != compareFile.Game)
                        {
                            MessageBox.Show("Files are for different games.");
                            return;
                        }

                        int numExportsToEnumerate = Math.Min(Pcc.ExportCount, compareFile.ExportCount);

                        var changedExports = new List<string>();
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
                                changedExports.Add($"Export header has changed: {exp1.UIndex} {exp1.GetFullPath}");
                            }
                            if (!exp1.Data.SequenceEqual(exp2.Data))
                            {
                                changedExports.Add($"Export data has changed: {exp1.UIndex} {exp1.GetFullPath}");
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
                            Debug.WriteLine($"Export only exists in {file}: {i + 1} {enumerateExtras.Exports[i].GetFullPath}");
                            changedExports.Add($"Export only exists in {file}: {i + 1} {enumerateExtras.Exports[i].GetFullPath}");
                        }

                        sw.Stop();
                        Debug.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");

                        ListDialog ld = new ListDialog(changedExports, "Changed exports between files", "The following exports are different between the files.", this);
                        ld.Show();
                    }
                }
            }
        }
        #endregion

        public PackageEditorWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Package Editor WPF", new WeakReference(this));

            //ME3UnrealObjectInfo.generateInfo();
            CurrentView = CurrentViewMode.Tree;
            LoadCommands();

            InitializeComponent();
            ((FrameworkElement)Resources["EntryContextMenu"]).DataContext = this;

            //map export loaders to their tabs
            ExportLoaders[InterpreterTab_Interpreter] = Interpreter_Tab;
            ExportLoaders[MetadataTab_MetadataEditor] = Metadata_Tab;
            ExportLoaders[SoundTab_Soundpanel] = Sound_Tab;
            ExportLoaders[CurveTab_CurveEditor] = CurveEditor_Tab;
            ExportLoaders[Bio2DATab_Bio2DAEditor] = Bio2DAViewer_Tab;
            ExportLoaders[ScriptTab_UnrealScriptEditor] = Script_Tab;
            ExportLoaders[BinaryInterpreterTab_BinaryInterpreter] = BinaryInterpreter_Tab;
            ExportLoaders[EmbeddedTextureViewerTab_EmbededTextureViewer] = EmbeddedTextureViewer_Tab;
            ExportLoaders[ME1TlkEditorWPFTab_ME1TlkEditor] = ME1TlkEditorWPF_Tab;

            InterpreterTab_Interpreter.SetParentNameList(NamesList); //reference to this control for name editor set
            BinaryInterpreterTab_BinaryInterpreter.SetParentNameList(NamesList); //reference to this control for name editor set
            Bio2DATab_Bio2DAEditor.SetParentNameList(NamesList); //reference to this control for name editor set

            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            LoadRecentList();
            RefreshRecent(false);
        }

        public void LoadFile(string s, int goToIndex = 0)
        {
            try
            {
                BusyText = "Loading " + Path.GetFileName(s);
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
                StatusBar_LeftMostText.Text = $"Loading {Path.GetFileName(s)} ({ByteSize.FromBytes(new FileInfo(s).Length)})";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadMEPackage(s);

                RefreshView();
                InitStuff();
                StatusBar_LeftMostText.Text = Path.GetFileName(s);
                Title = $"Package Editor WPF - {s}";
                InterpreterTab_Interpreter.UnloadExport();
                //InitializeTreeView();

                QueuedGotoNumber = goToIndex;

                Task.Run(InitializeTreeViewBackground)
                    .ContinueWithOnUIThread(InitializeTreeViewBackground_Completed);

                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            catch (Exception e)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(s);
                MessageBox.Show($"Error loading {Path.GetFileName(s)}:\n{e.Message}");
                IsBusy = false;
                IsBusyTaskbar = false;
                //throw e;
            }
        }

        private void InitializeTreeViewBackground_Completed(Task<ObservableCollectionExtended<TreeViewEntry>> prevTask)
        {
            if (prevTask.Result != null)
            {
                AllTreeViewNodesX.ClearEx();
                AllTreeViewNodesX.AddRange(prevTask.Result);
            }
            IsLoadingFile = false;
            if (QueuedGotoNumber != 0)
            {
                //Wait for UI to render
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle, null);
                BusyText = $"Navigating to {QueuedGotoNumber}";

                GoToNumber(QueuedGotoNumber);
                if (QueuedGotoNumber > 0)
                {
                    Interpreter_Tab.IsSelected = true;
                }
                QueuedGotoNumber = 0;
                IsBusy = false;
            }
            else
            {
                IsBusy = false;
            }
        }

        private ObservableCollectionExtended<TreeViewEntry> InitializeTreeViewBackground()
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "PackageEditorWPF TreeViewInitialization";

            BusyText = "Loading " + Path.GetFileName(Pcc.FileName);
            if (Pcc == null)
            {
                return null;
            }

            IReadOnlyList<ImportEntry> Imports = Pcc.Imports;
            IReadOnlyList<IExportEntry> Exports = Pcc.Exports;
            int importsOffset = Exports.Count;

            var rootEntry = new TreeViewEntry(null, Pcc.FileName) { IsExpanded = true };

            var rootNodes = new List<TreeViewEntry> { rootEntry };
            rootNodes.AddRange(Exports.Select(t => new TreeViewEntry(t)));
            rootNodes.AddRange(Imports.Select(t => new TreeViewEntry(t)));

            //configure links
            //Order: 0 = Root, [Exports], [Imports], <extra, new stuff>
            var itemsToRemove = new List<TreeViewEntry>();
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
            return new ObservableCollectionExtended<TreeViewEntry>(rootNodes.Except(itemsToRemove));
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

            TreeViewEntry rootEntry = new TreeViewEntry(null, Pcc.FileName) { IsExpanded = true };
            AllTreeViewNodesX.Add(rootEntry);

            foreach (IExportEntry exp in Exports)
            {
                AllTreeViewNodesX.Add(new TreeViewEntry(exp));
            }

            foreach (ImportEntry imp in Imports)
            {
                AllTreeViewNodesX.Add(new TreeViewEntry(imp));
            }

            //configure links
            //Order: 0 = Root, [Exports], [Imports], <extra, new stuff>
            var itemsToRemove = new List<TreeViewEntry>();
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
                foreach (var form in Application.Current.Windows)
                {
                    if (form is PackageEditorWPF wpf && this != wpf)
                    {
                        wpf.RefreshRecent(false, RFiles);
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
                RecentButtons[i].Content = Path.GetFileName(filepath.Replace("_", "__"));
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
            SearchHintText = "Object name";
            GotoHintText = "UIndex";
            CurrentView = CurrentViewMode.Tree;
        }
        private void NamesView_Click(object sender, RoutedEventArgs e)
        {
            SearchHintText = "Name";
            GotoHintText = "Index";
            CurrentView = CurrentViewMode.Names;
        }
        private void ImportsView_Click(object sender, RoutedEventArgs e)
        {
            SearchHintText = "Object name";
            GotoHintText = "UIndex";
            CurrentView = CurrentViewMode.Imports;
        }
        private void ExportsView_Click(object sender, RoutedEventArgs e)
        {
            SearchHintText = "Object name";
            GotoHintText = "UIndex";
            CurrentView = CurrentViewMode.Exports;
        }

        /// <summary>
        /// Gets the selected entry uindex in the left side view.
        /// </summary>
        /// <param name="n">int that will be updated to point to the selected entry index. Will return 0 if nothing was selected (check the return value for false).</param>
        /// <returns>True if an item was selected, false if nothing was selected.</returns>
        private bool GetSelected(out int n)
        {
            switch (CurrentView)
            {
                case CurrentViewMode.Tree when LeftSide_TreeView.SelectedItem is TreeViewEntry selected:
                    n = selected.UIndex;
                    return true;
                case CurrentViewMode.Exports when LeftSide_ListView.SelectedItem != null:
                    n = LeftSide_ListView.SelectedIndex + 1; //to unreal indexing
                    return true;
                case CurrentViewMode.Imports when LeftSide_ListView.SelectedItem != null:
                    n = -LeftSide_ListView.SelectedIndex - 1;
                    return true;
                default:
                    n = 0;
                    return false;
            }
        }

        private bool TryGetSelectedEntry(out IEntry entry)
        {
            if (GetSelected(out int uIndex) && Pcc.isEntry(uIndex))
            {
                entry = Pcc.getEntry(uIndex);
                return true;
            }
            entry = null;
            return false;
        }

        private bool TryGetSelectedExport(out IExportEntry export)
        {
            if (GetSelected(out int uIndex) && Pcc.isUExport(uIndex))
            {
                export = Pcc.getUExport(uIndex);
                return true;
            }
            export = null;
            return false;
        }
        private bool TryGetSelectedImport(out ImportEntry import)
        {
            if (GetSelected(out int uIndex) && Pcc.isUImport(uIndex))
            {
                import = Pcc.getUImport(uIndex);
                return true;
            }
            import = null;
            return false;
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool importChanges = changes.Contains(PackageChange.Import) || changes.Contains(PackageChange.ImportAdd);
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);
            bool hasSelection = GetSelected(out int n);

            //we might need to identify parent depths and add those first
            List<PackageUpdate> addedChanges = updates.Where(x => x.change == PackageChange.ExportAdd || x.change == PackageChange.ImportAdd).OrderBy(x => x.index).ToList();
            List<int> headerChanges = updates.Where(x => x.change == PackageChange.ExportHeader || x.change == PackageChange.Import).Select(x => x.change == PackageChange.ExportHeader ? x.index + 1 : -x.index - 1).OrderBy(x => x).ToList();
            if (addedChanges.Count > 0)
            {
                ClassDropdownList.ReplaceAll(Pcc.Exports.Select(x => x.idxClass).Distinct().Select(Pcc.getObjectName).ToList().OrderBy(p => p));
                MetadataTab_MetadataEditor.RefreshAllEntriesList(Pcc);
                //Find nodes that haven't been generated and added yet
                var addedChangesByUIndex = new List<PackageUpdate>();
                foreach (PackageUpdate u in addedChanges)
                {
                    //convert to uindex
                    addedChangesByUIndex.Add(new PackageUpdate { change = u.change, index = u.change == PackageChange.ExportAdd ? u.index + 1 : -u.index - 1 });
                }
                List<TreeViewEntry> treeViewItems = AllTreeViewNodesX[0].FlattenTree();

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
                    IEntry entry = Pcc.getEntry(newItem.index);
                    if (entry == null)
                    {
                        Debugger.Break(); //This shouldn't occur... I hope
                    }

                    TreeViewEntry parent = treeViewItems.FirstOrDefault(x => x.UIndex == entry.idxLink);
                    if (parent != null)
                    {
                        TreeViewEntry newEntry = new TreeViewEntry(entry) { Parent = parent };
                        parent.Sublinks.Add(newEntry);
                        treeViewItems.Add(newEntry); //used to find parents
                        nodesToSortChildrenFor.Add(parent);
                    }
                    else
                    {
                        Debug.WriteLine("Unable to attach new item to parent. Could not find parent with UIndex " + entry.idxLink);
                    }
                    //newItem.Parent = targetItem;
                    //targetItem.Sublinks.Add(newItem);
                }
                SuppressSelectionEvent = true;
                nodesToSortChildrenFor.ToList().ForEach(x => x.SortChildren());
                SuppressSelectionEvent = false;

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


                //Author: Mgamerz
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
                List<TreeViewEntry> tree = AllTreeViewNodesX[0].FlattenTree();
                var nodesNeedingResort = new List<TreeViewEntry>();

                List<TreeViewEntry> tviWithChangedHeaders = tree.Where(x => x.UIndex != 0 && headerChanges.Contains(x.Entry.UIndex)).ToList();
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
                SuppressSelectionEvent = true;
                nodesNeedingResort.ForEach(x => x.SortChildren());
                SuppressSelectionEvent = false;
            }

            if (changes.Contains(PackageChange.Names))
            {
                foreach (ExportLoaderControl elc in ExportLoaders.Keys)
                {
                    elc.SignalNamelistAboutToUpdate();
                }
                RefreshNames(updates.Where(x => x.change == PackageChange.Names).ToList());
                foreach (ExportLoaderControl elc in ExportLoaders.Keys)
                {
                    elc.SignalNamelistChanged();
                }
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
            if ((CurrentView == CurrentViewMode.Exports || CurrentView == CurrentViewMode.Tree) && hasSelection &&
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
                NamesList.ReplaceAll(Pcc.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
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
                        if (CurrentView == CurrentViewMode.Names)
                        {
                            LeftSideList_ItemsSource[update.index] = indexed;
                        }
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
        /// <param name="isRefresh">true if this is just a refresh of the currently-loaded export</param>
        private void Preview(bool isRefresh = false)
        {
            if (!TryGetSelectedEntry(out IEntry selectedEntry))
            {
                InterpreterTab_Interpreter.UnloadExport();
                return;
            }
            if (selectedEntry == null)
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
                Interpreter_Tab.IsEnabled = selectedEntry is IExportEntry;
                if (selectedEntry is IExportEntry exportEntry)
                {
                    foreach (KeyValuePair<ExportLoaderControl, TabItem> entry in ExportLoaders)
                    {
                        if (entry.Key.CanParse(exportEntry))
                        {
                            if (isRefresh && entry.Key is CurveEditor && entry.Value.Visibility == Visibility.Visible)
                            {
                                //CurveEditor handles its own refresh
                                continue;
                            }
                            entry.Key.LoadExport(exportEntry);
                            entry.Value.Visibility = Visibility.Visible;

                        }
                        else
                        {
                            entry.Value.Visibility = Visibility.Collapsed;
                            entry.Key.UnloadExport();
                        }
                    }
                    if (Interpreter_Tab.IsSelected && exportEntry.ClassName == "Class")
                    {
                        //We are on interpreter tab, selecting class. Switch to binary interpreter as interpreter will never be useful
                        BinaryInterpreter_Tab.IsSelected = true;
                    }
                    if (Interpreter_Tab.IsSelected && exportEntry.ClassName == "Function" && Script_Tab.IsVisible)
                    {
                        Script_Tab.IsSelected = true;
                    }
                }
                else if (selectedEntry is ImportEntry importEntry)
                {
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



        /// <summary>
        /// Handler for when the Goto button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GotoButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(Goto_TextBox.Text, out int n))
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
            switch (CurrentView)
            {
                case CurrentViewMode.Tree:
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
                        //DispatcherHelper.EmptyQueue();
                        var list = AllTreeViewNodesX[0].FlattenTree();
                        List<TreeViewEntry> selectNode = list.Where(s => s.Entry != null && s.UIndex == entryIndex).ToList();
                        if (selectNode.Any())
                        {
                            //selectNode[0].ExpandParents();
                            selectNode[0].IsProgramaticallySelecting = true;
                            SelectedItem = selectNode[0];
                            //FocusTreeViewNodeOld(selectNode[0]);

                            //selectNode[0].Focus(LeftSide_TreeView);
                        }
                        else
                        {
                            Debug.WriteLine("Could not find node");
                        }

                        break;
                    }
                case CurrentViewMode.Exports:
                case CurrentViewMode.Imports:
                    {
                        //Check bounds
                        var entry = Pcc.getEntry(entryIndex);
                        if (entry != null)
                        {
                            //UI switch
                            if (CurrentView == CurrentViewMode.Exports && entry is ImportEntry)
                            {
                                CurrentView = CurrentViewMode.Imports;
                            }
                            else if (CurrentView == CurrentViewMode.Imports && entry is IExportEntry)
                            {
                                CurrentView = CurrentViewMode.Exports;
                            }

                            LeftSide_ListView.SelectedIndex = Math.Abs(entryIndex) - 1;
                        }

                        break;
                    }
                case CurrentViewMode.Names when entryIndex >= 0 && entryIndex < LeftSide_ListView.Items.Count:
                    //Names
                    LeftSide_ListView.SelectedIndex = entryIndex;
                    break;
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
                    if (CurrentView == CurrentViewMode.Names)
                    {
                        if (index >= 0 && index < Pcc.NameCount)
                        {
                            Goto_Preview_TextBox.Text = Pcc.getNameEntry(index);
                        }
                        else
                        {
                            Goto_Preview_TextBox.Text = "Invalid value";
                        }
                    }
                    else
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
                                Goto_Preview_TextBox.Text = entry.GetFullPath + "_" + entry.indexValue;
                            }
                            else
                            {
                                Goto_Preview_TextBox.Text = "Index out of bounds of entry list";
                            }
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
            if ((dropInfo.Data as TreeViewEntry)?.Parent != null)
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
            if (dropInfo.TargetItem is TreeViewEntry targetItem && (dropInfo.Data as TreeViewEntry)?.Parent != null)
            {
                //Check if the path of the target and the source is the same. If so, offer to merge instead
                if (!MultiRelinkingModeActive)
                {
                    crossPCCObjectMap.Clear();
                }

                TreeViewEntry sourceItem = (TreeViewEntry)dropInfo.Data;

                if (sourceItem == targetItem || (targetItem.Entry != null && sourceItem.Entry.FileRef == targetItem.Entry.FileRef))
                {
                    return; //ignore
                }

                bool ClearRelinkingMapIfPortingContinues = false;
                if (MultiRelinkingModeActive && crossPCCObjectMap.Count > 0)
                {
                    //Check the incoming file matches the object in Cross PCC Object Map, otherwise will will have to discard the map or cancel the porting
                    var sentry = crossPCCObjectMap.Keys.First();
                    if (sentry.FileRef != sourceItem.Entry.FileRef)
                    {
                        var promptResult = MessageBox.Show($"The item dropped does not come from the same package file as other items in your multi-drop relinking session:\n{sourceItem.Entry.FileRef.FileName}\n\nContinuing will drop all items in your relinking session.", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
                        if (promptResult == MessageBoxResult.Cancel)
                        {
                            return;
                        }
                        else
                        {
                            ClearRelinkingMapIfPortingContinues = true;

                        }
                    }
                }

                var portingOption = TreeMergeDialog.GetMergeType(this, sourceItem, targetItem);

                if (portingOption == TreeMergeDialog.PortingOption.Cancel)
                {
                    return;
                }

                if (ClearRelinkingMapIfPortingContinues)
                {
                    crossPCCObjectMap.Clear();
                    MultiRelinkingModeActive = false;
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
                    if (sourceEntry is IExportEntry entry)
                    {
                        crossPCCObjectMap.Add(entry, targetLinkEntry);
                        ReplaceExportDataWithAnother(entry, targetLinkEntry as IExportEntry);
                        //if (successful)
                        //{
                        //    relinkObjects2(sourceEntry.FileRef);
                        //    relinkBinaryObjects(sourceEntry.FileRef);
                        //}
                    }
                    //return;
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
                //Don't clone the root element into this item since this is a merge
                if (portingOption != TreeMergeDialog.PortingOption.MergeTreeChildren && portingOption != TreeMergeDialog.PortingOption.ReplaceSingular)
                {
                    if (n >= 0)
                    {
                        //importing an export
                        if (importExport(sourceEntry as IExportEntry, link, out IExportEntry newExport))
                        {
                            newItem = new TreeViewEntry(newExport);
                            crossPCCObjectMap[sourceEntry] = newExport; //0 based. map old index to new index
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
                        ImportEntry newImport = getOrAddCrossImport(sourceEntry.GetFullPath, importpcc, Pcc,
                            sourceItem.Sublinks.Count == 0 ? link : (int?)null);
                        newItem = new TreeViewEntry(newImport);
                        crossPCCObjectMap[sourceEntry] = newImport;
                    }
                    newItem.Parent = targetItem;
                    targetItem.Sublinks.Add(newItem);
                }
                else
                {
                    newItem = targetItem; //Root item is the one we just dropped. Use that as the root.
                }


                //if this node has children
                if (sourceItem.Sublinks.Count > 0 && portingOption == TreeMergeDialog.PortingOption.CloneTreeAsChild || portingOption == TreeMergeDialog.PortingOption.MergeTreeChildren)
                {
                    importTree(sourceItem, importpcc, newItem, portingOption);
                }

                SuppressSelectionEvent = true;
                targetItem.SortChildren();
                SuppressSelectionEvent = false;

                //relinkObjects(importpcc);
                if (!MultiRelinkingModeActive)
                {
                    var relinkResults = new List<string>();
                    relinkResults.AddRange(relinkObjects2(importpcc));
                    relinkResults.AddRange(relinkBinaryObjects(importpcc));
                    crossPCCObjectMap.Clear();


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
                RefreshView();
                GoToNumber(n >= 0 ? Pcc.ExportCount : -Pcc.ImportCount);
            }
        }

        private bool ReplaceExportDataWithAnother(IExportEntry incomingExport, IExportEntry targetExport)
        {
            byte[] idata = incomingExport.Data;
            PropertyCollection props = incomingExport.GetProperties();
            int start = incomingExport.GetPropertyStart();
            int end = props.endOffset;

            MemoryStream res = new MemoryStream();
            if (incomingExport.HasStack)
            {
                //ME1, ME2 stack
                byte[] stackdummy = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, //Lets hope for the best :D
                                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00};

                if (Pcc.Game != MEGame.ME3)
                {
                    //TODO: Find a unique NetIndex instead of writing a blank... don't know if that will fix multiplayer sync issues
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
                MessageBox.Show($"Error occured while replacing data in {incomingExport.ObjectName} : {exception.Message}");
                return false;
            }
            res.Write(idata, end, idata.Length - end);
            targetExport.Data = res.ToArray();
            return true;
        }


        /// <summary>
        /// Recursive importing function for importing items from another PCC.
        /// </summary>
        /// <param name="sourceNode">Source node from the importing instance of PackageEditorWPF</param>
        /// <param name="importpcc">PCC to import from</param>
        /// <param name="newItemParent">The new parent node for the tree</param>
        /// <param name="portingOption">"The importing strtegy"</param>
        /// <returns></returns>
        private bool importTree(TreeViewEntry sourceNode, IMEPackage importpcc, TreeViewEntry newItemParent, TreeMergeDialog.PortingOption portingOption)
        {
            foreach (TreeViewEntry node in sourceNode.Sublinks)
            {
                int index = node.Entry.UIndex;
                TreeViewEntry newEntry = null;

                if (portingOption == TreeMergeDialog.PortingOption.MergeTreeChildren)
                {
                    //we must check to see if there is an item already matching what we are trying to port.

                    //Todo: We may need to enhance target checking here as getfullpath may not be reliable enough. Maybe have to do indexing, or something.
                    TreeViewEntry sameObjInTarget = newItemParent.Sublinks.FirstOrDefault(x => node.Entry.GetFullPath == x.Entry.GetFullPath);
                    if (sameObjInTarget != null)
                    {
                        crossPCCObjectMap[node.Entry] = sameObjInTarget.Entry;

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
                    //index--; //This was definitely wrong... probably
                    if (importExport(node.Entry as IExportEntry, newItemParent.UIndex, out IExportEntry importedEntry))
                    {
                        newEntry = new TreeViewEntry(importedEntry);
                        crossPCCObjectMap[node.Entry] = importedEntry;
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

                    newEntry = new TreeViewEntry(newImport);
                    crossPCCObjectMap[node.Entry] = newImport;
                }
                newEntry.Parent = newItemParent;
                newItemParent.Sublinks.Add(newEntry);

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
            if (ex.HasStack)
            {
                //ME1, ME2 stack
                byte[] stackdummy = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, //Lets hope for the best :D
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
                MessageBox.Show($"Error occured while trying to import {ex.ObjectName} : {exception.Message}");
                outputEntry = null;
                return false;
            }

            //set header so addresses are set
            byte[] header = ex.Header.TypedClone();
            if ((ex.FileRef.Game == MEGame.ME1 || ex.FileRef.Game == MEGame.ME2) && Pcc.Game == MEGame.ME3)
            {
                //we need to clip some bytes out of the header
                var clippedHeader = new byte[header.Length - 4];
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
            }
            else if (ex.FileRef.Game == MEGame.ME3)
            {
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
            else
            {
                res.Write(idata, end, idata.Length - end);
            }

            int classValue = 0;
            int archetype = 0;
            int superclass = 0;
            //Set class. This will only work if the class is an import, as we can't reliably pull in exports without lots of other stuff.
            if (ex.idxClass < 0)
            {
                //The class of the export we are importing is an import. We should attempt to relink this.
                ImportEntry portingFromClassImport = ex.FileRef.getUImport(ex.idxClass);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, ex.FileRef, Pcc);
                classValue = newClassImport.UIndex;
            }
            else if (ex.idxClass > 0)
            {
                //Todo: Add cross mapping support as multi-mode will allow this to work now
                IExportEntry portingInClass = ex.FileRef.getUExport(ex.idxClass);
                IExportEntry matchingExport = Pcc.Exports.FirstOrDefault(x => x.GetIndexedFullPath == portingInClass.GetIndexedFullPath);
                if (matchingExport != null)
                {
                    classValue = matchingExport.UIndex;
                }
            }

            //Set superclass
            if (ex.idxClassParent < 0)
            {
                //The class of the export we are importing is an import. We should attempt to relink this.
                ImportEntry portingFromClassImport = ex.FileRef.getUImport(ex.idxClassParent);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, ex.FileRef, Pcc);
                superclass = newClassImport.UIndex;
            }
            else if (ex.idxClassParent > 0)
            {
                //Todo: Add cross mapping support as multi-mode will allow this to work now
                IExportEntry portingInClass = ex.FileRef.getUExport(ex.idxClassParent);
                IExportEntry matchingExport = Pcc.Exports.FirstOrDefault(x => x.GetIndexedFullPath == portingInClass.GetIndexedFullPath);
                if (matchingExport != null)
                {
                    superclass = matchingExport.UIndex;
                }
            }

            //Check archetype.
            if (ex.idxArchtype < 0)
            {
                ImportEntry portingFromClassImport = ex.FileRef.getImport(Math.Abs(ex.idxArchtype) - 1);
                ImportEntry newClassImport = getOrAddCrossImport(portingFromClassImport.GetFullPath, ex.FileRef, Pcc);
                archetype = newClassImport.UIndex;
            }
            else if (ex.idxArchtype > 0)
            {
                IExportEntry portingInClass = ex.FileRef.getUExport(ex.idxArchtype);
                IExportEntry matchingExport = Pcc.Exports.FirstOrDefault(x => x.GetIndexedFullPath == portingInClass.GetIndexedFullPath);
                if (matchingExport != null)
                {
                    archetype = matchingExport.UIndex;
                }
            }

            if (!dataAlreadySet)
            {
                outputEntry.Data = res.ToArray();
            }
            outputEntry.idxClass = classValue;
            outputEntry.idxObjectName = Pcc.FindNameOrAdd(ex.FileRef.getNameEntry(ex.idxObjectName));
            outputEntry.idxLink = link;
            outputEntry.idxClassParent = superclass;
            outputEntry.idxArchtype = archetype;
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
            if (ClassDropdown_Combobox.SelectedItem == null)
                return;

            string searchClass = ClassDropdown_Combobox.SelectedItem.ToString();

            if (CurrentView == CurrentViewMode.Tree)
            {
                TreeViewEntry selectedNode = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                List<TreeViewEntry> items = AllTreeViewNodesX[0].FlattenTree();
                int pos = selectedNode == null ? 0 : items.IndexOf(selectedNode);
                pos += 1; //search this and 1 forward
                for (int i = 0; i < items.Count; i++)
                {
                    int curIndex = (i + pos) % items.Count;
                    TreeViewEntry node = items[curIndex];
                    if (node.Entry == null)
                    {
                        continue;
                    }

                    if (node.Entry.ClassName.Equals(searchClass))
                    {
                        node.IsProgramaticallySelecting = true;
                        SelectedItem = node;
                        break;
                    }
                }
            }
            else
            {
                int n = LeftSide_ListView.SelectedIndex;
                int start;
                if (n == -1)
                    start = 0;
                else
                    start = n + 1;
                if (CurrentView == CurrentViewMode.Exports)
                {
                    IReadOnlyList<IExportEntry> pccObjectList = Pcc.Exports;
                    for (int i = start; i < pccObjectList.Count; i++)
                        if (pccObjectList[i].ClassName == searchClass)
                        {
                            LeftSide_ListView.SelectedIndex = i;
                            break;
                        }
                }
                else if (CurrentView == CurrentViewMode.Imports)
                {
                    IReadOnlyList<ImportEntry> pccObjectList = Pcc.Imports;
                    for (int i = start; i < pccObjectList.Count; i++)
                        if (pccObjectList[i].ClassName == searchClass)
                        {
                            LeftSide_ListView.SelectedIndex = i;
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
            if (CurrentView == CurrentViewMode.Tree && AllTreeViewNodesX.Count > 0)
            {
                TreeViewEntry selectedNode = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                var items = AllTreeViewNodesX[0].FlattenTree();
                int pos = selectedNode == null ? -1 : items.IndexOf(selectedNode);
                pos += 1; //search this and 1 forward
                for (int i = 0; i < items.Count; i++)
                {
                    int curIndex = (i + pos) % items.Count;
                    TreeViewEntry node = items[curIndex];
                    if (node.Entry == null)
                    {
                        continue;
                    }
                    if (node.Entry.ObjectName.ToLower().Contains(searchTerm))
                    {
                        node.IsProgramaticallySelecting = true;
                        SelectedItem = node;
                        //node.IsSelected = true;
                        break;
                    }
                }
            }
        }

        private void BuildME1TLKDB_Clicked(object sender, RoutedEventArgs e)
        {
            string myBasePath = ME1Directory.gamePath;
            string[] extensions = { ".u", ".upk" };
            FileInfo[] files = new DirectoryInfo(ME1Directory.cookedPath).EnumerateFiles("*", SearchOption.AllDirectories)
                               .Where(f => extensions.Contains(f.Extension.ToLower()))
                               .ToArray();
            int i = 1;
            var stringMapping = new SortedDictionary<int, KeyValuePair<string, List<string>>>();
            foreach (FileInfo f in files)
            {
                StatusBar_LeftMostText.Text = $"[{i}/{files.Length}] Scanning {f.FullName}";
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

                                if (!stringMapping.TryGetValue(sref.StringID, out var dictEntry))
                                {
                                    dictEntry = new KeyValuePair<string, List<string>>(sref.Data, new List<string>());
                                    stringMapping[sref.StringID] = dictEntry;
                                }
                                if (sref.StringID == 158104)
                                {
                                    Debugger.Break();
                                }
                                dictEntry.Value.Add($"{subPath} in uindex {exp.UIndex} \"{exp.ObjectName}\"");
                            }
                        }
                    }
                    i++;
                }
            }

            int done = 0;
            int total = stringMapping.Count;
            using (StreamWriter file = new StreamWriter(@"C:\Users\Public\SuperTLK.txt"))
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
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
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


        private void TouchComfyMode_Clicked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.TouchComfyMode = !Properties.Settings.Default.TouchComfyMode;
            Properties.Settings.Default.Save();
            TouchComfySettings.ModeSwitched();
        }

        private void PackageEditorWPF_Closing(object sender, CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                SoundTab_Soundpanel.FreeAudioResources();
                foreach (var el in ExportLoaders.Keys)
                {
                    el.Dispose(); //Remove hosted winforms references
                }
                LeftSideList_ItemsSource.ClearEx();
                //LeftSide_TreeView = null; //peregrine treeview dispatcher leak //we don't use peregrine tree view anymore
                AllTreeViewNodesX.ClearEx();
            }
        }

        private void OpenIn_Clicked(object sender, RoutedEventArgs e)
        {
            var myValue = (string)((MenuItem)sender).Tag;
            switch (myValue)
            {
                case "DialogueEditor":
                    if (Pcc.Game == MEGame.ME3)
                    {
                        var diaEditor = new DialogEditor.DialogEditor();
                        diaEditor.LoadFile(Pcc.FileName);
                        diaEditor.Show();
                        break;
                    }
                    else if (Pcc.Game == MEGame.ME2)
                    {
                        var dia2Editor = new ME2Explorer.DialogEditor();
                        dia2Editor.LoadFile(Pcc.FileName);
                        dia2Editor.Show();
                        break;
                    }
                    else if (Pcc.Game == MEGame.ME1)
                    {
                        var dia1Editor = new ME1Explorer.DialogEditor();
                        dia1Editor.LoadFile(Pcc.FileName);
                        dia1Editor.Show();
                        break;
                    }
                    break;
                case "FaceFXEditor":
                    var facefxEditor = new FaceFX.FaceFXEditor();
                    facefxEditor.LoadFile(Pcc.FileName);
                    facefxEditor.Show();
                    break;
                case "PathfindingEditor":
                    var pathEditor = new PathfindingEditorWPF(Pcc.FileName);
                    pathEditor.Show();
                    break;
                case "SoundplorerWPF":
                    var soundplorerWPF = new Soundplorer.SoundplorerWPF();
                    soundplorerWPF.LoadFile(Pcc.FileName);
                    soundplorerWPF.Show();
                    break;
                case "SequenceEditor":
                    var seqEditor = new Sequence_Editor.SequenceEditorWPF();
                    seqEditor.LoadFile(Pcc.FileName);
                    seqEditor.Show();
                    break;
            }
        }

        private void HexConverterMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(App.HexConverterPath))
            {
                Process.Start(App.HexConverterPath);
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

            public override bool Equals(Object obj)
            {
                //Check for null and compare run-time types.
                if ((obj == null) || GetType() != obj.GetType())
                {
                    return false;
                }
                else
                {
                    IndexedName other = (IndexedName)obj;
                    return Index == other.Index && Name == other.Name;
                }
            }
        }

        private void BinaryInterpreterWPF_AlwaysAutoParse_Click(object sender, RoutedEventArgs e)
        {
            //BinaryInterpreterWPF_AlwaysAutoParse_MenuItem.IsChecked = !BinaryInterpreterWPF_AlwaysAutoParse_MenuItem.IsChecked;
            Properties.Settings.Default.BinaryInterpreterWPFAutoScanAlways = !Properties.Settings.Default.BinaryInterpreterWPFAutoScanAlways;
            Properties.Settings.Default.Save();
        }

        //To be moved to Pathinding Editor WPF. will take some re-architecting though for relinking
        private void Port_SFXObjectives_Click(object sender, RoutedEventArgs e)
        {
            if (Pcc == null)
            {
                return;
            }
            OpenFileDialog d = new OpenFileDialog { Title = "Select source file", Filter = "*.pcc|*.pcc" };
            bool? result = d.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No source file selected");
                return;
            }
            if (d.FileName == Pcc.FileName)
            {
                Debug.WriteLine("Same input/target file");
                return;
            }

            IExportEntry targetPersistentLevel;
            using (IMEPackage sourceFile = MEPackageHandler.OpenMEPackage(d.FileName))
            {
                targetPersistentLevel = Pcc.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");

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

                Debug.WriteLine($"Base coordinate for positioning: {xPos},{y},{z}");

                crossPCCObjectMap.Clear();

                var itemsToAddToLevel = new List<IExportEntry>();
                foreach (IExportEntry export in sourceFile.Exports)
                {
                    if (export.ObjectName == "SFXOperation_ObjectiveSpawnPoint")
                    {
                        Debug.WriteLine("Porting " + export.GetFullPath + "_" + export.indexValue);
                        importExport(export, targetPersistentLevel.UIndex, out IExportEntry portedObjective);
                        crossPCCObjectMap[export] = portedObjective;
                        itemsToAddToLevel.Add(portedObjective);
                        var child = export.GetProperty<ObjectProperty>("CollisionComponent");
                        IExportEntry collCyl = sourceFile.Exports[child.Value - 1];
                        Debug.WriteLine($"Porting {collCyl.GetFullPath}_{collCyl.indexValue}");
                        importExport(collCyl, portedObjective.UIndex, out IExportEntry portedCollisionCylinder);
                        crossPCCObjectMap[collCyl] = portedCollisionCylinder;
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
                        Debug.WriteLine($"Original coordinate of objective: {xProp.Value},{yProp.Value},{zProp.Value}");

                        xProp.Value = xPos;
                        yProp.Value = y;
                        zProp.Value = z;

                        Debug.WriteLine($"--New coordinate for positioning: {xPos},{y},{z}");

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
                leveldata.OverwriteRange(start, BitConverter.GetBytes(numberofitems + (uint)itemsToAddToLevel.Count));
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
            }

            crossPCCObjectMap.Clear();
            GoToNumber(targetPersistentLevel.UIndex);
            Debug.WriteLine("Done");
        }

        private void GenerateGUIDCacheForFolder_Clicked(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select folder to generate GUID cache on"
            };
            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                string dir = m.FileName;
                string[] files = Directory.GetFiles(dir, "*.pcc");
                if (files.Any())
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
                        bool hasPackageNamingItself = false;
                        using (var package = MEPackageHandler.OpenMEPackage(file))
                        {
                            var filesToSkip = new[] { "BioD_Cit004_270ShuttleBay1", "BioD_Cit003_600MechEvent", "CAT6_Executioner", "SFXPawn_Demo", "SFXPawn_Sniper", "SFXPawn_Heavy", "GethAssassin", "BioD_OMG003_125LitExtra" };
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
                                    byte[] guidbytes = exp.Header.Skip(preguidcountoffset + 4 + (count * 4)).Take(16).ToArray();
                                    if (guidbytes.Any(singleByte => singleByte != 0))
                                    {
                                        Guid guid = new Guid(guidbytes);
                                        GuidPackageMap.TryGetValue(guid, out string packagename);
                                        if (packagename != null && packagename != exp.ObjectName)
                                        {
                                            Debug.WriteLine($"-> {exp.UIndex} {exp.ObjectName} has a guid different from already found one ({packagename})! {guid}");
                                        }
                                        if (packagename == null)
                                        {
                                            GuidPackageMap[guid] = exp.ObjectName;
                                        }
                                    }
                                }
                            }
                        }

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
                        using (var package = MEPackageHandler.OpenMEPackage(guidcachefile))
                        {
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
                        }
                    }
                    Debug.WriteLine("Done. Cache size: " + GuidPackageMap.Count);

                    IsBusy = false;
                }
            }
        }



        private void GenerateNewGUIDForPackageFile_Clicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This process applies immediately and cannot be undone.\nEnsure the file you are going to regenerate is not open in ME3Explorer in any tools.\nBe absolutely sure you know what you're doing before you use this!");
            OpenFileDialog d = new OpenFileDialog
            {
                Title = "Select file to regen guid for",
                Filter = "*.pcc|*.pcc"
            };
            if (d.ShowDialog() == true)
            {
                Guid newGuid;
                using (IMEPackage sourceFile = MEPackageHandler.OpenMEPackage(d.FileName))
                {
                    string fname = Path.GetFileNameWithoutExtension(d.FileName);
                    newGuid = Guid.NewGuid();
                    IExportEntry selfNamingExport = null;
                    foreach (IExportEntry exp in sourceFile.Exports)
                    {
                        if (exp.ClassName == "Package"
                         && exp.idxLink == 0
                         && string.Equals(exp.ObjectName, fname, StringComparison.InvariantCultureIgnoreCase))
                        {
                            selfNamingExport = exp;
                            break;
                        }
                    }

                    if (selfNamingExport == null)
                    {
                        MessageBox.Show("Selected package does not contain a self-naming package export.\nCannot regenerate package file-level GUID if it doesn't contain self-named export.");
                        return;
                    }
                    byte[] header = selfNamingExport.GetHeader();
                    int preguidcountoffset = selfNamingExport.FileRef.Game == MEGame.ME3 ? 0x2C : 0x30;
                    int count = BitConverter.ToInt32(header, preguidcountoffset);
                    int headerguidoffset = preguidcountoffset + 4 + (count * 4);

                    header.OverwriteRange(headerguidoffset, newGuid.ToByteArray());
                    selfNamingExport.Header = header;
                    sourceFile.save();
                }

                var fileAsBytes = File.ReadAllBytes(d.FileName);
                fileAsBytes.OverwriteRange(0x4E, newGuid.ToByteArray());
                File.WriteAllBytes(d.FileName, fileAsBytes);
                MessageBox.Show("Generated a new GUID for package.");
            }
        }

        private void MakeAllGrenadesAmmoRespawn_Click(object sender, RoutedEventArgs e)
        {
            var ammoGrenades = Pcc.Exports.Where(x => x.ClassName != "Class" && !x.ObjectName.StartsWith("Default") && (x.ObjectName == "SFXAmmoContainer" || x.ObjectName == "SFXGrenadeContainer" || x.ObjectName == "SFXAmmoContainer_Simulator"));
            foreach (var container in ammoGrenades)
            {
                BoolProperty respawns = new BoolProperty(true, "bRespawns");
                float respawnTimeVal = 20;
                if (container.ObjectName == "SFXGrenadeContainer") { respawnTimeVal = 8; }
                if (container.ObjectName == "SFXAmmoContainer") { respawnTimeVal = 3; }
                if (container.ObjectName == "SFXAmmoContainer_Simulator") { respawnTimeVal = 5; }
                FloatProperty respawnTime = new FloatProperty(respawnTimeVal, "RespawnTime");
                var currentprops = container.GetProperties();
                currentprops.AddOrReplaceProp(respawns);
                currentprops.AddOrReplaceProp(respawnTime);
                container.WriteProperties(currentprops);
            }

        }

        private void BuildME1NativeFunctionsInfo_Click(object sender, RoutedEventArgs e)
        {
            if (ME1Directory.gamePath != null)
            {
                var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
                var dir = new DirectoryInfo(ME1Directory.gamePath);
                var filesToSearch = dir.GetFiles(/*"*.sfm", SearchOption.AllDirectories).Union(dir.GetFiles(*/"*.u", SearchOption.AllDirectories).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME1Package(fi.FullName))
                    {
                        Debug.WriteLine(fi.Name);
                        foreach (IExportEntry export in package.Exports)
                        {
                            if (export.ClassName == "Function")
                            {

                                BinaryReader reader = new BinaryReader(new MemoryStream(export.Data));
                                reader.ReadBytes(12);
                                int super = reader.ReadInt32();
                                int children = reader.ReadInt32();
                                reader.ReadBytes(12);
                                int line = reader.ReadInt32();
                                int textPos = reader.ReadInt32();
                                int scriptSize = reader.ReadInt32();
                                byte[] bytecode = reader.ReadBytes(scriptSize);
                                int nativeIndex = reader.ReadInt16();
                                int operatorPrecedence = reader.ReadByte();
                                int functionFlags = reader.ReadInt32();
                                if ((functionFlags & UE3FunctionReader._flagSet.GetMask("Net")) != 0)
                                {
                                    reader.ReadInt16();  // repOffset
                                }
                                int friendlyNameIndex = reader.ReadInt32();
                                reader.ReadInt32();
                                var function = new UnFunction(export, package.getNameEntry(friendlyNameIndex),
                                                              new FlagValues(functionFlags, UE3FunctionReader._flagSet), bytecode, nativeIndex, operatorPrecedence);

                                if (nativeIndex != 0)
                                {
                                    Debug.WriteLine(">>NATIVE Function " + nativeIndex + " " + export.ObjectName);
                                    var newInfo = new CachedNativeFunctionInfo
                                    {
                                        nativeIndex = nativeIndex,
                                        Name = export.ObjectName,
                                        Filename = fi.Name,
                                        Operator = function.Operator,
                                        PreOperator = function.PreOperator,
                                        PostOperator = function.PostOperator
                                    };
                                    newCachedInfo[nativeIndex] = newInfo;
                                }
                            }
                        }
                    }
                }
                File.WriteAllText(Path.Combine(App.ExecFolder, "ME1NativeFunctionInfo.json"), JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));
                Debug.WriteLine("Done");
            }
        }

        private void FindME12DATables_Click(object sender, RoutedEventArgs e)
        {
            if (ME1Directory.gamePath != null)
            {
                var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
                var dir = new DirectoryInfo(Path.Combine(ME1Directory.gamePath/*, "BioGame", "CookedPC", "Maps"*/));
                var filesToSearch = dir.GetFiles("*.sfm", SearchOption.AllDirectories).Union(dir.GetFiles("*.u", SearchOption.AllDirectories)).Union(dir.GetFiles("*.upk", SearchOption.AllDirectories)).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME1Package(fi.FullName))
                    {
                        foreach (IExportEntry export in package.Exports)
                        {
                            if ((export.ClassName == "BioSWF"))
                            //|| export.ClassName == "Bio2DANumberedRows") && export.ObjectName.Contains("BOS"))
                            {
                                Debug.WriteLine($"{export.ClassName}({export.ObjectName}) in {fi.Name} at export {export.UIndex}");
                            }
                        }
                    }
                }
                //File.WriteAllText(System.Windows.Forms.Application.StartupPath + "//exec//ME1NativeFunctionInfo.json", JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));
                Debug.WriteLine("Done");
            }
        }

        private void FindAllME3PowerCustomAction_Click(object sender, RoutedEventArgs e)
        {
            if (ME3Directory.gamePath != null)
            {
                var newCachedInfo = new SortedDictionary<string, List<string>>();
                var dir = new DirectoryInfo(ME3Directory.gamePath);
                var filesToSearch = dir.GetFiles("*.pcc", SearchOption.AllDirectories).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME3Package(fi.FullName))
                    {
                        foreach (IExportEntry export in package.Exports)
                        {
                            if (export.ClassParent == "SFXPowerCustomAction")
                            {
                                Debug.WriteLine($"{export.ClassName}({export.ObjectName}) in {fi.Name} at export {export.UIndex}");
                                if (newCachedInfo.TryGetValue(export.ObjectName, out List<string> instances))
                                {
                                    instances.Add($"{fi.Name} at export { export.UIndex}");
                                }
                                else
                                {
                                    newCachedInfo[export.ObjectName] = new List<string> { $"{fi.Name} at export {export.UIndex}" };
                                }
                            }
                        }
                    }
                }


                string outstr = "";
                foreach (KeyValuePair<string, List<string>> instancelist in newCachedInfo)
                {
                    outstr += instancelist.Key;
                    outstr += "\n";
                    foreach (string str in instancelist.Value)
                    {
                        outstr += " - " + str + "\n";
                    }
                }
                File.WriteAllText(@"C:\users\public\me3powers.txt", outstr);
                Debug.WriteLine("Done");
            }
        }

        private void CreatePCCDumpME1_Click(object sender, RoutedEventArgs e)
        {
            new PackageDumper.PackageDumper(this).Show();
        }

        private void AssociateFileTypes_Clicked(object sender, RoutedEventArgs e)
        {
            Main_Window.Utilities.FileAssociations.EnsureAssociationsSet("pcc", "Mass Effect 2/3 Package File");
            Main_Window.Utilities.FileAssociations.EnsureAssociationsSet("sfm", "Mass Effect 1 Package File");
        }

        private void BuildME1ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME1UnrealObjectInfo.generateInfo();
        }

        private void BuildME2ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME2Explorer.Unreal.ME2UnrealObjectInfo.generateInfo();
        }

        private void BuildME3ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME3UnrealObjectInfo.generateInfo();
        }

        private void RefreshProperties_Clicked(object sender, RoutedEventArgs e)
        {
            var properties = InterpreterTab_Interpreter.CurrentLoadedExport?.GetProperties();
        }

        private void PrintLoadedPackages_Clicked(object sender, RoutedEventArgs e)
        {
            MEPackageHandler.PrintOpenPackages();
        }

        private void ObjectInfosSearch_Click(object sender, RoutedEventArgs e)
        {
            var searchTerm = PromptDialog.Prompt(this, "Enter key value to search", "ObjectInfos Search");
            if (searchTerm != null)
            {
                string searchResult = "";

                //ME1
                if (ME1UnrealObjectInfo.Classes.TryGetValue(searchTerm, out ClassInfo _))
                {
                    searchResult += "Key found in ME1 Classes\n";
                }
                if (ME1UnrealObjectInfo.Structs.TryGetValue(searchTerm, out ClassInfo _))
                {
                    searchResult += "Key found in ME1 Structs\n";
                }
                if (ME1UnrealObjectInfo.Enums.TryGetValue(searchTerm, out _))
                {
                    searchResult += "Key found in ME1 Enums\n";
                }

                //ME2
                if (ME2Explorer.Unreal.ME2UnrealObjectInfo.Classes.TryGetValue(searchTerm, out ClassInfo _))
                {
                    searchResult += "Key found in ME2 Classes\n";
                }
                if (ME2Explorer.Unreal.ME2UnrealObjectInfo.Structs.TryGetValue(searchTerm, out ClassInfo _))
                {
                    searchResult += "Key found in ME2 Structs\n";
                }
                if (ME2Explorer.Unreal.ME2UnrealObjectInfo.Enums.TryGetValue(searchTerm, out _))
                {
                    searchResult += "Key found in ME2 Enums\n";
                }

                //ME3
                if (ME3UnrealObjectInfo.Classes.TryGetValue(searchTerm, out ClassInfo _))
                {
                    searchResult += "Key found in ME3 Classes\n";
                }
                if (ME3UnrealObjectInfo.Structs.TryGetValue(searchTerm, out ClassInfo _))
                {
                    searchResult += "Key found in ME3 Structs\n";
                }
                if (ME3UnrealObjectInfo.Enums.TryGetValue(searchTerm, out _))
                {
                    searchResult += "Key found in ME3 Enums\n";
                }

                if (searchResult == "")
                {
                    searchResult = "Key " + searchTerm + " not found in any ObjectInfo Structs/Classes/Enums dictionaries";
                }
                else
                {
                    searchResult = "Key " + searchTerm + " found in the following:\n" + searchResult;
                }

                MessageBox.Show(searchResult);
            }
        }

        private void TLKManagerWPF_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            new TlkManagerNS.TLKManagerWPF().Show();
        }

        private void PropertyParsing_ME3UnknownArrayAsObj_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.PropertyParsingME3UnknownArrayAsObject = !Properties.Settings.Default.PropertyParsingME3UnknownArrayAsObject;
            Properties.Settings.Default.Save();
        }

        private void PropertyParsing_ME2UnknownArrayAsObj_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.PropertyParsingME2UnknownArrayAsObject = !Properties.Settings.Default.PropertyParsingME2UnknownArrayAsObject;
            Properties.Settings.Default.Save();
        }

        private void PropertyParsing_ME1UnknownArrayAsObj_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.PropertyParsingME1UnknownArrayAsObject = !Properties.Settings.Default.PropertyParsingME1UnknownArrayAsObject;
            Properties.Settings.Default.Save();
        }

        private void MountEditor_Click(object sender, RoutedEventArgs e)
        {
            new MountEditor.MountEditorWPF().Show();
        }

        private void ShowObjectIndexes_Click(object sender, RoutedEventArgs e)
        {

            Properties.Settings.Default.PackageEditorWPF_TreeViewShowEntryIndex = !Properties.Settings.Default.PackageEditorWPF_TreeViewShowEntryIndex;
            Properties.Settings.Default.Save();
            if (AllTreeViewNodesX.Any())
            {
                AllTreeViewNodesX[0].FlattenTree().ForEach(x => x.RefreshDisplayName());
            }


        }

        private void EmbeddedTextureViewer_AutoLoad_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.EmbeddedTextureViewer_AutoLoad = !Properties.Settings.Default.EmbeddedTextureViewer_AutoLoad;
            Properties.Settings.Default.Save();
        }

        private void InterpreterWPF_AdvancedMode_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.InterpreterWPF_AdvancedDisplay = !Properties.Settings.Default.InterpreterWPF_AdvancedDisplay;
            Properties.Settings.Default.Save();
        }

        private void ListNetIndexes_Click(object sender, RoutedEventArgs e)
        {
            List<string> strs = new List<String>();
            Debug.WriteLine((Pcc as ME3Package).Generations0NameCount);
            foreach (IExportEntry exp in Pcc.Exports)
            {
                if (exp.GetFullPath.StartsWith("TheWorld.PersistentLevel") && exp.GetFullPath.Count(f => f == '.') == 2)
                {
                    strs.Add($"{exp.NetIndex} {exp.GetIndexedFullPath}");
                }
            }

            var d = new ListDialog(strs, "NetIndexes", "Here are the netindexes in this file", this);
            d.Show();
        }

        private void ListLinkerValues_Click(object sender, RoutedEventArgs e)
        {
            List<string> strs = new List<String>();
            foreach (IExportEntry exp in Pcc.Exports.Where(x => x.LinkerIndex >= 0).OrderBy(x => x.LinkerIndex))
            {
                strs.Add($"UI:{exp.UIndex} -> LI:{BitConverter.ToInt32(exp.Data, 0)} = {exp.GetIndexedFullPath}");
            }

            var d = new ListDialog(strs, "Linker Indexes", "Here are the linker indexes in this file", this);
            d.Show();
        }

        private void ScanAllShaderCaches_Click(object sender, RoutedEventArgs e)
        {
            var filePaths = ME3LoadedFiles.GetEnabledDLC().SelectMany(dlcDir => Directory.EnumerateFiles(Path.Combine(dlcDir, "CookedPCConsole"), "*.pcc"));
            var interestingExports = new List<string>();
            foreach (string filePath in filePaths)
            {
                ScanShaderCache(filePath);
                //ScanMaterials(filePath);
            }

            var listDlg = new ListDialog(interestingExports, "Interesting Exports", "", this);
            listDlg.Show();

            void ScanMaterials(string filePath)
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath))
                {
                    var materials = pcc.Exports.Where(exp => exp.ClassName == "Material");

                    foreach (IExportEntry material in materials)
                    {
                        try
                        {
                            MemoryStream binData = new MemoryStream(material.Data);
                            binData.JumpTo(material.propsEnd());
                            int compileErrrorsCount = binData.ReadInt32();
                            for (int i = 0; i < compileErrrorsCount; i++)
                            {
                                int stringLen = binData.ReadInt32() * -2;
                                binData.Skip(stringLen);
                            }
                            binData.Skip(28);
                            int textureCount = binData.ReadInt32();
                            binData.Skip(textureCount * 4);
                            binData.Skip(20);
                            int candidate1 = binData.ReadInt32();
                            int candidate2 = binData.ReadInt32();
                            if (candidate1 > 1)
                            {
                                interestingExports.Add($"{material.UIndex}: {filePath}");
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            interestingExports.Add($"{material.UIndex}: {filePath}\n{exception}");
                        }
                    }
                }
            }

            void ScanShaderCache(string filePath)
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath))
                {
                    IExportEntry shaderCache = pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache");
                    if (shaderCache == null) return;
                    int oldDataOffset = shaderCache.DataOffset;

                    try
                    {
                        MemoryStream binData = new MemoryStream(shaderCache.Data);
                        binData.JumpTo(shaderCache.propsEnd() + 1);

                        int nameList1Count = binData.ReadInt32();
                        binData.Skip(nameList1Count * 12);

                        int namelist2Count = binData.ReadInt32(); //namelist2
                        binData.Skip(namelist2Count * 12);

                        int shaderCount = binData.ReadInt32();
                        for (int i = 0; i < shaderCount; i++)
                        {
                            binData.Skip(24);
                            int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                            binData.Skip(14);
                            if (binData.ReadInt32() != 1111577667) //CTAB
                            {
                                interestingExports.Add($"{binData.Position - 4}: {filePath}");
                                return;
                            }

                            binData.JumpTo(nextShaderOffset);
                        }

                        int vertexFactoryMapCount = binData.ReadInt32();
                        binData.Skip(vertexFactoryMapCount * 12);

                        int materialShaderMapCount = binData.ReadInt32();
                        for (int i = 0; i < materialShaderMapCount; i++)
                        {
                            binData.Skip(16);

                            int switchParamCount = binData.ReadInt32();
                            binData.Skip(switchParamCount * 32);

                            int componentMaskParamCount = binData.ReadInt32();
                            //if (componentMaskParamCount != 0)
                            //{
                            //    interestingExports.Add($"{i}: {filePath}");
                            //    return;
                            //}

                            binData.Skip(componentMaskParamCount * 44);

                            int normalParams = binData.ReadInt32();
                            if (normalParams != 0)
                            {
                                interestingExports.Add($"{i}: {filePath}");
                                return;
                            }

                            binData.Skip(normalParams * 29);

                            int unrealVersion = binData.ReadInt32();
                            int licenseeVersion = binData.ReadInt32();
                            if (unrealVersion != 684 || licenseeVersion != 194)
                            {
                                interestingExports.Add($"{binData.Position - 8}: {filePath}");
                                return;
                            }

                            int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                            binData.JumpTo(nextMaterialShaderMapOffset);
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        interestingExports.Add($"{filePath}\n{exception}");
                    }
                }
            }
        }

        private void InterpreterWPF_Colorize_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.InterpreterWPF_Colorize = !Properties.Settings.Default.InterpreterWPF_Colorize;
            Properties.Settings.Default.Save();
        }
    }
}