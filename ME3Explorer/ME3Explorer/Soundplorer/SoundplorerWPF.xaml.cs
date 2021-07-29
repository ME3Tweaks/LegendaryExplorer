using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FontAwesome5;
using ME3Explorer.TlkManagerNS;
using ME3Explorer.Unreal.Classes;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using WwiseStreamHelper = ME3Explorer.Unreal.WwiseStreamHelper;
using WwiseStream = ME3ExplorerCore.Unreal.BinaryConverters.WwiseStream;

namespace ME3Explorer.Soundplorer
{
    /// <summary>
    /// Interaction logic for SoundplorerWPF.xaml
    /// </summary>
    public partial class SoundplorerWPF : WPFBase, IBusyUIHost, IRecents
    {
        private string LoadedISBFile;
        private string LoadedAFCFile;
        BackgroundWorker backgroundScanner;
        public ObservableCollectionExtended<object> BindedItemsList { get; set; } = new ObservableCollectionExtended<object>();

        public bool AudioFileLoaded => Pcc != null || LoadedISBFile != null || LoadedAFCFile != null;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get => _isBusyTaskbar;
            set => SetProperty(ref _isBusyTaskbar, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private string _statusBarIDText;
        public string StatusBarIDText
        {
            get => _statusBarIDText;
            set => SetProperty(ref _statusBarIDText, value);
        }

        private string _taskbarText = "Open a file to view sound-related exports/data";
        public string TaskbarText
        {
            get => _taskbarText;
            set => SetProperty(ref _taskbarText, value);
        }

        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;

        public SoundplorerWPF() : base("Soundplorer")
        {
            LoadCommands();
            InitializeComponent();
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileName => LoadFile(fileName));
        }

        public SoundplorerWPF(ExportEntry export) : this()
        {
            FileQueuedForLoad = export.FileRef.FilePath;
            ExportQueuedForFocusing = export;
        }

        public ICommand PopoutCurrentViewCommand { get; set; }
        private void LoadCommands()
        {
            PopoutCurrentViewCommand = new GenericCommand(PopoutSoundpanel, ExportIsSelected);
        }

        private void PopoutSoundpanel()
        {
            soundPanel.PopOut();
        }

