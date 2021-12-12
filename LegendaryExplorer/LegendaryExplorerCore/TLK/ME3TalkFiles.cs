using System.IO;
using System.Collections.Generic;
using LegendaryExplorerCore.TLK.ME2ME3;

namespace LegendaryExplorerCore.TLK
{
    public static class ME3TalkFiles
    {
        public static readonly List<ME2ME3LazyTLK> tlkList = new();

        public static void LoadTlkData(string fileName)
        {
            if (File.Exists(fileName))
            {
                var tlk = new ME2ME3LazyTLK();
                tlk.LoadTlkData(fileName);
                tlkList.Add(tlk);
            }
        }

        public static string findDataById(int strRefID, bool withFileName = false)
        {
            string s = "No Data";
            foreach (ME2ME3LazyTLK tlk in tlkList)
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
