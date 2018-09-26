using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;
using ME3Explorer.Soundplorer;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using NAudio.Wave;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for Soundpanel.xaml
    /// </summary>
    public partial class Soundpanel : ExportLoaderControl
    {
        new MEGame[] SupportedGames = new MEGame[] { MEGame.ME2, MEGame.ME3 };
        public BindingList<object> ExportInformationList { get; set; }
        WwiseStream w;
        public string afcPath = "";
        DispatcherTimer seekbarUpdateTimer = new DispatcherTimer();
        private bool SeekUpdatingDueToTimer = false;
        private bool SeekDragging = false;
        Stream vorbisStream;
        //IMEPackage CurrentPackage; //used to tell when to update WwiseEvents list
        //private Dictionary<IExportEntry, List<Tuple<string, int, double>>> WemIdsToWwwiseEventIdMapping = new Dictionary<IExportEntry, List<Tuple<string, int, double>>>();

        public Soundpanel()
        {
            PlayPauseImageSource = "/soundplorer/play.png";
            ExportInformationList = new BindingList<object>();
            LoadCommands();
            CurrentVolume = 1;
            _playbackState = PlaybackState.Stopped;
            seekbarUpdateTimer.Interval = new TimeSpan(0, 0, 1);
            seekbarUpdateTimer.Tick += new EventHandler(this.UpdateSeekBarPos);
            InitializeComponent();
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            ExportInformationList.Clear();

            //Check if we need to first gather wwiseevents for wem IDing
            //Uncomment when HIRC stuff is implemented, if ever...
            /*if (exportEntry.FileRef != CurrentPackage)
            {
                //update
                WemIdsToWwwiseEventIdMapping.Clear();
                List<IExportEntry> wwiseEventExports = exportEntry.FileRef.Exports.Where(x => x.ClassName == "WwiseEvent").ToList();
                foreach (IExportEntry wwiseEvent in wwiseEventExports)
                {
                    StructProperty relationships = wwiseEvent.GetProperty<StructProperty>("Relationships");
                    IntProperty id = wwiseEvent.GetProperty<IntProperty>("Id");
                    FloatProperty DurationMilliseconds = wwiseEvent.GetProperty<FloatProperty>("DurationMilliseconds");

                    if (relationships != null)
                    {
                        ObjectProperty bank = relationships.GetProp<ObjectProperty>("Bank");
                        if (bank != null && bank.Value > 0)
                        {
                            //export in this file
                            List<Tuple<string, int, double>> bankWemInfosList;
                            Tuple<string, int, double> newData = new Tuple<string, int, double>(wwiseEvent.ObjectName, id.Value, DurationMilliseconds.Value);
                            if (WemIdsToWwwiseEventIdMapping.TryGetValue(exportEntry.FileRef.Exports[bank.Value - 1], out bankWemInfosList))
                            {
                                bankWemInfosList.Add(newData);
                            }
                            else
                            {
                                WemIdsToWwwiseEventIdMapping[exportEntry.FileRef.Exports[bank.Value - 1]] = new List<Tuple<string, int, double>>();
                                WemIdsToWwwiseEventIdMapping[exportEntry.FileRef.Exports[bank.Value - 1]].Add(newData);
                            }
                        }
                    }
                }

            }
            CurrentPackage = exportEntry.FileRef;*/
            ExportInformationList.Add("#" + exportEntry.Index + " " + exportEntry.ClassName + " : " + exportEntry.ObjectName);
            if (exportEntry.ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(exportEntry);
                ExportInformationList.Add("Filename : " + (w.FileName ?? "Stored in this PCC"));
                ExportInformationList.Add("Data size: " + w.DataSize + " bytes");
                ExportInformationList.Add("Data offset: 0x" + w.DataOffset.ToString("X8"));
                ExportInformationList.Add("ID: 0x" + w.Id.ToString("X8") + " = " + w.Id);
                CurrentLoadedExport = exportEntry;
            }
            if (exportEntry.ClassName == "WwiseBank")
            {
                WwiseBank wb = new WwiseBank(exportEntry);
                var embeddedWEMFiles = wb.GetWEMFilesMetadata();
                var data = wb.GetDataBlock();
                int i = 0;
                if (embeddedWEMFiles.Count > 0)
                {
                    foreach (var singleWemMetadata in embeddedWEMFiles)
                    {
                        byte[] wemData = new byte[singleWemMetadata.Item3];
                        //copy WEM data to buffer. Add 0x8 to skip DATA and DATASIZE header for this block.
                        Buffer.BlockCopy(data, singleWemMetadata.Item2 + 0x8, wemData, 0, singleWemMetadata.Item3);
                        //check for RIFF header as some don't seem to have it and are not playable.
                        string wemHeader = "" + (char)wemData[0] + (char)wemData[1] + (char)wemData[2] + (char)wemData[3];
                        string wemName = "Embedded WEM 0x" + singleWemMetadata.Item1.ToString("X8");// + "(" + singleWemMetadata.Item1 + ")";

                        /* //HIRC lookup, if I ever get around to supporting HIRC
                        List<Tuple<string, int, double>> wemInfo;
                        if (WemIdsToWwwiseEventIdMapping.TryGetValue(exportEntry, out wemInfo))
                        {
                            var info = wemInfo.FirstOrDefault(x => x.Item2 == singleWemMetadata.Item1); //item2 in x = ID, singleWemMetadata.Item1 = ID
                            if (info != null)
                            {
                                //have info
                                wemName = info.Item1;
                            }
                        }*/
                        if (wemHeader == "RIFF")
                        {
                            EmbeddedWEMFile wem = new EmbeddedWEMFile(wemData, i + ": " + wemName, CurrentLoadedExport.FileRef.Game, singleWemMetadata.Item1);
                            ExportInformationList.Add(wem);
                        }
                        else
                        {
                            ExportInformationList.Add(i + ": " + wemName + " - No RIFF header");
                        }
                        i++;
                    }
                }
                else
                {
                    ExportInformationList.Add("This soundbank has no embedded WEM files");
                }
                CurrentLoadedExport = exportEntry;
            }

        }

        public override void UnloadExport()
        {
            //throw new NotImplementedException();
            //waveOut.Stop();
            //CurrentVorbisStream.Dispose();
            //_audioPlayer.Dispose();
            //infoTextBox.Text = "Select an export";
            CurrentLoadedExport = null;
        }

        public void FreeAudioResources()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.PlaybackStopType = VorbisAudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser; //will prevent loop from restarting
                _audioPlayer.Stop();
                _audioPlayer.Dispose();
            }
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return ((exportEntry.FileRef.Game == MEGame.ME2 || exportEntry.FileRef.Game == MEGame.ME3) && (exportEntry.ClassName == "WwiseBank" || exportEntry.ClassName == "WwiseStream"));
        }

        private void Pause_Clicked(object sender, RoutedEventArgs e)
        {
            WwiseStream w = new WwiseStream(CurrentLoadedExport.FileRef as ME3Package, CurrentLoadedExport.Index);

        }

        /// <summary>
        /// Gets a PCM stream of data (WAV) from either teh currently loaded export or selected WEM
        /// </summary>
        /// <param name="forcedWemFile">WEM that we will force to get a stream for</param>
        /// <returns></returns>
        public Stream getPCMStream(IExportEntry forcedWwiseStreamExport = null, EmbeddedWEMFile forcedWemFile = null)
        {
            IExportEntry localCurrentExport = forcedWwiseStreamExport ?? CurrentLoadedExport;
            if (localCurrentExport != null || forcedWemFile != null)
            {
                if (localCurrentExport != null && localCurrentExport.ClassName == "WwiseStream")
                {
                    w = new WwiseStream(localCurrentExport);
                    string path;
                    if (w.IsPCCStored)
                    {
                        path = localCurrentExport.FileRef.FileName;
                    }
                    else
                    {
                        path = w.getPathToAFC(); // only to check if AFC exists.
                    }
                    if (path != "")
                    {
                        return w.CreateWaveStream(path);
                    }
                }
                if (forcedWemFile != null || (localCurrentExport != null && localCurrentExport.ClassName == "WwiseBank"))
                {
                    object currentWEMItem = forcedWemFile ?? ExportInfoListBox.SelectedItem;
                    if (currentWEMItem == null || currentWEMItem is string)
                    {
                        return null; //nothing selected, or current wem is not playable
                    }
                    var wemObject = (EmbeddedWEMFile)currentWEMItem;
                    string basePath = System.IO.Path.GetTempPath() + "ME3EXP_SOUND_" + Guid.NewGuid().ToString();
                    File.WriteAllBytes(basePath + ".dat", wemObject.WemData);
                    return WwiseStream.ConvertRiffToWav(basePath + ".dat", wemObject.Game == MEGame.ME2);
                }
            }
            return null;
        }



        #region MVVM stuff
        private string _playPauseImageSource;
        public string PlayPauseImageSource
        {
            get { return _playPauseImageSource; }
            set
            {
                if (value == _playPauseImageSource) return;
                _playPauseImageSource = value;
                OnPropertyChanged(nameof(PlayPauseImageSource));
            }
        }

        private string _title;
        private double _currentTrackLength;
        private double _currentTrackPosition;
        private float _currentVolume;
        private VorbisAudioPlayer _audioPlayer;
        public string Title
        {
            get { return _title; }
            set
            {
                if (value == _title) return;
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        public float CurrentVolume
        {
            get { return _currentVolume; }
            set
            {

                if (value.Equals(_currentVolume)) return;
                _currentVolume = value;
                OnPropertyChanged(nameof(CurrentVolume));
            }
        }

        public double CurrentTrackLength
        {
            get { return _currentTrackLength; }
            set
            {
                if (value.Equals(_currentTrackLength)) return;
                _currentTrackLength = value;
                OnPropertyChanged(nameof(CurrentTrackLength));
            }
        }

        public double CurrentTrackPosition
        {
            get { return _currentTrackPosition; }
            set
            {
                if (value.Equals(_currentTrackPosition)) return;
                _currentTrackPosition = value;
                SeekUpdatingDueToTimer = true;
                OnPropertyChanged(nameof(CurrentTrackPosition));
                SeekUpdatingDueToTimer = false;
            }
        }

        public ICommand ReplaceAudioCommand { get; set; }

        public ICommand ExportAudioCommand { get; set; }
        public ICommand StartPlaybackCommand { get; set; }
        public ICommand StopPlaybackCommand { get; set; }

        public ICommand TrackControlMouseDownCommand { get; set; }
        public ICommand TrackControlMouseUpCommand { get; set; }
        public ICommand VolumeControlValueChangedCommand { get; set; }
        private enum PlaybackState
        {
            Playing, Stopped, Paused
        }

        private PlaybackState _playbackState;

        private void LoadCommands()
        {
            // Player commands
            ReplaceAudioCommand = new RelayCommand(ReplaceAudio, CanReplaceAudio);
            ExportAudioCommand = new RelayCommand(ExportAudio, CanExportAudio);
            StartPlaybackCommand = new RelayCommand(StartPlayback, CanStartPlayback);
            StopPlaybackCommand = new RelayCommand(StopPlayback, CanStopPlayback);



            // Event commands
            TrackControlMouseDownCommand = new RelayCommand(TrackControlMouseDown, CanTrackControlMouseDown);
            TrackControlMouseUpCommand = new RelayCommand(TrackControlMouseUp, CanTrackControlMouseUp);
            VolumeControlValueChangedCommand = new RelayCommand(VolumeControlValueChanged, CanVolumeControlValueChanged);
        }

        private bool CanReplaceAudio(object obj)
        {
            if (CurrentLoadedExport == null) return false;
            if (CurrentLoadedExport.ClassName == "WwiseStream")
            {
                return CurrentLoadedExport.FileRef.Game == MEGame.ME3;
            }
            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                return false; //maybe someday...
            }
            return false;
        }

        private void ReplaceAudio(object obj)
        {
            ReplaceAudio();
        }

        // Player commands
        private void ExportAudio(object p)
        {
            if (CurrentLoadedExport.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "Wave PCM File|*.wav";
                d.FileName = CurrentLoadedExport.ObjectName + ".wav";
                if (d.ShowDialog().Value)
                {
                    WwiseStream w = new WwiseStream(CurrentLoadedExport);
                    string wavPath = w.CreateWave(w.getPathToAFC());
                    if (wavPath != null && File.Exists(wavPath))
                    {
                        File.Copy(wavPath, d.FileName, true);
                    }
                    MessageBox.Show("Done.");
                }
            }

            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                EmbeddedWEMFile currentWEMItem = (EmbeddedWEMFile)ExportInfoListBox.SelectedItem;
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "Wave PCM File|*.wav";
                d.FileName = CurrentLoadedExport.ObjectName + "_0x" + currentWEMItem.Id.ToString("X8") + ".wav";
                if (d.ShowDialog().Value)
                {

                    Stream ms = getPCMStream();
                    ms.Seek(0, SeekOrigin.Begin);
                    using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                    {
                        ms.CopyTo(fs);
                        fs.Flush();
                    }
                    MessageBox.Show("Done.");
                    MessageBox.Show("Done.");
                }
            }
        }
        private bool CanExportAudio(object p)
        {
            if (CurrentLoadedExport == null) return false;
            if (CurrentLoadedExport.ClassName == "WwiseStream") return true;
            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                object currentWEMItem = ExportInfoListBox.SelectedItem;
                return currentWEMItem != null && currentWEMItem is EmbeddedWEMFile;
            }
            return false;
        }

        private void StartPlayback(object p)
        {
            bool playToggle = true;
            if (_playbackState == PlaybackState.Stopped)
            {
                if (vorbisStream == null)
                {
                    vorbisStream = getPCMStream();
                }

                //check to make sure stream has loaded before we attempt to play it
                if (vorbisStream != null)
                {
                    try
                    {
                        vorbisStream.Position = 0;
                        _audioPlayer = new VorbisAudioPlayer(vorbisStream, CurrentVolume);
                        _audioPlayer.PlaybackStopType = VorbisAudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
                        _audioPlayer.PlaybackPaused += _audioPlayer_PlaybackPaused;
                        _audioPlayer.PlaybackResumed += _audioPlayer_PlaybackResumed;
                        _audioPlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                        CurrentTrackLength = _audioPlayer.GetLengthInSeconds();
                        playToggle = true;

                        // Start the timer.  Note that this call can be made from any thread.
                        seekbarUpdateTimer.Start();
                        // Timer callback code here...
                    }
                    catch (Exception)
                    {
                        //error playing audio or initializing
                        vorbisStream = null;
                        playToggle = false;
                    }

                    //_audioPlayer.Play(NAudio.Wave.PlaybackState.Stopped, CurrentVolume);
                    //CurrentlyPlayingTrack = CurrentlySelectedTrack;
                }
                else
                {
                    playToggle = false;
                }
            }

            if (playToggle)
            {
                _audioPlayer.TogglePlayPause(CurrentVolume);
            }
        }
        private void UpdateSeekBarPos(object state, EventArgs e)
        {
            if (!SeekDragging)
            {
                CurrentTrackPosition = _audioPlayer == null ? 0 : _audioPlayer.GetPositionInSeconds();
            }
        }


        private bool CanStartPlayback(object p)
        {
            if (vorbisStream != null) return true; //looping
            if (CurrentLoadedExport == null) return false;
            if (CurrentLoadedExport.ClassName == "WwiseStream") return true;
            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                object currentWEMItem = ExportInfoListBox.SelectedItem;
                if (currentWEMItem == null || currentWEMItem is string)
                {
                    return false; //nothing selected, or current wem is not playable
                }
                if (currentWEMItem is EmbeddedWEMFile) return true;
            }

            return false;
        }

        private void StopPlayback(object p)
        {
            seekbarUpdateTimer.Stop();
            if (_audioPlayer != null)
            {

                _audioPlayer.PlaybackStopType = VorbisAudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser;
                _audioPlayer.Stop();
            }
            if (vorbisStream != null)
            {
                vorbisStream.Dispose();
                vorbisStream = null;
            }
        }
        private bool CanStopPlayback(object p)
        {
            if (_playbackState == PlaybackState.Playing || _playbackState == PlaybackState.Paused || vorbisStream != null)
            {
                return true;
            }
            return false;
        }

        // Events
        private void TrackControlMouseDown(object p)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Pause();
            }
        }

        private void TrackControlMouseUp(object p)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.SetPosition(CurrentTrackPosition);
                _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
            }
        }

        private bool CanTrackControlMouseDown(object p)
        {
            if (_playbackState == PlaybackState.Playing)
            {
                return true;
            }
            return false;
        }

        private bool CanTrackControlMouseUp(object p)
        {
            if (_playbackState == PlaybackState.Paused)
            {
                return true;
            }
            return false;
        }

        private void VolumeControlValueChanged(object p)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.SetVolume(CurrentVolume); // set value of the slider to current volume
            }
        }

        private bool CanVolumeControlValueChanged(object p)
        {
            return true;
        }

        private void _audioPlayer_PlaybackStopped()
        {
            _playbackState = PlaybackState.Stopped;
            PlayPauseImageSource = "/soundplorer/play.png";

            CommandManager.InvalidateRequerySuggested();
            CurrentTrackPosition = 0;

            if (_audioPlayer.PlaybackStopType == VorbisAudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile)
            {
                StartPlayback(null);
            }
        }

        private void _audioPlayer_PlaybackResumed()
        {
            _playbackState = PlaybackState.Playing;
            PlayPauseImageSource = "/soundplorer/pause.png";
        }

        private void _audioPlayer_PlaybackPaused()
        {
            UpdateSeekBarPos(null, null);
            _playbackState = PlaybackState.Paused;
            PlayPauseImageSource = "/soundplorer/play.png";
        }

        #endregion

        /// <summary>
        /// Call this method when the soundpanel is being destroyed to release the audio and stop playback.
        /// </summary>
        public void Soundpanel_Unload()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Stop();
                _audioPlayer.Dispose();
            }
        }

        private void Seekbar_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            SeekDragging = true;
        }

        private void Seekbar_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (!SeekUpdatingDueToTimer)
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.SetPosition(CurrentTrackPosition);
                    _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
                }
            }
            SeekDragging = false;
        }

        private void Seekbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SeekUpdatingDueToTimer && !SeekDragging)
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.SetPosition(CurrentTrackPosition);
                    _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
                }
            }
        }

        /// <summary>
        /// Replaces the audio in the current loaded export, or the forced export. Will prompt user for a Wwise Encoded Ogg file.
        /// </summary>
        /// <param name="forcedExport">Export to update. If null, the currently loadedo ne is used instead.</param>
        public void ReplaceAudio(IExportEntry forcedExport = null)
        {
            IExportEntry exportToWorkOn = forcedExport ?? CurrentLoadedExport;
            if (exportToWorkOn != null && exportToWorkOn.ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(exportToWorkOn);
                if (w.IsPCCStored)
                {
                    //TODO: enable replacing of PCC-stored sounds
                    MessageBox.Show("Cannot replace pcc-stored sounds.");
                    return;
                }

                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "Wwise Encoded Ogg|*.ogg";
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    w.ImportFromFile(d.FileName, w.getPathToAFC());
                    CurrentLoadedExport.Data = w.memory.TypedClone();
                    //Status.Text = "Ready";
                    MessageBox.Show("Done");
                    //}
                }
            }
        }
    }

    public class EmbeddedWEMFile
    {
        public int Id;
        public bool HasBeenFixed;
        public MEGame Game;
        public EmbeddedWEMFile(byte[] WemData, string DisplayString, MEGame game, int Id = 0)
        {
            this.Id = Id;
            this.Game = game;
            this.WemData = WemData;
            this.DisplayString = DisplayString;


            int size = BitConverter.ToInt32(WemData, 4);
            int subchunk2size = BitConverter.ToInt32(WemData, 0x5A);

            if (size != WemData.Length - 8)
            {
                //Some clips in ME3 are just the intro to the audio. The raw data is literally cutoff and the first ~.5 seconds are inserted into the soundbank.
                //In order to attempt to even listen to these we have to fix the headers for size and subchunk2size.
                size = WemData.Length - 8;
                HasBeenFixed = true;
                this.DisplayString += " - Preloading";
                int offset = 4;
                WemData[offset] = (byte)size; // fourth byte
                WemData[offset + 1] = (byte)(size >> 8); // third byte
                WemData[offset + 2] = (byte)(size >> 16); // second byte
                WemData[offset + 3] = (byte)(size >> 24); // last byte

                offset = 0x5A; //Subchunk2 size offset
                size = WemData.Length - 94; //size of data to follow
                WemData[offset] = (byte)size; // fourth byte
                WemData[offset + 1] = (byte)(size >> 8); // third byte
                WemData[offset + 2] = (byte)(size >> 16); // second byte
                WemData[offset + 3] = (byte)(size >> 24); // last byte
            }
        }

        public byte[] WemData { get; set; }
        public string DisplayString { get; set; }
    }

    public class ImportExportSoundEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? false : true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
