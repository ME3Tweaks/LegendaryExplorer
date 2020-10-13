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
using System.Windows.Shell;
using ME3Explorer.ME3ExpMemoryAnalyzer;
using ME3ExplorerCore.Gammtek.Collections.ObjectModel;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;
using AnimSequence = ME3ExplorerCore.Unreal.BinaryConverters.AnimSequence;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.ME1;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.Unreal.Classes;
using SkeletalMesh = ME3ExplorerCore.Unreal.BinaryConverters.SkeletalMesh;
using ME3ExplorerCore.TLK;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace ME3Explorer.AssetDatabase
{


    /// <summary>
    /// Interaction logic for AssetDB
    /// </summary>
    public partial class AssetDB : NotifyPropertyChangedWindowBase
    {
        #region Declarations
        public const string dbCurrentBuild = "4.0"; //If changes are made that invalidate old databases edit this.
        private int previousView { get; set; }
        private int _currentView;
        public int currentView { get => _currentView; set { previousView = _currentView; SetProperty(ref _currentView, value); } }
        public enum dbTableType
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
        public PropsDataBase CurrentDataBase { get; } = new PropsDataBase(MEGame.Unknown, null, null, new ObservableCollectionExtended<Tuple<string, int>>(), new List<string>(), new ObservableCollectionExtended<ClassRecord>(), new ObservableCollectionExtended<Material>(),
            new ObservableCollectionExtended<Animation>(), new ObservableCollectionExtended<MeshRecord>(), new ObservableCollectionExtended<ParticleSys>(), new ObservableCollectionExtended<TextureRecord>(), new ObservableCollectionExtended<GUIElement>(),
            new ObservableCollectionExtended<Conversation>(), new ObservableCollectionExtended<ConvoLine>());
        public ObservableCollectionExtended<Tuple<string, string>> FileListExtended { get; } = new ObservableCollectionExtended<Tuple<string, string>>();
        /// <summary>
        /// Dictionary that stores generated classes
        /// </summary>
        public ConcurrentDictionary<string, ClassRecord> GeneratedClasses = new ConcurrentDictionary<string, ClassRecord>();
        /// <summary>
        /// Dictionary that stores generated Animations
        /// </summary>
        public ConcurrentDictionary<string, Animation> GeneratedAnims = new ConcurrentDictionary<string, Animation>();
        /// <summary>
        /// Dictionary that stores generated Materials
        /// </summary>
        public ConcurrentDictionary<string, Material> GeneratedMats = new ConcurrentDictionary<string, Material>();
        /// <summary>
        /// Dictionary that stores generated Meshes
        /// </summary>
        public ConcurrentDictionary<string, MeshRecord> GeneratedMeshes = new ConcurrentDictionary<string, MeshRecord>();
        /// <summary>
        /// Dictionary that stores generated Particle Systems
        /// </summary>
        public ConcurrentDictionary<string, ParticleSys> GeneratedPS = new ConcurrentDictionary<string, ParticleSys>();
        /// <summary>
        /// Dictionary that stores generated Textures
        /// </summary>
        public ConcurrentDictionary<string, TextureRecord> GeneratedText = new ConcurrentDictionary<string, TextureRecord>();
        /// <summary>
        /// Dictionary that stores generated GFXMovies
        /// </summary>
        public ConcurrentDictionary<string, GUIElement> GeneratedGUI = new ConcurrentDictionary<string, GUIElement>();
        /// <summary>
        /// Dictionary that stores generated convos
        /// </summary>
        public ConcurrentDictionary<string, Conversation> GeneratedConvo = new ConcurrentDictionary<string, Conversation>();
        /// <summary>
        /// Dictionary that stores generated lines
        /// </summary>
        public ConcurrentDictionary<string, ConvoLine> GeneratedLines = new ConcurrentDictionary<string, ConvoLine>();
        /// <summary>
        /// Used to check if values generated are unique.
        /// </summary>
        public ConcurrentDictionary<string, bool> GeneratedValueChecker = new ConcurrentDictionary<string, bool>();

        /// <summary>
        /// Items show in the list that are currently being processed
        /// </summary>
        public ObservableCollectionExtended<ClassScanSingleFileTask> CurrentDumpingItems { get; set; } = new ObservableCollectionExtended<ClassScanSingleFileTask>();

        /// <summary>
        /// All items in the queue
        /// </summary>
        private List<ClassScanSingleFileTask> AllDumpingItems;

        private static BackgroundWorker dbworker = new BackgroundWorker();
        public BlockingCollection<ClassRecord> _dbqueue = new BlockingCollection<ClassRecord>();
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
        private BlockingCollection<ConvoLine> _linequeue = new BlockingCollection<ConvoLine>();
        private Tuple<string, string, int, string, bool> _currentConvo = new Tuple<string, string, int, string, bool>(null, null, -1, null, false); //ConvoName, FileName, export, contentdir, isAmbient
        public Tuple<string, string, int, string, bool> CurrentConvo { get => _currentConvo; set => SetProperty(ref _currentConvo, value); }
        public ObservableCollectionExtended<string> SpeakerList { get; } = new ObservableCollectionExtended<string>();
        private bool _isGettingTLKs;
        public bool IsGettingTLKs { get => _isGettingTLKs; set => SetProperty(ref _isGettingTLKs, value); }
        public ObservableDictionary<int, string> CustomFileList { get; } = new ObservableDictionary<int, string>(); //FileKey, filename<space>Dir
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
            return currentView == 5 && CurrentGame == MEGame.ME3 && lstbx_Anims.SelectedIndex >= 0 && !((lstbx_Anims.SelectedItem as Animation)?.IsAmbPerf ?? true);
        }
        private bool IsAnimSequenceSelected() => currentView == 5 && lstbx_Anims.SelectedIndex >= 0 && !((lstbx_Anims.SelectedItem as Animation)?.IsAmbPerf ?? true);

        #endregion

        #region Startup/Exit

        public AssetDB()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(new MemoryAnalyzerObjectExtended("Asset Database", new WeakReference(this)));
            Analytics.TrackEvent("Used tool", new Dictionary<string, string>()
            {
                { "Toolname", "Asset Database" }
            });
            LoadCommands();

            //Get default db / gane
            CurrentDBPath = Properties.Settings.Default.AssetDBPath;
            Enum.TryParse<MEGame>(Properties.Settings.Default.AssetDBGame, out MEGame game);
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
                SwitchGame(MEGame.ME3.ToString());
            }
            Activate();
        }
        private async void AssetDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            Properties.Settings.Default.AssetDBPath = CurrentDBPath;
            Properties.Settings.Default.AssetDBGame = CurrentGame.ToString();
            EmbeddedTextureViewerTab_EmbeddedTextureViewer.UnloadExport();
            BIKExternalExportLoaderTab_BIKExternalExportLoader.UnloadExport();
            MeshRendererTab_MeshRenderer.UnloadExport();
            SoundpanelWPF_ADB.UnloadExport();
            SoundpanelWPF_ADB.FreeAudioResources();

            MeshRendererTab_MeshRenderer.Dispose();
            SoundpanelWPF_ADB.Dispose();
            BIKExternalExportLoaderTab_BIKExternalExportLoader.Dispose();
            EmbeddedTextureViewerTab_EmbeddedTextureViewer.Dispose();

            audioPcc?.Dispose();
            meshPcc?.Dispose();
            textPcc?.Dispose();
#if DEBUG
            await Task.Delay(6000);

            if (audioPcc != null || meshPcc != null || textPcc != null)
            {
                MessageBox.Show("Still stuff in memory!", "Asset DB");
            }
