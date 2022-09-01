using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.ToolsetDev.MemoryAnalyzer;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.AppCenter.Analytics;

namespace LegendaryExplorer.SharedUI.Bases
{
    /// <summary>
    /// Window subclass that allows the window to operate on a single package, and subscribe to package updates for that package.
    /// </summary>
    public abstract class WPFBase : NotifyPropertyChangedWindowBase, IPackageUser, IBusyUIHost
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
            MemoryAnalyzer.AddTrackedMemoryItem(new MemoryAnalyzerObjectExtended($"[{nameof(WPFBase)}] {memoryTrackerName}", new WeakReference(this)));
            if (submitTelemetry)
            {
                Analytics.TrackEvent("Opened tool", new Dictionary<string, string>
                {
                    {"Toolname", memoryTrackerName}
                });
            }

            Closing += WPFBase_Closing;
        }

        private void WPFBase_Closing(object sender, CancelEventArgs e)
        {
            if (pcc is { IsModified: true } && pcc.Users.Count == 1 &&
                MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(pcc.FilePath)} has unsaved changes. Do you really want to close {Title}?", "Unsaved changes", MessageBoxButton.YesNo))
            {
                e.Cancel = true;
            }
            else
            {
                DataContext = null; //Remove all binding sources
                Closing -= WPFBase_Closing;
            }
        }

        /// <summary>
        /// Registers use of an already open package. Releases the existing one, if any.
        /// This is the same as LoadMEPackage, but the package is already loaded
        /// </summary>
        /// <param name="package"></param>
        protected void RegisterPackage(IMEPackage package)
        {
            UnLoadMEPackage();
            Pcc = MEPackageHandler.OpenMEPackage(package, this);
        }

        protected void LoadMEPackage(string s)
        {
            UnLoadMEPackage();
            Pcc = MEPackageHandler.OpenMEPackage(s, this);
        }

        protected void LoadMEPackage(Stream stream, string associatedFilePath = null)
        {
            UnLoadMEPackage();
            Pcc = MEPackageHandler.OpenMEPackageFromStream(stream, associatedFilePath, user: this);
        }

        protected void UnLoadMEPackage()
        {
            pcc?.Release(this);
            Pcc = null;
        }

        public abstract void HandleUpdate(List<PackageUpdate> updates);

        private EventHandler wpfClosed;
        public void RegisterClosed(Action handler)
        {
            wpfClosed = (obj, args) =>
            {
                handler();
                pcc = null;
            };
            Closed += wpfClosed;
        }

        public void ReleaseUse()
        {
            Closed -= wpfClosed;
            wpfClosed = null;
        }

        public static bool IsOpenInExisting<T>(string filePath) where T : WPFBase
        {
            foreach (IMEPackage pcc in MEPackageHandler.PackagesInTools)
            {
                if (pcc.FilePath == filePath && pcc.Users.OfType<T>().Any())
                {
                    return true;
                }
            }
            return false;
        }

        public static bool GetExistingToolInstance<T>(string filePath, [NotNullWhen(true)] out T tool) where T : WPFBase
        {
            foreach (IMEPackage pcc in MEPackageHandler.PackagesInTools)
            {
                if (pcc.FilePath == filePath)
                {
                    foreach (T user in pcc.Users.OfType<T>())
                    {
                        tool = user;
                        return true;
                    }
                }
            }
            tool = null;
            return false;
        }

        public void HandleSaveStateChange(bool isSaving)
        {
            if (isSaving)
            {
                SetBusy("Saving");
            }
            else
            {
                EndBusy();
            }
        }

        #region Busy variables

        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isBusyTaskbar;

        public bool IsBusyTaskbar
        {
            get => _isBusyTaskbar;
            set => SetProperty(ref _isBusyTaskbar, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        public virtual void SetBusy(string text = null)
        {
            BusyText = text;
            IsBusy = true;
        }
        public virtual void EndBusy()
        {
            IsBusy = false;
        }

        #endregion
    }
}
