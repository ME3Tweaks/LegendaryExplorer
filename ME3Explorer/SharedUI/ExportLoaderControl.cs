using ME3Explorer.Packages;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace ME3Explorer
{
    /// <summary>
    /// Subclass of UserControl that also sets up the CurrentLoadedExport, LoadExport(), UnloadExport() and more methods
    /// </summary>
    public abstract class ExportLoaderControl : NotifyPropertyChangedControlBase
    {
        /// <summary>
        /// Method to determine if an export is parsable by this control
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <returns></returns>
        public abstract bool CanParse(IExportEntry exportEntry);

        /// <summary>
        /// The list of supported games that this loader control can handle. Typically used by CanParse().
        /// </summary>
        private IExportEntry _currentLoadedExport;
        /// <summary>
        /// The currently loaded export, or null if none is currently loaded
        /// </summary>
        public IExportEntry CurrentLoadedExport
        {
            get => _currentLoadedExport;
            protected set => SetProperty(ref _currentLoadedExport, value);
        }

        /// <summary>
        /// Loads an export into this control and initializes the control
        /// </summary>
        /// <param name="exportEntry"></param>
        public abstract void LoadExport(IExportEntry exportEntry);

        /// <summary>
        /// Unloads any loaded exports from this control and resets the control UI
        /// </summary>
        public abstract void UnloadExport();
    }
}