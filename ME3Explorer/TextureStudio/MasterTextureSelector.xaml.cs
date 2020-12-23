using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MassEffectModder.Images;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;

namespace ME3Explorer.TextureStudio
{
    /// <summary>
    /// Interaction logic for MasterTextureSelector.xaml
    /// </summary>
    public partial class MasterTextureSelector : WPFBase
    {
        #region Binding Vars

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

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

        public ObservableCollectionExtended<TreeViewEntry> AllTreeViewNodesX { get; set; } =
            new ObservableCollectionExtended<TreeViewEntry>();

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

                    GenerateNewTextureExport(linkSelector.selectedPackageRoot ? 0 : linkSelector.selectedEntry.UIndex, textureName, (PixelFormat)Enum.Parse(typeof(PixelFormat), destPixelFormat), selectDDS.FileName, image);
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

        public override void handleUpdate(List<PackageUpdate> updates)
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

        private ObservableCollectionExtendedWPF<TreeViewEntry> InitializeTreeViewBackground()
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "ME1MasterPackageEditor TreeViewInitialization";

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
            foreach (TreeViewEntry entry in rootNodes)
            {
                TrimNonTextureNodes(entry);
            }


            return new ObservableCollectionExtendedWPF<TreeViewEntry>(rootNodes.Except(itemsToRemove));
        }

        public void TrimNonTextureNodes(TreeViewEntry entry)
        {
            for (int i = entry.Sublinks.Count - 1; i > 0; i--)
            {
                var subEntry = entry.Sublinks[i];
                TrimNonTextureNodes(subEntry);
            }

            // Am I a texture or a package?
            if (entry.Entry != null && (entry.Entry.IsTexture() || entry.Entry.ClassName == "@Package"))
            {
                // Keep me
            }
            else
            {
                entry.Parent?.Sublinks.Remove(entry);
            }
        }

        private int QueuedGotoNumber;
        private bool IsLoadingFile;

        private void InitializeTreeViewBackground_Completed(Task<ObservableCollectionExtendedWPF<TreeViewEntry>> prevTask)
        {
            if (prevTask.Result != null)
            {
                AllTreeViewNodesX.ClearEx();
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

        /// <summary>
        /// Generates a new package stored master texture
        /// </summary>
        /// <param name="idxLink"></param>
        /// <param name="objectName"></param>
        /// <param name="sourceFilepath"></param>
        /// <param name="incomingImage"></param>
        private void GenerateNewTextureExport(int idxLink, string objectName, PixelFormat destFormat, string sourceFilepath, Image incomingImage)
        {
            PropertyCollection props = new PropertyCollection();

            incomingImage.correctMips(destFormat);

            props.AddOrReplaceProp(new IntProperty(incomingImage.mipMaps[0].width, @"SizeX"));
            props.AddOrReplaceProp(new IntProperty(incomingImage.mipMaps[0].height, @"SizeY"));
            props.AddOrReplaceProp(new IntProperty(incomingImage.mipMaps.Count, @"MipTailBaseIdx"));

            // Todo: Make it so there's way to choose this. I have no idea how to determine this however
            props.AddOrReplaceProp(new EnumProperty(@"PF_A8R8G8B8", @"EPixelFormat", Pcc.Game, @"Format"));
            if (Pcc.Game == MEGame.ME1)
            {
                props.AddOrReplaceProp(new StrProperty(sourceFilepath, @"SourceFilePath"));
                props.AddOrReplaceProp(new StrProperty(new FileInfo(sourceFilepath).LastAccessTimeUtc.ToString("yyyy-mm-dd hh:mm:ss"), @"SourceFileTimeStamp"));
            }

            UTexture2D binary = new UTexture2D();
            binary.Mips = new List<UTexture2D.Texture2DMipMap>();
            binary.Unk1 = 0xD; // This is value many other textures have
            binary.TextureGuid = new Guid();

            var index = 0;
            foreach (var mipData in incomingImage.mipMaps)
            {
                var mip = new UTexture2D.Texture2DMipMap();
                if (index < incomingImage.mipMaps.Count - 6)
                {
                    mip.Mip = TextureCompression.CompressTexture(mipData.data, StorageTypes.pccLZO); //LZO 
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

            Pcc.AddExport(new ExportEntry(Pcc, properties: props, binary: binary)
            {
                ObjectName = objectName,
                idxLink = idxLink,
                Class = EntryImporter.GetOrAddCrossImportOrPackageFromGlobalFile("Engine.Texture2D", GetGlobalPackageForGame(Pcc.Game), Pcc)
            });
        }

        /// <summary>
        /// Fetches a package that contains Texture2D exports that we can pull data from
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        private IMEPackage GetGlobalPackageForGame(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return MEPackageHandler.OpenMEPackage(Path.Combine(ME1Directory.CookedPCPath, @"BIOG_ASA_ARM_CTH_R.upk"), forceLoadFromDisk: true); //Force from disk will prevent refcount
            }
            throw new NotImplementedException();
        }
    }
}