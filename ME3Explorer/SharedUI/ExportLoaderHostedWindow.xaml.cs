using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ME3Explorer.Packages;
using static ME3Explorer.PackageEditorWPF;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for ExportLoaderHostedWindow.xaml
    /// </summary>
    public partial class ExportLoaderHostedWindow : WPFBase
    {
        private readonly IExportEntry LoadedExport;
        private readonly ExportLoaderControl hostedControl;
        public ObservableCollectionExtended<IndexedName> NamesList { get; } = new ObservableCollectionExtended<IndexedName>();
        public ExportLoaderHostedWindow(ExportLoaderControl hostedControl, IExportEntry exportToLoad)
        {
            this.hostedControl = hostedControl;
            this.LoadedExport = exportToLoad;
            NamesList.ReplaceAll(LoadedExport.FileRef.Names.Select((name, i) => new IndexedName(i, name))); //we replaceall so we don't add one by one and trigger tons of notifications

            InitializeComponent();
            RootPanel.Children.Add(hostedControl);
        }

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
                LoadMEPackage(LoadedExport.FileRef.FileName); //This will register the tool and assign a reference to it. Since this export is already in memory we will just reference the existing package instead.
                hostedControl.LoadExport(LoadedExport);
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
