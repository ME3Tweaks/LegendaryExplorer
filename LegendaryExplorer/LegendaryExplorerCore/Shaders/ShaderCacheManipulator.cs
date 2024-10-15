using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Collections;
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
                Shaders = new(),
                MaterialShaderMaps = new(),
                ShaderTypeCRCMap = localCache.ShaderTypeCRCMap,
                VertexFactoryTypeCRCMap = localCache.VertexFactoryTypeCRCMap
            };

            //get corresponding MaterialShaderMap for each StaticParameterSet
            foreach ((StaticParameterSet key, MaterialShaderMap msm) in localCache.MaterialShaderMaps)
            {
                if (staticParamSets.Contains(key))
                {
                    tempCache.MaterialShaderMaps.TryAdd(key, msm);
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
                if (shaderGuids.Contains(key))
                {
                    tempCache.Shaders.TryAdd(key, shader);
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
                destCache.MaterialShaderMaps.TryAdd(key, materialShaderMap);
            }

            foreach ((Guid key, Shader shader) in shadersToAdd.Shaders)
            {
                destCache.Shaders.TryAdd(key, shader);
            }

            destCacheExport.WriteBinary(destCache);
        }

        /// <summary>
        /// For locking shader cache file
        /// </summary>
        private static object shaderCacheReaderObj = new object();

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

            if (pcc.FindExport("SeekFreeShaderCache") is ExportEntry localCacheExport)
            {
                var localCache = localCacheExport.GetBinaryData<ShaderCache>();
                foreach ((StaticParameterSet key, _) in localCache.MaterialShaderMaps)
                {
                    staticParamSets.Remove(key);
                }
            }

            if (staticParamSets.Count is 0)
            {
                return brokenMaterials;
            }

            lock (shaderCacheReaderObj)
            {
                RefShaderCacheReader.RemoveStaticParameterSetsThatAreInTheGlobalCache(staticParamSets, pcc.Game, gamePathOverride);
            }

            foreach (StaticParameterSet staticParamSet in staticParamSets)
            {
                brokenMaterials.AddRange(staticParamSetsToMaterialsDict[staticParamSet]);
            }
            return brokenMaterials;
        }

        public static bool IsMaterialBroken(ExportEntry export, string gamePathOverride = null)
        {
            if (!export.Game.IsMEGame())
                return false; // We can't detect

            var staticParamSetsToMaterialsDict = new Dictionary<StaticParameterSet, List<ExportEntry>>();

            if (export.ClassName == "Material")
            {
                staticParamSetsToMaterialsDict.AddToListAt((StaticParameterSet)ObjectBinary.From<Material>(export).SM3MaterialResource.ID, export);
            }
            else if (export.IsA("MaterialInstance") && export.GetProperty<BoolProperty>("bHasStaticPermutationResource"))
            {
                staticParamSetsToMaterialsDict.AddToListAt(ObjectBinary.From<MaterialInstance>(export).SM3StaticParameterSet, export);
            }
            if (staticParamSetsToMaterialsDict.Count is 0)
            {
                return false;
            }
            HashSet<StaticParameterSet> staticParamSets = staticParamSetsToMaterialsDict.Keys.ToHashSet();
            lock (shaderCacheReaderObj)
            {
                RefShaderCacheReader.RemoveStaticParameterSetsThatAreInTheGlobalCache(staticParamSets, export.Game, gamePathOverride);
            }

            if (staticParamSets.Count is 0)
            {
                // All shaders found
                return false;
            }

            if (export.FileRef.FindExport("SeekFreeShaderCache") is ExportEntry localCacheExport)
            {
                var localCache = localCacheExport.GetBinaryData<ShaderCache>();
                foreach ((StaticParameterSet key, _) in localCache.MaterialShaderMaps)
                {
                    staticParamSets.Remove(key);
                }
            }

            return staticParamSets.Any();
        }

        /// <summary>
        /// Copies the shader map used by this material, and its referenced shader files, to local cache and updates the material to use them with a new guid. The material should be renamed to ensure memory collisions do not occur.
        /// </summary>
        /// <param name="material">Material to move shaders to</param>
        /// <param name="cloneIfLocal">Clones the material even if it's already in the local cache</param>
        /// <returns>Guid of the existing shader map if found locally and not cloned; New guid if cloned.</returns>
        /// <exception cref="Exception"></exception>
        public static Guid? CopyRefShadersToLocal(ExportEntry material, bool cloneIfLocal = false)
        {
            StaticParameterSet sps = material.ClassName switch
            {
                "Material" => (StaticParameterSet) ObjectBinary.From<Material>(material).SM3MaterialResource.ID,
                _ => ObjectBinary.From<MaterialInstance>(material).SM3StaticParameterSet
            };
            ShaderCache seekFreeShaderCache;
            Guid newMatGuid;
            //if (material.FileRef.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is { } seekFreeShaderCacheExport) // Old code. Not sure if there are other shader caches, since this is not ref?

            // Check the local shader cache
            if (material.FileRef.FindExport("SeekFreeShaderCache", "ShaderCache") is { } seekFreeShaderCacheExport)
            {
                seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                if (seekFreeShaderCache.MaterialShaderMaps.TryGetValue(sps, out MaterialShaderMap msm))
                {
                    if (!cloneIfLocal)
                        return sps.BaseMaterialId; // This is the ID of shader

                    Dictionary<Guid, Guid> shaderGuidMap = msm.DeepCopyWithNewGuidsInto(seekFreeShaderCache, out newMatGuid);
                    foreach ((Guid oldGuid, Guid newGuid) in shaderGuidMap)
                    {
                        if (!seekFreeShaderCache.Shaders.TryGetValue(oldGuid, out Shader oldShader))
                        {
                            throw new Exception($"Shader {oldGuid} not found!");
                        }

                        Shader newShader = oldShader.Clone();
                        newShader.Guid = newGuid;
                        seekFreeShaderCache.Shaders.Add(newGuid, newShader);
                    }

                    seekFreeShaderCacheExport.WriteBinary(seekFreeShaderCache);

                    if (material.ClassName == "Material")
                    {
                        var matBin = ObjectBinary.From<Material>(material);
                        matBin.SM3MaterialResource.ID = newMatGuid;
                        material.WriteBinary(matBin);
                    }
                    else
                    {

                        var matBin = ObjectBinary.From<MaterialInstance>(material);
                        matBin.SM3StaticPermutationResource.ID = newMatGuid;
                        material.WriteBinary(matBin);
                    }

                    return newMatGuid;
                }
            }
            else
            {
                // Create SeekFreeShaderCache
                seekFreeShaderCacheExport = new ExportEntry(material.FileRef, 0, "SeekFreeShaderCache", BitConverter.GetBytes(-1), binary: ShaderCache.Create())
                {
                    Class = material.FileRef.GetEntryOrAddImport("Engine.ShaderCache", "Class"),
                    ObjectFlags = UnrealFlags.EObjectFlags.LoadForClient | UnrealFlags.EObjectFlags.LoadForEdit |
                                  UnrealFlags.EObjectFlags.LoadForServer | UnrealFlags.EObjectFlags.Standalone
                };
                material.FileRef.AddExport(seekFreeShaderCacheExport);
                seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
            }

            if (!RefShaderCacheReader.IsShaderOffsetsDictInitialized(material.Game))
            {
                // Todo: Callbacks?
                // BusyText = "Calculating Shader offsets\n(May take ~15s)";
            }

            MaterialShaderMap msmFromGlobalCache = RefShaderCacheReader.GetMaterialShaderMap(material.Game, sps, out _);
            if (msmFromGlobalCache != null && material is not null)
            {
                Dictionary<Guid, Guid> shaderGuidMap =
                    msmFromGlobalCache.DeepCopyWithNewGuidsInto(seekFreeShaderCache, out newMatGuid);
                Shader[] shaders = RefShaderCacheReader.GetShaders(material.Game, shaderGuidMap.Keys,
                    out UMultiMap<NameReference, uint> shaderTypeCRCMap,
                    out UMultiMap<NameReference, uint> vertexFactoryTypeCRCMap);
                if (shaders is null)
                {
                    throw new Exception("Unable to retrieve shaders from RefShaderCache");
                }

                foreach (Shader oldShader in shaders)
                {
                    Shader newShader = oldShader.Clone();
                    newShader.Guid = shaderGuidMap[oldShader.Guid];
                    seekFreeShaderCache.Shaders.Add(newShader.Guid, newShader);
                }

                foreach ((NameReference key, uint value) in shaderTypeCRCMap)
                {
                    seekFreeShaderCache.ShaderTypeCRCMap.TryAddUnique(key, value);
                }

                foreach ((NameReference key, uint value) in vertexFactoryTypeCRCMap)
                {
                    seekFreeShaderCache.VertexFactoryTypeCRCMap.TryAddUnique(key, value);
                }

                seekFreeShaderCacheExport.WriteBinary(seekFreeShaderCache);

                if (material.ClassName == "Material")
                {
                    var matBin = ObjectBinary.From<Material>(material);
                    matBin.SM3MaterialResource.ID = newMatGuid;
                    material.WriteBinary(matBin);
                }
                else
                {

                    var matBin = ObjectBinary.From<MaterialInstance>(material);
                    matBin.SM3StaticPermutationResource.ID = newMatGuid;
                    material.WriteBinary(matBin);
                }

                return newMatGuid;
            }

            return null; // Not found
        }

        //if MaterialInstanceConstant, bHasStaticPermutationResource _must_ be true!
        public static (MaterialShaderMap, Shader[]) GetMaterialShaderMapAndShaders(ExportEntry material, params string[] shaderTypes)
        {
            StaticParameterSet sps = material.ClassName switch
            {
                "Material" => (StaticParameterSet)ObjectBinary.From<Material>(material).SM3MaterialResource.ID,
                _ => ObjectBinary.From<MaterialInstance>(material).SM3StaticParameterSet
            };
            var shaders = new Shader[shaderTypes.Length];
            if (material.FileRef.FindExport("SeekFreeShaderCache", "ShaderCache") is { } seekFreeShaderCacheExport)
            {
                var seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                if (seekFreeShaderCache.MaterialShaderMaps.TryGetValue(sps, out MaterialShaderMap msm))
                {
                    foreach (MeshShaderMap meshShaderMap in msm.MeshShaderMaps)
                    {
                        if (meshShaderMap.VertexFactoryType.Name == "FLocalVertexFactory")
                        {
                            for (int i = 0; i < shaderTypes.Length; i++)
                            {
                                if (meshShaderMap.Shaders.TryGetValue(shaderTypes[i], out ShaderReference shaderRef))
                                {
                                    shaders[i] = seekFreeShaderCache.Shaders[shaderRef.Id];
                                }
                            }
                            break;
                        }
                    }
                    return (msm, shaders);
                }
            }

            var shaderGuids = new Guid[shaderTypes.Length];
            MaterialShaderMap materialShaderMap = RefShaderCacheReader.GetMaterialShaderMap(material.Game, sps, out _);
            foreach (MeshShaderMap meshShaderMap in materialShaderMap.MeshShaderMaps)
            {
                if (meshShaderMap.VertexFactoryType.Name == "FLocalVertexFactory")
                {
                    for (int i = 0; i < shaderTypes.Length; i++)
                    {
                        if (meshShaderMap.Shaders.TryGetValue(shaderTypes[i], out ShaderReference shaderRef))
                        {
                            shaderGuids[i] = shaderRef.Id;
                        }
                    }
                    RefShaderCacheReader.GetShaders(material.Game, shaderGuids, out _, out _)?.CopyTo(shaders, 0);
                    break;
                }
            }
            return (materialShaderMap, shaders);
        }
    }
}
