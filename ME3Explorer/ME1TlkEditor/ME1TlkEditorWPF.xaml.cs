using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Xml;
using ME3Explorer.SharedUI;
using Microsoft.Win32;
using System.Media;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.TLK.ME1;
using TalkFile = ME3ExplorerCore.TLK.ME2ME3.TalkFile;
using ME2ME3HuffmanCompression = ME3ExplorerCore.TLK.ME2ME3.HuffmanCompression;

namespace ME3Explorer.ME1TlkEditor
{
    /// <summary>
    /// Interaction logic for ME1TlkEditorWPF.xaml
    /// </summary>
    public partial class ME1TlkEditorWPF : FileExportLoaderControl
    {
        private TalkFile CurrentME2ME3TalkFile;
        public List<ME1TalkFile.TLKStringRef> LoadedStrings; //Loaded TLK
        public ObservableCollectionExtended<ME1TalkFile.TLKStringRef> CleanedStrings { get; } = new(); // Displayed
        private bool xmlUp;

        private const string NO_STRING_SELECTED = "No string selected";

        public bool StringSelected
        {
            get
            {
                if (DisplayedString_ListBox == null) return false;
                return DisplayedString_ListBox.SelectedIndex >= 0;
            }
        }

        public ME1TlkEditorWPF() : base("TLKEditor")
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }


        public ICommand SaveCommand { get; set; }
        public ICommand CommitCommand { get; set; }
        public ICommand SetIDCommand { get; set; }
        public ICommand DeleteStringCommand { get; set; }


        private void LoadCommands()
        {
            SaveCommand = new RelayCommand(SaveString, CanSaveString);
            CommitCommand = new RelayCommand(CommitTLK, CanCommitTLK);
            SetIDCommand = new RelayCommand(SetStringID, StringIsSelected);
            DeleteStringCommand = new RelayCommand(DeleteString, StringIsSelected);
        }

