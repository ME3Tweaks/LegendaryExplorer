using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioInert : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, Guid> NameGuidMap;//? Speculative name
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref NameGuidMap, SCExt.Serialize, SCExt.Serialize);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            return new List<(UIndex, string)>(); // None
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(NameGuidMap.Select((kvp, i) => (kvp.Key, $"Name[{i}]")));

            return names;
        }
    }
}