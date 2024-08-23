using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.UDK
{
    public static class StaticLightingImporter
    {
        /// <summary>
        /// Imports static lighting from UDK.
        /// </summary>
        /// <param name="package">The persistent package file. It MUST contain the AdditionalPackagesToCook or it will not import it. Tags must be set on StaticMeshActors to map their source.</param>
        public static void ImportStaticLighting(IMEPackage package, string baseUdkMapPath = null)
        {
            var persistentPackage = (MEPackage)package;
            var basePath = Directory.GetParent(persistentPackage.FilePath).FullName;
            using var persistentUdk = GetUDKMapPackage(persistentPackage.FileNameNoExtension, baseUdkMapPath);
            if (persistentUdk == null)
                return; // Package not found
            AssignStaticLighting(persistentPackage, persistentUdk);

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
                    if (lod.LightMap is LightMap_1D lm1d)
                    {
                        // No textures
                    }
                    else if (lod.LightMap is LightMap_2D lm2d)
                    {
                        // Has textures
                        TrashRef(mePackage, ref lm2d.Texture1);
                        TrashRef(mePackage, ref lm2d.Texture2);
                        TrashRef(mePackage, ref lm2d.Texture3);
                        TrashRef(mePackage, ref lm2d.Texture4);
                    }
                    else if (lod.LightMap is LightMap_3 lm3)
                    {
                        // No textures
                    }
                    else if (lod.LightMap is LightMap_4or6 lm46)
                    {
                        // Has textures
                        TrashRef(mePackage, ref lm46.Texture1);
                        TrashRef(mePackage, ref lm46.Texture1);
                        TrashRef(mePackage, ref lm46.Texture1);
                    }
                    else if (lod.LightMap is LightMap_5 lm5)
                    {
                        // No textures
                    }
                }
                smc.WriteBinary(meBin);
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
        /// Ports a lightmap from UDK into the given MEPackage, Package stored. For it to work properly it must be moved to TFC as lighting requires TFC
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
            if (mePackage.FindExport(udkTex.InstancedFullPath) != null)
            {
                // Already in the file
                textureUIndex = mePackage.FindExport(udkTex.InstancedFullPath).UIndex;
                return;
            }

            // Port it
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, udkTex, mePackage, null, true, new RelinkerOptionsPackage(), out var lightmap);
            textureUIndex = lightmap.UIndex;
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
                if (tag != null && tag.Value.Name == udkPackage.FileNameNoExtension)
                {
                    meToUdkRemapping[mePackage.GetUExport(tag.Value.Number)] = smc;
                }
            }


            // Remove so we don't have any collisions
            ClearLightmaps(mePackage);

            // Import the lightamps
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
                        if (lod.LightMap is LightMap_1D lm1d)
                        {
                            // No textures
                            lm1d.Owner = smc.UIndex;
                        }
                        else if (lod.LightMap is LightMap_2D lm2d)
                        {
                            // Has textures
                            PortLightMap(mePackage, ref lm2d.Texture1, udkPackage);
                            PortLightMap(mePackage, ref lm2d.Texture2, udkPackage);
                            PortLightMap(mePackage, ref lm2d.Texture3, udkPackage);
                            PortLightMap(mePackage, ref lm2d.Texture4, udkPackage);
                        }
                        else if (lod.LightMap is LightMap_3 lm3)
                        {
                            // No textures
                        }
                        else if (lod.LightMap is LightMap_4or6 lm46)
                        {
                            // Has textures
                            PortLightMap(mePackage, ref lm46.Texture1, udkPackage);
                            PortLightMap(mePackage, ref lm46.Texture2, udkPackage);
                            PortLightMap(mePackage, ref lm46.Texture3, udkPackage);
                        }
                        else if (lod.LightMap is LightMap_5 lm5)
                        {
                            // No textures
                        }
                    }

                    smc.WriteBinary(meBin);
                }
                else
                {
                    Debug.WriteLine($"Did not find UDK SMC in remapping: {smc.InstancedFullPath}");
                }
            }
        }
    }
}
