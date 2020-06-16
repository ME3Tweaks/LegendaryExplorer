using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class GuidCache : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, Guid> PackageGuidMap;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref PackageGuidMap, SCExt.Serialize, SCExt.Serialize);
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            return new List<(NameReference, string)>(PackageGuidMap.Select((kvp, i) => (kvp.Key, $"{nameof(PackageGuidMap)}[{i}]")));
        }
    }
}
