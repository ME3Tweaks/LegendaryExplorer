using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using Microsoft.Win32;
using Vlc.DotNet.Forms;
using Path = System.IO.Path;

namespace ME3Explorer.PackageEditor
{
    /// <summary>
    /// Interaction logic for MovieViewerTab.xaml
    /// </summary>
    public partial class BIKExternalExportLoader : ExportLoaderControl
    {
        private static readonly string[] parsableClasses = { "TextureMovie", "BioLoadingMovie", "BioSeqAct_MovieBink", "SFXInterpTrackMovieBink", "SFXSeqAct_PlatformMovieBink" };
        private bool _radIsInstalled;
        public VlcControl MoviePlayer = new VlcControl();
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
        private bool _vlcIsInstalled;
        public bool VLCIsInstalled
        {
            get => _vlcIsInstalled;
            set
            {
                SetProperty(ref _vlcIsInstalled, value);
                OnPropertyChanged(nameof(VLCNotInstalled));
            }
        }
        public bool VLCNotInstalled => !VLCIsInstalled;
        private bool _isexternallyCached;
        public bool IsExternallyCached { get => _isexternallyCached; set => SetProperty(ref _isexternallyCached, value); }
        private bool _islocallyCached;
        public bool IsLocallyCached { get => _islocallyCached; set => SetProperty(ref _islocallyCached, value); }
        private bool _isexternalFile;
        public bool IsExternalFile { get => _isexternalFile; set => SetProperty(ref _isexternalFile, value); }
        private string _tfcName;
        public string TfcName { get => _tfcName; set => SetProperty(ref _tfcName, value); }
        private string _bikfileName;
        public string BikFileName { get => _bikfileName; set => SetProperty(ref _bikfileName, value); }
        private bool _isvlcPlaying;
        public bool IsVLCPlaying { get => _isvlcPlaying; set => SetProperty(ref _isvlcPlaying, value); }
        private bool _showInfo;
        public bool ShowInfo { get => _showInfo; set => SetProperty(ref _showInfo, value); }
        private string RADExecutableLocation;
        private string VLCDirectory;
        private string CurrentRADExportedFilepath;
        private bool IsExportable()
        {
            return !IsExternalFile;
        }
        private bool IsMoviePlaying()
        {
            return VLCIsInstalled && IsVLCPlaying;
        }
        private bool IsMovieStopped()
        {
            return VLCIsInstalled && !IsVLCPlaying;
        }
        public BIKExternalExportLoader()
        {
            DataContext = this;
            GetRADInstallationStatus();
            GetVLCInstallationStatus();
            LoadCommands();
            InitializeComponent();
            vlcplayer_WinFormsHost.Child = MoviePlayer;

            // Load VLC library

            //var currentAssembly = Assembly.GetEntryAssembly();  //LOAD NUGET 
            //var currentDirectory = new FileInfo(currentAssembly.Location).DirectoryName;
            //currentDirectory = Path.Combine(currentDirectory, "lib");
            //var libDirectory = new DirectoryInfo(currentDirectory);


            var libDirectory = new DirectoryInfo(VLCDirectory);

            if (!VLCIsInstalled || !File.Exists(Path.Combine(VLCDirectory, "libvlc.dll")))
            {
                Debug.WriteLine("VLC library not found.");
                video_Panel.Visibility = Visibility.Collapsed;

            }
            else
            {
                MoviePlayer.BeginInit();
                MoviePlayer.VlcLibDirectory = libDirectory;
                if (!ShowInfo)
                {
                    MoviePlayer.VlcMediaplayerOptions = new string[] { "--video-title-show" };  //Can we find options to show frame counts/frame rates/time etc
                }
                MoviePlayer.EndInit();
                MoviePlayer.Playing += (sender, e) => {
                    IsVLCPlaying = true;
                    Debug.WriteLine("Started");
                };
                MoviePlayer.Stopped += (sender, e) => {
                    IsVLCPlaying = false;
                    Debug.WriteLine("Stopped");
                };
                MoviePlayer.EncounteredError += (sender, e) =>
                {
                    Console.Error.Write("An error occurred");
                    IsVLCPlaying = false;
                };

                MoviePlayer.EndReached += (sender, e) => {
                    IsVLCPlaying = false;
                    Debug.WriteLine("Reached End");
                    //ThreadPool.QueueUserWorkItem(_ => ResetPlayer(MoviePlayer)  ) ;
                };
            }
        }

        static void ResetPlayer(Object obj )
        {
            var vlcplayer = obj as VlcControl;
            vlcplayer.Pause();
            vlcplayer.VlcMediaPlayer.Time = 0;
            Debug.WriteLine("Reached End");
        }

