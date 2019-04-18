/*
    Copyright (C) 2018 Pawel Kolodziejski
    Copyright (C) 2018 Mgamerz

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with program.  If not, see<https://www.gnu.org/licenses/>.
*/

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
using System.Windows.Input;
using System.Windows.Threading;
using ME3Explorer.SharedUI;

namespace ME3Explorer.DLCUnpacker
{
    /// <summary>
    /// Interaction logic for DLCUnpacker.xaml
    /// </summary>
    public partial class DLCUnpacker : WPFBase
    {
        public ICommand UnpackDLCCommand { get; set; }
        public ICommand CancelUnpackCommand { get; set; }
        private BackgroundWorker UnpackDLCWorker;
        private double RequiredSpace;
        private double AvailableSpace;
        public string ME3DLCPath { get; set; }

        /// <summary>
        /// Allow cancel DLCs and revert to state before unpack
        /// </summary>
        public bool UnpackCanceled;

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
        private static Dictionary<string, string> _cachedPrettyDLCNames;
        public static Dictionary<string, string> PrettyDLCNames
        {
            get
            {
                if (_cachedPrettyDLCNames != null) return _cachedPrettyDLCNames;
                _cachedPrettyDLCNames = new Dictionary<string, string>
                {
                    ["DLC_OnlinePassHidCE"] = "Collector's Edition Bonus Content",
                    ["DLC_HEN_PR"] = "From Ashes",
                    ["DLC_CON_END"] = "Extended Cut",
                    ["DLC_EXP_Pack001"] = "Leviathan",
                    ["DLC_EXP_Pack002"] = "Omega",
                    ["DLC_EXP_Pack003"] = "Citadel",
                    ["DLC_EXP_Pack003_Base"] = "Citadel Base",
                    ["DLC_CON_DH1"] = "Mass Effect: Genesis 2",

                    ["DLC_CON_APP01"] = "Alternate Appearance Pack 1",
                    ["DLC_CON_GUN01"] = "Firefight Pack",
                    ["DLC_CON_GUN02"] = "Groundside Resistance Pack",

                    ["DLC_CON_MP1"] = "Resurgence",
                    ["DLC_CON_MP2"] = "Rebellion",
                    ["DLC_CON_MP3"] = "Earth",
                    ["DLC_CON_MP4"] = "Retaliation",
                    ["DLC_CON_MP5"] = "Reckoning",
                    ["DLC_UPD_Patch01"] = "Patch01",
                    ["DLC_UPD_Patch02"] = "Patch02",
                    ["DLC_TestPatch"] = "TestPatch"
                };
                return _cachedPrettyDLCNames;
            }
        }

        private const string NotEnoughSpaceStr = "Not enough free space to unpack DLC.";

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

