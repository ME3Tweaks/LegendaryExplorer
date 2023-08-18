using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.AppCenter.Analytics;
using Microsoft.WindowsAPICodePack.Dialogs;
using AFCCompactor = LegendaryExplorerCore.Audio.AFCCompactor;
using Application = System.Windows.Application;

namespace LegendaryExplorer.Tools.AFCCompactorWindow
{
    /// <summary>
    /// Interaction logic for AFCCompactorWindow.xaml
    /// </summary>
    public partial class AFCCompactorWindow : TrackingNotifyPropertyChangedWindowBase
    {
        public class DLCDependency : NotifyPropertyChangedBase
        {
            protected bool Equals(DLCDependency other)
            {
                return UIString == other.UIString;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((DLCDependency)obj);
            }

            public override int GetHashCode()
            {
                return (UIString != null ? UIString.GetHashCode() : 0);
            }

            public string DLCName { get; set; }
            public string UIString { get; set; }
            private bool _isDependedOn = true;
            public bool IsDependedOn { get => _isDependedOn; set => SetProperty(ref _isDependedOn, value); }
        }

        public AFCCompactorWindow() : base("AFC Compactor", true)
        {
            LoadCommands();
            AudioReferencesView.Filter = FilterReferences;
            InitializeComponent();
        }

        private bool FilterReferences(object obj)
        {
            if (obj is AFCCompactor.ReferencedAudio ra)
            {
                var dlcDep = getDlcDependencyForAFC(ra.AFCName);
                // Any will work since there should be only 1 instance that matches in the list
                if (!ra.IsModified && dlcDep != null && DLCDependencies.Any(x => x.DLCName.Equals(dlcDep, StringComparison.InvariantCultureIgnoreCase) && !x.IsDependedOn))
                {
                    return false; // Selected to be depended on. Which means it won't be pulled in. Don't show it
                }
                if (string.IsNullOrWhiteSpace(FilterText)) return true;

                if (ra.AFCName.Contains(FilterText, StringComparison.InvariantCultureIgnoreCase)) return true;
                if (ra.AudioOffset.ToString("X8").Contains(FilterText, StringComparison.InvariantCultureIgnoreCase)) return true;
                if (ra.AFCSourceType.Contains(FilterText, StringComparison.InvariantCultureIgnoreCase)) return true;
                if (ra.OriginatingExportName.Contains(FilterText, StringComparison.InvariantCultureIgnoreCase)) return true;
            }

            return false;
        }

        private void LoadCommands()
        {
            ScanForReferencesCommand = new GenericCommand(ScanForReferences, CanScanForReferences);
            SelectDLCInputFolderCommand = new GenericCommand(SelectDLCInputFolder, () => !IsBusy);
            CompactAFCCommand = new GenericCommand(BeginCompactingAFC, CanCompactAFC);
        }

