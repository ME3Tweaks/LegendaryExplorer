using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    class MeshScanner : AssetScanner
    {
        public MeshScanner() : base()
        {
        }

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (e.IsDefault) return;
            if (e.ClassName is "SkeletalMesh" or "StaticMesh")
            {
                var meshUsage = new MeshUsage(e.FileKey, e.Export.UIndex, e.IsMod);
                if (db.GeneratedMeshes.ContainsKey(e.AssetKey))
                {
                    var mr = db.GeneratedMeshes[e.AssetKey];
                    lock (mr)
                    {
                        mr.Usages.Add(meshUsage);
                    }
                }
                else
                {
                    bool IsSkel = e.ClassName == "SkeletalMesh";
                    int bones = 0;
                    if (IsSkel)
                    {
                        var bin = ObjectBinary.From<SkeletalMesh>(e.Export);
                        bones = bin?.RefSkeleton.Length ?? 0;
                    }

                    var NewMeshRec = new MeshRecord(e.ObjectNameInstanced, IsSkel, e.IsMod, bones);
                    NewMeshRec.Usages.Add(meshUsage);
                    if (!db.GeneratedMeshes.TryAdd(e.AssetKey, NewMeshRec))
                    {
                        var mr = db.GeneratedMeshes[e.AssetKey];
                        lock (mr)
                        {
                            mr.Usages.Add(meshUsage);
                        }
                    }
                }
            }
        }
    }
}
