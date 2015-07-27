using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace ME3LibWV
{
    public class PCCPackage
    {
        public struct MetaInfo
        {
            public bool inDLC;
            public bool loaded;
            public bool loadfull;
            public bool compressed;
            public string filepath;
            public string inDLCPath;
            public int inDLCIndex;
            public DLCPackage dlc;
        }        
        public struct HeaderInfo
        {
            public uint magic;
            public ushort ver1;
            public ushort ver2;
            public uint HeaderLength;
            public string Group;
            public uint _offsetFlag;
            public uint Flags;
            public uint unk1;
            public uint NameCount;
            public uint NameOffset;
            public uint ExportCount;
            public uint ExportOffset;
            public uint ImportCount;
            public uint ImportOffset;
            public uint FreeZoneStart;
            public uint FreeZoneEnd;
            public uint unk2;
            public uint unk3;
            public uint unk4;
            public byte[] GUID;
            public List<Generation> Generations;
            public uint EngineVersion;
            public uint CookerVersion;
            public uint unk5;
            public uint unk6;
            public uint CompressionFlag;
            public uint _offsetCompFlagEnd;
            public List<CompressedChunk> Chunks;
            public uint unk7;
            public uint unk8;
            public MemoryStream DeCompBuffer;
        }
        public struct Generation
        {
            public uint ExportCount;
            public uint ImportCount;
            public uint NetObjCount;
        }
        public struct CompressedChunkBlock
        {
            public uint CompSize;
            public uint UnCompSize;
            public bool loaded;
        }
        public struct CompressedChunk
        {
            public uint UnCompOffset;
            public uint UnCompSize;
            public uint CompOffset;
            public uint CompSize;
            public uint Magic;
            public uint BlockSize;
            public List<CompressedChunkBlock> Blocks;
        }
        public struct ImportEntry
        {
            public int idxPackage;  //0x00 0
            public int Unk1;        //0x04 4
            public int idxClass;    //0x08 8
            public int Unk2;        //0x0C 12
            public int idxLink;     //0x10 16
            public int idxName;     //0x14 20
            public int Unk3;        //0x18 24
        }
        public struct ExportEntry
        {
            public int idxClass;    //0x00 0
            public int idxParent;   //0x04 4
            public int idxLink;     //0x08 8
            public int idxName;     //0x0C 12
            public int Index;       //0x10 16
            public int idxArchetype;//0x14 20
            public int Unk1;        //0x18 24
            public int ObjectFlags; //0x1C 28
            public int Datasize;    //0x20 32
            public int Dataoffset;  //0x24 36
            public int Unk2;        //0x28 40
            public int[] Unk3;      //0x2C 44
            public int Unk4;
            public int Unk5;
            public int Unk6;
            public int Unk7;
            public int Unk8;
            public byte[] Data;
            public bool DataLoaded;
            public uint _infooffset;
        }

        public MetaInfo GeneralInfo;
        public HeaderInfo Header;
        public List<string> Names;
        public List<ImportEntry> Imports;
        public List<ExportEntry> Exports;
        public Stream Source;
        public bool verbose = false;

        public PCCPackage()
        {
            GeneralInfo = new MetaInfo();
            GeneralInfo.loaded = false;
        }
        public PCCPackage(DLCPackage dlc, int Index, bool loadfull = true, bool verbosemode = false)
        {
            try
            {
                verbose = verbosemode;
                MemoryStream m = dlc.DecompressEntry(Index);                
                GeneralInfo = new MetaInfo();
                GeneralInfo.inDLC = true;
                GeneralInfo.loadfull = loadfull;                
                GeneralInfo.filepath = dlc.MyFileName;
                GeneralInfo.inDLCPath = dlc.Files[Index].FileName;
                GeneralInfo.dlc = dlc;
                GeneralInfo.inDLCIndex = Index;
                Load(m);
                GeneralInfo.loaded = true;
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::PCCPACKAGE ERROR:\n" + ex.Message);
            }
        }
        public PCCPackage(string pccpath, bool loadfull = true, bool verbosemode = false, bool closestream = false)
        {
            try
            {
                verbose = verbosemode;
                FileStream fs = new FileStream(pccpath, FileMode.Open, FileAccess.ReadWrite);
                GeneralInfo = new MetaInfo();
                GeneralInfo.loadfull = loadfull;
                GeneralInfo.inDLC = false;
                GeneralInfo.filepath = pccpath;
                Load(fs);
                GeneralInfo.loaded = true;
                if(closestream)
                    fs.Close();
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::PCCPACKAGE ERROR:\n" + ex.Message);
            }
        }
        public bool isName(int index)
        {
            return index >= 0 && index < Header.NameCount;
        }
        public string GetName(int index)
        {
            string s = "";
            if (isName(index))
                s = Names[index];
            return s;
        }
        public string GetObject(int uindex)
        {
            if (uindex == 0)
                return "Class";
            if (uindex > 0)
            {
                return GetName(Exports[uindex - 1].idxName);
            }
            else
            {
                return GetName(Imports[-uindex - 1].idxName);
            }
        }
        public string GetObjectPath(int uindex)
        {
            string s = "";
            if (uindex == 0)
                return s;
            if (uindex > 0)
                uindex = Exports[uindex - 1].idxLink;
            else
                uindex = Imports[-uindex - 1].idxLink;
            while (uindex != 0)
            {
                s = GetObject(uindex) + "." + s;
                if (uindex > 0)
                    uindex = Exports[uindex - 1].idxLink;
                else
                    uindex = Imports[-uindex - 1].idxLink;
            }
            return s;
        }
        public byte[] GetObjectData(int index)
        {
            if (index >= 0 && index < Header.ExportCount)
            {
                ExportEntry e = Exports[index];
                if (e.DataLoaded)
                    return e.Data;
                else
                {
                    if (GeneralInfo.compressed)
                    {
                        UncompressRange((uint)e.Dataoffset, (uint)e.Datasize);
                        e.Data = new byte[e.Datasize];
                        Header.DeCompBuffer.Seek(e.Dataoffset, 0);
                        Header.DeCompBuffer.Read(e.Data, 0, e.Datasize);
                    }
                    else
                    {
                        e.Data = new byte[e.Datasize];
                        Source.Seek(e.Dataoffset, 0);
                        Source.Read(e.Data, 0, e.Datasize);
                    }
                    e.DataLoaded = true;
                    Exports[index] = e;
                    return e.Data;
                }
            }
            else
                return new byte[0];
        }
        public byte[] GetObjectData(int offset, int size)
        {
            byte[] res = new byte[size];
            if (GeneralInfo.compressed)
            {
                UncompressRange((uint)offset, (uint)size);
                Header.DeCompBuffer.Seek(offset, 0);
                Header.DeCompBuffer.Read(res, 0, size);
            }
            else
            {
                Source.Seek(offset, 0);
                Source.Read(res, 0, size);
            }
            return res;
        }
        public string GetObjectClass(int uindex)
        {
            if (uindex == 0)
                return "Class";
            if (uindex > 0)
            {
                return GetObject(Exports[uindex - 1].idxClass);
            }
            else
            {
                return GetObject(Imports[-uindex - 1].idxClass);
            }
        }
        public int[] GetLinkList(int uindex)
        {
            List<int> res = new List<int>();
            while (uindex != 0)
            {
                res.Add(uindex);
                if (uindex > 0)
                    uindex = Exports[uindex - 1].idxLink;
                else
                    uindex = Imports[-uindex - 1].idxLink;
            }
            res.Reverse();
            return res.ToArray();
        }
        public int GetBiggestIndex()
        {
            int res = 0;
            foreach (ExportEntry e in Exports)
                if (e.Index > res)
                    res = e.Index;
            return res;
        }
        public void Save()
        {
            try
            {
                DebugLog.PrintLn("Writing Header...", true);
                MemoryStream m = new MemoryStream();
                m.Write(BitConverter.GetBytes(Header.magic), 0, 4);
                m.Write(BitConverter.GetBytes(Header.ver1), 0, 2);
                m.Write(BitConverter.GetBytes(Header.ver2), 0, 2);
                m.Write(BitConverter.GetBytes(Header.HeaderLength), 0, 4);
                WriteUString(Header.Group, m);
                if (GeneralInfo.compressed)
                    m.Write(BitConverter.GetBytes(Header.Flags ^ 0x02000000), 0, 4);
                else
                    m.Write(BitConverter.GetBytes(Header.Flags), 0, 4);
                m.Write(BitConverter.GetBytes(Header.unk1), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(Header.unk2), 0, 4);
                m.Write(BitConverter.GetBytes(Header.unk3), 0, 4);
                m.Write(BitConverter.GetBytes(Header.unk4), 0, 4);
                m.Write(Header.GUID, 0, 16);
                m.Write(BitConverter.GetBytes(Header.Generations.Count), 0, 4);
                foreach (Generation g in Header.Generations)
                {
                    m.Write(BitConverter.GetBytes(g.ExportCount), 0, 4);
                    m.Write(BitConverter.GetBytes(g.ImportCount), 0, 4);
                    m.Write(BitConverter.GetBytes(g.NetObjCount), 0, 4);
                }
                m.Write(BitConverter.GetBytes(Header.EngineVersion), 0, 4);
                m.Write(BitConverter.GetBytes(Header.CookerVersion), 0, 4);
                m.Write(BitConverter.GetBytes(Header.unk5), 0, 4);
                m.Write(BitConverter.GetBytes(Header.unk6), 0, 4);
                m.Write(BitConverter.GetBytes(Header.CompressionFlag), 0, 4);
                m.Write(BitConverter.GetBytes(0), 0, 4);
                m.Write(BitConverter.GetBytes(Header.unk7), 0, 4);
                m.Write(BitConverter.GetBytes(Header.unk8), 0, 4);
                DebugLog.PrintLn("Writing Name Table...", true);
                Header.NameOffset = (uint)m.Position;
                Header.NameCount = (uint)Names.Count;
                foreach (string s in Names)
                    WriteUString(s, m);
                DebugLog.PrintLn("Writing Import Table...", true);
                Header.ImportOffset = (uint)m.Position;
                Header.ImportCount = (uint)Imports.Count;
                foreach (ImportEntry e in Imports)
                {
                    m.Write(BitConverter.GetBytes(e.idxPackage), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk1), 0, 4);
                    m.Write(BitConverter.GetBytes(e.idxClass), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk2), 0, 4);
                    m.Write(BitConverter.GetBytes(e.idxLink), 0, 4);
                    m.Write(BitConverter.GetBytes(e.idxName), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk3), 0, 4);
                }
                DebugLog.PrintLn("Writing Export Table...", true);
                Header.ExportOffset = (uint)m.Position;
                Header.ExportCount = (uint)Exports.Count;
                for (int i = 0; i < Exports.Count; i++)
                {
                    ExportEntry e = Exports[i];
                    e._infooffset = (uint)m.Position;
                    Exports[i] = e;
                    m.Write(BitConverter.GetBytes(e.idxClass), 0, 4);
                    m.Write(BitConverter.GetBytes(e.idxParent), 0, 4);
                    m.Write(BitConverter.GetBytes(e.idxLink), 0, 4);
                    m.Write(BitConverter.GetBytes(e.idxName), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Index), 0, 4);
                    m.Write(BitConverter.GetBytes(e.idxArchetype), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk1), 0, 4);
                    m.Write(BitConverter.GetBytes(e.ObjectFlags), 0, 4);
                    m.Write(BitConverter.GetBytes(0), 0, 4);
                    m.Write(BitConverter.GetBytes(0), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk2), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk3.Length), 0, 4);
                    foreach (int j in e.Unk3)
                        m.Write(BitConverter.GetBytes(j), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk4), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk5), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk6), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk7), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Unk8), 0, 4);
                }
                DebugLog.PrintLn("Writing Free Zone...", true);
                int FreeZoneSize = (int)Header.FreeZoneEnd - (int)Header.FreeZoneStart;
                Header.FreeZoneStart = (uint)m.Position;
                m.Write(new byte[FreeZoneSize], 0, FreeZoneSize);
                Header.FreeZoneEnd = Header.HeaderLength = (uint)m.Position;
                DebugLog.PrintLn("Writing Export Data...", true);
                for (int i = 0; i < Exports.Count; i++)
                {
                    ExportEntry e = Exports[i];
                    byte[] buff = GetObjectData(i);
                    e.Dataoffset = (int)m.Position;
                    e.Datasize = buff.Length;
                    m.Write(buff, 0, buff.Length);
                    long pos = m.Position;
                    m.Seek(e._infooffset + 32, 0);
                    m.Write(BitConverter.GetBytes(e.Datasize), 0, 4);
                    m.Write(BitConverter.GetBytes(e.Dataoffset), 0, 4);
                    m.Seek(pos, 0);
                }
                DebugLog.PrintLn("Updating Header...", true);
                m.Seek(8, 0);
                m.Write(BitConverter.GetBytes(Header.HeaderLength), 0, 4);
                m.Seek(24 + (Header.Group.Length + 1) * 2, 0);
                m.Write(BitConverter.GetBytes(Header.NameCount), 0, 4);
                m.Write(BitConverter.GetBytes(Header.NameOffset), 0, 4);
                m.Write(BitConverter.GetBytes(Header.ExportCount), 0, 4);
                m.Write(BitConverter.GetBytes(Header.ExportOffset), 0, 4);
                m.Write(BitConverter.GetBytes(Header.ImportCount), 0, 4);
                m.Write(BitConverter.GetBytes(Header.ImportOffset), 0, 4);
                m.Write(BitConverter.GetBytes(Header.FreeZoneStart), 0, 4);
                m.Write(BitConverter.GetBytes(Header.FreeZoneEnd), 0, 4);
                DebugLog.PrintLn("Done generating.", true);
                if (GeneralInfo.inDLC)
                {
                }
                else
                {
                    if (Source != null)
                        Source.Close();                    
                    File.WriteAllBytes(GeneralInfo.filepath, m.ToArray());
                }
                DebugLog.PrintLn("Done.", true);
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::SAVE ERROR:\n" + ex.Message);
            }
        }
        public void CloneEntry(int uIndex)
        {
            if (uIndex > 0)
            {
                ExportEntry e = Exports[uIndex - 1];
                ExportEntry n = new ExportEntry();
                n.Data = CopyArray(GetObjectData(uIndex - 1));
                n.DataLoaded = true;
                n.Datasize = n.Data.Length;
                n.idxClass = e.idxClass;
                n.idxParent = e.idxParent;
                n.idxLink = e.idxLink;
                n.idxName = e.idxName;
                n.Index = GetBiggestIndex() + 1;
                n.idxArchetype = e.idxArchetype;
                n.Unk1 = e.Unk1;
                n.ObjectFlags = e.ObjectFlags;
                n.Unk2 = e.Unk2;
                n.Unk3 = new int[e.Unk3.Length];
                for (int i = 0; i < e.Unk3.Length; i++)
                    n.Unk3[i] = e.Unk3[i];
                n.Unk2 = e.Unk4;
                n.Unk2 = e.Unk5;
                n.Unk2 = e.Unk6;
                n.Unk2 = e.Unk7;
                n.Unk2 = e.Unk8;
                Exports.Add(n);
                Header.ExportCount++;
            }
            else
            {
                ImportEntry e = Imports[-uIndex - 1];
                ImportEntry n = new ImportEntry();
                n.idxPackage = e.idxPackage;
                n.Unk1 = e.Unk1;
                n.idxClass = e.idxClass;
                n.Unk2 = e.Unk2;
                n.idxLink = e.idxLink;
                n.idxName = e.idxName;
                n.Unk3 = e.Unk3;
                Imports.Add(n);
                Header.ImportCount++;
            }
        }

        public int FindNameOrAdd(string name)
        {
            for (int i = 0; i < Names.Count; i++)
                if (Names[i] == name)
                    return i;
            Names.Add(name);
            return (int)Header.NameCount++;
        }

        public int FindClass(string name)
        {
            for (int i = 0; i < Imports.Count; i++)
                if (GetName(Imports[i].idxName) == name)
                    return (-i - 1);
            for (int i = 0; i < Exports.Count; i++)
                if (GetName(Exports[i].idxName) == name)
                    return (i + 1);
            return 0;

        }

        private void Load(Stream s)
        {
            try
            {
                Source = s;
                ReadHeader(s);
                ReadNameTable();
                ReadImportTable();
                ReadExportTable();
                DebugLog.Update();
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::LOAD ERROR:\n" + ex.Message);
            }
        }
        private void ReadHeader(Stream s)
        {
            try
            {
                s.Seek(0, 0);
                DebugLog.PrintLn("Reading Package Summary...");
                HeaderInfo h = new HeaderInfo();
                h.magic = ReadUInt(s);
                if (h.magic != 0x9E2A83C1)
                    throw new Exception("Not a valid PCC Package, wrong magic!");
                h.ver1 = ReadUInt16(s);
                h.ver2 = ReadUInt16(s);
                h.HeaderLength = ReadUInt(s);
                h.Group = ReadUString(s);
                h._offsetFlag = (uint)s.Position;
                h.Flags = ReadUInt(s);
                GeneralInfo.compressed = (h.Flags & 0x02000000) != 0;
                DebugLog.PrintLn("Is Compressed : " + GeneralInfo.compressed);
                h.unk1 = ReadUInt(s);
                if(h.unk1 != 0)
                    throw new Exception("Not a valid PCC Package, Unk1 != 0");
                h.NameCount = ReadUInt(s);
                h.NameOffset = ReadUInt(s);
                h.ExportCount = ReadUInt(s);
                h.ExportOffset = ReadUInt(s);
                h.ImportCount = ReadUInt(s);
                h.ImportOffset = ReadUInt(s);
                h.FreeZoneStart = ReadUInt(s);
                h.FreeZoneEnd = ReadUInt(s);
                h.unk2 = ReadUInt(s);
                h.unk3 = ReadUInt(s);
                h.unk4 = ReadUInt(s);
                h.GUID = new byte[16];
                s.Read(h.GUID, 0, 16);
                int count = ReadInt(s);
                DebugLog.PrintLn("Reading Generations...");
                h.Generations = new List<Generation>();
                for (int i = 0; i < count; i++)
                {
                    Generation g = new Generation();
                    g.ExportCount = ReadUInt(s);
                    g.ImportCount = ReadUInt(s);
                    g.NetObjCount = ReadUInt(s);
                    h.Generations.Add(g);
                }
                DebugLog.PrintLn("Done.");
                h.EngineVersion = ReadUInt(s);
                h.CookerVersion = ReadUInt(s);
                h.unk5 = ReadUInt(s);
                h.unk6 = ReadUInt(s);
                h.CompressionFlag = ReadUInt(s);
                h._offsetCompFlagEnd = (uint)s.Position;
                count = ReadInt(s);
                h.Chunks = new List<CompressedChunk>();
                if (GeneralInfo.compressed)
                {
                    DebugLog.PrintLn("Reading Chunktable...");
                    for (int i = 0; i < count; i++)
                    {
                        CompressedChunk c = new CompressedChunk();
                        c.UnCompOffset = ReadUInt(s);
                        c.UnCompSize = ReadUInt(s);
                        c.CompOffset = ReadUInt(s);
                        c.CompSize = ReadUInt(s);
                        h.Chunks.Add(c);
                    }
                    h.DeCompBuffer = new MemoryStream();
                    DebugLog.PrintLn("Done.");
                }
                h.unk7 = ReadUInt(s);
                h.unk8 = ReadUInt(s);
                Header = h;
                if (GeneralInfo.compressed)
                    ReadChunks(s);
                DebugLog.PrintLn("Done.");
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::READHEADER ERROR:\n" + ex.Message);
            }
        }
        private void ReadChunks(Stream s)
        {
            try
            {
                DebugLog.PrintLn("Reading Chunks...");
                for (int i = 0; i < Header.Chunks.Count; i++)
                {
                    if(verbose)
                        DebugLog.PrintLn("Reading Chunk(" + i + ") Header...");
                    CompressedChunk c = Header.Chunks[i];
                    s.Seek(c.CompOffset, 0);
                    c.Magic = ReadUInt(s);
                    if (c.Magic != 0x9E2A83C1)
                        throw new Exception("Not a valid Chunkheader, wrong magic!(#" + i + ")");
                    c.BlockSize = ReadUInt(s);
                    ReadUInt(s); 
                    ReadUInt(s);
                    uint count = (c.UnCompSize + c.BlockSize - 1) / c.BlockSize;
                    c.Blocks = new List<CompressedChunkBlock>();
                    if (verbose)
                        DebugLog.PrintLn("Reading Chunk(" + i + ") Blocks...");
                    for (int j = 0; j < count; j++)
                    {
                        CompressedChunkBlock b = new CompressedChunkBlock();
                        b.CompSize = ReadUInt(s);
                        b.UnCompSize = ReadUInt(s);
                        b.loaded = false;
                        c.Blocks.Add(b);
                    }
                    Header.Chunks[i] = c;
                }
                if (Header.Chunks.Count != 0)
                {
                    uint FullSize = Header.Chunks[Header.Chunks.Count - 1].UnCompOffset + Header.Chunks[Header.Chunks.Count - 1].UnCompSize;
                    Header.DeCompBuffer = new MemoryStream(new byte[FullSize]);
                    Header.DeCompBuffer.Seek(0, 0);
                    Source.Seek(0, 0);
                    byte[]buff = new byte[Header._offsetCompFlagEnd];
                    Source.Read(buff, 0, (int)Header._offsetCompFlagEnd);
                    Header.DeCompBuffer.Write(buff, 0, (int)Header._offsetCompFlagEnd);
                    Header.DeCompBuffer.Write(BitConverter.GetBytes((int)0), 0, 4);
                    Header.DeCompBuffer.Write(BitConverter.GetBytes(Header.unk7), 0, 4);
                    Header.DeCompBuffer.Write(BitConverter.GetBytes(Header.unk8), 0, 4);
                    Header.DeCompBuffer.Seek(Header._offsetFlag, 0);
                    uint newFlags = (Header.Flags ^ 0x02000000);
                    Header.DeCompBuffer.Write(BitConverter.GetBytes(newFlags), 0, 4);
                }
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::READCHUNKS ERROR:\n" + ex.Message);
            }
        }
        private void ReadNameTable()
        {
            try
            {
                DebugLog.PrintLn("Reading Name Table...");
                Names = new List<string>();
                if (GeneralInfo.compressed)
                {
                    UncompressRange(Header._offsetCompFlagEnd + 0xC, Header.HeaderLength - (Header._offsetCompFlagEnd + 0xC));
                    Header.DeCompBuffer.Seek(Header.NameOffset, 0);
                    for (int i = 0; i < Header.NameCount; i++)
                        Names.Add(ReadUString(Header.DeCompBuffer));
                }
                else
                {
                    Source.Seek(Header.NameOffset, 0);
                    for (int i = 0; i < Header.NameCount; i++)
                        Names.Add(ReadUString(Source));
                }
                DebugLog.PrintLn("Done.");
                if (verbose)
                    for (int i = 0; i < Header.NameCount; i++)
                      DebugLog.PrintLn(i.ToString("d5") + " : " + Names[i]);
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::READNAMETABLE ERROR:\n" + ex.Message);
            }
        }
        private void ReadImportTable()
        {
            try
            {
                DebugLog.PrintLn("Reading Import Table...");
                Imports = new List<ImportEntry>();
                if (GeneralInfo.compressed)
                {
                    UncompressRange(Header._offsetCompFlagEnd + 0xC, Header.HeaderLength - (Header._offsetCompFlagEnd + 0xC));
                    Header.DeCompBuffer.Seek(Header.ImportOffset, 0);
                    for (int i = 0; i < Header.ImportCount; i++)
                    {
                        ImportEntry e = new ImportEntry();
                        e.idxPackage = ReadInt(Header.DeCompBuffer);
                        e.Unk1 = ReadInt(Header.DeCompBuffer);
                        e.idxClass = ReadInt(Header.DeCompBuffer);
                        e.Unk2 = ReadInt(Header.DeCompBuffer);
                        e.idxLink = ReadInt(Header.DeCompBuffer);
                        e.idxName = ReadInt(Header.DeCompBuffer);
                        e.Unk3 = ReadInt(Header.DeCompBuffer);
                        Imports.Add(e);
                    }
                }
                else
                {
                    Source.Seek(Header.ImportOffset, 0);
                    for (int i = 0; i < Header.ImportCount; i++)
                    {
                        ImportEntry e = new ImportEntry();
                        e.idxPackage = ReadInt(Source);
                        e.Unk1 = ReadInt(Source);
                        e.idxClass = ReadInt(Source);
                        e.Unk2 = ReadInt(Source);
                        e.idxLink = ReadInt(Source);
                        e.idxName = ReadInt(Source);
                        e.Unk3 = ReadInt(Source);
                        Imports.Add(e);
                    }
                }
                DebugLog.PrintLn("Done.");
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::READIMPORTTABLE ERROR:\n" + ex.Message);
            }
        }
        private void ReadExportTable()
        {
            try
            {
                DebugLog.PrintLn("Reading Export Table...");
                Exports = new List<ExportEntry>();
                if (GeneralInfo.compressed)
                {
                    UncompressRange(Header._offsetCompFlagEnd + 0xC, Header.HeaderLength - (Header._offsetCompFlagEnd + 0xC));
                    Header.DeCompBuffer.Seek(Header.ExportOffset, 0);
                    for (int i = 0; i < Header.ExportCount; i++)
                    {
                        ExportEntry e = new ExportEntry();
                        e.idxClass = ReadInt(Header.DeCompBuffer);
                        e.idxParent = ReadInt(Header.DeCompBuffer);
                        e.idxLink = ReadInt(Header.DeCompBuffer);
                        e.idxName = ReadInt(Header.DeCompBuffer);
                        e.Index = ReadInt(Header.DeCompBuffer);
                        e.idxArchetype = ReadInt(Header.DeCompBuffer);
                        e.Unk1 = ReadInt(Header.DeCompBuffer);
                        e.ObjectFlags = ReadInt(Header.DeCompBuffer);
                        e.Datasize = ReadInt(Header.DeCompBuffer);
                        e.Dataoffset = ReadInt(Header.DeCompBuffer);
                        long pos = Header.DeCompBuffer.Position;
                        if (!GeneralInfo.loadfull)
                            e.DataLoaded = false;
                        else
                        {
                            e.Data = GetObjectData(e.Dataoffset, e.Datasize);
                            e.DataLoaded = true;
                        }
                        Header.DeCompBuffer.Seek(pos, 0);
                        e.Unk2 = ReadInt(Header.DeCompBuffer);
                        int count = ReadInt(Header.DeCompBuffer);
                        e.Unk3 = new int[count];
                        for (int j = 0; j < count; j++)
                            e.Unk3[j] = ReadInt(Header.DeCompBuffer);
                        e.Unk4 = ReadInt(Header.DeCompBuffer);
                        e.Unk5 = ReadInt(Header.DeCompBuffer);
                        e.Unk6 = ReadInt(Header.DeCompBuffer);
                        e.Unk7 = ReadInt(Header.DeCompBuffer);
                        e.Unk8 = ReadInt(Header.DeCompBuffer);
                        Exports.Add(e);
                    }
                }
                else
                {
                    Source.Seek(Header.ExportOffset, 0);
                    for (int i = 0; i < Header.ExportCount; i++)
                    {
                        ExportEntry e = new ExportEntry();
                        e.idxClass = ReadInt(Source);
                        e.idxParent = ReadInt(Source);
                        e.idxLink = ReadInt(Source);
                        e.idxName = ReadInt(Source);
                        e.Index = ReadInt(Source);
                        e.idxArchetype = ReadInt(Source);
                        e.Unk1 = ReadInt(Source);
                        e.ObjectFlags = ReadInt(Source);
                        e.Datasize = ReadInt(Source);
                        e.Dataoffset = ReadInt(Source);
                        long pos = Source.Position;
                        if (!GeneralInfo.loadfull)
                            e.DataLoaded = false;
                        else
                        {
                            e.Data = GetObjectData(e.Dataoffset, e.Datasize);
                            e.DataLoaded = true;
                        }
                        Source.Seek(pos, 0);
                        e.Unk2 = ReadInt(Source);
                        int count = ReadInt(Source);
                        e.Unk3 = new int[count];
                        for (int j = 0; j < count; j++)
                            e.Unk3[j] = ReadInt(Source);
                        e.Unk4 = ReadInt(Source);
                        e.Unk5 = ReadInt(Source);
                        e.Unk6 = ReadInt(Source);
                        e.Unk7 = ReadInt(Source);
                        e.Unk8 = ReadInt(Source);
                        Exports.Add(e);
                    }
                }
                DebugLog.PrintLn("Done.");
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::READEXPORTTABLE ERROR:\n" + ex.Message);
            }
        }

        public uint ReadUInt(Stream s)
        {
            byte[] buff = new byte[4];
            s.Read(buff, 0, 4);
            uint u = BitConverter.ToUInt32(buff, 0);
            if(verbose)
                DebugLog.PrintLn("Read @0x" + (s.Position - 4).ToString("X8") + " UInt32 = 0x" + u.ToString("X8"));
            return u;
        }
        public int ReadInt(Stream s)
        {
            byte[] buff = new byte[4];
            s.Read(buff, 0, 4);
            int i = BitConverter.ToInt32(buff, 0);
            if(verbose)
                DebugLog.PrintLn("Read @0x" + (s.Position - 4).ToString("X8") + " Int32 = 0x" + i.ToString("X8"));
            return i;
        }
        public float ReadFloat(Stream s)
        {
            byte[] buff = new byte[4];
            s.Read(buff, 0, 4);
            float f = BitConverter.ToSingle(buff, 0);
            if (verbose)
                DebugLog.PrintLn("Read @0x" + (s.Position - 4).ToString("X8") + " Float = " + f);
            return f;
        }
        public ushort ReadUInt16(Stream s)
        {
            byte[] buff = new byte[2];
            s.Read(buff, 0, 2);
            ushort u = BitConverter.ToUInt16(buff, 0);
            if(verbose)
                DebugLog.PrintLn("Read @0x" + (s.Position - 2).ToString("X8") + " UInt16 = 0x" + u.ToString("X8"));
            return u;
        }
        public string ReadUString(Stream s)
        {
            string res = "";
            int count = ReadInt(s);
            if (count < 0)
            {
                for (int i = 0; i < -count - 1; i++)
                {
                    res += (char)s.ReadByte();
                    s.ReadByte();
                }
                s.ReadByte();
                s.ReadByte();
                if(verbose)
                    DebugLog.PrintLn("Read @0x" + (s.Position + count * 2).ToString("X8") + " String = " + res);           
            }            
            return res;
        }
        public void WriteUString(string text, Stream s)
        {
            if (!text.EndsWith("\0"))
            {
                text += "\0";
            }
            s.Write(BitConverter.GetBytes((int)(-text.Length)), 0, 4);
            foreach (char c in text)
            {
                s.WriteByte((byte)c);
                s.WriteByte(0);
            }
        }
        public byte[] CopyArray(byte[] buff)
        {
            byte[] res = new byte[buff.Length];
            for (int i = 0; i < buff.Length; i++)
                res[i] = buff[i];
            return res;
        }

        private void UncompressRange(uint offset, uint size)
        {
            try
            {
                int startchunk = 0;
                int endchunk = -1;
                for (int i = 0; i < Header.Chunks.Count; i++)
                {
                    if (Header.Chunks[i].UnCompOffset > offset)
                        break;
                    startchunk = i;                    
                }
                for (int i = 0; i < Header.Chunks.Count; i++)
                {
                    if (Header.Chunks[i].UnCompOffset >= offset + size)
                        break;
                    endchunk = i;                    
                }
                if (startchunk == -1 || endchunk == -1)
                    return;
                for (int i = startchunk; i <= endchunk; i++)
                {
                    CompressedChunk c = Header.Chunks[i];
                    Header.DeCompBuffer.Seek(c.UnCompOffset, 0);
                    for (int j = 0; j < c.Blocks.Count; j++)
                    {
                        CompressedChunkBlock b = c.Blocks[j];
                        uint startblock = (uint)Header.DeCompBuffer.Position;
                        uint endblock = (uint)Header.DeCompBuffer.Position + b.UnCompSize;
                        if (((startblock >= offset && startblock < offset + size) ||
                            (endblock >= offset && endblock < offset + size) ||
                            (offset >= startblock && offset < endblock) ||
                            (offset + size > startblock && offset + size <= endblock)) &&
                            !b.loaded)
                        {
                            Header.DeCompBuffer.Write(UncompressBlock(i, j), 0, (int)b.UnCompSize);
                            b.loaded = true;
                            c.Blocks[j] = b;
                            Header.Chunks[i] = c;
                        }
                        else
                            Header.DeCompBuffer.Seek(b.UnCompSize, SeekOrigin.Current);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::UNCOMPRESSRANGE ERROR:\n" + ex.Message);
            }
        }
        private byte[] UncompressBlock(int ChunkIdx, int BlockIdx)
        {
            try
            {
                CompressedChunk c = Header.Chunks[ChunkIdx];
                Source.Seek(c.CompOffset, 0);
                Source.Seek(0x10 + 0x08 * c.Blocks.Count, SeekOrigin.Current);
                for (int i = 0; i < BlockIdx; i++)
                    Source.Seek(c.Blocks[i].CompSize, SeekOrigin.Current);
                return UncompressBlock(Source, c.Blocks[BlockIdx].CompSize, c.Blocks[BlockIdx].UnCompSize);
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::UNCOMPRESSBLOCK ERROR:\n" + ex.Message);
                return new byte[0];
            }            
        }
        private byte[] UncompressBlock(Stream s, uint CompSize, uint UnCompSize)
        {
            byte[] res = new byte[UnCompSize];
            try
            {
                InflaterInputStream zipstream = new InflaterInputStream(s);
                zipstream.Read(res, 0, (int)UnCompSize);
                zipstream.Flush();         
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("PCCPACKAGE::UNCOMPRESSBLOCK ERROR:\n" + ex.Message);                
            }
            return res;
        }

    }
}
