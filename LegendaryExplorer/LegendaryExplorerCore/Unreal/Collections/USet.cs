using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using LegendaryExplorerCore.Gammtek.Collections;
using Microsoft.Toolkit.HighPerformance;

namespace LegendaryExplorerCore.Unreal.Collections;

//C# version of Unreal's TSet
public class USet<T> : USet<T, T, DefaultKeyFuncs<T>> {}
public class USet<T, TKey, TKeyFuncs> : IEnumerable<T> where TKeyFuncs : IKeyFuncs<T, TKey>
{
    [DebuggerDisplay("SetElementId | {Index}")]
    public readonly record struct SetElementId
    {
        private readonly int Index;

        public bool IsValidId() => Index != -1;

        public SetElementId()
        {
            Index = -1;
        }

        public SetElementId(int index)
        {
            Index = index;
        }

        public static implicit operator int(SetElementId id)
        {
            return id.Index;
        }

        public static implicit operator SetElementId(int index)
        {
            return new SetElementId(index);
        }
    }
    private struct SetElement : IEquatable<SetElement>
    {
        public T Value;
        public SetElementId HashNextId;
        public int HashIndex;

        public SetElement()
        {
            Value = default;
            HashNextId = new SetElementId();
            HashIndex = 0;
        }

        public SetElement(T value) : this()
        {
            Value = value;
        }

        #region IEquatable

        public bool Equals(SetElement other)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is SetElement other && Equals(other);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<T>.Default.GetHashCode(Value);
        }

        public static bool operator ==(SetElement left, SetElement right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SetElement left, SetElement right)
        {
            return !left.Equals(right);
        }

