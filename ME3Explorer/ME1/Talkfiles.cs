using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME1Explorer.Unreal.Classes;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;
using ME3Explorer;
using TalkFile = ME1Explorer.Unreal.Classes.TalkFile;
using Newtonsoft.Json;

namespace ME1Explorer
{
    public static class ME1TalkFiles
    {
        public static List<TalkFile> tlkList = new List<TalkFile>();
        public static readonly string LoadedTLKsPath = App.AppDataFolder + "ME1LoadedTLKs.JSON";
        private static bool loadedGlobalTlk = false;

        public static void LoadSavedTlkList()
        {
            if (File.Exists(LoadedTLKsPath))
            {
                List<(int, string)> files = JsonConvert.DeserializeObject<List<(int, string)>>(File.ReadAllText(LoadedTLKsPath));
                foreach ((int exportnum, string filename) in files)
                {
                    LoadTlkData(filename, exportnum);
                }
            }
            else
            {
                string path = ME1Directory.cookedPath + @"Packages\Dialog\GlobalTlk.upk";
                try
                {
                    ME1Package pcc = MEPackageHandler.OpenME1Package(path);
                    tlkList.Add(new TalkFile(pcc, 0));
                    loadedGlobalTlk = true;
                }
                catch (Exception)
                {
                    return;
                }
            }
        }

        public static void SaveTLKList()
        {
            File.WriteAllText(LoadedTLKsPath, JsonConvert.SerializeObject(tlkList.Select(x => (x.index, x.pcc.FileName))));
        }

        public static void LoadTlkData(string fileName, int index)
        {
            if (File.Exists(fileName))
            {
                ME1Package pcc = MEPackageHandler.OpenME1Package(fileName);
                TalkFile tlk = new TalkFile(pcc, index);
                tlk.LoadTlkData();
                tlkList.Add(tlk);
            }
        }

        public static string findDataById(int strRefID, bool withFileName = false)
        {
            string s = "No Data";
            foreach (TalkFile tlk in tlkList)
            {
                s = tlk.findDataById(strRefID, withFileName);
                if (s != "No Data")
                {
                    return s;
                }
            }
            return s;
        }
    }
}
