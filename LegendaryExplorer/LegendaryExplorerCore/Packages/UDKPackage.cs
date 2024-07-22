using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Packages
{
    public sealed class UDKPackage : UnrealPackageFile, IMEPackage
    {
        public const int UDKUnrealVersion2015 = 868; // 2015, the primary one
        public const int UDKUnrealVersion2014 = 867; // 2014, some really old ME3 mods ship these files
        public const int UDKUnrealVersion2011 = 812; // 2011, similar in age to ME3 // UDK 7797
        public const int UDKUnrealVersion2010_09 = 765; 
        public const int UDKLicenseeVersion = 0; // 2015

        public MEGame Game => MEGame.UDK;
        public MEPackage.GamePlatform Platform => MEPackage.GamePlatform.PC;
        public Endian Endian => Endian.Native; //we do not support big endian UDK packages
        public MELocalization Localization => MELocalization.None;

        /// <summary>
        /// Custom user-defined metadata to associate with this package object. This data has no effect on saving or loading, it is only for library user convenience.
        /// </summary>
        public Dictionary<string, object> CustomMetadata { get; set; } = new(0);

        /// <summary>
        /// This property is never used as UDK packages do not save LECLData
        /// </summary>
        public LECLData LECLTagData { get; }

        public byte[] getHeader()
        {
            using var ms = MemoryManager.GetMemoryStream();
            WriteHeader(ms);
            return ms.ToArray();
        }

        public bool CanSave => unrealVersion == UDKUnrealVersion2015;

        List<ME1TalkFile> IMEPackage.LocalTalkFiles => throw new NotImplementedException(); //not supported on this package type

        /// <summary>
        /// Passthrough to UnrealPackageFile's IsModified
        /// </summary>
        bool IMEPackage.IsModified
        {
            get => IsModified;
            set => IsModified = value;
        }

        public bool IsMemoryPackage { get; set; }

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
        private readonly ushort unrealVersion;
        private readonly string folderName;
        private int importExportGuidsOffset;
        private int importGuidsCount;
        private int exportGuidsCount;
        private int thumbnailTableOffset;
        private int Gen0ExportCount;
        private int Gen0NameCount;
        private int Gen0NetworkedObjectCount;
        private readonly int engineVersion;
        private int cookedContentVersion;
        private uint packageSource;
        private readonly List<Thumbnail> ThumbnailTable = new();
        #endregion

        private static bool _isBlankPackageCreatorRegistered;
        internal static Func<string, UDKPackage> RegisterBlankPackageCreator()
        {
            if (_isBlankPackageCreatorRegistered)
            {
                throw new Exception(nameof(UDKPackage) + " can only be initialized once");
            }
            _isBlankPackageCreatorRegistered = true;
            return fileName => new UDKPackage(fileName);
        }

        private static bool _isStreamLoaderRegistered;
        internal static Func<Stream, string, UDKPackage> RegisterStreamLoader()
        {
            if (_isStreamLoaderRegistered)
            {
                throw new Exception(nameof(UDKPackage) + " streamloader can only be initialized once");
            }

            _isStreamLoaderRegistered = true;
            return (s, associatedFilePath) => new UDKPackage(s, associatedFilePath);
        }

        public static Action<UDKPackage, string, bool, object> RegisterSaver() => saveByReconstructing;

        /// <summary>
        ///     UDKPackage class constructor. It also load namelist, importlist and exportinfo (not exportdata) from udk file
        /// </summary>
        /// <param name="filePath">full path + file name of desired udk file.</param>
        private UDKPackage(string filePath) : base(filePath != null ? Path.GetFullPath(filePath) : null)
        {
            names = new List<string>();
            imports = new List<ImportEntry>();
            exports = new List<ExportEntry>();
            folderName = "None";
            unrealVersion = UDKUnrealVersion2015;
            engineVersion = 12791;
            //reasonable defaults?
            Flags = EPackageFlags.AllowDownload | EPackageFlags.NoExportsData;
            return;
        }

        /// <summary>S
        ///     UDKPackage class constructor. It also load namelist, importlist and exportinfo (not exportdata) from udk file
        /// </summary>
        /// <param name="filePath">full path + file name of desired udk file.</param>
        private UDKPackage(Stream fs, string filePath) : base(filePath != null ? Path.GetFullPath(filePath) : null)
        {
            #region Header

            uint magic = fs.ReadUInt32();
            if (magic != packageTagLittleEndian)
            {
                throw new FormatException("Not an Unreal package!");
            }
            unrealVersion = fs.ReadUInt16();
            ushort licenseeVersion = fs.ReadUInt16();
            FullHeaderSize = fs.ReadInt32();
            int foldernameStrLen = fs.ReadInt32();
            //always "None", so don't bother saving result
            if (foldernameStrLen > 0)
                folderName = fs.ReadStringLatin1Null(foldernameStrLen);
            else
                folderName = fs.ReadStringUnicodeNull(foldernameStrLen * -2);

            Flags = (EPackageFlags)fs.ReadUInt32();

            //if (Flags.Has(EPackageFlags.Compressed))
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
                inStream = CompressionHelper.DecompressPackage(new EndianReader(fs), compressionInfoOffset);
            }

            var reader = new EndianReader(inStream); //these will always be little endian so we don't actually use this except for passing
                                                     //through to methods that can use endianness

            inStream.JumpTo(NameOffset);
            names = new List<string>(NameCount);
            for (int i = 0; i < NameCount; i++)
            {
                var name = inStream.ReadUnrealString();
                names.Add(name);
                nameLookupTable[name] = i;
                inStream.Skip(8);
            }

            inStream.JumpTo(ImportOffset);
            imports = new List<ImportEntry>(ImportCount);
            for (int i = 0; i < ImportCount; i++)
            {
                var imp = new ImportEntry(this, reader) { Index = i };
                imp.PropertyChanged += importChanged;
                imports.Add(imp);
            }

            //read exportTable (ExportEntry constructor reads export data)
            inStream.JumpTo(ExportOffset);
            exports = new List<ExportEntry>(ExportCount);
            for (int i = 0; i < ExportCount; i++)
            {
                var e = new ExportEntry(this, reader) { Index = i };
                e.PropertyChanged += exportChanged;
                exports.Add(e);
            }

            EntryLookupTable = new CaseInsensitiveDictionary<IEntry>(ExportCount + ImportCount);
            RebuildLookupTable();
        }

        private static void saveByReconstructing(UDKPackage udkPackage, string path, bool isSaveAs, object diskIOSyncLock = null)
        {
            var datastream = udkPackage.SaveToStream(false);
            // Lock writing with the sync object (if not null) to prevent disk concurrency issues
            // (the good old 'This file is in use by another process' message)
            if (diskIOSyncLock == null)
            {
                datastream.WriteToFile(path ?? udkPackage.FilePath);
            }
            else
            {
                lock (diskIOSyncLock)
                {
                    datastream.WriteToFile(path ?? udkPackage.FilePath);
                }
            }

            if (!isSaveAs)
            {
                udkPackage.AfterSave();
            }
        }

        /// <summary>
        /// Saves this UDK package to disk. The compression flag is not used by this method, the package will always save uncompressed.
        /// </summary>
        /// <param name="compress"></param>
        /// <param name="includeAdditionalPackagesToCook">This value is not used by this call</param>
        /// <param name="includeDependencyTable">This value is not used by this call</param>
        /// <returns></returns>
        public MemoryStream SaveToStream(bool compress, bool includeAdditionalPackagesToCook = true, bool includeDependencyTable = true)
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

            var ms = MemoryManager.GetMemoryStream();

            //just for positioning. We write over this later when the header values have been updated
            WriteHeader(ms);

            //name table
            NameOffset = (int)ms.Position;
            NameCount = Gen0NameCount = names.Count;
            foreach (string name in names)
            {
                ms.WriteUnrealStringLatin1(name);
                ms.WriteInt32(0);
                ms.WriteInt32(458768);
            }

            //import table
            ImportOffset = (int)ms.Position;
            ImportCount = imports.Count;
            foreach (ImportEntry e in imports)
            {
                e.SerializeHeader(ms);
            }

            //export table
            ExportOffset = (int)ms.Position;
            ExportCount = Gen0NetworkedObjectCount = Gen0ExportCount = exports.Count;
            foreach (ExportEntry e in exports)
            {
                e.HeaderOffset = (int)ms.Position;
                e.SerializeHeader(ms);
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
                    Texture2DMipInfo mip = tex.Mips.FirstOrDefault(mip => mip.height <= 256) ?? tex.GetTopMip();
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
                ms.WriteUnrealStringLatin1(thumbnail.ClassName);
                ms.WriteUnrealStringLatin1(thumbnail.PathName);
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

            ms.Position = 0;
            return ms;
        }

        private byte[] GetDefaultThumbnailBytes() => LegendaryExplorerCoreUtilities.LoadEmbeddedFile("udkdefaultthumb.png").ToArray();

        private void WriteHeader(Stream ms)
        {
            ms.WriteUInt32(packageTagLittleEndian);
            //version
            ms.WriteUInt16(UDKUnrealVersion2015);
            ms.WriteUInt16(UDKLicenseeVersion);

            ms.WriteInt32(FullHeaderSize);
            ms.WriteUnrealStringLatin1(folderName);

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
                binData.Skip(8);
                int thumbnailSize = binData.ReadInt32();
                binData.WriteInt32(baseOffset + (int)binData.Position + 4);
                binData.Skip(thumbnailSize);
                for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
                {
                    var storageFlags = (StorageFlags)binData.ReadInt32();
                    if (!storageFlags.Has(StorageFlags.externalFile)) //pcc-stored
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
                export.WriteBinary(binData.ToArray());
            }
        }
    }
}
