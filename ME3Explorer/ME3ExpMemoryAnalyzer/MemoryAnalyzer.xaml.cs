using ByteSizeLib;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ME3Explorer.ME3ExpMemoryAnalyzer
{
    /// <summary>
    /// Interaction logic for MemoryAnalyzer.xaml
    /// </summary>
    public partial class MemoryAnalyzer : NotifyPropertyChangedWindowBase
    {

        #region Static Reference Adding
        private static List<MemoryAnalyzerObject> TrackedMemoryObjects = new List<MemoryAnalyzerObject>();
        public static void AddTrackedMemoryItem(string objectname, WeakReference reference)
        {
#if DEBUG
            //Force concurrency
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                TrackedMemoryObjects.Add(new MemoryAnalyzerObject(objectname, reference));
            }));
        }
#endif
#endregion

        public ObservableCollectionExtended<MemoryAnalyzerObject> InstancedTrackedMemoryObjects { get; set; } = new ObservableCollectionExtended<MemoryAnalyzerObject>();

        DispatcherTimer dispatcherTimer;

        public MemoryAnalyzer()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Memory Analyzer", new WeakReference(this));

            DataContext = this;
            Refresh();
            InitializeComponent();

            //  DispatcherTimer setup
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(automatedRefresh_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 3);
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

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            TrackedMemoryObjects.Where(x => !x.IsAlive()).ToList().ForEach(x => x.RemainingLifetimeAfterGC--);
            TrackedMemoryObjects.RemoveAll(x => !x.IsAlive() && x.RemainingLifetimeAfterGC < 0);
            InstancedTrackedMemoryObjects.ReplaceAll(TrackedMemoryObjects);
            LastRefreshText = "Last refreshed: " + DateTime.Now;
            CurrentMemoryUsageText = "Current process allocation: " + ByteSize.FromBytes(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64);
            //foreach (var item in InstancedTrackedMemoryObjects)
            //{
            //    item.RefreshStatus();
            //}
        }

        private void CleanUpOldRefs_Click(object sender, RoutedEventArgs e)
        {
            TrackedMemoryObjects.RemoveAll(x => !x.IsAlive());
            InstancedTrackedMemoryObjects.ReplaceAll(TrackedMemoryObjects);
        }

        public class MemoryAnalyzerObject : NotifyPropertyChangedBase
        {
            private WeakReference Reference;
            public string AllocationTime { get; }
            private string _referenceName;
            public System.Windows.Media.Brush DrawColor
            {
                get
                {
                    if (RemainingLifetimeAfterGC < 5)
                    {
                        //Fadeout
                        return new SolidColorBrush(Color.FromArgb((byte)(128 + (RemainingLifetimeAfterGC * 25)), 0, 0, 0));
                    }
                    else
                    {
                        return Brushes.Black;
                    }
                }
            }
            public int RemainingLifetimeAfterGC = 10;
            public string ReferenceName
            {
                get => _referenceName;
                set => SetProperty(ref _referenceName, value);
            }

            public string ReferenceStatus
            {
                get
                {
                    if (Reference.IsAlive)
                    {
                        if (Reference.Target is FrameworkElement w)
                        {
                            return w.IsLoaded ? "In Memory, Open" : "In Memory, Closed";
                        }
                        if (Reference.Target is System.Windows.Forms.Control f)
                        {
                            return f.IsDisposed ? "In Memory, Disposed" : "In Memory, Active";
                        }
                        return "In Memory";
                    }
                    else
                    {
                        return "Garbage Collected";
                    }
                }
            }

            public MemoryAnalyzerObject(string ReferenceName, WeakReference Reference)
            {
                AllocationTime = DateTime.Now.ToString();
                this.Reference = Reference;
                this.ReferenceName = ReferenceName;
            }

            public void RefreshStatus()
            {
                OnPropertyChanged(nameof(ReferenceStatus));
            }

            public bool IsAlive()
            {
                return Reference.IsAlive;
            }
        }

        private void MemoryAnalyzer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dispatcherTimer.Stop();
        }
    }
}
