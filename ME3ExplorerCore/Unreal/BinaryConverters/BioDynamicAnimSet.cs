using System.Collections.Generic;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class BioDynamicAnimSet : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, int> SequenceNamesToUnkMap;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SequenceNamesToUnkMap, SCExt.Serialize, SCExt.Serialize);
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(SequenceNamesToUnkMap.Select((kvp, i) => (kvp.Key, $"SequenceNamesToUnkMap[{i}]")));

            return names;
        }
    }
}
