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
    }
}
