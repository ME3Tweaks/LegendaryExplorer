using ME3ExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Text;

namespace ME3ExplorerCore
{
    /// <summary>
    /// Entrypoint for the ME3Explorer Library
    /// </summary>
    public static class CoreLib
    {
        public static string RepositoryURL => "http://github.com/ME3Tweaks/ME3Explorer/";
        public static string BugReportURL => $"{RepositoryURL}issues/";

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

#if DEBUG
        public static bool IsDebug => true;
#else
        public static bool IsDebug => false;
#endif
    }
}
