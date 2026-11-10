using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// What an asset picker asks for: an asset of <paramref name="Kind"/> for <paramref name="Destination"/>. <see cref="Problem"/> is the use's own check
/// (such as whether the game draws a material on a decal): why the asset, resolved, can't be used there, or null
/// </summary>
public sealed record AssetPickRequest(string Title, IMEPackage Destination, AssetKind Kind)
{
    public Func<IEntry, PackageCache, string> Problem { get; init; }
}

/// <summary>An asset a picker offers: an entry of the file being edited, or an asset of the game that the asset database lists</summary>
public sealed class AssetChoice
{
    public string Name { get; private init; }
    public string InstancedFullPath { get; private init; }
    public AssetOrigin Origin { get; private init; }
    /// <summary>"In this file", "Base game", "DLC: name" or "Mod: folder"</summary>
    public string SourceLabel { get; private init; }
    public bool IsMod => Origin is AssetOrigin.Mod;
    /// <summary>The entry of the file being edited, for one in it (an export or an import). Null for one in other files</summary>
    public IEntry Entry { get; private init; }
    /// <summary>The asset database's record of it, or null</summary>
    public IndexedAsset Indexed { get; private init; }
    internal string SearchText { get; private init; }

    internal static AssetChoice InFile(IEntry entry, IndexedAsset indexed) => Create(entry.ObjectName.Instanced, entry.InstancedFullPath,
        AssetOrigin.ThisFile, AssetIndex.OriginLabel(AssetOrigin.ThisFile, null), entry, indexed, Path.GetFileName(entry.FileRef.FilePath));

    internal static AssetChoice FromIndex(IndexedAsset asset) => Create(asset.Name, asset.InstancedFullPath, asset.Source.File.Origin,
        asset.Source.File.Label, null, asset, asset.Source.File.FileName);

    private static AssetChoice Create(string name, string path, AssetOrigin origin, string label, IEntry entry, IndexedAsset indexed, string fileName) => new()
    {
        Name = name, InstancedFullPath = path, Origin = origin, SourceLabel = label, Entry = entry, Indexed = indexed,
        SearchText = $"{path} {label} {fileName}"
    };
}

/// <summary>
/// What a choice is, opened: what a pick refers to (<see cref="AssetReference"/>), the export it is (an import resolved), and why it can't be used, or null
/// </summary>
public sealed record ResolvedAssetChoice(AssetReference Reference, ExportEntry Export, string Problem);

public static class AssetPicking
{
    /// <summary>The class an asset of the kind is</summary>
    public static string ClassOf(AssetKind kind) => kind switch
    {
        AssetKind.StaticMesh => "StaticMesh",
        AssetKind.ParticleSystem => "ParticleSystem",
        _ => "MaterialInterface"
    };

