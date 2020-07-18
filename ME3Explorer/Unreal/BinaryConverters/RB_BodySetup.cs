using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.Unreal.BinaryConverters
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
