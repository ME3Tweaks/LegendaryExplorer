using System;
using System.Numerics;
using System.Windows;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Tools.PackageEditor
{
    /// <summary>
    /// Interaction logic for TextureCreatorDialog.xaml
    /// </summary>
    public partial class TextureCreatorDialog : NotifyPropertyChangedWindowBase
    {
        private IEntry Parent;

        private IMEPackage Package;

        private int _sizeX = 2048;
        private int _sizeY = 2048;
        private PixelFormat _pixelFormat = PixelFormat.DXT5;
        private string _chosenName;
        private bool _isMipped = true;

        public int SizeX
        {
            get => _sizeX;
            set => SetProperty(ref _sizeX, value);
        }

        public int SizeY
        {
            get => _sizeY;
            set => SetProperty(ref _sizeY, value);
        }

        public PixelFormat PixelFormat
        {
            get => _pixelFormat;
            set => SetProperty(ref _pixelFormat, value);
        }

        public string ChosenName
        {
            get => _chosenName;
            set => SetProperty(ref _chosenName, value);
        }

        public bool IsMipped
        {
            get => _isMipped;
            set => SetProperty(ref _isMipped, value);
        }

        /// <summary>
        /// Export generated, if any
        /// </summary>
        public ExportEntry GeneratedExport { get; set; }

        /// <summary>
        /// List of available pixel formats. Should probably be changed to be specific to the given game (only matters for OT).
        /// </summary>
        public PixelFormat[] PixelFormats { get; } = Enum.GetValues<PixelFormat>();

        public TextureCreatorDialog(IMEPackage package, IEntry parent)
        {
            Parent = parent;
            Package = package;
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GenerateTexture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ValidateSettings();
            }
            catch (TextureSizeNotPowerOf2Exception ex)
            {
                MessageBox.Show(this, "Texture dimensions must be powers of 2 and less than 8192.", "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            GeneratedExport = Texture2D.CreateTexture(Package, NameReference.FromInstancedString(ChosenName), SizeX, SizeY, PixelFormat, IsMipped, Parent as ExportEntry);
            Close();
        }

        private void ValidateSettings()
        {
            if (!BitOperations.IsPow2(SizeX) || !BitOperations.IsPow2(SizeY))
                throw new TextureSizeNotPowerOf2Exception();
        }

        private void Cancel_Click2(object sender, RoutedEventArgs e)
        {

        }
    }
}
