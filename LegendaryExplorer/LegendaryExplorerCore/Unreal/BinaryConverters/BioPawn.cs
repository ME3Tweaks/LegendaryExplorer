using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.Toolkit.HighPerformance;
using UIndex = System.Int32;

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

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(AnimationMap.Select((kvp, i) => (kvp.Key, $"AnimationMap[{i}]")));

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            var span = AnimationMap.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                int value = span[i].Value;
                int originalValue = value;
                NameReference key = span[i].Key;
                Unsafe.AsRef(action).Invoke(ref value, $"AnimationMap[{key.Instanced}]");
                if (value != originalValue)
                {
                    span[i] = new KeyValuePair<NameReference, int>(key, value);
                }
            }
        }
    }
}
