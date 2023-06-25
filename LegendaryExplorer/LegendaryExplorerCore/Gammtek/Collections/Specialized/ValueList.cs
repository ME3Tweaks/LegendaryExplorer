// This class is based on the ValueList<T> class from https://github.com/terrafx/terrafx
// The original code is Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;

namespace LegendaryExplorerCore.Gammtek.Collections.Specialized;

/// <summary>Represents a list of items that can be accessed by index.</summary>
/// <typeparam name="T">The type of the items contained in the list.</typeparam>
/// <remarks>This type is meant to be used as an implementation detail of another type and should not be part of your public api.</remarks>
[DebuggerDisplay("Capacity = {Capacity}; Count = {Count}")]
[DebuggerTypeProxy(typeof(ValueList<>.DebugView))]
internal struct ValueList<T> : IEnumerable<T>
{
    /// <summary>Gets an empty list.</summary>
    public static ValueList<T> Empty => new();

    private T[] _items;
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="ValueList{T}" /> struct.</summary>
    public ValueList()
    {
        _items = Array.Empty<T>();
        _count = 0;
    }

    /// <summary>Initializes a new instance of the <see cref="ValueList{T}" /> struct.</summary>
    /// <param name="capacity">The initial capacity of the list.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity" /> is <c>negative</c>.</exception>
    public ValueList(int capacity)
    {
        Guard.IsGreaterThanOrEqualTo(capacity, 0);

        if (capacity != 0)
        {
            _items = GC.AllocateUninitializedArray<T>(capacity);
        }
        else
        {
            _items = Array.Empty<T>();
        }
        _count = 0;
    }

    /// <summary>Initializes a new instance of the <see cref="ValueList{T}" /> struct.</summary>
    /// <param name="source">The enumerable that is used to populate the list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source" /> is <c>null</c>.</exception>
    public ValueList(IEnumerable<T> source)
    {
        // This is an extension method and throws ArgumentNullException if null
        T[] items = source.ToArray();

        _items = items;
        _count = items.Length;
    }

    /// <summary>Initializes a new instance of the <see cref="ValueList{T}" /> struct.</summary>
    /// <param name="span">The span that is used to populate the list.</param>
    public ValueList(ReadOnlySpan<T> span)
    {
        if (span.Length != 0)
        {
            T[] items = GC.AllocateUninitializedArray<T>(span.Length);
            span.CopyTo(items);
            _items = items;
        }
        else
        {
            _items = Array.Empty<T>();
        }

        _count = span.Length;
    }

    /// <summary>Initializes a new instance of the <see cref="ValueList{T}" /> struct.</summary>
    /// <param name="array">The array that is used to populate the list.</param>
    /// <param name="takeOwnership"><c>true</c> if the list should take ownership of the array; otherwise, <c>false</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array" /> is <c>null</c>.</exception>
    /// <remarks>By default ownership of <paramref name="array" /> is given to the value list.</remarks>
    public ValueList(T[] array, bool takeOwnership = true)
    {
        Guard.IsNotNull(array);

        if (takeOwnership)
        {
            _items = array;
        }
        else
        {
            T[] items = GC.AllocateUninitializedArray<T>(array.Length);
            Array.Copy(array, items, array.Length);
            _items = items;
        }

        _count = array.Length;
    }

    /// <summary>Gets the number of items that can be contained by the list without being resized.</summary>
    public readonly int Capacity
    {
        get
        {
            T[] items = _items;
            return items?.Length ?? 0;
        }
    }

    /// <summary>Gets the number of items contained in the list.</summary>
    public readonly int Count => _count;

    /// <summary>Gets or sets the item at the specified index in the list.</summary>
    /// <param name="index">The index of the item to get or set.</param>
    /// <returns>The item that exists at <paramref name="index" /> in the list.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> is negative or greater than or equal to <see cref="Count" />.</exception>
    public T this[int index]
    {
        readonly get
        {
            if (index < 0 || index >= _count)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            return _items[index];
        }

        set
        {
            if (index < 0 || index >= _count)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            _items[index] = value;
        }
    }

    /// <summary>Adds an item to the list.</summary>
    /// <param name="item">The item to add to the list.</param>
    public void Add(T item)
    {
        int count = _count;
        int newCount = count + 1;

        EnsureCapacity(newCount);

        _count = newCount;
        _items[count] = item;
    }

