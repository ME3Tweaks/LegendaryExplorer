using LegendaryExplorerCore.Packages;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Helpers;
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
        /// <summary>
        /// The URL to the repository for Legendary Explorer (Core)
        /// </summary>
        public static string RepositoryURL => "http://github.com/ME3Tweaks/LegendaryExplorer/";
        /// <summary>
        /// The URL to send users to for support for Legendary Explorer (Core)
        /// </summary>
        public static string BugReportURL => $"{RepositoryURL}issues/";
        /// <summary>
        /// The synchronization context used when calling <c>Task.ContinueWithOnUIThread()</c> [<see cref="Helpers.TaskExtensions.ContinueWithOnUIThread"/>]. This may need to be set via <see cref="SetSynchronizationContext"/> before the library is initialized if you use the <c>ContinueWithOnUIThread</c> extension before initializing the library.
        /// </summary>
        public static TaskScheduler SYNCHRONIZATION_CONTEXT { get; private set; }

        /// <summary>
        /// Maps a game to its custom resources filename in the GameResources.zip embedded file
        /// </summary>
        /// <param name="game">Game to lookup the custom resource for</param>
        /// <returns>The resources package filename for supported games. Unsupported games default to returning ME3Resources.pcc.</returns>
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

        [Obsolete("This method is scheduled for removal in future builds")]
        internal static string GetVersion()
        {
            return "4.0.0.0"; //This is used by the TLK tool. We should probably change this to be more proper
        }

        private static bool initialized = false;

        /// <summary>
        /// Allows you to specify the synchronization context, for example, if you want to use <c>Task.ContinueWithOnUIThread()</c> before
        /// the library has been initialized.
        /// </summary>
        /// <remarks>
        /// <para>
        ///    If your application has a UI thread, you must obtain a task scheduler from the UI thread. For WPF, you can call <see cref="TaskScheduler.FromCurrentSynchronizationContext"/> from a UI thread, and then pass that object to this method.
        /// </para>
        /// </remarks>
        /// <param name="scheduler">TaskScheduler generated from a UI thread. If your application doesn't have a UI thread, passing in any task scheduler should be fine.</param>
        public static void SetSynchronizationContext(TaskScheduler scheduler)
        {
            SYNCHRONIZATION_CONTEXT = scheduler;
        }


        /// <summary>
        /// Initializes the LegendaryExplorerCore library for use. Call this before using anything in this library. It registers things such as package loaders, initializes the object databases, and other startup tasks
        /// </summary>
        /// <param name="uiSyncContext">Synchronization context for use with <c>Task.ContineWithOnUIThread()</c>. See <see cref="SetSynchronizationContext"/> for more information.
        /// If a synchronization context was already specified before this library was initialized, this value is ignored.
        /// </param>
        /// <param name="packageSavingFailed">Delegate that invoked when a package fails to save</param>
        /// <param name="logger">Serilog logger to use for logging operations. If null, no logging is performed.</param>
        public static void InitLib(TaskScheduler uiSyncContext, Action<string> packageSavingFailed = null, ILogger logger = null)
        {
            if (initialized) return;
            LECLog.logger = logger;
            SYNCHRONIZATION_CONTEXT ??= uiSyncContext;
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

        /// <summary>
        /// ????? NEEDS DOCUMENTED
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Always true in debug builds. Always false in non-debug builds.
        /// </summary>
        public static bool IsDebug => true;
#else
        /// <summary>
        /// Always true in debug builds. Always false in non-debug builds.
        /// </summary>
        public static bool IsDebug => false;
#endif
    }
}
