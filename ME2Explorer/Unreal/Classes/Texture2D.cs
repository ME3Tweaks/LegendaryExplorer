using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using AmaroK86.ImageFormat;
using Gibbed.IO;
using ME2Explorer;
using System.Windows.Forms;
using ManagedLZO;
using System.Diagnostics;
using ME2Explorer.Helper;

namespace ME2Explorer.Unreal.Classes
{
    public class Texture2D
    {
        public enum storage
        {
            //arcCpr = 0x3, // archive compressed
            arcCpr = 0x11, //archive compressed (guessing)
            arcUnc = 0x1, // archive uncompressed (DLC)
            pccSto = 0x0, // pcc local storage
            empty = 0x21  // unused image (void pointer sorta)
        }

        public struct ImageInfo
        {
            public storage storageType;
            public int uncSize;
            public int cprSize;
            public int offset;
            public ImageSize imgSize;

        }

        public const string className = "Texture2D";
        public const string class2 = "LightMapTexture2D";
        public const string class3 = "TextureFlipBook";
        public const string CustCache = "CustTextures";
        public string texName;
        public byte[] headerData;
        public byte[] imageData;
        byte[] footerData;
        public List<string> allPccs;
        public Dictionary<string, SaltPropertyReader.Property> properties;
        public List<ImageInfo> imgList { get; private set; } // showable image list
        public int exportOffset;
        public int pccExpIdx;
        public uint pccOffset;
        public uint dataOffset = 0;
        private uint numMipMaps;
        public string arcName;
        public string FullArcPath { get; private set; }
        public string pccFileName;
        public string LODGroup = "None";
        public string texFormat;
        public string Compression;
        public bool hasChanged;
        public string FullPackage;
        public int UnpackNum;
        private int ArcDataSize;
        public List<int> ExpIDs;
        public uint Hash;
        public String ListName;
        public String Class;

        public Texture2D(string name, List<string> pccs, List<int> expIDs, uint hash = 0, String listname = null)
        {
            hasChanged = false;
            texName = name;
            allPccs = pccs;
            ExpIDs = expIDs;
            Hash = hash;
            ListName = listname;
        }

        public Texture2D(PCCObject pcc, int pccExpID, String pathBioGame)
        {
            PCCObject.ExportEntry exp = pcc.Exports[pccExpID]; ;
            if (String.Compare(exp.ClassName, className) != 0 && String.Compare(exp.ClassName, class2) != 0 && String.Compare(exp.ClassName, class3) != 0)
            {
                throw new FormatException("Export is not a texture");
            }
            Class = exp.ClassName;
            exportOffset = exp.DataOffset;
            FullPackage = exp.PackageFullName;
            texName = exp.ObjectName;
            pccFileName = pcc.pccFileName;
            allPccs = new List<string>();
            allPccs.Add(pcc.pccFileName);
            properties = new Dictionary<string, SaltPropertyReader.Property>();
            byte[] rawData = (byte[])exp.Data.Clone();
            Compression = "No Compression";
            int propertiesOffset = SaltPropertyReader.detectStart(pcc, rawData);
            headerData = new byte[propertiesOffset];
            Buffer.BlockCopy(rawData, 0, headerData, 0, propertiesOffset);
            pccOffset = (uint)exp.DataOffset;
            UnpackNum = 0;
            List<SaltPropertyReader.Property> tempProperties = SaltPropertyReader.getPropList(pcc, rawData);
            for (int i = 0; i < tempProperties.Count; i++)
            {
                SaltPropertyReader.Property property = tempProperties[i];
                if (property.Name == "UnpackMin")
                    UnpackNum++;

                if (!properties.ContainsKey(property.Name))
                    properties.Add(property.Name, property);

                switch (property.Name)
                {
                    case "Format": texFormat = property.Value.StringValue; break;
                    case "TextureFileCacheName": arcName = property.Value.StringValue; break;
                    case "LODGroup": LODGroup = property.Value.StringValue; break;
                    case "CompressionSettings": Compression = property.Value.StringValue; break;
                    case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                }
            }
            // if "None" property isn't found throws an exception
            if (dataOffset == 0)
                throw new Exception("\"None\" property not found");

            if (!String.IsNullOrEmpty(arcName))
                FullArcPath = GetTexArchive(pathBioGame);

            imageData = new byte[rawData.Length - dataOffset];
            Buffer.BlockCopy(rawData, (int)dataOffset, imageData, 0, (int)(rawData.Length - dataOffset));

            //DebugOutput.PrintLn("ImageData size = " + imageData.Length);
            pccExpIdx = pccExpID;

            MemoryStream dataStream = new MemoryStream(imageData);
            imgList = new List<ImageInfo>();
            dataStream.ReadValueU32(); //Current position in pcc
            numMipMaps = dataStream.ReadValueU32();
            uint count = numMipMaps;
            ArcDataSize = 0;
            //DebugOutput.PrintLn(numMipMaps + " derp");
            while (dataStream.Position < dataStream.Length && count > 0)
            {
                ImageInfo imgInfo = new ImageInfo();
                imgInfo.storageType = (storage)dataStream.ReadValueS32();
                imgInfo.uncSize = dataStream.ReadValueS32();
                imgInfo.cprSize = dataStream.ReadValueS32();
                imgInfo.offset = dataStream.ReadValueS32();
                if (imgInfo.storageType == storage.pccSto)
                {
                    imgInfo.offset = (int)dataStream.Position;
                    dataStream.Seek(imgInfo.uncSize, SeekOrigin.Current);
                }
                else if (imgInfo.storageType == storage.arcCpr || imgInfo.storageType == storage.arcUnc)
                {
                    ArcDataSize += imgInfo.uncSize;
                }

                imgInfo.imgSize = new ImageSize(dataStream.ReadValueU32(), dataStream.ReadValueU32());
                if (imgList.Exists(img => img.imgSize == imgInfo.imgSize))
                {
                    uint width = imgInfo.imgSize.width;
                    uint height = imgInfo.imgSize.height;
                    if (width == 4 && imgList.Exists(img => img.imgSize.width == width))
                        width = imgList.Last().imgSize.width / 2;
                    if (width == 0)
                        width = 1;
                    if (height == 4 && imgList.Exists(img => img.imgSize.height == height))
                        height = imgList.Last().imgSize.height / 2;
                    if (height == 0)
                        height = 1;
                    imgInfo.imgSize = new ImageSize(width, height);
                    if (imgList.Exists(img => img.imgSize == imgInfo.imgSize))
                        throw new Exception("Duplicate image size found");
                }
                imgList.Add(imgInfo);
                count--;
                //DebugOutput.PrintLn("ImgInfo no: " + count + ", Storage Type = " + imgInfo.storageType + ", offset = " + imgInfo.offset);
            }
            // Grab the rest for the footer
            footerData = new byte[dataStream.Length - dataStream.Position];
            footerData = dataStream.ReadBytes(footerData.Length);
        }

