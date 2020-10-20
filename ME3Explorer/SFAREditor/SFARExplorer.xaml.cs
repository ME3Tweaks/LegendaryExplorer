using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer.SFAREditor
{
    /// <summary>
    /// Interaction logic for SFARExplorer.xaml
    /// </summary>
    public partial class SFARExplorer : TrackingNotifyPropertyChangedWindowBase, IRecents
    {
        private string _bottomLeftText = "Open or drop SFAR";

        public string BottomLeftText
        {
            get => _bottomLeftText;
            set => SetProperty(ref _bottomLeftText, value);
        }

        private DLCPackage _dlcPackage;
        public DLCPackage LoadedDLCPackage
        {
            get => _dlcPackage;
            set => SetProperty(ref _dlcPackage, value);
        }

        public SFARExplorer() : base("SFAR Explorer", true)
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileName => LoadFile(fileName));

        }

        public GenericCommand LoadDLCCommand { get; set; }
        public RelayCommand OpenInPackageEditorCommand { get; set; }
        public GenericCommand UnpackDLCCommand { get; set; }
        public RelayCommand OpenInTLKEditorCommand { get; set; }
        public RelayCommand ExtractFileCommand { get; set; }

        private void LoadCommands()
        {
            LoadDLCCommand = new GenericCommand(PromptForDLC, CanLoadDLC);
            OpenInPackageEditorCommand = new RelayCommand(openInPackageEditor, canOpenInPackageEditor);
            OpenInTLKEditorCommand = new RelayCommand(openInTlkEditor, canOpenInTlkEditor);
            ExtractFileCommand = new RelayCommand(extractSingleFile, canExtractFile);
            UnpackDLCCommand = new GenericCommand(extractDLC, () => LoadedDLCPackage != null);
        }

        private void extractDLC()
        {
            var dlg = new CommonOpenFileDialog("Select output folder")
            {
                IsFolderPicker = true,
                EnsurePathExists = true
            };

            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                Task.Run(() =>
                {
                    foreach (var f in LoadedDLCPackage.Files)
                    {
                        if (f.isActualFile)
                        {
                            var decompressedFile = LoadedDLCPackage.DecompressEntry(f);
                            var outPath = Path.Combine(dlg.FileName, f.FileName.TrimStart('/'));
                            Directory.CreateDirectory(Directory.GetParent(outPath).FullName);
                            decompressedFile.WriteToFile(outPath);
                        }
                    }
                    return null;
                }).ContinueWithOnUIThread(result =>
                {
                    MessageBox.Show("Done extracting");
                });
            }
        }

        private bool canExtractFile(object obj) => obj is DLCPackage.FileEntryStruct;

        private void extractSingleFile(object obj)
        {
            if (obj is DLCPackage.FileEntryStruct fes)
            {
                var recommendedName = Path.GetFileName(fes.FileName);
                var dlg = new SaveFileDialog
                {
                    FileName = recommendedName
                };
                var result = dlg.ShowDialog(this);
                if (result.HasValue && result.Value)
                {
                    var outpath = dlg.FileName;
                    LoadedDLCPackage?.DecompressEntry(fes).WriteToFile(outpath);
                }
            }
        }

        private bool canOpenInPackageEditor(object obj)
        {
            if (obj is DLCPackage.FileEntryStruct fes)
            {
                return (fes.FileName.EndsWith(".pcc") || fes.FileName.EndsWith(".xxx"));
            }

            return false;
        }

        private bool canOpenInTlkEditor(object obj)
        {
            if (obj is DLCPackage.FileEntryStruct fes)
            {
                return (fes.FileName.EndsWith(".tlk"));
            }

            return false;
        }

        private void openInPackageEditor(object obj)
        {
            if (obj is DLCPackage.FileEntryStruct fes)
            {
                var packageStream = LoadedDLCPackage.DecompressEntry(fes);
                PackageEditorWPF p = new PackageEditorWPF();
                p.Show();
                p.LoadFileFromStream(packageStream, fes.FileName);
                p.Activate();
            }
        }

        private void openInTlkEditor(object obj)
        {
            if (obj is DLCPackage.FileEntryStruct fes)
            {
                var tlkStream = LoadedDLCPackage.DecompressEntry(fes);
                var ed = new ME1TlkEditor.ME1TlkEditorWPF();
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(ed);
                ed.LoadFileFromStream(tlkStream);
                elhw.Show();
                elhw.Activate();
            }
        }



        private bool CanLoadDLC()
        {
            return true;
        }

        private void PromptForDLC()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "SFAR files|*.sfar"
            };

            var result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                LoadFile(ofd.FileName);
            }
        }

        private void LoadFile(string sfarPath)
        {
            LoadedDLCPackage = new DLCPackage(sfarPath);
            BottomLeftText =
                $"{Path.GetFileName(sfarPath)}, compression scheme: {LoadedDLCPackage.Header.CompressionScheme}";

            RecentsController.AddRecent(sfarPath, false);
            RecentsController.SaveRecentList(true);
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length != 1) return;
                var file = files[0];
                if (file.EndsWith(".sfar", StringComparison.InvariantCultureIgnoreCase))
                {
                    LoadFile(file);
                }
            }
        }

        public void PropogateRecentsChange(IEnumerable<string> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "SFARExplorer";
    }
}