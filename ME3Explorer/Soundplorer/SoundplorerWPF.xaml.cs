using ByteSizeLib;
using FontAwesome.WPF;
using KFreonLib.Debugging;
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
using System.IO.Compression;
using System.Linq;
using System.Reflection;
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
using System.Xml.Linq;

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

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get { return _isBusyTaskbar; }
            set { if (_isBusyTaskbar != value) { _isBusyTaskbar = value; OnPropertyChanged(); } }
        }

        private string _busyText;
        public string BusyText
        {
            get { return _busyText; }
            set { if (_busyText != value) { _busyText = value; OnPropertyChanged(); } }
        }

        private string _taskbarText;
        public string TaskbarText
        {
            get { return _taskbarText; }
            set { if (_taskbarText != value) { _taskbarText = value; OnPropertyChanged(); } }
        }

        public SoundplorerWPF()
        {
            TaskbarText = "Open a file to view sound-related exports";
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

        public void LoadFile(string fileName)
        {
            try
            {
                StatusBar_GameID_Container.Visibility = Visibility.Collapsed;
                TaskbarText = "Loading " + System.IO.Path.GetFileName(fileName) + " (" + ByteSize.FromBytes(new System.IO.FileInfo(fileName).Length) + ")";
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
                Title = "Soundplorer - " + System.IO.Path.GetFileName(fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        private void GetStreamTimes(object sender, DoWorkEventArgs e)
        {
            int i = 0;
            foreach (SoundplorerExport se in BindedExportsList)
            {
                backgroundScanner.ReportProgress((int)((i * 100.0) / BindedExportsList.Count));

                if (backgroundScanner.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }
                //Debug.WriteLine("Getting time for " + se.Export.UIndex);
                se.LoadData();
                i++;
            }
        }

        private void LoadObjects()
        {
            BindedExportsList = Pcc.Exports.Where(e => e.ClassName == "WwiseBank" || e.ClassName == "WwiseStream").Select(x => new SoundplorerExport(x)).ToList();
            SoundExports_ListBox.ItemsSource = BindedExportsList;

            backgroundScanner = new BackgroundWorker();
            backgroundScanner.DoWork += GetStreamTimes;
            backgroundScanner.RunWorkerCompleted += GetStreamTimes_Completed;
            backgroundScanner.WorkerReportsProgress = true;
            backgroundScanner.ProgressChanged += GetStreamTimes_ReportProgress;
            backgroundScanner.WorkerSupportsCancellation = true;
            backgroundScanner.RunWorkerAsync();
            IsBusyTaskbar = true;
            //string s = i.ToString("d6") + " : " + e.ClassName + " : \"" + e.ObjectName + "\"";
        }

        private void GetStreamTimes_ReportProgress(object sender, ProgressChangedEventArgs e)
        {
            TaskbarText = "Parsing " + System.IO.Path.GetFileName(Pcc.FileName) + " (" + e.ProgressPercentage + "%)";
        }

        private void GetStreamTimes_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarText = System.IO.Path.GetFileName(Pcc.FileName);
            IsBusyTaskbar = false;
        }

        private void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pcc.save();
        }

        private void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            string extension = System.IO.Path.GetExtension(pcc.FileName);
            d.Filter = $"*{extension}|*{extension}";
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }


        public override void handleUpdate(List<PackageUpdate> updates)
        {
            List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool importChanges = changes.Contains(PackageChange.Import) || changes.Contains(PackageChange.ImportAdd);
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);

            var loadedIndexes = BindedExportsList.Where(x => x.Export != null).Select(y => y.Export.Index).ToList();
            foreach (PackageUpdate pc in updates)
            {
                if (loadedIndexes.Contains(pc.index))
                {
                    LoadObjects();
                    break;
                }
            }
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
                        IsBusyTaskbar = true;
                        soundPanel.FreeAudioResources(); // stop playing
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
            IsBusyTaskbar = false;
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
                i++;
            }
            BusyText = "Rebuild complete";
            System.Threading.Thread.Sleep(2000);
        }

        private void ExportWav_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null && spExport.Export.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog();

                d.Filter = "Wave PCM|*.wav";
                d.FileName = spExport.Export.ObjectName + ".wav";
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    WwiseStream w = new WwiseStream(spExport.Export);
                    Stream source = w.CreateWaveStream(w.getPathToAFC());
                    if (source != null)
                    {
                        using (var fileStream = File.Create(d.FileName))
                        {
                            source.Seek(0, SeekOrigin.Begin);
                            source.CopyTo(fileStream);
                        }
                        MessageBox.Show("Done.");
                    }
                    else
                    {
                        MessageBox.Show("Error creating Wave file.\nThis might not be a supported codec or the AFC data may be incorrect.");
                    }
                }
            }
        }

        private void ExportRaw_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null && spExport.Export.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog();

                d.Filter = "Wwise WEM|*.wem";
                d.FileName = spExport.Export.ObjectName + ".wem";
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    WwiseStream w = new WwiseStream(spExport.Export);
                    if (w.ExtractRawFromStream(d.FileName, w.getPathToAFC()))
                    {
                        MessageBox.Show("Done.");
                    }
                    else
                    {
                        MessageBox.Show("Error extracting WEM file.\nMetadata for this raw data may be incorrect (e.g. too big for file).");
                    }
                }
            }
        }

        private void ExportOgg_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null && spExport.Export.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog();

                d.Filter = "Ogg Vorbis|*.ogg";
                d.FileName = spExport.Export.ObjectName + ".ogg";
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    WwiseStream w = new WwiseStream(spExport.Export);
                    string riffOutputFile = System.IO.Path.Combine(Directory.GetParent(d.FileName).FullName, System.IO.Path.GetFileNameWithoutExtension(d.FileName)) + ".dat";

                    if (w.ExtractRawFromStream(riffOutputFile, w.getPathToAFC()))
                    {
                        MemoryStream oggStream = WwiseStream.ConvertRIFFToWWwiseOGG(riffOutputFile, spExport.Export.FileRef.Game == MEGame.ME2);
                        //string outputOggPath = 
                        if (oggStream != null)// && File.Exists(outputOggPath))
                        {
                            oggStream.Seek(0, SeekOrigin.Begin);
                            using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                            {
                                oggStream.CopyTo(fs);
                                fs.Flush();
                            }
                            MessageBox.Show("Done.");
                        }
                        else
                        {
                            MessageBox.Show("Error extracting Ogg file.\nMetadata for the raw data may be incorrect (e.g. header specifies data is longer than it actually is).");
                        }
                    }
                }
            }
        }

        private void CloneAndReplace_Clicked(object sender, RoutedEventArgs e)
        {

        }

        private void ReplaceAudio_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null)
            {
                soundPanel.ReplaceAudio(spExport.Export);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                LoadFile(files[0]);
            }
        }

        /// <summary>
        /// Convert files to Wwise-encoded Oggs. Requires Wwise to be installed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ConvertToWwise_Clicked(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject")))
            {
                await TryDeleteDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject"));
            }

            //Verify Wwise is installed with the correct version
            string wwisePath = Environment.GetEnvironmentVariable("WWiseRoot");
            if (wwisePath != null)
            {
                wwisePath = System.IO.Path.Combine(wwisePath, @"Authoring\x64\Release\bin\WwiseCLI.exe");
                if (File.Exists(wwisePath))
                {
                    //check that it's a supported version...
                    var versionInfo = FileVersionInfo.GetVersionInfo(wwisePath);
                    string version = versionInfo.ProductVersion; // Will typically return "1.0.0" in your case
                    if (version != "2010.3.3.3773")
                    {
                        //wrong version
                        MessageBox.Show("WwiseCLI.exe found, but it's the wrong version:" + version + ".\nInstall Wwise Build 3773 to use this tool.");
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("WwiseCLI.exe was not found on your system.\nInstall Wwise Build 3773 to use this tool.");
                    return;
                }
            }
            else
            {
                MessageBox.Show("Wwise does not appear to be installed on your system.\nInstall Wwise Build 3773 to use this tool.");
                return;
            }

            //Wwise installed and cli found
            //MessageBox.Show("OK");


            /* The process for converting is going to be pretty in depth but will make converting files much easier and faster.
             * 1. Have a drag/drop panel or folder chooser where user .wav files will exist
             * 2. User starts conversion
             * 
             * Program steps when conversion starts:
             * 1. Copy source files to the originals directory, as this is where the data is converted from (Originals\SFX\)
             * 2. Update the Default Work Unit.wwu file in Actor-Mixer-Hierarchy:
             *    - Add Sound objects to AudioObjects for each sound.
             *       -SoundName: PathWithoutExtension(wav)
             *       -ID: GUID.GenerateGUID() <-- Might not be necessary
             *       -ShortID: FNVHash (must port from C to C# - see https://www.audiokinetic.com/qa/13/how-can-i-generate-wwise-work-units-wwu-files <-- Might not be necessary
             *       -other stuff.
             *    - Set conversion settings. Maybe just use the default
             * 3. Generate a soundbank through the CLI, runnign our project and setting the cache dir to our own.
             * 4. Pull the files out of the cache and then discard the project.
             * */

            //Folder chooser for .wavs
            var dlg = new CommonOpenFileDialog("Select folder containing .wav files")
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }

            string[] filesToConvert = Directory.GetFiles(dlg.FileName, "*.wav");
            if (filesToConvert.Count() == 0)
            {
                MessageBox.Show("The selected folder does not contain any .wav files for converting.");
                return;
            }

            //Extract the template project to temp
            var assembly = Assembly.GetExecutingAssembly();
            var stuff = assembly.GetManifestResourceNames();
            var resourceName = "ME3Explorer.Soundplorer.WwiseTemplateProject.zip";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                ZipArchive archive = new ZipArchive(stream);
                archive.ExtractToDirectory(System.IO.Path.GetTempPath());
            }

            //XElement wwiseDocument = new XElement("WwiseDocument", new XAttribute("Type", "WorkUnit"), new XAttribute("ID", "{5EBAC175-501D-487D-9F9C-C635AB93A866}"), new XAttribute("SchemaVersion", 44.ToString()));
            XElement externalSourcesList = new XElement("ExternalSourcesList", new XAttribute("SchemaVersion", 1.ToString()), new XAttribute("Root", dlg.FileName));

            //Generate the default Actor-Mixer document with the data we need

            //XElement wwiseDocument = new XElement("WwiseDocument", new XAttribute("Type", "WorkUnit"), new XAttribute("ID", "{5EBAC175-501D-487D-9F9C-C635AB93A866}"), new XAttribute("SchemaVersion", 44.ToString()));
            //XElement audioObjects = new XElement("AudioObjects");
            //wwiseDocument.Add(audioObjects);

            foreach (string file in filesToConvert)
            {
                XElement source = new XElement("Source", new XAttribute("Path", System.IO.Path.GetFileName(file)), new XAttribute("Conversion", "Vorbis"));
                externalSourcesList.Add(source);

                /*string guid = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
                XElement sound = new XElement("Sound", new XAttribute("Name", System.IO.Path.GetFileNameWithoutExtension(file)), new XAttribute("Type", "SoundFX"), new XAttribute("ID", guid));
                audioObjects.Add(sound);

                XElement childrenList = new XElement("ChildrenList");
                sound.Add(childrenList);

                guid = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
                XElement audioFileSource = new XElement("AudioFileSource", new XAttribute("Name", System.IO.Path.GetFileNameWithoutExtension(file)), new XAttribute("ID", guid));
                childrenList.Add(audioFileSource);

                audioFileSource.Add(new XElement("Language", "SFX"));
                audioFileSource.Add(new XElement("AudioFile", System.IO.Path.GetFileName(file)));

                //Bus Routing
                //			<BusRouting Name="Master Audio Bus" ID="{1514A4D8-1DA6-412A-A17E-75CA0C2149F3}" />

                XElement busRouting = new XElement("BusRouting", new XAttribute("Name", "Master Audio Bus"), new XAttribute("ID", "{1514A4D8-1DA6-412A-A17E-75CA0C2149F3}"));
                sound.Add(busRouting);

                //Default conversion settings GUID for templateproject: {6D1B890C-9826-4384-BF07-C15223E9FB56}
                XElement conversionInfo = new XElement("ConversionInfo");
                sound.Add(conversionInfo);

                XElement conversionRef = new XElement("ConversionRef", new XAttribute("Name", "Default Conversion Settings"), new XAttribute("ID", "{6D1B890C-9826-4384-BF07-C15223E9FB56}"));
                conversionInfo.Add(conversionRef);

                XElement activeSourceList = new XElement("ActiveSourceList");
                sound.Add(activeSourceList);

                XElement activeSource = new XElement("ActiveSource", new XAttribute("Name", System.IO.Path.GetFileName(file)), new XAttribute("ID", guid), new XAttribute("Platform", "Linked"));
                activeSourceList.Add(activeSource);*/
            }

            //Write ExternalSources.wsources
            string wsourcesFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject", "ExternalSources.wsources");

            File.WriteAllText(wsourcesFile, externalSourcesList.ToString());
            Debug.WriteLine(externalSourcesList.ToString());


            //Run Conversion
            DebugOutput.StartDebugger("Wwise Wav to Ogg Converter");
            Process process = new Process();
            process.StartInfo.FileName = wwisePath;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.OutputDataReceived += (s, eventArgs) => { Debug.WriteLine(eventArgs.Data); DebugOutput.PrintLn(eventArgs.Data); };
            process.ErrorDataReceived += (s, eventArgs) => { Debug.WriteLine(eventArgs.Data); DebugOutput.PrintLn(eventArgs.Data); };

            string projFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject", "TemplateProject.wproj");
            process.StartInfo.Arguments = $"\"{projFile}\" -ConvertExternalSources Windows";

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();
            process.Close();

            //Files generates
            string outputDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject", "OutputFiles");
            string copyToDirectory = System.IO.Path.Combine(dlg.FileName, "Converted");
            Directory.CreateDirectory(copyToDirectory);
            foreach (string file in filesToConvert)
            {
                string basename = System.IO.Path.GetFileNameWithoutExtension(file);
                File.Copy(System.IO.Path.Combine(outputDirectory, basename + ".ogg"), System.IO.Path.Combine(copyToDirectory, basename + ".ogg"), true);
            }
            var deleteResult = await TryDeleteDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject"));
            Debug.WriteLine("Deleted templatedproject: " + deleteResult);
            MessageBox.Show("Files have been converted and placed into the Converted subdirectory.");
        }

        public static async Task<bool> TryDeleteDirectory(string directoryPath, int maxRetries = 10, int millisecondsDelay = 30)
        {
            if (directoryPath == null)
                throw new ArgumentNullException(directoryPath);
            if (maxRetries < 1)
                throw new ArgumentOutOfRangeException(nameof(maxRetries));
            if (millisecondsDelay < 1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

            for (int i = 0; i < maxRetries; ++i)
            {
                try
                {
                    if (Directory.Exists(directoryPath))
                    {
                        Directory.Delete(directoryPath, true);
                    }

                    return true;
                }
                catch (IOException)
                {
                    await Task.Delay(millisecondsDelay);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(millisecondsDelay);
                }
            }

            return false;
        }
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

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get { return _needsLoading; }
            set
            {
                if (value != this._needsLoading)
                {
                    this._needsLoading = value;
                    OnPropertyChanged();
                }
            }
        }



        private FontAwesomeIcon _icon;
        public FontAwesomeIcon Icon
        {
            get { return _icon; }
            set
            {
                if (value != this._icon)
                {
                    this._icon = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _timeString;
        public string SubText
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
                SubText = "Calculating stream length";
                Icon = FontAwesomeIcon.Spinner;
            }
            else
            {
                SubText = "Calculating number of embedded WEMs";
                Icon = FontAwesomeIcon.University;
            }
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            int paddingSize = Export.FileRef.ExportCount.ToString().Length;
            DisplayString = Export.UIndex.ToString("d" + paddingSize) + ": " + Export.ObjectName;
        }

        public void LoadData()
        {
            if (Export.ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(Export);
                string afcPath = w.getPathToAFC();
                if (afcPath == "")
                {
                    SubText = "Could not find AFC";
                }
                else
                {
                    TimeSpan? time = w.GetSoundLength();
                    if (time != null)
                    {
                        //here backslash must be present to tell that parser colon is
                        //not the part of format, it just a character that we want in output
                        SubText = time.Value.ToString(@"mm\:ss\:fff");
                    }
                    else
                    {
                        SubText = "Error getting length";
                    }
                }
                NeedsLoading = false;
                Icon = FontAwesomeIcon.VolumeUp;
            }
            if (Export.ClassName == "WwiseBank")
            {
                WwiseBank wb = new WwiseBank(Export);
                var embeddedWEMFiles = wb.GetWEMFilesMetadata();
                SubText = embeddedWEMFiles.Count() + " embedded WEM" + (embeddedWEMFiles.Count() != 1 ? "s" : "");
                NeedsLoading = false;
                Icon = FontAwesomeIcon.University;
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
