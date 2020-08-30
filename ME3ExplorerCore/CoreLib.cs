using System;
using System.Collections.Generic;
using System.Text;

namespace ME3ExplorerCore
{
    public static class CoreLib
    {

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
