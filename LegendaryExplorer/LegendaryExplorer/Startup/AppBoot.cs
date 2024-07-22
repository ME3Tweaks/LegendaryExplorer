using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using FontAwesome5;
using FontAwesome5.Extensions;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.MainWindow;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI.PeregrineTreeView;
using LegendaryExplorer.Tools.CustomFilesManager;
using LegendaryExplorerCore;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Serilog;

namespace LegendaryExplorer.Startup
{
    /// <summary>
    /// Contains the application bootup code that is shown when loading, during the splash screen
    /// </summary>
    public static class AppBoot
    {
        private static bool IsLoaded;
        private static Queue<string[]> Arguments;

        /// <summary>
        /// Invoked during the splash screen sequence for the application
        /// </summary>
        public static void Startup(App app)
        {
            app.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/LegendaryExplorer;component/AppResources.xaml", UriKind.Relative));

            Arguments = new Queue<string[]>();
            Arguments.Enqueue(Environment.GetCommandLineArgs());

            // Prevent working directory from being locked if opened via file assoc
            // We must do this this way for single file support as it will otherwise return dll instead
            Directory.SetCurrentDirectory(Directory.GetParent(Process.GetCurrentProcess().MainModule.FileName).FullName);

            //Peregrine's Dispatcher (for WPF Treeview selecting on virtualized lists)
            DispatcherHelper.Initialize();

            Settings.LoadSettings();
            initCoreLib();

            // AppCenter setup
#if !DEBUG
            //We should only track things like this in release mode so we don't pollute our dataset
            if (Settings.Global_Analytics_Enabled && Misc.Telemetry.APIKeys.HasAppCenterKey)
            {
                Microsoft.AppCenter.AppCenter.Start(Misc.Telemetry.APIKeys.AppCenterKey,
                                                    typeof(Microsoft.AppCenter.Analytics.Analytics), typeof(Microsoft.AppCenter.Crashes.Crashes));
            }
#endif

            // Winforms setup
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            WindowsFormsHost.EnableWindowsFormsInterop();

            // WPF setup
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));
            //fixes bad WPF default. Users aren't going to not want to know what a button does just because it's disabled at the moment!
            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(true));

            //force fontawesome's icons into memory, so that it won't need to happen when the main window is opening 
            EFontAwesomeIcon.None.GetUnicode();

            // Initialize VLC
            LibVLCSharp.Shared.Core.Initialize();

            //set up AppData Folder
            if (!Directory.Exists(AppDirectories.AppDataFolder))
            {
                Directory.CreateDirectory(AppDirectories.AppDataFolder);
            }

            Settings.LoadSettings();

            ToolSet.Initialize();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Dispatcher.UnhandledException += App.OnDispatcherUnhandledException; //only start handling them after bootup

            RootCommand cliHandler = CommandLineArgs.CreateCLIHandler();
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
                    Debug.WriteLine("Unable to determine core count from WMI, defaulting to 2");
                }

                // 08/13/2022 - Custom Class Inventory
                CustomFilesManagerWindow.InventoryCustomAssetDirectories();

                // 08/13/2022 - Custom Startup Files
                CustomFilesManagerWindow.InstallCustomStartupFiles();
            }).ContinueWithOnUIThread(x =>
            {
                var mainWindow = new LEXMainWindow();
                app.MainWindow = mainWindow;
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.TransitionFromSplashToMainWindow();

                GameController.InitializeMessageHook(mainWindow);

                IsLoaded = true;

                while (Arguments.Any())
                {
                    cliHandler.InvokeAsync(Arguments.Dequeue());
                }
            });

            var mpc1 = LegendaryExplorerCore.PlotDatabase.PlotDatabases.GetModPlotContainerForGame(MEGame.LE1);
            if (mpc1.Mods.IsEmpty()) mpc1.LoadModsFromDisk(AppDirectories.AppDataFolder);
            var mpc2 = LegendaryExplorerCore.PlotDatabase.PlotDatabases.GetModPlotContainerForGame(MEGame.LE2);
            if (mpc2.Mods.IsEmpty()) mpc2.LoadModsFromDisk(AppDirectories.AppDataFolder);
            var mpc3 = LegendaryExplorerCore.PlotDatabase.PlotDatabases.GetModPlotContainerForGame(MEGame.LE3);
            if (mpc3.Mods.IsEmpty()) mpc3.LoadModsFromDisk(AppDirectories.AppDataFolder);
        }

        private static void initCoreLib()
        {
#if DEBUG
            MemoryAnalyzer.IsTrackingMemory = true;
#endif
            static void packageSaveFailed(string message)
            {
                // I'm not sure if this requires ui thread since it's win32 but i'll just make sure
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message);
                });
            }
#if DEBUG
            // This makes LECLog messages go to the debug console.
            Log.Logger = CreateLogger();
#endif
            LegendaryExplorerCoreLib.InitLib(TaskScheduler.FromCurrentSynchronizationContext(), packageSaveFailed, Log.Logger);
            CoreLibSettingsBridge.MapSettingsIntoBridge();
            PackageSaver.CheckME3Running = () =>
            {
                GameController.TryGetMEProcess(MEGame.ME3, out var me3Proc);
                return me3Proc != null;
            };
            PackageSaver.NotifyRunningTOCUpdateRequired = GameController.SendME3TOCUpdateMessage;
        }

#if DEBUG
        /// <summary>
        /// Creates an ILogger for ME3Tweaks Mod Manager. This does NOT assign it to the Log.Logger instance.
        /// </summary>
        /// <returns></returns>
        public static ILogger CreateLogger()
        {

            var loggerConfig = new LoggerConfiguration();
            loggerConfig = loggerConfig.WriteTo.Debug();
            return loggerConfig.CreateLogger();
        }
#endif

        /// <summary>
        /// Invoked when a second instance of Legendary Explorer is opened
        /// </summary>
        /// <param name="args"></param>
        public static void HandleDuplicateInstanceArgs(string[] args)
        {
            if (IsLoaded)
            {
                if (args.Length is 0)
                {
                    App.Instance.MainWindow.SetForegroundWindow();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CommandLineArgs.CreateCLIHandler().InvokeAsync(args);
                    });
                }
            }
            else
            {
                Arguments.Enqueue(args);
            }
        }
    }
}
