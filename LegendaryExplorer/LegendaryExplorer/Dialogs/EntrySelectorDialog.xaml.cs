using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// A dialog for selecting entries (imports and/or exports) from a package file.
    /// Provides filtering capabilities and support for optional package root selection.
    /// </summary>
    public partial class EntrySelector : NotifyPropertyChangedWindowBase, IDisposable
    {
        /// <summary>
        /// Flags indicating which types of entries can be selected in the dialog.
        /// </summary>
        [Flags]
        public enum SupportedTypes
        {
            /// <summary>Only export entries can be selected.</summary>
            Exports = 1,
            /// <summary>Only import entries can be selected.</summary>
            Imports = 2,
            /// <summary>Both export and import entries can be selected.</summary>
            ExportsAndImports = 3
        }

        /// <summary>
        /// The package file from which entries are loaded.
        /// </summary>
        private IMEPackage Pcc;
        
        /// <summary>
        /// Gets the collection of all entries available for selection, filtered by the provided predicate.
        /// </summary>
        public ObservableCollectionExtended<object> AllEntriesList { get; } = new();

        /// <summary>
        /// Instantiates a EntrySelectorDialog WPF dialog
        /// </summary>
        /// <param name="owner">WPF owning window. Used for centering. Set to null if the calling window is not WPF based</param>
        /// <param name="pcc">Package file to load entries from</param>
        /// <param name="supportedInputTypes">Supported selection types</param>
        /// <param name="directionsText">Optional custom text to display as directions to the user</param>
        /// <param name="entryPredicate">A predicate to narrow the displayed entries</param>
        /// <param name="supportRootSelection">Whether to include a "[Package root]" option in the selection list</param>
        private EntrySelector(Window owner, IMEPackage pcc, SupportedTypes supportedInputTypes, string directionsText = null, Predicate<IEntry> entryPredicate = null, bool supportRootSelection = false)
        {
            this.Pcc = pcc;
            this.SupportedInputTypes = supportedInputTypes;
            this.DirectionsTextOverride = directionsText;

            var allEntriesBuilding = new List<object>();
            if (SupportedInputTypes.HasFlag(SupportedTypes.Imports))
            {
                for (int i = Pcc.Imports.Count - 1; i >= 0; i--)
                {
                    if (entryPredicate?.Invoke(Pcc.Imports[i]) ?? true)
                    {
                        allEntriesBuilding.Add(Pcc.Imports[i]);
                    }
                }
            }
            if (SupportedInputTypes.HasFlag(SupportedTypes.Exports))
            {
                foreach (ExportEntry exp in Pcc.Exports)
                {
                    if (entryPredicate?.Invoke(exp) ?? true)
                    {
                        allEntriesBuilding.Add(exp);
                    }
                }
            }

            if (supportRootSelection)
            {
                allEntriesBuilding.Insert(0, "[Package root]");
            }
            AllEntriesList.ReplaceAll(allEntriesBuilding);
            Owner = owner;
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            EntrySelectorComboBox.Focus();
        }

        /// <summary>
        /// Displays an entry selection dialog that includes a "[Package root]" option in addition to entries.
        /// </summary>
        /// <typeparam name="T">The type of entry to select (ExportEntry, ImportEntry, or IEntry)</typeparam>
        /// <param name="owner">WPF owning window for centering. Set to null if the calling window is not WPF based</param>
        /// <param name="pcc">Package file to load entries from</param>
        /// <param name="directionsText">Optional custom text to display as directions to the user</param>
        /// <param name="predicate">Optional predicate to filter the displayed entries</param>
        /// <returns>A tuple indicating whether the package root was selected and the selected entry (null if root was selected or dialog was cancelled)</returns>
        public static (bool selectedPackageRoot, T selectedEntry) GetEntryWithNoOption<T>(Window owner, IMEPackage pcc, string directionsText = null, Predicate<T> predicate = null) where T : class, IEntry
        {
            SupportedTypes supportedInputTypes = SupportedTypes.ExportsAndImports;
            if (typeof(T) == typeof(ExportEntry))
            {
                supportedInputTypes = SupportedTypes.Exports;
            }
            else if (typeof(T) == typeof(ImportEntry))
            {
                supportedInputTypes = SupportedTypes.Imports;
            }

            Predicate<IEntry> entryPredicate = null;
            if (predicate != null)
            {
                entryPredicate = entry => predicate((T)entry);
            }
            using var dlg = new EntrySelector(owner, pcc, supportedInputTypes, directionsText, entryPredicate, true);
            if (dlg.ShowDialog() == true)
            {
                return (dlg.ChoseRoot, dlg.ChosenEntry as T);
            }

            return (false,null); //No option was picked.
        }

        /// <summary>
        /// Displays an entry selection dialog and returns the selected entry.
        /// </summary>
        /// <typeparam name="T">The type of entry to select (ExportEntry, ImportEntry, or IEntry)</typeparam>
        /// <param name="owner">WPF owning window for centering. Set to null if the calling window is not WPF based</param>
        /// <param name="pcc">Package file to load entries from</param>
        /// <param name="directionsText">Optional custom text to display as directions to the user</param>
        /// <param name="predicate">Optional predicate to filter the displayed entries</param>
        /// <param name="defaultItem">Optional entry to pre-select in the dialog</param>
        /// <returns>The selected entry, or null if the dialog was cancelled</returns>
        public static T GetEntry<T>(Window owner, IMEPackage pcc, string directionsText = null, Predicate<T> predicate = null, IEntry defaultItem = null) where T : class, IEntry
        {
            SupportedTypes supportedInputTypes = SupportedTypes.ExportsAndImports;
            if (typeof(T) == typeof(ExportEntry))
            {
                supportedInputTypes = SupportedTypes.Exports;
            }
            else if (typeof(T) == typeof(ImportEntry))
            {
                supportedInputTypes = SupportedTypes.Imports;
            }

            Predicate<IEntry> entryPredicate = null;
            if (predicate != null)
            {
                entryPredicate = entry => predicate((T)entry);
            }
            using var dlg = new EntrySelector(owner, pcc, supportedInputTypes, directionsText, entryPredicate);
            if (defaultItem is not null)
            {
                dlg.EntrySelectorComboBox.SelectedItem = defaultItem;
            }
            if (dlg.ShowDialog() == true)
            {
                return dlg.ChosenEntry as T;
            }

            return null;
        }

        /// <summary>
        /// Gets the command for accepting the current selection.
        /// </summary>
        public ICommand OKCommand { get; set; }
        
        private void LoadCommands()
        {
            OKCommand = new GenericCommand(AcceptSelection, CanAcceptSelection);
        }

        /// <summary>
        /// Determines whether the current selection can be accepted.
        /// </summary>
        /// <returns>True if an item is selected; otherwise, false</returns>
        private bool CanAcceptSelection()
        {
            return EntrySelectorComboBox.SelectedItem != null;
        }

        /// <summary>
        /// Accepts the current selection and closes the dialog with a positive result.
        /// </summary>
        private void AcceptSelection()
        {
            DialogResult = true;
            ChosenEntry = EntrySelectorComboBox.SelectedItem as IEntry;
            ChoseRoot = EntrySelectorComboBox.SelectedItem is string;
            Dispose();
        }

        /// <summary>
        /// The entry that was selected by the user.
        /// </summary>
        private IEntry ChosenEntry;
        
        /// <summary>
        /// Indicates whether the user selected the "[Package root]" option.
        /// </summary>
        private bool ChoseRoot;

        /// <summary>
        /// The types of entries that can be selected in this dialog instance.
        /// </summary>
        private readonly SupportedTypes SupportedInputTypes;

        /// <summary>
        /// Custom directions text to display to the user, overriding the default.
        /// </summary>
        private string DirectionsTextOverride;
        
        /// <summary>
        /// Gets the directions text to display to the user based on the supported input types.
        /// </summary>
        public string DirectionsText
        {
            get
            {
                if (DirectionsTextOverride != null) return DirectionsTextOverride;
                switch (SupportedInputTypes)
                {
                    case SupportedTypes.Exports:
                        return "Select an export";
                    case SupportedTypes.Imports:
                        return "Select an import";
                    case SupportedTypes.ExportsAndImports:
                        return "Select an import or export";
                }
                return "Unknown input type selected";
            }
        }

        #region IDisposable Support
        /// <summary>
        /// Tracks whether this object has been disposed.
        /// </summary>
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Releases resources used by this dialog.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(); false if called from a finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    Pcc = null;
                }

                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        /// <summary>
        /// Releases all resources used by this dialog.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
        }

        /// <summary>
        /// Handles the Cancel button click event.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Dispose();
        }

        /// <summary>
        /// Handles key down events in the entry selector combo box, allowing Enter key to accept selection.
        /// </summary>
        private void EntrySelector_ComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && OKCommand.CanExecute(null))
            {
                OKCommand.Execute(null);
            }
        }
    }
}
