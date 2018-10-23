using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
using FontAwesome.WPF;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;
using Microsoft.Win32;

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
        private bool TaskPaneOpen = false;
        private bool ToolInfoPanelOpen = false;
        private bool PathsPanelOpen = false;
        private bool TaskPaneInfoPanelOpen = false;

        Brush HighlightBrush = Application.Current.FindResource("HighlightColor") as Brush;
        Brush LabelTextBrush = Application.Current.FindResource("LabelTextBrush") as Brush;
        public static double dpiScaleX = 1;
        public static double dpiScaleY = 1;

        private static FieldInfo _menuDropAlignmentField;

        public MainWindow()
        {
            //exception occurs in InitializeComponent() without try block, but doesn't if present. wtf
            try
            {
                InitializeComponent();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                SystemCommands.CloseWindow(this);
            }

            //make menu's appear right side, which is busted on WPF with touchscreens
            _menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
            System.Diagnostics.Debug.Assert(_menuDropAlignmentField != null);

            EnsureStandardPopupAlignment();
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;



            installModspanel.setToolList(Tools.Items.Where(x => x.tags.Contains("user")));
            Tools.FavoritesChanged += Tools_FavoritesChanged;
            Tools_FavoritesChanged(null, null);
            utilitiesPanel.setToolList(Tools.Items.Where(x => x.tags.Contains("utility")));
            createModsPanel.setToolList(Tools.Items.Where(x => x.tags.Contains("developer")));

            Topmost = Properties.Settings.Default.AlwaysOnTop;
            //Check that at least one game path is set. If none are, show the initial dialog.
            if (ME1Directory.gamePath == null && ME2Directory.gamePath == null && ME3Directory.gamePath == null)
            {
                (new InitialSetup()).ShowDialog();
            }
            UpdateGamePathWarningIconStatus();

        }

        private static void SystemParameters_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            EnsureStandardPopupAlignment();
        }

        private static void EnsureStandardPopupAlignment()
        {
            if (SystemParameters.MenuDropAlignment && _menuDropAlignmentField != null)
            {
                _menuDropAlignmentField.SetValue(null, false);
            }
        }

        private void Tools_FavoritesChanged(object sender, EventArgs e)
        {
            IEnumerable<Tool> favs = Tools.Items.Where(x => x.IsFavorited);
            favoritesPanel.setToolList(favs);
            favoritesWatermark.Visibility = favs.Any() ? Visibility.Hidden : Visibility.Visible;
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
            (new About()).Show();
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
                if (TaskPaneOpen)
                {
                    closeTaskPane(100);
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
                closeSearch(duration / 3);
            }
            if (AdvancedOpen)
            {
                closeAdvancedSettings(duration / 3);
            }
            if (ToolInfoPanelOpen)
            {
                closeToolInfo();
            }
            if (PathsPanelOpen)
            {
                closeGamePaths();
            }
            CICPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!SearchOpen)
            {
                if (AdvancedOpen)
                {
                    closeAdvancedSettings();
                }
                if (ToolInfoPanelOpen)
                {
                    closeToolInfo();
                }
                if (PathsPanelOpen)
                {
                    closeGamePaths();
                }
                SearchOpen = true;
                searchPanel.BeginDoubleAnimation(WidthProperty, 300, 200);
            }

            List<Tool> results = new List<Tool>();
            string[] words = SearchBox.Text.ToLower().Split(' ');
            foreach (Tool tool in Tools.Items)
            {
                if (tool.open != null)
                {
                    //for each word we've typed in
                    foreach (string word in words)
                    {
                        if (tool.tags.FuzzyMatch(word) || tool.name.ToLower().Contains(word) || tool.name.ToLower().Split(' ').FuzzyMatch(word))
                        {
                            results.Add(tool);
                            break;
                        }
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
            List<GenericWindow> tools = new List<GenericWindow>();
            foreach (var package in MEPackageHandler.packagesInTools)
            {
                foreach (var tool in package.Tools)
                {
                    tools.Add(tool);
                }
            }
            foreach (var tool in tools)
            {
                tool.Close();
            }
            if (MEPackageHandler.packagesInTools.Count > 0)
            {
                e.Cancel = true;
            }
            Properties.Settings.Default.AlwaysOnTop = alwaysOnTopCheckBox.IsChecked ?? false;
        }

        private void advancedSettings_Click(object sender, RoutedEventArgs e)
        {
            if (AdvancedOpen)
            {
                closeAdvancedSettings(200);
            }
            else
            {
                if (SearchOpen)
                {
                    closeSearch();
                }
                if (ToolInfoPanelOpen)
                {
                    closeToolInfo();
                }
                if (PathsPanelOpen)
                {
                    closeGamePaths();
                }
                AdvancedOpen = true;
                advancedPanel.BeginDoubleAnimation(WidthProperty, 300, 200);
            }
        }

        private void closeSearch(int duration = 100)
        {
            SearchOpen = false;
            searchPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
        }

        private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!SearchOpen && SearchBox.Text.Trim() != string.Empty)
            {
                SearchOpen = true;
                searchPanel.BeginDoubleAnimation(WidthProperty, 300, 200);
                if (AdvancedOpen)
                {
                    closeAdvancedSettings();
                }
                if (ToolInfoPanelOpen)
                {
                    closeToolInfo();
                }
                if (PathsPanelOpen)
                {
                    closeGamePaths();
                }
            }
        }

        private void closeAdvancedSettings(int duration = 100)
        {
            AdvancedOpen = false;
            advancedPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
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
                if (TaskPaneOpen)
                {
                    closeTaskPane(100);
                }
                utilitiesButton.OpacityMask = HighlightBrush;
                utilitiesPanel.BeginDoubleAnimation(WidthProperty, 650, 300);
            }
        }

        private void closeUtilities(int duration = 300)
        {
            if (ToolInfoPanelOpen)
            {
                closeToolInfo();
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
                if (TaskPaneOpen)
                {
                    closeTaskPane(100);
                }
                createModsButton.OpacityMask = HighlightBrush;
                createModsPanel.BeginDoubleAnimation(WidthProperty, 650, 300);
            }
        }

        private void closeCreateMods(int duration = 300)
        {
            if (ToolInfoPanelOpen)
            {
                closeToolInfo(50);
            }
            CreateModsOpen = false;
            createModsPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
            createModsButton.OpacityMask = LabelTextBrush;
        }

        private void closeToolInfo(int duration = 50)
        {
            ToolInfoPanelOpen = false;
            toolInfoPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
        }

        private void ToolMouseOver(object sender, Tool t)
        {
            if (SearchBox.IsFocused)
            {
                SearchBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
            openToolInfo(t);
        }

        private void openToolInfo(Tool e)
        {
            toolInfoPanel.setTool(e);
            if (!ToolInfoPanelOpen)
            {
                if (SearchOpen)
                {
                    closeSearch();
                }
                if (AdvancedOpen)
                {
                    closeAdvancedSettings();
                }
                if (PathsPanelOpen)
                {
                    closeGamePaths();
                }
                ToolInfoPanelOpen = true;
                toolInfoPanel.BeginDoubleAnimation(WidthProperty, 300, 50);
            }
        }

        private void closeGamePaths(int duration = 50)
        {
            PathsPanelOpen = false;
            pathsPanel.BeginDoubleAnimation(WidthProperty, 0, duration);

            string me1Path = null;
            string me2Path = null;
            string me3Path = null;
            if (File.Exists(Path.Combine(me1PathBox.Text, "Binaries", "MassEffect.exe")))
            {
                me1Path = me1PathBox.Text;
            }
            if (File.Exists(Path.Combine(me2PathBox.Text, "Binaries", "MassEffect2.exe")))
            {
                me2Path = me2PathBox.Text;
            }
            if (File.Exists(Path.Combine(me3PathBox.Text, "Binaries", "Win32", "MassEffect3.exe")))
            {
                me3Path = me3PathBox.Text;
            }
            List<string> directories = new List<string> { me1Path, me2Path, me3Path };
            MEDirectories.SaveSettings(directories);

            gamePathsWarningIcon.Visibility = directories.All(item => item == null || !Directory.Exists(item)) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void pathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            string game;
            if (b == me1PathBrowseButton)
            {
                game = "Mass Effect";
            }
            else if (b == me2PathBrowseButton)
            {
                game = "Mass Effect 2";
            }
            else if (b == me3PathBrowseButton)
            {
                game = "Mass Effect 3";
            }
            else
            {
                return;
            }
            if (game != "")
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = $"Select {game} executable.";
                game = game.Replace(" ", "");
                ofd.Filter = $"{game}.exe|{game}.exe";

                if (ofd.ShowDialog() == true)
                {
                    string result = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                    switch (game)
                    {
                        case "MassEffect":
                            me1PathBox.Text = ME1Directory.gamePath = result;
                            me1PathBox.Visibility = Visibility.Visible;
                            break;
                        case "MassEffect2":
                            me2PathBox.Text = ME2Directory.gamePath = result;
                            me2PathBox.Visibility = Visibility.Visible;
                            break;
                        case "MassEffect3":
                            me3PathBox.Text = ME3Directory.gamePath = Path.GetDirectoryName(result);
                            me3PathBox.Visibility = Visibility.Visible;
                            break;
                    }
                    UpdateGamePathWarningIconStatus();
                }
            }
        }

        private void UpdateGamePathWarningIconStatus()
        {
            var warningIcons = new List<ImageAwesome> { me1GamePathWarningIcon, me2GamePathWarningIcon, me3GamePathWarningIcon };
            var directories = new List<string> { ME1Directory.gamePath, ME2Directory.gamePath, ME3Directory.gamePath };
            gamePathsWarningIcon.Visibility = directories.Any(item => item != null && (!Directory.Exists(item) || !Directory.Exists(Path.Combine(item, "BIOGame")) || !Directory.Exists(Path.Combine(item, "Binaries")))) ? Visibility.Visible : Visibility.Collapsed;
            for (int i = 0; i < warningIcons.Count; i++)
            {
                ImageAwesome icon = warningIcons[i];
                string directory = directories[i];
                icon.Visibility = directory != null && (!Directory.Exists(directory) || !Directory.Exists(Path.Combine(directory, "BIOGame")) || !Directory.Exists(Path.Combine(directory, "Binaries"))) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void gamePaths_Click(object sender, RoutedEventArgs e)
        {
            if (PathsPanelOpen)
            {
                closeGamePaths();
            }
            else
            {
                if (ME1Directory.gamePath != null)
                {
                    me1PathBox.Text = ME1Directory.gamePath;
                }
                else
                {
                    me1PathBox.Visibility = Visibility.Collapsed;
                }
                if (ME2Directory.gamePath != null)
                {
                    me2PathBox.Text = ME2Directory.gamePath;
                }
                else
                {
                    me2PathBox.Visibility = Visibility.Collapsed;
                }
                if (ME3Directory.gamePath != null)
                {
                    me3PathBox.Text = ME3Directory.gamePath;
                }
                else
                {
                    me3PathBox.Visibility = Visibility.Collapsed;
                }
                if (SearchOpen)
                {
                    closeSearch();
                }
                if (AdvancedOpen)
                {
                    closeAdvancedSettings();
                }
                if (ToolInfoPanelOpen)
                {
                    closeToolInfo();
                }
                PathsPanelOpen = true;
                pathsPanel.BeginDoubleAnimation(WidthProperty, 300, 50);
            }
        }

        private void taskPaneButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskPaneOpen)
            {
                closeTaskPane();
            }
            else
            {
                TaskPaneOpen = true;
                if (CICOpen)
                {
                    closeCIC(100);
                }
                if (UtilitiesOpen)
                {
                    closeUtilities(100);
                }
                if (CreateModsOpen)
                {
                    closeCreateMods(100);
                }
                taskPaneButton.OpacityMask = HighlightBrush;
                taskPanePanel.BeginDoubleAnimation(WidthProperty, 650, 300);
            }
        }

        private void closeTaskPane(int duration = 300)
        {
            if (TaskPaneInfoPanelOpen)
            {
                closeTaskPaneInfoPanel();
            }
            TaskPaneOpen = false;
            taskPanePanel.BeginDoubleAnimation(WidthProperty, 0, duration);
            taskPaneButton.OpacityMask = LabelTextBrush;
        }

        private void closeTaskPaneInfoPanel(int duration = 100)
        {
            TaskPaneInfoPanelOpen = false;
            taskPaneInfoPanel.BeginDoubleAnimation(WidthProperty, 0, duration);
        }

        private void taskPanePanel_ToolMouseOver(object sender, GenericWindow e)
        {
            taskPaneInfoPanel.setTool(e);
            if (!TaskPaneInfoPanelOpen)
            {
                TaskPaneInfoPanelOpen = true;
                taskPaneInfoPanel.BeginDoubleAnimation(WidthProperty, 300, 100);
            }
        }

        private void taskPaneInfoPanel_Close(object sender, EventArgs e)
        {
            closeTaskPaneInfoPanel();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PresentationSource source = PresentationSource.FromVisual(this);

            if (source != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }
        }

        private void ME3TweaksDiscord_Clicked(object sender, RoutedEventArgs e)
        {
            string link = "https://discordapp.com/invite/s8HA6dc";
            try
            {
                System.Diagnostics.Process.Start(link);
            }
            catch (Exception)
            {
                try
                {
                    System.Windows.Clipboard.SetText(link);
                }
                catch
                {
                    //RIP
                }
            }
        }
    }
}
