#if DEBUG
using System;
using System.Diagnostics;

namespace LegendaryExplorerCore.DebugTools
{
    public static class DebugUtilities
    {
        [Conditional("DEBUG")]
        public static void CompareByteArrays(byte[] arr1, byte[] arr2)
        {
            int amountAllowedLeftToWrite = 20;
            Debug.WriteLine($"Lengths: {arr1.Length} vs {arr2.Length}");
            int maxCount = Math.Min(arr1.Length, arr2.Length);
            for (int i = 0; i < maxCount; i++)
            {
                if (arr1[i] != arr2[i])
                {
                    Debug.WriteLine($"Difference at 0x{i:X6}: {arr1[i]:X2} vs {arr2[i]:X2}");
                    if (amountAllowedLeftToWrite-- <= 0)
                    {
                        // Don't print a billion things.
                        break;
                    }
                }
            }
        }
    }
}
#endif