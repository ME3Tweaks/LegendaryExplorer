using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.ME1
{
    public static class ME1TalkFiles
    {
        public static List<ME1TalkFile> tlkList = new();
        public static Dictionary<ME1TalkFile, string> localtlkList = new();
        
        public static void LoadTlkData(string fileName, int uIndex)
        {
            if (File.Exists(fileName))
            {
                using IMEPackage pcc = MEPackageHandler.UnsafePartialLoad(fileName, exp => exp.UIndex == uIndex);
                tlkList.Add(new ME1TalkFile(pcc, uIndex));
            }
        }

        public static string findDataById(int strRefID, IMEPackage package, bool withFileName = false)
        {
            string s = "No Data";

            //Look in package local first
            if (package != null)
            {
                foreach (ME1TalkFile tlk in package.LocalTalkFiles)
                {
                    s = tlk.findDataById(strRefID, withFileName);
                    if (s != "No Data")
                    {
                        return s;
                    }
                }
            }

            //Look in loaded list
            foreach (ME1TalkFile tlk in tlkList)
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
