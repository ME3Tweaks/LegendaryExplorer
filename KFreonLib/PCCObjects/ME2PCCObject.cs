using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Diagnostics;
using KFreonLib.Textures;
using KFreonLib.Helpers.LiquidEngine;
using KFreonLib.Helpers;
using BitConverter = KFreonLib.Misc.BitConverter;
using KFreonLib.Debugging;

namespace KFreonLib.PCCObjects
{
    public class ME2PCCObject : IPCCObject
    {
        public struct NameEntry
        {
            public string name;
            public int Unk;
            public int flags;
        }
        List<IImportEntry> iimports;
        List<IExportEntry> iexports;
        public byte[] header;
        int gamevers = 2;
        private uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        private ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        private ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        public int expDataBegOffset { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int nameSize { get { int val = BitConverter.ToInt32(header, 12); if (val < 0) return val * -2; else return val; } }
        public uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }
        public bool bCompressed
        {
            get { return (flags & 0x02000000) != 0; }
            set
            {
                if (value) // sets the compressed flag if bCompressed set equal to true
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | 0x02000000), 0, header, 16 + nameSize, sizeof(int));
                else // else set to false
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~0x02000000), 0, header, 16 + nameSize, sizeof(int));
            }
        }
        public int NameCount { get { return BitConverter.ToInt32(header, nameSize + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 20, sizeof(int)); } }
        public int NameOffset { get { return BitConverter.ToInt32(header, nameSize + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 24, sizeof(int)); } }
        public int ExportCount { get { return BitConverter.ToInt32(header, nameSize + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 28, sizeof(int)); } }
        public int ExportOffset { get { return BitConverter.ToInt32(header, nameSize + 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 32, sizeof(int)); } }
        public int ImportCount { get { return BitConverter.ToInt32(header, nameSize + 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 36, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, nameSize + 40); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 40, sizeof(int)); } }
        public int Generator { get { return BitConverter.ToInt32(header, nameSize + 64); } }
        public int Compression { get { return BitConverter.ToInt32(header, header.Length - 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, header.Length - 4, sizeof(int)); } }
        public int ExportDataEnd
        {
            get
            {
                return (int)(LastExport.DataOffset + LastExport.DataSize);
            }
        }
        public IExportEntry LastExport { get; set; }

        public uint PackageFlags;
        public int NumChunks;
        public MemoryTributary listsStream;
        public List<string> Names;
        public List<ME2ImportEntry> Imports;
        public List<ME2ExportEntry> Exports;
        public int _HeaderOff;
        public MemoryStream m;
        SaltLZOHelper lzo;
        public string fullname;
        public string pccFileName;

        public ME2PCCObject(String path)
        {
            lzo = new SaltLZOHelper();
            fullname = path;
            BitConverter.IsLittleEndian = true;
            DebugOutput.PrintLn("Load file : " + path);
            pccFileName = Path.GetFullPath(path);
            MemoryTributary tempStream = new MemoryTributary();
            if (!File.Exists(pccFileName))
                throw new FileNotFoundException("PCC file not found");
            using (FileStream fs = new FileStream(pccFileName, FileMode.Open, FileAccess.Read))
            {
                FileInfo tempInfo = new FileInfo(pccFileName);
                tempStream.WriteFromStream(fs, tempInfo.Length);
                if (tempStream.Length != tempInfo.Length)
                {
                    throw new FileLoadException("File not fully read in. Try again later");
                }
            }

            LoadHelper(tempStream);
        }


        public ME2PCCObject(String path, MemoryTributary tempStream)
        {
            lzo = new SaltLZOHelper();
            fullname = path;
            BitConverter.IsLittleEndian = true;
            DebugOutput.PrintLn("Load file : " + path);
            pccFileName = Path.GetFullPath(path);

            LoadHelper(tempStream);
        }

        private void LoadHelper(MemoryTributary tempStream)
        {
            tempStream.Seek(12, SeekOrigin.Begin);
            int tempNameSize = tempStream.ReadValueS32();
            tempStream.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerator = tempStream.ReadValueS32();
            tempStream.Seek(36 + tempGenerator * 12, SeekOrigin.Current);
            int tempPos = (int)tempStream.Position;
            NumChunks = tempStream.ReadValueS32();
            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadBytes(tempPos);
            tempStream.Seek(0, SeekOrigin.Begin);

            if (magic != ZBlock.magic && magic.Swap() != ZBlock.magic)
            {
                DebugOutput.PrintLn("Magic number incorrect: " + magic);
                throw new FormatException("This is not a pcc file. The magic number is incorrect.");
            }

            if (bCompressed)
            {
                DebugOutput.PrintLn("File is compressed");
                {
                    listsStream = lzo.DecompressPCC(tempStream, this);

                    //Correct the header
                    bCompressed = false;
                    listsStream.Seek(0, SeekOrigin.Begin);
                    listsStream.WriteBytes(header);

                    //Set numblocks to zero
                    listsStream.WriteValueS32(0);
                    //Write the magic number
                    listsStream.WriteValueS32(1026281201);
                    //Write 8 bytes of 0
                    listsStream.WriteValueS32(0);
                    listsStream.WriteValueS32(0);
                }
            }
            else
            {
                DebugOutput.PrintLn("File already decompressed. Reading decompressed data.");
                listsStream = tempStream;
            }

            ReadNames(listsStream);
            ReadImports(listsStream);
            ReadExports(listsStream);
            LoadExports();
        }

        private void LoadExports()
        {
            DebugOutput.PrintLn("Prefetching Export Name Data...");
            for (int i = 0; i < ExportCount; i++)
            {
                Exports[i].hasChanged = false;
                Exports[i].ObjectName = Names[Exports[i].ObjectNameID];
            }
            for (int i = 0; i < ExportCount; i++)
            {
                Exports[i].PackageFullName = FollowLink(Exports[i].LinkID);
                if (String.IsNullOrEmpty(Exports[i].PackageFullName))
                    Exports[i].PackageFullName = "Base Package";
                else if (Exports[i].PackageFullName[Exports[i].PackageFullName.Length - 1] == '.')
                    Exports[i].PackageFullName = Exports[i].PackageFullName.Remove(Exports[i].PackageFullName.Length - 1);
            }
            for (int i = 0; i < ExportCount; i++)
                Exports[i].ClassName = GetClass(Exports[i].ClassNameID);
        }

        public void SaveToFile(string path)
        {
            listsStream.Seek(ExportDataEnd, SeekOrigin.Begin); // Write names
            NameOffset = (int)listsStream.Position;
            //NameCount = Names.Where(nam => nam != null).Count();
            NameCount = Names.Count;
            foreach (String name in Names)
            {
                if (name != null)
                {
                    listsStream.WriteValueS32(name.Length + 1);
                    listsStream.WriteString(name);
                }
                else
                    listsStream.WriteValueS32(1);
                listsStream.WriteByte(0);
                listsStream.WriteValueS32(-14);
            }

            DebugOutput.PrintLn("Writing pcc to: " + path + "\nRefreshing header to stream...");
            listsStream.Seek(0, SeekOrigin.Begin);
            listsStream.WriteBytes(header);
            DebugOutput.PrintLn("Opening filestream and writing to disk...");
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                listsStream.WriteTo(fs);
        }

        private void ReadNames(MemoryTributary fs)
        {
            DebugOutput.PrintLn("Reading Names...");
            fs.Seek(NameOffset, SeekOrigin.Begin);
            Names = new List<string>();
            for (int i = 0; i < NameCount; i++)
            {
                int len = fs.ReadValueS32();
                string s = fs.ReadString((uint)(len - 1));
                fs.Seek(5, SeekOrigin.Current);
                Names.Add(s);
            }
        }

        private void ReadImports(MemoryTributary fs)
        {
            DebugOutput.PrintLn("Reading Imports...");
            Imports = new List<ME2ImportEntry>();
            fs.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ME2ImportEntry import = new ME2ImportEntry();
                import.Package = Names[fs.ReadValueS32()];
                fs.Seek(12, SeekOrigin.Current);
                import.link = fs.ReadValueS32();
                import.Name = Names[fs.ReadValueS32()];
                fs.Seek(-24, SeekOrigin.Current);
                import.raw = fs.ReadBytes(28);
                Imports.Add(import);
            }
        }

        private void ReadExports(MemoryTributary fs)
        {
            DebugOutput.PrintLn("Reading Exports...");
            fs.Seek(ExportOffset, SeekOrigin.Begin);
            Exports = new List<ME2ExportEntry>();

            for (int i = 0; i < ExportCount; i++)
            {
                long start = fs.Position;
                ME2ExportEntry exp = new ME2ExportEntry();
                exp.pccRef = this;
                exp.infoOffset = (int)start;

                fs.Seek(40, SeekOrigin.Current);
                int count = fs.ReadValueS32();
                fs.Seek(4 + count * 12, SeekOrigin.Current);
                count = fs.ReadValueS32();
                fs.Seek(4 + count * 4, SeekOrigin.Current);
                fs.Seek(16, SeekOrigin.Current);
                long end = fs.Position;
                fs.Seek(start, SeekOrigin.Begin);
                exp.info = fs.ReadBytes((int)(end - start));
                Exports.Add(exp);
                fs.Seek(end, SeekOrigin.Begin);

                if (LastExport == null || exp.DataOffset > LastExport.DataOffset)
                    LastExport = exp;
            }
        }

        public bool isName(int Index)
        {
            return (Index >= 0 && Index < NameCount);
        }

        public bool isImport(int Index)
        {
            return (Index >= 0 && Index < ImportCount);
        }

        public bool isExport(int Index)
        {
            return (Index >= 0 && Index < ExportCount);
        }

        public string GetClass(int Index)
        {
            if (Index > 0 && isExport(Index - 1))
                return Exports[Index - 1].ObjectName;
            if (Index < 0 && isImport(Index * -1 - 1))
                return Imports[Index * -1 - 1].Name;
            return "Class";
        }

        public string FollowLink(int Link)
        {
            string s = "";
            if (Link > 0 && isExport(Link - 1))
            {
                s = Exports[Link - 1].ObjectName + ".";
                s = FollowLink(Exports[Link - 1].LinkID) + s;
            }
            if (Link < 0 && isImport(Link * -1 - 1))
            {
                s = Imports[Link * -1 - 1].Name + ".";
                s = FollowLink(Imports[Link * -1 - 1].link) + s;
            }
            return s;
        }

        public string GetName(int Index)
        {
            string s = "";
            if (isName(Index))
                s = Names[Index];
            return s;
        }

        internal string getObjectName(int index)
        {
            if (index > 0 && index < ExportCount)
                return Exports[index - 1].ObjectName;
            if (index * -1 > 0 && index * -1 < ImportCount)
                return Imports[index * -1 - 1].Name;
            return "";
        }

        public int AddName(string newName)
        {
            int nameID = 0;
            //First check if name already exists
            for (int i = 0; i < NameCount; i++)
            {
                if (Names[i] == newName)
                {
                    nameID = i;
                    return nameID;
                }
            }

            Names.Add(newName);
            NameCount++;
            return Names.Count - 1;
        }

        public void DumpPCC(string path)
        {
            listsStream.Seek(0, SeekOrigin.Begin);
            byte[] stream = listsStream.ToArray();
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                fs.WriteBytes(stream);
            }
        }

        public int FindExp(string name)
        {
            for (int i = 0; i < ExportCount; i++)
            {
                if (String.Compare(Exports[i].ObjectName, name, true) == 0)
                    return i;
            }
            return -1;
        }

        public int FindExp(string name, string className)
        {
            for (int i = 0; i < ExportCount; i++)
            {
                if (String.Compare(Exports[i].ObjectName, name, true) == 0 && Exports[i].ClassName == className)
                    return i;
            }
            return -1;
        }

        byte[] IPCCObject.header
        {
            get
            {
                return header;
            }
            set
            {
                header = value;
            }
        }

        int IPCCObject.nameSize
        {
            get
            {
                return nameSize;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        uint IPCCObject.flags
        {
            get
            {
                return flags;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        int IPCCObject.Generator
        {
            get
            {
                return Generator;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        int IPCCObject.ExportDataEnd
        {
            get
            {
                return ExportDataEnd;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        uint IPCCObject.PackageFlags
        {
            get
            {
                return PackageFlags;
            }
            set
            {
                PackageFlags = value;
            }
        }

        int IPCCObject.NumChunks
        {
            get
            {
                return NumChunks;
            }
            set
            {
                NumChunks = value;
            }
        }

        MemoryTributary IPCCObject.listsStream
        {
            get
            {
                return listsStream;
            }
            set
            {
                listsStream = value;
            }
        }

        List<string> IPCCObject.Names
        {
            get
            {
                return Names;
            }
            set
            {
                Names = value;
            }
        }

        int IPCCObject._HeaderOff
        {
            get
            {
                return _HeaderOff;
            }
            set
            {
                _HeaderOff = value;
            }
        }

        MemoryStream IPCCObject.m
        {
            get
            {
                return m;
            }
            set
            {
                m = value;
            }
        }

        string IPCCObject.fullname
        {
            get
            {
                return fullname;
            }
            set
            {
                fullname = value;
            }
        }

        string IPCCObject.pccFileName
        {
            get
            {
                return pccFileName;
            }
            set
            {
                pccFileName = value;
            }
        }


        string IPCCObject.getObjectName(int index)
        {
            return this.getObjectName(index);
        }


        public string getNameEntry(int index)
        {
            throw new NotImplementedException();
        }


        List<IImportEntry> IPCCObject.Imports
        {
            get
            {
                if (iimports == null)
                    iimports = Imports.ToList<IImportEntry>();
                return iimports;
            }
            set
            {
                List<ME2ImportEntry> temp = new List<ME2ImportEntry>();
                for (int i = 0; i < value.Count; i++)
                    temp.Add((ME2ImportEntry)value[i]);
                Imports = temp;
            }
        }

        List<IExportEntry> IPCCObject.Exports
        {
            get
            {
                if (iexports == null)
                    iexports = Exports.ToList<IExportEntry>();
                return iexports;
            }
            set
            {
                List<ME2ExportEntry> temp = new List<ME2ExportEntry>();
                for (int i = 0; i < value.Count; i++)
                    temp.Add((ME2ExportEntry)value[i]);
                Exports = temp;
            }
        }

        public int GameVersion
        {
            get
            {
                return gamevers;
            }
            set
            {
                gamevers = value;
            }
        }


        public void saveToFile(string newFileName = null, bool WriteToMemoryStream = false)
        {
            SaveToFile(newFileName);
        }

        public Textures.ITexture2D CreateTexture2D(int expID, string pathBIOGame, uint hash = 0)
        {
            ITexture2D temptex2D = new ME2Texture2D(this, expID, pathBIOGame);
            if (hash != 0)
                temptex2D.Hash = hash;
            return temptex2D;
        }

        #region Unused Inherited Functions
        public string getClassName(int classname)
        {
            throw new NotImplementedException();
        }

        public void addExport(IExportEntry entry)
        {
            throw new NotImplementedException();
        }


        public long expDataEndOffset
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public bool bDLCStored
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        public int findName(string name)
        {
            return Names.IndexOf(name);
        }
    }
}
