using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AmaroK86.MassEffect3.ZlibBlock;
using Gibbed.IO;
using KFreonLib.Debugging;

namespace ME3Explorer.Packages
{
    public class ME2Package : IMEPackage
    {
        public MEGame game { get { return MEGame.ME2; } }
        public string fileName { get; private set; }

        public byte[] header;
        private uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        private ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        private ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        private int expDataBegOffset { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        private int nameSize { get { int val = BitConverter.ToInt32(header, 12); if (val < 0) return val * -2; else return val; } }
        public uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }

        public bool isModified { get { return exports.Any(entry => entry.hasChanged == true); } }
        public bool canReconstruct { get { return !exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"); } }
        public bool bCompressed
        {
            get { return (flags & 0x02000000) != 0; }
            private set
            {
                if (value) // sets the compressed flag if bCompressed set equal to true
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | 0x02000000), 0, header, 16 + nameSize, sizeof(int));
                else // else set to false
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~0x02000000), 0, header, 16 + nameSize, sizeof(int));
            }
        }
        private int NameCount { get { return BitConverter.ToInt32(header, nameSize + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 20, sizeof(int)); } }
        private int NameOffset { get { return BitConverter.ToInt32(header, nameSize + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 24, sizeof(int)); } }
        public int ExportCount { get { return BitConverter.ToInt32(header, nameSize + 28); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 28, sizeof(int)); } }
        private int ExportOffset { get { return BitConverter.ToInt32(header, nameSize + 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 32, sizeof(int)); } }
        public int ImportCount { get { return BitConverter.ToInt32(header, nameSize + 36); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 36, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, nameSize + 40); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 40, sizeof(int)); } }
        private int FreeZoneStart { get { return BitConverter.ToInt32(header, nameSize + 44); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 44, sizeof(int)); } }
        private int Generator { get { return BitConverter.ToInt32(header, nameSize + 64); } }
        private int Compression { get { return BitConverter.ToInt32(header, header.Length - 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, header.Length - 4, sizeof(int)); } }
        
        private ME2ExportEntry LastExport;

        public uint PackageFlags;

        private List<string> names;
        private List<ME2ImportEntry> imports;
        private List<ME2ExportEntry> exports;

        public IReadOnlyList<string> Names { get { return names; } }
        public IReadOnlyList<IExportEntry> Exports
        {
            get
            {
                return exports;
            }
        }
        public IReadOnlyList<IImportEntry> Imports
        {
            get
            {
                return imports;
            }
        }

        public ME2Package(string path)
        {
            BitConverter.IsLittleEndian = true;
            DebugOutput.PrintLn("Load file : " + path);
            fileName = Path.GetFullPath(path);
            MemoryStream tempStream = new MemoryStream();
            if (!File.Exists(fileName))
                throw new FileNotFoundException("PCC file not found");
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                FileInfo tempInfo = new FileInfo(fileName);
                tempStream.WriteFromStream(fs, tempInfo.Length);
                if (tempStream.Length != tempInfo.Length)
                {
                    throw new FileLoadException("File not fully read in. Try again later");
                }
            }

            tempStream.Seek(12, SeekOrigin.Begin);
            int tempNameSize = tempStream.ReadValueS32();
            tempStream.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerations = tempStream.ReadValueS32();
            tempStream.Seek(36 + tempGenerations * 12, SeekOrigin.Current);
            int tempPos = (int)tempStream.Position;
            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadBytes(tempPos);
            tempStream.Seek(0, SeekOrigin.Begin);

            if (magic != ZBlock.magic && magic.Swap() != ZBlock.magic)
            {
                DebugOutput.PrintLn("Magic number incorrect: " + magic);
                throw new FormatException("This is not a pcc file. The magic number is incorrect.");
            }

            MemoryStream listsStream;
            if (bCompressed)
            {
                DebugOutput.PrintLn("File is compressed");
                {
                    listsStream = CompressionHelper.DecompressPCC(tempStream);

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
        }

        private void ReadNames(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Names...");
            fs.Seek(NameOffset, SeekOrigin.Begin);
            names = new List<string>();
            for (int i = 0; i < NameCount; i++)
            {
                int len = fs.ReadValueS32();
                string s = fs.ReadString((uint)(len - 1));
                fs.Seek(5, SeekOrigin.Current);
                names.Add(s);
            }
        }

        private void ReadImports(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Imports...");
            imports = new List<ME2ImportEntry>();
            fs.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ME2ImportEntry import = new ME2ImportEntry(this, fs.ReadBytes(28));
                imports.Add(import);
            }
        }

        private void ReadExports(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Exports...");
            fs.Seek(ExportOffset, SeekOrigin.Begin);
            exports = new List<ME2ExportEntry>();
            byte[] buffer;
            for (int i = 0; i < ExportCount; i++)
            {
                long start = fs.Position;
                ME2ExportEntry exp = new ME2ExportEntry(this);
                exp.headerOffset = (uint)start;

                fs.Seek(40, SeekOrigin.Current);
                int count = fs.ReadValueS32();
                fs.Seek(4 + count * 12, SeekOrigin.Current);
                count = fs.ReadValueS32();
                fs.Seek(4 + count * 4, SeekOrigin.Current);
                fs.Seek(16, SeekOrigin.Current);
                long end = fs.Position;
                fs.Seek(start, SeekOrigin.Begin);
                exp.header = fs.ReadBytes((int)(end - start));
                buffer = new byte[exp.DataSize];
                fs.Seek(exp.DataOffset, SeekOrigin.Begin);
                fs.Read(buffer, 0, buffer.Length);
                exp.Data = buffer;
                exp.hasChanged = false;
                exports.Add(exp);
                fs.Seek(end, SeekOrigin.Begin);

                if (LastExport == null || exp.DataOffset > LastExport.DataOffset)
                    LastExport = exp;
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
                appendSave(path);
            }
        }

        /// <summary>
        ///     save PCCObject to file by reconstruction from data
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void saveByReconstructing(string path)
        {
            try
            {
                this.bCompressed = false;
                MemoryStream m = new MemoryStream();
                m.WriteBytes(header);

                //Set numblocks to zero
                m.WriteValueS32(0);
                //Write the magic number
                m.WriteValueS32(1026281201);
                //Write 8 bytes of 0
                m.WriteValueS32(0);
                m.WriteValueS32(0);

                //name table
                NameOffset = (int)m.Position;
                NameCount = names.Count;
                foreach (string name in names)
                {
                    m.WriteValueS32(name.Length + 1);
                    m.WriteString(name);
                    m.WriteByte(0);
                    m.WriteValueS32(-14);
                }
                //import table
                ImportOffset = (int)m.Position;
                ImportCount = imports.Count;
                foreach (ME2ImportEntry e in imports)
                {
                    m.WriteBytes(e.header);
                }
                //export table
                ExportOffset = (int)m.Position;
                ExportCount = exports.Count;
                for (int i = 0; i < exports.Count; i++)
                {
                    ME2ExportEntry e = exports[i];
                    e.headerOffset = (uint)m.Position;
                    m.WriteBytes(e.header);
                }
                //freezone
                int FreeZoneSize = expDataBegOffset - FreeZoneStart;
                FreeZoneStart = (int)m.Position;
                m.Write(new byte[FreeZoneSize], 0, FreeZoneSize);
                expDataBegOffset = (int)m.Position;
                //export data
                for (int i = 0; i < exports.Count; i++)
                {
                    ME2ExportEntry e = exports[i];
                    e.DataOffset = (int)m.Position;
                    e.DataSize = e.Data.Length;
                    m.WriteBytes(e.Data);
                    long pos = m.Position;
                    m.Seek(e.headerOffset + 32, SeekOrigin.Begin);
                    m.WriteValueS32(e.DataSize);
                    m.WriteValueS32(e.DataOffset);
                    m.Seek(pos, SeekOrigin.Begin);
                }
                //update header
                m.Seek(0, SeekOrigin.Begin);
                m.WriteBytes(header);

                File.WriteAllBytes(path, m.ToArray());
            }
            catch (Exception ex)
            {
                MessageBox.Show("PCC Save error:\n" + ex.Message);
            }
        }

        /// <summary>
        /// This method is an alternate way of saving PCCs
        /// Instead of reconstructing the PCC from the data taken, it instead copies across the existing
        /// data, appends new exports, updates the export list, changes the namelist location and updates the
        /// value in the header
        /// </summary>
        /// <param name="newFileName">The filename to write to</param>
        /// <param name="attemptOverwrite">Do you wish to attempt to overwrite the existing export</param>
        public string appendSave(string newFileName, bool attemptOverwrite = true, int HeaderNameOffset = 34)
        {
            string rtValues = "";
            string loc = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);


            //Get info
            int expInfoEndOffset = ExportOffset + exports.Sum(export => export.header.Length);
            if (expDataBegOffset < expInfoEndOffset)
                expDataBegOffset = expInfoEndOffset;
            List<ME2ExportEntry> unchangedExports = exports.Where(export => !export.hasChanged).ToList();
            List<ME2ExportEntry> changedExports;
            List<ME2ExportEntry> replaceExports = null;
            if (!attemptOverwrite)
            {
                //If not trying to overwrite, then select all exports that have been changed
                changedExports = exports.Where(export => export.hasChanged).ToList();
            }
            else
            {
                //If we are trying to overwrite, then split up the exports that have been changed that can and can't overwrite the originals
                changedExports = exports.Where(export => export.hasChanged && export.Data.Length > export.DataSize).ToList();
                replaceExports = exports.Where(export => export.hasChanged && export.Data.Length <= export.DataSize).ToList();
            }
            int max = exports.Max(maxExport => maxExport.DataOffset);
            ME2ExportEntry lastExport = exports.Find(export => export.DataOffset == max);
            int lastDataOffset = lastExport.DataOffset + lastExport.DataSize;

            if (!attemptOverwrite)
            {
                int offset = ExportOffset;
                foreach (ME2ExportEntry export in exports)
                {
                    if (!export.hasChanged)
                    {
                        offset += export.header.Length;
                    }
                    else
                        break;
                }
                rtValues += offset + " ";
                using (FileStream stream = new FileStream(loc + "\\exec\\infoCache.bin", FileMode.Append))
                {
                    stream.Seek(0, SeekOrigin.End);
                    rtValues += stream.Position + " ";
                    //throw new FileNotFoundException();
                    stream.Write(changedExports[0].header, 32, 8);
                }
            }


            byte[] oldPCC = new byte[lastDataOffset];//Check whether compressed
            using (FileStream oldPccStream = new FileStream(fileName, FileMode.Open))
            {
                if (bCompressed)
                {
                    oldPCC = CompressionHelper.DecompressPCC(oldPccStream).ToArray().Take(lastDataOffset).ToArray();
                }
                else
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
                    foreach (ME2ExportEntry export in changedExports)
                    {
                        export.DataOffset = (int)newPCCStream.Position;
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                }
                else
                {
                    //If we are then move to each offset and overwrite the data with the new exports
                    foreach (ME2ExportEntry export in replaceExports)
                    {
                        //newPCCStream.Position = export.DataOffset;
                        newPCCStream.Seek(export.DataOffset, SeekOrigin.Begin);
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                    //Then move to the end and append the new data
                    //newPCCStream.Position = lastDataOffset;
                    newPCCStream.Seek(lastDataOffset, SeekOrigin.Begin);

                    foreach (ME2ExportEntry export in changedExports)
                    {
                        export.DataOffset = (int)newPCCStream.Position;
                        export.DataSize = export.Data.Length;
                        newPCCStream.Write(export.Data, 0, export.Data.Length);
                    }
                }
                //Set the new nameoffset and namecounts
                NameOffset = (int)newPCCStream.Position;
                NameCount = names.Count;
                //Then write out the namelist
                foreach (string name in names)
                {
                    newPCCStream.WriteValueS32(name.Length + 1);
                    newPCCStream.WriteString(name);
                    newPCCStream.WriteByte(0);
                    newPCCStream.WriteValueS32(-14);
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
                foreach (ME2ImportEntry import in imports)
                {
                    newPCCStream.Write(import.header, 0, import.header.Length);
                }

                //Finally, update the export list
                newPCCStream.Seek(ExportOffset, SeekOrigin.Begin);
                foreach (ME2ExportEntry export in exports)
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

        /// <summary>
        ///     gets Export or Import entry
        /// </summary>
        /// <param name="index">unreal index</param>
        public IEntry getEntry(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1];
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1];
            return null;
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

        public string getNameEntry(int Index)
        {
            string s = "";
            if (isName(Index))
                s = names[Index];
            return s;
        }

        public string getObjectName(int index)
        {
            if (index > 0 && index < ExportCount)
                return exports[index - 1].ObjectName;
            if (index * -1 > 0 && index * -1 < ImportCount)
                return imports[index * -1 - 1].ObjectName;
            return "";
        }

        public string getClassName(int index)
        {
            string s = "";
            if (index > 0)
            {
                s = names[exports[index - 1].idxObjectName];
            }
            if (index < 0)
            {
                s = names[imports[index * -1 - 1].idxObjectName];
            }
            if (index == 0)
            {
                s = "Class";
            }
            return s;
        }

        public string getObjectClass(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1].ClassName;
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1].ClassName;
            return "";
        }

        public int FindNameOrAdd(string newName)
        {
            int nameID = 0;
            //First check if name already exists
            for (int i = 0; i < NameCount; i++)
            {
                if (names[i] == newName)
                {
                    nameID = i;
                    return nameID;
                }
            }

            names.Add(newName);
            NameCount++;
            return names.Count - 1;
        }

        public void addName(string name)
        {
            if (!names.Contains(name))
            {
                names.Add(name);
                NameCount++;
            }
        }

        public void addImport(IImportEntry importEntry)
        {
            if (importEntry is ME2ImportEntry)
            {
                addImport(importEntry as ME2ImportEntry);
            }
            else
            {
                throw new FormatException("Cannot add import to an ME2 package that is not from ME2");
            }
        }

        public void addImport(ME2ImportEntry importEntry)
        {
            if (importEntry.fileRef2 != this)
                throw new Exception("you cannot add a new import entry from another pcc file, it has invalid references!");

            imports.Add(importEntry);
            ImportCount = imports.Count;
        }

        public void addExport(IExportEntry exportEntry)
        {
            if (exportEntry is ME2ExportEntry)
            {
                addExport(exportEntry as ME2ExportEntry);
            }
            else
            {
                throw new FormatException("Cannot add export to an ME2 package that is not from ME2");
            }
        }

        public void addExport(ME2ExportEntry exportEntry)
        {
            if (exportEntry.fileRef2 != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.hasChanged = true;

            //changing data offset in order to append it at the end of the file
            int maxOffset = exports.Max(entry => entry.DataOffset);
            ME2ExportEntry lastExport = exports.Find(export => export.DataOffset == maxOffset);
            int lastOffset = lastExport.DataOffset + lastExport.Data.Length;
            exportEntry.DataOffset = lastOffset;

            exports.Add(exportEntry);
            ExportCount = exports.Count;
        }

        /// <summary>
        /// Checks whether a name exists in the PCC and returns its index
        /// If it doesn't exist returns -1
        /// </summary>
        /// <param name="nameToFind">The name of the string to find</param>
        /// <returns></returns>
        public int findName(string nameToFind)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Compare(nameToFind, getNameEntry(i)) == 0)
                    return i;
            }
            return -1;
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
                    System.Diagnostics.Process.Start("http://me3explorer.freeforums.org/research-how-to-turn-your-dlc-pcc-into-a-vanilla-one-t2264.html");
                }
                return false;
            }
            return true;
        }

        public IExportEntry getExport(int index)
        {
            return exports[index];
        }

        public IImportEntry getImport(int index)
        {
            return imports[index];
        }

        public void setNames(List<string> list)
        {
            names = list;
        }
    }
}
