using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using Gibbed.IO;
using AmaroK86.ImageFormat;
using AmaroK86.MassEffect3.ZlibBlock;
using Ini;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Unreal.Classes
{
    public class Texture2D
    {
        public enum storage
        {
            arcCpr = 0x3, // archive compressed
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

        PCCObject pccRef;
        public const string className = "Texture2D";
        public string texName { get; private set; }
        public string arcName { get; private set; }
        public string LODGroup { get; private set; }
        private string texFormat;
        private byte[] headerData;
        private byte[] imageData;
        private int pccExpIdx;
        public uint pccOffset;
        private uint dataOffset = 0;
        private uint numMipMaps;
        public Dictionary<string,PropertyReader.Property> properties;
        public List<ImageInfo> imgList { get; private set; } // showable image list

        public Texture2D(PCCObject pccObj, int texIdx)
        {
            pccRef = pccObj;
            // check if texIdx is an Export index and a Texture2D class
            if (pccObj.isExport(texIdx) && (pccObj.Exports[texIdx].ClassName == className))
            {
                PCCObject.ExportEntry expEntry = pccObj.Exports[texIdx];
                properties = new Dictionary<string, PropertyReader.Property>();
                byte[] rawData = (byte[])expEntry.Data.Clone();
                int propertiesOffset = PropertyReader.detectStart(pccObj, rawData);
                headerData = new byte[propertiesOffset];
                Buffer.BlockCopy(rawData, 0, headerData, 0, propertiesOffset);
                pccOffset = (uint)expEntry.DataOffset;
                List<PropertyReader.Property> tempProperties = PropertyReader.getPropList(pccObj, rawData);
                texName = expEntry.ObjectName;
                for (int i = 0; i < tempProperties.Count; i++)
                {
                    PropertyReader.Property property = tempProperties[i];
                    if (!properties.ContainsKey(pccObj.Names[property.Name]))
                        properties.Add(pccObj.Names[property.Name], property);

                    switch (pccObj.Names[property.Name])
                    {
                        case "Format": texFormat = pccObj.Names[property.Value.IntValue].Substring(3); break;
                        case "TextureFileCacheName": arcName = pccObj.Names[property.Value.IntValue]; break;
                        case "LODGroup": LODGroup = pccObj.Names[property.Value.IntValue]; break;
                        case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                    }
                }

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
            MemoryStream dataStream = new MemoryStream(imageData);
            numMipMaps = dataStream.ReadValueU32();
            uint count = numMipMaps;

            imgList = new List<ImageInfo>();
            while (dataStream.Position < dataStream.Length && count > 0)
            {
                ImageInfo imgInfo = new ImageInfo();
                imgInfo.storageType = (storage)dataStream.ReadValueS32();
                imgInfo.uncSize = dataStream.ReadValueS32();
                imgInfo.cprSize = dataStream.ReadValueS32();
                imgInfo.offset = dataStream.ReadValueS32();
                if (imgInfo.storageType == storage.pccSto)
                {
                    //imgInfo.offset = (int)(pccOffset + dataOffset); // saving pcc offset as relative to exportdata offset, not absolute
                    imgInfo.offset = (int)dataStream.Position; // saving pcc offset as relative to exportdata offset, not absolute
                    //MessageBox.Show("Pcc class offset: " + pccOffset + "\nimages data offset: " + imgInfo.offset.ToString());
                    dataStream.Seek(imgInfo.uncSize, SeekOrigin.Current);
                }
                imgInfo.imgSize = new ImageSize(dataStream.ReadValueU32(), dataStream.ReadValueU32());
                imgList.Add(imgInfo);
                count--;
            }

            // save what remains
            /*int remainingBytes = (int)(dataStream.Length - dataStream.Position);
            footerData = new byte[remainingBytes];
            dataStream.Read(footerData, 0, footerData.Length);*/
        }

        public byte[] ToArray(int pccExportDataOffset)
        {
            MemoryStream buffer = new MemoryStream();
            buffer.Write(headerData, 0, headerData.Length);
            foreach (KeyValuePair<string, PropertyReader.Property> kvp in properties)
            {
                PropertyReader.Property property = kvp.Value;

                // this is the part when I get rid of the LODGroup property!!!!!!!!!!!!!!!
                // the texture will use texturegroup_world as default texturegroup
                //if (kvp.Key == "LODGroup")
                //    continue;
                if (kvp.Key == "LODBias")
                    continue;
                if (kvp.Key == "InternalFormatLODBias")
                    continue;

                buffer.Write(property.raw, 0, property.raw.Length);
                if (kvp.Key == "UnpackMin")
                {
                    buffer.Write(property.raw, 0, property.raw.Length);
                    buffer.Write(property.raw, 0, property.raw.Length);
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
            buffer.Write(imageData, imageData.Length-24, 24);
            return buffer.ToArray();
        }

        public void extractImage(string strImgSize, string archiveDir = null, string fileName = null)
        {
            ImageSize imgSize = ImageSize.stringToSize(strImgSize);
            if (imgList.Exists(img => img.imgSize == imgSize))
                extractImage(imgList.Find(img => img.imgSize == imgSize), archiveDir, fileName);
            else
                throw new FileNotFoundException("Image with resolution " + imgSize + " not found");
        }

        public void extractImage(ImageInfo imgInfo, string archiveDir = null, string fileName = null)
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
                    Buffer.BlockCopy(imageData, imgInfo.offset, imgBuffer, 0, imgInfo.uncSize);
                    break;
                case storage.arcCpr:
                case storage.arcUnc:
                    string archivePath = archiveDir + "\\" + arcName + ".tfc";
                    if (!File.Exists(archivePath))
                    {
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);
                    }

                    using (FileStream archiveStream = File.OpenRead(archivePath))
                    {
                        archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            imgBuffer = ZBlock.Decompress(archiveStream, imgInfo.cprSize);
                        }
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

            if (getFileFormat() == ".dds")
                imgFile = new DDS(fileName, imgInfo.imgSize, texFormat, imgBuffer);
            else
                imgFile = new TGA(fileName, imgInfo.imgSize, texFormat, imgBuffer);

            byte[] saveImg = imgFile.ToArray();
            using (FileStream outputImg = new FileStream(imgFile.fileName, FileMode.Create, FileAccess.Write))
                outputImg.Write(saveImg, 0, saveImg.Length);
        }

        public void extractMaxImage(string archiveDir = null, string fileName = null)
        {
            // select max image size, excluding void images with offset = -1
            ImageSize maxImgSize = imgList.Where(img => img.offset != -1).Max(image => image.imgSize);
            // extracting max image
            extractImage(imgList.Find(img => img.imgSize == maxImgSize), archiveDir, fileName);
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
                case ".tga": imgFile = new TGA(fileToReplace, null); break;
                default: throw new FileFormatException(fileFormat + " image extension not supported");
            }

            // check if images have same format type
            if (texFormat != imgFile.format)
            {
                DialogResult selection = MessageBox.Show("Warning, replacing image has format " + imgFile.subtype() + " while original has " + texFormat + ", would you like to replace it anyway?", "Warning, different image format found", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (selection == DialogResult.Yes)
                    imgFile.format = texFormat;
                else
                    return;
                //throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());
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
                    string archivePath = archiveDir + "\\" + arcName + ".tfc";
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    if (getFileFormat() == ".tga")
                        imgBuffer = imgFile.resize(); // shrink image to essential data
                    else
                        imgBuffer = imgFile.imgData;

                    if (imgBuffer.Length != imgInfo.uncSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.uncSize + ", new is " + imgBuffer.Length);

                    using (FileStream archiveStream = new FileStream(archivePath, FileMode.Append, FileAccess.Write))
                    {
                        int newOffset = (int)archiveStream.Position;

                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            imgBuffer = ZBlock.Compress(imgBuffer);
                            /*byte[] compressed = ZBlock.Compress(imgBuffer);
                            archiveStream.Write(compressed, 0, compressed.Length);*/
                            imgInfo.cprSize = imgBuffer.Length;
                        }
                        //else
                        archiveStream.Write(imgBuffer, 0, imgBuffer.Length);

                        imgInfo.offset = newOffset;
                    }
                    break;

                case storage.pccSto:
                    imgBuffer = imgFile.imgData; // copy image data as-is
                    if (imgBuffer.Length != imgInfo.uncSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.uncSize + ", new is " + imgBuffer.Length);

                    using (MemoryStream dataStream = new MemoryStream(imageData))
                    {
                        dataStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                        dataStream.Write(imgBuffer, 0, imgBuffer.Length);
                    }

                    break;
            }

            imgList[imageIdx] = imgInfo;
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
                case ".tga": imgFile = new TGA(imagePathToAdd, null); break;
                default: throw new FileFormatException(fileFormat + " image extension not supported");
            }

            // check if image to add is valid
            if(biggerImageSizeOnList.width * 2 != imgFile.imgSize.width || biggerImageSizeOnList.height * 2 != imgFile.imgSize.height)
                throw new FormatException("image size " + imgFile.imgSize + " isn't valid, must be " + new ImageSize(biggerImageSizeOnList.width * 2,biggerImageSizeOnList.height * 2));

            // this check avoids insertion inside textures that have only 1 image stored inside pcc
            if(!imgList.Exists(img => img.storageType != storage.empty && img.storageType != storage.pccSto))
                throw new Exception("Unable to add image, texture must have a reference to an external archive");

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = imgList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
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

        public void OneImageToRuleThemAll(string imageFileName, string archiveDir, out string newTextureGroup)
        {
            newTextureGroup = null;
            ImageMipMapHandler imgMipMap = new ImageMipMapHandler(imageFileName, null);

            // starts from the smaller image
            for (int i = imgMipMap.imageList.Count - 1; i >= 0; i--)
            {
                ImageFile newImageFile = imgMipMap.imageList[i];

                // insert images only with size > 64
                if (newImageFile.imgSize.width < 64 && newImageFile.imgSize.height < 64)
                    continue;

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
                else // if the image doesn't exists then we have to add it
                {
                    // ...and use my addBiggerImage function! :P
                    addBiggerImage(newImageFile.fileName, archiveDir);
                }

                File.Delete(newImageFile.fileName);
            }

            // add texturegroup_world inside GamerSettings.ini in order to overwrite values
            ImageSize maxSize = imgList.Max(image => image.imgSize);
            uint maxValue = Math.Max(maxSize.width, maxSize.height);
            string section = "SystemSettings";
            string key = "texturegroup_shadowmap";
            string newValue = "(MinLODSize=128,MaxLODSize=" + maxValue + ",LODBias=0)";
            IniFile iniFile = new IniFile(ME3Directory.GamerSettingsIniFile);
            string oldValue = iniFile.IniReadValue(section, key);
            if (oldValue == "")
            {
                iniFile.IniWriteValue(section, key, newValue);
            }
            else
            {
                char[] delimiters = new char[] { '=', ',' };
                uint maxLODSize = Convert.ToUInt32(oldValue.Split(delimiters)[3]);
                if(maxValue > maxLODSize)
                    iniFile.IniWriteValue(section, key, newValue);
            }
            
            // check that Texture2D has a TextureGroup
            if (!properties.ContainsKey("LODGroup"))
                return;

            // extracting values from LODGroup Property
            PropertyReader.Property LODGroup = properties["LODGroup"];
            string textureGroupName = pccRef.Names[LODGroup.Value.IntValue];

            string newTextureGroupName = "TEXTUREGROUP_Shadowmap";
            textureGroupName = newTextureGroupName;
            if(!pccRef.Names.Exists(name => name == newTextureGroupName))
                pccRef.Names.Add(newTextureGroupName);
            using (MemoryStream rawStream = new MemoryStream(LODGroup.raw))
            {
                rawStream.Seek(32, SeekOrigin.Begin);
                rawStream.WriteValueS32(pccRef.Names.FindIndex(name => name == newTextureGroupName));
                //rawStream.Seek(32, SeekOrigin.Begin);
                rawStream.WriteValueS32(0);
                properties["LODGroup"].raw = rawStream.ToArray();
            }
            //LODGroup.Value.IntValue = pccRef.Names.FindIndex(name => name == newTextureGroupName);

            /*MessageBox.Show("Texturegroup Name: " + textureGroupName);
            ImageSize maxImageSize = imgList.Max(image => image.imgSize);
            int textureGroupValue = (int)Math.Max(maxImageSize.width, maxImageSize.height) + 1;

            // open Engine.pcc file and edit TextureGroup enumerator
            {
                PCCObject pccEngine = new PCCObject(ME3Directory.cookedPath + "Engine.pcc");
                int idxTexGroups = pccEngine.Exports.FindIndex(export => export.ObjectName == "TextureGroup");

                TextureGroup texGroup = new TextureGroup(pccEngine, pccEngine.Exports[idxTexGroups].Data);
                if (texGroup.ExistsTextureGroup(textureGroupName, textureGroupValue))
                    return;
                else
                {
                    if (!pccEngine.Names.Exists(name => name == newTextureGroupName))
                        pccEngine.Names.Add(newTextureGroupName);

                    newTextureGroup = textureGroupName + "_" + (textureGroupValue - 1);

                    texGroup.Add(textureGroupName, textureGroupValue);
                    MessageBox.Show("Now editing texgroup enum");
                    pccEngine.Exports[idxTexGroups].Data = texGroup.ToArray();
                    MessageBox.Show("Now saving engine.pcc");
                    pccEngine.saveToFile(true, ME3Directory.cookedPath + "Engine.pcc");
                    MessageBox.Show("Saved engine.pcc");


                }
            }*/
        }

        /*public bool increaseLODGroup(int newLODGroupValue, int subValue = 0)
        {
            if(!properties.ContainsKey("LODGroup"))
                throw new InvalidOperationException("This texture doesn't have a LODGroup property");
            PropertyReader.Property LODGroup = properties["LODGroup"];
            LODGroup.Value.IntValue = newLODGroupValue;
            //LODGroup.Value.Array[0].IntValue = subValue;
        }*/

        public List<PropertyReader.Property> getPropertyList()
        {
            List<PropertyReader.Property> propertyList = new List<PropertyReader.Property>();
            foreach (KeyValuePair<string, PropertyReader.Property> kvp in properties)
                propertyList.Add(kvp.Value);
            return propertyList;
        }

        public string getFileFormat()
        {
            switch (texFormat)
            {
                case "DXT1":
                case "DXT5":
                case "V8U8": return ".dds";
                case "G8":
                case "A8R8G8B8": return ".tga";
                default: throw new FormatException("Unknown ME3 texture format");
            }
        }

        public void removeImage()
        {
            //MessageBox.Show("1. Number of imgs = " + imgList.Count);
            imgList.RemoveAt(0);
            //MessageBox.Show("2. Number of imgs = " + imgList.Count);
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
            //MessageBox.Show("Init. width = " + imgList[0].imgSize.width);
            propVal = (int)imgList[0].imgSize.width;
            properties["SizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteValueS32(propVal);
                properties["SizeX"].raw = rawStream.ToArray();
            }
            //MessageBox.Show("Final width = " + imgList[0].imgSize.width);
            //properties["SizeX"] = Size;
            //Size = properties["SizeY"];
            //properties["SizeY"].Value.IntValue = (int)newImgInfo.imgSize.height;
            properties["SizeY"].Value.IntValue = (int)imgList[0].imgSize.height;
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
        }


    }
}
