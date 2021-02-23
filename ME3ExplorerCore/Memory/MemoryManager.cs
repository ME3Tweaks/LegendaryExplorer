using Microsoft.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime;
using System.Text;
using System.Threading;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using Newtonsoft.Json;

namespace ME3ExplorerCore.Memory
{
    public class MemoryManager
    {
        private static RecyclableMemoryStreamManager MemManager;
        private static ArrayPool<byte> ByteArrayPool;
        private static bool UsePooledMemory { get; set; }

        public static void SetUsePooledMemory(bool usePooledMemory,
            bool generateCallStacks = false,
            bool aggressiveBufferReturn = false,
            int blockSize = 256 * 1024,
            int maxBufferSizeMB = 128,
            int maxFreeSmallPoolSizeMB = 16,
            bool useExponentialBuffer = false)
        {
            UsePooledMemory = usePooledMemory;
            MemManager = usePooledMemory ? new RecyclableMemoryStreamManager(blockSize, (int)FileSize.MebiByte * 4, maxBufferSizeMB * (int)FileSize.MebiByte, useExponentialBuffer) : null;
            if (MemManager != null)
            {
                // Setup Stream Manager
                MemManager.GenerateCallStacks = generateCallStacks;
                MemManager.AggressiveBufferReturn = aggressiveBufferReturn;
                MemManager.MaximumFreeSmallPoolBytes = FileSize.MebiByte * maxFreeSmallPoolSizeMB;

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
        public static long BlockSize => MemManager?.BlockSize ?? 0;
        public static long SmallPoolInUseSize => MemManager?.SmallPoolInUseSize ?? 0;
        public static long MaximumBufferSize => MemManager?.MaximumBufferSize ?? 0;
        public static long SmallPoolFreeSize => MemManager?.SmallPoolFreeSize ?? 0;
        public static long LargePoolTotalSize => MemManager?.MaximumFreeLargePoolBytes ?? 0;
        public static long SmallPoolTotalSize => MemManager?.MaximumFreeSmallPoolBytes ?? 0;
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

            return new MemoryStream();
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
                bool isResetting = false;
                if (MemManager == null || (MemManager.LargePoolInUseSize == 0 && MemManager.SmallPoolInUseSize == 0)) // Only allow if no items are 'in use'
                {
                    // Not really sure what good defaults are here to use.....
                    if (MemManager != null) isResetting = true;
                    MemManager = new RecyclableMemoryStreamManager((int)FileSize.KibiByte * 256, (int)FileSize.MebiByte, (int)FileSize.MebiByte * 16);
                    MemManager.GenerateCallStacks = false;
                    MemManager.AggressiveBufferReturn = true;
                }

                ByteArrayPool = ArrayPool<byte>.Create();

                if (isResetting)
                {
                    int i = 4;
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
