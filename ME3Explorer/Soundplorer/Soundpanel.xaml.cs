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
using ME3Explorer.Unreal.Classes;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for Soundpanel.xaml
    /// </summary>
    public partial class Soundpanel : ExportLoaderControl
    {
        new MEGame[] SupportedGames = new MEGame[] { MEGame.ME3 };

        public Soundpanel()
        {
            InitializeComponent();
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            //throw new NotImplementedException();
            WwiseStream w = new WwiseStream(exportEntry.FileRef as ME3Package, exportEntry.Index);
            string s = "#" + exportEntry.Index + " WwiseStream : " + exportEntry.ObjectName + "\n\n";
            s += "Filename : \"" + w.FileName + "\"\n";
            s += "Data size: " + w.DataSize + " bytes\n";
            s += "Data offset: 0x" + w.DataOffset.ToString("X8") + "\n";
            s += "ID: 0x" + w.Id.ToString("X8") + " = " + w.Id + "\n";
            Temp.Text = s;
            CurrentLoadedExport = exportEntry;
        }

        public override void UnloadExport()
        {
            //throw new NotImplementedException();
            CurrentLoadedExport = null;
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return (exportEntry.FileRef.Game == MEGame.ME3 && (exportEntry.ClassName == "WwiseBank" || exportEntry.ClassName == "WwiseStream"));
        }
    }
}
