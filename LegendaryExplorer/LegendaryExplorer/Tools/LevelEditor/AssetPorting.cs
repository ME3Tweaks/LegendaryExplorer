using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// For a commit, inside its <see cref="PackageEditScope"/> and before any reference is written: copies the other files' assets its actors use into the
/// actors' packages, and points the actors at the copies (<see cref="ActorProxy.UseCopiedAssets"/>). A failure leaves the rest to the scope's rollback.
/// <para>
/// An asset in a file the game always has loaded with the package is imported from it. Another is copied with what it uses, as
/// <see cref="EntryExporter"/> ports an export: imports of packages the game always has loaded stay imports, and the rest is cloned, since a level is
/// cooked seek-free. Where the package already has an object of the same class at a path the copy needs (the asset itself, or something it uses), that
/// object is used, as the relinker does and as the game would, which loads one object per path: each level has its own cooked copy of the assets it
/// shares with others, which differ in what cooking makes anew (materials' LightingGuids, for one), so what the package has is used even where its
/// content differs from the other file's. One of another class there fails the copy, which would otherwise be a second object at that path, except
/// what the relinker uses in its place: a material of another class where something the copy uses refers to one
/// </para>
/// </summary>
internal static class AssetPorting
{
    /// <summary>Copies in the assets <paramref name="actors"/> would write references to</summary>
    /// <param name="sources">
    /// The commit's own cache the other files are opened from disk in, as the asset database found them: never the editor's (another tool's copy may
    /// have unsaved edits), and porting can change its source (EntryExporter.PrepareGlobalFileForPorting)
    /// </param>
    /// <param name="inMemory">The package an asset of a file is copied from instead of that file, if any: a snapshot of a pasted copy's source's level as it
    /// is in memory (<see cref="ActorCloning.SourceSnapshots.OfFile"/>), which the copy itself is made from</param>
    /// <returns>Points the actors back at the other files' assets, for when the commit is abandoned</returns>
    public static Action CopyIn(IEnumerable<ActorProxy> actors, PackageCache sources, Func<string, IMEPackage> inMemory = null)
    {
        var copies = new Dictionary<(IMEPackage Package, AssetReference Asset), AssetReference>();
        var restores = new List<Action>();
        void Restore()
        {
            for (int i = restores.Count - 1; i >= 0; i--) restores[i]();
        }
        var committing = actors.ToList();
        try
        {
            foreach (var actor in committing)
            {
                var package = actor.Package;
                var assets = actor.AssetsToCopy.Where(a => a is { IsExternal: true }).Distinct().ToList();
                if (assets.Count == 0) continue;
                foreach (var asset in assets.Where(a => !copies.ContainsKey((package, a))))
                {
                    copies[(package, asset)] = AssetReference.To(CopyFor(asset, package, sources,
                        () => committing.Where(a => a.Package == package && a.AssetsToCopy.Contains(asset)), inMemory));
                }
                restores.Add(actor.UseCopiedAssets(a => a is { IsExternal: true } && copies.TryGetValue((package, a), out var copy) ? copy : a));
            }
        }
        catch
        {
            Restore();
            throw;
        }
        return Restore;
    }

    /// <summary>
    /// <see cref="Copy"/>, whose failure names the actors that use <paramref name="asset"/> (<paramref name="users"/>), so they can be given another
    /// asset or deleted
    /// </summary>
    internal static IEntry CopyFor(AssetReference asset, IMEPackage package, PackageCache sources, Func<IEnumerable<ActorProxy>> users,
        Func<string, IMEPackage> inMemory = null)
    {
        try
        {
            return Copy(asset, package, sources, inMemory);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            var names = users().Select(a => a.DisplayText).Distinct().ToList();
            if (names.Count == 0) throw;
            const int listed = 10;
            string list = string.Join(", ", names.Take(listed)) + (names.Count > listed ? $", and {names.Count - listed} more" : "");
            string them = names.Count == 1 ? "it" : "them";
            throw new InvalidOperationException($"{error.Message.TrimEnd()}\n\n{asset.SourcePath} is used by {list}. Choose another asset for {them}, " +
                $"or delete {them}.", error);
        }
    }

