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
    public static class ME3Directory
    {
        private static string _gamePath = null;
        public static string gamePath
        {
            get
            {
                if (_gamePath == null)
                    return null;

                if (!_gamePath.EndsWith("\\"))
                    _gamePath += '\\';
                return _gamePath;
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
        public static string DLCPath => gamePath != null ? Path.Combine(gamePath , @"BIOGame\DLC\") : "Not Found";

        // "C:\...\MyDocuments\BioWare\Mass Effect 3\" folder
        public static string BioWareDocPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"\BioWare\Mass Effect 3\");
        public static string GamerSettingsIniFile => Path.Combine(BioWareDocPath, @"BIOGame\Config\GamerSettings.ini");

        public static string DLCFilePath(string DLCName)
        {
            string fullPath = Path.Combine(DLCPath, DLCName, @"\CookedPCConsole\Default.sfar");
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Invalid DLC path {fullPath}");

            return fullPath;
        }

        static ME3Directory()
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ME3Directory))
            {
                gamePath = Properties.Settings.Default.ME3Directory;
            }
            else
            {
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string subkey = @"BioWare\Mass Effect 3";

                string keyName = hkey32 + subkey;
                string test = (string)Microsoft.Win32.Registry.GetValue(keyName, "Install Dir", null);
                if (test != null)
                {
                    gamePath = test;
                    return;
                }

                /*if (gamePath != null)
                    return;*/

                keyName = hkey64 + subkey;
                gamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Install Dir", null); 
            }
        }
    }
}
