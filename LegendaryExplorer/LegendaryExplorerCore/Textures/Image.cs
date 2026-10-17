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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using LegendaryExplorerCore.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace LegendaryExplorerCore.Textures
{
    ///// <summary>
    ///// GamePlatform enumeration, which can be passed to other projects
    ///// </summary>
    //public enum GamePlatform
    //{
    //    PC,
    //    Xenon,
    //    PS3,
    //    WiiU
    //}

    /// <summary>
    /// The available formats for pixels of a texture
    /// </summary>
    public enum PixelFormat
    {
        Unknown, DXT1, DXT3, DXT5, ATI2, V8U8, ARGB, RGB, G8, BC7, BC5
    }

    /// <summary>
    /// Describes a texture MipMap for use with texturing tools.
    /// </summary>
    [DebuggerDisplay("MEM MipMap {width}x{height}")]
    public class MipMap
    {
        /// <summary>
        /// The raw data of the mip
        /// </summary>
        public byte[] data { get; private set; }
        /// <summary>
        /// The width of the mip
        /// </summary>
        public int width { get; private set; }
        /// <summary>
        /// The height of the mip
        /// </summary>
        public int height { get; private set; }
        public int origWidth { get; private set; }
        public int origHeight { get; private set; }

        public MipMap(int w, int h, PixelFormat format)
        {
            width = origWidth = w;
            height = origHeight = h;

            if (format is PixelFormat.DXT1 or PixelFormat.DXT3 or PixelFormat.DXT5 or PixelFormat.ATI2 or PixelFormat.BC5 or PixelFormat.BC7)
            {
                if (width < 4)
                    width = 4;
                if (height < 4)
                    height = 4;
            }

            data = new byte[getBufferSize(width, height, format)];
        }

        public MipMap(byte[] src, int w, int h, PixelFormat format)
        {
            width = origWidth = w;
            height = origHeight = h;

            if (format is PixelFormat.DXT1 or PixelFormat.DXT3 or PixelFormat.DXT5 or PixelFormat.ATI2 or PixelFormat.BC5 or PixelFormat.BC7)
            {
                if (width < 4)
                    width = 4;
                if (height < 4)
                    height = 4;
            }

            if (src.Length != getBufferSize(width, height, format))
                throw new Exception("data size is not valid");
            data = src;
        }

        public static int getBufferSize(int w, int h, PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.ARGB:
                    return 4 * w * h;
                case PixelFormat.RGB:
                    return 3 * w * h;
                case PixelFormat.V8U8:
                    return 2 * w * h;
                case PixelFormat.DXT3:
                case PixelFormat.DXT5:
                case PixelFormat.ATI2:
                case PixelFormat.BC5:
                case PixelFormat.G8:
                case PixelFormat.BC7:
                    return w * h;
                case PixelFormat.DXT1:
                    return (w * h) / 2;
                default:
                    throw new Exception("unknown format");
            }
        }
    }

    public class TextureSizeNotPowerOf2Exception : Exception { }

    [DebuggerDisplay("MEM Image | Num Mips: {mipMaps.Count}")]
    public partial class Image
    {
        public enum ImageFormat
        {
            Unknown, DDS, PNG, BMP, TGA, JPEG
        }

        public List<MipMap> mipMaps { get; set; }
        public PixelFormat pixelFormat { get; private set; } = PixelFormat.Unknown;

        public Image(string fileName, ImageFormat format = ImageFormat.Unknown)
        {
            if (format == ImageFormat.Unknown)
                format = DetectImageByFilename(fileName);

            using FileStream stream = File.OpenRead(fileName);
            LoadImage(new MemoryStream(stream.ReadToBuffer(stream.Length)), format);
        }

        public Image(MemoryStream stream, ImageFormat format)
        {
            LoadImage(stream, format);
        }

        public Image(MemoryStream stream, string extension)
        {
            LoadImage(stream, DetectImageByExtension(extension));
        }

        public Image(byte[] image, ImageFormat format)
        {
            LoadImage(new MemoryStream(image), format);
        }

        public Image(byte[] image, string extension)
        {
            LoadImage(new MemoryStream(image), DetectImageByExtension(extension));
        }

        /// <summary>
        /// Only use if you know what you're doing
        /// </summary>
        private Image() { }

        public Image(List<MipMap> mipmaps, PixelFormat pixelFmt)
        {
            mipMaps = mipmaps;
            pixelFormat = pixelFmt;
        }

        private static ImageFormat DetectImageByFilename(string fileName)
        {
            return DetectImageByExtension(Path.GetExtension(fileName));
        }

        public static Image LoadFromRaw(byte[] rawBytes, PixelFormat format, int width, int height)
        {
            Image image = new Image();
            image.mipMaps = new List<MipMap>();
            image.pixelFormat = format;
            var mipmap = new MipMap(rawBytes, width, height, format);
            image.mipMaps.Add(mipmap);
            return image;
        }

        private static ImageFormat DetectImageByExtension(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".dds":
                    return ImageFormat.DDS;
                case ".tga":
                    return ImageFormat.TGA;
                case ".bmp":
                    return ImageFormat.BMP;
                case ".png":
                    return ImageFormat.PNG;
                case ".jpg":
                case ".jpeg":
                    return ImageFormat.JPEG;
                default:
                    return ImageFormat.Unknown;
            }
        }

        private void LoadImage(MemoryStream stream, ImageFormat format)
        {
            mipMaps = new List<MipMap>();
            switch (format)
            {
                case ImageFormat.DDS:
                    {
                        LoadImageDDS(stream);
                        break;
                    }
                case ImageFormat.TGA:
                    {
                        LoadImageTGA(stream);
                        break;
                    }
                case ImageFormat.BMP:
                    {
                        LoadImageBMP(stream);
                        break;
                    }
                case ImageFormat.PNG:
                    {
                        var image = PngDecoder.Instance.Decode<Rgba32>(new DecoderOptions(), stream);
                        if (!BitOperations.IsPow2(image.Width) || !BitOperations.IsPow2(image.Height))
                            throw new TextureSizeNotPowerOf2Exception();

                        byte[] pixels = SLImageToRawBytes<Rgba32>(image);
                        pixelFormat = PixelFormat.ARGB;

                        var mipmap = new MipMap(pixels, image.Width, image.Height, pixelFormat);
                        mipMaps.Add(mipmap);
                    }
                    break;
                case ImageFormat.JPEG:
                    {
                        // NOTE: This could also be RGB24! IDK how you are supposed to know which one to use
                        var image = JpegDecoder.Instance.Decode<Bgr24>(new DecoderOptions(), stream);
                        if (!BitOperations.IsPow2(image.Width) || !BitOperations.IsPow2(image.Height))
                            throw new TextureSizeNotPowerOf2Exception();

                        byte[] pixels = null;
                        // JPG does not have alpha channel
                        pixels = SLImageToRawBytes(image);
                        pixelFormat = PixelFormat.RGB;

                        var mipmap = new MipMap(pixels, image.Width, image.Height, pixelFormat);
                        mipMaps.Add(mipmap);
                        break;
                    }
                default:
                    throw new Exception();
            }
        }

        public static byte[] ToArray(SixLabors.ImageSharp.Image image, IImageFormat imageFormat)
        {
            using var memoryStream = new MemoryStream();
            var imageEncoder = image.Configuration.ImageFormatsManager.GetEncoder(imageFormat);
            image.Save(memoryStream, imageEncoder);
            return memoryStream.ToArray();
        }

        public static byte[] convertRawToARGB(byte[] src, ref int w, ref int h, PixelFormat format, bool clearAlpha = false)
        {
            byte[] tmpData;

            switch (format)
            {
                case PixelFormat.DXT1:
                case PixelFormat.DXT3:
                case PixelFormat.DXT5:
                    {
                        // Mips smaller than 4x4 are still stored as a full 4x4 block
                        if (w < 4)
                            w = 4;
                        if (h < 4)
                            h = 4;
                        if (src.Length < MipMap.getBufferSize(w, h, format))
                            return new byte[w * h * 4]; // Data written by old versions of LEX lacks block padding
                        tmpData = Image.decompressMipmap(format, src, w, h);
                        break;
                    }
                case PixelFormat.BC5:
                case PixelFormat.ATI2:
                    // Mips smaller than 4x4 are still stored as a full 4x4 block
                    if (w < 4)
                        w = 4;
                    if (h < 4)
                        h = 4;
                    if (src.Length < MipMap.getBufferSize(w, h, format))
                        return new byte[w * h * 4]; // Data written by old versions of LEX lacks block padding
                    tmpData = Image.decompressMipmap(format, src, w, h);
                    if (format == PixelFormat.BC5)
                    {
                        // Swap R and G
                        SwapChannelsARGB(tmpData, 1, 2);
                    }
                    break;
                case PixelFormat.ARGB: tmpData = src; break;
                case PixelFormat.RGB: tmpData = RGBToARGB(src, w, h); break;
                case PixelFormat.V8U8: tmpData = V8U8ToARGB(src, w, h); break;
                case PixelFormat.G8: tmpData = G8ToARGB(src, w, h); break;
                case PixelFormat.BC7: tmpData = BC7ToARGB(src, w, h); break;
                default:
                    throw new Exception("invalid texture format " + format);
            }

            if (clearAlpha)
                clearAlphaFromARGB(tmpData, w, h);

            return tmpData;
        }

        private static byte[] BC7ToARGB(byte[] src, int w, int h)
        {
            var decoder = new BcDecoder();
            var decoded = decoder.DecodeRaw(src, w, h, CompressionFormat.Bc7);
            byte[] bytes = MemoryMarshal.AsBytes(decoded.AsSpan()).ToArray();
            // Swap red and blue channels. 
            // idk if there is faster way to do this
            SwapChannelsARGB(bytes, 0, 2); // decoded data is in ABGR in memory

            return bytes;
        }

        /// <summary>
        /// Swaps two color bytes in an A R G B (not specificaly in this order) byte array
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="c1">Channel index 1</param>
        /// <param name="c2">Channel index 2</param>
        /// <returns></returns>
        private static byte[] SwapChannelsARGB(byte[] bytes, int c1, int c2)
        {
            for (int i = 0; i < bytes.Length / 4; i++)
            {
                var b1 = bytes[i * 4 + c1];
                bytes[i * 4 + c1] = bytes[i * 4 + c2];
                bytes[i * 4 + c2] = b1;
            }

            return bytes;
        }

        private static byte[] ShiftChannels(byte[] bytes, int numRight)
        {
            for (int i = 0; i < bytes.Length / 4; i++)
            {
                var b1 = bytes[i * 4 + 3];
                bytes[i * 4 + 1] = bytes[i * 4];
                bytes[i * 4 + 2] = bytes[i * 4 + 1];
                bytes[i * 4 + 3] = bytes[i * 4 + 2];
                bytes[i * 4] = b1;
            }

            return bytes;
        }

        /// <summary>
        /// Converts a SixLabors Image to raw bytes
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private static byte[] SLImageToRawBytes<TPixel>(Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
        {
            return MemoryMarshal.AsBytes(image.GetPixelMemoryGroup().ToArray()[0].Span).ToArray();
        }

        public static byte[] convertRawToRGB(byte[] src, int w, int h, PixelFormat format)
        {
            return ARGBtoRGB(convertRawToARGB(src, ref w, ref h, format), w, h);
        }

        private static void clearAlphaFromARGB(byte[] src, int w, int h)
        {
            for (int i = 0; i < w * h; i++)
            {
                src[4 * i + 3] = 255;
            }
        }

        private static byte[] RGBToARGB(byte[] src, int w, int h)
        {
            byte[] tmpData = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                tmpData[4 * i + 0] = src[3 * i + 0];
                tmpData[4 * i + 1] = src[3 * i + 1];
                tmpData[4 * i + 2] = src[3 * i + 2];
                tmpData[4 * i + 3] = 255;
            }
            return tmpData;
        }

        private static byte[] ARGBtoRGB(byte[] src, int w, int h)
        {
            byte[] tmpData = new byte[w * h * 3];
            for (int i = 0; i < w * h; i++)
            {
                tmpData[3 * i + 0] = src[4 * i + 0];
                tmpData[3 * i + 1] = src[4 * i + 1];
                tmpData[3 * i + 2] = src[4 * i + 2];
            }
            return tmpData;
        }

        private static byte[] V8U8ToARGB(byte[] src, int w, int h)
        {
            byte[] tmpData = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                tmpData[4 * i + 0] = 255;
                tmpData[4 * i + 1] = (byte)(((sbyte)src[2 * i + 1]) + 128);
                tmpData[4 * i + 2] = (byte)(((sbyte)src[2 * i + 0]) + 128);
                tmpData[4 * i + 3] = 255;
            }
            return tmpData;
        }

        private static byte[] ARGBtoV8U8(byte[] src, int w, int h)
        {
            byte[] tmpData = new byte[w * h * 2];
            for (int i = 0; i < w * h; i++)
            {
                tmpData[2 * i + 0] = (byte)((sbyte)(src[4 * i + 2]) - 128);
                tmpData[2 * i + 1] = (byte)((sbyte)(src[4 * i + 1]) - 128);
            }
            return tmpData;
        }

        private static byte[] G8ToARGB(byte[] src, int w, int h)
        {
            byte[] tmpData = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                tmpData[4 * i + 0] = src[i];
                tmpData[4 * i + 1] = src[i];
                tmpData[4 * i + 2] = src[i];
                tmpData[4 * i + 3] = 255;
            }

            return tmpData;
        }

        private static byte[] ARGBtoG8(byte[] src, int w, int h)
        {
            byte[] tmpData = new byte[w * h];
            for (int i = 0; i < w * h; i++)
            {
                int c = src[i * 4 + 0] + src[i * 4 + 1] + src[i * 4 + 2];
                tmpData[i] = (byte)(c / 3);
            }

            return tmpData;
        }

        // Averages round to nearest. Truncating instead loses about 1.5 counts per level on average, and because
        // each mip is built from the previous one that compounds down the chain - visible as darker low mips in
        // dark, smooth data such as lightmaps.
        internal static byte[] downscaleARGB(byte[] src, int w, int h)
        {
            if (w == 1 && h == 1)
                throw new Exception("1x1 can not be downscaled");

            byte[] tmpData;
            if (w == 1 || h == 1)
            {
                tmpData = new byte[w * h * 2];
                for (int srcPos = 0, dstPos = 0; dstPos < w * h * 2; srcPos += 8)
                {
                    tmpData[dstPos++] = (byte)((uint)(src[srcPos + 0] + src[srcPos + 4 + 0] + 1) >> 1);
                    tmpData[dstPos++] = (byte)((uint)(src[srcPos + 1] + src[srcPos + 4 + 1] + 1) >> 1);
                    tmpData[dstPos++] = (byte)((uint)(src[srcPos + 2] + src[srcPos + 4 + 2] + 1) >> 1);
                    tmpData[dstPos++] = (byte)((uint)(src[srcPos + 3] + src[srcPos + 4 + 3] + 1) >> 1);
                }
            }
            else
            {
                tmpData = new byte[w * h];
                int pitch = w * 4;
                for (int srcPos = 0, dstPos = 0; dstPos < w * h; srcPos += pitch)
                {
                    for (int x = 0; x < (w / 2); x++, srcPos += 8)
                    {
                        tmpData[dstPos++] = (byte)((uint)(src[srcPos + 0] + src[srcPos + 4 + 0] + src[srcPos + pitch + 0] + src[srcPos + pitch + 4 + 0] + 2) >> 2);
                        tmpData[dstPos++] = (byte)((uint)(src[srcPos + 1] + src[srcPos + 4 + 1] + src[srcPos + pitch + 1] + src[srcPos + pitch + 4 + 1] + 2) >> 2);
                        tmpData[dstPos++] = (byte)((uint)(src[srcPos + 2] + src[srcPos + 4 + 2] + src[srcPos + pitch + 2] + src[srcPos + pitch + 4 + 2] + 2) >> 2);
                        tmpData[dstPos++] = (byte)((uint)(src[srcPos + 3] + src[srcPos + 4 + 3] + src[srcPos + pitch + 3] + src[srcPos + pitch + 4 + 3] + 2) >> 2);
                    }
                }
            }

            return tmpData;
        }

        /// <summary>
        /// Pads an ARGB image to block-aligned (multiple of 4) dimensions by replicating edge pixels,
        /// so mips smaller than a 4x4 block can be block-compressed. Updates w and h to the padded dimensions.
        /// </summary>
        private static byte[] padARGBTo4(byte[] src, ref int w, ref int h)
        {
            int paddedW = (w + 3) & ~3;
            int paddedH = (h + 3) & ~3;
            byte[] dst = new byte[paddedW * paddedH * 4];
            for (int y = 0; y < paddedH; y++)
            {
                int srcY = Math.Min(y, h - 1);
                for (int x = 0; x < paddedW; x++)
                {
                    int srcX = Math.Min(x, w - 1);
                    Array.Copy(src, (srcY * w + srcX) * 4, dst, (y * paddedW + x) * 4, 4);
                }
            }
            w = paddedW;
            h = paddedH;
            return dst;
        }

        /// <summary>
        /// Crops an ARGB image to the top-left w x h region.
        /// </summary>
        private static byte[] cropARGB(byte[] src, int srcW, int w, int h)
        {
            byte[] dst = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                Array.Copy(src, y * srcW * 4, dst, y * w * 4, w * 4);
            return dst;
        }

        private static byte[] downscaleRGB(byte[] src, int w, int h)
        {
            if (w == 1 && h == 1)
                throw new Exception("1x1 can not be downscaled");

            byte[] tmpData;
            if (w == 1 || h == 1)
            {
                tmpData = new byte[(w * h * 3) / 2];
                for (int srcPos = 0, dstPos = 0; dstPos < (w * h * 3) / 2; srcPos += 6)
                {
                    tmpData[dstPos++] = (byte)((uint)(src[srcPos + 0] + src[srcPos + 3 + 0] + 1) >> 1);
                    tmpData[dstPos++] = (byte)((uint)(src[srcPos + 1] + src[srcPos + 3 + 1] + 1) >> 1);
                    tmpData[dstPos++] = (byte)((uint)(src[srcPos + 2] + src[srcPos + 3 + 2] + 1) >> 1);
                }
            }
            else
            {
                tmpData = new byte[(w * h * 3) / 4];
                int pitch = w * 3;
                for (int srcPos = 0, dstPos = 0; dstPos < (w * h * 3) / 4; srcPos += pitch)
                {
                    for (int x = 0; x < (w / 2); x++, srcPos += 6)
                    {
                        tmpData[dstPos++] = (byte)((uint)(src[srcPos + 0] + src[srcPos + 3 + 0] + src[srcPos + pitch + 0] + src[srcPos + pitch + 3 + 0] + 2) >> 2);
                        tmpData[dstPos++] = (byte)((uint)(src[srcPos + 1] + src[srcPos + 3 + 1] + src[srcPos + pitch + 1] + src[srcPos + pitch + 3 + 1] + 2) >> 2);
                        tmpData[dstPos++] = (byte)((uint)(src[srcPos + 2] + src[srcPos + 3 + 2] + src[srcPos + pitch + 2] + src[srcPos + pitch + 3 + 2] + 2) >> 2);
                    }
                }
            }

            return tmpData;
        }

        /// <summary>
        /// Converts raw texture data to a PNG file
        /// </summary>
        /// <param name="src">Raw, uncompressed pixel data</param>
        /// <param name="w">The width of the source texture's data</param>
        /// <param name="h">The height of the source texture's data</param>
        /// <param name="format">The format of the uncompressed pixel data</param>
        /// <returns><see cref="MemoryStream"/> of the PNG that was converted. If written to disk, this would be a PNG file.</returns>
        public static MemoryStream convertToPng(byte[] src, int w, int h, PixelFormat format)
        {
            byte[] tmpData = convertRawToARGB(src, ref w, ref h, format);
            var ms = new MemoryStream();
            var im = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(tmpData, w, h);
            im.SaveAsPng(ms);
            ms.Position = 0;
            return ms;
        }

        private static byte[] convertToFormat(PixelFormat srcFormat, byte[] src, int w, int h, PixelFormat dstFormat, bool dxt1HasAlpha = false, byte dxt1Threshold = 128)
        {
            if (dstFormat == PixelFormat.DXT1 && srcFormat == PixelFormat.ARGB)
            {
                // The native BC1 encoder makes every texel with alpha below 255 punch-through transparent black, so resolve
                // the alpha here: cut at the threshold when 1-bit alpha is wanted, opaque otherwise
                src = (byte[])src.Clone();
                for (int i = 3; i < src.Length; i += 4)
                    src[i] = dxt1HasAlpha && src[i] < dxt1Threshold ? (byte)0 : (byte)255;
            }
            return TexConverter.ConvertTexture(src, (uint)w, (uint)h, srcFormat, dstFormat);
        }

        private static byte[] convertRawToBC(byte[] imageBytes, int w, int h, PixelFormat dstFormat)
        {
#if WINDOWS
            // todo: Use native method on windows
#endif

            if (w < 4 || h < 4)
            {
                // Block compression works on whole 4x4 blocks; pad undersized mips before compressing
                imageBytes = padARGBTo4(imageBytes, ref w, ref h);
            }
            var i = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(imageBytes, w, h);
            var encoder = new BcEncoder();
            encoder.OutputOptions.GenerateMipMaps = false;
            encoder.OutputOptions.Quality = CompressionQuality.Balanced;
            encoder.OutputOptions.Format = dstFormat == PixelFormat.BC5 ? CompressionFormat.Bc5 : CompressionFormat.Bc7;
            return encoder.EncodeToRawBytes(i, 0, out var mipW, out var mipH);
        }

        /// <summary>
        /// Replaces every mip below the top one with a freshly generated chain (down to 1x1), converted to
        /// <paramref name="dstFormat"/>. Any lower mips already on the image are discarded.
        /// </summary>
        /// <param name="dstFormat">The format of the finished chain.</param>
        /// <param name="dxt1HasAlpha">Whether a DXT1 target should keep 1-bit alpha.</param>
        /// <param name="dxt1Threshold">Alpha cut-off for DXT1 1-bit alpha.</param>
        /// <param name="options">How to filter the lower mips. Null (or <see cref="MipGenerationOptions.Default"/>) is the
        /// historical box filter. With any other options the top mip is left as it is whenever no format conversion is needed,
        /// so it is never recompressed.</param>
        public void correctMips(PixelFormat dstFormat, bool dxt1HasAlpha = false, byte dxt1Threshold = 128, MipGenerationOptions options = null)
        {
            byte[] tempData;

            if (pixelFormat != PixelFormat.ARGB)
            {
                int w = mipMaps[0].width;
                int h = mipMaps[0].height;
                tempData = convertRawToARGB(mipMaps[0].data, ref w, ref h, pixelFormat);
                // Sub-4x4 block-compressed mips decompress to a full 4x4 block; crop back to the real dimensions
                if (w != mipMaps[0].origWidth || h != mipMaps[0].origHeight)
                    tempData = cropARGB(tempData, w, mipMaps[0].origWidth, mipMaps[0].origHeight);
            }
            else
            {
                tempData = mipMaps[0].data;
            }

            int width = mipMaps[0].origWidth;
            int height = mipMaps[0].origHeight;

            if (options != null && !options.IsDefault)
            {
                correctMipsWithOptions(tempData, width, height, dstFormat, dxt1HasAlpha, dxt1Threshold, options);
                return;
            }

            if (mipMaps.Count > 1)
                mipMaps.RemoveRange(1, mipMaps.Count - 1);

            if (dstFormat != pixelFormat || (dstFormat == PixelFormat.DXT1 && !dxt1HasAlpha))
            {
                // tempData is already ARGB by this point, so a target of ARGB needs no conversion at
                // all. Handing that no-op to the native converter fails - and fails opaquely, with an
                // ArgumentException out of Marshal ("Value does not fall within the expected range")
                // rather than anything naming the formats - which makes correctMips(ARGB) look broken
                // when it is the one target that needs no work. The downscale loop below already has
                // this case right; only the top mip was missing it.
                byte[] top = dstFormat == PixelFormat.ARGB
                    ? tempData
                    : convertToFormat(PixelFormat.ARGB, tempData, width, height, dstFormat, dxt1HasAlpha, dxt1Threshold);
                mipMaps.RemoveAt(0);
                mipMaps.Add(new MipMap(top, width, height, dstFormat));
                pixelFormat = dstFormat;
            }

            int prevW, prevH;
            int origW = width;
            int origH = height;
            for (; ; )
            {
                prevW = width;
                prevH = height;
                origW >>= 1;
                origH >>= 1;
                if (origW == 0 && origH == 0)
                    break;
                if (origW == 0)
                    origW = 1;
                if (origH == 0)
                    origH = 1;
                width = origW;
                height = origH;

                tempData = downscaleARGB(tempData, prevW, prevH);
                if (pixelFormat != PixelFormat.ARGB)
                {
                    byte[] converted = convertToFormat(PixelFormat.ARGB, tempData, origW, origH, pixelFormat, dxt1HasAlpha, dxt1Threshold);
                    mipMaps.Add(new MipMap(converted, origW, origH, pixelFormat));
                }
                else
                {
                    mipMaps.Add(new MipMap(tempData, origW, origH, pixelFormat));
                }
            }
        }

        /// <summary>
        /// The <see cref="correctMips"/> path for non-default <see cref="MipGenerationOptions"/>: builds the whole chain as ARGB
        /// first, then converts each level. The top mip is only rewritten when it has to change format, so a top mip that is already
        /// in the target format is never recompressed - not even a DXT1 one without alpha, which the default path re-encodes.
        /// </summary>
        private void correctMipsWithOptions(byte[] topArgb, int width, int height, PixelFormat dstFormat, bool dxt1HasAlpha, byte dxt1Threshold, MipGenerationOptions options)
        {
            MipMap originalTop = mipMaps[0];
            bool convertTop = dstFormat != pixelFormat;

            List<byte[]> levels = MipGenerator.BuildChain(topArgb, width, height, options);

            mipMaps.Clear();
            if (convertTop)
            {
                byte[] top = dstFormat == PixelFormat.ARGB
                    ? levels[0]
                    : convertToFormat(PixelFormat.ARGB, levels[0], width, height, dstFormat, dxt1HasAlpha, dxt1Threshold);
                mipMaps.Add(new MipMap(top, width, height, dstFormat));
                pixelFormat = dstFormat;
            }
            else
            {
                mipMaps.Add(originalTop);
            }

            int levelWidth = width;
            int levelHeight = height;
            for (int i = 1; i < levels.Count; i++)
            {
                levelWidth = Math.Max(levelWidth >> 1, 1);
                levelHeight = Math.Max(levelHeight >> 1, 1);
                byte[] data = pixelFormat == PixelFormat.ARGB
                    ? levels[i]
                    : convertToFormat(PixelFormat.ARGB, levels[i], levelWidth, levelHeight, pixelFormat, dxt1HasAlpha, dxt1Threshold);
                mipMaps.Add(new MipMap(data, levelWidth, levelHeight, pixelFormat));
            }
        }

        /// <summary>
        /// Replaces every level of this image with <paramref name="mips"/>, already encoded in <paramref name="format"/>.
        /// </summary>
        internal void SetMips(List<MipMap> mips, PixelFormat format)
        {
            mipMaps = mips;
            pixelFormat = format;
        }

        /// <summary>
        /// True when this image has a complete mip chain: more than one level, each half the size of the one above
        /// (never below 1), ending at 1x1. This is the shape <see cref="correctMips"/> produces, and the shape
        /// <see cref="Unreal.Classes.Texture2D.Replace"/> accepts when asked to use a supplied chain. The pixel format of
        /// the levels does not matter.
        /// </summary>
        public bool HasCompleteMipChain()
        {
            // Note this doesn't count if you're using TextureGroup_UI
            // Engine only takes top mip if you're using this texture group.
            if (mipMaps == null || mipMaps.Count < 2)
            {
                return false;
            }

            int width = mipMaps[0].origWidth;
            int height = mipMaps[0].origHeight;
            for (int i = 0; i < mipMaps.Count; i++)
            {
                if (mipMaps[i].origWidth != width || mipMaps[i].origHeight != height)
                {
                    return false;
                }

                if (width == 1 && height == 1)
                {
                    return i == mipMaps.Count - 1;
                }

                width = Math.Max(width >> 1, 1);
                height = Math.Max(height >> 1, 1);
            }

            // Ran out of levels before reaching 1x1
            return false;
        }

        /// <summary>
        /// Converts every mip of this image to <paramref name="dstFormat"/> without regenerating any of them: each level keeps
        /// its own pixels, so a chain that was authored level by level (rather than derived from the top mip) survives.
        /// Levels already in the target format are left untouched.
        /// </summary>
        /// <param name="dstFormat">The format to convert every level to.</param>
        /// <param name="dxt1HasAlpha">Whether a DXT1 target should keep 1-bit alpha.</param>
        /// <param name="dxt1Threshold">Alpha cut-off for DXT1 1-bit alpha.</param>
        public void ConvertMipsTo(PixelFormat dstFormat, bool dxt1HasAlpha = false, byte dxt1Threshold = 128)
        {
            if (pixelFormat == dstFormat)
            {
                return;
            }

            var converted = new List<MipMap>(mipMaps.Count);
            foreach (MipMap mip in mipMaps)
            {
                byte[] argb;
                if (pixelFormat == PixelFormat.ARGB)
                {
                    argb = mip.data;
                }
                else
                {
                    int w = mip.width;
                    int h = mip.height;
                    argb = convertRawToARGB(mip.data, ref w, ref h, pixelFormat);
                    // Sub-4x4 block-compressed mips decompress to a full 4x4 block; crop back to the real dimensions
                    if (w != mip.origWidth || h != mip.origHeight)
                    {
                        argb = cropARGB(argb, w, mip.origWidth, mip.origHeight);
                    }
                }

                byte[] data = dstFormat == PixelFormat.ARGB
                    ? argb
                    : convertToFormat(PixelFormat.ARGB, argb, mip.origWidth, mip.origHeight, dstFormat, dxt1HasAlpha, dxt1Threshold);
                converted.Add(new MipMap(data, mip.origWidth, mip.origHeight, dstFormat));
            }

            mipMaps.Clear();
            mipMaps.AddRange(converted);
            pixelFormat = dstFormat;
        }

        public static PixelFormat getPixelFormatType(string format)
        {
            switch (format)
            {
                case "PF_BC1":
                case "PF_DXT1":
                    return PixelFormat.DXT1;
                case "PF_DXT3":
                    return PixelFormat.DXT3;
                case "PF_DXT5":
                case "PF_BC3":
                    return PixelFormat.DXT5;
                case "PF_NormalMap_HQ":
                    return PixelFormat.ATI2;
                case "PF_BC5":
                    return PixelFormat.BC5;
                case "PF_V8U8":
                    return PixelFormat.V8U8;
                case "PF_A8R8G8B8":
                    return PixelFormat.ARGB;
                case "PF_R8G8B8":
                    return PixelFormat.RGB;
                case "PF_G8":
                    return PixelFormat.G8;
                case "PF_BC7":
                    return PixelFormat.BC7;
                default:
                    throw new Exception("invalid texture format");
            }
        }

        /// <summary>
        /// Gets the enum value for the given PixelFormat enum, used in properties of a texture
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string getEngineFormatType(PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.DXT1:
                    return "PF_DXT1";
                case PixelFormat.DXT3:
                    return "PF_DXT3";
                case PixelFormat.DXT5:
                    return "PF_DXT5";
                case PixelFormat.ATI2:
                    return "PF_NormalMap_HQ";
                case PixelFormat.V8U8:
                    return "PF_V8U8";
                case PixelFormat.ARGB:
                    return "PF_A8R8G8B8";
                case PixelFormat.RGB:
                    return "PF_R8G8B8";
                case PixelFormat.G8:
                    return "PF_G8";
                case PixelFormat.BC7:
                    return "PF_BC7";
                case PixelFormat.BC5:
                    return "PF_BC5";
                default:
                    throw new Exception("invalid texture format");
            }
        }

        /// <summary>
        /// Loads an image from disk and converts it internally to the specified pixel format
        /// </summary>
        /// <param name="filename">Full file path on disk to the image</param>
        /// <param name="targetFormat">The destination image pixel format</param>
        /// <returns>Image with the specified pixel format</returns>
        public static Image LoadFromFile(string filename, PixelFormat targetFormat)
        {
            var mips = new List<MipMap>();

            byte[] pixelData = TexConverter.LoadTexture(filename, out uint width, out uint height, ref targetFormat);
            mips.Add(new MipMap(pixelData, (int)width, (int)height, targetFormat));
            //TexConverter.SaveTexture(pixelData, width, height, PixelFormat.ARGB, @"C:\users\mgamerz\desktop.id.png");
            return new Image(mips, targetFormat);
        }

        /// <summary>
        /// Loads an image from an array and converts it internally to the specified pixel format
        /// </summary>
        /// <param name="buffer">Full data of a file to load</param>
        /// <param name="imageType">1 = DDS, 2 = PNG, 3 = TGA</param>
        /// <param name="targetFormat">The destination image pixel format</param>
        /// <returns>Image with the specified pixel format</returns>
        public static Image LoadFromFileMemory(byte[] buffer, int imageType, PixelFormat targetFormat)
        {
            var mips = new List<MipMap>();
            byte[] pixelData = TexConverter.LoadTextureFromMemory(buffer, imageType, out uint width, out uint height, ref targetFormat);
            mips.Add(new MipMap(pixelData, (int)width, (int)height, targetFormat));
            return new Image(mips, targetFormat);
        }

        /// <summary>
        /// Checks if the top mip has any pixels that don't have an alpha of 0 or 1. Only works on ARGB images.
        /// </summary>
        /// <returns>True if any pixel is not 0 or 1 alpha and is in ARGB format, false otherwise</returns>
        internal bool HasFullAlpha()
        {
            if (!mipMaps.Any())
                return false;

            // We can really only check ARGB...
            if (pixelFormat == PixelFormat.ARGB)
            {
                var mip = mipMaps[0];
                for (int i = 3; i < mip.data.Length; i += 4) // B G R A
                {
                    if (mip.data[i] != 0 && mip.data[i] != 255) return true; // Not 0 or 1
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if any pixel of the top mip has an alpha below 255. Works on ARGB, DXT1, DXT3 and DXT5 images.
        /// </summary>
        internal bool HasTransparency()
        {
            if (!mipMaps.Any())
                return false;

            var mip = mipMaps[0];
            byte[] argb = mip.data;
            if (pixelFormat is PixelFormat.DXT1 or PixelFormat.DXT3 or PixelFormat.DXT5)
            {
                int w = mip.width, h = mip.height;
                argb = convertRawToARGB(mip.data, ref w, ref h, pixelFormat);
            }
            else if (pixelFormat != PixelFormat.ARGB)
            {
                return false;
            }

            for (int i = 3; i < argb.Length; i += 4)
            {
                if (argb[i] != 255) return true;
            }

            return false;
        }
    }
}