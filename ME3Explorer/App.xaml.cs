using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using KFreonLib.MEDirectories;
using ME1Explorer.Unreal;
using ME2Explorer.Unreal;
using ME3Explorer.Unreal;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string AppDataFolder { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ME3Explorer\"; } }

        public static readonly string FileFilter = "*.pcc;*.u;*.upk;*sfm|*.pcc;*.u;*.upk;*sfm|All Files (*.*)|*.*";

        public static string Version { get { return "v" + GetVersion(); } }

        public static string GetVersion()
        {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            return ver.Major + "." + ver.Minor + "." + ver.Build;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //Winforms interop
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Integration.WindowsFormsHost.EnableWindowsFormsInterop();

            //set up AppData Folder
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            //load in data files
            TalkFiles.LoadSavedTlkList();
            ME2Explorer.TalkFiles.LoadSavedTlkList();
            ME1UnrealObjectInfo.loadfromJSON();
            ME2UnrealObjectInfo.loadfromJSON();
            ME3UnrealObjectInfo.loadfromJSON();

            //static class setup
            Tools.Initialize();

            int exitCode = 0;
            if (HandleCommandLineArgs(Environment.GetCommandLineArgs(), out exitCode))
            {
                Shutdown(exitCode);
            }
            else
            {
                (new MainWindow()).Show();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            ME3Explorer.Properties.Settings.Default.Save();
        }

        private bool HandleCommandLineArgs(IList<string> args, out int exitCode)
        {
            //TODO: handle command line args
            if (args.Count < 2)
            {
                exitCode = 0;
                return false;
            }
            //automation
            try
            {
                if (args[1].Equals("-dlcinject") || args[1].Equals("-dlcextract") || args[1].Equals("-dlcaddfiles") || args[1].Equals("-dlcremovefiles") || args[1].Equals("-dlcunpack") || args[1].Equals("-dlcunpack-nodebug"))
                {
                    //Can't be automated because all operations depend on methods that 
                    //require UI instances and can't be static
                    //TODO: fix this to actually handle return codes properly
                    SFAREditor2 sfar2 = new SFAREditor2();
                    exitCode = 0;
                    return true;
                }
                else if (args[1].Equals("-toceditorupdate"))
                {
                    //Legacy command requested by FemShep
                    TOCeditor toc = new TOCeditor();
                    exitCode = toc.updateTOCFromCommandLine(args.Skip(2).ToList());
                    return true;
                }
                else if (args[1].Equals("-autotoc"))
                {
                    if (args.Count == 2)
                    {
                        AutoTOC.GenerateAllTOCs();
                    }
                    else
                    {
                        AutoTOC.prepareToCreateTOC(args[2]);
                    }
                    exitCode = 0;
                    return true;
                }
                else if (args[1].Equals("-sfarautotoc"))
                {
                    if (args.Count != 3)
                    {
                        MessageBox.Show("-sfarautotoc command line argument requires 1 parameter:\nSFARFILE.sfar", "Automated SFAR TOC Update Fail", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }
                    BitConverter.IsLittleEndian = true;
                    DLCPackage DLC = new DLCPackage(args[2]);
                    DLC.UpdateTOCbin(true);
                    exitCode = 0;
                    return true;
                }
                else if (args[1].Equals("-decompresspcc"))
                {
                    if (args.Count != 4)
                    {
                        MessageBox.Show("-decompresspcc command line argument requires 2 parameters:\ninputfile.pcc outputfile.pcc\nBoth arguments can be the same.", "Auto Decompression Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        exitCode = 1;
                        return true;
                    }
                    exitCode = PCCRepack.autoDecompressPcc(args[2], args[3]);
                    return true;
                }
                else if (args[1].Equals("-compresspcc"))
                {
                    if (args.Count != 4)
                    {
                        MessageBox.Show("-compresspcc command line argument requires 2 parameters:\ninputfile.pcc outputfile.pcc\nBoth arguments can be the same.", "Auto Compression Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        exitCode = 1;
                        return true;
                    }
                    exitCode = PCCRepack.autoCompressPcc(args[2], args[3]);
                    return true;
                }
                else if (args[1].Equals("--help") || args[1].Equals("-h") || args[1].Equals("/?"))
                {
                    string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    string commandLineHelp = "\nME3Explorer v" + version + " Command Line Options\n";
                    commandLineHelp += " -autotoc rootpath\n";
                    commandLineHelp += "     Automates the AutoTOC tool to generate a new PCConsoleTOC.bin file in the folder rootpath.\n\n";
                    commandLineHelp += " -compresspcc pccPath.pcc compressedPath.pcc\n";
                    commandLineHelp += "     Automates PCCRepacker to compress a pcc to the new location.\n\n";
                    commandLineHelp += " -decompresspcc pccPath.pcc decompressedPath.pcc\n";
                    commandLineHelp += "     Automates PCCRepacker to decompress a pcc to the new location.\n\n";
                    commandLineHelp += " -dlcinject DLC.sfar SearchTerm PathToNewFile [SearchTerm2 PathToNewFile2]...\n";
                    commandLineHelp += "     Automates injecting pairs of files into a .sfar file using DLCEditor2. SearchTerm is a value you would type into the searchbox with the first result being the file that will be replaced.\n\n";
                    commandLineHelp += " -dlcextract DLC.sfar SearchTerm ExtractionPath\n";
                    commandLineHelp += "     Automates DLCEditor2 to extract the specified SearchTerm. SearchTerm is a value you would type into the searchbox with the first result being the file that will be extracted. The file is extracted to the specied ExtractionPath.\n\n";
                    commandLineHelp += " -dlcaddfiles DLC.sfar InternalPath NewFile [InternalPath2 NewFile2]...\n";
                    commandLineHelp += "     Automates DLCEditor2 to add the specified new files. InternalPath is the internal path in the SFAR the file NewFile will be placed at.\n\n";
                    commandLineHelp += " -dlcremovefiles DLC.sfar SearchTerm [SearchTerm2]...\n";
                    commandLineHelp += "     Automates removing a file or list of files from a DLC. SearchTerm is a value you would type into the Searchbox with the first result being the file that will be removed.\n\n";
                    commandLineHelp += " -dlcunpack DLC.sfar Unpackpath\n";
                    commandLineHelp += "     Automates unpacking an SFAR file to the specified directory. Shows the debug interface to show progress. To unpack a game DLC for use by the game, unpack to the Mass Effect 3 directory. Unpacking Patch_001.sfar will cause the game to crash at startup.\n\n";
                    commandLineHelp += " -dlcunpack-nodebug DLC.sfar Unpackpath\n";
                    commandLineHelp += "     Same as -dlcunpack but does not show the debugging interface.\n\n";
                    commandLineHelp += " -toceditorupdate PCConsoleTOC.bin SearchTerm size\n";
                    commandLineHelp += "     Updates TOC entry for specified file\n\n";
                    Console.WriteLine(commandLineHelp);
                    exitCode = 0;
                    return true;
                }
            }
            catch
            {
                exitCode = 1;
                return true;
            }

            string ending = Path.GetExtension(args[1]).ToLower();
            switch (ending)
            {
                //TODO: delay this until after the mainwindow opens somehow
                case ".pcc":
                    PackageEditor editor = new PackageEditor();
                    editor.Show();
                    editor.LoadFile(args[1]);
                    break;
            }
            exitCode = 0;
            return false;
        }
    }
}