        private bool ExportIsSelected() => SoundExports_ListBox.SelectedItem is SoundplorerExport;

        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "All supported files|*.pcc;*.u;*.sfm;*.upk;*.isb;*.afc;*.xxx|Package files|*.pcc;*.u;*.sfm;*.upk;*.xxx|ISACT Sound Bank files|*.isb|Audio File Cache (AFC)|*.afc" };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                try
                {
                    LoadFile(d.FileName);
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
                soundPanel.FreeAudioResources(); //stop playback
                StatusBarIDText = null;
                TaskbarText = $"Loading {Path.GetFileName(fileName)} ({FileSize.FormatSize(new FileInfo(fileName).Length)})";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                UnLoadMEPackage();
                LoadedISBFile = null;
                LoadedAFCFile = null;
                if (Path.GetExtension(fileName).ToLower() == ".isb")
                {
                    LoadedISBFile = fileName;
                    StatusBarIDText = "ISB";
                }
                else if (Path.GetExtension(fileName).ToLower() == ".afc")
                {
                    LoadedAFCFile = fileName;
                    StatusBarIDText = "AFC";
                }
                else
                {
                    LoadMEPackage(fileName);
                    StatusBarIDText = Pcc.Game.ToString();
                }


                if (LoadedISBFile != null)
                {
                    LoadISB();
                }
                else if (LoadedAFCFile != null)
                {
                    LoadAFC();
                }
                else
                {
                    LoadObjects();
                }
                Title = $"Soundplorer - {Path.GetFileName(fileName)}";
                OnPropertyChanged(nameof(AudioFileLoaded));
                RecentsController.AddRecent(fileName, false);
                RecentsController.SaveRecentList(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        private async void LoadAFC()
        {
            BindedItemsList.ClearEx();
            IsBusyTaskbar = true;
            TaskbarText = $"Loading AFC: {Path.GetFileName(LoadedAFCFile)}";
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync(); //cancel current operation
                while (backgroundScanner.IsBusy)
                {
                    //I am sorry for this. I truely am. 
                    //But it's the simplest code to get the job done while we wait for the thread to finish.
                    await Task.Delay(200);
                }
            }

            //Background thread as larger AFC parsing can lock up the UI for a few seconds.
            backgroundScanner = new BackgroundWorker();
            backgroundScanner.DoWork += LoadAFCFile;
            backgroundScanner.RunWorkerCompleted += LoadAFCFile_Completed;
            backgroundScanner.WorkerSupportsCancellation = true;
            backgroundScanner.RunWorkerAsync(LoadedAFCFile);
        }

        private void LoadAFCFile_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is List<AFCFileEntry> result)
            {
                BindedItemsList.AddRange(result);
                backgroundScanner = new BackgroundWorker();
                backgroundScanner.DoWork += GetStreamTimes;
                backgroundScanner.RunWorkerCompleted += GetStreamTimes_Completed;
                backgroundScanner.WorkerReportsProgress = true;
                backgroundScanner.ProgressChanged += GetStreamTimes_ReportProgress;
                backgroundScanner.WorkerSupportsCancellation = true;
                backgroundScanner.RunWorkerAsync(result.Cast<object>().ToList());
            }
        }

        private void LoadAFCFile(object sender, DoWorkEventArgs e)
        {
            var entries = new List<AFCFileEntry>();
            using (FileStream fileStream = new FileStream((string)e.Argument, FileMode.Open, FileAccess.Read))
            {
                // Get endianness
                Endian endianness = null;

                if (fileStream.Position < fileStream.Length - 4)
                {
                    var firstMagic = fileStream.ReadStringASCII(4);
                    if (firstMagic == "RIFX") endianness = Endian.Big;
                    if (firstMagic == "RIFF") endianness = Endian.Little;
                    if (endianness == null)
                        Debug.WriteLine("Malformed AFC! It must start with RIFF/RIFX");
                }

                if (endianness == null)
                {
                    Debug.WriteLine("Malformed AFC! It must start with RIFF/RIFX");
                    endianness = Endian.Little; //just ignore it anyways
                }

                fileStream.Position = 0;
                EndianReader reader = new EndianReader(fileStream) { Endian = endianness };

                while (fileStream.Position < fileStream.Length - 4)
                {
                    int offset = (int)fileStream.Position;
                    TaskbarText = $"Loading AFC: {Path.GetFileName(LoadedAFCFile)} ({(int)((fileStream.Position * 100.0) / fileStream.Length)}%)";

                    string readStr = fileStream.ReadStringASCII(4);
                    if (readStr != "RIFF" && readStr != "RIFX")
                    {
                        //keep scanning
                        fileStream.Seek(-3, SeekOrigin.Current);
                        continue;
                    }

                    //Found header
                    int size = reader.ReadInt32();
                    fileStream.Seek(8, SeekOrigin.Current); //skip WAVE and fmt

                    short wwiseVersionMaybe = fileStream.ReadInt16();

                    fileStream.Seek(size - 10, SeekOrigin.Current);

                    var entry = new AFCFileEntry(LoadedAFCFile, offset, size + 8, wwiseVersionMaybe == 0x28, reader.Endian);
                    entries.Add(entry);
                }
            }
            e.Result = entries;
            return;
        }

        private async void LoadISB()
        {
            BindedItemsList.ClearEx();
            IsBusyTaskbar = true;
            TaskbarText = $"Loading ISB: {Path.GetFileName(LoadedISBFile)}";
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync(); //cancel current operation
                while (backgroundScanner.IsBusy)
                {
                    //I am sorry for this. I truely am. 
                    //But it's the simplest code to get the job done while we wait for the thread to finish.
                    await Task.Delay(200);
                }
            }

            //Background thread as larger ISB parsing can lock up the UI for a few seconds.
            backgroundScanner = new BackgroundWorker();
            backgroundScanner.DoWork += LoadISBFile;
            backgroundScanner.RunWorkerCompleted += LoadISBFile_Completed;
            backgroundScanner.WorkerSupportsCancellation = true;
            backgroundScanner.RunWorkerAsync(LoadedISBFile);
        }

        private void LoadISBFile_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is ISBank result)
            {
                var entries = new List<ISACTFileEntry>(result.BankEntries.Where(x => x.DataAsStored != null).Select(x => new ISACTFileEntry(x)));
                BindedItemsList.AddRange(entries);
                backgroundScanner = new BackgroundWorker();
                backgroundScanner.DoWork += GetStreamTimes;
                backgroundScanner.RunWorkerCompleted += GetStreamTimes_Completed;
                backgroundScanner.WorkerReportsProgress = true;
                backgroundScanner.ProgressChanged += GetStreamTimes_ReportProgress;
                backgroundScanner.WorkerSupportsCancellation = true;
                backgroundScanner.RunWorkerAsync(entries.Cast<object>().ToList());
            }
        }

        private void LoadISBFile(object sender, DoWorkEventArgs e)
        {
            ISBank bank = new ISBank((string)e.Argument);
            e.Result = bank;
        }

        private void GetStreamTimes(object sender, DoWorkEventArgs e)
        {
            var ExportsToLoad = (List<object>)e.Argument;
            int i = 0;
            foreach (object se in ExportsToLoad)
            {
                if (backgroundScanner.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                backgroundScanner.ReportProgress((int)((i * 100.0) / BindedItemsList.Count));
                //Debug.WriteLine("Getting time for " + se.Export.UIndex);
                switch (se)
                {
                    case SoundplorerExport export:
                        export.LoadData();
                        break;
                    case ISACTFileEntry entry:
                        entry.LoadData();
                        break;
                    case AFCFileEntry aentry:
                        aentry.LoadData();
                        break;
                }
                i++;
            }
        }

        private async void LoadObjects(List<SoundplorerExport> exportsToReload = null)
        {
            if (exportsToReload == null)
            {
                BindedItemsList.Clear();
                BindedItemsList.AddRange(Pcc.Exports.Where(e => e.ClassName == "WwiseBank" || e.ClassName == "WwiseStream" || e.ClassName == "SoundNodeWave").Select(x => new SoundplorerExport(x)));
                //SoundExports_ListBox.ItemsSource = BindedExportsList; //todo: figure out why this is required and data is not binding
            }
            else
            {
                foreach (SoundplorerExport se in exportsToReload)
                {
                    se.SubText = "Refreshing";
                    se.NeedsLoading = true;
                    se.Icon = EFontAwesomeIcon.Solid_Spinner;
                }
            }
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync(); //cancel current operation
                while (backgroundScanner.IsBusy)
                {
                    //I am sorry for this. I truely am. 
                    //But it's the simplest code to get the job done while we wait for the thread to finish.
                    await Task.Delay(200);
                }
            }


            backgroundScanner = new BackgroundWorker();
            backgroundScanner.DoWork += GetStreamTimes;
            backgroundScanner.RunWorkerCompleted += GetStreamTimes_Completed;
            backgroundScanner.WorkerReportsProgress = true;
            backgroundScanner.ProgressChanged += GetStreamTimes_ReportProgress;
            backgroundScanner.WorkerSupportsCancellation = true;
            backgroundScanner.RunWorkerAsync(exportsToReload != null ? exportsToReload.Cast<object>().ToList() : BindedItemsList.ToList());
            IsBusyTaskbar = true;
            //string s = i.ToString("d6") + " : " + e.ClassName + " : \"" + e.ObjectName + "\"";
        }

        private void GetStreamTimes_ReportProgress(object sender, ProgressChangedEventArgs e)
        {
            IsBusyTaskbar = true; //enforce spinner
            TaskbarText = "Parsing " + Path.GetFileName(LoadedISBFile ?? LoadedAFCFile ?? Pcc.FilePath) + " (" + e.ProgressPercentage + "%)";
        }

        private void GetStreamTimes_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarText = Path.GetFileName(LoadedISBFile ?? LoadedAFCFile ?? Pcc.FilePath);
            IsBusyTaskbar = false;
        }

        private void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pcc.Save();
        }

        private void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            string extension = Path.GetExtension(Pcc.FilePath);
            d.Filter = $"*{extension}|*{extension}";
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Pcc.Save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            if (LoadedISBFile != null || LoadedAFCFile != null)
            {
                return; //we don't handle updates on ISB or AFC
            }

            List<SoundplorerExport> bindedListAsCasted = BindedItemsList.Cast<SoundplorerExport>().ToList();
            List<PackageChange> changes = updates.Select(x => x.Change).ToList();

            var loadedUIndexes = bindedListAsCasted.Where(x => x.Export != null).Select(y => y.Export.UIndex).ToList();

            List<SoundplorerExport> exportsRequiringReload = new List<SoundplorerExport>();
            foreach (PackageUpdate pc in updates)
            {
                if (loadedUIndexes.Contains(pc.Index))
                {
                    SoundplorerExport sp = bindedListAsCasted.First(x => x.Export.UIndex == pc.Index);
                    exportsRequiringReload.Add(sp);
                }
            }



            if (exportsRequiringReload.Any())
            {
                SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
                if (spExport == null)
                {
                    if (exportsRequiringReload.Contains(spExport))
                    {
                        soundPanel.FreeAudioResources(); //unload the current export
                    }
                }
                LoadObjects(exportsRequiringReload);

                if (spExport != null)
                {
                    soundPanel.LoadExport(spExport.Export); //reload
                }
            }
        }

        private void SoundExports_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object selectedItem = SoundExports_ListBox.SelectedItem;
            SoundplorerExport spExport = selectedItem as SoundplorerExport;
            if (spExport == null)
            {
                soundPanel.UnloadExport();
            }
            ISACTFileEntry isEntry = selectedItem as ISACTFileEntry;
            if (isEntry == null)
            {
                soundPanel.UnloadISACTEntry();
            }

            AFCFileEntry aEntry = selectedItem as AFCFileEntry;
            if (aEntry == null)
            {
                soundPanel.UnloadAFCEntry();
            }


            if (isEntry != null) soundPanel.LoadISACTEntry(isEntry.Entry);
            if (spExport != null) soundPanel.LoadExport(spExport.Export);
            if (aEntry != null) soundPanel.LoadAFCEntry(aEntry);

            if(Properties.Settings.Default.SoundplorerAutoplayEntriesOnSelection)
            {
                soundPanel.StartPlayingCurrentSelection();
            }
        }

        private void Soundplorer_Closing(object sender, CancelEventArgs e)
        {
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync();
            }
            soundPanel.FreeAudioResources();
            soundPanel.Dispose(); //Gets rid of WinForms control
        }

        private void ExtractWEMAsWaveFromBank_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            ExtractBankToWav(spExport);
        }

        private void ExtractBankToWav(SoundplorerExport spExport, string location = null)
        {
            if (spExport != null && spExport.Export.ClassName == "WwiseBank")
            {
                var bank = spExport.Export.GetBinaryData<WwiseBank>();
                if (bank.EmbeddedFiles.Count > 0)
                {
                    if (location == null)
                    {
                        var dlg = new CommonOpenFileDialog("Select output folder")
                        {
                            IsFolderPicker = true
                        };

                        if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok)
                        {
                            return;
                        }
                        location = dlg.FileName;
                    }

                    foreach ((uint wemID, byte[] wemData) in bank.EmbeddedFiles)
                    {
                        string wemHeader = "" + (char)wemData[0] + (char)wemData[1] + (char)wemData[2] + (char)wemData[3];
                        string wemName = $"{spExport.Export.ObjectName}_0x{wemID:X8}";
                        if (wemHeader == "RIFF" || wemHeader == "RIFX")
                        {
                            EmbeddedWEMFile wem = new EmbeddedWEMFile(wemData, wemName, spExport.Export); //will correct truncated stuff
                            Stream waveStream = soundPanel.getPCMStream(forcedWemFile: wem);
                            if (waveStream != null && waveStream.Length > 0)
                            {
                                string outputname = wemName + ".wav";
                                string outpath = Path.Combine(location, outputname);
                                waveStream.SeekBegin();
                                using var fileStream = File.Create(outpath);
                                waveStream.CopyTo(fileStream);
                            }
                        }
                    }
                }
            }
        }

        private void ExtractBank_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null && spExport.Export.ClassName == "WwiseBank")
            {
                ExportBank(spExport);
            }
        }

        private void ExportBank(SoundplorerExport spExport)
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "WwiseBank|*.bnk",
                FileName = spExport.Export.ObjectName + ".bnk",
                Title = "Select save location for bank file"
            };
            bool? res = d.ShowDialog();
            if (res.HasValue && res.Value)
            {
                //File.WriteAllBytes(d.FileName, spExport.Export.getBinaryData());
                File.WriteAllBytes(d.FileName, spExport.Export.Data);
                MessageBox.Show("Done.");
            }
        }

        private void CompactAFC_Clicked(object sender, RoutedEventArgs e)
        {
            new AFCCompactorUI.AFCCompactorUI().Show();
        }

        private void ExportWav_Clicked(object sender, RoutedEventArgs e)
        {
            switch (SoundExports_ListBox.SelectedItem)
            {
                case SoundplorerExport spE:
                    ExportWave(spE);
                    break;
                case AFCFileEntry afE:
                    ExportWaveAFC(afE);
                    break;
            }
        }

        private void ExportWaveAFC(AFCFileEntry afE, string location = null)
        {
            bool silent = location != null;
            if (!silent)
            {
                string presetfilename = $"{Path.GetFileNameWithoutExtension(afE.AFCPath)}_{afE.Offset}.wav";
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Wave PCM File|*.wav",
                    FileName = presetfilename
                };
                if (d.ShowDialog().Value)
                {
                    location = d.FileName;
                }
            }

            if (location != null)
            {
                using (Stream s = WwiseStreamHelper.CreateWaveStreamFromRaw(afE.AFCPath, afE.Offset, afE.DataSize, afE.ME2))
                {
                    using (var fileStream = File.Create(location))
                    {
                        s.Seek(0, SeekOrigin.Begin);
                        s.CopyTo(fileStream);
                    }
                }
            }
            if (!silent)
            {
                MessageBox.Show("Done.");
            }
        }


        private void ExportWave(SoundplorerExport spExport, string outputLocation = null)
        {
            if (spExport != null && spExport.Export.ClassName == "WwiseStream")
            {
                bool silent = outputLocation != null;
                if (outputLocation == null)
                {
                    SaveFileDialog d = new SaveFileDialog
                    {
                        Filter = "Wave PCM|*.wav",
                        FileName = spExport.Export.ObjectName + ".wav"
                    };
                    if (d.ShowDialog() == true)
                    {
                        outputLocation = d.FileName;
                    }
                    else
                    {
                        return;
                    }
                }

                WwiseStream w = spExport.Export.GetBinaryData<WwiseStream>();
                Stream source = w.CreateWaveStream();
                if (source != null)
                {
                    using (var fileStream = File.Create(outputLocation))
                    {
                        source.Seek(0, SeekOrigin.Begin);
                        source.CopyTo(fileStream);
                    }
                    if (!silent)
                    {
                        MessageBox.Show("Done.");
                    }
                }
                else
                {
                    if (!silent)
                    {
                        MessageBox.Show("Error creating Wave file.\nThis might not be a supported codec or the AFC data may be incorrect.");
                    }
                }
            }
        }

        private void ExportRaw_Clicked(object sender, RoutedEventArgs e)
        {
            switch (SoundExports_ListBox.SelectedItem)
            {
                case SoundplorerExport spExport:
                    if (spExport.Export.ClassName == "WwiseStream")
                    {
                        SaveFileDialog d = new SaveFileDialog
                        {
                            Filter = "Wwise WEM|*.wem",
                            FileName = spExport.Export.ObjectName + ".wem"
                        };
                        if (d.ShowDialog() == true)
                        {
                            var w = spExport.Export.GetBinaryData<WwiseStream>();
                            if (w.ExtractRawFromSourceToFile(d.FileName))
                            {
                                MessageBox.Show("Done.");
                            }
                            else
                            {
                                MessageBox.Show("Error extracting WEM file.\nMetadata for this raw data may be incorrect (e.g. too big for file).");
                            }
                        }
                    }
                    break;
                case AFCFileEntry afcEntry:
                    {
                        string presetfilename = $"{Path.GetFileNameWithoutExtension(afcEntry.AFCPath)}_{afcEntry.Offset}.wem";

                        SaveFileDialog d = new SaveFileDialog
                        {
                            Filter = "Wwise WEM|*.wem",
                            FileName = presetfilename
                        };
                        if (d.ShowDialog() == true)
                        {
                            if (WwiseStreamHelper.ExtractRawFromSourceToFile(d.FileName, afcEntry.AFCPath, afcEntry.DataSize, afcEntry.Offset))
                            {
                                MessageBox.Show("Done.");
                            }
                            else
                            {
                                MessageBox.Show("Error extracting AFC WEM file.");
                            }
                        }
                    }
                    break;
            }
        }

        private void ExportOgg_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport && spExport.Export.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Ogg Vorbis|*.ogg",
                    FileName = spExport.Export.ObjectName + ".ogg"
                };
                if (d.ShowDialog() == true)
                {
                    WwiseStream w = spExport.Export.GetBinaryData<WwiseStream>();
                    string riffOutputFile = Path.Combine(Directory.GetParent(d.FileName).FullName, Path.GetFileNameWithoutExtension(d.FileName)) + ".dat";

                    if (w.ExtractRawFromSourceToFile(riffOutputFile))
                    {
                        MemoryStream oggStream = WwiseStreamHelper.ConvertRIFFToWWwiseOGG(riffOutputFile, spExport.Export.FileRef.Game == MEGame.ME2);
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

            if (SoundExports_ListBox.SelectedItem is AFCFileEntry afE)
            {
                string presetfilename = $"{Path.GetFileNameWithoutExtension(afE.AFCPath)}_{afE.Offset}.ogg";
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Ogg Vorbis|*.ogg",
                    FileName = presetfilename
                };
                if (d.ShowDialog() == true)
                {
                    string riffOutputFile = Path.Combine(Directory.GetParent(d.FileName).FullName, Path.GetFileNameWithoutExtension(d.FileName)) + ".dat";

                    if (WwiseStreamHelper.ExtractRawFromSourceToFile(riffOutputFile, afE.AFCPath, afE.DataSize, afE.Offset))
                    {
                        MemoryStream oggStream = WwiseStreamHelper.ConvertRIFFToWWwiseOGG(riffOutputFile, afE.ME2);
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
                            MessageBox.Show("Error extracting and converting to Ogg file.");
                        }
                    }
                }
            }
        }

        private void CloneAndReplace_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport)
            {
                CloneAndReplace(false);
            }
        }

        private void CloneAndReplace(bool fromWave)
        {
            string result = PromptDialog.Prompt(this, "Enter a new object name for the cloned item.", "Cloned export name");
            if (result != null)
            {
                SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
                if (spExport != null && spExport.Export.ClassName == "WwiseStream")
                {
                    ExportEntry clone = spExport.Export.Clone();
                    clone.ObjectName = result;
                    spExport.Export.FileRef.AddExport(clone);
                    SoundplorerExport newExport = new SoundplorerExport(clone);
                    BindedItemsList.Add(newExport);
                    var reloadList = new List<SoundplorerExport> { newExport };
                    SoundExports_ListBox.ScrollIntoView(newExport);
                    SoundExports_ListBox.UpdateLayout();
                    if (SoundExports_ListBox.ItemContainerGenerator.ContainerFromItem(newExport) is ListBoxItem item)
                    {
                        item.Focus();
                    }
                    if (fromWave)
                    {
                        soundPanel.ReplaceAudioFromWwiseOgg(forcedExport: clone);
                    }
                    else
                    {
                        soundPanel.ReplaceAudioFromWave(forcedExport: clone);
                    }
                    LoadObjects(reloadList);
                }
            }
        }

        private void ReplaceAudio_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport)
            {
                soundPanel.ReplaceAudioFromWwiseOgg(forcedExport: spExport.Export);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                // Note that you can have more than one file.
                e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {

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
        private async void ConvertFolderToWwise_Clicked(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(Path.Combine(Path.GetTempPath(), "TemplateProject")))
            {
                await Soundpanel.TryDeleteDirectory(Path.Combine(Path.GetTempPath(), "TemplateProject"));
            }

            //Verify Wwise is installed with the correct version
            string wwisePath = Soundpanel.GetWwiseCLIPath(false);
            if (wwisePath == null) return; //abort. getpath is not silent so it will show dialogs before this is reached.
            var dlg = new CommonOpenFileDialog("Select folder containing .wav files") { IsFolderPicker = true };
            if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok) { return; }

            string[] filesToConvert = Directory.GetFiles(dlg.FileName, "*.wav");
            if (!filesToConvert.Any())
            {
                MessageBox.Show("The selected folder does not contain any .wav files for converting.");
                return;
            }

            SoundReplaceOptionsDialog srod = new SoundReplaceOptionsDialog();
            if (srod.ShowDialog().Value)
            {
                string convertedFolder = await soundPanel.RunWwiseConversion(wwisePath, dlg.FileName, srod.ChosenSettings);
                MessageBox.Show("Done. Converted ogg files have been placed into:\n" + convertedFolder);
            }
            else
            {
                return; //user didn't choose any settings
            }


        }

        private void ExtractAllAudio_Clicked(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog("Select output folder")
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok)
            {
                return;
            }
            var location = dlg.FileName;

            IsBusy = true;
            BusyText = "Extracting audio";
            var itemsToExport = new List<object>(BindedItemsList);
            var exportWorker = new BackgroundWorker();
            exportWorker.DoWork += delegate
            {
                foreach (object o in itemsToExport)
                {
                    switch (o)
                    {
                        case SoundplorerExport sp when sp.Export.ClassName == "WwiseStream":
                            {
                                string outfile = Path.Combine(location, sp.Export.ObjectName + ".wav");
                                ExportWave(sp, outfile);
                                break;
                            }
                        case SoundplorerExport sp when sp.Export.ClassName == "WwiseBank":
                            {
                                ExtractBankToWav(sp, location);
                                break;
                            }
                        case ISACTFileEntry ife:
                            {
                                string outfile = Path.Combine(location, Path.GetFileNameWithoutExtension(ife.Entry.FileName) + ".wav");
                                MemoryStream ms = ife.Entry.GetWaveStream();
                                File.WriteAllBytes(outfile, ms.ToArray());
                                break;
                            }
                        case AFCFileEntry afE:
                            {
                                string presetfilename = $"{Path.GetFileNameWithoutExtension(afE.AFCPath)}_{afE.Offset}.wav";
                                ExportWaveAFC(afE, Path.Combine(location, presetfilename));
                                break;
                            }
                    }
                }
            };
            exportWorker.RunWorkerCompleted += delegate
            {
                IsBusy = false;
                MessageBox.Show("Done.");
            };
            exportWorker.RunWorkerAsync();
        }

        private void ReplaceAudioFromWav_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport)
            {
                soundPanel.ReplaceAudioFromWave(forcedExport: spExport.Export);
            }
        }

        private void CloneAndReplaceFromWav_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null)
            {
                CloneAndReplace(true);
            }
        }

        private void SoundExportItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e is KeyEventArgs ke)
            {
                if (ke.Key == Key.Space)
                {
                    if (soundPanel.CanStartPlayback())
                    {
                        soundPanel.StartOrPausePlaying();
                    }
                    ke.Handled = true;
                }
                if (ke.Key == Key.Escape)
                {
                    soundPanel.StopPlaying();
                    ke.Handled = true;
                }
            }
        }

        private void ReverseEndiannessOfIDs_Clicked(object sender, RoutedEventArgs e)
        {
            ReverseEndianDisplayOfIDs_MenuItem.IsChecked = !ReverseEndianDisplayOfIDs_MenuItem.IsChecked;
            Properties.Settings.Default.SoundplorerReverseIDDisplayEndianness = ReverseEndianDisplayOfIDs_MenuItem.IsChecked;
            Properties.Settings.Default.Save();
        }

        private void AutoplayEntryOnSelection_MenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            AutoplayEntryOnSelection_MenuItem.IsChecked = !AutoplayEntryOnSelection_MenuItem.IsChecked;
            Properties.Settings.Default.SoundplorerAutoplayEntriesOnSelection = AutoplayEntryOnSelection_MenuItem.IsChecked;
            Properties.Settings.Default.Save();
        }

        private void SoundExports_ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            soundPanel.StartPlayingCurrentSelection();
        }

        private void DebugWriteBankToFileRebuild_Clicked(object sender, RoutedEventArgs e)
        {
            /* leave here until ISB work is completed - Mgamerz Feb 19 2020
            var soundfiles = Directory.GetFiles(@"D:\Origin Games\Mass Effect\Biogame\CookedPC\Packages\Audio_Content", "*.upk", SearchOption.AllDirectories).ToList();
            foreach (var sf in soundfiles)
            {
                var p = MEPackageHandler.OpenMEPackage(sf);
                var snwexports = p.Exports.Where(x => x.ClassName == "SoundNodeWave").ToList();
                foreach (var ex in snwexports)
                {
                    int dataSizeOffset = ex.propsEnd() + 4;
                    int dataLength = BitConverter.ToInt32(ex.Data, dataSizeOffset);
                    if (dataLength > 0)
                    {
                        byte[] binData = ex.Data.Skip(ex.propsEnd() + 20).ToArray();
                        ISBank isb = new ISBank(binData, true);
                        foreach (var isbe in isb.BankEntries)
                        {
                            if (isbe.CodecID != 0x2)
                            {
                                Debug.WriteLine($"Codec {isbe.CodecID} in {sf} {ex.UIndex} {ex.InstancedFullPath}");
                            }
                        }
                    }
                }
            }

            return;
            */
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport)
            {
                if (spExport.Export.ClassName == "WwiseBank")
                {
                    var bank = spExport.Export.GetBinaryData<WwiseBank>();
                    if (bank.EmbeddedFiles.Count > 0)
                    {
                        int i = 0;
                        var AllWems = new List<EmbeddedWEMFile>();
                        foreach ((uint wemID, byte[] wemData) in bank.EmbeddedFiles)
                        {
                            string wemId = wemID.ToString("X8");
                            string wemName = "Embedded WEM 0x" + wemId;// + "(" + singleWemMetadata.Item1 + ")";

                            EmbeddedWEMFile wem = new EmbeddedWEMFile(wemData, $"{i}: {wemName}", spExport.Export, wemID);
                            AllWems.Add(wem);
                            i++;
                        }
                        bank.EmbeddedFiles.Clear();
                        bank.EmbeddedFiles.AddRange(AllWems.Select(wem => new KeyValuePair<uint, byte[]>(wem.Id, wem.HasBeenFixed ? wem.OriginalWemData : wem.WemData)));
                        ExportBank(spExport);
                    }
                }
            }
        }

        private void ExtractISACTAsRaw_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is ISACTFileEntry spExport)
            {
                ExportISACTEntryRaw(spExport);
            }
        }

        private void ExtractISACTAsWave_Clicked(object sender, RoutedEventArgs e)
        {

        }

        private static void ExportISACTEntryRaw(ISACTFileEntry spExport)
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "ISACT Audio|*.isa",
                FileName = spExport.Entry.FileName + ".isa",
                Title = "Select save location for ISACT file"
            };

            bool? res = d.ShowDialog();
            if (res.HasValue && res.Value)
            {
                //File.WriteAllBytes(d.FileName, spExport.Export.getBinaryData());
                File.WriteAllBytes(d.FileName, spExport.Entry.DataAsStored);
                MessageBox.Show("Done.");
            }
        }

        private void SoundplorerWPF_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(FileQueuedForLoad))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    LoadFile(FileQueuedForLoad);
                    FileQueuedForLoad = null;

                    if (BindedItemsList.FirstOrDefault(obj => obj is SoundplorerExport sExport && sExport.Export == ExportQueuedForFocusing) is SoundplorerExport soundExport)
                    {
                        SoundExports_ListBox.SelectedItem = soundExport;
                        SoundExports_ListBox.ScrollIntoView(soundExport);
                    }
                    ExportQueuedForFocusing = null;

                    Activate();
                }));
            }
        }

        public void PropogateRecentsChange(IEnumerable<string> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "Soundplorer";
    }

    public class AFCFileEntry : NotifyPropertyChangedBase
    {
        public bool ME2;

        private string _afcpath;
        private readonly Endian Endian;

        public string AFCPath
        {
            get => _afcpath;
            set => SetProperty(ref _afcpath, value);
        }

        private int _datasize;
        public int DataSize
        {
            get => _datasize;
            set => SetProperty(ref _datasize, value);
        }

        private int _offset;
        public int Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
        }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private EFontAwesomeIcon _icon;
        public EFontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        public AFCFileEntry(string afcpath, int offset, int size, bool ME2, Endian endian)
        {
            Endian = endian;
            AFCPath = afcpath;
            DataSize = size;
            this.ME2 = ME2;
            Offset = offset;
            SubText = "Calculating length";
            Icon = EFontAwesomeIcon.Solid_Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            DisplayString = $"AFC Entry @ 0x{Offset:X6}";
        }

        public void LoadData()
        {
            using FileStream _rawRiff = new FileStream(AFCPath, FileMode.Open);
            EndianReader reader = new EndianReader(_rawRiff) { Endian = Endian };
            reader.Position = Offset;
            //Parse RIFF header a bit
            var riffTag = reader.ReadStringASCII(4); //RIFF
            reader.ReadInt32();//size
            var wavetype = reader.ReadStringASCII(4);
            reader.ReadInt32();//'fmt '/
            var fmtsize = reader.ReadInt32(); //data should directly follow fmt
            var fmtPos = reader.Position;
            var riffFormat = reader.ReadUInt16();
            var channels = reader.ReadInt16();
            var sampleRate = reader.ReadInt32();
            var averageBPS = reader.ReadInt32();
            var blockAlign = reader.ReadInt16();
            var bitsPerSample = reader.ReadInt16();
            var extraSize = reader.ReadInt16(); //gonna need some testing on this cause there's a lot of header formats for wwise
            if (riffFormat == 0xFFFF)
            {
                double seconds = 0;

                //if (extraSize == 0x30 || extraSize == 0x06) //0x30 on PC, 0x06 on PS3 ?
                //{
                //find 'vorb' chunk (ME2)
                reader.Seek(extraSize, SeekOrigin.Current);
                var chunkName = reader.ReadStringASCII(4);
                uint numSamples = 1; //to prevent division by 0
                if (chunkName == "vorb")
                {
                    //ME2 Vorbis
                    var vorbsize = reader.ReadInt32();
                    numSamples = reader.ReadUInt32();
                }
                else if (chunkName == "data")
                {
                    //ME3 Vorbis
                    var numSamplesOffset = reader.Position = fmtPos + 0x18;
                    numSamples = reader.ReadUInt32();
                }

                seconds = (double)numSamples / sampleRate;
                //}
                //else
                //{
                //    // !!??
                //    Debug.WriteLine($"Unknown extra size in wwiseheader: 0x{extraSize:X2}");
                //}

                SubText = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\:fff");
            }
            else
            {
                //placeholder?
                SubText = new TimeSpan(132045).ToString(@"mm\:ss\:fff");
            }
            /*
            Stream waveStream = WwiseStream.CreateWaveStreamFromRaw(AFCPath, Offset, DataSize, ME2);
            if (waveStream != null)
            {
                //Check it is RIFF
                byte[] riffHeaderBytes = new byte[4];
                waveStream.Read(riffHeaderBytes, 0, 4);
                string wemHeader = "" + (char)riffHeaderBytes[0] + (char)riffHeaderBytes[1] + (char)riffHeaderBytes[2] + (char)riffHeaderBytes[3];
                if (wemHeader == "RIFF")
                {
                    waveStream.Position = 0;
                    WaveFileReader wf = new WaveFileReader(waveStream);
                    SubText = wf.TotalTime.ToString(@"mm\:ss\:fff");
                }
                else
                {
                    SubText = "Error getting length, may be unsupported";
                }
            }*/
            NeedsLoading = false;
            Icon = EFontAwesomeIcon.Solid_VolumeUp;
        }
    }

    public class ISACTFileEntry : NotifyPropertyChangedBase
    {
        public ISBankEntry Entry { get; set; }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private EFontAwesomeIcon _icon;
        public EFontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        public ISACTFileEntry(ISBankEntry entry)
        {
            Entry = entry;
            SubText = "Calculating stream length";
            Icon = EFontAwesomeIcon.Solid_Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            DisplayString = Entry.FileName;
        }

        public void LoadData()
        {
            // Check if there is TLK string in the export name
            var splits = Entry.FileName.Split('_', ',');
            for (int i = splits.Length - 1; i > 0; i--)
            {
                //backwards is faster
                if (int.TryParse(splits[i], out var parsed))
                {
                    //Lookup TLK
                    var data = TLKManagerWPF.GlobalFindStrRefbyID(parsed, MEGame.ME1, null);
                    if (data != "No Data")
                    {
                        Entry.TLKString = data;
                    }
                }
            }



            if (Entry.DataAsStored != null)
            {
                //Debug.WriteLine("getting time for " + Entry.FileName + " Ogg: " + Entry.isOgg);
                TimeSpan? time = Entry.GetLength();
                if (time != null)
                {
                    //here backslash must be present to tell that parser colon is
                    //not the part of format, it just a character that we want in output
                    SubText = time.Value.ToString(@"mm\:ss\:fff");
                }
                else
                {
                    SubText = "Error getting length, may be unsupported";
                }
            }
            else
            {
                SubText = "Sound stub only";
            }
            NeedsLoading = false;
            Icon = EFontAwesomeIcon.Solid_VolumeUp;
        }
    }

    public class SoundplorerExport : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; set; }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private EFontAwesomeIcon _icon;
        public EFontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        public string _tlkString;
        public string TLKString
        {
            get => _tlkString;
            set => SetProperty(ref _tlkString, value);
        }

        public SoundplorerExport(ExportEntry export)
        {
            Export = export;
            if (Export.ClassName == "WwiseStream")
            {
                SubText = "Calculating stream length";
            }
            else
            {
                SubText = "Calculating number of embedded WEMs";
            }
            Icon = EFontAwesomeIcon.Solid_Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            int paddingSize = Export.FileRef.ExportCount.ToString().Length;
            DisplayString = $"{Export.UIndex.ToString("d" + paddingSize)}: {Export.ObjectName.Instanced}";
        }

        public void LoadData()
        {
            switch (Export.ClassName)
            {
                case "WwiseStream":
                    {
                        // Check if there is TLK string in the export name
                        var splits = Export.ObjectName.Name.Split('_', ',');
                        for (int i = splits.Length - 1; i > 0; i--)
                        {
                            //backwards is faster
                            if (int.TryParse(splits[i], out var parsed))
                            {
                                //Lookup TLK
                                var data = TLKManagerWPF.GlobalFindStrRefbyID(parsed, Export.FileRef);
                                if (data != "No Data")
                                {
                                    TLKString = data;
                                }
                            }
                        }


                        WwiseStream w = Export.GetBinaryData<WwiseStream>();
                        if (!w.IsPCCStored && w.GetPathToAFC() == "")
                        {
                            //AFC not found.
                            SubText = $"AFC unavailable: {w.Filename}";
                        }
                        else
                        {
                            var length = w.GetAudioInfo()?.GetLength();
                            if (length != null)
                            {
                                //here backslash must be present to tell that parser colon is
                                //not the part of format, it just a character that we want in output
                                SubText = length.Value.ToString(@"mm\:ss\:fff");
                            }
                            else
                            {
                                SubText = "Error getting length, may be unsupported";
                            }
                        }

                        //string afcPath = w.GetPathToAFC();
                        //if (afcPath == "")
                        //{
                        //    SubText = "Could not find AFC";
                        //}
                        //else
                        //{
                        //    TimeSpan? time = w.GetSoundLength();
                        //    if (time != null)
                        //    {
                        //        //here backslash must be present to tell that parser colon is
                        //        //not the part of format, it just a character that we want in output
                        //        SubText = time.Value.ToString(@"mm\:ss\:fff");
                        //    }
                        //    else
                        //    {
                        //        SubText = "Error getting length, may be unsupported";
                        //    }
                        //}
                        NeedsLoading = false;
                        Icon = EFontAwesomeIcon.Solid_VolumeUp;
                        break;
                    }
                case "WwiseBank":
                    {
                        var bank = Export.GetBinaryData<WwiseBank>();
                        SubText = $"{bank.EmbeddedFiles.Count} embedded WEM{(bank.EmbeddedFiles.Count != 1 ? "s" : "")}";
                        NeedsLoading = false;
                        Icon = EFontAwesomeIcon.Solid_University;
                        break;
                    }
                case "SoundNodeWave":
                    SubText = "";
                    NeedsLoading = false;
                    Icon = EFontAwesomeIcon.Solid_VolumeUp;
                    break;
            }
        }
    }
}
