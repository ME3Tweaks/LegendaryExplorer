using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocumentFormat.OpenXml.Wordprocessing;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Textures.Studio;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Settings = LegendaryExplorer.Misc.AppSettings.Settings;

namespace LegendaryExplorer.Tools.TFCCompactor
{
    /// <summary>
    /// Interaction logic for TFCCompactor.xaml
    /// </summary>
    public partial class TFCCompactorWindow : TrackingNotifyPropertyChangedWindowBase
    {
        private BackgroundWorker backgroundWorker;

        public TFCCompactorWindow() : base("TFC Compactor", true)
        {
            LoadCommands();
            BusyProgressBarMax = 100;
            InitializeComponent();

            CurrentOperationText = "Select a directory to compact textures for";
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

        private string _stagingDirectory = Settings.TFCCompactor_LastStagingPath;
        public string StagingDirectory
        {
            get => _stagingDirectory;
            set => SetProperty(ref _stagingDirectory, value);
        }

        public bool IsNotBusy => backgroundWorker == null || !backgroundWorker.IsBusy;

        private string BaseFolder;

        public ObservableCollectionExtended<GameWrapper> GameList { get; } = new();
        public ObservableCollectionExtended<string> CustomDLCFolderList { get; } = new();
        public ObservableCollectionExtended<TFCSelector> TextureCachesToPullFromList { get; } = new();

        public ICommand CompactTFCCommand { get; set; }
        public ICommand ScanCommand { get; set; }
        public ICommand ChangeStagingDirCommand { get; set; }
        public ICommand BrowseCommand { get; set; }

        private void LoadCommands()
        {
            CompactTFCCommand = new GenericCommand(BeginTFCCompaction, () => ScanForGameCompleted && IsNotBusy && !string.IsNullOrEmpty(StagingDirectory) && Directory.Exists(StagingDirectory));
            ScanCommand = new GenericCommand(BeginReferencedTFCScan, () => IsNotBusy);
            ChangeStagingDirCommand = new GenericCommand(ChangeStagingDir, () => IsNotBusy);
        }

        private void ChangeStagingDir()
        {
            var openFolder = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select staging directory",
                AllowNonFileSystemItems = false,
                EnsurePathExists = true
            };

            if (openFolder.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }
            var dir = openFolder.FileName;
            if (!Directory.Exists(dir))
            {
                MessageBox.Show("The selected destination directory does not exist: " + dir, "Directory does not exist");
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

        //public void EnsureCriticalFiles(object sender, DoWorkEventArgs args)
        //{
        //    void progressCallback(long bytesDownloaded, long bytesToDownload)
        //    {
        //        BusyProgressBarMax = (int)bytesToDownload;
        //        BusyProgressBarValue = (int)bytesDownloaded;
        //    }

        //    try
        //    {
        //        string staticExecutablesDirectory = Directory.CreateDirectory(Path.Combine(AppDirectories.AppDataFolder, "staticexecutables")).FullName;


        //        string memCS = Path.Combine(staticExecutablesDirectory, "MassEffectModderNoGuiCS.exe");
        //        if (!File.Exists(memCS))
        //        {
        //            BusyText = "Downloading MEM command line (C#)";
        //            var downloadError = OnlineContent.EnsureStaticExecutable("MassEffectModderNoGuiCS.exe", progressCallback);
        //            if (downloadError != null)
        //            {
        //                args.Result = downloadError;
        //                return;
        //            }
        //        }


        //        string memQT = Path.Combine(staticExecutablesDirectory, "MassEffectModderNoGuiQT.exe");
        //        if (!File.Exists(memQT))
        //        {
        //            BusyText = "Downloading MEM command line (qT)";
        //            var downloadError = OnlineContent.EnsureStaticExecutable("MassEffectModderNoGuiQT.exe", progressCallback);
        //            if (downloadError != null)
        //            {
        //                args.Result = downloadError;
        //                return;
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        args.Result = "Error downloading required files:\n" + e.FlattenException();
        //    }
        //}

        private void BeginReferencedTFCScan()
        {
            var dlg = new CommonOpenFileDialog("Select base folder to compact") { IsFolderPicker = true };
            if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok) { return; }
            BaseFolder = dlg.FileName;
            var dirName = Path.GetFileName(BaseFolder);
            if (dirName.StartsWith("DLC_MOD_"))
            {
                DLCName = dirName;
            }
            else
            {
                var dirs = Directory.GetDirectories(BaseFolder);
                var candidates = dirs.Where(x => Path.GetFileName(x).StartsWith("DLC_MOD_")).ToList();
                if (candidates.Count == 1 && string.IsNullOrWhiteSpace(DLCName))
                {
                    DLCName = Path.GetFileName(candidates[0]);
                }
            }

            backgroundWorker = new BackgroundWorker { WorkerReportsProgress = true };

            backgroundWorker.DoWork += (a, b) =>
            {
                FindReferencedTextures();
            };
            backgroundWorker.RunWorkerCompleted += (a, b) =>
            {
                ProgressBarIndeterminate = false;
                ScanForGameCompleted = true;
                backgroundWorker = null;
                CurrentOperationText = "Initial scan completed";
                OnPropertyChanged(nameof(IsNotBusy));
                CommandManager.InvalidateRequerySuggested();
            };
            backgroundWorker.RunWorkerAsync();
            OnPropertyChanged(nameof(IsNotBusy));
        }

        void scanProgress(string text, int done, int total)
        {
            if (text != null)
            {
                CurrentOperationText = text;
            }
            if (total > 0)
            {
                ProgressBarIndeterminate = false;
                ProgressBarValue = done;
                ProgressBarMax = total;
            }
            else if (done == -1 && total == -1)
            {
                ProgressBarIndeterminate = true;
            }
        }

        private List<string> FindReferencedTextures()
        {
            CurrentOperationText = "Getting list of files";
            ProgressBarValue = 0;
            ProgressBarIndeterminate = true;

            //string[] files = Directory.GetFiles(dlcDir, "*.pcc", SearchOption.AllDirectories);
            //ProgressBarMax = files.Length;
            //ProgressBarIndeterminate = false;
            SortedSet<TFCSelector> referencedTFCs = new();

            var rootNodes = new List<TextureMapMemoryEntry>();
            void addRootNode(TextureMapMemoryEntry x) => rootNodes.Add(x);
            TextureMapMemoryEntry generateNode(IEntry x) => new(x);



            LoadedTextureMap = TextureMapGenerator.GenerateMapForFolder(BaseFolder, generateNode, addRootNode, scanProgress);

            foreach (var tl in rootNodes)
            {
                foreach (var tex in tl.GetAllTextureEntries().Where(x => x.Instances.Any()))
                {
                    if (tex.Instances[0].TFCName != null)
                        referencedTFCs.Add(new TFCSelector(tex.Instances[0].TFCName, !MEDirectories.BasegameTFCs(LoadedTextureMap.Game).Contains(tex.Instances[0].TFCName)));
                    else
                        Debug.WriteLine($"No TFC name, compressed mips: {tex.Instances[0].CompressedMipInfos.Count}");
                }
            }
            //if (Enumerable.Any(files))
            //{
            //    foreach (string file in files)
            //    {
            //        CurrentOperationText = $"Scanning {Path.GetFileName(file)}...";
            //        using (var package = MEPackageHandler.OpenMEPackage(file))
            //        {
            //            if (movieScan)
            //            {
            //                var movieExports = package.Exports.Where(x => x.ClassName == "TextureMovie");
            //                foreach (var movietexture in movieExports)
            //                {
            //                    if (movietexture.GetProperty<NameProperty>("TextureFileCacheName") is NameProperty tfcNameProperty)
            //                    {
            //                        string tfcname = tfcNameProperty.Value;
            //                        if (referencedTFCs.Add(new TFCSelector(tfcname, forSelecting)))
            //                        {
            //                            if (forSelecting)
            //                            {
            //                                Application.Current.Dispatcher.Invoke(delegate
            //                                {
            //                                    TextureCachesToPullFromList.ReplaceAll(referencedTFCs);
            //                                });
            //                            }
            //                        }
            //                    }
            //                }
            //            }
            //            else
            //            {
            //                var textureExports = package.Exports.Where(x => x.IsTexture());
            //                foreach (var texture in textureExports)
            //                {
            //                    if (texture.GetProperty<NameProperty>("TextureFileCacheName") is NameProperty tfcNameProperty)
            //                    {
            //                        string tfcname = tfcNameProperty.Value;
            //                        //if (tfcname == "CustTextures0")
            //                        //{
            //                        //    Debug.WriteLine($"CustTextures0 TFC Reference: {texture.FullPath} {texture.UIndex} in {texture.FileRef.FilePath}");
            //                        //}
            //                        if (!MEDirectories.BasegameTFCs(SelectedGame.Game).Contains(tfcname))
            //                        {
            //                            //Check that external mips are referenced.
            //                            //some texture2d have a tfc but don't have any external mips for some reason
            //                            Texture2D texture2d = new(texture);
            //                            var topmip = texture2d.GetTopMip();
            //                            if (topmip.storageType is StorageTypes.extLZO or StorageTypes.extZlib or StorageTypes.extOodle or StorageTypes.extUnc)
            //                            {
            //                                if (referencedTFCs.Add(new TFCSelector(tfcname, forSelecting)))
            //                                {
            //                                    //Debug.WriteLine($"Reference to {tfcname} in {Path.GetFileName(texture.FileRef.FilePath)} {texture.UIndex} {texture.InstancedFullPath}");
            //                                    if (forSelecting)
            //                                    {
            //                                        Application.Current.Dispatcher.Invoke(delegate
            //                                        {
            //                                            TextureCachesToPullFromList.ReplaceAll(referencedTFCs);
            //                                        });
            //                                    }
            //                                }
            //                            }
            //                            else
            //                            {
            //                                //Debug.WriteLine($"Skipping Reference, no external mips defined: {texture.GetFullPath} {texture.UIndex} in {texture.FileRef.FilePath}");
            //                            }
            //                        }
            //                    }
            //                }
            //            }
            //        }
            //        ProgressBarValue++;
            //    }
            //}
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextureCachesToPullFromList.ReplaceAll(referencedTFCs);
            });
            return referencedTFCs.Select(x => x.TFCName).ToList();
        }

