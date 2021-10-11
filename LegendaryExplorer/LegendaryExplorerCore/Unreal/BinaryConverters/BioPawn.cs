using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioPawn : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, UIndex> AnimationMap;//? Speculative name
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref AnimationMap, SCExt.Serialize, SCExt.Serialize);
        }

        public static BioPawn Create()
        {
            return new()
            {
                AnimationMap = new OrderedMultiValueDictionary<NameReference, UIndex>()
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            return AnimationMap.Select((kvp, i) => (kvp.Value, $"AnimationMap[{i}]")).ToList();
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(AnimationMap.Select((kvp, i) => (kvp.Key, $"AnimationMap[{i}]")));

            return names;
        }
    }
}
