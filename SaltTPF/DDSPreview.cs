using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using AmaroK86.ImageFormat;

namespace SaltTPF
{
    public class DDSPreview
    {
        private const UInt32 DDSMagic = 0x20534444;
        private UInt32 DDSFlags;
        public bool mips;
        public UInt32 Height;
        public UInt32 Width;
        private UInt32 NumMips;
        public String Format;
        private byte[] imgData;
        private float BPP;
        private UInt32 pfFlags;
        private UInt32 fourCC;
        private UInt32 rgbBitCount;
        private UInt32 rBitMask;
        private UInt32 gBitMask;
        private UInt32 bBitMask;
        private UInt32 aBitMask;
        public bool stdDDS;
        private bool compressed;

        public DDSPreview(byte[] data)
        {
            if (BitConverter.ToUInt32(data, 0) != DDSMagic)
                throw new FormatException("Invalid DDS Magic Number");
            if (BitConverter.ToUInt32(data, 4) != 0x7C)
                throw new FormatException("Invalid header size");
            DDSFlags = BitConverter.ToUInt32(data, 8);
            if ((DDSFlags & 0x1) != 0x1 || (DDSFlags & 0x2) != 0x2 || (DDSFlags & 0x4) != 0x4 || (DDSFlags & 0x1000) != 0x1000)
                throw new FormatException("Invalid DDS Flags");
            mips = (DDSFlags & 0x20000) == 0x20000;
            Height = BitConverter.ToUInt32(data, 12);
            Width = BitConverter.ToUInt32(data, 16);
            // Skip pitch and depth, unimportant
            NumMips = BitConverter.ToUInt32(data, 28);
            
            // Read format stuff
            pfFlags = BitConverter.ToUInt32(data, 0x50);
            fourCC = BitConverter.ToUInt32(data, 0x54);
            rgbBitCount = BitConverter.ToUInt32(data, 0x58);
            rBitMask = BitConverter.ToUInt32(data, 0x5C);
            gBitMask = BitConverter.ToUInt32(data, 0x60);
            bBitMask = BitConverter.ToUInt32(data, 0x64);
            aBitMask = BitConverter.ToUInt32(data, 0x68);
            Format = GetFormat();
            
            imgData = new byte[data.Length - 0x80];
            Array.Copy(data, 0x80, imgData, 0, imgData.Length);

            switch (Format)
            {
                case "DXT1": BPP = 0.5F; stdDDS = true; compressed = true; break;
                case "DXT5": BPP = 1F; stdDDS = true; compressed = true; break;
                case "V8U8": BPP = 2F; stdDDS = true; compressed = false; break;
                case "ATI2": BPP = 1F; stdDDS = true; compressed = true; break;
                case "A8R8G8B8": BPP = 4F; stdDDS = false; compressed = false; break;
                case "R8G8B8": BPP = 3F; stdDDS = false; compressed = false; break;
                default: BPP = 1; stdDDS = false; compressed = false; break;
            }
            
        }

        public DDSFormat GetDDSFormat()
        {
            switch (fourCC)
            {
                case 0x31545844: return DDSFormat.DXT1;
                case 0x35545844: return DDSFormat.DXT5;
                case 0x38553856: return DDSFormat.V8U8;
                case 0x32495441: return DDSFormat.ATI2;
                default:
                    throw new FormatException("Unknown/ Unsupported DDS Format");
            }
        }

        private String GetFormat()
        {
            if ((pfFlags & 0x4) == 0x4) // DXT
            {
                switch ((int)fourCC)
                {
                    case (int)DDS.FourCC.DXT1: return "DXT1";
                    case (int)DDS.FourCC.DXT5: return "DXT5";
                    case (int)DDS.FourCC.ATI2: return "ATI2";
                    default: throw new FormatException("Unknown 4CC");
                }
            }
            else if ((pfFlags & 0x40) == 0x40) // Uncompressed RGB
            {
                if (rBitMask == 0xFF0000 && gBitMask == 0xFF00 && bBitMask == 0xFF)
                {
                    if ((pfFlags & 0x1) == 0x1 && aBitMask == 0xFF000000 && rgbBitCount == 0x20)
                        return "A8R8G8B8";
                    else if ((pfFlags & 0x1) == 0x0 && rgbBitCount == 0x18)
                        return "R8G8B8";
                }
            }
            else if ((pfFlags & 0x80000) == 0x80000 && rgbBitCount == 0x10 && rBitMask == 0xFF && gBitMask == 0xFF00) // V8U8
                return "V8U8";
            
            else if ((pfFlags & 0x20000) == 0x20000 && rgbBitCount == 0x8 && rBitMask == 0xFF)
                return "G8";

            throw new FormatException("Unknown format");
        }

        public byte[] GetMipData()
        {
            int len = (int)(BPP * Height * Width);
            byte[] img = new byte[len];
            Array.Copy(imgData, 0, img, 0, len);
            return img;
        }

        public long GetMipMapDataSize()
        {
            return GetMipMapDataSize(new ImageSize(Width, Height));
        }

        private long GetMipMapDataSize(ImageSize imgsize)
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
            long totalBytes = (long)((float)(w * h) * BPP);
            w = imgsize.width;
            h = imgsize.height;
            if (w == 1 && h == 1)
                return totalBytes;
            if (w != 1)
                w = imgsize.width / 2;
            if (h != 1)
                h = imgsize.height / 2;
            return totalBytes + GetMipMapDataSize(new ImageSize(w, h));
        }

        public Bitmap ToBitmap()
        {
            if (stdDDS)
                return DDSImage.ToBitmap(GetMipData(), GetDDSFormat(), (int)Width, (int)Height);
            else
            {
                switch (Format)
                {
                    case "A8R8G8B8":
                        return DDSImage.View32Bit(GetMipData(), (int)Width, (int)Height);
                    case "R8G8B8":
                        return DDSImage.View24Bit(GetMipData(), (int)Width, (int)Height);
                    default:
                        throw new FormatException("Unknown/Unsupported Format");
                }
            }
        }
    }
}
