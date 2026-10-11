using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.Lightmaps.Codec;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Texture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;
using Texture2DMipInfo = LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// A texture file cache written by this tool: a GUID header followed by compressed mip blobs. A new file in the output
/// directory, or with <c>append</c> an existing mod TFC extended in place (its GUID kept, so textures already stored in it stay
/// valid). The game's own TFCs must never be opened for writing.
/// </summary>
public sealed class TfcWriter
{
    readonly HashSet<ExportEntry> writtenTextures = [];

    public string Name { get; }
    public Guid Guid { get; }
    public string FilePath { get; }

    public TfcWriter(string directory, string name, bool append = false, IEnumerable<IMEPackage> sourcePackages = null)
    {
        Name = name;
        FilePath = Path.Combine(directory, name + ".tfc");
        if (append && !File.Exists(FilePath) && sourcePackages != null) SeedFallbackCache(sourcePackages);
        if (append && File.Exists(FilePath) && new FileInfo(FilePath).Length >= 16)
        {
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read);
            var header = new byte[16];
            stream.ReadExactly(header);
            Guid = new Guid(header);
            return;
        }
        Guid = Guid.NewGuid();
        using var created = new FileStream(FilePath, FileMode.Create, FileAccess.Write);
        created.Write(Guid.ToByteArray());
    }

    // A new adjacent cache would take precedence over the parent's/game's existing cache. Seed it before
    // any writes, preserving every offset, including references in packages outside this bake.
    void SeedFallbackCache(IEnumerable<IMEPackage> packages)
    {
        string archive = Name + ".tfc";
        foreach (var package in packages)
        {
            string folder = Path.GetDirectoryName(package.FilePath);
            if (File.Exists(Path.Combine(folder, archive))) continue;
            bool usesCache = package.Exports.Where(e => e.IsTexture()
                    && e.GetProperty<NameProperty>("TextureFileCacheName") is { } cache
                    && cache.Value.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
                .Any(e => new Texture2D(e).Mips.Any(m => !m.IsPackageStored && m.storageType != StorageTypes.empty));
            if (!usesCache) continue;

            // Match Texture2D.GetTextureData's LE3 fallback order: CookedPCConsole parent, then game files.
            string source = null;
            if (folder.Contains(@"\CookedPCConsole") && !Path.GetFileName(folder).Equals("CookedPCConsole", StringComparison.OrdinalIgnoreCase))
            {
                string parent = folder;
                while (parent != null && !Path.GetFileName(parent).Equals("CookedPCConsole", StringComparison.OrdinalIgnoreCase))
                    parent = Path.GetDirectoryName(parent);
                if (parent != null && File.Exists(Path.Combine(parent, archive))) source = Path.Combine(parent, archive);
            }
            source ??= MELoadedFiles.GetAllGameFiles(null, package.Game, false, true)
                .FirstOrDefault(f => Path.GetFileName(f).Equals(archive, StringComparison.OrdinalIgnoreCase));
            if (source == null) throw new FileNotFoundException($"The source cache {archive} for {package.FileNameNoExtension} could not be found.");
            File.Copy(source, FilePath);
            return;
        }
    }

    /// <summary>Appends a blob and returns its offset.</summary>
    public int Append(byte[] data)
    {
        // Existing texture readers use FileShare.Read. Release write access between appends so
        // the next package/allocation can read the original cache during an in-place rebake.
        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Write);
        long offset = stream.Seek(0, SeekOrigin.End);
        if (offset + data.Length > int.MaxValue) throw new InvalidOperationException($"{Name}.tfc would exceed 2 GiB");
        stream.Write(data);
        return (int)offset;
    }

    internal void Written(ExportEntry texture) => writtenTextures.Add(texture);

    /// <summary>
    /// Move surviving references to this cache name into the output cache. Run after rewriting, while packages still
    /// resolve their original texture data beside their source files. Copy compressed bytes without another encode.
    /// </summary>
    public void PreserveUnwrittenTextures(IEnumerable<IMEPackage> packages)
    {
        foreach (var package in packages)
        {
            string localCache = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(package.FilePath), Name + ".tfc"));
            if (localCache.Equals(Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var export in package.Exports)
            {
                if (!export.IsTexture() || writtenTextures.Contains(export)
                    || export.GetProperty<NameProperty>("TextureFileCacheName") is not { } cache
                    || !cache.Value.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)) continue;
                var texture = new Texture2D(export);
                bool copied = false;
                foreach (var mip in texture.Mips)
                {
                    if (mip.storageType == StorageTypes.empty || ((int)mip.storageType & (int)StorageFlags.externalFile) == 0) continue;
                    byte[] data = Texture2D.GetTextureData(mip, package.Game, decompress: false);
                    mip.externalOffset = Append(data);
                    copied = true;
                }
                if (!copied) continue;
                var props = export.GetProperties();
                props.AddOrReplaceProp(Guid.ToGuidStructProp("TFCFileGuid"));
                props.RemoveNamedProperty("TFCFileGuidHiRes");
                LightmapTextureWriter.Serialize(export, texture, props);
            }
        }
    }
}

