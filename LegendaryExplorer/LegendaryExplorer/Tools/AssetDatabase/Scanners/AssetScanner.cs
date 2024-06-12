using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    internal abstract class AssetScanner
    {
        protected AssetScanner()
        {
        }

        public abstract void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options);

        protected static string GetTopParentPackage(IEntry entry)
        {
            while (true)
            {
                if (entry.HasParent)
                {
                    entry = entry.Parent;
                }
                else
                {
                    return entry.ObjectName;
                }
            }
        }
    }
}
