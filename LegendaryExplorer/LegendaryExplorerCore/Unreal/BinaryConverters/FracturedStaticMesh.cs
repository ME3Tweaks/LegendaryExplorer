using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class FracturedStaticMesh : StaticMesh
    {
        public UIndex SourceStaticMesh;
        public FragmentInfo[] Fragments;
        public int CoreFragmentIndex;
        public int InteriorElementIndex;// ME3/UDK
        public Vector3 CoreMeshScale3D;// ME3/UDK
        public Vector3 CoreMeshOffset;// ME3/UDK
        public Rotator CoreMeshRotation;// ME3/UDK
        public Vector3 PlaneBias;// ME3/UDK
        public ushort NonCriticalBuildVersion;// ME3/UDK
        public ushort LicenseeNonCriticalBuildVersion;// ME3/UDK

        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref SourceStaticMesh);
            sc.Serialize(ref Fragments, SCExt.Serialize);
            sc.Serialize(ref CoreFragmentIndex);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref InteriorElementIndex);
                sc.Serialize(ref CoreMeshScale3D);
                sc.Serialize(ref CoreMeshOffset);
                sc.Serialize(ref CoreMeshRotation);
                sc.Serialize(ref PlaneBias);
                sc.Serialize(ref NonCriticalBuildVersion);
                sc.Serialize(ref LicenseeNonCriticalBuildVersion);
            }
            else if (sc.IsLoading)
            {
                InteriorElementIndex = -1;
                CoreMeshScale3D = new Vector3(1,1,1);
                PlaneBias = new Vector3(1,1,1);
                NonCriticalBuildVersion = 1;
                LicenseeNonCriticalBuildVersion = 1;
            }
        }

        public new static FracturedStaticMesh Create()
        {
            return new()
            {
                Bounds = new BoxSphereBounds(),
                BodySetup = 0,
                kDOPTreeME3UDKLE = KDOPTreeBuilder.ToCompact(Array.Empty<kDOPCollisionTriangle>(), Array.Empty<Vector3>()),
                LODModels = Array.Empty<StaticMeshRenderData>(),
                HighResSourceMeshName = "",
                unkFloats = Array.Empty<float>(),
                SourceStaticMesh = 0,
                Fragments = Array.Empty<FragmentInfo>(),
                InteriorElementIndex = -1,
                CoreMeshScale3D = new Vector3(1, 1, 1),
                PlaneBias = new Vector3(1, 1, 1),
                NonCriticalBuildVersion = 1,
                LicenseeNonCriticalBuildVersion = 1,
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref SourceStaticMesh, nameof(SourceStaticMesh));
        }
    }

    public class FragmentInfo
    {
        public Vector3 Center;
        public ConvexHull ConvexHull;
        public BoxSphereBounds Bounds;
        public byte[] Neighbours; // ME3/UDK
        public bool bCanBeDestroyed;// ME3/UDK
        public bool bRootFragment; // ME3/UDK
        public bool bNeverSpawnPhysicsChunk;// ME3/UDK
        public Vector3 AverageExteriorNormal; // ME3/UDK
        public float[] NeighbourDims;// ME3/UDK
    }

    public class ConvexHull
    {
        public Vector3[] VertexData;
        public Plane[] PermutedVertexData;
        public int[] FaceTriData;
        public Vector3[] EdgeDirections;
        public Vector3[] FaceNormalDirections;
        public Plane[] FacePlaneData;
        public Box ElemBox;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref ConvexHull hull)
        {
            if (sc.IsLoading)
            {
                hull = new ConvexHull();
            }
            sc.Serialize(ref hull.VertexData);
            sc.Serialize(ref hull.PermutedVertexData, Serialize);
            sc.Serialize(ref hull.FaceTriData, SCExt.Serialize);
            sc.Serialize(ref hull.EdgeDirections);
            sc.Serialize(ref hull.FaceNormalDirections);
            sc.Serialize(ref hull.FacePlaneData, Serialize);
            sc.Serialize(ref hull.ElemBox);
        }
        public static void Serialize(this SerializingContainer2 sc, ref FragmentInfo info)
        {
            if (sc.IsLoading)
            {
                info = new FragmentInfo();
            }
            sc.Serialize(ref info.Center);
            sc.Serialize(ref info.ConvexHull);
            sc.Serialize(ref info.Bounds);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref info.Neighbours);
                sc.Serialize(ref info.bCanBeDestroyed);
                sc.Serialize(ref info.bRootFragment);
                sc.Serialize(ref info.bNeverSpawnPhysicsChunk);
                sc.Serialize(ref info.AverageExteriorNormal);
                sc.Serialize(ref info.NeighbourDims, SCExt.Serialize);
            }
            else if (sc.IsLoading)
            {
                info.Neighbours = new byte[info.ConvexHull.FacePlaneData.Length];
                info.bCanBeDestroyed = true;
                info.NeighbourDims = new float[info.Neighbours.Length];
                for (int i = 0; i < info.NeighbourDims.Length; i++)
                {
                    info.NeighbourDims[i] = 1;
                }
            }
        }
    }
}