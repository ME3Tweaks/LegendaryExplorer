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

        protected override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref SourceStaticMesh);
            sc.Serialize(ref Fragments, sc.Serialize);
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
                kDOPTreeME3UDKLE = KDOPTreeBuilder.ToCompact([], []),
                LODModels = [],
                HighResSourceMeshName = "",
                unkFloats = [],
                SourceStaticMesh = 0,
                Fragments = [],
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

    public partial class SerializingContainer
    {
        public void Serialize(ref ConvexHull hull)
        {
            if (IsLoading)
            {
                hull = new ConvexHull();
            }
            Serialize(ref hull.VertexData);
            Serialize(ref hull.PermutedVertexData, Serialize);
            Serialize(ref hull.FaceTriData, Serialize);
            Serialize(ref hull.EdgeDirections);
            Serialize(ref hull.FaceNormalDirections);
            Serialize(ref hull.FacePlaneData, Serialize);
            Serialize(ref hull.ElemBox);
        }
        public void Serialize(ref FragmentInfo info)
        {
            if (IsLoading)
            {
                info = new FragmentInfo();
            }
            Serialize(ref info.Center);
            Serialize(ref info.ConvexHull);
            Serialize(ref info.Bounds);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref info.Neighbours);
                Serialize(ref info.bCanBeDestroyed);
                Serialize(ref info.bRootFragment);
                Serialize(ref info.bNeverSpawnPhysicsChunk);
                Serialize(ref info.AverageExteriorNormal);
                Serialize(ref info.NeighbourDims, Serialize);
            }
            else if (IsLoading)
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