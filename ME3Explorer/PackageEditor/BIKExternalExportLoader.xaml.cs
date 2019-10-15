using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
using Path = System.IO.Path;

namespace ME3Explorer.PackageEditor
{
    /// <summary>
    /// Interaction logic for ExternalToolLauncher.xaml
    /// </summary>
    public partial class BIKExternalExportLoader : ExportLoaderControl
    {
        private static readonly string[] parsableClasses = { "TextureMovie" };
        private bool _radIsInstalled;

        public ICommand OpenFileInRADCommand { get; private set; }
        public ICommand ImportRADSavedFileCommand { get; private set; }
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
        public bool _isexternallyCached;
        public bool IsExternallyCached { get => _isexternallyCached; set => SetProperty(ref _isexternallyCached, value); }
        public string _tfcName;
        public string TfcName { get => _tfcName; set => SetProperty(ref _tfcName, value); }

        private string RADExecutableLocation;
        private string CurrentRADExportedFilepath;

        public BIKExternalExportLoader()
        {
            DataContext = this;
            GetRADInstallationStatus();
            LoadCommands();
            InitializeComponent();
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

        private void LoadCommands()
        {
            OpenFileInRADCommand = new GenericCommand(OpenExportInRAD, () => RADIsInstalled);
            ImportRADSavedFileCommand = new GenericCommand(ImportBikFile, RADExportFileExists);
        }

        private bool RADExportFileExists() => CurrentLoadedExport != null && File.Exists(Path.Combine(Path.GetTempPath(), CurrentLoadedExport.FullPath + ".bik"));

        private void GetCacheProps()
        {
            var props = CurrentLoadedExport.GetProperties();
            var tfcprop = props.GetProp<NameProperty>("TextureFileCacheName");
            if (tfcprop == null)
            {
                IsExternallyCached = false;
                TfcName = "None";
                return;
            }
            IsExternallyCached = true;
            TfcName = tfcprop.Value;
        }
        private void OpenExportInRAD()
        {
            try
            {
                var props = CurrentLoadedExport.GetProperties();
                var binary = CurrentLoadedExport.getBinaryData();
                int length = BitConverter.ToInt32(binary, 4);
                int offset = BitConverter.ToInt32(binary, 12);

                byte[] bikMovie = new byte[] { };
                
                if(IsExternallyCached) //is contained in TFC
                {
                    string filePath = null;
                    string rootPath = MEDirectories.GamePath(Pcc.Game);
                    if (rootPath == null || !Directory.Exists(rootPath))
                    {
                        MessageBox.Show($"{Pcc.Game} has not been found. Please check your ME3Explorer settings");
                        return;
                    }

                    string filename = $"{TfcName}.tfc";
                    filePath = Directory.GetFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                    if (filePath == null || !File.Exists(filePath))
                    {
                        MessageBox.Show($"Movie cache {filename} has not been found.");
                        return;
                    }
                    var movieMS = GetMovieFromTFC(filePath, offset, length);
                    bikMovie = movieMS.ToArray();
                }
                else //is locally contained
                {
                    bikMovie = binary.Slice(16, length).ToArray();
                    if(bikMovie == null)
                    {
                        MessageBox.Show($"Embedded texture movie has not been found.");
                        return;
                    }
                }
                string writeoutPath = Path.Combine(Path.GetTempPath(), CurrentLoadedExport.FullPath + ".bik");

                File.WriteAllBytes(writeoutPath, bikMovie);

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

        private MemoryStream GetMovieFromTFC(string tfcpath, int offset, int length)
        {
            MemoryStream textureMovie = new MemoryStream();
            using (FileStream fs = new FileStream(tfcpath, FileMode.Open, FileAccess.Read))
            {
                fs.Position = offset;
                int bikend = offset + length;
                while(fs.Position < bikend)
                {
                    fs.CopyTo(textureMovie);
                }
            }

            return textureMovie;
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

        private void OpenWithRAD_Click(object sender, RoutedEventArgs e)
        {

        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.IsDefaultObject;
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            GetRADInstallationStatus();
            CurrentLoadedExport = exportEntry;
            GetCacheProps();
        }

        public override void UnloadExport()
        {
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
    }
}
