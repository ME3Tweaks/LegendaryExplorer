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
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using NAudio.Vorbis;
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
            if (exportEntry.ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(exportEntry);
                ExportInformationList.Add("#" + exportEntry.Index + " WwiseStream : " + exportEntry.ObjectName);
                ExportInformationList.Add("Filename : " + w.FileName); ;
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
                foreach (var singleWemMetadata in embeddedWEMFiles)
                {
                    byte[] wemData = new byte[singleWemMetadata.Item3];
                    //copy WEM data to buffer. Add 0x8 to skip DATA and DATASIZE header for this block.
                    Buffer.BlockCopy(data, singleWemMetadata.Item2 + 0x8, wemData, 0, singleWemMetadata.Item3);
                    //check for RIFF header as some don't seem to have it and are not playable.
                    string wemHeader = "" + (char)wemData[0] + (char)wemData[1] + (char)wemData[2] + (char)wemData[3];
                    if (wemHeader == "RIFF")
                    {
                        EmbeddedWEMFile wem = new EmbeddedWEMFile(wemData, i + ": Embedded WEM 0x" + singleWemMetadata.Item1.ToString("X8"));
                        ExportInformationList.Add(wem);
                    }
                    else
                    {
                        ExportInformationList.Add(i + ": Embedded WEM 0x" + singleWemMetadata.Item1.ToString("X8") + " - No RIFF header");
                    }
                    i++;
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
                Debug.WriteLine("Unloading resources");
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

        private Stream getPCMStream()
        {
            if (CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.ClassName == "WwiseStream")
                {
                    w = new WwiseStream(CurrentLoadedExport);
                    string path;
                    if (w.IsPCCStored)
                    {
                        path = CurrentLoadedExport.FileRef.FileName;
                    }
                    else
                    {
                        path = w.getPathToAFC(); // only to check if AFC exists.
                    }
                    if (path != "")
                    {
                        return w.GetPCMStream(path);
                    }
                }
                if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    object currentWEMItem = ExportInfoListBox.SelectedItem;
                    if (currentWEMItem == null || currentWEMItem is string)
                    {
                        return null; //nothing selected, or current wem is not playable
                    }
                    var wemObject = (EmbeddedWEMFile)currentWEMItem;
                    string basePath = System.IO.Path.GetTempPath() + "ME3EXP_SOUND_" + Guid.NewGuid().ToString();
                    File.WriteAllBytes(basePath + ".dat", wemObject.WemData);
                    WwiseStream.ConvertRiffToWav(basePath + ".dat", CurrentLoadedExport.FileRef.Game == MEGame.ME2);
                    if (File.Exists(basePath + ".wav"))
                    {
                        byte[] pcmBytes = File.ReadAllBytes(basePath + ".wav");
                        File.Delete(basePath + ".wav");
                        return new MemoryStream(pcmBytes);
                    }
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

        public ICommand ExitApplicationCommand { get; set; }
        public ICommand AddFileToPlaylistCommand { get; set; }
        public ICommand AddFolderToPlaylistCommand { get; set; }
        public ICommand SavePlaylistCommand { get; set; }
        public ICommand LoadPlaylistCommand { get; set; }

        public ICommand RewindToStartCommand { get; set; }
        public ICommand StartPlaybackCommand { get; set; }
        public ICommand StopPlaybackCommand { get; set; }
        public ICommand ForwardToEndCommand { get; set; }
        public ICommand ShuffleCommand { get; set; }

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
            // Menu commands
            ExitApplicationCommand = new RelayCommand(ExitApplication, CanExitApplication);
            AddFileToPlaylistCommand = new RelayCommand(AddFileToPlaylist, CanAddFileToPlaylist);
            AddFolderToPlaylistCommand = new RelayCommand(AddFolderToPlaylist, CanAddFolderToPlaylist);
            SavePlaylistCommand = new RelayCommand(SavePlaylist, CanSavePlaylist);
            LoadPlaylistCommand = new RelayCommand(LoadPlaylist, CanLoadPlaylist);

            // Player commands
            RewindToStartCommand = new RelayCommand(RewindToStart, CanRewindToStart);
            StartPlaybackCommand = new RelayCommand(StartPlayback, CanStartPlayback);
            StopPlaybackCommand = new RelayCommand(StopPlayback, CanStopPlayback);

            // Event commands
            TrackControlMouseDownCommand = new RelayCommand(TrackControlMouseDown, CanTrackControlMouseDown);
            TrackControlMouseUpCommand = new RelayCommand(TrackControlMouseUp, CanTrackControlMouseUp);
            VolumeControlValueChangedCommand = new RelayCommand(VolumeControlValueChanged, CanVolumeControlValueChanged);
        }

        // Menu commands
        private void ExitApplication(object p)
        {

        }
        private bool CanExitApplication(object p)
        {
            return true;
        }

        private void AddFileToPlaylist(object p)
        {

        }
        private bool CanAddFileToPlaylist(object p)
        {
            return true;
        }

        private void AddFolderToPlaylist(object p)
        {

        }

        private bool CanAddFolderToPlaylist(object p)
        {
            return true;
        }

        private void SavePlaylist(object p)
        {

        }

        private bool CanSavePlaylist(object p)
        {
            return true;
        }

        private void LoadPlaylist(object p)
        {

        }

        private bool CanLoadPlaylist(object p)
        {
            return true;
        }

        // Player commands
        private void RewindToStart(object p)
        {

        }
        private bool CanRewindToStart(object p)
        {
            return true;
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
                if (vorbisStream != null)
                {
                    vorbisStream.Dispose();
                    vorbisStream = null;
                }
                _audioPlayer.PlaybackStopType = VorbisAudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser;
                _audioPlayer.Stop();
            }
        }
        private bool CanStopPlayback(object p)
        {
            if (_playbackState == PlaybackState.Playing || _playbackState == PlaybackState.Paused)
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
            _playbackState = PlaybackState.Paused;
            PlayPauseImageSource = "/soundplorer/play.png";
        }

        #endregion

        private void SoundPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Dispose();
            }
        }

        private void ExportAsWavePCM(object sender, RoutedEventArgs e)
        {
            if (CurrentLoadedExport.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "Wave PCM File |*.wav";
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

        private void ImportFromWave_Clicked(object sender, RoutedEventArgs e)
        {
            if (CurrentLoadedExport != null && CurrentLoadedExport.ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(CurrentLoadedExport);


                if (w.IsPCCStored)
                {
                    //TODO: enable replacing of PCC-stored sounds
                    MessageBox.Show("Cannot replace pcc-stored sounds.");
                    return;
                }
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*Wave PCM File|*.wav";
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    //if (path != "")
                    //{
                    //Status.Text = "Importing...";
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
        public EmbeddedWEMFile(byte[] WemData, string DisplayString)
        {
            this.WemData = WemData;
            this.DisplayString = DisplayString;
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
