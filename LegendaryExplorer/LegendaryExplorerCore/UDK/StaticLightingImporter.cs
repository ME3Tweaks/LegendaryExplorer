using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.UDK
{
    public static class StaticLightingImporter
    {
        /// <summary>
        /// Imports static lighting from UDK.
        /// </summary>
        /// <param name="package">The persistent package file. To import sublevels, the package must have AdditionalPackagesToCook set in the package. Tags must be set on StaticMeshActors to map their source.</param>
        public static void ImportStaticLighting(IMEPackage package, string baseUdkMapPath = null, bool includeSublevels = true)
        {
            var persistentPackage = (MEPackage)package;
            var basePath = Directory.GetParent(persistentPackage.FilePath).FullName;
            using var persistentUdk = GetUDKMapPackage(persistentPackage.FileNameNoExtension, baseUdkMapPath);
            if (persistentUdk == null)
                return; // Package not found
            AssignStaticLighting(persistentPackage, persistentUdk);

            if (includeSublevels)
            {
                foreach (var subLevel in persistentPackage.AdditionalPackagesToCook)
                {
                    var mePath = Path.Combine(basePath, subLevel + ".pcc");
                    using var mePackage = (MEPackage)MEPackageHandler.OpenMEPackage(mePath);
                    using var udkPackage = GetUDKMapPackage(subLevel);
                    AssignStaticLighting(mePackage, udkPackage);
                    if (mePackage.IsModified)
                        mePackage.Save();
                }
            }
        }

        /// <summary>
        /// Trashes all lightmaps in a file in preparing for bringing in new lightmap data
        /// </summary>
        /// <param name="mePackage"></param>
        private static void ClearLightmaps(IMEPackage mePackage)
        {
            foreach (var smc in mePackage.Exports.Where(x => x.IsA("StaticMeshComponent")).ToList())
            {
                var meBin = ObjectBinary.From<StaticMeshComponent>(smc);
                foreach (var lod in meBin.LODData)
                {
                    ClearLightmap(lod.LightMap, mePackage);
                }

                smc.WriteBinary(meBin);
            }

            foreach (var mc in mePackage.Exports.Where(x => x.IsA("ModelComponent")).ToList())
            {
                var meBin = ObjectBinary.From<ModelComponent>(mc);
                foreach (var elem in meBin.Elements)
                {
                    ClearLightmap(elem.LightMap, mePackage);
                }

                mc.WriteBinary(meBin);
            }

            foreach (var tc in mePackage.Exports.Where(x => x.IsA("TerrainComponent")).ToList())
            {
                var meBin = ObjectBinary.From<TerrainComponent>(tc);
                ClearLightmap(meBin.LightMap, mePackage);
                tc.WriteBinary(meBin);
            }
        }

        private static void ClearLightmap(LightMap lightMap, IMEPackage mePackage)
        {
            if (lightMap is LightMap_1D lm1d)
            {
                // No textures
            }
            else if (lightMap is LightMap_2D lm2d)
            {
                // Has textures
                TrashRef(mePackage, ref lm2d.Texture1);
                TrashRef(mePackage, ref lm2d.Texture2);
                TrashRef(mePackage, ref lm2d.Texture3);
                TrashRef(mePackage, ref lm2d.Texture4);
            }
            else if (lightMap is LightMap_3 lm3)
            {
                // No textures
            }
            else if (lightMap is LightMap_4or6 lm46)
            {
                // Has textures
                TrashRef(mePackage, ref lm46.Texture1);
                TrashRef(mePackage, ref lm46.Texture1);
                TrashRef(mePackage, ref lm46.Texture1);
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
        private static void TrashRef(IMEPackage mePackage, ref int uindex)
        {
            if (uindex > 0)
            {
                var export = mePackage.GetUExport(uindex);
                if (!export.IsTrash())
                {
                    EntryPruner.TrashEntries(mePackage, [export]);
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
                Debug.WriteLine($"Lightmap already in file: {existing.InstancedFullPath}");
                return;
            }

            // Port it
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, udkTex, mePackage, null, true, new RelinkerOptionsPackage(), out var lightmap);
            textureUIndex = lightmap.UIndex;
            //Debug.WriteLine($"Installed lightmap {lightmap.InstancedFullPath}");

            var lightmapTex = lightmap as ExportEntry;

            // Turn on streaming
            var bin = ObjectBinary.From<LightMapTexture2D>(lightmapTex);
            bin.LightMapFlags = ELightMapFlags.LMF_Streamed;
            lightmapTex.WriteBinary(bin);

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

        private static void AssignStaticLighting(MEPackage mePackage, UDKPackage udkPackage)
        {
            var meToUdkRemapping = new Dictionary<ExportEntry, ExportEntry>();
            foreach (var smc in udkPackage.Exports.Where(x => x.IsA("StaticMeshComponent")))
            {
                var parent = smc.Parent as ExportEntry;
                var tag = parent.GetProperty<NameProperty>("Tag");
                if (tag != null)
                {
                    var exp = mePackage.FindExport(tag.Value.Name, "StaticMeshComponent");
                    if (exp != null)
                    {
                        meToUdkRemapping[exp] = smc;
                    }
                }
            }

            // Non tagged, same IFP
            foreach (var smc in udkPackage.Exports.Where(x => x.IsA("TerrainComponent") || x.IsA("ModelComponent")))
            {
                var meExp = mePackage.FindExport(smc.InstancedFullPath, smc.ClassName);
                if (meExp != null)
                {
                    meToUdkRemapping[meExp] = smc;
                }
            }


            // Remove so we don't have any collisions
            ClearLightmaps(mePackage);

            // Import the lightamps

            // StaticMeshComponent
            foreach (var smc in mePackage.Exports.Where(x => x.IsA("StaticMeshComponent")).ToList())
            {
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
                var meBin = ObjectBinary.From<TerrainComponent>(tc);
                var udkBin = ObjectBinary.From<TerrainComponent>(tcp.Value);
                meBin.LightMap = udkBin.LightMap;
                PortLightMaps(meBin.LightMap, tc, udkPackage); // Must assign UDK version here.
                tc.WriteBinary(meBin);
            }

            // Model
            foreach (var tcp in meToUdkRemapping.Where(x => x.Key.IsA("ModelComponent")).ToList())
            {
                var tc = tcp.Key;
                var udkBin = ObjectBinary.From<ModelComponent>(tcp.Value);
                var meBin = ObjectBinary.From<ModelComponent>(tcp.Value);

                for (int i = 0; i < meBin.Elements.Length; i++)
                {
                    meBin.Elements[i].LightMap = udkBin.Elements[i].LightMap;
                    PortLightMaps(meBin.Elements[i].LightMap, tc, udkPackage);
                }
                tc.WriteBinary(meBin);
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
    }
}
