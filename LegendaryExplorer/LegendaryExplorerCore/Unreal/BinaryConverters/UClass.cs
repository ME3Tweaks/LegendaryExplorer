using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UClass : UState
    {
        public UnrealFlags.EClassFlags ClassFlags;
        public UIndex OuterClass;
        public NameReference ClassConfigName;
        public NameReference[] unkNameList1; //ME1, ME2
        public OrderedMultiValueDictionary<NameReference, UIndex> ComponentNameToDefaultObjectMap;
        public OrderedMultiValueDictionary<UIndex, UIndex> Interfaces;
        public NameReference unkName2;//ME3, LE
        public uint unk2; //ME3, LE
        public uint le2ps3me2Unknown; //ME2, PS3 only and LE2
        public NameReference[] unkNameList2;//ME1/ME2
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
                sc.Serialize(ref unkName2);
                sc.Serialize(ref unk2);
            }
            else
            {
                sc.Serialize(ref unkNameList2, SCExt.Serialize);
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
                SuperClass = 0,
                Next = 0,
                Children = 0,
                ScriptBytes = Array.Empty<byte>(),
                IgnoreMask = (UnrealFlags.EProbeFunctions)ulong.MaxValue,
                LocalFunctionMap = new OrderedMultiValueDictionary<NameReference, UIndex>(),
                OuterClass = 0,
                ClassConfigName = "None",
                unkNameList1 = Array.Empty<NameReference>(),
                ComponentNameToDefaultObjectMap = new OrderedMultiValueDictionary<NameReference, UIndex>(),
                Interfaces = new OrderedMultiValueDictionary<UIndex, UIndex>(),
                unkName2 = "None",
                unkNameList2 = Array.Empty<NameReference>(),
                Defaults = 0,
                VirtualFunctionTable = Array.Empty<UIndex>()
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndices = base.GetUIndexes(game);
            uIndices.Add((OuterClass, "OuterClass"));
            uIndices.AddRange(ComponentNameToDefaultObjectMap.Select((kvp, i) => (kvp.Value, $"ComponentMap[{i}]")));

            uIndices.AddRange(Interfaces.SelectMany((kvp, i) => new[] { (kvp.Key, $"InterfacesMap[{i}]"), (kvp.Value, $"InterfacesMap[{i}].PropertyPointer") }));

            uIndices.Add((Defaults, "Defaults"));
            if (game is MEGame.UDK or MEGame.ME3 or MEGame.LE3)
            {
                uIndices.AddRange(VirtualFunctionTable.Select((u, i) => (u, $"FullFunctionsList[{i}]")));
            }

            return uIndices;
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
                names.Add((unkName2, nameof(unkName2)));
            }
            else
            {
                names.AddRange(unkNameList2.Select((name, i) => (name, $"unkNameList2[{i}]")));
            }

            return names;
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