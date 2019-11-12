using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using static ME3Explorer.Unreal.UnrealFlags;
using StreamHelpers;

namespace ME3Explorer.Packages
{
    public sealed class UDKPackage : UnrealPackageFile, IMEPackage
    {
        public MEGame Game => MEGame.UDK;

        public byte[] getHeader()
        {
            var ms = new MemoryStream();
            WriteHeader(ms);
            return ms.ToArray();
        }

        public bool CanReconstruct => true;

        #region HeaderMisc
        private class Thumbnail
        {
            public string ClassName;
            public string PathName;
            public int Offset;
            public int Width;
            public int Height;
            public byte[] Data;
        }
        private string folderName;
        private int importExportGuidsOffset;
        private int importGuidsCount;
        private int exportGuidsCount;
        private int thumbnailTableOffset;
        private int Gen0ExportCount;
        private int Gen0NameCount;
        private int Gen0NetworkedObjectCount;
        private int engineVersion;
        private int cookedContentVersion;
        private uint packageSource;
        private List<Thumbnail> ThumbnailTable = new List<Thumbnail>();
        #endregion

        static bool isInitialized;
        internal static Func<string, bool, UDKPackage> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(UDKPackage) + " can only be initialized once");
            }
            else
            {
                isInitialized = true;
                return (fileName, shouldCreate) => new UDKPackage(fileName, shouldCreate);
            }
        }

        /// <summary>
        ///     UDKPackage class constructor. It also load namelist, importlist and exportinfo (not exportdata) from udk file
        /// </summary>
        /// <param name="UDKPackagePath">full path + file name of desired udk file.</param>
        /// <param name="create">Create a file instead of reading from disk</param>
        private UDKPackage(string UDKPackagePath, bool create = false) : base(Path.GetFullPath(UDKPackagePath))
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"UDKPackage {Path.GetFileName(UDKPackagePath)}", new WeakReference(this));

            if (create)
            {
                folderName = "None";
                engineVersion = 12791;
                //reasonable defaults?
                Flags = EPackageFlags.AllowDownload | EPackageFlags.NoExportsData;
                return;
            }

            using (var fs = File.OpenRead(FilePath))
            {
                #region Header

                uint magic = fs.ReadUInt32();
                if (magic != packageTag)
                {
                    throw new FormatException("Not an Unreal package!");
                }
                ushort unrealVersion = fs.ReadUInt16();
                ushort licenseeVersion = fs.ReadUInt16();
                FullHeaderSize = fs.ReadInt32();
                int foldernameStrLen = fs.ReadInt32();
                //always "None", so don't bother saving result
                if (foldernameStrLen > 0)
                    folderName = fs.ReadStringASCIINull(foldernameStrLen);
                else
                    folderName = fs.ReadStringUnicodeNull(foldernameStrLen * -2);

                Flags = (EPackageFlags)fs.ReadUInt32();

                //if (Flags.HasFlag(EPackageFlags.Compressed))
                //{
                //    throw new FormatException("Cannot read compressed UDK packages!");
                //}

                NameCount = fs.ReadInt32();
                NameOffset = fs.ReadInt32();
                ExportCount = fs.ReadInt32();
                ExportOffset = fs.ReadInt32();
                ImportCount = fs.ReadInt32();
                ImportOffset = fs.ReadInt32();
                DependencyTableOffset = fs.ReadInt32();
                importExportGuidsOffset = fs.ReadInt32();
                importGuidsCount = fs.ReadInt32();
                exportGuidsCount = fs.ReadInt32();
                thumbnailTableOffset = fs.ReadInt32();
                PackageGuid = fs.ReadGuid();

                uint generationsTableCount = fs.ReadUInt32();
                if (generationsTableCount > 0)
                {
                    generationsTableCount--;
                    Gen0ExportCount = fs.ReadInt32();
                    Gen0NameCount = fs.ReadInt32();
                    Gen0NetworkedObjectCount = fs.ReadInt32();
                }
                //don't care about other gens, so skip them
                fs.Skip(generationsTableCount * 12);
                engineVersion = fs.ReadInt32();
                cookedContentVersion = fs.ReadInt32();

                //skip compression type chunks. Decompressor will handle that
                long compressionInfoOffset = fs.Position;
                fs.SkipInt32();
                int numChunks = fs.ReadInt32();
                fs.Skip(numChunks * 16);

                packageSource = fs.ReadUInt32();
                //additional packages to cook, and texture allocation, but we don't care about those, so we won't read them in.

                #endregion
                Stream inStream = fs;
                if (IsCompressed && numChunks > 0)
                {
                    inStream = CompressionHelper.DecompressUDK(fs, compressionInfoOffset);
                }

                inStream.JumpTo(NameOffset);
                for (int i = 0; i < NameCount; i++)
                {
                    names.Add(inStream.ReadUnrealString());
                    inStream.Skip(8);
                }

                inStream.JumpTo(ImportOffset);
                for (int i = 0; i < ImportCount; i++)
                {
                    ImportEntry imp = new ImportEntry(this, inStream) { Index = i };
                    imp.PropertyChanged += importChanged;
                    imports.Add(imp);
                }

                //read exportTable (ExportEntry constructor reads export data)
                inStream.JumpTo(ExportOffset);
                for (int i = 0; i < ExportCount; i++)
                {
                    ExportEntry e = new ExportEntry(this, inStream) { Index = i };
                    e.PropertyChanged += exportChanged;
                    exports.Add(e);
                }
            }
        }

        public void Save()
        {
            Save(FilePath);
        }

        /// <summary>
        ///     Not supported for UDK files
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void Save(string path)
        {
            bool isSaveAs = path != FilePath;
            saveByReconstructing(path, isSaveAs);
        }
        private void saveByReconstructing(string path, bool isSaveAs)
        {
            try
            {
                if (exports.Any(exp => exp.ClassName == "Level"))
                {
                    Flags |= EPackageFlags.Map;
                }

                //UDK does not like it when exports have the forced export flag, which is common in ME files
                foreach (ExportEntry export in exports)
                {
                    //if (export.ClassName != "Package")
                    {
                        export.ExportFlags &= ~EExportFlags.ForcedExport;
                    }
                }

                var ms = new MemoryStream();

                //just for positioning. We write over this later when the header values have been updated
                WriteHeader(ms);

                //name table
                NameOffset = (int)ms.Position;
                NameCount = Gen0NameCount = names.Count;
                foreach (string name in names)
                {
                    ms.WriteUnrealStringASCII(name);
                    ms.WriteInt32(0);
                    ms.WriteInt32(458768);
                }

                //import table
                ImportOffset = (int)ms.Position;
                ImportCount = imports.Count;
                foreach (ImportEntry e in imports)
                {
                    ms.WriteFromBuffer(e.Header);
                }

                //export table
                ExportOffset = (int)ms.Position;
                ExportCount = Gen0NetworkedObjectCount = Gen0ExportCount = exports.Count;
                foreach (ExportEntry e in exports)
                {
                    e.HeaderOffset = (uint)ms.Position;
                    ms.WriteFromBuffer(e.Header);
                }

                //dependency table
                DependencyTableOffset = (int)ms.Position;
                ms.WriteZeros(4 * ExportCount);

                importExportGuidsOffset = (int)ms.Position;
                importGuidsCount = exportGuidsCount = 0;

                //generate thumbnails
                ThumbnailTable.Clear();
                foreach (ExportEntry export in exports)
                {
                    if (export.IsTexture())
                    {
                        var tex = new Texture2D(export);
                        var mip = tex.GetTopMip();
                        ThumbnailTable.Add(new Thumbnail
                        {
                            ClassName = export.ClassName,
                            PathName = export.InstancedFullPath,
                            Width = mip.width,
                            Height = mip.height,
                            Data = tex.GetPNG(mip)
                        });
                    }
                }

                //write thumbnails
                foreach (Thumbnail thumbnail in ThumbnailTable)
                {
                    thumbnail.Offset = (int)ms.Position;
                    ms.WriteInt32(thumbnail.Width);
                    ms.WriteInt32(thumbnail.Height);
                    ms.WriteInt32(thumbnail.Data.Length);
                    ms.WriteFromBuffer(thumbnail.Data);
                    //File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(FilePath), $"{thumbnail.PathName}({thumbnail.ClassName}).png"), thumbnail.Data);
                }
                thumbnailTableOffset = (int)ms.Position;
                ms.WriteInt32(ThumbnailTable.Count);
                foreach (Thumbnail thumbnail in ThumbnailTable)
                {
                    ms.WriteUnrealStringASCII(thumbnail.ClassName);
                    ms.WriteUnrealStringASCII(thumbnail.PathName);
                    ms.WriteInt32(thumbnail.Offset);
                }

                FullHeaderSize = (int)ms.Position;

                //export data
                foreach (ExportEntry e in exports)
                {
                    UpdateUDKOffsets(e, (int)ms.Position);
                    e.DataOffset = (int)ms.Position;

                    ms.WriteFromBuffer(e.Data);
                    //update size and offset in already-written header
                    long pos = ms.Position;
                    ms.JumpTo(e.HeaderOffset + 32);
                    ms.WriteInt32(e.DataSize); //DataSize might have been changed by UpdateOffsets
                    ms.WriteInt32(e.DataOffset);
                    ms.JumpTo(pos);
                }

                //re-write header with updated values
                ms.JumpTo(0);
                WriteHeader(ms);


                File.WriteAllBytes(path, ms.ToArray());
                if (!isSaveAs)
                {
                    AfterSave();
                }
            }
            catch (Exception ex) when (!App.IsDebug)
            {
                MessageBox.Show($"Error saving {FilePath}:\n{ExceptionHandlerDialogWPF.FlattenException(ex)}");
            }
        }

        private void WriteHeader(Stream ms)
        {
            ms.WriteUInt32(packageTag);
            //version
            ms.WriteUInt16(868);
            ms.WriteUInt16(0);

            ms.WriteInt32(FullHeaderSize);
            ms.WriteUnrealStringASCII(folderName);

            ms.WriteUInt32((uint)Flags);
            ms.WriteInt32(NameCount);
            ms.WriteInt32(NameOffset);
            ms.WriteInt32(ExportCount);
            ms.WriteInt32(ExportOffset);
            ms.WriteInt32(ImportCount);
            ms.WriteInt32(ImportOffset);
            ms.WriteInt32(DependencyTableOffset);
            ms.WriteInt32(importExportGuidsOffset);
            ms.WriteInt32(importGuidsCount);
            ms.WriteInt32(exportGuidsCount);
            ms.WriteInt32(thumbnailTableOffset);
            ms.WriteGuid(PackageGuid);

            //Write 1 generation
            ms.WriteInt32(1);
            ms.WriteInt32(Gen0ExportCount);
            ms.WriteInt32(Gen0NameCount);
            ms.WriteInt32(Gen0NetworkedObjectCount);

            ms.WriteInt32(engineVersion);
            ms.WriteInt32(cookedContentVersion);


            ms.WriteInt32(0);//CompressiontType.None
            ms.WriteInt32(0);//numChunks

            ms.WriteUInt32(packageSource);

            ms.WriteInt32(0);//empty additionalPackagesToCook array
            ms.WriteInt32(0);//empty TextureAllocations
        }

        private static void UpdateUDKOffsets(ExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            //update offsets for pcc-stored mips in Textures
            if (export.IsTexture())
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream binData = new MemoryStream(export.GetBinaryData());
                binData.Skip(12);
                binData.WriteInt32(baseOffset + (int)binData.Position + 4);
                for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
                {
                    var storageFlags = (StorageFlags)binData.ReadInt32();
                    if (!storageFlags.HasFlag(StorageFlags.externalFile)) //pcc-stored
                    {
                        int uncompressedSize = binData.ReadInt32();
                        int compressedSize = binData.ReadInt32();
                        binData.WriteInt32(baseOffset + (int)binData.Position + 4);//update offset
                        binData.Seek((storageFlags == StorageFlags.noFlags ? uncompressedSize : compressedSize) + 8, SeekOrigin.Current); //skip texture and width + height values
                    }
                    else
                    {
                        binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
                    }
                }

                binData.Skip(40);
                binData.WriteInt32(baseOffset + (int)binData.Position + 4);
                export.SetBinaryData(binData.ToArray());
            }
        }
    }
}
