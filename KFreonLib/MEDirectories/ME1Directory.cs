using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using UsefulThings;

namespace KFreonLib.MEDirectories
{
    public static class ME1Directory
    {
        private static List<String> files = null;
        public static List<string> Files { 
            get
            {
                if (files == null)
                {
                    Debugging.DebugOutput.PrintLn("ME1 COOKED: " + ME1Directory.cookedPath);
                    if (String.IsNullOrEmpty(ME1Directory.cookedPath) || !Directory.Exists(ME1Directory.cookedPath))
                        return null;

                    files = MEDirectories.EnumerateGameFiles(1, ME1Directory.cookedPath);

                    Debugging.DebugOutput.PrintLn("ME1 DLC: " + ME1Directory.DLCPath);
                    if (!String.IsNullOrEmpty(ME1Directory.DLCPath) && Directory.Exists(ME1Directory.DLCPath))
                        files.AddRange(MEDirectories.EnumerateGameFiles(1, ME1Directory.DLCPath));
                }
                return files;
            }
        }

        private static string _gamePath = null;
        public static string gamePath
        {
            get // if you are trying to use gamePath variable and it's null it asks to locate ME3 exe file
            {
                /*if (_gamePath == null)
                    _gamePath = KFreonLib.Misc.Methods.SelectGameLoc(1);*/
                return _gamePath;
            }
            private set { _gamePath = value; }
        }
        public static string GamePath(string path = null)
        {
            if (path != null)
            {
                if (path.Contains("BioGame"))
                    path = path.Substring(0, path.LastIndexOf("BioGame"));
                _gamePath = path;
            }

            return _gamePath;
        }
        public static string cookedPath { get { return (gamePath != null) ? Path.Combine(gamePath, gamePath.Contains("biogame", StringComparison.OrdinalIgnoreCase) ? @"CookedPC\" : @"BioGame\CookedPC\") : null; } }
        public static string DLCPath { get { return (gamePath != null) ? gamePath.Contains("biogame", StringComparison.OrdinalIgnoreCase) ? Path.Combine(Path.GetDirectoryName(gamePath), @"DLC\") : Path.Combine(gamePath, @"DLC\") : null; } }

        // "C:\...\MyDocuments\BioWare\Mass Effect\" folder
        public static string BioWareDocPath { get { return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\"; } }
        public static string GamerSettingsIniFile { get { return BioWareDocPath + @"BIOGame\Config\GamerSettings.ini"; } }

        public static string DLCFilePath(string DLCName)
        {
            string fullPath = DLCPath + DLCName + @"\CookedPC\";
            if (File.Exists(fullPath))
                return fullPath;
            else
                throw new FileNotFoundException("Invalid DLC path " + fullPath);
        }

        static ME1Directory()
        {
            string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
            string subkey = @"BioWare\Mass Effect";
            string keyName;

            keyName = hkey32 + subkey;
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
