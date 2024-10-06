using System.Numerics;
using System;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class LightComponent : ObjectBinary
    {
        public ConvexVolume[] InclusionConvexVolumes;
        public ConvexVolume[] ExclusionConvexVolumes;
        protected override void Serialize(SerializingContainer sc)
        {
            if (sc.Game is not MEGame.UDK)
            {
                sc.Serialize(ref InclusionConvexVolumes, sc.Serialize);
                sc.Serialize(ref ExclusionConvexVolumes, sc.Serialize);
            }
            else if (sc.IsLoading)
            {
                InclusionConvexVolumes = [];
                ExclusionConvexVolumes = [];
            }
        }

        public static LightComponent Create()
        {
            return new()
            {
                InclusionConvexVolumes = [],
                ExclusionConvexVolumes = []
            };
        }
    }

    public class ConvexVolume
    {
        public Plane[] Planes;
        public Plane[] PermutedPlanes;
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref ConvexVolume vol)
        {
            if (IsLoading)
            {
                vol = new ConvexVolume();
            }
            Serialize(ref vol.Planes, Serialize);
            Serialize(ref vol.PermutedPlanes, Serialize);
        }
    }
}