using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.Toolkit.HighPerformance;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UState : UStruct
    {
        public EProbeFunctions ProbeMask;
        public EProbeFunctions IgnoreMask;
        public ushort LabelTableOffset;
        public EStateFlags StateFlags;
        public OrderedMultiValueDictionary<NameReference, UIndex> LocalFunctionMap;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            if (sc.Game is MEGame.UDK)
            {
                uint temp = (uint)ProbeMask;
                sc.Serialize(ref temp);
                if (sc.IsLoading)
                {
                    ProbeMask = (EProbeFunctions)temp; //this is going to be garbage...
                    IgnoreMask = (EProbeFunctions)ulong.MaxValue;
                }
            }
            else
            {
                sc.Serialize(ref ProbeMask);
                sc.Serialize(ref IgnoreMask);
            }
            sc.Serialize(ref LabelTableOffset);
            sc.Serialize(ref StateFlags);
            sc.Serialize(ref LocalFunctionMap, SCExt.Serialize, SCExt.Serialize);
        }

        public static UState Create()
        {
            return new()
            {
                ScriptBytes = Array.Empty<byte>(),
                IgnoreMask = (EProbeFunctions)ulong.MaxValue,
                LocalFunctionMap = new OrderedMultiValueDictionary<NameReference, UIndex>()
            };
        }
        
        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.AddRange(LocalFunctionMap.Select((kvp, i) => (kvp.Key, $"LocalFunctions[{i}]")));

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            var span = LocalFunctionMap.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                int value = span[i].Value;
                int originalValue = value;
                NameReference key = span[i].Key;
                Unsafe.AsRef(action).Invoke(ref value, $"LocalFunctionMap[{key.Instanced}]");
                if (value != originalValue)
                {
                    span[i] = new KeyValuePair<NameReference, int>(key, value);
                }
            }
        }
    }

    public struct LabelTableEntry
    {
        public NameReference NameRef;
        public uint Offset; // standard bytescript MemOffs
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref EStateFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (EStateFlags)sc.ms.ReadUInt32();
            }
            else
            {
                sc.ms.Writer.WriteUInt32((uint)flags);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref EProbeFunctions flags)
        {
            if (sc.IsLoading)
            {
                flags = (EProbeFunctions)sc.ms.ReadUInt64();
            }
            else
            {
                sc.ms.Writer.WriteUInt64((ulong)flags);
            }
        }
    }
}