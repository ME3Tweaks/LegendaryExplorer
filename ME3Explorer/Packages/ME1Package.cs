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
    public sealed class ME1Package : MEPackage, IMEPackage
    {
        public MEGame Game { get { return MEGame.ME1;} }

        public override bool IsModified
        {
            get
            {
                return exports.Any(entry => entry.DataChanged == true) || imports.Any(entry => entry.HeaderChanged == true || namesAdded > 0);
            }
        }
        public bool CanReconstruct { get { return !exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"); } }

        protected override int NameCount { get { return BitConverter.ToInt32(header, nameSize + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 20, sizeof(int)); } }
        private int NameOffset { get { return BitConverter.ToInt32(header, nameSize + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 24, sizeof(int)); } }
        public int ExportCount { get { return BitConverter.ToInt32(header, nameSize + 28); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 28, sizeof(int)); } }
        private int ExportOffset { get { return BitConverter.ToInt32(header, nameSize + 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 32, sizeof(int)); } }
        public int ImportCount { get { return BitConverter.ToInt32(header, nameSize + 36); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 36, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, nameSize + 40); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 40, sizeof(int)); } }
        private int FreeZoneStart { get { return BitConverter.ToInt32(header, nameSize + 44); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 44, sizeof(int)); } }
        private int Generations { get { return BitConverter.ToInt32(header, nameSize + 64); } }
        private int Compression { get { return BitConverter.ToInt32(header, header.Length - 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, header.Length - 4, sizeof(int)); } }
        
        private List<ME1ImportEntry> imports;
        private List<ME1ExportEntry> exports;
        
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

        static bool isInitialized;
        public static Func<string, ME1Package> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(ME1Package) + " can only be initialized once");
            }
            else
            {
                isInitialized = true;
                return f => new ME1Package(f);
            }
        }

        private ME1Package(string path)
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
            int tempPos = (int)tempStream.Position + 4;
            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadBytes(tempPos);
            tempStream.Seek(0, SeekOrigin.Begin);

            if (magic != ZBlock.magic && magic.Swap() != ZBlock.magic)
            {
                DebugOutput.PrintLn("Magic number incorrect: " + magic);
                throw new FormatException("This is not an ME1 Package file. The magic number is incorrect.");
            }
            MemoryStream listsStream;
            if (IsCompressed)
            {
                DebugOutput.PrintLn("File is compressed");
                listsStream = CompressionHelper.DecompressME1orME2(tempStream);

                //Correct the header
                IsCompressed = false;
                listsStream.Seek(0, SeekOrigin.Begin);
                listsStream.WriteBytes(header);

                // Set numblocks to zero
                listsStream.WriteValueS32(0);
                //Write the magic number
                listsStream.WriteBytes(new byte[] { 0xF2, 0x56, 0x1B, 0x4E });
                // Write 4 bytes of 0
                listsStream.WriteValueS32(0);
            }
            else
            {
                DebugOutput.PrintLn("File already decompressed. Reading decompressed data.");
                //listsStream = tempStream;
                listsStream = new MemoryStream();
                tempStream.WriteTo(listsStream);
            }
            tempStream.Dispose();
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
                string s = "";
                if (len > 0)
                {
                    s = fs.ReadString((uint)(len - 1));
                    fs.Seek(9, SeekOrigin.Current);
                }
                else
                {
                    len *= -1;
                    for (int j = 0; j < len - 1; j++)
                    {
                        s += (char)fs.ReadByte();
                        fs.ReadByte();
                    }
                    fs.Seek(10, SeekOrigin.Current);
                }
                names.Add(s);
            }
        }

        private void ReadImports(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Imports...");
            imports = new List<ME1ImportEntry>();
            fs.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ME1ImportEntry import = new ME1ImportEntry(this, fs.ReadBytes(28));
                import.Index = i;
                import.PropertyChanged += importChanged;
                imports.Add(import);
            }
        }

        private void ReadExports(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Exports...");
            fs.Seek(ExportOffset, SeekOrigin.Begin);
            exports = new List<ME1ExportEntry>();
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

                ME1ExportEntry exp = new ME1ExportEntry(this, fs.ReadBytes((int)(end - start)), (uint)start);
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
                throw new Exception("Cannot save ME1 packages with a SeekFreeShaderCache. Please make an issue on github: https://github.com/ME3Explorer/ME3Explorer/issues");
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

                // Set numblocks to zero
                m.WriteValueS32(0);
                //Write the magic number (What is this?)
                m.WriteBytes(new byte[] { 0xF2, 0x56, 0x1B, 0x4E });
                // Write 4 bytes of 0
                m.WriteValueS32(0);

                //name table
                NameOffset = (int)m.Position;
                NameCount = names.Count;
                foreach (string name in names)
                {
                    m.WriteValueS32(name.Length + 1);
                    m.WriteString(name);
                    m.WriteByte(0);
                    m.WriteValueS32(0);
                    m.WriteValueS32(458768);
                }
                //import table
                ImportOffset = (int)m.Position;
                ImportCount = imports.Count;
                foreach (ME1ImportEntry e in imports)
                {
                    m.WriteBytes(e.header);
                }
                //export table
                ExportOffset = (int)m.Position;
                ExportCount = exports.Count;
                for (int i = 0; i < exports.Count; i++)
                {
                    ME1ExportEntry e = exports[i];
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
                    ME1ExportEntry e = exports[i];
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

        protected override void AfterSave()
        {
            base.AfterSave();
            foreach (var export in exports)
            {
                export.DataChanged = false;
            }
            foreach (var import in imports)
            {
                import.HeaderChanged = false;
            }
            namesAdded = 0;
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
                return exports[Index - 1].ObjectName;
            if (Index < 0 && isImport(Index * -1 - 1))
                return imports[Index * -1 - 1].ObjectName;
            return "Class";
        }

        public string getObjectName(int p)
        {
            return GetClass(p);
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

        public string getObjectClass(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1].ClassName;
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1].ClassName;
            return "";
        }
        
        public void addImport(IImportEntry importEntry)
        {
            if (importEntry is ME1ImportEntry)
            {
                addImport(importEntry as ME1ImportEntry);
            }
            else
            {
                throw new FormatException("Cannot add import to an ME1 package that is not from ME1");
            }
        }

        public void addImport(ME1ImportEntry importEntry)
        {
            if (importEntry.FileRef != this)
                throw new Exception("you cannot add a new import entry from another file, it has invalid references!");

            importEntry.PropertyChanged += importChanged;
            imports.Add(importEntry);
            ImportCount = imports.Count;

            updateTools(PackageChange.ImportAdd, ImportCount - 1);
        }

        public void addExport(IExportEntry exportEntry)
        {
            if (exportEntry is ME1ExportEntry)
            {
                addExport(exportEntry as ME1ExportEntry);
            }
            else
            {
                throw new FormatException("Cannot add export to an ME1 package that is not from ME1");
            }
        }

        public void addExport(ME1ExportEntry exportEntry)
        {
            if (exportEntry.FileRef != this)
                throw new Exception("you cannot add a new export entry from another file, it has invalid references!");

            exportEntry.DataChanged = true;

            exportEntry.PropertyChanged += exportChanged;
            exports.Add(exportEntry);
            ExportCount = exports.Count;

            updateTools(PackageChange.ExportAdd, ExportCount);
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
