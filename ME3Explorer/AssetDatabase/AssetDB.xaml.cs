using ME3Explorer.Dialogue_Editor;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.SharedUI;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Gammtek.Collections.ObjectModel;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using Microsoft.Win32;
using AnimSequence = ME3ExplorerCore.Unreal.BinaryConverters.AnimSequence;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.ME1;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.Unreal.Classes;
using SkeletalMesh = ME3ExplorerCore.Unreal.BinaryConverters.SkeletalMesh;
using ME3ExplorerCore.TLK;
using MessagePack;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace ME3Explorer.AssetDatabase
{


    /// <summary>
    /// Interaction logic for AssetDB
    /// </summary>
    public partial class AssetDB : TrackingNotifyPropertyChangedWindowBase
    {
        #region Declarations
        public const string dbCurrentBuild = "5.0"; //If changes are made that invalidate old databases edit this.
        private int previousView { get; set; }
        private int _currentView;
        public int currentView { get => _currentView; set { previousView = _currentView; SetProperty(ref _currentView, value); } }
        public enum DBTableType
        {
            Master = 0,
            Class,
            Materials,
            Meshes,
            Textures,
            Particles,
            Animations,
            GUIElements,
            Convos,
            Lines
        }
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

        private string CurrentDBPath { get; set; }
        public PropsDataBase CurrentDataBase { get; } = new();
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

        public record FileDirPair(string FileName, string Directory);
        /// <summary>
        /// Dictionary that stores generated classes
        /// </summary>
        public ConcurrentDictionary<string, ClassRecord> GeneratedClasses = new();
        /// <summary>
        /// Dictionary that stores generated Animations
        /// </summary>
        public ConcurrentDictionary<string, AnimationRecord> GeneratedAnims = new();
        /// <summary>
        /// Dictionary that stores generated Materials
        /// </summary>
        public ConcurrentDictionary<string, MaterialRecord> GeneratedMats = new();
        /// <summary>
        /// Dictionary that stores generated Meshes
        /// </summary>
        public ConcurrentDictionary<string, MeshRecord> GeneratedMeshes = new();
        /// <summary>
        /// Dictionary that stores generated Particle Systems
        /// </summary>
        public ConcurrentDictionary<string, ParticleSysRecord> GeneratedPS = new();
        /// <summary>
        /// Dictionary that stores generated Textures
        /// </summary>
        public ConcurrentDictionary<string, TextureRecord> GeneratedText = new();
        /// <summary>
        /// Dictionary that stores generated GFXMovies
        /// </summary>
        public ConcurrentDictionary<string, GUIElement> GeneratedGUI = new();
        /// <summary>
        /// Dictionary that stores generated convos
        /// </summary>
        public ConcurrentDictionary<string, Conversation> GeneratedConvo = new();
        /// <summary>
        /// Dictionary that stores generated lines
        /// </summary>
        public ConcurrentDictionary<string, ConvoLine> GeneratedLines = new();
        /// <summary>
        /// Used to do per-class locking during generation
        /// </summary>
        public ConcurrentDictionary<string, object> ClassLocks = new();

        /// <summary>
        /// Items show in the list that are currently being processed
        /// </summary>
        public ObservableCollectionExtended<ClassScanSingleFileTask> CurrentDumpingItems { get; set; } = new();

        /// <summary>
        /// All items in the queue
        /// </summary>
        private List<ClassScanSingleFileTask> AllDumpingItems;

        private static BackgroundWorker dbworker = new();

        private ActionBlock<ClassScanSingleFileTask> ProcessingQueue;
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
                || (lstbx_GUIUsages.SelectedIndex >= 0 && currentView == 7) || (lstbx_Lines.SelectedIndex >= 0 && currentView == 8) || currentView == 0;
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
        private bool CanUseAnimViewer(object obj)
        {
            return currentView == 5 && CurrentGame == MEGame.ME3 && lstbx_Anims.SelectedIndex >= 0 && !((lstbx_Anims.SelectedItem as AnimationRecord)?.IsAmbPerf ?? true);
        }
        private bool IsAnimSequenceSelected() => currentView == 5 && lstbx_Anims.SelectedIndex >= 0 && !((lstbx_Anims.SelectedItem as AnimationRecord)?.IsAmbPerf ?? true);

        #endregion

        #region Startup/Exit

        public AssetDB() : base("Asset Database", true)
        {
            LoadCommands();

            //Get default db / gane
            CurrentDBPath = Properties.Settings.Default.AssetDBPath;
            Enum.TryParse(Properties.Settings.Default.AssetDBGame, out MEGame game);
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
                if (Enum.TryParse<MEGame>(Properties.Settings.Default.AssetDB_DefaultGame, out var game))
                {
                    gameDbToLoad = game.ToString();
                }
                SwitchGame(gameDbToLoad);
            }
            Activate();
        }
        private async void AssetDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            Properties.Settings.Default.AssetDBPath = CurrentDBPath;
            Properties.Settings.Default.AssetDBGame = CurrentGame.ToString();
            
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
        public static async Task LoadDatabase(string currentDbPath, MEGame game, PropsDataBase database, CancellationToken cancelloadingToken, DBTableType dbTable = DBTableType.Master)
        {
            var build = dbCurrentBuild.Trim(' ', '*', '.');
            ////Async load
            PropsDataBase pdb = await ParseDBAsync(game, currentDbPath, build, cancelloadingToken, dbTable);
            database.meGame = pdb.meGame;
            database.GenerationDate = pdb.GenerationDate;
            database.DataBaseversion = pdb.DataBaseversion;
            database.FileList.AddRange(pdb.FileList);
            database.ContentDir.AddRange(pdb.ContentDir);
            database.ClassRecords.AddRange(pdb.ClassRecords);
            database.Materials.AddRange(pdb.Materials);
            database.Animations.AddRange(pdb.Animations);
            database.Meshes.AddRange(pdb.Meshes);
            database.Particles.AddRange(pdb.Particles);
            database.Textures.AddRange(pdb.Textures);
            database.GUIElements.AddRange(pdb.GUIElements);
            database.Conversations.AddRange(pdb.Conversations);
            database.Lines.AddRange(pdb.Lines);
            foreach (var table in pdb.dbTable)
            {
                database.dbTable[table.Key] = table.Value;
            }
        }
        public static async Task<PropsDataBase> ParseDBAsync(MEGame dbgame, string dbpath, string build, CancellationToken cancel, DBTableType dbTable)
        {
            var deserializingQueue = new BlockingCollection<PropsDataBase>();
            var expectedtables = new ConcurrentDictionary<DBTableType, bool>();  //Stores which tables are expected to load
            var typology = Enum.GetValues(typeof(DBTableType)).Cast<DBTableType>().ToList();
            foreach (DBTableType type in typology)
            {
                bool expectedToLoad = !(type != DBTableType.Master && dbTable != DBTableType.Master && dbTable != type);
                expectedtables.TryAdd(type, expectedToLoad);
            }

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
                            DBTableType entryType = DBTableType.Master;
                            if (!entry.Name.StartsWith("Master"))
                            {
                                bool typecast = Enum.TryParse(entry.Name.Split('.')[0], out entryType);
                                if (!typecast || (dbTable != DBTableType.Master && dbTable != entryType))
                                    continue;
                            }

                            var ms = new MemoryStream();
                            using (Stream estream = entry.Open())
                            {
                                estream.CopyTo(ms);
                            }
                            ms.Position = 0;
                            Task.Run(() => JsonFileParse(ms, entryType, deserializingQueue, cancel));
                        }

                    }
                    else //Wrong build - send dummy pdb back and ask user to refresh
                    {
                        PropsDataBase pdb = new();
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
                    var readData = new PropsDataBase();
                    foreach (PropsDataBase pdb in deserializingQueue.GetConsumingEnumerable())
                    {
                        var readtable = pdb.dbTable.FirstOrDefault(t => t.Value == true);
                        readData.dbTable[readtable.Key] = true;
                        switch (readtable.Key)
                        {
                            case DBTableType.Class:
                                readData.ClassRecords.AddRange(pdb.ClassRecords);
                                break;
                            case DBTableType.Materials:
                                readData.Materials.AddRange(pdb.Materials);
                                break;
                            case DBTableType.Animations:
                                readData.Animations.AddRange(pdb.Animations);
                                break;
                            case DBTableType.Meshes:
                                readData.Meshes.AddRange(pdb.Meshes);
                                break;
                            case DBTableType.Particles:
                                readData.Particles.AddRange(pdb.Particles);
                                break;
                            case DBTableType.Textures:
                                readData.Textures.AddRange(pdb.Textures);
                                break;
                            case DBTableType.GUIElements:
                                readData.GUIElements.AddRange(pdb.GUIElements);
                                break;
                            case DBTableType.Convos:
                                readData.Conversations.AddRange(pdb.Conversations);
                                break;
                            case DBTableType.Lines:
                                readData.Lines.AddRange(pdb.Lines);
                                break;
                            default:
                                readData.meGame = pdb.meGame;
                                readData.GenerationDate = pdb.GenerationDate;
                                readData.DataBaseversion = pdb.DataBaseversion;
                                readData.FileList.AddRange(pdb.FileList);
                                readData.ContentDir.AddRange(pdb.ContentDir);
                                break;
                        }
                        bool alldone = true;
                        foreach (var tbl in expectedtables) //Check if loading is done
                        {
                            alldone = readData.dbTable[tbl.Key] == tbl.Value;
                            if (!alldone) { break; }
                        }
                        if (alldone) { deserializingQueue.CompleteAdding(); }
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
        private static void JsonFileParse(MemoryStream ms, DBTableType dbType, BlockingCollection<PropsDataBase> propsDataBases, CancellationToken ct)
        {

            PropsDataBase readData = new() { DataBaseversion = dbType.ToString() };
            readData.dbTable[dbType] = true;
            try
            {
                switch (dbType)
                {
                    case DBTableType.Master:
                        var mst = MessagePackSerializer.Deserialize<PropsDataBase>(ms);
                        readData.meGame = mst.meGame;
                        readData.GenerationDate = mst.GenerationDate;
                        readData.DataBaseversion = mst.DataBaseversion;
                        readData.FileList.AddRange(mst.FileList);
                        readData.ContentDir.AddRange(mst.ContentDir);
                        break;
                    case DBTableType.Class:
                        var cls = MessagePackSerializer.Deserialize<List<ClassRecord>>(ms);
                        readData.ClassRecords.AddRange(cls);
                        break;
                    case DBTableType.Materials:
                        var mats = MessagePackSerializer.Deserialize<List<MaterialRecord>>(ms);
                        readData.Materials.AddRange(mats);
                        break;
                    case DBTableType.Animations:
                        var an = MessagePackSerializer.Deserialize<List<AnimationRecord>>(ms);
                        readData.Animations.AddRange(an);
                        break;
                    case DBTableType.Meshes:
                        var msh = MessagePackSerializer.Deserialize<List<MeshRecord>>(ms);
                        readData.Meshes.AddRange(msh);
                        break;
                    case DBTableType.Particles:
                        var ps = MessagePackSerializer.Deserialize<List<ParticleSysRecord>>(ms);
                        readData.Particles.AddRange(ps);
                        break;
                    case DBTableType.Textures:
                        var txt = MessagePackSerializer.Deserialize<List<TextureRecord>>(ms);
                        readData.Textures.AddRange(txt);
                        break;
                    case DBTableType.GUIElements:
                        var gui = MessagePackSerializer.Deserialize<List<GUIElement>>(ms);
                        readData.GUIElements.AddRange(gui);
                        break;
                    case DBTableType.Convos:
                        var cnv = MessagePackSerializer.Deserialize<List<Conversation>>(ms);
                        readData.Conversations.AddRange(cnv);
                        break;
                    case DBTableType.Lines:
                        var line = MessagePackSerializer.Deserialize<List<ConvoLine>>(ms);
                        readData.Lines.AddRange(line);
                        break;
                }
                if (ct.IsCancellationRequested)
                {
                    Console.WriteLine("Cancelled ParseDB");
                    return;
                }
                propsDataBases.Add(readData);
            }
            catch
            {
                MessageBox.Show($"Failure deserializing type: {dbType}");
            }
        }
        public async void SaveDatabase()
        {
            BusyHeader = "Saving database";
            BusyText = "Please wait...";
            BusyBarInd = true;
            IsBusy = true;
            CurrentOverallOperationText = $"Database saving...";

            var masterDB = new PropsDataBase(CurrentDataBase.meGame, CurrentDataBase.GenerationDate, CurrentDataBase.DataBaseversion, CurrentDataBase.FileList, CurrentDataBase.ContentDir);
            var linesExLine = new ObservableCollectionExtended<ConvoLine>();
            if (ParseConvos && CurrentGame != MEGame.ME1)
            {
                foreach (var line in CurrentDataBase.Lines)
                {
                    linesExLine.Add(new ConvoLine(line.StrRef, line.Speaker, line.Convo));
                }
            }
            else if (CurrentGame == MEGame.ME1)
            {
                linesExLine.AddRange(CurrentDataBase.Lines);
            }

            using (var fileStream = new FileStream(CurrentDBPath, FileMode.Create))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
                {
                    var build = dbCurrentBuild.Trim(' ', '*', '.');
                    var masterjson = archive.CreateEntry($"MasterDB.{CurrentGame}_{build}.bin");
                    using (var entryStream = masterjson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, masterDB);
                    }
                    var classjson = archive.CreateEntry($"{DBTableType.Class}.DB{CurrentGame}.bin");
                    using (var entryStream = classjson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, CurrentDataBase.ClassRecords);
                    }
                    var matjson = archive.CreateEntry($"{DBTableType.Materials}.DB{CurrentGame}.bin");
                    using (var entryStream = matjson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, CurrentDataBase.Materials);
                    }
                    var animJson = archive.CreateEntry($"{DBTableType.Animations}.DB{CurrentGame}.bin");
                    using (var entryStream = animJson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, CurrentDataBase.Animations);
                    }
                    var mshJson = archive.CreateEntry($"{DBTableType.Meshes}.DB{CurrentGame}.bin");
                    using (var entryStream = mshJson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, CurrentDataBase.Meshes);
                    }
                    var psJson = archive.CreateEntry($"{DBTableType.Particles}.DB{CurrentGame}.bin");
                    using (var entryStream = psJson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, CurrentDataBase.Particles);
                    }
                    var txtJson = archive.CreateEntry($"{DBTableType.Textures}.DB{CurrentGame}.bin");
                    using (var entryStream = txtJson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, CurrentDataBase.Textures);
                    }
                    var guiJson = archive.CreateEntry($"{DBTableType.GUIElements}.DB{CurrentGame}.bin");
                    using (var entryStream = guiJson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, CurrentDataBase.GUIElements);
                    }
                    var convJson = archive.CreateEntry($"{DBTableType.Convos}.DB{CurrentGame}.bin");
                    using (var entryStream = convJson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, CurrentDataBase.Conversations);
                    }
                    var lineJson = archive.CreateEntry($"{DBTableType.Lines}.DB{CurrentGame}.bin");
                    using (var entryStream = lineJson.Open())
                    {
                        await MessagePackSerializer.SerializeAsync(entryStream, linesExLine);
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
            CurrentDataBase.meGame = CurrentGame;
            CurrentDataBase.GenerationDate = null;
            CurrentDataBase.FileList.Clear();
            CurrentDataBase.ContentDir.Clear();
            CurrentDataBase.ClassRecords.Clear();
            CurrentDataBase.Animations.Clear();
            CurrentDataBase.Materials.Clear();
            CurrentDataBase.Meshes.Clear();
            CurrentDataBase.Particles.Clear();
            CurrentDataBase.Textures.Clear();
            CurrentDataBase.GUIElements.Clear();
            CurrentDataBase.Conversations.Clear();
            CurrentDataBase.Lines.Clear();
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
            if (CurrentGame == MEGame.ME1)
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
            GeneratedLines.Clear();
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
                if (GeneratedLines.ContainsKey(line.StrRef.ToString()))
                {
                    line.Line = GeneratedLines[line.StrRef.ToString()].Line;
                }
                if (spkrs.All(s => s != line.Speaker))
                    spkrs.Add(line.Speaker);
            }
            var emptylines = CurrentDataBase.Lines.Where(l => l.Line == "No Data").ToList();
            foreach (var line in emptylines)
            {
                CurrentDataBase.Lines.Remove(line);
            }
            GeneratedLines.Clear();
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
                        //Shouldn't be called in ME1
                        break;
                    case MEGame.ME2:
                        ol.Line = ME2TalkFiles.findDataById(ol.StrRef);
                        break;
                    case MEGame.ME3:
                        ol.Line = ME3TalkFiles.findDataById(ol.StrRef);
                        break;
                }
                GeneratedLines.TryAdd(ol.StrRef.ToString(), ol);
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
                default:
                    CurrentGame = MEGame.ME3;
                    switchME3_menu.IsChecked = true;
                    menu_fltrPerf.IsEnabled = true;
                    break;
            }

            if (updateDefaultDB)
            {
                Properties.Settings.Default.AssetDB_DefaultGame = CurrentGame.ToString();
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
                        foreach ((string fileName, int directoryKey) in CurrentDataBase.FileList)
                        {
                            var cd = CurrentDataBase.ContentDir[directoryKey];
                            FileListExtended.Add(new(fileName, cd));
                        }

                        ParseConvos = !CurrentDataBase.Lines.IsEmpty();
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
            return Path.Combine(App.AppDataFolder, $"AssetDB{game}.zip");
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
        private void OpenUsagePkg(object obj)
        {
            var tool = obj as string;
            string usagepkg = null;
            int usageUID = 0;
            string contentdir = null;

            if (lstbx_Usages.SelectedIndex >= 0 && currentView == 1)
            {
                var c = (ClassUsage)lstbx_Usages.SelectedItem;
                (usagepkg, contentdir) = FileListExtended[c.FileKey];
                usageUID = c.UIndex;
            }
            else if (lstbx_MatUsages.SelectedIndex >= 0 && currentView == 2)
            {
                var m = (MatUsage)lstbx_MatUsages.SelectedItem;
                (usagepkg, contentdir)= FileListExtended[m.FileKey];
                usageUID = m.UIndex;
            }
            else if (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3)
            {
                var s = (MeshUsage)lstbx_MeshUsages.SelectedItem;
                (usagepkg, contentdir) = FileListExtended[s.FileKey];
                usageUID = s.UIndex;
            }
            else if (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 4)
            {
                var t = (TextureUsage)lstbx_TextureUsages.SelectedItem;
                (usagepkg, contentdir) = FileListExtended[t.FileKey];
                usageUID = t.UIndex;
            }
            else if (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 5)
            {
                var a = (AnimUsage)lstbx_AnimUsages.SelectedItem;
                (usagepkg, contentdir) = FileListExtended[a.FileKey];
                usageUID = a.UIndex;
            }
            else if (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 6)
            {
                var ps = (ParticleSysUsage)lstbx_PSUsages.SelectedItem;
                (usagepkg, contentdir) = FileListExtended[ps.FileKey];
                usageUID = ps.UIndex;
            }
            else if (lstbx_GUIUsages.SelectedIndex >= 0 && currentView == 7)
            {
                var sf = (GUIUsage)lstbx_GUIUsages.SelectedItem;
                (usagepkg, contentdir) = FileListExtended[sf.FileKey];
                usageUID = sf.UIndex;
            }
            else if (lstbx_Lines.SelectedIndex >= 0 && currentView == 8)
            {
                usagepkg = CurrentConvo.Item2;
                contentdir = CurrentConvo.Item4;
                usageUID = CurrentConvo.Item3;
            }
            else if (lstbx_Files.SelectedIndex >= 0 && currentView == 0)
            {
                (usagepkg, contentdir) = (FileDirPair)lstbx_Files.SelectedItem;
            }

            if (usagepkg == null)
            {
                MessageBox.Show("File not found.");
                return;
            }

            OpenInToolkit(tool, usagepkg, contentdir, usageUID);
        }
        private void OpenSourcePkg(object obj)
        {
            var cr = (ClassRecord)lstbx_Classes.SelectedItem;
            var sourcepkg = cr.Definition_package;
            var sourceexp = cr.Definition_UID;

            int sourcedefaultUsage = cr.Usages.FirstOrDefault(u => u.IsDefault)?.FileKey ?? 0;

            if (sourcepkg == null || sourcedefaultUsage == 0)
            {
                MessageBox.Show("Definition file unknown.");
                return;
            }
            var contentdir = FileListExtended[sourcedefaultUsage].Directory;

            OpenInToolkit("PackageEditor", sourcepkg, contentdir, sourceexp);
        }
        private void OpenInToolkit(string tool, string filename, string contentdir, int uindex = 0)
        {
            string filePath = null;
            string rootPath = MEDirectories.GetDefaultGamePath(CurrentGame);

            if (rootPath == null || !Directory.Exists(rootPath))
            {
                MessageBox.Show($"{CurrentGame} has not been found. Please check your ME3Explorer settings");
                return;
            }

            var supportedExtensions = new List<string> { ".pcc", ".u", ".upk", ".sfm" };
            filename = $"{filename}.*";
            filePath = Directory.EnumerateFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir) && supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            if (filePath == null)
            {
                MessageBox.Show($"File {filename} not found in content directory {contentdir}.");
                return;
            }

            switch (tool)
            {
                case "Meshplorer":
                    var meshPlorer = new MeshplorerWPF();
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
                    var pathEd = new PathfindingEditorWPF(filePath);
                    pathEd.Show();
                    break;
                case "DlgEd":
                    var diagEd = new DialogueEditorWPF();
                    diagEd.Show();
                    diagEd.LoadFile(filePath);
                    break;
                case "SeqEd":
                    var SeqEd = new SequenceEditorWPF();
                    SeqEd.Show();
                    SeqEd.LoadFile(filePath);
                    break;
                default:
                    var packEditor = new PackageEditorWPF();
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
                    (string fileName, int directoryKey) = CurrentDataBase.FileList[convo.ConvFile.Item1];
                    CurrentConvo = new Tuple<string, string, int, string, bool>(convo.ConvName, fileName, convo.ConvFile.Item2, CurrentDataBase.ContentDir[directoryKey], convo.IsAmbient);
                    ToggleLinePlayback();
                    return;
                }
            }
            CurrentConvo = new Tuple<string, string, int, string, bool>(null, null, 0, null, false);

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
                MessageBox.Show($"{CurrentGame} has not been found. Please check your ME3Explorer settings");
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
                MessageBox.Show($"{CurrentGame} has not been found. Please check your ME3Explorer settings");
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
            bool showAudio = btn_LinePlaybackToggle.IsChecked == true && lstbx_Lines.SelectedIndex >= 0 && CurrentConvo.Item1 != null && CurrentGame != MEGame.ME1 && currentView == 8;

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
                MessageBox.Show($"{CurrentGame} has not been found. Please check your ME3Explorer settings");
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
                if (!Application.Current.Windows.OfType<AnimationExplorer.AnimationViewer>().Any())
                {
                    AnimationExplorer.AnimationViewer av = new(CurrentDataBase, anim);
                    av.Show();
                }
                else
                {
                    var aexp = Application.Current.Windows.OfType<AnimationExplorer.AnimationViewer>().First();
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
                        Filter = AnimationImporter.PSAFilter,
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
                var animImporter = new AnimationImporter(filePath, animUIndex);
                animImporter.Show();
                animImporter.Activate();
            }
        }
        private string GetFilePath(int fileListIndex)
        {
            (string filename, string contentdir) = FileListExtended[fileListIndex];
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
                    showthis = mr.MeshName.ToLower().Contains(FilterBox.Text.ToLower());
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

                    string sortBy;
                    switch (currentView)
                    {
                        case 0:
                            ICollectionView dataView = CollectionViewSource.GetDefaultView(lstbx_Files.ItemsSource);
                            sortBy = "Item2";
                            if (headerClicked.Column.Header.ToString().StartsWith("File"))
                            {
                                sortBy = "Item1";
                            }

                            dataView.SortDescriptions.Clear();
                            dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
                            dataView.Refresh();
                            lstbx_Files.ItemsSource = dataView;
                            break;
                        case 8:
                            ICollectionView linedataView = CollectionViewSource.GetDefaultView(lstbx_Lines.ItemsSource);
                            sortBy = headerClicked.Column.Header.ToString();
                            linedataView.SortDescriptions.Clear();
                            linedataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
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
                        var key = FileListExtended.IndexOf(new(parts[0], parts[1]));
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
                SelectedClassUsages = SelectedClass.Usages.OrderBy(u => u.FileKey).ToList();
            }
            else
            {
                SelectedClassUsages = SelectedClass.Usages.OrderBy(u => u.FileKey).Aggregate(new List<ClassUsage>(), (list, usage) =>
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
                MessageBox.Show($"{CurrentGame} has not been found. Please check your ME3Explorer settings");
                return;
            }

            rootPath = Path.GetFullPath(rootPath);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc" };
            List<string> files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories).Where(s => supportedExtensions.Contains(Path.GetExtension(s.ToLower()))).ToList();

            await dumpPackages(files, CurrentGame);
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
            ClearGenerationDictionaries();


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
                CurrentDataBase.FileList.Add((Path.GetFileNameWithoutExtension(f), dirkey));
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
            ProcessingQueue = new ActionBlock<ClassScanSingleFileTask>(x =>
            {
                if (x.DumpCanceled)
                {
                    //OverallProgressValue++;
                    return;
                }
                Application.Current.Dispatcher.Invoke(() => CurrentDumpingItems.Add(x));
                x.dumpPackageFile(game, this); // What to do on each item
                Application.Current.Dispatcher.Invoke(() =>
                {
                    BusyText = $"Scanned {OverallProgressValue}/{OverallProgressMaximum} files\n\nClasses: { GeneratedClasses.Count}\nAnimations: { GeneratedAnims.Count}\nMaterials: { GeneratedMats.Count}\nMeshes: { GeneratedMeshes.Count}\n" +
                    $"Particles: { GeneratedPS.Count}\nTextures: { GeneratedText.Count}\nGUI Elements: { GeneratedGUI.Count}\nLines: {GeneratedLines.Count}";
                    OverallProgressValue++; //Concurrency 
                    CurrentDumpingItems.Remove(x);
                });
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = App.CoreCount }); // App.CoreCount

            AllDumpingItems = new List<ClassScanSingleFileTask>();
            CurrentDumpingItems.ClearEx();
            foreach (var fkey in fileKeys)
            {
                var threadtask = new ClassScanSingleFileTask(fkey.Item2, fkey.Item1, scanCRC, ParseConvos);
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
                ClearGenerationDictionaries();
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

            PropsDataBase pdb = await Task.Run(CollateDataBase);
            //Add and sort Classes
            CurrentDataBase.ClassRecords.AddRange(pdb.ClassRecords);
            CurrentDataBase.Animations.AddRange(pdb.Animations);
            CurrentDataBase.Materials.AddRange(pdb.Materials);
            CurrentDataBase.Meshes.AddRange(pdb.Meshes);
            CurrentDataBase.Particles.AddRange(pdb.Particles);
            CurrentDataBase.Textures.AddRange(pdb.Textures);
            CurrentDataBase.GUIElements.AddRange(pdb.GUIElements);
            CurrentDataBase.Conversations.AddRange(pdb.Conversations);
            CurrentDataBase.Lines.AddRange(pdb.Lines);

            foreach ((string fileName, int directoryKey) in CurrentDataBase.FileList)
            {
                var cd = CurrentDataBase.ContentDir[directoryKey];
                FileListExtended.Add(new(fileName, cd));
            }

            ClearGenerationDictionaries();
            isProcessing = false;
            SaveDatabase();
            TopDock.IsEnabled = true;
            MidDock.IsEnabled = true;
            IsBusy = false;
            var elapsed = DateTime.Now - beginTime;
            MessageBox.Show(this, $"{CurrentGame} Database generated in {elapsed:mm\\:ss}");
            MemoryAnalyzer.ForceFullGC();
            if (CurrentGame != MEGame.ME1 && ParseConvos)
            {
                GetConvoLinesBackground();
            }
        }

        private void CancelDump(object obj)
        {
            DumpCanceled = true;
            AllDumpingItems?.ForEach(x => x.DumpCanceled = true);
            CommandManager.InvalidateRequerySuggested(); //Refresh commands
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
        private void ClearGenerationDictionaries()
        {
            GeneratedClasses.Clear();
            GeneratedAnims.Clear();
            GeneratedMats.Clear();
            GeneratedMeshes.Clear();
            GeneratedPS.Clear();
            GeneratedText.Clear();
            GeneratedGUI.Clear();
            GeneratedConvo.Clear();
            GeneratedLines.Clear();
        }
        private PropsDataBase CollateDataBase()
        {
            var pdb = new PropsDataBase();
            pdb.ClassRecords.AddRange(GeneratedClasses.Values.OrderBy(x => x.Class));
            pdb.Conversations.AddRange(GeneratedConvo.Values);

            var animsSorted = GeneratedAnims.Values.OrderBy(x => x.AnimSequence).ToList();
            foreach (AnimationRecord anim in animsSorted)
            {
                anim.IsModOnly = anim.Usages.All(u => u.IsInMod);
            }
            pdb.Animations.AddRange(animsSorted);

            var matsSorted = GeneratedMats.Values.OrderBy(x => x.MaterialName).ToList();
            foreach (MaterialRecord mat in matsSorted)
            {
                mat.IsDLCOnly = mat.Usages.All(m => m.IsInDLC);
            }
            pdb.Materials.AddRange(matsSorted);

            var meshesSorted = GeneratedMeshes.Values.OrderBy(x => x.MeshName).ToList();
            foreach (MeshRecord meshRecord in meshesSorted)
            {
                meshRecord.IsModOnly = meshRecord.Usages.All(m => m.IsInMod);
            }
            pdb.Meshes.AddRange(meshesSorted);

            var particleSysSorted = GeneratedPS.Values.OrderBy(x => x.PSName).ToList();
            foreach (ParticleSysRecord particleSysRecord in particleSysSorted)
            {
                particleSysRecord.IsModOnly = particleSysRecord.Usages.All(p => p.IsInMod);
                particleSysRecord.IsDLCOnly = particleSysRecord.Usages.All(p => p.IsInDLC);
            }
            pdb.Particles.AddRange(particleSysSorted);

            var texSorted = GeneratedText.Values.OrderBy(x => x.TextureName).ToList();
            foreach (TextureRecord tex in texSorted)
            {
                tex.IsModOnly = tex.Usages.All(t => t.IsInMod);
                tex.IsDLCOnly = tex.Usages.All(t => t.IsInDLC);
            }
            pdb.Textures.AddRange(texSorted);

            var guisSorted = GeneratedGUI.Values.OrderBy(x => x.GUIName).ToList();
            foreach (GUIElement gui in guisSorted)
            {
                gui.IsModOnly = gui.Usages.All(g => g.IsInMod);
            }
            pdb.GUIElements.AddRange(guisSorted);

            pdb.Lines.AddRange(GeneratedLines.Values.OrderBy(x => x.StrRef).ToList());
            return pdb;
        }

        #endregion
    }

    #region Database
    /// <summary>
    /// Database Classes
    /// </summary>
    /// 
    [MessagePackObject]
    public class PropsDataBase
    {
        [Key(0)]
        public MEGame meGame { get; set; }
        [Key(1)]
        public string GenerationDate { get; set; }
        [Key(2)]
        public string DataBaseversion { get; set; }
        [Key(3)]
        public Dictionary<AssetDB.DBTableType, bool> dbTable { get; set; } = new()
        {
            { AssetDB.DBTableType.Master, false },
            { AssetDB.DBTableType.Class, false },
            { AssetDB.DBTableType.Materials, false },
            { AssetDB.DBTableType.Meshes, false },
            { AssetDB.DBTableType.Textures, false },
            { AssetDB.DBTableType.Animations, false },
            { AssetDB.DBTableType.Particles, false },
            { AssetDB.DBTableType.GUIElements, false },
            { AssetDB.DBTableType.Convos, false },
            { AssetDB.DBTableType.Lines, false }
        };
        [Key(4)]
        public List<(string FileName, int DirectoryKey)> FileList { get; set; } = new(); //filename and key to contentdir
        [Key(5)]
        public List<string> ContentDir { get; set; } = new();
        [IgnoreMember]
        public List<ClassRecord> ClassRecords { get; } = new();
        [IgnoreMember]
        public List<MaterialRecord> Materials { get; } = new();
        [IgnoreMember]
        public List<AnimationRecord> Animations { get; } = new();
        [IgnoreMember]
        public List<MeshRecord> Meshes { get; } = new();
        [IgnoreMember]
        public List<ParticleSysRecord> Particles { get; } = new();
        [IgnoreMember]
        public List<TextureRecord> Textures { get; } = new();
        [IgnoreMember]
        public List<GUIElement> GUIElements { get; } = new();
        [IgnoreMember]
        public List<Conversation> Conversations { get; } = new();
        [IgnoreMember]
        public List<ConvoLine> Lines { get; } = new();
        public PropsDataBase(MEGame meGame, string GenerationDate, string DataBaseversion, IEnumerable<(string, int)> FileList, IEnumerable<string> ContentDir)
        {
            this.meGame = meGame;
            this.GenerationDate = GenerationDate;
            this.DataBaseversion = DataBaseversion;
            this.FileList.AddRange(FileList);
            this.ContentDir.AddRange(ContentDir);
        }

        public PropsDataBase()
        { }

    }

    [MessagePackObject]
    public class ClassRecord
    {
        [Key(0)]
        public string Class { get; set; }
        [Key(1)]
        public string Definition_package { get; set; }
        [Key(2)]
        public int Definition_UID { get; set; }
        [Key(3)]
        public string SuperClass { get; set; }
        [Key(4)]
        public bool IsModOnly { get; set; }
        [Key(5)]
        public HashSet<PropertyRecord> PropertyRecords { get; set; } = new();
        [Key(6)]
        public List<ClassUsage> Usages { get; set; } = new();

        public ClassRecord(string Class, string Definition_package, int Definition_UID, string SuperClass)
        {
            this.Class = Class;
            this.Definition_package = Definition_package;
            this.Definition_UID = Definition_UID;
            this.SuperClass = SuperClass;
        }

        public ClassRecord()
        { }
    }
    [MessagePackObject] public sealed record PropertyRecord([property: Key(0)] string Property, [property: Key(1)] string Type);

    [MessagePackObject]
    public class ClassUsage
    {
        [Key(0)]
        public int FileKey { get; set; }
        [Key(1)]
        public int UIndex { get; set; }
        [Key(2)]
        public bool IsDefault { get; set; }
        [Key(3)]
        public bool IsMod { get; set; }

        public ClassUsage(int FileKey, int uIndex, bool IsDefault, bool IsMod)
        {
            this.FileKey = FileKey;
            this.UIndex = uIndex;
            this.IsDefault = IsDefault;
            this.IsMod = IsMod;
        }
        public ClassUsage()
        { }
    }
    [MessagePackObject]
    public class MaterialRecord
    {
        [Key(0)]
        public string MaterialName { get; set; }
        [Key(1)]
        public string ParentPackage { get; set; }
        [Key(2)]
        public bool IsDLCOnly { get; set; }
        [Key(3)]
        public List<MatUsage> Usages { get; set; } = new();
        [Key(4)]
        public List<MatSetting> MatSettings { get; set; } = new();

        public MaterialRecord(string MaterialName, string ParentPackage, bool IsDLCOnly, IEnumerable<MatSetting> MatSettings)
        {
            this.MaterialName = MaterialName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.MatSettings.AddRange(MatSettings);
        }

        public MaterialRecord()
        { }
    }

    [MessagePackObject] public sealed record MatUsage([property: Key(0)] int FileKey, [property: Key(1)] int UIndex, [property: Key(2)] bool IsInDLC);
    [MessagePackObject] public sealed record MatSetting([property: Key(0)] string Name, [property: Key(1)] string Parm1, [property: Key(2)] string Parm2);

    [MessagePackObject]
    public class AnimationRecord
    {
        [Key(0)]
        public string AnimSequence { get; set; }
        [Key(1)]
        public string SeqName { get; set; }
        [Key(2)]
        public string AnimData { get; set; }
        [Key(3)]
        public float Length { get; set; }
        [Key(4)]
        public int Frames { get; set; }
        [Key(5)]
        public string Compression { get; set; }
        [Key(6)]
        public string KeyFormat { get; set; }
        [Key(7)]
        public bool IsAmbPerf { get; set; }
        [Key(8)]
        public bool IsModOnly { get; set; }
        [Key(9)]
        public List<AnimUsage> Usages { get; set; } = new();

        public AnimationRecord(string AnimSequence, string SeqName, string AnimData, float Length, int Frames, string Compression, string KeyFormat, bool IsAmbPerf, bool IsModOnly)
        {
            this.AnimSequence = AnimSequence;
            this.SeqName = SeqName;
            this.AnimData = AnimData;
            this.Length = Length;
            this.Frames = Frames;
            this.Compression = Compression;
            this.KeyFormat = KeyFormat;
            this.IsAmbPerf = IsAmbPerf;
            this.IsModOnly = IsModOnly;
        }

        public AnimationRecord()
        { }
    }

    [MessagePackObject] public sealed record AnimUsage([property: Key(0)] int FileKey, [property: Key(1)] int UIndex, [property: Key(2)] bool IsInMod);

    [MessagePackObject]
    public class MeshRecord
    {
        [Key(0)]
        public string MeshName { get; set; }
        [Key(1)]
        public bool IsSkeleton { get; set; }
        [Key(2)] 
        public int BoneCount { get; set; }
        [Key(3)]
        public bool IsModOnly { get; set; }
        [Key(4)]
        public List<MeshUsage> Usages { get; set; } = new();

        public MeshRecord(string MeshName, bool IsSkeleton, bool IsModOnly, int BoneCount)
        {
            this.MeshName = MeshName;
            this.IsSkeleton = IsSkeleton;
            this.BoneCount = BoneCount;
            this.IsModOnly = IsModOnly;
        }

        public MeshRecord()
        { }
    }
    [MessagePackObject] public sealed record MeshUsage([property: Key(0)] int FileKey, [property: Key(1)] int UIndex, [property: Key(2)] bool IsInMod);

    [MessagePackObject]
    public class ParticleSysRecord
    {
        public enum VFXClass
        {
            ParticleSystem,
            RvrClientEffect,
            BioVFXTemplate
        }

        [Key(0)]
        public string PSName { get; set; }
        [Key(1)]
        public string ParentPackage { get; set; }
        [Key(2)]
        public bool IsDLCOnly { get; set; }
        [Key(3)]
        public bool IsModOnly { get; set; }
        [Key(4)]
        public int EffectCount { get; set; }
        [Key(5)]
        public VFXClass VFXType { get; set; }
        [Key(6)]
        public List<ParticleSysUsage> Usages { get; set; } = new();

        public ParticleSysRecord(string PSName, string ParentPackage, bool IsDLCOnly, bool IsModOnly, int EffectCount, VFXClass VFXType)
        {
            this.PSName = PSName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.IsModOnly = IsModOnly;
            this.EffectCount = EffectCount;
            this.VFXType = VFXType;
        }

        public ParticleSysRecord()
        { }
    }
    [MessagePackObject] public sealed record ParticleSysUsage([property: Key(0)] int FileKey, [property: Key(1)] int UIndex, [property: Key(2)] bool IsInDLC, [property: Key(3)] bool IsInMod);

    [MessagePackObject]
    public class TextureRecord
    {
        [Key(0)]
        public string TextureName { get; set; }
        [Key(1)]
        public string ParentPackage { get; set; }
        [Key(2)]
        public bool IsDLCOnly { get; set; }
        [Key(3)]
        public bool IsModOnly { get; set; }
        [Key(4)]
        public string CFormat { get; set; }
        [Key(5)]
        public string TexGrp { get; set; }
        [Key(6)]
        public int SizeX { get; set; }
        [Key(7)]
        public int SizeY { get; set; }
        [Key(8)]
        public string CRC { get; set; }
        [Key(9)]
        public List<TextureUsage> Usages { get; set; } = new();

        public TextureRecord(string TextureName, string ParentPackage, bool IsDLCOnly, bool IsModOnly, string CFormat, string TexGrp, int SizeX, int SizeY, string CRC)
        {
            this.TextureName = TextureName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.IsModOnly = IsModOnly;
            this.CFormat = CFormat;
            this.TexGrp = TexGrp;
            this.SizeX = SizeX;
            this.SizeY = SizeY;
            this.CRC = CRC;
        }

        public TextureRecord()
        { }
    }
    [MessagePackObject] public sealed record TextureUsage([property: Key(0)] int FileKey, [property: Key(1)] int UIndex, [property: Key(2)] bool IsInDLC, [property: Key(3)] bool IsInMod);

    [MessagePackObject]
    public class GUIElement
    {
        [Key(0)]
        public string GUIName { get; set; }
        [Key(1)]
        public int DataSize { get; set; }
        [Key(2)]
        public bool IsModOnly { get; set; }
        [Key(3)]
        public List<GUIUsage> Usages { get; set; } = new(); //File reference then export

        public GUIElement(string GUIName, int DataSize, bool IsModOnly)
        {
            this.GUIName = GUIName;
            this.DataSize = DataSize;
            this.IsModOnly = IsModOnly;
        }

        public GUIElement()
        { }
    }
    [MessagePackObject] public sealed record GUIUsage([property: Key(0)] int FileKey, [property: Key(1)] int UIndex, [property: Key(2)] bool IsInMod);

    [MessagePackObject]
    public class Conversation
    {
        [Key(0)]
        public string ConvName { get; set; }
        [Key(1)]
        public bool IsAmbient { get; set; }
        [Key(2)]
        public Tuple<int, int> ConvFile { get; set; } //file, export
        public Conversation(string ConvName, bool IsAmbient, Tuple<int, int> ConvFile)
        {
            this.ConvName = ConvName;
            this.IsAmbient = IsAmbient;
            this.ConvFile = ConvFile;
        }

        public Conversation()
        { }
    }
    [MessagePackObject]
    public class ConvoLine
    {
        [Key(0)]
        public int StrRef { get; set; }
        [Key(1)]
        public string Speaker { get; set; }
        [Key(2)]
        public string Line { get; set; }
        [Key(3)]
        public string Convo { get; set; }

        public ConvoLine(int StrRef, string Speaker, string Convo)
        {
            this.StrRef = StrRef;
            this.Speaker = Speaker;
            this.Convo = Convo;
        }

        public ConvoLine()
        { }
    }

    public class FileIndexToNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            int fileindex = (int)values[0];
            var listofFiles = values[1] as ObservableCollectionExtended<AssetDB.FileDirPair>;
            if (listofFiles == null || fileindex < 0 || fileindex >= listofFiles.Count || listofFiles.Count == 0)
            {
                return $"Error file name not found";
            }
            var export = (int)values[2];
            (string fileName, string directory) = listofFiles[fileindex];
            return $"{fileName}  # {export}   {directory} ";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null; //not needed
        }
    }

    #endregion

    #region SingleFileScan
    
    public class ClassScanSingleFileTask
    {
        public string ShortFileName { get; }

        public ClassScanSingleFileTask(string file, int filekey, bool scanCRC, bool scanLines)
        {
            File = file;
            ShortFileName = Path.GetFileNameWithoutExtension(file);
            FileKey = filekey;
            ScanCRC = scanCRC;
            ScanLines = scanLines;
        }

        public bool DumpCanceled;
        private readonly int FileKey;
        private readonly string File;
        private readonly bool ScanCRC;
        private readonly bool ScanLines;

        /// <summary>
        /// Dumps Property data to concurrent dictionary
        /// </summary>
        public void dumpPackageFile(MEGame GameBeingDumped, AssetDB dbScanner)
        {
            try
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(File);
                if (pcc.Game != GameBeingDumped)
                {
                    return; //rogue file from other game or UDK
                }

                bool IsDLC = pcc.IsInOfficialDLC();
                bool IsMod = !pcc.IsInBasegame() && !IsDLC;
                //foreach (IEntry entry in pcc.Exports.Concat<IEntry>(pcc.Imports))
                foreach (ExportEntry entry in pcc.Exports)
                {
                    if (DumpCanceled || pcc.FilePath.Contains("_LOC_") && !pcc.FilePath.Contains("INT")
                    ) //TEMP NEED BETTER WAY TO HANDLE LANGUAGES
                    {
                        return;
                    }

                    try
                    {
                        string className = entry.ClassName; //Handle basic class record
                        string objectNameInstanced = entry.ObjectName.Instanced;
                        int uindex = entry.UIndex;
                        var export = entry as ExportEntry;
                        if (className != "Class")
                        {
                            bool isDefault = export?.IsDefaultObject == true;

                            var pList = new List<PropertyRecord>();
                            var mSets = new List<MatSetting>();
                            PropertyCollection props = null;
                            if (export is not null)
                            {
                                props = export.GetProperties(false, false);
                                foreach (var p in props)
                                {
                                    string pName = p.Name;
                                    string pType = p.PropType.ToString();
                                    string pValue = "null";
                                    switch (p)
                                    {
                                        case ArrayPropertyBase parray:
                                            pValue = "Array";
                                            break;
                                        case StructProperty pstruct:
                                            pValue = "Struct";
                                            break;
                                        case NoneProperty pnone:
                                            pValue = "None";
                                            break;
                                        case ObjectProperty pobj:
                                            if (pcc.IsEntry(pobj.Value))
                                            {
                                                pValue = pcc.GetEntry(pobj.Value).ClassName;
                                            }

                                            break;
                                        case BoolProperty pbool:
                                            pValue = pbool.Value.ToString();
                                            break;
                                        case IntProperty pint:
                                            if (isDefault)
                                            {
                                                pValue = pint.Value.ToString();
                                            }
                                            else
                                            {
                                                pValue = "int"; //Keep DB size down
                                            }

                                            break;
                                        case FloatProperty pflt:
                                            if (isDefault)
                                            {
                                                pValue = pflt.Value.ToString();
                                            }
                                            else
                                            {
                                                pValue = "float"; //Keep DB size down
                                            }

                                            break;
                                        case NameProperty pnme:
                                            pValue = pnme.Value.ToString();
                                            break;
                                        case ByteProperty pbte:
                                            pValue = pbte.Value.ToString();
                                            break;
                                        case EnumProperty penum:
                                            pValue = penum.Value.ToString();
                                            break;
                                        case StrProperty pstr:
                                            if (isDefault)
                                            {
                                                pValue = pstr;
                                            }
                                            else
                                            {
                                                pValue = "string";
                                            }

                                            break;
                                        case StringRefProperty pstrref:
                                            if (isDefault)
                                            {
                                                pValue = pstrref.Value.ToString();
                                            }
                                            else
                                            {
                                                pValue = "TLK StringRef";
                                            }

                                            break;
                                        case DelegateProperty pdelg:
                                            if (pdelg.Value != null)
                                            {
                                                var pscrdel = pdelg.Value.Object;
                                                if (pscrdel != 0)
                                                {
                                                    pValue = pcc.GetEntry(pscrdel).ClassName;
                                                }
                                            }

                                            break;
                                        default:
                                            pValue = p.ToString();
                                            break;
                                    }

                                    var NewPropertyRecord = new PropertyRecord(pName, pType);
                                    pList.Add(NewPropertyRecord);

                                    if (entry.ClassName == "Material" && !dbScanner.GeneratedMats.ContainsKey(objectNameInstanced) &&
                                        !isDefault) //Run material settings
                                    {
                                        MatSetting pSet;
                                        var matSet_name = p.Name;
                                        if (matSet_name == "Expressions")
                                        {
                                            foreach (var param in p as ArrayProperty<ObjectProperty>)
                                            {
                                                if (param.Value > 0)
                                                {
                                                    var exprsn = pcc.GetUExport(param.Value);
                                                    var paramName = "n/a";
                                                    var paramNameProp = exprsn.GetProperty<NameProperty>("ParameterName");
                                                    if (paramNameProp != null)
                                                    {
                                                        paramName = paramNameProp.Value;
                                                    }

                                                    string exprsnName =
                                                        exprsn.ClassName.Replace("MaterialExpression", string.Empty);
                                                    switch (exprsn.ClassName)
                                                    {
                                                        case "MaterialExpressionScalarParameter":
                                                            var sValue = exprsn.GetProperty<FloatProperty>("DefaultValue");
                                                            string defscalar = "n/a";
                                                            if (sValue != null)
                                                            {
                                                                defscalar = sValue.Value.ToString();
                                                            }

                                                            pSet = new MatSetting(exprsnName, paramName, defscalar);
                                                            break;
                                                        case "MaterialExpressionVectorParameter":
                                                            string linearColor = "n/a";
                                                            var vValue = exprsn.GetProperty<StructProperty>("DefaultValue");
                                                            if (vValue != null)
                                                            {
                                                                var r = vValue.GetProp<FloatProperty>("R");
                                                                var g = vValue.GetProp<FloatProperty>("G");
                                                                var b = vValue.GetProp<FloatProperty>("B");
                                                                var a = vValue.GetProp<FloatProperty>("A");
                                                                if (r != null && g != null && b != null && a != null)
                                                                {
                                                                    linearColor =
                                                                        $"R:{r.Value} G:{g.Value} B:{b.Value} A:{a.Value}";
                                                                }
                                                            }

                                                            pSet = new MatSetting(exprsnName, paramName, linearColor);
                                                            break;
                                                        default:
                                                            pSet = new MatSetting(exprsnName, paramName, null);
                                                            break;
                                                    }

                                                    mSets.Add(pSet);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            pSet = new MatSetting(matSet_name, pType, pValue);
                                            mSets.Add(pSet);
                                        }
                                    }
                                }
                                
                            }

                            var classUsage = new ClassUsage(FileKey, uindex, isDefault, IsMod);
                            lock (dbScanner.ClassLocks.GetOrAdd(className, new object()))
                            {
                                if (dbScanner.GeneratedClasses.TryGetValue(className, out var oldVal))
                                {
                                    oldVal.Usages.Add(classUsage);
                                    foreach (PropertyRecord propRecord in pList)
                                    {
                                        if (!oldVal.PropertyRecords.Contains(propRecord))
                                        {
                                            oldVal.PropertyRecords.Add(propRecord);
                                        }
                                    }
                                }
                                else
                                {
                                    var newVal = new ClassRecord { Class = className, IsModOnly = IsMod };
                                    newVal.Usages.Add(classUsage);
                                    newVal.PropertyRecords.AddRange(pList);
                                    dbScanner.GeneratedClasses[className] = newVal;
                                }
                            }

                            if (isDefault)
                            {
                                continue;
                            }

                            string assetKey = entry.InstancedFullPath.ToLower();

                            if (className == "Material" || className == "DecalMaterial")
                            {
                                var matUsage = new MatUsage(FileKey, uindex, IsDLC);
                                if (dbScanner.GeneratedMats.TryGetValue(assetKey, out var eMat))
                                {
                                    lock (eMat)
                                    {
                                        eMat.Usages.Add(matUsage);
                                    }
                                }
                                else
                                {

                                    string parent;
                                    if (GameBeingDumped == MEGame.ME1 && File.EndsWith(".upk"))
                                    {
                                        parent = ShortFileName;
                                    }
                                    else
                                    {
                                        parent = GetTopParentPackage(entry);
                                    }
                                    
                                    if (className == "DecalMaterial" && !objectNameInstanced.Contains("Decal"))
                                    {
                                        objectNameInstanced += "_Decal";
                                    }

                                    var NewMat = new MaterialRecord(objectNameInstanced, parent, IsDLC, mSets);
                                    NewMat.Usages.Add(matUsage);
                                    if (!dbScanner.GeneratedMats.TryAdd(assetKey, NewMat))
                                    {
                                        var mat = dbScanner.GeneratedMats[assetKey];
                                        lock (mat)
                                        {
                                            mat.Usages.Add(matUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "AnimSequence" || className == "SFXAmbPerfGameData")
                            {
                                var animUsage = new AnimUsage(FileKey, uindex, IsMod);
                                if (dbScanner.GeneratedAnims.TryGetValue(assetKey, out var anim))
                                {
                                    lock (anim)
                                    {
                                        anim.Usages.Add(animUsage);
                                    }
                                }
                                else
                                {
                                    string aSeq = null;
                                    string aGrp = "None";
                                    float aLength = 0;
                                    int aFrames = 0;
                                    string aComp = "None";
                                    string aKeyF = "None";
                                    bool IsAmbPerf = false;
                                    if (className == "AnimSequence")
                                    {
                                        var pSeq = props.GetProp<NameProperty>("SequenceName");
                                        if (pSeq != null)
                                        {
                                            aSeq = pSeq.Value.Instanced;
                                            aGrp = objectNameInstanced.Replace($"{aSeq}_", null);
                                        }

                                        var pLength = props.GetProp<FloatProperty>("SequenceLength");
                                        aLength = pLength?.Value ?? 0;

                                        var pFrames = props.GetProp<IntProperty>("NumFrames");
                                        aFrames = pFrames?.Value ?? 0;

                                        var pComp = props.GetProp<EnumProperty>("RotationCompressionFormat");
                                        aComp = pComp?.Value.ToString() ?? "None";

                                        var pKeyF = props.GetProp<EnumProperty>("KeyEncodingFormat");
                                        aKeyF = pKeyF?.Value.ToString() ?? "None";
                                    }
                                    else //is ambient performance
                                    {
                                        IsAmbPerf = true;
                                        aSeq = "Multiple";
                                        var pAnimsets = props.GetProp<ArrayProperty<StructProperty>>("m_aAnimsets");
                                        aFrames = pAnimsets?.Count ?? 0;
                                    }

                                    var NewAnim = new AnimationRecord(objectNameInstanced, aSeq, aGrp, aLength, aFrames, aComp, aKeyF, IsAmbPerf, IsMod);
                                    NewAnim.Usages.Add(animUsage);
                                    if (!dbScanner.GeneratedAnims.TryAdd(assetKey, NewAnim))
                                    {
                                        var a = dbScanner.GeneratedAnims[assetKey];
                                        lock (a)
                                        {
                                            a.Usages.Add(animUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "SkeletalMesh" || className == "StaticMesh")
                            {
                                var meshUsage = new MeshUsage(FileKey, uindex, IsMod);
                                if (dbScanner.GeneratedMeshes.ContainsKey(assetKey))
                                {
                                    var mr = dbScanner.GeneratedMeshes[assetKey];
                                    lock (mr)
                                    {
                                        mr.Usages.Add(meshUsage);
                                    }
                                }
                                else
                                {
                                    bool IsSkel = className == "SkeletalMesh";
                                    int bones = 0;
                                    if (IsSkel)
                                    {
                                        var bin = ObjectBinary.From<SkeletalMesh>(entry);
                                        bones = bin?.RefSkeleton.Length ?? 0;
                                    }

                                    var NewMeshRec = new MeshRecord(objectNameInstanced, IsSkel, IsMod, bones);
                                    NewMeshRec.Usages.Add(meshUsage);
                                    if (!dbScanner.GeneratedMeshes.TryAdd(assetKey, NewMeshRec))
                                    {
                                        var mr = dbScanner.GeneratedMeshes[assetKey];
                                        lock (mr)
                                        {
                                            mr.Usages.Add(meshUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "ParticleSystem" || className == "RvrClientEffect" || className == "BioVFXTemplate")
                            {
                                var particleSysUsage = new ParticleSysUsage(FileKey, uindex, IsDLC, IsMod);
                                if (dbScanner.GeneratedPS.ContainsKey(assetKey))
                                {
                                    var ePS = dbScanner.GeneratedPS[assetKey];
                                    lock (ePS)
                                    {
                                        ePS.Usages.Add(particleSysUsage);
                                    }
                                }
                                else
                                {
                                    string parent = null;
                                    if (GameBeingDumped == MEGame.ME1 && File.EndsWith(".upk"))
                                    {
                                        parent = ShortFileName;
                                    }
                                    else
                                    {
                                        parent = GetTopParentPackage(entry);
                                    }

                                    var vfxtype = ParticleSysRecord.VFXClass.BioVFXTemplate;
                                    int EmCnt = 0;
                                    if (className == "ParticleSystem")
                                    {
                                        var EmtProp = props.GetProp<ArrayProperty<ObjectProperty>>("Emitters");
                                        EmCnt = EmtProp?.Count ?? 0;
                                        vfxtype = ParticleSysRecord.VFXClass.ParticleSystem;
                                    }
                                    else if (className == "RvrClientEffect")
                                    {
                                        var RvrProp = props.GetProp<ArrayProperty<ObjectProperty>>("m_lstModules");
                                        EmCnt = RvrProp?.Count ?? 0;
                                        vfxtype = ParticleSysRecord.VFXClass.RvrClientEffect;
                                    }

                                    var NewPS = new ParticleSysRecord(objectNameInstanced, parent, IsDLC, IsMod, EmCnt, vfxtype);
                                    NewPS.Usages.Add(particleSysUsage);
                                    if (!dbScanner.GeneratedPS.TryAdd(assetKey, NewPS))
                                    {
                                        var ePS = dbScanner.GeneratedPS[assetKey];
                                        lock (ePS)
                                        {
                                            ePS.Usages.Add(particleSysUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "Texture2D" || className == "TextureCube" || className == "TextureMovie")
                            {
                                var textureUsage = new TextureUsage(FileKey, uindex, IsDLC, IsMod);
                                if (dbScanner.GeneratedText.ContainsKey(assetKey))
                                {
                                    var t = dbScanner.GeneratedText[assetKey];
                                    lock (t)
                                    {
                                        t.Usages.Add(textureUsage);
                                    }
                                }
                                else
                                {
                                    string parent;
                                    if (GameBeingDumped == MEGame.ME1 && File.EndsWith(".upk"))
                                    {
                                        parent = ShortFileName;
                                    }
                                    else
                                    {
                                        parent = GetTopParentPackage(entry);
                                    }

                                    string pformat = "TextureCube";
                                    int psizeX = 0;
                                    int psizeY = 0;
                                    string cRC = "n/a";
                                    string texgrp = "n/a";
                                    if (className != "TextureCube")
                                    {
                                        pformat = "TextureMovie";
                                        if (className != "TextureMovie")
                                        {
                                            var formp = props.GetProp<EnumProperty>("Format");
                                            pformat = formp?.Value.Name ?? "n/a";
                                            pformat = pformat.Replace("PF_", string.Empty);
                                            var tgrp = props.GetProp<EnumProperty>("LODGroup");
                                            texgrp = tgrp?.Value.Instanced ?? "n/a";
                                            texgrp = texgrp.Replace("TEXTUREGROUP_", string.Empty);
                                            texgrp = texgrp.Replace("_", string.Empty);
                                            if (ScanCRC)
                                            {
                                                cRC = Texture2D.GetTextureCRC(entry).ToString("X8");
                                            }
                                        }

                                        var propX = props.GetProp<IntProperty>("SizeX");
                                        psizeX = propX?.Value ?? 0;
                                        var propY = props.GetProp<IntProperty>("SizeY");
                                        psizeY = propY?.Value ?? 0;
                                    }

                                    if (entry.Parent?.ClassName == "TextureCube")
                                    {
                                        objectNameInstanced = $"{entry.Parent.ObjectName}_{objectNameInstanced}";
                                    }

                                    var NewTex = new TextureRecord(objectNameInstanced, parent, IsDLC, IsMod, pformat, texgrp, psizeX, psizeY, cRC);
                                    NewTex.Usages.Add(textureUsage);
                                    if (dbScanner.GeneratedText.TryAdd(assetKey, NewTex))
                                    {
                                        var t = dbScanner.GeneratedText[assetKey];
                                        lock (t)
                                        {
                                            t.Usages.Add(textureUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "GFxMovieInfo" || className == "BioSWF")
                            {
                                if (dbScanner.GeneratedGUI.ContainsKey(assetKey))
                                {
                                    var eGUI = dbScanner.GeneratedGUI[assetKey];
                                    lock (eGUI)
                                    {
                                        eGUI.Usages.Add(new GUIUsage(FileKey, uindex, IsMod));
                                    }
                                }
                                else
                                {
                                    string dataPropName = className == "GFxMovieInfo" ? "RawData" : "Data";
                                    var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                                    int datasize = rawData?.Count ?? 0;
                                    var NewGUI = new GUIElement(objectNameInstanced, datasize, IsMod);
                                    NewGUI.Usages.Add(new GUIUsage(FileKey, uindex, IsMod));
                                    if (dbScanner.GeneratedGUI.TryAdd(assetKey, NewGUI))
                                    {
                                        var eGUI = dbScanner.GeneratedGUI[assetKey];
                                        lock (eGUI)
                                        {
                                            eGUI.Usages.Add(new GUIUsage(FileKey, uindex, IsMod));
                                        }
                                    }
                                }
                            }
                            else if (ScanLines && className == "BioConversation")
                            {
                                if (!dbScanner.GeneratedConvo.ContainsKey(objectNameInstanced))
                                {
                                    bool IsAmbient = true;
                                    var speakers = new List<string> { "Shepard", "Owner" };
                                    if (entry.Game != MEGame.ME3)
                                    {
                                        var s_speakers = props.GetProp<ArrayProperty<StructProperty>>("m_SpeakerList");
                                        if (s_speakers != null)
                                        {
                                            speakers.AddRange(s_speakers.Select(t => t.GetProp<NameProperty>("sSpeakerTag").ToString()));
                                        }
                                    }
                                    else
                                    {
                                        var a_speakers = props.GetProp<ArrayProperty<NameProperty>>("m_aSpeakerList");
                                        if (a_speakers != null)
                                        {
                                            foreach (NameProperty n in a_speakers)
                                            {
                                                speakers.Add(n.ToString());
                                            }
                                        }
                                    }

                                    var entryprop = props.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
                                    foreach (StructProperty Node in entryprop)
                                    {
                                        int speakerindex = Node.GetProp<IntProperty>("nSpeakerIndex");
                                        speakerindex = speakerindex + 2;
                                        if (speakerindex < 0 || speakerindex >= speakers.Count)
                                            continue;
                                        int linestrref = 0;
                                        var linestrrefprop = Node.GetProp<StringRefProperty>("srText");
                                        if (linestrrefprop != null)
                                        {
                                            linestrref = linestrrefprop.Value;
                                        }

                                        var ambientLine = Node.GetProp<BoolProperty>("IsAmbient");
                                        if (IsAmbient)
                                            IsAmbient = ambientLine;

                                        ConvoLine newLine = new(linestrref, speakers[speakerindex], objectNameInstanced);
                                        if (GameBeingDumped == MEGame.ME1)
                                        {
                                            newLine.Line = ME1TalkFiles.findDataById(linestrref, pcc);
                                            if (newLine.Line == "No Data" || newLine.Line == "\"\"" ||
                                                newLine.Line == "\" \"" || newLine.Line == " ")
                                                continue;
                                        }

                                        dbScanner.GeneratedLines.TryAdd(linestrref.ToString(), newLine);
                                    }

                                    var replyprop = props.GetProp<ArrayProperty<StructProperty>>("m_ReplyList");
                                    if (replyprop != null)
                                    {
                                        foreach (StructProperty Node in replyprop)
                                        {
                                            int linestrref = 0;
                                            var linestrrefprop = Node.GetProp<StringRefProperty>("srText");
                                            if (linestrrefprop != null)
                                            {
                                                linestrref = linestrrefprop.Value;
                                            }

                                            var ambientLine = Node.GetProp<BoolProperty>("IsAmbient");
                                            if (IsAmbient)
                                                IsAmbient = ambientLine;

                                            ConvoLine newLine = new(linestrref, "Shepard", objectNameInstanced);
                                            if (GameBeingDumped == MEGame.ME1)
                                            {
                                                newLine.Line = ME1TalkFiles.findDataById(linestrref, pcc);
                                                if (newLine.Line == "No Data" || newLine.Line == "\"\"" ||
                                                    newLine.Line == "\" \"" || newLine.Line == " ")
                                                    continue;
                                            }

                                            dbScanner.GeneratedLines.TryAdd(linestrref.ToString(), newLine);
                                        }
                                    }

                                    var NewConv = new Conversation(objectNameInstanced, IsAmbient,
                                                                   new Tuple<int, int>(FileKey, uindex));
                                    dbScanner.GeneratedConvo.TryAdd(assetKey, NewConv);
                                }
                            }
                        }
                        else if (export is not null)
                        {
                            var newClassRecord = new ClassRecord(export.ObjectName, ShortFileName, uindex, export.SuperClassName) {IsModOnly = IsMod};
                            var classUsage = new ClassUsage(FileKey, uindex, false, IsMod);

                            lock (dbScanner.ClassLocks.GetOrAdd(objectNameInstanced, new object()))
                            {
                                if (dbScanner.GeneratedClasses.TryGetValue(objectNameInstanced, out ClassRecord oldVal))
                                {
                                    if (oldVal.Definition_package is null) //fake classrecord, created when a usage was found
                                    {
                                        newClassRecord.Usages.AddRange(oldVal.Usages);
                                        newClassRecord.Usages.Add(classUsage);
                                        newClassRecord.PropertyRecords.AddRange(oldVal.PropertyRecords);
                                        newClassRecord.IsModOnly = IsMod & oldVal.IsModOnly;
                                        dbScanner.GeneratedClasses[objectNameInstanced] = newClassRecord;
                                    }
                                    else
                                    {
                                        oldVal.Usages.Add(classUsage);
                                        oldVal.IsModOnly &= IsMod;
                                    }
                                }
                                else
                                {
                                    newClassRecord.Usages.Add(classUsage);
                                    dbScanner.GeneratedClasses[objectNameInstanced] = newClassRecord;
                                }
                            }
                        }
                    }
                    catch (Exception e) when (!App.IsDebug)
                    {
                        MessageBox.Show(
                            $"Exception Bug detected in single file: {entry.FileRef.FilePath} Export:{entry.UIndex}");
                    }
                }
            }
            catch (Exception e) when (!App.IsDebug)
            {
                throw new Exception($"Error dumping package file {File}. See the inner exception for details.", e);
            }
        }


        private static string GetTopParentPackage(IEntry entry)
        {
            while (true)
            {
                if (entry.HasParent)
                {
                    entry = entry.Parent;
                }
                else
                {
                    return entry.ObjectName;
                }
            }
        }
    }

    #endregion

}
