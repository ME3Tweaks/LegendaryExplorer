using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using UsefulThings;

namespace ME3Explorer
{
    public static class ME2Directory
    {
        private static string _gamePath = null;
        public static string gamePath
        {
            get => _gamePath;
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

        public static string BioGamePath => gamePath != null ? gamePath.Contains("biogame", StringComparison.OrdinalIgnoreCase) ? gamePath : Path.Combine(gamePath, @"BioGame\") : null;
        public static string cookedPath => gamePath != null ? Path.Combine(gamePath,  @"BioGame\CookedPC\") : "Not Found";
        public static string DLCPath => gamePath != null ? Path.Combine(gamePath, @"BioGame\DLC\") : "Not Found";

        // "C:\...\MyDocuments\BioWare\Mass Effect 2\" folder
        public static string BioWareDocPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"\BioWare\Mass Effect 2\");
        public static string GamerSettingsIniFile => BioWareDocPath + @"BIOGame\Config\GamerSettings.ini";

        public static string DLCFilePath(string DLCName)
        {
            string fullPath = DLCPath + DLCName + @"\CookedPC";
            if (File.Exists(fullPath))
                return fullPath;
            else
                throw new FileNotFoundException("Invalid DLC path " + fullPath);
        }

        static ME2Directory()
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ME2Directory))
            {
                gamePath = Properties.Settings.Default.ME2Directory;
            }
            else
            {
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string subkey = @"BioWare\Mass Effect 2";

                string keyName = hkey32 + subkey;
                string test = (string)Microsoft.Win32.Registry.GetValue(keyName, "Path", null);
                if (test != null)
                {
                    gamePath = test;
                    return;
                }

                /*if (gamePath != null)
                {
                    gamePath = gamePath + "\\";
                    return;
                }*/

                keyName = hkey64 + subkey;
                gamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Path", null);
                if (gamePath != null)
                {
                    gamePath = gamePath + "\\";
                    return;
                } 
            }
        }
    }
}
