using ByteSizeLib;
using ME3Explorer.SharedUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ME3ExplorerCore.Misc;

namespace ME3Explorer.ME3ExpMemoryAnalyzer
{
    /// <summary>
    /// Interaction logic for MemoryAnalyzer.xaml
    /// </summary>
    public partial class MemoryAnalyzerUI : NotifyPropertyChangedWindowBase
    {
        readonly DispatcherTimer dispatcherTimer;

        public MemoryAnalyzerUI()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(new MemoryAnalyzerObjectExtended("Memory Analyzer", new WeakReference(this)));

            DataContext = this;
            Refresh();
            InitializeComponent();

            //  DispatcherTimer setup
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += automatedRefresh_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }



        private string _lastRefreshText;
        public string LastRefreshText { get => _lastRefreshText; set => SetProperty(ref _lastRefreshText, value); }
        private string _currentUsageText;
        public string CurrentMemoryUsageText { get => _currentUsageText; set => SetProperty(ref _currentUsageText, value); }

        private void automatedRefresh_Tick(object sender, EventArgs e)
        {
            Refresh();
        }

        private void ForceLargeGC_Click(object sender, RoutedEventArgs e)
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }

        private void ForceGC_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }

        private void Refresh()
        {
            MemoryAnalyzer.Refresh();

            LastRefreshText = "Last refreshed: " + DateTime.Now;
            CurrentMemoryUsageText = "Current process allocation: " + ByteSize.FromBytes(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64);
            //foreach (var item in InstancedTrackedMemoryObjects)
            //{
            //    item.RefreshStatus();
            //}
        }

        private void CleanUpOldRefs_Click(object sender, RoutedEventArgs e)
        {
            MemoryAnalyzer.CleanupOldRefs();
        }



        private void MemoryAnalyzer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dispatcherTimer.Stop();
        }
    }
}
