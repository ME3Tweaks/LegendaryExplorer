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
using FontAwesome5;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.ToolsetDev.MemoryAnalyzer;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.Win32;

namespace LegendaryExplorer.Tools.AutoTOC
{
    /// <summary>
    /// Interaction logic for AutoTOCWindow.xaml
    /// </summary>
    public partial class AutoTOCWindow : TrackingNotifyPropertyChangedWindowBase
    {
        private readonly object _myCollectionLock = new();

        public ObservableCollectionExtended<ListBoxTask> TOCTasks { get; } = new();

        public AutoTOCWindow() : base("AutoTOC", true)
        {
            DataContext = this;
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
            RunAutoTOCCommand = new GenericCommand(RunAutoTOC, () => CanRunAutoTOC(SelectedGame));
            GenerateDLCTOCCommand = new GenericCommand(GenerateSingleDLCTOC, BackgroundThreadNotRunning);
            BuildME1FileListCommand = new GenericCommand(GenerateME1FileList, BackgroundThreadNotRunning);
        }

        private MEGame _selectedGame = MEGame.ME3;
        public MEGame SelectedGame { get => _selectedGame; set => SetProperty(ref _selectedGame, value); }

        public ObservableCollectionExtended<MEGame> GameOptions { get; } = new (){ MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 };

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
                if (dlcDir.EndsWith("DLC_UNC", StringComparison.InvariantCultureIgnoreCase))
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
                        DuplicatingIni dlcAutoload = DuplicatingIni.LoadIni(autoLoadPath);
                        string name = Path.GetFileName(dlcDir);
                        int mount = Convert.ToInt32(dlcAutoload["ME1DLCMOUNT"]["ModMount"].Value);
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

            var BioEngine = DuplicatingIni.LoadIni(bioEnginePath);

            //Clean out seekfreepaths and moviepaths
            BioEngine["Core.System"].RemoveAllNamedEntries("SeekFreePCPaths");
            BioEngine["Core.System"].RemoveAllNamedEntries("DLC_MoviePaths");

            // 4. ADD SEEKFREE PATHS IN REVERSE ORDER (HIGHEST= BIOGAME, ETC).
            //SORT INTO REVERSE ORDER 0 => HIGHEST FOR BIOENGINE
            foreach (KeyValuePair<int, string> item in dlcTable.OrderBy(k => k.Key))
            {
                if (item.Key == 0)
                {
                    //The @"string\thing" allows you to use \ instead of \\. Very good if you are using paths. Though most times you should use Path.Combine() as it will prevent you missing one by accident
                    BioEngine["Core.System"].Entries.Add(new DuplicatingIni.IniEntry("SeekFreePCPaths", @"..\BioGame\CookedPC"));
                }
                else
                {
                    BioEngine["Core.System"].Entries.Add(new DuplicatingIni.IniEntry("SeekFreePCPaths", $@"..\DLC\{item.Value}\CookedPC"));
                    if (Directory.Exists(Path.Combine(ME1Directory.DLCPath, item.Value, "Movies")))
                    {
                        //Add MoviePath if present
                        BioEngine["Core.System"].Entries.Add(new DuplicatingIni.IniEntry("DLC_MoviePaths", $@"..\DLC\{item.Value}\Movies"));
                    }
                }
            }

            BioEngine.WriteToFile(bioEnginePath);

            // 5. BUILD FILEINDEX.TXT FILE FOR EACH DLC AND BASEGAME
            // BACKUP BASEGAME Fileindex.txt => Fileindex.bak if not done already.
            var fileIndexBackupFile = Path.Combine(ME1Directory.CookedPCPath, "FileIndex.bak");
            if (!File.Exists(fileIndexBackupFile))
            {
                //This might fail as the game will be installed into a write-protected directory for most users by default
                try
                {
                    File.Copy(Path.Combine(ME1Directory.CookedPCPath, "FileIndex.txt"), fileIndexBackupFile);
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
            var task = new ListBoxTask($"Generating file index for {dlcCookedDir}");
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
            var d = new SaveFileDialog
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
            CreateTOC(SelectedGame, e.Argument as string, TOCTasks);
            TOCTasks.Add(new ListBoxTask
            {
                Header = "TOC created",
                Icon = EFontAwesomeIcon.Solid_Check,
                Foreground = Brushes.Green,
                Spinning = false
            });
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

        private bool BackgroundThreadNotRunning()
        {
            return TOCWorker == null;
        }

        private void GenerateAllTOCs_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            TOCTasks.ClearEx();
            GenerateAllTOCs(TOCTasks, SelectedGame);
            TOCTasks.Add(new ListBoxTask
            {
                Header = $"{SelectedGame} AutoTOC complete",
                Icon = EFontAwesomeIcon.Solid_Check,
                Foreground = Brushes.Green,
                Spinning = false
            });
        }

        private bool CanRunAutoTOC(MEGame game)
        {
            if (string.IsNullOrEmpty(MEDirectories.GetBioGamePath(game)) || !Directory.Exists(MEDirectories.GetBioGamePath(game)))
            {
                return false;
            }
            return TOCWorker == null;
        }

        public static void GenerateAllTOCs(IList<ListBoxTask> tocTasks = null, MEGame game = MEGame.LE3)
        {
            var folders = new List<string>
            {
                MEDirectories.GetBioGamePath(game)
            };
            if (game != MEGame.LE1)
            {
                folders.AddRange((new DirectoryInfo(MEDirectories.GetDLCPath(game)).GetDirectories()
                    .Select(d => d.FullName)));
            }
            folders.ForEach(consoletocFile => CreateTOC(game, consoletocFile, tocTasks));
        }

        /// <summary>
        /// Passes the input folder to the TOC creation function and writes the output to a file
        /// </summary>
        /// <param name="folderToToc">BIOGame/DLC Folder to generate TOC file in</param>
        /// <param name="tocTasks">List of UI Task objects to display in UI</param>
        public static void CreateTOC(MEGame game, string folderToToc, IList<ListBoxTask> tocTasks = null)
        {
            if (TOCCreator.IsTOCableFolder(folderToToc, game is MEGame.LE2 or MEGame.LE3))
            {
                ListBoxTask task = null;
                if (tocTasks != null)
                {
                    task = new ListBoxTask($"Creating TOC in {folderToToc}");
                    tocTasks.Add(task);
                }

                try
                {
                    var tocOutFile = Path.Combine(folderToToc, "PCConsoleTOC.bin");
                    MemoryStream toc = new MemoryStream();
                    if (folderToToc == MEDirectories.GetBioGamePath(game))
                    {
                        toc = TOCCreator.CreateBasegameTOCForDirectory(folderToToc, game);
                    }
                    else toc = TOCCreator.CreateDLCTOCForDirectory(folderToToc, game);

                    File.WriteAllBytes(tocOutFile, toc.ToArray());
                    task?.Complete($"Created TOC for {folderToToc}");
                }
                catch
                {
                    task?.Failed($"Failed to create TOC for {folderToToc}");
                }
            }
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
