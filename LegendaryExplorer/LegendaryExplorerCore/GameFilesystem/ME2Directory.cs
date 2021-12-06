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
    public static class ME2Directory
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
            return Path.Combine(rootPathOverride, "Binaries");
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
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "asi");
        }

        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        public static string GetTextureModMarkerPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetCookedPCPath(rootPathOverride), "BIOC_Materials.pcc");
        }

        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "MassEffect2.exe", "ME2Game.exe" });
        public static readonly ReadOnlyCollection<string> VanillaDlls = Array.AsReadOnly(new[]
        {
            "binkw23.dll", // We say this is vanilla since it will very commonly be present and should not be removed
            "binkw32.dll",
            "cudart.dll",
            "GDFDLL.dll",
            "nvtt.dll",
            "NxCooking.dll",
            "ogg.dll",
            "OpenAL32.dll",
            "PhysXExtensions.dll",
            "umbra.dll",
            "unrar.dll",
            "vorbis.dll",
            "vorbisenc.dll",
            "vorbisfile.dll",
            "wrap_oal.dll"
        });

        public static string BioWareDocumentsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", @"Mass Effect 2");
        public static string LODConfigFile => Path.Combine(BioWareDocumentsPath, @"BIOGame", @"Config", @"GamerSettings.ini");
        public static string CookedName => "CookedPC";


        private static string _gamePath;
        public static string DefaultGamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_gamePath))
                {
                    if (string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.ME2Directory))
                    {
                        return null;
                    }
                    _gamePath = LegendaryExplorerCoreLibSettings.Instance.ME2Directory;
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

        static ME2Directory()
        {
            ReloadDefaultGamePath(false);
        }

        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.ME2Directory))
            {
                DefaultGamePath = LegendaryExplorerCoreLibSettings.Instance.ME2Directory;
            }
            else
            {
#pragma warning disable CA1416
#if WINDOWS
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string subkey = @"BioWare\Mass Effect 2";

                string keyName = hkey32 + subkey;
                string test = (string)Registry.GetValue(keyName, "Path", null);
                if (test != null)
                {
                    DefaultGamePath = test;
                    LegendaryExplorerCoreLibSettings.Instance.ME2Directory = DefaultGamePath;
                    return;
                }

                keyName = hkey64 + subkey;
                DefaultGamePath = (string)Registry.GetValue(keyName, "Path", null);
                if (DefaultGamePath != null)
                {
                    DefaultGamePath += Path.DirectorySeparatorChar;
                    LegendaryExplorerCoreLibSettings.Instance.ME2Directory = DefaultGamePath;
                }
#endif
#pragma warning restore CA1416
            }
        }

        public static readonly CaseInsensitiveDictionary<string> OfficialDLCNames = new CaseInsensitiveDictionary<string>
        {
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
            ["DLC_PRE_Cerberus"] = "Cerberus Weapon and Armor",
            ["DLC_PRE_Collectors"] = "Collectors' Weapon and Armor",
            ["DLC_PRE_DA"] = "Blood Dragon Armor",
            ["DLC_PRE_Gamestop"] = "Terminus Weapon and Armor",
            ["DLC_PRE_General"] = "Inferno Armor",
            ["DLC_PRE_Incisor"] = "M-29 Incisor",
            ["DLC_PRO_Gulp01"] = "Sentry Interface",
            ["DLC_PRO_Pepper01"] = "Umbra Visor",
            ["DLC_PRO_Pepper02"] = "Recon Hood",
            ["DLC_UNC_Hammer01"] = "Firewalker Pack",
            ["DLC_UNC_Moment01"] = "Normandy Crash Site",
            ["DLC_UNC_Pack01"] = "Overlord",
        };

        public static readonly ReadOnlyCollection<string> OfficialDLC = Array.AsReadOnly(new[]
        {
            "DLC_UNC_Moment01", //95
            "DLC_HEN_VT", //100
            "DLC_PRE_Cerberus", //105
            "DLC_PRE_Collectors", //106
            "DLC_PRE_DA", //107
            "DLC_PRE_Gamestop", //108
            "DLC_PRE_General",
            "DLC_PRE_Incisor",
            "DLC_PRO_Gulp01", //111
            "DLC_PRO_Pepper01", //112
            "DLC_PRO_Pepper02", //113
            "DLC_CER_Arc", //116
            "DLC_UNC_Hammer01", //118
            "DLC_HEN_MT", //119
            "DLC_CON_Pack01", //125
            "DLC_UNC_Pack01", //132
            "DLC_CER_02",
            "DLC_MCR_01", //136
            "DLC_MCR_03",
            "DLC_EXP_Part01", //300
            "DLC_DHME1", //375
            "DLC_CON_Pack02", //380
            "DLC_EXP_Part02", //400
        });

        /// <summary>
        /// TFCs that reside in the basegame directory
        /// </summary>
        public static readonly string[] BasegameTFCs = { "Textures" };

        public static bool IsValidGameDir(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "Binaries", "MassEffect2.exe"));
        }
    }
}
