using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using Newtonsoft.Json;
using PropertyChanged;

namespace LegendaryExplorerCore.Textures.Studio
{

    /// <summary>
    /// Describes a memory-unique texture, e.g. a unique full path.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    [DebuggerDisplay("TextureMapMemoryEntry {Children.Count} children, {Instances.Count} instances, TFC name {TFCName}")]
    public partial class TextureMapMemoryEntry
    {
        /// <summary>
        /// Parses a Texture object
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <param name="textureMapMemoryEntries"></param>
        /// <returns></returns>
        public static TextureMapMemoryEntry ParseTexture(ExportEntry exportEntry, string selectedFolder, Dictionary<string, TextureMapMemoryEntry> textureMapMemoryEntries, Dictionary<string, uint> crcCache,
            List<string> additionalTFCs, Func<IEntry, TextureMapMemoryEntry> generatorDelegate, Action<TextureMapMemoryEntry> addRootNodeDelegate)
        {
            var parent = EnsureParent(exportEntry, selectedFolder, textureMapMemoryEntries, additionalTFCs, generatorDelegate, addRootNodeDelegate);

            if (!textureMapMemoryEntries.TryGetValue(exportEntry.InstancedFullPath, out var memoryEntry))
            {
                memoryEntry = generatorDelegate(exportEntry);
                memoryEntry.Parent = parent;
                textureMapMemoryEntries[exportEntry.InstancedFullPath] = memoryEntry;
                parent?.Children.Add(memoryEntry);
            }

            // Add our instance to the memory entry
            if (memoryEntry.IsSameOffsets(exportEntry))
            {
                // Same TFC name / GUID / offsets
                memoryEntry.Instances.Add(new TextureMapPackageEntry(selectedFolder, exportEntry, additionalTFCs));
            }
            else
            {
                // Parse it out
                memoryEntry.Instances.Add(new TextureMapPackageEntry(selectedFolder, exportEntry, additionalTFCs, crcCache));
            }

            return memoryEntry;
        }

