using System;
using System.Windows;
using System.Windows.Threading;
using ME3Explorer.Packages;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for ExportLoaderHostedWindow.xaml
    /// </summary>
    public partial class ExportLoaderHostedWindow : Window
    {
        private IExportEntry exportToLoad;
        private ExportLoaderControl hostedControl;
        public ExportLoaderHostedWindow(ExportLoaderControl hostedControl, IExportEntry exportToLoad)
        {
            this.hostedControl = hostedControl;
            this.exportToLoad = exportToLoad;
            InitializeComponent();
            RootPanel.Children.Add(hostedControl);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                hostedControl.LoadExport(exportToLoad);
                exportToLoad = null; //no reference
            }));


        }
    }
}
