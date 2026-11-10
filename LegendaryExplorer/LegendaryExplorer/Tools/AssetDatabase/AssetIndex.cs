using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.AssetDatabase;

/// <summary>What an asset picker asks for</summary>
public enum AssetKind
{
    StaticMesh,
    ParticleSystem,
    /// <summary>Any material, for a mesh</summary>
    Material,
    /// <summary>A material the game draws on a decal</summary>
    DecalMaterial
}

/// <summary>Where a copy of an asset is: the file being edited, or a game file of the base game, an official DLC, or a mod</summary>
public enum AssetOrigin
{
    ThisFile,
    BaseGame,
    DLC,
    Mod
}

/// <summary>A game file the index lists assets in. <paramref name="ContentName"/> is a DLC's name, or a mod's folder</summary>
public sealed record IndexedFile(string FilePath, AssetOrigin Origin, string ContentName)
{
    public string Label => AssetIndex.OriginLabel(Origin, ContentName);
    public string FileName => Path.GetFileName(FilePath);
}

/// <summary>One copy of an asset: an export of a game file</summary>
public readonly record struct AssetCopy(IndexedFile File, int UIndex);

/// <summary>An asset of the game, by instanced full path, with every export of it the asset database found</summary>
public sealed class IndexedAsset
{
    public string Name { get; init; }
    public string InstancedFullPath { get; init; }
    /// <summary>Its class, where the database records it: a material's. Null for a mesh or a particle system, whose kind is their class</summary>
    public string ClassName { get; init; }
    /// <summary>A line about it, such as a particle system's emitter count, or null</summary>
    public string Details { get; init; }
    /// <summary>Its copies, the one to use first: base game, then official DLC, then mods</summary>
    public IReadOnlyList<AssetCopy> Copies { get; init; }
    public AssetCopy Source => Copies[0];
    internal int Kinds { get; init; }
    public bool Is(AssetKind kind) => (Kinds & 1 << (int)kind) != 0;
}

/// <summary>
/// The assets of a game that its asset database lists, by kind, with where each is. Built from <see cref="AssetDB"/>'s mesh, material and particle
/// system records, which are keyed by instanced full path: every export with that path in any file is one asset
/// </summary>
public sealed class AssetCatalog
{
    public MEGame Game { get; }
    public string GenerationDate { get; }
    private readonly Dictionary<string, IndexedAsset> byPath;
    private readonly IndexedAsset[][] byKind;

    private AssetCatalog(MEGame game, string generationDate, Dictionary<string, IndexedAsset> byPath)
    {
        Game = game;
        GenerationDate = generationDate;
        this.byPath = byPath;
        byKind = Enum.GetValues<AssetKind>().Select(kind => byPath.Values.Where(a => a.Is(kind))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.InstancedFullPath, StringComparer.OrdinalIgnoreCase).ToArray()).ToArray();
    }

    /// <summary>The kind's assets, by name</summary>
    public IReadOnlyList<IndexedAsset> Assets(AssetKind kind) => byKind[(int)kind];

    /// <summary>The asset with this instanced full path, or null</summary>
    public IndexedAsset Find(string instancedFullPath) => instancedFullPath is not null ? byPath.GetValueOrDefault(instancedFullPath) : null;

    /// <summary>
    /// From a database of version <see cref="AssetDatabaseWindow.dbCurrentBuild"/>. <paramref name="findFile"/> finds a database file (its content
    /// folder and file name) on disk, or gives null for one that's gone, whose copies are left out. An asset with no copy left is left out
    /// </summary>
    public static AssetCatalog Build(AssetDB db, Func<string, string, IndexedFile> findFile)
    {
        var files = db.FileList.Select(f => f.DirectoryKey >= 0 && f.DirectoryKey < db.ContentDir.Count ? findFile(db.ContentDir[f.DirectoryKey], f.FileName) : null).ToArray();
        var byPath = new Dictionary<string, IndexedAsset>(StringComparer.OrdinalIgnoreCase);
        void Add(string path, IEnumerable<IAssetUsage> usages, int kinds, string className, string details)
        {
            if (string.IsNullOrEmpty(path) || kinds == 0 || byPath.ContainsKey(path)) return;
            var copies = usages.Where(u => u.FileKey >= 0 && u.FileKey < files.Length && files[u.FileKey] is not null)
                .Select(u => new AssetCopy(files[u.FileKey], u.UIndex)).DistinctBy(c => c.File)
                .OrderBy(c => c.File.Origin).ThenBy(c => c.File.FilePath, StringComparer.OrdinalIgnoreCase).ToArray();
            if (copies.Length == 0) return;
            byPath.Add(path, new IndexedAsset
            {
                Name = path[(path.LastIndexOf('.') + 1)..], InstancedFullPath = path, ClassName = className, Details = details, Copies = copies, Kinds = kinds
            });
        }

        foreach (var mesh in db.Meshes.Where(m => !m.IsSkeleton))
            Add(mesh.InstancedFullPath, mesh.Usages, 1 << (int)AssetKind.StaticMesh, null, null);
        foreach (var effect in db.Particles.Where(p => p.VFXType is ParticleSysRecord.VFXClass.ParticleSystem))
            Add(effect.InstancedFullPath, effect.Usages, 1 << (int)AssetKind.ParticleSystem, null, effect.EffectCount == 1 ? "1 emitter" : $"{effect.EffectCount} emitters");
        var materials = db.Materials.Where(m => !string.IsNullOrEmpty(m.InstancedFullPath)).DistinctBy(m => m.InstancedFullPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(m => m.InstancedFullPath, StringComparer.OrdinalIgnoreCase);
        foreach (var material in materials.Values)
        {
            int kinds = 1 << (int)AssetKind.Material;
            if (IsDecalMaterial(material, materials, db.Game)) kinds |= 1 << (int)AssetKind.DecalMaterial;
            Add(material.InstancedFullPath, material.Usages, kinds, material.ClassName, null);
        }
        return new AssetCatalog(db.Game, db.GenerationDate, byPath);
    }

    /// <summary>
    /// Whether the game draws a material on a decal, from what the database records, as <see cref="LevelEditor.MaterialUsage.DecalProblem"/> decides
    /// it from the material itself: it's an instance of (through every parent) a DecalMaterial, or of a material that sets bUsedWithDecals, and not a
    /// fog volume or special engine material. One with a parent the database doesn't have can't be shown to be one, so it isn't
    /// </summary>
    private static bool IsDecalMaterial(MaterialRecord material, Dictionary<string, MaterialRecord> materials, MEGame game)
    {
        var visited = new HashSet<MaterialRecord>();
        while (material.ClassName is not null && GlobalUnrealObjectInfo.IsA(material.ClassName, "MaterialInstance", game))
        {
            if (!visited.Add(material)) return false;
            if (material.ParentPath is null) return false;
            if (!materials.TryGetValue(material.ParentPath, out material)) return false;
        }
        if (material.ClassName is null) return false;
        if (SetsFlag(material, "bUsedAsSpecialEngineMaterial")) return false;
        if (GlobalUnrealObjectInfo.IsA(material.ClassName, "DecalMaterial", game)) return true;
        return !SetsFlag(material, "bUsedWithFogVolumes") && SetsFlag(material, "bUsedWithDecals");
    }

    //a base material's bool property, as its record lists them (MaterialScanner: the property's name, type and value)
    private static bool SetsFlag(MaterialRecord material, string flag) =>
        material.MatSettings.Any(s => s.Name == flag && s.Parm1 == nameof(PropertyType.BoolProperty) && s.Parm2 == bool.TrueString);
}

