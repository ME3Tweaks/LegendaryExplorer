using System;
using System.IO;

namespace LegendaryExplorerCore.Textures
{
    /// <summary>
    /// Converts Xbox 360 tiled DXT mip data to linear, little-endian DXT blocks.
    /// </summary>
    public static class Xbox360Tiling
    {
        /// <remarks>
        /// Uses the Xenos 2D tiled addressing scheme. See also UEViewer's
        /// Unreal/UnrealMaterial/UnTexture.cpp, GetXbox360TiledOffset and DecodeXBox360.
        /// Each input must contain one independently stored mip, including its tile padding
        /// </remarks>
        public static byte[] Untile(ReadOnlySpan<byte> tiled, int width, int height, PixelFormat format)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            int logBytesPerBlock = format switch
            {
                PixelFormat.DXT1 => 3,
                PixelFormat.DXT3 or PixelFormat.DXT5 => 4,
                _ => throw new NotSupportedException($"Xbox 360 untiling is not supported for pixel format {format}")
            };
            int bytesPerBlock = 1 << logBytesPerBlock;
            int blockWidth = checked((width + 3) / 4);
            int blockHeight = checked((height + 3) / 4);
            int pitch = checked((blockWidth + 31) & ~31);
            var linear = new byte[checked(blockWidth * blockHeight * bytesPerBlock)];

            for (int y = 0; y < blockHeight; y++)
            {
                for (int x = 0; x < blockWidth; x++)
                {
                    int sourceOffset = GetTiledByteOffset(x, y, pitch, logBytesPerBlock);
                    if (sourceOffset < 0 || sourceOffset > tiled.Length - bytesPerBlock)
                    {
                        throw new InvalidDataException($"Xbox 360 {width}x{height} mip does not contain the required tiled data. Packed mip tails are not supported.");
                    }
                    int destinationOffset = (y * blockWidth + x) * bytesPerBlock;
                    // Xenos stores DXT blocks with bytes swapped within each 16-bit word.
                    for (int b = 0; b < bytesPerBlock; b++)
                    {
                        linear[destinationOffset + b] = tiled[sourceOffset + (b ^ 1)];
                    }
                }
            }
            return linear;
        }

        private static int GetTiledByteOffset(int x, int y, int pitch, int logBytesPerBlock)
        {
            int tile = ((y / 32) * (pitch / 32) + x / 32) << (logBytesPerBlock + 7);
            int within = ((x % 8) + ((y & 14) * 4)) << logBytesPerBlock;
            int mixed = tile + ((within & ~15) * 2) + (within & 15) + ((y & 1) * 16);
            return ((mixed & ~511) * 8) + ((y & 16) * 128) + ((mixed & 448) * 4)
                   + (((((y & 8) / 4) + (x / 8)) & 3) * 64) + (mixed & 63);
        }
    }
}
