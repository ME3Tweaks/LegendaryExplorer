using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Metadata = LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler.TextureCompilationMetadata;
using Dimension = LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler.TextureDimension;

namespace LegendaryExplorerCore.Shaders;

/// <summary>
/// Read-only LE3 package session that reads texture properties without loading texture export
/// payloads. Successful metadata is cached per export for this session. Caller resolves imports
/// to the appropriate donor session/export. No automatic package discovery or persistent cache.
/// Not thread-safe; do not edit/save the exposed header-only package or share its source stream.
/// </summary>
public sealed class LE3TextureMetadataReader : IDisposable
{
    private readonly Stream source;
    private readonly bool leaveOpen;
    private ILazyLoadPackage package;
    private readonly Dictionary<int, Metadata> cache = new();
    public ILazyLoadPackage Package { get { ThrowIfDisposed(); return package; } }

    /// <summary>
    /// Opens package tables and retains the supplied stream for lazy property reads. The source
    /// must start at position zero. Compressed package blocks may contain some adjacent binary
    /// data; this avoids export-sized buffers, not all compression-block I/O/decompression.
    /// </summary>
    public LE3TextureMetadataReader(Stream source, string packagePath = null, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || !source.CanSeek || source.Position != 0)
            throw new ArgumentException("A readable, seekable package stream positioned at zero is required.", nameof(source));
        this.source = source;
        this.leaveOpen = leaveOpen;
        try
        {
            package = MEPackageHandler.UnsafeLazyLoadFromStream(source, packagePath);
            if (!package.Game.IsLEGame3() || package.Platform != MEPackage.GamePlatform.PC)
                throw new NotSupportedException("Texture metadata reading is currently limited to LE3 PC packages.");
        }
        catch
        {
            package?.Dispose();
            package = null;
            if (!leaveOpen) source.Dispose();
            throw;
        }
    }

    public static LE3TextureMetadataReader Open(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new LE3TextureMetadataReader(stream, packagePath, leaveOpen: false);
    }

    public Metadata Read(int exportUIndex)
    {
        ThrowIfDisposed();
        if (!package.TryGetUExport(exportUIndex, out var export))
            throw new ArgumentException("The UIndex must identify a texture export.", nameof(exportUIndex));
        return Read(export);
    }

    public Metadata Read(string instancedFullPath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(instancedFullPath);
        var export = package.FindExport(instancedFullPath)
            ?? throw new KeyNotFoundException($"Texture export not found: {instancedFullPath}");
        return Read(export);
    }

    public Metadata Read(ExportEntry export)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(export);
        if (!ReferenceEquals(export.FileRef, package) || !package.TryGetUExport(export.UIndex, out var owned) || !ReferenceEquals(owned, export))
            throw new ArgumentException("The texture export must belong to this reader's package.", nameof(export));
        if (!export.IsA("Texture2D") && !export.IsA("TextureCube"))
            throw new ArgumentException("A Texture2D or TextureCube export is required.", nameof(export));
        if (cache.TryGetValue(export.UIndex, out var metadata)) return metadata;

        using var data = package.OpenExportDataStream(export);
        data.Position = export.GetPropertyStart(); // LE3 texture pre-properties are header-derived.
        var properties = PropertyCollection.ReadProps(export, data, export.ClassName, includeNoneProperty: true,
            entry: export.IsDefaultObject ? export.Class : export);
        // ReadProps can recover partial properties on malformed input. Never turn that into
        // seemingly valid default metadata; require a complete top-level terminator.
        if (properties.Count == 0 || properties[^1] is not NoneProperty terminator ||
            terminator.StartOffset + 8 != properties.EndOffset || properties.EndOffset > data.Length)
            throw new InvalidDataException($"Texture properties are malformed or unterminated: {export.InstancedFullPath}");
        foreach (var property in properties)
        {
            if (property.Name.Name == "CompressionSettings" && property is not EnumProperty ||
                property.Name.Name is "UnpackMin" or "UnpackMax" && property is not FloatProperty)
                throw new InvalidDataException($"Unexpected texture metadata property type: {export.InstancedFullPath}.{property.Name}");
        }
        metadata = Metadata.FromProperties(export.IsA("TextureCube") ? Dimension.TextureCube : Dimension.Texture2D, properties);
        cache.Add(export.UIndex, metadata);
        return metadata;
    }

    public void Dispose()
    {
        if (package == null) return;
        var ownedPackage = package;
        package = null;
        cache.Clear();
        try { ownedPackage.Dispose(); }
        finally { if (!leaveOpen) source.Dispose(); }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(package == null, this);
}