        public string getFileFormat()
        {
            return ".dds";
        }

        public void extractImage(ImageInfo imgInfo, string archiveDir = null, string fileName = null)
        {
            ImageFile imgFile;
            if (fileName == null)
            {
                fileName = texName + "_" + imgInfo.imgSize + getFileFormat();
            }

            byte[] imgBuffer = null;

            switch (imgInfo.storageType)
            {
                case storage.pccSto:
                    imgBuffer = new byte[imgInfo.uncSize];
                    Buffer.BlockCopy(imageData, imgInfo.offset, imgBuffer, 0, imgInfo.uncSize);
                    break;
                case storage.arcCpr:
                case storage.arcUnc:
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive(archiveDir);
                    if (!File.Exists(archivePath))
                    {
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);
                    }

                    using (FileStream archiveStream = File.OpenRead(archivePath))
                    {
                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            SaltLZOHelper lzohelp = new SaltLZOHelper();
                            imgBuffer = lzohelp.DecompressTex(archiveStream, imgInfo.offset, imgInfo.uncSize, imgInfo.cprSize);
                        }
                        else
                        {
                            archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                            imgBuffer = new byte[imgInfo.uncSize];
                            archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
                        }
                    }
                    break;
                default:
                    throw new FormatException("Unsupported texture storage type");
            }

