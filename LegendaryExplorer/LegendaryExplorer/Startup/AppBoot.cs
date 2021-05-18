using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using LegendaryExplorer.Dialogs.Splash;
using LegendaryExplorer.DialogueEditor;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Misc.Telemetry;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.PeregrineTreeView;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.Soundplorer;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Startup
{
    /// <summary>
    /// Contains the application bootup code that is shown when loading, during the splash screen
    /// </summary>
    public static class AppBoot
    {
        public static DPIAwareSplashScreen LEXSplashScreen;

        /// <summary>
        /// Invoked during the splash screen sequence for the application
        /// </summary>
        public static void Startup(App app)
        {
            LEXSplashScreen = new DPIAwareSplashScreen();
            LEXSplashScreen.Show();

            //Peregrine's Dispatcher (for WPF Treeview selecting on virtualized lists)
            DispatcherHelper.Initialize();
            initCoreLib();
            Settings.LoadSettings();

            // AppCenter setup
#if DEBUG
            //We should only track things like this in release mode so we don't pollute our dataset
            if (Settings.Global_Analytics_Enabled && APIKeys.HasAppCenterKey)
            {
                Microsoft.AppCenter.AppCenter.Start(APIKeys.AppCenterKey,
                    typeof(Microsoft.AppCenter.Analytics.Analytics), typeof(Microsoft.AppCenter.Crashes.Crashes));
            }
#endif

            // Winforms setup
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            WindowsFormsHost.EnableWindowsFormsInterop();

            // WPF setup
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));


            //set up AppData Folder
            if (!Directory.Exists(AppDirectories.AppDataFolder))
            {
                Directory.CreateDirectory(AppDirectories.AppDataFolder);
            }

            Settings.LoadSettings();

            ToolSet.Initialize();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Dispatcher.UnhandledException += app.OnDispatcherUnhandledException; //only start handling them after bootup

            Action actionDelegate = null;
            Task.Run(() =>
            {
                //Fetch core count from WMI - this can take like 1-2 seconds
                try
                {
                    App.CoreCount = 2;
                    foreach (var item in new System.Management.ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").Get())
                    {
                        App.CoreCount = int.Parse(item["NumberOfCores"].ToString());
                    }
                }
                catch
                {
                    //???
                }

                actionDelegate = HandleCommandLineJumplistCall(Environment.GetCommandLineArgs(), out int exitCode);
                if (actionDelegate == null)
                {
                    app.Shutdown(exitCode);
                    LEXSplashScreen?.Close();
                }
                else
                {

                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
                    //PendingAppLoadedAction = actionDelegate;

                    // TODO: IMPLEMENT IN LEX
                    //GameController.InitializeMessageHook(mainWindow);

#if DEBUG
                    //StandardLibrary.InitializeStandardLib();
#endif
                }
            }).ContinueWithOnUIThread(x =>
            {
                LEXSplashScreen?.Close();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                var mainWindow = new MainWindow();
                mainWindow.Show();
                actionDelegate?.Invoke();
            });
        }

        private static Action HandleCommandLineJumplistCall(string[] args, out int exitCode)
        {
            exitCode = 0;
            if (args.Any())
            {
                // Non-single file have dll as first parameter
                if (Path.GetFileName(args[0]) == "LegendaryExplorer.dll")
                {
                    if (args.Length == 1) return () => { };
                    var l = args.ToList();
                    l.RemoveAt(0);
                    args = l.ToArray();
                }
            }
            else
            {
                return () => { };
            }

            var arg = args[0];

            // JUMPLIST
            Action CreateJumplistAction<T>(Action<T> toolAction = null) // I heard you like actions
                where T : WPFBase, new()
            {
                return () =>
                {
                    var editor = new T();
                    editor.Show();
                    toolAction?.Invoke(editor);
                    editor.Activate();
                };
            }

            // TODO: Add more detailed CLI args. EG: -PackageEditor %1, -TLKEditor %1, -UIndex 112
            switch (arg)
            {
                // Tool must have a parameterless constructor for this to work
                case "-PackageEditor": return CreateJumplistAction<PackageEditorWindow>();
                case "-SequenceEditor": return CreateJumplistAction<SequenceEditorWPF>();
                case "-Soundplorer": return CreateJumplistAction<SoundplorerWPF>();
                case "-DialogueEditor": return CreateJumplistAction<DialogueEditorWindow>();
                case "-PathfindingEditor": return CreateJumplistAction<Tools.PathfindingEditor.PathfindingEditorWindow>();
                case "-Meshplorer": return CreateJumplistAction<Tools.Meshplorer.MeshplorerWindow>();
            }

            // OPENING PACKAGE FILE DIRECTLY
            string ending = Path.GetExtension(arg).ToLower();
            switch (ending)
            {
                case ".pcc":
                case ".sfm":
                case ".upk":
                case ".u":
                case ".udk":
                    return CreateJumplistAction<PackageEditorWindow>((p) => p.LoadFile(arg));
                //return 2; //Do not signal bring main forward
            }
            exitCode = 0; //is this even used?
            return null;
        }

        private static void initCoreLib()
        {
#if DEBUG
            MemoryAnalyzer.IsTrackingMemory = true;
#endif
            void packageSaveFailed(string message)
            {
                // I'm not sure if this requires ui thread since it's win32 but i'll just make sure
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message);
                });
            }

            LegendaryExplorerCoreLib.InitLib(TaskScheduler.FromCurrentSynchronizationContext(), packageSaveFailed);
            CoreLibSettingsBridge.MapSettingsIntoBridge();
            PackageSaver.CheckME3Running = () =>
            {
                // TODO: IMPLEMENT IN LEX
                return false;
                //GameController.TryGetMEProcess(MEGame.ME3, out var me3Proc);
                //return me3Proc != null;
            };
            //PackageSaver.NotifyRunningTOCUpdateRequired = GameController.SendME3TOCUpdateMessage;
            PackageSaver.GetPNGForThumbnail = texture2D => texture2D.GetPNG(texture2D.GetTopMip()); // Used for UDK packages
        }

        /// <summary>
        /// Invoked when a second instance of Legendary Explorer is opened
        /// </summary>
        /// <param name="args"></param>
        public static void HandleDuplicateInstanceArgs(string[] args)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                HandleCommandLineJumplistCall(args, out _)?.Invoke();
            });
        }
    }
}
