using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AmaroK86.ImageFormat;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;
using MassEffectModder.Images;
using ME3Explorer.ME3ExpMemoryAnalyzer;
using ME3Explorer.Packages;
using ME3Explorer.Properties;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using Image = MassEffectModder.Images.Image;
using PixelFormat = MassEffectModder.Images.PixelFormat;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for TextureViewerExportLoader.xaml
    /// </summary>
    public partial class TextureViewerExportLoader : ExportLoaderControl
    {
        public ObservableCollectionExtended<Texture2DMipInfo> MipList { get; } = new ObservableCollectionExtended<Texture2DMipInfo>();
        private string CurrentLoadedFormat;
        private string CurrentLoadedCacheName;
        private string CurrentLoadedBasePackageName;

        public ObservableCollectionExtended<string> AvailableTFCNames { get; } = new ObservableCollectionExtended<string>();

        private string _cannotShowTextureText;
        public string CannotShowTextureText
        {
            get => _cannotShowTextureText;
            set => SetProperty(ref _cannotShowTextureText, value);
        }

        private Visibility _cannotShowTextureTextVisibility;
        public Visibility CannotShowTextureTextVisibility
        {
            get => _cannotShowTextureTextVisibility;
            set => SetProperty(ref _cannotShowTextureTextVisibility, value);
        }

        private Stretch _imageStretchOption = Stretch.Uniform;
        public Stretch ImageStretchOption
        {
            get => _imageStretchOption;
            set => SetProperty(ref _imageStretchOption, value);
        }

        private uint _textureCRC;
        public uint TextureCRC
        {
            get => _textureCRC;
            set => SetProperty(ref _textureCRC, value);
        }

        public bool ViewerModeOnly
        {
            get => (bool)GetValue(ViewerModeOnlyProperty);
            set => SetValue(ViewerModeOnlyProperty, value);
        }
        /// <summary>
        /// Set to true to hide all of the editor controls
        /// </summary>
        public static readonly DependencyProperty ViewerModeOnlyProperty = DependencyProperty.Register(
            "ViewerModeOnly", typeof(bool), typeof(TextureViewerExportLoader), new PropertyMetadata(false, ViewerModeOnlyCallback));

        private const string CREATE_NEW_TFC_STRING = "Create new TFC";
        private const string STORE_EXTERNALLY_STRING = "Store externally in new TFC";
        private const string PACKAGE_STORED_STRING = "Package stored";

        private static void ViewerModeOnlyCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TextureViewerExportLoader i = (TextureViewerExportLoader)obj;
            i.OnPropertyChanged(nameof(ViewerModeOnly));
        }

        public TextureViewerExportLoader()
        {
            MemoryAnalyzer.AddTrackedMemoryItem("Embedded Texture Viewer Export Loader", new WeakReference(this));

            DataContext = this;
            
            if (!ViewerModeOnly)
                CannotShowTextureText = "Select a mip to view";
            CannotShowTextureTextVisibility = Visibility.Visible;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand ExportToPNGCommand { get; set; }
        public ICommand ReplaceFromPNGCommand { get; set; }
        private void LoadCommands()
        {
            ExportToPNGCommand = new GenericCommand(ExportToPNG, NonEmptyMipSelected);
            ReplaceFromPNGCommand = new GenericCommand(ReplaceFromFile, CanReplaceTexture);
        }

        private bool CanReplaceTexture()
        {
            return CurrentLoadedExport != null && CurrentLoadedExport.FileRef.CanReconstruct && !ViewerModeOnly;
        }

        private void ReplaceFromFile()
        {
            var selectedTFCName = (string)TextureCacheComboBox.SelectedItem;
            if (TFCCompactor.TFCCompactor.BasegameTFCs.Contains(selectedTFCName) || MEDirectories.OfficialDLC(CurrentLoadedExport.Game).Any(x => $"Textures_{x}" == selectedTFCName))
            {
                MessageBox.Show("Cannot replace textures into a TFC provided by BioWare. Choose a different target TFC from the list.");
                return;
            }
            OpenFileDialog selectDDS = new OpenFileDialog
            {
                Title = "Select texture file",
                Filter = "Texture (DDS PNG BMP TGA)|*.dds;*.png;*.bmp;*.tga"
            };
            var result = selectDDS.ShowDialog();
            if (result.HasValue && result.Value)
            {
                var image = new Image(selectDDS.FileName);
                //Check aspect ratios
                var props = CurrentLoadedExport.GetProperties();
                var listedWidth = props.GetProp<IntProperty>("SizeX");
                var listedHeight = props.GetProp<IntProperty>("SizeY");

                if (image.mipMaps[0].origWidth / image.mipMaps[0].origHeight != listedWidth / listedHeight)
                {
                    MessageBox.Show("Cannot replace texture: Aspect ratios must be the same.");
                    return;
                }

                string forcedTFCName = selectedTFCName;
                if (selectedTFCName == CREATE_NEW_TFC_STRING || selectedTFCName == STORE_EXTERNALLY_STRING)
                {
                    string defaultTfcName = "Textures_DLC_MOD_YourModFolderNameHere";
                    //attempt to lookup name.
                    var containingFolderInfo = Directory.GetParent(CurrentLoadedExport.FileRef.FilePath);
                    if (Path.GetFileName(containingFolderInfo.FullName).StartsWith("CookedPC"))
                    {
                        //Check next level up.
                        containingFolderInfo = containingFolderInfo.Parent;
                        if (containingFolderInfo != null && Path.GetFileName(containingFolderInfo.FullName).StartsWith("DLC_"))
                        {
                            var possibleDLCName = Path.GetFileName(containingFolderInfo.FullName);
                            if (!MEDirectories.OfficialDLC(CurrentLoadedExport.Game).Contains(possibleDLCName))
                            {
                                defaultTfcName = $"Textures_{possibleDLCName}";
                            }
                        }

                    }
                    PromptDialog p = new PromptDialog("Enter name for a new TFC. It must start with Textures_DLC_, and will be created in the local directory of this package file.", "Enter new name for TFC", defaultTfcName) { Owner = Window.GetWindow(this) };
                    var hasResult = p.ShowDialog();
                    if (hasResult.HasValue && hasResult.Value)
                    {
                        if (p.ResponseText.StartsWith("Textures_DLC_") && p.ResponseText.Length > 14)
                        {
                            //Check TFC name isn't in list
                            CurrentLoadedExport.FileRef.FindNameOrAdd(p.ResponseText);
                            forcedTFCName = p.ResponseText;
                        }
                        else
                        {
                            MessageBox.Show("Error: Name must start with Textures_DLC_, and must have at least one additional character.\nThe named should match your DLC's foldername.");
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                if (forcedTFCName == PACKAGE_STORED_STRING) forcedTFCName = null;
                replaceTextures(image, props, selectDDS.FileName, forcedTFCName);
            }

        }

        private void ExportToPNG()
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "PNG files|*.png",
                FileName = CurrentLoadedExport.ObjectName.Instanced + ".png"
            };
            if (d.ShowDialog() == true)
            {
                Texture2D t2d = new Texture2D(CurrentLoadedExport);
                t2d.ExportToPNG(d.FileName);
            }

        }

        private bool NonEmptyMipSelected()
        {
            if (MipList.Count > 0 && Mips_ListBox.SelectedIndex >= 0)
            {
                return MipList[Mips_ListBox.SelectedIndex].storageType != StorageTypes.empty;
            }
            return false;
        }

        public override bool CanParse(ExportEntry exportEntry) => !exportEntry.IsDefaultObject && exportEntry.IsTexture();

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new TextureViewerExportLoader(), CurrentLoadedExport)
                {
                    Title = $"Texture Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {Pcc.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            TextureImage.Source = null;
            try
            {
                AvailableTFCNames.ClearEx();
                PropertyCollection properties = exportEntry.GetProperties();
                var format = properties.GetProp<EnumProperty>("Format");
                var cache = properties.GetProp<NameProperty>("TextureFileCacheName");
                if (cache != null)
                {
                    CurrentLoadedCacheName = cache.Value.Name;
                }

                var neverStream = properties.GetProp<BoolProperty>("NeverStream") ?? false;
                AvailableTFCNames.AddRange(exportEntry.FileRef.Names.Where(x => x.StartsWith("Textures_DLC_")));

                //Populate list first in event loading fails, so user has way to still try to fix texture.
                if (cache != null)
                {
                    if (!AvailableTFCNames.Contains(cache.Value))
                    {
                        AvailableTFCNames.Add(cache.Value);
                    }
                    TextureCacheComboBox.SelectedIndex = AvailableTFCNames.IndexOf(cache.Value);
                    AvailableTFCNames.Add(CREATE_NEW_TFC_STRING);
                }
                else
                {
                    AvailableTFCNames.Add(PACKAGE_STORED_STRING);
                    AvailableTFCNames.Add(STORE_EXTERNALLY_STRING);
                    TextureCacheComboBox.SelectedIndex = AvailableTFCNames.IndexOf(PACKAGE_STORED_STRING);

                }

                List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(exportEntry, CurrentLoadedCacheName);

                var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);

                if (exportEntry.FileRef.Game == MEGame.ME1)
                {
                    string baseName = exportEntry.FileRef.FollowLink(exportEntry.idxLink).Split('.')[0].ToUpper();
                    if (mips.Exists(s => s.storageType == StorageTypes.extLZO) ||
                        mips.Exists(s => s.storageType == StorageTypes.extZlib) ||
                        mips.Exists(s => s.storageType == StorageTypes.extUnc))
                    {
                        CurrentLoadedBasePackageName = baseName;
                    }
                    else
                    {
                        if (baseName != "" && !neverStream)
                        {
                            List<string> gameFiles = MEDirectories.EnumerateGameFiles(MEGame.ME1, ME1Directory.gamePath);
                            if (gameFiles.Exists(s => Path.GetFileNameWithoutExtension(s).ToUpperInvariant() == baseName))
                            {
                                CurrentLoadedBasePackageName = baseName;
                            }
                        }
                    }
                }

                CurrentLoadedExport = exportEntry;
                CurrentLoadedFormat = format.Value.Name;
                MipList.ReplaceAll(mips);
                TextureCRC = Texture2D.GetTextureCRC(exportEntry);
                if (Settings.Default.EmbeddedTextureViewer_AutoLoad || ViewerModeOnly)
                {
                    Mips_ListBox.SelectedIndex = MipList.IndexOf(topmip);
                }
                //
                //LoadMip(topmip);
            }
            catch (Exception e)
            {
                //Error loading texture
                CannotShowTextureText = e.Message;
            }
        }



        private void LoadMip(Texture2DMipInfo mipToLoad)
        {
            if (mipToLoad == null)
            {
                TextureImage.Source = null;
                if (!ViewerModeOnly)
                    CannotShowTextureText = "Select a mip to view";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }

            if (mipToLoad.storageType == StorageTypes.empty)
            {
                TextureImage.Source = null;
                CannotShowTextureText = "Selected mip is null/empty";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }
            if (mipToLoad.width == 1 && mipToLoad.height == 1)
            {
                TextureImage.Source = null;
                CannotShowTextureText = "Selected mip too small to display";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }
            TextureImage.Source = null;
            try
            {
                var imagebytes = Texture2D.GetTextureData(mipToLoad);
                CannotShowTextureTextVisibility = Visibility.Collapsed;
                var fmt = DDSImage.convertFormat(CurrentLoadedFormat);
                var bitmap = DDSImage.ToBitmap(imagebytes, fmt, mipToLoad.width, mipToLoad.height);
                using (MemoryStream memory = new MemoryStream())
                {
                    bitmap.Save(memory, ImageFormat.Bmp);
                    memory.Position = 0;
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    TextureImage.Source = bitmapImage; //image1 is your control            }
                }
            }
            catch (Exception e)
            {
                TextureImage.Source = null;
                CannotShowTextureText = e.Message;
                CannotShowTextureTextVisibility = Visibility.Visible;
            }
        }

        public override void UnloadExport()
        {
            TextureImage.Source = null;
            CurrentLoadedFormat = null;
            MipList.ClearEx();
            CurrentLoadedExport = null;
        }

        private void MipList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MipList.Count > 0 && Mips_ListBox.SelectedIndex >= 0)
            {
                Debug.WriteLine($"Loading mip: {Mips_ListBox.SelectedIndex}");
                LoadMip(MipList[Mips_ListBox.SelectedIndex]);
            }
        }

        public string replaceTextures(Image image, PropertyCollection props, string fileSourcePath = null, string forcedTFCName = null)
        {
            string errors = "";
            Texture2D texture = new Texture2D(CurrentLoadedExport);
            var textureCache = forcedTFCName ?? texture.GetTopMip().TextureCacheName;
            string fmt = texture.TextureFormat;
            PixelFormat pixelFormat = Image.getPixelFormatType(fmt);
            texture.RemoveEmptyMipsFromMipList();

            // Not sure what this does?
            // Remove all but one mip?
            //if (CurrentLoadedExport.Game == MEGame.ME1 && texture.mipMapsList.Count < 6)
            //{
            //    for (int i = texture.mipMapsList.Count - 1; i != 0; i--)
            //        texture.mipMapsList.RemoveAt(i);
            //}
            PixelFormat newPixelFormat = pixelFormat;

            //Changing Texture Type. Not sure what this is, exactly.
            //if (mod.markConvert)
            //    newPixelFormat = changeTextureType(pixelFormat, image.pixelFormat, ref package, ref texture);


            if (!image.checkDDSHaveAllMipmaps() || (texture.Mips.Count > 1 && image.mipMaps.Count() <= 1) || (image.pixelFormat != newPixelFormat))
            //(!mod.markConvert && image.pixelFormat != pixelFormat))
            {
                bool dxt1HasAlpha = false;
                byte dxt1Threshold = 128;
                if (pixelFormat == PixelFormat.DXT1 && props.GetProp<EnumProperty>("CompressionSettings") is EnumProperty compressionSettings && compressionSettings.Value.Name == "TC_OneBitAlpha")
                {
                    dxt1HasAlpha = true;
                    if (image.pixelFormat == PixelFormat.ARGB ||
                        image.pixelFormat == PixelFormat.DXT3 ||
                        image.pixelFormat == PixelFormat.DXT5)
                    {
                        errors += "Warning: Texture was converted from full alpha to binary alpha." + Environment.NewLine;
                    }
                }

                //Generate lower mips
                image.correctMips(newPixelFormat, dxt1HasAlpha, dxt1Threshold);
            }

            if (texture.Mips.Count == 1)
            {
                var topMip = image.mipMaps[0];
                image.mipMaps.Clear();
                image.mipMaps.Add(topMip);
            }
            else
            {
                // remove lower mipmaps from source image which not exist in game data
                //Not sure what this does since we just generated most of these mips
                for (int t = 0; t < image.mipMaps.Count(); t++)
                {
                    if (image.mipMaps[t].origWidth <= texture.Mips[0].width &&
                        image.mipMaps[t].origHeight <= texture.Mips[0].height &&
                        texture.Mips.Count > 1)
                    {
                        if (!texture.Mips.Exists(m => m.width == image.mipMaps[t].origWidth && m.height == image.mipMaps[t].origHeight))
                        {
                            image.mipMaps.RemoveAt(t--);
                        }
                    }
                }

                // put empty mips if missing
                for (int t = 0; t < texture.Mips.Count; t++)
                {
                    if (texture.Mips[t].width <= image.mipMaps[0].origWidth &&
                        texture.Mips[t].height <= image.mipMaps[0].origHeight)
                    {
                        if (!image.mipMaps.Exists(m => m.origWidth == texture.Mips[t].width && m.origHeight == texture.Mips[t].height))
                        {
                            MipMap mipmap = new MipMap(texture.Mips[t].width, texture.Mips[t].height, pixelFormat);
                            image.mipMaps.Add(mipmap);
                        }
                    }
                }
            }

            //if (!texture.properties.exists("LODGroup"))
            //    texture.properties.setByteValue("LODGroup", "TEXTUREGROUP_Character", "TextureGroup", 1025);
            List<byte[]> compressedMips = new List<byte[]>();

            for (int m = 0; m < image.mipMaps.Count(); m++)
            {
                if (CurrentLoadedExport.Game == MEGame.ME2)
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, StorageTypes.extLZO)); //LZO 
                else
                    compressedMips.Add(TextureCompression.CompressTexture(image.mipMaps[m].data, StorageTypes.extZlib)); //ZLib
            }

            List<Texture2DMipInfo> mipmaps = new List<Texture2DMipInfo>();
            for (int m = 0; m < image.mipMaps.Count(); m++)
            {

                Texture2DMipInfo mipmap = new Texture2DMipInfo();
                mipmap.Export = CurrentLoadedExport;
                mipmap.width = image.mipMaps[m].origWidth;
                mipmap.height = image.mipMaps[m].origHeight;
                mipmap.TextureCacheName = textureCache;
                if (texture.Mips.Exists(x => x.width == mipmap.width && x.height == mipmap.height))
                {
                    var oldMip = texture.Mips.First(x => x.width == mipmap.width && x.height == mipmap.height);
                    mipmap.storageType = oldMip.storageType;
                }
                else
                {
                    mipmap.storageType = texture.Mips[0].storageType;
                    if (texture.Mips.Count() > 1)
                    {
                        //Will implement later. ME3Explorer won't support global relinking, that's MEM's job.
                        //if (CurrentLoadedExport.Game == MEGame.ME1 && matched.linkToMaster == -1)
                        //{
                        //    if (mipmap.storageType == StorageTypes.pccUnc)
                        //    {
                        //        mipmap.storageType = StorageTypes.pccLZO;
                        //    }
                        //}
                        //else if (CurrentLoadedExport.Game == MEGame.ME1 && matched.linkToMaster != -1)
                        //{
                        //    if (mipmap.storageType == StorageTypes.pccUnc ||
                        //        mipmap.storageType == StorageTypes.pccLZO ||
                        //        mipmap.storageType == StorageTypes.pccZlib)
                        //    {
                        //        mipmap.storageType = StorageTypes.extLZO;
                        //    }
                        //}
                        //else 
                    }
                }

                //ME2,ME3: Force compression type (not implemented yet)
                if (texture.Export.Game == MEGame.ME3)
                {
                    if (mipmap.storageType == StorageTypes.extLZO) //ME3 LZO -> ZLIB
                        mipmap.storageType = StorageTypes.extZlib;
                    if (mipmap.storageType == StorageTypes.pccLZO) //ME3 PCC LZO -> PCCZLIB
                        mipmap.storageType = StorageTypes.pccZlib;
                    if (mipmap.storageType == StorageTypes.extUnc) //ME3 Uncomp -> ZLib
                        mipmap.storageType = StorageTypes.extZlib;
                    //Leave here for future. WE might need this after dealing with double compression
                    //if (mipmap.storageType == StorageTypes.pccUnc && mipmap.width > 32) //ME3 Uncomp -> ZLib
                    //    mipmap.storageType = StorageTypes.pccZlib;
                    if (mipmap.storageType == StorageTypes.pccUnc && m < image.mipMaps.Count() - 6 && textureCache != null) //Moving texture to store externally.
                        mipmap.storageType = StorageTypes.extZlib;
                }
                else if (texture.Export.Game == MEGame.ME2)
                {
                    if (mipmap.storageType == StorageTypes.extZlib) //ME2 ZLib -> LZO
                        mipmap.storageType = StorageTypes.extLZO;
                    if (mipmap.storageType == StorageTypes.pccZlib) //M2 ZLib -> LZO
                        mipmap.storageType = StorageTypes.pccLZO;
                    if (mipmap.storageType == StorageTypes.extUnc) //ME2 Uncomp -> LZO
                        mipmap.storageType = StorageTypes.extLZO;
                    //Leave here for future. We might neable this after dealing with double compression
                    //if (mipmap.storageType == StorageTypes.pccUnc && mipmap.width > 32) //ME2 Uncomp -> LZO
                    //    mipmap.storageType = StorageTypes.pccLZO;
                    if (mipmap.storageType == StorageTypes.pccUnc && m < image.mipMaps.Count() - 6 && textureCache != null) //Moving texture to store externally. make sure bottom 6 are pcc stored
                        mipmap.storageType = StorageTypes.extLZO;
                }


                //Investigate. this has something to do with archive storage types
                //if (mod.arcTexture != null)
                //{
                //    if (mod.arcTexture[m].storageType != mipmap.storageType)
                //    {
                //        mod.arcTexture = null;
                //    }
                //}

                mipmap.width = image.mipMaps[m].width;
                mipmap.height = image.mipMaps[m].height;
                mipmaps.Add(mipmap);
                if (texture.Mips.Count() == 1)
                    break;
            }

            #region MEM code comments. Should probably leave for reference

            //if (texture.properties.exists("TextureFileCacheName"))
            //{
            //    string archive = texture.properties.getProperty("TextureFileCacheName").valueName;
            //    if (mod.arcTfcDLC && mod.arcTfcName != archive)
            //        mod.arcTexture = null;

            //    if (mod.arcTexture == null)
            //    {
            //        archiveFile = Path.Combine(GameData.MainData, archive + ".tfc");
            //        if (matched.path.ToLowerInvariant().Contains("\\dlc"))
            //        {
            //            mod.arcTfcDLC = true;
            //            string DLCArchiveFile = Path.Combine(Path.GetDirectoryName(GameData.GamePath + matched.path), archive + ".tfc");
            //            if (File.Exists(DLCArchiveFile))
            //                archiveFile = DLCArchiveFile;
            //            else if (!File.Exists(archiveFile))
            //            {
            //                List<string> files = Directory.GetFiles(GameData.bioGamePath, archive + ".tfc",
            //                    SearchOption.AllDirectories).Where(item => item.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList();
            //                if (files.Count == 1)
            //                    archiveFile = files[0];
            //                else if (files.Count == 0)
            //                {
            //                    using (FileStream fs = new FileStream(DLCArchiveFile, FileMode.CreateNew, FileAccess.Write))
            //                    {
            //                        fs.WriteFromBuffer(texture.properties.getProperty("TFCFileGuid").valueStruct);
            //                    }
            //                    archiveFile = DLCArchiveFile;
            //                    newTfcFile = true;
            //                }
            //                else
            //                    throw new Exception("More instnces of TFC file: " + archive + ".tfc");
            //            }
            //        }
            //        else
            //        {
            //            mod.arcTfcDLC = false;
            //        }

            //        // check if texture fit in old space
            //        for (int mip = 0; mip < image.mipMaps.Count(); mip++)
            //        {
            //            Texture.MipMap testMipmap = new Texture.MipMap();
            //            testMipmap.width = image.mipMaps[mip].origWidth;
            //            testMipmap.height = image.mipMaps[mip].origHeight;
            //            if (texture.existMipmap(testMipmap.width, testMipmap.height))
            //                testMipmap.storageType = texture.getMipmap(testMipmap.width, testMipmap.height).storageType;
            //            else
            //            {
            //                oldSpace = false;
            //                break;
            //            }

            //            if (testMipmap.storageType == StorageTypes.extZlib ||
            //                testMipmap.storageType == StorageTypes.extLZO)
            //            {
            //                Texture.MipMap oldTestMipmap = texture.getMipmap(testMipmap.width, testMipmap.height);
            //                if (mod.cacheCprMipmaps[mip].Length > oldTestMipmap.compressedSize)
            //                {
            //                    oldSpace = false;
            //                    break;
            //                }
            //            }
            //            if (texture.mipMapsList.Count() == 1)
            //                break;
            //        }

            //        long fileLength = new FileInfo(archiveFile).Length;
            //        if (!oldSpace && fileLength + 0x5000000 > 0x80000000)
            //        {
            //            archiveFile = "";
            //            foreach (TFCTexture newGuid in guids)
            //            {
            //                archiveFile = Path.Combine(GameData.MainData, newGuid.name + ".tfc");
            //                if (!File.Exists(archiveFile))
            //                {
            //                    texture.properties.setNameValue("TextureFileCacheName", newGuid.name);
            //                    texture.properties.setStructValue("TFCFileGuid", "Guid", newGuid.guid);
            //                    using (FileStream fs = new FileStream(archiveFile, FileMode.CreateNew, FileAccess.Write))
            //                    {
            //                        fs.WriteFromBuffer(newGuid.guid);
            //                    }
            //                    newTfcFile = true;
            //                    break;
            //                }
            //                else
            //                {
            //                    fileLength = new FileInfo(archiveFile).Length;
            //                    if (fileLength + 0x5000000 < 0x80000000)
            //                    {
            //                        texture.properties.setNameValue("TextureFileCacheName", newGuid.name);
            //                        texture.properties.setStructValue("TFCFileGuid", "Guid", newGuid.guid);
            //                        break;
            //                    }
            //                }
            //                archiveFile = "";
            //            }
            //            if (archiveFile == "")
            //                throw new Exception("No free TFC texture file!");
            //        }
            //    }
            //    else
            //    {
            //        texture.properties.setNameValue("TextureFileCacheName", mod.arcTfcName);
            //        texture.properties.setStructValue("TFCFileGuid", "Guid", mod.arcTfcGuid);
            //    }
            //}

            #endregion

            int allextmipssize = 0;

            for (int m = 0; m < image.mipMaps.Count(); m++)
            {
                Texture2DMipInfo x = mipmaps[m];
                var compsize = image.mipMaps[m].data.Length;

                if (x.storageType == StorageTypes.extZlib ||
                        x.storageType == StorageTypes.extLZO ||
                        x.storageType == StorageTypes.extUnc)
                {
                    allextmipssize += compsize; //compsize on Unc textures is same as LZO/ZLib
                }
            }
            //todo: check to make sure TFC will not be larger than 2GiB
            Guid tfcGuid = Guid.NewGuid(); //make new guid as storage
            bool locallyStored = mipmaps[0].storageType == StorageTypes.pccUnc || mipmaps[0].storageType == StorageTypes.pccZlib || mipmaps[0].storageType == StorageTypes.pccLZO;
            for (int m = 0; m < image.mipMaps.Count(); m++)
            {
                Texture2DMipInfo mipmap = mipmaps[m];
                //if (mipmap.width > 32)
                //{
                //    if (mipmap.storageType == StorageTypes.pccUnc)
                //    {
                //        mipmap.storageType = texture.Export.Game == MEGame.ME2 ? StorageTypes.pccLZO : StorageTypes.pccZlib;
                //    }
                //    if (mipmap.storageType == StorageTypes.extUnc)
                //    {
                //        mipmap.storageType = texture.Export.Game == MEGame.ME2 ? StorageTypes.extLZO : StorageTypes.extZlib;
                //    }
                //}

                mipmap.uncompressedSize = image.mipMaps[m].data.Length;
                if (CurrentLoadedExport.Game == MEGame.ME1)
                {
                    if (mipmap.storageType == StorageTypes.pccLZO ||
                        mipmap.storageType == StorageTypes.pccZlib)
                    {
                        mipmap.newDataForSerializing = compressedMips[m];
                        mipmap.compressedSize = mipmap.newDataForSerializing.Length;
                    }
                    else if (mipmap.storageType == StorageTypes.pccUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.newDataForSerializing = image.mipMaps[m].data;
                    }
                    else
                    {
                        throw new Exception("Unknown mip storage type!");
                    }
                }
                else
                {
                    if (mipmap.storageType == StorageTypes.extZlib ||
                        mipmap.storageType == StorageTypes.extLZO)
                    {
                        if (compressedMips.Count != image.mipMaps.Count())
                            throw new Exception("Amount of compressed mips does not match number of mips of incoming image!");
                        mipmap.newDataForSerializing = compressedMips[m];
                        mipmap.compressedSize = mipmap.newDataForSerializing.Length;
                    }


                    if (mipmap.storageType == StorageTypes.pccUnc ||
                        mipmap.storageType == StorageTypes.extUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.newDataForSerializing = image.mipMaps[m].data;
                    }

                    if (mipmap.storageType == StorageTypes.pccLZO || mipmap.storageType == StorageTypes.pccZlib)
                    {
                        mipmap.newDataForSerializing = compressedMips[m];
                        mipmap.compressedSize = mipmap.newDataForSerializing.Length;
                    }

                    if (mipmap.storageType == StorageTypes.extZlib ||
                        mipmap.storageType == StorageTypes.extLZO ||
                        mipmap.storageType == StorageTypes.extUnc)
                    {
                        if (!string.IsNullOrEmpty(mipmap.TextureCacheName) && mipmap.Export.Game != MEGame.ME1)
                        {
                            //Check local dir
                            string tfcarchive = mipmap.TextureCacheName + ".tfc";
                            var localDirectoryTFCPath = Path.Combine(Path.GetDirectoryName(mipmap.Export.FileRef.FilePath), tfcarchive);
                            if (File.Exists(localDirectoryTFCPath))
                            {
                                try
                                {
                                    using (FileStream fs = new FileStream(localDirectoryTFCPath, FileMode.Open, FileAccess.ReadWrite))
                                    {
                                        tfcGuid = fs.ReadGuid();
                                        fs.Seek(0, SeekOrigin.End);
                                        mipmap.externalOffset = (int)fs.Position;
                                        fs.Write(mipmap.newDataForSerializing, 0, mipmap.compressedSize);
                                    }
                                }
                                catch (Exception e)
                                {
                                    throw new Exception("Problem appending to TFC file " + tfcarchive + ": " + e.Message);
                                }
                                continue;
                            }

                            //Check game
                            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(mipmap.Export.Game, includeTFCs: true);
                            if (gameFiles.TryGetValue(tfcarchive, out string archiveFile))
                            {
                                try
                                {
                                    using (FileStream fs = new FileStream(archiveFile, FileMode.Open, FileAccess.ReadWrite))
                                    {
                                        tfcGuid = fs.ReadGuid();
                                        fs.Seek(0, SeekOrigin.End);
                                        mipmap.externalOffset = (int)fs.Position;
                                        fs.Write(mipmap.newDataForSerializing, 0, mipmap.compressedSize);
                                    }
                                }
                                catch (Exception e)
                                {
                                    throw new Exception("Problem appending to TFC file " + archiveFile + ": " + e.Message);
                                }
                                continue;
                            }


                            //Cache not found. Make new TFC
                            try
                            {
                                using (FileStream fs = new FileStream(localDirectoryTFCPath, FileMode.OpenOrCreate, FileAccess.Write))
                                {
                                    fs.WriteGuid(tfcGuid);
                                    mipmap.externalOffset = (int)fs.Position;
                                    fs.Write(mipmap.newDataForSerializing, 0, mipmap.compressedSize);
                                }
                            }
                            catch (Exception e)
                            {
                                throw new Exception("Problem creating new TFC file " + tfcarchive + ": " + e.Message);
                            }
                            continue;
                        }
                    }
                }
                mipmaps[m] = mipmap;
                if (texture.Mips.Count() == 1)
                    break;
            }

            texture.ReplaceMips(mipmaps);

            //Set properties


            // The bottom 6 mips are apparently always pcc stored. If there is less than 6 mips, set neverstream to true, which tells game
            // and toolset to never look into archives for mips.
            //if (CurrentLoadedExport.Game == MEGame.ME2 || CurrentLoadedExport.Game == MEGame.ME3)
            //{
            //    if (texture.properties.exists("TextureFileCacheName"))
            //    {
            //        if (texture.mipMapsList.Count < 6)
            //        {
            //            mipmap.storageType = StorageTypes.pccUnc;
            //            texture.properties.setBoolValue("NeverStream", true);
            //        }
            //        else
            //        {
            //            if (CurrentLoadedExport.Game == MEGame.ME2)
            //                mipmap.storageType = StorageTypes.extLZO;
            //            else
            //                mipmap.storageType = StorageTypes.extZlib;
            //        }
            //    }
            //}
            if (mipmaps.Count < 6)
            {
                props.AddOrReplaceProp(new BoolProperty(true, "NeverStream"));
            }
            else
            {
                var neverStream = props.GetProp<BoolProperty>("NeverStream");
                if (neverStream != null)
                {
                    props.Remove(neverStream);
                }
            }

            if (!locallyStored)
            {
                props.AddOrReplaceProp(tfcGuid.ToGuidStructProp("TFCFileGuid"));
                if (mipmaps[0].storageType == StorageTypes.extLZO || mipmaps[0].storageType == StorageTypes.extUnc || mipmaps[0].storageType == StorageTypes.extZlib)
                {
                    //Requires texture cache name
                    props.AddOrReplaceProp(new NameProperty(textureCache, "TextureFileCacheName"));
                }
                else
                {
                    //Should not have texture cache name
                    var cacheProp = props.GetProp<NameProperty>("TextureFileCacheName");
                    if (cacheProp != null)
                    {
                        props.Remove(cacheProp);
                    }
                }
            }
            else
            {
                props.RemoveNamedProperty("TFCFileGuid");
            }

            props.AddOrReplaceProp(new IntProperty(texture.Mips.First().width, "SizeX"));
            props.AddOrReplaceProp(new IntProperty(texture.Mips.First().height, "SizeY"));
            if (CurrentLoadedExport.Game < MEGame.ME3 && fileSourcePath != null)
            {
                props.AddOrReplaceProp(new StrProperty(fileSourcePath, "SourceFilePath"));
                props.AddOrReplaceProp(new StrProperty(File.GetLastWriteTimeUtc(fileSourcePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), "SourceFileTimestamp"));
            }

            var mipTailIdx = props.GetProp<IntProperty>("MipTailBaseIdx");
            if (mipTailIdx != null)
            {
                mipTailIdx.Value = texture.Mips.Count - 1;
            }

            EndianReader mem = new EndianReader(new MemoryStream()) { Endian = texture.Export.FileRef.Endian};
            props.WriteTo(mem.Writer, texture.Export.FileRef);
            mem.Position = 0;
            var test = PropertyCollection.ReadProps(texture.Export, mem.BaseStream, "Texture2D", true, true); //do not set properties as this may interfere with some other code. may change later.
            int propStart = CurrentLoadedExport.GetPropertyStart();
            var pos = mem.Position;
            mem.Position = 0;
            byte[] propData = mem.ToArray();
            if (CurrentLoadedExport.Game == MEGame.ME3)
            {
                CurrentLoadedExport.Data = CurrentLoadedExport.Data.Take(propStart).Concat(propData).Concat(texture.SerializeNewData()).ToArray();
            }
            else
            {
                var array = CurrentLoadedExport.Data.Take(propStart).Concat(propData).ToArray();
                var testdata = new MemoryStream(array);
                var test2 = PropertyCollection.ReadProps(texture.Export, testdata, "Texture2D", true, true, CurrentLoadedExport); //do not set properties as this may interfere with some other code. may change later.
                //ME2 post-data is this right?
                CurrentLoadedExport.Data = CurrentLoadedExport.Data.Take(propStart).Concat(propData).Concat(texture.SerializeNewData()).ToArray();
            }

            //using (MemoryStream newData = new MemoryStream())
            //{
            //    newData.WriteFromBuffer(texture.properties.toArray());
            //    newData.WriteFromBuffer(texture.toArray(0, false)); // filled later
            //    package.setExportData(matched.exportID, newData.ToArray());
            //}

            //using (MemoryStream newData = new MemoryStream())
            //{
            //    newData.WriteFromBuffer(texture.properties.toArray());
            //    newData.WriteFromBuffer(texture.toArray(package.exportsTable[matched.exportID].dataOffset + (uint)newData.Position));
            //    package.setExportData(matched.exportID, newData.ToArray());
            //}

            //Since this is single replacement, we don't want to relink to master
            //We want to ensure names are different though, will have to implement into UI
            //if (CurrentLoadedExport.Game == MEGame.ME1)
            //{
            //    if (matched.linkToMaster == -1)
            //        mod.masterTextures.Add(texture.mipMapsList, entryMap.listIndex);
            //}
            //else
            //{
            //    if (triggerCacheArc)
            //    {
            //        mod.arcTexture = texture.mipMapsList;
            //        mod.arcTfcGuid = texture.properties.getProperty("TFCFileGuid").valueStruct;
            //        mod.arcTfcName = texture.properties.getProperty("TextureFileCacheName").valueName;
            //    }
            //}


            return errors;
        }

        public override void Dispose()
        {
            //Nothing to dispose
        }

        private void ScalingTurnOff(object sender, RoutedEventArgs e)
        {
            ImageStretchOption = Stretch.None;
        }

        private void ScalingTurnOn(object sender, RoutedEventArgs e)
        {
            ImageStretchOption = Stretch.Uniform;
        }
    }
}
