using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Unreal.PhysX;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NxStream : IDisposable
{
    private readonly void** VFTable;
    public byte* Data;
    private uint Capacity;
    public uint Count;
    private uint ReadPos;

    public NxStream()
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

    static NxStream()
    {
        PhysXStreamVTable = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(NxStream), sizeof(void*) * 13);

        PhysXStreamVTable[0] = (delegate* unmanaged[MemberFunction]<NxStream*, void>)&Destructor;
        PhysXStreamVTable[1] = (delegate* unmanaged[MemberFunction]<NxStream*, byte>)&ReadByte;
        PhysXStreamVTable[2] = (delegate* unmanaged[MemberFunction]<NxStream*, ushort>)&ReadWord;
        PhysXStreamVTable[3] = (delegate* unmanaged[MemberFunction]<NxStream*, uint>)&ReadDWord;
        PhysXStreamVTable[4] = (delegate* unmanaged[MemberFunction]<NxStream*, float>)&ReadFloat;
        PhysXStreamVTable[5] = (delegate* unmanaged[MemberFunction]<NxStream*, double>)&ReadDouble;
        PhysXStreamVTable[6] = (delegate* unmanaged[MemberFunction]<NxStream*, void*, uint, void>)&ReadBuffer;
        PhysXStreamVTable[7] = (delegate* unmanaged[MemberFunction]<NxStream*, byte, NxStream*>)&StoreByte;
        PhysXStreamVTable[8] = (delegate* unmanaged[MemberFunction]<NxStream*, ushort, NxStream*>)&StoreWord;
        PhysXStreamVTable[9] = (delegate* unmanaged[MemberFunction]<NxStream*, uint, NxStream*>)&StoreDWord;
        PhysXStreamVTable[10] = (delegate* unmanaged[MemberFunction]<NxStream*, float, NxStream*>)&StoreFloat;
        PhysXStreamVTable[11] = (delegate* unmanaged[MemberFunction]<NxStream*, double, NxStream*>)&StoreDouble;
        PhysXStreamVTable[12] = (delegate* unmanaged[MemberFunction]<NxStream*, void*, uint, NxStream*>)&StoreBuffer;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void Destructor(NxStream* @this)
    {
        if (@this->Data is not null)
        {
            NativeMemory.Free(@this->Data);
            @this->Data = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static byte ReadByte(NxStream* @this)
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
    private static ushort ReadWord(NxStream* @this)
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
    private static uint ReadDWord(NxStream* @this)
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
    private static float ReadFloat(NxStream* @this)
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
    private static double ReadDouble(NxStream* @this)
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
    private static void ReadBuffer(NxStream* @this, void* buffer, uint size)
    {
        uint endPos = @this->ReadPos + size;
        if (endPos <= @this->Count)
        {
            Buffer.MemoryCopy(@this->Data + @this->ReadPos, buffer, size, size);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static NxStream* StoreByte(NxStream* @this, byte b)
    {
        EnsureCapacity(@this, sizeof(byte));
        *(@this->Data + @this->Count) = b;
        @this->Count += sizeof(byte);
        return @this;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static NxStream* StoreWord(NxStream* @this, ushort w)
    {
        EnsureCapacity(@this, sizeof(ushort));
        *(ushort*)(@this->Data + @this->Count) = w;
        @this->Count += sizeof(ushort);
        return @this;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static NxStream* StoreDWord(NxStream* @this, uint d)
    {
        EnsureCapacity(@this, sizeof(uint));
        *(uint*)(@this->Data + @this->Count) = d;
        @this->Count += sizeof(uint);
        return @this;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static NxStream* StoreFloat(NxStream* @this, float f)
    {
        EnsureCapacity(@this, sizeof(float));
        *(float*)(@this->Data + @this->Count) = f;
        @this->Count += sizeof(float);
        return @this;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static NxStream* StoreDouble(NxStream* @this, double f)
    {
        EnsureCapacity(@this, sizeof(double));
        *(double*)(@this->Data + @this->Count) = f;
        @this->Count += sizeof(double);
        return @this;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static NxStream* StoreBuffer(NxStream* @this, void* buffer, uint size)
    {
        EnsureCapacity(@this, size);
        Buffer.MemoryCopy(buffer, @this->Data + @this->Count, @this->Capacity - @this->Count, size);
        @this->Count += size;
        return @this;
    }

    private static void EnsureCapacity(NxStream* @this, uint size)
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
