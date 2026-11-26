using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using LegendaryExplorer.Audio;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.Soundplorer;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Sound.ISACT;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.WindowsAPICodePack.Dialogs;
using static LegendaryExplorer.Tools.TlkManagerNS.TLKManagerWPF;

namespace LegendaryExplorer.Tools.DialogueDumper
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class DialogueDumperWindow : TrackingNotifyPropertyChangedWindowBase
    {
        /// <summary>
        /// Items show in the list that are currently being processed
        /// </summary>
        public ObservableCollectionExtended<DialogueDumperSingleFileTask> CurrentDumpingItems { get; set; } = new ObservableCollectionExtended<DialogueDumperSingleFileTask>();

        public ObservableCollectionExtended<MEGame> SupportedGames { get; } = new ObservableCollectionExtended<MEGame>()
        {
            MEGame.ME1,
            MEGame.ME2,
            MEGame.ME3,
            MEGame.LE1,
            MEGame.LE2,
            MEGame.LE3
        };

        private MEGame _selectedGame = MEGame.LE3;
        public MEGame SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (SetProperty(ref _selectedGame, value))
                {
                    DumpableLocalizations.ReplaceAll(gameSpecificLocalization[value]);
                }
            }
        }

        private readonly Dictionary<MEGame, MELocalization[]> gameSpecificLocalization = new()
        {
            // Probably should just use the list from Mod Manager
            { MEGame.ME1, new[] { MELocalization.INT, MELocalization.ESN, MELocalization.DEU, MELocalization.FRA, MELocalization.ITA, MELocalization.RUS } }, // IDK if we can open the official POL-extension version (2nd release)
            { MEGame.ME2, new[] { MELocalization.INT, MELocalization.ESN, MELocalization.DEU, MELocalization.FRA, MELocalization.ITA, MELocalization.POL, MELocalization.RUS, MELocalization.JPN } }, // ME2 had a JPN version. It's rare
            { MEGame.ME3, new[] { MELocalization.INT, MELocalization.ESN, MELocalization.DEU, MELocalization.FRA, MELocalization.ITA, MELocalization.POL, MELocalization.RUS, MELocalization.JPN } }, // ME3 had a JPN version. It's rare
            { MEGame.LE1, new[] { MELocalization.INT, MELocalization.ESN, MELocalization.DEU, MELocalization.FRA, MELocalization.ITA, MELocalization.POL, MELocalization.RUS, MELocalization.JPN } },
            { MEGame.LE2, new[] { MELocalization.INT, MELocalization.ESN, MELocalization.DEU, MELocalization.FRA, MELocalization.ITA, MELocalization.POL, MELocalization.RUS, MELocalization.JPN } },
            { MEGame.LE3, new[] { MELocalization.INT, MELocalization.ESN, MELocalization.DEU, MELocalization.FRA, MELocalization.ITA, MELocalization.POL, MELocalization.RUS, MELocalization.JPN } }
        };

        public ObservableCollectionExtended<MELocalization> DumpableLocalizations { get; } = new();

        private MELocalization _selectedLocalization = MELocalization.INT;
        public MELocalization SelectedLocalization
        {
            get => _selectedLocalization;
            set => SetProperty(ref _selectedLocalization, value);
        }

        /// <summary>
        /// All items in the queue
        /// </summary>
        private List<DialogueDumperSingleFileTask> AllDumpingItems;

        public XLWorkbook workbook = new();
        private static BackgroundWorker xlworker = new();
        public BlockingCollection<List<string>> _xlqueue = new();
        private ActionBlock<DialogueDumperSingleFileTask> ProcessingQueue;
        public string outputfile;

        private int _listViewHeight;
        public int ListViewHeight
        {
            get => _listViewHeight;
            set => SetProperty(ref _listViewHeight, value);
        }

        private void LoadCommands()
        {
            // Player commands
            DumpGameCommand = new RelayCommand(DumpSelectedGame, CanDumpGame);
            DumpSpecificFilesCommand = new RelayCommand(DumpSpecificFiles, CanDumpSpecificFiles);
            DumpGameAudioCommand = new RelayCommand(DumpSelectedGameAudio, CanDumpGame);
            DumpSpecificAudioFilesCommand = new RelayCommand(DumpSpecificAudioFiles, CanDumpSpecificFiles);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
            ManageTLKsCommand = new RelayCommand(ManageTLKs);
        }

        private async void DumpSpecificFiles(object obj)
        {
            CommonOpenFileDialog dlg = new()
            {
                Multiselect = true,
                EnsureFileExists = true,
                Title = "Select files to dump",
            };
            dlg.Filters.Add(new CommonFileDialogFilter("All supported files", "*.pcc;*.sfm;*.u;*.upk"));
            dlg.Filters.Add(new CommonFileDialogFilter("Mass Effect package files", "*.sfm;*.u;*.upk"));
            dlg.Filters.Add(new CommonFileDialogFilter("Mass Effect 2/3/LE package files", "*.pcc"));

            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                CommonSaveFileDialog outputDlg = new()
                {
                    Title = "Select excel output",
                    DefaultFileName = "DialogueDump.xlsx",
                    DefaultExtension = "xlsx",
                };
                outputDlg.Filters.Add(new CommonFileDialogFilter("Excel Files", "*.xlsx"));

                if (outputDlg.ShowDialog(this) == CommonFileDialogResult.Ok)
                {
                    outputfile = outputDlg.FileName;
                    await DumpPackages(dlg.FileNames.ToList(), outputfile);
                }
            }
            this.RestoreAndBringToFront();
        }

        private async void DumpSpecificAudioFiles(object obj)
        {
            CommonOpenFileDialog dlg = new()
            {
                Multiselect = true,
                EnsureFileExists = true,
                Title = "Select files to dump",
            };
            dlg.Filters.Add(new CommonFileDialogFilter("All supported files", "*.pcc;*.sfm;*.u;*.upk"));
            dlg.Filters.Add(new CommonFileDialogFilter("Mass Effect package files", "*.sfm;*.u;*.upk"));
            dlg.Filters.Add(new CommonFileDialogFilter("Mass Effect 2/3/LE package files", "*.pcc"));

            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                var dlgOut = new CommonOpenFileDialog("Select output folder")
                {
                    IsFolderPicker = true
                };
                if (dlgOut.ShowDialog(this) != CommonFileDialogResult.Ok)
                {
                    return;
                }
                outputfile = dlgOut.FileName;
                await DumpAudioPackages(dlg.FileNames.ToList(), outputfile);
            }
            this.RestoreAndBringToFront();
        }

        /// <summary>
        /// Cancelation of dumping
        /// </summary>
        private bool DumpCanceled;

        /// <summary>
        /// used to monitor process
        /// </summary>
        private bool isProcessing;

        /// <summary>
        /// output debug info to excel
        /// </summary>
        public bool shouldDoDebugOutput;

        #region Commands
        public ICommand DumpGameCommand { get; set; }
        public ICommand DumpSpecificFilesCommand { get; set; }
        public ICommand DumpGameAudioCommand { get; set; }
        public ICommand DumpSpecificAudioFilesCommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }
        public ICommand ManageTLKsCommand { get; set; }

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

        private string _currentOverallOperationText;
        public string CurrentOverallOperationText
        {
            get => _currentOverallOperationText;
            set => SetProperty(ref _currentOverallOperationText, value);
        }

        private bool CanDumpSpecificFiles(object obj)
        {
            return (ProcessingQueue == null || ProcessingQueue.Completion.Status != TaskStatus.WaitingForActivation) && !isProcessing;
        }

        private bool CanDumpGame(object obj)
        {
            var gameDir = MEDirectories.GetDefaultGamePath(SelectedGame);
            var gameExists = gameDir != null && Directory.Exists(gameDir);
            return gameExists &&
                   (ProcessingQueue == null ||
                    ProcessingQueue.Completion.Status != TaskStatus.WaitingForActivation)
                   && !isProcessing;
        }

        private void DumpSelectedGame(object obj)
        {
            DumpGame(SelectedGame);
        }

        private void DumpSelectedGameAudio(object obj)
        {
            DumpGameAudio(SelectedGame);
        }

        private bool CanCancelDump(object obj)
        {
            return ((ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation) || isProcessing) && !DumpCanceled;
        }

        private void CancelDump(object obj)
        {
            DumpCanceled = true;
            AllDumpingItems?.ForEach(x => x.DumpCanceled = true);
            CommandManager.InvalidateRequerySuggested(); //Refresh commands
        }

        private static void ManageTLKs(object obj)
        {
            var tlkmgr = new TlkManagerNS.TLKManagerWPF();
            tlkmgr.Show();
        }

        #endregion

        public DialogueDumperWindow(Window owner = null) : base("Dialogue Dumper", true)
        {
            Owner = owner;
            LoadCommands();
            ListViewHeight = 25 * App.CoreCount;
            InitializeComponent();
        }

        private void DumpGame(MEGame game)
        {
            string rootPath = MEDirectories.GetDefaultGamePath(game);

            CommonSaveFileDialog m = new()
            {
                Title = "Select excel output",
                DefaultFileName = $"{game}DialogueDump.xlsx",
                DefaultExtension = "xlsx",
            };
            m.Filters.Add(new CommonFileDialogFilter("Excel Files", "*.xlsx"));

            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                outputfile = m.FileName;
                this.RestoreAndBringToFront();
                DumpPackagesFromFolder(rootPath, outputfile, game);
            }
        }

        private void DumpGameAudio(MEGame game)
        {
            string rootPath = MEDirectories.GetDefaultGamePath(game);
            var dlgOut = new CommonOpenFileDialog("Select output folder")
            {
                IsFolderPicker = true
            };
            if (dlgOut.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                outputfile = dlgOut.FileName;
                this.RestoreAndBringToFront();
                DumpAudioPackagesFromFolder(rootPath, outputfile, game);
            }
        }

        private async void DialogueDumper_FilesDropped(object sender, DragEventArgs e)
        {
            if (ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation) { return; } //Busy

            CommonSaveFileDialog outputDlg = new()
            {
                Title = "Select excel output",
                DefaultFileName = "DialogueDump.xlsx",
                DefaultExtension = "xlsx",
            };
            outputDlg.Filters.Add(new CommonFileDialogFilter("Excel Files", "*.xlsx"));

            if (outputDlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                outputfile = outputDlg.FileName;
            }
            this.RestoreAndBringToFront();

            OverallProgressValue = 0;
            OverallProgressMaximum = 100;
            CurrentOverallOperationText = "Scanning...";

            var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filenames.Length == 1 && Directory.Exists(filenames[0]))
            {
                //Directory - can drop
                DumpPackagesFromFolder(filenames[0], outputfile);
            }
            else
            {
                await DumpPackages(filenames.ToList(), outputfile);
            }
        }

        /// <summary>
        /// Dumps PCC data from all PCCs in the specified folder, recursively.
        /// </summary>
        /// <param name="path">Base path to start dumping functions from. Will search all subdirectories for package files.</param>
        /// <param name="game">MEGame game determines.  If default then Unknown, which means done as single file (always fully parsed). </param>
        /// <param name="outputfile">Output Excel document.</param>
        public async void DumpPackagesFromFolder(string path, string outFile, MEGame game = MEGame.Unknown)
        {
            OverallProgressValue = 0;
            OverallProgressMaximum = 100;
            CurrentOverallOperationText = "Scanning...";
            await Task.Delay(100);  //allow dialog catch up before i/o

            path = Path.GetFullPath(path);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc" };
            List<string> files = Directory.GetFiles(path, "Bio*.*", SearchOption.AllDirectories)
                .Where(s => supportedExtensions.Contains(Path.GetExtension(s.ToLower())) && s.GetUnrealLocalization() == MELocalization.None || s.GetUnrealLocalization() == SelectedLocalization).ToList();
            await DumpPackages(files, outFile, game);
        }


        /// <summary>
        /// Dumps PCC data from all PCCs in the specified folder, recursively.
        /// </summary>
        /// <param name="path">Base path to start dumping functions from. Will search all subdirectories for package files.</param>
        /// <param name="game">MEGame game determines.  If default then Unknown, which means done as single file (always fully parsed). </param>
        /// <param name="outputfile">Output audio files to the selected folder.</param>
        public async void DumpAudioPackagesFromFolder(string path, string outFile, MEGame game = MEGame.Unknown)
        {
            OverallProgressValue = 0;
            OverallProgressMaximum = 100;
            CurrentOverallOperationText = "Scanning...";
            await Task.Delay(100);  //allow dialog catch up before i/o

            path = Path.GetFullPath(path);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc" };
            List<string> files = Directory.GetFiles(path, "Bio*.*", SearchOption.AllDirectories)
                .Where(s => supportedExtensions.Contains(Path.GetExtension(s.ToLower())) && s.GetUnrealLocalization() == MELocalization.None || s.GetUnrealLocalization() == SelectedLocalization).ToList();
            await DumpAudioPackages(files, outFile, game);
        }

        private async Task DumpPackages(List<string> files, string outFile, MEGame game = MEGame.Unknown)
        {
            CurrentOverallOperationText = "Dumping packages...";
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;
            isProcessing = true;
            workbook = new XLWorkbook();
#if DEBUG
            shouldDoDebugOutput = true;  //Output for toolset devs
#endif

            var xlstrings = workbook.Worksheets.Add("TLKStrings");
            var xlowners = workbook.Worksheets.Add("ConvoOwners");

            //Setup column headers
            xlstrings.Cell(1, 1).Value = "Speaker";
            xlstrings.Cell(1, 2).Value = "TLK StringRef";
            xlstrings.Cell(1, 3).Value = "Line";
            xlstrings.Cell(1, 4).Value = "Conversation";
            xlstrings.Cell(1, 5).Value = "Game";
            xlstrings.Cell(1, 6).Value = "File";
            xlstrings.Cell(1, 7).Value = "Object #";

            xlowners.Cell(1, 1).Value = "Conversation";
            xlowners.Cell(1, 2).Value = "Owner";
            xlowners.Cell(1, 3).Value = "File";

            if (shouldDoDebugOutput) //DEBUG
            {
                var xltags = workbook.Worksheets.Add("Tags");
                xltags.Cell(1, 1).Value = "ActorTag";
                xltags.Cell(1, 2).Value = "StrRef";
                xltags.Cell(1, 3).Value = "FriendlyName";

                var xlanims = workbook.Worksheets.Add("Animations");
                xlanims.Cell(1, 1).Value = "Name";
                xlanims.Cell(1, 2).Value = "Package";
                xlanims.Cell(1, 3).Value = "Sequence Name";
                xlanims.Cell(1, 4).Value = "Length(sec)";
                xlanims.Cell(1, 5).Value = "Filename";
                xlanims.Cell(1, 6).Value = "Filename";

                var xldebug = workbook.Worksheets.Add("DEBUG");
                xldebug.Cell(1, 1).Value = "Status";
                xldebug.Cell(1, 2).Value = "File";
                xldebug.Cell(1, 3).Value = "Class";
                xldebug.Cell(1, 4).Value = "Uexport";
                xldebug.Cell(1, 5).Value = "e";
            }

            _xlqueue = new BlockingCollection<List<string>>(); //Reset queue for multiple operations

            //Background Consumer does excel work
            xlworker = new BackgroundWorker();
            xlworker.DoWork += XlProcessor;
            xlworker.RunWorkerCompleted += Xlworker_RunWorkerCompleted;
            xlworker.WorkerSupportsCancellation = true;
            xlworker.RunWorkerAsync();

            ProcessingQueue = new ActionBlock<DialogueDumperSingleFileTask>(x =>
            {
                if (x.DumpCanceled)
                {
                    OverallProgressValue++;
                    return;
                }
                Application.Current.Dispatcher.Invoke(() => CurrentDumpingItems.Add(x));
                x.DumpPackageFile(game, this); // What to do on each item
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OverallProgressValue++; //Concurrency 
                    CurrentDumpingItems.Remove(x);
                });
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Math.Min(App.CoreCount, 8) }); // How many items at the same time 

            AllDumpingItems = new List<DialogueDumperSingleFileTask>();
            CurrentDumpingItems.ClearEx();
            foreach (var item in files)
            {
                var threadtask = new DialogueDumperSingleFileTask(item, SelectedLocalization);
                AllDumpingItems.Add(threadtask); //For setting cancelation value
                ProcessingQueue.Post(threadtask); // Post all items to the block
            }

            ProcessingQueue.Complete(); // Signal completion
            CommandManager.InvalidateRequerySuggested();
            await ProcessingQueue.Completion;

            if (!shouldDoDebugOutput)
            {
                CurrentOverallOperationText = $"Dump {(DumpCanceled ? "canceled" : "completed")} - saving excel";
            }

            while (isProcessing)
            {
                isProcessing = await CheckProcess();
            }
        }

        private async Task DumpAudioPackages(List<string> files, string outFile, MEGame game = MEGame.Unknown)
        {
            CurrentOverallOperationText = "Dumping packages...";
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;
            isProcessing = true;

            _xlqueue = new BlockingCollection<List<string>>(); //Reset queue for multiple operations

            //Background Consumer does work
            xlworker = new BackgroundWorker();
            xlworker.DoWork += XlAudioProcessor;
            xlworker.RunWorkerCompleted += Xlworker_RunAudioWorkerCompleted;
            xlworker.WorkerSupportsCancellation = true;
            xlworker.RunWorkerAsync();

            ProcessingQueue = new ActionBlock<DialogueDumperSingleFileTask>(x =>
            {
                if (x.DumpCanceled)
                {
                    OverallProgressValue++;
                    return;
                }
                Application.Current.Dispatcher.Invoke(() => CurrentDumpingItems.Add(x));
                x.DumpPackageFileAudio(game, this); // What to do on each item
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OverallProgressValue++; //Concurrency 
                    CurrentDumpingItems.Remove(x);
                });
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Math.Min(App.CoreCount, 8) }); // How many items at the same time 

            AllDumpingItems = new List<DialogueDumperSingleFileTask>();
            CurrentDumpingItems.ClearEx();
            foreach (var item in files)
            {
                var threadtask = new DialogueDumperSingleFileTask(item, SelectedLocalization);
                AllDumpingItems.Add(threadtask); //For setting cancelation value
                ProcessingQueue.Post(threadtask); // Post all items to the block
            }

            ProcessingQueue.Complete(); // Signal completion
            CommandManager.InvalidateRequerySuggested();
            await ProcessingQueue.Completion;

            if (!shouldDoDebugOutput)
            {
                CurrentOverallOperationText = $"Dump {(DumpCanceled ? "canceled" : "completed")} - saving excel";
            }

            while (isProcessing)
            {
                isProcessing = await CheckProcess();
            }
        }

        public async Task<bool> CheckProcess()
        {
            if (_xlqueue.IsEmpty() && ((OverallProgressValue >= OverallProgressMaximum) || DumpCanceled))
            {
                _xlqueue.CompleteAdding();
                return false;
            }

            await Task.Delay(1000);
            return true;
        }

        private void Xlworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            xlworker.CancelAsync();
            CommandManager.InvalidateRequerySuggested();
            try
            {
                workbook.SaveAs(outputfile);
                if (DumpCanceled)
                {
                    DumpCanceled = false;
                    OverallProgressMaximum = 100;
                    MessageBox.Show("Dialogue Dump was cancelled. Work in progress was saved.", "Cancelled", MessageBoxButton.OK);
                    CurrentOverallOperationText = "Dump canceled";
                }
                else
                {
                    OverallProgressValue = 100;
                    OverallProgressMaximum = 100;
                    MessageBox.Show("Dialogue Dump was completed.", "Success", MessageBoxButton.OK);
                    CurrentOverallOperationText = "Dump completed";
                }
            }
            catch
            {
                MessageBox.Show("Unable to save excel file. Check it is not open.", "Error", MessageBoxButton.OK);
            }
        }

        private void Xlworker_RunAudioWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            xlworker.CancelAsync();
            CommandManager.InvalidateRequerySuggested();
            try
            {
                if (DumpCanceled)
                {
                    DumpCanceled = false;
                    OverallProgressMaximum = 100;
                    MessageBox.Show("Dialogue Dump was cancelled. Work in progress was saved.", "Cancelled", MessageBoxButton.OK);
                    CurrentOverallOperationText = "Dump canceled";
                }
                else
                {
                    OverallProgressValue = 100;
                    OverallProgressMaximum = 100;
                    MessageBox.Show("Dialogue Dump was completed.", "Success", MessageBoxButton.OK);
                    CurrentOverallOperationText = "Dump completed";
                }
            }
            catch
            {
                MessageBox.Show("Unable to save excel file. Check it is not open.", "Error", MessageBoxButton.OK);
            }
        }

        private void XlProcessor(object sender, DoWorkEventArgs e)
        {
            foreach (List<string> newrow in _xlqueue.GetConsumingEnumerable(CancellationToken.None))
            {
                try
                {
                    string sheetName = newrow[0];
                    var activesheet = workbook.Worksheet(sheetName);
                    int nextrow = activesheet.LastRowUsed().RowNumber() + 1;
                    //Write output to excel
                    for (int s = 1; s < newrow.Count; s++)
                    {
                        string val = newrow[s];
                        activesheet.Cell(nextrow, s).Value = val;
                    }
                }
                catch
                {
                    MessageBox.Show($"Error writing excel. {newrow[0]}, {newrow[2]}, {newrow[6]}, {newrow[7]}");
                }
            }
        }

        private void XlAudioProcessor(object sender, DoWorkEventArgs e)
        {
            foreach (List<string> newrow in _xlqueue.GetConsumingEnumerable(CancellationToken.None))
            {
                try
                {
                    // Noop - actual work is being done in the task
                }
                catch
                {
                    MessageBox.Show($"Error writing audio.");
                }
            }
        }

        private void DialogueDumper_Closing(object sender, CancelEventArgs e)
        {
            DumpCanceled = true;
            AllDumpingItems?.ForEach(x => x.DumpCanceled = true);
        }

        private void DialogueDumper_Loaded(object sender, RoutedEventArgs e)
        {
            Owner = null; //Detach from parent
            DumpableLocalizations.ReplaceAll(gameSpecificLocalization[SelectedGame]); // Populate the list for the first time
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
        }

        private void DialogueDumper_DragOver(object sender, DragEventArgs e)
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
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop, true);
                if (filenames.Length == 1 && Directory.Exists(filenames[0]))
                {
                    //Directory - can drop
                }
                else
                {
                    string[] acceptedExtensions = { ".pcc", ".u", ".upk", ".sfm" };
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

    public class DialogueDumperSingleFileTask : NotifyPropertyChangedBase
    {
        private string _currentOverallOperationText;
        private MELocalization _selectedLocalization;

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

        public DialogueDumperSingleFileTask(string file, MELocalization localization)
        {
            File = file;
            _selectedLocalization = localization;

            ShortFileName = Path.GetFileNameWithoutExtension(file);
            CurrentOverallOperationText = $"Dumping {ShortFileName}";
        }

        public bool DumpCanceled;

        private readonly string File;

        /// <summary>
        /// Dumps Conversation strings to xl worksheet
        /// </summary>
        /// <workbook>Output excel workbook</workbook>
        public void DumpPackageFile(MEGame GameBeingDumped, DialogueDumperWindow dumper)
        {
            string fileName = ShortFileName.ToUpper();
            dumper.CurrentOverallOperationText = $"Dumping Packages.... {dumper.OverallProgressValue}/{dumper.OverallProgressMaximum}";

            if (dumper.shouldDoDebugOutput)
            {
                var excelout = new List<string> { "DEBUG", "IN PROCESS", fileName };
                dumper._xlqueue.Add(excelout);
            }

            //SETUP FILE FILTERS
            bool CheckConv;
            bool CheckActor;
            var fileLoc = fileName.GetUnrealLocalization();
            if (GameBeingDumped == MEGame.Unknown) //Unknown = Single files or folders that always fully parse
            {
                CheckConv = true;
                CheckActor = true;
            }
            else if (GameBeingDumped.IsGame1() && (fileLoc == MELocalization.None || fileLoc == _selectedLocalization) && !fileName.EndsWith(@"LAY") && !fileName.EndsWith(@"SND") && !fileName.EndsWith(@"_T") && !fileName.StartsWith(@"BIOG") && !fileName.StartsWith(@"BIOC"))
            {
                CheckConv = true; //Filter ME1 remove file types that never have convos. Levels only.
                CheckActor = true;
            }
            // 05/07/2022 - Change from != ME1 to !IsGame1(), since structure didn't change for ME1 to LE1 - Mgamerz
            else if (!GameBeingDumped.IsGame1() && fileLoc == _selectedLocalization) //Filter ME2/3 files with potential convos
            {
                CheckConv = true;
                CheckActor = false;
            }
            else if (!GameBeingDumped.IsGame1() && fileLoc != _selectedLocalization && !fileName.StartsWith(@"BIOG")) //Filter ME2/3 files with potential actors
            {
                CheckConv = false;
                CheckActor = true;
            }
            else //Otherwise skip file
            {
                CurrentFileProgressValue = CurrentFileProgressMaximum;
                if (dumper.shouldDoDebugOutput)
                {
                    var excelout = new List<string> { "DEBUG", "SKIPPED", fileName };
                    dumper._xlqueue.Add(excelout);
                }
                return;
            }

            string className = null;
            IMEPackage pcc = null;

            try
            {
                var testPcc = MEPackageHandler.QuickOpenMEPackage(File);
                if (testPcc.Game.IsGame1() || dumper.shouldDoDebugOutput)
                {
                    // We need to do a full load so it loads the local TLKS
                    pcc = MEPackageHandler.OpenMEPackage(File);
                }
                else
                {
                    // We can do a partial load which will skip reading a lot of stuff
                    // THIS ONLY WORKS IN RELEASE MODE
                    // because the debug dump stuff would mean i have to load a bunch of stuff so I don't bother.
                    pcc = MEPackageHandler.UnsafePartialLoad(File, x =>
                        x.ClassName is "BioConversation" or "BioTlkFile" or "BioTlkFileSet"
                    );
                }
                if (pcc.Game.IsGame1() && pcc is UnrealPackageFile upf)
                {
                    // Force it to read the proper TLK - it may not be set properly in LEX
                    upf.SetLocalTLKs(upf.ReadLocalTLKs(_selectedLocalization.ToLocaleString(pcc.Game)));
                }
                //using IMEPackage pcc = MEPackageHandler.OpenMEPackage(File);
                if (GameBeingDumped == MEGame.Unknown) //Correct mapping
                {
                    GameBeingDumped = pcc.Game;
                }

                CurrentFileProgressMaximum = pcc.ExportCount;

                //CHECK FOR CONVERSATIONS TO DUMP
                if (CheckConv)
                {
                    // We do a .ToArray() since we need to account. Count() a few times
                    var convExports = pcc.Exports.Where(x => x.ClassName == "BioConversation").ToArray();
                    CurrentFileProgressMaximum = convExports.Length;

                    int doneCount = 0;
                    foreach (ExportEntry exp in convExports)
                    {
                        if (DumpCanceled)
                        {
                            return;
                        }

                        CurrentFileProgressValue = doneCount++;

                        string convName = exp.ObjectName.Instanced;
                        int convIdx = exp.UIndex;

                        var convo = exp.GetProperties();
                        if (convo.Count > 0)
                        {
                            //1.  Define speaker list "m_aSpeakerList"
                            var speakers = new List<string>();
                            if (!GameBeingDumped.IsGame3()) //05/07/2022 - Changed from != ME3 to !.IsGame3() - Mgamerz
                            {
                                var s_speakers = exp.GetProperty<ArrayProperty<StructProperty>>("m_SpeakerList");
                                if (s_speakers != null)
                                {
                                    speakers.AddRange(s_speakers.Select(s =>
                                        s.GetProp<NameProperty>("sSpeakerTag").Value.Instanced));
                                }
                            }
                            else
                            {
                                var a_speakers = exp.GetProperty<ArrayProperty<NameProperty>>("m_aSpeakerList");
                                if (a_speakers != null)
                                {
                                    speakers.AddRange(a_speakers.Select(n => n.Value.Instanced));
                                }
                            }

                            //2. Go through Entry list "m_EntryList"
                            // Parse line TLK StrRef, TLK Line, Speaker -1 = Owner, -2 = Shepard, or from m_aSpeakerList

                            var entryList = exp.GetProperty<ArrayProperty<StructProperty>>("m_EntryList");
                            foreach (StructProperty entry in entryList)
                            {
                                //Get and set speaker name
                                var speakeridx = entry.GetProp<IntProperty>("nSpeakerIndex");
                                string lineSpeaker;
                                if (speakeridx >= 0)
                                {
                                    lineSpeaker = speakers[speakeridx];
                                }
                                else if (speakeridx == -2)
                                {
                                    lineSpeaker = "Shepard";
                                }
                                else
                                {
                                    lineSpeaker = "Owner";
                                }

                                //Get StringRef
                                int lineStrRef = entry.GetProp<StringRefProperty>("srText").Value;
                                if (lineStrRef > 0)
                                {
                                    //Get StringRef Text
                                    string lineTLKstring =
                                        GlobalFindStrRefbyID(lineStrRef, GameBeingDumped, exp.FileRef);

                                    if (lineTLKstring != "No Data" && lineTLKstring != "\"\"" &&
                                        lineTLKstring != "\" \"")
                                    {
                                        //Write to Background thread
                                        var excelout = new List<string>
                                            {
                                                "TLKStrings", lineSpeaker, lineStrRef.ToString(), lineTLKstring,
                                                convName, GameBeingDumped.ToString(), fileName, convIdx.ToString()
                                            };
                                        dumper._xlqueue.Add(excelout);
                                    }
                                }
                            }

                            //3. Go through Reply list "m_ReplyList"
                            // Parse line TLK StrRef, TLK Line, Speaker always Shepard
                            var replyList = exp.GetProperty<ArrayProperty<StructProperty>>("m_ReplyList");
                            if (replyList != null)
                            {
                                foreach (StructProperty reply in replyList)
                                {
                                    //Get and set speaker name
                                    const string lineSpeaker = "Shepard";

                                    //Get StringRef
                                    var lineStrRef = reply.GetProp<StringRefProperty>("srText").Value;
                                    if (lineStrRef > 0)
                                    {
                                        //Get StringRef Text
                                        string lineTLKstring = GlobalFindStrRefbyID(lineStrRef, GameBeingDumped,
                                            exp.FileRef);
                                        if (lineTLKstring != "No Data" && lineTLKstring != "\"\"" &&
                                            lineTLKstring != "\" \"")
                                        {
                                            //Write to Background thread (must be 8 strings)
                                            var excelout = new List<string>
                                                {
                                                    "TLKStrings", lineSpeaker, lineStrRef.ToString(), lineTLKstring,
                                                    convName, GameBeingDumped.ToString(), fileName, convIdx.ToString()
                                                };
                                            dumper._xlqueue.Add(excelout);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //Build Table of conversation owner tags
                if (CheckActor)
                {
                    foreach (ExportEntry exp in pcc.Exports)
                    {
                        if (DumpCanceled)
                        {
                            return;
                        }

                        CurrentFileProgressValue = exp.UIndex;

                        string ownertag = "Not found";
                        className = exp.ClassName;
                        if (className == "BioSeqAct_StartConversation" || className == "SFXSeqAct_StartConversation" ||
                            className == "SFXSeqAct_StartAmbientConv")
                        {
                            string convo = "not found"; //Find Conversation 
                            var oconv = exp.GetProperty<ObjectProperty>("Conv");
                            if (oconv != null)
                            {
                                int iconv = oconv.Value;
                                if (iconv < 0)
                                {
                                    convo = pcc.GetImport(iconv).ObjectName.Instanced;
                                }
                                else
                                {
                                    convo = pcc.GetUExport(iconv).ObjectName.Instanced;
                                }
                            }

                            int iownerObj = 0; //Find owner tag in linked actor or variable
                            var links = exp.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
                            if (links != null)
                            {
                                foreach (StructProperty l in links)
                                {
                                    if (l.GetProp<StrProperty>("LinkDesc") == "Owner")
                                    {
                                        var ownerLink = l.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                                        if (ownerLink.Any())
                                        {
                                            iownerObj = ownerLink[0].Value;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (iownerObj > 0)
                            {
                                var svlink = pcc.GetUExport(iownerObj);
                                switch (svlink.ClassName)
                                {
                                    case "SeqVar_Object":
                                        {
                                            ObjectProperty oactorlink = svlink.GetProperty<ObjectProperty>("ObjValue");
                                            if (oactorlink != null)
                                            {
                                                var actor = pcc.GetUExport(oactorlink.Value);
                                                var actortag = actor.GetProperty<NameProperty>("Tag");
                                                if (actortag != null)
                                                {
                                                    ownertag = actortag.ToString();
                                                }
                                                else if (actor.HasArchetype && actor.Archetype is ExportEntry archetype)
                                                {
                                                    var archtag = archetype.GetProperty<NameProperty>("Tag");
                                                    if (archtag != null)
                                                    {
                                                        ownertag = archtag.ToString();
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                    case "BioSeqVar_ObjectFindByTag" when GameBeingDumped.IsGame3():
                                        ownertag = svlink.GetProperty<NameProperty>("m_sObjectTagToFind").ToString();
                                        break;
                                    case "BioSeqVar_ObjectFindByTag":
                                        ownertag = svlink.GetProperty<StrProperty>("m_sObjectTagToFind").ToString();
                                        break;
                                }
                            }

                            //Write to Background thread 
                            var excelout = new List<string> { "ConvoOwners", convo, ownertag, fileName };
                            dumper._xlqueue.Add(excelout);
                        }

                        if (dumper.shouldDoDebugOutput)
                        {
                            string tag = null;
                            int strref = -1;
                            if (GameBeingDumped.IsGame1() && className == "BioPawn")
                            {
                                var tagprop = exp.GetProperty<NameProperty>("Tag");
                                if (tagprop != null)
                                {
                                    tag = tagprop.ToString();
                                }
                                var behav = exp.GetProperty<ObjectProperty>("m_oBehavior");
                                if (behav != null)
                                {
                                    var set = pcc.GetUExport(behav.Value).GetProperty<ObjectProperty>("m_oActorType");
                                    if (set != null)
                                    {
                                        var strrefprop = pcc.GetUExport(set.Value)
                                            .GetProperty<StringRefProperty>("ActorGameNameStrRef");
                                        if (strrefprop != null)

                                        {
                                            strref = strrefprop.Value;
                                        }
                                    }
                                }
                            }
                            else if (GameBeingDumped.IsGame2() && className == "BioPawn")
                            {
                                var tagprop = exp.GetProperty<NameProperty>("Tag");
                                tag = tagprop.ToString();
                                var type = exp.GetProperty<ObjectProperty>("ActorType");
                                var strrefprop = pcc.GetUExport(type.Value)
                                    .GetProperty<StringRefProperty>("ActorGameNameStrRef");
                                if (strrefprop != null)
                                {
                                    strref = strrefprop.Value;
                                }
                            }
                            else if (className == "SFXStuntActor" || className == "SFXPointOfInterest")
                            {
                                var tagprop = exp.GetProperty<NameProperty>("Tag");
                                tag = tagprop.Value;
                                var modules = exp.GetProperty<ArrayProperty<ObjectProperty>>("Modules").ToList();
                                var simplemod = modules.FirstOrDefault(m =>
                                    exp.FileRef.GetUExport(m.Value).ClassName == "SFXSimpleUseModule");
                                strref = exp.FileRef.GetUExport(simplemod.Value)
                                    .GetProperty<StringRefProperty>("m_srGameName").Value;
                            }
                            else if (className.StartsWith("SFXPawn_"))
                            {
                                try
                                {
                                    var tagprop = exp.GetProperty<NameProperty>("Tag");
                                    tag = tagprop.Value;
                                    strref = exp.GetProperty<StringRefProperty>("PrettyName").Value;
                                }
                                catch
                                {
                                    //ignore SFXPawns without prettyname don't add to Debug list
                                }
                            }

                            if (tag != null && strref >= 0)
                            {
                                string actorname = GlobalFindStrRefbyID(strref, GameBeingDumped, exp.FileRef);
                                dumper._xlqueue.Add(new List<string> { "Tags", tag, strref.ToString(), actorname });
                            }

                            if (className == "AnimSequence")
                            {
                                string animname = exp.ObjectName.Instanced;
                                string animpackage = exp.Parent?.ObjectName.Instanced ?? exp.FileRef.FileNameNoExtension; // ME1 has lots of root stuff.
                                var seqName = exp.GetProperty<NameProperty>("SequenceName");
                                float length = exp.GetProperty<FloatProperty>("SequenceLength");
                                dumper._xlqueue.Add(new List<string>
                                    {
                                        "Animations", animname, animpackage, seqName.ToString(), length.ToString(),
                                        fileName, GameBeingDumped.ToString()
                                    });
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (dumper.shouldDoDebugOutput)
                {
                    var excelout = new List<string>
                        { "DEBUG", "FAILURE", fileName, className, CurrentFileProgressValue.ToString(), e.ToString() };
                    dumper._xlqueue.Add(excelout);
                }
            }
            finally
            {
                pcc?.Dispose();
            }

            if (dumper.shouldDoDebugOutput)
            {
                var excelout = new List<string> { "DEBUG", "SUCCESS", fileName };
                dumper._xlqueue.Add(excelout);
            }
        }


        /// <summary>
        /// Dumps Audio into a folder
        /// </summary>
        public void DumpPackageFileAudio(MEGame GameBeingDumped, DialogueDumperWindow dumper)
        {
            string fileName = ShortFileName.ToUpper();
            dumper.CurrentOverallOperationText = $"Dumping Packages.... {dumper.OverallProgressValue}/{dumper.OverallProgressMaximum}";

            //SETUP FILE FILTERS
            var fileLoc = fileName.GetUnrealLocalization();

            string className = null;
            IMEPackage pcc = null;

            try
            {
                var testPcc = MEPackageHandler.QuickOpenMEPackage(File);
                if (testPcc.Game.IsGame1() || dumper.shouldDoDebugOutput)
                {
                    // We need to do a full load so it loads the local TLKS
                    pcc = MEPackageHandler.OpenMEPackage(File);
                }
                else
                {
                    // We can do a partial load which will skip reading a lot of stuff
                    // THIS ONLY WORKS IN RELEASE MODE
                    // because the debug dump stuff would mean i have to load a bunch of stuff so I don't bother.
                    pcc = MEPackageHandler.UnsafePartialLoad(File, x =>
                        x.ClassName is "BioConversation" or "BioTlkFile" or "BioTlkFileSet"
                    );
                }
                if (pcc.Game.IsGame1() && pcc is UnrealPackageFile upf)
                {
                    // Force it to read the proper TLK - it may not be set properly in LEX
                    upf.SetLocalTLKs(upf.ReadLocalTLKs(_selectedLocalization.ToLocaleString(pcc.Game)));
                }
                //using IMEPackage pcc = MEPackageHandler.OpenMEPackage(File);
                if (GameBeingDumped == MEGame.Unknown) //Correct mapping
                {
                    GameBeingDumped = pcc.Game;
                }

                CurrentFileProgressMaximum = pcc.ExportCount;

                //CHECK FOR WAV FILES TO DUMP

                // We do a .ToArray() since we need to account. Count() a few times
                var audioExports = pcc.Exports.Where(e => e.ClassName is "WwiseBank" or "WwiseStream" or "SoundNodeWave").Select(x => new SoundplorerExport(x)).ToArray();
                CurrentFileProgressMaximum = audioExports.Length;

                int doneCount = 0;
                var location = dumper.outputfile;
                foreach (object o in audioExports)
                {
                    if (DumpCanceled)
                    {
                        return;
                    }

                    CurrentFileProgressValue = doneCount++;
                    switch (o)
                    {
                        case SoundplorerExport sp when sp.Export.ClassName == "WwiseStream":
                            {
                                string outfile = Path.Combine(location, sp.Export.ObjectName + ".wav");
                                ExportWave(sp, outfile);
                                break;
                            }
                        case SoundplorerExport sp when sp.Export.ClassName == "WwiseBank":
                            {
                                ExtractBankToWav(sp, location);
                                break;
                            }
                        case SoundplorerExport sp when sp.Export.ClassName == "SoundNodeWave":
                            {
                                ExtractSoundNodeToWav(sp, location);
                                break;
                            }
                        case ISACTFileEntry ife:
                            {
                                string outfile = Path.Combine(location, Path.GetFileNameWithoutExtension(ife.Entry.TitleInfo.Value) + ".wav");
                                MemoryStream ms = AudioStreamHelper.GetWaveStreamFromISBEntry(ife.Entry);
                                System.IO.File.WriteAllBytes(outfile, ms.ToArray());
                                break;
                            }
                        case AFCFileEntry afE:
                            {
                                string presetfilename = $"{Path.GetFileNameWithoutExtension(afE.AFCPath)}_{afE.Offset}.wav";
                                ExportWaveAFC(afE, Path.Combine(location, presetfilename));
                                break;
                            }
                    }
                }
            }
            catch (Exception e)
            {
                if (dumper.shouldDoDebugOutput)
                {
                    var excelout = new List<string>
                        { "DEBUG", "FAILURE", fileName, className, CurrentFileProgressValue.ToString(), e.ToString() };
                    dumper._xlqueue.Add(excelout);
                }
            }
            finally
            {
                pcc?.Dispose();
            }
        }

        private void ExportWave(SoundplorerExport spExport, string outputLocation)
        {
            if (spExport != null && spExport.Export.ClassName == "WwiseStream")
            {
                bool silent = outputLocation != null;
              
                WwiseStream w = spExport.Export.GetBinaryData<WwiseStream>();
                Stream source = w.CreateWaveStream();
                if (source != null)
                {
                    using (var fileStream = System.IO.File.Create(outputLocation))
                    {
                        source.Seek(0, SeekOrigin.Begin);
                        source.CopyTo(fileStream);
                    }
                    if (!silent)
                    {
                        MessageBox.Show("Done.");
                    }
                }
                else
                {
                    if (!silent)
                    {
                        MessageBox.Show("Error creating Wave file.\nThis might not be a supported codec or the AFC data may be incorrect.");
                    }
                }
            }
        }

        private void ExtractBankToWav(SoundplorerExport spExport, string location)
        {
            if (spExport != null && spExport.Export.ClassName == "WwiseBank")
            {
                var bank = spExport.Export.GetBinaryData<WwiseBankParsed>();
                if (bank.EmbeddedFiles.Count > 0)
                {
                    foreach ((uint wemID, byte[] wemData) in bank.EmbeddedFiles)
                    {
                        string wemHeader = "" + (char)wemData[0] + (char)wemData[1] + (char)wemData[2] + (char)wemData[3];
                        string wemName = $"{spExport.Export.ObjectName}_0x{wemID:X8}";
                        if (wemHeader == "RIFF" || wemHeader == "RIFX")
                        {
                            var wem = new EmbeddedWEMFile(wemData, wemName, spExport.Export); //will correct truncated stuff
                            Stream waveStream = GetPCMStream(forcedWemFile: wem);
                            if (waveStream != null && waveStream.Length > 0)
                            {
                                string outputname = wemName + ".wav";
                                string outpath = Path.Combine(location, outputname);
                                waveStream.SeekBegin();
                                using var fileStream = System.IO.File.Create(outpath);
                                waveStream.CopyTo(fileStream);
                            }
                        }
                    }
                }
            }
        }

        private void ExtractSoundNodeToWav(SoundplorerExport spExport, string location)
        {
            if (spExport != null && spExport.Export.ClassName == "SoundNodeWave")
            {
                var soundNodeWave = spExport.Export.GetBinaryData<SoundNodeWave>();
                if (soundNodeWave.RawData.Length > 0)
                {
                    ISACTBankPair ibp = ISACTHelper.GetPairedBanks(soundNodeWave.RawData);
                    foreach (var isbC in ibp.ISBBank.GetAllBankChunks().Where(x => x.ChunkName == "data"))
                    {
                        var objectParent = isbC.GetParent();
                        if (objectParent != null && objectParent is ISACTListBankChunk bankEntry)
                        {
                            string isbName = null;
                            try
                            {
                                // This is to prevent error if malformed names
                                isbName = spExport.Export.ObjectName.Instanced.Substring(0, spExport.Export.ObjectName.Instanced.IndexOf(":"));
                            }
                            catch
                            {
                            }
                            Stream waveStream = AudioStreamHelper.GetWaveStreamFromISBEntry(bankEntry, isbName: isbName, game: spExport.Export.Game);
                            if (waveStream != null && waveStream.Length > 0)
                            {
                                waveStream.WriteToFile(Path.Combine(location, spExport.Export.ObjectName.Instanced.Replace(':', '_') + ".wav"));
                            }
                        }
                    }
                } else
                {
                    var bsd = spExport.Export.GetProperty<ObjectProperty>("BioStreamingData");
                    if (bsd == null)
                    {
                        //ExportInformationList.Add("This export contains no embedded audio");
                        return;
                    }

                    // Imports are very unreliable here and will be slow to load
                    if (bsd.ResolveToEntry(spExport.Export.FileRef) is ExportEntry streamingData)
                    {
                        // Remove the ISB: prefix
                        var indexEntryName = spExport.Export.ObjectName.Instanced.Substring(spExport.Export.ObjectName.Instanced.IndexOf(":") + 1);
                        ISACTBankPair ibp = ISACTHelper.GetPairedBanks(streamingData.GetBinaryData().Skip(4).ToArray());
                        IndexEntry foundICBInfo = null;
                        if (ibp.ICBBank.GetAllBankChunks().FirstOrDefault(x => x.ChunkName == ContentIndexBankChunk.FixedChunkTitle) is ContentIndexBankChunk contentIndex)
                        {
                            // Find info about sample in ICB so we can get entry in ISB
                            foreach (var p in contentIndex.IndexPages)
                            {
                                if (foundICBInfo != null)
                                    break;
                                foreach (var indexEntry in p.IndexEntries)
                                {
                                    if (indexEntry.Title == indexEntryName)
                                    {
                                        foundICBInfo = indexEntry;
                                        break;
                                    }
                                }
                            }
                        }

                        if (foundICBInfo == null)
                        {
                            //ExportInformationList.Add("Could not find information about this sound in the streaming data ICB");
                            return;
                        }

                        var referencedSndeChunk = ibp.ICBBank.GetAllBankChunks().OfType<ISACTListBankChunk>().FirstOrDefault(x => x.GetAllSubChunks().Any(a => a is IntBankChunk { ChunkName: "indx" } ac && ac.Value == foundICBInfo.ObjectIndex));
                        if (referencedSndeChunk == null)
                        {
                            //ExportInformationList.Add("Could not find snde chunk about this sound in the streaming data ICB");
                            return;
                        }

                        var soundTracks = referencedSndeChunk.GetAllSubChunks().OfType<SoundEventSoundTracksFour>().FirstOrDefault()?.SoundTracks;
                        soundTracks ??= referencedSndeChunk.GetAllSubChunks().OfType<SoundEventSoundTracks>().FirstOrDefault()?.SoundTracks; // ISACT generated soundtracks
                        if (soundTracks == null)
                        {
                            //ExportInformationList.Add("Could not find sound track about this sound in the streaming data ICB");
                            return;
                        }

                        foreach (var soundTrack in soundTracks)
                        {
                            var isbIndex = soundTrack.BufferIndex & 0xFFFF;
                            var sampChunk = ibp.ISBBank.GetAllBankChunks().OfType<ISACTListBankChunk>().FirstOrDefault(x => x.ObjectType == "samp" && x.GetAllSubChunks().Any(a => a is IntBankChunk { ChunkName: "indx" } ac && ac.Value == isbIndex));
                            if (sampChunk == null)
                            {
                                //ExportInformationList.Add($"Could not find samp resource index {isbIndex} in streaming ISB referenced by ICB");
                                continue;
                            }

                            if (sampChunk.SampleOffset != null)
                            {

                                
                                string isbName = null;
                                try
                                {
                                    // This is to prevent error if malformed names
                                    isbName = spExport.Export.ObjectName.Instanced.Substring(0, spExport.Export.ObjectName.Instanced.IndexOf(":"));
                                }
                                catch
                                {
                                }
                                Stream waveStream = AudioStreamHelper.GetWaveStreamFromISBEntry(sampChunk, isbName: isbName, game: spExport.Export.Game);
                                if (waveStream != null && waveStream.Length > 0)
                                {
                                    waveStream.WriteToFile(Path.Combine(location, spExport.Export.ObjectName.Instanced.Replace(':', '_') + ".wav"));
                                }
                            }
                            else
                            {
                                //ExportInformationList.Add("The ISB data for this entry does not list an external ISB offset for some reason");
                            }
                        }
                    }
                    else
                    {
                        //ExportInformationList.Add("Audio data can only load in the toolset if the streaming data is an export");
                    }
                }
            }
        }


        /// <summary>
        /// Gets a PCM stream of data (WAV) from either the currently loaded export or selected WEM
        /// </summary>
        /// <param name="forcedWemFile">WEM that we will force to get a stream for</param>
        /// <returns></returns>
        public Stream GetPCMStream(ExportEntry forcedWwiseStreamExport = null, EmbeddedWEMFile forcedWemFile = null)
        {
            ExportEntry localCurrentExport = forcedWwiseStreamExport;
            WwiseStream wwiseStream = null;
            if (localCurrentExport != null || forcedWemFile != null)
            {
                if (localCurrentExport?.ClassName == "WwiseStream")
                {
                    wwiseStream = localCurrentExport.GetBinaryData<WwiseStream>();

                    if (wwiseStream.IsPCCStored || wwiseStream.GetPathToAFC() != "")
                    {
                        return wwiseStream.CreateWaveStream();
                    }
                }
                else if (forcedWemFile != null || (localCurrentExport?.ClassName == "WwiseBank"))
                {
                    object currentWEMItem = forcedWemFile;
                    if (currentWEMItem == null || currentWEMItem is string)
                    {
                        return null; //nothing selected, or current wem is not playable
                    }

                    var wemObject = (EmbeddedWEMFile)currentWEMItem;
                    string basePath = $"{Path.GetTempPath()}ME3EXP_SOUND_{Guid.NewGuid()}";
                    var outpath = basePath + ".wem";
                    System.IO.File.WriteAllBytes(outpath, wemObject.WemData);
                    return AudioStreamHelper.ConvertRIFFToWaveVGMStream(outpath); //use vgmstream
                }
            }

            return null;
        }

        private void ExportWaveAFC(AFCFileEntry afE, string location = null)
        {
            using (Stream s = AudioStreamHelper.CreateWaveStreamFromRaw(afE.AFCPath, afE.Offset, afE.DataSize, afE.ME2))
            {
                using (var fileStream = System.IO.File.Create(location))
                {
                    s.Seek(0, SeekOrigin.Begin);
                    s.CopyTo(fileStream);
                }
            }
        }
    }
}
