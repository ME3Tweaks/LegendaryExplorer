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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;

namespace ME3Explorer.ME1TlkEditor
{
    /// <summary>
    /// Interaction logic for ME1TlkEditorWPF.xaml
    /// </summary>
    public partial class ME1TlkEditorWPF : ExportLoaderControl
    {
        public ObservableCollectionExtended<string> LoadedStrings { get; } = new ObservableCollectionExtended<string>();

        public ME1TlkEditorWPF()
        {
            DataContext = this;
            InitializeComponent();
            LoadedStrings.Add("hello");
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return exportEntry.FileRef.Game == MEGame.ME1 && exportEntry.ClassName == "BioTlkFile";
        }

        public override void Dispose()
        {

        }

        public override void LoadExport(IExportEntry exportEntry)
        {

        }

        public override void UnloadExport()
        {

        }

        private void evt_EditOn(object sender, RoutedEventArgs e)
        {
            btnEdit.Content = "Cancel";
            btnEdit.ToolTip = "Cancel and revert to original";
            btnEdit.Click += evt_EditOff;
            btnEdit.Click -= evt_EditOn;
            btnCommit.IsEnabled = true;
            // TEMP
            LoadedStrings.Add("Edit On");
        }

        private void evt_Commit(object sender, RoutedEventArgs e)
        {
            evt_EditOff(sender, e);
            // TEMP
            LoadedStrings.Add("Committed");
        }

        private void evt_EditOff(object sender, RoutedEventArgs e)
        {
            btnEdit.Content = "Edit";
            btnEdit.ToolTip = "Edit the TLK";
            btnEdit.Click += evt_EditOn;
            btnEdit.Click -= evt_EditOff;
            btnCommit.IsEnabled = false;
            // TEMP
            LoadedStrings.Add("Edit Off");
        }
    }
}