        #endregion
    }

    private USparseArray<SetElement> Elements;
    private SetElementId[] Hash;
    private int HashSize; //MUST be a power of two!
    internal TKeyFuncs KeyFuncs;

    public int Count => Elements.Count;

    public USet()
    {
        Elements = new USparseArray<SetElement>();
        Hash = Array.Empty<SetElementId>();
        HashSize = 0;
        KeyFuncs = default;
    }

    public USet(int capacity)
    {
        Elements = new USparseArray<SetElement>(capacity);
        Hash = Array.Empty<SetElementId>();
        HashSize = 0;
        KeyFuncs = default;
    }

    public void Empty(int newCapacity = 0)
    {
        Elements.Empty(newCapacity);

        if (!ConditionalRehash(newCapacity, true))
        {
            if (HashSize > 0)
            {
                ClearHash();
            }
        }
    }

    public void Shrink()
    {
        Elements.Shrink();
        Relax();
    }

    public void Compact()
    {
        if (Elements.Compact())
        {
            Rehash();
        }
    }

    public void Relax()
    {
        ConditionalRehash(Elements.Count, true);
    }

    public bool IsValidId(SetElementId id)
    {
        return id.IsValidId() && id >= 0 && id < Elements.MaxIndex && Elements.IsAllocated(id);
    }

    internal T this[SetElementId index]
    {
        get
        {
            Guard.IsTrue(IsValidId(index));
            return Elements[index].Value;
        }
    }

    public void Add(T value, bool alwaysReplace = false)
    {
        if (alwaysReplace || !KeyFuncs.AllowDuplicateKeys)
        {
            SetElementId existingId = FindId(KeyFuncs.GetKey(value));

            if (existingId.IsValidId())
            {
                //replace
                Elements[existingId].Value = value;
                return;
            }
        }
        
        int elementId = Elements.Add(new SetElement(value));

        if (!ConditionalRehash(Elements.Count))
        {
            HashElement(elementId, ref Elements[elementId]);
        }
    }

    public bool TryAddUnique(T value)
    {
        if (FindId(KeyFuncs.GetKey(value)).IsValidId())
        {
            return false;
        }

        int elementId = Elements.Add(new SetElement(value));

        if (!ConditionalRehash(Elements.Count))
        {
            HashElement(elementId, ref Elements[elementId]);
        }
        return true;
    }

    public void AddRange(IEnumerable<T> enumerable)
    {
        foreach (T t in enumerable)
        {
            Add(t);
        }
    }

    public void Remove(SetElementId id)
    {
        if (HashSize > 0)
        {
            var elementBeingRemoved = Elements[id];

            for (ref SetElementId nextElementId = ref GetTypedHash(elementBeingRemoved.HashIndex); nextElementId.IsValidId(); nextElementId = ref Elements[nextElementId].HashNextId)
            {
                if (nextElementId == id)
                {
                    nextElementId = elementBeingRemoved.HashNextId;
                    break;
                }
            }
        }

        Elements.RemoveAt(id);
    }

    //returns number of elements removed
    public int Remove(TKey key)
    {
        int numRemovedElements = 0;

        if (HashSize > 0)
        {
            ref SetElementId nextElementId = ref GetTypedHash(KeyFuncs.GetKeyHash(key));
            while (nextElementId.IsValidId())
            {
                ref SetElement element = ref Elements[nextElementId];
                if (KeyFuncs.Matches(KeyFuncs.GetKey(element.Value), key))
                {
                    Remove(nextElementId);
                    numRemovedElements++;

                    if (!KeyFuncs.AllowDuplicateKeys)
                    {
                        break;
                    }
                }
                else
                {
                    nextElementId = ref element.HashNextId;
                }
            }
        }

        return numRemovedElements;
    }

    public bool Contains(TKey key) => FindId(key).IsValidId();


    public SetElementId FindId(TKey key)
    {
        if (HashSize > 0)
        {
            for (SetElementId elementId = GetTypedHash(KeyFuncs.GetKeyHash(key)); elementId.IsValidId(); elementId = Elements[elementId].HashNextId)
            {
                if (KeyFuncs.Matches(KeyFuncs.GetKey(Elements[elementId].Value), key))
                {
                    return elementId;
                }
            }
        }

        return new SetElementId();
    }

    public IEnumerable<SetElementId> MultiFindId(TKey key)
    {
        if (HashSize > 0)
        {
            for (SetElementId elementId = GetTypedHash(KeyFuncs.GetKeyHash(key)); elementId.IsValidId(); elementId = Elements[elementId].HashNextId)
            {
                if (KeyFuncs.Matches(KeyFuncs.GetKey(Elements[elementId].Value), key))
                {
                    yield return elementId;
                }
            }
        }
    }

    public bool TryGet(TKey key, out T value)
    {
        SetElementId id = FindId(key);

        if (id.IsValidId())
        {
            value = Elements[id].Value;
            return true;
        }
        value = default;
        return false;
    }

    private bool ConditionalRehash(int numHashedElements, bool allowShrinking = false)
    {
        int desiredHashSize = GetNumberOfHashBuckets(numHashedElements);

        if (numHashedElements > 0 && (HashSize == 0 
                                      || HashSize < desiredHashSize 
                                      || (HashSize > desiredHashSize && allowShrinking)))
        {
            HashSize = desiredHashSize;
            Rehash();
            return true;
        }
        return false;
    }

    internal void Rehash()
    {
        if (HashSize > 0)
        {
            if (HashSize != Hash.Length)
            {
                Hash = new SetElementId[HashSize];
            }
            ClearHash();

            using USparseArray<SetElement>.SparseArrayEnumerator enumerator = Elements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                HashElement(enumerator.Index, ref enumerator.CurrentRef);
            }
        }
        else
        {
            Hash = Array.Empty<SetElementId>();
        }
    }

    private void ClearHash()
    {
        //SetElementId is just an int, this sets them all to -1. Very fast since this just compiles down to a memset
        Hash.AsSpan().AsBytes().Fill(byte.MaxValue);
    }

    private void HashElement(SetElementId elementId, ref SetElement element)
    {
        element.HashIndex = KeyFuncs.GetKeyHash(KeyFuncs.GetKey(element.Value)) & (HashSize - 1);

        ref SetElementId hashSlot = ref GetTypedHash(element.HashIndex);
        element.HashNextId = hashSlot;
        hashSlot = elementId;
    }
    
    private ref SetElementId GetTypedHash(int hashIndex)
    {
        return ref Hash[hashIndex & (HashSize - 1)];
    }

    private static int GetNumberOfHashBuckets(int numHashedElements)
    {
        //provide way to configure these?
        const uint elementsPerBucket = 2;
        const uint baseNumberOfBuckets = 8;
        const uint minNumberOfHashedElements = 4;

        if (numHashedElements >= minNumberOfHashedElements)
        {
            return (int)BitOperations.RoundUpToPowerOf2((uint)numHashedElements / elementsPerBucket + baseNumberOfBuckets);
        }
        return 1;
    }

    public IEnumerator<T> GetEnumerator() => new SetEnumerator(Elements.GetEnumerator());

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private struct SetEnumerator : IRefEnumerator<T>
    {
        private USparseArray<SetElement>.SparseArrayEnumerator ParentEnumerator;

        public SetEnumerator(USparseArray<SetElement>.SparseArrayEnumerator parent)
        {
            ParentEnumerator = parent;
        }

        public bool MoveNext() => ParentEnumerator.MoveNext();

        public void Reset() => ParentEnumerator.Reset();

        public T Current => ParentEnumerator.CurrentRef.Value;

        object IEnumerator.Current => Current;

        public void Dispose() { }

        public ref T CurrentRef => ref ParentEnumerator.CurrentRef.Value;
    }

    internal IRefEnumerator<T> DangerousGetRefEnumerator() => new SetEnumerator(Elements.GetEnumerator());
}

public interface IKeyFuncs<in T, TKey>
{
    //TODO NET7: make these static
    public TKey GetKey(T t);

    public bool Matches(TKey a, TKey b);

    public int GetKeyHash(TKey key);

    public bool AllowDuplicateKeys { get; }
}

public struct DefaultKeyFuncs<T> : IKeyFuncs<T, T>
{
    public T GetKey(T t) => t;

    public bool Matches(T a, T b) => EqualityComparer<T>.Default.Equals(a, b);

    public int GetKeyHash(T key) => key.GetHashCode();
    public bool AllowDuplicateKeys => false;
}