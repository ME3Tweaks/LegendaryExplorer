using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.TLK
{
    public static class ME2TalkFiles
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

        public static void ClearLoadedTlks()
        {
            tlkList.Clear();
        }
    }
}
