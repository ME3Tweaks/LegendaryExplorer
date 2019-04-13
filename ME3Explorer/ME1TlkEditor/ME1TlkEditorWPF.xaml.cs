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
        public ObservableCollectionExtended<ME1Explorer.Unreal.Classes.TalkFile.TLKStringRef> LoadedStrings { get; } = new ObservableCollectionExtended<ME1Explorer.Unreal.Classes.TalkFile.TLKStringRef>();

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
            LoadedStrings.ClearEx(); //clear strings Ex does this in bulk (faster)
            LoadedStrings.AddRange(tlkFile.StringRefs);

            //RefreshTLKPanel(tlkFile);
        }

        public override void UnloadExport()
        {

        }

        //public void RefreshTLKPanel(ME1Explorer.Unreal.Classes.TalkFile tlkObject)
        //{


        //    LoadedStrings.Clear(); //reset
        //    //cycle through strings;
        //    for (int str_i = 1; str_i < tlkObject.StringRefs.Length; str_i++)
        //    {
        //        var iString = tlkObject.GetStringRefData(str_i);
        //        {
        //            LoadedStrings.Add(iString);
        //        }
        //    }
        //    DisplayedString_ListBox.Items.SortDescriptions.Clear(); //sort
        //    DisplayedString_ListBox.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("", System.ComponentModel.ListSortDirection.Ascending));

        //    var xmlTlk = tlkObject.TLKtoXmlstring();  // Convert to XML
        //    xmlBox.Text = xmlTlk; // write to XML box

        //}

        private void Evt_Commit(object sender, RoutedEventArgs e)
        {
 
        }

        private void DisplayedString_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as ME1Explorer.Unreal.Classes.TalkFile.TLKStringRef;

            if (selectedItem != null)
            {
                xmlBox.Text = selectedItem.Data;
            }
        }

        private void SaveButton_Clicked(object sender, RoutedEventArgs e)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as ME1Explorer.Unreal.Classes.TalkFile.TLKStringRef;

            if (selectedItem != null)
            {
                selectedItem.Data = xmlBox.Text;
            }
        }
    }
}
