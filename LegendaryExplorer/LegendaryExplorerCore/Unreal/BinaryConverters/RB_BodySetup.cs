using System.Collections.Generic;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class RB_BodySetup : ObjectBinary
    {
        public List<KCachedConvexData> PreCachedPhysData;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref PreCachedPhysData, SCExt.Serialize);
        }

        public static RB_BodySetup Create()
        {
            return new()
            {
                PreCachedPhysData = new List<KCachedConvexData>()
            };
        }
    }
}
