using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    internal abstract class AssetScanner
    {
        public ConcurrentAssetDB db { get; set; }

        protected AssetScanner(ConcurrentAssetDB assetDb)
        {
            db = assetDb;
        }

        public abstract void ScanExport(ExportEntry export, int FileKey, bool IsMod);
    }
}
