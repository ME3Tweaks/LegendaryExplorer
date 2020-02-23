using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class UClass : UState
    {
        public UnrealFlags.EClassFlags ClassFlags;
        public UIndex OuterClass;
        public NameReference ClassConfigName;
        public NameReference[] unkNameList1; //ME1, ME2
        public OrderedMultiValueDictionary<NameReference, UIndex> ComponentNameToDefaultObjectMap;
        public OrderedMultiValueDictionary<UIndex, UIndex> Interfaces;
        public NameReference unkName2;//ME3
        public uint unk2; //ME3
        public NameReference[] unkNameList2;//ME1/ME2
        public UIndex Defaults;
        public UIndex[] FullFunctionsList;//ME3

        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ClassFlags);
            if (sc.Game < MEGame.ME3)
            {
                byte dummy = 0;
                sc.Serialize(ref dummy);
            }
            sc.Serialize(ref OuterClass);
            sc.Serialize(ref ClassConfigName);
            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref unkNameList1, SCExt.Serialize);
            }
            sc.Serialize(ref ComponentNameToDefaultObjectMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref Interfaces, SCExt.Serialize, SCExt.Serialize);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref unkName2);
                sc.Serialize(ref unk2);
            }
            else
            {
                sc.Serialize(ref unkNameList2, SCExt.Serialize);
            }
            sc.Serialize(ref Defaults);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref FullFunctionsList, SCExt.Serialize);
            }
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndices = base.GetUIndexes(game);
            uIndices.Add((OuterClass, "OuterClass"));
            uIndices.AddRange(ComponentNameToDefaultObjectMap.Select((kvp, i) => (kvp.Value, $"ComponentMap[{i}]")));

            if (game >= MEGame.ME3)
            {
                uIndices.AddRange(Interfaces.SelectMany((kvp, i) => new []{(kvp.Key, $"InterfacesMap[{i}]"), (kvp.Value, $"InterfacesMap[{i}].PropertyPointer")}));
            }

            uIndices.Add((Defaults, "Defaults"));
            if (game >= MEGame.ME3)
            {
                uIndices.AddRange(FullFunctionsList.Select((u, i) => (u, $"FullFunctionsList[{i}]")));
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
            if (game == MEGame.ME3)
            {
                names.Add((unkName2, nameof(unkName2)));
            }

            if (game <= MEGame.ME2)
            {
                names.AddRange(unkNameList2.Select((name, i) => (name, $"unkNameList2[{i}]")));
            }

            return names;
        }
    }
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

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
