using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using SharpDX;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public sealed class World : ObjectBinary
    {
        private UIndex PersistentLevel;
        private UIndex PersistentFaceFXAnimSet; //ME3
        private readonly LevelViewportInfo[] EditorViews = new LevelViewportInfo[4];
        private UIndex DecalManager; //ME1
        private UIndex[] ExtraReferencedObjects;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref PersistentLevel);
            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref PersistentFaceFXAnimSet);
            }

            for (int i = 0; i < 4; i++)
            {

                sc.Serialize(ref EditorViews[i]);
            }
            int dummy = 0;
            sc.Serialize(ref dummy);
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref DecalManager);
            }

            sc.Serialize(ref ExtraReferencedObjects);
        }
    }

    public class LevelViewportInfo
    {
        public Vector3 CamPosition;
        public Rotator CamRotation;
        public float CamOrthoZoom;
    }

    static class WorldSCExt
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
