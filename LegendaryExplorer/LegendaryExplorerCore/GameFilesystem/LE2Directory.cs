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
    /// Contains information about the LE2 game directory
    /// </summary>
    public static class LE2Directory
    {
        /// <summary>
        /// Gets the path to the BioGame folder for LE2
        /// </summary>
        public static string BioGamePath => GetBioGamePath();
        /// <summary>
        /// Gets the path to the BioGame folder for LE2
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
        /// Gets the path to the DLC folder for LE2
        /// </summary>
        public static string DLCPath => GetDLCPath();
        /// <summary>
        /// Gets the path to the DLC folder for LE2
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
        /// Gets the path to the basegame Cooked folder for LE2
        /// </summary>
        public static string CookedPCPath => GetCookedPCPath();
        /// <summary>
        /// Gets the path to basegame Cooked folder for LE2
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
        /// Gets the path to the executable folder for LE2
        /// </summary>
        public static string ExecutableFolder => GetExecutableDirectory();
        /// <summary>
        /// Gets the path to the executable folder for LE2
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
        /// Gets the path to the game executable for LE2
        /// </summary>
        public static string ExecutablePath => GetExecutablePath();
        /// <summary>
        /// Gets the path to the game executable for LE2
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to game executable, null if no usable root path</returns>
        public static string GetExecutablePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "MassEffect2.exe");
        }

        /// <summary>
        /// Gets the path to the ASI install directory for LE2
        /// </summary>
        public static string ASIPath => GetASIPath();
        /// <summary>
        /// Gets the path to the ASI install directory for LE2
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
        /// Gets the path to the texture mod marker file for LE2
        /// </summary>
        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        /// <summary>
        /// Gets the path to the texture mod marker file for LE2
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
        /// The filenames of any valid LE2 executables
        /// </summary>
        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "MassEffect2.exe", });

        /// <summary>
        /// Gets the list of vanilla DLL filenames that ship with LE2
        /// </summary>
        /// <remarks>This list will include both the bink bypass and the original renamed bink dll</remarks>
        public static readonly ReadOnlyCollection<string> VanillaDlls = Array.AsReadOnly(new[]
        {
            "bink2w64_original.dll", // We say this is vanilla since it will very commonly be present and should not be removed
            "amd_ags_x64.dll",
            "bink2w64_original.dll",
            "bink2w64.dll",
            "dbdata.dll",
            "oo2core_8_win64.dll",
            "PhysXCooking64.dll",
            "PhysXCore64.dll",
        });

        /// <summary>
        /// Gets the path of the LE2 folder in the user's Documents/BioWare folder. This is where savegames and some configuration files are stored.
        /// </summary>
        /// <remarks>This is the same folder for all LE games</remarks>
        public static string BioWareDocumentsPath => LEDirectory.BioWareDocumentsPath;

        /// <summary>
        /// Path to the folder where career profiles are stored in the user config directory.
        /// </summary>
        public static string SaveFolderPath => Path.Combine(LEDirectory.BioWareDocumentsPath, @"Save", @"ME2");

        /// <summary>
        /// Path to the 'Local_Profile' file in the user config directory.
        /// </summary>
        public static string LocalProfilePath => Path.Combine(SaveFolderPath, @"GamerProfile");

        /// <summary>
        /// Gets the path to the LOD configuration file for LE2
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
        /// Gets the name of the Cooked folder for LE2
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
                    _gamePath = Path.Combine(LegendaryExplorerCoreLibSettings.Instance.LEDirectory, "Game", "ME2");
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
        /// Gets the path to the basegame PCConsoleTOC.bin file for LE2
        /// </summary>
        public static string TocFile => DefaultGamePath != null ? Path.Combine(DefaultGamePath, @"BioGame", "PCConsoleTOC.bin") : null;

        static LE2Directory()
        {
            ReloadDefaultGamePath(false);
        }

        /// <summary>
        /// Reloads the default LE2 game path, either from LEC settings or from the registry
        /// </summary>
        /// <param name="forceUseRegistry">If true, registry will be used to determine game path. If false, LEC settings may be used instead</param>
        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.LEDirectory))
            {
                DefaultGamePath = Path.Join(LegendaryExplorerCoreLibSettings.Instance.LEDirectory, "Game", "ME2");
            }
            else
            {
#if WINDOWS
                if (LEDirectory.LookupDefaultPath()) ReloadDefaultGamePath(false);
#endif
            }
        }

        /// <summary>
        /// Gets a mapping of official DLC folder names to human readable names for LE2
        /// </summary>
        public static readonly CaseInsensitiveDictionary<string> OfficialDLCNames = new()
        {
            ["DLC_00_Shared"] = "Shared TLK",
            ["DLC_CER_01"] = "Cerberus Network",
            ["DLC_CER_02"] = "Aegis Pack",
            ["DLC_CER_Arc"] = "Arc Projector",
            ["DLC_CON_Pack01"] = "Alternate Appearance Pack 1",
            ["DLC_CON_Pack02"] = "Alternate Appearance Pack 2",
            ["DLC_DHME1"] = "Genesis",
            ["DLC_EXP_Part01"] = "Lair of the Shadow Broker",
            ["DLC_EXP_Part02"] = "Arrival",
            ["DLC_HEN_MT"] = "Kasumi - Stolen Memory",
            ["DLC_HEN_VT"] = "Zaeed - The Price of Revenge",
            ["DLC_MCR_01"] = "Firepower pack",
            ["DLC_MCR_03"] = "Equalizer pack",
            ["DLC_METR_Patch01"] = "Legendary Edition Patch",
            ["DLC_PRE_Cerberus"] = "Cerberus Weapon and Armor",
            ["DLC_PRE_Collectors"] = "Collectors' Weapon and Armor",
            ["DLC_PRE_DA"] = "Blood Dragon Armor",
            ["DLC_PRE_General"] = "Inferno Armor",
            ["DLC_PRE_Incisor"] = "M-29 Incisor",
            ["DLC_PRE_Terminus"] = "Terminus Weapon and Armor",
            ["DLC_PRO_Gulp01"] = "Sentry Interface",
            ["DLC_PRO_Pepper01"] = "Umbra Visor",
            ["DLC_PRO_Pepper02"] = "Recon Hood",
            ["DLC_UNC_Hammer01"] = "Firewalker Pack",
            ["DLC_UNC_Moment01"] = "Normandy Crash Site",
            ["DLC_UNC_Pack01"] = "Overlord",
            ["DLC_UPD_Patch01"] = "Patch 1",
            ["DLC_UPD_Patch02"] = "Patch 2",
            ["DLC_UPD_Patch03"] = "Patch 3",
        };

        /// <summary>
        /// Gets a list of official DLC folder names for LE2
        /// </summary>
        public static readonly ReadOnlyCollection<string> OfficialDLC = Array.AsReadOnly(new[]
        {
            "DLC_00_Shared", //0
            "DLC_UNC_Moment01", //95
            "DLC_HEN_VT", //100
            "DLC_PRE_Incisor", //104
            "DLC_PRE_Cerberus", //105
            "DLC_PRE_Collectors", //106
            "DLC_PRE_DA", //107
            "DLC_PRE_Terminus", //108
            "DLC_PRE_General", //109
            "DLC_CER_01", //110
            "DLC_PRO_Gulp01", //111
            "DLC_PRO_Pepper01", //112
            "DLC_PRO_Pepper02", //113
            "DLC_CER_Arc", //116
            "DLC_UNC_Hammer01", //118
            "DLC_HEN_MT", //119
            "DLC_CON_Pack01", //125
            "DLC_MCR_03", //128
            "DLC_UNC_Pack01", //132
            "DLC_CER_02", //134
            "DLC_MCR_01", //136
            "DLC_EXP_Part01", //300
            "DLC_DHME1", //375
            "DLC_CON_Pack02", //380
            "DLC_EXP_Part02", //400
            "DLC_UPD_Patch01", //1000
            "DLC_UPD_Patch02", //1010
            "DLC_UPD_Patch03", //1020
            "DLC_METR_Patch01", //2000
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
            "CharTextures0_DLC_00_Shared",
            "CharTextures1_DLC_00_Shared",
            "CharTextures3_DLC_00_Shared",
            "CharTextures5_DLC_00_Shared",
            "CharTextures7_DLC_00_Shared",
            "Textures0_DLC_00_Shared",
            "Textures1_DLC_00_Shared",
            "Textures2_DLC_00_Shared",
            "Textures3_DLC_00_Shared",
            "Textures4_DLC_00_Shared",
            "Textures5_DLC_00_Shared",
            "Textures6_DLC_00_Shared",
            "Textures7_DLC_00_Shared",
            "Textures4_DLC_CER_01",
            "CharTextures3_DLC_CER_02",
            "CharTextures6_DLC_CER_02",
            "Textures0_DLC_CER_02",
            "Textures1_DLC_CER_02",
            "Textures2_DLC_CER_02",
            "Textures3_DLC_CER_02",
            "Textures4_DLC_CER_02",
            "Textures5_DLC_CER_02",
            "Textures6_DLC_CER_02",
            "Textures7_DLC_CER_02",
            "CharTextures3_DLC_CER_Arc",
            "Textures0_DLC_CER_Arc",
            "Textures1_DLC_CER_Arc",
            "CharTextures1_DLC_CON_Pack01",
            "CharTextures2_DLC_CON_Pack01",
            "CharTextures3_DLC_CON_Pack01",
            "CharTextures4_DLC_CON_Pack01",
            "CharTextures6_DLC_CON_Pack01",
            "CharTextures7_DLC_CON_Pack01",
            "Textures0_DLC_CON_Pack01",
            "Textures1_DLC_CON_Pack01",
            "Textures2_DLC_CON_Pack01",
            "Textures3_DLC_CON_Pack01",
            "Textures4_DLC_CON_Pack01",
            "Textures5_DLC_CON_Pack01",
            "Textures6_DLC_CON_Pack01",
            "Textures7_DLC_CON_Pack01",
            "CharTextures1_DLC_CON_Pack02",
            "CharTextures2_DLC_CON_Pack02",
            "CharTextures3_DLC_CON_Pack02",
            "CharTextures4_DLC_CON_Pack02",
            "CharTextures5_DLC_CON_Pack02",
            "CharTextures6_DLC_CON_Pack02",
            "CharTextures7_DLC_CON_Pack02",
            "Textures0_DLC_CON_Pack02",
            "Textures1_DLC_CON_Pack02",
            "Textures2_DLC_CON_Pack02",
            "Textures3_DLC_CON_Pack02",
            "Textures4_DLC_CON_Pack02",
            "Textures5_DLC_CON_Pack02",
            "Textures6_DLC_CON_Pack02",
            "Textures7_DLC_CON_Pack02",
            "Lighting0_DLC_DHME1",
            "Lighting1_DLC_DHME1",
            "Lighting2_DLC_DHME1",
            "Lighting3_DLC_DHME1",
            "Lighting4_DLC_DHME1",
            "Lighting6_DLC_DHME1",
            "Textures0_DLC_DHME1",
            "Textures1_DLC_DHME1",
            "Textures2_DLC_DHME1",
            "Textures3_DLC_DHME1",
            "Textures4_DLC_DHME1",
            "Textures5_DLC_DHME1",
            "Textures6_DLC_DHME1",
            "Textures7_DLC_DHME1",
            "CharTextures0_DLC_EXP_Part01",
            "CharTextures1_DLC_EXP_Part01",
            "CharTextures2_DLC_EXP_Part01",
            "CharTextures3_DLC_EXP_Part01",
            "CharTextures4_DLC_EXP_Part01",
            "CharTextures5_DLC_EXP_Part01",
            "CharTextures6_DLC_EXP_Part01",
            "CharTextures7_DLC_EXP_Part01",
            "Lighting0_DLC_EXP_Part01",
            "Lighting1_DLC_EXP_Part01",
            "Lighting2_DLC_EXP_Part01",
            "Lighting3_DLC_EXP_Part01",
            "Lighting4_DLC_EXP_Part01",
            "Lighting5_DLC_EXP_Part01",
            "Lighting6_DLC_EXP_Part01",
            "Lighting7_DLC_EXP_Part01",
            "Textures0_DLC_EXP_Part01",
            "Textures1_DLC_EXP_Part01",
            "Textures2_DLC_EXP_Part01",
            "Textures3_DLC_EXP_Part01",
            "Textures4_DLC_EXP_Part01",
            "Textures5_DLC_EXP_Part01",
            "Textures6_DLC_EXP_Part01",
            "Textures7_DLC_EXP_Part01",
            "CharTextures0_DLC_EXP_Part02",
            "CharTextures1_DLC_EXP_Part02",
            "CharTextures2_DLC_EXP_Part02",
            "CharTextures3_DLC_EXP_Part02",
            "CharTextures4_DLC_EXP_Part02",
            "CharTextures5_DLC_EXP_Part02",
            "CharTextures6_DLC_EXP_Part02",
            "CharTextures7_DLC_EXP_Part02",
            "Lighting0_DLC_EXP_Part02",
            "Lighting1_DLC_EXP_Part02",
            "Lighting2_DLC_EXP_Part02",
            "Lighting3_DLC_EXP_Part02",
            "Lighting4_DLC_EXP_Part02",
            "Lighting5_DLC_EXP_Part02",
            "Lighting6_DLC_EXP_Part02",
            "Lighting7_DLC_EXP_Part02",
            "Textures0_DLC_EXP_Part02",
            "Textures1_DLC_EXP_Part02",
            "Textures2_DLC_EXP_Part02",
            "Textures3_DLC_EXP_Part02",
            "Textures4_DLC_EXP_Part02",
            "Textures5_DLC_EXP_Part02",
            "Textures6_DLC_EXP_Part02",
            "Textures7_DLC_EXP_Part02",
            "CharTextures0_DLC_HEN_MT",
            "CharTextures1_DLC_HEN_MT",
            "CharTextures2_DLC_HEN_MT",
            "CharTextures3_DLC_HEN_MT",
            "CharTextures4_DLC_HEN_MT",
            "CharTextures5_DLC_HEN_MT",
            "CharTextures6_DLC_HEN_MT",
            "CharTextures7_DLC_HEN_MT",
            "Lighting0_DLC_HEN_MT",
            "Lighting1_DLC_HEN_MT",
            "Lighting2_DLC_HEN_MT",
            "Lighting3_DLC_HEN_MT",
            "Lighting4_DLC_HEN_MT",
            "Lighting5_DLC_HEN_MT",
            "Lighting6_DLC_HEN_MT",
            "Lighting7_DLC_HEN_MT",
            "Textures0_DLC_HEN_MT",
            "Textures1_DLC_HEN_MT",
            "Textures2_DLC_HEN_MT",
            "Textures3_DLC_HEN_MT",
            "Textures4_DLC_HEN_MT",
            "Textures5_DLC_HEN_MT",
            "Textures6_DLC_HEN_MT",
            "Textures7_DLC_HEN_MT",
            "CharTextures0_DLC_HEN_VT",
            "CharTextures1_DLC_HEN_VT",
            "CharTextures2_DLC_HEN_VT",
            "CharTextures3_DLC_HEN_VT",
            "CharTextures5_DLC_HEN_VT",
            "CharTextures7_DLC_HEN_VT",
            "Lighting0_DLC_HEN_VT",
            "Lighting1_DLC_HEN_VT",
            "Lighting2_DLC_HEN_VT",
            "Lighting3_DLC_HEN_VT",
            "Lighting4_DLC_HEN_VT",
            "Lighting5_DLC_HEN_VT",
            "Lighting6_DLC_HEN_VT",
            "Lighting7_DLC_HEN_VT",
            "Textures0_DLC_HEN_VT",
            "Textures1_DLC_HEN_VT",
            "Textures2_DLC_HEN_VT",
            "Textures3_DLC_HEN_VT",
            "Textures4_DLC_HEN_VT",
            "Textures5_DLC_HEN_VT",
            "Textures6_DLC_HEN_VT",
            "Textures7_DLC_HEN_VT",
            "CharTextures1_DLC_MCR_01",
            "CharTextures2_DLC_MCR_01",
            "CharTextures3_DLC_MCR_01",
            "CharTextures4_DLC_MCR_01",
            "CharTextures6_DLC_MCR_01",
            "Textures0_DLC_MCR_01",
            "Textures1_DLC_MCR_01",
            "Textures2_DLC_MCR_01",
            "Textures3_DLC_MCR_01",
            "Textures4_DLC_MCR_01",
            "Textures7_DLC_MCR_01",
            "CharTextures1_DLC_MCR_03",
            "CharTextures2_DLC_MCR_03",
            "CharTextures3_DLC_MCR_03",
            "CharTextures4_DLC_MCR_03",
            "CharTextures5_DLC_MCR_03",
            "CharTextures6_DLC_MCR_03",
            "CharTextures7_DLC_MCR_03",
            "Textures0_DLC_MCR_03",
            "Textures1_DLC_MCR_03",
            "Textures2_DLC_MCR_03",
            "Textures3_DLC_MCR_03",
            "Textures4_DLC_MCR_03",
            "Textures5_DLC_MCR_03",
            "Textures6_DLC_MCR_03",
            "Textures7_DLC_MCR_03",
            "CharTextures2_DLC_PRE_Cerberus",
            "CharTextures7_DLC_PRE_Cerberus",
            "Textures0_DLC_PRE_Cerberus",
            "Textures1_DLC_PRE_Cerberus",
            "Textures2_DLC_PRE_Cerberus",
            "Textures4_DLC_PRE_Cerberus",
            "Textures5_DLC_PRE_Cerberus",
            "Textures6_DLC_PRE_Cerberus",
            "Textures7_DLC_PRE_Cerberus",
            "Textures0_DLC_PRE_Collectors",
            "Textures1_DLC_PRE_Collectors",
            "Textures3_DLC_PRE_Collectors",
            "Textures4_DLC_PRE_Collectors",
            "Textures5_DLC_PRE_Collectors",
            "Textures6_DLC_PRE_Collectors",
            "Textures0_DLC_PRE_DA",
            "Textures1_DLC_PRE_DA",
            "Textures2_DLC_PRE_DA",
            "Textures4_DLC_PRE_DA",
            "Textures5_DLC_PRE_DA",
            "Textures7_DLC_PRE_DA",
            "CharTextures1_DLC_PRE_General",
            "CharTextures2_DLC_PRE_General",
            "CharTextures4_DLC_PRE_General",
            "CharTextures7_DLC_PRE_General",
            "Textures0_DLC_PRE_General",
            "Textures1_DLC_PRE_General",
            "Textures2_DLC_PRE_General",
            "Textures4_DLC_PRE_General",
            "Textures5_DLC_PRE_General",
            "Textures7_DLC_PRE_General",
            "CharTextures3_DLC_PRE_Incisor",
            "CharTextures6_DLC_PRE_Incisor",
            "Textures0_DLC_PRE_Incisor",
            "Textures1_DLC_PRE_Incisor",
            "Textures4_DLC_PRE_Incisor",
            "Textures0_DLC_PRE_Terminus",
            "Textures1_DLC_PRE_Terminus",
            "Textures2_DLC_PRE_Terminus",
            "Textures3_DLC_PRE_Terminus",
            "Textures4_DLC_PRE_Terminus",
            "Textures5_DLC_PRE_Terminus",
            "Textures6_DLC_PRE_Terminus",
            "Textures7_DLC_PRE_Terminus",
            "CharTextures1_DLC_PRO_Gulp01",
            "CharTextures2_DLC_PRO_Gulp01",
            "CharTextures4_DLC_PRO_Gulp01",
            "Textures1_DLC_PRO_Gulp01",
            "Textures2_DLC_PRO_Gulp01",
            "Textures4_DLC_PRO_Gulp01",
            "CharTextures3_DLC_PRO_Pepper01",
            "CharTextures6_DLC_PRO_Pepper01",
            "Textures3_DLC_PRO_Pepper01",
            "Textures4_DLC_PRO_Pepper01",
            "CharTextures2_DLC_PRO_Pepper02",
            "CharTextures7_DLC_PRO_Pepper02",
            "Textures4_DLC_PRO_Pepper02",
            "Textures7_DLC_PRO_Pepper02",
            "CharTextures1_DLC_UNC_Hammer01",
            "CharTextures2_DLC_UNC_Hammer01",
            "CharTextures3_DLC_UNC_Hammer01",
            "CharTextures4_DLC_UNC_Hammer01",
            "CharTextures6_DLC_UNC_Hammer01",
            "CharTextures7_DLC_UNC_Hammer01",
            "Lighting0_DLC_UNC_Hammer01",
            "Lighting1_DLC_UNC_Hammer01",
            "Lighting2_DLC_UNC_Hammer01",
            "Lighting3_DLC_UNC_Hammer01",
            "Lighting4_DLC_UNC_Hammer01",
            "Lighting5_DLC_UNC_Hammer01",
            "Lighting6_DLC_UNC_Hammer01",
            "Lighting7_DLC_UNC_Hammer01",
            "Textures0_DLC_UNC_Hammer01",
            "Textures1_DLC_UNC_Hammer01",
            "Textures2_DLC_UNC_Hammer01",
            "Textures3_DLC_UNC_Hammer01",
            "Textures4_DLC_UNC_Hammer01",
            "Textures5_DLC_UNC_Hammer01",
            "Textures6_DLC_UNC_Hammer01",
            "Textures7_DLC_UNC_Hammer01",
            "CharTextures0_DLC_UNC_Moment01",
            "CharTextures1_DLC_UNC_Moment01",
            "CharTextures2_DLC_UNC_Moment01",
            "CharTextures3_DLC_UNC_Moment01",
            "CharTextures4_DLC_UNC_Moment01",
            "CharTextures5_DLC_UNC_Moment01",
            "CharTextures6_DLC_UNC_Moment01",
            "Lighting0_DLC_UNC_Moment01",
            "Lighting1_DLC_UNC_Moment01",
            "Lighting2_DLC_UNC_Moment01",
            "Lighting3_DLC_UNC_Moment01",
            "Lighting4_DLC_UNC_Moment01",
            "Lighting5_DLC_UNC_Moment01",
            "Lighting6_DLC_UNC_Moment01",
            "Lighting7_DLC_UNC_Moment01",
            "Textures0_DLC_UNC_Moment01",
            "Textures1_DLC_UNC_Moment01",
            "Textures2_DLC_UNC_Moment01",
            "Textures3_DLC_UNC_Moment01",
            "Textures4_DLC_UNC_Moment01",
            "Textures5_DLC_UNC_Moment01",
            "Textures6_DLC_UNC_Moment01",
            "Textures7_DLC_UNC_Moment01",
            "CharTextures0_DLC_UNC_Pack01",
            "CharTextures1_DLC_UNC_Pack01",
            "CharTextures2_DLC_UNC_Pack01",
            "CharTextures3_DLC_UNC_Pack01",
            "CharTextures4_DLC_UNC_Pack01",
            "CharTextures7_DLC_UNC_Pack01",
            "Lighting0_DLC_UNC_Pack01",
            "Lighting1_DLC_UNC_Pack01",
            "Lighting2_DLC_UNC_Pack01",
            "Lighting3_DLC_UNC_Pack01",
            "Lighting4_DLC_UNC_Pack01",
            "Lighting5_DLC_UNC_Pack01",
            "Lighting6_DLC_UNC_Pack01",
            "Lighting7_DLC_UNC_Pack01",
            "Textures0_DLC_UNC_Pack01",
            "Textures1_DLC_UNC_Pack01",
            "Textures2_DLC_UNC_Pack01",
            "Textures3_DLC_UNC_Pack01",
            "Textures4_DLC_UNC_Pack01",
            "Textures5_DLC_UNC_Pack01",
            "Textures6_DLC_UNC_Pack01",
            "Textures7_DLC_UNC_Pack01",
            "Textures0_DLC_UPD_Patch01",
            "Textures1_DLC_UPD_Patch01",
            "Textures2_DLC_UPD_Patch01",
            "Textures3_DLC_UPD_Patch01",
            "Textures4_DLC_UPD_Patch01",
            "Textures5_DLC_UPD_Patch01",
            "Textures6_DLC_UPD_Patch01",
            "Textures7_DLC_UPD_Patch01",
            "Textures0_DLC_UPD_Patch02",
            "Textures1_DLC_UPD_Patch02",
            "Textures2_DLC_UPD_Patch02",
            "Textures3_DLC_UPD_Patch02",
            "Textures4_DLC_UPD_Patch02",
            "Textures5_DLC_UPD_Patch02",
            "Textures6_DLC_UPD_Patch02",
            "Textures7_DLC_UPD_Patch02",
            "Textures0_DLC_UPD_Patch03",
            "Textures1_DLC_UPD_Patch03",
            "Textures2_DLC_UPD_Patch03",
            "Textures3_DLC_UPD_Patch03",
            "Textures4_DLC_UPD_Patch03",
            "Textures5_DLC_UPD_Patch03",
            "Textures6_DLC_UPD_Patch03",
            "Textures7_DLC_UPD_Patch03",
            "CharTextures0_DLC_METR_Patch01",
            "CharTextures1_DLC_METR_Patch01",
            "CharTextures2_DLC_METR_Patch01",
            "CharTextures3_DLC_METR_Patch01",
            "CharTextures4_DLC_METR_Patch01",
            "CharTextures5_DLC_METR_Patch01",
            "CharTextures6_DLC_METR_Patch01",
            "CharTextures7_DLC_METR_Patch01",
            "Lighting0_DLC_METR_Patch01",
            "Lighting1_DLC_METR_Patch01",
            "Lighting2_DLC_METR_Patch01",
            "Lighting3_DLC_METR_Patch01",
            "Lighting4_DLC_METR_Patch01",
            "Lighting5_DLC_METR_Patch01",
            "Lighting6_DLC_METR_Patch01",
            "Lighting7_DLC_METR_Patch01",
            "Textures0_DLC_METR_Patch01",
            "Textures1_DLC_METR_Patch01",
            "Textures2_DLC_METR_Patch01",
            "Textures3_DLC_METR_Patch01",
            "Textures4_DLC_METR_Patch01",
            "Textures5_DLC_METR_Patch01",
            "Textures6_DLC_METR_Patch01",
            "Textures7_DLC_METR_Patch01"
        };
    }
}
