using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LegendaryExplorer.SharedUI.PeregrineTreeView
{
    /// <summary>
    /// Based on Galasoft MvvmLight DispatcherHelper, but allowing a DispatecherPriority value to be applied.
    /// </summary>
    public static class DispatcherHelper
    {
        public static Dispatcher UIDispatcher { get; private set; }

        public static void Initialize()
        {
            if (UIDispatcher != null && UIDispatcher.Thread.IsAlive)
                return;

            UIDispatcher = Dispatcher.CurrentDispatcher;
        }

        private static void CheckDispatcher()
        {
            if (UIDispatcher != null)
                return;

            const string errorMessage = "The Dispatcher Helper is not initialized.\r\n\r\n" +
                                        "Call perDispatcherHelper.Initialize() in the static App constructor.";

            throw new InvalidOperationException(errorMessage);
        }

        public static void CheckBeginInvokeOnUI(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var unused = RunAsync(action, priority);
        }

        public static DispatcherOperation RunAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            CheckDispatcher();
            return UIDispatcher.BeginInvoke(action, priority);
        }

        // max heap keeps the highest sorting item at the top of the heap, without any requirment to exactly sort the remaining items
        private static readonly perMaxHeap<perDispatcherItemPair> _priorityQueue = new perMaxHeap<perDispatcherItemPair>();

        // Add a new operation to the queue, with the default priority
        public static void AddToQueue(Action action)
        {
            AddToQueue(action, DispatcherPriority.Normal);
        }

        // Add a new operation to the queue - operations are executed in DispatcherPriority order (highest value executes first)
        public static void AddToQueue(Action action, DispatcherPriority dispatcherPriority)
        {
            _priorityQueue.Add(new perDispatcherItemPair(action, dispatcherPriority));
        }

        public static void EmptyQueue()
        {
            _priorityQueue.Clear();
        }

        // execute each operation from the operations queue in order.
        // An operation may result in more items being added to the queue, which will be executed in the appropriate order.
        public static async Task ProcessQueueAsync()
        {
            while (_priorityQueue.Any())
            {
                // remove brings the next highest item to the top of the heap
                var pair = _priorityQueue.Remove();
                await RunAsync(pair.Action, pair.DispatcherPriority);
            }
        }

        // ================================================================================

        // internal class representing a queued operation
        private class perDispatcherItemPair : IComparable<perDispatcherItemPair>
        {
            public perDispatcherItemPair(Action action, DispatcherPriority dispatcherPriority)
            {
                Action = action;
                DispatcherPriority = dispatcherPriority;

                // calculate ItemIndex so that items added earlier sort higher in the heap
                // no need to worry about Ticks overflowing, ~100 billion days ought to be sufficient
                ItemIndex = long.MaxValue - DateTime.UtcNow.Ticks;
            }

            public Action Action { get; }

            public DispatcherPriority DispatcherPriority { get; }

            // keep items with the same DispatcherPriority in the order they were added to the queue
            private long ItemIndex { get; }

            public int CompareTo(perDispatcherItemPair other)
            {
                var result = ((int)DispatcherPriority).CompareTo((int)other.DispatcherPriority);

                if (result == 0)
                    result = ItemIndex.CompareTo(other.ItemIndex);

                return result;
            }
        }
    }
}
