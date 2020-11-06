using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ME3ExplorerCore.Compression;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.TLK.ME1;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using static ME3ExplorerCore.Unreal.UnrealFlags;
#if AZURE
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace ME3ExplorerCore.Packages
{
    [Flags]
    public enum PackageChange
    {
        Export = 0x1,
        Import = 0x2,
        Name = 0x4,
        Add = 0x8,
        Remove = 0x10,
        Data = 0x20,
        Header = 0x40,
        Entry = 0x80,
        EntryAdd = Entry | Add,
        EntryRemove = Entry | Remove,
        EntryHeader = Entry | Header,
        ExportData = Export | Data | Entry,
        ExportHeader = Export | EntryHeader,
        ImportHeader = Import | EntryHeader,
        ExportAdd = Export | EntryAdd,
        ImportAdd = Import | EntryAdd,
        ExportRemove = Export | EntryRemove,
        ImportRemove = Import | EntryRemove,
        NameAdd = Name | Add,
        NameRemove = Name | Remove,
        NameEdit = Name | Data
    }

    [DebuggerDisplay("PackageUpdate | {Change} on index {Index}")]
    public readonly struct PackageUpdate
    {
        /// <summary>
        /// Details on what piece of data has changed
        /// </summary>
        public readonly PackageChange Change;
        /// <summary>
        /// index of what item has changed. Meaning depends on value of Change
        /// </summary>
        public readonly int Index;

        public PackageUpdate(PackageChange change, int index)
        {
            this.Change = change;
            this.Index = index;
        }
    }

    public sealed class MEPackage : UnrealPackageFile, IMEPackage, IDisposable
    {
        public const ushort ME1UnrealVersion = 491;
        public const ushort ME1LicenseeVersion = 1008;
        public const ushort ME1XboxUnrealVersion = 391;
        public const ushort ME1XboxLicenseeVersion = 92;
        public const ushort ME1PS3UnrealVersion = 684; //same as ME3 ;)
        public const ushort ME1PS3LicenseeVersion = 153;

        public const ushort ME2UnrealVersion = 512;
        public const ushort ME2PS3UnrealVersion = 684; //Same as ME3 ;)
        public const ushort ME2DemoUnrealVersion = 513;
        public const ushort ME2LicenseeVersion = 130;
        public const ushort ME2PS3LicenseeVersion = 150;

        public const ushort ME3UnrealVersion = 684;
        public const ushort ME3WiiUUnrealVersion = 845;
        public const ushort ME3Xenon2011DemoLicenseeVersion = 185;
        public const ushort ME3LicenseeVersion = 194;

        /// <summary>
        /// Indicates what type of package file this is. 0 is normal, 1 is TESTPATCH patch package.
        /// </summary>
        public int PackageTypeId { get; }

        /// <summary>
        /// This is not useful for modding but we should not be changing the format of the package file.
        /// </summary>
        public List<string> AdditionalPackagesToCook = new List<string>();


        public Endian Endian { get; }
        public MEGame Game { get; private set; } //can only be ME1, ME2, or ME3. UDK is a separate class
        public GamePlatform Platform { get; private set; } = GamePlatform.Unknown;

        public enum GamePlatform
        {
            Unknown, //Unassigned
            PC,
            Xenon,
            PS3,
            WiiU
        }

        public MELocalization Localization { get; } = MELocalization.None;

        public byte[] getHeader()
        {
            var ms = new MemoryStream();
            WriteHeader(ms);
            return ms.ToArray();
        }

#region HeaderMisc
        private int Gen0ExportCount;
        private int Gen0NameCount;
        private int Gen0NetworkedObjectCount;
        private int ImportExportGuidsOffset;
        //private int ImportGuidsCount;
        //private int ExportGuidsCount;
        //private int ThumbnailTableOffset;
        private uint packageSource;
        private int unknown4;
        private int unknown6;
#endregion

        static bool isLoaderRegistered;
        static bool isStreamLoaderRegistered;

        public static Func<string, MEGame, MEPackage> RegisterLoader()
        {
            if (isLoaderRegistered)
            {
                throw new Exception(nameof(MEPackage) + " can only be initialized once");
            }

            isLoaderRegistered = true;
            return (f, g) =>
            {
                if (g != MEGame.Unknown)
                {
                    return new MEPackage(g, f);
                }
                return new MEPackage(new MemoryStream(File.ReadAllBytes(f)), f);
            };
        }

        public static Func<Stream, string, MEPackage> RegisterStreamLoader()
        {
            if (isStreamLoaderRegistered)
            {
                throw new Exception(nameof(MEPackage) + " streamloader can only be initialized once");
            }

            isStreamLoaderRegistered = true;
            return (s, associatedFilePath) => new MEPackage(s, associatedFilePath);
        }


        private MEPackage(MEGame game, string filePath = null) : base(filePath != null ? Path.GetFullPath(filePath) : null)
        {
            //new Package
            Game = game;
            //reasonable defaults?
            Flags = EPackageFlags.Cooked | EPackageFlags.AllowDownload | EPackageFlags.DisallowLazyLoading | EPackageFlags.RequireImportsAlreadyLoaded;
            return;
        }

        /// <summary>
        /// Opens an ME package from the stream. If this file is from a disk, the filePath should be set to support saving and other lookups.
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="filePath"></param>
        private MEPackage(Stream fs, string filePath = null) : base(filePath != null ? File.Exists(filePath) ? Path.GetFullPath(filePath) : filePath: null)
        {
            //MemoryStream fs = new MemoryStream(File.ReadAllBytes(filePath));
            //Debug.WriteLine($"Reading MEPackage from stream starting at position 0x{fs.Position:X8}");
#region Header

            EndianReader packageReader = EndianReader.SetupForPackageReading(fs);
            packageReader.SkipInt32(); //skip magic as we have already read it
            Endian = packageReader.Endian;

            //Big endian means it will be console version and package header is slightly tweaked as some flags are always set

            // This is stored as integer by cooker as it is flipped by size word in big endian
            var versionLicenseePacked = packageReader.ReadUInt32();

            GamePlatform platformOverride = GamePlatform.Unknown; //Used to help differentiate beteween PS3 and Xenon ME3
            CompressionType fcCompressionType = CompressionType.None;

            if ((versionLicenseePacked == 0x00020000 || versionLicenseePacked == 0x00010000) && Endian == Endian.Little)
            {
                if (versionLicenseePacked == 0x20000)
                {
                    // It's WiiU LZMA or Xenon LZX
                    // To determine if it's LZMA we have to read the first block's compressed bytes and read first few bytes
                    // LZMA always starts with 0x5D and then is followed by a dictionary size of size word (32) (in ME it looks like 0x10000)

                    // This is done in the decompress fully compressed package method when we pass it None type
                    fs = CompressionHelper.DecompressFullyCompressedPackage(packageReader, ref fcCompressionType);
                    platformOverride = fcCompressionType == CompressionType.LZX ? GamePlatform.Xenon : GamePlatform.WiiU;
                }
                else if (versionLicenseePacked == 0x10000)
                {
                    //PS3, LZMA
                    fcCompressionType = CompressionType.LZMA; // Known already
                    fs = CompressionHelper.DecompressFullyCompressedPackage(packageReader, ref fcCompressionType);
                    platformOverride = GamePlatform.PS3;
                }
                // Fully compressed packages use little endian magic in them 
                // so we need to re-setup the endian reader
                // Why do they use different endians on the same processor platform?
                // Who the hell knows!
                packageReader = EndianReader.SetupForPackageReading(fs);
                packageReader.SkipInt32(); //skip magic as we have already read it
                Endian = packageReader.Endian;
                versionLicenseePacked = packageReader.ReadUInt32();
            }

            var unrealVersion = (ushort)(versionLicenseePacked & 0xFFFF);
            var licenseeVersion = (ushort)(versionLicenseePacked >> 16);
            bool platformNeedsResolved = false;
            switch (unrealVersion)
            {
                case ME1UnrealVersion when licenseeVersion == ME1LicenseeVersion:
                    Game = MEGame.ME1;
                    Platform = GamePlatform.PC;
                    break;
                case ME1XboxUnrealVersion when licenseeVersion == ME1XboxLicenseeVersion:
                    Game = MEGame.ME1;
                    Platform = GamePlatform.Xenon;
                    break;
                case ME1PS3UnrealVersion when licenseeVersion == ME1PS3LicenseeVersion:
                    Game = MEGame.ME1;
                    Platform = GamePlatform.PS3;
                    break;
                case ME2UnrealVersion when licenseeVersion == ME2LicenseeVersion && Endian == Endian.Little:
                case ME2DemoUnrealVersion when licenseeVersion == ME2LicenseeVersion:
                    Game = MEGame.ME2;
                    Platform = GamePlatform.PC;
                    break;
                case ME2UnrealVersion when licenseeVersion == ME2LicenseeVersion && Endian == Endian.Big:
                    Game = MEGame.ME2;
                    Platform = GamePlatform.Xenon;
                    break;
                case ME2PS3UnrealVersion when licenseeVersion == ME2PS3LicenseeVersion:
                    Game = MEGame.ME2;
                    Platform = GamePlatform.PS3;
                    break;
                case ME3WiiUUnrealVersion when licenseeVersion == ME3LicenseeVersion:
                    Game = MEGame.ME3;
                    Platform = GamePlatform.WiiU;
                    break;
                case ME3UnrealVersion when licenseeVersion == ME3LicenseeVersion:
                    Game = MEGame.ME3;
                    if (Endian == Endian.Little)
                    {
                        Platform = GamePlatform.PC;
                    }
                    else
                    {
                        // If the package is not compressed or fully compressed we cannot determine if this is PS3 or Xenon.
                        // PS3 and Xbox use same engine versions on the ME3 game (ME1/2 use same one but has slight differences for some reason)

                        // Code above determines platform if it's fully compressed, and code below determines platform based on compression type
                        // However if neither exist we don't have an easy way to differentiate files (such as files from SFAR)

                        // We attempt to resolve the platfrom later using SeekFreeShaderCache which is present
                        // in every single console file (vs PC's DLC only).
                        // Not 100% sure it's in every file. But hopefully it is.
                        if (platformOverride == GamePlatform.Unknown)
                        {
                            //Debug.WriteLine("Cannot differentiate PS3 vs Xenon ME3 files. Assuming PS3, this may be wrong assumption!");
                            platformNeedsResolved = true;
                            Platform = GamePlatform.PS3; //This is placeholder as Xenon and PS3 use same header format
                        }
                        else
                        {
                            Platform = platformOverride; // Used for fully compressed packages
                        }
                    }
                    break;
                case ME3UnrealVersion when licenseeVersion == ME3Xenon2011DemoLicenseeVersion:
                    Game = MEGame.ME3;
                    Platform = GamePlatform.Xenon;
                    break;
                default:
                    throw new FormatException("Not a Mass Effect Package!");
            }
            FullHeaderSize = packageReader.ReadInt32();
            int foldernameStrLen = packageReader.ReadInt32();
            //always "None", so don't bother saving result
            if (foldernameStrLen > 0)
                fs.ReadStringASCIINull(foldernameStrLen);
            else
                fs.ReadStringUnicodeNull(foldernameStrLen * -2);

            Flags = (EPackageFlags)packageReader.ReadUInt32();

            //Xenon Demo ME3 doesn't read this. Xenon ME3 Retail does
            if (Game == MEGame.ME3 
                && (Flags.HasFlag(EPackageFlags.Cooked) || Platform != GamePlatform.PC) && licenseeVersion != ME3Xenon2011DemoLicenseeVersion)
            {
                //Consoles are always cooked.
                PackageTypeId = packageReader.ReadInt32(); //0 = standard, 1 = patch ? Not entirely sure. patch_001 files with byte = 0 => game does not load
            }

            NameCount = packageReader.ReadInt32();
            NameOffset = packageReader.ReadInt32();
            ExportCount = packageReader.ReadInt32();
            ExportOffset = packageReader.ReadInt32();
            ImportCount = packageReader.ReadInt32();
            ImportOffset = packageReader.ReadInt32();

            if (Game != MEGame.ME1 || Platform != GamePlatform.Xenon)
            {
                // Seems this doesn't exist on ME1 Xbox
                DependencyTableOffset = packageReader.ReadInt32();
            }

            if (Game == MEGame.ME3 || Platform == GamePlatform.PS3)
            {
                ImportExportGuidsOffset = packageReader.ReadInt32();
                packageReader.ReadInt32(); //ImportGuidsCount always 0
                packageReader.ReadInt32(); //ExportGuidsCount always 0
                packageReader.ReadInt32(); //ThumbnailTableOffset always 0
            }

            PackageGuid = packageReader.ReadGuid();
            uint generationsTableCount = packageReader.ReadUInt32();
            if (generationsTableCount > 0)
            {
                generationsTableCount--;
                Gen0ExportCount = packageReader.ReadInt32();
                Gen0NameCount = packageReader.ReadInt32();
                Gen0NetworkedObjectCount = packageReader.ReadInt32();
            }
            //should never be more than 1 generation, but just in case
            packageReader.Skip(generationsTableCount * 12);

            packageReader.SkipInt32();//engineVersion          Like unrealVersion and licenseeVersion, these 2 are determined by what game this is,
            packageReader.SkipInt32();//cookedContentVersion   so we don't have to read them in

            if ((Game == MEGame.ME2 || Game == MEGame.ME1) && Platform != GamePlatform.PS3) //PS3 on ME3 engine
            {
                packageReader.SkipInt32(); //always 0
                packageReader.SkipInt32(); //always 47699
                unknown4 = packageReader.ReadInt32();
                packageReader.SkipInt32(); //always 1 in ME1, always 1966080 in ME2
            }

            unknown6 = packageReader.ReadInt32();
            var constantVal = packageReader.ReadInt32();//always -1 in ME1 and ME2, always 145358848 in ME3

            if (Game == MEGame.ME1 && Platform != GamePlatform.PS3)
            {
                packageReader.SkipInt32(); //always -1
            }

            //COMPRESSION AND COMPRESSION CHUNKS
            var compressionFlagPosition = packageReader.Position;
            var compressionType = (UnrealPackageFile.CompressionType)packageReader.ReadInt32();
            if (platformNeedsResolved && compressionType != CompressionType.None)
            {
                Platform = compressionType == CompressionType.LZX ? GamePlatform.Xenon : GamePlatform.PS3;
                platformNeedsResolved = false;
            }

            //Debug.WriteLine($"Compression type {filePath}: {compressionType}");
            int numChunks = packageReader.ReadInt32();

            //read package source
            var savedPos = packageReader.Position;
            packageReader.Skip(numChunks * 16); //skip chunk table so we can find package tag


            packageSource = packageReader.ReadUInt32(); //this needs to be read in so it can be properly written back out.

            if ((Game == MEGame.ME2 || Game == MEGame.ME1) && Platform != GamePlatform.PS3)
            {
                packageReader.SkipInt32(); //always 0
            }

            //Doesn't need to be written out, so it doesn't need to be read in
            //keep this here in case one day we learn that this has a purpose
            //Narrator: On Jan 26, 2020 it turns out this was actually necessary to make it work
            //with ME3Tweaks Mixins as old code did not remove this section
            //Also we should strive to ensure closeness to the original source files as possible
            //because debugging things is a huge PITA if you start to remove stuff
            if (Game == MEGame.ME2 || Game == MEGame.ME3 || Platform == GamePlatform.PS3)
            {
                int additionalPackagesToCookCount = packageReader.ReadInt32();
                for (int i = 0; i < additionalPackagesToCookCount; i++)
                {
                    var packageStr = packageReader.ReadUnrealString();
                    AdditionalPackagesToCook.Add(packageStr);
                }
            }

            packageReader.Position = savedPos; //restore position to chunk table
            Stream inStream = fs;
            if (IsCompressed && numChunks > 0)
            {
                inStream = CompressionHelper.DecompressPackage(packageReader, compressionFlagPosition, game: Game, platform: Platform);
            }


#endregion

            var endian = packageReader.Endian;
            packageReader = new EndianReader(inStream) { Endian = endian };
            //read namelist
            inStream.JumpTo(NameOffset);
            for (int i = 0; i < NameCount; i++)
            {
                var name = packageReader.ReadUnrealString();
                names.Add(name);
                nameLookupTable[name] = i;
                if (Game == MEGame.ME1 && Platform != GamePlatform.PS3)
                    inStream.Skip(8);
                else if (Game == MEGame.ME2 && Platform != GamePlatform.PS3)
                    inStream.Skip(4);
            }

            //read importTable
            inStream.JumpTo(ImportOffset);
            for (int i = 0; i < ImportCount; i++)
            {
                ImportEntry imp = new ImportEntry(this, packageReader) { Index = i };
                imp.PropertyChanged += importChanged;
                imports.Add(imp);
            }

            //read exportTable (ExportEntry constructor reads export data)
            inStream.JumpTo(ExportOffset);
            for (int i = 0; i < ExportCount; i++)
            {
                ExportEntry e = new ExportEntry(this, packageReader) { Index = i };
                e.PropertyChanged += exportChanged;
                exports.Add(e);
                if (platformNeedsResolved && e.ClassName == "ShaderCache")
                {
                    // Read the first binary byte, it's a platform flag
                    // 0 = PC
                    // 1 = PS3
                    // 2 = Xenon
                    // 5 = WiiU
                    // See ME3Explorer's EShaderPlatform enum in it's binary interpreter scans
                    var resetPos = packageReader.Position;
                    packageReader.Position = e.DataOffset + 0xC; // Skip 4 byte + "None"
                    var platform = packageReader.ReadByte();
                    if (platform == 1)
                    {
                        Platform = GamePlatform.PS3;
                        platformNeedsResolved = false;
                    }
                    else if (platform == 2)
                    {
                        Platform = GamePlatform.Xenon;
                        platformNeedsResolved = false;
                    }
                    else if (platform == 5)
                    {
                        // I think this won't ever occur
                        // as we have engine version diff
                        // But might as well just make sure
                        Platform = GamePlatform.WiiU;
                        platformNeedsResolved = false;
                    }
                    packageReader.Position = resetPos;
                }
            }

            if (Game == MEGame.ME1 && Platform == GamePlatform.PC)
            {
                ReadLocalTLKs();
            }


            if (filePath != null)
            {

                string localizationName = Path.GetFileNameWithoutExtension(filePath).ToUpper();
                if (localizationName.Length > 8)
                    localizationName = localizationName.Substring(localizationName.Length - 8, 8);
                switch (localizationName)
                {
                    case "_LOC_DEU":
                        Localization = MELocalization.DEU;
                        break;
                    case "_LOC_ESN":
                        Localization = MELocalization.ESN;
                        break;
                    case "_LOC_FRA":
                        Localization = MELocalization.FRA;
                        break;
                    case "_LOC_INT":
                        Localization = MELocalization.INT;
                        break;
                    case "_LOC_ITA":
                        Localization = MELocalization.ITA;
                        break;
                    case "_LOC_JPN":
                        Localization = MELocalization.JPN;
                        break;
                    case "_LOC_POL":
                        Localization = MELocalization.POL;
                        break;
                    case "_LOC_RUS":
                        Localization = MELocalization.RUS;
                        break;
                    default:
                        Localization = MELocalization.None;
                        break;
                }
            }

#if AZURE
            if (platformNeedsResolved)
            {
                Assert.Fail($"Package platform was not resolved! Package file: {FilePath}");
            }
#endif
        }



        public static Action<MEPackage, string, bool, bool> RegisterSaver() => saveByReconstructing;

        /// <summary>
        /// Saves the package to disk by reconstructing the package file
        /// </summary>
        /// <param name="mePackage"></param>
        /// <param name="path"></param>
        /// <param name="isSaveAs"></param>
        /// <param name="compress"></param>
        private static void saveByReconstructing(MEPackage mePackage, string path, bool isSaveAs, bool compress)
        {
            var saveStream = saveByReconstructingToStream(mePackage, isSaveAs, compress);
            saveStream.WriteToFile(path);
            if (!isSaveAs)
            {
                mePackage.AfterSave();
            }
        }

        /// <summary>
        /// Compresses a package's uncompressed stream to a compressed stream and returns it.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="uncompressedStream"></param>
        /// <returns></returns>
        private static MemoryStream compressPackage(MEPackage package, MemoryStream uncompressedStream)
        {
            uncompressedStream.Position = 0;
            MemoryStream compressedStream = new MemoryStream();
            package.WriteHeader(compressedStream); //for positioning
            var chunks = new List<CompressionHelper.Chunk>();
            var compressionType = package.Game != MEGame.ME3 ? CompressionType.LZO : CompressionType.Zlib;

            //Compression format:
            //uint ChunkMetaDataTableCount (chunk table)

            //CHUNK METADATA TABLE ENTRIES:
            //uint UncompressedOffset
            //uint UncompressedSize
            //uint CompressedOffset
            //uint CompressedSize
            //
            // After ChunkMetaDataTableCount * 16 bytes the chunk blocks begin
            // Each chunk block has it's own block header specifying the uncompressed and compressed size of the block.
            //

            CompressionHelper.Chunk chunk = new CompressionHelper.Chunk();
            //Tables chunk
            chunk.uncompressedSize = package.FullHeaderSize - package.NameOffset;
            chunk.uncompressedOffset = package.NameOffset;

#region DEBUG STUFF
            //string firstElement = "Tables";
            //string lastElement = firstElement;

            //MemoryStream m2 = new MemoryStream();
            //long pos = uncompressedStream.Position;
            //uncompressedStream.Position = NameOffset;
            //m2.WriteFromStream(uncompressedStream, chunk.uncompressedSize);
            //uncompressedStream.Position = pos;
#endregion

            //Export data chunks
            int chunkNum = 0;
            //Debug.WriteLine($"Exports start at {Exports[0].DataOffset}");
            foreach (ExportEntry e in package.Exports)
            {
                if (chunk.uncompressedSize + e.DataSize > CompressionHelper.MAX_CHUNK_SIZE)
                {
                    //Rollover to the next chunk as this chunk would be too big if we tried to put this export into the chunk
                    chunks.Add(chunk);
                    //Debug.WriteLine($"Chunk {chunkNum} ({chunk.uncompressedSize} bytes) contains {firstElement} to {lastElement} - 0x{chunk.uncompressedOffset:X6} to 0x{(chunk.uncompressedSize + chunk.uncompressedOffset):X6}");
                    chunkNum++;
                    chunk = new CompressionHelper.Chunk
                    {
                        uncompressedSize = e.DataSize,
                        uncompressedOffset = e.DataOffset
                    };
                }
                else
                {
                    chunk.uncompressedSize += e.DataSize; //This chunk can fit this export
                }
            }
            //Debug.WriteLine($"Chunk {chunkNum} contains {firstElement} to {lastElement}");
            chunks.Add(chunk);

            //Rewrite header with chunk table information so we can position the data blocks after table
            compressedStream.Position = 0;
            package.WriteHeader(compressedStream, compressionType, chunks);
            MemoryStream m1 = new MemoryStream();

            for (int c = 0; c < chunks.Count; c++)
            {
                chunk = chunks[c];
                chunk.compressedOffset = (int)compressedStream.Position;
                chunk.compressedSize = 0; // filled later

                int dataSizeRemainingToCompress = chunk.uncompressedSize;
                int numBlocksInChunk = (int)Math.Ceiling(chunk.uncompressedSize * 1.0 / CompressionHelper.MAX_BLOCK_SIZE);
                // skip chunk header and blocks table - filled later
                compressedStream.Seek(CompressionHelper.SIZE_OF_CHUNK_HEADER + CompressionHelper.SIZE_OF_CHUNK_BLOCK_HEADER * numBlocksInChunk, SeekOrigin.Current);

                uncompressedStream.JumpTo(chunk.uncompressedOffset);

                chunk.blocks = new List<CompressionHelper.Block>();

                //Calculate blocks by splitting data into 128KB "block chunks".
                for (int b = 0; b < numBlocksInChunk; b++)
                {
                    CompressionHelper.Block block = new CompressionHelper.Block();
                    block.uncompressedsize = Math.Min(CompressionHelper.MAX_BLOCK_SIZE, dataSizeRemainingToCompress);
                    dataSizeRemainingToCompress -= block.uncompressedsize;
                    block.uncompressedData = uncompressedStream.ReadToBuffer(block.uncompressedsize);
                    chunk.blocks.Add(block);
                }

                if (chunk.blocks.Count != numBlocksInChunk) throw new Exception("Number of blocks does not match expected amount");

                //Compress blocks
                Parallel.For(0, chunk.blocks.Count, b =>
                {
                    CompressionHelper.Block block = chunk.blocks[b];
                    if (compressionType == CompressionType.LZO)
                        block.compressedData = LZO2.Compress(block.uncompressedData);
                    else if (compressionType == CompressionType.Zlib)
                        block.compressedData = Zlib.Compress(block.uncompressedData);
                    else
                        throw new Exception("Internal error: Unsupported compression type for compressing blocks: " + compressionType);
                    if (block.compressedData.Length == 0)
                        throw new Exception("Internal error: Block compression failed! Compressor returned no bytes");
                    block.compressedsize = (int)block.compressedData.Length;
                    chunk.blocks[b] = block;
                });

                //Write compressed data to stream 
                for (int b = 0; b < numBlocksInChunk; b++)
                {
                    var block = chunk.blocks[b];
                    compressedStream.Write(block.compressedData, 0, (int)block.compressedsize);
                    chunk.compressedSize += block.compressedsize;
                }
                chunks[c] = chunk;
            }

            //Update each chunk header with new information
            foreach (var c in chunks)
            {
                compressedStream.JumpTo(c.compressedOffset); // jump to blocks header
                compressedStream.WriteUInt32(packageTagLittleEndian);
                compressedStream.WriteUInt32(CompressionHelper.MAX_BLOCK_SIZE); //128 KB
                compressedStream.WriteInt32(c.compressedSize);
                compressedStream.WriteInt32(c.uncompressedSize);

                //write block header table
                foreach (var block in c.blocks)
                {
                    compressedStream.WriteInt32(block.compressedsize);
                    compressedStream.WriteInt32(block.uncompressedsize);
                }
            }

            //Write final header
            compressedStream.Position = 0;
            package.WriteHeader(compressedStream, compressionType, chunks);
            return compressedStream;
        }

        /// <summary>
        /// Saves the package to stream. If this saving operation is not going to be committed to disk in the same place as the package was loaded from, you should mark this as a 'save as'.
        /// </summary>
        /// <param name="mePackage"></param>
        /// <param name="isSaveAs"></param>
        /// <returns></returns>
        private static MemoryStream saveByReconstructingToStream(MEPackage mePackage, bool isSaveAs, bool compress)
        {
            if (mePackage.Platform != GamePlatform.PC) throw new Exception("Cannot save packages for platforms other than PC");
            //if (mePackage.Game == MEGame.ME1 && compress) throw new Exception("Cannot save ME1 packages compressed due to texture linking issues");

            var sourceIsCompressed = mePackage.IsCompressed;

            // Set the compression flag that will be saved
            if (!compress)
            {
                mePackage.Flags &= ~EPackageFlags.Compressed;
            }
            else
            {
                mePackage.Flags |= EPackageFlags.Compressed;
            }

            try
            {
                var ms = new MemoryStream();

                //just for positioning. We write over this later when the header values have been updated
                mePackage.WriteHeader(ms);

                //name table
                mePackage.NameOffset = (int)ms.Position;
                mePackage.NameCount = mePackage.Gen0NameCount = mePackage.names.Count;
                foreach (string name in mePackage.names)
                {
                    switch (mePackage.Game)
                    {
                        case MEGame.ME1:
                            ms.WriteUnrealStringASCII(name);
                            ms.WriteInt32(0);
                            ms.WriteInt32(458768);
                            break;
                        case MEGame.ME2:
                            ms.WriteUnrealStringASCII(name);
                            ms.WriteInt32(-14);
                            break;
                        case MEGame.ME3:
                            ms.WriteUnrealStringUnicode(name);
                            break;
                    }
                }

                //import table
                mePackage.ImportOffset = (int)ms.Position;
                mePackage.ImportCount = mePackage.imports.Count;
                foreach (ImportEntry e in mePackage.imports)
                {
                    ms.WriteFromBuffer(e.Header);
                }

                //export table
                mePackage.ExportOffset = (int)ms.Position;
                mePackage.ExportCount = mePackage.Gen0ExportCount = mePackage.exports.Count;
                foreach (ExportEntry e in mePackage.exports)
                {
                    e.HeaderOffset = (uint)ms.Position;
                    ms.WriteFromBuffer(e.Header);
                }

                mePackage.DependencyTableOffset = (int)ms.Position;
                ms.WriteInt32(0); //zero-count DependencyTable
                mePackage.FullHeaderSize = mePackage.ImportExportGuidsOffset = (int)ms.Position;

                //export data
                foreach (ExportEntry e in mePackage.exports)
                {
                    //update offsets
                    ObjectBinary objBin = null;
                    if (!e.IsDefaultObject)
                    {
                        switch (e.ClassName)
                        {
                            case "WwiseBank":
                            case "WwiseStream" when e.GetProperty<NameProperty>("Filename") == null:
                            case "TextureMovie" when e.GetProperty<NameProperty>("TextureFileCacheName") == null:
                                objBin = ObjectBinary.From(e);
                                break;
                            case "ShaderCache":
                                UpdateShaderCacheOffsets(e, (int)ms.Position);
                                break;
                        }
                    }

                    e.DataOffset = (int)ms.Position;
                    if (objBin != null)
                    {
                        e.WriteBinary(objBin);
                    }



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
                mePackage.WriteHeader(ms);

                if (compress)
                    return compressPackage(mePackage, ms);
                return ms;
            }
            finally
            {
                //If we're doing save as, reset compressed flag to reflect file on disk as we still point to the original one
                if (isSaveAs)
                {
                    if (sourceIsCompressed)
                    {
                        mePackage.Flags |= EPackageFlags.Compressed;
                    }
                    else
                    {
                        mePackage.Flags &= ~EPackageFlags.Compressed;
                    }
                }
            }
        }

        public MemoryStream SaveToStream(bool compress = false)
        {
            return saveByReconstructingToStream(this, true, compress);
        }

        private static void UpdateShaderCacheOffsets(ExportEntry export, int newDataOffset)
        {
            int oldDataOffset = export.DataOffset;
            switch (export.Game)
            {
                case MEGame.ME2:
                    {
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
                        break;
                    }
                case MEGame.ME3:
                    {
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
                        break;
                    }
            }
        }

        private void WriteHeader(Stream ms, CompressionType compressionType = CompressionType.None, List<CompressionHelper.Chunk> chunks = null)
        {
            ms.WriteUInt32(packageTagLittleEndian);
            //version
            switch (Game)
            {
                case MEGame.ME1:
                    ms.WriteUInt16(ME1UnrealVersion);
                    ms.WriteUInt16(ME1LicenseeVersion);
                    break;
                case MEGame.ME2:
                    ms.WriteUInt16(ME2UnrealVersion);
                    ms.WriteUInt16(ME2LicenseeVersion);
                    break;
                case MEGame.ME3:
                    ms.WriteUInt16(ME3UnrealVersion);
                    ms.WriteUInt16(ME3LicenseeVersion);
                    break;
            }
            ms.WriteInt32(FullHeaderSize);
            if (Game == MEGame.ME3)
            {
                ms.WriteUnrealStringUnicode("None");
            }
            else
            {
                ms.WriteUnrealStringASCII("None");
            }

            ms.WriteUInt32((uint)Flags);

            if (Game == MEGame.ME3 && Flags.HasFlag(EPackageFlags.Cooked))
            {
                ms.WriteInt32(PackageTypeId);
            }

            ms.WriteInt32(NameCount);
            ms.WriteInt32(NameOffset);
            ms.WriteInt32(ExportCount);
            ms.WriteInt32(ExportOffset);
            ms.WriteInt32(ImportCount);
            ms.WriteInt32(ImportOffset);
            ms.WriteInt32(DependencyTableOffset);

            if (Game == MEGame.ME3)
            {
                ms.WriteInt32(ImportExportGuidsOffset);
                ms.WriteInt32(0); //ImportGuidsCount
                ms.WriteInt32(0); //ExportGuidsCount
                ms.WriteInt32(0); //ThumbnailTableOffset
            }
            ms.WriteGuid(PackageGuid);

            //Write 1 generation
            ms.WriteInt32(1);
            ms.WriteInt32(Gen0ExportCount);
            ms.WriteInt32(Gen0NameCount);
            ms.WriteInt32(Gen0NetworkedObjectCount);

            //engineVersion and cookedContentVersion
            switch (Game)
            {
                case MEGame.ME1:
                    ms.WriteInt32(3240);
                    ms.WriteInt32(47);
                    break;
                case MEGame.ME2:
                    ms.WriteInt32(3607);
                    ms.WriteInt32(64);
                    break;
                case MEGame.ME3:
                    ms.WriteInt32(6383);
                    ms.WriteInt32(196715);
                    break;
            }


            if (Game == MEGame.ME2 || Game == MEGame.ME1)
            {
                ms.WriteInt32(0);
                ms.WriteInt32(47699); //No idea what this is, but it's always 47699
                switch (Game)
                {
                    case MEGame.ME1:
                        ms.WriteInt32(0);
                        ms.WriteInt32(1);
                        break;
                    case MEGame.ME2:
                        ms.WriteInt32(unknown4);
                        ms.WriteInt32(1966080);
                        break;
                }
            }

            switch (Game)
            {
                case MEGame.ME1:
                    ms.WriteInt32(0);
                    ms.WriteInt32(-1);
                    break;
                case MEGame.ME2:
                    ms.WriteInt32(-1);
                    ms.WriteInt32(-1);
                    break;
                case MEGame.ME3:
                    ms.WriteInt32(unknown6);
                    ms.WriteInt32(145358848);
                    break;
            }

            if (Game == MEGame.ME1)
            {
                ms.WriteInt32(-1);
            }


            if (chunks == null || !chunks.Any() || compressionType == CompressionType.None)
            {
                // No compress
                ms.WriteUInt32((uint)CompressionType.None);
                ms.WriteInt32(0); //numChunks
            }
            else
            {
                ms.WriteUInt32((uint)compressionType);
                //Chunks
                ms.WriteInt32(chunks.Count);
                int i = 0;
                foreach (var chunk in chunks)
                {
                    ms.WriteInt32(chunk.uncompressedOffset);
                    ms.WriteInt32(chunk.uncompressedSize);
                    ms.WriteInt32(chunk.compressedOffset);
                    if (chunk.blocks != null)
                    {
                        var chunksize = chunk.compressedSize + CompressionHelper.SIZE_OF_CHUNK_HEADER + CompressionHelper.SIZE_OF_CHUNK_BLOCK_HEADER * chunk.blocks.Count;
                        //Debug.WriteLine($"Writing chunk table chunk {i} size: {chunksize}");
                        ms.WriteInt32(chunksize); //Size of compressed data + chunk header + block header * number of blocks in the chunk
                    }
                    else
                    {
                        //list is null - might not be populated yet
                        ms.WriteInt32(0); //write zero for now, we will call this method later with the compressedSize populated.
                    }
                    i++;
                }
            }

            ms.WriteUInt32(packageSource);

            if (Game == MEGame.ME2 || Game == MEGame.ME1)
            {
                ms.WriteInt32(0);
            }

            if (Game == MEGame.ME3 || Game == MEGame.ME2)
            {
                ms.WriteInt32(AdditionalPackagesToCook.Count);
                foreach (var pname in AdditionalPackagesToCook)
                {
                    if (Game == MEGame.ME2)
                    {
                        //ME2 Uses ASCII
                        ms.WriteUnrealStringASCII(pname);
                    }
                    else
                    {
                        ms.WriteUnrealStringUnicode(pname);
                    }
                }
            }
        }
        private void ReadLocalTLKs()
        {
            LocalTalkFiles.Clear();
            var exportsToLoad = new List<ExportEntry>();
            foreach (var tlkFileSet in Exports.Where(x => x.ClassName == "BioTlkFileSet" && !x.IsDefaultObject).Select(exp => exp.GetBinaryData<BioTlkFileSet>()))
            {
                foreach ((NameReference lang, BioTlkFileSet.BioTlkSet bioTlkSet) in tlkFileSet.TlkSets)
                {
                    if (CoreLibSettings.Instance.TLKDefaultLanguage.Equals(lang, StringComparison.InvariantCultureIgnoreCase))
                    {
                        exportsToLoad.Add(GetUExport(CoreLibSettings.Instance.TLKGenderIsMale ? bioTlkSet.Male : bioTlkSet.Female));
                        break;
                    }
                }
            }

            foreach (var exp in exportsToLoad)
            {
                //Debug.WriteLine("Loading local TLK: " + exp.GetIndexedFullPath);
                LocalTalkFiles.Add(new ME1TalkFile(exp));
            }
        }

        /// <summary>
        /// Sets the game for this MEPackage. DO NOT USE THIS UNLESS YOU ABSOLUTELY KNOW WHAT YOU ARE DOING
        /// </summary>
        /// <param name="newGame"></param>
        public void setGame(MEGame newGame)
        {
            Game = newGame;
        }

        /// <summary>
        /// Sets the platform for this MEPackage. DO NOT USE THIS UNLESS YOU ABSOLUTELY KNOW WHAT YOU ARE DOING.
        /// CHANGING THE PLATFORM TO ATTEMPT TO SAVE A CONSOLE FILE WILL NOT PRODUCE A USABLE CONSOLE FILE
        /// </summary>
        /// <param name="newPlatform"></param>
        internal void setPlatform(GamePlatform newPlatform)
        {
            Platform = newPlatform;
        }
    }
}