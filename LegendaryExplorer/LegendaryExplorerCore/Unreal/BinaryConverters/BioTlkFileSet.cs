using System;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioTlkFileSet : ObjectBinary
    {
        public UMultiMap<NameReference, BioTlkSet> TlkSets; //TODO: Make this a UMap

        protected override void Serialize(SerializingContainer sc)
        {
            if (!sc.Game.IsGame1())
            {
                throw new Exception($"BioTlkFileSet is not a valid class for {sc.Game}!");
            }
            sc.Serialize(ref TlkSets, sc.Serialize, (ref BioTlkSet tlkSet) =>
            {
                if (sc.IsLoading)
                {
                    tlkSet = new BioTlkSet();
                }
                sc.SerializeConstInt(2);
                sc.Serialize(ref tlkSet.Male);
                sc.Serialize(ref tlkSet.Female);
            });
        }

        public static BioTlkFileSet Create()
        {
            return new()
            {
                TlkSets = []
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            foreach ((NameReference lang, BioTlkSet bioTlkSet) in TlkSets)
            {
                Unsafe.AsRef(in action).Invoke(ref bioTlkSet.Male, $"{lang}: Male");
                Unsafe.AsRef(in action).Invoke(ref bioTlkSet.Female, $"{lang}: Female");
            }
        }

        public class BioTlkSet
        {
            public UIndex Male;
            public UIndex Female;
        }
    }
}
