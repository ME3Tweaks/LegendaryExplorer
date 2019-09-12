using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;

namespace ME3Explorer
{
    public static class Deconstructors
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }

        public static void Deconstruct(this Vector3 vec, out float x, out float y, out float z)
        {
            x = vec.X;
            y = vec.Y;
            z = vec.Z;
        }
    }
}
