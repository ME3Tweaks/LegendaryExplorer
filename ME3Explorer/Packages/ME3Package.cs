using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ME3Explorer.Unreal;
using System.Windows;
using System.Diagnostics;
using StreamHelpers;

namespace ME3Explorer.Packages
{
    [DebuggerDisplay("ME3Package | {" + nameof(FilePath) + "}")]
    public sealed class ME3Package : MEPackage, IMEPackage
    {
        const uint packageTag = 0x9E2A83C1;

        public MEGame Game => MEGame.ME3;

        private const int headerSize = 0x8E;

        private int idxOffsets
        {
            get
            {
                if ((flags & 8) != 0) return 24 + nameSize;
                return 20 + nameSize;
            }
        } // usually = 34

        public override int NameCount
        {
            get => BitConverter.ToInt32(header, idxOffsets);
            protected set
            {
                SetHeaderValue(value, 0);
                SetHeaderValue(value, 68);
            }
        }
        public int NameOffset
        {
            get => BitConverter.ToInt32(header, idxOffsets + 4);
            private set
            {
                SetHeaderValue(value, 4);
                SetHeaderValue(value, 100);
            }
        }
        public override int ExportCount
        {
            get => BitConverter.ToInt32(header, idxOffsets + 8);
            protected set
            {
                SetHeaderValue(value, 8);
                SetHeaderValue(value, 64);
            }
        }
        public int ExportOffset
        {
            get => BitConverter.ToInt32(header, idxOffsets + 12);
            private set => SetHeaderValue(value, 12);
        }
        public override int ImportCount
        {
            get => BitConverter.ToInt32(header, idxOffsets + 16);
            protected set => SetHeaderValue(value, 16);
        }
        public int ImportOffset
        {
            get => BitConverter.ToInt32(header, idxOffsets + 20);
            private set => SetHeaderValue(value, 20);
        }
        public int DependsOffset
        {
            get => BitConverter.ToInt32(header, idxOffsets + 24);
            private set => SetHeaderValue(value, 24);
        }
        int DependencyTableStart
        {
            get => BitConverter.ToInt32(header, idxOffsets + 24);
            set => SetHeaderValue(value, 24);
        }
        int DependencyTableEnd
        {
            get => BitConverter.ToInt32(header, idxOffsets + 28);
            set => SetHeaderValue(value, 28);
        }


        int Generations0ExportCount
        {
            get => BitConverter.ToInt32(header, idxOffsets + 0x40);
            set => SetHeaderValue(value, 0x40);
        }

        public int Generations0NameCount
        {
            get => BitConverter.ToInt32(header, idxOffsets+0x44 );
            set => SetHeaderValue(value, 0x44);
        }

        void SetHeaderValue(int val, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(val), 0, header, idxOffsets + offset, sizeof(int));
        }


