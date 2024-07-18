using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Packages;
using System.Windows.Media.Imaging;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    public class MatEdTexture : NotifyPropertyChangedBase, IMatEdTexture
    {
        private string _displayString;
        public string DisplayString { get => _displayString; set => SetProperty(ref _displayString, value); }

        private ExportEntry _textureExp;
        public ExportEntry TextureExp { get => _textureExp; set => SetProperty(ref _textureExp, value); }
        public ImportEntry TextureImp { get; set; }

        private BitmapSource _loadedBitMap;
        public BitmapSource LoadedBitMap { get => _loadedBitMap; set => SetProperty(ref _loadedBitMap, value); }

        public MatEdTexture(IMEPackage pcc, int texIdx, PackageCache cache)
        {
            MatEditorTextureLoader.InitTexture(this, pcc, texIdx, cache);
        }

    }
}
