using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;

namespace LegendaryExplorer.Tools.TlkManagerNS
{
    /// <summary>
    /// Interaction logic for TLKManagerWPF.xaml
    /// </summary>
    public partial class TLKManagerWPF : TrackingNotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<LoadedTLK> ME1TLKItems { get; } = new();
        public ObservableCollectionExtended<LoadedTLK> ME2TLKItems { get; } = new();
        public ObservableCollectionExtended<LoadedTLK> ME3TLKItems { get; } = new();
        public ObservableCollectionExtended<LoadedTLK> LE1TLKItems { get; } = new();
        public ObservableCollectionExtended<LoadedTLK> LE2TLKItems { get; } = new();
        public ObservableCollectionExtended<LoadedTLK> LE3TLKItems { get; } = new();

        private bool bSaveNeededME1;
        private bool bSaveNeededME2;
        private bool bSaveNeededME3;
        private bool bSaveNeededLE1;
        private bool bSaveNeededLE2;
        private bool bSaveNeededLE3;

        public TLKManagerWPF() : base("TLK Manager", true)
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            ME1TLKItems.CollectionChanged += ME1CollectionChangedEventHandler;
            ME2TLKItems.CollectionChanged += ME2CollectionChangedEventHandler;
            ME3TLKItems.CollectionChanged += ME3CollectionChangedEventHandler;
            LE1TLKItems.CollectionChanged += LE1CollectionChangedEventHandler;
            LE2TLKItems.CollectionChanged += LE2CollectionChangedEventHandler;
            LE3TLKItems.CollectionChanged += LE3CollectionChangedEventHandler;
            ME1TLKItems.AddRange(ME1TalkFiles.LoadedTlks.Select(x => new LoadedTLK(x.FilePath, x.UIndex, x.Name, true)));
            ME2TLKItems.AddRange(ME2TalkFiles.LoadedTlks.Select(x => new LoadedTLK(x.FilePath, true)));
            ME3TLKItems.AddRange(ME3TalkFiles.LoadedTlks.Select(x => new LoadedTLK(x.FilePath, true)));
            LE1TLKItems.AddRange(LE1TalkFiles.LoadedTlks.Select(x => new LoadedTLK(x.FilePath, x.UIndex, x.Name, true)));
            LE2TLKItems.AddRange(LE2TalkFiles.LoadedTlks.Select(x => new LoadedTLK(x.FilePath, true)));
            LE3TLKItems.AddRange(LE3TalkFiles.LoadedTlks.Select(x => new LoadedTLK(x.FilePath, true)));
        }

        #region Commands
        public ICommand ME1ReloadTLKs { get; set; }
        public ICommand ME2ReloadTLKs { get; set; }
        public ICommand ME3ReloadTLKs { get; set; }
        public ICommand LE1ReloadTLKs { get; set; }
        public ICommand LE2ReloadTLKs { get; set; }
        public ICommand LE3ReloadTLKs { get; set; }

        public ICommand ME1AutoFindTLK { get; set; }
        public ICommand ME2AutoFindTLK { get; set; }
        public ICommand ME3AutoFindTLK { get; set; }
        public ICommand LE1AutoFindTLK { get; set; }
        public ICommand LE2AutoFindTLK { get; set; }
        public ICommand LE3AutoFindTLK { get; set; }

        public ICommand ME1AddManualTLK { get; set; }
        public ICommand ME2AddManualTLK { get; set; }
        public ICommand ME3AddManualTLK { get; set; }
        public ICommand LE1AddManualTLK { get; set; }
        public ICommand LE2AddManualTLK { get; set; }
        public ICommand LE3AddManualTLK { get; set; }

        public ICommand ME1ExportImportTLK { get; set; }
        public ICommand ME2ExportImportTLK { get; set; }
        public ICommand ME3ExportImportTLK { get; set; }
        public ICommand LE1ExportImportTLK { get; set; }
        public ICommand LE2ExportImportTLK { get; set; }
        public ICommand LE3ExportImportTLK { get; set; }

        public ICommand ME1ApplyChanges { get; set; }
        public ICommand ME2ApplyChanges { get; set; }
        public ICommand ME3ApplyChanges { get; set; }
        public ICommand LE1ApplyChanges { get; set; }
        public ICommand LE2ApplyChanges { get; set; }
        public ICommand LE3ApplyChanges { get; set; }

        private static string _me1LastReloaded;
        private static string _me2LastReloaded;
        private static string _me3LastReloaded;
        private static string _le1LastReloaded;
        private static string _le2LastReloaded;
        private static string _le3LastReloaded;

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
        public static string LE1LastReloaded
        {
            get => _le1LastReloaded;
            set
            {
                if (_le1LastReloaded == value) return;
                _le1LastReloaded = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(LE1LastReloaded)));
            }
        }
        public static string LE2LastReloaded
        {
            get => _le2LastReloaded;
            set
            {
                if (_le2LastReloaded == value) return;
                _le2LastReloaded = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(LE2LastReloaded)));
            }
        }
        public static string LE3LastReloaded
        {
            get => _le3LastReloaded;
            set
            {
                if (_le3LastReloaded == value) return;
                _le3LastReloaded = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(LE3LastReloaded)));
            }
        }

        // Declare a static event representing changes to your static property
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        /// <summary>
        /// Used for static property binding only.
        /// </summary>
        static TLKManagerWPF()
        {
            StaticPropertyChanged += (_, _) => { };
        }

        private void LoadCommands()
        {
            ME1ReloadTLKs = new GenericCommand(ME1ReloadTLKStrings, ME1GamePathExists);
            ME2ReloadTLKs = new GenericCommand(ME2ReloadTLKStrings, ME2BIOGamePathExists);
            ME3ReloadTLKs = new GenericCommand(ME3ReloadTLKStrings, ME3BIOGamePathExists);
            LE1ReloadTLKs = new GenericCommand(LE1ReloadTLKStrings, LE1GamePathExists);
            LE2ReloadTLKs = new GenericCommand(LE2ReloadTLKStrings, LE2BIOGamePathExists);
            LE3ReloadTLKs = new GenericCommand(LE3ReloadTLKStrings, LE3BIOGamePathExists);

            ME1AutoFindTLK = new GenericCommand(AutoFindTLKME1, ME1GamePathExists);
            ME2AutoFindTLK = new GenericCommand(AutoFindTLKME2, ME2BIOGamePathExists);
            ME3AutoFindTLK = new GenericCommand(AutoFindTLKME3, ME3BIOGamePathExists);
            LE1AutoFindTLK = new GenericCommand(AutoFindTLKLE1, LE1GamePathExists);
            LE2AutoFindTLK = new GenericCommand(AutoFindTLKLE2, LE2BIOGamePathExists);
            LE3AutoFindTLK = new GenericCommand(AutoFindTLKLE3, LE3BIOGamePathExists);

            ME1AddManualTLK = new GenericCommand(AddTLKME1, ME1GamePathExists);
            ME2AddManualTLK = new GenericCommand(AddTLKME2, ME2BIOGamePathExists);
            ME3AddManualTLK = new GenericCommand(AddTLKME3, ME3BIOGamePathExists);
            LE1AddManualTLK = new GenericCommand(AddTLKLE1, LE1GamePathExists);
            LE2AddManualTLK = new GenericCommand(AddTLKLE2, LE2BIOGamePathExists);
            LE3AddManualTLK = new GenericCommand(AddTLKLE3, LE3BIOGamePathExists);

            ME1ExportImportTLK = new GenericCommand(ImportExportTLKME1, ME1TLKListNotEmptyAndGameExists);
            ME2ExportImportTLK = new GenericCommand(ImportExportTLKME2, ME2TLKListNotEmpty);
            ME3ExportImportTLK = new GenericCommand(ImportExportTLKME3, ME3TLKListNotEmpty);
            LE1ExportImportTLK = new GenericCommand(ImportExportTLKLE1, LE1TLKListNotEmptyAndGameExists);
            LE2ExportImportTLK = new GenericCommand(ImportExportTLKLE2, LE2TLKListNotEmpty);
            LE3ExportImportTLK = new GenericCommand(ImportExportTLKLE3, LE3TLKListNotEmpty);

            ME1ApplyChanges = new GenericCommand(ME1ReloadTLKStrings, ME1CanApplyChanges);
            ME2ApplyChanges = new GenericCommand(ME2ReloadTLKStrings, ME2CanApplyChanges);
            ME3ApplyChanges = new GenericCommand(ME3ReloadTLKStrings, ME3CanApplyChanges);
            LE1ApplyChanges = new GenericCommand(LE1ReloadTLKStrings, LE1CanApplyChanges);
            LE2ApplyChanges = new GenericCommand(LE2ReloadTLKStrings, LE2CanApplyChanges);
            LE3ApplyChanges = new GenericCommand(LE3ReloadTLKStrings, LE3CanApplyChanges);
        }

        private bool ME3TLKListNotEmpty() => ME3TLKItems.Count > 0;

        private bool ME2TLKListNotEmpty() => ME2TLKItems.Count > 0;

        private bool ME1TLKListNotEmptyAndGameExists() => ME1TLKItems.Count > 0 && ME1GamePathExists();

        private bool LE3TLKListNotEmpty() => LE3TLKItems.Count > 0;

        private bool LE2TLKListNotEmpty() => LE2TLKItems.Count > 0;

        private bool LE1TLKListNotEmptyAndGameExists() => LE1TLKItems.Count > 0 && LE1GamePathExists();

        private bool ME1CanApplyChanges() => bSaveNeededME1;

        private bool ME2CanApplyChanges() => bSaveNeededME2;

        private bool ME3CanApplyChanges() => bSaveNeededME3;

        private bool LE1CanApplyChanges() => bSaveNeededLE1;

        private bool LE2CanApplyChanges() => bSaveNeededLE2;

        private bool LE3CanApplyChanges() => bSaveNeededLE3;

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

        private void ImportExportTLKLE1()
        {
            new TLKManagerWPF_ExportReplaceDialog(LE1TLKItems.ToList()).Show();
        }

        private void ImportExportTLKLE2()
        {
            new TLKManagerWPF_ExportReplaceDialog(LE2TLKItems.ToList()).Show();
        }

        private void ImportExportTLKLE3()
        {
            new TLKManagerWPF_ExportReplaceDialog(LE3TLKItems.ToList()).Show();
        }
        private string getTLKFile()
        {
            var m = new CommonOpenFileDialog
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
                var lTLK = new LoadedTLK(tlk, true);
                ME3TLKItems.Add(lTLK);
                ME3TLKList.SelectedItems.Add(lTLK);
            }
        }

        private void AddTLKME2()
        {
            string tlk = getTLKFile();
            if (tlk != null)
            {
                var lTLK = new LoadedTLK(tlk, true);
                ME2TLKItems.Add(lTLK);
                ME2TLKList.SelectedItems.Add(lTLK);
            }
        }

        private void AddTLKME1()
        {
            var m = new CommonOpenFileDialog
            {
                EnsurePathExists = true,
                Title = "Select UPK containing TLK",
            };
            m.Filters.Add(new CommonFileDialogFilter("Unreal Package File (ME1)", "*.upk;*.sfm"));
            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                using IMEPackage upk = MEPackageHandler.OpenME1Package(m.FileName);
                foreach (ExportEntry exp in upk.Exports)
                {
                    if (exp.ClassName == "BioTlkFile")
                    {
                        var lTLK = new LoadedTLK(m.FileName, exp.UIndex, exp.ObjectName, false);
                        ME1TLKItems.Add(lTLK);
                        ME1TLKList.SelectedItems.Add(lTLK);
                    }
                }
                SelectLoadedTLKsME1();
            }
        }

        private void AddTLKLE3()
        {
            string tlk = getTLKFile();
            if (tlk != null)
            {
                var lTLK = new LoadedTLK(tlk, true);
                LE3TLKItems.Add(lTLK);
                LE3TLKList.SelectedItems.Add(lTLK);
            }
        }

        private void AddTLKLE2()
        {
            string tlk = getTLKFile();
            if (tlk != null)
            {
                var lTLK = new LoadedTLK(tlk, true);
                LE2TLKItems.Add(lTLK);
                LE2TLKList.SelectedItems.Add(lTLK);
            }
        }

        private void AddTLKLE1()
        {
            var m = new CommonOpenFileDialog
            {
                EnsurePathExists = true,
                Title = "Select PCC containing TLK",
            };
            m.Filters.Add(new CommonFileDialogFilter("Unreal Package File (LE1)", "*.pcc"));
            if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                using IMEPackage upk = MEPackageHandler.OpenMEPackage(m.FileName);
                foreach (ExportEntry exp in upk.Exports)
                {
                    if (exp.ClassName == "BioTlkFile")
                    {
                        var lTLK = new LoadedTLK(m.FileName, exp.UIndex, exp.ObjectName, false);
                        LE1TLKItems.Add(lTLK);
                        LE1TLKList.SelectedItems.Add(lTLK);
                    }
                }
                SelectLoadedTLKsLE1();
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
            ME3TalkFiles.ClearLoadedTlks();
            await Task.Run(() => ME3ReloadTLKStringsAsync(ME3TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private async void ME2ReloadTLKStrings()
        {
            BusyText = "Reloading Mass Effect 2 TLK strings";
            bSaveNeededME2 = false;
            IsBusy = true;
            ME2TalkFiles.ClearLoadedTlks();
            await Task.Run(() => ME2ReloadTLKStringsAsync(ME2TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private async void ME1ReloadTLKStrings()
        {
            BusyText = "Reloading Mass Effect TLK strings";
            bSaveNeededME1 = false;
            IsBusy = true;
            ME1TalkFiles.ClearLoadedTlks();
            await Task.Run(() => ME1ReloadTLKStringsAsync(ME1TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private async void LE3ReloadTLKStrings()
        {
            BusyText = "Reloading Mass Effect 3 Legendary Edition TLK strings";
            IsBusy = true;
            bSaveNeededLE3 = false;
            LE3TalkFiles.ClearLoadedTlks();
            await Task.Run(() => LE3ReloadTLKStringsAsync(LE3TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private async void LE2ReloadTLKStrings()
        {
            BusyText = "Reloading Mass Effect 2 Legendary Edition TLK strings";
            bSaveNeededLE2 = false;
            IsBusy = true;
            LE2TalkFiles.ClearLoadedTlks();
            await Task.Run(() => LE2ReloadTLKStringsAsync(LE2TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private async void LE1ReloadTLKStrings()
        {
            BusyText = "Reloading Mass Effect 1 Legendary Edition TLK strings";
            bSaveNeededLE1 = false;
            IsBusy = true;
            LE1TalkFiles.ClearLoadedTlks();
            await Task.Run(() => LE1ReloadTLKStringsAsync(LE1TLKItems.Where(x => x.selectedForLoad).ToList()));
            IsBusy = false;
        }

        private static void ME1ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            //ME1 TLKs are held in Package Files
            //For a proper full reload we have to reload the package from disk
            // which we don't do
            ME1TalkFiles.ClearLoadedTlks();
            foreach (LoadedTLK tlk in tlksToLoad)
            {
                ME1TalkFiles.LoadTlkData(tlk.tlkPath, tlk.exportNumber);
            }
            ME1LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TLKLoader.SaveTLKList(MEGame.ME1);
        }

        private static void ME2ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            ME2TalkFiles.ClearLoadedTlks();

            foreach (LoadedTLK tlk in tlksToLoad)
            {
                ME2TalkFiles.LoadTlkData(tlk.tlkPath);
            }
            ME2LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TLKLoader.SaveTLKList(MEGame.ME2);
        }

        private static void ME3ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            ME3TalkFiles.ClearLoadedTlks();

            foreach (LoadedTLK tlk in tlksToLoad)
            {
                ME3TalkFiles.LoadTlkData(tlk.tlkPath);
            }
            ME3LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TLKLoader.SaveTLKList(MEGame.ME3);
        }

        private static void LE1ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            //LE1 TLKs are held in Package Files
            //For a proper full reload we have to reload the package from disk
            // which we don't do
            LE1TalkFiles.ClearLoadedTlks();
            foreach (LoadedTLK tlk in tlksToLoad)
            {
                LE1TalkFiles.LoadTlkData(tlk.tlkPath, tlk.exportNumber);
            }
            LE1LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TLKLoader.SaveTLKList(MEGame.LE1);
        }

        private static void LE2ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            LE2TalkFiles.ClearLoadedTlks();

            foreach (LoadedTLK tlk in tlksToLoad)
            {
                LE2TalkFiles.LoadTlkData(tlk.tlkPath);
            }
            LE2LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TLKLoader.SaveTLKList(MEGame.LE2);
        }

        private static void LE3ReloadTLKStringsAsync(List<LoadedTLK> tlksToLoad)
        {
            LE3TalkFiles.ClearLoadedTlks();

            foreach (LoadedTLK tlk in tlksToLoad)
            {
                LE3TalkFiles.LoadTlkData(tlk.tlkPath);
            }
            LE3LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TLKLoader.SaveTLKList(MEGame.LE3);
        }

        private void AutoFindTLKME3()
        {
            BusyText = "Scanning for Mass Effect 3 TLK files";
            IsBusy = true;
            ME3TalkFiles.LoadedTlks.Clear();
            Task.Run(() =>
            {
                var tlkmountmap = new List<(string, int)>();
                var tlks = Directory.EnumerateFiles(ME3Directory.BioGamePath, "*.tlk", SearchOption.AllDirectories).Select(x => new LoadedTLK(x, false)).ToList();
                tlks.ForEach(x => x.LoadMountPriority());
                tlks.Sort((a, b) => a.mountpriority.CompareTo(b.mountpriority));
                return tlks;
            }).ContinueWithOnUIThread(prevTask =>
            {
                ME3TLKItems.ReplaceAll(prevTask.Result);
                SelectLoadedTLKsME3();
                IsBusy = false;
                if (prevTask.Result.Count > 0 && ME3TLKItems.Any(x => x.selectedForLoad))
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
            ME2TalkFiles.LoadedTlks.Clear();
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

        private void AutoFindTLKME1()
        {
            BusyText = "Scanning for Mass Effect TLK files";
            IsBusy = true;
            ME1TalkFiles.LoadedTlks.Clear();
            Task.Run(() =>
            {
                var tlkfiles = Directory.EnumerateFiles(ME1Directory.DefaultGamePath, "*Tlk*", SearchOption.AllDirectories).ToList();
                var tlks = new List<LoadedTLK>();
                foreach (string tlk in tlkfiles)
                {
                    using IMEPackage upk = MEPackageHandler.OpenME1Package(tlk);
                    foreach (ExportEntry exp in upk.Exports.Where(exp => exp.ClassName == "BioTlkFile"))
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

        private void AutoFindTLKLE3()
        {
            BusyText = "Scanning for Mass Effect 3 Legendary Edition TLK files";
            IsBusy = true;
            LE3TalkFiles.LoadedTlks.Clear();
            Task.Run(() =>
            {
                var tlkmountmap = new List<(string, int)>();
                var tlks = Directory.EnumerateFiles(LE3Directory.BioGamePath, "*.tlk", SearchOption.AllDirectories).Select(x => new LoadedTLK(x, false)).ToList();
                tlks.ForEach(x => x.LoadMountPriority());
                tlks.Sort((a, b) => a.mountpriority.CompareTo(b.mountpriority));
                return tlks;
            }).ContinueWithOnUIThread(prevTask =>
            {
                LE3TLKItems.ReplaceAll(prevTask.Result);
                SelectLoadedTLKsLE3();
                IsBusy = false;
                if (prevTask.Result.Count > 0 && LE3TLKItems.Any(x => x.selectedForLoad))
                {
                    PromptForReload(MEGame.LE3);
                }
            });
            bSaveNeededLE3 = true;
        }

        private void AutoFindTLKLE2()
        {
            BusyText = "Scanning for Mass Effect 2 Legendary Edition TLK files";
            IsBusy = true;
            LE2TalkFiles.LoadedTlks.Clear();
            Task.Run(() =>
            {
                var tlkmountmap = new List<(string, int)>();
                var tlks = Directory.EnumerateFiles(LE2Directory.BioGamePath, "*.tlk", SearchOption.AllDirectories).Select(x => new LoadedTLK(x, false)).ToList();
                tlks.ForEach(x => x.LoadMountPriority());
                tlks.Sort((a, b) => a.mountpriority.CompareTo(b.mountpriority));
                return tlks;
            }).ContinueWithOnUIThread(prevTask =>
            {
                LE2TLKItems.ReplaceAll(prevTask.Result);
                SelectLoadedTLKsLE2();
                IsBusy = false;
                if (prevTask.Result.Count > 0 && LE2TLKItems.Any(x => x.selectedForLoad))
                {
                    PromptForReload(MEGame.LE2);
                }
            });
            bSaveNeededLE2 = true;
        }

        private void AutoFindTLKLE1()
        {
            BusyText = "Scanning for Mass Effect 1 Legendary Edition TLK files";
            IsBusy = true;
            LE1TalkFiles.LoadedTlks.Clear();
            Task.Run(() =>
            {
                var tlkfiles = Directory.EnumerateFiles(LE1Directory.DefaultGamePath, "Startup_*", SearchOption.AllDirectories).ToList();
                if (Directory.Exists(LE1Directory.DLCPath))
                {
                    tlkfiles.AddRange(Directory
                        .EnumerateFiles(LE1Directory.DLCPath, "*Tlk*", SearchOption.AllDirectories).ToList());
                }

                var tlks = new List<LoadedTLK>();
                foreach (string tlk in tlkfiles)
                {
                    using (IMEPackage upk = MEPackageHandler.OpenLE1Package(tlk))
                    {
                        foreach (ExportEntry exp in upk.Exports.Where(exp => exp.ClassName == "BioTlkFile"))
                        {
                            tlks.Add(new LoadedTLK(tlk, exp.UIndex, exp.ObjectName, false));
                        }
                    }
                    //these startup files are LARGE. If they aren't forcibly cleared out every iteration, memory usage jumps to > 3 GBs
                    MemoryAnalyzer.ForceFullGC();
                }
                MemoryAnalyzer.ForceFullGC(true);
                return tlks;
            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.Exception != null)
                {
                    MessageBox.Show($@"Error occurred finding TLKs: {prevTask.Exception.FlattenException()}");
                }
                else
                {
                    LE1TLKItems.ReplaceAll(prevTask.Result);
                    SelectLoadedTLKsLE1();
                    IsBusy = false;
                    if (prevTask.Result.Count > 0 && LE1TLKItems.Any(x => x.selectedForLoad))
                    {
                        PromptForReload(MEGame.LE1);
                    }
                }
            });
            bSaveNeededLE1 = true;
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
                    case MEGame.LE1:
                        LE1ReloadTLKStrings();
                        break;
                    case MEGame.LE2:
                        LE2ReloadTLKStrings();
                        break;
                    case MEGame.LE3:
                        LE3ReloadTLKStrings();
                        break;
                }
            }
        }

        private static bool ME1GamePathExists()
        {
            return ME1Directory.DefaultGamePath != null && Directory.Exists(ME1Directory.DefaultGamePath);
        }

        private static bool ME2BIOGamePathExists()
        {
            return ME2Directory.BioGamePath != null && Directory.Exists(ME2Directory.BioGamePath);
        }

        private static bool ME3BIOGamePathExists()
        {
            return ME3Directory.BioGamePath != null && Directory.Exists(ME3Directory.BioGamePath);
        }

        private static bool LE1GamePathExists()
        {
            return LE1Directory.DefaultGamePath != null && Directory.Exists(LE1Directory.DefaultGamePath);
        }

        private static bool LE2BIOGamePathExists()
        {
            return LE2Directory.BioGamePath != null && Directory.Exists(LE2Directory.BioGamePath);
        }

        private static bool LE3BIOGamePathExists()
        {
            return LE3Directory.BioGamePath != null && Directory.Exists(LE3Directory.BioGamePath);
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

        private void ME3TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME3();
        }

        private void ME2TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME2();
        }

        private void ME1TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME1();
        }

        private void LE3TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsLE3();
        }

        private void LE2TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsLE2();
        }

        private void LE1TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsLE1();
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

        private void SelectLoadedTLKsLE1()
        {
            var tlkLang = ((ComboBoxItem)LE1TLKLangCombobox.SelectedItem).Content.ToString();
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
                tlkLang += ".pcc";
            }
            else
            {
                tlkLang = "INT.pcc";
            }

            foreach (LoadedTLK tlk in LE1TLKItems)
            {
                tlk.selectedForLoad = ((tlk.exportName.EndsWith("_M") && male) || (!tlk.exportName.EndsWith("_M") && !male)) && tlk.tlkPath.EndsWith(tlkLang);
            }
        }

        private void SelectLoadedTLKsLE3()
        {
            Debug.WriteLine("Loaded selected TLK.");
            var tlkLang = ((ComboBoxItem)LE3TLKLangCombobox.SelectedItem).Content.ToString();
            Debug.WriteLine("Content to string done");
            tlkLang += ".tlk";
            foreach (LoadedTLK tlk in LE3TLKItems)
            {
                tlk.selectedForLoad = tlk.tlkPath.EndsWith(tlkLang);
            }
            Debug.WriteLine("loaded");
        }

        private void SelectLoadedTLKsLE2()
        {
            var tlkLang = ((ComboBoxItem)LE2TLKLangCombobox.SelectedItem).Content.ToString();
            tlkLang += ".tlk";
            foreach (LoadedTLK tlk in LE2TLKItems)
            {
                tlk.selectedForLoad = tlk.tlkPath.EndsWith(tlkLang);
            }
        }

        private async void TLKManager_Closing(object sender, CancelEventArgs e)
        {
            if(bSaveNeededME1 || bSaveNeededME2 || bSaveNeededME3 || bSaveNeededLE1 || bSaveNeededLE2 || bSaveNeededLE3)
            {
                var confirm = MessageBox.Show("You are exiting the manager without saving the changes to the TLK list(s). Save now?", "TLK Manager", MessageBoxButton.YesNoCancel);
                if(confirm == MessageBoxResult.Yes)
                {
                    if(bSaveNeededME1)
                    {
                        ME1TalkFiles.LoadedTlks.Clear();
                        await Task.Run(() => ME1ReloadTLKStringsAsync(ME1TLKItems.Where(x => x.selectedForLoad).ToList()));
                    }
                    if(bSaveNeededME2)
                    {
                        ME2TalkFiles.LoadedTlks.Clear();
                        await Task.Run(() => ME2ReloadTLKStringsAsync(ME2TLKItems.Where(x => x.selectedForLoad).ToList()));
                    }
                    if (bSaveNeededME3)
                    {
                        ME3TalkFiles.LoadedTlks.Clear();
                        await Task.Run(() => ME3ReloadTLKStringsAsync(ME3TLKItems.Where(x => x.selectedForLoad).ToList()));
                    }
                    if(bSaveNeededLE1)
                    {
                        LE1TalkFiles.LoadedTlks.Clear();
                        await Task.Run(() => LE1ReloadTLKStringsAsync(LE1TLKItems.Where(x => x.selectedForLoad).ToList()));
                    }
                    if(bSaveNeededLE2)
                    {
                        LE2TalkFiles.LoadedTlks.Clear();
                        await Task.Run(() => LE2ReloadTLKStringsAsync(LE2TLKItems.Where(x => x.selectedForLoad).ToList()));
                    }
                    if (bSaveNeededLE3)
                    {
                        LE3TalkFiles.LoadedTlks.Clear();
                        await Task.Run(() => LE3ReloadTLKStringsAsync(LE3TLKItems.Where(x => x.selectedForLoad).ToList()));
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

        void LE1CollectionChangedEventHandler(object items, NotifyCollectionChangedEventArgs e)
        {
            if (items != null)
            {
                foreach (LoadedTLK t in LE1TLKItems)
                {
                    t.PropertyChanged += LE1PropertyChangeHandler;
                }
            }
        }

        public void LE1PropertyChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            bSaveNeededLE1 = true;
        }

        void LE2CollectionChangedEventHandler(object items, NotifyCollectionChangedEventArgs e)
        {
            if (items != null)
            {
                foreach (LoadedTLK t in LE2TLKItems)
                {
                    t.PropertyChanged += LE2PropertyChangeHandler;
                }
            }
        }

        public void LE2PropertyChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            bSaveNeededLE2 = true;
        }

        void LE3CollectionChangedEventHandler(object items, NotifyCollectionChangedEventArgs e)
        {
            if (items != null)
            {
                foreach (LoadedTLK t in LE3TLKItems)
                {
                    t.PropertyChanged += LE3PropertyChangeHandler;
                }
            }
        }

        public void LE3PropertyChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            bSaveNeededLE3 = true;
        }

        /// <summary>
        /// Looks up current loaded file game ID and returns appropriate string reference.
        /// </summary>
        /// <param name="stringRefID">The StringRef ID you want to resolve</param>
        /// <param name="game">Which game to look up TLK data for</param>
        /// <param name="me1Package">ME1 package to parse. You can pass in null if you're not using a ME1 Pacakge, or don't have a reference to one you need</param>
        /// <returns></returns>
        public static string GlobalFindStrRefbyID(int stringRefID, MEGame game, IMEPackage me1Package = null)
        {
            if (stringRefID <= 0)
            {
                return null;
            }

            return game switch
            {
                MEGame.ME1 => ME1TalkFiles.FindDataById(stringRefID, me1Package),
                MEGame.ME2 => ME2TalkFiles.FindDataById(stringRefID),
                MEGame.ME3 => ME3TalkFiles.FindDataById(stringRefID),
                MEGame.LE1 => LE1TalkFiles.FindDataById(stringRefID, me1Package),
                MEGame.LE2 => LE2TalkFiles.FindDataById(stringRefID),
                MEGame.LE3 => LE3TalkFiles.FindDataById(stringRefID),
                _ => "UDK String Refs Not Supported"
            };
        }

        /// <summary>
        /// Looks up current loaded file game ID and returns appropriate string reference.
        /// </summary>
        /// <param name="stringRefID">The StringRef ID you want to resolve</param>
        /// <param name="package">The package that is being looked up.  Used to determine the game and pass on local TLKs.</param>
        /// <returns></returns>
        public static string GlobalFindStrRefbyID(int stringRefID, IMEPackage package) => GlobalFindStrRefbyID(stringRefID, package.Game, package);
    }
}
