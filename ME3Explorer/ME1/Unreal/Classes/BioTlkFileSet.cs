using System;
using System.Collections.Generic;
using System.IO;
using ME3Explorer.Packages;

namespace ME1Explorer.Unreal.Classes
{
    public class BioTlkFileSet : ITalkFile
    {
        public List<TalkFile> talkFiles;
        public IMEPackage pcc;
        private int uIndex;
        public int selectedTLK;
        public string Name => uIndex != 0 ? (pcc.getObjectName(uIndex)): null;

        public BioTlkFileSet(IMEPackage _pcc)
        {
            pcc = _pcc;
            uIndex = 0;
            foreach (ExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "BioTlkFileSet")
                {
                    uIndex = exp.UIndex;
                }
            }
            if (uIndex != 0)
            {
                loadData(uIndex);
            }
            else
            {
                talkFiles = new List<TalkFile>();
            }
        }

        public void loadData(int _uIndex = 0)
        {
            if (_uIndex != 0)
            {
                uIndex = _uIndex;
            }
            BinaryReader r = new BinaryReader(new MemoryStream(pcc.GetUExport(uIndex).Data));

            //skip properties
            r.BaseStream.Seek(12, SeekOrigin.Begin);

            if (r.BaseStream.Length > 12)
            {
                int count = r.ReadInt32();
                talkFiles = new List<TalkFile>(count);
                for (int i = 0; i < count; i++)
                {
                    int langRef = r.ReadInt32();
                    r.ReadInt64();
                    talkFiles.Add(new TalkFile(pcc, r.ReadInt32(), true, langRef, uIndex));
                    talkFiles.Add(new TalkFile(pcc, r.ReadInt32(), false, langRef, uIndex));
                }
                for (int i = 0; i < talkFiles.Count; i++)
                {
                    if (talkFiles[i].language == "Int" && !talkFiles[i].male)
                    {
                        selectedTLK = i;
                        break;
                    }
                } 
            }
            else
            {
                talkFiles = new List<TalkFile>();
            }
        }

        public string findDataById(int strRefID, bool withFileName = false)
        {
            if (talkFiles != null)
            {
                if (talkFiles.Count > selectedTLK)
                {
                    return talkFiles[selectedTLK].findDataById(strRefID, withFileName);
                }
                return "No Data";
            }
            else
            {
                return "No Data";
            }
        }
    }
}
