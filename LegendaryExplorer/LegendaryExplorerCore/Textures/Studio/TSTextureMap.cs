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
using PropertyChanged;

namespace LegendaryExplorerCore.Textures.Studio
{

    /// <summary>
    /// Describes a memory-unique texture, e.g. a unique full path.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class TextureMapMemoryEntry
    {
        /// <summary>
        /// Parses a Texture object
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <param name="textureMapMemoryEntries"></param>
        /// <returns></returns>
        public static TextureMapMemoryEntry ParseTexture(ExportEntry exportEntry, string selectedFolder, Dictionary<string, TextureMapMemoryEntry> textureMapMemoryEntries,
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
            memoryEntry.Instances.Add(new TextureMapPackageEntry(selectedFolder, exportEntry, additionalTFCs));
            return memoryEntry;
        }

        /// <summary>
        /// Creates all parents of the specified export in the texture tree, if necessary
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <param name="textureMapMemoryEntries"></param>
        /// <returns></returns>
        private static TextureMapMemoryEntry EnsureParent(ExportEntry exportEntry, string selectedFolder, Dictionary<string, TextureMapMemoryEntry> textureMapMemoryEntries, List<string> additionalTFCs, Func<IEntry, TextureMapMemoryEntry> generatorDelegate, Action<TextureMapMemoryEntry> addRootNodeDelegate)
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
                        lastParent = ParseTexture(pe, selectedFolder, textureMapMemoryEntries, additionalTFCs, generatorDelegate, addRootNodeDelegate);
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
            IsPackage = iEntry.ClassName == @"Package";
            ObjectName = iEntry.ObjectName;
        }

        /// <summary>
        /// If this entry represents a 'package' and is not actually a texture.
        /// </summary>
        public bool IsPackage { get; set; }

        /// <summary>
        /// The parent entry, most times a package.
        /// </summary>
        public TextureMapMemoryEntry Parent { get; set; }

        /// <summary>
        /// The object name.
        /// </summary>
        public string ObjectName { get; set; }

        /// <summary>
        /// The instances of this entry.
        /// </summary>
        public virtual ObservableCollectionExtended<TextureMapPackageEntry> Instances { get; } = new ObservableCollectionExtended<TextureMapPackageEntry>();

