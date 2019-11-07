using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public static partial class APIKeys
    {
        public static bool HasAppCenterKey => typeof(APIKeys).GetProperty("Private_AppCenter") != null;
        public static string AppCenterKey => (string)typeof(APIKeys).GetProperty("Private_AppCenter").GetValue(typeof(APIKeys));
    }
}
