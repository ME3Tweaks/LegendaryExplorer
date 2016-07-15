using System;
using System.IO;
using System.Reflection;
using System.Windows;
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
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            ME3Explorer.Properties.Settings.Default.Save();
        }
    }
}
