using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorer.Tools.ObjectInstanceViewer
{
    /// <summary>
    /// Interaction logic for ObjectInstanceDBViewerWindow.xaml
    /// </summary>
    public partial class ObjectInstanceDBViewerWindow : NotifyPropertyChangedWindowBase
    {
        #region Properties
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private string _busyText;
        public string BusyText { get => _busyText; set => SetProperty(ref _busyText, value); }

        private string _statusText = "Loading";
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private string _searchTerm = "Loading";
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
                    AllInstancedFullPathsView.Refresh();
                }
            }
        }

        public MEGame Game { get; init; }

        #endregion

        private ObjectInstanceDB ObjectDB;

        private ObservableCollectionExtended<string> _allInstancedFullPaths { get; } = new();
        public ICollectionView AllInstancedFullPathsView => CollectionViewSource.GetDefaultView(_allInstancedFullPaths);

        public ObjectInstanceDBViewerWindow(MEGame game)
        {
            Game = game;
            InitializeComponent();
            AllInstancedFullPathsView.Filter = FilterObjects;
        }

        private bool FilterObjects(object obj)
        {
            if (obj is string ifp)
            {
                if (string.IsNullOrWhiteSpace(SearchTerm)) return true; //no filter
                return ifp.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            IsBusy = true;
            StatusText = $"Loading ObjectInstanceDB for {Game}";
            Task.Run(() =>
            {
                string objectDBPath = AppDirectories.GetObjectDatabasePath(Game);
                using FileStream fs = File.OpenRead(objectDBPath);
                ObjectDB = ObjectInstanceDB.Deserialize(Game, fs);
                return ObjectDB.GetAllObjectPaths(true).ToList(); // We do .ToList() here because it makes it run on background thread. Better responsiveness, but uses a bit more memory temporarily
            }).ContinueWithOnUIThread(x =>
            {
                _allInstancedFullPaths.ReplaceAll(x.Result);
                StatusText = $"Displaying {_allInstancedFullPaths.Count} unique object paths";
                IsBusy = false;
            });
        }
    }
}
