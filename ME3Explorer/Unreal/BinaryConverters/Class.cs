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
    public class Class : ObjectBinary
    {
        public UIndex SuperClass;
        public uint unk1;
        public UIndex ChildListStart;
        public long IgnoreMask;
        public byte[] StateBlock;
        public StateFlags StateFlags;
        public OrderedMultiValueDictionary<NameReference, UIndex> LocalFunctionMap;
        public UnrealFlags.EClassFlags ClassFlags;
        public UIndex OuterClass;
        public NameReference unkName1; //Seems to be either None, Editor, Engine, or Game
        public NameReference[] unkNameList1; //ME1, ME2
        public OrderedMultiValueDictionary<NameReference, UIndex> ComponentMap;
        public OrderedMultiValueDictionary<UIndex, UIndex> InterfacesMap;
        public NameReference unkName2;//ME3
        public uint unk2; //ME3
        public NameReference[] unkNameList2;//ME1/ME2
        public UIndex Defaults;
        public UIndex[] FullFunctionsList;//ME3

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SuperClass);
            sc.Serialize(ref unk1);
            sc.Serialize(ref ChildListStart);
            sc.Serialize(ref IgnoreMask);
            if (sc.IsLoading)
            {
                int startOffset = (int)sc.ms.Position;
                int counter = 0;
                while (sc.ms.Position < sc.ms.Length && counter < 10)
                {
                    if (sc.ms.ReadByte() == 255)
                    {
                        ++counter;
                    }
                    else
                    {
                        counter = 0;
                    }
                }

                int length = (int)sc.ms.Position - startOffset;
                sc.ms.JumpTo(startOffset);
                StateBlock = sc.ms.ReadToBuffer(length);
            }
            else
            {
                sc.ms.WriteFromBuffer(StateBlock);
            }
            sc.Serialize(ref StateFlags);
            sc.Serialize(ref LocalFunctionMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref ClassFlags);
            if (sc.Game < MEGame.ME3)
            {
                byte dummy = 0;
                sc.Serialize(ref dummy);
            }
            sc.Serialize(ref OuterClass);
            sc.Serialize(ref unkName1);
            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref unkNameList1, SCExt.Serialize);
            }
            sc.Serialize(ref ComponentMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref InterfacesMap, SCExt.Serialize, SCExt.Serialize);
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
            var uIndices = new List<(UIndex, string)>
            {
                (SuperClass, "SuperClass"),
                (ChildListStart, "ChildListStart")
            };
            uIndices.AddRange(LocalFunctionMap.Select((kvp, i) => (kvp.Value, $"LocalFunctions[{i}]")));
            uIndices.Add((OuterClass, "OuterClass"));
            uIndices.AddRange(ComponentMap.Select((kvp, i) => (kvp.Value, $"ComponentMap[{i}]")));

            if (game >= MEGame.ME3)
            {
                uIndices.AddRange(InterfacesMap.SelectMany((kvp, i) => new []{(kvp.Key, $"InterfacesMap[{i}]"), (kvp.Value, $"InterfacesMap[{i}].PropertyPointer")}));
            }

            uIndices.Add((Defaults, "Defaults"));
            if (game >= MEGame.ME3)
            {
                uIndices.AddRange(FullFunctionsList.Select((u, i) => (u, $"FullFunctionsList[{i}]")));
            }

            return uIndices;
        }
    }
    [Flags]
    public enum StateFlags : uint
    {
        None = 0,
        Editable = 1,
        Auto = 2,
        Simulated = 4,
    }
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref StateFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (StateFlags)sc.ms.ReadUInt32();
            }
            else
            {
                sc.ms.WriteUInt32((uint)flags);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref UnrealFlags.EClassFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (UnrealFlags.EClassFlags)sc.ms.ReadUInt32();
            }
            else
            {
                sc.ms.WriteUInt32((uint)flags);
            }
        }
    }

}
