using AmaroK86.ImageFormat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.IO;
using System.Drawing;
using DDSPreview = KFreonLib.Textures.SaltDDSPreview.DDSPreview;
using System.Drawing.Drawing2D;
using SaltTPF;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KFreonLib.PCCObjects;
using KFreonLib.Helpers.LiquidEngine;
using KFreonLib.Debugging;
using ResILWrapper;

namespace KFreonLib.Textures
{
    #region Old ImageEngine stuff
    /// <summary>
    /// Provides an object oriented way to interact with the ImageEngine.
    /// </summary>
    /*public class KFreonImage : IDisposable
    {
        // KFreon: Get DevIL image ID
        DevIL.Unmanaged.ImageID ID = ImageEngine.GenerateImage();
        public string Format { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        private bool isV8U8
        {
            get
            {
                return Format.ToUpperInvariant() == "V8U8";
            }
        }
        public int Mips
        {
            get
            {
                if (V8U8Mips != null)
                    return V8U8Mips.Count();
                else
                    return ImageEngine.GetNumMips(ID);
            }
        }

        public ImageEngine.MipMap[] V8U8Mips;


        /// <summary>
        /// Constructor with filename.
        /// </summary>
        /// <param name="filename">File to load as image.</param>
        public KFreonImage(string filename)
        {
            // KFreon: Check if already disposed
            if (ID == 0)
                throw new ObjectDisposedException("CurrentImage");

            // KFreon: Load image and set its surface format and extension
            int width;
            int height;
            Format = ImageEngine.LoadImage(ID, filename, out width, out height, ref V8U8Mips);
            Width = width;
            Height = height;
        }


        /// <summary>
        /// Constructor for stream.
        /// </summary>
        /// <param name="stream">Stream to load image from.</param>
        public KFreonImage(MemoryStream stream)
        {
            // KFreon: Check if disposed
            if (ID == 0)
                throw new ObjectDisposedException("CurrentImage");

            // KFreon: Load image and set surface format and set default save extension as .dds
            int width;
            int height;
            MemoryTributary ImgStream = new MemoryTributary();
            ImgStream.CopyTo(stream);
            Format = ImageEngine.LoadImage(ID, ImgStream, out width, out height, ref V8U8Mips);
            Width = width;
            Height = height;
        }


        /// <summary>
        /// Constructor for byte[] of data.
        /// </summary>
        /// <param name="imgData">Array of data to load image from.</param>
        public KFreonImage(byte[] imgData)
        {
            // KFreon: Check if disposed
            if (ID == 0)
                throw new ObjectDisposedException("CurrentImage");

            // KFreon: Load image and set surface format and set default save extension to .dds
            int width;
            int height;
            MemoryTributary stream = new MemoryTributary(imgData);
            Format = ImageEngine.LoadImage(ID, stream, out width, out height, ref V8U8Mips);
            Width = width;
            Height = height;
        }

        public Bitmap ToBitmap(int width = -1, int height = -1)
        {
            Bitmap bmp = null;
            /*if (ImgStream != null)
            {
                bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                {
                    BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0,
                                                        bmp.Width,
                                                        bmp.Height),
                                          ImageLockMode.WriteOnly,
                                          bmp.PixelFormat);

                    byte[] data = ImgStream.ToArray();
                    Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
                    bmp.UnlockBits(bmpData);
                }
            }
            else*/


            /*bmp = (Bitmap)Image.FromStream(ImageEngine.ToBitmap(ID));

            return bmp;
        }


        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            // KFreon: Check if already disposed.
            if (ID == 0)
                throw new ObjectDisposedException("CurrentImage");

            // KFreon: Delete image.
            ImageEngine.Delete(ID);
        }


        /// <summary>
        /// Saves image with currently set extension and surface format (if dds)
        /// </summary>
        /// <param name="savepath">Path to save image to.</param>
        public void Save(string savepath, bool mips)
        {
            // KFreon: Check if disposed
            if (ID == 0)
                throw new ObjectDisposedException("CurrentImage");
            ImageEngine.ConvertandSave(savepath, ID, Format, Height, Width, mips);
        }


        /// <summary>
        /// Set surface format of current image. Returns true if valid change.
        /// </summary>
        /// <param name="newFormat">Format to change to.</param>
        /// <returns>True if newformat is a valid surface format.</returns>
        public bool ChangeFormat(string newFormat)
        {
            if (ImageEngine.isValidFormat(newFormat))
                Format = newFormat;
            else
                return false;

            return true;
        }
    }*/


