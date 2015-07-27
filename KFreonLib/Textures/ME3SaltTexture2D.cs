using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.ImageFormat;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Threading;
using KFreonLib.PCCObjects;

namespace KFreonLib.Textures
{
    public class ME3SaltTexture2D : ITexture2D
    {
        public enum storage
        {
            arcCpr = 0x3, // archive compressed
            arcUnc = 0x1, // archive uncompressed (DLC)
            pccSto = 0x0, // pcc local storage
            empty = 0x21  // unused image (void pointer sorta)
        }

        public struct ImageInfo : IImageInfo
        {
            public storage storageType;
            public int cprSize { get; set; }

            public ImageSize imgSize
            {
                get;
                set;
            }

            public int offset
            {
                get;
                set;
            }

            public int GameVersion
            {
                get;
                set;
            }

            int IImageInfo.storageType
            {
                get
                {
                    return (int)storageType;
                }
                set
                {
                    storageType = (storage)value;
                }
            }


            public int uncSize
            {
                get;
                set;
            }

            public bool CompareStorage(string storage)
            {
                bool retval = false;
                switch (storage)
                {
                    case "pccSto":
                        retval = (int)ME3SaltTexture2D.storage.pccSto == storageType;
                        break;
                    case "arcCpr":
                        retval = (int)ME3SaltTexture2D.storage.arcCpr == (int)storageType;
                        break;
                    case "arcUnc":
                        retval = (int)ME3SaltTexture2D.storage.arcUnc == (int)storageType;
                        break;
                    case "empty":
                        retval = (int)ME3SaltTexture2D.storage.empty == (int)storageType;
                        break;
                }
                return retval;
            }
        }

        public const string className = "Texture2D";
        public const string class2 = "LightMapTexture2D";
        public const string class3 = "TextureFlipBook";
        public const string CustCache = "CustTextures";
        public string texName { get; set; }
        public string arcName { get; set; }
        public string FullArcPath { get; set; }
        public string LODGroup { get; set; }
        public uint Hash { get; set; }
        public byte[] headerData;
        public byte[] imageData;
        private uint dataOffset = 0;
        private uint numMipMaps;
        public Dictionary<string, SaltPropertyReader.Property> properties;

        public int exportOffset;
        private byte[] footerData;
        public int UnpackNum;
        private int ArcDataSize;
        public String Class;

        public ME3SaltTexture2D()
        {
            allPccs = new List<String>();
            hasChanged = false;
        }

        public ME3SaltTexture2D(string name, List<string> pccs, List<int> ExpIDs, uint hash, string pathBIOGame, int GameVersion)
        {
            hasChanged = false;
            texName = name;

            List<string> temppccs = new List<string>(pccs);
            List<int> tempexp = new List<int>(ExpIDs);
            //KFreonLib.PCCObjects.Misc.ReorderFiles(ref temppccs, ref tempexp, pathBIOGame, GameVersion);

            allPccs = temppccs;
            expIDs = tempexp;
            Hash = hash;
            privateimgList = new List<ImageInfo>();
        }

        public ME3SaltTexture2D(ME3PCCObject pccObj, int texIdx, String pathBioGame, uint hash = 0)
        {
            allPccs = new List<string>();
            hasChanged = false;
            Hash = hash;

            if (pccObj.isExport(texIdx) && (pccObj.Exports[texIdx].ClassName == className || pccObj.Exports[texIdx].ClassName == class2 || pccObj.Exports[texIdx].ClassName == class3))
            {
                Class = pccObj.Exports[texIdx].ClassName;
                ME3ExportEntry expEntry = pccObj.Exports[texIdx];
                properties = new Dictionary<string, SaltPropertyReader.Property>();
                byte[] rawData = (byte[])expEntry.Data.Clone();
                int propertiesOffset = SaltPropertyReader.detectStart(pccObj, rawData);
                headerData = new byte[propertiesOffset];
                Buffer.BlockCopy(rawData, 0, headerData, 0, propertiesOffset);
                pccOffset = (uint)expEntry.DataOffset;
                List<SaltPropertyReader.Property> tempProperties = SaltPropertyReader.getPropList(pccObj, rawData);
                texName = expEntry.ObjectName;
                for (int i = 0; i < tempProperties.Count; i++)
                {
                    SaltPropertyReader.Property property = tempProperties[i];
                    if (property.Name == "UnpackMin")
                        UnpackNum++;

                    if (!properties.ContainsKey(property.Name))
                        properties.Add(property.Name, property);

                    switch (property.Name)
                    {
                        case "Format":
                            texFormat = pccObj.Names[property.Value.IntValue].Substring(3);
                            break;
                        case "TextureFileCacheName": arcName = pccObj.Names[property.Value.IntValue]; break;
                        case "LODGroup": LODGroup = pccObj.Names[property.Value.IntValue]; break; //
                        case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                    }
                }
                if (!String.IsNullOrEmpty(arcName))
                    FullArcPath = GetTexArchive(pathBioGame);

                // if "None" property isn't found throws an exception
                if (dataOffset == 0)
                    throw new Exception("\"None\" property not found");
                else
                {
                    imageData = new byte[rawData.Length - dataOffset];
                    Buffer.BlockCopy(rawData, (int)dataOffset, imageData, 0, (int)(rawData.Length - dataOffset));
                }
            }
            else
                throw new Exception("Texture2D " + texIdx + " not found");

            pccExpIdx = texIdx;
            MemoryStream dataStream = new MemoryStream(imageData);  // FG: we will move forward with the memorystream (we are reading an export entry for a texture object data inside the pcc)
            numMipMaps = dataStream.ReadValueU32();                 // FG: 1st int32 (4 bytes / 32bits) is number of mipmaps
            uint count = numMipMaps;

            privateimgList = new List<ImageInfo>();
            ArcDataSize = 0;
            while (dataStream.Position < dataStream.Length && count > 0)
            {
                ImageInfo imgInfo = new ImageInfo();                            // FG: store properties in ImageInfo struct (code at top)
                imgInfo.storageType = (storage)dataStream.ReadValueS32();       // FG: 2nd int32 storage type (see storage types above in enum_struct)
                imgInfo.uncSize = dataStream.ReadValueS32();                    // FG: 3rd int32 uncompressed texture size
                imgInfo.cprSize = dataStream.ReadValueS32();                    // FG: 4th int32 compressed texture size
                imgInfo.offset = dataStream.ReadValueS32();                     // FG: 5th int32 texture offset
                if (imgInfo.storageType == storage.pccSto)
                {
                    //imgInfo.offset = (int)(pccOffset + dataOffset); // saving pcc offset as relative to exportdata offset, not absolute
                    imgInfo.offset = (int)dataStream.Position; // saving pcc offset as relative to exportdata offset, not absolute
                    //MessageBox.Show("Pcc class offset: " + pccOffset + "\nimages data offset: " + imgInfo.offset.ToString());
                    dataStream.Seek(imgInfo.uncSize, SeekOrigin.Current);       // FG: if local storage, texture data follows, so advance datastream to after uncompressed_size (pcc storage type only)
                }
                else if (imgInfo.storageType == storage.arcCpr || imgInfo.storageType == storage.arcUnc)
                {
                    ArcDataSize += imgInfo.uncSize;
                }
                imgInfo.imgSize = new ImageSize(dataStream.ReadValueU32(), dataStream.ReadValueU32());  // FG: 6th & 7th [or nth and (nth + 1) if local] int32 are width x height
                privateimgList.Add(imgInfo);                                                                   // FG: A salty's favorite, add the struct to a list<struct>
                count--;
            }


            // save what remains
            int remainingBytes = (int)(dataStream.Length - dataStream.Position);
            footerData = new byte[remainingBytes];
            dataStream.Read(footerData, 0, footerData.Length);


            dataStream.Dispose();
        }

