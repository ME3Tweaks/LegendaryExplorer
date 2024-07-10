using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Shaders
{
    public static class ShaderCacheManipulator
    {
        public static void CompactSeekFreeShaderCaches(IMEPackage pcc, string gamePathOverride = null)
        {
            var staticParamSetsInFile = new HashSet<StaticParameterSet>();
            //figure out which MaterialShaderMaps to keep
            foreach (ExportEntry export in pcc.Exports)
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
            RefShaderCacheReader.RemoveStaticParameterSetsThatAreInTheGlobalCache(staticParamSetsInFile, pcc.Game, gamePathOverride);

            var localCache = pcc.FindExport("SeekFreeShaderCache");

            localCache?.WriteBinary(GetLocalShaders(staticParamSetsInFile, localCache));
        }

        public static ShaderCache GetLocalShadersForMaterials(List<ExportEntry> materials, string gamePathOverride = null)
        {
            if (materials.Count is 0)
            {
                return null;
            }
            IMEPackage pcc = materials[0].FileRef;

            var localCache = pcc.FindExport("SeekFreeShaderCache");
            if (localCache is null)
            {
                return null;
            }

            var staticParamSets = new HashSet<StaticParameterSet>();

            foreach (ExportEntry export in materials)
            {
                if (export.ClassName == "Material")
                {
                    staticParamSets.Add((StaticParameterSet)ObjectBinary.From<Material>(export).SM3MaterialResource.ID);
                }
                else if (export.IsA("MaterialInstance") && export.GetProperty<BoolProperty>("bHasStaticPermutationResource"))
                {
                    staticParamSets.Add(ObjectBinary.From<MaterialInstance>(export).SM3StaticParameterSet);
                }
            }

            //can happen if list of exports passed in does not contain any materials
            if (staticParamSets.Count is 0)
            {
                return null;
            }
            RefShaderCacheReader.RemoveStaticParameterSetsThatAreInTheGlobalCache(staticParamSets, pcc.Game, gamePathOverride);
            if (staticParamSets.Count is 0)
            {
                return null;
            }

            return GetLocalShaders(staticParamSets, localCache);
        }

        public static ShaderCache GetLocalShaders(HashSet<StaticParameterSet> staticParamSets, ExportEntry seekFreeShaderCacheExport)
        {
            var localCache = seekFreeShaderCacheExport.GetBinaryData<ShaderCache>();

            var tempCache = new ShaderCache
            {
                Shaders = new (),
                MaterialShaderMaps = new (),
                ShaderTypeCRCMap = localCache.ShaderTypeCRCMap,
                VertexFactoryTypeCRCMap = localCache.VertexFactoryTypeCRCMap
            };

            //get corresponding MaterialShaderMap for each StaticParameterSet
            foreach ((StaticParameterSet key, MaterialShaderMap msm) in localCache.MaterialShaderMaps)
            {
                if (staticParamSets.Contains(key) && !tempCache.MaterialShaderMaps.ContainsKey(key))
                {
                    tempCache.MaterialShaderMaps.Add(key, msm);
                }
            }

            //get the guids for every shader referenced by the MaterialShaderMaps
            var shaderGuids = new HashSet<Guid>();
            foreach (MaterialShaderMap materialShaderMap in tempCache.MaterialShaderMaps.Values)
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
            foreach ((Guid key, Shader shader) in localCache.Shaders)
            {
                if (shaderGuids.Contains(key) && !tempCache.Shaders.ContainsKey(key))
                {
                    tempCache.Shaders.Add(key, shader);
                }
            }

            return tempCache;
        }

        public static void AddShadersToFile(IMEPackage destFile, ShaderCache shadersToAdd)
        {
            const string seekfreeshadercache = "SeekFreeShaderCache";
            var destCacheExport = destFile.FindExport(seekfreeshadercache);

            if (destCacheExport is null)
            {
                destFile.AddExport(new ExportEntry(destFile, 0, seekfreeshadercache, BitConverter.GetBytes(-1), binary: shadersToAdd)
                {
                    Class = destFile.GetEntryOrAddImport("Engine.ShaderCache", "Class"),
                    ObjectFlags = EObjectFlags.LoadForClient | EObjectFlags.LoadForEdit | EObjectFlags.LoadForServer | EObjectFlags.Standalone
                });
                return;
            }

            var destCache = destCacheExport.GetBinaryData<ShaderCache>();

            foreach ((StaticParameterSet key, MaterialShaderMap materialShaderMap) in shadersToAdd.MaterialShaderMaps)
            {
                if (!destCache.MaterialShaderMaps.ContainsKey(key))
                {
                    destCache.MaterialShaderMaps.Add(key, materialShaderMap);
                }
            }

            foreach ((Guid key, Shader shader) in shadersToAdd.Shaders)
            {
                if (!destCache.Shaders.ContainsKey(key))
                {
                    destCache.Shaders.Add(key, shader);
                }
            }

            destCacheExport.WriteBinary(destCache);
        }

        public static List<ExportEntry> GetBrokenMaterials(IMEPackage pcc, string gamePathOverride = null)
        {
            var brokenMaterials = new List<ExportEntry>();
            if (!pcc.Game.IsMEGame())
            {
                return brokenMaterials;
            }
            var staticParamSetsToMaterialsDict = new Dictionary<StaticParameterSet, List<ExportEntry>>();

            foreach (ExportEntry export in pcc.Exports)
            {
                if (export.ClassName == "Material")
                {
                    staticParamSetsToMaterialsDict.AddToListAt((StaticParameterSet)ObjectBinary.From<Material>(export).SM3MaterialResource.ID, export);
                }
                else if (export.IsA("MaterialInstance") && export.GetProperty<BoolProperty>("bHasStaticPermutationResource"))
                {
                    staticParamSetsToMaterialsDict.AddToListAt(ObjectBinary.From<MaterialInstance>(export).SM3StaticParameterSet, export);
                }
            }
            if (staticParamSetsToMaterialsDict.Count is 0)
            {
                return brokenMaterials;
            }
            HashSet<StaticParameterSet> staticParamSets = staticParamSetsToMaterialsDict.Keys.ToHashSet();
            RefShaderCacheReader.RemoveStaticParameterSetsThatAreInTheGlobalCache(staticParamSets, pcc.Game, gamePathOverride);
            if (staticParamSets.Count is 0)
            {
                return brokenMaterials;
            }

            if (pcc.FindExport("SeekFreeShaderCache") is ExportEntry localCacheExport)
            {
                var localCache = localCacheExport.GetBinaryData<ShaderCache>();
                foreach ((StaticParameterSet key, _) in localCache.MaterialShaderMaps)
                {
                    staticParamSets.Remove(key);
                }
            }

            foreach (StaticParameterSet staticParamSet in staticParamSets)
            {
                brokenMaterials.AddRange(staticParamSetsToMaterialsDict[staticParamSet]);
            }
            return brokenMaterials;
        }
    }
}
