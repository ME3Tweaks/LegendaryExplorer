using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JetBrains.Profiler.SelfApi;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
#if DEBUG
#endif

namespace LegendaryExplorer.ToolsetDev.MemoryAnalyzer
{
    /// <summary>
    /// Interaction logic for MemoryAnalyzer.xaml
    /// </summary>
    public partial class MemoryAnalyzerUI : TrackingNotifyPropertyChangedWindowBase
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

        public MemoryAnalyzerUI() : base("Memory Analyzer", false)
        {
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

        private void ForceGC_Click(object sender, RoutedEventArgs e)
        {
            LegendaryExplorerCore.Misc.MemoryAnalyzer.ForceFullGC();
        }

        private void Refresh()
        {
            LegendaryExplorerCore.Misc.MemoryAnalyzer.Refresh();

            LastRefreshText = "Last refreshed: " + DateTime.Now;
            CurrentMemoryUsageText = "Current process allocation: " + FileSize.FormatSize(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64);
        }

        private void CleanUpOldRefs_Click(object sender, RoutedEventArgs e)
        {
            LegendaryExplorerCore.Misc.MemoryAnalyzer.CleanupOldRefs();
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
            IsBusyText = "Taking snapshot (app will freeze for a moment)";
            await Task.Run(() => Thread.Sleep(1000));
            DotMemory.GetSnapshotOnce(conf);
            IsBusyText = "Opening dotMemory";
            await Task.Run(() => Thread.Sleep(4000));
            IsBusy = false;
#endif
        }

        private void MAUI_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
#if DEBUG
            if (sender is FrameworkElement fe && fe.DataContext is MemoryAnalyzerObject mao)
            {
                if (mao.reference.IsAlive)
                {
                    if (mao.reference.Target is UnrealPackageFile package)
                    {
                        var list = new List<string>();
                        list.Add($"CREATION STACK TRACE:\n{package.CreationStackTrace}");
                        list.AddRange(package.RegisterStackTraces);
                        ListDialog ld = new ListDialog(list, "Package register stack traces", "This is the list of stack traces that were used to register use of this package", this);
                        ld.Show();
                    }
                }
            }
#endif
        }
    }
}
