using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Audio
{
    public static class WwiseVersions
    {
        public static string WwiseFullVersion(MEGame game) => game switch
        {
            MEGame.ME1 => null,
            MEGame.ME2 => "2009.1.0.3143",
            MEGame.ME3 => "2010.3.3.3773",
            MEGame.LE1 => null,
            MEGame.LE2 => "2019.1.6.7110",
            MEGame.LE3 => "2019.1.6.7110",
            _ => null
        };

        public static int? WwiseVersion(MEGame game) => game switch
        {
            MEGame.ME1 => null,
            MEGame.ME2 => 3143,
            MEGame.ME3 => 3773,
            MEGame.LE1 => null,
            MEGame.LE2 => 7110,
            MEGame.LE3 => 7110,
            _ => null
        };

        public static bool IsCorrectWwiseVersion(MEGame game, string pathToWwiseExe)
        {
            if (File.Exists(pathToWwiseExe) && WwiseVersion(game) != null)
            {
                var fileInfo = new FileInfo(pathToWwiseExe);
                var versionInfo = FileVersionInfo.GetVersionInfo(pathToWwiseExe);
                string version = versionInfo.ProductVersion;
                return version == WwiseFullVersion(game) && fileInfo.Name.Equals("WwiseCLI.exe", StringComparison.CurrentCultureIgnoreCase);
            }

            return false;
        }
    }
}
