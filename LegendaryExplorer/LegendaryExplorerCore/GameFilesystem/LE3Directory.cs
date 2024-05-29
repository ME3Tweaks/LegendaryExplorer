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
    /// Contains information about the LE3 game directory
    /// </summary>
    public static class LE3Directory
    {
        /// <summary>
        /// Gets the path to the BioGame folder for LE3
        /// </summary>
        public static string BioGamePath => GetBioGamePath();
        /// <summary>
        /// Gets the path to the BioGame folder for LE3
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
        /// Gets the path to the DLC folder for LE3
        /// </summary>
        public static string DLCPath => GetDLCPath();
        /// <summary>
        /// Gets the path to the DLC folder for LE3
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
        /// Gets the path to the basegame Cooked folder for LE3
        /// </summary>
        public static string CookedPCPath => GetCookedPCPath();
        /// <summary>
        /// Gets the path to basegame Cooked folder for LE3
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
        /// Gets the path to the executable folder for LE3
        /// </summary>
        public static string ExecutableFolder => GetExecutableDirectory();
        /// <summary>
        /// Gets the path to the executable folder for LE3
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
        /// Gets the path to the game executable for LE3
        /// </summary>
        public static string ExecutablePath => GetExecutablePath();
        /// <summary>
        /// Gets the path to the game executable for LE3
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to game executable, null if no usable root path</returns>
        public static string GetExecutablePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "MassEffect3.exe");
        }

        /// <summary>
        /// Gets the path to the ASI install directory for LE3
        /// </summary>
        public static string ASIPath => GetASIPath();
        /// <summary>
        /// Gets the path to the ASI install directory for LE3
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
        /// Gets the path to the texture mod marker file for LE3
        /// </summary>
        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        /// <summary>
        /// Gets the path to the texture mod marker file for LE3
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
        /// The filenames of any valid LE3 executables
        /// </summary>
        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new [] { "MassEffect3.exe" });

        /// <summary>
        /// Gets the list of vanilla DLL filenames that ship with LE3
        /// </summary>
        /// <remarks>This list will include both the bink bypass and the original renamed bink dll</remarks>
        public static ReadOnlyCollection<string> VanillaDlls = Array.AsReadOnly(new []
        {
            "bink2w64_original.dll", // We say this is vanilla since it will very commonly be present and should not be removed
            "amd_ags_x64.dll",
            "bink2w64_original.dll",
            "bink2w64.dll",
            "dbdata.dll",
            "oo2core_8_win64.dll",
            "PhysXCooking64.dll",
            "PhysXCore64.dll"
        });

        /// <summary>
        /// Gets the path of the LE3 folder in the user's Documents/BioWare folder. This is where savegames and some configuration files are stored.
        /// </summary>
        /// <remarks>This is the same folder for all LE games</remarks>
        public static string BioWareDocumentsPath => LEDirectory.BioWareDocumentsPath;

        /// <summary>
        /// Path to the folder where career profiles are stored in the user config directory.
        /// </summary>
        public static string SaveFolderPath => Path.Combine(LEDirectory.BioWareDocumentsPath,@"Save", @"ME3");

        /// <summary>
        /// Path to the 'Local_Profile' file in the user config directory.
        /// </summary>
        public static string LocalProfilePath => Path.Combine(SaveFolderPath, @"Local_Profile");

        /// <summary>
        /// Gets the path to the LOD configuration file for LE3
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
        /// Gets the name of the Cooked folder for LE3
        /// </summary>
        public static string CookedName => "CookedPCConsole";

        private static string _DefaultGamePath;
        /// <summary>
        /// Gets or sets the default game root path that is used when locating game folders.
        /// By default, this path is loaded from the <see cref="LegendaryExplorerCoreLibSettings"/> instance.
        /// Updating this path will not update the value in the CoreLibSettings.
        /// </summary>
        public static string DefaultGamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_DefaultGamePath))
                {
                    if (string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.LEDirectory))
                    {
                        return null;
                    }
                    _DefaultGamePath = Path.Combine(LegendaryExplorerCoreLibSettings.Instance.LEDirectory, "Game", "ME3");
                }
                return Path.GetFullPath(_DefaultGamePath); //normalize
            }
            set
            {
                if (value != null)
                {
                    if (value.Contains("BIOGame", StringComparison.OrdinalIgnoreCase))
                        value = value.Substring(0, value.LastIndexOf("BIOGame", StringComparison.OrdinalIgnoreCase));
                }
                _DefaultGamePath = value;
            }
        }
        
        /// <summary>
        /// Gets the path to the basegame PCConsoleTOC.bin file for LE3
        /// </summary>
        public static string TocFile => DefaultGamePath != null ? Path.Combine(DefaultGamePath, @"BIOGame", "PCConsoleTOC.bin") : null;

        static LE3Directory()
        {
            ReloadDefaultGamePath(false);
        }

        /// <summary>
        /// Reloads the default LE3 game path, either from LEC settings or from the registry
        /// </summary>
        /// <param name="forceUseRegistry">If true, registry will be used to determine game path. If false, LEC settings may be used instead</param>
        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.LEDirectory))
            {
                DefaultGamePath = Path.Join(LegendaryExplorerCoreLibSettings.Instance.LEDirectory, "Game", "ME3");
            }
            else
            {
#if WINDOWS
                if (LEDirectory.LookupDefaultPath()) ReloadDefaultGamePath(false);
#endif
            }
        }

        /// <summary>
        /// Gets a mapping of official DLC folder names to human readable names for LE3
        /// </summary>
        public static readonly CaseInsensitiveDictionary<string> OfficialDLCNames = new()
        {
            ["DLC_CON_APP01"] = "Alternate Appearance Pack 1",
            ["DLC_CON_DH1"] = "Genesis 2",
            ["DLC_CON_END"] = "Extended Cut",
            ["DLC_HEN_PR"] = "From Ashes",
            ["DLC_CON_GUN01"] = "Firefight Pack",
            ["DLC_CON_GUN02"] = "Groundside Resistance Pack",
            ["DLC_CON_PRO1"] = "N7 Warfare Gear",
            ["DLC_CON_PRO2"] = "M55 Argus",
            ["DLC_CON_PRO3"] = "AT12 Raider, M55 Argus ",
            ["DLC_CON_PRO4"] = "M90 Indra ",
            ["DLC_CON_PRO5"] = "Reckoner Knight",
            ["DLC_CON_PRO6"] = "Chakram Launcher",
            ["DLC_EXP_Pack001"] = "Leviathan",
            ["DLC_EXP_Pack002"] = "Omega",
            ["DLC_EXP_Pack003"] = "Citadel - Part II",
            ["DLC_EXP_Pack003_Base"] = "Citadel - Part I",
            ["DLC_METR_Patch01"] = "Legendary Edition Patch",
            ["DLC_OnlinePassHidCE"] = "Collectors Edition Content",
            ["DLC_UPD_Patch01"] = "Multiplayer Balance Changes Cache 1",
            ["DLC_UPD_Patch02"] = "Multiplayer Balance Changes Cache 2",
        };

        /// <summary>
        /// Gets a list of official DLC folder names for LE3
        /// </summary>
        public static readonly ReadOnlyCollection<string> OfficialDLC = Array.AsReadOnly(new[]
        {
            "DLC_HEN_PR", //2000
            "DLC_OnlinePassHidCE", //2040
            "DLC_CON_PRO1", //2060
            "DLC_CON_PRO2", //2070
            "DLC_CON_PRO3", //2080
            "DLC_CON_PRO4", //2090
            "DLC_CON_PRO5", //2100
            "DLC_CON_PRO6", //2110
            "DLC_CON_END", //2900
            "DLC_CON_GUN01", //2950
            "DLC_EXP_Pack001", //2999
            "DLC_UPD_Patch01", //3025
            "DLC_CON_GUN02", //3100
            "DLC_EXP_Pack002", //3200
            "DLC_CON_APP01", //3210
            "DLC_UPD_Patch02", //3220
            "DLC_EXP_Pack003_Base", //3249
            "DLC_EXP_Pack003", //3250
            "DLC_CON_DH1", //3300
            "DLC_METR_Patch01" //4000
        });

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
            "Lighting4",
            "Lighting5",
            "Lighting6",
            "Lighting7",
            "CharTextures",
            "CharTextures0",
            "CharTextures1",
            "CharTextures2",
            "CharTextures3",
            "CharTextures4",
            "CharTextures5",
            "CharTextures6",
            "CharTextures7",
            "Movies",
            "Textures_DLC_CON_APP01",
            "Textures_DLC_CON_END",
            "Textures_DLC_CON_GUN01",
            "Textures_DLC_CON_GUN02",
            "Textures_DLC_EXP_Pack001",
            "Textures_DLC_EXP_Pack002",
            "Textures_DLC_EXP_Pack003",
            "Textures_DLC_EXP_Pack003_Base",
            "Textures_DLC_HEN_PR",
            "Textures_DLC_OnlinePassHidCE",
            "Textures_DLC_METR_Patch01"
        };
    }
}
