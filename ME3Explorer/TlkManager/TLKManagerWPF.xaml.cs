using ME1Explorer;
using ME2Explorer;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace ME3Explorer.TlkManagerNS
{
    /// <summary>
    /// Interaction logic for TLKManagerWPF.xaml
    /// </summary>
    public partial class TLKManagerWPF : NotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<LoadedTLK> ME1TLKItems { get; } = new ObservableCollectionExtended<LoadedTLK>();
        public ObservableCollectionExtended<LoadedTLK> ME2TLKItems { get; } = new ObservableCollectionExtended<LoadedTLK>();
        public ObservableCollectionExtended<LoadedTLK> ME3TLKItems { get; } = new ObservableCollectionExtended<LoadedTLK>();

        private bool bSaveNeededME1 = false;
        private bool bSaveNeededME2 = false;
        private bool bSaveNeededME3 = false;

        public TLKManagerWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("TLK Manager WPF", new WeakReference(this));

            DataContext = this;
            LoadCommands();
            InitializeComponent();
            ME1TLKItems.CollectionChanged += ME1CollectionChangedEventHandler;
            ME2TLKItems.CollectionChanged += ME2CollectionChangedEventHandler;
            ME3TLKItems.CollectionChanged += ME3CollectionChangedEventHandler;
            ME1TLKItems.AddRange(ME1TalkFiles.tlkList.Select(x => new LoadedTLK(x.pcc.FileName, x.uindex, x.pcc.getUExport(x.uindex).ObjectName, true)));
            ME2TLKItems.AddRange(ME2TalkFiles.tlkList.Select(x => new LoadedTLK(x.path, true)));
            ME3TLKItems.AddRange(ME3TalkFiles.tlkList.Select(x => new LoadedTLK(x.path, true)));

        }

        #region Commands
        public ICommand ME1ReloadTLKs { get; set; }
        public ICommand ME2ReloadTLKs { get; set; }
        public ICommand ME3ReloadTLKs { get; set; }

        public ICommand ME1AutoFindTLK { get; set; }
        public ICommand ME2AutoFindTLK { get; set; }
        public ICommand ME3AutoFindTLK { get; set; }

        public ICommand ME1AddManualTLK { get; set; }
        public ICommand ME2AddManualTLK { get; set; }
        public ICommand ME3AddManualTLK { get; set; }

        public ICommand ME1ExportImportTLK { get; set; }
        public ICommand ME2ExportImportTLK { get; set; }
        public ICommand ME3ExportImportTLK { get; set; }

        public ICommand ME1ApplyChanges { get; set; }
        public ICommand ME2ApplyChanges { get; set; }
        public ICommand ME3ApplyChanges { get; set; }

        private static string _me1LastReloaded;
        private static string _me2LastReloaded;
        private static string _me3LastReloaded;

        public static string ME1LastReloaded
        {
            get => _me1LastReloaded;
            set
            {
                if (_me1LastReloaded == value) return;
                _me1LastReloaded = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ME1LastReloaded)));
            }
        }
        public static string ME2LastReloaded
        {
            get => _me2LastReloaded;
            set
            {
                if (_me2LastReloaded == value) return;
                _me2LastReloaded = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ME2LastReloaded)));
            }
        }
        public static string ME3LastReloaded
        {
            get => _me3LastReloaded;
            set
            {
                if (_me3LastReloaded == value) return;
                _me3LastReloaded = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ME3LastReloaded)));
            }
        }

        // Declare a static event representing changes to your static property
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        /// <summary>
        /// Used for static property binding only.
        /// </summary>
        static TLKManagerWPF()
        {
            StaticPropertyChanged += (send, e) => { };
        }

        private void LoadCommands()
        {
            ME1ReloadTLKs = new GenericCommand(ME1ReloadTLKStrings, ME1GamePathExists);
            ME2ReloadTLKs = new GenericCommand(ME2ReloadTLKStrings, ME2BIOGamePathExists);
            ME3ReloadTLKs = new GenericCommand(ME3ReloadTLKStrings, ME3BIOGamePathExists);

            ME1AutoFindTLK = new GenericCommand(AutoFindTLKME1, ME1GamePathExists);
            ME2AutoFindTLK = new GenericCommand(AutoFindTLKME2, ME2BIOGamePathExists);
            ME3AutoFindTLK = new GenericCommand(AutoFindTLKME3, ME3BIOGamePathExists);

            ME1AddManualTLK = new GenericCommand(AddTLKME1, ME1GamePathExists);
            ME2AddManualTLK = new GenericCommand(AddTLKME2, ME2BIOGamePathExists);
            ME3AddManualTLK = new GenericCommand(AddTLKME3, ME3BIOGamePathExists);

            ME1ExportImportTLK = new GenericCommand(ImportExportTLKME1, ME1TLKListNotEmptyAndGameExists);
            ME2ExportImportTLK = new GenericCommand(ImportExportTLKME2, ME2TLKListNotEmpty);
            ME3ExportImportTLK = new GenericCommand(ImportExportTLKME3, ME3TLKListNotEmpty);

            ME1ApplyChanges = new GenericCommand(ME1ReloadTLKStrings, ME1CanApplyChanges);
            ME2ApplyChanges = new GenericCommand(ME2ReloadTLKStrings, ME2CanApplyChanges);
            ME3ApplyChanges = new GenericCommand(ME3ReloadTLKStrings, ME3CanApplyChanges);
        }

        private bool ME3TLKListNotEmpty()
        {
            return ME3TLKItems.Count > 0;
        }

        private bool ME2TLKListNotEmpty()
        {
            return ME2TLKItems.Count > 0;
        }

        private bool ME1TLKListNotEmptyAndGameExists()
        {
            return ME1TLKItems.Count > 0 && ME1GamePathExists();
        }

        private bool ME1CanApplyChanges()
        {
            return bSaveNeededME1;
        }

        private bool ME2CanApplyChanges()
        {
            return bSaveNeededME2;
        }

        private bool ME3CanApplyChanges()
        {
            return bSaveNeededME3;
        }

        private void ImportExportTLKME1()
        {
            new TLKManagerWPF_ExportReplaceDialog(ME1TLKItems.ToList()).Show();
        }

        private void ImportExportTLKME2()
        {
            new TLKManagerWPF_ExportReplaceDialog(ME2TLKItems.ToList()).Show();
        }

        private void ImportExportTLKME3()
        {
            new TLKManagerWPF_ExportReplaceDialog(ME3TLKItems.ToList()).Show();
        }

        private string getTLKFile()
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                EnsurePathExists = true,
                Title = "Select TLK file to load",
            };
            m.Filters.Add(new CommonFileDialogFilter("Talk files", "*.tlk"));
            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                return m.FileName;
            }
            return null;
        }

        private void AddTLKME3()
        {
            string tlk = getTLKFile();
            if (tlk != null)
            {
                LoadedTLK lTLK = new LoadedTLK(tlk, true);
                ME3TLKItems.Add(lTLK);
                ME3TLKList.SelectedItems.Add(lTLK);
            }
        }

        private void AddTLKME2()
        {
            string tlk = getTLKFile();
            if (tlk != null)
            {
                LoadedTLK lTLK = new LoadedTLK(tlk, true);
                ME2TLKItems.Add(lTLK);
                ME2TLKList.SelectedItems.Add(lTLK);
            }
        }

        private void AddTLKME1()
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                EnsurePathExists = true,
                Title = "Select UPK containing TLK",
            };
            m.Filters.Add(new CommonFileDialogFilter("Unreal Package File (ME1)", "*.upk;*.sfm")); //Maybe include SFM, though IDK if anyone would load an SFM. Maybe if they want to export ME1 TLKs for dialogue? Are the local ones even used?
            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                using (ME1Package upk = MEPackageHandler.OpenME1Package(m.FileName))
                {
                    foreach (IExportEntry exp in upk.Exports)
                    {
                        if (exp.ClassName == "BioTlkFile")
                        {
                            LoadedTLK lTLK = new LoadedTLK(m.FileName, exp.UIndex, exp.ObjectName, false);
                            ME1TLKItems.Add(lTLK);
                            ME1TLKList.SelectedItems.Add(lTLK);
                        }
                    }
                    SelectLoadedTLKsME1();
                }
            }
        }

        #endregion

        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get => _isBusyTaskbar;
            set => SetProperty(ref _isBusyTaskbar, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

        private async void ME3ReloadTLKStrings()
        {
            BusyText = "Reloading Mass Effect 3 TLK strings";
            IsBusy = true;
            bSaveNeededME3 = false;
            ME3TalkFiles.tlkList.Clear();
            await Task.Run(() => ME3ReloadTLKStringsAsync(ME3TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private async void ME2ReloadTLKStrings()
        {
            BusyText = "Reloading Mass Effect 2 TLK strings";
            bSaveNeededME2 = false;
            IsBusy = true;
            ME2TalkFiles.tlkList.Clear();
            await Task.Run(() => ME2ReloadTLKStringsAsync(ME2TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private void ME1ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            foreach (LoadedTLK tlk in tlksToLoad)
            {
                ME1TalkFiles.LoadTlkData(tlk.tlkPath, tlk.exportNumber);
            }
            ME1LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            ME1TalkFiles.SaveTLKList();
        }

        private void ME2ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            foreach (LoadedTLK tlk in tlksToLoad)
            {
                ME2TalkFiles.LoadTlkData(tlk.tlkPath);
            }
            ME2LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            ME2TalkFiles.SaveTLKList();
        }

        private void ME3ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            foreach (LoadedTLK tlk in tlksToLoad)
            {
                ME3TalkFiles.LoadTlkData(tlk.tlkPath);
            }
            ME3LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            ME3TalkFiles.SaveTLKList();
        }

        private async void ME1ReloadTLKStrings()
        {
            BusyText = "Reloading Mass Effect TLK strings";
            bSaveNeededME1 = false;
            IsBusy = true;
            ME1TalkFiles.tlkList.Clear();
            await Task.Run(() => ME1ReloadTLKStringsAsync(ME1TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private void AutoFindTLKME3()
        {
            BusyText = "Scanning for Mass Effect 3 TLK files";
            IsBusy = true;
            ME3TalkFiles.tlkList.Clear();
            Task.Run(() =>
            {
                var tlkmountmap = new List<(string, int)>();
                var tlks = Directory.EnumerateFiles(ME3Directory.BIOGamePath, "*.tlk", SearchOption.AllDirectories).Select(x => new LoadedTLK(x, false)).ToList();
                tlks.ForEach(x => x.LoadMountPriority());
                tlks.Sort((a, b) => a.mountpriority.CompareTo(b.mountpriority));
                return tlks;
            }).ContinueWithOnUIThread(prevTask =>
            {
                ME3TLKItems.ReplaceAll(prevTask.Result);
                SelectLoadedTLKsME3();
                IsBusy = false;
                if (prevTask.Result.Count > 0 && ME1TLKItems.Any(x => x.selectedForLoad))
                {
                    PromptForReload(MEGame.ME3);
                }
            });
            bSaveNeededME3 = true;
        }

        private void AutoFindTLKME2()
        {
            BusyText = "Scanning for Mass Effect 2 TLK files";
            IsBusy = true;
            ME2TalkFiles.tlkList.Clear();
            Task.Run(() =>
            {
                var tlkmountmap = new List<(string, int)>();
                var tlks = Directory.EnumerateFiles(ME2Directory.BioGamePath, "*.tlk", SearchOption.AllDirectories).Select(x => new LoadedTLK(x, false)).ToList();
                tlks.ForEach(x => x.LoadMountPriority());
                tlks.Sort((a, b) => a.mountpriority.CompareTo(b.mountpriority));
                return tlks;
            }).ContinueWithOnUIThread(prevTask =>
            {
                ME2TLKItems.ReplaceAll(prevTask.Result);
                SelectLoadedTLKsME2();
                IsBusy = false;
                if (prevTask.Result.Count > 0 && ME2TLKItems.Any(x => x.selectedForLoad))
                {
                    PromptForReload(MEGame.ME2);
                }
            });
            bSaveNeededME2 = true;
        }

        private void PromptForReload(MEGame game)
        {
            var result = MessageBox.Show(this, "Reload TLKs and save automatically found TLK list?", "Reload TLKs", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                switch (game)
                {
                    case MEGame.ME1:
                        ME1ReloadTLKStrings();
                        break;
                    case MEGame.ME2:
                        ME2ReloadTLKStrings();
                        break;
                    case MEGame.ME3:
                        ME3ReloadTLKStrings();
                        break;
                }
            }
        }

        private void AutoFindTLKME1()
        {
            BusyText = "Scanning for Mass Effect TLK files";
            IsBusy = true;
            ME1TalkFiles.tlkList.Clear();
            Task.Run(() =>
            {
                var tlkfiles = Directory.EnumerateFiles(ME1Directory.gamePath, "*Tlk*", SearchOption.AllDirectories).ToList();
                var tlks = new List<LoadedTLK>();
                foreach (string tlk in tlkfiles)
                {
                    //don't dispose the ME1Package, as this will prvent the talkfile from working
                    ME1Package upk = MEPackageHandler.OpenME1Package(tlk);
                    foreach (IExportEntry exp in upk.Exports)
                    {
                        tlks.Add(new LoadedTLK(tlk, exp.UIndex, exp.ObjectName, false));
                    }
                }
                return tlks;
            }).ContinueWithOnUIThread(prevTask =>
            {
                ME1TLKItems.ReplaceAll(prevTask.Result);
                SelectLoadedTLKsME1();
                IsBusy = false;
                if (prevTask.Result.Count > 0 && ME1TLKItems.Any(x => x.selectedForLoad))
                {
                    PromptForReload(MEGame.ME1);
                }
            });
            bSaveNeededME1 = true;
        }

        private static bool ME1GamePathExists()
        {
            return ME1Directory.gamePath != null && Directory.Exists(ME1Directory.gamePath);
        }

        private static bool ME2BIOGamePathExists()
        {
            return ME2Directory.BioGamePath != null && Directory.Exists(ME2Directory.BioGamePath);
        }

        private static bool ME3BIOGamePathExists()
        {
            return ME3Directory.BIOGamePath != null && Directory.Exists(ME3Directory.BIOGamePath);
        }

        public class LoadedTLK : NotifyPropertyChangedBase
        {
            public bool embedded { get; set; }
            public string tlkPath { get; set; }
            public int exportNumber { get; set; }
            public string tlkDisplayPath { get; set; }
            public string exportName { get; set; }
            public int mountpriority { get; set; }

            private bool _selectedForLoad;
            public bool selectedForLoad
            {
                get => _selectedForLoad;
                set => SetProperty(ref _selectedForLoad, value);
            }

            public LoadedTLK(string tlkPath, bool selectedForLoad)
            {
                this.tlkPath = tlkPath;
                this.tlkDisplayPath = System.IO.Path.GetFileName(tlkPath);
                this.selectedForLoad = selectedForLoad;
            }

            public LoadedTLK(string tlkPath, int exportNumber, string exportName, bool selectedForLoad)
            {
                this.exportNumber = exportNumber;
                this.embedded = true;
                this.tlkPath = tlkPath;
                this.exportName = exportName;
                this.tlkDisplayPath = $"{exportName} - {System.IO.Path.GetFileName(tlkPath)}";
                this.selectedForLoad = selectedForLoad;
            }

            /// <summary>
            /// Loads the mount.dlc file in the same directory as the TLK file and assigns it to this object, which can be used for sorting.
            /// If the mount.dlc file is not found, the default value of 0 is used (basegame).
            /// </summary>
            internal void LoadMountPriority()
            {
                if (embedded) return; //This does not apply to embedded files.
                string mountPath = System.IO.Path.Combine(Directory.GetParent(tlkPath).FullName, "mount.dlc");
                if (File.Exists(mountPath))
                {
                    mountpriority = new MountFile(mountPath).MountPriority;
                    tlkDisplayPath = $"Priority {mountpriority}: {tlkDisplayPath}";
                }
            }
        }

        private void ME3TLKLangCombobox_Changed(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME3();
        }

        private void SelectLoadedTLKsME1()
        {
            var tlkLang = ((ComboBoxItem)ME1TLKLangCombobox.SelectedItem).Content.ToString();
            bool male = tlkLang.EndsWith("Male");
            if (male)
            {
                tlkLang = tlkLang.Substring(0, tlkLang.Length - 7); //include 2 spaces and -
            }
            else
            {
                tlkLang = tlkLang.Substring(0, tlkLang.Length - 9); //include 2 spaces and -
            }

            if (tlkLang != "Default")
            {
                tlkLang += ".upk";
            }
            else
            {
                tlkLang = "Tlk.upk";
            }

            foreach (LoadedTLK tlk in ME1TLKItems)
            {
                tlk.selectedForLoad = ((tlk.exportName.EndsWith("_M") && male) || (!tlk.exportName.EndsWith("_M") && !male)) && tlk.tlkPath.EndsWith(tlkLang);
            }
        }

        private void SelectLoadedTLKsME3()
        {
            Debug.WriteLine("Loaded selected TLK.");
            var tlkLang = ((ComboBoxItem)ME3TLKLangCombobox.SelectedItem).Content.ToString();
            Debug.WriteLine("Content to string done");
            tlkLang += ".tlk";
            foreach (LoadedTLK tlk in ME3TLKItems)
            {
                tlk.selectedForLoad = tlk.tlkPath.EndsWith(tlkLang);
            }
            Debug.WriteLine("loaded");
        }

        private void SelectLoadedTLKsME2()
        {
            var tlkLang = ((ComboBoxItem)ME2TLKLangCombobox.SelectedItem).Content.ToString();
            tlkLang += ".tlk";
            foreach (LoadedTLK tlk in ME2TLKItems)
            {
                tlk.selectedForLoad = tlk.tlkPath.EndsWith(tlkLang);
            }
        }

        private void ME2TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME2();
        }

        private void ME1TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME1();
        }

        private async void TLKManager_Closing(object sender, CancelEventArgs e)
        {
            if(bSaveNeededME1 || bSaveNeededME2 || bSaveNeededME3)
            {
                var confirm = MessageBox.Show("You are exiting the manager without saving the changes to the TLK list(s). Save now?", "TLK Manager", MessageBoxButton.YesNoCancel);
                if(confirm == MessageBoxResult.Yes)
                {
                    if(bSaveNeededME1)
                    {
                        ME1TalkFiles.tlkList.Clear();
                        await Task.Run(() => ME1ReloadTLKStringsAsync(ME1TLKItems.Where(x => x.selectedForLoad).ToList()));
                    }
                    if(bSaveNeededME2)
                    {
                        ME2TalkFiles.tlkList.Clear();
                        await Task.Run(() => ME2ReloadTLKStringsAsync(ME2TLKItems.Where(x => x.selectedForLoad).ToList()));
                    }
                    if (bSaveNeededME3)
                    {
                        ME3TalkFiles.tlkList.Clear();
                        await Task.Run(() => ME3ReloadTLKStringsAsync(ME3TLKItems.Where(x => x.selectedForLoad).ToList()));
                    }
                }
                else if (confirm == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        void ME1CollectionChangedEventHandler(object items, NotifyCollectionChangedEventArgs e)
        {
            if (items != null)
            {
                foreach (LoadedTLK t in ME1TLKItems)
                {
                    t.PropertyChanged += ME1PropertyChangeHandler;
                }
            }
        }

        public void ME1PropertyChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            bSaveNeededME1 = true;
        }

        void ME2CollectionChangedEventHandler(object items, NotifyCollectionChangedEventArgs e)
        {
            if (items != null)
            {
                foreach (LoadedTLK t in ME2TLKItems)
                {
                    t.PropertyChanged += ME2PropertyChangeHandler;
                }
            }
        }

        public void ME2PropertyChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            bSaveNeededME2 = true;
        }

        void ME3CollectionChangedEventHandler(object items, NotifyCollectionChangedEventArgs e)
        {
            if (items != null)
            {
                foreach (LoadedTLK t in ME3TLKItems)
                {
                    t.PropertyChanged += ME3PropertyChangeHandler;
                }
            }
        }

        public void ME3PropertyChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            bSaveNeededME3 = true;
        }

        /// <summary>
        /// Looks up current loaded file game ID and returns appropriate string reference.
        /// </summary>
        /// <param name="stringRefID"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string GlobalFindStrRefbyID(int stringRefID, MEGame game)
        {
            if (stringRefID <= 0)
            {
                return null;
            }

            if (game == MEGame.ME1)
            {
                return ME1TalkFiles.findDataById(stringRefID);
            }
            else if (game == MEGame.ME2)
            {
                return ME2TalkFiles.findDataById(stringRefID);
            }
            else
            {
                return ME3TalkFiles.findDataById(stringRefID);
            }
        }
    }
}
