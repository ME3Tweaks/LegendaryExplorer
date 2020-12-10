using ME3ExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore
{
    /// <summary>
    /// Entrypoint for the ME3Explorer Library
    /// </summary>
    public static class CoreLib
    {
        public static string RepositoryURL => "http://github.com/ME3Tweaks/ME3Explorer/";
        public static string BugReportURL => $"{RepositoryURL}issues/";
        public static TaskScheduler SYNCHRONIZATION_CONTEXT { get; private set; }


        public static string CustomResourceFileName(MEGame game) => game switch
        {
            MEGame.ME3 => "ME3Resources.pcc",
            MEGame.ME2 => "ME2Resources.pcc",
            MEGame.ME1 => "ME1Resources.upk",
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
        public static void InitLib(TaskScheduler uiThreadScheduler, Action<string> packageSavingFailed = null)
        {
            if (initialized) return;
            if (SYNCHRONIZATION_CONTEXT == null)
            {
                SYNCHRONIZATION_CONTEXT = uiThreadScheduler;
            }
            MEPackageHandler.Initialize();
            PackageSaver.Initialize();
            PackageSaver.PackageSaveFailedCallback = packageSavingFailed;
            Action[] jsonLoaders =
            {
                ME1UnrealObjectInfo.loadfromJSON,
                ME2UnrealObjectInfo.loadfromJSON,
                ME3UnrealObjectInfo.loadfromJSON
            };
            Parallel.ForEach(jsonLoaders, action => action());
            CoreLibSettings.Instance = new CoreLibSettings();
            initialized = true;
        }

#if DEBUG
        public static bool IsDebug => true;
#else
        public static bool IsDebug => false;
#endif
    }
}
