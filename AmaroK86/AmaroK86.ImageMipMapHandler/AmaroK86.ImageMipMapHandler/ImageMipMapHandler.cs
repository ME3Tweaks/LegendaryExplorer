using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Drawing;
using System.Windows.Forms;

namespace AmaroK86.ImageFormat
{
    public class ImageMipMapHandler
    {
        public List<ImageFile> imageList { get; private set; }

        public ImageMipMapHandler(string imageWithMipMaps, byte[] data)
        {
            imageWithMipMaps = (imageWithMipMaps == "") ? ".dds" : imageWithMipMaps;
            imageList = new List<ImageFile>();
            string fileFormat = Path.GetExtension(imageWithMipMaps).ToLowerInvariant();
            string fileName = Path.GetFileNameWithoutExtension(imageWithMipMaps);
            ImageFile imageMipMap;
            int headerSize;

            if (data != null)
            {
                headerSize = 128;
                imageMipMap = new DDS("", data);
            }
            else
            {
                switch (fileFormat)
                {
                    case ".dds":
                        headerSize = 128;
                        imageMipMap = new DDS(imageWithMipMaps, null);
                        break;
                    case ".tga":
                        headerSize = 18;
                        imageMipMap = new TGA(imageWithMipMaps, null);
                        break;
                    default: throw new FormatException("Invalid image format");
                }
            }

            //Console.WriteLine("Image Format: {0}", imageMipMap.format);

            //check if image has mipmaps
            /* using (FileStream imageStream = File.OpenRead(imageWithMipMaps))
            {
                long size = ImageMipMapDataSize(imageMipMap.imgSize, CprFormat(imageMipMap.format), imageMipMap.BPP);
                if (imageStream.Length - headerSize != ImageMipMapDataSize(imageMipMap.imgSize, CprFormat(imageMipMap.format), imageMipMap.BPP))
                {
                    //MessageBox.Show("bytes in file: " + (imageStream.Length - headerSize) + ", bytes calulated: " + ImageMipMapDataSize(imageMipMap.imgSize, imageMipMap.format, imageMipMap.BPP) + ", BPP: " + imageMipMap.BPP + ", format: " + imageMipMap.format);
                    //Console.WriteLine("bytes in file: {0}, bytes calulated: {1}, BPP: {2}", imageStream.Length - headerSize, ImageMipMapDataSize(imageMipMap.imgSize, imageMipMap.format, imageMipMap.BPP), imageMipMap.BPP);
                    throw new FormatException("The image doesn't have any mipmaps");
                }
            } */
            if (imageMipMap.imgData.Length != ImageMipMapDataSize(imageMipMap.imgSize, CprFormat(imageMipMap.format), imageMipMap.BPP))
                throw new FormatException("The image doesn't have any mipmaps");

            byte[] buffer = null;

            // add the first tga image
            if (fileFormat == ".tga")
            {
                //buffer = new byte[imageMipMap.imgData.Length];
                buffer = imageMipMap.imgData;
                //imageList.Add(imageMipMap);
            }

            int maxCount = (int)Math.Min(imageMipMap.imgSize.width, imageMipMap.imgSize.height);
            int count = 1;
            int imgDataPos = 0;
            while (count <= maxCount)
            {
                ImageFile newImageFile;
                ImageSize newImageSize = imageMipMap.imgSize / count;
                //if (newImageSize.width < 4 || newImageSize.height < 4)
                //    break;
                int imgDataSize = (int)ImageDataSize(newImageSize, imageMipMap.format, imageMipMap.BPP);

                if (fileFormat == ".dds")
                {
                    buffer = new byte[imgDataSize];
                    Buffer.BlockCopy(imageMipMap.imgData, imgDataPos, buffer, 0, imgDataSize);
                    imgDataPos += imgDataSize;

                    if (imageMipMap.format == "R8G8B8") // Automatic conversion to 32-bit
                    {
                        buffer = ConvertTo32bit(buffer, (int)newImageSize.width, (int)newImageSize.height);
                        newImageFile = new DDS(fileName + "_" + newImageSize + fileFormat, newImageSize, "A8R8G8B8", buffer);
                    }
                    else
                        newImageFile = new DDS(fileName + "_" + newImageSize + fileFormat, newImageSize, imageMipMap.format, buffer);
                }
                else if (fileFormat == ".tga")
                {
                    newImageFile = new TGA(fileName + "_" + newImageSize + fileFormat, newImageSize, imageMipMap.format, buffer);
                    if (newImageSize != new ImageSize(1, 1))
                        buffer = ShrinkImage(buffer, newImageSize, imageMipMap.BPP);
                }
                else
                    throw new FormatException("Invalid image format");

                imageList.Add(newImageFile);

                count *= 2;
            }

        }

