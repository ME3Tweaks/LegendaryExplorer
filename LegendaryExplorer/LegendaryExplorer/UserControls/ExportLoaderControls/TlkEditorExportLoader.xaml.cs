using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;
using System.Media;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using HuffmanCompression = LegendaryExplorerCore.TLK.ME1.HuffmanCompression;
using ME2ME3HuffmanCompression = LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for TLKEditor.xaml
    /// </summary>
    public partial class TLKEditorExportLoader : FileExportLoaderControl
    {
        private ME2ME3TalkFile _currentMe2Me3Me2Me3TalkFile;
        public List<TLKStringRef> LoadedStrings; //Loaded TLK
        public ObservableCollectionExtended<TLKStringRef> CleanedStrings { get; } = new(); // Displayed
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

        public TLKEditorExportLoader() : base("TLKEditor")
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }


        public ICommand SaveCommand { get; set; }
        public ICommand CommitCommand { get; set; }
        public ICommand SetIDCommand { get; set; }
        public ICommand ExportXmlCommand { get; set; }
        public ICommand ImportXmlCommand { get; set; }
        public ICommand ViewXmlCommand { get; set; }
        public ICommand DeleteStringCommand { get; set; }
        public ICommand SearchCommand { get; set; }
        public ICommand AddStringCommand { get; set; }


        private void LoadCommands()
        {
            SaveCommand = new RelayCommand(SaveString, CanSaveString);
            CommitCommand = new RelayCommand(CommitTLK, CanCommitTLK);
            SetIDCommand = new RelayCommand(SetStringID, StringIsSelected);
            DeleteStringCommand = new RelayCommand(DeleteString, StringIsSelected);

            SearchCommand = new GenericCommand(TextSearch, HasTLKLoaded);
            AddStringCommand = new GenericCommand(AddString, HasTLKLoaded);


            ExportXmlCommand = new GenericCommand(ExportToXml, HasTLKLoaded);
            ImportXmlCommand = new GenericCommand(ImportFromXml, HasTLKLoaded);
            ViewXmlCommand = new GenericCommand(ViewAsXml, HasTLKLoaded);

        }

        private void DeleteString(object obj)
        {
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;
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
                var elhw = new ExportLoaderHostedWindow(new TLKEditorExportLoader(), CurrentLoadedExport)
                {
                    Title = $"TLK Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
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
            var huff = new HuffmanCompression();
            huff.LoadInputData(LoadedStrings);
            huff.SerializeTalkfileToExport(CurrentLoadedExport);
            FileModified = false;
        }

        private void SaveString(object obj)
        {
            if (DisplayedString_ListBox.SelectedItem is TLKStringRef selectedItem)
            {
                selectedItem.Data = EditorString;
                FileModified = true;
            }
        }

        private string EditorString => editBox.Text.Trim().Replace("\r\n", "\n");

        private bool CanSaveString(object obj)
        {
            if (DisplayedString_ListBox == null) return false;
            var selectedItem = DisplayedString_ListBox.SelectedItem as TLKStringRef;
            return selectedItem?.Data != null && EditorString != selectedItem.Data;
        }

        //SirC "efficiency is next to godliness" way of Checking export is ME1/TLK
        public override bool CanParse(ExportEntry exportEntry) => exportEntry.FileRef.Game.IsGame1() && exportEntry.ClassName == "BioTlkFile" && !exportEntry.IsDefaultObject;
        public override void PoppedOut(ExportLoaderHostedWindow elhw)
        {

            //Recents_MenuItem = recentsMenuItem;
            //LoadRecentList();
            //RefreshRecent(false);
        }

        /// <summary>
        /// Memory cleanup when this control is unloaded
        /// </summary>
        public override void Dispose()
        {
            CurrentLoadedExport = null;
            _currentMe2Me3Me2Me3TalkFile = null;
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

        public bool HasTLKLoaded() => CurrentLoadedFile != null || CurrentLoadedExport != null;

        private void DisplayedString_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DisplayedString_ListBox.SelectedItem is TLKStringRef selectedItem)
            {
                editBox.Text = selectedItem.Data;
            }
            OnPropertyChanged(nameof(StringSelected)); //Propogate this change
        }

        public int DlgStringID(int curID) //Dialog tlkstring id
        {
            int newID;
            while (true)
            {
                var inst = new PromptDialog("Set new string ID", "TLK Editor", curID.ToString(), true)
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

        private void AddString()
        {
            var blankstringref = new TLKStringRef(100, "New Blank Line", 1);
            LoadedStrings.Add(blankstringref);
            CleanedStrings.Add(blankstringref);
            DisplayedString_ListBox.SelectedIndex = CleanedStrings.Count() - 1; //Set focus to new line (which is the last one)
            DisplayedString_ListBox.ScrollIntoView(DisplayedString_ListBox.SelectedItem); //Scroll to last item
            SetNewID();
            FileModified = true;
        }

        private void ExportToXml()
        {
            var fnameBase = CurrentLoadedExport?.ObjectName.Name;
            if (fnameBase == null && CurrentLoadedFile != null) fnameBase = Path.GetFileNameWithoutExtension(CurrentLoadedFile);
            if (fnameBase == null) fnameBase = "TalkFile";
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml",
                FileName = fnameBase + ".xml"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                if (CurrentLoadedExport != null)
                {
                    var talkfile = new ME1TalkFile(CurrentLoadedExport);
                    talkfile.SaveToXML(saveFileDialog.FileName);
                } 
                else if (_currentMe2Me3Me2Me3TalkFile is not null)
                {
                    if (FileModified)
                    {
                        _currentMe2Me3Me2Me3TalkFile.LoadTlkDataFromStream(ME2ME3HuffmanCompression.SaveToTlkStream(LoadedStrings).SeekBegin());
                    }
                    _currentMe2Me3Me2Me3TalkFile?.SaveToXML(saveFileDialog.FileName);
                }
            }

        }

        private void ImportFromXml()
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "XML Files (*.xml)|*.xml",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (openFileDialog.ShowDialog() == true)
            {
                if (CurrentLoadedExport is not null)
                {
                    var compressor = new HuffmanCompression();
                    compressor.LoadInputData(openFileDialog.FileName);
                    compressor.SerializeTalkfileToExport(CurrentLoadedExport);
                }
                else if (_currentMe2Me3Me2Me3TalkFile is not null)
                {
                    ME2ME3HuffmanCompression compressor = new ();
                    compressor.LoadInputData(openFileDialog.FileName);
                    _currentMe2Me3Me2Me3TalkFile.LoadTlkDataFromStream(compressor.SaveToStream().SeekBegin());
                    RefreshME2ME3TLK();
                }
                FileModified = true; //this is not always technically true, but we'll assume it is
            }
        }

        private void ViewAsXml()
        {
            if (!xmlUp)
            {
                string xmlString = "";
                if (CurrentLoadedExport is not null)
                {
                    xmlString = ME1TalkFile.TLKtoXmlstring(CurrentLoadedExport.InstancedFullPath, LoadedStrings);
                }
                else if (_currentMe2Me3Me2Me3TalkFile is not null)
                {
                    if (FileModified)
                    {
                        _currentMe2Me3Me2Me3TalkFile.LoadTlkDataFromStream(ME2ME3HuffmanCompression.SaveToTlkStream(LoadedStrings).SeekBegin());
                    }
                    xmlString = _currentMe2Me3Me2Me3TalkFile.WriteXMLString();
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
            if (DisplayedString_ListBox.SelectedItem is TLKStringRef selectedItem)
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

                if (node.StringID.ToString().Contains(searchTerm))
                {
                    //ID Search
                    DisplayedString_ListBox.SelectedIndex = curIndex;
                    return;
                }
                else if (node.Data != null && node.Data.ToLower().Contains(searchTerm))
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
            _currentMe2Me3Me2Me3TalkFile = new ME2ME3TalkFile(filepath);
            LoadedFile = filepath;
            RefreshME2ME3TLK();
            FileModified = false;
            Window.GetWindow(this).Title = "TLK Editor - " + filepath;
            OnFileLoaded(EventArgs.Empty);
        }

        private void RefreshME2ME3TLK()
        {
            LoadedStrings = _currentMe2Me3Me2Me3TalkFile.StringRefs.ToList(); //This is not bound to so reassigning is fine
            CleanedStrings.ReplaceAll(LoadedStrings.Where(x => x.StringID > 0).ToList()); //remove 0 or null strings.
            editBox.Text = NO_STRING_SELECTED; //Reset ability to save, reset edit box if export changed.
        }

        public void LoadFileFromStream(Stream stream)
        {
            UnloadExport();
            CurrentLoadedFile = null;
            _currentMe2Me3Me2Me3TalkFile = new ME2ME3TalkFile(stream);

            // Need way to load a file without having it show up in the recents

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
            var d = new OpenFileDialog
            {
                Title = "Open TLK file",
                Filter = "ME2/ME3/LE2/LE3 Talk Files|*.tlk",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
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
            else if (_currentMe2Me3Me2Me3TalkFile is not null)
            {
                if (CurrentLoadedFile is null)
                {
                    MessageBox.Show("Cannot save TLK File loaded from an SFAR. Use the Save As option to save your changes to a new file.");
                    return;
                }
                // CurrentME2ME3TalkFile.
                ME2ME3HuffmanCompression.SaveToTlkFile(_currentMe2Me3Me2Me3TalkFile.FilePath, LoadedStrings);
                _currentMe2Me3Me2Me3TalkFile = new ME2ME3TalkFile(CurrentLoadedFile);
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
            else if (_currentMe2Me3Me2Me3TalkFile is not null)
            {
                SaveFileDialog d = new() { Filter = "ME2/ME3/LE2/LE3 talk files|*.tlk" };
                if (d.ShowDialog() == true)
                {
                    // CurrentME2ME3TalkFile.
                    ME2ME3HuffmanCompression.SaveToTlkFile(d.FileName, LoadedStrings);
                }

            }
        }

        public override bool CanSave() => CurrentLoadedExport is not null || _currentMe2Me3Me2Me3TalkFile is not null;

        //internal override void RecentFile_click(object sender, RoutedEventArgs e)
        //{
        //    string s = ((FrameworkElement)sender).Tag.ToString();
        //    if (File.Exists(s))
        //    {
        //        LoadFile(s);
        //    }
        //    else
        //    {
        //        MessageBox.Show("File does not exist: " + s);
        //    }
        //}

        public override string Toolname => "TLKEditor";

        internal override bool CanLoadFileExtension(string extension)
        {
            switch (extension)
            {
                case ".sfm":
                case ".u":
                case ".upk":
                case ".pcc":
                case ".tlk":
                    return true;
                default:
                    return false;
            }
        }

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

        private void CloseViewAsXml(object sender, RoutedEventArgs e)
        {
            Evt_CloseXML(sender, e);
        }
    }
}