/// <summary>
/// Replaces a LightMapTexture2D export's mip chain with pre-encoded data, laid out like LE3's own lightmaps: every mip
/// except the last six is Oodle-compressed into a TFC (extOodle), the last six are stored uncompressed in the package
/// (pccUnc). Without a TFC every mip is package-stored (as BSP lightmaps are).
/// </summary>
public static class LightmapTextureWriter
{
    const int PackageStoredMips = 6;

    /// <param name="levels">Encoded data per level, top first: DXT1 blocks or BGRA bytes (MipChain.EncodeDxt1 / EncodeBgra).</param>
    /// <param name="sizeX">Top level size; lower levels are max(1, size >> i), as UE3 records them.</param>
    /// <param name="srgb">Serialized SRGB: false for LightMapVector_Dir; null leaves the property absent (default TRUE).</param>
    public static void Write(ExportEntry export, string pixelFormat, int sizeX, int sizeY, List<byte[]> levels, TfcWriter tfc, bool? srgb)
    {
        var texture = new Texture2D(export);
        var mips = new List<Texture2DMipInfo>();
        for (int i = 0; i < levels.Count; i++)
        {
            bool packageStored = tfc == null || i >= levels.Count - PackageStoredMips;
            var data = levels[i];
            var mip = new Texture2DMipInfo
            {
                Export = export,
                width = Math.Max(1, sizeX >> i),
                height = Math.Max(1, sizeY >> i),
                uncompressedSize = data.Length,
                TextureCacheName = packageStored ? null : tfc.Name,
            };
            if (packageStored)
            {
                mip.storageType = StorageTypes.pccUnc;
                mip.Mip = data;
                mip.compressedSize = data.Length;
            }
            else
            {
                var compressed = TextureCompression.CompressTexture(data, StorageTypes.extOodle);
                mip.storageType = StorageTypes.extOodle;
                mip.compressedSize = compressed.Length;
                mip.externalOffset = tfc.Append(compressed);
                mip.Mip = compressed;
            }
            mips.Add(mip);
        }
        texture.ReplaceMips(mips);

        var props = export.GetProperties();
        props.AddOrReplaceProp(new IntProperty(sizeX, "SizeX"));
        props.AddOrReplaceProp(new IntProperty(sizeY, "SizeY"));
        props.AddOrReplaceProp(new IntProperty(mips.Count - 1, "MipTailBaseIdx"));
        props.AddOrReplaceProp(new EnumProperty(pixelFormat, "EPixelFormat", export.Game, "Format"));
        // LE's TFCFileGuidHiRes has no known meaning for mod TFCs; LEX drops it when it builds textures.
        props.RemoveNamedProperty("TFCFileGuidHiRes");
        if (tfc != null && mips.Count > PackageStoredMips)
        {
            props.AddOrReplaceProp(tfc.Guid.ToGuidStructProp("TFCFileGuid"));
            props.AddOrReplaceProp(new NameProperty(tfc.Name, "TextureFileCacheName"));
            props.RemoveNamedProperty("NeverStream");
        }
        else
        {
            props.RemoveNamedProperty("TFCFileGuid");
            props.RemoveNamedProperty("TextureFileCacheName");
        }
        if (srgb is { } s) props.AddOrReplaceProp(new BoolProperty(s, "SRGB"));
        else props.RemoveNamedProperty("SRGB");

        Serialize(export, texture, props);
        tfc?.Written(export);
    }

    internal static void Serialize(ExportEntry export, Texture2D texture, PropertyCollection props)
    {
        using var ms = new MemoryStream();
        ms.Write(export.GetPrePropBinary());
        var writer = new EndianWriter(ms) { Endian = export.FileRef.Endian };
        props.WriteTo(writer, export.FileRef);
        writer.Flush();
        texture.SerializeNewData(ms);
        export.Data = ms.ToArray();
    }

    /// <summary>Nominal texture sizes for a chain of MipChain levels (lower DXT1 levels are stored as whole 4x4 blocks).</summary>
    public static List<byte[]> Encode(List<MipLevel> levels, string pixelFormat) =>
        pixelFormat == "PF_DXT1" ? MipChain.EncodeDxt1(levels) : MipChain.EncodeBgra(levels);
}
