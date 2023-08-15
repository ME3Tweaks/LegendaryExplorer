using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Wordprocessing;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Textures.Studio;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Image = LegendaryExplorerCore.Textures.Image;
using Path = System.IO.Path;

namespace LegendaryExplorer.Tools.TextureStudio
{
    /// <summary>
    /// Interaction logic for TextureStudioWindow.xaml
    /// </summary>
    public partial class TextureStudioWindow : NotifyPropertyChangedWindowBase, IRecents, IBusyUIHost
    {
        public ObservableCollectionExtendedWPF<TextureMapMemoryEntry> AllRootTreeViewNodes { get; } = new();
        public ObservableCollectionExtendedWPF<string> ME1MasterTexturePackages { get; } = new();
        private Dictionary<uint, MEMTextureMap.TextureMapEntry> VanillaTextureMap { get; set; }
        /// <summary>
        /// Can produce tokens for cancelling things.
        /// </summary>
        private CancellationTokenSource CancellationSource = new();

        private string _tfcSuffix;
        public string TFCSuffix { get => _tfcSuffix; set => SetProperty(ref _tfcSuffix, value); }

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
                using var package = MEPackageHandler.UnsafePartialLoad(Path.Combine(SelectedFolder, SelectedInstance.RelativePackagePath), x => x.InstancedFullPath == SelectedInstance.ExportPath); // do not open the full package
                TextureViewer_ExportLoader.LoadExport(package.FindExport(SelectedInstance.ExportPath));
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
        #endregion



        public TextureStudioWindow()
        {
            LoadCommands();
            InitializeComponent();
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, fileName =>
            {
                InitWorkspace(fileName);
            });
        }

        private void InitWorkspace(string workspacepath)
        {
            SelectedFolder = workspacepath;
            BeginScan();

            if (SelectedFolder != null && Path.GetFileNameWithoutExtension(SelectedFolder).StartsWith("DLC_MOD_"))
            {
                TFCSuffix = Path.GetFileNameWithoutExtension(SelectedFolder);
            }

            TextureViewer_ExportLoader?.PreviewRenderer?.SetShouldRender(true); // turn on rendering
        }

        #region Command loading
        public GenericCommand RemoveAllEmptyMipsCommand { get; set; }
        public GenericCommand ME1UpdateMasterPointersCommand { get; set; }
        public GenericCommand ME1CreateNewMasterPackageCommand { get; set; }
        public GenericCommand BusyCancelCommand { get; set; }
        public GenericCommand ScanFolderCommand { get; set; }
        public GenericCommand ChangeAllInstancesTextureCommand { get; set; }
        public GenericCommand OpenInstanceInPackageEditorCommand { get; set; }
        public GenericCommand CloseWorkspaceCommand { get; set; }
        public GenericCommand LoadTextureMapCommand { get; set; }

        private void LoadCommands()
        {
            LoadTextureMapCommand = new GenericCommand(LoadTextureMap);
            BusyCancelCommand = new GenericCommand(CancelScan, () => IsBusy);
            ScanFolderCommand = new GenericCommand(() => ScanFolder(), CanScanFolder);

            RemoveAllEmptyMipsCommand = new GenericCommand(RemoveAllEmptyMips, CanRemoveEmptyMips);
            ME1UpdateMasterPointersCommand = new GenericCommand(ME1UpdateMasterPointers, CanUpdatePointers);
            ME1CreateNewMasterPackageCommand = new GenericCommand(ME1CreateNewMasterPackage, CanCreateNewMasterPackage);

            ChangeAllInstancesTextureCommand = new GenericCommand(ChangeAllInstances, CanChangeAllInstances);
            OpenInstanceInPackageEditorCommand = new GenericCommand(OpenInstanceInPackEd, () => SelectedInstance != null);

            CloseWorkspaceCommand = new GenericCommand(CloseWorkspace, () => SelectedFolder != null);
        }

        private void LoadTextureMap()
        {

        }

        private void CloseWorkspace()
        {
            ResetUI();
            SelectedFolder = null;
        }

        private void OpenInstanceInPackEd()
        {
            var p = new PackageEditorWindow();
            p.Show();
            p.LoadFile(Path.Combine(SelectedFolder, SelectedInstance.RelativePackagePath), SelectedInstance.UIndex);
            p.Activate(); //bring to front   
        }

        private bool CanChangeAllInstances() => SelectedItem != null && SelectedItem.Instances.Any() && TFCSuffix != null && TFCSuffix.StartsWith("DLC_MOD_");

