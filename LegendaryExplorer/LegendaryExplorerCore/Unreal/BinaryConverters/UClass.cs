using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;
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
        public UMultiMap<NameReference, UIndex> ComponentNameToDefaultObjectMap; //TODO: Make this a UMap
        public List<ImplementedInterface> Interfaces;
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
            sc.Serialize(ref Interfaces, SCExt.Serialize);
            if (sc.Game is MEGame.UDK)
            {
                NameReference[] dummyArray = Array.Empty<NameReference>();
                sc.Serialize(ref dummyArray, SCExt.Serialize);
                sc.Serialize(ref dummyArray, SCExt.Serialize);
                sc.Serialize(ref dummyArray, SCExt.Serialize);
                sc.Serialize(ref dummyArray, SCExt.Serialize);
                bool dummyBool = false;
                sc.Serialize(ref dummyBool);
                sc.Serialize(ref dummyArray, SCExt.Serialize);
                string dummyString = "";
                sc.Serialize(ref dummyString);
            }
            if (sc.Game >= MEGame.ME3 || sc.Pcc.Platform == MEPackage.GamePlatform.PS3)
            {
                sc.Serialize(ref DLLBindName);
                if (sc.Game is not MEGame.UDK)
                {
                    sc.Serialize(ref unk2);
                }
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
            if (sc.Game.IsGame3())
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
                LocalFunctionMap = new(),
                ClassConfigName = "None",
                unkNameList1 = Array.Empty<NameReference>(),
                ComponentNameToDefaultObjectMap = new(),
                Interfaces = new(),
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
            Unsafe.AsRef(in action).Invoke(ref OuterClass, nameof(OuterClass));

            ForEachUIndexValueInMultiMap(action, ComponentNameToDefaultObjectMap, nameof(ComponentNameToDefaultObjectMap));

            Span<ImplementedInterface> interfacesSpan = Interfaces.AsSpan();
            for (int i = 0; i < interfacesSpan.Length; i++)
            {
                Unsafe.AsRef(in action).Invoke(ref interfacesSpan[i].Class, $"{nameof(Interfaces)}[{i}].{nameof(ImplementedInterface.Class)}");
                Unsafe.AsRef(in action).Invoke(ref interfacesSpan[i].PointerProperty, $"{nameof(Interfaces)}[{i}].{nameof(ImplementedInterface.PointerProperty)}");
            }

            Unsafe.AsRef(in action).Invoke(ref Defaults, nameof(Defaults));
            if (game is MEGame.ME3 or MEGame.LE3)
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
        public struct ImplementedInterface
        {
            public UIndex Class;
            public UIndex PointerProperty;

            public ImplementedInterface(UIndex @class, UIndex pointerProperty)
            {
                Class = @class;
                PointerProperty = pointerProperty;
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

        public static void Serialize(this SerializingContainer2 sc, ref UClass.ImplementedInterface implementedInterface)
        {
            sc.Serialize(ref implementedInterface.Class);
            sc.Serialize(ref implementedInterface.PointerProperty);
        }
    }
}