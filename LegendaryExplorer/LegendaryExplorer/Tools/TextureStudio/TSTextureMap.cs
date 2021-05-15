using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassEffectModder.Images;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Tools.TextureStudio
{
    /// <summary>
    /// WPF extension for TextureMapMemoryEntry
    /// </summary>
    public class TextureMapMemoryEntryWPF : TextureMapMemoryEntry
    {
        public TextureMapMemoryEntryWPF(IEntry entry) : base(entry) { }

        public bool IsProgramaticallySelecting;

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        private bool isExpanded = true; 
        public bool IsExpanded
        {
            get => this.isExpanded;
            set => SetProperty(ref isExpanded, value);
        }

        public string IconSource => IsPackage ? @"/PackageEditor/EntryIcons/icon_package.png" : @"/PackageEditor/EntryIcons/icon_texture2d.png";

        public void ExpandParents()
        {
            if (Parent is TextureMapMemoryEntryWPF tmmew)
            {
                tmmew.ExpandParents();
                tmmew.IsExpanded = true;
            }
        }

        
        public override ObservableCollectionExtended<TextureMapPackageEntry> Instances { get; } = new ObservableCollectionExtendedWPF<TextureMapPackageEntry>();

        /// <summary>
        /// List of direct children to this memory entry
        /// </summary>
        public override ObservableCollectionExtended<TextureMapMemoryEntry> Children { get; } = new ObservableCollectionExtendedWPF<TextureMapMemoryEntry>();

        public IEnumerable<TextureMapMemoryEntryWPF> GetAllTextureEntries()
        {
            return Children.OfType<TextureMapMemoryEntryWPF>().Where(x=>x.IsPackage).SelectMany(x=>x.GetAllTextureEntries()).Concat(Children.OfType<TextureMapMemoryEntryWPF>().Where(x => !x.IsPackage));
        }
    }

    /// <summary>
    /// Describes a memory-unique texture, e.g. a unique full path.
    /// </summary>
    public class TextureMapMemoryEntry : NotifyPropertyChangedBase
    {
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
                Width = (short) tex2D.Mips[0].SizeX;
                Height = (short) tex2D.Mips[0].SizeY;
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
}
