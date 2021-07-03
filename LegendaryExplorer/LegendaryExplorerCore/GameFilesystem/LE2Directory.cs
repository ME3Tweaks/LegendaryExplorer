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
    public static class LE2Directory
    {
        public static string BioGamePath => GetBioGamePath();
        public static string GetBioGamePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "BioGame");
        }

        public static string DLCPath => GetDLCPath();
        public static string GetDLCPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), "DLC");
        }

        public static string CookedPCPath => GetCookedPCPath();
        public static string GetCookedPCPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), CookedName);
        }

        public static string ExecutableFolder => GetExecutableDirectory();
        public static string GetExecutableDirectory(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "Binaries", "Win64");
        }

        public static string ExecutablePath => GetExecutablePath();
        public static string GetExecutablePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "MassEffect2.exe");
        }

        public static string ASIPath => GetASIPath();
        public static string GetASIPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "ASI");
        }

        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        public static string GetTextureModMarkerPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetCookedPCPath(rootPathOverride), "SFXTest.pcc");
        }

        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "MassEffect2.exe", });
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

        public static string BioWareDocumentsPath => LEDirectory.BioWareDocumentsPath;
        public static string LODConfigFile => Path.Combine(BioGamePath, @"Config", @"GamerSettings.ini");
        public static string CookedName => "CookedPCConsole";


        private static string _gamePath;
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

        public static string TocFile => DefaultGamePath != null ? Path.Combine(DefaultGamePath, @"BioGame", "PCConsoleTOC.bin") : null;

        static LE2Directory()
        {
            ReloadDefaultGamePath(false);
        }

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
