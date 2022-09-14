using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.Toolkit.HighPerformance;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UClass : UState
    {
        public UnrealFlags.EClassFlags ClassFlags;
        public UIndex OuterClass;
        public NameReference ClassConfigName;
        public NameReference[] unkNameList1; //ME1, ME2. Categories?
        public OrderedMultiValueDictionary<NameReference, UIndex> ComponentNameToDefaultObjectMap;
        public OrderedMultiValueDictionary<UIndex, UIndex> Interfaces;
        public NameReference DLLBindName;//ME3, LE. Always None?
        public uint unk2; //ME3, LE. ForceScriptOrder?
        public uint le2ps3me2Unknown; //ME2, PS3 only and LE2
        public NameReference[] unkNameList2;//ME1/ME2. Categories?
        public UIndex Defaults;
        public UIndex[] VirtualFunctionTable;//ME3

        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ClassFlags);
            if (sc.Game < MEGame.ME3 && sc.Pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                byte dummy = 0;
                sc.Serialize(ref dummy);
            }
            sc.Serialize(ref OuterClass);
            sc.Serialize(ref ClassConfigName);
            if (sc.Game < MEGame.ME3 && sc.Pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                sc.Serialize(ref unkNameList1, SCExt.Serialize);
            }
            sc.Serialize(ref ComponentNameToDefaultObjectMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref Interfaces, SCExt.Serialize, SCExt.Serialize);
            if (sc.Game >= MEGame.ME3 || sc.Pcc.Platform == MEPackage.GamePlatform.PS3)
            {
                sc.Serialize(ref DLLBindName);
                sc.Serialize(ref unk2);
            }
            else
            {
                sc.Serialize(ref unkNameList2, SCExt.Serialize);
                if (sc.IsLoading)
                {
                    // 11/22/2021 - Load "None" to make sure when porting cross games this is populated
                    DLLBindName = "None";
                    unk2 = 0;
                }
            }

            if (sc.Game is MEGame.LE2 || sc.Game == MEGame.ME2 && sc.Pcc.Platform == MEPackage.GamePlatform.PS3) //ME2 PS3 has extra integer here for some reason
            {
                sc.Serialize(ref le2ps3me2Unknown);
            }
            sc.Serialize(ref Defaults);
            if (sc.Game is MEGame.ME3 or MEGame.UDK or MEGame.LE3)
            {
                sc.Serialize(ref VirtualFunctionTable, SCExt.Serialize);
            }
        }

        public new static UClass Create()
        {
            return new()
            {
                ScriptBytes = Array.Empty<byte>(),
                IgnoreMask = (UnrealFlags.EProbeFunctions)ulong.MaxValue,
                LocalFunctionMap = new OrderedMultiValueDictionary<NameReference, UIndex>(),
                ClassConfigName = "None",
                unkNameList1 = Array.Empty<NameReference>(),
                ComponentNameToDefaultObjectMap = new OrderedMultiValueDictionary<NameReference, UIndex>(),
                Interfaces = new OrderedMultiValueDictionary<UIndex, UIndex>(),
                DLLBindName = "None",
                unkNameList2 = Array.Empty<NameReference>(),
                VirtualFunctionTable = Array.Empty<UIndex>()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.Add((ClassConfigName, nameof(ClassConfigName)));
            if (game <= MEGame.ME2)
            {
                names.AddRange(unkNameList1.Select((name, i) => (name, $"unkNameList1[{i}]")));
            }
            names.AddRange(ComponentNameToDefaultObjectMap.Select((kvp, i) => (kvp.Key, $"ComponentNameToDefaultObjectMap[{i}]")));
            if (game >= MEGame.ME3)
            {
                names.Add((DLLBindName, nameof(DLLBindName)));
            }
            else
            {
                names.AddRange(unkNameList2.Select((name, i) => (name, $"unkNameList2[{i}]")));
            }

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(action).Invoke(ref OuterClass, nameof(OuterClass));

            var span = ComponentNameToDefaultObjectMap.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                int value = span[i].Value;
                int originalValue = value;
                NameReference key = span[i].Key;
                Unsafe.AsRef(action).Invoke(ref value, $"ComponentNameToDefaultObjectMap[{key.Instanced}]");
                if (value != originalValue)
                {
                    span[i] = new KeyValuePair<NameReference, int>(key, value);
                }
            }

            var span2 = Interfaces.AsSpan();
            for (int i = 0; i < span2.Length; i++)
            {
                UIndex value = span2[i].Value;
                UIndex originalValue = value;
                UIndex key = span2[i].Key;
                UIndex originalKey = key;
                Unsafe.AsRef(action).Invoke(ref key, $"Interfaces[{i}]");
                Unsafe.AsRef(action).Invoke(ref value, $"Interfaces[{i}]");
                if (value != originalValue || key != originalKey)
                {
                    span2[i] = new KeyValuePair<UIndex, UIndex>(key, value);
                }
            }


            Unsafe.AsRef(action).Invoke(ref Defaults, nameof(Defaults));
            if (game is MEGame.UDK or MEGame.ME3 or MEGame.LE3)
            {
                ForEachUIndexInSpan(action, VirtualFunctionTable.AsSpan(), nameof(VirtualFunctionTable));
            }
        }

        /// <summary>
        /// Rebuilds the LocalFunctions table with direct descendents of this node. Items of class 'Function' will participate.
        /// </summary>
        public void UpdateLocalFunctions()
        {
            LocalFunctionMap.Clear();
            foreach (ExportEntry c in Export.GetChildren<ExportEntry>().Reverse())
            {
                if (c.ClassName == "Function")
                {
                    LocalFunctionMap.Add(c.ObjectName, c.UIndex);
                }
            }
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref UnrealFlags.EClassFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (UnrealFlags.EClassFlags)sc.ms.ReadUInt32();
            }
            else
            {
                sc.ms.Writer.WriteUInt32((uint)flags);
            }
        }
    }
}