using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;

namespace ME3Explorer.PropertyDatabase
{
    /// <summary>
    /// Interaction logic for PropertyDB
    /// </summary>
    public partial class PropertyDB : WPFBase
    {
        #region Declarations

        public MEGame currentGame { get; set; }

        public PropsDataBase CurrentDataBase { get; } = new PropsDataBase(MEGame.Unknown, null, new ObservableCollectionExtended<ClassRecord>());
        public ConcurrentDictionary<String, ClassRecord> GeneratedClasses = new ConcurrentDictionary<String, ClassRecord>();
        public ConcurrentDictionary<String, Boolean> GeneratedValueChecker = new ConcurrentDictionary<String, Boolean>();
        private ObservableCollectionExtended<PropertyRecord> CurrentProps { get; } = new ObservableCollectionExtended<PropertyRecord>();
        private ObservableCollectionExtended<PropertyUsage> CurrentUsages { get; } = new ObservableCollectionExtended<PropertyUsage>();
        private string CurrentDBPath { get; set; }

        public ICommand GenerateDBCommand { get; set; }
        public ICommand SaveDBCommand { get; set; }
        public ICommand SwitchMECommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }
        public ICommand OpenSourcePkgCommand { get; set; }
        public ICommand GoToSuperclassCommand { get; set; }
        public ICommand OpenUsagePkgCommand { get; set; }
        public ICommand FilterSeqClassCommand { get; set; }

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
            return lstbx_Usages.SelectedIndex >= 0;
        }
        public override void handleUpdate(List<PackageUpdate> updates)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Startup/Exit

        public PropertyDB()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Property Database WPF", new WeakReference(this));
            LoadCommands();

            //Get default path
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
            //Dump Database
            Properties.Settings.Default.PropertyDBPath = CurrentDBPath;
            Properties.Settings.Default.PropertyDBGame = currentGame.ToString();
        }

        #endregion

        #region UserCommands
        public void GenerateDatabase()
        {
            ScanGame(currentGame);
        }

        public void LoadDatabase()
        {
            if(CurrentDBPath == null)
            {
                return;
            }

            //Load database
            if(File.Exists(CurrentDBPath))
            {
                var readData = JsonConvert.DeserializeObject<PropsDataBase>(File.ReadAllText(CurrentDBPath));

                CurrentDataBase.meGame = readData.meGame;
                CurrentDataBase.GenerationDate = readData.GenerationDate;
                CurrentDataBase.ClassRecords.Clear();
                CurrentDataBase.ClassRecords.AddRange(readData.ClassRecords);
                int classCount = CurrentDataBase.ClassRecords.Count;
                CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {classCount}";
            }
            else
            {
                CurrentOverallOperationText = "No database found.";
            }

        }

        public async void SaveDatabase()
        {
            CurrentOverallOperationText = $"Database saving...";
            await Task.Delay(10);
            ////Save database to JSON directly to file
            using (StreamWriter file = File.CreateText(CurrentDBPath))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, CurrentDataBase);
            }
            CurrentOverallOperationText = $"Database saved.";
            await Task.Delay(5000);
            CurrentOverallOperationText = $"Database generated {CurrentDataBase.GenerationDate} Classes: {CurrentDataBase.ClassRecords.Count}";
        }


        public void SwitchGame(object param)
        {
            var p = param as string;
            switchME1_menu.IsChecked = false;
            switchME2_menu.IsChecked = false;
            switchME3_menu.IsChecked = false;
            CurrentDataBase.ClassRecords.Clear();
            CurrentDataBase.GenerationDate = null;
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
            CurrentDBPath = System.IO.Path.Combine(App.AppDataFolder, $"PropertyDB{currentGame}.JSON");

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
            var usagepkg = CurrentDataBase.ClassRecords[lstbx_Classes.SelectedIndex].PropertyRecords[lstbx_Properties.SelectedIndex].PropertyUsages[lstbx_Usages.SelectedIndex].Filename;
            var usageexp = CurrentDataBase.ClassRecords[lstbx_Classes.SelectedIndex].PropertyRecords[lstbx_Properties.SelectedIndex].PropertyUsages[lstbx_Usages.SelectedIndex].ExportUID;
            if (usagepkg == null)
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
        private async void ScanGame(MEGame game)
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

            rootPath = System.IO.Path.GetFullPath(rootPath);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc" };
            List<string> files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories).Where(s => supportedExtensions.Contains(System.IO.Path.GetExtension(s.ToLower()))).ToList();
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
            CurrentOverallOperationText = $"Generating Database...  {OverallProgressValue}/{OverallProgressMaximum}";

            //Clear database
            CurrentDataBase.meGame = currentGame;
            CurrentDataBase.GenerationDate = DateTime.Now.ToString();
            CurrentDataBase.ClassRecords.Clear();
            CurrentProps.Clear();
            CurrentUsages.Clear();
            GeneratedClasses.Clear();
            GeneratedValueChecker.Clear();

            _dbqueue = new BlockingCollection<ClassRecord>(); //Reset queue for multiple operations

            //Background Consumer copies strings into Class
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
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = App.CoreCount }); // How many items at the same time 

            AllDumpingItems = new List<ClassScanSingleFileTask>();
            CurrentDumpingItems.ClearEx();
            foreach (var item in files)
            {
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
                CurrentDataBase.ClassRecords.AddRange(GeneratedClasses.Values);
                CurrentDataBase.ClassRecords.Sort(x => x.Class);
                foreach(var c in CurrentDataBase.ClassRecords)
                {
                    c.PropertyRecords.Sort(x => x.Property);
                }

                GeneratedClasses.Clear();
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

                    var pName = record.PropertyRecords[0].Property;

                    var proprecord = oldClassRecord.PropertyRecords.FirstOrDefault(p => p.Property == pName);
                    if (proprecord == null)
                    {
                        oldClassRecord.PropertyRecords.AddRange(record.PropertyRecords);
                    }
                    else
                    {
                        proprecord.PropertyUsages.AddRange(record.PropertyRecords[0].PropertyUsages);
                    }

                    if(isProcessing)
                    {
                        CurrentOverallOperationText = $"Processing Queue. {_dbqueue.Count}";
                    }

                }
                catch(Exception err)
                {
                    MessageBox.Show($"Error writing. {record.Class} {record.PropertyRecords[0].Property} {record.PropertyRecords[0].PropertyUsages[0].Filename} {err}");
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
        public ObservableCollectionExtended<ClassRecord> ClassRecords { get; } = new ObservableCollectionExtended<ClassRecord>();

        public PropsDataBase(MEGame meGame, string GenerationDate, ObservableCollectionExtended<ClassRecord> ClassRecords)
        {
            this.meGame = meGame;
            this.GenerationDate = GenerationDate;
            this.ClassRecords.AddRange(ClassRecords);
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

        public ClassRecord(string Class, string Definition_package, int Definition_UID, string SuperClass, ObservableCollectionExtended<PropertyRecord> PropertyRecords)
        {
            this.Class = Class;
            this.Definition_package = Definition_package;
            this.Definition_UID = Definition_UID;
            this.SuperClass = SuperClass;
            this.PropertyRecords.AddRange(PropertyRecords);
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
        public ObservableCollectionExtended<PropertyUsage> PropertyUsages { get; } = new ObservableCollectionExtended<PropertyUsage>();
        public PropertyRecord(string Property, string Type, ObservableCollectionExtended<PropertyUsage> PropertyUsages)
        {
            this.Property = Property;
            this.Type = Type;
            this.PropertyUsages.AddRange(PropertyUsages);
        }
        public PropertyRecord()
        { }
    }

    public class PropertyUsage : NotifyPropertyChangedBase
    {
        public string Filename { get; set; }
        public int ExportUID { get; set; }
        public bool IsDefault { get; set; }
        public string Value { get; set; }
        public PropertyUsage(string Filename, int ExportUID, bool IsDefault, string Value)
        {
            this.Filename = Filename;
            this.ExportUID = ExportUID;
            this.IsDefault = IsDefault;
            this.Value = Value;
        }
        public PropertyUsage()
        { }
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
            ShortFileName = System.IO.Path.GetFileNameWithoutExtension(file);

        }

        public bool DumpCanceled;

        private readonly string File;

        /// <summary>
        /// Dumps Conversation strings to xl worksheet
        /// </summary>
        /// <workbook>Output excel workbook</workbook>
        public void dumpPackageFile(MEGame GameBeingDumped, PropertyDB dumper)
        {
            dumper.CurrentOverallOperationText = $"Generating Database... Files: { dumper.OverallProgressValue}/{dumper.OverallProgressMaximum} Classes Found: { dumper.GeneratedClasses.Count} Unique Property Values: { dumper.GeneratedValueChecker.Count}";

            try
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(File))
                {
                    if (GameBeingDumped == MEGame.Unknown) //Correct mapping
                    {
                        GameBeingDumped = pcc.Game;
                    }

                    foreach (ExportEntry exp in pcc.Exports)
                    {
                        string pClass = exp.ClassName;  //Handle basic class record
                        string pSuperClass = null;
                        string pDefinitionPackage = null;
                        int pDefUID = 0;
                        string pFile = ShortFileName;
                        int pExport = exp.UIndex;
                        bool pIsdefault = false;  //Setup default cases

                        if (exp.ClassName != "Class")
                        {
                            if (exp.ObjectName.StartsWith("Default__"))
                            {
                                pIsdefault = true;
                            }
                            var props = exp.GetProperties(false, true);
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
                                        if (pobj.Value != 0)
                                        {
                                            pValue = pcc.getEntry(pobj.Value).ClassName;
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
                                            pValue = "String";
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
                                                pValue = pcc.getEntry(pscrdel).ClassName;
                                            }
                                        }
                                        break;
                                    default:
                                        pValue = p.ToString();
                                        break;
                                }

                                var NewUsageRecord = new PropertyUsage(pFile, pExport, pIsdefault, pValue);
                                var NewPropertyRecord = new PropertyRecord(pName, pType, new ObservableCollectionExtended<PropertyUsage>() { NewUsageRecord });
                                var NewClassRecord = new ClassRecord(pClass, pDefinitionPackage, pDefUID, pSuperClass, new ObservableCollectionExtended<PropertyRecord>() { NewPropertyRecord });
                                string valueKey = string.Concat(pClass, pName, pValue, pIsdefault.ToString());
                                if (!dumper.GeneratedClasses.TryAdd(pClass, NewClassRecord) && dumper.GeneratedValueChecker.TryAdd(valueKey, true))
                                {
                                    dumper._dbqueue.Add(NewClassRecord);

                                }
                            }
                        }
                        else
                        { 
                            pClass = exp.ObjectName;
                            pSuperClass = exp.SuperClassName;
                            pDefUID = exp.UIndex;
                            var NewUsageRecord = new PropertyUsage(pFile, pExport, pIsdefault, "None");
                            var NewPropertyRecord = new PropertyRecord("None", "NoneProperty", new ObservableCollectionExtended<PropertyUsage>() { NewUsageRecord });
                            var NewClassRecord = new ClassRecord(pClass, pFile, pDefUID, pSuperClass, new ObservableCollectionExtended<PropertyRecord>() { NewPropertyRecord });
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
                MessageBox.Show($"Exception {ShortFileName} SingleFileProcess");
            }
        }
    }
    #endregion

}
