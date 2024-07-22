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
using System.Windows.Threading;
using FontAwesome5;
using LegendaryExplorer.Audio;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Sound.ISACT;
using AudioStreamHelper = LegendaryExplorer.UnrealExtensions.AudioStreamHelper;
using WwiseStream = LegendaryExplorerCore.Unreal.BinaryConverters.WwiseStream;

namespace LegendaryExplorer.Tools.Soundplorer
{
    /// <summary>
    /// Interaction logic for SoundplorerWPF.xaml
    /// </summary>
    public partial class SoundplorerWPF : WPFBase, IBusyUIHost, IRecents
    {
        private string LoadedISBFile;
        private string LoadedAFCFile;
        BackgroundWorker backgroundScanner;
        public ObservableCollectionExtended<object> BindedItemsList { get; set; } = new();

        public bool AudioFileLoaded => Pcc != null || LoadedISBFile != null || LoadedAFCFile != null;

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
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, LoadFile);
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
            OpenFileDialog d = new()
            {
                Filter = "All supported files|*.pcc;*.u;*.sfm;*.upk;*.isb;*.afc;*.xxx|Package files|*.pcc;*.u;*.sfm;*.upk;*.xxx|ISACT Sound Bank files|*.isb|Audio File Cache (AFC)|*.afc",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
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
                RecentsController.AddRecent(fileName, false, Pcc?.Game);
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
            using (FileStream fileStream = new((string)e.Argument, FileMode.Open, FileAccess.Read))
            {
                // Get endianness
                Endian? endianness = null;

                if (fileStream.Position < fileStream.Length - 4)
                {
                    var firstMagic = fileStream.ReadStringLatin1(4);
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
                var reader = new EndianReader(fileStream) { Endian = endianness.Value };

                while (fileStream.Position < fileStream.Length - 4)
                {
                    int offset = (int)fileStream.Position;
                    TaskbarText = $"Loading AFC: {Path.GetFileName(LoadedAFCFile)} ({(int)((fileStream.Position * 100.0) / fileStream.Length)}%)";

                    string readStr = fileStream.ReadStringLatin1(4);
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

                    var entry = new AFCFileEntry(LoadedAFCFile, offset, size + 8, wwiseVersionMaybe, reader.Endian);
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
            if (e.Result is ISACTBank result)
            {
                // Get all sound samples as entries.
                var entries = new List<ISACTFileEntry>(result.GetAllBankChunks().Where(x => x.ChunkName == "data").Select(x => new ISACTFileEntry(x.GetParent() as ISACTListBankChunk)));
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
            using var fs = File.OpenRead((string)e.Argument);
            ISACTBank bank = new ISACTBank(fs);
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
                BindedItemsList.AddRange(Pcc.Exports.Where(e => e.ClassName is "WwiseBank" or "WwiseStream" or "SoundNodeWave").Select(x => new SoundplorerExport(x)));
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
            if (backgroundScanner is { IsBusy: true })
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
            backgroundScanner.RunWorkerAsync(exportsToReload?.Cast<object>().ToList() ?? BindedItemsList.ToList());
            IsBusyTaskbar = true;
            //string s = i.ToString("d6") + " : " + e.ClassName + " : \"" + e.ObjectName + "\"";
        }

        private void GetStreamTimes_ReportProgress(object sender, ProgressChangedEventArgs e)
        {
            IsBusyTaskbar = true; //enforce spinner
            TaskbarText = "Parsing " + Path.GetFileName(LoadedISBFile ?? LoadedAFCFile ?? Pcc?.FilePath) + " (" + e.ProgressPercentage + "%)";
        }

        private void GetStreamTimes_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarText = Path.GetFileName(LoadedISBFile ?? LoadedAFCFile ?? Pcc?.FilePath);
            IsBusyTaskbar = false;
        }

        private async void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            await Pcc.SaveAsync();
        }

        private async void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new()
            {
                Filter = $"*{extension}|*{extension}"
            };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                await Pcc.SaveAsync(d.FileName);
                MessageBox.Show("Done");
            }
        }

        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            if (LoadedISBFile != null || LoadedAFCFile != null)
            {
                return; //we don't handle updates on ISB or AFC
            }

            List<SoundplorerExport> bindedListAsCasted = BindedItemsList.Cast<SoundplorerExport>().ToList();
            List<PackageChange> changes = updates.Select(x => x.Change).ToList();

            var loadedUIndexes = bindedListAsCasted.Where(x => x.Export != null).Select(y => y.Export.UIndex).ToList();

            List<SoundplorerExport> exportsRequiringReload = new();
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