/// <summary>A game's <see cref="AssetCatalog"/>, or why there's none: <paramref name="Problem"/> is null when it loaded</summary>
public sealed record AssetIndexLoad(AssetCatalog Catalog, string Problem);

/// <summary>
/// The app's asset catalogs: each game's is loaded from its asset database once, in the background, and again once the database is rebuilt
/// </summary>
public static class AssetIndex
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<MEGame, (DateTime Written, Task<AssetIndexLoad> Load)> Loads = [];

    /// <summary>The game's catalog, loading it if its database is new or changed since it was last loaded</summary>
    public static Task<AssetIndexLoad> Load(MEGame game)
    {
        string path = AssetDatabaseWindow.GetDBPath(game);
        DateTime written = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
        lock (Gate)
        {
            if (Loads.TryGetValue(game, out var load) && load.Written == written && !load.Load.IsFaulted) return load.Load;
            var task = Task.Run(() => LoadFrom(path, game, written));
            Loads[game] = (written, task);
            return task;
        }
    }

    private static async Task<AssetIndexLoad> LoadFrom(string path, MEGame game, DateTime written)
    {
        if (written == default) return new(null, $"There's no {game} asset database.");
        var db = new AssetDB();
        await AssetDatabaseWindow.LoadDatabase(path, game, db, CancellationToken.None).ConfigureAwait(false);
        if (db.DatabaseVersion is null) return new(null, $"The {game} asset database couldn't be read.");
        if (db.DatabaseVersion != AssetDatabaseWindow.dbCurrentBuild)
            return new(null, $"The {game} asset database is out of date: it's version {db.DatabaseVersion}, and version {AssetDatabaseWindow.dbCurrentBuild} is needed.");
        return new(AssetCatalog.Build(db, GameFiles(game)), null);
    }

    /// <summary>
    /// The game's package files (base game and enabled DLC folders, as <see cref="MELoadedFiles.GetAllFiles"/> lists them), by content folder and file
    /// name, as the asset database names them: the content folder is the one holding the file's Cooked folder
    /// </summary>
    internal static Func<string, string, IndexedFile> GameFiles(MEGame game)
    {
        var dlcNames = MEDirectories.OfficialDLCNames(game);
        var files = new Dictionary<string, IndexedFile>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in MELoadedFiles.GetAllFiles(game))
        {
            if (ContentFolder(path) is not { } content) continue;
            AssetOrigin origin = MEDirectories.IsInBasegame(path, game) ? AssetOrigin.BaseGame
                : MEDirectories.IsInOfficialDLC(path, game) ? AssetOrigin.DLC : AssetOrigin.Mod;
            string name = origin is AssetOrigin.DLC && dlcNames.TryGetValue(content, out string dlcName) ? dlcName : content;
            files.TryAdd(Key(content, Path.GetFileName(path)), new IndexedFile(path, origin, name));
        }
        return (content, fileName) => files.GetValueOrDefault(Key(content, fileName));
    }

    private static string Key(string content, string fileName) => $"{content}\\{fileName}";

    //AssetDatabaseWindow.GetContentPath's: the folder holding the file's Cooked folder
    private static string ContentFolder(string path)
    {
        for (var directory = new FileInfo(path).Directory; directory?.Parent is not null; directory = directory.Parent)
        {
            if (directory.Name.StartsWith("Cooked", StringComparison.OrdinalIgnoreCase)) return directory.Parent.Name;
        }
        return null;
    }

    public static string OriginLabel(AssetOrigin origin, string contentName) => origin switch
    {
        AssetOrigin.ThisFile => "In this file",
        AssetOrigin.BaseGame => "Base game",
        AssetOrigin.DLC => $"DLC: {contentName}",
        _ => $"Mod: {contentName}"
    };
}
