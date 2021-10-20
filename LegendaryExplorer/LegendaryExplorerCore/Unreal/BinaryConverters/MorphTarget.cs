using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using System.Numerics;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class MorphTarget : ObjectBinary
    {
        public MorphLODModel[] MorphLODModels;
        public BoneOffset[] BoneOffsets;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref MorphLODModels, SCExt.Serialize);
            sc.Serialize(ref BoneOffsets, SCExt.Serialize);
        }

        public static MorphTarget Create()
        {
            return new()
            {
                MorphLODModels = Array.Empty<MorphLODModel>(),
                BoneOffsets = Array.Empty<BoneOffset>()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game) => new List<(NameReference, string)>(BoneOffsets.Select((offset, i) => (offset.Bone, $"BoneOffsets[{i}]")));

        public class MorphVertex
        {
            public Vector3 PositionDelta;
            public PackedNormal TangentZDelta;
            public ushort SourceIdx;
        }

        public class MorphLODModel
        {
            public MorphVertex[] Vertices;
            public int NumBaseMeshVerts;
        }

        public class BoneOffset
        {
            public Vector3 Offset;
            public NameReference Bone;
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(SerializingContainer2 sc, ref MorphTarget.MorphVertex vert)
        {
            if (sc.IsLoading)
            {
                vert = new MorphTarget.MorphVertex();
            }
            sc.Serialize(ref vert.PositionDelta);
            sc.Serialize(ref vert.TangentZDelta);
            sc.Serialize(ref vert.SourceIdx);
        }
        public static void Serialize(SerializingContainer2 sc, ref MorphTarget.MorphLODModel lod)
        {
            if (sc.IsLoading)
            {
                lod = new MorphTarget.MorphLODModel();
            }
            sc.Serialize(ref lod.Vertices, Serialize);
            sc.Serialize(ref lod.NumBaseMeshVerts);
        }
        public static void Serialize(SerializingContainer2 sc, ref MorphTarget.BoneOffset boff)
        {
            if (sc.IsLoading)
            {
                boff = new MorphTarget.BoneOffset();
            }
            sc.Serialize(ref boff.Offset);
            sc.Serialize(ref boff.Bone);
        }
    }
}