using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3ExplorerCore.Unreal;
using SharpDX;

namespace ME3Explorer
{
    public static class Deconstructors
    {
        public static void Deconstruct(this Vector3 vec, out float x, out float y, out float z)
        {
            x = vec.X;
            y = vec.Y;
            z = vec.Z;
        }
    }
}
