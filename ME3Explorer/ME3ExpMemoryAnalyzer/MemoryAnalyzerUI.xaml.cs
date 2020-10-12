using ME3Explorer.SharedUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
#if DEBUG
using JetBrains.Profiler.SelfApi;
#endif
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;

namespace ME3Explorer.ME3ExpMemoryAnalyzer
{
    /// <summary>
    /// Interaction logic for MemoryAnalyzer.xaml
    /// </summary>
    public partial class MemoryAnalyzerUI : NotifyPropertyChangedWindowBase
    {
        readonly DispatcherTimer dispatcherTimer;

        private bool _isBusy;
        private string _isBusyText;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }
        public string IsBusyText
        {
            get => _isBusyText;
            set => SetProperty(ref _isBusyText, value);
        }

        public MemoryAnalyzerUI()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(new MemoryAnalyzerObjectExtended("Memory Analyzer", new WeakReference(this)));
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
            CurrentMemoryUsageText = "Current process allocation: " + FileSize.FormatSize(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64);
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

        private void ShowOpenPackages_Click(object sender, RoutedEventArgs e)
        {
            var openPackages = MEPackageHandler.GetOpenPackages();
            ListDialog ld = new ListDialog(openPackages, "Open packages", "This is the list of packages the MEPackageHandler class has currently open.", this);
            ld.Show();
        }

        private async void TakeSnapshot_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            IsBusy = true;
            IsBusyText = "Ensuring dotMemory CLI";
            DotMemory.Config conf = new DotMemory.Config();
            conf.SaveToDir(Path.GetTempPath());
            conf.OpenDotMemory();
            await DotMemory.EnsurePrerequisiteAsync();
            IsBusyText = "Taking snapshot";
            await Task.Run(() => Thread.Sleep(1000));
            DotMemory.GetSnapshotOnce(conf);
            IsBusyText = "Opening dotMemory";
            await Task.Run(() => Thread.Sleep(4000));
            IsBusy = false;
#endif
        }
    }
}
