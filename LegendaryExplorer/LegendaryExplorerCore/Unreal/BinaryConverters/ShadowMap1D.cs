using System;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class ShadowMap1D : ObjectBinary
    {
        public int[] Samples;
        public Guid LightGuid;

        protected override void Serialize(SerializingContainer sc)
        {
            if (sc.Game.IsGame3())
            {
                sc.SerializeConstInt(4);
            }
            sc.Serialize(ref Samples, sc.Serialize);
            sc.Serialize(ref LightGuid);
        }

        public static ShadowMap1D Create()
        {
            return new()
            {
                Samples = []
            };
        }
    }
}
