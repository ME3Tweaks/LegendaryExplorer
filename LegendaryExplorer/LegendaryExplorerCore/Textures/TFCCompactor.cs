using System;
using System.Collections;
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
using LegendaryExplorerCore.Unreal.Classes;

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
        /// What game this compaction is building a TFC for
        /// </summary>
        public MEGame Game { get; set; }

        /// <summary>
        /// List of TFC names to pull from. Should not include extension and should not include basegame or dependent-dlc textures.
        /// </summary>
        public List<string> TFCsToCompact { get; set; }

        /// <summary>
        /// Maps CRC -> where it goes in new TFCs
        /// </summary>
        public Dictionary<uint, TFCInfo> OutputMapping = new();
    }

    /// <summary>
    /// Describes where to put TFC data for a specific texture
    /// </summary>
    [DebuggerDisplay("TFCInfo {TFCName}, {MipOffsetMap.Count} mip offsets")]
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

        /// <summary>
        /// Maps mip level -> compressed size in TFC
        /// </summary>
        public Dictionary<int, int> MipCompressedSizeMap = new();
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
            int? tfcIdx = infoPackage.UseIndexing ? 0 : null; // 0 cause it gets ++'d before first use
            while (true)
            {
                if (infoPackage.UseIndexing)
                {
                    tfcIdx++;
                }

                //var tfcName = $"{infoPackage.TFCType}_{infoPackage.DLCName}{tfcIdx}";
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

        public static void CompactTFC(TFCCompactorInfoPackage infoPackage, Action<string> errorCallback, Action<string, int, int> progressDelegate = null, TextureMap textureMap = null, CancellationToken cts = default)
        {
            TFCCompactor compactor = new TFCCompactor(infoPackage);

            var rootNodes = new List<TextureMapMemoryEntry>();
            textureMap ??= TextureMapGenerator.GenerateMapForFolder(infoPackage.BaseCompactionPath, x => new TextureMapMemoryEntry(x), x => rootNodes.Add(x), progressDelegate, cts);
            Debug.WriteLine($@"Texture map count: {textureMap.CalculatedMap.Count}");

            // CRC Check
            if (textureMap.CalculatedMap.Any(x => x.HasUnmatchedCRCs))
            {
                // ERROR: UNMATCHED CRCS
                // Textures are not properly named and unique
                errorCallback?.Invoke("There are unmatched CRCs in this directory. Textures with same full instanced paths must have the same textures. Use Texture Studio to identify and fix these issues.");
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
                    if (compactor.infoPackage.TFCsToCompact.Contains(entry.Instances[0].TFCName))
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

                                if (NeedsCompressionConverted(mipInfo.StorageType, textureMap.Game))
                                {
                                    Debug.WriteLine($@"Converting external storage type from {mipInfo.StorageType} to {GetTargetExternalStorageType(textureMap.Game)}");
                                    var decompressed = new byte[mipInfo.UncompressedSize];
                                    TextureCompression.DecompressTexture(decompressed, inStream, mipInfo.StorageType, mipInfo.UncompressedSize, mipInfo.CompressedSize);

                                    // Recompress texture
                                    var compressed = TextureCompression.CompressTexture(decompressed, GetTargetExternalStorageType(textureMap.Game));
                                    outStream.Write(compressed);
                                    destTFCInfo.MipCompressedSizeMap[i] = compressed.Length;
                                }
                                else
                                {
                                    inStream.CopyToEx(outStream, mipInfo.CompressedSize);
                                    destTFCInfo.MipCompressedSizeMap[i] = mipInfo.CompressedSize;
                                }
                            }

                            // Add to the map
                            compactor.CRCMap[texInfo.CRC] = destTFCInfo;
                        }
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
                    progressDelegate?.Invoke($"Updating package {packageNamePair.Key}", -1, -1);
                    using var package = MEPackageHandler.OpenMEPackage(namedPackage, forceLoadFromDisk: true);
                    foreach (var tu in packageNamePair.Value)
                    {
                        if (string.IsNullOrWhiteSpace(tu.TFCName))
                        {
                            continue; // PCC Stored
                        }
                        var exportToUpdate = package.FindExport(tu.ExportPath);
                        if (exportToUpdate == null)
                        {
                            Debug.WriteLine($@"Could not find export {tu.ExportPath} in package {namedPackage}! Skipping update on this file");
                            continue;
                        }

                        var t2d = ObjectBinary.From<UTexture2D>(exportToUpdate);
                        t2d.Mips.RemoveAll(x => x.StorageType == StorageTypes.empty); // Remove empty mips

                        // Update offset
                        if (compactor.CRCMap.TryGetValue(tu.CRC, out var updateInfo))
                        {
                            for (int i = 0; i < updateInfo.MipOffsetMap.Count; i++)
                            {
                                t2d.Mips[i].DataOffset = updateInfo.MipOffsetMap[i];

                                // For conversions
                                t2d.Mips[i].StorageType = GetTargetExternalStorageType(textureMap.Game); //tfc compactor changes external types so this should be OK?
                                t2d.Mips[i].CompressedSize = updateInfo.MipCompressedSizeMap[i];
                            }

                            exportToUpdate.WriteBinary(t2d);

                            // Update TFC properties
                            var properties = exportToUpdate.GetProperties();
                            properties.AddOrReplaceProp(new FGuid(updateInfo.TFCGuid).ToStructProperty("TFCFileGuid"));
                            properties.AddOrReplaceProp(new NameProperty(updateInfo.TFCName, "TextureFileCacheName"));
                            exportToUpdate.WriteProperties(properties);
                        }
                        else if (infoPackage.TFCsToCompact.Contains(tu.TFCName))
                        {
                            Debug.WriteLine($"CRC not found in map 0x{tu.CRC:X8}, {tu.ExportPath} in {tu.TFCName}");
                        }
                    }

                    if (package.IsModified)
                    {
                        package.Save();
                    }
                }
            }

            var dlcFolderDir = Directory.GetFileSystemEntries(infoPackage.BaseCompactionPath, infoPackage.DLCName, SearchOption.AllDirectories).FirstOrDefault();
            if (dlcFolderDir == null && Path.GetFileName(infoPackage.BaseCompactionPath) == infoPackage.DLCName)
                dlcFolderDir = infoPackage.BaseCompactionPath;
            if (dlcFolderDir != null)
            {
                // Delete all existing TFCs
                var tfcsToDelete = Directory.GetFileSystemEntries(dlcFolderDir, "*.tfc", SearchOption.AllDirectories);
                foreach (var tfc in tfcsToDelete)
                {
                    File.Delete(tfc);
                }

                var destPath = Path.Combine(dlcFolderDir, infoPackage.Game.CookedDirName());
                foreach (var tfc in compactor.GetAllTFCs())
                {
                    var tfcSource = Path.Combine(infoPackage.StagingPath, $"{tfc.TFCName}.tfc");
                    File.Move(tfcSource, Path.Combine(destPath, Path.GetFileName(tfcSource)));
                }

                if (compactor.infoPackage.UseIndexing)
                {
                    // Stub TFC
                    File.WriteAllBytes(Path.Combine(destPath, $"Textures_{compactor.infoPackage.DLCName}.tfc"), Guid.NewGuid().ToByteArray());
                }
            }
        }

        public static StorageTypes GetTargetExternalStorageType(MEGame textureMapGame)
        {
            if (textureMapGame is MEGame.ME1 or MEGame.ME2) return StorageTypes.extLZO;
            if (textureMapGame is MEGame.ME3) return StorageTypes.extZlib;
            if (textureMapGame.IsLEGame()) return StorageTypes.extOodle;
            throw new ArgumentOutOfRangeException($"Invalid value {textureMapGame} for {nameof(textureMapGame)} as target type for texture");
        }

        private static bool NeedsCompressionConverted(StorageTypes mipInfoStorageType, MEGame textureMapGame)
        {
            if (textureMapGame is MEGame.ME1 or MEGame.ME2 && mipInfoStorageType != StorageTypes.pccLZO && mipInfoStorageType != StorageTypes.extLZO) return true;
            if (textureMapGame is MEGame.ME3 && mipInfoStorageType != StorageTypes.pccZlib && mipInfoStorageType != StorageTypes.extZlib) return true;
            if (textureMapGame.IsLEGame() && mipInfoStorageType != StorageTypes.pccOodle && mipInfoStorageType != StorageTypes.extOodle) return true;
            return false;
        }

        private IEnumerable<TFCInfo> GetAllTFCs()
        {
            return tfcSizes.Select(x => x.tfcInfo);
        }
    }
}