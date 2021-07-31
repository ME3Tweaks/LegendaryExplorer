using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    class VFXScanner : AssetScanner
    {
        public VFXScanner() : base()
        {
        }

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (e.ClassName == "ParticleSystem" || e.ClassName == "RvrClientEffect" || e.ClassName == "BioVFXTemplate")
            {
                var particleSysUsage = new ParticleSysUsage(e.FileKey, e.Export.UIndex, e.IsDlc, e.IsMod);
                if (db.GeneratedPS.ContainsKey(e.AssetKey))
                {
                    var ePS = db.GeneratedPS[e.AssetKey];
                    lock (ePS)
                    {
                        ePS.Usages.Add(particleSysUsage);
                    }
                }
                else
                {
                    string parent = null;
                    if (e.Export.Game == MEGame.ME1 && e.FileName.EndsWith(".upk"))
                    {
                        parent = Path.GetFileNameWithoutExtension(e.FileName);
                    }
                    else
                    {
                        parent = GetTopParentPackage(e.Export);
                    }

                    var vfxtype = ParticleSysRecord.VFXClass.BioVFXTemplate;
                    int EmCnt = 0;
                    if (e.ClassName == "ParticleSystem")
                    {
                        var EmtProp = e.Properties.GetProp<ArrayProperty<ObjectProperty>>("Emitters");
                        EmCnt = EmtProp?.Count ?? 0;
                        vfxtype = ParticleSysRecord.VFXClass.ParticleSystem;
                    }
                    else if (e.ClassName == "RvrClientEffect")
                    {
                        var RvrProp = e.Properties.GetProp<ArrayProperty<ObjectProperty>>("m_lstModules");
                        EmCnt = RvrProp?.Count ?? 0;
                        vfxtype = ParticleSysRecord.VFXClass.RvrClientEffect;
                    }

                    var NewPS = new ParticleSysRecord(e.ObjectNameInstanced, parent, e.IsDlc, e.IsMod, EmCnt, vfxtype);
                    NewPS.Usages.Add(particleSysUsage);
                    if (!db.GeneratedPS.TryAdd(e.AssetKey, NewPS))
                    {
                        var ePS = db.GeneratedPS[e.AssetKey];
                        lock (ePS)
                        {
                            ePS.Usages.Add(particleSysUsage);
                        }
                    }
                }
            }
        }
    }
}
