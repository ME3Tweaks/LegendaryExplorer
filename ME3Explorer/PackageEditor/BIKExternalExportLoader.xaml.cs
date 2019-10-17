using System;
using System.Collections.Concurrent;
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
using Gammtek.Conduit.Extensions.IO;
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
        #region Declarations
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
        public ICommand ChangeLocaltoExtCommand { get; private set; }
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
        public ObservableCollectionExtended<string> AvailableTFCNames { get; } = new ObservableCollectionExtended<string>();

        private string RADExecutableLocation;
        private string VLCDirectory;
        private string CurrentRADExportedFilepath;
        private bool IsExportable()
        {
            return !IsExternalFile;
        }
        private bool CanSwitchFromLocalToExternal()
        {
            return CurrentLoadedExport?.Game == MEGame.ME3 && IsLocallyCached;
        }
        private bool IsMoviePlaying()
        {
            return VLCIsInstalled && IsVLCPlaying;
        }
        private bool IsMovieStopped()
        {
            return VLCIsInstalled && !IsVLCPlaying;
        }
        #endregion

        #region StartUp
        public BIKExternalExportLoader()
        {
            DataContext = this;
            GetRADInstallationStatus();
            GetVLCInstallationStatus();
            LoadCommands();
            InitializeComponent();
            vlcplayer_WinFormsHost.Child = MoviePlayer;

            var libDirectory = new DirectoryInfo(VLCDirectory);

            if (!VLCIsInstalled || !File.Exists(Path.Combine(VLCDirectory, "libvlc.dll")))
            {
                Debug.WriteLine("VLC library not found.");
            }
            else // Load VLC library
            {
                MoviePlayer.BeginInit();
                MoviePlayer.VlcLibDirectory = libDirectory;
                if (ShowInfo)
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

                MoviePlayer.EndReached += MediaEndReached;
            }
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
            ChangeLocaltoExtCommand = new GenericCommand(SwitchLocalToExternal, CanSwitchFromLocalToExternal);
        }
        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.IsDefaultObject;
        }
        public override void LoadExport(ExportEntry exportEntry)
        {
            GetRADInstallationStatus();
            if (VLCIsInstalled)
            {
                MoviePlayer.Stop();
                MoviePlayer.ResetMedia();
            }
            CurrentLoadedExport = exportEntry;
            AvailableTFCNames.ClearEx();
            AvailableTFCNames.Add("<Store Locally>");
            AvailableTFCNames.Add("<Create New Movie TFC>");
            AvailableTFCNames.AddRange(exportEntry.FileRef.Names.Where(x => x.StartsWith("Textures_DLC_") || x.StartsWith("Movies_DLC_")));

            GetBikProps();
            if (!AvailableTFCNames.Any(x => x == TfcName))
            {
                AvailableTFCNames.Add(TfcName);
            }
            TextureCacheComboBox.SelectedItem = TfcName;
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
        #endregion

        #region Playback
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
            if (bik != null && bik.State == Vlc.DotNet.Core.Interops.Signatures.MediaStates.Paused)
            {
                MoviePlayer.Pause();
            }
            else
            {
                MemoryStream bikMovie = new MemoryStream();
                bikMovie = GetMovie();
                if (bikMovie != null)
                    MoviePlayer.Play(bikMovie);
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
                    if(CurrentLoadedExport.Game != MEGame.ME3)
                    {
                        length = BitConverter.ToInt32(binary, 20);
                        offset = BitConverter.ToInt32(binary, 28);
                    }

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
                        int slicePos = CurrentLoadedExport.Game == MEGame.ME3 ? 16 : 32;
                        byte[] bikBytes = binary.Slice(slicePos, length).ToArray();
                        if (bikBytes == null)
                        {
                            MessageBox.Show($"Embedded texture movie has not been found.");
                            return null;
                        }
                        bikMovie = new MemoryStream(bikBytes);
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

        public async void MediaEndReached(object sender, EventArgs args)
        {
            Debug.WriteLine("Reached End");
            var mediaplayer = sender as VlcControl;
            await Task.Run(() => mediaplayer.VlcMediaPlayer.Time = 0);
            IsVLCPlaying = false;
        }

        #endregion

        #region usertools
        private void SaveBikToFile()
        {
            string fileFilter = $"*.bik |*.bik";
            SaveFileDialog d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                var bikStream = GetMovie();
                if (bikStream != null)
                {
                    bikStream.Seek(0, SeekOrigin.Begin);
                    using (FileStream fs = new FileStream(d.FileName, FileMode.Create))
                    {
                        bikStream.CopyTo(fs);
                        fs.Flush();
                    }
                    MessageBox.Show("Saved");
                }
            }
        }
        private void ImportBikFile()
        {
            if(IsMoviePlaying())
            {
                MoviePlayer.Stop();
            }
            bikcontrols_Panel.IsEnabled = false; //stop playing 

            var dlg = new OpenFileDialog();
            dlg.Title = "Import .bik movie file";
            dlg.DefaultExt = "*.bik | *.bik";
            dlg.ShowDialog();

            if (dlg == null)
            {
                bikcontrols_Panel.IsEnabled = true;
                return;
            }


            MemoryStream bikMovie = new MemoryStream();
            using (FileStream fs = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read))
            {
                fs.CopyTo(bikMovie);
            }
            bikMovie.Seek(0, SeekOrigin.Begin);
            if(IsLocallyCached) //Append to local object
            {

                byte[] binData = new byte[] { };
                
                if (!Int32.TryParse(bikMovie.Length.ToString(), out int biklength))
                {
                    MessageBox.Show($"{dlg.FileName} is too large to attach to an object. Aborting.", "Warning", MessageBoxButton.OK);
                    bikcontrols_Panel.IsEnabled = true;
                    return;
                }
                if(CurrentLoadedExport.Game == MEGame.ME3)
                {
                    binData = new byte[16 + biklength];
                    binData.OverwriteRange(0, BitConverter.GetBytes(0));
                    binData.OverwriteRange(4, BitConverter.GetBytes(biklength));
                    binData.OverwriteRange(8, BitConverter.GetBytes(biklength));
                    binData.OverwriteRange(12, BitConverter.GetBytes(CurrentLoadedExport.DataOffset + CurrentLoadedExport.propsEnd() + 16));
                    binData.OverwriteRange(16, bikMovie.ToArray());
                }
                else
                {
                    binData = new byte[32 + biklength];
                    binData.OverwriteRange(0, BitConverter.GetBytes(0));
                    binData.OverwriteRange(4, BitConverter.GetBytes(0));
                    binData.OverwriteRange(8, BitConverter.GetBytes(0));
                    binData.OverwriteRange(12, BitConverter.GetBytes(CurrentLoadedExport.DataOffset + CurrentLoadedExport.propsEnd() + 16));
                    binData.OverwriteRange(16, BitConverter.GetBytes(0));
                    binData.OverwriteRange(20, BitConverter.GetBytes(biklength));
                    binData.OverwriteRange(24, BitConverter.GetBytes(biklength));
                    binData.OverwriteRange(28, BitConverter.GetBytes(CurrentLoadedExport.DataOffset + CurrentLoadedExport.propsEnd() + 28));
                    binData.OverwriteRange(32, bikMovie.ToArray());
                }

                CurrentLoadedExport.setBinaryData(binData);
                var props = CurrentLoadedExport.GetProperties();
                props.RemoveNamedProperty("TextureFileCacheName");
                props.RemoveNamedProperty("TFCFileGuid");
            }
            else if (IsExternallyCached) //Append to tfc  NOT ME2
            {
                if(Pcc.Game != MEGame.ME3)
                {
                    MessageBox.Show($"Only ME3 can store movietextures in a cache file.");
                    bikcontrols_Panel.IsEnabled = true;
                    return;
                }

                if(!(TfcName.Contains("Movies_DLC_MOD_") || TfcName.Contains("Textures_DLC_MOD_")))
                {
                    MessageBox.Show($"Cannot replace movies into a TFC provided by BioWare. Choose a different target TFC from the list.");
                    bikcontrols_Panel.IsEnabled = true;
                    return;
                }

                string tfcPath = null;
                string rootPath = MEDirectories.GamePath(Pcc.Game);
                if (rootPath == null || !Directory.Exists(rootPath))
                {
                    MessageBox.Show($"{Pcc.Game} has not been found. Please check your ME3Explorer settings");
                    bikcontrols_Panel.IsEnabled = true;
                    return;
                }

                string filename = $"{TfcName}.tfc";
                tfcPath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                Guid tfcGuid = Guid.NewGuid();
                if (tfcPath == null || !File.Exists(tfcPath))
                {
                    var tdlg = MessageBox.Show($"Movie file cache {TfcName}.tfc has not been found.\nDo you wish to create a new one?","Warning", MessageBoxButton.YesNo);
                    if(tdlg == MessageBoxResult.No)
                    {
                        bikcontrols_Panel.IsEnabled = true;
                        return;
                    }

                    tfcPath = Path.Combine(Path.GetDirectoryName(Pcc.FilePath), TfcName);
                    using (FileStream fs = new FileStream(tfcPath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        fs.WriteGuid(tfcGuid);
                        fs.Flush();
                    }
                }
                
                long movielength = bikMovie.Length;
                long movieoffset = 0;
                using (FileStream fs = new FileStream(tfcPath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    movieoffset = fs.Length;
                    fs.Position = movieoffset;
                    fs.WriteFromStream(bikMovie, movielength);
                    fs.Flush();
                }

                var biklength = Int32.Parse(movielength.ToString());
                var bikoffset = Int32.Parse(movieoffset.ToString());

                var binData = CurrentLoadedExport.getBinaryData();
                binData.OverwriteRange(0, BitConverter.GetBytes(0));
                binData.OverwriteRange(4, BitConverter.GetBytes(biklength));
                binData.OverwriteRange(8, BitConverter.GetBytes(biklength));
                binData.OverwriteRange(12, BitConverter.GetBytes(bikoffset));
                CurrentLoadedExport.setBinaryData(binData);

                var props = CurrentLoadedExport.GetProperties();
                props.AddOrReplaceProp(new NameProperty(TfcName, "TextureFileCacheName"));
                props.AddOrReplaceProp(tfcGuid.ToGuidStructProp("TFCFileGuid"));
                CurrentLoadedExport.WriteProperties(props);
            }
            MessageBox.Show("Done");
            bikcontrols_Panel.IsEnabled = true; //unlock play
        }

        private void DownloadRad_MouseUp(object sender, MouseButtonEventArgs e)
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

        private void SwitchLocalToExternal()
        {
            var dlg = MessageBox.Show("Do you want to switch this object from a locally cached object to a externally cached one?", "Warning", MessageBoxButton.YesNo);
            if (dlg == MessageBoxResult.No)
                return;

            var window = this.Parent as PackageEditorWPF;

            var tfcPossibles = Pcc.Names.Where(x => x.Contains("_DLC_MOD_"));
            var result = InputComboBoxWPF.GetValue(window, "Write the TFC name, it should start either Movies_DLC_MOD_\nor Textures_DLC_MOD_ followed by the rest of the DLC name.", tfcPossibles);
            if (result == null || !result.StartsWith("Movies_DLC_MOD_") && !result.StartsWith("Textures_DLC_MOD_"))
            {
                MessageBox.Show("Invalid TFC Name", "Warning", MessageBoxButton.OK);
                return;
            }
            Pcc.FindNameOrAdd(result);
            TfcName = result;
            var props = CurrentLoadedExport.GetProperties();
            props.AddOrReplaceProp(new NameProperty(TfcName, "TextureFileCacheName"));
            CurrentLoadedExport.WriteProperty(new NameProperty(TfcName, "TextureFileCacheName"));
            if(!AvailableTFCNames.Any(x => x == TfcName))
            {
                AvailableTFCNames.Add(TfcName);
            }

            MessageBox.Show("Now you can save the attached bik to an external location\nand reimport it into the cache", "Instructions", MessageBoxButton.OK);
            SaveBikToFile();

            IsLocallyCached = false;
            IsExternallyCached = true;

            byte[] binary = new byte[16]; 
            CurrentLoadedExport.setBinaryData(binary);

            ImportBikFile();
        }
        #endregion

    }
}
