using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    internal class GUIScanner : AssetScanner
    {
        public GUIScanner() : base()
        {
        }

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (e.IsDefault) return;
            if (e.ClassName is "GFxMovieInfo" or "BioSWF")
            {
                if (db.GeneratedGUI.ContainsKey(e.AssetKey))
                {
                    var eGUI = db.GeneratedGUI[e.AssetKey];
                    lock (eGUI)
                    {
                        eGUI.Usages.Add(new GUIUsage(e.FileKey, e.Export.UIndex, e.IsMod));
                    }
                }
                else
                {
                    string dataPropName = e.ClassName == "GFxMovieInfo" ? "RawData" : "Data";
                    var rawData = e.Export.GetProperty<ImmutableByteArrayProperty>(dataPropName);
                    int datasize = rawData?.Count ?? 0;
                    var NewGUI = new GUIElement(e.Export.ObjectName.Instanced, datasize, e.IsMod);
                    NewGUI.Usages.Add(new GUIUsage(e.FileKey, e.Export.UIndex, e.IsMod));
                    if (db.GeneratedGUI.TryAdd(e.AssetKey, NewGUI))
                    {
                        var eGUI = db.GeneratedGUI[e.AssetKey];
                        lock (eGUI)
                        {
                            eGUI.Usages.Add(new GUIUsage(e.FileKey, e.Export.UIndex, e.IsMod));
                        }
                    }
                }
            }
        }
    }
}
