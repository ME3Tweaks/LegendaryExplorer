using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3ExplorerCore.Unreal.Classes
{
    public class Texture2D
    {
        public List<Texture2DMipInfo> Mips { get; }
        public readonly bool NeverStream;
        public readonly ExportEntry Export;
        public readonly string TextureFormat;
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
                int guidOffsetFromEnd = export.Game == MEGame.ME3 ? 20 : 16;
                if (export.ClassName == "LightMapTexture2D")
                {
                    guidOffsetFromEnd += 4;
                }
                TextureGuid = new Guid(Export.Data.Skip(Export.Data.Length - guidOffsetFromEnd).Take(16).ToArray());
            }
        }

        public static uint GetTextureCRC(ExportEntry export, List<string> additionalTFCs = null)
        {
            PropertyCollection properties = export.GetProperties();
            var format = properties.GetProp<EnumProperty>("Format");
            var cache = properties.GetProp<NameProperty>("TextureFileCacheName");
            List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(export, cache?.Value);
            var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            return Texture2D.GetMipCRC(topmip, format.Value, additionalTFCs: additionalTFCs);
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
            UTexture2D texBin = exportEntry.GetBinaryData<UTexture2D>();
            var mips = new List<Texture2DMipInfo>();
            for (int i = 0; i < texBin.Mips.Count; i++)
            {
                UTexture2D.Texture2DMipMap binMip = texBin.Mips[i];
                Texture2DMipInfo mip = new Texture2DMipInfo
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

        public byte[] GetImageBytesForMip(Texture2DMipInfo info, MEGame game, bool useLowerMipsIfTFCMissing, string gamePathToUse = null, List<string> additionalTFCs = null)
        {
            byte[] imageBytes = null;
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
                        imageBytes = GetTextureData(info, game, gamePathToUse);
                    }
                }
                else
                {
                    throw e; //rethrow
                }
            }
            if (imageBytes == null)
            {
                throw new Exception(GetLocalizedCouldNotFetchTextureDataMessage(info?.Export.InstancedFullPath, info?.Export.FileRef.FilePath));
            }
            return imageBytes;
        }

        public byte[] SerializeNewData()
        {
            MemoryStream ms = new MemoryStream();
            if (Export.FileRef.Game != MEGame.ME3)
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
                if (mip.storageType == StorageTypes.pccUnc ||
                    mip.storageType == StorageTypes.pccLZO ||
                    mip.storageType == StorageTypes.pccZlib)
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
            if (Export.Game == MEGame.ME3)
            {
                ms.WriteInt32(0);
                if (Export.ClassName == "LightMapTexture2D")
                {
                    ms.WriteInt32(0);
                }
            }
            return ms.ToArray();
        }

        public void ReplaceMips(List<Texture2DMipInfo> mipmaps)
        {
            Mips.Clear();
            Mips.AddRange(mipmaps);
        }


        public static byte[] GetTextureData(Texture2DMipInfo mipToLoad, MEGame game, string gamePathToUse = null, bool decompress = true, List<string> additionalTFCs = null)
        {
            bool dataLoaded = false;
            var imagebytes = new byte[decompress ? mipToLoad.uncompressedSize : mipToLoad.compressedSize];
            //Debug.WriteLine("getting texture data for " + mipToLoad.Export.FullPath);
            if (mipToLoad.storageType == StorageTypes.pccUnc)
            {
                Buffer.BlockCopy(mipToLoad.Mip, 0, imagebytes, 0, mipToLoad.uncompressedSize);
            }
            else if (mipToLoad.storageType == StorageTypes.pccLZO || mipToLoad.storageType == StorageTypes.pccZlib)
            {
                if (decompress)
                {
                    try
                    {
                        TextureCompression.DecompressTexture(imagebytes,
                                                             new MemoryStream(mipToLoad.Mip),
                                                             mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                    }
                    catch (Exception e)
                    {
                        throw new Exception(GetLocalizedTextureExceptionInternalMessage(e.Message, mipToLoad.storageType.ToString()));
                    }
                }
                else
                {
                    Buffer.BlockCopy(mipToLoad.Mip, 0, imagebytes, 0, mipToLoad.compressedSize);
                }
            }
            else if (mipToLoad.storageType == StorageTypes.extUnc || mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib || mipToLoad.storageType == StorageTypes.extLZMA)
            {
                string filename = null;
                List<string> loadedFiles = MELoadedFiles.GetAllGameFiles(gamePathToUse, game, false, true);
                if (mipToLoad.Export.Game == MEGame.ME1)
                {
                    var fullPath = loadedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(mipToLoad.TextureCacheName, StringComparison.InvariantCultureIgnoreCase));

                    if (fullPath != null)
                    {
                        filename = fullPath;
                    }
                    else
                    {
                        fullPath = AdditionalME1MasterTexturePackages.FirstOrDefault(x => Path.GetFileName(x).Equals(mipToLoad.TextureCacheName, StringComparison.InvariantCultureIgnoreCase));
                        if (fullPath == null)
                        {
                            throw new FileNotFoundException(GetLocalizedCouldNotFindME1TexturePackageMessage(mipToLoad.TextureCacheName));
                        }
                        filename = fullPath;
                    }
                }
                else
                {
                    string archive = mipToLoad.TextureCacheName + @".tfc";

                    var localDirectoryTFCPath = Path.Combine(Path.GetDirectoryName(mipToLoad.Export.FileRef.FilePath), archive);
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
                        var tfcs = loadedFiles.Where(x => x.EndsWith(@".tfc")).ToList();

                        var fullPath = loadedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(archive, StringComparison.InvariantCultureIgnoreCase));
                        if (fullPath != null)
                        {
                            filename = fullPath;
                        }
                        else if (game == MEGame.ME3 && mipToLoad.TextureCacheName.StartsWith(@"Textures_DLC"))
                        {
                            // Check SFAR
                            var dlcName = mipToLoad.TextureCacheName.Substring(9);
                            if (MEDirectories.OfficialDLC(MEGame.ME3).Contains(dlcName) && ME3Directory.DLCPath != null)
                            {
                                var sfarPath = Path.Combine(ME3Directory.DLCPath, dlcName, "CookedPCConsole", "Default.sfar");
                                if (File.Exists(sfarPath))
                                {
                                    DLCPackage dpackage = new DLCPackage(sfarPath);
                                    var entryId = dpackage.FindFileEntry(archive);
                                    if (entryId >= 0)
                                    {
                                        // TFC is in this SFAR
                                        imagebytes = dpackage.ReadFromEntry(entryId, mipToLoad.externalOffset, mipToLoad.uncompressedSize);
                                        dataLoaded = true;
                                    }
                                    else
                                    {
                                        // File not in archive
                                        throw new FileNotFoundException(GetLocalizedCouldNotFindME2ME3TextureCacheMessage(archive));
                                    }
                                }
                                else
                                {
                                    // SFAR not in folder
                                    throw new FileNotFoundException(GetLocalizedCouldNotFindME2ME3TextureCacheMessage(archive));
                                }
                            }
                            else
                            {
                                // Not an official DLC
                                throw new FileNotFoundException(GetLocalizedCouldNotFindME2ME3TextureCacheMessage(archive));
                            }
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
                        using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                        {
                            try
                            {
                                fs.Seek(mipToLoad.externalOffset, SeekOrigin.Begin);
                                if (mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib)
                                {
                                    if (decompress)
                                    {
                                        using (MemoryStream tmpStream = fs.ReadToMemoryStream(mipToLoad.compressedSize))
                                        {
                                            try
                                            {
                                                TextureCompression.DecompressTexture(imagebytes, tmpStream, mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                                            }
                                            catch (Exception e)
                                            {
                                                throw new Exception(GetLocalizedTextureExceptionExternalMessage(e.Message, filename, mipToLoad.storageType.ToString(), mipToLoad.externalOffset.ToString()));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        fs.Read(imagebytes, 0, mipToLoad.compressedSize);
                                    }
                                }
                                else
                                {
                                    fs.Read(imagebytes, 0, mipToLoad.uncompressedSize);
                                }
                            }
                            catch (Exception e)
                            {
                                throw new Exception(GetLocalizedTextureExceptionExternalMessage(e.Message, filename, mipToLoad.storageType.ToString(), mipToLoad.externalOffset.ToString()));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception(GetLocalizedTextureExceptionExternalMessage(e.Message, filename, mipToLoad.storageType.ToString(), mipToLoad.externalOffset.ToString()));
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
                return (uint)~ParallelCRC.Compute(data, 0, data.Length / 2);
            }
            return (uint)~ParallelCRC.Compute(data);
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

        public string TextureCacheName
        {
            get
            {
                if (Export.Game != MEGame.ME1) return _textureCacheName; //ME2/ME3 have property specifying the name. ME1 uses package lookup

                //ME1 externally references the UPKs. I think. It doesn't load external textures from SFMs
                string baseName = Export.FileRef.FollowLink(Export.idxLink).Split('.')[0].ToUpper() + ".upk"; //get top package name

                if (storageType == StorageTypes.extLZO || storageType == StorageTypes.extZlib || storageType == StorageTypes.extUnc)
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
                string mipinfostring = $"Mip {index} - {storageType}";
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