    /// <summary>Increments <see cref="Count"/> by <paramref name="numToAdd"/>, and expands the <see cref="Capacity"/> if neccesary. Any newly allocated memory may be uninitialized.</summary>
    /// <param name="numToAdd">The number of uninitialized items added to the list.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numToAdd" /> is <c>negative</c>.</exception>
    /// <remarks>
    ///     <para>This method allows you to explicitly grow the list.</para>
    ///     <para>This method is unsafe because growing the count may leak uninitialized memory.</para>
    /// </remarks>
    public void AddUnitializedElementsUnsafe(int numToAdd)
    {
        Guard.IsGreaterThanOrEqualTo(numToAdd, 0);

        int newCount = _count + numToAdd;
        EnsureCapacity(newCount);
        SetCountUnsafe(newCount);
    }

    /// <summary>Converts the backing array for the list to a span.</summary>
    /// <returns>A span that covers the backing array for the list.</returns>
    /// <remarks>
    ///     <para>This method is unsafe because other operations may invalidate the backing array.</para>
    ///     <para>This method is unsafe because it gives access to uninitialized memory in the backing array when <see cref="Count" /> is less than <see cref="Capacity" />.</para>
    /// </remarks>
    public Span<T> AsSpanUnsafe() => new Span<T>(_items);

    /// <summary>Converts the backing array for the list to a span starting at the specified index and continuing for the specified number of items.</summary>
    /// <param name="start">The index of the first item to include in the span.</param>
    /// <param name="length">The number of items to include in the span.</param>
    /// <returns>A span that covers the backing array for the list starting at <paramref name="start" /> and continuing for <paramref name="length" /> items.</returns>
    /// <remarks>
    ///     <para>This method is unsafe because other operations may invalidate the backing array.</para>
    ///     <para>This method is unsafe because it can give access to uninitialized memory in the backing array when <see cref="Count" /> is less than <see cref="Capacity" />.</para>
    /// </remarks>
    public Span<T> AsSpanUnsafe(int start, int length) => new Span<T>(_items, start, length);

    /// <summary>Removes all items from the list.</summary>
    public void Clear()
    {
        T[] items = _items;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() && items is not null)
        {
            Array.Clear(items, 0, _count);
        }

