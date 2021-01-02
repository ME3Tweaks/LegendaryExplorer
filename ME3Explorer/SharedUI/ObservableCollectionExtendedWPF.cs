using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using ME3ExplorerCore.Misc;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// An observable collection that can be updated from other threads. Only works in WPF due to using BindingOperations class
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObservableCollectionExtendedWPF<T> : ObservableCollectionExtended<T>
    {
        public ObservableCollectionExtendedWPF() : base()
        {
            Application.Current.Dispatcher.Invoke(() => { BindingOperations.EnableCollectionSynchronization(this, GetSyncLock()); });
        }

        public ObservableCollectionExtendedWPF(IEnumerable<T> collection) : base(collection)
        {
            Application.Current.Dispatcher.Invoke(() => { BindingOperations.EnableCollectionSynchronization(this, GetSyncLock()); });
        }
    }
}
