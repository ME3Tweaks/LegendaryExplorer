using System;
using System.ComponentModel;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.GameFilesystem;
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

        private string _searchTerm;
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

        private string _selectedObject;
        public string SelectedObject
        {
            get => _selectedObject;
            set
            {
                if (SetProperty(ref _selectedObject, value))
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        Instances.ClearEx();
                    }
                    else if (ObjectDB != null)
                    {
                        var instances = ObjectDB.GetFilesContainingObject(value);
                        if (instances != null)
                        {
                            Instances.ReplaceAll(instances);
                        }
                        else
                        {
                            // Not found
                            Instances.ClearEx();
                        }
                    }
                }
            }
        }

        private string _selectedFileInstance;
        public string SelectedFileInstance { get => _selectedFileInstance; set => SetProperty(ref _selectedFileInstance, value); }
        public ICommand OpenInPackageEditorCommand { get; set; }
        public MEGame Game { get; init; }

        #endregion

        private ObjectInstanceDB ObjectDB;
        public ObservableCollectionExtended<string> Instances { get; } = new();
        private ObservableCollectionExtended<string> _allInstancedFullPaths { get; } = new();
        public ICollectionView AllInstancedFullPathsView => CollectionViewSource.GetDefaultView(_allInstancedFullPaths);
        public ObjectInstanceDBViewerWindow(MEGame game)
        {
            Game = game;
            LoadCommands();
            InitializeComponent();
            AllInstancedFullPathsView.Filter = FilterObjects;
        }

        private void LoadCommands()
        {
            OpenInPackageEditorCommand = new RelayCommand(OpenInPE, CanOpen);
        }

        private bool CanOpen(object obj)
        {
            if (obj is string str)
            {
                var gameDir = MEDirectories.GetDefaultGamePath(Game);
                if (gameDir == null || !Directory.Exists(gameDir))
                    return false; // No can do
                var fPath = Path.Combine(gameDir, str);
                return File.Exists(fPath);
            }

            return false;
        }

        private void OpenInPE(object obj)
        {
            if (obj is string str)
            {
                var gameDir = MEDirectories.GetDefaultGamePath(Game);
                var fPath = Path.Combine(gameDir, str);
                var p = new PackageEditor.PackageEditorWindow();
                p.Show();
                p.LoadFile(fPath, goToEntry: SelectedObject);
                p.Activate(); //bring to front
            }
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
