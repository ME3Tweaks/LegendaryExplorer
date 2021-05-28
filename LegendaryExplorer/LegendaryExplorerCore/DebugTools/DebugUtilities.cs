#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.DebugTools
{
    public class DebugUtilities
    {
        public static void CompareByteArrays(byte[] arr1, byte[] arr2)
        {
            int maxCount = Math.Min(arr1.Length, arr2.Length);
            for (int i = 0; i < maxCount; i++)
            {
                if (arr1[i] != arr2[i])
                {
                    Debug.WriteLine($"Difference at 0x{i:X6}: {arr1[i]:X2} vs {arr2[i]:X2}");
                }
            }
        }
    }
}
#endif