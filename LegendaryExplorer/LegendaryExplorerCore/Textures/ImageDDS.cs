/*
 * MassEffectModder
 *
 * Copyright (C) 2016-2019 Pawel Kolodziejski
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Textures
{
    public partial class Image
    {
        struct DDS_PF
        {
            public uint flags;
            public uint fourCC;
            public uint bits;
            public uint Rmask;
            public uint Gmask;
            public uint Bmask;
            public uint Amask;
        }

        public const int DDS_TAG = 0x20534444;
        private const int DDS_HEADER_dwSize = 124;
        private const int DDS_PIXELFORMAT_dwSize = 32;

        private const int DDPF_ALPHAPIXELS = 0x1;
        private const int DDPF_FOURCC = 0x4;
        private const int DDPF_RGB = 0x40;
        private const int DDPF_LUMINANCE = 0x20000;
        private const int DDPF_SIGNED = 0x80000;

        private const int FOURCC_DX10_TAG = 0x30315844;
        private const int FOURCC_DXT1_TAG = 0x31545844;
        private const int FOURCC_DXT3_TAG = 0x33545844;
        private const int FOURCC_DXT5_TAG = 0x35545844;
        private const int FOURCC_ATI2_TAG = 0x32495441;

        private const int DDSD_CAPS = 0x1;
        private const int DDSD_HEIGHT = 0x2;
        private const int DDSD_WIDTH = 0x4;
        private const int DDSD_PIXELFORMAT = 0x1000;
        private const int DDSD_MIPMAPCOUNT = 0x20000;
        private const int DDSD_LINEARSIZE = 0x80000;

        private const int DDSCAPS_COMPLEX = 0x8;
        private const int DDSCAPS_TEXTURE = 0x1000;
        private const int DDSCAPS_MIPMAP = 0x400000;

        private DDS_PF ddsPixelFormat = new DDS_PF();
        private uint DDSflags;

        private void LoadImageDDS(MemoryStream stream)
        {
            if (stream.ReadUInt32() != DDS_TAG)
                throw new Exception("not DDS tag");

            if (stream.ReadInt32() != DDS_HEADER_dwSize)
                throw new Exception("wrong DDS header dwSize");

            DDSflags = stream.ReadUInt32();

            int dwHeight = stream.ReadInt32();
            int dwWidth = stream.ReadInt32();
            if (!BitOperations.IsPow2(dwWidth) ||
                !BitOperations.IsPow2(dwHeight))
                throw new TextureSizeNotPowerOf2Exception();

            stream.Skip(8); // dwPitchOrLinearSize, dwDepth

            int dwMipMapCount = stream.ReadInt32();
            if (dwMipMapCount == 0)
                dwMipMapCount = 1;

            stream.Skip(11 * 4); // dwReserved1
            stream.SkipInt32(); // ppf.dwSize

            ddsPixelFormat.flags = stream.ReadUInt32();
            ddsPixelFormat.fourCC = stream.ReadUInt32();
            if ((ddsPixelFormat.flags & DDPF_FOURCC) != 0 && ddsPixelFormat.fourCC == FOURCC_DX10_TAG)
                throw new Exception("DX10 DDS format not supported");

            ddsPixelFormat.bits = stream.ReadUInt32();
            ddsPixelFormat.Rmask = stream.ReadUInt32();
            ddsPixelFormat.Gmask = stream.ReadUInt32();
            ddsPixelFormat.Bmask = stream.ReadUInt32();
            ddsPixelFormat.Amask = stream.ReadUInt32();

            switch (ddsPixelFormat.fourCC)
            {
                case 0:
                    if (ddsPixelFormat.bits == 32 &&
                        (ddsPixelFormat.flags & DDPF_ALPHAPIXELS) != 0 &&
                           ddsPixelFormat.Rmask == 0xFF0000 &&
                           ddsPixelFormat.Gmask == 0xFF00 &&
                           ddsPixelFormat.Bmask == 0xFF &&
                           ddsPixelFormat.Amask == 0xFF000000)
                    {
                        pixelFormat = PixelFormat.ARGB;
                        break;
                    }
                    if (ddsPixelFormat.bits == 24 &&
                           ddsPixelFormat.Rmask == 0xFF0000 &&
                           ddsPixelFormat.Gmask == 0xFF00 &&
                           ddsPixelFormat.Bmask == 0xFF)
                    {
                        pixelFormat = PixelFormat.RGB;
                        break;
                    }
                    if (ddsPixelFormat.bits == 16 &&
                           ddsPixelFormat.Rmask == 0xFF &&
                           ddsPixelFormat.Gmask == 0xFF00 &&
                           ddsPixelFormat.Bmask == 0x00)
                    {
                        pixelFormat = PixelFormat.V8U8;
                        break;
                    }
                    if (ddsPixelFormat.bits == 8 &&
                           ddsPixelFormat.Rmask == 0xFF &&
                           ddsPixelFormat.Gmask == 0x00 &&
                           ddsPixelFormat.Bmask == 0x00)
                    {
                        pixelFormat = PixelFormat.G8;
                        break;
                    }
                    throw new Exception("Not supported DDS format");

                case 21:
                    pixelFormat = PixelFormat.ARGB;
                    break;

                case 20:
                    pixelFormat = PixelFormat.RGB;
                    break;

                case 60:
                    pixelFormat = PixelFormat.V8U8;
                    break;

                case 50:
                    pixelFormat = PixelFormat.G8;
                    break;

                case FOURCC_DXT1_TAG:
                    pixelFormat = PixelFormat.DXT1;
                    break;

                case FOURCC_DXT3_TAG:
                    pixelFormat = PixelFormat.DXT3;
                    break;

                case FOURCC_DXT5_TAG:
                    pixelFormat = PixelFormat.DXT5;
                    break;

                case FOURCC_ATI2_TAG:
                    pixelFormat = PixelFormat.ATI2;
                    break;

                default:
                    throw new Exception("Not supported DDS format");
            }
            stream.Skip(5 * 4); // dwCaps, dwCaps2, dwCaps3, dwCaps4, dwReserved2

            for (int i = 0; i < dwMipMapCount; i++)
            {
                int w = dwWidth >> i;
                int h = dwHeight >> i;
                int origW = w;
                int origH = h;
                if (origW == 0 && origH != 0)
                    origW = 1;
                if (origH == 0 && origW != 0)
                    origH = 1;
                w = origW;
                h = origH;

                if (pixelFormat == PixelFormat.DXT1 ||
                    pixelFormat == PixelFormat.DXT3 ||
                    pixelFormat == PixelFormat.DXT5)
                {
                    if (w < 4)
                        w = 4;
                    if (h < 4)
                        h = 4;
                }

                byte[] tempData;
                try
                {
                    tempData = stream.ReadToBuffer(MipMap.getBufferSize(w, h, pixelFormat));
                }
                catch
                {
                    throw new Exception("not enough data in stream");
                }

                mipMaps.Add(new MipMap(tempData, origW, origH, pixelFormat));
            }
        }

        public bool checkDDSHaveAllMipmaps()
        {
            if ((DDSflags & DDSD_MIPMAPCOUNT) != 0 && mipMaps.Count > 1)
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

        public LegendaryExplorerCore.Textures.Image convertToARGB()
        {
            for (int i = 0; i < mipMaps.Count; i++)
            {
                int width = mipMaps[i].width;
                int height = mipMaps[i].height;
                mipMaps[i] = new MipMap(convertRawToARGB(mipMaps[i].data, ref width, ref height, pixelFormat),
                                        mipMaps[i].width, mipMaps[i].height, PixelFormat.ARGB);
            }
            pixelFormat = PixelFormat.ARGB;

            return this;
        }

        public LegendaryExplorerCore.Textures.Image convertToRGB()
        {
            for (int i = 0; i < mipMaps.Count; i++)
            {
                mipMaps[i] = new MipMap(convertRawToRGB(mipMaps[i].data, mipMaps[i].width, mipMaps[i].height, pixelFormat),
                    mipMaps[i].width, mipMaps[i].height, PixelFormat.RGB);
            }
            pixelFormat = PixelFormat.RGB;

            return this;
        }

        static private DDS_PF getDDSPixelFormat(PixelFormat format)
        {
            DDS_PF pixelFormat = new DDS_PF();
            switch (format)
            {
                case PixelFormat.DXT1:
                    pixelFormat.flags = DDPF_FOURCC;
                    pixelFormat.fourCC = FOURCC_DXT1_TAG;
                    break;

                case PixelFormat.DXT3:
                    pixelFormat.flags = DDPF_FOURCC | DDPF_ALPHAPIXELS;
                    pixelFormat.fourCC = FOURCC_DXT3_TAG;
                    break;

                case PixelFormat.DXT5:
                    pixelFormat.flags = DDPF_FOURCC | DDPF_ALPHAPIXELS;
                    pixelFormat.fourCC = FOURCC_DXT5_TAG;
                    break;

                case PixelFormat.ATI2:
                    pixelFormat.flags = DDPF_FOURCC;
                    pixelFormat.fourCC = FOURCC_ATI2_TAG;
                    break;

                case PixelFormat.ARGB:
                    pixelFormat.flags = DDPF_ALPHAPIXELS | DDPF_RGB;
                    pixelFormat.bits = 32;
                    pixelFormat.Rmask = 0xFF0000;
                    pixelFormat.Gmask = 0xFF00;
                    pixelFormat.Bmask = 0xFF;
                    pixelFormat.Amask = 0xFF000000;
                    break;

                case PixelFormat.RGB:
                    pixelFormat.flags = DDPF_RGB;
                    pixelFormat.bits = 24;
                    pixelFormat.Rmask = 0xFF0000;
                    pixelFormat.Gmask = 0xFF00;
                    pixelFormat.Bmask = 0xFF;
                    break;

                case PixelFormat.V8U8:
                    pixelFormat.flags = DDPF_SIGNED;
                    pixelFormat.bits = 16;
                    pixelFormat.Rmask = 0xFF;
                    pixelFormat.Gmask = 0xFF00;
                    break;

                case PixelFormat.G8:
                    pixelFormat.flags = DDPF_LUMINANCE;
                    pixelFormat.bits = 8;
                    pixelFormat.Rmask = 0xFF;
                    break;

                default:
                    throw new Exception("invalid texture format " + pixelFormat);
            }
            return pixelFormat;
        }

        static public byte[] StoreMipToDDS(byte[] src, PixelFormat format, int w, int h)
        {
            MemoryStream stream = new MemoryStream();
            stream.WriteUInt32(DDS_TAG);
            stream.WriteInt32(DDS_HEADER_dwSize);
            stream.WriteUInt32(DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_MIPMAPCOUNT | DDSD_PIXELFORMAT | DDSD_LINEARSIZE);
            stream.WriteInt32(h);
            stream.WriteInt32(w);
            stream.WriteInt32(src.Length);

            stream.WriteUInt32(0); // dwDepth
            stream.WriteInt32(1);
            stream.WriteZeros(44); // dwReserved1

            stream.WriteInt32(DDS_PIXELFORMAT_dwSize);
            DDS_PF pixfmt = getDDSPixelFormat(format);
            stream.WriteUInt32(pixfmt.flags);
            stream.WriteUInt32(pixfmt.fourCC);
            stream.WriteUInt32(pixfmt.bits);
            stream.WriteUInt32(pixfmt.Rmask);
            stream.WriteUInt32(pixfmt.Gmask);
            stream.WriteUInt32(pixfmt.Bmask);
            stream.WriteUInt32(pixfmt.Amask);

            stream.WriteInt32(DDSCAPS_COMPLEX | DDSCAPS_MIPMAP | DDSCAPS_TEXTURE);
            stream.WriteUInt32(0); // dwCaps2
            stream.WriteUInt32(0); // dwCaps3
            stream.WriteUInt32(0); // dwCaps4
            stream.WriteUInt32(0); // dwReserved2

            stream.WriteFromBuffer(src);

            return stream.ToArray();
        }

        public void StoreImageToDDS(Stream stream, PixelFormat format = PixelFormat.Unknown)
        {
            stream.WriteUInt32(DDS_TAG);
            stream.WriteInt32(DDS_HEADER_dwSize);
            stream.WriteUInt32(DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_MIPMAPCOUNT | DDSD_PIXELFORMAT | DDSD_LINEARSIZE);
            stream.WriteInt32(mipMaps[0].height);
            stream.WriteInt32(mipMaps[0].width);

            int dataSize = 0;
            for (int i = 0; i < mipMaps.Count; i++)
            {
                dataSize += MipMap.getBufferSize(mipMaps[i].width, mipMaps[i].height, format == PixelFormat.Unknown ? pixelFormat : format);
            }

            stream.WriteInt32(dataSize);

            stream.WriteUInt32(0); // dwDepth
            stream.WriteInt32(mipMaps.Count);
            stream.WriteZeros(44); // dwReserved1

            stream.WriteInt32(DDS_PIXELFORMAT_dwSize);
            DDS_PF pixfmt = getDDSPixelFormat(format == PixelFormat.Unknown ? pixelFormat : format);
            stream.WriteUInt32(pixfmt.flags);
            stream.WriteUInt32(pixfmt.fourCC);
            stream.WriteUInt32(pixfmt.bits);
            stream.WriteUInt32(pixfmt.Rmask);
            stream.WriteUInt32(pixfmt.Gmask);
            stream.WriteUInt32(pixfmt.Bmask);
            stream.WriteUInt32(pixfmt.Amask);

            stream.WriteInt32(DDSCAPS_COMPLEX | DDSCAPS_MIPMAP | DDSCAPS_TEXTURE);
            stream.WriteUInt32(0); // dwCaps2
            stream.WriteUInt32(0); // dwCaps3
            stream.WriteUInt32(0); // dwCaps4
            stream.WriteUInt32(0); // dwReserved2
            for (int i = 0; i < mipMaps.Count; i++)
            {
                stream.WriteFromBuffer(mipMaps[i].data);
            }
        }

        public byte[] StoreImageToDDS()
        {
            var stream = new MemoryStream();
            StoreImageToDDS(stream);
            return stream.ToArray();
        }

        private static ReadOnlySpan<uint> readBlock4X4BPP4(byte[] src, int srcW, int blockX, int blockY)
        {
            return MemoryMarshal.Cast<byte, uint>(src.AsSpan(blockY * srcW * 2 + blockX * 2 * sizeof(uint), 2 * sizeof(uint)));
        }

        private static ReadOnlySpan<uint> readBlock4X4BPP8(byte[] src, int srcW, int blockX, int blockY)
        {
            return MemoryMarshal.Cast<byte, uint>(src.AsSpan(blockY * srcW * 4 + blockX * 4 * sizeof(uint), 4 * sizeof(uint)));
        }

        static private void writeBlock4X4BPP4(uint[] block, byte[] dst, int dstW, int blockX, int blockY)
        {
            int dstPtr = blockY * dstW * 2 + blockX * 2 * sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(block[0]), 0, dst, dstPtr + 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(block[1]), 0, dst, dstPtr + 4, 4);
        }

        static private void writeBlock4X4BPP8(uint[] block, byte[] dst, int dstW, int blockX, int blockY)
        {
            int dstPtr = blockY * dstW * 4 + blockX * 4 * sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(block[0]), 0, dst, dstPtr + 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(block[1]), 0, dst, dstPtr + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(block[2]), 0, dst, dstPtr + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(block[3]), 0, dst, dstPtr + 12, 4);
        }

        static private void writeBlock4X4ATI2(uint[] blockSrcX, uint[] blockSrcY, byte[] dst, int dstW, int blockX, int blockY)
        {
            int dstPtr = blockY * dstW * 4 + blockX * 4 * sizeof(uint);
            Array.Copy(BitConverter.GetBytes(blockSrcY[0]), 0, dst, dstPtr + (0 * sizeof(uint)), sizeof(uint));
            Array.Copy(BitConverter.GetBytes(blockSrcY[1]), 0, dst, dstPtr + (1 * sizeof(uint)), sizeof(uint));
            Array.Copy(BitConverter.GetBytes(blockSrcX[0]), 0, dst, dstPtr + (2 * sizeof(uint)), sizeof(uint));
            Array.Copy(BitConverter.GetBytes(blockSrcX[1]), 0, dst, dstPtr + (3 * sizeof(uint)), sizeof(uint));
        }

        static private byte[] readBlock4X4ARGB(byte[] srcARGB, int srcW, int blockX, int blockY)
        {
            int srcPitch = srcW * 4;
            int blockPitch = 4 * 4;
            byte[] blockARGB = new byte[4 * blockPitch];
            int srcARGBPtr = (blockY * 4) * srcPitch + blockX * 4 * 4;

            for (int y = 0; y < 4; y++)
            {
                int blockPtr = y * blockPitch;
                int srcARGBPtrY = srcARGBPtr + (y * srcPitch);
                for (int x = 0; x < 4 * 4; x += 4)
                {
                    int srcPtr = srcARGBPtrY + x;
                    blockARGB[blockPtr + 0] = srcARGB[srcPtr + 0];
                    blockARGB[blockPtr + 1] = srcARGB[srcPtr + 1];
                    blockARGB[blockPtr + 2] = srcARGB[srcPtr + 2];
                    blockARGB[blockPtr + 3] = srcARGB[srcPtr + 3];
                    blockPtr += 4;
                }
            }

            return blockARGB;
        }

        static private void writeBlock4X4ARGB(Span<byte> blockARGB, Span<byte> dstARGB, int dstW, int blockX, int blockY)
        {
            int dstPitch = dstW * 4;
            const int blockPitch = 4 * 4;
            int dstARGBPtr = (blockY * 4) * dstPitch + blockX * 4 * 4;

            for (int y = 0; y < 4; y++)
            {
                int blockPtr = y * blockPitch;
                int dstARGBPtrY = dstARGBPtr + (y * dstPitch);
                blockARGB.Slice(blockPtr, blockPitch).CopyTo(dstARGB.Slice(dstARGBPtrY));
            }
        }

        private static void readBlock4X4ATI2(byte[] src, int srcW, byte[] blockDstX, byte[] blockDstY, int blockX, int blockY)
        {
            int srcPitch = srcW * 4;
            int srcPtr = (blockY * 4) * srcPitch + blockX * 4 * 4;

            for (int y = 0; y < 4; y++)
            {
                int srcPtrY = srcPtr + (y * srcPitch);
                for (int x = 0; x < 4; x++)
                {
                    blockDstX[y * 4 + x] = src[srcPtrY + (x * 4) + 2];
                    blockDstY[y * 4 + x] = src[srcPtrY + (x * 4) + 1];
                }
            }
        }

        private static void writeBlock4X4ARGBATI2(Span<byte> blockR, Span<byte> blockG, byte[] dstARGB, int srcW, int blockX, int blockY)
        {
            int dstPitch = srcW * 4;
            const int blockPitch = 4;
            int dstARGBPtr = (blockY * 4) * dstPitch + blockX * 4 * 4;

            for (int y = 0; y < 4; y++)
            {
                int dstARGBPtrY = dstARGBPtr + (y * dstPitch);
                for (int x = 0; x < 4 * 4; x += 4)
                {
                    int blockPtr = y * blockPitch + (x / 4);
                    int dstPtr = dstARGBPtrY + x;
                    dstARGB[dstPtr + 0] = 255;
                    dstARGB[dstPtr + 1] = blockG[blockPtr];
                    dstARGB[dstPtr + 2] = blockR[blockPtr];
                    dstARGB[dstPtr + 3] = 255;
                }
            }
        }

        private static byte[] compressMipmap(PixelFormat dstFormat, byte[] src, int w, int h, bool useDXT1Alpha = false, byte DXT1Threshold = 128)
        {
            if (src.Length != w * h * 4)
                throw new Exception("not ARGB buffer input");
            int blockSize = Codecs.Codecs.BLOCK_SIZE_4X4BPP8;
            if (dstFormat == PixelFormat.DXT1)
                blockSize = Codecs.Codecs.BLOCK_SIZE_4X4BPP4;

            byte[] dst = new byte[blockSize * (w / 4) * (h / 4)];
            int cores = Environment.ProcessorCount;
            int partSize;
            if (w * h < 65536 || w < 256 || h < 16)
            {
                cores = 1;
                partSize = h / 4;
            }
            else
            {
                cores = (int)BitOperations.RoundUpToPowerOf2((uint)cores);
                if ((cores * 4 * 4) > h)
                    cores = h / 4 / 4;
                partSize = h / 4 / cores;
            }
            int[] range = new int[cores + 1];

            for (int p = 1; p <= cores; p++)
                range[p] = (partSize * p);

            Parallel.For(0, cores, p =>
            {
                byte[] srcBlock;
                for (int y = range[p]; y < range[p + 1]; y++)
                {
                    for (int x = 0; x < w / 4; x++)
                    {
                        if (dstFormat == PixelFormat.DXT1)
                        {
                            srcBlock = readBlock4X4ARGB(src, w, x, y);
                            uint[] block = Codecs.Codecs.CompressRGBBlock(srcBlock, true, useDXT1Alpha, DXT1Threshold);
                            writeBlock4X4BPP4(block, dst, w, x, y);
                        }
                        else if (dstFormat == PixelFormat.DXT3)
                        {
                            srcBlock = readBlock4X4ARGB(src, w, x, y);
                            uint[] block = Codecs.Codecs.CompressRGBABlock_ExplicitAlpha(srcBlock);
                            writeBlock4X4BPP8(block, dst, w, x, y);
                        }
                        else if (dstFormat == PixelFormat.DXT5)
                        {
                            srcBlock = readBlock4X4ARGB(src, w, x, y);
                            uint[] block = Codecs.Codecs.CompressRGBABlock(srcBlock);
                            writeBlock4X4BPP8(block, dst, w, x, y);
                        }
                        else if (dstFormat is PixelFormat.ATI2 or PixelFormat.BC5)
                        {
                            byte[] srcBlockX = new byte[Codecs.Codecs.BLOCK_SIZE_4X4BPP8];
                            byte[] srcBlockY = new byte[Codecs.Codecs.BLOCK_SIZE_4X4BPP8];
                            readBlock4X4ATI2(src, w, srcBlockX, srcBlockY, x, y);
                            uint[] blockX = Codecs.Codecs.CompressAlphaBlock(srcBlockX);
                            uint[] blockY = Codecs.Codecs.CompressAlphaBlock(srcBlockY);
                            writeBlock4X4ATI2(blockX, blockY, dst, w, x, y);
                        }
                        else
                            throw new Exception("not supported codec");
                    }
                }
            });

            return dst;
        }

        private static byte[] decompressMipmap(PixelFormat srcFormat, byte[] src, int w, int h)
        {
            byte[] dst = new byte[w * h * 4];
            int cores = Environment.ProcessorCount;
            int partSize;
            if (w * h < 65536 || w < 256 || h < 16)
            {
                cores = 1;
                partSize = h / 4;
            }
            else
            {
                cores = (int)BitOperations.RoundUpToPowerOf2((uint)cores);
                if ((cores * 4 * 4) > h)
                    cores = h / 4 / 4;
                partSize = h / 4 / cores;
            }
            int[] range = new int[cores + 1];

            for (int p = 1; p <= cores; p++)
                range[p] = (partSize * p);

            Parallel.For(0, cores, p =>
            {
                Span<byte> blockDst = stackalloc byte[Codecs.Codecs.BLOCK_SIZE_4X4X4];
                for (int y = range[p]; y < range[p + 1]; y++)
                {
                    for (int x = 0; x < w / 4; x++)
                    {
                        if (srcFormat == PixelFormat.DXT1)
                        {
                            ReadOnlySpan<uint> block = readBlock4X4BPP4(src, w, x, y);
                            Codecs.Codecs.DecompressRGBBlock(block, blockDst, true);
                            writeBlock4X4ARGB(blockDst, dst, w, x, y);
                        }
                        else if (srcFormat == PixelFormat.DXT3)
                        {
                            ReadOnlySpan<uint> block = readBlock4X4BPP8(src, w, x, y);
                            Codecs.Codecs.DecompressRGBABlock_ExplicitAlpha(block, blockDst);
                            writeBlock4X4ARGB(blockDst, dst, w, x, y);
                        }
                        else if (srcFormat == PixelFormat.DXT5)
                        {
                            ReadOnlySpan<uint> block = readBlock4X4BPP8(src, w, x, y);
                            Codecs.Codecs.DecompressRGBABlock(block, blockDst);
                            writeBlock4X4ARGB(blockDst, dst, w, x, y);
                        }
                        else if (srcFormat is PixelFormat.ATI2 or PixelFormat.BC5)
                        {
                            ReadOnlySpan<uint> block = readBlock4X4BPP8(src, w, x, y);
                            var blockX = block.Slice(2, 2);
                            var blockY = block.Slice(0, 2);
                            var blockDstR = blockDst.Slice(0, Codecs.Codecs.BLOCK_SIZE_4X4BPP8);
                            var blockDstG = blockDst.Slice(Codecs.Codecs.BLOCK_SIZE_4X4BPP8, Codecs.Codecs.BLOCK_SIZE_4X4BPP8);
                            Codecs.Codecs.DecompressAlphaBlock(blockX, blockDstR);
                            Codecs.Codecs.DecompressAlphaBlock(blockY, blockDstG);
                            writeBlock4X4ARGBATI2(blockDstR, blockDstG, dst, w, x, y);
                        }
                        else
                        {
                            throw new Exception("not supported codec");
                        }
                    }
                }
            });

            return dst;
        }
    }
}
