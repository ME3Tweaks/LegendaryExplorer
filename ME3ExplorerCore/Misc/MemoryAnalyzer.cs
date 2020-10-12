using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3ExplorerCore.Helpers;

namespace ME3ExplorerCore.Misc
{
    /// <summary>
    /// Used for tracking memory usage and finding memory leaks. Must be initialized with IsTrackingMemory = true;.
    /// </summary>
    public static class MemoryAnalyzer
    {
        public static bool IsTrackingMemory { get; set; }

        public static ObservableCollectionExtended<MemoryAnalyzerObject> TrackedMemoryObjects { get; } = new ObservableCollectionExtended<MemoryAnalyzerObject>();

        [Conditional("DEBUG")]
        public static void AddTrackedMemoryItem(string objectname, WeakReference reference)
        {
            if (IsTrackingMemory)
            {
                //Force concurrency
                Task.Factory.StartNew(() => TrackedMemoryObjects.Add(new MemoryAnalyzerObject(objectname, reference)),
                    default, TaskCreationOptions.None, CoreLib.SYNCHRONIZATION_CONTEXT);
            }
        }

        [Conditional("DEBUG")]
        public static void AddTrackedMemoryItem(MemoryAnalyzerObject obj)
        {
            if (IsTrackingMemory)
            {
                //Force concurrency
                Task.Factory.StartNew(() => TrackedMemoryObjects.Add(obj),
                    default, TaskCreationOptions.None, CoreLib.SYNCHRONIZATION_CONTEXT);
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
    }
}
