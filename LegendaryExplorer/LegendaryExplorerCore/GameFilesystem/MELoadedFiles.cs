using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Static methods to get collections of files in a game directory
    /// </summary>
    public static class MELoadedFiles
    {
        private static readonly string[] ME1FilePatterns = { "*.u", "*.upk", "*.sfm" };
        private static readonly string[] UDKFilePatterns = { "*.u", "*.upk", "*.udk" };
        private const string ME23LEFilePattern = "*.pcc";
        private static readonly string[] ME23LEFilePatternIncludeTFC = { "*.pcc", "*.tfc" };

        private static readonly string FauxStartupPath = Path.Combine("DLC_METR_Patch01", "CookedPCConsole", "Startup.pcc");

        /// <summary>
        /// Invalidates the cache of loaded files for all games
        /// </summary>
        public static void InvalidateCaches()
        {
            cachedME1LoadedFiles = cachedME2LoadedFiles = cachedME3LoadedFiles = cachedLE1LoadedFiles = cachedLE2LoadedFiles = cachedLE3LoadedFiles = cachedUDKLoadedFiles = null;
            cachedME1TargetFiles = cachedME2TargetFiles = cachedME3TargetFiles = cachedLE1TargetFiles = cachedLE2TargetFiles = cachedLE3TargetFiles = cachedUDKTargetFiles = null;
        }

        #region LoadedFiles
        private static CaseInsensitiveDictionary<string> cachedME1LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedME2LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedME3LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedLE1LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedLE2LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedLE3LoadedFiles;
        private static CaseInsensitiveDictionary<string> cachedUDKLoadedFiles;

        /// <summary>
        /// Gets a dictionary of all loaded files in the given game. Key is the filename, value is file path. This data may be cached; to reload it, set the forceReload flag to true
        /// </summary>
        /// <param name="game">Game to get loaded files for</param>
        /// <param name="forceReload">If false, method may return a cache of loaded files. If true, cache will be regenerated</param>
        /// <param name="includeTFCs">If true, files with the .tfc extension will be included</param>
        /// <param name="includeAFCs">If true, files with the .afc extension will be included</param>
        /// <param name="gameRootOverride">Optional: override game path root</param>
        /// <param name="forceUseCached">Optional: Set to true to forcibly use the cached version if available; ignoring the tfc/afc check for rebuilding. Only use if you know what you're doing; this is to improve performance in certain scenarios</param>
        /// <returns>Case insensitive dictionary where key is filename and value is file path</returns>
        public static CaseInsensitiveDictionary<string> GetFilesLoadedInGame(MEGame game, bool forceReload = false, bool includeTFCs = false, bool includeAFCs = false, string gameRootOverride = null, bool forceUseCached = false)
        {
            //Override: Do not use cached items
            if (!forceReload && gameRootOverride == null)
            {
                switch (game)
                {
                    case MEGame.ME1 when cachedME1LoadedFiles != null:
                        return cachedME1LoadedFiles;
                    case MEGame.ME2 when cachedME2LoadedFiles != null:
                    {
                        bool useCached = true;
                        if (!forceUseCached)
                        {
                            useCached &= !includeTFCs || cachedME2LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                            useCached &= !includeAFCs || cachedME2LoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                        }

                        if (useCached) return cachedME2LoadedFiles;
                        break;
                    }
                    case MEGame.ME3 when cachedME3LoadedFiles != null:
                    {
                        bool useCached = true;
                        if (!forceUseCached)
                        {
                            useCached &= !includeTFCs || cachedME3LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                            useCached &= !includeAFCs || cachedME3LoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                        }
                        if (useCached) return cachedME3LoadedFiles;
                        break;
                    }
                    case MEGame.LE1 when cachedLE1LoadedFiles != null:
                    {
                        bool useCached = true;
                        if (!forceUseCached)
                        {
                            useCached &= !includeTFCs || cachedLE1LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                        }
                        if (useCached) return cachedLE1LoadedFiles;
                        break;
                    }
                    case MEGame.LE2 when cachedLE2LoadedFiles != null:
                    {
                        bool useCached = true;
                        if (!forceUseCached)
                        {
                            useCached &= !includeTFCs || cachedLE2LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                            useCached &= !includeAFCs || cachedLE2LoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                        }
                        if (useCached) return cachedLE2LoadedFiles;
                        break;
                    }
                    case MEGame.LE3 when cachedLE3LoadedFiles != null:
                    {
                        bool useCached = true;
                        if (!forceUseCached)
                        {
                            useCached &= !includeTFCs || cachedLE3LoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                            useCached &= !includeAFCs || cachedLE3LoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                        }
                        if (useCached) return cachedLE3LoadedFiles;
                        break;
                    }
                    case MEGame.UDK when cachedUDKLoadedFiles is not null:
                    {
                        bool useCached = true;
                        if (!forceUseCached)
                        {
                            useCached &= !includeTFCs || cachedUDKLoadedFiles.Keys.Any(x => x.EndsWith(".tfc"));
                            useCached &= !includeAFCs || cachedUDKLoadedFiles.Keys.Any(x => x.EndsWith(".afc"));
                        }
                        if (useCached) return cachedUDKLoadedFiles;
                        break;
                    }
                }
            }

            //make dictionary from basegame files
            var loadedFiles = new CaseInsensitiveDictionary<string>();

            var bgPath = MEDirectories.GetBioGamePath(game, gameRootOverride);
            if (bgPath != null)
            {
                IEnumerable<string> directories;
                if (game is MEGame.UDK)
                {
                    directories = new[] { UDKDirectory.GetScriptPath(gameRootOverride), bgPath };
                }
                else
                {
                    directories = MELoadedDLC.GetEnabledDLCFolders(game, gameRootOverride)
                        .OrderBy(dir => MELoadedDLC.GetMountPriority(dir, game))
                        .Prepend(bgPath);
                }
                foreach (string directory in directories)
                {
                    foreach (string filePath in GetCookedFiles(game, directory, includeTFCs, includeAFCs))
                    {
                        string fileName = Path.GetFileName(filePath);
                        if (game == MEGame.LE3 && filePath.EndsWith(FauxStartupPath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue; // This file is not used by game and will break lots of stuff if we don't filter it out. This is a bug in how LE3 was cooked
                        }
                        if (fileName != null) loadedFiles[fileName] = filePath;
                    }
                }
            }

            if (gameRootOverride == null)
            {
                switch (game)
                {
                    // Cache results
                    case MEGame.ME1:
                        cachedME1LoadedFiles = loadedFiles;
                        break;
                    case MEGame.ME2:
                        cachedME2LoadedFiles = loadedFiles;
                        break;
                    case MEGame.ME3:
                        cachedME3LoadedFiles = loadedFiles;
                        break;
                    case MEGame.LE1:
                        cachedLE1LoadedFiles = loadedFiles;
                        break;
                    case MEGame.LE2:
                        cachedLE2LoadedFiles = loadedFiles;
                        break;
                    case MEGame.LE3:
                        cachedLE3LoadedFiles = loadedFiles;
                        break;
                    case MEGame.UDK:
                        cachedUDKLoadedFiles = loadedFiles;
                        break;
                }
            }

            return loadedFiles;
        }

        public static bool TryGetHighestMountedFile(IMEPackage pcc, out string filePath) => TryGetHighestMountedFile(pcc.Game, Path.GetFileName(pcc.FilePath), out filePath);

        public static bool TryGetHighestMountedFile(MEGame game, string fileName, out string filePath) => GetFilesLoadedInGame(game, true).TryGetValue(fileName ?? "", out filePath);

        #endregion

        private static List<string> cachedME1TargetFiles;
        private static List<string> cachedME2TargetFiles;
        private static List<string> cachedME3TargetFiles;
        private static List<string> cachedLE1TargetFiles;
        private static List<string> cachedLE2TargetFiles;
        private static List<string> cachedLE3TargetFiles;
        private static List<string> cachedUDKTargetFiles;

        /// <summary>
        /// Gets a list of all loaded files in the given game.
        /// </summary>
        /// <remarks>
        /// How does this differ from <see cref="GetFilesLoadedInGame"/>.Values.ToList()?
        /// GetFilesLoadedInGame handles edge cases and caching slightly better. I'm pretty sure M3 uses this one.
        /// </remarks>
        /// <param name="gamePath">Game root path. If null, default from MEDirectories will be used</param>
        /// <param name="game">Game to get all loaded files for</param>
        /// <param name="forceReload">If false, may return a cache of target files. If true, dictionary will be regenerated.</param>
        /// <param name="includeTFC">If true, files with the .tfc extension will be included</param>
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
                if (game == MEGame.UDK && cachedUDKTargetFiles != null) return cachedUDKTargetFiles;
            }

            //make dictionary from basegame files
            if (MEDirectories.GetDefaultGamePath(game) == null)
                return new List<string>(); // Game path not set!

            var loadedFiles = new List<string>(2000); IEnumerable<string> directories;
            string bgPath = MEDirectories.GetBioGamePath(game, gamePath);
            if (game is MEGame.UDK)
            {
                directories = new[] { UDKDirectory.GetScriptPath(gamePath), bgPath };
            }
            else
            {
                directories = MELoadedDLC.GetEnabledDLCFolders(game, gamePath)
                    .OrderBy(dir => MELoadedDLC.GetMountPriority(dir, game))
                    .Prepend(bgPath);
            }
            foreach (string directory in directories)
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
            if (game == MEGame.UDK) cachedUDKTargetFiles = loadedFiles;

            return loadedFiles;
        }

        /// <summary>
        /// Gets all package files in a specified DLC
        /// </summary>
        /// <param name="game">Game to search in</param>
        /// <param name="dlcName">Name of the DLC folder. Example: <c>DLC_MOD_EGM</c></param>
        /// <returns>Full paths to all package files in the specified DLC folder, empty enumerable if DLC does not exist</returns>
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

        /// <summary>
        /// Gets all files from a specified game, including basegame and all DLC folders.
        /// Package files will always be included, other extensions can be specified.
        /// </summary>
        /// <remarks>Includes files from mod added DLC folders</remarks>
        /// <param name="game">Game to get files for. Default game path is used.</param>
        /// <param name="includeTFCs">If true, files with the .tfc extension will also be included</param>
        /// <param name="includeAFCs">If true, files with the .afc extension will also be included</param>
        /// <param name="additionalExtensions">Array of additional extensions to be included. Only the extension should be passed in, EG: ".isb"</param>
        /// <returns>Full paths to all files in the specified game's folder</returns>
        public static IEnumerable<string> GetAllFiles(MEGame game, bool includeTFCs = false, bool includeAFCs = false, string[] additionalExtensions = null)
        {
            var path = MEDirectories.GetBioGamePath(game);
            if (path == null)
            {
                Debug.WriteLine($"Cannot get list of all files for game that cannot be found! Game: {game}. Returning empty list");
                return new List<string>();
            }
            IEnumerable<string> directories = game is MEGame.UDK ? new[] { UDKDirectory.ScriptPath, path } : MELoadedDLC.GetEnabledDLCFolders(game).Prepend(path);
            return directories.SelectMany(directory => GetCookedFiles(game, directory, includeTFCs, includeAFCs, additionalExtensions));
        }

        /// <summary>
        /// Gets all official files from a specified game, from basegame and official DLC folders.
        /// Package files will always be included, other extensions can be specified.
        /// </summary>
        /// <remarks>Does not get files in mod folders, use <see cref="GetAllFiles"/> to get mod-added files</remarks>
        /// <param name="game">Game to get files for. Default game path will be used.</param>
        /// <param name="includeTFCs">If true, files with the .tfc extension will also be included</param>
        /// <param name="includeAFCs">If true, files with the .afc extension will also be included</param>
        /// <param name="additionalExtensions">Array of additional extensions to be included. Only the extension should be passed in, EG: ".isb"</param>
        /// <returns>Full paths to all official files in the specified game's folder</returns>
        public static IEnumerable<string> GetOfficialFiles(MEGame game, bool includeTFCs = false, bool includeAFCs = false, string[] additionalExtensions = null)
        {
            var path = MEDirectories.GetBioGamePath(game);
            if (path == null)
            {
                Debug.WriteLine($"Cannot get list of official files for game that cannot be found! Game: {game}. Returning empty list");
                return new List<string>();
            }
            IEnumerable<string> directories = game is MEGame.UDK ? new[] { UDKDirectory.ScriptPath } : MELoadedDLC.GetOfficialDLCFolders(game).Prepend(path);
            return directories.SelectMany(directory => GetCookedFiles(game, directory, includeTFCs, includeAFCs, additionalExtensions));
        }

        /// <summary>
        /// Gets all game files from a specified BioGame or DLC folder. Package files will always be included, other extensions can be specified.
        /// Only returns files that are in the CookedPC subfolder of the input folder.
        /// </summary>
        /// <param name="game">Game you are searching files for. This determines extensions that are used.</param>
        /// <param name="directory">Path to DLC or Basegame directory to search in. Files will be enumerated from the CookedPC subfolder of this path.</param>
        /// <param name="includeTFCs">If true, files with the .tfc extension will be included</param>
        /// <param name="includeAFCs">If true, files with the .afc extension will be included</param>
        /// <param name="additionalExtensions">Array of additional extensions to be included. Only the extension should be passed in, EG: ".isb"</param>
        /// <returns>Full paths to all matching files in the input Cooked directory</returns>
        public static IEnumerable<string> GetCookedFiles(MEGame game, string directory, bool includeTFCs = false, bool includeAFCs = false, string[] additionalExtensions = null)
        {
            if (game == MEGame.ME1)
            {
                var patterns = ME1FilePatterns.ToList();
                if (additionalExtensions != null)
                    patterns.AddRange(additionalExtensions);
                return patterns.SelectMany(pattern => Directory.EnumerateFiles(Path.Combine(directory, game.CookedDirName()), pattern, SearchOption.AllDirectories));
            }
            if (game is MEGame.UDK)
            {
                return UDKFilePatterns.SelectMany(pattern => Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories));
            }

            List<string> extensions = new List<string>();
            if (includeTFCs) extensions.Add("*.tfc");
            if (includeAFCs) extensions.Add("*.afc");
            if (additionalExtensions != null)
                extensions.AddRange(additionalExtensions.Select(x => $"*{x}"));
            extensions.Add("*.pcc"); //This is last, as any of the lookup methods will see if any of these files types exist in-order.
                                         //By putting PCCs last, the lookups will be searched first when using includeTFC or includeAFC.
            return extensions.SelectMany(pattern => Directory.EnumerateFiles(Path.Combine(directory, game.CookedDirName()), pattern, SearchOption.AllDirectories));
        }

        /// <summary>
        /// Gets the base DLC directory of each unpacked DLC that will load in game
        /// </summary>
        /// <param name="game">Game to get DLC for</param>
        /// <param name="gameRootOverride">Optional: override game path root</param>
        /// <returns>An IEnumerable of full paths to enabled DLC folders (strings)</returns>
        [Obsolete("This method is deprecated, use the same named method in MELoadedDLC instead")]
        public static IEnumerable<string> GetEnabledDLCFolders(MEGame game, string gameRootOverride = null) =>
            MELoadedDLC.GetEnabledDLCFolders(game, gameRootOverride);

        /// <summary>
        /// Gets the base DLC directory of each unpacked official DLC
        /// </summary>
        /// <param name="game">Game to get DLC for</param>
        /// <returns>An IEnumerable of full paths to official DLC folders (strings)</returns>
        [Obsolete("This method is deprecated, use the same named method in MELoadedDLC instead")]
        public static IEnumerable<string> GetOfficialDLCFolders(MEGame game) => MELoadedDLC.GetOfficialDLCFolders(game);

        /// <summary>
        /// Gets the path to a Mount.dlc file from a given DLC folder
        /// </summary>
        /// <remarks>This method does not check if Mount.dlc exists. If given a Game 1 DLC folder, it will still provide a path to a Mount.dlc file.</remarks>
        /// <param name="dlcDirectory">Path to a DLC folder</param>
        /// <param name="game">Game this DLC is for</param>
        /// <returns>Path to where Mount.dlc file should be</returns>
        [Obsolete("This method is deprecated, use the same named method in MELoadedDLC instead")]
        public static string GetMountDLCFromDLCDir(string dlcDirectory, MEGame game) =>
            MELoadedDLC.GetMountDLCFromDLCDir(dlcDirectory, game);

        /// <summary>
        /// Checks whether a DLC folder is enabled or not. Checks for existence of mount file and if folder starts with "DLC_"
        /// </summary>
        /// <param name="dir">Path to a DLC folder</param>
        /// <param name="game">Game this DLC is for</param>
        /// <returns>True if DLC is enabled, false otherwise</returns>
        [Obsolete("This method is deprecated, use the same named method in MELoadedDLC instead")]
        public static bool IsEnabledDLC(string dir, MEGame game)
        {
            return MELoadedDLC.IsEnabledDLC(dir, game);
        }

        /// <summary>
        /// Checks whether a DLC directory is an official DLC
        /// </summary>
        /// <remarks>Only checks based on folder name, not folder contents</remarks>
        /// <param name="dir">Path to a DLC folder</param>
        /// <param name="game">Game to check official DLC for</param>
        /// <returns>True if path is directory to an official DLC, false otherwise</returns>
        [Obsolete("This method is deprecated, use the same named method in MELoadedDLC instead")]
        public static bool IsOfficialDLC(string dir, MEGame game)
        {
            return MELoadedDLC.IsOfficialDLC(dir, game);
        }

        /// <summary>
        /// Finds the mount priority of a given DLC folder. Uses AutoLoad.ini or mount file depending on game.
        /// </summary>
        /// <param name="dlcDirectory">Path to a DLC folder</param>
        /// <param name="game">Game of DLC</param>
        /// <returns>Mount priority</returns>
        [Obsolete("This method is deprecated, use the same named method in MELoadedDLC instead")]
        public static int GetMountPriority(string dlcDirectory, MEGame game)
        {
            return MELoadedDLC.GetMountPriority(dlcDirectory, game);
        }

        /// <summary>
        /// Removes the "DLC_" prefix from a DLC folder path
        /// </summary>
        /// <remarks>If the folder name is "DLC_MOD_", this method will not remove the "MOD_"</remarks>
        /// <param name="dlcDirectory">Path to a DLC folder</param>
        /// <returns>Folder name without "DLC_"</returns>
        [Obsolete("This method is deprecated, use the same named method in MELoadedDLC instead")]
        public static string GetDLCNameFromDir(string dlcDirectory) => MELoadedDLC.GetDLCNameFromDir(dlcDirectory);

        /// <summary>
        /// Gets all the enabled DLC in a game along with the mount value.
        /// </summary>
        /// <returns>Dictionary of DLC folder name to mount priority</returns>
        [Obsolete("This method is deprecated, use the same named method in MELoadedDLC instead")]
        public static Dictionary<string, int> GetDLCNamesWithMounts(MEGame game)
        {
            return MELoadedDLC.GetDLCNamesWithMounts(game);
        }
    }
}
