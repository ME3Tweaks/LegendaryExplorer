using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.TLK
{
    /// <summary>
    /// Stripped-down version of <see cref="System.Collections.BitArray"/> used by the TLK classes for huffman decompression.
    /// </summary>
    public sealed class TLKBitArray
    {
        /// <summary>
        /// Length in bytes
        /// </summary>
        public readonly int Length;
        private readonly int[] intArray;

        private TLKBitArray(int bytesLength)
        {
            Length = bytesLength * 8;
            int arrLen = Math.DivRem(bytesLength, sizeof(int), out int remainder);
            if (remainder > 0)
            {
                ++arrLen;
            }
            intArray = new int[arrLen];
        }

        /// <summary>
        /// Constructs a <see cref="TLKBitArray"/> from <paramref name="bytesLength"/> bytes from <paramref name="stream"/>
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read from</param>
        /// <param name="bytesLength">The number of bytes to read</param>
        public TLKBitArray(Stream stream, int bytesLength) : this(bytesLength)
        {
            //reinterpret the intArray as a byte array, then slice it to the bytesLength (since it might not be a multiple of four)
            stream.Read(MemoryMarshal.AsBytes(intArray.AsSpan())[..bytesLength]);
        }

        /// <summary>
        /// Constructs a <see cref="TLKBitArray"/> from a byte array
        /// </summary>
        /// <param name="bytes"></param>
        public TLKBitArray(byte[] bytes) : this(bytes.Length)
        {
            bytes.AsSpan().CopyTo(MemoryMarshal.AsBytes(intArray.AsSpan()));
        }
        
        /// <summary>
        /// Gets the bit at index <paramref name="i"/>
        /// </summary>
        /// <param name="i">Index into the bit array</param>
        /// <returns>True if the bit is 1, false if the bit is 0</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int i)=> (intArray[i >> 5] & (1 << i)) != 0;
    }
}