    /// <summary>The entry of <paramref name="package"/> that is <paramref name="asset"/>: one it has at its path that's the same object, or a new copy</summary>
    internal static IEntry Copy(AssetReference asset, IMEPackage package, PackageCache sources, Func<string, IMEPackage> inMemory = null)
    {
        string file = ActorCapture.FileName(asset.SourceFile), destination = ActorCapture.FileName(package.FilePath);
        var sourcePackage = inMemory?.Invoke(asset.SourceFile) ?? sources.GetCachedPackage(asset.SourceFile) ?? throw new InvalidOperationException($"{file} couldn't be opened to copy {asset.SourcePath} from.");
        if (sourcePackage.Game != package.Game)
            throw new InvalidOperationException($"{asset.SourcePath} is in {file}, a {sourcePackage.Game} file, so it can't be used in {destination}, a {package.Game} file.");
        if (sourcePackage.FindExport(asset.SourcePath) is not { } source)
            throw new InvalidOperationException($"{asset.SourcePath} isn't in {file} any more. Choose it again, or another asset.");

        // One of a file the game has loaded whenever it has this package loaded (Startup_INT's, say) is used from there, as the game's own levels use them:
        // an import, at the path it has in memory
        bool imported = EntryImporter.IsSafeToImportFrom(sourcePackage.FilePath, package);
        string path = imported ? source.MemoryFullPath : source.InstancedFullPath;
        if (package.FindEntry(path) is { } existing)
        {
            if (existing.ClassName != source.ClassName)
                throw new InvalidOperationException($"{destination} already has a {existing.ClassName} at {path}, where {file} has a {source.ClassName}. The game loads " +
                    $"one object per path, so it couldn't load both. Use {destination}'s own, or another asset.");
            return existing;
        }
        if (imported) return ImportOf(source, package);
        return Port(source, package, sources);
    }

    /// <summary>
    /// A copy of <paramref name="source"/> in <paramref name="package"/>, another package, with what it uses that the package doesn't have, as
    /// <see cref="EntryExporter"/> ports an export: at its own path, or under <paramref name="parent"/>, one of the package's entries
    /// </summary>
    internal static ExportEntry Port(ExportEntry source, IMEPackage package, PackageCache sources, IEntry parent = null)
    {
        string file = ActorCapture.FileName(source.FileRef.FilePath), destination = ActorCapture.FileName(package.FilePath);
        int exportsBefore = package.ExportCount, importsBefore = package.ImportCount;
        var issues = EntryExporter.ExportExportToPackage(source, package, out var copy, sources, targetParent: parent);
        if (issues.Count > 0)
            throw new InvalidOperationException($"{source.InstancedFullPath} couldn't be copied from {file}: " +
                string.Join("; ", issues.Take(5).Select(i => i.Message)) + (issues.Count > 5 ? $"; and {issues.Count - 5} more" : ""));
        if (copy is not ExportEntry copied)
            throw new InvalidOperationException($"{source.InstancedFullPath} couldn't be copied from {file}.");
        // The relinker uses what the package has at a path only if it's of the source's class (or both are materials): one of another class gets a second
        // object at its path, of which the game would load one. That's its path in the package, or in memory, where an export that isn't a forced export
        // is under its file's name (a root Scorch of D.pcc and a forced D.Scorch are both D.Scorch). (Only the entries it had with a new export's name
        // can be at its path)
        var added = package.Exports.Skip(exportsBefore).ToList();
        var names = added.Select(e => e.ObjectName).ToHashSet();
        var had = package.Exports.Take(exportsBefore).Cast<IEntry>().Concat(package.Imports.Take(importsBefore)).Where(e => names.Contains(e.ObjectName))
            .SelectMany(e => new[] { (Path: e.InstancedFullPath, Entry: e), (Path: e.MemoryFullPath, Entry: e) })
            .ToLookup(p => p.Path, p => p.Entry, StringComparer.OrdinalIgnoreCase);
        foreach (var made in added)
        {
            if ((had[made.InstancedFullPath].FirstOrDefault() ?? had[made.MemoryFullPath].FirstOrDefault()) is not { } existing) continue;
            throw new InvalidOperationException($"{source.InstancedFullPath} uses {made.InstancedFullPath}, a {made.ClassName}, but {destination} already has a " +
                $"{existing.ClassName} at {existing.InstancedFullPath}. The game loads one object per path, so it couldn't load both.");
        }
        return copied;
    }

