using LegendaryExplorerCore.Packages;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Serilog;
using Serilog.Core;

namespace LegendaryExplorerCore
{
    /// <summary>
    /// Entrypoint for the LegendaryExplorerCore Library
    /// </summary>
    public static class LegendaryExplorerCoreLib

    {
        public static string RepositoryURL => "http://github.com/ME3Tweaks/LegendaryExplorer/";
        public static string BugReportURL => $"{RepositoryURL}issues/";
        public static TaskScheduler SYNCHRONIZATION_CONTEXT { get; private set; }


        public static string CustomResourceFileName(MEGame game) => game switch
        {
            MEGame.ME3 => "ME3Resources.pcc",
            MEGame.ME2 => "ME2Resources.pcc",
            MEGame.ME1 => "ME1Resources.upk",
            MEGame.LE3 => "LE3Resources.pcc",
            MEGame.LE2 => "LE2Resources.pcc",
            MEGame.LE1 => "LE1Resources.pcc",
            MEGame.UDK => "UDKResources.upk",
            _ => "ME3Resources.pcc"
        };

        internal static string GetVersion()
        {
            return "4.0.0.0"; //This is used by the TLK tool. We should probably change this to be more proper
        }

        private static bool initialized = false;

        /// <summary>
        /// Allows you to specify the synchronization context, for example, if you want to use ContinueWithOnUIThread() before
        /// the library has been initialized.
        /// </summary>
        /// <param name="scheduler"></param>
        public static void SetSynchronizationContext(TaskScheduler scheduler)
        {
            SYNCHRONIZATION_CONTEXT = scheduler;
        }

        /// <summary>
        /// Call this before using anything in this library. It registers things such as package loaders
        /// </summary>
        public static void InitLib(TaskScheduler uiThreadScheduler, Action<string> packageSavingFailed = null, ILogger logger = null)
        {
            if (initialized) return;
            LECLog.logger = logger;
            if (SYNCHRONIZATION_CONTEXT == null)
            {
                SYNCHRONIZATION_CONTEXT = uiThreadScheduler;
            }
            LECLog.Information(@"Initializing LegendaryExplorerCore library");
            MEPackageHandler.Initialize();
            PackageSaver.Initialize();
            PackageSaver.PackageSaveFailedCallback = packageSavingFailed;
            Action<string>[] jsonLoaders =
            {
                ME1UnrealObjectInfo.loadfromJSON,
                ME2UnrealObjectInfo.loadfromJSON,
                ME3UnrealObjectInfo.loadfromJSON,
                // Todo: LE Load
                // Todo: Maybe not load all of these as they use a lot of memory, like 40MB each
                // For 6 games that will be pretty heavy
                // Maybe require 
                // Here for now
                LE1UnrealObjectInfo.loadfromJSON,
                LE2UnrealObjectInfo.loadfromJSON,
                LE3UnrealObjectInfo.loadfromJSON,
            };
            Parallel.ForEach(jsonLoaders, action => action(null));
            if (!OodleHelper.EnsureOodleDll())
            {
                Debug.WriteLine("Oodle decompression library not available. Make sure game is installed!");
            }
            initialized = true;
        }

        public static string CustomPlotFileName(MEGame game) => game switch
        {
            MEGame.ME3 => "le3.json",
            MEGame.ME2 => "le2.json",
            MEGame.ME1 => "le1.json",
            MEGame.LE3 => "le3.json",
            MEGame.LE2 => "le2.json",
            MEGame.LE1 => "le1.json",
            _ => "le3.json"
        };

#if DEBUG
        public static bool IsDebug => true;
#else
        public static bool IsDebug => false;
#endif
    }
}
