using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using System.Numerics;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class MorphTarget : ObjectBinary
    {
        public MorphLODModel[] MorphLODModels;
        public BoneOffset[] BoneOffsets;

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref MorphLODModels, sc.Serialize);
            sc.Serialize(ref BoneOffsets, sc.Serialize);
        }

        public static MorphTarget Create()
        {
            return new()
            {
                MorphLODModels = [],
                BoneOffsets = []
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game) => BoneOffsets.Select((offset, i) => (offset.Bone, $"BoneOffsets[{i}]")).ToList();

        [StructLayout(LayoutKind.Sequential)]
        public struct MorphVertex
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

    public partial class SerializingContainer
    {
        public unsafe void Serialize(ref MorphTarget.MorphVertex vert)
        {
            if (ms.Endian.IsNative)
            {
                //MorphVertex has 18 bytes of data, but aligned size is 20
                Span<byte> span = stackalloc byte[20];
                if (IsLoading)
                {
                    span.Clear();
                    ms.Read(span[..18]);
                    vert = MemoryMarshal.Read<MorphTarget.MorphVertex>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in vert);
                    ms.Writer.Write(span[..18]);
                }
            }
            else
            {
                if (IsLoading)
                {
                    vert = new MorphTarget.MorphVertex();
                }
                Serialize(ref vert.PositionDelta);
                Serialize(ref vert.TangentZDelta);
                Serialize(ref vert.SourceIdx);
            }
        }
        public void Serialize(ref MorphTarget.MorphLODModel lod)
        {
            if (IsLoading)
            {
                lod = new MorphTarget.MorphLODModel();
            }
            Serialize(ref lod.Vertices, Serialize);
            Serialize(ref lod.NumBaseMeshVerts);
        }
        public void Serialize(ref MorphTarget.BoneOffset boff)
        {
            if (IsLoading)
            {
                boff = new MorphTarget.BoneOffset();
            }
            Serialize(ref boff.Offset);
            Serialize(ref boff.Bone);
        }
    }
}