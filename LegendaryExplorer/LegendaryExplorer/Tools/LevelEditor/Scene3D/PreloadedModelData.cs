using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public class PreloadedModelData
{
    public object meshObject;
    public List<ModelPreviewSection> sections;
    public List<IEntry> Materials;

    public static PreloadedModelData LoadModel(ExportEntry export, PackageCache assetCache)
    {
        List<string> alreadyLoadedImportMaterials = [];
        var modelComp = ObjectBinary.From<Model>(export);
        var pmd = new PreloadedModelData
        {
            meshObject = modelComp,
            sections = [],
            Materials = [],
        };
        foreach (var mcExp in modelComp.Export.FileRef.Exports.Where(x =>
            x.ClassName == "ModelComponent" && !x.IsDefaultObject))
        {
            var mc = ObjectBinary.From<ModelComponent>(mcExp);
            if (mc.Model == modelComp.Self)
            {
                foreach (var element in mc.Elements)
                {
                    if (export == null) return pmd;
                    if (export.FileRef.IsUExport(element.Material))
                    {
                        ExportEntry entry = export.FileRef.GetUExport(element.Material);
                        AddMaterial(pmd.Materials, entry);
                    }
                    else if (export.FileRef.TryGetImport(element.Material, out var matImp) &&
                             alreadyLoadedImportMaterials.All(x => x != matImp.InstancedFullPath))
                    {
                        var extMaterialExport = EntryImporter.ResolveImport(matImp, assetCache);
                        if (extMaterialExport != null)
                        {
                            AddMaterial(pmd.Materials, extMaterialExport);
                            alreadyLoadedImportMaterials.Add(extMaterialExport.InstancedFullPath);
                        }
                        else
                        {
                            Debug.WriteLine("Could not find import material from FModelElement.");
                            Debug.WriteLine("Import material: " +
                                            export.FileRef.GetEntryString(element.Material));
                        }
                    }
                }
            }
        }
        return pmd;
    }
    public static PreloadedModelData LoadModelComponent(ExportEntry export, PackageCache assetCache)
    {
        var modelComp = ObjectBinary.From<ModelComponent>(export);
        var pmd = new PreloadedModelData
        {
            meshObject = modelComp,
            sections = [],
            Materials = [],
        };

        foreach (var element in modelComp.Elements)
        {
            if (export != null)
            {
                if (export.FileRef.TryGetUExport(element.Material, out var matExp))
                {
                    AddMaterial(pmd.Materials, matExp);
                    pmd.sections.Add(new ModelPreviewSection(matExp.InstancedFullPath, 0, 3)); //???
                }
                else if (export.FileRef.TryGetImport(element.Material, out var matImp))
                {
                    var extMaterialExport = EntryImporter.ResolveImport(matImp, assetCache);
                    //var extMaterialExport = ModelPreview.FindExternalAsset(matImp, pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                    if (extMaterialExport != null)
                    {
                        AddMaterial(pmd.Materials, extMaterialExport);
                    }
                    else
                    {
                        Debug.WriteLine("Could not find import material from section.");
                        Debug.WriteLine("Import material: " + export.FileRef.GetEntryString(element.Material));
                    }
                }
            }
        }
        return pmd;
    }

    private static void AddMaterial(List<IEntry> texturePreviewMaterials, ExportEntry entry)
    {
        if (texturePreviewMaterials.Any(x => x.InstancedFullPath == entry.InstancedFullPath))
            return; //already cached

        texturePreviewMaterials.Add(entry);
    }
}
