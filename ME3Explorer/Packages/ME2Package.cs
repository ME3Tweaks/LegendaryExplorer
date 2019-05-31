using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Gibbed.IO;

namespace ME3Explorer.Packages
{
    public sealed class ME2Package : MEPackage, IMEPackage
    {
        const uint packageTag = 0x9E2A83C1;

        public MEGame Game => MEGame.ME2;

        public override int NameCount
        {
            get => BitConverter.ToInt32(header, nameSize + 20);
            protected set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 20, sizeof(int));
        }
        public int NameOffset
        {
            get => BitConverter.ToInt32(header, nameSize + 24);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 24, sizeof(int));
        }
        public override int ExportCount
        {
            get => BitConverter.ToInt32(header, nameSize + 28);
            protected set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 28, sizeof(int));
        }
        public int ExportOffset
        {
            get => BitConverter.ToInt32(header, nameSize + 32);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 32, sizeof(int));
        }
        public override int ImportCount
        {
            get => BitConverter.ToInt32(header, nameSize + 36);
            protected set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 36, sizeof(int));
        }
        public int ImportOffset
        {
            get => BitConverter.ToInt32(header, nameSize + 40);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 40, sizeof(int));
        }
        private int FreeZoneStart
        {
            get => BitConverter.ToInt32(header, nameSize + 44);
            set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 44, sizeof(int));
        }
        private int Generations => BitConverter.ToInt32(header, nameSize + 64);
        private int Compression
        {
            get => BitConverter.ToInt32(header, header.Length - 4);
            set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, header.Length - 4, sizeof(int));
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
            //Debug.WriteLine(" >> Opening me2 package " + path);
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"ME2Package {Path.GetFileName(path)}", new WeakReference(this));

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
            tempStream.Seek(32 + tempGenerations * 12, SeekOrigin.Current);
            tempStream.ReadValueU32(); //Compression Type. We read this from header[] in MEPackage.cs intead when accessing value
            int tempPos = (int)tempStream.Position;
            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadBytes(tempPos);
            tempStream.Seek(0, SeekOrigin.Begin);

            if (magic != packageTag)
            {
                throw new FormatException("This is not a pcc file. The magic number is incorrect.");
            }

            MemoryStream listsStream;
            if (IsCompressed)
            {
                //Aquadran: Code to decompress package on disk.
                //Do not set the decompressed flag as some tools use this flag
                //to determine if the file on disk is still compressed or not
                //e.g. soundplorer's offset based audio access
                listsStream = CompressionHelper.DecompressME1orME2(tempStream);

                //Correct the header
                //IsCompressed = false; // DO NOT MARK FILE AS DECOMPRESSED AS THIS WILL CORRUPT FILES ON SAVE
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
            else
            {
                listsStream = tempStream;
            }

            names = new List<string>();
            listsStream.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                int len = listsStream.ReadValueS32();
                string s = listsStream.ReadString((uint)(len - 1));
                //skipping irrelevant data
                listsStream.Seek(5, SeekOrigin.Current);
                names.Add(s);
            }

            imports = new List<ImportEntry>();
            listsStream.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ImportEntry import = new ImportEntry(this, listsStream);
                import.Index = i;
                import.PropertyChanged += importChanged;
                imports.Add(import);
            }

            exports = new List<IExportEntry>();
            listsStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                ME2ExportEntry exp = new ME2ExportEntry(this, listsStream);
                exp.Index = i;
                exp.PropertyChanged += exportChanged;
                exports.Add(exp);
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
                //m.Seek(-4, SeekOrigin.Current);
                //m.WriteByte((byte)CompressionType.None); //Write header compression type to None
                //Set numblocks to zero
                m.WriteValueS32(0);
                //Write the magic number
                m.WriteValueS32(1026281201);
                //Write 8 bytes of 0
                m.WriteValueS64(0);

                //name table
                NameOffset = (int)m.Position;
                NameCount = names.Count;
                foreach (string name in names)
                {
                    m.WriteStringASCII(name);
                    m.WriteValueS32(-14);
                }
                //import table
                ImportOffset = (int)m.Position;
                ImportCount = imports.Count;
                foreach (ImportEntry e in imports)
                {
                    m.WriteBytes(e.Header);
                }
                //export table
                ExportOffset = (int)m.Position;
                ExportCount = exports.Count;
                for (int i = 0; i < exports.Count; i++)
                {
                    IExportEntry e = exports[i];
                    e.HeaderOffset = (uint)m.Position;
                    m.WriteBytes(e.Header);
                }
                //freezone
                int FreeZoneSize = expDataBegOffset - FreeZoneStart;
                FreeZoneStart = (int)m.Position;
                m.Write(new byte[FreeZoneSize], 0, FreeZoneSize);
                expDataBegOffset = (int)m.Position;
                //export data
                for (int i = 0; i < exports.Count; i++)
                {
                    IExportEntry e = exports[i];
                    e.DataOffset = (int)m.Position;
                    e.DataSize = e.Data.Length;
                    m.WriteBytes(e.Data);
                    long pos = m.Position;
                    m.Seek(e.HeaderOffset + 32, SeekOrigin.Begin);
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
            IEnumerable<IExportEntry> replaceExports;
            IEnumerable<IExportEntry> appendExports;

            int lastDataOffset;
            int max;
            if (IsAppend)
            {
                replaceExports = exports.Where(export => export.DataChanged && export.DataOffset < NameOffset && export.DataSize <= export.OriginalDataSize);
                appendExports = exports.Where(export => export.DataOffset > NameOffset || (export.DataChanged && export.DataSize > export.OriginalDataSize));
                var expsBeforeNameOffset = exports.Where(exp => exp.DataOffset < NameOffset).ToList();
                if (expsBeforeNameOffset.Count > 0)
                {
                    max = expsBeforeNameOffset.Max(e => e.DataOffset);
                }
                else
                {
                    max = exports.Max(maxExport => maxExport.DataOffset);
                }
            }
            else
            {
                IEnumerable<IExportEntry> changedExports;
                changedExports = exports.Where(export => export.DataChanged);
                replaceExports = changedExports.Where(export => export.DataSize <= export.OriginalDataSize);
                appendExports = changedExports.Except(replaceExports);
                max = exports.Max(maxExport => maxExport.DataOffset);
            }

            IExportEntry lastExport = exports.Find(export => export.DataOffset == max);
            lastDataOffset = lastExport.DataOffset + lastExport.DataSize;

            byte[] oldPCC = new byte[lastDataOffset];//Check whether compressed
            if (IsCompressed)
            {
                //Aquadran: Code to decompress package on disk.
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
                    newPCCStream.WriteBytes(import.Header);
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
                    newPCCStream.WriteBytes(export.Header);
                }

                IsAppend = true;

                //write the updated header
                newPCCStream.Seek(0, SeekOrigin.Begin);
                newPCCStream.WriteBytes(header);
                newPCCStream.Seek(-4, SeekOrigin.Current);
                newPCCStream.WriteBytes(BitConverter.GetBytes((int)CompressionType.None));
            }
            AfterSave();
        }
    }
}
