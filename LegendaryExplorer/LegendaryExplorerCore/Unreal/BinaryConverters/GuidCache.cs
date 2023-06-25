using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class GuidCache : ObjectBinary
    {
        public UMultiMap<NameReference, Guid> PackageGuidMap; //TODO: Make this a UMap

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref PackageGuidMap, SCExt.Serialize, SCExt.Serialize);
        }

        public static GuidCache Create()
        {
            return new()
            {
                PackageGuidMap = new()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            return new List<(NameReference, string)>(PackageGuidMap.Select((kvp, i) => (kvp.Key, $"{nameof(PackageGuidMap)}[{i}]")));
        }
    }
}
