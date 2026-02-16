using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LegendaryExplorerCore.Unreal.PhysX;

internal sealed unsafe partial class PhysXSDK : IDisposable
{
    //scaling factors between PhysX units and Unreal units, taken from LE3 decomp
    public const float PhysXtoUnrealScale = 100.0f;
    public const float UnrealtoPhysXScale = 0.01f;
    private const float AngleCompareThreshold = 0.0003f;

    private bool isDisposed;
    private NxPhysicsSDK* SDK;
    private PhysXCooker Cooker;

    public PhysXSDK()
    {
        if (!PhysXDllLoader.EnsureCoreDll())
        {
            isDisposed = true;
            throw new Exception($"Unable to load {PhysXDllLoader.PHYSXCORE64_DLL}");
        }
        if (!PhysXDllLoader.EnsureLoaderDll())
        {
            isDisposed = true;
            throw new Exception($"Unable to load {PhysXDllLoader.PHYSXLOADER64_DLL}");
        }

        var desc = new NxPhysicsSDKDesc();
        NxPhysicsSDK* sdk;
        fixed (byte* pGuid = GUID)
        {
            sdk = NxCreatePhysicsSDK(SDKVERSION, null, null, &desc, null, pGuid);
        }
        if (sdk is null)
        {
            isDisposed = true;
            throw new Exception("Failed to create PhysX SDK instance");
        }
        SDK = sdk;

        Cooker = new PhysXCooker();

        //just duplicating LE3 settings here, probably unnecessary for generating meshes
        SDK->SetParameter(NxParameter.NX_SKIN_WIDTH, 0.025f);
        SDK->SetParameter(NxParameter.NX_ASYNCHRONOUS_MESH_CREATION, 1.0f);
        SDK->SetParameter(NxParameter.NX_ADAPTIVE_FORCE, 0);
        SDK->SetParameter(NxParameter.NX_IMPROVED_SPRING_SOLVER, 0);
        SDK->SetParameter(NxParameter.NX_FAST_MASSIVE_BP_VOLUME_DELETION, 1);
    }

