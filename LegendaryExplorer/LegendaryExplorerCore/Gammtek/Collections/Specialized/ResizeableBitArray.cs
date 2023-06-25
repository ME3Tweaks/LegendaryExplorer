using System;
using System.Numerics;
using CommunityToolkit.Diagnostics;

namespace LegendaryExplorerCore.Gammtek.Collections.Specialized;

internal struct ResizeableBitArray
{
    private const int BITS_PER_INT = 32;
    private const int BITS_INDEX_MASK = 31;
    private const int INT_INDEX_SHIFT = 5;

    private ValueList<int> Data;
    private int NumBits;

    public ResizeableBitArray()
    {
        Data = ValueList<int>.Empty;
        NumBits = 0;
    }

    public ResizeableBitArray(int capacity)
    {
        int intCapacity = NumIntsNeeded(capacity);
        Data = new ValueList<int>(intCapacity);
        Data.AsSpanUnsafe().Clear();
        NumBits = 0;
    }

    public bool this[int i]
    {
        get => Get(i);
        set => Set(i, value);
    }

    /// <summary>
    /// Number of bits in the bit array
    /// </summary>
    public int Count => NumBits;

    /// <summary>
    /// Gets the bit at index <paramref name="i"/>
    /// </summary>
    /// <param name="i">Index into the bit array</param>
    /// <returns>True if the bit is 1, false if the bit is 0</returns>
    public bool Get(int i)
    {
        ThrowHelper.ThrowIfNotInBounds(i, NumBits);
        //use GetReferenceUnsafe instead of the indexer since Data.Count will always be 0
        return (Data.GetReferenceUnsafe(i >> INT_INDEX_SHIFT) & (1 << i)) != 0;
    }

    /// <summary>
    /// Sets the bit at index <paramref name="i"/>
    /// </summary>
    /// <param name="i">Index into the bit array</param>
    public void Set(int i, bool value)
    {
        ThrowHelper.ThrowIfNotInBounds(i, NumBits);
        ref int dword = ref Data.GetReferenceUnsafe(i >> INT_INDEX_SHIFT);
        if (value)
        {
            dword |= (1 << i);
        }
        else
        {
            dword &= ~(1 << i);
        }
    }

    public void SetRange(int startIndex, int length, bool value)
    {
        if (length <= 0)
        {
            return;
        }
        ThrowHelper.ThrowIfNotInBounds(startIndex, NumBits);
        ThrowHelper.ThrowIfNotInBounds(startIndex + length - 1, NumBits);

        int intIndex = (startIndex >> INT_INDEX_SHIFT) - 1;
        int numInCurrentInt = startIndex & BITS_INDEX_MASK;
        if (numInCurrentInt > 0)
        {
            ++intIndex;
            int mask;
            if (length < numInCurrentInt)
            {
                mask = ~(-1 << length) << (BITS_PER_INT - numInCurrentInt);
            }
            else
            {
                mask = -1 << (BITS_PER_INT - numInCurrentInt);
            }
            ref int dword = ref Data.GetReferenceUnsafe(intIndex);
            if (value)
            {
                dword |= mask;
            }
            else
            {
                dword &= ~mask;
            }
            ++intIndex;
            length -= numInCurrentInt;
            if (length <= 0)
            {
                return;
            }
        }
        (int numFullIntsToSet, int numInFinalInt) = Math.DivRem(length, BITS_PER_INT);
        if (numFullIntsToSet > 0)
        {
            Data.AsSpanUnsafe(intIndex, numFullIntsToSet).Fill(value ? -1 : 0);
            intIndex += numFullIntsToSet;
        }
        if (numInFinalInt > 0)
        {
            int mask = ~(-1 << numInFinalInt);
            ref int dword = ref Data.GetReferenceUnsafe(intIndex);
            if (value)
            {
                dword |= mask;
            }
            else
            {
                dword &= ~mask;
            }
        }
    }

    public void Add(bool value)
    {
        int newCount = NumBits + 1;
        EnsureCapacity(newCount);
        if ((newCount & BITS_INDEX_MASK) == 1)
        {
            Data.SetCountUnsafe(Data.Count + 1);
        }
        int index = NumBits;
        ++NumBits;
        Set(index, value);
    }

    public void RemoveFromEnd(int numToRemove)
    {
        Guard.IsLessThanOrEqualTo(numToRemove, NumBits);
        NumBits -= numToRemove;
        Data.SetCountUnsafe(NumIntsNeeded(NumBits));
    }

    public void Expand(int numToAdd)
    {
        Guard.IsGreaterThanOrEqualTo(numToAdd, 0);
        int newCount = NumBits + numToAdd;
        EnsureCapacity(newCount);
        int intsToAdd = NumIntsNeeded(newCount) - NumIntsNeeded(NumBits);
        Data.SetCountUnsafe(Data.Count + intsToAdd);
        int index = NumBits;
        NumBits = newCount;
        SetRange(index, numToAdd, false);
    }

    private void EnsureCapacity(int newCapacity)
    {
        int newIntCapacity = NumIntsNeeded(newCapacity);
        int oldIntCapacity = Data.Capacity;
        if (newIntCapacity > oldIntCapacity)
        {
            Data.EnsureCapacity(newIntCapacity);
        }
    }

    public void Empty(int newCapacity = 0)
    {
        int newIntCapacity = NumIntsNeeded(newCapacity);
        Data.Clear(newIntCapacity);
        NumBits = 0;
    }

    //empties the array, but keeps its allocated capacity
    public void Reset()
    {
        Data.Clear(Data.Capacity);
        NumBits = 0;
    }

    private static int NumIntsNeeded(int numBits)
    {
        return unchecked((int)((uint)(numBits - 1 + (1 << 5)) >> 5));
    }

    //Returns the index of the last 1 in the array, or -1 if all 0s
    public int MostSignificantBit()
    {
        int intIndex = (NumBits >> INT_INDEX_SHIFT) - 1;
        int numBitsInLastInt = NumBits & BITS_INDEX_MASK;
        if (numBitsInLastInt > 0)
        {
            //clear the unused bits
            ++intIndex;
            ref int dword = ref Data.GetReferenceUnsafe(intIndex);
            dword &= ~(-1 << numBitsInLastInt);
        }
        Span<int> span = Data.AsSpanUnsafe(0, intIndex + 1);
        //may be possible to vectorize?
        while (intIndex --> 0)
        {
            int lzc = BitOperations.LeadingZeroCount(unchecked((uint)span[intIndex]));
            if (lzc < BITS_PER_INT)
            {
                return intIndex * BITS_PER_INT + (BITS_PER_INT - 1 - lzc);
            }
        }
        return -1;
    }
}