using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    internal static class MatEditorTextureLoader
    {
        private static object singleThreadSyncLock = new object();
        public static void InitTexture(IMatEdTexture mt, IMEPackage pcc, int texIdx, PackageCache cache)
        {
            if (texIdx == 0)
            {
                mt.DisplayString = "Null";
                return;
            }

            ExportEntry tex = null;
            if (texIdx < 0)
            {
                mt.TextureImp = pcc.GetImport(texIdx);
                if (mt.TextureImp.IsTexture() || mt.TextureImp.ClassName == "TextureCube")
                {
                    var resolved = EntryImporter.ResolveImport(mt.TextureImp, cache, unsafeLoad: true, unsafeLoadDelegate: MaterialInfo.MaterialEdLoadOnlyUsefulExports);
                    if (resolved != null)
                    {
                        mt.DisplayString = $"{resolved.InstancedFullPath} ({resolved.FileRef.FileNameNoExtension}.pcc)";
                        mt.TextureExp = resolved;
                    }
                    else
                    {
                        mt.DisplayString = $"{mt.TextureImp.InstancedFullPath} (Failed to resolve)";
                    }
                }
            }
            else
            {
                var texE = pcc.GetUExport(texIdx);
                if (texE.IsTexture() || texE.ClassName.CaseInsensitiveEquals("TextureCube"))
                {
                    mt.TextureExp = texE;
                    mt.DisplayString = $"{mt.TextureExp.InstancedFullPath}";
                }
            }

            LoadTexture(mt);
        }

        private static void LoadTexture(IMatEdTexture mt)
        {
            if (mt.TextureExp != null)
            {
                Task.Run(() =>
                {
                    // Make this single threaded so we don't have multiple package openings at same time, etc. This is not perf critical
                    lock (singleThreadSyncLock)
                    {
                        var texExport = mt.TextureExp;
                        if (texExport.ClassName.CaseInsensitiveEquals("TextureCube"))
                        {
                            // Correct to the first cube face so we have a preview to show
                            var facePosX = texExport.GetProperty<ObjectProperty>("FacePosX");
                            texExport = facePosX.ResolveToEntry(texExport.FileRef) as ExportEntry;
                        }

                        var t2d = new Texture2D(texExport);
                        var mip = t2d.GetMipWithDimension(64, 64);
                        if (mip != null)
                        {
                            byte[] data = Texture2D.GetTextureData(mip, mip.Export.Game);
                            var bitmap = MaterialEditorLLE.ConvertRawToBitmapARGB(data, mip.width, mip.height,
                                Image.getPixelFormatType(t2d.TextureFormat), true);
                            var memory = new MemoryStream(bitmap.Height * bitmap.Width * 4 + 54);
                            bitmap.Save(memory, ImageFormat.Bmp);
                            memory.Position = 0;
                            return (BitmapSource)new ImageSourceConverter().ConvertFrom(memory);
                        }
                    }

                    return null;
                }).ContinueWithOnUIThread(x =>
                {
                    if (x.Exception == null && x.Result != null)
                    {
                        mt.LoadedBitMap = x.Result;
                    }
                });
            }
        }
    }
}
