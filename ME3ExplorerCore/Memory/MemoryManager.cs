using Microsoft.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ME3ExplorerCore.Memory
{
    class MemoryManager
    {
        private static RecyclableMemoryStreamManager MemManager;

        private static bool UsePooledMemory { get; set; }
        public static void SetUsePooledMemory(bool usePooledMemory)
        {
            UsePooledMemory = usePooledMemory;
            MemManager = usePooledMemory ? new RecyclableMemoryStreamManager() : null;
        }

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
    }
}
