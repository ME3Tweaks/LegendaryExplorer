using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for TaskPane.xaml
    /// </summary>
    public partial class TaskPane : UserControl
    {
        public event EventHandler<WPFBaseViewModel> ToolMouseOver;

        Button highlightedButton;

        public ObservableCollection<PackageViewModel> Packages { get; } = new ObservableCollection<PackageViewModel>();

        public TaskPane()
        {
            InitializeComponent();
            fileList.bind(ItemsControl.ItemsSourceProperty, this, nameof(Packages));
            MEPackageHandler.packagesInTools.CollectionChanged += PackagesInTools_CollectionChanged;
        }

        private async void PackagesInTools_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems?.Count > 0)
            {
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    var temp = Packages[i + e.OldStartingIndex];
                    Packages.RemoveAt(i + e.OldStartingIndex);
                    temp.Dispose();
                }
            }

            if (e.NewItems?.Count > 0)
            {
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    Packages.Insert(i + e.NewStartingIndex, new PackageViewModel(MEPackageHandler.packagesInTools[i + e.NewStartingIndex]));
                }
            }

            //wait for the UI to reflect the collection change
            await Task.Delay(100);
            if (fileList.ActualHeight > topScrollViewer.ActualHeight)
            {
                scrollIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                scrollIndicator.Visibility = Visibility.Hidden;
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ||
                e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true); 
            }
        }

        protected virtual void Button_Click(object sender, RoutedEventArgs e)
        {
            ((sender as Button)?.DataContext as WPFBase)?.RestoreAndBringToFront();
        }


        protected void Button_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) &&
                (sender as Button)?.DataContext is WPFBaseViewModel wpf)
            {
                var type = wpf.GetType();
                if (Tools.Items.FirstOrDefault(t => t.type == type) is Tool tool)
                {
                    tool.IsFavorited = !tool.IsFavorited;
                }
            }
        }

        protected virtual void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button b)
            {
                deselectButton(highlightedButton);
                highlightedButton = b;
                if (b.FindName("highlightUnderline") is Rectangle r)
                {
                    r.Visibility = Visibility.Visible;
                }
                if (b.FindName("toolIcon") is Image img)
                {
                    img.Opacity = 1;
                }
                ToolMouseOver?.Invoke(sender, b.DataContext as WPFBaseViewModel);
            }
        }

        private static void deselectButton(Button b)
        {
            if (b != null)
            {
                if (b.FindName("highlightUnderline") is Rectangle r)
                {
                    r.Visibility = Visibility.Hidden;
                }

                if (b.FindName("toolIcon") is Image img)
                {
                    img.Opacity = 0.85;
                }
            }
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            ((sender as Button)?.DataContext as IMEPackage)?.Save();
        }
    }

    public class PackageViewModel : NotifyPropertyChangedBase, IDisposable
    {
        private readonly IMEPackage pcc;

        public ObservableCollectionExtended<WPFBaseViewModel> Users { get; } = new ObservableCollectionExtended<WPFBaseViewModel>();

        public PackageViewModel(IMEPackage pckg)
        {
            pcc = pckg;

            (pcc as UnrealPackageFile).PropertyChanged += Upk_PropertyChanged;
            FilePath = pcc.FilePath;
            IsModified = pcc.IsModified;
            FileSize = pcc.FileSize;
            LastSaved = pcc.LastSaved;
            Game = pcc.Game;

            Users.AddRange(pcc.Users.Select(usr => new WPFBaseViewModel(usr)));
            pcc.Users.CollectionChanged += Users_CollectionChanged;
        }

        private void Upk_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IMEPackage.IsModified):
                    IsModified = pcc.IsModified;
                    break;
                case nameof(IMEPackage.FileSize):
                    FileSize = pcc.FileSize;
                    break;
                case nameof(IMEPackage.LastSaved):
                    LastSaved = pcc.LastSaved;
                    break;
            }
        }

        private void Users_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems?.Count > 0)
            {
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    Users.RemoveAt(i + e.OldStartingIndex);
                }
            }

            if (e.NewItems?.Count > 0)
            {
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    Users.Insert(i + e.NewStartingIndex, new WPFBaseViewModel(pcc.Users[i + e.NewStartingIndex]));
                }
            }
        }

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        private bool _isModified;

        public bool IsModified
        {
            get => _isModified;
            set => SetProperty(ref _isModified, value);
        }

        private long _fileSize;

        public long FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        private DateTime _lastSaved;

        public DateTime LastSaved
        {
            get => _lastSaved;
            set => SetProperty(ref _lastSaved, value);
        }

        private MEGame _game;

        public MEGame Game
        {
            get => _game;
            set => SetProperty(ref _game, value);
        }

        public void Dispose()
        {
            if (pcc != null)
            {
                pcc.Users.CollectionChanged -= Users_CollectionChanged;
                (pcc as UnrealPackageFile).PropertyChanged -= Upk_PropertyChanged;
            }
        }
    }

    public class WPFBaseViewModel : NotifyPropertyChangedBase
    {
        public WPFBase wpf;
        public WPFBaseViewModel(IPackageUser wpfBase)
        {
            wpf = wpfBase as WPFBase;
            var type = wpfBase.GetType();
            if (Tools.Items.FirstOrDefault(t => t.type == type) is Tool tool)
            {
                ToolName = tool.name;
                Icon = tool.icon;
            }
            
        }

        private string _toolName;

        public string ToolName
        {
            get => _toolName;
            set => SetProperty(ref _toolName, value);
        }

        private ImageSource _icon;

        public ImageSource Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }
    }
}
