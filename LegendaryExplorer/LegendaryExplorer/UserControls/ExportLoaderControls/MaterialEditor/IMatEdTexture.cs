using LegendaryExplorerCore.Packages;
using System.Windows.Media.Imaging;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    public interface IMatEdTexture
    {
        public string DisplayString { get; set; }
        public ExportEntry TextureExp { get; set; }
        public ImportEntry TextureImp { get; set; }
        public BitmapSource LoadedBitMap { get; set; }
    }
}
