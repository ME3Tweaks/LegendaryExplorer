using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class WwiseEvent : ObjectBinary
    {
        public uint WwiseEventID; //ME2
        public List<WwiseEventLink> Links;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref WwiseEventID);
                sc.Serialize(ref Links, SCExt.Serialize);
            }
            else if (sc.Game.IsGame3())
            {
                if (Links is null || Links.Count == 0)
                {
                    Links = new List<WwiseEventLink> { new WwiseEventLink { WwiseStreams = new List<UIndex>() } };
                }
                sc.Serialize(ref Links[0].WwiseStreams, SCExt.Serialize);
            }
            else if (sc.Game == MEGame.LE2)
            {
                sc.Serialize(ref WwiseEventID);
            }
            else
            {
                throw new Exception($"WwiseEvent is not a valid class for {sc.Game}!");
            }
        }

        public static WwiseEvent Create()
        {
            return new()
            {
                Links = new List<WwiseEventLink> { new WwiseEventLink { WwiseStreams = new List<UIndex>() } }
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndexes = base.GetUIndexes(game);

            if (game.IsGame3())
            {
                uIndexes.AddRange(Links[0].WwiseStreams.Select(((u, i) => (u, $"Wwisestreams[{i}]"))));
            }
            else if (game == MEGame.ME2) // LE2 doesn't have links, they're in properties
            {
                for (int i = 0; i < Links.Count; i++)
                {
                    uIndexes.AddRange(Links[i].WwiseBanks.Select((u, j) => (u, $"Links[{i}].WwiseBanks[{j}]")));
                    uIndexes.AddRange(Links[i].WwiseStreams.Select((u, j) => (u, $"Links[{i}].WwiseStreams[{j}]")));
                }
            }

            return uIndexes;
        }

        public class WwiseEventLink
        {
            public List<UIndex> WwiseBanks;
            public List<UIndex> WwiseStreams;
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref WwiseEvent.WwiseEventLink l)
        {
            if (sc.IsLoading)
            {
                l = new WwiseEvent.WwiseEventLink();
            }
            sc.Serialize(ref l.WwiseBanks, Serialize);
            sc.Serialize(ref l.WwiseStreams, Serialize);
        }
    }
}