        public byte[] ToArray(uint pccExportDataOffset, ME3PCCObject pcc)
        {
            using (MemoryStream tempStream = new MemoryStream())
            {
                tempStream.WriteBytes(headerData);

                // Whilst testing get rid of this
                if (properties.ContainsKey("LODGroup"))
                    properties["LODGroup"].Value.String2 = "TEXTUREGROUP_Shadowmap";
                else
                {
                    tempStream.WriteValueS64(pcc.addName2("LODGroup"));
                    tempStream.WriteValueS64(pcc.addName2("ByteProperty"));
                    tempStream.WriteValueS64(8);
                    tempStream.WriteValueS64(pcc.addName2("TextureGroup"));
                    tempStream.WriteValueS64(pcc.addName2("TEXTUREGROUP_Shadowmap"));
                }

                foreach (KeyValuePair<string, SaltPropertyReader.Property> kvp in properties)
                {
                    SaltPropertyReader.Property prop = kvp.Value;

                    if (prop.Name == "UnpackMin")
                    {
                        for (int i = 0; i < UnpackNum; i++)
                        {
                            tempStream.WriteValueS64(pcc.addName2(prop.Name));
                            tempStream.WriteValueS64(pcc.addName2(prop.TypeVal.ToString()));
                            tempStream.WriteValueS32(prop.Size);
                            tempStream.WriteValueS32(i);
                            tempStream.WriteValueF32(prop.Value.FloatValue);
                        }
                        continue;
                    }

                    tempStream.WriteValueS64(pcc.addName2(prop.Name));

                    if (prop.Name == "None")
                        continue;

                    tempStream.WriteValueS64(pcc.addName2(prop.TypeVal.ToString()));
                    tempStream.WriteValueS64(prop.Size);

                    switch (prop.TypeVal)
                    {
                        case SaltPropertyReader.Type.FloatProperty:
                            tempStream.WriteValueF32(prop.Value.FloatValue);
                            break;
                        case SaltPropertyReader.Type.IntProperty:
                            tempStream.WriteValueS32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.NameProperty:
                            tempStream.WriteValueS64(pcc.addName2(prop.Value.StringValue));
                            break;
                        case SaltPropertyReader.Type.ByteProperty:
                            tempStream.WriteValueS64(pcc.addName2(prop.Value.StringValue));
                            tempStream.WriteValueS64(pcc.addName2(prop.Value.String2));
                            //tempStream.WriteValueS32(pcc.addName2(prop.Value.String2));
                            //byte[] footer = new byte[4];
                            //Buffer.BlockCopy(prop.raw, prop.raw.Length - 4, footer, 0, 4);
                            //tempStream.WriteBytes(footer);
                            break;
                        case SaltPropertyReader.Type.BoolProperty:
                            tempStream.WriteValueBoolean(prop.Value.Boolereno);
                            break;
                        case SaltPropertyReader.Type.StructProperty:
                            tempStream.WriteValueS64(pcc.addName2(prop.Value.StringValue));
                            for (int i = 0; i < prop.Size; i++)
                                tempStream.WriteByte((byte)prop.Value.Array[i].IntValue);
                            break;
                        default:
                            throw new NotImplementedException("Property type: " + prop.TypeVal.ToString() + ", not yet implemented. TELL ME ABOUT THIS!");
                    }
                }

                //Remove empty textures
                List<ImageInfo> tempList = new List<ImageInfo>();
                foreach (ImageInfo imgInfo in privateimgList)
                {
                    if (imgInfo.storageType != storage.empty)
                        tempList.Add(imgInfo);
                }
                privateimgList = tempList;
                numMipMaps = (uint)privateimgList.Count;

                tempStream.WriteValueU32(numMipMaps);
                foreach (ImageInfo imgInfo in privateimgList)
                {
                    tempStream.WriteValueS32((int)imgInfo.storageType);
                    tempStream.WriteValueS32(imgInfo.uncSize);
                    tempStream.WriteValueS32(imgInfo.cprSize);
                    if (imgInfo.storageType == storage.pccSto)
                    {
                        tempStream.WriteValueS32((int)(imgInfo.offset + pccExportDataOffset + dataOffset));
                        tempStream.Write(imageData, (int)imgInfo.offset, imgInfo.uncSize);
                    }
                    else
                        tempStream.WriteValueS32(imgInfo.offset);
                    tempStream.WriteValueU32(imgInfo.imgSize.width);
                    tempStream.WriteValueU32(imgInfo.imgSize.height);
                }
                //// Texture2D footer, 24 bytes size - changed to 20
                //tempStream.Write(imageData, imageData.Length - 20, 20);
                tempStream.WriteBytes(footerData);
                return tempStream.ToArray();
            }

            #region Unused Code
            /*
            bool lodExists = false;
            foreach (KeyValuePair<string, PropertyReader.Property> kvp in properties)
            {
                PropertyReader.Property property = kvp.Value;
                if (kvp.Key == "LODGroup")
                {
                    lodExists = true;
                    break;
                }
            }

            MemoryStream buffer = new MemoryStream();
            buffer.Write(headerData, 0, headerData.Length);

            if (lodExists)
            {
                // extracting values from LODGroup Property
                PropertyReader.Property LODGroup = properties["LODGroup"];
                string textureGroupName = pcc.Names[LODGroup.Value.IntValue];
                bool nameExists = false;
                string newTextureGroupName = "TEXTUREGROUP_Shadowmap";
                if (String.Compare(newTextureGroupName, textureGroupName) != 0)
                {
                    textureGroupName = newTextureGroupName;
                    if (!pcc.Names.Exists(name => name == newTextureGroupName))
                        pcc.Names.Add(newTextureGroupName);
                    using (MemoryStream rawStream = new MemoryStream(LODGroup.raw))
                    {
                        rawStream.Seek(32, SeekOrigin.Begin);
                        rawStream.WriteValueS32(pcc.Names.FindIndex(name => name == newTextureGroupName));
                        rawStream.WriteValueS32(0);
                        properties["LODGroup"].raw = rawStream.ToArray();
                    }
                }
                else
                    nameExists = true;
                //MemoryStream buffer = new MemoryStream();
                //buffer.Write(headerData, 0, headerData.Length);
                foreach (KeyValuePair<string, PropertyReader.Property> kvp in properties)
                {
                    PropertyReader.Property property = kvp.Value;

                    if (kvp.Key == "LODBias")
                        continue;
                    if (kvp.Key == "InternalFormatLODBias")
                        continue;
                    if (kvp.Key == "LODGroup" && nameExists == false)
                    {
                        int name;
                        if (!nameExists)
                            name = pcc.Names.Count - 1;                 //Warranty Voiders Name redirect hack^^
                        else
                            name = LODGroup.Value.IntValue;
                        ME3_HR_Patch.Helper.BitConverter.IsLittleEndian = true;
                        byte[] buff = ME3_HR_Patch.Helper.BitConverter.GetBytes(name);
                        for (int i = 0; i < 4; i++)
                            property.raw[i + 24] = buff[i];
                    }
                    buffer.Write(property.raw, 0, property.raw.Length);
                    if (kvp.Key == "UnpackMin")
                    {
                        buffer.Write(property.raw, 0, property.raw.Length);
                        buffer.Write(property.raw, 0, property.raw.Length);
                    }
                }
            }
            else
            {
                //MemoryStream buffer = new MemoryStream();
                //buffer.Write(headerData, 0, headerData.Length);
                int lodID = pcc.findName("LODGroup");
                if (lodID == -1)
                {
                    pcc.addName("LODGroup");
                    lodID = pcc.Names.Count - 1;
                }
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes(lodID));
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes((int)0));
                lodID = pcc.findName("ByteProperty");
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes(lodID));
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes((int)0));
                //Write an int
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes((int)8));
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes((int)0));

                lodID = pcc.findName("TextureGroup");
                if (lodID == -1)
                {
                    pcc.addName("TextureGroup");
                    lodID = pcc.Names.Count - 1;
                }
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes(lodID));
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes((int)0));

                lodID = pcc.findName("TEXTUREGROUP_Shadowmap");
                if (lodID == -1)
                {
                    pcc.addName("TEXTUREGROUP_Shadowmap");
                    lodID = pcc.Names.Count - 1;
                }
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes(lodID));
                buffer.WriteBytes(ME3_HR_Patch.Helper.BitConverter.GetBytes((int)0));

                foreach (KeyValuePair<string, PropertyReader.Property> kvp in properties)
                {
                    PropertyReader.Property property = kvp.Value;

                    if (kvp.Key == "LODBias")
                        continue;
                    if (kvp.Key == "InternalFormatLODBias")
                        continue;
                    if (kvp.Key == "LODGroup")
                    {
                        int name = pcc.Names.Count - 1;                 //Warranty Voiders Name redirect hack^^
                        ME3_HR_Patch.Helper.BitConverter.IsLittleEndian = true;
                        byte[] buff = ME3_HR_Patch.Helper.BitConverter.GetBytes(name);
                        for (int i = 0; i < 4; i++)
                            property.raw[i + 24] = buff[i];
                    }
                    buffer.Write(property.raw, 0, property.raw.Length);
                    if (kvp.Key == "UnpackMin")
                    {
                        buffer.Write(property.raw, 0, property.raw.Length);
                        buffer.Write(property.raw, 0, property.raw.Length);
                    }
                }
            }

            buffer.WriteValueU32(numMipMaps);
            foreach (ImageInfo imgInfo in imgList)
            {
                buffer.WriteValueS32((int)imgInfo.storageType);
                buffer.WriteValueS32(imgInfo.uncSize);
                buffer.WriteValueS32(imgInfo.cprSize);
                if (imgInfo.storageType == storage.pccSto)
                {
                    buffer.WriteValueS32((int)(imgInfo.offset + pccExportDataOffset + dataOffset));
                    buffer.Write(imageData, imgInfo.offset, imgInfo.uncSize);
                }
                else
                    buffer.WriteValueS32(imgInfo.offset);
                buffer.WriteValueU32(imgInfo.imgSize.width);
                buffer.WriteValueU32(imgInfo.imgSize.height);
            }
            // Texture2D footer, 24 bytes size
            buffer.Write(imageData, imageData.Length - 24, 24);
            byte[] rawData = buffer.ToArray();
            return rawData;
            */
            #endregion
        }

