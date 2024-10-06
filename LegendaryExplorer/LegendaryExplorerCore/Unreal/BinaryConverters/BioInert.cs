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
        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref NameEntryGuidPairs, sc.Serialize);
        }

        public static BioInert Create()
        {
            return new()
            {
                NameEntryGuidPairs = []
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

    public partial class SerializingContainer
    {
        public void Serialize(ref NameEntryGuidPair negp)
        {
            if (IsLoading)
            {
                negp = new NameEntryGuidPair();
            }
            Serialize(ref negp.Name);
            if (Game == MEGame.LE1)
            {
                Serialize(ref negp.Entry);
            }
            Serialize(ref negp.GUID);
        }
    }
}