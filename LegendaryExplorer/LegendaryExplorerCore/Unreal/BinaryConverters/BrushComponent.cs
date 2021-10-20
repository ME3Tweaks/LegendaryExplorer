using System;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BrushComponent : ObjectBinary
    {
        public KCachedConvexData CachedPhysBrushData;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref CachedPhysBrushData);
        }

        public static BrushComponent Create()
        {
            return new()
            {
                CachedPhysBrushData = new KCachedConvexData
                {
                    CachedConvexElements = Array.Empty<KCachedConvexDataElement>()
                }
            };
        }
    }
}
