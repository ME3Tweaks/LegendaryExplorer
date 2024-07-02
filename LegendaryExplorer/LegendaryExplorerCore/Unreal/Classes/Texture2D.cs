using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Image = LegendaryExplorerCore.Textures.Image;
using PixelFormat = LegendaryExplorerCore.Textures.PixelFormat;

namespace LegendaryExplorerCore.Unreal.Classes
{
    public class Texture2D
    {
        public List<Texture2DMipInfo> Mips { get; }
        public readonly bool NeverStream;
        public readonly ExportEntry Export;
        public string TextureFormat { get; init; }
        public Guid TextureGuid;

        // Callback for when there's an exception. Used by M3 to localize the error. Defaults to an int version since ME3Explorer is INT only
        public static Func<string, string, string> GetLocalizedCouldNotFetchTextureDataMessage { get; set; } = (export, file) => $"Could not fetch texture data for texture {export} in file {file}";
        public static Func<string, string> GetLocalizedCouldNotFindME1TexturePackageMessage { get; set; } = file => $"Externally referenced texture package not found: {file}";
        public static Func<string, string> GetLocalizedCouldNotFindME2ME3TextureCacheMessage { get; set; } = file => $"Externally referenced texture file not found: {file}";
        public static Func<string, string, string, string, string> GetLocalizedTextureExceptionExternalMessage { get; set; } = (exceptionMessage, file, storageType, offset) => $"{exceptionMessage}\nFile: {file}\nStorageType: {storageType}\nExternal file offset: {offset}";
        public static Func<string, string, string> GetLocalizedTextureExceptionInternalMessage { get; set; } = (exceptionMessage, storageType) => $"{exceptionMessage}\nStorageType: {storageType}";

        /// <summary>
        /// Stores a list of 'master' texture packages that exist outside of the main ME1 game. These are used for looking up data
        /// </summary>
        public static List<string> AdditionalME1MasterTexturePackages { get; } = new List<string>();

        //public static Func<string> GetLocalizedCouldNotFetchTextureDataMessage { get; set; }

        public Texture2D(ExportEntry export)
        {
            Export = export;
            PropertyCollection properties = export.GetProperties();
            TextureFormat = properties.GetProp<EnumProperty>(@"Format").Value.Name;
            var cache = properties.GetProp<NameProperty>(@"TextureFileCacheName");

            NeverStream = properties.GetProp<BoolProperty>(@"NeverStream") ?? false;
            Mips = GetTexture2DMipInfos(export, cache?.Value);
            if (Export.Game != MEGame.ME1)
            {
                int guidOffsetFromEnd = export.Game is MEGame.ME3 || export.Game.IsLEGame() ? 20 : 16;
                if (export.ClassName == "LightMapTexture2D")
                {
                    guidOffsetFromEnd += 4;
                }
                TextureGuid = new Guid(Export.DataReadOnly.Slice(Export.DataSize - guidOffsetFromEnd, 16));
            }
        }

        public static uint GetTextureCRC(ExportEntry export, List<string> additionalTFCs = null)
        {
            PropertyCollection properties = export.GetProperties();
            var format = properties.GetProp<EnumProperty>("Format");
            if (format != null)
            {
                var cache = properties.GetProp<NameProperty>("TextureFileCacheName");
                List<Texture2DMipInfo> mips = GetTexture2DMipInfos(export, cache?.Value);
                var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
                return GetMipCRC(topmip, format.Value, additionalTFCs: additionalTFCs);
            }

            return 0; // BIOA_GLO_00_B_Sovereign_T.upk in ME1 has a Texture2D export in it that is completely blank, no props, no binary. no idea how this compiled
        }

        public void RemoveEmptyMipsFromMipList()
        {
            Mips.RemoveAll(x => x.storageType == StorageTypes.empty);
        }

        public Texture2DMipInfo GetTopMip()
        {
            return Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
        }

        public static List<Texture2DMipInfo> GetTexture2DMipInfos(ExportEntry exportEntry, string cacheName)
        {
            var texBin = exportEntry.GetBinaryData<UTexture2D>();
            var mips = new List<Texture2DMipInfo>();
            for (int i = 0; i < texBin.Mips.Count; i++)
            {
                UTexture2D.Texture2DMipMap binMip = texBin.Mips[i];
                var mip = new Texture2DMipInfo
                {
                    Export = exportEntry,
                    index = texBin.Mips.Count - i,
                    storageType = binMip.StorageType,
                    uncompressedSize = binMip.UncompressedSize,
                    compressedSize = binMip.CompressedSize,
                    externalOffset = binMip.DataOffset,
                    Mip = binMip.Mip,
                    TextureCacheName = cacheName, //If this is ME1, this will simply be ignored in the setter
                    width = binMip.SizeX,
                    height = binMip.SizeY
                };

                if (mip.width == 4 && mips.Exists(m => m.width == mip.width))
                    mip.width = mips.Last().width / 2;
                if (mip.height == 4 && mips.Exists(m => m.height == mip.height))
                    mip.height = mips.Last().height / 2;
                if (mip.width == 0)
                    mip.width = 1;
                if (mip.height == 0)
                    mip.height = 1;
                mips.Add(mip);
            }

            return mips;
        }

        public byte[] GetImageBytesForMip(Texture2DMipInfo info, MEGame game, bool useLowerMipsIfTFCMissing, out Texture2DMipInfo usedMip, string gamePathToUse = null, List<string> additionalTFCs = null)
        {
            byte[] imageBytes = null;
            usedMip = info;
            try
            {
                imageBytes = GetTextureData(info, game, gamePathToUse, true, additionalTFCs);
            }
            catch (FileNotFoundException e)
            {
                if (useLowerMipsIfTFCMissing)
                {
                    //External archive not found - using built in mips (will be hideous, but better than nothing)
                    info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                    if (info != null)
                    {
                        usedMip = info;
                        imageBytes = GetTextureData(info, game, gamePathToUse);
                    }
                }
                else
                {
                    throw; //rethrow
                }
            }
            if (imageBytes == null)
            {
                throw new Exception(GetLocalizedCouldNotFetchTextureDataMessage(info?.Export.InstancedFullPath, info?.Export.FileRef.FilePath));
            }
            return imageBytes;
        }

