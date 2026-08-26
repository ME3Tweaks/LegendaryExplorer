using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LegendaryExplorerCore.SourceGenerators
{
    /// <summary>
    /// An immutable array with structural equality, so that models built by the generator pipeline can be
    /// cached properly. <see cref="ImmutableArray{T}"/> has reference equality, which defeats incremental caching.
    /// </summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new EquatableArray<T>(ImmutableArray<T>.Empty);

        private readonly ImmutableArray<T> _array;

        public EquatableArray(ImmutableArray<T> array) => _array = array;

        public EquatableArray(IEnumerable<T> items) => _array = ImmutableArray.CreateRange(items);

        public int Length => _array.IsDefault ? 0 : _array.Length;

        public T this[int index] => _array[index];

        public bool Equals(EquatableArray<T> other)
        {
            if (_array.IsDefault || other._array.IsDefault)
            {
                return _array.IsDefault && other._array.IsDefault;
            }
            if (_array.Length != other._array.Length)
            {
                return false;
            }
            for (int i = 0; i < _array.Length; i++)
            {
                T left = _array[i];
                T right = other._array[i];
                // Null tolerated on both sides, to match GetHashCode below.
                if (left is null ? right is not null : !left.Equals(right))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            if (_array.IsDefault)
            {
                return 0;
            }
            int hash = 17;
            foreach (T item in _array)
            {
                hash = unchecked(hash * 31 + (item?.GetHashCode() ?? 0));
            }
            return hash;
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_array.IsDefault)
            {
                yield break;
            }
            foreach (T item in _array)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
