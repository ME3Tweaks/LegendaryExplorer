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
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorer.Tools.TFCCompactor;
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

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for TextureViewerExportLoader.xaml
    /// </summary>
    public partial class TextureViewerExportLoader : ExportLoaderControl, ISceneRenderContextConfigurable
    {
        #region Texture Preview
        private class TextureRenderContext : RenderContext
        {
            [Flags]
            public enum TextureViewFlags : int
            {
                None = 0,
                /// <summary>
                /// If normals should have the third color channel populated
                /// </summary>
                ReconstructNormalZ = 1 << 0,
                /// <summary>
                /// If alpha channel should be set to black, so textures can properly be viewed
                /// </summary>
                AlphaAsBlack = 1 << 1,
                EnableRedChannel = 1 << 2,
                EnableGreenChannel = 1 << 3,
                EnableBlueChannel = 1 << 4,
                EnableAlphaChannel = 1 << 5,
            }

            public struct TextureViewConstants
            {
                public Matrix4x4 Projection;
                public Matrix4x4 View;
                public int Mip;
                public TextureViewFlags Flags;
                private Vector2 Padding; // Constant buffers must be a multiple of 16 bytes long
            }

            private RenderTargetView BackbufferRTV = null;
            private SamplerState TextureSampler = null;
            private VertexShader TextureVertexShader = null;
            private PixelShader TexturePixelShader = null;
            private ShaderResourceView TextureRTV = null;
            public TextureViewConstants Constants = new TextureViewConstants() { Flags = TextureViewFlags.EnableRedChannel | TextureViewFlags.EnableGreenChannel | TextureViewFlags.EnableBlueChannel | TextureViewFlags.EnableAlphaChannel };
            private SharpDX.Direct3D11.Buffer ConstantBuffer = null;

            private SharpDX.Direct3D11.Texture2D _texture = null;
            public SharpDX.Direct3D11.Texture2D Texture
            {
                get => this._texture;
                set
                {
                    if (this.TextureRTV != null)
                    {
                        this.TextureRTV.Dispose();
                        this.TextureRTV = null;
                    }
                    this._texture = value;
                    if (this.Texture != null)
                    {
                        this.TextureRTV = new ShaderResourceView(this.Device, this.Texture);
                    }
                }
            }
            public float ScaleFactor { get; set; } = -1.0f; // -1 means scale to fit
            public Vector2 CameraCenter { get; set; } = Vector2.Zero;
            public int CurrentMip // NOTE: The texture export loader passes each mip as its own texture, meaning that we always want mip 0 of the given texture.
            {
                get => this.Constants.Mip;
                set => this.Constants.Mip = value;
            }
            public Vector4 BackgroundColor { get; set; } = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

            public override void CreateResources()
            {
                base.CreateResources();
                this.TextureSampler = new SamplerState(this.Device, new SamplerStateDescription() { AddressU = TextureAddressMode.Wrap, AddressV = TextureAddressMode.Wrap, AddressW = TextureAddressMode.Wrap, Filter = Filter.MinLinearMagMipPoint, MaximumLod = Single.MaxValue, MinimumLod = 0, MipLodBias = 0 });
                this.ConstantBuffer = new SharpDX.Direct3D11.Buffer(this.Device, SharpDX.Utilities.SizeOf<TextureViewConstants>(), ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);

                // Load shaders
                string textureShader = LegendaryExplorer.Resources.EmbeddedResources.TextureShader;
                SharpDX.D3DCompiler.CompilationResult result = SharpDX.D3DCompiler.ShaderBytecode.Compile(textureShader, "VSMain", "vs_4_0");
                SharpDX.D3DCompiler.ShaderBytecode vsbytecode = result.Bytecode;
                this.TextureVertexShader = new VertexShader(Device, vsbytecode);
                vsbytecode.Dispose();

                // Load pixel shader
                result = SharpDX.D3DCompiler.ShaderBytecode.Compile(textureShader, "PSMain", "ps_4_0");
                SharpDX.D3DCompiler.ShaderBytecode psbytecode = result.Bytecode;
                this.TexturePixelShader = new PixelShader(Device, psbytecode);
                psbytecode.Dispose();

                // Set render state (this is a pretty simple D3D component so we can set it once and forget it)
                this.ImmediateContext.PixelShader.SetSampler(0, this.TextureSampler);
                this.ImmediateContext.PixelShader.SetShader(this.TexturePixelShader, null, 0);
                this.ImmediateContext.VertexShader.SetShader(this.TextureVertexShader, null, 0);
                this.ImmediateContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleStrip;
                this.ImmediateContext.OutputMerger.SetBlendState(this.AlphaBlendState);
            }

            public override void CreateSizeDependentResources(int width, int height, SharpDX.Direct3D11.Texture2D newBackbuffer)
            {
                base.CreateSizeDependentResources(width, height, newBackbuffer);
                this.BackbufferRTV = new RenderTargetView(this.Device, this.Backbuffer);

                if (width >= height)
                {
                    float ratio = (float)width / height;
                    this.Constants.Projection = Matrix4x4.CreateOrthographic(ratio, 1.0f, -1.0f, 1.0f);
                }
                else
                {
                    float ratio = (float)height / width;
                    this.Constants.Projection = Matrix4x4.CreateOrthographic(1.0f, ratio, -1.0f, 1.0f);
                }
                this.ImmediateContext.Rasterizer.SetViewport(new SharpDX.Mathematics.Interop.RawViewportF() { X = 0, Y = 0, Width = width, Height = height, MinDepth = 0.0f, MaxDepth = 1.0f });
            }

            public override void Render()
            {
                this.ImmediateContext.OutputMerger.SetRenderTargets(this.BackbufferRTV);
                this.ImmediateContext.ClearRenderTargetView(this.BackbufferRTV, new SharpDX.Mathematics.Interop.RawColor4(this.BackgroundColor.X, this.BackgroundColor.Y, this.BackgroundColor.Z, this.BackgroundColor.W));

                float smallSize = this.Width <= this.Height ? this.Width : this.Height;
                float scale = 1.0f;
                if (this.ScaleFactor > 0.0f)
                {
                    scale = this.Texture.Description.Height / smallSize * this.ScaleFactor;
                }

                this.Constants.View = Matrix4x4.CreateTranslation(-this.CameraCenter.X, -this.CameraCenter.Y, 0.0f) * Matrix4x4.CreateScale(scale);
                SharpDX.DataBox constantBox = this.ImmediateContext.MapSubresource(this.ConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
                System.Runtime.InteropServices.Marshal.StructureToPtr(this.Constants, constantBox.DataPointer, false);
                this.ImmediateContext.UnmapSubresource(this.ConstantBuffer, 0);
                this.ImmediateContext.PixelShader.SetShaderResource(0, this.TextureRTV);
                this.ImmediateContext.VertexShader.SetConstantBuffer(0, this.ConstantBuffer);
                this.ImmediateContext.PixelShader.SetConstantBuffer(0, this.ConstantBuffer);
                this.ImmediateContext.Draw(4, 0);

                base.Render();
            }

            public override void Update(float timestep)
            {
                // Nothing to do here
            }

            public override void DisposeSizeDependentResources()
            {
                this.BackbufferRTV.Dispose();
                base.DisposeSizeDependentResources();
            }

            public override void DisposeResources()
            {
                this.ConstantBuffer.Dispose();
                this.TextureSampler.Dispose();
                base.DisposeResources();
            }
        }

        private TextureRenderContext TextureContext { get; } = new TextureRenderContext();
        #endregion
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
            this.PreviewRenderer.Loaded += (sender, args) => MipList_SelectedItemChanged(sender, null);
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
                Filter = "All supported types|*.png;*.dds;*.tga|PNG files (*.png)|*.png|DDS files (*.dds)|*.dds|TGA files (*.tga)|*.tga"
#else
                Filter = "Texture (DDS PNG BMP TGA)|*.dds;*.png;*.bmp;*.tga"
#endif
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
                        return;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Error: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (image.mipMaps[0].origWidth / image.mipMaps[0].origHeight != listedWidth / listedHeight)
                    {
                        MessageBox.Show("Cannot replace texture: Aspect ratios must be the same.");
                        return;
                    }

                    bool isPackageStored = selectedTFCName == PACKAGE_STORED_STRING;
                    if (isPackageStored) selectedTFCName = null;
                    ReplaceTextures(image, props, selectDDS.FileName, selectedTFCName, isPackageStored);

                    // MER: Dump to disk
                    //var binName = Path.Combine(Directory.GetParent(selectDDS.FileName).FullName, Path.GetFileNameWithoutExtension(selectDDS.FileName) + ".bin");
                    //File.WriteAllBytes(binName, CurrentLoadedExport.GetBinaryData());
                })
                .ContinueWithOnUIThread((a) =>
                {
                    if (HostingControl != null) HostingControl.IsBusy = false;

                });
            }
        }

        private string GetDestinationTFCName()
        {
            // TODO: IMPLEMENT UI FOR THIS
            return "Textures";
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
                    Title = $"Texture Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {Pcc.FilePath}"
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
                    if (Settings.TextureViewer_AutoLoadMip || ViewerModeOnly)
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
                TextureContext.Texture = TextureContext.LoadUnrealMip(mipToLoad, pixelFormat, SetAlphaToBlack);
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

        public string ReplaceTextures(Image image, PropertyCollection props, string fileSourcePath = null, string forcedTFCName = null, bool isPackageStored = false)
        {
            var texture = new LegendaryExplorerCore.Unreal.Classes.Texture2D(CurrentLoadedExport);
            return texture.Replace(image, props, fileSourcePath, forcedTFCName, isPackageStored: isPackageStored);
        }

        public override void Dispose()
        {
            //Nothing to dispose
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
    }
}
