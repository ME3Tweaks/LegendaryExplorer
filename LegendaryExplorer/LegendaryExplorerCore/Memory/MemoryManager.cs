using Microsoft.IO;
using System;
using System.Buffers;
using System.IO;
using System.Runtime;
using System.Threading;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Memory
{
    public class MemoryManager
    {
        private static RecyclableMemoryStreamManager MemManager;
        private static ArrayPool<byte> ByteArrayPool;
        private static bool UsePooledMemory { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="usePooledMemory">If the application should use pooled memory or not. Setting to false will clear the pool</param>
        /// <param name="generateCallStacks">If callstacks for allocations should generated. Useful for debugging</param>
        /// <param name="aggressiveBufferReturn">If memory should be aggressively returned to the free pool</param>
        /// <param name="blockSize">The size of blocks to give out to memorystreams. Bigger blocks mean less allocations but potentially more wasted memory</param>
        /// <param name="maxBufferSizeMB">When a memorystream requests a block of memory bigger than this, it won't be pulled from the pool, it will just be allocated</param>
        /// <param name="maxFreeSmallPoolSizeMB">How much of a 'buffer' we should keep in the small block pool available for use. When memory is returned, if it would create more than this much unsued memory, it just drops it out of the pool and is reclaimed by the process</param>
        /// <param name="useExponentialBuffer">If pool size should scale linearly or exponentially</param>
        public static void SetUsePooledMemory(bool usePooledMemory,
            bool generateCallStacks = false,
            bool aggressiveBufferReturn = false,
            int blockSize = 256 * 1024,
            int maxBufferSizeMB = 128,
            int maxFreeSmallPoolSizeMB = 16,
            bool useExponentialBuffer = false)
        {
            if (maxBufferSizeMB >= 2048)
            {
                // Will overflow if you do this
                throw new Exception("MaxBufferSize cannot be bigger than 2048MB");
            }

            var maxBufSize = maxBufferSizeMB * (int)FileSize.MebiByte;

            UsePooledMemory = usePooledMemory;
            MemManager = !usePooledMemory
                ? null
                : new RecyclableMemoryStreamManager(
                    new RecyclableMemoryStreamManager.Options()
                    {
                        BlockSize = blockSize,
                        LargeBufferMultiple = (int)FileSize.MebiByte * 4,
                        MaximumBufferSize = maxBufSize,
                        UseExponentialLargeBuffer = useExponentialBuffer,
                        GenerateCallStacks = generateCallStacks,
                        AggressiveBufferReturn = aggressiveBufferReturn,
                        MaximumSmallPoolFreeBytes = FileSize.MebiByte * maxFreeSmallPoolSizeMB
                    });
            if (MemManager != null)
            {
                // Setup Byte Array Manager
                ByteArrayPool = ArrayPool<byte>.Create();
            }
            else
            {
                ByteArrayPool = null;
            }
        }

        #region MemoryStream Manager
        public static long LargePoolInUseSize => MemManager?.LargePoolInUseSize ?? 0;
        public static long LargePoolFreeSize => MemManager?.LargePoolFreeSize ?? 0;
        public static long BlockSize => MemManager?.Settings.BlockSize ?? 0;
        public static long SmallPoolInUseSize => MemManager?.SmallPoolInUseSize ?? 0;
        public static long MaximumBufferSize => MemManager?.Settings.MaximumBufferSize ?? 0;
        public static long SmallPoolFreeSize => MemManager?.SmallPoolFreeSize ?? 0;
        public static long LargePoolTotalSize => MemManager?.Settings.MaximumLargePoolFreeBytes ?? 0;
        public static long SmallPoolTotalSize => MemManager?.Settings.MaximumSmallPoolFreeBytes ?? 0;
        public static long SmallBlocksAvailable => MemManager?.SmallBlocksFree ?? 0;
        public static long LargeBlocksAvailable => MemManager?.LargeBuffersFree ?? 0;

        /// <summary>
        /// Gets a memory stream for use. If UsePooledMemory is false, this simply returns a new MemoryStream object.
        /// </summary>
        /// <returns></returns>
        public static MemoryStream GetMemoryStream()
        {
            if (UsePooledMemory)
                return MemManager.GetStream();

            return new MemoryStream();
        }

        internal static MemoryStream GetMemoryStream(int bufferSize, string memoryStreamTag = null)
        {
            if (UsePooledMemory)
                return MemManager.GetStream(memoryStreamTag, bufferSize);

            return new MemoryStream(bufferSize);
        }
        #endregion

        #region Byte Array Pooling

        /// <summary>
        /// Fetches a byte array of the specified size. If using pooled memory, make sure you return this object through ReturnByteArray(array)!!
        /// NOTE: IF USING POOLED MEMORY YOU MAY RECEIVE A BUFFER LARGER THAN THE REQUESTED SIZE
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static byte[] GetByteArray(int size)
        {
            if (UsePooledMemory)
                return ByteArrayPool.Rent(size);
            return new byte[size];
        }

        /// <summary>
        /// Returns an array for re-use if using pooled memory.
        /// </summary>
        /// <param name="array">An array that was checked out using GetByteArray(). If UsePooledMemory is false, this method does nothing.</param>
        public static void ReturnByteArray(byte[] array)
        {
            if (!UsePooledMemory)
                return;
            ByteArrayPool.Return(array);
        }
        #endregion

        /// <summary>
        /// Attempts to free all memory used by this memory manager. ONLY USE WHEN YOU ARE SURE THE MEMORY MANAGER IS NOT IN USE.
        /// </summary>
        public static void ResetMemoryManager()
        {
            if (UsePooledMemory)
            {
                bool isResetting = MemManager != null;

                // Not really sure what good defaults are here to use.....
                MemManager = new RecyclableMemoryStreamManager(
                    new RecyclableMemoryStreamManager.Options()
                    {
                        BlockSize = (int)FileSize.KibiByte * 256,
                        LargeBufferMultiple = (int)FileSize.MebiByte,
                        MaximumBufferSize = (int)FileSize.MebiByte * 16,
                        GenerateCallStacks = false,
                        AggressiveBufferReturn = true,
                    });

                ByteArrayPool = ArrayPool<byte>.Create();

                if (isResetting)
                {
                    int i = 2;
                    while (i > 0)
                    {
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                        i--;
                        Thread.Sleep(500);
                    }
                }
            }
        }
    }
}
