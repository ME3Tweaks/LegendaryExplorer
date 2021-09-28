using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

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
            sc.Serialize(ref EntryGuidNumPairs, SCExt.Serialize);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> indices = new List<(UIndex, string)>(EntryGuidNumPairs.Length);
            if (game == MEGame.LE1)
            {
                for (int i = 0; i < EntryGuidNumPairs.Length; i++)
                {
                    indices.Add((EntryGuidNumPairs[i].Entry, $"EntryGuidNumPair[{i}]")); // how to handle nulls?
                }
            }
            return indices;
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