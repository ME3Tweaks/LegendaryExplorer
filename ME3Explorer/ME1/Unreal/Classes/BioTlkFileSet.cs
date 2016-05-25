using System;
using System.Collections.Generic;
using System.IO;

namespace ME1Explorer.Unreal.Classes
{
    public class BioTlkFileSet : ITalkFile
    {
        public List<TalkFile> talkFiles;
        public PCCObject pcc;
        public int index;
        public int selectedTLK;
        public string Name { get { return index != -1 ? (pcc.Exports[index].ObjectName  + "."): null; } }

        public BioTlkFileSet(PCCObject _pcc)
        {
            pcc = _pcc;
            index = -1;
            for (int i = 0; i < pcc.ExportCount; i++)
            {
                if (pcc.Exports[i].ClassName == "BioTlkFileSet")
                {
                    index = i;
                }
            }
            if (index != -1)
            {
                loadData();
            }
            else
            {
                talkFiles = new List<TalkFile>();
            }
        }

        public BioTlkFileSet(PCCObject _pcc, int _index)
        {
            pcc = _pcc;
            index = _index;
            loadData();
        }

        public void loadData(int _index = -1)
        {
            if (_index != -1)
            {
                index = _index;
            }
            BinaryReader r = new BinaryReader(new MemoryStream(pcc.Exports[index].Data));

            //skip properties
            r.BaseStream.Seek(12, SeekOrigin.Begin);

            int count = r.ReadInt32();
            talkFiles = new List<TalkFile>(count);
            int langRef;
            for (int i = 0; i < count; i++)
            {
                langRef = r.ReadInt32();
                r.ReadInt64();
                talkFiles.Add(new TalkFile(pcc, r.ReadInt32() - 1, true, langRef, index));
                talkFiles.Add(new TalkFile(pcc, r.ReadInt32() - 1, false, langRef, index));
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

        public string findDataById(int strRefID, bool withFileName = false)
        {
            if (talkFiles != null)
            {
                return talkFiles[selectedTLK].findDataById(strRefID, withFileName);
            }
            else
            {
                return "No Data";
            }
        }
    }
}
