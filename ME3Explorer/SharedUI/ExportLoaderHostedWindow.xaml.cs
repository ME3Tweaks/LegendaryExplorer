using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ME3Explorer.CurveEd;
using ME3Explorer.Packages;
using Microsoft.Win32;
using static ME3Explorer.PackageEditorWPF;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for ExportLoaderHostedWindow.xaml
    /// </summary>
    public partial class ExportLoaderHostedWindow : WPFBase
    {
        public ExportEntry LoadedExport { get; }
        public readonly ExportLoaderControl HostedControl;
        public ObservableCollectionExtended<IndexedName> NamesList { get; } = new ObservableCollectionExtended<IndexedName>();
        public bool SupportsRecents => HostedControl is FileExportLoaderControl;
        public ExportLoaderHostedWindow(ExportLoaderControl hostedControl, ExportEntry exportToLoad)
        {
            DataContext = this;
            this.HostedControl = hostedControl;
            this.LoadedExport = exportToLoad;
            NamesList.ReplaceAll(LoadedExport.FileRef.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
            LoadCommands();
            InitializeComponent();
            HostedControl.PoppedOut(Recents_MenuItem);
            RootPanel.Children.Add(hostedControl);
        }

        public ExportLoaderHostedWindow(FileExportLoaderControl hostedControl, string file = null)
        {
            DataContext = this;
            this.HostedControl = hostedControl;
            //NamesList.ReplaceAll(LoadedExport.FileRef.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
            LoadCommands();
            InitializeComponent();
            HostedControl.PoppedOut(Recents_MenuItem);
            RootPanel.Children.Add(hostedControl);
            if (file != null)
            {
                hostedControl.LoadFile(file);
            }
        }

        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand LoadFileCommand { get; set; }
        public ICommand OpenFileCommand { get; set; }
        private void LoadCommands()
        {
            SaveCommand = new GenericCommand(SavePackage, CanSave);
            SaveAsCommand = new GenericCommand(SavePackageAs, CanSave);
            LoadFileCommand = new GenericCommand(LoadFile, CanLoadFile);
            OpenFileCommand = new GenericCommand(OpenFile, CanLoadFile);

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

        private void SavePackageAs()
        {
            if (HostedControl is FileExportLoaderControl felc)
            {
                felc.SaveAs();
            }
            else
            {
                string extension = Path.GetExtension(Pcc.FilePath);
                SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
                if (d.ShowDialog() == true)
                {
                    Pcc.save(d.FileName);
                    MessageBox.Show("Done");
                }
            }
        }

        private void SavePackage()
        {
            if (HostedControl is FileExportLoaderControl felc)
            {
                felc.Save();
            }
            else
            {
                Pcc.save();
            }
        }

        private bool PackageIsLoaded()
        {
            return Pcc != null;
        }

        public string CurrentFile => Pcc != null ? Path.GetFileName(Pcc.FilePath) : "";
        public override void handleUpdate(List<PackageUpdate> updates)
        {
            if (updates.Any(x => x.change == PackageChange.Names))
            {
                HostedControl.SignalNamelistAboutToUpdate();
                NamesList.ReplaceAll(Pcc.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
                HostedControl.SignalNamelistChanged();
            }

            //Put code to reload the export here
            foreach (var update in updates)
            {
                if ((update.change == PackageChange.ExportAdd || update.change == PackageChange.ExportData)
                    && update.index == LoadedExport.Index)
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
                    LoadMEPackage(LoadedExport.FileRef.FilePath); //This will register the tool and assign a reference to it. Since this export is already in memory we will just reference the existing package instead.
                    HostedControl.LoadExport(LoadedExport);
                    OnPropertyChanged(nameof(CurrentFile));
                }
            }));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                HostedControl.Dispose();
            }
        }
    }
}
