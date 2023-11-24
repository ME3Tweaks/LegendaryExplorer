using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GongSolutions.Wpf.DragDrop;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.DialogueEditor;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.Meshplorer;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Sound.ISACT;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using LegendaryExplorerCore.Audio;
using System.IO.Packaging;
using LegendaryExplorer.Packages;
using LegendaryExplorerCore.Localization;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorer.Tools.PackageEditor
{
    /// <summary>
    /// Interaction logic for PackageEditorWPF.xaml
    /// </summary>
    public partial class PackageEditorWindow : WPFBase, IDropTarget, IBusyUIHost, IRecents
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
            "World", "Package", "StaticMesh", "SkeletalMesh", "Sequence", "Material", "Function", "Class", "State",
            "TextureCube", "Bio2DA", "Bio2DANumberedRows"
        };

        //Objects in this collection are displayed on the left list view (names, imports, exports)

        readonly Dictionary<ExportLoaderControl, TabItem> ExportLoaders = new();

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

        public ObservableCollectionExtended<object> LeftSideList_ItemsSource { get; } = new();

        //referenced by EntryMetaDataExportLoader's xaml, do not make private
        public ObservableCollectionExtended<IndexedName> NamesList { get; } = new();

        public ObservableCollectionExtended<string> ClassDropdownList { get; } = new();

        public ObservableCollectionExtended<TreeViewEntry> AllTreeViewNodesX { get; } = new();

        private TreeViewEntry _selectedItem;
        public TreeViewEntry SelectedItem
        {
            get => _selectedItem;
            set
            {
                var oldIndex = _selectedItem?.UIndex;
                // Some weird oddity exists in TreeView WPF where it selects the node twice when expanding stuff
                // and it makes first selection sometimes reset to nothing.
                // This is hack to make it not do that.

                // only allow selecting a null tree entry if there is no package loaded
                bool allowSelection = Pcc != null && value != null;
                if (!allowSelection && Pcc == null) allowSelection = true;

                if (allowSelection && SetProperty(ref _selectedItem, value) && !SuppressSelectionEvent)
                {
                    //_lastSelectionEvent = now;
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


        private int QueuedGotoNumber;
        private bool IsLoadingFile;


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

        private bool _showExperiments = App.IsDebug || Settings.PackageEditor_ShowExperiments;
        public bool ShowExperiments
        {
            get => _showExperiments;
            set
            {
                SetProperty(ref _showExperiments, value);
                Settings.PackageEditor_ShowExperiments = value;
            }
        }

        #region Commands
        public ICommand NavigateBackCommand { get; set; }
        public ICommand NavigateForwardCommand { get; set; }
        public ICommand ForceReloadPackageCommand { get; set; }
        public ICommand ComparePackagesCommand { get; set; }
        public ICommand OpenOtherVersionCommand { get; set; }
        public ICommand OpenHighestMountedCommand { get; set; }
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
        public ICommand ResolveImportsTreeViewCommand { get; set; }
        public ICommand CheckForDuplicateIndexesCommand { get; set; }
        public ICommand CheckForInvalidObjectPropertiesCommand { get; set; }
        public ICommand CheckForBrokenMaterialsCommand { get; set; }
        public ICommand CheckForScriptErrorsCommand { get; set; }
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
        public ICommand LECLEditorCommand { get; set; }
        public ICommand CreateNewPackageGUIDCommand { get; set; }
        public ICommand RestoreExportCommand { get; set; }
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
        public ICommand FindReferencesCommand { get; set; }
        public ICommand OpenExportInCommand { get; set; }
        public ICommand CompactShaderCacheCommand { get; set; }
        public ICommand GoToArchetypeCommand { get; set; }
        public ICommand ReplaceNamesCommand { get; set; }
        public ICommand NavigateToEntryCommand { get; set; }
        public ICommand ResolveImportCommand { get; set; }
        public ICommand ExtractToPackageCommand { get; set; }
        public ICommand PackageExportIsSelectedCommand { get; set; }
        public ICommand ReindexDuplicateIndexesCommand { get; set; }
        public ICommand ReplaceReferenceLinksCommand { get; set; }
        public ICommand CalculateExportMD5Command { get; set; }
        public ICommand CreateClassCommand { get; set; }
        public ICommand CreatePackageExportCommand { get; set; }
        public ICommand CreateObjectReferencerCommand { get; set; }
        public ICommand DeleteEntryCommand { get; set; }


        private void LoadCommands()
        {
            CalculateExportMD5Command = new GenericCommand(CalculateExportMD5, ExportIsSelected);
            CompareToUnmoddedCommand = new GenericCommand(() => SharedPackageTools.ComparePackageToUnmodded(this, entryDoubleClickToTreeview), () => SharedPackageTools.CanCompareToUnmodded(this));
            ComparePackagesCommand = new GenericCommand(() => SharedPackageTools.ComparePackageToAnother(this, entryDoubleClickToTreeview), PackageIsLoaded);
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
            CheckForInvalidObjectPropertiesCommand = new GenericCommand(CheckForBadObjectPropertyReferences, PackageIsLoaded);
            CheckForBrokenMaterialsCommand = new GenericCommand(CheckForBrokenMaterials, IsLoadedPackageME);
            CheckForScriptErrorsCommand = new GenericCommand(CheckForScriptErrors, IsLoadedPackageME);
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
            LECLEditorCommand = new GenericCommand(EditLECLData, CanEditLECLData);
            PackageExportIsSelectedCommand = new EnableCommand(PackageExportIsSelected);
            CreateNewPackageGUIDCommand = new GenericCommand(GenerateNewGUIDForSelected, PackageExportIsSelected);
            SetPackageAsFilenamePackageCommand = new GenericCommand(SetSelectedAsFilenamePackage, PackageExportIsSelected);
            FindEntryViaTagCommand = new GenericCommand(FindEntryViaTag, PackageIsLoaded);
            PopoutCurrentViewCommand = new GenericCommand(PopoutCurrentView, ExportIsSelected);
            CompactShaderCacheCommand = new GenericCommand(CompactShaderCache, HasShaderCache);
            GoToArchetypeCommand = new GenericCommand(GoToArchetype, CanGoToArchetype);
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

            NavigateToEntryCommand = new RelayCommand(NavigateToEntry, CanNavigateToEntry);

            ResolveImportCommand = new GenericCommand(OpenImportDefinition, ImportIsSelected);
            ResolveImportsTreeViewCommand = new GenericCommand(ResolveImportsTreeView, PackageIsLoaded);
            FindAllClassInstancesCommand = new GenericCommand(FindAllInstancesofClass, PackageIsLoaded);
            ExtractToPackageCommand = new GenericCommand(ExtractEntryToNewPackage, ExportIsSelected);

            RestoreExportCommand = new GenericCommand(RestoreExportData, ExportIsSelected);
            OpenOtherVersionCommand = new GenericCommand(OpenOtherVersion, IsLoadedPackageME);
            OpenHighestMountedCommand = new GenericCommand(OpenHighestMountedVersion, IsLoadedPackageME);

            ForceReloadPackageCommand = new GenericCommand(() => ExperimentsMenu.ForceReloadPackageWithoutSharing(), () => ShowExperiments && ExperimentsMenu.CanForceReload());

            NavigateForwardCommand = new GenericCommand(NavigateToNextEntry, () => CurrentView == CurrentViewMode.Tree && ForwardsIndexes != null && ForwardsIndexes.Any());
            NavigateBackCommand = new GenericCommand(NavigateToPreviousEntry, () => CurrentView == CurrentViewMode.Tree && BackwardsIndexes != null && BackwardsIndexes.Any());

            CreateClassCommand = new GenericCommand(CreateClass, IsLoadedPackageME);
            CreatePackageExportCommand = new GenericCommand(CreatePackageExport, IsLoadedPackageME);
            CreateObjectReferencerCommand = new GenericCommand(CreateObjectReferencer, IsLoadedPackageME);
            DeleteEntryCommand = new GenericCommand(DeleteEntry, EntryIsSelected);
        }

        private void CreateObjectReferencer()
        {
            if (Pcc.Flags.HasFlag(UnrealFlags.EPackageFlags.Map))
            {
                MessageBox.Show(@"Map packages do not use ObjectReferencer; to keep objects in memory, add root objects to ExtraReferencedObjects in TheWorld's binary.");
                return;
            }

            var objRef = Pcc.Exports.FirstOrDefault(x => x.ClassName == "ObjectReferencer" && !x.IsDefaultObject);
            if (objRef != null)
            {
                GoToEntry(objRef.InstancedFullPath);
                return;
            }

            // This part ported from Mass Effect 2 Randomizer POackag
            var rop = new RelinkerOptionsPackage() { Cache = new PackageCache() };
            var referencer = new ExportEntry(Pcc, 0, Pcc.GetNextIndexedName("ObjectReferencer"), properties: new PropertyCollection() { new ArrayProperty<ObjectProperty>("ReferencedObjects") })
            {
                Class = EntryImporter.EnsureClassIsInFile(Pcc, "ObjectReferencer", rop)
            };
            Pcc.AddExport(referencer);
            GoToEntry(referencer.InstancedFullPath);
        }

        private void DeleteEntry()
        {

            TrashEntryAndChildren();
        }

        private void CheckForScriptErrors()
        {
            if (Pcc is null)
            {
                return;
            }
            BusyText = "Checking for Script errors...";
            IsBusy = true;
            Task.Run(() =>
            {
                var errors = new List<EntryStringPair>();

                var fileLib = new FileLib(Pcc);
                using var packageCache = new PackageCache();
                if (fileLib.Initialize(packageCache))
                {
                    foreach (ExportEntry export in Pcc.Exports.Where(exp => exp.IsClass))
                    {
                        try
                        {
                            (_, string source) = UnrealScriptCompiler.DecompileExport(export, fileLib, packageCache);
                            var log = new MessageLog();

                            var (ast, _) = UnrealScriptCompiler.CompileOutlineAST(source, "Class", log, Pcc.Game);
                            if (!log.HasErrors)
                            {
                                UnrealScriptCompiler.CompileNewClassAST(Pcc, (Class)ast, log, fileLib, out bool vfTableChanged);
                                if (vfTableChanged)
                                {
                                    log.LogError("Virtual function table needs to be updated!");
                                }
                            }
                            if (log.HasErrors)
                            {
                                errors.Add(new EntryStringPair(export, $"#{export.UIndex,-9}\t{export.InstancedFullPath}:\n{string.Join('\n', log.AllErrors)}"));
                            }
                        }
                        catch (Exception e)
                        {
                            errors.Add(new EntryStringPair(export, $"{export.UIndex,-9}\t{export.InstancedFullPath}: EXCEPTION while checking for errors\n{e.FlattenException()}"));
                        }
                    }
                }
                else
                {
                    errors.Add(new EntryStringPair($"FileLib failed to initialize! Errors: \n{string.Join('\n', fileLib.InitializationLog.AllErrors)}"));
                }
                return errors;
            }).ContinueWithOnUIThread(prevTask =>
            {
                IsBusy = false;
                if (prevTask.Result.IsEmpty())
                {
                    MessageBox.Show(this, "No Script Errors found!");
                }
                else
                {
                    new ListDialog(prevTask.Result, "Script errors", "", this)
                    {
                        DoubleClickEntryHandler = entryDoubleClick
                    }.Show();
                }
            });
        }

        private void OpenOtherVersion()
        {
            var result = CrossGenHelpers.FetchOppositeGenPackage(Pcc, out var otherGen);
            if (result != null)
            {
                MessageBox.Show(result);
            }
            else
            {
                TryGetSelectedEntry(out var entry);
                PackageEditorWindow pe = new PackageEditorWindow();
                pe.LoadPackage(otherGen, goToEntry: entry?.InstancedFullPath);
                pe.Show();
            }
        }

        private void OpenHighestMountedVersion()
        {
            if (MEDirectories.GetBioGamePath(Pcc.Game) is null)
            {
                MessageBox.Show($"No {Pcc.Game} installation detected!");
                return;
            }
            string fileName = Path.GetFileName(Pcc.FilePath);
            if (!MELoadedFiles.TryGetHighestMountedFile(Pcc.Game, fileName, out string filePath))
            {
                MessageBox.Show($"No file named '{fileName}' was found in the {Pcc.Game} installation.");
            }
            else if (Path.GetFullPath(filePath) == Path.GetFullPath(Pcc.FilePath))
            {
                MessageBox.Show($"This is the highest mounted version of {fileName} in your {Pcc.Game} installation.");
            }
            else
            {
                TryGetSelectedEntry(out var entry);
                var pe = new PackageEditorWindow();
                pe.LoadFile(filePath, goToEntry: entry?.InstancedFullPath);
                pe.Show();
            }
        }

        // LECLData is only available on LE game files
        private bool CanEditLECLData() => Pcc != null && Pcc.Game.IsLEGame();

        private void EditLECLData()
        {
            new LECLDataEditorWindow(this, Pcc).ShowDialog();
        }

        private void CreatePackageExport()
        {
            var packName = PromptDialog.Prompt(this, "Enter a package name to create at the root.", "Enter package export name");
            if (string.IsNullOrWhiteSpace(packName))
                return;
            var package = ExportCreator.CreatePackageExport(Pcc, packName);
            GoToNumber(package.UIndex);
        }

        private void CreateClass()
        {
            IEntry parent = null;
            string fileName = Path.GetFileName(Pcc.FilePath);
            if (fileName.CaseInsensitiveEquals("Startup_INT.pcc") || !FileLib.PackagesWithTopLevelClasses(Pcc.Game).Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                //not a base file, so classes must be within a package.

                var existingPackages = new List<ExportEntry>();
                foreach (TreeNode<IEntry, int> root in Pcc.Tree.Roots)
                {
                    if (root.Data is ExportEntry exp && exp.ClassName.CaseInsensitiveEquals("Package"))
                    {
                        existingPackages.Add(exp);
                    }
                }

                if (existingPackages.Count is 0)
                {
                    MessageBox.Show(this, "Classes must be child of a Package export. Add one to the file first.");
                    return;
                }

                IEntry defaultParent = null;
                if (TryGetSelectedExport(out var currentExport) && (currentExport.Parent is null && currentExport.ClassName == "Package" || currentExport.Parent is { ClassName: "Package" }))
                {
                    // This will match both cases given the if statement.
                    defaultParent = currentExport.Parent ?? currentExport;
                }
                else
                {
                    defaultParent = Pcc.Exports.FirstOrDefault(exp => exp.IsClass)?.Parent;
                }

                parent = EntrySelector.GetEntry<ExportEntry>(this, Pcc, "Pick a Package export your class should be a child of.",
                    exp => existingPackages.Contains(exp), defaultParent);
                if (parent is null)
                {
                    return;
                }
            }
            var className = PromptDialog.Prompt(this, "Enter the name of your class:", "Class Name", "MyClass", true);
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }
            string fullPath = parent is null ? className : $"{parent.InstancedFullPath}.{className}";
            if (Pcc.FindEntry(fullPath) is not null)
            {
                MessageBox.Show(this, $"'{fullPath}' already exists in this file!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var fileLib = new FileLib(Pcc);
            if (!fileLib.Initialize())
            {
                var dlg = new ListDialog(fileLib.InitializationLog.AllErrors.Select(msg => msg.ToString()), "Script Error", "Could not build script database for this file!", this);
                dlg.Show();
                return;
            }
            (_, MessageLog log) = UnrealScriptCompiler.CompileClass(Pcc, $"class {className};", fileLib, parent: parent);
            if (log.HasErrors)
            {
                var dlg = new ListDialog(log.AllErrors.Select(msg => msg.ToString()), "Script Error", "Could not create class!", this);
                dlg.Show();
                return;
            }
            CurrentView = CurrentViewMode.Tree;
            GoToNumber(Pcc.FindEntry(fullPath)?.UIndex ?? 0);
        }

        private void CalculateExportMD5()
        {
            if (TryGetSelectedExport(out var ee))
            {
                var hash = MD5.Create().ComputeHash(ee.Data);
                StringBuilder result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2"));
                Clipboard.SetText(result.ToString());
            }
        }

        private void ResolveImportsTreeView()
        {
            if (Enumerable.Any(AllTreeViewNodesX))
            {
                Task.Run(() =>
                {
                    var unresolvableImports = new List<EntryStringPair>();
                    BusyText = "Resolving imports";
                    IsBusy = true;

                    var treeNodes = AllTreeViewNodesX[0].FlattenTree().Where(x => x.Entry is ImportEntry);

                    var cache = new PackageCache();
                    foreach (var impTV in treeNodes)
                    {
                        if (impTV.Entry.IsAKnownNativeClass())
                        {
                            impTV.SubText = $"{impTV.Entry.InstancedFullPath.Substring(0, impTV.Entry.InstancedFullPath.IndexOf('.'))}.{(impTV.Game == MEGame.ME1 ? "u" : "pcc")} (Native)";
                        }
                        else
                        {
                            var resolvedExp = EntryImporter.ResolveImport(impTV.Entry as ImportEntry, null, cache);
                            if (resolvedExp == null)
                            {
                                unresolvableImports.Add(new EntryStringPair(impTV.Entry, $"Unresolvable import: {impTV.Entry.InstancedFullPath}"));
                            }
                            else if (resolvedExp.FileRef.FilePath != null)
                            {
                                var fname = Path.GetFileName(resolvedExp.FileRef.FilePath);
                                impTV.SubText = fname;
                            }
                        }

                    }


                    return unresolvableImports;
                }).ContinueWithOnUIThread(unresolvableImports =>
                {
                    IsBusy = false;
                    if (unresolvableImports.Exception == null)
                    {
                        ListDialog ld = new ListDialog(unresolvableImports.Result, "Found unresolved imports", "The following imports failed to resolve. This may be due to improperly named files (an issue in LEX, not in the game), or they may be incorrectly named.", this) { DoubleClickEntryHandler = GetEntryDoubleClickAction() };
                        ld.Show();
                    }
                });
            }
        }

        private void RestoreExportData()
        {
            if (!Pcc.Game.IsLEGame() && !Pcc.Game.IsOTGame())
            {
                MessageBox.Show(this, "Not a supported file for restoring export data. Only LE/OT files are supported.");
                return;
            }

            Task.Run(() =>
            {
                BusyText = "Finding unmodded candidates...";
                IsBusy = true;
                return SharedPackageTools.GetUnmoddedCandidatesForPackage(this);
            }).ContinueWithOnUIThread(foundCandidates =>
            {
                IsBusy = false;
                if (!foundCandidates.Result.Any())
                {
                    MessageBox.Show(this, "Cannot find any candidates for this file!");
                    return;
                }

                var choices = foundCandidates.Result.DiskFiles.ToList(); //make new list
                choices.AddRange(foundCandidates.Result.SFARPackageStreams.Select(x => x.Key));

                var choice = InputComboBoxDialog.GetValue(this, "Choose file to compare to:", "Unmodified file comparison", choices, choices.Last());
                if (string.IsNullOrEmpty(choice))
                {
                    return;
                }

                var restorePackage = MEPackageHandler.OpenMEPackage(choice, forceLoadFromDisk: true);
                if (TryGetSelectedExport(out var exportToOvewrite))
                {
                    var sourceExport = restorePackage.GetUExport(exportToOvewrite.UIndex);
                    exportToOvewrite.Data = sourceExport.Data;
                }
            });
        }

        private void ExtractEntryToNewPackage()
        {
            if (SelectedItem.Entry is ExportEntry exp)
            {
                SharedPackageTools.ExtractEntryToNewPackage(exp, x => IsBusy = x, x => BusyText = x, GetEntryDoubleClickAction(), this);
            }
        }

        private void FindAllInstancesofClass()
        {
            var classes = Pcc.Exports.Select(x => x.ClassName).NonNull().Distinct().ToList().OrderBy(p => p).ToList();
            var chosenClass = InputComboBoxDialog.GetValue(this, "Select a class to list all instances of.", "Class selector", classes, classes.FirstOrDefault());
            if (!string.IsNullOrWhiteSpace(chosenClass))
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
                        var pwpf = new PackageEditorWindow();
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
                        if (Timeline.CanParseStatic(exp))
                        {
                            var p = new InterpEditor.InterpEditorWindow();
                            p.Show();
                            p.LoadFile(Pcc.FilePath);
                            if (exp.ObjectName == "InterpData")
                            {
                                p.SelectedInterpData = exp;
                            }
                        }
                        break;
                    case "Soundplorer":
                        if (Soundpanel.CanParseStatic(exp))
                        {
                            new Soundplorer.SoundplorerWPF(exp).Show();
                        }
                        break;
                    case "FaceFXEditor":
                        if (exp.ClassName == "FaceFXAnimSet")
                        {
                            new FaceFXEditor.FaceFXEditorWindow(exp).Show();
                        }
                        break;
                    case "DialogueEditor":
                        if (exp.ClassName == "BioConversation")
                        {
                            new DialogueEditorWindow(exp).Show();
                        }
                        break;
                    case "PathfindingEditor":
                        if (PathfindingEditor.PathfindingEditorWindow.CanParseStatic(exp))
                        {
                            var pf = new PathfindingEditor.PathfindingEditorWindow(exp);
                            pf.Show();

                        }
                        break;
                    case "Meshplorer":
                        if (MeshRenderer.CanParseStatic(exp))
                        {
                            new Meshplorer.MeshplorerWindow(exp).Show();
                        }
                        break;
                    case "WwiseEditor":
                        if (exp.ClassName == "WwiseBank")
                        {
                            var w = new WwiseEditor.WwiseEditorWindow(exp);
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
                        return MeshRenderer.CanParseStatic(exp);
                    case "PathfindingEditor":
                        return PathfindingEditor.PathfindingEditorWindow.CanParseStatic(exp);
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
                var m = new CommonOpenFileDialog
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
                var m = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    Title = "Select folder of GFX/SWF files to import"
                };
                if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
                {
                    var bw = new BackgroundWorker();
                    bw.RunWorkerAsync(m.FileName);
                    bw.RunWorkerCompleted += (x, y) =>
                    {
                        IsBusy = false;
                        var ld = new ListDialog((List<EntryStringPair>)y.Result, "Imported Files", "The following files were imported.", this)
                        {
                            DoubleClickEntryHandler = entryDoubleClick
                        };
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

                                string dataPropName = matchingExport.ClassName == "GFxMovieInfo" ? "RawData" : "Data";
                                var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                                //Write SWF data
                                rawData.Bytes = bytes;

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
                            importedFiles.Add(new EntryStringPair((IEntry)null, "No matching filenames were found."));
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


        internal async void SaveFileAs()
        {
            string fileFilter;
            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    fileFilter = GameFileFilters.ME1SaveFileFilter;
                    break;
                case MEGame.ME2:
                case MEGame.ME3:
                    fileFilter = GameFileFilters.ME3ME2SaveFileFilter;
                    break;
                default:
                    string extension = Path.GetExtension(Pcc.FilePath);
                    fileFilter = $"*{extension}|*{extension}";
                    break;
            }

            var d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                await Pcc.SaveAsync(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private async void SaveFile()
        {
            await Pcc.SaveAsync();
        }

        private void OpenFile()
        {
            var d = AppDirectories.GetOpenPackageDialog();
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
            string gameString = InputComboBoxDialog.GetValue(this, "Choose a game to create a file for:",
                "Create new package file", new[] { "LE3", "LE2", "LE1", "ME3", "ME2", "ME1", "UDK" }, "LE3");
            if (Enum.TryParse(gameString, out MEGame game))
            {
                var dlg = new SaveFileDialog
                {
                    Filter = game switch
                    {
                        MEGame.UDK => GameFileFilters.UDKFileFilter,
                        MEGame.ME1 => GameFileFilters.ME1SaveFileFilter,
                        MEGame.ME2 => GameFileFilters.ME3ME2SaveFileFilter,
                        MEGame.ME3 => GameFileFilters.ME3ME2SaveFileFilter,
                        _ => GameFileFilters.LESaveFileFilter
                    },
                    CustomPlaces = AppDirectories.GameCustomPlaces,
                };
                if (dlg.ShowDialog() == true)
                {
                    MEPackageHandler.CreateAndSavePackage(dlg.FileName, game);
                    LoadFile(dlg.FileName);
                }
            }
        }

        private void NewLevelFile()
        {
            string gameString = InputComboBoxDialog.GetValue(this, "Choose game to create a level file for:",
                                                          "Create new level file", new[] { "LE3", "LE2", "LE1", "ME3", "ME2", "ME1" }, "LE3");
            if (Enum.TryParse(gameString, out MEGame game) && (game.IsLEGame() || game.IsOTGame()))
            {
                var dlg = new SaveFileDialog
                {
                    Filter = GameFileFilters.ME3ME2SaveFileFilter,
                    OverwritePrompt = true
                };
                if (game.IsLEGame())
                    dlg.Filter = GameFileFilters.LESaveFileFilter;
                if (game == MEGame.ME1)
                    dlg.Filter = GameFileFilters.ME1SaveFileFilter;

                if (dlg.ShowDialog() == true)
                {
                    if (File.Exists(dlg.FileName))
                    {
                        File.Delete(dlg.FileName);
                    }

                    MEPackageHandler.CreateEmptyLevel(dlg.FileName, game);

                    LoadFile(dlg.FileName);
                }
            }
        }

        // This is a coupling hack for splitting the experiments class out. Probably can make this an interface though for more wide-usability
        /// <summary>
        /// Returns a method that can be used in other windows to navigate this instance of Package Editor to a specify entry
        /// </summary>
        /// <returns></returns>
        public Action<EntryStringPair> GetEntryDoubleClickAction() => entryDoubleClick;

        private void entryDoubleClick(EntryStringPair clickedItem)
        {
            if (clickedItem?.Entry != null && clickedItem.Entry.UIndex != 0)
            {
                GoToNumber(clickedItem.Entry.UIndex);
            }
        }

        /// <summary>
        /// Same as <see cref="entryDoubleClick"/>, but navigates to the TreeView first if you're on the names tab
        /// Used in the "Find Usages of Name" list dialog
        /// </summary>
        /// <param name="clickedItem"></param>
        private void entryDoubleClickToTreeview(EntryStringPair clickedItem)
        {
            if (CurrentView is CurrentViewMode.Names)
            {
                SearchHintText = "Object name";
                GotoHintText = "UIndex";
                CurrentView = CurrentViewMode.Tree;
            }
            entryDoubleClick(clickedItem);
        }

        private void PopoutCurrentView()
        {
            if (EditorTabs.SelectedItem is TabItem { Content: ExportLoaderControl exportLoader })
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
                string searchTerm = result.Name;
                bool found = Pcc.Names.Any(x => x.CaseInsensitiveEquals(searchTerm));
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

        // I think this should be moved to it's own file. Like a PackageInfoWindow class.
        private void ViewPackageInfo()
        {
            var items = new List<string>();
            try
            {
                byte[] header = Pcc.getHeader();
                var ms = new MemoryStream(header);

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
                    string str = ms.ReadStringLatin1(foldernameStrLen - 1);
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
                UnrealFlags.EPackageFlags flagEnum = (UnrealFlags.EPackageFlags)flags;
                var setFlags = flagEnum.MaskToList();
                foreach (var setFlag in setFlags)
                {
                    flagsStr += " " + setFlag;
                }

                items.Add(flagsStr);

                if (Pcc.Game is MEGame.ME3 or MEGame.LE3 && Pcc.Flags.HasFlag(UnrealFlags.EPackageFlags.Cooked))
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

                if (Pcc.Game.IsLEGame() || (Pcc.Game != MEGame.ME1 || Pcc.Platform != MEPackage.GamePlatform.Xenon))
                {
                    uint dependencyTableOffset = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} Dependency Table Offset: 0x{dependencyTableOffset:X8} (Not used in Mass Effect games)");
                }

                if (Pcc.Game >= MEGame.ME3)
                {
                    uint importExportGuidsOffset = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} ImportExportGuidsOffset: 0x{importExportGuidsOffset:X8} (Not used in Mass Effect games)");

                    uint unknown2 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} ImportGuidsCount: {unknown2} (0x{unknown2:X8}) (Not used in Mass Effect games)");

                    uint unknown3 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} ExportGuidsCount: {unknown3} (0x{unknown3:X8}) (Not used in Mass Effect games)");
                    uint unknown4 = ms.ReadUInt32();
                    items.Add($"0x{ms.Position - 4:X2} ThumbnailTableOffset: {unknown4} (0x{unknown4:X8}) (Not used in Mass Effect games)");
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

                if (Pcc.Game is MEGame.ME2 or MEGame.ME3 || Pcc.Game.IsLEGame() || Pcc.Platform == MEPackage.GamePlatform.PS3)
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
            catch
            {

            }

            new ListDialog(items, Path.GetFileName(Pcc.FilePath) + " package summary",
                "Below is information about this package from the package summary.", this).Show();
        }

        private void TrashEntryAndChildren()
        {
            if (TreeEntryIsSelected())
            {
                var selected = (TreeViewEntry)LeftSide_TreeView.SelectedItem;
                // 06/12/2022 - Change from FullPath.StartsWith() because if somehow trashed object has children (old files, bad experiments, etc) 
                // this prevents removing these items easily
                if (selected.Entry is IEntry ent && ent.ClassName == @"Package" && ent.ObjectName.Name == UnrealPackageFile.TrashPackageName)
                {
                    MessageBox.Show("Cannot trash an already trashed item.");
                    return;
                }

                bool skipReferencesCheck = ShowExperiments &&
                    (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)); // Bypass the check if holding SHIFT

                BusyText = "Performing reference check...";
                IsBusy = true;
                var positionInBranch = selected.Parent.Sublinks.IndexOf(selected);
                Task.Run(() =>
                {

                    List<IEntry> itemsToTrash = selected.FlattenTree().OrderByDescending(x => x.UIndex).Select(tvEntry => tvEntry.Entry).ToList();

                    IEntry entryWithReferences =
                        // Requested by Khaar 05/12/2022
                        // Way to bypass references check as it slows down mass
                        // trashing of objects especially when the dev knows what they're doing
                        // Implemented by Mgamerz 05/14/2022
                        skipReferencesCheck ? null : GetExternallyReferencedEntry(itemsToTrash);
                    return (itemsToTrash, entryWithReferences);
                }).ContinueWithOnUIThread(prevTask =>
                {
                    IsBusy = false;
                    (List<IEntry> itemsToTrash, IEntry entryWithReferences) = prevTask.Result;
                    if (entryWithReferences is not null)
                    {
                        MessageBoxResult messageBoxResult = MessageBox.Show(this,
                            $"#{entryWithReferences.UIndex} {entryWithReferences.InstancedFullPath} is referenced by other entries! Use the \"{FindReferencesMenuText}\" option in the context menu to see the references. " +
                            "These references will be broken if you trash it! Are you sure you want to proceed?",
                            "Trash warning", MessageBoxButton.YesNo);
                        if (messageBoxResult != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }

                    int newSelection = selected.Entry.Parent?.UIndex ?? 0; // The parent
                    if (positionInBranch > 0)
                    {
                        if (selected.Parent.Sublinks.Count > positionInBranch + 1) // Node has not been removed yet from the entry tree so we have to check +1
                        {
                            newSelection = selected.Parent.Sublinks[positionInBranch + 1].UIndex; // Go to the item that will be shifted into our position
                        }
                        else if (positionInBranch > 0) // go to the previous item
                        {
                            newSelection = selected.Parent.Sublinks[positionInBranch - 1].UIndex; // Go to the item that is was before our item
                        }
                    }

                    if (!GoToNumber(newSelection))
                    {
                        AllTreeViewNodesX[0].IsProgramaticallySelecting = true;
                        SelectedItem = AllTreeViewNodesX[0];
                    }

                    bool removedFromLevel = selected.Entry is ExportEntry { ParentName: "PersistentLevel" } exp && exp.IsA("Actor") && Pcc.RemoveFromLevelActors(exp);

                    EntryPruner.TrashEntries(Pcc, itemsToTrash);

                    if (removedFromLevel)
                    {
                        MessageBox.Show(this, "Trashed and removed from level!");
                    }
                });

                static IEntry GetExternallyReferencedEntry(List<IEntry> entriesToTrash)
                {
                    if (entriesToTrash.IsEmpty())
                    {
                        return null;
                    }
                    IMEPackage pcc = entriesToTrash[0].FileRef;
                    MEGame pccGame = pcc.Game;
                    var uIndexes = new HashSet<int>(entriesToTrash.Select(entry => entry.UIndex));

                    foreach (ExportEntry exp in pcc.Exports.Except(entriesToTrash.OfType<ExportEntry>()))
                    {
                        try
                        {
                            //find header references
                            if (uIndexes.Contains(exp.idxArchetype))
                            {
                                return pcc.GetEntry(exp.idxArchetype);
                            }
                            if (uIndexes.Contains(exp.idxClass))
                            {
                                return pcc.GetEntry(exp.idxClass);
                            }
                            if (uIndexes.Contains(exp.idxSuperClass))
                            {
                                return pcc.GetEntry(exp.idxSuperClass);
                            }
                            if (exp.HasComponentMap && exp.ComponentMap.Any(kvp => uIndexes.Contains(kvp.Value)))
                            {
                                return pcc.GetEntry(exp.ComponentMap.Values.First(uIdx => uIndexes.Contains(uIdx)));
                            }

                            //find stack references
                            if (exp.HasStack)
                            {
                                if (uIndexes.TryGetValue(EndianReader.ToInt32(exp.DataReadOnly, 0, exp.FileRef.Endian), out int stack1))
                                {
                                    return pcc.GetEntry(stack1);
                                }
                                if (uIndexes.TryGetValue(EndianReader.ToInt32(exp.DataReadOnly, 4, exp.FileRef.Endian), out int stack2))
                                {
                                    return pcc.GetEntry(stack2);
                                }
                            }
                            else if (exp.TemplateOwnerClassIdx is var toci and >= 0 &&
                                     uIndexes.TryGetValue(EndianReader.ToInt32(exp.DataReadOnly, toci, exp.FileRef.Endian), out int tocuIdx))
                            {
                                return pcc.GetEntry(tocuIdx);
                            }


                            //find property references
                            if (GetReferencedEntryInProps(exp.GetProperties()) is IEntry entry)
                            {
                                return entry;
                            }

                            //find binary references
                            if (!exp.IsDefaultObject
                                && exp.ClassName != "AnimSequence" //has no UIndexes, and is expensive to deserialize
                                && ObjectBinary.From(exp) is ObjectBinary objBin)
                            {
                                var indices = new List<int>();
                                if (objBin is Level levelBin)
                                {
                                    //trashing a level object will automatically remove it from the Actor list
                                    //so we don't care if it's referenced there
                                    levelBin.ForEachUIndexExceptActorList(pccGame, new UIndexCollector(indices));
                                }
                                else
                                {
                                    objBin.ForEachUIndex(pccGame, new UIndexCollector(indices));
                                }
                                foreach (int uIndex in indices)
                                {
                                    if (uIndexes.Contains(uIndex))
                                    {
                                        return pcc.GetEntry(uIndex);
                                    }
                                }
                            }
                        }
                        catch (Exception e) //when (!App.IsDebug)
                        {
                            MessageBox.Show($"Exception occurred while reading export# {exp.UIndex}: {e.Message}");
                        }
                    }

                    return null;

                    IEntry GetReferencedEntryInProps(PropertyCollection props)
                    {
                        foreach (Property prop in props)
                        {
                            switch (prop)
                            {
                                case ObjectProperty objectProperty:
                                    if (uIndexes.Contains(objectProperty.Value))
                                    {
                                        return pcc.GetEntry(objectProperty.Value);
                                    }
                                    break;
                                case DelegateProperty delegateProperty:
                                    if (uIndexes.Contains(delegateProperty.Value.ContainingObjectUIndex))
                                    {
                                        return pcc.GetEntry(delegateProperty.Value.ContainingObjectUIndex);
                                    }
                                    break;
                                case StructProperty structProperty:
                                    if (GetReferencedEntryInProps(structProperty.Properties) is ExportEntry export1)
                                    {
                                        return export1;
                                    }
                                    break;
                                case ArrayProperty<ObjectProperty> arrayProperty:
                                    foreach (ObjectProperty objProp in arrayProperty)
                                    {
                                        if (uIndexes.Contains(objProp.Value))
                                        {
                                            return pcc.GetEntry(objProp.Value);
                                        }
                                    }
                                    break;
                                case ArrayProperty<StructProperty> arrayProperty:
                                    foreach (StructProperty structProp in arrayProperty)
                                    {
                                        if (GetReferencedEntryInProps(structProp.Properties) is IEntry entry)
                                        {
                                            return entry;
                                        }
                                    }
                                    break;
                            }
                        }
                        return null;
                    }
                }
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public static string FindReferencesMenuText => "Find references";
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
                string name = iName.Name;
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
                    { DoubleClickEntryHandler = entryDoubleClickToTreeview };
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
                    case "SoundNodeWave":
                    case "BioSoundNodeWaveStreamingData":
                    case "FaceFXAsset":
                    case "WwiseBank":
                    case "BrushComponent":
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
                                byte[] data = DataProp.Bytes;

                                if (savePath == null)
                                {
                                    //GFX is scaleform extensions for SWF
                                    //SWC is Shockwave Compressed
                                    //SWF is Shockwave Flash (uncompressed)
                                    var d = new SaveFileDialog
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

                            break;
                        }
                    case "BioTlkFile":
                        {
                            string extension = Path.GetExtension(".xml");
                            var d = new SaveFileDialog
                            {
                                Title = "Export TLK as XML",
                                FileName = exp.FullPath + ".xml",
                                Filter = $"*{extension}|*{extension}"
                            };
                            if (d.ShowDialog() == true)
                            {
                                var exportingTalk = new ME1TalkFile(exp);
                                exportingTalk.SaveToXML(d.FileName);
                                MessageBox.Show("Done");
                            }

                            break;
                        }
                    case "SoundNodeWave":
                        {
                            var ob = ObjectBinary.From<SoundNodeWave>(exp);
                            if (ob.RawData == null || !ob.RawData.Any())
                            {
                                MessageBox.Show("This export has no sound data embedded in it.");
                                return;
                            }

                            var d = new CommonOpenFileDialog()
                            {
                                Title = "Select output folder for ICB/ISB",
                                IsFolderPicker = true
                            };

                            if (d.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                // todo: Change to ISACTBankPair?

                                // ICB
                                var outDir = d.FileName;
                                // todo: Use objectbinary when we implement it
                                var data = new MemoryStream(ob.RawData);
                                // var totalStreamingDataLen = data.ReadInt32();
                                var isbOffset = data.ReadInt32();

                                string icbName = null;

                                // ICB
                                var dataStartPos = data.Position; // RIFF start
                                var riffForDebug = data.ReadStringASCII(0x4); // get riff length
                                var riffLen = data.ReadInt32() + 0x8; // include len and RIFF
                                data.Skip(0x8); // Jump to start of unicode string
                                var strLen = data.ReadInt32();
                                icbName = data.ReadStringUnicodeNull(strLen);

                                data.Position = dataStartPos;
                                using FileStream fs = new FileStream(Path.Combine(outDir, icbName), FileMode.Create);
                                data.CopyToEx(fs, riffLen);

                                // ISB
                                data.Position = isbOffset;

                                var audioName =
                                    exp.ObjectName.Instanced.Substring(exp.ObjectName.Instanced.IndexOf(":") +
                                                                       1); // This is really weak 
                                using FileStream fs2 = new FileStream(
                                    Path.Combine(outDir,
                                        $"{Path.GetFileNameWithoutExtension(icbName)}_{audioName}.isb"),
                                    FileMode.Create);
                                data.Copy(fs2, new byte[2048]);

                                MessageBox.Show("Done");
                            }
                        }
                        break;
                    case "BioSoundNodeWaveStreamingData":
                        {
                            var d = new CommonOpenFileDialog
                            {
                                Title = "Select output folder for ICB/Stripped ISB",
                                IsFolderPicker = true
                            };
                            if (d.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                // ICB
                                var outDir = d.FileName;

                                var bsnwsd = ObjectBinary.From<BioSoundNodeWaveStreamingData>(exp);
                                var icbBank = bsnwsd.BankPair.ICBBank;
                                var icbName = icbBank.BankChunks.OfType<TitleBankChunk>().FirstOrDefault();

                                using var fs =
                                    new FileStream(
                                        Path.Combine(outDir, Path.GetFileNameWithoutExtension(icbName.Value) + ".icb"),
                                        FileMode.Create);
                                bsnwsd.BankPair.ICBBank.Write(fs);
                                // ISB
                                using var fs2 =
                                    new FileStream(
                                        Path.Combine(outDir, Path.GetFileNameWithoutExtension(icbName.Value) + ".isb"),
                                        FileMode.Create);
                                bsnwsd.BankPair.ISBBank.Write(fs2);

                                MessageBox.Show("Done");
                            }

                            break;
                        }
                    case "FaceFXAsset":
                        {
                            var d = new SaveFileDialog
                            {
                                Title = "Save Face FX Asset",
                                FileName = exp.FullPath + ".fxa",
                                Filter = "*.fxa|*.fxa"
                            };
                            if (d.ShowDialog() == true)
                            {
                                var data = new MemoryStream(exp.GetBinaryData());
                                data.Skip(0x4);
                                using FileStream fs = new FileStream(d.FileName, FileMode.Create);
                                data.CopyToEx(fs, (int)data.Length - 4);
                                MessageBox.Show("Done");
                            }

                            break;
                        }
                    case "WwiseBank":
                        {
                            var wdiag = new SaveFileDialog
                            {
                                Title = "WwiseBank file",
                                FileName = exp.FullPath + ".bnk",
                                Filter = "*.bnk|*.bnk"
                            };
                            if (wdiag.ShowDialog() == true)
                            {
                                var data = new MemoryStream(exp.GetBinaryData());
                                if (exp.Game.IsGame3())
                                {
                                    data.Skip(0x10);
                                }
                                else if (exp.Game.IsGame2())
                                {
                                    data.Skip(0x18);
                                }

                                using FileStream fs = new FileStream(wdiag.FileName, FileMode.Create);
                                data.CopyToEx(fs, (int)data.Length - 0x10);
                                MessageBox.Show("Done");
                            }
                        }
                        break;
                    case "BrushComponent":
                        {
                            var cachedConv = ObjectBinary.From<BrushComponent>(exp);
                            if (cachedConv.CachedPhysBrushData == null ||
                                cachedConv.CachedPhysBrushData.CachedConvexElements == null ||
                                cachedConv.CachedPhysBrushData.CachedConvexElements.Length == 0)
                            {
                                MessageBox.Show("This BrushComponent doesn't have a cached convex hull");
                                break;
                            }

                            var saveDiag = new SaveFileDialog
                            {
                                Title = "Cached Convex Hull Data",
                                FileName = exp.InstancedFullPath + ".phys",
                                Filter = "*.phys|*.phys"
                            };
                            if (saveDiag.ShowDialog() == true)
                            {
                                File.WriteAllBytes(saveDiag.FileName, cachedConv.CachedPhysBrushData.CachedConvexElements[0].ConvexElementData);
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
                                var d = new OpenFileDialog
                                {
                                    Title = "Replace SWF",
                                    FileName = exp.FullPath + ".swf",
                                    Filter = $"*{extension};*.gfx|*{extension};*.gfx",
                                    CustomPlaces = AppDirectories.GameCustomPlaces
                                };
                                if (d.ShowDialog() == true)
                                {
                                    var bytes = File.ReadAllBytes(d.FileName);
                                    var props = exp.GetProperties();

                                    string dataPropName = exp.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
                                    var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                                    //Write SWF data
                                    rawData.Bytes = bytes;

                                    //Write SWF metadata
                                    if (exp.FileRef.Game.IsGame1() || exp.FileRef.Game.IsGame2())
                                    {
                                        string sourceFilePropName = "SourceFilePath";
                                        StrProperty sourceFilePath = props.GetProp<StrProperty>(sourceFilePropName);
                                        if (sourceFilePath == null)
                                        {
                                            sourceFilePath = new StrProperty(d.FileName, sourceFilePropName);
                                            props.Add(sourceFilePath);
                                        }

                                        sourceFilePath.Value = d.FileName;
                                    }

                                    if (exp.FileRef.Game.IsGame1())
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
                            break;
                        }
                    case "BioTlkFile":
                        {
                            string extension = Path.GetExtension(".xml");
                            var d = new OpenFileDialog
                            {
                                Title = "Replace TLK from exported XML (ME1 Only)",
                                FileName = exp.FullPath + ".xml",
                                Filter = $"*{extension}|*{extension}",
                                CustomPlaces = AppDirectories.GameCustomPlaces
                            };
                            if (d.ShowDialog() == true)
                            {
                                HuffmanCompression compressor = new HuffmanCompression();
                                compressor.LoadInputData(d.FileName);
                                compressor.SerializeTalkfileToExport(exp, false);
                            }
                            break;
                        }
                    case "BioSoundNodeWaveStreamingData":
                        {

                            // Requires ICB and ISB
                            string extension = Path.GetExtension(".icb");
                            var d = new OpenFileDialog
                            {
                                Title = "Select the ICB file (ISB should be same name next to it)",
                                Filter = $"ISACT Content Bank (*.icb)|*{extension}",
                                CheckFileExists = true,
                                CustomPlaces = AppDirectories.GameCustomPlaces
                            };
                            if (d.ShowDialog() == false)
                                return;

                            var isbF = Path.Combine(Directory.GetParent(d.FileName).FullName, $"{Path.GetFileNameWithoutExtension(d.FileName)}.isb");
                            var errorMsg = ISACTHelper.GenerateSoundNodeWaveStreamingDataCS(exp, d.FileName, isbF);
                            if (errorMsg != null)
                            {
                                MessageBox.Show(errorMsg);
                            }
                            break;
                        }
                    case "SoundNodeWave":
                        {
                            // I don't think we should import this way. In release builds don't allow this
#if !DEBUG
                            MessageBox.Show("Not currently supported");
                            return;
#endif
                            // Requires ICB and ISB
                            string extension = Path.GetExtension(".icb");
                            var d = new OpenFileDialog
                            {
                                Title = "Select stripped ICB",
                                Filter = $"*{extension}|*{extension}",
                                CustomPlaces = AppDirectories.GameCustomPlaces
                            };
                            if (d.ShowDialog() == false)
                                return;

                            extension = ".isb";
                            var d2 = new OpenFileDialog
                            {
                                Title = "Select stripped ISB",
                                Filter = $"*{extension}|*{extension}",
                                CustomPlaces = AppDirectories.GameCustomPlaces
                            };
                            if (d2.ShowDialog() == false)
                                return;

                            MemoryStream ms = new MemoryStream();
                            ms.WriteInt32(0);
                            ms.Write(File.ReadAllBytes(d.FileName));
                            ms.Seek(0, SeekOrigin.Begin);
                            ms.WriteInt32((int)ms.Length /*- 4*/);
                            ms.Seek(0, SeekOrigin.End);
                            ms.Write(File.ReadAllBytes(d2.FileName));
                            var snw = ObjectBinary.From<SoundNodeWave>(exp);
                            snw.RawData = ms.ToArray();
                            exp.WriteBinary(snw);
                        }
                        break;
                    case "FaceFXAsset":
                        {
                            string extension = Path.GetExtension(".fxa");
                            var d = new OpenFileDialog
                            {
                                Title = "Select FaceFX Asset",
                                Filter = $"*{extension}|*{extension}",
                                CustomPlaces = AppDirectories.GameCustomPlaces
                            };
                            if (d.ShowDialog() == true)
                            {
                                var length = new FileInfo(d.FileName).Length;
                                MemoryStream outStream = new MemoryStream();
                                outStream.WriteInt32((int)length - 4);
                                outStream.Write(File.ReadAllBytes(d.FileName));
                                exp.WriteBinary(outStream.GetBuffer());
                            }
                            break;
                        }
                    case "WwiseBank":
                        {
                            string extension = Path.GetExtension(".bnk");
                            var wdiag = new OpenFileDialog
                            {
                                Title = "Select WwiseBank file",
                                Filter = $"*{extension}|*{extension}",
                                CustomPlaces = AppDirectories.GameCustomPlaces
                            };
                            if (wdiag.ShowDialog() == true)
                            {
                                var length = new FileInfo(wdiag.FileName).Length;
                                MemoryStream outStream = new MemoryStream();
                                // Write Bulk Data header
                                outStream.WriteInt32(0); // Local
                                outStream.WriteInt32((int)length); // Compressed size
                                outStream.WriteInt32((int)length); // Decompressed size
                                outStream.WriteInt32(0); // Data offset - this is not external so this is not used
                                outStream.Write(File.ReadAllBytes(wdiag.FileName));
                                exp.WriteBinary(outStream.ToArray()); // Do not use buffer
                            }
                            break;
                        }
                    case "BrushComponent":
                        {
                            string extension = Path.GetExtension(".phys");
                            var wdiag = new OpenFileDialog
                            {
                                Title = "Select LEX exported Phys file",
                                Filter = $"*{extension}|*{extension}",
                                CustomPlaces = AppDirectories.GameCustomPlaces
                            };
                            if (wdiag.ShowDialog() == true)
                            {
                                var brush = BrushComponent.Create();
                                brush.CachedPhysBrushData.CachedConvexElements = new KCachedConvexDataElement[]
                                {
                                    new KCachedConvexDataElement() { ConvexElementData = File.ReadAllBytes(wdiag.FileName) }
                                };
                                exp.WriteBinary(brush);
                            }
                            break;
                        }
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
                string name = iName.Name;
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
                defaultValue: "search text", selectText: true);
            if (string.IsNullOrEmpty(searchstr))
                return;

            string replacestr = PromptDialog.Prompt(this, "Input new text:", "Search and Replace Names",
                defaultValue: "replacement text", selectText: true);
            if (string.IsNullOrEmpty(replacestr))
                return;

            var wdlg = MessageBox.Show(
                $"This will replace every name containing the text \"{searchstr}\" with a new name containing \"{replacestr}\".\n" +
                $"This may break any properties, or links containing this string. Please confirm.", "WARNING:",
                MessageBoxButton.OKCancel);
            if (wdlg == MessageBoxResult.Cancel)
                return;
            int count = 0;
            for (int i = 0; i < Pcc.Names.Count; i++)
            {
                string name = Pcc.Names[i];
                if (name.Contains(searchstr))
                {
                    var newName = name.Replace(searchstr, replacestr);
                    Pcc.replaceName(i, newName);
                    count++;
                }
            }

            RefreshNames();
            RefreshView();
            MessageBox.Show($"{count} names were amended.", "Search and Replace Names", MessageBoxButton.OK);
        }

        private void CheckForBadObjectPropertyReferences()
        {
            if (Pcc == null)
            {
                return;
            }

            ReferenceCheckPackage rcp = new ReferenceCheckPackage();
            EntryChecker.CheckReferences(rcp, Pcc, LECLocalizationShim.NonLocalizedStringConverter);

            if (rcp.GetSignificantIssues().Any())
            {
                MessageBox.Show($"{rcp.GetSignificantIssues().Count} object reference issues were found.", "Reference issues found");
                var lw = new ListDialog(rcp.GetSignificantIssues().ToList(), $"Reference issues in {Pcc.FilePath}",
                        "The following items have referencing issues. Note that this is a best-effort check and may not be 100% accurate.",
                        this)
                { DoubleClickEntryHandler = entryDoubleClick };
                lw.Show();
            }
            else
            {
                MessageBox.Show(
                    "No referencing issues were found. Note that this is a best-effort check and may not be 100% accurate and does not account for imports being preloaded in memory before package load.",
                    "Check complete");
            }
        }

        private void CheckForBrokenMaterials()
        {
            if (Pcc == null)
            {
                return;
            }

            var brokenMaterials = ShaderCacheManipulator.GetBrokenMaterials(Pcc);
            if (brokenMaterials.Any())
            {
                var lw = new ListDialog(brokenMaterials.Select(exp => new EntryStringPair(exp)), $"Broken Materials in {Pcc.FilePath}",
                        "The following Materials or MaterialInstances have no corresponding entry in either the local or global shader cache.",
                        this)
                { DoubleClickEntryHandler = entryDoubleClick };
                lw.Show();
            }
            else
            {
                MessageBox.Show("No broken materials were found.",
                    "Check complete");
            }
        }

        private void CheckForDuplicateIndexes()
        {
            if (Pcc == null)
            {
                return;
            }

            var duplicates = EntryChecker.CheckForDuplicateIndices(Pcc);

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
                        "The following items have duplicate indexes. The game may choose to use the first occurrence of the index it finds, or may crash if indexing is checked internally (such as pathfinding). You can reindex an object to force all same named items to be reindexed in the given unique path. You should reindex from the topmost duplicate entry first if one is found, as it may resolve lower item duplicates.",
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
                        if (offsetDec >= imp.HeaderOffset && offsetDec < imp.HeaderOffset + ImportEntry.HeaderLength)
                        {
                            GoToNumber(imp.UIndex);
                            Metadata_Tab.IsSelected = true;
                            MetadataTab_MetadataEditor.SetHexboxSelectedOffset(imp.HeaderOffset + ImportEntry.HeaderLength - offsetDec);
                            return;
                        }
                    }

                    foreach (ExportEntry exp in Pcc.Exports)
                    {
                        //header
                        if (offsetDec >= exp.HeaderOffset && offsetDec < exp.HeaderOffset + exp.HeaderLength)
                        {
                            GoToNumber(exp.UIndex);
                            Metadata_Tab.IsSelected = true;
                            MetadataTab_MetadataEditor.SetHexboxSelectedOffset(exp.HeaderOffset + exp.HeaderLength - offsetDec);
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
                    TryAddToStaticCollectionActor(newTreeRoot, entry);
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
                    TryAddToStaticCollectionActor(newEntry, entry);
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

        private bool TryAddToStaticCollectionActor(IEntry newEntry, IEntry originalEntry)
        {
            if (newEntry is ExportEntry
                {
                    Parent: ExportEntry
                    {
                        ClassName: nameof(StaticMeshCollectionActor) or nameof(StaticLightCollectionActor)
                    } scaExp
                } &&
                ObjectBinary.From(scaExp) is StaticCollectionActor scaBin)
            {
                var componentsProp = scaExp.GetProperty<ArrayProperty<ObjectProperty>>(scaBin.ComponentPropName);
                int originalIndex = componentsProp.IndexOf(new ObjectProperty(originalEntry));
                if (originalIndex == -1)
                {
                    return false;
                }
                componentsProp.Add(new ObjectProperty(newEntry));
                scaExp.WriteProperty(componentsProp);

                scaBin.LocalToWorldTransforms.Add(scaBin.LocalToWorldTransforms[originalIndex]);
                scaExp.WriteBinary(scaBin);
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
                FileName = export.ObjectName.Instanced + ".bin",
                CustomPlaces = AppDirectories.GameCustomPlaces
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




        #endregion

        public PackageEditorWindow() : this(submitTelemetry: true) { }

        public PackageEditorWindow(bool submitTelemetry = true) : base("Package Editor", submitTelemetry)
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
            ExportLoaders[BinaryInterpreterTab_BinaryInterpreter] = BinaryInterpreter_Tab;
            ExportLoaders[EmbeddedTextureViewerTab_EmbededTextureViewer] = EmbeddedTextureViewer_Tab;
            ExportLoaders[CollectionActorEditorTab_CollectionActorEditor] = CollectionActorEditor_Tab;
            ExportLoaders[ParticleSystemTab_ParticleSystemLoader] = ParticleSystem_Tab;
            ExportLoaders[ParticleModuleTab_ParticleModuleLoader] = ParticleModule_Tab;
            ExportLoaders[MeshRendererTab_MeshRenderer] = MeshRenderer_Tab;
            ExportLoaders[JPEXLauncherTab_JPEXLauncher] = JPEXLauncher_Tab;
            ExportLoaders[TlkEditorTab_TlkEditor] = TlkEditor_Tab;
            ExportLoaders[MaterialViewerTab_MaterialExportLoader] = MaterialViewer_Tab;
            ExportLoaders[ScriptTab_UnrealScriptIDE] = Script_Tab;
            ExportLoaders[RADLauncherTab_BIKLauncher] = RADLaunch_Tab;
            ExportLoaders[AnimNodeTab_AnimNodeLoader] = AnimNode_Tab;


            InterpreterTab_Interpreter.SetParentNameList(NamesList); //reference to this control for name editor set

            BinaryInterpreterTab_BinaryInterpreter.SetParentNameList(NamesList); //reference to this control for name editor set
            Bio2DATab_Bio2DAEditor.SetParentNameList(NamesList); //reference to this control for name editor set

            InterpreterTab_Interpreter.HideHexBox = Settings.PackageEditor_HideInterpreterHexBox;
            InterpreterTab_Interpreter.ToggleHexbox_Button.Visibility = Visibility.Visible;

            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileName => LoadFile(fileName));
        }

        /// <summary>
        /// Opens an existing package object, that may have been loaded from somewhere else.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="goToIndex"></param>
        /// <param name="goToEntry"></param>
        public void LoadPackage(IMEPackage package, int goToIndex = 0, string goToEntry = null)
        {
            // Todo: Maybe prompt if there are pending changes to the current package?
            var packageFilePath = package.FilePath;
            try
            {
                preloadPackage(Path.GetFileName(packageFilePath), 0); // Package is already loaded.
                RegisterPackage(package);
                _selectedItem = null; // We change the backing data so we don't fire off a tree event since it checks if Pcc is null.
                if (goToIndex == 0 && !string.IsNullOrWhiteSpace(goToEntry))
                {
                    goToIndex = Pcc.FindEntry(goToEntry)?.UIndex ?? 0;
                }

                postloadPackage(packageFilePath, goToIndex);
                if (File.Exists(packageFilePath))
                {
                    RecentsController.AddRecent(packageFilePath, false, Pcc?.Game);
                    RecentsController.SaveRecentList(true);
                }
            }
            catch (Exception e) when (!App.IsDebug)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(packageFilePath);
                MessageBox.Show($"Error loading {Path.GetFileName(packageFilePath)}:\n{e.Message}");
                IsBusy = false;
                IsBusyTaskbar = false;
                //throw e;
            }
        }

        public void LoadFile(string s, int goToIndex = 0, string goToEntry = null)
        {
            // Todo: Maybe prompt if there are pending changes to the current package?
            try
            {
                preloadPackage(Path.GetFileName(s), new FileInfo(s).Length);
                LoadMEPackage(s);
                _selectedItem = null; // We change the backing data so we don't fire off a tree event since it checks if Pcc is null.
                if (goToIndex == 0 && !string.IsNullOrWhiteSpace(goToEntry))
                {
                    goToIndex = Pcc.FindEntry(goToEntry)?.UIndex ?? 0;
                }
                postloadPackage(s, goToIndex);

                RecentsController.AddRecent(s, false, Pcc?.Game);
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
        private void postloadPackage(string filePath, int goToIndex = 0)
        {
            RefreshView();
            InitStuff();
            StatusBar_LeftMostText.Text = GetStatusBarText();
            Title = $"Package Editor - {filePath}";
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

            ResetTreeView();
            NamesList.ClearEx();
            ClassDropdownList.ClearEx();
            BackwardsIndexes = new Stack<int>();
            ForwardsIndexes = new Stack<int>();
            StatusBar_LeftMostText.Text =
                $"Loading {loadingName} ({FileSize.FormatSize(loadingSize)})";
            //Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
        }

        private void InitializeTreeViewBackground_Completed(Task<List<TreeViewEntry>> prevTask)
        {
            if (prevTask.Result != null)
            {
                ResetTreeView();
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

        private List<TreeViewEntry> InitializeTreeViewBackground()
        {
            BusyText = "Loading " + Path.GetFileName(Pcc.FilePath);
            if (Pcc == null)
            {
                return null;
            }

            IReadOnlyList<ImportEntry> Imports = Pcc.Imports;
            IReadOnlyList<ExportEntry> Exports = Pcc.Exports;

            var rootEntry = new TreeViewEntry(null, Path.GetFileName(Pcc.FilePath)) { IsExpanded = true, PackageRef = Pcc };

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

            return new List<TreeViewEntry>(rootNodes.Except(itemsToRemove));
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
        public bool GetSelected(out int n)
        {
            n = 0;
            if (Pcc is null)
            {
                return false;
            }
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
                case CurrentViewMode.Names:
                default:
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

        internal bool TryGetSelectedExport([NotNullWhen(true)] out ExportEntry? export)
        {
            if (GetSelected(out int uIndex) && Pcc.IsUExport(uIndex))
            {
                export = Pcc.GetUExport(uIndex);
                return true;
            }

            export = null;
            return false;
        }

        private bool TryGetSelectedImport([NotNullWhen(true)] out ImportEntry? import)
        {
            if (GetSelected(out int uIndex) && Pcc.IsImport(uIndex))
            {
                import = Pcc.GetImport(uIndex);
                return true;
            }

            import = null;
            return false;
        }

        public override void HandleUpdate(List<PackageUpdate> updates)
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

            if (updates.Any(x => x.Change is PackageChange.ExportRemove or PackageChange.ImportRemove))
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

            List<PackageUpdate> addedChanges = updates.Where(x => x.Change.HasFlag(PackageChange.EntryAdd)).OrderBy(x => x.Index).ToList();
            HashSet<int> headerChanges = updates.Where(x => x.Change.HasFlag(PackageChange.EntryHeader)).Select(x => x.Index).ToHashSet();

            // Reduces tree enumeration
            List<TreeViewEntry> treeViewItems = AllTreeViewNodesX[0].FlattenTree();
            var uindexMap = new Dictionary<int, TreeViewEntry>();
            if (Enumerable.Any(addedChanges) || Enumerable.Any(headerChanges))
            {
                foreach (TreeViewEntry tv in treeViewItems)
                {
                    uindexMap[tv.UIndex] = tv;
                }
            }

            if (addedChanges.Count > 0)
            {
                InitClassDropDown();
                MetadataTab_MetadataEditor.RefreshAllEntriesList(Pcc);
                //Find nodes that haven't been generated and added yet

                List<IEntry> entriesToAdd = addedChanges.Select(change => Pcc.GetEntry(change.Index)).ToList();

                //Generate new nodes
                var nodesToSortChildrenFor = new HashSet<TreeViewEntry>();
                //might have to loop a few times if it contains children before parents

                while (Enumerable.Any(entriesToAdd))
                {
                    var orphans = new List<IEntry>();
                    foreach (IEntry entry in entriesToAdd)
                    {
                        if (uindexMap.TryGetValue(entry.idxLink, out TreeViewEntry parent))
                        {
                            var newEntry = new TreeViewEntry(entry) { Parent = parent };
                            parent.Sublinks.Add(newEntry);
                            treeViewItems.Add(newEntry); //used to find parents
                            nodesToSortChildrenFor.Add(parent);
                            uindexMap[entry.UIndex] = newEntry;
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

            if (headerChanges.Count > 0)
            {
                //List<TreeViewEntry> tree = AllTreeViewNodesX[0].FlattenTree();
                var nodesNeedingResort = new List<TreeViewEntry>();
                List<TreeViewEntry> tviWithChangedHeaders = uindexMap.Values.Where(x => x.UIndex != 0 && headerChanges.Contains(x.Entry.UIndex)).ToList();
                foreach (TreeViewEntry tvi in tviWithChangedHeaders)
                {
                    if (tvi.Parent.UIndex != tvi.Entry.idxLink)
                    {
                        //Debug.WriteLine("Reorder req for " + tvi.UIndex);
                        if (!uindexMap.TryGetValue(tvi.Entry.idxLink, out var newParent))
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

            if (CurrentView is CurrentViewMode.Exports or CurrentViewMode.Tree && hasSelection &&
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
                    new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
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
                        var nr = Pcc.Names[update.Index];
                        NamesList.Add(new IndexedName(update.Index, nr));
                        if (CurrentView == CurrentViewMode.Names)
                        {
                            LeftSideList_ItemsSource.Add(new IndexedName(update.Index, nr));
                        }
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
            if (CurrentView is CurrentViewMode.Imports or CurrentViewMode.Exports or CurrentViewMode.Tree)
            {
                Interpreter_Tab.IsEnabled = selectedEntry is ExportEntry;
                if (selectedEntry is ExportEntry exportEntry)
                {
                    foreach ((ExportLoaderControl exportLoader, TabItem tab) in ExportLoaders)
                    {
                        try
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
                        catch (Exception e)
                        {
                            new ExceptionHandlerDialog(e).ShowDialog();
                        }
                    }

                    if (Interpreter_Tab.IsSelected && exportEntry.ClassName == "Class")
                    {
                        //We are on interpreter tab, selecting class. Switch to binary interpreter as interpreter will never be useful
                        BinaryInterpreter_Tab.IsSelected = true;
                    }
                    if (Interpreter_Tab.IsSelected && Bytecode_Tab.IsVisible)
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
                var currentTab = (TabItem)EditorTabs.Items[EditorTabs.SelectedIndex];
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
                        if (Enumerable.Any(selectNode))
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

        public bool GoToEntry(string instancedFullPath)
        {
            if (instancedFullPath == null) return false;
            if (Pcc.FindEntry(instancedFullPath) is IEntry entry)
            {
                CurrentView = CurrentViewMode.Tree;
                return GoToNumber(entry.UIndex);
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

                var dragInfo = dropInfo.DragInfo;
                var sourceWindow = Window.GetWindow(dragInfo.VisualSource) as PackageEditorWindow;
                if (targetItem.Game.IsLEGame() != sourceItem.Game.IsLEGame() &&
                    !App.IsDebug &&
                    sourceItem.Entry.Game != MEGame.UDK) // allow UDK -> OT and LE)
                {
                    MessageBox.Show(
                        "Cannot port assets between Original Trilogy (OT) games and Legendary Edition (LE) games in release builds of Legendary Explorer.", "Cannot port asset", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 07/06/2022
                // Holding shift will allow to drag an export to another link in the same package
                // Check if the path of the target and the source is the same. If so, offer to merge instead
                var isShiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                var isSamePackageDrop = (targetItem.Entry != null && sourceItem.Entry.FileRef == targetItem.Entry.FileRef) // entry to entry
                                        || (targetItem.PackageRef != null && sourceItem.Entry.FileRef == targetItem.PackageRef); // entry to root

                if (sourceItem == targetItem || (isSamePackageDrop && !isShiftHeld))
                {
                    return; // ignore
                }

                if (isSamePackageDrop && isShiftHeld)
                {
                    // Change the link instead
                    sourceItem.Entry.idxLink = targetItem?.Entry?.UIndex ?? 0;
                    return;
                }

                var portingOption = TreeMergeDialog.GetMergeType(sourceWindow, this, sourceItem, targetItem, Pcc.Game);

                if (portingOption.PortingOptionChosen == EntryImporter.PortingOption.Cancel)
                {
                    return;
                }


                if (sourceItem.Entry.FileRef == null)
                {
                    return;
                }

                IEntry sourceEntry = sourceItem.Entry;
                IEntry targetLinkEntry = targetItem.Entry;

                int originalIndex = -1;
                bool hadChanges = false;
                bool hadHeaderChanges = false;
                if (portingOption.PortingOptionChosen != EntryImporter.PortingOption.ReplaceSingular
                    && portingOption.PortingOptionChosen != EntryImporter.PortingOption.ReplaceSingularWithRelink
                    && targetItem.Entry?.FileRef.FindEntry(sourceItem.Entry.InstancedFullPath) != null)
                {
                    // It's a duplicate. Offer to index it, as this will break the lookup if it's identical on inbound
                    // (it will just install into an existing entry)
                    var result = MessageBox.Show("The item being ported in has the same full path as an object in the target package. " +
                                                 "This will cause issues in the game as well as with the toolset if the imported object is not renamed beforehand or has its index changed.\n\n" +
                                                 "Legendary Explorer will automatically adjust the index for you. You may need to adjust it back after changing the name.", "Indexing issues",
                                                 MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
                    if (result == MessageBoxResult.Cancel)
                    {
                        return; // User canceled
                    }

                    // Adjust numeral on inbound export so it doesn't port into existing item.
                    // save original value for restoration after porting operation is complete.
                    originalIndex = sourceEntry.indexValue;
                    hadChanges = sourceEntry.EntryHasPendingChanges;
                    hadHeaderChanges = sourceEntry.HeaderChanged;
                    sourceEntry.indexValue = targetItem.Entry.FileRef.GetNextIndexedName(sourceEntry.ObjectName).Number;
                }

                // Load the object DB if games are different
                string objectDBPath = AppDirectories.GetObjectDatabasePath(targetItem.Game);
                bool shouldUseDonors = portingOption.PortUsingDonors && sourceEntry.Game != targetItem.Game && sourceEntry.Game != MEGame.UDK;
                ObjectInstanceDB objectDB = null;
                if (shouldUseDonors)
                {
                    if (File.Exists(objectDBPath))
                    {
                        using FileStream fs = File.OpenRead(objectDBPath);
                        objectDB = ObjectInstanceDB.Deserialize(targetItem.Game, fs);
                    }
                    else
                    {
                        var result = MessageBox.Show("Port With Donors checkbox was selected, but no object database was found! Continue operation without donors?",
                            "No object database", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                        if (result is not MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }
                }

                // To profile this, run dotTrace and attach to the process, make sure to choose option to profile via API
                //MeasureProfiler.StartCollectingData(); // Start profiling
                //var sw = new Stopwatch();
                //sw.Start();


                int numExports = Pcc.ExportCount;
                //Import!
                var rop = new RelinkerOptionsPackage
                {
                    IsCrossGame = sourceEntry.Game != targetItem.Game && sourceEntry.Game != MEGame.UDK,
                    TargetGameDonorDB = objectDB,
                    ImportExportDependencies = portingOption.PortingOptionChosen is EntryImporter.PortingOption.CloneAllDependencies
                        or EntryImporter.PortingOption.ReplaceSingularWithRelink,
                    GenerateImportsForGlobalFiles = portingOption.PortGlobalsAsImports,
                    PortImportsMemorySafe = portingOption.PortExportsMemorySafe,
                    PortExportsAsImportsWhenPossible = portingOption.PortExportsAsImportsWhenPossible,
                };

                var relinkResults = EntryImporter.ImportAndRelinkEntries(portingOption.PortingOptionChosen, sourceEntry, Pcc,
                    targetLinkEntry, true, rop, out IEntry newEntry);

                if (originalIndex >= 0)
                {
                    //index was temporarily adjusted for porting. restore state
                    sourceEntry.indexValue = originalIndex;
                    sourceEntry.HeaderChanged = hadHeaderChanges;
                    sourceEntry.EntryHasPendingChanges = hadChanges;
                }

                TryAddToPersistentLevel(Pcc.Exports.Skip(numExports));

                //sw.Stop();
                //MessageBox.Show($"Took {sw.ElapsedMilliseconds}ms");
                //MeasureProfiler.SaveData(); // End profiling
                if ((relinkResults?.Count ?? 0) > 0)
                {
                    var ld = new ListDialog(relinkResults, "Relink report",
                        "The following items reported relinking issues.", this)
                    { DoubleClickEntryHandler = entryDoubleClick };
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


            string searchTerm = Search_TextBox.Text.Trim();

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
                    if (Pcc.Names[i].Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
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
                    if (Pcc.Imports[i].ObjectName.Name.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
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
                    if (Pcc.Exports[i].ObjectName.Name.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
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

                    if (node.Entry.ObjectName.Instanced.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        node.IsProgramaticallySelecting = true;
                        SelectedItem = node;
                        //node.IsSelected = true;
                        break;
                    }
                }
            }
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
            Settings.PackageEditor_TouchComfyMode = !Settings.PackageEditor_TouchComfyMode;
            TouchComfySettings.ModeSwitched();
        }

        private void ShowImpExpPrefix_Clicked(object sender, RoutedEventArgs e)
        {
            Settings.PackageEditor_ShowImpExpPrefix =
                !Settings.PackageEditor_ShowImpExpPrefix;
            if (Enumerable.Any(AllTreeViewNodesX))
            {
                AllTreeViewNodesX[0].FlattenTree().ForEach(x => x.RefreshDisplayName());
            }
        }


        private void PackageEditorWPF_Closing(object sender, CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                SoundTab_Soundpanel.FreeAudioResources();
                foreach (ExportLoaderControl el in ExportLoaders.Keys)
                {
                    el.Dispose(); //Remove hosted winforms references
                }

                LeftSideList_ItemsSource.ClearEx();
                ResetTreeView();
                RecentsController?.Dispose();
            }
        }

        private void ResetTreeView()
        {
            if (AllTreeViewNodesX.Count > 0)
            {
                foreach (TreeViewEntry tv in AllTreeViewNodesX[0].FlattenTree())
                {
                    tv.Dispose();
                }
            }
            AllTreeViewNodesX.ClearEx();
        }

        private void OpenIn_Clicked(object sender, RoutedEventArgs e)
        {
            var myValue = (string)((MenuItem)sender).Tag;
            switch (myValue)
            {
                case "SequenceEditor":
                    var seqEditor = new Sequence_Editor.SequenceEditorWPF();
                    seqEditor.LoadFile(Pcc.FilePath);
                    seqEditor.Show();
                    break;
                case "FaceFXEditor":
                    var facefxEditor = new FaceFXEditor.FaceFXEditorWindow();
                    facefxEditor.LoadFile(Pcc.FilePath);
                    facefxEditor.Show();
                    break;
                case "SoundplorerWPF":
                    var soundplorerWPF = new Soundplorer.SoundplorerWPF();
                    soundplorerWPF.LoadFile(Pcc.FilePath);
                    soundplorerWPF.Show();
                    break;
                case "DialogueEditor":
                    var dialogueEditorWPF = new DialogueEditorWindow();
                    dialogueEditorWPF.LoadFile(Pcc.FilePath);
                    dialogueEditorWPF.Show();
                    break;
                case "PathfindingEditor":
                    var pathEditor = new PathfindingEditor.PathfindingEditorWindow(Pcc);
                    pathEditor.Show();
                    break;
                case "Meshplorer":
                    var meshplorer = new MeshplorerWindow();
                    meshplorer.LoadFile(Pcc.FilePath);
                    meshplorer.Show();
                    break;

            }
        }

        private void HexConverterMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(AppDirectories.HexConverterPath))
            {
                Process.Start(AppDirectories.HexConverterPath);
            }
            else
            {
                new HexConverter.MainWindow().Show();
            }
        }

        private void BinaryInterpreterWPF_AlwaysAutoParse_Click(object sender, RoutedEventArgs e)
        {
            //BinaryInterpreterWPF_AlwaysAutoParse_MenuItem.IsChecked = !BinaryInterpreterWPF_AlwaysAutoParse_MenuItem.IsChecked;
            Settings.BinaryInterpreter_SkipAutoParseSizeCheck = !Settings.BinaryInterpreter_SkipAutoParseSizeCheck;
        }

        private void AssociatePCCSFM_Clicked(object sender, RoutedEventArgs e)
        {
            FileAssociations.AssociatePCCSFM();
        }

        private void AssociateUPKUDK_Clicked(object sender, RoutedEventArgs e)
        {
            FileAssociations.AssociateUPKUDK();
        }

        private void AssociateOtherFiles_Clicked(object sender, RoutedEventArgs e)
        {
            FileAssociations.AssociateOthers();
        }


        private void TLKManagerWPF_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            new TlkManagerNS.TLKManagerWPF().Show();
        }

        private void PropertyParsing_UnknownArrayAsObj_Click(object sender, RoutedEventArgs e)
        {
            Settings.Global_PropertyParsing_ParseUnknownArrayTypeAsObject =
                !Settings.Global_PropertyParsing_ParseUnknownArrayTypeAsObject;
        }

        private void MountEditor_Click(object sender, RoutedEventArgs e)
        {
            new MountEditor.MountEditorWindow().Show();
        }

        private void EmbeddedTextureViewer_AutoLoad_Click(object sender, RoutedEventArgs e)
        {
            Settings.TextureViewer_AutoLoadMip =
                !Settings.TextureViewer_AutoLoadMip;
        }

        private void InterpreterWPF_AdvancedMode_Click(object sender, RoutedEventArgs e)
        {
            Settings.Interpreter_AdvancedDisplay =
                !Settings.Interpreter_AdvancedDisplay;
        }



        private void InterpreterWPF_Colorize_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Settings.Interpreter_Colorize = !Settings.Interpreter_Colorize;
        }

        private void InterpreterWPF_ArrayPropertySizeLimit_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Settings.Interpreter_LimitArrayPropertySize =
                !Settings.Interpreter_LimitArrayPropertySize;
        }

        private void ShowExportIcons_Click(object sender, RoutedEventArgs e)
        {
            Settings.PackageEditor_ShowExportTypeIcons =
                !Settings.PackageEditor_ShowExportTypeIcons;

            // this triggers binding updates
            LeftSide_TreeView.DataContext = null;
            LeftSide_TreeView.DataContext = this;
        }

        private bool HasShaderCache() => PackageIsLoaded() && Pcc.Exports.Any(exp => exp.ClassName == "ShaderCache");

        private void CompactShaderCache()
        {
            IsBusy = true;
            BusyText = "Compacting local ShaderCaches";
            Task.Run(() => ShaderCacheManipulator.CompactSeekFreeShaderCaches(Pcc)).ContinueWithOnUIThread(_ => IsBusy = false);
        }

        private void InterpreterWPF_LinearColorWheel_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Settings.Interpreter_ShowLinearColorWheel =
                !Settings.Interpreter_ShowLinearColorWheel;
        }

        private void ShowExportMetadataInTree_Clicked(object sender, RoutedEventArgs e)
        {
            Settings.PackageEditor_ShowTreeEntrySubText =
                !Settings.PackageEditor_ShowTreeEntrySubText;
            if (AllTreeViewNodesX.Any)
            {
                foreach (TreeViewEntry tv in AllTreeViewNodesX[0].FlattenTree())
                {
                    tv.RefreshSubText();
                }
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

        public void LoadFileFromStream(Stream packageStream, string associatedFilePath, int goToIndex = 0, string goToEntry = null)
        {
            // Todo: Maybe prompt if there are pending changes to the current package?
            try
            {
                preloadPackage(Path.GetFileName(associatedFilePath), packageStream.Length);
                LoadMEPackage(packageStream, associatedFilePath);
                _selectedItem = null; // We change the backing data so we don't fire off a tree event since it checks if Pcc is null.
                if (goToIndex == 0 && !string.IsNullOrWhiteSpace(goToEntry))
                {
                    goToIndex = Pcc.FindEntry(goToEntry)?.UIndex ?? 0;
                }
                postloadPackage(associatedFilePath, goToIndex);

                // Loading from stream is not supported for saving or direct loading.
                // RecentsController.AddRecent(s, false, Pcc?.Game);
                // RecentsController.SaveRecentList(true);
            }
            catch (Exception e) when (!App.IsDebug)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + associatedFilePath;
                MessageBox.Show($"Error loading {associatedFilePath}:\n{e.Message}");
                IsBusy = false;
                IsBusyTaskbar = false;
                //throw e;
            }
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "PackageEditor";

    }
}