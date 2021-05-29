using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.GameFilesystem
{
    public static class MEDirectories
    {
        public static string GetCookedPath(MEGame game, string gamePathRoot = null)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.GetCookedPCPath(gamePathRoot),
                MEGame.ME2 => ME2Directory.GetCookedPCPath(gamePathRoot),
                MEGame.ME3 => ME3Directory.GetCookedPCPath(gamePathRoot),
                MEGame.LE1 => LE1Directory.GetCookedPCPath(gamePathRoot),
                MEGame.LE2 => LE2Directory.GetCookedPCPath(gamePathRoot),
                MEGame.LE3 => LE3Directory.GetCookedPCPath(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        /// <summary>
        /// Gets the default game path for the listed game.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string GetDefaultGamePath(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.DefaultGamePath,
                MEGame.ME2 => ME2Directory.DefaultGamePath,
                MEGame.ME3 => ME3Directory.DefaultGamePath,
                MEGame.LE1 => LE1Directory.DefaultGamePath,
                MEGame.LE2 => LE2Directory.DefaultGamePath,
                MEGame.LE3 => LE3Directory.DefaultGamePath,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static string GetBioGamePath(MEGame game, string gamePathRoot = null)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.GetBioGamePath(gamePathRoot),
                MEGame.ME2 => ME2Directory.GetBioGamePath(gamePathRoot),
                MEGame.ME3 => ME3Directory.GetBioGamePath(gamePathRoot),
                MEGame.LE1 => LE1Directory.GetBioGamePath(gamePathRoot),
                MEGame.LE2 => LE2Directory.GetBioGamePath(gamePathRoot),
                MEGame.LE3 => LE3Directory.GetBioGamePath(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static string GetDLCPath(MEGame game, string gamePathRoot = null)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.GetDLCPath(gamePathRoot),
                MEGame.ME2 => ME2Directory.GetDLCPath(gamePathRoot),
                MEGame.ME3 => ME3Directory.GetDLCPath(gamePathRoot),
                MEGame.LE1 => LE1Directory.GetDLCPath(gamePathRoot),
                MEGame.LE2 => LE2Directory.GetDLCPath(gamePathRoot),
                MEGame.LE3 => LE3Directory.GetDLCPath(gamePathRoot),
                MEGame.LELauncher => null,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }


        public static string GetExecutablePath(MEGame game, string gamePathRoot = null)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.GetExecutablePath(gamePathRoot),
                MEGame.ME2 => ME2Directory.GetExecutablePath(gamePathRoot),
                MEGame.ME3 => ME3Directory.GetExecutablePath(gamePathRoot),
                MEGame.LE1 => LE1Directory.GetExecutablePath(gamePathRoot),
                MEGame.LE2 => LE2Directory.GetExecutablePath(gamePathRoot),
                MEGame.LE3 => LE3Directory.GetExecutablePath(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static string GetExecutableFolderPath(MEGame game, string gamePathRoot = null)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.GetExecutableDirectory(gamePathRoot),
                MEGame.ME2 => ME2Directory.GetExecutableDirectory(gamePathRoot),
                MEGame.ME3 => ME3Directory.GetExecutableDirectory(gamePathRoot),
                MEGame.LE1 => LE1Directory.GetExecutableDirectory(gamePathRoot),
                MEGame.LE2 => LE2Directory.GetExecutableDirectory(gamePathRoot),
                MEGame.LE3 => LE3Directory.GetExecutableDirectory(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static string GetASIPath(MEGame game, string gamePathRoot = null)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.GetASIPath(gamePathRoot),
                MEGame.ME2 => ME2Directory.GetASIPath(gamePathRoot),
                MEGame.ME3 => ME3Directory.GetASIPath(gamePathRoot),
                MEGame.LE1 => LE1Directory.GetASIPath(gamePathRoot),
                MEGame.LE2 => LE2Directory.GetASIPath(gamePathRoot),
                MEGame.LE3 => LE3Directory.GetASIPath(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static string GetTextureModMarkerPath(MEGame game, string gamePathRoot)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.GetTextureModMarkerPath(gamePathRoot),
                MEGame.ME2 => ME2Directory.GetTextureModMarkerPath(gamePathRoot),
                MEGame.ME3 => ME3Directory.GetTextureModMarkerPath(gamePathRoot),
                MEGame.LE1 => LE1Directory.GetTextureModMarkerPath(gamePathRoot),
                MEGame.LE2 => LE2Directory.GetTextureModMarkerPath(gamePathRoot),
                MEGame.LE3 => LE3Directory.GetTextureModMarkerPath(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static ReadOnlyCollection<string> ExecutableNames(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.ExecutableNames,
                MEGame.ME2 => ME2Directory.ExecutableNames,
                MEGame.ME3 => ME3Directory.ExecutableNames,
                MEGame.LE1 => LE1Directory.ExecutableNames,
                MEGame.LE2 => LE2Directory.ExecutableNames,
                MEGame.LE3 => LE3Directory.ExecutableNames,
                MEGame.LELauncher => LEDirectory.ExecutableNames,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static string GetLODConfigFile(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.LODConfigFile,
                MEGame.ME2 => ME2Directory.LODConfigFile,
                MEGame.ME3 => ME3Directory.LODConfigFile,
                //MEGame.LE1 => LE1Directory.LODConfigFile,
                //MEGame.LE2 => LE2Directory.LODConfigFile,
                //MEGame.LE3 => LE3Directory.LODConfigFile,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static ReadOnlyCollection<string> VanillaDlls(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.VanillaDlls,
                MEGame.ME2 => ME2Directory.VanillaDlls,
                MEGame.ME3 => ME3Directory.VanillaDlls,
                MEGame.LE1 => LE1Directory.VanillaDlls,
                MEGame.LE2 => LE2Directory.VanillaDlls,
                MEGame.LE3 => LE3Directory.VanillaDlls,
                MEGame.LELauncher => LEDirectory.VanillaLauncherDlls,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
            throw new NotImplementedException();
        }

        public static string CookedName(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.CookedName,
                MEGame.ME2 => ME2Directory.CookedName,
                MEGame.ME3 => ME3Directory.CookedName,
                MEGame.LE1 => LE1Directory.CookedName,
                MEGame.LE2 => LE2Directory.CookedName,
                MEGame.LE3 => LE3Directory.CookedName,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static string GetTOCFile(MEGame game)
        {
            return game switch
            {
                MEGame.ME3 => ME3Directory.TocFile,
                MEGame.LE1 => LE1Directory.TocFile,
                MEGame.LE2 => LE2Directory.TocFile,
                MEGame.LE3 => LE3Directory.TocFile,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static ReadOnlyCollection<string> OfficialDLC(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.OfficialDLC,
                MEGame.ME2 => ME2Directory.OfficialDLC,
                MEGame.ME3 => ME3Directory.OfficialDLC,
                MEGame.LE1 => LE1Directory.OfficialDLC,
                MEGame.LE2 => LE2Directory.OfficialDLC,
                MEGame.LE3 => LE3Directory.OfficialDLC,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        public static bool IsInBasegame(this IMEPackage pcc, string gameRootOverride = null) => IsInBasegame(pcc.FilePath, pcc.Game, gameRootOverride);

        public static bool IsInBasegame(string path, MEGame game, string gameRootOverride = null)
        {
            if (game == MEGame.UDK || game == MEGame.LELauncher) return false;
            if (gameRootOverride is null && GetDefaultGamePath(game) is null)
            {
                return false;
            }
            if (game == MEGame.LE1 && path.StartsWith(LE1Directory.GetISACTPath(gameRootOverride))) return true;
            return path.StartsWith(GetCookedPath(game, gameRootOverride));
        }

        public static bool IsInOfficialDLC(this IMEPackage pcc, string gameRootOverride = null) => IsInOfficialDLC(pcc.FilePath, pcc.Game, gameRootOverride);

        public static bool IsInOfficialDLC(string path, MEGame game, string gameRootOverride = null)
        {
            if (game is MEGame.UDK or MEGame.LELauncher or MEGame.LE1)
            {
                return false;
            }
            string dlcPath = GetDLCPath(game, gameRootOverride);
            if (dlcPath is null)
            {
                return false;
            }
            return OfficialDLC(game).Any(dlcFolder => path.StartsWith(Path.Combine(dlcPath, dlcFolder)));
        }

        /// <summary>
        /// Refreshes the registry active paths for all games
        /// </summary>
        public static void ReloadGamePaths(bool forceUseRegistry)
        {
            ME1Directory.ReloadDefaultGamePath(forceUseRegistry);
            ME2Directory.ReloadDefaultGamePath(forceUseRegistry); 
            ME3Directory.ReloadDefaultGamePath(forceUseRegistry);
            LE1Directory.ReloadDefaultGamePath(forceUseRegistry);
            LE2Directory.ReloadDefaultGamePath(forceUseRegistry);
            LE3Directory.ReloadDefaultGamePath(forceUseRegistry);
        }

        public static void SaveSettings(List<string> BIOGames)
        {
            try
            {
                if (!string.IsNullOrEmpty(BIOGames[0]))
                {
                    LegendaryExplorerCoreLibSettings.Instance.ME1Directory = BIOGames[0];
                    ME1Directory.DefaultGamePath = BIOGames[0];
                }

                if (!string.IsNullOrEmpty(BIOGames[1]))
                {
                    LegendaryExplorerCoreLibSettings.Instance.ME2Directory = BIOGames[1];
                    ME2Directory.DefaultGamePath = BIOGames[1];
                }

                if (!string.IsNullOrEmpty(BIOGames[2]))
                {
                    LegendaryExplorerCoreLibSettings.Instance.ME3Directory = BIOGames[2];
                    ME3Directory.DefaultGamePath = BIOGames[2];
                }

                if (!string.IsNullOrEmpty(BIOGames[3]))
                {
                    LegendaryExplorerCoreLibSettings.Instance.LEDirectory = BIOGames[3];
                    LE1Directory.ReloadDefaultGamePath();
                    LE2Directory.ReloadDefaultGamePath();
                    LE3Directory.ReloadDefaultGamePath();
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
                    case MEGame.LE1:
                    case MEGame.LE2:
                    case MEGame.LE3:
                        predicate = s => s.ToLowerInvariant().EndsWith(".pcc", true, null) || s.ToLowerInvariant().EndsWith(".tfc", true, null);
                        break;
                }
            }

            return files.Where(t => predicate(t)).ToList();
        }

        public static CaseInsensitiveDictionary<string> OfficialDLCNames(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.OfficialDLCNames,
                MEGame.ME2 => ME2Directory.OfficialDLCNames,
                MEGame.ME3 => ME3Directory.OfficialDLCNames,
                MEGame.LE1 => LE1Directory.OfficialDLCNames,
                MEGame.LE2 => LE2Directory.OfficialDLCNames,
                MEGame.LE3 => LE3Directory.OfficialDLCNames,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }
    }
}
