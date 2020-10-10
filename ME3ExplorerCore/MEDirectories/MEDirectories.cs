using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.MEDirectories
{
    public static class MEDirectories
    {
        public static string CookedPath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.cookedPath;
                case MEGame.ME2:
                    return ME2Directory.cookedPath;
                case MEGame.ME3:
                    return ME3Directory.cookedPath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        /// <summary>
        /// Returns cooked path for specified game path and game
        /// </summary>
        /// <param name="gamepath"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string CookedPath(string gamepath, MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                case MEGame.ME2:
                    return Path.Combine(gamepath, "BioGame", "CookedPC");
                case MEGame.ME3:
                    return Path.Combine(gamepath, "BioGame", "CookedPCConsole");
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string GamePath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.gamePath;
                case MEGame.ME2:
                    return ME2Directory.gamePath;
                case MEGame.ME3:
                    return ME3Directory.gamePath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string BioGamePath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.BioGamePath;
                case MEGame.ME2:
                    return ME2Directory.BioGamePath;
                case MEGame.ME3:
                    return ME3Directory.BIOGamePath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string DLCPath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.DLCPath;
                case MEGame.ME2:
                    return ME2Directory.DLCPath;
                case MEGame.ME3:
                    return ME3Directory.DLCPath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        /// <summary>
        /// Returns DLC path for game at the specified game path
        /// </summary>
        /// <param name="gamepath"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string DLCPath(string gamepath, MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return Path.Combine(gamepath, "DLC");
                case MEGame.ME2:
                case MEGame.ME3:
                    return Path.Combine(gamepath, "BioGame", "DLC");
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string ExecutablePath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1Directory.ExecutablePath;
                case MEGame.ME2:
                    return ME2Directory.ExecutablePath;
                case MEGame.ME3:
                    return ME3Directory.ExecutablePath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static List<string> OfficialDLC(MEGame game)
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

        public static bool IsInBasegame(this IMEPackage pcc) => IsInBasegame(pcc.FilePath, pcc.Game);

        public static bool IsInBasegame(string path, MEGame game) => game != MEGame.UDK && game != MEGame.Unknown && path.StartsWith(CookedPath(game));

        public static bool IsInOfficialDLC(this IMEPackage pcc) => IsInOfficialDLC(pcc.FilePath, pcc.Game);

        public static bool IsInOfficialDLC(string path, MEGame game)
        {
            if (game == MEGame.UDK || game == MEGame.Unknown)
            {
                return false;
            }
            string dlcPath = DLCPath(game);

            return OfficialDLC(game).Any(dlcFolder => path.StartsWith(Path.Combine(dlcPath, dlcFolder)));
        }

        public static void SaveSettings(List<string> BIOGames)
        {
            try
            {
                if (!string.IsNullOrEmpty(BIOGames[0]))
                {
                    CoreLibSettings.Instance.ME1Directory = BIOGames[0];
                    ME1Directory.gamePath = BIOGames[0];
                }

                if (!string.IsNullOrEmpty(BIOGames[1]))
                {
                    CoreLibSettings.Instance.ME2Directory = BIOGames[1];
                    ME2Directory.gamePath = BIOGames[1];
                }

                if (!string.IsNullOrEmpty(BIOGames[2]))
                {
                    CoreLibSettings.Instance.ME3Directory = BIOGames[2];
                    ME3Directory.gamePath = BIOGames[2];
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error saving pathing: {e.Message}");
            }
        }

        public static List<string> EnumerateGameFiles(MEGame GameVersion, string searchPath, bool recurse = true, Predicate<string> predicate = null)
        {
            List<string> files = Directory.EnumerateFiles(searchPath, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

            files = EnumerateGameFiles(GameVersion, files, predicate);

            return files;
        }

        public static List<string> EnumerateGameFiles(MEGame GameVersion, List<string> files, Predicate<string> predicate = null)
        {
            if (predicate == null)
            {
                // KFreon: Set default search predicate.
                switch (GameVersion)
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
