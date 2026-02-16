using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorerCore.Helpers
{
    public static class MaterialHelper
    {
        /// <summary>
        /// Gathers all textures that might be used by a given material, grouped by parameter name. If this is a MaterialInstanceConstant that overrides a parent value, it will return the overridden value and not the parent value.
        /// Can optionally include the Uniform Expression Textures, which do not have parameter names and might be overridden. In some cases, this is the only place where important textures are located.
        /// </summary>
        /// <param name="materialExport">The material export from which to gather textures.</param>
        /// <param name="baseTextures">the textures from the base material uniform expression textures.</param>
        /// <param name="includeUniformExpressionTextures">Whether to include the uniform expression textures</param>
        private static Dictionary<string, ExportEntry> GetMaterialTextures(ExportEntry materialExport, out List<ExportEntry> baseTextures, bool includeUniformExpressionTextures = true)
        {
            Dictionary<string, ExportEntry> textureExports = [];
            List<ExportEntry> tempBaseTextures = [];
            var cache = new PackageCache();

            delegateByType(materialExport);
            baseTextures = tempBaseTextures;
            return textureExports;

            void delegateByType(ExportEntry materialEntry)
            {
                var selectedEntryClass = materialEntry.ClassName;
                if (materialEntry.ClassName == "Material")
                {
                    ExportBaseMaterialTextures(materialEntry, includeUniformExpressionTextures);
                }
                else if (materialEntry.IsA("MaterialInstanceConstant"))
                {
                    ExportMICTextures(materialEntry);
                }
                else if (materialEntry.IsA("RvrEffectsMaterialUser"))
                {
                    ExportEffectMatUserTextures(materialEntry);

                }
                else
                {
                    return;
                }
            }

            void ExportEffectMatUserTextures(ExportEntry effectsMatEntry)
            {
                // for this, just get the base material stuff
                if (effectsMatEntry.GetProperty<ObjectProperty>("m_pBaseMaterial", cache).TryResolveExport(effectsMatEntry.FileRef, cache, out var baseMat))
                {
                    delegateByType(baseMat);
                }
            }

            void ExportMICTextures(ExportEntry micExport)
            {
                // get anything from the texture Parameters
                var texParamsProp = micExport.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues", cache);
                if (texParamsProp != null)
                {
                    foreach (var texParam in texParamsProp)
                    {
                        var paramName = texParam.GetProp<NameProperty>("ParameterName").Value.Instanced;
                        if (!textureExports.ContainsKey(paramName) && texParam.GetProp<ObjectProperty>("ParameterValue").TryResolveExport(micExport.FileRef, cache, out var value))
                        {
                            // skip the really dumb textures
                            if (value.ObjectNameString.StartsWith("GBL_ARM_ALL"))
                            {
                                continue;
                            }
                            if (value.IsA("Texture2D"))
                            {
                                textureExports.Add(paramName, value);
                            }
                        }
                    }
                }
                // then go to the parent, if it exists
                if (micExport.GetProperty<ObjectProperty>("Parent", cache).TryResolveExport(micExport.FileRef, cache, out var parent))
                {
                    delegateByType(parent);
                }
            }

            void ExportBaseMaterialTextures(ExportEntry baseMatEntry, bool includeUniformExpressionTextures = true)
            {
                if (includeUniformExpressionTextures)
                {
                    var matBin = ObjectBinary.From<Material>(baseMatEntry);
                    if (matBin.SM2MaterialResource.UniformExpressionTextures != null)
                    {
                        foreach (var texIdx in matBin.SM2MaterialResource.UniformExpressionTextures)
                        {
                            if (baseMatEntry.FileRef.TryGetUExport(texIdx, out var tex))
                            {
                                // skip the really dumb textures
                                if (tex.ObjectNameString.StartsWith("GBL_ARM_ALL"))
                                {
                                    continue;
                                }
                                if (tex.IsA("Texture2D"))
                                {
                                    tempBaseTextures.Add(tex);
                                }
                            }
                        }
                    }

                    if (matBin.SM3MaterialResource.UniformExpressionTextures != null)
                    {
                        foreach (var texIdx in matBin.SM3MaterialResource.UniformExpressionTextures)
                        {
                            if (baseMatEntry.FileRef.TryGetEntry(texIdx, out var texEntry))
                            {
                                ExportEntry texExport;
                                // skip the really dumb textures
                                if (texEntry.ObjectNameString.StartsWith("GBL_ARM_ALL"))
                                {
                                    continue;
                                }
                                if (texEntry is ImportEntry importTex)
                                {
                                    EntryImporter.TryResolveImport(importTex, out texExport);
                                }
                                else
                                {
                                    texExport = texEntry as ExportEntry;
                                }
                                if (texEntry.IsA("Texture2D"))
                                {
                                    tempBaseTextures.Add(texExport);
                                }
                            }
                        }
                    }
                }

                var expressions = baseMatEntry.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
                if (expressions == null)
                {
                    return;
                }

                // Read default expressions
                foreach (var expr in expressions.Select(x => x.ResolveToEntry(baseMatEntry.FileRef)).Where(x => x != null && x.IsA("MaterialExpressionTextureSampleParameter")).OfType<ExportEntry>())
                {
                    var paramName = expr.GetProperty<NameProperty>("ParameterName")?.Value.Instanced ?? "None";

                    if (!textureExports.ContainsKey(paramName) && expr.GetProperty<ObjectProperty>("Texture").TryResolveExport(baseMatEntry.FileRef, cache, out var value))
                    {
                        if (value.IsA("Texture2D"))
                        {
                            textureExports.Add(paramName, value);
                        }
                    }
                }
            }
        }

        public static IReadOnlyDictionary<string, ExportEntry> GetMaterialTextures(this ExportEntry materialExport)
        {
            return GetMaterialTextures(materialExport, out var _, false);
        }

        public static IReadOnlyDictionary<string, ExportEntry> GetMaterialTextures(this ExportEntry materialExport, out IReadOnlyList<ExportEntry> baseTextures)
        {
            var paramTextures = GetMaterialTextures(materialExport, out var baseTextureList, true);
            baseTextures = baseTextureList;
            return paramTextures;
        }

        public static ExportEntry GetBaseMaterial(this ExportEntry materialExport, PackageCache cache = null)
        {
            switch (materialExport.ClassName)
            {
                case "BioMaterialInstanceConstant":
                case "MaterialInstanceConstant":
                    cache ??= new PackageCache();
                    var parent = materialExport.GetProperty<ObjectProperty>("Parent").ResolveToExport(materialExport.FileRef, cache);
                    return parent?.GetBaseMaterial(cache);
                case "RvrEffectsMaterialUser":
                    cache ??= new PackageCache();
                    var baseMat = materialExport.GetProperty<ObjectProperty>("m_pBaseMaterial").ResolveToExport(materialExport.FileRef, cache);
                    return baseMat?.GetBaseMaterial(cache);
                case "Material":
                    return materialExport;
                default:
                    return null;
            }
        }

        public static void FindBestDiffAndNormForMaterial(ExportEntry material, out ExportEntry diffuseTexture, out ExportEntry normalTexture)
        {
            diffuseTexture = null;
            normalTexture = null;
            var paramTextures = material.GetMaterialTextures(out var baseTextures);
            var baseMaterial = material.GetBaseMaterial().ObjectNameString;
            // for specific materials, we know which parameter to look at for norm and diff, and matching by name sometimes picks the wrong one or can't find it at all
            // but we can't just blindly go by parameter name either, or we will grab things like Teeth Diff instead of Scalp Diff. 
            switch (baseMaterial)
            {
                case "HMN_HED_NPC_Scalp_Mat_1a":
                    paramTextures.TryGetValue("HED_Scalp_Diff", out diffuseTexture);
                    paramTextures.TryGetValue("HED_Scalp_Norm", out normalTexture);
                    return;
                case "HMM_CTH_MASTER_MAT":
                    paramTextures.TryGetValue("HMM_ARM_ALL_Diff_Stack", out diffuseTexture);
                    paramTextures.TryGetValue("HMM_ARM_ALL_Norm_Stack", out normalTexture);
                    return;
                case "HMN_HED_LASH_Unlit_MASTER_MAT":
                    paramTextures.TryGetValue("HED_Lash_Diff", out diffuseTexture);
                    return;
                case "ElevatorConsole02_Material01":
                    diffuseTexture = baseTextures[2];
                    normalTexture = baseTextures[0];
                    return;
                case "BIOG_SOV_INGAME_MASTER_A_MAT":
                case "BIOG_SOV_INGAME_MASTER_MAT":
                    diffuseTexture = baseTextures[6];
                    normalTexture = baseTextures[0];
                    return;
                case "BIOG_SOV_Damage_Master_Mat":
                    diffuseTexture = baseTextures[8];
                    normalTexture = baseTextures[0];
                    return;
                // consider Galaxy_Holo_Mat
                default:
                    break;
            }
            string matPackage = null;
            if (material.Parent != null)
            {
                matPackage = material.Parent.FullPath.ToLower();
            }
            IEnumerable<ExportEntry> allTextures = [.. paramTextures.Values, .. baseTextures];
            //search for textures under the same package as the export
            foreach (var textureEntry in allTextures)
            {
                var texObjectName = textureEntry.FullPath.ToLower();
                if ((matPackage == null || texObjectName.StartsWith(matPackage)) && texObjectName.Contains("diff"))
                {
                    // we have found the diffuse texture!
                    diffuseTexture ??= textureEntry;
                }
                else if ((matPackage == null || texObjectName.StartsWith(matPackage)) && texObjectName.Contains("norm"))
                {
                    // we have found the normal texture!
                    normalTexture ??= textureEntry;
                }
            }

            foreach (var textureEntry in allTextures)
            {
                var texObjectName = textureEntry.ObjectName.Name.ToLower();
                if (texObjectName.Contains("diff") || texObjectName.Contains("tex"))
                {
                    // we have found the diffuse texture!
                    diffuseTexture ??= textureEntry;
                }
                if (texObjectName.Contains("norm"))
                {
                    // we have found the normal texture!
                    normalTexture ??= textureEntry;
                }
            }
            foreach (var texparam in allTextures)
            {
                var texObjectName = texparam.ObjectName.Name.ToLower();

                if (texObjectName.Contains("detail"))
                {
                    // I guess a detail texture is good enough if we didn't return for a diffuse texture earlier...
                    diffuseTexture ??= texparam;
                }
            }
            foreach (var texparam in allTextures)
            {
                var texObjectName = texparam.ObjectName.Name.ToLower();
                if (!texObjectName.Contains("norm") && !texObjectName.Contains("opac"))
                {
                    //Anything is better than nothing I suppose
                    diffuseTexture ??= texparam;
                }
            }
            return;
        }
    }
}
