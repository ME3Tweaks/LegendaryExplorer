using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.Toolkit.HighPerformance;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
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

        public static PrefabInstance Create()
        {
            return new()
            {
                ArchetypeToInstanceMap = new OrderedMultiValueDictionary<UIndex, UIndex>(),
                ObjectMap = new OrderedMultiValueDictionary<UIndex, int>()
            };
        }
        
        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ref TAction a = ref Unsafe.AsRef(action);

            Span<KeyValuePair<int, int>> span = ArchetypeToInstanceMap.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                int key = span[i].Key;
                int value = span[i].Value;
                int originalKey = key;
                int originalValue = key;
                Unsafe.AsRef(action).Invoke(ref key, $"ArchetypeToInstanceMap[{i}]");
                Unsafe.AsRef(action).Invoke(ref value, $"ArchetypeToInstanceMap[{i}]");
                if (key != originalKey || value != originalValue)
                {
                    span[i] = new(key, value);
                }
            }
            ForEachUIndexKeyInOrderedMultiValueDictionary(action, ObjectMap.AsSpan(), nameof(ObjectMap));
        }
    }
}