        private bool IsSameOffsets(ExportEntry exportEntry)
        {
            Texture2D utd = new Texture2D(exportEntry);
            var topMip = utd.GetTopMip();
            if (topMip.externalOffset == 0 || TopMipOffset != topMip.externalOffset)
            {
                // Force calculation
                return false;
            }

            Guid tfcGuid = default;
            var guid = exportEntry.GetProperty<StructProperty>("TFCFileGuid");
            if (guid != null)
                tfcGuid = new FGuid(guid).ToGuid();

            if (TFCGuid != tfcGuid)
            {
                // Different TFC
                return false;
            }

            if (topMip.TextureCacheName != TFCName)
            {
                // Different TFC
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates all parents of the specified export in the texture tree, if necessary
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <param name="textureMapMemoryEntries"></param>
        /// <returns></returns>
        private static TextureMapMemoryEntry EnsureParent(ExportEntry exportEntry, string selectedFolder, Dictionary<string, TextureMapMemoryEntry> textureMapMemoryEntries, List<string> additionalTFCs, Func<IEntry, TextureMapMemoryEntry> generatorDelegate, Action<TextureMapMemoryEntry> addRootNodeDelegate, Dictionary<string, uint> crcCache = null)
        {
            IEntry parentT = exportEntry;
            List<IEntry> parents = new List<IEntry>();
            while (parentT.HasParent)
            {
                parents.Insert(0, parentT.Parent);
                parentT = parentT.Parent;
            }


            TextureMapMemoryEntry lastParent = null;
            for (int i = 0; i < parents.Count; i++)
            {
                var p = parents[i];
                if (!textureMapMemoryEntries.TryGetValue(p.InstancedFullPath, out lastParent))
                {
                    if (p.IsTexture() && p is ExportEntry pe)
                    {
                        // Parent is texture. Normally this doesn't occur but devs be devs
                        lastParent = ParseTexture(pe, selectedFolder, textureMapMemoryEntries, crcCache, additionalTFCs, generatorDelegate, addRootNodeDelegate);
                    }
                    else
                    {
                        // Parent doesn't exist, create
                        lastParent = generatorDelegate(p);
                        lastParent.Parent = i > 0 ? textureMapMemoryEntries[parents[i - 1].InstancedFullPath] : null;
                        // Set the parent child
                        lastParent.Parent?.Children.Add(lastParent);
                        if (lastParent.Parent == null)
                        {
                            addRootNodeDelegate?.Invoke(lastParent);
                        }
                    }

                    textureMapMemoryEntries[p.InstancedFullPath] = lastParent;
                }

            }

            return lastParent;
        }

        public TextureMapMemoryEntry(IEntry iEntry)
        {
            IsTexture = iEntry.IsTexture();
            ObjectName = iEntry.ObjectName.Instanced;
        }

        /// <summary>
        /// If this entry represents a 'texture' and is not actually something else, such as cubemap or package.
        /// </summary>
        public bool IsTexture { get; set; }

        /// <summary>
        /// The parent entry, most times a package.
        /// </summary>
        public TextureMapMemoryEntry Parent { get; set; }

        /// <summary>
        /// The object name. (Should be changed to NameReference!)
        /// </summary>
        public string ObjectName { get; set; }

        /// <summary>
        /// The full instanced path of this entry
        /// </summary>
        public string InstancedFullPath
        {
            get
            {
                if (Parent != null)
                {
                    return $"{Parent.InstancedFullPath}.{ObjectName}";
                }
                return ObjectName;
            }
        }

        /// <summary>
        /// The instances of this entry.
        /// </summary>
        public virtual ObservableCollectionExtended<TextureMapPackageEntry> Instances { get; } = new();

        /// <summary>
        /// List of direct children to this memory entry
        /// </summary>
        public virtual ObservableCollectionExtended<TextureMapMemoryEntry> Children { get; } = new();

        #region FOR TEXTURE MAP BUILD
        /// <summary>
        /// The GUID of the external TFC for this texture
        /// </summary>
        public Guid TFCGuid { get; set; }
        /// <summary>
        /// Top non-emtpy mip offset. Used to compare without having to parse texture CRC
        /// </summary>
        public int TopMipOffset { get; set; }

        /// <summary>
        /// The name of the TFC the mips above the bottom 6 reside in
        /// </summary>
        public string TFCName { get; set; }
        #endregion

        /// <summary>
        /// If one of the instances in the children (or subchildren) of this node has unmatched CRCs in it's memory entry
        /// </summary>
        public bool HasUnmatchedCRCs { get; set; }

        /// <summary>
        /// Gets all children textures of this node
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TextureMapMemoryEntry> GetAllTextureEntries()
        {
            return Children.OfType<TextureMapMemoryEntry>().Where(x => !x.IsTexture).SelectMany(x => x.GetAllTextureEntries()).Concat(Children.OfType<TextureMapMemoryEntry>().Where(x => x.IsTexture));
        }

        public int GetExternalDiskSize()
        {
            return Instances.Any() ? Instances[0].ExternalStorageSize : 0;
        }

        public uint GetCRC()
        {
            if (HasUnmatchedCRCs)
            {
                Debug.WriteLine(@"Fetching CRC on texture that has mismatched CRCs across memory instances!");
                return 0;
            }
            return Instances.Any() ? Instances[0].CRC : int.MaxValue;
        }
    }

    /// <summary>
    /// Describes where a texture can be found for Texture Studio. This object describes a single instance of a texture, rather than a single 'texture' which can have multiple defined instances
    /// </summary>
    public class TextureMapPackageEntry
    {
        public TextureMapPackageEntry()
        {

        }

        /// <summary>
        /// Generate an entry from an export
        /// </summary>
        /// <param name="basePath"></param>
        /// <param name="exportEntry"></param>
        /// <param name="additionalTFCs"></param>
        /// <param name="crcCache"></param>
        public TextureMapPackageEntry(string basePath, ExportEntry exportEntry, List<string> additionalTFCs = null, Dictionary<string, uint> crcCache = null)
        {
            RelativePackagePath = exportEntry.FileRef.FilePath.Substring(basePath.Length).TrimStart('\\', '/');
            PackageName = Path.GetFileName(RelativePackagePath);
            ExportPath = exportEntry.InstancedFullPath;

            var tex2D = ObjectBinary.From<UTexture2D>(exportEntry);
            NumMips = tex2D.Mips.Count;
            NumEmptyMips = tex2D.Mips.Count(x => x.StorageType == StorageTypes.empty);

            var topMip = tex2D.Mips[NumEmptyMips]; // Skip the number of empty mips and use first non-empty
            if (NumMips > 0)
            {
                Width = (short)topMip.SizeX;
                Height = (short)topMip.SizeY;
            }

            HasExternalReferences = tex2D.Mips.Any(x => !x.IsLocallyStored);
            if (HasExternalReferences)
            {
                ExternalStorageSize = tex2D.Mips.Where(x => !x.IsLocallyStored).Sum(x => x.CompressedSize);
                foreach (var em in tex2D.Mips.Where(x => !x.IsLocallyStored && x.StorageType != StorageTypes.empty))
                {
                    CompressedMipInfos.Add(new MEMTextureMap.CompressedMipInfo() { Offset = em.DataOffset, CompressedSize = em.CompressedSize, UncompressedSize = em.UncompressedSize, StorageType = em.StorageType });
                }

            }

            var props = exportEntry.GetProperties();
            var format = props.GetProp<EnumProperty>(@"Format");
            if (format != null)
            {
                PixelFormat = Image.getPixelFormatType(format.Value);
            }

            var cache = props.GetProp<NameProperty>(@"TextureFileCacheName");
            if (cache != null)
            {
                TFCName = cache.Value.Name;
            }

            var guid = props.GetProp<StructProperty>(@"TFCFileGuid");
            if (guid != null)
            {
                TFCGuid = new FGuid(guid).ToGuid();
            }

            if (exportEntry.Game == MEGame.ME1)
            {
                IEntry pEntry = exportEntry;
                while (pEntry.Parent != null)
                {
                    pEntry = pEntry.Parent;
                }

                if (pEntry.ClassName == @"Package")
                {
                    MasterPackageName = pEntry.ObjectName;
                }
            }

            UIndex = exportEntry.UIndex; // This is so texture can be located in package by tooling

            // This needs some optimization once it's working
            var t2d = new Texture2D(exportEntry);
            bool canCache = t2d.GetTopMip().storageType != StorageTypes.empty && crcCache != null;
            if (canCache && crcCache.TryGetValue($"{TFCName}_{t2d.GetTopMip().externalOffset}", out uint crc))
            {
                CRC = crc;
            }
            try
            {
                CRC = Texture2D.GetTextureCRC(exportEntry, additionalTFCs);
                if (canCache)
                {
                    crcCache[$"{TFCName}_{t2d.GetTopMip().externalOffset}"] = CRC;
                }

            }
            catch (Exception)
            {
                // CRC could not be calculated

            }
        }

        public string TFCName { get; set; }

        public Guid TFCGuid { get; set; }

        /// <summary>
        /// The name of the package
        /// </summary>
        public string PackageName { get; set; }

        /// <summary>
        /// Relative path to the package
        /// </summary>
        public string RelativePackagePath { get; set; }

        /// <summary>
        /// UIndex of the export for the package
        /// </summary>
        public int UIndex { get; set; }

        /// <summary>
        /// Instanced full path of the export
        /// </summary>
        public string ExportPath { get; set; }

        /// <summary>
        /// The number of mips
        /// </summary>
        public int NumMips { get; set; }

        /// <summary>
        /// The number of empty mips
        /// </summary>
        public int NumEmptyMips { get; set; }

        /// <summary>
        /// Texture Width
        /// </summary>
        public short Width { get; set; }

        /// <summary>
        /// Texture Height
        /// </summary>
        public short Height { get; set; }

        /// <summary>
        /// Amount of bytes this texture takes up in a TFC, including all external mips
        /// </summary>
        public int ExternalStorageSize { get; set; }

        /// <summary>
        /// The CRC of the top mip for this instance
        /// </summary>
        public uint CRC { get; set; }

        /// <summary>
        /// The format of the texture
        /// </summary>
        public PixelFormat PixelFormat { get; set; }

        /// <summary>
        /// If this entry is for TextureMovie
        /// </summary>
        public bool IsMovieTexture { get; set; }

        /// <summary>
        /// ME1 Only: The name of the Master Package that contains the higher mips
        /// </summary>
        public string MasterPackageName { get; set; }

        /// <summary>
        /// If this texture references textures in another file or package
        /// </summary>
        public bool HasExternalReferences { get; set; }

        /// <summary>
        /// Information about compressed mips
        /// </summary>
        public List<MEMTextureMap.CompressedMipInfo> CompressedMipInfos = new(7);

        /// <summary>
        /// MEM Texture Map Stuff
        /// </summary>
        public uint ME1TextureOffset { get; set; }
        /// <summary>
        /// MEM Texture Map Stuff
        /// </summary>
        public bool ME1IsSlave { get; set; }
    }

    public class TextureMap
    {
        [JsonIgnore]
        public Dictionary<uint, MEMTextureMap.TextureMapEntry> VanillaMap { get; set; }
        public List<TextureMapMemoryEntry> CalculatedMap { get; set; }
        public MEGame Game { get; set; }

        // Todo: Have way to serialize map to and from disk, with info about filesizes to ensure map is in sync with disk state
    }

    public static class TextureMapGenerator
    {
        /// <summary>
        /// DO NOT CHANGE THIS
        /// This is the prefix for ME1 mod master texture packages. This naming scheme will let us identify texture masters.
        /// </summary>
        public static readonly string ME1_MOD_MASTER_TEXTURE_PACKAGE_PREFIX = "Textures_Master_";

        private static void SetUnmatchedCRC(TextureMapMemoryEntry memEntry, bool hasUnmatchedCRC)
        {
            memEntry.HasUnmatchedCRCs = hasUnmatchedCRC;
            TextureMapMemoryEntry parent = memEntry.Parent;
            while (parent != null)
            {
                parent.HasUnmatchedCRCs = hasUnmatchedCRC || parent.Children.Any(x => x.HasUnmatchedCRCs); // If one is corrected, another may exist under this tree.
                parent = parent.Parent;
            }
        }


        public static TextureMap GenerateMapForFolder(string rootDirectory,
            Func<IEntry, TextureMapMemoryEntry> nodeGeneratorDelegate,
            Action<TextureMapMemoryEntry> addRootElementDelegate,
            Action<string, int, int> progressDelegate = null,
            CancellationToken cts = default)
        {

            var rootNodes = new List<TextureMapMemoryEntry>();
            var game = MEGame.Unknown;

            void addRootItem(TextureMapMemoryEntry entry)
            {
                rootNodes.Add(entry);
                addRootElementDelegate?.Invoke(entry);
            }

            // Mapping of full paths to their entries
            progressDelegate?.Invoke(@"Calculating texture map", -1, -1);
            var entries = new Dictionary<string, TextureMapMemoryEntry>();
            var packageFiles = Directory.GetFiles(rootDirectory, "*.*", SearchOption.AllDirectories).Where(x => x.RepresentsPackageFilePath()).ToList();
            var tfcs = Directory.GetFiles(rootDirectory, "*.tfc", SearchOption.AllDirectories).ToList();
            progressDelegate?.Invoke(@"Calculating texture map", 0, packageFiles.Count);


            // Pass 1: Find all unique memory texture paths
            int numDone = 0;
            Dictionary<uint, MEMTextureMap.TextureMapEntry> vanillaMap = null;
            Dictionary<string, uint> crcCache = new();
            foreach (var p in packageFiles)
            {
                var filename = Path.GetFileName(p);
                progressDelegate?.Invoke($@"Scanning {filename}", numDone, packageFiles.Count);

                if (cts.IsCancellationRequested)
                    break;
                //using var package = MEPackageHandler.OpenMEPackage(p);
                using var package = MEPackageHandler.UnsafePartialLoad(p, x => !x.IsDefaultObject && x.IsTexture());

                if (game != MEGame.Unknown && game != package.Game)
                {
                    // This workspace has files from multiple games!
                    throw new Exception("A directory being scanned cannot have packages from different games in it");
                }

                if (vanillaMap is null)
                {
                    game = package.Game;
                    vanillaMap = MEMTextureMap.LoadTextureMap(game);
                }

                var textures = package.Exports.Where(x => x.IsDataLoaded());
                foreach (var t in textures)
                {
                    if (cts.IsCancellationRequested) break;
                    TextureMapMemoryEntry.ParseTexture(t, rootDirectory, entries, crcCache, tfcs, nodeGeneratorDelegate, addRootItem);
                }

                // Todo: Actually finish someday
                //if (filename.StartsWith(ME1_MOD_MASTER_TEXTURE_PACKAGE_PREFIX))
                //{
                //    ME1MasterTexturePackages.Add(p);
                //    Texture2D.AdditionalME1MasterTexturePackages.Add(p); // TODO: THIS NEEDS CLEANED UP AND MANAGED IN TEXTURE2D.CS
                //}

                numDone++;
                progressDelegate?.Invoke(null, numDone, packageFiles.Count);
            }

            // Pass 2: Find any unique items among the unique paths (e.g. CRC not equal to other members of same entry)
            var allTextures = rootNodes.SelectMany(x => x.GetAllTextureEntries());

            // Pass 3: Find items that have matching CRCs across memory entries
            var crcMap = new Dictionary<uint, List<TextureMapMemoryEntry>>();
            foreach (var t in allTextures)
            {
                if (t.Instances.Any())
                {
                    var firstCRC = t.Instances[0].CRC;

                    var areAllEqualCRC = t.Instances.All(x => x.CRC == firstCRC);
                    if (!areAllEqualCRC)
                    {
                        // Some textures are not the same across the same entry!
                        // This will lead to weird engine behavior as memory is dumped and newly loaded data is different
                        Debug.WriteLine($@"UNMATCHED CRCSs for texture {t.ObjectName}");
                        SetUnmatchedCRC(t, true);
                    }
                    else
                    {
                        if (!crcMap.TryGetValue(firstCRC, out var list))
                        {
                            list = new List<TextureMapMemoryEntry>();
                            crcMap[firstCRC] = list;
                        }

                        list.Add(t);
                    }
                }
            }

            return new TextureMap
            {
                VanillaMap = vanillaMap,
                CalculatedMap = rootNodes,
                Game = game,
            };

        }

        public static void RegenerateEntries(string selectedFolder, List<TextureMapMemoryEntry> entriesToRefresh,
            Dictionary<string, TextureMapMemoryEntry> textureMapMemoryEntries, Dictionary<string, uint> crcCache,
            List<string> additionalTFCs, Func<IEntry, TextureMapMemoryEntry> generatorDelegate, Action<TextureMapMemoryEntry> addRootNodeDelegate,
            CancellationToken cancelToken)
        {
            var pc = new PackageCache();
            foreach (var textureEntry in entriesToRefresh.SelectMany(x => x.GetAllTextureEntries()))
            {
                if (cancelToken.IsCancellationRequested)
                    return;
                foreach (var instance in textureEntry.Instances)
                {
                    if (cancelToken.IsCancellationRequested)
                        return;
                    var fullPath = Path.Combine(selectedFolder, instance.RelativePackagePath);
                    var package = pc.GetCachedPackage(fullPath);
                    TextureMapMemoryEntry.ParseTexture(package.FindExport(instance.ExportPath), selectedFolder, textureMapMemoryEntries, crcCache, additionalTFCs, generatorDelegate, addRootNodeDelegate);
                }
            }
        }
    }
}