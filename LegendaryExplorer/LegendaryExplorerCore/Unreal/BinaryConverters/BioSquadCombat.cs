using System;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class EntryGuidNumPair
    {
        public UIndex Entry;
        public Guid GUID;
        public int UnknownZero;
    }

    public class BioSquadCombat : ObjectBinary
    {
        public EntryGuidNumPair[] EntryGuidNumPairs;//? Speculative name
        protected override void Serialize(SerializingContainer2 sc)
        {
            if (!sc.Game.IsGame1())
                return; // No binary except in Game 1

            sc.Serialize(ref EntryGuidNumPairs, SCExt.Serialize);
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            if (game == MEGame.LE1)
            {
                for (int i = 0; i < EntryGuidNumPairs.Length; i++)
                {
                    Unsafe.AsRef(in action).Invoke(ref EntryGuidNumPairs[i].Entry, $"EntryGuidNumPair[{i}]");
                }
            }
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref EntryGuidNumPair egnp)
        {
            if (sc.IsLoading)
            {
                egnp = new EntryGuidNumPair();
            }
            if (sc.Game == MEGame.LE1)
            {
                sc.Serialize(ref egnp.Entry);
            }
            sc.Serialize(ref egnp.GUID);
            sc.Serialize(ref egnp.UnknownZero);
        }
    }
}