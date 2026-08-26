using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class PrefabInstance : ObjectBinary
    {
        [UIndexRef("Object", UIndexRefFlags.Key)]
        [UIndexRef("Object", UIndexRefFlags.Value)]
        public UMultiMap<UIndex, UIndex> ArchetypeToInstanceMap; //TODO: Make this a UMap
        [UIndexRef("Object", UIndexRefFlags.Key)]
        public UMultiMap<UIndex, int> ObjectMap; //TODO: Make this a UMap

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref ArchetypeToInstanceMap, sc.Serialize, sc.Serialize);
            sc.Serialize(ref ObjectMap, sc.Serialize, sc.Serialize);
        }

        public static PrefabInstance Create()
        {
            return new()
            {
                ArchetypeToInstanceMap = [],
                ObjectMap = []
            };
        }
        
        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ForEachUIndexKeyInMultiMap(action, ArchetypeToInstanceMap, nameof(ArchetypeToInstanceMap));
            ForEachUIndexValueInMultiMap(action, ArchetypeToInstanceMap, nameof(ArchetypeToInstanceMap));
            ForEachUIndexKeyInMultiMap(action, ObjectMap, nameof(ObjectMap));
        }
    }
}
