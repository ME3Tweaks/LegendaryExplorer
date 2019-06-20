using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using StreamHelpers;

namespace ME3Explorer.Packages
{
    public sealed class ME1Package : MEPackage, IMEPackage
    {
        const uint packageTag = 0x9E2A83C1;

        public MEGame Game => MEGame.ME1;

        public override int NameCount { get => BitConverter.ToInt32(header, nameSize + 20);
            protected set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 20, sizeof(int));
        }
        public int NameOffset { get => BitConverter.ToInt32(header, nameSize + 24);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 24, sizeof(int));
        }
        public override int ExportCount { get => BitConverter.ToInt32(header, nameSize + 28);
            protected set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 28, sizeof(int));
        }
        public int ExportOffset { get => BitConverter.ToInt32(header, nameSize + 32);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 32, sizeof(int));
        }
        public override int ImportCount { get => BitConverter.ToInt32(header, nameSize + 36);
            protected set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 36, sizeof(int));
        }
        public int ImportOffset { get => BitConverter.ToInt32(header, nameSize + 40);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 40, sizeof(int));
        }
        int FreeZoneStart { get => BitConverter.ToInt32(header, nameSize + 44);
            set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, nameSize + 44, sizeof(int));
        }
        int Generations => BitConverter.ToInt32(header, nameSize + 64);
        int Compression { get => BitConverter.ToInt32(header, header.Length - 4);
            set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, header.Length - 4, sizeof(int));
        }
        public List<ME1Explorer.Unreal.Classes.TalkFile> LocalTalkFiles { get; } = new List<ME1Explorer.Unreal.Classes.TalkFile>();

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
            //Debug.WriteLine(" >> Opening me1 package " + path);
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"ME1Package {Path.GetFileName(path)}", new WeakReference(this));

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
            int tempNameSize = tempStream.ReadInt32();
            tempStream.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerations = tempStream.ReadInt32();
            tempStream.Seek(36 + tempGenerations * 12, SeekOrigin.Current);

            tempStream.ReadUInt32(); //Compression Type. We read this from header[] in MEPackage.cs intead when accessing value
            int tempPos = (int)tempStream.Position;
            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadToBuffer(tempPos);
            tempStream.Seek(0, SeekOrigin.Begin);

            if (magic != packageTag)
            {
                throw new FormatException("This is not an ME1 Package file. The magic number is incorrect.");
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
                IsCompressed = false;
                listsStream.Seek(0, SeekOrigin.Begin);
                listsStream.WriteFromBuffer(header);

                // Set numblocks to zero
                listsStream.WriteInt32(0);
                //Write the magic number
                listsStream.WriteFromBuffer(new byte[] { 0xF2, 0x56, 0x1B, 0x4E });
                // Write 4 bytes of 0
                listsStream.WriteInt32(0);
            }
            else
            {
                //listsStream = tempStream;
                listsStream = new MemoryStream();
                tempStream.WriteTo(listsStream);
            }
            tempStream.Dispose();


            ReadNames(listsStream);
            ReadImports(listsStream);
            ReadExports(listsStream);
            ReadLocalTLKs();
        }

        private void ReadLocalTLKs()
        {
            LocalTalkFiles.Clear();
            List<IExportEntry> tlkFileSets = Exports.Where(x => x.ClassName == "BioTlkFileSet" && !x.ObjectName.StartsWith("Default__")).ToList();
            var exportsToLoad = new List<IExportEntry>();
            foreach(var tlkFileSet in tlkFileSets)
            {
                MemoryStream r = new MemoryStream(tlkFileSet.Data);
                r.Position = tlkFileSet.propsEnd();
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int langRef = r.ReadInt32();
                    r.ReadInt32(); //second half of name
                    string lang = getNameEntry(langRef);
                    int numTlksForLang = r.ReadInt32(); //I beleive this is always 2. Hopefully I am not wrong.
                    int maleTlk = r.ReadInt32();
                    int femaleTlk = r.ReadInt32();

                    if (Properties.Settings.Default.TLKLanguage.Equals(lang,StringComparison.InvariantCultureIgnoreCase))
                    {
                        exportsToLoad.Add(getUExport(Properties.Settings.Default.TLKGender_IsMale ? maleTlk : femaleTlk));
                        break;
                    }

                    //r.ReadInt64();
                    //talkFiles.Add(new TalkFile(pcc, r.ReadInt32(), true, langRef, index));
                    //talkFiles.Add(new TalkFile(pcc, r.ReadInt32(), false, langRef, index));
                }
            }

            foreach(var exp in exportsToLoad)
            {
                Debug.WriteLine("Loading local TLK: " + exp.GetIndexedFullPath);
                LocalTalkFiles.Add(new ME1Explorer.Unreal.Classes.TalkFile(exp));
            }
        }

        private void ReadNames(MemoryStream fs)
        {
            names = new List<string>();
            fs.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                int len = fs.ReadInt32();
                string s = "";
                if (len > 0)
                {
                    s = fs.ReadStringASCIINull(len);
                    fs.Skip(8);
                }
                else
                {
                    len *= -1;
                    for (int j = 0; j < len - 1; j++)
                    {
                        s += (char)fs.ReadByte();
                        fs.ReadByte();
                    }
                    fs.Skip(10);
                }
                names.Add(s);
            }
        }

        private void ReadImports(MemoryStream fs)
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

        private void ReadExports(MemoryStream fs)
        {
            exports = new List<IExportEntry>();
            fs.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                ME1ExportEntry exp = new ME1ExportEntry(this, fs);
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
                throw new Exception($"Cannot save ME1 packages with a SeekFreeShaderCache. Please make an issue on github: {App.BugReportURL}");
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
                m.WriteFromBuffer(header);

                // Set numblocks to zero
                m.WriteInt32(0);
                //Write the magic number (What is this?)
                m.WriteFromBuffer(new byte[] { 0xF2, 0x56, 0x1B, 0x4E });
                // Write 4 bytes of 0
                m.WriteInt32(0);

                //name table
                NameOffset = (int)m.Position;
                NameCount = names.Count;
                foreach (string name in names)
                {
                    m.WriteUnrealStringASCII(name);
                    m.WriteInt32(0);
                    m.WriteInt32(458768);
                }
                //import table
                ImportOffset = (int)m.Position;
                ImportCount = imports.Count;
                foreach (ImportEntry e in imports)
                {
                    m.WriteFromBuffer(e.Header);
                }
                //export table
                ExportOffset = (int)m.Position;
                ExportCount = exports.Count;
                foreach (IExportEntry e in exports)
                {
                    e.HeaderOffset = (uint)m.Position;
                    m.WriteFromBuffer(e.Header);
                }
                //freezone
                int FreeZoneSize = expDataBegOffset - FreeZoneStart;
                FreeZoneStart = (int)m.Position;
                m.Write(new byte[FreeZoneSize], 0, FreeZoneSize);
                expDataBegOffset = (int)m.Position;
                //export data
                foreach (IExportEntry e in exports)
                {
                    e.DataOffset = (int)m.Position;
                    e.DataSize = e.Data.Length;
                    m.WriteFromBuffer(e.Data);
                    long pos = m.Position;
                    m.Seek(e.HeaderOffset + 32, SeekOrigin.Begin);
                    m.WriteInt32(e.DataSize);
                    m.WriteInt32(e.DataOffset);
                    m.Seek(pos, SeekOrigin.Begin);
                }
                //update header
                m.Seek(0, SeekOrigin.Begin);
                m.WriteFromBuffer(header);

                File.WriteAllBytes(path, m.ToArray());
                AfterSave();
            }
            catch (Exception ex)
            {
                MessageBox.Show("PCC Save error:\n" + ex.Message);
            }
        }
    }
}
