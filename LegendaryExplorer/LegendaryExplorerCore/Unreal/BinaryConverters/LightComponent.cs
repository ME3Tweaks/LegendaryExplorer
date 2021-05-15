using LegendaryExplorerCore.SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class LightComponent : ObjectBinary
    {
        public ConvexVolume[] InclusionConvexVolumes;
        public ConvexVolume[] ExclusionConvexVolumes;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref InclusionConvexVolumes, SCExt.Serialize);
            sc.Serialize(ref ExclusionConvexVolumes, SCExt.Serialize);
        }
    }

    public class ConvexVolume
    {
        public Plane[] Planes;
        public Plane[] PermutedPlanes;
    }

    public static partial class SCExt
    {
        public static void Serialize(SerializingContainer2 sc, ref ConvexVolume vol)
        {
            if (sc.IsLoading)
            {
                vol = new ConvexVolume();
            }
            sc.Serialize(ref vol.Planes, Serialize);
            sc.Serialize(ref vol.PermutedPlanes, Serialize);
        }
    }
}