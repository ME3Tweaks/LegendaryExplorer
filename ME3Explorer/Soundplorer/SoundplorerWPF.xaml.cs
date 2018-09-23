using ByteSizeLib;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ME3Explorer.Soundplorer
{
    /// <summary>
    /// Interaction logic for SoundplorerWPF.xaml
    /// </summary>
    public partial class SoundplorerWPF : WPFBase, INotifyPropertyChanged
    {
        public static readonly string SoundplorerDataFolder = System.IO.Path.Combine(App.AppDataFolder, @"Soundplorer\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;

        BackgroundWorker backgroundScanner;
        public List<SoundplorerExport> BindedExportsList { get; set; }
        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
        }

        private string _busyText;
        public string BusyText
        {
            get { return _busyText; }
            set { if (_busyText != value) { _busyText = value; OnPropertyChanged(); } }
        }
        public SoundplorerWPF()
        {
            InitializeComponent();

            LoadRecentList();
            RefreshRecent(false);
        }

        private void LoadRecentList()
        {
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = SoundplorerDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(SoundplorerDataFolder))
            {
                Directory.CreateDirectory(SoundplorerDataFolder);
            }
            string path = SoundplorerDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of packed

                //This code can be removed when non-WPF package editor is removed.
                var forms = System.Windows.Forms.Application.OpenForms;
                foreach (System.Windows.Forms.Form form in forms)
                {
                    if (form is PackageEditor) //it will never be "this"
                    {
                        ((PackageEditor)form).RefreshRecent(false, RFiles);
                    }
                }
                foreach (var form in App.Current.Windows)
                {
                    if (form is PackageEditorWPF && this != form)
                    {
                        ((PackageEditorWPF)form).RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }
            Recents_MenuItem.IsEnabled = true;


            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((MenuItem)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
            }
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            Recents_MenuItem.IsEnabled = true;
        }


        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "*.pcc|*.pcc" };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                try
                {
                    LoadFile(d.FileName);
                    AddRecent(d.FileName, false);
                    SaveRecentList();
                    RefreshRecent(true, RFiles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }

        private void LoadFile(string fileName)
        {
            try
            {
                StatusBar_GameID_Container.Visibility = Visibility.Collapsed;
                StatusBar_LeftMostText.Text = "Loading " + System.IO.Path.GetFileName(fileName) + " (" + ByteSize.FromBytes(new System.IO.FileInfo(fileName).Length) + ")";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadMEPackage(fileName);
                StatusBar_GameID_Container.Visibility = Visibility.Visible;

                switch (Pcc.Game)
                {
                    case MEGame.ME1:
                        StatusBar_GameID_Text.Text = "ME1";
                        StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                        break;
                    case MEGame.ME2:
                        StatusBar_GameID_Text.Text = "ME2";
                        StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Maroon);
                        break;
                    case MEGame.ME3:
                        StatusBar_GameID_Text.Text = "ME3";
                        StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkSeaGreen);
                        break;
                    case MEGame.UDK:
                        StatusBar_GameID_Text.Text = "UDK";
                        StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.IndianRed);
                        break;
                }

                LoadObjects();
                StatusBar_LeftMostText.Text = System.IO.Path.GetFileName(fileName);
                Title = "Soundplorer - " + System.IO.Path.GetFileName(fileName);

                backgroundScanner = new BackgroundWorker();
                backgroundScanner.DoWork += GetStreamTimes;
                backgroundScanner.WorkerSupportsCancellation = true;
                backgroundScanner.RunWorkerAsync();
                //Status.Text = "Ready";
                //saveToolStripMenuItem.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        private void GetStreamTimes(object sender, DoWorkEventArgs e)
        {
            foreach (SoundplorerExport se in BindedExportsList)
            {
                if (backgroundScanner.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }
                Debug.WriteLine("Getting time for " + se.Export.UIndex);
                se.LoadTime();
            }
        }

        private void LoadObjects()
        {
            BindedExportsList = Pcc.Exports.Where(e => e.ClassName == "WwiseBank" || e.ClassName == "WwiseStream").Select(x => new SoundplorerExport(x)).ToList();
            SoundExports_ListBox.ItemsSource = BindedExportsList;
            //string s = i.ToString("d6") + " : " + e.ClassName + " : \"" + e.ObjectName + "\"";
        }

        private void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //save();
            /*
            foreach (var item in AllTreeViewNodes)
            {
                item.Background = null;
                item.ToolTip = null;
            }*/
        }

        private void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //saveAs();
            /*
            foreach (var item in AllTreeViewNodes)
            {
                item.Background = null;
                item.ToolTip = null;
            }*/
        }


        public override void handleUpdate(List<PackageUpdate> updates)
        {
            /*List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool importChanges = changes.Contains(PackageChange.Import) || changes.Contains(PackageChange.ImportAdd);
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);
            int n = 0;
            bool hasSelection = GetSelected(out n);
            if (CurrentView == View.Names && changes.Contains(PackageChange.Names))
            {
                //int scrollTo = LeftSide_ListView..TopIndex + 1;
                //int selected = listBox1.SelectedIndex;
                RefreshView();
                //listBox1.SelectedIndex = selected;
                //listBox1.TopIndex = scrollTo;
            }
            else if (CurrentView == View.Imports && importChanges ||
                     CurrentView == View.Exports && exportNonDataChanges ||
                     CurrentView == View.Tree && (importChanges || exportNonDataChanges))
            {
                RefreshView();
                if (hasSelection)
                {
                    goToNumber(n);
                }
            }
            else if ((CurrentView == View.Exports || CurrentView == View.Tree) &&
                     hasSelection &&
                     updates.Contains(new PackageUpdate { index = n - 1, change = PackageChange.ExportData }))
            {
                //interpreterControl.memory = pcc.getExport(n).Data;
                //interpreterControl.RefreshMem();
                //binaryInterpreterControl.memory = pcc.getExport(n).Data;
                //binaryInterpreterControl.RefreshMem();
                Preview(true);
            }
            RefreshExportChangedStatus();*/
        }

        private void SoundExports_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport == null)
            {
                soundPanel.UnloadExport();
                return;
            }
            soundPanel.LoadExport(spExport.Export);
        }

        private void Soundplorer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync();
            }
            soundPanel.FreeAudioResources();
        }

        private void ExtractWEMFromBank_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null && spExport.Export.ClassName == "WwiseBank")
            {
                WwiseBank wb = new WwiseBank(spExport.Export);

                var dlg = new CommonOpenFileDialog("Select Output Folder")
                {
                    IsFolderPicker = true
                };

                if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return;
                }
                wb.GetQuickScan(); //load data
                wb.ExportAllWEMFiles(dlg.FileName);
            }
        }

        private void ExtractBank_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null && spExport.Export.ClassName == "WwiseBank")
            {
                SaveFileDialog d = new SaveFileDialog();

                d.Filter = "WwiseBank|*.bnk";
                d.FileName = spExport.Export.ObjectName + ".bnk";
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    //File.WriteAllBytes(d.FileName, spExport.Export.getBinaryData());
                    File.WriteAllBytes(d.FileName, spExport.Export.Data);
                    MessageBox.Show("Done.");
                }
            }
        }

        private void CompactAFC_Clicked(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog("Select mod's CookedPCConsole folder")
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }

            string[] afcFiles = System.IO.Directory.GetFiles(dlg.FileName, "*.afc");
            string[] pccFiles = System.IO.Directory.GetFiles(dlg.FileName, "*.pcc");

            if (afcFiles.Count() > 0 && pccFiles.Count() > 0)
            {
                string foldername = System.IO.Path.GetFileName(dlg.FileName);
                if (foldername.ToLower() == "cookedpcconsole")
                {
                    foldername = System.IO.Path.GetFileName(System.IO.Directory.GetParent(dlg.FileName).FullName);
                }
                string result = PromptDialog.Prompt("Enter an AFC filename that all mod referenced items will be repointed to.\n\nCompacting AFC folder: " + foldername, "Enter an AFC filename");
                if (result != null)
                {
                    var regex = new Regex(@"^[a-zA-Z0-9_]+$");

                    if (regex.IsMatch(result))
                    {
                        BusyText = "Finding all referenced audio";
                        IsBusy = true;
                        BackgroundWorker afcCompactWorker = new BackgroundWorker();
                        afcCompactWorker.DoWork += CompactAFCBackgroundThread;
                        afcCompactWorker.RunWorkerCompleted += compactAFCBackgroundThreadCompleted;
                        afcCompactWorker.RunWorkerAsync(new Tuple<string, string>(dlg.FileName, result));
                    }
                    else
                    {
                        MessageBox.Show("Only alphanumeric characters and underscores are allowed for the AFC filename.", "Error creating AFC");
                    }
                }
            }

        }

        private void compactAFCBackgroundThreadCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            IsBusy = false;
        }

        private void CompactAFCBackgroundThread(object sender, DoWorkEventArgs e)
        {
            var arguments = (Tuple<string, string>)e.Argument;
            string path = arguments.Item1;
            string NewAFCBaseName = arguments.Item2;

            string[] pccFiles = System.IO.Directory.GetFiles(path, "*.pcc");
            string[] afcFiles = System.IO.Directory.GetFiles(path, "*.afc").Select(x => System.IO.Path.GetFileNameWithoutExtension(x).ToLower()).ToArray();

            var ReferencedAFCAudio = new List<Tuple<string, int, int>>();

            int i = 1;
            foreach (string pccPath in pccFiles)
            {
                BusyText = "Finding all referenced audio (" + i + "/" + pccFiles.Count() + ")";
                using (IMEPackage pack = MEPackageHandler.OpenMEPackage(pccPath))
                {
                    List<IExportEntry> wwiseStreamExports = pack.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                    foreach (IExportEntry exp in wwiseStreamExports)
                    {
                        var afcNameProp = exp.GetProperty<NameProperty>("Filename");
                        if (afcNameProp != null && afcFiles.Contains(afcNameProp.ToString().ToLower()))
                        {
                            string afcName = afcNameProp.ToString().ToLower();
                            int readPos = exp.Data.Length - 8;
                            int audioSize = BitConverter.ToInt32(exp.Data, exp.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(exp.Data, exp.Data.Length - 4);
                            ReferencedAFCAudio.Add(new Tuple<string, int, int>(afcName, audioSize, audioOffset));
                        }
                    }
                }
                i++;
            }
            ReferencedAFCAudio = ReferencedAFCAudio.Distinct().ToList();

            //extract referenced audio
            BusyText = "Extracting referenced audio";
            var extractedAudioMap = new Dictionary<Tuple<string, int, int>, byte[]>();
            i = 1;
            foreach (Tuple<string, int, int> reference in ReferencedAFCAudio)
            {
                BusyText = "Extracting referenced audio (" + i + " / " + ReferencedAFCAudio.Count() + ")";
                string afcPath = System.IO.Path.Combine(path, reference.Item1 + ".afc");
                FileStream stream = new FileStream(afcPath, FileMode.Open, FileAccess.Read);
                stream.Seek(reference.Item3, SeekOrigin.Begin);
                byte[] extractedAudio = new byte[reference.Item2];
                stream.Read(extractedAudio, 0, reference.Item2);
                stream.Close();
                extractedAudioMap[reference] = extractedAudio;
                i++;
            }

            var newAFCEntryPointMap = new Dictionary<Tuple<string, int, int>, long>();
            i = 1;
            string newAfcPath = System.IO.Path.Combine(path, NewAFCBaseName + ".afc");
            if (File.Exists(newAfcPath))
            {
                File.Delete(newAfcPath);
            }
            FileStream newAFCStream = new FileStream(newAfcPath, FileMode.CreateNew, FileAccess.Write);

            foreach (Tuple<string, int, int> reference in ReferencedAFCAudio)
            {
                BusyText = "Building new AFC file (" + i + " / " + ReferencedAFCAudio.Count() + ")";
                newAFCEntryPointMap[reference] = newAFCStream.Position; //save entry point in map
                newAFCStream.Write(extractedAudioMap[reference], 0, extractedAudioMap[reference].Length);
                i++;
            }
            newAFCStream.Close();
            extractedAudioMap = null; //clean out ram on next GC

            i = 1;
            foreach (string pccPath in pccFiles)
            {
                BusyText = "Updating audio references (" + i + "/" + pccFiles.Count() + ")";
                using (IMEPackage pack = MEPackageHandler.OpenMEPackage(pccPath))
                {
                    bool shouldSave = false;
                    List<IExportEntry> wwiseStreamExports = pack.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                    foreach (IExportEntry exp in wwiseStreamExports)
                    {
                        var afcNameProp = exp.GetProperty<NameProperty>("Filename");
                        if (afcNameProp != null && afcFiles.Contains(afcNameProp.ToString().ToLower()))
                        {
                            string afcName = afcNameProp.ToString().ToLower();
                            int readPos = exp.Data.Length - 8;
                            int audioSize = BitConverter.ToInt32(exp.Data, exp.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(exp.Data, exp.Data.Length - 4);
                            var key = new Tuple<string, int, int>(afcName, audioSize, audioOffset);
                            long newOffset;
                            if (newAFCEntryPointMap.TryGetValue(key, out newOffset))
                            {
                                //its a match
                                afcNameProp.Value = NewAFCBaseName;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    exp.WriteProperty(afcNameProp);
                                    byte[] newData = exp.Data;
                                    Buffer.BlockCopy(BitConverter.GetBytes((int)newOffset), 0, newData, newData.Length - 4, 4); //update AFC audio offset
                                    exp.Data = newData;
                                    if (exp.DataChanged)
                                    {
                                        //don't mark for saving if the data didn't acutally change (e.g. trying to compact a compacted AFC).
                                        shouldSave = true;
                                    }
                                });
                            }
                        }
                        if (shouldSave)
                        {
                            Application.Current.Dispatcher.Invoke(
                            () =>
                            {
                                // Must run on the UI thread or the tool interop will throw an exception
                                // because we are on a background thread.
                                pack.save();
                            });
                        }
                    }

                }
                i++;
            }
            BusyText = "Rebuild complete";
            System.Threading.Thread.Sleep(2000);
        }

        /*private void ContextMenu_Opening(object sender, ContextMenuEventArgs e)
        {
            Debug.WriteLine("hi");
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null)
            {
                if (spExport.Export.ClassName == "WwiseBank")
                {

                } else
                {
                    ExportBankToFile_MenuItem.Visibility = Visibility.Collapsed;
                }
            }
    }*/
    }

    public class SoundplorerExport : INotifyPropertyChanged
    {
        public IExportEntry Export { get; set; }
        public bool ShouldHighlightAsChanged
        {
            get
            {
                if (Export != null)
                {
                    if (Export.HeaderChanged)
                    {
                        return true;
                    }
                    else if (Export.DataChanged)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool _loaded = false;
        public bool Loaded
        {
            get { return _loaded; }
            set
            {
                if (value != this._loaded)
                {
                    this._loaded = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _hideSoundIcon = true;
        public bool HideSoundIcon
        {
            get { return _hideSoundIcon; }
            set
            {
                if (value != this._hideSoundIcon)
                {
                    this._hideSoundIcon = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _timeString;
        public string TimeString
        {
            get { return _timeString; }
            set
            {
                if (value != this._timeString)
                {
                    this._timeString = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _displayString;
        public string DisplayString
        {
            get { return _displayString; }
            set
            {
                if (value != this._displayString)
                {
                    this._displayString = value;
                    OnPropertyChanged();
                }
            }
        }
        public SoundplorerExport(IExportEntry export)
        {
            this.Export = export;
            if (Export.ClassName == "WwiseStream")
            {
                TimeString = "Calculating";
            }
            else
            {
                TimeString = "Soundbank";
                Loaded = true;
            }
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            int paddingSize = Export.FileRef.ExportCount.ToString().Length;
            DisplayString = Export.UIndex.ToString("d" + paddingSize) + " : " + Export.ObjectName;
        }

        public void LoadTime()
        {
            if (Export.ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(Export);
                TimeSpan? time = w.GetSoundLength();
                if (time != null)
                {
                    //here backslash must be present to tell that parser colon is
                    //not the part of format, it just a character that we want in output
                    TimeString = time.Value.ToString(@"mm\:ss\:fff");
                }
                Loaded = true;
                HideSoundIcon = false;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