        /// <summary>
        /// List of direct children to this memory entry
        /// </summary>
        public virtual ObservableCollectionExtended<TextureMapMemoryEntry> Children { get; } = new ObservableCollectionExtended<TextureMapMemoryEntry>();

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
            return Children.OfType<TextureMapMemoryEntry>().Where(x => x.IsPackage).SelectMany(x => x.GetAllTextureEntries()).Concat(Children.OfType<TextureMapMemoryEntry>().Where(x => !x.IsPackage));
        }
    }

    /// <summary>
    /// Describes where a texture can be found for Texture Studio. This object describes a single instance of a texture, rather than a single 'texture' which can have multiple defined instances
    /// </summary>
    public class TextureMapPackageEntry
    {
        public TextureMapPackageEntry(string basePath, ExportEntry exportEntry, List<string> additionalTFCs = null)
        {
            RelativePackagePath = exportEntry.FileRef.FilePath.Substring(basePath.Length).TrimStart('\\', '/');
            PackageName = Path.GetFileName(RelativePackagePath);
            UIndex = exportEntry.UIndex;

            var tex2D = ObjectBinary.From<UTexture2D>(exportEntry);
            NumMips = tex2D.Mips.Count;
            if (NumMips > 0)
            {
                Width = (short)tex2D.Mips[0].SizeX;
                Height = (short)tex2D.Mips[0].SizeY;
            }

            NumEmptyMips = tex2D.Mips.Count(x => x.StorageType == StorageTypes.empty);
            HasExternalReferences = tex2D.Mips.Any(x => !x.IsLocallyStored);
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

            try
            {
                CRC = Texture2D.GetTextureCRC(exportEntry, additionalTFCs);
            }
            catch (Exception e)
            {
                // CRC could not be calculated
            }
        }

        public string TFCName { get; set; }

        /// <summary>
        /// The name of the package
        /// </summary>
        public string PackageName { get; set; }

        /// <summary>
        /// Relative path to the package
        /// </summary>
        public string RelativePackagePath { get; set; }

        /// <summary>
        /// In-package UIndex
        /// </summary>
        public int UIndex { get; set; }

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
        /// The CRC of the top mip for this instance
        /// </summary>
        public uint CRC { get; set; }

        /// <summary>
        /// The format of the texture
        /// </summary>
        public PixelFormat PixelFormat { get; set; }

        /// <summary>
        /// The full name of the texture instance
        /// </summary>
        public string FullName { get; set; }

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
    }

    public class TextureMap
    {
        public Dictionary<uint, MEMTextureMap.TextureMapEntry> VanillaMap { get; set; }
        public List<TextureMapMemoryEntry> CalculatedMap { get; set; }
    }

    public class TextureMapGenerator
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


        public static TextureMap GenerateMapForFolder(string rootDirectory, MEGame game, 
            Func<IEntry, TextureMapMemoryEntry> nodeGeneratorDelegate,
            Action<TextureMapMemoryEntry> addRootElementDelegate,
            Action<string, int, int> progressDelegate = null,
            CancellationToken cts = default)
        {
            // Mapping of full paths to their entries
            progressDelegate?.Invoke(@"Calculating texture map", -1, -1);
            Dictionary<string, TextureMapMemoryEntry> entries = new Dictionary<string, TextureMapMemoryEntry>();
            var packageFiles = Directory.GetFiles(rootDirectory, "*.*", SearchOption.AllDirectories).Where(x => x.RepresentsPackageFilePath()).ToList();
            var tfcs = Directory.GetFiles(rootDirectory, "*.tfc", SearchOption.AllDirectories).ToList();
            progressDelegate?.Invoke(@"Calculating texture map", 0, packageFiles.Count);


            var allNodes = new List<TextureMapMemoryEntry>();
            // Pass 1: Find all unique memory texture paths
            int numDone = 0;
            Dictionary<uint, MEMTextureMap.TextureMapEntry> vanillaMap = null;
            foreach (var p in packageFiles)
            {
                var filename = Path.GetFileName(p);
                progressDelegate?.Invoke($@"Scanning {filename}", numDone, packageFiles.Count);

                if (cts.IsCancellationRequested) break;
                using var package = MEPackageHandler.OpenMEPackage(p);

                if (game != MEGame.Unknown && game != package.Game)
                {
                    // This workspace has files from multiple games!
                    throw new Exception("A directory being scanned cannot have packages from different games in it");
                }
                else
                {
                    game = package.Game;
                    vanillaMap = MEMTextureMap.LoadTextureMap(game);
                }

                var textures = package.Exports.Where(x => x.IsTexture());
                foreach (var t in textures)
                {
                    if (cts.IsCancellationRequested) break;
                    TextureMapMemoryEntry.ParseTexture(t, rootDirectory, entries, tfcs, nodeGeneratorDelegate, addRootElementDelegate);
                }

                //if (filename.StartsWith(ME1_MOD_MASTER_TEXTURE_PACKAGE_PREFIX))
                //{
                //    ME1MasterTexturePackages.Add(p);
                //    Texture2D.AdditionalME1MasterTexturePackages.Add(p); // TODO: THIS NEEDS CLEANED UP AND MANAGED IN TEXTURE2D.CS
                //}

                numDone++;
            }

            // Pass 2: Find any unique items among the unique paths (e.g. CRC not equal to other members of same entry)
            var allTextures = allNodes.OfType<TextureMapMemoryEntry>().SelectMany(x => x.GetAllTextureEntries());

            // Pass 3: Find items that have matching CRCs across memory entries
            Dictionary<uint, List<TextureMapMemoryEntry>> crcMap = new Dictionary<uint, List<TextureMapMemoryEntry>>();
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
                        Debug.WriteLine(@"UNMATCHED CRCSSSSSSSSSSSSSSSSSSSSSSSSSSS");
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

            return new TextureMap()
            {
                VanillaMap = vanillaMap,
                CalculatedMap = allNodes
            };

        }
    }
}