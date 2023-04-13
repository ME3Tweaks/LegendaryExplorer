using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Static methods to get information on DLC installed in a game
    /// </summary>
    public static class MELoadedDLC
    {
        /// <summary>
        /// Gets the base DLC directory of each unpacked DLC that will load in game. Includes
        /// </summary>
        /// <param name="game">Game to get DLC for</param>
        /// <param name="gameDirectoryOverride">Optional: override game path root</param>
        /// <returns>An IEnumerable of full paths to enabled DLC folders (strings)</returns>
        public static IEnumerable<string> GetEnabledDLCFolders(MEGame game, string gameDirectoryOverride = null) =>
            Directory.Exists(MEDirectories.GetDLCPath(game, gameDirectoryOverride))
                ? Directory.EnumerateDirectories(MEDirectories.GetDLCPath(game, gameDirectoryOverride)).Where(dir => IsEnabledDLC(dir, game))
                : Enumerable.Empty<string>();

        /// <summary>
        /// Gets the base DLC directory of each unpacked official DLC
        /// </summary>
        /// <param name="game">Game to get DLC for</param>
        /// <param name="gameDirectoryOverride">Optional: override game path root</param>
        /// <returns>An IEnumerable of full paths to official DLC folders (strings)</returns>
        public static IEnumerable<string> GetOfficialDLCFolders(MEGame game, string gameDirectoryOverride = null) =>
            Directory.Exists(MEDirectories.GetDLCPath(game, gameDirectoryOverride))
                ? Directory.EnumerateDirectories(MEDirectories.GetDLCPath(game, gameDirectoryOverride)).Where(dir => IsOfficialDLC(dir, game))
                : Enumerable.Empty<string>();

        /// <summary>
        /// Gets the path to a Mount.dlc file from a given DLC folder
        /// </summary>
        /// <remarks>This method does not check if Mount.dlc exists. If given a Game 1 DLC folder, it will still provide a path to a Mount.dlc file.</remarks>
        /// <param name="dlcDirectory">Path to a DLC folder</param>
        /// <param name="game">Game this DLC is for</param>
        /// <returns>Path to where Mount.dlc file should be</returns>
        public static string GetMountDLCFromDLCDir(string dlcDirectory, MEGame game) => Path.Combine(dlcDirectory, game.CookedDirName(), "Mount.dlc");

        /// <summary>
        /// Checks whether a DLC folder is enabled or not. Checks for existence of mount file and if folder starts with "DLC_"
        /// </summary>
        /// <param name="dir">Path to a DLC folder</param>
        /// <param name="game">Game this DLC is for</param>
        /// <returns>True if DLC is enabled, false otherwise</returns>
        public static bool IsEnabledDLC(string dir, MEGame game)
        {
            string dlcName = Path.GetFileName(dir);
            if (game == MEGame.ME1)
            {
                return ME1Directory.OfficialDLC.Contains(dlcName) || File.Exists(Path.Combine(dir, "AutoLoad.ini"));
            }
            else if (game == MEGame.LE1) return dlcName.StartsWith("DLC_MOD") && File.Exists(Path.Combine(dir, "AutoLoad.ini"));
            return (dlcName.StartsWith("DLC_") || game == MEGame.ME2) && File.Exists(GetMountDLCFromDLCDir(dir, game));
        }

        /// <summary>
        /// Checks whether a DLC directory is an official DLC
        /// </summary>
        /// <remarks>Only checks based on folder name, not folder contents</remarks>
        /// <param name="dir">Path to a DLC folder</param>
        /// <param name="game">Game to check official DLC for</param>
        /// <returns>True if path is directory to an official DLC, false otherwise</returns>
        public static bool IsOfficialDLC(string dir, MEGame game)
        {
            string dlcName = Path.GetFileName(dir);
            if (game == MEGame.LE1) return false;
            return MEDirectories.OfficialDLC(game).Contains(dlcName);
        }

        /// <summary>
        /// Finds the mount priority of a given DLC folder. Uses AutoLoad.ini or mount file depending on game.
        /// </summary>
        /// <param name="dlcDirectory">Path to a DLC folder</param>
        /// <param name="game">Game of DLC</param>
        /// <returns>Mount priority</returns>
        public static int GetMountPriority(string dlcDirectory, MEGame game)
        {
            if (game.IsGame1())
            {
                // Check for priority of ME1 official DLC
                if (game is MEGame.ME1)
                {
                    int idx = 1 + ME1Directory.OfficialDLC.IndexOf(Path.GetFileName(dlcDirectory));
                    if (idx > 0)
                    {
                        return idx;
                    }
                }
                // DLC folder is Game1 mod
                string autoLoadPath = Path.Combine(dlcDirectory, "AutoLoad.ini");
                var dlcAutoload = DuplicatingIni.LoadIni(autoLoadPath);
                if (int.TryParse(dlcAutoload["ME1DLCMOUNT"]["ModMount"].Value, out var value))
                {
                    return value;
                }

                LECLog.Error($@"Invalid mount priority value in Autoload.ini: {dlcAutoload["ME1DLCMOUNT"]["ModMount"].Value}");
                return 0;
            }
            return MountFile.GetMountPriority(GetMountDLCFromDLCDir(dlcDirectory, game));
        }

        /// <summary>
        /// Removes the "DLC_" prefix from a DLC folder path
        /// </summary>
        /// <remarks>If the folder name is "DLC_MOD_", this method will not remove the "MOD_"</remarks>
        /// <param name="dlcDirectory">Path to a DLC folder</param>
        /// <returns>Folder name without "DLC_"</returns>
        public static string GetDLCNameFromDir(string dlcDirectory) => Path.GetFileName(dlcDirectory).Substring(4);

        /// <summary>
        /// Gets all the enabled DLC in a game along with the mount value.
        /// </summary>
        /// <returns>Dictionary of DLC folder name to mount priority</returns>
        public static Dictionary<string, int> GetDLCNamesWithMounts(MEGame game, string gameDirectoryOverride = null)
        {
            var dlcs = GetEnabledDLCFolders(game, gameDirectoryOverride);
            var mountlist = new Dictionary<string, int>();
            foreach (var d in dlcs)
            {
                var m = GetMountPriority(d, game);
                mountlist.Add(Path.GetFileName(d), m);
            }
            return mountlist;
        }

        /// <summary>
        /// Gets the list of enabled DLC in load order, that is lowest-to-highest mount priority. This does not factor in SFAR packed DLCs from ME3.
        /// </summary>
        /// <param name="game">The game to enumerate DLC for</param>
        /// <param name="gameDirectoryOverride">The directory of the game. If null, the default path will be used</param>
        /// <returns></returns>
        public static List<string> GetDLCNamesInMountOrder(MEGame game, string gameDirectoryOverride = null)
        {
            var list = GetDLCNamesWithMounts(game, gameDirectoryOverride);
            return list.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        }
    }
}