        static bool isInitialized;
        public static Func<string, ME3Package> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(ME3Package) + " can only be initialized once");
            }

            isInitialized = true;
            return f => new ME3Package(f);
        }

        /// <summary>
        ///     PCCObject class constructor. It also loads namelist, importlist, exportinfo, and exportdata from pcc file
        /// </summary>
        /// <param name="pccFilePath">full path + file name of desired pcc file.</param>
        private ME3Package(string pccFilePath)
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"ME3Package {Path.GetFileName(pccFilePath)}", new WeakReference(this));

            //Debug.WriteLine(" >> Opening me3 package " + pccFilePath);
            FilePath = Path.GetFullPath(pccFilePath);
            MemoryStream inStream;
            using (FileStream pccStream = File.OpenRead(FilePath))
            {
                header = pccStream.ReadToBuffer(headerSize);
                if (magic != packageTag)
                {
                    throw new FormatException("Not an Unreal package!");
                }

                if (lowVers != 684 && highVers != 194)
                {
                    throw new FormatException("Not an ME3 Package!");
                }
                if (IsCompressed)
                {
                    //Aquadran: Code to decompress package on disk.
                    inStream = CompressionHelper.DecompressME3(pccStream);
                    //read uncompressed header
                    inStream.Seek(0, SeekOrigin.Begin);
                    inStream.Read(header, 0, header.Length); //load uncompressed header
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
                int strLength = inStream.ReadInt32();
                string str = inStream.ReadStringUnicodeNull(strLength * -2);
                names.Add(str);
            }
            imports = new List<ImportEntry>();
            inStream.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ImportEntry imp = new ImportEntry(this, inStream) { Index = i };
                imp.PropertyChanged += importChanged;
                imports.Add(imp);
            }
            exports = new List<IExportEntry>();
            inStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                ME3ExportEntry e = new ME3ExportEntry(this, inStream) { Index = i };
                e.PropertyChanged += exportChanged;
                exports.Add(e);
            }
        }

        /// <summary>
        ///     save PCC to same file by reconstruction
        /// </summary>
        public void save()
        {
            save(FilePath);
        }

        /// <summary>
        ///     save PCC by reconstruction
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void save(string path)
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
                m.WriteFromBuffer(header);
                //name table
                NameOffset = (int)m.Position;
                NameCount = names.Count;
                foreach (string s in names)
                {
                    m.WriteUnrealStringUnicode(s);
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
                int DependencyTableSize = DependencyTableEnd - DependencyTableStart; //Should be ExportsCount * 4, technically.
                DependencyTableStart = (int)m.Position;
                m.WriteFromBuffer(new byte[DependencyTableSize]);
                DependencyTableEnd = expDataBegOffset = (int)m.Position;
                Generations0ExportCount = ExportCount;
                Generations0NameCount = NameCount;
                //export data
                foreach (IExportEntry e in exports)
                {
                    UpdateOffsets(e, (int)m.Position);

                    e.DataOffset = (int)m.Position;
                    e.DataSize = e.Data.Length;


                    m.WriteFromBuffer(e.Data);
                    //update size and offset in already-written header
                    long pos = m.Position;
                    m.Seek(e.HeaderOffset + 32, SeekOrigin.Begin);
                    m.WriteInt32(e.DataSize);
                    m.WriteInt32(e.DataOffset);
                    m.Seek(pos, SeekOrigin.Begin);
                }

                //update header
                m.Seek(0, SeekOrigin.Begin);
                m.WriteFromBuffer(header);

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

        private static void UpdateOffsets(IExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            //update offsets for pcc-stored audio in wwisestreams
            if ((export.ClassName == "WwiseStream" && export.GetProperty<NameProperty>("Filename") == null) || export.ClassName == "WwiseBank")
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(12, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored movies in texturemovies
            if (export.ClassName == "TextureMovie" && export.GetProperty<NameProperty>("TextureFileCacheName") == null)
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(12, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored mips in Textures
            else if (export.ClassName == "Texture2D" || export.ClassName == "LightMapTexture2D" || export.ClassName == "TextureFlipBook")
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream binData = new MemoryStream(export.getBinaryData());
                for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
                {
                    if (binData.ReadInt32() == 0) //pcc-stored
                    {
                        int uncompressedSize = binData.ReadInt32();
                        binData.Seek(4, SeekOrigin.Current); //skip compressed size
                        binData.WriteInt32(baseOffset + (int)binData.Position + 4);//update offset
                        binData.Seek(uncompressedSize + 8, SeekOrigin.Current); //skip texture and width + height values
                    }
                    else
                    {
                        binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
                    }
                }
                export.setBinaryData(binData.ToArray());
            }
            else if (export.ClassName == "ShaderCache")
            {
                int oldDataOffset = export.DataOffset;

                MemoryStream binData = new MemoryStream(export.Data);
                binData.Seek(export.propsEnd() + 1, SeekOrigin.Begin);

                int nameList1Count = binData.ReadInt32();
                binData.Seek(nameList1Count * 12, SeekOrigin.Current);

                int namelist2Count = binData.ReadInt32();//namelist2
                binData.Seek(namelist2Count * 12, SeekOrigin.Current);

                int shaderCount = binData.ReadInt32();
                for (int i = 0; i < shaderCount; i++)
                {
                    binData.Seek(24, SeekOrigin.Current);
                    int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextShaderOffset + newDataOffset);
                    binData.Seek(nextShaderOffset, SeekOrigin.Begin);
                }

                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);

                int materialShaderMapCount = binData.ReadInt32();
                for (int i = 0; i < materialShaderMapCount; i++)
                {
                    binData.Seek(16, SeekOrigin.Current);

                    int switchParamCount = binData.ReadInt32();
                    binData.Seek(switchParamCount * 32, SeekOrigin.Current);

                    int componentMaskParamCount = binData.ReadInt32();
                    binData.Seek(componentMaskParamCount * 44, SeekOrigin.Current);

                    int normalParams = binData.ReadInt32();
                    binData.Seek(normalParams * 29, SeekOrigin.Current);

                    binData.Seek(8, SeekOrigin.Current);

                    int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextMaterialShaderMapOffset + newDataOffset);
                    binData.Seek(nextMaterialShaderMapOffset, SeekOrigin.Begin);
                }

                export.Data = binData.ToArray();
            }
        }
    }
}