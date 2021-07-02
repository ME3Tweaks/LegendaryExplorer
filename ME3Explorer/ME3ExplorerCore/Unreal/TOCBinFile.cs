using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Gammtek.IO;

namespace ME3ExplorerCore.Unreal
{
    // Todo: Convert to EndianReader
    public class TOCBinFile
    {
        public static int TOCMagicNumber = 0x3AB70C13;

        MemoryStream Memory;
        
        [DebuggerDisplay("TOCEntry 0x{offset.ToString(\"8\")}, {name}, file size {size}")]
        public struct Entry
        {
            public int offset;
            public string name;
            public int size;
        }

        public List<Entry> Entries;

        public TOCBinFile(MemoryStream m)
        {
            Memory = m;
            ReadFile();
        }

        public TOCBinFile(string path)
        {
            Memory = new MemoryStream(File.ReadAllBytes(path).ToArray());
            ReadFile();
        }

        public void ReadFile()
        {
            EndianReader reader = new EndianReader(Memory);
            uint magic = (uint)reader.ReadInt32();
            if (magic != TOCMagicNumber)
            {
                throw new Exception("Not a TOC file, bad magic header");
            }

            reader.Skip(12);
            int count = reader.ReadInt32();
            Entries = new List<Entry>(count);

            for(int i = 0; i < count; i++)
            {
                Entry e = new() {
                    offset = (int)reader.Position
                };

                var entrySize = reader.ReadUInt16();    //Size of entry - last entry is size 0
                reader.ReadUInt16();                    //Flag
                e.size = reader.ReadInt32();
                reader.Skip(20);
                e.name = reader.ReadStringASCIINull();

                Entries.Add(e);
            }
        }


        public void UpdateEntry(int Index, int size)
        {
            if (Entries == null || Index < 0 || Index >= Entries.Count)
                return;
            Entry e = Entries[Index];
            e.size = size;
            Entries[Index] = e;
        }

        public MemoryStream Save()
        {

            foreach (Entry e in Entries)
            {
                Memory.Seek(e.offset + 4, 0);
                Memory.Write(BitConverter.GetBytes(e.size), 0, 4);
            }
            return Memory;
        }
    }
}
