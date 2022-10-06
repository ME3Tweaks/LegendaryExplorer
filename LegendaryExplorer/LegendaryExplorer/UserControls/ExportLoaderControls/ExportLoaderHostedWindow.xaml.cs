using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for ExportLoaderHostedWindow.xaml
    /// </summary>
    public partial class ExportLoaderHostedWindow : WPFBase, IRecents
    {
        public ExportEntry LoadedExport { get; private set; }
        public readonly ExportLoaderControl HostedControl;
        public ObservableCollectionExtended<IndexedName> NamesList { get; } = new();
        public bool SupportsRecents => HostedControl is FileExportLoaderControl;

        private bool _fileHasPendingChanges;
        public bool FileHasPendingChanges
        {
            get => _fileHasPendingChanges;
            set
            {
                SetProperty(ref _fileHasPendingChanges, value);
                OnPropertyChanged(nameof(IsModifiedProxy));
            }
        }
        public bool IsModifiedProxy
        {
            get
            {
                if (LoadedExport != null)
                {
                    return LoadedExport.EntryHasPendingChanges;
                }
                if (HostedControl is FileExportLoaderControl felc)
                {
                    return felc.FileModified;
                }
                return true;
            }
        }

        /// <summary>
        /// Opens ELHW with an export and the specified tool.
        /// </summary>
        /// <param name="hostedControl"></param>
        /// <param name="exportToLoad"></param>
        public ExportLoaderHostedWindow(ExportLoaderControl hostedControl, ExportEntry exportToLoad) : base($"ELHW for {hostedControl.GetType()}")
        {
            DataContext = this;
            HostedControl = hostedControl;
            LoadedExport = exportToLoad;
            LoadedExport.EntryModifiedChanged += NotifyPendingChangesStatusChanged;
            NamesList.ReplaceAll(LoadedExport.FileRef.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
            LoadCommands();
            InitializeComponent();
            ContentGrid.Children.Add(HostedControl);
            HostedControl.IsPoppedOut = true;
            HostedControl.PoppedOut(this);
            switch (HostedControl)
            {
                case BinaryInterpreterWPF binaryInterpreterWpf:
                    binaryInterpreterWpf.bind(BinaryInterpreterWPF.SubstituteImageForHexBoxProperty, this, nameof(IsBusy));
                    break;
                case BytecodeEditor bytecodeEditor:
                    bytecodeEditor.bind(BytecodeEditor.SubstituteImageForHexBoxProperty, this, nameof(IsBusy));
                    break;
                case EntryMetadataExportLoader entryMetadataExportLoader:
                    entryMetadataExportLoader.bind(EntryMetadataExportLoader.SubstituteImageForHexBoxProperty, this, nameof(IsBusy));
                    break;
                case InterpreterExportLoader interpreterExportLoader:
                    interpreterExportLoader.bind(InterpreterExportLoader.SubstituteImageForHexBoxProperty, this, nameof(IsBusy));
                    break;
            }

            ConfigureRecents();
        }

        private void ConfigureRecents()
        {
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, HostedControl is FileExportLoaderControl felc ? felc.LoadFile : null);
        }

        private void NotifyPendingChangesStatusChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsModifiedProxy));
        }



        /// <summary>
        /// Opens ELFH with a file loader and an optional file.
        /// </summary>
        /// <param name="hostedControl"></param>
        /// <param name="file"></param>
        public ExportLoaderHostedWindow(FileExportLoaderControl hostedControl, string file = null) : base($"ELHW for {hostedControl.GetType()}")
        {
            DataContext = this;
            this.HostedControl = hostedControl;
            hostedControl.FileLoaded += FELCFileLoaded;
            hostedControl.ModifiedStatusChanging += NotifyPendingChangesStatusChanged;
            //NamesList.ReplaceAll(LoadedExport.FileRef.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
            LoadCommands();
            InitializeComponent();
            HostedControl.PoppedOut(this);
            ContentGrid.Children.Add(hostedControl);
            RecentsController.InitRecentControl(hostedControl.Toolname, Recents_MenuItem, hostedControl.LoadFile);
            if (file != null)
            {
                hostedControl.LoadFile(file);
            }
        }

        /// <summary>
        /// Invoked when a FELC file is loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FELCFileLoaded(object sender, EventArgs e)
        {
            if (HostedControl is FileExportLoaderControl felc)
            {
                OnPropertyChanged(nameof(ShouldShowRecentsController));
                if (felc.Toolname != null)
                {
                    // Update recents
                    RecentsController.AddRecent(felc.LoadedFile, false, null); // If we ever support games in FELC we should change the EventArgs
                    RecentsController.PropogateRecentsChange(true, RecentsController.RecentItems);
                }
            }
        }

        public bool ShouldShowRecentsController => HostedControl is FileExportLoaderControl felc && felc.LoadedFile == null; // Only File Export Loaders support Recents

        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand LoadFileCommand { get; set; }
        public ICommand OpenFileCommand { get; set; }
        public ICommand ReloadCurrentExportCommand { get; set; }
        private void LoadCommands()
        {
            SaveCommand = new GenericCommand(SavePackage, CanSave);
            SaveAsCommand = new GenericCommand(SavePackageAs, CanSave);
            LoadFileCommand = new GenericCommand(LoadFile, CanLoadFile);
            OpenFileCommand = new GenericCommand(OpenFile, CanLoadFile);
            ReloadCurrentExportCommand = new GenericCommand(ReloadCurrentExport, IsExportLoaded);
        }

        private void ReloadCurrentExport()
        {
            var exp = HostedControl.CurrentLoadedExport;
            HostedControl.UnloadExport();
            HostedControl.LoadExport(exp);
        }

        private bool IsExportLoaded()
        {
            if (HostedControl is FileExportLoaderControl felc && felc.LoadedFile != null) return false;
            if (HostedControl.CurrentLoadedExport != null) return true;
            return false;
        }

        private bool CanSave()
        {
            if (HostedControl is FileExportLoaderControl felc)
            {
                return felc.CanSave();
            }
            else
            {
                return true;
            }
        }

        private void OpenFile()
        {
            if (HostedControl is FileExportLoaderControl felc)
            {
                FileHasPendingChanges = false;
                felc.OpenFile();
            }
        }

        private bool CanLoadFile()
        {
            return HostedControl is FileExportLoaderControl felc && felc.CanLoadFile();
        }

        private void LoadFile()
        {
            var felc = HostedControl as FileExportLoaderControl;
            felc.OpenFile();
        }

        private async void SavePackageAs()
        {
            if (HostedControl is FileExportLoaderControl felc)
            {
                felc.SaveAs();
                FileHasPendingChanges = false;
            }
            else
            {
                string extension = Path.GetExtension(Pcc.FilePath);
                var d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
                if (d.ShowDialog() == true)
                {
                    await Pcc.SaveAsync(d.FileName);
                    MessageBox.Show("Done");
                }
            }
        }

        private async void SavePackage()
        {
            if (HostedControl is FileExportLoaderControl felc)
            {
                felc.Save();
                FileHasPendingChanges = false;
            }
            else
            {
                await Pcc.SaveAsync();
            }
        }

        public string CurrentFile => Pcc != null ? Path.GetFileName(Pcc.FilePath) : "";
        public override void HandleUpdate(List<PackageUpdate> updates)
        {
            if (updates.Any(x => x.Change.Has(PackageChange.Name)))
            {
                HostedControl.SignalNamelistAboutToUpdate();
                NamesList.ReplaceAll(Pcc.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
                HostedControl.SignalNamelistChanged();
            }

            //Put code to reload the export here
            foreach (var update in updates)
            {
                if ((update.Change.Has(PackageChange.Export))
                    && update.Index == LoadedExport.UIndex)
                {
                    HostedControl.LoadExport(LoadedExport); //reload export
                    return;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (LoadedExport != null)
                {
                    // This will register the tool and assign a reference to it.
                    // Since this export is already in memory we will just reference the existing package instead.
                    RegisterPackage(LoadedExport.FileRef);
                    HostedControl.LoadExport(LoadedExport);
                    OnPropertyChanged(nameof(CurrentFile));
                }
            }));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                if (LoadedExport != null)
                {
                    LoadedExport.EntryModifiedChanged -= NotifyPendingChangesStatusChanged;
                    LoadedExport = null;
                }
                if (HostedControl is FileExportLoaderControl felc)
                {
                    felc.FileLoaded -= FELCFileLoaded;
                    felc.ModifiedStatusChanging -= NotifyPendingChangesStatusChanged;
                }

                HostedControl.Dispose();
            }
        }

        private void ExportLoaderHostedWindow_OnDrop(object sender, DragEventArgs e)
        {

            if (e.Data.GetDataPresent(DataFormats.FileDrop) && HostedControl is FileExportLoaderControl felc)
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                if (felc.CanLoadFileExtension(Path.GetExtension(files[0])))
                {
                    felc.LoadFile(files[0]);
                }
            }
        }

        private void ExportLoaderHostedWindow_OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && HostedControl is FileExportLoaderControl felc)
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (!felc.CanLoadFileExtension(ext))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            if (HostedControl is FileExportLoaderControl felc && felc.Toolname != null && felc.Toolname == propogationSource)
            {
                RecentsController.PropogateRecentsChange(false, newRecents);
            }
        }

        public string Toolname => HostedControl is FileExportLoaderControl felc ? felc.Toolname : null;

        private void ExportLoaderHostedWindow_OnContentRendered(object sender, EventArgs e)
        {
            // If popped open with a file we should do this here
            OnPropertyChanged(nameof(ShouldShowRecentsController));
        }
    }
}
