using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public sealed class World : ObjectBinary
    {
        private UIndex PersistentLevel;
        private UIndex PersistentFaceFXAnimSet; //ME3
        private readonly LevelViewportInfo[] EditorViews = new LevelViewportInfo[4];
        private UIndex DecalManager; //ME1
        private float unkFloat; //UDK
        public UIndex[] ExtraReferencedObjects;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref PersistentLevel);
            if (sc.Game == MEGame.ME3 || sc.Game.IsLEGame())
            {
                sc.Serialize(ref PersistentFaceFXAnimSet);
            }
            else if (sc.IsLoading)
            {
                PersistentFaceFXAnimSet = new UIndex(0);
            }

            for (int i = 0; i < 4; i++)
            {

                sc.Serialize(ref EditorViews[i]);
            }

            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref unkFloat);
            }
            int dummy = 0;
            sc.Serialize(ref dummy);
            if (sc.Game.IsGame1())
            {
                sc.Serialize(ref DecalManager);
            }
            else if (sc.IsLoading)
            {
                DecalManager = new UIndex(0);
            }

            sc.Serialize(ref ExtraReferencedObjects, SCExt.Serialize);
        }

        public static World Create()
        {
            var world = new World
            {
                PersistentLevel = 0,
                PersistentFaceFXAnimSet = 0,
                DecalManager = 0,
                ExtraReferencedObjects = Array.Empty<UIndex>()
            };
            for (int i = 0; i < 4; i++)
            {
                world.EditorViews[i] = new LevelViewportInfo();
            }

            return world;
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)> { (PersistentLevel, "PersistentLevel") };
            if (game == MEGame.ME3 || game.IsLEGame())
            {
                uIndexes.Add((PersistentFaceFXAnimSet, "PersistentFaceFXAnimSet"));
            }
            else if (game.IsGame1())
            {
                uIndexes.Add((DecalManager, "DecalManager"));
            }
            uIndexes.AddRange(ExtraReferencedObjects.Select((t, i) => (t, $"ExtraReferencedObjects[{i}]")));

            return uIndexes;
        }
    }

    public class LevelViewportInfo
    {
        public Vector3 CamPosition;
        public Rotator CamRotation;
        public float CamOrthoZoom;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref LevelViewportInfo info)
        {
            if (sc.IsLoading)
            {
                info = new LevelViewportInfo();
            }
            sc.Serialize(ref info.CamPosition);
            sc.Serialize(ref info.CamRotation);
            sc.Serialize(ref info.CamOrthoZoom);
        }
    }
}