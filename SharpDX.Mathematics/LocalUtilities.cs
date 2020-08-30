using System;
using System.Collections.Generic;
using System.Text;

namespace SharpDX.Mathematics
{
    public static class LocalUtilities
    {
        /// <summary>
        /// Return the sizeof a struct from a CLR. Equivalent to sizeof operator but works on generics too.
        /// </summary>
        /// <typeparam name="T">A struct to evaluate.</typeparam>
        /// <returns>Size of this struct.</returns>
        public static int SizeOf<T>() where T : struct
        {
            return Interop.SizeOf<T>();
        }

        /// <summary>
        /// Return the sizeof an array of struct. Equivalent to sizeof operator but works on generics too.
        /// </summary>
        /// <typeparam name="T">A struct.</typeparam>
        /// <param name="array">The array of struct to evaluate.</param>
        /// <returns>Size in bytes of this array of struct.</returns>
        public static int SizeOf<T>(T[] array) where T : struct
        {
            return array == null ? 0 : array.Length * Interop.SizeOf<T>();
        }
    }
}
