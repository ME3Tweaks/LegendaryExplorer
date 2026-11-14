using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// An asset a draft or a pending edit uses: an entry of the package it's for (an export or an import), or an export of another file. Another file's is
/// referred to by that file and the export's instanced full path, never by an object or index of the file, so it doesn't hold the package cache's copy
/// of it, and stays right if the file is reopened. Previews open it through their package cache; nothing is added to the destination until a commit
/// </summary>
public sealed record AssetReference
{
    /// <summary>The destination package's own entry, or null for another file's export</summary>
    public IEntry Entry { get; }
    /// <summary>For another file's export: the file</summary>
    public string SourceFile { get; }
    /// <summary>For another file's export: its instanced full path</summary>
    public string SourcePath { get; }

    private AssetReference(IEntry entry, string sourceFile, string sourcePath)
    {
        Entry = entry;
        SourceFile = sourceFile;
        SourcePath = sourcePath;
    }

    /// <summary>A reference to an entry of the package it's for, or null for none</summary>
    public static AssetReference To(IEntry entry) => entry is null ? null : new(entry, null, null);

    /// <summary>A reference to the export at <paramref name="instancedFullPath"/> in the file <paramref name="filePath"/>, another file than the destination</summary>
    public static AssetReference InFile(string filePath, string instancedFullPath) =>
        new(null, filePath ?? throw new ArgumentNullException(nameof(filePath)), instancedFullPath ?? throw new ArgumentNullException(nameof(instancedFullPath)));

    /// <summary>
    /// A reference to <paramref name="entry"/>, as <paramref name="package"/> has or inherits it: its own entry, or another package's (an archetype's), which
    /// is referred to by the export it resolves to, or, if it doesn't resolve, by its own file and path. Null for none
    /// </summary>
    public static AssetReference Of(IEntry entry, IMEPackage package, PackageCache cache)
    {
        if (entry is null) return null;
        if (entry.FileRef == package) return To(entry);
        var export = EffectiveLightingProperties.Resolve(entry, cache) ?? entry;
        if (export.FileRef.FilePath is null)
            throw new InvalidOperationException($"{export.InstancedFullPath} is in a package that wasn't opened from a file, so it can't be referred to from {Path.GetFileName(package.FilePath)}.");
        return InFile(export.FileRef.FilePath, export.InstancedFullPath);
    }

    /// <summary>
    /// This asset as <paramref name="package"/> refers to it: as its own entry, or another file's (<see cref="Of"/>). An export of the package's own file is
    /// the package's entry at its path, as the package has it in memory, if it has one
    /// </summary>
    public AssetReference In(IMEPackage package, PackageCache cache)
    {
        if (Entry is not null) return Entry.FileRef == package ? this : Of(Entry, package, cache);
        return string.Equals(SourceFile, package.FilePath, StringComparison.OrdinalIgnoreCase) && package.FindEntry(SourcePath) is { } own ? To(own) : this;
    }

    //a file's path and an object's path are each the same whatever their case
    public bool Equals(AssetReference other) => other is not null && EqualityComparer<IEntry>.Default.Equals(Entry, other.Entry)
        && string.Equals(SourceFile, other.SourceFile, StringComparison.OrdinalIgnoreCase) && string.Equals(SourcePath, other.SourcePath, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => HashCode.Combine(Entry, SourceFile is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(SourceFile),
        SourcePath is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(SourcePath));

    /// <summary>Whether it's another file's export, which a commit would have to copy into the destination</summary>
    public bool IsExternal => Entry is null;

    public string InstancedFullPath => Entry?.InstancedFullPath ?? SourcePath;

    /// <summary>The object's own name, without its outers</summary>
    public string Name => Entry?.ObjectName.Instanced ?? SourcePath[(SourcePath.LastIndexOf('.') + 1)..];

    /// <summary>The path, and for another file's, which file</summary>
    public string Description => Entry is not null ? InstancedFullPath : $"{SourcePath} (from {Path.GetFileName(SourceFile)})";

    /// <summary>
    /// What it refers to: the destination's entry, or the export of the other file, opened through <paramref name="cache"/>. Null if the file can't be
    /// opened or no longer has it
    /// </summary>
    public IEntry Find(PackageCache cache) => Entry ?? cache.GetCachedPackage(SourceFile)?.FindExport(SourcePath);

    /// <summary>
    /// For a commit writing a reference to it into <paramref name="package"/>: its entry there. Another file's export must have been copied in, and the
    /// reference replaced with the copy, first (<see cref="AssetPorting"/>)
    /// </summary>
    public IEntry EntryIn(IMEPackage package)
    {
        if (Entry is null)
            throw new InvalidOperationException($"{SourcePath}, from {Path.GetFileName(SourceFile)}, wasn't copied into {Path.GetFileName(package.FilePath)}.");
        if (Entry.FileRef != package) throw new InvalidOperationException($"{Entry.InstancedFullPath} is an entry of another package than {Path.GetFileName(package.FilePath)}.");
        return Entry;
    }

    public override string ToString() => Description;
}

/// <summary>
/// A mesh preview's mesh and material overrides (by section MaterialIndex; null for none), each its package's entry or another file's export: a draft's,
/// or a pending edit's, drawn before a commit writes them
/// </summary>
public sealed record PreviewAssets(AssetReference Mesh, IReadOnlyList<AssetReference> Materials);
