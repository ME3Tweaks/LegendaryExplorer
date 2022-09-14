﻿using System.Collections.Generic;
using System.IO;
using System.Numerics;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal
{
    //WIP. Not sure if it produces correct output
    public class PSK
    {
        public List<Vector3> Points;
        public List<PSKWedge> Wedges;
        public List<PSKTriangle> Faces;
        public List<PSKMaterial> Materials;
        public List<PSA.PSABone> Bones;
        public List<PSKWeight> Weights;

        private const int version = 1999801;

        protected void Serialize(SerializingContainer2 sc)
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
            sc.Serialize(ref Points, pointsHeader.DataCount, SCExt.Serialize);
            var wedgesHeader = new PSA.ChunkHeader
            {
                ChunkID = "VTXW0000",
                Version = version,
                DataSize = 0x10,
                DataCount = Wedges?.Count ?? 0
            };
            sc.Serialize(ref wedgesHeader);
            sc.Serialize(ref Wedges, wedgesHeader.DataCount, SCExt.Serialize);
            var facesHeader = new PSA.ChunkHeader
            {
                ChunkID = "FACE0000",
                Version = version,
                DataSize = 0xC,
                DataCount = Faces?.Count ?? 0
            };
            sc.Serialize(ref facesHeader);
            sc.Serialize(ref Faces, facesHeader.DataCount, SCExt.Serialize);
            var matsHeader = new PSA.ChunkHeader
            {
                ChunkID = "MATT0000",
                Version = version,
                DataSize = 0x58,
                DataCount = Materials?.Count ?? 0
            };
            sc.Serialize(ref matsHeader);
            sc.Serialize(ref Materials, matsHeader.DataCount, SCExt.Serialize);
            var bonesHeader = new PSA.ChunkHeader
            {
                ChunkID = "REFSKELT",
                Version = version,
                DataSize = 0x78,
                DataCount = Bones?.Count ?? 0
            };
            sc.Serialize(ref bonesHeader);
            sc.Serialize(ref Bones, bonesHeader.DataCount, SCExt.Serialize);
            var weightsHeader = new PSA.ChunkHeader
            {
                ChunkID = "RAWWEIGHTS",
                Version = version,
                DataSize = 0xC,
                DataCount = Weights?.Count ?? 0
            };
            sc.Serialize(ref weightsHeader);
            sc.Serialize(ref Weights, weightsHeader.DataCount, SCExt.Serialize);
        }

        public void ToFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            Serialize(new SerializingContainer2(fs, null));
        }

        public static PSK FromFile(string filePath)
        {
            var psk = new PSK();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            psk.Serialize(new SerializingContainer2(fs, null, true));
            return psk;
        }

        public static PSK CreateFromSkeletalMesh(SkeletalMesh skelMesh, int lodIdx = 0)
        {
            var lod = skelMesh.LODModels[0];

            int numVertices = (int)lod.NumVertices;
            var psk = new PSK
            {
                Points = new List<Vector3>(numVertices),
                Wedges = new List<PSKWedge>(),
                Faces = new List<PSKTriangle>(),
                Materials = new List<PSKMaterial>(),
                Bones = new List<PSA.PSABone>(),
                Weights = new List<PSKWeight>()
            };
            int numTriangles = 0;
            var matIndices = new byte[numVertices];
            foreach (SkelMeshSection section in lod.Sections)
            {
                numTriangles += section.NumTriangles;
                for (int i = 0; i < section.NumTriangles * 3; i++)
                {
                    uint baseIndex = section.BaseIndex;
                    byte materialIndex = (byte)section.MaterialIndex;
                    matIndices[lod.IndexBuffer[baseIndex + i]] = materialIndex;
                    psk.Faces.Add(new PSKTriangle
                    {
                        //intentionally flipped
                        WedgeIdx1 = (ushort)(baseIndex + i * 3 + 0),
                        WedgeIdx0 = (ushort)(baseIndex + i * 3 + 1),
                        WedgeIdx2 = (ushort)(baseIndex + i * 3 + 2),
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
                    psk.Points.Add(new Vector3(vertex.Position.X, vertex.Position.Y * -1, vertex.Position.Z * -1));
                    psk.Wedges.Add(new PSKWedge
                    {
                        MatIndex = matIndices[i],
                        PointIndex = (ushort)i,
                        U = vertex.UV.X,
                        V = vertex.UV.Y
                    });
                    for (int j = 0; j < 4; j++)
                    {
                        if (vertex.InfluenceBones[j] == 0)
                        {
                            break;
                        }

                        psk.Weights.Add(new PSKWeight
                        {
                            Bone = vertex.InfluenceBones[j],
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
                    psk.Points.Add(new Vector3(vertex.Position.X, vertex.Position.Y * -1, vertex.Position.Z * -1));
                    psk.Wedges.Add(new PSKWedge
                    {
                        MatIndex = matIndices[i],
                        PointIndex = (ushort)i,
                        U = vertex.UV.X,
                        V = vertex.UV.Y
                    });
                    for (int j = 0; j < 4; j++)
                    {
                        if (vertex.InfluenceBones[j] == 0)
                        {
                            break;
                        }

                        psk.Weights.Add(new PSKWeight
                        {
                            Bone = vertex.InfluenceBones[j],
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
                    Rotation = new Quaternion(meshBone.Orientation.X, meshBone.Orientation.Y * -1, meshBone.Orientation.Z, meshBone.Orientation.W * -1)
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
    }
}

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref PSK.PSKWedge wedge)
        {
            if (sc.IsLoading)
            {
                wedge = new PSK.PSKWedge();
            }
            sc.Serialize(ref wedge.PointIndex);
            sc.SerializeConstShort(0); //Padding
            sc.Serialize(ref wedge.U);
            sc.Serialize(ref wedge.V);
            sc.Serialize(ref wedge.MatIndex);
            sc.SerializeConstByte(0); //Reserved
            sc.SerializeConstShort(0); //Padding
        }
        public static void Serialize(this SerializingContainer2 sc, ref PSK.PSKTriangle tri)
        {
            if (sc.IsLoading)
            {
                tri = new PSK.PSKTriangle();
            }
            sc.Serialize(ref tri.WedgeIdx0);
            sc.Serialize(ref tri.WedgeIdx1);
            sc.Serialize(ref tri.WedgeIdx2);
            sc.Serialize(ref tri.MatIndex);
            sc.SerializeConstByte(0);
            sc.SerializeConstInt(0);
        }
        public static void Serialize(this SerializingContainer2 sc, ref PSK.PSKMaterial mat)
        {
            if (sc.IsLoading)
            {
                mat = new PSK.PSKMaterial();
            }
            sc.SerializeFixedSizeString(ref mat.Name, 64);
            sc.Serialize(ref mat.Texture);
            sc.Serialize(ref mat.polyflags);
            sc.Serialize(ref mat.auxmaterial);
            sc.Serialize(ref mat.auxflags);
            sc.Serialize(ref mat.LODbias);
            sc.Serialize(ref mat.LODstyle);
        }
        public static void Serialize(this SerializingContainer2 sc, ref PSK.PSKWeight w)
        {
            if (sc.IsLoading)
            {
                w = new PSK.PSKWeight();
            }
            sc.Serialize(ref w.Weight);
            sc.Serialize(ref w.Point);
            sc.Serialize(ref w.Bone);
        }
    }
}
