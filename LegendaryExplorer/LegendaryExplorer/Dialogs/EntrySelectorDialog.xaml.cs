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
    /// Interaction logic for EntrySelectorDialogWPF.xaml
    /// </summary>
    public partial class EntrySelector : NotifyPropertyChangedWindowBase, IDisposable
    {
        [Flags]
        public enum SupportedTypes
        {
            Exports = 1,
            Imports = 2,
            ExportsAndImports = 3
        }

        private IMEPackage Pcc;
        public ObservableCollectionExtended<object> AllEntriesList { get; } = new();

        /// <summary>
        /// Instantiates a EntrySelectorDialog WPF dialog
        /// </summary>
        /// <param name="owner">WPF owning window. Used for centering. Set to null if the calling window is not WPF based</param>
        /// <param name="pcc">Package file to load entries from</param>
        /// <param name="supportedInputTypes">Supported selection types</param>
        /// <param name="entryPredicate">A predicate to narrow the displayed entries</param>
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

        public ICommand OKCommand { get; set; }
        private void LoadCommands()
        {
            OKCommand = new GenericCommand(AcceptSelection, CanAcceptSelection);
        }

        private bool CanAcceptSelection()
        {
            return EntrySelectorComboBox.SelectedItem != null;
        }

        private void AcceptSelection()
        {
            DialogResult = true;
            ChosenEntry = EntrySelectorComboBox.SelectedItem as IEntry;
            ChoseRoot = EntrySelectorComboBox.SelectedItem is string;
            Dispose();
        }

        private IEntry ChosenEntry;
        private bool ChoseRoot;

        private readonly SupportedTypes SupportedInputTypes;

        private string DirectionsTextOverride;
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
        private bool disposedValue = false; // To detect redundant calls

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

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Dispose();
        }

        private void EntrySelector_ComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && OKCommand.CanExecute(null))
            {
                OKCommand.Execute(null);
            }
        }
    }
}
