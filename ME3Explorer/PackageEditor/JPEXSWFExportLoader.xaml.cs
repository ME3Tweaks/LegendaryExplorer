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
    public partial class JPEXExternalExportLoader : ExportLoaderControl
    {
        private static readonly string[] parsableClasses = { "BioSWF", "GFxMovieInfo" };
        private bool _jpexIsInstalled;

        public ICommand OpenFileInJPEXCommand { get; private set; }
        public ICommand ImportJPEXSavedFileCommand { get; private set; }
        public bool JPEXIsInstalled
        {
            get => _jpexIsInstalled;
            set
            {
                SetProperty(ref _jpexIsInstalled, value);
                OnPropertyChanged(nameof(JPEXNotInstalled));
            }
        }
        public bool JPEXNotInstalled => !JPEXIsInstalled;

        private string JPEXExecutableLocation;
        private string CurrentJPEXExportedFilepath;

        public JPEXExternalExportLoader()
        {
            DataContext = this;
            GetJPEXInstallationStatus();
            LoadCommands();
            InitializeComponent();
        }

        private void GetJPEXInstallationStatus()
        {
            if (JPEXIsInstalled) return;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{E618D276-6596-41F4-8A98-447D442A77DB}_is1"))
                {
                    if (key != null)
                    {
                        if (key.GetValue("InstallLocation") is string InstallDir)
                        {
                            JPEXExecutableLocation = Path.Combine(InstallDir, "ffdec.exe");
                            JPEXIsInstalled = true;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)  //just for demonstration...it's always best to handle specific exceptions
            {
                //react appropriately
            }
            JPEXIsInstalled = false;
            JPEXExecutableLocation = null;
        }

        private void LoadCommands()
        {
            OpenFileInJPEXCommand = new GenericCommand(OpenExportInJPEX, () => JPEXIsInstalled);
            ImportJPEXSavedFileCommand = new GenericCommand(ImportJPEXFile, JPEXExportFileExists);
        }

        private bool JPEXExportFileExists() => CurrentLoadedExport != null && File.Exists(Path.Combine(Path.GetTempPath(), CurrentLoadedExport.FullPath + ".swf"));


        private void OpenExportInJPEX()
        {
            try
            {
                var props = CurrentLoadedExport.GetProperties();
                string dataPropName = CurrentLoadedExport.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";

                byte[] data = props.GetProp<ImmutableByteArrayProperty>(dataPropName).bytes;
                string writeoutPath = Path.Combine(Path.GetTempPath(), CurrentLoadedExport.FullPath + ".swf");

                File.WriteAllBytes(writeoutPath, data);

                Process process = new Process();
                // Configure the process using the StartInfo properties.
                process.StartInfo.FileName = JPEXExecutableLocation;
                process.StartInfo.Arguments = writeoutPath;
                process.Start();
                CurrentJPEXExportedFilepath = writeoutPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error launching JPEX: " + ExceptionHandlerDialogWPF.FlattenException(ex));
                MessageBox.Show("Error launching JPEX:\n\n" + ExceptionHandlerDialogWPF.FlattenException(ex));
            }
        }

        private void ImportJPEXFile()
        {
            var bytes = File.ReadAllBytes(CurrentJPEXExportedFilepath);
            var props = CurrentLoadedExport.GetProperties();

            string dataPropName = CurrentLoadedExport.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";
            var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
            //Write SWF data
            rawData.bytes = bytes;

            //Write SWF metadata
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME1 || CurrentLoadedExport.FileRef.Game == MEGame.ME2)
            {
                string sourceFilePropName = CurrentLoadedExport.FileRef.Game != MEGame.ME1 ? "SourceFile" : "SourceFilePath";
                StrProperty sourceFilePath = props.GetProp<StrProperty>(sourceFilePropName);
                if (sourceFilePath == null)
                {
                    sourceFilePath = new StrProperty(Path.GetFileName(CurrentJPEXExportedFilepath), sourceFilePropName);
                    props.Add(sourceFilePath);
                }
                sourceFilePath.Value = Path.GetFileName(CurrentJPEXExportedFilepath);
            }

            if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
            {
                StrProperty sourceFileTimestamp = props.GetProp<StrProperty>("SourceFileTimestamp");
                sourceFileTimestamp = File.GetLastWriteTime(CurrentJPEXExportedFilepath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            CurrentLoadedExport.WriteProperties(props);
        }

        private void OpenWithJPEX_Click(object sender, RoutedEventArgs e)
        {

        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.IsDefaultObject;
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            GetJPEXInstallationStatus();
            CurrentLoadedExport = exportEntry;
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            CurrentJPEXExportedFilepath = null;
        }

        public override void PopOut()
        {
            //throw new NotImplementedException();
        }

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}
