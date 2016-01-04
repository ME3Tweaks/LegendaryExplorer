using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Windows.Forms;
using KFreonLib.Debugging;
using System.Diagnostics;

namespace ME3Explorer.Unreal
{
    public class PCCObject
    {
        public string pccFileName { get; private set; }

        static int headerSize = 0x8E;
        public byte[] header = new byte[headerSize];
        byte[] extraNamesList = null;

        private uint magic      { get { return BitConverter.ToUInt32(header, 0); } }
        private ushort lowVers  { get { return BitConverter.ToUInt16(header, 4); } }
        private ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        private int nameSize    { get { int val = BitConverter.ToInt32(header, 12); return (val < 0) ? val * -2 : val; } } // usually = 10
        public uint flags       { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }

        public bool isModified { get { return Exports.Any(entry => entry.hasChanged == true); } }
        public bool bDLCStored = false;
        public bool bExtraNamesList { get { return extraNamesList != null; } }
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
        public bool Loaded = false;

        int idxOffsets   { get { if ((flags & 8) != 0) return 24 + nameSize; else return 20 + nameSize; } } // usually = 34
        int NameCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 68, sizeof(int));
            }
        }
        public int NameOffset
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 4); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 100, sizeof(int));
            }
        }
        int ExportCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 8); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 64, sizeof(int));
            }
        }
        int ExportOffset { get { Debug.WriteLine("idxOffsets: "+idxOffsets+", offset for export offset: "+(idxOffsets+12)); return BitConverter.ToInt32(header, idxOffsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int)); } }
        int ImportCount  { get { return BitConverter.ToInt32(header, idxOffsets + 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int)); } }

        int expInfoEndOffset { get { return BitConverter.ToInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(int)); } }
        int expDataBegOffset
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 28); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int));
            }
        }

        int headerEnd;

        public List<string> Names { get; private set; }
        public List<ImportEntry> Imports { get; private set; }
        public List<ExportEntry> Exports { get; private set; }

        List<Block> blockList = null;
        protected class Block
        {
            public int uncOffset;
            public int uncSize;
            public int cprOffset;
            public int cprSize;
            public bool bRead = false;
        }

        public class ImportEntry
        {
            public static int byteSize = 28;
            internal byte[] data = new byte[byteSize];
            internal PCCObject pccRef;
            public int Link;

            public int idxPackageFile { get { return BitConverter.ToInt32(data, 0); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 0, sizeof(int)); } }
            public int idxClassName   { get { return BitConverter.ToInt32(data, 8); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 8, sizeof(int)); } }
            public int idxPackageName { get { return BitConverter.ToInt32(data, 16) - 1; } private set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, data, 16, sizeof(int)); } }
            public int idxObjectName  { get { return BitConverter.ToInt32(data, 20); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 20, sizeof(int)); } }
            public int idxLink        { get { return BitConverter.ToInt32(data, 16); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 16, sizeof(int)); } }
            public int ObjectFlags    { get { return BitConverter.ToInt32(data, 24); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 24, sizeof(int)); } }

            public string ClassName   { get { return pccRef.Names[idxClassName]; } }
            public string PackageFile { get { return pccRef.Names[idxPackageFile] + ".pcc"; } }
            public string ObjectName  { get { return pccRef.Names[idxObjectName]; } }
            public string PackageName { get { int val = idxPackageName; if (val >= 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "Package"; } }
            public string PackageFullName
            {
                get
                {
                    string result = PackageName;
                    int idxNewPackName = idxPackageName;

                    while (idxNewPackName >= 0)
                    {
                        string newPackageName = pccRef.Exports[idxNewPackName].PackageName;
                        if (newPackageName != "Package")
                            result = newPackageName + "." + result;
                        idxNewPackName = pccRef.Exports[idxNewPackName].idxPackageName;
                    }
                    return result;
                }
            }

            public ImportEntry(PCCObject pccFile, byte[] importData)
            {
                pccRef = pccFile;
                data = (byte[])importData.Clone();
            }

            public ImportEntry(PCCObject pccFile, Stream importData)
            {
                pccRef = pccFile;
                data = new byte[ImportEntry.byteSize];
                importData.Read(data, 0, data.Length);
            }
        }

        public class ExportEntry : ICloneable // class containing info about export entry (header info + data)
        {
            internal byte[] info; // holds data about export header, not the export data.
            public PCCObject pccRef;
            public uint offset { get; set; }
            public int Link; // deprecated var, soon will be removed

            public int idxClassName    { get { return BitConverter.ToInt32(info, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 0, sizeof(int)); } }
            public int idxClassParent  { get { return BitConverter.ToInt32(info, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 4, sizeof(int)); } }
            public int idxLink         { get { return BitConverter.ToInt32(info, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 8, sizeof(int)); } }
            public int idxPackageName  { get { return BitConverter.ToInt32(info, 8) - 1; } set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, info, 8, sizeof(int)); } }
            public int idxObjectName   { get { return BitConverter.ToInt32(info, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 12, sizeof(int)); } }
            public int indexValue      { get { return BitConverter.ToInt32(info, 16); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 16, sizeof(int)); } }
            public int idxArchtypeName { get { return BitConverter.ToInt32(info, 20); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 20, sizeof(int)); } }
            public long ObjectFlags    { get { return BitConverter.ToInt64(info, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 64, sizeof(long)); } }

            public string ObjectName   { get { return pccRef.Names[idxObjectName]; } }
            public string ClassName    { get { int val = idxClassName; if (val < 0)  return pccRef.Names[pccRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "Class"; } }
            public string ClassParent  { get { int val = idxClassParent; if (val < 0)  return pccRef.Names[pccRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return pccRef.Names[pccRef.Exports[val - 1].idxObjectName]; else return "Class"; } }
            public string PackageName  { get { int val = idxPackageName; if (val >= 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "Package"; } }
            public string PackageFullName
            {
                get
                {
                    string result = PackageName;
                    int idxNewPackName = idxPackageName;

                    while (idxNewPackName >= 0)
                    {
                        string newPackageName = pccRef.Exports[idxNewPackName].PackageName;
                        if (newPackageName != "Package")
                            result = newPackageName + "." + result;
                        idxNewPackName = pccRef.Exports[idxNewPackName].idxPackageName;
                    }
                    return result;
                }
            }

            public string ContainingPackage
            {
                get
                {
                    string result = PackageName;
                    if (result.EndsWith(ObjectName))
                    {
                        result = "";
                    }
                    int idxNewPackName = idxPackageName;

                    while (idxNewPackName >= 0)
                    {
                        string newPackageName = pccRef.Exports[idxNewPackName].PackageName;
                        if (newPackageName != "Package")
                        {
                            if (!result.Equals(""))
                            {
                                result = newPackageName + "." + result;
                            }
                            else
                            {
                                result = newPackageName;
                            }
                        }
                        idxNewPackName = pccRef.Exports[idxNewPackName].idxPackageName;
                    }
                    return result;
                }
            }

            public string GetFullPath
            {
                get
                {
                    string s = "";
                    if (PackageFullName != "Class" && PackageFullName != "Package")
                        s += PackageFullName + ".";
                    s += ObjectName;
                    return s;
                }
            }
            public string ArchtypeName { get { int val = idxArchtypeName; if (val < 0)  return pccRef.Names[pccRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "None"; } }

            public int DataSize   { get { return BitConverter.ToInt32(info, 32); } internal set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 32, sizeof(int)); } }
            public int DataOffset { get { return BitConverter.ToInt32(info, 36); } internal set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 36, sizeof(int)); } }
            public int DataOffsetTmp;
            byte[] _data = null;
            public byte[] Data // holds data about export data
            {
                get
                {
                    // if data isn't loaded then fill it from pcc file (load-on-demand)
                    if (_data == null)
                    {
                        pccRef.getData(DataOffset);
                    }
                    return _data;
                }

                set { _data = value; hasChanged = true; }
            }
            public bool likelyCoalescedVal
            {
                get
                {
                    return (Data.Length < 25) ? false : (Data[25] == 64); //0x40
                }
                set { }
            }
            public bool hasChanged { get; internal set; }

            public ExportEntry(PCCObject pccFile, byte[] importData, uint exportOffset)
            {
                pccRef = pccFile;
                info = (byte[])importData.Clone();
                offset = exportOffset;
                hasChanged = false;
            }

            public ExportEntry()
            {
                // TODO: Complete member initialization
            }

            object ICloneable.Clone()
            {
                return this.Clone();
            }

            public ExportEntry Clone()
            {
                ExportEntry newExport = (ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
                // now creates new copies of referenced objects
                newExport.info = (byte[])this.info.Clone();
                newExport.Data = (byte[])this.Data.Clone();
                return newExport;
            }
        }

        /// <summary>
        ///     PCCObject class constructor. It also load namelist, importlist and exportinfo (not exportdata) from pcc file
        /// </summary>
        /// <param name="pccFilePath">full path + file name of desired pcc file.</param>
        public PCCObject(string pccFilePath, bool fullFileInMemory = false)
        {
            Loaded = true;
            pccFileName = Path.GetFullPath(pccFilePath);
            using (FileStream pccStream = File.OpenRead(pccFileName))
            {
                Names = new List<string>();
                Imports = new List<ImportEntry>();
                Exports = new List<ExportEntry>();

                pccStream.Read(header, 0, header.Length);
                if (magic != ZBlock.magic &&
                    magic.Swap() != ZBlock.magic)
                {
                    throw new FormatException("not a pcc file");
                }

                // BitConverter isn't working?!?!
                if (magic == 0x9E2A83C1)
                    BitConverter.IsLittleEndian = true;
                else
                    BitConverter.IsLittleEndian = true;

                if (lowVers != 684 && highVers != 194)
                {
                    throw new FormatException("unsupported version");
                }

                Stream listsStream;

                if (bCompressed)
                {
                    // seeks the blocks info position
                    pccStream.Seek(idxOffsets + 60, SeekOrigin.Begin);
                    int generator = pccStream.ReadValueS32();
                    pccStream.Seek((generator * 12) + 20, SeekOrigin.Current);

                    int blockCount = pccStream.ReadValueS32();
                    blockList = new List<Block>();

                    // creating the Block list
                    for (int i = 0; i < blockCount; i++)
                    {
                        Block temp = new Block();
                        temp.uncOffset = pccStream.ReadValueS32();
                        temp.uncSize = pccStream.ReadValueS32();
                        temp.cprOffset = pccStream.ReadValueS32();
                        temp.cprSize = pccStream.ReadValueS32();
                        blockList.Add(temp);
                    }

                    // correcting the header, in case there's need to be saved
                    Buffer.BlockCopy(BitConverter.GetBytes((int)0), 0, header, header.Length - 12, sizeof(int));
                    pccStream.Read(header, header.Length - 8, 8);
                    headerEnd = (int)pccStream.Position;

                    // copying the extraNamesList
                    int extraNamesLenght = blockList[0].cprOffset - headerEnd;
                    if (extraNamesLenght > 0)
                    {
                        extraNamesList = new byte[extraNamesLenght];
                        pccStream.Read(extraNamesList, 0, extraNamesLenght);
                        //FileStream fileStream = File.Create(Path.GetDirectoryName(pccFileName) + "\\temp.bin");
                        //fileStream.Write(extraNamesList, 0, extraNamesLenght);
                        //MessageBox.Show("posizione: " + pccStream.Position.ToString("X8"));
                    }

                    // decompress first block that holds infos about names, imports and exports
                    pccStream.Seek(blockList[0].cprOffset, SeekOrigin.Begin);
                    byte[] uncBlock = ZBlock.Decompress(pccStream, blockList[0].cprSize);

                    // write decompressed block inside temporary stream
                    listsStream = new MemoryStream();
                    listsStream.Seek(blockList[0].uncOffset, SeekOrigin.Begin);
                    listsStream.Write(uncBlock, 0, uncBlock.Length);
                }
                else
                {
                    listsStream = pccStream;
                    headerEnd = (int)NameOffset;

                    // copying the extraNamesList
                    int extraNamesLenght = headerEnd - headerSize;
                    if (extraNamesLenght > 0)
                    {
                        extraNamesList = new byte[extraNamesLenght];
                        listsStream.Seek(headerSize, SeekOrigin.Begin);
                        listsStream.Read(extraNamesList, 0, extraNamesLenght);
                        //FileStream fileStream = File.Create(Path.GetDirectoryName(pccFileName) + "\\temp.bin");
                        //fileStream.Write(extraNamesList, 0, extraNamesLenght);
                        //MessageBox.Show("posizione: " + pccStream.Position.ToString("X8"));
                    }
                }

                /*if(bExtraNamesList)
                {
                    int extraNamesListSize = namesOffset - headerEnd;
                    extraNamesList = new byte[extraNamesListSize];
                    pccStream.Seek(headerEnd, SeekOrigin.Begin);
                    pccStream.Read(extraNamesList, 0, extraNamesList.Length);
                }*/

                // fill names list
                listsStream.Seek(NameOffset, SeekOrigin.Begin);
                for (int i = 0; i < NameCount; i++)
                {
                    long currOffset = listsStream.Position;
                    int strLength = listsStream.ReadValueS32();
                    string str = listsStream.ReadString(strLength * -2, true, Encoding.Unicode);
                    //Debug.WriteLine("Read name "+i+" "+str+" length: " + strLength+", offset: "+currOffset);
                    Names.Add(str);
                }
                //Debug.WriteLine("Names done. Current offset: "+listsStream.Position);
                //Debug.WriteLine("Import Offset: " + ImportOffset);

                // fill import list
                //Console.Out.WriteLine("IMPORT OFFSET: " + ImportOffset);
                listsStream.Seek(ImportOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[ImportEntry.byteSize];
                for (int i = 0; i < ImportCount; i++)
                {

                    long offset = listsStream.Position;
                    ImportEntry e = new ImportEntry(this, listsStream);
                    Imports.Add(e);
                    //Debug.WriteLine("Read import " + i + " " + e.ObjectName + ", offset: " + offset);
                };

                // fill export list (only the headers, not the data)
                listsStream.Seek(ExportOffset, SeekOrigin.Begin);
                //Console.Out.WriteLine("Export OFFSET: " + ImportOffset);
                for (int i = 0; i < ExportCount; i++)
                {
                    uint expInfoOffset = (uint)listsStream.Position;

                    listsStream.Seek(44, SeekOrigin.Current);
                    int count = listsStream.ReadValueS32();
                    listsStream.Seek(-48, SeekOrigin.Current);

                    int expInfoSize = 68 + (count * 4);
                    buffer = new byte[expInfoSize];

                    listsStream.Read(buffer, 0, buffer.Length);
                    ExportEntry e = new ExportEntry(this, buffer, expInfoOffset);                    
                    //Debug.WriteLine("Read export " + i + " " + e.ObjectName + ", offset: " + expInfoOffset+ ", size: "+expInfoSize);

                    Exports.Add(e);
                }
            }
            Debug.WriteLine(getMetadataString());
        }

        public PCCObject()
        {
        }

        /// <summary>
        ///     given export data offset, the function recovers it from the file.
        /// </summary>
        /// <param name="offset">offset position of desired export data</param>
        private void getData(int offset)
        {
            byte[] buffer;
            if (bCompressed)
            {
                Block selected = blockList.Find(block => block.uncOffset <= offset && block.uncOffset + block.uncSize > offset);
                byte[] uncBlock;

                using (FileStream pccStream = File.OpenRead(pccFileName))
                {
                    pccStream.Seek(selected.cprOffset, SeekOrigin.Begin);
                    uncBlock = ZBlock.Decompress(pccStream, selected.cprSize);

                    // the selected block has been read
                    selected.bRead = true;
                }

                // fill all the exports data extracted from the uncBlock
                foreach (ExportEntry expInfo in Exports)
                {
                    if (expInfo.DataOffset >= selected.uncOffset && expInfo.DataOffset + expInfo.DataSize <= selected.uncOffset + selected.uncSize)
                    {
                        buffer = new byte[expInfo.DataSize];
                        Buffer.BlockCopy(uncBlock, expInfo.DataOffset - selected.uncOffset, buffer, 0, expInfo.DataSize);
                        expInfo.Data = buffer;
                    }
                }
            }
            else
            {
                int expIndex = Exports.FindIndex(export => export.DataOffset <= offset && export.DataOffset + export.DataSize > offset);
                ExportEntry expSelect = Exports[expIndex];
                using (FileStream pccStream = File.OpenRead(pccFileName))
                {
                    buffer = new byte[expSelect.DataSize];
                    pccStream.Seek(expSelect.DataOffset, SeekOrigin.Begin);
                    pccStream.Read(buffer, 0, buffer.Length);
                    expSelect.Data = buffer;
                }
            }
        }

        /// <summary>
        ///     save PCCObject to original file.
        /// </summary>
        /// <param name="saveCompress">set true if you want a zlib compressed pcc file.</param>
        public void saveToFile(bool saveCompress)
        {
            saveToFile(null, saveCompress);
        }


        /// <summary>
        ///     save PCCObject to file.
        /// </summary>
        /// <param name="newFileName">set full path + file name.</param>
        /// <param name="saveCompress">set true if you want a zlib compressed pcc file.</param>
        public void saveToFile(string newFileName = null, bool saveCompress = false)
        {
            bool bOverwriteFile = false;

            if (string.IsNullOrWhiteSpace(newFileName) || newFileName == pccFileName)
            {
                bOverwriteFile = true;
                newFileName = Path.GetFullPath(pccFileName) + ".tmp";
            }

            if (bDLCStored)
                saveCompress = false;

            using (MemoryStream newPccStream = new MemoryStream())
            {
                //ME3Explorer.DebugOutput.Clear();
                DebugOutput.PrintLn("Saving file...");

                DebugOutput.PrintLn("writing names list...");

                // this condition needs a deeper check. todo...
                if (bExtraNamesList)
                {
                    //MessageBox.Show("sono dentro, dimensione extranamelist: " + extraNamesList.Length + " bytes");
                    newPccStream.Seek(headerSize, SeekOrigin.Begin);
                    newPccStream.Write(extraNamesList, 0, extraNamesList.Length);
                }

                //writing names list
                newPccStream.Seek(NameOffset, SeekOrigin.Begin);
                NameCount = Names.Count;
                foreach (string name in Names)
                {
                    newPccStream.WriteValueS32(-(name.Length + 1));
                    newPccStream.WriteString(name + "\0", (uint)(name.Length + 1) * 2, Encoding.Unicode);
                }

                DebugOutput.PrintLn("writing imports list...");

                //writing import infos
                ImportOffset = (int)newPccStream.Position;
                ImportCount = Imports.Count;
                foreach (ImportEntry import in Imports)
                    newPccStream.Write(import.data, 0, import.data.Length);

                //updating general export infos
                ExportOffset = (int)newPccStream.Position;
                ExportCount = Exports.Count;
                expInfoEndOffset = ExportOffset + Exports.Sum(export => export.info.Length);
                expDataBegOffset = expInfoEndOffset;

                // WV code stuff...
                DebugOutput.PrintLn("writing export data...");
                int counter = 0;
                int breaker = Exports.Count / 100;
                if (breaker == 0)
                    breaker = 1;

                //updating general export infos
                ExportOffset = (int)newPccStream.Position;
                ExportCount = Exports.Count;
                expInfoEndOffset = ExportOffset + Exports.Sum(export => export.info.Length);
                if (expDataBegOffset < expInfoEndOffset)
                    expDataBegOffset = expInfoEndOffset;

                //writing export data
                /*newPccStream.Seek(expDataBegOffset, SeekOrigin.Begin);
                foreach (ExportEntry export in Exports)
                {
                    //updating info values
                    export.DataSize = export.Data.Length;
                    export.DataOffset = (int)newPccStream.Position;

                    //writing data
                    newPccStream.Write(export.Data, 0, export.Data.Length);
                }*/
                //writing export data
                List<ExportEntry> unchangedExports = Exports.Where(export => !export.hasChanged || (export.hasChanged && export.Data.Length <= export.DataSize)).ToList();
                List<ExportEntry> changedExports = Exports.Where(export => export.hasChanged && export.Data.Length > export.DataSize).ToList();

                foreach (ExportEntry export in unchangedExports)
                {
                    newPccStream.Seek(export.DataOffset, SeekOrigin.Begin);
                    //updating info values
                    export.DataSize = export.Data.Length;
                    export.DataOffset = (int)newPccStream.Position;

                    //writing data
                    newPccStream.Write(export.Data, 0, export.Data.Length);
                }

                ExportEntry lastExport = unchangedExports.Find(export => export.DataOffset == unchangedExports.Max(maxExport => maxExport.DataOffset));
                int lastDataOffset = lastExport.DataOffset + lastExport.DataSize;

                newPccStream.Seek(lastDataOffset, SeekOrigin.Begin);
                foreach (ExportEntry export in changedExports)
                {
                    //updating info values
                    export.DataSize = export.Data.Length;
                    export.DataOffset = (int)newPccStream.Position;

                    //writing data
                    newPccStream.Write(export.Data, 0, export.Data.Length);
                }

                //if (Exports.Any(x => x.Data == null))
                //    throw new Exception("values null!!");

                //writing export info
                newPccStream.Seek(ExportOffset, SeekOrigin.Begin);
                foreach (ExportEntry export in Exports)
                {
                    newPccStream.Write(export.info, 0, export.info.Length);
                }
                /*foreach (ExportEntry export in unchangedExports)
                {
                    newPccStream.Write(export.info, 0, export.info.Length);
                }
                foreach (ExportEntry export in changedExports)
                {
                    newPccStream.Write(export.info, 0, export.info.Length);
                }*/

                DebugOutput.PrintLn("writing header file...");

                //writing header
                bCompressed = false;
                newPccStream.Seek(0, SeekOrigin.Begin);
                newPccStream.Write(header, 0, header.Length);
                newPccStream.Seek(0, SeekOrigin.Begin);

                if (saveCompress)
                {
                    DebugOutput.PrintLn("compressing in zlib format, it may take a while...");
                    PCCHandler.CompressAndSave(newPccStream, newFileName);
                }
                else
                {
                    using (FileStream outputStream = File.Create(newFileName))
                    {
                        newPccStream.CopyTo(outputStream);
                    }
                }
            }

            if (bOverwriteFile)
            {
                File.Delete(pccFileName);
                File.Move(newFileName, pccFileName);
            }
            DebugOutput.PrintLn(Path.GetFileName(pccFileName) + " has been saved.");
        }

        public string getNameEntry(int index)
        {
            if (!isName(index))
                return "";
            return Names[index];
        }

        public string getObjectName(int index)
        {
            if (index > 0 && index < ExportCount)
                return Exports[index - 1].ObjectName;
            if (index * -1 > 0 && index * -1 < ImportCount)
                return Imports[index * -1 - 1].ObjectName;
            return "";
        }

        public string getObjectClass(int index)
        {
            if (index > 0 && index < ExportCount)
                return Exports[index - 1].ClassName;
            if (index * -1 > 0 && index * -1 < ImportCount)
                return Imports[index * -1 - 1].ClassName;
            return "";
        }

        public string getClassName(int index)
        {
            string s = "";
            if (index > 0)
            {
                s = Names[Exports[index - 1].idxObjectName];
            }
            if (index < 0)
            {
                s = Names[Imports[index * -1 - 1].idxObjectName];
            }
            if (index == 0)
            {
                s = "Class";
            }
            return s;
        }

        public bool isName(int index)
        {
            return (index >= 0 && index < Names.Count);
        }
        public bool isImport(int index)
        {
            return (index >= 0 && index < Imports.Count);
        }
        public bool isExport(int index)
        {
            return (index >= 0 && index < Exports.Count);
        }

        public void addName(string name)
        {
            Names.Add(name);
        }

        public void addImport(PCCObject.ImportEntry importEntry)
        {
            if (importEntry.pccRef != this)
                throw new Exception("you cannot add a new import entry from another pcc file, it has invalid references!");

            Imports.Add(importEntry);
        }

        public void addExport(PCCObject.ExportEntry exportEntry)
        {
            if (exportEntry.pccRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.hasChanged = true;

            //changing data offset in order to append it at the end of the file
            ExportEntry lastExport = Exports.Find(export => export.DataOffset == Exports.Max(entry => entry.DataOffset));
            int lastOffset = lastExport.DataOffset + lastExport.Data.Length;
            exportEntry.DataOffset = lastOffset;

            Exports.Add(exportEntry);
        }
        
        /// <summary>
        /// This method is an alternate way of saving PCCs
        /// Instead of reconstructing the PCC from the data taken, it instead copies across the existing
        /// data, appends new exports, updates the export list, changes the namelist location and updates the
        /// value in the header
        /// </summary>
        /// <param name="newFileName">The filename to write to</param>
        /// <param name="attemptOverwrite">Do you wish to attempt to overwrite the existing export</param>
        public string altSaveToFile(string newFileName, bool attemptOverwrite, int HeadeNameOffset = 34)
        {
            string rtValues = "";
            string loc = Path.GetDirectoryName(Application.ExecutablePath);

            //Check whether compressed
            if (this.bCompressed)
            {
                Form2 decompress = new Form2();
                decompress.Decompress(this.pccFileName);
                decompress.Close();
                //MessageBox.Show("Decompression complete");
            }

            //Get info
            expInfoEndOffset = ExportOffset + Exports.Sum(export => export.info.Length);
            if (expDataBegOffset < expInfoEndOffset)
                expDataBegOffset = expInfoEndOffset;
            //List<ExportEntry> unchangedExports = Exports.Where(export => !export.hasChanged || (export.hasChanged && export.Data.Length <= export.DataSize)).ToList();
            List<ExportEntry> unchangedExports = Exports.Where(export => !export.hasChanged).ToList();
            List<ExportEntry> changedExports;
            List<ExportEntry> replaceExports = null;
            if (!attemptOverwrite)
            {
                //If not trying to overwrite, then select all exports that have been changed
                changedExports = Exports.Where(export => export.hasChanged).ToList();
                //MessageBox.Show("No changed exports = " + changedExports.Count);
                //throw new NullReferenceException();
            }
            else
            {
                //If we are trying to overwrite, then split up the exports that have been changed that can and can't overwrite the originals
                changedExports = Exports.Where(export => export.hasChanged && export.Data.Length > export.DataSize).ToList();
                replaceExports = Exports.Where(export => export.hasChanged && export.Data.Length <= export.DataSize).ToList();
            }
            //ExportEntry lastExport = unchangedExports.Find(export => export.DataOffset == unchangedExports.Max(maxExport => maxExport.DataOffset));
            ExportEntry lastExport = Exports.Find(export => export.DataOffset == Exports.Max(maxExport => maxExport.DataOffset));
            int lastDataOffset = lastExport.DataOffset + lastExport.DataSize;
            byte[] oldPCC = new byte[lastDataOffset];
            //byte[] oldName;

            if (!attemptOverwrite)
            {
                int offset = ExportOffset;
                foreach (ExportEntry export in Exports)
                {
                    if (!export.hasChanged)
                    {
                        offset += export.info.Length;
                    }
                    else
                        break;
                }
                rtValues += offset.ToString() + " ";
                using (FileStream stream = new FileStream(loc + "\\exec\\infoCache.bin", FileMode.Append))
                {
                    stream.Seek(0, SeekOrigin.End);
                    rtValues += stream.Position + " ";
                    //throw new FileNotFoundException();
                    stream.Write(changedExports[0].info, 32, 8);
                }
            }


            using (FileStream oldPccStream = new FileStream(this.pccFileName, FileMode.Open))
            {
                //Read the original data up to the last export
                oldPccStream.Read(oldPCC, 0, lastDataOffset);
                #region Unused code
                /* Maybe implement this if I want to directly copy the names across.
                 * Not useful at this time
                if (NameOffset == 0x8E)
                {
                    oldName = new byte[ImportOffset - 0x8E];
                    oldPccStream.Seek(0x8E, SeekOrigin.Begin);
                    oldPccStream.Read(oldName, 0, (int)oldPccStream.Length - lastDataOffset);
                }
                else
                {
                    oldName = new byte[oldPccStream.Length - lastDataOffset];
                    oldPccStream.Seek(lastDataOffset, SeekOrigin.Begin);
                    oldPccStream.Read(oldName, 0, (int)oldPccStream.Length - lastDataOffset);
                }
                 * */
                #endregion
            }
            //Start writing the new file
            using (FileStream newPCCStream = new FileStream(newFileName, FileMode.Create))
            {
                newPCCStream.Seek(0, SeekOrigin.Begin);
                //Write the original file up til the last original export (note that this leaves in all the original exports)
                newPCCStream.Write(oldPCC, 0, lastDataOffset);
                if (!attemptOverwrite)
                {
                    //If we're not trying to overwrite then just append all the changed exports
                    foreach (ExportEntry export in changedExports)
                    {
                        export.DataOffset = (int)newPCCStream.Position;
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                }
                else
                {
                    //If we are then move to each offset and overwrite the data with the new exports
                    foreach (ExportEntry export in replaceExports)
                    {
                        //newPCCStream.Position = export.DataOffset;
                        newPCCStream.Seek(export.DataOffset, SeekOrigin.Begin);
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                    //Then move to the end and append the new data
                    //newPCCStream.Position = lastDataOffset;
                    newPCCStream.Seek(lastDataOffset, SeekOrigin.Begin);
                    
                    foreach (ExportEntry export in changedExports)
                    {
                        export.DataOffset = (int)newPCCStream.Position;
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                }
                //Set the new nameoffset and namecounts
                NameOffset = (int)newPCCStream.Position;
                NameCount = Names.Count;
                //Then write out the namelist
                foreach (string name in Names)
                {
                    newPCCStream.WriteValueS32(-(name.Length + 1));
                    newPCCStream.WriteString(name + "\0", (uint)(name.Length + 1) * 2, Encoding.Unicode);
                }
                //Move to the name info position in the header - not a strong piece of code, but it's working so far
                //newPCCStream.Position = 34;
                newPCCStream.Seek(HeadeNameOffset, SeekOrigin.Begin);
                //And write the new info
                byte[] nameHeader = new byte[8];
                byte[] nameCount = BitConverter.GetBytes(NameCount);
                byte[] nameOff = BitConverter.GetBytes(NameOffset);
                for (int i = 0; i < 4; i++)
                    nameHeader[i] = nameCount[i];
                for (int i = 0; i < 4; i++)
                    nameHeader[i + 4] = nameOff[i];
                newPCCStream.Write(nameHeader, 0, 8);

                //Finally, update the export list
                newPCCStream.Seek(ExportOffset, SeekOrigin.Begin);
                foreach (ExportEntry export in Exports)
                {
                    newPCCStream.Write(export.info, 0, export.info.Length);
                }

                if (!attemptOverwrite)
                {
                    using (FileStream stream = new FileStream(loc + "\\exec\\infoCache.bin", FileMode.Append))
                    {
                        stream.Seek(0, SeekOrigin.End);
                        rtValues += stream.Position + " ";
                        stream.Write(changedExports[0].info, 32, 8);
                    }
                }
            }
            return rtValues;
        }

        /// <summary>
        /// Checks whether a name exists in the PCC and returns its index
        /// If it doesn't exist returns -1
        /// </summary>
        /// <param name="nameToFind">The name of the string to find</param>
        /// <returns></returns>
        public int findName(string nameToFind)
        {
            for (int i = 0; i < Names.Count; i++)
            {
                if (String.Compare(nameToFind, getNameEntry(i)) == 0)
                    return i;
            }
            return -1;
        }

        public string getMetadataString()
        {
            string str = "PCC File Metadata";
            str += "\nNames Offset: " + this.NameOffset;
            str += "\nImports Offset: " + this.ImportOffset;
            str += "\nExport Offset: " + this.ExportOffset;
            return str;

        }
    }
}