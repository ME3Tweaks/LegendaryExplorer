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
        private bool AdvancedOpen = false;
        private bool UtilitiesOpen = false;
        private bool CreateModsOpen = false;
        private bool ToolInfoPanelOpen = false;
        
        Brush HighlightBrush = Application.Current.FindResource("HighlightColor") as Brush;
        Brush LabelTextBrush = Application.Current.FindResource("LabelTextBrush") as Brush;

        public bool DisableFlyouts
        {
            get { return (bool)GetValue(DisableFlyoutsProperty); }
            set { SetValue(DisableFlyoutsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DisableFlyouts.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DisableFlyoutsProperty =
            DependencyProperty.Register("DisableFlyouts", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        

        public MainWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                SystemCommands.CloseWindow(this);
            }
            installModspanel.setToolList(Tools.Items.Where(x => x.tags.Contains("user")));
            favoritesPanel.setToolList(Tools.Items.Where(x => x.IsFavorited));
            Tools.FavoritesChanged += Tools_FavoritesChanged;
            utilitiesPanel.setToolList(Tools.Items.Where(x => x.tags.Contains("utility")));
            createModsPanel.setToolList(Tools.Items.Where(x => x.tags.Contains("developer")));

            DisableFlyouts = Properties.Settings.Default.DisableToolDescriptions;
            disableSetupCheckBox.IsChecked = Properties.Settings.Default.DisableDLCCheckOnStart;
        }

        private void Tools_FavoritesChanged(object sender, EventArgs e)
        {
            favoritesPanel.setToolList(Tools.Items.Where(x => x.IsFavorited));
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
            if (CICOpen)
            {
                closeCIC();
            }
            else
            {
                if (CreateModsOpen)
                {
                    closeCreateMods(100);
                }
                if (UtilitiesOpen)
                {
                    closeUtilities(100);
                }
                CICOpen = true;
                Logo.Source = (ImageSource)Logo.FindResource("LogoOnImage");
                if (SearchBox.Text.Trim() != string.Empty)
                {
                    SearchOpen = true;
                    searchPanel.BeginDoubleAnimation(WidthProperty, 300, 200);
                }
                CICPanel.BeginDoubleAnimation(WidthProperty, 650, 300);
            }
        }

        private void closeCIC(int duration = 300)
        {
            CICOpen = false;
            Logo.Source = (ImageSource)Logo.FindResource("LogoOffImage");
            if (SearchOpen)
            {
                SearchOpen = false;
                searchPanel.BeginDoubleAnimation(WidthProperty, 0, duration / 3);
            }
            if (AdvancedOpen)
            {
                AdvancedOpen = false;
                advancedPanel.BeginDoubleAnimation(WidthProperty, 0, duration / 3);
            }
            CICPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
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
                if (AdvancedOpen)
                {
                    AdvancedOpen = false;
                    advancedPanel.BeginDoubleAnimation(WidthProperty, 0, 100);
                }
                SearchOpen = true;
                searchPanel.BeginDoubleAnimation(WidthProperty, 300, 200);
            }

            List<Tool> results = new List<Tool>();
            string[] words = SearchBox.Text.ToLower().Split(' ');
            foreach (Tool tool in Tools.Items)
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
                searchPanel.BeginDoubleAnimation(WidthProperty, 0, 200);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.DisableDLCCheckOnStart = disableSetupCheckBox.IsChecked ?? false;
            Properties.Settings.Default.DisableToolDescriptions = DisableFlyouts;
        }

        private void advancedSettings_Click(object sender, RoutedEventArgs e)
        {
            AdvancedOpen = !AdvancedOpen;
            if (AdvancedOpen)
            {
                if (SearchOpen)
                {
                    SearchOpen = false;
                    searchPanel.BeginDoubleAnimation(WidthProperty, 0, 100);
                }
                advancedPanel.BeginDoubleAnimation(WidthProperty, 300, 200);
            }
            else
            {
                advancedPanel.BeginDoubleAnimation(WidthProperty, 0, 200);
            }
        }

        private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!SearchOpen && SearchBox.Text.Trim() != string.Empty)
            {
                SearchOpen = true;
                searchPanel.BeginDoubleAnimation(WidthProperty, 300, 200);
                if (AdvancedOpen)
                {
                    AdvancedOpen = false;
                    advancedPanel.BeginDoubleAnimation(WidthProperty, 0, 100);
                }
            }
        }

        private void UtilitiesButton_Click(object sender, RoutedEventArgs e)
        {
            if (UtilitiesOpen)
            {
                closeUtilities();
            }
            else
            {
                UtilitiesOpen = true;
                if (CICOpen)
                {
                    closeCIC(100);
                }
                if (CreateModsOpen)
                {
                    closeCreateMods(100);
                }
                utilitiesButton.OpacityMask = HighlightBrush;
                utilitiesPanel.BeginDoubleAnimation(WidthProperty, 650, 300);
            }
        }

        private void closeUtilities(int duration = 300)
        {
            if (ToolInfoPanelOpen)
            {
                ToolInfoPanelOpen = false;
                toolInfoPanel.BeginDoubleAnimation(WidthProperty, 0, 50);
            }
            UtilitiesOpen = false;
            utilitiesPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
            utilitiesButton.OpacityMask = LabelTextBrush;
        }

        private void CreateModsButton_Click(object sender, RoutedEventArgs e)
        {
            if (CreateModsOpen)
            {
                closeCreateMods();
            }
            else
            {
                CreateModsOpen = true;
                if (CICOpen)
                {
                    closeCIC(100);
                }
                if (UtilitiesOpen)
                {
                    closeUtilities(100);
                }
                createModsButton.OpacityMask = HighlightBrush;
                createModsPanel.BeginDoubleAnimation(WidthProperty, 650, 300);
            }
        }

        private void closeCreateMods(int duration = 300)
        {
            if (ToolInfoPanelOpen)
            {
                ToolInfoPanelOpen = false;
                toolInfoPanel.BeginDoubleAnimation(WidthProperty, 0, 50);
            }
            CreateModsOpen = false;
            createModsPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
            createModsButton.OpacityMask = LabelTextBrush;
        }

        private void utilitiesPanel_ToolMouseOver(object sender, Tool e)
        {
            openToolInfo(e);
        }

        private void openToolInfo(Tool e)
        {
            toolInfoPanel.setTool(e);
            if (!ToolInfoPanelOpen && !DisableFlyouts)
            {
                ToolInfoPanelOpen = true;
                toolInfoPanel.BeginDoubleAnimation(WidthProperty, 300, 50);
            }
        }

        private void createModsPanel_ToolMouseOver(object sender, Tool e)
        {
            openToolInfo(e);
        }
    }
}
