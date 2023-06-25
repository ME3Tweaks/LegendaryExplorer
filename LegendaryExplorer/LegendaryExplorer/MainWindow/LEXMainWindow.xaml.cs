using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Startup;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorer.MainWindow
{
    /// <summary>
    /// Interaction logic for LEXMainWindow.xaml
    /// </summary>
    public partial class LEXMainWindow : Window
    {
#if NIGHTLY
        public string LEXLogo => "/Resources/Images/Legendary_Explorer_Graphic2_Nightly.png";
#else
        public string LEXLogo => "/Resources/Images/Legendary_Explorer_Graphic2.png";
#endif

        public LEXMainWindow()
        {
            InitializeComponent();
            DataContext = this;

            //Check that at least one game path is set. If none are, show the initial dialog.
            if (!Settings.MainWindow_CompletedInitialSetup || 
                (ME1Directory.DefaultGamePath == null && ME2Directory.DefaultGamePath == null && 
                 ME3Directory.DefaultGamePath == null && LegendaryExplorerCoreLibSettings.Instance.LEDirectory == null))
            {
                new InitialSetup().ShowDialog();
            }

            if (ToolSet.Items.Any((t) => t.IsFavorited))
            {
                favoritesButton.IsChecked = true;
                SetToolListFromFavorites();
            }
            else
            {
                coreEditorsButton.IsChecked = true;
                SetToolList("Core Editors");
            }
            ToolSet.FavoritesChanged += ToolSet_FavoritesChanged;
            mainToolPanel.ToolMouseOver += Tool_MouseOver;

#if DEBUG
            MaxWidth = 915;
            toolsetDevsButton.Visibility = Visibility.Visible;
#endif

            Task.Run(TLKLoader.LoadSavedTlkList);
        }

        public void TransitionFromSplashToMainWindow(Window splashScreen)
        {
            if (Settings.MainWindow_DisableTransparencyAndAnimations)
            {
                Opacity = 1;
                AllowsTransparency = false;
                Show();
                splashScreen.Close();
                DependencyCheck.CheckDependencies(this);
            }
            else
            {
                Show();
                splashScreen.SetForegroundWindow();

                var sb = new Storyboard();
                sb.AddDoubleAnimation(1, 300, this, nameof(Opacity));
                sb.AddDoubleAnimation(0, 300, splashScreen, nameof(Opacity));
                sb.Completed += (_, _) =>
                {
                    splashScreen.Close();
                    DependencyCheck.CheckDependencies(this);
                };
                sb.Begin();
            }
        }

        private void ToolSet_FavoritesChanged(object sender, EventArgs e)
        {
            if(favoritesButton.IsChecked ?? false)
            {
                SetToolListFromFavorites();
            }
        }

        private void Tool_MouseOver(object sender, Tool t)
        {
            toolInfoPanel.Visibility = Visibility.Visible;
            toolInfoIcon.Source = t.icon;
            toolInfoText.Text = t.description;
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            new About().Show();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow().Show();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            new Help().Show();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearch();
        }

        private void SearchBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                ApplySearch();
            }
        }

        private void ApplySearch()
        {
            var results = new List<Tool>();
            string[] words = SearchBox.Text.ToLower().Split(' ');
            foreach (Tool tool in ToolSet.Items)
            {
                if (tool.open != null)
                {
                    //for each word we've typed in
                    if (words.Any(word => tool.tags.FuzzyMatch(word) || tool.name.ToLower().Contains(word) || tool.name.ToLower().Split(' ').FuzzyMatch(word)))
                    {
                        results.Add(tool);
                    }
                }
            }

            SetToolList(results);
            foreach (object child in LogicalTreeHelper.GetChildren(categoriesMenu))
            {
                if (child is RadioButton rb)
                {
                    rb.IsChecked = false;
                }
            }
        }

        private void Favorites_Clicked(object sender, RoutedEventArgs e)
        {
            SetToolListFromFavorites();
        }

        private void CategoryButton_Clicked(object sender, RoutedEventArgs e)
        {
            var button = (RadioButton)sender;
            string category = (string)button.Tag;
            SetToolList(category);
        }

        private void SetToolListFromFavorites()
        {
            var favorites = ToolSet.Items.Where(tool => tool.IsFavorited);
            SetToolList(favorites);
            if (!favorites.Any())
            {
                favoritesHint.Visibility = Visibility.Visible;
            }
        }

        private void SetToolList(string category)
        {
            SetToolList(ToolSet.Items.Where((t) => t.category == category || t.category2 == category));
        }

        private void SetToolList(IEnumerable<Tool> tools)
        {
            mainToolPanel.setToolList(tools);
            favoritesHint.Visibility = Visibility.Collapsed;
            toolInfoPanel.Visibility = Visibility.Collapsed;
        }

        private void SystemCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }
        
        private void MinimizeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void BackgroundMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (SearchBox.IsFocused)
                {
                    SearchBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
                this.DragMove();
            }
        }
    }
}
