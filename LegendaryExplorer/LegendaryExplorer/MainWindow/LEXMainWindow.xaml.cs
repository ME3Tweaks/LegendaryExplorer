using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
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
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorer.MainWindow
{
    /// <summary>
    /// Interaction logic for LEXMainWindow.xaml
    /// </summary>
    public partial class LEXMainWindow : Window
    {
        public LEXMainWindow()
        {
            InitializeComponent();
            DataContext = this;

            if (ToolSet.Items.Any((t) => t.IsFavorited))
            {
                favoritesButton.IsChecked = true;
                mainToolPanel.setToolList(ToolSet.Items.Where(t => t.IsFavorited));
            }
            else
            {
                coreEditorsButton.IsChecked = true;
                mainToolPanel.setToolList(ToolSet.Items.Where(t => t.category == "Core Editors" || t.category2 == "Core Editors"));
            }
            ToolSet.FavoritesChanged += ToolSet_FavoritesChanged;
            mainToolPanel.ToolMouseOver += Tool_MouseOver;

#if DEBUG
            toolsetDevsButton.Visibility = Visibility.Visible;
#endif

            Task.Run(TLKLoader.LoadSavedTlkList);
        }

        /// <summary>
        /// Displayed version in the UI. About page will be more detailed.
        /// </summary>
        public string DisplayedVersion
        {
            get
            {
                Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
                string version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";
                if (assemblyVersion.Build != 0)
                {
                    version += "." + assemblyVersion.Build;
                }
                
#if DEBUG
                version += " DEBUG";
#elif NIGHTLY
                //This is what will be placed in release. Comment this out when building for a stable!
                version += " NIGHTLY"; //ENSURE THIS IS CHANGED FOR MAJOR RELEASES AND RELEASE CANDIDATES
#elif RELEASE
                // UPDATE THIS FOR RELEASE
                //version += " RC";
#endif
                return $"{version} {App.BuildDateTime.ToShortDateString()}";
            }
        }

        public void TransitionFromSplashToMainWindow(Window splashScreen)
        {
            if (Settings.MainWindow_DisableTransparencyAndAnimations)
            {
                Opacity = 1;
                AllowsTransparency = false;
                Show();
                splashScreen.Close();
            }
            else
            {
                Show();
                splashScreen.SetForegroundWindow();

                var sb = new Storyboard();
                sb.AddDoubleAnimation(1, 300, this, nameof(Opacity));
                sb.AddDoubleAnimation(0, 300, splashScreen, nameof(Opacity));
                sb.Completed += (_, _) => splashScreen.Close();
                sb.Begin();
            }
        }

        private void ToolSet_FavoritesChanged(object sender, EventArgs e)
        {
            if(favoritesButton.IsChecked ?? false)
            {
                mainToolPanel.setToolList(ToolSet.Items.Where(tool => tool.IsFavorited));
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
            (new About()).Show();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
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

            mainToolPanel.setToolList(results);
            toolInfoPanel.Visibility = Visibility.Collapsed;
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
            mainToolPanel.setToolList(ToolSet.Items.Where(tool => tool.IsFavorited));
            toolInfoPanel.Visibility = Visibility.Collapsed;
        }

        private void CategoryButton_Clicked(object sender, RoutedEventArgs e)
        {
            var button = (RadioButton)sender;
            var category = (string)button.Tag;
            mainToolPanel.setToolList(ToolSet.Items.Where((t) => t.category == category || t.category2 == category));
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
