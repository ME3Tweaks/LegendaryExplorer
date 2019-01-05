using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MassEffect3ModManagerCmdLine;

namespace ME3Explorer.PackageDumper
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PackageDumper : Window
    {
        public PackageDumper()
        {
            InitializeComponent();
        }

        public PackageDumper(int v)
        {
            this.v = v;
        }

        private bool verbose;
        private int v;

        public bool Verbose
        {
            set
            {
                verbose = value;
            }
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
        /// Dumps data from a pcc file to a text file
        /// </summary>
        /// <param name="file">PCC file path to dump from</param>
        /// <param name="args">6 element boolean array, specifying what should be dumped. In order: imports, exports, data, scripts, coalesced, names. At least 1 of these options must be true.</param>
        /// <param name="outputfolder"></param>
        public void dumpPCCFile(string file, bool[] args, string outputfolder = null)
        {
            //if (GamePath == null)
            //{
            //    Console.Error.WriteLine("Game path not defined. Can't dump file file with undefined game System.IO.Path.");
            //    return;
            //}
            //try
            {
                Boolean imports = args[0];
                Boolean exports = args[1];
                Boolean data = args[2];
                Boolean scripts = args[3];
                Boolean coalesced = args[4];
                Boolean names = args[5];
                Boolean separateExports = args[6];
                Boolean properties = args[7];

                IMEPackage pcc = MEPackageHandler.OpenMEPackage(file);

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
                if (imports || exports || data || scripts || coalesced || names || properties)
                {
                    //dumps data.
                    string savepath = outfolder + System.IO.Path.GetFileNameWithoutExtension(file) + ".txt";
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(savepath));
                    stringoutput = new StreamWriter(savepath);
                }

                using (stringoutput)
                {

                    if (imports)
                    {
                        writeVerboseLine("Getting Imports");
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
                    }

                    if (exports || scripts || data || coalesced)
                    {
                        string datasets = "";
                        if (exports)
                        {
                            datasets += "Exports ";
                        }
                        if (scripts)
                        {
                            datasets += "Scripts ";
                        }
                        if (coalesced)
                        {
                            datasets += "Coalesced ";
                        }
                        if (data)
                        {
                            datasets += "Data ";
                        }

                        stringoutput.WriteLine("--Start of " + datasets);

                        int numDone = 1;
                        int numTotal = pcc.Exports.Count;
                        int lastProgress = 0;
                        writeVerboseLine("Enumerating exports");
                        Boolean needsFlush = false;
                        int index = 0;
                        string swfoutfolder = outfolder + System.IO.Path.GetFileNameWithoutExtension(file) + "\\";
                        foreach (IExportEntry exp in pcc.Exports)
                        {
                            index++;
                            writeVerboseLine("Parse export #" + index);

                            //Boolean isCoalesced = coalesced && exp.likelyCoalescedVal;
                            String className = exp.ClassName;
                            Boolean isCoalesced = exp.ReadsFromConfig;
                            Boolean isScript = scripts && (className == "Function");
                            int progress = ((int)(((double)numDone / numTotal) * 100));
                            while (progress >= (lastProgress + 10))
                            {
                                Console.Write("..." + (lastProgress + 10) + "%");
                                needsFlush = true;
                                lastProgress += 10;
                            }

                            if (exports || data || isScript || (coalesced && isCoalesced))
                            {
                                if (separateExports)
                                {
                                    stringoutput.WriteLine("=======================================================================");
                                }
                                stringoutput.Write("#" + index + " ");
                                if (isCoalesced && coalesced)
                                {
                                    stringoutput.Write("[C] ");
                                }

                                if (exports && exports || isCoalesced && coalesced || isScript && scripts)
                                {
                                    stringoutput.Write(exp.PackageFullName + "." + exp.ObjectName + "(" + exp.ClassName + ")");
                                    int ival = exp.indexValue;
                                    if (ival > 0)
                                    {
                                        stringoutput.Write("(Index: " + ival + ") ");

                                    }
                                    stringoutput.WriteLine("(Superclass: " + exp.ClassParent + ") (Data Offset: 0x " + exp.DataOffset.ToString("X4") + ")");
                                }

                                if (isScript)
                                {
                                    stringoutput.WriteLine("==============Function==============");
                                    //Function func = new Function(exp.Data, pcc);
                                    //stringoutput.WriteLine(func.ToRawText());
                                }
                                if (properties)
                                {
                                    //TODO: Change to UProperty
                                    //Interpreter i = new Interpreter();
                                    //i.Pcc = pcc;
                                    //i.Index = index - 1; //0-based array
                                    //i.InitInterpreter();
                                    //TreeNode top = i.topNode;

                                    //if (top.Children.Count > 0 && top.Children[0].Tag != Interpreter.nodeType.None)
                                    //{
                                    //    stringoutput.WriteLine("=================================================Properties=================================================");
                                    //    //stringoutput.WriteLine(String.Format("|{0,40}|{1,15}|{2,10}|{3,30}|", "Name", "Type", "Size", "Value"));
                                    //    top.PrintPretty("", stringoutput, false);
                                    //    stringoutput.WriteLine();
                                    //}
                                }
                                if (data)
                                {
                                    stringoutput.WriteLine("==============Data==============");
                                    stringoutput.WriteLine(BitConverter.ToString(exp.Data));
                                }
                            }
                            numDone++;
                        }
                        stringoutput.WriteLine("--End of " + datasets);

                        if (needsFlush)
                        {
                            Console.WriteLine();
                        }
                    }

                    if (names)
                    {
                        writeVerboseLine("Gathering names");
                        stringoutput.WriteLine("--Names");

                        int count = 0;
                        foreach (string s in pcc.Names)
                            stringoutput.WriteLine((count++) + " : " + s);
                        stringoutput.WriteLine("--End of Names");

                    }
                }

                if (properties)
                {
                    //Resolve LevelStreamingKismet references
                    string savepath = outfolder + System.IO.Path.GetFileNameWithoutExtension(file) + ".txt";
                    string output = File.ReadAllText(savepath);
                    string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

                    int parsingLine = 0;
                    string streamingline = "LevelStreamingKismet [EXPORT";
                    string kismetprefix = "LevelStreamingKismet(LevelStreamingKismet)";
                    Dictionary<int, int> streamingLines = new Dictionary<int, int>(); //Maps string line # to export #s
                    Dictionary<int, string> lskPackageName = new Dictionary<int, string>();
                    //string streamingline = "LevelStreamingKismet[EXPORT";
                    string packagenameprefix = "Name: \"PackageName\" Type: \"NameProperty\" Size: 8 Value: \"";
                    foreach (string line in lines)
                    {

                        int exportnumstart = line.IndexOf(streamingline);
                        if (exportnumstart > 0)
                        {
                            exportnumstart += streamingline.Length;
                            string truncstr = line.Substring(exportnumstart);
                            int exportnumend = truncstr.IndexOf("]");
                            string exportidstr = truncstr.Substring(0, exportnumend);
                            int export = int.Parse(exportidstr);
                            export++;
                            streamingLines[parsingLine] = export;
                            parsingLine++;
                            continue;
                        }

                        if (line.Contains(kismetprefix))
                        {
                            //Get Export #
                            string exportStr = line.Substring(1); //Remove #
                            exportStr = exportStr.Substring(0, exportStr.IndexOf(" "));
                            int exportNum = int.Parse(exportStr);
                            //Get PackageName
                            string packagenamline = lines[parsingLine + 3];
                            if (packagenamline.Contains("PackageName"))
                            {
                                int prefixindex = packagenamline.IndexOf(packagenameprefix);
                                prefixindex += packagenameprefix.Length;
                                packagenamline = packagenamline.Substring(prefixindex);
                                int endofpackagename = packagenamline.IndexOf("\"");
                                string packagename = packagenamline.Substring(0, endofpackagename);
                                lskPackageName[exportNum] = packagename;
                            }
                            parsingLine++;
                            continue;
                        }
                        parsingLine++;
                    }

                    //Updates lines.
                    foreach (KeyValuePair<int, int> entry in streamingLines)
                    {
                        lines[entry.Key] += " - " + lskPackageName[entry.Value];
                        Console.WriteLine(lines[entry.Key]);

                        // do something with entry.Value or entry.Key
                    }
                    File.WriteAllLines(savepath, lines, Encoding.UTF8);
                }
            }
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
    }
}