        public void SerializeNewData(Stream ms)
        {
            if (!Export.FileRef.Game.IsGame3()) // Is this rig
            {
                for (int i = 0; i < 12; i++)
                    ms.WriteByte(0); //12 0s
                for (int i = 0; i < 4; i++)
                    ms.WriteByte(0); //position in the package. will be updated later
            }

            ms.WriteInt32(Mips.Count);
            foreach (var mip in Mips)
            {
                ms.WriteUInt32((uint)mip.storageType);
                ms.WriteInt32(mip.uncompressedSize);
                ms.WriteInt32(mip.compressedSize);
                ms.WriteInt32(mip.externalOffset);
                if (mip.storageType is StorageTypes.pccUnc or StorageTypes.pccLZO or StorageTypes.pccZlib or StorageTypes.pccOodle)
                {
                    ms.Write(mip.Mip, 0, mip.Mip.Length);
                }
                ms.WriteInt32(mip.width);
                ms.WriteInt32(mip.height);
            }

            if (Export.Game != MEGame.UDK)
            {
                ms.WriteInt32(0);
            }
            if (Export.Game != MEGame.ME1)
            {
                ms.WriteGuid(TextureGuid);
            }
            if (Export.Game == MEGame.UDK)
            {
                ms.WriteZeros(4 * 8);
            }
            if (Export.Game == MEGame.ME3 || Export.Game.IsLEGame())
            {
                ms.WriteInt32(0);
                if (Export.ClassName == "LightMapTexture2D")
                {
                    ms.WriteInt32(0);
                }
            }
        }

        public void ReplaceMips(List<Texture2DMipInfo> mipmaps)
        {
            Mips.Clear();
            Mips.AddRange(mipmaps);
        }

        /// <summary>
        /// Gets texture data for the given mip.
        /// </summary>
        /// <param name="mipToLoad"></param>
        /// <param name="game"></param>
        /// <param name="gamePathToUse"></param>
        /// <param name="decompress"></param>
        /// <param name="additionalTFCs"></param>
        /// <returns></returns>
        public static byte[] GetTextureData(Texture2DMipInfo mipToLoad, MEGame game, string gamePathToUse = null, bool decompress = true, List<string> additionalTFCs = null)
        {
            return GetTextureData(game, mipToLoad.Mip, mipToLoad.storageType, decompress, mipToLoad.uncompressedSize, mipToLoad.compressedSize, mipToLoad.externalOffset, mipToLoad.TextureCacheName, gamePathToUse, additionalTFCs, mipToLoad.Export?.FileRef.FilePath);
        }

