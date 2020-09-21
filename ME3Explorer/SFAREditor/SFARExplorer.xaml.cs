using System.IO;
using System.Windows;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using Microsoft.Win32;

namespace ME3Explorer.SFAREditor
{
    /// <summary>
    /// Interaction logic for SFARExplorer.xaml
    /// </summary>
    public partial class SFARExplorer : NotifyPropertyChangedWindowBase
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

        public SFARExplorer()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public GenericCommand LoadDLCCommand { get; set; }
        public RelayCommand OpenInPackageEditorCommand { get; set; }
        public RelayCommand OpenInTLKEditorCommand { get; set; }

        private void LoadCommands()
        {
            LoadDLCCommand = new GenericCommand(PromptForDLC, CanLoadDLC);
            OpenInPackageEditorCommand = new RelayCommand(openInPackageEditor, canOpenInPackageEditor);
            OpenInTLKEditorCommand = new RelayCommand(openInTlkEditor, canOpenInTlkEditor);
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
                loadSfar(ofd.FileName);
            }
        }

        private void loadSfar(string sfarPath)
        {
            LoadedDLCPackage = new DLCPackage(sfarPath);
            BottomLeftText =
                $"{Path.GetFileName(sfarPath)}, compression scheme: {LoadedDLCPackage.Header.CompressionScheme}";
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
                if (file.EndsWith(".sfar"))
                {
                    loadSfar(file);
                }
            }
        }
    }
}