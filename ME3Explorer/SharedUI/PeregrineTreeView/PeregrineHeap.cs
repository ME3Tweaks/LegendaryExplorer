using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.SharedUI.PeregrineTreeView
{
    // A Collection class where the Lowest / Highest sorting value is kept at the top of the heap
    // The remaining items are kept in an optimised manner without being precisely in sorted order

    public interface IperHeap<T> where T : IComparable<T>
    {
        int Count { get; }
        bool Any();

        void Add(T item);
        void Add(params T[] items);
        T Remove();
        T Peek();
    }

    public class perMinHeap<T> : perBaseHeap<T> where T : IComparable<T>
    {
        public perMinHeap()
            : base(HeapType.Min)
        {
        }
    }

    // ================================================================================

    public class perMaxHeap<T> : perBaseHeap<T> where T : IComparable<T>
    {
        public perMaxHeap()
            : base(HeapType.Max)
        {
        }
    }

    // ================================================================================

    public abstract class perBaseHeap<T> : IperHeap<T> where T : IComparable<T>
    {
        protected enum HeapType
        {
            Min,
            Max
        }

        private readonly HeapType _heapType;
        private int _capacity = 15;

        protected perBaseHeap(HeapType heapType)
        {
            _heapType = heapType;
            Count = 0;
            Heap = new T[_capacity];
        }

        private T[] Heap { get; set; }
        public int Count { get; private set; }

        public bool Any()
        {
            return Count > 0;
        }

        // Add a new item to the heap, then rearrange the items so that the highest / lowest item is at the top
        public void Add(T item)
        {
            // grow the heap array if necessary
            if (Count == _capacity)
            {
                _capacity = _capacity * 2 + 1;
                var newHeap = new T[_capacity];
                Array.Copy(Heap, 0, newHeap, 0, Count);
                Heap = newHeap;
            }

            Heap[Count] = item;
            Count++;

            FixHeapAfterAdd();
        }

        public void Add(params T[] items)
        {
            foreach (var item in items)
                Add(item);
        }

        // Return the highest / lowest value from the top of the heap, then re-arrange the remaining
        // items so that the next highest / lowest item is moved to the top.
        public T Remove()
        {
            if (Count == 0)
                throw new InvalidOperationException($"{nameof(Remove)}() called on an empty heap");

            var result = Heap[0];

            Count--;
            if (Count > 0)
                SwapItems(0, Count);

            Heap[Count] = default(T);
            FixHeapAfterRemove();

            return result;
        }

        // Return the highest / lowest item without removing it.
        public T Peek()
        {
            if (Count == 0)
                throw new InvalidOperationException($"{nameof(Peek)}() called on an empty heap");

            return Heap[0];
        }

        private void SwapItems(int indexA, int indexB)
        {
            if (indexA == indexB)
                return;

            var temp = Heap[indexA];
            Heap[indexA] = Heap[indexB];
            Heap[indexB] = temp;
        }

        // The new item wass added in last element of array (index = Count-1)
        // Now rearrange the heap, staring from the last item, so that each parent node is sorted higher than both of its children
        private void FixHeapAfterAdd()
        {
            var currentIndex = Count - 1;
            while (currentIndex > 0)
            {
                var parentIndex = (currentIndex - 1) / 2;
                var compare = Heap[currentIndex].CompareTo(Heap[parentIndex]);

                if ((_heapType == HeapType.Min && compare < 0) || (_heapType == HeapType.Max && compare > 0))
                {
                    SwapItems(parentIndex, currentIndex);
                    currentIndex = parentIndex;
                }
                else
                    break;
            }
        }

        // The Last item in the heap was swapped with the removed item (index 0), which is then effectively discarded as the heap count is reduced by 1.
        // Now rearrange the heap, starting from the top item, comparing it to each of its children, swapping to promote the best to that slot.
        // Repeat as necessary with the demoted item.
        private void FixHeapAfterRemove()
        {
            var currentIndex = 0;

            // Keep walking down the heap, placing best of three items (current + its two children) in highest position, so long as a swap is required            
            while (true)
            {
                var bestItemIndex = currentIndex;
                var leftChildIndex = currentIndex * 2 + 1;
                var rightChildIndex = currentIndex * 2 + 2;

                if (leftChildIndex < Count)
                {
                    var compare = Heap[leftChildIndex].CompareTo(Heap[currentIndex]);
                    if ((_heapType == HeapType.Min && compare < 0) || (_heapType == HeapType.Max && compare > 0))
                        bestItemIndex = leftChildIndex;
                }

                if (rightChildIndex < Count)
                {
                    var compare = Heap[rightChildIndex].CompareTo(Heap[bestItemIndex]);
                    if ((_heapType == HeapType.Min && compare < 0) || (_heapType == HeapType.Max && compare > 0))
                        bestItemIndex = rightChildIndex;
                }

                if (bestItemIndex == currentIndex)
                    break;

                SwapItems(currentIndex, bestItemIndex);
                currentIndex = bestItemIndex;
            }
        }

        public override string ToString()
        {
            return string.Join(" ", Heap.Take(Count));
        }
    }
}
