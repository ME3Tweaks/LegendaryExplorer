using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ME3Explorer.Unreal;
namespace ME3Explorer
{
    public static class MPOpt
    {
        public static bool SKM_cullfaces = false;
        public static bool SKM_swaptangents = true;
        public static bool SKM_normalize = true;
        public static bool SKM_importbones = false;
        public static bool SKM_fixtexcoord = false;
        public static bool SKM_tnflipX = false;
        public static bool SKM_tnflipY = false;
        public static bool SKM_tnflipZ = true;
        public static bool SKM_tnflipW = false;
        public static bool SKM_biflipX = false;
        public static bool SKM_biflipY = false;
        public static bool SKM_biflipZ = true;
        public static bool SKM_biflipW = false;
        public static bool SKM_tnW100 = true;

        public static PCCObject pcc;
        public static int SelectedObject;
        public static int SelectedLOD;
    }
}
