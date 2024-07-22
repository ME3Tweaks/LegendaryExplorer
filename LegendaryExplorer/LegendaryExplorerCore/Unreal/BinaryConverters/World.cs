using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public sealed class World : ObjectBinary
    {
        private UIndex PersistentLevel;
        private UIndex PersistentFaceFXAnimSet; //ME3/LE
        private Fixed4<LevelViewportInfo> EditorViews;
        private UIndex DecalManager; //ME1/LE1
        private float unkFloat; //UDK
        public UIndex[] ExtraReferencedObjects;

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref PersistentLevel);
            if (sc.Game == MEGame.ME3 || sc.Game.IsLEGame())
            {
                sc.Serialize(ref PersistentFaceFXAnimSet);
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

            sc.Serialize(ref ExtraReferencedObjects, sc.Serialize);
        }

        public static World Create()
        {
            var world = new World
            {
                ExtraReferencedObjects = []
            };
            for (int i = 0; i < 4; i++)
            {
                world.EditorViews[i] = new LevelViewportInfo();
            }

            return world;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            Unsafe.AsRef(in action).Invoke(ref PersistentLevel, nameof(PersistentLevel));
            if (game == MEGame.ME3 || game.IsLEGame())
            {
                Unsafe.AsRef(in action).Invoke(ref PersistentFaceFXAnimSet, nameof(PersistentFaceFXAnimSet));
            }
            else if (game.IsGame1())
            {
                Unsafe.AsRef(in action).Invoke(ref DecalManager, nameof(DecalManager));
            }
            ForEachUIndexInSpan(action, ExtraReferencedObjects.AsSpan(), nameof(ExtraReferencedObjects));
        }
    }

    public class LevelViewportInfo
    {
        public Vector3 CamPosition;
        public Rotator CamRotation;
        public float CamOrthoZoom;
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref LevelViewportInfo info)
        {
            if (IsLoading)
            {
                info = new LevelViewportInfo();
            }
            Serialize(ref info.CamPosition);
            Serialize(ref info.CamRotation);
            Serialize(ref info.CamOrthoZoom);
        }
    }
}