        private void ChangeAllInstances()
        {
            var selectDDS = new OpenFileDialog
            {
                Title = "Select texture file",
                Filter = "Texture (DDS PNG BMP TGA)|*.dds;*.png;*.bmp;*.tga",
                CustomPlaces = AppDirectories.GameCustomPlaces
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
                                    "(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192(LE only))", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (CurrentStudioGame == MEGame.ME1)
                {
                    // Oh boy...

                    // Ingest the texture
                    //uint crc = 0;
                    var generateMips = SelectedItem.Instances.Any(x => x.NumMips > 1);
                    if (generateMips)
                    {
                        // It has mips. Generate mips for our new texture
                        //crc = (uint)~ParallelCRC.Compute(image.mipMaps[0].data); //crc will change on non argb... not sure how to deal with this
                        image.correctMips(SelectedItem.Instances[0].PixelFormat);
                    }

                    var requiresExternalStorage = image.mipMaps.Count > 6; // We must store these externally

                    // TODO: Check if there is a vanilla CRC that matches
                    // and offer to use that instead, saving memory and disk space

                    if (requiresExternalStorage)
                    {
                        // See if there's any CRC that matches. We can just link to existing one then and not use disk space
                        //if (VanillaTextureMap.TryGetValue(crc, out var vanillaInfo))
                        //{
                        //    Debug.WriteLine("MIP ALREADY EXISTS!");
                        //}

                        var master = SelectMasterPackage();
                        if (master == null) return; // No package was selected. We cannot continue

                        var textureName = PromptDialog.Prompt(this, "Enter the new name of the texture.", "Enter texture name");

                        if (!string.IsNullOrWhiteSpace(textureName))
                        {
                            // TODO: Validate texture name!

                            var lodGroup = "TEXTUREGROUP_World";
                            var masterExport = MasterTextureSelector.GenerateNewMasterTextureExport(master, 0, textureName, SelectedItem.Instances[0].PixelFormat, lodGroup, selectDDS.FileName, image);

                            // The package must be saved so the offsets are corrected. This will break all sorts of things for sure, requiring a global repointing operation on the workspace
                            master.Save();

                            foreach (var inst in SelectedItem.Instances)
                            {
                                using var sPackage = MEPackageHandler.OpenMEPackage(Path.Combine(SelectedFolder, inst.RelativePackagePath));
                                CorrectMasterPackagePathing(sPackage, SelectedItem, inst, masterExport);
                                RepointME1SlaveInstance(sPackage, inst, masterExport);
                                sPackage.Save();
                            }
                        }
                    }
                }
                else
                {
                    // Perform texture replacement on one instance.
                    // Copy the properties and binary to the others

                    //uint crc = 0;
                    Task.Run(() =>
                    {
                        BusyText = "Replacing textures";
                        BusyProgressValue = 0;
                        BusyProgressMaximum = SelectedItem.Instances.Count;
                        BusyProgressIndeterminate = false;
                        IsBusy = true;
                        var generateMips = SelectedItem.Instances.Any(x => x.NumMips > 1);
                        if (generateMips)
                        {
                            // It has mips. Generate mips for our new texture
                            //crc = (uint)~ParallelCRC.Compute(image.mipMaps[0].data); //crc will change on non argb... not sure how to deal with this
                            image.correctMips(SelectedItem.Instances[0].PixelFormat);
                        }

                        PackageCache pc = new PackageCache();
                        Texture2D firstInstance = null;
                        UTexture2D fiBin = null;
                        for (int i = 0; i < SelectedItem.Instances.Count; i++)
                        {
                            var instance = SelectedItem.Instances[i];
                            var lPackage = pc.GetCachedPackage(Path.Combine(SelectedFolder, instance.RelativePackagePath));
                            var textureExp = lPackage.FindExport(instance.ExportPath);
                            if (i == 0)
                            {
                                // First instance
                                firstInstance = new Texture2D(textureExp);
                                firstInstance.Replace(image, textureExp.GetProperties(), selectDDS.FileName, $"Textures_{TFCSuffix}", forcedTFCPath: GetTFCPath()); // This is placeholder name for TFC name
                                fiBin = ObjectBinary.From<UTexture2D>(textureExp);
                            }
                            else
                            {
                                // others, just copy from first
                                textureExp.WriteProperties(firstInstance.Export.GetProperties());
                                textureExp.WriteBinary(fiBin);
                            }
                            BusyProgressValue++;
                        }

                        BusyProgressValue = 0;
                        BusyProgressMaximum = pc.Cache.Count;
                        BusyText = "Saving packages";
                        foreach (var p in pc.Cache.Values)
                        {
                            if (p.IsModified)
                                p.Save();
                            BusyProgressValue++;
                        }
                    }).ContinueWithOnUIThread(x =>
                    {
                        IsBusy = false;
                        TextureMapMemoryEntry tme = SelectedItem;
                        while (tme.Parent != null)
                            tme = tme.Parent;
                        BeginScanInternal(new List<TextureMapMemoryEntry>(new[] { tme }), SelectedItem.InstancedFullPath);
                    });
                }
            }
        }

