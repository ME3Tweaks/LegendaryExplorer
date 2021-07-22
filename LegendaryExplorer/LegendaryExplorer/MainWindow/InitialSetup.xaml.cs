using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Shell.Interop;

namespace LegendaryExplorer.MainWindow
{
    /// <summary>
    /// Interaction logic for InitialSetup.xaml
    /// </summary>
    public partial class InitialSetup : Window
    {
        private bool me1Okay = false;
        private bool me2Okay = false;
        private bool me3Okay = false;
        private bool leOkay = false;

        public InitialSetup()
        {
            InitializeComponent();
            me1PathBox.Text = ME1Directory.DefaultGamePath;
            me2PathBox.Text = ME2Directory.DefaultGamePath;
            me3PathBox.Text = ME3Directory.DefaultGamePath;

            LEDirectory.LookupDefaultPath();
            melePathBox.Text = LegendaryExplorerCoreLibSettings.Instance.LEDirectory;
        }

        private void me1PathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            me1Okay = ME1Directory.IsValidGameDir(me1PathBox.Text);
            if (me1Okay)
            {
                me1PathBox.BorderBrush = Brushes.Lime;
            }
            else
            {
                me1PathBox.BorderBrush = Brushes.Red;
            }
        }

        private void me2PathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            me2Okay = ME2Directory.IsValidGameDir(me2PathBox.Text);
            if (me2Okay)
            {
                me2PathBox.BorderBrush = Brushes.Lime;
            }
            else
            {
                me2PathBox.BorderBrush = Brushes.Red;
            }
        }

        private void me3PathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            me3Okay = ME3Directory.IsValidGameDir(me3PathBox.Text);
            if (me3Okay)
            {
                me3PathBox.BorderBrush = Brushes.Lime;
            }
            else
            {
                me3PathBox.BorderBrush = Brushes.Red;
            }
        }

        private void melePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            leOkay = LEDirectory.IsValidGameDir(melePathBox.Text);
            if (leOkay)
            {
                melePathBox.BorderBrush = Brushes.Lime;
            }
            else
            {
                melePathBox.BorderBrush = Brushes.Red;
            }
        }

        private void ChangeGamePath(MEGame game)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = $"Select {game} executable";
            var exe = MEDirectories.ExecutableNames(game)[0];
            string filter = $"{exe}|{exe}";
            ofd.Filter = filter;
            if (ofd.ShowDialog() == true)
            {
                string result = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                if (game >= MEGame.ME3)
                    result = Path.GetDirectoryName(result); //up one more because of win32 directory.
                switch (game)
                {
                    case MEGame.ME1:
                        me1PathBox.Text = result;
                        break;
                    case MEGame.ME2:
                        me2PathBox.Text = result;
                        break;
                    case MEGame.ME3:
                        me3PathBox.Text = result;
                        break;
                    case MEGame.LELauncher:
                        melePathBox.Text = result;
                        break;
                }
            }
        }

        private void changePathsButton_Click(object sender, RoutedEventArgs e)
        {
            string game = InputComboBoxWPF.GetValue(this, "Which game's path do you want to change?", "Change game paths",
                new[] { "Mass Effect", "Mass Effect 2", "Mass Effect 3" }, "Mass Effect 3");
            if (game != "")
            {
                List<string> allowedVersions = new List<string>();
                switch (game)
                {
                    case "Mass Effect":
                        allowedVersions.Add("1.2.20608.0");
                        break;
                    case "Mass Effect 2":
                        allowedVersions.Add("1.2.1604.0"); // Steam
                        allowedVersions.Add("01604.00"); // Origin
                        break;
                    case "Mass Effect 3":
                        allowedVersions.Add("05427.124");
                        break;
                }

                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = $"Select {game} executable.";
                game = game.Replace(" ", "");
                string filter = $"{game}.exe|{game}.exe";
                ofd.Filter = filter;

                if (ofd.ShowDialog() == true)
                {
                    var fvi = FileVersionInfo.GetVersionInfo(ofd.FileName);
                    if (!allowedVersions.Contains(fvi.FileVersion))
                    {
                        MessageBox.Show($"Cannot use this executable: The only supported file versions are:\n{string.Join("\n", allowedVersions)}\n\nThe selected one has version: {fvi.FileVersion}\n\nNote: ME3Explorer does not support Mass Effect Legendary Edition games.",
                            "Invalid game", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string result = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                    if (game.Last() == '3')
                        result = Path.GetDirectoryName(result);
                    switch (game)
                    {
                        case "MassEffect":
                            me1PathBox.Text = result;
                            break;
                        case "MassEffect2":
                            me2PathBox.Text = result;
                            break;
                        case "MassEffect3":
                            me3PathBox.Text = result;
                            break;
                    }
                }
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            if ((me1Okay || me2Okay || me3Okay || leOkay) == false)
            {
                MessageBox.Show("Must have at least one game path correctly set to save settings.");
                return;
            }

            Settings.Global_ME1Directory = me1Okay ? me1PathBox.Text : "";
            Settings.Global_ME2Directory = me2Okay ? me2PathBox.Text : "";
            Settings.Global_ME3Directory = me3Okay ? me3PathBox.Text : "";
            Settings.Global_LEDirectory = leOkay ? melePathBox.Text : "";

            Settings.MainWindow_CompletedInitialSetup = true;
            MEDirectories.SaveSettings(new List<string> { Settings.Global_ME1Directory, Settings.Global_ME2Directory, Settings.Global_ME3Directory, Settings.Global_LEDirectory });
            Close();
        }

        private void ChangeME1GamePath_Click(object sender, RoutedEventArgs e)
        {
            ChangeGamePath(MEGame.ME1);
        }

        private void ChangeME2GamePath_Click(object sender, RoutedEventArgs e)
        {
            ChangeGamePath(MEGame.ME2);
        }

        private void ChangeME3GamePath_Click(object sender, RoutedEventArgs e)
        {
            ChangeGamePath(MEGame.ME3);
        }

        private void ChangeMELEGamePath_Click(object sender, RoutedEventArgs e)
        {
            ChangeGamePath(MEGame.LELauncher);
        }
    }
}
