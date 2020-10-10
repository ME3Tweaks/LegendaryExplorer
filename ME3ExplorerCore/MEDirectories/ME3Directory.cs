using ME3ExplorerCore.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using ME3ExplorerCore.Misc;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace ME3ExplorerCore.MEDirectories
{
    public static class ME3Directory
    {
        private static string _gamePath;
        public static string gamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_gamePath))
                    return null;
                return Path.GetFullPath(_gamePath); //normalize
            }
            set
            {
                if (value != null)
                {
                    if (value.Contains("BIOGame"))
                        value = value.Substring(0, value.LastIndexOf("BIOGame"));
                }
                _gamePath = value;
            }
        }
        public static string BIOGamePath => gamePath != null ? gamePath.Contains("biogame", StringComparison.OrdinalIgnoreCase) ? gamePath : Path.Combine(gamePath, @"BIOGame\") : null;
        public static string tocFile => gamePath != null ? Path.Combine(gamePath, @"BIOGame\PCConsoleTOC.bin") : null;
        public static string cookedPath => gamePath != null ? Path.Combine(gamePath, @"BIOGame\CookedPCConsole\") : "Not Found";
        public static string DLCPath => gamePath != null ? Path.Combine(gamePath, @"BIOGame\DLC\") : "Not Found";

        public static string BinariesPath => gamePath != null ? Path.Combine(gamePath, @"Binaries\Win32\") : null;

        public static string ExecutablePath => gamePath != null ? Path.Combine(BinariesPath, @"MassEffect3.exe") : null;

        // "C:\...\MyDocuments\BioWare\Mass Effect 3\" folder
        public static string BioWareDocPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"\BioWare\Mass Effect 3\");
        public static string GamerSettingsIniFile => Path.Combine(BioWareDocPath, @"BIOGame\Config\GamerSettings.ini");

        static ME3Directory()
        {
            if (!string.IsNullOrEmpty(CoreLibSettings.Instance.ME3Directory))
            {
                gamePath = CoreLibSettings.Instance.ME3Directory;
            }
            else
            {
#if WINDOWS
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string subkey = @"BioWare\Mass Effect 3";

                string keyName = hkey32 + subkey;
                string test = (string)Registry.GetValue(keyName, "Install Dir", null);
                if (test != null)
                {
                    gamePath = test;
                    CoreLibSettings.Instance.ME2Directory = gamePath;
                    return;
                }

                keyName = hkey64 + subkey;
                gamePath = (string)Registry.GetValue(keyName, "Install Dir", null);
                if (gamePath != null)
                {
                    gamePath += Path.DirectorySeparatorChar;
                    CoreLibSettings.Instance.ME2Directory = gamePath;
                }
#endif
            }
        }

        public static CaseInsensitiveDictionary<string> OfficialDLCNames = new CaseInsensitiveDictionary<string>
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

        public static List<string> OfficialDLC = new List<string>
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
        };
    }
}
