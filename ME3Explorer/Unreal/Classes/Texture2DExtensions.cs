using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.Classes;
using MassEffectModder.Images;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Helpers;
using System.Globalization;
using ME3ExplorerCore.Gammtek.IO;

namespace ME3Explorer.Unreal.Classes
{
    public static class Texture2DExtensions
    {

        public static string Replace(this Texture2D t2d, Image image, PropertyCollection props, string fileSourcePath = null, string forcedTFCName = null)
        {
            string errors = "";
            var textureCache = forcedTFCName ?? t2d.GetTopMip().TextureCacheName;
            string fmt = t2d.TextureFormat;
            PixelFormat pixelFormat = Image.getPixelFormatType(fmt);
            t2d.RemoveEmptyMipsFromMipList();

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


            if (!image.checkDDSHaveAllMipmaps() || t2d.Mips.Count > 1 && image.mipMaps.Count() <= 1 || image.pixelFormat != newPixelFormat)
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

            if (t2d.Mips.Count == 1)
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
                    if (image.mipMaps[t].origWidth <= t2d.Mips[0].width &&
                        image.mipMaps[t].origHeight <= t2d.Mips[0].height &&
                        t2d.Mips.Count > 1)
                    {
                        if (!t2d.Mips.Exists(m => m.width == image.mipMaps[t].origWidth && m.height == image.mipMaps[t].origHeight))
                        {
                            image.mipMaps.RemoveAt(t--);
                        }
                    }
                }

