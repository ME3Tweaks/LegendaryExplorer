using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    internal class TextureScanner : AssetScanner
    {
        public TextureScanner() : base()
        {
        }

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (e.IsDefault) return;
            
            // 05/02/2025 - Change to .IsTexture() to cover other types like TextureFlipbooks.
            if (e.Export.IsTexture())
            {
                var objectNameInstanced = e.ObjectNameInstanced;
                var textureUsage = new TextureUsage(e.FileKey, e.Export.UIndex, e.IsDlc, e.IsMod);
                if (db.GeneratedText.ContainsKey(e.AssetKey))
                {
                    var t = db.GeneratedText[e.AssetKey];
                    lock (t)
                    {
                        t.Usages.Add(textureUsage);
                    }
                }
                else
                {
                    string parent;
                    if (e.Export.Game == MEGame.ME1 && e.FileName.EndsWith(".upk"))
                    {
                        parent = Path.GetFileNameWithoutExtension(e.FileName);
                    }
                    else
                    {
                        parent = GetTopParentPackage(e.Export);
                    }

                    string pformat = "TextureCube";
                    int psizeX = 0;
                    int psizeY = 0;
                    string cRC = "n/a";
                    string texgrp = "n/a";
                    if (e.ClassName != "TextureCube")
                    {
                        pformat = "TextureMovie";
                        if (e.ClassName != "TextureMovie")
                        {
                            var formp = e.Properties.GetProp<EnumProperty>("Format");
                            pformat = formp?.Value.Name ?? "n/a";
                            pformat = pformat.Replace("PF_", string.Empty);
                            var tgrp = e.Properties.GetProp<EnumProperty>("LODGroup");
                            texgrp = tgrp?.Value.Instanced ?? "n/a";
                            texgrp = texgrp.Replace("TEXTUREGROUP_", string.Empty);
                            texgrp = texgrp.Replace("_", string.Empty);
                            if (options.ScanCRC)
                            {
                                cRC = Texture2D.GetTextureCRC(e.Export).ToString("X8");
                            }
                        }

                        var propX = e.Properties.GetProp<IntProperty>("SizeX");
                        psizeX = propX?.Value ?? 0;
                        var propY = e.Properties.GetProp<IntProperty>("SizeY");
                        psizeY = propY?.Value ?? 0;
                    }

                    if (e.Export.Parent?.ClassName == "TextureCube")
                    {
                        objectNameInstanced = $"{e.Export.Parent.ObjectName}_{objectNameInstanced}";
                    }

                    var NewTex = new TextureRecord(objectNameInstanced, parent, e.IsDlc, e.IsMod, pformat, texgrp, psizeX, psizeY, cRC);
                    NewTex.Usages.Add(textureUsage);
                    if (db.GeneratedText.TryAdd(e.AssetKey, NewTex))
                    {
                        var t = db.GeneratedText[e.AssetKey];
                        lock (t)
                        {
                            t.Usages.Add(textureUsage);
                        }
                    }
                }
            }
        }
    }
}
