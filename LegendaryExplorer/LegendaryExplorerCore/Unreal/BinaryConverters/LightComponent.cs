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

        //FConvexVolume::IntersectBox: the box is outside if it's entirely in front of any plane
        public bool IntersectsBox(BoxSphereBounds bounds)
        {
            foreach (Plane plane in Planes ?? [])
            {
                Vector3 n = plane.Normal;
                float distance = Vector3.Dot(n, bounds.Origin) - plane.D;
                float pushOut = MathF.Abs(n.X) * bounds.BoxExtent.X + MathF.Abs(n.Y) * bounds.BoxExtent.Y + MathF.Abs(n.Z) * bounds.BoxExtent.Z;
                if (distance > pushOut)
                {
                    return false;
                }
            }
            return true;
        }
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