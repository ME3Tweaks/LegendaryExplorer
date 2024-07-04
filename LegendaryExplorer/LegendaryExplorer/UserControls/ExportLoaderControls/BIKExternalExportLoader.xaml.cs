using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LibVLCSharp.Shared;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for MovieViewerTab.xaml
    /// </summary>
    public partial class BIKExternalExportLoader : ExportLoaderControl
    {
        #region Declarations
        private static readonly string[] parsableClasses = { "TextureMovie", "BioLoadingMovie", "BioSeqAct_MovieBink", "SFXInterpTrackMovieBink", "SFXSeqAct_PlatformMovieBink" };
        private bool _radIsInstalled;
        public LibVLC libvlc;
        public MediaPlayer mediaPlayer;
        public ICommand OpenFileInRADCommand { get; private set; }
        public ICommand ImportBikFileCommand { get; private set; }
        public ICommand PlayBikInVLCCommand { get; private set; }
        public ICommand PauseVLCCommand { get; private set; }
        public ICommand StopVLCCommand { get; private set; }
        public ICommand RewindVLCCommand { get; private set; }
        public ICommand ExtractBikCommand { get; private set; }
        public bool RADIsInstalled
        {
            get => _radIsInstalled;
            set
            {
                SetProperty(ref _radIsInstalled, value);
                OnPropertyChanged(nameof(RADNotInstalled));
            }
        }
        public bool RADNotInstalled => !RADIsInstalled;
        private bool _isexternallyCached;
        public bool IsExternallyCached { get => _isexternallyCached; set => SetProperty(ref _isexternallyCached, value); }
        private bool _islocallyCached;
        public bool IsLocallyCached { get => _islocallyCached; set => SetProperty(ref _islocallyCached, value); }
        private bool _isexternalFile;
        public bool IsExternalFile { get => _isexternalFile; set => SetProperty(ref _isexternalFile, value); }
        private string _tfcName;
        public string TfcName
        {
            get => _tfcName;
            set
            {
                SetProperty(ref _tfcName, value);
                OnPropertyChanged(nameof(TfcName));
            }
        }
        private string _bikfileName;
        public string BikFileName { get => _bikfileName; set => SetProperty(ref _bikfileName, value); }
        private bool _isvlcPlaying;
        public bool IsVLCPlaying { get => _isvlcPlaying; set => SetProperty(ref _isvlcPlaying, value); }
        private int _sizeX;
        public int SizeX { get => _sizeX; set => SetProperty(ref _sizeX, value); }
        private int _sizeY;
        public int SizeY { get => _sizeY; set => SetProperty(ref _sizeY, value); }
        private bool _showInfo;
        public bool ShowInfo { get => _showInfo; set => SetProperty(ref _showInfo, value); }
        public ObservableCollectionExtended<string> AvailableTFCNames { get; } = new();
        private string RADExecutableLocation;
        private bool IsExportable()
        {
            return !IsExternalFile;
        }
        private bool CanSwitchFromLocalToExternal()
        {
            return CurrentLoadedExport != null && CurrentLoadedExport.Game.IsGame3() && IsLocallyCached;
        }
        private bool IsMoviePlaying()
        {
            return IsBink1 && IsVLCPlaying;
        }
        private bool IsMovieStopped()
        {
            return IsBink1 && !IsVLCPlaying;
        }

        public bool ViewerModeOnly
        {
            get => (bool)GetValue(ViewerModeOnlyProperty);
            set => SetValue(ViewerModeOnlyProperty, value);
        }

        private uint _movieCRC;
        public uint MovieCRC
        {
            get => _movieCRC;
            set => SetProperty(ref _movieCRC, value);
        }

        private bool _isBink1;
        public bool IsBink1
        {
            get => _isBink1;
            set
            {
                if (SetProperty(ref _isBink1, value))
                {
                    OnPropertyChanged(nameof(BinkVersion));
                }
            }
        }

        public int BinkVersion => IsBink1 ? 1 : 2;

        /// <summary>
        /// Set to true to hide all of the editor controls
        /// </summary>
        public static readonly DependencyProperty ViewerModeOnlyProperty = DependencyProperty.Register(
            nameof(ViewerModeOnly), typeof(bool), typeof(BIKExternalExportLoader), new PropertyMetadata(false, ViewerModeOnlyCallback));

        private static void ViewerModeOnlyCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            BIKExternalExportLoader i = (BIKExternalExportLoader)obj;
            i.OnPropertyChanged(nameof(ViewerModeOnly));
        }

        private const string MOVE_TO_EXTERNAL_STRING = "<Move Bik from Local Pcc to External Cache>";
        private const string MOVE_TO_LOCAL_STRING = "<Move Bik from TFC cache to Local Pcc>";
        private const string STORE_LOCAL_STRING = "<Store new bik Locally>";
        private const string NEW_TFC_STRING = "<Create New Movie TFC>";
        private const string ADD_TFC_STRING = "<Add Existing Movie TFC>";

        #endregion

        #region StartUp
        public BIKExternalExportLoader() : base("BIKExternal")
        {
            DataContext = this;
            GetRADInstallationStatus();
            LoadCommands();
            InitializeComponent();

            libvlc = new LibVLC();
            mediaPlayer = new MediaPlayer(libvlc);

            //MoviePlayer.VlcMediaplayerOptions = new[] { "--video-title-show" };  //Can we find options to show frame counts/frame rates/time etc

            vlcVideoView.Loaded += VideoView_Loaded;
            TextureCacheComboBox.SelectionChanged += TextureCacheComboBox_SelectionChanged;

            mediaPlayer.Playing += OnPlaying;
            mediaPlayer.Stopped += OnStopped;
            mediaPlayer.EncounteredError += OnEncounteredError;
            mediaPlayer.EndReached += MediaEndReached;
        }

        private void OnEncounteredError(object sender, EventArgs e)
        {
            Console.Error.Write("An error occurred");
            IsVLCPlaying = false;
        }

        private void OnStopped(object sender, EventArgs e)
        {
            IsVLCPlaying = false;
            Debug.WriteLine("BikMoviePlayer Stopped");
        }

        private void OnPlaying(object sender, EventArgs e)
        {
            IsVLCPlaying = true;
            Debug.WriteLine("BikMoviePlayer Started");
        }

        public BIKExternalExportLoader(bool autoplayPopout, bool showcontrols = false) : base("BIKExternal")
        {
            if (!showcontrols)
            {
                bikcontrols_Panel.Visibility = Visibility.Collapsed;
            }
            //Add autoplay in VLC window
            if (autoplayPopout)
            {
                PlayExportInVLC();
            }
        }
        private void GetRADInstallationStatus()
        {
            if (RADIsInstalled) return;
            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\RAD Game Tools\RADVideo\");
                if (key?.GetValue("InstallDir") is string InstallDir)
                {
                    RADExecutableLocation = Path.Combine(InstallDir, "binkplay.exe");
                    RADIsInstalled = true;
                    return;
                }
            }
            catch
            {
                // ignored
            }

            RADIsInstalled = false;
            RADExecutableLocation = null;
        }

        private void LoadCommands()
        {
            OpenFileInRADCommand = new GenericCommand(OpenExportInRAD, () => RADIsInstalled);
            ImportBikFileCommand = new GenericCommand(ImportBikFileAction, IsExportable);
            PlayBikInVLCCommand = new GenericCommand(PlayExportInVLC, IsMovieStopped);
            PauseVLCCommand = new GenericCommand(PauseMoviePlayer, IsMoviePlaying);
            RewindVLCCommand = new GenericCommand(RewindMoviePlayer, IsMoviePlaying);
            StopVLCCommand = new GenericCommand(StopMoviePlayer, IsMoviePlaying);
            ExtractBikCommand = new GenericCommand(SaveBikToFile, IsExportable);
        }
        public override bool CanParse(ExportEntry exportEntry) => parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.IsDefaultObject;

        private void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            vlcVideoView.MediaPlayer = mediaPlayer;
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            MovieCRC = 0; //reset
            IsBink1 = false;
            Loading_text.Visibility = Visibility.Visible;
            Warning_text.Visibility = Visibility.Collapsed;
            Unsupported_Text.Visibility = Visibility.Collapsed;
            CurrentLoadedExport = exportEntry;
            AvailableTFCNames.ClearEx();
            AvailableTFCNames.Add(STORE_LOCAL_STRING);
            if (CurrentLoadedExport.Game.IsGame3())
            {
                AvailableTFCNames.Add(NEW_TFC_STRING);
                AvailableTFCNames.Add(ADD_TFC_STRING);
                AvailableTFCNames.AddRange(exportEntry.FileRef.Names.Where(x => x.StartsWith("Textures_DLC_") || x.StartsWith("Movies_DLC_")));
            }

            GetBikProps();
            if (AvailableTFCNames.All(x => x != TfcName))
            {
                AvailableTFCNames.Add(TfcName);
            }
            TextureCacheComboBox.SelectedItem = TfcName;
            Task.Run(() =>
            {
                try
                {
                    var movieBytes = GetMovieBytes();
                    if (movieBytes != null)
                    {
                        MovieCRC = TextureCRC.Compute(movieBytes);
                        IsBink1 = movieBytes[0] == 'B';
                    }
                    else
                    {
                        Warning_text.Visibility = Visibility.Visible;
                    }
                }
                catch
                {
                    //
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                Loading_text.Visibility = Visibility.Collapsed;
                mediaPlayer.Stop();
                var bik = mediaPlayer.Media;
                if (bik != null)
                {
                    mediaPlayer.Media = null;
                    bik.Dispose();
                }
                if (IsBink1)
                {
                    video_Panel.IsEnabled = true;
                }
                else
                {
                    Unsupported_Text.Visibility = Visibility.Visible;
                    video_Panel.IsEnabled = false;
                }
            });
        }
        public override void UnloadExport()
        {
            MovieCRC = 0;
            IsBink1 = false;
            mediaPlayer.Stop();
            var bik = mediaPlayer.Media;
            if (bik != null)
            {
                mediaPlayer.Media = null;
                bik.Dispose();
            }
            CurrentLoadedExport = null;
            Warning_text.Visibility = Visibility.Collapsed;
            Unsupported_Text.Visibility = Visibility.Collapsed;
            video_Panel.IsEnabled = true;
        }
        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new (new BIKExternalExportLoader(), CurrentLoadedExport)
                {
                    Title = $"BIK Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            UnloadExport();

            vlcVideoView.Loaded -= VideoView_Loaded;
            TextureCacheComboBox.SelectionChanged -= TextureCacheComboBox_SelectionChanged;

            mediaPlayer.Playing -= OnPlaying;
            mediaPlayer.Stopped -= OnStopped;
            mediaPlayer.EncounteredError -= OnEncounteredError;
            mediaPlayer.EndReached -= MediaEndReached;
            mediaPlayer?.Dispose();
            libvlc?.Dispose();
        }

        private void GetBikProps()
        {
            IsExternallyCached = false;
            IsExternalFile = false;
            IsLocallyCached = false;
            TfcName = "None";
            BikFileName = "No file";
            var props = CurrentLoadedExport.GetProperties();
            if (CurrentLoadedExport.ClassName == "TextureMovie")
            {
                var Xprop = props.GetProp<IntProperty>("SizeX");
                SizeX = Xprop?.Value ?? 0;
                var Yprop = props.GetProp<IntProperty>("SizeY");
                SizeY = Yprop?.Value ?? 0;
                var tfcprop = props.GetProp<NameProperty>("TextureFileCacheName");
                if (tfcprop == null)
                {
                    if (CurrentLoadedExport.Game.IsGame3())
                    {
                        AvailableTFCNames.Insert(0, MOVE_TO_EXTERNAL_STRING);
                    }
                    IsLocallyCached = true;
                    TfcName = STORE_LOCAL_STRING;
                    return;
                }
                AvailableTFCNames.Insert(0, MOVE_TO_LOCAL_STRING);
                IsExternallyCached = true;
                TfcName = tfcprop.Value;
            }
            else
            {
                string propbikName = "m_sMovieName";
                if (CurrentLoadedExport.ClassName == "BioLoadingMovie")
                {
                    propbikName = "MovieName";
                }
                var bikprop = props.GetProp<StrProperty>(propbikName);
                if (bikprop != null)
                {
                    BikFileName = bikprop.ToString();
                    if (BikFileName.EndsWith(".bik", true, System.Globalization.CultureInfo.InvariantCulture))
                        BikFileName = BikFileName.Replace(".bik", "", true, System.Globalization.CultureInfo.InvariantCulture);
                    IsExternalFile = true;
                }
            }
        }
        #endregion

        #region Playback
        private void OpenExportInRAD()
        {
            try
            {
                string moviePath;
                if(IsExternalFile)
                {
                    string bikName = BikFileName + ".bik";
                    moviePath = Path.GetDirectoryName(Path.GetDirectoryName(CurrentLoadedExport.FileRef.FilePath));
                    moviePath = Path.Combine(moviePath, "Movies", bikName);
                    if (!File.Exists(moviePath))
                    {
                        string rootPath = MEDirectories.GetDefaultGamePath(CurrentLoadedExport.Game);
                        moviePath = Directory.GetFiles(rootPath, bikName, SearchOption.AllDirectories).FirstOrDefault();
                    }
                }
                else
                {
                    byte[] data = GetMovieBytes();
                    moviePath = Path.Combine(Path.GetTempPath(), CurrentLoadedExport.FullPath + ".bik");

                    File.WriteAllBytes(moviePath, data);
                }

                if (!File.Exists(moviePath))
                    MessageBox.Show("bik movie not found.");
                else
                {
                    var process = new Process
                    {
                        StartInfo =
                    {
                        FileName = RADExecutableLocation,
                        Arguments = $"\"{moviePath}\" /P"
                    }
                    };
                    process.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error launching RADTools: " + ex.FlattenException());
                MessageBox.Show("Error launching RADTools:\n\n" + ex.FlattenException());
            }
        }

        private void PlayExportInVLC()
        {
            if (mediaPlayer.IsPlaying)
            {
                mediaPlayer.Pause();
            }
            else
            {
                var stream = GetMovieStream();
                var bikMovie = new Media(libvlc, new StreamMediaInput(stream));
                mediaPlayer.Play(bikMovie);
            }
            IsVLCPlaying = true;
        }

        private void PauseMoviePlayer()
        {
            IsVLCPlaying = false;
            mediaPlayer.Pause();
        }
        private void RewindMoviePlayer() => mediaPlayer.Position = 0;

        private void StopMoviePlayer()
        {
            mediaPlayer.Stop();
            var bik = mediaPlayer.Media;
            if (bik != null)
            {
                mediaPlayer.Media = null;
                bik.Dispose();
            }
        }

        private byte[] GetMovieBytes()
        {
            try
            {
                if (IsExternalFile)
                {
                    string filename = $"{BikFileName}.bik";
                    string rootPath = MEDirectories.GetDefaultGamePath(Pcc.Game);
                    if (rootPath == null || !Directory.Exists(rootPath))
                    {
                        MessageBox.Show($"{Pcc.Game} has not been found. Please check your Legendary Explorer settings");
                        return null;
                    }

                    string filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                    if (filePath == null || !File.Exists(filePath))
                    {
                        MessageBox.Show($"Bik file {BikFileName}.bik has not been found.");
                        return null;
                    }

                    using FileStream fs = new (filePath, FileMode.Open, FileAccess.Read);
                    return fs.ReadToBuffer(fs.Length);
                }
                else
                {
                    var binary = CurrentLoadedExport.GetBinaryData<TextureMovie>();
                    if (IsExternallyCached)
                    {
                        var tfcprop = CurrentLoadedExport.GetProperty<NameProperty>("TextureFileCacheName");
                        string filename = $"{tfcprop.Value}.tfc";

                        string rootPath = MEDirectories.GetDefaultGamePath(Pcc.Game);
                        if (rootPath == null || !Directory.Exists(rootPath))
                        {
                            MessageBox.Show($"{Pcc.Game} has not been found. Please check your Legendary Explorer settings");
                            return null;
                        }

                        string filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                        if (filePath == null || !File.Exists(filePath))
                        {
                            MessageBox.Show($"Movie cache {filename} has not been found.");
                            return null;
                        }

                        int length = binary.DataSize;
                        int offset = binary.DataOffset;

                        using FileStream fs = new (filePath, FileMode.Open, FileAccess.Read);
                        fs.Seek(offset, SeekOrigin.Begin);
                        int bikend = offset + length;

                        if (bikend > fs.Length)
                            throw new Exception("tfc corrupt");

                        byte[] bikBytes = fs.ReadToBuffer(length);
#if DEBUG
                        Debug.WriteLine($"Length: {length:#,#,0}");
                        Debug.WriteLine($"Offset (bik start): {offset:#,#,0}");
                        Debug.WriteLine($"Bik End at: {bikend:#,#,0}");
                        Debug.WriteLine($"File End at: {fs.Length:#,#,0}");
                        Debug.WriteLine($"Bik ms size: {bikBytes.Length:#,#,0} Should be same as length {length:#,#,0}");
#endif
                        return bikBytes;
                    }
                    else if (IsLocallyCached) //is locally contained
                    {
                        byte[] bikBytes = binary.EmbeddedData;
                        if (bikBytes.Length == 0)
                        {
                            MessageBox.Show($"Embedded texture movie has not been found.");
                            return null;
                        }

                        return bikBytes;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                // Should we do something here?
                Debug.WriteLine($"Error loading movie: {e.Message}");
            }

            return null;
        }

        private MemoryStream GetMovieStream()
        {
            var movieBytes = GetMovieBytes();
            if (movieBytes != null)
            {
                return new MemoryStream(movieBytes);
            }
            else
            {
                Warning_text.Visibility = Visibility.Visible;
                video_Panel.IsEnabled = false;
            }

            return null;
        }

        public async void MediaEndReached(object sender, EventArgs args)
        {
            Debug.WriteLine("Reached End");
            var mediaplayer = sender as MediaPlayer;
            await Task.Run(() => mediaPlayer.Position = 0);
            IsVLCPlaying = false;
        }

        #endregion

        #region usertools
        private void SaveBikToFile()
        {
            bool saved = false;
            SaveFileDialog d = new()
            {
                Filter = "Bik Movie File (*.bik) |*.bik",
                FileName = $"{CurrentLoadedExport.ObjectName.Instanced}.bik"
            };
            if (d.ShowDialog() == true)
            {
                saved = ExportBikToFile(d.FileName);
            }

            if (saved)
            {
                MessageBox.Show("Saved");
            }
        }
        private bool ExportBikToFile(string bikfile)
        {
            var bikBytes = GetMovieBytes();
            if (bikBytes != null)
            {
                using (FileStream fs = new (bikfile, FileMode.Create))
                {
                    fs.WriteFromBuffer(bikBytes);
                }
                return true;
            }
            return false;
        }
        private void ImportBikFileAction()
        {
            ImportBikFile();
        }
        private bool ImportBikFile()
        {
            bool success = false;
            var dlg = new OpenFileDialog
            {
                //FileName = "Select a bik file",
                Title = "Import Bik movie file",
                Filter = "Bik Movie Files (*.bik)|*.bik",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };

            if (dlg.ShowDialog() == true && File.Exists(dlg.FileName))
            {
                success = ImportBiktoCache(dlg.FileName);
                if (success)
                {
                    MessageBox.Show("Done");
                }
            }
            return success;
        }
        private bool ImportBiktoCache(string bikfile, string tfcPath = null)
        {
            if (bikfile == null)
                return false;

            if (IsMoviePlaying())
            {
                mediaPlayer.Stop();
            }
            bikcontrols_Panel.IsEnabled = false; //stop user playing 

            var bikMovie = new MemoryStream();
            using (FileStream fs = new (bikfile, FileMode.OpenOrCreate, FileAccess.Read))
            {
                fs.CopyTo(bikMovie);
            }
            bikMovie.Seek(0, SeekOrigin.Begin);
            if (bikMovie.ReadStringASCII(3) is "KB2" && Pcc.Game.IsOTGame())
            {
                MessageBox.Show($"{Path.GetFileName(bikfile)} is a Bink2 file! {Pcc.Game} only supports Bink 1. Aborting.", "Warning", MessageBoxButton.OK);
                bikcontrols_Panel.IsEnabled = true;
                return false;
            }
            bikMovie.Position += 17;
            SizeX = bikMovie.ReadInt32();
            SizeY = bikMovie.ReadInt32();
            bikMovie.Seek(0, SeekOrigin.Begin);
            if (IsLocallyCached) //Append to local object
            {
                if (bikMovie.Length > int.MaxValue)
                {
                    MessageBox.Show($"{Path.GetFileName(bikfile)} is too large to attach to an object. Aborting.", "Warning", MessageBoxButton.OK);
                    bikcontrols_Panel.IsEnabled = true;
                    return false;
                }

                var props = CurrentLoadedExport.GetProperties();
                props.AddOrReplaceProp(new IntProperty(SizeX, "SizeX"));
                props.AddOrReplaceProp(new IntProperty(SizeY, "SizeY"));
                props.RemoveNamedProperty("TextureFileCacheName");
                props.RemoveNamedProperty("TFCFileGuid");
                props.AddOrReplaceProp(new EnumProperty("MovieStream_Memory", "EMovieStreamSource", CurrentLoadedExport.Game, "MovieStreamSource"));

                CurrentLoadedExport.WritePropertiesAndBinary(props, new TextureMovie
                {
                    IsExternal = false,
                    EmbeddedData = bikMovie.ToArray()
                });
            }
            else if (IsExternallyCached) //Append to tfc  NOT ME2/ME1
            {
                if (!Pcc.Game.IsGame3())
                {
                    MessageBox.Show("Only Game 3 can store movietextures in a cache file.");
                    bikcontrols_Panel.IsEnabled = true;
                    return false;
                }

                if (!(TfcName.Contains("Movies_DLC_MOD_") || TfcName.Contains("Textures_DLC_MOD_")))
                {
                    MessageBox.Show("Cannot replace movies into a TFC provided by BioWare. Choose a different target TFC from the list.");
                    bikcontrols_Panel.IsEnabled = true;
                    return false;
                }

                if (tfcPath == null || !File.Exists(tfcPath))
                {
                    string filename = $"{TfcName}.tfc";
                    string rootPath = MEDirectories.GetDefaultGamePath(Pcc.Game);
                    if (rootPath == null || !Directory.Exists(rootPath))
                    {
                        MessageBox.Show($"{Pcc.Game} has not been found. Please check your Legendary Explorer settings");
                        bikcontrols_Panel.IsEnabled = true;
                        return false;
                    }

                    List<string> tfcPaths = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).ToList();
                    switch (tfcPaths.Count)
                    {
                        case 0:
                            tfcPath = CreateNewMovieTFC();
                            if (tfcPath == null)
                            {
                                MessageBox.Show("Error. New tfc not created.");
                                return false;
                            }
                            break;
                        case 1:
                            tfcPath = tfcPaths[0];
                            break;
                        default:
                            MessageBox.Show($"Error. More than one tfc with this name was found in the {Pcc.Game} folders. TFC names need to be unique.");
                            return false;
                    }
                    TfcName = Path.GetFileNameWithoutExtension(tfcPath);
                }

                Guid tfcGuid;
                byte[] bikarray = bikMovie.ToArray();
                int biklength = bikarray.Length;
                int bikoffset;
                using (FileStream fs = new (tfcPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    tfcGuid = fs.ReadGuid();
                    fs.Seek(0, SeekOrigin.End);
                    bikoffset = (int)fs.Position;
                    fs.Write(bikarray, 0, biklength);
                }

                var props = CurrentLoadedExport.GetProperties();
                props.AddOrReplaceProp(new IntProperty(SizeX, "SizeX"));
                props.AddOrReplaceProp(new IntProperty(SizeY, "SizeY"));
                props.AddOrReplaceProp(new NameProperty(TfcName, "TextureFileCacheName"));
                props.AddOrReplaceProp(tfcGuid.ToGuidStructProp("TFCFileGuid"));
                props.AddOrReplaceProp(new EnumProperty("MovieStream_File", "EMovieStreamSource", CurrentLoadedExport.Game, "MovieStreamSource"));

                CurrentLoadedExport.WritePropertiesAndBinary(props, new TextureMovie
                {
                    IsExternal = true,
                    DataSize = biklength,
                    DataOffset = bikoffset
                });
            }

            bikcontrols_Panel.IsEnabled = true; //unlock play
            return true;
        }
        private void TextureCacheComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems == null || e.RemovedItems == null || e.AddedItems.Count == 0 || e.RemovedItems.Count == 0 || e.AddedItems == e.RemovedItems)
                return;
            TextureCacheComboBox.SelectionChanged -= TextureCacheComboBox_SelectionChanged; //stop duplicate events on reversion
            var oldselection = (string)e.RemovedItems[0];
            string newSelection = TextureCacheComboBox.SelectedItem.ToString();
            var olocalcache = IsLocallyCached;  //Backup old data in case of cancellation.
            var oextcache = IsExternallyCached;
            var oTfcName = TfcName;
            bool wasCancelled = false;
            switch (newSelection)
            {
                case MOVE_TO_EXTERNAL_STRING: //Before was local move to external
                    var adlg = MessageBox.Show($"Do you want to move the bik at {CurrentLoadedExport.ObjectNameString} to an external tfc?", "Move to External", MessageBoxButton.OKCancel);
                    if (adlg == MessageBoxResult.OK)
                    {
                        string newtfc = null;
                        var meChkdlg = MessageBox.Show($"Do you want to use an existing tfc?\n (Select No to create a new one.)", "Move to External", MessageBoxButton.YesNo);
                        if (meChkdlg == MessageBoxResult.No)
                        {
                            newtfc = CreateNewMovieTFC();
                            if (newtfc == null)
                            {
                                TextureCacheComboBox.SelectedItem = oldselection;
                                break;
                            }
                            TfcName = Path.GetFileNameWithoutExtension(newtfc);
                        }
                        else
                        {
                            var possibleTFCs = AvailableTFCNames.Where(x => x.StartsWith("Movies_DLC_MOD_") || x.StartsWith("Textures_DLC_MOD_")).ToList();
                            var owner = Window.GetWindow(this);
                            var tdlg = InputComboBoxWPF.GetValue(owner, "Select a Movie TFC", "TFC Selector", possibleTFCs);
                            if (tdlg == null)
                            {
                                TextureCacheComboBox.SelectedItem = oldselection;
                                break;
                            }

                            string rootPath = MEDirectories.GetDefaultGamePath(Pcc.Game);
                            if (rootPath == null || !Directory.Exists(rootPath))
                            {
                                MessageBox.Show($"{Pcc.Game} has not been found. Please check your Legendary Explorer settings");
                                TextureCacheComboBox.SelectedItem = oldselection;
                                break;
                            }

                            string filename = $"{tdlg}.tfc";
                            newtfc = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                            if (newtfc == null || !File.Exists(newtfc))
                            {
                                MessageBox.Show($"TFC {tdlg}.tfc has not been found.");
                                TextureCacheComboBox.SelectedItem = oldselection;
                                break;
                            }
                            TfcName = tdlg;
                        }
                        SwitchLocalToExternal(newtfc);
                    }
                    else
                    {
                        TextureCacheComboBox.SelectedItem = oldselection;
                    }
                    break;
                case MOVE_TO_LOCAL_STRING: //Before was external move to local
                    var swELdlg = MessageBox.Show($"Do you want to move the bik from {oldselection}.tfc\ninto {Path.GetFileName(CurrentLoadedExport.FileRef.FilePath)}?\nThis is not recommended for large files.", "Move to Local", MessageBoxButton.OKCancel);
                    if (swELdlg == MessageBoxResult.Cancel)
                    {
                        TextureCacheComboBox.SelectedItem = oldselection;
                        break;
                    }
                    SwitchExternalToLocal();
                    break;
                case STORE_LOCAL_STRING:  //Overwrite locally from new file
                    var impdlg = MessageBox.Show($"Do you want to import a new bik file into {Path.GetFileName(CurrentLoadedExport.FileRef.FilePath)}?\nThis is not recommended for large files.", "Warning", MessageBoxButton.YesNo);
                    if (impdlg == MessageBoxResult.No)
                    {
                        TextureCacheComboBox.SelectedItem = oldselection;
                        break;
                    }

                    IsLocallyCached = true;
                    TfcName = STORE_LOCAL_STRING;
                    IsExternallyCached = false;
                    wasCancelled = !ImportBikFile();
                    break;
                case NEW_TFC_STRING:
                    var bdlg = MessageBox.Show($"Do you want to create a new tfc and add it to the list?", "Create a New TFC", MessageBoxButton.OKCancel);
                    if (bdlg != MessageBoxResult.Cancel)
                    {
                        var createdTFC = CreateNewMovieTFC();
                        if (createdTFC != null)
                        {
                            var newtfcname = Path.GetFileNameWithoutExtension(createdTFC);
                            var newImptdlg = MessageBox.Show($"Do you want to import a movie cached at {newtfcname}", "Movie Import", MessageBoxButton.YesNo);
                            if (newImptdlg != MessageBoxResult.No)
                            {
                                IsLocallyCached = false;
                                IsExternallyCached = true;
                                TfcName = newtfcname;
                                CurrentLoadedExport.WriteProperty(new NameProperty(TfcName, "TextureFileCacheName"));
                                wasCancelled = !ImportBikFile();
                                break;
                            }
                        }
                    }
                    TextureCacheComboBox.SelectedItem = oldselection;
                    break;
                case ADD_TFC_STRING:
                    var addChkDlg = MessageBox.Show($"Do you want to add an existing tfc to the list?", "Add a TFC", MessageBoxButton.OKCancel);
                    if (addChkDlg == MessageBoxResult.Cancel)
                    {
                        TfcName = oldselection;
                        break;
                    }
                    var adddlg = new OpenFileDialog()
                    {
                        FileName = "Select a TFC file",
                        Title = "Import TFC cache for movie files",
                        Filter = "TextureFileCache (*.tfc)|*.tfc",
                        CustomPlaces = AppDirectories.GameCustomPlaces
                    };

                    if (adddlg.ShowDialog() ?? false)
                    {
                        string addedtfc = Path.GetFileNameWithoutExtension(adddlg.FileName);
                        if (!Directory.GetDirectories(MEDirectories.GetDefaultGamePath(Pcc.Game), "*", SearchOption.AllDirectories).ToList().Contains(Path.GetDirectoryName(adddlg.FileName)))
                        {
                            MessageBox.Show("This location does not reside within the game directories.", "Aborting", MessageBoxButton.OK);
                        }
                        else if (!addedtfc.StartsWith("Textures_DLC_") && !addedtfc.StartsWith("Movies_DLC_"))
                        {
                            MessageBox.Show($"Cannot replace movies into a TFC provided by BioWare.\nMust have valid DLC name starting 'Movies_DLC_MOD_' or 'Textures_DLC_MOD_'", "Invalid TFC", MessageBoxButton.OK);
                        }
                        else
                        {
                            Pcc.FindNameOrAdd(addedtfc);
                            AvailableTFCNames.Add(addedtfc);
                            var addimptdlg = MessageBox.Show($"Do you want to import a movie cached at {addedtfc}", "Movie Import", MessageBoxButton.YesNo);
                            if (addimptdlg != MessageBoxResult.No)
                            {
                                IsLocallyCached = false;
                                IsExternallyCached = true;
                                TfcName = addedtfc;
                                CurrentLoadedExport.WriteProperty(new NameProperty(TfcName, "TextureFileCacheName"));
                                wasCancelled = !ImportBikFile();
                                break;
                            }
                        }
                    }

                    TextureCacheComboBox.SelectedItem = oldselection;
                    break;
                default: //This means a tfc name was selected
                    if (IsLocallyCached)
                    {
                        var ddlg = MessageBox.Show($"Do you want to add a new bik file cached at {newSelection}.tfc?", "Warning", MessageBoxButton.OKCancel);
                        if (ddlg == MessageBoxResult.Cancel)
                        {
                            TextureCacheComboBox.SelectedItem = oldselection;
                            break;
                        }
                        CurrentLoadedExport.WriteProperty(new NameProperty(newSelection, "TextureFileCacheName"));
                        IsLocallyCached = false;
                        IsExternallyCached = true;
                    }
                    else //is in existing tfc
                    {
                        var dlg = MessageBox.Show($"Do you want to add a new bik file cached at {newSelection}.tfc?", "Warning", MessageBoxButton.YesNo);
                        if (dlg == MessageBoxResult.No)
                        {
                            TextureCacheComboBox.SelectedItem = oldselection;
                            break;
                        }
                    }
                    TfcName = newSelection;
                    wasCancelled = !ImportBikFile();
                    break;
            }

            if (wasCancelled)
            {
                IsLocallyCached = olocalcache;
                IsExternallyCached = oextcache;
                TfcName = oTfcName;
                TextureCacheComboBox.SelectedItem = oldselection;
                if (IsLocallyCached)
                {
                    CurrentLoadedExport.RemoveProperty("TextureFileCacheName");
                }
                else
                {
                    CurrentLoadedExport.WriteProperty(new NameProperty(TfcName, "TextureFileCacheName"));
                }
            }

            TextureCacheComboBox.SelectionChanged += TextureCacheComboBox_SelectionChanged; //event handling back on
            e.Handled = true;
        }
        /// <summary>
        /// Create a new blank tfc
        /// </summary>
        /// <returns>full filepath of new tfc</returns>
        private string CreateNewMovieTFC()
        {
            var owner = Window.GetWindow(this);
            var nprompt = PromptDialog.Prompt(owner, "Add a new Tfc name.\nIt must begin either Movies_DLC_MOD_ or Textures_DLC_MOD_\nand be followed by the rest of the dlc name.\nIt must reside in the game folders.", "Create a new movie TFC", "Movies_DLC_MOD_", true);
            if (nprompt == null || !nprompt.StartsWith("Movies_DLC_MOD_") && !nprompt.StartsWith("Textures_DLC_MOD_"))
            {
                MessageBox.Show("Invalid TFC Name", "Warning", MessageBoxButton.OK);
                return null;
            }

            CommonOpenFileDialog m = new()
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select folder to save tfc"
            };
            if (m.ShowDialog(owner) != CommonFileDialogResult.Ok)
            {
                return null;
            }
            string outputTFC = Path.Combine(m.FileName, $"{nprompt}.tfc");
            bool createTFC = true;

            //if (!Directory.GetDirectories(MEDirectories.GamePath(Pcc.Game), "*", SearchOption.AllDirectories).ToList().Contains(Path.GetDirectoryName(outputTFC)))
            //{
            //    MessageBox.Show("This location does not reside within the game directories.", "Aborting", MessageBoxButton.OK);
            //    return null;
            //}

            if (File.Exists(outputTFC))
            {
                var fedlg = MessageBox.Show("This tfc already exists. Do you wish to use it?", "Create a new movie TFC", MessageBoxButton.OKCancel);
                if (fedlg == MessageBoxResult.Cancel)
                    return null;

                createTFC = false;
            }

            Pcc.FindNameOrAdd(nprompt);
            if (AvailableTFCNames.All(x => x != nprompt))
            {
                AvailableTFCNames.Add(nprompt);
            }

            if (createTFC)
            {
                Guid tfcGuid = Guid.NewGuid();
                using FileStream fs = new (outputTFC, FileMode.OpenOrCreate, FileAccess.Write);
                fs.WriteGuid(tfcGuid);
                fs.Flush();
            }

            return outputTFC;
        }
        private void SwitchLocalToExternal(string tfcPath = null)
        {
            var tempfilepath = Path.Combine(Path.GetTempPath(), "Temp.bik");
            ExportBikToFile(tempfilepath);

            CurrentLoadedExport.WriteProperty(new NameProperty(TfcName, "TextureFileCacheName"));

            IsLocallyCached = false;
            IsExternallyCached = true;

            bool finished = ImportBiktoCache(tempfilepath, tfcPath);

            File.Delete(tempfilepath);
            if (finished)
            {
                MessageBox.Show("Switch to Cache Completed");
            }
        }
        private void SwitchExternalToLocal()
        {
            var tempfilepath = Path.Combine(Path.GetTempPath(), "Temp.bik");
            ExportBikToFile(tempfilepath);

            CurrentLoadedExport.RemoveProperty("TextureFileCacheName");

            IsLocallyCached = true;
            TfcName = "<Store Locally>";
            IsExternallyCached = false;

            bool finished = ImportBiktoCache(tempfilepath);
            File.Delete(tempfilepath);
            TextureCacheComboBox.SelectedItem = TfcName;
            if (finished)
            {
                MessageBox.Show("Switch to Local Completed");
            }
        }
        private void DownloadRad_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var dlg = MessageBox.Show("Open the RAD Tools website?", "Warning", MessageBoxButton.YesNo);
            if (dlg == MessageBoxResult.No)
                return;
            HyperlinkExtensions.OpenURL("http://www.radgametools.com/bnkdown.htm");
        }
        private void DownloadVLC_Click(object sender, RoutedEventArgs e)
        {
            var dlg = MessageBox.Show("Open the VideoLAN (VLC) website?", "Warning", MessageBoxButton.YesNo);
            if (dlg == MessageBoxResult.No)
                return;
            HyperlinkExtensions.OpenURL("https://www.videolan.org/vlc/");
        }

        #endregion

        private void CRC_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MovieCRC != 0 && e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2)
            {
                Clipboard.SetText(MovieCRC.ToString("X8"));
            }
        }
    }
}
