using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Diagnostics;
using LegendaryExplorerCore.Gammtek.Collections;
using ThrowHelper = LegendaryExplorerCore.Gammtek.ThrowHelper;

namespace LegendaryExplorerCore.Unreal.Collections;

//C# version of Unreal's TMap
public abstract class UMapBase<TKey, TValue, TKeyFuncs> : IDictionary<TKey, TValue> where TKeyFuncs : struct, IKeyFuncs<KeyValuePair<TKey, TValue>, TKey>
{
    protected readonly USet<KeyValuePair<TKey, TValue>, TKey, TKeyFuncs> Pairs;

    protected UMapBase()
    {
        Pairs = new ();
    }

    protected UMapBase(int capacity)
    {
        Pairs = new (capacity);
    }

    protected UMapBase(IEnumerable<KeyValuePair<TKey, TValue>> enumerable) : this()
    {
        foreach ((TKey key, TValue value) in enumerable)
        {
            Add(key, value);
        }
    }

    public int Count => Pairs.Count;

    public ICollection<TKey> Keys
    {
        get
        {
            if (TKeyFuncs.AllowDuplicateKeys)
            {
                var keys = new HashSet<TKey>(Pairs.Count);
                foreach ((TKey key, _) in Pairs)
                {
                    keys.Add(key);
                }
                return keys;
            }
            else
            {
                var keys = new List<TKey>(Pairs.Count);
                foreach ((TKey key, _) in Pairs)
                {
                    keys.Add(key);
                }
                return keys;
            }
        }
    }

    public ICollection<TValue> Values
    {
        get
        {
            var vals = new List<TValue>();
            foreach ((TKey _, TValue value) in Pairs)
            {
                vals.Add(value);
            }
            return vals;
        }
    }

    public TValue this[TKey key]
    {
        get
        {
            if (Pairs.TryGet(key, out var kvp))
            {
                return kvp.Value;
            }
            throw new KeyNotFoundException();
        }
        set => Pairs.Add(new KeyValuePair<TKey, TValue>(key, value), true);
    }

    public void Add(TKey key, TValue value)
    {
        if (TKeyFuncs.AllowDuplicateKeys)
        {
            Pairs.Add(new KeyValuePair<TKey, TValue>(key, value));
        }
        else if (!Pairs.TryAddUnique(new KeyValuePair<TKey, TValue>(key, value)))
        {
            ThrowHelper.ThrowArgumentException(nameof(key), "An element with the same key already exists in this map.");
        }
    }
        
    public bool Remove(TKey key) => Pairs.Remove(key) > 0;

    public bool ContainsKey(TKey key) => Pairs.Contains(key);

    public void Empty(int expectedCapacity = 0) => Pairs.Empty(expectedCapacity);

    public void Shrink() => Pairs.Shrink();

    public void Compact() => Pairs.Compact();
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Pairs.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (Pairs.TryGet(key, out var kvp))
        {
            value = kvp.Value;
            return true;
        }
        value = default;
        return false;
    }

    internal IRefEnumerator<KeyValuePair<TKey, TValue>> DangerousGetRefEnumerator() => Pairs.DangerousGetRefEnumerator();

    internal void Rehash() => Pairs.Rehash();

    #region ICollection

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Pairs.Add(item);

    public void Clear() => Empty();

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
    {
        foreach (var id in Pairs.MultiFindId(item.Key))
        {
            if (EqualityComparer<TValue>.Default.Equals(Pairs[id].Value, item.Value))
            {
                return true;
            }
        }
        return false;
    }

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        Guard.IsNotNull(array);
        ThrowHelper.ThrowIfNotInBounds(arrayIndex, array.Length);
        Guard.IsLessThanOrEqualTo(array.Length - arrayIndex, Count);

        foreach (KeyValuePair<TKey, TValue> kvp in Pairs)
        {
            array[arrayIndex++] = kvp;
        }
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        foreach (var id in Pairs.MultiFindId(item.Key))
        {
            if (EqualityComparer<TValue>.Default.Equals(Pairs[id].Value, item.Value))
            {
                Pairs.Remove(id);
                return true;
            }
        }
        return false;
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    #endregion
}

public struct MapKeyFuncs<TKey, TValue> : IKeyFuncs<KeyValuePair<TKey, TValue>, TKey>
{
    public static TKey GetKey(KeyValuePair<TKey, TValue> kvp) => kvp.Key;

    public static bool Matches(TKey a, TKey b) => EqualityComparer<TKey>.Default.Equals(a, b);

    public static int GetKeyHash(TKey key) => key.GetHashCode();
    public static bool AllowDuplicateKeys => false;
}

public struct MultiMapKeyFuncs<TKey, TValue> : IKeyFuncs<KeyValuePair<TKey, TValue>, TKey>
{
    public static TKey GetKey(KeyValuePair<TKey, TValue> kvp) => kvp.Key;

    public static bool Matches(TKey a, TKey b) => EqualityComparer<TKey>.Default.Equals(a, b);

    public static int GetKeyHash(TKey key) => key.GetHashCode();
    public static bool AllowDuplicateKeys => true;
}

public class UMap<TKey, TValue> : UMapBase<TKey, TValue, MapKeyFuncs<TKey, TValue>>
{
    public UMap() : base(){}
    public UMap(int capacity) : base(capacity){}
}

public class UMultiMap<TKey, TValue> : UMapBase<TKey, TValue, MultiMapKeyFuncs<TKey, TValue>>
{
    public UMultiMap() : base() { }
    public UMultiMap(int capacity) : base(capacity) { }
    public UMultiMap(IEnumerable<KeyValuePair<TKey, TValue>> enumerable) : base(enumerable) { }

    public IEnumerable<TValue> MultiFind(TKey key) => Pairs.MultiFindId(key).Select(id => Pairs[id].Value);

    //will only add if there is no other entry with the same key and value
    public bool TryAddUnique(TKey key, TValue value)
    {
        foreach (var id in Pairs.MultiFindId(key))
        {
            if (EqualityComparer<TValue>.Default.Equals(Pairs[id].Value, value))
            {
                return false;
            }
        }
        Pairs.Add(new KeyValuePair<TKey, TValue>(key, value));
        return true;
    }

    public bool RemoveSingle(TKey key, TValue value)
    {
        return ((ICollection<KeyValuePair<TKey, TValue>>)this).Remove(new KeyValuePair<TKey, TValue>(key, value));
    }
}