using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Gammtek.Extensions
{
    public static unsafe class UnsafeExtensions
    {
        public static Span<byte> AsBytes<T>(this ref T val) where T : unmanaged
        {
            return MemoryMarshal.AsBytes(new Span<T>(ref val));
        }

        public static Span<TTo> AsSpanOf<TFrom, TTo>(this ref TFrom val) where TFrom : unmanaged where TTo : unmanaged
        {
            int spanLength = sizeof(TFrom) / sizeof(TTo);
            return MemoryMarshal.CreateSpan(ref Unsafe.As<TFrom, TTo>(ref val), spanLength);
        }
    }
}
