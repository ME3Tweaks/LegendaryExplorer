using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Packages;
using PropertyChanged;

namespace LegendaryExplorerCore.Misc
{
    public class MemoryAnalyzerObject : INotifyPropertyChanged
    {
        public readonly WeakReference reference;
        public string AllocationTime { get; }

        private double normalPostGCLifetime = 10d; //10s
        public double RemainingLifetime { get; set; }
        public string ReferenceName { get; set; }

        [DependsOn(nameof(RemainingLifetime))]
        public double PercentTimeRemaining => RemainingLifetime / normalPostGCLifetime;

        public virtual string ReferenceStatus
        {
            get
            {
                if (reference.IsAlive)
                {
                    if (reference.Target is UnrealPackageFile upf && (upf.FilePath == null || Path.GetFileNameWithoutExtension(upf.FilePath) != "Core"))
                    {
                        if (upf.RefCount > 0)
                        {
                            return $"{upf.RefCount} PH refs";
                        }
                        else
                        {
                            return "0 PH refs, should GC";
                        }
                    }

                    return "In Memory";
                }
                return "Garbage Collected";
            }
        }

        public MemoryAnalyzerObject(string referenceName, WeakReference reference)
        {
            RemainingLifetime = normalPostGCLifetime;
            AllocationTime = DateTime.Now.ToString();
            this.reference = reference;
            this.ReferenceName = referenceName;
        }

        public void RefreshStatus()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReferenceStatus)));
        }

        public bool IsAlive()
        {
            return reference.IsAlive;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
