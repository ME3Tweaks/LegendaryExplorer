using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.ME3ExpMemoryAnalyzer;
using ME3ExplorerCore.Misc;
using Microsoft.AppCenter.Analytics;

namespace ME3Explorer
{
    /// <summary>
    /// Inherits from the notify property base classes, giving them telemetry and memory tracking features on top of standard property notification methods
    /// </summary>
    public abstract class TrackingNotifyPropertyChangedWindowBase : NotifyPropertyChangedWindowBase
    {

        // trackTelemetry doesn't use conditional parameter to prevent us from making new windows and then tracking it with telemetry for things like a dialog/

        /// <summary>
        /// Base constructor for tracking windows. Specify track telemetry if this is a tool - if this is not a tool (e.g. a dialog) SET IT TO FALSE to only perform memory tracking.
        /// </summary>
        public TrackingNotifyPropertyChangedWindowBase(string trackingName, bool trackTelemetry)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(new MemoryAnalyzerObjectExtended($"[TrackingWindow] {trackingName}", new WeakReference(this)));
            if (trackTelemetry)
            {
#if !DEBUG
                Analytics.TrackEvent("Opened tool", new Dictionary<string, string>
                {
                    {"Toolname", trackingName}
                });
#endif
            }
        }
    }
}
