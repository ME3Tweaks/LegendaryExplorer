using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
using Microsoft.WindowsAPICodePack.Dialogs;
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
        private bool _showInfo;
        public bool ShowInfo { get => _showInfo; set => SetProperty(ref _showInfo, value); }
        public ObservableCollectionExtended<string> AvailableTFCNames { get; } = new ObservableCollectionExtended<string>();

        private string RADExecutableLocation;
        private string VLCDirectory;
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
        public bool ViewerModeOnly
        {
            get => (bool)GetValue(ViewerModeOnlyProperty);
            set => SetValue(ViewerModeOnlyProperty, value);
        }
        /// <summary>
        /// Set to true to hide all of the editor controls
        /// </summary>
        public static readonly DependencyProperty ViewerModeOnlyProperty = DependencyProperty.Register(
            "ViewerModeOnly", typeof(bool), typeof(BIKExternalExportLoader), new PropertyMetadata(false, ViewerModeOnlyCallback));

        private static void ViewerModeOnlyCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            BIKExternalExportLoader i = (BIKExternalExportLoader)obj;
            i.OnPropertyChanged(nameof(ViewerModeOnly));
        }

        private const string MOVE_TO_EXTERNAL_STRING = "<Move to External Cache>";
        private const string MOVE_TO_LOCAL_STRING = "<Move to Local File>";
        private const string STORE_LOCAL_STRING = "<Store Locally>";
        private const string NEW_TFC_STRING = "<Create New Movie TFC>";

        #endregion

        #region StartUp
        public BIKExternalExportLoader()
        {
            DataContext = this;
            GetRADInstallationStatus();
            GetVLCInstallationStatus();
            LoadCommands();
            InitializeComponent();
            
            TextureCacheComboBox.SelectionChanged += TextureCacheComboBox_SelectionChanged;


            if (!VLCIsInstalled)
            {
                Debug.WriteLine("VLC library not found.");
            }
            else // Load VLC library
            {
                var libDirectory = new DirectoryInfo(VLCDirectory);
                vlcplayer_WinFormsHost.Child = MoviePlayer;
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
                            if (File.Exists(Path.Combine(InstallDir, "libvlc.dll")))
                            {
                                VLCDirectory = InstallDir;
                                VLCIsInstalled = true;
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
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
        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.IsDefaultObject;
        }
        public override void LoadExport(ExportEntry exportEntry)
        {
            if (VLCIsInstalled)
            {
                MoviePlayer.Stop();
                var bik = MoviePlayer.GetCurrentMedia();
                if(bik != null)
                {
                    MoviePlayer.ResetMedia();
                    bik.Dispose();
                }
            }
            CurrentLoadedExport = exportEntry;
            AvailableTFCNames.ClearEx();
            AvailableTFCNames.Add(STORE_LOCAL_STRING);
            AvailableTFCNames.Add(NEW_TFC_STRING);
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
                var bik = MoviePlayer.GetCurrentMedia();
                if (bik != null)
                {
                    MoviePlayer.ResetMedia();
                    bik.Dispose();
                }

            }
            CurrentLoadedExport = null;
            Warning_text.Visibility = Visibility.Collapsed;
            video_Panel.IsEnabled = true;
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
                    AvailableTFCNames.Insert(0, MOVE_TO_EXTERNAL_STRING);
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
            if(bik != null)
            {
                MoviePlayer.ResetMedia();
                bik.Dispose();
            }
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

                        var tfcprop = CurrentLoadedExport.GetProperty<NameProperty>("TextureFileCacheName");
                        string filename = $"{tfcprop.Value}.tfc";
                        filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                        if (filePath == null || !File.Exists(filePath))
                        {
                            MessageBox.Show($"Movie cache {filename} has not been found.");
                            return null;
                        }
                        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            fs.Seek((long)offset, SeekOrigin.Begin);
                            int bikend = offset + length;
                            if (bikend > fs.Length)
                                throw new Exception("tfc corrupt");
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
                
                Warning_text.Visibility = Visibility.Visible;
                video_Panel.IsEnabled = false;
                return null;
            }
            
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
            bool saved = false;
            SaveFileDialog d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                saved = ExportBikToFile(d.FileName);
            }
            if(saved) { MessageBox.Show("Saved"); }
        }
        private bool ExportBikToFile(string bikfile)
        {
            var bikStream = GetMovie();
            if (bikStream != null)
            {
                bikStream.Seek(0, SeekOrigin.Begin);
                using (FileStream fs = new FileStream(bikfile, FileMode.Create))
                {
                    bikStream.CopyTo(fs);
                    fs.Flush();
                }
                return true;
            }
            return false;
        }
        private void ImportBikFile()
        {
            var dlg = new OpenFileDialog();
            dlg.Title = "Import .bik movie file";
            dlg.DefaultExt = "*.bik | *.bik";
            dlg.ShowDialog();

            if (dlg.FileName != null)
            {
                var success = ImportBiktoCache(dlg.FileName);
                if(success) { MessageBox.Show("Done"); }
            }
                
        }
        private bool ImportBiktoCache(string bikfile, string tfcPath = null)
        {
            if (bikfile == null)
                return false;
            
            if (IsMoviePlaying())
            {
                MoviePlayer.Stop();
            }
            bikcontrols_Panel.IsEnabled = false; //stop user playing 

            MemoryStream bikMovie = new MemoryStream();
            using (FileStream fs = new FileStream(bikfile, FileMode.OpenOrCreate, FileAccess.Read))
            {
                fs.CopyTo(bikMovie);
            }
            bikMovie.Seek(0, SeekOrigin.Begin);
            if(IsLocallyCached) //Append to local object
            {

                byte[] binData = new byte[] { };
                
                if (!Int32.TryParse(bikMovie.Length.ToString(), out int biklength))
                {
                    MessageBox.Show($"{Path.GetFileName(bikfile)} is too large to attach to an object. Aborting.", "Warning", MessageBoxButton.OK);
                    bikcontrols_Panel.IsEnabled = true;
                    return false;
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
                props.AddOrReplaceProp(new EnumProperty("MovieStreamSource_Memory", "EMovieStreamSource", CurrentLoadedExport.Game, "MovieStreamSource"));
                CurrentLoadedExport.WriteProperties(props);
            }
            else if (IsExternallyCached) //Append to tfc  NOT ME2/ME1
            {
                if(Pcc.Game != MEGame.ME3)
                {
                    MessageBox.Show($"Only ME3 can store movietextures in a cache file.");
                    bikcontrols_Panel.IsEnabled = true;
                    return false;
                }

                if (!(TfcName.Contains("Movies_DLC_MOD_") || TfcName.Contains("Textures_DLC_MOD_")))
                {
                    MessageBox.Show($"Cannot replace movies into a TFC provided by BioWare. Choose a different target TFC from the list.");
                    bikcontrols_Panel.IsEnabled = true;
                    return false;
                }

                if (tfcPath == null || !File.Exists(tfcPath))
                {
                    string rootPath = MEDirectories.GamePath(Pcc.Game);
                    if (rootPath == null || !Directory.Exists(rootPath))
                    {
                        MessageBox.Show($"{Pcc.Game} has not been found. Please check your ME3Explorer settings");
                        bikcontrols_Panel.IsEnabled = true;
                        return false;
                    }

                    string filename = $"{TfcName}.tfc";
                    tfcPath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                    if(tfcPath == null)
                    {
                        var created = CreateNewMovieTFC();
                        if (created == null)
                        {
                            MessageBox.Show("Error. New tfc not created.");
                            return false;
                        }
                    }
                }

                Guid tfcGuid = Guid.NewGuid();
                long movielength = bikMovie.Length;
                long movieoffset = 0;
                using (FileStream fs = new FileStream(tfcPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    tfcGuid = fs.ReadGuid();
                    fs.Seek(0, SeekOrigin.End);
                    movieoffset = fs.Position;
                    fs.WriteFromStream(bikMovie, movielength);
                    fs.Close();
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
                props.AddOrReplaceProp(new EnumProperty("MovieStreamSource_File", "EMovieStreamSource", CurrentLoadedExport.Game, "MovieStreamSource"));
                CurrentLoadedExport.WriteProperties(props);
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

            switch(newSelection)
            {
                case MOVE_TO_EXTERNAL_STRING: //Before was local move to external
                    var adlg = MessageBox.Show($"Do you want to move the bik at {CurrentLoadedExport.ObjectNameString} to an external tfc?", "Move to External", MessageBoxButton.OKCancel);
                    if (adlg == MessageBoxResult.OK)
                    {
                        string newtfc = null;
                        var bdlg = MessageBox.Show($"Do you want to use an existing tfc?\n (Select No to create a new one.)", "Move to External", MessageBoxButton.YesNo);
                        if(bdlg == MessageBoxResult.No)
                        {
                            newtfc = CreateNewMovieTFC();
                            if (newtfc == null)
                                break;
                        }
                        else
                        {
                            var possibleTFCs = AvailableTFCNames.Where(x => x.StartsWith("Movies_DLC_MOD_") || x.StartsWith("Textures_DLC_MOD_")).ToList();
                            var owner = Window.GetWindow(this);
                            var tdlg = InputComboBoxWPF.GetValue(owner, "Select a Movie TFC", possibleTFCs);
                            if (tdlg == null)
                            {
                                TfcName = oldselection;
                                break;
                            }

                            string rootPath = MEDirectories.GamePath(Pcc.Game);
                            if (rootPath == null || !Directory.Exists(rootPath))
                            {
                                MessageBox.Show($"{Pcc.Game} has not been found. Please check your ME3Explorer settings");
                                TfcName = oldselection;
                                break;
                            }

                            string filename = $"{tdlg}.tfc";
                            newtfc = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                            if (newtfc == null || !File.Exists(newtfc))
                            {
                                MessageBox.Show($"TFC {tdlg}.tfc has not been found.");
                                TfcName = oldselection;
                                break;
                            }
                            TfcName = tdlg;
                        }
                        SwitchLocalToExternal(newtfc);
                    }
                    else
                    {
                        TfcName = oldselection;
                    }
                    break;
                case MOVE_TO_LOCAL_STRING: //Before was external move to local
                    var swELdlg = MessageBox.Show($"Do you want to move the bik from {oldselection}.tfc\ninto {Path.GetFileName(CurrentLoadedExport.FileRef.FilePath)}?\nThis is not recommended for large files.", "Move to Local", MessageBoxButton.OKCancel);
                    if (swELdlg == MessageBoxResult.Cancel)
                    {
                        TfcName = oldselection;
                        break;
                    }
                    SwitchExternalToLocal();
                    break;
                case STORE_LOCAL_STRING:  //Overwrite locally from new file
                    var impdlg = MessageBox.Show($"Do you want to import a new bik file into {Path.GetFileName(CurrentLoadedExport.FileRef.FilePath)}?\nThis is not recommended for large files.", "Warning", MessageBoxButton.YesNo);
                    if (impdlg == MessageBoxResult.No)
                    {
                        TfcName = oldselection;
                        break;
                    }
                    CurrentLoadedExport.RemoveProperty("TextureFileCacheName");
                    IsLocallyCached = true;
                    TfcName = STORE_LOCAL_STRING;
                    IsExternallyCached = false;
                    ImportBikFile();
                    break;
                case NEW_TFC_STRING:
                    if(IsLocallyCached) //=> if was local create tfc 
                    {
                        var bdlg = MessageBox.Show($"Do you want to overwrite {CurrentLoadedExport.ObjectNameString}\nwith a new bik file cached at a new tfc?", "Create a New TFC", MessageBoxButton.OKCancel);
                        if (bdlg == MessageBoxResult.Cancel)
                        {
                            TfcName = oldselection;
                            break;
                        }
                    }
                    else //is in existing tfc
                    {
                        var exdlg = MessageBox.Show($"Do you want to add a new bik file cached at a new tfc?", "Create a New TFC", MessageBoxButton.OKCancel);
                        if (exdlg == MessageBoxResult.Cancel)
                        {
                            TfcName = oldselection;
                            break;
                        }
                    }
                    var createdTFC = CreateNewMovieTFC();
                    if (createdTFC != null)
                    { 
                        var dlg = new OpenFileDialog();
                        dlg.Title = "Import .bik movie file";
                        dlg.DefaultExt = "*.bik | *.bik";
                        dlg.ShowDialog();
                        if (dlg.FileName != null)
                        {
                            var success = ImportBiktoCache(dlg.FileName, createdTFC);
                            if (success) { MessageBox.Show("Done"); }
                        }
                    }
                    break;
                default:
                    if (IsLocallyCached) //=> if was local then shift bik to existing cache
                    {
                        var ddlg = MessageBox.Show($"Do you want to add a new bik file cached at {TfcName}.tfc?", "Warning", MessageBoxButton.OKCancel);
                        if (ddlg == MessageBoxResult.Cancel)
                        {
                            TfcName = oldselection;
                            break;
                        }
                        CurrentLoadedExport.WriteProperty(new NameProperty(TfcName, "TextureFileCacheName"));
                        IsLocallyCached = false;
                        IsExternallyCached = true;
                        byte[] binary = new byte[16];
                        CurrentLoadedExport.setBinaryData(binary);
                    }
                    else //is in existing tfc
                    {
                        var dlg = MessageBox.Show($"Do you want to add a new bik file cached at {TfcName}.tfc?", "Warning", MessageBoxButton.YesNo);
                        if (dlg == MessageBoxResult.No)
                        {
                            TfcName = oldselection;
                            break;
                        }
                    }
                    ImportBikFile();
                    break;
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
            var nprompt = PromptDialog.Prompt(owner, "Add a new Tfc name.\nIt must begin either Movies_DLC_MOD_ or Textures_DLC_MOD_\nand be followed by the rest of the dlc name", "Create a new movie TFC", "Movies_DLC_MOD_XYZ", true);
            if (nprompt == null || !nprompt.StartsWith("Movies_DLC_MOD_") && !nprompt.StartsWith("Textures_DLC_MOD_"))
            {
                MessageBox.Show("Invalid TFC Name", "Warning", MessageBoxButton.OK);
                return null;
            }

            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select folder to save tfc"
            };
            if (m.ShowDialog(owner) != CommonFileDialogResult.Ok)
            {
                return null;
            }
            string outputTFC = Path.Combine(m.FileName, $"{nprompt}.tfc") ;

            if(File.Exists(outputTFC))
            {
                var fedlg = MessageBox.Show("This tfc already exists. Do you wish to use it?", "Create a new movie TFC", MessageBoxButton.OKCancel);
                if (fedlg == MessageBoxResult.Cancel)
                    return null;

                Pcc.FindNameOrAdd(nprompt);
                if (!AvailableTFCNames.Any(x => x == TfcName))
                {
                    AvailableTFCNames.Add(TfcName);
                }
                TfcName = nprompt;
                return outputTFC;
            }

            Pcc.FindNameOrAdd(nprompt);
            TfcName = nprompt;
            if (!AvailableTFCNames.Any(x => x == TfcName))
            {
                AvailableTFCNames.Add(TfcName);
            }

            Guid tfcGuid = Guid.NewGuid();
            using (FileStream fs = new FileStream(outputTFC, FileMode.OpenOrCreate, FileAccess.Write))
            {
                fs.WriteGuid(tfcGuid);
                fs.Flush();
            }
            return outputTFC;
        }
        private void SwitchLocalToExternal(string tfcPath = null)
        {

            var tempfilepath = Path.Combine(Path.GetTempPath(), "Temp.bik");
            bool finished = ExportBikToFile(tempfilepath);

            CurrentLoadedExport.WriteProperty(new NameProperty(TfcName, "TextureFileCacheName"));

            IsLocallyCached = false;
            IsExternallyCached = true;

            byte[] binary = new byte[16]; 
            CurrentLoadedExport.setBinaryData(binary);

            finished = ImportBiktoCache(tempfilepath, tfcPath);

            File.Delete(tempfilepath);
            if(finished)
            {
                MessageBox.Show("Switch to Cache Completed");
            }
        }
        private void SwitchExternalToLocal()
        {
            var tempfilepath = Path.Combine(Path.GetTempPath(), "Temp.bik");
            bool finished = ExportBikToFile(tempfilepath);

            CurrentLoadedExport.RemoveProperty("TextureFileCacheName");

            IsLocallyCached = true;
            TfcName = "<Store Locally>";
            IsExternallyCached = false;

            finished = ImportBiktoCache(tempfilepath);
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
            Process.Start("http://www.radgametools.com/bnkdown.htm");
        }
        private void DownloadVLC_Click(object sender, RoutedEventArgs e)
        {
            var dlg = MessageBox.Show("Open the VideoLAN (VLC) website?", "Warning", MessageBoxButton.YesNo);
            if (dlg == MessageBoxResult.No)
                return;
            Process.Start("https://www.videolan.org/vlc/");
        }

        #endregion


    }
}
