using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace LegendaryExplorerCore.Unreal.PhysX
{
    internal sealed unsafe partial class PhysXCooker : IDisposable
    {
        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial byte NxCookConvexMesh(ConvexMeshDesc* desc, NxStream* stream);

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial byte NxCookTriangleMesh(TriangleMeshDesc* desc, NxStream* stream);

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial byte NxInitCooking(NxAllocator* allocator, void* outputstream);

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial byte NxSetCookingParams(NxCookingParams* parms);

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial NxCookingParams* NxGetCookingParams();

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial void NxCloseCooking();

        /// <summary>
        /// Held while a cooker exists. Cooking is global to the process: NxInitCooking and NxCloseCooking start and end it for everyone, so a cooker
        /// on one thread can't overlap another's
        /// </summary>
        private static readonly Lock Sync = new();

        public static T Use<T>(Func<PhysXCooker, T> cook)
        {
            lock (Sync)
            {
                using var cooker = new PhysXCooker();
                return cook(cooker);
            }
        }

        private bool isDisposed;
        private PhysXCooker()
        {
            if (!PhysXDllLoader.EnsureCookingDll())
            {
                isDisposed = true;
                throw new Exception($"Unable to load {PhysXDllLoader.PHYSXCOOKING64_DLL}");
            }
            if (NxInitCooking(null, null) is 0)
            {
                isDisposed = true;
                throw new Exception("PhysX cooking couldn't be started");
            }
            //as UE3 does (PhysSkinWidth)
            NxCookingParams parms = *NxGetCookingParams();
            parms.skinWidth = 0.025f;
            NxSetCookingParams(&parms);
        }

        /// <returns>The convex mesh of <paramref name="verts"/> cooked for collision, or null if PhysX can't cook it (as for a flat hull)</returns>
        public byte[] TryGenerateCachedPhysicsData(ReadOnlySpan<Vector3> verts)
        {
            if (!TryCook(verts, hullOnly: false, out NxStream stream)) return null;
            using (stream)
            {
                byte[] cachedData = new byte[stream.Count];
                new Span<byte>(stream.Data, (int)stream.Count).CopyTo(cachedData);
                return cachedData;
            }
        }

        /// <param name="hullOnly">Only compute the convex hull of <paramref name="verts"/>, as FKConvexElem::GenerateHullData does, rather than cook it for
        /// collision as MakeCachedConvexDataForAggGeom does, which inflates it</param>
        public NxStream GenerateCachedPhysicsDataStream(ReadOnlySpan<Vector3> verts, bool hullOnly = false) =>
            TryCook(verts, hullOnly, out NxStream stream) ? stream : throw new Exception("Convex Mesh cooking failed!");

        private bool TryCook(ReadOnlySpan<Vector3> verts, bool hullOnly, out NxStream cooked)
        {
            if (isDisposed)
            {
                throw new Exception($"Cannot use a disposed {nameof(PhysXCooker)}");
            }
            fixed (Vector3* data = verts)
            {
                var meshDesc = new ConvexMeshDesc
                {
                    NumVertices = (uint)verts.Length,
                    PointStrideBytes = 12u,
                    Points = data,
                    Flags = hullOnly ? ConvexFlags.ComputeConvex : ConvexFlags.ComputeConvex | ConvexFlags.InflateConvex | ConvexFlags.UseUncompressedNormals
                };

                var stream = new NxStream();

                if (NxCookConvexMesh(&meshDesc, &stream) is 0)
                {
                    stream.Dispose();
                    cooked = default;
                    return false;
                }
                cooked = stream;
                return true;
            }
        }

        /// <summary>
        /// The triangle mesh of <paramref name="points"/> (PhysX units) and <paramref name="indices"/> cooked for collision, as UE3's
        /// MakeCachedPerTriMeshDataForStaticMesh does (LE3: RVA 0x697640), or null if PhysX can't cook it
        /// </summary>
        /// <param name="mirrored">Whether the mesh is mirrored, which flips its winding</param>
        public byte[] TryCookTriangleMesh(ReadOnlySpan<Vector3> points, ReadOnlySpan<ushort> indices, bool mirrored)
        {
            if (isDisposed)
            {
                throw new Exception($"Cannot use a disposed {nameof(PhysXCooker)}");
            }
            fixed (Vector3* pointData = points)
            fixed (ushort* indexData = indices)
            {
                var desc = new TriangleMeshDesc
                {
                    NumVertices = (uint)points.Length,
                    NumTriangles = (uint)(indices.Length / 3),
                    PointStrideBytes = 12u,
                    TriangleStrideBytes = 6u,
                    Points = pointData,
                    Triangles = indexData,
                    // UE3 flips a mesh that isn't mirrored, and marks it for hardware when cooking for PC, as this cooker does
                    Flags = MeshFlags.SixteenBitIndices | MeshFlags.HardwareMesh | (mirrored ? 0 : MeshFlags.FlipNormals),
                    HeightFieldVerticalAxis = NotHeightField,
                    ConvexEdgeThreshold = 0.001f
                };
                var stream = new NxStream();
                try
                {
                    if (NxCookTriangleMesh(&desc, &stream) is 0) return null;
                    return new Span<byte>(stream.Data, (int)stream.Count).ToArray();
                }
                finally
                {
                    stream.Dispose();
                }
            }
        }

        // No finalizer: only Use makes one, and it disposes it inside the lock
        public void Dispose()
        {
            if (!isDisposed)
            {
                NxCloseCooking();
                isDisposed = true;
            }
        }

        [Flags]
        private enum ConvexFlags : uint
        {
            FlipNormals = 1,
            SixteenBitIndices = 1 << 1,
            ComputeConvex = 1 << 2,
            InflateConvex = 1 << 3,
            UseUncompressedNormals = 1 << 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConvexMeshDesc
        {
            public uint NumVertices;
            public uint NumTriangles;
            public uint PointStrideBytes;
            public uint TriangleStrideBytes;
            public void* Points;
            public void* Triangles;
            public ConvexFlags Flags;
        }

        [Flags]
        private enum MeshFlags : uint
        {
            FlipNormals = 1,
            SixteenBitIndices = 1 << 1,
            HardwareMesh = 1 << 2
        }

        // NX_NOT_HEIGHTFIELD
        private const uint NotHeightField = 0xFF;

        // NxTriangleMeshDesc: NxSimpleTriangleMesh's fields, padded to its 8-byte alignment, then its own
        [StructLayout(LayoutKind.Explicit, Size = 0x50)]
        private struct TriangleMeshDesc
        {
            [FieldOffset(0x00)] public uint NumVertices;
            [FieldOffset(0x04)] public uint NumTriangles;
            [FieldOffset(0x08)] public uint PointStrideBytes;
            [FieldOffset(0x0C)] public uint TriangleStrideBytes;
            [FieldOffset(0x10)] public void* Points;
            [FieldOffset(0x18)] public void* Triangles;
            [FieldOffset(0x20)] public MeshFlags Flags;
            [FieldOffset(0x28)] public uint MaterialIndexStride;
            [FieldOffset(0x30)] public void* MaterialIndices;
            [FieldOffset(0x38)] public uint HeightFieldVerticalAxis;
            [FieldOffset(0x3C)] public float HeightFieldVerticalExtent;
            [FieldOffset(0x40)] public void* PMap;
            [FieldOffset(0x48)] public float ConvexEdgeThreshold;
        }

        enum NxPlatform : int
        {
            PLATFORM_PC,
            PLATFORM_XENON,
            PLATFORM_PLAYSTATION3,
            PLATFORM_GAMECUBE,
            PLATFORM_WII = PLATFORM_GAMECUBE
        };

        [StructLayout(LayoutKind.Sequential)]
        struct NxCookingParams
        {
            public NxPlatform targetPlatform;
            public float skinWidth;
            public bool hintCollisionSpeed;
        };
    }
}
