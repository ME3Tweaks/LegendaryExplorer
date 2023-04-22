using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures.Studio;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using FGuid = LegendaryExplorerCore.Helpers.FGuid;

namespace LegendaryExplorerCore.Textures
{
    /// <summary>
    /// Class containing useful TFC-related methods
    /// </summary>
    public static class TFCTools
    {
        public static void FindExternalizableTextures(string dir)
        {
            // Todo: Add to texture map so you don't have to put duplicates into TFC again.

            var tfcName = $"Textures_{Path.GetFileName(dir)}";
            var textureFile = Path.Combine(dir, @"CookedPCConsole", $"{tfcName}.tfc");
            FileStream fs;
            Guid tfcGuid = Guid.NewGuid();
            if (File.Exists(textureFile))
            {
                fs = File.Open(textureFile, FileMode.Open, FileAccess.ReadWrite);
                tfcGuid = fs.ReadGuid();
                fs.Seek(0, SeekOrigin.End);
            }
            else
            {
                fs = File.Create(textureFile);
                fs.WriteGuid(tfcGuid);
            }

            Dictionary<uint, MEMTextureMap.TextureMapEntry> vanillaMap = null;

            var packageFiles = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(x => x.RepresentsPackageFilePath());
            foreach (var pf in packageFiles)
            {
                Debug.WriteLine($"Opening package {pf}");
                using var ufp = MEPackageHandler.OpenMEPackage(pf);
                if (vanillaMap == null)
                {
                    vanillaMap = MEMTextureMap.LoadTextureMap(ufp.Game);
                }

                foreach (var exp in ufp.Exports.Where(x => x.IsTexture() && x.ClassName != "ShadowMapTexture2D" && !x.ObjectName.Name.Contains(@"CubemapFace")))
                {
                    //if (exp.ObjectName == @"Omg_PanelA_Spec")
                    //    Debugger.Break();

                    var tex = ObjectBinary.From<UTexture2D>(exp);

                    // First 6 mips are always locally stored
                    if (tex.Mips.Count > 6)
                    {
                        var externalMips = tex.Mips.Take(Math.Abs(6 - tex.Mips.Count)).ToList();
                        if (externalMips.Any())
                        {
                            // May be externalizable
                            Texture2D t2d = new Texture2D(exp);
                            if (t2d.Mips[0].TextureCacheName == tfcName)
                                continue; // This is already in our TFC
                            var crc = Texture2D.GetMipCRC(t2d.GetTopMip(), t2d.TextureFormat);
                            if (vanillaMap.TryGetValue(crc, out var vanillaInfo))
                            {
                                var p = vanillaInfo.ContainingPackages[0];


                                // The vanilla texture map from me doesn't include TFC guid or TFC name (or compressed mip info either, it seems)
                                // We need to open the package to find them

                                // Find relative path (from MEM vanilla file)
                                var relativePath = p.RelativePackagePath.Substring(p.RelativePackagePath.IndexOf("BioGame", StringComparison.InvariantCultureIgnoreCase));

                                var cpPath = Path.Combine(MEDirectories.GetDefaultGamePath(ufp.Game), relativePath);
                                var containingPackage = MEPackageHandler.UnsafePartialLoad(cpPath, x => x.UIndex == p.UIndex + 1);
                                var matchingExport = containingPackage.GetUExport(p.UIndex + 1); // MEM uses 0 based indexing.


                                var matchingTex = ObjectBinary.From<UTexture2D>(matchingExport);
                                var matchingCompressedMips = matchingTex.Mips.Take(matchingTex.Mips.Count - 6).ToList(); // Bottom 6 are always package stored
                                for (int i = 0; i < matchingCompressedMips.Count; i++)
                                {
                                    var cm = matchingCompressedMips[i];

                                    // Modify ObjectBinary version as it's easier to work with
                                    var em = tex.Mips[i];
                                    em.DataOffset = cm.DataOffset;
                                    em.StorageType = cm.StorageType;
                                    em.UncompressedSize = cm.UncompressedSize;
                                    em.CompressedSize = cm.CompressedSize;
                                    em.Mip = Array.Empty<byte>();
                                }

                                var props = matchingExport.GetProperties();


                                props.AddOrReplaceProp(matchingExport.GetProperty<NameProperty>(@"TextureFileCacheName"));
                                props.AddOrReplaceProp(matchingExport.GetProperty<StructProperty>(@"TFCFileGuid"));
                                props.RemoveNamedProperty(@"NeverStream");

                                matchingExport.WritePropertiesAndBinary(props, tex);

                            }
                            else
                            {
                                foreach (var mip in externalMips)
                                {
                                    mip.DataOffset = (int)fs.Position;
                                    byte[] data = mip.Mip;
                                    if (mip.StorageType == StorageTypes.pccUnc)
                                    {
                                        var storageType = TFCCompactor.GetTargetExternalStorageType(ufp.Game);
                                        mip.StorageType = storageType;
                                        data = TextureCompression.CompressTexture(mip.Mip, storageType);
                                        mip.CompressedSize = data.Length;

                                    }

                                    mip.DataOffset = (int)fs.Position;
                                    fs.Write(data);
                                    mip.Mip = Array.Empty<byte>();
                                }

                                exp.WriteProperty(new NameProperty(tfcName, @"TextureFileCacheName"));
                                exp.WriteProperty(new FGuid(tfcGuid).ToStructProperty(@"TFCFileGuid"));
                                exp.RemoveProperty(@"NeverStream");
                                exp.WriteBinary(tex);
                            }
                        }
                        //    foreach (var em in externalMips)
                        //    {
                        //        if (em.IsLocallyStored)
                        //        {
                        //            // Debug.WriteLine($@"Externalizing mip found in {exp.InstancedFullPath} in {pf}");

                        //            // Texture data is stored locally but needs decompressed.
                        //            var texData = Texture2D.GetTextureData(ufp.Game, em.Mip, em.StorageType, true,
                        //                em.UncompressedSize, em.CompressedSize, em.DataOffset, null, null, null,
                        //                exp.FileRef.FilePath);
                        //            var texCRC = TextureCRC.Compute(texData);


                        //            // Write new
                        //            var offset = fs.Position;
                        //            em.DataOffset = (int)offset;
                        //            em.StorageType |= (StorageTypes)StorageFlags.externalFile;
                        //            fs.Write(em.Mip);

                        //            exp.WriteProperty(new NameProperty(tfcName, @"TextureFileCacheName"));
                        //            exp.WriteProperty(new FGuid(tfcGuid).ToStructProperty(@"TFCFileGuid"));

                        //            em.Mip = Array.Empty<byte>();
                        //            writeTFCProp = true;

                        //            // break;
                        //        }
                        //    }
                    }

                    //if (writeTFCProp)
                    //{
                    //    exp.WriteBinary(tex);
                    //    exp.RemoveProperty(@"NeverStream");
                    //}
                }

                if (ufp.IsModified)
                    ufp.Save();

                // if (i > 6)
                //    break;
            }
            fs.Close();
        }
    }
}