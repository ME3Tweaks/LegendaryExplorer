using System;
using System.Collections.Generic;
using System.Linq;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3Explorer.MaterialViewer
{
    public static class ShaderCacheManipulator
    {
        public static void CompactShaderCaches(IMEPackage mePackage)
        {
            var staticParamSetsInFile = new HashSet<StaticParameterSet>();
            //figure out which MaterialShaderMaps to keep
            foreach (ExportEntry export in mePackage.Exports)
            {
                if (export.ClassName == "Material")
                {
                    staticParamSetsInFile.Add((StaticParameterSet)ObjectBinary.From<Material>(export).SM3MaterialResource.ID);
                }
                else if (export.IsA("MaterialInstance") && export.GetProperty<BoolProperty>("bHasStaticPermutationResource"))
                {
                    staticParamSetsInFile.Add(ObjectBinary.From<MaterialInstance>(export).SM3StaticParameterSet);
                }
            }

            var compactedShaderCache = new ShaderCache
            {
                Shaders = new OrderedMultiValueDictionary<Guid, Shader>(),
                MaterialShaderMaps = new OrderedMultiValueDictionary<StaticParameterSet, MaterialShaderMap>()
            };

            //add MaterialShaderMaps
            foreach (ExportEntry shaderCacheExport in mePackage.Exports.Where(exp => exp.ClassName == "ShaderCache"))
            {
                var shaderCache = ObjectBinary.From<ShaderCache>(shaderCacheExport);
                compactedShaderCache.ShaderTypeCRCMap = shaderCache.ShaderTypeCRCMap;
                compactedShaderCache.VertexFactoryTypeCRCMap = shaderCache.VertexFactoryTypeCRCMap;
                foreach ((StaticParameterSet key, MaterialShaderMap msm) in shaderCache.MaterialShaderMaps)
                {
                    if (staticParamSetsInFile.Any(sps => sps == key) && !compactedShaderCache.MaterialShaderMaps.ContainsKey(key))
                    {
                        compactedShaderCache.MaterialShaderMaps.Add(key, msm);
                    }
                }
            }

            //Figure out which shaders to keep
            var shaderGuids = new HashSet<Guid>();
            foreach ((_, MaterialShaderMap materialShaderMap) in compactedShaderCache.MaterialShaderMaps)
            {
                foreach ((_, ShaderReference shaderRef) in materialShaderMap.Shaders)
                {
                    shaderGuids.Add(shaderRef.Id);
                }

                foreach (MeshShaderMap meshShaderMap in materialShaderMap.MeshShaderMaps)
                {
                    foreach ((_, ShaderReference shaderRef) in meshShaderMap.Shaders)
                    {
                        shaderGuids.Add(shaderRef.Id);
                    }
                }
            }

            ExportEntry firstShaderCache = null;
            //add Shaders
            foreach (ExportEntry shaderCacheExport in mePackage.Exports.Where(exp => exp.ClassName == "ShaderCache"))
            {
                var shaderCache = ObjectBinary.From<ShaderCache>(shaderCacheExport);
                foreach ((Guid key, Shader shader) in shaderCache.Shaders)
                {
                    if (shaderGuids.Contains(key) && !compactedShaderCache.Shaders.ContainsKey(key))
                    {
                        compactedShaderCache.Shaders.Add(key, shader);
                    }
                }

                if (firstShaderCache == null)
                {
                    firstShaderCache = shaderCacheExport;
                }
                else
                {
                    EntryPruner.TrashEntryAndDescendants(shaderCacheExport);
                }
            }

            firstShaderCache.WriteBinary(compactedShaderCache);
        }
    }
}
