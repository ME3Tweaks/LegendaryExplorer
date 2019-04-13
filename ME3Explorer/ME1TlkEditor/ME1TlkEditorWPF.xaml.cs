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

namespace ME3Explorer.ME1TlkEditor
{
    /// <summary>
    /// Interaction logic for ME1TlkEditorWPF.xaml
    /// </summary>
    public partial class ME1TlkEditorWPF : ExportLoaderControl
    {
        public ME1Explorer.Unreal.Classes.TalkFile.TLKStringRef[] StringRefs;
        public string newXml;
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
            RefreshTLKPanel(tlkFile);
        }

        public override void UnloadExport()
        {

        }

        public void RefreshTLKPanel(ME1Explorer.Unreal.Classes.TalkFile tlkObject)
        {


            LoadedStrings.Clear(); //reset
            //cycle through strings;
            for (int str_i = 1; str_i < tlkObject.StringRefs.Length; str_i++)
            {
                var iString = tlkObject.GetStringRefData(str_i);
                LoadedStrings.Add(iString);
            }
            DisplayedString_ListBox.Items.SortDescriptions.Clear(); //sort
            DisplayedString_ListBox.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("", System.ComponentModel.ListSortDirection.Ascending));

            var xmlTlk = tlkObject.TLKtoXmlstring();  // Convert to XML
            xmlBox.Text = xmlTlk; // write to XML box

        }

        private void Evt_EditOn(object sender, RoutedEventArgs e)
        {
            btnEdit.Content = "Cancel";
            btnEdit.ToolTip = "Cancel and revert to original";
            btnEdit.Click += Evt_EditOff;
            btnEdit.Click -= Evt_EditOn;
            btnCommit.IsEnabled = true;
            xmlBox.IsReadOnly = false;
            xmlBox.Background = Brushes.White;
            // TEMP
            LoadedStrings.Add("Edit On");
        }

        private void Evt_Commit(object sender, RoutedEventArgs e)
        {
            Evt_EditOff(sender, e);





            //TEMP
                LoadedStrings.Add("Committed");
        }

        private void Evt_EditOff(object sender, RoutedEventArgs e)
        {
            btnEdit.Content = "Edit";
            btnEdit.ToolTip = "Edit the TLK";
            btnEdit.Click += Evt_EditOn;
            btnEdit.Click -= Evt_EditOff;
            btnCommit.IsEnabled = false;
            xmlBox.IsReadOnly = true;
            xmlBox.Background = Brushes.LightGray;
            // TEMP
            LoadedStrings.Add("Edit Off");
        }

        private void XmlBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            newXml = xmlBox.Text; //Update current xml
        }

        private void DisplayedString_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ;
        }

    }
}
