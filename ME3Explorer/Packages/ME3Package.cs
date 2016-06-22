using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using KFreonLib.Debugging;
using System.Diagnostics;
using ME3Explorer.Unreal;
using System.Windows;

namespace ME3Explorer.Packages
{
    public class ME3Package : IMEPackage
    {
        public MEGame game { get { return MEGame.ME3; } }
        public string fileName { get; private set; }

        static int headerSize = 0x8E;
        private byte[] header = new byte[headerSize];

        private uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        private ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        private ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        private uint HeaderLength { get { return BitConverter.ToUInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(uint)); } }
        private int nameSize { get { int val = BitConverter.ToInt32(header, 12); return (val < 0) ? val * -2 : val; } } // usually = 10
        private uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }

        public bool isModified { get { return Exports.Any(entry => entry.hasChanged == true); } }
        public bool canReconstruct { get { return !Exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"); } }
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

        private int idxOffsets { get { if ((flags & 8) != 0) return 24 + nameSize; else return 20 + nameSize; } } // usually = 34
        private int NameCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 68, sizeof(int));
            }
        }
        private int NameOffset
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 4); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 100, sizeof(int));
            }
        }
        private int ExportCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 8); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 64, sizeof(int));
            }
        }
        private int ExportOffset { get { Debug.WriteLine("idxOffsets: " + idxOffsets + ", offset for export offset: " + (idxOffsets + 12)); return BitConverter.ToInt32(header, idxOffsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int)); } }
        private int ImportCount { get { return BitConverter.ToInt32(header, idxOffsets + 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int)); } }
        private uint FreeZoneStart { get { return BitConverter.ToUInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(uint)); } }
        private uint FreeZoneEnd { get { return BitConverter.ToUInt32(header, idxOffsets + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(uint)); } }

        private int expInfoEndOffset { get { return BitConverter.ToInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(int)); } }
        private int expDataBegOffset
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 28); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int));
            }
        }

        public List<string> Names { get; set; }
        public List<ME3ImportEntry> Imports { get; private set; }
        public List<ME3ExportEntry> Exports { get; private set; }

        public List<IExportEntry> IExports
        {
            get
            {
                return Exports.ToList<IExportEntry>();
            }
        }

        public List<IImportEntry> IImports
        {
            get
            {
                return Imports.ToList<IImportEntry>();
            }
        }

        private List<Block> blockList = null;
        protected class Block
        {
            public int uncOffset;
            public int uncSize;
            public int cprOffset;
            public int cprSize;
            public bool bRead = false;
        }

        /// <summary>
        ///     PCCObject class constructor. It also load namelist, importlist and exportinfo (not exportdata) from pcc file
        /// </summary>
        /// <param name="pccFilePath">full path + file name of desired pcc file.</param>
        public ME3Package(string pccFilePath)
        {
            fileName = Path.GetFullPath(pccFilePath);
            using (FileStream pccStream = File.OpenRead(fileName))
            {
                Names = new List<string>();
                Imports = new List<ME3ImportEntry>();
                Exports = new List<ME3ExportEntry>();

                pccStream.Read(header, 0, header.Length);
                if (magic != ZBlock.magic &&
                    magic.Swap() != ZBlock.magic)
                {
                    throw new FormatException("not a pcc file");
                }

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
                }
                
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
                byte[] buffer = new byte[ME3ImportEntry.byteSize];
                for (int i = 0; i < ImportCount; i++)
                {

                    long offset = listsStream.Position;
                    ME3ImportEntry e = new ME3ImportEntry(this, listsStream);
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
                    ME3ExportEntry e = new ME3ExportEntry(this, buffer, expInfoOffset);
                    //Debug.WriteLine("Read export " + i + " " + e.ObjectName + ", offset: " + expInfoOffset+ ", size: "+expInfoSize); 
                    Exports.Add(e);
                }
                //load in all data
                byte[] buff;
                foreach (ME3ExportEntry e in Exports)
                {
                    buff = e.Data;
                }
            }
            Debug.WriteLine(getMetadataString());
        }

        /// <summary>
        ///     given export data offset, the function recovers it from the file.
        /// </summary>
        /// <param name="offset">offset position of desired export data</param>
        public void getData(int offset, IExportEntry exp = null)
        {
            byte[] buffer;
            if (bCompressed)
            {
                Block selected = blockList.Find(block => block.uncOffset <= offset && block.uncOffset + block.uncSize > offset);
                byte[] uncBlock;

                using (FileStream pccStream = File.OpenRead(fileName))
                {
                    pccStream.Seek(selected.cprOffset, SeekOrigin.Begin);
                    uncBlock = ZBlock.Decompress(pccStream, selected.cprSize);

                    // the selected block has been read
                    selected.bRead = true;
                }

                // fill all the exports data extracted from the uncBlock
                foreach (IExportEntry expInfo in Exports)
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
                IExportEntry expSelect;
                if (exp == null)
                {
                    int expIndex = Exports.FindIndex(export => export.DataOffset <= offset && export.DataOffset + export.DataSize > offset);
                    expSelect = Exports[expIndex];
                }
                else
                {
                    expSelect = exp;
                }
                using (FileStream pccStream = File.OpenRead(fileName))
                {
                    buffer = new byte[expSelect.DataSize];
                    pccStream.Seek(expSelect.DataOffset, SeekOrigin.Begin);
                    pccStream.Read(buffer, 0, buffer.Length);
                    expSelect.Data = buffer;
                }
            }
        }

        /// <summary>
        ///     save PCC to same file by reconstruction if possible, append if not
        /// </summary>
        public void save()
        {
            save(fileName);
        }

        /// <summary>
        ///     save PCC by reconstruction if possible, append if not
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void save(string path)
        {
            if (canReconstruct)
            {
                saveByReconstructing(path);
            }
            else
            {
                appendSave(path, true);
            }
        }

        /// <summary>
        ///     save PCCObject to file by reconstruction from data
        /// </summary>
        /// <param name="path">full path + file name.</param>
        /// <param name="compress">true if you want a zlib compressed pcc file.</param>
        public void saveByReconstructing(string path)
        {
            saveByReconstructing(path, false);
        }

        /// <summary>
        ///     save PCCObject to file by reconstruction from data
        /// </summary>
        /// <param name="path">full path + file name.</param>
        /// <param name="compress">true if you want a zlib compressed pcc file.</param>
        public void saveByReconstructing(string path, bool compress)
        {
            try
            {
                this.bCompressed = false;
                MemoryStream m = new MemoryStream();
                m.WriteBytes(header);
                //name table
                NameOffset = (int)m.Position;
                NameCount = Names.Count;
                foreach (string s in Names)
                {
                    string text = s;
                    if (!text.EndsWith("\0"))
                    {
                        text += "\0";
                    }
                    m.Write(BitConverter.GetBytes(-text.Length), 0, 4);
                    foreach (char c in text)
                    {
                        m.WriteByte((byte)c);
                        m.WriteByte(0);
                    }
                }
                //import table
                ImportOffset = (int)m.Position;
                ImportCount = Imports.Count;
                foreach (ME3ImportEntry e in Imports)
                {
                    m.WriteBytes(e.header);
                }
                //export table
                ExportOffset = (int)m.Position;
                ExportCount = Exports.Count;
                for (int i = 0; i < Exports.Count; i++)
                {
                    ME3ExportEntry e = Exports[i];
                    e.headerOffset = (uint)m.Position;
                    m.WriteBytes(e.header);
                }
                //freezone
                int FreeZoneSize = (int)FreeZoneEnd - (int)FreeZoneStart;
                FreeZoneStart = (uint)m.Position;
                m.Write(new byte[FreeZoneSize], 0, FreeZoneSize);
                FreeZoneEnd = HeaderLength = (uint)m.Position;
                //export data
                for (int i = 0; i < Exports.Count; i++)
                {
                    ME3ExportEntry e = Exports[i];
                    e.DataOffset = (int)m.Position;
                    e.DataSize = e.Data.Length;
                    m.WriteBytes(e.Data);
                    long pos = m.Position;
                    m.Seek(e.headerOffset + 32, SeekOrigin.Begin);
                    m.Write(BitConverter.GetBytes(e.DataSize), 0, 4);
                    m.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                    m.Seek(pos, SeekOrigin.Begin);
                }
                //update header
                m.Seek(0, SeekOrigin.Begin);
                m.WriteBytes(header);

                if (compress)
                {
                    MEPackageHandler.CompressAndSave(m, path);
                }
                else
                {
                    File.WriteAllBytes(path, m.ToArray()); 
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("PCC Save error:\n" + ex.Message);
            }
        }

        /// <summary>
        /// This method is an alternate way of saving PCCs
        /// Instead of reconstructing the PCC from the data taken, it instead copies across the existing
        /// data, appends changed exports, updates the export list, changes the namelist location and updates the
        /// value in the header
        /// </summary>
        /// <param name="newFileName">The filename to write to</param>
        /// <param name="attemptOverwrite">Do you wish to attempt to overwrite the existing export</param>
        public string appendSave(string newFileName, bool attemptOverwrite, int HeaderNameOffset = 34)
        {
            string rtValues = "";
            string loc = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);


            //Get info
            expInfoEndOffset = ExportOffset + Exports.Sum(export => export.header.Length);
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
            int max = Exports.Max(maxExport => maxExport.DataOffset);
            ME3ExportEntry lastExport = Exports.Find(export => export.DataOffset == max);
            int lastDataOffset = lastExport.DataOffset + lastExport.DataSize;
            //byte[] oldName;

            if (!attemptOverwrite)
            {
                int offset = ExportOffset;
                foreach (ME3ExportEntry export in Exports)
                {
                    if (!export.hasChanged)
                    {
                        offset += export.header.Length;
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
                    stream.Write(changedExports[0].header, 32, 8);
                }
            }


            byte[] oldPCC = new byte[lastDataOffset];//Check whether compressed
            if (this.bCompressed)
            {
                oldPCC = MEPackageHandler.Decompress(fileName).Take(lastDataOffset).ToArray();
            }
            else
            {
                using (FileStream oldPccStream = new FileStream(this.fileName, FileMode.Open))
                {
                    //Read the original data up to the last export
                    oldPccStream.Read(oldPCC, 0, lastDataOffset);
                }
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
                    foreach (ME3ExportEntry export in changedExports)
                    {
                        export.DataOffset = (int)newPCCStream.Position;
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                }
                else
                {
                    //If we are then move to each offset and overwrite the data with the new exports
                    foreach (ME3ExportEntry export in replaceExports)
                    {
                        //newPCCStream.Position = export.DataOffset;
                        newPCCStream.Seek(export.DataOffset, SeekOrigin.Begin);
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                    //Then move to the end and append the new data
                    //newPCCStream.Position = lastDataOffset;
                    newPCCStream.Seek(lastDataOffset, SeekOrigin.Begin);

                    foreach (ME3ExportEntry export in changedExports)
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
                newPCCStream.Seek(HeaderNameOffset, SeekOrigin.Begin);
                //And write the new info
                byte[] nameHeader = new byte[8];
                byte[] nameCount = BitConverter.GetBytes(NameCount);
                byte[] nameOff = BitConverter.GetBytes(NameOffset);
                for (int i = 0; i < 4; i++)
                    nameHeader[i] = nameCount[i];
                for (int i = 0; i < 4; i++)
                    nameHeader[i + 4] = nameOff[i];
                newPCCStream.Write(nameHeader, 0, 8);

                //update the import list
                newPCCStream.Seek(ImportOffset, SeekOrigin.Begin);
                foreach (ME3ImportEntry import in Imports)
                {
                    newPCCStream.Write(import.header, 0, import.header.Length);
                }

                //Finally, update the export list
                newPCCStream.Seek(ExportOffset, SeekOrigin.Begin);
                foreach (ME3ExportEntry export in Exports)
                {
                    newPCCStream.Write(export.header, 0, export.header.Length);
                }

                if (!attemptOverwrite)
                {
                    using (FileStream stream = new FileStream(loc + "\\exec\\infoCache.bin", FileMode.Append))
                    {
                        stream.Seek(0, SeekOrigin.End);
                        rtValues += stream.Position + " ";
                        stream.Write(changedExports[0].header, 32, 8);
                    }
                }
            }
            return rtValues;
        }

        public string getNameEntry(int index)
        {
            if (!isName(index))
                return "";
            return Names[index];
        }

        public string getObjectName(int index)
        {
            if (index > 0 && index <= ExportCount)
                return Exports[index - 1].ObjectName;
            if (-index > 0 && -index <= ImportCount)
                return Imports[-index - 1].ObjectName;
            return "";
        }

        public string getObjectClass(int index)
        {
            if (index > 0 && index <= ExportCount)
                return Exports[index - 1].ClassName;
            if (-index > 0 && -index <= ImportCount)
                return Imports[-index - 1].ClassName;
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

        /// <summary>
        ///     gets Export or Import entry
        /// </summary>
        /// <param name="index">unreal index</param>
        public IEntry getEntry(int index)
        {
            if (index > 0 && index <= ExportCount)
                return Exports[index - 1];
            if (-index > 0 && -index <= ImportCount)
                return Imports[-index - 1];
            return null;
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

        public int FindNameOrAdd(string name)
        {
            for (int i = 0; i < Names.Count; i++)
                if (Names[i] == name)
                    return i;
            Names.Add(name);
            return Names.Count - 1;
        }

        public void addName(string name)
        {
            if (!Names.Contains(name))
                Names.Add(name);
        }

        public void addImport(IImportEntry importEntry)
        {
            if (importEntry is ME3ImportEntry)
            {
                addImport(importEntry as ME3ImportEntry);
            }
            else
            {
                throw new FormatException("Cannot add import to an ME3 package that is not from ME3");
            }
        }

        public void addImport(ME3ImportEntry importEntry)
        {
            if (importEntry.fileRef != this)
                throw new Exception("you cannot add a new import entry from another pcc file, it has invalid references!");

            Imports.Add(importEntry);
            ImportCount = Imports.Count;
        }

        public void addExport(IExportEntry exportEntry)
        {
            if (exportEntry is ME3ExportEntry)
            {
                addExport(exportEntry as ME3ExportEntry);
            }
            else
            {
                throw new FormatException("Cannot add export to an ME3 package that is not from ME3");
            }
        }

        public void addExport(ME3ExportEntry exportEntry)
        {
            if (exportEntry.fileRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.hasChanged = true;

            //changing data offset in order to append it at the end of the file
            int maxOffset = Exports.Max(entry => entry.DataOffset);
            ME3ExportEntry lastExport = Exports.Find(export => export.DataOffset == maxOffset);
            int lastOffset = lastExport.DataOffset + lastExport.Data.Length;
            exportEntry.DataOffset = lastOffset;

            Exports.Add(exportEntry);
            ExportCount = Exports.Count;
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

        public bool canClone()
        {
            if (!canReconstruct)
            {
                var res = MessageBox.Show("This file contains a SeekFreeShaderCache. Cloning will cause a crash when ME3 attempts to load this file.\n" +
                    "Do you want to visit a forum thread with more information and a possible solution?",
                    "I'm sorry, Dave. I'm afraid I can't do that.", MessageBoxButton.YesNo, MessageBoxImage.Stop);
                if (res == MessageBoxResult.Yes)
                {
                    Process.Start("http://me3explorer.freeforums.org/research-how-to-turn-your-dlc-pcc-into-a-vanilla-one-t2264.html");
                }
                return false;
            }
            return true;
        }
    }
}