        public BIKExternalExportLoader(bool autoplayPopout, bool showcontrols = false)
        {
            //Always collapse the editing tools
            biktools_Panel.Visibility = Visibility.Collapsed;
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
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\RAD Game Tools\RADVideo\"))
                {
                    if (key != null)
                    {
                        if (key.GetValue("InstallDir") is string InstallDir)
                        {
                            RADExecutableLocation = Path.Combine(InstallDir, "binkpl64.exe");
                            RADIsInstalled = true;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)  //just for demonstration...it's always best to handle specific exceptions
            {
                //react appropriately
            }
            RADIsInstalled = false;
            RADExecutableLocation = null;
        }
        private void GetVLCInstallationStatus()
        {
            if (VLCIsInstalled) return;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\VideoLAN\VLC"))
                {
                    if (key != null)
                    {
                        if (key.GetValue("InstallDir") is string InstallDir)
                        {
                            VLCDirectory = InstallDir;
                            VLCIsInstalled = true;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)  //just for demonstration...it's always best to handle specific exceptions
            {
                //react appropriately
            }
            VLCIsInstalled = false;
            VLCDirectory = null;
        }

        private void LoadCommands()
        {
            OpenFileInRADCommand = new GenericCommand(OpenExportInRAD, () => RADIsInstalled);
            ImportBikFileCommand = new GenericCommand(ImportBikFile, IsExportable);
            PlayBikInVLCCommand = new GenericCommand(PlayExportInVLC, IsMovieStopped);
            PauseVLCCommand = new GenericCommand(PauseMoviePlayer, IsMoviePlaying);
            RewindVLCCommand = new GenericCommand(RewindMoviePlayer, IsMoviePlaying);
            StopVLCCommand = new GenericCommand(StopMoviePlayer, IsMoviePlaying);
            ExtractBikCommand = new GenericCommand(SaveBikToFile, IsExportable);
        }

        private void GetBikProps()
        {
            IsExternallyCached = false;
            IsExternalFile = false;
            IsLocallyCached = false;
            TfcName = "None";
            BikFileName = "No file";
            var props = CurrentLoadedExport.GetProperties();
            if(CurrentLoadedExport.ClassName == "TextureMovie")
            {
                var tfcprop = props.GetProp<NameProperty>("TextureFileCacheName");
                if (tfcprop == null)
                {
                    IsLocallyCached = true;
                    return;
                }
                IsExternallyCached = true;
                TfcName = tfcprop.Value;
            }
            else
            {
                string propbikName = "m_sMovieName";
                if(CurrentLoadedExport.ClassName == "BioLoadingMovie")
                {
                    propbikName = "MovieName";
                }
                var bikprop = props.GetProp<StrProperty>(propbikName);
                if(bikprop != null)
                {
                    BikFileName = bikprop.ToString();
                    IsExternalFile = true;
                }
            }
        }
        private void OpenExportInRAD()
        {
            try
            {
                MemoryStream bikMovie = GetMovie();
                byte[] data = bikMovie.ToArray();
                string writeoutPath = Path.Combine(Path.GetTempPath(), CurrentLoadedExport.FullPath + ".bik");

                File.WriteAllBytes(writeoutPath, data);

                Process process = new Process();
                // Configure the process using the StartInfo properties.
                process.StartInfo.FileName = RADExecutableLocation;
                process.StartInfo.Arguments = $"{writeoutPath} /P";
                process.Start();
                CurrentRADExportedFilepath = writeoutPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error launching RADTools: " + ExceptionHandlerDialogWPF.FlattenException(ex));
                MessageBox.Show("Error launching RADTools:\n\n" + ExceptionHandlerDialogWPF.FlattenException(ex));
            }
        }
        private void PlayExportInVLC()
        {
            var bik = MoviePlayer.GetCurrentMedia();
            if (bik == null)
            {
                MemoryStream bikMovie = GetMovie();

                if (bikMovie != null)
                    MoviePlayer.Play(bikMovie);
            }
            else
            {
                MoviePlayer.Pause();
            }
            IsVLCPlaying = true;
        }
        private void PauseMoviePlayer()
        {
            IsVLCPlaying = false;
            MoviePlayer.Pause();
        }
        private void RewindMoviePlayer()
        {
            MoviePlayer.VlcMediaPlayer.Time = 0;
        }
        private void StopMoviePlayer()
        {
            MoviePlayer.Stop();
            var bik = MoviePlayer.GetCurrentMedia();
            bik?.Dispose();
            MoviePlayer.ResetMedia();
        }
        private MemoryStream GetMovie()
        {
            try
            {
                MemoryStream bikMovie = new MemoryStream();
                if (IsExternalFile)
                {
                    string filePath = null;
                    string rootPath = MEDirectories.GamePath(Pcc.Game);
                    if (rootPath == null || !Directory.Exists(rootPath))
                    {
                        MessageBox.Show($"{Pcc.Game} has not been found. Please check your ME3Explorer settings");
                        return null;
                    }

                    string filename = $"{BikFileName}.bik";
                    filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                    if (filePath == null || !File.Exists(filePath))
                    {
                        MessageBox.Show($"Bik file {BikFileName}.bik has not been found.");
                        return null;
                    }
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {

                        fs.CopyTo(bikMovie);
                    }
                }
                else
                {
                    var binary = CurrentLoadedExport.getBinaryData();
                    int length = BitConverter.ToInt32(binary, 4);
                    int offset = BitConverter.ToInt32(binary, 12);

                    if (IsExternallyCached)
                    {
                        string filePath = null;
                        string rootPath = MEDirectories.GamePath(Pcc.Game);
                        if (rootPath == null || !Directory.Exists(rootPath))
                        {
                            MessageBox.Show($"{Pcc.Game} has not been found. Please check your ME3Explorer settings");
                            return null;
                        }

                        string filename = $"{TfcName}.tfc";
                        filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                        if (filePath == null || !File.Exists(filePath))
                        {
                            MessageBox.Show($"Movie cache {filename} has not been found.");
                            return null;
                        }
                        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            fs.Position = offset;
                            int bikend = offset + length;
                            while (fs.Position < bikend)
                            {
                                fs.CopyTo(bikMovie);
                            }
                        }
                    }
                    else if (IsLocallyCached) //is locally contained
                    {
                        byte[] bikBytes = binary.Slice(16, length).ToArray();
                        if (bikBytes == null)
                        {
                            MessageBox.Show($"Embedded texture movie has not been found.");
                            return null;
                        }
                        using (var writer = new BinaryWriter(bikMovie))
                        {
                            writer.Write(bikBytes);
                        }
                    }
                    else
                    {
                        return null;
                    }
                }

                return bikMovie;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error loading movie: " + ExceptionHandlerDialogWPF.FlattenException(ex));
                MessageBox.Show("Error loading movie:\n\n" + ExceptionHandlerDialogWPF.FlattenException(ex));
            }
            return null;
        }

        private void SaveBikToFile()
        {
            string fileFilter = $"*.bik |*.bik";
            SaveFileDialog d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                var bikStream = GetMovie();
                if(bikStream != null)
                {
                    bikStream.Seek(0, SeekOrigin.Begin);
                    using (FileStream fs = new FileStream(d.FileName, FileMode.Create))
                    {
                        bikStream.CopyTo(fs);
                        fs.Flush();
                    }
                    MessageBox.Show("Done");
                }
            }
        }
        private void ImportBikFile()
        {
            //var bytes = File.ReadAllBytes(CurrentRADExportedFilepath);
            //var props = CurrentLoadedExport.GetProperties();

            //string dataPropName = CurrentLoadedExport.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
            //var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
            ////Write SWF data
            //rawData.bytes = bytes;

            ////Write SWF metadata
            //if (CurrentLoadedExport.FileRef.Game == MEGame.ME1 || CurrentLoadedExport.FileRef.Game == MEGame.ME2)
            //{
            //    string sourceFilePropName = CurrentLoadedExport.FileRef.Game != MEGame.ME1 ? "SourceFile" : "SourceFilePath";
            //    StrProperty sourceFilePath = props.GetProp<StrProperty>(sourceFilePropName);
            //    if (sourceFilePath == null)
            //    {
            //        sourceFilePath = new StrProperty(Path.GetFileName(CurrentRADExportedFilepath), sourceFilePropName);
            //        props.Add(sourceFilePath);
            //    }
            //    sourceFilePath.Value = Path.GetFileName(CurrentRADExportedFilepath);
            //}

            //if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
            //{
            //    StrProperty sourceFileTimestamp = props.GetProp<StrProperty>("SourceFileTimestamp");
            //    sourceFileTimestamp = File.GetLastWriteTime(CurrentRADExportedFilepath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            //}
            //CurrentLoadedExport.WriteProperties(props);
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.IsDefaultObject;
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            GetRADInstallationStatus();
            if(VLCIsInstalled)
            {
                MoviePlayer.Stop();
                MoviePlayer.ResetMedia();
                //var bik = MoviePlayer.GetCurrentMedia();
                //bik?.Dispose();
            }
            CurrentLoadedExport = exportEntry;
            GetBikProps();
        }

        public override void UnloadExport()
        {
            if (VLCIsInstalled)
            {
                MoviePlayer.Stop();
                MoviePlayer.ResetMedia();
                //var bik = MoviePlayer.GetCurrentMedia();
                //bik?.Dispose();
            }
            CurrentLoadedExport = null;
            CurrentRADExportedFilepath = null;
        }

        public override void PopOut()
        {
            //throw new NotImplementedException();
        }

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var dlg = MessageBox.Show("Open the RAD Tools website?", "Warning", MessageBoxButton.YesNo);
            if (dlg == MessageBoxResult.No)
                return;
            Process.Start("http://www.radgametools.com/bnkdown.htm");
        }

        private void DownloadVLC_Click(object sender, RoutedEventArgs e)
        {
            var dlg = MessageBox.Show("Open the VideoLAN (VLC) website?", "Warning", MessageBoxButton.YesNo);
            if (dlg == MessageBoxResult.No)
                return;
            Process.Start("https://www.videolan.org/vlc/");
        }
    }
}
