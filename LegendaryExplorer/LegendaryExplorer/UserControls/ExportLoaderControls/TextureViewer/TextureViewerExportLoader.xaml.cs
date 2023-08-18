using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorer.Tools.TFCCompactor;
using LegendaryExplorer.UserControls.ExportLoaderControls.TextureViewer;
using LegendaryExplorer.UserControls.Interfaces;
using LegendaryExplorer.UserControls.SharedToolControls.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.Win32;
using Image = LegendaryExplorerCore.Textures.Image;
using LegendaryExplorerCore.Helpers;
using SharpDX.Direct3D11;
using LegendaryExplorer.Misc;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for TextureViewerExportLoader.xaml
    /// </summary>
    public partial class TextureViewerExportLoader : ExportLoaderControl, ISceneRenderContextConfigurable
    {

        // Renderer
        private TextureRenderContext TextureContext { get; } = new TextureRenderContext();
        public ObservableCollectionExtended<Texture2DMipInfo> MipList { get; } = new ObservableCollectionExtended<Texture2DMipInfo>();
        private string CurrentLoadedFormat;
        private string CurrentLoadedCacheName;
        private string CurrentLoadedBasePackageName;

        //public ObservableCollectionExtended<string> AvailableTFCNames { get; } = new ObservableCollectionExtended<string>();

        private string _cannotShowTextureText;
        public string CannotShowTextureText
        {
            get => _cannotShowTextureText;
            set => SetProperty(ref _cannotShowTextureText, value);
        }

        private string _textureCacheName;
        public string TextureCacheName
        {
            get => _textureCacheName;
            set => SetProperty(ref _textureCacheName, value);
        }

        #region DISPLAY OPTIONS
        private bool _setAlphaToBlack = true;
        public bool SetAlphaToBlack
        {
            get => _setAlphaToBlack;
            set
            {
                SetProperty(ref _setAlphaToBlack, value);
                if (value)
                {
                    this.TextureContext.Constants.Flags |= TextureRenderContext.TextureViewFlags.AlphaAsBlack;
                }
                else
                {
                    this.TextureContext.Constants.Flags &= ~TextureRenderContext.TextureViewFlags.AlphaAsBlack;
                }
                RequestRender();
            }
        }

        private bool _showRedChannel = true;
        public bool ShowRedChannel
        {
            get => _showRedChannel;
            set
            {
                SetProperty(ref _showRedChannel, value);
                if (value)
                {
                    this.TextureContext.Constants.Flags |= TextureRenderContext.TextureViewFlags.EnableRedChannel;
                }
                else
                {
                    this.TextureContext.Constants.Flags &= ~TextureRenderContext.TextureViewFlags.EnableRedChannel;
                }
                RequestRender();
            }
        }

        private bool _showGreenChannel = true;
        public bool ShowGreenChannel
        {
            get => _showGreenChannel;
            set
            {
                SetProperty(ref _showGreenChannel, value);
                if (value)
                {
                    this.TextureContext.Constants.Flags |= TextureRenderContext.TextureViewFlags.EnableGreenChannel;
                }
                else
                {
                    this.TextureContext.Constants.Flags &= ~TextureRenderContext.TextureViewFlags.EnableGreenChannel;
                }
                RequestRender();
            }
        }



        private bool _showBlueChannel = true;
        public bool ShowBlueChannel
        {
            get => _showBlueChannel;
            set
            {
                SetProperty(ref _showBlueChannel, value);
                if (value)
                {
                    this.TextureContext.Constants.Flags |= TextureRenderContext.TextureViewFlags.EnableBlueChannel;
                }
                else
                {
                    this.TextureContext.Constants.Flags &= ~TextureRenderContext.TextureViewFlags.EnableBlueChannel;
                }
                RequestRender();
            }
        }



        private bool _showAlphaChannel = true;
        public bool ShowAlphaChannel
        {
            get => _showAlphaChannel;
            set
            {
                SetProperty(ref _showAlphaChannel, value);
                if (value)
                {
                    this.TextureContext.Constants.Flags |= TextureRenderContext.TextureViewFlags.EnableAlphaChannel;
                }
                else
                {
                    this.TextureContext.Constants.Flags &= ~TextureRenderContext.TextureViewFlags.EnableAlphaChannel;
                }
                RequestRender();
            }
        }

        private Color _backgroundColor = Colors.White;
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                SetProperty(ref _backgroundColor, value);
                TextureContext.BackgroundColor = new Vector4(value.R / 255.0f, value.G / 255.0f, value.B / 255.0f, value.A / 255.0f);
                RequestRender();
            }
        }
        #endregion

        private Visibility _cannotShowTextureTextVisibility;
        public Visibility CannotShowTextureTextVisibility
        {
            get => _cannotShowTextureTextVisibility;
            set => SetProperty(ref _cannotShowTextureTextVisibility, value);
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

        public IBusyUIHost HostingControl
        {
            get => (IBusyUIHost)GetValue(HostingControlProperty);
            set => SetValue(HostingControlProperty, value);
        }

        public static readonly DependencyProperty HostingControlProperty = DependencyProperty.Register(
            nameof(HostingControl), typeof(IBusyUIHost), typeof(TextureViewerExportLoader));

        private const string CREATE_NEW_TFC_STRING = "Create new TFC";
        private const string STORE_EXTERNALLY_STRING = "Store externally in new TFC";
        private const string PACKAGE_STORED_STRING = "Package stored";

        private static void ViewerModeOnlyCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TextureViewerExportLoader i = (TextureViewerExportLoader)obj;
            i.OnPropertyChanged(nameof(ViewerModeOnly));
        }

        public TextureViewerExportLoader() : base("Texture Viewer")
        {
            DataContext = this;

            if (!ViewerModeOnly)
                CannotShowTextureText = "Select a mip to view";
            CannotShowTextureTextVisibility = Visibility.Visible;
            LoadCommands();
            InitializeComponent();
            this.PreviewRenderer.Context = this.TextureContext;
            this.TextureContext.BackgroundColor = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            this.PreviewRenderer.Loaded += RendererLoaded;
            this.PreviewRenderer.Unloaded += RendererUnloaded;

            // Once an image has been rendered we turn it back off until we need a new one rendered, or we just waste GPU resources.
            PreviewRenderer.OnImageRendered = SignalRendered;
        }

        private void RendererLoaded(object sender, RoutedEventArgs e)
        {
            MipList_SelectedItemChanged(sender, null);
            if (Parent is TabItem { Parent: TabControl tc })
            {
                tc.SelectionChanged += TextureViewer_HostingTabSelectionChanged;
            }
        }

        private void RendererUnloaded(object sender, RoutedEventArgs e)
        {
            if (Parent is TabItem { Parent: TabControl tc })
            {
                tc.SelectionChanged -= TextureViewer_HostingTabSelectionChanged;
            }
        }

        public ICommand ExportToPNGCommand { get; set; }
        public ICommand ReplaceFromPNGCommand { get; set; }
        public ICommand DropMipCommand { get; set; }
        private void LoadCommands()
        {
            ExportToPNGCommand = new GenericCommand(ExportToPNG, NonEmptyMipSelected);
            ReplaceFromPNGCommand = new GenericCommand(ReplaceFromFile, CanReplaceTexture);
            DropMipCommand = new GenericCommand(DropTopMip, CanDropTopMip);
        }

        private void DropTopMip()
        {
            var props = CurrentLoadedExport.GetProperties();

            // Note: This will not remove TFC data if streamed mips are all removed.
            // Hopefully a dev doesn't do this
            // I'm sure someday I will read this comment again and regret it
            // -Mgamerz
            var tex = ObjectBinary.From<UTexture2D>(CurrentLoadedExport);
            tex.Mips.RemoveAt(Mips_ListBox.SelectedIndex);
            props.GetProp<IntProperty>("SizeX").Value = tex.Mips[0].SizeX;
            props.GetProp<IntProperty>("SizeY").Value = tex.Mips[0].SizeY;
            props.GetProp<IntProperty>("MipTailBaseIdx").Value = tex.Mips.Count - 1; // 0 based
            CurrentLoadedExport.WritePropertiesAndBinary(props, tex);
        }

        private bool CanDropTopMip()
        {
            // There must be at least 1 mip
            return CanReplaceTexture() && MipList.Count > 1 /*&& Mips_ListBox.SelectedIndex == 0*/;
        }

        private bool CanReplaceTexture()
        {
            return CurrentLoadedExport != null && CurrentLoadedExport.FileRef.CanReconstruct() && !ViewerModeOnly;
        }

        private void ReplaceFromFile()
        {
            var selectedTFCName = GetDestinationTFCName();
            if (MEDirectories.BasegameTFCs(CurrentLoadedExport.Game).Contains(selectedTFCName, StringComparer.InvariantCultureIgnoreCase) || MEDirectories.OfficialDLC(CurrentLoadedExport.Game).Any(x => $"Textures_{x}".Equals(selectedTFCName, StringComparison.InvariantCultureIgnoreCase)))
            {
                MessageBox.Show("Cannot replace textures into a TFC provided by BioWare. Choose a different target TFC from the list.");
                return;
            }

            OpenFileDialog selectDDS = new OpenFileDialog
            {
                Title = "Select texture file",
#if WINDOWS
                Filter = "All supported types|*.png;*.dds;*.tga|PNG files (*.png)|*.png|DDS files (*.dds)|*.dds|TGA files (*.tga)|*.tga",
#else
                Filter = "Texture (DDS PNG BMP TGA)|*.dds;*.png;*.bmp;*.tga",
#endif
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            var result = selectDDS.ShowDialog();
            if (result.HasValue && result.Value)
            {
                if (HostingControl != null)
                {
                    HostingControl.IsBusy = true;
                    HostingControl.BusyText = "Replacing textures";
                }

                if (selectedTFCName == CREATE_NEW_TFC_STRING || selectedTFCName == STORE_EXTERNALLY_STRING)
                {
                    string defaultTfcName = "Textures_DLC_MOD_YourModFolderNameHere";
                    //attempt to lookup name.
                    var containingFolderInfo = Directory.GetParent(CurrentLoadedExport.FileRef.FilePath);
                    if (Path.GetFileName(containingFolderInfo.FullName).StartsWith("CookedPC"))
                    {
                        //Check next level up.
                        containingFolderInfo = containingFolderInfo.Parent;
                        if (containingFolderInfo != null &&
                            Path.GetFileName(containingFolderInfo.FullName).StartsWith("DLC_"))
                        {
                            var possibleDLCName = Path.GetFileName(containingFolderInfo.FullName);
                            if (!MEDirectories.OfficialDLC(CurrentLoadedExport.Game).Contains(possibleDLCName))
                            {
                                defaultTfcName = $"Textures_{possibleDLCName}";
                            }
                        }

                    }
                    PromptDialog p = new PromptDialog("Enter name for a new TFC. It must start with Textures_DLC_MOD_, and will be created in the local directory of this package file.", "Enter new name for TFC", defaultTfcName, true, "Textures_DLC_MOD_".Length) { Owner = Window.GetWindow(this) };
                    var hasResult = p.ShowDialog();
                    if (hasResult.HasValue && hasResult.Value)
                    {
                        if (p.ResponseText.StartsWith("Textures_DLC_MOD_") && p.ResponseText.Length > 14)
                        {
                            //Check TFC name isn't in list
                            CurrentLoadedExport.FileRef.FindNameOrAdd(p.ResponseText);
                            selectedTFCName = p.ResponseText;
                        }
                        else
                        {
                            MessageBox.Show(
                                "Error: Name must start with Textures_DLC_, and must have at least one additional character.\nThe named should match your DLC's foldername.");
                            return;
                        }
                    }
                    else
                    {
                        if (HostingControl != null)
                        {
                            HostingControl.IsBusy = false;
                        }
                        return;
                    }
                }

                Task.Run(() =>
                {
                    //Check aspect ratios
                    var props = CurrentLoadedExport.GetProperties();
                    var listedWidth = props.GetProp<IntProperty>("SizeX")?.Value ?? 0;
                    var listedHeight = props.GetProp<IntProperty>("SizeY")?.Value ?? 0;

                    Image image;
                    try
                    {
#if WINDOWS
                        image = Image.LoadFromFile(selectDDS.FileName, LegendaryExplorerCore.Textures.PixelFormat.ARGB);
#else
                    image = new Image(selectDDS.FileName);
#endif
                    }
                    catch (TextureSizeNotPowerOf2Exception)
                    {
                        MessageBox.Show("The width and height of a texture must both be a power of 2\n" +
                                        "(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 (LE only))", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Error: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }

                    if (image.mipMaps[0].origWidth / image.mipMaps[0].origHeight != listedWidth / listedHeight)
                    {
                        MessageBox.Show("Cannot replace texture: Aspect ratios must be the same.");
                        return null;
                    }

                    bool isPackageStored = selectedTFCName == PACKAGE_STORED_STRING;
                    if (isPackageStored) selectedTFCName = null;
                    return ReplaceTextures(image, props, selectDDS.FileName, selectedTFCName, isPackageStored);

                    // MER: Dump to disk
                    //var binName = Path.Combine(Directory.GetParent(selectDDS.FileName).FullName, Path.GetFileNameWithoutExtension(selectDDS.FileName) + ".bin");
                    //File.WriteAllBytes(binName, CurrentLoadedExport.GetBinaryData());
                })
                .ContinueWithOnUIThread((a) =>
                {
                    if (HostingControl != null) HostingControl.IsBusy = false;
                    if (a.Exception == null && a.Result != null && a.Result.Any())
                    {
                        var ld = new ListDialog(a.Result, "Textures replaced", "The following messages were generated during replacement of textures.", Window.GetWindow(this));
                        ld.Show();
                    }
                });
            }
        }

        private string GetDestinationTFCName()
        {
            var tex = ObjectBinary.From<UTexture2D>(CurrentLoadedExport);
            if (tex.Mips.Count == 1)
                return PACKAGE_STORED_STRING; // If there is only 1 mip it will always be package stored.

            // This might need updated if we need to stuff textures into UDK for some reason
            var options = new List<string>();
            if (CurrentLoadedExport.Game > MEGame.ME1)
            {
                // TFCs
                options.AddRange(CurrentLoadedExport.FileRef.Names.Where(x => x.StartsWith("Textures_DLC_MOD_")));
                options.Add(CREATE_NEW_TFC_STRING);
            }

            options.Add(PACKAGE_STORED_STRING);
            


            return InputComboBoxWPF.GetValue(Window.GetWindow(this),
                "Select where the new texture should be stored. TFCs are better for game performance.",
                "Select storage location", options, options.First());
        }

        private void ExportToPNG()
        {
            SaveFileDialog d = new SaveFileDialog
            {
#if WINDOWS
                Filter = "PNG files (*.png)|*.png|DDS files (*.dds)|*.dds|TGA files (*.tga)|*.tga",
#else
                Filter = "PNG files|*.png",
#endif
                FileName = CurrentLoadedExport.ObjectName.Instanced + ".png"
            };
            if (d.ShowDialog() == true)
            {
                LegendaryExplorerCore.Unreal.Classes.Texture2D t2d = new LegendaryExplorerCore.Unreal.Classes.Texture2D(CurrentLoadedExport);
#if WINDOWS
                t2d.ExportToFile(d.FileName);
#else
                t2d.ExportToPNG(d.FileName);
#endif
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
                    Title = $"Texture Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {Pcc.FilePath}",
                };
                elhw.Show();
            }
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            TextureContext.Texture = null;
            try
            {
                MipList.ClearEx();
                PropertyCollection properties = exportEntry.GetProperties();
                var format = properties.GetProp<EnumProperty>("Format");
                var cache = properties.GetProp<NameProperty>("TextureFileCacheName");
                if (cache != null)
                {
                    CurrentLoadedCacheName = cache.Value.Name;
                }

                var neverStream = properties.GetProp<BoolProperty>("NeverStream") ?? false;

                //Populate list first in event loading fails, so user has way to still try to fix texture.
                if (cache != null)
                {
                    TextureCacheName = cache.Value.Instanced;
                }

                List<Texture2DMipInfo> mips = LegendaryExplorerCore.Unreal.Classes.Texture2D.GetTexture2DMipInfos(exportEntry, CurrentLoadedCacheName);
                CurrentLoadedExport = exportEntry;

                if (mips.Any())
                {
                    var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);

                    // Some textures list a tfc but are stored locally still
                    // so the tfc is never actually used

                    if (topmip is { storageType: StorageTypes.pccLZO or StorageTypes.pccZlib or StorageTypes.pccOodle or StorageTypes.pccUnc })
                    {
                        TextureCacheName = "Package stored";
                    }

                    if (cache == null && exportEntry.Game > MEGame.ME1)
                    {
                        TextureCacheName = "Package stored";
                    }



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
                                List<string> gameFiles =
                                    MEDirectories.EnumerateGameFiles(MEGame.ME1, ME1Directory.DefaultGamePath);
                                if (gameFiles.Exists(s =>
                                    Path.GetFileNameWithoutExtension(s).ToUpperInvariant() == baseName))
                                {
                                    CurrentLoadedBasePackageName = baseName;
                                }
                            }
                        }
                    }

                    CurrentLoadedFormat = format.Value.Name;
                    MipList.ReplaceAll(mips);
                    TextureCRC = LegendaryExplorerCore.Unreal.Classes.Texture2D.GetTextureCRC(exportEntry);
                    if (Settings.TextureViewer_AutoLoadMip || ViewerModeOnly || IsPoppedOut)
                    {
                        Mips_ListBox.SelectedIndex = MipList.IndexOf(topmip);
                    }
                }

            }
            catch (Exception e)
            {
                //Error loading texture
                CannotShowTextureText = e.Message;
            }
        }



        private void LoadMip(Texture2DMipInfo mipToLoad)
        {
            RequestRender();

            if (mipToLoad == null)
            {
                TextureContext.Texture = null;
                if (!ViewerModeOnly)
                    CannotShowTextureText = "Select a mip to view";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }

            if (mipToLoad.storageType == StorageTypes.empty)
            {
                TextureContext.Texture = null;
                CannotShowTextureText = "Selected mip is null/empty";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }
            /*if (mipToLoad.width == 1 && mipToLoad.height == 1)
            {
                TextureContext.Texture = null;
                CannotShowTextureText = "Selected mip too small to display"; // This will crash the toolset if we show it
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }*/
            TextureContext.Texture = null;
            try
            {
                // ORIGINAL CODE
                //var imagebytes = Texture2D.GetTextureData(mipToLoad, mipToLoad.Export.Game);
                //CannotShowTextureTextVisibility = Visibility.Collapsed;

                //// NOTE: Set 'ClearAlpha' to false to make image support transparency!
                //var bitmap = Image.convertRawToBitmapARGB(imagebytes, mipToLoad.width, mipToLoad.height, Image.getPixelFormatType(CurrentLoadedFormat), SetAlphaToBlack);
                ////var bitmap = DDSImage.ToBitmap(imagebytes, fmt, mipToLoad.width, mipToLoad.height, CurrentLoadedExport.FileRef.Platform.ToString());
                //var memory = new MemoryStream(bitmap.Height * bitmap.Width * 4 + 54);
                //bitmap.Save(memory, ImageFormat.Png);
                //memory.Position = 0;
                //TextureImage.Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(memory);

                LegendaryExplorerCore.Textures.PixelFormat pixelFormat = Image.getPixelFormatType(CurrentLoadedFormat);
                TextureContext.Texture = TextureContext.LoadUnrealMip(mipToLoad, pixelFormat);
                bool needsReconstruction = pixelFormat is LegendaryExplorerCore.Textures.PixelFormat.ATI2
                    or LegendaryExplorerCore.Textures.PixelFormat.BC5
                    or LegendaryExplorerCore.Textures.PixelFormat.V8U8;
                if (needsReconstruction)
                {
                    this.TextureContext.Constants.Flags |= TextureRenderContext.TextureViewFlags.ReconstructNormalZ;
                }
                else
                {
                    this.TextureContext.Constants.Flags &= ~TextureRenderContext.TextureViewFlags.ReconstructNormalZ;
                }
                CannotShowTextureTextVisibility = Visibility.Collapsed;
            }
            catch (Exception e)
            {
                TextureContext.Texture = null;
                CannotShowTextureText = e.Message;
                CannotShowTextureTextVisibility = Visibility.Visible;
            }
        }

        public override void UnloadExport()
        {
            TextureContext.Texture = null;
            CurrentLoadedFormat = null;
            MipList.ClearEx();
            CurrentLoadedExport = null;
        }

        private void MipList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MipList.Count > 0 && Mips_ListBox.SelectedIndex >= 0)
            {
                if (this.TextureContext.IsReady)
                {
                    Debug.WriteLine($"Loading mip: {Mips_ListBox.SelectedIndex}");
                    LoadMip(MipList[Mips_ListBox.SelectedIndex]);
                }
            }
        }

        public List<string> ReplaceTextures(Image image, PropertyCollection props, string fileSourcePath = null, string forcedTFCName = null, bool isPackageStored = false)
        {
            var texture = new LegendaryExplorerCore.Unreal.Classes.Texture2D(CurrentLoadedExport);
            return texture.Replace(image, props, fileSourcePath, forcedTFCName, isPackageStored: isPackageStored);
        }

        public override void Dispose()
        {
            PreviewRenderer.OnImageRendered = null; // Remove reference to this
            PreviewRenderer.Loaded -= RendererLoaded;
            PreviewRenderer.Unloaded -= RendererUnloaded;
            PreviewRenderer.SetShouldRender(false);
        }

        private void CRC_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (TextureCRC != 0 && e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2)
            {
                Clipboard.SetText(TextureCRC.ToString("X8"));
            }
        }

        private void ScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestRender();

            if (this.ScaleComboBox.SelectedValue is ComboBoxItem item && item.Content is string inputString)
            {
                if (inputString == "Scale to Fit")
                {
                    this.TextureContext.ScaleFactor = -1.0f;
                }
                else
                {
                    if (inputString.EndsWith("x") && Single.TryParse(inputString.Substring(0, inputString.Length - 1), out float scaleFactor))
                    {
                        this.TextureContext.ScaleFactor = scaleFactor;
                    }
                    else
                    {
#if DEBUG
                        throw new Exception("That's not a valid scale! Expected 'Scale to Fit' or '?.??x'");
#else
                        System.Diagnostics.Debug.WriteLine("[WARNING]: Invalid scale entered in TextureViewerExportLoader!");
#endif
                    }
                }
            }
        }



        public override void PoppedOut(ExportLoaderHostedWindow window)
        {
            RequestRender(); // turn on rendering
        }

        #region Performance

        /// <summary>
        /// We should render a new frame
        /// </summary>
        public void RequestRender()
        {
            PreviewRenderer?.SetShouldRender(true);
        }

        /// <summary>
        /// Textures do not change (there is no camera, lighting, etc), so we should not render more than the first time.
        /// </summary>
        public void SignalRendered()
        {
            PreviewRenderer?.SetShouldRender(false);
        }

        private void TextureViewer_HostingTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Do not render if we are not visible
            if (Parent is TabItem ti)
            {
                if (e.AddedItems.Contains(ti))
                {
                    RequestRender();
                }
                else if (e.RemovedItems.Contains(ti))
                {
                    PreviewRenderer?.SetShouldRender(false);
                }
            }
        }
        #endregion
    }
}
