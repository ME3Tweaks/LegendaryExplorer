using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Microsoft.Win32;
using System.Threading;
using System.Media;

namespace ME3Explorer.ME1TlkEditor
{
    /// <summary>
    /// Interaction logic for ME1TlkEditorWPF.xaml
    /// </summary>
    public partial class ME1TlkEditorWPF : ExportLoaderControl
    {
        public TLKStringRef[] StringRefs;
        public List<TLKStringRef> LoadedStrings; //Loaded TLK
        public ObservableCollectionExtended<TLKStringRef> CleanedStrings { get; } = new ObservableCollectionExtended<TLKStringRef>(); // Displayed
        private bool xmlUp;

        private static string NO_STRING_SELECTED = "No string selected";

        public bool StringSelected
        {
            get
            {
                if (DisplayedString_ListBox == null) return false;
                return DisplayedString_ListBox.SelectedIndex >= 0;
            }
        }

        public ME1TlkEditorWPF()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }


        public ICommand SaveCommand { get; set; }
        public ICommand CommitCommand { get; set; }
        public ICommand SetIDCommand { get; set; }
        public ICommand DeleteStringCommand { get; set; }
        public bool hasPendingChanges { get; private set; }

        private void LoadCommands()
        {
            SaveCommand = new RelayCommand(SaveString, CanSaveString);
            CommitCommand = new RelayCommand(CommitTLK, CanCommitTLK);
            SetIDCommand = new RelayCommand(SetStringID, StringIsSelected);
            DeleteStringCommand = new RelayCommand(DeleteString, StringIsSelected);
        }

