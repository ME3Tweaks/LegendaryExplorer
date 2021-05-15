using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.GameFilesystem;
using Microsoft.Win32;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for InitialSetup.xaml
    /// </summary>
    public partial class InitialSetup : Window
    {
        public InitialSetup()
        {
            InitializeComponent();
            me1PathBox.Text = ME1Directory.DefaultGamePath;
            me2PathBox.Text = ME2Directory.DefaultGamePath;
            me3PathBox.Text = ME3Directory.DefaultGamePath;
        }

        private void completeStep2()
        {
            /*step2Mask.Visibility = Visibility.Visible;
            step2TextBlock.Inlines.Add("--COMPLETE");
            unpackOutput.AppendLine("DLC Unpacking Complete");
            unpackDLCButton.Content = "Unpacked";
            timeRemainingTextBlock.Text = "Complete";
            step2Complete = true;
            checkIfFinished();*/
        }

        private void failSetup()
        {
            doneButton.Background = new SolidColorBrush(Color.FromRgb(0xb8, 0, 0));
            doneButton.Content = "Setup failed! Exit by clicking the \"x\" in the upper right.";
            doneButton.IsEnabled = true;
        }

        private void me1PathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (File.Exists(Path.Combine(me1PathBox.Text, "Binaries", "MassEffect.exe")))
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
            if (File.Exists(Path.Combine(me2PathBox.Text, "Binaries", "MassEffect2.exe")))
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
            if (File.Exists(Path.Combine(me3PathBox.Text, "Binaries", "Win32", "MassEffect3.exe")))
            {
                me3PathBox.BorderBrush = Brushes.Lime;
            }
            else
            {
                me3PathBox.BorderBrush = Brushes.Red;
            }
        }

        private void ChangeGamePath(int gameNum)
        {
            string game = "Mass Effect" + (gameNum > 1 ? " " + gameNum : "");
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = $"Select {game} executable";
            game = game.Replace(" ", "");
            string filter = $"{game}.exe|{game}.exe";
            ofd.Filter = filter;
            if (ofd.ShowDialog() == true)
            {
                string result = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                if (gameNum == 3)
                    result = Path.GetDirectoryName(result); //up one more because of win32 directory.
                switch (gameNum)
                {
                    case 1:
                        me1PathBox.Text = result;
                        break;
                    case 2:
                        me2PathBox.Text = result;
                        break;
                    case 3:
                        me3PathBox.Text = result;
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

        /*
        private void pathsOKButton_Click(object sender, RoutedEventArgs e)
        {
            string me1Path = null;
            string me2Path = null;
            string me3Path = null;
            if (File.Exists(Path.Combine(me1PathBox.Text, "Binaries", "MassEffect.exe")))
            {
                me1Path = me1PathBox.Text;
            }
            else
            {
                me1PathBox.Text = "Game Not Found";
            }
            if (File.Exists(Path.Combine(me2PathBox.Text, "Binaries", "MassEffect2.exe")))
            {
                me2Path = me2PathBox.Text;
            }
            else
            {
                me2PathBox.Text = "Game Not Found";
            }
            if (File.Exists(Path.Combine(me3PathBox.Text, "Binaries", "Win32", "MassEffect3.exe")))
            {
                me3Path = me3PathBox.Text;
            }
            else
            {
                me3PathBox.Text = "Game Not Found";
            }
            MEDirectories.SaveSettings(new List<string> { me1Path, me2Path, me3Path});
            step1Mask.Visibility = Visibility.Visible;
            step1TextBlock.Inlines.Add("--COMPLETE");
            step1Complete = true;
            prepDLCUnpacking();
        }*/

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            ME1Directory.DefaultGamePath = me1PathBox.Text;
            ME2Directory.DefaultGamePath = me2PathBox.Text;
            ME3Directory.DefaultGamePath = me3PathBox.Text;
            MEDirectories.SaveSettings(new List<string> { me1PathBox.Text, me2PathBox.Text, me3PathBox.Text });
            this.Close();
        }

        private void ChangeME1GamePath_Click(object sender, RoutedEventArgs e)
        {
            ChangeGamePath(1);
        }

        private void ChangeME2GamePath_Click(object sender, RoutedEventArgs e)
        {
            ChangeGamePath(2);
        }

        private void ChangeME3GamePath_Click(object sender, RoutedEventArgs e)
        {
            ChangeGamePath(3);
        }
    }
}
