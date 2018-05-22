using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Diagnostics;
using ME3Explorer.Unreal;
using System.Windows;

namespace ME3Explorer.Packages
{
    public sealed class ME3Package : MEPackage, IMEPackage
    {
        public MEGame Game { get { return MEGame.ME3; } }

        static int headerSize = 0x8E;

        private int idxOffsets { get { if ((flags & 8) != 0) return 24 + nameSize; else return 20 + nameSize; } } // usually = 34
        public override int NameCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets); }
            protected set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 68, sizeof(int));
            }
        }
        int NameOffset
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 4); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 100, sizeof(int));
            }
        }
        public override int ExportCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 8); }
            protected set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 64, sizeof(int));
            }
        }
        int ExportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int)); } }
        public override int ImportCount { get { return BitConverter.ToInt32(header, idxOffsets + 16); } protected set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 20); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int)); } }
        public int DependsOffset { get { return BitConverter.ToInt32(header, idxOffsets + 24); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(int)); } }

        int FreeZoneStart { get { return BitConverter.ToInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(uint)); } }
        int FreeZoneEnd { get { return BitConverter.ToInt32(header, idxOffsets + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(uint)); } }


        static bool isInitialized;
        public static Func<string, ME3Package> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(ME3Package) + " can only be initialized once");
            }
            else
            {
                isInitialized = true;
                return f => new ME3Package(f);
            }
        }

        /// <summary>
        ///     PCCObject class constructor. It also loads namelist, importlist, exportinfo, and exportdata from pcc file
        /// </summary>
        /// <param name="pccFilePath">full path + file name of desired pcc file.</param>
        private ME3Package(string pccFilePath)
        {
            FileName = Path.GetFullPath(pccFilePath);
            MemoryStream inStream;
            using (FileStream pccStream = File.OpenRead(FileName))
            {
                header = pccStream.ReadBytes(headerSize);
                if (magic != ZBlock.magic &&
                    magic.Swap() != ZBlock.magic)
                {
                    throw new FormatException("Not an Unreal package!");
                }

                if (lowVers != 684 && highVers != 194)
                {
                    throw new FormatException("Not an ME3 Package!");
                }
                
                if (IsCompressed)
                {
                    inStream = CompressionHelper.DecompressME3(pccStream);

                    //read uncompressed header
                    inStream.Seek(0, SeekOrigin.Begin);
                    inStream.Read(header, 0, header.Length);
                }
                else
                {
                    inStream = new MemoryStream();
                    pccStream.Seek(0, SeekOrigin.Begin);
                    pccStream.CopyTo(inStream);
                }
            }
            
            names = new List<string>();
            inStream.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                int strLength = inStream.ReadValueS32();
                string str = inStream.ReadString(strLength * -2, true, Encoding.Unicode);
                names.Add(str);
            }
            
            imports = new List<ImportEntry>();
            inStream.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ImportEntry imp = new ImportEntry(this, inStream);
                imp.Index = i;
                imp.PropertyChanged += importChanged;
                imports.Add(imp);
            }
            
            exports = new List<IExportEntry>();
            inStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                ME3ExportEntry e = new ME3ExportEntry(this, inStream);
                e.Index = i;
                e.PropertyChanged += exportChanged;
                exports.Add(e);
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
                IsCompressed = false;
                MemoryStream m = new MemoryStream();
                m.WriteBytes(header);
                //name table
                NameOffset = (int)m.Position;
                NameCount = names.Count;
                foreach (string s in names)
                {
                    m.WriteStringUnicode(s);
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
                foreach (IExportEntry e in exports)
                {
                    e.headerOffset = (uint)m.Position;
                    m.WriteBytes(e.header);
                }
                //freezone
                int FreeZoneSize = FreeZoneEnd - FreeZoneStart;
                FreeZoneStart = (int)m.Position;
                m.WriteBytes(new byte[FreeZoneSize]);
                FreeZoneEnd = expDataBegOffset = (int)m.Position;
                //export data
                foreach (IExportEntry e in exports)
                {
                    e.DataOffset = (int)m.Position;
                    e.DataSize = e.Data.Length;

                    UpdateOffsets(e);

                    m.WriteBytes(e.Data);
                    //update size and offset in already-written header
                    long pos = m.Position;
                    m.Seek(e.headerOffset + 32, SeekOrigin.Begin);
                    m.WriteValueS32(e.DataSize);
                    m.WriteValueS32(e.DataOffset);
                    m.Seek(pos, SeekOrigin.Begin);
                }
                
                //update header
                m.Seek(0, SeekOrigin.Begin);
                m.WriteBytes(header);

                if (compress)
                {
                    CompressionHelper.CompressAndSave(m, path);
                }
                else
                {
                    File.WriteAllBytes(path, m.ToArray());
                }
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
        /// data, appends the name list and import list, appends changed and new exports, and then appends the export list.
        /// Changed exports with the same datasize or smaller are updaed in place.
        /// </summary>
        /// <param name="newFileName">The filename to write to</param>
        private void appendSave(string newFileName)
        {
            IEnumerable<IExportEntry> replaceExports;
            IEnumerable<IExportEntry> appendExports;

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
                IEnumerable<IExportEntry> changedExports;
                changedExports = exports.Where(export => export.DataChanged);
                replaceExports = changedExports.Where(export => export.DataSize <= export.OriginalDataSize);
                appendExports = changedExports.Except(replaceExports);
                max = exports.Max(maxExport => maxExport.DataOffset);
            }

            IExportEntry lastExport = exports.Find(export => export.DataOffset == max);
            lastDataOffset = lastExport.DataOffset + lastExport.DataSize;

            byte[] oldPCC = new byte[lastDataOffset];
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
                foreach (IExportEntry export in replaceExports)
                {
                    newPCCStream.Seek(export.DataOffset, SeekOrigin.Begin);
                    export.DataSize = export.Data.Length;
                    newPCCStream.WriteBytes(export.Data);
                }

                newPCCStream.Seek(lastDataOffset, SeekOrigin.Begin);
                //Set the new nameoffset and namecounts
                NameOffset = (int)newPCCStream.Position;
                NameCount = names.Count;
                //Write out the namelist
                foreach (string name in names)
                {
                    newPCCStream.WriteValueS32(-(name.Length + 1));
                    newPCCStream.WriteString(name + "\0", (uint)(name.Length + 1) * 2, Encoding.Unicode);
                }

                //Write the import list
                ImportOffset = (int)newPCCStream.Position;
                ImportCount = imports.Count;
                foreach (ImportEntry import in imports)
                {
                    newPCCStream.WriteBytes(import.header);
                }

                //Append the new data
                foreach (IExportEntry export in appendExports)
                {
                    export.DataOffset = (int)newPCCStream.Position;
                    export.DataSize = export.Data.Length;
                    UpdateOffsets(export);

                    newPCCStream.WriteBytes(export.Data);
                }

                //Write the export list
                ExportOffset = (int)newPCCStream.Position;
                ExportCount = exports.Count;
                foreach (ME3ExportEntry export in exports)
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

        private static void UpdateOffsets(IExportEntry export)
        {
            //update offsets for pcc-stored audio in wwisestreams
            if (export.ClassName == "WwiseStream" && export.GetProperty<NameProperty>("Filename") == null)
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(12, BitConverter.GetBytes(export.DataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored movies in texturemovies
            if (export.ClassName == "TextureMovie" && export.GetProperty<NameProperty>("TextureFileCacheName") == null)
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(12, BitConverter.GetBytes(export.DataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored mips in Textures
            else if (export.ClassName == "Texture2D" || export.ClassName == "LightMapTexture2D" || export.ClassName == "TextureFlipBook")
            {
                int baseOffset = export.DataOffset + export.propsEnd();
                MemoryStream binData = new MemoryStream(export.getBinaryData());
                for (int i = binData.ReadValueS32(); i > 0 && binData.Position < binData.Length; i--)
                {
                    if (binData.ReadValueS32() == 0) //pcc-stored
                    {
                        int uncompressedSize = binData.ReadValueS32();
                        binData.Seek(4, SeekOrigin.Current); //skip compressed size
                        binData.WriteValueS32(baseOffset + (int)binData.Position + 4);//update offset
                        binData.Seek(uncompressedSize + 8, SeekOrigin.Current); //skip texture and width + height values
                    }
                    else
                    {
                        binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
                    }
                }
                export.setBinaryData(binData.ToArray());
            }
        }
    }
}