        public byte[] extractImage(string strImgSize, bool NoOutput, string archiveDir = null, string fileName = null)
        {
            ImageSize imgSize = ImageSize.stringToSize(strImgSize);
            byte[] retval;
            if (privateimgList.Exists(img => img.imgSize == imgSize))
                retval = extractImage(privateimgList.Find(img => img.imgSize == imgSize), NoOutput, archiveDir, fileName);
            else
                throw new FileNotFoundException("Image with resolution " + imgSize + " not found");
            return retval;
        }

        public byte[] extractImage(ImageInfo imgInfo, bool NoOutput, string archiveDir = null, string fileName = null)
        {
            ImageFile imgFile;
            if (fileName == null)
            {
                fileName = texName + "_" + imgInfo.imgSize + getFileFormat();
            }

            byte[] imgBuffer;

            switch (imgInfo.storageType)
            {
                case storage.pccSto:
                    imgBuffer = new byte[imgInfo.uncSize];
                    Buffer.BlockCopy(imageData, (int)imgInfo.offset, imgBuffer, 0, imgInfo.uncSize);
                    break;
                case storage.arcCpr:
                case storage.arcUnc:
                    //string archivePath = GetTexArchive(archiveDir);
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        GetTexArchive(archiveDir);
                    if (archivePath == null)
                        throw new FileNotFoundException("Texture archive not found!");
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    using (FileStream archiveStream = File.OpenRead(archivePath))
                    {
                        archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                        if (imgInfo.storageType == storage.arcCpr)
                            imgBuffer = ZBlock.Decompress(archiveStream, imgInfo.cprSize);
                        else
                        {
                            imgBuffer = new byte[imgInfo.uncSize];
                            archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
                        }
                    }
                    break;
                default:
                    throw new FormatException("Unsupported texture storage type");
            }

            //if (getFileFormat() == ".dds")
            imgFile = new DDS(fileName, imgInfo.imgSize, texFormat, imgBuffer);
            //else
            //    imgFile = new TGA(fileName, imgInfo.imgSize, texFormat, imgBuffer);

            byte[] saveImg = imgFile.ToArray();

            if (!NoOutput)
                using (FileStream outputImg = new FileStream(imgFile.fileName, FileMode.Create, FileAccess.Write))
                    outputImg.Write(saveImg, 0, saveImg.Length);

            return saveImg;
        }

        public byte[] extractMaxImage(bool NoOutput, string archiveDir = null, string fileName = null)
        {
            // select max image size, excluding void images with offset = -1
            ImageSize maxImgSize = privateimgList.Where(img => img.offset != -1).Max(image => image.imgSize);
            // extracting max image
            return extractImage(privateimgList.Find(img => img.imgSize == maxImgSize), NoOutput, archiveDir, fileName);
        }

        public void removeImage()
        {
            privateimgList.RemoveAt(0);

            numMipMaps--;
            int propVal = properties["MipTailBaseIdx"].Value.IntValue;
            propVal--;
            properties["MipTailBaseIdx"].Value.IntValue = propVal;

            using (MemoryStream rawStream = new MemoryStream(properties["MipTailBaseIdx"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["MipTailBaseIdx"].raw = rawStream.ToArray();
            }
            propVal = (int)privateimgList[0].imgSize.width;
            properties["SizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeX"].raw = rawStream.ToArray();
            }
            properties["SizeY"].Value.IntValue = (int)privateimgList[0].imgSize.height;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeY"].raw = rawStream.ToArray();
            }
            properties["OriginalSizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["OriginalSizeX"].raw = rawStream.ToArray();
            }
            properties["OriginalSizeY"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["OriginalSizeY"].raw = rawStream.ToArray();
            }
        }

