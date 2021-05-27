using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UnrealExtensions;

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

            if (ToolSet.Items.Any((t) => t.IsFavorited))
            {
                mainToolPanel.setToolList(ToolSet.Items.Where(t => t.IsFavorited));
            }
            else
            {
                mainToolPanel.setToolList(ToolSet.Items.Where(t => t.category == "Core Editors" || t.category2 == "Core Editors"));
            }
            ToolSet.FavoritesChanged += ToolSet_FavoritesChanged;

#if DEBUG
            toolsetDevsButton.Visibility = Visibility.Visible;
#endif

            Task.Run(TLKLoader.LoadSavedTlkList);
        }

        private void ToolSet_FavoritesChanged(object sender, EventArgs e)
        {
            // TODO: If favorite tab is selected, update displayed tools
            // We need this event handler to exist for the favorites system to work
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
        }

        private void Favorites_Clicked(object sender, RoutedEventArgs e)
        {
            mainToolPanel.setToolList(ToolSet.Items.Where(tool => tool.IsFavorited));
        }

        private void CategoryButton_Clicked(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var category = (string) button.Tag;
            mainToolPanel.setToolList(ToolSet.Items.Where((t) => t.category == category || t.category2 == category));
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
