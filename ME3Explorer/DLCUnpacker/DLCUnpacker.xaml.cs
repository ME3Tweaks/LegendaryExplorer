using ByteSizeLib;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
    public partial class DLCUnpacker : WPFBase
    {
        public ICommand UnpackDLCCommand { get; set; }
        private BackgroundWorker UnpackDLCWorker;
        private double RequiredSpace;
        private double AvailableSpace;

        #region MVVM Databindings
        private string _unpackingPercentString;
        public string UnpackingPercentString
        {
            get { return _unpackingPercentString; }
            set
            {
                if (value != _unpackingPercentString)
                {
                    _unpackingPercentString = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _requiredSpaceText;
        public string RequiredSpaceText
        {
            get { return _requiredSpaceText; }
            set
            {
                if (value != _requiredSpaceText)
                {
                    _requiredSpaceText = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _availableSpaceText;
        public string AvailableSpaceText
        {
            get { return _availableSpaceText; }
            set
            {
                if (value != _availableSpaceText)
                {
                    _availableSpaceText = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _currentOverallProgressValue;
        public double CurrentOverallProgressValue
        {
            get { return _currentOverallProgressValue; }
            set
            {
                if (value != _currentOverallProgressValue)
                {
                    _currentOverallProgressValue = value;
                    OnPropertyChanged();
                }
            }
        }

        //Used for the current operation (e.g. which DLC is being unpacked, it's %)
        private int _currentOperationProgressValue;
        public int CurrentOperationPercentValue
        {
            get { return _currentOperationProgressValue; }
            set
            {
                if (value != _currentOperationProgressValue)
                {
                    _currentOperationProgressValue = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _progressBarIndeterminate;
        public bool ProgressBarIndeterminate
        {
            get { return _progressBarIndeterminate; }
            set
            {
                if (value != _progressBarIndeterminate)
                {
                    _progressBarIndeterminate = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _currentOperationText;
        public string CurrentOperationText
        {
            get { return _currentOperationText; }
            set
            {
                if (value != _currentOperationText)
                {
                    _currentOperationText = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion
        List<DLCPackage> sfarsToUnpack = new List<DLCPackage>();

        public DLCUnpacker()
        {
            LoadCommands();
            RequiredSpaceText = "Calculating...";
            InitializeComponent();
            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += CalculateUnpackRequirements;
            bg.RunWorkerCompleted += CalculateUnpackRequirements_Completed;
            bg.RunWorkerAsync();

            //prepDLCUnpacking();
        }

        private void CalculateUnpackRequirements_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested(); //Refresh commands
        }

        private void CalculateUnpackRequirements(object sender, DoWorkEventArgs e)
        {
            //Background thread
            CalculateUnpackRequirements();
        }

        /// <summary>
        /// Calculates the space required and available space for unpacking.
        /// If there is nothing to unpack (0 bytes to unpack) the unpack DLC button will become locked.
        /// </summary>
        private void CalculateUnpackRequirements()
        {
            if (ME3Directory.gamePath != null)
            {
                ProgressBarIndeterminate = true;
                var parts = ME3Directory.gamePath.Split(':');
                DriveInfo info = new DriveInfo(parts[0]);
                AvailableSpace = info.AvailableFreeSpace;
                AvailableSpaceText = ByteSize.FromBytes(AvailableSpace).ToString();
                Debug.WriteLine("Available space set");
                RequiredSpace = GetRequiredSize();
                if (RequiredSpace >= 0)
                {
                    RequiredSpaceText = ByteSize.FromBytes(RequiredSpace).ToString();
                }
                else
                {
                    RequiredSpaceText = "Error calculating";
                }
                ProgressBarIndeterminate = false;
            }
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
                if (!System.IO.Path.GetFileName(folder).StartsWith("DLC"))
                    continue;

                try
                {
                    FileInfo info = new FileInfo(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).First(file => file.EndsWith(".sfar", StringComparison.OrdinalIgnoreCase)));
                    compressedSize += info.Length;

                    //This can be changed to MEM code
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

            //This can probably be changed (original code comment is as follows)

            //each SFAR is stripped of all its files after unpacking, so the maximum space needed on the drive is 
            //the difference between the uncompressed size and compressed size of all SFARS, plus the compressed size
            //of the largest SFAR. I'm using the uncompressed size instead as a fudge factor.
            return (uncompressedSize - compressedSize) + largestUncompressedSize;
        }

        private void LoadCommands()
        {
            // Player commands
            UnpackDLCCommand = new RelayCommand(UnpackDLC, CanUnpackDLC);
        }

        private bool CanUnpackDLC(object obj)
        {
            return AvailableSpace > RequiredSpace && RequiredSpace > 0 && UnpackDLCWorker == null;
        }

        private void UnpackDLC(object obj)
        {
            UnpackDLCWorker = new BackgroundWorker();
            UnpackDLCWorker.DoWork += UnpackAllDLC;
            UnpackDLCWorker.RunWorkerCompleted += UnpackAllDLC_Completed;
            UnpackDLCWorker.RunWorkerAsync();
        }

        private void UnpackAllDLC_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            //Unpack DLC and update binded values as appropriate, if any.
            CommandManager.InvalidateRequerySuggested(); //Refresh commands
            UnpackDLCWorker = null;
        }

        private void UnpackAllDLC(object sender, DoWorkEventArgs e)
        {
            //Post completion stuff here

            // ... 
            CalculateUnpackRequirements(); //Will lock the unpack button and update UI for user
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //This tool does not handle updates.
        }
    }
}
