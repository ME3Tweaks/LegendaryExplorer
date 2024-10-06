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
    }

    public static class StaticLightingImporter
    {
        /// <summary>
        /// Imports static lighting from UDK.
        /// </summary>
        /// <param name="package">The persistent package file. To import sublevels, the package must have AdditionalPackagesToCook set in the package. Tags must be set on StaticMeshActors to map their source.</param>
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
                    var mePath = Path.Combine(basePath, subLevel + ".pcc");
                    using var mePackage = (MEPackage)MEPackageHandler.OpenMEPackage(mePath);
                    using var udkPackage = GetUDKMapPackage(subLevel);
                    AssignStaticLighting(mePackage, udkPackage, setup);
                    if (mePackage.IsModified)
                        mePackage.Save();
                }
            }
        }

        /// <summary>
        /// Trashes all lightmaps in a file in preparing for bringing in new lightmap data
        /// </summary>
        /// <param name="mePackage"></param>
        private static void ClearLightmaps(IMEPackage mePackage, LightingImportSetup setup)
        {
            // StaticMeshes
            foreach (var smc in mePackage.Exports.Where(x => x.IsA("StaticMeshComponent")).ToList())
            {
                var meBin = ObjectBinary.From<StaticMeshComponent>(smc);
                if (setup.ShouldKeepLightMap != null && setup.ShouldKeepLightMap(smc))
                {
                    foreach (var lod in meBin.LODData)
                    {
                        KeepLightMapTextures(lod.LightMap, mePackage, setup);
                    }
                }
                else
                {
                    foreach (var lod in meBin.LODData)
                    {
                        ClearLightmap(lod.LightMap, mePackage, setup);
                    }
                }

                smc.WriteBinary(meBin);
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

            foreach (var tex in setup.LightmapsToTrash)
            {
                if (tex.ObjectName.Name.StartsWith(setup.KeptLightmapPrefix) || tex.IsTrash())
                {
                    // Do not trash
                    continue;
                }

                EntryPruner.TrashEntries(tex.FileRef, [tex]);
            }
            setup.LightmapsToTrash.Clear();
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
        /// Ports a lightmap from UDK into the given MEPackage, package stored. For it to work properly it must be moved to TFC as lighting requires TFC
        /// </summary>
        /// <param name="mePackage"></param>
        /// <param name="textureUIndex">On input, this is the UIndex in the UDKPackage. On exit, it should be the MEPackage UIndex.</param>
        /// <param name="udkPackage"></param>
        /// <exception cref="NotImplementedException"></exception>
        private static void PortLightMap(IMEPackage mePackage, ref int textureUIndex, UDKPackage udkPackage)
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
                var format = mePackage.Game.IsLEGame() ? PixelFormat.BC7 : PixelFormat.DXT1;
                tex.GenerateMips(format);
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



            // Remove so we don't have any collisions
            ClearLightmaps(mePackage, setup);

            // Import the lightmaps

            // StaticMeshComponent
            foreach (var smc in mePackage.Exports.Where(x => x.IsA("StaticMeshComponent")).ToList())
            {
                if (setup.ShouldKeepLightMap?.Invoke(smc) ?? false)
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
                    meBin.LODData = uBin.LODData;
                    foreach (var lod in meBin.LODData)
                    {
                        // References will be updated since it's a copy of UDK version.
                        PortLightMaps(lod.LightMap, smc, udkPackage);
                    }

                    smc.WriteBinary(meBin);
                }
                else
                {
                    Debug.WriteLine($"Did not find UDK SMC in remapping: {smc.InstancedFullPath}");
                }
            }

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
                PortLightMaps(meBin.LightMap, tc, udkPackage); // Must assign UDK version here.
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
                    PortLightMaps(udkBin.Elements[i].LightMap, mc, udkPackage);
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
                        udkModel.Surfs[i].Actor = mePackage.FindEntry(udkPackage.GetEntry(udkModel.Surfs[i].Actor).InstancedFullPath)?.UIndex ?? 0; 
                    }

                    // Write relinked UDK model to ours
                    owningModel.WriteBinary(udkModel);
                    updatedModels.Add(owningModel);
                }
            }
        }

        private static void PortLightMaps(LightMap lmWithUdkRefs, ExportEntry meComponent, UDKPackage udkPackage)
        {
            if (lmWithUdkRefs is LightMap_1D lm1d)
            {
                // No textures
                lm1d.Owner = meComponent.UIndex;
            }
            else if (lmWithUdkRefs is LightMap_2D lm2d)
            {
                // Has textures
                PortLightMap(meComponent.FileRef, ref lm2d.Texture1, udkPackage);
                PortLightMap(meComponent.FileRef, ref lm2d.Texture2, udkPackage);
                PortLightMap(meComponent.FileRef, ref lm2d.Texture3, udkPackage);
                PortLightMap(meComponent.FileRef, ref lm2d.Texture4, udkPackage);
            }
            else if (lmWithUdkRefs is LightMap_3 lm3)
            {
                // No textures
            }
            else if (lmWithUdkRefs is LightMap_4or6 lm46)
            {
                // Has textures
                PortLightMap(meComponent.FileRef, ref lm46.Texture1, udkPackage);
                PortLightMap(meComponent.FileRef, ref lm46.Texture2, udkPackage);
                PortLightMap(meComponent.FileRef, ref lm46.Texture3, udkPackage);
            }
            else if (lmWithUdkRefs is LightMap_5 lm5)
            {
                // No textures
            }
        }

        /// <summary>
        /// Ensures a lightmap reference will not be deleted by static lighting import
        /// </summary>
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
                var cloneTex = EntryCloner.CloneEntry(lmTex);
                cloneTex.ObjectName = new NameReference(setup.KeptLightmapPrefix + cloneTex.ObjectName.Name, cloneTex.ObjectName.Number);
                Debug.WriteLine($"Kept lightmap texture {cloneTex.InstancedFullPath}");
                textureUIndex = cloneTex.UIndex;
            }
        }
    }
}
