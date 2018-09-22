using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        new MEGame[] SupportedGames = new MEGame[] { MEGame.ME3 };

        WwiseStream w;
        public string afcPath = "";
        DispatcherTimer seekbarUpdateTimer = new DispatcherTimer();
        private bool SeekUpdatingDueToTimer = false;
        private bool SeekDragging = false;
        Stream vorbisStream;

        public Soundpanel()
        {
            PlayPauseImageSource = "/soundplorer/play.png";
            LoadCommands();
            CurrentVolume = 1;
            _playbackState = PlaybackState.Stopped;
            seekbarUpdateTimer.Interval = new TimeSpan(0, 0, 1);
            seekbarUpdateTimer.Tick += new EventHandler(this.UpdateSeekBarPos);
            InitializeComponent();
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            WwiseStream w = new WwiseStream(exportEntry);
            string s = "#" + exportEntry.Index + " WwiseStream : " + exportEntry.ObjectName + "\n\n";
            s += "Filename : \"" + w.FileName + "\"\n";
            s += "Data size: " + w.DataSize + " bytes\n";
            s += "Data offset: 0x" + w.DataOffset.ToString("X8") + "\n";
            s += "ID: 0x" + w.Id.ToString("X8") + " = " + w.Id + "\n";
            CurrentLoadedExport = exportEntry;
            infoTextBox.Text = s;
        }

        public override void UnloadExport()
        {
            //throw new NotImplementedException();
            //waveOut.Stop();
            //CurrentVorbisStream.Dispose();
            //_audioPlayer.Dispose();
            infoTextBox.Text = "Select an export";
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
            return (exportEntry.FileRef.Game == MEGame.ME3 && (exportEntry.ClassName == "WwiseBank" || exportEntry.ClassName == "WwiseStream"));
        }

        private void Pause_Clicked(object sender, RoutedEventArgs e)
        {
            WwiseStream w = new WwiseStream(CurrentLoadedExport.FileRef as ME3Package, CurrentLoadedExport.Index);

        }

        private void Play_Clicked(object sender, RoutedEventArgs e)
        {
            PlayLoadedExport();
        }

        public void PlayLoadedExport()
        {

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
                if (vorbisStream == null )
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
            //if (CurrentlySelectedTrack != null)
            //{
            //    return true;
            //}
            return true;
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

        private void ExportAsOgg(object sender, RoutedEventArgs e)
        {
            Stream vorbisStream = getPCMStream();
            vorbisStream.Position = 0;
            if (vorbisStream != null)
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "Ogg Vorbis |*.ogg";
                if (d.ShowDialog().Value)
                {
                    using (var fileStream = File.Create(d.FileName))
                    {
                        byte[] buffer = new byte[8 * 1024];
                        int len;
                        while ((len = vorbisStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fileStream.Write(buffer, 0, len);
                        }
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
    }
}