        private void DeleteString(object obj)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as ME1TalkFile.TLKStringRef;
            CleanedStrings.Remove(selectedItem);
            LoadedStrings.Remove(selectedItem);
            FileModified = true;
        }

        private void SetStringID(object obj)
        {
            SetNewID();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new ME1TlkEditorWPF(), CurrentLoadedExport);
                elhw.Title = $"TLK Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}";
                elhw.Show();
            }
        }

        private bool StringIsSelected(object obj)
        {
            return StringSelected;
        }

        private bool CanCommitTLK(object obj)
        {
            return FileModified;
        }

        private void CommitTLK(object obj)
        {
            HuffmanCompression huff = new HuffmanCompression();
            huff.LoadInputData(LoadedStrings);
            huff.serializeTLKStrListToExport(CurrentLoadedExport);
            FileModified = false;
        }

        private void SaveString(object obj)
        {
            if (DisplayedString_ListBox.SelectedItem is ME1TalkFile.TLKStringRef selectedItem)
            {
                selectedItem.Data = editBox.Text.Trim();
                FileModified = true;
            }
        }

        private bool CanSaveString(object obj)
        {
            if (DisplayedString_ListBox == null) return false;
            var selectedItem = DisplayedString_ListBox.SelectedItem as ME1TalkFile.TLKStringRef;
            return selectedItem?.Data != null && editBox.Text.Trim() != selectedItem.Data;
        }

        //SirC "efficiency is next to godliness" way of Checking export is ME1/TLK
        public override bool CanParse(ExportEntry exportEntry) => exportEntry.FileRef.Game == MEGame.ME1 && exportEntry.ClassName == "BioTlkFile";
        public override void PoppedOut(MenuItem recentsMenuItem)
        {
            Recents_MenuItem = recentsMenuItem;
            LoadRecentList();
            RefreshRecent(false, null);
        }

        /// <summary>
        /// Memory cleanup when this control is unloaded
        /// </summary>
        public override void Dispose()
        {
            CurrentLoadedExport = null;
            CurrentME2ME3TalkFile = null;
            LoadedStrings?.Clear();
            CleanedStrings?.ClearEx();
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedFile = null;
            var tlkFile = new ME1TalkFile(exportEntry); // Setup object as TalkFile
            LoadedStrings = tlkFile.StringRefs.ToList(); //This is not binded to so reassigning is fine
            CleanedStrings.ClearEx(); //clear strings Ex does this in bulk (faster)
            CleanedStrings.AddRange(LoadedStrings.Where(x => x.StringID > 0).ToList()); //nest it remove 0 strings.
            CurrentLoadedExport = exportEntry;
            editBox.Text = NO_STRING_SELECTED; //Reset ability to save, reset edit box if export changed.
            FileModified = false;
        }

        public string CurrentLoadedFile { get; set; }

        public override void UnloadExport()
        {
            FileModified = false;
        }

        private void DisplayedString_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DisplayedString_ListBox.SelectedItem is ME1TalkFile.TLKStringRef selectedItem)
            {
                editBox.Text = selectedItem.Data;
            }
            OnPropertyChanged(nameof(StringSelected)); //Propogate this change
        }

        public int DlgStringID(int curID) //Dialog tlkstring id
        {
            var newID = 0;
            while (true)
            {
                PromptDialog inst = new PromptDialog("Set new string ID", "TLK Editor", curID.ToString(), true)
                {
                    Owner = Window.GetWindow(this)
                };
                //center to parent
                if (inst.ShowDialog() == true)
                {
                    if (int.TryParse(inst.ResponseText, out int newIDInt) &&
                        newIDInt > 0) //test result is an acceptable input
                    { 
                        if (LoadedStrings.Any(x => x.StringID == newIDInt))
                        {
                            MessageBox.Show($"String ID must be unique.\n{newIDInt} is currently in use in this TLK.");
                            continue;
                        }

                        newID = newIDInt;
                        break;
                    }

                    MessageBox.Show("String ID must be a positive integer");
                }
                else
                {
                    return curID; //cancel
                }
            }
            return newID;
        }

        private void Evt_AddString(object sender, RoutedEventArgs e)
        {
            var blankstringref = new ME1TalkFile.TLKStringRef(100, 1, "New Blank Line");
            LoadedStrings.Add(blankstringref);
            CleanedStrings.Add(blankstringref);
            DisplayedString_ListBox.SelectedIndex = CleanedStrings.Count() - 1; //Set focus to new line (which is the last one)
            DisplayedString_ListBox.ScrollIntoView(DisplayedString_ListBox.SelectedItem); //Scroll to last item
            SetNewID();
            FileModified = true;
        }

        private void Evt_ExportXML(object sender, RoutedEventArgs e)
        {
            var fnameBase = CurrentLoadedExport?.ObjectName.Name;
            if (fnameBase == null && CurrentLoadedFile != null) fnameBase = Path.GetFileNameWithoutExtension(CurrentLoadedFile);
            if (fnameBase == null) fnameBase = "TalkFile";
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml",
                FileName = fnameBase + ".xml"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                if (CurrentLoadedExport != null)
                {
                    ME1TalkFile talkfile = new ME1TalkFile(CurrentLoadedExport);
                    talkfile.saveToFile(saveFileDialog.FileName);
                } 
                else if (CurrentME2ME3TalkFile is not null)
                {
                    if (FileModified)
                    {
                        CurrentME2ME3TalkFile.LoadTlkDataFromStream(ME2ME3HuffmanCompression.SaveToTlkStream(LoadedStrings).SeekBegin());
                    }
                    CurrentME2ME3TalkFile?.DumpToFile(saveFileDialog.FileName);
                }
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
                if (CurrentLoadedExport is not null)
                {
                    HuffmanCompression compressor = new HuffmanCompression();
                    compressor.LoadInputData(openFileDialog.FileName);
                    compressor.serializeTLKStrListToExport(CurrentLoadedExport);
                }
                else if (CurrentME2ME3TalkFile is not null)
                {
                    ME2ME3HuffmanCompression compressor = new ();
                    compressor.LoadInputData(openFileDialog.FileName);
                    CurrentME2ME3TalkFile.LoadTlkDataFromStream(compressor.SaveToStream().SeekBegin());
                    RefreshME2ME3TLK();
                }
                FileModified = true; //this is not always technically true, but we'll assume it is
            }
        }

        private void Evt_ViewXML(object sender, RoutedEventArgs e)
        {
            if (!xmlUp)
            {
                string xmlString = "";
                if (CurrentLoadedExport is not null)
                {
                    xmlString = ME1TalkFile.TLKtoXmlstring(CurrentLoadedExport.InstancedFullPath, LoadedStrings);
                }
                else if (CurrentME2ME3TalkFile is not null)
                {
                    if (FileModified)
                    {
                        CurrentME2ME3TalkFile.LoadTlkDataFromStream(ME2ME3HuffmanCompression.SaveToTlkStream(LoadedStrings).SeekBegin());
                    }
                    xmlString = CurrentME2ME3TalkFile.WriteXMLString();
                }
                popoutXmlBox.Text = xmlString;

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
            if (DisplayedString_ListBox.SelectedItem is ME1TalkFile.TLKStringRef selectedItem)
            {

                var stringRefNewID = DlgStringID(selectedItem.StringID); //Run popout box to set tlkstring id
                if (selectedItem.StringID != stringRefNewID)
                {
                    selectedItem.StringID = stringRefNewID;
                    FileModified = true;
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
                ME1TalkFile.TLKStringRef node = CleanedStrings[curIndex];

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

        public override void LoadFile(string filepath)
        {
            UnloadExport();
            CurrentLoadedFile = filepath;
            CurrentME2ME3TalkFile = new TalkFile();
            CurrentME2ME3TalkFile.LoadTlkData(filepath);

            RefreshME2ME3TLK();
            FileModified = false;

            AddRecent(filepath, false);
            SaveRecentList();
            RefreshRecent(true, RFiles);
            Window.GetWindow(this).Title = "TLK Editor - " + filepath;
        }

        private void RefreshME2ME3TLK()
        {
            LoadedStrings = CurrentME2ME3TalkFile.StringRefs.ToList(); //This is not bound to so reassigning is fine
            CleanedStrings.ReplaceAll(LoadedStrings.Where(x => x.StringID > 0).ToList()); //remove 0 or null strings.
            editBox.Text = NO_STRING_SELECTED; //Reset ability to save, reset edit box if export changed.
        }

        public void LoadFileFromStream(Stream stream)
        {
            UnloadExport();
            //CurrentLoadedFile = filepath;
            CurrentME2ME3TalkFile = new TalkFile();
            CurrentME2ME3TalkFile.LoadTlkDataFromStream(stream);

            RefreshME2ME3TLK();
            FileModified = false;
        }

        public override bool CanLoadFile()
        {
            //this doesn't do any background threading so we can always load files
            return true;
        }

        internal override void OpenFile()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "All TLK Editor supported files|*.sfm;*.u;*.upk;*.tlk|ME1 Package Files|*.sfm;*.u;*.upk|ME2/ME3 Talk Files|*.tlk" };
            if (d.ShowDialog() == true)
            {
#if !DEBUG
                try
                {
#endif
                LoadFile(d.FileName);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }

        public override void Save()
        {
            if (CurrentLoadedExport != null)
            {
                CurrentLoadedExport.FileRef.Save();
            }
            else if (CurrentME2ME3TalkFile != null)
            {
                // CurrentME2ME3TalkFile.
                ME2ME3HuffmanCompression.SaveToTlkFile(CurrentME2ME3TalkFile.path, LoadedStrings);

                FileModified = false; //you can only commit to file, not to export and then file in file mode.
            }
            //throw new NotImplementedException();

        }

        public override void SaveAs()
        {
            if (CurrentLoadedExport != null)
            {
                SaveFileDialog d = new() { Filter = $"*{Path.GetExtension(CurrentLoadedExport.FileRef.FilePath)}|*{Path.GetExtension(CurrentLoadedExport.FileRef.FilePath)}" };
                if (d.ShowDialog() == true)
                {
                    CurrentLoadedExport.FileRef.Save(d.FileName);
                }
            }
            else if (CurrentME2ME3TalkFile != null)
            {
                SaveFileDialog d = new() { Filter = $"ME2/ME3 talk files|*.tlk" };
                if (d.ShowDialog() == true)
                {
                    // CurrentME2ME3TalkFile.
                    ME2ME3HuffmanCompression.SaveToTlkFile(d.FileName, LoadedStrings);
                }

            }
        }

        public override bool CanSave()
        {
            return true;
        }

        internal override void RecentFile_click(object sender, RoutedEventArgs e)
        {
            string s = ((FrameworkElement)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
            }
        }

        internal override bool CanLoadFileExtension(string extension)
        {
            switch (extension)
            {
                case ".sfm":
                case ".u":
                case ".upk":
                case ".tlk":
                    return true;
                default:
                    return false;
            }
        }

        internal override string DataFolder { get; } = "TLKEditorWPF";

        private void TlkStrField_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
            }

            if (CanSaveString(null))
            {
                SaveString(null);
            }
        }
    }
}