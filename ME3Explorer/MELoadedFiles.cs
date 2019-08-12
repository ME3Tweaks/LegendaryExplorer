using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ini;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public static class MELoadedFiles
    {
        private static readonly string[] ME1FilePatterns = { "*.u", "*.upk", "*.sfm"};
        private const string ME2and3FilePattern = "*.pcc";
        /// <summary>
        /// Gets a Dictionary of all loaded files in the given game. Key is the filename, value is file path
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetFilesLoadedInGame(MEGame game)
        {
            //make dictionary from basegame files
            var loadedFiles = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (string directory in GetEnabledDLC(game).OrderBy(dir => GetMountPriority(dir, game)).Prepend(MEDirectories.BioGamePath(game)))
            {
                foreach (string filePath in GetCookedFiles(game, directory))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName != null) loadedFiles[fileName] = filePath;
                }
            }
            return loadedFiles;
        }

        public static IEnumerable<string> GetAllFiles(MEGame game) => GetEnabledDLC(game).Prepend(MEDirectories.BioGamePath(game)).SelectMany(directory => GetCookedFiles(game, directory));

        private static IEnumerable<string> GetCookedFiles(MEGame game, string directory)
        {
            if (game == MEGame.ME1)
                return ME1FilePatterns.SelectMany(pattern => Directory.EnumerateFiles(Path.Combine(directory, "CookedPC"), pattern, SearchOption.AllDirectories));
            return Directory.EnumerateFiles(Path.Combine(directory, game == MEGame.ME3 ? "CookedPCConsole" : "CookedPC"), ME2and3FilePattern);
        }

        /// <summary>
        /// Gets the base DLC directory of each unpacked DLC/mod that will load in game (eg. C:\Program Files (x86)\Origin Games\Mass Effect 3\BIOGame\DLC\DLC_EXP_Pack001)
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetEnabledDLC(MEGame game) => Directory.EnumerateDirectories(MEDirectories.DLCPath(game)).Where(dir => IsEnabledDLC(dir, game));

        private static string GetMountDLCFromDLCDir(string dlcDirectory, MEGame game) => Path.Combine(dlcDirectory, game == MEGame.ME3 ? "CookedPCConsole" : "CookedPC", "Mount.dlc");

        private static bool IsEnabledDLC(string dir, MEGame game)
        {
            string dlcName = Path.GetFileName(dir);
            if (game == MEGame.ME1)
            {
                return ME1Directory.OfficialDLC.Contains(dlcName) || File.Exists(Path.Combine(dir, "AutoLoad.ini"));
            }
            return dlcName.StartsWith("DLC_") && File.Exists(GetMountDLCFromDLCDir(dir, game));
        }

        public static int GetMountPriority(string dlcDirectory, MEGame game)
        {
            if (game == MEGame.ME1)
            {
                int idx = 1 + ME1Directory.OfficialDLC.IndexOf(Path.GetFileName(dlcDirectory));
                if (idx > 0)
                {
                    return idx;
                }
                //is mod
                string autoLoadPath = Path.Combine(dlcDirectory, "AutoLoad.ini");
                IniFile dlcAutoload = new IniFile(autoLoadPath);
                return Convert.ToInt32(dlcAutoload.ReadValue("ME1DLCMOUNT", "ModMount"));
            }
            return MountFile.GetMountPriority(GetMountDLCFromDLCDir(dlcDirectory, game));
        }

        public static string GetDLCNameFromDir(string dlcDirectory) => Path.GetFileName(dlcDirectory).Substring(4);
    }
}
