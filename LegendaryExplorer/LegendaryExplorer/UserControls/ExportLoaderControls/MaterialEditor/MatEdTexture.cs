using System;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Packages;
using System.Windows.Media.Imaging;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.Win32;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    public class MatEdTexture : NotifyPropertyChangedBase, IMatEdTexture
    {
        private string _displayString;

        public Guid HostingControlGuid { get; set; }

        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        private ExportEntry _textureExp;

        public ExportEntry TextureExp
        {
            get => _textureExp;
            set => SetProperty(ref _textureExp, value);
        }

        public ImportEntry TextureImp { get; set; }

        private BitmapSource _loadedBitMap;

        public BitmapSource LoadedBitMap
        {
            get => _loadedBitMap;
            set => SetProperty(ref _loadedBitMap, value);
        }

        public GenericCommand ReplaceTextureCommand { get; }

        /// <summary>
        /// Package to put edits into
        /// </summary>
        public IMEPackage EditingPackage { get; set; }

        public MatEdTexture(IMEPackage pcc, int texIdx, PackageCache cache)
        {
            MatEditorTextureLoader.InitTexture(this, pcc, texIdx, cache);
            ReplaceTextureCommand = new GenericCommand(ReplaceTexture);
        }

        private void ReplaceTexture()
        {
            OpenFileDialog selectDDS = new OpenFileDialog
            {
                Title = "Select texture file",
                Filter =
                    "All supported types|*.png;*.dds;*.tga|PNG files (*.png)|*.png|DDS files (*.dds)|*.dds|TGA files (*.tga)|*.tga",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            var result = selectDDS.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Task.Run(() =>
                {
                    //Check aspect ratios
                    var props = TextureExp.GetProperties();
                    var listedWidth = props.GetProp<IntProperty>("SizeX")?.Value ?? 0;
                    var listedHeight = props.GetProp<IntProperty>("SizeY")?.Value ?? 0;

                    Image image;
                    try
                    {
#if WINDOWS
                        image = Image.LoadFromFile(selectDDS.FileName,
                            LegendaryExplorerCore.Textures.PixelFormat.ARGB);
#else
                    image = new Image(selectDDS.FileName);
#endif
                    }
                    catch (TextureSizeNotPowerOf2Exception)
                    {
                        MessageBox.Show("The width and height of a texture must both be a power of 2\n" +
                                        "(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192)", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
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

                    if (TextureExp.FileRef != EditingPackage)
                    {
                        // Needs moved to editing package
                        var rop = new RelinkerOptionsPackage()
                        {
                            ImportExportDependencies = true,
                            Cache = new PackageCache(),
                        };
                        EntryExporter.ExportExportToPackage(TextureExp, EditingPackage, out var newExp); // Maybe use cache here
                        if (newExp is ImportEntry imp)
                        {
                            // We must forcibly create the texture export
                            var parent = ExportCreator.CreatePackageExport(EditingPackage, "LLEMatEd_ReplacedTextures");
                            Texture2D orig = new Texture2D(TextureExp);
                            TextureExp = Texture2D.CreateTexture(EditingPackage, "CustomTexture_MatEd", orig.GetTopMip().width, orig.GetTopMip().height, Image.getPixelFormatType(orig.TextureFormat), true, parent);
                        }
                        else
                        {
                            TextureExp = newExp as ExportEntry;
                        }
                        TextureExp.ObjectName = new NameReference(TextureExp.ObjectName.Name, new Random().Next(12341234)); // Make something random
                    }

                    var texture = new Texture2D(TextureExp);
                    return texture.Replace(image, props, isPackageStored: true);
                }).ContinueWithOnUIThread(x =>
                {
                    // IsBusy = false;
                    PackageCache cache = new PackageCache();
                    MatEditorTextureLoader.InitTexture(this, TextureExp.FileRef, TextureExp.UIndex, cache);
                });
            }
        }

        public void ReplaceTexture(IMatEdTexture other)
        {
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, other.TextureExp, TextureExp.FileRef, TextureExp, true, new RelinkerOptionsPackage(), out _);
            MatEditorTextureLoader.InitTexture(this, TextureExp.FileRef, TextureExp.UIndex, new PackageCache()); // Reload the texture
        }
    }
}