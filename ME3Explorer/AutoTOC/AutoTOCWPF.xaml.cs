using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Ini;
using FontAwesome5;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;

namespace ME3Explorer.AutoTOC
{
    /// <summary>
    /// Interaction logic for AutoTOCWPF.xaml
    /// </summary>
    public partial class AutoTOCWPF : NotifyPropertyChangedWindowBase
    {
        private readonly object _myCollectionLock = new object();

        public ObservableCollectionExtended<ListBoxTask> TOCTasks { get; } = new ObservableCollectionExtended<ListBoxTask>();

        public AutoTOCWPF()
        {
            DataContext = this;
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("AutoTOC WPF", new WeakReference(this));
            Analytics.TrackEvent("Used tool", new Dictionary<string, string>()
            {
                { "Toolname", "AutoTOC" }
            });
            LoadCommands();
            InitializeComponent();
            BindingOperations.EnableCollectionSynchronization(TOCTasks, _myCollectionLock);
        }

        public ICommand RunAutoTOCCommand { get; private set; }
        public ICommand GenerateDLCTOCCommand { get; private set; }
        public ICommand BuildME1FileListCommand { get; private set; }

        public BackgroundWorker TOCWorker;

        private void LoadCommands()
        {
            RunAutoTOCCommand = new GenericCommand(RunAutoTOC, CanRunAutoTOC);
            GenerateDLCTOCCommand = new GenericCommand(GenerateSingleDLCTOC, BackgroundThreadNotRunning);
            BuildME1FileListCommand = new GenericCommand(GenerateME1FileList, BackgroundThreadNotRunning);
        }