    public static KConvexElem GenerateHullData(ReadOnlySpan<Vector3> convexHull)
    {
        using var sdk = new PhysXSDK();
        var physXScaleHullVerts = new Vector3[convexHull.Length];
        for (int i = 0; i < convexHull.Length; i++)
        {
            physXScaleHullVerts[i] = convexHull[i] * UnrealtoPhysXScale;
        }
        using NxStream stream = sdk.Cooker.GenerateCachedPhysicsDataStream(physXScaleHullVerts);
        NxConvexMesh* mesh = sdk.SDK->CreateConvexMesh(&stream);
        if (mesh is null)
        {
            throw new Exception("Failed to create convex mesh from cooked data");
        }
        try
        {
            var convexElem = new KConvexElem();
            //sanity checks
            if (mesh->GetStride(0, NxInternalArray.NX_ARRAY_VERTICES) != sizeof(Vector3))
            {
                throw new Exception("Unexpected vertex format in convex mesh");
            }
            if (mesh->GetFormat(0, NxInternalArray.NX_ARRAY_TRIANGLES) != NxInternalFormat.NX_FORMAT_INT
                || mesh->GetStride(0, NxInternalArray.NX_ARRAY_TRIANGLES) != sizeof(int) * 3)
            {
                throw new Exception("Unexpected triangle index format in convex mesh");
            }

            int vertexCount = (int)mesh->GetCount(0, NxInternalArray.NX_ARRAY_VERTICES);
            var verts = new ReadOnlySpan<Vector3>(mesh->GetBase(0, NxInternalArray.NX_ARRAY_VERTICES), vertexCount);
            convexElem.VertexData.Capacity = verts.Length;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 rescaledVert = verts[i] * PhysXtoUnrealScale;
                convexElem.VertexData.Add(rescaledVert);
                convexElem.ElemBox.Add(rescaledVert);
            }
            int triangleCount = (int)mesh->GetCount(0, NxInternalArray.NX_ARRAY_TRIANGLES);
            var indices = new ReadOnlySpan<int>(mesh->GetBase(0, NxInternalArray.NX_ARRAY_TRIANGLES), triangleCount * 3);
            convexElem.FaceTriData.Capacity = indices.Length;

            var edges = new List<int>();
            var triNormals = new List<Vector3>();
            for (int i = 0; i < triangleCount; i++)
            {
                //reordering of indices is intentional to convert from PhysX winding order to Unreal winding order
                int i0 = indices[i * 3];
                int i1 = indices[i * 3 + 2];
                int i2 = indices[i * 3 + 1];

                Vector3 v0 = convexElem.VertexData[i0];
                Vector3 v1 = convexElem.VertexData[i1];
                Vector3 v2 = convexElem.VertexData[i2];

                convexElem.FaceTriData.Add(i0);
                convexElem.FaceTriData.Add(i1);
                convexElem.FaceTriData.Add(i2);

                AddEdgeIfNotPresent(edges, i0, i1);
                AddEdgeIfNotPresent(edges, i1, i2);
                AddEdgeIfNotPresent(edges, i2, i0);

                Vector3 normal = Vector3.Normalize(Vector3.Cross(v2 - v0, v1 - v0));
                triNormals.Add(normal);
                AddDirIfNotPresent(convexElem.FaceNormalDirections, normal);

                AddPlaneIfNotPresent(convexElem.FacePlaneData, new Plane(normal, Vector3.Dot(normal, v0)));
            }

            for (int i = 0; i < edges.Count / 2; i++)
            {
                int edge0 = edges[i * 2];
                int edge1 = edges[i * 2 + 1];
                
                (int tri0Index, int tri1Index) = getTriIndicesUsingEdge(edge0, edge1, convexElem.FaceTriData);
                if (tri0Index == -1 || tri1Index == -1)
                {
                    throw new Exception("Open Mesh");
                }
                Vector3 tri0Normal = triNormals[tri0Index];
                Vector3 tri1Normal = triNormals[tri1Index];
                float faceNormalDot = Vector3.Dot(tri0Normal, tri1Normal);

                if (MathF.Abs(faceNormalDot - 1.0f) < AngleCompareThreshold)
                {
                    Vector3 edgeDir = Vector3.Normalize(convexElem.VertexData[edge1] - convexElem.VertexData[edge0]);
                    AddDirIfNotPresent(convexElem.EdgeDirections, edgeDir);
                }
            }

            //make simd ready copy of vertex data in PermutedVertexData
            int numRemainingVerts = convexElem.VertexData.Count % 4;
            int numToAdd = (convexElem.VertexData.Count / 4) * 3;
            int numToProcess = convexElem.VertexData.Count - numRemainingVerts;
            convexElem.PermutedVertexData.Capacity = numToAdd + (numRemainingVerts > 0 ? 3 : 0);

            for (int i = 0; i < numToProcess; i += 4)
            {
                //Xs
                convexElem.PermutedVertexData.Add(new (convexElem.VertexData[i].X, convexElem.VertexData[i + 1].X, convexElem.VertexData[i + 2].X, convexElem.VertexData[i + 3].X));
                //Ys
                convexElem.PermutedVertexData.Add(new (convexElem.VertexData[i].Y, convexElem.VertexData[i + 1].Y, convexElem.VertexData[i + 2].Y, convexElem.VertexData[i + 3].Y));
                //Zs
                convexElem.PermutedVertexData.Add(new (convexElem.VertexData[i].Z, convexElem.VertexData[i + 1].Z, convexElem.VertexData[i + 2].Z, convexElem.VertexData[i + 3].Z));
            }
            if (numRemainingVerts > 0)
            {
                Vector3 last1, last2, last3, last4;

                last1 = convexElem.VertexData[numToProcess];
                last4 = last1;
                switch (numRemainingVerts)
                {
                    case 3:
                        last2 = convexElem.VertexData[numToProcess + 1];
                        last3 = convexElem.VertexData[numToProcess + 2];
                        break;
                    case 2:
                        last2 = convexElem.VertexData[numToProcess + 1];
                        last3 = last1;
                        break;
                    case 1:
                        last3 = last2 = last1;
                        break;
                    default:
                        throw new Exception("Unreachable");
                }
                convexElem.PermutedVertexData.Add(new (last1.X, last2.X, last3.X, last4.X));
                convexElem.PermutedVertexData.Add(new (last1.Y, last2.Y, last3.Y, last4.Y));
                convexElem.PermutedVertexData.Add(new (last1.Z, last2.Z, last3.Z, last4.Z));
            }

            return convexElem;
        }
        finally
        {
            sdk.SDK->ReleaseConvexMesh(mesh);
        }