            if (Settings.Soundplorer_AutoplayEntriesOnSelection)
            {
                soundPanel.StartPlayingCurrentSelection();
            }
        }

        private void Soundplorer_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync();
            }
            soundPanel.FreeAudioResources();
            soundPanel.Dispose(); //Gets rid of WinForms control
            RecentsController?.Dispose();
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
                            var wem = new EmbeddedWEMFile(wemData, wemName, spExport.Export); //will correct truncated stuff
                            Stream waveStream = soundPanel.GetPCMStream(forcedWemFile: wem);
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
            SaveFileDialog d = new()
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
            new AFCCompactorWindow.AFCCompactorWindow().Show();
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
                SaveFileDialog d = new()
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
                using (Stream s = AudioStreamHelper.CreateWaveStreamFromRaw(afE.AFCPath, afE.Offset, afE.DataSize, afE.ME2))
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
                    SaveFileDialog d = new()
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
                        SaveFileDialog d = new()
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

                        SaveFileDialog d = new()
                        {
                            Filter = "Wwise WEM|*.wem",
                            FileName = presetfilename
                        };
                        if (d.ShowDialog() == true)
                        {
                            if (AudioStreamHelper.ExtractRawFromSourceToFile(d.FileName, afcEntry.AFCPath, afcEntry.DataSize, afcEntry.Offset))
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
                SaveFileDialog d = new()
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
                        MemoryStream oggStream = AudioStreamHelper.ConvertRIFFToWwiseOGG(riffOutputFile, spExport.Export.FileRef.Game == MEGame.ME2, spExport.Export.FileRef.Game > MEGame.ME3);
                        //string outputOggPath = 
                        if (oggStream != null)// && File.Exists(outputOggPath))
                        {
                            oggStream.Seek(0, SeekOrigin.Begin);
                            using (var fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
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
                SaveFileDialog d = new()
                {
                    Filter = "Ogg Vorbis|*.ogg",
                    FileName = presetfilename
                };
                if (d.ShowDialog() == true)
                {
                    string riffOutputFile = Path.Combine(Directory.GetParent(d.FileName).FullName, Path.GetFileNameWithoutExtension(d.FileName)) + ".dat";

                    if (AudioStreamHelper.ExtractRawFromSourceToFile(riffOutputFile, afE.AFCPath, afE.DataSize, afE.Offset))
                    {
                        MemoryStream oggStream = AudioStreamHelper.ConvertRIFFToWwiseOGG(riffOutputFile, afE.ME2, afE.IsLE());
                        //string outputOggPath = 
                        if (oggStream != null)// && File.Exists(outputOggPath))
                        {
                            oggStream.Seek(0, SeekOrigin.Begin);
                            using (var fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
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

        private async void CloneAndReplace(bool fromWave)
        {
            string result = PromptDialog.Prompt(this, "Enter a new object name for the cloned item.", "Cloned export name");
            if (result != null)
            {
                SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
                if (spExport != null && spExport.Export.ClassName == "WwiseStream")
                {
                    ExportEntry clone = EntryCloner.CloneEntry(spExport.Export);
                    clone.ObjectName = result;
                    var newExport = new SoundplorerExport(clone);
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
                        soundPanel.ReplaceAudioFromWwiseEncodedFile(forcedExport: clone);
                    }
                    else
                    {
                        await soundPanel.ReplaceAudioFromWave(forcedExport: clone);
                    }
                    LoadObjects(reloadList);
                }
            }
        }

        private void ReplaceAudio_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport)
            {
                soundPanel.ReplaceAudioFromWwiseEncodedFile(forcedExport: spExport.Export);
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
            WwiseCliHandler.DeleteTemplateProjectDirectory();

            // Shows wwise path dialog if no paths are set
            var pathCorrect = WwiseCliHandler.CheckWwisePathForGame(Pcc?.Game ?? MEGame.ME3);
            if (!pathCorrect) return;

            var dlg = new CommonOpenFileDialog("Select folder containing .wav files") { IsFolderPicker = true };
            if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok) { return; }

            string[] filesToConvert = Directory.GetFiles(dlg.FileName, "*.wav");
            if (!filesToConvert.Any())
            {
                MessageBox.Show("The selected folder does not contain any .wav files for converting.");
                return;
            }

            SoundReplaceOptionsDialog srod = new (showUpdateEvents: false);
            if (!srod.ShowDialog().Value) return;

            //Verify Wwise is installed with the correct version
            string wwisePath = WwiseCliHandler.GetWwiseCliPath(srod.ChosenSettings.TargetGame);
            if (wwisePath == null)
            {
                MessageBox.Show("Wwise path not set for specified game.");
                return; //abort. getpath is not silent so it will show dialogs before this is reached.
            }
            
            string convertedFolder = await WwiseCliHandler.RunWwiseConversion(srod.ChosenSettings.TargetGame, dlg.FileName, srod.ChosenSettings);
            MessageBox.Show("Done. Converted files have been placed into:\n" + convertedFolder);
        }

        private void SetWwisePaths_Clicked(object sender, RoutedEventArgs e)
        {
            SetWwisePathDialog swpd = new ();
            swpd.ShowDialog();
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
                                string outfile = Path.Combine(location, Path.GetFileNameWithoutExtension(ife.Entry.TitleInfo.Value) + ".wav");
                                MemoryStream ms = AudioStreamHelper.GetWaveStreamFromISBEntry(ife.Entry);
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

        private async void ReplaceAudioFromWav_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport)
            {
                await soundPanel.ReplaceAudioFromWave(forcedExport: spExport.Export);
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
            Settings.Soundplorer_ReverseIDDisplayEndianness = ReverseEndianDisplayOfIDs_MenuItem.IsChecked;
            Settings.Save();
        }

        private void AutoplayEntryOnSelection_MenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            AutoplayEntryOnSelection_MenuItem.IsChecked = !AutoplayEntryOnSelection_MenuItem.IsChecked;
            Settings.Soundplorer_AutoplayEntriesOnSelection = AutoplayEntryOnSelection_MenuItem.IsChecked;
            Settings.Save();
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

                            var wem = new EmbeddedWEMFile(wemData, $"{i}: {wemName}", spExport.Export, wemID);
                            AllWems.Add(wem);
                            i++;
                        }
                        bank.EmbeddedFiles.Empty(AllWems.Count);
                        bank.EmbeddedFiles.AddRange(AllWems.Select(wem => new KeyValuePair<uint, byte[]>(wem.Id, wem.HasBeenFixed ? wem.OriginalWemData : wem.WemData)));
                        ExportBank(spExport);
                    }
                }
            }
        }

        private void ExtractISACTAsWave_Clicked(object sender, RoutedEventArgs e)
        {
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

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "Soundplorer";
    }
}
