using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.AppCenter.Utils.Files;

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
            if (e.ClassName == "Texture2D" || e.ClassName == "TextureCube" || e.ClassName == "TextureMovie")
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