        static void AddDirIfNotPresent(List<Vector3> dirs, Vector3 newDir)
        {
            for (int i = 0; i < dirs.Count; i++)
            {
                Vector3 testDir = dirs[i];
                float dirDot = MathF.Abs(Vector3.Dot(testDir, newDir));
                if (MathF.Abs(dirDot - 1.0f) < 0.0003f)
                {
                    return;
                }
            }
            dirs.Add(newDir);
        }

        static void AddEdgeIfNotPresent(List<int> edges, int edge0, int edge1)
        {
            int numEdges = edges.Count / 2;
            for (int i = 0; i < numEdges; i++)
            {
                int testEdge0 = edges[i * 2];
                int testEdge1 = edges[i * 2 + 1];
                if ((testEdge0 == edge0 && testEdge1 == edge1) ||
                    (testEdge1 == edge0 && testEdge0 == edge1))
                {
                    return;
                }
            }
            edges.Add(edge0);
            edges.Add(edge1);
        }

        static void AddPlaneIfNotPresent(List<Plane> planes, Plane newPlane)
        {
            for (int i = 0; i < planes.Count; i++)
            {
                Plane testPlane = planes[i];
                float normalDot = Vector3.Dot(testPlane.Normal, newPlane.Normal);
                if (MathF.Abs(normalDot - 1.0f) < 0.0003f &&
                    MathF.Abs(testPlane.D - newPlane.D) < 0.1f)
                {
                    return;
                }
            }
            planes.Add(newPlane);
        }