        public TextureMap LoadedTextureMap { get; set; }

        private void BeginTFCCompaction()
        {
            if (string.IsNullOrWhiteSpace(DLCName) || !DLCName.StartsWith("DLC_MOD_"))
            {
                MessageBox.Show("DLC name must begin with DLC_MOD_. Mods not following this naming convention are not supported.", "Invalid DLC name", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var tfcsToPull = TextureCachesToPullFromList.Where(x => x.Selected).Select(x => x.TFCName).ToList();
            if (!tfcsToPull.Any())
            {
                MessageBox.Show("You must select at one or more referenced TFCs to pull into the new TFC.", "No TFCs selected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            TFCCompactorInfoPackage pack = new()
            {
                TFCType = "Textures",
                StagingPath = StagingDirectory,
                BaseCompactionPath = BaseFolder,
                Game = LoadedTextureMap.Game,
                GamePath = MEDirectories.GetDefaultGamePath(LoadedTextureMap.Game),
                UseIndexing = LoadedTextureMap.Game == MEGame.LE2, //.IsLEGame(),
                DLCName = DLCName,
                TFCsToCompact = tfcsToPull
            };

            void errorCallback(string error)
            {
                MessageBox.Show(error);
            }
            backgroundWorker = new BackgroundWorker { WorkerReportsProgress = true };
            backgroundWorker.DoWork += (sender, args) =>
            {
                CurrentOperationText = "Compacting...";
                LegendaryExplorerCore.Textures.TFCCompactor.CompactTFC(pack, errorCallback, scanProgress, LoadedTextureMap);
            };



            //if (movieScan)
            //{
            //    backgroundWorker.DoWork += (a, b) =>
            //    {
            //        if (Directory.EnumerateFileSystemEntries(StagingDirectory).Any())
            //        {
            //            DeleteDirectory(StagingDirectory);
            //            Directory.CreateDirectory(StagingDirectory);
            //        }
            //        CurrentOperationText = "Creating compaction workspace";
            //        ProgressBarValue = 0;
            //        ProgressBarMax = 100;
            //        ProgressBarIndeterminate = true;

            //        List<string> TFCsToPullFrom = TextureCachesToPullFromList.Where(x => x.Selected).Select(x => x.TFCName).ToList();
            //        var ExportsToBeReplaced = new List<(string fileName, int uIndex, string moviecache_offset)>();
            //        var MoviesToBeReplaced = new Dictionary<string, int>(); //moviecache_offset, offset
            //        var MoviesWrittenToNewTFC = new Dictionary<string, (int newLength, int newOffset)>(); //moviecache_offset, new length, new offset
            //        //Copy basegame TFCs to staged
            //        string[] tfclist = Directory.GetFiles(sourceGamePath, "*.tfc", SearchOption.AllDirectories);
            //        foreach (var tfc in TextureCachesToPullFromList.Where(x => x.Selected))
            //        {
            //            string otfcPath = tfclist.FirstOrDefault(t => Path.GetFileNameWithoutExtension(t) == tfc.TFCName);
            //            File.Copy(otfcPath, Path.Combine(StagingDirectory, Path.GetFileName(otfcPath)));
            //        }
            //        //Find all movie references
            //        //Search all files in mod directory for texturemovie references to selected DLCs
            //        string[] files = Directory.GetFiles(Path.Combine(sourceGamePath, "DLC", BaseFolder), "*.pcc", SearchOption.AllDirectories);
            //        foreach (string file in files)
            //        {
            //            CurrentOperationText = $"Scanning and extracting {Path.GetFileName(file)}...";
            //            using IMEPackage package = MEPackageHandler.OpenMEPackage(file);
            //            var movieExports = package.Exports.Where(x => x.ClassName == "TextureMovie");
            //            foreach (var movietexture in movieExports)
            //            {
            //                if (movietexture.GetProperty<NameProperty>("TextureFileCacheName") is NameProperty tfcNameProperty)
            //                {
            //                    if (TFCsToPullFrom.Contains<string>(tfcNameProperty.Value))
            //                    {
            //                        var binary = movietexture.GetBinaryData<TextureMovie>();
            //                        int offset = binary.DataOffset;
            //                        string bikfile = $"{tfcNameProperty.Value}_{offset}";
            //                        ExportsToBeReplaced.Add((file, movietexture.UIndex, bikfile));
            //                        if (!MoviesToBeReplaced.ContainsKey(bikfile))
            //                        {
            //                            MoviesToBeReplaced.Add(bikfile, offset);
            //                            //Extract movie references
            //                            string destination = Path.Combine(StagingDirectory, $"{bikfile}.bik");
            //                            ExtractBikToFile(movietexture, destination);
            //                        }
            //                    }
            //                }
            //            }
            //        }

            //        //Compile new tfc
            //        Guid tfcGuid = Guid.NewGuid();
            //        var tfcName = "Movies_" + BaseFolder;
            //        var outputTFC = Path.Combine(StagingDirectory, $"{tfcName}.tfc");
            //        if (File.Exists(outputTFC))
            //        {
            //            File.Delete(outputTFC);
            //        }
            //        using (FileStream fs = new(outputTFC, FileMode.OpenOrCreate, FileAccess.Write))
            //        {
            //            fs.WriteGuid(tfcGuid);
            //            fs.Flush();
            //        }
            //        //Import biks to cache
            //        CurrentOperationText = "Compiling biks to cache...";
            //        foreach (var bik in MoviesToBeReplaced)
            //        {
            //            var addedmovie = ImportBiktoCache(Path.Combine(StagingDirectory, $"{bik.Key}.bik"), outputTFC);
            //            MoviesWrittenToNewTFC.Add(bik.Key, addedmovie);
            //        }
            //        //Replace references in files
            //        CurrentOperationText = "Replacing file references...";
            //        foreach ((string fileName, int uIndex, string moviecacheOffset) in ExportsToBeReplaced)
            //        {
            //            using IMEPackage package = MEPackageHandler.OpenMEPackage(fileName);
            //            var expTexMov = package.GetUExport(uIndex);
            //            (int newLength, int newOffset) = MoviesWrittenToNewTFC[moviecacheOffset];

            //            var props = expTexMov.GetProperties();
            //            props.AddOrReplaceProp(new NameProperty(tfcName, "TextureFileCacheName"));
            //            props.AddOrReplaceProp(tfcGuid.ToGuidStructProp("TFCFileGuid"));
            //            expTexMov.WritePropertiesAndBinary(props, new TextureMovie
            //            {
            //                IsExternal = true,
            //                DataSize = newLength,
            //                DataOffset = newOffset
            //            });

            //            package.Save();
            //        }

            //        //Copy tfc to ModFolder
            //        CurrentOperationText = $"Cleaning Up...";
            //        File.Copy(outputTFC, Path.Combine(sourceGamePath, "DLC", BaseFolder, "CookedPCConsole", Path.GetFileName(outputTFC)), true);

            //        //Clean Up
            //        DirectoryInfo di = new DirectoryInfo(StagingDirectory);
            //        foreach (FileInfo file in di.GetFiles())
            //        {
            //            file.Delete();
            //        }

            //        ProgressBarValue = 0;
            //        ProgressBarMax = 100;
            //        ProgressBarIndeterminate = false;
            //        var dependancylist = new List<string>() { tfcName };
            //        dependancylist.AddRange(TextureCachesToPullFromList.Where(x => !x.Selected).Select(x => x.TFCName).ToList());
            //        b.Result = (CompactionResult.RESULT_OK, dependancylist);
            //        return;
            //    };

            //}
            //else
            //{
            //    backgroundWorker.DoWork += (a, b) =>
            //    {
            //        if (Directory.EnumerateFileSystemEntries(StagingDirectory).Any())
            //        {
            //            DeleteDirectory(StagingDirectory);
            //            Directory.CreateDirectory(StagingDirectory);
            //        }
            //        CurrentOperationText = "Creating compaction workspace";
            //        ProgressBarValue = 0;
            //        ProgressBarMax = 100;
            //        ProgressBarIndeterminate = false;

            //        List<string> dlcTFCsToPullFrom = TextureCachesToPullFromList.Where(x => x.Selected).Select(x => x.TFCName).ToList();
            //        var tfcsToStage = TextureCachesToPullFromList.Select(x => x.TFCName);
            //        //Create workspace for MEM
            //        var game = (int)SelectedGame.Game;

            //        //Create fake game directory
            //        Directory.CreateDirectory(workingGamePath);
            //        Directory.CreateDirectory(Path.Combine(workingGamePath, "Binaries"));
            //        if (game == 3)
            //        {
            //            Directory.CreateDirectory(Path.Combine(workingGamePath, "Binaries", "win32"));
            //            File.Create(Path.Combine(workingGamePath, "Binaries", "win32", "MassEffect3.exe")).Close();
            //        }
            //        else
            //        {
            //            //ME2
            //            File.Create(Path.Combine(workingGamePath, "Binaries", "MassEffect2.exe")).Close();
            //        }

            //        string cookedDirName = game == 2 ? "CookedPC" : "CookedPCConsole";
            //        Directory.CreateDirectory(Path.Combine(workingGamePath, "BioGame"));
            //        var dlcDir = Path.Combine(workingGamePath, "BioGame", "DLC");
            //        Directory.CreateDirectory(dlcDir);
            //        var basegameCookedDir = Path.Combine(workingGamePath, "BioGame", cookedDirName);
            //        Directory.CreateDirectory(basegameCookedDir);

            //        //Copy basegame TFCs to cookedDiretory
            //        var basegameDirToCopyFrom = MEDirectories.GetCookedPath(SelectedGame.Game);
            //        var tfcs = Directory.GetFiles(basegameDirToCopyFrom, "*.tfc").ToList();
            //        var currentgamefiles = MELoadedFiles.GetFilesLoadedInGame(SelectedGame.Game, forceReload: true, includeTFCs: true);
            //        //var debug = currentgamefiles.Where(x => x.Value.Contains(".tfc")).ToList();
            //        //debug.ForEach(x => Debug.WriteLine(x));
            //        foreach (var tfc in tfcsToStage)
            //        {
            //            var fullname = tfc.EndsWith(".tfc") ? tfc : tfc + ".tfc";
            //            if (currentgamefiles.TryGetValue(fullname, out string fullpath))
            //            {
            //                tfcs.Add(fullpath);
            //            }
            //            else if (SelectedGame.Game == MEGame.ME3)
            //            {
            //                //Attempt SFAR lookup at later stage below. Will abort if we cannot find TFC.
            //                tfcs.Add(tfc); //no suffix
            //            }
            //            else
            //            {
            //                b.Result = (CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
            //                return;
            //            }
            //        }

            //        ProgressBarIndeterminate = true;
            //        foreach (var tfc in tfcs)
            //        {
            //            string tfcShortName = Path.GetFileNameWithoutExtension(tfc);
            //            CurrentOperationText = $"Staging {tfcShortName}";
            //            string destPath = Path.Combine(basegameCookedDir, tfcShortName + ".tfc");
            //            if (File.Exists(tfc))
            //            {
            //                File.Copy(tfc, destPath, true);
            //            }
            //            else if (SelectedGame.Game == MEGame.ME3)
            //            {
            //                if (tfcShortName.StartsWith("Textures_DLC_MOD_"))
            //                {
            //                    string dlcFolderName = tfc[9..]; // skip Textures_
            //                    var sfar = Path.Combine(MEDirectories.GetDLCPath(SelectedGame.Game), dlcFolderName, "CookedPCConsole", "Default.sfar");
            //                    if (File.Exists(sfar) && new FileInfo(sfar).Length > 32)
            //                    {
            //                        //sfar exists and is not fully unpacked (with mem style 32 byte sfar)
            //                        DLCPackage p = new DLCPackage(sfar);
            //                        var tfcIndex = p.FindFileEntry(tfcShortName + ".tfc");
            //                        if (tfcIndex >= 0)
            //                        {
            //                            var tfcMemory = p.DecompressEntry(tfcIndex);
            //                            File.WriteAllBytes(destPath, tfcMemory.ToArray());
            //                            tfcMemory.Close();
            //                        }
            //                        else
            //                        {
            //                            //Can't find TFC!
            //                            b.Result = (CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
            //                            return;
            //                        }
            //                    }
            //                    else
            //                    {
            //                        //Can't find TFC!

            //                        //Can we try ME3Tweaks backup?

            //                        string me3BackupPath = ME3TweaksBackups.GetGameBackupPath(MEGame.ME3);
            //                        if (me3BackupPath != null && Directory.Exists(me3BackupPath))
            //                        {
            //                            sfar = Path.Combine(me3BackupPath, "BioGame", "DLC", dlcFolderName, "CookedPCConsole", "Default.sfar");
            //                            if (File.Exists(sfar) && new FileInfo(sfar).Length > 32)
            //                            {
            //                                //sfar exists and is not fully unpacked (with mem style 32 byte sfar)
            //                                var p = new DLCPackage(sfar);
            //                                var tfcIndex = p.FindFileEntry(tfcShortName + ".tfc");
            //                                if (tfcIndex >= 0)
            //                                {
            //                                    var tfcMemory = p.DecompressEntry(tfcIndex);
            //                                    File.WriteAllBytes(destPath, tfcMemory.ToArray());
            //                                    tfcMemory.Close();
            //                                    continue; //OK
            //                                }
            //                                else
            //                                {
            //                                    //Can't find TFC!
            //                                    b.Result = (CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory OR backup path: {tfc}");
            //                                    return;
            //                                }
            //                            }
            //                        }

            //                        b.Result = (CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
            //                        return;
            //                    }
            //                }
            //                else
            //                {
            //                    //Can't find TFC!
            //                    b.Result = (CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
            //                    return;
            //                }
            //            }
            //            else
            //            {
            //                //Can't find TFC!
            //                if (dlcTFCsToPullFrom.Contains(tfc))
            //                {
            //                    b.Result = (CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for pulling textures from in game directory: {tfc}");
            //                }
            //                else
            //                {
            //                    b.Result = (CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for ensuring clean texture scan: {tfc}");
            //                }
            //                return;
            //            }
            //        }

            //        //Copy DLC
            //        var destDLCDir = Path.Combine(dlcDir, BaseFolder);
            //        var sourceDLCDir = Path.Combine(SelectedGame.DLCPath, BaseFolder);
            //        CopyDir.CopyAll_ProgressBar(new DirectoryInfo(sourceDLCDir), Directory.CreateDirectory(destDLCDir), backgroundWorker, ignoredExtensions: new[] { ".tfc" });
            //        // get MassEffectModder.ini
            //        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            //            "MassEffectModder");
            //        string _iniPath = Path.Combine(path, "MassEffectModder.ini");
            //        if (!Directory.Exists(path))
            //        {
            //            Directory.CreateDirectory(path);
            //        }

            //        DuplicatingIni ini = DuplicatingIni.LoadIni(_iniPath);
            //        var oldValue = ini["GameDataPath"]["ME" + game].Value;
            //        ini["GameDataPath"].SetSingleEntry("ME" + game, workingGamePath);
            //        ini.WriteToFile(_iniPath);

            //        //Scan game
            //        ProgressBarMax = 100;
            //        ProgressBarValue = 0;
            //        //List<string> missingTFCs = new List<string>();
            //        var errors = new List<string>();
            //        var triggers = new Dictionary<string, Action<string>>
            //        {
            //            {"TASK_PROGRESS", s => ProgressBarValue = int.Parse(s)},
            //            {"PROCESSING_FILE", s => CurrentOperationText = $"Building texture map for {Path.GetFileName(s)}"},
            //            {"ERROR", s => errors.Add(s)}
            //        };

            //        string args = $"--scan --gameid {game} --ipc";
            //        var memProcess = MassEffectModderIPCWrapper.RunMEM(args, triggers);
            //        while (memProcess.State == AppState.Running)
            //        {
            //            Thread.Sleep(100); //this is kind of hacky but it works
            //        }

            //        if (errors.Count > 0)
            //        {
            //            //Do something...
            //            b.Result = (CompactionResult.RESULT_SCAN_ERRORS, errors);
            //            return;
            //        }

            //        //Extract textures
            //        var tempTextureCache = Directory.CreateDirectory(Path.Combine(workingGamePath, "TextureStaging")).FullName;
            //        var singlepasscount = Directory.GetFiles(destDLCDir, "*.pcc", SearchOption.AllDirectories).Length;
            //        var totalpasses = singlepasscount * dlcTFCsToPullFrom.Count;
            //        int previousFullStepsDone = 0;
            //        ProgressBarValue = 0;
            //        ProgressBarMax = totalpasses;
            //        foreach (var tfcname in dlcTFCsToPullFrom)
            //        {
            //            CurrentOperationText = $"Extracting referenced textures from {tfcname}";
            //            triggers = new Dictionary<string, Action<string>> {
            //            { "Package", s =>
            //                {
            //                    int done = int.Parse(s.Substring(0,s.IndexOf('/')));
            //                    ProgressBarValue = previousFullStepsDone + done;
            //                }
            //            }
            //        };

            //            args = $"--extract-all-dds --gameid {game} --output \"{tempTextureCache}\" --tfc-name {tfcname}";
            //            memProcess = MassEffectModderIPCWrapper.RunMEM(args, null, triggers); //this command does not support IPC commands
            //            while (memProcess.State == AppState.Running)
            //            {
            //                Thread.Sleep(100); //this is kind of hacky but it works
            //            }

            //            previousFullStepsDone += singlepasscount;
            //        }

            //        //Install new textures
            //        string newTextureCacheName = "Textures_" + BaseFolder;
            //        CurrentOperationText = $"Building " + newTextureCacheName;
            //        ProgressBarValue = 0;
            //        ProgressBarMax = Directory.GetFiles(tempTextureCache, "*.dds").Length;
            //        triggers = new Dictionary<string, Action<string>> {
            //        { "Installing", s =>
            //            {
            //                string remainingStr = s[5..]; //cut off "mod: "

            //                int done = int.Parse(remainingStr.Substring(0,remainingStr.IndexOf(' ')));
            //                ProgressBarValue = done;
            //                CurrentOperationText = $"Building {newTextureCacheName} | {remainingStr[remainingStr.LastIndexOf(' ')..].Trim()}";
            //            }
            //        }
            //    };

            //        args = $"-dlc-mod-for-mgamerz {game} \"{tempTextureCache}\" {newTextureCacheName}";
            //        memProcess = MassEffectModderIPCWrapper.RunMEM(args, null, triggers, true); //this command does not support IPC commands
            //        while (memProcess.State == AppState.Running)
            //        {
            //            Thread.Sleep(100); //this is kind of hacky but it works
            //        }


            //        //Restore old path in MEM ini
            //        if (!string.IsNullOrEmpty(oldValue))
            //        {
            //            ini["GameDataPath"].SetSingleEntry("ME" + game, oldValue);
            //            ini.WriteToFile(_iniPath);
            //        }

            //        //cleanup
            //        DirectoryInfo di = new DirectoryInfo(basegameCookedDir);
            //        foreach (FileInfo file in di.GetFiles())
            //        {
            //            file.Delete();
            //        }

            //        b.Result = (CompactionResult.RESULT_OK, FindReferencedTextures());
            //        Process.Start(dlcDir);

            //    };
            //}

            //backgroundWorker.ProgressChanged += (a, b) =>
            //{
            //    if (b.UserState is ThreadCommand tc)
            //    {
            //        switch (tc.Command)
            //        {
            //            case CopyDir.UPDATE_PROGRESSBAR_VALUE:
            //                ProgressBarValue = (int)tc.Data;
            //                break;
            //            case CopyDir.UPDATE_PROGRESSBAR_MAXVALUE:
            //                ProgressBarMax = (int)tc.Data;
            //                break;
            //            case CopyDir.UPDATE_CURRENT_FILE_TEXT:
            //                CurrentOperationText = $"Staging {(string)tc.Data}";
            //                break;
            //            case CopyDir.UPDATE_PROGRESSBAR_INDETERMINATE:
            //                ProgressBarIndeterminate = (bool)tc.Data;
            //                break;
            //        }
            //    }
            //};
            backgroundWorker.RunWorkerCompleted += (a, b) =>
            {
                ProgressBarIndeterminate = false;
                Settings.TFCCompactor_LastStagingPath = StagingDirectory;
                
                // These results will never be hit. We need to handle errors somehow. Like if mismatched CRCs are found.
                if (b.Result is ValueTuple<CompactionResult, string> result)
                {
                    switch (result.Item1)
                    {
                        case CompactionResult.RESULT_ERROR_TFC_NOT_FOUND:
                            CurrentOperationText = "TFC not found during scan: " + result.Item2;
                            MessageBox.Show(result.Item2);
                            break;
                    }
                }
                else if (b.Result is ValueTuple<CompactionResult, List<string>> listresult)
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

                CurrentOperationText = "TFC Compaction Complete";
                Debug.WriteLine("COMPACT DONE");
                OnPropertyChanged(nameof(IsNotBusy));
                CommandManager.InvalidateRequerySuggested();
            };
            backgroundWorker.RunWorkerAsync();
            OnPropertyChanged(nameof(IsNotBusy));
        }

        private string _dlcName;
        public string DLCName { get => _dlcName; set => SetProperty(ref _dlcName, value); }

        private enum CompactionResult
        {
            RESULT_OK,
            RESULT_ERROR_TFC_NOT_FOUND,
            RESULT_SCAN_ERRORS
        }

        //private bool GameIsSelected() => SelectedGame != null && SelectedGame.IsBrowseForCustom == false;
        //private bool DLCModFolderIsSelected() => GameIsSelected() && BaseFolder != null;

        public class GameWrapper : NotifyPropertyChangedBase
        {
            public MEGame Game;
            private string _displayName;
            public string DisplayName
            {
                get => _displayName;
                set => SetProperty(ref _displayName, value);
            }

            public string RootPath;
            public string DLCPath;
            public bool IsBrowseForCustom;
            public bool IsCustomPath;

            public GameWrapper(MEGame game, string displayName, string rootPath)
            {
                Game = game;
                DisplayName = displayName;
                if (game != MEGame.Unknown)
                {
                    DLCPath = MEDirectories.GetDLCPath(game, rootPath);
                    RootPath = rootPath;
                }
            }
        }

        private void DLCModComboBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                ScanForGameCompleted = false;
                BaseFolder = (string)e.AddedItems[0];
            }
            else
            {
                ScanForGameCompleted = false;
                BaseFolder = null;
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

        //private void GameComboBox_Changed(object sender, SelectionChangedEventArgs e)
        //{
        //    if (e.AddedItems.Count > 0)
        //    {
        //        ScanForGameCompleted = false;
        //        var newItem = (GameWrapper)e.AddedItems[0];
        //        if (newItem.IsBrowseForCustom)
        //        {
        //            // Browse
        //            OpenFileDialog ofd = new()
        //            {
        //                Title = "Select game executable",
        //                Filter = "ME2/ME3 executable|MassEffect2.exe;MassEffect3.exe"
        //            };
        //            if (ofd.ShowDialog() == true)
        //            {
        //                MEGame gameSelected = Path.GetFileName(ofd.FileName).Equals("MassEffect3.exe", StringComparison.InvariantCultureIgnoreCase) ? MEGame.ME3 : MEGame.ME2;

        //                string result = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

        //                if (gameSelected == MEGame.ME3)
        //                    result = Path.GetDirectoryName(result); //up one more because of win32 directory.
        //                var root = result;
        //                string displayPath = result;
        //                result = Path.Combine(result, @"BioGame\DLC");

        //                if (Directory.Exists(result))
        //                {
        //                    newItem.Game = gameSelected;
        //                    newItem.DisplayName = displayPath;
        //                    newItem.RootPath = root;
        //                    newItem.DLCPath = result;
        //                    newItem.IsCustomPath = true;
        //                    newItem.IsBrowseForCustom = false;
        //                    GameList.RemoveAll(x => (x.IsBrowseForCustom || x.IsCustomPath) && x != newItem);
        //                    GameList.Add(new GameWrapper(MEGame.Unknown, "Select game...", null) { IsBrowseForCustom = true, IsCustomPath = true });
        //                    SelectedGame = newItem;
        //                    var officialDLC = gameSelected == MEGame.ME3 ? ME3Directory.OfficialDLC : ME2Directory.OfficialDLC;
        //                    var DLC = MELoadedFiles.GetEnabledDLCFolders(gameSelected, result).Select(Path.GetFileName).Where(x => !officialDLC.Contains(x));
        //                    CustomDLCFolderList.ReplaceAll(DLC);
        //                }
        //            }
        //        }
        //        else
        //        {
        //            ScanForGameCompleted = false;
        //            SelectedGame = newItem;
        //            var officialDLC = newItem.Game == MEGame.ME3 ? ME3Directory.OfficialDLC : ME2Directory.OfficialDLC;
        //            var DLC = MELoadedFiles.GetEnabledDLCFolders(newItem.Game, newItem.RootPath).Select(Path.GetFileName).Where(x => !officialDLC.Contains(x));
        //            CustomDLCFolderList.ReplaceAll(DLC);
        //            movieScan = newItem.DisplayName == "Mass Effect 3 (Movies)";
        //        }
        //    }
        //}

        public class TFCSelector : IComparable
        {
            public TFCSelector(string tfcname, bool selected)
            {
                TFCName = tfcname;
                Selected = selected;
                Enabled = selected; //if not selected by deafult then then is disabled.
            }

            public string TFCName { get; }
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

            public override int GetHashCode()
            {
                return TFCName.GetHashCode();
            }
        }

        private void ExtractBikToFile(ExportEntry export, string destination)
        {
            MemoryStream bikMovie;
            var binary = export.GetBinaryData<TextureMovie>();
            int length = binary.DataSize;
            int offset = binary.DataOffset;
            var tfcprop = export.GetProperty<NameProperty>("TextureFileCacheName");
            string tfcname = $"{tfcprop.Value}.tfc";
            string filePath = Path.Combine(StagingDirectory, tfcname);

            using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read))
            {
                fs.Seek((long)offset, SeekOrigin.Begin);
                int bikend = offset + length;

                if (bikend > fs.Length)
                    throw new Exception("tfc corrupt");

                byte[] bikBytes = fs.ReadToBuffer(length);
                bikMovie = new MemoryStream(bikBytes);
            }

            bikMovie.Seek(0, SeekOrigin.Begin);
            byte[] bikarray = bikMovie.ToArray();
            using (FileStream fs = new(destination, FileMode.Create))
            {
                fs.WriteFromBuffer(bikarray);
            }
        }

        private static (int length, int offset) ImportBiktoCache(string bikfile, string tfcPath)
        {
            MemoryStream bikMovie = new();
            using (FileStream fs = new(bikfile, FileMode.OpenOrCreate, FileAccess.Read))
            {
                fs.CopyTo(bikMovie);
            }
            bikMovie.Seek(0, SeekOrigin.Begin);

            var bikarray = bikMovie.ToArray();
            int biklength = bikarray.Length;
            int bikoffset = 0;
            Guid tfcGuid = Guid.NewGuid();
            using (FileStream fs = new(tfcPath, FileMode.Open, FileAccess.ReadWrite))
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
