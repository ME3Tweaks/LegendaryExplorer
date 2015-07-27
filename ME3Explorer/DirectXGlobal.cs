using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer
{
    static class DirectXGlobal
    {
        public static Texture Tex_Default;
        public static Texture Tex_Select;
        public static bool DrawWireFrame = false;
        public struct Camera
        {
            public Vector3 pos;
            public Vector3 dir;
        }
        public static Camera Cam;        
    }
}
