using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class AnimSequence : ObjectBinary
    {
        public byte[] AnimationData; //todo: actually parse animation data

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game == MEGame.ME2)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                int offset = sc.FileOffset + 4;
                sc.Serialize(ref offset);
            }
            sc.Serialize(ref AnimationData, SCExt.Serialize);
        }
    }
}
