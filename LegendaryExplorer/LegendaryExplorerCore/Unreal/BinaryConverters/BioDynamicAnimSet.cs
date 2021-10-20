using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioDynamicAnimSet : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, int> SequenceNamesToUnkMap;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SequenceNamesToUnkMap, SCExt.Serialize, SCExt.Serialize);
        }

        public static BioDynamicAnimSet Create()
        {
            return new()
            {
                SequenceNamesToUnkMap = new OrderedMultiValueDictionary<NameReference, int>()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(SequenceNamesToUnkMap.Select((kvp, i) => (kvp.Key, $"SequenceNamesToUnkMap[{i}]")));

            return names;
        }
    }
}
