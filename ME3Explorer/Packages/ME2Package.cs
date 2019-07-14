using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using ME3Explorer.Unreal;
using StreamHelpers;

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

            FilePath = Path.GetFullPath(path);
            MemoryStream tempStream = new MemoryStream();
            if (!File.Exists(FilePath))
                throw new FileNotFoundException("PCC file not found");
            using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
            {
                fs.CopyTo(tempStream);
            }

            tempStream.Seek(12, SeekOrigin.Begin);
            int tempNameSize = tempStream.ReadInt32();
            tempStream.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerations = tempStream.ReadInt32();
            tempStream.Seek(32 + tempGenerations * 12, SeekOrigin.Current);
            tempStream.ReadUInt32(); //Compression Type. We read this from header[] in MEPackage.cs intead when accessing value
            int tempPos = (int)tempStream.Position;
            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadToBuffer(tempPos);
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
                listsStream.WriteFromBuffer(header);

                //Set numblocks to zero
                listsStream.WriteInt32(0);
                //Write the magic number
                listsStream.WriteInt32(1026281201);
                //Write 8 bytes of 0
                listsStream.WriteInt32(0);
                listsStream.WriteInt32(0);
            }
            else
            {
                listsStream = tempStream;
            }

            names = new List<string>();
            listsStream.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                int len = listsStream.ReadInt32();
                string s = listsStream.ReadStringASCII(len - 1);
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

            exports = new List<ExportEntry>();
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
            saveByReconstructing(path);
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
                //m.Seek(-4, SeekOrigin.Current);
                //m.WriteByte((byte)CompressionType.None); //Write header compression type to None
                //Set numblocks to zero
                m.WriteInt32(0);
                //Write the magic number
                m.WriteInt32(1026281201);
                //Write 8 bytes of 0
                m.WriteInt64(0);

                //name table
                NameOffset = (int)m.Position;
                NameCount = names.Count;
                foreach (string name in names)
                {
                    m.WriteUnrealStringASCII(name);
                    m.WriteInt32(-14);
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
                foreach (ExportEntry e in exports)
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
                foreach (ExportEntry e in exports)
                {
                    UpdateOffsets(e, (int)m.Position);

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

        private static void UpdateOffsets(ExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            //update offsets for pcc-stored audio in wwisestreams
            if ((export.ClassName == "WwiseStream" && export.GetProperty<NameProperty>("Filename") == null) || export.ClassName == "WwiseBank")
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(44, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored mips in Textures
            else if (export.ClassName == "Texture2D" || export.ClassName == "LightMapTexture2D" || export.ClassName == "TextureFlipBook")
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream binData = new MemoryStream(export.getBinaryData());
                binData.Skip(12);
                binData.WriteInt32(baseOffset + (int)binData.Position + 4);
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

                    int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextMaterialShaderMapOffset + newDataOffset);
                    binData.Seek(nextMaterialShaderMapOffset, SeekOrigin.Begin);
                }

                export.Data = binData.ToArray();
            }
            else if (export.ClassName == "StaticMeshComponent")
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream bin = new MemoryStream(export.Data);
                bin.JumpTo(export.propsEnd());

                int lodDataCount = bin.ReadInt32();
                for (int i = 0; i < lodDataCount; i++)
                {
                    int shadowMapCount = bin.ReadInt32();
                    bin.Skip(shadowMapCount * 4);
                    int shadowVertCount = bin.ReadInt32();
                    bin.Skip(shadowVertCount * 4);
                    int lightMapType = bin.ReadInt32();
                    if (lightMapType == 0) continue;
                    int lightGUIDsCount = bin.ReadInt32();
                    bin.Skip(lightGUIDsCount * 16);
                    switch (lightMapType)
                    {
                        case 1:
                            bin.Skip(4 + 8);
                            int bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12 * 4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            break;
                        case 2:
                            bin.Skip((16) * 4 + 16);
                            break;
                    }
                }
            }
        }
    }
}
