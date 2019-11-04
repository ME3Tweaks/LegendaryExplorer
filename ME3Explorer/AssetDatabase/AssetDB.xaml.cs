using ME3Explorer.Dialogue_Editor;
using ME3Explorer.Packages;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.Classes;
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
using System.Windows.Media;
using Microsoft.AppCenter.Analytics;

namespace ME3Explorer.AssetDatabase
{


    /// <summary>
    /// Interaction logic for AssetDB
    /// </summary>
    public partial class AssetDB : WPFBase
    {
        #region Declarations
        public const string dbCurrentBuild = "2.1"; //If changes are made that invalidate old databases edit this.
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
        private string CurrentDBPath { get; set; }
        public PropsDataBase CurrentDataBase { get; } = new PropsDataBase(MEGame.Unknown, null, null, new ObservableCollectionExtended<Tuple<string, int>>(), new List<string>(), new ObservableCollectionExtended<ClassRecord>(), new ObservableCollectionExtended<Material>(),
            new ObservableCollectionExtended<Animation>(), new ObservableCollectionExtended<MeshRecord>(), new ObservableCollectionExtended<ParticleSys>(), new ObservableCollectionExtended<TextureRecord>());
        public ObservableCollectionExtended<Tuple<string, string>> FileListExtended { get; } = new ObservableCollectionExtended<Tuple<string, string>>();
        /// <summary>
        /// Dictionary that stores generated classes
        /// </summary>
        public ConcurrentDictionary<String, ClassRecord> GeneratedClasses = new ConcurrentDictionary<String, ClassRecord>();
        /// <summary>
        /// Dictionary that stores generated Animations
        /// </summary>
        public ConcurrentDictionary<String, Animation> GeneratedAnims = new ConcurrentDictionary<String, Animation>();
        /// <summary>
        /// Dictionary that stores generated Materials
        /// </summary>
        public ConcurrentDictionary<String, Material> GeneratedMats = new ConcurrentDictionary<String, Material>();
        /// <summary>
        /// Dictionary that stores generated Meshes
        /// </summary>
        public ConcurrentDictionary<String, MeshRecord> GeneratedMeshes = new ConcurrentDictionary<String, MeshRecord>();
        /// <summary>
        /// Dictionary that stores generated Particle Systems
        /// </summary>
        public ConcurrentDictionary<String, ParticleSys> GeneratedPS = new ConcurrentDictionary<String, ParticleSys>();
        /// <summary>
        /// Dictionary that stores generated Textures
        /// </summary>
        public ConcurrentDictionary<String, TextureRecord> GeneratedText = new ConcurrentDictionary<String, TextureRecord>();
        /// <summary>
        /// Used to check if values generated are unique.
        /// </summary>
        public ConcurrentDictionary<String, Boolean> GeneratedValueChecker = new ConcurrentDictionary<String, Boolean>();

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
        private BlockingCollection<PropsDataBase> deserializingQueue = new BlockingCollection<PropsDataBase>();
        /// <summary>
        /// Cancelation of dumping
        /// </summary>
        private bool DumpCanceled;

        /// <summary>
        /// used to switch queue countdown on
        /// </summary>
        public bool isProcessing;

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
            set => SetProperty(ref _overallProgressValue, value);
        }