                // put empty mips if missing
                for (int t = 0; t < t2d.Mips.Count; t++)
                {
                    if (t2d.Mips[t].width <= image.mipMaps[0].origWidth &&
                        t2d.Mips[t].height <= image.mipMaps[0].origHeight)
                    {
                        if (!image.mipMaps.Exists(m => m.origWidth == t2d.Mips[t].width && m.origHeight == t2d.Mips[t].height))
                        {
                            MipMap mipmap = new MipMap(t2d.Mips[t].width, t2d.Mips[t].height, pixelFormat);
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
                if (t2d.Export.Game == MEGame.ME2)
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, StorageTypes.extLZO)); //LZO 
                else
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, StorageTypes.extZlib)); //ZLib
            }

            List<Texture2DMipInfo> mipmaps = new List<Texture2DMipInfo>();
            for (int m = 0; m < image.mipMaps.Count(); m++)
            {

                Texture2DMipInfo mipmap = new Texture2DMipInfo();
                mipmap.Export = t2d.Export;
                mipmap.width = image.mipMaps[m].origWidth;
                mipmap.height = image.mipMaps[m].origHeight;
                mipmap.TextureCacheName = textureCache;
                if (t2d.Mips.Exists(x => x.width == mipmap.width && x.height == mipmap.height))
                {
                    var oldMip = t2d.Mips.First(x => x.width == mipmap.width && x.height == mipmap.height);
                    mipmap.storageType = oldMip.storageType;
                }
                else
                {
                    mipmap.storageType = t2d.Mips[0].storageType;
                    if (t2d.Mips.Count() > 1)
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
                if (t2d.Export.Game == MEGame.ME3)
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
                else if (t2d.Export.Game == MEGame.ME2)
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
                if (t2d.Mips.Count() == 1)
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
                if (t2d.Export.Game == MEGame.ME1)
                {
                    if (mipmap.storageType == StorageTypes.pccLZO ||
                        mipmap.storageType == StorageTypes.pccZlib)
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
                        mipmap.Mip = compressedMips[m];
                        mipmap.compressedSize = mipmap.Mip.Length;
                    }


                    if (mipmap.storageType == StorageTypes.pccUnc ||
                        mipmap.storageType == StorageTypes.extUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.Mip = image.mipMaps[m].data;
                    }

                    if (mipmap.storageType == StorageTypes.pccLZO || mipmap.storageType == StorageTypes.pccZlib)
                    {
                        mipmap.Mip = compressedMips[m];
                        mipmap.compressedSize = mipmap.Mip.Length;
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
                                        fs.Write(mipmap.Mip, 0, mipmap.compressedSize);
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
                                        fs.Write(mipmap.Mip, 0, mipmap.compressedSize);
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
                                    fs.Write(mipmap.Mip, 0, mipmap.compressedSize);
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
                if (t2d.Mips.Count() == 1)
                    break;
            }

            t2d.ReplaceMips(mipmaps);

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

            var hasNeverStream = props.GetProp<BoolProperty>("NeverStream") != null;
            if (locallyStored)
            {
                // Rules for default neverstream
                // 1. Must be Package Stored
                // 2. Must have at least 6 not empty mips
                if (mipmaps.Count >= 6)
                {
                    props.AddOrReplaceProp(new BoolProperty(true, "NeverStream"));
                }
                // Side case: NeverStream property was already set, we should respect the value
                // But won't that always be set here? 
                // Is there a time where we should remove NeverStream? I can't see any logical way
                // neverstream would be here
            }

            if (mipmaps.Count < 6)
            {
                props.RemoveNamedProperty("NeverStream");
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

            props.AddOrReplaceProp(new IntProperty(t2d.Mips.First().width, "SizeX"));
            props.AddOrReplaceProp(new IntProperty(t2d.Mips.First().height, "SizeY"));
            if (t2d.Export.Game < MEGame.ME3 && fileSourcePath != null)
            {
                props.AddOrReplaceProp(new StrProperty(fileSourcePath, "SourceFilePath"));
                props.AddOrReplaceProp(new StrProperty(File.GetLastWriteTimeUtc(fileSourcePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), "SourceFileTimestamp"));
            }

            var mipTailIdx = props.GetProp<IntProperty>("MipTailBaseIdx");
            if (mipTailIdx != null)
            {
                mipTailIdx.Value = t2d.Mips.Count - 1;
            }

            EndianReader mem = new EndianReader(new MemoryStream()) { Endian = t2d.Export.FileRef.Endian };
            props.WriteTo(mem.Writer, t2d.Export.FileRef);
            mem.Position = 0;
            var test = PropertyCollection.ReadProps(t2d.Export, mem.BaseStream, "Texture2D", true, true); //do not set properties as this may interfere with some other code. may change later.
            int propStart = t2d.Export.GetPropertyStart();
            var pos = mem.Position;
            mem.Position = 0;
            byte[] propData = mem.ToArray();
            if (t2d.Export.Game == MEGame.ME3)
            {
                t2d.Export.Data = t2d.Export.Data.Take(propStart).Concat(propData).Concat(t2d.SerializeNewData()).ToArray();
            }
            else
            {
                var array = t2d.Export.Data.Take(propStart).Concat(propData).ToArray();
                var testdata = new MemoryStream(array);
                var test2 = PropertyCollection.ReadProps(t2d.Export, testdata, "Texture2D", true, true, t2d.Export); //do not set properties as this may interfere with some other code. may change later.
                                                                                                                 //ME2 post-data is this right?
                t2d.Export.Data = t2d.Export.Data.Take(propStart).Concat(propData).Concat(t2d.SerializeNewData()).ToArray();
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
        public static bool ExportToPNG(this Texture2D t2d, string outputPath = null, Stream outStream = null)
        {
            if (outputPath == null && outStream == null)
                throw new Exception("ExportToPNG() requires at least one not-null parameter.");
            Texture2DMipInfo info = new Texture2DMipInfo();
            info = t2d.Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            if (info != null)
            {
                byte[] imageBytes = null;
                try
                {
                    imageBytes = Texture2D.GetTextureData(info);
                }
                catch (FileNotFoundException e)
                {
                    Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                    //External archive not found - using built in mips (will be hideous, but better than nothing)
                    info = t2d.Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                    if (info != null)
                    {
                        imageBytes = Texture2D.GetTextureData(info);
                    }
                }

                if (imageBytes != null)
                {
                    PixelFormat format = Image.getPixelFormatType(t2d.TextureFormat);

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

        public static byte[] GetPNG(this Texture2D t2d, Texture2DMipInfo info)
        {
            PixelFormat format = Image.getPixelFormatType(t2d.TextureFormat);

            MemoryStream ms = new MemoryStream();
            Image.convertToPng(Texture2D.GetTextureData(info), info.width, info.height, format).Save(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Creates a Direct 3D 11 textured based off the top mip of this Texture2D export
        /// </summary>
        /// <param name="device">Device to render texture from/to ?</param>
        /// <param name="description">Direct3D description of the texture</param>
        /// <returns></returns>
        public static SharpDX.Direct3D11.Texture2D generatePreviewTexture(this Texture2D t2d, SharpDX.Direct3D11.Device device, out SharpDX.Direct3D11.Texture2DDescription description, Texture2DMipInfo info = null, byte[] imageBytes = null)
        {
            if (info == null)
            {
                info = new Texture2DMipInfo();
                info = t2d.Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            }
            if (info == null)
            {
                description = new SharpDX.Direct3D11.Texture2DDescription();
                return null;
            }


            Debug.WriteLine($"Generating preview texture for Texture2D {info.Export.FullPath} of format {t2d.TextureFormat}");
            if (imageBytes == null)
            {
                imageBytes = t2d.GetImageBytesForMip(info);
            }
            int width = (int)info.width;
            int height = (int)info.height;
            var fmt = AmaroK86.ImageFormat.DDSImage.convertFormat(t2d.TextureFormat);
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
            description.Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm;
            description.SampleDescription.Count = 1;
            description.SampleDescription.Quality = 0;
            description.Usage = SharpDX.Direct3D11.ResourceUsage.Default;
            description.BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget;
            description.CpuAccessFlags = 0;
            description.OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.GenerateMipMaps;

            // Set up the texture data
            int stride = width * 4;
            SharpDX.DataStream ds = new SharpDX.DataStream(height * stride, true, true);
            ds.Write(pixels, 0, height * stride);
            ds.Position = 0;
            // Create texture
            SharpDX.Direct3D11.Texture2D tex = new SharpDX.Direct3D11.Texture2D(device, description, new SharpDX.DataRectangle(ds.DataPointer, stride));
            ds.Dispose();

            return tex;
        }

    }
}