using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public readonly ExportEntry Export;
        public readonly HashSet<IEntry> Textures = [];


        public MaterialInstanceConstant(ExportEntry export, PackageCache assetCache = null, bool resolveImports = true)
        {
            Export = export;
            ReadMaterial(export, assetCache, resolveImports);
        }

        private void ReadMaterial(ExportEntry export, PackageCache assetCache, bool resolveImports)
        {
            if (export.ClassName == "Material")
            {
                var parsedMaterial = ObjectBinary.From<Material>(export);
                ReadBaseMaterial(export, assetCache, parsedMaterial);
            }
            else if (export.ClassName == "RvrEffectsMaterialUser")
            {
                var props = export.GetProperties();
                if (props.GetProp<ObjectProperty>("m_pBaseMaterial") is ObjectProperty baseProp)
                {
                    // This is an instance... maybe?
                    if (baseProp.Value > 0)
                    {
                        // Local export
                        ReadMaterial(export.FileRef.GetUExport(baseProp.Value), assetCache, resolveImports);
                    }
                    else
                    {
                        ImportEntry ie = export.FileRef.GetImport(baseProp.Value);
                        if (resolveImports)
                        {
                            var externalEntry = EntryImporter.ResolveImport(ie, assetCache);
                            if (externalEntry != null)
                            {
                                ReadMaterial(externalEntry, assetCache, resolveImports);
                            }
                        }
                        else
                        {
                            Textures.Add(ie);
                        }
                    }
                }
            }
            else if (export.IsA("MaterialInstanceConstant"))
            {
                var props = export.GetProperties(packageCache: assetCache);

                //Read Local
                ReadMaterialInstanceConstant(export, props);

                //Read parent
                if (props.GetProp<ObjectProperty>("Parent") is ObjectProperty parentObjProp)
                {
                    // This is an instance... maybe?
                    if (parentObjProp.Value > 0)
                    {
                        // Local export
                        ReadMaterial(export.FileRef.GetUExport(parentObjProp.Value), assetCache, resolveImports);
                    }
                    else
                    {
                        ImportEntry ie = export.FileRef.GetImport(parentObjProp.Value);
                        if (resolveImports)
                        {
                            var externalEntry = EntryImporter.ResolveImport(ie, assetCache);
                            if (externalEntry != null)
                            {
                                ReadMaterial(externalEntry, assetCache, resolveImports);
                            }
                        }
                        else
                        {
                            Textures.Add(ie);
                        }
                    }
                }
            }
        }

        protected virtual void ReadBaseMaterial(ExportEntry mat, PackageCache assetCache, Material parsedMaterial)
        {
            foreach (int uIndex in parsedMaterial.SM3MaterialResource.UniformExpressionTextures)
            {
                IEntry tex = mat.FileRef.GetEntry(uIndex);
                if (tex != null)
                {
                    Textures.Add(tex);
                }
            }
        }

        protected virtual void ReadMaterialInstanceConstant(ExportEntry matInst, PropertyCollection props)
        {
            if (props.GetProp<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> textureparams)
            {
                foreach (var param in textureparams)
                {
                    var paramValue = param.GetProp<ObjectProperty>("ParameterValue");
                    var texntry = matInst.FileRef.GetEntry(paramValue.Value);
                    if (texntry is not null)
                    {
                        Textures.Add(texntry);
                    }
                }
            }

            if (props.GetProp<ArrayProperty<ObjectProperty>>("ReferencedTextures") is ArrayProperty<ObjectProperty> textures)
            {
                foreach (var obj in textures)
                {
                    var texntry = matInst.FileRef.GetEntry(obj.Value);
                    if (texntry is not null)
                    {
                        Textures.Add(texntry);
                    }
                }
            }
        }

        public static IEnumerable<IEntry> GetTextures(ExportEntry export, PackageCache assetCache = null, bool resolveImports = true)
        {
            var mic = new MaterialInstanceConstant(export, assetCache, resolveImports);
            return mic.Textures.Where(entry => entry.ClassName == "Texture2D"); //no texturecubes
        }
    }
}
