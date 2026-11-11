using System;

namespace LegendaryExplorerCore.Textures
{
    /// <summary>
    /// Converts Wii U (GX2 / AMD R7xx AddrLib) tiled texture data to linear data.
    /// </summary>
    /// <remarks>
    /// ME3 WiiU stores each mip as an independently tiled surface with a swizzle of 0.
    /// Mips are 2D_TILED_THIN1 when they fill at least one macro tile, otherwise 1D_TILED_THIN1.
    /// Only the thin tile modes used by the WiiU cooker are implemented.
    /// </remarks>
    public static class GX2Tiling
    {
        // Wii U GPU configuration
        private const int NumBanks = 4;
        private const int NumPipes = 2;
        private const int PipeInterleaveBytes = 256;
        private const int GroupBits = 8; // log2(PipeInterleaveBytes)
        private const int PipeBits = 1;
        private const int BankBits = 2;
        private const int MacroTileWidth = 8 * NumBanks; // in elements
        private const int MacroTileHeight = 8 * NumPipes;

        /// <summary>
        /// Untiles a single WiiU texture mip.
        /// </summary>
        /// <param name="tiled">Tiled (decompressed) mip data</param>
        /// <param name="width">Mip width in pixels</param>
        /// <param name="height">Mip height in pixels</param>
        /// <param name="format">Pixel format of the texture</param>
        /// <returns>Linear mip data, sized as <see cref="MipMap.getBufferSize"/> expects</returns>
        public static byte[] Untile(ReadOnlySpan<byte> tiled, int width, int height, PixelFormat format)
        {
            bool isBlockCompressed = format is PixelFormat.DXT1 or PixelFormat.DXT3 or PixelFormat.DXT5 or PixelFormat.ATI2 or PixelFormat.BC5 or PixelFormat.BC7;
            int bpp = format switch
            {
                PixelFormat.DXT1 => 64,
                PixelFormat.DXT3 or PixelFormat.DXT5 or PixelFormat.ATI2 or PixelFormat.BC5 or PixelFormat.BC7 => 128,
                PixelFormat.ARGB => 32,
                PixelFormat.V8U8 => 16,
                PixelFormat.G8 => 8,
                _ => throw new NotSupportedException($"GX2 untiling is not supported for pixel format {format}")
            };
            int bytesPerElement = bpp / 8;

            // Dimensions in elements (blocks for compressed formats)
            int elemWidth = isBlockCompressed ? Math.Max(1, (width + 3) / 4) : Math.Max(1, width);
            int elemHeight = isBlockCompressed ? Math.Max(1, (height + 3) / 4) : Math.Max(1, height);

            int macroPitchAlign = Math.Max(MacroTileWidth, MacroTileWidth * (PipeInterleaveBytes / bpp / 8));
            int macroPitch = Align(elemWidth, macroPitchAlign);
            int macroSize = macroPitch * Align(elemHeight, MacroTileHeight) * bytesPerElement;
            bool macroTiled = tiled.Length >= macroSize;
            int pitch = macroTiled ? macroPitch : Align(elemWidth, Math.Max(8, PipeInterleaveBytes / bpp));

            var linear = new byte[elemWidth * elemHeight * bytesPerElement];
            for (int y = 0; y < elemHeight; y++)
            {
                for (int x = 0; x < elemWidth; x++)
                {
                    int srcOffset = macroTiled
                        ? ComputeMacroTiledAddress(x, y, bpp, pitch)
                        : ComputeMicroTiledAddress(x, y, bpp, pitch);
                    if (srcOffset + bytesPerElement > tiled.Length)
                        continue;
                    tiled.Slice(srcOffset, bytesPerElement).CopyTo(linear.AsSpan((y * elemWidth + x) * bytesPerElement, bytesPerElement));
                }
            }
            return linear;
        }

        private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

        private static int PixelIndexWithinMicroTile(int x, int y, int bpp)
        {
            int b0, b1, b2, b3, b4, b5;
            switch (bpp)
            {
                case 8:
                    b0 = x & 1; b1 = (x >> 1) & 1; b2 = (x >> 2) & 1; b3 = (y >> 1) & 1; b4 = y & 1; b5 = (y >> 2) & 1;
                    break;
                case 16:
                    b0 = x & 1; b1 = (x >> 1) & 1; b2 = (x >> 2) & 1; b3 = y & 1; b4 = (y >> 1) & 1; b5 = (y >> 2) & 1;
                    break;
                case 64:
                    b0 = x & 1; b1 = y & 1; b2 = (x >> 1) & 1; b3 = (x >> 2) & 1; b4 = (y >> 1) & 1; b5 = (y >> 2) & 1;
                    break;
                case 128:
                    b0 = y & 1; b1 = x & 1; b2 = (x >> 1) & 1; b3 = (x >> 2) & 1; b4 = (y >> 1) & 1; b5 = (y >> 2) & 1;
                    break;
                default: // 32
                    b0 = x & 1; b1 = (x >> 1) & 1; b2 = y & 1; b3 = (x >> 2) & 1; b4 = (y >> 1) & 1; b5 = (y >> 2) & 1;
                    break;
            }
            return b0 | (b1 << 1) | (b2 << 2) | (b3 << 3) | (b4 << 4) | (b5 << 5);
        }

        // 1D_TILED_THIN1
        private static int ComputeMicroTiledAddress(int x, int y, int bpp, int pitch)
        {
            int microTileBytes = 64 * bpp / 8;
            int microTileOffset = microTileBytes * ((x >> 3) + (y >> 3) * (pitch >> 3));
            return microTileOffset + ((bpp * PixelIndexWithinMicroTile(x, y, bpp)) >> 3);
        }

        // 2D_TILED_THIN1, swizzle 0
        private static int ComputeMacroTiledAddress(int x, int y, int bpp, int pitch)
        {
            int elemOffset = (bpp * PixelIndexWithinMicroTile(x, y, bpp)) >> 3;

            int pipe = ((y >> 3) ^ (x >> 3)) & 1;
            int bank = (((y / (16 * NumPipes)) ^ (x >> 3)) & 1) | (2 * (((y / (8 * NumPipes)) ^ (x >> 4)) & 1));

            int macroTileBytes = MacroTileHeight * MacroTileWidth * bpp / 8;
            int macroTilesPerRow = pitch / MacroTileWidth;
            long macroTileOffset = (long)(x / MacroTileWidth + macroTilesPerRow * (y / MacroTileHeight)) * macroTileBytes;

            const int groupMask = (1 << GroupBits) - 1;
            long totalOffset = elemOffset + (macroTileOffset >> (BankBits + PipeBits));
            long offsetHigh = (totalOffset & ~groupMask) << (BankBits + PipeBits);
            long offsetLow = totalOffset & groupMask;
            return (int)((bank << (PipeBits + GroupBits)) | (pipe << GroupBits) | offsetLow | offsetHigh);
        }
    }
}
