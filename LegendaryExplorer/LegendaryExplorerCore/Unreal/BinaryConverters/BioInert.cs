using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class NameEntryGuidPair
    {
        public NameReference Name;
        public UIndex Entry;
        public Guid GUID;
    }

    public class BioInert : ObjectBinary
    {
        public NameEntryGuidPair[] NameEntryGuidPairs;//? Speculative name
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref NameEntryGuidPairs, SCExt.Serialize);
        }

        public static BioInert Create()
        {
            return new()
            {
                NameEntryGuidPairs = Array.Empty<NameEntryGuidPair>()
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            if (game == MEGame.LE1)
            {
                for (int i = 0; i < NameEntryGuidPairs.Length; i++)
                {
                    Unsafe.AsRef(in action).Invoke(ref NameEntryGuidPairs[i].Entry, $"NameEntryGuid[{i}]");
                }
            }
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();
            names.AddRange(NameEntryGuidPairs.Select((kvp, i) => (kvp.Name, $"Name[{i}]")));
            return names;
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref NameEntryGuidPair negp)
        {
            if (sc.IsLoading)
            {
                negp = new NameEntryGuidPair();
            }
            sc.Serialize(ref negp.Name);
            if (sc.Game == MEGame.LE1)
            {
                sc.Serialize(ref negp.Entry);
            }
            sc.Serialize(ref negp.GUID);
        }
    }
}