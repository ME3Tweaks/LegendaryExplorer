using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using KFreonLib.MEDirectories;
using ME1Explorer.Unreal;
using ME2Explorer.Unreal;
using ME3Explorer.Packages;
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
            ME3TalkFiles.LoadSavedTlkList();
            ME2Explorer.ME2TalkFiles.LoadSavedTlkList();
            ME1UnrealObjectInfo.loadfromJSON();
            ME2UnrealObjectInfo.loadfromJSON();
            ME3UnrealObjectInfo.loadfromJSON();

            //static class setup
            Tools.Initialize();
            MEPackageHandler.Initialize();

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

        private bool HandleCommandLineArgs(string[] args, out int exitCode)
        {
            if (args.Length < 2)
            {
                exitCode = 0;
                return false;
            }
            //automation
            try
            {
                if (args[1].Equals("-dlcinject"))
                {
                    if (args.Length % 2 != 1 || args.Length < 5)
                    {
                        MessageBox.Show("Wrong number of arguments for the -dlcinject switch.:\nSyntax is: <exe> -dlcinject SFARPATH SEARCHTERM NEWFILEPATH [SEARCHTERM2 NEWFILEPATH2]...", "ME3 DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }
                    string dlcFileName = args[2];
                    int numfiles = (args.Length - 3) / 2;

                    string[] filesToReplace = new string[numfiles];
                    string[] newFiles = new string[numfiles];

                    int argnum = 3; //starts at 3
                    for (int i = 0; i < filesToReplace.Length; i++)
                    {
                        filesToReplace[i] = args[argnum];
                        argnum++;
                        newFiles[i] = args[argnum];
                        argnum++;
                    }
                    if (File.Exists(dlcFileName))
                    {
                        DLCPackage dlc = new DLCPackage(dlcFileName);
                        for (int i = 0; i < numfiles; i++)
                        {
                            int idx = dlc.FindFileEntry(filesToReplace[i]);
                            if (idx == -1)
                            {
                                MessageBox.Show("DLCEditor2 automator encountered an error: the file to replace does not exist.", "ME3 DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                exitCode = 1;
                                return true;
                            }
                            dlc.ReplaceEntry(newFiles[i], idx);
                        }
                        exitCode = 0;
                        return true;
                    }
                    MessageBox.Show("Failed to autoinject: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    exitCode = 1;
                    return true;
                }
                else if (args[1].Equals("-dlcextract"))
                {
                    if (args.Length != 5)
                    {
                        //-2 for me3explorer & -dlcextract
                        MessageBox.Show("Wrong number of arguments for the -dlcextract switch.:\nSyntax is: <exe> -dlcextract SFARPATH SEARCHTERM EXTRACTIONPATH", "ME3 DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }
                    string dlcFileName = args[2];
                    string searchTerm = args[3];
                    string extractionPath = args[4];
                    if (File.Exists(dlcFileName))
                    {
                        DLCPackage dlc = new DLCPackage(dlcFileName);
                        int idx = dlc.FindFileEntry(searchTerm);
                        if (idx == -1)
                        {
                            MessageBox.Show("DLCEditor2 extraction automator encountered an error:\nThe file to replace does not exist or the tree has not been initialized.", "DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            exitCode = 1;
                            return true;
                        }
                        using (FileStream fs = new FileStream(extractionPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                        {
                            dlc.DecompressEntryAsync(idx, fs).Wait();
                        }
                        exitCode = 0;
                        return true;
                    }
                    MessageBox.Show("Failed to autoextract: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    exitCode = 1;
                    return true;

                }
                else if (args[1].Equals("-dlcaddfiles"))
                {
                    if (args.Length % 2 != 1 || args.Length < 5)
                    {
                        MessageBox.Show("Wrong number of arguments for the -dlcaddfiles switch.:\nSyntax is: <exe> -dlcinject SFARPATH INTERNALPATH NEWFILEPATH [INTERNALPATH2 NEWFILEPATH2]...", "ME3 DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }
                    
                    string dlcFileName = args[2];
                    int numfiles = (args.Length - 3) / 2;
                    string[] internalPaths = new string[numfiles];
                    string[] sourcePaths = new string[numfiles];

                    int argnum = 3; //starts at 3
                    for (int i = 0; i < internalPaths.Length; i++)
                    {
                        internalPaths[i] = args[argnum];
                        argnum++;
                        sourcePaths[i] = args[argnum];
                        argnum++;
                    }

                    if (File.Exists(dlcFileName))
                    {
                        DLCPackage dlc = new DLCPackage(dlcFileName);
                        for (int i = 0; i < internalPaths.Length; i++)
                        {
                            dlc.AddFileQuick(sourcePaths[i], internalPaths[i]);
                        }
                        exitCode = 0;
                        return true;
                    }
                    MessageBox.Show("Failed to autoadd: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    exitCode = 1;
                    return true;
                }
                else if (args[1].Equals("-dlcremovefiles"))
                {
                    if (args.Length < 4)
                    {
                        //-2 for me3explorer & -dlcextract
                        MessageBox.Show("Wrong number of arguments for the -dlcremovefiles switch.:\nSyntax is: <exe> -dlcinject SFARPATH INTERNALPATH [INTERNALPATH2]...", "ME3 DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }
                    string dlcFileName = args[2];
                    int numfiles = (args.Length - 3);
                    string[] filesToRemove = new string[numfiles];
                    int argnum = 3; //starts at 3
                    for (int i = 0; i < filesToRemove.Length; i++)
                    {
                        filesToRemove[i] = args[argnum];
                        argnum++;
                    }

                    if (File.Exists(dlcFileName))
                    {
                        DLCPackage dlc = new DLCPackage(dlcFileName);
                        for (int i = 0; i < filesToRemove.Length; i++)
                        {
                            int idx = dlc.FindFileEntry(filesToRemove[i]);
                            if (idx == -1)
                            {
                                MessageBox.Show("DLCEditor2 file removal automator encountered an error:\nThe file to remove does not exist or the tree has not been initialized.", "DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                exitCode = 1;
                                return true;
                            }
                            dlc.DeleteEntry(idx);
                        }
                        exitCode = 0;
                        return true;
                    }
                    MessageBox.Show("Failed to autoremove: DLC file does not exist: " + dlcFileName, "ME3Explorer DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    exitCode = 1;
                    return true;

                }
                else if (args[1].Equals("-dlcunpack") || args[1].Equals("-dlcunpack-nodebug"))
                {
                    if (args.Length != 4)
                    {
                        MessageBox.Show("Wrong number of arguments for automated DLC unpacking:\nSyntax is: <exe> -dlcunpack SFARPATH EXTRACTIONPATH", "ME3 DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }

                    string sfarPath = args[2];
                    string autoUnpackFolder = args[3];
                    
                    if (File.Exists(sfarPath))
                    {
                        DLCPackage dlc = new DLCPackage(sfarPath);
                        if (args[1].Equals("-dlcunpack"))
                        {
                            //open debugging window since this operation takes a long time.
                            KFreonLib.Debugging.DebugOutput.StartDebugger("DLC Editor 2");
                        }
                        //Simulate Unpack operation click.
                        SFAREditor2.unpackSFAR(dlc);
                        exitCode = 0;
                        return true;
                    }
                    else
                    {
                        MessageBox.Show("Failed to autounpack: DLC file does not exist: " + sfarPath, "ME3Explorer DLCEditor2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }
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
                    if (args.Length == 2)
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
                    if (args.Length != 3)
                    {
                        MessageBox.Show("-sfarautotoc command line argument requires at least 1 parameter:\nSFARFILE.sfar", "Automated SFAR TOC Update Fail", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }
                    
                    var result = Parallel.For(2, args.Length, i =>
                    {
                        if (args[i].EndsWith(".sfar") && File.Exists(args[i]))
                        {
                            DLCPackage DLC = new DLCPackage(args[2]);
                            DLC.UpdateTOCbin(true); 
                        }
                    });
                    exitCode = result.IsCompleted ? 0 : 1;
                    return true;
                }
                else if (args[1].Equals("-decompresspcc"))
                {
                    if (args.Length != 4)
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
                    if (args.Length != 4)
                    {
                        MessageBox.Show("-compresspcc command line argument requires 2 parameters:\ninputfile.pcc outputfile.pcc\nBoth arguments can be the same.", "Auto Compression Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        exitCode = 1;
                        return true;
                    }
                    exitCode = PCCRepack.autoCompressPcc(args[2], args[3]);
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
