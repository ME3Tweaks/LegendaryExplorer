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
    public partial class EntrySelectorDialogWPF : NotifyPropertyChangedWindowBase, IDisposable
    {
        private IMEPackage Pcc;

        public EntrySelectorDialogWPF(IMEPackage pcc, SupportedTypes SupportedInputTypes)
        {
            this.Pcc = pcc;
            this.SupportedInputTypes = SupportedInputTypes;
            DataContext = this;
            InitializeComponent();
        }

        public IEntry ChosenEntry;

        [Flags]
        public enum SupportedTypes
        {
            Exports = 1,
            Imports = 2,
            ExportsAndImports = 3
        }

        private readonly SupportedTypes SupportedInputTypes = 0;

        public string DirectionsText
        {
            get
            {
                switch (SupportedInputTypes)
                {
                    case SupportedTypes.Exports:
                        return "Enter an export UIndex";
                    case SupportedTypes.Imports:
                        return "Enter an import UIndex";
                    case SupportedTypes.ExportsAndImports:
                        return "Enter an import or export UIndex";
                }
                return "Unknown input type selected";
            }
        }

        private string _entryPreviewText = "Enter a value";
        public string EntryPreviewText { get => _entryPreviewText; set => SetProperty(ref _entryPreviewText, value); }

        private void EntryUIndex_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (int.TryParse(EntryUIndex_TextBox.Text, out int uindex) && uindex != 0)
            {

                if ((SupportedInputTypes & SupportedTypes.Exports) != 0)
                {
                    if (Pcc.isUExport(uindex))
                    {
                        //Parse
                        IExportEntry destExport = Pcc.getUExport(uindex);
                        EntryPreviewText = $"{destExport.GetFullPath}_{destExport.indexValue} (EXPORT)";
                        OK_Button.IsEnabled = true;
                        return;
                    }
                    else if (uindex > Pcc.ExportCount)
                    {
                        EntryPreviewText = "Invalid export index (out of bounds)";
                    }
                }
                else if ((SupportedInputTypes & SupportedTypes.Imports) != 0)
                {
                    if (Pcc.isUImport(uindex))
                    {
                        //Parse
                        ImportEntry destImport = Pcc.getUImport(uindex);
                        EntryPreviewText = $"{destImport.GetFullPath}_{destImport.indexValue} (IMPORT)";
                        OK_Button.IsEnabled = true;
                        return;
                    }
                    else if (Math.Abs(uindex) > Pcc.ImportCount)
                    {
                        EntryPreviewText = "Invalid import index (out of bounds)";
                    }
                }
            }
            else
            {
                EntryPreviewText = "Invalid input";
            }
            OK_Button.IsEnabled = false;
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

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~EntrySelectorDialogWPF() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            ChosenEntry = Pcc.getEntry(int.Parse(EntryUIndex_TextBox.Text));
            Dispose();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Dispose();
        }
    }
}
