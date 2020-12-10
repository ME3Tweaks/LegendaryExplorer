using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using ME3Explorer.PackageEditorWPFControls;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.Dialogue_Editor;
using ME3Explorer.MaterialViewer;
using ME3Explorer.ME3Tweaks;
using ME3Explorer.Meshplorer;
using GongSolutions.Wpf.DragDrop;
using Newtonsoft.Json;
using ME3Explorer.PackageEditor.Experiments;
using ME3Explorer.Packages;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using static ME3ExplorerCore.Unreal.UnrealFlags;
using Guid = System.Guid;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.TLK.ME1;
using ME3Script;
using ME3ExplorerCore.GameFilesystem;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for PackageEditorWPF.xaml
    /// </summary>
    public partial class PackageEditorWPF : WPFBase, IDropTarget, IBusyUIHost, IRecents
    {
        public enum CurrentViewMode
        {
            Names,
            Imports,
            Exports,
            Tree
        }

        public static readonly string[] ExportFileTypes =
            {"GFxMovieInfo", "BioSWF", "Texture2D", "WwiseStream", "BioTlkFile"};

        public static readonly string[] ExportIconTypes =
        {
            "GFxMovieInfo", "BioSWF", "Texture2D", "WwiseStream", "BioTlkFile",
            "World", "Package", "StaticMesh", "SkeletalMesh", "Sequence", "Material", "Function", "Class", "State"
        };

        /// <summary>
        /// Used to populate the metadata editor values so the list does not constantly need to rebuilt, which can slow down the program on large files like SFXGame or BIOC_Base.
        /// </summary>
        List<string> AllEntriesList;

        //Objects in this collection are displayed on the left list view (names, imports, exports)

        readonly Dictionary<ExportLoaderControl, TabItem>
            ExportLoaders = new Dictionary<ExportLoaderControl, TabItem>();

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

        public ObservableCollectionExtended<object> LeftSideList_ItemsSource { get; set; } =
            new ObservableCollectionExtended<object>();

        public ObservableCollectionExtended<IndexedName> NamesList { get; set; } =
            new ObservableCollectionExtended<IndexedName>();

        public ObservableCollectionExtended<string> ClassDropdownList { get; set; } =
            new ObservableCollectionExtended<string>();

        public ObservableCollectionExtended<TreeViewEntry> AllTreeViewNodesX { get; set; } =
            new ObservableCollectionExtended<TreeViewEntry>();

        private TreeViewEntry _selectedItem;

        public TreeViewEntry SelectedItem
        {
            get => _selectedItem;
            set
            {
                var oldIndex = _selectedItem?.UIndex;
                if (SetProperty(ref _selectedItem, value) && !SuppressSelectionEvent)
                {
                    if (oldIndex.HasValue && oldIndex.Value != 0 && !IsBackForwardsNavigationEvent)
                    {
                        // 0 = tree root
                        //Debug.WriteLine("Push onto backwards: " + oldIndex);
                        BackwardsIndexes.Push(oldIndex.Value);
                        ForwardsIndexes.Clear(); //forward list is no longer valid
                    }

                    Preview();
                }
            }
        }


        /// <summary>
        /// PCC map that maps values from a source PCC to values in this PCC. Used extensively during relinking.
        /// </summary>
        private readonly Dictionary<IEntry, IEntry> crossPCCObjectMap = new Dictionary<IEntry, IEntry>();

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

        public string SearchHintText
        {
            get => _searchHintText;
            set => SetProperty(ref _searchHintText, value);
        }

        private string _gotoHintText = "UIndex";
        private bool SuppressSelectionEvent;

        public string GotoHintText
        {
            get => _gotoHintText;
            set => SetProperty(ref _gotoHintText, value);
        }

        #region Commands

        public ICommand ComparePackagesCommand { get; set; }
        public ICommand CompareToUnmoddedCommand { get; set; }
        public ICommand ExportAllDataCommand { get; set; }
        public ICommand ExportBinaryDataCommand { get; set; }
        public ICommand ImportAllDataCommand { get; set; }
        public ICommand ImportBinaryDataCommand { get; set; }
        public ICommand CloneCommand { get; set; }
        public ICommand CloneTreeCommand { get; set; }
        public ICommand MultiCloneCommand { get; set; }
        public ICommand MultiCloneTreeCommand { get; set; }
        public ICommand FindEntryViaOffsetCommand { get; set; }
        public ICommand CheckForDuplicateIndexesCommand { get; set; }
        public ICommand CheckForInvalidObjectPropertiesCommand { get; set; }
        public ICommand EditNameCommand { get; set; }
        public ICommand AddNameCommand { get; set; }
        public ICommand CopyNameCommand { get; set; }
        public ICommand FindNameUsagesCommand { get; set; }
        public ICommand RebuildStreamingLevelsCommand { get; set; }
        public ICommand ExportEmbeddedFileCommand { get; set; }
        public ICommand ImportEmbeddedFileCommand { get; set; }
        public ICommand ReindexCommand { get; set; }
        public ICommand TrashCommand { get; set; }
        public ICommand SetIndicesInTreeToZeroCommand { get; set; }
        public ICommand PackageHeaderViewerCommand { get; set; }
        public ICommand CreateNewPackageGUIDCommand { get; set; }
        public ICommand SetPackageAsFilenamePackageCommand { get; set; }
        public ICommand FindEntryViaTagCommand { get; set; }
        public ICommand PopoutCurrentViewCommand { get; set; }
        public ICommand BulkExportSWFCommand { get; set; }
        public ICommand BulkImportSWFCommand { get; set; }
        public ICommand OpenFileCommand { get; set; }
        public ICommand NewFileCommand { get; set; }
        public ICommand NewLevelFileCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand FindCommand { get; set; }
        public ICommand FindAllClassInstancesCommand { get; set; }
        public ICommand GotoCommand { get; set; }
        public ICommand TabRightCommand { get; set; }
        public ICommand TabLeftCommand { get; set; }
        public ICommand DumpAllShadersCommand { get; set; }
        public ICommand DumpMaterialShadersCommand { get; set; }
        public ICommand FindReferencesCommand { get; set; }
        public ICommand OpenMapInGameCommand { get; set; }
        public ICommand OpenExportInCommand { get; set; }
        public ICommand CompactShaderCacheCommand { get; set; }
        public ICommand GoToArchetypecommand { get; set; }
        public ICommand ReplaceNamesCommand { get; set; }
        public ICommand NavigateToEntryCommand { get; set; }
        public ICommand ResolveImportCommand { get; set; }
        public ICommand PackageExportIsSelectedCommand { get; set; }
        public ICommand ReindexDuplicateIndexesCommand { get; set; }
        public ICommand ReplaceReferenceLinksCommand { get; set; }

        private void LoadCommands()
        {
            CompareToUnmoddedCommand = new GenericCommand(CompareUnmodded, CanCompareToUnmodded);
            ComparePackagesCommand = new GenericCommand(ComparePackages, PackageIsLoaded);
            ExportAllDataCommand = new GenericCommand(ExportAllData, ExportIsSelected);
            ExportBinaryDataCommand = new GenericCommand(ExportBinaryData, ExportIsSelected);
            ImportAllDataCommand = new GenericCommand(ImportAllData, ExportIsSelected);
            ImportBinaryDataCommand = new GenericCommand(ImportBinaryData, ExportIsSelected);
            CloneCommand = new GenericCommand(() => CloneEntry(1), EntryIsSelected);
            CloneTreeCommand = new GenericCommand(() => CloneTree(1), TreeEntryIsSelected);
            MultiCloneCommand = new GenericCommand(CloneEntryMultiple, EntryIsSelected);
            MultiCloneTreeCommand = new GenericCommand(CloneTreeMultiple, TreeEntryIsSelected);
            FindEntryViaOffsetCommand = new GenericCommand(FindEntryViaOffset, PackageIsLoaded);
            CheckForDuplicateIndexesCommand = new GenericCommand(CheckForDuplicateIndexes, PackageIsLoaded);
            CheckForInvalidObjectPropertiesCommand =
                new GenericCommand(CheckForBadObjectPropertyReferences, PackageIsLoaded);
            EditNameCommand = new GenericCommand(EditName, NameIsSelected);
            AddNameCommand = new RelayCommand(AddName, CanAddName);
            CopyNameCommand = new GenericCommand(CopyName, NameIsSelected);
            FindNameUsagesCommand = new GenericCommand(FindNameUsages, NameIsSelected);
            RebuildStreamingLevelsCommand = new GenericCommand(RebuildStreamingLevels, PackageIsLoaded);
            ExportEmbeddedFileCommand = new GenericCommand(ExportEmbeddedFilePrompt, DoesSelectedItemHaveEmbeddedFile);
            ImportEmbeddedFileCommand = new GenericCommand(ImportEmbeddedFile, DoesSelectedItemHaveEmbeddedFile);
            FindReferencesCommand = new GenericCommand(FindReferencesToObject, EntryIsSelected);
            ReindexCommand = new GenericCommand(ReindexObjectByName, ExportIsSelected);
            SetIndicesInTreeToZeroCommand = new GenericCommand(SetIndicesInTreeToZero, TreeEntryIsSelected);
            TrashCommand = new GenericCommand(TrashEntryAndChildren, TreeEntryIsSelected);
            PackageHeaderViewerCommand = new GenericCommand(ViewPackageInfo, PackageIsLoaded);
            PackageExportIsSelectedCommand = new EnableCommand(PackageExportIsSelected);
            CreateNewPackageGUIDCommand = new GenericCommand(GenerateNewGUIDForSelected, PackageExportIsSelected);
            SetPackageAsFilenamePackageCommand =
                new GenericCommand(SetSelectedAsFilenamePackage, PackageExportIsSelected);
            FindEntryViaTagCommand = new GenericCommand(FindEntryViaTag, PackageIsLoaded);
            PopoutCurrentViewCommand = new GenericCommand(PopoutCurrentView, ExportIsSelected);
            CompactShaderCacheCommand = new GenericCommand(CompactShaderCache, HasShaderCache);
            GoToArchetypecommand = new GenericCommand(GoToArchetype, CanGoToArchetype);
            ReplaceNamesCommand = new GenericCommand(SearchReplaceNames, PackageIsLoaded);
            ReindexDuplicateIndexesCommand = new GenericCommand(ReindexDuplicateIndexes, PackageIsLoaded);
            ReplaceReferenceLinksCommand = new GenericCommand(ReplaceReferenceLinks, PackageIsLoaded);
            OpenFileCommand = new GenericCommand(OpenFile);
            NewFileCommand = new GenericCommand(NewFile);
            NewLevelFileCommand = new GenericCommand(NewLevelFile);
            SaveFileCommand = new GenericCommand(SaveFile, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SaveFileAs, PackageIsLoaded);
            FindCommand = new GenericCommand(FocusSearch, PackageIsLoaded);
            GotoCommand = new GenericCommand(FocusGoto, PackageIsLoaded);
            TabRightCommand = new GenericCommand(TabRight, PackageIsLoaded);
            TabLeftCommand = new GenericCommand(TabLeft, PackageIsLoaded);

            BulkExportSWFCommand = new GenericCommand(BulkExportSWFs, PackageIsLoaded);
            BulkImportSWFCommand = new GenericCommand(BulkImportSWFs, PackageIsLoaded);
            OpenExportInCommand = new RelayCommand(OpenExportIn, CanOpenExportIn);

            DumpAllShadersCommand = new GenericCommand(DumpAllShaders, HasShaderCache);
            DumpMaterialShadersCommand = new GenericCommand(DumpMaterialShaders, PackageIsLoaded);
            NavigateToEntryCommand = new RelayCommand(NavigateToEntry, CanNavigateToEntry);
            OpenMapInGameCommand = new GenericCommand(OpenMapInGame,
                () => PackageIsLoaded() && Pcc.Game != MEGame.UDK && Pcc.Exports.Any(exp => exp.ClassName == "Level"));
            ResolveImportCommand = new GenericCommand(OpenImportDefinition, ImportIsSelected);
            FindAllClassInstancesCommand = new GenericCommand(FindAllInstancesofClass, PackageIsLoaded);
        }

        private void FindAllInstancesofClass()
        {
            var classes = Pcc.Exports.Select(x => x.ClassName).NonNull().Distinct().ToList().OrderBy(p => p).ToList();
            var chosenClass = InputComboBoxWPF.GetValue(this, "Select a class to list all instances of.", "Class selector", classes, classes.FirstOrDefault());
            if (chosenClass != null)
            {
                var foundExports = Pcc.Exports.Where(x => x.ClassName == chosenClass).ToList();
                // Have to make new EntryStringPair as Entry can be casted into String
                ListDialog ld = new ListDialog(foundExports.Select(x => new EntryStringPair(x, x.InstancedFullPath)),
                    $"Instances of {chosenClass}", $"These are all the exports in this package file that have a class of type {chosenClass}.", this)
                {
                    DoubleClickEntryHandler = entryDoubleClick
                };
                ld.Show();
            }
        }

        private void SetIndicesInTreeToZero()
        {
            if (TreeEntryIsSelected() &&
                MessageBoxResult.Yes ==
                MessageBox.Show(
                    "Are you sure you want to do this? Removing the Indexes from objects can break things if you don't know what you're doing.",
                    "", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning))
            {
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;

                IEnumerable<IEntry> itemsTosetIndexTo0 = selected.FlattenTree().Select(tvEntry => tvEntry.Entry);

                foreach (IEntry entry in itemsTosetIndexTo0)
                {
                    entry.indexValue = 0;
                }
            }
        }

        private void OpenImportDefinition()
        {
            if (TryGetSelectedEntry(out IEntry entry) && entry is ImportEntry curImport)
            {
                BusyText = "Attempting to find source of import...";
                IsBusy = true;
                Task.Run(() => EntryImporter.ResolveImport(curImport)).ContinueWithOnUIThread(prevTask =>
                {
                    IsBusy = false;
                    if (prevTask.Result is ExportEntry res)
                    {
                        PackageEditorWPF pwpf = new PackageEditorWPF();
                        pwpf.Show();
                        pwpf.LoadEntry(res);
                        pwpf.RestoreAndBringToFront();
                    }
                    else
                    {
                        MessageBox.Show(
                            "Could not find the export that this import references.\nHas the link or name (including parents) of this import been changed?\nDo the filenames match the BioWare naming scheme if it's a BioX file?");
                    }
                });
            }
        }

        public void LoadEntry(IEntry entry)
        {
            LoadFile(entry.FileRef.FilePath, entry.UIndex);
        }

        private void NavigateToEntry(object obj)
        {
            IEntry e = (IEntry)obj;
            GoToNumber(e.UIndex);
        }

        private bool CanNavigateToEntry(object o) => o is IEntry entry && entry.FileRef == Pcc;

        private void GoToArchetype()
        {
            if (TryGetSelectedExport(out ExportEntry export) && export.HasArchetype)
            {
                GoToNumber(export.Archetype.UIndex);
            }
        }

        private bool CanGoToArchetype()
        {
            return TryGetSelectedExport(out ExportEntry exp) && exp.HasArchetype;
        }

        private void OpenExportIn(object obj)
        {
            if (obj is string toolName && TryGetSelectedExport(out ExportEntry exp))
            {
                switch (toolName)
                {
                    case "DialogueEditor":
                        if (exp.ClassName == "BioConversation")
                        {
                            new DialogueEditorWPF(exp).Show();
                        }
                        break;
                    case "FaceFXEditor":
                        if (exp.ClassName == "FaceFXAnimSet")
                        {
                            new FaceFX.FaceFXEditor(exp).Show();
                        }
                        break;
                    case "Meshplorer":
                        if (MeshRendererWPF.CanParseStatic(exp))
                        {
                            new MeshplorerWPF(exp).Show();
                        }
                        break;
                    case "Soundplorer":
                        if (Soundpanel.CanParseStatic(exp))
                        {
                            new Soundplorer.SoundplorerWPF(exp).Show();
                        }
                        break;
                    case "SequenceEditor":
                        if (exp.IsA("SequenceObject"))
                        {
                            if (exp.IsA("Sequence") && exp.Parent is ExportEntry parent && parent.IsA("SequenceReference"))
                            {
                                exp = parent;
                            }
                            new Sequence_Editor.SequenceEditorWPF(exp).Show();
                        }
                        break;
                    case "InterpViewer":
                        if (exp.ClassName == "InterpData")
                        {
                            var p = new Matinee.InterpEditor();
                            p.Show();
                            p.LoadFile(Pcc.FilePath);
                            if (exp.ObjectName == "InterpData")
                            {
                                p.SelectedInterpData = exp;
                            }
                        }
                        break;
                    case "PathfindingEditor":
                        if (PathfindingEditorWPF.CanParseStatic(exp))
                        {
                            var pf = new PathfindingEditorWPF(exp);
                            pf.Show();

                        }
                        break;
                    case "WwiseEditor":
                        if (exp.ClassName == "WwiseBank")
                        {
                            var w = new WwiseEditor.WwiseEditorWPF(exp);
                            w.Show();
                        }
                        break;
                }
            }
        }

        private bool CanOpenExportIn(object obj)
        {
            if (obj is string toolName && TryGetSelectedExport(out ExportEntry exp) && !exp.IsDefaultObject)
            {
                switch (toolName)
                {
                    case "DialogueEditor":
                        return exp.ClassName == "BioConversation";
                    case "FaceFXEditor":
                        return exp.ClassName == "FaceFXAnimSet";
                    case "Meshplorer":
                        return MeshRendererWPF.CanParseStatic(exp);
                    case "PathfindingEditor":
                        return PathfindingEditorWPF.CanParseStatic(exp);
                    case "Soundplorer":
                        return Soundpanel.CanParseStatic(exp);
                    case "SequenceEditor":
                        return exp.IsA("SequenceObject");
                    case "InterpViewer":
                        return exp.ClassName == "InterpData";
                    case "WwiseEditor":
                        return exp.ClassName == "WwiseBank";
                }
            }

            return false;
        }

        private void ExportEmbeddedFilePrompt()
        {
            ExportEmbeddedFile();
        }

        private void BulkExportSWFs()
        {
            var swfsInFile = Pcc.Exports.Where(x =>
                x.ClassName == (Pcc.Game == MEGame.ME1 ? "BioSWF" : "GFxMovieInfo") && !x.IsDefaultObject).ToList();
            if (swfsInFile.Count > 0)
            {
                CommonOpenFileDialog m = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    Title = "Select output folder"
                };
                if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
                {

                    string dir = m.FileName;
                    Stopwatch stopwatch = Stopwatch.StartNew(); //creates and start the instance of Stopwatch
                    //your sample code                    
                    foreach (var export in swfsInFile)
                    {
                        string exportFilename = $"{export.FullPath}.swf";
                        string outputPath = Path.Combine(dir, exportFilename);
                        ExportEmbeddedFile(export, outputPath);
                    }

                    stopwatch.Stop();
                    Console.WriteLine(stopwatch.ElapsedMilliseconds);
                }
            }
            else
            {
                MessageBox.Show("This file contains no scaleform exports.");
            }
        }

        private void BulkImportSWFs()
        {
            var swfsInFile = Pcc.Exports.Where(x =>
                x.ClassName == (Pcc.Game == MEGame.ME1 ? "BioSWF" : "GFxMovieInfo") && !x.IsDefaultObject).ToList();
            if (swfsInFile.Count > 0)
            {
                CommonOpenFileDialog m = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    Title = "Select folder of GFX/SWF files to import"
                };
                if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
                {
                    BackgroundWorker bw = new BackgroundWorker();
                    bw.RunWorkerAsync(m.FileName);
                    bw.RunWorkerCompleted += (x, y) =>
                    {
                        IsBusy = false;
                        ListDialog ld =
                            new ListDialog((List<EntryStringPair>)y.Result, "Imported Files",
                                    "The following files were imported.", this)
                            { DoubleClickEntryHandler = entryDoubleClick };
                        ld.Show();
                    };
                    bw.DoWork += (param, eventArgs) =>
                    {
                        BusyText = "Importing SWFs";
                        IsBusy = true;
                        string dir = (string)eventArgs.Argument;
                        var allfiles = new List<string>();
                        allfiles.AddRange(Directory.GetFiles(dir, "*.swf"));
                        allfiles.AddRange(Directory.GetFiles(dir, "*.gfx"));
                        var importedFiles = new List<EntryStringPair>();
                        foreach (var file in allfiles)
                        {
                            var fullpath = Path.GetFileNameWithoutExtension(file);
                            var matchingExport = swfsInFile.FirstOrDefault(x =>
                                x.FullPath.Equals(fullpath, StringComparison.InvariantCultureIgnoreCase));
                            if (matchingExport != null)
                            {
                                //Import and replace file
                                BusyText = $"Importing {fullpath}";

                                var bytes = File.ReadAllBytes(file);
                                var props = matchingExport.GetProperties();

                                string dataPropName = matchingExport.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
                                var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                                //Write SWF data
                                rawData.bytes = bytes;

                                //Write SWF metadata
                                if (matchingExport.FileRef.Game == MEGame.ME1 ||
                                    matchingExport.FileRef.Game == MEGame.ME2)
                                {
                                    string sourceFilePropName = matchingExport.FileRef.Game != MEGame.ME1
                                        ? "SourceFile"
                                        : "SourceFilePath";
                                    StrProperty sourceFilePath = props.GetProp<StrProperty>(sourceFilePropName);
                                    if (sourceFilePath == null)
                                    {
                                        sourceFilePath = new StrProperty(file, sourceFilePropName);
                                        props.Add(sourceFilePath);
                                    }

                                    sourceFilePath.Value = file;
                                }

                                if (matchingExport.FileRef.Game == MEGame.ME1)
                                {
                                    StrProperty sourceFileTimestamp = props.GetProp<StrProperty>("SourceFileTimestamp");
                                    sourceFileTimestamp = File.GetLastWriteTime(file)
                                        .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                }

                                importedFiles.Add(new EntryStringPair(matchingExport,
                                    $"{matchingExport.UIndex} {fullpath}"));
                                matchingExport.WriteProperties(props);
                            }
                        }

                        if (importedFiles.Count == 0)
                        {
                            importedFiles.Add(new EntryStringPair(null, "No matching filenames were found."));
                        }

                        eventArgs.Result = importedFiles;
                    };
                }
            }
            else
            {
                MessageBox.Show("This file contains no scaleform exports.");
            }
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
            string fileFilter;
            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    fileFilter = App.ME1SaveFileFilter;
                    break;
                case MEGame.ME2:
                case MEGame.ME3:
                    fileFilter = App.ME3ME2SaveFileFilter;
                    break;
                default:
                    string extension = Path.GetExtension(Pcc.FilePath);
                    fileFilter = $"*{extension}|*{extension}";
                    break;
            }

            SaveFileDialog d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                Pcc.Save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void SaveFile()
        {
            Pcc.Save();
        }

        private void OpenFile()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.OpenFileFilter };
            if (d.ShowDialog() == true)
            {
#if !DEBUG
                try
                {
#endif
                LoadFile(d.FileName);
                //AddRecent(d.FileName, false);
                //SaveRecentList();
                //RefreshRecent(true, RFiles);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }

        private void NewFile()
        {
            string gameString = InputComboBoxWPF.GetValue(this, "Choose a game to create a file for:",
                "Create new package file", new[] { "ME3", "ME2", "ME1", "UDK" }, "ME3");
            if (Enum.TryParse(gameString, out MEGame game))
            {
                var dlg = new SaveFileDialog
                {
                    Filter = game switch
                    {
                        MEGame.UDK => App.UDKFileFilter,
                        MEGame.ME1 => App.ME1SaveFileFilter,
                        _ => App.ME3ME2SaveFileFilter
                    }
                };
                if (dlg.ShowDialog() == true)
                {
                    MEPackageHandler.CreateAndSavePackage(dlg.FileName, game);
                    LoadFile(dlg.FileName);
                    RecentsController.AddRecent(dlg.FileName, false);
                    RecentsController.SaveRecentList(true);
                }
            }
        }

        private void NewLevelFile()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "ME3 package file|*.pcc",
                OverwritePrompt = true
            };

            if (dlg.ShowDialog() == true)
            {
                if (File.Exists(dlg.FileName))
                {
                    File.Delete(dlg.FileName);
                }


                File.Copy(Path.Combine(App.ExecFolder, "ME3EmptyLevel.pcc"), dlg.FileName);
                LoadFile(dlg.FileName);
                for (int i = 0; i < Pcc.Names.Count; i++)
                {
                    string name = Pcc.Names[i];
                    if (name.Equals("ME3EmptyLevel"))
                    {
                        var newName = name.Replace("ME3EmptyLevel", Path.GetFileNameWithoutExtension(dlg.FileName));
                        Pcc.replaceName(i, newName);
                    }
                }

                var packguid = Guid.NewGuid();
                var package = Pcc.GetUExport(1);
                package.PackageGUID = packguid;
                Pcc.PackageGuid = packguid;
                SaveFile();
                RecentsController.AddRecent(dlg.FileName, false);
                RecentsController.SaveRecentList(true);
            }
        }

        private void entryDoubleClick(EntryStringPair clickedItem)
        {
            if (clickedItem?.Entry != null && clickedItem.Entry.UIndex != 0)
            {
                GoToNumber(clickedItem.Entry.UIndex);
            }
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
                    foreach (ExportEntry exp in Pcc.Exports)
                    {
                        try
                        {
                            var tag = exp.GetProperty<NameProperty>("Tag");
                            if (tag != null &&
                                tag.Value.Name.Equals(searchTerm, StringComparison.InvariantCultureIgnoreCase))
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

        private void SetSelectedAsFilenamePackage()
        {
            if (!TryGetSelectedExport(out ExportEntry export)) return;

            export.PackageGUID = export.FileRef.PackageGuid;

            export.ObjectName = Path.GetFileNameWithoutExtension(export.FileRef.FilePath);
        }

        private void GenerateNewGUIDForSelected()
        {
            if (!TryGetSelectedExport(out ExportEntry export)) return;
            export.PackageGUID = Guid.NewGuid();
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
                items.Add(
                    $"0x{ms.Position - 4:X2} Folder name string length: {foldernameStrLen} (0x{foldernameStrLen:X8}) (Negative means Unicode)");
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

                if (Pcc.Game == MEGame.ME3 && Pcc.Flags.HasFlag(EPackageFlags.Cooked))
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

                uint dependencyTableOffset = ms.ReadUInt32();
                items.Add(
                    $"0x{ms.Position - 4:X2} Dependency Table Offset: 0x{dependencyTableOffset:X8} (Not used in Mass Effect games)");

                if (Pcc.Game >= MEGame.ME3)
                {
                    uint importExportGuidsOffset = ms.ReadUInt32();
                    items.Add(
                        $"0x{ms.Position - 4:X2} ImportExportGuidsOffset: 0x{importExportGuidsOffset:X8} (Not used in Mass Effect games)");

                    uint unknown2 = ms.ReadUInt32();
                    items.Add(
                        $"0x{ms.Position - 4:X2} ImportGuidsCount: {unknown2} (0x{unknown2:X8}) (Not used in Mass Effect games)");

                    uint unknown3 = ms.ReadUInt32();
                    items.Add(
                        $"0x{ms.Position - 4:X2} ExportGuidsCount: {unknown3} (0x{unknown3:X8}) (Not used in Mass Effect games)");
                    uint unknown4 = ms.ReadUInt32();
                    items.Add(
                        $"0x{ms.Position - 4:X2} ThumbnailTableOffset: {unknown4} (0x{unknown4:X8}) (Not used in Mass Effect games)");
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
                    items.Add(
                        $"0x{ms.Position - 4:X2}   Generation #{i}: Net(worked) object count: {generationNetcount}");
                }

                uint engineVersion = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Engine Version: {engineVersion}");

                uint cookerVersion = ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} CookedContent Version: {cookerVersion}");

                if (Pcc.Game == MEGame.ME2 || Pcc.Game == MEGame.ME1)
                {
                    int unknown2 = ms.ReadInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 2: {unknown2} (0x{unknown2:X8})");

                    int unknown3 = ms.ReadInt32();
                    items.Add($"0x{ms.Position - 4:X2} Static 47699: {unknown3} (0x{unknown3:X8})");

                    if (Pcc.Game == MEGame.ME1)
                    {
                        int static0 = ms.ReadInt32();
                        items.Add($"0x{ms.Position - 4:X2} Static 0: {static0} (0x{static0:X8})");
                        int static1 = ms.ReadInt32();
                        items.Add($"0x{ms.Position - 4:X2} Static 1: {static1} (0x{static1:X8})");
                    }
                    else
                    {
                        int unknown4 = ms.ReadInt32();
                        items.Add($"0x{ms.Position - 4:X2} Unknown 4: {unknown4} (0x{unknown4:X8})");
                        int static1966080 = ms.ReadInt32();
                        items.Add($"0x{ms.Position - 4:X2} Static 1966080: {static1966080} (0x{static1966080:X8})");
                    }

                }

                if (Pcc.Game != MEGame.UDK)
                {
                    int unknown5 = ms.ReadInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 5: {unknown5} (0x{unknown5:X8})");

                    int unknown6 = ms.ReadInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 6: {unknown6} (0x{unknown6:X8})");
                }

                if (Pcc.Game == MEGame.ME1)
                {
                    int unknown7 = ms.ReadInt32();
                    items.Add($"0x{ms.Position - 4:X2} Unknown 7: {unknown7} (0x{unknown7:X8})");
                }

                UnrealPackageFile.CompressionType compressionType = (UnrealPackageFile.CompressionType)ms.ReadUInt32();
                items.Add($"0x{ms.Position - 4:X2} Package Compression Type: {compressionType.ToString()}");

                int numChunks = ms.ReadInt32();
                items.Add($"0x{ms.Position - 4:X2} Number of compressed chunks: {numChunks.ToString()}");

                //read package source
                //var savedPos = ms.Position;
                ms.Skip(numChunks * 16); //skip chunk table so we can find package tag


                var packageSource = ms.ReadUInt32(); //this needs to be read in so it can be properly written back out.
                items.Add($"0x{ms.Position - 4:X4} Package Source: {packageSource:X8}");

                if ((Pcc.Game == MEGame.ME2 || Pcc.Game == MEGame.ME1) && Pcc.Platform != MEPackage.GamePlatform.PS3)
                {
                    var alwaysZero1 =
                        ms.ReadUInt32(); //this needs to be read in so it can be properly written back out.
                    items.Add($"0x{ms.Position - 4:X4} Always zero: {alwaysZero1}");
                }

                if (Pcc.Game == MEGame.ME2 || Pcc.Game == MEGame.ME3 || Pcc.Platform == MEPackage.GamePlatform.PS3)
                {
                    int additionalPackagesToCookCount = ms.ReadInt32();
                    items.Add(
                        $"0x{ms.Position - 4:X4} Number of additional packages to cook: {additionalPackagesToCookCount}");
                    //var additionalPackagesToCook = new string[additionalPackagesToCookCount];
                    for (int i = 0; i < additionalPackagesToCookCount; i++)
                    {
                        var pos = ms.Position;
                        var packageStr = ms.ReadUnrealString();
                        items.Add($"0x{pos:X4} Additional package to cook: {packageStr}");

                    }
                }
            }
            catch (Exception e)
            {

            }

            new ListDialog(items, Path.GetFileName(Pcc.FilePath) + " header information",
                "Below is information about this package from the header.", this).Show();
        }

        private void TrashEntryAndChildren()
        {
            if (TreeEntryIsSelected())
            {
                TreeViewEntry selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;

                var itemsToTrash = selected.FlattenTree().OrderByDescending(x => x.UIndex)
                    .Select(tvEntry => tvEntry.Entry);

                if (selected.Entry is IEntry ent && ent.FullPath.StartsWith(UnrealPackageFile.TrashPackageName))
                {
                    MessageBox.Show("Cannot trash an already trashed item.");
                    return;
                }

                int parentEntry = selected.Entry.Parent?.UIndex ?? 0;

                if (!GoToNumber(parentEntry))
                {
                    AllTreeViewNodesX[0].IsProgramaticallySelecting = true;
                    SelectedItem = AllTreeViewNodesX[0];
                }

                bool removedFromLevel = selected.Entry is ExportEntry exp && exp.ParentName == "PersistentLevel" &&
                                        exp.IsA("Actor") && Pcc.RemoveFromLevelActors(exp);

                EntryPruner.TrashEntries(Pcc, itemsToTrash);

                if (removedFromLevel)
                {
                    MessageBox.Show(this, "Trashed and removed from level!");
                }
            }
        }

        private void FindReferencesToObject()
        {
            if (TryGetSelectedEntry(out IEntry entry))
            {
                BusyText = "Finding references...";
                IsBusy = true;
                Task.Run(() => entry.GetEntriesThatReferenceThisOne()).ContinueWithOnUIThread(prevTask =>
                {
                    IsBusy = false;
                    var dlg = new ListDialog(
                            prevTask.Result.SelectMany(kvp => kvp.Value.Select(refName =>
                                new EntryStringPair(kvp.Key,
                                    $"#{kvp.Key.UIndex} {kvp.Key.ObjectName.Instanced}: {refName}"))).ToList(),
                            $"{prevTask.Result.Count} Objects that reference #{entry.UIndex} {entry.InstancedFullPath}",
                            "There may be additional references to this object in the unparsed binary of some objects",
                            this)
                    { DoubleClickEntryHandler = entryDoubleClick };
                    dlg.Show();
                });


            }
        }

        private void ReindexObjectByName()
        {
            if (!TryGetSelectedExport(out ExportEntry export)) return;
            if (export.FullPath.StartsWith(UnrealPackageFile.TrashPackageName))
            {
                MessageBox.Show(
                    "Cannot reindex exports that are part of ME3ExplorerTrashPackage. All items in this package should have an object index of 0.");
                return;
            }

            ReindexObjectsByName(export, true);
        }

        private void ReindexObjectsByName(ExportEntry exp, bool showUI)
        {
            if (exp != null)
            {
                bool uiConfirm = false;
                string prefixToReindex = exp.ParentInstancedFullPath;
                //if (numItemsInFullPath > 0)
                //{
                //    prefixToReindex = prefixToReindex.Substring(0, prefixToReindex.LastIndexOf('.'));
                //}
                string objectname = exp.ObjectName.Name;
                if (showUI)
                {
                    uiConfirm = MessageBox.Show(
                        $"Confirm reindexing of all exports named {objectname} within the following package path:\n{(string.IsNullOrEmpty(prefixToReindex) ? "Package file root" : prefixToReindex)}\n\n" +
                        $"Only use this reindexing feature for items that are meant to be indexed 1 and above (and not 0) as this tool will force all items to be indexed at 1 or above.\n\n" +
                        $"Ensure this file has a backup, this operation may cause the file to stop working if you use it improperly.",
                        "Confirm Reindexing",
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes;
                }

                if (!showUI || uiConfirm)
                {
                    // Get list of all exports with that object name.
                    //List<ExportEntry> exports = new List<ExportEntry>();
                    //Could use LINQ... meh.

                    int index = 1; //we'll start at 1.
                    foreach (ExportEntry export in Pcc.Exports)
                    {
                        //Check object name is the same, the package path count is the same, the package prefix is the same, and the item is not of type Class
                        if (objectname == export.ObjectName.Name && export.ParentInstancedFullPath == prefixToReindex &&
                            !export.IsClass)
                        {
                            export.indexValue = index;
                            index++;
                        }
                    }
                }

                if (showUI && uiConfirm)
                {
                    MessageBox.Show($"Objects named \"{objectname}\" under {prefixToReindex} have been reindexed.",
                        "Reindexing completed");
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

        private void FindNameUsages()
        {
            if (LeftSide_ListView.SelectedItem is IndexedName iName)
            {
                string name = iName.Name.Name;
                BusyText = $"Finding usages of '{name}'...";
                IsBusy = true;
                Task.Run(() => Pcc.FindUsagesOfName(name)).ContinueWithOnUIThread(prevTask =>
                {
                    IsBusy = false;
                    var dlg = new ListDialog(
                            prevTask.Result.SelectMany(kvp => kvp.Value.Select(refName =>
                                new EntryStringPair(kvp.Key,
                                    $"#{kvp.Key.UIndex} {kvp.Key.ObjectName.Instanced}: {refName}"))).ToList(),
                            $"{prevTask.Result.Count} Objects that use '{name}'",
                            "There may be additional usages of this name in the unparsed binary of some objects", this)
                    { DoubleClickEntryHandler = entryDoubleClick };
                    dlg.Show();
                });
            }
        }

        private bool DoesSelectedItemHaveEmbeddedFile()
        {
            if (TryGetSelectedExport(out ExportEntry export))
            {
                switch (export.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                    case "BioTlkFile":
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Exports the embedded file in the given export to the given path. If the export given is empty, the one currently selected in the tree is exported.
        /// If the given save path is null, it will prompt the user and say Done when completed in a messagebox.
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="savePath"></param>
        private void ExportEmbeddedFile(ExportEntry exp = null, string savePath = null)
        {
            if (exp == null) TryGetSelectedExport(out exp);
            if (exp != null)
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        {
                            try
                            {
                                var props = exp.GetProperties();
                                string dataPropName = exp.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
                                var DataProp = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                                byte[] data = DataProp.bytes;

                                if (savePath == null)
                                {
                                    //GFX is scaleform extensions for SWF
                                    //SWC is Shockwave Compressed
                                    //SWF is Shockwave Flash (uncompressed)
                                    SaveFileDialog d = new SaveFileDialog
                                    {
                                        Title = "Save SWF",
                                        FileName = exp.FullPath + ".swf",
                                        Filter = "*.swf|*.swf"
                                    };
                                    if (d.ShowDialog() == true)
                                    {
                                        File.WriteAllBytes(d.FileName, data);
                                        MessageBox.Show("Done");
                                    }
                                }
                                else
                                {
                                    File.WriteAllBytes(savePath, data);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Error reading/saving SWF data:\n\n" + ex.FlattenException());
                            }
                        }
                        break;
                    case "BioTlkFile":
                        {
                            string extension = Path.GetExtension(".xml");
                            SaveFileDialog d = new SaveFileDialog
                            {
                                Title = "Export TLK as XML",
                                FileName = exp.FullPath + ".xml",
                                Filter = $"*{extension}|*{extension}"
                            };
                            if (d.ShowDialog() == true)
                            {
                                var exportingTalk = new ME1TalkFile(exp);
                                exportingTalk.saveToFile(d.FileName);
                                MessageBox.Show("Done");
                            }
                        }
                        break;
                }
            }
        }

        private void ImportEmbeddedFile()
        {
            if (TryGetSelectedExport(out ExportEntry exp))
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        {
                            try
                            {
                                string extension = Path.GetExtension(".swf");
                                OpenFileDialog d = new OpenFileDialog
                                {
                                    Title = "Replace SWF",
                                    FileName = exp.FullPath + ".swf",
                                    Filter = $"*{extension};*.gfx|*{extension};*.gfx"
                                };
                                if (d.ShowDialog() == true)
                                {
                                    var bytes = File.ReadAllBytes(d.FileName);
                                    var props = exp.GetProperties();

                                    string dataPropName = exp.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
                                    var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                                    //Write SWF data
                                    rawData.bytes = bytes;

                                    //Write SWF metadata
                                    if (exp.FileRef.Game == MEGame.ME1 || exp.FileRef.Game == MEGame.ME2)
                                    {
                                        string sourceFilePropName =
                                            exp.FileRef.Game != MEGame.ME1 ? "SourceFile" : "SourceFilePath";
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
                                        sourceFileTimestamp = File.GetLastWriteTime(d.FileName)
                                            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                    }

                                    exp.WriteProperties(props);
                                    MessageBox.Show("Done");
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Error reading/setting SWF data:\n\n" + ex.FlattenException());
                            }
                        }
                        break;
                    case "BioTlkFile":
                        {
                            string extension = Path.GetExtension(".xml");
                            OpenFileDialog d = new OpenFileDialog
                            {
                                Title = "Replace TLK from exported XML (ME1 Only)",
                                FileName = exp.FullPath + ".xml",
                                Filter = $"*{extension}|*{extension}"
                            };
                            if (d.ShowDialog() == true)
                            {
                                HuffmanCompression compressor = new HuffmanCompression();
                                compressor.LoadInputData(d.FileName);
                                compressor.serializeTalkfileToExport(exp, false);
                            }
                        }
                        break;
                }
            }
        }

        private void RebuildStreamingLevels()
        {
            try
            {
                var levelStreamingKismets = new List<ExportEntry>();
                ExportEntry bioworldinfo = null;
                foreach (ExportEntry exp in Pcc.Exports)
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

                levelStreamingKismets = levelStreamingKismets
                    .OrderBy(o => o.GetProperty<NameProperty>("PackageName").ToString()).ToList();
                if (bioworldinfo != null)
                {
                    var streamingLevelsProp =
                        bioworldinfo.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels") ??
                        new ArrayProperty<ObjectProperty>("StreamingLevels");

                    streamingLevelsProp.Clear();
                    foreach (ExportEntry exp in levelStreamingKismets)
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
                    MessageBox.Show($"{result} already exists in this package file.\nName index: {idx} (0x{idx:X8})",
                        "Name already exists");
                }
                else
                {

                    MessageBox.Show($"{result} has been added as a name.\nName index: {idx} (0x{idx:X8})",
                        "Name added");
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

        private bool NameIsSelected() =>
            CurrentView == CurrentViewMode.Names && LeftSide_ListView.SelectedItem is IndexedName;

        private void EditName()
        {
            if (LeftSide_ListView.SelectedItem is IndexedName iName)
            {
                var name = iName.Name;
                string input = $"Enter a new name to replace this name ({name}) with.";
                string result =
                    PromptDialog.Prompt(this, input, "Enter new name", defaultValue: name, selectText: true);
                if (!string.IsNullOrEmpty(result))
                {
                    Pcc.replaceName(LeftSide_ListView.SelectedIndex, result);
                }
            }
        }

        private void SearchReplaceNames()
        {

            string searchstr = PromptDialog.Prompt(this, "Input text to be replaced:", "Search and Replace Names",
                defaultValue: "search text", selectText: true, PromptDialog.InputType.Text);
            if (string.IsNullOrEmpty(searchstr))
                return;

            string replacestr = PromptDialog.Prompt(this, "Input new text:", "Search and Replace Names",
                defaultValue: "replacement text", selectText: true, PromptDialog.InputType.Text);
            if (string.IsNullOrEmpty(replacestr))
                return;

            var wdlg = MessageBox.Show(
                $"This will replace every name containing the text \"{searchstr}\" with a new name containing \"{replacestr}\".\n" +
                $"This may break any properties, or links containing this string. Please confirm.", "WARNING:",
                MessageBoxButton.OKCancel);
            if (wdlg == MessageBoxResult.Cancel)
                return;

            for (int i = 0; i < Pcc.Names.Count; i++)
            {
                string name = Pcc.Names[i];
                if (name.Contains(searchstr))
                {
                    var newName = name.Replace(searchstr, replacestr);
                    Pcc.replaceName(i, newName);
                }
            }

            RefreshNames();
            RefreshView();
            MessageBox.Show("Done", "Search and Replace Names", MessageBoxButton.OK);
        }

        private void CheckForBadObjectPropertyReferences()
        {
            if (Pcc == null)
            {
                return;
            }

            var badReferences = new List<EntryStringPair>();

            void recursiveCheckProperty(IEntry entry, Property property)
            {
                if (property is UnknownProperty up)
                {
                    badReferences.Add(new EntryStringPair(entry,
                        $"Export {entry.UIndex} had broken property data! Detected unknown properties: {entry.InstancedFullPath}"));

                }
                else if (property is ObjectProperty op)
                {
                    if (op.Value > 0 && op.Value > Pcc.ExportCount)
                    {
                        //bad
                        //bad
                        if (op.Name.Name != null)
                        {
                            badReferences.Add(new EntryStringPair(entry,
                                $"{op.Name.Name} Export {op.Value} is outside of export table, Export #{entry.UIndex} {entry.InstancedFullPath}"));
                        }
                        else
                        {
                            badReferences.Add(new EntryStringPair(entry,
                                $"[Nested property] Export {op.Value} is outside of export table, Export #{entry.UIndex} {entry.InstancedFullPath}"));
                        }
                    }
                    else if (op.Value < 0 && Math.Abs(op.Value) > Pcc.ImportCount)
                    {
                        //bad
                        if (op.Name.Name != null)
                        {
                            badReferences.Add(new EntryStringPair(entry,
                                $"{op.Name.Name} Import {op.Value} is outside of import table, Export #{entry.UIndex} {entry.InstancedFullPath}"));
                        }
                        else
                        {
                            badReferences.Add(new EntryStringPair(entry,
                                $"[Nested property] Import {op.Value} is outside of import table, Export #{entry.UIndex} {entry.InstancedFullPath}"));
                        }
                    }
                    else if (Pcc.GetEntry(op.Value)?.ObjectName.ToString() == "Trash")
                    {
                        badReferences.Add(new EntryStringPair(entry,
                            $"[Nested property] Export {op.Value} is a Trashed object, Export #{entry.UIndex} {entry.InstancedFullPath}"));
                    }
                    else if (Pcc.GetEntry(op.Value)?.ObjectName.ToString() == "ME3ExplorerTrashPackage")
                    {
                        badReferences.Add(new EntryStringPair(entry,
                            $"[Nested property] Export {op.Value} is a Trashed object, Export #{entry.UIndex} {entry.InstancedFullPath}"));
                    }
                }
                else if (property is ArrayProperty<ObjectProperty> aop)
                {
                    foreach (var p in aop)
                    {
                        recursiveCheckProperty(entry, p);
                    }
                }
                else if (property is StructProperty sp)
                {
                    foreach (var p in sp.Properties)
                    {
                        recursiveCheckProperty(entry, p);
                    }
                }
                else if (property is ArrayProperty<StructProperty> asp)
                {
                    foreach (var p in asp)
                    {
                        recursiveCheckProperty(entry, p);
                    }
                }
                else if (property is DelegateProperty dp)
                {
                    if (dp.Value.Object != 0 && !Pcc.IsEntry(dp.Value.Object))
                    {
                        badReferences.Add(new EntryStringPair(entry,
                            $"DelegateProperty {dp.Name.Name} is outside of export table, Export #{entry.UIndex} {entry.InstancedFullPath}"));
                    }
                }
            }

            foreach (ExportEntry exp in Pcc.Exports)
            {
                if (exp.idxArchetype != 0 && !Pcc.IsEntry(exp.idxArchetype))
                {
                    badReferences.Add(new EntryStringPair(exp,
                        $"Archetype {exp.idxArchetype} is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                }

                if (exp.idxSuperClass != 0 && !Pcc.IsEntry(exp.idxSuperClass))
                {
                    badReferences.Add(new EntryStringPair(exp,
                        $"Header SuperClass {exp.idxSuperClass} is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                }

                if (exp.idxClass != 0 && !Pcc.IsEntry(exp.idxClass))
                {
                    badReferences.Add(new EntryStringPair(exp,
                        $"Header Class {exp.idxClass} is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                }

                if (exp.idxLink != 0 && !Pcc.IsEntry(exp.idxLink))
                {
                    badReferences.Add(new EntryStringPair(exp,
                        $"Header Link {exp.idxLink} is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                }

                if (exp.HasComponentMap)
                {
                    foreach (var c in exp.ComponentMap)
                    {
                        if (!Pcc.IsEntry(c.Value))
                        {
                            // Can components point to 0? I don't think so
                            badReferences.Add(new EntryStringPair(exp,
                                $"Header Component Map item ({c.Value}) is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                        }
                    }
                }

                //find stack references
                if (exp.HasStack && exp.Data is byte[] data)
                {
                    var stack1 = EndianReader.ToInt32(data, 0, exp.FileRef.Endian);
                    var stack2 = EndianReader.ToInt32(data, 4, exp.FileRef.Endian);
                    if (stack1 != 0 && !Pcc.IsEntry(stack1))
                    {
                        badReferences.Add(new EntryStringPair(exp,
                            $"Export Stack[0] ({stack1}) is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                    }

                    if (stack2 != 0 && !Pcc.IsEntry(stack2))
                    {
                        badReferences.Add(new EntryStringPair(exp,
                            $"Export Stack[1] ({stack2}) is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                    }
                }
                else if (exp.TemplateOwnerClassIdx is var toci && toci >= 0)
                {
                    var TemplateOwnerClassIdx = EndianReader.ToInt32(exp.Data, toci, exp.FileRef.Endian);
                    if (TemplateOwnerClassIdx != 0 && !Pcc.IsEntry(TemplateOwnerClassIdx))
                    {
                        badReferences.Add(new EntryStringPair(exp, 
                            $"TemplateOwnerClass (Data offset 0x{toci:X}) ({TemplateOwnerClassIdx}) is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                    }
                }

                var props = exp.GetProperties();
                foreach (var p in props)
                {
                    recursiveCheckProperty(exp, p);
                }

                //find binary references
                try
                {
                    if (!exp.IsDefaultObject && ObjectBinary.From(exp) is ObjectBinary objBin)
                    {
                        List<(UIndex, string)> indices = objBin.GetUIndexes(exp.FileRef.Game);
                        foreach ((UIndex uIndex, string propName) in indices)
                        {
                            if (uIndex.value != 0 && !exp.FileRef.IsEntry(uIndex.value))
                            {
                                badReferences.Add(new EntryStringPair(exp,
                                    $"Binary reference ({uIndex.value}) is outside of import/export table, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                            }
                            else if (exp.FileRef.GetEntry(uIndex.value)?.ObjectName.ToString() == "Trash" || exp.FileRef.GetEntry(uIndex.value)?.ObjectName.ToString() == "ME3ExplorerTrashPackage")
                            {
                                badReferences.Add(new EntryStringPair(exp,
                                    $"Binary reference ({uIndex.value}) is a Trashed object, Export #{exp.UIndex} {exp.InstancedFullPath}"));
                            }
                        }

                        var nameIndicies = objBin.GetNames(exp.FileRef.Game);
                        foreach (var ni in nameIndicies)
                        {
                            if (ni.Item1 == "")
                            {
                                badReferences.Add(new EntryStringPair(exp,
                                    $"Binary name reference is invalid in Export #{exp.UIndex} {exp.InstancedFullPath}"));
                            }
                        }
                    }
                }
                catch (Exception e) when (!App.IsDebug)
                {
                    badReferences.Add(new EntryStringPair(exp,
                        $"Unable to parse binary for export #{exp.UIndex} {exp.InstancedFullPath}"));
                }
            }

            foreach (ImportEntry imp in Pcc.Imports)
            {
                if (imp.idxLink != 0 && !Pcc.TryGetEntry(imp.idxLink, out _))
                {
                    badReferences.Add(new EntryStringPair(imp, $"Import #{imp.UIndex} has an invalid link value that is outside of the import/export table {imp.idxLink}"));
                } else if (imp.idxLink == imp.UIndex)
                {
                    badReferences.Add(new EntryStringPair(imp, $"Import #{imp.UIndex} has a circular self reference for it's link. The game and the toolset may be unable to handle this condition"));
                }
            }

            if (badReferences.Any())
            {
                MessageBox.Show(badReferences.Count + " invalid object references were found in export properties.",
                    "Bad ObjectProperty references found");
                ListDialog lw = new ListDialog(badReferences, "Bad object references",
                        "The following items have values outside of the range of the import and export tables. Note that this is a best-effort check and may not be 100% accurate.",
                        this)
                { DoubleClickEntryHandler = entryDoubleClick };
                lw.Show();
            }
            else
            {
                MessageBox.Show(
                    "No bad object references were found. Note that this is a best-effort check and may not be 100% accurate.",
                    "Check complete");
            }
        }

        private void CheckForDuplicateIndexes()
        {
            if (Pcc == null)
            {
                return;
            }

            var duplicates = new List<EntryStringPair>();
            var duplicatesPackagePathIndexMapping = new Dictionary<string, List<int>>();
            foreach (ExportEntry exp in Pcc.Exports)
            {
                string key = exp.InstancedFullPath;
                if (key.StartsWith(UnrealPackageFile.TrashPackageName))
                    continue; //Do not report these as requiring re-indexing.
                if (!duplicatesPackagePathIndexMapping.TryGetValue(key, out List<int> indexList))
                {
                    indexList = new List<int>();
                    duplicatesPackagePathIndexMapping[key] = indexList;
                }
                else
                {
                    duplicates.Add(new EntryStringPair(exp,
                        $"{exp.UIndex} {exp.InstancedFullPath} has duplicate index (index value {exp.indexValue})"));
                }

                indexList.Add(exp.UIndex);
            }

            if (duplicates.Count > 0)
            {
                string copy = "";
                foreach (var ei in duplicates)
                {

                    copy += ei.Message + "\n";
                }

                //Clipboard.SetText(copy);
                MessageBox.Show(duplicates.Count + " duplicate indexes were found.", "BAD INDEXING");
                ListDialog lw = new ListDialog(duplicates, "Duplicate indexes",
                        "The following items have duplicate indexes. The game may choose to use the first occurance of the index it finds, or may crash if indexing is checked internally (such as pathfinding). You can reindex an object to force all same named items to be reindexed in the given unique path. You should reindex from the topmost duplicate entry first if one is found, as it may resolve lower item duplicates.",
                        this)
                { DoubleClickEntryHandler = entryDoubleClick };
                lw.Show();
            }
            else
            {
                MessageBox.Show("No duplicate indexes were found.", "Indexing OK");
            }
        }

        private void ReindexDuplicateIndexes()
        {
            if (Pcc == null)
            {
                return;
            }

            if (MessageBox.Show(
                $"This will reindex all objects that have duplicate indexing. Objects this will affect can be seen via `Debugging > Check for duplicate indexes`\n" +
                "If you don't understand what this does, do not do it!\n\n" +
                "Ensure this file has a backup, this operation may cause the file to stop working if you use it improperly.",
                "Confirm Reindexing",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var duplicatesPackagePathIndexMapping = new Dictionary<string, List<ExportEntry>>();
                foreach (ExportEntry exp in Pcc.Exports)
                {
                    string key = exp.InstancedFullPath;
                    if (key.StartsWith(UnrealPackageFile.TrashPackageName))
                        continue; //Do not report these as requiring re-indexing.
                    if (!duplicatesPackagePathIndexMapping.TryGetValue(key, out List<ExportEntry> indexList))
                    {
                        indexList = new List<ExportEntry>();
                        duplicatesPackagePathIndexMapping[key] = indexList;
                    }

                    indexList.Add(exp);
                }

                foreach (ExportEntry exp in duplicatesPackagePathIndexMapping.Values.Where(list => list.Count > 1)
                    .Select(list => list.First()))
                {
                    ReindexObjectsByName(exp, false);
                }
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
                            MetadataTab_MetadataEditor.SetHexboxSelectedOffset(imp.HeaderOffset + imp.Header.Length -
                                                                               offsetDec);
                            return;
                        }
                    }

                    foreach (ExportEntry exp in Pcc.Exports)
                    {
                        //header
                        if (offsetDec >= exp.HeaderOffset && offsetDec < exp.HeaderOffset + exp.Header.Length)
                        {
                            GoToNumber(exp.UIndex);
                            Metadata_Tab.IsSelected = true;
                            MetadataTab_MetadataEditor.SetHexboxSelectedOffset(exp.HeaderOffset + exp.Header.Length -
                                                                               offsetDec);
                            return;
                        }

                        //data
                        if (offsetDec >= exp.DataOffset && offsetDec < exp.DataOffset + exp.DataSize)
                        {
                            GoToNumber(exp.UIndex);
                            int inExportDataOffset = exp.DataOffset + exp.DataSize - offsetDec;
                            int propsEnd = exp.propsEnd();

                            if (inExportDataOffset > propsEnd && exp.DataSize > propsEnd &&
                                BinaryInterpreterTab_BinaryInterpreter.CanParse(exp))
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

        private void CloneTree(int numClones)
        {
            if (CurrentView == CurrentViewMode.Tree && TryGetSelectedEntry(out IEntry entry))
            {
                int lastTreeRoot = 0;
                for (int i = 0; i < numClones; i++)
                {
                    IEntry newTreeRoot = EntryCloner.CloneTree(entry);
                    TryAddToPersistentLevel(newTreeRoot);
                    lastTreeRoot = newTreeRoot.UIndex;
                }
                GoToNumber(lastTreeRoot);
            }
        }

        private void CloneEntryMultiple()
        {
            var result = PromptDialog.Prompt(this, "How many times do you want to clone this entry?", "Multiple entry cloning", "2", true);
            if (int.TryParse(result, out var howManyTimes) && howManyTimes > 0)
            {
                CloneEntry(howManyTimes);
            }
        }

        private void CloneTreeMultiple()
        {
            var result = PromptDialog.Prompt(this, "How many times do you want to clone this tree?", "Multiple tree cloning", "2", true);
            if (int.TryParse(result, out var howManyTimes) && howManyTimes > 0)
            {
                CloneTree(howManyTimes);
            }
        }

        private void CloneEntry(int numClones)
        {
            if (TryGetSelectedEntry(out IEntry entry))
            {
                int lastClonedUIndex = 0;
                for (int i = 0; i < numClones; i++)
                {
                    IEntry newEntry = EntryCloner.CloneEntry(entry);
                    TryAddToPersistentLevel(newEntry);
                    lastClonedUIndex = newEntry.UIndex;
                }
                GoToNumber(lastClonedUIndex);
            }
        }

        private bool TryAddToPersistentLevel(params IEntry[] newEntries) =>
            TryAddToPersistentLevel((IEnumerable<IEntry>)newEntries);

        private bool TryAddToPersistentLevel(IEnumerable<IEntry> newEntries)
        {
            ExportEntry[] actorsToAdd = newEntries.OfType<ExportEntry>()
                .Where(exp => exp.Parent?.ClassName == "Level" && exp.IsA("Actor")).ToArray();
            int num = actorsToAdd.Length;
            if (num > 0 && Pcc.AddToLevelActorsIfNotThere(actorsToAdd))
            {
                MessageBox.Show(this,
                    $"Added actor{(num > 1 ? "s" : "")} to PersistentLevel's Actor list:\n{actorsToAdd.Select(exp => exp.ObjectName.Instanced).StringJoin("\n")}");
                return true;
            }

            return false;
        }

        private void ImportBinaryData() => ImportExpData(true);

        private void ImportAllData() => ImportExpData(false);

        private void ImportExpData(bool binaryOnly)
        {
            if (!TryGetSelectedExport(out ExportEntry export))
            {
                return;
            }

            OpenFileDialog d = new OpenFileDialog
            {
                Filter = "*.bin|*.bin",
                FileName = export.ObjectName.Instanced + ".bin"
            };
            if (d.ShowDialog() == true)
            {
                byte[] data = File.ReadAllBytes(d.FileName);
                if (binaryOnly)
                {
                    export.WriteBinary(data);
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
            if (!TryGetSelectedExport(out ExportEntry export))
            {
                return;
            }

            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "*.bin|*.bin",
                FileName = export.ObjectName.Instanced + ".bin"
            };
            if (d.ShowDialog() == true)
            {
                File.WriteAllBytes(d.FileName, binaryOnly ? export.GetBinaryData() : export.Data);
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
                string extension = Path.GetExtension(Pcc.FilePath);
                OpenFileDialog d = new OpenFileDialog { Filter = "*" + extension + "|*" + extension };
                if (d.ShowDialog() == true)
                {
                    if (Pcc.FilePath == d.FileName)
                    {
                        MessageBox.Show("You selected the same file as the one already open.");
                        return;
                    }

                    CompareToPackageWrapper(diskPath: d.FileName);
                }
            }
        }

        private void CompareToPackageWrapper(IMEPackage package = null, string diskPath = null, Stream packageStream = null)
        {
            Task.Run(() =>
                    {
                        BusyText = "Comparing packages...";
                        IsBusy = true;
                        try
                        {
                            if (package != null) return (object)Pcc.CompareToPackage(package);
                            if (diskPath != null) return (object)Pcc.CompareToPackage(diskPath);
                            if (packageStream != null) return (object)Pcc.CompareToPackage(packageStream);
                            return "CompareToPackageWrapper() requires at least one parameter be set!";
                        }
                        catch (Exception e)
                        {
                            return e.Message;
                        }
                    }).ContinueWithOnUIThread(result =>
                    {
                        IsBusy = false;
                        if (result.Result is string errorMessage)
                        {
                            MessageBox.Show(errorMessage, "Error comparing packages");
                        }
                        else if (result.Result is List<EntryStringPair> results)
                        {
                            if (results.Any())
                            {
                                ListDialog ld = new ListDialog(results, "Changed exports/imports/names between files",
                                        "The following exports, imports, and names are different between the files.", this)
                                { DoubleClickEntryHandler = entryDoubleClick };
                                ld.Show();
                            }
                            else
                            {
                                MessageBox.Show("No changes between names/imports/exports were found between the files.", "Packages seem identical");
                            }
                        }
                    });
        }

        private bool CanCompareToUnmodded() => PackageIsLoaded() && Pcc.Game != MEGame.UDK &&
                                               !(Pcc.IsInBasegame() || Pcc.IsInOfficialDLC());

        private void CompareUnmodded()
        {
            if (Pcc.Game != MEGame.ME1 && Pcc.Game != MEGame.ME2 && Pcc.Game != MEGame.ME3)
            {
                MessageBox.Show(this, "Not a trilogy file!");
                return;
            }

            Task.Run(() =>
            {
                BusyText = "Finding unmodded candidates...";
                IsBusy = true;
                string lookupFilename = Path.GetFileName(Pcc.FilePath);
                string dlcPath = MEDirectories.GetDLCPath(Pcc.Game);
                var backupPath = ME3TweaksBackups.GetGameBackupPath(Pcc.Game);
                var unmoddedCandidates = new UnmoddedCandidatesLookup();

                // Lookup unmodded ON DISK files
                List<string> unModdedFileLookup(string filename)
                {
                    List<string> inGameCandidates = MEDirectories.OfficialDLC(Pcc.Game)
                        .Select(dlcName => Path.Combine(dlcPath, dlcName))
                        .Prepend(MEDirectories.GetCookedPath(Pcc.Game))
                        .Where(Directory.Exists)
                        .Select(cookedPath =>
                            Directory.EnumerateFiles(cookedPath, "*", SearchOption.AllDirectories)
                                .FirstOrDefault(path => Path.GetFileName(path) == filename))
                        .NonNull().ToList();

                    if (backupPath != null)
                    {
                        var backupDlcPath = MEDirectories.GetDLCPath(Pcc.Game, backupPath);
                        inGameCandidates.AddRange(MEDirectories.OfficialDLC(Pcc.Game)
                            .Select(dlcName => Path.Combine(backupDlcPath, dlcName))
                            .Prepend(MEDirectories.GetCookedPath(Pcc.Game, backupPath))
                            .Where(Directory.Exists)
                            .Select(cookedPath =>
                                Directory.EnumerateFiles(cookedPath, "*", SearchOption.AllDirectories)
                                    .FirstOrDefault(path => Path.GetFileName(path) == filename))
                            .NonNull());
                    }

                    return inGameCandidates;
                }

                unmoddedCandidates.DiskFiles.AddRange(unModdedFileLookup(lookupFilename));
                if (unmoddedCandidates.DiskFiles.IsEmpty())
                {
                    //Try to lookup using info in this file
                    var packages = Pcc.Exports.Where(x => x.ClassName == "Package" && x.idxLink == 0).ToList();
                    foreach (var p in packages)
                    {
                        if ((p.PackageFlags & EPackageFlags.Cooked) != 0)
                        {
                            //try this one
                            var objName = p.ObjectName;
                            if (p.indexValue > 0) objName += $"_{p.indexValue - 1}"; //Some ME3 map files are indexed
                            var cookedPackageName = objName + (Pcc.Game == MEGame.ME1 ? ".sfm" : ".pcc");
                            unmoddedCandidates.DiskFiles.ReplaceAll(unModdedFileLookup(cookedPackageName)); //ME1 could be upk/u too I guess, but I think only sfm have packages cooked into them
                            break;
                        }
                    }
                }

                //if (filecandidates.Any())
                //{
                //    // Use em'
                //    string filePath = InputComboBoxWPF.GetValue(this, "Choose file to compare to:",
                //        "Unmodified file comparison", filecandidates, filecandidates.Last());

                //    if (string.IsNullOrEmpty(filePath))
                //    {
                //        return null;
                //    }

                //    ComparePackage(filePath);
                //    return true;
                //}

                if (Pcc.Game == MEGame.ME3 && backupPath != null)
                {
                    var backupDlcPath = Path.Combine(backupPath, "BIOGame", "DLC");
                    if (Directory.Exists(dlcPath))
                    {
                        var sfars = Directory.GetFiles(backupDlcPath, "*.sfar", SearchOption.AllDirectories).ToList();

                        var testPatch = Path.Combine(backupDlcPath, "BIOGame", "Patches", "PCConsole", "Patch_001.sfar");
                        if (File.Exists(testPatch))
                        {
                            sfars.Add(testPatch);
                        }

                        foreach (var sfar in sfars)
                        {
                            DLCPackage dlc = new DLCPackage(sfar);
                            // Todo: Port in M3's better SFAR lookup code
                            var sfarIndex = dlc.FindFileEntry(Path.GetFileName(lookupFilename));
                            if (sfarIndex >= 0)
                            {
                                var uiName = Path.GetFileName(sfar) == "Patch_001.sfar" ? "TestPatch" : Directory.GetParent(sfar).Parent.Name;
                                unmoddedCandidates.SFARPackageStreams[$"{uiName} SFAR"] = dlc.DecompressEntry(sfarIndex);
                            }
                        }
                    }
                }

                return unmoddedCandidates;
            }).ContinueWithOnUIThread(foundCandidates =>
           {
               IsBusy = false;
               if (!foundCandidates.Result.Any()) MessageBox.Show(this, "Cannot find any candidates for this file!");

               var choices = foundCandidates.Result.DiskFiles.ToList(); //make new list
               choices.AddRange(foundCandidates.Result.SFARPackageStreams.Select(x => x.Key));

               var choice = InputComboBoxWPF.GetValue(this, "Choose file to compare to:", "Unmodified file comparison", choices, choices.Last());
               if (string.IsNullOrEmpty(choice))
               {
                   return;
               }

               if (foundCandidates.Result.DiskFiles.Contains(choice))
               {
                   CompareToPackageWrapper(diskPath: choice);
               }
               else if (foundCandidates.Result.SFARPackageStreams.TryGetValue(choice, out var packageStream))
               {
                   CompareToPackageWrapper(packageStream: packageStream);
               }
               else
               {
                   MessageBox.Show("Selected candidate not found in the lists! This is a bug", "OH NO");
               }
           });
        }

        private class UnmoddedCandidatesLookup
        {
            public List<string> DiskFiles = new List<string>();
            public Dictionary<string, Stream> SFARPackageStreams = new Dictionary<string, Stream>();
            public bool Any() => DiskFiles.Any() || SFARPackageStreams.Any();
        }



        #endregion

        public PackageEditorWPF(bool submitTelemetry = true) : base("Package Editor", submitTelemetry)
        {
            CurrentView = CurrentViewMode.Tree;
            LoadCommands();

            InitializeComponent();
            DataContext = this;
            ((FrameworkElement)Resources["EntryContextMenu"]).DataContext = this;

            //map export loaders to their tabs
            ExportLoaders[InterpreterTab_Interpreter] = Interpreter_Tab;
            ExportLoaders[MetadataTab_MetadataEditor] = Metadata_Tab;
            ExportLoaders[SoundTab_Soundpanel] = Sound_Tab;
            ExportLoaders[CurveTab_CurveEditor] = CurveEditor_Tab;
            ExportLoaders[FaceFXTab_Editor] = FaceFXAnimSet_Tab;
            ExportLoaders[Bio2DATab_Bio2DAEditor] = Bio2DAViewer_Tab;
            ExportLoaders[BytecodeTab_BytecodeEditor] = Bytecode_Tab;
            ExportLoaders[ScriptTab_UnrealScriptIDE] = Script_Tab;
            ExportLoaders[BinaryInterpreterTab_BinaryInterpreter] = BinaryInterpreter_Tab;
            ExportLoaders[EmbeddedTextureViewerTab_EmbededTextureViewer] = EmbeddedTextureViewer_Tab;
            ExportLoaders[ME1TlkEditorWPFTab_ME1TlkEditor] = ME1TlkEditorWPF_Tab;
            ExportLoaders[JPEXLauncherTab_JPEXLauncher] = JPEXLauncher_Tab;
            ExportLoaders[MeshRendererTab_MeshRenderer] = MeshRenderer_Tab;
            ExportLoaders[MaterialViewerTab_MaterialExportLoader] = MaterialViewer_Tab;
            ExportLoaders[RADLauncherTab_BIKLauncher] = RADLaunch_Tab;
            ExportLoaders[CollectionActorEditorTab_CollectionActorEditor] = CollectionActorEditor_Tab;
            ExportLoaders[ParticleSystemTab_ParticleSystemLoader] = ParticleSystem_Tab;
            ExportLoaders[ParticleModuleTab_ParticleModuleLoader] = ParticleModule_Tab;


            InterpreterTab_Interpreter.SetParentNameList(NamesList); //reference to this control for name editor set
            BinaryInterpreterTab_BinaryInterpreter
                .SetParentNameList(NamesList); //reference to this control for name editor set
            Bio2DATab_Bio2DAEditor.SetParentNameList(NamesList); //reference to this control for name editor set

            InterpreterTab_Interpreter.HideHexBox = Properties.Settings.Default.PackageEditor_HideInterpreterHexBox;
            InterpreterTab_Interpreter.ToggleHexbox_Button.Visibility = Visibility.Visible;

            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileName => LoadFile(fileName));
        }

        public void LoadFile(string s, int goToIndex = 0)
        {
            try
            {
                preloadPackage(Path.GetFileName(s), new FileInfo(s).Length);
                LoadMEPackage(s);
                postloadPackage(Path.GetFileName(s), s, goToIndex);

                RecentsController.AddRecent(s, false);
                RecentsController.SaveRecentList(true);
            }
            catch (Exception e) when (!App.IsDebug)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(s);
                MessageBox.Show($"Error loading {Path.GetFileName(s)}:\n{e.Message}");
                IsBusy = false;
                IsBusyTaskbar = false;
                //throw e;
            }
        }

        /// <summary>
        /// Call once the MEPackage has been loaded and set
        /// </summary>
        /// <param name="uiname"></param>
        /// <param name="goToIndex"></param>
        private void postloadPackage(string shortname, string fullname, int goToIndex = 0)
        {
            RefreshView();
            InitStuff();
            StatusBar_LeftMostText.Text = shortname;
            Title = $"Package Editor - {fullname}";
            InterpreterTab_Interpreter.UnloadExport();

            QueuedGotoNumber = goToIndex;

            InitializeTreeView();
        }

        /// <summary>
        /// Call this before loading an ME Package to clear the UI up and show the loading interface
        /// </summary>
        /// <param name="loadingName"></param>
        /// <param name="loadingSize"></param>
        private void preloadPackage(string loadingName, long loadingSize)
        {
            BusyText = $"Loading {loadingName}";
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
            crossPCCObjectMap.Clear();
            BackwardsIndexes = new Stack<int>();
            ForwardsIndexes = new Stack<int>();
            StatusBar_LeftMostText.Text =
                $"Loading {loadingName} ({FileSize.FormatSize(loadingSize)})";
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
        }

        private void InitializeTreeViewBackground_Completed(
            Task<ObservableCollectionExtended<TreeViewEntry>> prevTask)
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
                Goto_TextBox.Text = QueuedGotoNumber.ToString();
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

            BusyText = "Loading " + Path.GetFileName(Pcc.FilePath);
            if (Pcc == null)
            {
                return null;
            }

            IReadOnlyList<ImportEntry> Imports = Pcc.Imports;
            IReadOnlyList<ExportEntry> Exports = Pcc.Exports;

            var rootEntry = new TreeViewEntry(null, Path.GetFileName(Pcc.FilePath)) { IsExpanded = true };

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
            if (Pcc == null)
            {
                return;
            }

            Task.Run(InitializeTreeViewBackground)
                .ContinueWithOnUIThread(InitializeTreeViewBackground_Completed);
        }

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
            InitClassDropDown();
            MetadataTab_MetadataEditor.LoadPccData(Pcc);
            RefreshNames();
            if (CurrentView != CurrentViewMode.Tree)
            {
                RefreshView(); //Tree will initialize itself in thread
            }
        }

        private void InitClassDropDown() =>
            ClassDropdownList.ReplaceAll(Pcc.Exports.Select(x => x.ClassName).NonNull().Distinct().ToList()
                .OrderBy(p => p));

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
                case CurrentViewMode.Tree when SelectedItem is TreeViewEntry selected:
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
            if (GetSelected(out int uIndex) && Pcc.IsEntry(uIndex))
            {
                entry = Pcc.GetEntry(uIndex);
                return true;
            }

            entry = null;
            return false;
        }

        private bool TryGetSelectedExport(out ExportEntry export)
        {
            if (GetSelected(out int uIndex) && Pcc.IsUExport(uIndex))
            {
                export = Pcc.GetUExport(uIndex);
                return true;
            }

            export = null;
            return false;
        }

        private bool TryGetSelectedImport(out ImportEntry import)
        {
            if (GetSelected(out int uIndex) && Pcc.IsImport(uIndex))
            {
                import = Pcc.GetImport(uIndex);
                return true;
            }

            import = null;
            return false;
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            List<PackageChange> changes = updates.Select(x => x.Change).ToList();
            if (changes.Any(x => x.HasFlag(PackageChange.Name)))
            {
                foreach (ExportLoaderControl elc in ExportLoaders.Keys)
                {
                    elc.SignalNamelistAboutToUpdate();
                }

                RefreshNames(updates.Where(x => x.Change.HasFlag(PackageChange.Name)).ToList());
                foreach (ExportLoaderControl elc in ExportLoaders.Keys)
                {
                    elc.SignalNamelistChanged();
                }
            }

            if (updates.Any(x => x.Change == PackageChange.ExportRemove || x.Change == PackageChange.ImportRemove))
            {
                InitializeTreeView();
                InitClassDropDown();
                MetadataTab_MetadataEditor.RefreshAllEntriesList(Pcc);
                Preview();
                return;
            }

            bool hasImportChanges = changes.Any(x => x.HasFlag(PackageChange.Import));
            bool hasExportNonDataChanges =
                changes.Any(x => x != PackageChange.ExportData && x.HasFlag(PackageChange.Export));
            bool hasSelection = GetSelected(out int selectedEntryUIndex);

            List<PackageUpdate> addedChanges = updates.Where(x => x.Change.HasFlag(PackageChange.EntryAdd))
                .OrderBy(x => x.Index).ToList();
            if (addedChanges.Count > 0)
            {
                InitClassDropDown();
                MetadataTab_MetadataEditor.RefreshAllEntriesList(Pcc);
                //Find nodes that haven't been generated and added yet

                List<TreeViewEntry> treeViewItems = AllTreeViewNodesX[0].FlattenTree();

                //filter to only nodes that don't exist yet (created by external tools)
                foreach (TreeViewEntry tvi in treeViewItems)
                {
                    addedChanges.RemoveAll(x => x.Index == tvi.UIndex);
                }

                List<IEntry> entriesToAdd = addedChanges.Select(change => Pcc.GetEntry(change.Index)).ToList();

                //Generate new nodes
                var nodesToSortChildrenFor = new HashSet<TreeViewEntry>();
                //might have to loop a few times if it contains children before parents
                while (entriesToAdd.Any())
                {
                    var orphans = new List<IEntry>();
                    foreach (IEntry entry in entriesToAdd)
                    {

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
                            orphans.Add(entry);
                        }
                    }

                    if (orphans.Count == entriesToAdd.Count)
                    {
                        //actual orphans
                        Debug.WriteLine("Unable to attach new items to parents.");
                        break;
                    }

                    entriesToAdd = orphans;
                }

                SuppressSelectionEvent = true;
                nodesToSortChildrenFor.ToList().ForEach(x => x.SortChildren());
                SuppressSelectionEvent = false;

                if (CurrentView == CurrentViewMode.Imports)
                {
                    foreach (PackageUpdate update in addedChanges)
                    {
                        if (update.Index < 0)
                        {
                            LeftSideList_ItemsSource.Add(Pcc.GetEntry(update.Index));
                        }
                    }
                }

                if (CurrentView == CurrentViewMode.Exports)
                {
                    foreach (PackageUpdate update in addedChanges)
                    {
                        if (update.Index > 0)
                        {
                            LeftSideList_ItemsSource.Add(Pcc.GetEntry(update.Index));
                        }
                    }
                }
            }

            var headerChanges = updates.Where(x => x.Change.HasFlag(PackageChange.EntryHeader)).Select(x => x.Index)
                .ToHashSet();
            if (headerChanges.Count > 0)
            {
                List<TreeViewEntry> tree = AllTreeViewNodesX[0].FlattenTree();
                var nodesNeedingResort = new List<TreeViewEntry>();

                List<TreeViewEntry> tviWithChangedHeaders =
                    tree.Where(x => x.UIndex != 0 && headerChanges.Contains(x.Entry.UIndex)).ToList();
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


            if (CurrentView == CurrentViewMode.Imports && hasImportChanges ||
                CurrentView == CurrentViewMode.Exports && hasExportNonDataChanges ||
                CurrentView == CurrentViewMode.Tree && (hasImportChanges || hasExportNonDataChanges))
            {
                RefreshView();
                if (QueuedGotoNumber != 0 && GoToNumber(QueuedGotoNumber))
                {
                    QueuedGotoNumber = 0;
                }
                else if (hasSelection)
                {
                    GoToNumber(selectedEntryUIndex);
                }
            }

            if ((CurrentView == CurrentViewMode.Exports || CurrentView == CurrentViewMode.Tree) && hasSelection &&
                updates.Contains(new PackageUpdate(PackageChange.ExportData, selectedEntryUIndex)))
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
                NamesList.ReplaceAll(Pcc.Names.Select((name, i) =>
                    new IndexedName(i,
                        name))); //we replaceall so we don't add one by one and trigger tons of notifications
            }
            else
            {
                //only modify the list
                updates = updates.OrderBy(x => x.Index).ToList(); //ensure ascending order
                foreach (PackageUpdate update in updates)
                {
                    if (update.Index >= Pcc.NameCount)
                    {
                        continue;
                    }

                    if (update.Change == PackageChange.NameAdd) //names are 0 indexed
                    {
                        NameReference nr = Pcc.Names[update.Index];
                        NamesList.Add(new IndexedName(update.Index, nr));
                        LeftSideList_ItemsSource.Add(new IndexedName(update.Index, nr));
                    }
                    else if (update.Change == PackageChange.NameEdit)
                    {
                        IndexedName indexed = new IndexedName(update.Index, Pcc.Names[update.Index]);
                        NamesList[update.Index] = indexed;
                        if (CurrentView == CurrentViewMode.Names)
                        {
                            LeftSideList_ItemsSource.ReplaceAll(NamesList);
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
                foreach ((ExportLoaderControl exportLoader, TabItem tab) in ExportLoaders)
                {
                    exportLoader.UnloadExport();
                    tab.Visibility = Visibility.Collapsed;
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
            if (CurrentView == CurrentViewMode.Imports || CurrentView == CurrentViewMode.Exports ||
                CurrentView == CurrentViewMode.Tree)
            {
                Interpreter_Tab.IsEnabled = selectedEntry is ExportEntry;
                if (selectedEntry is ExportEntry exportEntry)
                {
                    foreach ((ExportLoaderControl exportLoader, TabItem tab) in ExportLoaders)
                    {
                        if (exportLoader.CanParse(exportEntry))
                        {
                            exportLoader.LoadExport(exportEntry);
                            tab.Visibility = Visibility.Visible;

                        }
                        else
                        {
                            tab.Visibility = Visibility.Collapsed;
                            exportLoader.UnloadExport();
                        }
                    }

                    if (Interpreter_Tab.IsSelected && exportEntry.ClassName == "Class")
                    {
                        //We are on interpreter tab, selecting class. Switch to binary interpreter as interpreter will never be useful
                        BinaryInterpreter_Tab.IsSelected = true;
                    }

                    if (Interpreter_Tab.IsSelected && exportEntry.ClassName == "Function" && Bytecode_Tab.IsVisible)
                    {
                        Bytecode_Tab.IsSelected = true;
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
        public bool GoToNumber(int entryIndex)
        {
            if (entryIndex == 0)
            {
                return false; //PackageEditorWPF uses Unreal Indexing for entries
            }

            if (IsLoadingFile)
            {
                QueuedGotoNumber = entryIndex;
                return false;
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
                        List<TreeViewEntry> selectNode =
                            list.Where(s => s.Entry != null && s.UIndex == entryIndex).ToList();
                        if (selectNode.Any())
                        {
                            //selectNode[0].ExpandParents();
                            selectNode[0].IsProgramaticallySelecting = true;
                            SelectedItem = selectNode[0];
                            //FocusTreeViewNodeOld(selectNode[0]);

                            //selectNode[0].Focus(LeftSide_TreeView);
                            return true;
                        }

                        QueuedGotoNumber = entryIndex; //May be trying to select node that doesn't exist yet
                        break;
                    }
                case CurrentViewMode.Exports:
                case CurrentViewMode.Imports:
                    {
                        //Check bounds
                        var entry = Pcc.GetEntry(entryIndex);
                        if (entry != null)
                        {
                            //UI switch
                            if (CurrentView == CurrentViewMode.Exports && entry is ImportEntry)
                            {
                                CurrentView = CurrentViewMode.Imports;
                            }
                            else if (CurrentView == CurrentViewMode.Imports && entry is ExportEntry)
                            {
                                CurrentView = CurrentViewMode.Exports;
                            }

                            LeftSide_ListView.SelectedIndex = Math.Abs(entryIndex) - 1;
                            return true;
                        }

                        break;
                    }
                case CurrentViewMode.Names when entryIndex >= 0 && entryIndex < LeftSide_ListView.Items.Count:
                    //Names
                    LeftSide_ListView.SelectedIndex = entryIndex;
                    return true;
            }

            return false;
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
                            Goto_Preview_TextBox.Text = Pcc.GetNameEntry(index);
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
                            var entry = Pcc.GetEntry(index);
                            if (entry != null)
                            {
                                Goto_Preview_TextBox.Text = entry.InstancedFullPath;
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
            if (dropInfo.TargetItem is TreeViewEntry targetItem && dropInfo.Data is TreeViewEntry sourceItem &&
                sourceItem.Parent != null)
            {
                //Check if the path of the target and the source is the same. If so, offer to merge instead
                crossPCCObjectMap.Clear();

                if (sourceItem == targetItem ||
                    (targetItem.Entry != null && sourceItem.Entry.FileRef == targetItem.Entry.FileRef))
                {
                    return; //ignore
                }

                var portingOption = TreeMergeDialog.GetMergeType(this, sourceItem, targetItem, Pcc.Game);

                if (portingOption == EntryImporter.PortingOption.Cancel)
                {
                    return;
                }


                if (sourceItem.Entry.FileRef == null)
                {
                    return;
                }

                // To profile this, run dotTrace and attach to the process, make sure to choose option to profile via API
                //MeasureProfiler.StartCollectingData(); // Start profiling
                //var sw = new Stopwatch();
                //sw.Start();
                IEntry sourceEntry = sourceItem.Entry;
                IEntry targetLinkEntry = targetItem.Entry;

                int numExports = Pcc.ExportCount;
                //Import!
                var relinkResults = EntryImporter.ImportAndRelinkEntries(portingOption, sourceEntry, Pcc,
                    targetLinkEntry, true, out IEntry newEntry, crossPCCObjectMap);

                TryAddToPersistentLevel(Pcc.Exports.Skip(numExports));

                crossPCCObjectMap.Clear();
                //sw.Stop();
                //MessageBox.Show($"Took {sw.ElapsedMilliseconds}ms");
                //MeasureProfiler.SaveData(); // End profiling
                if ((relinkResults?.Count ?? 0) > 0)
                {
                    ListDialog ld = new ListDialog(relinkResults, "Relink report",
                        "The following items failed to relink.", this);
                    ld.Show();
                }
                else
                {
                    MessageBox.Show(
                        "Items have been ported and relinked with no reported issues.\nNote that this does not mean all binary properties were relinked, only supported ones were.");
                }

                RefreshView();
                GoToNumber(newEntry.UIndex);
            }
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
                FindNextObjectByClass(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
            }
        }

        /// <summary>
        /// Finds the next entry that has the selected class from the dropdown.
        /// </summary>
        private void FindNextObjectByClass(bool reverse)
        {
            if (Pcc == null)
                return;
            if (ClassDropdown_Combobox.SelectedItem == null)
                return;

            string searchClass = ClassDropdown_Combobox.SelectedItem.ToString();

            void LoopFunc(ref int integer, int count)
            {
                if (reverse)
                {
                    integer--;
                }
                else
                {
                    integer++;
                }

                if (integer < 0)
                {
                    integer = count - 1;
                }
                else if (integer >= count)
                {
                    integer = 0;
                }
            }

            if (CurrentView == CurrentViewMode.Tree)
            {
                TreeViewEntry selectedNode = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                List<TreeViewEntry> items = AllTreeViewNodesX[0].FlattenTree();
                int pos = selectedNode == null ? 0 : items.IndexOf(selectedNode);
                LoopFunc(ref pos,
                    items.Count); //increment 1 forward or back to start so we don't immediately find ourself.
                for (int i = pos, numSearched = 0;
                    numSearched < items.Count;
                    LoopFunc(ref i, items.Count), numSearched++)
                {
                    //int curIndex = (i + pos) % items.Count;
                    TreeViewEntry node = items[i];
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
                //Todo: Loopfunc
                int n = LeftSide_ListView.SelectedIndex;
                int start;
                if (n == -1)
                    start = 0;
                else
                    start = n + 1;
                if (CurrentView == CurrentViewMode.Exports)
                {
                    for (int i = start; i < Pcc.Exports.Count; i++)
                    {
                        if (Pcc.Exports[i].ClassName == searchClass)
                        {
                            LeftSide_ListView.SelectedIndex = i;
                            break;
                        }
                    }
                }
                else if (CurrentView == CurrentViewMode.Imports)
                {
                    for (int i = start; i < Pcc.Imports.Count; i++)
                    {
                        if (Pcc.Imports[i].ClassName == searchClass)
                        {
                            LeftSide_ListView.SelectedIndex = i;
                            break;
                        }
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
            FindNextObjectByClass(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
        }

        /// <summary>
        /// Click handler for the search button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchButton_Clicked(object sender, RoutedEventArgs e)
        {
            Search(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
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
                Search(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
            }
        }

        /// <summary>
        /// Takes the contents of the search box and finds the next instance of it.
        /// </summary>
        private void Search(bool reverseSearch)
        {
            if (Pcc == null)
                return;
            int start = LeftSide_ListView.SelectedIndex;
            if (Search_TextBox.Text == "")
                return;
            //int start;
            //if (n == -1)
            //    start = 0;
            //else
            //    start = n + 1;


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
            void LoopFunc(ref int integer, int count)
            {
                if (reverseSearch)
                {
                    integer--;
                }
                else
                {
                    integer++;
                }

                if (integer < 0)
                {
                    integer = count - 1;
                }
                else if (integer >= count)
                {
                    integer = 0;
                }
            }


            if (CurrentView == CurrentViewMode.Names)
            {
                LoopFunc(ref start,
                    Pcc.NameCount); //increment 1 forward or back to start so we don't immediately find ourself.
                for (int i = start, numSearched = 0;
                    numSearched < Pcc.Names.Count;
                    LoopFunc(ref i, Pcc.NameCount), numSearched++)
                {
                    if (Pcc.Names[i].ToLower().Contains(searchTerm))
                    {
                        LeftSide_ListView.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (CurrentView == CurrentViewMode.Imports)
            {
                LoopFunc(ref start,
                    Pcc.ImportCount); //increment 1 forward or back to start so we don't immediately find ourself.
                for (int i = start, numSearched = 0;
                    numSearched < Pcc.Imports.Count;
                    LoopFunc(ref i, Pcc.ImportCount), numSearched++)
                {
                    if (Pcc.Imports[i].ObjectName.Name.ToLower().Contains(searchTerm))
                    {
                        LeftSide_ListView.SelectedIndex = i;
                        break;
                    }

                    //if (i >= Imports.Count - 1)
                    //{
                    //    i = -1;
                    //}
                }
            }

            if (CurrentView == CurrentViewMode.Exports)
            {
                LoopFunc(ref start,
                    Pcc.ExportCount); //increment 1 forward or back to start so we don't immediately find ourself.
                for (int i = start, numSearched = 0;
                    numSearched < Pcc.Exports.Count;
                    LoopFunc(ref i, Pcc.ExportCount), numSearched++)
                {
                    if (Pcc.Exports[i].ObjectName.Name.ToLower().Contains(searchTerm))
                    {
                        LeftSide_ListView.SelectedIndex = i;
                        break;
                    }

                    //if (i >= Exports.Count - 1)
                    //{
                    //    i = -1;
                    //}
                }
            }

            if (CurrentView == CurrentViewMode.Tree && AllTreeViewNodesX.Count > 0)
            {
                TreeViewEntry selectedNode = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                var items = AllTreeViewNodesX[0].FlattenTree();
                int pos = selectedNode == null ? -1 : items.IndexOf(selectedNode);

                LoopFunc(ref pos,
                    items.Count); //increment 1 forward or back to start so we don't immediately find ourself.

                //Start at the selected node, then search up or down the number of items in the list. If nothing is found, ding.
                for (int numSearched = 0; numSearched < items.Count; LoopFunc(ref pos, items.Count), numSearched++)

                //for (int i = 0; i < items.Count; LoopFunc(ref i, items.Count))
                {
                    //int curIndex = (i + pos) % items.Count;
                    TreeViewEntry node = items[pos];
                    if (node.Entry == null)
                    {
                        continue;
                    }

                    if (node.Entry.ObjectName.Instanced.ToLower().Contains(searchTerm))
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
            string myBasePath = ME1Directory.DefaultGamePath;
            string[] extensions = { ".u", ".upk" };
            FileInfo[] files = new DirectoryInfo(ME1Directory.CookedPCPath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
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
                    List<ExportEntry> tlkExports = pack.Exports.Where(x =>
                        (x.ObjectName == "tlk" || x.ObjectName == "tlk_M") && x.ClassName == "BioTlkFile").ToList();
                    if (tlkExports.Count > 0)
                    {
                        string subPath = f.FullName.Substring(basePathLen);
                        Debug.WriteLine("Found exports in " + f.FullName.Substring(basePathLen));
                        foreach (ExportEntry exp in tlkExports)
                        {
                            var talkFile = new ME1TalkFile(exp);
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
                if (ext != ".u" && ext != ".upk" && ext != ".pcc" && ext != ".sfm" && ext != ".xxx" && ext != ".udk")
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
        }


        private void TouchComfyMode_Clicked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.TouchComfyMode = !Properties.Settings.Default.TouchComfyMode;
            Properties.Settings.Default.Save();
            TouchComfySettings.ModeSwitched();
        }

        private void ShowImpExpPrefix_Clicked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.PackageEditorWPF_ShowImpExpPrefix =
                !Properties.Settings.Default.PackageEditorWPF_ShowImpExpPrefix;
            Properties.Settings.Default.Save();
            if (AllTreeViewNodesX.Any())
            {
                AllTreeViewNodesX[0].FlattenTree().ForEach(x => x.RefreshDisplayName());
            }
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
                AllTreeViewNodesX.ClearEx();
            }
        }

        private void OpenIn_Clicked(object sender, RoutedEventArgs e)
        {
            var myValue = (string)((MenuItem)sender).Tag;
            switch (myValue)
            {
                case "DialogueEditor":
                    var dialogueEditorWPF = new Dialogue_Editor.DialogueEditorWPF();
                    dialogueEditorWPF.LoadFile(Pcc.FilePath);
                    dialogueEditorWPF.Show();
                    break;
                case "FaceFXEditor":
                    var facefxEditor = new FaceFX.FaceFXEditor();
                    facefxEditor.LoadFile(Pcc.FilePath);
                    facefxEditor.Show();
                    break;
                case "PathfindingEditor":
                    var pathEditor = new PathfindingEditorWPF(Pcc.FilePath);
                    pathEditor.Show();
                    break;
                case "SoundplorerWPF":
                    var soundplorerWPF = new Soundplorer.SoundplorerWPF();
                    soundplorerWPF.LoadFile(Pcc.FilePath);
                    soundplorerWPF.Show();
                    break;
                case "SequenceEditor":
                    var seqEditor = new Sequence_Editor.SequenceEditorWPF();
                    seqEditor.LoadFile(Pcc.FilePath);
                    seqEditor.Show();
                    break;
                case "Meshplorer":
                    var meshplorer = new MeshplorerWPF();
                    meshplorer.LoadFile(Pcc.FilePath);
                    meshplorer.Show();
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

            public override bool Equals(object obj)
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
            Properties.Settings.Default.BinaryInterpreterWPFAutoScanAlways =
                !Properties.Settings.Default.BinaryInterpreterWPFAutoScanAlways;
            Properties.Settings.Default.Save();
        }

        //To be moved to Pathinding Editor WPF. will take some re-architecting though for relinking
        //todo: this should be possible to move now
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

            if (d.FileName == Pcc.FilePath)
            {
                Debug.WriteLine("Same input/target file");
                return;
            }

            ExportEntry targetPersistentLevel;
            using (IMEPackage sourceFile = MEPackageHandler.OpenMEPackage(d.FileName))
            {
                targetPersistentLevel =
                    Pcc.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");

                if (targetPersistentLevel == null)
                {
                    Debug.WriteLine("Could not find persistent level in current file");
                    return;
                }

                var pathnodeForPositioning =
                    Pcc.Exports.FirstOrDefault(x => x.ClassName == "PathNode" && x.ObjectName == "PathNode");
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

                var objectMap = new Dictionary<IEntry, IEntry>();

                var itemsToAddToLevel = new List<ExportEntry>();
                foreach (ExportEntry export in sourceFile.Exports)
                {
                    if (export.ObjectName == "SFXOperation_ObjectiveSpawnPoint")
                    {
                        Debug.WriteLine("Porting " + export.InstancedFullPath);
                        ExportEntry portedObjective = EntryImporter.ImportExport(Pcc, export,
                            targetPersistentLevel.UIndex, objectMapping: objectMap);
                        itemsToAddToLevel.Add(portedObjective);
                        var child = export.GetProperty<ObjectProperty>("CollisionComponent");
                        ExportEntry collCyl = sourceFile.Exports[child.Value - 1];
                        Debug.WriteLine($"Porting {collCyl.InstancedFullPath}");
                        EntryImporter.ImportExport(Pcc, collCyl, portedObjective.UIndex, objectMapping: objectMap);
                    }
                }

                Relinker.RelinkAll(objectMap);

                xPos -= (itemsToAddToLevel.Count / 2) * 55.0f;
                foreach (ExportEntry addingExport in itemsToAddToLevel)
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

                Level level = ObjectBinary.From<Level>(targetPersistentLevel);
                foreach (ExportEntry actorExport in itemsToAddToLevel)
                {
                    level.Actors.Add(actorExport.UIndex);
                }

                targetPersistentLevel.WriteBinary(level);
            }


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
                            var filesToSkip = new[]
                            {
                                "BioD_Cit004_270ShuttleBay1", "BioD_Cit003_600MechEvent", "CAT6_Executioner",
                                "SFXPawn_Demo", "SFXPawn_Sniper", "SFXPawn_Heavy", "GethAssassin",
                                "BioD_OMG003_125LitExtra"
                            };
                            foreach (ExportEntry exp in package.Exports)
                            {
                                if (exp.ClassName == "Package" && exp.idxLink == 0 &&
                                    !filesToSkip.Contains(exp.ObjectName.Name))
                                {
                                    if (string.Equals(exp.ObjectName.Name, fname,
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        hasPackageNamingItself = true;
                                    }

                                    Guid guid = exp.PackageGUID;
                                    if (guid != Guid.Empty)
                                    {
                                        GuidPackageMap.TryGetValue(guid, out string packagename);
                                        if (packagename != null && packagename != exp.ObjectName.Name)
                                        {
                                            Debug.WriteLine(
                                                $"-> {exp.UIndex} {exp.ObjectName.Name} has a guid different from already found one ({packagename})! {guid}");
                                        }

                                        if (packagename == null)
                                        {
                                            GuidPackageMap[guid] = exp.ObjectName.Name;
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

                            package.Save();
                        }
                    }

                    Debug.WriteLine("Done. Cache size: " + GuidPackageMap.Count);

                    IsBusy = false;
                }
            }
        }



        private void GenerateNewGUIDForPackageFile_Clicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "This process applies immediately and cannot be undone.\nEnsure the file you are going to regenerate is not open in ME3Explorer in any tools.\nBe absolutely sure you know what you're doing before you use this!");
            OpenFileDialog d = new OpenFileDialog
            {
                Title = "Select file to regen guid for",
                Filter = "*.pcc|*.pcc"
            };
            if (d.ShowDialog() == true)
            {
                using (IMEPackage sourceFile = MEPackageHandler.OpenMEPackage(d.FileName))
                {
                    string fname = Path.GetFileNameWithoutExtension(d.FileName);
                    Guid newGuid = Guid.NewGuid();
                    ExportEntry selfNamingExport = null;
                    foreach (ExportEntry exp in sourceFile.Exports)
                    {
                        if (exp.ClassName == "Package"
                            && exp.idxLink == 0
                            && string.Equals(exp.ObjectName.Name, fname, StringComparison.InvariantCultureIgnoreCase))
                        {
                            selfNamingExport = exp;
                            break;
                        }
                    }

                    if (selfNamingExport == null)
                    {
                        MessageBox.Show(
                            "Selected package does not contain a self-naming package export.\nCannot regenerate package file-level GUID if it doesn't contain self-named export.");
                        return;
                    }

                    selfNamingExport.PackageGUID = newGuid;
                    sourceFile.PackageGuid = newGuid;
                    sourceFile.Save();
                }

                MessageBox.Show("Generated a new GUID for package.");
            }
        }

        private void MakeAllGrenadesAmmoRespawn_Click(object sender, RoutedEventArgs e)
        {
            var ammoGrenades = Pcc.Exports.Where(x =>
                x.ClassName != "Class" && !x.IsDefaultObject && (x.ObjectName == "SFXAmmoContainer" ||
                                                                 x.ObjectName == "SFXGrenadeContainer" ||
                                                                 x.ObjectName == "SFXAmmoContainer_Simulator"));
            foreach (var container in ammoGrenades)
            {
                BoolProperty respawns = new BoolProperty(true, "bRespawns");
                float respawnTimeVal = 20;
                if (container.ObjectName == "SFXGrenadeContainer")
                {
                    respawnTimeVal = 8;
                }

                if (container.ObjectName == "SFXAmmoContainer")
                {
                    respawnTimeVal = 3;
                }

                if (container.ObjectName == "SFXAmmoContainer_Simulator")
                {
                    respawnTimeVal = 5;
                }

                FloatProperty respawnTime = new FloatProperty(respawnTimeVal, "RespawnTime");
                var currentprops = container.GetProperties();
                currentprops.AddOrReplaceProp(respawns);
                currentprops.AddOrReplaceProp(respawnTime);
                container.WriteProperties(currentprops);
            }

        }

        private void BuildME1NativeFunctionsInfo_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.BuildME1NativeFunctionsInfo();
        }

        private void PrintNatives(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.PrintAllNativeFuncsToDebug(Pcc);
        }

        private void FindAllFilesWithSpecificClass(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindAllFilesWithClass(this);
        }

        private void FindME12DATables_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindME1ME22DATables();
        }

        private void FindAllME3PowerCustomAction_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindAllME3PowerCustomActions();
        }

        //For Tajfun
        private void FindAllME2PowerCustomAction_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.FindAllME2Powers();
        }

        private void CreatePCCDumpME1_Click(object sender, RoutedEventArgs e)
        {
            new PackageDumper.PackageDumper(this).Show();
        }

        private void AssociatePCCSFM_Clicked(object sender, RoutedEventArgs e)
        {
            Main_Window.Utilities.FileAssociations.EnsureAssociationsSet("pcc", "Mass Effect 2/3 Package File");
            Main_Window.Utilities.FileAssociations.EnsureAssociationsSet("sfm", "Mass Effect 1 Package File");
        }

        private void AssociateUPKUDK_Clicked(object sender, RoutedEventArgs e)
        {
            Main_Window.Utilities.FileAssociations.EnsureAssociationsSet("upk", "Unreal Package File");
            Main_Window.Utilities.FileAssociations.EnsureAssociationsSet("udk", "UDK Package File");
        }

        private void BuildME1ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME1UnrealObjectInfo.generateInfo(Path.Combine(App.ExecFolder, "ME1ObjectInfo.json"));
            this.RestoreAndBringToFront();
            MessageBox.Show(this, "Done");
        }

        private void BuildME2ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME2UnrealObjectInfo.generateInfo(Path.Combine(App.ExecFolder, "ME2ObjectInfo.json"));
            this.RestoreAndBringToFront();
            MessageBox.Show(this, "Done");
        }

        private void BuildME3ObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME3UnrealObjectInfo.generateInfo(Path.Combine(App.ExecFolder, "ME3ObjectInfo.json"));
            this.RestoreAndBringToFront();
            MessageBox.Show(this, "Done");
        }

        private void BuildAllObjectInfo_Clicked(object sender, RoutedEventArgs e)
        {
            ME1UnrealObjectInfo.generateInfo(Path.Combine(App.ExecFolder, "ME1ObjectInfo.json"));
            ME2UnrealObjectInfo.generateInfo(Path.Combine(App.ExecFolder, "ME2ObjectInfo.json"));
            ME3UnrealObjectInfo.generateInfo(Path.Combine(App.ExecFolder, "ME3ObjectInfo.json"));
            this.RestoreAndBringToFront();
            MessageBox.Show(this, "Done");
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
                if (ME2UnrealObjectInfo.Classes.TryGetValue(searchTerm, out ClassInfo _))
                {
                    searchResult += "Key found in ME2 Classes\n";
                }

                if (ME2UnrealObjectInfo.Structs.TryGetValue(searchTerm, out ClassInfo _))
                {
                    searchResult += "Key found in ME2 Structs\n";
                }

                if (ME2UnrealObjectInfo.Enums.TryGetValue(searchTerm, out _))
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
                    searchResult = "Key " + searchTerm +
                                   " not found in any ObjectInfo Structs/Classes/Enums dictionaries";
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

        private void PropertyParsing_UnknownArrayAsObj_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.PropertyParsingUnknownArrayAsObject =
                !Properties.Settings.Default.PropertyParsingUnknownArrayAsObject;
            Properties.Settings.Default.Save();
        }

        private void MountEditor_Click(object sender, RoutedEventArgs e)
        {
            new MountEditor.MountEditorWPF().Show();
        }

        private void EmbeddedTextureViewer_AutoLoad_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.EmbeddedTextureViewer_AutoLoad =
                !Properties.Settings.Default.EmbeddedTextureViewer_AutoLoad;
            Properties.Settings.Default.Save();
        }

        private void InterpreterWPF_AdvancedMode_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.InterpreterWPF_AdvancedDisplay =
                !Properties.Settings.Default.InterpreterWPF_AdvancedDisplay;
            Properties.Settings.Default.Save();
        }

        private void ListNetIndexes_Click(object sender, RoutedEventArgs e)
        {
            var strs = new List<string>();
            foreach (ExportEntry exp in Pcc.Exports)
            {
                if (exp.ParentName == "PersistentLevel")
                {
                    strs.Add($"{exp.NetIndex} {exp.InstancedFullPath}");
                }
            }

            var d = new ListDialog(strs, "NetIndexes", "Here are the netindexes in this file", this);
            d.Show();
        }


        private void InterpreterWPF_Colorize_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.InterpreterWPF_Colorize = !Properties.Settings.Default.InterpreterWPF_Colorize;
            Properties.Settings.Default.Save();
        }

        private void InterpreterWPF_ArrayPropertySizeLimit_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.InterpreterWPF_LimitArrayPropertySize =
                !Properties.Settings.Default.InterpreterWPF_LimitArrayPropertySize;
            Properties.Settings.Default.Save();
        }

        private void GenerateObjectInfoDiff_Click(object sender, RoutedEventArgs e)
        {
            var enumsDiff = new Dictionary<string, (List<NameReference>, List<NameReference>)>();
            var structsDiff = new Dictionary<string, (ClassInfo, ClassInfo)>();
            var classesDiff = new Dictionary<string, (ClassInfo, ClassInfo)>();

            var immutableME1Structs = ME1UnrealObjectInfo.Structs
                .Where(kvp => ME1UnrealObjectInfo.IsImmutableStruct(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var immutableME2Structs = ME2UnrealObjectInfo.Structs
                .Where(kvp => ME2UnrealObjectInfo.IsImmutableStruct(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var immutableME3Structs = ME2UnrealObjectInfo.Structs
                .Where(kvp => ME3UnrealObjectInfo.IsImmutableStruct(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach ((string className, ClassInfo classInfo) in immutableME1Structs)
            {
                if (immutableME2Structs.TryGetValue(className, out ClassInfo classInfo2) &&
                    (!classInfo.properties.SequenceEqual(classInfo2.properties) ||
                     classInfo.baseClass != classInfo2.baseClass))
                {
                    structsDiff.Add(className, (classInfo, classInfo2));
                }

                if (immutableME3Structs.TryGetValue(className, out ClassInfo classInfo3) &&
                    (!classInfo.properties.SequenceEqual(classInfo3.properties) ||
                     classInfo.baseClass != classInfo3.baseClass))
                {
                    structsDiff.Add(className, (classInfo, classInfo3));
                }
            }

            foreach ((string className, ClassInfo classInfo) in immutableME2Structs)
            {
                if (immutableME3Structs.TryGetValue(className, out ClassInfo classInfo3) &&
                    (!classInfo.properties.SequenceEqual(classInfo3.properties) ||
                     classInfo.baseClass != classInfo3.baseClass))
                {
                    structsDiff.Add(className, (classInfo, classInfo3));
                }
            }

            File.WriteAllText(Path.Combine(App.ExecFolder, "Diff.json"),
                JsonConvert.SerializeObject((immutableME1Structs, immutableME2Structs, immutableME3Structs),
                    Formatting.Indented));
            return;

            var srcEnums = ME2UnrealObjectInfo.Enums;
            var compareEnums = ME3UnrealObjectInfo.Enums;
            var srcStructs = ME2UnrealObjectInfo.Structs;
            var compareStructs = ME3UnrealObjectInfo.Structs;
            var srcClasses = ME2UnrealObjectInfo.Classes;
            var compareClasses = ME3UnrealObjectInfo.Classes;

            foreach ((string enumName, List<NameReference> values) in srcEnums)
            {
                if (!compareEnums.TryGetValue(enumName, out var values2) || !values.SubsetOf(values2))
                {
                    enumsDiff.Add(enumName, (values, values2));
                }
            }

            foreach ((string className, ClassInfo classInfo) in srcStructs)
            {
                if (!compareStructs.TryGetValue(className, out var classInfo2) ||
                    !classInfo.properties.SubsetOf(classInfo2.properties) ||
                    classInfo.baseClass != classInfo2.baseClass)
                {
                    structsDiff.Add(className, (classInfo, classInfo2));
                }
            }

            foreach ((string className, ClassInfo classInfo) in srcClasses)
            {
                if (!compareClasses.TryGetValue(className, out var classInfo2) ||
                    !classInfo.properties.SubsetOf(classInfo2.properties) ||
                    classInfo.baseClass != classInfo2.baseClass)
                {
                    classesDiff.Add(className, (classInfo, classInfo2));
                }
            }

            File.WriteAllText(Path.Combine(App.ExecFolder, "Diff.json"),
                JsonConvert.SerializeObject(new { enumsDiff, structsDiff, classesDiff }, Formatting.Indented));
        }

        private void CreateDynamicLighting(object sender, RoutedEventArgs e)
        {
            if (Pcc == null) return;
            PackageEditorExperimentsS.CreateDynamicLighting(Pcc);
        }

        private void RandomizeTerrain_Click(object sender, RoutedEventArgs e)
        {
            if (Pcc == null) return;
            PackageEditorExperimentsM.RandomizeTerrain(Pcc);
        }

        private void ConvertAllDialogueToSkippable_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.ConvertAllDialogueToSkippable(this);
        }

        private void ConvertToDifferentGameFormat_Click(object sender, RoutedEventArgs e)
        {
            if (Pcc is MEPackage pcc)
            {
                var gameString = InputComboBoxWPF.GetValue(this, "Which game's format do you want to convert to?",
                    "Game file converter",
                    new[] { "ME1", "ME2", "ME3" }, "ME2");
                if (Enum.TryParse(gameString, out MEGame game))
                {
                    IsBusy = true;
                    BusyText = "Converting...";
                    Task.Run(() => { pcc.ConvertTo(game); }).ContinueWithOnUIThread(prevTask =>
                    {
                        IsBusy = false;
                        SaveFileAs();
                        Close();
                    });
                }
            }
            else
            {
                MessageBox.Show(this, "Can only convert Mass Effect files!");
            }
        }

        private void ShowExportIcons_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.PackageEditorWPF_ShowExportIcons =
                !Properties.Settings.Default.PackageEditorWPF_ShowExportIcons;
            Properties.Settings.Default.Save();

            // this triggers binding updates
            LeftSide_TreeView.DataContext = null;
            LeftSide_TreeView.DataContext = this;
        }

        private void DumpAllShaders()
        {
            if (Pcc == null) return;
            PackageEditorExperimentsS.DumpAllShaders(Pcc);
        }

        private void DumpMaterialShaders()
        {
            if (TryGetSelectedExport(out ExportEntry matExport) && matExport.IsA("MaterialInterface"))
            {
                PackageEditorExperimentsS.DumpMaterialShaders(matExport);

            }
        }

        void OpenMapInGame()
        {
            const string tempMapName = "__ME3EXPDEBUGLOAD";

            if (Pcc.Exports.All(exp => exp.ClassName != "Level"))
            {
                MessageBox.Show(this, "This file is not a map file!");
            }

            //only works for ME3?
            string mapName = Path.GetFileNameWithoutExtension(Pcc.FilePath);

            string tempDir = MEDirectories.GetCookedPath(Pcc.Game);
            tempDir = Pcc.Game == MEGame.ME1 ? Path.Combine(tempDir, "Maps") : tempDir;
            string tempFilePath = Path.Combine(tempDir, $"{tempMapName}.{(Pcc.Game == MEGame.ME1 ? "SFM" : "pcc")}");

            Pcc.Save(tempFilePath);

            using (var tempPcc = MEPackageHandler.OpenMEPackage(tempFilePath, forceLoadFromDisk: true))
            {
                //insert PlayerStart if neccesary
                if (!(tempPcc.Exports.FirstOrDefault(exp => exp.ClassName == "PlayerStart") is ExportEntry playerStart))
                {
                    var levelExport = tempPcc.Exports.First(exp => exp.ClassName == "Level");
                    Level level = ObjectBinary.From<Level>(levelExport);
                    float x = 0, y = 0, z = 0;
                    if (tempPcc.TryGetUExport(level.NavListStart, out ExportEntry firstNavPoint))
                    {
                        if (firstNavPoint.GetProperty<StructProperty>("Location") is StructProperty locProp)
                        {
                            (x, y, z) = CommonStructs.GetVector3(locProp);
                        }
                        else if (firstNavPoint.GetProperty<StructProperty>("location") is StructProperty locProp2)
                        {
                            (x, y, z) = CommonStructs.GetVector3(locProp2);
                        }
                    }

                    playerStart = new ExportEntry(tempPcc, properties: new PropertyCollection
                    {
                        CommonStructs.Vector3Prop(x, y, z, "location")
                    })
                    {
                        Parent = levelExport,
                        ObjectName = "PlayerStart",
                        Class = tempPcc.getEntryOrAddImport("Engine.PlayerStart")
                    };
                    tempPcc.AddExport(playerStart);
                    level.Actors.Add(playerStart.UIndex);
                    levelExport.WriteBinary(level);
                }

                tempPcc.Save();
            }


            Process.Start(MEDirectories.GetExecutablePath(Pcc.Game), $"{tempMapName} -nostartupmovies");
        }

        private void ReSerializeExport_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetSelectedExport(out ExportEntry export))
            {
                PackageEditorExperimentsS.ReserializeExport(export);
            }
        }

        private void RunPropertyCollectionTest(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.RunPropertyCollectionTest(this);
        }

        private void UDKifyTest(object sender, RoutedEventArgs e)
        {
            if (Pcc != null)
            {
                PackageEditorExperimentsS.UDKifyTest(this);
            }
        }

        private bool HasShaderCache() => PackageIsLoaded() && Pcc.Exports.Any(exp => exp.ClassName == "ShaderCache");

        private void CompactShaderCache()
        {
            IsBusy = true;
            BusyText = "Compacting local ShaderCaches";
            Task.Run(() => ShaderCacheManipulator.CompactShaderCaches(Pcc))
                .ContinueWithOnUIThread(_ => IsBusy = false);
        }

        private void MakeME1TextureFileList(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.MakeME1TextureFileList(this);
        }

        private void CondenseAllArchetypes(object sender, RoutedEventArgs e)
        {
            if (PackageIsLoaded() && Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is ExportEntry level)
            {
                IsBusy = true;
                BusyText = "Condensing Archetypes";
                Task.Run(() =>
                {
                    foreach (ExportEntry export in level.GetAllDescendants().OfType<ExportEntry>())
                    {
                        export.CondenseArchetypes(false);
                    }

                    IsBusy = false;
                });
            }
        }

        private void ValidateNavpointChain_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.ValidateNavpointChain(Pcc);
        }


        private void InterpreterWPF_LinearColorWheel_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.InterpreterWPF_ShowLinearColorWheel =
                !Properties.Settings.Default.InterpreterWPF_ShowLinearColorWheel;
            Properties.Settings.Default.Save();
        }

        private void ShowExportMetadataInTree_Clicked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.PackageEditorWPF_ShowSubText =
                !Properties.Settings.Default.PackageEditorWPF_ShowSubText;
            Properties.Settings.Default.Save();
            foreach (TreeViewEntry tv in AllTreeViewNodesX[0].FlattenTree())
            {
                tv.RefreshSubText();
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton.Equals(MouseButton.XButton1))
                NavigateToPreviousEntry();
            if (e.ChangedButton.Equals(MouseButton.XButton2))
                NavigateToNextEntry();
        }

        private Stack<int> BackwardsIndexes;
        private Stack<int> ForwardsIndexes;

        private void NavigateToNextEntry()
        {
            if (ForwardsIndexes != null && ForwardsIndexes.Any())
            {
                if (SelectedItem != null && SelectedItem.UIndex != 0 && ForwardsIndexes.Peek() != SelectedItem.UIndex)
                {
                    //Debug.WriteLine("Push onto backwards: " + SelectedItem.UIndex);
                    BackwardsIndexes.Push(SelectedItem.UIndex);
                }

                var index = ForwardsIndexes.Pop();
                Debug.WriteLine("Navigate to " + index);
                IsBackForwardsNavigationEvent = true;
                GoToNumber(index);
                IsBackForwardsNavigationEvent = true;
            }
        }

        public bool IsBackForwardsNavigationEvent = false;

        private void NavigateToPreviousEntry()
        {
            if (BackwardsIndexes != null && BackwardsIndexes.Any())
            {
                if (SelectedItem != null && SelectedItem.UIndex != 0 && BackwardsIndexes.Peek() != SelectedItem.UIndex)
                {
                    //Debug.WriteLine("Push onto forwards: " + SelectedItem.UIndex);
                    ForwardsIndexes.Push(SelectedItem.UIndex);
                }

                var index = BackwardsIndexes.Pop();
                Debug.WriteLine("Navigate to " + index);
                IsBackForwardsNavigationEvent = true;
                GoToNumber(index);
                IsBackForwardsNavigationEvent = false;
            }
        }

        private void CreateTestPatchDelta_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.BuildTestPatchComparison();
        }

        private void TintAllNormalizedAverageColor_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.TintAllNormalizedAverageColors(Pcc);
        }

        private void DumpAllExecFunctionSignatures_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.DumpAllExecFunctionsFromGame();
        }

        private void RebuildLevelNetindexing_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.RebuildFullLevelNetindexes();
        }

        private void ReplaceReferenceLinks()
        {
            if (TryGetSelectedEntry(out IEntry selectedEntry))
            {
                var replacement = EntrySelector.GetEntry<IEntry>(this, Pcc, "Select replacement reference");
                if (replacement == null || replacement.UIndex == 0)
                    return;

                BusyText = "Replacing references...";
                IsBusy = true;

                Task.Run(() => selectedEntry.ReplaceAllReferencesToThisOne(replacement)).ContinueWithOnUIThread(
                    prevTask =>
                    {
                        IsBusy = false;
                        MessageBox.Show($"Replaced {prevTask.Result} reference links.");
                    });

            }
        }

        private void RecompileAll_OnClick(object sender, RoutedEventArgs e)
        {
            if (PackageIsLoaded() && Pcc.Game == MEGame.ME3)
            {
                var exportsWithDecompilationErrors = new List<EntryStringPair>();
                foreach (ExportEntry export in Pcc.Exports.Where(exp => exp.IsClass))
                {
                    (_, string script) = ME3ScriptCompiler.DecompileExport(export);
                    (ASTNode ast, MessageLog log) = ME3ScriptCompiler.CompileAST(script, export.ClassName);
                    if (ast == null)
                    {
                        exportsWithDecompilationErrors.Add(new EntryStringPair(export, "Compilation Error!"));
                        break;
                    }
                }

                var dlg = new ListDialog(exportsWithDecompilationErrors, $"Compilation errors", "", this)
                {
                    DoubleClickEntryHandler = entryDoubleClick
                };
                dlg.Show();
            }
        }

        private void SetAllWwiseEventDurations_Click(object sender, RoutedEventArgs e)
        {
            BusyText = "Scanning audio and updating events";
            IsBusy = true;
            Task.Run(() => PackageEditorExperimentsM.SetAllWwiseEventDurations(Pcc)).ContinueWithOnUIThread(prevTask =>
            {
                IsBusy = false;
                MessageBox.Show("Wwiseevents updated.");
            });
        }

        public void AutoEnumerateClassNetIndex(object sender, RoutedEventArgs e)
        {
            int baseindex = 0;
            if (SelectedItem.Entry is ExportEntry classexp && classexp.IsClass)
            {
                baseindex = classexp.NetIndex;
                var classbin = classexp.GetBinaryData<UClass>();
                ExportEntry defaultxp = Pcc.GetUExport(classbin.Defaults);
                defaultxp.NetIndex = baseindex + 1;
                EnumerateChildNetIndexes(classbin.Children);
            }

            void EnumerateChildNetIndexes(int child)
            {
                if (child > 0 && child <= Pcc.ExportCount)
                {
                    var childexp = Pcc.GetUExport(child);
                    baseindex--;
                    childexp.NetIndex = baseindex;
                    var childbin = ObjectBinary.From(childexp);
                    if (childbin is UFunction funcbin)
                    {
                        EnumerateChildNetIndexes(funcbin.Children);
                        EnumerateChildNetIndexes(funcbin.Next);
                    }
                    else if (childbin is UProperty propbin)
                    {

                        EnumerateChildNetIndexes(propbin.Next);
                    }
                }

                return;
            }
        }

        private void TransferLevelBetweenGames(object sender, RoutedEventArgs e)
        {
            if (Pcc is MEPackage pcc && Path.GetFileNameWithoutExtension(pcc.FilePath).StartsWith("BioP") &&
                pcc.Game == MEGame.ME2)
            {
                var cdlg = MessageBox.Show(
                    "This is a highly experimental method to copy the static art and collision from an ME2 level to an ME3 one.  It will not copy materials or design elements.",
                    "Warning", MessageBoxButton.OKCancel);
                if (cdlg == MessageBoxResult.Cancel)
                    return;

                CommonOpenFileDialog o = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    Title = "Select output folder"
                };
                if (o.ShowDialog(this) == CommonFileDialogResult.Ok)
                {
                    string tfc = PromptDialog.Prompt(this,
                        "Enter Name of Target Textures File Cache (tfc) without extension", "Level Conversion Tool",
                        "Textures_DLC_MOD_", false, PromptDialog.InputType.Text);

                    if (tfc == null || tfc == "Textures_DLC_MOD_")
                        return;

                    BusyText = "Parsing level files";
                    IsBusy = true;
                    Task.Run(() =>
                        PackageEditorExperimentsK.ConvertLevelToGame(MEGame.ME3, pcc, o.FileName, tfc,
                            newText => BusyText = newText)).ContinueWithOnUIThread(prevTask =>
                    {
                        if (Pcc != null)
                            LoadFile(Pcc.FilePath);
                        IsBusy = false;
                        var dlg = new ListDialog(prevTask.Result, $"Conversion errors: ({prevTask?.Result.Count})", "",
                            this)
                        {
                            DoubleClickEntryHandler = entryDoubleClick
                        };
                        dlg.Show();
                    });

                }

            }
            else
            {
                MessageBox.Show(this,
                    "Load a level's BioP file to start the transfer.\nCurrently can only convert from ME2 to ME3.");
            }
        }

        private void RestartTransferFromJSON(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.RestartTransferFromJSON(this, entryDoubleClick);
        }

        private void RecookLevelToTestFromJSON(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsK.RecookLevelToTestFromJSON(this, entryDoubleClick);
        }

        private void ConvertFileToME3(object sender, RoutedEventArgs e)
        {
            BusyText = "Converting files";
            IsBusy = true;
            string tfc = PromptDialog.Prompt(this, "Enter Name of Target Textures File Cache (tfc) without extension",
                "Level Conversion Tool", "Textures_DLC_MOD_", false, PromptDialog.InputType.Text);

            if (Pcc == null || tfc == null || tfc == "Textures_DLC_MOD_")
                return;
            tfc = Path.Combine(Path.GetDirectoryName(Pcc.FilePath), $"{tfc}.tfc");

            if (Pcc is MEPackage tgt && Pcc.Game != MEGame.ME3)
            {
                Task.Run(() => tgt.ConvertTo(MEGame.ME3, tfc, true)).ContinueWithOnUIThread(prevTask =>
                {
                    IsBusy = false;
                });
            }
        }

        private void ShiftME1AnimCutScene(object sender, RoutedEventArgs e)
        {
            var selected = GetSelected(out var uindex);
            if (selected && uindex > 0)
            {
                PackageEditorExperimentsM.ShiftME1AnimCutscene(Pcc.GetUExport(uindex));
            }
        }

        private void PortWiiUBSP(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.PortWiiUBSP();
        }

        public void LoadFileFromStream(Stream packageStream, string associatedFilePath, int goToIndex = 0)
        {
            try
            {
                preloadPackage(Path.GetFileName(associatedFilePath), packageStream.Length);
                LoadMEPackage(packageStream, associatedFilePath);
                postloadPackage(Path.GetFileName(associatedFilePath), associatedFilePath, goToIndex);
            }
            catch (Exception e) when (!App.IsDebug)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(associatedFilePath);
                MessageBox.Show($"Error loading {Path.GetFileName(associatedFilePath)}:\n{e.Message}");
                IsBusy = false;
                IsBusyTaskbar = false;
                //throw e;
            }
        }

        private void ScanStuff_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsS.ScanStuff(this);
        }

        private void ExportLevelToT3D_Click(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsO.DumpPackageToT3D(Pcc);
        }

        public void PropogateRecentsChange(IEnumerable<string> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "PackageEditor";

        private void PortME1EntryMenu_Clicked(object sender, RoutedEventArgs e)
        {
            PackageEditorExperimentsM.PortME1EntryMenuToME3ViaBioPChar(Pcc);
        }
    }
}