    /// <summary>
    /// Provides image loading methods. Predominantly for DDS images as .NET supports everything else.
    /// Should be threadsafe, but note that engine itself is single threaded. i.e. Each function locks the others out. 
    /// </summary>
    /*public static class ImageEngine
    {
        static readonly object locker = new object();
        static bool initialised = false;

        // KFreon: Valid formats
        public readonly static List<string> ValidFormats = new List<string> { "DXT1", "DXT3", "DXT5", "3DC", "ATI2N", "V8U8", "JPG", "PNG", "BMP", "GIF" };
        private readonly static List<string> Extensions = new List<string> { ".dds", ".jpg", ".png", ".bmp", ".gif" };


        public static int GetNumMips(int ID)
        {
            lock (locker)
            {
                DevIL.Unmanaged.IL.BindImage(ID);
                DevIL.Unmanaged.ImageInfo info = DevIL.Unmanaged.IL.GetImageInfo();
                return info.MipMapCount;
            }
        }


        public static void Shutdown()
        {
            lock (locker)
                DevIL.Unmanaged.IL.Shutdown();
        }


        public static int GenerateImage()
        {
            lock (locker)
            {
                Initialise();
                return DevIL.Unmanaged.IL.GenerateImage();
            }
        }


        private static bool? CheckIfV8U8(string file = null, byte[] data = null)
        {
            if (data == null && file == null)
                return null;

            bool retval = false;
            try
            {
                DDSPreview dds = new DDSPreview(data == null ? File.ReadAllBytes(file) : data);
                retval = dds.FormatString == "V8U8";
                //DevIL.Unmanaged.ILU.
            }
            catch
            {
                // Ignore
            }
            return retval;
        }


        /// <summary>
        /// Checks if given extension is valid.
        /// </summary>
        /// <param name="ext">Extension to check.</param>
        /// <returns>True if valid.</returns>
        public static bool isValidExtension(string ext)
        {
            return Extensions.Contains(ext.ToLowerInvariant());
        }


        /// <summary>
        /// Checks if given format is valid.
        /// </summary>
        /// <param name="format">Format to check.</param>
        /// <returns>True if valid.</returns>
        public static bool isValidFormat(string format)
        {
            return ValidFormats.Contains(format.ToUpperInvariant());
        }


        /// <summary>
        /// Initialises DevIL subsystems.
        /// </summary>
        static void Initialise()
        {
            lock (locker)
                if (!initialised)
                {
                    DevIL.Unmanaged.IL.Initialize();
                    DevIL.Unmanaged.ILU.Initialize();
                    initialised = true;
                }
        }


        /// <summary>
        /// Loads an image into a given ID from filename.
        /// </summary>
        /// <param name="ID">ID of image loaded.</param>
        /// <param name="filename">File to load image from.</param>
        /// <returns>Surface format of image. Null if not DDS.</returns>
        public static string LoadImage(int ID, string filename, out int width, out int height, ref MipMap[] V8U8mipMaps)
        {
            lock (locker)
            {
                Initialise();
                width = -1;
                height = -1;

                DevIL.Unmanaged.IL.BindImage(ID);

                // KFreon: V8U8 images
                if (CheckIfV8U8(filename) == true)
                {
                    LoadV8U8(filename, out width, out height, out V8U8mipMaps);
                    return "V8U8";
                }
                else
                {
                    // KFreon: Other images
                    DevIL.Unmanaged.IL.LoadImage(filename);
                    return GetSurfaceFormat(DevIL.Unmanaged.IL.GetDxtcFormat());
                }
            }
        }

        public static DevIL.Unmanaged.ImageInfo GetImageInfo(int ID)
        {
            lock (locker)
            {
                DevIL.Unmanaged.IL.BindImage(ID);
                return DevIL.Unmanaged.IL.GetImageInfo();
            }
        }

        public static MemoryStream ToBitmap(int ID)
        {
            MemoryStream stream = new MemoryStream();
            lock (locker)
            {
                DevIL.Unmanaged.IL.BindImage(ID);
                DevIL.Unmanaged.IL.SaveImageToStream(DevIL.ImageType.Bmp, stream);
            }
            return stream;
        }


        /// <summary>
        /// Converts given surface format to a string.
        /// </summary>
        /// <param name="format">Surface format to convert to string.</param>
        /// <returns>Surface format as string.</returns>
        private static string GetSurfaceFormat(DevIL.CompressedDataFormat format)
        {
            string retval = null;
            switch (format)
            {
                case DevIL.CompressedDataFormat.ThreeDC:
                    retval = "3Dc/ATI2N";
                    break;
                case DevIL.CompressedDataFormat.DXT5:
                    retval = "DXT5";
                    break;
                case DevIL.CompressedDataFormat.DXT3:
                    retval = "DXT3";
                    break;
                case DevIL.CompressedDataFormat.DXT1:
                    retval = "DXT1";
                    break;
            }
            return retval;
        }


        /// <summary>
        /// Load image from stream.
        /// </summary>
        /// <param name="ID">ID to load image into.</param>
        /// <param name="stream">Stream to load image from.</param>
        /// <returns>Surface format of loaded image. Null if not a DDS.</returns>
        public static string LoadImage(int ID, MemoryTributary stream, out int width, out int height, ref MipMap[] V8U8mipMaps)
        {
            lock (locker)
            {
                width = -1;
                height = -1;

                // KFreon: Initialise if necessary
                Initialise();

                // KFreon: V8U8
                if (CheckIfV8U8(null, stream.ToArray()) == true)
                {
                    LoadV8U8(stream, out width, out height, out V8U8mipMaps);
                    return "V8U8";
                }
                else
                {
                    // KFreon: All other images
                    DevIL.Unmanaged.IL.BindImage(ID);
                    DevIL.Unmanaged.IL.LoadImageFromStream(stream);
                    return GetSurfaceFormat(DevIL.Unmanaged.IL.GetDxtcFormat());
                }
            }
        }


        /// <summary>
        /// Remove image from DevIL image manangement.
        /// </summary>
        /// <param name="ID">ID to delete.</param>
        public static void Delete(int ID)
        {
            lock (locker)
                DevIL.Unmanaged.IL.DeleteImage(ID);
        }


        private static string GetExtension(string format)
        {
            string retval = null;
            switch (format.ToLowerInvariant())
            {
                case "dxt1":
                case "dxt3":
                case "dxt5":
                case "3dc":
                case "ati2":
                    retval = ".dds";
                    break;
                default:
                    retval = "." + format.ToLowerInvariant();
                    break;
            }
            return retval;
        }


        /// <summary>
        /// Converts and saves an image. Must be done at same time as save formats are global settings.
        /// </summary>
        /// <param name="savepath">Path to save image to.</param>
        /// <param name="ID">ID of image to save.</param>
        /// <param name="format">Surface format of image to save. Only valid if DDS.</param>
        /// <returns>True if passed all valid parameters.</returns>
        public static bool ConvertandSave(string savepath, int ID, string format, int height, int width, bool Mips)
        {
            lock (locker)
            {
                bool retval = true;
                int numMips = 0;
                if (Mips)
                {
                    int determiningDimension = width > height ? height : width;
                    numMips = (int)Math.Log(determiningDimension, 2);
                }

                DevIL.Unmanaged.IL.BindImage(ID);

                // KFreon: Convert to V8U8
                if (format == "V8U8")
                {
                    MemoryTributary stream = new MemoryTributary(DevIL.Unmanaged.IL.GetDxtcData(DevIL.CompressedDataFormat.None));
                    WriteV8U8(stream, savepath, height, width, numMips);
                }
                else
                {
                    // KFreon: Get extension
                    string extension = GetExtension(format);

                    // KFreon: Set output surface format if necessary
                    DevIL.CompressedDataFormat mat;
                    if (extension == ".dds")
                    {
                        if (!ParseFormat(format, out mat))
                            retval = false;
                        else
                            DevIL.Unmanaged.IL.SetDxtcFormat(mat);
                    }


                    // KFreon: Get imagetype
                    DevIL.ImageType type = DevIL.ImageType.Dds;
                    if (!ParseType(extension, out type))
                        retval = false;

                    // KFreon: Build mipmaps if requested
                    DevIL.Unmanaged.ILU.BuildMipMaps();

                    // KFreon: Save if everything worked
                    if (retval)
                        DevIL.Unmanaged.IL.SaveImage(type, savepath);
                }

                return retval;
            }
        }


        /// <summary>
        /// Get ImageType from string.
        /// </summary>
        /// <param name="imgType">Image extension to parse.</param>
        /// <param name="type">OUT: ImageType enum.</param>
        /// <returns>True if successful.</returns>
        private static bool ParseType(string imgType, out DevIL.ImageType type)
        {
            bool retval = true;
            type = DevIL.ImageType.Dds;
            switch (imgType.ToLowerInvariant())
            {
                case ".dds":
                    type = DevIL.ImageType.Dds;
                    break;
                case ".bmp":
                    type = DevIL.ImageType.Bmp;
                    break;
                case ".jpg":
                case ".jpeg":
                    type = DevIL.ImageType.Jpg;
                    break;
                case ".png":
                    type = DevIL.ImageType.Png;
                    break;
                case ".gif":
                    type = DevIL.ImageType.Gif;
                    break;
                default:
                    retval = false;
                    break;
            }
            return retval;
        }


        /// <summary>
        /// Gets surface format from string.
        /// </summary>
        /// <param name="format">Surface format string to parse.</param>
        /// <param name="mat">OUT: Surface format enum.</param>
        /// <returns>True if successful.</returns>
        private static bool ParseFormat(string format, out DevIL.CompressedDataFormat mat)
        {
            bool retval = true;
            mat = DevIL.CompressedDataFormat.None;

            // KFreon: Find format
            switch (format.ToUpperInvariant())
            {
                case "DXT1":
                    mat = DevIL.CompressedDataFormat.DXT1;
                    break;
                case "DXT3":
                    mat = DevIL.CompressedDataFormat.DXT3;
                    break;
                case "DXT5":
                    mat = DevIL.CompressedDataFormat.DXT5;
                    break;
                case "3DC":
                case "ATI2N":
                case "3DC/ATI2N":
                    mat = DevIL.CompressedDataFormat.ThreeDC;
                    break;
                default:
                    // KFreon: Default failure
                    retval = false;
                    break;
            }
            return retval;
        }


        // KFreon: Most of this stuff is from DDSImage.cs from this project, and the original at http://code.google.com/p/kprojects/
        #region V8U8 Stuff
        /// <summary>
        /// Reads DDS header from file.
        /// </summary>
        /// <param name="h">Header struct.</param>
        /// <param name="r">File reader.</param>
        private static void Read_DDS_HEADER(DDS_HEADER h, BinaryReader r)
        {
            h.dwSize = r.ReadInt32();
            h.dwFlags = r.ReadInt32();
            h.dwHeight = r.ReadInt32();
            h.dwWidth = r.ReadInt32();
            h.dwPitchOrLinearSize = r.ReadInt32();
            h.dwDepth = r.ReadInt32();
            h.dwMipMapCount = r.ReadInt32();
            for (int i = 0; i < 11; ++i)
            {
                h.dwReserved1[i] = r.ReadInt32();
            }
            Read_DDS_PIXELFORMAT(h.ddspf, r);
            h.dwCaps = r.ReadInt32();
            h.dwCaps2 = r.ReadInt32();
            h.dwCaps3 = r.ReadInt32();
            h.dwCaps4 = r.ReadInt32();
            h.dwReserved2 = r.ReadInt32();
        }


        /// <summary>
        /// Reads DDS pixel format.
        /// </summary>
        /// <param name="p">Pixel format struct.</param>
        /// <param name="r">File reader.</param>
        private static void Read_DDS_PIXELFORMAT(DDS_PIXELFORMAT p, BinaryReader r)
        {
            p.dwSize = r.ReadInt32();
            p.dwFlags = r.ReadInt32();
            p.dwFourCC = r.ReadInt32();
            p.dwRGBBitCount = r.ReadInt32();
            p.dwRBitMask = r.ReadInt32();
            p.dwGBitMask = r.ReadInt32();
            p.dwBBitMask = r.ReadInt32();
            p.dwABitMask = r.ReadInt32();
        }


        /// <summary>
        /// Get V8U8 width and height.
        /// </summary>
        /// <param name="ddsFileName">File to load.</param>
        /// <param name="width">OUT: Width of largest mip.</param>
        /// <param name="height">OUT: Height of largest mip.</param>
        private static void SetupV8U8(string ddsFileName, out int width, out int height, out MipMap[] mipMaps)
        {
            using (FileStream ddsStream = File.OpenRead(ddsFileName))
                SetupV8U8(ddsStream, out width, out height, out mipMaps);
        }


        /// <summary>
        /// Gets V8U8 width and height.
        /// </summary>
        /// <param name="ddsStream">Stream of DDS data.</param>
        /// <param name="width">OUT: Width of largest mip.</param>
        /// <param name="height">OUT: Height of largest mip.</param>
        private static void SetupV8U8(Stream ddsStream, out int width, out int height, out MipMap[] mipMaps)
        {
            width = -1;
            height = -1;
            using (BinaryReader r = new BinaryReader(ddsStream))
            {
                int dwMagic = r.ReadInt32();
                if (dwMagic != 0x20534444)
                {
                    throw new Exception("This is not a DDS!");
                }

                DDS_HEADER header = new DDS_HEADER();
                Read_DDS_HEADER(header, r);

                if (((header.ddspf.dwFlags & 0x00000004) != 0) && (header.ddspf.dwFourCC == 0x30315844 /*DX10*//*))
                {
                    throw new Exception("DX10 not supported yet!");
                }

                int mipMapCount = 1;
                if ((header.dwFlags & 0x00020000) != 0)
                    mipMapCount = header.dwMipMapCount;

                int w = 0;
                int h = 0;

                double bytePerPixel = 2;
                mipMaps = new MipMap[mipMapCount];

                // KFreon: Get mips
                for (int i = 0; i < mipMapCount; i++)
                {
                    w = (int)(header.dwWidth / Math.Pow(2, i));
                    h = (int)(header.dwHeight / Math.Pow(2, i));

                    // KFreon: Set max image size
                    if (i == 0)
                    {
                        width = w;
                        height = h;
                    }

                    int mipMapBytes = (int)(w * h * bytePerPixel);
                    mipMaps[i] = new MipMap(r.ReadBytes(mipMapBytes), DDSFormat.V8U8, w, h);
                }
            }
        }

        public class MipMap
        {
            public int width;
            public int height;
            DDSFormat ddsFormat;
            private byte[] _data;
            public byte[] data
            {
                get
                {
                    return _data;
                }
                set
                {
                    _data = value;
                }
            }


            public MipMap(byte[] data, DDSFormat format, int w, int h)
            {
                long requiredSize = (long)(w * h * 2);
                if (data.Length != requiredSize)
                    throw new InvalidDataException("Data size is not valid for selected format.\nActual: " + data.Length + " bytes\nRequired: " + requiredSize + " bytes");

                this.data = data;
                ddsFormat = format;
                width = w;
                height = h;
            }
        }

        private static DDS_HEADER Get_V8U8_DDS_Header(int Mips, int height, int width)
        {
            DDS_HEADER header = new DDS_HEADER();
            header.dwSize = 124;
            header.dwFlags = 0x1 | 0x2 | 0x4 | 0x1000 | (Mips != 0 ? 0x20000 : 0x0);  // Flags to denote valid fields: DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_MIPMAPCOUNT
            header.dwWidth = width;
            header.dwHeight = height;
            header.dwCaps = 0x1000 | 0x8 | (Mips == 0 ? 0 : 0x400000);
            header.dwMipMapCount = Mips == 0 ? 1 : Mips;
            //header.dwPitchOrLinearSize = ((width + 1) >> 1)*4;

            DDS_PIXELFORMAT px = new DDS_PIXELFORMAT();
            px.dwSize = 32;
            px.dwFlags = 0x200;
            px.dwRGBBitCount = 16;
            px.dwRBitMask = 255;
            px.dwGBitMask = 0x0000FF00;

            header.ddspf = px;
            return header;
        }

        private static void Write_V8U8_DDS_Header(DDS_HEADER header, BinaryWriter writer)
        {
            // KFreon: Write magic number ("DDS")
            writer.Write(0x20534444);

            // KFreon: Write all header fields regardless of filled or not
            writer.Write(header.dwSize);
            writer.Write(header.dwFlags);
            writer.Write(header.dwHeight);
            writer.Write(header.dwWidth);
            writer.Write(header.dwPitchOrLinearSize);
            writer.Write(header.dwDepth);
            writer.Write(header.dwMipMapCount);

            // KFreon: Write reserved1
            for (int i = 0; i < 11; i++)
                writer.Write(0);

            // KFreon: Write PIXELFORMAT
            DDS_PIXELFORMAT px = header.ddspf;
            writer.Write(px.dwSize);
            writer.Write(px.dwFlags);
            writer.Write(px.dwFourCC);
            writer.Write(px.dwRGBBitCount);
            writer.Write(px.dwRBitMask);
            writer.Write(px.dwGBitMask);
            writer.Write(px.dwBBitMask);
            writer.Write(px.dwABitMask);

            writer.Write(header.dwCaps);
            writer.Write(header.dwCaps2);
            writer.Write(header.dwCaps3);
            writer.Write(header.dwCaps4);
            writer.Write(header.dwReserved2);
        }

        private static void ReadV8U8(int w, int h, byte[] ImageData)
        {
            MemoryStream bitmapStream = new MemoryStream(w * h * 2);
            BinaryWriter bitmapBW = new BinaryWriter(bitmapStream);

            int ptr = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    sbyte red = (sbyte)Buffer.GetByte(ImageData, ptr++);
                    sbyte green = (sbyte)Buffer.GetByte(ImageData, ptr++);
                    byte blue = 0xFF;

                    int fCol = blue | (0x7F + green) << 8 | (0x7F + red) << 16 | 0xFF << 24;
                    bitmapBW.Write(fCol);
                }
            }

            byte[] imageData = bitmapStream.ToArray();



            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0,
                                                    bmp.Width,
                                                    bmp.Height),
                                      ImageLockMode.WriteOnly,
                                      bmp.PixelFormat);

                Marshal.Copy(imageData, 0, bmpData.Scan0, imageData.Length);
                bmp.UnlockBits(bmpData);
            }

            MemoryStream ms = new MemoryStream();
            bmp.Save("D:\\test.jpg");
            bmp.Dispose();
            System.Threading.Thread.Sleep(1000);
            DevIL.Unmanaged.IL.LoadImage("D:\\test.jpg");
            DevIL.Unmanaged.IL.SetJpgSaveFormat(DevIL.JpgSaveFormat.Jpg);
            DevIL.Unmanaged.IL.SaveImage("D:\\test2.jpg");
            MemoryStream news = new MemoryStream();
            bool te = DevIL.Unmanaged.IL.SaveImageToStream(DevIL.ImageType.Bmp, news);
            Bitmap kjsd = new Bitmap(news);
        }

        private static void WriteV8U8(MemoryTributary stream, string savepath, int height, int width, int Mips)
        {
            using (FileStream fs = new FileStream(savepath, FileMode.CreateNew))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    // KFreon: Get and write header
                    DDS_HEADER header = Get_V8U8_DDS_Header(Mips, height, width);
                    Write_V8U8_DDS_Header(header, writer);
                    stream.Seek(0, SeekOrigin.Begin);

                    // KFreon: Write data ATM ONLY 1 IMAGE i.e. no mips
                    for (int h = 0; h < height; h++)
                    {
                        for (int w = 0; w < width; w++)
                        {
                            sbyte red = (sbyte)stream.ReadByte();
                            sbyte green = (sbyte)stream.ReadByte();
                            byte blue = 0xFF;

                            int fCol = blue | (0x7F + green) >> 8 | (0x7F + red) >> 16 | 0xFF >> 24;
                            writer.Write(fCol);
                        }
                    }
                }
            }
        }

        private static void LoadV8U8(string filename, out int width, out int height, out MipMap[] mipMaps)
        {
            width = -1;
            height = -1;
            SetupV8U8(filename, out width, out height, out mipMaps);
            ReadV8U8(width, height, mipMaps[0].data);
        }

        private static void LoadV8U8(MemoryTributary stream, out int width, out int height, out MipMap[] mipMaps)
        {
            height = -1;
            width = -1;
            SetupV8U8(stream, out width, out height, out mipMaps);
            ReadV8U8(width, height, mipMaps[0].data);
        }
        #endregion
    }*/
    #endregion

