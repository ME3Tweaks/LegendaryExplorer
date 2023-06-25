using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Newtonsoft.Json;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
#if AZURE
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace LegendaryExplorerCore.Packages
{
    public sealed class MEPackage : UnrealPackageFile, IMEPackage, IDisposable
    {
        /// <summary>
        /// MEM writes this to every single package file it modifies
        /// </summary>
        private const string MEMPackageTag = "ThisIsMEMEndOfFileMarker"; //TODO NET 7: make this a utf8 literal
        private const int MEMPackageTagLength = 24;

        /// <summary>
        /// LEC-saved LE packages will always end in this, assuming MEM did not save later
        /// </summary>
        private const string LECPackageTag = "LECL"; //TODO NET 7: make this a utf8 literal
        private const int LECPackageTagLength = 4;
        private const int LECPackageTag_Version_EmptyData = 1;
        private const int LECPackageTag_Version_JSON = 2;


        /// <summary>
        /// Player.sav in ME1 save files starts with this and needs to be scrolled forward to find actual start of package
        /// </summary>
        public const uint ME1SavePackageTag = 0x484D4752; // 'RGMH'

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

        // LEGENDARY EDITION
        public const ushort LE1UnrealVersion = 684;
        public const ushort LE1LicenseeVersion = 171;

        public const ushort LE2UnrealVersion = 684;
        public const ushort LE2LicenseeVersion = 168;

        // PS4 Orbis uses this version information too
        public const ushort LE3UnrealVersion = 685;
        public const ushort LE3LicenseeVersion = 205;

        /// <summary>
        /// Indicates what type of package file this is. 0 is normal, 1 is TESTPATCH patch package.
        /// </summary>
        public int PackageTypeId { get; }

        /// <summary>
        /// This is not useful for modding but we should not be changing the format of the package file.
        /// </summary>
        public readonly List<string> AdditionalPackagesToCook = new();

        /// <summary>
        /// Passthrough to UnrealPackageFile's IsModified
        /// </summary>
        bool IMEPackage.IsModified
        {
            // Not sure why I can't use a private setter here.
            get => IsModified;
            set => IsModified = value;
        }

        public bool IsMemoryPackage { get; set; }

        public Endian Endian { get; }
        public MEGame Game { get; private set; } //can only be ME1, ME2, ME3, LE1, LE2, LE3. UDK is a separate class
        public GamePlatform Platform { get; private set; }

        public enum GamePlatform
        {
            Unknown, //Unassigned
            PC,
            Xenon,
            PS3,
            WiiU
        }

        public MELocalization Localization { get; } = MELocalization.None;

        /// <summary>
        /// Custom user-defined metadata to associate with this package object. This data has no effect on saving or loading, it is only for library user convenience. This data is NOT serialized to disk!
        /// </summary>
        public Dictionary<string, object> CustomMetadata { get; set; } = new(0);

        /// <summary>
        /// Metadata that is serialized to the end of the package file and contains useful information for tooling
        /// </summary>
        public LECLData LECLTagData { get; }

        public byte[] getHeader()
        {
            using var ms = MemoryManager.GetMemoryStream();
            WriteHeader(ms, includeAdditionalPackageToCook: true);
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

        private static bool _isBlankPackageCreatorRegistered;
        private static bool _isStreamLoaderRegistered;
        public static Func<string, MEGame, MEPackage> RegisterBlankPackageCreator()
        {
            if (_isBlankPackageCreatorRegistered)
            {
                throw new Exception(nameof(MEPackage) + " can only be initialized once");
            }

            _isBlankPackageCreatorRegistered = true;
            return (f, g) => new MEPackage(g, f);
        }

        public static Func<Stream, string, bool, Func<ExportEntry, bool>, MEPackage> RegisterStreamLoader()
        {
            if (_isStreamLoaderRegistered)
            {
                throw new Exception(nameof(MEPackage) + " streamloader can only be initialized once");
            }

            _isStreamLoaderRegistered = true;
            return (s, associatedFilePath, onlyheader, dataLoadPredicate) => new MEPackage(s, associatedFilePath, onlyheader, dataLoadPredicate);
        }

        /// <summary>
        /// Gets a decompressed stream of a package. Mixin rules makes it follow the following rules if the package is compressed and needs to be decompressed:
        /// 1. Additional packages to cook is not written to the stream.
        /// 2. Dependency table is included.
        /// If the package is not compressed, the additional packages header is written.
        /// ME3CMM decompression code was based on ME3Exp 2.0 which would do this when decompressing files. If a file was already decompressed, it would not modify it, so it did not affect SFAR files.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="mixinRules"></param>
        /// <returns></returns>
        public static MemoryStream GetDecompressedPackageStream(MemoryStream stream, bool includeAdditionalPackagesToCook = true, bool includeDependencyTable = true)
        {
            var package = MEPackageHandler.OpenMEPackageFromStream(stream);
            return package.SaveToStream(false, includeAdditionalPackagesToCook, includeDependencyTable);
        }

        /// <summary>
        /// Creates a new blank MEPackage object.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="filePath"></param>
        private MEPackage(MEGame game, string filePath = null) : base(filePath != null ? Path.GetFullPath(filePath) : null)
        {
            names = new List<string>();
            imports = new List<ImportEntry>();
            exports = new List<ExportEntry>();
            //new Package
            Game = game;
            Platform = GamePlatform.PC; //Platform must be set or saving code will throw exception (cannot save non-PC platforms)
            //reasonable defaults?
            Flags = EPackageFlags.Cooked | EPackageFlags.AllowDownload | EPackageFlags.DisallowLazyLoading | EPackageFlags.RequireImportsAlreadyLoaded;
            EntryLookupTable = new CaseInsensitiveDictionary<IEntry>();
            LECLTagData = new LECLData();
        }

        /// <summary>
        /// Opens an ME package from the stream. If this file is from a disk, the filePath should be set to support saving and other lookups.
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="filePath"></param>
        /// <param name="onlyHeader">Only read header data. Do not load the tables or decompress</param>
        /// <param name="dataLoadPredicate">If provided, export data will only be read for exports that match the predicate</param>
        private MEPackage(Stream fs, string filePath = null, bool onlyHeader = false, Func<ExportEntry, bool> dataLoadPredicate = null) : base(filePath != null ? File.Exists(filePath) ? Path.GetFullPath(filePath) : filePath : null)
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

            if ((versionLicenseePacked is 0x00020000 or 0x00010000) && Endian == Endian.Little)
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
                case LE1UnrealVersion when licenseeVersion == LE1LicenseeVersion:
                    Game = MEGame.LE1;
                    Platform = GamePlatform.PC;
                    break;
                case LE2UnrealVersion when licenseeVersion == LE2LicenseeVersion:
                    Game = MEGame.LE2;
                    Platform = GamePlatform.PC;
                    break;
                case LE3UnrealVersion when licenseeVersion == LE3LicenseeVersion:
                    Game = MEGame.LE3;
                    Platform = GamePlatform.PC;
                    break;
                default:
                    throw new FormatException("Not a Mass Effect Package!");
            }

            if (Game.IsLEGame() && filePath != null && Path.GetExtension(filePath) == ".xxx")
            {
                // There is no way to differentiate Orbis vs Durango so we will just mark it as 
                // Orbis.
                Platform = GamePlatform.PS3; // Just use PS3 flag for now.
            }

            FullHeaderSize = packageReader.ReadInt32();
            int foldernameStrLen = packageReader.ReadInt32();
            //always "None", so don't bother saving result
            if (foldernameStrLen > 0)
                fs.ReadStringLatin1Null(foldernameStrLen);
            else
                fs.ReadStringUnicodeNull(foldernameStrLen * -2);

            Flags = (EPackageFlags)packageReader.ReadUInt32();

            //Xenon Demo ME3 doesn't read this. Xenon ME3 Retail does
            if (Game is MEGame.ME3 or MEGame.LE3
                && (Flags.Has(EPackageFlags.Cooked) || Platform != GamePlatform.PC) && licenseeVersion != ME3Xenon2011DemoLicenseeVersion)
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


            if (Game.IsLEGame() || Game != MEGame.ME1 || Platform != GamePlatform.Xenon)
            {
                // Seems this doesn't exist on ME1 Xbox
                DependencyTableOffset = packageReader.ReadInt32();
            }

            if (Game.IsLEGame() || Game == MEGame.ME3 || Platform == GamePlatform.PS3)
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

            //if (Game != MEGame.LE1)
            //{
            packageReader.SkipInt32(); //engineVersion          Like unrealVersion and licenseeVersion, these 2 are determined by what game this is,
            packageReader.SkipInt32(); //cookedContentVersion   so we don't have to read them in

            if ((Game is MEGame.ME2 or MEGame.ME1) && Platform != GamePlatform.PS3) //PS3 on ME3 engine
            {
                packageReader.SkipInt32(); //always 0
                packageReader.SkipInt32(); //always 47699
                unknown4 = packageReader.ReadInt32();
                packageReader.SkipInt32(); //always 1 in ME1, always 1966080 in ME2
            }

            unknown6 = packageReader.ReadInt32(); // Build 
            packageReader.SkipInt32(); // Branch

            if (Game == MEGame.ME1 && Platform != GamePlatform.PS3)
            {
                packageReader.SkipInt32(); //always -1
            }
            //}
            //else
            //{
            //    packageReader.Position += 0x14; // Skip an unkonwn 14 bytes we will figure out later
            //}

            //COMPRESSION AND COMPRESSION CHUNKS
            var compressionFlagPosition = packageReader.Position;
            var compressionType = (CompressionType)packageReader.ReadInt32();
            if (platformNeedsResolved && compressionType != CompressionType.None)
            {
                Platform = compressionType == CompressionType.LZX ? GamePlatform.Xenon : GamePlatform.PS3;
                platformNeedsResolved = false;
            }

            //Debug.WriteLine($"Compression type {filePath}: {compressionType}");
            NumCompressedChunksAtLoad = packageReader.ReadInt32();

            //read package source
            var savedPos = packageReader.Position;
            packageReader.Skip(NumCompressedChunksAtLoad * 16); //skip chunk table so we can find package tag



            packageSource = packageReader.ReadUInt32(); //this needs to be read in so it can be properly written back out.

            if ((Game is MEGame.ME2 or MEGame.ME1) && Platform != GamePlatform.PS3)
            {
                packageReader.SkipInt32(); //always 0
            }

            //Doesn't need to be written out, so it doesn't need to be read in
            //keep this here in case one day we learn that this has a purpose
            //Narrator: On Jan 26, 2020 it turns out this was actually necessary to make it work
            //with ME3Tweaks Mixins as old code did not remove this section
            //Also we should strive to ensure closeness to the original source files as possible
            //because debugging things is a huge PITA if you start to remove stuff
            if (Game is MEGame.ME2 or MEGame.ME3 || Game.IsLEGame() || Platform == GamePlatform.PS3)
            {
                int additionalPackagesToCookCount = packageReader.ReadInt32();
                for (int i = 0; i < additionalPackagesToCookCount; i++)
                {
                    var packageStr = packageReader.ReadUnrealString();
                    AdditionalPackagesToCook.Add(packageStr);
                }
            }

            if (onlyHeader) return; // That's all we need to parse. 
            #endregion

            #region Decompression of package data

            //determine if tables are in order.
            //The < 0x500 is just to check that the tables are all at the start of the file. (will never not be the case for unedited files, but for modded ones, all things are possible)
            bool tablesInOrder = NameOffset < 0x500 && NameOffset < ImportOffset && ImportOffset < ExportOffset;

            packageReader.Position = savedPos; //restore position to chunk table
            Stream inStream = fs;
            bool wasOriginallyCompressed = false; // Used to know if we should dispose the stream used for decompressed data
            if (IsCompressed && NumCompressedChunksAtLoad > 0)
            {
                wasOriginallyCompressed = true;
                inStream = CompressionHelper.DecompressPackage(packageReader, compressionFlagPosition, game: Game, platform: Platform,
                                                               canUseLazyDecompression: tablesInOrder && !platformNeedsResolved);
            }
            #endregion

            var endian = packageReader.Endian;
            packageReader = new EndianReader(inStream) { Endian = endian };
            //read namelist
            inStream.JumpTo(NameOffset);
            names = new List<string>(NameCount);
            nameLookupTable.EnsureCapacity(NameCount);
            if (Game > MEGame.ME2 && inStream is CompressionHelper.PackageDecompressionStreamBase packageDecompressionStream)
            {
                for (int i = 0; i < NameCount; i++)
                {
                    string name = packageDecompressionStream.ReadUnrealStringLittleEndianFast();
                    names.Add(name);
                    nameLookupTable[name] = i;
                }
            }
            else
            {
                for (int i = 0; i < NameCount; i++)
                {
                    string name = packageReader.ReadUnrealString();
                    names.Add(name);
                    nameLookupTable[name] = i;
                    if (Game == MEGame.ME1 && Platform != GamePlatform.PS3)
                        inStream.Skip(8);
                    else if (Game == MEGame.ME2 && Platform != GamePlatform.PS3)
                        inStream.Skip(4);
                }
            }

            //read importTable
            inStream.JumpTo(ImportOffset);
            imports = new List<ImportEntry>(ImportCount);

            //explicitly creating the delegate outside the loop avoids allocating a new delegate for every import
            var importChangedHandler = new PropertyChangedEventHandler(importChanged);
            for (int i = 0; i < ImportCount; i++)
            {
                var imp = new ImportEntry(this, packageReader) { Index = i };
                if (MEPackageHandler.GlobalSharedCacheEnabled)
                {
                    imp.PropertyChanged += importChangedHandler; // If packages are not shared there is no point to attaching this
                }
                imports.Add(imp);
            }

            //read exportTable
            inStream.JumpTo(ExportOffset);
            exports = new List<ExportEntry>(ExportCount);

            //explicitly creating the delegate outside the loop avoids allocating a new delegate for every import
            var exportChangedHandler = new PropertyChangedEventHandler(exportChanged);
            for (int i = 0; i < ExportCount; i++)
            {
                var e = new ExportEntry(this, packageReader, false) { Index = i };
                if (MEPackageHandler.GlobalSharedCacheEnabled)
                {
                    e.PropertyChanged += exportChangedHandler; // If packages are not shared there is no point to attaching this
                }
                exports.Add(e);
                if (platformNeedsResolved && e.ClassName == "ShaderCache")
                {
                    // Read the first binary byte, it's a platform flag
                    // 0 = PC
                    // 1 = PS3
                    // 2 = Xenon
                    // 5 = WiiU / SM5 (LE)
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

            foreach (ExportEntry export in dataLoadPredicate is null ? exports : exports.Where(dataLoadPredicate))
            {
                inStream.JumpTo(export.DataOffset);
                var data = new byte[export.DataSize];
                int bytesRead = inStream.Read(data.AsSpan());
                if (bytesRead != data.Length)
                {
                    throw new EndOfStreamException("Attempted to read export data past the end of the stream!");
                }
                export.Data = data;
            }

            if (Game.IsLEGame())
            {
                //read from the original stream here, as this data is not part of the compressed data and will always be little endian
                try
                {
                    // Find MEM tag to see if it exists since it will append to ours (LEC will not save with a MEM tag)

                    bool taggedByMEM = false;
                    bool taggedByLEC = false;
                    string leclv2Data = null;
                    if (fs.Position != fs.Length) //optimize for the case where it's not tagged
                    {
                        long tagOffsetFromEnd = -LECPackageTagLength; 
                        fs.Seek(-MEMPackageTagLength, SeekOrigin.End);
                        if (fs.ReadStringASCII(MEMPackageTagLength) == MEMPackageTag)
                        {
                            taggedByMEM = true;
                            tagOffsetFromEnd -= MEMPackageTagLength;
                            LECLTagData = new LECLData { WasSavedWithMEM = true };
                        }

                        fs.Seek(tagOffsetFromEnd, SeekOrigin.End);
                        if (fs.ReadStringASCII(LECPackageTagLength) == LECPackageTag)
                        {
                            taggedByLEC = true;

                            // Read <LECL Data>
                            fs.Seek(-(LECPackageTagLength + sizeof(int)), SeekOrigin.Current); //seek to payload length
                            int payloadLength = fs.ReadInt32();

                            //Read version
                            fs.Seek(-(sizeof(int) + payloadLength), SeekOrigin.Current); // Seek to version
                            int leclVersion = fs.ReadInt32();

                            if (leclVersion >= LECPackageTag_Version_JSON)
                            {
                                leclv2Data = fs.ReadStringUtf8(payloadLength - 4);
                            }
                        }
                    }

                    LECLTagData = (leclv2Data != null ? JsonConvert.DeserializeObject<LECLData>(leclv2Data) : new LECLData()) ?? new LECLData(); // This prevents invalid parsing of LECLTagData from causing package to be unable to save
                    LECLTagData.WasSavedWithMEM = taggedByMEM;
                    LECLTagData.WasSavedWithLEC = taggedByLEC;
                }
                catch (Exception e)
                {
                    LECLog.Error($"Error reading LECLDataTag on package: {e.Message}. The data will not be deserialized.");
                }
            }

            if (wasOriginallyCompressed)
            {
                // Do not dispose if the package was compressed as it will close the input stream
                packageReader.Dispose();
            }


            if (filePath != null)
            {
                Localization = filePath.GetUnrealLocalization();
            }

            //Allocate the lookup table. It is initialized on an if-needed basis.
            EntryLookupTable = new CaseInsensitiveDictionary<IEntry>(ExportCount + ImportCount);
#if AZURE
            if (platformNeedsResolved)
            {
                Assert.Fail($"Package platform was not resolved! Package file: {FilePath}");
            }
#endif
        }



        public static Action<MEPackage, string, bool, bool, bool, bool, object> RegisterSaver() => saveByReconstructing;

        /// <summary>
        /// Saves the package to disk by reconstructing the package file
        /// </summary>
        /// <param name="mePackage"></param>
        /// <param name="path"></param>
        /// <param name="isSaveAs"></param>
        /// <param name="compress"></param>
        private static void saveByReconstructing(MEPackage mePackage, string path, bool isSaveAs, bool compress, bool includeAdditionalPackagesToCook, bool includeDependencyTable, object diskIOSyncLockObject = null)
        {
            //var sw = Stopwatch.StartNew();
            using var saveStream = compress
                ? saveCompressed(mePackage, isSaveAs, includeAdditionalPackagesToCook, includeDependencyTable)
                : saveUncompressed(mePackage, isSaveAs, includeAdditionalPackagesToCook, includeDependencyTable);

            // Lock writing with the sync object (if not null) to prevent disk concurrency issues
            // (the good old 'This file is in use by another process' message)
            if (diskIOSyncLockObject == null)
            {
                saveStream.WriteToFile(path ?? mePackage.FilePath);
            }
            else
            {
                lock (diskIOSyncLockObject)
                {
                    saveStream.WriteToFile(path ?? mePackage.FilePath);
                }
            }

            if (!isSaveAs)
            {
                mePackage.AfterSave();
            }
            //var milliseconds = sw.ElapsedMilliseconds;
            //Debug.WriteLine($"Saved {Path.GetFileName(path)} in {milliseconds}");
            //sw.Stop();
        }

        /// <summary>
        /// Saves the package to stream. If this saving operation is not going to be committed to disk in the same place as the package was loaded from, you should mark this as a 'save as'.
        /// </summary>
        /// <param name="mePackage"></param>
        /// <param name="includeAdditionalPackageToCook"></param>
        /// <param name="includeDependencyTable"></param>
        /// <returns></returns>
        private static MemoryStream saveUncompressed(MEPackage mePackage, bool isSaveAs, bool includeAdditionalPackageToCook = true, bool includeDependencyTable = true)
        {
            if (mePackage.Platform != GamePlatform.PC) throw new Exception("Cannot save packages for platforms other than PC");
            var sourceIsCompressed = mePackage.IsCompressed;
            mePackage.Flags &= ~EPackageFlags.Compressed;

            //calculate total size, to prevent MemoryStream re-sizing
            int nameTableSize = mePackage.names.Sum(name => name.Length);
            switch (mePackage.Game)
            {
                case MEGame.ME1:
                    nameTableSize += 13 * mePackage.NameCount;
                    break;
                case MEGame.ME2:
                    nameTableSize += 9 * mePackage.NameCount;
                    break;
                case MEGame.ME3:
                case MEGame.LE3:
                    nameTableSize = nameTableSize * 2 + 6 * mePackage.NameCount;
                    break;
                case MEGame.LE1:
                case MEGame.LE2:
                    nameTableSize += 5 * mePackage.NameCount;
                    break;
            }

            int importTableSize = mePackage.imports.Count * ImportEntry.HeaderLength;
            int exportTableSize = mePackage.exports.Sum(exp => exp.HeaderLength);
            int dependencyTableSize = includeDependencyTable ? mePackage.ExportCount * 4 : 4;
            int totalSize = 500 //fake header size. will mean allocating a few hundred extra bytes, but that's not a huge deal.
                          + nameTableSize
                          + importTableSize
                          + exportTableSize
                          + dependencyTableSize
                          + mePackage.exports.Sum(exp => exp.DataSize);


            var ms = MemoryManager.GetMemoryStream(totalSize);

            //just for positioning. We write over this later when the header values have been updated
            mePackage.WriteHeader(ms, includeAdditionalPackageToCook: includeAdditionalPackageToCook);

            //name table
            mePackage.NameOffset = (int)ms.Position;
            mePackage.NameCount = mePackage.Gen0NameCount = mePackage.names.Count;
            foreach (string name in mePackage.names)
            {
                switch (mePackage.Game)
                {
                    case MEGame.ME1:
                        ms.WriteUnrealStringLatin1(name);
                        ms.WriteInt32(0);
                        ms.WriteInt32(458768);
                        break;
                    case MEGame.ME2:
                        ms.WriteUnrealStringLatin1(name);
                        ms.WriteInt32(-14);
                        break;
                    case MEGame.ME3:
                    case MEGame.LE3:
                        ms.WriteUnrealStringUnicode(name);
                        break;
                    case MEGame.LE1:
                    case MEGame.LE2:
                        ms.WriteUnrealStringLatin1(name);
                        break;
                }
            }

            //import table
            mePackage.ImportOffset = (int)ms.Position;
            mePackage.ImportCount = mePackage.imports.Count;
            foreach (ImportEntry imp in mePackage.imports)
            {
                imp.SerializeHeader(ms);
            }

            //export table
            mePackage.ExportOffset = (int)ms.Position;
            mePackage.ExportCount = mePackage.Gen0ExportCount = mePackage.exports.Count;
            foreach (ExportEntry exp in mePackage.exports)
            {
                exp.HeaderOffset = (int)ms.Position;
                exp.SerializeHeader(ms);
            }

            mePackage.DependencyTableOffset = (int)ms.Position;

            if (includeDependencyTable)
            {
                //Unreal Engine style (keeping in line with the package specification)
                //write the table out. No count for this table.
                ms.Position += mePackage.ExportCount * 4; //no need to allocate an array then copy a bunch of zeros, setting position past Length will call Array.Clear
            }
            else
            {
                //ME3EXP STYLE -BLANK(?) table
                ms.WriteInt32(0); //Technically this is not a count. The count is the number of exports. but this is just for consistency with ME3Exp.
            }

            mePackage.FullHeaderSize = mePackage.ImportExportGuidsOffset = (int)ms.Position;

            //export data
            foreach (ExportEntry e in mePackage.exports)
            {
                int oldDataOffset = e.DataOffset;

                // Update the header position
                //needs to be updated BEFORE the offsets are updated 
                e.DataOffset = (int)ms.Position;

                UpdateOffsets(e, oldDataOffset);

                ms.Write(e.DataReadOnly);
                //update size and offset in already-written header
                long pos = ms.Position;
                ms.JumpTo(e.HeaderOffset + 32);
                ms.WriteInt32(e.DataSize);
                ms.WriteInt32(e.DataOffset);
                ms.JumpTo(pos);
            }

            if (mePackage.Game.IsLEGame())
            {
                WriteLegendaryExplorerCoreTag(ms, mePackage);
            }

            ms.JumpTo(0);
            //re-write header with updated values
            mePackage.WriteHeader(ms, includeAdditionalPackageToCook: includeAdditionalPackageToCook);

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

            return ms;
        }

        private static MemoryStream saveCompressed(MEPackage package, bool isSaveAs, bool includeAdditionalPackageToCook = true, bool includeDependencyTable = true)
        {
            if (package.Platform != GamePlatform.PC) throw new Exception("Cannot save packages for platforms other than PC");
            var sourceIsCompressed = package.IsCompressed;
            try
            {
                package.Flags |= EPackageFlags.Compressed;

                int nameTableSize = 0;

                // Null or empty names do not use a terminator
                // As such we have to account for them or saving a compressed package will yield a broken file.
                int validNameTableSize = 0;
                int invalidNameCount = 0;
                int validNameCount = 0;

                foreach (var v in package.names)
                {
                    if (v.Length > 0)
                    {
                        validNameTableSize += v.Length;
                        validNameCount++;
                    }
                    else
                    {
                        invalidNameCount++;
                    }
                }


                switch (package.Game)
                {
                    case MEGame.ME1:
                        nameTableSize = validNameTableSize /* ascii */ + 13 * validNameCount; // size (4) + null terminator (1) + 2 (4) unknowns (0, 458768)
                        nameTableSize += 4 * invalidNameCount; // 4 bytes for size and nothing else. Null and empty strings are just the length of 0
                        break;
                    case MEGame.ME2:
                        nameTableSize = validNameTableSize /* ascii */ + 9 * validNameCount; // size (4) + null terminator (1) + unknown (4) (as -14) 
                        nameTableSize += 4 * invalidNameCount; // 4 bytes for size and nothing else. Null and empty strings are just the length of 0
                        break;
                    case MEGame.ME3:
                    case MEGame.LE3:
                        // UNICODE
                        nameTableSize = validNameTableSize * 2 /* x2 for unicode */ + 6 * validNameCount; // size (4), null terminator (2 in unicode)
                        nameTableSize += 4 * invalidNameCount; // 4 bytes for size and nothing else. Null and empty strings are just the length of 0
                        break;
                    case MEGame.LE1:
                    case MEGame.LE2:
                        nameTableSize = validNameTableSize /* ascii */ + 5 * validNameCount;
                        nameTableSize += 4 * invalidNameCount; // 4 bytes for size and nothing else. Null and empty strings are just the length of 0
                        break;
                }


                int importTableSize = package.imports.Count * ImportEntry.HeaderLength;
                int exportTableSize = package.exports.Sum(exp => exp.HeaderLength);
                int dependencyTableSize = (includeDependencyTable ? package.ExportCount * 4 : 4);
                int totalSize = 500 //fake header size.
                              + nameTableSize
                              + importTableSize
                              + exportTableSize
                              + dependencyTableSize
                              + package.exports.Sum(exp => exp.DataSize);

                //This will be enough to prevent resizing for 84% of LE files, and only 2% will have to resize twice
                int compressedLengthEstimate = (int)(totalSize * 0.4);
                var compressedStream = MemoryManager.GetMemoryStream(compressedLengthEstimate);
                package.WriteHeader(compressedStream, includeAdditionalPackageToCook: includeAdditionalPackageToCook);

                //calculate all offsets
                package.NameOffset = (int)compressedStream.Position;
                package.NameCount = package.Gen0NameCount = package.names.Count;
                package.ImportOffset = package.NameOffset + nameTableSize;
                package.ImportCount = package.imports.Count;
                package.ExportOffset = package.ImportOffset + importTableSize;
                package.ExportCount = package.Gen0ExportCount = package.exports.Count;
                int offset = package.ExportOffset;
                foreach (ExportEntry export in package.exports)
                {
                    export.HeaderOffset = offset;
                    offset += export.HeaderLength;
                }
                package.DependencyTableOffset = offset;
                package.FullHeaderSize = package.ImportExportGuidsOffset = offset + dependencyTableSize;

                //calculate chunks
                int numChunksRoughGuess = (int)BitOperations.RoundUpToPowerOf2((uint)(totalSize / CompressionHelper.MAX_CHUNK_SIZE + 1));
                var chunks = new List<CompressionHelper.Chunk>(numChunksRoughGuess);
                var compressionType = package.Game switch
                {
                    MEGame.ME3 => CompressionType.Zlib,
                    MEGame.LE1 => CompressionType.OodleLeviathan,
                    MEGame.LE2 => CompressionType.OodleLeviathan,
                    MEGame.LE3 => CompressionType.OodleLeviathan,
                    _ => CompressionType.LZO
                };
                var maxBlockSize = package.Game.IsOTGame() ? CompressionHelper.MAX_BLOCK_SIZE_OT : CompressionHelper.MAX_BLOCK_SIZE_LE;
                //Tables chunk
                var chunk = new CompressionHelper.Chunk
                {
                    uncompressedSize = package.FullHeaderSize - package.NameOffset,
                    uncompressedOffset = package.NameOffset
                };
                int actualMaxChunkSize = chunk.uncompressedSize;
                //Export data chunks
                offset = package.FullHeaderSize;
                foreach (ExportEntry e in package.exports)
                {
                    int exportDataSize = e.DataSize;
                    int oldExportOffset = e.DataOffset;
                    e.DataOffset = offset;
                    offset += exportDataSize;

                    UpdateOffsets(e, oldExportOffset);

                    if (chunk.uncompressedSize + exportDataSize > CompressionHelper.MAX_CHUNK_SIZE)
                    {
                        //Rollover to the next chunk as this chunk would be too big if we tried to put this export into the chunk
                        actualMaxChunkSize = Math.Max(actualMaxChunkSize, chunk.uncompressedSize);
                        chunks.Add(chunk);
                        chunk = new CompressionHelper.Chunk
                        {
                            uncompressedSize = exportDataSize,
                            uncompressedOffset = e.DataOffset
                        };
                    }
                    else
                    {
                        chunk.uncompressedSize += exportDataSize; //This chunk can fit this export
                    }
                }
                actualMaxChunkSize = Math.Max(actualMaxChunkSize, chunk.uncompressedSize);
                chunks.Add(chunk);

                //Rewrite header with chunk table information so we can position the data blocks after table
                compressedStream.Position = 0;
                package.WriteHeader(compressedStream, compressionType, chunks, includeAdditionalPackageToCook);

                var uncompressedData = MemoryManager.GetByteArray(actualMaxChunkSize);
                int positionInChunkData;

                //write tables to first chunk
                using (var ms = new MemoryStream(uncompressedData))
                {
                    switch (package.Game)
                    {
                        case MEGame.ME1:
                            foreach (string name in package.names)
                            {
                                ms.WriteUnrealStringLatin1(name);
                                ms.WriteInt32(0);
                                ms.WriteInt32(458768);
                            }
                            break;
                        case MEGame.ME2:
                            foreach (string name in package.names)
                            {
                                ms.WriteUnrealStringLatin1(name);
                                ms.WriteInt32(-14);
                            }
                            break;
                        case MEGame.ME3:
                        case MEGame.LE3:
                            foreach (string name in package.names)
                            {
                                ms.WriteUnrealStringUnicode(name);
                            }
                            break;
                        case MEGame.LE1:
                        case MEGame.LE2:
                            foreach (string name in package.names)
                            {
                                ms.WriteUnrealStringLatin1(name);
                            }
                            break;
                    }

                    // sanity check
#if DEBUG
                    if (ms.Position != nameTableSize)
                        throw new Exception(@"INVALID NAME TABLE SIZE! Check that the serialized size and calculated size make sense (e.g. 0 length strings)");
#endif
                    foreach (ImportEntry imp in package.imports)
                    {
                        imp.SerializeHeader(ms);
                    }
                    foreach (ExportEntry exp in package.exports)
                    {
                        exp.SerializeHeader(ms);
                    }
                    Array.Clear(uncompressedData, (int)ms.Position, dependencyTableSize);
                    positionInChunkData = (int)ms.Position + dependencyTableSize;
                }



                var compressionOutputSize = compressionType switch
                {
                    CompressionType.Zlib => Zlib.GetCompressionBound(maxBlockSize),
                    CompressionType.LZO => LZO2.GetCompressionBound(maxBlockSize),
                    CompressionType.OodleLeviathan => OodleHelper.GetCompressionBound(maxBlockSize),
                    _ => throw new Exception("Internal error: Unsupported compression type for compressing blocks: " + compressionType)
                };
                int maxBlocksInChunk = (int)Math.Ceiling(actualMaxChunkSize * 1.0 / maxBlockSize);
                var rentedOutputArrays = new List<byte[]>();
                for (int j = rentedOutputArrays.Count; j < maxBlocksInChunk; j++)
                {
                    rentedOutputArrays.Add(MemoryManager.GetByteArray(compressionOutputSize));
                }
                int exportIdx = 0;
                ExportEntry curExport = package.ExportCount > 0 ? package.exports[exportIdx] : null;
                for (int i = 0; i < chunks.Count; i++)
                {
                    chunk = chunks[i];
                    chunk.compressedOffset = (int)compressedStream.Position;
                    chunk.compressedSize = 0;
                    int dataSizeRemainingToCompress = chunk.uncompressedSize;
                    int chunkUncompressedEndOffset = chunk.uncompressedOffset + chunk.uncompressedSize;
                    int numBlocksInChunk = (int)Math.Ceiling(chunk.uncompressedSize * 1.0 / maxBlockSize);
                    chunk.blocks = new CompressionHelper.Block[numBlocksInChunk];
                    // skip chunk header and blocks table - filled later
                    compressedStream.Seek(CompressionHelper.SIZE_OF_CHUNK_HEADER + CompressionHelper.SIZE_OF_CHUNK_BLOCK_HEADER * numBlocksInChunk, SeekOrigin.Current);

                    //write Export Data to chunk
                    //export data never crosses chunk boundaries, so we only need to check if the beginning of the data is in the chunk
                    while (curExport is not null && curExport.DataOffset < chunkUncompressedEndOffset)
                    {
                        curExport.DataReadOnly.CopyTo(uncompressedData.AsSpan(positionInChunkData));
                        positionInChunkData += curExport.DataSize;
                        ++exportIdx;
                        if (exportIdx >= package.ExportCount)
                        {
                            break;
                        }
                        curExport = package.exports[exportIdx];
                    }

                    //Calculate blocks by splitting data into 128KB "block chunks".
                    positionInChunkData = 0;
                    for (int b = 0; b < numBlocksInChunk; b++)
                    {
                        var block = new CompressionHelper.Block();
                        block.uncompressedsize = Math.Min(maxBlockSize, dataSizeRemainingToCompress);
                        dataSizeRemainingToCompress -= block.uncompressedsize;
                        block.uncompressedData = new ArraySegment<byte>(uncompressedData, positionInChunkData, block.uncompressedsize);
                        block.compressedData = rentedOutputArrays[b];
                        chunk.blocks[b] = block;

                        positionInChunkData += block.uncompressedsize;
                    }
                    switch (compressionType)
                    {
                        case CompressionType.LZO:
                            Parallel.ForEach(chunk.blocks, static block => block.compressedsize = LZO2.Compress(block.uncompressedData, block.compressedData));
                            break;
                        case CompressionType.Zlib:
                            Parallel.ForEach(chunk.blocks, static block => block.compressedsize = Zlib.Compress(block.uncompressedData, block.compressedData));
                            break;
                        case CompressionType.OodleLeviathan:
                            Parallel.ForEach(chunk.blocks, static block => block.compressedsize = OodleHelper.Compress(block.uncompressedData, block.compressedData));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("Internal error: Unsupported compression type for compressing blocks: " + compressionType);
                    }

                    //Write compressed data to stream 
                    for (int b = 0; b < numBlocksInChunk; b++)
                    {
                        var block = chunk.blocks[b];
                        if (block.compressedsize == 0)
                        {
                            throw new Exception("Internal error: Block compression failed! Compressor returned no bytes");
                        }
                        compressedStream.Write(block.compressedData, 0, block.compressedsize);
                        chunk.compressedSize += block.compressedsize;
                    }

                    positionInChunkData = 0;
                }
                MemoryManager.ReturnByteArray(uncompressedData);
                foreach (byte[] rentedArray in rentedOutputArrays)
                {
                    MemoryManager.ReturnByteArray(rentedArray);
                }

                if (package.Game.IsLEGame())
                {
                    WriteLegendaryExplorerCoreTag(compressedStream, package);
                }

                //Update each chunk header with new information
                foreach (var c in chunks)
                {
                    compressedStream.JumpTo(c.compressedOffset); // jump to blocks header
                    compressedStream.WriteUInt32(packageTagLittleEndian);
                    compressedStream.WriteInt32(maxBlockSize); // technically this is apparently a UINT
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
                package.WriteHeader(compressedStream, compressionType, chunks, includeAdditionalPackageToCook: includeAdditionalPackageToCook);
                return compressedStream;
            }
            finally
            {
                //If we're doing save as, reset compressed flag to reflect file on disk as we still point to the original one
                if (isSaveAs)
                {
                    if (sourceIsCompressed)
                    {
                        package.Flags |= EPackageFlags.Compressed;
                    }
                    else
                    {
                        package.Flags &= ~EPackageFlags.Compressed;
                    }
                }
            }
        }

        private static void WriteLegendaryExplorerCoreTag(MemoryStream ms, IMEPackage package)
        {
            if (package is not MEPackage mep) return; // Do not write on non ME packages.
            if (!mep.Game.IsLEGame()) return; // Do not write on non-LE even if this is somehow called.

            var pos = ms.Position;

            // BASIC TAG FORMAT:
            // <data of package to the end>
            // INT: LECL TAG VERSION
            // <LECL DATA>
            // INT: LECL DATA size in bytes + 4
            // ASCII 'LECL'

            // DOCUMENT VERSIONS HERE

            // 1: INITIAL VERSION
            // Contains no LECL DATA.

            // 2: Import 'Hinting' 08/21/2022
            // Contains data for hinting to LEC what files contain imports, which will
            // automatically add them to the list of files that can be imported from

            if (package.LECLTagData != null && package.LECLTagData.HasAnyData())
            {
                ms.WriteInt32(LECPackageTag_Version_JSON); // The current version
                var data = JsonConvert.SerializeObject(package.LECLTagData, Formatting.None, 
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }); // This makes it not serialize default values, like false bools
                ms.WriteStringUtf8(data);
            }
            else
            {
                ms.WriteInt32(LECPackageTag_Version_EmptyData); // Blank data version. Version 1 will not attempt to read data.
            }

            ms.WriteInt32((int)(ms.Position - pos)); // Size of the LECL data & version tag in bytes
            ms.WriteStringASCII(LECPackageTag);
        }

        //Must not change export's DataSize!
        private static void UpdateOffsets(ExportEntry e, int oldDataOffset)
        {
            if (!e.IsDefaultObject)
            {
                if (e.Game == MEGame.ME1 && e.IsTexture())
                {
                    // For us to reliably have in-memory textures, the data offset of 'externally' stored textures
                    // needs to be updated to be accurate so that master and slave textures are in sync.
                    // So any texture mips stored as pccLZO needs their DataOffsets updated
                    var t2d = ObjectBinary.From<UTexture2D>(e);
                    var binStart = -1;
                    foreach (var mip in t2d.Mips.Where(x => x.IsCompressed && x.IsLocallyStored))
                    {
                        if (binStart == -1)
                        {
                            binStart = e.DataOffset + e.propsEnd();
                        }

                        // This is 
                        mip.DataOffset = binStart + mip.MipInfoOffsetFromBinStart + 0x10; // actual data offset is past storagetype, uncomp, comp, dataoffset
                    }

                    e.WriteBinary(t2d);
                }
                else
                {
                    switch (e.ClassName)
                    {
                        //case "WwiseBank":
                        case "WwiseStream" when e.GetProperty<NameProperty>("Filename") == null:
                        case "TextureMovie" when e.GetProperty<NameProperty>("TextureFileCacheName") == null:
                            e.WriteBinary(ObjectBinary.From(e));
                            break;
                        case "ShaderCache":
                            e.UpdateShaderCacheOffsets(oldDataOffset);
                            break;
                    }
                }
            }
        }

        public MemoryStream SaveToStream(bool compress = false, bool includeAdditionalPackagesToCook = true, bool includeDependencyTable = true)
        {
            return compress
                ? saveCompressed(this, true, includeAdditionalPackagesToCook, includeDependencyTable)
                : saveUncompressed(this, true, includeAdditionalPackagesToCook, includeDependencyTable);
        }

        private void WriteHeader(Stream ms, CompressionType compressionType = CompressionType.None, List<CompressionHelper.Chunk> chunks = null, bool includeAdditionalPackageToCook = true)
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
                case MEGame.LE1:
                    ms.WriteUInt16(LE1UnrealVersion);
                    ms.WriteUInt16(LE1LicenseeVersion);
                    break;
                case MEGame.LE2:
                    ms.WriteUInt16(LE2UnrealVersion);
                    ms.WriteUInt16(LE2LicenseeVersion);
                    break;
                case MEGame.LE3:
                    ms.WriteUInt16(LE3UnrealVersion);
                    ms.WriteUInt16(LE3LicenseeVersion);
                    break;
            }
            ms.WriteInt32(FullHeaderSize);
            if (Game.IsGame3())
            {
                ms.WriteUnrealStringUnicode("None");
            }
            else
            {
                ms.WriteUnrealStringLatin1("None");
            }

            ms.WriteUInt32((uint)Flags);

            if (Game.IsGame3() && Flags.Has(EPackageFlags.Cooked))
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

            if (Game == MEGame.ME3 || Game.IsLEGame())
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
                case MEGame.LE3:
                    ms.WriteInt32(6383);
                    ms.WriteInt32(196715);
                    break;
                case MEGame.LE1:
                case MEGame.LE2:
                    ms.WriteInt32(6383);
                    ms.WriteInt32(65643);
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

            // Write build and branch numbers
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
                case MEGame.LE1:
                    ms.WriteInt32(1376256);
                    ms.WriteInt32(1376256);
                    break;
                case MEGame.LE2:
                    ms.WriteInt32(131268608);
                    ms.WriteInt32(145752064);
                    break;
                case MEGame.LE3:
                    ms.WriteInt32(372637696);
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
                        var chunksize = chunk.compressedSize + CompressionHelper.SIZE_OF_CHUNK_HEADER + CompressionHelper.SIZE_OF_CHUNK_BLOCK_HEADER * chunk.blocks.Length;
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

            if (Game is MEGame.ME2 or MEGame.ME1)
            {
                ms.WriteInt32(0);
            }

            if (Game is MEGame.ME2 or MEGame.ME3 || Game.IsLEGame())
            {
                // ME3Explorer should always save with this flag set to true.
                // Only things that depend on legacy configuration (like Mixins) should
                // set it to false.
                if (includeAdditionalPackageToCook)
                {
                    ms.WriteInt32(AdditionalPackagesToCook.Count);
                    foreach (var pname in AdditionalPackagesToCook)
                    {
                        if (Game is MEGame.ME2 or MEGame.LE1 or MEGame.LE2)
                        {
                            // Uses ASCII
                            ms.WriteUnrealStringLatin1(pname);
                        }
                        else
                        {
                            ms.WriteUnrealStringUnicode(pname);
                        }
                    }
                }
                else
                {
                    ms.WriteInt32(0);
                }
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