        /// <summary>
        /// Gets path to where the TFC should be saved. This is a best effort system. Can return null if the folder can't be determined.
        /// </summary>
        /// <returns></returns>
        private string GetTFCPath()
        {
            if (SelectedFolder != null)
            {
                var allSubDirs = Directory.GetDirectories(SelectedFolder, "*", SearchOption.AllDirectories);
                var subDirToUse = allSubDirs.FirstOrDefault(x => x.EndsWith("CookedPCConsole") || x.EndsWith("CookedPC"));
                if (subDirToUse != null)
                {
                    return Path.Combine(subDirToUse, $"Textures_{TFCSuffix}.tfc");
                }
            }
            return null;

        }

        /// <summary>
        /// Corrects the instance to have the correct package file structure for the texture to find it's master
        /// </summary>
        /// <param name="sPackage"></param>
        /// <param name="inst"></param>
        /// <param name="masterTextureExport"></param>
        private void CorrectMasterPackagePathing(IMEPackage sPackage, TextureMapMemoryEntryWPF memEntry, TextureMapPackageEntry inst, ExportEntry masterTextureExport)
        {
            var sExp = sPackage.GetUExport(inst.UIndex);

            var requiredTopLevelPackageExportName = Path.GetFileNameWithoutExtension(masterTextureExport.FileRef.FilePath);

            ExportEntry masterPackageExport = sPackage.Exports.FirstOrDefault(x => x.ClassName == @"Package" && x.InstancedFullPath == requiredTopLevelPackageExportName);
            TextureMapMemoryEntryWPF masterPackageNode = null;
            if (masterPackageExport == null)
            {
                // Must be created
                masterPackageExport = ExportCreator.CreatePackageExport(sPackage, requiredTopLevelPackageExportName);
                masterPackageNode = new TextureMapMemoryEntryWPF(masterPackageExport);
                AllRootTreeViewNodes.Add(masterPackageNode);
            }
            else
            {
                masterPackageNode = AllRootTreeViewNodes.OfType<TextureMapMemoryEntryWPF>().FirstOrDefault(x => x.ObjectName == inst.MasterPackageName);
            }

            if (sExp.idxLink != masterPackageExport.UIndex && masterPackageNode != null)
            {
                // Detatch this node from the parent
                memEntry.Instances.Remove(inst);
                memEntry.Parent?.Children.Remove(memEntry);
                masterPackageNode.Children.Add(memEntry);
                memEntry.Parent = masterPackageNode;

                // Reattach this node to the new one

            }


            // Todo: Support subpackage folders

            sExp.idxLink = masterPackageExport.UIndex;
            sExp.ObjectName = masterTextureExport.ObjectName;


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
                var allPackages = AllRootTreeViewNodes.OfType<TextureMapMemoryEntryWPF>().SelectMany(x => x.GetAllTextureEntries()).SelectMany(y => y.Instances.Select(z => z.RelativePackagePath)).Distinct().ToList();
                BusyProgressValue = 0;
                BusyProgressMaximum = allPackages.Count;
                BusyProgressIndeterminate = false;
                foreach (var mpMap in allPackages)
                {
                    var pPath = Path.Combine(SelectedFolder, mpMap);
                    Debug.WriteLine($@"Removing empty mips in {pPath}");
                    using var package = MEPackageHandler.OpenMEPackage(pPath, forceLoadFromDisk: true);
                    foreach (var tex in package.Exports.Where(x => x.IsTexture()))
                    {
                        UTexture2D t2d = ObjectBinary.From<UTexture2D>(tex);
                        var removedCount = t2d.Mips.RemoveAll(x => x.StorageType == StorageTypes.empty);

                        if (removedCount > 0)
                        {
                            Debug.WriteLine($@"Removing empty mips from {pPath} {tex.InstancedFullPath}");
                            tex.WriteBinary(t2d);
                            tex.WriteProperty(new IntProperty(t2d.Mips.Count, "MipTailBaseIdx"));
                            //package.Save();
                        }
                    }

                    BusyProgressValue++;
                }
            };
            bw.RunWorkerCompleted += (sender, args) =>
            {
                IsBusy = false;
                ScanFolder();
            };
            BusyProgressIndeterminate = true;
            BusyHeader = "Removing empty mips";
            IsBusy = true;
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
            var bw = new BackgroundWorker();
            bw.DoWork += (sender, args) =>
            {
                var masterCache = new Dictionary<string, IMEPackage>();
                var refsToUpdate = AllRootTreeViewNodes.OfType<TextureMapMemoryEntryWPF>()
                    .SelectMany(x => x.GetAllTextureEntries())
                    .SelectMany(x => x.Instances.Where(y => y.HasExternalReferences && y.MasterPackageName != null && y.MasterPackageName.StartsWith(TextureMapGenerator.ME1_MOD_MASTER_TEXTURE_PACKAGE_PREFIX)))
                    .OrderBy(x => x.RelativePackagePath).ToList();

                IMEPackage lastOpenedSPackage = null;
                foreach (var pInstance in refsToUpdate)
                {
                    using var package = MEPackageHandler.OpenMEPackage(Path.Combine(SelectedFolder, pInstance.RelativePackagePath));
                    if (lastOpenedSPackage != package)
                    {
                        lastOpenedSPackage?.Save();
                        lastOpenedSPackage = package;
                    }

                    var sExp = package.GetUExport(pInstance.UIndex);
                    var masterPackagePath = Texture2D.AdditionalME1MasterTexturePackages.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(pInstance.MasterPackageName));
                    if (masterPackagePath != null)
                    {
                        if (!masterCache.TryGetValue(masterPackagePath, out IMEPackage masterPackage))
                        {
                            masterPackage = MEPackageHandler.OpenMEPackage(masterPackagePath, forceLoadFromDisk: true);
                            masterCache[masterPackagePath] = masterPackage;
                        }

                        // Find the master export
                        var masterInPackagePath = string.Join(".", sExp.InstancedFullPath.Split('.').Skip(1).Take(10));
                        var masterExp = masterPackage.Exports.FirstOrDefault(x => x.InstancedFullPath == masterInPackagePath);
                        Debug.WriteLine(masterInPackagePath);
                        RepointME1SlaveInstance(package, pInstance, masterExp);
                    }
                }
                lastOpenedSPackage?.Save();
            };
            bw.RunWorkerCompleted += (sender, args) =>
            {
                IsBusy = false;
            };
            BusyHeader = "Updating texture references";
            IsBusy = true;
            bw.RunWorkerAsync();
        }

        private bool CanCreateNewMasterPackage()
        {
            return true;
        }

        private void CancelScan()
        {
            CancellationSource.Cancel();
        }


        private bool CanScanFolder() => !IsBusy;

        #endregion

        #region OnPROPERTYNAMEChanged() methods
        private void OnSelectedItemChanged()
        {
            SelectedInstance = SelectedItem?.Instances.FirstOrDefault();
        }

        private void OnSelectedFolderChanged()
        {
            StatusText = SelectedFolder != null ? $@"Operating on {SelectedFolder}" : @"Open a folder to begin working on textures";
        }

        #endregion

        #region Scanning methods
        private void ScanFolder(string path = null)
        {
            if (path == null)
            {
                var dlg = new CommonOpenFileDialog("Select a folder containing package files to work on")
                {
                    IsFolderPicker = true
                };

                if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
                {
                    InitWorkspace(dlg.FileName);
                }
            }
            else
            {
                InitWorkspace(path);
            }
        }

        private void BeginScan()
        {
            RecentsController.AddRecent(SelectedFolder, false, null);
            BeginScanInternal();
        }

        private void BeginScanInternal(List<TextureMapMemoryEntry> entriesToReload = null, string nodeToSelect = null)
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += ScanFolderThread;
            bw.RunWorkerCompleted += (sender, args) =>
            {
                ScanCanceled = false;
                IsBusy = false;

                if (nodeToSelect != null && entriesToReload is { Count: 1 })
                {
                    var root = AllRootTreeViewNodes.FirstOrDefault(x => x.InstancedFullPath == entriesToReload[0].InstancedFullPath);
                    if (root != null)
                    {
                        // it's gonna be under here
                        var node = root.GetAllTextureEntries().FirstOrDefault(x => x.InstancedFullPath == nodeToSelect);
                        if (node is TextureMapMemoryEntryWPF twpf)
                        {
                            SelectEntry(twpf);
                        }
                    }

                }
            };
            IsBusy = true;
            if (entriesToReload == null)
            {
                ResetUI();
            }
            else
            {
                AllRootTreeViewNodes.RemoveRange(entriesToReload);
            }

            bw.RunWorkerAsync(entriesToReload);
        }

        private void SelectEntry(TextureMapMemoryEntryWPF twpf)
        {
            twpf.ExpandParents();
            twpf.IsSelected = true;
        }

        private void ResetUI()
        {
            AllRootTreeViewNodes.ClearEx();
            CurrentStudioGame = MEGame.Unknown;
            VanillaTextureMap = null;
        }

        private TextureMapMemoryEntryWPF MemoryEntryGeneratorWPF(IEntry entry)
        {
            return new TextureMapMemoryEntryWPF(entry);
        }


        private void ScanFolderThread(object sender, DoWorkEventArgs e)
        {
            CancellationSource = new CancellationTokenSource(); // Make new so it doesn't have old request
            //// Mapping of full paths to their entries
            //BusyHeader = @"Calculating texture map";
            //Dictionary<string, TextureMapMemoryEntry> entries = new Dictionary<string, TextureMapMemoryEntry>();
            //var packageFiles = Directory.GetFiles(SelectedFolder, "*.*", SearchOption.AllDirectories).Where(x => x.RepresentsPackageFilePath()).ToList();
            //var tfcs = Directory.GetFiles(SelectedFolder, "*.tfc", SearchOption.AllDirectories).ToList();
            //BusyProgressValue = 0;
            //BusyProgressMaximum = packageFiles.Count;

            //// Pass 1: Find all unique memory texture paths
            BusyText = "Generating texture map";

            if (e.Argument is List<TextureMapMemoryEntry> entriesToRefresh)
            {
                var entries = new Dictionary<string, TextureMapMemoryEntry>();
                TextureMapGenerator.RegenerateEntries(SelectedFolder, entriesToRefresh, entries, new Dictionary<string, uint>(), new List<string>(), MemoryEntryGeneratorWPF, addTopLevelNode, CancellationSource.Token);
            }
            else
            {
                // Generate the whole thing
                TextureMapGenerator.GenerateMapForFolder(SelectedFolder, MemoryEntryGeneratorWPF, addTopLevelNode, textureMapProgress, CancellationSource.Token);
            }

            // Pass 4: Sort
            BusyText = "Sorting tree";
            foreach (var t in AllRootTreeViewNodes.OfType<TextureMapMemoryEntryWPF>())
            {
                // Collapse the top branches
                t.IsExpanded = false;
            }
            SortNodes(AllRootTreeViewNodes);

            // 

            // Attempt to determine TFC Suffix
            foreach (var v in AllRootTreeViewNodes)
            {
                var textureInstances = v.GetAllTextureEntries();
                foreach (var memoryInstance in textureInstances)
                {
                    foreach (var packageInstance in memoryInstance.Instances)
                    {
                        if (packageInstance.RelativePackagePath.Contains(@"DLC_MOD_"))
                        {
                            var dlcFolderName = packageInstance.RelativePackagePath.Substring(packageInstance.RelativePackagePath.IndexOf(@"DLC_MOD_"));
                            var dlcIndex = dlcFolderName.IndexOf(@"\");
                            if (dlcIndex > 0)
                            {
                                dlcFolderName = dlcFolderName.Substring(0, dlcIndex);
                                TFCSuffix = dlcFolderName;
                                break;
                            }
                        }
                    }

                    if (TFCSuffix != null)
                    {
                        break;
                    }
                }
                if (TFCSuffix != null)
                {
                    break;
                }
            }


            Thread.Sleep(200); //UI will take a few moments to update so we will stall this busy overlay
            BusyProgressIndeterminate = true;
        }

        private void addTopLevelNode(TextureMapMemoryEntry obj)
        {
            AllRootTreeViewNodes.Add(obj);
        }

        private void textureMapProgress(string text, int done, int total)
        {
            BusyHeader = text;
            if (total <= 0)
            {
                BusyProgressIndeterminate = true;
            }
            else
            {
                BusyProgressIndeterminate = false;
                BusyProgressValue = done;
                BusyProgressMaximum = total;
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
                package.IsMemoryPackage = true;
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

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "TextureStudio";

        private void TextureStudioWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }
            RecentsController?.Dispose();
        }

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
    }
}
