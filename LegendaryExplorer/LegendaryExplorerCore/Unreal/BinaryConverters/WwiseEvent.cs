﻿using System;
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