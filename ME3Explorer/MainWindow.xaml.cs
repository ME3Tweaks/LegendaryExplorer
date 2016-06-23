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

        public MainWindow()
        {
            InitializeComponent();
            Tools.InitializeTools();
            installModspanel.setToolList(Tools.items.Where(x => x.tags.Contains("user")));
            favoritesPanel.setToolList(Tools.items.Where(x => x.tags.Contains("developer")));
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
            Storyboard sb;
            if (CICOpen)
            {
                sb = FindResource("sbSlideOutPanel") as Storyboard;
            }
            else
            {
                sb = FindResource("sbSlideInPanel") as Storyboard;
            }
            if (sb != null)
            {
                sb.Begin(CICPanel);
            }
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
    }
}
