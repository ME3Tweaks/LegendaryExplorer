using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KFreonLib.MEDirectories;
using ME1Explorer.Unreal;
using ME2Explorer.Unreal;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI.PeregrineTreeView;
using ME3Explorer.Soundplorer;
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

        public static string Version { get { return GetVersion(); } }

        public static string GetVersion()
        {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            return "v" + ver.Major + "." + ver.Minor + "." + ver.Build + "." + ver.Revision;
        }

        public static SynchronizationContext SYNCHRONIZATION_CONTEXT_UI;
        public static TaskScheduler SYNCHRONIZATION_CONTEXT_TASKSCHEDULER;
        public static int CoreCount;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //Peregrine's Dispatcher (for WPF Treeview selecting on virtualized lists)
            DispatcherHelper.Initialize();
            SYNCHRONIZATION_CONTEXT_UI = SynchronizationContext.Current;
            SYNCHRONIZATION_CONTEXT_TASKSCHEDULER = TaskScheduler.FromCurrentSynchronizationContext();
            //Winforms interop
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Integration.WindowsFormsHost.EnableWindowsFormsInterop();

            //set up AppData Folder
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            //This is in startup as it takes about 1 second to execute and will stall the UI.
            CoreCount = 0;
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                CoreCount += int.Parse(item["NumberOfCores"].ToString());
            }
            if (CoreCount == 0) { CoreCount = 2; }


            ME1UnrealObjectInfo.loadfromJSON();
            ME2UnrealObjectInfo.loadfromJSON();
            ME3UnrealObjectInfo.loadfromJSON();


            //static class setup
            Tools.Initialize();
            MEPackageHandler.Initialize();

            //load kismet font
            SequenceObjects.SText.LoadFont();


            System.Windows.Controls.ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));

            splashScreen.Close(TimeSpan.FromMilliseconds(1));
            if (HandleCommandLineJumplistCall(Environment.GetCommandLineArgs(), out int exitCode) == 0)
            {
                Shutdown(exitCode);
            }
            else
            {
                this.Dispatcher.UnhandledException += OnDispatcherUnhandledException; //only start handling them after bootup
                (new MainWindow()).Show();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            ME3Explorer.Properties.Settings.Default.Save();
        }

        private int HandleCommandLineJumplistCall(string[] args, out int exitCode)
        {
            if (args.Length < 2)
            {
                exitCode = 0;
                return 1;
            }

            string arg = args[1];
            if (arg == "JUMPLIST_PACKAGE_EDITOR")
            {
                PackageEditorWPF editor = new PackageEditorWPF();
                editor.Show();
                editor.Focus();
                exitCode = 0;
                return 1;
            }
            if (arg == "JUMPLIST_SEQUENCE_EDITOR")
            {
                SequenceEditor editor = new SequenceEditor();
                editor.BringToFront();
                editor.Show();
                exitCode = 0;
                return 1;
            }
            if (arg == "JUMPLIST_PATHFINDING_EDITOR")
            {
                PathfindingEditor editor = new PathfindingEditor();
                editor.Show();
                editor.RestoreAndBringToFront();
                exitCode = 0;
                return 1;
            }
            if (arg == "JUMPLIST_SOUNDPLORER")
            {
                SoundplorerWPF editor = new SoundplorerWPF();
                editor.Show();
                editor.RestoreAndBringToFront();
                exitCode = 0;
                return 1;
            }

            string ending = Path.GetExtension(args[1]).ToLower();
            switch (ending)
            {
                case ".pcc":
                case ".sfm":
                case ".upk":
                case ".u":
                    PackageEditorWPF editor = new PackageEditorWPF();
                    editor.Show();
                    editor.LoadFile(args[1]);
                    editor.RestoreAndBringToFront();
                    exitCode = 0;
                    return 2; //Do not signal bring main forward
            }
            exitCode = 0;
            return 1;
        }

        /// <summary>
        /// Called when an unhandled exception occurs. This method can only be invoked after startup has completed. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Exception to process</param>
        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            SharedUI.ExceptionHandlerDialogWPF eh = new SharedUI.ExceptionHandlerDialogWPF(e.Exception);
            Window wpfActiveWindow = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            eh.Owner = wpfActiveWindow;
            eh.ShowDialog();
            e.Handled = eh.Handled;
        }
    }
}