        _count = 0;
    }

    public void Clear(int newCapacity)
    {
        if (newCapacity == Capacity)
        {
            Clear();
        }
        else
        {
            _items = GC.AllocateUninitializedArray<T>(newCapacity);
            _count = 0;
        }
    }

    /// <summary>Checks whether the list contains a specified item.</summary>
    /// <param name="item">The item to check for in the list.</param>
    /// <returns><c>true</c> if <paramref name="item" /> was found in the list; otherwise, <c>false</c>.</returns>
    public readonly bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>Copies the items of the list to a span.</summary>
    /// <param name="destination">The span to which the items will be copied.</param>
    /// <exception cref="ArgumentException"><see cref="Count" /> is greater than the length of <paramref name="destination" />.</exception>
    public readonly void CopyTo(Span<T> destination) => _items.AsSpan(0, _count).CopyTo(destination);

    /// <summary>Ensures the capacity of the list is at least the specified value.</summary>
    /// <param name="capacity">The minimum capacity the list should support.</param>
    /// <remarks>This method does not throw if <paramref name="capacity" /> is negative and instead does nothing.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        int currentCapacity = Capacity;

        if (capacity > currentCapacity)
        {
            Resize(capacity, currentCapacity);
        }
    }

    /// <summary>Gets an enumerator that can iterate through the items in the list.</summary>
    /// <returns>An enumerator that can iterate through the items in the list.</returns>
    public ItemsEnumerator GetEnumerator() => new(this);

    /// <summary>Gets a reference to the item at the specified index of the list.</summary>
    /// <param name="index">The index of the item to get a pointer to.</param>
    /// <returns>A reference to the item that exists at <paramref name="index" /> in the list.</returns>
    /// <remarks>
    ///     <para>This method is because other operations may invalidate the backing array.</para>
    ///     <para>This method is because it does not validate that <paramref name="index" /> is less than <see cref="Capacity" />.</para>
    /// </remarks>
    public ref T GetReferenceUnsafe(int index)
    {
#if DEBUG
        ThrowHelper.ThrowExceptionIfNull(nameof(_items), _items);
        Guard.IsNotNull(_items);
        Guard.IsLessThanOrEqualTo((uint)index, (uint)Capacity);
#endif
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), index);
    }

    /// <summary>Gets the index of the first occurence of an item in the list.</summary>
    /// <param name="item">The item to find in the list.</param>
    /// <returns>The index of <paramref name="item" /> if it was found in the list; otherwise, <c>-1</c>.</returns>
    public readonly int IndexOf(T item)
    {
        T[] items = _items;
        return items is not null ? Array.IndexOf(items, item, 0, _count) : -1;
    }

    /// <summary>Inserts an item into list at the specified index.</summary>
    /// <param name="index">The zero-based index at which <paramref name="item" /> is inserted.</param>
    /// <param name="item">The item to insert into the list.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> is negative or greater than <see cref="Count" />.</exception>
    public void Insert(int index, T item)
    {
        int count = _count;
        if (index < 0 || index > count)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        int newCount = count + 1;
        EnsureCapacity(newCount);

        T[] items = _items;

        if (index != newCount)
        {
            Array.Copy(items, index, items, index + 1, count - index);
        }

        _count = newCount;
        items[index] = item;
    }

    /// <summary>Removes the first occurence of an item from the list.</summary>
    /// <param name="item">The item to remove from the list.</param>
    /// <returns><c>true</c> if <paramref name="item" /> was removed from the list; otherwise, <c>false</c>.</returns>
    public bool Remove(T item)
    {
        int index = IndexOf(item);

        if (index >= 0)
        {
            RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>Removes the item at the specified index from the list.</summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> is negative or greater than or equal to <see cref="Count" />.</exception>
    public void RemoveAt(int index)
    {
        int count = _count;
        if (index < 0 || index >= _count)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        int newCount = count - 1;
        T[] items = _items;

        if (index < newCount)
        {
            Array.Copy(items, index + 1, items, index, newCount - index);
        }

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            items[newCount] = default!;
        }

        _count = newCount;
    }

    public void RemoveRange(int startIndex, int length)
    {
        if (length <= 0)
        {
            return;
        }
        ThrowHelper.ThrowIfNotInBounds(startIndex, Count);
        ThrowHelper.ThrowIfNotInBounds(startIndex + length - 1, Count);

        int newCount = _count - length;

        if (startIndex < newCount)
        {
            Array.Copy(_items, startIndex + length, _items, startIndex, _count - (startIndex + length));
        }

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            AsSpanUnsafe(newCount, length).Clear();
        }

        _count = newCount;
    }

    /// <summary>Sets the number of items contained in the list.</summary>
    /// <param name="count">The new number of items contained in the list.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is <c>negative</c> or greater than <see cref="Capacity" />.</exception>
    /// <remarks>
    ///     <para>This method allows you to explicitly shrink the list down to zero or grow it up to <see cref="Capacity" />.</para>
    ///     <para>This method is unsafe because growing the count may leak uninitialized memory.</para>
    /// </remarks>
    public void SetCountUnsafe(int count)
    {
        if (count < 0 || count > Capacity)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
        }
        _count = count;
    }

    /// <summary>Trims any excess capacity from the list.</summary>
    public void TrimExcess()
    {
        if (_count < Capacity)
        {
            var newItems = GC.AllocateUninitializedArray<T>(_count);
            CopyTo(newItems);
            _items = newItems;
        }
    }

    private void Resize(int capacity, int currentCapacity)
    {
        int newCapacity = Math.Max(capacity, currentCapacity * 2);
        T[] newItems = GC.AllocateUninitializedArray<T>(newCapacity);

        CopyTo(newItems);
        _items = newItems;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <summary>An enumerator which can iterate through the items in a list.</summary>
    public struct ItemsEnumerator : IEnumerator<T>
    {
        private ValueList<T> _list;
        private int _index;

        internal ItemsEnumerator(ValueList<T> list)
        {
            _list = list;
            _index = -1;
        }

        /// <inheritdoc />
        public T Current => CurrentRef;

        /// <summary>Gets a readonly reference to the item at the current position of the enumerator.</summary>
        public ref readonly T CurrentRef => ref _list.GetReferenceUnsafe(_index);

        /// <inheritdoc />
        public bool MoveNext()
        {
            bool succeeded = true;
            int index = unchecked(_index + 1);

            if (index == _list._count)
            {
                index--;
                succeeded = false;
            }

            _index = index;
            return succeeded;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _index = -1;
        }

        object IEnumerator.Current => Current;

        void IDisposable.Dispose() { }
    }
    internal sealed class DebugView
    {
        private readonly ValueList<T> _list;

        public DebugView(ValueList<T> list)
        {
            _list = list;
        }

        public int Capacity => _list.Capacity;

        public int Count => _list._count;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get
            {
                ref readonly ValueList<T> list = ref _list;
                T[] items = GC.AllocateUninitializedArray<T>(list._count);

                list.CopyTo(items);
                return items;
            }
        }
    }
}