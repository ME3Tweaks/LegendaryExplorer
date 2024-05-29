using System;
using System.Collections.ObjectModel;
using System.IO;
using LegendaryExplorerCore.Misc;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Contains information about the ME3 game directory
    /// </summary>
    public static class ME3Directory
    {
        /// <summary>
        /// Gets the path to the BioGame folder for ME3
        /// </summary>
        public static string BioGamePath => GetBioGamePath();
        /// <summary>
        /// Gets the path to the BioGame folder for ME3
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to BioGame folder, null if no usable root path</returns>
        public static string GetBioGamePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "BIOGame");
        }

        /// <summary>
        /// Gets the path to the DLC folder for ME3
        /// </summary>
        public static string DLCPath => GetDLCPath();
        /// <summary>
        /// Gets the path to the DLC folder for ME3
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
        /// Gets the path to the basegame Cooked folder for ME3
        /// </summary>
        public static string CookedPCPath => GetCookedPCPath();
        /// <summary>
        /// Gets the path to basegame Cooked folder for ME3
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
        /// Gets the path to the executable folder for ME3
        /// </summary>
        public static string ExecutableFolder => GetExecutableDirectory();
        /// <summary>
        /// Gets the path to the executable folder for ME3
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to executable folder, null if no usable root path</returns>
        public static string GetExecutableDirectory(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "Binaries", "Win32");
        }

        /// <summary>
        /// Gets the path to the game executable for ME3
        /// </summary>
        public static string ExecutablePath => GetExecutablePath();
        /// <summary>
        /// Gets the path to the game executable for ME3
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
        /// Gets the path to the ASI install directory for ME3
        /// </summary>
        public static string ASIPath => GetASIPath();
        /// <summary>
        /// Gets the path to the ASI install directory for ME3
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to ASI folder, null if no usable root path</returns>
        public static string GetASIPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "asi");
        }

        /// <summary>
        /// Gets the path to the texture mod marker file for ME3
        /// </summary>
        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        /// <summary>
        /// Gets the path to the texture mod marker file for ME3
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to texture mod marker, null if no usable root path</returns>
        public static string GetTextureModMarkerPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetCookedPCPath(rootPathOverride), "adv_combat_tutorial_xbox_D_Int.afc");
        }

        /// <summary>
        /// Gets the path to the TestPatch SFAR for ME3
        /// </summary>
        public static string TestPatchSFARPath => GetTestPatchSFARPath();
        /// <summary>
        /// Gets the path to the TestPatch SFAR for ME3
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to test patch, null if no usable root path</returns>
        public static string GetTestPatchSFARPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), "Patches", "PCConsole", "Patch_001.sfar");
        }

        /// <summary>
        /// The filenames of any valid ME3 executables
        /// </summary>
        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new [] { "MassEffect3.exe" });

        /// <summary>
        /// Gets the list of vanilla DLL filenames that ship with ME3
        /// </summary>
        /// <remarks>This list will include both the bink bypass and the original renamed bink dll</remarks>
        public static ReadOnlyCollection<string> VanillaDlls = Array.AsReadOnly(new []
        {
            "atiags.dll",
            "binkw32.dll",
            "binkw23.dll", // We say this is vanilla since it will very commonly be present and should not be removed
            "PhysXExtensions.dll"
        });

        /// <summary>
        /// Gets the path of the ME3 folder in the user's Documents/BioWare folder. This is where savegames and some configuration files are stored.
        /// </summary>
        public static string BioWareDocumentsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", @"Mass Effect 3");

        /// <summary>
        /// Path to the folder where career profiles are stored in the user config directory.
        /// </summary>
        public static string SaveFolderPath => Path.Combine(LEDirectory.BioWareDocumentsPath, @"Save");

        /// <summary>
        /// Path to the persistent storage file in the user config directory.
        /// </summary>
        public static string LocalProfilePath => Path.Combine(SaveFolderPath, @"Local_Profile.sav");

        /// <summary>
        /// Gets the path to the LOD configuration file for ME3
        /// </summary>
        public static string LODConfigFile => Path.Combine(BioWareDocumentsPath, @"BIOGame", @"Config", @"GamerSettings.ini");

        /// <summary>
        /// Gets the name of the Cooked folder for ME3
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
                    if (string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.ME3Directory))
                    {
                        return null;
                    }
                    _DefaultGamePath = LegendaryExplorerCoreLibSettings.Instance.ME3Directory;
                }
                return Path.GetFullPath(_DefaultGamePath); //normalize
            }
            set
            {
                if (value != null)
                {
                    if (value.Contains("BioGame", StringComparison.OrdinalIgnoreCase))
                        value = value.Substring(0, value.LastIndexOf("BioGame", StringComparison.OrdinalIgnoreCase));
                }
                _DefaultGamePath = value;
            }
        }
        
        /// <summary>
        /// Gets the path to the basegame PCConsoleTOC.bin file for ME3
        /// </summary>
        public static string TocFile => DefaultGamePath != null ? Path.Combine(DefaultGamePath, @"BIOGame\PCConsoleTOC.bin") : null;
        
        
        /// <summary>
        /// TFCs that reside in the basegame directory
        /// </summary>
        public static readonly string[] BasegameTFCs = { "CharTextures", "Movies", "Textures", "Lighting" };

        static ME3Directory()
        {
            ReloadDefaultGamePath(false);
        }

        /// <summary>
        /// Reloads the default ME3 game path, either from LEC settings or from the registry
        /// </summary>
        /// <param name="forceUseRegistry">If true, registry will be used to determine game path. If false, LEC settings may be used instead</param>
        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.ME3Directory))
            {
                DefaultGamePath = LegendaryExplorerCoreLibSettings.Instance.ME3Directory;
            }
            else
            {
#pragma warning disable CA1416
#if WINDOWS
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string subkey = @"BioWare\Mass Effect 3";

                string keyName = hkey32 + subkey;
                string test = (string)Registry.GetValue(keyName, "Install Dir", null);
                if (test != null)
                {
                    DefaultGamePath = test;
                    LegendaryExplorerCoreLibSettings.Instance.ME3Directory = DefaultGamePath;
                    return;
                }

                keyName = hkey64 + subkey;
                DefaultGamePath = (string)Registry.GetValue(keyName, "Install Dir", null);
                if (DefaultGamePath != null)
                {
                    DefaultGamePath += Path.DirectorySeparatorChar;
                    LegendaryExplorerCoreLibSettings.Instance.ME3Directory = DefaultGamePath;
                }
#endif
#pragma warning restore CA1416
            }
        }

        /// <summary>
        /// Gets a mapping of official DLC folder names to human readable names for ME3
        /// </summary>
        public static readonly CaseInsensitiveDictionary<string> OfficialDLCNames = new CaseInsensitiveDictionary<string>
        {
            ["DLC_HEN_PR"] = "From Ashes",
            ["DLC_OnlinePassHidCE"] = "Collectors Edition Content",
            ["DLC_CON_MP1"] = "Resurgence",
            ["DLC_CON_MP2"] = "Rebellion",
            ["DLC_CON_MP3"] = "Earth",
            ["DLC_CON_END"] = "Extended Cut",
            ["DLC_CON_GUN01"] = "Firefight Pack",
            ["DLC_EXP_Pack001"] = "Leviathan",
            ["DLC_UPD_Patch01"] = "Multiplayer Balance Changes Cache 1",
            ["DLC_CON_MP4"] = "Retaliation",
            ["DLC_CON_GUN02"] = "Groundside Resistance Pack",
            ["DLC_EXP_Pack002"] = "Omega",
            ["DLC_CON_APP01"] = "Alternate Appearance Pack 1",
            ["DLC_UPD_Patch02"] = "Multiplayer Balance Changes Cache 2",
            ["DLC_CON_MP5"] = "Reckoning",
            ["DLC_EXP_Pack003_Base"] = "Citadel - Part I",
            ["DLC_EXP_Pack003"] = "Citadel - Part II",
            ["DLC_CON_DH1"] = "Genesis 2"
        };

        /// <summary>
        /// Gets a list of official DLC folder names for ME3
        /// </summary>
        public static readonly ReadOnlyCollection<string> OfficialDLC = Array.AsReadOnly(new[]
        {
            "DLC_HEN_PR",
            "DLC_OnlinePassHidCE",
            "DLC_CON_MP1",
            "DLC_CON_MP2",
            "DLC_CON_MP3",
            "DLC_CON_END",
            "DLC_CON_GUN01",
            "DLC_EXP_Pack001",
            "DLC_UPD_Patch01",
            "DLC_CON_MP4",
            "DLC_CON_GUN02",
            "DLC_EXP_Pack002",
            "DLC_CON_APP01",
            "DLC_UPD_Patch02",
            "DLC_CON_MP5",
            "DLC_EXP_Pack003_Base",
            "DLC_EXP_Pack003",
            "DLC_CON_DH1"
        });

        /// <summary>
        /// Determines if a Mass Effect 3 folder is a valid game directory by checking for the game executable
        /// </summary>
        /// <param name="rootPath">Path to check</param>
        /// <returns>True if directory is valid, false otherwise</returns>
        public static bool IsValidGameDir(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "Binaries", "Win32", "MassEffect3.exe"));
        }
    }
}
