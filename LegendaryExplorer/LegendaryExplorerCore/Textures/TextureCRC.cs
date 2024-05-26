using System;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LegendaryExplorerCore.Textures
{
    /// <summary>
    /// Helper class for generating CRCs in the tpf format
    /// </summary>
    public static class TextureCRC
    {
        public static uint Compute(ReadOnlySpan<byte> data)
        {
            //the tpf crc format is the IEEE 802.3 format, inverted 
            if (Sse41.IsSupported && Pclmulqdq.IsSupported)
            {
                return ~Append(0, data);
            }
            return ~BinaryPrimitives.ReadUInt32LittleEndian(Crc32.Hash(data));
        }

        public static uint Compute(byte[] data, int offset, int count) => Compute(data.AsSpan(offset, count));

        public static uint Compute(byte[] data) => Compute(data.AsSpan());

        //These constants are generated from the IEEE 802.3 polynomial
        const long K1 = 0x154442bd4;
        const long K2 = 0x1c6e41596;
        const long K3 = 0x1751997d0;
        const long K4 = 0x0ccaa009e;
        const long K5 = 0x163cd6124;
        const long P_X = 0x1DB710641;
        const long U_PRIME = 0x1F7011641;

        //adapted from https://github.com/srijs/rust-crc32fast/blob/master/src/specialized/pclmulqdq.rs
        private static unsafe uint Append(uint crc, ReadOnlySpan<byte> input)
        {
            if (input.Length < 64)
            {
                return crc32_halfbyte(crc, input);
            }

            fixed (byte* inputPtr = input)
            {
                byte* endPtr = inputPtr + input.Length;
                byte* ptr = inputPtr;
                var x3 = Get(ref ptr);
                var x2 = Get(ref ptr);
                var x1 = Get(ref ptr);
                var x0 = Get(ref ptr);

                x3 = Sse2.Xor(x3, Sse2.ConvertScalarToVector128Int32((int)~crc).AsByte());

                var k1k2 = Vector128.Create(K1, K2).AsByte(); //order of args reversed as compared to _mm_set_epi64x
                while (endPtr - ptr >= 64)
                {
                    x3 = Reduce128(x3, Get(ref ptr), k1k2);
                    x2 = Reduce128(x2, Get(ref ptr), k1k2);
                    x1 = Reduce128(x1, Get(ref ptr), k1k2);
                    x0 = Reduce128(x0, Get(ref ptr), k1k2);
                }

                var k3k4 = Vector128.Create(K3, K4).AsByte();
                var x = Reduce128(x3, x2, k3k4);
                x = Reduce128(x, x1, k3k4);
                x = Reduce128(x, x0, k3k4);

                while (endPtr - ptr >= 16)
                {
                    x = Reduce128(x, Get(ref ptr), k3k4);
                }

                x = Sse2.Xor(
                    Pclmulqdq.CarrylessMultiply(x.AsUInt64(), k3k4.AsUInt64(), 0x10).AsByte(),
                    Sse2.ShiftRightLogical128BitLane(x, 8)
                    );

                var xor32 = Vector128.Create(~0, 0, 0, 0).AsByte();
                x = Sse2.Xor(
                    Pclmulqdq.CarrylessMultiply(
                        Sse2.And(x, xor32).AsInt64(),
                        Vector128.Create(K5, 0),
                        0x00).AsByte(),
                    Sse2.ShiftRightLogical128BitLane(x, 4)
                    );

                var pu = Vector128.Create(P_X, U_PRIME);

                var t1 = Pclmulqdq.CarrylessMultiply(
                    Sse2.And(x, xor32).AsInt64(),
                    pu,
                    0x10);

                var t2 = Pclmulqdq.CarrylessMultiply(
                    Sse2.And(t1.AsByte(), xor32).AsInt64(),
                    pu,
                    0x00);

                crc = ~Sse41.Extract(Sse2.Xor(x, t2.AsByte()).AsUInt32(), 1);

                if (endPtr - ptr > 0)
                {
                    crc = crc32_halfbyte(crc, new ReadOnlySpan<byte>(ptr, (int)(endPtr - ptr)));
                }
            }

            return crc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Reduce128(Vector128<byte> a, Vector128<byte> b, Vector128<byte> keys)
        {
            var t1 = Pclmulqdq.CarrylessMultiply(a.AsUInt64(), keys.AsUInt64(), 0x00);
            var t2 = Pclmulqdq.CarrylessMultiply(a.AsUInt64(), keys.AsUInt64(), 0x11);
            return Sse2.Xor(Sse2.Xor(b, t1.AsByte()), t2.AsByte());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe Vector128<byte> Get(ref byte* ptr)
        {
            Vector128<byte> vec = Sse2.LoadVector128(ptr);
            ptr += 16;
            return vec;
        }

        private static readonly uint[] HalfByteTable = {
            0x00000000,0x1DB71064,0x3B6E20C8,0x26D930AC,0x76DC4190,0x6B6B51F4,0x4DB26158,0x5005713C,
            0xEDB88320,0xF00F9344,0xD6D6A3E8,0xCB61B38C,0x9B64C2B0,0x86D3D2D4,0xA00AE278,0xBDBDF21C
        };
        //adapted from https://github.com/stbrumme/crc32/blob/master/Crc32.cpp
        private static uint crc32_halfbyte(uint crc, ReadOnlySpan<byte> data)
        {
            crc = ~crc;
            for (int i = 0; i < data.Length; i++)
            {
                crc = HalfByteTable[(crc ^ data[i]) & 0x0F] ^ (crc >> 4);
                crc = HalfByteTable[(crc ^ (data[i] >> 4)) & 0x0F] ^ (crc >> 4);
            }
            return ~crc;
        }
    }
}