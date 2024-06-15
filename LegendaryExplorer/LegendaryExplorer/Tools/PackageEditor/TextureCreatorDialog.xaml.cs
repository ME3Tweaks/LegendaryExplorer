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
        private bool _isUsedInSWF;

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

        public bool IsUsedInSWF
        {
            get => _isUsedInSWF;
            set
            {
                SetProperty(ref _isUsedInSWF, value);
                if (value)
                    IsMipped = false; // SWF images cannot be mipped
            }
        }

        /// <summary>
        /// Export generated, if any
        /// </summary>
        public ExportEntry GeneratedExport { get; set; }

        /// <summary>
        /// List of available pixel formats. Should probably be changed to be specific to the given game (only matters for OT).
        /// </summary>
        public PixelFormat[] PixelFormats { get; } = Enum.GetValues<PixelFormat>();

        public TextureCreatorDialog(Window window, IMEPackage package, IEntry parent)
        {
            Owner = window; // must be here for centering
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

            var nameToUse = ChosenName;
            if (IsUsedInSWF)
            {
                nameToUse += "_I1";
            }
            GeneratedExport = Texture2D.CreateTexture(Package, NameReference.FromInstancedString(nameToUse), SizeX, SizeY, PixelFormat, IsMipped, Parent as ExportEntry);
            if (IsUsedInSWF)
            {
                Texture2D.CreateSWFForTexture(GeneratedExport);
            }
            Close();
        }

        private void ValidateSettings()
        {
            if (SizeX < 0 || SizeY < 0 || SizeX > 8192 || SizeY > 8192)
                throw new Exception(@"Texture dimensions must be greater than zero and less than 8192.");
            if (!BitOperations.IsPow2(SizeX) || !BitOperations.IsPow2(SizeY))
                throw new TextureSizeNotPowerOf2Exception();
            if (string.IsNullOrWhiteSpace(ChosenName))
                throw new Exception(@"Name cannot be empty.");
        }

        private void Cancel_Click2(object sender, RoutedEventArgs e)
        {

        }
    }
}
