using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using KFreonLib.Textures;
using KFreonLib.Helpers.LiquidEngine;
using BitConverter = KFreonLib.Misc.BitConverter;
using KFreonLib.Debugging;

namespace KFreonLib.PCCObjects
{
    public class ME3PCCObject : IPCCObject
    {
        List<IImportEntry> iimports;
        List<IExportEntry> iexports;
        public string pccFileName { get; set; }

        static int headerSize = 0x8E;
        public byte[] header = new byte[headerSize];
        byte[] extraNamesList = null;
        private int gamevers = 3;

        private uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        private ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        private ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        private int nameSize { get { int val = BitConverter.ToInt32(header, 12); if (val < 0) return val * -2; else return val; } }
        public uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }

        public bool isModified { get { return Exports.Any(entry => entry.hasChanged == true); } }
        public bool bDLCStored { get; set; }
        public bool bExtraNamesList { get { return (flags & 0x10000000) != 0; } }
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

        int idxOffsets { get { if ((flags & 8) != 0) return 24 + nameSize; else return 20 + nameSize; } }
        int NameCount { get { return BitConverter.ToInt32(header, idxOffsets); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int)); } }
        int NameOffset { get { return BitConverter.ToInt32(header, idxOffsets + 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int)); } }
        int ExportCount { get { return BitConverter.ToInt32(header, idxOffsets + 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int)); } }
        int ExportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int)); } }
        int ImportCount { get { return BitConverter.ToInt32(header, idxOffsets + 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int)); } }
        int ImportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int)); } }

        int expInfoEndOffset { get { return BitConverter.ToInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(int)); } }
        int expDataBegOffset { get { return BitConverter.ToInt32(header, idxOffsets + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(int)); Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        int headerEnd;
        public bool Loaded = false;

        public long expDataEndOffset
        {
            get
            {

                uint max = Exports.Max(maxExport => maxExport.DataOffset);
                ME3ExportEntry lastEntry = null;
                foreach (ME3ExportEntry ex in Exports)
                {
                    if (ex.DataOffset == max)
                    {
                        lastEntry = ex;
                        break;
                    }

                }
                //ExportEntry lastEntry = Exports.Find(export => export.DataOffset == Exports.Max(maxExport => maxExport.DataOffset));
                /*Console.WriteLine((long)(lastEntry.DataOffset + lastEntry.DataSize) + "\n");
                Console.WriteLine((long)(lastEntry2.DataOffset + lastEntry2.DataSize) + "\n\n");*/
                return (long)(lastEntry.DataOffset + lastEntry.DataSize);
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        List<Block> blockList = null;

        // KFreon: Decompresses specific blocks if compressed, or returns data specified by offset and length.
        public byte[] Decompressor(uint offset, int length)
        {
            using (MemoryTributary retval = new MemoryTributary())
            {
                uint newoffset = 0;
                /*if (blockList.Count == 1)
                    bCompressed = false;*/
                /*if (bCompressed)
                {*/
                // KFreon: Find datablocks to decompress
                int DataStart = 0;
                int DataEnd = 0;
                int got = 0;
                for (int m = 0; m < blockList.Count; m++)
                {
                    if (got == 0 && blockList[m].uncOffset + blockList[m].uncSize > offset)
                    {
                        DataStart = m;
                        got++;
                    }

                    if (got == 1 && blockList[m].uncOffset + blockList[m].uncSize > offset + length)
                    {
                        DataEnd = m;
                        got++;
                    }

                    if (got == 2)
                        break;
                }

                if (DataEnd == 0 && DataStart != 0)
                    DataEnd = DataStart;

                /*// KFreon: Move end along so as able to read
                if (DataStart == DataEnd)
                    DataEnd++;*/

                // KFreon: Decompress blocks
                newoffset = offset - (uint)blockList[DataStart].uncOffset;
                for (int i = (int)DataStart; i <= DataEnd; i++)
                {
                    DataStream.Seek(blockList[i].cprOffset, SeekOrigin.Begin);
                    //retval.Seek(blockList[i].uncOffset, SeekOrigin.Begin);
                    retval.WriteBytes(ZBlock.Decompress(DataStream, blockList[i].cprSize));
                }
                /*}
                else
                {
                    listsStream.Seek(offset, SeekOrigin.Begin);
                    retval.ReadFrom(listsStream, length);
                    newoffset = 0;
                }*/
                //retval.Seek(offset, SeekOrigin.Begin);
                retval.Seek(newoffset, SeekOrigin.Begin);
                return retval.ReadBytes(length);
            }
        }
        protected class Block
        {
            public int uncOffset;
            public int uncSize;
            public int cprOffset;
            public int cprSize;
            public bool bRead = false;
        }

        public List<string> Names { get; set; }

        public List<ME3ImportEntry> Imports { get; set; }
        public List<ME3ExportEntry> Exports { get; set; }
        List<Block> blocklist = null;
        public int NumChunks;
        public MemoryTributary listsStream;
        public MemoryTributary DataStream;



        /// <summary>
        ///     ME3PCCObject class constructor. It also load namelist, importlist and exportinfo (not exportdata) from pcc file
        /// </summary>
        /// <param name="pccFilePath">full path + file name of desired pcc file.</param>
        public ME3PCCObject(string filePath, bool TablesOnly = false)
        {
            pccFileName = Path.GetFullPath(filePath);
            BitConverter.IsLittleEndian = true;

            MemoryTributary tempStream = new MemoryTributary();
            if (!File.Exists(pccFileName))
                throw new FileNotFoundException("LET ME KNOW ABOUT THIS! filename: " + pccFileName);

            int trycout = 0;
            while (trycout < 50)
            {
                try
                {
                    using (FileStream fs = new FileStream(pccFileName, FileMode.Open, FileAccess.Read))
                    {
                        FileInfo tempInfo = new FileInfo(pccFileName);
                        tempStream.WriteFromStream(fs, tempInfo.Length);
                        if (tempStream.Length != tempInfo.Length)
                        {
                            throw new FileLoadException("File not fully read in. Try again later");
                        }
                    }
                    break;
                }
                catch (Exception e)
                {
                    // KFreon: File inaccessible or someting
                    Console.WriteLine(e.Message);
                    DebugOutput.PrintLn("File inaccessible: " + filePath + ".  Attempt: " + trycout);
                    trycout++;
                    System.Threading.Thread.Sleep(100);
                }
            }

            ME3PCCObjectHelper(tempStream, filePath, TablesOnly);
        }

        public void ME3PCCObjectHelper(MemoryTributary tempStream, string filePath, bool TablesOnly)
        {
            tempStream.Seek(0, SeekOrigin.Begin);
            DataStream = new MemoryTributary();
            tempStream.WriteTo(DataStream);
            Names = new List<string>();
            Imports = new List<ME3ImportEntry>();
            Exports = new List<ME3ExportEntry>();

            header = tempStream.ReadBytes(headerSize);
            if (magic != ZBlock.magic &&
                    magic.Swap() != ZBlock.magic)
                throw new FormatException(filePath + " is not a pcc file");

            if (lowVers != 684 && highVers != 194)
                throw new FormatException("unsupported version");

            if (bCompressed)
            {
                // seeks the blocks info position
                tempStream.Seek(idxOffsets + 60, SeekOrigin.Begin);
                int generator = tempStream.ReadValueS32();
                tempStream.Seek((generator * 12) + 20, SeekOrigin.Current);

                int blockCount = tempStream.ReadValueS32();
                blockList = new List<Block>();

                // creating the Block list
                for (int i = 0; i < blockCount; i++)
                {
                    Block temp = new Block();
                    temp.uncOffset = tempStream.ReadValueS32();
                    temp.uncSize = tempStream.ReadValueS32();
                    temp.cprOffset = tempStream.ReadValueS32();
                    temp.cprSize = tempStream.ReadValueS32();
                    blockList.Add(temp);
                }

                // correcting the header, in case there's need to be saved
                Buffer.BlockCopy(BitConverter.GetBytes((int)0), 0, header, header.Length - 12, sizeof(int));
                tempStream.Read(header, header.Length - 8, 8);
                headerEnd = (int)tempStream.Position;

                // copying the extraNamesList
                int extraNamesLenght = blockList[0].cprOffset - headerEnd;
                if (extraNamesLenght > 0)
                {
                    extraNamesList = new byte[extraNamesLenght];
                    tempStream.Read(extraNamesList, 0, extraNamesLenght);
                    //FileStream fileStream = File.Create(Path.GetDirectoryName(pccFileName) + "\\temp.bin");
                    //fileStream.Write(extraNamesList, 0, extraNamesLenght);
                    //MessageBox.Show("posizione: " + pccStream.Position.ToString("X8"));
                }

                int dataStart = 0;
                using (MemoryStream he = new MemoryStream(header))
                {
                    he.Seek(0, SeekOrigin.Begin);
                    he.ReadValueS32();
                    he.ReadValueS32();
                    dataStart = he.ReadValueS32();
                }

                if (TablesOnly)
                {
                    int TableStart = 0;
                    for (int m = 0; m < blockList.Count; m++)
                    {
                        if (blockList[m].uncOffset + blockList[m].uncSize > dataStart)
                        {
                            TableStart = m;
                            break;
                        }
                    }

                    listsStream = new MemoryTributary();
                    tempStream.Seek(blockList[TableStart].cprOffset, SeekOrigin.Begin);
                    listsStream.Seek(blockList[TableStart].uncOffset, SeekOrigin.Begin);
                    listsStream.WriteBytes(ZBlock.Decompress(tempStream, blockList[TableStart].cprSize));
                    DataStream = new MemoryTributary();
                    tempStream.WriteTo(DataStream);
                    bCompressed = true;
                }
                else
                {
                    //Decompress ALL blocks
                    listsStream = new MemoryTributary();
                    for (int i = 0; i < blockCount; i++)
                    {
                        tempStream.Seek(blockList[i].cprOffset, SeekOrigin.Begin);
                        listsStream.Seek(blockList[i].uncOffset, SeekOrigin.Begin);
                        listsStream.WriteBytes(ZBlock.Decompress(tempStream, blockList[i].cprSize));
                    }
                }
                bCompressed = false;
            }
            else
            {
                listsStream = new MemoryTributary();
                listsStream.WriteBytes(tempStream.ToArray());
            }
            tempStream.Dispose();

            //Fill name list
            listsStream.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                int strLength = listsStream.ReadValueS32();
                Names.Add(listsStream.ReadString(strLength * -2, true, Encoding.Unicode));
            }

            // fill import list
            listsStream.Seek(ImportOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[ME3ImportEntry.byteSize];
            for (int i = 0; i < ImportCount; i++)
            {
                Imports.Add(new ME3ImportEntry(this, listsStream));
            }

            //fill export list
            listsStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                uint expInfoOffset = (uint)listsStream.Position;

                listsStream.Seek(44, SeekOrigin.Current);
                int count = listsStream.ReadValueS32();
                listsStream.Seek(-48, SeekOrigin.Current);

                int expInfoSize = 68 + (count * 4);
                buffer = new byte[expInfoSize];

                listsStream.Read(buffer, 0, buffer.Length);
                Exports.Add(new ME3ExportEntry(this, buffer, expInfoOffset));
            }
        }

        // KFreon: Alternate intialiser to allow loading from existing stream
        public ME3PCCObject(string filePath, MemoryTributary stream, bool TablesOnly = false)
        {
            pccFileName = filePath;
            BitConverter.IsLittleEndian = true;

            ME3PCCObjectHelper(stream, filePath, TablesOnly);
        }

        public ME3PCCObject()
        {
        }

        /// <summary>
        ///     save ME3PCCObject to file.
        /// </summary>
        /// <param name="newFileName">set full path + file name.</param>
        /// <param name="saveCompress">set true if you want a zlib compressed pcc file.</param>
        public void saveToFile(string newFileName = null, bool WriteToMemoryStream = false)
        {
            //Refresh header and namelist
            listsStream.Seek(expDataEndOffset, SeekOrigin.Begin);
            NameOffset = (int)listsStream.Position;
            NameCount = Names.Count;
            foreach (string name in Names)
            {
                listsStream.WriteValueS32(-(name.Length + 1));
                listsStream.WriteString(name + "\0", (uint)(name.Length + 1) * 2, Encoding.Unicode);
            }

            listsStream.Seek(0, SeekOrigin.Begin);
            listsStream.WriteBytes(header);

            // KFreon: If want to write to memorystream instead of to file, exit here
            if (WriteToMemoryStream)
                return;

            while (true)
            {
                int tries = 0;
                try
                {
                    using (FileStream fs = new FileStream(newFileName, FileMode.Create, FileAccess.Write))
                    {
                        byte[] test = listsStream.ToArray();
                        fs.WriteBytes(test);
                        test = null;
                    }
                    break;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                    tries++;
                    if (tries > 100)
                    {
                        throw new IOException("The PCC can't be written to disk because of an IOException");
                    }
                }
            }
            listsStream.Dispose();
            Exports.Clear();
            Imports.Clear();
            Names.Clear();
            Exports = null;
            Imports = null;
            Names = null;
            GC.Collect();
        }

        public void saveToFile(bool saveCompress)
        {
            saveToCprFile(null, saveCompress);
        }


        /// <summary>
        ///     save PCCObject to file.
        /// </summary>
        /// <param name="newFileName">set full path + file name.</param>
        /// <param name="saveCompress">set true if you want a zlib compressed pcc file.</param>
        public void saveToCprFile(string newFileName = null, bool saveCompress = false)
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
                foreach (ME3ImportEntry import in Imports)
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
                List<ME3ExportEntry> unchangedExports = Exports.Where(export => !export.hasChanged || (export.hasChanged && export.Data.Length <= export.DataSize)).ToList();
                List<ME3ExportEntry> changedExports = Exports.Where(export => export.hasChanged && export.Data.Length > export.DataSize).ToList();

                foreach (ME3ExportEntry export in unchangedExports)
                {
                    newPccStream.Seek(export.DataOffset, SeekOrigin.Begin);
                    //updating info values
                    export.DataSize = export.Data.Length;
                    //export.DataOffset = (int)newPccStream.Position;
                    export.DataOffset = (uint)newPccStream.Position;

                    //writing data
                    newPccStream.Write(export.Data, 0, export.Data.Length);
                }

                ME3ExportEntry lastExport = unchangedExports.Find(export => export.DataOffset == unchangedExports.Max(maxExport => maxExport.DataOffset));
                int lastDataOffset = (int)(lastExport.DataOffset + lastExport.DataSize);

                newPccStream.Seek(lastDataOffset, SeekOrigin.Begin);
                foreach (ME3ExportEntry export in changedExports)
                {
                    //updating info values
                    export.DataSize = export.Data.Length;
                    export.DataOffset = (uint)newPccStream.Position;

                    //writing data
                    newPccStream.Write(export.Data, 0, export.Data.Length);
                }

                //if (Exports.Any(x => x.Data == null))
                //    throw new Exception("values null!!");

                //writing export info
                newPccStream.Seek(ExportOffset, SeekOrigin.Begin);
                foreach (ME3ExportEntry export in Exports)
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
            if (findName(name) != -1)
                return;
            Names.Add(name);
        }

        public int addName2(string name)
        {
            int nameID = findName(name);

            if (nameID != -1)
                return nameID;

            Names.Add(name);
            return Names.Count - 1;
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
            DebugOutput.PrintLn("Saving pcc with alternate method.");
            string rtValues = "";
            string loc = KFreonLib.Misc.Methods.GetExecutingLoc();

            //Check whether compressed
            if (this.bCompressed)
                KFreonLib.PCCObjects.Misc.PCCDecompress(this.pccFileName);

            //Get info
            expInfoEndOffset = ExportOffset + Exports.Sum(export => export.info.Length);
            if (expDataBegOffset < expInfoEndOffset)
                expDataBegOffset = expInfoEndOffset;
            //List<ExportEntry> unchangedExports = Exports.Where(export => !export.hasChanged || (export.hasChanged && export.Data.Length <= export.DataSize)).ToList();
            List<ME3ExportEntry> unchangedExports = Exports.Where(export => !export.hasChanged).ToList();
            List<ME3ExportEntry> changedExports;
            List<ME3ExportEntry> replaceExports = null;

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


            uint max = Exports.Max(maxExport => maxExport.DataOffset);
            ME3ExportEntry lastExport = Exports.Find(export => export.DataOffset == max);

            int lastDataOffset = (int)(lastExport.DataOffset + lastExport.DataSize);
            byte[] oldPCC = new byte[lastDataOffset];
            //byte[] oldName;

            if (!attemptOverwrite)
            {
                int offset = ExportOffset;
                foreach (ME3ExportEntry export in Exports)
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
                Console.WriteLine();
                Console.WriteLine("Starting Save");
                newPCCStream.Seek(0, SeekOrigin.Begin);
                Console.WriteLine("newPCCStream length: " + newPCCStream.Length);
                //Write the original file up til the last original export (note that this leaves in all the original exports)
                newPCCStream.Write(oldPCC, 0, lastDataOffset);
                Console.WriteLine("OldPCC length: " + oldPCC.Length);
                Console.WriteLine("lastDataOFfset: " + lastDataOffset);
                Console.WriteLine("overwriting?: " + attemptOverwrite);
                if (!attemptOverwrite)
                {
                    //If we're not trying to overwrite then just append all the changed exports
                    foreach (ME3ExportEntry export in changedExports)
                    {
                        export.DataOffset = (uint)newPCCStream.Position;
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                }
                else
                {
                    Console.WriteLine("replaceExports count: " + replaceExports.Count);
                    //If we are then move to each offset and overwrite the data with the new exports
                    foreach (ME3ExportEntry export in replaceExports)
                    {
                        //newPCCStream.Position = export.DataOffset;
                        newPCCStream.Seek(export.DataOffset, SeekOrigin.Begin);
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                        //Console.WriteLine("exports.DataOffset: " + export.DataOffset);
                        //Console.WriteLine("export datalength: " + export.Data.Length);
                    }
                    //Then move to the end and append the new data
                    //newPCCStream.Position = lastDataOffset;
                    newPCCStream.Seek(lastDataOffset, SeekOrigin.Begin);

                    Console.WriteLine("changedExports count: " + changedExports.Count);
                    foreach (ME3ExportEntry export in changedExports)
                    {
                        export.DataOffset = (uint)newPCCStream.Position;
                        //Console.WriteLine("newstream position: " + newPCCStream.Position);
                        export.DataSize = export.Data.Length;
                        //Console.WriteLine("export size: " + export.DataSize);
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                        //Console.WriteLine("datalength: " + export.Data.Length);
                    }
                }
                //Set the new nameoffset and namecounts
                NameOffset = (int)newPCCStream.Position;
                Console.WriteLine("nameoffset: " + NameOffset);
                NameCount = Names.Count;
                Console.WriteLine("namecount: " + Names.Count);
                //Then write out the namelist
                foreach (string name in Names)
                {
                    //Console.WriteLine("name: " + name);
                    newPCCStream.WriteValueS32(-(name.Length + 1));
                    newPCCStream.WriteString(name + "\0", (uint)(name.Length + 1) * 2, Encoding.Unicode);
                }
                Console.WriteLine("newPCCStream.length: " + newPCCStream.Length);
                //Move to the name info position in the header - not a strong piece of code, but it's working so far
                //newPCCStream.Position = 34;
                newPCCStream.Seek(HeadeNameOffset, SeekOrigin.Begin);
                Console.WriteLine("headernameoffset: " + HeadeNameOffset);
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
                foreach (ME3ExportEntry export in Exports)
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

        public void addExport(IExportEntry exportEntry)
        {
            if (exportEntry.pccRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.hasChanged = true;

            //changing data offset in order to append it at the end of the file
            ME3ExportEntry lastExport = Exports.Find(export => export.DataOffset == Exports.Max(entry => entry.DataOffset));
            int lastOffset = (int)(lastExport.DataOffset + lastExport.Data.Length);
            exportEntry.DataOffset = (uint)lastOffset;

            Exports.Add((ME3ExportEntry)exportEntry);
        }



        public void SaveToFile(string path)
        {
            saveToFile();
        }

        public string GetClass(int Index)
        {
            return getClassName(Index);
        }

        public string FollowLink(int Link)
        {
            throw new NotImplementedException();
        }

        public string GetName(int Index)
        {
            return getNameEntry(Index);
        }

        public int AddName(string newName)
        {
            return addName2(newName);
        }

        #region Unused Inherited Stuff
        public int Generator
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

        public int Compression
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

        public int ExportDataEnd
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

        public uint PackageFlags
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

        public int _HeaderOff
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

        public MemoryStream m
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

        public string fullname
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

        public IExportEntry LastExport
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


        public void DumpPCC(string path)
        {
            throw new NotImplementedException();
        }

        public int FindExp(string name)
        {
            throw new NotImplementedException();
        }

        public int FindExp(string name, string className)
        {
            throw new NotImplementedException();
        }
        #endregion

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

        int IPCCObject.expDataBegOffset
        {
            get
            {
                return expDataBegOffset;
            }
            set
            {
                expDataBegOffset = value;
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

        int IPCCObject.NameCount
        {
            get
            {
                return NameCount;
            }
            set
            {
                NameCount = value;
            }
        }

        int IPCCObject.NameOffset
        {
            get
            {
                return NameOffset;
            }
            set
            {
                NameOffset = value;
            }
        }

        int IPCCObject.ExportCount
        {
            get
            {
                return ExportCount;
            }
            set
            {
                ExportCount = value;
            }
        }

        int IPCCObject.ExportOffset
        {
            get
            {
                return ExportOffset;
            }
            set
            {
                ExportOffset = value;
            }
        }

        int IPCCObject.ImportCount
        {
            get
            {
                return ImportCount;
            }
            set
            {
                ImportCount = value;
            }
        }

        int IPCCObject.ImportOffset
        {
            get
            {
                return ImportOffset;
            }
            set
            {
                ImportOffset = value;
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
                List<ME3ImportEntry> temp = new List<ME3ImportEntry>();
                for (int i = 0; i < value.Count; i++)
                    temp.Add((ME3ImportEntry)Imports[i]);
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
                List<ME3ExportEntry> temp = new List<ME3ExportEntry>();
                for (int i = 0; i < value.Count; i++)
                    temp.Add((ME3ExportEntry)Exports[i]);
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

        public Textures.ITexture2D CreateTexture2D(int expID, string pathBIOGame, uint hash = 0)
        {
            ITexture2D temptex2D = new ME3SaltTexture2D(this, expID, pathBIOGame);
            if (hash != 0)
                temptex2D.Hash = hash;
            return temptex2D;
        }
    }
}