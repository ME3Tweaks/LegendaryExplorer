using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
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
/// cooked seek-free. Where the package already has an object at a path the copy needs (the asset itself, or
/// something it uses), that object is used, as the relinker does, but only if it's the same as the source's (<see cref="SameObject"/>): the game loads
/// one object per path, so a different one would be drawn in its place
/// </para>
/// </summary>
internal static class AssetPorting
{
    /// <summary>Copies in the assets <paramref name="actors"/> would write references to</summary>
    /// <param name="sources">
    /// The commit's own cache the other files are opened from disk in, as the asset database found them: never the editor's (another tool's copy may
    /// have unsaved edits), and porting can change its source (EntryExporter.PrepareGlobalFileForPorting)
    /// </param>
    /// <returns>Points the actors back at the other files' assets, for when the commit is abandoned</returns>
    public static Action CopyIn(IEnumerable<ActorProxy> actors, PackageCache sources)
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
                        () => committing.Where(a => a.Package == package && a.AssetsToCopy.Contains(asset))));
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
    internal static IEntry CopyFor(AssetReference asset, IMEPackage package, PackageCache sources, Func<IEnumerable<ActorProxy>> users)
    {
        try
        {
            return Copy(asset, package, sources);
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
    internal static IEntry Copy(AssetReference asset, IMEPackage package, PackageCache sources)
    {
        string file = Path.GetFileName(asset.SourceFile), destination = Path.GetFileName(package.FilePath);
        var sourcePackage = sources.GetCachedPackage(asset.SourceFile) ?? throw new InvalidOperationException($"{file} couldn't be opened to copy {asset.SourcePath} from.");
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
            if (existing.ClassName != source.ClassName || existing is ExportEntry export && !SameObject(export, source))
                throw new InvalidOperationException($"{destination} already has a different {existing.ClassName} at {path}. The game loads one object " +
                    $"per path, so it would use that one instead of {file}'s. Use {destination}'s own, or another asset.");
            //and so must everything it uses
            if (existing is ExportEntry same) CheckUsed(same, _ => true, sourcePackage, file, destination);
            return existing;
        }
        if (imported) return ImportOf(source, package);

        int exportsBefore = package.ExportCount;
        var issues = EntryExporter.ExportExportToPackage(source, package, out var copy, sources);
        if (issues.Count > 0)
            throw new InvalidOperationException($"{asset.SourcePath} couldn't be copied from {file}: " +
                string.Join("; ", issues.Take(5).Select(i => i.Message)) + (issues.Count > 5 ? $"; and {issues.Count - 5} more" : ""));
        if (copy is not ExportEntry copied) throw new InvalidOperationException($"{asset.SourcePath} couldn't be copied from {file}.");
        //what the relinker used of the package's own in place of the source's
        CheckUsed(copied, used => used.UIndex <= exportsBefore, sourcePackage, file, destination);
        return copied;
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

    /// <summary>
    /// Checks that what <paramref name="asset"/> uses, at any depth, is the same as the source's at each path, where it's an object the package already
    /// had (<paramref name="hadIt"/>), which the relinker used in place of the source's (a copy's own new objects are the source's, copied)
    /// </summary>
    private static void CheckUsed(ExportEntry asset, Func<ExportEntry, bool> hadIt, IMEPackage source, string file, string destination)
    {
        var package = asset.FileRef;
        var visited = new HashSet<ExportEntry> { asset };
        var pending = new Stack<ExportEntry>([asset]);
        while (pending.TryPop(out var export))
        {
            foreach (int uIndex in References(export))
            {
                if (package.GetEntry(uIndex) is not ExportEntry used || !visited.Add(used)) continue;
                if (hadIt(used) && source.FindExport(used.InstancedFullPath) is { } original && !SameObject(used, original))
                {
                    throw new InvalidOperationException($"{asset.InstancedFullPath} uses {used.InstancedFullPath}, which {destination} already has, but different from " +
                        $"{file}'s. The game loads one object per path, so {destination}'s would be used.");
                }
                pending.Push(used);
            }
        }
    }

    /// <summary>
    /// Whether two exports, each of its own package, are the same object: of the same class, with the same properties and binary, where references are
    /// to objects at the same paths and names are the same names
    /// </summary>
    internal static bool SameObject(ExportEntry a, ExportEntry b)
    {
        if (a.ClassName != b.ClassName || a.Game != b.Game) return false;
        // Both are written into one package, with each referenced object's path given the same number, so that equal objects write equal bytes
        var paths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var common = MEPackageHandler.CreateMemoryEmptyPackage("Comparison.pcc", a.Game);
        return Canonical(a, paths, common).AsSpan().SequenceEqual(Canonical(b, paths, common));
    }

    private static byte[] Canonical(ExportEntry export, Dictionary<string, int> paths, IMEPackage common)
    {
        int Number(int uIndex)
        {
            if (uIndex == 0) return 0;
            if (export.FileRef.GetEntry(uIndex) is not { } entry) return int.MinValue;
            string path = entry.InstancedFullPath;
            if (!paths.TryGetValue(path, out int number)) paths[path] = number = paths.Count + 1;
            return number;
        }
        var properties = export.IsClass ? null : export.GetProperties().DeepClone();
        if (properties is not null) ForEachReference(properties, Number);
        var binary = ObjectBinary.From(export);
        binary?.ForEachUIndex(export.Game, new Renumber(Number));
        var writer = new EndianWriter { Endian = common.Endian };
        properties?.WriteTo(writer, common);
        //a class LEC can't read the binary of: what's after its properties, as it is (so a copy of one that has references in it isn't the same)
        if (binary is null) writer.Write(export.GetBinaryData());
        else binary.WriteTo(writer, common);
        return writer.ToArray();
    }

    /// <summary>The UIndexes an export's properties and binary refer to</summary>
    private static List<int> References(ExportEntry export)
    {
        var references = new List<int>();
        if (!export.IsClass) ForEachReference(export.GetProperties(), uIndex => { references.Add(uIndex); return uIndex; });
        if (ObjectBinary.From(export) is { } binary) references.AddRange(binary.GetUIndexes(export.Game));
        return references;
    }

    //every object reference in the properties, as the relinker finds them, replaced with what change gives for it
    private static void ForEachReference(PropertyCollection properties, Func<int, int> change)
    {
        foreach (var property in properties)
        {
            switch (property)
            {
                case StructProperty structProperty:
                    ForEachReference(structProperty.Properties, change);
                    break;
                case ArrayProperty<StructProperty> structs:
                    foreach (var element in structs) ForEachReference(element.Properties, change);
                    break;
                case ArrayProperty<ObjectProperty> objects:
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

    private readonly struct Renumber(Func<int, int> number) : IUIndexAction
    {
        public void Invoke(ref int uIndex, string propName) => uIndex = number(uIndex);
    }
}
