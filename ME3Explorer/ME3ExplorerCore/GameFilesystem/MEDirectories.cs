using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.GameFilesystem
{
    public static class MEDirectories
    {
        public static string GetCookedPath(MEGame game, string gamePathRoot = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.GetCookedPCPath(gamePathRoot);
                case MEGame.ME2:
                    return ME2Directory.GetCookedPCPath(gamePathRoot);
                case MEGame.ME3:
                    return ME3Directory.GetCookedPCPath(gamePathRoot);
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }
        
        /// <summary>
        /// Gets the default game path for the listed game.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string GetDefaultGamePath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.DefaultGamePath;
                case MEGame.ME2:
                    return ME2Directory.DefaultGamePath;
                case MEGame.ME3:
                    return ME3Directory.DefaultGamePath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string GetBioGamePath(MEGame game, string gamePathRoot = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.GetBioGamePath(gamePathRoot);
                case MEGame.ME2:
                    return ME2Directory.GetBioGamePath(gamePathRoot);
                case MEGame.ME3:
                    return ME3Directory.GetBioGamePath(gamePathRoot);
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string GetDLCPath(MEGame game, string gamePathRoot = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.GetDLCPath(gamePathRoot);
                case MEGame.ME2:
                    return ME2Directory.GetDLCPath(gamePathRoot);
                case MEGame.ME3:
                    return ME3Directory.GetDLCPath(gamePathRoot);
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }


        public static string GetExecutablePath(MEGame game, string gamePathRoot = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.GetExecutablePath(gamePathRoot);
                case MEGame.ME2:
                    return ME2Directory.GetExecutablePath(gamePathRoot);
                case MEGame.ME3:
                    return ME3Directory.GetExecutablePath(gamePathRoot);
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string GetExecutableFolderPath(MEGame game, string gamePathRoot = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.GetExecutableDirectory(gamePathRoot);
                case MEGame.ME2:
                    return ME2Directory.GetExecutableDirectory(gamePathRoot);
                case MEGame.ME3:
                    return ME3Directory.GetExecutableDirectory(gamePathRoot);
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string GetASIPath(MEGame game, string gamePathRoot = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.GetASIPath(gamePathRoot);
                case MEGame.ME2:
                    return ME2Directory.GetASIPath(gamePathRoot);
                case MEGame.ME3:
                    return ME3Directory.GetASIPath(gamePathRoot);
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string GetTextureModMarkerPath(MEGame game, string gamePathRoot)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.GetTextureModMarkerPath(gamePathRoot);
                case MEGame.ME2:
                    return ME2Directory.GetTextureModMarkerPath(gamePathRoot);
                case MEGame.ME3:
                    return ME3Directory.GetTextureModMarkerPath(gamePathRoot); 
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static ReadOnlyCollection<string> ExecutableNames(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.ExecutableNames; 
                case MEGame.ME2:
                    return ME2Directory.ExecutableNames;
                case MEGame.ME3:
                    return ME3Directory.ExecutableNames;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string GetLODConfigFile(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.LODConfigFile;
                case MEGame.ME2:
                    return ME2Directory.LODConfigFile;
                case MEGame.ME3:
                    return ME3Directory.LODConfigFile;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static ReadOnlyCollection<string> VanillaDlls(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.VanillaDlls;
                case MEGame.ME2:
                    return ME2Directory.VanillaDlls;
                case MEGame.ME3:
                    return ME3Directory.VanillaDlls;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
            throw new NotImplementedException();
        }

        public static string CookedName(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.CookedName;
                case MEGame.ME2:
                    return ME2Directory.CookedName;
                case MEGame.ME3:
                    return ME3Directory.CookedName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static ReadOnlyCollection<string> OfficialDLC(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.OfficialDLC;
                case MEGame.ME2:
                    return ME2Directory.OfficialDLC;
                case MEGame.ME3:
                    return ME3Directory.OfficialDLC;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static bool IsInBasegame(this IMEPackage pcc, string gameRootOverride = null) => IsInBasegame(pcc.FilePath, pcc.Game, gameRootOverride);

        public static bool IsInBasegame(string path, MEGame game, string gameRootOverride = null) => game != MEGame.UDK && game != MEGame.Unknown && path.StartsWith(GetCookedPath(game, gameRootOverride));

        public static bool IsInOfficialDLC(this IMEPackage pcc, string gameRootOverride = null) => IsInOfficialDLC(pcc.FilePath, pcc.Game, gameRootOverride);

        public static bool IsInOfficialDLC(string path, MEGame game, string gameRootOverride = null)
        {
            if (game == MEGame.UDK || game == MEGame.Unknown)
            {
                return false;
            }
            string dlcPath = GetDLCPath(game, gameRootOverride);

            return OfficialDLC(game).Any(dlcFolder => path.StartsWith(Path.Combine(dlcPath, dlcFolder)));
        }

        /// <summary>
        /// Refreshes the registry active paths for all three games
        /// </summary>
        public static void ReloadGamePaths(bool forceUseRegistry)
        {
            ME1Directory.ReloadDefaultGamePath(forceUseRegistry);
            ME2Directory.ReloadDefaultGamePath(forceUseRegistry); 
            ME3Directory.ReloadDefaultGamePath(forceUseRegistry); 
        }

        public static void SaveSettings(List<string> BIOGames)
        {
            try
            {
                if (!string.IsNullOrEmpty(BIOGames[0]))
                {
                    ME3ExplorerCoreLibSettings.Instance.ME1Directory = BIOGames[0];
                    ME1Directory.DefaultGamePath = BIOGames[0];
                }

                if (!string.IsNullOrEmpty(BIOGames[1]))
                {
                    ME3ExplorerCoreLibSettings.Instance.ME2Directory = BIOGames[1];
                    ME2Directory.DefaultGamePath = BIOGames[1];
                }

                if (!string.IsNullOrEmpty(BIOGames[2]))
                {
                    ME3ExplorerCoreLibSettings.Instance.ME3Directory = BIOGames[2];
                    ME3Directory.DefaultGamePath = BIOGames[2];
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error saving pathing: {e.Message}");
            }
        }

        public static List<string> EnumerateGameFiles(MEGame game, string searchPath, bool recurse = true, Predicate<string> predicate = null)
        {
            List<string> files = Directory.EnumerateFiles(searchPath, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

            files = EnumerateGameFiles(game, files, predicate);

            return files;
        }

        public static List<string> EnumerateGameFiles(MEGame game, List<string> files, Predicate<string> predicate = null)
        {
            if (predicate == null)
            {
                // KFreon: Set default search predicate.
                switch (game)
                {
                    case MEGame.ME1:
                        predicate = s => s.ToLowerInvariant().EndsWith(".upk", true, null) || s.ToLowerInvariant().EndsWith(".u", true, null) || s.ToLowerInvariant().EndsWith(".sfm", true, null);
                        break;
                    case MEGame.ME2:
                    case MEGame.ME3:
                        predicate = s => s.ToLowerInvariant().EndsWith(".pcc", true, null) || s.ToLowerInvariant().EndsWith(".tfc", true, null);
                        break;
                }
            }

            return files.Where(t => predicate(t)).ToList();
        }

        public static CaseInsensitiveDictionary<string> OfficialDLCNames(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.OfficialDLCNames;
                case MEGame.ME2:
                    return ME2Directory.OfficialDLCNames;
                case MEGame.ME3:
                    return ME3Directory.OfficialDLCNames;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }
    }
}
