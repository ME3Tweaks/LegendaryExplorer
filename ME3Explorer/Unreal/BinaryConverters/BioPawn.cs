using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class BioPawn : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, UIndex> AnimationMap;//? Speculative name
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref AnimationMap, SCExt.Serialize, SCExt.Serialize);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            return AnimationMap.Select((kvp, i) => (kvp.Value, $"AnimationMap[{i}]")).ToList();
        }
    }
}
