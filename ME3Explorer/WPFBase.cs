using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using ME3Explorer.ME3ExpMemoryAnalyzer;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using Microsoft.AppCenter.Analytics;

namespace ME3Explorer
{
    /// <summary>
    /// Window subclass that allows the window to operate on a single package, and subscribe to package updates for that package.
    /// </summary>
    public abstract class WPFBase : NotifyPropertyChangedWindowBase, IPackageUser
    {
        private IMEPackage pcc;
        /// <summary>
        /// Currently loaded Package file, if any.
        /// </summary>
        public IMEPackage Pcc
        {
            get => pcc;
            private set => SetProperty(ref pcc, value);
        }

        protected WPFBase(string memoryTrackerName, bool submitTelemetry = true)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(new MemoryAnalyzerObjectExtended($"[WPFBase] {memoryTrackerName}", new WeakReference(this)));
            if (submitTelemetry)
            {
                Analytics.TrackEvent("Opened tool", new Dictionary<string, string>
                {
                    {"Toolname", memoryTrackerName}
                });
            }

            this.Closing += WPFBase_Closing;
        }

        private void WPFBase_Closing(object sender, CancelEventArgs e)
        {
            if (pcc != null && pcc.IsModified && pcc.Users.Count == 1 &&
                MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(pcc.FilePath)} has unsaved changes. Do you really want to close {Title}?", "Unsaved changes", MessageBoxButton.YesNo))
            {
                e.Cancel = true;
            }
            else
            {
                DataContext = null; //Remove all binding sources
            }
        }

        public void LoadMEPackage(string s)
        {
            UnLoadMEPackage();
            Pcc = MEPackageHandler.OpenMEPackage(s, this);
        }

        public void LoadMEPackage(Stream stream, string associatedFilePath = null)
        {
            UnLoadMEPackage();
            Pcc = MEPackageHandler.OpenMEPackageFromStream(stream, associatedFilePath);
        }

        protected void UnLoadMEPackage()
        {
            pcc?.Release(this);
            Pcc = null;
        }

        public abstract void handleUpdate(List<PackageUpdate> updates);

        EventHandler wpfClosed;
        public void RegisterClosed(Action handler)
        {
            wpfClosed = (obj, args) =>
            {
                handler();
            };
            Closed += wpfClosed;
        }

        public void ReleaseUse()
        {
            Closed -= wpfClosed;
            wpfClosed = null;
        }

        public static bool TryOpenInExisting<T>(string filePath, out T tool) where T : WPFBase
        {
            foreach (IMEPackage pcc in MEPackageHandler.packagesInTools)
            {
                if (pcc.FilePath == filePath)
                {
                    foreach (var user in pcc.Users.OfType<T>())
                    {
                        tool = user;
                        tool.RestoreAndBringToFront();
                        return true;
                    }
                }
            }
            tool = null;
            return false;
        }
    }
}