    /// <summary>
    /// Before <paramref name="tree"/>, a copy's objects, is ported into <paramref name="package"/>: ports in what they import from a file the package can't
    /// import from, judged by the file each is found in (<see cref="EntryImporter.IsSafeToImportFrom(string, IMEPackage, bool)"/>), as EntryExporter and
    /// <see cref="Copy"/> judge it, not by its root package's name (an asset's root is a package in another file: BioVFX_Z_MATERIALS is Startup's): a class,
    /// an archetype or an asset of a file loaded with some levels and not others (LE3's BIO_COMMON). A class is an asset like any other, which a level has
    /// a copy of if it can't import it (a Pawn subclass is often a level's own).
    /// <see cref="EntryExporter"/> copies in only what's imported from a level or a global (BIOG, GUI) file. Each is copied at its own path, under its
    /// file's name (as another file's asset is: <see cref="Port"/>), which is the import's, so the copy's port uses it
    /// </summary>
    internal static void PortUnimportable(IEnumerable<ExportEntry> tree, IMEPackage package, PackageCache sources)
    {
        var imports = new HashSet<ImportEntry>();
        foreach (var export in tree)
        {
            var source = export.FileRef;
            int Found(int uIndex)
            {
                if (uIndex < 0 && source.GetEntry(uIndex) is ImportEntry import) imports.Add(import);
                return uIndex;
            }
            Found(export.Class?.UIndex ?? 0);
            Found(export.Archetype?.UIndex ?? 0);
            ForEachReference(export.GetProperties(), Found);
            ObjectBinary.From(export)?.ForEachUIndex(export.Game, new Renumber(Found));
        }
        foreach (var import in imports)
        {
            if (import.ClassName == "Package" || package.FindEntry(import.InstancedFullPath) is not null) continue;
            // One that can't be found stays the import its source has, which the game looks for as it does for its source (EntryExporter leaves LE2's
            // missing BioVFX_Z_TEXTURES.Generic.Glass_Shards_Norm so)
            if (EntryImporter.ResolveImport(import, sources) is not { } resolved || EntryImporter.IsSafeToImportFrom(resolved.FileRef.FilePath, package)) continue;
            Port(resolved, package, sources);
        }
    }

    /// <summary>
    /// <paramref name="source"/>, another package's entry, as an import of <paramref name="destination"/>, with its outers, at the path the game has it in
    /// memory (<see cref="IEntry.MemoryFullPath"/>): an export that isn't a forced export is under its file's package. Or the entry
    /// <paramref name="destination"/> already has there
    /// </summary>
    internal static IEntry ImportOf(IEntry source, IMEPackage destination)
    {
        IEntry parent = source.Parent is { } outer ? ImportOf(outer, destination)
            : source is ExportEntry { IsForcedExport: false } ? destination.GetEntryOrAddImport(source.FileRef.FileNameNoExtension.StripUnrealLocalization(), "Package") : null;
        string path = parent is null ? source.ObjectName.Instanced : $"{parent.InstancedFullPath}.{source.ObjectName.Instanced}";
        if (destination.FindEntry(path, source.ClassName) is { } existing) return existing;
        var import = source is ExportEntry export ? new ImportEntry(export, parent?.UIndex ?? 0, destination)
            : new ImportEntry(destination, parent, source.ObjectName) { ClassName = source.ClassName, PackageFile = ((ImportEntry)source).PackageFile };
        destination.AddImport(import);
        return import;
    }

    //every object reference in the properties, as the relinker finds them, replaced with what change gives for it. An array's entries that removed says
    //to are taken out of it first
    internal static void ForEachReference(PropertyCollection properties, Func<int, int> change, Func<int, bool> removed = null)
    {
        foreach (var property in properties)
        {
            switch (property)
            {
                case StructProperty structProperty:
                    ForEachReference(structProperty.Properties, change, removed);
                    break;
                case ArrayProperty<StructProperty> structs:
                    foreach (var element in structs) ForEachReference(element.Properties, change, removed);
                    break;
                case ArrayProperty<ObjectProperty> objects:
                    if (removed is not null) objects.Values.RemoveAll(o => removed(o.Value));
                    foreach (var element in objects) element.Value = change(element.Value);
                    break;
                case ObjectProperty objectProperty:
                    objectProperty.Value = change(objectProperty.Value);
                    break;
                case DelegateProperty delegateProperty:
                    delegateProperty.Value = new ScriptDelegate(change(delegateProperty.Value.ContainingObjectUIndex), delegateProperty.Value.FunctionName);
                    break;
            }
        }
    }

    internal readonly struct Renumber(Func<int, int> number) : IUIndexAction
    {
        public void Invoke(ref int uIndex, string propName) => uIndex = number(uIndex);
    }
}
