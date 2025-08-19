using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal
{
    public class PSK
    {
        // standard official PSK stuff, in the official order
        public List<Vector3> Points;
        public List<PSKWedge> Wedges;
        public List<PSKTriangle> Faces;
        public List<PSKMaterial> Materials;
        public List<PSA.PSABone> Bones;
        public List<PSKWeight> Weights;
        // nonstandard, but some things can read/write this data
        public List<Vector3> VertexNormals;
        public List<MorphInfo> Morphs;
        public List<MorphDelta> MorphData;

        private const int version = 1999801;

        protected void Serialize(SerializingContainer sc)
        {
            var mainHeader = new PSA.ChunkHeader
            {
                ChunkID = "ACTRHEAD",
                Version = version,
            };
            sc.Serialize(ref mainHeader);

            var pointsHeader = new PSA.ChunkHeader
            {
                ChunkID = "PNTS0000",
                Version = version,
                DataSize = 0xC,
                DataCount = Points?.Count ?? 0
            };
            sc.Serialize(ref pointsHeader);
            sc.Serialize(ref Points, pointsHeader.DataCount, sc.Serialize);
            var wedgesHeader = new PSA.ChunkHeader
            {
                ChunkID = "VTXW0000",
                Version = version,
                DataSize = 0x10,
                DataCount = Wedges?.Count ?? 0
            };
            sc.Serialize(ref wedgesHeader);
            sc.Serialize(ref Wedges, wedgesHeader.DataCount, sc.Serialize);
            var facesHeader = new PSA.ChunkHeader
            {
                ChunkID = "FACE0000",
                Version = version,
                DataSize = 0xC,
                DataCount = Faces?.Count ?? 0
            };
            sc.Serialize(ref facesHeader);
            sc.Serialize(ref Faces, facesHeader.DataCount, sc.Serialize);
            var matsHeader = new PSA.ChunkHeader
            {
                ChunkID = "MATT0000",
                Version = version,
                DataSize = 0x58,
                DataCount = Materials?.Count ?? 0
            };
            sc.Serialize(ref matsHeader);
            sc.Serialize(ref Materials, matsHeader.DataCount, sc.Serialize);
            var bonesHeader = new PSA.ChunkHeader
            {
                ChunkID = "REFSKELT",
                Version = version,
                DataSize = 0x78,
                DataCount = Bones?.Count ?? 0
            };
            sc.Serialize(ref bonesHeader);
            sc.Serialize(ref Bones, bonesHeader.DataCount, sc.Serialize);
            var weightsHeader = new PSA.ChunkHeader
            {
                ChunkID = "RAWWEIGHTS",
                Version = version,
                DataSize = 0xC,
                DataCount = Weights?.Count ?? 0
            };
            sc.Serialize(ref weightsHeader);
            sc.Serialize(ref Weights, weightsHeader.DataCount, sc.Serialize);

            // if we are reading this from a file, some extra bits may or may not be present in any order
            if (sc.IsLoading)
            {
                if (sc.TryReadString(out var chunkHeader) && chunkHeader == "VTXNORMS")
                {
                    SerializeVertexNormals(sc);
                }
            }
            // if we are writing, just emit how we expect
            else
            {
                // some programs support slightly nonstandard data like vertex norms, which we will emit/read if present
                if (VertexNormals != null && VertexNormals.Count > 0)
                {
                    SerializeVertexNormals(sc);
                }

                // some programs (including Blender 4.2+ PSK plugin) can understand slightly nonstandard data such as the vertex offsets of morph targets, or shape keys in Blender's terminology. 
                // If the code does not put this in explicitly, it will emit a standard psk. 
                if (Morphs != null && Morphs.Count != 0)
                {
                    var morphsHeader = new PSA.ChunkHeader
                    {
                        ChunkID = "MRPHINFO",
                        Version = version,
                        DataSize = 0x44,
                        DataCount = Morphs.Count
                    };
                    sc.Serialize(ref morphsHeader);
                    sc.Serialize(ref Morphs, morphsHeader.DataCount, sc.Serialize);

                    var morphDataHeader = new PSA.ChunkHeader
                    {
                        ChunkID = "MRPHDATA",
                        Version = version,
                        DataSize = 0x1c,
                        DataCount = MorphData.Count
                    };
                    sc.Serialize(ref morphDataHeader);
                    sc.Serialize(ref MorphData, morphDataHeader.DataCount, sc.Serialize);
                }
            }
        }

        private void SerializeVertexNormals(SerializingContainer sc)
        {
            var VertexNormalsHeader = new PSA.ChunkHeader
            {
                ChunkID = "VTXNORMS",
                Version = version,
                DataSize = 0xc,
                DataCount = VertexNormals?.Count ?? 0
            };
            sc.Serialize(ref VertexNormalsHeader);
            sc.Serialize(ref VertexNormals, VertexNormalsHeader.DataCount, sc.Serialize);
        }

        public void ToFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            Serialize(new SerializingContainer(fs, null));
        }

        public static PSK FromFile(string filePath)
        {
            var psk = new PSK();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            psk.Serialize(new SerializingContainer(fs, null, true));
            return psk;
        }

        public static PSK CreateFromSkeletalMesh(SkeletalMesh skelMesh, int lodIdx = 0, bool includeVertexNormals = false)
        {
            var lod = skelMesh.LODModels[lodIdx];

            int numVertices = (int)lod.NumVertices;
            var psk = new PSK
            {
                Points = [],
                Wedges = [],
                Faces = [],
                Materials = [],
                Bones = [],
                Weights = [],
                Morphs = [],
                MorphData = [],
                VertexNormals = []
            };
            int numTriangles = 0;
            var matIndices = new byte[numVertices];
            foreach (SkelMeshSection section in lod.Sections)
            {
                numTriangles += section.NumTriangles;
                for (uint t = 0; t < section.NumTriangles; t++)
                {
                    uint baseIndex = section.BaseIndex;
                    int i1 = lod.IndexBuffer[baseIndex + t * 3];
                    int i2 = lod.IndexBuffer[baseIndex + t * 3 + 1];
                    int i3 = lod.IndexBuffer[baseIndex + t * 3 + 2];
                    byte materialIndex = (byte)section.MaterialIndex;
                    matIndices[i1] = materialIndex;
                    matIndices[i2] = materialIndex;
                    matIndices[i3] = materialIndex;
                    psk.Faces.Add(new PSKTriangle
                    {
                        // intentionally flipped; corner ordering determines normal direction, and flipped normals will mess everything up
                        WedgeIdx1 = (ushort)i1,
                        WedgeIdx0 = (ushort)i2,
                        WedgeIdx2 = (ushort)i3,
                        MatIndex = materialIndex
                    });
                }
            }

            foreach (int uIndex in skelMesh.Materials)
            {
                psk.Materials.Add(new PSKMaterial
                {
                    Name = skelMesh.Export.FileRef.GetEntry(uIndex)?.ObjectName.Instanced ?? ""
                });
            }

            const float weightUnpackScale = 1f / 255;
            if (lod.ME1VertexBufferGPUSkin != null)
            {
                for (int i = 0; i < lod.ME1VertexBufferGPUSkin.Length; i++)
                {
                    SoftSkinVertex vertex = lod.ME1VertexBufferGPUSkin[i];
                    psk.Points.Add(new Vector3(vertex.Position.X, vertex.Position.Y * -1, vertex.Position.Z));
                    psk.Wedges.Add(new PSKWedge
                    {
                        MatIndex = matIndices[i],
                        PointIndex = (ushort)i,
                        U = vertex.UV.X,
                        V = vertex.UV.Y
                    });
                    for (int j = 0; j < 4; j++)
                    {
                        if (vertex.InfluenceWeights[j] == 0)
                        {
                            break;
                        }

                        // first, we need to find the chunk containing this vertex:
                        var chunk = lod.Chunks.Last(x => x.BaseVertexIndex <= i);

                        psk.Weights.Add(new PSKWeight
                        {
                            Bone = chunk.BoneMap[vertex.InfluenceBones[j]],
                            Weight = vertex.InfluenceWeights[j] * weightUnpackScale,
                            Point = i
                        });
                    }
                }
            }
            else
            {
                for (int i = 0; i < lod.VertexBufferGPUSkin.VertexData.Length; i++)
                {
                    GPUSkinVertex vertex = lod.VertexBufferGPUSkin.VertexData[i];
                    psk.Points.Add(new Vector3(vertex.Position.X, vertex.Position.Y * -1, vertex.Position.Z));
                    psk.Wedges.Add(new PSKWedge
                    {
                        MatIndex = matIndices[i],
                        PointIndex = (ushort)i,
                        U = vertex.UV.X,
                        V = vertex.UV.Y
                    });
                    // include some slightly nonstandard stuff here
                    if (includeVertexNormals)
                    {
                        var vertNorm = (Vector3)vertex.TangentZ;
                        vertNorm = vertNorm with { Y = -vertNorm.Y };
                        vertNorm = Vector3.Normalize(vertNorm);
                        psk.VertexNormals.Add(vertNorm);
                    }
                    for (int j = 0; j < 4; j++)
                    {
                        if (vertex.InfluenceWeights[j] == 0)
                        {
                            break;
                        }

                        // first, we need to find the chunk containing this vertex:
                        var chunk = lod.Chunks.Last(x => x.BaseVertexIndex <= i);

                        psk.Weights.Add(new PSKWeight
                        {
                            Bone = chunk.BoneMap[vertex.InfluenceBones[j]],
                            Weight = vertex.InfluenceWeights[j] * weightUnpackScale,
                            Point = i
                        });
                    }
                }
            }
            foreach (MeshBone meshBone in skelMesh.RefSkeleton)
            {
                psk.Bones.Add(new PSA.PSABone
                {
                    Name = meshBone.Name.Instanced,
                    Flags = meshBone.Flags,
                    ParentIndex = meshBone.ParentIndex,
                    NumChildren = meshBone.NumChildren,
                    Position = new Vector3(meshBone.Position.X, meshBone.Position.Y * -1, meshBone.Position.Z),
                    Rotation = new Quaternion(meshBone.Orientation.X, meshBone.Orientation.Y * -1, meshBone.Orientation.Z, meshBone.Orientation.W)
                });
            }

            return psk;
        }

        public class PSKWedge
        {
            public ushort PointIndex;
            public float U;
            public float V;
            public byte MatIndex;
        }

        public class PSKTriangle
        {
            public ushort WedgeIdx0;
            public ushort WedgeIdx1;
            public ushort WedgeIdx2;
            public byte MatIndex;
        }

        public struct PSKMaterial
        {
            public string Name;
            public int Texture;
            public int polyflags;
            public int auxmaterial;
            public int auxflags;
            public int LODbias;
            public int LODstyle;
        }
        public class PSKWeight
        {
            public float Weight;
            public int Point;
            public int Bone;
        }

        public class MorphInfo
        {
            public string Name;
            public int VertexCount;
        }

        public class MorphDelta
        {
            public Vector3 PositionDelta;
            public Vector3 TangentZDelta;
            public int PointIndex;
        }
    }
}

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class SerializingContainer
    {
        public void Serialize(ref PSK.PSKWedge wedge)
        {
            if (IsLoading)
            {
                wedge = new PSK.PSKWedge();
            }
            Serialize(ref wedge.PointIndex);
            SerializeConstShort(0); //Padding
            Serialize(ref wedge.U);
            Serialize(ref wedge.V);
            Serialize(ref wedge.MatIndex);
            SerializeConstByte(0); //Reserved
            SerializeConstShort(0); //Padding
        }
        public void Serialize(ref PSK.PSKTriangle tri)
        {
            if (IsLoading)
            {
                tri = new PSK.PSKTriangle();
            }
            Serialize(ref tri.WedgeIdx0);
            Serialize(ref tri.WedgeIdx1);
            Serialize(ref tri.WedgeIdx2);
            Serialize(ref tri.MatIndex);
            SerializeConstByte(0);
            SerializeConstInt(0);
        }
        public void Serialize(ref PSK.PSKMaterial mat)
        {
            if (IsLoading)
            {
                mat = new PSK.PSKMaterial();
            }
            SerializeFixedSizeString(ref mat.Name, 64);
            Serialize(ref mat.Texture);
            Serialize(ref mat.polyflags);
            Serialize(ref mat.auxmaterial);
            Serialize(ref mat.auxflags);
            Serialize(ref mat.LODbias);
            Serialize(ref mat.LODstyle);
        }
        public void Serialize(ref PSK.PSKWeight w)
        {
            if (IsLoading)
            {
                w = new PSK.PSKWeight();
            }
            Serialize(ref w.Weight);
            Serialize(ref w.Point);
            Serialize(ref w.Bone);
        }

        public void Serialize(ref PSK.MorphInfo m)
        {
            if (IsLoading)
            {
                m = new PSK.MorphInfo();
            }
            SerializeFixedSizeString(ref m.Name, 64);
            Serialize(ref m.VertexCount);
        }

        public void Serialize(ref PSK.MorphDelta m)
        {
            if (IsLoading)
            {
                m = new PSK.MorphDelta();
            }
            Serialize(ref m.PositionDelta);
            Serialize(ref m.TangentZDelta);
            Serialize(ref m.PointIndex);
        }
    }
}