        private void GenerateME1FileList()
        {
            // AUTO DLC MOUNTING

            // 1. GET LIST OF DLC DIRECTORIES, SET MAIN VARIABLES
            //IList() me1DLCs; // set list of directorys
            string DLCDirectory = ME1Directory.DLCPath;

            string[] dlcList = Directory.GetDirectories(DLCDirectory, "*.*", SearchOption.TopDirectoryOnly);

            var dlcTable = new Dictionary<int, string>();


            // 2. READ AUTOLOAD.INI FROM EACH DLC.  BUILD TABLE OF DIRECTORIES & MOUNTS
            foreach (string dlcDir in dlcList)
            {
                if (dlcDir.EndsWith("DLC_UNC",StringComparison.InvariantCultureIgnoreCase))
                {
                    dlcTable.Add(1, "DLC_UNC");
                }
                else if (dlcDir.EndsWith("DLC_VEGAS", StringComparison.InvariantCultureIgnoreCase))
                {
                    dlcTable.Add(2, "DLC_VEGAS");
                }
                else
                {
                    string autoLoadPath = Path.Combine(dlcDir, "autoload.ini");  //CHECK IF FILE EXISTS?
                    if (File.Exists(autoLoadPath))
                    {
                        IniFile dlcAutoload = new IniFile(autoLoadPath);
                        string name = Path.GetFileName(dlcDir);
                        int mount = Convert.ToInt32(dlcAutoload.ReadValue("ME1DLCMOUNT", "ModMount"));
                        dlcTable.Add(mount, name);
                    }
                }
            }
            // ADD BASEGAME = 0
            dlcTable.Add(0, "BioGame");


            // 3. REMOVE ALL SEEKFREEPCPATHs/DLCMOVIEPATHS FROM $DOCUMENTS$\BIOWARE\MASS EFFECT\CONFIG\BIOENGINE.ini
            string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var bioEnginePath = Path.Combine(userDocs, "BioWare", "Mass Effect", "Config", "BIOEngine.ini");
            try
            {
                File.SetAttributes(bioEnginePath, File.GetAttributes(bioEnginePath) & ~FileAttributes.ReadOnly);
            }
            catch (IOException e)
            {
                MessageBox.Show($"BioEngine not found. Run config or game to set it up. {e.FlattenException()}");
                return;
            }
            
            
            var BioEngine = new IniFile(bioEnginePath);

            //Clean out seekfreepaths and moviepaths
            while (BioEngine.ReadValue("Core.System", "SeekFreePCPaths") != "")
            {
                BioEngine.RemoveKey("Core.System", "SeekFreePCPaths");
            }

            while (BioEngine.ReadValue("Core.System", "DLC_MoviePaths") != "")
            {
                BioEngine.RemoveKey("Core.System", "DLC_MoviePaths");
            }

            // 4. ADD SEEKFREE PATHS IN REVERSE ORDER (HIGHEST= BIOGAME, ETC).
            //SORT INTO REVERSE ORDER 0 => HIGHEST FOR BIOENGINE
            foreach (KeyValuePair<int, string> item in dlcTable.OrderBy(k => k.Key))
            {
                if (item.Key == 0)
                {
                    //The @"string\thing" allows you to use \ instead of \\. Very good if you are using paths. Though most times you should use Path.Combine() as it will prevent you missing one by accident
                    BioEngine.WriteNewValue("Core.System", "SeekFreePCPaths", @"..\BioGame\CookedPC");
                }
                else
                {
                    BioEngine.WriteNewValue("Core.System", "SeekFreePCPaths", $@"..\DLC\{item.Value}\CookedPC");
                    if(Directory.Exists(Path.Combine(ME1Directory.DLCPath, item.Value, "Movies")))
                    {
                        BioEngine.WriteNewValue("Core.System", "DLC_MoviePaths", $@"..\DLC\{item.Value}\Movies"); //Add MoviePath if present
                    }
                }
            }

            


            // 5. BUILD FILEINDEX.TXT FILE FOR EACH DLC AND BASEGAME
            // BACKUP BASEGAME Fileindex.txt => Fileindex.bak if not done already.
            var fileIndexBackupFile = Path.Combine(ME1Directory.cookedPath, "FileIndex.bak");
            if (!File.Exists(fileIndexBackupFile))
            {
                //This might fail as the game will be installed into a write-protected directory for most users by default
                try
                {
                    File.Copy(Path.Combine(ME1Directory.cookedPath, "FileIndex.txt"), fileIndexBackupFile);
                }
                catch (IOException e)
                {
                    MessageBox.Show($"Error backup up FileIndex.txt:\n{e.FlattenException()}");
                    return;
                }
            }

            // CALL FUNCTION TO BUILD EACH FILEINDEX.  START WITH HIGHEST DLC MOUNT -> ADD TO MASTER FILE LIST
            // DO NOT ADD DUPLICATES
            TOCTasks.ClearEx();

            var masterList = new List<string>(); 
            foreach (KeyValuePair<int, string> fileListStem in dlcTable.OrderByDescending(k => k.Key))
            {
                if (fileListStem.Value == "BioGame")
                {
                    //Using a list is pass by reference so our copy and the function's copy will be the same.
                    //Note this does not work with primitive types like int (unless using the ref keyword), or immutable types like string.
                    //(Immutable in c# = they can't be changed. modifying a string will return a new string instead)
                    GenerateFileList(Path.Combine(ME1Directory.BioGamePath, "CookedPC"), masterList);
                }
                else
                {
                    GenerateFileList(Path.Combine(ME1Directory.DLCPath, fileListStem.Value, "CookedPC"), masterList);
                }
            }




            //7. FINAL MESSAGE ON TOC TASKS
            TOCTasks.Add(new ListBoxTask
            {
                Header = "Done",
                Icon = EFontAwesomeIcon.Solid_Check,
                Foreground = Brushes.Green,
                Spinning = false
            });
        }

