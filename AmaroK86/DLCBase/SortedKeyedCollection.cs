using System.Collections.Generic;
using System.Collections.ObjectModel;

public abstract class SortedKeyedCollection<TKey, TItem> : KeyedCollection<TKey, TItem>
{
    protected virtual IComparer<TKey> KeyComparer
    {
        get
        {
            return Comparer<TKey>.Default;
        }
    }

    protected override void InsertItem(int index, TItem item)
    {
        int insertIndex = index;

        for (int i = 0; i < Count; i++)
        {
            TItem retrievedItem = this[i];
            if (KeyComparer.Compare(GetKeyForItem(item), GetKeyForItem(retrievedItem)) < 0)
            {
                insertIndex = i;
                break;
            }
        }

        base.InsertItem(insertIndex, item);
    }
}
