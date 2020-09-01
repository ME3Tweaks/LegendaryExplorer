using System.IO;
using System.Collections.Generic;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.TLK
{
    public static class ME3TalkFiles
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
                s = tlk.findDataById(strRefID);
                if (s != "No Data")
                {
                    if (withFileName)
                    {
                        s += " (" + tlk.name + ")";
                    }
                    return s;
                }
            }
            return s;
        }

        internal static void ClearLoadedTlks()
        {
            tlkList.Clear();
        }
    }
}
