using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
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
        private ObservableCollectionExtended<ClassRecord> CurrentClasses { get; } = new ObservableCollectionExtended<ClassRecord>();
        private string CurrentDBPath { get; set; }

        public ICommand GenerateDBCommand { get; set; }
        public ICommand SaveDBCommand { get; set; }
        public ICommand SwitchMECommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }

        /// <summary>
        /// Items show in the list that are currently being processed
        /// </summary>
        public ObservableCollectionExtended<ClassScanSingleFileTask> CurrentDumpingItems { get; set; } = new ObservableCollectionExtended<ClassScanSingleFileTask>();

        /// <summary>
        /// All items in the queue
        /// </summary>
        private List<ClassScanSingleFileTask> AllDumpingItems;

        private static BackgroundWorker dbworker = new BackgroundWorker();
        public BlockingCollection<List<string>> _dbqueue = new BlockingCollection<List<string>>();
        private ActionBlock<ClassScanSingleFileTask> ProcessingQueue;

        /// <summary>
        /// Cancelation of dumping
        /// </summary>
        private bool DumpCanceled;

        /// <summary>
        /// output debug info to excel
        /// </summary>
        public bool shouldDoDebugOutput;

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

            if (CurrentDBPath != null && CurrentDBPath.EndsWith("xml") && File.Exists(CurrentDBPath) && game != MEGame.Unknown)
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
            //SaveDBCommand = new GenericCommand();
            SwitchMECommand = new RelayCommand(SwitchGame);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
        }

        private void PropertyDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;
            //Dump Database
            //Save settings (path, currentGame)
            Properties.Settings.Default.PropertyDBPath = CurrentDBPath;
            Properties.Settings.Default.PropertyDBGame = currentGame.ToString();
        }

        #endregion

        #region UserCommands
        public void GenerateDatabase()
        {
            //Use Property Dumper - run directly? to generate?
            //Save to XML
            ScanGame(currentGame);
        }

        public void LoadDatabase()
        {
            if(CurrentDBPath == null)
            {
                //Open XML file
                return;
            }

            //Load database
            if(File.Exists(CurrentDBPath))
            {
                var readData = XmlHelper.FromXmlFile<PropsDataBase>(CurrentDBPath);
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

        public void SaveDatabase()
        {
            //Save database to XML
            XmlHelper.ToXmlFile(CurrentDataBase, CurrentDBPath);
            CurrentOverallOperationText = $"Database saved.";
        }


        public void SwitchGame(object param)
        {
            var p = param as string;
            switchME1_menu.IsChecked = false;
            switchME2_menu.IsChecked = false;
            switchME3_menu.IsChecked = false;
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
            CurrentDBPath = System.IO.Path.Combine(App.AppDataFolder, $"PropertyDB{currentGame}.xml");

            LoadDatabase();
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
            CurrentDataBase.ClassRecords.SortDescending(x => x.Class);

            MessageBox.Show("Done");
            SaveDatabase();
        }

        private async Task dumpPackages(List<string> files, MEGame game)
        {
            CurrentOverallOperationText = "Generating Database...";
            StatusBar_Progress.Visibility = Visibility.Visible;
            StatusBar_RightSide_LastSaved.Visibility = Visibility.Hidden;
            StatusBar_CancelBtn.Visibility = Visibility.Visible;
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;
            //bool isProcessing = true;

            //Clear database
            CurrentDataBase.meGame = currentGame;
            CurrentDataBase.GenerationDate = DateTime.Now.ToString();
            CurrentDataBase.ClassRecords.Clear();

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

            if (DumpCanceled)
            {
                DumpCanceled = false;
                //CurrentFileProgressValue = 0;
                OverallProgressMaximum = 100;
                CurrentOverallOperationText = "Dump canceled";
            }
            else
            {
                OverallProgressValue = 100;
                OverallProgressMaximum = 100;
                CurrentOverallOperationText = "Dump completed";
            }

        }


        private void dbworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            dbworker.CancelAsync();
            CommandManager.InvalidateRequerySuggested();
            try
            {
                StatusBar_Progress.Visibility = Visibility.Hidden;
                StatusBar_RightSide_LastSaved.Visibility = Visibility.Visible;
                StatusBar_CancelBtn.Visibility = Visibility.Hidden;
            }
            catch
            {
                MessageBox.Show("Unable to save excel file. Check it is not open.", "Error", MessageBoxButton.OK);
            }
        }

        private void DBProcessor(object sender, DoWorkEventArgs e)
        {

            foreach (List<string> property in _dbqueue.GetConsumingEnumerable(CancellationToken.None))
            {
                //Property List<string> { pClass 0, pSuperClass 1, pDefinitionPackage 2, pName 3, pFile 4, pExport 5, pIsdefault.ToString() 6, pType 7, pValue 8 };
                try
                {


                    //Check if class has been generated
                    //Check if property has been generated
                    //Add usage or create record as needed

                    var classrecord = CurrentDataBase.ClassRecords.FirstOrDefault(c => c.Class == property[0]);
                    if(classrecord == null)
                    {
                        classrecord = new ClassRecord(property[0], null, null, new List<PropertyRecord>());
                        CurrentDataBase.ClassRecords.Add(classrecord);
                    }

                    if (classrecord.SuperClass == null)
                    {
                        classrecord.SuperClass = property[1];
                    }
                    if (classrecord.Definition_package == null)
                    {
                        classrecord.Definition_package = property[2];
                    }


                    var proprecord = classrecord.PropertyRecords.FirstOrDefault(p => p.Property == property[3]);
                    if (proprecord == null)
                    {
                        proprecord = new PropertyRecord(property[3], new List<PropertyUsage>());
                        classrecord.PropertyRecords.Add(proprecord);
                    }

                    var usagelist = proprecord.PropertyUsages.FirstOrDefault(p => p.Value == property[8]);
                    if (usagelist == null)
                    {
                        var usage = new PropertyUsage(property[4], property[5], Boolean.Parse(property[6]), property[7], property[8]);
                        proprecord.PropertyUsages.Add(usage);
                    }
                 
                }
                catch
                {
                    MessageBox.Show($"Error writing. {property[0]} {property[4]} {property[5]}");
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
    public class PropsDataBase
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

    public class ClassRecord
    {
        public string Class { get; set; }
        public string Definition_package { get; set; }
        public string SuperClass { get; set; }
        public List<PropertyRecord> PropertyRecords { get; } = new List<PropertyRecord>();

        public ClassRecord(string Class, string Definition_package, string SuperClass, List<PropertyRecord> PropertyRecords)
        {
            this.Class = Class;
            this.Definition_package = Definition_package;
            this.SuperClass = SuperClass;
            this.PropertyRecords.AddRange(PropertyRecords);
        }

        public ClassRecord()
        { }
    }

    public class PropertyRecord
    {
        public string Property { get; set; }
        public List<PropertyUsage> PropertyUsages { get; } = new List<PropertyUsage>();
        public PropertyRecord(string Property, List<PropertyUsage> PropertyUsages)
        {
            this.Property = Property;
            this.PropertyUsages.AddRange(PropertyUsages);
        }
        public PropertyRecord()
        { }
    }

    public class PropertyUsage
    {
        public string Filename { get; set; }
        public string ExportUID { get; set; }
        public bool IsDefault { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public PropertyUsage(string Filename, string ExportUID, bool IsDefault, string Type, string Value)
        {
            this.Filename = Filename;
            this.ExportUID = ExportUID;
            this.IsDefault = IsDefault;
            this.Type = Type;
            this.Value = Value;
        }
        public PropertyUsage()
        { }
    }
    #endregion

    #region SingleFileScan
    public class ClassScanSingleFileTask : NotifyPropertyChangedBase
    {
        private string _currentOverallOperationText;
        public string CurrentOverallOperationText
        {
            get => _currentOverallOperationText;
            set => SetProperty(ref _currentOverallOperationText, value);
        }

        private int _currentFileProgressValue;
        public int CurrentFileProgressValue
        {
            get => _currentFileProgressValue;
            set => SetProperty(ref _currentFileProgressValue, value);
        }

        private int _currentFileProgressMaximum;
        public int CurrentFileProgressMaximum
        {
            get => _currentFileProgressMaximum;
            set => SetProperty(ref _currentFileProgressMaximum, value);
        }

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
            CurrentOverallOperationText = $"Dumping {ShortFileName}";
        }

        public bool DumpCanceled;

        private readonly string File;

        /// <summary>
        /// Dumps Conversation strings to xl worksheet
        /// </summary>
        /// <workbook>Output excel workbook</workbook>
        public void dumpPackageFile(MEGame GameBeingDumped, PropertyDatabase.PropertyDB dumper)
        {
            string fileName = ShortFileName.ToUpper();

            try
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(File))
                {
                    if (GameBeingDumped == MEGame.Unknown) //Correct mapping
                    {
                        GameBeingDumped = pcc.Game;
                    }

                    CurrentFileProgressMaximum = pcc.ExportCount;
                    foreach (ExportEntry exp in pcc.Exports)
                    {
                        CurrentFileProgressValue = exp.UIndex;
                        string pClass = exp.ClassName;  //Handle basic class record
                        string pSuperClass = null;
                        string pDefinitionPackage = null;
                        string pFile = ShortFileName;
                        string pExport = exp.UIndex.ToString();
                        if (exp.ClassName == "Class")
                        {
                            pClass = exp.ObjectName;
                            pSuperClass = exp.SuperClassName;
                            pDefinitionPackage = pFile;
                        }

                        bool pIsdefault = false;  //Setup default cases
                        if (exp.ObjectName.StartsWith("Default__"))
                        {
                            pIsdefault = true;
                        }

                        var props = exp.GetProperties();
                        foreach(var p in props)
                        {
                            string pName = p.Name;
                            string pType = p.PropType.ToString();
                            string pValue = "null";
                            switch(p)
                            {
                                case ArrayPropertyBase parray:
                                    pValue = "Array";
                                    break;
                                case StructProperty pstruct:
                                    pValue = "Struct";
                                    break;
                                case ObjectProperty pobj:
                                    if(pobj.Value != 0)
                                    {
                                        pValue = pcc.getEntry(pobj.Value).ClassName;
                                    }
                                    break;
                                case BoolProperty pbool:
                                    pValue = pbool.Value.ToString();
                                    break;
                                case IntProperty pint:
                                    //pValue = pint.Value.ToString();
                                    pValue = "int"; //Keep DB size down
                                    break;
                                case FloatProperty pflt:
                                    //pValue = pflt.Value.ToString();
                                    pValue = "float"; //Keep DB size down
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
                                    pValue = "String";
                                    break;
                                case StringRefProperty pstrref:
                                    pValue = "TLK StringRef";
                                    break;
                                case DelegateProperty pdelg:
                                    if (pdelg.Value != null)
                                    {
                                        var pscrdel = pdelg.Value.Object;
                                        if(pscrdel != 0)
                                        {
                                            pValue = pcc.getEntry(pscrdel).ClassName;
                                        }
                                    }
                                    break;
                                default:
                                    pValue = p.ToString();
                                    break;
                            }

                            var dbout = new List<string> { pClass, pSuperClass, pDefinitionPackage, pName, pFile, pExport, pIsdefault.ToString(), pType, pValue };
                            dumper._dbqueue.Add(dbout);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception {ShortFileName}");
            }
        }
    }
    #endregion



    #region XMLGen
    public static class XmlHelper
    {
        public static bool NewLineOnAttributes { get; set; }
        /// <summary>
        /// Serializes an object to an XML string, using the specified namespaces.
        /// </summary>
        public static string ToXml(object obj, XmlSerializerNamespaces ns)
        {
            Type T = obj.GetType();

            var xs = new XmlSerializer(T);
            var ws = new XmlWriterSettings { Indent = true, NewLineOnAttributes = NewLineOnAttributes, OmitXmlDeclaration = true };

            var sb = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(sb, ws))
            {
                xs.Serialize(writer, obj, ns);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Serializes an object to an XML string.
        /// </summary>
        public static string ToXml(object obj)
        {
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            return ToXml(obj, ns);
        }

        /// <summary>
        /// Deserializes an object from an XML string.
        /// </summary>
        public static PropsDataBase FromXml<T>(string xml)
        {
            XmlSerializer xs = new XmlSerializer(typeof(PropsDataBase));
            using (StringReader sr = new StringReader(xml))
            {
                return (PropsDataBase)xs.Deserialize(sr);
            }
        }

        /// <summary>
        /// Deserializes an object from an XML string, using the specified type name.
        /// </summary>
        public static object FromXml(string xml, string typeName)
        {
            Type T = Type.GetType(typeName);
            XmlSerializer xs = new XmlSerializer(T);
            using (StringReader sr = new StringReader(xml))
            {
                return xs.Deserialize(sr);
            }
        }

        /// <summary>
        /// Serializes an object to an XML file.
        /// </summary>
        public static void ToXmlFile(Object obj, string filePath)
        {
            var xs = new XmlSerializer(obj.GetType());
            var ns = new XmlSerializerNamespaces();
            var ws = new XmlWriterSettings { Indent = true, NewLineOnAttributes = NewLineOnAttributes, OmitXmlDeclaration = true };
            ns.Add("", "");

            using (XmlWriter writer = XmlWriter.Create(filePath, ws))
            {
                xs.Serialize(writer, obj);
            }
        }

        /// <summary>
        /// Deserializes an object from an XML file.
        /// </summary>
        public static PropsDataBase FromXmlFile<T>(string filePath)
        {
            StreamReader sr = new StreamReader(filePath);
            try
            {
                var result = FromXml<PropsDataBase>(sr.ReadToEnd());
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("There was an error attempting to read the file " + filePath + "\n\n" + e.InnerException.Message);
            }
            finally
            {
                sr.Close();
            }
        }
    }
    #endregion
}
