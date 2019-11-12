using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gammtek.Conduit.Extensions.Collections.Generic;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using SlavaGu.ConsoleAppLauncher;

namespace ME3Explorer.TFCCompactor
{
    /// <summary>
    /// Interaction logic for TFCCompactor.xaml
    /// </summary>
    public partial class TFCCompactor : NotifyPropertyChangedWindowBase
    {
        private BackgroundWorker backgroundWorker;

        public TFCCompactor()
        {
            DataContext = this;
            LoadCommands();
            BusyProgressBarMax = 100;
            InitializeComponent();
            IsBusyUI = true;
            BusyText = "Verifying MEM";
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += EnsureCriticalFiles;
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is string message)
                {
                    MessageBox.Show($"An error occured fetching MassEffectModder command line tools that are required for TFC Compactor. Please comes to the ME3Tweaks Discord for assistance.\n\n{message}", "Error fetching texture tools");
                    CurrentOperationText = "Error downloading command line tools for textures";
                }
                else if (b.Result == null)
                {
                    ToolsDownloaded = true;
                    CurrentOperationText = "Select DLC mod to compact textures for";
                }
                IsBusyUI = false;
            };
            bw.RunWorkerAsync();
        }
        private bool movieScan;
        private int _progressBarMax = 100, _progressBarValue;
        private bool _progressBarIndeterminate;
        public int ProgressBarMax
        {
            get => _progressBarMax;
            set => SetProperty(ref _progressBarMax, value);
        }
        public int ProgressBarValue
        {
            get => _progressBarValue;
            set => SetProperty(ref _progressBarValue, value);
        }
        public bool ProgressBarIndeterminate
        {
            get => _progressBarIndeterminate;
            set => SetProperty(ref _progressBarIndeterminate, value);
        }
        private GameWrapper _selectedGame;

        public GameWrapper SelectedGame
        {
            get => _selectedGame;
            set => SetProperty(ref _selectedGame, value);
        }

        private string _currentOperationText;
        public string CurrentOperationText
        {
            get => _currentOperationText;
            set => SetProperty(ref _currentOperationText, value);
        }

        private bool _scanForGameCompleted;

        public bool ScanForGameCompleted
        {
            get => _scanForGameCompleted;
            set => SetProperty(ref _scanForGameCompleted, value);
        }

        private bool _toolsDownloaded;

        public bool ToolsDownloaded
        {
            get => _toolsDownloaded;
            set => SetProperty(ref _toolsDownloaded, value);
        }

        private bool _isBusyUI;

        public bool IsBusyUI
        {
            get => _isBusyUI;
            set => SetProperty(ref _isBusyUI, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private int _busyProgressBarMax;

        public int BusyProgressBarMax
        {
            get => _busyProgressBarMax;
            set => SetProperty(ref _busyProgressBarMax, value);
        }

        private int _busyProgressBarValue;
        public int BusyProgressBarValue
        {
            get => _busyProgressBarValue;
            set => SetProperty(ref _busyProgressBarValue, value);
        }

        private string _stagingDirectory;
        public string StagingDirectory
        {
            get => _stagingDirectory;
            set => SetProperty(ref _stagingDirectory, value);
        }

        public bool IsNotBusy => backgroundWorker == null || !backgroundWorker.IsBusy;

        private string SelectedDLCModFolder;

        public ObservableCollectionExtended<GameWrapper> GameList { get; } = new ObservableCollectionExtended<GameWrapper>();
        public ObservableCollectionExtended<string> CustomDLCFolderList { get; } = new ObservableCollectionExtended<string>();
        public ObservableCollectionExtended<TFCSelector> TextureCachesToPullFromList { get; } = new ObservableCollectionExtended<TFCSelector>();

        public ICommand CompactTFCCommand { get; set; }
        public ICommand ScanCommand { get; set; }
        public ICommand ChangeStagingDirCommand { get; set; }

        private void LoadCommands()
        {
            if (ME2Directory.DLCPath != null)
            {
                GameList.Add(new GameWrapper(MEGame.ME2, "Mass Effect 2", ME2Directory.DLCPath));
            }

            if (ME3Directory.DLCPath != null)
            {
                GameList.Add(new GameWrapper(MEGame.ME3, "Mass Effect 3", ME3Directory.DLCPath));
                GameList.Add(new GameWrapper(MEGame.ME3, "Mass Effect 3 (Movies)", ME3Directory.DLCPath));
            }

            GameList.Add(new GameWrapper(MEGame.Unknown, "Select game...", null) { IsBrowseForCustom = true, IsCustomPath = true });


            CompactTFCCommand = new GenericCommand(BeginTFCCompaction, () => ScanForGameCompleted && IsNotBusy && ToolsDownloaded && !string.IsNullOrEmpty(StagingDirectory) && Directory.Exists(StagingDirectory));
            ScanCommand = new GenericCommand(BeginReferencedTFCScan, () => DLCModFolderIsSelected() && IsNotBusy && ToolsDownloaded);
            ChangeStagingDirCommand = new GenericCommand(ChangeStagingDir, () => IsNotBusy && ToolsDownloaded);
        }

        private void ChangeStagingDir()
        {
            var openFolder = new CommonOpenFileDialog();
            openFolder.IsFolderPicker = true;
            openFolder.Title = "Select staging directory";
            openFolder.AllowNonFileSystemItems = false;
            openFolder.EnsurePathExists = true;
            if (openFolder.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }
            var dir = openFolder.FileName;
            if (!Directory.Exists(dir))
            {
                MessageBox.Show("The backup destination directory does not exist: " + dir, "Directory does not exist");
                return;
            }

            if (Directory.EnumerateFileSystemEntries(dir).Any())
            {
                var result = MessageBox.Show("The selected directory is not empty. Do you want to delete it's contents and use it as the staging directory?", "Staging directory not empty", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    DeleteDirectory(dir);
                    Thread.Sleep(100); //seems like there is some sort of delay here or windows might just not create the directory
                    Directory.CreateDirectory(dir);
                }
                else
                {
                    MessageBox.Show("The staging directory directory must be empty.", "Directory is not empty");
                    return;
                }
            }
            StagingDirectory = dir;
        }

        public void EnsureCriticalFiles(object sender, DoWorkEventArgs args)
        {
            void progressCallback(long bytesDownloaded, long bytesToDownload)
            {
                BusyProgressBarMax = (int)bytesToDownload;
                BusyProgressBarValue = (int)bytesDownloaded;
            }

            try
            {
                string staticExecutablesDirectory = Directory.CreateDirectory(Path.Combine(App.AppDataFolder, "staticexecutables")).FullName;


                string memCS = Path.Combine(staticExecutablesDirectory, "MassEffectModderNoGuiCS.exe");
                if (!File.Exists(memCS))
                {
                    BusyText = "Downloading MEM command line (C#)";
                    var downloadError = OnlineContent.EnsureStaticExecutable("MassEffectModderNoGuiCS.exe", progressCallback);
                    if (downloadError != null)
                    {
                        args.Result = downloadError;
                        return;
                    }
                }


                string memQT = Path.Combine(staticExecutablesDirectory, "MassEffectModderNoGuiQT.exe");
                if (!File.Exists(memQT))
                {
                    BusyText = "Downloading MEM command line (qT)";
                    var downloadError = OnlineContent.EnsureStaticExecutable("MassEffectModderNoGuiQT.exe", progressCallback);
                    if (downloadError != null)
                    {
                        args.Result = downloadError;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                args.Result = "Error downloading required files:\n" + ExceptionHandlerDialogWPF.FlattenException(e);
            }
        }

        public static string[] BasegameTFCs = { "CharTextures", "Movies", "Textures", "Lighting", "Movies" };
        private void BeginReferencedTFCScan()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            var dlcDir = SelectedGame.DLCPath;
            dlcDir = Path.Combine(dlcDir, SelectedDLCModFolder);

            backgroundWorker.DoWork += (a, b) =>
            {
                FindReferencedTextures(dlcDir, true);
            };
            backgroundWorker.RunWorkerCompleted += (a, b) =>
            {
                ScanForGameCompleted = true;
                backgroundWorker = null;
                CurrentOperationText = "Initial scan completed";
                OnPropertyChanged(nameof(IsNotBusy));
                CommandManager.InvalidateRequerySuggested();
            };
            backgroundWorker.RunWorkerAsync();
            OnPropertyChanged(nameof(IsNotBusy));
        }

        private List<string> FindReferencedTextures(string dlcDir, bool forSelecting = true)
        {
            CurrentOperationText = "Getting list of files";
            ProgressBarValue = 0;
            ProgressBarIndeterminate = true;

            string[] files = Directory.GetFiles(dlcDir, "*.pcc", SearchOption.AllDirectories);
            ProgressBarMax = files.Length;
            ProgressBarIndeterminate = false;
            SortedSet<TFCSelector> referencedTFCs = new SortedSet<TFCSelector>();

            if (files.Any())
            {
                foreach (string file in files)
                {
                    CurrentOperationText = $"Scanning {Path.GetFileName(file)}...";
                    using (var package = MEPackageHandler.OpenMEPackage(file))
                    {
                        if (movieScan)
                        {
                            var movieExports = package.Exports.Where(x => x.ClassName == "TextureMovie");
                            foreach( var movietexture in movieExports)
                            {
                                if (movietexture.GetProperty<NameProperty>("TextureFileCacheName") is NameProperty tfcNameProperty)
                                {
                                    string tfcname = tfcNameProperty.Value;
                                    if (referencedTFCs.Add(new TFCSelector(tfcname, forSelecting)))
                                    {
                                        if (forSelecting)
                                        {
                                            Application.Current.Dispatcher.Invoke(delegate
                                            {
                                                TextureCachesToPullFromList.ReplaceAll(referencedTFCs);
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            var textureExports = package.Exports.Where(x => x.IsTexture());
                            foreach (var texture in textureExports)
                            {
                                if (texture.GetProperty<NameProperty>("TextureFileCacheName") is NameProperty tfcNameProperty)
                                {
                                    string tfcname = tfcNameProperty.Value;
                                    //if (tfcname == "CustTextures0")
                                    //{
                                    //    Debug.WriteLine($"CustTextures0 TFC Reference: {texture.FullPath} {texture.UIndex} in {texture.FileRef.FilePath}");
                                    //}
                                    if (!BasegameTFCs.Contains(tfcname))
                                    {
                                        //Check that external mips are referenced.
                                        //some texture2d have a tfc but don't have any external mips for some reason
                                        Texture2D texture2d = new Texture2D(texture);
                                        var topmip = texture2d.GetTopMip();
                                        if (topmip.storageType == StorageTypes.extLZO ||
                                            topmip.storageType == StorageTypes.extZlib ||
                                            topmip.storageType == StorageTypes.extUnc)
                                        {
                                            if (referencedTFCs.Add(new TFCSelector(tfcname, forSelecting)))
                                            {
                                                //Debug.WriteLine($"Reference to {tfcname} in {Path.GetFileName(texture.FileRef.FilePath)} {texture.UIndex} {texture.InstancedFullPath}");
                                                if (forSelecting)
                                                {
                                                    Application.Current.Dispatcher.Invoke(delegate
                                                    {
                                                        TextureCachesToPullFromList.ReplaceAll(referencedTFCs);
                                                    });
                                                }
                                            }
                                        }
                                        else
                                        {
                                            //Debug.WriteLine($"Skipping Reference, no external mips defined: {texture.GetFullPath} {texture.UIndex} in {texture.FileRef.FilePath}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    ProgressBarValue++;
                }
            }

            return referencedTFCs.Select(x => x.TFCName).ToList();
        }

        private void BeginTFCCompaction()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            string sourceGamePath = Path.GetDirectoryName(Path.GetDirectoryName(SelectedGame.DLCPath));
            string workingGamePath = StagingDirectory; //Todo: Allow user to change this path

            if(movieScan)
            {
                backgroundWorker.DoWork += (a, b) =>
                {
                    if (Directory.EnumerateFileSystemEntries(StagingDirectory).Any())
                    {
                        DeleteDirectory(StagingDirectory);
                        Directory.CreateDirectory(StagingDirectory);
                    }
                    CurrentOperationText = "Creating compaction workspace";
                    ProgressBarValue = 0;
                    ProgressBarMax = 100;
                    ProgressBarIndeterminate = true;

                    var TFCsToPullFrom = TextureCachesToPullFromList.Where(x => x.Selected).Select(x => x.TFCName);
                    var ExportsToBeReplaced = new List<(string, int, string)>(); //filename, export number, moviecache_offset
                    var MoviesToBeReplaced = new Dictionary<string, int>(); //moviecache_offset, offset
                    var MoviesWrittenToNewTFC = new Dictionary<string, (int, int)>(); //moviecache_offset, new offset, new length
                    //Copy basegame TFCs to staged
                    var tfclist = Directory.GetFiles(sourceGamePath, "*.tfc", SearchOption.AllDirectories);
                    foreach (var tfc in TextureCachesToPullFromList.Where(x => x.Selected))
                    {
                        string otfcPath = tfclist.FirstOrDefault(t => Path.GetFileNameWithoutExtension(t) == tfc.TFCName);
                        File.Copy(otfcPath, Path.Combine(StagingDirectory, Path.GetFileName(otfcPath)));
                    }
                    //Find all movie references
                    //Search all files in mod directory for texturemovie references to selected DLCs
                    string[] files = Directory.GetFiles(Path.Combine(sourceGamePath, "DLC", SelectedDLCModFolder), "*.pcc", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        CurrentOperationText = $"Scanning and extracting {Path.GetFileName(file)}...";
                        using (var package = MEPackageHandler.OpenMEPackage(file))
                        {
                            var movieExports = package.Exports.Where(x => x.ClassName == "TextureMovie");
                            foreach (var movietexture in movieExports)
                            {
                                if (movietexture.GetProperty<NameProperty>("TextureFileCacheName") is NameProperty tfcNameProperty)
                                {
                                    if (TFCsToPullFrom.Contains<string>(tfcNameProperty.Value))
                                    {
                                        var binary = movietexture.GetBinaryData();
                                        int offset = BitConverter.ToInt32(binary, 12);
                                        string bikfile = $"{tfcNameProperty.Value}_{offset}";
                                        ExportsToBeReplaced.Add((file, movietexture.UIndex, bikfile));
                                        if (!MoviesToBeReplaced.ContainsKey(bikfile))
                                        {
                                            MoviesToBeReplaced.Add(bikfile, offset);
                                            //Extract movie references
                                            string destination = Path.Combine(StagingDirectory, $"{bikfile}.bik");
                                            ExtractBikToFile(movietexture, destination);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    //Compile new tfc
                    Guid tfcGuid = Guid.NewGuid();
                    var tfcName = "Movies_" + SelectedDLCModFolder;
                    var outputTFC = Path.Combine(StagingDirectory, $"{tfcName}.tfc");
                    if (File.Exists(outputTFC))
                    {
                        File.Delete(outputTFC);
                    }
                    using (FileStream fs = new FileStream(outputTFC, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        fs.WriteGuid(tfcGuid);
                        fs.Flush();
                    }
                    //Import biks to cache
                    CurrentOperationText = $"Compiling biks to cache...";
                    foreach (var bik in MoviesToBeReplaced)
                    {
                        var addedmovie = ImportBiktoCache(Path.Combine(StagingDirectory, $"{bik.Key}.bik"), outputTFC);
                        MoviesWrittenToNewTFC.Add(bik.Key, addedmovie);
                    }
                    //Replace references in files
                    CurrentOperationText = $"Replacing file references...";
                    foreach (var expRef in ExportsToBeReplaced)
                    {
                        using (var package = MEPackageHandler.OpenMEPackage(expRef.Item1))
                        {
                            var expTexMov = package.GetUExport(expRef.Item2);
                            var moviedata = MoviesWrittenToNewTFC[expRef.Item3];

                            var binData = expTexMov.GetBinaryData();
                            binData.OverwriteRange(0, BitConverter.GetBytes(1));
                            binData.OverwriteRange(4, BitConverter.GetBytes(moviedata.Item1)); //Length
                            binData.OverwriteRange(8, BitConverter.GetBytes(moviedata.Item1)); //Length
                            binData.OverwriteRange(12, BitConverter.GetBytes(moviedata.Item2)); //offset
                            expTexMov.SetBinaryData(binData);

                            var props = expTexMov.GetProperties();
                            props.AddOrReplaceProp(new NameProperty(tfcName, "TextureFileCacheName"));
                            props.AddOrReplaceProp(tfcGuid.ToGuidStructProp("TFCFileGuid"));
                            expTexMov.WriteProperties(props);

                            package.Save();
                        }
                    }

                    //Copy tfc to ModFolder
                    CurrentOperationText = $"Cleaning Up...";
                    File.Copy(outputTFC, Path.Combine(sourceGamePath, "DLC", SelectedDLCModFolder, "CookedPCConsole", Path.GetFileName(outputTFC)), true);

                    //Clean Up
                    System.IO.DirectoryInfo di = new DirectoryInfo(StagingDirectory);
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }

                    ProgressBarValue = 0;
                    ProgressBarMax = 100;
                    ProgressBarIndeterminate = false;
                    var dependancylist = new List<string>() { tfcName };
                    dependancylist.AddRange(TextureCachesToPullFromList.Where(x => !x.Selected).Select(x => x.TFCName).ToList());
                    b.Result = new Tuple<CompactionResult, List<string>>(CompactionResult.RESULT_OK, dependancylist);
                    return;
                };

            }
            else
            {
                backgroundWorker.DoWork += (a, b) =>
                {
                    if (Directory.EnumerateFileSystemEntries(StagingDirectory).Any())
                    {
                        DeleteDirectory(StagingDirectory);
                        Directory.CreateDirectory(StagingDirectory);
                    }
                    CurrentOperationText = "Creating compaction workspace";
                    ProgressBarValue = 0;
                    ProgressBarMax = 100;
                    ProgressBarIndeterminate = false;

                    var dlcTFCsToPullFrom = TextureCachesToPullFromList.Where(x => x.Selected).Select(x => x.TFCName);
                    var tfcsToStage = TextureCachesToPullFromList.Select(x => x.TFCName);
                    //Create workspace for MEM
                    var game = (int)SelectedGame.Game;

                    //Create fake game directory
                    Directory.CreateDirectory(workingGamePath);
                    Directory.CreateDirectory(Path.Combine(workingGamePath, "Binaries"));
                    if (game == 3)
                    {
                        Directory.CreateDirectory(Path.Combine(workingGamePath, "Binaries", "win32"));
                        File.Create(Path.Combine(workingGamePath, "Binaries", "win32", "MassEffect3.exe")).Close();
                    }
                    else
                    {
                        //ME2
                        File.Create(Path.Combine(workingGamePath, "Binaries", "MassEffect2.exe")).Close();
                    }

                    string cookedDirName = game == 2 ? "CookedPC" : "CookedPCConsole";
                    Directory.CreateDirectory(Path.Combine(workingGamePath, "BioGame"));
                    var dlcDir = Path.Combine(workingGamePath, "BioGame", "DLC");
                    Directory.CreateDirectory(dlcDir);
                    var basegameCookedDir = Path.Combine(workingGamePath, "BioGame", cookedDirName);
                    Directory.CreateDirectory(basegameCookedDir);

                    //Copy basegame TFCs to cookedDiretory
                    var basegameDirToCopyFrom = MEDirectories.CookedPath(SelectedGame.Game);
                    var tfcs = Directory.GetFiles(basegameDirToCopyFrom, "*.tfc").ToList();
                    var currentgamefiles = MELoadedFiles.GetFilesLoadedInGame(SelectedGame.Game, forceReload: true, includeTFCs: true);
                    //var debug = currentgamefiles.Where(x => x.Value.Contains(".tfc")).ToList();
                    //debug.ForEach(x => Debug.WriteLine(x));
                    foreach (var tfc in tfcsToStage)
                    {
                        var fullname = tfc.EndsWith(".tfc") ? tfc : tfc + ".tfc";
                        if (currentgamefiles.TryGetValue(fullname, out string fullpath))
                        {
                            tfcs.Add(fullpath);
                        }
                        else if (SelectedGame.Game == MEGame.ME3)
                        {
                            //Attempt SFAR lookup at later stage below. Will abort if we cannot find TFC.
                            tfcs.Add(tfc); //no suffix
                        }
                        else
                        {
                            b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                            return;
                        }
                    }

                    ProgressBarIndeterminate = true;
                    foreach (var tfc in tfcs)
                    {
                        string tfcShortName = Path.GetFileNameWithoutExtension(tfc);
                        CurrentOperationText = $"Staging {tfcShortName}";
                        string destPath = Path.Combine(basegameCookedDir, tfcShortName + ".tfc");
                        if (File.Exists(tfc))
                        {
                            File.Copy(tfc, destPath, true);
                        }
                        else if (SelectedGame.Game == MEGame.ME3)
                        {
                            if (tfcShortName.StartsWith("Textures_DLC"))
                            {
                                string dlcFolderName = tfc.Substring(9);
                                var sfar = Path.Combine(MEDirectories.DLCPath(SelectedGame.Game), dlcFolderName, "CookedPCConsole", "Default.sfar");
                                if (File.Exists(sfar) && new FileInfo(sfar).Length > 32)
                                {
                                    //sfar exists and is not fully unpacked (with mem style 32 byte sfar)
                                    DLCPackage p = new DLCPackage(sfar);
                                    var tfcIndex = p.FindFileEntry(tfcShortName + ".tfc");
                                    if (tfcIndex >= 0)
                                    {
                                        var tfcMemory = p.DecompressEntry(tfcIndex);
                                        File.WriteAllBytes(destPath, tfcMemory.ToArray());
                                        tfcMemory.Close();
                                    }
                                    else
                                    {
                                        //Can't find TFC!
                                        b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                                        return;
                                    }
                                }
                                else
                                {
                                    //Can't find TFC!
                                    b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                                    return;
                                }
                            }
                            else
                            {
                                //Can't find TFC!
                                b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                                return;
                            }
                        }
                        else
                        {
                            //Can't find TFC!
                            if (dlcTFCsToPullFrom.Contains(tfc))
                            {
                                b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for pulling textures from in game directory: {tfc}");
                            }
                            else
                            {
                                b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for ensuring clean texture scan: {tfc}");
                            }
                            return;
                        }
                    }

                    //Copy DLC
                    var destDLCDir = Path.Combine(dlcDir, SelectedDLCModFolder);
                    var sourceDLCDir = Path.Combine(SelectedGame.DLCPath, SelectedDLCModFolder);
                    CopyDir.CopyAll_ProgressBar(new DirectoryInfo(sourceDLCDir), Directory.CreateDirectory(destDLCDir), backgroundWorker, ignoredExtensions: new string[] { ".tfc" });
                    // get MassEffectModder.ini
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MassEffectModder");
                    string _iniPath = Path.Combine(path, "MassEffectModder.ini");
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    if (!File.Exists(_iniPath))
                    {
                        File.Create(_iniPath);
                    }

                    Ini.IniFile ini = new Ini.IniFile(_iniPath);
                    var oldValue = ini.ReadValue("ME" + game, "GameDataPath");
                    ini.WriteValue("GameDataPath", "ME" + game, workingGamePath);

                    //Scan game
                    ProgressBarMax = 100;
                    ProgressBarValue = 0;
                    //List<string> missingTFCs = new List<string>();
                    List<string> errors = new List<string>();
                    var triggers = new Dictionary<string, Action<string>> {
                    { "TASK_PROGRESS", s => ProgressBarValue = int.Parse(s)},
                    { "PROCESSING_FILE", s => CurrentOperationText = $"Building texture map for {Path.GetFileName(s)}"},
                    //{ "ERROR_REFERENCED_TFC_NOT_FOUND", s => missingTFCs.Add(s) }, //this ipc can be thrown but we should not run into this error since we attempt to stage everything.
                    { "ERROR", s => errors.Add(s) },
                };

                    string args = $"--scan --gameid {game} --ipc";
                    var memProcess = MassEffectModder.MassEffectModderIPCWrapper.RunMEM(args, triggers);
                    while (memProcess.State == AppState.Running)
                    {
                        Thread.Sleep(100); //this is kind of hacky but it works
                    }

                    if (errors.Count > 0)
                    {
                        //Do something...
                        b.Result = new Tuple<CompactionResult, List<string>>(CompactionResult.RESULT_SCAN_ERRORS, errors);
                        return;
                    }

                    //Extract textures
                    var tempTextureCache = Directory.CreateDirectory(Path.Combine(workingGamePath, "TextureStaging")).FullName;
                    var singlepasscount = Directory.GetFiles(destDLCDir, "*.pcc", SearchOption.AllDirectories).Length;
                    var totalpasses = singlepasscount * dlcTFCsToPullFrom.Count();
                    int previousFullStepsDone = 0;
                    ProgressBarValue = 0;
                    ProgressBarMax = totalpasses;
                    foreach (var tfcname in dlcTFCsToPullFrom)
                    {
                        CurrentOperationText = $"Extracting referenced textures from {tfcname}";
                        triggers = new Dictionary<string, Action<string>> {
                        { "Package", s =>
                            {
                                int done = int.Parse(s.Substring(0,s.IndexOf('/')));
                                ProgressBarValue = previousFullStepsDone + done;
                            }
                        }
                    };

                        args = $"--extract-all-dds --gameid {game} --output \"{tempTextureCache}\" --tfc-name {tfcname}";
                        memProcess = MassEffectModder.MassEffectModderIPCWrapper.RunMEM(args, null, triggers); //this command does not support IPC commands
                        while (memProcess.State == AppState.Running)
                        {
                            Thread.Sleep(100); //this is kind of hacky but it works
                        }

                        previousFullStepsDone += singlepasscount;
                    }

                    //Install new textures
                    string newTextureCacheName = "Textures_" + SelectedDLCModFolder;
                    CurrentOperationText = $"Building " + newTextureCacheName;
                    ProgressBarValue = 0;
                    ProgressBarMax = Directory.GetFiles(tempTextureCache, "*.dds").Length;
                    triggers = new Dictionary<string, Action<string>> {
                    { "Installing", s =>
                        {
                            string remainingStr = s.Substring(5); //cut off "mod: "

                            int done = int.Parse(remainingStr.Substring(0,remainingStr.IndexOf(' ')));
                            ProgressBarValue = done;
                            CurrentOperationText = $"Building {newTextureCacheName} | {remainingStr.Substring(remainingStr.LastIndexOf(' ')).Trim()}";
                        }
                    }
                };

                    args = $"-dlc-mod-for-mgamerz {game} \"{tempTextureCache}\" {newTextureCacheName}";
                    memProcess = MassEffectModder.MassEffectModderIPCWrapper.RunMEM(args, null, triggers, true); //this command does not support IPC commands
                    while (memProcess.State == AppState.Running)
                    {
                        Thread.Sleep(100); //this is kind of hacky but it works
                    }


                    //Restore old path in MEM ini
                    if (!string.IsNullOrEmpty(oldValue))
                    {
                        ini.WriteValue("GameDataPath", "ME" + game, oldValue);
                    }

                    //cleanup
                    System.IO.DirectoryInfo di = new DirectoryInfo(basegameCookedDir);
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }

                    b.Result = new Tuple<CompactionResult, List<string>>(CompactionResult.RESULT_OK, FindReferencedTextures(dlcDir, false));
                    Process.Start(dlcDir);

                };
            }
            
            backgroundWorker.ProgressChanged += (a, b) =>
            {
                if (b.UserState is ThreadCommand tc)
                {
                    switch (tc.Command)
                    {
                        case CopyDir.UPDATE_PROGRESSBAR_VALUE:
                            ProgressBarValue = (int)tc.Data;
                            break;
                        case CopyDir.UPDATE_PROGRESSBAR_MAXVALUE:
                            ProgressBarMax = (int)tc.Data;
                            break;
                        case CopyDir.UPDATE_CURRENT_FILE_TEXT:
                            CurrentOperationText = $"Staging {(string)tc.Data}";
                            break;
                        case CopyDir.UPDATE_PROGRESSBAR_INDETERMINATE:
                            ProgressBarIndeterminate = (bool)tc.Data;
                            break;
                    }
                }
            };
            backgroundWorker.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is Tuple<CompactionResult, string> result)
                {
                    switch (result.Item1)
                    {
                        
                        case CompactionResult.RESULT_ERROR_TFC_NOT_FOUND:
                            CurrentOperationText = "TFC not found during scan: " + result.Item2;
                            MessageBox.Show(result.Item2);
                            break;
                    }
                }
                else if (b.Result is Tuple<CompactionResult, List<string>> listresult)
                {
                    switch (listresult.Item1)
                    {
                        case CompactionResult.RESULT_OK:
                            //nothing
                            CurrentOperationText = "Compaction completed";
                            new ListDialog(listresult.Item2, "Compaction result", "The compacted DLC now depends on the following TFCs:", this).Show();
                            break;
                        case CompactionResult.RESULT_SCAN_ERRORS:
                            CurrentOperationText = "Error occured(s) during texture scan";
                            new ListDialog(listresult.Item2, "Error(s) occured during texture scan", "The following errors occurred during the texture scan and must be fixed before compaction can proceed.", this).Show();
                            break;
                    }
                }
                OnPropertyChanged(nameof(IsNotBusy));
                CommandManager.InvalidateRequerySuggested();
            };
            backgroundWorker.RunWorkerAsync();
            OnPropertyChanged(nameof(IsNotBusy));
        }

        private enum CompactionResult
        {
            RESULT_OK,
            RESULT_ERROR_TFC_NOT_FOUND,
            RESULT_SCAN_ERRORS
        }

        private bool GameIsSelected() => SelectedGame != null && SelectedGame.IsBrowseForCustom == false;
        private bool DLCModFolderIsSelected() => GameIsSelected() && SelectedDLCModFolder != null;

        public class GameWrapper : NotifyPropertyChangedBase
        {
            public MEGame Game;
            private string _displayName;
            public string DisplayName
            {
                get => _displayName;
                set => SetProperty(ref _displayName, value);
            }
            public string DLCPath;
            public bool IsBrowseForCustom;
            public bool IsCustomPath;

            public GameWrapper(MEGame game, string displayName, string dlcPath)
            {
                Game = game;
                DisplayName = displayName;
                DLCPath = dlcPath;
            }
        }

        private void DLCModComboBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                ScanForGameCompleted = false;
                SelectedDLCModFolder = (string)e.AddedItems[0];
            }
            else
            {
                ScanForGameCompleted = false;
                SelectedDLCModFolder = null;
            }
        }


        /// <summary>
        /// Depth-first recursive delete, with handling for descendant 
        /// directories open in Windows Explorer.
        /// </summary>
        public static void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }

        private void GameComboBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                ScanForGameCompleted = false;
                var newItem = (GameWrapper)e.AddedItems[0];
                if (newItem.IsBrowseForCustom)
                {
                    // Browse
                    OpenFileDialog ofd = new OpenFileDialog();
                    ofd.Title = $"Select game executable";
                    string filter = $"ME2/ME3 executable|MassEffect2.exe;MassEffect3.exe";
                    ofd.Filter = filter;
                    if (ofd.ShowDialog() == true)
                    {
                        MEGame gameSelected = Path.GetFileName(ofd.FileName).Equals("MassEffect3.exe", StringComparison.InvariantCultureIgnoreCase) ? MEGame.ME3 : MEGame.ME2;

                        string result = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                        if (gameSelected == MEGame.ME3)
                            result = Path.GetDirectoryName(result); //up one more because of win32 directory.
                        string displayPath = result;
                        result = Path.Combine(result, @"BioGame\DLC");

                        if (Directory.Exists(result))
                        {
                            newItem.Game = gameSelected;
                            newItem.DisplayName = displayPath;
                            newItem.DLCPath = result;
                            newItem.IsCustomPath = true;
                            newItem.IsBrowseForCustom = false;
                            GameList.RemoveAll(x => (x.IsBrowseForCustom || x.IsCustomPath) && x != newItem);
                            GameList.Add(new GameWrapper(MEGame.Unknown, "Select game...", null) { IsBrowseForCustom = true, IsCustomPath = true });
                            SelectedGame = newItem;
                            var officialDLC = gameSelected == MEGame.ME3 ? ME3Directory.OfficialDLC : ME2Directory.OfficialDLC;
                            var DLC = MELoadedFiles.GetEnabledDLCFiles(gameSelected, result).Select(x => Path.GetFileName(x)).Where(x => !officialDLC.Contains(x));
                            CustomDLCFolderList.ReplaceAll(DLC);
                        }
                    }
                }
                else
                {
                    ScanForGameCompleted = false;
                    SelectedGame = newItem;
                    var officialDLC = newItem.Game == MEGame.ME3 ? ME3Directory.OfficialDLC : ME2Directory.OfficialDLC;
                    var DLC = MELoadedFiles.GetEnabledDLCFiles(newItem.Game, newItem.DLCPath).Select(x => Path.GetFileName(x)).Where(x => !officialDLC.Contains(x));
                    CustomDLCFolderList.ReplaceAll(DLC);
                    movieScan = newItem.DisplayName == "Mass Effect 3 (Movies)";
                }
            }
        }

        public class TFCSelector : IComparable
        {
            public TFCSelector(string tfcname, bool selected)
            {
                TFCName = tfcname;
                Selected = selected;
                Enabled = selected; //if not selected by deafult then then is disabled.
            }

            public string TFCName { get; set; }
            public bool Selected { get; set; }
            public bool Enabled { get; set; }

            public int CompareTo(object other)
            {
                if (other is TFCSelector t)
                {
                    return TFCName.CompareTo(t.TFCName);
                }
                return 1;
            }

            public override bool Equals(object other)
            {
                if (other is TFCSelector t)
                {
                    return t.TFCName == TFCName;
                }
                return false;
            }
        }

        private void ExtractBikToFile(ExportEntry export, string destination)
        {
            MemoryStream bikMovie = new MemoryStream();
            var binary = export.GetBinaryData();
            int length = BitConverter.ToInt32(binary, 4);
            int offset = BitConverter.ToInt32(binary, 12);
            var tfcprop = export.GetProperty<NameProperty>("TextureFileCacheName");
            string tfcname = $"{tfcprop.Value}.tfc";
            string filePath = Path.Combine(StagingDirectory, tfcname);

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                fs.Seek((long)offset, SeekOrigin.Begin);
                int bikend = offset + length;

                if (bikend > fs.Length)
                    throw new Exception("tfc corrupt");

                byte[] bikBytes = fs.ReadBytes(length);
                bikMovie = new MemoryStream(bikBytes);
            }

            bikMovie.Seek(0, SeekOrigin.Begin);
            var bikarray = bikMovie.ToArray();
            using (FileStream fs = new FileStream(destination, FileMode.Create))
            {
                fs.WriteBytes(bikarray);
            }
        }

        private (int, int) ImportBiktoCache(string bikfile, string tfcPath) //Returns length, offset
        {
            MemoryStream bikMovie = new MemoryStream();
            using (FileStream fs = new FileStream(bikfile, FileMode.OpenOrCreate, FileAccess.Read))
            {
                fs.CopyTo(bikMovie);
            }
            bikMovie.Seek(0, SeekOrigin.Begin);

            var bikarray = bikMovie.ToArray();
            int biklength = (int)bikarray.Length;
            int bikoffset = 0;
            Guid tfcGuid = Guid.NewGuid();
            using (FileStream fs = new FileStream(tfcPath, FileMode.Open, FileAccess.ReadWrite))
            {
                tfcGuid = fs.ReadGuid();
                fs.Seek(0, SeekOrigin.End);
                bikoffset = (int)fs.Position;
                fs.Write(bikarray, 0, biklength);
            }

            return (biklength, bikoffset);
        }
    }
}