        public void upscaleImage(ME3SaltTexture2D inTex)
        {
            numMipMaps++;
            int propVal = properties["MipTailBaseIdx"].Value.IntValue;
            propVal++;
            properties["MipTailBaseIdx"].Value.IntValue = propVal;

            using (MemoryStream rawStream = new MemoryStream(properties["MipTailBaseIdx"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["MipTailBaseIdx"].raw = rawStream.ToArray();
            }

            // update Sizes
            propVal = (int)privateimgList[0].imgSize.width;
            properties["SizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeX"].raw = rawStream.ToArray();
            }

            properties["SizeY"].Value.IntValue = (int)privateimgList[0].imgSize.height;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeY"].raw = rawStream.ToArray();
            }

            properties["OriginalSizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["OriginalSizeX"].raw = rawStream.ToArray();
            }
            properties["OriginalSizeY"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["OriginalSizeY"].raw = rawStream.ToArray();
            }
        }

        public void singleImageUpscale(ImageFile im, string archiveDir)
        {
            ImageSize biggerImageSizeOnList = privateimgList.Max(image => image.imgSize);
            // check if replacing image is supported
            ImageFile imgFile = im;

            //NEW Check for correct image format
            if (texFormat != imgFile.format)
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = privateimgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
            newImgInfo.imgSize = imgFile.imgSize;
            newImgInfo.uncSize = imgFile.resize().Length;
            newImgInfo.cprSize = 0x00; // not yet filled
            newImgInfo.offset = 0x00; // not yet filled
            //imgList.Insert(0, newImgInfo); // insert new image on top of the list
            privateimgList.RemoveAt(0);  // Remove old single image and add new one
            privateimgList.Add(newImgInfo);
            //now I let believe the program that I'm doing an image replace, saving lot of code ;)
            replaceImage2(newImgInfo.imgSize.ToString(), im, archiveDir);

            // update Sizes
            //PropertyReader.Property Size = properties["SizeX"];
            int propVal = (int)newImgInfo.imgSize.width;
            properties["SizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeX"].raw = rawStream.ToArray();
            }
            //properties["SizeX"] = Size;
            //Size = properties["SizeY"];
            properties["SizeY"].Value.IntValue = (int)newImgInfo.imgSize.height;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeY"].raw = rawStream.ToArray();
            }
            //properties["SizeY"] = Size;
            properties["OriginalSizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["OriginalSizeX"].raw = rawStream.ToArray();
            }
            properties["OriginalSizeY"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["OriginalSizeY"].raw = rawStream.ToArray();
            }
            //this.hasChanged = true;
        }

        public void replaceImage2(string strImgSize, ImageFile im, string archiveDir)
        {
            ImageSize imgSize = ImageSize.stringToSize(strImgSize);
            if (!privateimgList.Exists(img => img.imgSize == imgSize))
                throw new FileNotFoundException("Image with resolution " + imgSize + " isn't found");

            int imageIdx = privateimgList.FindIndex(img => img.imgSize == imgSize);
            ImageInfo imgInfo = privateimgList[imageIdx];

            // check if replacing image is supported
            ImageFile imgFile = im;

            // check if images have same format type
            if (texFormat != imgFile.format && texFormat != "G8")
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());

            byte[] imgBuffer;

            // if the image is empty then recover the archive compression from the image list
            if (imgInfo.storageType == storage.empty)
            {
                imgInfo.storageType = privateimgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
                imgInfo.uncSize = imgFile.resize().Length;
                imgInfo.cprSize = imgFile.resize().Length;
            }

            switch (imgInfo.storageType)
            {
                case storage.arcCpr:
                case storage.arcUnc:
                    //string archivePath = GetTexArchive(archiveDir);
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive(archiveDir);
                    if (archivePath == null)
                        throw new FileNotFoundException("Teture archive not found!");
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    if (getFileFormat() == ".tga")
                        imgBuffer = imgFile.resize(); // shrink image to essential data
                    else
                        imgBuffer = imgFile.imgData;

                    if (imgBuffer.Length != imgInfo.uncSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.uncSize + ", new is " + imgBuffer.Length);

                    if (arcName.Length <= CustCache.Length || arcName.Substring(0, CustCache.Length) != CustCache) // Check whether existing texture is in a custom cache
                    {
                        ChooseNewCache(archiveDir, imgBuffer.Length);
                        archivePath = FullArcPath;
                    }
                    else
                    {
                        FileInfo arc = new FileInfo(archivePath);
                        if (arc.Length + imgBuffer.Length >= 0x80000000)
                        {
                            ChooseNewCache(archiveDir, imgBuffer.Length);
                            archivePath = FullArcPath;
                        }
                    }
                    /* FileInfo arc = new FileInfo(archivePath);
                    if (arc.Length + imgBuffer.Length >= 0x80000000)
                    {
                        //2GB fix
                        ChooseNewCache(archiveDir, imgBuffer.Length);
                        archivePath = archiveDir + "\\" + arcName + ".tfc";
                    } */

                    using (FileStream archiveStream = new FileStream(archivePath, FileMode.Append, FileAccess.Write))
                    {
                        int newOffset = (int)archiveStream.Position;

                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            imgBuffer = ZBlock.Compress(imgBuffer);
                            imgInfo.cprSize = imgBuffer.Length;
                        }
                        archiveStream.Write(imgBuffer, 0, imgBuffer.Length);

                        imgInfo.offset = newOffset;
                    }
                    break;
                case storage.pccSto:
                    imgBuffer = imgFile.imgData; // copy image data as-is
                    /*if (imgBuffer.Length != imgInfo.uncSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.uncSize + ", new is " + imgBuffer.Length);*/
                    using (MemoryStream dataStream = new MemoryStream())
                    {
                        dataStream.WriteBytes(imageData);
                        if (imgBuffer.Length <= imgInfo.uncSize && imgInfo.offset > 0)
                            dataStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                        else
                            imgInfo.offset = (int)dataStream.Position;
                        dataStream.WriteBytes(imgBuffer);
                        imgInfo.cprSize = imgBuffer.Length;
                        imgInfo.uncSize = imgBuffer.Length;
                        imageData = dataStream.ToArray();
                    }
                    #region Old code
                    /*
                    try
                    {
                        using (MemoryStream dataStream = new MemoryStream(imageData))
                        {
                            dataStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                            dataStream.Write(imgBuffer, 0, imgBuffer.Length);
                        }
                    }
                    catch (NotSupportedException)
                    {
                        MemoryStream dataStream = new MemoryStream();
                        dataStream.WriteBytes(imgBuffer);
                        dataStream.WriteBytes(imageData);
                        imageData = dataStream.ToArray();
                        dataStream.Close();
                        for (int i = 1; i < imgList.Count; i++)
                        {
                            ImageInfo img = imgList[i];
                            img.offset += imgBuffer.Length;
                            imgList[i] = img;
                        }
                    }
                    */
                    #endregion
                    break;
            }

            privateimgList[imageIdx] = imgInfo;
            //this.hasChanged = true;
        }

        public void replaceImage(string strImgSize, ImageFile im, string archiveDir)
        {
            //DebugOutput.PrintLn( "\nIn replace image...\n");
            ImageSize imgSize = ImageSize.stringToSize(strImgSize);
            if (!privateimgList.Exists(img => img.imgSize == imgSize))
                throw new FileNotFoundException("Image with resolution " + imgSize + " isn't found");

            int imageIdx = privateimgList.FindIndex(img => img.imgSize == imgSize);
            ImageInfo imgInfo = privateimgList[imageIdx];

            ImageFile imgFile = im;


            // check if images have same format type
            if (texFormat != imgFile.format && texFormat != "G8")
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());

            byte[] imgBuffer;

            // if the image is empty then recover the archive compression from the image list
            if (imgInfo.storageType == storage.empty)
            {
                imgInfo.storageType = privateimgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
                imgInfo.uncSize = imgFile.resize().Length;
                imgInfo.cprSize = imgFile.resize().Length;
            }

            switch (imgInfo.storageType)
            {
                case storage.arcCpr:
                case storage.arcUnc:
                    //string archivePath = GetTexArchive(archiveDir);
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive(archiveDir);
                    if (archivePath == null)
                        throw new FileNotFoundException("Teture archive not found!");
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    if (getFileFormat() == ".tga")
                        imgBuffer = imgFile.resize(); // shrink image to essential data
                    else
                        imgBuffer = imgFile.imgData;

                    if (imgBuffer.Length != imgInfo.uncSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.uncSize + ", new is " + imgBuffer.Length);

                    //DebugOutput.PrintLn( "\nOK here's the stuff we came for. About to change/add cache.\n");
                    //DebugOutput.PrintLn( "Initial arcname = " + arcName + "   custCache = " + CustCache + "\n");
                    if (arcName.Length <= CustCache.Length || arcName.Substring(0, CustCache.Length) != CustCache) // Check whether existing texture is in a custom cache
                    {
                        ChooseNewCache(archiveDir, imgBuffer.Length);
                        archivePath = FullArcPath;
                    }
                    else
                    {
                        FileInfo arc = new FileInfo(archivePath);
                        //DebugOutput.PrintLn( "Extra bits maybe:  " + arc.Length + "\n");
                        if (arc.Length + imgBuffer.Length >= 0x80000000)
                        {
                            ChooseNewCache(archiveDir, imgBuffer.Length);
                            archivePath = FullArcPath;
                        }
                    }
                    /* FileInfo arc = new FileInfo(archivePath);
                    if (arc.Length + imgBuffer.Length >= 0x80000000)
                    {
                        //2GB fix
                        ChooseNewCache(archiveDir, imgBuffer.Length);
                        archivePath = archiveDir + "\\" + arcName + ".tfc";
                    } */

                    using (FileStream archiveStream = new FileStream(archivePath, FileMode.Append, FileAccess.Write))
                    {
                        int newOffset = (int)archiveStream.Position;

                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            imgBuffer = ZBlock.Compress(imgBuffer);
                            imgInfo.cprSize = imgBuffer.Length;
                        }
                        archiveStream.Write(imgBuffer, 0, imgBuffer.Length);

                        imgInfo.offset = newOffset;
                    }
                    break;
                case storage.pccSto:
                    imgBuffer = imgFile.imgData; // copy image data as-is
                    if (imgBuffer.Length != imgInfo.uncSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.uncSize + ", new is " + imgBuffer.Length);
                    using (MemoryStream dataStream = new MemoryStream())
                    {
                        dataStream.WriteBytes(imageData);
                        if (imgBuffer.Length <= imgInfo.uncSize && imgInfo.offset > 0)
                            dataStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                        else
                            imgInfo.offset = (int)dataStream.Position;
                        dataStream.WriteBytes(imgBuffer);
                        imgInfo.cprSize = imgBuffer.Length;
                        imgInfo.uncSize = imgBuffer.Length;
                        imageData = dataStream.ToArray();
                    }
                    #region Old code
                    /*
                    try
                    {
                        using (MemoryStream dataStream = new MemoryStream(imageData))
                        {
                            dataStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                            dataStream.Write(imgBuffer, 0, imgBuffer.Length);
                        }
                    }
                    catch (NotSupportedException)
                    {
                        MemoryStream dataStream = new MemoryStream();
                        dataStream.WriteBytes(imgBuffer);
                        dataStream.WriteBytes(imageData);
                        imageData = dataStream.ToArray();
                        dataStream.Close();
                        for (int i = 1; i < imgList.Count; i++)
                        {
                            ImageInfo img = imgList[i];
                            img.offset += imgBuffer.Length;
                            imgList[i] = img;
                        }
                    }
                    */
                    #endregion
                    break;
            }

            privateimgList[imageIdx] = imgInfo;
            //this.hasChanged = true;
        }

        public void addBiggerImage(ImageFile im, string archiveDir)
        {
            //DebugOutput.PrintLn( "\nIn Addbiggerimage...\n");
            ImageSize biggerImageSizeOnList = privateimgList.Max(image => image.imgSize);
            // check if replacing image is supported
            ImageFile imgFile = im;

            //NEW Check for correct image format
            if (texFormat != imgFile.format || texFormat.Contains("ATI") && imgFile.format.Contains("NormalMap") || texFormat.Contains("NormalMap") && imgFile.format.Contains("ATI"))
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());

            // check if image to add is valid
            if (biggerImageSizeOnList.width * 2 != imgFile.imgSize.width || biggerImageSizeOnList.height * 2 != imgFile.imgSize.height)
                throw new FormatException("image size " + imgFile.imgSize + " isn't valid, must be " + new ImageSize(biggerImageSizeOnList.width * 2, biggerImageSizeOnList.height * 2));

            // this check avoids insertion inside textures that have only 1 image stored inside pcc
            if (privateimgList.Count <= 1)
                throw new Exception("Unable to add image, texture must have more than one existing image");

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = privateimgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
            newImgInfo.imgSize = imgFile.imgSize;
            newImgInfo.uncSize = imgFile.resize().Length;
            newImgInfo.cprSize = 0x00; // not yet filled
            newImgInfo.offset = 0x00; // not yet filled
            privateimgList.Insert(0, newImgInfo); // insert new image on top of the list
            //now I let believe the program that I'm doing an image replace, saving lot of code ;)
            replaceImage(newImgInfo.imgSize.ToString(), im, archiveDir);

            //updating num of images
            numMipMaps++;

            // update MipTailBaseIdx
            //PropertyReader.Property MipTail = properties["MipTailBaseIdx"];
            int propVal = properties["MipTailBaseIdx"].Value.IntValue;
            propVal++;
            properties["MipTailBaseIdx"].Value.IntValue = propVal;
            //MessageBox.Show("raw size: " + properties["MipTailBaseIdx"].raw.Length + "\nproperty offset: " + properties["MipTailBaseIdx"].offsetval);
            using (MemoryStream rawStream = new MemoryStream(properties["MipTailBaseIdx"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["MipTailBaseIdx"].raw = rawStream.ToArray();
            }
            //properties["MipTailBaseIdx"] = MipTail;

            // update Sizes
            //PropertyReader.Property Size = properties["SizeX"];
            propVal = (int)newImgInfo.imgSize.width;
            properties["SizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeX"].raw = rawStream.ToArray();
            }
            //properties["SizeX"] = Size;
            //Size = properties["SizeY"];
            properties["SizeY"].Value.IntValue = (int)newImgInfo.imgSize.height;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeY"].raw = rawStream.ToArray();
            }
            //properties["SizeY"] = Size;
            try
            {
                properties["OriginalSizeX"].Value.IntValue = propVal;
                using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeX"].raw))
                {
                    rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                    rawStream.WriteValueS32(propVal);
                    properties["OriginalSizeX"].raw = rawStream.ToArray();
                }
                properties["OriginalSizeY"].Value.IntValue = propVal;
                using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeY"].raw))
                {
                    rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                    rawStream.WriteValueS32(propVal);
                    properties["OriginalSizeY"].raw = rawStream.ToArray();
                }
            }
            catch
            {
                // Some lightmaps don't have these properties. I'm ignoring them cos I'm ignorant. KFreon.
            }

            //this.hasChanged = true;
        }

        public void OneImageToRuleThemAll(ImageFile im, string archiveDir, byte[] imgData)
        {
            //throw new Exception();
            ImageMipMapHandler imgMipMap = new ImageMipMapHandler("", imgData);

            // starts from the smaller image
            for (int i = imgMipMap.imageList.Count - 1; i >= 0; i--)
            {
                ImageFile newImageFile = imgMipMap.imageList[i];

                if (newImageFile.imgSize.height < 4 || newImageFile.imgSize.width < 4)
                    continue;

                //NEW Check for correct format
                if (texFormat != newImageFile.format)
                {
                    //MessageBox.Show("Warning! The input image is of the wrong format! Aborting");
                    throw new FormatException("Different image format, original is " + texFormat + ", new is " + newImageFile.subtype());
                }

                // if the image size exists inside the texture2d image list then we have to replace it
                if (privateimgList.Exists(img => img.imgSize == newImageFile.imgSize))
                {
                    // ...but at least for now I can reuse my replaceImage function... ;)
                    replaceImage(newImageFile.imgSize.ToString(), newImageFile, archiveDir);
                }
                else // if the image doesn't exists then we have to add it
                {
                    // ...and use my addBiggerImage function! :P
                    addBiggerImage(newImageFile, archiveDir);
                }
            }

            while (privateimgList[0].imgSize.width > imgMipMap.imageList[0].imgSize.width)
            {
                privateimgList.RemoveAt(0);
                numMipMaps--;
                if (properties.ContainsKey("MipTailBaseIdx"))
                    properties["MipTailBaseIdx"].Value.IntValue--;
            }
            if (properties.ContainsKey("SizeX"))
                properties["SizeX"].Value.IntValue = (int)imgMipMap.imageList[0].imgSize.width;
            if (properties.ContainsKey("SizeY"))
                properties["SizeY"].Value.IntValue = (int)imgMipMap.imageList[0].imgSize.height;

        }

        public List<SaltPropertyReader.Property> getPropertyList()
        {
            List<SaltPropertyReader.Property> propertyList = new List<SaltPropertyReader.Property>();
            foreach (KeyValuePair<string, SaltPropertyReader.Property> kvp in properties)
                propertyList.Add(kvp.Value);
            return propertyList;
        }

        public string getFileFormat()
        {
            return ".dds";
            /* switch (texFormat)
            {
                case "DXT1":
                case "DXT5":
                case "V8U8": return ".dds";
                case "G8":
                case "A8R8G8B8": return ".tga";
                default: throw new FormatException("Unknown ME3 texture format");
            } */
        }

        public void CopyImgList(ME3SaltTexture2D inTex, ME3PCCObject pcc)
        {
            imageData = inTex.imageData;
            privateimgList = inTex.privateimgList;
            numMipMaps = inTex.numMipMaps;

            //Copy Properties
            byte[] buff;
            using (MemoryStream tempMem = new MemoryStream())
            {
                tempMem.WriteBytes(headerData);
                for (int i = 0; i < inTex.properties.Count; i++)
                {
                    SaltPropertyReader.Property prop = inTex.properties.ElementAt(i).Value;

                    if (prop.Name == "UnpackMin")
                    {
                        for (int j = 0; j < inTex.UnpackNum; j++)
                        {
                            tempMem.WriteValueS64(pcc.addName2(prop.Name));
                            tempMem.WriteValueS64(pcc.addName2(prop.TypeVal.ToString()));
                            tempMem.WriteValueS32(prop.Size);
                            tempMem.WriteValueS32(j);
                            tempMem.WriteValueF32(prop.Value.FloatValue);
                        }
                        continue;
                    }

                    tempMem.WriteValueS64(pcc.addName2(prop.Name));

                    if (prop.Name == "None")
                        continue;


                    tempMem.WriteValueS64(pcc.addName2(prop.TypeVal.ToString()));
                    tempMem.WriteValueS64(prop.Size);

                    switch (prop.TypeVal)
                    {
                        case SaltPropertyReader.Type.FloatProperty:
                            tempMem.WriteValueF32(prop.Value.FloatValue);
                            break;
                        case SaltPropertyReader.Type.IntProperty:
                            tempMem.WriteValueS32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.NameProperty:
                            tempMem.WriteValueS64(pcc.addName2(prop.Value.StringValue));
                            break;
                        case SaltPropertyReader.Type.ByteProperty:
                            tempMem.WriteValueS64(pcc.addName2(prop.Value.StringValue));
                            tempMem.WriteValueS32(pcc.addName2(prop.Value.String2));
                            byte[] footer = new byte[4];
                            Buffer.BlockCopy(prop.raw, prop.raw.Length - 4, footer, 0, 4);
                            tempMem.WriteBytes(footer);
                            break;
                        case SaltPropertyReader.Type.BoolProperty:
                            tempMem.WriteValueBoolean(prop.Value.Boolereno);
                            break;
                        case SaltPropertyReader.Type.StructProperty:
                            tempMem.WriteValueS64(pcc.addName2(prop.Value.StringValue));
                            for (int k = 0; k < prop.Size; k++)
                                tempMem.WriteByte((byte)prop.Value.Array[k].IntValue);
                            break;
                        default:
                            throw new NotImplementedException("Property type: " + prop.TypeVal.ToString() + ", not yet implemented. TELL ME ABOUT THIS!");
                    }
                }
                buff = tempMem.ToArray();
            }

            properties = new Dictionary<string, SaltPropertyReader.Property>();

            List<SaltPropertyReader.Property> tempProperties = SaltPropertyReader.ReadProp(pcc, buff, headerData.Length);
            for (int i = 0; i < tempProperties.Count; i++)
            {
                SaltPropertyReader.Property property = tempProperties[i];
                if (property.Name == "UnpackMin")
                    UnpackNum++;

                if (!properties.ContainsKey(property.Name))
                    properties.Add(property.Name, property);

                switch (property.Name)
                {
                    case "Format":
                        texFormat = pcc.Names[property.Value.IntValue].Substring(3);
                        break;
                    case "TextureFileCacheName": arcName = pcc.Names[property.Value.IntValue]; break;
                    case "LODGroup": LODGroup = pcc.Names[property.Value.IntValue]; break; //
                    case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                }
            }

            // if "None" property isn't found throws an exception
            if (dataOffset == 0)
                throw new Exception("\"None\" property not found");
        }

        private void ChooseNewCache(string bioPath, int buffLength)
        {
            //DebugOutput.PrintLn( "\nIn ChooseNewCache\n");
            int i = 0;
            while (true)
            {
                FileInfo cacheInfo;
                List<string> parts = new List<string>(bioPath.Split('\\'));
                parts.Remove("");
                if (parts[parts.Count - 1] == "CookedPCConsole")
                    cacheInfo = new FileInfo(Path.Combine(bioPath, CustCache + i + ".tfc"));
                else
                    cacheInfo = new FileInfo(Path.Combine(bioPath, "CookedPCConsole", CustCache + i + ".tfc"));

                //DebugOutput.PrintLn( "Cacheinfo:  " + cacheInfo.Name + "   " + cacheInfo.DirectoryName + "   " + cacheInfo.FullName + "   " + cacheInfo.Exists + "   \n");

                if (!cacheInfo.Exists)
                {
                    //DebugOutput.PrintLn( "Cache info doesn't exist. Make new cache.\n");
                    MakeCache(cacheInfo.FullName, bioPath);
                    List<string> parts1 = new List<string>(bioPath.Split('\\'));
                    parts1.Remove("");
                    if (parts1[parts1.Count - 1] == "CookedPCConsole")
                        MoveCaches(bioPath, CustCache + i + ".tfc");
                    else
                        MoveCaches(bioPath + "\\CookedPCConsole", CustCache + i + ".tfc");

                    properties["TextureFileCacheName"].Value.StringValue = CustCache + i;
                    arcName = CustCache + i;
                    FullArcPath = cacheInfo.FullName;
                    return;
                }
                else if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)
                {
                    List<string> parts1 = new List<string>(bioPath.Split('\\'));
                    parts1.Remove("");
                    if (parts1[parts1.Count - 1] == "CookedPCConsole")
                        MoveCaches(bioPath, CustCache + i + ".tfc");
                    else
                        MoveCaches(bioPath + "\\CookedPCConsole", CustCache + i + ".tfc");
                    properties["TextureFileCacheName"].Value.StringValue = CustCache + i;
                    arcName = CustCache + i;
                    FullArcPath = cacheInfo.FullName;
                    return;
                }
                i++;
            }

            #region Old code
            /*
            cacheInfo = new FileInfo(Path.Combine(bioPath, "CharTextures.tfc"));
            if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)
            {
                MoveCaches(bioPath, "CharTextures.tfc");
                properties["TextureFileCacheName"].Value.StringValue = "CharTextures";
                arcName = "CharTextures";
                return;
            }

            cacheInfo = new FileInfo(Path.Combine(bioPath, "Textures.tfc"));
            if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)
            {
                MoveCaches(bioPath, "Textures.tfc");
                properties["TextureFileCacheName"].Value.StringValue = "Textures";
                arcName = "Textures";
                return;
            }

            cacheInfo = new FileInfo(Path.Combine(bioPath, "Lighting.tfc"));
            if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)
            {
                MoveCaches(bioPath, "Lighting.tfc");
                properties["TextureFileCacheName"].Value.StringValue = "Lighting";
                arcName = "Lighting";
                return;
            }

            cacheInfo = new FileInfo(Path.Combine(bioPath, "Movies.tfc"));
            if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)
            {
                MoveCaches(bioPath, "Movies.tfc");
                properties["TextureFileCacheName"].Value.StringValue = "Movies";
                arcName = "Movies";
                return;
            }

            throw new NotImplementedException("All texture caches detected as up to 2GB. Adding new caches is not yet supported");
            */
            #endregion
        }

        private void MoveCaches(string cookedPath, string NewCache)
        {
            //DebugOutput.PrintLn( "\nMoving cache...\n");
            //Fix the GUID
            using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Open, FileAccess.Read))
            {
                SaltPropertyReader.Property GUIDProp = properties["TFCFileGuid"];

                for (int i = 0; i < 16; i++)
                {
                    SaltPropertyReader.PropertyValue tempVal = GUIDProp.Value.Array[i];
                    tempVal.IntValue = newCache.ReadByte();
                    GUIDProp.Value.Array[i] = tempVal;
                }
            }


            //Move across any existing textures
            //using (FileStream oldCache = new FileStream(Path.Combine(cookedPath, arcName + ".tfc"), FileMode.Open, FileAccess.Read))
            using (FileStream oldCache = new FileStream(FullArcPath, FileMode.Open, FileAccess.Read))
            {
                using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Append, FileAccess.Write))
                {
                    for (int i = 0; i < privateimgList.Count; i++)
                    {
                        ImageInfo img = privateimgList[i];

                        switch (img.storageType)
                        {
                            case storage.arcCpr:
                                byte[] buff = new byte[img.cprSize];
                                oldCache.Seek(img.offset, SeekOrigin.Begin);
                                Buffer.BlockCopy(oldCache.ReadBytes(img.cprSize), 0, buff, 0, img.cprSize);
                                img.offset = (int)newCache.Position;
                                newCache.WriteBytes(buff);
                                break;
                            case storage.arcUnc:
                                buff = new byte[img.uncSize];
                                oldCache.Seek(img.offset, SeekOrigin.Begin);
                                Buffer.BlockCopy(oldCache.ReadBytes(img.cprSize), 0, buff, 0, img.cprSize);
                                img.offset = (int)newCache.Position;
                                newCache.WriteBytes(buff);
                                break;
                            case storage.pccSto:
                                break;
                            case storage.empty:
                                break;
                            default:
                                throw new NotImplementedException("Storage type not supported yet");
                        }
                        privateimgList[i] = img;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the raw image data, mainly for use with AK's image displayer
        /// </summary>
        /// <param name="strImgSize"></param>
        /// <param name="archiveDir"></param>
        /// <returns></returns>
        public byte[] DumpImg(ImageSize imgSize, string archiveDir)
        {
            byte[] imgBuff;

            ImageInfo imgInfo;
            if (privateimgList.Exists(img => img.imgSize == imgSize))
                imgInfo = privateimgList.Find(img => img.imgSize == imgSize);
            else
                throw new FileNotFoundException("Image with resolution " + imgSize + " not found");

            switch (imgInfo.storageType)
            {
                case storage.pccSto:
                    imgBuff = new byte[imgInfo.uncSize];
                    Buffer.BlockCopy(imageData, (int)imgInfo.offset, imgBuff, 0, imgInfo.uncSize);
                    break;
                case storage.arcCpr:
                case storage.arcUnc:
                    //string archivePath = archiveDir + "\\" + arcName + ".tfc";
                    string archivePath = GetTexArchive(archiveDir);

                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    using (FileStream archiveStream = File.OpenRead(archivePath))
                    {
                        archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            imgBuff = ZBlock.Decompress(archiveStream, imgInfo.cprSize);
                        }
                        else
                        {
                            imgBuff = new byte[imgInfo.uncSize];
                            archiveStream.Read(imgBuff, 0, imgBuff.Length);
                        }
                    }
                    break;
                default:
                    throw new FormatException("Unsupported texture storage type");
            }

            return imgBuff;
        }

        public String GetTexArchive(string dir)
        {
            if (arcName == null)
                return dir + "\\DLC\\" + arcName.Substring(9, arcName.Length - 9) + "\\CookedPCConsole\\" + arcName + ".tfc";

            List<String> arclist;
            List<string> parts = new List<string>(dir.Split('\\'));
            parts.Remove("");
            if (parts[parts.Count - 1] == "CookedPCConsole")
                arclist = Directory.GetFiles(dir, "*.tfc", SearchOption.TopDirectoryOnly).ToList();
            else
                arclist = Directory.GetFiles(Path.Combine(dir, "CookedPCConsole"), "*.tfc", SearchOption.TopDirectoryOnly).ToList();
            if (Directory.Exists(Path.Combine(dir, "TexplorerDLCCache")))
                arclist.AddRange(Directory.GetFiles(Path.Combine(dir, "TexplorerDLCCache"), "*.tfc", SearchOption.AllDirectories));

            //string[] arcs = Directory.GetFiles(dir, "*.tfc", SearchOption.AllDirectories);
            foreach (String arc in arclist)
            {
                string test = arcName.Substring(arcName.Length - 4, 4);
                if (test.Equals(".tfc"))
                {
                    arcName = arcName.Split('.')[0];
                }
                if (String.Compare(Path.GetFileNameWithoutExtension(arc), arcName, true) == 0)
                    return Path.GetFullPath(arc);
            }

            // KFreon: If not found, either broken, or DLC
            return dir + "\\DLC\\" + arcName.Substring(9, arcName.Length - 9) + "\\CookedPCConsole\\" + arcName + ".tfc";
            //return null;
        }

        private void MakeCache(String filename, String biopath)
        {
            Random r = new Random();
            //string[] parts = filename.Split('\\');

            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < 4; i++)
                    fs.WriteValueS32(r.Next());
            }
            //DebugOutput.PrintLn( "Made new cache?  " + filename + "\n");
            AddToTOC(filename, biopath);
        }

        private void AddToTOC(String filename, String biopath)
        {
            List<string> parts = new List<string>(biopath.Split('\\'));
            parts.Remove("");
            if (parts[parts.Count - 1] == "CookedPCConsole")
            {
                parts.RemoveAt(parts.Count - 1);
                biopath = String.Join("\\", parts);
            }
            if (!File.Exists(Path.Combine(biopath, "PCConsoleTOC.bin")))
                throw new FileNotFoundException("TOC.bin not found at: " + Path.Combine(biopath, "PCConsoleTOC.bin"));

            AmaroK86.MassEffect3.TOCHandler tochandler = new AmaroK86.MassEffect3.TOCHandler(Path.Combine(biopath, "PCConsoleTOC.bin"), Path.GetDirectoryName(biopath) + @"\");
            //DebugOutput.PrintLn( " TOC handler exists?" + tochandler.existsFile(filename) + "\n");
            if (tochandler.existsFile(filename))
                return;

            int minval = 100000;
            int minid = 0;
            for (int i = 0; i < tochandler.chunkList.Count; i++)
            {
                if (tochandler.chunkList[i].fileList == null || tochandler.chunkList[i].fileList.Count == 0)
                    continue;
                if (tochandler.chunkList[i].fileList.Count < minval)
                {
                    minval = tochandler.chunkList[i].fileList.Count;
                    minid = i;
                }
            }
            // DebugOutput.PrintLn( "Should be saving TOC to file now\n");
            tochandler.addFile(filename, minid);
            tochandler.saveToFile(true);
        }

        private List<ImageInfo> privateimgList { get; set; } // showable image list
        public List<IImageInfo> imgList
        {
            get
            {
                List<IImageInfo> retval = new List<IImageInfo>();
                foreach (ImageInfo inf in privateimgList)
                    retval.Add(inf);
                return retval;
            }
            set
            {
                List<ImageInfo> retval = new List<ImageInfo>();
                foreach (IImageInfo inf in value)
                    retval.Add((ImageInfo)inf);
                privateimgList = retval;
            }
        }


        public string texFormat
        {
            get;
            set;
        }

        public List<string> allPccs
        {
            get;
            set;
        }

        public uint pccOffset
        {
            get;
            set;
        }

        public bool hasChanged
        {
            get;
            set;
        }

        public List<int> expIDs
        {
            get;
            set;
        }

        public byte[] ToArray(uint pccExportDataOffset, IPCCObject pcc)
        {
            return ToArray(pccExportDataOffset, (ME3PCCObject)pcc);
        }

        public int pccExpIdx
        {
            get;
            set;
        }

        public void CopyImgList(ITexture2D tex2D, IPCCObject PCC)
        {
            CopyImgList((ME3SaltTexture2D)tex2D, (ME3PCCObject)PCC);
        }


        public List<string> allFiles { get; set; }


        public int Mips
        {
            get;
            set;
        }


        public bool NoRenderFix
        {
            get;
            set;
        }


        public void LowResFix()
        {
            throw new NotImplementedException();
        }

        public IImageInfo GenerateImageInfo()
        {
            IImageInfo imginfo = privateimgList.First(img => (int)img.storageType != (int)ME3Texture2D.storage.empty);
            imginfo.GameVersion = 3;
            return imginfo;
        }

        public byte[] GetImageData(int size = -1)
        {
            byte[] imgdata = null;
            if (size == -1)
                imgdata = extractMaxImage(true);
            else
            {
                ImageSize tes;
                if (privateimgList.Count != 1)
                    tes = privateimgList.Where(img => (img.imgSize.width <= size || img.imgSize.height <= size) && img.offset != -1).Max(image => image.imgSize);
                else
                    tes = privateimgList.First().imgSize;
                imgdata = extractImage(tes.ToString(), true);
            }
            return imgdata;
        }

        public System.Drawing.Bitmap GetImage(int size = -1)
        {
            try
            {
                byte[] imgdata = GetImageData(size);
                if (imgdata == null)
                    return null;
                return Textures.Methods.GetImage(texFormat, imgdata);
            }
            catch { }
            return null;
        }

        public void DumpTexture(string filename)
        {
            using (StreamWriter sw = new StreamWriter(filename))
            {
                try
                {
                    sw.WriteLine("Allfiles: " + allFiles.Count);
                    foreach (string file in allFiles)
                        sw.WriteLine(file);
                }
                catch { }
                

                sw.WriteLine("AllPccs: " + allPccs.Count);
                foreach (string pcc in allPccs)
                    sw.WriteLine(pcc);

                sw.WriteLine(ArcDataSize);
                sw.WriteLine(arcName);
                sw.WriteLine(Class);
                sw.WriteLine(dataOffset);
                sw.WriteLine("ExpIDs: " + expIDs.Count);
                for (int i = 0; i < expIDs.Count; i++)
                    sw.WriteLine(expIDs);
                sw.WriteLine(exportOffset);

                sw.WriteLine(footerData);
                sw.WriteLine(FullArcPath);
                sw.WriteLine(headerData);
                sw.WriteLine(imageData);
                
                //this.imgList;
                sw.WriteLine(LODGroup);
                sw.WriteLine(Mips);
                sw.WriteLine(numMipMaps);
                sw.WriteLine(pccExpIdx);
                sw.WriteLine(pccOffset);
                sw.WriteLine(texFormat);
                sw.WriteLine(texName);
            }
        }
    }
}
