using System;
using System.Buffers.Binary;
using System.IO.Hashing;

namespace LegendaryExplorerCore.Helpers
{
    /// <summary>
    /// Helper class for generating CRCs in the format expected by MEM
    /// </summary>
    public static class TextureCRC
    {
        public static uint Compute(ReadOnlySpan<byte> data)
        {
            return ~BinaryPrimitives.ReadUInt32LittleEndian(Crc32.Hash(data));
        }

        public static uint Compute(byte[] data, int offset, int count) => Compute(data.AsSpan(offset, count));

        public static uint Compute(byte[] data) => Compute(data.AsSpan());

    }
}
