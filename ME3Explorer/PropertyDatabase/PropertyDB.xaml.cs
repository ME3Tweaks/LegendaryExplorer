using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ME3Explorer.PropertyDatabase
{


    /// <summary>
    /// Interaction logic for PropertyDB
    /// </summary>
    public partial class PropertyDB : WPFBase
    {
        #region Declarations
        private int _currentView;
        public int currentView { get => _currentView; set { SetProperty(ref _currentView, value); Console.WriteLine($"Tab index{_currentView}"); } }

        public enum DBView
        {
            Class = 0,
            Animations,
            Materials,
        }
        public MEGame currentGame { get; set; }
        private string CurrentDBPath { get; set; }
        public PropsDataBase CurrentDataBase { get; } = new PropsDataBase(MEGame.Unknown, null, new ObservableCollectionExtended<ClassRecord>(), new ObservableCollectionExtended<Material>(), new ObservableCollectionExtended<Animation>());

        /// <summary>
        /// Dictionary that stores generated classes
        /// </summary>
        public ConcurrentDictionary<String, ClassRecord> GeneratedClasses = new ConcurrentDictionary<String, ClassRecord>();
        /// <summary>
        /// Dictionary that stores generated Animations
        /// </summary>
        public ConcurrentDictionary<String, Animation> GeneratedAnims = new ConcurrentDictionary<String, Animation>();
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

        public ICommand GenerateDBCommand { get; set; }
        public ICommand SwitchViewCommand { get; set; }
        public ICommand SwitchMECommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }
        public ICommand OpenSourcePkgCommand { get; set; }
        public ICommand GoToSuperclassCommand { get; set; }
        public ICommand OpenUsagePkgCommand { get; set; }
        public ICommand FilterSeqClassCommand { get; set; }

        private bool CanCancelDump(object obj)
        {
            return ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation && !DumpCanceled;
        }
        private bool IsClassSelected(object obj)
        {
            return lstbx_Classes.SelectedIndex >= 0;
        }
        private bool IsUsageSelected(object obj)
        {
            return (lstbx_Usages.SelectedIndex >= 0 && currentView == 0) || (lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 1);
        }
        public override void handleUpdate(List<PackageUpdate> updates)
        {
            throw new NotImplementedException();
        }



        #endregion

        #region Startup/Exit

        public PropertyDB()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Property and Asset Database", new WeakReference(this));
            LoadCommands();

            //Get default db / gane
            CurrentDBPath = Properties.Settings.Default.PropertyDBPath;
            Enum.TryParse<MEGame>(Properties.Settings.Default.PropertyDBGame, out MEGame game);

            InitializeComponent();

            if (CurrentDBPath != null && CurrentDBPath.EndsWith("JSON") && File.Exists(CurrentDBPath) && game != MEGame.Unknown)
            {
                SwitchGame(game.ToString());
            }
            else
            {
                CurrentDBPath = null;
                SwitchGame("ME3");
                CurrentOverallOperationText = "No database found.";
            }
        }

        private void LoadCommands()
        {
            GenerateDBCommand = new GenericCommand(GenerateDatabase);
            FilterSeqClassCommand = new GenericCommand(FilterSeqClasses);
            SwitchMECommand = new RelayCommand(SwitchGame);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
            OpenSourcePkgCommand = new RelayCommand(OpenSourcePkg, IsClassSelected);
            GoToSuperclassCommand = new RelayCommand(GoToSuperClass, IsClassSelected);
            OpenUsagePkgCommand = new RelayCommand(OpenUsagePkg, IsUsageSelected);
        }



        private void PropertyDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;
           
            Properties.Settings.Default.PropertyDBPath = CurrentDBPath;
            Properties.Settings.Default.PropertyDBGame = currentGame.ToString();
        }

        #endregion

        #region UserCommands
        public void GenerateDatabase()
        {
            ScanGame();
        }

        public void LoadDatabase()
        {
            if(CurrentDBPath == null)
            {
                return;
            }

            if(File.Exists(CurrentDBPath))
            {
                var readData = JsonConvert.DeserializeObject<PropsDataBase>(File.ReadAllText(CurrentDBPath));

                CurrentDataBase.meGame = readData.meGame;
                CurrentDataBase.GenerationDate = readData.GenerationDate;
                CurrentDataBase.FileList.Clear();
                CurrentDataBase.FileList.AddRange(readData.FileList);
                CurrentDataBase.ClassRecords.Clear();
                CurrentDataBase.ClassRecords.AddRange(readData.ClassRecords);
                CurrentDataBase.Animations.Clear();
                CurrentDataBase.Animations.AddRange(readData.Animations);
                CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count}";
            }
            else
            {
                CurrentOverallOperationText = "No database found.";
            }

        }

        public async void SaveDatabase()
        {
            CurrentOverallOperationText = $"Database saving...";

            ////Save database to JSON directly to file
            using (StreamWriter file = File.CreateText(CurrentDBPath))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, CurrentDataBase);
            }
            CurrentOverallOperationText = $"Database saved.";
            await Task.Delay(5000);
            CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count} Animations: {CurrentDataBase.Animations.Count} Materials: {CurrentDataBase.Materials.Count}";
        }

        public void ClearDataBase()
        {
            CurrentDataBase.meGame = currentGame;
            CurrentDataBase.GenerationDate = null;
            CurrentDataBase.FileList.Clear();
            CurrentDataBase.ClassRecords.ClearEx();
            CurrentDataBase.Animations.ClearEx();
            CurrentDataBase.Materials.ClearEx();
        }

        public void SwitchGame(object param)
        {
            var p = param as string;
            switchME1_menu.IsChecked = false;
            switchME2_menu.IsChecked = false;
            switchME3_menu.IsChecked = false;
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
            CurrentDBPath = Path.Combine(App.AppDataFolder, $"PropertyDB{currentGame}.JSON");

            LoadDatabase();
        }

        private void FilterSeqClasses()
        {
            if(!menu_fltrSeq.IsChecked)
            {
                menu_fltrSeq.IsChecked = true;
                ICollectionView view = CollectionViewSource.GetDefaultView(CurrentDataBase.ClassRecords);
                view.Filter = delegate (object item) {
                    var classes = item as ClassRecord;
                    if (classes != null && (
                    classes.Class.ToLower().StartsWith("seq") ||
                    classes.Class.ToLower().StartsWith("bioseq") ||
                    classes.Class.ToLower().StartsWith("sfxseq") ||
                    classes.Class.ToLower().StartsWith("rvrseq"))) return true;
                    return false;
                }; ;
                lstbx_Classes.ItemsSource = view;
            }
            else
            {
                menu_fltrSeq.IsChecked = false;
                ICollectionView view = CollectionViewSource.GetDefaultView(CurrentDataBase.ClassRecords);
                view.Filter = null;
                lstbx_Classes.ItemsSource = view;
            }
        }
        private void FilterBox_KeyUp(object sender, KeyEventArgs e)
        {
            string ftxt = FilterBox.Text.ToLower();
            ICollectionView view = CollectionViewSource.GetDefaultView(CurrentDataBase.ClassRecords);
            if(ftxt == null)
            {
                view.Filter = null;
                
            }
            else
            {
                view.Filter = delegate (object item) {
                    var classes = item as ClassRecord;
                    if (classes != null &&
                    classes.Class.ToLower().Contains(ftxt)) return true;
                    return false;
                }; ;
            }
            
            lstbx_Classes.ItemsSource = view;
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
            if(scidx >= 0)
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
            string usagepkg = null;
            var usageexp = 0;

            if (lstbx_Usages.SelectedIndex >= 0 && currentView == 0)
            {
                var c = lstbx_Usages.SelectedItem as ClassUsage;
                usagepkg = CurrentDataBase.FileList[c.FileKey];
                usageexp = c.ExportUID;
            }
            else if(lstbx_AnimUsages.SelectedIndex >= 0 && currentView == 1)
            {
                var a = lstbx_AnimUsages.SelectedItem as Tuple<int,int>;
                usagepkg = CurrentDataBase.FileList[a.Item1];
                usageexp = a.Item2;
            }

            if(usagepkg == null)
            {
                MessageBox.Show("Usage file unknown.");
                return;
            }


            OpenInToolkit("PackageEditor", usagepkg, usageexp);
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
            string rootPath = null;
            switch (currentGame)
            {
                case MEGame.ME1:
                    rootPath = ME1Directory.gamePath;
                    break;
                case MEGame.ME2:
                    rootPath = ME2Directory.gamePath;
                    break;
                case MEGame.ME3:
                    rootPath = ME3Directory.gamePath;
                    break;
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
                case "PackageEditor":
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

        #endregion

        #region Scan
        private async void ScanGame()
        {
            string rootPath = null;
            string outputDir = CurrentDBPath;
            if(CurrentDBPath == null)
            {
                outputDir = App.AppDataFolder;
            }
            switch (currentGame)
            {
                case MEGame.ME1:
                    rootPath = ME1Directory.gamePath;
                    break;
                case MEGame.ME2:
                    rootPath = ME2Directory.gamePath;
                    break;
                case MEGame.ME3:
                    rootPath = ME3Directory.gamePath;
                    break;
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
            StatusBar_Progress.Visibility = Visibility.Visible;
            StatusBar_RightSide_LastSaved.Visibility = Visibility.Hidden;
            StatusBar_CancelBtn.Visibility = Visibility.Visible;
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;
            CurrentOverallOperationText = $"Generating Database...";

            //Clear database
            ClearDataBase();
            CurrentDataBase.GenerationDate = DateTime.Now.ToString();
            GeneratedClasses.Clear();
            GeneratedAnims.Clear();
            GeneratedValueChecker.Clear();



            _dbqueue = new BlockingCollection<ClassRecord>(); //Reset queue for multiple operations

            //Background Consumer to compile Class records for subsequent class readings
            dbworker = new BackgroundWorker();
            dbworker.DoWork += DBProcessor;
            dbworker.RunWorkerCompleted += dbworker_RunWorkerCompleted;
            dbworker.WorkerSupportsCancellation = true;
            dbworker.RunWorkerAsync();

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
                    OverallProgressValue++; //Concurrency 
                    CurrentDumpingItems.Remove(x);
                });
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = App.CoreCount });

            AllDumpingItems = new List<ClassScanSingleFileTask>();
            CurrentDumpingItems.ClearEx();
            foreach (var item in files)
            {
                CurrentDataBase.FileList.Add(Path.GetFileNameWithoutExtension(item));
                var threadtask = new ClassScanSingleFileTask(item);
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
                CurrentOverallOperationText = $"Dump canceled. Processing Queue.";
            }
            else
            {
                OverallProgressValue = 100;
                OverallProgressMaximum = 100;
                CurrentOverallOperationText = "Dump completed. Processing Queue.";
            }
            _dbqueue.CompleteAdding();
        }


        private void dbworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            dbworker.CancelAsync();
            CommandManager.InvalidateRequerySuggested();
            try
            {
               
                //Add and sort Classes
                CurrentDataBase.ClassRecords.AddRange(GeneratedClasses.Values);
                CurrentDataBase.ClassRecords.Sort(x => x.Class);
                foreach(var c in CurrentDataBase.ClassRecords)
                {
                    c.PropertyRecords.Sort(x => x.Property);
                }

                //Add animations
                CurrentDataBase.Animations.AddRange(GeneratedAnims.Values);
                CurrentDataBase.Animations.Sort(x => x.AnimSequence);


                GeneratedClasses.Clear();
                GeneratedAnims.Clear();
                GeneratedValueChecker.Clear();
                isProcessing = false;
                StatusBar_Progress.Visibility = Visibility.Hidden;
                StatusBar_RightSide_LastSaved.Visibility = Visibility.Visible;
                StatusBar_CancelBtn.Visibility = Visibility.Hidden;
                TopDock.IsEnabled = true;
                MidDock.IsEnabled = true;
                SaveDatabase();
                MessageBox.Show("Done");
            }
            catch
            {
                MessageBox.Show("Unable to finish.", "Error", MessageBoxButton.OK);
            }
        }

        private void DBProcessor(object sender, DoWorkEventArgs e)
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

                    foreach( var r in record.PropertyRecords)
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
                        CurrentOverallOperationText = $"Processing Queue. {_dbqueue.Count}";
                    }

                }
                catch(Exception err)
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
        public List<string> FileList { get; } = new List<string>();
        public ObservableCollectionExtended<ClassRecord> ClassRecords { get; } = new ObservableCollectionExtended<ClassRecord>();
        public ObservableCollectionExtended<Material> Materials { get; } = new ObservableCollectionExtended<Material>();
        public ObservableCollectionExtended<Animation> Animations { get; } = new ObservableCollectionExtended<Animation>();
        public PropsDataBase(MEGame meGame, string GenerationDate, ObservableCollectionExtended<ClassRecord> ClassRecords, ObservableCollectionExtended<Material> Materials, ObservableCollectionExtended<Animation> Animations)
        {
            this.meGame = meGame;
            this.GenerationDate = GenerationDate;
            this.ClassRecords.AddRange(ClassRecords);
            this.Materials.AddRange(Materials);
            this.Animations.AddRange(Animations);
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
        public string ParentPackage;
        public bool IsDLC;
        public ObservableCollectionExtended<(string, int)> MaterialUsages { get; } = new ObservableCollectionExtended<(string, int)>(); //File reference then export

        public ObservableCollectionExtended<string> MatSettings { get; } = new ObservableCollectionExtended<string>();

        public Material(string MaterialName, string ParentPackage, bool IsDLC, ObservableCollectionExtended<(string, int)> MaterialUsages, ObservableCollectionExtended<string> MatSettings)
        {
            this.MaterialName = MaterialName;
            this.ParentPackage = ParentPackage;
            this.IsDLC = IsDLC;
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
        public string AnimData;
        public float Length;
        public ObservableCollectionExtended<Tuple<int, int>> AnimUsages { get; } = new ObservableCollectionExtended<Tuple<int, int>>(); //File reference then export

        public Animation(string AnimSequence, string AnimData, float Length, ObservableCollectionExtended<Tuple<int, int>> AnimUsages)
        {
            this.AnimSequence = AnimSequence;
            this.AnimData = AnimData;
            this.Length = Length;
            this.AnimUsages.AddRange(AnimUsages);
        }

        public Animation()
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

        public ClassScanSingleFileTask(string file)
        {
            File = file;
            ShortFileName = Path.GetFileNameWithoutExtension(file);

        }

        public bool DumpCanceled;

        private readonly string File;

        /// <summary>
        /// Dumps Property data to concurrent dictionary
        /// </summary>
        public void dumpPackageFile(MEGame GameBeingDumped, PropertyDB dumper)
        {
            dumper.CurrentOverallOperationText = $"Generating Database... Files: { dumper.OverallProgressValue}/{dumper.OverallProgressMaximum} {dumper._dbqueue.Count} Found Classes: { dumper.GeneratedClasses.Count} Animations: { dumper.GeneratedAnims.Count}";
            
            try
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(File))
                {
                    if (GameBeingDumped == MEGame.Unknown) //Correct mapping
                    {
                        GameBeingDumped = pcc.Game;
                    }

                    int pFileKey = dumper.CurrentDataBase.FileList.FindIndex(i => i == ShortFileName);

                    foreach (ExportEntry exp in pcc.Exports)
                    {
                        string pClass = exp.ClassName;  //Handle basic class record
                        string pExp = exp.ObjectName;
                        string pSuperClass = null;
                        string pDefinitionPackage = null;
                        int pDefUID = 0;
                        int pExportUID = exp.UIndex;
                        bool pIsdefault = false;  //Setup default cases

                        if (exp.ClassName != "Class")
                        {
                            if (exp.ObjectName.StartsWith("Default__"))
                            {
                                pIsdefault = true;
                            }
                            var pList = new ObservableCollectionExtended<PropertyRecord>();
                            var props = exp.GetProperties(false, true);
                            foreach (var p in props)
                            {
                                string pName = p.Name;
                                string pType = p.PropType.ToString();
                                //string pValue = "null";
                                //switch (p)
                                //{
                                //    case ArrayPropertyBase parray:
                                //        pValue = "Array";
                                //        break;
                                //    case StructProperty pstruct:
                                //        pValue = "Struct";
                                //        break;
                                //    case NoneProperty pnone:
                                //        pValue = "None";
                                //        break;
                                //    case ObjectProperty pobj:
                                //        if (pobj.Value != 0)
                                //        {
                                //            pValue = pcc.getEntry(pobj.Value).ClassName;
                                //        }
                                //        break;
                                //    case BoolProperty pbool:
                                //        pValue = pbool.Value.ToString();
                                //        break;
                                //    case IntProperty pint:
                                //        if (pIsdefault)
                                //        {
                                //            pValue = pint.Value.ToString();
                                //        }
                                //        else
                                //        {
                                //            pValue = "int"; //Keep DB size down
                                //        }
                                //        break;
                                //    case FloatProperty pflt:
                                //        if (pIsdefault)
                                //        {
                                //            pValue = pflt.Value.ToString();
                                //        }
                                //        else
                                //        {
                                //            pValue = "float"; //Keep DB size down
                                //        }
                                //        break;
                                //    case NameProperty pnme:
                                //        pValue = pnme.Value.ToString();
                                //        break;
                                //    case ByteProperty pbte:
                                //        pValue = pbte.Value.ToString();
                                //        break;
                                //    case EnumProperty penum:
                                //        pValue = penum.Value.ToString();
                                //        break;
                                //    case StrProperty pstr:
                                //        if (pIsdefault)
                                //        {
                                //            pValue = pstr;
                                //        }
                                //        else
                                //        {
                                //            pValue = "string";
                                //        }
                                //        break;
                                //    case StringRefProperty pstrref:
                                //        if (pIsdefault)
                                //        {
                                //            pValue = pstrref.Value.ToString();
                                //        }
                                //        else
                                //        {
                                //            pValue = "TLK StringRef";
                                //        }
                                //        break;
                                //    case DelegateProperty pdelg:
                                //        if (pdelg.Value != null)
                                //        {
                                //            var pscrdel = pdelg.Value.Object;
                                //            if (pscrdel != 0)
                                //            {
                                //                pValue = pcc.getEntry(pscrdel).ClassName;
                                //            }
                                //        }
                                //        break;
                                //    default:
                                //        pValue = p.ToString();
                                //        break;
                                //}


                                var NewPropertyRecord = new PropertyRecord(pName, pType);
                                pList.Add(NewPropertyRecord);
                            }


                            var NewUsageRecord = new ClassUsage(pFileKey, pExportUID, pIsdefault);
                            var NewClassRecord = new ClassRecord(pClass, pDefinitionPackage, pDefUID, pSuperClass, pList, new ObservableCollectionExtended<ClassUsage>() { NewUsageRecord });
                            string valueKey = string.Concat(pClass, ShortFileName, pIsdefault.ToString());
                            if (!dumper.GeneratedClasses.TryAdd(pClass, NewClassRecord) && dumper.GeneratedValueChecker.TryAdd(valueKey, true))
                            {
                                dumper._dbqueue.Add(NewClassRecord);

                            }

                            if (exp.ClassName == "AnimSequence")
                            {
                                string aSet = null;
                                var pSet = exp.GetProperty<NameProperty>("SequenceName");
                                if (pSet != null)
                                {
                                    aSet = pSet.Value;
                                }
                                float aLength = 0;
                                var pLength = exp.GetProperty<FloatProperty>("SequenceLength");
                                if (pLength != null)
                                {
                                    aLength = pLength.Value;
                                }
                                var NewAnim = new Animation(pExp, aSet, aLength, new ObservableCollectionExtended<Tuple<int, int>>() { new Tuple<int, int>(pFileKey, pExportUID) });
                                if (!dumper.GeneratedAnims.TryAdd(pExp, NewAnim))
                                {
                                    var anim = dumper.GeneratedAnims[pExp];
                                    anim.AnimUsages.Add(new Tuple<int, int>(pFileKey, pExportUID));
                                }
                            }
                        }
                        else
                        { 
                            pClass = exp.ObjectName;
                            pSuperClass = exp.SuperClassName;
                            pDefUID = exp.UIndex;
                            var NewUsageRecord = new ClassUsage(pFileKey, pExportUID, pIsdefault);
                            var NewPropertyRecord = new PropertyRecord("None", "NoneProperty");
                            var NewClassRecord = new ClassRecord(pClass, ShortFileName, pDefUID, pSuperClass, new ObservableCollectionExtended<PropertyRecord>() { NewPropertyRecord }, new ObservableCollectionExtended<ClassUsage>() { NewUsageRecord });
                            if (!dumper.GeneratedClasses.TryAdd(pClass, NewClassRecord))
                            {
                                dumper._dbqueue.Add(NewClassRecord);
    
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception {ShortFileName} SingleFileProcess {e}");
            }
        }
    }
    #endregion

}
