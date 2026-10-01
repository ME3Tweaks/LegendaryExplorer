using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        /// Opt-in filter to split candidate textures by root position: null (default) compacts everything matching
        /// <see cref="TFCsToCompact"/> with no further filtering (the historical behavior). Set true to only compact
        /// root textures (idxLink == 0 - lightmaps are always root), or false to only compact non-root textures.
        /// Used by callers that build separate Lighting/Textures TFCs from the same source pool.
        /// </summary>
        public bool? RootTexturesOnly { get; set; }

        /// <summary>
        /// Opt-in: only modify textures whose external data is moved into the new TFC(s). Textures with no external data
        /// (package stored, even if they still have TFC properties), and textures in TFCs not listed in
        /// <see cref="TFCsToCompact"/>, are left exactly as they are instead of being cleaned up. Packages that contain no
        /// moved textures are therefore not saved. Used when the compacted result must differ from the input as little as
        /// possible, such as when the differences are shipped as patches.
        /// </summary>
        public bool OnlyUpdateRelocatedTextures { get; set; }

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

    /// <summary>
    /// A texture queued to be copied into a new TFC. Built up serially, then its mip data is
    /// read/converted in parallel (<see cref="MipBuffers"/>), and finally written out serially.
    /// </summary>
    file sealed class TextureCopyWorkItem
    {
        public TextureCopyWorkItem(TextureMapPackageEntry texInfo, TFCInfo destTFCInfo, string entryDisplayName)
        {
            TexInfo = texInfo;
            DestTFCInfo = destTFCInfo;
            EntryDisplayName = entryDisplayName;
        }

        public TextureMapPackageEntry TexInfo { get; }
        public TFCInfo DestTFCInfo { get; }
        public string EntryDisplayName { get; }

        /// <summary>
        /// Per-mip data ready to write to the destination TFC, populated by the parallel read/convert phase.
        /// </summary>
        public byte[][] MipBuffers { get; set; }
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
            foreach (var basePathTFC in Directory.GetFiles(infoPackage.BaseCompactionPath, "*.tfc", SearchOption.AllDirectories))
            {
                gameFiles[Path.GetFileName(basePathTFC)] = basePathTFC;
            }

            // Packages are not updated in this step so if something goes wrong it doesn't blow up the files
            progressDelegate?.Invoke("Building TFC files", 0, 0);

            // Phase 1 (serial): decide which textures need copying and which output TFC each goes to.
            // CRC de-duplication and destination-TFC selection both depend on state that changes as
            // each texture is claimed, so this has to stay a single pass.
            var copyWorkItems = new List<TextureCopyWorkItem>();
            foreach (var v in textureMap.CalculatedMap)
            {
                foreach (var entry in v.GetAllTextureEntries())
                {
                    if (!compactor.infoPackage.TFCsToCompact.Contains(entry.Instances[0].TFCName))
                        continue;

                    if (compactor.infoPackage.RootTexturesOnly is { } rootTexturesOnly && entry.Instances[0].IsRootTexture != rootTexturesOnly)
                        continue;

                    if (compactor.infoPackage.OnlyUpdateRelocatedTextures && entry.Instances[0].CompressedMipInfos.Count == 0)
                        continue; // No external data to move

                    if (compactor.CRCMap.ContainsKey(entry.GetCRC()))
                        continue; // Texture doesn't need ported into TFC

                    // texture has not been copied to TFC
                    var texInfo = entry.Instances[0];
                    var diskSize = entry.GetExternalDiskSize();
                    var destTFCInfo = compactor.GetOutTFC(diskSize); // Get TFC to write to
                    compactor.CRCMap[texInfo.CRC] = destTFCInfo; // Claim now so repeated CRCs aren't queued twice

                    copyWorkItems.Add(new TextureCopyWorkItem(texInfo, destTFCInfo, entry.InstancedFullPath));
                }
            }

            // Phase 2 (parallel): read + decompress/recompress each texture's mip data into memory.
            // This is the CPU-heavy part and each texture is independent of the others. Source TFCs
            // are opened with FileShare.Read (the File.OpenRead default) so concurrent readers of the
            // same source TFC don't block each other; each work item gets its own handle rather than
            // sharing one FileStream across threads.
            int totalToCopy = copyWorkItems.Count;
            int completedCount = 0;
            int highestReported = 0;
            var progressLock = new object();
            Parallel.ForEach(copyWorkItems, workItem =>
            {
                var texInfo = workItem.TexInfo;
                workItem.MipBuffers = new byte[texInfo.CompressedMipInfos.Count][];

                using var inStream = File.OpenRead(gameFiles[$"{texInfo.TFCName}.tfc"]);
                for (int i = 0; i < texInfo.CompressedMipInfos.Count; i++)
                {
                    var mipInfo = texInfo.CompressedMipInfos[i];
                    inStream.Seek(mipInfo.Offset, SeekOrigin.Begin);

                    if (NeedsCompressionConverted(mipInfo.StorageType, textureMap.Game))
                    {
                        Debug.WriteLine($@"Converting external storage type from {mipInfo.StorageType} to {GetTargetExternalStorageType(textureMap.Game)}");
                        var decompressed = new byte[mipInfo.UncompressedSize];
                        TextureCompression.DecompressTexture(decompressed, inStream, mipInfo.StorageType, mipInfo.UncompressedSize, mipInfo.CompressedSize);

                        // Recompress texture
                        workItem.MipBuffers[i] = TextureCompression.CompressTexture(decompressed, GetTargetExternalStorageType(textureMap.Game));
                    }
                    else
                    {
                        var raw = new byte[mipInfo.CompressedSize];
                        inStream.ReadExactly(raw);
                        workItem.MipBuffers[i] = raw;
                    }
                }

                int done = Interlocked.Increment(ref completedCount);

                // Threads can finish (and reach this point) in a different order than they acquire
                // the lock below, so a lower `done` value can arrive here after a higher one already
                // has. Only forward strictly-increasing values so the reported progress never goes
                // backwards, even though individual textures complete out of order.
                lock (progressLock)
                {
                    if (done > highestReported)
                    {
                        highestReported = done;
                        progressDelegate?.Invoke("Building TFC files", done, totalToCopy);
                    }
                }
            });

            // Phase 3 (serial): write the precomputed buffers to their destination TFCs in order,
            // recording offsets as we go (offsets depend on write order within a TFC, so this can't
            // be parallelized). One stream per destination TFC is kept open and reused instead of
            // reopening the file for every texture.
            var openOutputStreams = new Dictionary<string, FileStream>();
            try
            {
                foreach (var workItem in copyWorkItems)
                {
                    var destTFCInfo = workItem.DestTFCInfo;
                    if (!openOutputStreams.TryGetValue(destTFCInfo.TFCName, out var outStream))
                    {
                        var destTFCPath = Path.Combine(infoPackage.StagingPath, $"{destTFCInfo.TFCName}.tfc");
                        outStream = File.Open(destTFCPath, FileMode.Append, FileAccess.Write);
                        openOutputStreams[destTFCInfo.TFCName] = outStream;
                    }

                    for (int i = 0; i < workItem.MipBuffers.Length; i++)
                    {
                        Debug.WriteLine($@"Writing texture data to TFC: {workItem.EntryDisplayName}, mip {i}, offset: {outStream.Position:X8}");
                        destTFCInfo.MipOffsetMap[i] = (int)outStream.Position;
                        destTFCInfo.MipCompressedSizeMap[i] = workItem.MipBuffers[i].Length;
                        outStream.Write(workItem.MipBuffers[i]);
                    }
                }
            }
            finally
            {
                foreach (var s in openOutputStreams.Values)
                {
                    s.Dispose();
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

                        if (infoPackage.OnlyUpdateRelocatedTextures && (tu.CompressedMipInfos.Count == 0 || !infoPackage.TFCsToCompact.Contains(tu.TFCName) || !compactor.CRCMap.ContainsKey(tu.CRC)))
                        {
                            continue; // None of this texture's data was moved, so leave it untouched
                        }
                        var exportToUpdate = package.FindExport(tu.ExportPath);
                        if (exportToUpdate == null)
                        {
                            Debug.WriteLine($@"Could not find export {tu.ExportPath} in package {namedPackage}! Skipping update on this file");
                            continue;
                        }

                        // Ensure you use .From() and not the typed version so it properly serializes back.
                        var t2d = (UTexture2D) ObjectBinary.From(exportToUpdate);
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

                        if (!tu.HasExternalReferences)
                        {
                            // Remove external reference properties
                            exportToUpdate.RemoveProperty("TFCFileGuid");
                            exportToUpdate.RemoveProperty("TextureFileCacheName");
                        }
                    }

                    if (package.IsModified)
                    {
                        package.Save();
                    }
                }
            }

            var dlcFolderDir = Directory.GetFileSystemEntries(infoPackage.BaseCompactionPath, infoPackage.DLCName, SearchOption.AllDirectories).FirstOrDefault();
            if (dlcFolderDir == null || Path.GetFileName(infoPackage.BaseCompactionPath) == infoPackage.DLCName)
                dlcFolderDir = infoPackage.BaseCompactionPath;
            if (dlcFolderDir != null)
            {
                string destPath = null;
                if (Path.GetFileName(dlcFolderDir) == infoPackage.Game.CookedDirName())
                {
                    destPath = dlcFolderDir;
                }
                else
                {
                    destPath = Path.Combine(dlcFolderDir, infoPackage.Game.CookedDirName());
                }

                // Delete existing TFCs. When RootTexturesOnly is set, the caller is building separate Lighting/Textures
                // TFCs from the same source pool via multiple calls, so only this pass's own TFCType is wiped -
                // otherwise this pass would delete the sibling TFC another pass just built. Callers not using the
                // split (RootTexturesOnly is null) keep the original behavior of wiping every TFC in the folder.
                var tfcsToDelete = Directory.GetFileSystemEntries(dlcFolderDir, "*.tfc", SearchOption.AllDirectories).Where(x => !x.StartsWith(infoPackage.StagingPath));
                if (infoPackage.RootTexturesOnly.HasValue)
                {
                    tfcsToDelete = tfcsToDelete.Where(x => Path.GetFileName(x).StartsWith(infoPackage.TFCType, StringComparison.OrdinalIgnoreCase));
                }
                foreach (var tfc in tfcsToDelete)
                {
                    File.Delete(tfc);
                }

                foreach (var tfc in compactor.GetAllTFCs())
                {
                    var tfcSource = Path.Combine(infoPackage.StagingPath, $"{tfc.TFCName}.tfc");
                    File.Move(tfcSource, Path.Combine(destPath, Path.GetFileName(tfcSource)));
                }


                if (compactor.infoPackage.UseIndexing)
                {
                    // Stub TFC
                    File.WriteAllBytes(Path.Combine(destPath, $"{infoPackage.TFCType}_{compactor.infoPackage.DLCName}.tfc"), Guid.NewGuid().ToByteArray());
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