using ME3ExplorerCore.ME1;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.TLK;
using ME3ExplorerCore.TLK.ME1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ME3Explorer.Unreal
{
    public static class TLKLoader
    {
        public static readonly string LoadedTLKsPathME1 = App.AppDataFolder + "ME1LoadedTLKs.JSON";
        public static readonly string LoadedTLKsPathME2 = App.AppDataFolder + "ME2LoadedTLKs.JSON";
        public static readonly string LoadedTLKsPathME3 = App.AppDataFolder + "ME3LoadedTLKs.JSON";

        public static void LoadSavedTlkList()
        {
            if (File.Exists(LoadedTLKsPathME1))
            {
                List<(int, string)> files = JsonConvert.DeserializeObject<List<(int, string)>>(File.ReadAllText(LoadedTLKsPathME1));
                foreach ((int exportnum, string filename) in files)
                {
                    ME1TalkFiles.LoadTlkData(filename, exportnum);
                }
            }
            else
            {
                string path = ME1Directory.cookedPath + @"Packages\Dialog\GlobalTlk.upk";
                try
                {
                    IMEPackage pcc = MEPackageHandler.OpenME1Package(path);
                    ME1TalkFiles.tlkList.Add(new ME1TalkFile(pcc, 1));
                }
                catch (Exception)
                {
                    return;
                }
            }


            if (File.Exists(LoadedTLKsPathME2))
            {
                List<string> files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(LoadedTLKsPathME3));
                foreach (string filePath in files)
                {
                    ME3TalkFiles.LoadTlkData(filePath);
                }
            }
            else
            {
                string tlkPath = ME2Directory.cookedPath + "BIOGame_INT.tlk";
                ME2TalkFiles.LoadTlkData(tlkPath);
            }

            if (File.Exists(LoadedTLKsPathME3))
            {
                List<string> files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(LoadedTLKsPathME3));
                foreach (string filePath in files)
                {
                    ME3TalkFiles.LoadTlkData(filePath);
                }
            }
            else
            {
                string tlkPath = ME3Directory.cookedPath + "BIOGame_INT.tlk";
                ME3TalkFiles.LoadTlkData(tlkPath);
            }
        }

        public static void SaveTLKList()
        {
            File.WriteAllText(LoadedTLKsPathME1, JsonConvert.SerializeObject(ME1TalkFiles.tlkList.Select(x => (x.uindex, x.pcc.FilePath))));
            File.WriteAllText(LoadedTLKsPathME2, JsonConvert.SerializeObject(ME2TalkFiles.tlkList.Select(x => x.path)));
            File.WriteAllText(LoadedTLKsPathME3, JsonConvert.SerializeObject(ME3TalkFiles.tlkList.Select(x => x.path)));
        }
    }
}
