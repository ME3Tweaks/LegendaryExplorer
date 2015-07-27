using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using Gibbed.IO;

namespace AmaroK86.ImageFormat
{
    public class ImageSize : IComparable
    {
        public readonly uint width;
        public readonly uint height;

        public ImageSize(uint width, uint height)
        {
            if (!checkIsPower2(width))
                new FormatException("Invalid width value, must be power of 2");
            if (!checkIsPower2(width))
                new FormatException("Invalid height value, must be power of 2");
            if (width == 0)
                width = 1;
            if (height == 0)
                height = 1;
            this.width = width;
            this.height = height;
        }

        private bool checkIsPower2(uint val)
        {
            uint power = 1;
            while (power < val)
            {
                power *= 2;
            }
            return val == power;
        }

        public int CompareTo(object obj)
        {
            if (obj is ImageSize)
            {
                ImageSize temp = (ImageSize)obj;
                if ((temp.width * temp.height) == (this.width * this.height))
                    return 0;
                if ((temp.width * temp.height) > (this.width * this.height))
                    return -1;
                else
                    return 1;
            }
            throw new ArgumentException();
        }

        public override string ToString()
        {
            return this.width + "x" + this.height;
        }

        public override bool Equals(System.Object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Point return false.
            ImageSize p = obj as ImageSize;
            if ((System.Object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (this.width == p.width) && (this.height == p.height);
        }

        public bool Equals(ImageSize p)
        {
            // If parameter is null return false:
            if ((object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (this.width == p.width) && (this.height == p.height);
        }

        public override int GetHashCode()
        {
            return (int)(width ^ height);
        }

        public static bool operator ==(ImageSize a, ImageSize b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
            {
                return true;
            }

            // If one is null, but not both, return false.
            if (((object)a == null) || ((object)b == null))
            {
                return false;
            }

            // Return true if the fields match:
            return a.width == b.width && a.height == b.height;
        }

        public static bool operator !=(ImageSize a, ImageSize b)
        {
            return !(a == b);
        }

        public static ImageSize operator /(ImageSize a, int b)
        {
            return new ImageSize((uint)(a.width / b), (uint)(a.height / b));
        }

        public static ImageSize operator *(ImageSize a, int b)
        {
            return new ImageSize((uint)(a.width * b), (uint)(a.height * b));
        }

        public static ImageSize stringToSize(string input)
        {
            string[] parsed = input.Split('x');
            if (parsed.Length != 2)
                throw new FormatException();
            uint width = Convert.ToUInt32(parsed[0]);
            uint height = Convert.ToUInt32(parsed[1]);
            return new ImageSize(width, height);
        }
    }

    public class ImageFile
    {
        public string fileName;
        public ImageSize imgSize;
        public int headSize { get { return (headData == null) ? 0 : headData.Length; } }
        public int dataSize { get { return (imgData == null) ? 0 : imgData.Length; } }
        public byte[] headData = null;
        public byte[] imgData = null;
        public string format = null;
        public float BPP { get; protected set; }

        protected ImageFile(string fileName, ImageSize imgSize, string format)
        {
            this.fileName = fileName;
            this.imgSize = imgSize;
            this.format = format;
        }

        protected ImageFile(string fileName, byte[] data)
        {
            string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            int headerSize = 128;

            /*switch (fileExtension)
            {
                case ".dds": headerSize = 128; break;
                case ".tga": headerSize = 18; break;
                default: throw new FormatException("invalid file");
            }*/

            Stream stream;
            if (data != null)
                stream = new MemoryStream(data);
            else
                stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            headData = new byte[headerSize];
            stream.Read(headData, 0, headData.Length);
            imgData = new byte[stream.Length - headData.Length];
            stream.Read(imgData, 0, imgData.Length);
            stream.Close();
        }

        virtual public byte[] ToArray()
        {
            return null;
        }

        virtual public string subtype()
        {
            return null;
        }

        virtual public byte[] resize()
        {
            return imgData;
        }

        virtual protected void flipVertically()
        {
            throw new NotImplementedException();
        }

        public static long ImageDataSize(ImageSize imgsize, string format, float BytesPerPixel)
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
    }

    public class DDS : ImageFile
    {
        public enum FourCC : uint
        {
            DXT1 = 0x31545844,
            DXT3 = 0x33545844,
            DXT5 = 0x35545844,
            ATI2 = 0x32495441
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]
        private unsafe struct _DDSHeader
        {
            public Int32 magic;
            public Int32 Size;
            public Int32 Flags;
            public UInt32 Height;
            public UInt32 Width;
            public Int32 PitchOrLinearSize;
            public Int32 Depth;
            public Int32 MipMapCount;
            public fixed Int32 Reserved1[11];
            // DDS_PIXELFORMAT
            public Int32 pfSize;
            public Int32 pfFlags;
            public Int32 FourCC;
            public Int32 RGBBitCount;
            public Int32 RBitMask;
            public Int32 GBitMask;
            public Int32 BBitMask;
            public Int32 ABitMask;
            // END DDS_PIXELFORMAT
            public Int32 Caps;
            public Int32 Caps2;
            public Int32 Caps3;
            public Int32 Caps4;
            public Int32 Reserved2;
        }

        private _DDSHeader DDSHeader;

        public DDS(string fileName, ImageSize imgSize, string format, byte[] rawData)
            : base(fileName, imgSize, format)
        {
            DDSHeader.magic = 0x20534444;
            DDSHeader.Size = 0x7C;
            DDSHeader.Flags = 0x081007;
            DDSHeader.Caps = 0x1000;
            DDSHeader.pfFlags = 0x4;
            DDSHeader.pfSize = 0x20;
            DDSHeader.Width = imgSize.width;
            DDSHeader.Height = imgSize.height;
            switch (format)
            {
                case "PF_DXT1":
                case "DXT1":
                    DDSHeader.FourCC = (int)FourCC.DXT1;
                    BPP = 0.5F;
                    break;
                case "PF_DXT5":
                case "DXT5":
                    DDSHeader.FourCC = (int)FourCC.DXT5;
                    BPP = 1;
                    break;
                case "PF_V8U8":
                case "V8U8":
                    DDSHeader.Flags = 0x001007;
                    DDSHeader.pfFlags = 0x80000;
                    DDSHeader.RGBBitCount = 0x10;
                    DDSHeader.RBitMask = 0x0000FF;
                    DDSHeader.GBitMask = 0x00FF00;
                    BPP = 2;
                    break;
                case "ATI2":
                case "PF_NormalMap_HQ":
                    DDSHeader.FourCC = (int)FourCC.ATI2;
                    BPP = 1;
                    break;
                case "PF_A8R8G8B8":
                case "A8R8G8B8":
                    BPP = 4;
                    DDSHeader.pfFlags = 0x41;
                    DDSHeader.RGBBitCount = 0x20;
                    DDSHeader.RBitMask = 0xFF0000;
                    DDSHeader.GBitMask = 0xFF00;
                    DDSHeader.BBitMask = 0xFF;
                    DDSHeader.ABitMask = -16777216;
                    break;
                case "PF_R8G8B8":
                case "R8G8B8":
                    BPP = 3;
                    DDSHeader.pfFlags = 0x40;
                    DDSHeader.RGBBitCount = 0x18;
                    DDSHeader.RBitMask = 0xFF0000;
                    DDSHeader.GBitMask = 0xFF00;
                    DDSHeader.BBitMask = 0xFF;
                    DDSHeader.ABitMask = 0x0;
                    break;
                case "PF_G8":
                case "G8":
                    BPP = 1;
                    DDSHeader.pfFlags = 0x20000;
                    DDSHeader.RGBBitCount = 0x8;
                    DDSHeader.RBitMask = 0xFF;
                    break;
                default: throw new FormatException("Invalid DDS format");
            }
            imgData = rawData;

            // convert DDSHeader to byte array
            int headSize = Marshal.SizeOf(DDSHeader);
            headData = new byte[headSize];
            IntPtr ptr = Marshal.AllocHGlobal(headSize);
            Marshal.StructureToPtr(DDSHeader, ptr, true);
            Marshal.Copy(ptr, headData, 0, headData.Length);
            Marshal.FreeHGlobal(ptr);
        }

        public DDS(string fileName, byte[] data)
            : base(fileName, data) // call the base constructor
        {
            byte[] buffer = headData;
            // put data inside DDSHeader
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            DDSHeader = (_DDSHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(_DDSHeader));
            handle.Free();

            imgSize = new ImageSize(DDSHeader.Width, DDSHeader.Height);
            format = subtype();
            switch (format)
            {
                case "DXT1": BPP = 0.5F; break;
                case "DXT5":
                case "ATI2": BPP = 1F; break;
                case "V8U8": BPP = 2F; break;
                default: BPP = (float)DDSHeader.RGBBitCount / 8; break;
            }
        }

        public override string subtype()
        {
            if ((DDSHeader.pfFlags & 0x4) == 0x4) // DXT
            {
                switch (DDSHeader.FourCC)
                {
                    case (int)FourCC.DXT1: return "DXT1";
                    case (int)FourCC.DXT5: return "DXT5";
                    case (int)FourCC.ATI2: return "ATI2";
                    default: throw new FormatException("Unknown 4CC");
                }
            }
            else if ((DDSHeader.pfFlags & 0x40) == 0x40) // Uncompressed RGB
            {
                if (DDSHeader.RBitMask == 0xFF0000 && DDSHeader.GBitMask == 0xFF00 && DDSHeader.BBitMask == 0xFF)
                {
                    if ((DDSHeader.pfFlags & 0x1) == 0x1 && DDSHeader.ABitMask == -16777216 && DDSHeader.RGBBitCount == 0x20)
                        return "A8R8G8B8";
                    else if ((DDSHeader.pfFlags & 0x1) == 0x0 && DDSHeader.RGBBitCount == 0x18)
                        return "R8G8B8";
                }
            }
            else if ((DDSHeader.pfFlags & 0x80000) == 0x80000 && DDSHeader.RGBBitCount == 0x10 && DDSHeader.RBitMask == 0xFF && DDSHeader.GBitMask == 0xFF00) // V8U8
            {
                return "V8U8";
            }
            else if ((DDSHeader.pfFlags & 0x20000) == 0x20000 && DDSHeader.RGBBitCount == 0x8 && DDSHeader.RBitMask == 0xFF)
                return "G8";

            throw new FormatException("Unknown format");
        }

        public override byte[] resize()
        {
            byte[] buff = new byte[ImageDataSize(this.imgSize, this.format, this.BPP)];
            Buffer.BlockCopy(base.resize(), 0, buff, 0, buff.Length);
            return buff;
            //return base.resize();
        }

        public override byte[] ToArray()
        {
            int size = Marshal.SizeOf(DDSHeader);
            if (size != headData.Length)
                throw new FormatException("Incorrect DXT header size");
            byte[] head = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(DDSHeader, ptr, true);
            Marshal.Copy(ptr, head, 0, size);
            Marshal.FreeHGlobal(ptr);

            byte[] total = new byte[headSize + dataSize];
            head.CopyTo(total, 0);
            imgData.CopyTo(total, headSize);
            return total;
        }
    }

    public class TGA : ImageFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 18)]
        public unsafe struct _TGAHeader
        {
            public byte identsize;
            public byte colourmaptype;
            public byte imagetype;
            public Int16 colourmapstart;
            public Int16 colourmaplength;
            public byte colourmapbits;
            public Int16 xstart;
            public Int16 ystart;
            public Int16 width;
            public Int16 height;
            public byte bits;
            public byte descriptor;
        }

        public _TGAHeader TGAHeader;
        public bool RLECompressed
        {
            get { return (TGAHeader.imagetype & 0x08) == 0x08; }
            private set { if (value) TGAHeader.imagetype |= 0x08; else TGAHeader.imagetype &= unchecked((byte)~0x08); }
        }
        public bool verticalFlipped
        {
            get { return (TGAHeader.descriptor & 0x20) == 0x20; }
            private set { if (value) TGAHeader.descriptor |= 0x20; else TGAHeader.descriptor &= unchecked((byte)~0x20); }
        }

        public TGA(string fileName, ImageSize imgSize, string format, byte[] rawData) // used when extracting raw data from me3 archive
            : base(fileName, imgSize, format)
        {
            TGAHeader.imagetype = 0x2; // RGB Image type
            TGAHeader.bits = (byte)(rawData.Length / (imgSize.width * imgSize.height) * 8);
            TGAHeader.width = (short)imgSize.width;
            TGAHeader.height = (short)imgSize.height;
            //TGAHeader.colourmapbits = 32;
            TGAHeader.descriptor = 0x00; // 0x20 = flips the image vertically
            BPP = TGAHeader.bits / 8;

            switch (format)
            {
                case "PF_G8":
                case "G8":
                    {
                        imgData = new byte[rawData.Length];
                        for (int i = 0; i < imgSize.height; i++)
                        {
                            for (int j = 0; j < imgSize.width; j++)
                            {
                                imgData[(imgSize.width * i) + j] = rawData[(imgSize.width * i) + j];
                            }
                        }
                        TGAHeader.imagetype = 0x3;
                    }
                    break;

                case "PF_A8R8G8B8":
                case "A8R8G8B8":
                    {
                        TGAHeader.descriptor |= 0x08; // 0x08 = 8-bit alpha bits
                        imgData = new byte[rawData.Length];
                        int intBPP = (int)BPP;

                        for (int i = 0; i < imgSize.height; i++)
                        {
                            for (int j = 0; j < imgSize.width; j++)
                            {
                                for (int k = 0; k < intBPP; k++)
                                    imgData[(intBPP * imgSize.width * (imgSize.height - 1 - i)) + (j * intBPP) + k] = rawData[(imgSize.width * intBPP * i) + (j * intBPP) + k];
                                /*imgData[(4 * imgSize.width * (imgSize.height - 1 - i)) + (j * 4) + 1] = rawData[(imgSize.width * 4 * i) + (j * 4) + 1];
                                imgData[(4 * imgSize.width * (imgSize.height - 1 - i)) + (j * 4) + 2] = rawData[(imgSize.width * 4 * i) + (j * 4) + 2];
                                imgData[(4 * imgSize.width * (imgSize.height - 1 - i)) + (j * 4) + 3] = rawData[(imgSize.width * 4 * i) + (j * 4) + 3];*/
                            }
                        }
                        verticalFlipped = true;
                        flipVertically();
                    }
                    break;
                default: throw new FormatException("Invalid TGA format");
            }

            // convert TGAHeader to byte array
            int headSize = Marshal.SizeOf(TGAHeader);
            headData = new byte[headSize];
            IntPtr ptr = Marshal.AllocHGlobal(headSize);
            Marshal.StructureToPtr(TGAHeader, ptr, true);
            Marshal.Copy(ptr, headData, 0, headData.Length);
            Marshal.FreeHGlobal(ptr);
        }

        public TGA(string fileName, byte[] data)
            : base(fileName, data) // used when importing file
        {
            // saving tga header data in TGAHeader structure
            GCHandle handle = GCHandle.Alloc(headData, GCHandleType.Pinned);
            TGAHeader = (_TGAHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(_TGAHeader));
            handle.Free();

            imgSize = new ImageSize((uint)TGAHeader.width, (uint)TGAHeader.height);
            BPP = TGAHeader.bits / 8;

            if (RLECompressed)
                RLEDecompress(); // decompress
            else
                imgData = imgData.Take(TGAHeader.width * TGAHeader.height * (int)BPP).ToArray(); // get rid of tga footer

            if (verticalFlipped)
                flipVertically();

            format = subtype();
        }

        public override string subtype() // analyze data and gives an "opinion", useful when getting data from an external file
        {
            bool isG8 = true;
            /*
            for (int i = 0; i < imgData.Length / BPP; i += (int)BPP)
            {
                isG8 = isG8 && (imgData[i] == imgData[i + 1]) && (imgData[i + 1] == imgData[i + 2]);
            }
             */

            switch (TGAHeader.imagetype)
            {
                case 3:
                    isG8 = true;
                    break;
                case 2:
                    isG8 = false;
                    break;
                default:
                    throw new FormatException("Format not recognised by code");
            }
            if (isG8)
                return "G8";
            else
                return "A8R8G8B8";
        }

        public override byte[] resize()
        {
            byte[] buffer;
            int intBPP = (int)BPP;

            switch (format)
            {
                case "G8":
                    //buffer = new byte[imgData.Length / 4];
                    //for (int i = 0; i < imgData.Length / 4; i++)
                    //{
                    //    buffer[i] = imgData[i * 4];
                    //}
                    buffer = new byte[imgSize.width * imgSize.height];
                    Buffer.BlockCopy(imgData, 0, buffer, 0, buffer.Length);
                    break;
                default:
                    buffer = imgData;
                    break;
            }
            return buffer;
        }

        public override byte[] ToArray()
        {
            int size = Marshal.SizeOf(TGAHeader);
            if (size != headSize)
                throw new FormatException("Incorrect TGA header size");
            byte[] head = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(TGAHeader, ptr, true);
            Marshal.Copy(ptr, head, 0, size);
            Marshal.FreeHGlobal(ptr);

            byte[] total = new byte[headSize + dataSize];
            head.CopyTo(total, 0);
            imgData.CopyTo(total, headSize);
            return total;
        }

        private void RLEDecompress()
        {
            if (RLECompressed) //decompressing image
            {
                MemoryStream oldImgData = new MemoryStream(imgData);
                MemoryStream newImgData = new MemoryStream((int)(imgSize.width * imgSize.height * BPP));
                byte[] buffer;

                while (newImgData.Position < (imgSize.width * imgSize.height * BPP))
                {
                    int count = oldImgData.ReadByte();
                    bool isCompressed = (count & 0x80) == 0x80; //if value > 128
                    if (isCompressed)
                    {
                        count -= 0x7F; //value - 127 = num of repetitions
                        buffer = new byte[(int)BPP];
                        oldImgData.Read(buffer, 0, buffer.Length);
                        for (int j = 0; j < count; j++) // write pixel for each count
                        {
                            newImgData.Write(buffer, 0, buffer.Length);
                        }
                    }
                    else // if it's not compressed copy the values
                    {
                        buffer = new byte[(int)BPP * (count + 1)];
                        oldImgData.Read(buffer, 0, buffer.Length);
                        newImgData.Write(buffer, 0, buffer.Length);
                    }
                }
                if (newImgData.Position != newImgData.Length)
                    throw new BadImageFormatException();
                RLECompressed = false; //remove RLE flag
                imgData = newImgData.ToArray();

                oldImgData.Close();
                newImgData.Close();
            }
        }

        protected override void flipVertically()
        {
            if (verticalFlipped)
            {
                byte[] imgFlip = new byte[imgData.Length];
                int intBPP = (int)BPP;

                for (int i = 0; i < imgSize.height; i++)
                    for (int j = 0; j < imgSize.width; j++)
                        for (int k = 0; k < BPP; k++)
                            imgFlip[(intBPP * imgSize.width * (imgSize.height - 1 - i)) + (j * intBPP) + k] = imgData[(imgSize.width * intBPP * i) + (j * intBPP) + k];

                imgData = imgFlip;
                verticalFlipped = false;
            }
        }
    }
}
