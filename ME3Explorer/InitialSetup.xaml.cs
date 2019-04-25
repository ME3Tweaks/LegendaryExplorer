using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Shell;
using KFreonLib.MEDirectories;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
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
            me1PathBox.Text = ME1Directory.gamePath;
            me2PathBox.Text = ME2Directory.gamePath;
            me3PathBox.Text = ME3Directory.gamePath;
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
            string game = InputComboBox.GetValue("Which game's path do you want to change?",
                new string[] { "Mass Effect", "Mass Effect 2", "Mass Effect 3" }, "Mass Effect 3");
            if (game != "")
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = $"Select {game} executable.";
                game = game.Replace(" ", "");
                string filter = $"{game}.exe|{game}.exe";
                ofd.Filter = filter;

                if (ofd.ShowDialog() == true)
                {
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
            ME1Directory.gamePath = me1PathBox.Text;
            ME2Directory.gamePath = me2PathBox.Text;
            ME3Directory.gamePath = me3PathBox.Text;
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