        /// <summary>
        /// Generates new items to the FileIndex.txt (ME1) removing duplicate references contained in masterList
        /// </summary>
        /// <param name="CookedPath"></param>
        /// <param name="masterList"></param>
        private void GenerateFileList(string CookedPath, List<string> masterList)
        {

            string[] extensions = { ".sfm", ".upk", ".bik", ".u", ".isb" };

            //remove trailing slash
            string dlcCookedDir = Path.GetFullPath(CookedPath); //standardize  
            ListBoxTask task = new ListBoxTask($"Generating file index for {dlcCookedDir}");
            TOCTasks.Add(task);
            int rootLength = dlcCookedDir.Length + 1; //trailing slash path separator. This is used to strip off the absolute part of the path and leave only relative

            //Where first as not all files need to be selected and then filtered, they should be filtered and then selected
            List<string> files = (Directory.EnumerateFiles(dlcCookedDir, "*.*", SearchOption.AllDirectories)
                .Where(s => extensions.Any(ext => ext == Path.GetExtension(s).ToLower()))
                .Select(p => p.Remove(0, rootLength))).ToList();

            var addressedFiles = new List<string>();  //sub list of files that actually are addressed by the game (not duplicated at higher levels)
            foreach (string file in files)
            {
                Debug.WriteLine(file);
                if (!masterList.Contains(file))
                {
                    //Only add items that are not already done.
                    masterList.Add(file);
                    addressedFiles.Add(file);
                }
            }

            string fileName = Path.Combine(dlcCookedDir, "FileIndex.txt");
            File.WriteAllLines(fileName, addressedFiles);
            task.Complete($"Generated file index for {dlcCookedDir}");
            TOCTasks.Add(new ListBoxTask
            {
                Header = "Done",
                Icon = EFontAwesomeIcon.Solid_Check,
                Foreground = Brushes.Green,
                Spinning = false
            });
        }

