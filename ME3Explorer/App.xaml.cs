using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ME1Explorer.Unreal;
using ME2Explorer.Unreal;
using ME3Explorer.ASI;
using ME3Explorer.Dialogue_Editor;
using ME3Explorer.GameInterop;
using ME3Explorer.MountEditor;
using ME3Explorer.Packages;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SharedUI.PeregrineTreeView;
using ME3Explorer.Soundplorer;
using ME3Explorer.Splash;
using ME3Explorer.Unreal;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //Should move this to Path.Combine() in future
        public static string AppDataFolder => Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ME3Explorer\").FullName;
        public static string StaticExecutablesDirectory => Directory.CreateDirectory(Path.Combine(AppDataFolder, "staticexecutables")).FullName; //ensures directory will always exist.

        /// <summary>
        /// Static files base URL points to the static directory on the ME3Explorer github and will have executable and other files that are no distributed in the initial download of ME3Explorer.
        /// </summary>
        public const string StaticFilesBaseURL = "https://github.com/ME3Tweaks/ME3Explorer/raw/Beta/StaticFiles/";
        public static string ExecFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exec");
        /// <summary>
        /// When the app is opened for the first time, if its called to open command line arg, this will lbe populated and fired after loading
        /// </summary>
        public static Action PendingAppLoadedAction = null;
        public static string HexConverterPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HexConverter.exe");

        public static bool TlkFirstLoadDone; //Set when the TLK loading at startup is finished.
        public const string FileFilter = "*.pcc;*.u;*.upk;*sfm;*udk;*.xxx|*.pcc;*.u;*.upk;*sfm;*udk;*.xxx|All Files (*.*)|*.*";
        public const string UDKFileFilter = "*.upk;*udk|*.upk;*udk";
        public const string ME1FileFilter = "*.u;*.upk;*sfm;*.xxx|*.u;*.upk;*sfm;*.xxx";
        public const string ME3ME2FileFilter = "*.pcc*;.xxx|*.pcc;*.xxx";

        public static string CustomResourceFilePath(MEGame game) => Path.Combine(ExecFolder, game switch
        {
            MEGame.ME3 => "ME3Resources.pcc",
            MEGame.ME2 => "ME2Resources.pcc",
            MEGame.ME1 => "ME1Resources.upk",
            MEGame.UDK => "UDKResources.upk",
            _ => "ME3Resources.pcc"
        });

        public static string Version => GetVersion();

        public static Visibility IsDebugVisibility => IsDebug ? Visibility.Visible : Visibility.Collapsed;

#if DEBUG
        public static bool IsDebug => true;
#else
        public static bool IsDebug => false;
#endif

        public static bool IsDarkMode;

        public static string RepositoryURL => "http://github.com/ME3Tweaks/ME3Explorer/";
        public static string BugReportURL => $"{RepositoryURL}issues/";

        public static string GetVersion()
        {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            return "v" + ver.Major + "." + ver.Minor + "." + ver.Build + "." + ver.Revision;
        }

        public static TaskScheduler SYNCHRONIZATION_CONTEXT;
        public static int CoreCount;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            //API keys are not stored in the git repository for ME3Explorer.
            //You will need to provide your own keys for use by defining public properties
            //in a partial APIKeys class.
#if !DEBUG
            //We should only track things like this in release mode so we don't pollute our dataset
            var props = typeof(APIKeys).GetProperties();
            if (APIKeys.HasAppCenterKey)
            {
                AppCenter.Start(APIKeys.AppCenterKey,
                    typeof(Analytics), typeof(Crashes));
            }
