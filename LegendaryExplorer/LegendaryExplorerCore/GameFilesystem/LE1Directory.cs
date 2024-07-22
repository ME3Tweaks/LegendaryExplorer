using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Contains information about the LE1 game directory
    /// </summary>
    public static class LE1Directory
    {
        /// <summary>
        /// Gets the path to the BioGame folder for LE1
        /// </summary>
        public static string BioGamePath => GetBioGamePath();
        /// <summary>
        /// Gets the path to the BioGame folder for LE1
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to BioGame folder, null if no usable root path</returns>
        public static string GetBioGamePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "BioGame");
        }

        /// <summary>
        /// Gets the path to the DLC folder for LE1
        /// </summary>
        public static string DLCPath => GetDLCPath();
        /// <summary>
        /// Gets the path to the DLC folder for LE1
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to DLC folder, null if no usable root path</returns>
        public static string GetDLCPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), "DLC");
        }

        /// <summary>
        /// Gets the path to the basegame Cooked folder for LE1
        /// </summary>
        public static string CookedPCPath => GetCookedPCPath();
        /// <summary>
        /// Gets the path to basegame Cooked folder for LE1
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to Cooked folder, null if no usable root path</returns>
        public static string GetCookedPCPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), CookedName);
        }

        /// <summary>
        /// Gets the path to the executable folder for LE1
        /// </summary>
        public static string ExecutableFolder => GetExecutableDirectory();
        /// <summary>
        /// Gets the path to the executable folder for LE1
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to executable folder, null if no usable root path</returns>
        public static string GetExecutableDirectory(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "Binaries", "Win64");
        }

        /// <summary>
        /// Gets the path to the game executable for LE1
        /// </summary>
        public static string ExecutablePath => GetExecutablePath();
        /// <summary>
        /// Gets the path to the game executable for LE1
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to game executable, null if no usable root path</returns>
        public static string GetExecutablePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "MassEffect1.exe");
        }

        /// <summary>
        /// Gets the path to the ASI install directory for LE1
        /// </summary>
        public static string ASIPath => GetASIPath();
        /// <summary>
        /// Gets the path to the ASI install directory for LE1
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to ASI folder, null if no usable root path</returns>
        public static string GetASIPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "ASI");
        }

        /// <summary>
        /// Gets the path to the texture mod marker file for LE1
        /// </summary>
        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        /// <summary>
        /// Gets the path to the texture mod marker file for LE1
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to texture mod marker, null if no usable root path</returns>
        public static string GetTextureModMarkerPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetCookedPCPath(rootPathOverride), "SFXTest.pcc");
        }

        /// <summary>
        /// Gets the path to the ISACT directory for LE1
        /// </summary>
        public static string ISACTPath => GetISACTPath();
        /// <summary>
        /// Gets the path to the ISACT directory for LE1
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to ISACT directory, null if no usable root path</returns>
        public static string GetISACTPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), "Content", "Packages", "ISACT");
        }

        /// <summary>
        /// The filenames of any valid LE1 executables
        /// </summary>
        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "MassEffect1.exe" });

        /// <summary>
        /// Gets the list of vanilla DLL filenames that ship with LE1
        /// </summary>
        /// <remarks>This list will include both the bink bypass and the original renamed bink dll</remarks>
        public static readonly ReadOnlyCollection<string> VanillaDlls = Array.AsReadOnly(new[]
        {
            "amd_ags_x64.dll",
            "bink2w64_original.dll", // We say this is vanilla since it will very commonly be present and should not be removed
            "bink2w64.dll",
            "dbdata.dll",
            "ogg.dll",
            "oo2core_8_win64.dll",
            "OpenAL32.dll",
            "PhysXCooking64.dll",
            "PhysXCore64.dll",
            "vorbis.dll",
            "vorbisfile.dll",
            "wrap_oal.dll"
        });

        /// <summary>
        /// Gets the path of the LE1 folder in the user's Documents/BioWare folder. This is where savegames and some configuration files are stored.
        /// </summary>
        /// <remarks>This is the same folder for all LE games</remarks>
        public static string BioWareDocumentsPath => LEDirectory.BioWareDocumentsPath;

        /// <summary>
        /// Path to the folder where career profiles are stored in the user config directory.
        /// </summary>
        public static string SaveFolderPath => Path.Combine(LEDirectory.BioWareDocumentsPath, @"Save", @"ME1");

        /// <summary>
        /// Path to the persistent storage file in the user config directory.
        /// </summary>
        public static string LocalProfilePath => Path.Combine(SaveFolderPath, @"PROFILE", @"GamerProfile.pcsav");

        /// <summary>
        /// Gets the path to the LOD configuration file for LE1
        /// </summary>
        /// <param name="gamePathRootOverride">Optional: override game path root</param>
        /// <returns>Path to LOD configuration file</returns>
        public static string GetLODConfigFile(string gamePathRootOverride = null)
        {
            if (gamePathRootOverride != null)
            {
                return Path.Combine(gamePathRootOverride, @"BioGame", @"Config", @"GamerSettings.ini");
            }
            return Path.Combine(BioGamePath, @"Config", @"GamerSettings.ini");
        }

        /// <summary>
        /// Gets the name of the Cooked folder for LE1
        /// </summary>
        public static string CookedName => "CookedPCConsole";

        private static string _gamePath;
        /// <summary>
        /// Gets or sets the default game root path that is used when locating game folders.
        /// By default, this path is loaded from the <see cref="LegendaryExplorerCoreLibSettings"/> instance.
        /// Updating this path will not update the value in the CoreLibSettings.
        /// </summary>
        public static string DefaultGamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_gamePath))
                {
                    if (string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.LEDirectory))
                    {
                        return null;
                    }
                    _gamePath = Path.Combine(LegendaryExplorerCoreLibSettings.Instance.LEDirectory, "Game", "ME1");
                }

                return Path.GetFullPath(_gamePath); //normalize
            }
            set
            {
                if (value != null)
                {
                    if (value.Contains("BioGame", StringComparison.OrdinalIgnoreCase))
                        value = value.Substring(0, value.LastIndexOf("BioGame", StringComparison.OrdinalIgnoreCase));
                }
                _gamePath = value;
            }
        }

        /// <summary>
        /// Gets the path to the basegame PCConsoleTOC.bin file for LE1
        /// </summary>
        public static string TocFile => DefaultGamePath != null ? Path.Combine(DefaultGamePath, @"BioGame", "PCConsoleTOC.bin") : null;

        static LE1Directory()
        {
            ReloadDefaultGamePath(false);
        }

        /// <summary>
        /// Reloads the default LE1 game path, either from LEC settings or from the registry
        /// </summary>
        /// <param name="forceUseRegistry">If true, registry will be used to determine game path. If false, LEC settings may be used instead</param>
        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.LEDirectory))
            {
                DefaultGamePath = Path.Combine(LegendaryExplorerCoreLibSettings.Instance.LEDirectory, "Game", "ME1");
            }
            else
            {
#if WINDOWS
                if (LEDirectory.LookupDefaultPath()) ReloadDefaultGamePath(false);
#endif
            }
        }

        /// <summary>
        /// Gets a mapping of official DLC folder names to human readable names for LE1
        /// </summary>
        public static readonly CaseInsensitiveDictionary<string> OfficialDLCNames = new CaseInsensitiveDictionary<string>
        {
        };

        /// <summary>
        /// Gets a list of official DLC folder names for LE1
        /// </summary>
        public static readonly ReadOnlyCollection<string> OfficialDLC = Array.AsReadOnly(Array.Empty<string>());

        /// <summary>
        /// TFCs that reside in the basegame directory
        /// </summary>
        public static readonly string[] BasegameTFCs =
        {
            "Textures",
            "Textures0",
            "Textures1",
            "Textures2",
            "Textures3",
            "Textures4",
            "Textures5",
            "Textures6",
            "Textures7",
            "Lighting",
            "Lighting0",
            "Lighting1",
            "Lighting2",
            "Lighting3",
            "CharTextures",
            "CharTextures0",
            "CharTextures1",
            "CharTextures2",
            "CharTextures3",
        };
    }
}