        private void GenerateSingleDLCTOC()
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "PCConsoleTOC.bin|PCConsoleTOC.bin",
                FileName = "PCConsoleTOC.bin"
            };
            var result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                string path = Path.GetDirectoryName(d.FileName) + "\\";
                TOCWorker = new BackgroundWorker();
                TOCWorker.DoWork += GenerateSingleTOC_BackgroundThread;
                TOCWorker.RunWorkerCompleted += GenerateAllTOCs_Completed;
                TOCWorker.RunWorkerAsync(path);
            }
        }

        private void GenerateSingleTOC_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            TOCTasks.ClearEx();
            prepareToCreateTOC(e.Argument as string, TOCTasks);
            TOCTasks.Add(new ListBoxTask
            {
                Header = "TOC created",
                Icon = EFontAwesomeIcon.Solid_Check,
                Foreground = Brushes.Green,
                Spinning = false
            });
        }

        private bool BackgroundThreadNotRunning()
        {
            return TOCWorker == null;
        }

        private void RunAutoTOC()
        {
            TOCWorker = new BackgroundWorker();
            TOCWorker.DoWork += GenerateAllTOCs_BackgroundThread;
            TOCWorker.RunWorkerCompleted += GenerateAllTOCs_Completed;
            TOCWorker.RunWorkerAsync();
        }

        private void GenerateAllTOCs_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested(); //Refresh commands
            TOCWorker = null;
        }

        private void GenerateAllTOCs_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            TOCTasks.ClearEx();
            GenerateAllTOCs(TOCTasks);
            TOCTasks.Add(new ListBoxTask
            {
                Header = "AutoTOC complete",
                Icon = EFontAwesomeIcon.Solid_Check,
                Foreground = Brushes.Green,
                Spinning = false
            });
        }

        private bool CanRunAutoTOC()
        {
            if (string.IsNullOrEmpty(ME3Directory.BIOGamePath) || !Directory.Exists(ME3Directory.BIOGamePath))
            {
                return false;
            }
            return TOCWorker == null;
        }

        /// <summary>
        /// Prepares to create the indexed TOC file by gathering data and then passing it to the TOC creation function
        /// </summary>
        /// <param name="consoletocFile"></param>
        /// <param name="tocTasks"></param>
        public static void prepareToCreateTOC(string consoletocFile, IList<ListBoxTask> tocTasks = null)
        {
            if (!consoletocFile.EndsWith("\\"))
            {
                consoletocFile += "\\";
            }
            List<string> files = GetFiles(consoletocFile);
            if (files.Count != 0)
            {
                //These variable names.......
                ListBoxTask task = null;
                if (tocTasks != null)
                {
                    task = new ListBoxTask($"Creating TOC in {consoletocFile}");
                    tocTasks.Add(task);
                }
                string t = files[0];
                int n = t.IndexOf("DLC_");
                if (n > 0)
                {
                    for (int i = 0; i < files.Count; i++)
                        files[i] = files[i].Substring(n);
                    string t2 = files[0];
                    n = t2.IndexOf("\\");
                    for (int i = 0; i < files.Count; i++)
                        files[i] = files[i].Substring(n + 1);
                }
                else
                {
                    n = t.IndexOf("BIOGame");
                    if (n > 0)
                    {
                        for (int i = 0; i < files.Count; i++)
                            files[i] = files[i].Substring(n);
                    }
                }
                string pathbase;
                string t3 = files[0];
                int n2 = t3.IndexOf("BIOGame");
                if (n2 >= 0)
                {
                    pathbase = Path.GetDirectoryName(Path.GetDirectoryName(consoletocFile)) + "\\";
                }
                else
                {
                    pathbase = consoletocFile;
                }
                CreateTOC(pathbase, consoletocFile + "PCConsoleTOC.bin", files.ToArray());
                task?.Complete($"Created TOC for {consoletocFile}");
            }
        }

        private static void CreateTOC(string basepath, string tocFile, string[] files)
        {

            byte[] SHA1 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            using (FileStream fs = new FileStream(tocFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                fs.WriteInt32(0x3AB70C13);
                fs.WriteInt32(0x0);
                fs.WriteInt32(0x1);
                fs.WriteInt32(0x8);
                fs.WriteInt32(files.Length);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    if (i == files.Length - 1)//Entry Size
                        fs.WriteUInt16(0);
                    else
                        fs.WriteUInt16((ushort)(0x1D + file.Length));
                    fs.WriteUInt16(0);//Flags
                    if (Path.GetFileName(file).ToLower() != "pcconsoletoc.bin")
                    {
                        fs.WriteInt32((int)(new FileInfo(basepath + file)).Length);//Filesize
                    }
                    else
                    {
                        fs.WriteInt32(0);//Filesize
                    }
                    fs.Write(SHA1, 0, 20);
                    foreach (char c in file)
                        fs.WriteByte((byte)c);
                    fs.WriteByte(0);
                }
            }
        }

        private static List<string> GetFiles(string basefolder)
        {
            var res = new List<string>();
            string test = Path.GetFileName(Path.GetDirectoryName(basefolder));
            res.AddRange(GetTocableFiles(basefolder));
            DirectoryInfo folder = new DirectoryInfo(basefolder);
            DirectoryInfo[] folders = folder.GetDirectories();
            if (folders.Length != 0)
            {
                if (test != "BIOGame")
                {
                    foreach (DirectoryInfo f in folders)
                        res.AddRange(GetFiles(basefolder + f.Name + "\\"));
                }
                else
                {
                    foreach (DirectoryInfo f in folders)
                    {
                        if (f.Name == "CookedPCConsole" || /*f.Name == "DLC" ||*/ f.Name == "Movies" || f.Name == "Splash")
                            res.AddRange(GetFiles(basefolder + f.Name + "\\"));
                    }
                }
            }

            return res;
        }

        private static IEnumerable<string> GetTocableFiles(string path)
        {
            string[] Pattern = { "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.txt", "*.cnd", "*.upk", "*.tfc" };
            var res = new List<string>();
            foreach (string s in Pattern)
                res.AddRange(Directory.GetFiles(path, s));
            return res.ToArray();
        }

        public static void GenerateAllTOCs(IList<ListBoxTask> tocTasks = null)
        {
            List<string> folders = (new DirectoryInfo(ME3Directory.DLCPath)).GetDirectories().Select(d => d.FullName).ToList();
            folders.Add(ME3Directory.gamePath + @"BIOGame\");
            folders.ForEach(consoletocFile => prepareToCreateTOC(consoletocFile, tocTasks));
        }

        private void AutoTOCWPF_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
        }

        private void ListBox_OnLoaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;

            var scrollViewer = FindScrollViewer(listBox);

            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += (o, args) =>
                {
                    if (args.ExtentHeightChange > 0)
                        scrollViewer.ScrollToBottom();
                };
            }
        }

        // Search for ScrollViewer, breadth-first
        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            var queue = new Queue<DependencyObject>(new[] { root });

            do
            {
                var item = queue.Dequeue();

                if (item is ScrollViewer scrollViewer)
                    return scrollViewer;

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++)
                    queue.Enqueue(VisualTreeHelper.GetChild(item, i));
            } while (queue.Count > 0);

            return null;
        }
    }
}
