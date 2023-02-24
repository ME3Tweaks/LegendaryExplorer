using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioPawn : ObjectBinary
    {
        public UMultiMap<NameReference, UIndex> AnimationMap;//? Speculative name  //TODO: Make this a UMap
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref AnimationMap, SCExt.Serialize, SCExt.Serialize);
        }

        public static BioPawn Create()
        {
            return new()
            {
                AnimationMap = new()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(AnimationMap.Select((kvp, i) => (kvp.Key, $"{nameof(AnimationMap)}[{i}]")));

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ForEachUIndexValueInMultiMap(action, AnimationMap, nameof(AnimationMap));
        }
    }
}
