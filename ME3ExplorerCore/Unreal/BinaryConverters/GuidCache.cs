using System;
using System.Collections.Generic;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
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
