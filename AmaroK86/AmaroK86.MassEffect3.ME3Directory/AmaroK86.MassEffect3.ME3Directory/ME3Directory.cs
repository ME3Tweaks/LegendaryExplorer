/*  Copyright (C) 2013 AmaroK86 (marcidm 'at' hotmail 'dot' com)
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.

 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.

 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using AmaroK86.Tools;

namespace AmaroK86.MassEffect3
{
    public static class ME3Paths
    {
        static INIHandler iniHnd;

        private static string _gamePath = null;
        public static string gamePath
        {
            get // if you are trying to use gamePath variable and it's null it asks to locate ME3 exe file
            {
                if (_gamePath == null)
                {
                    string gameExe = "MassEffect3.exe";
                    OpenFileDialog selectDir = new OpenFileDialog();
                    selectDir.FileName = gameExe;
                    selectDir.Filter = "ME3 exe file|" + gameExe;
                    selectDir.Title = "Select the Mass Effect 3 executable file";
                    if (selectDir.ShowDialog() == DialogResult.OK)
                        _gamePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(selectDir.FileName))) + @"\";
                }
                return _gamePath;
            }
            set { _gamePath = value; iniHnd.IniWriteValue("Paths", "gamePath", _gamePath); }
        }
        public static string tocFile { get { return (gamePath != null) ? gamePath + @"BIOGame\PCConsoleTOC.bin" : null; } }
        public static string cookedPath { get { return (gamePath != null) ? gamePath + @"BIOGame\CookedPCConsole\" : null; } }
        public static string DLCPath { get { return (gamePath != null) ? gamePath + @"BIOGame\DLC\" : null; } }

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

        static ME3Paths()
        { 
            iniHnd = new INIHandler();

            string iniGamePath = iniHnd.IniReadValue("Paths", "gamePath");

            //MessageBox.Show("ini file: " + Path.GetFullPath(iniHnd.iniFileName) + "\n" + iniGamePath);

            if (iniGamePath == "" || iniGamePath == null)
            {
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string subkey = @"BioWare\Mass Effect 3";
                string keyName;

                keyName = hkey32 + subkey;
                gamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Install Dir", null);
                if (gamePath != null)
                    return;

                keyName = hkey64 + subkey;
                gamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Install Dir", null);
                if (gamePath != null)
                    return;
            }
            else
                gamePath = iniGamePath;
        }
    }
}
