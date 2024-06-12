/**
 * Package Dumper is based on ME3Tweaks' Mass Effect 3 Mod Manager Command Line Tools
 * TransplanterLib. This is a modified version provided by Mgamerz
 * (c) Mgamerz 2019
 */

using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.WindowsAPICodePack.Taskbar;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.UnrealScript;

namespace LegendaryExplorer.Tools.PackageDumper
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PackageDumperWindow : TrackingNotifyPropertyChangedWindowBase
    {
        /// <summary>
        /// Items show in the list that are currently being processed
        /// </summary>
        public ObservableCollectionExtended<PackageDumperSingleFileTask> CurrentDumpingItems { get; set; } = new();

        public ObservableCollectionExtended<MEGame> SupportedGames { get; private set; } = new()
        {
            MEGame.ME1,
            MEGame.ME2,
            MEGame.ME3,
            MEGame.LE1,
            MEGame.LE2,
            MEGame.LE3
        };

        /// <summary>
        /// All items in the queue
        /// </summary>
        private List<PackageDumperSingleFileTask> AllDumpingItems;

        private int _listViewHeight;
        public int ListViewHeight
        {
            get => _listViewHeight;
            set => SetProperty(ref _listViewHeight, value);
        }

        private MEGame _selectedGame = MEGame.ME1;
        public MEGame SelectedGame { get => _selectedGame; set => SetProperty(ref _selectedGame, value); }

        private void LoadCommands()
        {
            // Player commands
            DumpSelectedGameCommand = new GenericCommand(() => DumpGame(SelectedGame), () => CanDumpGame(SelectedGame) );
            DumpSpecificFilesCommand = new RelayCommand(DumpSpecificFiles, CanDumpSpecificFiles);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
        }

        private async void DumpSpecificFiles(object obj)
        {
            CommonOpenFileDialog dlg = new()
            {
                Multiselect = true,
                EnsureFileExists = true,
                Title = "Select files to dump",
            };
            dlg.Filters.Add(new CommonFileDialogFilter("All supported files", "*.pcc;*.sfm;*.u;*.upk;*.xxx"));
            dlg.Filters.Add(new CommonFileDialogFilter("Mass Effect 1 package files", "*.sfm;*.u;*.upk;*.xxx"));
            dlg.Filters.Add(new CommonFileDialogFilter("Mass Effect 2/3/LE package files", "*.pcc;*.xxx"));

            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                CommonOpenFileDialog outputDlg = new()
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    Title = "Select output folder"
                };
                if (outputDlg.ShowDialog(this) == CommonFileDialogResult.Ok)
                {
                    string outputDir = outputDlg.FileName;
                    await DumpPackages(dlg.FileNames.ToList(), outputDir);
                }
            }
        }

        /// <summary>
        /// Allow cancelation of dumping
        /// </summary>
        private bool DumpCanceled;

        #region commands
        public ICommand DumpSelectedGameCommand { get; set; }
        public ICommand DumpSpecificFilesCommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }

        private int _overallProgressValue;
        public int OverallProgressValue
        {
            get => _overallProgressValue;
            set
            {
                if (SetProperty(ref _overallProgressValue, value))
                {
                    if (value > 0)
                    {
                        TaskbarHelper.SetProgress(value, OverallProgressMaximum);
                        TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
                    }
                    else
                    {
                        TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                    }
                }
            }
        }

        private ActionBlock<PackageDumperSingleFileTask> ProcessingQueue;
        private int _overallProgressMaximum;
        public int OverallProgressMaximum
        {
            get => _overallProgressMaximum;
            set => SetProperty(ref _overallProgressMaximum, value);
        }

        private string _currentOverallOperationText;
        public string CurrentOverallOperationText
        {
            get => _currentOverallOperationText;
            set => SetProperty(ref _currentOverallOperationText, value);
        }

        private bool CanDumpSpecificFiles(object obj)
        {
            return ProcessingQueue == null || ProcessingQueue.Completion.Status != TaskStatus.WaitingForActivation;
        }

        private bool CanDumpGame(MEGame game)
        {
            return MEDirectories.GetDefaultGamePath(game) != null && Directory.Exists(MEDirectories.GetDefaultGamePath(game)) && (ProcessingQueue == null || ProcessingQueue.Completion.Status != TaskStatus.WaitingForActivation);
        }

        private bool CanCancelDump(object obj)
        {
            return ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation && !DumpCanceled;
        }

        private void CancelDump(object obj)
        {
            DumpCanceled = true;
            AllDumpingItems?.ForEach(x => x.DumpCanceled = true);
            CommandManager.InvalidateRequerySuggested(); //Refresh commands
        }

        #endregion

        public PackageDumperWindow(Window owner = null) : base("Package Dumper", true)
        {
            Owner = owner;
            DataContext = this;
            LoadCommands();
            ListViewHeight = 25 * App.CoreCount;
            InitializeComponent();
        }

        private bool verbose;

        //private int v;

        public bool Verbose
        {
            set => verbose = value;
        }

        private async void DumpGame(MEGame game)
        {
            CommonOpenFileDialog m = new()
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select output folder"
            };
            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                string outputDir = m.FileName;
                await DumpPackages(MELoadedFiles.GetFilesLoadedInGame(game, true).Values.ToList(), outputDir);
            }
        }

        private void Dump_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            var (rootPath, outputDir) = (ValueTuple<string, string>)e.Argument;
        }

        private void Dump_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                var result = e.Result;
            }
            catch (Exception ex)
            {
                var exceptionMessage = ex.FlattenException();
                Debug.WriteLine(exceptionMessage);
            }

            //throw new NotImplementedException();
        }

        /// <summary>
        /// Dumps PCC data from all PCCs in the specified folder, recursively.
        /// </summary>
        /// <param name="path">Base path to start dumping functions from. Will search all subdirectories for package files.</param>
        /// <param name="args">Set of arguments for what to dump. In order: imports, exports, data, scripts, coalesced, names. At least 1 of these options must be true.</param>
        /// <param name="outputfolder">Output path to place files in. If null, it will use the same folder as the currently processing PCC. Files will be placed relative to the base path.</param>
        public async void DumpPackagesFromFolder(string path, string outputfolder = null)
        {
            path = Path.GetFullPath(path);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc" };
            List<string> files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Where(s => supportedExtensions.Contains(Path.GetExtension(s.ToLower()))).ToList();
            await DumpPackages(files, outputfolder);
        }

        private async Task DumpPackages(List<string> files, string outputfolder = null)
        {
            CurrentOverallOperationText = "Dumping packages...";
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;
            ProcessingQueue = new ActionBlock<PackageDumperSingleFileTask>(x =>
            {
                if (x.DumpCanceled) { OverallProgressValue++; return; }
                Application.Current.Dispatcher.Invoke(() => CurrentDumpingItems.Add(x));
                x.DumpPackageFile(); // What to do on each item
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OverallProgressValue++; //Concurrency
                    CurrentDumpingItems.Remove(x);
                });
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = App.CoreCount }); // How many items at the same time

            AllDumpingItems = new List<PackageDumperSingleFileTask>();
            CurrentDumpingItems.ClearEx();

            // Calculate what files have duplicate names so we can avoid same-file accessing/writing
            var inputMapping = new CaseInsensitiveDictionary<List<string>>();
            foreach (var item in files)
            {
                var fname = Path.GetFileName(item);
                if (!inputMapping.TryGetValue(fname, out var listOfSameNamedFiles))
                {
                    listOfSameNamedFiles = new List<string>();
                    inputMapping[fname] = listOfSameNamedFiles;
                }
                listOfSameNamedFiles.Add(item);
            }

            // Prevent multithreading conflicts by making unique names
            foreach (var item in inputMapping)
            {
                int i = 0;
                foreach (var packageF in item.Value)
                {
                    var outputFilename = Path.GetFileNameWithoutExtension(item.Key);
                    if (item.Value.Count > 1)
                    {
                        // we need to change names
                        DirectoryInfo parentPath = Directory.GetParent(packageF);
                        while (parentPath != null)
                        {
                            if (parentPath.Name.StartsWith("CookedPC", StringComparison.InvariantCultureIgnoreCase)
                                && parentPath.Parent != null
                                && parentPath.Parent.Name.Equals("BioGame", StringComparison.InvariantCultureIgnoreCase))
                            {
                                outputFilename += "_Basegame";
                                break;
                            }
                            else if (parentPath.Name.StartsWith("DLC_"))
                            {
                                outputFilename += $"_{parentPath.Name}";
                                break;
                            }
                            parentPath = parentPath.Parent;
                        }

                        if (parentPath == null)
                        {
                            // Didn't find it
                            outputFilename += $"_Instance{i}"; // We could use some logic to find a better name, but it's probably not that useful.
                        }
                    }

                    outputFilename += ".txt";

                    string outfolder = outputfolder ?? Directory.GetParent(packageF).ToString();

                    var threadtask = new PackageDumperSingleFileTask(packageF, outputFilename, outfolder);
                    AllDumpingItems.Add(threadtask); //For setting cancellation value
                    ProcessingQueue.Post(threadtask); // Post all items to the block
                    i++;
                }
            }

            ProcessingQueue.Complete(); // Signal completion of adding items
            CommandManager.InvalidateRequerySuggested();
            await ProcessingQueue.Completion; // Asynchronously wait for completion.
            AllDumpingItems = null; // Let the garbage collector clear stuff
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

            ProcessingQueue = null;
            TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Writes a line to the console if verbose mode is turned on
        /// </summary>
        /// <param name="message">Verbose message to write</param>
        public void WriteVerboseLine(string message)
        {
            if (verbose)
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Creates a relative path from one file or folder to another.
        /// </summary>
        /// <param name="fromPath">Contains the directory that defines the start of the relative System.IO.Path.</param>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative System.IO.Path.</param>
        /// <returns>The relative path from the start directory to the end System.IO.Path.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fromPath"/> or <paramref name="toPath"/> is <c>null</c>.</exception>
        /// <exception cref="UriFormatException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                throw new ArgumentNullException(nameof(fromPath));
            }

            if (string.IsNullOrEmpty(toPath))
            {
                throw new ArgumentNullException(nameof(toPath));
            }

            Uri fromUri = new(AppendDirectorySeparatorChar(fromPath));
            Uri toUri = new(AppendDirectorySeparatorChar(toPath));

            if (fromUri.Scheme != toUri.Scheme)
            {
                return toPath;
            }

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());
            if (relativePath == "./")
            {
                return toPath;
            }
            if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            // Append a slash only if the path is a directory and does not have a slash.
            if (!Path.HasExtension(path) &&
                !path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }

        private void PackageDumper_Closing(object sender, CancelEventArgs e)
        {
            DumpCanceled = true;
            AllDumpingItems?.ForEach(x => x.DumpCanceled = true);
            AllDumpingItems = null;
        }

        private void PackageDumper_Loaded(object sender, RoutedEventArgs e)
        {
            Owner = null; //Detach from parent
        }

        private async void PackageDumper_FilesDropped(object sender, DragEventArgs e)
        {
            if (ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation) { return; } //Busy

            string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filenames.Length == 1 && Directory.Exists(filenames[0]))
            {
                //Directory - can drop
                DumpPackagesFromFolder(filenames[0]);
            }
            else
            {
                await DumpPackages(filenames.ToList());
            }
        }

        private void PackageDumper_DragOver(object sender, DragEventArgs e)
        {
            if (ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation)
            {
                //Busy
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }

            bool dropEnabled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                string[] filenames =
                                 e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (filenames.Length == 1 && Directory.Exists(filenames[0]))
                {
                    //Directory - can drop
                }
                else
                {
                    string[] acceptedExtensions = new string[] { ".pcc", ".u", ".upk", ".sfm" };
                    foreach (string filename in filenames)
                    {
                        string extension = Path.GetExtension(filename).ToLower();
                        if (!acceptedExtensions.Contains(extension))
                        {
                            dropEnabled = false;
                            break;
                        }
                    }
                }
            }
            else
            {
                dropEnabled = false;
            }

            if (!dropEnabled)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }
    }

    public class PackageDumperSingleFileTask : NotifyPropertyChangedBase
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

        public PackageDumperSingleFileTask(string packageToDump, string outputFilenameNoExtension = null, string outputfolder = null)
        {
            MemoryAnalyzer.AddTrackedMemoryItem($"PDSFT for {packageToDump}", new WeakReference(this));
            _packageToDump = packageToDump;
            ShortFileName = outputFilenameNoExtension ?? Path.GetFileNameWithoutExtension(packageToDump);
            OutputFolder = outputfolder;
            CurrentOverallOperationText = "Dumping " + ShortFileName;
        }

        public bool DumpCanceled;
        private readonly string _packageToDump;
        private readonly string OutputFolder;

        /// <summary>
        /// Dumps data from a pcc file to a text file
        /// </summary>
        public void DumpPackageFile()
        {
            //if (GamePath == null)
            //{
            //    Console.Error.WriteLine("Game path not defined. Can't dump file file with undefined game System.IO.Path.");
            //    return;
            //}
            //try
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(_packageToDump);
                CurrentFileProgressMaximum = pcc.ExportCount;
                string outfolder = OutputFolder ?? Directory.GetParent(_packageToDump).ToString();

                FileLib fileLib = null;

                //if (properties)
                //{
                //    UnrealObjectInfo.loadfromJSON();
                //}
                //dumps data.
                string savepath = Path.Combine(outfolder, OutputFolder == null ? ShortFileName : Path.GetFileNameWithoutExtension(_packageToDump) + ".txt");
                Directory.CreateDirectory(Path.GetDirectoryName(savepath));

                using StreamWriter stringoutput = new(savepath);
                //if (imports)
                //{
                //writeVerboseLine("Getting Imports");
                stringoutput.WriteLine("--Imports");
                for (int x = 0; x < pcc.Imports.Count; x++)
                {
                    ImportEntry imp = pcc.Imports[x];
                    stringoutput.WriteLine($"#{imp.UIndex}: {imp.InstancedFullPath}(From: {imp.PackageFile}) (Offset: 0x {pcc.ImportOffset + (x * ImportEntry.HeaderLength):X4})");
                }
                stringoutput.WriteLine("--End of Imports");
                //}

                string datasets = "Exports Coalesced ";
                //if (GameBeingDumped != MEGame.ME2)
                //{
                datasets += " Functions";
                //}

                stringoutput.WriteLine("--Start of " + datasets);
                stringoutput.WriteLine("Exports starting with [C] can be overriden from the configuration file");

                int numDone = 1;
                //writeVerboseLine("Enumerating exports");
                //string swfoutfolder = outfolder + Path.GetFileNameWithoutExtension(_packageToDump) + "\\";
                foreach (ExportEntry exp in pcc.Exports)
                {
                    if (DumpCanceled)
                    {
                        return;
                    }
                    //writeVerboseLine("Parse export #" + index);
                    CurrentFileProgressValue = exp.UIndex;
                    //bool isCoalesced = coalesced && exp.likelyCoalescedVal;
                    string className = exp.ClassName;
                    bool isCoalesced = false;
                    if (exp.HasStack)
                    {
                        var objectFlags = exp.GetPropertyFlags();
                        isCoalesced = objectFlags != null && objectFlags.Value.HasFlag(UnrealFlags.EPropertyFlags.Config);
                    }
                    bool shouldUseDecompiledText = (className is "Function" or "Enum" or "State");
                    //int progress = ((int)(((double)numDone / numTotal) * 100));
                    //while (progress >= (lastProgress + 10))
                    //{
                    //    Console.Write("..." + (lastProgress + 10) + "%");
                    //    //needsFlush = true;
                    //    lastProgress += 10;
                    //}

                    stringoutput.WriteLine("=======================================================================");
                    stringoutput.Write($"#{exp.UIndex} ");
                    if (isCoalesced)
                    {
                        stringoutput.Write("[C] ");
                    }

                    stringoutput.Write($"{exp.InstancedFullPath}({exp.ClassName})");
                    if (exp.SuperClassName != "Class")
                    {
                        stringoutput.Write($" (Superclass: {exp.SuperClass?.InstancedFullPath})");
                    }

                    if (exp.Archetype != null)
                    {
                        stringoutput.Write($" (Archetype: {exp.Archetype?.InstancedFullPath})");
                    }

                    stringoutput.WriteLine(); // next line please
                    

                    if (shouldUseDecompiledText)
                    {
                        //initing filelib is expensive, so we only want to do it for files that have script
                        if (fileLib is null)
                        {
                            fileLib = new FileLib(pcc);
                            try
                            {
                                fileLib.Initialize();
                            }
                            catch
                            {
                                //we're not checking if it failed to initialize because we don't really care, since decompilation doesn't actually need an initialized filelib,
                                //it just makes the output a bit nicer (at the time of this writing all it does is resolve some integer literals into enum values)
                            }
                        }
                        stringoutput.WriteLine($"=============={exp.ClassName}==============");

                        (_, string functionText) = UnrealScriptCompiler.DecompileExport(exp, fileLib);

                        stringoutput.WriteLine(functionText);
                    }
                    //TODO: Change to UProperty

                    if (!shouldUseDecompiledText && exp.ClassName != "Class" && exp.ClassName != "ShaderCache")
                    {
                        try
                        {
                            var props = exp.GetProperties();
                            if (props.Count > 0)
                            {
                                stringoutput.WriteLine("==============Properties==============");
                                var topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                                foreach (Property prop in props)
                                {
                                    InterpreterExportLoader.GenerateUPropertyTreeForProperty(prop, topLevelTree, exp);
                                }
                                topLevelTree.PrintPretty("", stringoutput, false, exp);
                                stringoutput.WriteLine();
                            }
                        }
                        catch
                        {
                            Debug.WriteLine(exp.UIndex);
                        }
                    }
                }
                numDone++;
                stringoutput.WriteLine($"--End of {datasets}");

                // writeVerboseLine("Gathering names");
                stringoutput.WriteLine("--Start of Names");

                int count = 0;
                foreach (string s in pcc.Names)
                    stringoutput.WriteLine((count++) + " : " + s);
                stringoutput.WriteLine("--End of Names");
            }
        }
    }
}
