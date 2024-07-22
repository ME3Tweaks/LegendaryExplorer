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
        protected override void Serialize(SerializingContainer sc)
        {
            if (!sc.Game.IsGame1())
                return; // No binary except in Game 1

            sc.Serialize(ref EntryGuidNumPairs, sc.Serialize);
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

    public partial class SerializingContainer
    {
        public void Serialize(ref EntryGuidNumPair egnp)
        {
            if (IsLoading)
            {
                egnp = new EntryGuidNumPair();
            }
            if (Game == MEGame.LE1)
            {
                Serialize(ref egnp.Entry);
            }
            Serialize(ref egnp.GUID);
            Serialize(ref egnp.UnknownZero);
        }
    }
}