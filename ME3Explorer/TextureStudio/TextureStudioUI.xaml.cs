using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using DocumentFormat.OpenXml.Presentation;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Compression;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer.TextureStudio
{
    /// <summary>
    /// Interaction logic for TextureStudioUI.xaml
    /// </summary>
    public partial class TextureStudioUI : NotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtendedWPF<TextureMapMemoryEntry> AllTreeViewNodes { get; } = new ObservableCollectionExtendedWPF<TextureMapMemoryEntry>();

        #region Variables

        private TextureMapMemoryEntryWPF _selectedItem;
        public TextureMapMemoryEntryWPF SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnSelectedItemChanged();
                }

            }
        }

        private TextureMapPackageEntry _selectedInstance;
        public TextureMapPackageEntry SelectedInstance
        {
            get => _selectedInstance;
            set
            {
                if (SetProperty(ref _selectedInstance, value))
                {
                    OnSelectedInstanceChanged();
                }

            }
        }

        private void OnSelectedInstanceChanged()
        {
            if (SelectedInstance == null)
            {
                TextureViewer_ExportLoader.UnloadExport();
            }
            else
            {
                var package = MEPackageHandler.OpenMEPackage(Path.Combine(SelectedFolder, SelectedInstance.RelativePackagePath));
                TextureViewer_ExportLoader.LoadExport(package.GetUExport(SelectedInstance.UIndex));
                AddPackageToCache(package);
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _busyProgressIndeterminate = true;
        public bool BusyProgressIndeterminate
        {
            get => _busyProgressIndeterminate;
            set => SetProperty(ref _busyProgressIndeterminate, value);
        }

        private string _selectedFolder;
        public string SelectedFolder { get => _selectedFolder; set => SetProperty(ref _selectedFolder, value); }

        private string _busyText;
        public string BusyText { get => _busyText; set => SetProperty(ref _busyText, value); }

        private string _busyHeader;
        public string BusyHeader { get => _busyHeader; set => SetProperty(ref _busyHeader, value); }

        private long _busyProgressMaximum = 1;
        public long BusyProgressMaximum
        {
            get => _busyProgressMaximum;
            set => SetProperty(ref _busyProgressMaximum, value);
        }

        private long _busyProgressValue;

        public long BusyProgressValue
        {
            get => _busyProgressValue;
            set
            {
                if (SetProperty(ref _busyProgressValue, value))
                {
                    BusyProgressIndeterminate = false;
                }
            }
        }

        private bool ScanCanceled;

        private List<IMEPackage> CachedPackages = new List<IMEPackage>(10);

        private void AddPackageToCache(IMEPackage package)
        {
            // Move to end of the list.
            CachedPackages.Remove(package);
            CachedPackages.Add(package);

            if (CachedPackages.Count > 10)
            {
                var packageToRelease = CachedPackages[0];
                CachedPackages.RemoveAt(0); //Remove the first item
                packageToRelease.Dispose();
            }
        }
        #endregion



        public TextureStudioUI()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            BusyCancelCommand = new GenericCommand(CancelScan, () => IsBusy);
            ScanFolderCommand = new GenericCommand(ScanFolder, CanScanFolder);
        }

        private void CancelScan()
        {
            ScanCanceled = true;
        }

        public GenericCommand BusyCancelCommand { get; set; }

        private bool CanScanFolder() => !IsBusy;

        private void OnSelectedItemChanged()
        {

        }

        private void ScanFolder()
        {
            var dlg = new CommonOpenFileDialog("Select a folder containing package files to work on")
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += ScanFolderThread;
                bw.RunWorkerCompleted += (sender, args) =>
                {
                    ScanCanceled = false;
                    IsBusy = false;
                };
                IsBusy = true;
                SelectedFolder = dlg.FileName;
                bw.RunWorkerAsync();

            }
        }

        private TextureMapMemoryEntryWPF MemoryEntryGeneratorWPF(IEntry entry)
        {
            return new TextureMapMemoryEntryWPF(entry);
        }

        private void ScanFolderThread(object sender, DoWorkEventArgs e)
        {
            // Mapping of full paths to their entries
            BusyHeader = @"Calculating texture map";
            Dictionary<string, TextureMapMemoryEntry> entries = new Dictionary<string, TextureMapMemoryEntry>();
            var packageFiles = Directory.GetFiles(SelectedFolder, "*.*", SearchOption.AllDirectories).Where(x => x.RepresentsPackageFilePath()).ToList();
            var tfcs = Directory.GetFiles(SelectedFolder, "*.tfc", SearchOption.AllDirectories).ToList();
            BusyProgressValue = 0;
            BusyProgressMaximum = packageFiles.Count;

            // Pass 1: Find all unique memory texture paths
            foreach (var p in packageFiles)
            {
                BusyText = $@"Scanning {Path.GetFileName(p)}";
                if (ScanCanceled) break;
                using var package = MEPackageHandler.OpenMEPackage(p);
                var textures = package.Exports.Where(x => x.IsTexture());
                foreach (var t in textures)
                {
                    if (ScanCanceled) break;
                    ParseTexture(t, entries, tfcs, MemoryEntryGeneratorWPF);
                }
                BusyProgressValue++;
            }

            // Pass 2: Find any unique items among the unique paths (e.g. CRC not equal to other members of same entry)
            var allTextures = AllTreeViewNodes.OfType<TextureMapMemoryEntryWPF>().SelectMany(x => x.GetAllTextureEntries());

            // Pass 3: Sort
            BusyText = "Sorting tree";
            AllTreeViewNodes.OfType<TextureMapMemoryEntryWPF>().ForEach(x => x.IsExpanded = false); // Collapse the top branches
            SortNodes(AllTreeViewNodes);

            // Pass 4: Find items that have matching CRCs across memory entries
            Dictionary<uint, List<TextureMapMemoryEntry>> crcMap = new Dictionary<uint, List<TextureMapMemoryEntry>>();
            foreach (var t in allTextures)
            {
                if (t.Instances.Any())
                {
                    var firstCRC = t.Instances[0].CRC;

                    var areAllEqualCRC = t.Instances.All(x => x.CRC == firstCRC);
                    if (!areAllEqualCRC)
                    {
                        // Some textures are not the same across the same entry!
                        // This will lead to weird engine behavior as memory is dumped and newly loaded data is different
                        Debug.WriteLine(@"UNMATCHED CRCSSSSSSSSSSSSSSSSSSSSSSSSSSS");
                        SetUnmatchedCRC(t, true);
                    }
                    else
                    {
                        if (!crcMap.TryGetValue(firstCRC, out var list))
                        {
                            list = new List<TextureMapMemoryEntry>();
                            crcMap[firstCRC] = list;
                        }

                        list.Add(t);
                    }
                }
            }

            BusyProgressIndeterminate = true;

        }

        private void SetUnmatchedCRC(TextureMapMemoryEntry memEntry, bool hasUnmatchedCRC)
        {
            memEntry.HasUnmatchedCRCs = hasUnmatchedCRC;
            TextureMapMemoryEntry parent = memEntry.Parent;
            while(parent != null)
            {
                parent.HasUnmatchedCRCs = hasUnmatchedCRC || parent.Children.Any(x => x.HasUnmatchedCRCs); // If one is corrected, another may exist under this tree.
                parent = parent.Parent;
            }
        }

        private void SortNodes(ObservableCollectionExtended<TextureMapMemoryEntry> branch)
        {
            foreach (var node in branch)
            {
                SortNodes(node.Children);
            }

            branch.Sort(x => x.ObjectName);
        }

        public GenericCommand ScanFolderCommand { get; set; }
        private void LTM(object sender, RoutedEventArgs e)
        {
            MEMTextureMap.LoadTextureMap(MEGame.ME3);
        }

        // BELOW CODE IS NOT TIED TO WPF
        // PLEASE KEEP IT THIS WAY IN THE EVENT
        // IT MOVES TO THE LIB

        /// <summary>
        /// Parses a Texture object
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <param name="textureMapMemoryEntries"></param>
        /// <returns></returns>
        private TextureMapMemoryEntry ParseTexture(ExportEntry exportEntry, Dictionary<string, TextureMapMemoryEntry> textureMapMemoryEntries, List<string> additionalTFCs, Func<IEntry, TextureMapMemoryEntry> generatorDelegate)
        {
            var parent = EnsureParent(exportEntry, textureMapMemoryEntries, additionalTFCs, generatorDelegate);

            if (!textureMapMemoryEntries.TryGetValue(exportEntry.InstancedFullPath, out var memoryEntry))
            {
                memoryEntry = generatorDelegate(exportEntry);
                memoryEntry.Parent = parent;
                textureMapMemoryEntries[exportEntry.InstancedFullPath] = memoryEntry;
                parent?.Children.Add(memoryEntry);
            }

            // Add our instance to the memory entry
            memoryEntry.Instances.Add(new TextureMapPackageEntry(SelectedFolder, exportEntry, additionalTFCs));
            return memoryEntry;
        }

        /// <summary>
        /// Creates all parents of the specified export in the texture tree, if necessary
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <param name="textureMapMemoryEntries"></param>
        /// <returns></returns>
        private TextureMapMemoryEntry EnsureParent(ExportEntry exportEntry, Dictionary<string, TextureMapMemoryEntry> textureMapMemoryEntries, List<string> additionalTFCs, Func<IEntry, TextureMapMemoryEntry> generatorDelegate)
        {
            IEntry parentT = exportEntry;
            List<IEntry> parents = new List<IEntry>();
            while (parentT.HasParent)
            {
                parents.Insert(0, parentT.Parent);
                parentT = parentT.Parent;
            }


            TextureMapMemoryEntry lastParent = null;
            for (int i = 0; i < parents.Count; i++)
            {
                var p = parents[i];
                if (!textureMapMemoryEntries.TryGetValue(p.InstancedFullPath, out lastParent))
                {
                    if (p.IsTexture() && p is ExportEntry pe)
                    {
                        // Parent is texture. Normally this doesn't occur but devs be devs
                        lastParent = ParseTexture(pe, textureMapMemoryEntries, additionalTFCs, generatorDelegate);
                    }
                    else
                    {
                        // Parent doesn't exist, create
                        lastParent = generatorDelegate(p);
                        lastParent.Parent = i > 0 ? textureMapMemoryEntries[parents[i - 1].InstancedFullPath] : null;
                        // Set the parent child
                        lastParent.Parent?.Children.Add(lastParent);
                        if (lastParent.Parent == null)
                        {
                            AllTreeViewNodes.Add(lastParent); //It's a new root node
                        }
                    }

                    textureMapMemoryEntries[p.InstancedFullPath] = lastParent;
                }

            }

            return lastParent;
        }

        private void TSTR(object sender, RoutedEventArgs e)
        {
            DLCPackage dpackage = new DLCPackage(@"Z:\ME3-Backup\BIOGame\DLC\DLC_CON_APP01\CookedPCConsole\Default.sfar");

            var testFiles = dpackage.Files.Where(x => x.FileName.Contains(@"BioH_"));
            foreach (var tf in testFiles)
            {
                var decompressed = dpackage.DecompressEntry(tf);
                var package = MEPackageHandler.OpenMEPackageFromStream(decompressed, tf.FileName);
                foreach (var f in package.Exports.Where(x => x.IsTexture()))
                {
                    var t2d = ObjectBinary.From<UTexture2D>(f);
                    var cacheName = f.GetProperty<NameProperty>(@"TextureFileCacheName");
                    if (cacheName != null && cacheName.Value == @"Textures_DLC_CON_APP01")
                    {
                        var cacheEntry = dpackage.FindFileEntry(cacheName.Value + @".tfc");
                        foreach (var extTex in t2d.Mips.Where(x => !x.IsLocallyStored))
                        {
                            var textureData = dpackage.ReadFromEntry(cacheEntry, extTex.DataOffset, extTex.UncompressedSize);

                        }
                    }
                }
            }
        }

        private void SelectedTreeNodeChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedItem = e.NewValue as TextureMapMemoryEntryWPF;
            //if (SelectedItem )
        }
    }
}
