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
using KFreonLib.MEDirectories;
using ME3Explorer.Unreal;
using Microsoft.Win32;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for InitialSetup.xaml
    /// </summary>
    public partial class InitialSetup : Window
    {
        Dictionary<string, string> prettyDLCNames = new Dictionary<string, string>()
        {
            ["DLC_CON_APP01"] = "Alternate Appearance Pack 1",
            ["DLC_EXP_Pack003"] = "Citadel",
            ["DLC_EXP_Pack003_Base"] = "Citadel Base",
            ["DLC_OnlinePassHidCE"] = "Collector's Edition Bonus Content",
            ["DLC_CON_END"] = "Extended Cut",
            ["DLC_HEN_PR"] = "From Ashes",
            ["DLC_CON_GUN01"] = "Firefight Pack",
            ["DLC_CON_GUN02"] = "Groundside Resistance Pack",
            ["DLC_EXP_Pack001"] = "Leviathan",
            ["DLC_CON_DH1"] = "Mass Effect: Genesis 2",
            ["DLC_EXP_Pack002"] = "Omega",
            ["DLC_CON_MP3"] = "Earth",
            ["DLC_CON_MP2"] = "Rebellion",
            ["DLC_CON_MP5"] = "Reckoning",
            ["DLC_CON_MP1"] = "Resurgence",
            ["DLC_CON_MP4"] = "Retaliation",
            ["DLC_UPD_Patch01"] = "Patch01",
            ["DLC_UPD_Patch02"] = "Patch02"
        };

        List<DLCPackage> sfarsToUnpack = new List<DLCPackage>();
        double totalUncompressedSize;

        bool step1Complete;
        bool step2Complete;

        public InitialSetup()
        {
            InitializeComponent();
            me1PathBox.Text = ME1Directory.gamePath;
            me2PathBox.Text = ME2Directory.gamePath;
            me3PathBox.Text = ME3Directory.gamePath;
        }

        private void prepDLCUnpacking()
        {
            if (ME3Directory.gamePath != null)
            {
                var parts = ME3Directory.gamePath.Split(':');
                DriveInfo info = new DriveInfo(parts[0]);
                double availableSpace = info.AvailableFreeSpace;
                availableSpaceRun.Text = UsefulThings.General.GetFileSizeAsString(availableSpace);
                double requiredSpace = GetRequiredSize();
                if (requiredSpace >= 0)
                {
                    requiredSpaceRun.Text = UsefulThings.General.GetFileSizeAsString(requiredSpace);
                }
                else
                {
                    requiredSpaceRun.Text = "?";
                }
                unpackOutput.AppendLine($"Unpacked DLC detected: {sfarsToUnpack.Count}");
                if (sfarsToUnpack.Count > 0)
                {
                    if (availableSpace < requiredSpace)
                    {
                        requiredSpaceRun.Foreground = new SolidColorBrush(Color.FromRgb(0xb8, 0, 0));
                        unpackDLCButton.Content = "Unpack Failed!";
                        failSetup();
                        step2Mask.Visibility = Visibility.Visible;
                        step2TextBlock.Inlines.Add("--FAILED");
                    }
                }
                else
                {
                    completeStep2();
                }
                unpackDLCButton.IsEnabled = true; 
            }
            else
            {
                completeStep2();
            }
        }

        private void completeStep2()
        {
            step2Mask.Visibility = Visibility.Visible;
            step2TextBlock.Inlines.Add("--COMPLETE");
            unpackOutput.AppendLine("DLC Unpacking Complete");
            unpackDLCButton.Content = "Unpacked";
            timeRemainingTextBlock.Text = "Complete";
            step2Complete = true;
            checkIfFinished();
        }

        private void failSetup()
        {
            doneButton.Background = new SolidColorBrush(Color.FromRgb(0xb8, 0, 0));
            doneButton.Content = "Setup failed! Exit by clicking the \"x\" in the upper right.";
            doneButton.IsEnabled = true;
        }

        private double GetRequiredSize()
        {
            var folders = Directory.EnumerateDirectories(ME3Directory.DLCPath);
            var extracted = folders.Where(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any(file => file.EndsWith("pcconsoletoc.bin", StringComparison.OrdinalIgnoreCase)));
            var unextracted = folders.Except(extracted);

            double compressedSize = 0;
            double uncompressedSize = 0;
            double largestUncompressedSize = 0;
            double temp;
            foreach (var folder in unextracted)
            {
                if (folder.Contains("__metadata"))
                    continue;

                try
                {
                    FileInfo info = new FileInfo(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(file => file.EndsWith(".sfar", StringComparison.OrdinalIgnoreCase)).First());
                    compressedSize += info.Length;

                    DLCPackage sfar = new DLCPackage(info.FullName);
                    sfarsToUnpack.Add(sfar);

                    temp = sfar.UncompressedSize;
                    uncompressedSize += temp;
                    if (temp > largestUncompressedSize)
                    {
                        largestUncompressedSize = temp;
                    }
                }
                catch (Exception)
                {
                    return -1;
                }
            }
            totalUncompressedSize = uncompressedSize;
            //each SFAR is stripped of all its files after unpacking, so the maximum space needed on the drive is 
            //the difference between the uncompressed size and compressed size of all SFARS, plus the compressed size
            //of the largest SFAR. I'm using the uncompressed size instead as a fudge factor.
            return (uncompressedSize - compressedSize) + largestUncompressedSize;
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
        }

        private void checkIfFinished()
        {
            if (step1Complete && step2Complete)
            {
                doneButton.IsEnabled = true;
                doneButton.Content = "Done! Click here to exit Setup and launch ME3Explorer.";
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!step1Complete || !step2Complete)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Setup is incomplete. Game paths may be incorrect and you will not be able to use the toolset to mod or browse textures for ME3. Cancel setup?",
                    "", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private async void unpackDLCButton_Click(object sender, RoutedEventArgs e)
        {
            unpackDLCButton.IsEnabled = false;
            unpackDLCButton.Content = "Unpacking...";
            string[] patt = { "pcc", "bik", "tfc", "afc", "cnd", "tlk", "bin", "dlc" };
            string gamebase = ME3Directory.gamePath;
            unpackProgress.Maximum = totalUncompressedSize;
            timeRemainingTextBlock.Text = "Estimating Time Remaining...";

            unpackOutput.AppendLine("DLC unpacking initiated...");
            double uncompressedSoFar = 0;
            DateTime initial = DateTime.Now;
            foreach (var sfar in sfarsToUnpack)
            {
                string dlcName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(sfar.FileName)));
                if (prettyDLCNames.ContainsKey(dlcName))
                {
                    dlcName = prettyDLCNames[dlcName];
                }
                unpackOutput.AppendLine($"Unpacking {dlcName}...");
                if (sfar.Files.Length > 1)
                {
                    List<int> Indexes = new List<int>();
                    for (int i = 0; i < sfar.Files.Length; i++)
                    {
                        string DLCpath = sfar.Files[i].FileName;
                        for (int j = 0; j < patt.Length; j++)
                        {
                            if (DLCpath.EndsWith(patt[j], StringComparison.OrdinalIgnoreCase))
                            {
                                string outpath = gamebase + DLCpath.Replace("/", "\\");
                                if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                    Directory.CreateDirectory(Path.GetDirectoryName(outpath));

                                if (!File.Exists(outpath))
                                {
                                    using (FileStream fs = new FileStream(outpath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                                    {
                                        await sfar.DecompressEntryAsync(i, fs);
                                    }
                                }
                                Indexes.Add(i);

                                uncompressedSoFar += sfar.Files[i].RealUncompressedSize;
                                unpackProgress.Value = uncompressedSoFar;
                                TimeSpan elapsed = DateTime.Now - initial;
                                double PercentComplete = uncompressedSoFar / totalUncompressedSize * 100;
                                TimeSpan timeRemaining = TimeSpan.FromSeconds((elapsed.TotalSeconds / PercentComplete) * (100 - PercentComplete));
                                if (timeRemaining.TotalMinutes < 1)
                                {
                                    timeRemainingTextBlock.Text = $"Estimated Time Remaining: < 1 minute";
                                }
                                else
                                {
                                    timeRemainingTextBlock.Text = string.Format("Estimated Time Remaining: {0:%h}h {0:%m}m", timeRemaining); 
                                }
                                break;
                            }
                        }
                    }
                    sfar.DeleteEntries(Indexes);
                }
                unpackOutput.AppendLine($"{dlcName} unpacked");
            }
            unpackOutput.AppendLine("Generating TOCs...");
            await Task.Run(() =>
            {
                AutoTOC.GenerateAllTOCs();
            });
            unpackOutput.AppendLine("ALL TOCs Generated!");
            unpackProgress.Value = unpackProgress.Maximum;
            completeStep2();

            //temp
            this.RestoreAndBringToFront();
            MessageBox.Show((DateTime.Now - initial).ToString());
        }
    }
}
