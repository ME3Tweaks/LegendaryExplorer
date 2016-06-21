using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME1Explorer.Unreal.Classes;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;

namespace ME1Explorer
{
    public class TalkFiles : ITalkFile
    {
        public List<TalkFile> tlkList;

        public TalkFiles()
        {
            tlkList = new List<TalkFile>();
        }

        public void LoadGlobalTlk()
        {
            string path = ME1Directory.cookedPath + @"Packages\Dialog\GlobalTlk.upk";
            try
            {
                ME1Package pcc = new ME1Package(path);
                tlkList.Add(new TalkFile(pcc, 0));
            }
            catch (Exception)
            {
                return;
            }
        }

        public string findDataById(int strRefID, bool withFileName = false)
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
