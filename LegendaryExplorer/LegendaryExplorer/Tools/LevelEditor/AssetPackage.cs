using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// An asset chosen from another file (Asset Picker), copied with what it uses into a package of its own, which the editor previews it from and a commit
/// copies it from (<see cref="AssetPorting.Copy"/>): its file is read once, when it's chosen, and can then be changed or deleted. The package is one of
/// <see cref="TemporaryPackages"/>, made from the file, whose imports are found as the file's are. Its asset is at the path the game has it at in memory, as
/// it is in its file, so a level it's copied into gets it at that path
/// </summary>
public sealed class AssetPackage : IDisposable
{
    private readonly IActorEditorContext editor;

    /// <summary>The package</summary>
    public IMEPackage Package { get; }
    /// <summary>What was chosen: the asset in its own file</summary>
    public AssetReference Chosen { get; }
    /// <summary>The copy, in <see cref="Package"/>, which picks of <see cref="Chosen"/> refer to</summary>
    public AssetReference Asset { get; }

    private AssetPackage(IActorEditorContext editor, IMEPackage package, AssetReference chosen, AssetReference asset)
    {
        this.editor = editor;
        Package = package;
        Chosen = chosen;
        Asset = asset;
    }

    /// <summary>
    /// Whether <paramref name="chosen"/> is copied into a package of its own for <paramref name="destination"/>: it's another file's asset, and not one of a
    /// file the game always has loaded with it, which a commit imports from that file (<see cref="AssetPorting.Copy"/>)
    /// </summary>
    public static bool IsNeeded(AssetReference chosen, IMEPackage destination) =>
        chosen is { IsExternal: true } && !EntryImporter.IsSafeToImportFrom(chosen.SourceFile, destination);

    /// <summary>
    /// Copies <paramref name="chosen"/>, another file's asset, from its file on disk, as a commit would copy it, and has the editor's previews find the copy.
    /// If it can't be copied, the error says why, and nothing is left of the copy
    /// </summary>
    public static AssetPackage Make(IActorEditorContext editor, AssetReference chosen)
    {
        if (chosen is not { IsExternal: true }) throw new ArgumentException("Only another file's asset is copied into a package of its own.", nameof(chosen));
        string file = Path.GetFileName(chosen.SourceFile);
        // Its own cache, as a commit's: never another tool's copy of the file, which may have unsaved edits, and porting can change its source
        // (EntryExporter.PrepareGlobalFileForPorting)
        using var sources = new PackageCache();
        var source = sources.GetCachedPackage(chosen.SourceFile) ?? throw new InvalidOperationException($"{file} couldn't be opened to copy {chosen.SourcePath} from.");
        if (source.FindExport(chosen.SourcePath) is not { } export)
            throw new InvalidOperationException($"{chosen.SourcePath} isn't in {file} any more. Choose it again, or another asset.");
        // (its path in memory, before porting can move it)
        string memoryPath = export.MemoryFullPath;
        var package = TemporaryPackages.Create(source, file);
        try
        {
            var copy = AssetPorting.Port(export, package, sources);
            if (!string.Equals(copy.MemoryFullPath, memoryPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{chosen.SourcePath} couldn't be copied from {file} at the path the game has it at, {memoryPath}: it was copied to {copy.MemoryFullPath}.");
            // Its previews find it by its path. Until the previews' packages are let go of, when the last open level closes, which releases it
            editor.RenderContext.PackageCache.UsePackage(package);
            return new AssetPackage(editor, package, chosen, AssetReference.InFile(package.FilePath, copy.InstancedFullPath));
        }
        catch
        {
            TemporaryPackages.Release(package);
            throw;
        }
    }

    /// <summary>
    /// The other files' assets that <paramref name="files"/>' actors (drafts, undone ones that redo would bring back, and pending edits) and
    /// <paramref name="history"/>'s steps refer to, which a chosen asset's package is kept for
    /// </summary>
    public static IEnumerable<AssetReference> ReferencesIn(IEnumerable<OpenLevelFile> files, UndoHistory history) =>
        files.SelectMany(f => f.Actors.Concat(f.RetainedActors)).Where(a => !a.IsDisposed).SelectMany(a => a.AssetsInUse)
            .Concat(history.Steps.SelectMany(s => s.Assets)).Where(r => r is { IsExternal: true });

    /// <summary>Whether any of <paramref name="references"/> is to its copy</summary>
    public bool IsUsedBy(IEnumerable<AssetReference> references) =>
        references.Any(r => string.Equals(r?.SourceFile, Package.FilePath, StringComparison.OrdinalIgnoreCase));

    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var context = editor.RenderContext;
        context.PackageCache.ReleasePackage(Package.FilePath);
        context.ForgetPackage(Package);
        TemporaryPackages.Release(Package);
    }
}

/// <summary>
/// An editor's chosen assets' packages (<see cref="AssetPackage"/>): what picks are kept as, each asset's made once and kept until nothing refers to it
/// </summary>
public sealed class AssetPackages(IActorEditorContext editor)
{
    private readonly List<AssetPackage> packages = [];

    /// <summary>
    /// What <paramref name="chosen"/>, picked for <paramref name="destination"/>, is kept as: another file's asset that the destination can't import, as
    /// its copy in a package of its own, made the first time it's picked (whatever it's for) and used again until nothing refers to it. Otherwise itself
    /// </summary>
    public AssetReference Keep(AssetReference chosen, IMEPackage destination)
    {
        if (!AssetPackage.IsNeeded(chosen, destination)) return chosen;
        if (packages.FirstOrDefault(p => p.Chosen == chosen) is { } kept) return kept.Asset;
        var package = AssetPackage.Make(editor, chosen);
        packages.Add(package);
        return package.Asset;
    }

    /// <summary>Disposes the packages that none of <paramref name="references"/> is to (<see cref="AssetPackage.ReferencesIn"/>)</summary>
    public void ReleaseUnused(IEnumerable<AssetReference> references)
    {
        if (packages.Count == 0) return;
        var used = references.ToList();
        foreach (var package in packages.Where(p => !p.IsUsedBy(used)).ToList())
        {
            packages.Remove(package);
            package.Dispose();
        }
    }

    /// <summary>Disposes them all</summary>
    public void ReleaseAll()
    {
        foreach (var package in packages) package.Dispose();
        packages.Clear();
    }
}
