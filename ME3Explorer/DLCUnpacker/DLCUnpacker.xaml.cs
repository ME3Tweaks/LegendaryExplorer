using ByteSizeLib;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace ME3Explorer.DLCUnpacker
{
    /// <summary>
    /// Interaction logic for DLCUnpacker.xaml
    /// </summary>
    public partial class DLCUnpacker : WPFBase, INotifyPropertyChanged
    {

        public string UnpackingPercentString { get; set; }
        public string RequiredSpaceText { get; set; }
        public string AvailableSpaceText { get; set; }

        List<DLCPackage> sfarsToUnpack = new List<DLCPackage>();

        public DLCUnpacker()
        {
            InitializeComponent();
            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += CalculateUnpackRequirements;
            bg.RunWorkerCompleted += CalculateUnpackRequirements_Completed;
            bg.RunWorkerAsync();

            //prepDLCUnpacking();
        }

        private void CalculateUnpackRequirements_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void CalculateUnpackRequirements(object sender, DoWorkEventArgs e)
        {
            if (ME3Directory.gamePath != null)
            {
                var parts = ME3Directory.gamePath.Split(':');
                DriveInfo info = new DriveInfo(parts[0]);
                double availableSpace = info.AvailableFreeSpace;
                AvailableSpaceText = ByteSize.FromBytes(availableSpace).ToString();

                double requiredSpace = GetRequiredSize();
                if (requiredSpace >= 0)
                {
                    RequiredSpaceText = ByteSize.FromBytes(requiredSpace).ToString();
                }
                else
                {
                    //requiredSpaceRun.Text = "?";
                }

                /*unpackOutput.AppendLine($"Packed DLC detected: {sfarsToUnpack.Count}");*/
                if (sfarsToUnpack.Count > 0)
                {
                    if (availableSpace < requiredSpace)
                    {
                        //requiredSpaceRun.Foreground = new SolidColorBrush(Color.FromRgb(0xb8, 0, 0));

                    }
                }
                else
                {
                }
            }
        }
        private void prepDLCUnpacking()
        {
            
        }

        private double GetRequiredSize()
        {
            double totalUncompressedSize;

            var folders = Directory.EnumerateDirectories(ME3Directory.DLCPath);
            var extracted = folders.Where(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any(file => file.EndsWith("mount.dlc", StringComparison.OrdinalIgnoreCase)));
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
                    FileInfo info = new FileInfo(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).First(file => file.EndsWith(".sfar", StringComparison.OrdinalIgnoreCase)));
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

        private void UnpackDLCButton_Click(object sender, RoutedEventArgs e)
        {

        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //This tool does not handle updates.
        }
    }
}