    /// <summary>
    /// Provides methods related to textures.
    /// </summary>
    public static class Methods
    {
        #region HASHES
        /// <summary>
        /// Finds hash from texture name given list of PCC's and ExpID's.
        /// </summary>
        /// <param name="name">Name of texture.</param>
        /// <param name="Files">List of PCC's to search with.</param>
        /// <param name="IDs">List of ExpID's to search with.</param>
        /// <param name="TreeTexes">List of tree textures to search through.</param>
        /// <returns>Hash if found, else 0.</returns>
        public static uint FindHashByName(string name, List<string> Files, List<int> IDs, List<TreeTexInfo> TreeTexes)
        {
            foreach (TreeTexInfo tex in TreeTexes)
                if (name == tex.TexName)
                    for (int i = 0; i < Files.Count; i++)
                        for (int j = 0; j < tex.Files.Count; j++)
                            if (tex.Files[j].Contains(Files[i].Replace("\\\\", "\\")))
                                if (tex.ExpIDs[j] == IDs[i])
                                    return tex.Hash;
            return 0;
        }


        /// <summary>
        /// Returns a uint of a hash in string format. 
        /// </summary>
        /// <param name="line">String containing hash in texmod log format of name|0xhash.</param>
        /// <returns>Hash as a uint.</returns>
        public static uint FormatTexmodHashAsUint(string line)
        {
            return uint.Parse(line.Split('|')[0].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
        }


        /// <summary>
        /// Returns hash as a string in the 0xhash format.
        /// </summary>
        /// <param name="hash">Hash as a uint.</param>
        /// <returns>Hash as a string.</returns>
        public static string FormatTexmodHashAsString(uint hash)
        {
            return "0x" + System.Convert.ToString(hash, 16).PadLeft(8, '0').ToUpper();
        }
        #endregion

        #region Images

        /// <summary>
        /// Gets external image data as byte[] with some buffering i.e. retries if fails up to 20 times.
        /// </summary>
        /// <param name="file">File to get data from.</param>
        /// <returns>byte[] of image.</returns>
        public static byte[] GetExternalData(string file)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    // KFreon: Try readng file to byte[]
                    return File.ReadAllBytes(file);
                }
                catch
                {
                    // KFreon: Sleep for a bit and try again
                    System.Threading.Thread.Sleep(300);
                }
            }
            return null;
        }


        
        /// <summary>
        /// GONNA BE REPLACED!
        /// </summary>
        /// <param name="tmpTex"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [Obsolete("Use BetterDDSCheck instead.")]
        public static Bitmap DDSCheck(abstractTexInfo tmpTex, byte[] data)
        {
            DDSPreview dds = new DDSPreview(data);
            tmpTex.NumMips = (int)dds.NumMips;
            tmpTex.Format = dds.FormatString;
            byte[] datat = dds.GetMipData();
            Bitmap retval = DDSImage.ToBitmap(datat, (dds.FormatString == "G8") ? DDSFormat.G8 : dds.Format, (int)dds.Width, (int)dds.Height);
            dds = null;
            return retval;
        }

        public static Bitmap BetterDDSCheck(abstractTexInfo tmpTex, byte[] imgData)
        {
            Bitmap retval = null;
            using (ResILImage img = new ResILImage(imgData))
            {
                tmpTex.NumMips = img.Mips;
                tmpTex.Format = img.SurfaceFormat.ToString();

                if (tmpTex.Format == "None" && img.MemoryFormat == ResIL.Unmanaged.DataFormat.RGBA)
                    tmpTex.Format = "ARGB";

                
                try
                {
                    retval = new Bitmap(new MemoryStream(img.ToArray(ResIL.Unmanaged.ImageType.Jpg)));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                } 
            }
            
            return retval;
        }


        /// <summary>
        /// Check that texture format is what we want.  PROBABLY GONNA BE REPLACED!
        /// </summary>
        /// <param name="newtexture">Path to new texture file.</param>
        /// <param name="desiredformat">Format retrieved from tree, to which newtexture must conform.</param>
        /// <returns>Format of new texture as string, empty string if not correct, BORKED if something broke.</returns>
        public static bool CheckTextureFormat(byte[] data, string desiredformat, out string format)
        {
            format = null;
            try
            {
                // KFreon: Load image to DDS to test its format
                DDSPreview dds = new DDSPreview(data);
                format = dds.FormatString;
                return CheckTextureFormat(format, desiredformat);
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckTextureFormat(string currentFormat, string desiredFormat)
        {
            string curr = currentFormat.ToLowerInvariant().Replace("pf_", "");
            string des = desiredFormat.ToLowerInvariant().Replace("pf_", "");
            bool correct = curr == des;
            return correct || (!correct && curr.Contains("ati") && des.Contains("normalmap")) || (!correct && curr.Contains("normalmap") && des.Contains("ati"));
        }


        /// <summary>
        /// Check that new texture contains enough mips.
        /// </summary>
        /// <param name="newtexture">Path to texture to load.</param>
        /// <param name="ExpectedMips">Number of expected mips.</param>
        /// <returns>True if number of mips is valid.</returns>
        public static bool CheckTextureMips(string newtexture, int ExpectedMips, out int numMips)
        {
            numMips = 0;
            try
            {
                DDSPreview dds = new DDSPreview(File.ReadAllBytes(newtexture));
                numMips = (int)dds.NumMips;
                if (ExpectedMips > 1)
                {
                    if (dds.NumMips < ExpectedMips)
                        return false;
                    else
                        return true;
                }
                else
                    return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Resizes thumbnail to correct proportions and returns it.
        /// </summary>
        /// <param name="image">Original thumbnail.</param>
        /// <param name="size">Size to set.</param>
        /// <returns>Bitmap of new thumbnail.</returns>
        public static Bitmap FixThumb(Image image, int size)
        {
            int tw, th, tx, ty;
            int w = image.Width;
            int h = image.Height;
            double whRatio = (double)w / h;

            if (image.Width >= image.Height)
            {
                tw = size;
                th = (int)(tw / whRatio);
            }
            else
            {
                th = size;
                tw = (int)(th * whRatio);
            }
            tx = (size - tw) / 2;
            ty = (size - th) / 2;

            Bitmap thumb = new Bitmap(size, size);
            Graphics g = Graphics.FromImage(thumb);
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(image, new Rectangle(tx, ty, tw, th), new Rectangle(0, 0, w, h), GraphicsUnit.Pixel);

            return thumb;
        }


        /// <summary>
        /// Salts resize image function. Returns resized image.
        /// </summary>
        /// <param name="imgToResize">Image to resize</param>
        /// <param name="size">Size to shape to</param>
        /// <returns>Resized image as an Image.</returns>
        public static Image resizeImage(Image imgToResize, Size size)
        {
            // KFreon: And so begins the black magic
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)size.Width / (float)sourceWidth);
            nPercentH = ((float)size.Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap b = new Bitmap(destWidth, destHeight);
            using (Graphics g = Graphics.FromImage((Image)b))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            }
            return (Image)b;
        }


        /// <summary>
        /// Gets image from image data. PROBABLY GOING TO CHANGE!
        /// </summary>
        /// <param name="ddsformat">Format of DDS we're getting image from. If null or has a . in it, designated as non DDS.</param>
        /// <param name="imgData">Array of image data.</param>
        /// <returns>Image as a Bitmap.</returns>
        public static Bitmap GetImage(string ddsformat, byte[] imgData)
        {
            Bitmap img = null;
            MemoryStream ms = null;

            try
            {
                // KFreon: NON DDS
                if (ddsformat == null || ddsformat.Contains('.'))
                {
                    ms = new MemoryStream(imgData);
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            img = Image.FromStream(ms) as Bitmap;
                            break;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                }
                else   // KFreon: DDS
                {
                    if (ddsformat.ToLower() != "dxt3")
                    {
                        DDSPreview data = KFreonLib.Textures.Methods.GetAllButDXT3DDSData(imgData);
                        if (data != null)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                try
                                {
                                    img = DDSImage.ToBitmap(data.GetMipData(), (data.FormatString == "G8") ? DDSFormat.G8 : data.Format, (int)data.Width, (int)data.Height);
                                    break;
                                }
                                catch
                                {
                                    System.Threading.Thread.Sleep(500);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++) 
                        {
                            try
                            {
                                /*ms = new MemoryStream(imgData);
                                ms = (MemoryStream)KFreonLib.Textures.Methods.GetDXTS3Data((Stream)ms);*/
                                using (ResILImage kfimg = new ResILImage(imgData))
                                    img = new Bitmap(new MemoryStream(kfimg.ToArray(ResIL.Unmanaged.ImageType.Jpg)));
                                break;
                            }
                            catch
                            {
                                System.Threading.Thread.Sleep(500);
                            }
                        }       
                    }
                }
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Image creation failed: " + e.Message);
            }

            return img;
        }


        /// <summary>
        /// Saves given image to file.
        /// </summary>
        /// <param name="image">Image to save.</param>
        /// <param name="savepath">Path to save image to.</param>
        /// <returns>True if saved successfully. False if failed or already exists.</returns>
        public static bool SaveImage(Image image, string savepath)
        {
            if (!File.Exists(savepath))
                try
                {
                    image.Save(savepath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }
                catch (Exception e)
                {
                    DebugOutput.PrintLn("GDI Error in: " + savepath);
                    DebugOutput.PrintLn("ERROR: " + e.Message);
                    return false;
                }

            return true;
        }


        /// <summary>
        /// GOING TO CHANGE
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static DDSPreview GetAllButDXT3DDSData(byte[] data)
        {
            if (data == null)
            {
                DebugOutput.PrintLn("Unable to access file or invalid file.");
                return null;
            }

            DDSPreview ddsimg = new DDSPreview(data);
            return ddsimg;
        }


        /// <summary>
        /// GOING TO CHANGE
        /// </summary>
        /// <param name="data"></param>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        /*public static Stream GetDXTS3Data(byte[] data, string imagePath)
        {
            DevIL.ImageImporter DImporter = new DevIL.ImageImporter();
            DevIL.Image im = DImporter.LoadImage(imagePath);
            DevIL.ImageExporter DExporter = new DevIL.ImageExporter();

            MemoryStream ms = new MemoryStream();
            DExporter.SaveImageToStream(im, DevIL.ImageType.Bmp, ms);

            DImporter.Dispose();
            im.Dispose();
            DExporter.Dispose();
            return ms;
        }*/


        /// <summary>
        /// GOING TO CHANGE
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        /*public static Stream GetDXTS3Data(Stream stream)
        {
            DevIL.ImageImporter DImporter = new DevIL.ImageImporter();
            DevIL.Image im = DImporter.LoadImageFromStream(stream);
            DevIL.ImageExporter DExporter = new DevIL.ImageExporter();

            MemoryStream ms = new MemoryStream();
            DExporter.SaveImageToStream(im, DevIL.ImageType.Bmp, ms);

            DImporter.Dispose();
            im.Dispose();
            DExporter.Dispose();
            return ms;
        }*/
    }


    /// <summary>
    /// Provides functions to create texture objects.
    /// </summary>
    public static class Creation
    {
        #region Object Creators
        /// <summary>
        /// Generates a thumbnail image from a given image.
        /// </summary>
        /// <param name="img">Image to get a thumbnail from.</param>
        /// <param name="size">OPTIONAL: Size to resize to (Maximum in any direction). Defaults to 128.</param>
        /// <returns></returns>
        public static Bitmap GenerateThumbImage(Image img, int size = 128)
        {
            // KFreon: Get resize details for resize to max size 128 (same aspect ratio)
            double DeterminingDimension = (img.Width > img.Height) ? img.Width : img.Height;
            double divisor = DeterminingDimension / size;

            // KFreon: Check for smaller dimensions and don't resize if so (SCALE UP?)
            if (divisor < 1)
                divisor = 1;

            // KFreon: If image is weird (i.e. 1px high), nullify
            if ((int)img.Width / divisor < 1 || (int)img.Height / divisor < 1)
                return null;
            else
            {
                // KFreon: Resize image
                Image image = (divisor == 1) ? new Bitmap(img) : new Bitmap(img, new Size((int)(img.Width / divisor), (int)(img.Height / divisor)));//resizeImage((Image)img, new Size((int)(img.Width / divisor), (int)(img.Height / divisor)));
                image = Methods.FixThumb(image, size);
                return image as Bitmap;
            }
        }


        /// <summary>
        /// Generates a thumbnail from an image and saves to file.
        /// </summary>
        /// <param name="img">Image to generate thumbnail from.</param>
        /// <param name="savepath">Path to save thumbnail to.</param>
        /// <param name="execpath">Path to ME3Explorer \exec\ folder.</param>
        /// <returns>Path to saved thumbnail.</returns>
        public static string GenerateThumbnail(Bitmap img, string savepath, string execpath)
        {
            // KFreon: Get thumbnail and set savepath
            using (Bitmap newimg = GenerateThumbImage(img))
            {
                if (newimg == null)
                    savepath = execpath + "placeholder.ico";
                else
                {
                    // KFreon: Save image to file
                    for (int i = 0; i < 3; i++)
                        if (Methods.SaveImage(newimg, savepath))
                            break;
                        else
                            System.Threading.Thread.Sleep(1000);
                }
            }
            string retval = execpath + "placeholder.ico";
            if (savepath != "")
                retval = savepath;

            return retval;
        }

        public static string GenerateThumbnail(string filename, int WhichGame, int expID, string pathBIOGame, string savepath, string execpath)
        {
            ITexture2D tex2D = CreateTexture2D(filename, expID, WhichGame, pathBIOGame);
            Bitmap bmp = tex2D.GetImage();
            return GenerateThumbnail(bmp, savepath, execpath);
        }


        /// <summary>
        /// Load an image into one of AK86's classes.
        /// </summary>
        /// <param name="im">AK86 image already, just return it unless null. Then load from fileToLoad.</param>
        /// <param name="fileToLoad">Path to file to be loaded. Irrelevent if im is provided.</param>
        /// <returns>AK86 Image file.</returns>
        public static ImageFile LoadAKImageFile(ImageFile im, string fileToLoad)
        {
            ImageFile imgFile = null;
            if (im != null)
                imgFile = im;
            else
            {
                if (!File.Exists(fileToLoad))
                    throw new FileNotFoundException("invalid file to replace: " + fileToLoad);

                // check if replacing image is supported
                string fileFormat = Path.GetExtension(fileToLoad);
                switch (fileFormat)
                {
                    case ".dds": imgFile = new DDS(fileToLoad, null); break;
                    case ".tga": imgFile = new TGA(fileToLoad, null); break;
                    default: throw new FileNotFoundException(fileFormat + " image extension not supported");
                }
            }
            return imgFile;
        }


        /// <summary>
        /// Create a Texture2D from things.
        /// </summary>
        /// <param name="filename">Filename to load Texture2D from.</param>
        /// <param name="expID">ExpID of texture in question.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pathBIOGame">Path to BIOGame.</param>
        /// <param name="hash">Hash of texture.</param>
        /// <returns>Texture2D object</returns>
        public static ITexture2D CreateTexture2D(string filename, int expID, int WhichGame, string pathBIOGame, uint hash = 0)
        {
            IPCCObject pcc = PCCObjects.Creation.CreatePCCObject(filename, WhichGame);
            return pcc.CreateTexture2D(expID, pathBIOGame, hash);
        }


        /// <summary>
        /// Populates a Texture2D given a base Texture2D.
        /// </summary>
        /// <param name="tex2D">Base Texture2D. Most things missing.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pathBIOGame">Path to BIOGame.</param>
        /// <returns>Populated Texture2D.</returns>
        public static ITexture2D CreateTexture2D(ITexture2D tex2D, int WhichGame, string pathBIOGame)
        {
            ITexture2D temp = CreateTexture2D(tex2D.allPccs[0], tex2D.expIDs[0], WhichGame, pathBIOGame);
            temp.allPccs = new List<string>(tex2D.allPccs);
            temp.Hash = tex2D.Hash;
            temp.hasChanged = tex2D.hasChanged;
            temp.expIDs = tex2D.expIDs;
            return temp;
        }


        /// <summary>
        /// Creates a Texture2D from a bunch of stuff.
        /// </summary>
        /// <param name="texName">Name of texture to create.</param>
        /// <param name="pccs">List of PCC's containing texture.</param>
        /// <param name="ExpIDs">List of ExpID's of texture in PCC's. MUST have same number of elements as in PCC's.</param>
        /// <param name="WhichGame">Game target.</param>
        /// <param name="pathBIOGame">Path to BIOGame.</param>
        /// <param name="hash">Hash of texture.</param>
        /// <returns>Texture2D object.</returns>
        public static ITexture2D CreateTexture2D(string texName, List<string> pccs, List<int> ExpIDs, int WhichGame, string pathBIOGame, uint hash = 0)
        {
            ITexture2D temptex2D = null;
            switch (WhichGame)
            {
                case 1:
                    temptex2D = new ME1Texture2D(texName, pccs, ExpIDs, pathBIOGame, WhichGame, hash);
                    break;
                case 2:
                    temptex2D = new ME2Texture2D(texName, pccs, ExpIDs, pathBIOGame,WhichGame, hash);
                    break;
                case 3:
                    temptex2D = new ME3SaltTexture2D(texName, pccs, ExpIDs, hash, pathBIOGame, WhichGame);
                    //temptex2D = new ME3SaltTexture2D(new ME3PCCObject(pccs[0]), ExpIDs[0], pathBIOGame, hash);
                    break;
            }
            if (hash != 0)
                temptex2D.Hash = hash;
            return temptex2D;
        }
        #endregion
    }
        #endregion
}
