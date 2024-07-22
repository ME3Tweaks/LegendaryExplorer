using System.Collections.Generic;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class RB_BodySetup : ObjectBinary
    {
        public List<KCachedConvexData> PreCachedPhysData;
        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref PreCachedPhysData, sc.Serialize);
        }

        public static RB_BodySetup Create()
        {
            return new()
            {
                PreCachedPhysData = []
            };
        }
    }
}
