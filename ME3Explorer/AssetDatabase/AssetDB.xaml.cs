using ME3Explorer.Dialogue_Editor;
using ME3Explorer.Packages;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ME3Explorer.AssetDatabase
{


    /// <summary>
    /// Interaction logic for AssetDB
    /// </summary>
    public partial class AssetDB : WPFBase
    {
        #region Declarations
        public static string dbCurrentBuild { get; set; } = "1.0"; //If changes are made that invalidate old databases edit this.
        private int _currentView;
        public int currentView { get => _currentView; set => SetProperty(ref _currentView, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        private string _busyText;
        public string BusyText { get => _busyText; set => SetProperty(ref _busyText, value); }
        private string _busyHeader;
        public string BusyHeader { get => _busyHeader; set => SetProperty(ref _busyHeader, value); }
        private bool _BusyUnk;
        public bool BusyUnk { get => _BusyUnk; set => SetProperty(ref _BusyUnk, value); }
        public MEGame currentGame { get; set; }
        private string CurrentDBPath { get; set; }
        public PropsDataBase CurrentDataBase { get; } = new PropsDataBase(MEGame.Unknown, null, new ObservableCollectionExtended<ClassRecord>(), new ObservableCollectionExtended<Material>(), new ObservableCollectionExtended<Animation>(), new ObservableCollectionExtended<MeshRecord>());

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
        private ConcurrentQueue<PropsDataBase> deserializingQueue = new ConcurrentQueue<PropsDataBase>();
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
        public ICollectionView filesFiltered { get; set; }
        private IMEPackage meshPcc;
        private IMEPackage textPcc;
        public ICommand GenerateDBCommand { get; set; }
        public ICommand SaveDBCommand { get; set; }
        public ICommand SwitchMECommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }
        public ICommand OpenSourcePkgCommand { get; set; }
        public ICommand GoToSuperclassCommand { get; set; }
        public ICommand OpenUsagePkgCommand { get; set; }
        public ICommand FilterSeqClassCommand { get; set; }
        public ICommand FilterMatCommand { get; set; }
        public ICommand FilterMeshCommand { get; set; }

        private bool CanCancelDump(object obj)
        {
            return ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation && !DumpCanceled;
        }
        private bool IsClassSelected(object obj)
        {
            return lstbx_Classes.SelectedIndex >= 0 && currentView == 0;
        }
        private bool IsUsageSelected(object obj)
        {
            return (lstbx_Usages.SelectedIndex >= 0 && currentView == 0) || (lstbx_MatUsages.SelectedIndex >= 0 && currentView == 1) || (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 2) 
                || (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3) || (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 4) || (lstbx_TextureUsages.SelectedIndex >= 0 && currentView == 5) || currentView == 6;
        }
        private bool IsViewingClass(object obj)
        {
            return currentView == 0;
        }
        private bool IsViewingMaterials(object obj)
        {
            return currentView == 1;
        }
        private bool IsViewingMeshes(object obj)
        {
            return currentView == 3;
        }
        private bool IsViewingTextures(object obj)
        {
            return currentView == 5;
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
            LoadCommands();

            //Get default db / gane
            CurrentDBPath = Properties.Settings.Default.AssetDBPath;
            Enum.TryParse<MEGame>(Properties.Settings.Default.AssetDBGame, out MEGame game);

            InitializeComponent();

            if (CurrentDBPath != null && CurrentDBPath.EndsWith("zip") && File.Exists(CurrentDBPath) && game != MEGame.Unknown)
            {
                SwitchGame(game.ToString());
            }
            else
            {
                CurrentDBPath = null;
                SwitchGame(MEGame.ME3);

            }

            Activate();
        }


        private async void Delay(int time = 1000)
        {
            await Task.Delay(time);
        } 

        private void LoadCommands()
        {
            GenerateDBCommand = new GenericCommand(GenerateDatabase);
            SaveDBCommand = new GenericCommand(SaveDatabase);
            FilterSeqClassCommand = new RelayCommand(SetFilters, IsViewingClass);
            FilterMatCommand = new RelayCommand(SetFilters, IsViewingMaterials);
            FilterMeshCommand = new RelayCommand(SetFilters, IsViewingMeshes);
            SwitchMECommand = new RelayCommand(SwitchGame);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
            OpenSourcePkgCommand = new RelayCommand(OpenSourcePkg, IsClassSelected);
            GoToSuperclassCommand = new RelayCommand(GoToSuperClass, IsClassSelected);
            OpenUsagePkgCommand = new RelayCommand(OpenUsagePkg, IsUsageSelected);
        }
               
        private void AssetDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;

            Properties.Settings.Default.AssetDBPath = CurrentDBPath;
            Properties.Settings.Default.AssetDBGame = currentGame.ToString();
            EmbeddedTextureViewerTab_EmbededTextureViewer.UnloadExport();
            MeshRendererTab_MeshRenderer.UnloadExport();
            meshPcc?.Dispose();
            textPcc?.Dispose();
        }

        #endregion

        #region UserCommands
        public void GenerateDatabase()
        {
            ScanGame();
        }

        public async void LoadDatabase()
        {
            if (CurrentDBPath == null)
            {
                return;
            }

            if (File.Exists(CurrentDBPath))
            {
                CurrentOverallOperationText = "Loading database";
                BusyHeader = "Loading database";
                BusyText = "Please wait...";
                BusyUnk = true;
                IsBusy = true;
                ClearDataBase();
                var start = DateTime.UtcNow;
                
                //Sync load
                //using (ZipArchive archive = ZipFile.OpenRead(CurrentDBPath))
                //using (var jsonstream = archive.GetEntry($"AssetDB{currentGame}.json").Open())
                //using (StreamReader sr = new StreamReader(jsonstream))
                //using (JsonReader reader = new JsonTextReader(sr))
                //{
                //    readData = serializer.Deserialize<PropsDataBase>(reader);
                //}

                ////Async load
                await Task.Factory.StartNew(() => ParseDBAsync());
                while (deserializingQueue.Count > 0)
                {
                    deserializingQueue.TryDequeue(out PropsDataBase pdb);
                    switch (pdb.DataBaseversion)
                    {
                        case "Class":
                            CurrentDataBase.ClassRecords.AddRange(pdb.ClassRecords);
                            break;
                        case "Materials":
                            CurrentDataBase.Materials.AddRange(pdb.Materials);
                            break;
                        case "Anims":
                            CurrentDataBase.Animations.AddRange(pdb.Animations);
                            break;
                        case "Meshes":
                            CurrentDataBase.Meshes.AddRange(pdb.Meshes);
                            break;
                        case "Particles":
                            CurrentDataBase.Particles.AddRange(pdb.Particles);
                            break;
                        case "Textures":
                            CurrentDataBase.Textures.AddRange(pdb.Textures);
                            break;
                        default:
                            CurrentDataBase.meGame = pdb.meGame;
                            CurrentDataBase.GenerationDate = pdb.GenerationDate;
                            CurrentDataBase.DataBaseversion = pdb.DataBaseversion;
                            CurrentDataBase.FileList.AddRange(pdb.FileList);
                            //Single thread async load only
                            CurrentDataBase.ClassRecords.AddRange(pdb.ClassRecords);
                            CurrentDataBase.Materials.AddRange(pdb.Materials);
                            CurrentDataBase.Animations.AddRange(pdb.Animations);
                            CurrentDataBase.Meshes.AddRange(pdb.Meshes);
                            CurrentDataBase.Particles.AddRange(pdb.Particles);
                            CurrentDataBase.Textures.AddRange(pdb.Textures);
                            break;
                    }
                }

                if (CurrentDataBase.DataBaseversion == null || CurrentDataBase.DataBaseversion != dbCurrentBuild)
                {
                    var warn = MessageBox.Show("This database is out of date. A new version is required. Do you wish to rebuild?", "Warning", MessageBoxButton.OKCancel);
                    if (warn != MessageBoxResult.Cancel)
                        GenerateDatabase();
                    ClearDataBase();
                    IsBusy = false;
                    return;
                }

                IsBusy = false;

                CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count} Meshes: {CurrentDataBase.Meshes.Count} Particles: { CurrentDataBase.Particles.Count} Textures: { CurrentDataBase.Textures.Count}";
#if DEBUG
                var end = DateTime.UtcNow;
                double length = (end - start).TotalMilliseconds;
                CurrentOverallOperationText = $"{CurrentOverallOperationText} LoadTime: {length}ms";
#endif
            }
            else
            {
                CurrentOverallOperationText = "No database found.";
            }
        }

        private void ParseDBSnippets(JToken jt, string type)
        {
            var pdb = new PropsDataBase();
            pdb.DataBaseversion = type;
            switch(type)
            {

                case "Class":
                    pdb.ClassRecords.AddRange(JsonConvert.DeserializeObject<ObservableCollectionExtended<ClassRecord>>(jt.First.ToString()));
                    break;
                case "Materials":
                    pdb.Materials.AddRange(JsonConvert.DeserializeObject<ObservableCollectionExtended<Material>>(jt.First.ToString()));
                    break;
                case "Anims":
                    pdb.Animations.AddRange(JsonConvert.DeserializeObject<ObservableCollectionExtended<Animation>>(jt.First.ToString()));
                    break;
                case "Meshes":
                    pdb.Meshes.AddRange(JsonConvert.DeserializeObject<ObservableCollectionExtended<MeshRecord>>(jt.First.ToString()));
                    break;
                case "Particles":
                    pdb.Particles.AddRange(JsonConvert.DeserializeObject<ObservableCollectionExtended<ParticleSys>>(jt.First.ToString()));
                    break;
                case "Textures":
                    pdb.Textures.AddRange(JsonConvert.DeserializeObject<ObservableCollectionExtended<TextureRecord>>(jt.First.ToString()));
                    break;
            }

            deserializingQueue.Enqueue(pdb);
        }
        private async void ParseDBAsync()
        {
            PropsDataBase readData = new PropsDataBase();
            //Async load
            try
            {
                using (ZipArchive archive = new ZipArchive((Stream)new FileStream(CurrentDBPath, FileMode.Open)))
                {
                    var jsonstream = archive.GetEntry($"AssetDB{currentGame}.json").Open();
                    JToken jfiles = null;
                    using (StreamReader sr = new StreamReader(jsonstream))
                    {
                        using (JsonReader reader = new JsonTextReader(sr))
                        {
                            var Serializer = new JsonSerializer();
                            readData = Serializer.Deserialize<PropsDataBase>(reader);
                            //while (reader.Depth == 0) { reader.Read(); } //Get past root nodes
                            //var builders = new List<JToken>();
                            //int n = 0;
                            //while (reader.Depth >= 1)
                            //{
                            //    JToken jt = null;
                            //    try
                            //    {
                            //        jt = JToken.Load(reader);
                            //    }
                            //    catch
                            //    {
                            //        continue;
                            //    }
                            //    builders.Add(jt);
                            //    n++;
                            //    switch (n)
                            //    {
                            //        case 1: //"meGame"
                            //            MEGame meG = MEGame.Unknown;
                            //            Enum.TryParse(jt.First.ToString(), out meG);
                            //            readData.meGame = meG;
                            //            break;
                            //        case 2: //"GenerationDate"
                            //            readData.GenerationDate = jt.First.ToString();
                            //            break;
                            //        case 3: //"DataBaseversion"
                            //            readData.DataBaseversion = jt.First.ToString();
                            //            break;
                            //        case 4: //"FileList"
                            //            jfiles = jt;
                            //            break;
                            //        case 5: //"Classes"
                            //            var tskC = new Task(() => ParseDBSnippets(jt, "Class"));
                            //            tskC.Start();
                            //            break;
                            //        case 6: //"Materials"
                            //            var tskM = new Task(() => ParseDBSnippets(jt, "Materials"));
                            //            tskM.Start();
                            //            break;
                            //        case 7: //"Anims"
                            //            var tskA = new Task(() => ParseDBSnippets(jt, "Anims"));
                            //            tskA.Start();
                            //            break;
                            //        case 8: //"Meshes"
                            //            var tskMR = new Task(() => ParseDBSnippets(jt, "Meshes"));
                            //            tskMR.Start();
                            //            break;
                            //        case 9: //"PS"
                            //            var tskP = new Task(() => ParseDBSnippets(jt, "Particles"));
                            //            tskP.Start();
                            //            break;
                            //        case 10: //"Textures"
                            //            var tskT = new Task(() => ParseDBSnippets(jt, "Textures"));
                            //            tskT.Start();
                            //            break;
                            //        default:
                            //            break;
                            //    }
                            //}
                        }
                    }


                    //Do filelist
                    //if (jfiles != null)
                    //{
                    //    var fileLXZ = jfiles.First.ToObject<List<string>>();
                    //    readData.FileList.AddRange(fileLXZ);
                    //}
                    deserializingQueue.Enqueue(readData);
                }
            }
            catch
            {
                MessageBox.Show("Compressed archive: " + currentGame + " is corrupted.");
            }
            await Task.Delay(1);
        }
        public async void SaveDatabase()
        {
            BusyHeader = "Saving database";
            BusyText = "Please wait...";
            OverallProgressMaximum = 100;
            OverallProgressValue = 50;
            IsBusy = true;
            CurrentOverallOperationText = $"Database saving...";

            using (var fileStream = new FileStream(CurrentDBPath, FileMode.Create))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
                {
                    var zipFile = archive.CreateEntry($"AssetDB{currentGame}.json");

                    using (var entryStream = zipFile.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        var jsondb = await Task.Run(() => JsonConvert.SerializeObject(CurrentDataBase));
                        IsBusy = false;
                        await Task.Run(() => streamWriter.Write(jsondb));
                    }
                }
            }
            CurrentOverallOperationText = $"Database saved.";
            OverallProgressValue = 100;

            await Task.Delay(5000);
            CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count} Meshes: {CurrentDataBase.Meshes.Count} Particles: { CurrentDataBase.Particles.Count} Textures: { CurrentDataBase.Textures.Count}";
        }

        public void ClearDataBase()
        {
            CurrentDataBase.meGame = currentGame;
            CurrentDataBase.GenerationDate = null;
            CurrentDataBase.FileList.Clear();
            CurrentDataBase.ClassRecords.ClearEx();
            CurrentDataBase.Animations.ClearEx();
            CurrentDataBase.Materials.ClearEx();
            CurrentDataBase.Meshes.ClearEx();
            CurrentDataBase.Particles.ClearEx();
            CurrentDataBase.Textures.ClearEx();
        }

        public void SwitchGame(object param)
        {
            var p = param as string;
            switchME1_menu.IsChecked = false;
            switchME2_menu.IsChecked = false;
            switchME3_menu.IsChecked = false;
            FilterBox.Clear();
            ClearDataBase();

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

            LoadDatabase();
        }
        private void GoToSuperClass(object obj)
        {
            var sClass = CurrentDataBase.ClassRecords[lstbx_Classes.SelectedIndex].SuperClass;
            if (sClass == null)
            {
                MessageBox.Show("SuperClass unknown.");
                return;
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
            var usageexp = 0;

            if (lstbx_Usages.SelectedIndex >= 0 && currentView == 0)
            {
                var c = lstbx_Usages.SelectedItem as ClassUsage;
                usagepkg = CurrentDataBase.FileList[c.FileKey];
                usageexp = c.ExportUID;
            }
            else if (lstbx_MatUsages.SelectedIndex >= 0 && currentView == 1)
            {
                var m = lstbx_MatUsages.SelectedItem as Tuple<int, int, bool>;
                usagepkg = CurrentDataBase.FileList[m.Item1];
                usageexp = m.Item2;
            }
            else if (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 2)
            {
                var a = lstbx_AnimUsages.SelectedItem as Tuple<int, int>;
                usagepkg = CurrentDataBase.FileList[a.Item1];
                usageexp = a.Item2;
            }
            else if (lstbx_MeshUsages.SelectedIndex >= 0 && currentView == 3)
            {
                var s = lstbx_MeshUsages.SelectedItem as Tuple<int, int>;
                usagepkg = CurrentDataBase.FileList[s.Item1];
                usageexp = s.Item2;
            }
            else if (lstbx_PSUsages.SelectedIndex >= 0 && currentView == 4)
            {
                var s = lstbx_PSUsages.SelectedItem as Tuple<int, int, bool>;
                usagepkg = CurrentDataBase.FileList[s.Item1];
                usageexp = s.Item2;
            }
            else if (lstbx_Textures.SelectedIndex >= 0 && currentView == 5)
            {
                usagepkg = lstbx_Files.SelectedItem.ToString();
            }
            else if (lstbx_Files.SelectedIndex >= 0 && currentView == 6)
            {
                usagepkg = lstbx_Files.SelectedItem.ToString();
            }

            if (usagepkg == null)
            {
                MessageBox.Show("File not found.");
                return;
            }

            OpenInToolkit(tool, usagepkg, usageexp);
        }
        private void OpenSourcePkg(object obj)
        {
            var sourcepkg = CurrentDataBase.ClassRecords[lstbx_Classes.SelectedIndex].Definition_package;
            var sourceexp = CurrentDataBase.ClassRecords[lstbx_Classes.SelectedIndex].Definition_UID;
            if (sourcepkg == null)
            {
                MessageBox.Show("Definition file unknown.");
                return;
            }
            OpenInToolkit("PackageEditor", sourcepkg, sourceexp);
        }

        private void OpenInToolkit(string tool, string filename, int export = 0, string param = null)
        {
            string filePath = null;
            string rootPath = MEDirectories.GamePath(currentGame);

            if (rootPath == null || !Directory.Exists(rootPath))
            {
                MessageBox.Show($"{currentGame} has not been found. Please check your ME3Explorer settings");
                return;
            }

            filename = $"{filename}.*";
            filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
            if (filePath == null)
            {
                MessageBox.Show($"File {filename} not found.");
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

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) //Fires if Tab moves away
        {
            if (e.AddedItems == e.RemovedItems || e.RemovedItems.Count == 0)
            {
                return;
            }
            var item = sender as TabControl;
            var selected = item.SelectedItem as TabItem;
            var unselected = e.RemovedItems[0] as TabItem;

            if (unselected != null && selected.TabIndex != unselected.TabIndex)
            {
                FilterBox.Clear();
                Filter();

                if (unselected.TabIndex == 3)
                {
                    ToggleRenderMesh();
                    btn_MeshRenderToggle.IsChecked = false;
                    btn_MeshRenderToggle.Content = "Toggle Mesh Rendering";
                }

                if (unselected.TabIndex == 5)
                {
                    ToggleRenderTexture();
                    btn_TextRenderToggle.IsChecked = false;
                    btn_TextRenderToggle.Content = "Toggle Texture Rendering";
                }

                if(selected.TabIndex == 6)
                {
                    menu_OpenUsage.Header = "Open File";
                }

                if (unselected.TabIndex == 6)
                {
                    menu_OpenUsage.Header = "Open Usage";
                }
            }

        }

        private void lstbx_Meshes_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (currentView == 3 && lstbx_Meshes.SelectedIndex >= 0)
            {
                ToggleRenderMesh();
            }
        }
        private void lstbx_Textures_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (currentView == 5 && lstbx_Textures.SelectedIndex >= 0)
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
            var selecteditem = lstbx_Meshes.SelectedItem as MeshRecord;

            var filekey = selecteditem.MeshUsages[0].Item1;
            var filename = CurrentDataBase.FileList[filekey];
            string rootPath = MEDirectories.GamePath(currentGame);

            if (rootPath == null)
            {
                MessageBox.Show($"{currentGame} has not been found. Please check your ME3Explorer settings");
                return;
            }

            filename = $"{filename}.*";
            var filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
            if (filePath == null)
            {
                MessageBox.Show($"File {filename} not found.");
                return;
            }

            if (meshPcc != null)
            {
                MeshRendererTab_MeshRenderer.UnloadExport();
                meshPcc.Dispose();
            }
            meshPcc = MEPackageHandler.OpenMEPackage(filePath);
            var meshExp = meshPcc.GetUExport(selecteditem.MeshUsages[0].Item2);
            MeshRendererTab_MeshRenderer.LoadExport(meshExp);

        }
        private void ToggleRenderTexture()
        {
            bool showText = false;
            if (btn_TextRenderToggle.IsChecked == true && (lstbx_Textures.SelectedIndex >= 0) && CurrentDataBase.Textures[lstbx_Textures.SelectedIndex].TextureUsages.Count > 0 && currentView == 5)
            {
                showText = true;
            }

            if (!showText)
            {
                EmbeddedTextureViewerTab_EmbededTextureViewer.UnloadExport();
                textPcc?.Dispose();
                return;
            }
            var selecteditem = lstbx_Textures.SelectedItem as TextureRecord;

            var filekey = selecteditem.TextureUsages[0].Item1;
            var filename = CurrentDataBase.FileList[filekey];
            string rootPath = MEDirectories.GamePath(currentGame);

            if (rootPath == null)
            {
                MessageBox.Show($"{currentGame} has not been found. Please check your ME3Explorer settings");
                return;
            }

            filename = $"{filename}.*";
            var filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
            if (filePath == null)
            {
                MessageBox.Show($"File {filename} not found.");
                return;
            }

            if (textPcc != null)
            {
                EmbeddedTextureViewerTab_EmbededTextureViewer.UnloadExport();
                textPcc.Dispose();
            }
            textPcc = MEPackageHandler.OpenMEPackage(filePath);
            var TextExp = textPcc.GetUExport(selecteditem.TextureUsages[0].Item2);
            EmbeddedTextureViewerTab_EmbededTextureViewer.LoadExport(TextExp);

        }
        #endregion

        #region Filters
        bool ClassFilter(object d)
        {
            var cr = d as ClassRecord;
            bool showthis = true;
            if(FilterBox.Text != null)
            {
                showthis = cr.Class.ToLower().Contains(FilterBox.Text.ToLower());
            }
            if(showthis && menu_fltrSeq.IsChecked && (!cr.Class.ToLower().StartsWith("seq") && !cr.Class.ToLower().StartsWith("bioseq") && !cr.Class.ToLower().StartsWith("sfxseq") && !cr.Class.ToLower().StartsWith("rvrseq")))
            {
                showthis = false;
            }
            return showthis;
        }
        bool MaterialFilter(object d)
        {
            var mr = d as Material;
            bool showthis = true;
            if (FilterBox.Text != null)
            {
                showthis = mr.MaterialName.ToLower().Contains(FilterBox.Text.ToLower());
            }
            if (showthis && menu_fltrMatUnlit.IsChecked && !mr.MatSettings.Any(x => x.Item1 == "LightingModel" && x.Item3 == "MLM_Unlit"))
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
            return showthis;
        }
        bool MeshFilter(object d)
        {
            var mr = d as MeshRecord;
            bool showthis = true;
            if (FilterBox.Text != null)
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
            if (FilterBox.Text != null)
            {
                showthis = ar.AnimSequence.ToLower().Contains(FilterBox.Text.ToLower());
            }

            return showthis;
        }
        bool PSFilter(object d)
        {
            var ps = d as ParticleSys;
            bool showthis = true;
            if (FilterBox.Text != null)
            {
                showthis = ps.PSName.ToLower().Contains(FilterBox.Text.ToLower());
            }

            return showthis;
        }
        bool TexFilter(object d)
        {
            var tr = d as TextureRecord;
            bool showthis = true;
            if (FilterBox.Text != null)
            {
                showthis = tr.TextureName.ToLower().Contains(FilterBox.Text.ToLower());
            }

            return showthis;
        }
        private bool FileFilter(object d)
        {
            bool showthis = true;
            if (FilterBox.Text != null)
            {
                showthis = (d as string).ToLower().Contains(FilterBox.Text.ToLower());
            }
            return showthis;
        }
        private void Filter()
        {
            ICollectionView viewM = CollectionViewSource.GetDefaultView(CurrentDataBase.Materials);
            ICollectionView viewA = CollectionViewSource.GetDefaultView(CurrentDataBase.Animations);
            ICollectionView viewS = CollectionViewSource.GetDefaultView(CurrentDataBase.Meshes);
            ICollectionView viewC = CollectionViewSource.GetDefaultView(CurrentDataBase.ClassRecords);
            ICollectionView viewP = CollectionViewSource.GetDefaultView(CurrentDataBase.Particles);
            ICollectionView viewT = CollectionViewSource.GetDefaultView(CurrentDataBase.Textures);
            ICollectionView filesFiltered = CollectionViewSource.GetDefaultView(CurrentDataBase.FileList);
            
            filesFiltered.SortDescriptions.Add(new SortDescription("", ListSortDirection.Ascending));
            filesFiltered.Filter = FileFilter;
            viewC.Filter = ClassFilter;
            viewM.Filter = MaterialFilter;
            viewS.Filter = MeshFilter;
            viewA.Filter = AnimFilter;
            viewP.Filter = PSFilter;
            viewT.Filter = TexFilter;

            lstbx_Anims.ItemsSource = viewP;
            lstbx_Materials.ItemsSource = viewM;
            lstbx_Anims.ItemsSource = viewA;
            lstbx_Meshes.ItemsSource = viewS;
            lstbx_Classes.ItemsSource = viewC;
            lstbx_Textures.ItemsSource = viewT;
            lstbx_Files.ItemsSource = filesFiltered;
        }
        private void SetFilters(object obj)
        {
            var param = obj as string;
            switch(param)
            {
                case "Seq":
                    menu_fltrSeq.IsChecked = !menu_fltrSeq.IsChecked;
                    break;
                case "Unlit":
                    menu_fltrMatUnlit.IsChecked = !menu_fltrMatUnlit.IsChecked;
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
                    if(!menu_fltrMat1side.IsChecked)
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
                default:
                    break;
            }
            Filter();
        }
        private void FilterBox_KeyUp(object sender, KeyEventArgs e)
        {
           
           Filter();
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

            //Shuffle randomly to avoid localizations concurrently accessing
            int n = files.Count;
            var rng = new Random();
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = files[k];
                files[k] = files[n];
                files[n] = value;
            }

            await dumpPackages(files, currentGame);
        }
        private async Task dumpPackages(List<string> files, MEGame game)
        {
            TopDock.IsEnabled = false;
            MidDock.IsEnabled = false;
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;
            BusyUnk = false;
            CurrentOverallOperationText = $"Generating Database...";

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


            IsBusy = true;
            BusyHeader = $"Generating database for {currentGame}";
            ProcessingQueue = new ActionBlock<ClassScanSingleFileTask>(x =>
            {
                if (x.DumpCanceled)
                {
                    OverallProgressValue++;
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
            foreach (var item in files)
            {
                var filekey = CurrentDataBase.FileList.Count;
                CurrentDataBase.FileList.Add(Path.GetFileNameWithoutExtension(item));
                var threadtask = new ClassScanSingleFileTask(item, filekey);
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
        public List<string> FileList { get; } = new List<string>();
        public ObservableCollectionExtended<ClassRecord> ClassRecords { get; } = new ObservableCollectionExtended<ClassRecord>();
        public ObservableCollectionExtended<Material> Materials { get; } = new ObservableCollectionExtended<Material>();
        public ObservableCollectionExtended<Animation> Animations { get; } = new ObservableCollectionExtended<Animation>();
        public ObservableCollectionExtended<MeshRecord> Meshes { get; } = new ObservableCollectionExtended<MeshRecord>();
        public ObservableCollectionExtended<ParticleSys> Particles { get; } = new ObservableCollectionExtended<ParticleSys>();
        public ObservableCollectionExtended<TextureRecord> Textures { get; } = new ObservableCollectionExtended<TextureRecord>();
        public PropsDataBase(MEGame meGame, string GenerationDate, ObservableCollectionExtended<ClassRecord> ClassRecords, ObservableCollectionExtended<Material> Materials, ObservableCollectionExtended<Animation> Animations, ObservableCollectionExtended<MeshRecord> Meshes)
        {
            this.meGame = meGame;
            this.GenerationDate = GenerationDate;
            this.ClassRecords.AddRange(ClassRecords);
            this.Materials.AddRange(Materials);
            this.Animations.AddRange(Animations);
            this.Meshes.AddRange(Meshes);
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
        public ObservableCollectionExtended<Tuple<int, int, bool>> TextureUsages { get; } = new ObservableCollectionExtended<Tuple<int, int, bool>>(); //File reference then export, isDLC file

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
            var listofFiles = values[1] as List<string>;
            if (fileindex == 0 || listofFiles.Count == 0)
                return "Error file not found";
            return listofFiles[fileindex];
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

        public ClassScanSingleFileTask(string file, int filekey)
        {
            File = file;
            ShortFileName = Path.GetFileNameWithoutExtension(file);
            FileKey = filekey;
        }

        public bool DumpCanceled;
        private readonly int FileKey;
        private readonly string File;

        /// <summary>
        /// Dumps Property data to concurrent dictionary
        /// </summary>
        public void dumpPackageFile(MEGame GameBeingDumped, AssetDB dbScanner)
        {
            using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(File))
            {
                foreach (ExportEntry exp in pcc.Exports)
                {
                    try
                    {
                        string pClass = exp.ClassName;  //Handle basic class record
                        string pExp = exp.ObjectName;
                        if (exp.indexValue > 0)
                        {
                            pExp = $"{pExp}_{exp.indexValue}";
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

                                if (exp.ClassName == "Material" && !dbScanner.GeneratedMats.ContainsKey(pExp)) //Run material settings
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
                                    lock(eMat)
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
                                    aGrp = pExp.Replace(aSeq, null);
                                }
                                float aLength = 0;
                                var pLength = exp.GetProperty<FloatProperty>("SequenceLength");
                                if (pLength != null)
                                {
                                    aLength = pLength.Value;
                                }
                                int aFrames = 0;
                                var pFrames = exp.GetProperty<IntProperty>("NumFrames");
                                if (pFrames != null)
                                {
                                    aFrames = pFrames.Value;
                                }
                                string aComp = "None";
                                var pComp = exp.GetProperty<EnumProperty>("RotationCompressionFormat");
                                if (pComp != null)
                                {
                                    aComp = pComp.Value.ToString();
                                }
                                string aKeyF = "None";
                                var pKeyF = exp.GetProperty<EnumProperty>("KeyEncodingFormat");
                                if (pKeyF != null)
                                {
                                    aKeyF = pKeyF.Value.ToString();
                                }
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
                                    if(bin != null)
                                    {
                                        bones = bin.RefSkeleton.Length;
                                    }
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
                                int EmCnt = 0;
                                var EmtProp = exp.GetProperty<ArrayProperty<ObjectProperty>>("Emitters");
                                if(EmtProp != null)
                                {
                                    EmCnt = EmtProp.Count;
                                }

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

                            if (exp.ClassName == "Texture2D" && !pIsdefault)
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
                                string pformat = "n/a";
                                var formp = exp.GetProperty<EnumProperty>("Format");
                                if (formp != null)
                                {
                                    pformat = formp.Value.Name.ToString() ;
                                }
                                int psizeX = 0;
                                var propX = exp.GetProperty<IntProperty>("SizeX");
                                if (propX != null)
                                {
                                    psizeX = propX;
                                }
                                int psizeY = 0;
                                var propY = exp.GetProperty<IntProperty>("SizeY");
                                if (propY != null)
                                {
                                    psizeY = propY;
                                }

                                string cRC = "n/a"; //TO DO ADD MAGIC

                                var NewTex = new TextureRecord(pExp, parent, IsDLC, pformat, psizeX, psizeY, cRC, new ObservableCollectionExtended<Tuple<int, int, bool>>() { new Tuple<int, int, bool>(FileKey, pExportUID, IsDLC) });
                                if (!dbScanner.GeneratedText.TryAdd(pExp, NewTex))
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
