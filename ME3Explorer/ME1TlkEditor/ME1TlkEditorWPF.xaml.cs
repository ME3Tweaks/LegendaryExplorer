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
using System.IO;
using System.Xml;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME1Explorer.Unreal.Classes;
using TalkFile = ME1Explorer.Unreal.Classes.TalkFile;
using ME1Explorer;

namespace ME3Explorer.ME1TlkEditor
{
    /// <summary>
    /// Interaction logic for ME1TlkEditorWPF.xaml
    /// </summary>
    public partial class ME1TlkEditorWPF : ExportLoaderControl
    {
        public ME1Explorer.Unreal.Classes.TalkFile.TLKStringRef[] StringRefs;
        public ObservableCollectionExtended<string> LoadedStrings { get; } = new ObservableCollectionExtended<string>();

        public ME1TlkEditorWPF()
        {
            DataContext = this;
            InitializeComponent();
                                          
        }

        //SirC "efficiency is next to godliness" way of Checking export is ME1/TLK
        public override bool CanParse(IExportEntry exportEntry) => exportEntry.FileRef.Game == MEGame.ME1 && exportEntry.ClassName == "BioTlkFile";

        public override void Dispose()
        {

        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            var tlkFile = new ME1Explorer.Unreal.Classes.TalkFile(exportEntry); // Setup object as TalkFile


            //cycle through strings;
            for (int str_i = 1; str_i < tlkFile.StringRefs.Length; str_i++)
            {
            var iString = tlkFile.getStringRefData(str_i);
            LoadedStrings.Add(iString);
            }

            
            var xmlTlk = tlkFile.TLKtoXmlstring();  // Convert to XML
            xmlBox.Text = xmlTlk; // write to XML box

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

        private void XmlBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void DisplayedString_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
