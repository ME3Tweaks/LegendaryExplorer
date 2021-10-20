using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class GuidCache : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, Guid> PackageGuidMap;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref PackageGuidMap, SCExt.Serialize, SCExt.Serialize);
        }

        public static GuidCache Create()
        {
            return new()
            {
                PackageGuidMap = new OrderedMultiValueDictionary<NameReference, Guid>()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            return new List<(NameReference, string)>(PackageGuidMap.Select((kvp, i) => (kvp.Key, $"{nameof(PackageGuidMap)}[{i}]")));
        }
    }
}
