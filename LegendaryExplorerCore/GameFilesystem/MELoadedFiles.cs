using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.GameFilesystem
{
    public static class MELoadedFiles
    {
        private static readonly string[] ME1FilePatterns = { "*.u", "*.upk", "*.sfm" };
        private const string ME23LEXFilePattern = "*.pcc";

        private static readonly string[] ME23LEXFilePatternIncludeTFC = { "*.pcc", "*.tfc" };

        public static void InvalidateCaches()
        {
            cachedME1LoadedFiles = cachedME2LoadedFiles = cachedME3LoadedFiles = cachedLE1LoadedFiles = cachedLE2LoadedFiles = cachedLE3LoadedFiles = null;
            cachedME1TargetFiles = cachedME2TargetFiles = cachedME3TargetFiles = cachedLE1TargetFiles = cachedLE2TargetFiles = cachedLE3TargetFiles = null;
        }

        #region LoadedFiles
        private static CaseInsensitiveDictionary<string> cachedME1LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedME2LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedME3LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedLE1LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedLE2LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedLE3LoadedFiles;
        /// <summary>
        /// Gets a Dictionary of all loaded files in the given game. Key is the filename, value is file path
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static CaseInsensitiveDictionary<string> GetFilesLoadedInGame(MEGame game, bool forceReload = false, bool includeTFCs = false, bool includeAFCs = false)
        {
            if (!forceReload)
            {
                if (game == MEGame.ME1 && cachedME1LoadedFiles != null) return cachedME1LoadedFiles;
                if (game == MEGame.ME2 && cachedME2LoadedFiles != null)
                {
                    bool useCached = true;
                    useCached &= !includeTFCs || !cachedME2LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                    useCached &= !includeAFCs || !cachedME2LoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                    if (useCached) return cachedME2LoadedFiles;
                }
                if (game == MEGame.ME3 && cachedME3LoadedFiles != null)
                {
                    bool useCached = true;
                    useCached &= !includeTFCs || !cachedME3LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                    useCached &= !includeAFCs || !cachedME3LoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                    if (useCached) return cachedME3LoadedFiles;
                }
                if (game == MEGame.LE1 && cachedLE1LoadedFiles != null)
                {
                    bool useCached = true;
                    useCached &= !includeTFCs || !cachedLE1LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                    if (useCached) return cachedLE1LoadedFiles;
                }
                if (game == MEGame.LE2 && cachedLE2LoadedFiles != null)
                {
                    bool useCached = true;
                    useCached &= !includeTFCs || !cachedLE2LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                    useCached &= !includeAFCs || !cachedLE2LoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                    if (useCached) return cachedLE2LoadedFiles;
                }
                if (game == MEGame.LE3 && cachedLE3LoadedFiles != null)
                {
                    bool useCached = true;
                    useCached &= !includeTFCs || !cachedLE3LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                    useCached &= !includeAFCs || !cachedLE3LoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                    if (useCached) return cachedLE3LoadedFiles;
                }
            }

            //make dictionary from basegame files
            var loadedFiles = new CaseInsensitiveDictionary<string>();
            if (game == MEGame.UDK)
            {
                return loadedFiles;
            }

            var bgPath = MEDirectories.GetBioGamePath(game);
            if (bgPath != null)
            {
                foreach (string directory in GetEnabledDLCFolders(game).OrderBy(dir => GetMountPriority(dir, game)).Prepend(bgPath))
                {
                    foreach (string filePath in GetCookedFiles(game, directory, includeTFCs, includeAFCs))
                    {
                        string fileName = Path.GetFileName(filePath);
                        if (fileName != null) loadedFiles[fileName] = filePath;
                    }
                }
            }

            if (game == MEGame.ME1) cachedME1LoadedFiles = loadedFiles;
            else if (game == MEGame.ME2) cachedME2LoadedFiles = loadedFiles;
            else if (game == MEGame.ME3) cachedME3LoadedFiles = loadedFiles;
            else if (game == MEGame.LE1) cachedLE1LoadedFiles = loadedFiles;
            else if (game == MEGame.LE2) cachedLE2LoadedFiles = loadedFiles;
            else if (game == MEGame.LE3) cachedLE3LoadedFiles = loadedFiles;

            return loadedFiles;
        }
        #endregion

        #region All Game Files
        private static List<string> cachedME1TargetFiles;
        private static List<string> cachedME2TargetFiles;
        private static List<string> cachedME3TargetFiles;
        private static List<string> cachedLE1TargetFiles;
        private static List<string> cachedLE2TargetFiles;
        private static List<string> cachedLE3TargetFiles;

        /// <summary>
        /// Gets a Dictionary of all loaded files in the given game. Key is the filename, value is file path
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static List<string> GetAllGameFiles(string gamePath, MEGame game, bool forceReload = false, bool includeTFC = false)
        {
            if (!forceReload)
            {
                if (game == MEGame.ME1 && cachedME1TargetFiles != null) return cachedME1TargetFiles;
                if (game == MEGame.ME2 && cachedME2TargetFiles != null) return cachedME2TargetFiles;
                if (game == MEGame.ME3 && cachedME3TargetFiles != null) return cachedME3TargetFiles;
                if (game == MEGame.LE1 && cachedLE1TargetFiles != null) return cachedLE1TargetFiles;
                if (game == MEGame.LE2 && cachedLE2TargetFiles != null) return cachedLE2TargetFiles;
                if (game == MEGame.LE3 && cachedLE3TargetFiles != null) return cachedLE3TargetFiles;
            }

            //make dictionary from basegame files
            if (MEDirectories.GetDefaultGamePath(game) == null)
                return new List<string>(); // Game path not set!

            var loadedFiles = new List<string>(2000);
            foreach (string directory in MELoadedFiles.GetEnabledDLCFolders(game, gamePath).OrderBy(dir => GetMountPriority(dir, game)).Prepend(MEDirectories.GetBioGamePath(game, gamePath)))
            {
                foreach (string filePath in GetCookedFiles(game, directory, includeTFC))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName != null) loadedFiles.Add(filePath);
                }
            }

            if (game == MEGame.ME1) cachedME1TargetFiles = loadedFiles;
            if (game == MEGame.ME2) cachedME2TargetFiles = loadedFiles;
            if (game == MEGame.ME3) cachedME3TargetFiles = loadedFiles;
            if (game == MEGame.LE1) cachedLE1TargetFiles = loadedFiles;
            if (game == MEGame.LE2) cachedLE2TargetFiles = loadedFiles;
            if (game == MEGame.LE3) cachedLE3TargetFiles = loadedFiles;

            return loadedFiles;
        }
        #endregion

        public static IEnumerable<string> GetDLCFiles(MEGame game, string dlcName)
        {
            var dlcPath = MEDirectories.GetDLCPath(game);
            if (dlcPath != null)
            {
                string specificDlcPath = Path.Combine(dlcPath, dlcName);
                if (Directory.Exists(specificDlcPath))
                {
                    return GetCookedFiles(game, specificDlcPath);
                }
            }

            return Enumerable.Empty<string>();
        }

        // this should have a null check on Biogamepath to avoid throwing an exception
        public static IEnumerable<string> GetAllFiles(MEGame game, bool includeTFCs = false, bool includeAFCs = false) => GetEnabledDLCFolders(game).Prepend(MEDirectories.GetBioGamePath(game)).SelectMany(directory => GetCookedFiles(game, directory, includeTFCs, includeAFCs));
        // this should have a null check on Biogamepath to void throwing an exception
        public static IEnumerable<string> GetOfficialFiles(MEGame game, bool includeTFCs = false, bool includeAFCs = false) => GetOfficialDLCFolders(game).Prepend(MEDirectories.GetBioGamePath(game)).SelectMany(directory => GetCookedFiles(game, directory, includeTFCs, includeAFCs));

        public static IEnumerable<string> GetCookedFiles(MEGame game, string directory, bool includeTFCs = false, bool includeAFCs = false)
        {
            if (game == MEGame.ME1)
                return ME1FilePatterns.SelectMany(pattern => Directory.EnumerateFiles(Path.Combine(directory, "CookedPC"), pattern, SearchOption.AllDirectories));

            List<string> extensions = new List<string>();
            if (includeTFCs) extensions.Add("*.tfc");
            if (includeAFCs) extensions.Add("*.afc");
            extensions.Add("*.pcc"); //This is last, as any of the lookup methods will see if any of these files types exist in-order. By putting pcc's last, the lookups will be searched first when using
            //includeTFC or includeAFC.
            return extensions.SelectMany(pattern => Directory.EnumerateFiles(Path.Combine(directory, game == MEGame.ME2 ? "CookedPC" : "CookedPCConsole" ), pattern, SearchOption.AllDirectories));
        }


        // These methods should check DLCPath to avoid throwing exception

        /// <summary>
        /// Gets the base DLC directory of each unpacked DLC/mod that will load in game (eg. C:\Program Files (x86)\Origin Games\Mass Effect 3\BIOGame\DLC\DLC_EXP_Pack001)
        /// Directory Override is used to use a custom path, for things like TFC Compactor, where the directory ME3Exp is pointing to may not be the one you want to use.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetEnabledDLCFolders(MEGame game, string gameDirectoryOverride = null) =>
            Directory.Exists(MEDirectories.GetDLCPath(game, gameDirectoryOverride))
                ? Directory.EnumerateDirectories(MEDirectories.GetDLCPath(game,gameDirectoryOverride)).Where(dir => IsEnabledDLC(dir, game))
                : Enumerable.Empty<string>();
        public static IEnumerable<string> GetOfficialDLCFolders(MEGame game) =>
            Directory.Exists(MEDirectories.GetDLCPath(game))
                ? Directory.EnumerateDirectories(MEDirectories.GetDLCPath(game)).Where(dir => IsOfficialDLC(dir, game))
                : Enumerable.Empty<string>();

        public static string GetMountDLCFromDLCDir(string dlcDirectory, MEGame game) => Path.Combine(dlcDirectory, game == MEGame.ME2 ? "CookedPC" : "CookedPCConsole", "Mount.dlc");

        public static bool IsEnabledDLC(string dir, MEGame game)
        {
            string dlcName = Path.GetFileName(dir);
            if (game == MEGame.ME1)
            {
                return ME1Directory.OfficialDLC.Contains(dlcName) || File.Exists(Path.Combine(dir, "AutoLoad.ini"));
            }
            return dlcName.StartsWith("DLC_") && File.Exists(GetMountDLCFromDLCDir(dir, game));
        }

        public static bool IsOfficialDLC(string dir, MEGame game)
        {
            string dlcName = Path.GetFileName(dir);
            return MEDirectories.OfficialDLC(game).Contains(dlcName);
        }

        public static int GetMountPriority(string dlcDirectory, MEGame game)
        {
            if (game == MEGame.ME1)
            {
                int idx = 1 + ME1Directory.OfficialDLC.IndexOf(Path.GetFileName(dlcDirectory));
                if (idx > 0)
                {
                    return idx;
                }
                //is mod
                string autoLoadPath = Path.Combine(dlcDirectory, "AutoLoad.ini");
                var dlcAutoload = DuplicatingIni.LoadIni(autoLoadPath);
                return Convert.ToInt32(dlcAutoload["ME1DLCMOUNT"]["ModMount"].Value); // Should we  try catch this to avoid hitting an exception on malformed mods? Like DLC_xMeow
            }
            return MountFile.GetMountPriority(GetMountDLCFromDLCDir(dlcDirectory, game));
        }

        public static string GetDLCNameFromDir(string dlcDirectory) => Path.GetFileName(dlcDirectory).Substring(4);
    }
}
