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
    public sealed class ME2Package : MEPackage, IMEPackage
    {
        public MEGame Game { get { return MEGame.ME2; } }

        public override bool IsModified
        {
            get
            {
                return exports.Any(entry => entry.DataChanged == true) || imports.Any(entry => entry.HeaderChanged == true) || namesAdded > 0;
            }
        }
        public bool CanReconstruct { get { return !exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"); } }

        protected override int NameCount { get { return BitConverter.ToInt32(header, nameSize + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 20, sizeof(int)); } }
        private int NameOffset { get { return BitConverter.ToInt32(header, nameSize + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 24, sizeof(int)); } }
        public int ExportCount { get { return BitConverter.ToInt32(header, nameSize + 28); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 28, sizeof(int)); } }
        private int ExportOffset { get { return BitConverter.ToInt32(header, nameSize + 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 32, sizeof(int)); } }
        public override int ImportCount { get { return BitConverter.ToInt32(header, nameSize + 36); } protected set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 36, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, nameSize + 40); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 40, sizeof(int)); } }
        private int FreeZoneStart { get { return BitConverter.ToInt32(header, nameSize + 44); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 44, sizeof(int)); } }
        private int Generations { get { return BitConverter.ToInt32(header, nameSize + 64); } }
        private int Compression { get { return BitConverter.ToInt32(header, header.Length - 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, header.Length - 4, sizeof(int)); } }
        
        private List<ME2ExportEntry> exports;
        
        public IReadOnlyList<IExportEntry> Exports
        {
            get
            {
                return exports;
            }
        }

        static bool isInitialized;
        public static Func<string, ME2Package> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(ME2Package) + " can only be initialized once");
            }
            else
            {
                isInitialized = true;
                return f => new ME2Package(f);
            }
        }

        private ME2Package(string path)
        {
            
            DebugOutput.PrintLn("Load file : " + path);
            FileName = Path.GetFullPath(path);
            MemoryStream tempStream = new MemoryStream();
            if (!File.Exists(FileName))
                throw new FileNotFoundException("PCC file not found");
            using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            {
                FileInfo tempInfo = new FileInfo(FileName);
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
            if (IsCompressed)
            {
                DebugOutput.PrintLn("File is compressed");
                {
                    listsStream = CompressionHelper.DecompressME1orME2(tempStream);

                    //Correct the header
                    IsCompressed = false;
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

        void ReadNames(MemoryStream fs)
        {
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

        void ReadImports(MemoryStream fs)
        {
            imports = new List<ImportEntry>();
            fs.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ImportEntry import = new ImportEntry(this, fs);
                import.Index = i;
                import.PropertyChanged += importChanged;
                imports.Add(import);
            }
        }

        void ReadExports(MemoryStream fs)
        {
            fs.Seek(ExportOffset, SeekOrigin.Begin);
            exports = new List<ME2ExportEntry>();
            byte[] buffer;
            for (int i = 0; i < ExportCount; i++)
            {
                long start = fs.Position;

                fs.Seek(40, SeekOrigin.Current);
                int count = fs.ReadValueS32();
                fs.Seek(4 + count * 12, SeekOrigin.Current);
                count = fs.ReadValueS32();
                fs.Seek(4 + count * 4, SeekOrigin.Current);
                fs.Seek(16, SeekOrigin.Current);
                long end = fs.Position;
                fs.Seek(start, SeekOrigin.Begin);

                ME2ExportEntry exp = new ME2ExportEntry(this, fs.ReadBytes((int)(end - start)), (uint)start);
                buffer = new byte[exp.DataSize];
                fs.Seek(exp.DataOffset, SeekOrigin.Begin);
                fs.Read(buffer, 0, buffer.Length);
                exp.Data = buffer;
                exp.DataChanged = false;
                exp.Index = i;
                exp.PropertyChanged += exportChanged;
                exports.Add(exp);
                fs.Seek(end, SeekOrigin.Begin);
            }
        }

        /// <summary>
        ///     save PCC to same file by reconstruction if possible, append if not
        /// </summary>
        public void save()
        {
            save(FileName);
        }

        /// <summary>
        ///     save PCC by reconstruction if possible, append if not
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void save(string path)
        {
            if (CanReconstruct)
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
                this.IsCompressed = false;
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
                foreach (ImportEntry e in imports)
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
                AfterSave();
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
        /// 
        public void appendSave(string newFileName)
        {
            IEnumerable<ME2ExportEntry> replaceExports;
            IEnumerable<ME2ExportEntry> appendExports;

            int lastDataOffset;
            int max;
            if (IsAppend)
            {
                replaceExports = exports.Where(export => export.DataChanged && export.DataOffset < NameOffset && export.DataSize <= export.OriginalDataSize);
                appendExports = exports.Where(export => export.DataOffset > NameOffset || (export.DataChanged && export.DataSize > export.OriginalDataSize));
                max = exports.Where(exp => exp.DataOffset < NameOffset).Max(e => e.DataOffset);
            }
            else
            {
                IEnumerable<ME2ExportEntry> changedExports;
                changedExports = exports.Where(export => export.DataChanged);
                replaceExports = changedExports.Where(export => export.DataSize <= export.OriginalDataSize);
                appendExports = changedExports.Except(replaceExports);
                max = exports.Max(maxExport => maxExport.DataOffset);
            }

            ME2ExportEntry lastExport = exports.Find(export => export.DataOffset == max);
            lastDataOffset = lastExport.DataOffset + lastExport.DataSize;

            byte[] oldPCC = new byte[lastDataOffset];//Check whether compressed
            if (IsCompressed)
            {
                oldPCC = CompressionHelper.Decompress(FileName).Take(lastDataOffset).ToArray();
                IsCompressed = false;
            }
            else
            {
                using (FileStream oldPccStream = new FileStream(this.FileName, FileMode.Open))
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

                //write the in-place export updates
                foreach (ME2ExportEntry export in replaceExports)
                {
                    newPCCStream.Seek(export.DataOffset, SeekOrigin.Begin);
                    export.DataSize = export.Data.Length;
                    newPCCStream.WriteBytes(export.Data);
                }

                
                newPCCStream.Seek(lastDataOffset, SeekOrigin.Begin);
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

                //Write the import list
                ImportOffset = (int)newPCCStream.Position;
                ImportCount = imports.Count;
                foreach (ImportEntry import in imports)
                {
                    newPCCStream.WriteBytes(import.header);
                }

                //append the new data
                foreach (ME2ExportEntry export in appendExports)
                {
                    export.DataOffset = (int)newPCCStream.Position;
                    export.DataSize = export.Data.Length;
                    newPCCStream.Write(export.Data, 0, export.Data.Length);
                }
                
                //Write the export list
                ExportOffset = (int)newPCCStream.Position;
                ExportCount = exports.Count;
                foreach (ME2ExportEntry export in exports)
                {
                    newPCCStream.WriteBytes(export.header);
                }

                IsAppend = true;

                //write the updated header
                newPCCStream.Seek(0, SeekOrigin.Begin);
                newPCCStream.WriteBytes(header);
            }
            AfterSave();
        }

        protected override void AfterSave()
        {
            base.AfterSave();
            foreach (var export in exports)
            {
                export.DataChanged = false;
            }
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

        public bool isExport(int Index)
        {
            return (Index >= 0 && Index < ExportCount);
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
            if (exportEntry.FileRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.DataChanged = true;
            exportEntry.Index = exports.Count;
            exportEntry.PropertyChanged += exportChanged;
            exports.Add(exportEntry);
            ExportCount = exports.Count;

            updateTools(PackageChange.ExportAdd, ExportCount);
        }

        public IExportEntry getExport(int index)
        {
            return exports[index];
        }
    }
}
