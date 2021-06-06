using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures.Studio;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Textures
{
    public class TFCCompactorInfoPackage
    {
        /// <summary>
        /// Path containing package files that will be scanned and compacted.
        /// </summary>
        public string BaseCompactionPath { get; set; }

        /// <summary>
        /// Base name of the TFC to generate, such as Textures_DLC_MOD_Puzzle (do not include numbers or extensions)
        /// </summary>
        public string DLCName { get; set; }

        /// <summary>
        /// The path to the game that will be used to pull in textures from DLC (ME2/3 only, but still required for validation)
        /// </summary>
        public string GamePath { get; set; }

        /// <summary>
        /// List of DLC foldernames that should not be pulled in, as they are considered 'dependent', and therefore will always be present in-game
        /// </summary>
        public string[] DependentDLC { get; set; }

        /// <summary>
        /// Where data is staged during compaction
        /// </summary>
        public string StagingPath { get; set; }

        /// <summary>
        /// TFC type, such as 'Textures', 'Lighting', etc. Should probably always just use Textures
        /// </summary>
        public string TFCType { get; set; }

        /// <summary>
        /// If the TFC should use indexing. This is true for LE, false for OT
        /// </summary>
        public bool UseIndexing { get; set; }

        /// <summary>
        /// Maps CRC -> where it goes in new TFCs
        /// </summary>
        public Dictionary<uint, TFCInfo> OutputMapping = new();
    }

    /// <summary>
    /// Describes where to put TFC data for a specific texture
    /// </summary>
    public class TFCInfo
    {
        public TFCInfo()
        {
        }

        /// <summary>
        /// Copy constructor. Drops the mip mapping
        /// </summary>
        /// <param name="clone"></param>
        public TFCInfo(TFCInfo clone)
        {
            TFCGuid = clone.TFCGuid;
            TFCName = clone.TFCName;
        }

        /// <summary>
        /// Guid of referenced TFC
        /// </summary>
        public Guid TFCGuid { get; set; }

        /// <summary>
        /// Name of TFC
        /// </summary>
        public string TFCName { get; set; }

        /// <summary>
        /// Maps mip level -> offset in TFC
        /// </summary>
        public Dictionary<int, int> MipOffsetMap = new();
    }

    public class TFCCompactor
    {
        private readonly TFCCompactorInfoPackage infoPackage;

        /// <summary>
        /// Maps a CRC to where the texture is located in the new compilation
        /// </summary>
        public readonly Dictionary<uint, TFCInfo> CRCMap = new();

        private List<(TFCInfo tfcInfo, int currentSize)> tfcSizes = new(); // how big each TFC is, so we don't have to open them or do FileInfo()

        private TFCCompactor(TFCCompactorInfoPackage infoPackage)
        {
            this.infoPackage = infoPackage;
        }

        /// <summary>
        /// Gets the next TFC that can store the specified amount of data
        /// </summary>
        /// <param name="requiredSize"></param>
        /// <returns></returns>
        public TFCInfo GetOutTFC(int requiredSize)
        {
            if (!tfcSizes.Any())
            {
                return InitTFC();
            }

            foreach (var tfc in tfcSizes)
            {
                if (tfc.currentSize + requiredSize < int.MaxValue)
                {
                    return new TFCInfo(tfc.tfcInfo);
                }
            }

            return InitTFC();
        }

        /// <summary>
        /// Initializes a new TFC file
        /// </summary>
        /// <returns></returns>
        private TFCInfo InitTFC()
        {
            int? tfcIdx = infoPackage.UseIndexing ? -1 : null;
            while (true)
            {
                if (infoPackage.UseIndexing)
                {
                    tfcIdx++;
                }

                var tfcName = $"{infoPackage.TFCType}{tfcIdx}_{infoPackage.DLCName}";
                var testTFCPath = Path.Combine(infoPackage.StagingPath, $"{tfcName}.tfc");
                if (File.Exists(testTFCPath))
                    continue; // go to next available name

                // Init TFC
                using var fs = File.Open(testTFCPath, FileMode.Create, FileAccess.Write);
                Guid g = Guid.NewGuid();
                fs.WriteGuid(g);
                var nTfc = new TFCInfo() { TFCGuid = g, TFCName = tfcName };

                tfcSizes.Add(nTfc, 16);
                return nTfc;
            }
        }

        public static void CompactTFC(TFCCompactorInfoPackage infoPackage, Action<string> errorCallback, Action<string, int, int> progressDelegate = null, CancellationToken cts = default)
        {
            // TESTING
            infoPackage = new TFCCompactorInfoPackage()
            {
                BaseCompactionPath = @"X:\m3modlibrary\ME3\Fanciful EDI Armor Variations - Movies TV and Games\DLC_MOD_FancifulEDIMovie",
                DLCName = "DLC_MOD_FancifulEDIMovie",
                StagingPath = @"X:\ML2",
                TFCType = "Textures",
                DependentDLC = new string[] { }
            };

            TFCCompactor compactor = new TFCCompactor(infoPackage);

            var rootNodes = new List<TextureMapMemoryEntry>();
            var textureMap = TextureMapGenerator.GenerateMapForFolder(infoPackage.BaseCompactionPath, x => new TextureMapMemoryEntry(x), x => rootNodes.Add(x), progressDelegate, cts);
            Debug.WriteLine($@"Texture map count: {textureMap.CalculatedMap.Count}");

            // CRC Check
            if (textureMap.CalculatedMap.Any(x => x.HasUnmatchedCRCs))
            {
                // ERROR: UNMATCHED CRCS
                // Textures are not properly named and unique
                errorCallback?.Invoke("There are unmatched CRCs in this directory. Textures with same full instanced paths must have the same textures.");
                return;
            }

            // Copy external data to new TFC(s)--------------------
            foreach (var f in Directory.GetFiles(infoPackage.StagingPath, "*.tfc", SearchOption.TopDirectoryOnly))
            {
                File.Delete(f); // Delete existing TFCs in staging dir
            }
            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(textureMap.Game, includeTFCs: true, gameRootOverride: infoPackage.GamePath);

            // add TFCs from the directory we're compacting so we can pull data out
            foreach (var basePathTFC in Directory.GetFiles(infoPackage.BaseCompactionPath, "*.tfc",
                SearchOption.AllDirectories))
            {
                gameFiles[Path.GetFileName(basePathTFC)] = basePathTFC;
            }

            // Packages are not updated in this step so if something goes wrong it doesn't blow up the files
            foreach (var v in textureMap.CalculatedMap)
            {
                var entries = v.GetAllTextureEntries().ToList();
                foreach (var entry in entries)
                {
                    if (compactor.CRCMap.TryGetValue(entry.GetCRC(), out var portedInfo))
                    {
                        // Texture doesn't need ported into TFC
                    }
                    else
                    {
                        // texture has not been copied to TFC
                        var texInfo = entry.Instances[0];
                        var diskSize = entry.GetExternalDiskSize();
                        var destTFCInfo = compactor.GetOutTFC(diskSize); // Get TFC to write to
                        var destTFCPath = Path.Combine(infoPackage.StagingPath, $"{destTFCInfo.TFCName}.tfc");
                        using var outStream = File.Open(destTFCPath, FileMode.Append, FileAccess.Write);
                        using var inStream = File.OpenRead(gameFiles[$"{texInfo.TFCName}.tfc"]);
                        for (int i = 0; i < texInfo.CompressedMipInfos.Count; i++)
                        {
                            var mipInfo = texInfo.CompressedMipInfos[i];
                            destTFCInfo.MipOffsetMap[i] = (int)outStream.Position;
                            inStream.Seek(mipInfo.Offset, SeekOrigin.Begin);
                            inStream.CopyToEx(outStream, mipInfo.CompressedSize);
                        }

                        // Add to the map
                        compactor.CRCMap[texInfo.CRC] = destTFCInfo;
                    }
                }
            }

            // Build list of package updates
            Dictionary<string, List<TextureMapPackageEntry>> packageTextureMap = new(); // Maps a package name to all instances that need to be updated, so we can open a single package at a time and update it
            foreach (var v in textureMap.CalculatedMap)
            {
                // Get all texture leaves
                var entries = v.GetAllTextureEntries().ToList();
                foreach (var entry in entries)
                {
                    foreach (var instance in entry.Instances)
                    {
                        // Build list of TextureMapPackageEntry
                        List<TextureMapPackageEntry> packageTexturesToUpdate;
                        if (!packageTextureMap.TryGetValue(instance.PackageName, out packageTexturesToUpdate))
                        {
                            packageTexturesToUpdate = new List<TextureMapPackageEntry>();
                            packageTextureMap[instance.PackageName] = packageTexturesToUpdate;
                        }

                        packageTexturesToUpdate.Add(instance);
                    }
                }
            }

            // Update packages
            var packagePool = Directory.GetFileSystemEntries(infoPackage.BaseCompactionPath, "*.*", SearchOption.AllDirectories).ToList();
            foreach (var packageNamePair in packageTextureMap)
            {
                // Mods may have multiple same-named packages, if for example, they use alternates in M3
                // So we can't use a mapping of filenames. Hopefully files don't diverge far enough that this won't matter...
                foreach (var namedPackage in packagePool.Where(x => Path.GetFileName(x).Equals(packageNamePair.Key, StringComparison.InvariantCultureIgnoreCase)))
                {
                    using var package = MEPackageHandler.OpenMEPackage(namedPackage, forceLoadFromDisk: true);
                    foreach (var tu in packageNamePair.Value)
                    {
                        var exportToUpdate = package.FindExport(tu.ExportPath);
                        if (exportToUpdate == null)
                        {
                            Debug.WriteLine($@"Could not find export {tu.ExportPath} in package {namedPackage}, skipping update");
                        }
                        var t2d = ObjectBinary.From<UTexture2D>(exportToUpdate);
                        t2d.Mips.RemoveAll(x => x.StorageType == StorageTypes.empty); // Remove empty mips

                        // Update offset
                        var updateInfo = compactor.CRCMap[tu.CRC];
                        for (int i = 0; i < updateInfo.MipOffsetMap.Count; i++)
                        {
                            t2d.Mips[i].DataOffset = updateInfo.MipOffsetMap[i];
                        }

                        exportToUpdate.WriteBinary(t2d);

                        // Update TFC properties
                        var properties = exportToUpdate.GetProperties();
                        properties.AddOrReplaceProp(new FGuid(updateInfo.TFCGuid).ToStructProperty("TFCFileGuid"));
                        properties.AddOrReplaceProp(new NameProperty(updateInfo.TFCName, "TextureFileCacheName"));
                        exportToUpdate.WriteProperties(properties);
                    }

                    if (package.IsModified)
                    {
                        package.Save();
                    }
                }
            }
        }
    }
}