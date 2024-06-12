using System;
using System.Windows.Controls;
using LegendaryExplorer.Misc;
using LegendaryExplorer.ToolsetDev.MemoryAnalyzer;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Subclass of UserControl that also sets up the CurrentLoadedExport, LoadExport(), UnloadExport() and more methods
    /// </summary>
    public abstract class ExportLoaderControl : NotifyPropertyChangedControlBase, IDisposable
    {
        /// <summary>
        /// If this ExportLoaderControl has been popped out to its own standalone window
        /// </summary>
        public bool IsPoppedOut { get; set; }

        protected ExportLoaderControl(string memoryTrackerName)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(new MemoryAnalyzerObjectExtended($"[EL] {memoryTrackerName}", new WeakReference(this)));
        }
        /// <summary>
        /// Method to determine if an export is parsable by this control
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <returns></returns>
        public abstract bool CanParse(ExportEntry exportEntry);

        /// <summary>
        /// Called when the control is being popped out. Allows subcontrol to updated UI to account for more space than when embedded in a tool
        /// </summary>
        public virtual void PoppedOut(ExportLoaderHostedWindow window) {}

        /// <summary>
        /// The list of supported games that this loader control can handle. Typically used by CanParse().
        /// </summary>
        private ExportEntry _currentLoadedExport;
        /// <summary>
        /// The currently loaded export, or null if none is currently loaded
        /// </summary>
        public virtual ExportEntry CurrentLoadedExport
        {
            get => _currentLoadedExport;
            protected set => SetProperty(ref _currentLoadedExport, value);
        }

        /// <summary>
        /// The currently loaded export's file, or null if none is currently loaded
        /// </summary>
        public IMEPackage Pcc => CurrentLoadedExport?.FileRef;

        /// <summary>
        /// Signals to the export loader that namelist is about to be modified
        /// and that anything selected that is bound to a namelist (typically IndexedName)
        /// should cached their current values until the change has completed
        /// and SignalNamelistChanged() fires.
        /// </summary>
        public virtual void SignalNamelistAboutToUpdate()
        {
        }

        /// <summary>
        /// Loads an export into this control and initializes the control
        /// </summary>
        /// <param name="exportEntry"></param>
        public abstract void LoadExport(ExportEntry exportEntry);

        /// <summary>
        /// Signals to the export loader control that the namelist has changed, and that
        /// anything that is bound to a list of names (typically IndexedName) should have its
        /// selected item restored.
        /// </summary>
        public virtual void SignalNamelistChanged()
        {
        }

        /// <summary>
        /// Unloads any loaded exports from this control and resets the control UI
        /// </summary>
        public abstract void UnloadExport();

        /// <summary>
        /// Creates a new ExportLoaderHostedWindow to pop open with a new instance of the export loader
        /// pointing to the current export
        /// </summary>
        public abstract void PopOut();

        /// <summary>
        /// Free resources for this control
        /// </summary>
        public abstract void Dispose();
    }
}