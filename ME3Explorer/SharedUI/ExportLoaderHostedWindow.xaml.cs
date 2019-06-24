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
        public IExportEntry LoadedExport { get; }
        private readonly ExportLoaderControl hostedControl;
        public ObservableCollectionExtended<IndexedName> NamesList { get; } = new ObservableCollectionExtended<IndexedName>();
        public ExportLoaderHostedWindow(ExportLoaderControl hostedControl, IExportEntry exportToLoad)
        {
            DataContext = this;
            this.hostedControl = hostedControl;
            this.LoadedExport = exportToLoad;
            NamesList.ReplaceAll(LoadedExport.FileRef.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
            LoadCommands();
            InitializeComponent();
            RootPanel.Children.Add(hostedControl);
        }

        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        private void LoadCommands()
        {
            SaveCommand = new GenericCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, PackageIsLoaded);
        }

        private void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void SavePackage()
        {
            Pcc.save();
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
                hostedControl.SignalNamelistAboutToUpdate();
                NamesList.ReplaceAll(Pcc.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications
                hostedControl.SignalNamelistChanged();
            }

            //Put code to reload the export here
            foreach (var update in updates)
            {
                if ((update.change == PackageChange.ExportAdd || update.change == PackageChange.ExportData)
                    && update.index == LoadedExport.Index)
                {
                    hostedControl.LoadExport(LoadedExport); //reload export
                    return;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                LoadMEPackage(LoadedExport.FileRef.FilePath); //This will register the tool and assign a reference to it. Since this export is already in memory we will just reference the existing package instead.
                hostedControl.LoadExport(LoadedExport);
                OnPropertyChanged(nameof(CurrentFile));
            }));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                hostedControl.Dispose();
            }
        }
    }
}
