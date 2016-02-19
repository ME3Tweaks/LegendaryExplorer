using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public class TalkFiles
    {
        public List<TalkFile> tlkList;

        public TalkFiles()
        {
            tlkList = new List<TalkFile>();
        }

        public void LoadTlkData(string fileName)
        {
            if (File.Exists(fileName))
            {
                TalkFile tlk = new TalkFile();
                tlk.LoadTlkData(fileName);
                tlkList.Add(tlk);
            }
        }
        public String findDataById(int strRefID, bool withFileName = false)
        {
            String s = "No Data";
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
    }
}
