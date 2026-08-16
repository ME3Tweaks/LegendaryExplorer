using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Unreal.PhysX
{
    internal sealed unsafe partial class PhysXCooker : IDisposable
    {
        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial byte NxCookConvexMesh(ConvexMeshDesc* desc, NxStream* stream);

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial byte NxInitCooking(NxAllocator* allocator, void* outputstream);

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial byte NxSetCookingParams(NxCookingParams* parms);

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial NxCookingParams* NxGetCookingParams();

        [LibraryImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static partial void NxCloseCooking();

        private bool isDisposed;
        public PhysXCooker()
        {
            if (!PhysXDllLoader.EnsureCookingDll())
            {
                isDisposed = true;
                throw new Exception($"Unable to load {PhysXDllLoader.PHYSXCOOKING64_DLL}");
            }
            NxInitCooking(null, null);
            NxCookingParams* parms = NxGetCookingParams();
            NxCookingParams defaultParms = *parms;
            parms->skinWidth = 0.025f;
            NxSetCookingParams(&defaultParms);
        }

        public byte[] GenerateCachedPhysicsData(ReadOnlySpan<Vector3> verts)
        {
            using NxStream stream = GenerateCachedPhysicsDataStream(verts);

            byte[] cachedData = new byte[stream.Count];
            new Span<byte>(stream.Data, (int)stream.Count).CopyTo(cachedData);
            return cachedData;
        }

        public NxStream GenerateCachedPhysicsDataStream(ReadOnlySpan<Vector3> verts)
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
                    Flags = ConvexFlags.ComputeConvex | ConvexFlags.InflateConvex | ConvexFlags.UseUncompressedNormals
                };

                var stream = new NxStream();

                if (NxCookConvexMesh(&meshDesc, &stream) is 0)
                {
                    stream.Dispose();
                    throw new Exception("Convex Mesh cooking failed!");
                }
                return stream;
            }
        }

        private void InternalDispose()
        {
            if (!isDisposed)
            {
                NxCloseCooking();
                isDisposed = true;
            }
        }

        public void Dispose()
        {
            InternalDispose();
            GC.SuppressFinalize(this);
        }

        ~PhysXCooker()
        {
            InternalDispose();
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
