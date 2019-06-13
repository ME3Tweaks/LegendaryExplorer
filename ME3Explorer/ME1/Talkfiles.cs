using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME1Explorer.Unreal.Classes;
using ME3Explorer.Packages;
using ME3Explorer;
using TalkFile = ME1Explorer.Unreal.Classes.TalkFile;
using Newtonsoft.Json;

namespace ME1Explorer
{
    public static class ME1TalkFiles
    {
        public static List<TalkFile> tlkList = new List<TalkFile>();
        public static Dictionary<TalkFile, string> localtlkList = new Dictionary<TalkFile, string>();
        public static readonly string LoadedTLKsPath = App.AppDataFolder + "ME1LoadedTLKs.JSON";
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
                    tlkList.Add(new TalkFile(pcc, 1));
                }
                catch (Exception)
                {
                    return;
                }
            }
        }

        public static void SaveTLKList()
        {
            File.WriteAllText(LoadedTLKsPath, JsonConvert.SerializeObject(tlkList.Select(x => (x.uindex, x.pcc.FileName))));
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

        /// <summary>
        /// Loads local Tlk data for ME1 strings.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="language">if null then English/INT, else ES, DE, FR, IT. Use M_DE for Male TlkSet. </param>
        public static void LoadLocalTlkData(IMEPackage Pcc, string language = null)
        {
            if(Pcc.Game == MEGame.ME1)
            {
                string slang = null;
                if (language != null)
                {
                    slang = $"tlk_{language}".ToLower();
                }
                else
                {
                    slang = "tlk";
                }

                foreach (var exp in Pcc.Exports.Where(exp => exp.ClassName.Equals("BioTlkFile") && exp.ObjectName.ToLower().Equals(slang)))
                {
                    TalkFile tlk = new TalkFile(Pcc as ME1Package, exp.UIndex);
                    if (!localtlkList.ContainsKey(tlk))
                    {
                        tlk.LoadTlkData();
                        localtlkList.Add(tlk, Pcc.FileName);
                    }
                }
            }
        }

        public static void UnLoadLocalTlkData(IMEPackage Pcc)
        {
            var toRemove = localtlkList.Where(t => t.Value.Contains(Pcc.FileName))
                         .Select(pair => pair.Key)
                         .ToList();

            foreach (var key in toRemove)
            {
                localtlkList.Remove(key);
            }
        }

        public static string findDataById(int strRefID, bool withFileName = false)
        {
            string s = "No Data";
            foreach (var tlk in localtlkList)
            {
                s = tlk.Key.findDataById(strRefID, withFileName);
                if (s != "No Data")
                {
                    return s;
                }
            }

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
