using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
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

        public JPEXExternalExportLoader() : base("JPEX External Launcher")
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
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{E618D276-6596-41F4-8A98-447D442A77DB}_is1");
                if (key?.GetValue("InstallLocation") is string InstallDir)
                {
                    JPEXExecutableLocation = Path.Combine(InstallDir, "ffdec.exe");
                    JPEXIsInstalled = true;
                    return;
                }
            }
            catch
            {
                //ignore
            }
            JPEXIsInstalled = false;
            JPEXExecutableLocation = null;
        }

        private void LoadCommands()
        {
            OpenFileInJPEXCommand = new GenericCommand(OpenExportInJPEX, () => JPEXIsInstalled);
            ImportJPEXSavedFileCommand = new GenericCommand(ImportJPEXFile, JPEXExportFileExists);
        }

        private string GetSWFExportPath() => Path.Combine(Path.GetTempPath(), CurrentLoadedExport.InstancedFullPath, CurrentLoadedExport.InstancedFullPath + ".gfx");

        private bool JPEXExportFileExists() => CurrentLoadedExport != null && File.Exists(GetSWFExportPath());

        /// <summary>
        /// Assets commonly referenced by swf files
        /// </summary>
        private static readonly Dictionary<(string, string), string> ME3SharedAssets = new Dictionary<(string infilename, string outfilename), string>
        {
            {("PC_SharedAssets","PC_SharedAssets"), "Startup.pcc"},
            {("Xbox_ControllerIcons","Xbox_ControllerIcons"), "Startup.pcc"},
            {("gfxfonts", "fonts/gfxfontlib"), "SFXGUI_Fonts.pcc"}
        };

        /// <summary>
        /// Assets commonly referenced by swf files
        /// </summary>
        private static readonly Dictionary<(string, string), string> LE2SharedAssets = new Dictionary<(string infilename, string outfilename), string>
        {
            {("mainController","mainController"), "Startup_INT.pcc"},
            {("PC_SharedAssets","PC_SharedAssets"), "Startup_INT.pcc"},
            {("Xbox_ControllerIcons","Xbox_ControllerIcons"), "Startup_INT.pcc"},
            {("BioMassFont", "BioMassFont"), "Startup_INT.pcc"},
            {("BioMassFont_glyphs", "BioMassFont_glyphs"), "Startup_INT.pcc"},
            {("AeroLightFont", "AeroLightFont"), "Startup_INT.pcc"},
            {("AeroLightFont_glyphs", "AeroLightFont_glyphs"), "Startup_INT.pcc"}
        };

        /// <summary>
        /// Assets commonly referenced by swf files
        /// </summary>
        private static readonly Dictionary<(string, string), string> LE1SharedAssets = new Dictionary<(string infilename, string outfilename), string>
        {
            {("mainController","mainController"), "Startup_INT.pcc"},
            {("PC_SharedAssets","PC_SharedAssets"), "Startup_INT.pcc"},
            {("XBox_ControllerIcons","XBox_ControllerIcons"), "Startup_INT.pcc"},
            {("BioMassFont", "BioMassFont"), "Startup_INT.pcc"},
            {("BioMassFont_glyphs", "BioMassFont_glyphs"), "Startup_INT.pcc"},
            {("AeroLightFont", "AeroLightFont"), "Startup_INT.pcc"},
            {("AeroLightFont_glyphs", "AeroLightFont_glyphs"), "Startup_INT.pcc"},
            // {("gfxfonts", "fonts/gfxfontlib"), "SFXGUI_Fonts.pcc"}
        };

        private void extractSwf(ExportEntry export, string destination)
        {
            Debug.WriteLine($"Extracting {export.InstancedFullPath} to {destination}");
            var props = export.GetProperties();
            string dataPropName = export.ClassName == "GFxMovieInfo" ? "RawData" : "Data";
            byte[] data = props.GetProp<ImmutableByteArrayProperty>(dataPropName).Bytes;
            File.WriteAllBytes(destination, data);
        }

        private void OpenExportInJPEX()
        {
            try
            {
                // Get additional assets used by swf
                Dictionary<(string infile, string outfile), string> sharedAssets = null;
                switch (CurrentLoadedExport.Game)
                {
                    case MEGame.LE1:
                        sharedAssets = LE1SharedAssets;
                        break;
                    case MEGame.LE3:
                    case MEGame.ME3:
                        sharedAssets = ME3SharedAssets;
                        break;
                    case MEGame.LE2:
                        sharedAssets = LE2SharedAssets;
                        break;
                }

                var storagePath = Directory.GetParent(GetSWFExportPath()).FullName;

                //if game is not installed this will probably fail
                var loadedFiles = MELoadedFiles.GetAllFiles(CurrentLoadedExport.Game).ToList();

                if (sharedAssets != null)
                {
                    foreach (var asset in sharedAssets)
                    {
                        if (asset.Key.infile == CurrentLoadedExport.ObjectName.Name) continue; //don't extract if we're opening an asset
                        var packageF = loadedFiles.FirstOrDefault(x => Path.GetFileName(x) == asset.Value);
                        if (packageF != null)
                        {
                            using var package = MEPackageHandler.OpenMEPackage(packageF);
                            var export = package.Exports.FirstOrDefault(x => x.ObjectName.Name == asset.Key.infile);
                            if (export != null)
                            {
                                // Extract asset to same path as our destination SWF
                                var outfile = Path.Combine(storagePath, asset.Key.outfile + ".swf");
                                Directory.CreateDirectory(Path.GetDirectoryName(outfile)); //some items must be in subfolder
                                extractSwf(export, outfile);
                            }
                        }
                    }
                }

                Directory.CreateDirectory(storagePath);
                string writeoutPath = Path.Combine(storagePath, CurrentLoadedExport.FullPath + ".gfx");
                extractSwf(CurrentLoadedExport, writeoutPath);

                // Texture refereences
                var references = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>(@"References");
                if (references is not null)
                {
                    foreach (var reference in references)
                    {
                        var refExp = CurrentLoadedExport.FileRef.GetEntry(reference.Value) as ExportEntry;
                        if (refExp == null || !refExp.IsTexture())
                            continue; // import or not a texture?

                        // Extract texture TGA
                        var tex = new Texture2D(refExp);
                        tex.ExportToFile(Path.Combine(storagePath, refExp.ObjectName.Instanced + ".tga"));
                    }
                }

                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = JPEXExecutableLocation,
                        Arguments = writeoutPath
                    }
                };
                process.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error launching JPEX: " + ex.FlattenException());
                MessageBox.Show("Error launching JPEX:\n\n" + ex.FlattenException());
            }
        }

        private void ImportJPEXFile()
        {
            if (File.Exists(GetSWFExportPath()))
            {
                byte[] bytes = File.ReadAllBytes(GetSWFExportPath());
                var props = CurrentLoadedExport.GetProperties();

                string dataPropName = CurrentLoadedExport.ClassName == "GFxMovieInfo" ? "RawData" : "Data";
                var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                //Write SWF data
                rawData.Bytes = bytes;

                //Write SWF metadata
                if (CurrentLoadedExport.FileRef.Game.IsGame1() || CurrentLoadedExport.FileRef.Game.IsGame2())
                {
                    string sourceFilePropName = "SourceFilePath";
                    StrProperty sourceFilePath = props.GetProp<StrProperty>(sourceFilePropName);
                    if (sourceFilePath == null)
                    {
                        sourceFilePath = new StrProperty(Path.GetFileName(GetSWFExportPath()), sourceFilePropName);
                        props.Add(sourceFilePath);
                    }

                    sourceFilePath.Value = Path.GetFileName(GetSWFExportPath());
                }

                if (CurrentLoadedExport.FileRef.Game.IsGame1())
                {
                    StrProperty sourceFileTimestamp = props.GetProp<StrProperty>("SourceFileTimestamp");
                    sourceFileTimestamp = File.GetLastWriteTime(GetSWFExportPath()).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                }

                CurrentLoadedExport.WriteProperties(props);
            }
            else
            {
                MessageBox.Show("No file opened in JPEX found. To import a file from the system right click on the export in the tree and select import > embedded file.");
            }
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
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new JPEXExternalExportLoader(), CurrentLoadedExport)
                {
                    Title = $"JPEX Launcher - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}