        private int _overallProgressValue;
        public int OverallProgressValue
        {
            get { return _overallProgressValue; }
            set
            {
                if (value != _overallProgressValue)
                {
                    _overallProgressValue = value;
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

        private string _currentOverallOperationText;
        public string CurrentOverallOperationText
        {
            get { return _currentOverallOperationText; }
            set
            {
                if (value != _currentOverallOperationText)
                {
                    _currentOverallOperationText = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion
        List<DLCUnpack> sfarsToUnpack = new List<DLCUnpack>();
        private DispatcherTimer backgroundticker;

        public DLCUnpacker()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("DLC Unpacker", new WeakReference(this));

            LoadCommands();
            RequiredSpaceText = "Calculating...";
            InitializeComponent();
            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += CalculateUnpackRequirements;
            bg.RunWorkerCompleted += CalculateUnpackRequirements_Completed;
            bg.RunWorkerAsync();

            //Drive space calculations
            if (backgroundticker == null)
            {
                backgroundticker = new DispatcherTimer();
                backgroundticker.Tick += new EventHandler(DriveSpaceUpdater_Tick);
                backgroundticker.Interval = new TimeSpan(0, 0, 3); // execute every 5s
                backgroundticker.Start();
            }
            DriveSpaceUpdater_Tick(null, null);
        }

        private void DriveSpaceUpdater_Tick(object sender, EventArgs e)
        {
            if (UnpackDLCWorker == null || !UnpackDLCWorker.IsBusy)
            {
                var parts = ME3Directory.gamePath.Split(':');
                DriveInfo info = new DriveInfo(parts[0]);
                AvailableSpace = info.AvailableFreeSpace;
                AvailableSpaceText = ByteSize.FromBytes(AvailableSpace).ToString();
                if (AvailableSpace < RequiredSpace)
                {
                    CurrentOverallOperationText = NotEnoughSpaceStr;
                }
                else if (CurrentOverallOperationText == NotEnoughSpaceStr)
                {
                    //clear the message
                    CurrentOverallOperationText = "";
                }
                CommandManager.InvalidateRequerySuggested(); //Refresh commands
            }
        }

        private void CalculateUnpackRequirements_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            UnpackCanceled = false;
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

        public static string GetPrettyDLCNameFromPath(string sfarPath)
        {
            var path = Path.GetFileName(Directory.GetParent(Directory.GetParent(sfarPath).FullName).FullName);
            return PrettyDLCNames[path];
        }

        private double GetRequiredSize()
        {
            double totalUncompressedSize;

            var folders = Directory.EnumerateDirectories(ME3Directory.DLCPath);
            var extracted = folders.Where(folder => Directory.EnumerateFiles(folder, "*",
                SearchOption.AllDirectories).Any(file => file.EndsWith("mount.dlc", StringComparison.OrdinalIgnoreCase)));
            var unextracted = folders.Except(extracted);

            double compressedSize = 0;
            double uncompressedSize = 0;
            double largestUncompressedSize = 0;
            double largestCompressedSize = 0;
            sfarsToUnpack = new List<DLCUnpack>();
            foreach (var folder in unextracted)
            {
                if (!Path.GetFileName(folder).StartsWith("DLC"))
                    continue;

                try
                {
                    FileInfo info = new FileInfo(Directory.EnumerateFiles(folder, "*",
                        SearchOption.AllDirectories).First(file => file.EndsWith(".sfar", StringComparison.OrdinalIgnoreCase)));

                    // Skip sfar files which are already unpacked
                    if (info.Length < 64000)
                        continue;

                    DLCUnpack sfar = new DLCUnpack(info.FullName);
                    sfarsToUnpack.Add(sfar);

                    compressedSize += info.Length;
                    largestCompressedSize = Math.Max(largestCompressedSize, info.Length);

                    uncompressedSize += sfar.UncompressedSize;
                    largestUncompressedSize = Math.Max(largestUncompressedSize, sfar.UncompressedSize);
                }
                catch (Exception)
                {
                    return -1;
                }
            }

            totalUncompressedSize = uncompressedSize;

            if (sfarsToUnpack.Count == 0)
            {
                CurrentOverallOperationText = "All installed DLC is currently unpacked.";
            }

            // each SFAR is stripped of all its files after unpacking, so the maximum space needed on the drive is
            // the difference between the uncompressed size and compressed size of all SFARS, plus the compressed and 
            // uncompressed of the largest SFAR.
            return (uncompressedSize - compressedSize) + largestUncompressedSize + largestCompressedSize;
        }

        private void LoadCommands()
        {
            // Player commands
            UnpackDLCCommand = new RelayCommand(UnpackDLC, CanUnpackDLC);
            CancelUnpackCommand = new RelayCommand(CancelUnpacking, CanCancelUnpack);
        }

        private bool CanCancelUnpack(object obj)
        {
            return UnpackDLCWorker != null && UnpackDLCWorker.IsBusy && !UnpackCanceled;
        }

        private void CancelUnpacking(object obj)
        {
            UnpackCanceled = true;
            foreach (DLCUnpack dlc in sfarsToUnpack)
            {
                dlc.UnpackCanceled = true;
            }
        }

        private bool CanUnpackDLC(object obj)
        {
            return AvailableSpace > RequiredSpace && RequiredSpace > 0 && sfarsToUnpack.Count > 0 && UnpackDLCWorker == null;
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
            foreach (var sfar in sfarsToUnpack)
            {
                if (!UnpackCanceled)
                {
                    sfar.PropertyChanged += SFAR_PropertyChanged;
                    string DLCname = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(sfar.filePath)));
                    string outPath = Path.Combine(ME3Directory.DLCPath, DLCname);
                    sfar.Extract(outPath);
                }
            }

            if (UnpackCanceled)
            {
                CurrentOverallProgressValue = 0;
                OverallProgressValue = 0;
                CurrentOverallOperationText = "DLC unpacking cancelled";
            }
            else
            {
                CurrentOverallOperationText = "DLC has been unpacked";
                CurrentOverallProgressValue = 100;
                OverallProgressValue = 100;
            }
            CurrentOperationText = "";

            RequiredSpaceText = "Calculating...";
            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += CalculateUnpackRequirements;
            bg.RunWorkerCompleted += CalculateUnpackRequirements_Completed;
            bg.RunWorkerAsync();
        }

        private void SFAR_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "CurrentStatus":
                    CurrentOperationText = (sender as DLCUnpack).CurrentStatus;
                    break;
                case "CurrentOverallStatus":
                    CurrentOverallOperationText = (sender as DLCUnpack).CurrentOverallStatus;
                    break;
                case "CurrentProgress":
                    CurrentOverallProgressValue = (sender as DLCUnpack).CurrentProgress;
                    break;
                case "CurrentFilesProcessed":
                    RecalculateOverallProgress();
                    break;
                case "IndeterminateState":
                    ProgressBarIndeterminate = (sender as DLCUnpack).IndeterminateState;
                    break;
            }
        }

        private void RecalculateOverallProgress()
        {
            int totalFiles = (int)sfarsToUnpack.Sum(x => x.TotalFilesInDLC);
            int processedFiles = sfarsToUnpack.Sum(x => x.CurrentFilesProcessed);
            OverallProgressValue = (int)(100.0 * processedFiles) / totalFiles;
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //This tool does not handle updates.
        }

        private void DLCUnpacker_Closing(object sender, CancelEventArgs e)
        {
            backgroundticker.Stop();
            CancelUnpacking(null);
        }
    }
}
