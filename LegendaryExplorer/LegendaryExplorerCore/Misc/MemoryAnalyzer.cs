using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Misc
{
    /// <summary>
    /// Used for tracking memory usage and finding memory leaks. Must be initialized with IsTrackingMemory = true;.
    /// </summary>
    public static class MemoryAnalyzer
    {
        public static bool IsTrackingMemory { get; set; }

        public static ObservableCollectionExtended<MemoryAnalyzerObject> TrackedMemoryObjects { get; } = new();

        [Conditional("DEBUG")]
        public static void AddTrackedMemoryItem(string objectname, WeakReference reference)
        {
            if (IsTrackingMemory)
            {
                //Force concurrency
                Task.Factory.StartNew(() => TrackedMemoryObjects.Add(new MemoryAnalyzerObject(objectname, reference)),
                    default, TaskCreationOptions.None, LegendaryExplorerCoreLib.SYNCHRONIZATION_CONTEXT);
            }
        }

        [Conditional("DEBUG")]
        public static void AddTrackedMemoryItem(MemoryAnalyzerObject obj)
        {
            if (IsTrackingMemory)
            {
                //Force concurrency
                Task.Factory.StartNew(() => TrackedMemoryObjects.Add(obj),
                    default, TaskCreationOptions.None, LegendaryExplorerCoreLib.SYNCHRONIZATION_CONTEXT);
            }
        }

        public static void Refresh()
        {
            TrackedMemoryObjects.Where(x => !x.IsAlive()).ForEach(x => x.RemainingLifetime--);
            TrackedMemoryObjects.RemoveAll(x => !x.IsAlive() && x.RemainingLifetime < 0);
            foreach (var v in TrackedMemoryObjects)
            {
                v.RefreshStatus();
            }
        }

        public static void CleanupOldRefs()
        {
            TrackedMemoryObjects.RemoveAll(x => !x.IsAlive());
        }

        public static void ForceFullGC(bool compactLargeObjectHeap = false)
        {
            // This should promote things into future generations and clear out. This is how jetbrains seems to do it in dotMemory according to stackoverflow
            // https://stackoverflow.com/questions/42022723/what-exactly-happens-when-i-ask-dotmemory-to-force-garbage-collection
            for (int i = 0; i < 4; i++)
            {
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }

            if (compactLargeObjectHeap)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, true, true);
            }
        }
    }
}
