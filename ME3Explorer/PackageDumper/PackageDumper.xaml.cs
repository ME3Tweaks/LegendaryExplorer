/**
 * Package Dumper is based on ME3Tweaks Mass Effect 3 Mod Manager Command Line Tools
 * TransplanterLib. This is a modified version provided by Mgamerz
 * (c) Mgamerz 2019
 */

using Gammtek.Conduit.Extensions.IO;
using KFreonLib.MEDirectories;
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
using System.Windows;
using System.Windows.Input;

namespace ME3Explorer.PackageDumper
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PackageDumper : WPFBase
    {
        MEGame GameBeingDumped;
        BackgroundWorker DumpWorker;
        private void LoadCommands()
        {
            // Player commands
            DumpME1Command = new RelayCommand(DumpGameME1, CanDumpGameME1);
            DumpME2Command = new RelayCommand(DumpGameME2, CanDumpGameME2);
            DumpME3Command = new RelayCommand(DumpGameME3, CanDumpGameME3);
            CancelDumpCommand = new RelayCommand(CancelDump, CanCancelDump);
        }

        /// <summary>
        /// Allow cancelation of dumping
        /// </summary>
        private bool DumpCanceled;

        #region commands
        public ICommand DumpME1Command { get; set; }
        public ICommand DumpME2Command { get; set; }
        public ICommand DumpME3Command { get; set; }
        public ICommand CancelDumpCommand { get; set; }

        private int _overallProgressValue;
        public int OverallProgressValue
        {
            get { return _overallProgressValue; }
            set
            {
                if (value != _overallProgressValue)
                {
                    _overallProgressValue = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _overallProgressMaximum;
        public int OverallProgressMaximum
        {
            get { return _overallProgressMaximum; }
            set
            {
                if (value != _overallProgressMaximum)
                {
                    _overallProgressMaximum = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _currentFileProgressValue;
        public int CurrentFileProgressValue
        {
            get { return _currentFileProgressValue; }
            set
            {
                if (value != _currentFileProgressValue)
                {
                    _currentFileProgressValue = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _currentFileProgressMaximum;
        public int CurrentFileProgressMaximum
        {
            get { return _currentFileProgressMaximum; }
            set
            {
                if (value != _currentFileProgressMaximum)
                {
                    _currentFileProgressMaximum = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _currentOverallOperationText;
        public string CurrentOverallOperationText
        {
            get { return _currentOverallOperationText; }
            set
            {
                if (value != _currentOverallOperationText)
                {
                    _currentOverallOperationText = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool CanDumpGameME1(object obj)
        {
            return ME1Directory.gamePath != null && Directory.Exists(ME1Directory.gamePath) && (DumpWorker == null || !DumpWorker.IsBusy);
        }

        private bool CanDumpGameME2(object obj)
        {
            return ME2Directory.gamePath != null && Directory.Exists(ME2Directory.gamePath) && (DumpWorker == null || !DumpWorker.IsBusy);
        }

        private bool CanDumpGameME3(object obj)
        {
            return ME3Directory.gamePath != null && Directory.Exists(ME3Directory.gamePath) && (DumpWorker == null || !DumpWorker.IsBusy);
        }

        private bool CanCancelDump(object obj)
        {
            return DumpWorker != null && DumpWorker.IsBusy && !DumpCanceled;
        }

        private void CancelDump(object obj)
        {
            DumpCanceled = true;
            CommandManager.InvalidateRequerySuggested(); //Refresh commands
        }

        private void DumpGameME1(object obj)
        {
            GameBeingDumped = MEGame.ME1;
            DumpGame(1);
        }

        private void DumpGameME2(object obj)
        {
            GameBeingDumped = MEGame.ME2;
            DumpGame(2);
        }

        private void DumpGameME3(object obj)
        {
            GameBeingDumped = MEGame.ME3;
            DumpGame(3);
        }

        #endregion

        public PackageDumper(Window owner = null)
        {
            Owner = owner;
            DataContext = this;
            LoadCommands();
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

        private void DumpGame(int game)
        {
            string rootPath = null;
            switch (game)
            {
                case 1:
                    rootPath = ME1Directory.gamePath;
                    break;
                case 2:
                    rootPath = ME2Directory.gamePath;
                    break;
                case 3:
                    rootPath = ME3Directory.gamePath;
                    break;
            }
            CommonOpenFileDialog m = new CommonOpenFileDialog();
            m.IsFolderPicker = true;
            m.EnsurePathExists = true;
            m.Title = "Select output folder";
            if (m.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string outputDir = m.FileName;
                DumpWorker = new BackgroundWorker();
                DumpWorker.DoWork += Dump_BackgroundThread;
                DumpWorker.RunWorkerCompleted += Dump_Completed;
                DumpWorker.RunWorkerAsync(argument: new Tuple<string, string>(rootPath, outputDir));
            }
        }

        private void Dump_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            var args = (Tuple<string, string>)e.Argument;
            dumpPackagesFromFolder(args.Item1, args.Item2);
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
            if (DumpCanceled)
            {
                DumpCanceled = false;
                CurrentFileProgressValue = 0;
                OverallProgressMaximum = 100;
                CurrentOverallOperationText = "Dump canceled";
            }
            else
            {
                OverallProgressValue = 100;
                OverallProgressMaximum = 100;
                CurrentOverallOperationText = "Dump completed";
            }
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Formats arguments as a string
        /// </summary>
        /// <param name="filename">EXE file</param>
        /// <param name="arguments">EXE arguments</param>
        /// <returns></returns>
        public string Format(string filename, string arguments)
        {
            return "'" + filename +
                ((string.IsNullOrEmpty(arguments)) ? string.Empty : " " + arguments) +
                "'";
        }

        /// <summary>
        /// Dumps PCC data from all PCCs in the specified folder, recursively.
        /// </summary>
        /// <param name="path">Base path to start dumping functions from. Will search all subdirectories for package files.</param>
        /// <param name="args">Set of arguments for what to dump. In order: imports, exports, data, scripts, coalesced, names. At least 1 of these options must be true.</param>
        /// <param name="outputfolder">Output path to place files in. If null, it will use the same folder as the currently processing PCC. Files will be placed relative to the base path.</param>
        public void dumpPackagesFromFolder(string path, string outputfolder = null)
        {
            path = Path.GetFullPath(path);
            var supportedExtensions = new List<string> { ".u", ".upk", ".sfm", ".pcc" };
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Where(s => supportedExtensions.Contains(Path.GetExtension(s))).ToList();
            //string[] files = Directory.GetFiles(path, "*.pcc", SearchOption.AllDirectories);
            bool beginParsing = false;
            OverallProgressMaximum = files.Count;
            for (int i = 0; i < files.Count; i++)
            {
                if (!DumpCanceled)
                {
                    string file = Path.GetFullPath(files[i]);
                    if (file.EndsWith("SFXGame.pcc") || beginParsing)
                    {
                    beginParsing = true;
                        string outfolder = outputfolder;
                        if (outfolder != null)
                        {
                            string relative = GetRelativePath(path, Directory.GetParent(file).ToString());
                            outfolder = Path.Combine(outfolder, relative);
                        }
                        CurrentOverallOperationText = "Dumping " + Path.GetFileNameWithoutExtension(file);

                        Debug.WriteLine("[" + (i + 1) + "/" + files.Count + "] Dumping " + Path.GetFileNameWithoutExtension(file));
                        dumpPCCFile(file, outfolder);
                        OverallProgressValue = i;
                    }
                }
            }
        }

        /// <summary>
        /// Dumps data from a pcc file to a text file
        /// </summary>
        /// <param name="file">PCC file path to dump from</param>
        /// <param name="args">6 element bool array, specifying what should be dumped. In order: imports, exports, data, scripts, coalesced, names. At least 1 of these options must be true.</param>
        /// <param name="outputfolder"></param>
        public void dumpPCCFile(string file, string outputfolder = null)
        {
            //if (GamePath == null)
            //{
            //    Console.Error.WriteLine("Game path not defined. Can't dump file file with undefined game System.IO.Path.");
            //    return;
            //}
            //try
            {
                IMEPackage pcc = MEPackageHandler.OpenMEPackage(file);
                CurrentFileProgressMaximum = pcc.ExportCount;
                string outfolder = outputfolder;
                if (outfolder == null)
                {
                    outfolder = Directory.GetParent(file).ToString();
                }

                if (!outfolder.EndsWith(@"\"))
                {
                    outfolder += @"\";
                }

                //if (properties)
                //{
                //    UnrealObjectInfo.loadfromJSON();
                //}
                StreamWriter stringoutput = StreamWriter.Null;
                //dumps data.
                string savepath = outfolder + System.IO.Path.GetFileNameWithoutExtension(file) + ".txt";
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(savepath));
                stringoutput = new StreamWriter(savepath);

                using (stringoutput)
                {

                    //if (imports)
                    //{
                    //writeVerboseLine("Getting Imports");
                    stringoutput.WriteLine("--Imports");
                    for (int x = 0; x < pcc.Imports.Count; x++)
                    {
                        ImportEntry imp = pcc.Imports[x];
                        if (imp.PackageFullName != "Class" && imp.PackageFullName != "Package")
                        {
                            stringoutput.WriteLine("#" + ((x + 1) * -1) + ": " + imp.PackageFullName + "." + imp.ObjectName + "(From: " + imp.PackageFile + ") " +
                                "(Offset: 0x " + (pcc.ImportOffset + (x * ImportEntry.byteSize)).ToString("X4") + ")");
                        }
                        else
                        {
                            stringoutput.WriteLine("#" + ((x + 1) * -1) + ": " + imp.ObjectName + "(From: " + imp.PackageFile + ") " +
                                "(Offset: 0x " + (pcc.ImportOffset + (x * ImportEntry.byteSize)).ToString("X4") + ")");
                        }
                    }

                    stringoutput.WriteLine("--End of Imports");
                    //}

                    string datasets = "Exports Coalesced ";
                    if (GameBeingDumped != MEGame.ME2)
                    {
                        datasets += " Functions";
                    }

                    stringoutput.WriteLine("--Start of " + datasets);
                    stringoutput.WriteLine("Exports starting with [C] can be overriden from the configuration file");

                    int numDone = 1;
                    int numTotal = pcc.Exports.Count;
                    int lastProgress = 0;
                    //writeVerboseLine("Enumerating exports");
                    string swfoutfolder = outfolder + System.IO.Path.GetFileNameWithoutExtension(file) + "\\";
                    foreach (IExportEntry exp in pcc.Exports)
                    {
                        if (DumpCanceled)
                        {
                            pcc.Release();
                            return;
                        }
                        //writeVerboseLine("Parse export #" + index);
                        CurrentFileProgressValue = exp.UIndex;
                        //bool isCoalesced = coalesced && exp.likelyCoalescedVal;
                        String className = exp.ClassName;
                        bool isCoalesced = exp.ReadsFromConfig;
                        bool isScript = (className == "Function");
                        //int progress = ((int)(((double)numDone / numTotal) * 100));
                        //while (progress >= (lastProgress + 10))
                        //{
                        //    Console.Write("..." + (lastProgress + 10) + "%");
                        //    //needsFlush = true;
                        //    lastProgress += 10;
                        //}

                        stringoutput.WriteLine("=======================================================================");
                        stringoutput.Write("#" + exp.UIndex + " ");
                        if (isCoalesced)
                        {
                            stringoutput.Write("[C] ");
                        }

                        stringoutput.Write(exp.GetFullPath + "(" + exp.ClassName + ")");
                        int ival = exp.indexValue;
                        if (ival > 0)
                        {
                            stringoutput.Write(" (Index: " + ival + ") ");

                        }
                        stringoutput.WriteLine("(Superclass: " + exp.ClassParent + ") (Data Offset: 0x " + exp.DataOffset.ToString("X5") + ")");

                        if (isScript)
                        {
                            stringoutput.WriteLine("==============Function==============");
                            switch (GameBeingDumped)
                            {
                                case MEGame.ME1:
                                    stringoutput.WriteLine(UE3FunctionReader.ReadFunction(exp));
                                    break;
                            }
                            //Function func = new Function(exp.Data, pcc);
                            //stringoutput.WriteLine(func.ToRawText());
                        }
                        //TODO: Change to UProperty

                        if (exp.ClassName != "Class" && exp.ClassName != "Function" && exp.ClassName != "ShaderCache")
                        {
                            try
                            {
                                var props = exp.GetProperties();
                                if (props.Count > 0)
                                {
                                    stringoutput.WriteLine("==============Properties==============");
                                    UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                                    foreach (UProperty prop in props)
                                    {
                                        InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, exp);
                                    }
                                    topLevelTree.PrintPretty("", stringoutput, false, exp);
                                    stringoutput.WriteLine();
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine(exp.UIndex);
                            }
                        }

                        //Interpreter i = new Interpreter();
                        //i.Pcc = pcc;
                        //i.Index = index - 1; //0-based array
                        //i.InitInterpreter();
                        //TreeNode top = i.topNode;

                        //if (top.Children.Count > 0 && top.Children[0].Tag != Interpreter.nodeType.None)
                        //{
                        //    
                        //    //stringoutput.WriteLine(String.Format("|{0,40}|{1,15}|{2,10}|{3,30}|", "Name", "Type", "Size", "Value"));
                        //    top.PrintPretty("", stringoutput, false);
                        //    stringoutput.WriteLine();
                        //}
                    }
                    numDone++;
                    stringoutput.WriteLine("--End of " + datasets);


                    // writeVerboseLine("Gathering names");
                    stringoutput.WriteLine("--Start of Names");

                    int count = 0;
                    foreach (string s in pcc.Names)
                        stringoutput.WriteLine((count++) + " : " + s);
                    stringoutput.WriteLine("--End of Names");
                }
                pcc.Release();
            }

            //if (properties)
            //{
            //Resolve LevelStreamingKismet references
            //    string savepath = outfolder + System.IO.Path.GetFileNameWithoutExtension(file) + ".txt";
            //    string output = File.ReadAllText(savepath);
            //    string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

            //    int parsingLine = 0;
            //    string streamingline = "LevelStreamingKismet [EXPORT";
            //    string kismetprefix = "LevelStreamingKismet(LevelStreamingKismet)";
            //    Dictionary<int, int> streamingLines = new Dictionary<int, int>(); //Maps string line # to export #s
            //    Dictionary<int, string> lskPackageName = new Dictionary<int, string>();
            //    //string streamingline = "LevelStreamingKismet[EXPORT";
            //    string packagenameprefix = "Name: \"PackageName\" Type: \"NameProperty\" Size: 8 Value: \"";
            //    foreach (string line in lines)
            //    {

            //        int exportnumstart = line.IndexOf(streamingline);
            //        if (exportnumstart > 0)
            //        {
            //            exportnumstart += streamingline.Length;
            //            string truncstr = line.Substring(exportnumstart);
            //            int exportnumend = truncstr.IndexOf("]");
            //            string exportidstr = truncstr.Substring(0, exportnumend);
            //            int export = int.Parse(exportidstr);
            //            export++;
            //            streamingLines[parsingLine] = export;
            //            parsingLine++;
            //            continue;
            //        }

            //        if (line.Contains(kismetprefix))
            //        {
            //            //Get Export #
            //            string exportStr = line.Substring(1); //Remove #
            //            exportStr = exportStr.Substring(0, exportStr.IndexOf(" "));
            //            int exportNum = int.Parse(exportStr);
            //            //Get PackageName
            //            string packagenamline = lines[parsingLine + 3];
            //            if (packagenamline.Contains("PackageName"))
            //            {
            //                int prefixindex = packagenamline.IndexOf(packagenameprefix);
            //                prefixindex += packagenameprefix.Length;
            //                packagenamline = packagenamline.Substring(prefixindex);
            //                int endofpackagename = packagenamline.IndexOf("\"");
            //                string packagename = packagenamline.Substring(0, endofpackagename);
            //                lskPackageName[exportNum] = packagename;
            //            }
            //            parsingLine++;
            //            continue;
            //        }
            //        parsingLine++;
            //    }

            //    //Updates lines.
            //    foreach (KeyValuePair<int, int> entry in streamingLines)
            //    {
            //        lines[entry.Key] += " - " + lskPackageName[entry.Value];
            //        Console.WriteLine(lines[entry.Key]);

            //        // do something with entry.Value or entry.Key
            //    }
            //    File.WriteAllLines(savepath, lines, Encoding.UTF8);
            //}
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("Exception parsing " + file + "\n" + e.Message);
            //}
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
                throw new ArgumentNullException("fromPath");
            }

            if (string.IsNullOrEmpty(toPath))
            {
                throw new ArgumentNullException("toPath");
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

        private string AppendDirectorySeparatorChar(string path)
        {
            // Append a slash only if the path is a directory and does not have a slash.
            if (!System.IO.Path.HasExtension(path) &&
                !path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
            {
                return path + System.IO.Path.DirectorySeparatorChar;
            }

            return path;
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //This window does not handle updates
        }

        private void PackageDumper_Closing(object sender, CancelEventArgs e)
        {
            DumpCanceled = true;
        }

        private void PackageDumper_Loaded(object sender, RoutedEventArgs e)
        {
            Owner = null; //Detach from parent
        }
    }
}
