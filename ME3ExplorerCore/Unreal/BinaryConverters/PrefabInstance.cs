using System.Collections.Generic;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class PrefabInstance : ObjectBinary
    {
        public OrderedMultiValueDictionary<UIndex, UIndex> ArchetypeToInstanceMap;
        public OrderedMultiValueDictionary<UIndex, int> ObjectMap;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref ArchetypeToInstanceMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref ObjectMap, SCExt.Serialize, SCExt.Serialize);
        }
        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();

            foreach ((UIndex archetype, UIndex instance) in ArchetypeToInstanceMap)
            {
                uIndexes.Add((archetype, "ArchetypeToInstanceMap.Archetype"));
                uIndexes.Add((instance, "ArchetypeToInstanceMap.Instance"));
            }

            uIndexes.AddRange(ObjectMap.Select((kvp, i) => (kvp.Key, $"ObjectMap[{i}]")));

            return uIndexes;
        }
    }
}
