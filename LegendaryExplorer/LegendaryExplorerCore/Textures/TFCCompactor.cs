using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Textures.Studio;

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
        /// Maps CRC -> where it goes in new TFCs
        /// </summary>
        public Dictionary<uint, TFCInfo> OutputMapping = new();
    }

    /// <summary>
    /// Describes where to put TFC data
    /// </summary>
    public class TFCInfo
    {
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

    class TFCCompactor
    {
        private readonly TFCCompactorInfoPackage infoPackage;
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
                    return tfc.tfcInfo;
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
            int tfcIdx = -1;
            while (true)
            {
                tfcIdx++;
                var tfcName = $"{infoPackage.TFCType}{tfcIdx}_{infoPackage.DLCName}.tfc";
                var testTFCPath = Path.Combine(infoPackage.StagingPath, tfcName);
                if (File.Exists(testTFCPath))
                    continue; // go to next available name

                // Init TFC
                using var fs = File.Open(testTFCPath, FileMode.Create, FileAccess.Write);
                Guid g = new Guid();
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
                BaseCompactionPath = @"C:\Users\Mgamerz\Desktop\M3\mods\ME3\Alliance Warpack",
                DLCName = "Textures_DLC_MOD_EGM_Alliance",
                StagingPath = @"D:\NIOut",
                DependentDLC = new string[] { }
            };

            TFCCompactor compactor = new TFCCompactor(infoPackage);

            var rootNodes = new List<TextureMapMemoryEntry>();
            var textureMap = TextureMapGenerator.GenerateMapForFolder(infoPackage.BaseCompactionPath, x => new TextureMapMemoryEntry(x), x => rootNodes.Add(x), progressDelegate, cts);

            // CRC Check
            if (textureMap.CalculatedMap.Any(x => x.HasUnmatchedCRCs))
            {
                // ERROR: UNMATCHED CRCS
                // Textures are not properly named and unique
                errorCallback?.Invoke("There are unmatched CRCs in this directory. Textures with same full instanced paths must have the same textures.");
                return;
            }

            // Copy external data to new TFC(s)
            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(textureMap.Game, includeTFCs: true, gameRootOverride: infoPackage.GamePath);

            // add TFCs from the directory we're compacting so we can pull data out
            foreach (var basePathTFC in Directory.GetFiles(infoPackage.BaseCompactionPath, "*.tfc",
                SearchOption.AllDirectories))
            {
                gameFiles[Path.GetFileName(basePathTFC)] = basePathTFC;
            }


            foreach (var v in textureMap.CalculatedMap)
            {
                var entries = v.GetAllTextureEntries().ToList();
                foreach (var entry in entries)
                {
                    var diskSize = entry.GetExternalDiskSize();

                    var destTFCInfo = compactor.GetOutTFC(diskSize);

                    var destTFCPath = Path.Combine(infoPackage.StagingPath, destTFCInfo.TFCName);
                    var outStream = File.OpenWrite(destTFCPath);

                    var texInfo = entry.Instances[0];

                    var inStream = File.OpenRead(gameFiles[texInfo.TFCName + ".tfc"]);
                    foreach (var mipInfo in texInfo.CompressedMipInfos)
                    {
                        inStream.Seek(mipInfo.Offset, SeekOrigin.Begin);
                        inStream.CopyToEx(outStream, mipInfo.CompressedSize);
                    }
                }
            }
        }
    }
}
