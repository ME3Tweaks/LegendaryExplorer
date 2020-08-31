using System.Collections.Generic;
using ME3ExplorerCore.SharpDX;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.Helpers
{
    public static class Deconstructors
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }



        public static void Deconstruct(this NameReference nameRef, out string name, out int number)
        {
            name = nameRef.Name;
            number = nameRef.Number;
        }

        public static void Deconstruct(this Vector2 vector, out float x, out float y)
        {
            x = vector.X;
            y = vector.Y;
        }

        public static void Deconstruct(this Vector3 vector, out float x, out float y, out float z)
        {
            x = vector.X;
            y = vector.Y;
            z = vector.Z;
        }
    }
}