    /// <summary>
    /// The destination's own entries of the kind (exports and imports), by name: of the kind's class, and for a decal material, drawn on a decal by the
    /// game (<see cref="MaterialUsage.DecalProblem"/>, resolving it through <paramref name="cache"/>). Judged as they are in the file, which may be edited,
    /// not as the asset database recorded them. Can be called off the UI thread
    /// </summary>
    public static List<AssetChoice> InFile(IMEPackage destination, AssetKind kind, PackageCache cache)
    {
        string className = ClassOf(kind);
        return destination.Exports.Where(e => !e.IsDefaultObject).Cast<IEntry>().Concat(destination.Imports)
            .Where(e => e.IsA(className) && (kind is not AssetKind.DecalMaterial || MaterialUsage.DecalProblem(e, cache) is null))
            .Select(e => AssetChoice.InFile(e, null))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.InstancedFullPath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// The choices for a pick with the game's asset database: <paramref name="inFile"/> (<see cref="InFile"/>, of <paramref name="destination"/>) with
    /// what the catalog records about each, then the catalog's other assets of the kind, by name. None at a path the destination has any entry at, even
    /// one <see cref="InFile"/> left out or of another class: the game loads one object per path, so a commit would use the destination's, or refuse a
    /// different one (<see cref="AssetPorting.Copy"/>). Opens no other package
    /// </summary>
    public static List<AssetChoice> WithCatalog(IMEPackage destination, IReadOnlyList<AssetChoice> inFile, AssetKind kind, AssetCatalog catalog)
    {
        var choices = inFile.Select(c => AssetChoice.InFile(c.Entry, catalog.Find(c.InstancedFullPath))).ToList();
        var paths = new HashSet<string>(destination.Exports.Cast<IEntry>().Concat(destination.Imports).Select(e => e.InstancedFullPath), StringComparer.OrdinalIgnoreCase);
        choices.AddRange(catalog.Assets(kind).Where(a => !paths.Contains(a.InstancedFullPath)).Select(AssetChoice.FromIndex));
        return choices;
    }

    /// <summary>The choices matching every word of <paramref name="query"/>, in a path, source or file name, in order</summary>
    public static IEnumerable<AssetChoice> Search(IEnumerable<AssetChoice> choices, string query)
    {
        string[] words = (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? choices : choices.Where(c => words.All(w => c.SearchText.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Opens the choice: an entry of the destination as it is, resolved through <paramref name="cache"/>; another file's asset from the copy the catalog
    /// prefers, opened through it, which a pick refers to by that file and its path. Then runs the use's check on it. Can be called off the UI thread
    /// </summary>
    public static ResolvedAssetChoice Resolve(AssetChoice choice, AssetPickRequest request, PackageCache cache)
    {
        IEntry entry = choice.Entry;
        ExportEntry export;
        AssetReference reference;
        if (entry is not null)
        {
            export = EffectiveLightingProperties.Resolve(entry, cache);
            reference = AssetReference.To(entry);
        }
        else
        {
            AssetCopy copy = choice.Indexed.Source;
            var package = cache.GetCachedPackage(copy.File.FilePath);
            export = package?.TryGetUExport(copy.UIndex, out var atIndex) == true
                && string.Equals(atIndex.InstancedFullPath, choice.InstancedFullPath, StringComparison.OrdinalIgnoreCase)
                ? atIndex : package?.FindExport(choice.InstancedFullPath);
            if (export is null || !export.IsA(ClassOf(request.Kind)))
            {
                return new(null, null, $"{choice.InstancedFullPath} isn't in {copy.File.FileName} any more, so the asset database is out of date. Rebuild it to use this asset.");
            }
            entry = export;
            reference = AssetReference.InFile(copy.File.FilePath, export.InstancedFullPath);
        }
        return new(reference, export, request.Problem?.Invoke(entry, cache));
    }
}

/// <summary>
/// A folder of the choices' tree by instanced full path: each object a path passes through (a package or group) is a folder, the choice its leaf
/// </summary>
public sealed class AssetFolder
{
    public string Name { get; }
    /// <summary>How many choices are under it, at any depth</summary>
    public int Count { get; private set; }
    public bool IsExpanded { get; set; }
    private readonly Dictionary<string, AssetFolder> folders = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AssetChoice> choices = [];
    private List<object> children;

    private AssetFolder(string name) => Name = name;

    /// <summary>Its folders by name, then its choices in their order</summary>
    public IReadOnlyList<object> Children => children ??= ChildrenOf(folders, choices);

    private static List<object> ChildrenOf(Dictionary<string, AssetFolder> folders, List<AssetChoice> choices) =>
        [.. folders.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase), .. choices];

    /// <summary>The top of the tree of <paramref name="choices"/>: its outermost folders, then any choice with no outer object</summary>
    /// <param name="expanded">Whether every folder starts expanded</param>
    public static IReadOnlyList<object> Build(IEnumerable<AssetChoice> choices, bool expanded)
    {
        var roots = new Dictionary<string, AssetFolder>(StringComparer.OrdinalIgnoreCase);
        var rootChoices = new List<AssetChoice>();
        foreach (var choice in choices)
        {
            string[] segments = choice.InstancedFullPath.Split('.');
            var level = roots;
            AssetFolder folder = null;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (!level.TryGetValue(segments[i], out var next))
                {
                    level[segments[i]] = next = new AssetFolder(segments[i]) { IsExpanded = expanded };
                }
                next.Count++;
                folder = next;
                level = next.folders;
            }
            (folder?.choices ?? rootChoices).Add(choice);
        }
        return ChildrenOf(roots, rootChoices);
    }
}