        private int _overallProgressMaximum;
        public int OverallProgressMaximum
        {
            get => _overallProgressMaximum;
            set => SetProperty(ref _overallProgressMaximum, value);
        }
        private IMEPackage meshPcc;
        private IMEPackage textPcc;
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        public ICommand GenerateDBCommand { get; set; }
        public ICommand SaveDBCommand { get; set; }
        public ICommand SwitchMECommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }
        public ICommand OpenSourcePkgCommand { get; set; }
        public ICommand GoToSuperclassCommand { get; set; }
        public ICommand OpenUsagePkgCommand { get; set; }
        public ICommand FilterClassCommand { get; set; }
        public ICommand FilterMatCommand { get; set; }
        public ICommand FilterMeshCommand { get; set; }
        public ICommand FilterTexCommand { get; set; }
        public ICommand SetCRCCommand { get; set; }
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
                || (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3) || (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 6) || (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 4) || currentView == 0;
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
        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //Not applicable
        }

        #endregion

        #region Startup/Exit

        public AssetDB()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Asset Database", new WeakReference(this));
            Analytics.TrackEvent("Used tool", new Dictionary<string, string>()
            {
                { "Toolname", "Asset Database" }
            });
            LoadCommands();

            //Get default db / gane
            CurrentDBPath = Properties.Settings.Default.AssetDBPath;
            Enum.TryParse<MEGame>(Properties.Settings.Default.AssetDBGame, out MEGame game);
            currentGame = game;

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
            SwitchMECommand = new RelayCommand(SwitchGame);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
            OpenSourcePkgCommand = new RelayCommand(OpenSourcePkg, IsClassSelected);
            GoToSuperclassCommand = new RelayCommand(GoToSuperClass, IsClassSelected);
            OpenUsagePkgCommand = new RelayCommand(OpenUsagePkg, IsUsageSelected);
            SetCRCCommand = new RelayCommand(SetCRCScan);
        }
        private void AssetDB_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentOverallOperationText = "Starting Up";
            BusyHeader = "Loading database";
            BusyText = "Please wait...";
            IsBusy = true;
            BusyBarInd = true;

            if (CurrentDBPath != null && CurrentDBPath.EndsWith("zip") && File.Exists(CurrentDBPath) && currentGame != MEGame.Unknown && currentGame != MEGame.UDK)
            {
                SwitchGame(currentGame.ToString());
            }
            else
            {
                CurrentDBPath = null;
                SwitchGame(MEGame.ME3.ToString());
            }
            Activate();
        }
        private void AssetDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            Properties.Settings.Default.AssetDBPath = CurrentDBPath;
            Properties.Settings.Default.AssetDBGame = currentGame.ToString();
            EmbeddedTextureViewerTab_EmbededTextureViewer.UnloadExport();
            BIKExternalExportLoaderTab_BIKExternalExportLoader.UnloadExport();
            MeshRendererTab_MeshRenderer.UnloadExport();
            meshPcc?.Dispose();
            textPcc?.Dispose();
        }

        #endregion

        #region Database I/O        
        public async void LoadDatabase()
        {

            CurrentOverallOperationText = "Loading database";
            BusyHeader = "Loading database";
            BusyText = "Please wait...";
            BusyBarInd = true;
            IsBusy = true;

            var start = DateTime.UtcNow;
            var build = dbCurrentBuild.Trim(new Char[] { ' ', '*', '.' });
            ////Async load
            PropsDataBase pdb = await ParseDBAsync(currentGame, CurrentDBPath, build);
            CurrentDataBase.meGame = pdb.meGame;
            CurrentDataBase.GenerationDate = pdb.GenerationDate;
            CurrentDataBase.DataBaseversion = pdb.DataBaseversion;
            CurrentDataBase.FileList.AddRange(pdb.FileList);
            CurrentDataBase.ContentDir.AddRange(pdb.ContentDir);
            CurrentDataBase.ClassRecords.AddRange(pdb.ClassRecords);
            CurrentDataBase.Materials.AddRange(pdb.Materials);
            CurrentDataBase.Animations.AddRange(pdb.Animations);
            CurrentDataBase.Meshes.AddRange(pdb.Meshes);
            CurrentDataBase.Particles.AddRange(pdb.Particles);
            CurrentDataBase.Textures.AddRange(pdb.Textures);

            if (CurrentDataBase.DataBaseversion == null || CurrentDataBase.DataBaseversion != dbCurrentBuild)
            {

                var warn = MessageBox.Show($"This database is out of date (v {CurrentDataBase.DataBaseversion} versus v {dbCurrentBuild})\nA new version is required. Do you wish to rebuild?", "Warning", MessageBoxButton.OKCancel);
                if (warn != MessageBoxResult.Cancel)
                {
                    GenerateDatabase();
                    return;
                }
                ClearDataBase();
                IsBusy = false;
                return;
            }

            foreach (var f in CurrentDataBase.FileList)
            {
                var cd = CurrentDataBase.ContentDir[f.Item2];
                FileListExtended.Add(new Tuple<string, string>(f.Item1, cd));
            }


            IsBusy = false;
            CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count} Meshes: {CurrentDataBase.Meshes.Count} Particles: { CurrentDataBase.Particles.Count} Textures: { CurrentDataBase.Textures.Count}";
#if DEBUG
            var end = DateTime.UtcNow;
            double length = (end - start).TotalMilliseconds;
            CurrentOverallOperationText = $"{CurrentOverallOperationText} LoadTime: {length}ms";
