using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class ShadowMap1D : ObjectBinary
    {
        public int[] Samples;
        public Guid LightGuid;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game == MEGame.ME3)
            {
                sc.SerializeConstInt(4);
            }
            sc.Serialize(ref Samples, SCExt.Serialize);
            sc.Serialize(ref LightGuid);
        }
    }
}
