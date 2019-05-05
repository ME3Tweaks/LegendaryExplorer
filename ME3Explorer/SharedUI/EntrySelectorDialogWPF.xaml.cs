using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for EntrySelectorDialogWPF.xaml
    /// </summary>
    public partial class EntrySelector : NotifyPropertyChangedWindowBase, IDisposable
    {
        private IMEPackage Pcc;
        public ObservableCollectionExtended<object> AllEntriesList { get; } = new ObservableCollectionExtended<object>();

        /// <summary>
        /// Instantiates a EntrySelectorDialog WPF dialog
        /// </summary>
        /// <param name="owner">WPF owning window. Used for centering. Set to null if the calling window is not WPF based</param>
        /// <param name="pcc">Package file to load entries from</param>
        /// <param name="supportedInputTypes">Supported selection types</param>
        private EntrySelector(Window owner, IMEPackage pcc, SupportedTypes supportedInputTypes)
        {
            this.Pcc = pcc;
            this.SupportedInputTypes = supportedInputTypes;

            var allEntriesBuilding = new List<object>();
            if (SupportedInputTypes.HasFlag(SupportedTypes.Imports))
            {
                for (int i = Pcc.Imports.Count - 1; i >= 0; i--)
                {
                    allEntriesBuilding.Add(Pcc.Imports[i]);
                }
            }
            if (SupportedInputTypes.HasFlag(SupportedTypes.Exports))
            {
                foreach (IExportEntry exp in Pcc.Exports)
                {
                    allEntriesBuilding.Add(exp);
                }
            }
            AllEntriesList.ReplaceAll(allEntriesBuilding);
            Owner = owner;
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            EntrySelector_ComboBox.Focus();
        }

        public static IEntry GetEntry(Window owner, IMEPackage pcc, SupportedTypes supportedInputTypes)
        {
            var dlg = new EntrySelector(owner, pcc, supportedInputTypes);
            return dlg.ShowDialog() == true ? dlg.ChosenEntry : null;
        }

        public ICommand OKCommand { get; set; }
        private void LoadCommands()
        {
            OKCommand = new GenericCommand(AcceptSelection, CanAcceptSelection);
        }

        private bool CanAcceptSelection()
        {
            return EntrySelector_ComboBox.SelectedItem is IEntry;
        }

        private void AcceptSelection()
        {
            DialogResult = true;
            ChosenEntry = EntrySelector_ComboBox.SelectedItem as IEntry;
            Dispose();
        }

        private IEntry ChosenEntry;

        [Flags]
        public enum SupportedTypes
        {
            Exports = 1,
            Imports = 2,
            ExportsAndImports = 3
        }

        private readonly SupportedTypes SupportedInputTypes;

        public string DirectionsText
        {
            get
            {
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
