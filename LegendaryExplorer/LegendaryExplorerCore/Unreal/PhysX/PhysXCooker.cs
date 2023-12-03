using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Unreal.PhysX
{
    public sealed unsafe class PhysXCooker : IDisposable
    {
        [DllImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static extern byte NxCookConvexMesh(ConvexMeshDesc* desc, PhysXStream* stream);

        [DllImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static extern byte NxInitCooking(void* allocator, void* outputstream);

        [DllImport(PhysXDllLoader.PHYSXCOOKING64_DLL)]
        private static extern void NxCloseCooking();

        private bool isDisposed;
        public PhysXCooker()
        {
            if (!PhysXDllLoader.EnsureCookingDll())
            {
                isDisposed = true;
                throw new Exception($"Unable to load {PhysXDllLoader.PHYSXCOOKING64_DLL}");
            }
            NxInitCooking(null, null);
        }

        // needs to be an instance method to ensure NXInitCooking has been called
        // ReSharper disable once MemberCanBeMadeStatic.Global
        public byte[] GenerateCachedPhysicsData(ReadOnlySpan<Vector3> verts)
        {
            fixed (Vector3* data = verts)
            {
                var meshDesc = new ConvexMeshDesc
                {
                    NumVertices = (uint)verts.Length,
                    PointStrideBytes = 12u,
                    Points = data,
                    Flags = ConvexFlags.ComputeConvex | ConvexFlags.InflateConvex | ConvexFlags.UseUncompressedNormals
                };

                using var stream = new PhysXStream();

                if (NxCookConvexMesh(&meshDesc, &stream) is 0)
                {
                    throw new Exception("Convex Mesh cooking failed!");
                }
                byte[] cachedData = new byte[stream.Count];
                new Span<byte>(stream.Data, (int)stream.Count).CopyTo(cachedData);
                return cachedData;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct PhysXStream : IDisposable
        {
            private void** VFTable;
            public byte* Data;
            private uint Capacity;
            public uint Count;
            private uint ReadPos;

            public PhysXStream()
            {
                VFTable = PhysXStreamVTable;
            }

            public void Dispose()
            {
                if (Data is not null)
                {
                    NativeMemory.Free(Data);
                    Data = null;
                }
            }

            private static readonly void** PhysXStreamVTable;

            static PhysXStream()
            {
                PhysXStreamVTable = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(PhysXStream), sizeof(void*) * 13);

                PhysXStreamVTable[0] = (delegate* unmanaged[MemberFunction]<PhysXStream*, void>)&Destructor;
                PhysXStreamVTable[1] = (delegate* unmanaged[MemberFunction]<PhysXStream*, byte>)&ReadByte;
                PhysXStreamVTable[2] = (delegate* unmanaged[MemberFunction]<PhysXStream*, ushort>)&ReadWord;
                PhysXStreamVTable[3] = (delegate* unmanaged[MemberFunction]<PhysXStream*, uint>)&ReadDWord;
                PhysXStreamVTable[4] = (delegate* unmanaged[MemberFunction]<PhysXStream*, float>)&ReadFloat;
                PhysXStreamVTable[5] = (delegate* unmanaged[MemberFunction]<PhysXStream*, double>)&ReadDouble;
                PhysXStreamVTable[6] = (delegate* unmanaged[MemberFunction]<PhysXStream*, void*, uint, void>)&ReadBuffer;
                PhysXStreamVTable[7] = (delegate* unmanaged[MemberFunction]<PhysXStream*, byte, PhysXStream*>)&StoreByte;
                PhysXStreamVTable[8] = (delegate* unmanaged[MemberFunction]<PhysXStream*, ushort, PhysXStream*>)&StoreWord;
                PhysXStreamVTable[9] = (delegate* unmanaged[MemberFunction]<PhysXStream*, uint, PhysXStream*>)&StoreDWord;
                PhysXStreamVTable[10] = (delegate* unmanaged[MemberFunction]<PhysXStream*, float, PhysXStream*>)&StoreFloat;
                PhysXStreamVTable[11] = (delegate* unmanaged[MemberFunction]<PhysXStream*, double, PhysXStream*>)&StoreDouble;
                PhysXStreamVTable[12] = (delegate* unmanaged[MemberFunction]<PhysXStream*, void*, uint, PhysXStream*>)&StoreBuffer;
            }


            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static void Destructor(PhysXStream* @this)
            {
                if (@this->Data is not null)
                {
                    NativeMemory.Free(@this->Data);
                    @this->Data = null;
                }
            }


            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static byte ReadByte(PhysXStream* @this)
            {
                uint pos = @this->ReadPos;
                @this->ReadPos += 1;
                if (pos > @this->Count)
                {
                    @this->ReadPos = pos;
                    return 0;
                }
                return @this->Data[pos];
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static ushort ReadWord(PhysXStream* @this)
            {
                uint pos = @this->ReadPos;
                @this->ReadPos += sizeof(ushort);
                if (pos > @this->Count)
                {
                    @this->ReadPos = pos;
                    return 0;
                }
                return *(ushort*)(@this->Data + pos);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static uint ReadDWord(PhysXStream* @this)
            {
                uint pos = @this->ReadPos;
                @this->ReadPos += sizeof(uint);
                if (pos > @this->Count)
                {
                    @this->ReadPos = pos;
                    return 0;
                }
                return *(uint*)(@this->Data + pos);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static float ReadFloat(PhysXStream* @this)
            {
                uint pos = @this->ReadPos;
                @this->ReadPos += sizeof(float);
                if (pos > @this->Count)
                {
                    @this->ReadPos = pos;
                    return 0;
                }
                return *(float*)(@this->Data + pos);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static double ReadDouble(PhysXStream* @this)
            {
                uint pos = @this->ReadPos;
                @this->ReadPos += sizeof(double);
                if (pos > @this->Count)
                {
                    @this->ReadPos = pos;
                    return 0;
                }
                return *(double*)(@this->Data + pos);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static void ReadBuffer(PhysXStream* @this, void* buffer, uint size)
            {
                uint endPos = @this->ReadPos + size;
                if (endPos <= @this->Count)
                {
                    Buffer.MemoryCopy(@this->Data + @this->ReadPos, buffer, size, size);
                }
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static PhysXStream* StoreByte(PhysXStream* @this, byte b)
            {
                EnsureCapacity(@this, sizeof(byte));
                *(@this->Data + @this->Count) = b;
                @this->Count += sizeof(byte);
                return @this;
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static PhysXStream* StoreWord(PhysXStream* @this, ushort w)
            {
                EnsureCapacity(@this, sizeof(ushort));
                *(ushort*)(@this->Data + @this->Count) = w;
                @this->Count += sizeof(ushort);
                return @this;
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static PhysXStream* StoreDWord(PhysXStream* @this, uint d)
            {
                EnsureCapacity(@this, sizeof(uint));
                *(uint*)(@this->Data + @this->Count) = d;
                @this->Count += sizeof(uint);
                return @this;
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static PhysXStream* StoreFloat(PhysXStream* @this, float f)
            {
                EnsureCapacity(@this, sizeof(float));
                *(float*)(@this->Data + @this->Count) = f;
                @this->Count += sizeof(float);
                return @this;
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static PhysXStream* StoreDouble(PhysXStream* @this, double f)
            {
                EnsureCapacity(@this, sizeof(double));
                *(double*)(@this->Data + @this->Count) = f;
                @this->Count += sizeof(double);
                return @this;
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
            public static PhysXStream* StoreBuffer(PhysXStream* @this, void* buffer, uint size)
            {
                EnsureCapacity(@this, size);
                Buffer.MemoryCopy(buffer, @this->Data + @this->Count, @this->Capacity - @this->Count, size);
                @this->Count += size;
                return @this;
            }

            private static void EnsureCapacity(PhysXStream* @this, uint size)
            {
                if (@this->Data is null)
                {
                    @this->Data = (byte*)NativeMemory.Alloc(@this->Capacity = BitOperations.RoundUpToPowerOf2(size));
                    @this->Count = 0;
                }
                uint newCount = @this->Count + size;
                if (newCount > @this->Capacity)
                {
                    @this->Data = (byte*)NativeMemory.Realloc(@this->Data, @this->Capacity = BitOperations.RoundUpToPowerOf2(newCount));
                }
            }
        }
    }
}
