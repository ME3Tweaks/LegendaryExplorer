using System.Collections.Generic;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class RB_BodySetup : ObjectBinary
    {
        public List<KCachedConvexData> PreCachedPhysData;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref PreCachedPhysData, SCExt.Serialize);
        }
    }
}
