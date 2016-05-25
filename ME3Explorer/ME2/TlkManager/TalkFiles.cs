using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KFreonLib.MEDirectories;
using Newtonsoft.Json;

namespace ME2Explorer
{
    public static class TalkFiles
    {
        public static List<TalkFile> tlkList = new List<TalkFile>();

        public static void LoadTlkData(string fileName)
        {
            if (File.Exists(fileName))
            {
                TalkFile tlk = new TalkFile();
                tlk.LoadTlkData(fileName);
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

        public static void LoadSavedTlkList()
        {
            string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\ME2LoadedTLKs.JSON";
            if (File.Exists(path))
            {
                List<string> files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path));
                foreach (string filePath in files)
                {
                    LoadTlkData(filePath);
                }
            }
            else
            {
                string tlkPath = ME2Directory.cookedPath + "BIOGame_INT.tlk";
                LoadTlkData(tlkPath);
            }
        }

        private static void SaveTLKList()
        {
            string path = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\ME2LoadedTLKs.JSON";
            File.WriteAllText(path, JsonConvert.SerializeObject(tlkList.Select(x => x.path)));
        }

        public static void addTLK(string fileName)
        {
            LoadTlkData(fileName);
            SaveTLKList();
        }

        public static void removeTLK(int index)
        {
            tlkList.RemoveAt(index);
            SaveTLKList();
        }

        public static void moveTLKUp(int index)
        {
            TalkFile tlk = tlkList[index];
            tlkList.RemoveAt(index);
            tlkList.Insert(index - 1, tlk);
            SaveTLKList();
        }

        public static void moveTLKDown(int index)
        {
            TalkFile tlk = tlkList[index];
            tlkList.RemoveAt(index);
            tlkList.Insert(index + 1, tlk);
            SaveTLKList();
        }
    }
}
