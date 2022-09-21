using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public ExportEntry Export;
        public List<IEntry> Textures = new();

        //public List<TextureParam> Textures = new List<TextureParam>();

        //public struct TextureParam
        //{
        //    public int TexIndex;
        //    public string Desc;
        //}

        public MaterialInstanceConstant(ExportEntry export, PackageCache assetCache = null)
        {
            Export = export;
            ReadMaterial(export, assetCache);
        }

        private void ReadMaterial(ExportEntry export, PackageCache assetCache = null)
        {
            if (export.ClassName == "Material")
            {
                var parsedMaterial = ObjectBinary.From<Material>(export);
                foreach (int v in parsedMaterial.SM3MaterialResource.UniformExpressionTextures)
                {
                    IEntry tex = export.FileRef.GetEntry(v);
                    if (tex != null)
                    {
                        Textures.Add(tex);
                    }
                }
            }
            else if (export.ClassName == "RvrEffectsMaterialUser")
            {
                var props = export.GetProperties();
                if (export.GetProperty<ObjectProperty>("m_pBaseMaterial") is ObjectProperty baseProp)
                {
                    // This is an instance... maybe?
                    if (baseProp.Value > 0)
                    {
                        // Local export
                        ReadMaterial(export.FileRef.GetUExport(baseProp.Value));
                    }
                    else
                    {
                        ImportEntry ie = export.FileRef.GetImport(baseProp.Value);
                        var externalEntry = EntryImporter.ResolveImport(ie, null, assetCache);
                        if (externalEntry != null)
                        {
                            ReadMaterial(externalEntry);
                        }
                    }
                }
            }
            else if (export.ClassName == "MaterialInstanceConstant")
            {
                //Read Local
                if (export.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> textureparams)
                {
                    foreach (var param in textureparams)
                    {
                        var paramValue = param.GetProp<ObjectProperty>("ParameterValue");
                        var texntry = export.FileRef.GetEntry(paramValue.Value);
                        if (texntry?.ClassName == "Texture2D" && !Textures.Contains(texntry))
                        {
                            Textures.Add(texntry);
                        }
                    }
                }

                if (export.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedTextures") is ArrayProperty<ObjectProperty> textures)
                {
                    foreach (var obj in textures)
                    {
                        var texntry = export.FileRef.GetEntry(obj.Value);
                        if (texntry.ClassName == "Texture2D" && !Textures.Contains(texntry))
                        {
                            Textures.Add(texntry);
                        }
                    }
                }

                //Read parent
                if (export.GetProperty<ObjectProperty>("Parent") is ObjectProperty parentObjProp)
                {
                    // This is an instance... maybe?
                    if (parentObjProp.Value > 0)
                    {
                        // Local export
                        ReadMaterial(export.FileRef.GetUExport(parentObjProp.Value));
                    }
                    else
                    {
                        ImportEntry ie = export.FileRef.GetImport(parentObjProp.Value);
                        var externalEntry = EntryImporter.ResolveImport(ie, null, assetCache); 
                        if (externalEntry != null)
                        {
                            ReadMaterial(externalEntry);
                        }
                    }
                }
            }
        }
    }
}