#endif
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
        public static async Task LoadDatabase(string currentDbPath, MEGame game, PropsDataBase database, CancellationToken cancelloadingToken, dbTableType dbTable = dbTableType.Master)
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
        public static async Task<PropsDataBase> ParseDBAsync(MEGame dbgame, string dbpath, string build, CancellationToken cancel, dbTableType dbTable)
        {
            var deserializingQueue = new BlockingCollection<PropsDataBase>();
            var expectedtables = new ConcurrentDictionary<dbTableType, bool>();  //Stores which tables are expected to load
            var typology = Enum.GetValues(typeof(dbTableType)).Cast<dbTableType>().ToList();
            foreach (dbTableType type in typology)
            {
                bool expectedToLoad = true;
                if (type != dbTableType.Master && dbTable != dbTableType.Master && dbTable != type)
                    expectedToLoad = false;
                expectedtables.TryAdd(type, expectedToLoad);
            }

            try
            {
                await Task.Run(() =>
                {
                    var archiveEntries = new Dictionary<string, ZipArchiveEntry>();
                    using ZipArchive archive = new ZipArchive(new FileStream(dbpath, FileMode.Open));
                    if (archive.Entries.Any(e => e.Name == $"MasterDB{dbgame}_{build}.json"))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            dbTableType entryType = dbTableType.Master;
                            if (!entry.Name.StartsWith("Master"))
                            {
                                bool typecast = Enum.TryParse(entry.Name.Substring(0, entry.Name.Length - 10), out entryType);
                                if (!typecast || (dbTable != dbTableType.Master && dbTable != entryType))
                                    continue;
                            }

                            var ms = new MemoryStream();
                            using (Stream estream = entry.Open())
                            {
                                estream.CopyTo(ms);
                            }
                            ms.Position = 0;
                            var unitTask = Task.Run(() => JsonFileParse(ms, entryType, deserializingQueue, cancel));
                        }

                    }
                    else //Wrong build - send dummy pdb back and ask user to refresh
                    {
                        PropsDataBase pdb = new PropsDataBase();
                        var entry = archive.Entries.FirstOrDefault(z => z.Name.StartsWith("Master"));
                        pdb.DataBaseversion = "pre 2.0";
                        if (entry != null)
                        {
                            using Stream estream = entry.Open();
                            using StreamReader sr = new StreamReader(estream);
                            using JsonTextReader reader = new JsonTextReader(sr);
                            var Serializer = new JsonSerializer();
                            while (reader.Read())
                            {
                                if (reader.TokenType == JsonToken.PropertyName
                                 && (string)reader.Value == "DataBaseversion")
                                {
                                    reader.Read();

                                    string oldDbver = Serializer.Deserialize<string>(reader);
                                    pdb.DataBaseversion = oldDbver;
                                    break;
                                }
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
                            case dbTableType.Class:
                                readData.ClassRecords.AddRange(pdb.ClassRecords);
                                break;
                            case dbTableType.Materials:
                                readData.Materials.AddRange(pdb.Materials);
                                break;
                            case dbTableType.Animations:
                                readData.Animations.AddRange(pdb.Animations);
                                break;
                            case dbTableType.Meshes:
                                readData.Meshes.AddRange(pdb.Meshes);
                                break;
                            case dbTableType.Particles:
                                readData.Particles.AddRange(pdb.Particles);
                                break;
                            case dbTableType.Textures:
                                readData.Textures.AddRange(pdb.Textures);
                                break;
                            case dbTableType.GUIElements:
                                readData.GUIElements.AddRange(pdb.GUIElements);
                                break;
                            case dbTableType.Convos:
                                readData.Conversations.AddRange(pdb.Conversations);
                                break;
                            case dbTableType.Lines:
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
        private static void JsonFileParse(MemoryStream ms, dbTableType dbType, BlockingCollection<PropsDataBase> propsDataBases, CancellationToken ct)
        {

            PropsDataBase readData = new PropsDataBase { DataBaseversion = dbType.ToString() };
            readData.dbTable[dbType] = true;
            try
            {
                using StreamReader sr = new StreamReader(ms);
                using JsonTextReader reader = new JsonTextReader(sr);
                var Serializer = new JsonSerializer();
                switch (dbType)
                {
                    case dbTableType.Master:
                        var mst = Serializer.Deserialize<PropsDataBase>(reader);
                        readData.meGame = mst.meGame;
                        readData.GenerationDate = mst.GenerationDate;
                        readData.DataBaseversion = mst.DataBaseversion;
                        readData.FileList.AddRange(mst.FileList);
                        readData.ContentDir.AddRange(mst.ContentDir);
                        break;
                    case dbTableType.Class:
                        var cls = Serializer.Deserialize<ObservableCollectionExtended<ClassRecord>>(reader);
                        readData.ClassRecords.AddRange(cls);
                        break;
                    case dbTableType.Materials:
                        var mats = Serializer.Deserialize<ObservableCollectionExtended<Material>>(reader);
                        readData.Materials.AddRange(mats);
                        break;
                    case dbTableType.Animations:
                        var an = Serializer.Deserialize<ObservableCollectionExtended<Animation>>(reader);
                        readData.Animations.AddRange(an);
                        break;
                    case dbTableType.Meshes:
                        var msh = Serializer.Deserialize<ObservableCollectionExtended<MeshRecord>>(reader);
                        readData.Meshes.AddRange(msh);
                        break;
                    case dbTableType.Particles:
                        var ps = Serializer.Deserialize<ObservableCollectionExtended<ParticleSys>>(reader);
                        readData.Particles.AddRange(ps);
                        break;
                    case dbTableType.Textures:
                        var txt = Serializer.Deserialize<ObservableCollectionExtended<TextureRecord>>(reader);
                        readData.Textures.AddRange(txt);
                        break;
                    case dbTableType.GUIElements:
                        var gui = Serializer.Deserialize<ObservableCollectionExtended<GUIElement>>(reader);
                        readData.GUIElements.AddRange(gui);
                        break;
                    case dbTableType.Convos:
                        var cnv = Serializer.Deserialize<ObservableCollectionExtended<Conversation>>(reader);
                        readData.Conversations.AddRange(cnv);
                        break;
                    case dbTableType.Lines:
                        var line = Serializer.Deserialize<ObservableCollectionExtended<ConvoLine>>(reader);
                        readData.Lines.AddRange(line);
                        break;
                }
                if (ct.IsCancellationRequested)
                {
                    Console.WriteLine("Cancelled JsonParseDB");
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

            var masterDB = new PropsDataBase(CurrentDataBase.meGame, CurrentDataBase.GenerationDate, CurrentDataBase.DataBaseversion, CurrentDataBase.FileList, CurrentDataBase.ContentDir, new ObservableCollectionExtended<ClassRecord>(), new ObservableCollectionExtended<Material>(),
            new ObservableCollectionExtended<Animation>(), new ObservableCollectionExtended<MeshRecord>(), new ObservableCollectionExtended<ParticleSys>(), new ObservableCollectionExtended<TextureRecord>(), new ObservableCollectionExtended<GUIElement>(), new ObservableCollectionExtended<Conversation>(), new ObservableCollectionExtended<ConvoLine>());
            var masterSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(masterDB));
            var clsSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.ClassRecords));
            var mtlSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Materials));
            var animSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Animations));
            var mshSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Meshes));
            var psSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Particles));
            var txtSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Textures));
            var guiSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.GUIElements));
            var convSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Conversations));
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
            var lineSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(linesExLine));

            using (var fileStream = new FileStream(CurrentDBPath, FileMode.Create))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
                {
                    await Task.WhenAll(masterSrl, clsSrl, mtlSrl, animSrl, mshSrl, psSrl, txtSrl, guiSrl, convSrl, lineSrl);
                    var build = dbCurrentBuild.Trim(' ', '*', '.');
                    var masterjson = archive.CreateEntry($"MasterDB{CurrentGame}_{build}.json");
                    using (var entryStream = masterjson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(masterSrl.Result));
                    }
                    var classjson = archive.CreateEntry($"{dbTableType.Class.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = classjson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(clsSrl.Result));
                    }
                    var matjson = archive.CreateEntry($"{dbTableType.Materials.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = matjson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(mtlSrl.Result));
                    }
                    var animJson = archive.CreateEntry($"{dbTableType.Animations.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = animJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(animSrl.Result));
                    }
                    var mshJson = archive.CreateEntry($"{dbTableType.Meshes.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = mshJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(mshSrl.Result));
                    }
                    var psJson = archive.CreateEntry($"{dbTableType.Particles.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = psJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(psSrl.Result));
                    }
                    var txtJson = archive.CreateEntry($"{dbTableType.Textures.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = txtJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(txtSrl.Result));
                    }
                    var guiJson = archive.CreateEntry($"{dbTableType.GUIElements.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = guiJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(guiSrl.Result));
                    }
                    var convJson = archive.CreateEntry($"{dbTableType.Convos.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = convJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(convSrl.Result));
                    }
                    var lineJson = archive.CreateEntry($"{dbTableType.Lines.ToString()}DB{CurrentGame}.json");
                    using (var entryStream = lineJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(lineSrl.Result));
                    }
                }
            }
            menu_SaveXEmptyLines.IsEnabled = false;
            CurrentOverallOperationText = $"Database saved.";
            IsBusy = false;
            await Task.Delay(5000);
            CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count} Meshes: {CurrentDataBase.Meshes.Count} Particles: { CurrentDataBase.Particles.Count} Textures: { CurrentDataBase.Textures.Count} Elements: { CurrentDataBase.GUIElements.Count}";
        }
        public void ClearDataBase()
        {
            CurrentDataBase.meGame = CurrentGame;
            CurrentDataBase.GenerationDate = null;
            CurrentDataBase.FileList.Clear();
            CurrentDataBase.ContentDir.Clear();
            CurrentDataBase.ClassRecords.ClearEx();
            CurrentDataBase.Animations.ClearEx();
            CurrentDataBase.Materials.ClearEx();
            CurrentDataBase.Meshes.ClearEx();
            CurrentDataBase.Particles.ClearEx();
            CurrentDataBase.Textures.ClearEx();
            CurrentDataBase.GUIElements.ClearEx();
            CurrentDataBase.Conversations.ClearEx();
            CurrentDataBase.Lines.ClearEx();
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
                    if (!spkrs.Any(s => s == line.Speaker))
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
                if (!spkrs.Any(s => s == line.Speaker))
                    spkrs.Add(line.Speaker);
            }
            var emptylines = CurrentDataBase.Lines.Where(l => l.Line == "No Data").ToList();
            CurrentDataBase.Lines.RemoveRange(emptylines);
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
            CurrentDBPath = GetDBPath(CurrentGame);

            if (CurrentDBPath != null && File.Exists(CurrentDBPath))
            {
                CurrentOverallOperationText = "Loading database";
                BusyHeader = "Loading database";
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
                        if (warn != MessageBoxResult.Cancel)
                        {
                            ScanGame();
                            return false;
                        }
                        ClearDataBase();
                        IsBusy = false;
                        return false;
                    }
                    return true;
                }).ContinueWithOnUIThread(prevTask =>
                {
                    if (prevTask.Result)
                    {
                        foreach (var f in CurrentDataBase.FileList)
                        {
                            var cd = CurrentDataBase.ContentDir[f.Item2];
                            FileListExtended.Add(new Tuple<string, string>(f.Item1, cd));
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
            var cr = lstbx_Classes.SelectedItem as ClassRecord;
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
            var scidx = CurrentDataBase.ClassRecords.IndexOf(CurrentDataBase.ClassRecords.Where(r => r.Class == sClass).FirstOrDefault());
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
            int usageexp = 0;
            string contentdir = null;

            if (lstbx_Usages.SelectedIndex >= 0 && currentView == 1)
            {
                var c = lstbx_Usages.SelectedItem as ClassUsage;
                usagepkg = FileListExtended[c.FileKey].Item1;
                contentdir = FileListExtended[c.FileKey].Item2;
                usageexp = c.ExportUID;
            }
            else if (lstbx_MatUsages.SelectedIndex >= 0 && currentView == 2)
            {
                var m = lstbx_MatUsages.SelectedItem as Tuple<int, int, bool>;
                usagepkg = FileListExtended[m.Item1].Item1;
                contentdir = FileListExtended[m.Item1].Item2;
                usageexp = m.Item2;
            }
            else if (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3)
            {
                var s = lstbx_MeshUsages.SelectedItem as Tuple<int, int, bool>;
                usagepkg = FileListExtended[s.Item1].Item1;
                contentdir = FileListExtended[s.Item1].Item2;
                usageexp = s.Item2;
            }
            else if (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 4)
            {
                var t = lstbx_TextureUsages.SelectedItem as Tuple<int, int, bool, bool>;
                usagepkg = FileListExtended[t.Item1].Item1;
                contentdir = FileListExtended[t.Item1].Item2;
                usageexp = t.Item2;
            }
            else if (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 5)
            {
                var a = lstbx_AnimUsages.SelectedItem as Tuple<int, int, bool>;
                usagepkg = FileListExtended[a.Item1].Item1;
                contentdir = FileListExtended[a.Item1].Item2;
                usageexp = a.Item2;
            }
            else if (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 6)
            {
                var ps = lstbx_PSUsages.SelectedItem as Tuple<int, int, bool, bool>;
                usagepkg = FileListExtended[ps.Item1].Item1;
                contentdir = FileListExtended[ps.Item1].Item2;
                usageexp = ps.Item2;
            }
            else if (lstbx_GUIUsages.SelectedIndex >= 0 && currentView == 7)
            {
                var sf = (Tuple<int, int, bool>)lstbx_GUIUsages.SelectedItem;
                usagepkg = FileListExtended[sf.Item1].Item1;
                contentdir = FileListExtended[sf.Item1].Item2;
                usageexp = sf.Item2;
            }
            else if (lstbx_Lines.SelectedIndex >= 0 && currentView == 8)
            {
                usagepkg = CurrentConvo.Item2;
                contentdir = CurrentConvo.Item4;
                usageexp = CurrentConvo.Item3;
            }
            else if (lstbx_Files.SelectedIndex >= 0 && currentView == 0)
            {
                var fileref = (Tuple<string, string>)lstbx_Files.SelectedItem;
                usagepkg = fileref.Item1;
                contentdir = fileref.Item2;
            }

            if (usagepkg == null)
            {
                MessageBox.Show("File not found.");
                return;
            }

            OpenInToolkit(tool, usagepkg, contentdir, usageexp);
        }
        private void OpenSourcePkg(object obj)
        {
            var cr = lstbx_Classes.SelectedItem as ClassRecord;
            var sourcepkg = cr.Definition_package;
            var sourceexp = cr.Definition_UID;

            int sourcedefaultUsage = cr.ClassUsages.FirstOrDefault(u => u.IsDefault == true).FileKey;

            if (sourcepkg == null || sourcedefaultUsage == 0)
            {
                MessageBox.Show("Definition file unknown.");
                return;
            }
            var contentdir = FileListExtended[sourcedefaultUsage].Item2;

            OpenInToolkit("PackageEditor", sourcepkg, contentdir, sourceexp);
        }
        private void OpenInToolkit(string tool, string filename, string contentdir, int export = 0)
        {
            string filePath = null;
            string rootPath = MEDirectories.GamePath(CurrentGame);

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
                    if (export != 0)
                    {
                        meshPlorer.LoadFile(filePath, export);
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
                    if (export != 0)
                    {
                        packEditor.LoadFile(filePath, export);
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
                    var file = CurrentDataBase.FileList[convo.ConvFile.Item1];
                    CurrentConvo = new Tuple<string, string, int, string, bool>(convo.ConvName, file.Item1, convo.ConvFile.Item2, CurrentDataBase.ContentDir[file.Item2], convo.IsAmbient);
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
            bool showmesh = false;
            if (btn_MeshRenderToggle.IsChecked == true && (lstbx_Meshes.SelectedIndex >= 0) && CurrentDataBase.Meshes[lstbx_Meshes.SelectedIndex].MeshUsages.Count > 0 && currentView == 3)
            {
                showmesh = true;
            }

            if (!showmesh)
            {
                MeshRendererTab_MeshRenderer.UnloadExport();
                meshPcc?.Dispose();
                return;
            }
            string rootPath = MEDirectories.GamePath(CurrentGame);
            var selecteditem = lstbx_Meshes.SelectedItem as MeshRecord;
            var filekey = selecteditem.MeshUsages[0].Item1;
            var filename = CurrentDataBase.FileList[filekey].Item1;
            var cdir = CurrentDataBase.ContentDir[CurrentDataBase.FileList[filekey].Item2];

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
                var uexpIdx = selecteditem.MeshUsages[0].Item2;
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
            bool showText = false;
            if (btn_TextRenderToggle.IsChecked == true && (lstbx_Textures.SelectedIndex >= 0) && CurrentDataBase.Textures[lstbx_Textures.SelectedIndex].TextureUsages.Count > 0 && currentView == 4)
            {
                showText = true;
            }

            var selecteditem = lstbx_Textures.SelectedItem as TextureRecord;
            if (!showText || selecteditem.CFormat == "TextureCube")
            {
                EmbeddedTextureViewerTab_EmbeddedTextureViewer.UnloadExport();
                BIKExternalExportLoaderTab_BIKExternalExportLoader.UnloadExport();
                EmbeddedTextureViewerTab_EmbeddedTextureViewer.Visibility = Visibility.Visible;
                BIKExternalExportLoaderTab_BIKExternalExportLoader.Visibility = Visibility.Collapsed;
                textPcc?.Dispose();
                return;
            }

            var filekey = selecteditem.TextureUsages[0].Item1;
            var filename = CurrentDataBase.FileList[filekey].Item1;
            string rootPath = MEDirectories.GamePath(CurrentGame);
            var cdir = CurrentDataBase.ContentDir[CurrentDataBase.FileList[filekey].Item2];
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
                var uexpIdx = selecteditem.TextureUsages[0].Item2;
                if (uexpIdx <= textPcc.ExportCount)
                {
                    var textExp = textPcc.GetUExport(uexpIdx);
                    string cubemapParent = textExp.Parent.ClassName == "CubeMap" ? selecteditem.TextureName.Substring(textExp.Parent.ObjectName.ToString().Length + 1) : null;
                    string indexedName = $"{textExp.ObjectNameString}_{textExp.indexValue - 1}";
                    if (textExp.ClassName.StartsWith("Texture") && (textExp.ObjectNameString == selecteditem.TextureName || selecteditem.TextureName == indexedName || textExp.ObjectNameString == cubemapParent))
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
            bool showAudio = false;
            if (btn_LinePlaybackToggle.IsChecked == true && (lstbx_Lines.SelectedIndex >= 0) && CurrentConvo.Item1 != null && CurrentGame != MEGame.ME1 && currentView == 8)
            {
                showAudio = true;
            }

            if (!showAudio)
            {
                SoundpanelWPF_ADB.UnloadExport();
                audioPcc?.Dispose();
                return;
            }

            var selecteditem = lstbx_Lines.SelectedItem as ConvoLine;
            var filename = CurrentConvo.Item2;
            var cdir = CurrentConvo.Item4;
            string rootPath = MEDirectories.GamePath(CurrentGame);
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

            string searchWav = $"{selecteditem.StrRef.ToString()}_m";
            if (genderTabs.SelectedIndex == 1)
                searchWav = $"{selecteditem.StrRef.ToString()}_f";

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
            var anim = lstbx_Anims.SelectedItem as Animation;
            if (anim != null)
            {
                if (!Application.Current.Windows.OfType<AnimationExplorer.AnimationViewer>().Any())
                {
                    AnimationExplorer.AnimationViewer av = new AnimationExplorer.AnimationViewer(CurrentDataBase, anim);
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
            if (lstbx_Anims.SelectedItem is Animation anim && anim.AnimUsages.Any())
            {
                var (fileListIndex, animUIndex, isMod) = anim.AnimUsages[0];
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
            if (lstbx_Anims.SelectedItem is Animation anim && anim.AnimUsages.Any())
            {
                (int fileListIndex, int animUIndex, bool isMod) = anim.AnimUsages[0];
                string filePath = GetFilePath(fileListIndex);
                var animImporter = new AnimationImporter(filePath, animUIndex);
                animImporter.Show();
                animImporter.Activate();
            }
        }
        private string GetFilePath(int fileListIndex)
        {
            (string filename, string contentdir) = FileListExtended[fileListIndex];
            return Directory.GetFiles(MEDirectories.GamePath(CurrentGame), $"{filename}.*", SearchOption.AllDirectories).FirstOrDefault(f => f.Contains(contentdir));
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
            var cr = d as ClassRecord;
            bool showthis = true;
            if (!string.IsNullOrEmpty(FilterBox.Text) && cr != null)
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
            if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !cr.ClassUsages.Select(c => c.FileKey).Intersect(CustomFileList.Keys).Any())
            {
                showthis = false;
            }
            return showthis;
        }
        bool MaterialFilter(object d)
        {
            var mr = d as Material;
            bool showthis = true;
            if (!string.IsNullOrEmpty(FilterBox.Text) && mr != null)
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
            if (showthis && menu_fltrMatUnlit.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "LightingModel" && x.Item3 == "MLM_Unlit"))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatSkM.IsChecked && mr.MatSettings.Any(x => x.Item1 == "bUsedWithSkeletalMesh" && x.Item3 == "True"))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMat2side.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "TwoSided" && x.Item3 == "True"))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMat1side.IsChecked && mr.MatSettings.Any(x => x.Item1 == "TwoSided" && x.Item3 == "True"))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatNoDLC.IsChecked && mr.IsDLCOnly)
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatTrans.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "BlendMode" && x.Item3 == "BLEND_Translucent"))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatAdd.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "BlendMode" && x.Item3 == "BLEND_Additive"))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatOpq.IsChecked && mr.MatSettings.Any(x => x.Item1 == "BlendMode" && (x.Item3 == "BLEND_Additive" || x.Item3 == "BLEND_Translucent")))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatColor.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "VectorParameter" && x.Item2.ToLower().Contains("color")))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatText.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "TextureSampleParameter2D"))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatTalk.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "ScalarParameter" && x.Item2.ToLower().Contains("talk")))
            {
                showthis = false;
            }
            if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !mr.MaterialUsages.Select(tuple => tuple.Item1).Intersect(CustomFileList.Keys).Any())
            {
                showthis = false;
            }
            return showthis;
        }
        bool MeshFilter(object d)
        {
            var mr = d as MeshRecord;
            bool showthis = true;
            if (!string.IsNullOrEmpty(FilterBox.Text) && mr != null)
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
            if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !mr.MeshUsages.Select(tuple => tuple.Item1).Intersect(CustomFileList.Keys).Any())
            {
                showthis = false;
            }
            return showthis;
        }
        bool AnimFilter(object d)
        {
            var ar = d as Animation;
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
            if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !ar.AnimUsages.Select(tuple => tuple.Item1).Intersect(CustomFileList.Keys).Any())
            {
                showthis = false;
            }
            return showthis;
        }
        bool PSFilter(object d)
        {
            var ps = d as ParticleSys;
            bool showthis = true;
            if (!string.IsNullOrEmpty(FilterBox.Text) && ps != null)
            {
                showthis = ps.PSName.ToLower().Contains(FilterBox.Text.ToLower());
            }
            if (showthis && menu_VFXPartSys.IsChecked && ps.VFXType != ParticleSys.VFXClass.ParticleSystem)
            {
                showthis = false;
            }
            if (showthis && menu_VFXRvrEff.IsChecked && ps.VFXType == ParticleSys.VFXClass.ParticleSystem)
            {
                showthis = false;
            }
            if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !ps.PSUsages.Select(tuple => tuple.Item1).Intersect(CustomFileList.Keys).Any())
            {
                showthis = false;
            }
            return showthis;
        }
        bool TexFilter(object d)
        {
            var tr = d as TextureRecord;
            bool showthis = true;
            if (!string.IsNullOrEmpty(FilterBox.Text) && tr != null)
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
            if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !tr.TextureUsages.Select(tuple => tuple.Item1).Intersect(CustomFileList.Keys).Any())
            {
                showthis = false;
            }
            return showthis;
        }
        bool SFFilter(object d)
        {
            var sf = d as GUIElement;
            bool showthis = true;
            if (!string.IsNullOrEmpty(FilterBox.Text) && sf != null)
            {
                showthis = sf.GUIName.ToLower().Contains(FilterBox.Text.ToLower());
            }
            if (showthis && IsFilteredByFiles && !CustomFileList.IsEmpty() && !sf.GUIUsages.Select(tuple => tuple.Item1).Intersect(CustomFileList.Keys).Any())
            {
                showthis = false;
            }
            return showthis;
        }
        bool LineFilter(object d)
        {
            var line = d as ConvoLine;
            bool showthis = true;
            if (showthis && cmbbx_filterSpkrs.SelectedIndex >= 0)
            {
                showthis = line.Speaker.ToLower() == cmbbx_filterSpkrs.SelectedItem.ToString().ToLower();
            }
            if (showthis && !string.IsNullOrEmpty(FilterBox.Text) && line != null)
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
        private bool FileFilter(object d)
        {
            bool showthis = true;
            var f = (Tuple<string, string>)d;
            var t = FilterBox.Text;
            if (!string.IsNullOrEmpty(t))
            {
                showthis = f.Item1.ToLower().Contains(t.ToLower());
                if (!showthis)
                {
                    showthis = f.Item2.ToLower().Contains(t.ToLower());
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
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
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

            SaveFileDialog d = new SaveFileDialog
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
            OpenFileDialog d = new OpenFileDialog
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
                    if (parts[0] != null && parts[1] != null)
                    {
                        var key = FileListExtended.IndexOf(new Tuple<string, string>(parts[0], parts[1]));
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
                        var c = lstbx_Usages.SelectedItem as ClassUsage;
                        FileKey = c.FileKey;
                    }
                    else if (lstbx_MatUsages.SelectedIndex >= 0 && currentView == 2)
                    {
                        var m = lstbx_MatUsages.SelectedItem as Tuple<int, int, bool>;
                        FileKey = m.Item1;
                    }
                    else if (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3)
                    {
                        var s = lstbx_MeshUsages.SelectedItem as Tuple<int, int, bool>;
                        FileKey = s.Item1;

                    }
                    else if (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 4)
                    {
                        var t = lstbx_TextureUsages.SelectedItem as Tuple<int, int, bool, bool>;
                        FileKey = t.Item1;
                    }
                    else if (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 5)
                    {
                        var a = lstbx_AnimUsages.SelectedItem as Tuple<int, int, bool>;
                        FileKey = a.Item1;

                    }
                    else if (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 6)
                    {
                        var ps = lstbx_PSUsages.SelectedItem as Tuple<int, int, bool, bool>;
                        FileKey = ps.Item1;
                    }
                    else if (lstbx_GUIUsages.SelectedIndex >= 0 && currentView == 7)
                    {
                        var sf = (Tuple<int, int, bool>)lstbx_GUIUsages.SelectedItem;
                        FileKey = sf.Item1;
                    }
                    else if (lstbx_Lines.SelectedIndex >= 0 && currentView == 8)
                    {
                        FileKey = FileListExtended.FindIndex(f => f.Item1 == CurrentConvo.Item2);
                    }
                    else if (lstbx_Files.SelectedIndex >= 0 && currentView == 0)
                    {
                        foreach (var fr in lstbx_Files.SelectedItems)
                        {
                            var fileref = (Tuple<string, string>)fr;
                            FileKey = FileListExtended.IndexOf(fileref);
                            if (!CustomFileList.ContainsKey(FileKey))
                            {
                                var file = FileListExtended[FileKey];
                                CustomFileList.Add(FileKey, $"{file.Item1} {file.Item2}");
                            }
                        }
                        FileKey = -1;
                    }
                    if (!expander_CustomFiles.IsExpanded)
                        expander_CustomFiles.IsExpanded = true;
                    if (FileKey >= 0 && !CustomFileList.ContainsKey(FileKey))
                    {
                        var file = FileListExtended[FileKey];
                        CustomFileList.Add(FileKey, $"{file.Item1} {file.Item2}");
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
        #endregion

        #region Scan
        private async void ScanGame()
        {

            string outputDir = CurrentDBPath;
            if (CurrentDBPath == null)
            {
                outputDir = App.AppDataFolder;
            }
            string rootPath = MEDirectories.GamePath(CurrentGame);

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
            TopDock.IsEnabled = false;
            MidDock.IsEnabled = false;
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;
            BusyBarInd = false;
            CurrentOverallOperationText = $"Generating Database...";
            bool scanCRC = menu_checkCRC.IsChecked;

            //Clear database
            ClearDataBase();
            CurrentDataBase.GenerationDate = DateTime.Now.ToString();
            CurrentDataBase.DataBaseversion = dbCurrentBuild;
            ClearGenerationDictionaries();

            _dbqueue = new BlockingCollection<ClassRecord>();

            //Background Consumer to compile Class records for subsequent class readings
            dbworker = new BackgroundWorker();
            dbworker.DoWork += DBProcessor;
            dbworker.RunWorkerCompleted += dbworker_RunWorkerCompleted;
            dbworker.WorkerSupportsCancellation = true;
            dbworker.RunWorkerAsync();

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
                CurrentDataBase.FileList.Add(new Tuple<string, int>(Path.GetFileNameWithoutExtension(f), dirkey));
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

            ProcessingQueue.Complete(); // Signal completion
            CommandManager.InvalidateRequerySuggested();
            await ProcessingQueue.Completion;
            isProcessing = true;

            if (DumpCanceled)
            {
                DumpCanceled = false;
                BusyHeader = $"Dump canceled. Processing Queue.";
            }
            else
            {
                OverallProgressValue = 100;
                OverallProgressMaximum = 100;
                BusyHeader = "Dump completed. Processing Queue.";
            }
            _dbqueue.CompleteAdding();
            TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
        }
        private async void dbworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            BusyHeader = "Collating and sorting the database";
            BusyText = "Please wait...";
            BusyBarInd = true;
            dbworker.CancelAsync();
            CommandManager.InvalidateRequerySuggested();

            PropsDataBase pdb = await CollateDataBase();
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

            foreach (var f in CurrentDataBase.FileList)
            {
                var cd = CurrentDataBase.ContentDir[f.Item2];
                FileListExtended.Add(new Tuple<string, string>(f.Item1, cd));
            }

            ClearGenerationDictionaries();
            isProcessing = false;
            SaveDatabase();
            IsBusy = false;
            TopDock.IsEnabled = true;
            MidDock.IsEnabled = true;
            MessageBox.Show("Done");

            if (CurrentGame != MEGame.ME1 && ParseConvos)
            {
                GetConvoLinesBackground();
            }
        }
        private void DBProcessor(object sender, DoWorkEventArgs e) //Background worker to clean up class data.
        {

            foreach (ClassRecord record in _dbqueue.GetConsumingEnumerable(CancellationToken.None))
            {
                try
                {
                    var oldClassRecord = GeneratedClasses.FirstOrDefault(r => r.Key == record.Class).Value;

                    if (record.SuperClass != null && oldClassRecord.SuperClass == null)
                    {
                        oldClassRecord.SuperClass = record.SuperClass;
                    }
                    if (record.Definition_package != null && oldClassRecord.Definition_package == null)
                    {
                        oldClassRecord.Definition_package = record.Definition_package;
                        oldClassRecord.Definition_UID = record.Definition_UID;
                    }

                    foreach (var r in record.PropertyRecords)
                    {
                        var proprecord = oldClassRecord.PropertyRecords.FirstOrDefault(p => p.Property == r.Property);
                        if (proprecord == null)
                        {
                            oldClassRecord.PropertyRecords.Add(r);
                        }
                    }

                    oldClassRecord.ClassUsages.AddRange(record.ClassUsages);

                    if (isProcessing)
                    {
                        BusyHeader = $"Processing Queue. {_dbqueue.Count}";
                    }

                }
                catch (Exception err)
                {
                    MessageBox.Show($"Error writing. {record.Class} {record.PropertyRecords[0].Property} {record.ClassUsages[0].FileKey} {err}");
                }
            }
        }
        private void CancelDump(object obj)
        {
            DumpCanceled = true;
            if (AllDumpingItems != null)
            {
                AllDumpingItems.ForEach(x => x.DumpCanceled = true);
            }

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
            GeneratedValueChecker.Clear();
        }
        private async Task<PropsDataBase> CollateDataBase()
        {


            //Add Lines / Convos don't need sorting
            var linessorted = Task<ObservableCollectionExtended<ConvoLine>>.Factory.StartNew(() =>
            {
                var l = new ObservableCollectionExtended<ConvoLine>();
                l.AddRange(GeneratedLines.Values);
                l.Sort(x => x.StrRef);
                return l;
            });

            //Add and sort Classes
            var classSorted = Task<ObservableCollectionExtended<ClassRecord>>.Factory
                .StartNew(() =>
                {
                    var classes = new ObservableCollectionExtended<ClassRecord>();
                    classes.AddRange(GeneratedClasses.Values);
                    classes.Sort(x => x.Class);
                    foreach (var c in classes)
                    {
                        c.PropertyRecords.Sort(x => x.Property);
                    }
                    return classes;
                });

            //Add animations
            var animsorted = Task<ObservableCollectionExtended<Animation>>.Factory
                .StartNew(() =>
                {
                    var anims = new ObservableCollectionExtended<Animation>();
                    anims.AddRange(GeneratedAnims.Values);
                    anims.Sort(x => x.AnimSequence);
                    return anims;
                });

            //Add Materials
            var matsorted = Task<ObservableCollectionExtended<Material>>.Factory.StartNew(() =>
            {
                var mats = new ObservableCollectionExtended<Material>();
                mats.AddRange(GeneratedMats.Values);
                mats.Sort(x => x.MaterialName);
                return mats;
            });

            //Add Meshes
            var mshsorted = Task<ObservableCollectionExtended<MeshRecord>>.Factory.StartNew(() =>
            {
                var m = new ObservableCollectionExtended<MeshRecord>();
                m.AddRange(GeneratedMeshes.Values);
                m.Sort(x => x.MeshName);
                return m;
            });

            //Add Particles
            var pssorted = Task<ObservableCollectionExtended<ParticleSys>>.Factory.StartNew(() =>
            {
                var p = new ObservableCollectionExtended<ParticleSys>();
                p.AddRange(GeneratedPS.Values);
                p.Sort(x => x.PSName);
                return p;
            });

            //Add Textures
            var txtsorted = Task<ObservableCollectionExtended<TextureRecord>>.Factory.StartNew(() =>
            {
                var t = new ObservableCollectionExtended<TextureRecord>();
                t.AddRange(GeneratedText.Values);
                t.Sort(x => x.TextureName);
                return t;
            });


            //Add GUI
            var guisorted = Task<ObservableCollectionExtended<GUIElement>>.Factory.StartNew(() =>
            {
                var g = new ObservableCollectionExtended<GUIElement>();
                g.AddRange(GeneratedGUI.Values);
                g.Sort(x => x.GUIName);
                return g;
            });

            var pdb = new PropsDataBase();
            pdb.Conversations.AddRange(GeneratedConvo.Values);
            await Task.WhenAll(classSorted, animsorted, matsorted, mshsorted, pssorted, txtsorted, guisorted, linessorted);

            pdb.ClassRecords.AddRange(classSorted.Result);
            pdb.Animations.AddRange(animsorted.Result);
            pdb.Materials.AddRange(matsorted.Result);
            pdb.Meshes.AddRange(mshsorted.Result);
            pdb.Particles.AddRange(pssorted.Result);
            pdb.Textures.AddRange(txtsorted.Result);
            pdb.GUIElements.AddRange(guisorted.Result);
            pdb.Lines.AddRange(linessorted.Result);
            return pdb;
        }

        #endregion

    }
    #region Database
    /// <summary>
    /// Database Classes
    /// </summary>
    /// 
    public class PropsDataBase : NotifyPropertyChangedBase
    {
        public MEGame meGame { get; set; }
        public string GenerationDate { get; set; }
        public string DataBaseversion { get; set; }
        public Dictionary<AssetDB.dbTableType, bool> dbTable { get; } = new Dictionary<AssetDB.dbTableType, bool>()
        {
            { AssetDB.dbTableType.Master, false },
            { AssetDB.dbTableType.Class, false },
            { AssetDB.dbTableType.Materials, false },
            { AssetDB.dbTableType.Meshes, false },
            { AssetDB.dbTableType.Textures, false },
            { AssetDB.dbTableType.Animations, false },
            { AssetDB.dbTableType.Particles, false },
            { AssetDB.dbTableType.GUIElements, false },
            { AssetDB.dbTableType.Convos, false },
            { AssetDB.dbTableType.Lines, false }
        };
        public ObservableCollectionExtended<Tuple<string, int>> FileList { get; } = new ObservableCollectionExtended<Tuple<string, int>>(); //filename and key to contentdir
        public List<string> ContentDir { get; } = new List<string>();
        public ObservableCollectionExtended<ClassRecord> ClassRecords { get; } = new ObservableCollectionExtended<ClassRecord>();
        public ObservableCollectionExtended<Material> Materials { get; } = new ObservableCollectionExtended<Material>();
        public ObservableCollectionExtended<Animation> Animations { get; } = new ObservableCollectionExtended<Animation>();
        public ObservableCollectionExtended<MeshRecord> Meshes { get; } = new ObservableCollectionExtended<MeshRecord>();
        public ObservableCollectionExtended<ParticleSys> Particles { get; } = new ObservableCollectionExtended<ParticleSys>();
        public ObservableCollectionExtended<TextureRecord> Textures { get; } = new ObservableCollectionExtended<TextureRecord>();
        public ObservableCollectionExtended<GUIElement> GUIElements { get; } = new ObservableCollectionExtended<GUIElement>();
        public ObservableCollectionExtended<Conversation> Conversations { get; } = new ObservableCollectionExtended<Conversation>();
        public ObservableCollectionExtended<ConvoLine> Lines { get; } = new ObservableCollectionExtended<ConvoLine>();
        public PropsDataBase(MEGame meGame, string GenerationDate, string DataBaseversion, ObservableCollectionExtended<Tuple<string, int>> FileList, List<string> ContentDir, ObservableCollectionExtended<ClassRecord> ClassRecords, ObservableCollectionExtended<Material> Materials,
            ObservableCollectionExtended<Animation> Animations, ObservableCollectionExtended<MeshRecord> Meshes, ObservableCollectionExtended<ParticleSys> Particles, ObservableCollectionExtended<TextureRecord> Textures, ObservableCollectionExtended<GUIElement> GUIElements,
            ObservableCollectionExtended<Conversation> Conversations, ObservableCollectionExtended<ConvoLine> Lines)
        {
            this.meGame = meGame;
            this.GenerationDate = GenerationDate;
            this.DataBaseversion = DataBaseversion;
            this.FileList.AddRange(FileList);
            this.ContentDir.AddRange(ContentDir);
            this.ClassRecords.AddRange(ClassRecords);
            this.Materials.AddRange(Materials);
            this.Animations.AddRange(Animations);
            this.Meshes.AddRange(Meshes);
            this.Particles.AddRange(Particles);
            this.Textures.AddRange(Textures);
            this.GUIElements.AddRange(GUIElements);
            this.Conversations.AddRange(Conversations);
            this.Lines.AddRange(Lines);
        }

        public PropsDataBase()
        { }

    }
    public class ClassRecord : NotifyPropertyChangedBase
    {
        private string _Class;
        public string Class { get => _Class; set => SetProperty(ref _Class, value); }
        public string Definition_package { get; set; }
        public int Definition_UID { get; set; }
        public string SuperClass { get; set; }
        private bool _IsModOnly;
        public bool IsModOnly { get => _IsModOnly; set => SetProperty(ref _IsModOnly, value); }
        public ObservableCollectionExtended<PropertyRecord> PropertyRecords { get; } = new ObservableCollectionExtended<PropertyRecord>();
        public ObservableCollectionExtended<ClassUsage> ClassUsages { get; } = new ObservableCollectionExtended<ClassUsage>();

        public ClassRecord(string Class, string Definition_package, int Definition_UID, string SuperClass, ObservableCollectionExtended<PropertyRecord> PropertyRecords, ObservableCollectionExtended<ClassUsage> ClassUsages)
        {
            this.Class = Class;
            this.Definition_package = Definition_package;
            this.Definition_UID = Definition_UID;
            this.SuperClass = SuperClass;
            this.PropertyRecords.AddRange(PropertyRecords);
            this.ClassUsages.AddRange(ClassUsages);
        }

        public ClassRecord()
        { }
    }
    public class PropertyRecord : NotifyPropertyChangedBase
    {
        private string _Property;
        public string Property { get => _Property; set => SetProperty(ref _Property, value); }
        private string _Type;
        public string Type { get => _Type; set => SetProperty(ref _Type, value); }

        public PropertyRecord(string Property, string Type)
        {
            this.Property = Property;
            this.Type = Type;
        }
        public PropertyRecord()
        { }
    }
    public class ClassUsage : NotifyPropertyChangedBase
    {
        public int FileKey { get; set; }
        public int ExportUID { get; set; }
        public bool IsDefault { get; set; }
        private bool _IsMod;
        public bool IsMod { get => _IsMod; set => SetProperty(ref _IsMod, value); }
        public ClassUsage(int FileKey, int ExportUID, bool IsDefault, bool IsMod)
        {
            this.FileKey = FileKey;
            this.ExportUID = ExportUID;
            this.IsDefault = IsDefault;
            this.IsMod = IsMod;
        }
        public ClassUsage()
        { }
    }
    public class Material : NotifyPropertyChangedBase
    {
        private string _MaterialName;
        public string MaterialName { get => _MaterialName; set => SetProperty(ref _MaterialName, value); }
        private string _ParentPackagee;
        public string ParentPackage { get => _ParentPackagee; set => SetProperty(ref _ParentPackagee, value); }
        private bool _IsDLCOnly;
        public bool IsDLCOnly { get => _IsDLCOnly; set => SetProperty(ref _IsDLCOnly, value); }
        public ObservableCollectionExtended<Tuple<int, int, bool>> MaterialUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool>>(); //File reference, export, isDLC file

        public ObservableCollectionExtended<Tuple<string, string, string>> MatSettings { get; } = new ObservableCollectionExtended<Tuple<string, string, string>>();

        public Material(string MaterialName, string ParentPackage, bool IsDLCOnly, ObservableCollectionExtended<Tuple<int, int, bool>> MaterialUsages, ObservableCollectionExtended<Tuple<string, string, string>> MatSettings)
        {
            this.MaterialName = MaterialName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.MaterialUsages.AddRange(MaterialUsages);
            this.MatSettings.AddRange(MatSettings);
        }

        public Material()
        { }
    }
    public class Animation : NotifyPropertyChangedBase
    {
        private string _AnimSequence;
        public string AnimSequence { get => _AnimSequence; set => SetProperty(ref _AnimSequence, value); }
        private string _SeqName;
        public string SeqName { get => _SeqName; set => SetProperty(ref _SeqName, value); }
        private string _AnimData;
        public string AnimData { get => _AnimData; set => SetProperty(ref _AnimData, value); }
        private float _Length;
        public float Length { get => _Length; set => SetProperty(ref _Length, value); }
        private int _Frames;
        public int Frames { get => _Frames; set => SetProperty(ref _Frames, value); }
        private string _Compression;
        public string Compression { get => _Compression; set => SetProperty(ref _Compression, value); }
        private string _KeyFormat;
        public string KeyFormat { get => _KeyFormat; set => SetProperty(ref _KeyFormat, value); }
        private bool _IsAmbPerf;
        public bool IsAmbPerf { get => _IsAmbPerf; set => SetProperty(ref _IsAmbPerf, value); }
        private bool _IsModOnly;
        public bool IsModOnly { get => _IsModOnly; set => SetProperty(ref _IsModOnly, value); }
        public ObservableCollectionExtended<Tuple<int, int, bool>> AnimUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool>>(); //File reference then export

        public Animation(string AnimSequence, string SeqName, string AnimData, float Length, int Frames, string Compression, string KeyFormat, bool IsAmbPerf, bool IsModOnly, ObservableCollectionExtended<Tuple<int, int, bool>> AnimUsages)
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
            this.AnimUsages.AddRange(AnimUsages);
        }

        public Animation()
        { }
    }
    public class MeshRecord : NotifyPropertyChangedBase
    {
        private string _MeshName;
        public string MeshName { get => _MeshName; set => SetProperty(ref _MeshName, value); }
        private bool _IsSkeleton;
        public bool IsSkeleton { get => _IsSkeleton; set => SetProperty(ref _IsSkeleton, value); }
        private int _BoneCount;
        public int BoneCount { get => _BoneCount; set => SetProperty(ref _BoneCount, value); }
        private bool _IsModOnly;
        public bool IsModOnly { get => _IsModOnly; set => SetProperty(ref _IsModOnly, value); }
        public ObservableCollectionExtended<Tuple<int, int, bool>> MeshUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool>>(); //File reference then export

        public MeshRecord(string MeshName, bool IsSkeleton, bool IsModOnly, int BoneCount, ObservableCollectionExtended<Tuple<int, int, bool>> MeshUsages)
        {
            this.MeshName = MeshName;
            this.IsSkeleton = IsSkeleton;
            this.BoneCount = BoneCount;
            this.IsModOnly = IsModOnly;
            this.MeshUsages.AddRange(MeshUsages);
        }

        public MeshRecord()
        { }
    }
    public class ParticleSys : NotifyPropertyChangedBase
    {
        public enum VFXClass
        {
            ParticleSystem,
            RvrClientEffect,
            BioVFXTemplate
        }

        private string _PSName;
        public string PSName { get => _PSName; set => SetProperty(ref _PSName, value); }
        private string _ParentPackagee;
        public string ParentPackage { get => _ParentPackagee; set => SetProperty(ref _ParentPackagee, value); }
        private bool _IsDLCOnly;
        public bool IsDLCOnly { get => _IsDLCOnly; set => SetProperty(ref _IsDLCOnly, value); }
        private bool _IsModOnly;
        public bool IsModOnly { get => _IsModOnly; set => SetProperty(ref _IsModOnly, value); }
        private int _EffectCount;
        public int EffectCount { get => _EffectCount; set => SetProperty(ref _EffectCount, value); }
        private VFXClass _vfxType;
        public VFXClass VFXType { get => _vfxType; set => SetProperty(ref _vfxType, value); }
        public ObservableCollectionExtended<Tuple<int, int, bool, bool>> PSUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool, bool>>(); //File reference, export, isDLC file

        public ParticleSys(string PSName, string ParentPackage, bool IsDLCOnly, bool IsModOnly, int EffectCount, VFXClass VFXType, ObservableCollectionExtended<Tuple<int, int, bool, bool>> PSUsages)
        {
            this.PSName = PSName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.IsModOnly = IsModOnly;
            this.EffectCount = EffectCount;
            this.VFXType = VFXType;
            this.PSUsages.AddRange(PSUsages);
        }

        public ParticleSys()
        { }
    }
    public class TextureRecord : NotifyPropertyChangedBase
    {
        private string _TextureName;
        public string TextureName { get => _TextureName; set => SetProperty(ref _TextureName, value); }
        private string _ParentPackage;
        public string ParentPackage { get => _ParentPackage; set => SetProperty(ref _ParentPackage, value); }
        private bool _IsDLCOnly;
        public bool IsDLCOnly { get => _IsDLCOnly; set => SetProperty(ref _IsDLCOnly, value); }
        private bool _IsModOnly;
        public bool IsModOnly { get => _IsModOnly; set => SetProperty(ref _IsModOnly, value); }
        private string _CFormat;
        public string CFormat { get => _CFormat; set => SetProperty(ref _CFormat, value); }
        private string _TexGrp;
        public string TexGrp { get => _TexGrp; set => SetProperty(ref _TexGrp, value); }
        private int _SizeX;
        public int SizeX { get => _SizeX; set => SetProperty(ref _SizeX, value); }
        private int _SizeY;
        public int SizeY { get => _SizeY; set => SetProperty(ref _SizeY, value); }
        private string _CRC;
        public string CRC { get => _CRC; set => SetProperty(ref _CRC, value); }
        public ObservableCollectionExtended<Tuple<int, int, bool, bool>> TextureUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool, bool>>(); //File reference, then export, isDLC file, isMod file

        public TextureRecord(string TextureName, string ParentPackage, bool IsDLCOnly, bool IsModOnly, string CFormat, string TexGrp, int SizeX, int SizeY, string CRC, ObservableCollectionExtended<Tuple<int, int, bool, bool>> TextureUsages)
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
            this.TextureUsages.AddRange(TextureUsages);
        }

        public TextureRecord()
        { }
    }
    public class GUIElement : NotifyPropertyChangedBase
    {
        private string _GUIName;
        public string GUIName { get => _GUIName; set => SetProperty(ref _GUIName, value); }
        private int _DataSize;
        public int DataSize { get => _DataSize; set => SetProperty(ref _DataSize, value); }
        private bool _IsModOnly;
        public bool IsModOnly { get => _IsModOnly; set => SetProperty(ref _IsModOnly, value); }
        public ObservableCollectionExtended<Tuple<int, int, bool>> GUIUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool>>(); //File reference then export

        public GUIElement(string GUIName, int DataSize, bool IsModOnly, ObservableCollectionExtended<Tuple<int, int, bool>> GUIUsages)
        {
            this.GUIName = GUIName;
            this.DataSize = DataSize;
            this.IsModOnly = IsModOnly;
            this.GUIUsages.AddRange(GUIUsages);
        }

        public GUIElement()
        { }
    }
    public class Conversation : NotifyPropertyChangedBase
    {
        private string _ConvName;
        public string ConvName { get => _ConvName; set => SetProperty(ref _ConvName, value); }
        private bool _IsAmbient;
        public bool IsAmbient { get => _IsAmbient; set => SetProperty(ref _IsAmbient, value); }
        private Tuple<int, int> _convFile; //file, export
        public Tuple<int, int> ConvFile { get => _convFile; set => SetProperty(ref _convFile, value); }
        public Conversation(string ConvName, bool IsAmbient, Tuple<int, int> ConvFile)
        {
            this.ConvName = ConvName;
            this.IsAmbient = IsAmbient;
            this.ConvFile = ConvFile;
        }

        public Conversation()
        { }
    }
    public class ConvoLine : NotifyPropertyChangedBase
    {
        private int _strRef;
        public int StrRef { get => _strRef; set => SetProperty(ref _strRef, value); }
        private string _speaker;
        public string Speaker { get => _speaker; set => SetProperty(ref _speaker, value); }
        private string _line;
        public string Line { get => _line; set => SetProperty(ref _line, value); }
        private string _convo;
        public string Convo { get => _convo; set => SetProperty(ref _convo, value); }

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
            var listofFiles = values[1] as ObservableCollectionExtended<Tuple<string, string>>;
            if (listofFiles == null || fileindex == 0 || listofFiles.Count == 0)
            {
                return $"Error file name not found";
            }
            var export = (int)values[2];
            var file = listofFiles[fileindex];
            return $"{file.Item1}  # {export}   {file.Item2} ";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null; //not needed
        }
    }

    #endregion

    #region SingleFileScan
    public class ClassScanSingleFileTask : NotifyPropertyChangedBase
    {

        private string _shortFileName;
        public string ShortFileName
        {
            get => _shortFileName;
            set => SetProperty(ref _shortFileName, value);
        }

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
                foreach (ExportEntry exp in pcc.Exports)
                {
                    if (DumpCanceled || (pcc.FilePath.Contains("_LOC_") && !pcc.FilePath.Contains("INT"))
                    ) //TEMP NEED BETTER WAY TO HANDLE LANGUAGES
                    {
                        return;
                    }

                    try
                    {
                        string pClass = exp.ClassName; //Handle basic class record
                        string pExp = exp.ObjectName.Instanced;
                        string pKey = exp.InstancedFullPath.ToLower();
                        string pSuperClass = null;
                        string pDefinitionPackage = null;
                        int pDefUID = 0;
                        int pExportUID = exp.UIndex;
                        bool pIsdefault = false; //Setup default cases

                        if (exp.ClassName != "Class")
                        {
                            if (exp.IsDefaultObject)
                            {
                                pIsdefault = true;
                            }

                            var pList = new ObservableCollectionExtended<PropertyRecord>();
                            var mSets = new ObservableCollectionExtended<Tuple<string, string, string>>();
                            var props = exp.GetProperties(false, false);
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
                                        if (pIsdefault)
                                        {
                                            pValue = pint.Value.ToString();
                                        }
                                        else
                                        {
                                            pValue = "int"; //Keep DB size down
                                        }

                                        break;
                                    case FloatProperty pflt:
                                        if (pIsdefault)
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
                                        if (pIsdefault)
                                        {
                                            pValue = pstr;
                                        }
                                        else
                                        {
                                            pValue = "string";
                                        }

                                        break;
                                    case StringRefProperty pstrref:
                                        if (pIsdefault)
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

                                if (exp.ClassName == "Material" && !dbScanner.GeneratedMats.ContainsKey(pExp) &&
                                    !pIsdefault) //Run material settings
                                {
                                    var pSet = new Tuple<string, string, string>(null, null, null);
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

                                                        pSet = new Tuple<string, string, string>(exprsnName, paramName,
                                                            defscalar);
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

                                                        pSet = new Tuple<string, string, string>(exprsnName, paramName,
                                                            linearColor);
                                                        break;
                                                    default:
                                                        pSet = new Tuple<string, string, string>(exprsnName, paramName,
                                                            null);
                                                        break;
                                                }

                                                mSets.Add(pSet);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        pSet = new Tuple<string, string, string>(matSet_name, pType, pValue);
                                        mSets.Add(pSet);
                                    }
                                }
                            }

                            var NewUsageRecord = new ClassUsage(FileKey, pExportUID, pIsdefault, IsMod);
                            var NewClassRecord = new ClassRecord(pClass, pDefinitionPackage, pDefUID, pSuperClass,
                                pList, new ObservableCollectionExtended<ClassUsage>() { NewUsageRecord });
                            string valueKey = string.Concat(pClass, ShortFileName, pIsdefault.ToString());
                            if (!dbScanner.GeneratedClasses.TryAdd(pClass, NewClassRecord) &&
                                dbScanner.GeneratedValueChecker.TryAdd(valueKey, true))
                            {
                                dbScanner._dbqueue.Add(NewClassRecord);

                            }

                            if ((exp.ClassName == "Material" || exp.ClassName == "DecalMaterial") && !pIsdefault)
                            {
                                if (dbScanner.GeneratedMats.ContainsKey(pKey))
                                {
                                    var eMat = dbScanner.GeneratedMats[pKey];
                                    lock (eMat)
                                    {
                                        eMat.MaterialUsages.Add(new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC));
                                        if (eMat.IsDLCOnly)
                                        {
                                            eMat.IsDLCOnly = IsDLC;
                                        }
                                    }
                                }
                                else
                                {

                                }

                                string parent = null;
                                if (GameBeingDumped == MEGame.ME1 && File.EndsWith(".upk"))
                                {
                                    parent = ShortFileName;
                                }
                                else
                                {
                                    parent = GetTopParentPackage(exp);
                                }

                                string matname = pExp;
                                if (exp.ClassName == "DecalMaterial" && !matname.Contains("Decal"))
                                {
                                    matname = $"{pExp}_Decal";
                                }

                                var NewMat = new Material(matname, parent, IsDLC,
                                    new ObservableCollectionExtended<Tuple<int, int, bool>>()
                                        {new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC)}, mSets);
                                dbScanner.GeneratedMats.TryAdd(pKey, NewMat);
                            }
                            else if ((exp.ClassName == "AnimSequence" || exp.ClassName == "SFXAmbPerfGameData") &&
                                     !pIsdefault)
                            {
                                if (dbScanner.GeneratedAnims.ContainsKey(pKey))
                                {
                                    var anim = dbScanner.GeneratedAnims[pKey];
                                    lock (anim)
                                    {
                                        anim.AnimUsages.Add(new Tuple<int, int, bool>(FileKey, pExportUID, IsMod));
                                        if (anim.IsModOnly)
                                        {
                                            anim.IsModOnly = IsMod;
                                        }
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
                                    if (exp.ClassName == "AnimSequence")
                                    {
                                        var pSeq = exp.GetProperty<NameProperty>("SequenceName");
                                        if (pSeq != null)
                                        {
                                            aSeq = pSeq.Value.Instanced;
                                            aGrp = pExp.Replace($"{aSeq}_", null);
                                        }

                                        var pLength = exp.GetProperty<FloatProperty>("SequenceLength");
                                        aLength = pLength?.Value ?? 0;

                                        var pFrames = exp.GetProperty<IntProperty>("NumFrames");
                                        aFrames = pFrames?.Value ?? 0;

                                        var pComp = exp.GetProperty<EnumProperty>("RotationCompressionFormat");
                                        aComp = pComp?.Value.ToString() ?? "None";

                                        var pKeyF = exp.GetProperty<EnumProperty>("KeyEncodingFormat");
                                        aKeyF = pKeyF?.Value.ToString() ?? "None";
                                    }
                                    else //is ambient performance
                                    {
                                        IsAmbPerf = true;
                                        aSeq = "Multiple";
                                        var pAnimsets = exp.GetProperty<ArrayProperty<StructProperty>>("m_aAnimsets");
                                        aFrames = pAnimsets?.Count ?? 0;
                                    }

                                    var NewAnim = new Animation(pExp, aSeq, aGrp, aLength, aFrames, aComp, aKeyF,
                                        IsAmbPerf, IsMod,
                                        new ObservableCollectionExtended<Tuple<int, int, bool>>()
                                            {new Tuple<int, int, bool>(FileKey, pExportUID, IsMod)});
                                    dbScanner.GeneratedAnims.TryAdd(pKey, NewAnim);
                                }
                            }
                            else if ((exp.ClassName == "SkeletalMesh" || exp.ClassName == "StaticMesh") && !pIsdefault)
                            {
                                if (dbScanner.GeneratedMeshes.ContainsKey(pKey))
                                {
                                    var mr = dbScanner.GeneratedMeshes[pKey];
                                    lock (mr)
                                    {
                                        mr.MeshUsages.Add(new Tuple<int, int, bool>(FileKey, pExportUID, IsMod));
                                        if (mr.IsModOnly)
                                        {
                                            mr.IsModOnly = IsMod;
                                        }
                                    }
                                }
                                else
                                {
                                    bool IsSkel = exp.ClassName == "SkeletalMesh";
                                    int bones = 0;
                                    if (IsSkel)
                                    {
                                        var bin = ObjectBinary.From<SkeletalMesh>(exp);
                                        bones = bin?.RefSkeleton.Length ?? 0;
                                    }

                                    var NewMeshRec = new MeshRecord(pExp, IsSkel, IsMod, bones,
                                        new ObservableCollectionExtended<Tuple<int, int, bool>>
                                            {new Tuple<int, int, bool>(FileKey, pExportUID, IsMod)});
                                    dbScanner.GeneratedMeshes.TryAdd(pKey, NewMeshRec);
                                }
                            }
                            else if ((exp.ClassName == "ParticleSystem" || exp.ClassName == "RvrClientEffect" ||
                                      exp.ClassName == "BioVFXTemplate") && !pIsdefault)
                            {
                                if (dbScanner.GeneratedPS.ContainsKey(pKey))
                                {
                                    var ePS = dbScanner.GeneratedPS[pKey];
                                    lock (ePS)
                                    {
                                        ePS.PSUsages.Add(
                                            new Tuple<int, int, bool, bool>(FileKey, pExportUID, IsDLC, IsMod));
                                        if (ePS.IsDLCOnly)
                                        {
                                            ePS.IsDLCOnly = IsDLC;
                                        }

                                        if (ePS.IsModOnly)
                                        {
                                            ePS.IsModOnly = IsMod;
                                        }
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
                                        parent = GetTopParentPackage(exp);
                                    }

                                    var vfxtype = ParticleSys.VFXClass.BioVFXTemplate;
                                    int EmCnt = 0;
                                    if (exp.ClassName == "ParticleSystem")
                                    {
                                        var EmtProp = exp.GetProperty<ArrayProperty<ObjectProperty>>("Emitters");
                                        EmCnt = EmtProp?.Count ?? 0;
                                        vfxtype = ParticleSys.VFXClass.ParticleSystem;
                                    }
                                    else if (exp.ClassName == "RvrClientEffect")
                                    {
                                        var RvrProp = exp.GetProperty<ArrayProperty<ObjectProperty>>("m_lstModules");
                                        EmCnt = RvrProp?.Count ?? 0;
                                        vfxtype = ParticleSys.VFXClass.RvrClientEffect;
                                    }

                                    var NewPS = new ParticleSys(pExp, parent, IsDLC, IsMod, EmCnt, vfxtype,
                                        new ObservableCollectionExtended<Tuple<int, int, bool, bool>>
                                            {new Tuple<int, int, bool, bool>(FileKey, pExportUID, IsDLC, IsMod)});
                                    dbScanner.GeneratedPS.TryAdd(pKey, NewPS);
                                }
                            }
                            else if ((exp.ClassName == "Texture2D" || exp.ClassName == "TextureCube" ||
                                      exp.ClassName == "TextureMovie") && !pIsdefault)
                            {
                                if (exp.Parent?.ClassName == "TextureCube")
                                {
                                    pExp = $"{exp.Parent.ObjectName}_{pExp}";
                                }

                                if (dbScanner.GeneratedText.ContainsKey(pKey))
                                {
                                    var t = dbScanner.GeneratedText[pKey];
                                    lock (t)
                                    {
                                        t.TextureUsages.Add(
                                            new Tuple<int, int, bool, bool>(FileKey, pExportUID, IsDLC, IsMod));
                                        if (t.IsDLCOnly)
                                        {
                                            t.IsDLCOnly = IsDLC;
                                        }

                                        if (t.IsModOnly)
                                        {
                                            t.IsModOnly = IsMod;
                                        }
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
                                        parent = GetTopParentPackage(exp);
                                    }

                                    string pformat = "TextureCube";
                                    int psizeX = 0;
                                    int psizeY = 0;
                                    string cRC = "n/a";
                                    string texgrp = "n/a";
                                    if (exp.ClassName != "TextureCube")
                                    {
                                        pformat = "TextureMovie";
                                        if (exp.ClassName != "TextureMovie")
                                        {
                                            var formp = exp.GetProperty<EnumProperty>("Format");
                                            pformat = formp?.Value.Name ?? "n/a";
                                            pformat = pformat.Replace("PF_", string.Empty);
                                            var tgrp = exp.GetProperty<EnumProperty>("LODGroup");
                                            texgrp = tgrp?.Value.Instanced ?? "n/a";
                                            texgrp = texgrp.Replace("TEXTUREGROUP_", string.Empty);
                                            texgrp = texgrp.Replace("_", string.Empty);
                                            if (ScanCRC)
                                            {
                                                cRC = Texture2D.GetTextureCRC(exp).ToString("X8");
                                            }
                                        }

                                        var propX = exp.GetProperty<IntProperty>("SizeX");
                                        psizeX = propX?.Value ?? 0;
                                        var propY = exp.GetProperty<IntProperty>("SizeY");
                                        psizeY = propY?.Value ?? 0;
                                    }

                                    var NewTex = new TextureRecord(pExp, parent, IsDLC, IsMod, pformat, texgrp, psizeX,
                                        psizeY, cRC,
                                        new ObservableCollectionExtended<Tuple<int, int, bool, bool>>()
                                            {new Tuple<int, int, bool, bool>(FileKey, pExportUID, IsDLC, IsMod)});
                                    dbScanner.GeneratedText.TryAdd(pKey, NewTex);
                                }
                            }
                            else if ((exp.ClassName == "GFxMovieInfo" || exp.ClassName == "BioSWF") && !pIsdefault)
                            {
                                if (dbScanner.GeneratedGUI.ContainsKey(pKey))
                                {
                                    var eGUI = dbScanner.GeneratedGUI[pKey];
                                    lock (eGUI)
                                    {
                                        eGUI.GUIUsages.Add(new Tuple<int, int, bool>(FileKey, pExportUID, IsMod));
                                        if (eGUI.IsModOnly)
                                        {
                                            eGUI.IsModOnly = IsMod;
                                        }
                                    }
                                }
                                else
                                {
                                    string dataPropName = exp.ClassName == "GFxMovieInfo" ? "RawData" : "Data";
                                    var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                                    int datasize = rawData?.Count ?? 0;
                                    var NewGUI = new GUIElement(pExp, datasize, IsMod,
                                        new ObservableCollectionExtended<Tuple<int, int, bool>>
                                            {new Tuple<int, int, bool>(FileKey, pExportUID, IsMod)});
                                    dbScanner.GeneratedGUI.TryAdd(pKey, NewGUI);
                                }
                            }
                            else if (ScanLines && exp.ClassName == "BioConversation" && !pIsdefault)
                            {
                                if (!dbScanner.GeneratedConvo.ContainsKey(pExp))
                                {
                                    bool IsAmbient = true;
                                    var speakers = new List<string>() { "Shepard", "Owner" };
                                    if (exp.Game != MEGame.ME3)
                                    {
                                        var s_speakers = props.GetProp<ArrayProperty<StructProperty>>("m_SpeakerList");
                                        if (s_speakers != null)
                                        {
                                            for (int id = 0; id < s_speakers.Count; id++)
                                            {
                                                var sspkr = s_speakers[id].GetProp<NameProperty>("sSpeakerTag")
                                                    .ToString();
                                                speakers.Add(sspkr);
                                            }
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

                                        ConvoLine newLine = new ConvoLine(linestrref, speakers[speakerindex], pExp);
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

                                            ConvoLine newLine = new ConvoLine(linestrref, "Shepard", pExp);
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

                                    var NewConv = new Conversation(pExp, IsAmbient,
                                        new Tuple<int, int>(FileKey, pExportUID));
                                    dbScanner.GeneratedConvo.TryAdd(pKey, NewConv);
                                }
                            }
                        }
                        else
                        {
                            pClass = exp.ObjectName;
                            pSuperClass = exp.SuperClassName;
                            pDefUID = exp.UIndex;
                            var NewUsageRecord = new ClassUsage(FileKey, pExportUID, pIsdefault, IsMod);
                            var NewPropertyRecord = new PropertyRecord("None", "NoneProperty");
                            var NewClassRecord = new ClassRecord(pClass, ShortFileName, pDefUID, pSuperClass,
                                new ObservableCollectionExtended<PropertyRecord> { NewPropertyRecord },
                                new ObservableCollectionExtended<ClassUsage> { NewUsageRecord });
                            if (!dbScanner.GeneratedClasses.TryAdd(pExp, NewClassRecord))
                            {
                                dbScanner._dbqueue.Add(NewClassRecord);
                            }
                        }
                    }
                    catch (Exception e) when (!App.IsDebug)
                    {
                        MessageBox.Show(
                            $"Exception Bug detected in single file: {exp.FileRef.FilePath} Export:{exp.UIndex}");
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error dumping package file {File}. See the inner exception for details.", e);
            }
        }


        private string GetTopParentPackage(IEntry export)
        {
            string toppackage = null;
            if (export.HasParent)
            {
                toppackage = GetTopParentPackage(export.Parent);
            }
            else
            {
                toppackage = export.ObjectName;
            }
            return toppackage;
        }
    }

    #endregion

}
