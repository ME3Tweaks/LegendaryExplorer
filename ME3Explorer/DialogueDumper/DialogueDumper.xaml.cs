/**
 * Package Dumper is based on ME3Tweaks Mass Effect 3 Mod Manager Command Line Tools
 * TransplanterLib. This is a modified version provided by Mgamerz
 * (c) Mgamerz 2019
 */

using ClosedXML.Excel;
using Gammtek.Conduit.Extensions.IO;
using KFreonLib.MEDirectories;
using ME3Explorer;
using ME3Explorer.ME1.Unreal.UnhoodBytecode;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Input;
using Unreal.ME3Structs;

namespace ME3Explorer.DialogueDumper
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class DialogueDumper : NotifyPropertyChangedWindowBase
    {
        /// <summary>
        /// Items show in the list that are currently being processed
        /// </summary>
        public ObservableCollectionExtended<DialogueDumperSingleFileTask> CurrentDumpingItems { get; set; } = new ObservableCollectionExtended<DialogueDumperSingleFileTask>();

        /// <summary>
        /// All items in the queue
        /// </summary>
        private List<DialogueDumperSingleFileTask> AllDumpingItems;

        private int _listViewHeight;
        public int ListViewHeight
        {
            get => _listViewHeight;
            set => SetProperty(ref _listViewHeight, value);
        }

        private void LoadCommands()
        {
            // Player commands
            DumpME1Command = new RelayCommand(DumpGameME1, CanDumpGameME1);
            DumpME2Command = new RelayCommand(DumpGameME2, CanDumpGameME2);
            DumpME3Command = new RelayCommand(DumpGameME3, CanDumpGameME3);
            DumpSpecificFilesCommand = new RelayCommand(DumpSpecificFiles, CanDumpSpecificFiles);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
        }

        private async void DumpSpecificFiles(object obj)
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog
            {
                Multiselect = true,
                EnsureFileExists = true,
                Title = "Select files to dump",
            };
            dlg.Filters.Add(new CommonFileDialogFilter("All supported files", "*.pcc;*.sfm;*.u;*.upk"));
            dlg.Filters.Add(new CommonFileDialogFilter("Mass Effect package files", "*.sfm;*.u;*.upk"));
            dlg.Filters.Add(new CommonFileDialogFilter("Mass Effect 2/3 package files", "*.pcc"));


            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {

                CommonSaveFileDialog outputDlg = new CommonSaveFileDialog
                {
                    Title = "Select excel output",
                    DefaultFileName = "DialogueDump.xlsx",
                    DefaultExtension = "xlsx",
                };
                outputDlg.Filters.Add(new CommonFileDialogFilter("Excel Files (.xlsx)", "*.xlsx"));

                if (outputDlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outputDir = outputDlg.FileName;
                    await dumpPackages(dlg.FileNames.ToList(), outputDlg.FileName);
                }
            }
        }

        /// <summary>
        /// Allow cancelation of dumping
        /// </summary>
        private bool DumpCanceled;

        #region commands
        public ICommand DumpME1Command { get; set; }
        public ICommand DumpME2Command { get; set; }
        public ICommand DumpME3Command { get; set; }
        public ICommand DumpSpecificFilesCommand { get; set; }
        public ICommand CancelDumpCommand { get; set; }

        private int _overallProgressValue;
        public int OverallProgressValue
        {
            get => _overallProgressValue;
            set => SetProperty(ref _overallProgressValue, value);
        }

        private ActionBlock<DialogueDumperSingleFileTask> ProcessingQueue;
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

        private bool CanDumpGameME1(object obj)
        {
            return ME1Directory.gamePath != null && Directory.Exists(ME1Directory.gamePath) && (ProcessingQueue == null || ProcessingQueue.Completion.Status != TaskStatus.WaitingForActivation);
        }

        private bool CanDumpGameME2(object obj)
        {
            return ME2Directory.gamePath != null && Directory.Exists(ME2Directory.gamePath) && (ProcessingQueue == null || ProcessingQueue.Completion.Status != TaskStatus.WaitingForActivation);
        }

        private bool CanDumpGameME3(object obj)
        {
            return ME3Directory.gamePath != null && Directory.Exists(ME3Directory.gamePath) && (ProcessingQueue == null || ProcessingQueue.Completion.Status != TaskStatus.WaitingForActivation);
        }

        private bool CanCancelDump(object obj)
        {
            return ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation && !DumpCanceled;
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

        private void DumpGameME1(object obj)
        {
            DumpGame(MEGame.ME1);
        }

        private void DumpGameME2(object obj)
        {
            DumpGame(MEGame.ME2);
        }

        private void DumpGameME3(object obj)
        {
            DumpGame(MEGame.ME3);
        }

        #endregion

        public DialogueDumper(Window owner = null)
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Package Dumper", new WeakReference(this));
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
            set
            {
                verbose = value;
            }
        }

        private void DumpGame(MEGame game)
        {
            string rootPath = null;
            switch (game)
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
            CommonSaveFileDialog m = new CommonSaveFileDialog
            {
                Title = "Select excel output",
                DefaultFileName = "DialogueDump.xlsx",
                DefaultExtension = "xlsx",
            };
            m.Filters.Add(new CommonFileDialogFilter("Excel Files (.xlsx)", "*.xlsx"));

            if (m.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string outputFile = m.FileName;
                dumpPackagesFromFolder(rootPath, outputFile);
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
                var exceptionMessage = ExceptionHandlerDialogWPF.FlattenException(ex);
                Debug.WriteLine(exceptionMessage);
            }

            //throw new NotImplementedException();
        }


        /// <summary>
        /// Dumps PCC data from all PCCs in the specified folder, recursively.
        /// </summary>
        /// <param name="path">Base path to start dumping functions from. Will search all subdirectories for package files.</param>
        /// <param name="args">Set of arguments for what to dump. In order: imports, exports, data, scripts, coalesced, names. At least 1 of these options must be true.</param>
        /// <param name="outputfile">Output Excel document.</param>
        public async void dumpPackagesFromFolder(string path, string outputfile = null)
        {
            path = Path.GetFullPath(path);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc" };
            List<string> files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Where(s => supportedExtensions.Contains(Path.GetExtension(s.ToLower()))).ToList();
            await dumpPackages(files, outputfile);
        }

        private async Task dumpPackages(List<string> files, string outputfile = null)
        {
            CurrentOverallOperationText = "Dumping packages...";
            OverallProgressMaximum = files.Count;
            OverallProgressValue = 0;

            var workbook = new XLWorkbook();
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

            ProcessingQueue = new ActionBlock<DialogueDumperSingleFileTask>(x =>
            {
                if (x.DumpCanceled) { OverallProgressValue++; return; }
                Application.Current.Dispatcher.Invoke(new Action(() => CurrentDumpingItems.Add(x)));
                x.dumpPackageFile(workbook); // What to do on each item
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    OverallProgressValue++; //Concurrency
                    CurrentDumpingItems.Remove(x);
                }));
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = App.CoreCount }); // How many items at the same time

            AllDumpingItems = new List<DialogueDumperSingleFileTask>();
            CurrentDumpingItems.ClearEx();
            foreach (var item in files)
            {
                string outfolder = outputfile;
                if (outfolder != null)
                {
                    string relative = GetRelativePath(Path.GetFullPath(item), Directory.GetParent(item).ToString());
                    outfolder = Path.Combine(outfolder, relative);
                }

                var threadtask = new DialogueDumperSingleFileTask(item, outfolder);
                AllDumpingItems.Add(threadtask); //For setting cancelation value
                ProcessingQueue.Post(threadtask); // Post all items to the block
            }

            ProcessingQueue.Complete(); // Signal completion
            CommandManager.InvalidateRequerySuggested();
            await ProcessingQueue.Completion; // Asynchronously wait for completion.        }

            workbook.SaveAs(outputfile);

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
            CommandManager.InvalidateRequerySuggested();
        }


        /// <summary>
        /// Writes a line to the console if verbose mode is turned on
        /// </summary>
        /// <param name="message">Verbose message to write</param>
        public void writeVerboseLine(String message)
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
        public string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                throw new ArgumentNullException(nameof(fromPath));
            }

            if (string.IsNullOrEmpty(toPath))
            {
                throw new ArgumentNullException(nameof(toPath));
            }

            Uri fromUri = new Uri(AppendDirectorySeparatorChar(fromPath));
            Uri toUri = new Uri(AppendDirectorySeparatorChar(toPath));

            if (fromUri.Scheme != toUri.Scheme)
            {
                return toPath;
            }

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            // Append a slash only if the path is a directory and does not have a slash.
            if (!System.IO.Path.HasExtension(path) &&
                !path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
            {
                return path + System.IO.Path.DirectorySeparatorChar;
            }

            return path;
        }

        private void DialogueDumper_Closing(object sender, CancelEventArgs e)
        {
            DumpCanceled = true;
            if (AllDumpingItems != null)
            {
                AllDumpingItems.ForEach(x => x.DumpCanceled = true);
            }
        }

        private void DialogueDumper_Loaded(object sender, RoutedEventArgs e)
        {
            Owner = null; //Detach from parent
        }

        private async void DialogueDumper_FilesDropped(object sender, DragEventArgs e)
        {
            if (ProcessingQueue != null && ProcessingQueue.Completion.Status == TaskStatus.WaitingForActivation) { return; } //Busy

            string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filenames.Length == 1 && Directory.Exists(filenames[0]))
            {
                //Directory - can drop
                dumpPackagesFromFolder(filenames[0]);
            }
            else
            {
                await dumpPackages(filenames.ToList());
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
                        string extension = System.IO.Path.GetExtension(filename).ToLower();
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

        public DialogueDumperSingleFileTask(string file, string outputfolder = null)
        {
            this.File = file;
            this.ShortFileName = Path.GetFileNameWithoutExtension(file);
            this.OutputFolder = outputfolder;
            CurrentOverallOperationText = "Dumping " + ShortFileName;
        }

        public bool DumpCanceled;
        private string File;
        private string OutputFolder;

        /// <summary>
        /// Dumps Conversation strings to xl worksheet
        /// </summary>
        /// <worksheet>Output excel worksheet</worksheet>
        public void dumpPackageFile(XLWorkbook workbook)
        {
            var xlstrings = workbook.Worksheet(1);
            var xlowners = workbook.Worksheet(2);

            using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(File))
            {
                string fileName = Path.GetFileNameWithoutExtension(File);
                var GameBeingDumped = pcc.Game;
                if ((fileName.EndsWith(@"LOC_INT")) || GameBeingDumped == MEGame.ME1)
                {
                    int numDone = 1;
                    int numTotal = pcc.Exports.Count;

                    foreach (IExportEntry exp in pcc.Exports)
                    {
                        if (DumpCanceled)
                        {
                            return;
                        }

                        CurrentFileProgressValue = exp.UIndex;

                        string className = exp.ClassName;
                        if (className == "BioConversation")
                        {
                            string convName = exp.ObjectName;
                            
                            int convIdx = exp.UIndex;

                            try
                            {
                                var convo = exp.GetProperties();
                                if (convo.Count > 0)
                                {
                                    //1.  Define speaker list "m_aSpeakerList"
                                    List<string> speakers = new List<string>();
                                    var a_speakers = exp.GetProperty<ArrayProperty<NameProperty>>("m_aSpeakerList");                                     //
                                    if (a_speakers != null)
                                    {
                                        foreach (NameProperty n in a_speakers)
                                        {
                                            speakers.Add(n.ToString());
                                        }
                                    }

                                    //2. Go through Entry list "m_EntryList"
                                    // Parse line TLK StrRef, TLK Line, Speaker -1 = Owner, -2 = Shepard, or from m_aSpeakerList

                                    var entryList = exp.GetProperty<ArrayProperty<StructProperty>>("m_EntryList");
                                    foreach (StructProperty entry in entryList)
                                    {
                                        //Get and set speaker name
                                        var speakeridx = entry.GetProp<IntProperty>("nSpeakerIndex");
                                        string lineSpeaker = null;
                                        if (speakeridx >= 0)
                                        {
                                            lineSpeaker = speakers[speakeridx].ToString();
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
                                            string lineTLKstring = ME3TalkFiles.findDataById(lineStrRef);

                                            if (lineTLKstring != "No Data")
                                            {
                                                int nextrow = xlstrings.LastRowUsed().RowNumber() + 1;
                                                //Write output to excel
                                                xlstrings.Cell(nextrow, 1).Value = lineSpeaker;
                                                xlstrings.Cell(nextrow, 2).Value = lineStrRef;
                                                xlstrings.Cell(nextrow, 3).Value = lineTLKstring;
                                                xlstrings.Cell(nextrow, 4).Value = convName;
                                                xlstrings.Cell(nextrow, 5).Value = GameBeingDumped;
                                                xlstrings.Cell(nextrow, 6).Value = fileName;
                                                xlstrings.Cell(nextrow, 7).Value = convIdx;
                                            }
                                        }
                                    }
                                    //3. Go through Reply list "m_ReplyList"
                                    // Parse line TLK StrRef, TLK Line, Speaker always Shepard

                                    var replyList = exp.GetProperty<ArrayProperty<StructProperty>>("m_ReplyList");
                                    foreach (StructProperty reply in replyList)
                                    {
                                        //Get and set speaker name
                                        string lineSpeaker = "Shepard";

                                        //Get StringRef
                                        var lineStrRef = reply.GetProp<StringRefProperty>("srText").Value;
                                        if (lineStrRef > 0)
                                        {

                                            //Get StringRef Text
                                            string lineTLKstring = ME3TalkFiles.findDataById(lineStrRef);

                                            if (lineTLKstring != "No Data")
                                            {
                                                int nextrow = xlstrings.LastRowUsed().RowNumber() + 1;
                                                xlstrings.Cell(nextrow, 1).Value = lineSpeaker;
                                                xlstrings.Cell(nextrow, 2).Value = lineStrRef;
                                                xlstrings.Cell(nextrow, 3).Value = lineTLKstring;
                                                xlstrings.Cell(nextrow, 4).Value = convName;
                                                xlstrings.Cell(nextrow, 5).Value = GameBeingDumped;
                                                xlstrings.Cell(nextrow, 6).Value = fileName;
                                                xlstrings.Cell(nextrow, 7).Value = convIdx;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine(exp.UIndex);
                            }
                        }
                    }
                    numDone++;
                }

                if ( !fileName.EndsWith(@"LOC_INT")) //Build Table of conversation owner tags
                {
                    string ownertag = null;
                    foreach (IExportEntry exp in pcc.Exports)
                    {
                        int numDone = 1;
                        int numTotal = pcc.Exports.Count;
                        string Class = exp.ClassName;
                        if (Class == "BioSeqAct_Conversation" || Class == "SFXSeqAct_StartConversation" || Class == "SFXSeqAct_StartAmbientConv")
                        {
                            try
                            {
                                string convo = "not found";  //Find Conversation
                                var iconv = exp.GetProperty<ObjectProperty>("Conv").Value;
                                if(iconv < 0)
                                {
                                    convo = pcc.getUImport(iconv).ObjectName;
                                }
                                else
                                {
                                    convo = pcc.getUExport(iconv).ObjectName;
                                }

                                int iownerObj = 0;
                                var links = exp.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
                                foreach(StructProperty l in links )
                                {
                                    if(l.GetProp<StrProperty>("LinkDesc") == "Owner")
                                    {
                                        var ownerLink = l.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                                        iownerObj = ownerLink[0].Value;
                                        break;
                                    }
                                }

                                var svlink = pcc.getUExport(iownerObj);
                                if(svlink.ClassName == "SeqVar_Object")
                                {
                                    var iactorlink = svlink.GetProperty<ObjectProperty>("ObjValue").Value;
                                    var actorlink = pcc.getUExport(iactorlink);
                                    ownertag = actorlink.GetProperty<NameProperty>("Tag").ToString();
                                }
                                else if (svlink.ClassName == "BioSeqVar_ObjectFindByTag")
                                {
                                    ownertag = svlink.GetProperty<NameProperty>("m_sObjectTagToFind").ToString();
                                }

                                int nextrow = xlowners.LastRowUsed().RowNumber() + 1;
                                xlowners.Cell(nextrow, 1).Value = convo;
                                xlowners.Cell(nextrow, 2).Value = ownertag;
                                xlowners.Cell(nextrow, 3).Value = fileName;
                            }
                            catch
                            {

                            }
                        }
                        numDone++;
                    }
                    
                }
            }
        }
    }
}