        private void DeleteString(object obj)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;
            CleanedStrings.Remove(selectedItem);
            LoadedStrings.Remove(selectedItem);
            hasPendingChanges = true;
        }

        private void SetStringID(object obj)
        {
            SetNewID();
        }

        private bool StringIsSelected(object obj)
        {
            return StringSelected;
        }

        private bool CanCommitTLK(object obj)
        {
            return hasPendingChanges;
        }

        private void CommitTLK(object obj)
        {
            ME1Explorer.HuffmanCompression huff = new ME1Explorer.HuffmanCompression();
            huff.LoadInputData(LoadedStrings);
            huff.serializeTLKStrListToExport(CurrentLoadedExport);
            hasPendingChanges = false;
        }

        private void SaveString(object obj)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;
            if (selectedItem != null)
            {
                selectedItem.Data = editBox.Text.Trim();
                hasPendingChanges = true;
            }
        }

        private bool CanSaveString(object obj)
        {
            if (DisplayedString_ListBox == null) return false;
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;
            return selectedItem != null && selectedItem.Data != null && editBox.Text.Trim() != selectedItem.Data;
        }

        //SirC "efficiency is next to godliness" way of Checking export is ME1/TLK
        public override bool CanParse(IExportEntry exportEntry) => exportEntry.FileRef.Game == MEGame.ME1 && exportEntry.ClassName == "BioTlkFile";

        /// <summary>
        /// Memory cleanup when this control is unloaded
        /// </summary>
        public override void Dispose()
        {
            CurrentLoadedExport = null;
            LoadedStrings.Clear();
            CleanedStrings.ClearEx();
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            var tlkFile = new ME1Explorer.Unreal.Classes.TalkFile(exportEntry); // Setup object as TalkFile
            LoadedStrings = tlkFile.StringRefs.ToList(); //This is not binded to so reassigning is fine
            CleanedStrings.ClearEx(); //clear strings Ex does this in bulk (faster)
            CleanedStrings.AddRange(LoadedStrings.Where(x => x.StringID > 0).ToList()); //nest it remove 0 strings.
            CurrentLoadedExport = exportEntry;
            editBox.Text = NO_STRING_SELECTED; //Reset ability to save, reset edit box if export changed.
            hasPendingChanges = false;

        }

        public override void UnloadExport()
        {
            hasPendingChanges = false;

        }

        private void DisplayedString_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;

            if (selectedItem != null)
            {
                editBox.Text = selectedItem.Data;
            }
            OnPropertyChanged(nameof(StringSelected)); //Propogate this change
        }

        public int DlgStringID(int curID) //Dialog tlkstring id
        {
            var newID = 0;
            bool isValid = false;
            while (!isValid)
            {
                PromptDialog inst = new PromptDialog("Set new string ID", "TLK Editor", curID.ToString(), true);
                inst.Owner = Window.GetWindow(this); //center to parent
                if (inst.ShowDialog().Value)
                {
                    if (int.TryParse(inst.ResponseText, out int newIDInt))
                    {
                        //test result is an acceptable input
                        if (newIDInt > 0)
                        {
                            if (!LoadedStrings.Any(x => x.StringID == newIDInt))
                            {
                                isValid = true;
                                newID = newIDInt;
                                break;
                            }
                            else
                            {
                                MessageBox.Show($"String ID must be unique.\n{newIDInt} is currently in use in this TLK.");
                                continue;
                            }
                        }
                    }

                    MessageBox.Show("String ID must be a positive integer");
                }
                else
                {
                    return curID; //cancel
                }
            }

            if (isValid)
            {
                return newID;
            }
            return curID;
        }

        private void Evt_AddString(object sender, RoutedEventArgs e)
        {
            var blankstringref = new TLKStringRef(100, 1, "New Blank Line");
            LoadedStrings.Add(blankstringref);
            CleanedStrings.Add(blankstringref);
            DisplayedString_ListBox.SelectedIndex = CleanedStrings.Count() - 1; //Set focus to new line (which is the last one)
            DisplayedString_ListBox.ScrollIntoView(DisplayedString_ListBox.SelectedItem); //Scroll to last item
            SetNewID();
            hasPendingChanges = true;
        }

        private void Evt_ExportXML(object sender, RoutedEventArgs e)
        {

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml",
                FileName = CurrentLoadedExport.ObjectName + ".xml"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                ME1Explorer.Unreal.Classes.TalkFile talkfile = new ME1Explorer.Unreal.Classes.TalkFile(CurrentLoadedExport);
                talkfile.saveToFile(saveFileDialog.FileName);
            }

        }

        private void Evt_ImportXML(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "XML Files (*.xml)|*.xml"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ME1Explorer.HuffmanCompression compressor = new ME1Explorer.HuffmanCompression();
                compressor.LoadInputData(openFileDialog.FileName);
                compressor.serializeTLKStrListToExport(CurrentLoadedExport);
                hasPendingChanges = true; //this is not always technically true, but we'll assume it is
            }
        }

        private void Evt_ViewXML(object sender, RoutedEventArgs e)
        {
            if (!xmlUp)
            {
                StringBuilder xmlTLK = new StringBuilder();
                using (StringWriter stringWriter = new StringWriter(xmlTLK))
                {
                    using (XmlTextWriter writer = new XmlTextWriter(stringWriter))
                    {
                        writer.Formatting = Formatting.Indented;
                        writer.Indentation = 4;

                        writer.WriteStartDocument();
                        writer.WriteStartElement("tlkFile");
                        writer.WriteAttributeString("Name", CurrentLoadedExport.PackageFullName + "_" + CurrentLoadedExport.ObjectName);

                        for (int i = 0; i < LoadedStrings.Count; i++)
                        {
                            writer.WriteStartElement("string");
                            writer.WriteStartElement("id");
                            writer.WriteValue(LoadedStrings[i].StringID);
                            writer.WriteEndElement(); // </id>
                            writer.WriteStartElement("flags");
                            writer.WriteValue(LoadedStrings[i].Flags);
                            writer.WriteEndElement(); // </flags>
                            if (LoadedStrings[i].Flags != 1)
                                writer.WriteElementString("data", "-1");
                            else
                                writer.WriteElementString("data", LoadedStrings[i].Data);
                            writer.WriteEndElement(); // </string>
                        }
                        writer.WriteEndElement(); // </tlkFile>
                    }
                }
                popoutXmlBox.Text = xmlTLK.ToString();

                popupDlg.Height = LowerDock.ActualHeight + DisplayedString_ListBox.ActualHeight;
                popupDlg.Width = DisplayedString_ListBox.ActualWidth;
                btnViewXML.ToolTip = "Close XML View.";
                popupDlg.IsOpen = true;
                xmlUp = true;
            }
        }

        private async void Evt_CloseXML(object sender, EventArgs e)
        {
            await System.Threading.Tasks.Task.Delay(100);  //Catch double clicks of XML button 
            xmlUp = false;
            btnViewXML.ToolTip = "View as XML.";

        }

        private void SetNewID()
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;
            if (selectedItem != null)
            {

                var stringRefNewID = DlgStringID(selectedItem.StringID); //Run popout box to set tlkstring id
                if (selectedItem.StringID != stringRefNewID)
                {
                    selectedItem.StringID = stringRefNewID;
                    hasPendingChanges = true;
                }
            }
        }

        private void Evt_KeyUp(object sender, KeyEventArgs k)
        {
            if (k.Key == Key.Return)
            {
                TextSearch();
            }
        }

        private void Evt_Search(object sender, RoutedEventArgs e)
        {
            TextSearch();
        }

        private void TextSearch()
        {
            string searchTerm = boxSearch.Text.Trim().ToLower();
            if (searchTerm == "") return; //don't search blank

            int pos = DisplayedString_ListBox.SelectedIndex;
            pos += 1; //search this and 1 forward
            for (int i = 0; i < CleanedStrings.Count; i++)
            {
                int curIndex = (i + pos) % CleanedStrings.Count;
                TLKStringRef node = CleanedStrings[curIndex];

                if (CleanedStrings[curIndex].StringID.ToString().Contains(searchTerm))
                {
                    //ID Search
                    DisplayedString_ListBox.SelectedIndex = curIndex;
                    return;
                }
                else if (CleanedStrings[curIndex].Data != null && CleanedStrings[curIndex].Data.ToLower().Contains(searchTerm))
                {
                    DisplayedString_ListBox.SelectedIndex = curIndex;
                    DisplayedString_ListBox.ScrollIntoView(DisplayedString_ListBox.SelectedItem);
                    return;
                }
            }
            //Not found
            SystemSounds.Beep.Play();
        }
    }
}