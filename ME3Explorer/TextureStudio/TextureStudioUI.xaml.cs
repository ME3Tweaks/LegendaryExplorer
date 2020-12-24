using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MassEffectModder.Images;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.Unreal.Classes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3Explorer.TextureStudio
{
    /// <summary>
    /// Interaction logic for TextureStudioUI.xaml
    /// </summary>
    public partial class TextureStudioUI : NotifyPropertyChangedWindowBase, IRecents
    {
        public ObservableCollectionExtendedWPF<TextureMapMemoryEntry> AllTreeViewNodes { get; } = new ObservableCollectionExtendedWPF<TextureMapMemoryEntry>();
        public ObservableCollectionExtendedWPF<string> ME1MasterTexturePackages { get; } = new ObservableCollectionExtendedWPF<string>();
        private Dictionary<uint, MEMTextureMap.TextureMapEntry> VanillaTextureMap { get; set; }

        #region Variables

        private MEGame _currentStudioGame;
        public MEGame CurrentStudioGame
        {
            get => _currentStudioGame;
            set => SetProperty(ref _currentStudioGame, value);
        }

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

        public string SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (SetProperty(ref _selectedFolder, value))
                {
                    OnSelectedFolderChanged();
                }
            }
        }

        private string _statusText;
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

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
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileName =>
            {
                SelectedFolder = fileName;
                BeginScan();
            });

        }

        #region Command loading
        public GenericCommand RemoveAllEmptyMipsCommand { get; set; }
        public GenericCommand ME1UpdateMasterPointersCommand { get; set; }
        public GenericCommand ME1CreateNewMasterPackageCommand { get; set; }
        public GenericCommand BusyCancelCommand { get; set; }
        public GenericCommand ScanFolderCommand { get; set; }
        public GenericCommand ChangeAllInstancesTextureCommand { get; set; }
        public GenericCommand OpenInstanceInPackageEditorCommand { get; set; }

        private void LoadCommands()
        {
            BusyCancelCommand = new GenericCommand(CancelScan, () => IsBusy);
            ScanFolderCommand = new GenericCommand(ScanFolder, CanScanFolder);

            RemoveAllEmptyMipsCommand = new GenericCommand(RemoveAllEmptyMips, CanRemoveEmptyMips);
            ME1UpdateMasterPointersCommand = new GenericCommand(ME1UpdateMasterPointers, CanUpdatePointers);
            ME1CreateNewMasterPackageCommand = new GenericCommand(ME1CreateNewMasterPackage, CanCreateNewMasterPackage);

            ChangeAllInstancesTextureCommand = new GenericCommand(ChangeAllInstances, CanChangeAllInstances);
            OpenInstanceInPackageEditorCommand = new GenericCommand(OpenInstanceInPackEd, () => SelectedInstance != null);
        }

        private void OpenInstanceInPackEd()
        {
            PackageEditorWPF p = new PackageEditorWPF();
            p.Show();
            p.LoadFile(Path.Combine(SelectedFolder, SelectedInstance.RelativePackagePath), SelectedInstance.UIndex);
            p.Activate(); //bring to front   
        }


        private bool CanChangeAllInstances() => SelectedItem != null && SelectedItem.Instances.Any();

        private void ChangeAllInstances()
        {
            OpenFileDialog selectDDS = new OpenFileDialog
            {
                Title = "Select texture file",
                Filter = "Texture (DDS PNG BMP TGA)|*.dds;*.png;*.bmp;*.tga"
            };
            var result = selectDDS.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Image image;
                try
                {
                    image = new Image(selectDDS.FileName);
                }
                catch (TextureSizeNotPowerOf2Exception)
                {
                    MessageBox.Show("The width and height of a texture must both be a power of 2\n" +
                                    "(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096)", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (CurrentStudioGame == MEGame.ME1)
                {
                    // Oh boy.........

                    // Ingest the texture
                    var generateMips = SelectedItem.Instances.Any(x => x.NumMips > 1);
                    if (generateMips)
                    {
                        // It has mips. Generate mips for our new texture
                        image.correctMips(SelectedItem.Instances[0].PixelFormat);
                    }

                    var requiresExternalStorage = image.mipMaps.Count > 6; // We must store these externally

                    // TODO: Check if there is a vanilla CRC that matches
                    // and offer to use that instead, saving memory and disk space

                    if (requiresExternalStorage)
                    {
                        var master = SelectMasterPackage();
                        if (master == null) return; // No package was selected. We cannot continue

                        var lodGroup = "TEXTUREGROUP_World";
                        var masterExport = MasterTextureSelector.GenerateNewMasterTextureExport(master, 0, "TestName", SelectedItem.Instances[0].PixelFormat, lodGroup, selectDDS.FileName, image);

                        // The package must be saved so the offsets are corrected. This will break all sorts of things for sure, requiring a global repointing operation on the workspace
                        master.Save();

                        foreach (var inst in SelectedItem.Instances)
                        {
                            using var sPackage = MEPackageHandler.OpenMEPackage(Path.Combine(SelectedFolder, inst.RelativePackagePath));
                            CorrectMasterPackagePathing(sPackage, inst, masterExport);
                            RepointME1SlaveInstance(sPackage, inst, masterExport);
                            sPackage.Save();
                        }
                    }
                }
                else
                {
                    // Perform texture replacement on one instance.
                    // Copy the properties and binary to the others
                }
            }
        }

        /// <summary>
        /// Corrects the instance to have the correct package file structure for the texture to find it's master
        /// </summary>
        /// <param name="sPackage"></param>
        /// <param name="inst"></param>
        /// <param name="masterExport"></param>
        private void CorrectMasterPackagePathing(IMEPackage sPackage, TextureMapPackageEntry inst, ExportEntry masterExport)
        {
            var sExp = sPackage.GetUExport(inst.UIndex);
            
            var requiredTopLevelPackageExportName = Path.GetFileNameWithoutExtension(masterExport.FileRef.FilePath);

            ExportEntry masterPackageExport = sPackage.Exports.FirstOrDefault(x => x.ClassName == @"Package" && x.InstancedFullPath == requiredTopLevelPackageExportName);
            if (masterPackageExport == null)
            {
                // Must be created
                masterPackageExport = ExportCreator.CreatePackageExport(sPackage, requiredTopLevelPackageExportName);
            }

            // Todo: Support subpackage folders
            
            sExp.idxLink = masterPackageExport.UIndex;
            sExp.ObjectName = masterExport.ObjectName;
            // Todo: Support indexing? 
        }

        private void RepointME1SlaveInstance(IMEPackage slavePackage, TextureMapPackageEntry inst, ExportEntry masterExport)
        {
            var instExp = slavePackage.GetUExport(inst.UIndex);
            
            // Adjust the export so it is aligned to the master
            var mastProps = masterExport.GetProperties();
            instExp.WriteProperties(mastProps);

            var masterInfo = ObjectBinary.From<UTexture2D>(masterExport);
            var slaveInfo = ObjectBinary.From<UTexture2D>(instExp);
            
            slaveInfo.Mips.Clear();
            foreach (var mm in masterInfo.Mips)
            {
                var tmip = new UTexture2D.Texture2DMipMap();
                tmip.CompressedSize = mm.CompressedSize;
                tmip.UncompressedSize = mm.UncompressedSize;
                tmip.SizeX = mm.SizeX;
                tmip.SizeY = mm.SizeY;
                if (mm.StorageType.HasFlag(StorageTypes.pccLZO) || mm.StorageType.HasFlag(StorageTypes.pccZlib))
                {
                    // It's compressed in master. In slave we must set this to ext
                    tmip.DataOffset = mm.DataOffset;
                    // If it's not, we'll have to store the mip entry start offset on read so we can calculate it later.
                    if (mm.StorageType.HasFlag(StorageTypes.pccLZO))
                    {
                        tmip.StorageType = StorageTypes.extLZO;
                    }
                    else
                    {
                        tmip.StorageType = StorageTypes.extZlib;
                    }

                    tmip.Mip = new byte[0]; // No actual data
                }
                else
                {
                    tmip.DataOffset = 0; // Doesn't actually get read by export
                    tmip.Mip = mm.Mip;
                    tmip.StorageType = StorageTypes.pccUnc;
                }

                slaveInfo.Mips.Add(tmip);
            }
            
            instExp.WriteBinary(slaveInfo);
        }

        private IMEPackage SelectMasterPackage()
        {
            var selectedItem = InputComboBoxWPF.GetValue(this, "Select a master package that will store mips greater than 64 (in any dimension). If none are available, add a master package first in the menu.",
                "Select master package",
                ME1MasterTexturePackages.Select(x => x.Substring(SelectedFolder.Length + 1)));
            if (!string.IsNullOrWhiteSpace(selectedItem))
            {
                return MEPackageHandler.OpenMEPackage(Path.Combine(SelectedFolder, selectedItem));
            }

            return null;
        }

        private void RepointME1Instances(Image newImage)
        {

        }

        #endregion

        #region Command methods

        private void ME1CreateNewMasterPackage()
        {
            SaveFileDialog sfd = new SaveFileDialog()
            {
                Filter = "ME1 texture package files|*.upk",
                Title = "Select location to save package",
                FileName = "Textures_Master_",
                InitialDirectory = SelectedFolder
            };

            var result = sfd.ShowDialog(this);
            if (result.HasValue && result.Value)
            {
                // filename must start with:
                // Textures_Master_
                if (!Path.GetFileName(sfd.FileName).StartsWith(@"Textures_Master_"))
                {
                    MessageBox.Show(@"All newly created ME1 texture master files must start with 'Textures_Master_'. Typically you will also want to include your DLC name as well to ensure uniqueness.");
                    return;
                }

                MEPackageHandler.CreateAndSavePackage(sfd.FileName, MEGame.ME1);
                ME1MasterTexturePackages.Add(sfd.FileName); // Todo: Make sure this is unique and within the mod folder
                Texture2D.AdditionalME1MasterTexturePackages.Add(sfd.FileName); // TODO: THIS NEEDS CLEANED UP AND MANAGED IN TEXTURE2D.CS
            }

        }

        private void RemoveAllEmptyMips()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (sender, args) =>
            {
                var allTextures = AllTreeViewNodes.OfType<TextureMapMemoryEntryWPF>().SelectMany(x => x.GetAllTextureEntries()).ToList();
                var texturesWithEmptyMips = allTextures.Where(x => x.Instances.Any(x => x.NumEmptyMips > 0));

                Dictionary<string, List<int>> exportMap = new Dictionary<string, List<int>>();
                foreach (var t in texturesWithEmptyMips)
                {
                    foreach (var instance in t.Instances)
                    {
                        if (instance.NumEmptyMips > 0)
                        {
                            // Add to list
                            if (!exportMap.TryGetValue(instance.RelativePackagePath, out var uindexes))
                            {
                                uindexes = new List<int>();
                                exportMap[instance.RelativePackagePath] = uindexes;
                            }

                            uindexes.Add(instance.UIndex);
                        }
                    }
                }

                foreach (var mpMap in exportMap)
                {
                    foreach (var item in mpMap.Value)
                    {
                        Debug.WriteLine($@"Removing empty mips in {mpMap.Key}, uindex {item}");
                    }
                }
            };
            bw.RunWorkerCompleted += (sender, args) =>
            {
                IsBusy = false;
                ScanFolder();
            };
            bw.RunWorkerAsync();
        }

        private bool CanUpdatePointers()
        {
            return true;
        }

        private bool CanRemoveEmptyMips()
        {
            return true;
        }

        private void ME1UpdateMasterPointers()
        {

        }

        private bool CanCreateNewMasterPackage()
        {
            return true;
        }

        private void CancelScan()
        {
            ScanCanceled = true;
        }


        private bool CanScanFolder() => !IsBusy;

        #endregion

        #region OnPROPERTYNAMEChanged() methods
        private void OnSelectedItemChanged()
        {

        }

        private void OnSelectedFolderChanged()
        {
            StatusText = SelectedFolder != null ? $@"Operating on {SelectedFolder}" : @"Open a folder to begin working on textures";
        }

        #endregion

        #region Scanning methods
        private void ScanFolder()
        {
            var dlg = new CommonOpenFileDialog("Select a folder containing package files to work on")
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                SelectedFolder = dlg.FileName;
                BeginScan();
            }
        }

        private void BeginScan()
        {
            RecentsController.AddRecent(SelectedFolder, false);
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += ScanFolderThread;
            bw.RunWorkerCompleted += (sender, args) =>
            {
                ScanCanceled = false;
                IsBusy = false;
            };
            IsBusy = true;
            ResetUI();
            bw.RunWorkerAsync();
        }

        private void ResetUI()
        {
            foreach (var v in CachedPackages)
            {
                v.Dispose();
            }
            CachedPackages.Clear();
            AllTreeViewNodes.ClearEx();
            CurrentStudioGame = MEGame.Unknown;
            VanillaTextureMap = null;
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
                var filename = Path.GetFileName(p);
                BusyText = $@"Scanning {filename}";
                if (ScanCanceled) break;
                using var package = MEPackageHandler.OpenMEPackage(p);

                if (CurrentStudioGame != MEGame.Unknown && CurrentStudioGame != package.Game)
                {
                    // This workspace has files from multiple games!

                }
                else
                {
                    CurrentStudioGame = package.Game;
                    //VanillaTextureMap = MEMTextureMap.LoadTextureMap(CurrentStudioGame);
                }

                var textures = package.Exports.Where(x => x.IsTexture());
                foreach (var t in textures)
                {
                    if (ScanCanceled) break;
                    ParseTexture(t, entries, tfcs, MemoryEntryGeneratorWPF);
                }

                if (filename.StartsWith(ME1_MOD_MASTER_TEXTURE_PACKAGE_PREFIX))
                {
                    ME1MasterTexturePackages.Add(p);
                    Texture2D.AdditionalME1MasterTexturePackages.Add(p); // TODO: THIS NEEDS CLEANED UP AND MANAGED IN TEXTURE2D.CS
                }
                BusyProgressValue++;
            }

            // Pass 2: Find any unique items among the unique paths (e.g. CRC not equal to other members of same entry)
            var allTextures = AllTreeViewNodes.OfType<TextureMapMemoryEntryWPF>().SelectMany(x => x.GetAllTextureEntries());

            // Pass 3: Sort
            BusyText = "Sorting tree";
            foreach (var t in AllTreeViewNodes.OfType<TextureMapMemoryEntryWPF>())
            {
                // Collapse the top branches
                t.IsExpanded = false;
            }
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

        /// <summary>
        /// DO NOT CHANGE THIS
        /// This is the prefix for ME1 mod master texture packages. This naming scheme will let us identify texture masters.
        /// </summary>
        public static readonly string ME1_MOD_MASTER_TEXTURE_PACKAGE_PREFIX = "Textures_Master_";

        private void SetUnmatchedCRC(TextureMapMemoryEntry memEntry, bool hasUnmatchedCRC)
        {
            memEntry.HasUnmatchedCRCs = hasUnmatchedCRC;
            TextureMapMemoryEntry parent = memEntry.Parent;
            while (parent != null)
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

        #endregion

        #region TEXTURE MAP (NOT-WPF)
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
        #endregion

        #region Test Methods

        /// <summary>
        /// Test SFAR Texture Reading
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        #endregion

        private void SelectedTreeNodeChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedItem = e.NewValue as TextureMapMemoryEntryWPF;
            //if (SelectedItem )
        }

        public void PropogateRecentsChange(IEnumerable<string> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "TextureStudio";
    }
}
