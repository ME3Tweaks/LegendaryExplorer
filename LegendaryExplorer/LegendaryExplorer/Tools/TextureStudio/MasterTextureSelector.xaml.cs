using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using Image = LegendaryExplorerCore.Textures.Image;

namespace LegendaryExplorer.Tools.TextureStudio
{
    /// <summary>
    /// Interaction logic for MasterTextureSelector.xaml
    /// </summary>
    public partial class MasterTextureSelector : WPFBase
    {
        #region Binding Vars

        private bool _selectorMode;
        public bool SelectorMode
        {
            get => _selectorMode;
            set => SetProperty(ref _selectorMode, value);
        }

        private TreeViewEntry _selectedItem;

        public TreeViewEntry SelectedItem
        {
            get => _selectedItem;
            set
            {
                var oldIndex = _selectedItem?.UIndex;
                if (SetProperty(ref _selectedItem, value))// && !SuppressSelectionEvent)
                {
                    //if (oldIndex.HasValue && oldIndex.Value != 0 && !IsBackForwardsNavigationEvent)
                    //{
                    //    // 0 = tree root
                    //    //Debug.WriteLine("Push onto backwards: " + oldIndex);
                    //    BackwardsIndexes.Push(oldIndex.Value);
                    //    ForwardsIndexes.Clear(); //forward list is no longer valid
                    //}

                    OnSelectedItemChanged();
                }
            }
        }

        private void OnSelectedItemChanged()
        {
            if (SelectedItem?.Entry == null || !SelectedItem.Entry.IsTexture() || SelectedItem.Entry is ImportEntry)
            {
                textureViewer.UnloadExport();
            }
            else
            {
                textureViewer.LoadExport(SelectedItem.Entry as ExportEntry);
            }
        }

        public ObservableCollectionExtended<TreeViewEntry> AllTreeViewNodesX { get; } = new();

        #endregion

        public MasterTextureSelector() : base(@"ME1MasterTexturePackageEditor", true)
        {
            LoadCommands();
            InitializeComponent();
        }

        #region Commands
        public GenericCommand AddNewTextureCommand { get; set; }
        public GenericCommand SelectTextureCommand { get; set; }

        private void LoadCommands()
        {
            AddNewTextureCommand = new GenericCommand(AddNewTexture, () => Pcc != null);
            SelectTextureCommand = new GenericCommand(SelectTexture, () => Pcc != null && SelectorMode && SelectedItem != null);
        }

