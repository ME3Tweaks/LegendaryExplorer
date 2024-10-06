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

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref PackageGuidMap, sc.Serialize, sc.Serialize);
        }

        public static GuidCache Create()
        {
            return new()
            {
                PackageGuidMap = []
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            return PackageGuidMap.Select((kvp, i) => (kvp.Key, $"{nameof(PackageGuidMap)}[{i}]")).ToList();
        }
    }
}
