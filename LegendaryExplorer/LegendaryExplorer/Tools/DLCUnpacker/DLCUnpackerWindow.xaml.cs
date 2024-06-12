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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.DLCUnpacker
{
    /// <summary>
    /// Interaction logic for DLCUnpackerWindow.xaml
    /// </summary>
    public partial class DLCUnpackerWindow : TrackingNotifyPropertyChangedWindowBase
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
            get => _unpackingPercentString;
            set => SetProperty(ref _unpackingPercentString, value);
        }
        private string _requiredSpaceText;

        private const string NotEnoughSpaceStr = "Not enough free space to unpack DLC.";

        public string RequiredSpaceText
        {
            get => _requiredSpaceText;
            set => SetProperty(ref _requiredSpaceText, value);
        }
        private string _availableSpaceText;
        public string AvailableSpaceText
        {
            get => _availableSpaceText;
            set => SetProperty(ref _availableSpaceText, value);
        }

        private double _currentOverallProgressValue;
        public double CurrentOverallProgressValue
        {
            get => _currentOverallProgressValue;
            set => SetProperty(ref _currentOverallProgressValue, value);
        }

        private int _overallProgressValue;
        public int OverallProgressValue
        {
            get => _overallProgressValue;
            set => SetProperty(ref _overallProgressValue, value);
        }

        //Used for the current operation (e.g. which DLC is being unpacked, it's %)
        private int _currentOperationProgressValue;
        public int CurrentOperationPercentValue
        {
            get => _currentOperationProgressValue;
            set => SetProperty(ref _currentOperationProgressValue, value);
        }

        private bool _progressBarIndeterminate;
        public bool ProgressBarIndeterminate
        {
            get => _progressBarIndeterminate;
            set => SetProperty(ref _progressBarIndeterminate, value);
        }
        private string _currentOperationText;
        public string CurrentOperationText
        {
            get => _currentOperationText;
            set => SetProperty(ref _currentOperationText, value);
        }

        private string _currentOverallOperationText;
        public string CurrentOverallOperationText
        {
            get => _currentOverallOperationText;
            set => SetProperty(ref _currentOverallOperationText, value);
        }

        #endregion
        List<SFARUnpacker> sfarsToUnpack = new List<SFARUnpacker>();
        private DispatcherTimer backgroundticker;

        public DLCUnpackerWindow() : base("DLC Unpacker", true)
        {
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
                var parts = ME3Directory.DefaultGamePath.Split(':');
                DriveInfo info = new DriveInfo(parts[0]);
                AvailableSpace = info.AvailableFreeSpace;
                AvailableSpaceText = FileSize.FormatSize((long)AvailableSpace);
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
            if (ME3Directory.DefaultGamePath != null)
            {
                ProgressBarIndeterminate = true;
                Debug.WriteLine("Available space set");
                RequiredSpace = GetRequiredSize();
                if (RequiredSpace >= 0)
                {
                    RequiredSpaceText = FileSize.FormatSize((long)RequiredSpace);
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
            var extracted = folders.Where(folder => Directory.EnumerateFiles(folder, "*",
                SearchOption.AllDirectories).Any(file => file.EndsWith("mount.dlc", StringComparison.OrdinalIgnoreCase)));
            var unextracted = folders.Except(extracted);

            double compressedSize = 0;
            double uncompressedSize = 0;
            double largestUncompressedSize = 0;
            double largestCompressedSize = 0;
            sfarsToUnpack = new List<SFARUnpacker>();
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

                    SFARUnpacker unpacker = new SFARUnpacker(info.FullName);
                    sfarsToUnpack.Add(unpacker);

                    compressedSize += info.Length;
                    largestCompressedSize = Math.Max(largestCompressedSize, info.Length);

                    uncompressedSize += unpacker.UncompressedSize;
                    largestUncompressedSize = Math.Max(largestUncompressedSize, unpacker.UncompressedSize);
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
            foreach (SFARUnpacker dlc in sfarsToUnpack)
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
                    CurrentOperationText = (sender as SFARUnpacker).CurrentStatus;
                    break;
                case "CurrentOverallStatus":
                    CurrentOverallOperationText = (sender as SFARUnpacker).CurrentOverallStatus;
                    break;
                case "CurrentProgress":
                    CurrentOverallProgressValue = (sender as SFARUnpacker).CurrentProgress;
                    break;
                case "CurrentFilesProcessed":
                    RecalculateOverallProgress();
                    break;
                case "IndeterminateState":
                    ProgressBarIndeterminate = (sender as SFARUnpacker).IndeterminateState;
                    break;
            }
        }

        private void RecalculateOverallProgress()
        {
            int totalFiles = (int)sfarsToUnpack.Sum(x => x.TotalFilesInDLC);
            int processedFiles = sfarsToUnpack.Sum(x => x.CurrentFilesProcessed);
            OverallProgressValue = (int)(100.0 * processedFiles) / totalFiles;
        }

        private void DLCUnpacker_Closing(object sender, CancelEventArgs e)
        {
            backgroundticker.Stop();
            CancelUnpacking(null);
        }
    }
}