#endif
            //Peregrine's Dispatcher (for WPF Treeview selecting on virtualized lists)
            DispatcherHelper.Initialize();
            SYNCHRONIZATION_CONTEXT = TaskScheduler.FromCurrentSynchronizationContext();
            //Winforms interop
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Integration.WindowsFormsHost.EnableWindowsFormsInterop();

            //set up AppData Folder
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            //Set up Dark Mode
            //try
            //{
            //    var v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", "1");
            //    IsDarkMode = v?.ToString() == "0";
            //}
            //catch
            //{
            //    IsDarkMode = false;
            //}
            //Be.Windows.Forms.HexBox.SetColors((IsDarkMode ? Color.FromArgb(255, 55, 55, 55) : Colors.White).ToWinformsColor(), SystemColors.ControlTextColor.ToWinformsColor());

            Parallel.Invoke(
                            () => ME1UnrealObjectInfo.loadfromJSON(),
                            () => ME2UnrealObjectInfo.loadfromJSON(),
                            () => ME3UnrealObjectInfo.loadfromJSON()
                );



            //static class setup
            Tools.Initialize();
            MEPackageHandler.Initialize();


            System.Windows.Controls.ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));

            

            Action actionDelegate = HandleCommandLineJumplistCall(Environment.GetCommandLineArgs(), out int exitCode);
            if (actionDelegate == null)
            {
                Shutdown(exitCode);
                ME3ExplorerSplashScreen?.Close();
            }
            else
            {
                PendingAppLoadedAction = actionDelegate;
                Dispatcher.UnhandledException += OnDispatcherUnhandledException; //only start handling them after bootup
                var mainWindow = new MainWindow();
                mainWindow.Show();

                //close splash after
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                ME3ExplorerSplashScreen?.Close();
                ShutdownMode = ShutdownMode.OnMainWindowClose;

                GameController.InitializeMessageHook(mainWindow);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            ME3Explorer.Properties.Settings.Default.Save();
        }

        private Action HandleCommandLineJumplistCall(string[] args, out int exitCode)
        {
            exitCode = 0;
            if (args.Length < 2)
            {
                return ()=>{}; //do nothing delgate. Will do nothing when main UI loads
            }

            string arg = args[1];
            if (arg == "JUMPLIST_PACKAGE_EDITOR")
            {
                return () =>
                {
                    PackageEditorWPF editor = new PackageEditorWPF();
                    editor.Show();
                    editor.Activate();
                };
            }

            if (arg == "JUMPLIST_SEQUENCE_EDITOR")
            {
                return () =>
                {
                    var editor = new SequenceEditorWPF();
                    editor.Show();
                    editor.Activate();
                };
            }

            if (arg == "JUMPLIST_PATHFINDING_EDITOR")
            {
                return () =>
                {
                    PathfindingEditorWPF editor = new PathfindingEditorWPF();
                    editor.Show();
                    editor.Activate();
                };
            }

            if (arg == "JUMPLIST_SOUNDPLORER")
            {
                return () =>
                {
                    SoundplorerWPF soundplorerWpf = new SoundplorerWPF();
                    soundplorerWpf.Show();
                    soundplorerWpf.Activate();

                };
            }

            //Do not remove - used by Mass Effect Mod Manager to boot the tool
            if (arg == "JUMPLIST_ASIMANAGER")
            {
                return () =>
                {
                    ASIManager asiManager = new ASIManager();
                    asiManager.Show();
                    asiManager.Activate();
                };
            }

            //Do not remove - used by Mass Effect Mod Manager to boot the tool
            if (arg == "JUMPLIST_MOUNTEDITOR")
            {
                return () =>
                {
                    MountEditorWPF mountEditorWpf = new MountEditorWPF();
                    mountEditorWpf.Show();
                    mountEditorWpf.Activate();
                };
            }

            //Do not remove - used by Mass Effect Mod Manager to boot the tool
            if (arg == "JUMPLIST_PACKAGEDUMPER")
            {
                return () =>
                {
                    PackageDumper.PackageDumper packageDumper = new PackageDumper.PackageDumper();
                    packageDumper.Show();
                    packageDumper.Activate();
                };
            }

            //Do not remove - used by Mass Effect Mod Manager to boot the tool
            if (arg == "JUMPLIST_DLCUNPACKER")
            {
                return () =>
                {
                    DLCUnpacker.DLCUnpacker dlcUnpacker = new DLCUnpacker.DLCUnpacker();
                    dlcUnpacker.Show();
                    dlcUnpacker.Activate();
                };
            }

            if (arg == "JUMPLIST_DIALOGUEEDITOR")
            {
                return () =>
                {
                    DialogueEditorWPF editor = new DialogueEditorWPF();
                    editor.Show();
                    editor.Activate();
                };
            }

            if (arg == "JUMPLIST_MESHPLORER")
            {
                return () =>
                {
                    MeshplorerWPF meshplorerWpf = new MeshplorerWPF();
                    meshplorerWpf.Show();
                    meshplorerWpf.Activate();
                };
            }


            string ending = Path.GetExtension(args[1]).ToLower();
            switch (ending)
            {
                case ".pcc":
                case ".sfm":
                case ".upk":
                case ".u":
                case ".udk":
                    return () =>
                    {
                        PackageEditorWPF editor = new PackageEditorWPF();
                        editor.Show();
                        editor.LoadFile(args[1]);
                        editor.RestoreAndBringToFront();
                    };
                    //return 2; //Do not signal bring main forward
            }
            exitCode = 0; //is this even used?
            return null;
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
