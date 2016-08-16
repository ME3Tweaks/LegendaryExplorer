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
                        MessageBox.Show("-sfarautotoc command line argument requires at least 1 parameter:\nSFARFILE.sfar", "Automated SFAR TOC Update Fail", MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                        return true;
                    }
                    
                    var result = Parallel.For(2, args.Count, i =>
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
