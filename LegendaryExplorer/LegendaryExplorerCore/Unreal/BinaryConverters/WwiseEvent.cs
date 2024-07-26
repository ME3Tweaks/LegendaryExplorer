using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using Microsoft.Toolkit.HighPerformance;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class WwiseEvent : ObjectBinary
    {
        public uint WwiseEventID; //ME2
        public List<WwiseEventLink> Links;

        protected override void Serialize(SerializingContainer sc)
        {
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref WwiseEventID);
                sc.Serialize(ref Links, sc.Serialize);
            }
            else if (sc.Game.IsGame3())
            {
                if (Links is null || Links.Count == 0)
                {
                    Links = [new WwiseEventLink { WwiseStreams = [] }];
                }
                sc.Serialize(ref Links[0].WwiseStreams, sc.Serialize);
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
                Links = [new WwiseEventLink { WwiseStreams = [] }]
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            if (game.IsGame3())
            {
                ForEachUIndexInSpan(action, Links[0].WwiseStreams.AsSpan(), "WwiseStreams");
            }
            else if (game is MEGame.ME2)
            {
                for (int i = 0; i < Links.Count; i++)
                {
                    ForEachUIndexInSpan(action, Links[i].WwiseStreams.AsSpan(), $"Links[{i}].WwiseStreams");
                    ForEachUIndexInSpan(action, Links[i].WwiseBanks.AsSpan(), $"Links[{i}].WwiseBanks");
                }
            }
        }

        public class WwiseEventLink
        {
            public List<UIndex> WwiseBanks;
            public List<UIndex> WwiseStreams;
        }
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref WwiseEvent.WwiseEventLink l)
        {
            if (IsLoading)
            {
                l = new WwiseEvent.WwiseEventLink();
            }
            Serialize(ref l.WwiseBanks, Serialize);
            Serialize(ref l.WwiseStreams, Serialize);
        }
    }
}