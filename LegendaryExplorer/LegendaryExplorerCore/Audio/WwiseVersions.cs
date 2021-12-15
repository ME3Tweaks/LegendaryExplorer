using System;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Audio
{
    /// <summary>
    /// Contains info on the Wwise version for each game
    /// </summary>
    public static class WwiseVersions
    {
        /// <summary>
        /// Gets the full Wwise version string for a game, as found in the Wwise executable's version info
        /// </summary>
        /// <param name="game">Game to get version for</param>
        /// <returns>Wwise version string, or null if game does not use Wwise</returns>
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

        /// <summary>
        /// Gets the 4-digit Wwise version number for a game
        /// </summary>
        /// <param name="game">Game to get version for</param>
        /// <returns>Wwise version, or null if game does not use Wwise</returns>
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

        /// <summary>
        /// Checks if a WwiseCLI.exe file is of the correct version.
        /// This method checks that the filename is WwiseCLI.exe, and that the Product Version is correct
        /// </summary>
        /// <remarks>This method returns false if file does not exist, or game does not use Wwise</remarks>
        /// <param name="game">Game to check version against</param>
        /// <param name="pathToWwiseExe">Path to a WwiseCLI.exe executable</param>
        /// <returns>True if version is correct, false otherwise</returns>
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
