using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Collections.ObjectModel;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using Microsoft.Win32;
using AnimSequence = LegendaryExplorerCore.Unreal.BinaryConverters.AnimSequence;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.TLK;
using Microsoft.WindowsAPICodePack.Taskbar;
using BinaryPack;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.PlotDatabase;

namespace LegendaryExplorer.Tools.AssetDatabase
{
    /// <summary>
    /// Interaction logic for AssetDB
    /// </summary>
    public partial class AssetDatabaseWindow : TrackingNotifyPropertyChangedWindowBase
    {
        #region Declarations
        public const string dbCurrentBuild = "7.1"; //If changes are made that invalidate old databases edit this.
        private int previousView { get; set; }
        private int _currentView;
        public int currentView { get => _currentView; set { previousView = _currentView; SetProperty(ref _currentView, value); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        private string _busyText;
        public string BusyText { get => _busyText; set => SetProperty(ref _busyText, value); }
        private string _busyHeader;
        public string BusyHeader { get => _busyHeader; set => SetProperty(ref _busyHeader, value); }
        private bool _BusyBarInd;
        public bool BusyBarInd { get => _BusyBarInd; set => SetProperty(ref _BusyBarInd, value); }
        public MEGame currentGame;
        public MEGame CurrentGame
        {
            get => currentGame;
            set => SetProperty(ref currentGame, value);
        }

        private MELocalization _localization = MELocalization.INT;
        public MELocalization Localization { get => _localization; set => SetProperty(ref _localization, value); }

        public ObservableCollectionExtended<MELocalization> AvailableLocalizations { get; set; } = new()
        {
            MELocalization.INT,
            MELocalization.DEU,
            MELocalization.FRA,
            MELocalization.ITA,
            MELocalization.POL,
            MELocalization.RUS
        };

        private string CurrentDBPath { get; set; }
        public AssetDB CurrentDataBase { get; } = new();
        public ObservableCollectionExtended<FileDirPair> FileListExtended { get; } = new();

        private ClassRecord _selectedClass;
        public ClassRecord SelectedClass
        {
            get => _selectedClass;
            set
            {
                if (SetProperty(ref _selectedClass, value))
                {
                    UpdateSelectedClassUsages();
                }
            }
        }

        private ICollection<ClassUsage> _selectedClassUsages;
        public ICollection<ClassUsage> SelectedClassUsages
        {
            get => _selectedClassUsages;
            set => SetProperty(ref _selectedClassUsages, value);
        }

        private bool _showAllClassUsages;
        public bool ShowAllClassUsages
        {
            get => _showAllClassUsages;
            set
            {
                if (SetProperty(ref _showAllClassUsages, value))
                {
                    UpdateSelectedClassUsages();
                }
            }
        }

        public ObservableCollectionExtended<PlotUsage> SelectedPlotUsages { get; set; } = new();

        public record FileDirPair(string FileName, string Directory, int Mount);

        private ConcurrentAssetDB GeneratedDB = new();

        /// <summary>
        /// Items show in the list that are currently being processed
        /// </summary>
        public ObservableCollectionExtended<SingleFileScanner> CurrentDumpingItems { get; set; } = new();

        /// <summary>
        /// All items in the queue
        /// </summary>
        private List<SingleFileScanner> AllDumpingItems;

        private static BackgroundWorker dbworker = new();

        private ActionBlock<SingleFileScanner> ProcessingQueue;
        /// <summary>
        /// Cancelation of dumping
        /// </summary>
        private bool DumpCanceled;

        /// <summary>
        /// used to switch queue countdown on
        /// </summary>
        public bool isProcessing;
        private CancellationTokenSource cancelloading;
        private string _currentOverallOperationText;
        public string CurrentOverallOperationText
        {
            get => _currentOverallOperationText;
            set => SetProperty(ref _currentOverallOperationText, value);
        }
        private int _overallProgressValue;
        public int OverallProgressValue
        {
            get => _overallProgressValue;
            set
            {
                if (SetProperty(ref _overallProgressValue, value) && OverallProgressMaximum > 0)
                {
                    TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                    TaskbarHelper.SetProgress(value, OverallProgressMaximum);
                }
            }
        }

        private int _overallProgressMaximum;
        public int OverallProgressMaximum
        {
            get => _overallProgressMaximum;
            set => SetProperty(ref _overallProgressMaximum, value);
        }
        private IMEPackage meshPcc;
        private IMEPackage textPcc;
        private IMEPackage audioPcc;
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private bool _parseConvos;
        public bool ParseConvos { get => _parseConvos; set => SetProperty(ref _parseConvos, value); }
        private bool _parsePlotUsages;
        public bool ParsePlotUsages { get => _parsePlotUsages; set => SetProperty(ref _parsePlotUsages, value); }
        private BlockingCollection<ConvoLine> _linequeue = new();
        private Tuple<string, string, int, string, bool> _currentConvo = new(null, null, -1, null, false); //ConvoName, FileName, export, contentdir, isAmbient
        public Tuple<string, string, int, string, bool> CurrentConvo { get => _currentConvo; set => SetProperty(ref _currentConvo, value); }
        public ObservableCollectionExtended<string> SpeakerList { get; } = new();
        private bool _isGettingTLKs;
        public bool IsGettingTLKs { get => _isGettingTLKs; set => SetProperty(ref _isGettingTLKs, value); }
        public ObservableDictionary<int, string> CustomFileList { get; } = new(); //FileKey, filename<space>Dir
        public const string CustomListDesc = "Custom File Lists allow the database to be filtered so only assets that are in certain files or groups of files are shown. Lists can be saved/reloaded.";
        private bool _isFilteredByFiles;
        public bool IsFilteredByFiles { get => _isFilteredByFiles; set => SetProperty(ref _isFilteredByFiles, value); }
        public ICommand GenerateDBCommand { get; set; }
        public ICommand SaveDBCommand { get; set; }
        public ICommand SwitchMECommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }
        public ICommand OpenSourcePkgCommand { get; set; }
        public ICommand GoToSuperclassCommand { get; set; }
        public ICommand OpenUsagePkgCommand { get; set; }
        public ICommand OpenInAnimViewerCommand { get; set; }
        public ICommand ExportToPSACommand { get; set; }
        public ICommand OpenInAnimationImporterCommand { get; set; }
        public ICommand FilterClassCommand { get; set; }
        public ICommand FilterMatCommand { get; set; }
        public ICommand FilterMeshCommand { get; set; }
        public ICommand FilterTexCommand { get; set; }
        public ICommand FilterAnimsCommand { get; set; }
        public ICommand FilterVFXCommand { get; set; }
        public ICommand SetCRCCommand { get; set; }
        public ICommand FilterFilesCommand { get; set; }
        public ICommand LoadFileListCommand { get; set; }
        public ICommand SaveFileListCommand { get; set; }
        public ICommand EditFileListCommand { get; set; }
        public ICommand CopyToClipboardCommand { get; set; }
        public ICommand OpenInWindowsExplorerCommand { get; set; }
        public ICommand OpenInPlotDBCommand { get; set; }
        public ICommand OpenPEDefinitionCommand { get; set; }
        public ICommand ChangeLocalizationCommand { get; set; }
        private bool CanCancelDump(object obj)
        {
            return ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation && !DumpCanceled;
        }
        private bool IsClassSelected(object obj)
        {
            return lstbx_Classes.SelectedIndex >= 0 && currentView == 1;
        }
        private bool IsUsageSelected(object obj)
        {
            return (lstbx_Usages.SelectedIndex >= 0 && currentView == 1) || (lstbx_MatUsages.SelectedIndex >= 0 && currentView == 2) || (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 5)
                || (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3) || (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 6) || (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 4)
                || (lstbx_GUIUsages.SelectedIndex >= 0 && currentView == 7) || (lstbx_Lines.SelectedIndex >= 0 && currentView == 8) || (currentView == 9 && lstbx_PlotUsages.SelectedIndex >= 0)
                || (currentView == 0 && IsNotCND(lstbx_Files.SelectedItem));
        }

        private bool IsNotCND(object obj)
        {
            if (obj != null && obj is FileDirPair fdp)
            {
                return !fdp.FileName.EndsWith(".cnd", StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }
        private bool IsViewingClass(object obj)
        {
            return currentView == 1;
        }
        private bool IsViewingMaterials(object obj)
        {
            return currentView == 2;
        }
        private bool IsViewingMeshes(object obj)
        {
            return currentView == 3;
        }
        private bool IsViewingTextures(object obj)
        {
            return currentView == 4;
        }
        private bool IsViewingAnimations(object obj)
        {
            return currentView == 5;
        }
        private bool IsViewingVFX(object obj)
        {
            return currentView == 6;
        }
        private bool IsViewingPlotElements(object obj)
        {
            return currentView == 9;
        }
        private bool CanUseAnimViewer(object obj)
        {
            return currentView == 5 && CurrentGame == MEGame.ME3 && lstbx_Anims.SelectedIndex >= 0 && !((lstbx_Anims.SelectedItem as AnimationRecord)?.IsAmbPerf ?? true);
        }
        private bool IsAnimSequenceSelected() => currentView == 5 && lstbx_Anims.SelectedIndex >= 0 && !((lstbx_Anims.SelectedItem as AnimationRecord)?.IsAmbPerf ?? true);

        private bool IsPlotElementSelected() => GetSelectedPlotRecord() != null;

        #endregion

        #region Startup/Exit

        public AssetDatabaseWindow() : base("Asset Database", true)
        {
            LoadCommands();

            //Get default db / game
            CurrentDBPath = Settings.AssetDBPath;
            Enum.TryParse(Settings.AssetDBGame, out MEGame game);
            CurrentGame = game;

            InitializeComponent();

        }
        private void LoadCommands()
        {
            GenerateDBCommand = new GenericCommand(GenerateDatabase);
            SaveDBCommand = new GenericCommand(SaveDatabase);
            FilterClassCommand = new RelayCommand(SetFilters, IsViewingClass);
            FilterMatCommand = new RelayCommand(SetFilters, IsViewingMaterials);
            FilterMeshCommand = new RelayCommand(SetFilters, IsViewingMeshes);
            FilterTexCommand = new RelayCommand(SetFilters, IsViewingTextures);
            FilterAnimsCommand = new RelayCommand(SetFilters, IsViewingAnimations);
            FilterVFXCommand = new RelayCommand(SetFilters, IsViewingVFX);
            SwitchMECommand = new RelayCommand(SwitchGame);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
            OpenSourcePkgCommand = new RelayCommand(OpenSourcePkg, IsClassSelected);
            GoToSuperclassCommand = new RelayCommand(GoToSuperClass, IsClassSelected);
            OpenUsagePkgCommand = new RelayCommand(OpenUsagePkg, IsUsageSelected);
            SetCRCCommand = new RelayCommand(SetCRCScan);
            OpenInAnimViewerCommand = new RelayCommand(OpenInAnimViewer, CanUseAnimViewer);
            ExportToPSACommand = new GenericCommand(ExportToPSA, IsAnimSequenceSelected);
            OpenInAnimationImporterCommand = new GenericCommand(OpenInAnimationImporter, IsAnimSequenceSelected);
            FilterFilesCommand = new RelayCommand(SetFilters);
            LoadFileListCommand = new GenericCommand(LoadCustomFileList);
            SaveFileListCommand = new GenericCommand(SaveCustomFileList);
            EditFileListCommand = new RelayCommand(EditCustomFileList);
            CopyToClipboardCommand = new RelayCommand(CopyStringToClipboard);
            OpenInWindowsExplorerCommand = new RelayCommand(OpenFileInWindowsExplorer, IsUsageSelected);
            OpenInPlotDBCommand = new GenericCommand(OpenInPlotDB, IsPlotElementSelected);
            OpenPEDefinitionCommand = new GenericCommand(OpenPEDefinitionInToolset, IsPlotElementSelected);
            ChangeLocalizationCommand = new RelayCommand((e) => { Localization = (MELocalization)e; });
        }

        private void AssetDB_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentOverallOperationText = "Starting Up";
            BusyHeader = "Loading database";
            BusyText = "Please wait...";
            IsBusy = true;
            BusyBarInd = true;

            if (CurrentDBPath != null && CurrentDBPath.EndsWith("zip") && File.Exists(CurrentDBPath) && CurrentGame != MEGame.Unknown && CurrentGame != MEGame.UDK)
            {
                SwitchGame(CurrentGame.ToString());
            }
            else
            {
                CurrentDBPath = null;
                var gameDbToLoad = "ME3";
                if (Enum.TryParse<MEGame>(Settings.AssetDB_DefaultGame, out var game))
                {
                    gameDbToLoad = game.ToString();
                }
                SwitchGame(gameDbToLoad);
            }
            Activate();
        }
        private void AssetDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            Settings.AssetDBPath = CurrentDBPath;
            Settings.AssetDBGame = CurrentGame.ToString();

            MeshRendererTab_MeshRenderer?.Dispose();
            SoundpanelWPF_ADB?.Dispose();
            BIKExternalExportLoaderTab_BIKExternalExportLoader?.Dispose();
            EmbeddedTextureViewerTab_EmbeddedTextureViewer?.Dispose();

            audioPcc?.Dispose();
            meshPcc?.Dispose();
            textPcc?.Dispose();

            audioPcc = null;
            meshPcc = null;
            textPcc = null;

            dbworker.DoWork -= GetLineStrings;
            dbworker.RunWorkerCompleted -= dbworker_LineWorkCompleted;

            ClearDataBase();
        }

        #endregion

        #region Database I/O        
        /// <summary>
        /// Load the database or a particular database table.
        /// </summary>
        /// <param name="currentDbPath"></param>
        /// <param name="game"></param>
        /// <param name="database"></param>
        /// <param name="cancelloadingToken"></param>
        /// <param name="dbTable">Table parameter returns a database with only that table in it. Master = all.</param>
        /// <returns></returns>
        public static async Task LoadDatabase(string currentDbPath, MEGame game, AssetDB database, CancellationToken cancelloadingToken)
        {
            var build = dbCurrentBuild.Trim(' ', '*', '.');
            //Async load
            AssetDB pdb = await ParseDBAsync(game, currentDbPath, build, cancelloadingToken);
            database.meGame = pdb.meGame;
            database.GenerationDate = pdb.GenerationDate;
            database.DataBaseversion = pdb.DataBaseversion;
            database.Localization = pdb.Localization;
            database.FileList.AddRange(pdb.FileList);
            database.ContentDir.AddRange(pdb.ContentDir);
            database.AddRecords(pdb);
            database.PlotUsages.LoadPlotPaths(game);
        }
        public static async Task<AssetDB> ParseDBAsync(MEGame dbgame, string dbpath, string build, CancellationToken cancel)
        {
            var deserializingQueue = new BlockingCollection<AssetDB>();

            try
            {
                await Task.Run(() =>
                {
                    var archiveEntries = new Dictionary<string, ZipArchiveEntry>();
                    using ZipArchive archive = new(new FileStream(dbpath, FileMode.Open));
                    if (archive.Entries.Any(e => e.Name == $"MasterDB.{dbgame}_{build}.bin"))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            var ms = new MemoryStream();
                            using (Stream estream = entry.Open())
                            {
                                estream.CopyTo(ms);
                            }
                            ms.Position = 0;
                            Task.Run(() => JsonFileParse(ms, deserializingQueue, cancel));
                        }

                    }
                    else //Wrong build - send dummy pdb back and ask user to refresh
                    {
                        AssetDB pdb = new();
                        var entry = archive.Entries.FirstOrDefault(z => z.Name.StartsWith("Master"));
                        pdb.DataBaseversion = "pre 2.0";
                        if (entry != null)
                        {
                            var split = Path.GetFileNameWithoutExtension(entry.Name).Split('_');
                            if (split.Length == 2)
                            {
                                pdb.DataBaseversion = split[1];
                            }
                        }

                        deserializingQueue.Add(pdb);
                        if (!cancel.IsCancellationRequested)
                        {
                            deserializingQueue.CompleteAdding();
                        }
                    }
                });

                return await Task.Run(() =>
                {
                    AssetDB readData = null;
                    foreach (AssetDB pdb in deserializingQueue.GetConsumingEnumerable())
                    {
                        readData = pdb;
                        deserializingQueue.CompleteAdding();
                    }
                    return readData;
                });
            }
            catch (Exception e)
            {
                Console.WriteLine("Error ParseDB");
            }
            return null;
        }
        private static void JsonFileParse(MemoryStream ms, BlockingCollection<AssetDB> propsDataBases, CancellationToken ct)
        {

            try
            {
                AssetDB readData = BinaryConverter.Deserialize<AssetDB>(ms);
                if (ct.IsCancellationRequested)
                {
                    Console.WriteLine("Cancelled ParseDB");
                    return;
                }
                propsDataBases.Add(readData);
            }
            catch
            {
                MessageBox.Show($"Failure deserializing database");
            }
        }
        public async void SaveDatabase()
        {
            BusyHeader = "Saving database";
            BusyText = "Please wait...";
            BusyBarInd = true;
            IsBusy = true;
            CurrentOverallOperationText = "Database saving...";

            if (!ParseConvos && !CurrentGame.IsGame1())
            {
                CurrentDataBase.Lines.Clear();
            }

            await using (var fileStream = new FileStream(CurrentDBPath, FileMode.Create))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
                {
                    var build = dbCurrentBuild.Trim(' ', '*', '.');
                    var archiveEntry = archive.CreateEntry($"MasterDB.{CurrentGame}_{build}.bin");
                    await using (var entryStream = archiveEntry.Open())
                    {
                        await Task.Run(() => BinaryConverter.Serialize(CurrentDataBase, entryStream));
                    }
                }
            }
            menu_SaveXEmptyLines.IsEnabled = false;
            CurrentOverallOperationText = $"Database saved.";
            IsBusy = false;
            await Task.Delay(3000);
            CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count} Meshes: {CurrentDataBase.Meshes.Count} Particles: { CurrentDataBase.Particles.Count} Textures: { CurrentDataBase.Textures.Count} Elements: { CurrentDataBase.GUIElements.Count}";
        }
        public void ClearDataBase()
        {
            CurrentDataBase.Clear();
            CurrentDataBase.meGame = CurrentGame;
            CurrentDataBase.Localization = Localization;

            FileListExtended.ClearEx();
            CustomFileList.Clear();
            IsFilteredByFiles = false;
            expander_CustomFiles.IsExpanded = false;
            SpeakerList.ClearEx();
            FilterBox.Clear();
            Filter();
        }
        private void GetConvoLinesBackground()
        {
            if (CurrentGame.IsGame1())
            {
                var spkrs = new List<string>();
                foreach (var line in CurrentDataBase.Lines)
                {
                    if (spkrs.All(s => s != line.Speaker))
                        spkrs.Add(line.Speaker);
                }
                spkrs.Sort();
                SpeakerList.AddRange(spkrs);
                return;
            }
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Line worker getting Strings from TLK");
#endif
            IsGettingTLKs = true;
            GeneratedDB.GeneratedLines.Clear();
            _linequeue = new BlockingCollection<ConvoLine>();
            dbworker = new BackgroundWorker();
            dbworker.WorkerSupportsCancellation = true;
            dbworker.DoWork += GetLineStrings;
            dbworker.RunWorkerCompleted += dbworker_LineWorkCompleted;
            dbworker.RunWorkerAsync();

            foreach (var line in CurrentDataBase.Lines)
            {
                _linequeue.Add(line);
            }
            _linequeue.CompleteAdding();
        }
        private void dbworker_LineWorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            dbworker.CancelAsync();
            CommandManager.InvalidateRequerySuggested();
            var spkrs = new List<string>();
            foreach (var line in CurrentDataBase.Lines)
            {
                if (GeneratedDB.GeneratedLines.ContainsKey(line.StrRef.ToString()))
                {
                    line.Line = GeneratedDB.GeneratedLines[line.StrRef.ToString()].Line;
                }
                if (spkrs.All(s => s != line.Speaker))
                    spkrs.Add(line.Speaker);
            }
            var emptylines = CurrentDataBase.Lines.Where(l => l.Line == "No Data").ToList();
            foreach (var line in emptylines)
            {
                CurrentDataBase.Lines.Remove(line);
            }
            GeneratedDB.GeneratedLines.Clear();
            spkrs.Sort();
            SpeakerList.AddRange(spkrs);
            if (!emptylines.IsEmpty())
            {
                menu_SaveXEmptyLines.IsEnabled = true;
            }
            IsGettingTLKs = false;
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Line worker done");
#endif
        }
        private void GetLineStrings(object sender, DoWorkEventArgs e)
        {
            foreach (var ol in _linequeue.GetConsumingEnumerable(CancellationToken.None))
            {
                switch (CurrentGame)
                {
                    case MEGame.ME1:
                    case MEGame.LE1:
                        //Shouldn't be called in ME1/LE1
                        break;
                    case MEGame.ME2:
                        ol.Line = ME2TalkFiles.findDataById(ol.StrRef);
                        break;
                    case MEGame.ME3:
                        ol.Line = ME3TalkFiles.findDataById(ol.StrRef);
                        break;
                    case MEGame.LE2:
                        ol.Line = LE2TalkFiles.findDataById(ol.StrRef);
                        break;
                    case MEGame.LE3:
                        ol.Line = LE3TalkFiles.findDataById(ol.StrRef);
                        break;
                }
                GeneratedDB.GeneratedLines.TryAdd(ol.StrRef.ToString(), ol);
            }
        }

        #endregion

        #region UserCommands

        public void GenerateDatabase()
        {
            var shouldGenerate = MessageBox.Show($"Generate a new database for {CurrentGame}?", "Generating new DB",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
            if (shouldGenerate)
            {
                ScanGame();
            }
        }

        public void SwitchGame(object param)
        {
            var p = param as string;
            switchME1_menu.IsChecked = false;
            switchME2_menu.IsChecked = false;
            switchME3_menu.IsChecked = false;
            switchLE1_menu.IsChecked = false;
            switchLE2_menu.IsChecked = false;
            switchLE3_menu.IsChecked = false;
            ClearDataBase();
            currentView = 0;
            MeshRendererTab_MeshRenderer.UnloadExport();
            meshPcc?.Dispose();
            btn_MeshRenderToggle.IsChecked = false;
            btn_MeshRenderToggle.Content = "Toggle Mesh Rendering";
            EmbeddedTextureViewerTab_EmbeddedTextureViewer.UnloadExport();
            textPcc?.Dispose();
            btn_TextRenderToggle.IsChecked = false;
            btn_TextRenderToggle.Content = "Toggle Texture Rendering";
            SoundpanelWPF_ADB.UnloadExport();
            audioPcc?.Dispose();
            SoundpanelWPF_ADB.FreeAudioResources();
            btn_LinePlaybackToggle.IsChecked = false;
            btn_LinePlaybackToggle.Content = "Toggle Line Playback";
            menu_fltrPerf.IsEnabled = false;
            btn_LinePlaybackToggle.IsEnabled = true;
            tabCtrl_plotUsage.SelectedIndex = 0;
            SelectedPlotUsages.ClearEx();
            lstbx_PlotBool.SelectedIndex = -1;
            lstbx_PlotInt.SelectedIndex = -1;
            lstbx_PlotFloat.SelectedIndex = -1;
            lstbx_PlotTrans.SelectedIndex = -1;
            lstbx_PlotCond.SelectedIndex = -1;
            bool updateDefaultDB = CurrentGame != MEGame.Unknown;
            switch (p)
            {
                case "ME1":
                    CurrentGame = MEGame.ME1;
                    switchME1_menu.IsChecked = true;
                    btn_LinePlaybackToggle.IsEnabled = false;
                    break;
                case "ME2":
                    CurrentGame = MEGame.ME2;
                    switchME2_menu.IsChecked = true;
                    break;
                case "ME3":
                    CurrentGame = MEGame.ME3;
                    switchME3_menu.IsChecked = true;
                    menu_fltrPerf.IsEnabled = true;
                    break;
                case "LE1":
                    CurrentGame = MEGame.LE1;
                    switchLE1_menu.IsChecked = true;
                    btn_LinePlaybackToggle.IsEnabled = false;
                    break;
                case "LE2":
                    CurrentGame = MEGame.LE2;
                    switchLE2_menu.IsChecked = true;
                    break;
                case "LE3":
                    CurrentGame = MEGame.LE3;
                    switchLE3_menu.IsChecked = true;
                    menu_fltrPerf.IsEnabled = true;
                    break;
            }

            if (updateDefaultDB)
            {
                Settings.AssetDB_DefaultGame = CurrentGame.ToString();
            }
            CurrentDBPath = GetDBPath(CurrentGame);

            if (CurrentDBPath != null && File.Exists(CurrentDBPath))
            {
                CurrentOverallOperationText = "Loading database";
                BusyHeader = $"Loading database for {CurrentGame}";
                BusyText = "Please wait...";
                BusyBarInd = true;
                IsBusy = true;
                cancelloading?.Cancel();
                cancelloading = new CancellationTokenSource();
                var start = DateTime.UtcNow;
                LoadDatabase(CurrentDBPath, CurrentGame, CurrentDataBase, cancelloading.Token).ContinueWithOnUIThread(prevTask =>
                {
                    if (CurrentDataBase.DataBaseversion == null || CurrentDataBase.DataBaseversion != dbCurrentBuild)
                    {

                        var warn = MessageBox.Show($"This database is out of date (v {CurrentDataBase.DataBaseversion} versus v {dbCurrentBuild})\nA new version is required. Do you wish to rebuild?", "Warning", MessageBoxButton.OKCancel);
                        if (warn == MessageBoxResult.Cancel)
                        {
                            ClearDataBase();
                            IsBusy = false;
                        }
                        else
                        {
                            ScanGame();
                        }
                    }
                    else
                    {
                        var dlcs = MELoadedFiles.GetDLCNamesWithMounts(CurrentGame);
                        dlcs.Add("BioGame", 0);
                        foreach ((string fileName, int directoryKey) in CurrentDataBase.FileList)
                        {
                            var cd = CurrentDataBase.ContentDir[directoryKey];
                            int mount = -1;
                            dlcs.TryGetValue(cd, out mount);
                            FileListExtended.Add(new(fileName, cd, mount));
                        }

                        Localization = CurrentDataBase.Localization;
                        ParseConvos = !CurrentDataBase.Lines.IsEmpty();
                        ParsePlotUsages = CurrentDataBase.PlotUsages.Any();
                        IsBusy = false;
                        CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} " +
                                                      $"Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count} Meshes: {CurrentDataBase.Meshes.Count} " +
                                                      $"Particles: { CurrentDataBase.Particles.Count} Textures: { CurrentDataBase.Textures.Count} Elements: { CurrentDataBase.GUIElements.Count}";
                        if (ParseConvos)
                        {
                            CurrentOverallOperationText = CurrentOverallOperationText + $" Lines: { CurrentDataBase.Lines.Count}";
                        }
#if DEBUG
                        var end = DateTime.UtcNow;
                        double length = (end - start).TotalMilliseconds;
                        CurrentOverallOperationText = $"{CurrentOverallOperationText} LoadTime: {length}ms";
#endif

                        if (ParseConvos)
                        {
                            GetConvoLinesBackground();
                        }
                    }
                });

            }
            else
            {
                IsBusy = false;
                CurrentOverallOperationText = "No database found.";
            }
        }
        public static string GetDBPath(MEGame game)
        {
            return Path.Combine(AppDirectories.AppDataFolder, $"AssetDB{game}.zip");
        }

        private ListBoxScroll GetSelectedPlotListBox()
        {
            if (currentView == 9)
            {
                return tabCtrl_plotUsage.SelectedIndex switch
                {
                    0 => lstbx_PlotBool,
                    1 => lstbx_PlotInt,
                    2 => lstbx_PlotFloat,
                    3 => lstbx_PlotTrans,
                    4 => lstbx_PlotCond,
                    _ => null
                };
            }

            return null;
        }

        private PlotRecord GetSelectedPlotRecord()
        {
            var lstbx = GetSelectedPlotListBox();
            if (lstbx is { SelectedIndex: > -1 })
            {
                return (PlotRecord)lstbx.SelectedItem;
            }
            return null;
        }

        private List<PlotRecord> GetSelectedPlotSource()
        {
            if (currentView == 9 && CurrentDataBase.PlotUsages != null)
            {
                return tabCtrl_plotUsage.SelectedIndex switch
                {
                    0 => CurrentDataBase.PlotUsages.Bools,
                    1 => CurrentDataBase.PlotUsages.Ints,
                    2 => CurrentDataBase.PlotUsages.Floats,
                    3 => CurrentDataBase.PlotUsages.Transitions,
                    4 => CurrentDataBase.PlotUsages.Conditionals,
                    _ => null
                };
            }

            return null;
        }

        private void GoToSuperClass(object obj)
        {
            var cr = (ClassRecord)lstbx_Classes.SelectedItem;
            var sClass = cr.SuperClass;
            if (sClass == null)
            {
                MessageBox.Show("SuperClass unknown.");
                return;
            }
            if (FilterBox.Text != null)
            {
                FilterBox.Clear();
                Filter();
            }
            var scidx = CurrentDataBase.ClassRecords.IndexOf(CurrentDataBase.ClassRecords.FirstOrDefault(r => r.Class == sClass));
            if (scidx >= 0)
            {
                lstbx_Classes.SelectedIndex = scidx;
            }
            else
            {
                MessageBox.Show("SuperClass not found.");
            }

        }

        private (string, string, int, int) GetSelectedUsageInfo()
        {
            string usagepkg = null;
            string contentdir = null;
            int usagemount = 0;
            int usageUID = 0;
            if (lstbx_Usages.SelectedIndex >= 0 && currentView == 1)
            {
                var c = (ClassUsage)lstbx_Usages.SelectedItem;
                (usagepkg, contentdir, usagemount) = FileListExtended[c.FileKey];
                usageUID = c.UIndex;
            }
            else if (lstbx_MatUsages.SelectedIndex >= 0 && currentView == 2)
            {
                var m = (MatUsage)lstbx_MatUsages.SelectedItem;
                (usagepkg, contentdir, usagemount) = FileListExtended[m.FileKey];
                usageUID = m.UIndex;
            }
            else if (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3)
            {
                var s = (MeshUsage)lstbx_MeshUsages.SelectedItem;
                (usagepkg, contentdir, usagemount) = FileListExtended[s.FileKey];
                usageUID = s.UIndex;
            }
            else if (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 4)
            {
                var t = (TextureUsage)lstbx_TextureUsages.SelectedItem;
                (usagepkg, contentdir, usagemount) = FileListExtended[t.FileKey];
                usageUID = t.UIndex;
            }
            else if (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 5)
            {
                var a = (AnimUsage)lstbx_AnimUsages.SelectedItem;
                (usagepkg, contentdir, usagemount) = FileListExtended[a.FileKey];
                usageUID = a.UIndex;
            }
            else if (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 6)
            {
                var ps = (ParticleSysUsage)lstbx_PSUsages.SelectedItem;
                (usagepkg, contentdir, usagemount) = FileListExtended[ps.FileKey];
                usageUID = ps.UIndex;
            }
            else if (lstbx_GUIUsages.SelectedIndex >= 0 && currentView == 7)
            {
                var sf = (GUIUsage)lstbx_GUIUsages.SelectedItem;
                (usagepkg, contentdir, usagemount) = FileListExtended[sf.FileKey];
                usageUID = sf.UIndex;
            }
            else if (lstbx_Lines.SelectedIndex >= 0 && currentView == 8)
            {
                var lu = (ConvoLine)lstbx_Lines.SelectedItem;
                usagepkg = CurrentConvo.Item2;
                contentdir = CurrentConvo.Item4;
                usageUID = CurrentConvo.Item3;
            }
            else if (lstbx_PlotUsages.SelectedIndex >= 0 && currentView == 9)
            {
                var pu = (PlotUsage)lstbx_PlotUsages.SelectedItem;
                (usagepkg, contentdir, usagemount) = FileListExtended[pu.FileKey];
                usageUID = pu.UIndex;

            }
            else if (lstbx_Files.SelectedIndex >= 0 && currentView == 0)
            {
                (usagepkg, contentdir, usagemount) = (FileDirPair)lstbx_Files.SelectedItem;
            }

            return (usagepkg, contentdir, usagemount, usageUID);
        }

        private void OpenUsagePkg(object obj)
        {
            var tool = obj as string;
            string usagepkg = null;
            int usagemount = 0;
            int usageUID = 0;
            int strRef = 0;
            string contentdir = null;

            (usagepkg, contentdir, usagemount, usageUID) = GetSelectedUsageInfo();

            if (lstbx_Lines.SelectedIndex >= 0 && currentView == 8)
            {
                var lu = (ConvoLine)lstbx_Lines.SelectedItem;
                strRef = lu.StrRef;
            }
            else if (lstbx_PlotUsages.SelectedIndex >= 0 && currentView == 9)
            {
                var pu = (PlotUsage)lstbx_PlotUsages.SelectedItem;
                tool = pu.Context.ToTool();
                if (tool == "PlotEd")
                {
                    OpenInPlotEditor(GetFilePath(usagepkg, contentdir), pu);
                    return;
                }
                if (tool == "DlgEd" && pu.ContainerID.HasValue)
                {
                    strRef = pu.ContainerID.Value;
                }
            }

            if (usagepkg == null)
            {
                MessageBox.Show("File not found.");
                return;
            }

            OpenInToolkit(tool, GetFilePath(usagepkg, contentdir), usageUID, strRef);
        }
        private void OpenSourcePkg(object obj)
        {
            var cr = (ClassRecord)lstbx_Classes.SelectedItem;
            var sourcepkg = cr.DefinitionFile;
            var sourceexp = cr.Definition_UID;

            if (sourcepkg < 0)
            {
                MessageBox.Show("Definition file unknown.");
                return;
            }
            (string filename, string dir, _) = FileListExtended[sourcepkg];

            OpenInToolkit("PackageEditor", GetFilePath(filename, dir), sourceexp);
        }

        private string GetFilePath(string filename, string contentdir)
        {
            string filePath = null;
            string rootPath = MEDirectories.GetDefaultGamePath(CurrentGame);

            if (rootPath == null || !Directory.Exists(rootPath))
            {
                MessageBox.Show($"{CurrentGame} has not been found. Please check your Legendary Explorer settings");
                return null;
            }

            filePath = Directory.EnumerateFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir));

            if (filePath == null)
            {
                MessageBox.Show($"File {filename} not found in content directory {contentdir}.");
                return null;
            }

            return filePath;
        }
        private void OpenInToolkit(string tool, string filePath, int uindex = 0, int strRef = 0)
        {
            switch (tool)
            {
                case "Meshplorer":
                    var meshPlorer = new Meshplorer.MeshplorerWindow();
                    meshPlorer.Show();
                    if (uindex != 0)
                    {
                        meshPlorer.LoadFile(filePath, uindex);
                    }
                    else
                    {
                        meshPlorer.LoadFile(filePath);
                    }
                    break;
                case "PathEd":
                    var pathEd = new PathfindingEditor.PathfindingEditorWindow(filePath);
                    pathEd.Show();
                    break;
                case "DlgEd":
                    var diagEd = new DialogueEditor.DialogueEditorWindow();
                    diagEd.Show();
                    if (uindex != 0)
                    {
                        diagEd.LoadFile(filePath, uindex);
                        if (strRef != 0) diagEd.TrySelectStrRef(strRef);
                    }
                    else
                    {
                        diagEd.LoadFile(filePath);
                    }
                    break;
                case "SeqEd":
                    var SeqEd = new Sequence_Editor.SequenceEditorWPF();
                    SeqEd.Show();
                    if (uindex != 0)
                    {
                        SeqEd.LoadFile(filePath, uindex);
                    }
                    else
                    {
                        SeqEd.LoadFile(filePath);
                    }
                    break;
                case "SoundExplorer":
                    var soundplorer = new Soundplorer.SoundplorerWPF();
                    soundplorer.Show();
                    soundplorer.LoadFile(filePath);
                    break;
                case "CndEd":
                    var cndEd = new ConditionalsEditor.ConditionalsEditorWindow();
                    cndEd.Show();
                    if (uindex != 0)
                    {
                        cndEd.LoadFile(filePath, uindex);
                    }
                    else
                    {
                        cndEd.LoadFile(filePath);
                    }

                    break;
                default:
                    var packEditor = new PackageEditor.PackageEditorWindow();
                    packEditor.Show();
                    if (uindex != 0)
                    {
                        packEditor.LoadFile(filePath, uindex);
                    }
                    else
                    {
                        packEditor.LoadFile(filePath);
                    }
                    break;
            }
        }

        /// <summary>
        /// Open in Toolkit with some extra logic to go directly to a transition/quest/codex
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="usage"></param>
        private void OpenInPlotEditor(string filePath, PlotUsage usage)
        {
            var plotEditor = new PlotEditor.PlotEditorWindow();
            plotEditor.Show();
            plotEditor.LoadFile(filePath);
            if (usage.ContainerID.HasValue)
            {
                switch (usage.Context)
                {
                    case PlotUsageContext.Transition:
                        plotEditor.GoToStateEvent(usage.ContainerID.Value);
                        break;
                    case PlotUsageContext.Codex:
                        plotEditor.GoToCodex(usage.ContainerID.Value);
                        break;
                    case PlotUsageContext.Quest:
                        plotEditor.GoToQuest(usage.ContainerID.Value);
                        break;
                    case PlotUsageContext.BoolTaskEval:
                    case PlotUsageContext.IntTaskEval:
                    case PlotUsageContext.FloatTaskEval:
                    default:
                        break;
                }
            }
        }

        private void OpenFileInWindowsExplorer(object obj)
        {
            var (filename, contentDir, _, _) = GetSelectedUsageInfo();
            if (filename is null || contentDir is null) return;

            string filePath = GetFilePath(filename, contentDir);

            if (File.Exists(filePath))
            {
                string cmd = "explorer.exe";
                string arg = "/select, " + filePath;
                System.Diagnostics.Process.Start(cmd, arg);
            }
        }

        private void OpenInPlotDB()
        {
            var record = GetSelectedPlotRecord();
            var plotElement = PlotDatabases.FindPlotElementFromID(record.ElementID, record.ElementType.ToPlotElementType(),
                CurrentGame);
            var plotDB = new PlotManager.PlotManagerWindow();
            plotDB.Show();
            plotDB.SelectPlotElement(plotElement, CurrentGame.ToLEVersion());
        }

        private void OpenPEDefinitionInToolset()
        {
            var record = GetSelectedPlotRecord();

            if (record.ElementType is PlotRecordType.Conditional or PlotRecordType.Transition && record.BaseUsage != null)
            {
                (string usagepkg, string contentdir, int usagemount) = FileListExtended[record.BaseUsage.FileKey];
                int usageUID = record.BaseUsage.UIndex;
                if (record.BaseUsage.Context is PlotUsageContext.Conditional)
                {
                    OpenInToolkit("", GetFilePath(usagepkg, contentdir), usageUID);
                }
                else if (record.BaseUsage.Context is PlotUsageContext.Transition)
                {
                    OpenInPlotEditor(GetFilePath(usagepkg, contentdir), record.BaseUsage);
                }
                else if (record.BaseUsage.Context is PlotUsageContext.CndFile)
                {
                    // TODO
                }
            }
        }
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) //Fires if Tab moves away
        {
            e.Handled = true;

            if (currentView != previousView)
            {
                FilterBox.Clear();
                Filter();
                switch (currentView)
                {
                    case 2:
                        FilterBox.Watermark = "Search (by material name or parent package)";
                        break;
                    case 4:
                        FilterBox.Watermark = "Search (by texture name or CRC if compiled)";
                        break;
                    case 0:
                        FilterBox.Watermark = "Search (by filename or source directory)";
                        break;
                    default:
                        FilterBox.Watermark = "Search";
                        break;
                }

                if (previousView == 3)
                {
                    ToggleRenderMesh();
                    btn_MeshRenderToggle.IsChecked = false;
                    btn_MeshRenderToggle.Content = "Toggle Mesh Rendering";
                }

                if (previousView == 4)
                {
                    ToggleRenderTexture();
                    btn_TextRenderToggle.IsChecked = false;
                    btn_TextRenderToggle.Content = "Toggle Texture Rendering";
                }

                if (currentView == 0)
                {
                    menu_OpenUsage.Header = "Open File";
                }

                if (previousView == 0)
                {
                    menu_OpenUsage.Header = "Open Usage";
                }
                previousView = currentView;
            }
        }
        private void lstbx_Meshes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            if (currentView == 3 && lstbx_Meshes.SelectedIndex >= 0)
            {
                ToggleRenderMesh();
            }
        }
        private void lstbx_Textures_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            if (currentView == 4 && lstbx_Textures.SelectedIndex >= 0)
            {
                ToggleRenderTexture();
            }
        }
        private void lstbx_Lines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            if (currentView == 8 && lstbx_Lines.SelectedIndex >= 0)
            {
                var newline = (ConvoLine)lstbx_Lines.SelectedItem;
                var convo = CurrentDataBase.Conversations.FirstOrDefault(x => x.ConvName == newline.Convo);
                if (convo != null)
                {
                    (string fileName, int directoryKey) = CurrentDataBase.FileList[convo.ConvFile.File];
                    CurrentConvo = new Tuple<string, string, int, string, bool>(convo.ConvName, fileName, convo.ConvFile.ExportUIndex, CurrentDataBase.ContentDir[directoryKey], convo.IsAmbient);
                    ToggleLinePlayback();
                    return;
                }
            }
            CurrentConvo = new Tuple<string, string, int, string, bool>(null, null, 0, null, false);

        }
        private void PETabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            if (currentView == 9)
            {
                FilterBox.Clear();
                Filter();
            }
        }
        private void lstbx_PlotElement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            if (currentView == 9)
            {
                PlotRecord selectedRecord = GetSelectedPlotRecord();
                if (selectedRecord != null)
                {
                    SelectedPlotUsages.Clear();
                    SelectedPlotUsages.AddRange(selectedRecord.Usages);
                }
            }
        }
        private void btn_TextRenderToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleRenderTexture();
            if (btn_TextRenderToggle.IsChecked == true)
            {
                btn_TextRenderToggle.Content = "Untoggle Texture Rendering";
            }
            else
            {
                btn_TextRenderToggle.Content = "Toggle Texture Rendering";
            }
        }
        private void btn_MeshRenderToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleRenderMesh();
            if (btn_MeshRenderToggle.IsChecked == true)
            {
                btn_MeshRenderToggle.Content = "Untoggle Mesh Rendering";
            }
            else
            {
                btn_MeshRenderToggle.Content = "Toggle Mesh Rendering";
            }
        }
        private void btn_LinePlaybackToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleLinePlayback();
            if (btn_LinePlaybackToggle.IsChecked == true)
            {
                btn_LinePlaybackToggle.Content = "Untoggle Line Playback";
            }
            else
            {
                btn_LinePlaybackToggle.Content = "Toggle Line Playback";
            }
        }
        private void ToggleRenderMesh()
        {
            bool showmesh = btn_MeshRenderToggle.IsChecked == true && lstbx_Meshes.SelectedIndex >= 0 && CurrentDataBase.Meshes[lstbx_Meshes.SelectedIndex].Usages.Count > 0 && currentView == 3;

            if (!showmesh)
            {
                MeshRendererTab_MeshRenderer.UnloadExport();
                meshPcc?.Dispose();
                return;
            }
            string rootPath = MEDirectories.GetDefaultGamePath(CurrentGame);
            var selecteditem = (MeshRecord)lstbx_Meshes.SelectedItem;
            var filekey = selecteditem.Usages[0].FileKey;
            var (filename, dirKey) = CurrentDataBase.FileList[filekey];
            var cdir = CurrentDataBase.ContentDir[dirKey];

            if (rootPath == null)
            {
                MessageBox.Show($"{CurrentGame} has not been found. Please check your Legendary Explorer settings");
                return;
            }
            filename = $"{filename}.*";


            var files = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories);
            if (files.IsEmpty())
            {
                MessageBox.Show($"File {filename} not found.");
                return;
            }



            if (meshPcc != null) //unload existing file
            {
                MeshRendererTab_MeshRenderer.UnloadExport();
                meshPcc.Dispose();
            }

            foreach (var filePath in files)  //handle cases of mods/dlc having same file.
            {
                bool isBaseFile = cdir.ToLower() == "biogame";
                bool isDLCFile = filePath.ToLower().Contains("dlc");
                if (isBaseFile == isDLCFile)
                {
                    continue;
                }
                meshPcc = MEPackageHandler.OpenMEPackage(filePath);
                var uexpIdx = selecteditem.Usages[0].UIndex;
                if (uexpIdx <= meshPcc.ExportCount)
                {
                    var meshExp = meshPcc.GetUExport(uexpIdx);
                    if (meshExp.ObjectName == selecteditem.MeshName)
                    {
                        MeshRendererTab_MeshRenderer.LoadExport(meshExp);
                        break;
                    }
                }
                meshPcc.Dispose();
            }
        }
        private void ToggleRenderTexture()
        {
            bool showText = btn_TextRenderToggle.IsChecked == true && lstbx_Textures.SelectedIndex >= 0 && CurrentDataBase.Textures[lstbx_Textures.SelectedIndex].Usages.Count > 0 && currentView == 4;

            var selecteditem = (TextureRecord)lstbx_Textures.SelectedItem;
            if (!showText || selecteditem.CFormat == "TextureCube")
            {
                EmbeddedTextureViewerTab_EmbeddedTextureViewer.UnloadExport();
                BIKExternalExportLoaderTab_BIKExternalExportLoader.UnloadExport();
                EmbeddedTextureViewerTab_EmbeddedTextureViewer.Visibility = Visibility.Visible;
                BIKExternalExportLoaderTab_BIKExternalExportLoader.Visibility = Visibility.Collapsed;
                textPcc?.Dispose();
                return;
            }

            var filekey = selecteditem.Usages[0].FileKey;
            var (filename, dirKey) = CurrentDataBase.FileList[filekey];
            string rootPath = MEDirectories.GetDefaultGamePath(CurrentGame);
            var cdir = CurrentDataBase.ContentDir[dirKey];
            if (rootPath == null)
            {
                MessageBox.Show($"{CurrentGame} has not been found. Please check your Legendary Explorer settings");
                return;
            }

            filename = $"{filename}.*";
            var files = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).ToList();
            if (files.IsEmpty())
            {
                MessageBox.Show($"File {filename} not found.");
                return;
            }

            if (textPcc != null)
            {
                EmbeddedTextureViewerTab_EmbeddedTextureViewer.UnloadExport();
                BIKExternalExportLoaderTab_BIKExternalExportLoader.UnloadExport();
                textPcc.Dispose();
            }

            foreach (var filePath in files)  //handle cases of mods/dlc having same file.
            {
                bool isBaseFile = cdir.ToLower() == "biogame";
                bool isDLCFile = filePath.ToLower().Contains("dlc");
                if (isBaseFile == isDLCFile)
                {
                    continue;
                }
                textPcc = MEPackageHandler.OpenMEPackage(filePath);
                var uexpIdx = selecteditem.Usages[0].UIndex;
                if (uexpIdx <= textPcc.ExportCount)
                {
                    var textExp = textPcc.GetUExport(uexpIdx);
                    string cubemapParent = null;
                    if (textExp.Parent != null)
                    {
                        cubemapParent = textExp.Parent.ClassName == "CubeMap" ? selecteditem.TextureName.Substring(textExp.Parent.ObjectName.ToString().Length + 1) : null;
                    }
                    string indexedName = $"{textExp.ObjectNameString}_{textExp.indexValue - 1}";
                    if (textExp.ClassName.StartsWith("Texture") && (textExp.ObjectNameString == selecteditem.TextureName || selecteditem.TextureName == indexedName || (cubemapParent != null && textExp.ObjectNameString == cubemapParent)))
                    {
                        if (selecteditem.CFormat == "TextureMovie")
                        {
                            BIKExternalExportLoaderTab_BIKExternalExportLoader.LoadExport(textExp);
                            BIKExternalExportLoaderTab_BIKExternalExportLoader.Visibility = Visibility.Visible;
                            EmbeddedTextureViewerTab_EmbeddedTextureViewer.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            EmbeddedTextureViewerTab_EmbeddedTextureViewer.LoadExport(textExp);
                            EmbeddedTextureViewerTab_EmbeddedTextureViewer.Visibility = Visibility.Visible;
                            BIKExternalExportLoaderTab_BIKExternalExportLoader.Visibility = Visibility.Collapsed;
                        }
                        break;
                    }
                }
                textPcc.Dispose();
            }
        }
        private void ToggleLinePlayback()
        {
            bool showAudio = btn_LinePlaybackToggle.IsChecked == true && lstbx_Lines.SelectedIndex >= 0 && CurrentConvo.Item1 != null && !CurrentGame.IsGame1() && currentView == 8;

            if (!showAudio)
            {
                SoundpanelWPF_ADB.UnloadExport();
                audioPcc?.Dispose();
                return;
            }

            var selecteditem = (ConvoLine)lstbx_Lines.SelectedItem;
            var filename = CurrentConvo.Item2;
            var cdir = CurrentConvo.Item4;
            string rootPath = MEDirectories.GetDefaultGamePath(CurrentGame);
            if (rootPath == null)
            {
                MessageBox.Show($"{CurrentGame} has not been found. Please check your Legendary Explorer settings");
                return;
            }

            filename = $"{filename}.*";
            var files = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).ToList();
            if (files.IsEmpty())
            {
                MessageBox.Show($"File {filename} not found.");
                return;
            }

            string searchWav = $"{selecteditem.StrRef}_m";
            if (genderTabs.SelectedIndex == 1)
                searchWav = $"{selecteditem.StrRef}_f";

            if (audioPcc != null)
            {
                if (Path.GetFileNameWithoutExtension(audioPcc.FilePath) == CurrentConvo.Item2) //if switching gender file is already loaded
                {
                    var stream = audioPcc.Exports.FirstOrDefault(x => x.ClassName == "WwiseStream" && x.ObjectNameString.ToLower().Contains(searchWav));
                    if (stream != null)
                    {
                        SoundpanelWPF_ADB.LoadExport(stream);
                        return;
                    }
                }
                SoundpanelWPF_ADB.UnloadExport();
                audioPcc.Dispose();
            }

            foreach (var filePath in files)  //handle cases of mods/dlc having same file.
            {
                bool isBaseFile = cdir.ToLower() == "biogame";
                bool isDLCFile = filePath.ToLower().Contains("dlc");
                if (isBaseFile == isDLCFile)
                {
                    continue;
                }
                audioPcc = MEPackageHandler.OpenMEPackage(filePath);
                var stream = audioPcc.Exports.FirstOrDefault(x => x.ClassName == "WwiseStream" && x.ObjectNameString.ToLower().Contains(searchWav));
                if (stream != null)
                {
                    SoundpanelWPF_ADB.LoadExport(stream);
                    break;
                }
                audioPcc.Dispose();
            }
        }
        private void SetCRCScan(object obj)
        {
            if (menu_checkCRC.IsChecked)
            {
                menu_checkCRC.IsChecked = false;
            }
            else
            {
                var crcdlg = MessageBox.Show("Do you want to turn on CRC checking? This will significantly increase scan times.", "Asset Database", MessageBoxButton.YesNo);
                if (crcdlg == MessageBoxResult.Yes)
                {
                    menu_checkCRC.IsChecked = true;
                }
            }

        }
        private void OpenInAnimViewer(object obj)
        {
            if (lstbx_Anims.SelectedItem is AnimationRecord anim)
            {
                if (!Application.Current.Windows.OfType<AnimationViewer.AnimationViewerWindow>().Any())
                {
                    var av = new AnimationViewer.AnimationViewerWindow(CurrentDataBase, anim);
                    av.Show();
                }
                else
                {
                    var aexp = Application.Current.Windows.OfType<AnimationViewer.AnimationViewerWindow>().First();
                    if (aexp.ReadyToView)
                    {
                        aexp.LoadAnimation(anim);
                    }
                    else
                    {
                        aexp.AnimQueuedForFocus = anim;
                    }
                    aexp.Focus();
                }
            }
        }
        private void ExportToPSA()
        {
            if (lstbx_Anims.SelectedItem is AnimationRecord anim && anim.Usages.Any())
            {
                var (fileListIndex, animUIndex, _) = anim.Usages[0];
                string filePath = GetFilePath(fileListIndex);
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                if (pcc.IsUExport(animUIndex) &&
                    pcc.GetUExport(animUIndex) is ExportEntry animSeqExp &&
                    ObjectBinary.From(animSeqExp) is AnimSequence animSequence)
                {
                    var dlg = new SaveFileDialog
                    {
                        Filter = AnimationImporterExporter.AnimationImporterExporterWindow.PSAFilter,
                        FileName = $"{anim.SeqName}.psa",
                        AddExtension = true
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        PSA.CreateFrom(animSequence).ToFile(dlg.FileName);
                        MessageBox.Show("Done!", "PSA Export", MessageBoxButton.OK);
                    }
                }
            }
        }
        private void OpenInAnimationImporter()
        {
            if (lstbx_Anims.SelectedItem is AnimationRecord anim && anim.Usages.Any())
            {
                (int fileListIndex, int animUIndex, bool _) = anim.Usages[0];
                string filePath = GetFilePath(fileListIndex);
                var animImporter = new AnimationImporterExporter.AnimationImporterExporterWindow(filePath, animUIndex);
                animImporter.Show();
                animImporter.Activate();
            }
        }
        private string GetFilePath(int fileListIndex)
        {
            (string filename, string contentdir, int mount) = FileListExtended[fileListIndex];
            return Directory.GetFiles(MEDirectories.GetDefaultGamePath(CurrentGame), $"{filename}.*", SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir));
        }

        private void genderTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentView == 8 && (btn_LinePlaybackToggle.IsChecked ?? false))
            {
                ToggleLinePlayback(); ;
            }
        }
        private void CopyStringToClipboard(object obj)
        {
            if (!(obj is string cmd))
                return;
            Clipboard.Clear();
            string copytext = null;
            switch (cmd)
            {
                case "Line":
                    var line = (ConvoLine)lstbx_Lines.SelectedItem;
                    copytext = line.Line;
                    break;
                case "StrRef":
                    var lineref = (ConvoLine)lstbx_Lines.SelectedItem;
                    copytext = lineref.StrRef.ToString();
                    break;
                default:
                    break;
            }

            if (copytext == null)
                return;

            Clipboard.SetText(copytext);
        }

        #endregion

        #region Filters
        bool ClassFilter(object d)
        {
            if (d is ClassRecord cr)
            {
                bool showthis = true;
                if (!string.IsNullOrEmpty(FilterBox.Text))
                {
                    showthis = cr.Class.ToLower().Contains(FilterBox.Text.ToLower());
                }

                if (showthis && menu_fltrSeq.IsChecked && (!cr.Class.ToLower().StartsWith("seq") && !cr.Class.ToLower().StartsWith("bioseq") && !cr.Class.ToLower().StartsWith("sfxseq") && !cr.Class.ToLower().StartsWith("rvrseq")))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrInterp.IsChecked && (!cr.Class.ToLower().StartsWith("interp") && !cr.Class.ToLower().StartsWith("bioevtsys") && !cr.Class.ToLower().Contains("interptrack") && !cr.Class.ToLower().Contains("sfxscene")))
                {
                    showthis = false;
                }

                if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !cr.Usages.Select(c => c.FileKey).Intersect(CustomFileList.Keys).Any())
                {
                    showthis = false;
                }

                return showthis;
            }
            return false;
        }
        bool MaterialFilter(object d)
        {
            if (d is MaterialRecord mr)
            {
                bool showthis = true;
                if (!string.IsNullOrEmpty(FilterBox.Text))
                {
                    showthis = mr.MaterialName.ToLower().Contains(FilterBox.Text.ToLower());
                    if (!showthis)
                    {
                        showthis = mr.ParentPackage.ToLower().Contains(FilterBox.Text.ToLower());
                    }
                }

                if (showthis && menu_fltrMatDecal.IsChecked && !mr.MaterialName.Contains("Decal"))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatUnlit.IsChecked && !mr.MatSettings.Any(x => x.Name == "LightingModel" && x.Parm2 == "MLM_Unlit"))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatSkM.IsChecked && mr.MatSettings.Any(x => x.Name == "bUsedWithSkeletalMesh" && x.Parm2 == "True"))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMat2side.IsChecked && !mr.MatSettings.Any(x => x.Name == "TwoSided" && x.Parm2 == "True"))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMat1side.IsChecked && mr.MatSettings.Any(x => x.Name == "TwoSided" && x.Parm2 == "True"))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatNoDLC.IsChecked && mr.IsDLCOnly)
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatTrans.IsChecked && !mr.MatSettings.Any(x => x.Name == "BlendMode" && x.Parm2 == "BLEND_Translucent"))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatAdd.IsChecked && !mr.MatSettings.Any(x => x.Name == "BlendMode" && x.Parm2 == "BLEND_Additive"))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatOpq.IsChecked && mr.MatSettings.Any(x => x.Name == "BlendMode" && (x.Parm2 == "BLEND_Additive" || x.Parm2 == "BLEND_Translucent")))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatColor.IsChecked && !mr.MatSettings.Any(x => x.Name == "VectorParameter" && x.Parm1.ToLower().Contains("color")))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatText.IsChecked && mr.MatSettings.All(x => x.Name != "TextureSampleParameter2D"))
                {
                    showthis = false;
                }

                if (showthis && menu_fltrMatTalk.IsChecked && !mr.MatSettings.Any(x => x.Name == "ScalarParameter" && x.Parm1.ToLower().Contains("talk")))
                {
                    showthis = false;
                }

                if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !mr.Usages.Select(tuple => tuple.FileKey).Intersect(CustomFileList.Keys).Any())
                {
                    showthis = false;
                }

                return showthis;
            }

            return false;
        }
        bool MeshFilter(object d)
        {
            if (d is MeshRecord mr)
            {
                bool showthis = true;
                if (!string.IsNullOrEmpty(FilterBox.Text))
                {
                    if (mr.IsSkeleton && FilterBox.Text.ToLower().StartsWith("bones:") && FilterBox.Text.Length > 6 && int.TryParse(FilterBox.Text.Remove(0, 6).ToLower(), out int bonecount))
                    {
                        showthis = mr.BoneCount == bonecount;
                    }
                    else
                    {
                        showthis = mr.MeshName.ToLower().Contains(FilterBox.Text.ToLower());
                    }
                }

                if (showthis && menu_fltrSkM.IsChecked && !mr.IsSkeleton)
                {
                    showthis = false;
                }

                if (showthis && menu_fltrStM.IsChecked && mr.IsSkeleton)
                {
                    showthis = false;
                }

                if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !mr.Usages.Select(usage => usage.FileKey).Intersect(CustomFileList.Keys).Any())
                {
                    showthis = false;
                }

                return showthis;
            }

            return false;
        }
        bool AnimFilter(object d)
        {
            if (d is AnimationRecord ar)
            {
                bool showthis = true;
                if (!string.IsNullOrEmpty(FilterBox.Text) && ar != null)
                {
                    showthis = ar.AnimSequence.ToLower().Contains(FilterBox.Text.ToLower());
                }

                if (showthis && menu_fltrAnim.IsChecked && ar.IsAmbPerf)
                {
                    showthis = false;
                }

                if (showthis && menu_fltrPerf.IsChecked && !ar.IsAmbPerf)
                {
                    showthis = false;
                }

                if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !ar.Usages.Select(usage => usage.FileKey).Intersect(CustomFileList.Keys).Any())
                {
                    showthis = false;
                }

                return showthis;
            }

            return false;
        }
        bool PSFilter(object d)
        {
            if (d is ParticleSysRecord ps)
            {
                bool showthis = true;
                if (!string.IsNullOrEmpty(FilterBox.Text))
                {
                    showthis = ps.PSName.ToLower().Contains(FilterBox.Text.ToLower());
                }
                if (showthis && menu_VFXPartSys.IsChecked && ps.VFXType != ParticleSysRecord.VFXClass.ParticleSystem)
                {
                    showthis = false;
                }
                if (showthis && menu_VFXRvrEff.IsChecked && ps.VFXType == ParticleSysRecord.VFXClass.ParticleSystem)
                {
                    showthis = false;
                }
                if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !ps.Usages.Select(usage => usage.FileKey).Intersect(CustomFileList.Keys).Any())
                {
                    showthis = false;
                }
                return showthis;
            }

            return false;
        }
        bool TexFilter(object d)
        {
            if (d is TextureRecord tr)
            {
                bool showthis = true;
                if (!string.IsNullOrEmpty(FilterBox.Text))
                {
                    showthis = tr.TextureName.ToLower().Contains(FilterBox.Text.ToLower());
                    if (!showthis)
                    {
                        showthis = tr.CRC.ToLower().Contains(FilterBox.Text.ToLower());
                    }

                    if (!showthis)
                    {
                        showthis = tr.ParentPackage.ToLower().Contains(FilterBox.Text.ToLower());
                    }

                    if (!showthis && FilterBox.Text.ToLower().StartsWith("size: ") && FilterBox.Text.ToLower().Contains("x") && FilterBox.Text.Length > 6)
                    {
                        var sr = FilterBox.Text.Remove(0, 6).ToLower().Split("x");
                        if (int.TryParse(sr[0], out int xVal) && int.TryParse(sr[1], out int yVal))
                        {
                            showthis = tr.SizeX == xVal && tr.SizeY == yVal;
                        }
                    }
                }

                if (showthis && menu_TCube.IsChecked && tr.CFormat != "TextureCube")
                {
                    showthis = false;
                }

                if (showthis && menu_TMovie.IsChecked && tr.CFormat != "TextureMovie")
                {
                    showthis = false;
                }

                if (showthis && menu_T1024.IsChecked && tr.SizeX < 1024 && tr.SizeY < 1024)
                {
                    showthis = false;
                }

                if (showthis && menu_T4096.IsChecked && tr.SizeX < 4096 && tr.SizeY < 4096)
                {
                    showthis = false;
                }

                if (showthis && !menu_TGPromo.IsChecked && tr.TexGrp == "Promotional")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGChar1024.IsChecked && tr.TexGrp == "Character1024")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGCharDiff.IsChecked && tr.TexGrp == "CharacterDiff")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGCharNorm.IsChecked && tr.TexGrp == "CharacterNorm")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGCharSpec.IsChecked && tr.TexGrp == "CharacterSpec")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGWorld.IsChecked && tr.TexGrp == "World")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGWorldSpec.IsChecked && tr.TexGrp == "WorldSpecular")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGWorldNorm.IsChecked && tr.TexGrp == "WorldNormalMap")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGAmblgtMap.IsChecked && tr.TexGrp == "AmbientLightMap")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGShadowMap.IsChecked && tr.TexGrp == "Shadowmap")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGEnviro64.IsChecked && tr.TexGrp == "Environment64")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGEnviro128.IsChecked && tr.TexGrp == "Environment128")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGEnviro256.IsChecked && tr.TexGrp == "Environment256")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGEnviro512.IsChecked && tr.TexGrp == "Environment512")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGEnviro1024.IsChecked && tr.TexGrp == "Environment1024")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGVFX64.IsChecked && tr.TexGrp == "VFX64")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGVFX128.IsChecked && tr.TexGrp == "VFX128")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGVFX256.IsChecked && tr.TexGrp == "VFX256")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGVFX512.IsChecked && tr.TexGrp == "VFX512")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGVFX1024.IsChecked && tr.TexGrp == "VFX1024")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGAPL64.IsChecked && tr.TexGrp == "APL64")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGAPL128.IsChecked && tr.TexGrp == "APL128")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGAPL256.IsChecked && tr.TexGrp == "APL256")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGAPL512.IsChecked && tr.TexGrp == "APL512")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGAPL1024.IsChecked && tr.TexGrp == "APL1024")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGUI.IsChecked && tr.TexGrp == "UI")
                {
                    showthis = false;
                }

                if (showthis && !menu_TGNone.IsChecked && tr.TexGrp == "n/a")
                {
                    showthis = false;
                }

                if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !tr.Usages.Select(usage => usage.FileKey).Intersect(CustomFileList.Keys).Any())
                {
                    showthis = false;
                }

                return showthis;
            }

            return false;
        }
        bool SFFilter(object d)
        {
            if (d is GUIElement sf)
            {
                bool showthis = true;
                if (!string.IsNullOrEmpty(FilterBox.Text))
                {
                    showthis = sf.GUIName.ToLower().Contains(FilterBox.Text.ToLower());
                }
                if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !sf.Usages.Select(usage => usage.FileKey).Intersect(CustomFileList.Keys).Any())
                {
                    showthis = false;
                }
                return showthis;
            }
            return false;
        }
        bool LineFilter(object d)
        {
            if (d is ConvoLine line)
            {
                bool showthis = true;
                if (cmbbx_filterSpkrs.SelectedIndex >= 0)
                {
                    showthis = string.Equals(line.Speaker, cmbbx_filterSpkrs.SelectedItem.ToString(), StringComparison.CurrentCultureIgnoreCase);
                }
                if (showthis && !string.IsNullOrEmpty(FilterBox.Text))
                {
                    showthis = line.StrRef.ToString().Contains(FilterBox.Text.ToLower());
                    if (!showthis)
                    {
                        showthis = line.Convo.ToLower().Contains(FilterBox.Text.ToLower());
                    }
                    if (!showthis)
                    {
                        showthis = line.Line.ToLower().Contains(FilterBox.Text.ToLower());
                    }
                }

                return showthis;
            }

            return false;
        }
        private bool FileFilter(object d)
        {
            bool showthis = true;
            var f = (FileDirPair)d;
            var t = FilterBox.Text;
            if (!string.IsNullOrEmpty(t))
            {
                showthis = f.FileName.ToLower().Contains(t.ToLower());
                if (!showthis)
                {
                    showthis = f.Directory.ToLower().Contains(t.ToLower());
                }
            }
            return showthis;
        }

        private bool PEFilter(object d)
        {
            if (d is PlotRecord pr)
            {
                bool showthis = true;
                if (!string.IsNullOrEmpty(FilterBox.Text))
                {
                    showthis = pr.DisplayText.ToLower().Contains(FilterBox.Text.ToLower());
                }
                if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !pr.Usages.Select(usage => usage.FileKey).Intersect(CustomFileList.Keys).Any())
                {
                    showthis = false;
                }
                return showthis;
            }

            return false;
        }
        private void Filter()
        {
            switch (currentView)
            {
                case 1:  //Classes
                    ICollectionView viewC = CollectionViewSource.GetDefaultView(CurrentDataBase.ClassRecords);
                    viewC.Filter = ClassFilter;
                    lstbx_Classes.ItemsSource = viewC;
                    break;
                case 2: //Materials
                    ICollectionView viewM = CollectionViewSource.GetDefaultView(CurrentDataBase.Materials);
                    viewM.Filter = MaterialFilter;
                    lstbx_Materials.ItemsSource = viewM;
                    break;
                case 3: //Meshes
                    ICollectionView viewS = CollectionViewSource.GetDefaultView(CurrentDataBase.Meshes);
                    viewS.Filter = MeshFilter;
                    lstbx_Meshes.ItemsSource = viewS;
                    break;
                case 4: //Textures
                    ICollectionView viewT = CollectionViewSource.GetDefaultView(CurrentDataBase.Textures);
                    viewT.Filter = TexFilter;
                    lstbx_Textures.ItemsSource = viewT;
                    break;
                case 5: //Animations
                    ICollectionView viewA = CollectionViewSource.GetDefaultView(CurrentDataBase.Animations);
                    viewA.Filter = AnimFilter;
                    lstbx_Anims.ItemsSource = viewA;
                    break;
                case 6: //Particles
                    ICollectionView viewP = CollectionViewSource.GetDefaultView(CurrentDataBase.Particles);
                    viewP.Filter = PSFilter;
                    lstbx_Particles.ItemsSource = viewP;
                    break;
                case 7: //Scaleform
                    ICollectionView viewG = CollectionViewSource.GetDefaultView(CurrentDataBase.GUIElements);
                    viewG.Filter = SFFilter;
                    lstbx_Scaleform.ItemsSource = viewG;
                    break;
                case 8: //Lines
                    ICollectionView viewL = CollectionViewSource.GetDefaultView(CurrentDataBase.Lines);
                    viewL.Filter = LineFilter;
                    lstbx_Lines.ItemsSource = viewL;
                    break;
                case 9: // PlotElements
                    var lstbx = GetSelectedPlotListBox();
                    var plotSource = GetSelectedPlotSource();
                    if (plotSource is null || lstbx is null) break;
                    ICollectionView viewPE = CollectionViewSource.GetDefaultView(plotSource);
                    viewPE.Filter = PEFilter;
                    lstbx.ItemsSource = viewPE;
                    break;
                default: //Files
                    lstbx_Files.Items.Filter = FileFilter;
                    break;
            }
        }
        private void SetFilters(object obj)
        {
            var param = obj as string;
            switch (param)
            {
                case "Anim":
                    if (!menu_fltrAnim.IsChecked)
                    {
                        menu_fltrAnim.IsChecked = true;
                        menu_fltrPerf.IsChecked = false;
                    }
                    else
                    {
                        menu_fltrAnim.IsChecked = false;
                    }
                    break;
                case "Perf":
                    if (!menu_fltrPerf.IsChecked)
                    {
                        menu_fltrPerf.IsChecked = true;
                        menu_fltrAnim.IsChecked = false;
                    }
                    else
                    {
                        menu_fltrPerf.IsChecked = false;
                    }
                    break;
                case "Seq":
                    menu_fltrSeq.IsChecked = !menu_fltrSeq.IsChecked;
                    break;
                case "Interp":
                    menu_fltrInterp.IsChecked = !menu_fltrInterp.IsChecked;
                    break;
                case "Unlit":
                    menu_fltrMatUnlit.IsChecked = !menu_fltrMatUnlit.IsChecked;
                    break;
                case "SkM":
                    menu_fltrMatSkM.IsChecked = !menu_fltrMatSkM.IsChecked;
                    break;
                case "Twoside":
                    if (!menu_fltrMat2side.IsChecked)
                    {
                        menu_fltrMat2side.IsChecked = true;
                        menu_fltrMat1side.IsChecked = false;
                    }
                    else
                    {
                        menu_fltrMat2side.IsChecked = false;
                    }
                    break;
                case "Oneside":
                    if (!menu_fltrMat1side.IsChecked)
                    {
                        menu_fltrMat1side.IsChecked = true;
                        menu_fltrMat2side.IsChecked = false;
                    }
                    else
                    {
                        menu_fltrMat1side.IsChecked = false;
                    }
                    break;
                case "NoDLC":
                    menu_fltrMatNoDLC.IsChecked = !menu_fltrMatNoDLC.IsChecked;
                    break;
                case "Transl":
                    if (!menu_fltrMatTrans.IsChecked)
                    {
                        menu_fltrMatTrans.IsChecked = true;
                        menu_fltrMatAdd.IsChecked = false;
                        menu_fltrMatOpq.IsChecked = false;
                    }
                    else
                    {
                        menu_fltrMatTrans.IsChecked = false;
                    }
                    break;
                case "BAdd":
                    if (!menu_fltrMatAdd.IsChecked)
                    {
                        menu_fltrMatTrans.IsChecked = false;
                        menu_fltrMatAdd.IsChecked = true;
                        menu_fltrMatOpq.IsChecked = false;
                    }
                    else
                    {
                        menu_fltrMatAdd.IsChecked = false;
                    }
                    break;
                case "Opq":
                    if (!menu_fltrMatOpq.IsChecked)
                    {
                        menu_fltrMatTrans.IsChecked = false;
                        menu_fltrMatAdd.IsChecked = false;
                        menu_fltrMatOpq.IsChecked = true;
                    }
                    else
                    {
                        menu_fltrMatOpq.IsChecked = false;
                    }
                    break;
                case "Vcolor":
                    menu_fltrMatColor.IsChecked = !menu_fltrMatColor.IsChecked;
                    break;
                case "TextP":
                    menu_fltrMatText.IsChecked = !menu_fltrMatText.IsChecked;
                    break;
                case "TalkS":
                    menu_fltrMatTalk.IsChecked = !menu_fltrMatTalk.IsChecked;
                    break;
                case "Decal":
                    menu_fltrMatDecal.IsChecked = !menu_fltrMatDecal.IsChecked;
                    break;
                case "Skel":
                    if (!menu_fltrSkM.IsChecked)
                    {
                        menu_fltrSkM.IsChecked = true;
                        menu_fltrStM.IsChecked = false;
                    }
                    else
                    {
                        menu_fltrSkM.IsChecked = false;
                    }
                    break;
                case "Static":
                    if (!menu_fltrStM.IsChecked)
                    {
                        menu_fltrSkM.IsChecked = false;
                        menu_fltrStM.IsChecked = true;
                    }
                    else
                    {
                        menu_fltrStM.IsChecked = false;
                    }
                    break;
                case "Cube":
                    menu_TCube.IsChecked = !menu_TCube.IsChecked;
                    break;
                case "Movie":
                    menu_TMovie.IsChecked = !menu_TMovie.IsChecked;
                    break;
                case "1024":
                    if (!menu_T1024.IsChecked)
                    {
                        menu_T4096.IsChecked = false;
                        menu_T1024.IsChecked = true;
                    }
                    else
                    {
                        menu_T1024.IsChecked = false;
                    }
                    break;
                case "4096":
                    if (!menu_T4096.IsChecked)
                    {
                        menu_T1024.IsChecked = false;
                        menu_T4096.IsChecked = true;
                    }
                    else
                    {
                        menu_T4096.IsChecked = false;
                    }
                    break;
                case "TGShow":
                    menu_TGPromo.IsChecked = true;
                    menu_TGChar1024.IsChecked = true;
                    menu_TGCharDiff.IsChecked = true;
                    menu_TGCharNorm.IsChecked = true;
                    menu_TGCharSpec.IsChecked = true;
                    menu_TGWorld.IsChecked = true;
                    menu_TGWorldSpec.IsChecked = true;
                    menu_TGWorldNorm.IsChecked = true;
                    menu_TGAmblgtMap.IsChecked = true;
                    menu_TGShadowMap.IsChecked = true;
                    menu_TGEnviro64.IsChecked = true;
                    menu_TGEnviro128.IsChecked = true;
                    menu_TGEnviro256.IsChecked = true;
                    menu_TGEnviro512.IsChecked = true;
                    menu_TGEnviro1024.IsChecked = true;
                    menu_TGVFX64.IsChecked = true;
                    menu_TGVFX128.IsChecked = true;
                    menu_TGVFX256.IsChecked = true;
                    menu_TGVFX512.IsChecked = true;
                    menu_TGVFX1024.IsChecked = true;
                    menu_TGAPL64.IsChecked = true;
                    menu_TGAPL128.IsChecked = true;
                    menu_TGAPL256.IsChecked = true;
                    menu_TGAPL512.IsChecked = true;
                    menu_TGAPL1024.IsChecked = true;
                    menu_TGUI.IsChecked = true;
                    menu_TGNone.IsChecked = true;
                    break;
                case "TGClear":
                    menu_TGPromo.IsChecked = false;
                    menu_TGChar1024.IsChecked = false;
                    menu_TGCharDiff.IsChecked = false;
                    menu_TGCharNorm.IsChecked = false;
                    menu_TGCharSpec.IsChecked = false;
                    menu_TGWorld.IsChecked = false;
                    menu_TGWorldSpec.IsChecked = false;
                    menu_TGWorldNorm.IsChecked = false;
                    menu_TGAmblgtMap.IsChecked = false;
                    menu_TGShadowMap.IsChecked = false;
                    menu_TGEnviro64.IsChecked = false;
                    menu_TGEnviro128.IsChecked = false;
                    menu_TGEnviro256.IsChecked = false;
                    menu_TGEnviro512.IsChecked = false;
                    menu_TGEnviro1024.IsChecked = false;
                    menu_TGVFX64.IsChecked = false;
                    menu_TGVFX128.IsChecked = false;
                    menu_TGVFX256.IsChecked = false;
                    menu_TGVFX512.IsChecked = false;
                    menu_TGVFX1024.IsChecked = false;
                    menu_TGAPL64.IsChecked = false;
                    menu_TGAPL128.IsChecked = false;
                    menu_TGAPL256.IsChecked = false;
                    menu_TGAPL512.IsChecked = false;
                    menu_TGAPL1024.IsChecked = false;
                    menu_TGUI.IsChecked = false;
                    menu_TGNone.IsChecked = false;
                    break;
                case "PS":
                    if (!menu_VFXPartSys.IsChecked)
                    {
                        menu_VFXRvrEff.IsChecked = false;
                        menu_VFXPartSys.IsChecked = true;
                    }
                    else
                    {
                        menu_VFXPartSys.IsChecked = false;
                    }
                    break;
                case "RvrEff":
                    if (!menu_VFXRvrEff.IsChecked)
                    {
                        menu_VFXPartSys.IsChecked = false;
                        menu_VFXRvrEff.IsChecked = true;
                    }
                    else
                    {
                        menu_VFXRvrEff.IsChecked = false;
                    }
                    break;
                case "CustFiles":
                    if (IsFilteredByFiles)
                    {
                        btn_custFilter.Content = "Filtered";
                        expander_CustomFiles.IsExpanded = true;
                    }
                    else
                    {
                        btn_custFilter.Content = "Filter";
                        if (CustomFileList.IsEmpty())
                            expander_CustomFiles.IsExpanded = false;
                    }
                    break;
                default:
                    break;
            }
            Filter();
        }
        private void FilterBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (IsGettingTLKs && currentView == 8)
            {
                MessageBox.Show("Currently parsing TLK line data. Please wait.", "Asset Database", MessageBoxButton.OK);
                return;
            }
            Filter();
        }
        private void views_ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    ListSortDirection direction;
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    string primarySort;
                    string secondarySort;
                    switch (currentView)
                    {
                        case 0:
                            ICollectionView dataView = CollectionViewSource.GetDefaultView(lstbx_Files.ItemsSource);
                            primarySort = "Directory";
                            secondarySort = "FileName";
                            var header = headerClicked.Column.Header.ToString();
                            switch (header)
                            {
                                case "FileName":
                                    primarySort = "FileName";
                                    secondarySort = "Directory";
                                    break;
                                case "Mount":
                                    primarySort = "Mount";
                                    secondarySort = "FileName";
                                    break;
                            }

                            dataView.SortDescriptions.Clear();
                            dataView.SortDescriptions.Add(new SortDescription(primarySort, direction));
                            dataView.SortDescriptions.Add(new SortDescription(secondarySort, direction));
                            dataView.Refresh();
                            lstbx_Files.ItemsSource = dataView;
                            break;
                        case 8:
                            ICollectionView linedataView = CollectionViewSource.GetDefaultView(lstbx_Lines.ItemsSource);
                            primarySort = headerClicked.Column.Header.ToString();
                            linedataView.SortDescriptions.Clear();
                            linedataView.SortDescriptions.Add(new SortDescription(primarySort, direction));
                            linedataView.Refresh();
                            lstbx_Lines.ItemsSource = linedataView;
                            break;
                        default:
                            return;
                    }

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                    {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }
        private void cmbbx_filterSpkrs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            Filter();
        }
        private void SaveCustomFileList()
        {
            if (CustomFileList.IsEmpty())
            {
                MessageBox.Show("You cannot save an empty file list.", "Save File List", MessageBoxButton.OK);
                return;
            }

            string directory = Path.GetDirectoryName(CurrentDBPath);

            SaveFileDialog d = new()
            {
                Filter = $"*.txt|*.txt",
                InitialDirectory = directory,
                FileName = $"ADB_{CurrentGame}_*.txt",
                AddExtension = true
            };
            if (d.ShowDialog() == true)
            {
                TextWriter tw = new StreamWriter(d.FileName);
                foreach (KeyValuePair<int, string> file in CustomFileList)
                {
                    tw.WriteLine(file.Value);
                }
                tw.Close();
                MessageBox.Show("Done.");
            }
        }
        private void LoadCustomFileList()
        {
            string directory = Path.GetDirectoryName(CurrentDBPath);
            OpenFileDialog d = new()
            {
                Filter = $"*.txt|*.txt",
                InitialDirectory = directory,
                FileName = $"ADB_{CurrentGame}_*.txt",
                AddExtension = true

            };
            if (d.ShowDialog() == true)
            {
                TextReader tr = new StreamReader(d.FileName);
                string name = "";
                var nameslist = new List<string>();
                while ((name = tr.ReadLine()) != null)
                {
                    nameslist.Add(name);
                }

                var cdlg = MessageBox.Show($"Replace current list with these names:\n{string.Join("\n", nameslist)}", "Asset Database", MessageBoxButton.YesNo);
                if (cdlg == MessageBoxResult.No)
                    return;
                CustomFileList.Clear();
                var errorlist = new List<string>();
                foreach (var n in nameslist)
                {
                    string[] parts = n.Split(' ');
                    if (parts.Length >= 2)
                    {
                        var key = FileListExtended.IndexOf(new(parts[0], parts[1], int.Parse(parts[2])));
                        if (key >= 0)
                        {
                            CustomFileList.Add(key, n);
                            continue;
                        }
                    }
                    errorlist.Add(n);
                }

                if (!errorlist.IsEmpty())
                {
                    MessageBox.Show($"The following files are not in the {CurrentGame} database:\n{string.Join(", ", errorlist)}");
                }
            }
        }
        private void EditCustomFileList(object obj)
        {
            var action = obj as string;
            int FileKey = -1;
            switch (action)
            {
                case "Add":
                    if (lstbx_Usages.SelectedIndex >= 0 && currentView == 1)
                    {
                        var c = (ClassUsage)lstbx_Usages.SelectedItem;
                        FileKey = c.FileKey;
                    }
                    else if (lstbx_MatUsages.SelectedIndex >= 0 && currentView == 2)
                    {
                        var m = (MatUsage)lstbx_MatUsages.SelectedItem;
                        FileKey = m.FileKey;
                    }
                    else if (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3)
                    {
                        var s = (MeshUsage)lstbx_MeshUsages.SelectedItem;
                        FileKey = s.FileKey;

                    }
                    else if (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 4)
                    {
                        var t = (TextureUsage)lstbx_TextureUsages.SelectedItem;
                        FileKey = t.FileKey;
                    }
                    else if (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 5)
                    {
                        var a = (AnimUsage)lstbx_AnimUsages.SelectedItem;
                        FileKey = a.FileKey;

                    }
                    else if (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 6)
                    {
                        var ps = (ParticleSysUsage)lstbx_PSUsages.SelectedItem;
                        FileKey = ps.FileKey;
                    }
                    else if (lstbx_GUIUsages.SelectedIndex >= 0 && currentView == 7)
                    {
                        var sf = (GUIUsage)lstbx_GUIUsages.SelectedItem;
                        FileKey = sf.FileKey;
                    }
                    else if (lstbx_Lines.SelectedIndex >= 0 && currentView == 8)
                    {
                        FileKey = FileListExtended.FindIndex(f => f.FileName == CurrentConvo.Item2);
                    }
                    else if (currentView == 9 && lstbx_PlotUsages.SelectedIndex >= 0)
                    {
                        var pu = (PlotUsage)lstbx_PlotUsages.SelectedItem;
                        FileKey = pu.FileKey;
                    }
                    else if (lstbx_Files.SelectedIndex >= 0 && currentView == 0)
                    {
                        foreach (var fr in lstbx_Files.SelectedItems)
                        {
                            var fileref = (FileDirPair)fr;
                            FileKey = FileListExtended.IndexOf(fileref);
                            if (!CustomFileList.ContainsKey(FileKey))
                            {
                                var file = FileListExtended[FileKey];
                                CustomFileList.Add(FileKey, $"{file.FileName} {file.Directory}");
                            }
                        }
                        FileKey = -1;
                    }
                    if (!expander_CustomFiles.IsExpanded)
                        expander_CustomFiles.IsExpanded = true;
                    if (FileKey >= 0 && !CustomFileList.ContainsKey(FileKey))
                    {
                        var file = FileListExtended[FileKey];
                        CustomFileList.Add(FileKey, $"{file.FileName} {file.Directory}");
                    }
                    SortedDictionary<int, string> orderlist = new SortedDictionary<int, string>();
                    foreach (KeyValuePair<int, string> file in CustomFileList)
                    {
                        orderlist.Add(file.Key, file.Value);
                    }
                    CustomFileList.Clear();
                    CustomFileList.AddRange(orderlist);
                    break;
                case "Remove":
                    if (lstbx_CustomFiles.SelectedIndex >= 0 && currentView == 0)
                    {
                        var cf = (KeyValuePair<int, string>)lstbx_CustomFiles.SelectedItem;
                        FileKey = cf.Key;
                    }
                    if (FileKey >= 0 && CustomFileList.ContainsKey(FileKey))
                        CustomFileList.Remove(FileKey);
                    break;
                case "Clear":
                    CustomFileList.Clear();
                    break;
                default:
                    break;
            }

        }

        public void UpdateSelectedClassUsages()
        {
            if (ShowAllClassUsages)
            {
                SelectedClassUsages = SelectedClass?.Usages.OrderBy(u => u.FileKey).ToList();
            }
            else
            {
                SelectedClassUsages = SelectedClass?.Usages.OrderBy(u => u.FileKey).Aggregate(new List<ClassUsage>(), (list, usage) =>
                {
                    if (list.Count == 0 || usage.IsDefault || list[list.Count - 1].FileKey != usage.FileKey)
                    {
                        list.Add(usage);
                    }

                    return list;
                });
            }
        }
        #endregion

        #region Scan
        private async void ScanGame()
        {
            string rootPath = MEDirectories.GetDefaultGamePath(CurrentGame);

            if (rootPath == null || !Directory.Exists(rootPath))
            {
                MessageBox.Show($"{CurrentGame} has not been found. Please check your Legendary Explorer settings");
                return;
            }

            rootPath = Path.GetFullPath(rootPath);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc", ".cnd" };
            string ShaderCacheName = CurrentGame.IsLEGame() ? "RefShaderCache-PC-D3D-SM5.upk" : "RefShaderCache-PC-D3D-SM3.upk";
            List<string> files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
                                          .Where(s => supportedExtensions.Contains(Path.GetExtension(s.ToLower())) && !s.EndsWith(ShaderCacheName)).ToList();

            //MemoryManager.SetUsePooledMemory(true, blockSize: (int)FileSize.MebiByte, maxBufferSizeMB: 128);
            await dumpPackages(files, CurrentGame);
            MemoryManager.SetUsePooledMemory(false);
        }

        private async Task dumpPackages(List<string> files, MEGame game)
        {
            var beginTime = DateTime.Now;
            TopDock.IsEnabled = false;
            MidDock.IsEnabled = false;
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;
            BusyBarInd = false;
            CurrentOverallOperationText = $"Generating Database...";
            bool scanCRC = menu_checkCRC.IsChecked;

            //Clear database
            ClearDataBase();
            CurrentDataBase.GenerationDate = beginTime.ToString();
            CurrentDataBase.DataBaseversion = dbCurrentBuild;

            GeneratedDB.Clear();

            //Build filelists
            CurrentDataBase.ContentDir.Add("Unknown");
            var fileKeys = new List<(int, string)>();
            files = files.OrderBy(Path.GetFileName, StringComparer.InvariantCultureIgnoreCase).ToList();
            foreach (var f in files)
            {
                var contdir = GetContentPath(new DirectoryInfo(f));
                if (contdir == null)
                {
                    continue;
                }
                var dirkey = CurrentDataBase.ContentDir.IndexOf(contdir.Name);
                if (dirkey < 0)
                {
                    dirkey = CurrentDataBase.ContentDir.Count;
                    CurrentDataBase.ContentDir.Add(contdir.Name);
                }
                var filekey = CurrentDataBase.FileList.Count;
                CurrentDataBase.FileList.Add(new(Path.GetFileName(f), dirkey));
                fileKeys.Add((filekey, f));
            }

            //Shuffle filekeys randomly to avoid localizations concurrently accessing
            //int n = fileKeys.Count;
            //var rng = new Random();
            //while (n > 1)
            //{
            //    n--;
            //    int k = rng.Next(n + 1);
            //    var value = fileKeys[k];
            //    fileKeys[k] = fileKeys[n];
            //    fileKeys[n] = value;
            //}

            IsBusy = true;
            BusyHeader = $"Generating database for {CurrentGame}";
            ProcessingQueue = new ActionBlock<SingleFileScanner>(x =>
            {
                if (x.DumpCanceled)
                {
                    //OverallProgressValue++;
                    return;
                }
                Application.Current.Dispatcher.Invoke(() => CurrentDumpingItems.Add(x));
                x.DumpPackageFile(game, GeneratedDB); // What to do on each item
                Application.Current.Dispatcher.Invoke(() =>
                {
                    BusyText = $"Scanned {OverallProgressValue}/{OverallProgressMaximum} files\n\n{GeneratedDB.GetProgressString()}";
                    OverallProgressValue++; //Concurrency 
                    CurrentDumpingItems.Remove(x);
                });
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4) });

            AllDumpingItems = new List<SingleFileScanner>();
            CurrentDumpingItems.ClearEx();
            var scanOptions = new AssetDBScanOptions(scanCRC, ParseConvos, ParsePlotUsages, CurrentDataBase.Localization);
            foreach (var fkey in fileKeys)
            {
                var threadtask = new SingleFileScanner(fkey.Item2, fkey.Item1, scanOptions);
                AllDumpingItems.Add(threadtask); //For setting cancelation value
                ProcessingQueue.Post(threadtask); // Post all items to the block

            }

            Exception caughtException = null;
            try
            {
                ProcessingQueue.Complete(); // Signal completion
                CommandManager.InvalidateRequerySuggested();
                await ProcessingQueue.Completion;
                isProcessing = true;
            }
            catch (Exception e)
            {
                caughtException = e;
            }
            finally
            {


                if (DumpCanceled)
                {
                    DumpCanceled = false;
                    BusyHeader = "Dump canceled. ";
                }
                else
                {
                    OverallProgressValue = 100;
                    OverallProgressMaximum = 100;
                    BusyHeader = "Dump completed. ";
                }

                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
            }

            if (caughtException != null)
            {
                GeneratedDB.Clear();
                CurrentOverallOperationText = "Database generation failed";
                IsBusy = false;
                isProcessing = false;
                TopDock.IsEnabled = true;
                MidDock.IsEnabled = true;
                throw caughtException;
            }

            BusyHeader += "Collating and sorting the database";
            BusyText = "Please wait...";
            BusyBarInd = true;
            CommandManager.InvalidateRequerySuggested();

            AssetDB pdb = await Task.Run(GeneratedDB.CollateDataBase);
            //Add and sort Classes
            CurrentDataBase.AddRecords(pdb);

            var dlcs = MELoadedFiles.GetDLCNamesWithMounts(CurrentGame);
            dlcs.Add("BioGame", 0);
            foreach ((string fileName, int directoryKey) in CurrentDataBase.FileList)
            {
                var cd = CurrentDataBase.ContentDir[directoryKey];
                int mount = -1;
                dlcs.TryGetValue(cd, out mount);
                FileListExtended.Add(new(fileName, cd, mount));
            }

            GeneratedDB.Clear();
            isProcessing = false;
            SaveDatabase();
            TopDock.IsEnabled = true;
            MidDock.IsEnabled = true;
            IsBusy = false;
            var elapsed = DateTime.Now - beginTime;
            MessageBox.Show(this, $"{CurrentGame} Database generated in {elapsed:mm\\:ss}");
            MemoryAnalyzer.ForceFullGC(true);
            if (!CurrentGame.IsGame1() && ParseConvos)
            {
                GetConvoLinesBackground();
            }
            if (ParsePlotUsages)
            {
                CurrentDataBase.PlotUsages.LoadPlotPaths(game);
            }
        }

        private void CancelDump(object obj)
        {
            DumpCanceled = true;
            AllDumpingItems?.ForEach(x => x.DumpCanceled = true);
            CommandManager.InvalidateRequerySuggested(); //Refresh commands
        }

        private void CopyUsages_Click(object sender, RoutedEventArgs e)
        {
            string text = null;

            if (FileListExtended == null || !FileListExtended.Any())
                return; // Can't copy anything

            if (sender == CopyUsagesMaterials_Button && lstbx_Materials.SelectedItem is MaterialRecord matR)
            {
                text = string.Join("\n", matR.Usages.Select(x => FileListExtended[x.FileKey]?.FileName).Distinct());
            }
            else if (sender == CopyUsagesTextures_Button && lstbx_Textures.SelectedItem is TextureRecord tr)
            {
                text = string.Join("\n", tr.Usages.Select(x => FileListExtended[x.FileKey]?.FileName).Distinct());
            }
            else if (sender == CopyUsagesMeshes_Button && lstbx_Meshes.SelectedItem is MeshRecord mr)
            {
                text = string.Join("\n", mr.Usages.Select(x => FileListExtended[x.FileKey]?.FileName).Distinct());
            }
            else if (sender == CopyUsagesAnimations_Button && lstbx_Anims.SelectedItem is AnimationRecord animR)
            {
                text = string.Join("\n", animR.Usages.Select(x => FileListExtended[x.FileKey]?.FileName).Distinct());
            }
            else if (sender == CopyUsagesVFX_Button && lstbx_Particles.SelectedItem is ParticleSysRecord psysR)
            {
                text = string.Join("\n", psysR.Usages.Select(x => FileListExtended[x.FileKey]?.FileName).Distinct());
            }
            else if (sender == CopyUsagesGUI_Button && lstbx_Scaleform.SelectedItem is GUIElement ge)
            {
                text = string.Join("\n", ge.Usages.Select(x => FileListExtended[x.FileKey]?.FileName).Distinct());
            }
            
            if (text != null)
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error copying to clipboard", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private DirectoryInfo GetContentPath(DirectoryInfo directory)
        {
            if (directory == null)
            {
                return null;
            }
            var parent = directory.Parent;
            if (!directory.Name.StartsWith("Cooked"))
            {
                return GetContentPath(parent);
            }
            else
            {
                return parent;
            }

        }

        #endregion
    }

    public class FileIndexToNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            int fileindex = (int)values[0];
            var listofFiles = values[1] as ObservableCollectionExtended<AssetDatabaseWindow.FileDirPair>;
            if (listofFiles == null || fileindex < 0 || fileindex >= listofFiles.Count || listofFiles.Count == 0)
            {
                return "Error: file name not found";
            }
            var export = (int)values[2];
            (string fileName, string directory, int mount) = listofFiles[fileindex];
            return $"{fileName}  # {export}   {directory} ";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null; //not needed
        }
    }

}
