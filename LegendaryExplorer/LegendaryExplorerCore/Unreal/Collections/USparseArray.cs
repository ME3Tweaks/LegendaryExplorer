using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using LegendaryExplorerCore.Gammtek.Collections;
using LegendaryExplorerCore.Gammtek.Collections.Specialized;
using ThrowHelper = LegendaryExplorerCore.Gammtek.ThrowHelper;

namespace LegendaryExplorerCore.Unreal.Collections;

//C# version of Unreal's TSparseArray
internal struct USparseArray<T> : IEnumerable<T>
{
    private struct ElementOrFreeListLink
    {
        public T Element;
        public int NextFreeIndex;
    }

    private ValueList<ElementOrFreeListLink> Data;
    private ResizeableBitArray AllocationFlags;
    private int FirstFreeIndex;
    private int NumFreeIndices;

    public USparseArray()
    {
        Data = ValueList<ElementOrFreeListLink>.Empty;
        AllocationFlags = new ResizeableBitArray();
        FirstFreeIndex = -1;
        NumFreeIndices = 0;
    }

    public USparseArray(int capacity)
    {
        Data = new ValueList<ElementOrFreeListLink>(capacity);
        AllocationFlags = new ResizeableBitArray(capacity);
        FirstFreeIndex = -1;
        NumFreeIndices = 0;
    }

    private ref T AllocateIndex(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Guard.IsLessThan(index, MaxIndex);
        Guard.IsFalse(IsAllocated(index));

        AllocationFlags[index] = true;

        return ref GetData(index).Element;
    }

    internal ref T AddUninitialized(out int index)
    {
        if (NumFreeIndices > 0)
        {
            index = FirstFreeIndex;
            FirstFreeIndex = GetData(FirstFreeIndex).NextFreeIndex;
            --NumFreeIndices;
        }
        else
        {
            index = Data.Count;
            Data.AddUnitializedElementsUnsafe(1);
            AllocationFlags.Add(false);
        }
        return ref AllocateIndex(index);
    }

    public int Add(T element)
    {
        AddUninitialized(out int index) = element;
        return index;
    }

    //The entire range must be allocated elements, no free slots!
    public void RemoveAt(int index, int count = 1)
    {
        if (count <= 0)
        {
            return;
        }
        ThrowHelper.ThrowIfNotInBounds(index, MaxIndex);
        ThrowHelper.ThrowIfNotInBounds(index + count - 1, MaxIndex);
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Data.AsSpanUnsafe(index, count).Clear();
        }

        while (count --> 0)
        {
            Guard.IsTrue(IsAllocated(index));

            ref var data = ref GetData(index);
            data.NextFreeIndex = NumFreeIndices > 0 ? FirstFreeIndex : -1;
            FirstFreeIndex = index;
            AllocationFlags[index] = false;
            ++NumFreeIndices;
            ++index;
        }
    }
        
    public void Empty(int newCapacity = 0)
    {
        Data.Clear(newCapacity);
        FirstFreeIndex = -1;
        NumFreeIndices = 0;
        AllocationFlags.Empty(newCapacity);
    }

    //empties the array, but keeps its allocation as slack
    public void Reset()
    {
        Data.Clear(Data.Capacity);
        FirstFreeIndex = -1;
        NumFreeIndices = 0;
        AllocationFlags.Reset();
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity > Data.Count)
        {
            int numToAdd = capacity - Data.Count;
            Data.AddUnitializedElementsUnsafe(numToAdd);
            int freeIndex = AllocationFlags.Count;
            AllocationFlags.Expand(numToAdd);
            while (freeIndex < capacity)
            {
                ref var data = ref GetData(freeIndex);
                data.NextFreeIndex = NumFreeIndices > 0 ? FirstFreeIndex : -1;
                FirstFreeIndex = freeIndex;
                ++NumFreeIndices;
                ++freeIndex;
            }
        }
    }

    //removes empty slots at end of array and reallocs to remove slack. Does not compact, so there may still be empty slots. 
    public void Shrink()
    {
        int firstIndexToRemove = AllocationFlags.MostSignificantBit() + 1;

        if (firstIndexToRemove < MaxIndex)
        {
            if (NumFreeIndices > 0)
            {
                // Look for elements in the free list that are in the memory to be freed.
                ref int previousNextFreeIndex = ref FirstFreeIndex;
                for (int FreeIndex = FirstFreeIndex;
                     FreeIndex != -1;
                     FreeIndex = previousNextFreeIndex)
                {
                    if (FreeIndex >= firstIndexToRemove)
                    {
                        previousNextFreeIndex = GetData(FreeIndex).NextFreeIndex;
                        --NumFreeIndices;
                    }
                    else
                    {
                        previousNextFreeIndex = ref GetData(FreeIndex).NextFreeIndex;
                    }
                }
            }

            Data.RemoveRange(firstIndexToRemove, Data.Count - firstIndexToRemove);
            AllocationFlags.RemoveFromEnd(AllocationFlags.Count - firstIndexToRemove);
        }

        Data.TrimExcess();
    }

    public bool Compact()
    {
        if (NumFreeIndices == 0)
        {
            return false;
        }

        var compactedArray = new USparseArray<T>();
        compactedArray.Empty(Count);

        foreach (T t in this)
        {
            compactedArray.Add(t);
        }

        Data = compactedArray.Data;
        AllocationFlags = compactedArray.AllocationFlags;
        FirstFreeIndex = compactedArray.FirstFreeIndex;
        NumFreeIndices = compactedArray.NumFreeIndices;

        return true;
    }

    public ref T this[int index]
    {
        get
        {
            ThrowHelper.ThrowIfNotInBounds(index, MaxIndex);
            Guard.IsTrue(IsAllocated(index));
            return ref GetData(index).Element;
        }
    }

    private ref ElementOrFreeListLink GetData(int index) => ref Data.GetReferenceUnsafe(index);

    public bool IsAllocated(int index) => AllocationFlags[index];
    public int MaxIndex => Data.Count;
    public int Count => Data.Count - NumFreeIndices;

    public SparseArrayEnumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct SparseArrayEnumerator : IRefEnumerator<T>
    {
        private USparseArray<T> SparseArray;
        public int Index { get; private set; }

        public SparseArrayEnumerator(USparseArray<T> sparseArray)
        {
            SparseArray = sparseArray;
            Index = -1;
        }

        public bool MoveNext()
        {
            int index = Index + 1;

            for(;index < SparseArray.MaxIndex; ++index)
            {
                if (SparseArray.IsAllocated(index))
                {
                    Index = index;
                    return true;
                }
            }
            return false;
        }

        public void Reset()
        {
            Index = -1;
        }

        public T Current => CurrentRef;
        public ref T CurrentRef => ref SparseArray.GetData(Index).Element;

        object IEnumerator.Current => Current;

        public void Dispose() {}
    }
}