using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ME3Explorer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool CICOpen = true;
        private bool SearchOpen = false;

        public MainWindow()
        {
            InitializeComponent();
            Tools.InitializeTools();
            installModspanel.setToolList(Tools.items.Where(x => x.tags.Contains("user")));
            favoritesPanel.setToolList(Tools.items.Where(x => x.tags.Contains("developer")));
            searchPanel.setToolList(Tools.items.Where(x => x.tags.Contains("utility")));
        }

        private void Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        private void MaximizeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
        }

        private void MinimizeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
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

        private void About_Click(object sender, RoutedEventArgs e)
        {
            (new AboutME3Explorer()).Show();
        }

        private void Debug_Click(object sender, RoutedEventArgs e)
        {
            KFreonLib.Debugging.DebugOutput.StartDebugger("ME3Explorer Main Window");
        }
        
        private void Logo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CICOpen = !CICOpen;
            Image img = sender as Image;
            img.Source = (ImageSource)img.FindResource(CICOpen ? "LogoOnImage" : "LogoOffImage");
            DoubleAnimation anim;
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(300);
            if (CICOpen)
            {
                anim = new DoubleAnimation(650, timeSpan);
                if (SearchBox.Text.Trim() != string.Empty)
                {
                    SearchOpen = true;
                    searchPanel.BeginAnimation(WidthProperty, new DoubleAnimation(300, TimeSpan.FromMilliseconds(200)));
                }
            }
            else
            {
                anim = new DoubleAnimation(0, timeSpan);
                if (SearchOpen)
                {
                    SearchOpen = false;
                    searchPanel.BeginAnimation(WidthProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(100)));
                }
            }
            CICPanel.BeginAnimation(WidthProperty, anim);
        }

        private void LinkLabel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label l = sender as Label;
            if (l != null)
            {
                switch (l.Content as string)
                {
                    case "GitHub":
                        Process.Start("https://github.com/ME3Explorer/ME3Explorer");
                        break;
                    case "Nexus":
                        Process.Start("http://www.nexusmods.com/masseffect3/mods/409/?");
                        break;
                    case "Forums":
                        Process.Start("http://me3explorer.freeforums.org/");
                        break;
                    case "Wikia":
                        Process.Start("http://me3explorer.wikia.com/wiki/ME3Explorer_Wiki");
                        break;
                    default:
                        break;
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!SearchOpen)
            {
                SearchOpen = true;
                searchPanel.BeginAnimation(WidthProperty, new DoubleAnimation(300, TimeSpan.FromMilliseconds(200)));
            }

            List<Tool> results = new List<Tool>();
            string[] words = SearchBox.Text.ToLower().Split(' ');
            foreach (Tool tool in Tools.items)
            {
                foreach (string word in words)
                {
                    if (tool.tags.FuzzyMatch(word) || tool.name.ToLower().Split(' ').FuzzyMatch(word))
                    {
                        results.Add(tool);
                        break;
                    }
                }
            }
            searchPanel.setToolList(results);
        }
        
        private void SearchBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (SearchOpen && SearchBox.Text.Trim() == string.Empty)
            {
                SearchOpen = false;
                searchPanel.BeginAnimation(WidthProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
            }
        }
    }
}
