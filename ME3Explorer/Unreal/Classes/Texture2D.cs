using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using Gammtek.Conduit.Extensions.IO;
using AmaroK86.ImageFormat;
using MassEffectModder;
using AmaroK86.MassEffect3.ZlibBlock;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using static MassEffectModder.Images.Image;
using System.Windows.Media.Imaging;
using Gammtek.Conduit.IO;
using MassEffectModder.Images;
using ME3Explorer.PackageEditor.TextureViewer;
using StreamHelpers;
using Buffer = System.Buffer;

namespace ME3Explorer.Unreal.Classes
{
    public class Texture2D
    {
        public List<Texture2DMipInfo> Mips { get; }
        public readonly bool NeverStream;
        public readonly ExportEntry Export;
        public readonly string TextureFormat;
        public Guid TextureGuid;

        public Texture2D(ExportEntry export)
        {
            Export = export;
            PropertyCollection properties = export.GetProperties();
            TextureFormat = properties.GetProp<EnumProperty>("Format").Value.Name;
            var cache = properties.GetProp<NameProperty>("TextureFileCacheName");

            NeverStream = properties.GetProp<BoolProperty>("NeverStream") ?? false;
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

        public static uint GetTextureCRC(ExportEntry export)
        {
            PropertyCollection properties = export.GetProperties();
            var format = properties.GetProp<EnumProperty>("Format");
            var cache = properties.GetProp<NameProperty>("TextureFileCacheName");
            List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(export, cache?.Value);
            var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            return Texture2D.GetMipCRC(topmip, format.Value);
        }

        public void RemoveEmptyMipsFromMipList()
        {
            Mips.RemoveAll(x => x.storageType == StorageTypes.empty);
        }

        public string Replace(Image image, PropertyCollection props, string fileSourcePath = null, string forcedTFCName = null)
        {
            string errors = "";
            var textureCache = forcedTFCName ?? GetTopMip().TextureCacheName;
            string fmt = TextureFormat;
            PixelFormat pixelFormat = Image.getPixelFormatType(fmt);
            RemoveEmptyMipsFromMipList();

            // Not sure what this does?
            // Remove all but one mip?
            //if (Export.Game == MEGame.ME1 && texture.mipMapsList.Count < 6)
            //{
            //    for (int i = texture.mipMapsList.Count - 1; i != 0; i--)
            //        texture.mipMapsList.RemoveAt(i);
            //}
            PixelFormat newPixelFormat = pixelFormat;

            //Changing Texture Type. Not sure what this is, exactly.
            //if (mod.markConvert)
            //    newPixelFormat = changeTextureType(pixelFormat, image.pixelFormat, ref package, ref texture);


            if (!image.checkDDSHaveAllMipmaps() || (Mips.Count > 1 && image.mipMaps.Count() <= 1) || (image.pixelFormat != newPixelFormat))
            //(!mod.markConvert && image.pixelFormat != pixelFormat))
            {
                bool dxt1HasAlpha = false;
                byte dxt1Threshold = 128;
                if (pixelFormat == PixelFormat.DXT1 && props.GetProp<EnumProperty>("CompressionSettings") is EnumProperty compressionSettings && compressionSettings.Value.Name == "TC_OneBitAlpha")
                {
                    dxt1HasAlpha = true;
                    if (image.pixelFormat == PixelFormat.ARGB ||
                        image.pixelFormat == PixelFormat.DXT3 ||
                        image.pixelFormat == PixelFormat.DXT5)
                    {
                        errors += "Warning: Texture was converted from full alpha to binary alpha." + Environment.NewLine;
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
                for (int t = 0; t < image.mipMaps.Count(); t++)
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
                            MipMap mipmap = new MipMap(Mips[t].width, Mips[t].height, pixelFormat);
                            image.mipMaps.Add(mipmap);
                        }
                    }
                }
            }

            //if (!texture.properties.exists("LODGroup"))
            //    texture.properties.setByteValue("LODGroup", "TEXTUREGROUP_Character", "TextureGroup", 1025);
            List<byte[]> compressedMips = new List<byte[]>();

            for (int m = 0; m < image.mipMaps.Count(); m++)
            {
                if (Export.Game == MEGame.ME2)
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, StorageTypes.extLZO)); //LZO 
                else
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, StorageTypes.extZlib)); //ZLib
            }

            List<Texture2DMipInfo> mipmaps = new List<Texture2DMipInfo>();
            for (int m = 0; m < image.mipMaps.Count(); m++)
            {

                Texture2DMipInfo mipmap = new Texture2DMipInfo();
                mipmap.Export = Export;
                mipmap.width = image.mipMaps[m].origWidth;
                mipmap.height = image.mipMaps[m].origHeight;
                mipmap.TextureCacheName = textureCache;
                if (Mips.Exists(x => x.width == mipmap.width && x.height == mipmap.height))
                {
                    var oldMip = Mips.First(x => x.width == mipmap.width && x.height == mipmap.height);
                    mipmap.storageType = oldMip.storageType;
                }
                else
                {
                    mipmap.storageType = Mips[0].storageType;
                    if (Mips.Count() > 1)
                    {
                        //Will implement later. ME3Explorer won't support global relinking, that's MEM's job.
                        //if (Export.Game == MEGame.ME1 && matched.linkToMaster == -1)
                        //{
                        //    if (mipmap.storageType == StorageTypes.pccUnc)
                        //    {
                        //        mipmap.storageType = StorageTypes.pccLZO;
                        //    }
                        //}
                        //else if (Export.Game == MEGame.ME1 && matched.linkToMaster != -1)
                        //{
                        //    if (mipmap.storageType == StorageTypes.pccUnc ||
                        //        mipmap.storageType == StorageTypes.pccLZO ||
                        //        mipmap.storageType == StorageTypes.pccZlib)
                        //    {
                        //        mipmap.storageType = StorageTypes.extLZO;
                        //    }
                        //}
                        //else 
                    }
                }

                //ME2,ME3: Force compression type (not implemented yet)
                if (Export.Game == MEGame.ME3)
                {
                    if (mipmap.storageType == StorageTypes.extLZO) //ME3 LZO -> ZLIB
                        mipmap.storageType = StorageTypes.extZlib;
                    if (mipmap.storageType == StorageTypes.pccLZO) //ME3 PCC LZO -> PCCZLIB
                        mipmap.storageType = StorageTypes.pccZlib;
                    if (mipmap.storageType == StorageTypes.extUnc) //ME3 Uncomp -> ZLib
                        mipmap.storageType = StorageTypes.extZlib;
                    //Leave here for future. WE might need this after dealing with double compression
                    //if (mipmap.storageType == StorageTypes.pccUnc && mipmap.width > 32) //ME3 Uncomp -> ZLib
                    //    mipmap.storageType = StorageTypes.pccZlib;
                    if (mipmap.storageType == StorageTypes.pccUnc && m < image.mipMaps.Count() - 6 && textureCache != null) //Moving texture to store externally.
                        mipmap.storageType = StorageTypes.extZlib;
                }
                else if (Export.Game == MEGame.ME2)
                {
                    if (mipmap.storageType == StorageTypes.extZlib) //ME2 ZLib -> LZO
                        mipmap.storageType = StorageTypes.extLZO;
                    if (mipmap.storageType == StorageTypes.pccZlib) //M2 ZLib -> LZO
                        mipmap.storageType = StorageTypes.pccLZO;
                    if (mipmap.storageType == StorageTypes.extUnc) //ME2 Uncomp -> LZO
                        mipmap.storageType = StorageTypes.extLZO;
                    //Leave here for future. We might neable this after dealing with double compression
                    //if (mipmap.storageType == StorageTypes.pccUnc && mipmap.width > 32) //ME2 Uncomp -> LZO
                    //    mipmap.storageType = StorageTypes.pccLZO;
                    if (mipmap.storageType == StorageTypes.pccUnc && m < image.mipMaps.Count() - 6 && textureCache != null) //Moving texture to store externally. make sure bottom 6 are pcc stored
                        mipmap.storageType = StorageTypes.extLZO;
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
                if (Mips.Count() == 1)
                    break;
            }

            #region MEM code comments. Should probably leave for reference

            //if (texture.properties.exists("TextureFileCacheName"))
            //{
            //    string archive = texture.properties.getProperty("TextureFileCacheName").valueName;
            //    if (mod.arcTfcDLC && mod.arcTfcName != archive)
            //        mod.arcTexture = null;

            //    if (mod.arcTexture == null)
            //    {
            //        archiveFile = Path.Combine(GameData.MainData, archive + ".tfc");
            //        if (matched.path.ToLowerInvariant().Contains("\\dlc"))
            //        {
            //            mod.arcTfcDLC = true;
            //            string DLCArchiveFile = Path.Combine(Path.GetDirectoryName(GameData.GamePath + matched.path), archive + ".tfc");
            //            if (File.Exists(DLCArchiveFile))
            //                archiveFile = DLCArchiveFile;
            //            else if (!File.Exists(archiveFile))
            //            {
            //                List<string> files = Directory.GetFiles(GameData.bioGamePath, archive + ".tfc",
            //                    SearchOption.AllDirectories).Where(item => item.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList();
            //                if (files.Count == 1)
            //                    archiveFile = files[0];
            //                else if (files.Count == 0)
            //                {
            //                    using (FileStream fs = new FileStream(DLCArchiveFile, FileMode.CreateNew, FileAccess.Write))
            //                    {
            //                        fs.WriteFromBuffer(texture.properties.getProperty("TFCFileGuid").valueStruct);
            //                    }
            //                    archiveFile = DLCArchiveFile;
            //                    newTfcFile = true;
            //                }
            //                else
            //                    throw new Exception("More instnces of TFC file: " + archive + ".tfc");
            //            }
            //        }
            //        else
            //        {
            //            mod.arcTfcDLC = false;
            //        }

            //        // check if texture fit in old space
            //        for (int mip = 0; mip < image.mipMaps.Count(); mip++)
            //        {
            //            Texture.MipMap testMipmap = new Texture.MipMap();
            //            testMipmap.width = image.mipMaps[mip].origWidth;
            //            testMipmap.height = image.mipMaps[mip].origHeight;
            //            if (ExistMipmap(testMipmap.width, testMipmap.height))
            //                testMipmap.storageType = texture.getMipmap(testMipmap.width, testMipmap.height).storageType;
            //            else
            //            {
            //                oldSpace = false;
            //                break;
            //            }

            //            if (testMipmap.storageType == StorageTypes.extZlib ||
            //                testMipmap.storageType == StorageTypes.extLZO)
            //            {
            //                Texture.MipMap oldTestMipmap = texture.getMipmap(testMipmap.width, testMipmap.height);
            //                if (mod.cacheCprMipmaps[mip].Length > oldTestMipmap.compressedSize)
            //                {
            //                    oldSpace = false;
            //                    break;
            //                }
            //            }
            //            if (texture.mipMapsList.Count() == 1)
            //                break;
            //        }

            //        long fileLength = new FileInfo(archiveFile).Length;
            //        if (!oldSpace && fileLength + 0x5000000 > 0x80000000)
            //        {
            //            archiveFile = "";
            //            foreach (TFCTexture newGuid in guids)
            //            {
            //                archiveFile = Path.Combine(GameData.MainData, newGuid.name + ".tfc");
            //                if (!File.Exists(archiveFile))
            //                {
            //                    texture.properties.setNameValue("TextureFileCacheName", newGuid.name);
            //                    texture.properties.setStructValue("TFCFileGuid", "Guid", newGuid.guid);
            //                    using (FileStream fs = new FileStream(archiveFile, FileMode.CreateNew, FileAccess.Write))
            //                    {
            //                        fs.WriteFromBuffer(newGuid.guid);
            //                    }
            //                    newTfcFile = true;
            //                    break;
            //                }
            //                else
            //                {
            //                    fileLength = new FileInfo(archiveFile).Length;
            //                    if (fileLength + 0x5000000 < 0x80000000)
            //                    {
            //                        texture.properties.setNameValue("TextureFileCacheName", newGuid.name);
            //                        texture.properties.setStructValue("TFCFileGuid", "Guid", newGuid.guid);
            //                        break;
            //                    }
            //                }
            //                archiveFile = "";
            //            }
            //            if (archiveFile == "")
            //                throw new Exception("No free TFC texture file!");
            //        }
            //    }
            //    else
            //    {
            //        texture.properties.setNameValue("TextureFileCacheName", mod.arcTfcName);
            //        texture.properties.setStructValue("TFCFileGuid", "Guid", mod.arcTfcGuid);
            //    }
            //}

            #endregion

            int allextmipssize = 0;

            for (int m = 0; m < image.mipMaps.Count(); m++)
            {
                Texture2DMipInfo x = mipmaps[m];
                var compsize = image.mipMaps[m].data.Length;

                if (x.storageType == StorageTypes.extZlib ||
                        x.storageType == StorageTypes.extLZO ||
                        x.storageType == StorageTypes.extUnc)
                {
                    allextmipssize += compsize; //compsize on Unc textures is same as LZO/ZLib
                }
            }
            //todo: check to make sure TFC will not be larger than 2GiB
            Guid tfcGuid = Guid.NewGuid(); //make new guid as storage
            bool locallyStored = mipmaps[0].storageType == StorageTypes.pccUnc || mipmaps[0].storageType == StorageTypes.pccZlib || mipmaps[0].storageType == StorageTypes.pccLZO;
            for (int m = 0; m < image.mipMaps.Count(); m++)
            {
                Texture2DMipInfo mipmap = mipmaps[m];
                //if (mipmap.width > 32)
                //{
                //    if (mipmap.storageType == StorageTypes.pccUnc)
                //    {
                //        mipmap.storageType = Export.Game == MEGame.ME2 ? StorageTypes.pccLZO : StorageTypes.pccZlib;
                //    }
                //    if (mipmap.storageType == StorageTypes.extUnc)
                //    {
                //        mipmap.storageType = Export.Game == MEGame.ME2 ? StorageTypes.extLZO : StorageTypes.extZlib;
                //    }
                //}

                mipmap.uncompressedSize = image.mipMaps[m].data.Length;
                if (Export.Game == MEGame.ME1)
                {
                    if (mipmap.storageType == StorageTypes.pccLZO ||
                        mipmap.storageType == StorageTypes.pccZlib)
                    {
                        mipmap.newDataForSerializing = compressedMips[m];
                        mipmap.compressedSize = mipmap.newDataForSerializing.Length;
                    }
                    else if (mipmap.storageType == StorageTypes.pccUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.newDataForSerializing = image.mipMaps[m].data;
                    }
                    else
                    {
                        throw new Exception("Unknown mip storage type!");
                    }
                }
                else
                {
                    if (mipmap.storageType == StorageTypes.extZlib ||
                        mipmap.storageType == StorageTypes.extLZO)
                    {
                        if (compressedMips.Count != image.mipMaps.Count())
                            throw new Exception("Amount of compressed mips does not match number of mips of incoming image!");
                        mipmap.newDataForSerializing = compressedMips[m];
                        mipmap.compressedSize = mipmap.newDataForSerializing.Length;
                    }


                    if (mipmap.storageType == StorageTypes.pccUnc ||
                        mipmap.storageType == StorageTypes.extUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.newDataForSerializing = image.mipMaps[m].data;
                    }

                    if (mipmap.storageType == StorageTypes.pccLZO || mipmap.storageType == StorageTypes.pccZlib)
                    {
                        mipmap.newDataForSerializing = compressedMips[m];
                        mipmap.compressedSize = mipmap.newDataForSerializing.Length;
                    }

                    if (mipmap.storageType == StorageTypes.extZlib ||
                        mipmap.storageType == StorageTypes.extLZO ||
                        mipmap.storageType == StorageTypes.extUnc)
                    {
                        if (!string.IsNullOrEmpty(mipmap.TextureCacheName) && mipmap.Export.Game != MEGame.ME1)
                        {
                            //Check local dir
                            string tfcarchive = mipmap.TextureCacheName + ".tfc";
                            var localDirectoryTFCPath = Path.Combine(Path.GetDirectoryName(mipmap.Export.FileRef.FilePath), tfcarchive);
                            if (File.Exists(localDirectoryTFCPath))
                            {
                                try
                                {
                                    using (FileStream fs = new FileStream(localDirectoryTFCPath, FileMode.Open, FileAccess.ReadWrite))
                                    {
                                        tfcGuid = fs.ReadGuid();
                                        fs.Seek(0, SeekOrigin.End);
                                        mipmap.externalOffset = (int)fs.Position;
                                        fs.Write(mipmap.newDataForSerializing, 0, mipmap.compressedSize);
                                    }
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
                                    using (FileStream fs = new FileStream(archiveFile, FileMode.Open, FileAccess.ReadWrite))
                                    {
                                        tfcGuid = fs.ReadGuid();
                                        fs.Seek(0, SeekOrigin.End);
                                        mipmap.externalOffset = (int)fs.Position;
                                        fs.Write(mipmap.newDataForSerializing, 0, mipmap.compressedSize);
                                    }
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
                                using (FileStream fs = new FileStream(localDirectoryTFCPath, FileMode.OpenOrCreate, FileAccess.Write))
                                {
                                    fs.WriteGuid(tfcGuid);
                                    mipmap.externalOffset = (int)fs.Position;
                                    fs.Write(mipmap.newDataForSerializing, 0, mipmap.compressedSize);
                                }
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
                if (Mips.Count() == 1)
                    break;
            }

            ReplaceMips(mipmaps);

            //Set properties


            // The bottom 6 mips are apparently always pcc stored. If there is less than 6 mips, set neverstream to true, which tells game
            // and toolset to never look into archives for mips.
            //if (Export.Game == MEGame.ME2 || Export.Game == MEGame.ME3)
            //{
            //    if (texture.properties.exists("TextureFileCacheName"))
            //    {
            //        if (texture.mipMapsList.Count < 6)
            //        {
            //            mipmap.storageType = StorageTypes.pccUnc;
            //            texture.properties.setBoolValue("NeverStream", true);
            //        }
            //        else
            //        {
            //            if (Export.Game == MEGame.ME2)
            //                mipmap.storageType = StorageTypes.extLZO;
            //            else
            //                mipmap.storageType = StorageTypes.extZlib;
            //        }
            //    }
            //}
            if (mipmaps.Count < 6)
            {
                props.AddOrReplaceProp(new BoolProperty(true, "NeverStream"));
            }
            else
            {
                var neverStream = props.GetProp<BoolProperty>("NeverStream");
                if (neverStream != null)
                {
                    props.Remove(neverStream);
                }
            }

            if (!locallyStored)
            {
                props.AddOrReplaceProp(tfcGuid.ToGuidStructProp("TFCFileGuid"));
                if (mipmaps[0].storageType == StorageTypes.extLZO || mipmaps[0].storageType == StorageTypes.extUnc || mipmaps[0].storageType == StorageTypes.extZlib)
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

            EndianReader mem = new EndianReader(new MemoryStream()) { Endian = Export.FileRef.Endian };
            props.WriteTo(mem.Writer, Export.FileRef);
            mem.Position = 0;
            var test = PropertyCollection.ReadProps(Export, mem.BaseStream, "Texture2D", true, true); //do not set properties as this may interfere with some other code. may change later.
            int propStart = Export.GetPropertyStart();
            var pos = mem.Position;
            mem.Position = 0;
            byte[] propData = mem.ToArray();
            if (Export.Game == MEGame.ME3)
            {
                Export.Data = Export.Data.Take(propStart).Concat(propData).Concat(SerializeNewData()).ToArray();
            }
            else
            {
                var array = Export.Data.Take(propStart).Concat(propData).ToArray();
                var testdata = new MemoryStream(array);
                var test2 = PropertyCollection.ReadProps(Export, testdata, "Texture2D", true, true, Export); //do not set properties as this may interfere with some other code. may change later.
                                                                                                                                  //ME2 post-data is this right?
                Export.Data = Export.Data.Take(propStart).Concat(propData).Concat(SerializeNewData()).ToArray();
            }

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


            return errors;
        }


        /// <summary>
        /// Exports the texture to PNG format. Writes to the specified stream, or the specified path if not defined.
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="outStream"></param>
        /// <returns></returns>
        public bool ExportToPNG(string outputPath = null, Stream outStream = null)
        {
            if (outputPath == null && outStream == null)
                throw new Exception("ExportToPNG() requires at least one not-null parameter.");
            Texture2DMipInfo info = new Texture2DMipInfo();
            info = Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            if (info != null)
            {
                byte[] imageBytes = null;
                try
                {
                    imageBytes = GetTextureData(info);
                }
                catch (FileNotFoundException e)
                {
                    Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                    //External archive not found - using built in mips (will be hideous, but better than nothing)
                    info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                    if (info != null)
                    {
                        imageBytes = GetTextureData(info);
                    }
                }

                if (imageBytes != null)
                {
                    PixelFormat format = Image.getPixelFormatType(TextureFormat);

                    PngBitmapEncoder image = Image.convertToPng(imageBytes, info.width, info.height, format);
                    if (outStream == null)
                    {
                        outStream = new FileStream(outputPath, FileMode.Create);
                        image.Save(outStream);
                        outStream.Close();
                    }
                    else
                    {
                        image.Save(outStream);
                    }
                }
            }

            return true;
        }

        public byte[] GetPNG(Texture2DMipInfo info)
        {
            PixelFormat format = getPixelFormatType(TextureFormat);

            MemoryStream ms = new MemoryStream();
            convertToPng(GetTextureData(info), info.width, info.height, format).Save(ms);
            return ms.ToArray();
        }


        /// <summary>
        /// Creates a Direct 3D 11 textured based off the top mip of this Texture2D export
        /// </summary>
        /// <param name="device">Device to render texture from/to ?</param>
        /// <param name="description">Direct3D description of the texture</param>
        /// <returns></returns>
        public SharpDX.Direct3D11.Texture2D generatePreviewTexture(Device device, out Texture2DDescription description, Texture2DMipInfo info = null, byte[] imageBytes = null)
        {
            if (info == null)
            {
                info = new Texture2DMipInfo();
                info = Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            }
            if (info == null)
            {
                description = new Texture2DDescription();
                return null;
            }


            Debug.WriteLine($"Generating preview texture for Texture2D {info.Export.FullPath} of format {TextureFormat}");
            if (imageBytes == null)
            {
                imageBytes = GetImageBytesForMip(info);
            }
            int width = (int)info.width;
            int height = (int)info.height;
            var fmt = AmaroK86.ImageFormat.DDSImage.convertFormat(TextureFormat);
            var bmp = AmaroK86.ImageFormat.DDSImage.ToBitmap(imageBytes, fmt, info.width, info.height);
            // Convert compressed image data to an A8R8G8B8 System.Drawing.Bitmap
            /* DDSFormat format;
            const Format dxformat = Format.B8G8R8A8_UNorm;
            switch (texFormat)
            {
                case "DXT1":
                    format = DDSFormat.DXT1;
                    break;
                case "DXT5":
                    format = DDSFormat.DXT5;
                    break;
                case "V8U8":
                    format = DDSFormat.V8U8;
                    break;
                case "G8":
                    format = DDSFormat.G8;
                    break;
                case "A8R8G8B8":
                    format = DDSFormat.ARGB;
                    break;
                case "NormalMap_HQ":
                    format = DDSFormat.ATI2;
                    break;
                default:
                    throw new FormatException("Unknown texture format: " + texFormat);
            }

            byte[] compressedData = extractRawData(info, pccRef);
            Bitmap bmp = DDSImage.ToBitmap(compressedData, format, width, height); */

            // Load the decompressed data into an array
            System.Drawing.Imaging.BitmapData data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var pixels = new byte[data.Stride * data.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(data);

            // Create description of texture
            description.Width = width;
            description.Height = height;
            description.MipLevels = 1;
            description.ArraySize = 1;
            description.Format = Format.B8G8R8A8_UNorm;
            description.SampleDescription.Count = 1;
            description.SampleDescription.Quality = 0;
            description.Usage = ResourceUsage.Default;
            description.BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget;
            description.CpuAccessFlags = 0;
            description.OptionFlags = ResourceOptionFlags.GenerateMipMaps;

            // Set up the texture data
            int stride = width * 4;
            DataStream ds = new DataStream(height * stride, true, true);
            ds.Write(pixels, 0, height * stride);
            ds.Position = 0;
            // Create texture
            SharpDX.Direct3D11.Texture2D tex = new SharpDX.Direct3D11.Texture2D(device, description, new DataRectangle(ds.DataPointer, stride));
            ds.Dispose();

            return tex;
        }

        internal Texture2DMipInfo GetTopMip()
        {
            return Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
        }

        public static List<Texture2DMipInfo> GetTexture2DMipInfos(ExportEntry exportEntry, string cacheName)
        {
            EndianReader ms = new EndianReader(new MemoryStream(exportEntry.Data)) { Endian = exportEntry.FileRef.Endian };
            ms.Seek(exportEntry.propsEnd(), SeekOrigin.Begin);
            if (exportEntry.FileRef.Game != MEGame.ME3)
            {
                ms.Skip(4);//BulkDataFlags
                ms.Skip(4);//ElementCount
                int bulkDataSize = ms.ReadInt32();
                ms.Seek(4, SeekOrigin.Current); // position in the package
                ms.Skip(bulkDataSize); //skips over thumbnail png, if it exists
            }

            var mips = new List<Texture2DMipInfo>();
            int numMipMaps = ms.ReadInt32();
            for (int l = 0; l < numMipMaps; l++)
            {
                Texture2DMipInfo mip = new Texture2DMipInfo
                {
                    Export = exportEntry,
                    index = l,
                    storageType = (StorageTypes)ms.ReadInt32(),
                    uncompressedSize = ms.ReadInt32(),
                    compressedSize = ms.ReadInt32(),
                    externalOffset = ms.ReadInt32(),
                    localExportOffset = (int)ms.Position,
                    TextureCacheName = cacheName //If this is ME1, this will simply be ignored in the setter
                };
                switch (mip.storageType)
                {
                    case StorageTypes.pccUnc:
                        ms.Seek(mip.uncompressedSize, SeekOrigin.Current);
                        break;
                    case StorageTypes.pccLZO:
                    case StorageTypes.pccZlib:
                        ms.Seek(mip.compressedSize, SeekOrigin.Current);
                        break;
                }

                mip.width = ms.ReadInt32();
                mip.height = ms.ReadInt32();
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

        public byte[] GetImageBytesForMip(Texture2DMipInfo info)
        {
            byte[] imageBytes = null;
            try
            {
                imageBytes = GetTextureData(info);
            }
            catch (FileNotFoundException e)
            {
                Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                //External archive not found - using built in mips (will be hideous, but better than nothing)
                info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                if (info != null)
                {
                    imageBytes = GetTextureData(info);
                }
            }
            if (imageBytes == null)
            {
                throw new Exception("Could not fetch texture data for texture " + info?.Export.ObjectName.Instanced);
            }
            return imageBytes;
        }

        internal byte[] SerializeNewData()
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
                    ms.Write(mip.newDataForSerializing, 0, mip.newDataForSerializing.Length);
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

        internal void ReplaceMips(List<Texture2DMipInfo> mipmaps)
        {
            Mips.Clear();
            Mips.AddRange(mipmaps);
        }


        public static byte[] GetTextureData(Texture2DMipInfo mipToLoad, bool decompress = true)
        {
            var imagebytes = new byte[decompress ? mipToLoad.uncompressedSize : mipToLoad.compressedSize];
            //Debug.WriteLine("getting texture data for " + mipToLoad.Export.FullPath);
            if (mipToLoad.storageType == StorageTypes.pccUnc)
            {
                Buffer.BlockCopy(mipToLoad.Export.Data, mipToLoad.localExportOffset, imagebytes, 0, mipToLoad.uncompressedSize);
            }
            else if (mipToLoad.storageType == StorageTypes.pccLZO || mipToLoad.storageType == StorageTypes.pccZlib)
            {
                if (decompress)
                {
                    try
                    {
                        TextureCompression.DecompressTexture(imagebytes,
                                                             new MemoryStream(mipToLoad.Export.Data, mipToLoad.localExportOffset, mipToLoad.compressedSize),
                                                             mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{e.Message}\nStorageType: {mipToLoad.storageType}\n");
                    }
                }
                else
                {
                    Buffer.BlockCopy(mipToLoad.Export.Data, mipToLoad.localExportOffset, imagebytes, 0, mipToLoad.compressedSize);
                }
            }
            else if (mipToLoad.storageType == StorageTypes.extUnc || mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib || mipToLoad.storageType == StorageTypes.extLZMA)
            {
                string filename = null;
                var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(mipToLoad.Export.Game, true, true);
                if (mipToLoad.Export.Game == MEGame.ME1)
                {
                    if (loadedFiles.TryGetValue(mipToLoad.TextureCacheName, out var fullPath))
                    {
                        filename = fullPath;
                    }
                    else
                    {
                        throw new FileNotFoundException($"Externally referenced texture file not found in game: {mipToLoad.TextureCacheName}.");
                    }
                }
                else
                {
                    string archive = mipToLoad.TextureCacheName + ".tfc";
                    var localDirectoryTFCPath = Path.Combine(Path.GetDirectoryName(mipToLoad.Export.FileRef.FilePath), archive);
                    if (File.Exists(localDirectoryTFCPath))
                    {
                        filename = localDirectoryTFCPath;
                    }
                    else
                    {
                        {
                            if (loadedFiles.TryGetValue(archive, out var fullPath))
                            {
                                filename = fullPath;
                            }
                            else
                            {
                                throw new FileNotFoundException($"Externally referenced texture cache not found: {archive}.");
                            }
                        }
                    }
                }

                //exceptions above will prevent filename from being null here

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
                                    using (MemoryStream tmpStream = new MemoryStream(fs.ReadBytes(mipToLoad.compressedSize)))
                                    {
                                        try
                                        {
                                            TextureCompression.DecompressTexture(imagebytes, tmpStream, mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                                        }
                                        catch (Exception e)
                                        {
                                            throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                                                                "StorageType: " + mipToLoad.storageType + "\n" +
                                                                "External file offset: " + mipToLoad.externalOffset);
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
                            throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                                "StorageType: " + mipToLoad.storageType + "\n" +
                                "External file offset: " + mipToLoad.externalOffset);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                        "StorageType: " + mipToLoad.storageType + "\n" +
                        "External file offset: " + mipToLoad.externalOffset);
                }
            }
            return imagebytes;
        }

        public static uint GetMipCRC(Texture2DMipInfo mip, string textureFormat)
        {
            byte[] data = GetTextureData(mip);
            if (data == null) return 0;
            if (textureFormat == "PF_NormalMap_HQ")
            {
                // only ME1 and ME2
                return (uint)~ParallelCRC.Compute(data, 0, data.Length / 2);
            }
            return (uint)~ParallelCRC.Compute(data);
        }
    }

    [DebuggerDisplay("Texture2DMipInfo for {Export.ObjectName.Instanced} | {width}x{height} | {storageType}")]
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
        public int localExportOffset;
        public StorageTypes storageType;
        private string _textureCacheName;
        public byte[] newDataForSerializing;

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
                string mipinfostring = "Mip " + index;
                mipinfostring += "\nStorage Type: ";
                mipinfostring += storageType;
                if (storageType == StorageTypes.extLZO || storageType == StorageTypes.extZlib || storageType == StorageTypes.extUnc)
                {
                    mipinfostring += "\nLocated in: ";
                    mipinfostring += TextureCacheName ?? "(NULL!)";
                }

                mipinfostring += "\nUncompressed size: ";
                mipinfostring += uncompressedSize;
                mipinfostring += "\nCompressed size: ";
                mipinfostring += compressedSize;
                mipinfostring += "\nOffset: ";
                mipinfostring += externalOffset;
                mipinfostring += "\nWidth: ";
                mipinfostring += width;
                mipinfostring += "\nHeight: ";
                mipinfostring += height;
                return mipinfostring;
            }
        }
    }
}
