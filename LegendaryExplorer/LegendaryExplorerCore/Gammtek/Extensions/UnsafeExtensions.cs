using System;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Gammtek.Extensions
{
    public static unsafe class UnsafeExtensions
    {
        public static Span<byte> AsBytes<T>(this ref T val) where T : unmanaged
        {
            return MemoryMarshal.AsBytes(new Span<T>(ref val));
        }
    }
}
