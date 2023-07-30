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
    /// <summary>
    /// Static methods to get game directory information from an MEGame enum
    /// </summary>
    public static class MEDirectories
    {
        /// <summary>
        /// Gets the TFCs in the basegame directory for the input game
        /// </summary>
        /// <param name="game">Game to get TFCs for</param>
        /// <returns>Array of TFC filenames</returns>
        public static string[] BasegameTFCs(MEGame game)
        {
            return game switch
            {
                MEGame.ME2 => ME2Directory.BasegameTFCs,
                MEGame.ME3 => ME3Directory.BasegameTFCs,
                MEGame.LE1 => LE1Directory.BasegameTFCs,
                MEGame.LE2 => LE2Directory.BasegameTFCs,
                MEGame.LE3 => LE3Directory.BasegameTFCs,
                _ => new string[] { }

            };
        }

        /// <summary>
        /// Gets the CookedPC directory for the input game
        /// </summary>
        /// <param name="game">Game to get directory for</param>
        /// <param name="gamePathRoot">Optional: override game path root</param>
        /// <returns>Path to CookedPC directory</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no CookedPC path</exception>
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
        /// <param name="game">Game</param>
        /// <returns>Default game path</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no default path</exception>
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
                MEGame.UDK => UDKDirectory.DefaultGamePath,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        /// <summary>
        /// Gets the BioGame directory for the input game
        /// </summary>
        /// <param name="game">Game to get directory for</param>
        /// <param name="gamePathRoot">Optional: override game path root</param>
        /// <returns>Path to BioGame directory</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no BioGame path</exception>
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
                MEGame.UDK => UDKDirectory.GetUDKGamePath(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        /// <summary>
        /// Gets the DLC directory for the input game. This is the folder where all DLC reside.
        /// </summary>
        /// <param name="game">Game to get directory for</param>
        /// <param name="gamePathRoot">Optional: override game path root</param>
        /// <returns>Path to DLC directory</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no DLC directory</exception>
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

        /// <summary>
        /// Gets the path to the main executable for the input game
        /// </summary>
        /// <param name="game">Game to get executable for</param>
        /// <param name="gamePathRoot">Optional: override game path root</param>
        /// <returns>Path to game's .exe file</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no executable</exception>
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
                MEGame.LELauncher => LEDirectory.GetLauncherExecutable(gamePathRoot),
                MEGame.UDK => UDKDirectory.GetExecutablePath(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        /// <summary>
        /// Gets the folder containing the executable for the input game
        /// </summary>
        /// <param name="game">Game to get directory for</param>
        /// <param name="gamePathRoot">Optional: override game path root</param>
        /// <returns>Path to executable directory</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no executable folder</exception>
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
                MEGame.LELauncher => LEDirectory.GetExecutableDirectory(gamePathRoot),
                MEGame.UDK => UDKDirectory.GetExecutableDirectory(gamePathRoot),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        /// <summary>
        /// Gets the ASI folder for the input game. This is the folder where installed ASI mods reside.
        /// </summary>
        /// <param name="game">Game to get directory for</param>
        /// <param name="gamePathRoot">Optional: override game path root</param>
        /// <returns>Path to ASI directory</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no ASI path</exception>
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

        /// <summary>
        /// Gets the path to the texture mod marker file for the input game
        /// </summary>
        /// <param name="game">Game to get marker file for</param>
        /// <param name="gamePathRoot">Optional: override game path root</param>
        /// <returns>Path to texture mod marker file</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no texture mod marker</exception>
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

        /// <summary>
        /// Gets the list of filenames a game's main executable may be called, for an input game
        /// </summary>
        /// <param name="game">Game to get executable file names for</param>
        /// <returns>Collection of executable file names</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no executable names</exception>
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
                MEGame.UDK => UDKDirectory.ExecutableNames,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        /// <summary>
        /// Gets path to the LOD config file for an input game
        /// </summary>
        /// <param name="game">Game to get config file for</param>
        /// <param name="gamePathOverride">Optional: override game path root</param>
        /// <returns>Path to LOD config file</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no LOD config file</exception>
        public static string GetLODConfigFile(MEGame game, string gamePathOverride = null)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.LODConfigFile,
                MEGame.ME2 => ME2Directory.LODConfigFile,
                MEGame.ME3 => ME3Directory.LODConfigFile,
                MEGame.LE1 => LE1Directory.GetLODConfigFile(gamePathOverride),
                MEGame.LE2 => LE2Directory.GetLODConfigFile(gamePathOverride),
                MEGame.LE3 => LE3Directory.GetLODConfigFile(gamePathOverride),
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }

        /// <summary>
        /// Gets the list of vanilla DLL filenames that ship with an input game.
        /// </summary>
        /// <remarks>This list will include both the bink bypass and the original renamed bink dll</remarks>
        /// <param name="game">Game to get DLLs for</param>
        /// <returns>Collection of DLL filenames</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no vanilla DLLs</exception>
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
        }

        /// <summary>
        /// Gets the name of this game's CookedPC folder
        /// </summary>
        /// <remarks>Will be either CookedPC or CookedPCConsole depending on the game</remarks>
        /// <param name="game">Game to get name for</param>
        /// <returns>Cooked folder name</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no CookedPC folder</exception>
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

        /// <summary>
        /// Gets the path to the basegame PCConsoleTOC.bin file for an input game
        /// </summary>
        /// <remarks>A game may have additional TOC files inside DLC folders</remarks>
        /// <param name="game">Game to get TOC file for</param>
        /// <returns>Path to PCConsoleTOC.bin</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no TOC file</exception>
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

        /// <summary>
        /// Gets a list of Official DLC folder names for an input game
        /// </summary>
        /// <param name="game">Game to get DLC for</param>
        /// <returns>List of DLC folder names</returns>
        /// <exception cref="ArgumentOutOfRangeException">Game has no official DLC</exception>
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

        /// <summary>
        /// Gets a mapping of official DLC folder names to human readable names
        /// </summary>
        /// <param name="game">Game to get DLC mappings for</param>
        /// <returns>Dictionary of DLC folders</returns>
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

        /// <summary>
        /// Checks if a package file is in the basegame folder
        /// </summary>
        /// <param name="pcc">Package file</param>
        /// <param name="gameRootOverride">Optional: override game path root</param>
        /// <returns>True if file is in basegame</returns>
        public static bool IsInBasegame(this IMEPackage pcc, string gameRootOverride = null) => IsInBasegame(pcc.FilePath, pcc.Game, gameRootOverride);

        /// <summary>
        /// Checks if a package file is in the basegame folder
        /// </summary>
        /// <param name="path">Path to package file</param>
        /// <param name="game">Game to check basegame folder</param>
        /// <param name="gameRootOverride">Optional: override game path root</param>
        /// <returns>True if file is in basegame</returns>
        public static bool IsInBasegame(string path, MEGame game, string gameRootOverride = null)
        {
            if (game is MEGame.UDK or MEGame.LELauncher) return false;
            if (gameRootOverride is null && GetDefaultGamePath(game) is null)
            {
                return false;
            }
            if (game == MEGame.LE1 && path.StartsWith(LE1Directory.GetISACTPath(gameRootOverride))) return true;
            return path.StartsWith(GetCookedPath(game, gameRootOverride));
        }

        /// <summary>
        /// Checks if a package file is in an official DLC folder (not mod-added DLC)
        /// </summary>
        /// <param name="pcc">Package folder</param>
        /// <param name="gameRootOverride">Optional: override game path root</param>
        /// <returns>True if the file is in an official DLC folder</returns>
        public static bool IsInOfficialDLC(this IMEPackage pcc, string gameRootOverride = null) => IsInOfficialDLC(pcc.FilePath, pcc.Game, gameRootOverride);

        /// <summary>
        /// Checks if a package file is in an official DLC folder (not mod-added DLC)
        /// </summary>
        /// <param name="path">Path to package file</param>
        /// <param name="game">Game to check official DLC folders</param>
        /// <param name="gameRootOverride">Optional: override game path root</param>
        /// <returns>True if the file is in an official DLC folder</returns>
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
        /// Gets a short description of where the file is located (Basegame, DLC name, or Not in installation). Does not specify game
        /// </summary>
        public static bool GetLocationDescriptor(IMEPackage pcc, out string descriptor, string gameRootOverride = null) => GetLocationDescriptor(pcc.FilePath, pcc.Game, out descriptor, gameRootOverride);

        /// <summary>
        /// Gets a short description of where the file is located (Basegame, DLC name, or Not in installation). Does not specify game
        /// </summary>
        public static bool GetLocationDescriptor(string filePath, MEGame game, out string descriptor, string gameRootOverride = null)
        {
            descriptor = "Not in installation";
            if (filePath is not null && game.IsMEGame() && (GetDefaultGamePath(game) is not null || gameRootOverride is not null))
            {
                filePath = Path.GetFullPath(filePath);
                if (filePath.StartsWith(GetCookedPath(game, gameRootOverride)))
                {
                    descriptor = "Basegame";
                    return true;
                }
                string dlcPath = GetDLCPath(game, gameRootOverride);
                if (filePath.StartsWith(dlcPath))
                {
                    string relativePath = Path.GetRelativePath(dlcPath, filePath);
                    int dirSepIndex = relativePath.AsSpan().IndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (dirSepIndex > 0)
                    {
                        descriptor = relativePath[..dirSepIndex];
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Refreshes the default game path for all games
        /// </summary>
        /// <param name="forceUseRegistry">If true, all paths will attempt to be loaded from registry. If false, existing path settings may be used.</param>
        public static void ReloadGamePaths(bool forceUseRegistry)
        {
            ME1Directory.ReloadDefaultGamePath(forceUseRegistry);
            ME2Directory.ReloadDefaultGamePath(forceUseRegistry);
            ME3Directory.ReloadDefaultGamePath(forceUseRegistry);
            LE1Directory.ReloadDefaultGamePath(forceUseRegistry);
            LE2Directory.ReloadDefaultGamePath(forceUseRegistry);
            LE3Directory.ReloadDefaultGamePath(forceUseRegistry);
            UDKDirectory.ReloadDefaultGamePath();
        }

        /// <summary>
        /// Saves MEDirectory settings, with a list of game directories, in order: ME1/ME2/ME3/LE1/LE2/LE3. 
        /// </summary>
        /// <param name="bioGameFolders">List of game folders</param>
        public static void SaveSettings(List<string> bioGameFolders)
        {
            if (bioGameFolders.Count != 4)
                throw new Exception("SaveSettings() requires 4 items in the parameter");
            try
            {
                if (!string.IsNullOrEmpty(bioGameFolders[0]))
                {
                    LegendaryExplorerCoreLibSettings.Instance.ME1Directory = bioGameFolders[0];
                    ME1Directory.DefaultGamePath = bioGameFolders[0];
                }

                if (!string.IsNullOrEmpty(bioGameFolders[1]))
                {
                    LegendaryExplorerCoreLibSettings.Instance.ME2Directory = bioGameFolders[1];
                    ME2Directory.DefaultGamePath = bioGameFolders[1];
                }

                if (!string.IsNullOrEmpty(bioGameFolders[2]))
                {
                    LegendaryExplorerCoreLibSettings.Instance.ME3Directory = bioGameFolders[2];
                    ME3Directory.DefaultGamePath = bioGameFolders[2];
                }

                if (!string.IsNullOrEmpty(bioGameFolders[3]))
                {
                    LegendaryExplorerCoreLibSettings.Instance.LEDirectory = bioGameFolders[3];
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

        /// <summary>
        /// Enumerates all files in an input path, matching them for a predicate
        /// </summary>
        /// <param name="game">Game to match extensions for</param>
        /// <param name="searchPath">Path to enumerate all files in</param>
        /// <param name="recurse">If true, all subdirectories are enumerated. If false, only the top directory is searched</param>
        /// <param name="predicate">Optional: predicate to match. If null, default predicate only matching game file extensions is used</param>
        /// <returns>List of files in <see cref="searchPath"/> matching the predicate</returns>
        public static List<string> EnumerateGameFiles(MEGame game, string searchPath, bool recurse = true, Predicate<string> predicate = null)
        {
            List<string> files = Directory.EnumerateFiles(searchPath, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

            files = EnumerateGameFiles(game, files, predicate);

            return files;
        }

        /// <summary>
        /// Enumerate an input list of package and TFC files
        /// </summary>
        /// <param name="game">Game to enumerate files</param>
        /// <param name="files">List of files to enumerate</param>
        /// <param name="predicate">Optional: predicate to match. If null, default predicate only matching game file extensions is used</param>
        /// <returns>List of files matching the predicate</returns>
        public static List<string> EnumerateGameFiles(MEGame game, List<string> files, Predicate<string> predicate = null)
        {
            if (predicate == null)
            {
                // KFreon: Set default search predicate.
                switch (game)
                {
                    case MEGame.ME1:
                        predicate = s => s.EndsWith(".upk", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".u", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".sfm", StringComparison.OrdinalIgnoreCase);
                        break;
                    case MEGame.ME2:
                    case MEGame.ME3:
                    case MEGame.LE1:
                    case MEGame.LE2:
                    case MEGame.LE3:
                        predicate = s => s.EndsWith(".pcc", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase);
                        break;
                    case MEGame.UDK:
                        predicate = s => s.EndsWith(".upk", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".u", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".udk", StringComparison.OrdinalIgnoreCase);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(game), game, null);
                }
            }

            return files.Where(t => predicate(t)).ToList();
        }

        public static string GetProfileSave(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1Directory.LocalProfilePath,
                MEGame.ME2 => ME2Directory.LocalProfilePath,
                MEGame.ME3 => ME3Directory.LocalProfilePath,
                MEGame.LE1 => LE1Directory.LocalProfilePath,
                MEGame.LE2 => LE2Directory.LocalProfilePath,
                MEGame.LE3 => LE3Directory.LocalProfilePath,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
            };
        }
    }
}