        static (int, int) getTriIndicesUsingEdge(int edge0, int edge1, List<int> triData)
        {
            int tri0Index = -1;
            int tri1Index = -1;
            int numTris = triData.Count / 3;
            for (int i = 0; i < numTris; i++)
            {
                if (triHasEdge(triData[i * 3], triData[i * 3 + 1], triData[i * 3 + 2], edge0, edge1))
                {
                    if (tri0Index == -1)
                    {
                        tri0Index = i;
                    }
                    else if (tri1Index == -1)
                    {
                        tri1Index = i;
                    }
                    else
                    {
                        throw new Exception("3 tris share an edge.");
                    }
                }
            }
            return (tri0Index, tri1Index);

            static bool triHasEdge(int t0, int t1, int t2, int edge0, int edge1)
            {
                return (t0 == edge0 && t1 == edge1) || (t1 == edge0 && t0 == edge1) ||
                    (t0 == edge0 && t2 == edge1) || (t2 == edge0 && t0 == edge1) ||
                    (t1 == edge0 && t2 == edge1) || (t2 == edge0 && t1 == edge1);
            }
        }
    }

    private void InternalDispose()
    {
        if (!isDisposed)
        {
            Cooker?.Dispose();
            Cooker = null;
            NxReleasePhysicsSDK(SDK);
            SDK = null;
            isDisposed = true;
        }
    }

    public void Dispose()
    {
        InternalDispose();
        GC.SuppressFinalize(this);
    }

    ~PhysXSDK()
    {
        InternalDispose();
    }


    [LibraryImport(PhysXDllLoader.PHYSXLOADER64_DLL)]
    private static partial NxPhysicsSDK* NxCreatePhysicsSDK(uint sdkVersion, NxAllocator* allocator, void* outputStream, NxPhysicsSDKDesc* desc, uint* unk, byte* guid);

    [LibraryImport(PhysXDllLoader.PHYSXLOADER64_DLL)]
    private static partial void NxReleasePhysicsSDK(NxPhysicsSDK* sdk);

    //from LE3 decomp
    private uint SDKVERSION = 0x2080400;
    //not sure what this is, but LE3 passes it to CreatePhysicsSDK
    private ReadOnlySpan<byte> GUID => "BE9042F0-ADC4-4b12-A93E-DB8A731FDBD5"u8;

    [StructLayout(LayoutKind.Sequential)]
    private struct NxPhysicsSDKDesc
    {
        public uint hwPageSize;
        public uint hwPageMax;
        public uint hwConvexMax;
        public uint cookerThreadMask;
        public uint flags;
        public uint gpuHeapSize;
        public uint meshCacheSize;

        //values taken from LE3 decomp
        public NxPhysicsSDKDesc()
        {
            hwPageSize = 65536;
            hwConvexMax = 2048;
            hwPageMax = 256;
            flags = 3;
            cookerThreadMask = 0;
            gpuHeapSize = 32;
            meshCacheSize = 8;
        }
    }

    enum NxParameter : int
    {
        NX_PENALTY_FORCE = 0,
        NX_SKIN_WIDTH = 1,
        NX_DEFAULT_SLEEP_LIN_VEL_SQUARED = 2,
        NX_DEFAULT_SLEEP_ANG_VEL_SQUARED = 3,
        NX_BOUNCE_THRESHOLD = 4,
        NX_DYN_FRICT_SCALING = 5,
        NX_STA_FRICT_SCALING = 6,
        NX_MAX_ANGULAR_VELOCITY = 7,
        NX_CONTINUOUS_CD = 8,
        NX_VISUALIZATION_SCALE = 9,
        NX_VISUALIZE_WORLD_AXES = 10,
        NX_VISUALIZE_BODY_AXES = 11,
        NX_VISUALIZE_BODY_MASS_AXES = 12,
        NX_VISUALIZE_BODY_LIN_VELOCITY = 13,
        NX_VISUALIZE_BODY_ANG_VELOCITY = 14,
        NX_VISUALIZE_BODY_JOINT_GROUPS = 22,
        NX_VISUALIZE_JOINT_LOCAL_AXES = 27,
        NX_VISUALIZE_JOINT_WORLD_AXES = 28,
        NX_VISUALIZE_JOINT_LIMITS = 29,
        NX_VISUALIZE_CONTACT_POINT = 33,
        NX_VISUALIZE_CONTACT_NORMAL = 34,
        NX_VISUALIZE_CONTACT_ERROR = 35,
        NX_VISUALIZE_CONTACT_FORCE = 36,
        NX_VISUALIZE_ACTOR_AXES = 37,
        NX_VISUALIZE_COLLISION_AABBS = 38,
        NX_VISUALIZE_COLLISION_SHAPES = 39,
        NX_VISUALIZE_COLLISION_AXES = 40,
        NX_VISUALIZE_COLLISION_COMPOUNDS = 41,
        NX_VISUALIZE_COLLISION_VNORMALS = 42,
        NX_VISUALIZE_COLLISION_FNORMALS = 43,
        NX_VISUALIZE_COLLISION_EDGES = 44,
        NX_VISUALIZE_COLLISION_SPHERES = 45,
        NX_VISUALIZE_COLLISION_STATIC = 47,
        NX_VISUALIZE_COLLISION_DYNAMIC = 48,
        NX_VISUALIZE_COLLISION_FREE = 49,
        NX_VISUALIZE_COLLISION_CCD = 50,
        NX_VISUALIZE_COLLISION_SKELETONS = 51,
        NX_VISUALIZE_FLUID_EMITTERS = 52,
        NX_VISUALIZE_FLUID_POSITION = 53,
        NX_VISUALIZE_FLUID_VELOCITY = 54,
        NX_VISUALIZE_FLUID_KERNEL_RADIUS = 55,
        NX_VISUALIZE_FLUID_BOUNDS = 56,
        NX_VISUALIZE_FLUID_PACKETS = 57,
        NX_VISUALIZE_FLUID_MOTION_LIMIT = 58,
        NX_VISUALIZE_FLUID_DYN_COLLISION = 59,
        NX_VISUALIZE_FLUID_STC_COLLISION = 60,
        NX_VISUALIZE_FLUID_MESH_PACKETS = 61,
        NX_VISUALIZE_FLUID_DRAINS = 62,
        NX_VISUALIZE_FLUID_PACKET_DATA = 90,
        NX_VISUALIZE_CLOTH_MESH = 63,
        NX_VISUALIZE_CLOTH_COLLISIONS = 64,
        NX_VISUALIZE_CLOTH_SELFCOLLISIONS = 65,
        NX_VISUALIZE_CLOTH_WORKPACKETS = 66,
        NX_VISUALIZE_CLOTH_SLEEP = 67,
        NX_VISUALIZE_CLOTH_SLEEP_VERTEX = 94,
        NX_VISUALIZE_CLOTH_TEARABLE_VERTICES = 80,
        NX_VISUALIZE_CLOTH_TEARING = 81,
        NX_VISUALIZE_CLOTH_ATTACHMENT = 82,
        NX_VISUALIZE_CLOTH_VALIDBOUNDS = 92,
        NX_VISUALIZE_SOFTBODY_MESH = 83,
        NX_VISUALIZE_SOFTBODY_COLLISIONS = 84,
        NX_VISUALIZE_SOFTBODY_WORKPACKETS = 85,
        NX_VISUALIZE_SOFTBODY_SLEEP = 86,
        NX_VISUALIZE_SOFTBODY_SLEEP_VERTEX = 95,
        NX_VISUALIZE_SOFTBODY_TEARABLE_VERTICES = 87,
        NX_VISUALIZE_SOFTBODY_TEARING = 88,
        NX_VISUALIZE_SOFTBODY_ATTACHMENT = 89,
        NX_VISUALIZE_SOFTBODY_VALIDBOUNDS = 93,
        NX_ADAPTIVE_FORCE = 68,
        NX_COLL_VETO_JOINTED = 69,
        NX_TRIGGER_TRIGGER_CALLBACK = 70,
        NX_SELECT_HW_ALGO = 71,
        NX_VISUALIZE_ACTIVE_VERTICES = 72,
        NX_CCD_EPSILON = 73,
        NX_SOLVER_CONVERGENCE_THRESHOLD = 74,
        NX_BBOX_NOISE_LEVEL = 75,
        NX_IMPLICIT_SWEEP_CACHE_SIZE = 76,
        NX_DEFAULT_SLEEP_ENERGY = 77,
        NX_CONSTANT_FLUID_MAX_PACKETS = 78,
        NX_CONSTANT_FLUID_MAX_PARTICLES_PER_STEP = 79,
        NX_VISUALIZE_FORCE_FIELDS = 91,
        NX_ASYNCHRONOUS_MESH_CREATION = 96,
        NX_FORCE_FIELD_CUSTOM_KERNEL_EPSILON = 97,
        NX_IMPROVED_SPRING_SOLVER = 98,
        NX_FAST_MASSIVE_BP_VOLUME_DELETION = 99,
        NX_LEGACY_JOINT_DRIVE = 100,
        NX_VISUALIZE_CLOTH_HIERARCHY = 101,
        NX_VISUALIZE_CLOTH_HARD_STRETCHING_LIMITATION = 102,
        NX_PARAMS_NUM_VALUES = 103,
        NX_PARAMS_FORCE_DWORD = 0x7fffffff
    }

    //must be acquired from NxCreatePhysicsSDK
    [StructLayout(LayoutKind.Sequential)]
    private struct NxPhysicsSDK
    {
        private NxPhysicsSDKVTable* vtable;
        private struct NxPhysicsSDKVTable
        {
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, void> Destructor;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, void> release;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, int, float, void> setParameter;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, int, float> getParameter;
            public void* createScene;
            public void* releaseScene;
            public void* getNbScenes;
            public void* getScene;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, NxStream*, void*> createTriangleMesh;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, void*, void> releaseTriangleMesh;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, uint> getNbTriangleMeshes;
            public void* createHeightField;
            public void* releaseHeightField;
            public void* getNbHeightFields;
            public void* createCCDSkeleton;
            public void* createCCDSkeletonBuff;
            public void* releaseCCDSkeleton;
            public void* getNbCCDSkeletons;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, NxStream*, NxConvexMesh*> createConvexMesh;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, NxConvexMesh*, void> releaseConvexMesh;
            public delegate* unmanaged[MemberFunction]<NxPhysicsSDK*, uint> getNbConvexMeshes;
            //followed by more functions, but we don't need them
        }

        public void Release()
        {
            fixed (NxPhysicsSDK* pThis = &this)
            {
                vtable->release(pThis);
            }
        }

        public void SetParameter(NxParameter param, float value)
        {
            fixed (NxPhysicsSDK* pThis = &this)
            {
                vtable->setParameter(pThis, (int)param, value);
            }
        }

        public float GetParameter(NxParameter param)
        {
            fixed (NxPhysicsSDK* pThis = &this)
            {
                return vtable->getParameter(pThis, (int)param);
            }
        }

        public NxConvexMesh* CreateConvexMesh(NxStream* stream)
        {
            fixed (NxPhysicsSDK* pThis = &this)
            {
                return vtable->createConvexMesh(pThis, stream);
            }
        }

        public void ReleaseConvexMesh(NxConvexMesh* mesh)
        {
            fixed (NxPhysicsSDK* pThis = &this)
            {
                vtable->releaseConvexMesh(pThis, mesh);
            }
        }

        public uint GetNbConvexMeshes()
        {
            fixed (NxPhysicsSDK* pThis = &this)
            {
                return vtable->getNbConvexMeshes(pThis);
            }
        }
    }

    enum NxInternalArray : int
    {
        NX_ARRAY_TRIANGLES,
        NX_ARRAY_VERTICES,
        NX_ARRAY_NORMALS,
        NX_ARRAY_HULL_VERTICES,
        NX_ARRAY_HULL_POLYGONS,
        NX_ARRAY_TRIANGLES_REMAP
    }

    enum NxInternalFormat : int
    {
        NX_FORMAT_NODATA,
        NX_FORMAT_FLOAT,
        NX_FORMAT_BYTE,
        NX_FORMAT_SHORT,
        NX_FORMAT_INT
    }

    //must be acquired from NxPhysicsSDK::CreateConvexMesh, and released with NxPhysicsSDK::ReleaseConvexMesh
    [StructLayout(LayoutKind.Sequential)]
    private struct NxConvexMesh
    {
        private NxConvexMeshVTable* vtable;
        private struct NxConvexMeshVTable
        {
            public void* saveToDesc;
            public void* getSubmeshCount;
            public delegate* unmanaged[MemberFunction]<NxConvexMesh*, uint, int, uint> getCount;
            public delegate* unmanaged[MemberFunction]<NxConvexMesh*, uint, int, int> getFormat;
            public delegate* unmanaged[MemberFunction]<NxConvexMesh*, uint, int, void*> getBase;
            public delegate* unmanaged[MemberFunction]<NxConvexMesh*, uint, int, uint> getStride;
            public void* load;
            public void* getReferenceCount;
            public void* getMassInformation;
            public void* getInternal;
            public void* Destructor;
        }

        public uint GetCount(uint subMeshIndex, NxInternalArray array)
        {
            fixed (NxConvexMesh* pThis = &this)
            {
                return vtable->getCount(pThis, subMeshIndex, (int)array);
            }
        }

        public NxInternalFormat GetFormat(uint subMeshIndex, NxInternalArray array)
        {
            fixed (NxConvexMesh* pThis = &this)
            {
                return (NxInternalFormat)vtable->getFormat(pThis, subMeshIndex, (int)array);
            }
        }

        public void* GetBase(uint subMeshIndex, NxInternalArray array)
        {
            fixed (NxConvexMesh* pThis = &this)
            {
                return vtable->getBase(pThis, subMeshIndex, (int)array);
            }
        }

        public uint GetStride(uint subMeshIndex, NxInternalArray array)
        {
            fixed (NxConvexMesh* pThis = &this)
            {
                return vtable->getStride(pThis, subMeshIndex, (int)array);
            }
        }
    }
}