using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.PhysX;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NxAllocator
{
    const nuint Alignment = 16;

    private readonly void** VFTable;

    public NxAllocator()
    {
        VFTable = PhysXAllocatorVTable;
    }

    private static readonly void** PhysXAllocatorVTable;
    static NxAllocator()
    {
        PhysXAllocatorVTable = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(NxAllocator), sizeof(void*) * 8);

        PhysXAllocatorVTable[0] = (delegate* unmanaged[MemberFunction]<NxAllocator*, nuint, byte*, int, void*>)&mallocDEBUG;
        PhysXAllocatorVTable[1] = (delegate* unmanaged[MemberFunction]<NxAllocator*, nuint, byte*, int, byte*, int, void*>)&mallocDEBUGClass;
        PhysXAllocatorVTable[2] = (delegate* unmanaged[MemberFunction]<NxAllocator*, nuint, void*>)&malloc;
        PhysXAllocatorVTable[3] = (delegate* unmanaged[MemberFunction]<NxAllocator*, nuint, int, void*>)&mallocType;
        PhysXAllocatorVTable[4] = (delegate* unmanaged[MemberFunction]<NxAllocator*, void*, nuint, void*>)&realloc;
        PhysXAllocatorVTable[5] = (delegate* unmanaged[MemberFunction]<NxAllocator*, void*, void>)&free;
        PhysXAllocatorVTable[6] = (delegate* unmanaged[MemberFunction]<NxAllocator*, void>)&checkDEBUG;
        PhysXAllocatorVTable[7] = (delegate* unmanaged[MemberFunction]<NxAllocator*, void>)&Destructor;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void* realloc(NxAllocator* @this, void* memory, nuint size)
    {
        return NativeMemory.AlignedRealloc(memory, size, Alignment);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void free(NxAllocator* @this, void* memory)
    {
        NativeMemory.Free(memory);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void* malloc(NxAllocator* @this, nuint size)
    {
        return MallocImpl(size);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void* mallocType(NxAllocator* @this, nuint size, int type)
    {
        return MallocImpl(size);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void* mallocDEBUG(NxAllocator* @this, nuint size, byte* fileName, int line)
    {
        return MallocImpl(size);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void* mallocDEBUGClass(NxAllocator* @this, nuint size, byte* fileName, int line, byte* className, int type)
    {
        return MallocImpl(size);
    }

    private static void* MallocImpl(nuint size)
    {
        return NativeMemory.AlignedAlloc(size, Alignment);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void Destructor(NxAllocator* @this) { }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static void checkDEBUG(NxAllocator* @this) { }
}
