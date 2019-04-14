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
using static ME1Explorer.Unreal.Classes.TalkFile;

namespace ME3Explorer.ME1TlkEditor
{
    /// <summary>
    /// Interaction logic for ME1TlkEditorWPF.xaml
    /// </summary>
    public partial class ME1TlkEditorWPF : ExportLoaderControl
    {
        public TLKStringRef[] StringRefs;
        public string newXml;
        public List<TLKStringRef> LoadedStrings; //Loaded TLK
        public ObservableCollectionExtended<TLKStringRef> CleanedStrings { get; } = new ObservableCollectionExtended<TLKStringRef>(); // Displayed

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
            LoadedStrings = tlkFile.StringRefs.ToList(); //This is not binded to so reassigning is fine
            CleanedStrings.ClearEx(); //clear strings Ex does this in bulk (faster)
            CleanedStrings.AddRange(LoadedStrings.Where(x => x.StringID > 0).ToList()); //nest it remove 0 strings.
        }

        public override void UnloadExport()
        {

        }


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
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;

            if (selectedItem != null)
            {
                selectedItem.Data = xmlBox.Text;
            }
        }

        private void Evt_SetID(object sender, RoutedEventArgs e)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;

            //Run popout box to set tlkstring id
        }

        private void Evt_AddString(object sender, RoutedEventArgs e)
        {
            //CleanedStrings.Add(TLKStringRef(0,0,1,"NewData"))
        }

        private void Evt_DeleteString(object sender, RoutedEventArgs e)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;
            CleanedStrings.Remove(selectedItem);
        }

        private void Evt_ExportXML(object sender, RoutedEventArgs e)
        {

        }

        private void Evt_ImportXML(object sender, RoutedEventArgs e)
        {

        }
    }
}
