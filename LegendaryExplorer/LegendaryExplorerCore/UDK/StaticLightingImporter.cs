using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.UDK
{
    /// <summary>
    /// Contains variables for configuring lightmap importing
    /// </summary>
    public class LightingImportSetup
    {
        /// <summary>
        /// Path where UDK map files that contain static lighting are held
        /// </summary>
        public string UDKMapsBasePath { get; set; } = UDKDirectory.MapsPath;

        /// <summary>
        /// If items in AdditionalPackagesToCook should be also modified
        /// </summary>
        public bool IncludeSubLevels { get; set; } = true;

        /// <summary>
        /// Delegate that can be invoked in order to not clear a lightmap, preserving the original.
        /// </summary>
        public Func<ExportEntry, bool> ShouldKeepLightMap;

        /// <summary>
        /// The prefix to use when keeping a lightmap, changing the name.
        /// </summary>
        public string KeptLightmapPrefix { get; set; } = "Kept";


        internal List<ExportEntry> LightmapsToTrash { get; set; } = new();

        /// <summary>
        /// Output: StaticMeshComponents that received UDK lighting, across every package this setup was used for. Anything
        /// lit but not in here still carries the lighting it had before the import.
        /// </summary>
        public HashSet<ExportEntry> RelitComponents { get; } = new();

        /// <summary>
        /// List of sublevel names to ignore when importing static lighting. This is useful for levels that have sublevels that are not relevant to the lighting bake or should not be modified.
        /// </summary>
        public string[] IgnoredSublevelNames { get; set; }

        // MILAN CHANGE - 2026-10-11 - hooks so the caller can cache the (expensive) BC7 mip chains of ported lightmaps.
        // All optional: with none of them set, lightmaps are mipped exactly as before.

        /// <summary>
        /// Called for each ported lightmap texture that still has a single mip, right before its mips would be generated. The
        /// texture's export holds the UDK pixels at this point. Return true if the callback installed a complete mip chain
        /// itself (for example from a cache, with <see cref="Texture2D.ReplaceWithEncodedMips"/>); the mips are then not generated.
        /// </summary>
        public Func<ExportEntry, bool> TryUsePrecomputedLightmapMips;

        /// <summary>
        /// Called for each ported lightmap texture right after its mips were generated (not when
        /// <see cref="TryUsePrecomputedLightmapMips"/> handled it), so a caller can store the result.
        /// </summary>
        public Action<ExportEntry> LightmapMipsGenerated;

        /// <summary>
        /// How the lower mips of a ported lightmap are filtered. Null keeps the historical box filter. Anything that caches
        /// the generated mips should include these options in its key.
        /// </summary>
        public MipGenerationOptions LightmapMipOptions { get; set; }
    }

    public static class StaticLightingImporter
    {
        /// <summary>
        /// Imports static lighting from UDK.
        /// </summary>
        /// <param name="package">The package file. To import sublevels, the package must have AdditionalPackagesToCook set in the package and the setup object indicating it should import sublevels. Tags must be set on StaticMeshActors to map their source.</param>
        public static void ImportStaticLighting(IMEPackage package, LightingImportSetup setup)
        {
            var persistentPackage = (MEPackage)package;
            var basePath = Directory.GetParent(persistentPackage.FilePath).FullName;
            using var persistentUdk = GetUDKMapPackage(persistentPackage.FileNameNoExtension, setup.UDKMapsBasePath);
            if (persistentUdk == null)
                return; // Package not found
            AssignStaticLighting(persistentPackage, persistentUdk, setup);

            if (setup.IncludeSubLevels)
            {
                foreach (var subLevel in persistentPackage.AdditionalPackagesToCook)
                {
                    if (setup.IgnoredSublevelNames != null && setup.IgnoredSublevelNames.Contains(subLevel, StringComparer.OrdinalIgnoreCase))
                        continue;
                    var mePath = Path.Combine(basePath, subLevel + ".pcc");
                    using var mePackage = (MEPackage)MEPackageHandler.OpenMEPackage(mePath);
                    using var udkPackage = GetUDKMapPackage(subLevel, setup.UDKMapsBasePath);
                    if (udkPackage == null)
                    {
                        LECLog.Warning($"Sublevel not found for lighting import, skipping: {subLevel}");
                        continue;
                    }
                    AssignStaticLighting(mePackage, udkPackage, setup);
                    if (mePackage.IsModified)
                        mePackage.Save();
                }
            }
        }

        /// <summary>
        /// Clears replaceable lighting while preserving excluded and unmatched mesh components.
        /// </summary>
        /// <param name="mePackage"></param>
        private static void ClearLightmaps(IMEPackage mePackage, LightingImportSetup setup,
            ISet<ExportEntry> unmatchedMeshComponents)
        {
            // StaticMeshes
            foreach (var smc in mePackage.Exports.Where(x => x.IsA("StaticMeshComponent")).ToList())
            {
                var meBin = ObjectBinary.From<StaticMeshComponent>(smc);
                if (unmatchedMeshComponents.Contains(smc) || (setup.ShouldKeepLightMap?.Invoke(smc) ?? false))
                {
                    foreach (var lod in meBin.LODData)
                    {
                        KeepLightMapTextures(lod.LightMap, mePackage, setup);
                        KeepShadowMaps(mePackage, lod.ShadowMaps, setup);
                        KeepShadowMaps(mePackage, lod.ShadowVertexBuffers, setup);
                    }
                }
                else
                {
                    foreach (var lod in meBin.LODData)
                    {
                        ClearLightmap(lod.LightMap, mePackage, setup);
                        ClearShadowMaps(mePackage, ref lod.ShadowMaps, setup);
                        ClearShadowMaps(mePackage, ref lod.ShadowVertexBuffers, setup);
                    }
                }

                smc.WriteBinary(meBin);
            }

            // Decal Components
            foreach (var dc in mePackage.Exports.Where(x => x.IsA("DecalComponent")).ToList())
            {
                var meBin = ObjectBinary.From<DecalComponent>(dc);
                if (setup.ShouldKeepLightMap != null && setup.ShouldKeepLightMap(dc))
                {
                    foreach (var staticRec in meBin.StaticReceivers)
                    {
                        KeepLightMapTextures(staticRec.LightMap, mePackage, setup);
                    }
                }
                else
                {
                    foreach (var staticRec in meBin.StaticReceivers)
                    {
                        ClearLightmap(staticRec.LightMap, mePackage, setup);
                    }
                }

                dc.WriteBinary(meBin);
            }

            foreach (var mc in mePackage.Exports.Where(x => x.IsA("ModelComponent")).ToList())
            {
                var meBin = ObjectBinary.From<ModelComponent>(mc);
                if (setup.ShouldKeepLightMap != null && setup.ShouldKeepLightMap(mc))
                {
                    foreach (var elem in meBin.Elements)
                    {
                        KeepLightMapTextures(elem.LightMap, mePackage, setup);
                    }
                }
                else
                {
                    foreach (var elem in meBin.Elements)
                    {
                        ClearLightmap(elem.LightMap, mePackage, setup);
                        ClearShadowMaps(mePackage, ref elem.ShadowMaps, setup);
                    }
                }

                mc.WriteBinary(meBin);
            }

            foreach (var tc in mePackage.Exports.Where(x => x.IsA("TerrainComponent")).ToList())
            {
                var meBin = ObjectBinary.From<TerrainComponent>(tc);

                if (setup.ShouldKeepLightMap != null && setup.ShouldKeepLightMap(tc))
                {
                    KeepLightMapTextures(meBin.LightMap, mePackage, setup);
                }
                else
                {
                    ClearLightmap(meBin.LightMap, mePackage, setup);
                }

                tc.WriteBinary(meBin);
            }

            // Trash in one batch - each TrashEntries call rebuilds the package lookup table and reserializes the level, so
            // doing it per entry is very slow on levels with many lightmaps/shadowmaps
            var toTrash = setup.LightmapsToTrash.Where(tex => !tex.ObjectName.Name.StartsWith(setup.KeptLightmapPrefix) && !tex.IsTrash()).ToList();
            if (toTrash.Count > 0)
            {
                EntryPruner.TrashEntries(mePackage, toTrash);
            }
            setup.LightmapsToTrash.Clear();
        }

        /// <summary>
        /// Deletes the shadows maps from the package, their textures, and then clears the shadow map array, to help prevent collisions
        /// </summary>
        /// <param name="shadowMaps">List of ShadowMap2D indices</param>
        private static void ClearShadowMaps(IMEPackage mePackage, ref int[] shadowMaps, LightingImportSetup setup)
        {
            foreach(var uindex in shadowMaps)
            {
                var shadowMap = mePackage.GetUExport(uindex);
                if (shadowMap.ClassName == "ShadowMap2D")
                {
                    var tindex = shadowMap.GetProperty<ObjectProperty>("Texture");
                    if (tindex != null)
                    {
                        var temp = tindex.Value;
                        TrashRef(mePackage, ref temp, setup);
                    }
                }

                var index = shadowMap.UIndex;
                TrashRef(mePackage, ref index, setup);
            }

            shadowMaps = [];
        }

        private static void ClearLightmap(LightMap lightMap, IMEPackage mePackage, LightingImportSetup setup)
        {
            if (lightMap is LightMap_1D lm1d)
            {
                // No textures
            }
            else if (lightMap is LightMap_2D lm2d)
            {
                // Has textures
                TrashRef(mePackage, ref lm2d.Texture1, setup);
                TrashRef(mePackage, ref lm2d.Texture2, setup);
                TrashRef(mePackage, ref lm2d.Texture3, setup);
                TrashRef(mePackage, ref lm2d.Texture4, setup);
            }
            else if (lightMap is LightMap_3 lm3)
            {
                // No textures
            }
            else if (lightMap is LightMap_4or6 lm46)
            {
                // Has textures
                TrashRef(mePackage, ref lm46.Texture1, setup);
                TrashRef(mePackage, ref lm46.Texture2, setup);
                TrashRef(mePackage, ref lm46.Texture3, setup);
            }
            else if (lightMap is LightMap_5 lm5)
            {
                // No textures
            }
        }

        /// <summary>
        /// Sets a reference to 0 if it is not 0 to begin with, and trashes the referenced item
        /// </summary>
        /// <param name="mePackage"></param>
        /// <param name="uindex"></param>
        private static void TrashRef(IMEPackage mePackage, ref int uindex, LightingImportSetup setup)
        {
            if (uindex > 0)
            {
                var export = mePackage.GetUExport(uindex);
                if (!export.IsTrash() && !setup.LightmapsToTrash.Contains(export))
                {
                    // Lightmaps to keep will filter this list when applied
                    setup.LightmapsToTrash.Add(export);
                }

                uindex = 0;
            }
        }

        /// <summary>
        /// Ports a lightmap from UDK into the given MEPackage, package stored, with a full mip chain. This does not move it to a TFC;
        /// a caller that wants it external has to do that afterwards.
        /// </summary>
        /// <param name="mePackage"></param>
        /// <param name="textureUIndex">On input, this is the UIndex in the UDKPackage. On exit, it should be the MEPackage UIndex.</param>
        /// <param name="udkPackage"></param>
        /// <param name="setup">Optional mip hooks and options (see <see cref="LightingImportSetup.TryUsePrecomputedLightmapMips"/>).</param>
        /// <exception cref="NotImplementedException"></exception>
        private static void PortLightMap(IMEPackage mePackage, ref int textureUIndex, UDKPackage udkPackage, LightingImportSetup setup = null)
        {
            if (textureUIndex == 0)
                return;

            var udkTex = udkPackage.GetUExport(textureUIndex);
            var existing = mePackage.FindExport(udkTex.InstancedFullPath);
            if (existing != null)
            {
                // Already in the file
                textureUIndex = existing.UIndex;
                // Debug.WriteLine($"Lightmap already in file: {existing.InstancedFullPath}");
                return;
            }

            // Port it
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, udkTex, mePackage, null, true, new RelinkerOptionsPackage(), out var lightmap);
            textureUIndex = lightmap.UIndex;
            //Debug.WriteLine($"Installed lightmap {lightmap.InstancedFullPath}");

            var lightmapTex = lightmap as ExportEntry;

            // Texture will keep UDK's lightmap flags until texture is externalized.

            var tex = new Texture2D(lightmapTex);
            if (tex.Mips.Count == 1)
            {
                // 10/13/2026 - Add support for caller to supply their own lightmap data
                if (setup?.TryUsePrecomputedLightmapMips?.Invoke(lightmapTex) == true)
                    return;

                var format = mePackage.Game.IsLEGame() ? PixelFormat.BC7 : PixelFormat.DXT1;
                tex.GenerateMips(format, setup?.LightmapMipOptions);
                setup?.LightmapMipsGenerated?.Invoke(lightmapTex);
            }
        }

        private static UDKPackage GetUDKMapPackage(string baseName, string baseMapPath = null)
        {
            var udkPath = Path.Combine(baseMapPath ?? UDKDirectory.MapsPath, baseName + ".udk");
            if (File.Exists(udkPath))
            {
                return (UDKPackage)MEPackageHandler.OpenUDKPackage(udkPath);
            }

            return null;
        }

        private static void AssignStaticLighting(MEPackage mePackage, UDKPackage udkPackage, LightingImportSetup setup)
        {
            LECLog.Information($"Importing static lighting for {mePackage.FileNameNoExtension} from {udkPackage.FilePath}");

            // List of models that we have updated so we can skip a secondary update
            List<ExportEntry> updatedModels = new List<ExportEntry>();
            var meToUdkRemapping = new Dictionary<ExportEntry, ExportEntry>();

            // StaticMeshComponent
            foreach (var smc in udkPackage.Exports.Where(x => x.IsA("StaticMeshComponent")))
            {
                var parent = smc.Parent as ExportEntry;
                var tag = parent.GetProperty<NameProperty>("Tag");
                if (tag != null)
                {
                    var exp = mePackage.FindExport(tag.Value.Name, smc.ClassName);
                    if (exp != null)
                    {
                        meToUdkRemapping[exp] = smc;
                    }
                }
            }

            // Non tagged, same IFP
            foreach (var smc in udkPackage.Exports.Where(x => x.IsA("TerrainComponent")))
            {
                var meExp = mePackage.FindExport(smc.InstancedFullPath, smc.ClassName);
                if (meExp != null)
                {
                    meToUdkRemapping[meExp] = smc;
                }
            }

            // ModelComponents are pretty complicated... hopefully order doesn't change
            var udkLevelBin = udkPackage.GetLevelBinary();
            var meLevelBin = mePackage.GetLevelBinary();
            if (udkLevelBin.ModelComponents.Length == meLevelBin.ModelComponents.Length)
            {
                for (int i = 0; i < udkLevelBin.ModelComponents.Length; i++)
                {
                    meToUdkRemapping[mePackage.GetUExport(meLevelBin.ModelComponents[i])] = udkPackage.GetUExport(udkLevelBin.ModelComponents[i]);
                }
            }



            // Older bakes a pipeline uses may have deliberately excluded/unresolved actors may have no UDK counterpart.
            // Preserve their lighting before clearing shared textures; only matched components are relit.
            var unmatchedMeshComponents = new HashSet<ExportEntry>();
            foreach (var smc in mePackage.Exports.Where(x => x.IsA("StaticMeshComponent")))
            {
                if (!meToUdkRemapping.ContainsKey(smc))
                {
                    if (udkPackage.FindExport(smc.InstancedFullPath, smc.ClassName) is { } udkSmc)
                        meToUdkRemapping[smc] = udkSmc;
                    else
                        unmatchedMeshComponents.Add(smc);
                }
            }
            ClearLightmaps(mePackage, setup, unmatchedMeshComponents);

            // Import the lightmaps

            // Components that received new lighting from UDK, so decals on them can follow suit
            var relitComponents = new HashSet<ExportEntry>();

            // StaticMeshComponent
            foreach (var smc in mePackage.Exports.Where(x => x.IsA("StaticMeshComponent")).ToList())
            {
                if (unmatchedMeshComponents.Contains(smc) || (setup.ShouldKeepLightMap?.Invoke(smc) ?? false))
                {
                    continue; // Do not install this lightmap.
                }
                meToUdkRemapping.TryGetValue(smc, out var udkSmc);
                if (udkSmc == null)
                {
                    udkSmc = udkPackage.FindExport(smc.InstancedFullPath);
                }

                if (udkSmc != null)
                {
                    var meBin = ObjectBinary.From<StaticMeshComponent>(smc);
                    var uBin = ObjectBinary.From<StaticMeshComponent>(udkSmc);
                    // Older UDK maps may lack paint; the target component remains authoritative.
                    meBin.LODData = StaticMeshComponentLightingData.MergeLighting(meBin.LODData, uBin.LODData);
                    foreach (var lod in meBin.LODData)
                    {
                        PortLightMaps(lod.LightMap, smc, udkPackage, setup);
                        PortShadowMaps(ref lod.ShadowMaps, smc, udkPackage);
                        PortShadowMaps(ref lod.ShadowVertexBuffers, smc, udkPackage);
                    }

                    smc.WriteBinary(meBin);

                    // IrrelevantLights lists the lights the bake found have no influence on this component (fully shadowed,
                    // out of range, or no shared channel). It belongs to the same bake as the lightmap and shadow maps, so
                    // the list from the original game's bake is stale once the UDK lighting is installed.
                    if (udkSmc.GetProperty<ArrayProperty<StructProperty>>("IrrelevantLights") is { } irrelevantLights)
                    {
                        smc.WriteProperty(irrelevantLights);
                    }
                    else
                    {
                        smc.RemoveProperty("IrrelevantLights");
                    }

                    relitComponents.Add(smc);
                    setup.RelitComponents.Add(smc);
                }
                else
                {
                    Debug.WriteLine($"Did not find UDK SMC in remapping: {smc.InstancedFullPath}");
                }
            }

            // DecalComponent
            var importedDecals = ImportDecalStaticReceivers(mePackage, udkPackage, meToUdkRemapping, setup);
            UpdateDecalStaticReceivers(mePackage, relitComponents, setup, importedDecals);

            // Terrain
            foreach (var tcp in meToUdkRemapping.Where(x => x.Key.IsA("TerrainComponent")).ToList())
            {
                var tc = tcp.Key;

                if (setup.ShouldKeepLightMap?.Invoke(tc) ?? false)
                {
                    continue; // Do not install this lightmap.
                }

                var meBin = ObjectBinary.From<TerrainComponent>(tc);
                var udkBin = ObjectBinary.From<TerrainComponent>(tcp.Value);
                meBin.LightMap = udkBin.LightMap;
                PortLightMaps(meBin.LightMap, tc, udkPackage, setup); // Must assign UDK version here.
                tc.WriteBinary(meBin);
            }

            // Model
            foreach (var mcp in meToUdkRemapping.Where(x => x.Key.IsA("ModelComponent")).ToList())
            {
                var mc = mcp.Key;
                if (setup.ShouldKeepLightMap?.Invoke(mc) ?? false)
                {
                    continue; // Do not install this lightmap.
                }

                var meBin = ObjectBinary.From<ModelComponent>(mc);
                var udkBin = ObjectBinary.From<ModelComponent>(mcp.Value);

                // UDK can 'optimize' a model so the input and return from UDK might not match elements
                // We will just take the UDK binary wholesaale, so lightmap works.
                // That's why code here uses UDK instead of ME version of objects.

                // Relink
                var udkOriginalOwningModel = udkBin.Model; // Cache here before relink

                var owningModel = mePackage.GetUExport(meBin.Model);
                // Relink components
                for (int i = 0; i < udkBin.Elements.Length; i++)
                {
                    udkBin.Model = owningModel.UIndex;
                    udkBin.Elements[i].Component = mc.UIndex; // Point to self
                    udkBin.Elements[i].Material = mePackage.FindEntry(udkPackage.GetEntry(udkBin.Elements[i].Material).InstancedFullPath)?.UIndex ?? 0; // Point to material
                    PortLightMaps(udkBin.Elements[i].LightMap, mc, udkPackage, setup);
                    PortShadowMaps(ref udkBin.Elements[i].ShadowMaps, mc, udkPackage);
                }
                // Yes write UDK version as we just relinked it to us
                mc.WriteBinary(udkBin);

                // We must also update the model binary as that seems to affect the components. I don't really know why
                // but this was verified to fix black lightmap
                // Since there might be multiple models we have to do this for every component
                if (!updatedModels.Contains(owningModel))
                {
                    var udkOwningModel = udkPackage.GetUExport(udkOriginalOwningModel);
                    var udkModel = ObjectBinary.From<Model>(udkOwningModel);

                    var meModel = ObjectBinary.From<Model>(owningModel);

                    udkModel.Polys = meModel.Polys; // Relink
                    udkModel.Self = meModel.Self; // Relink

                    for (int i = 0; i < udkModel.Surfs.Length; i++) // Relink
                    {
                        udkModel.Surfs[i].Material = mePackage.FindEntry(udkPackage.GetEntry(udkModel.Surfs[i].Material).InstancedFullPath)?.UIndex ?? 0; 
                        udkModel.Surfs[i].Actor = mePackage.FindEntry(udkPackage.GetEntry(udkModel.Surfs[i].Actor)?.InstancedFullPath)?.UIndex ?? 0; 
                    }

                    // Write relinked UDK model to ours
                    owningModel.WriteBinary(udkModel);
                    updatedModels.Add(owningModel);
                }
            }
        }

        /// <summary>
        /// Imports projection geometry and lighting generated by UDK, remapping each receiver back to its source component.
        /// </summary>
        private static HashSet<ExportEntry> ImportDecalStaticReceivers(MEPackage mePackage, UDKPackage udkPackage,
            Dictionary<ExportEntry, ExportEntry> meToUdkRemapping, LightingImportSetup setup)
        {
            // MILAN CHANGE - Mgamerz - 2026-09-30
            // Decals are now exported with source component paths in their actor Tags. Receiver
            // references belong to UDK's split mesh actors and must not be copied as raw UIndexes.
            var importedDecals = new HashSet<ExportEntry>();
            var receivers = meToUdkRemapping.ToDictionary(x => x.Value.UIndex, x => x.Key);
            var exportedReceiverIndices = meToUdkRemapping.Keys
                .Where(x => !(setup.ShouldKeepLightMap?.Invoke(x) ?? false)).Select(x => x.UIndex).ToHashSet();
            foreach (var udkDecal in udkPackage.Exports.Where(x => x.IsA("DecalComponent")))
            {
                var tag = (udkDecal.Parent as ExportEntry)?.GetProperty<NameProperty>("Tag");
                var meDecal = tag == null ? null : mePackage.FindExport(tag.Value.Name);
                if (meDecal == null || !meDecal.IsA("DecalComponent") || (setup.ShouldKeepLightMap?.Invoke(meDecal) ?? false))
                    continue;

                var original = ObjectBinary.From<DecalComponent>(meDecal);
                var baked = ObjectBinary.From<DecalComponent>(udkDecal);
                if (baked.StaticReceivers.Length == 0)
                {
                    LECLog.Warning($"UDK decal {udkDecal.InstancedFullPath} has no generated receivers; keeping source projection data");
                    continue;
                }
                // Keep projections onto unexported objects or receivers whose original lighting is retained.
                var combined = original.StaticReceivers.Where(x => !exportedReceiverIndices.Contains(x.Component)).ToList();
                foreach (var sr in baked.StaticReceivers)
                {
                    if (!receivers.TryGetValue(sr.Component, out var receiver))
                        throw new InvalidDataException($"Cannot map UDK decal receiver #{sr.Component} for {meDecal.InstancedFullPath}");
                    if (!exportedReceiverIndices.Contains(receiver.UIndex)) continue;
                    sr.Component = receiver.UIndex;
                    // UDK's decal clipping keeps faces that run parallel to the projection, which BioWare data
                    // culls; they render as streaks. Without per-decal-vertex lighting
                    // the geometry is lighting independent, so the source projection can be kept as authored.
                    var source = original.StaticReceivers.FirstOrDefault(x => x.Component == receiver.UIndex);
                    if (source != null && HasNoPerVertexLighting(source) && HasNoPerVertexLighting(sr))
                    {
                        sr.Vertices = source.Vertices;
                        sr.Indices = source.Indices;
                        sr.NumTriangles = source.NumTriangles;
                    }
                    PortLightMaps(sr.LightMap, receiver, udkPackage, setup);
                    sr.ShadowMap1D ??= [];
                    PortShadowMaps(ref sr.ShadowMap1D, meDecal, udkPackage);
                    combined.Add(sr);
                }
                original.StaticReceivers = combined.ToArray();
                meDecal.WriteBinary(original);
                importedDecals.Add(meDecal);
            }
            return importedDecals;
            // END MILAN CHANGE
        }

        /// <summary>
        /// Whether a decal receiver stores no lighting tied to its own vertex order (no LMT_1D samples or ShadowMap1Ds),
        /// so its projection geometry can be swapped without misaligning lighting data.
        /// </summary>
        private static bool HasNoPerVertexLighting(StaticReceiverData sr) =>
            (sr.LightMap == null || sr.LightMap.LightMapType == ELightMapType.LMT_None) && (sr.ShadowMap1D == null || sr.ShadowMap1D.Length == 0);

        /// <summary>
        /// Brings legacy decal static receivers in line with the new lighting on their receiving components.
        /// Older UDK maps contain no DecalActors, so the receiver's data has to be derived from the receiver component.
        /// Decals whose regenerated receivers were imported from UDK are left intact. This follows what the
        /// shipping LE3 levels contain: a receiver on a texture-lightmapped mesh stores no lightmap of its own (it is always
        /// LMT_None), while a receiver on a vertex-lit mesh stores its own per-decal-vertex LMT_1D, plus ShadowMap1D exports
        /// owned by the DecalActor that mirror the receiving mesh's ShadowVertexBuffers.
        /// </summary>
        private static void UpdateDecalStaticReceivers(IMEPackage mePackage, HashSet<ExportEntry> relitComponents,
            LightingImportSetup setup, HashSet<ExportEntry> importedDecals)
        {
            var droppedShadowMaps = new HashSet<int>();
            var keptShadowMaps = new HashSet<int>();
            foreach (var dc in mePackage.Exports.Where(x => x.IsA("DecalComponent")).ToList())
            {
                if (importedDecals.Contains(dc))
                {
                    foreach (var sr in ObjectBinary.From<DecalComponent>(dc).StaticReceivers)
                        keptShadowMaps.UnionWith(sr.ShadowMap1D ?? []);
                    continue;
                }
                var meBin = ObjectBinary.From<DecalComponent>(dc);
                bool keepDecal = setup.ShouldKeepLightMap?.Invoke(dc) ?? false;
                bool modified = false;
                foreach (var sr in meBin.StaticReceivers)
                {
                    sr.ShadowMap1D ??= []; // Not serialized before ME3
                    if (keepDecal || !mePackage.TryGetUExport(sr.Component, out var receiver) || !relitComponents.Contains(receiver))
                    {
                        // Receiver still has the lighting this decal was generated against
                        keptShadowMaps.UnionWith(sr.ShadowMap1D);
                        continue;
                    }

                    var receiverLOD = ObjectBinary.From<StaticMeshComponent>(receiver).LODData.FirstOrDefault();
                    if (receiverLOD?.LightMap is LightMap_1D)
                    {
                        // Per-decal-vertex samples can't be regenerated from the receiver's new vertex lightmap here,
                        // so leave whatever the decal already had.
                        if (sr.LightMap is LightMap_1D decalLM1D)
                        {
                            decalLM1D.Owner = receiver.UIndex;
                        }
                    }
                    else if (sr.LightMap.LightMapType != ELightMapType.LMT_None)
                    {
                        sr.LightMap = new LightMap { LightMapType = ELightMapType.LMT_None };
                    }

                    var receiverShadowLightGuids = GetShadowMapLightGuids(mePackage, receiverLOD?.ShadowVertexBuffers);
                    if (sr.ShadowMap1D.Length > 0)
                    {
                        var retainedShadowMaps = new List<int>();
                        foreach (int shadowMapUIndex in sr.ShadowMap1D)
                        {
                            if (mePackage.TryGetUExport(shadowMapUIndex, out var shadowMap)
                                && shadowMap.ClassName == "ShadowMap1D"
                                && receiverShadowLightGuids.Contains(ObjectBinary.From<ShadowMap1D>(shadowMap).LightGuid))
                            {
                                retainedShadowMaps.Add(shadowMapUIndex);
                                keptShadowMaps.Add(shadowMapUIndex);
                            }
                            else
                            {
                                droppedShadowMaps.Add(shadowMapUIndex);
                            }
                        }

                        sr.ShadowMap1D = retainedShadowMaps.ToArray();
                    }

                    modified = true;
                }

                if (modified)
                {
                    dc.WriteBinary(meBin);
                }
            }

            droppedShadowMaps.ExceptWith(keptShadowMaps);
            var shadowMapsToTrash = droppedShadowMaps.Select(x => mePackage.TryGetUExport(x, out var sm) ? sm : null)
                .Where(x => x != null && x.ClassName == "ShadowMap1D" && !x.IsTrash()).ToList();
            if (shadowMapsToTrash.Count > 0)
            {
                EntryPruner.TrashEntries(mePackage, shadowMapsToTrash);
            }
        }

        private static HashSet<Guid> GetShadowMapLightGuids(IMEPackage package, int[] shadowMapUIndexes)
        {
            var lightGuids = new HashSet<Guid>();
            if (shadowMapUIndexes == null)
            {
                return lightGuids;
            }

            foreach (int shadowMapUIndex in shadowMapUIndexes)
            {
                if (package.TryGetUExport(shadowMapUIndex, out var shadowMap) && shadowMap.ClassName == "ShadowMap1D")
                {
                    lightGuids.Add(ObjectBinary.From<ShadowMap1D>(shadowMap).LightGuid);
                }
            }

            return lightGuids;
        }

        /// <summary>
        /// Ports shadowmaps and updates the shadowmaps array with the relinked UIndex values
        /// </summary>
        /// <param name="shadowMaps"></param>
        /// <param name="mc"></param>
        /// <param name="udkPackage"></param>
        private static void PortShadowMaps(ref int[] shadowMaps, ExportEntry mc, UDKPackage udkPackage)
        {
            var finalShadowMapsArray = new List<int>();
            foreach (var shadowMapIdx in shadowMaps)
            {
                if (udkPackage.TryGetUExport(shadowMapIdx, out var shadowMap))
                {
                    if (shadowMap.ClassName is "ShadowMap1D" or "ShadowMap2D")
                    {
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild,
                            shadowMap, mc.FileRef, mc, true,
                            new RelinkerOptionsPackage { ImportExportDependencies = true }, out var newShadowMap);
                        finalShadowMapsArray.Add(newShadowMap.UIndex);
                    }
                }
            }

            // This dumps out invalid values.
            shadowMaps = finalShadowMapsArray.ToArray();
        }

        private static void PortLightMaps(LightMap lmWithUdkRefs, ExportEntry meComponent, UDKPackage udkPackage, LightingImportSetup setup = null)
        {
            if (lmWithUdkRefs is LightMap_1D lm1d)
            {
                // No textures
                lm1d.Owner = meComponent.UIndex;
            }
            else if (lmWithUdkRefs is LightMap_2D lm2d)
            {
                // Has textures
                PortLightMap(meComponent.FileRef, ref lm2d.Texture1, udkPackage, setup);
                PortLightMap(meComponent.FileRef, ref lm2d.Texture2, udkPackage, setup);
                PortLightMap(meComponent.FileRef, ref lm2d.Texture3, udkPackage, setup);
                PortLightMap(meComponent.FileRef, ref lm2d.Texture4, udkPackage, setup);
            }
            else if (lmWithUdkRefs is LightMap_3 lm3)
            {
                // No textures
            }
            else if (lmWithUdkRefs is LightMap_4or6 lm46)
            {
                // Has textures
                PortLightMap(meComponent.FileRef, ref lm46.Texture1, udkPackage, setup);
                PortLightMap(meComponent.FileRef, ref lm46.Texture2, udkPackage, setup);
                PortLightMap(meComponent.FileRef, ref lm46.Texture3, udkPackage, setup);
            }
            else if (lmWithUdkRefs is LightMap_5 lm5)
            {
                // No textures
            }
        }

        /// <summary>
        /// Protects shadow resources shared with components whose lighting is being replaced.
        /// </summary>
        private static void KeepShadowMaps(IMEPackage package, int[] shadowMaps, LightingImportSetup setup)
        {
            for (int i = 0; i < shadowMaps.Length; i++)
            {
                // Shadow maps and their textures may be shared with a component being replaced.
                // Clone both into the kept namespace so cleanup cannot invalidate the preserved LOD.
                KeepLightMap(package, ref shadowMaps[i], setup);
                if (package.TryGetUExport(shadowMaps[i], out var shadowMap)
                    && shadowMap.ClassName == "ShadowMap2D"
                    && shadowMap.GetProperty<ObjectProperty>("Texture") is { } texture)
                {
                    var textureIndex = texture.Value;
                    KeepLightMap(package, ref textureIndex, setup);
                    texture.Value = textureIndex;
                    shadowMap.WriteProperty(texture);
                }
            }
        }

        /// <summary>Ensures lightmap textures will not be deleted by static lighting import.</summary>
        private static void KeepLightMapTextures(LightMap bin, IMEPackage sourcePackage, LightingImportSetup setup)
        {
            if (bin is LightMap_1D lm1d)
            {
                // No textures
            }
            else if (bin is LightMap_2D lm2d)
            {
                // Has textures
                KeepLightMap(sourcePackage, ref lm2d.Texture1, setup);
                KeepLightMap(sourcePackage, ref lm2d.Texture2, setup);
                KeepLightMap(sourcePackage, ref lm2d.Texture3, setup);
                KeepLightMap(sourcePackage, ref lm2d.Texture4, setup);
            }
            else if (bin is LightMap_3 lm3)
            {
                // No textures
            }
            else if (bin is LightMap_4or6 lm46)
            {
                // Has textures
                KeepLightMap(sourcePackage, ref lm46.Texture1, setup);
                KeepLightMap(sourcePackage, ref lm46.Texture2, setup);
                KeepLightMap(sourcePackage, ref lm46.Texture3, setup);
            }
            else if (bin is LightMap_5 lm5)
            {
                // No textures
            }
        }

        private static void KeepLightMap(IMEPackage sourcePackage, ref int textureUIndex, LightingImportSetup setup)
        {
            if (sourcePackage.TryGetUExport(textureUIndex, out var lmTex))
            {
                if (lmTex.ObjectName.Name.StartsWith(setup.KeptLightmapPrefix))
                {
                    return; // Already kept
                }

                var keptVersion = sourcePackage.FindExport(setup.KeptLightmapPrefix + lmTex.InstancedFullPath);
                if (keptVersion != null)
                {
                    // Already kept
                    textureUIndex = keptVersion.UIndex;
                    return;
                }
                var cloneTex = EntryCloner.CloneEntry(lmTex, incrementIndex: false); // Do not increment or previous check will fail
                cloneTex.ObjectName = new NameReference(setup.KeptLightmapPrefix + cloneTex.ObjectName.Name, cloneTex.ObjectName.Number);
                Debug.WriteLine($"Kept lightmap texture {cloneTex.InstancedFullPath}");
                textureUIndex = cloneTex.UIndex;
            }
        }
    }
}
