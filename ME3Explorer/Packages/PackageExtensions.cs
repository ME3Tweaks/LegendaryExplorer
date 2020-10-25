using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Unreal.Classes;

namespace ME3Explorer.Packages
{
    public static class PackageExtensions
    {

        public static void ConvertTo(this MEPackage package, MEGame newGame, string tfcPath = null, bool preserveMaterialInstances = false)
        {
            MEGame oldGame = package.Game;
            var prePropBinary = new List<byte[]>(package.ExportCount);
            var propCollections = new List<PropertyCollection>(package.ExportCount);
            var postPropBinary = new List<ObjectBinary>(package.ExportCount);

            if (oldGame == MEGame.ME1 && newGame != MEGame.ME1)
            {
                int idx = package.Names.IndexOf("BIOC_Base");
                if (idx >= 0)
                {
                    package.replaceName(idx, "SFXGame");
                }
            }
            else if (newGame == MEGame.ME1)
            {
                int idx = package.Names.IndexOf("SFXGame");
                if (idx >= 0)
                {
                    package.replaceName(idx, "BIOC_Base");
                }
            }

            //fix up Default_ package.Imports
            if (newGame == MEGame.ME3)
            {
                using IMEPackage core = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "Core.pcc"));
                using IMEPackage engine = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "Engine.pcc"));
                using IMEPackage sfxGame = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "SFXGame.pcc"));
                foreach (ImportEntry defImp in package.Imports.Where(imp => imp.ObjectName.Name.StartsWith("Default_")).ToList())
                {
                    string packageName = defImp.FullPath.Split('.')[0];
                    IMEPackage pck = packageName switch
                    {
                        "Core" => core,
                        "Engine" => engine,
                        "SFXGame" => sfxGame,
                        _ => null
                    };
                    if (pck != null && pck.Exports.FirstOrDefault(exp => exp.ObjectName == defImp.ObjectName) is ExportEntry defExp)
                    {
                        List<IEntry> impChildren = defImp.GetChildren();
                        List<IEntry> expChildren = defExp.GetChildren();
                        foreach (IEntry expChild in expChildren)
                        {
                            if (impChildren.FirstOrDefault(imp => imp.ObjectName == expChild.ObjectName) is ImportEntry matchingImp)
                            {
                                impChildren.Remove(matchingImp);
                            }
                            else
                            {
                                package.AddImport(new ImportEntry(package)
                                {
                                    idxLink = defImp.UIndex,
                                    ClassName = expChild.ClassName,
                                    ObjectName = expChild.ObjectName,
                                    PackageFile = defImp.PackageFile
                                });
                            }
                        }

                        foreach (IEntry impChild in impChildren)
                        {
                            EntryPruner.TrashEntries(package, impChild.GetAllDescendants().Prepend(impChild));
                        }
                    }
                }
            }

            //purge MaterialExpressions
            if (newGame == MEGame.ME3)
            {
                var entriesToTrash = new List<IEntry>();
                foreach (ExportEntry mat in package.Exports.Where(exp => exp.ClassName == "Material").ToList())
                {
                    entriesToTrash.AddRange(mat.GetAllDescendants());
                }
                EntryPruner.TrashEntries(package, entriesToTrash.ToHashSet());
            }

            EntryPruner.TrashIncompatibleEntries(package, oldGame, newGame);

            foreach (ExportEntry export in package.Exports)
            {
                //convert stack, or just get the pre-prop binary if no stack
                prePropBinary.Add(ExportBinaryConverter.ConvertPrePropBinary(export, newGame));

                PropertyCollection props = export.ClassName == "Class" ? null : EntryPruner.RemoveIncompatibleProperties(package, export.GetProperties(), export.ClassName, newGame);
                propCollections.Add(props);

                //convert binary data
                postPropBinary.Add(ExportBinaryConverter.ConvertPostPropBinary(export, newGame, props));

                //writes header in whatever format is correct for newGame
                export.RegenerateHeader(newGame, true);
            }

            package.setGame(newGame);

            for (int i = 0; i < package.Exports.Count; i++)
            {
                package.Exports[i].WritePrePropsAndPropertiesAndBinary(prePropBinary[i], propCollections[i], postPropBinary[i]);
            }

            if (newGame != MEGame.ME3)  //Fix Up Textures before Materials
            {
                foreach (ExportEntry texport in package.Exports.Where(exp => exp.IsTexture()))
                {
                    texport.WriteProperty(new BoolProperty(true, "NeverStream"));
                }
            }
            else if (package.Exports.Any(exp => exp.IsTexture() && Texture2D.GetTexture2DMipInfos(exp, null)
                                                                                .Any(mip => mip.storageType == StorageTypes.pccLZO
                                                                                         || mip.storageType == StorageTypes.pccZlib)))
            {
                //ME3 can't deal with compressed textures in a pcc, so we'll need to stuff them into a tfc
                tfcPath ??= Path.ChangeExtension(package.FilePath, "tfc");
                string tfcName = Path.GetFileNameWithoutExtension(tfcPath);
                using var tfc = new FileStream(tfcPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                Guid tfcGuid;
                if (tfc.Length >= 16)
                {
                    tfcGuid = tfc.ReadGuid();
                    tfc.SeekEnd();
                }
                else
                {
                    tfcGuid = Guid.NewGuid();
                    tfc.WriteGuid(tfcGuid);
                }

                foreach (ExportEntry texport in package.Exports.Where(exp => exp.IsTexture()))
                {
                    List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(texport, null);
                    var offsets = new List<int>();
                    foreach (Texture2DMipInfo mipInfo in mips)
                    {
                        if (mipInfo.storageType == StorageTypes.pccLZO || mipInfo.storageType == StorageTypes.pccZlib)
                        {
                            offsets.Add((int)tfc.Position);
                            byte[] mip = mipInfo.storageType == StorageTypes.pccLZO
                                ? TextureCompression.CompressTexture(Texture2D.GetTextureData(mipInfo), StorageTypes.extZlib)
                                : Texture2D.GetTextureData(mipInfo, false);
                            tfc.WriteFromBuffer(mip);
                        }
                    }
                    offsets.Add((int)tfc.Position);
                    texport.WriteBinary(ExportBinaryConverter.ConvertTexture2D(texport, package.Game, offsets, StorageTypes.extZlib));
                    texport.WriteProperty(new NameProperty(tfcName, "TextureFileCacheName"));
                    texport.WriteProperty(tfcGuid.ToGuidStructProp("TFCFileGuid"));
                }
            }
            if (oldGame == MEGame.ME3 && newGame != MEGame.ME3)
            {
                int idx = package.Names.IndexOf("location");
                if (idx >= 0)
                {
                    package.replaceName(idx,"Location");
                }
            }
            else if (newGame == MEGame.ME3)
            {
                int idx = package.Names.IndexOf("Location");
                if (idx >= 0)
                {
                    package.replaceName(idx,"location");
                }
            }

            if (newGame == MEGame.ME3) //Special handling where materials have been ported between games.
            {

                //change all materials to default material, but try to preserve diff and norm textures
                using var resourcePCC = MEPackageHandler.OpenMEPackageFromStream(Utilities.GetCustomAppResourceStream(MEGame.ME3));
                var defaultmaster = resourcePCC.Exports.First(exp => exp.ObjectName == "NormDiffMaterial");
                var materiallist = package.Exports.Where(exp => exp.ClassName == "Material" || exp.ClassName == "MaterialInstanceConstant").ToList();
                foreach (var mat in materiallist)
                {
                    Debug.WriteLine($"Fixing up {mat.FullPath}");
                    var masterMat = defaultmaster;
                    var hasDefaultMaster = true;
                    UIndex[] textures = Array.Empty<UIndex>();
                    if (mat.ClassName == "Material")
                    {
                        textures = ObjectBinary.From<Material>(mat).SM3MaterialResource.UniformExpressionTextures;
                        switch (mat.FullPath)
                        {
                            case "BioT_Volumetric.LAG_MM_Volumetric":
                            case "BioT_Volumetric.LAG_MM_FalloffSphere":
                            case "BioT_LevelMaster.Materials.Opaque_MM":
                            case "BioT_LevelMaster.Materials.GUI_Lit_MM":
                            case "BioT_LevelMaster.Materials.Signage.MM_GUIMaster_Emissive":
                            case "BioT_LevelMaster.Materials.Signage.MM_GUIMaster_Emissive_Fallback":
                            case "BioT_LevelMaster.Materials.Opaque_Standard_MM":
                            case "BioT_LevelMaster.Tech_Inset_MM":
                            case "BioT_LevelMaster.Tech_Border_MM":
                            case "BioT_LevelMaster.Brushed_Metal":
                                masterMat = resourcePCC.Exports.First(exp => exp.FullPath == mat.FullPath);
                                hasDefaultMaster = false;
                                break;
                            default:
                                break;
                        }
                    }
                    else if (mat.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    {
                        if (mat.GetProperty<ObjectProperty>("Parent") is ObjectProperty parentProp && package.GetEntry(parentProp.Value) is IEntry parent && parent.ClassName == "Material")
                        {
                            switch (parent.FullPath)
                            {
                                case "BioT_LevelMaster.Materials.Opaque_MM":
                                    masterMat = resourcePCC.Exports.First(exp => exp.FullPath == "Materials.Opaque_MM_INST");
                                    hasDefaultMaster = false;
                                    break;
                                case "BIOG_APL_MASTER_MATERIAL.Placeable_MM":
                                    masterMat = resourcePCC.Exports.First(exp => exp.FullPath == "Materials.Placeable_MM_INST");
                                    hasDefaultMaster = false;
                                    break;
                                case "BioT_LevelMaster.Materials.Opaque_Standard_MM":
                                    masterMat = resourcePCC.Exports.First(exp => exp.FullPath == "Materials.Opaque_Standard_MM_INST");
                                    hasDefaultMaster = false;
                                    break;
                                default:
                                    textures = ObjectBinary.From<MaterialInstance>(mat).SM3StaticPermutationResource.UniformExpressionTextures;
                                    break;
                            }

                            if (!hasDefaultMaster && mat.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> texParams)
                            {
                                textures = texParams.Select(structProp => new UIndex(structProp.GetProp<ObjectProperty>("ParameterValue")?.Value ?? 0)).ToArray();
                            }

                        }
                    }
                    else if (preserveMaterialInstances)
                    {
                        continue;
                    }
                    else if (mat.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> texParams)
                    {
                        textures = texParams.Select(structProp => new UIndex(structProp.GetProp<ObjectProperty>("ParameterValue")?.Value ?? 0)).ToArray();
                    }
                    else if (mat.GetProperty<ObjectProperty>("Parent") is ObjectProperty parentProp && package.GetEntry(parentProp.Value) is ExportEntry parent && parent.ClassName == "Material")
                    {
                        textures = ObjectBinary.From<Material>(parent).SM3MaterialResource.UniformExpressionTextures;
                    }

                    if (hasDefaultMaster)
                    {
                        EntryImporter.ReplaceExportDataWithAnother(masterMat, mat);
                        int norm = 0;
                        int diff = 0;
                        foreach (UIndex texture in textures)
                        {
                            if (package.GetEntry(texture) is IEntry tex)
                            {
                                if (diff == 0 && tex.ObjectName.Name.Contains("diff", StringComparison.OrdinalIgnoreCase))
                                {
                                    diff = texture;
                                }
                                else if (norm == 0 && tex.ObjectName.Name.Contains("norm", StringComparison.OrdinalIgnoreCase))
                                {
                                    norm = texture;
                                }
                            }
                        }
                        if (diff == 0)
                        {
                            diff = EntryImporter.GetOrAddCrossImportOrPackage("EngineMaterials.DefaultDiffuse", resourcePCC, package).UIndex;
                        }

                        var matBin = ObjectBinary.From<Material>(mat);
                        matBin.SM3MaterialResource.UniformExpressionTextures = new UIndex[] { norm, diff };
                        mat.WriteBinary(matBin);
                        mat.Class = package.Imports.First(imp => imp.ObjectName == "Material");
                    }
                    else if (mat.ClassName == "Material")
                    {
                        var mmparent = EntryImporter.GetOrAddCrossImportOrPackage(masterMat.ParentFullPath, resourcePCC, package);
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, masterMat, package, mmparent, true, out IEntry targetexp);
                        mat.ReplaceAllReferencesToThisOne(targetexp);
                        EntryPruner.TrashEntryAndDescendants(mat);
                    }
                    else if (mat.ClassName == "MaterialInstanceConstant")
                    {
                        try
                        {
                            var matprops = mat.GetProperties();
                            var parentlightguid = masterMat.GetProperty<StructProperty>("ParentLightingGuid");
                            matprops.AddOrReplaceProp(parentlightguid);
                            var mguid = masterMat.GetProperty<StructProperty>("m_Guid");
                            matprops.AddOrReplaceProp(mguid);
                            var lguid = masterMat.GetProperty<StructProperty>("LightingGuid");
                            matprops.AddOrReplaceProp(lguid);
                            var masterBin = ObjectBinary.From<MaterialInstance>(masterMat);
                            var matBin = ObjectBinary.From<MaterialInstance>(mat);
                            var staticResTextures3 = masterBin.SM3StaticPermutationResource.UniformExpressionTextures.ToList();
                            var newtextures3 = new List<UIndex>();
                            var staticResTextures2 = masterBin.SM2StaticPermutationResource.UniformExpressionTextures.ToList();
                            var newtextures2 = new List<UIndex>();
                            IEntry norm = null;
                            IEntry diff = null;
                            IEntry spec = null;
                            foreach (var texref in textures)
                            {
                                IEntry texEnt = package.GetEntry(texref);
                                string texName = texEnt?.ObjectName ?? "None";
                                if (texName.ToLowerInvariant().Contains("norm"))
                                    norm = texEnt;
                                else if (texName.ToLowerInvariant().Contains("diff"))
                                    diff = texEnt;
                                else if (texName.ToLowerInvariant().Contains("spec"))
                                    spec = texEnt;
                                else if (texName.ToLowerInvariant().Contains("msk"))
                                    spec = texEnt;
                            }

                            foreach (var texidx in staticResTextures2)
                            {
                                var masterTxt = resourcePCC.GetEntry(texidx);
                                IEntry newTxtEnt = masterTxt;
                                switch (masterTxt?.ObjectName.Name)
                                {
                                    case "DefaultDiffuse":
                                        if (diff != null)
                                            newTxtEnt = diff;
                                        break;
                                    case "DefaultNormal":
                                        if (norm != null)
                                            newTxtEnt = norm;
                                        break;
                                    case "Gray":  //Spec
                                        if (spec != null)
                                            newTxtEnt = spec;
                                        break;
                                    default:
                                        break;
                                }

                                var newtexidx = package.Exports.FirstOrDefault(x => x.FullPath == newTxtEnt.FullPath)?.UIndex ?? 0;
                                if (newtexidx == 0)
                                    newtexidx = package.Imports.FirstOrDefault(x => x.FullPath == newTxtEnt.FullPath)?.UIndex ?? 0;
                                if (newTxtEnt == masterTxt && newtexidx == 0)
                                {
                                    var texparent = EntryImporter.GetOrAddCrossImportOrPackage(newTxtEnt.ParentFullPath, resourcePCC, package);
                                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, newTxtEnt, package, texparent, true, out IEntry newtext);
                                    newtextures2.Add(newtext?.UIndex ?? 0);
                                }
                                else
                                {
                                    newtextures2.Add(newtexidx);
                                }
                            }

                            foreach (var texidx in staticResTextures3)
                            {
                                var masterTxt = resourcePCC.GetEntry(texidx);
                                IEntry newTxtEnt = masterTxt;
                                switch (masterTxt?.ObjectName)
                                {
                                    case "DefaultDiffuse":
                                        if (diff != null)
                                            newTxtEnt = diff;
                                        break;
                                    case "DefaultNormal":
                                        if (norm != null)
                                            newTxtEnt = norm;
                                        break;
                                    case "Gray":  //Spec
                                        if (spec != null)
                                            newTxtEnt = spec;
                                        break;
                                    default:
                                        break;
                                }
                                var newtexidx = package.Exports.FirstOrDefault(x => x.FullPath == newTxtEnt.FullPath)?.UIndex ?? 0;
                                if (newtexidx == 0)
                                    newtexidx = package.Imports.FirstOrDefault(x => x.FullPath == newTxtEnt.FullPath)?.UIndex ?? 0;
                                if (newTxtEnt == masterTxt && newtexidx == 0)
                                {
                                    var texparent = EntryImporter.GetOrAddCrossImportOrPackage(newTxtEnt.ParentFullPath, resourcePCC, package);
                                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, newTxtEnt, package, texparent, true, out IEntry newtext);
                                    newtextures3.Add(newtext?.UIndex ?? 0);
                                }
                                else
                                {
                                    newtextures3.Add(newtexidx);
                                }
                            }
                            masterBin.SM2StaticPermutationResource.UniformExpressionTextures = newtextures2.ToArray();
                            masterBin.SM3StaticPermutationResource.UniformExpressionTextures = newtextures3.ToArray();
                            mat.WritePropertiesAndBinary(matprops, masterBin);
                        }
                        catch
                        {
                            Debug.WriteLine("MaterialInstanceConversion error");
                        }
                    }
                }
            }
        }
    }
}
