using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public static class ME3LoadedFiles
    {
        private const string SEARCH_PATTERN = "*.pcc";

        public static Dictionary<string, string> GetFilesLoadedInGame()
        {
            //make dictionary from basegame files
            Dictionary<string, string> loadedFiles = Directory.EnumerateFiles(ME3Directory.cookedPath, SEARCH_PATTERN).ToDictionary(Path.GetFileName);

            foreach (string dlcDirectory in GetEnabledDLC().OrderBy(GetMountPriority))
            {
                foreach (string filePath in Directory.EnumerateFiles(Path.Combine(dlcDirectory, "CookedPCConsole"), SEARCH_PATTERN))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName != null) loadedFiles[fileName] = filePath;
                }
            }
            return loadedFiles;
        }
        /// <summary>
        /// Gets the base DLC directory of each unpacked DLC/mod that will load in game (eg. C:\Program Files (x86)\Origin Games\Mass Effect 3\BIOGame\DLC\DLC_EXP_Pack001)
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetEnabledDLC() => Directory.EnumerateDirectories(ME3Directory.DLCPath).Where(IsEnabledDLC);

        private static string GetMountDLCFromDLCDir(string dlcDirectory) => Path.Combine(dlcDirectory, "CookedPCConsole", "Mount.dlc");

        private static bool IsEnabledDLC(string dir) => Path.GetFileName(dir).StartsWith("DLC_") && File.Exists(GetMountDLCFromDLCDir(dir));

        public static int GetMountPriority(string dlcDirectory) => MountFile.GetMountPriority(GetMountDLCFromDLCDir(dlcDirectory));

        public static string GetDLCNameFromDir(string dlcDirectory) => Path.GetFileName(dlcDirectory).Substring(4);
    }
}