#endif
        }
        public async Task<PropsDataBase> ParseDBAsync(MEGame dbgame, string dbpath, string build)
        {
            deserializingQueue = new BlockingCollection<PropsDataBase>();
            
            try
            {
                await Task.Run(() =>
                {
                    Dictionary<string, ZipArchiveEntry> archiveEntries = new Dictionary<string, ZipArchiveEntry>();
                    using (ZipArchive archive = new ZipArchive((Stream)new FileStream(dbpath, FileMode.Open)))
                    {
                        if (archive.Entries.Any(e => e.Name == $"MasterDB{currentGame}_{build}.json" ))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string dbType = entry.Name.Substring(0, entry.Name.Length - 10);
                                if (entry.Name.StartsWith("Master"))
                                    dbType = "Master";
                                var ms = new MemoryStream();
                                using (Stream estream = entry.Open())
                                {
                                    estream.CopyTo(ms);
                                }
                                ms.Position = 0;
                                var unitTask = Task.Factory.StartNew(() => JsonFileParse(ms, dbType));
                            }

                        }
                        else //Wrong build - send dummy pdb back and ask user to refresh
                        {
                            PropsDataBase pdb = new PropsDataBase();
                            var entry = archive.Entries.FirstOrDefault(z => z.Name.StartsWith("Master"));
                            pdb.DataBaseversion = "pre 2.0";
                            if(entry != null)
                            {
                                using (Stream estream = entry.Open())
                                using (StreamReader sr = new StreamReader(estream))
                                using (JsonTextReader reader = new JsonTextReader(sr))
                                {
                                    var Serializer = new JsonSerializer();
                                    string oldDbver;
                                    while (reader.Read())
                                    {
                                        if (reader.TokenType == JsonToken.PropertyName
                                            && (string)reader.Value == "DataBaseversion")
                                        {
                                            reader.Read();

                                            oldDbver = Serializer.Deserialize<string>(reader);
                                            pdb.DataBaseversion = oldDbver;
                                            break;
                                        }
                                    }
                                }
                            }
                            
                            deserializingQueue.Add(pdb);
                            deserializingQueue.CompleteAdding();
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Console.WriteLine("Error ParseDB");
            }
            return await Task<PropsDataBase>.Run(() =>
            {
                var readData = new PropsDataBase();

                int n = 0;
                foreach (PropsDataBase pdb in deserializingQueue.GetConsumingEnumerable())
                {
                    n++;
                    switch (pdb.DataBaseversion)
                    {
                        case "Class":
                            readData.ClassRecords.AddRange(pdb.ClassRecords);
                            break;
                        case "Mat":
                            readData.Materials.AddRange(pdb.Materials);
                            break;
                        case "Anim":
                            readData.Animations.AddRange(pdb.Animations);
                            break;
                        case "Mesh":
                            readData.Meshes.AddRange(pdb.Meshes);
                            break;
                        case "Ps":
                            readData.Particles.AddRange(pdb.Particles);
                            break;
                        case "Txt":
                            readData.Textures.AddRange(pdb.Textures);
                            break;
                        default:
                            readData.meGame = pdb.meGame;
                            readData.GenerationDate = pdb.GenerationDate;
                            readData.DataBaseversion = pdb.DataBaseversion;
                            readData.FileList.AddRange(pdb.FileList);
                            readData.ContentDir.AddRange(pdb.ContentDir);
                            break;
                    }
                    if (n == 7) { deserializingQueue.CompleteAdding(); }
                }

                return readData;
            });
        }
        private void JsonFileParse(MemoryStream ms, string dbType)
        {
            
            PropsDataBase readData = new PropsDataBase();
            readData.DataBaseversion = dbType;
            using (StreamReader sr = new StreamReader(ms))
            {
                using (JsonTextReader reader = new JsonTextReader(sr))
                {
                    try
                    {
                        var Serializer = new JsonSerializer();
                        switch (dbType)
                        {
                            case "Master":
                                var mst = Serializer.Deserialize<PropsDataBase>(reader);
                                readData.meGame = mst.meGame;
                                readData.GenerationDate = mst.GenerationDate;
                                readData.DataBaseversion = mst.DataBaseversion;
                                readData.FileList.AddRange(mst.FileList);
                                readData.ContentDir.AddRange(mst.ContentDir);
                                break;
                            case "Class":
                                var cls = Serializer.Deserialize<ObservableCollectionExtended<ClassRecord>>(reader);
                                readData.ClassRecords.AddRange(cls);
                                break;
                            case "Mat":
                                var mats = Serializer.Deserialize<ObservableCollectionExtended<Material>>(reader);
                                readData.Materials.AddRange(mats);
                                break;
                            case "Anim":
                                var an = Serializer.Deserialize<ObservableCollectionExtended<Animation>>(reader);
                                readData.Animations.AddRange(an);
                                break;
                            case "Mesh":
                                var msh = Serializer.Deserialize<ObservableCollectionExtended<MeshRecord>>(reader);
                                readData.Meshes.AddRange(msh);
                                break;
                            case "Ps":
                                var ps = Serializer.Deserialize<ObservableCollectionExtended<ParticleSys>>(reader);
                                readData.Particles.AddRange(ps);
                                break;
                            case "Txt":
                                var txt = Serializer.Deserialize<ObservableCollectionExtended<TextureRecord>>(reader);
                                readData.Textures.AddRange(txt);
                                break;
                        }
                    }
                    catch
                    {
                        MessageBox.Show($"Failure deserializing type: {dbType}");
                    }
                }
            }

            deserializingQueue.Add(readData);
        }
        public async void SaveDatabase()
        {
            BusyHeader = "Saving database";
            BusyText = "Please wait...";
            BusyBarInd = true;
            IsBusy = true;
            CurrentOverallOperationText = $"Database saving...";

            //V2.1 split files
            var masterDB = new PropsDataBase(CurrentDataBase.meGame, CurrentDataBase.GenerationDate, CurrentDataBase.DataBaseversion, CurrentDataBase.FileList, CurrentDataBase.ContentDir, new ObservableCollectionExtended<ClassRecord>(), new ObservableCollectionExtended<Material>(),
            new ObservableCollectionExtended<Animation>(), new ObservableCollectionExtended<MeshRecord>(), new ObservableCollectionExtended<ParticleSys>(), new ObservableCollectionExtended<TextureRecord>());
            var masterSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(masterDB));
            var clsSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.ClassRecords));
            var mtlSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Materials));
            var animSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Animations));
            var mshSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Meshes));
            var psSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Particles));
            var txtSrl = Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(CurrentDataBase.Textures));


            using (var fileStream = new FileStream(CurrentDBPath, FileMode.Create))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
                {
                    await Task.WhenAll(masterSrl,clsSrl, mtlSrl, animSrl, mshSrl, psSrl, txtSrl);
                    var build = dbCurrentBuild.Trim(new Char[] { ' ', '*', '.' });
                    var masterjson = archive.CreateEntry($"MasterDB{currentGame}_{build}.json");
                    using (var entryStream = masterjson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(masterSrl.Result));
                    }
                    var classjson = archive.CreateEntry($"ClassDB{currentGame}.json");
                    using (var entryStream = classjson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(clsSrl.Result));
                    }
                    var matjson = archive.CreateEntry($"MatDB{currentGame}.json");
                    using (var entryStream = matjson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(mtlSrl.Result));
                    }
                    var animJson = archive.CreateEntry($"AnimDB{currentGame}.json");
                    using (var entryStream = animJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(animSrl.Result));
                    }
                    var mshJson = archive.CreateEntry($"MeshDB{currentGame}.json");
                    using (var entryStream = mshJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(mshSrl.Result));
                    }
                    var psJson = archive.CreateEntry($"PsDB{currentGame}.json");
                    using (var entryStream = psJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(psSrl.Result));
                    }
                    var txtJson = archive.CreateEntry($"TxtDB{currentGame}.json");
                    using (var entryStream = txtJson.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        await Task.Run(() => streamWriter.Write(txtSrl.Result));
                    }
                }
            }
            CurrentOverallOperationText = $"Database saved.";
            IsBusy = false;
            await Task.Delay(5000);
            CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count} Meshes: {CurrentDataBase.Meshes.Count} Particles: { CurrentDataBase.Particles.Count} Textures: { CurrentDataBase.Textures.Count}";
        }
        public void ClearDataBase()
        {
            CurrentDataBase.meGame = currentGame;
            CurrentDataBase.GenerationDate = null;
            CurrentDataBase.FileList.Clear();
            CurrentDataBase.ContentDir.Clear();
            CurrentDataBase.ClassRecords.ClearEx();
            CurrentDataBase.Animations.ClearEx();
            CurrentDataBase.Materials.ClearEx();
            CurrentDataBase.Meshes.ClearEx();
            CurrentDataBase.Particles.ClearEx();
            CurrentDataBase.Textures.ClearEx();
            FileListExtended.ClearEx();
            FilterBox.Clear();
            Filter();
        }
        #endregion

        #region UserCommands
        public void GenerateDatabase()
        {
            ScanGame();
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
            EmbeddedTextureViewerTab_EmbededTextureViewer.UnloadExport();
            textPcc?.Dispose();
            btn_TextRenderToggle.IsChecked = false;
            btn_TextRenderToggle.Content = "Toggle Texture Rendering";
            switch (p)
            {
                case "ME1":
                    currentGame = MEGame.ME1;
                    StatusBar_GameID_Text.Text = "ME1";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                    switchME1_menu.IsChecked = true;
                    break;
                case "ME2":
                    currentGame = MEGame.ME2;
                    StatusBar_GameID_Text.Text = "ME2";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Maroon);
                    switchME2_menu.IsChecked = true;
                    break;
                default:
                    currentGame = MEGame.ME3;
                    StatusBar_GameID_Text.Text = "ME3";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkSeaGreen);
                    switchME3_menu.IsChecked = true;
                    break;
            }
            CurrentDBPath = Path.Combine(App.AppDataFolder, $"AssetDB{currentGame}.zip");

            if (CurrentDBPath != null && File.Exists(CurrentDBPath))
            {
                LoadDatabase();
            }
            else
            {
                IsBusy = false;
                CurrentOverallOperationText = "No database found.";
            }
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
            if(FilterBox.Text != null)
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
                var s = lstbx_MeshUsages.SelectedItem as Tuple<int, int>;
                usagepkg = FileListExtended[s.Item1].Item1;
                contentdir = FileListExtended[s.Item1].Item2;
                usageexp = s.Item2;
            }
            else if (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 4)
            {
                var t = lstbx_TextureUsages.SelectedItem as Tuple<int, int, bool>;
                usagepkg = FileListExtended[t.Item1].Item1;
                contentdir = FileListExtended[t.Item1].Item2;
                usageexp = t.Item2;
            }
            else if (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 5)
            {
                var a = lstbx_AnimUsages.SelectedItem as Tuple<int, int>;
                usagepkg = FileListExtended[a.Item1].Item1;
                contentdir = FileListExtended[a.Item1].Item2;
                usageexp = a.Item2;
            }
            else if (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 6)
            {
                var ps = lstbx_PSUsages.SelectedItem as Tuple<int, int, bool>;
                usagepkg = FileListExtended[ps.Item1].Item1;
                contentdir = FileListExtended[ps.Item1].Item2;
                usageexp = ps.Item2;
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
            string rootPath = MEDirectories.GamePath(currentGame);

            if (rootPath == null || !Directory.Exists(rootPath))
            {
                MessageBox.Show($"{currentGame} has not been found. Please check your ME3Explorer settings");
                return;
            }

            filename = $"{filename}.*";
            filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).Where(f => f.Contains(contentdir)).FirstOrDefault();
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
            string rootPath = MEDirectories.GamePath(currentGame);
            var selecteditem = lstbx_Meshes.SelectedItem as MeshRecord;
            var filekey = selecteditem.MeshUsages[0].Item1;
            var filename = CurrentDataBase.FileList[filekey].Item1;
            var cdir = CurrentDataBase.ContentDir[CurrentDataBase.FileList[filekey].Item2];

            if (rootPath == null)
            {
                MessageBox.Show($"{currentGame} has not been found. Please check your ME3Explorer settings");
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
                EmbeddedTextureViewerTab_EmbededTextureViewer.UnloadExport();
                BIKExternalExportLoaderTab_BIKExternalExportLoader.UnloadExport();
                textPcc?.Dispose();
                return;
            }
            
            var filekey = selecteditem.TextureUsages[0].Item1;
            var filename = CurrentDataBase.FileList[filekey].Item1;
            string rootPath = MEDirectories.GamePath(currentGame);
            var cdir = CurrentDataBase.ContentDir[CurrentDataBase.FileList[filekey].Item2];
            if (rootPath == null)
            {
                MessageBox.Show($"{currentGame} has not been found. Please check your ME3Explorer settings");
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
                EmbeddedTextureViewerTab_EmbededTextureViewer.UnloadExport();
                BIKExternalExportLoaderTab_BIKExternalExportLoader.UnloadExport();
                textPcc.Dispose();
            }

            foreach (var filePath in files)  //handle cases of mods/dlc having same file.
            {
                bool isBaseFile = cdir.ToLower() == "biogame";
                bool isDLCFile = filePath.ToLower().Contains("dlc");
                if ( isBaseFile == isDLCFile )
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
                        if(selecteditem.CFormat == "TextureMovie")
                        {
                            BIKExternalExportLoaderTab_BIKExternalExportLoader.LoadExport(textExp);
                            BIKExternalExportLoaderTab_BIKExternalExportLoader.Visibility = Visibility.Visible;
                            EmbeddedTextureViewerTab_EmbededTextureViewer.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            EmbeddedTextureViewerTab_EmbededTextureViewer.LoadExport(textExp);
                            EmbeddedTextureViewerTab_EmbededTextureViewer.Visibility = Visibility.Visible;
                            BIKExternalExportLoaderTab_BIKExternalExportLoader.Visibility = Visibility.Collapsed;
                        }
                        break;
                    }
                }
                textPcc.Dispose();
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
        #endregion

        #region Filters
        bool ClassFilter(object d)
        {
            var cr = d as ClassRecord;
            bool showthis = true;
            if (!String.IsNullOrEmpty(FilterBox.Text) && cr != null)
            {
                showthis = cr.Class.ToLower().Contains(FilterBox.Text.ToLower());
            }
            if (showthis && menu_fltrSeq.IsChecked && (!cr.Class.ToLower().StartsWith("seq") && !cr.Class.ToLower().StartsWith("bioseq") && !cr.Class.ToLower().StartsWith("sfxseq") && !cr.Class.ToLower().StartsWith("rvrseq")))
            {
                showthis = false;
            }
            if (showthis && menu_fltrInterp.IsChecked && (!cr.Class.ToLower().StartsWith("interp") && !cr.Class.ToLower().StartsWith("bioevtsys") && !cr.Class.ToLower().Contains("interptrack")))
            {
                showthis = false;
            }
            return showthis;
        }
        bool MaterialFilter(object d)
        {
            var mr = d as Material;
            bool showthis = true;
            if (!String.IsNullOrEmpty(FilterBox.Text) && mr != null)
            {
                showthis = mr.MaterialName.ToLower().Contains(FilterBox.Text.ToLower());
                if(!showthis)
                {
                    showthis = mr.ParentPackage.ToLower().Contains(FilterBox.Text.ToLower());
                }
            }
            if (showthis && menu_fltrMatUnlit.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "LightingModel" && x.Item3 == "MLM_Unlit"))
            {
                showthis = false;
            }
            if (showthis && menu_fltrMatSkM.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "bUsedWithSkeletalMesh" && x.Item3 == "True"))
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
            return showthis;
        }
        bool MeshFilter(object d)
        {
            var mr = d as MeshRecord;
            bool showthis = true;
            if (!String.IsNullOrEmpty(FilterBox.Text) && mr != null)
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
            return showthis;
        }
        bool AnimFilter(object d)
        {
            var ar = d as Animation;
            bool showthis = true;
            if (!String.IsNullOrEmpty(FilterBox.Text) && ar != null)
            {
                showthis = ar.AnimSequence.ToLower().Contains(FilterBox.Text.ToLower());
            }

            return showthis;
        }
        bool PSFilter(object d)
        {
            var ps = d as ParticleSys;
            bool showthis = true;
            if (!String.IsNullOrEmpty(FilterBox.Text) && ps != null)
            {
                showthis = ps.PSName.ToLower().Contains(FilterBox.Text.ToLower());
            }

            return showthis;
        }
        bool TexFilter(object d)
        {
            var tr = d as TextureRecord;
            bool showthis = true;
            if (!String.IsNullOrEmpty(FilterBox.Text) && tr != null)
            {
                showthis = tr.TextureName.ToLower().Contains(FilterBox.Text.ToLower());
                if (!showthis)
                {
                    showthis = tr.CRC.ToLower().Contains(FilterBox.Text.ToLower());
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
            return showthis;
        }
        private bool FileFilter(object d)
        {
            bool showthis = true;
            var f = (Tuple<string, string>)d;
            var t = FilterBox.Text;
            if (!String.IsNullOrEmpty(t))
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
                    menu_T1024.IsChecked = !menu_T1024.IsChecked;
                    break;
                default:
                    break;
            }
            Filter();
        }
        private void FilterBox_KeyUp(object sender, KeyEventArgs e)
        {
            Filter();
        }
        private void files_ColumnHeader_Click(object sender, RoutedEventArgs e)
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

                    ICollectionView dataView = CollectionViewSource.GetDefaultView(lstbx_Files.ItemsSource);

                    string sortBy = "Item2";
                    if(headerClicked.Column.Header.ToString().StartsWith("File"))
                    {
                        sortBy = "Item1";
                    }

                    dataView.SortDescriptions.Clear();
                    dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
                    dataView.Refresh();
                    lstbx_Files.ItemsSource = dataView;
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
        #endregion

        #region Scan
        private async void ScanGame()
        {

            string outputDir = CurrentDBPath;
            if (CurrentDBPath == null)
            {
                outputDir = App.AppDataFolder;
            }
            string rootPath = MEDirectories.GamePath(currentGame);

            if (rootPath == null || !Directory.Exists(rootPath))
            {
                MessageBox.Show($"{currentGame} has not been found. Please check your ME3Explorer settings");
                return;
            }

            rootPath = Path.GetFullPath(rootPath);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc" };
            List<string> files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories).Where(s => supportedExtensions.Contains(Path.GetExtension(s.ToLower()))).ToList();

            await dumpPackages(files, currentGame);
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
            GeneratedClasses.Clear();
            GeneratedAnims.Clear();
            GeneratedMats.Clear();
            GeneratedMeshes.Clear();
            GeneratedPS.Clear();
            GeneratedText.Clear();
            GeneratedValueChecker.Clear();

            _dbqueue = new BlockingCollection<ClassRecord>(); //Reset queue for multiple operations

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
            BusyHeader = $"Generating database for {currentGame}";
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
                    BusyText = $"Scanned {OverallProgressValue}/{OverallProgressMaximum} files\n\nClasses: { GeneratedClasses.Count}\nAnimations: { GeneratedAnims.Count}\nMaterials: { GeneratedMats.Count}\nMeshes: { GeneratedMeshes.Count}\nParticles: { GeneratedPS.Count}\nTextures: { GeneratedText.Count}";
                    OverallProgressValue++; //Concurrency 
                    CurrentDumpingItems.Remove(x);
                });
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = App.CoreCount }); // App.CoreCount

            AllDumpingItems = new List<ClassScanSingleFileTask>();
            CurrentDumpingItems.ClearEx();
            foreach (var fkey in fileKeys)
            {
                var threadtask = new ClassScanSingleFileTask(fkey.Item2, fkey.Item1, scanCRC);
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
        }
        private void dbworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

            dbworker.CancelAsync();
            CommandManager.InvalidateRequerySuggested();

            //Add and sort Classes
            CurrentDataBase.ClassRecords.AddRange(GeneratedClasses.Values);
            CurrentDataBase.ClassRecords.Sort(x => x.Class);
            foreach (var c in CurrentDataBase.ClassRecords)
            {
                c.PropertyRecords.Sort(x => x.Property);
            }

            //Add animations
            CurrentDataBase.Animations.AddRange(GeneratedAnims.Values);
            CurrentDataBase.Animations.Sort(x => x.AnimSequence);

            //Add Materials
            CurrentDataBase.Materials.AddRange(GeneratedMats.Values);
            CurrentDataBase.Materials.Sort(x => x.MaterialName);

            //Add Meshes
            CurrentDataBase.Meshes.AddRange(GeneratedMeshes.Values);
            CurrentDataBase.Meshes.Sort(x => x.MeshName);

            //Add Particles
            CurrentDataBase.Particles.AddRange(GeneratedPS.Values);
            CurrentDataBase.Particles.Sort(x => x.PSName);

            //Add Textures
            CurrentDataBase.Textures.AddRange(GeneratedText.Values);
            CurrentDataBase.Textures.Sort(x => x.TextureName);

            foreach (var f in CurrentDataBase.FileList)
            {
                var cd = CurrentDataBase.ContentDir[f.Item2];
                FileListExtended.Add(new Tuple<string, string>(f.Item1, cd));
            }

            GeneratedClasses.Clear();
            GeneratedAnims.Clear();
            GeneratedMats.Clear();
            GeneratedMeshes.Clear();
            GeneratedPS.Clear();
            GeneratedText.Clear();
            GeneratedValueChecker.Clear();
            isProcessing = false;
            SaveDatabase();
            IsBusy = false;
            TopDock.IsEnabled = true;
            MidDock.IsEnabled = true;

            MessageBox.Show("Done");

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
        public ObservableCollectionExtended<Tuple<string, int>> FileList { get; } = new ObservableCollectionExtended<Tuple<string, int>>(); //filename and key to contentdir
        public List<string> ContentDir { get; } = new List<string>();
        public ObservableCollectionExtended<ClassRecord> ClassRecords { get; } = new ObservableCollectionExtended<ClassRecord>();
        public ObservableCollectionExtended<Material> Materials { get; } = new ObservableCollectionExtended<Material>();
        public ObservableCollectionExtended<Animation> Animations { get; } = new ObservableCollectionExtended<Animation>();
        public ObservableCollectionExtended<MeshRecord> Meshes { get; } = new ObservableCollectionExtended<MeshRecord>();
        public ObservableCollectionExtended<ParticleSys> Particles { get; } = new ObservableCollectionExtended<ParticleSys>();
        public ObservableCollectionExtended<TextureRecord> Textures { get; } = new ObservableCollectionExtended<TextureRecord>();
        public PropsDataBase(MEGame meGame, string GenerationDate, string DataBaseversion, ObservableCollectionExtended<Tuple<string, int>> FileList, List<string> ContentDir, ObservableCollectionExtended<ClassRecord> ClassRecords, ObservableCollectionExtended<Material> Materials,
            ObservableCollectionExtended<Animation> Animations, ObservableCollectionExtended<MeshRecord> Meshes, ObservableCollectionExtended<ParticleSys> Particles, ObservableCollectionExtended<TextureRecord> Textures)
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
        public ClassUsage(int FileKey, int ExportUID, bool IsDefault)
        {
            this.FileKey = FileKey;
            this.ExportUID = ExportUID;
            this.IsDefault = IsDefault;
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

        public ObservableCollectionExtended<Tuple<int, int>> AnimUsages { get; } = new ObservableCollectionExtended<Tuple<int, int>>(); //File reference then export

        public Animation(string AnimSequence, string SeqName, string AnimData, float Length, int Frames, string Compression, string KeyFormat, ObservableCollectionExtended<Tuple<int, int>> AnimUsages)
        {
            this.AnimSequence = AnimSequence;
            this.SeqName = SeqName;
            this.AnimData = AnimData;
            this.Length = Length;
            this.Frames = Frames;
            this.Compression = Compression;
            this.KeyFormat = KeyFormat;
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

        public ObservableCollectionExtended<Tuple<int, int>> MeshUsages { get; } = new ObservableCollectionExtended<Tuple<int, int>>(); //File reference then export

        public MeshRecord(string MeshName, bool IsSkeleton, int BoneCount, ObservableCollectionExtended<Tuple<int, int>> MeshUsages)
        {
            this.MeshName = MeshName;
            this.IsSkeleton = IsSkeleton;
            this.BoneCount = BoneCount;
            this.MeshUsages.AddRange(MeshUsages);
        }

        public MeshRecord()
        { }
    }
    public class ParticleSys : NotifyPropertyChangedBase
    {
        private string _PSName;
        public string PSName { get => _PSName; set => SetProperty(ref _PSName, value); }
        private string _ParentPackagee;
        public string ParentPackage { get => _ParentPackagee; set => SetProperty(ref _ParentPackagee, value); }
        private bool _IsDLCOnly;
        public bool IsDLCOnly { get => _IsDLCOnly; set => SetProperty(ref _IsDLCOnly, value); }
        private int _EmitterCount;
        public int EmitterCount { get => _EmitterCount; set => SetProperty(ref _EmitterCount, value); }
        public ObservableCollectionExtended<Tuple<int, int, bool>> PSUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool>>(); //File reference, export, isDLC file

        public ParticleSys(string PSName, string ParentPackage, bool IsDLCOnly, int EmitterCount, ObservableCollectionExtended<Tuple<int, int, bool>> PSUsages)
        {
            this.PSName = PSName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.EmitterCount = EmitterCount;
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
        private string _CFormat;
        public string CFormat { get => _CFormat; set => SetProperty(ref _CFormat, value); }
        private int _SizeX;
        public int SizeX { get => _SizeX; set => SetProperty(ref _SizeX, value); }
        private int _SizeY;
        public int SizeY { get => _SizeY; set => SetProperty(ref _SizeY, value); }
        private string _CRC;
        public string CRC { get => _CRC; set => SetProperty(ref _CRC, value); }
        public ObservableCollectionExtended<Tuple<int, int, bool>> TextureUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool>>(); //File reference, then export, isDLC file

        public TextureRecord(string TextureName, string ParentPackage, bool IsDLCOnly, string CFormat, int SizeX, int SizeY, string CRC, ObservableCollectionExtended<Tuple<int, int, bool>> TextureUsages)
        {
            this.TextureName = TextureName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.CFormat = CFormat;
            this.SizeX = SizeX;
            this.SizeY = SizeY;
            this.CRC = CRC;
            this.TextureUsages.AddRange(TextureUsages);
        }

        public TextureRecord()
        { }
    }
    public class FileIndexToNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            int fileindex = (Int32)values[0];
            var listofFiles = values[1] as ObservableCollectionExtended<Tuple<string, string>>;
            if (listofFiles == null || fileindex == 0 || listofFiles.Count == 0)
            {
                return $"Error file name not found";
            }
            var export = (Int32)values[2];
            var file = listofFiles[fileindex];
            return $"{file.Item1}  # {export}   {file.Item2} " ;
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

        public ClassScanSingleFileTask(string file, int filekey, bool scanCRC)
        {
            File = file;
            ShortFileName = Path.GetFileNameWithoutExtension(file);
            FileKey = filekey;
            ScanCRC = scanCRC;
        }

        public bool DumpCanceled;
        private readonly int FileKey;
        private readonly string File;
        private readonly bool ScanCRC;

        /// <summary>
        /// Dumps Property data to concurrent dictionary
        /// </summary>
        public void dumpPackageFile(MEGame GameBeingDumped, AssetDB dbScanner)
        {
            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(File);
            foreach (ExportEntry exp in pcc.Exports)
            {
                if (DumpCanceled)
                    return;
                try
                {
                    string pClass = exp.ClassName;  //Handle basic class record
                    string pExp = exp.ObjectName;
                    if (exp.indexValue > 0)
                    {
                        pExp = $"{pExp}_{exp.indexValue - 1}";
                    }
                    string pSuperClass = null;
                    string pDefinitionPackage = null;
                    int pDefUID = 0;
                    int pExportUID = exp.UIndex;
                    bool pIsdefault = false;  //Setup default cases

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

                            if (exp.ClassName == "Material" && !dbScanner.GeneratedMats.ContainsKey(pExp) && !pIsdefault) //Run material settings
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
                                            string exprsnName = exprsn.ClassName.Remove(0, 18);
                                            switch (exprsn.ClassName)
                                            {
                                                case "MaterialExpressionScalarParameter":
                                                    var sValue = exprsn.GetProperty<FloatProperty>("DefaultValue");
                                                    string defscalar = "n/a";
                                                    if (sValue != null)
                                                    {
                                                        defscalar = sValue.Value.ToString();
                                                    }
                                                    pSet = new Tuple<string, string, string>(exprsnName, paramName, defscalar);
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
                                                            linearColor = $"R:{r.Value} G:{g.Value} B:{b.Value} A:{a.Value}";
                                                        }
                                                    }

                                                    pSet = new Tuple<string, string, string>(exprsnName, paramName, linearColor);
                                                    break;
                                                default:
                                                    pSet = new Tuple<string, string, string>(exprsnName, paramName, null);
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

                        var NewUsageRecord = new ClassUsage(FileKey, pExportUID, pIsdefault);
                        var NewClassRecord = new ClassRecord(pClass, pDefinitionPackage, pDefUID, pSuperClass, pList, new ObservableCollectionExtended<ClassUsage>() { NewUsageRecord });
                        string valueKey = string.Concat(pClass, ShortFileName, pIsdefault.ToString());
                        if (!dbScanner.GeneratedClasses.TryAdd(pClass, NewClassRecord) && dbScanner.GeneratedValueChecker.TryAdd(valueKey, true))
                        {
                            dbScanner._dbqueue.Add(NewClassRecord);

                        }

                        if (exp.ClassName == "Material" && !pIsdefault)
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
                            bool IsDLC = pcc.IsInOfficialDLC();

                            var NewMat = new Material(pExp, parent, IsDLC, new ObservableCollectionExtended<Tuple<int, int, bool>>() { new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC) }, mSets);
                            if (!dbScanner.GeneratedMats.TryAdd(pExp, NewMat))
                            {
                                var eMat = dbScanner.GeneratedMats[pExp];
                                lock (eMat)
                                {
                                    eMat.MaterialUsages.Add(new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC));
                                    if (eMat.IsDLCOnly)
                                    {
                                        eMat.IsDLCOnly = IsDLC;
                                    }
                                }
                            }
                        }

                        if (exp.ClassName == "AnimSequence" && !pIsdefault)
                        {
                            string aSeq = null;
                            string aGrp = "None";
                            var pSeq = exp.GetProperty<NameProperty>("SequenceName");
                            if (pSeq != null)
                            {
                                aSeq = pSeq.Value;
                                aGrp = pExp.Replace($"{aSeq}_", null);
                            }

                            var pLength = exp.GetProperty<FloatProperty>("SequenceLength");
                            float aLength = pLength?.Value ?? 0;

                            var pFrames = exp.GetProperty<IntProperty>("NumFrames");
                            int aFrames = pFrames?.Value ?? 0;

                            var pComp = exp.GetProperty<EnumProperty>("RotationCompressionFormat");
                            string aComp = pComp?.Value.ToString() ?? "None";

                            var pKeyF = exp.GetProperty<EnumProperty>("KeyEncodingFormat");
                            string aKeyF = pKeyF?.Value.ToString() ?? "None";

                            var NewAnim = new Animation(pExp, aSeq, aGrp, aLength, aFrames, aComp, aKeyF, new ObservableCollectionExtended<Tuple<int, int>>() { new Tuple<int, int>(FileKey, pExportUID) });
                            if (!dbScanner.GeneratedAnims.TryAdd(pExp, NewAnim))
                            {
                                var anim = dbScanner.GeneratedAnims[pExp];
                                lock (anim)
                                {
                                    anim.AnimUsages.Add(new Tuple<int, int>(FileKey, pExportUID));
                                }
                            }
                        }

                        if ((exp.ClassName == "SkeletalMesh" || exp.ClassName == "StaticMesh") && !pIsdefault)
                        {
                            bool IsSkel = exp.ClassName == "SkeletalMesh";
                            int bones = 0;
                            if (IsSkel)
                            {
                                var bin = ObjectBinary.From<Unreal.BinaryConverters.SkeletalMesh>(exp);
                                bones = bin != null ? bin.RefSkeleton.Length : 0;
                            }
                            var NewMeshRec = new MeshRecord(pExp, IsSkel, bones, new ObservableCollectionExtended<Tuple<int, int>> { new Tuple<int, int>(FileKey, pExportUID) });
                            if (!dbScanner.GeneratedMeshes.TryAdd(pExp, NewMeshRec))
                            {
                                var mr = dbScanner.GeneratedMeshes[pExp];
                                lock (mr)
                                {
                                    mr.MeshUsages.Add(new Tuple<int, int>(FileKey, pExportUID));
                                }

                            }
                        }

                        if (exp.ClassName == "ParticleSystem" && !pIsdefault)
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

                            bool IsDLC = pcc.IsInOfficialDLC();
                            var EmtProp = exp.GetProperty<ArrayProperty<ObjectProperty>>("Emitters");
                            int EmCnt = EmtProp != null ? EmtProp.Count : 0;
                            var NewPS = new ParticleSys(pExp, parent, IsDLC, EmCnt, new ObservableCollectionExtended<Tuple<int, int, bool>>() { new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC) });
                            if (!dbScanner.GeneratedPS.TryAdd(pExp, NewPS))
                            {
                                var ePS = dbScanner.GeneratedPS[pExp];
                                lock (ePS)
                                {
                                    ePS.PSUsages.Add(new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC));
                                    if (ePS.IsDLCOnly)
                                    {
                                        ePS.IsDLCOnly = IsDLC;
                                    }
                                }
                            }
                        }

                        if ((exp.ClassName == "Texture2D" || exp.ClassName == "TextureCube" || exp.ClassName == "TextureMovie") && !pIsdefault)
                        {
                            bool IsDLC = pcc.IsInOfficialDLC();
                            if (exp.Parent?.ClassName == "TextureCube")
                            {
                                pExp = $"{exp.Parent.ObjectName}_{pExp}";
                            }

                            if (dbScanner.GeneratedText.ContainsKey(pExp))
                            {
                                var t = dbScanner.GeneratedText[pExp];
                                lock (t)
                                {
                                    t.TextureUsages.Add(new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC));
                                    if (t.IsDLCOnly)
                                    {
                                        t.IsDLCOnly = IsDLC;
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
                                if (exp.ClassName != "TextureCube")
                                {
                                    pformat = "TextureMovie";
                                    if (exp.ClassName != "TextureMovie")
                                    {
                                        var formp = exp.GetProperty<EnumProperty>("Format");
                                        pformat = formp != null ? formp.Value.Name.ToString() : "n/a";
                                        if (ScanCRC)
                                        {
                                            cRC = Texture2D.GetTextureCRC(exp).ToString("X8");
                                        }
                                    }
                                    var propX = exp.GetProperty<IntProperty>("SizeX");
                                    psizeX = propX != null ? propX.Value : 0;
                                    var propY = exp.GetProperty<IntProperty>("SizeY");
                                    psizeY = propY != null ? propY.Value : 0;
                                }
                                var NewTex = new TextureRecord(pExp, parent, IsDLC, pformat, psizeX, psizeY, cRC, new ObservableCollectionExtended<Tuple<int, int, bool>>() { new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC) });
                                dbScanner.GeneratedText.TryAdd(pExp, NewTex);
                            }
                        }
                    }
                    else
                    {
                        pClass = exp.ObjectName;
                        pSuperClass = exp.SuperClassName;
                        pDefUID = exp.UIndex;
                        var NewUsageRecord = new ClassUsage(FileKey, pExportUID, pIsdefault);
                        var NewPropertyRecord = new PropertyRecord("None", "NoneProperty");
                        var NewClassRecord = new ClassRecord(pClass, ShortFileName, pDefUID, pSuperClass, new ObservableCollectionExtended<PropertyRecord>() { NewPropertyRecord }, new ObservableCollectionExtended<ClassUsage>() { NewUsageRecord });
                        if (!dbScanner.GeneratedClasses.TryAdd(pClass, NewClassRecord))
                        {
                            dbScanner._dbqueue.Add(NewClassRecord);
                        }
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Exception Bug detected in single file: {exp.FileRef.FilePath} Export:{exp.UIndex}");
                }
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