        private void BeginCompactingAFC()
        {
            var result = MessageBox.Show(
                            "Warning: This will modify all files in your mod. You should ensure you have a backup of your entire mod before you perform this procedure.\n\nDo you have a backup of your mod?",
                            "WARNING", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No) return;
            var referencesFiltered = AudioReferences.ToList(); //Clone
            FilterText = "";
            AudioReferencesView.Refresh();
            Task.Run(() =>
            {
                IsBusy = true;
                bool showBrokenAudio(List<(AFCCompactor.ReferencedAudio audioRef, string brokenReason)> brokenAudio)
                {
                    bool returnValue = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            "The following audio references are broken and will not be included in your new AFC. Please review them, and confirm continuation in the follow up dialog.",
                            "Broken audio found", MessageBoxButton.OK, MessageBoxImage.Error);
                        var ld = new ListDialog(brokenAudio.Select(x =>
                                $"{x.audioRef.OriginatingExportName} |||| {x.brokenReason}"),
                            "Broken audio references",
                            "The following audio references are invalid and do not work. These references will not be updated and the audio will remain broken. Once this dialog is closed, there will a confirmation dialog you can abort the compaction with.",
                            this);
                        ld.ShowDialog();
                        returnValue = MessageBox.Show(
                                          "It is highly recommended that you fix your audio references before compacting audio.\n\nCompact your AFC anyways?",
                                          "Broken audio found", MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
                                      MessageBoxResult.Yes;
                    });
                    return returnValue;
                }


                referencesFiltered.ReplaceAll(referencesFiltered.Where(x => FilterReferences(x)).ToList()); //Must use tolist or we'll get concurrent modification
                string finalAfcPath = null;
                var compactionResult = AFCCompactor.CompactAFC(SelectedGame, DLCInputFolder, NewAFCName, referencesFiltered, showBrokenAudio,
                    (done, total) =>
                    {
                        ProgressValue = done;
                        ProgressMax = total;
                    },
                    statusUpdate => StatusText = statusUpdate,
                    fafcpath => finalAfcPath = fafcpath,
                        (msg) => { });
                if (!compactionResult) return (compactionResult, null);

                // Check references
                var recalcedRefs = AFCCompactor.GetReferencedAudio(SelectedGame, DLCInputFolder,
                    (done, total) =>
                    {
                        ProgressValue = done;
                        ProgressMax = total;
                    },
                    x => StatusText = $"Rescanning {Path.GetFileName(x)}");
                var dependencyList = recalcedRefs.availableAFCReferences
                    .Where(x =>
                    {
                        var dependencyName = getDlcDependencyForAFC(x.AFCName);
                        if (dependencyName == null) return false;
                        if (x.AFCName.Equals(NewAFCName, StringComparison.InvariantCultureIgnoreCase)) return false; //we just made this obviously it depends on it
                        return true;
                    }).Select(x => getDlcDependencyForAFC(x.AFCName)).Distinct().ToList();
                if (finalAfcPath != null && File.Exists(finalAfcPath))
                {
                    LegendaryExplorerCoreUtilities.OpenAndSelectFileInExplorer(finalAfcPath);

                }
                return (compactionResult, dependencyList);

            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.Result.compactionResult)
                {
                    AudioReferences.ClearEx();
                    StatusText = "Compaction completed";
                    ListDialog ld = new ListDialog(prevTask.Result.dependencyList, "Compaction dependency results", "Your mod now depends on the following DLCs for audio. Users without these DLC will be have no audio play if the referenced AFC is attempted to be used from them.", this);
                    ld.Show();
                }
                else
                {
                    StatusText = "Compaction aborted or failed";
                }
                IsBusy = false;
                Analytics.TrackEvent("Compacted AFC");
            });

        }

        private bool CanCompactAFC()
        {
            if (!IsBusy && AudioReferences.Any() && !string.IsNullOrWhiteSpace(DLCInputFolder) && !string.IsNullOrWhiteSpace(NewAFCName))
            {
                var regex = new Regex(@"^[a-zA-Z0-9_]+$");
                return regex.IsMatch(NewAFCName);
            }
            return false;
        }

        #region Commands
        public GenericCommand ScanForReferencesCommand { get; set; }
        public GenericCommand SelectDLCInputFolderCommand { get; set; }
        public GenericCommand CompactAFCCommand { get; set; }
        #endregion


        #region Binding Vars
        public ObservableCollectionExtended<MEGame> GameOptions { get; } = new(new[] { MEGame.ME2, MEGame.ME3, MEGame.LE2, MEGame.LE3 });
        public ObservableCollectionExtended<AFCCompactor.ReferencedAudio> AudioReferences { get; } = new();
        public ICollectionView AudioReferencesView => CollectionViewSource.GetDefaultView(AudioReferences);
        public ObservableCollectionExtended<DLCDependency> DLCDependencies { get; } = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private long _progressMax = 100;
        public long ProgressMax
        {
            get => _progressMax;
            set => SetProperty(ref _progressMax, value);
        }
        private long _progressValue;
        public long ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }


        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _filterText;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    AudioReferencesView.Refresh();
                }
            }
        }

        private string _newAFCName;
        public string NewAFCName
        {
            get => _newAFCName;
            set => SetProperty(ref _newAFCName, value);
        }

        private string _dlcInputFolder;
        public string DLCInputFolder
        {
            get => _dlcInputFolder;
            set
            {
                SetProperty(ref _dlcInputFolder, value);
                if (!string.IsNullOrWhiteSpace(_dlcInputFolder))
                {
                    string cookedFolder = _dlcInputFolder;
                    string foldername = Path.GetFileName(DLCInputFolder);

                    // Locate the CookedPC folder
                    if (!foldername.StartsWith("CookedPC", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Directory.Exists(Path.Combine(_dlcInputFolder, "CookedPC")))
                            cookedFolder = Path.Combine(_dlcInputFolder, "CookedPC");
                        else if (Directory.Exists(Path.Combine(_dlcInputFolder, "CookedPCConsole")))
                            cookedFolder = Path.Combine(_dlcInputFolder, "CookedPCConsole");
                    }

                    // Determine game from mount file
                    try
                    {
                        var mount = new MountFile(Path.Combine(cookedFolder, "Mount.dlc"));
                        SelectedGame = mount.Game;
                    }
                    catch
                    {
                        SelectedGame = cookedFolder.Contains("CookedPCConsole", StringComparison.OrdinalIgnoreCase) ? MEGame.ME3 : MEGame.ME2;
                    }


                    if (foldername.ToLower().StartsWith("cookedpc"))
                    {
                        foldername = Path.GetFileName(Directory.GetParent(_dlcInputFolder).FullName);
                    }

                    NewAFCName = $"{foldername}_Audio";
                }
            }
        }

        private MEGame _selectedGame;
        public MEGame SelectedGame
        {
            get => _selectedGame;
            set => SetProperty(ref _selectedGame, value);
        }

        //private bool _includeBasegameAudio;
        //public bool IncludeBasegameAudio
        //{
        //    get => _includeBasegameAudio;
        //    set => SetProperty(ref _includeBasegameAudio, value);
        //}

        //private bool _includeOfficialDLCAudio = true;
        //public bool IncludeOfficialDLCAudio
        //{
        //    get => _includeOfficialDLCAudio;
        //    set => SetProperty(ref _includeOfficialDLCAudio, value);
        //}

        private bool _scanCompleted = true;
        public bool ScanCompleted
        {
            get => _scanCompleted;
            set => SetProperty(ref _scanCompleted, value);
        }

        #endregion


        private void ScanForReferences()
        {
            if (Directory.Exists(DLCInputFolder))
            {
                //string[] afcFiles = Directory.GetFiles(DLCInputFolder, "*.afc", SearchOption.AllDirectories);
                string[] pccFiles = Directory.GetFiles(DLCInputFolder, "*.pcc", SearchOption.AllDirectories);

                if (pccFiles.Any())
                {
                    var allMessages = new List<string>();
                    Task.Run(() =>
                    {
                        IsBusy = true;
                        return AFCCompactor.GetReferencedAudio(SelectedGame, DLCInputFolder,
                            (done, total) =>
                            {
                                ProgressValue = done;
                                ProgressMax = total;
                            },
                            scanningPcc => StatusText = $"Scanning {Path.GetFileName(scanningPcc)}",
                            debugMsg =>
                            {
                                allMessages.Add(debugMsg);
                            });
                    }).ContinueWithOnUIThread(prevTask =>
                    {
                        //File.WriteAllLines(@"C:\Users\Public\AFCCompactorLog.txt", allMessages);
                        StatusText = "Review audio references and adjust as necessary";
                        AudioReferences.ReplaceAll(prevTask.Result.availableAFCReferences);
                        DLCDependencies.ReplaceAll(getDLCDependencies(prevTask.Result.availableAFCReferences, SelectedGame));
                        if (prevTask.Result.missingAFCReferences.Any())
                        {
                            var ld = new ListDialog(prevTask.Result.missingAFCReferences.Select(x =>
                                    $"{x.OriginatingExportName} references an AFC that could not be found: {x.AFCName}"),
                                "Broken audio references",
                                "The following audio references reference AFC files that could not be found. These are considered broken and will not be compacted, as they do not work.",
                                this);
                            ld.ShowDialog();
                        }
                        IsBusy = false;
                    });
                }
            }
        }

        private string getDlcDependencyForAFC(string afcName)
        {
            if (afcName.Contains("dlc_", StringComparison.InvariantCultureIgnoreCase))
            {
                return afcName.Substring(0, afcName.LastIndexOf('_'));
            }
            return null;
        }

        private IEnumerable<DLCDependency> getDLCDependencies(List<AFCCompactor.ReferencedAudio> audioReferences, MEGame game)
        {
            var dependencies = new HashSet<DLCDependency>();
            foreach (var reference in audioReferences)
            {
                var uiString = getDlcDependencyForAFC(reference.AFCName);
                if (uiString != null)
                {
                    var dlcDep = uiString;
                    if (MEDirectories.OfficialDLCNames(game).TryGetValue(uiString, out var humanName))
                    {
                        uiString += $" ({humanName})";
                    }

                    var dependencyObj = new DLCDependency()
                    {
                        UIString = uiString,
                        DLCName = dlcDep,
                        IsDependedOn = true
                    };
                    dependencyObj.PropertyChanged += DependencyPropertyChanged;

                    dependencies.Add(dependencyObj);
                }
            }

            return dependencies;
        }

        // Property changed for dependency object. Not the xaml type of DependencyProperty
        private void DependencyPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DLCDependency.IsDependedOn))
            {
                // Dependency state changing
                AudioReferencesView.Refresh();
            }
        }

        private void Extras()
        {
            //    string result = PromptDialog.Prompt(this,
            //            "Enter an AFC filename that all mod referenced items will be repointed to.\n\nCompacting AFC folder: " +
            //            foldername, "Enter an AFC filename");
            //    if (result != null)
            //{
            //    var regex = new Regex(@"^[a-zA-Z0-9_]+$");

            //    if (regex.IsMatch(result))
            //    {
            //        //BusyText = "Finding all referenced audio";
            //        //IsBusy = true;
            //        //IsBusyTaskbar = true;
            //        //soundPanel.FreeAudioResources(); // stop playing

            //        else
            //        {
            //            MessageBox.Show(
            //                "Only alphanumeric characters and underscores are allowed for the AFC filename.",
            //                "Error creating AFC");
            //        }
            //    }
        }

        private bool CanScanForReferences()
        {
            return !IsBusy && DLCInputFolder != null;
        }

        public void SelectDLCInputFolder()
        {
            var dlg = new CommonOpenFileDialog("Select your mod's DLC_ folder")
            {
                IsFolderPicker = true
            };

                if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                string[] pccFiles = Directory.GetFiles(dlg.FileName, "*.pcc", SearchOption.AllDirectories);
                string[] mountFiles = Directory.GetFiles(dlg.FileName, "*.dlc", SearchOption.AllDirectories);

                if (mountFiles.Length != 1)
                {
                    MessageBox.Show("Selected folder must contain a Mount.dlc file to indicate it is a DLC.", "Error");
                    return;
                }
                else if (!pccFiles.Any())
                {
                    MessageBox.Show("Selected folder must contain package files.", "Error");
                    return;
                }

                DLCInputFolder = dlg.FileName;
            }
        }

        private void AFCCompactorWindow_OnContentRendered(object sender, EventArgs e)
        {
            SelectedGame = MEGame.ME3;
        }
    }
}
