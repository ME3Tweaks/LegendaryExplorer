using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.TLK.ME1;
using Newtonsoft.Json;

namespace ME3ExplorerCore.ME1
{
    public static class ME1TalkFiles
    {
        public static List<ME1TalkFile> tlkList = new List<ME1TalkFile>();
        public static Dictionary<ME1TalkFile, string> localtlkList = new Dictionary<ME1TalkFile, string>();
        
        public static void LoadTlkData(string fileName, int index)
        {
            if (File.Exists(fileName))
            {
                IMEPackage pcc = MEPackageHandler.OpenME1Package(fileName, forceLoadFromDisk: true); //do not cache this in the packages list.
                ME1TalkFile tlk = new ME1TalkFile(pcc, index);
                tlk.LoadTlkData();
                tlkList.Add(tlk);
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
            foreach (var talkFile in tlkList)
            {
                talkFile.pcc?.Release();
            }

            tlkList.Clear();
        }
    }
}
