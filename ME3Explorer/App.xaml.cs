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
