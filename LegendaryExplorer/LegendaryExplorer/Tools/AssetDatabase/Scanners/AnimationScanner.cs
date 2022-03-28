using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    class AnimationScanner : AssetScanner
    {
        public AnimationScanner() : base()
        {
        }

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (e.IsDefault) return;
            if (e.ClassName is "AnimSequence" or "SFXAmbPerfGameData")
            {
                var animUsage = new AnimUsage(e.FileKey, e.Export.UIndex, e.IsMod);
                if (db.GeneratedAnims.TryGetValue(e.AssetKey, out var anim))
                {
                    lock (anim)
                    {
                        anim.Usages.Add(animUsage);
                    }
                }
                else
                {
                    string aSeq = null;
                    string aGrp = "None";
                    float aLength = 0;
                    int aFrames = 0;
                    string aComp = "None";
                    string aKeyF = "None";
                    bool IsAmbPerf = false;
                    if (e.ClassName == "AnimSequence")
                    {
                        var pSeq = e.Properties.GetProp<NameProperty>("SequenceName");
                        if (pSeq != null)
                        {
                            aSeq = pSeq.Value.Instanced;
                            aGrp = e.Export.ObjectName.Instanced.Replace($"{aSeq}_", null);
                        }

                        var pLength = e.Properties.GetProp<FloatProperty>("SequenceLength");
                        aLength = pLength?.Value ?? 0;

                        var pFrames = e.Properties.GetProp<IntProperty>("NumFrames");
                        aFrames = pFrames?.Value ?? 0;

                        var pComp = e.Properties.GetProp<EnumProperty>("RotationCompressionFormat");
                        aComp = pComp?.Value.ToString() ?? "None";

                        var pKeyF = e.Properties.GetProp<EnumProperty>("KeyEncodingFormat");
                        aKeyF = pKeyF?.Value.ToString() ?? "None";
                    }
                    else //is ambient performance
                    {
                        IsAmbPerf = true;
                        aSeq = "Multiple";
                        var pAnimsets = e.Properties.GetProp<ArrayProperty<StructProperty>>("m_aAnimsets");
                        aFrames = pAnimsets?.Count ?? 0;
                    }

                    var NewAnim = new AnimationRecord(e.ObjectNameInstanced, aSeq, aGrp, aLength, aFrames, aComp, aKeyF, IsAmbPerf, e.IsMod);
                    NewAnim.Usages.Add(animUsage);
                    if (!db.GeneratedAnims.TryAdd(e.AssetKey, NewAnim))
                    {
                        var a = db.GeneratedAnims[e.AssetKey];
                        lock (a)
                        {
                            a.Usages.Add(animUsage);
                        }
                    }
                }
            }
        }
    }
}
