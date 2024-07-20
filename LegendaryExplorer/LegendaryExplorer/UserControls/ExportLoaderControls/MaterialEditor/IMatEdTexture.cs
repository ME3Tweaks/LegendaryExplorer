using System;
using LegendaryExplorerCore.Packages;
using System.Windows.Media.Imaging;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    public interface IMatEdTexture
    {
        /// <summary>
        /// Used to make sure we don't drag/drop onto same control. Uses a guid as we don't want to have a reference.
        /// </summary>
        public Guid HostingControlGuid { get; set; }
        public string DisplayString { get; set; }
        public ExportEntry TextureExp { get; set; }
        public ImportEntry TextureImp { get; set; }
        public BitmapSource LoadedBitMap { get; set; }
        void ReplaceTexture(IMatEdTexture met);
    }
}
