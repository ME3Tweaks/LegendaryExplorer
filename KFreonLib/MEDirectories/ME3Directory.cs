using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace KFreonLib.MEDirectories
{
    public static class ME3Directory
    {
        private static string _gamePath = null;
        public static string gamePath
        {
            get // if you are trying to use gamePath variable and it's null it asks to locate ME3 exe file
            {
                /*if (_gamePath == null)
                    _gamePath = KFreonLib.Misc.Methods.SelectGameLoc(3);
                else */
                if (_gamePath == null)
                    return null;

                if (!_gamePath.EndsWith("\\"))
                    _gamePath += '\\';
                return _gamePath;
            }
            private set { _gamePath = value; }
        }
        public static string GamePath(string path = null)
        {
            if (path != null)
                _gamePath = path;

            return _gamePath;
        }
        public static string tocFile { get { return (gamePath != null) ? Path.Combine(gamePath, @"BIOGame\PCConsoleTOC.bin") : null; } }
        public static string cookedPath { get { return (gamePath != null) ? Path.Combine(gamePath, @"BIOGame\CookedPCConsole\") : null; } }
        public static string DLCPath { get { return (gamePath != null) ? Path.Combine(gamePath , @"BIOGame\DLC\") : null; } }

        // "C:\...\MyDocuments\BioWare\Mass Effect 3\" folder
        public static string BioWareDocPath { get { return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect 3\"; } }
        public static string GamerSettingsIniFile { get { return BioWareDocPath + @"BIOGame\Config\GamerSettings.ini"; } }

        public static string DLCFilePath(string DLCName)
        {
            string fullPath = DLCPath + DLCName + @"\CookedPCConsole\Default.sfar";
            if (File.Exists(fullPath))
                return fullPath;
            else
                throw new FileNotFoundException("Invalid DLC path " + fullPath);
        }

        static ME3Directory()
        {
            string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
            string subkey = @"BioWare\Mass Effect 3";
            string keyName;

            keyName = hkey32 + subkey;
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

        static List<string> _files = null;
        public static List<string> Files
        {
            get
            {
                if (_files == null)
                {
                    Debugging.DebugOutput.PrintLn("ME3 COOKED: " + ME3Directory.cookedPath);
                    if (String.IsNullOrEmpty(ME3Directory.cookedPath))
                        return null;

                    _files = MEDirectories.EnumerateGameFiles(3, ME3Directory.cookedPath);

                    Debugging.DebugOutput.PrintLn("ME3 DLC: " + ME3Directory.DLCPath);
                    if (String.IsNullOrEmpty(ME3Directory.DLCPath))
                    {
                        _files.AddRange(MEDirectories.EnumerateGameFiles(3, ME3Directory.DLCPath));
                    }
                }

                return _files;
            }
        }
    }
}