        public void SaveAll()
        {
            foreach (ImageFile imgFile in imageList)
            {
                using (FileStream imgStream = File.OpenWrite(imgFile.fileName))
                {
                    byte[] buffer = imgFile.ToArray();
                    imgStream.Write(buffer, 0, buffer.Length);
                }
            }
        }

        private long ImageDataSize(ImageSize imgsize, string format, float BytesPerPixel)
        {
            uint w = imgsize.width;
            uint h = imgsize.height;
            if (CprFormat(format))
            {
                if (w < 4)
                    w = 4;
                if (h < 4)
                    h = 4;
            }
            return (long)((float)(w * h) * BytesPerPixel);

            /*long totalBytes = (long)((float)(imgsize.width * imgsize.height) * BytesPerPixel);
            switch (format)
            {
                case "DXT1":
                    if (imgsize.width <= 4 && imgsize.height <= 4)
                        return 8;
                    else
                        return totalBytes;
                case "ATI2":
                case "DXT5":
                    if (imgsize.width <= 4 && imgsize.height <= 4)
                        return 16;
                    else
                        return totalBytes;
                case "V8U8":
                    if (imgsize.width <= 4 && imgsize.height <= 4)
                        return 32;
                    else
                        return totalBytes;
                case "G8":
                case "R8G8B8":
                case "A8R8G8B8":
                    return totalBytes;
                default:
                    throw new FormatException("Invalid image format");
            } */
        }

        private long ImageMipMapDataSize(ImageSize imgsize, bool compressed, float BytesPerPixel)
        {
            uint w = imgsize.width;
            uint h = imgsize.height;
            if (compressed)
            {
                if (w < 4)
                    w = 4;
                if (h < 4)
                    h = 4;
            }
            long totalBytes = (long)((float)(w * h) * BytesPerPixel);
            w = imgsize.width;
            h = imgsize.height;
            if (w == 1 && h == 1)
                return totalBytes;
            if (w != 1)
                w = imgsize.width / 2;
            if (h != 1)
                h = imgsize.height / 2;
            return totalBytes + ImageMipMapDataSize(new ImageSize(w, h), compressed, BytesPerPixel);
        }

        public static bool CprFormat(string format)
        {
            switch (format)
            {
                case "DXT1":
                case "DXT5":
                case "ATI2": return true;
                case "V8U8":
                case "A8R8G8B8":
                case "G8":
                case "R8G8B8": return false;
                default: throw new FormatException("Unknown Format");
            }
        }

        private byte[] ShrinkImage(byte[] imageData, ImageSize imgsize, float BytesPerPixel)
        {
            //Console.WriteLine("image length: {0}, image size: {1}, BPP: {2}", imageData.Length, imgsize, BytesPerPixel);
            byte[] final = new byte[imageData.Length / 4];
            int BPP = (int)BytesPerPixel;

            for (int i = 0; i < imgsize.height; i += 2)
            {
                for (int j = 0; j < imgsize.width; j += 2)
                {
                    for (int k = 0; k < BPP; k++)
                    {
                        final[(i * (imgsize.width * BPP) / 4) + j * BPP / 2 + k] = (byte)
                          (((int)imageData[i * (imgsize.width * BPP) + j * BPP + k] +
                            (int)imageData[i * (imgsize.width * BPP) + j * BPP + BPP + k] +
                            (int)imageData[(i + 1) * (imgsize.width * BPP) + j * BPP + k] +
                            (int)imageData[(i + 1) * (imgsize.width * BPP) + j * BPP + BPP + k]) / 4);
                    }
                }
            }

            return final;
        }

        public static byte[] ConvertTo32bit(byte[] buff, int w, int h)
        {
            if (buff.Length != (w * h * 3))
                throw new ArgumentException("Buffer length is not equivalent to a 24-bit texture length");
            byte[] val = new byte[w * h * 4];

            for (int i = 0; i < w * h; i++)
            {
                Array.Copy(buff, i * 3, val, i * 4, 3);
                val[(i * 4) + 3] = 0xFF;
            }

            return val;
        }
    }
}
