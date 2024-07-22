using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;
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
        public UMultiMap<NameReference, UIndex> LocalFunctionMap; //TODO: Make this a UMap
        protected override void Serialize(SerializingContainer sc)
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
            sc.Serialize(ref LocalFunctionMap, sc.Serialize, sc.Serialize);
        }

        public static UState Create()
        {
            return new()
            {
                ScriptBytes = [],
                IgnoreMask = (EProbeFunctions)ulong.MaxValue,
                LocalFunctionMap = []
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
            ForEachUIndexValueInMultiMap(action, LocalFunctionMap, nameof(LocalFunctionMap));
        }
    }

    public struct LabelTableEntry
    {
        public NameReference NameRef;
        public uint Offset; // standard bytescript MemOffs
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref EStateFlags flags)
        {
            if (IsLoading)
            {
                flags = (EStateFlags)ms.ReadUInt32();
            }
            else
            {
                ms.Writer.WriteUInt32((uint)flags);
            }
        }
        public void Serialize(ref EProbeFunctions flags)
        {
            if (IsLoading)
            {
                flags = (EProbeFunctions)ms.ReadUInt64();
            }
            else
            {
                ms.Writer.WriteUInt64((ulong)flags);
            }
        }
    }
}