/*  Copyright (C) 2013 AmaroK86 (marcidm 'at' hotmail 'dot' com)
 *  Copyright (C) 2013 saltisgood
 *  Copyright (C) 2013-2014 KFreon
 *  Copyright (C) 2016-2019 Pawel Kolodziejski
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.

 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.

 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

/*
 * This is a heavy modification of
 * original project took from:
 * http://code.google.com/p/kprojects/
 *
 * Kons 2012-12-03 Version .1
 * Supported features:
 * - DXT1
 * - DXT5
 * - V8U8 (by AmaroK86)
 *
 * contact: kons.snok<at>gmail.com
 */

using System;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using StreamHelpers;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace AmaroK86.ImageFormat
{
    public enum DDSFormat
    {
        DXT1, DXT3, DXT5, V8U8, ATI2, G8, ARGB, RGB
    }

    public enum ATI2BitCodes : ulong
    {
        color0 = 0xFFFFFFFFFFFFFFFFUL, // checks 000
        color1 = 0xDFFFFFFFFFFFFFFFUL, // 001
        interpColor0 = 0xBFFFFFFFFFFFFFFFUL, // 010
        interpColor1 = 0x9FFFFFFFFFFFFFFFUL, // 011
        interpColor2 = 0x7FFFFFFFFFFFFFFFUL, // 100
        interpColor3 = 0x5FFFFFFFFFFFFFFFUL, // 101
        interpColor4 = 0x3FFFFFFFFFFFFFFFUL, // 110
        interpColor5 = 0x1FFFFFFFFFFFFFFFUL, // 111
        result = 0xE000000000000000UL // The expected value on a successful test
    }

    public class DDSImage
    {
        public class MipMap
        {
            public int width;
            public int height;
            public int origWidth;
            public int origHeight;
            public DDSFormat ddsFormat { get; private set; }
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
                    _bitmap = null;
                }
            }
            private Bitmap _bitmap;
            public Bitmap bitmap
            {
                get
                {
                    if (_bitmap == null)
                    {
                        _bitmap = ToBitmap(data, ddsFormat, width, height);
                    }
                    return _bitmap;
                }
            }

            public MipMap(byte[] data, DDSFormat format, int w, int h)
            {
                origWidth = w;
                origHeight = h;

                if (format == DDSFormat.DXT1 || format == DDSFormat.DXT3 || format == DDSFormat.DXT5)
                {
                    w = (w < 4) ? 4 : w;
                    h = (h < 4) ? 4 : h;
                }

                long requiredSize = (long)(w * h * getBytesPerPixel(format));
                if (data.Length != requiredSize)
                    throw new Exception("Data size is not valid for selected format.\nActual: " + data.Length + " bytes\nRequired: " + requiredSize + " bytes");

                this.data = data;
                ddsFormat = format;
                width = w;
                height = h;
            }
        }

        private const int DDS_HEADER_size = 124;
        private const int DDS_PIXELFORMAT_size = 32;
        private const int DDPF_ALPHAPIXELS = 0x00000001;
        private const int DDPF_ALPHA = 0x00000002;
        private const int DDPF_FOURCC = 0x00000004;
        private const int DDPF_RGB = 0x00000040;
        private const int DDPF_YUV = 0x00000200;
        private const int DDPF_LUMINANCE = 0x00020000;
        private const int DDSD_CAPS = 0x00000001;
        private const int DDSD_HEIGHT = 0x00000002;
        private const int DDSD_WIDTH = 0x00000004;
        private const int DDSD_PIXELFORMAT = 0x00001000;
        private const int DDSD_MIPMAPCOUNT = 0x00020000;
        private const int DDSD_LINEARSIZE = 0x00080000;
        private const int DDSCAPS_COMPLEX = 0x00000008;
        private const int DDSCAPS_MIPMAP = 0x00400000;
        private const int DDSCAPS_TEXTURE = 0x00001000;
        private const int FOURCC_DXT1 = 0x31545844;
        private const int FOURCC_DXT3 = 0x33545844;
        private const int FOURCC_DX10 = 0x30315844;
        private const int FOURCC_DXT5 = 0x35545844;
        private const int FOURCC_ATI2 = 0x32495441;

        public int dwMagic;
        private DDS_HEADER header = new DDS_HEADER();
        public DDSFormat ddsFormat { get; private set; }
        public bool hasAlpha { get; private set; }

        public List<MipMap> mipMaps;

        public DDSImage(List<MipMap> mips)
        {
            mipMaps = mips;
        }

        public DDSImage(string ddsFileName, bool bypassCheck = false)
        {
            using (FileStream ddsStream = File.OpenRead(ddsFileName))
            {
                LoadDDSImage(new MemoryStream(ddsStream.ReadToBuffer(ddsStream.Length)), bypassCheck);
            }
        }

        public DDSImage(MemoryStream ddsStream, bool bypassCheck = false)
        {
            LoadDDSImage(ddsStream, bypassCheck);
        }

        public void SaveDDSImage(Stream ddsStream)
        {
            ddsStream.WriteUInt32(0x20534444);
            Write_DDS_HEADER(ddsStream);
            for (int i = 0; i < mipMaps.Count; i++)
            {
                ddsStream.WriteFromBuffer(mipMaps[i].data);
            }
        }

        private void LoadDDSImage(MemoryStream ddsStream, bool bypassCheck = false)
        {
            using (BinaryReader r = new BinaryReader(ddsStream))
            {
                dwMagic = r.ReadInt32();
                if (dwMagic != 0x20534444)
                {
                    throw new Exception("This is not a DDS!");
                }

                Read_DDS_HEADER(header, r);

                if (((header.ddspf.dwFlags & DDPF_FOURCC) != 0) && (header.ddspf.dwFourCC == FOURCC_DX10 /*DX10*/))
                {
                    throw new Exception("DX10 not supported yet!");
                }

                int mipMapCount = 1;
                if ((header.dwFlags & DDSD_MIPMAPCOUNT) != 0)
                    mipMapCount = header.dwMipMapCount;
                if (mipMapCount == 0)
                    mipMapCount = 1;

                mipMaps = new List<MipMap>();

                ddsFormat = getFormat();

                double bytePerPixel = getBytesPerPixel(ddsFormat);

                for (int i = 0; i < mipMapCount; i++)
                {
                    int w = (int)(header.dwWidth / Math.Pow(2, i));
                    int h = (int)(header.dwHeight / Math.Pow(2, i));

                    int origW = w;
                    int origH = h;
                    if (origW == 0 && origH != 0)
                        origW = 1;
                    if (origH == 0 && origW != 0)
                        origH = 1;
                    w = origW;
                    h = origH;
                    if (ddsFormat == DDSFormat.DXT1 || ddsFormat == DDSFormat.DXT3 || ddsFormat == DDSFormat.DXT5)
                    {
                        w = (w < 4) ? 4 : w;
                        h = (h < 4) ? 4 : h;
                    }
                    int mipMapBytes = (int)(w * h * bytePerPixel);
                    byte[] data = r.ReadBytes(mipMapBytes);
                    mipMaps.Add(new MipMap(data, ddsFormat, origW, origH));
                }
            }
        }

        public bool checkExistAllMipmaps()
        {
            if ((header.dwFlags & DDSD_MIPMAPCOUNT) != 0 && header.dwMipMapCount > 1)
            {
                int width = mipMaps[0].origWidth;
                int height = mipMaps[0].origHeight;
                for (int i = 0; i < mipMaps.Count; i++)
                {
                    if (mipMaps[i].origWidth < 4 || mipMaps[i].origHeight < 4)
                        return true;
                    if (mipMaps[i].origWidth != width && mipMaps[i].origHeight != height)
                        return false;
                    width /= 2;
                    height /= 2;
                }
                return true;
            }
            else
            {
                return true;
            }
        }

        private DDSFormat getFormat()
        {
            switch (header.ddspf.dwFourCC)
            {
                case FOURCC_DXT1:
                    {
                        if ((header.ddspf.dwFlags & DDPF_ALPHAPIXELS) != 0)
                            hasAlpha = true;
                        return DDSFormat.DXT1;
                    }
                case FOURCC_DXT3: hasAlpha = true; return DDSFormat.DXT3;
                case FOURCC_DXT5: hasAlpha = true; return DDSFormat.DXT5;
                case FOURCC_ATI2: return DDSFormat.ATI2;
                case 0:
                    if (header.ddspf.dwRGBBitCount == 0x10 &&
                           header.ddspf.dwRBitMask == 0xFF &&
                           header.ddspf.dwGBitMask == 0xFF00 &&
                           header.ddspf.dwBBitMask == 0x00 &&
                           header.ddspf.dwABitMask == 0x00)
                        return DDSFormat.V8U8;
                    if (header.ddspf.dwRGBBitCount == 0x8 &&
                           header.ddspf.dwRBitMask == 0xFF &&
                           header.ddspf.dwGBitMask == 0x00 &&
                           header.ddspf.dwBBitMask == 0x00 &&
                           header.ddspf.dwABitMask == 0x00)
                        return DDSFormat.G8;
                    if (header.ddspf.dwRGBBitCount == 0x20 &&
                           header.ddspf.dwRBitMask == 0xFF0000 &&
                           header.ddspf.dwGBitMask == 0xFF00 &&
                           header.ddspf.dwBBitMask == 0xFF &&
                           header.ddspf.dwABitMask == 0xFF000000)
                    {
                        hasAlpha = true;
                        return DDSFormat.ARGB;
                    }
                    if (header.ddspf.dwRGBBitCount == 0x18 &&
                           header.ddspf.dwRBitMask == 0xFF0000 &&
                           header.ddspf.dwGBitMask == 0xFF00 &&
                           header.ddspf.dwBBitMask == 0xFF &&
                           header.ddspf.dwABitMask == 0x00)
                        return DDSFormat.RGB;
                    break;
                case 60:
                        return DDSFormat.V8U8;
                case 50:
                        return DDSFormat.G8;
                case 21:
                    {
                        hasAlpha = true;
                        return DDSFormat.ARGB;
                    }
                case 20:
                        return DDSFormat.RGB;
                default: break;
            }
            throw new Exception("invalid texture format");
        }

        private DDS_PIXELFORMAT getDDSPixelFormat(DDSFormat format)
        {
            DDS_PIXELFORMAT pixelFormat = new DDS_PIXELFORMAT();
            switch (format)
            {
                case DDSFormat.DXT1:
                    pixelFormat.dwFlags = DDPF_FOURCC;
                    if (hasAlpha)
                        pixelFormat.dwFourCC = FOURCC_DXT1 | DDPF_ALPHAPIXELS;
                    else
                        pixelFormat.dwFourCC = FOURCC_DXT1;
                    break;
                case DDSFormat.DXT3:
                    pixelFormat.dwFlags = DDPF_FOURCC | DDPF_ALPHAPIXELS;
                    pixelFormat.dwFourCC = FOURCC_DXT3;
                    break;
                case DDSFormat.DXT5:
                    pixelFormat.dwFlags = DDPF_FOURCC | DDPF_ALPHAPIXELS;
                    pixelFormat.dwFourCC = FOURCC_DXT5;
                    break;
                case DDSFormat.ATI2:
                    pixelFormat.dwFlags = DDPF_FOURCC;
                    pixelFormat.dwFourCC = FOURCC_ATI2;
                    break;
                case DDSFormat.V8U8:
                    pixelFormat.dwFlags = 0x80000;
                    pixelFormat.dwRGBBitCount = 0x10;
                    pixelFormat.dwRBitMask = 0xFF;
                    pixelFormat.dwGBitMask = 0xFF00;
                    pixelFormat.dwBBitMask = 0x00;
                    pixelFormat.dwABitMask = 0x00;
                    break;
                case DDSFormat.G8:
                    pixelFormat.dwFlags = DDPF_LUMINANCE;
                    pixelFormat.dwRGBBitCount = 0x08;
                    pixelFormat.dwRBitMask = 0xFF;
                    pixelFormat.dwGBitMask = 0x00;
                    pixelFormat.dwBBitMask = 0x00;
                    pixelFormat.dwABitMask = 0x00;
                    break;
                case DDSFormat.ARGB:
                    pixelFormat.dwFlags = DDPF_ALPHAPIXELS | DDPF_RGB;
                    pixelFormat.dwRGBBitCount = 0x20;
                    pixelFormat.dwRBitMask = 0xFF0000;
                    pixelFormat.dwGBitMask = 0xFF00;
                    pixelFormat.dwBBitMask = 0xFF;
                    pixelFormat.dwABitMask = 0xFF000000;
                    break;
                case DDSFormat.RGB:
                    pixelFormat.dwFlags = DDPF_RGB;
                    pixelFormat.dwRGBBitCount = 0x18;
                    pixelFormat.dwRBitMask = 0xFF0000;
                    pixelFormat.dwGBitMask = 0xFF00;
                    pixelFormat.dwBBitMask = 0xFF;
                    pixelFormat.dwABitMask = 0x00;
                    break;
                default:
                    throw new Exception("invalid texture format " + ddsFormat);
            }
            return pixelFormat;
        }

        public static double getBytesPerPixel(DDSFormat ddsFormat)
        {
            switch (ddsFormat)
            {
                case DDSFormat.DXT1: return 0.5;
                case DDSFormat.DXT3:
                case DDSFormat.DXT5:
                case DDSFormat.ATI2:
                case DDSFormat.G8: return 1;
                case DDSFormat.V8U8: return 2;
                case DDSFormat.ARGB: return 4;
                case DDSFormat.RGB: return 3;
                default:
                    throw new Exception("invalid texture format " + ddsFormat);
            }
        }

        public static DDSFormat convertFormat(string format)
        {
            switch (format)
            {
                case "PF_DXT1":
                    return DDSFormat.DXT1;
                case "PF_DXT3":
                    return DDSFormat.DXT3;
                case "PF_DXT5":
                    return DDSFormat.DXT5;
                case "PF_NormalMap_HQ":
                    return DDSFormat.ATI2;
                case "PF_V8U8":
                    return DDSFormat.V8U8;
                case "PF_A8R8G8B8":
                    return DDSFormat.ARGB;
                case "PF_R8G8B8":
                    return DDSFormat.RGB;
                case "PF_G8":
                    return DDSFormat.G8;
                default:
                    throw new Exception("invalid texture format");
            }
        }

        public static byte[] ToARGB(MipMap mipmap)
        {
            switch (mipmap.ddsFormat)
            {
                case DDSFormat.DXT1: return DXT1ToARGB(mipmap.data, mipmap.width, mipmap.height);
                case DDSFormat.DXT3: return DXT3ToARGB(mipmap.data, mipmap.width, mipmap.height);
                case DDSFormat.DXT5: return DXT5ToARGB(mipmap.data, mipmap.width, mipmap.height);
                case DDSFormat.ATI2: return ATI2ToARGB(mipmap.data, mipmap.width, mipmap.height);
                case DDSFormat.V8U8: return V8U8ToARGB(mipmap.data, mipmap.width, mipmap.height);
                case DDSFormat.G8: return G8ToARGB(mipmap.data, mipmap.width, mipmap.height);
                case DDSFormat.ARGB: return mipmap.data;
                case DDSFormat.RGB: return RGBToARGB(mipmap.data, mipmap.width, mipmap.height);
                default:
                    throw new Exception("invalid texture format " + mipmap.ddsFormat);
            }
        }

        public static byte[] ToARGB(byte[] imgData, DDSFormat ddsFormat, int w, int h)
        {
            switch (ddsFormat)
            {
                case DDSFormat.DXT1: return DXT1ToARGB(imgData, w, h);
                case DDSFormat.DXT3: return DXT3ToARGB(imgData, w, h);
                case DDSFormat.DXT5: return DXT5ToARGB(imgData, w, h);
                case DDSFormat.ATI2: return ATI2ToARGB(imgData, w, h);
                case DDSFormat.V8U8: return V8U8ToARGB(imgData, w, h);
                case DDSFormat.G8: return G8ToARGB(imgData, w, h);
                case DDSFormat.ARGB: return imgData;
                case DDSFormat.RGB: return RGBToARGB(imgData, w, h);
                default:
                    throw new Exception("invalid texture format " + ddsFormat);
            }
        }

        public static Bitmap ToBitmap(byte[] imgData, DDSFormat ddsFormat, int w, int h)
        {
            switch (ddsFormat)
            {
                case DDSFormat.DXT1: return DXT1ToBitmap(imgData, w, h);
                case DDSFormat.DXT3: return DXT3ToBitmap(imgData, w, h);
                case DDSFormat.DXT5: return DXT5ToBitmap(imgData, w, h);
                case DDSFormat.ATI2: return ATI2ToBitmap(imgData, w, h);
                case DDSFormat.V8U8: return V8U8ToBitmap(imgData, w, h);
                case DDSFormat.G8: return G8ToBitmap(imgData, w, h);
                case DDSFormat.ARGB: return ARGBToBitmap(imgData, w, h);
                case DDSFormat.RGB: return RGBToBitmap(imgData, w, h);
                default:
                    throw new Exception("invalid texture format " + ddsFormat);
            }
        }

        public static PngBitmapEncoder ToPng(byte[] imgData, DDSFormat ddsFormat, int w, int h)
        {
            switch (ddsFormat)
            {
                case DDSFormat.DXT1: return DXT1ToPng(imgData, w, h);
                case DDSFormat.DXT3: return DXT3ToPng(imgData, w, h);
                case DDSFormat.DXT5: return DXT5ToPng(imgData, w, h);
                case DDSFormat.ATI2: return ATI2ToPng(imgData, w, h);
                case DDSFormat.V8U8: return V8U8ToPng(imgData, w, h);
                case DDSFormat.G8: return G8ToPng(imgData, w, h);
                case DDSFormat.ARGB: return RGBAToPng(imgData, w, h);
                case DDSFormat.RGB: return RGBToPng(imgData, w, h);
                default:
                    throw new Exception("invalid texture format " + ddsFormat);
            }
        }

        #region DXT1
        private static byte[] DXT1ToARGB(byte[] imgData, int w, int h)
        {
            return UncompressDXT1(imgData, w, h);
        }

        private static Bitmap DXT1ToBitmap(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressDXT1(imgData, w, h, true);
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0,
                                                    bmp.Width,
                                                    bmp.Height),
                                      ImageLockMode.WriteOnly,
                                      bmp.PixelFormat);

                Marshal.Copy(imageData, 0, bmpData.Scan0, imageData.Length);
                bmp.UnlockBits(bmpData);
            }
            return bmp;
        }

        private static PngBitmapEncoder DXT1ToPng(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressDXT1(imgData, w, h);
            PngBitmapEncoder png = new PngBitmapEncoder();
            BitmapSource image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, imageData, w * 4);
            png.Frames.Add(BitmapFrame.Create(image));
            return png;
        }

        private static byte[] UncompressDXT1(byte[] imgData, int w, int h, bool stripAlpha = false)
        {
            const int bufferSize = 8;
            byte[] blockStorage = new byte[bufferSize];
            using (MemoryStream bitmapStream = new MemoryStream(w * h * 2))
            {
                using (BinaryWriter bitmapBW = new BinaryWriter(bitmapStream))
                {
                    int readPtr = 0;
                    for (int s = 0; s < h; s += 4)
                    {
                        for (int t = 0; t < w; t += 4)
                        {
                            Buffer.BlockCopy(imgData, readPtr, blockStorage, 0, bufferSize);
                            readPtr += bufferSize;
                            {
                                int color0 = blockStorage[0] | blockStorage[1] << 8;
                                int color1 = blockStorage[2] | blockStorage[3] << 8;

                                int temp;

                                temp = (color0 >> 11) * 255 + 16;
                                int r0 = ((temp >> 5) + temp) >> 5;
                                temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
                                int g0 = ((temp >> 6) + temp) >> 6;
                                temp = (color0 & 0x001F) * 255 + 16;
                                int b0 = ((temp >> 5) + temp) >> 5;

                                temp = (color1 >> 11) * 255 + 16;
                                int r1 = ((temp >> 5) + temp) >> 5;
                                temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
                                int g1 = ((temp >> 6) + temp) >> 6;
                                temp = (color1 & 0x001F) * 255 + 16;
                                int b1 = ((temp >> 5) + temp) >> 5;

                                int code = blockStorage[4] | blockStorage[5] << 8 | blockStorage[6] << 16 | blockStorage[7] << 24;

                                for (int j = 0; j < 4; j++)
                                {
                                    bitmapStream.Seek(((s + j) * w * 4) + (t * 4), SeekOrigin.Begin);
                                    for (int i = 0; i < 4; i++)
                                    {
                                        int fCol = 0;
                                        int positionCode = ((code >> 2 * (4 * j + i)) & 0x03);

                                        if (color0 > color1)
                                        {
                                            switch (positionCode)
                                            {
                                                case 0:
                                                    fCol = b0 | (g0 << 8) | (r0 << 16) | 0xFF << 24;
                                                    break;
                                                case 1:
                                                    fCol = b1 | (g1 << 8) | (r1 << 16) | 0xFF << 24;
                                                    break;
                                                case 2:
                                                    fCol = ((2 * b0 + b1) / 3) | (((2 * g0 + g1) / 3) << 8) | (((2 * r0 + r1) / 3) << 16) | (0xFF << 24);
                                                    break;
                                                case 3:
                                                    fCol = ((b0 + 2 * b1) / 3) | ((g0 + 2 * g1) / 3) << 8 | ((r0 + 2 * r1) / 3) << 16 | 0xFF << 24;
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            switch (positionCode)
                                            {
                                                case 0:
                                                    fCol = b0 | g0 << 8 | r0 << 16 | 0xFF << 24;
                                                    break;
                                                case 1:
                                                    fCol = b1 | g1 << 8 | r1 << 16 | 0xFF << 24;
                                                    break;
                                                case 2:
                                                    fCol = ((b0 + b1) / 2) | ((g0 + g1) / 2) << 8 | ((r0 + r1) / 2) << 16 | 0xFF << 24;
                                                    break;
                                                case 3:
                                                    fCol = (stripAlpha ? 0xFF : 0x00) << 24;
                                                    break;
                                            }
                                        }

                                        bitmapBW.Write(fCol);
                                    }
                                }
                            }
                        }
                    }
                }

                return bitmapStream.ToArray();
            }
        }
        #endregion
        #region DXT3
        private static byte[] DXT3ToARGB(byte[] imgData, int w, int h)
        {
            return UncompressDXT3(imgData, w, h);
        }

        private static Bitmap DXT3ToBitmap(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressDXT3(imgData, w, h, true);
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0,
                                                    bmp.Width,
                                                    bmp.Height),
                                      ImageLockMode.WriteOnly,
                                      bmp.PixelFormat);

                Marshal.Copy(imageData, 0, bmpData.Scan0, imageData.Length);
                bmp.UnlockBits(bmpData);
            }
            return bmp;
        }

        private static PngBitmapEncoder DXT3ToPng(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressDXT3(imgData, w, h);
            PngBitmapEncoder png = new PngBitmapEncoder();
            BitmapSource image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, imageData, w * 4);
            png.Frames.Add(BitmapFrame.Create(image));
            return png;
        }

        private static byte[] UncompressDXT3(byte[] imgData, int w, int h, bool stripAlpha = false)
        {
            const int bufferSize = 16;
            byte[] blockStorage = new byte[bufferSize];
            using (MemoryStream bitmapStream = new MemoryStream(w * h * 2))
            {
                using (BinaryWriter bitmapBW = new BinaryWriter(bitmapStream))
                {
                    int ptr = 0;
                    for (int s = 0; s < h; s += 4)
                    {
                        for (int t = 0; t < w; t += 4)
                        {
                            Buffer.BlockCopy(imgData, ptr, blockStorage, 0, bufferSize);
                            ptr += bufferSize;
                            {
                                int color0 = blockStorage[8] | blockStorage[9] << 8;
                                int color1 = blockStorage[10] | blockStorage[11] << 8;

                                int temp;

                                temp = (color0 >> 11) * 255 + 16;
                                int r0 = ((temp >> 5) + temp) >> 5;
                                temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
                                int g0 = ((temp >> 6) + temp) >> 6;
                                temp = (color0 & 0x001F) * 255 + 16;
                                int b0 = ((temp >> 5) + temp) >> 5;

                                temp = (color1 >> 11) * 255 + 16;
                                int r1 = ((temp >> 5) + temp) >> 5;
                                temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
                                int g1 = ((temp >> 6) + temp) >> 6;
                                temp = (color1 & 0x001F) * 255 + 16;
                                int b1 = ((temp >> 5) + temp) >> 5;

                                int code = blockStorage[12] | blockStorage[13] << 8 | blockStorage[14] << 16 | blockStorage[15] << 24;

                                for (int j = 0; j < 4; j++)
                                {
                                    bitmapStream.Seek(((s + j) * w * 4) + (t * 4), SeekOrigin.Begin);
                                    for (int i = 0; i < 4; i++)
                                    {
                                        byte alpha = (byte)((blockStorage[(j * i) < 8 ? 0 : 1] >> (((i * j) % 8) * 4)) & 0xFF);
                                        alpha = (byte)((alpha << 4) | alpha);
                                        if (stripAlpha)
                                            alpha = 0xFF;

                                        int fCol = 0;
                                        int colorCode = (code >> 2 * (4 * j + i)) & 0x03;

                                        switch (colorCode)
                                        {
                                            case 0:
                                                fCol = b0 | g0 << 8 | r0 << 16 | 0xFF << alpha;
                                                break;
                                            case 1:
                                                fCol = b1 | g1 << 8 | r1 << 16 | 0xFF << alpha;
                                                break;
                                            case 2:
                                                fCol = (2 * b0 + b1) / 3 | (2 * g0 + g1) / 3 << 8 | (2 * r0 + r1) / 3 << 16 | 0xFF << alpha;
                                                break;
                                            case 3:
                                                fCol = (b0 + 2 * b1) / 3 | (g0 + 2 * g1) / 3 << 8 | (r0 + 2 * r1) / 3 << 16 | 0xFF << alpha;
                                                break;
                                        }

                                        bitmapBW.Write(fCol);
                                    }
                                }
                            }
                        }
                    }

                    return bitmapStream.ToArray();
                }
            }
        }
        #endregion
        #region DXT5
        private static byte[] DXT5ToARGB(byte[] imgData, int w, int h)
        {
            return UncompressDXT5(imgData, w, h);
        }

        private static Bitmap DXT5ToBitmap(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressDXT5(imgData, w, h, true);
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0,
                                                    bmp.Width,
                                                    bmp.Height),
                                      ImageLockMode.WriteOnly,
                                      bmp.PixelFormat);

                Marshal.Copy(imageData, 0, bmpData.Scan0, imageData.Length);
                bmp.UnlockBits(bmpData);
            }
            return bmp;
        }

        private static PngBitmapEncoder DXT5ToPng(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressDXT5(imgData, w, h);
            PngBitmapEncoder png = new PngBitmapEncoder();
            BitmapSource image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, imageData, w * 4);
            png.Frames.Add(BitmapFrame.Create(image));
            return png;
        }

        private static byte[] UncompressDXT5(byte[] imgData, int w, int h, bool stripAlpha = false)
        {
            const int bufferSize = 16;
            byte[] blockStorage = new byte[bufferSize];
            using (MemoryStream bitmapStream = new MemoryStream(w * h * 2))
            {
                using (BinaryWriter bitmapBW = new BinaryWriter(bitmapStream))
                {
                    int ptr = 0;
                    for (int s = 0; s < h; s += 4)
                    {
                        for (int t = 0; t < w; t += 4)
                        {
                            Buffer.BlockCopy(imgData, ptr, blockStorage, 0, bufferSize);
                            ptr += bufferSize;
                            {
                                byte alpha0 = blockStorage[0];
                                byte alpha1 = blockStorage[1];

                                uint alphaCode1 = (uint)(blockStorage[4] | (blockStorage[5] << 8) | (blockStorage[6] << 16) | (blockStorage[7] << 24));
                                ushort alphaCode2 = (ushort)(blockStorage[2] | (blockStorage[3] << 8));

                                int color0 = blockStorage[8] | blockStorage[9] << 8;
                                int color1 = blockStorage[10] | blockStorage[11] << 8;

                                int temp;

                                temp = (color0 >> 11) * 255 + 16;
                                int r0 = ((temp >> 5) + temp) >> 5;
                                temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
                                int g0 = ((temp >> 6) + temp) >> 6;
                                temp = (color0 & 0x001F) * 255 + 16;
                                int b0 = ((temp >> 5) + temp) >> 5;

                                temp = (color1 >> 11) * 255 + 16;
                                int r1 = ((temp >> 5) + temp) >> 5;
                                temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
                                int g1 = ((temp >> 6) + temp) >> 6;
                                temp = (color1 & 0x001F) * 255 + 16;
                                int b1 = ((temp >> 5) + temp) >> 5;

                                int code = blockStorage[12] | blockStorage[13] << 8 | blockStorage[14] << 16 | blockStorage[15] << 24;

                                for (int j = 0; j < 4; j++)
                                {
                                    bitmapStream.Seek(((s + j) * w * 4) + (t * 4), SeekOrigin.Begin);
                                    for (int i = 0; i < 4; i++)
                                    {
                                        int alphaCodeIndex = 3 * (4 * j + i);
                                        int alphaCode;

                                        if (alphaCodeIndex <= 12)
                                        {
                                            alphaCode = (alphaCode2 >> alphaCodeIndex) & 0x07;
                                        }
                                        else if (alphaCodeIndex == 15)
                                        {
                                            alphaCode = (int)((uint)(alphaCode2 >> 15) | ((alphaCode1 << 1) & 0x06));
                                        }
                                        else
                                        {
                                            alphaCode = (int)((alphaCode1 >> (alphaCodeIndex - 16)) & 0x07);
                                        }

                                        byte alpha;
                                        if (alphaCode == 0)
                                        {
                                            alpha = alpha0;
                                        }
                                        else if (alphaCode == 1)
                                        {
                                            alpha = alpha1;
                                        }
                                        else
                                        {
                                            if (alpha0 > alpha1)
                                            {
                                                alpha = (byte)(((8 - alphaCode) * alpha0 + (alphaCode - 1) * alpha1) / 7);
                                            }
                                            else
                                            {
                                                if (alphaCode == 6)
                                                    alpha = 0;
                                                else if (alphaCode == 7)
                                                    alpha = 255;
                                                else
                                                    alpha = (byte)(((6 - alphaCode) * alpha0 + (alphaCode - 1) * alpha1) / 5);
                                            }
                                        }

                                        if (stripAlpha)
                                            alpha = 0xFF;

                                        int fCol = 0;
                                        int colorCode = (code >> 2 * (4 * j + i)) & 0x03;

                                        switch (colorCode)
                                        {
                                            case 0:
                                                fCol = b0 | g0 << 8 | r0 << 16 | alpha << 24;
                                                break;
                                            case 1:
                                                fCol = b1 | g1 << 8 | r1 << 16 | alpha << 24;
                                                break;
                                            case 2:
                                                fCol = (2 * b0 + b1) / 3 | (2 * g0 + g1) / 3 << 8 | (2 * r0 + r1) / 3 << 16 | alpha << 24;
                                                break;
                                            case 3:
                                                fCol = (b0 + 2 * b1) / 3 | (g0 + 2 * g1) / 3 << 8 | (r0 + 2 * r1) / 3 << 16 | alpha << 24;
                                                break;
                                        }

                                        bitmapBW.Write(fCol);
                                    }
                                }
                            }
                        }
                    }

                    return bitmapStream.ToArray();
                }
            }
        }
        #endregion
        #region V8U8
        private static byte[] V8U8ToARGB(byte[] imgData, int w, int h)
        {
            return UncompressV8U8(imgData, w, h);
        }

        private static Bitmap V8U8ToBitmap(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressV8U8(imgData, w, h);
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0,
                                                    bmp.Width,
                                                    bmp.Height),
                                      ImageLockMode.WriteOnly,
                                      bmp.PixelFormat);

                Marshal.Copy(imageData, 0, bmpData.Scan0, imageData.Length);
                bmp.UnlockBits(bmpData);
            }
            return bmp;
        }

        private static PngBitmapEncoder V8U8ToPng(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressV8U8(imgData, w, h);
            PngBitmapEncoder png = new PngBitmapEncoder();
            BitmapSource image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, imageData, w * 4);
            png.Frames.Add(BitmapFrame.Create(image));
            return png;
        }

        private static byte[] UncompressV8U8(byte[] imgData, int w, int h)
        {
            using (MemoryStream bitmapStream = new MemoryStream(w * h * 2))
            {
                using (BinaryWriter bitmapBW = new BinaryWriter(bitmapStream))
                {
                    int ptr = 0;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            sbyte red = (sbyte)Buffer.GetByte(imgData, ptr++);
                            sbyte green = (sbyte)Buffer.GetByte(imgData, ptr++);
                            byte blue = 0xFF;

                            int fCol = blue | (128 + green) << 8 | (128 + red) << 16 | 255 << 24;
                            bitmapBW.Write(fCol);
                        }
                    }

                    return bitmapStream.ToArray();
                }
            }
        }
        #endregion
        #region ATI2
        private static byte[] ATI2ToARGB(byte[] imgData, int w, int h)
        {
            const int bytesPerPixel = 4;
            byte[] imageData = UncompressATI2(imgData, w, h);
            if (imageData.Length != (w * h * bytesPerPixel))
                throw new FormatException("Incorect length of generated data array");
            return imageData;
        }

        private static Bitmap ATI2ToBitmap(byte[] imgData, int w, int h)
        {
            const int bytesPerPixel = 4;
            byte[] imageData = UncompressATI2(imgData, w, h);
            if (imageData.Length != (w * h * bytesPerPixel))
                throw new FormatException("Incorect length of generated data array");
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(imageData, 0, bmpData.Scan0, imageData.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        private static PngBitmapEncoder ATI2ToPng(byte[] imgData, int w, int h)
        {
            byte[] imageData = UncompressATI2(imgData, w, h);
            PngBitmapEncoder png = new PngBitmapEncoder();
            BitmapSource image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, imageData, w * 4);
            png.Frames.Add(BitmapFrame.Create(image));
            return png;
        }

        private static byte[] UncompressATI2(byte[] imgData, int w, int h)
        {
            const int bufferSize = 16;
            const int bytesPerPixel = 4;
            byte[] blockStorage = new byte[bufferSize];
            using (MemoryStream bitmapStream = new MemoryStream(w * h * 2))
            {
                int ptr = 0;
                for (int s = 0; s < h; s += 4)
                {
                    for (int t = 0; t < w; t += 4)
                    {
                        Buffer.BlockCopy(imgData, ptr, blockStorage, 0, bufferSize);
                        ptr += bufferSize;
                        #region Block Decompression Loop
                        byte[][] rgbVals = new byte[3][];
                        byte[] blueVals = new byte[bufferSize];
                        for (int j = 1; j >= 0; j--)
                        {
                            byte colour0 = blockStorage[j * 8]; // First 2 bytes are the min and max vals to be interpolated between
                            byte colour1 = blockStorage[1 + (j * 8)];
                            ulong longRep = BitConverter.ToUInt64(blockStorage, j * 8);
                            byte[] colVals = new byte[bufferSize];

                            for (int k = bufferSize - 1; k >= 0; k--)
                            {
                                ulong tempLong = longRep | (ulong)ATI2BitCodes.interpColor5; // Set all trailing bits to 1

                                if ((tempLong ^ (ulong)ATI2BitCodes.color0) == (ulong)ATI2BitCodes.result) // First 2 values mean to use the specified min or max values
                                {
                                    colVals[k] = colour0;
                                }
                                else if ((tempLong ^ (ulong)ATI2BitCodes.color1) == (ulong)ATI2BitCodes.result)
                                {
                                    colVals[k] = colour1;
                                }
                                else if ((tempLong ^ (ulong)ATI2BitCodes.interpColor0) == (ulong)ATI2BitCodes.result) // Remaining values interpolate the min/max
                                {
                                    if (colour0 > colour1)
                                        colVals[k] = (byte)((6 * colour0 + colour1) / 7);
                                    else
                                        colVals[k] = (byte)((4 * colour0 + colour1) / 5);
                                }
                                else if ((tempLong ^ (ulong)ATI2BitCodes.interpColor1) == (ulong)ATI2BitCodes.result)
                                {
                                    if (colour0 > colour1)
                                        colVals[k] = (byte)((5 * colour0 + 2 * colour1) / 7);
                                    else
                                        colVals[k] = (byte)((3 * colour0 + 2 * colour1) / 5);
                                }
                                else if ((tempLong ^ (ulong)ATI2BitCodes.interpColor2) == (ulong)ATI2BitCodes.result)
                                {
                                    if (colour0 > colour1)
                                        colVals[k] = (byte)((4 * colour0 + 3 * colour1) / 7);
                                    else
                                        colVals[k] = (byte)((2 * colour0 + 3 * colour1) / 5);
                                }
                                else if ((tempLong ^ (ulong)ATI2BitCodes.interpColor3) == (ulong)ATI2BitCodes.result)
                                {
                                    if (colour0 > colour1)
                                        colVals[k] = (byte)((3 * colour0 + 4 * colour1) / 7);
                                    else
                                        colVals[k] = (byte)((colour0 + 4 * colour1) / 5);
                                }
                                else if ((tempLong ^ (ulong)ATI2BitCodes.interpColor4) == (ulong)ATI2BitCodes.result)
                                {
                                    if (colour0 > colour1)
                                        colVals[k] = (byte)((2 * colour0 + 5 * colour1) / 7);
                                    else
                                        colVals[k] = (byte)0;
                                }
                                else if ((tempLong ^ (ulong)ATI2BitCodes.interpColor5) == (ulong)ATI2BitCodes.result)
                                {
                                    if (colour0 > colour1)
                                        colVals[k] = (byte)((colour0 + 6 * colour1) / 7);
                                    else
                                        colVals[k] = (byte)255;
                                }
                                else
                                {
                                    throw new FormatException("Unknown bit value found. This shouldn't be possible...");
                                }
                                longRep <<= 3;
                            }
                            int index = (j == 0) ? 0 : 1;
                            rgbVals[index] = colVals;
                        }
                        for (int j = 0; j < bufferSize; j++)
                        {
                            if (rgbVals[0][j] <= 20 && rgbVals[1][j] <= 20)
                                blueVals[j] = 128;
                            else
                                blueVals[j] = 255;
                        }
                        rgbVals[2] = blueVals;
                        #endregion

                        for (int i = 0; i < 4; i++)
                        {
                            bitmapStream.Seek(((s + i) * w * bytesPerPixel) + (t * bytesPerPixel), SeekOrigin.Begin);
                            for (int j = 0; j < 4; j++)
                            {
                                bitmapStream.WriteByte(rgbVals[2][(i * 4) + j]);
                                bitmapStream.WriteByte(rgbVals[0][(i * 4) + j]);
                                bitmapStream.WriteByte(rgbVals[1][(i * 4) + j]);
                                bitmapStream.WriteByte(255);
                            }
                        }
                    }
                }

                return bitmapStream.ToArray();
            }
        }
        #endregion
        #region A8R8G8B8
        private static Bitmap ARGBToBitmap(byte[] imgData, int w, int h)
        {
            if (imgData.Length != (w * h * 4))
                throw new ArgumentException("Input array is not correct size");
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(imgData, 0, bmpData.Scan0, imgData.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        private static PngBitmapEncoder RGBAToPng(byte[] imgData, int w, int h)
        {
            if (imgData.Length != (w * h * 4))
                throw new ArgumentException("Input array is not correct size");
            PngBitmapEncoder png = new PngBitmapEncoder();
            BitmapSource image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, imgData, w * 4);
            png.Frames.Add(BitmapFrame.Create(image));
            return png;
        }

        #endregion
        #region R8G8B8
        private static byte[] RGBToARGB(byte[] imgData, int w, int h)
        {
            if (imgData.Length != (w * h * 3))
                throw new ArgumentException("Input array is not correct size");
            byte[] buff = new byte[w * h * 4];
            for (int i = 0; i < (w * h); i++)
            {
                buff[(4 * i) + 0] = imgData[(3 * i) + 0];
                buff[(4 * i) + 1] = imgData[(3 * i) + 1];
                buff[(4 * i) + 2] = imgData[(3 * i) + 2];
                buff[(4 * i) + 3] = 255;
            }
            return buff;
        }

        private static Bitmap RGBToBitmap(byte[] imgData, int w, int h)
        {
            if (imgData.Length != (w * h * 3))
                throw new ArgumentException("Input array is not correct size");
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(imgData, 0, bmpData.Scan0, imgData.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        private static PngBitmapEncoder RGBToPng(byte[] imgData, int w, int h)
        {
            if (imgData.Length != (w * h * 3))
                throw new ArgumentException("Input array is not correct size");
            PngBitmapEncoder png = new PngBitmapEncoder();
            BitmapSource image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Rgb24, null, imgData, w * 3);
            png.Frames.Add(BitmapFrame.Create(image));
            return png;
        }
        #endregion
        #region G8
        private static byte[] G8ToARGB(byte[] imgData, int w, int h)
        {
            if (imgData.Length != (w * h))
                throw new ArgumentException("Input array is not correct size");
            byte[] buff = new byte[w * h * 4];
            for (int i = 0; i < (w * h); i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    buff[(4 * i) + j] = imgData[i];
                }
                buff[(4 * i) + 3] = 0;
            }

            return buff;
        }

        private static Bitmap G8ToBitmap(byte[] imgData, int w, int h)
        {
            if (imgData.Length != (w * h))
                throw new ArgumentException("Input array is not correct size");
            byte[] buff = new byte[w * h * 3];
            for (int i = 0; i < (w * h); i++)
            {
                for (int j = 0; j < 3; j++)
                    buff[(3 * i) + j] = imgData[i];
            }

            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(buff, 0, bmpData.Scan0, buff.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        private static PngBitmapEncoder G8ToPng(byte[] imgData, int w, int h)
        {
            if (imgData.Length != (w * h))
                throw new ArgumentException("Input array is not correct size");
            byte[] buff = new byte[w * h * 3];
            for (int i = 0; i < (w * h); i++)
            {
                for (int j = 0; j < 3; j++)
                    buff[(3 * i) + j] = imgData[i];
            }
            PngBitmapEncoder png = new PngBitmapEncoder();
            BitmapSource image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Rgb24, null, buff, w * 3);
            png.Frames.Add(BitmapFrame.Create(image));
            return png;
        }
        #endregion

        private void Read_DDS_HEADER(DDS_HEADER h, BinaryReader r)
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

        private void Write_DDS_HEADER(Stream s)
        {
            s.WriteInt32(DDS_HEADER_size);
            s.WriteUInt32(DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_MIPMAPCOUNT | DDSD_PIXELFORMAT | DDSD_LINEARSIZE);
            s.WriteInt32(mipMaps[0].height);
            s.WriteInt32(mipMaps[0].width);
            s.WriteInt32((int)getBytesPerPixel(mipMaps[0].ddsFormat) * mipMaps[0].width * mipMaps[0].height);
            s.WriteUInt32(0); // dwDepth
            s.WriteInt32(mipMaps.Count);
            s.WriteZeros(44); // dwReserved1
            Write_DDS_PIXELFORMAT(mipMaps[0].ddsFormat, s);
            s.WriteInt32(DDSCAPS_COMPLEX | DDSCAPS_MIPMAP | DDSCAPS_TEXTURE);
            s.WriteUInt32(0); // dwCaps2
            s.WriteUInt32(0); // dwCaps3
            s.WriteUInt32(0); // dwCaps4
            s.WriteUInt32(0); // dwReserved2
        }

        private void Read_DDS_PIXELFORMAT(DDS_PIXELFORMAT p, BinaryReader r)
        {
            p.dwSize = r.ReadUInt32();
            p.dwFlags = r.ReadUInt32();
            p.dwFourCC = r.ReadUInt32();
            p.dwRGBBitCount = r.ReadUInt32();
            p.dwRBitMask = r.ReadUInt32();
            p.dwGBitMask = r.ReadUInt32();
            p.dwBBitMask = r.ReadUInt32();
            p.dwABitMask = r.ReadUInt32();
        }

        private void Write_DDS_PIXELFORMAT(DDSFormat p, Stream s)
        {
            DDS_PIXELFORMAT fmt = getDDSPixelFormat(p);
            s.WriteInt32(DDS_PIXELFORMAT_size);
            s.WriteUInt32(fmt.dwFlags);
            s.WriteUInt32(fmt.dwFourCC);
            s.WriteUInt32(fmt.dwRGBBitCount);
            s.WriteUInt32(fmt.dwRBitMask);
            s.WriteUInt32(fmt.dwGBitMask);
            s.WriteUInt32(fmt.dwBBitMask);
            s.WriteUInt32(fmt.dwABitMask);
        }
    }

    public class DDS_HEADER
    {
        public int dwSize;
        public int dwFlags;
        public int dwHeight;
        public int dwWidth;
        public int dwPitchOrLinearSize;
        public int dwDepth;
        public int dwMipMapCount;
        public int[] dwReserved1 = new int[11];
        public DDS_PIXELFORMAT ddspf = new DDS_PIXELFORMAT();
        public int dwCaps;
        public int dwCaps2;
        public int dwCaps3;
        public int dwCaps4;
        public int dwReserved2;
    }

    public class DDS_PIXELFORMAT
    {
        public uint dwSize;
        public uint dwFlags;
        public uint dwFourCC;
        public uint dwRGBBitCount;
        public uint dwRBitMask;
        public uint dwGBitMask;
        public uint dwBBitMask;
        public uint dwABitMask;
    }
}