        private void AddNewTexture()
        {
            var textureName = PromptDialog.Prompt(this, "Enter a new name for the new texture object.", "Enter texture name");
            var linkSelector = EntrySelector.GetEntryWithNoOption<IEntry>(this, Pcc, @"Select the parent the new texture should have. If you want the new texture directly at the root, select [Package root].", x => x.ClassName == "Package");
            if (linkSelector.selectedPackageRoot || linkSelector.selectedEntry != null)
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

                    var destPixelFormat = InputComboBoxWPF.GetValue(this, @"Select the pixel format for this texture.", "Pixel format selection", Enum.GetValues(typeof(PixelFormat)).Cast<object>());
                    var lodGroup = "TEXTUREGROUP_World";
                    GenerateNewMasterTextureExport(Pcc, linkSelector.selectedPackageRoot ? 0 : linkSelector.selectedEntry.UIndex, textureName, (PixelFormat)Enum.Parse(typeof(PixelFormat), destPixelFormat), lodGroup,  selectDDS.FileName, image);
                }
            }
        }

        private void SelectTexture()
        {
            DialogResult = true;
            Close();
        }

        #endregion

        public void LoadFile(string file)
        {
            LoadMEPackage(file);
            InitializeTreeView();
        }

        public override void HandleUpdate(List<PackageUpdate> updates)
        {
        }

        private void InitializeTreeView()
        {
            IsBusy = true;
            if (Pcc == null)
            {
                return;
            }

            Task.Run(InitializeTreeViewBackground)
                .ContinueWithOnUIThread(InitializeTreeViewBackground_Completed);
        }

        private List<TreeViewEntry> InitializeTreeViewBackground()
        {
            //BusyText = "Loading " + Path.GetFileName(Pcc.FilePath);
            if (Pcc == null)
            {
                return null;
            }

            IReadOnlyList<ImportEntry> Imports = Pcc.Imports;
            IReadOnlyList<ExportEntry> Exports = Pcc.Exports;

            var rootEntry = new TreeViewEntry(null, Path.GetFileName(Pcc.FilePath)) { IsExpanded = true };

            var rootNodes = new List<TreeViewEntry> { rootEntry };
            rootNodes.AddRange(Exports.Select(t => new TreeViewEntry(t)));
            rootNodes.AddRange(Imports.Select(t => new TreeViewEntry(t)));

            //configure links
            //Order: 0 = Root, [Exports], [Imports], <extra, new stuff>
            var itemsToRemove = new List<TreeViewEntry>();
            foreach (TreeViewEntry entry in rootNodes)
            {
                if (entry.Entry != null)
                {
                    int tvLink = entry.Entry.idxLink;
                    if (tvLink < 0)
                    {
                        //import
                        //Debug.WriteLine("import tvlink " + tvLink);

                        tvLink = Exports.Count + Math.Abs(tvLink);
                        //Debug.WriteLine("Linking " + entry.Entry.GetFullPath + " to index " + tvLink);
                    }

                    TreeViewEntry parent = rootNodes[tvLink];
                    parent.Sublinks.Add(entry);
                    entry.Parent = parent;
                    itemsToRemove.Add(entry); //remove from this level as we have added it to another already
                }
            }

            // Pass 2: remove non texture items
            TrimTree(rootEntry, false);

            // Pass 3: Remove unused packages from tree by running a second trimming pass
            TrimTree(rootEntry, true);

            return new List<TreeViewEntry>(rootNodes.Except(itemsToRemove));
        }

        public bool TrimTree(TreeViewEntry entry, bool removeEmpty)
        {
            for (int i = entry.Sublinks.Count - 1; i >= 0; i--)
            {
                if (TrimTree(entry.Sublinks[i], removeEmpty))
                {
                    //Debug.WriteLine($@"Trimming {entry.Sublinks[i].Entry?.UIndex} {entry.Sublinks[i].Entry?.InstancedFullPath}  ({entry.Sublinks[i].Entry?.ClassName})");
                    entry.Sublinks.RemoveAt(i);
                }
            }

            // Am I a texture or a package?
            if (!removeEmpty)
            {
                if (entry.Entry == null || (entry.Entry.ClassName == @"Package" || entry.Entry.IsTexture()))
                {
                    // Keep me
                    //Debug.WriteLine($@"Keeping {entry.Entry?.InstancedFullPath} ({entry.Entry?.ClassName})");
                    return false;
                }
                else
                {
                    return true; // Remove this node from the parent
                }
            }
            else
            {
                return entry.Entry != null && !HasAnyTextureLeaves(entry); //If there are any leaves, return false, to indicate this node should not be trimmed
            }
        }

        private bool HasAnyTextureLeaves(TreeViewEntry entry)
        {
            if (entry.Entry.IsTexture()) return true;
            foreach (var t in entry.Sublinks)
            {
                if (HasAnyTextureLeaves(t))
                {
                    return true;
                }
            }

            return false;
        }

        private int QueuedGotoNumber;
        private bool IsLoadingFile;

        private void InitializeTreeViewBackground_Completed(Task<List<TreeViewEntry>> prevTask)
        {
            if (prevTask.Result != null)
            {
                ResetTreeView();
                AllTreeViewNodesX.AddRange(prevTask.Result);
            }

            IsLoadingFile = false;
            if (QueuedGotoNumber != 0)
            {
                //Wait for UI to render
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle, null);
                //BusyText = $"Navigating to {QueuedGotoNumber}";

                //GoToNumber(QueuedGotoNumber);
                //Goto_TextBox.Text = QueuedGotoNumber.ToString();
                if (QueuedGotoNumber > 0)
                {
                    //Interpreter_Tab.IsSelected = true;
                }

                QueuedGotoNumber = 0;
                IsBusy = false;
            }
            else
            {
                IsBusy = false;
            }
        }

        private void ResetTreeView()
        {
            foreach (TreeViewEntry tvi in AllTreeViewNodesX.SelectMany(node => node.FlattenTree()))
            {
                tvi.Dispose();
            }
            AllTreeViewNodesX.ClearEx();
        }

        /// <summary>
        /// Generates a new package stored master texture
        /// </summary>
        /// <param name="idxLink"></param>
        /// <param name="objectName"></param>
        /// <param name="sourceFilepath"></param>
        /// <param name="incomingImage"></param>
        public static ExportEntry GenerateNewMasterTextureExport(IMEPackage package, int idxLink, string objectName, PixelFormat destFormat, string lodGroup, string sourceFilepath, Image incomingImage)
        {
            var props = new PropertyCollection();

            incomingImage.correctMips(destFormat);

            props.AddOrReplaceProp(new IntProperty(incomingImage.mipMaps[0].width, @"SizeX"));
            props.AddOrReplaceProp(new IntProperty(incomingImage.mipMaps[0].height, @"SizeY"));
            props.AddOrReplaceProp(new EnumProperty(lodGroup, @"TextureGroup", package.Game, @"LODGroup"));
            props.AddOrReplaceProp(new IntProperty(incomingImage.mipMaps.Count, @"MipTailBaseIdx"));

            props.AddOrReplaceProp(new EnumProperty(Image.getEngineFormatType(destFormat), @"EPixelFormat", package.Game, @"Format"));
            if (package.Game == MEGame.ME1)
            {
                props.AddOrReplaceProp(new StrProperty(sourceFilepath, @"SourceFilePath"));
                props.AddOrReplaceProp(new StrProperty(new FileInfo(sourceFilepath).LastAccessTimeUtc.ToString("yyyy-mm-dd hh:mm:ss"), @"SourceFileTimeStamp"));
            }

            var binary = new UTexture2D
            {
                Mips = new List<UTexture2D.Texture2DMipMap>(),
                Unk1 = 0xD, // This is value many other textures have
                TextureGuid = new Guid()
            };

            var index = 0;
            foreach (var mipData in incomingImage.mipMaps)
            {
                var mip = new UTexture2D.Texture2DMipMap();
                if (index < incomingImage.mipMaps.Count - 6)
                {
                    mip.Mip = TextureCompression.CompressTexture(mipData.data, StorageTypes.pccLZO); //LZO for me1
                    mip.CompressedSize = mip.Mip.Length;
                    mip.UncompressedSize = mipData.data.Length;
                    mip.StorageType = StorageTypes.pccLZO;
                }
                else
                {
                    mip.Mip = mipData.data;
                    mip.CompressedSize = mipData.data.Length;
                    mip.UncompressedSize = mipData.data.Length;
                    mip.StorageType = StorageTypes.pccUnc;
                }

                mip.SizeX = mipData.width;
                mip.SizeY = mipData.height;
                binary.Mips.Add(mip);
                index++;
            }

            var texport = new ExportEntry(package, idxLink, objectName, properties: props, binary: binary)
            {
                Class = EntryImporter.GetOrAddCrossImportOrPackage("Engine.Texture2D", GetGlobalPackageForGame(package.Game), package, new RelinkerOptionsPackage()),
            };

            package.AddExport(texport);
            return texport;
        }

        /// <summary>
        /// Fetches a package that contains Texture2D exports that we can pull data from
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        private static IMEPackage GetGlobalPackageForGame(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return MEPackageHandler.OpenMEPackage(Path.Combine(ME1Directory.CookedPCPath, @"BIOG_ASA_ARM_CTH_R.upk"), forceLoadFromDisk: true); //Force from disk will prevent refcount
            }
            throw new NotImplementedException();
        }

        private void MasterTextureSelector_OnClosing(object sender, CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                ResetTreeView();
            }
        }
    }
}