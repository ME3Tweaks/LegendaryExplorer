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

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (!sc.Game.IsGame1())
            {
                throw new Exception($"BioTlkFileSet is not a valid class for {sc.Game}!");
            }
            sc.Serialize(ref TlkSets, SCExt.Serialize, static (SerializingContainer2 sc2, ref BioTlkSet tlkSet) =>
            {
                if (sc2.IsLoading)
                {
                    tlkSet = new BioTlkSet();
                }
                sc2.SerializeConstInt(2);
                sc2.Serialize(ref tlkSet.Male);
                sc2.Serialize(ref tlkSet.Female);
            });
        }

        public static BioTlkFileSet Create()
        {
            return new()
            {
                TlkSets = new ()
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