        /// <summary>
        /// Gets texture data from the given data.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="mipData"></param>
        /// <param name="storageType"></param>
        /// <param name="decompress"></param>
        /// <param name="uncompressedSize"></param>
        /// <param name="compressedSize"></param>
        /// <param name="externalOffset"></param>
        /// <param name="textureCacheName"></param>
        /// <param name="gamePathToUse"></param>
        /// <param name="additionalTFCs"></param>
        /// <param name="packagePathForLocalLookup"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public static byte[] GetTextureData(MEGame game, byte[] mipData, StorageTypes storageType, bool decompress, int uncompressedSize, int compressedSize, int externalOffset, string textureCacheName, string gamePathToUse = null, List<string> additionalTFCs = null, string packagePathForLocalLookup = null)
        {
            bool dataLoaded = false;
            var imagebytes = new byte[decompress ? uncompressedSize : compressedSize];
            //Debug.WriteLine("getting texture data for " + Export.FullPath);
            if (storageType == StorageTypes.pccUnc)
            {
                Buffer.BlockCopy(mipData, 0, imagebytes, 0, uncompressedSize);
            }
            else if (storageType is StorageTypes.pccLZO or StorageTypes.pccZlib or StorageTypes.pccOodle)
            {
                if (decompress)
                {
                    try
                    {
                        TextureCompression.DecompressTexture(imagebytes,
                                                             new MemoryStream(mipData),
                                                             storageType, uncompressedSize, compressedSize);
                    }
                    catch (Exception e)
                    {
                        throw new Exception(GetLocalizedTextureExceptionInternalMessage(e.Message, storageType.ToString()));
                    }
                }
                else
                {
                    Buffer.BlockCopy(mipData, 0, imagebytes, 0, compressedSize);
                }
            }
            else if (storageType != StorageTypes.empty && ((int)storageType & (int)StorageFlags.externalFile) != 0)
            {
                // external 
                string filename = null;
                List<string> loadedFiles = MELoadedFiles.GetAllGameFiles(gamePathToUse, game, false, true);
                if (game == MEGame.ME1)
                {
                    var fullPath = loadedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(textureCacheName, StringComparison.InvariantCultureIgnoreCase));

                    if (fullPath != null)
                    {
                        filename = fullPath;
                    }
                    else
                    {
                        fullPath = AdditionalME1MasterTexturePackages.FirstOrDefault(x => Path.GetFileName(x).Equals(textureCacheName, StringComparison.InvariantCultureIgnoreCase));
                        filename = fullPath ?? throw new FileNotFoundException(GetLocalizedCouldNotFindME1TexturePackageMessage(textureCacheName));
                    }
                }
                else
                {
                    string archive = textureCacheName + @".tfc";

                    var localDirectoryTFCPath = Path.Combine(Path.GetDirectoryName(packagePathForLocalLookup), archive);
                    if (File.Exists(localDirectoryTFCPath))
                    {
                        filename = localDirectoryTFCPath;
                    }
                    else if (additionalTFCs != null && additionalTFCs.Any(x => Path.GetFileName(x).Equals(archive, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        filename = additionalTFCs.First(x => Path.GetFileName(x).Equals(archive, StringComparison.InvariantCultureIgnoreCase));
                    }
                    else
                    {
                        //var tfcs = loadedFiles.Where(x => x.EndsWith(@".tfc")).ToList();

                        var fullPath = loadedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(archive, StringComparison.InvariantCultureIgnoreCase));
                        if (fullPath != null)
                        {
                            filename = fullPath;
                        }
                        else if (game == MEGame.ME3 && textureCacheName.StartsWith(@"Textures_DLC"))
                        {
                            // Check SFAR
                            var dlcName = textureCacheName.Substring(9);
                            if (!MEDirectories.OfficialDLC(MEGame.ME3).Contains(dlcName) || ME3Directory.DLCPath is null)
                            {
                                // Not an official DLC
                                throw new FileNotFoundException(GetLocalizedCouldNotFindME2ME3TextureCacheMessage(archive));
                            }
                            var sfarPath = Path.Combine(ME3Directory.DLCPath, dlcName, game.CookedDirName(), "Default.sfar");
                            if (!File.Exists(sfarPath))
                            {
                                // SFAR not in folder
                                throw new FileNotFoundException(GetLocalizedCouldNotFindME2ME3TextureCacheMessage(archive));
                            }
                            var dpackage = new DLCPackage(sfarPath);
                            var entryId = dpackage.FindFileEntry(archive);
                            if (entryId < 0)
                            {
                                // File not in archive
                                throw new FileNotFoundException(GetLocalizedCouldNotFindME2ME3TextureCacheMessage(archive));
                            }
                            // TFC is in this SFAR
                            imagebytes = dpackage.ReadFromEntry(entryId, externalOffset, uncompressedSize);
                            dataLoaded = true;
                        }
                        else
                        {
                            throw new FileNotFoundException(GetLocalizedCouldNotFindME2ME3TextureCacheMessage(archive));
                        }
                    }
                }

                //exceptions above will prevent filename from being null here

                if (!dataLoaded) // The data hasn't been extracted yet (sfar fetch)
                {
                    try
                    {
                        using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                        fs.Seek(externalOffset, SeekOrigin.Begin);
                        if (storageType is StorageTypes.extLZO or StorageTypes.extZlib or StorageTypes.extOodle)
                        {
                            if (decompress)
                            {
                                TextureCompression.DecompressTexture(imagebytes, fs, storageType, uncompressedSize, compressedSize);
                            }
                            else
                            {
                                fs.Read(imagebytes, 0, compressedSize);
                            }
                        }
                        else
                        {
                            fs.Read(imagebytes, 0, uncompressedSize);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception(GetLocalizedTextureExceptionExternalMessage(e.Message, filename, storageType.ToString(), externalOffset.ToString()));
                    }
                }
            }
            return imagebytes;
        }

        public static uint GetMipCRC(Texture2DMipInfo mip, string textureFormat, string gamePathToUse = null, List<string> additionalTFCs = null)
        {
            byte[] data = GetTextureData(mip, mip.Export.Game, gamePathToUse, additionalTFCs: additionalTFCs);
            if (data == null) return 0;
            if (textureFormat == "PF_NormalMap_HQ")
            {
                // only ME1 and ME2
                return TextureCRC.Compute(data, 0, data.Length / 2);
            }
            return TextureCRC.Compute(data);
        }

        /// <summary>
        /// Generates the data for a single blank (black) mip of the given size.
        /// </summary>
        /// <param name="sizeX"></param>
        /// <param name="sizeY"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public static byte[] CreateBlankTextureMip(int sizeX, int sizeY, PixelFormat format)
        {
            // Generates blank texture data in the given format
            Bitmap bmp = new Bitmap(sizeX, sizeY);
            var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            var blank = new Image(ms.ToArray(), Image.ImageFormat.BMP);
            blank.correctMips(format); // Generate the mip data
            return blank.mipMaps[0].data;
        }

        /// <summary>
        /// Generates the data for a mipped blank (black) texture of the given size.
        /// </summary>
        /// <param name="sizeX"></param>
        /// <param name="sizeY"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public static List<MipMap> CreateBlankTextureMips(int sizeX, int sizeY, PixelFormat format)
        {
            // Generates blank texture data in the given format
            Bitmap bmp = new Bitmap(sizeX, sizeY);
            var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            var blank = new Image(ms.ToArray(), Image.ImageFormat.BMP);
            blank.correctMips(format); // Generate the mip data
            return blank.mipMaps;
        }

        /// <summary>
        /// Generates the data for a single mip from the given data and type.
        /// </summary>
        /// <param name="inputData"></param>
        /// <param name="dataFormat"></param>
        /// <param name="sizeX"></param>
        /// <param name="sizeY"></param>
        /// <param name="destFormat"></param>
        /// <returns></returns>
        public static byte[] CreateSingleMip(byte[] inputData, Image.ImageFormat dataFormat, PixelFormat destFormat)
        {
            // Generates blank texture data in the given format
            var blank = new Image(inputData, dataFormat);
            blank.correctMips(destFormat); // Generate the mip data
            return blank.mipMaps[0].data;
        }

        /// <summary>
        /// Replaces the texture data for this Texture2D based on the incoming data from image.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="props"></param>
        /// <param name="fileSourcePath"></param>
        /// <param name="forcedTFCName"></param>
        /// <param name="forcedTFCPath"></param>
        /// <param name="isPackageStored"></param>
        /// <returns></returns>
        public List<string> Replace(Image image, PropertyCollection props, string fileSourcePath = null, string forcedTFCName = null, string forcedTFCPath = null, bool isPackageStored = false, PixelFormat forcedFormat = PixelFormat.Unknown)
        {
            var messages = new List<string>();
            var textureCache = forcedTFCName ?? GetTopMip().TextureCacheName;
            if (isPackageStored) textureCache = null;
            string fmt = TextureFormat;
            PixelFormat pixelFormat = Image.getPixelFormatType(fmt);
            RemoveEmptyMipsFromMipList();

            PixelFormat newPixelFormat = forcedFormat != PixelFormat.Unknown ? forcedFormat : pixelFormat;

            // Generate mips if necessary
            if (!image.checkDDSHaveAllMipmaps() || Mips.Count > 1 && image.mipMaps.Count <= 1 || image.pixelFormat != newPixelFormat)
            {
                bool dxt1HasAlpha = false;
                byte dxt1Threshold = 128;
                if (pixelFormat == PixelFormat.DXT1 && (image.HasFullAlpha() || props.GetProp<EnumProperty>("CompressionSettings") is { Value.Name: "TC_OneBitAlpha" }))
                {
                    dxt1HasAlpha = true;
                    if (image.pixelFormat is PixelFormat.ARGB or PixelFormat.DXT3 or PixelFormat.DXT5)
                    {
                        messages.Add("Texture was converted from full alpha to binary alpha. DXT1 does not support more than 1-bit alpha");
                    }
                }

                //Generate lower mips
                image.correctMips(newPixelFormat, dxt1HasAlpha, dxt1Threshold);
            }

            if (Mips.Count == 1)
            {
                var topMip = image.mipMaps[0];
                image.mipMaps.Clear();
                image.mipMaps.Add(topMip);
            }
            else
            {
                // remove lower mipmaps from source image which not exist in game data
                //Not sure what this does since we just generated most of these mips
                for (int t = 0; t < image.mipMaps.Count; t++)
                {
                    if (image.mipMaps[t].origWidth <= Mips[0].width &&
                        image.mipMaps[t].origHeight <= Mips[0].height &&
                        Mips.Count > 1)
                    {
                        if (!Mips.Exists(m => m.width == image.mipMaps[t].origWidth && m.height == image.mipMaps[t].origHeight))
                        {
                            image.mipMaps.RemoveAt(t--);
                        }
                    }
                }

                // put empty mips if missing
                for (int t = 0; t < Mips.Count; t++)
                {
                    if (Mips[t].width <= image.mipMaps[0].origWidth &&
                        Mips[t].height <= image.mipMaps[0].origHeight)
                    {
                        if (!image.mipMaps.Exists(m => m.origWidth == Mips[t].width && m.origHeight == Mips[t].height))
                        {
                            var mipmap = new MipMap(Mips[t].width, Mips[t].height, pixelFormat);
                            image.mipMaps.Add(mipmap);
                        }
                    }
                }
            }

            var compressedMips = new List<byte[]>();

            for (int m = 0; m < image.mipMaps.Count; m++)
            {
                // Mips go big to small

                if (m > image.mipMaps.Count - 6) // 0 indexed
                {
                    // Lower 6 mips are never stored compressed so don't bother wasting time compressing the data
                    compressedMips.Add(null);
                    continue;
                }

                if (Export.Game == MEGame.ME3)
                {
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, isPackageStored ? StorageTypes.pccZlib : StorageTypes.extZlib)); //ZLib
                }
                else if (Export.Game.IsOTGame() || Export.Game is MEGame.UDK)
                {
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, isPackageStored ? StorageTypes.pccLZO : StorageTypes.extLZO)); //LZO 
                }
                else if (Export.Game.IsLEGame())
                {
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, isPackageStored ? StorageTypes.pccOodle : StorageTypes.extOodle)); //Oodle
                }
            }

            var mipmaps = new List<Texture2DMipInfo>();
            for (int m = 0; m < image.mipMaps.Count; m++)
            {
                var mipmap = new Texture2DMipInfo
                {
                    Export = Export,
                    width = image.mipMaps[m].origWidth,
                    height = image.mipMaps[m].origHeight,
                    TextureCacheName = textureCache
                };
                bool mipShouldBePackageStored = isPackageStored || m >= image.mipMaps.Count - 6 || textureCache == null;

                if (Mips.Exists(x => x.width == mipmap.width && x.height == mipmap.height))
                {
                    var oldMip = Mips.First(x => x.width == mipmap.width && x.height == mipmap.height);
                    mipmap.storageType = CalculateStorageType(oldMip.storageType, Export.Game, mipShouldBePackageStored);
                }
                else
                {
                    // New mipmaps
                    mipmap.storageType = CalculateStorageType(Mips[0].storageType, Export.Game, mipShouldBePackageStored);
                }

                //Investigate. this has something to do with archive storage types
                //if (mod.arcTexture != null)
                //{
                //    if (mod.arcTexture[m].storageType != mipmap.storageType)
                //    {
                //        mod.arcTexture = null;
                //    }
                //}

                mipmap.width = image.mipMaps[m].width;
                mipmap.height = image.mipMaps[m].height;
                mipmaps.Add(mipmap);
                if (Mips.Count == 1)
                    break;
            }

            int allExtMipsSize = 0;

            for (int m = 0; m < image.mipMaps.Count; m++)
            {
                Texture2DMipInfo x = mipmaps[m];
                var compSize = image.mipMaps[m].data.Length;

                if (x.storageType is StorageTypes.extZlib or StorageTypes.extLZO or StorageTypes.extUnc or StorageTypes.extOodle)
                {
                    allExtMipsSize += compSize; //compSize on Unc textures is same as LZO/ZLib
                }
            }

            //todo: check to make sure TFC will not be larger than 2GiB

            Guid tfcGuid = Guid.NewGuid(); //make new guid as storage
            bool locallyStored = mipmaps[0].storageType is StorageTypes.pccUnc or StorageTypes.pccZlib or StorageTypes.pccLZO or StorageTypes.pccOodle;
            for (int m = 0; m < image.mipMaps.Count; m++)
            {
                Texture2DMipInfo mipmap = mipmaps[m];
                mipmap.uncompressedSize = image.mipMaps[m].data.Length;
                if (Export.Game == MEGame.ME1)
                {
                    if (mipmap.storageType is StorageTypes.pccLZO or StorageTypes.pccZlib or StorageTypes.pccOodle)
                    {
                        mipmap.Mip = compressedMips[m];
                        mipmap.compressedSize = mipmap.Mip.Length;
                    }
                    else if (mipmap.storageType == StorageTypes.pccUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.Mip = image.mipMaps[m].data;
                    }
                    else
                    {
                        throw new Exception("Unsupported mip storage type for this operation! Are you trying to replace ext textures not using Texture Studio?");
                    }
                }
                else
                {
                    // ME2/ME3/LE
                    if (mipmap.storageType is StorageTypes.extZlib or StorageTypes.extLZO or StorageTypes.extOodle)
                    {
                        if (compressedMips.Count != image.mipMaps.Count)
                            throw new Exception("Amount of compressed mips does not match number of mips of incoming image!");
                        mipmap.Mip = compressedMips[m];
                        mipmap.compressedSize = mipmap.Mip.Length;
                    }

                    if (mipmap.storageType is StorageTypes.pccUnc or StorageTypes.extUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.Mip = image.mipMaps[m].data;
                    }

                    if (mipmap.storageType is StorageTypes.pccLZO or StorageTypes.pccZlib or StorageTypes.pccOodle)
                    {
                        mipmap.Mip = compressedMips[m];
                        mipmap.compressedSize = mipmap.Mip.Length;
                    }

                    if (mipmap.storageType is StorageTypes.extZlib or StorageTypes.extLZO or StorageTypes.extUnc or StorageTypes.extOodle)
                    {
                        if (!string.IsNullOrEmpty(mipmap.TextureCacheName) && mipmap.Export.Game != MEGame.ME1)
                        {
                            //Check local dir
                            string tfcarchive = mipmap.TextureCacheName + ".tfc";
                            var localDirectoryTFCPath = forcedTFCPath ?? Path.Combine(Path.GetDirectoryName(mipmap.Export.FileRef.FilePath), tfcarchive);
                            if (File.Exists(localDirectoryTFCPath))
                            {
                                try
                                {
                                    using var fs = new FileStream(localDirectoryTFCPath, FileMode.Open, FileAccess.ReadWrite);
                                    tfcGuid = fs.ReadGuid();
                                    fs.Seek(0, SeekOrigin.End);
                                    mipmap.externalOffset = (int)fs.Position;
                                    fs.Write(mipmap.Mip, 0, mipmap.compressedSize);
                                }
                                catch (Exception e)
                                {
                                    throw new Exception("Problem appending to TFC file " + tfcarchive + ": " + e.Message);
                                }
                                continue;
                            }

                            //Check game
                            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(mipmap.Export.Game, includeTFCs: true);
                            if (gameFiles.TryGetValue(tfcarchive, out string archiveFile))
                            {
                                try
                                {
                                    using var fs = new FileStream(archiveFile, FileMode.Open, FileAccess.ReadWrite);
                                    tfcGuid = fs.ReadGuid();
                                    fs.Seek(0, SeekOrigin.End);
                                    mipmap.externalOffset = (int)fs.Position;
                                    fs.Write(mipmap.Mip, 0, mipmap.compressedSize);
                                }
                                catch (Exception e)
                                {
                                    throw new Exception("Problem appending to TFC file " + archiveFile + ": " + e.Message);
                                }
                                continue;
                            }

                            //Cache not found. Make new TFC
                            try
                            {
                                using var fs = new FileStream(forcedTFCPath ?? localDirectoryTFCPath, FileMode.OpenOrCreate, FileAccess.Write);
                                fs.WriteGuid(tfcGuid);
                                mipmap.externalOffset = (int)fs.Position;
                                fs.Write(mipmap.Mip, 0, mipmap.compressedSize);
                            }
                            catch (Exception e)
                            {
                                throw new Exception("Problem creating new TFC file " + tfcarchive + ": " + e.Message);
                            }
                            continue;
                        }
                    }
                }
                mipmaps[m] = mipmap;
                if (Mips.Count == 1)
                    break;
            }

            ReplaceMips(mipmaps);

            //Set properties

            // The bottom 6 mips are apparently always pcc stored. If there is less than 6 mips, set neverstream to true, which tells game
            // and toolset to never look into archives for mips.
            if (locallyStored)
            {
                // Rules for default neverstream
                // 1. Must be Package Stored
                // 2. Must have at least 6 not empty mips

                // Never stream forces all textures in package to be loaded and not streamed
                // which is required since streaming only works for external textures
                if (mipmaps.Count >= 6)
                {
                    props.AddOrReplaceProp(new BoolProperty(true, "NeverStream"));
                }
            }

            if (mipmaps.Count < 6 || !locallyStored)
            {
                props.RemoveNamedProperty("NeverStream");
            }

            if (!locallyStored)
            {
                props.AddOrReplaceProp(tfcGuid.ToGuidStructProp("TFCFileGuid"));
                if (mipmaps[0].storageType is StorageTypes.extLZO or StorageTypes.extUnc or StorageTypes.extZlib or StorageTypes.extOodle)
                {
                    //Requires texture cache name
                    props.AddOrReplaceProp(new NameProperty(textureCache, "TextureFileCacheName"));
                }
                else
                {
                    //Should not have texture cache name
                    var cacheProp = props.GetProp<NameProperty>("TextureFileCacheName");
                    if (cacheProp != null)
                    {
                        props.Remove(cacheProp);
                    }
                }
            }
            else
            {
                props.RemoveNamedProperty("TFCFileGuid");
            }

            props.AddOrReplaceProp(new IntProperty(Mips.First().width, "SizeX"));
            props.AddOrReplaceProp(new IntProperty(Mips.First().height, "SizeY"));
            if (Export.Game < MEGame.ME3 && fileSourcePath != null)
            {
                props.AddOrReplaceProp(new StrProperty(fileSourcePath, "SourceFilePath"));
                props.AddOrReplaceProp(new StrProperty(File.GetLastWriteTimeUtc(fileSourcePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), "SourceFileTimestamp"));
            }

            var mipTailIdx = props.GetProp<IntProperty>("MipTailBaseIdx");
            if (mipTailIdx != null)
            {
                mipTailIdx.Value = Mips.Count - 1;
            }

            if (Export.Game.IsLEGame())
            {
                // Adjust the internal lod bias.
                var maxLodInfo = TextureLODInfo.LEMaxLodSizes(Export.Game);
                var texGroup = props.GetProp<EnumProperty>(@"LODGroup");
                if (texGroup != null && maxLodInfo.TryGetValue(texGroup.Value.Instanced, out int maxDimension))
                {
                    // cubemaps will have null texture group. we don't want to update these
                    int lodBias = 0;
                    while (Mips[0].width > maxDimension || Mips[0].height > maxDimension)
                    {
                        lodBias--;
                        maxDimension *= 2;
                    }

                    if (lodBias != 0)
                    {
                        props.AddOrReplaceProp(new IntProperty(lodBias, @"InternalFormatLodBias"));
                    }
                }
            }

            // Texture conversion
            if (pixelFormat != newPixelFormat)
            {
                props.AddOrReplaceProp(new EnumProperty(Image.getEngineFormatType(newPixelFormat), "EPixelFormat", Export.Game, "Format"));
            }

            var mem = new EndianReader(new MemoryStream(0x400 + Mips.Sum(mip => mip.IsPackageStored ? 24 + mip.Mip.Length : 24))) { Endian = Export.FileRef.Endian };
            mem.Writer.WriteFromBuffer(Export.GetPrePropBinary());
            props.WriteTo(mem.Writer, Export.FileRef);
            SerializeNewData(mem.BaseStream);
            Export.Data = mem.ToArray();

            //using (MemoryStream newData = new MemoryStream())
            //{
            //    newData.WriteFromBuffer(texture.properties.toArray());
            //    newData.WriteFromBuffer(texture.toArray(0, false)); // filled later
            //    package.setExportData(matched.exportID, newData.ToArray());
            //}

            //using (MemoryStream newData = new MemoryStream())
            //{
            //    newData.WriteFromBuffer(texture.properties.toArray());
            //    newData.WriteFromBuffer(texture.toArray(package.exportsTable[matched.exportID].dataOffset + (uint)newData.Position));
            //    package.setExportData(matched.exportID, newData.ToArray());
            //}

            //Since this is single replacement, we don't want to relink to master
            //We want to ensure names are different though, will have to implement into UI
            //if (Export.Game == MEGame.ME1)
            //{
            //    if (matched.linkToMaster == -1)
            //        mod.masterTextures.Add(texture.mipMapsList, entryMap.listIndex);
            //}
            //else
            //{
            //    if (triggerCacheArc)
            //    {
            //        mod.arcTexture = texture.mipMapsList;
            //        mod.arcTfcGuid = texture.properties.getProperty("TFCFileGuid").valueStruct;
            //        mod.arcTfcName = texture.properties.getProperty("TextureFileCacheName").valueName;
            //    }
            //}

            return messages;
        }

        /// <summary>
        /// Returns the appropriate storage type for a mip based on the previous storage type of that mip and other parameters
        /// </summary>
        /// <param name="prevType">Existing storage type for this mip</param>
        /// <param name="game">The game this texture belongs to</param>
        /// <param name="isPackageStored">Is mip package stored? Should be true if in bottom six mips</param>
        /// <returns></returns>
        public static StorageTypes CalculateStorageType(StorageTypes prevType, MEGame game, bool isPackageStored)
        {
            StorageTypes type = prevType;
            //ME2,ME3: Force compression type (not implemented yet)
            if (game is MEGame.ME3)
            {
                if (type is StorageTypes.extLZO) //ME3 LZO -> ZLIB
                    type = StorageTypes.extZlib;
                if (type is StorageTypes.pccLZO) //ME3 PCC LZO -> PCCZLIB
                    type = StorageTypes.pccZlib;
                if (type is StorageTypes.extUnc) //ME3 Uncomp -> ZLib
                    type = StorageTypes.extZlib;
                //Leave here for future. WE might need this after dealing with double compression
                //if (type == StorageTypes.pccUnc && mipmap.width > 32) //ME3 Uncomp -> ZLib
                //    type = StorageTypes.pccZlib;
                if (type is StorageTypes.pccUnc && !isPackageStored) //Moving texture to store externally.
                    type = StorageTypes.extZlib;
            }
            else if (game is MEGame.ME2)
            {
                if (type is StorageTypes.extZlib) //ME2 ZLib -> LZO
                    type = StorageTypes.extLZO;
                if (type is StorageTypes.pccZlib) //ME2 PCC ZLib -> LZO
                    type = StorageTypes.pccLZO;
                if (type is StorageTypes.extUnc) //ME2 Uncomp -> LZO
                    type = StorageTypes.extLZO;
                //Leave here for future. We might neable this after dealing with double compression
                //if (type == StorageTypes.pccUnc && mipmap.width > 32) //ME2 Uncomp -> LZO
                //    type = StorageTypes.pccLZO;
                if (type is StorageTypes.pccUnc && !isPackageStored) //Moving texture to store externally. make sure bottom 6 are pcc stored
                    type = StorageTypes.extLZO;

                // TEXTURE WORK BRANCH TOOLING ONLY!!
                //if (type == StorageTypes.extLZO)
                //    type = StorageTypes.pccLZO;
            }
            else if (game.IsLEGame())
            {
                if (type is StorageTypes.extUnc)
                    type = StorageTypes.extOodle; // Compress external unc to Oodle
                if (type is StorageTypes.pccUnc && !isPackageStored) //Moving texture to store externally. make sure bottom 6 are pcc stored
                    type = StorageTypes.extOodle;
                if (type is StorageTypes.pccOodle && isPackageStored)
                    type = StorageTypes.pccUnc; // We always store LE packages as compressed. So do not compress the textures stored locally
            }

            // Force storage type to either ext or pcc. Does not handle LZMA or empty
            if (isPackageStored)
            {
                type = type switch
                {
                    StorageTypes.extOodle => StorageTypes.pccUnc, // We do not compress package stored in LE as all packages are compressed
                    StorageTypes.extLZO => StorageTypes.pccLZO,
                    StorageTypes.extUnc => StorageTypes.pccUnc,
                    StorageTypes.extZlib => StorageTypes.pccZlib,
                    _ => type
                };
            }
            else
            {
                type = type switch
                {
                    StorageTypes.pccOodle => StorageTypes.extOodle,
                    StorageTypes.pccLZO => StorageTypes.extLZO,
                    StorageTypes.pccUnc => StorageTypes.extUnc,
                    StorageTypes.pccZlib => StorageTypes.extZlib,
                    _ => type
                };
            }

            return type;
        }

        public bool ExportToFile(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path must be specified.", nameof(outputPath));

            var info = new Texture2DMipInfo();
            info = Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            if (info != null)
            {
                byte[] imageBytes = null;
                try
                {
                    imageBytes = Texture2D.GetTextureData(info, Export.Game);
                }
                catch (FileNotFoundException)
                {
                    Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                    //External archive not found - using built in mips (will be hideous, but better than nothing)
                    info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                    if (info != null)
                    {
                        imageBytes = Texture2D.GetTextureData(info, Export.Game);
                    }
                }

                if (imageBytes != null)
                {
                    PixelFormat format = Image.getPixelFormatType(TextureFormat);
                    TexConverter.SaveTexture(imageBytes, (uint)info.width, (uint)info.height, format, outputPath);
                }
            }
            return true;
        }

        /// <summary>
        /// Exports the top mip to raw ARGB bytes
        /// </summary>
        /// <param name="outStream">Stream to write to instead of to disk.</param>
        /// <returns></returns>
        public bool ExportToARGB(Stream outStream)
        {
            var info = Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            if (info != null)
            {
                byte[] imageBytes = null;
                try
                {
                    imageBytes = GetTextureData(info, Export.Game);
                }
                catch (FileNotFoundException)
                {
                    Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                    //External archive not found - using built in mips (will be hideous, but better than nothing)
                    info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                    if (info != null)
                    {
                        imageBytes = GetTextureData(info, Export.Game);
                    }
                }

                if (imageBytes != null)
                {
                    PixelFormat format = Image.getPixelFormatType(TextureFormat);
                    outStream.Write(Image.convertRawToARGB(imageBytes, ref info.width, ref info.height, format));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Exports the texture to PNG format. Writes to the specified stream, or the specified path if not defined.
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="outStream">Stream to write to instead of to disk.</param>
        /// <returns></returns>
        public bool ExportToPNG(string outputPath = null, Stream outStream = null)
        {
            if (outputPath == null && outStream == null)
                throw new Exception("ExportToPNG() requires at least one not-null parameter.");
            var info = Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            if (info != null)
            {
                byte[] imageBytes = null;
                try
                {
                    imageBytes = GetTextureData(info, Export.Game);
                }
                catch (FileNotFoundException)
                {
                    Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                    //External archive not found - using built in mips (will be hideous, but better than nothing)
                    info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                    if (info != null)
                    {
                        imageBytes = GetTextureData(info, Export.Game);
                    }
                }

                if (imageBytes != null)
                {
                    PixelFormat format = Image.getPixelFormatType(TextureFormat);

                    var pngdata = Image.convertToPng(imageBytes, info.width, info.height, format);
                    if (outStream == null)
                    {
                        outStream = new FileStream(outputPath, FileMode.Create);
                        pngdata.CopyTo(outStream);
                        outStream.Close();
                    }
                    else
                    {
                        pngdata.CopyTo(outStream);
                    }
                    pngdata.Close();
                }
            }

            return true;
        }

        public byte[] GetPNG(Texture2DMipInfo info)
        {
            PixelFormat format = Image.getPixelFormatType(TextureFormat);
            return Image.convertToPng(GetTextureData(info, Export.Game), info.width, info.height, format)
                .ToArray();
        }

        public Image ToImage(PixelFormat dstFormat)
        {
            PixelFormat format = Image.getPixelFormatType(TextureFormat);
            Texture2DMipInfo topMip = GetTopMip();
            var image = new Image(new List<MipMap> { new(GetTextureData(topMip, Export.Game), topMip.width, topMip.height, format) }, format);
            image.correctMips(dstFormat);
            return image;
        }

        /// <summary>
        /// Creates a new Texture2D export with the given parameters.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="textureName"></param>
        /// <param name="sizeX"></param>
        /// <param name="sizeY"></param>
        /// <param name="pixelFormat"></param>
        /// <param name="mipped"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static ExportEntry CreateTexture(IMEPackage package, NameReference textureName, int sizeX, int sizeY, PixelFormat pixelFormat, bool mipped, ExportEntry parent = null)
        {

            // There's probably more properties to set, but right now this seems OK, I suppose...
            var exp = ExportCreator.CreateExport(package, textureName, @"Texture2D", parent: parent, indexed: false);
            var props = exp.GetProperties();
            props.AddOrReplaceProp(new EnumProperty(Image.getEngineFormatType(pixelFormat), @"EPixelFormat", package.Game, @"Format"));
            props.AddOrReplaceProp(new IntProperty(sizeX, @"SizeX"));
            props.AddOrReplaceProp(new IntProperty(sizeY, @"SizeY"));
            props.AddOrReplaceProp(new BoolProperty(true, @"NeverStream"));

            if (!mipped)
            {
                props.AddOrReplaceProp(new BoolProperty(true, @"CompressionNoMipmaps"));
            }

            // In LE this does not matter but we will set one anyways.
            props.AddOrReplaceProp(new EnumProperty(@"TEXTUREGROUP_UI", @"TextureGroup", package.Game, @"LODGroup"));
            exp.WriteProperties(props);


            UTexture2D texTemp = UTexture2D.Create();
            if (!mipped)
            {
                // Generate a single mip
                var mipData = Texture2D.CreateBlankTextureMip(sizeX, sizeY, pixelFormat);
                texTemp.Mips.Add(new UTexture2D.Texture2DMipMap(mipData, sizeX, sizeY));
            }
            else
            {
                var mipDatas = Texture2D.CreateBlankTextureMips(sizeX, sizeY, pixelFormat);
                foreach (var mip in mipDatas)
                {
                    texTemp.Mips.Add(new UTexture2D.Texture2DMipMap(mip.data, mip.width, mip.height));
                }
            }

            exp.WriteBinary(texTemp);

            return exp;
        }

        public static ExportEntry CreateSWFForTexture(ExportEntry texture)
        {
            // Will require naming validation prior to calling. Must have _ on the end. Or does it?
            var textureName = texture.ObjectName.Name;
            var swfName = texture.ObjectName.Name[..textureName.LastIndexOf('_')]; // I am pretty sure they don't use unreal indexing for these
            var newExp = ExportCreator.CreateExport(texture.FileRef, swfName, "GFxMovieInfo", texture.Parent, indexed: false);

            var references = new ArrayProperty<ObjectProperty>("References");
            references.Add(texture);
            newExp.WriteProperty(references);

            var blankStream = LegendaryExplorerCoreUtilities.LoadEmbeddedFile("blankswfimage.gfx");

            // From LE1R
            // Set up the SWF
            blankStream.Seek(0, SeekOrigin.Begin);
            MemoryStream dataStream = new MemoryStream();
            blankStream.CopyToEx(dataStream, 0x20); // SWFName Offset
            blankStream.ReadByte(); // Skip length 0 in the original file

            dataStream.WriteByte((byte)swfName.Length); // Write SWF Name Len (1 byte)
            dataStream.WriteStringASCII(swfName); // Write SWF Name

            blankStream.CopyToEx(dataStream, 0x18); // Copy bytes 0x21 - 0x39
            blankStream.ReadByte();

            dataStream.WriteByte((byte)(textureName.Length + 4)); // Write SWF Texture Name Len (1 byte, +4 for .tga)
            dataStream.WriteStringASCII(textureName + ".tga"); // Write SWF Texture Name

            blankStream.CopyTo(dataStream); // Copy the rest of the stream.

            // Update data lengths.
            var offset = (short)(swfName.Length - 9); // This is how much the length changed

            // SWF Size
            dataStream.Seek(4, SeekOrigin.Begin);
            dataStream.WriteInt32((int)dataStream.Length);

            // Exporter Info Tag - this is a fixed value
            dataStream.Seek(0x15, SeekOrigin.Begin);
            var len = dataStream.ReadInt16();
            len += offset;
            dataStream.Seek(-2, SeekOrigin.Current);
            dataStream.WriteInt16(len);

            // DefineExternalImage2 Tag
            dataStream.Seek(0x35 + offset, SeekOrigin.Begin);
            len = dataStream.ReadInt16();
            len += offset; // galMap001 is 9 chars long, so adjust it
            dataStream.Seek(-2, SeekOrigin.Current);
            dataStream.WriteInt16(len);

            newExp.WriteProperty(new ImmutableByteArrayProperty(dataStream.ToArray(), "RawData"));

            return newExp;
        }
    }

    [DebuggerDisplay(@"Texture2DMipInfo for {Export.ObjectName.Instanced} | {width}x{height} | {storageType}")]
    public class Texture2DMipInfo
    {
        public ExportEntry Export;
        public bool NeverStream; //copied from parent
        public int index;
        public int uncompressedSize;
        public int compressedSize;
        public int width;
        public int height;
        public int externalOffset;
        public StorageTypes storageType;
        private string _textureCacheName;
        public byte[] Mip;

        public bool IsPackageStored => !((StorageFlags)storageType).Has(StorageFlags.externalFile);

        public string TextureCacheName
        {
            get
            {
                if (Export.Game != MEGame.ME1) return _textureCacheName; //ME2/ME3 have property specifying the name. ME1 uses package lookup

                //ME1 externally references the UPKs. I think. It doesn't load external textures from SFMs
                string baseName = Export.FileRef.FollowLink(Export.idxLink).Split('.')[0].ToUpper() + ".upk"; //get top package name

                if (storageType is StorageTypes.extLZO or StorageTypes.extZlib or StorageTypes.extUnc)
                {
                    return baseName;
                }

                //NeverStream is set if there are more than 6 mips. Some sort of design implementation of ME1 texture streaming
                if (baseName != "" && !NeverStream)
                {
                    var gameFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME1);
                    if (gameFiles.ContainsKey(baseName)) //I am pretty sure these will only ever resolve to UPKs...
                    {
                        return baseName;
                    }
                }

                return null;
            }
            set => _textureCacheName = value; //This isn't INotifyProperty enabled so we don't need to SetProperty this
        }


        public string MipDisplayString
        {
            get
            {
                string mipinfostring = $"mipData {index} - {storageType}";
                if (storageType == StorageTypes.extLZO || storageType == StorageTypes.extZlib || storageType == StorageTypes.extUnc)
                {
                    mipinfostring += "\nLocated in: ";
                    mipinfostring += TextureCacheName ?? "(NULL!)";
                }

                if (storageType == StorageTypes.empty)
                {
                    mipinfostring += "\nEmpty mip";
                }
                else
                {
                    mipinfostring += $"\nUncompressed size: {uncompressedSize} ({FileSize.FormatSize(uncompressedSize)})\nCompressed size: {compressedSize} ({FileSize.FormatSize(compressedSize)})\nOffset: {externalOffset}\n{width}x{height}";
                }
                return mipinfostring;
            }
        }
    }
}