            imgFile = new DDS(fileName, imgInfo.imgSize, texFormat, imgBuffer);
            byte[] saveImg = imgFile.ToArray();
            using (FileStream outputImg = new FileStream(imgFile.fileName, FileMode.Create, FileAccess.Write))
                outputImg.Write(saveImg, 0, saveImg.Length);
        }

        public void extractImage(string strImgSize, string archiveDir = null, string fileName = null)
        {
            ImageSize imgSize = ImageSize.stringToSize(strImgSize);
            if (imgList.Exists(img => img.imgSize == imgSize))
                extractImage(imgList.Find(img => img.imgSize == imgSize), archiveDir, fileName);
            else
                throw new FileNotFoundException("Image with resolution " + imgSize + " not found");
        }

        public byte[] ToArray(int pccExportDataOffset, PCCObject pcc)
        {
            MemoryStream buffer = new MemoryStream();
            buffer.Write(headerData, 0, headerData.Length);

            if (properties.ContainsKey("LODGroup"))
            {
                properties["LODGroup"].Value.StringValue = "TEXTUREGROUP_LightAndShadowMap";
                properties["LODGroup"].Value.String2 = pcc.Names[0];
            }
            else
            {
                buffer.WriteValueS64(pcc.AddName("LODGroup"));
                buffer.WriteValueS64(pcc.AddName("ByteProperty"));
                buffer.WriteValueS64(8);
                buffer.WriteValueS64(pcc.AddName("TEXTUREGROUP_LightAndShadowMap"));
            }

            foreach (KeyValuePair<string, SaltPropertyReader.Property> kvp in properties)
            {
                SaltPropertyReader.Property prop = kvp.Value;

                if (prop.Name == "UnpackMin")
                {
                    for (int j = 0; j < UnpackNum; j++)
                    {
                        buffer.WriteValueS64(pcc.AddName(prop.Name));
                        buffer.WriteValueS64(pcc.AddName(prop.TypeVal.ToString()));
                        buffer.WriteValueS32(prop.Size);
                        buffer.WriteValueS32(j);
                        buffer.WriteValueF32(prop.Value.FloatValue, Endian.Little);
                    }
                    continue;
                }

                buffer.WriteValueS64(pcc.AddName(prop.Name));
                if (prop.Name == "None")
                {
                    for (int j = 0; j < 12; j++)
                        buffer.WriteByte(0);
                }
                else
                {
                    buffer.WriteValueS64(pcc.AddName(prop.TypeVal.ToString()));
                    buffer.WriteValueS64(prop.Size);

                    switch (prop.TypeVal)
                    {
                        case SaltPropertyReader.Type.IntProperty:
                            buffer.WriteValueS32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.BoolProperty:
                            buffer.WriteValueS32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.NameProperty:
                            buffer.WriteValueS64(pcc.AddName(prop.Value.StringValue));
                            break;
                        case SaltPropertyReader.Type.StrProperty:
                            buffer.WriteValueS32(prop.Value.StringValue.Length + 1);
                            foreach (char c in prop.Value.StringValue)
                                buffer.WriteByte((byte)c);
                            buffer.WriteByte(0);
                            break;
                        case SaltPropertyReader.Type.StructProperty:
                            buffer.WriteValueS64(pcc.AddName(prop.Value.StringValue));
                            foreach (SaltPropertyReader.PropertyValue value in prop.Value.Array)
                                buffer.WriteValueS32(value.IntValue);
                            break;
                        case SaltPropertyReader.Type.ByteProperty:
                            buffer.WriteValueS32(pcc.AddName(prop.Value.StringValue));
                            buffer.WriteValueS32(pcc.AddName(prop.Value.String2));
                            break;
                        case SaltPropertyReader.Type.FloatProperty:
                            buffer.WriteValueF32(prop.Value.FloatValue, Endian.Little);
                            break;
                        default:
                            throw new FormatException("unknown property");
                    }
                }
            }

            buffer.WriteValueS32((int)buffer.Position + pccExportDataOffset);

            //Remove empty textures
            List<ImageInfo> tempList = new List<ImageInfo>();
            foreach (ImageInfo imgInfo in imgList)
            {
                if (imgInfo.storageType != storage.empty)
                    tempList.Add(imgInfo);
            }
            imgList = tempList;
            numMipMaps = (uint)imgList.Count;

            buffer.WriteValueU32(numMipMaps);
            foreach (ImageInfo imgInfo in imgList)
            {
                buffer.WriteValueS32((int)imgInfo.storageType);
                buffer.WriteValueS32(imgInfo.uncSize);
                buffer.WriteValueS32(imgInfo.cprSize);
                if (imgInfo.storageType == storage.pccSto)
                {
                    buffer.WriteValueS32((int)(buffer.Position + pccExportDataOffset));
                    buffer.Write(imageData, imgInfo.offset, imgInfo.uncSize);
                }
                else
                    buffer.WriteValueS32(imgInfo.offset);
                if (imgInfo.imgSize.width < 4)
                    buffer.WriteValueU32(4);
                else
                    buffer.WriteValueU32(imgInfo.imgSize.width);
                if (imgInfo.imgSize.height < 4)
                    buffer.WriteValueU32(4);
                else
                    buffer.WriteValueU32(imgInfo.imgSize.height);
            }
            buffer.WriteBytes(footerData);
            return buffer.ToArray();
        }

        public void replaceImage(string strImgSize, string fileToReplace, string archiveDir)
        {
            ImageSize imgSize = ImageSize.stringToSize(strImgSize);
            if (!imgList.Exists(img => img.imgSize == imgSize))
                throw new FileNotFoundException("Image with resolution " + imgSize + " isn't found");

            int imageIdx = imgList.FindIndex(img => img.imgSize == imgSize);
            ImageInfo imgInfo = imgList[imageIdx];

            if (!File.Exists(fileToReplace))
                throw new FileNotFoundException("invalid file to replace: " + fileToReplace);

            // check if replacing image is supported
            ImageFile imgFile;
            string fileFormat = Path.GetExtension(fileToReplace);
            switch (fileFormat)
            {
                case ".dds": imgFile = new DDS(fileToReplace, null); break;
                case ".DDS": imgFile = new DDS(fileToReplace, null); break;
                default: throw new FormatException(fileFormat + " image extension not supported");
            }

            if (imgFile.imgSize.height != imgInfo.imgSize.height || imgFile.imgSize.width != imgInfo.imgSize.width)
                throw new FormatException("Incorrect input texture dimensions. Expected: " + imgInfo.imgSize.ToString());

            if (texFormat == "PF_NormalMap_HQ")
            {
                if (imgFile.format != "ATI2")
                    throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
            }
            else if (String.Compare(texFormat, "PF_" + imgFile.format, true) != 0 &&
                String.Compare(texFormat, imgFile.format, true) != 0)
            {
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
            }

            byte[] imgBuffer;

            // if the image is empty then recover the archive compression from the image list
            if (imgInfo.storageType == storage.empty)
            {
                imgInfo.storageType = imgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
                imgInfo.uncSize = imgFile.resize().Length;
                imgInfo.cprSize = imgFile.resize().Length;
            }

            // overwrite previous choices for specific cases
            if (properties.ContainsKey("NeverStream") && properties["NeverStream"].Value.IntValue == 1)
                imgInfo.storageType = storage.pccSto;

            switch (imgInfo.storageType)
            {
                case storage.arcCpr:
                case storage.arcUnc:
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive(archiveDir);
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

                    using (FileStream archiveStream = new FileStream(archivePath, FileMode.Append, FileAccess.Write))
                    {
                        int newOffset = (int)archiveStream.Position;
                        //archiveStream.Position = imgInfo.offset;
                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            byte[] tempBuff;
                            SaltLZOHelper lzohelper = new SaltLZOHelper();
                            tempBuff = lzohelper.CompressTex(imgBuffer);
                            imgBuffer = new byte[tempBuff.Length];
                            Buffer.BlockCopy(tempBuff, 0, imgBuffer, 0, tempBuff.Length);
                            imgInfo.cprSize = imgBuffer.Length;
                        }
                        //else
                        archiveStream.Write(imgBuffer, 0, imgBuffer.Length);

                        imgInfo.offset = newOffset;
                    }
                    break;
                case storage.pccSto:
                    //imgBuffer = imgFile.imgData; // copy image data as-is
                    imgBuffer = imgFile.resize();
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
                    break;
            }

            imgList[imageIdx] = imgInfo;
        }

        public void OneImageToRuleThemAll(string imageFileName, string archiveDir)
        {
            if (Path.GetExtension(imageFileName).ToLowerInvariant() == ".tga")
            {
                throw new FormatException("TGAs don't support MIPMAPs. Use individual level replacements instead.");
            }

            ImageMipMapHandler imgMipMap = new ImageMipMapHandler(imageFileName, null);

            if (Class == class2 || Class == class3)
                ChangeFormat(imgMipMap.imageList[0].format);

            // starts from the smaller image
            for (int i = imgMipMap.imageList.Count - 1; i >= 0; i--)
            {
                ImageFile newImageFile = imgMipMap.imageList[i];

                if (texFormat == "PF_NormalMap_HQ")
                {
                    if (newImageFile.format != "ATI2")
                        throw new FormatException("Different image format, original is " + texFormat + ", new is " + newImageFile.subtype());
                }
                else if (String.Compare(texFormat, "PF_" + newImageFile.format, true) != 0 &&
                    String.Compare(texFormat, newImageFile.format, true) != 0)
                {
                    throw new FormatException("Different image format, original is " + texFormat + ", new is " + newImageFile.subtype());
                }

                // write the new image into a file (I know that's crappy solution, I'll find another one later...)
                using (FileStream newImageStream = File.Create(newImageFile.fileName))
                {
                    byte[] buffer = newImageFile.ToArray();
                    newImageStream.Write(buffer, 0, buffer.Length);
                }

                // if the image size exists inside the texture2d image list then we have to replace it
                if (imgList.Exists(img => img.imgSize == newImageFile.imgSize))
                {
                    // ...but at least for now I can reuse my replaceImage function... ;)
                    replaceImage(newImageFile.imgSize.ToString(), newImageFile.fileName, archiveDir);
                }
                else if (newImageFile.imgSize.width > imgList[0].imgSize.width) // if the image doesn't exists then we have to add it
                {
                    // ...and use my addBiggerImage function! :P
                    addBiggerImage(newImageFile.fileName, archiveDir);
                }
                //else
                //    addMissingImage(newImageFile.imgSize.ToString(), newImageFile.fileName);
                // else ignore the image

                File.Delete(newImageFile.fileName);
            }

            // Remove higher res versions and fix up properties
            while (imgList[0].imgSize.width > imgMipMap.imageList[0].imgSize.width)
            //while (imgMipMap.imageList.Count + 2 < imgList.Count)
            {
                imgList.RemoveAt(0);
                numMipMaps--;
            }
            if (properties.ContainsKey("SizeX"))
                properties["SizeX"].Value.IntValue = (int)imgList[0].imgSize.width;
            if (properties.ContainsKey("SizeY"))
                properties["SizeY"].Value.IntValue = (int)imgList[0].imgSize.height;
            if (properties.ContainsKey("MipTailBaseIdx"))
                properties["MipTailBaseIdx"].Value.IntValue = imgList.Count + 1;

        }

        public void addBiggerImage(string imagePathToAdd, string archiveDir)
        {
            ImageSize biggerImageSizeOnList = imgList.Max(image => image.imgSize);
            // check if replacing image is supported
            ImageFile imgFile;
            string fileFormat = Path.GetExtension(imagePathToAdd);
            switch (fileFormat)
            {
                case ".dds": imgFile = new DDS(imagePathToAdd, null); break;
                case ".DDS": imgFile = new DDS(imagePathToAdd, null); break;
                default: throw new FormatException(fileFormat + " image extension not supported");
            }

            if (texFormat == "PF_NormalMap_HQ")
            {
                if (imgFile.format != "ATI2")
                    throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
            }
            else if (String.Compare(texFormat, "PF_" + imgFile.format, true) != 0 &&
                String.Compare(texFormat, imgFile.format, true) != 0)
            {
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
            }

            // check if image to add is valid
            if (biggerImageSizeOnList.width * 2 != imgFile.imgSize.width || biggerImageSizeOnList.height * 2 != imgFile.imgSize.height)
                throw new FormatException("image size " + imgFile.imgSize + " isn't valid, must be " + new ImageSize(biggerImageSizeOnList.width * 2, biggerImageSizeOnList.height * 2));

            if (imgList.Count <= 1)
                throw new Exception("Unable to add image, texture must have more than one image present");

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = imgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
            // for additional mipmaps keep them in external archive but only when
            // texture allready have such property
            if (properties.ContainsKey("TextureFileCacheName"))
                newImgInfo.storageType = storage.arcCpr;
            newImgInfo.imgSize = imgFile.imgSize;
            newImgInfo.uncSize = imgFile.resize().Length;
            newImgInfo.cprSize = 0x00; // not yet filled
            newImgInfo.offset = 0x00; // not yet filled
            imgList.Insert(0, newImgInfo); // insert new image on top of the list

            //now I let believe the program that I'm doing an image replace, saving lot of code ;)
            replaceImage(newImgInfo.imgSize.ToString(), imagePathToAdd, archiveDir);

            //updating num of images
            numMipMaps++;

            // update MipTailBaseIdx
            int propVal = properties["MipTailBaseIdx"].Value.IntValue;
            propVal++;
            properties["MipTailBaseIdx"].Value.IntValue = propVal;

            // update Sizes
            properties["SizeX"].Value.IntValue = (int)newImgInfo.imgSize.width;
            properties["SizeY"].Value.IntValue = (int)newImgInfo.imgSize.height;
        }

        public void singleImageUpscale(string imagePathToAdd, string archiveDir)
        {
            ImageSize biggerImageSizeOnList = imgList.Max(image => image.imgSize);
            // check if replacing image is supported
            ImageFile imgFile;
            string fileFormat = Path.GetExtension(imagePathToAdd);
            switch (fileFormat)
            {
                case ".dds": imgFile = new DDS(imagePathToAdd, null); break;
                case ".DDS": imgFile = new DDS(imagePathToAdd, null); break;
                default: throw new FormatException(fileFormat + " image extension not supported");
            }

            if (texFormat == "PF_NormalMap_HQ")
            {
                if (imgFile.format != "ATI2")
                    throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
            }
            else if (String.Compare(texFormat, "PF_" + imgFile.format, true) != 0 &&
                String.Compare(texFormat, imgFile.format, true) != 0)
            {
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
            }

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = imgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
            newImgInfo.imgSize = imgFile.imgSize;
            newImgInfo.uncSize = imgFile.resize().Length;
            newImgInfo.cprSize = 0x00; // not yet filled
            newImgInfo.offset = 0x00; // not yet filled
            imgList.RemoveAt(0);  // Remove old single image and add new one
            imgList.Add(newImgInfo);

            //now I let believe the program that I'm doing an image replace, saving lot of code ;)
            replaceImage2(newImgInfo.imgSize.ToString(), imagePathToAdd, archiveDir);

            // update Sizes
            properties["SizeX"].Value.IntValue = (int)newImgInfo.imgSize.width;
            properties["SizeY"].Value.IntValue = (int)newImgInfo.imgSize.height;
        }

        public void replaceImage2(string strImgSize, string fileToReplace, string archiveDir)
        {
            ImageSize imgSize = ImageSize.stringToSize(strImgSize);
            if (!imgList.Exists(img => img.imgSize == imgSize))
                throw new FileNotFoundException("Image with resolution " + imgSize + " isn't found");

            int imageIdx = imgList.FindIndex(img => img.imgSize == imgSize);
            ImageInfo imgInfo = imgList[imageIdx];

            if (!File.Exists(fileToReplace))
                throw new FileNotFoundException("invalid file to replace: " + fileToReplace);

            // check if replacing image is supported
            ImageFile imgFile;
            string fileFormat = Path.GetExtension(fileToReplace);
            switch (fileFormat)
            {
                case ".dds": imgFile = new DDS(fileToReplace, null); break;
                case ".DDS": imgFile = new DDS(fileToReplace, null); break;
                default: throw new FormatException(fileFormat + " image extension not supported");
            }

            if (texFormat == "PF_NormalMap_HQ")
            {
                if (imgFile.format != "ATI2")
                    throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
            }
            else if (String.Compare(texFormat, "PF_" + imgFile.format, true) != 0 &&
                String.Compare(texFormat, imgFile.format, true) != 0)
            {
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
            }

            byte[] imgBuffer;

            // if the image is empty then recover the archive compression from the image list
            if (imgInfo.storageType == storage.empty)
            {
                imgInfo.storageType = imgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
                imgInfo.uncSize = imgFile.resize().Length;
                imgInfo.cprSize = imgFile.resize().Length;
            }

            switch (imgInfo.storageType)
            {
                case storage.arcCpr:
                case storage.arcUnc:
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive(archiveDir);
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

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

                    using (FileStream archiveStream = new FileStream(archivePath, FileMode.Append, FileAccess.Write))
                    {
                        int newOffset = (int)archiveStream.Position;
                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            byte[] tempBuff;
                            SaltLZOHelper lzohelper = new SaltLZOHelper();
                            tempBuff = lzohelper.CompressTex(imgBuffer);
                            imgBuffer = new byte[tempBuff.Length];
                            Buffer.BlockCopy(tempBuff, 0, imgBuffer, 0, tempBuff.Length);
                            imgInfo.cprSize = imgBuffer.Length;
                        }
                        archiveStream.Write(imgBuffer, 0, imgBuffer.Length);

                        imgInfo.offset = newOffset;
                    }
                    break;
                case storage.pccSto:
                    imgBuffer = imgFile.resize();
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
                    break;
            }

            imgList[imageIdx] = imgInfo;
        }

        public void DumpImageData(ImageInfo imgInfo, string archiveDir = null, string fileName = null)
        {
            if (fileName == null)
            {
                fileName = texName + "_" + imgInfo.imgSize + ".bin";
            }

            byte[] imgBuffer;

            switch (imgInfo.storageType)
            {
                case storage.pccSto:
                    imgBuffer = new byte[imgInfo.uncSize];
                    Buffer.BlockCopy(imageData, imgInfo.offset, imgBuffer, 0, imgInfo.uncSize);
                    break;
                case storage.arcCpr:
                case storage.arcUnc:
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        GetTexArchive(archiveDir);
                    if (!File.Exists(archivePath))
                    {
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);
                    }

                    using (FileStream archiveStream = File.OpenRead(archivePath))
                    {
                        //archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            SaltLZOHelper lzohelp = new SaltLZOHelper();
                            imgBuffer = lzohelp.DecompressTex(archiveStream, imgInfo.offset, imgInfo.uncSize, imgInfo.cprSize);
                        }
                        else
                        {
                            archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                            imgBuffer = new byte[imgInfo.uncSize];
                            archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
                        }
                    }
                    break;
                default:
                    throw new FormatException("Unsupported texture storage type");
            }
            using (FileStream outputImg = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                outputImg.Write(imgBuffer, 0, imgBuffer.Length);
        }

        public void DumpImage(string strImgSize, string archiveDir = null, string fileName = null)
        {
            ImageSize imgSize = ImageSize.stringToSize(strImgSize);
            if (imgList.Exists(img => img.imgSize == imgSize))
                DumpImageData(imgList.Find(img => img.imgSize == imgSize), archiveDir, fileName);
            else
                throw new FileNotFoundException("Image with resolution " + imgSize + " not found");
        }

        public byte[] DumpImage(ImageSize imgSize, string archiveDir)
        {
            byte[] imgBuff = null;

            ImageInfo imgInfo;
            if (imgList.Exists(img => img.imgSize == imgSize))
                imgInfo = imgList.Find(img => img.imgSize == imgSize);
            else
                throw new FileNotFoundException("Image with resolution " + imgSize + " not found");

            switch (imgInfo.storageType)
            {
                case storage.pccSto:
                    imgBuff = new byte[imgInfo.uncSize];
                    Buffer.BlockCopy(imageData, imgInfo.offset, imgBuff, 0, imgInfo.uncSize);
                    break;
                case storage.arcCpr:
                case storage.arcUnc:
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive(archiveDir);
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    using (FileStream archiveStream = File.OpenRead(archivePath))
                    {
                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            SaltLZOHelper lzohelp = new SaltLZOHelper();
                            imgBuff = lzohelp.DecompressTex(archiveStream, imgInfo.offset, imgInfo.uncSize, imgInfo.cprSize);
                        }
                        else
                        {
                            archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
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

        public void ChangeTexFormat(string newFormat, PCCObject pcc)
        {
            SaltPropertyReader.Property prop = properties["Format"];
            Int64 formatID = (Int64)pcc.AddName(newFormat);
            byte[] buff = BitConverter.GetBytes(formatID);
            Buffer.BlockCopy(buff, 0, prop.raw, 24, sizeof(Int64));
            prop.Value.StringValue = pcc.Names[(int)formatID];
            properties["Format"] = prop;
            texFormat = properties["Format"].Value.StringValue;
        }

        public void ChangeCompression(string newComp, PCCObject pcc)
        {
            if (!properties.ContainsKey("CompressionSettings"))
            {
                throw new KeyNotFoundException("Texture doesn't have a compression property");
            }
            SaltPropertyReader.Property prop = properties["CompressionSettings"];
            Int64 comp = (Int64)pcc.AddName(newComp);
            byte[] buff = BitConverter.GetBytes(comp);
            Buffer.BlockCopy(buff, 0, prop.raw, 24, sizeof(Int64));
            prop.Value.StringValue = pcc.Names[(int)comp];
            properties["CompressionSettings"] = prop;
            Compression = properties["CompressionSettings"].Value.StringValue;

        }

        public void CopyImgList(Texture2D inTex, PCCObject pcc)
        {
            numMipMaps = inTex.numMipMaps;

            if (properties.ContainsKey("NeverStream") && properties["NeverStream"].Value.IntValue == 1)
            {
                imageData = null;
                GC.Collect();
                // store images as pccSto format
                imgList = new List<ImageInfo>();
                MemoryStream tempData = new MemoryStream();

                for (int i = 0; i < inTex.imgList.Count; i++)
                {
                    ImageInfo newImg = new ImageInfo();
                    ImageInfo replaceImg = inTex.imgList[i];
                    newImg.storageType = storage.pccSto;
                    newImg.uncSize = replaceImg.uncSize;
                    newImg.cprSize = replaceImg.uncSize;
                    newImg.imgSize = replaceImg.imgSize;
                    newImg.offset = (int)(tempData.Position);
                    if (replaceImg.storageType == storage.arcCpr)
                    {
                        string archivePath = inTex.FullArcPath;
                        if (!File.Exists(archivePath))
                            throw new FileNotFoundException("Texture archive not found in " + archivePath);

                        using (FileStream archiveStream = File.OpenRead(archivePath))
                        {
                            archiveStream.Seek(replaceImg.offset, SeekOrigin.Begin);
                            SaltLZOHelper lzohelp = new SaltLZOHelper();
                            tempData.WriteBytes(lzohelp.DecompressTex(archiveStream, replaceImg.offset, replaceImg.uncSize, replaceImg.cprSize));
                        }
                    }
                    else if (replaceImg.storageType == storage.pccSto)
                    {
                        byte[] buffer = new byte[newImg.cprSize];
                        Buffer.BlockCopy(inTex.imageData, replaceImg.offset, buffer, 0, buffer.Length);
                        tempData.WriteBytes(buffer);
                    }
                    else
                        throw new NotImplementedException("Copying from non package stored texture no available");
                    imgList.Add(newImg);
                }

                for (int i = 0; i < imgList.Count; i++)
                {
                    ImageInfo tempinfo = imgList[i];
                    if (inTex.imgList[i].storageType == storage.empty)
                        tempinfo.storageType = storage.empty;
                    imgList[i] = tempinfo;
                }

                imageData = tempData.ToArray();
                tempData.Close();
                tempData = null;
                GC.Collect();
            }
            else
            {
                imageData = inTex.imageData;
                imgList = inTex.imgList;
            }

            // add properties "TextureFileCacheName" and "TFCFileGuid" if they are missing,
            if (!properties.ContainsKey("TextureFileCacheName") && inTex.properties.ContainsKey("TextureFileCacheName"))
            {
                SaltPropertyReader.Property none = properties["None"];
                properties.Remove("None");

                SaltPropertyReader.Property property = new SaltPropertyReader.Property();
                property.TypeVal = SaltPropertyReader.Type.NameProperty;
                property.Name = "TextureFileCacheName";
                property.Size = 8;
                SaltPropertyReader.PropertyValue value = new SaltPropertyReader.PropertyValue();
                value.StringValue = "Textures";
                property.Value = value;
                properties.Add("TextureFileCacheName", property);
                arcName = value.StringValue;

                if (!properties.ContainsKey("TFCFileGuid"))
                {
                    SaltPropertyReader.Property guidprop = new SaltPropertyReader.Property();
                    guidprop.TypeVal = SaltPropertyReader.Type.StructProperty;
                    guidprop.Name = "TFCFileGuid";
                    guidprop.Size = 16;
                    SaltPropertyReader.PropertyValue guid = new SaltPropertyReader.PropertyValue();
                    guid.len = guidprop.Size;
                    guid.StringValue = "Guid";
                    guid.IntValue = pcc.AddName(guid.StringValue);
                    guid.Array = new List<SaltPropertyReader.PropertyValue>();
                    for (int i = 0; i < 4; i++)
                        guid.Array.Add(new SaltPropertyReader.PropertyValue());
                    guidprop.Value = guid;
                    properties.Add("TFCFileGuid", guidprop);
                }

                properties.Add("None", none);
            }

            // copy specific properties from inTex
            for (int i = 0; i < inTex.properties.Count; i++)
            {
                SaltPropertyReader.Property prop = inTex.properties.ElementAt(i).Value;
                switch (prop.Name)
                {
                    case "TextureFileCacheName":
                        arcName = prop.Value.StringValue;
                        properties["TextureFileCacheName"].Value.StringValue = arcName;
                        break;
                    case "TFCFileGuid":
                        SaltPropertyReader.Property GUIDProp = properties["TFCFileGuid"];
                        for (int l = 0; l < 4; l++)
                        {
                            SaltPropertyReader.PropertyValue tempVal = GUIDProp.Value.Array[l];
                            tempVal.IntValue = prop.Value.Array[l].IntValue;
                            GUIDProp.Value.Array[l] = tempVal;
                        }
                        break;
                    case "MipTailBaseIdx":
                        properties["MipTailBaseIdx"].Value.IntValue = prop.Value.IntValue;
                        break;
                    case "SizeX":
                        properties["SizeX"].Value.IntValue = prop.Value.IntValue;
                        break;
                    case "SizeY":
                        properties["SizeY"].Value.IntValue = prop.Value.IntValue;
                        break;
                }
            }
        }

        public String GetTexArchive(string dir)
        {
            if (arcName == null)
                return null;

            List<String> arclist = Directory.GetFiles(Path.Combine(dir, "CookedPC"), "*.tfc", SearchOption.TopDirectoryOnly).ToList();
            if (Directory.Exists(Path.Combine(dir, "DLC")))
                arclist.AddRange(Directory.GetFiles(Path.Combine(dir, "DLC"), "*.tfc", SearchOption.AllDirectories));

            foreach (String arc in arclist)
            {
                if (String.Compare(Path.GetFileNameWithoutExtension(arc), arcName, true) == 0)
                    return Path.GetFullPath(arc);
            }
            return null;
        }

        private void MakeCache(String filename, String biopath)
        {
            Random r = new Random();

            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < 4; i++)
                    fs.WriteValueS32(r.Next());
            }
        }

        private void ChooseNewCache(string bioPath, int buffLength)
        {
            int i = 0;
            while (true)
            {
                FileInfo cacheInfo;
                cacheInfo = new FileInfo(Path.Combine(bioPath, "CookedPC", CustCache + i + ".tfc"));
                if (!cacheInfo.Exists)
                {
                    MakeCache(cacheInfo.FullName, bioPath);
                    MoveCaches(bioPath + "\\CookedPC", CustCache + i + ".tfc");
                    properties["TextureFileCacheName"].Value.StringValue = CustCache + i;
                    arcName = CustCache + i;
                    FullArcPath = cacheInfo.FullName;
                    return;
                }
                else if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)
                {
                    MoveCaches(bioPath + "\\CookedPC", CustCache + i + ".tfc");
                    properties["TextureFileCacheName"].Value.StringValue = CustCache + i;
                    arcName = CustCache + i;
                    FullArcPath = cacheInfo.FullName;
                    return;
                }
                i++;
            }
        }

        private void MoveCaches(string cookedPath, string NewCache)
        {
            //Fix the GUID
            using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Open, FileAccess.Read))
            {
                SaltPropertyReader.Property GUIDProp = properties["TFCFileGuid"];

                for (int i = 0; i < 4; i++)
                {
                    SaltPropertyReader.PropertyValue tempVal = GUIDProp.Value.Array[i];
                    tempVal.IntValue = newCache.ReadValueS32();
                    GUIDProp.Value.Array[i] = tempVal;
                }
            }

            //Move across any existing textures
            using (FileStream oldCache = new FileStream(FullArcPath, FileMode.Open, FileAccess.Read))
            {
                using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Append, FileAccess.Write))
                {
                    for (int i = 0; i < imgList.Count; i++)
                    {
                        ImageInfo img = imgList[i];

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
                        imgList[i] = img;
                    }
                }
            }
        }

        public DDSFormat GetDDSFormat()
        {
            switch (texFormat)
            {
                case "PF_DXT1":
                    return DDSFormat.DXT1;
                case "PF_DXT5":
                    return DDSFormat.DXT5;
                case  "PF_NormalMap_HQ":
                    return DDSFormat.ATI2;
                default:
                    throw new FormatException("Unknown or non-DDS Format");
            }
        }

        private void ChangeFormat(string newformat)
        {
            if (newformat == "PF_R8G8B8" || newformat == "R8G8B8")
                throw new FormatException("24-bit textures are not allowed in ME2");
            if (texFormat != "PF_NormalMap_HQ")
            {
                if (texFormat != newformat && texFormat != "PF_" + newformat)
                {
                    if (newformat.Substring(0, 3) != "PF_")
                        texFormat = "PF_" + newformat;
                    else
                        texFormat = newformat;
                    properties["Format"].Value.StringValue = texFormat;
                }
            }
            else
            {
                if (newformat != "ATI2")
                {
                    if (newformat.Substring(0, 3) != "PF_")
                        texFormat = "PF_" + newformat;
                    else
                        texFormat = newformat;
                    properties["Format"].Value.StringValue = texFormat;
                }
            }
        }
    }
}
