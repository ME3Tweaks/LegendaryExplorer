using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioDynamicAnimSet : ObjectBinary
    {
        public UMultiMap<NameReference, int> SequenceNamesToUnkMap;
        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref SequenceNamesToUnkMap, sc.Serialize, sc.Serialize);
        }

        public static BioDynamicAnimSet Create()
        {
            return new()
            {
                SequenceNamesToUnkMap = []
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
