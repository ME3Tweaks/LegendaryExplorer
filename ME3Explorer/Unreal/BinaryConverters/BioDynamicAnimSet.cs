using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class BioDynamicAnimSet : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, int> SequenceNamesToUnkMap;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SequenceNamesToUnkMap, SCExt.Serialize, SCExt.Serialize);
        }
    }
}
