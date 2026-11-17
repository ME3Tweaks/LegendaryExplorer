#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Packages.NetIndexing;

public static class Issues
{
    /// <summary>No declaration of the linker carries a guid.</summary>
    public const string NoGuid = "NO_GUID";
    /// <summary>Some declarations of the linker carry a guid and others are empty.</summary>
    public const string GuidPartial = "GUID_PARTIAL";
    /// <summary>The linker's declarations disagree on the guid.</summary>
    public const string GuidMismatch = "GUID_MISMATCH";
    /// <summary>Declarations of the linker disagree on its net object count.</summary>
    public const string CountMismatch = "COUNT_MISMATCH";
    /// <summary>Some declaration's net object count doesn't cover the linker's highest NetIndex.</summary>
    public const string CountTooSmall = "COUNT_TOO_SMALL";
    /// <summary>The same object has different net indexes in different files.</summary>
    public const string NetIndexMismatch = "NETINDEX_MISMATCH";
    /// <summary>Different objects in one linker share a net index.</summary>
    public const string NetIndexDuplicate = "NETINDEX_DUPLICATE";
    /// <summary>Localized content is networked: a localized linker declared with a count, or a localized file's own linker counted or indexed.</summary>
    public const string LocalizedNetworked = "LOCALIZED_NETWORKED";

    public static readonly string[] All = [NoGuid, GuidPartial, GuidMismatch, CountMismatch, CountTooSmall, NetIndexMismatch, NetIndexDuplicate, LocalizedNetworked];
}

/// <param name="Files">Indexes into <see cref="LinkerInventory.Files"/> of the files involved.</param>
public sealed record Finding(string Issue, string Linker, string Object, string Detail, int[] Files);

/// <summary>One object seen in one file. File indexes <see cref="LinkerInventory.Files"/>.</summary>
public readonly record struct ObjectObservation(int File, int NetIndex, bool LocalizedResource);

/// <summary>First observation inline, any further ones (the object lives in several files) in a list.</summary>
internal struct ObjectSlot
{
    public ObjectObservation First;
    public List<ObjectObservation>? Others;
    public bool HasFirst;
}

/// <summary>Every observation of one object across the scanned files, in the order the files were added.</summary>
public readonly struct ObjectObservations : IReadOnlyList<ObjectObservation>
{
    private readonly ObjectSlot _slot;

    internal ObjectObservations(ObjectSlot slot) => _slot = slot;

    public int Count => 1 + (_slot.Others?.Count ?? 0);

    public ObjectObservation this[int index] => index == 0 ? _slot.First : _slot.Others![index - 1];

    public IEnumerator<ObjectObservation> GetEnumerator()
    {
        yield return _slot.First;
        if (_slot.Others == null) yield break;
        foreach (var other in _slot.Others)
            yield return other;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Read-only view of everything the inventory saw for one linker.</summary>
public sealed class LinkerView
{
    internal LinkerView(string name) => Name = name;

    public string Name { get; }
    /// <summary>Where the linker declares its guid and count; File indexes <see cref="LinkerInventory.Files"/>.</summary>
    public IReadOnlyList<(int File, LinkerDeclaration Declaration)> Declarations => DeclarationList;
    /// <summary>Number of distinct object keys.</summary>
    public int ObjectCount => ObjectMap.Count;
    /// <summary>Total exports seen, counting an object once per file it appears in.</summary>
    public int ExportCount { get; internal set; }
    /// <summary>Highest NetIndex of any object in any file, or -1.</summary>
    public int MaxNetIndex { get; internal set; } = -1;
    /// <summary>At least one of the linker's objects carries RF_LocalizedResource in some file, so the linker is server-side only.</summary>
    public bool IsLocalized { get; internal set; }
    /// <summary>The linker is the file name of a localization file (a per-language package's own objects).</summary>
    public bool IsLocalizationFileLinker { get; internal set; }

    internal List<(int File, LinkerDeclaration Declaration)> DeclarationList { get; } = [];
    internal Dictionary<string, ObjectSlot> ObjectMap { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal bool HasNonLocalizationExports { get; set; }

    /// <summary>Linker-qualified object keys (case-insensitive) with their observations.</summary>
    public IEnumerable<KeyValuePair<string, ObjectObservations>> Objects =>
        ObjectMap.Select(kvp => new KeyValuePair<string, ObjectObservations>(kvp.Key, new ObjectObservations(kvp.Value)));

    public bool TryGetObject(string key, out ObjectObservations observations)
    {
        if (ObjectMap.TryGetValue(key, out var slot))
        {
            observations = new ObjectObservations(slot);
            return true;
        }

        observations = default;
        return false;
    }
}

/// <summary>
/// Accumulates every linker seen across the scanned packages, then checks each for a guid, consistent and unique net
/// indexes, and that localized content stays out of the package map.
/// </summary>
public sealed class LinkerInventory
{
    private readonly List<ScannedFile> _files = [];
    private readonly Dictionary<string, LinkerView> _linkers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _localizedLinkers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ScannedFile> Files => _files;
    public int LinkerCount => _linkers.Count;
    public long ExportCount { get; private set; }
    public long UnassignedExportCount { get; private set; }

    public IReadOnlyCollection<string> LinkerNames => _linkers.Keys;

    /// <summary>Every linker with at least one object flagged RF_LocalizedResource in any file.</summary>
    public IReadOnlyCollection<string> LocalizedLinkers => _localizedLinkers;

    public bool TryGetLinker(string name, out LinkerView view) => _linkers.TryGetValue(name, out view!);

    public void Add(PackageNetScan scan, string relativePath, string origin)
    {
        int fileIndex = _files.Count;
        _files.Add(new ScannedFile(relativePath, origin, scan.IsLocalizationFile));

        if (scan.IsLocalizationFile)
            GetLinker(scan.FileName).IsLocalizationFileLinker = true;

        foreach (var declaration in scan.Declarations)
            GetLinker(declaration.Linker).DeclarationList.Add((fileIndex, declaration));

        foreach (var export in scan.Exports)
        {
            var linker = GetLinker(export.Linker);
            linker.ExportCount++;
            linker.MaxNetIndex = Math.Max(linker.MaxNetIndex, export.NetIndex);
            ExportCount++;
            if (export.NetIndex < 0) UnassignedExportCount++;
            if (!scan.IsLocalizationFile) linker.HasNonLocalizationExports = true;
            if (export.LocalizedResource)
            {
                linker.IsLocalized = true;
                _localizedLinkers.Add(linker.Name);
            }

            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(linker.ObjectMap, export.Key, out _);
            var observation = new ObjectObservation(fileIndex, export.NetIndex, export.LocalizedResource);
            if (!slot.HasFirst)
            {
                slot.First = observation;
                slot.HasFirst = true;
            }
            else
            {
                (slot.Others ??= []).Add(observation);
            }
        }
    }

    private LinkerView GetLinker(string name)
    {
        if (!_linkers.TryGetValue(name, out var linker))
            _linkers[name] = linker = new LinkerView(name);
        return linker;
    }

    /// <summary>
    /// One row per (linker, file): the file's declaration of the linker, if any, and the net indexes of the linker's
    /// objects in that file. For investigating how linkers are laid out across files (e.g. localized variants).
    /// </summary>
    public IEnumerable<string[]> DumpLinkerFiles()
    {
        foreach (var linker in _linkers.Values.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
        {
            var perFile = new Dictionary<int, FileStats>();
            foreach (var slot in linker.ObjectMap.Values)
            {
                foreach (var o in new ObjectObservations(slot))
                {
                    var s = perFile.GetValueOrDefault(o.File, FileStats.Empty);
                    if (o.LocalizedResource)
                        s = s with { LocalizedResources = s.LocalizedResources + 1 };
                    perFile[o.File] = o.NetIndex < 0
                        ? s with { Count = s.Count + 1, Unassigned = s.Unassigned + 1 }
                        : s with { Count = s.Count + 1, Min = Math.Min(s.Min, o.NetIndex), Max = Math.Max(s.Max, o.NetIndex) };
                }
            }

            var files = perFile.Keys.Concat(linker.DeclarationList.Select(d => d.File)).Distinct().OrderBy(f => _files[f].Path, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var decl = linker.DeclarationList.Where(d => d.File == file).ToList();
                var stats = perFile.GetValueOrDefault(file, FileStats.Empty);
                yield return
                [
                    linker.Name, _files[file].Path, _files[file].Origin, _files[file].IsLocalization ? "loc" : "",
                    string.Join(";", decl.Select(d => d.Declaration.Source == DeclarationSource.ForcedExportPackage ? "export" : "header")),
                    string.Join(";", decl.Select(d => d.Declaration.Guid == Guid.Empty ? "empty" : d.Declaration.Guid.ToString())),
                    string.Join(";", decl.Select(d => d.Declaration.NetObjectCount.ToString())),
                    stats.Count.ToString(), stats.Unassigned.ToString(), stats.LocalizedResources.ToString(),
                    stats.Min == int.MaxValue ? "" : stats.Min.ToString(), stats.Max < 0 ? "" : stats.Max.ToString()
                ];
            }
        }
    }

    public List<Finding> Analyze()
    {
        var findings = new List<Finding>();
        foreach (var linker in _linkers.Values.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
        {
            CheckLocalization(linker, findings);

            // Localized linkers are server-side only, so whatever indexes they still carry are inert; a localized
            // file's own linker is never networked. Neither takes part in the guid, count or index checks.
            if (linker.IsLocalized || linker.IsLocalizationFileLinker)
                continue;

            CheckGuid(linker, findings);
            CheckCounts(linker, findings);
            CheckNetIndexes(linker, findings);
        }

        return findings;
    }

    /// <summary>
    /// Localized content must stay out of the package map: clients load different languages, so anything that varies by
    /// language would make each machine's ObjectBase computation diverge. A localized linker must have no generation
    /// count (0 and none are both fine); a localized file's own linker must have count 0 and every object at NetIndex -1.
    /// </summary>
    private void CheckLocalization(LinkerView linker, List<Finding> findings)
    {
        if (!linker.IsLocalized && !linker.IsLocalizationFileLinker)
            return;

        var files = new HashSet<int>();
        var details = new List<string>();

        foreach (var (file, declaration) in linker.DeclarationList.Where(d => d.Declaration.NetObjectCount > 0))
        {
            files.Add(file);
            details.Add($"declared with count {declaration.NetObjectCount} in {_files[file].Path} ({(declaration.Source == DeclarationSource.ForcedExportPackage ? "export" : "header")})");
        }

        if (linker.IsLocalizationFileLinker)
        {
            var perFile = new Dictionary<int, FileStats>();
            foreach (var slot in linker.ObjectMap.Values)
            {
                foreach (var o in new ObjectObservations(slot).Where(o => o.NetIndex >= 0))
                {
                    var s = perFile.GetValueOrDefault(o.File, FileStats.Empty);
                    perFile[o.File] = s with { Count = s.Count + 1, Min = Math.Min(s.Min, o.NetIndex), Max = Math.Max(s.Max, o.NetIndex) };
                }
            }

            foreach (var (file, stats) in perFile.OrderBy(p => _files[p.Key].Path, StringComparer.OrdinalIgnoreCase))
            {
                files.Add(file);
                details.Add($"{stats.Count} object(s) with NetIndex {stats.Min}-{stats.Max} in {_files[file].Path}");
            }
        }

        if (details.Count > 0)
            findings.Add(new Finding(Issues.LocalizedNetworked, linker.Name, "", string.Join("; ", details), files.Order().ToArray()));
    }

    private void CheckGuid(LinkerView linker, List<Finding> findings)
    {
        // Localized packages are never given guids (Conformance skips them), so they cannot fail this check.
        var declarations = linker.DeclarationList.Where(d => !_files[d.File].IsLocalization).ToList();
        if (declarations.Count == 0)
        {
            if (linker.HasNonLocalizationExports)
                findings.Add(new Finding(Issues.NoGuid, linker.Name, "", "No package declares this linker, so it cannot have a guid", []));
            return;
        }

        var empty = declarations.Where(d => d.Declaration.Guid == Guid.Empty).ToList();
        if (empty.Count == declarations.Count)
        {
            findings.Add(new Finding(Issues.NoGuid, linker.Name, "",
                $"All {declarations.Count} declaration(s) have an empty guid: {DescribeDeclarations(empty)}",
                empty.Select(d => d.File).Distinct().ToArray()));
            return;
        }

        if (empty.Count > 0)
        {
            findings.Add(new Finding(Issues.GuidPartial, linker.Name, "",
                $"Empty guid in {empty.Count} of {declarations.Count} declaration(s): {DescribeDeclarations(empty)}",
                empty.Select(d => d.File).Distinct().ToArray()));
        }

        var byGuid = declarations.Where(d => d.Declaration.Guid != Guid.Empty).GroupBy(d => d.Declaration.Guid).ToList();
        if (byGuid.Count > 1)
        {
            findings.Add(new Finding(Issues.GuidMismatch, linker.Name, "",
                string.Join(" | ", byGuid.Select(g => $"{g.Key} <- {DescribeDeclarations(g)}")),
                byGuid.SelectMany(g => g).Select(d => d.File).Distinct().ToArray()));
        }
    }

    private string DescribeDeclarations(IEnumerable<(int File, LinkerDeclaration Declaration)> declarations) =>
        string.Join(", ", declarations.Select(d =>
            $"{_files[d.File].Path}{(d.Declaration.Source == DeclarationSource.ForcedExportPackage ? " (export)" : " (header)")}"));

    /// <summary>
    /// The engine reads a linker's net object count from whichever declaring file loads first (UPackage::InitNetInfo), and
    /// UPackageMap computes every package's index base from these counts on each machine independently - they are not sent
    /// over the wire. So every declaration must carry the same count, and it must cover every NetIndex in the linker.
    /// </summary>
    private void CheckCounts(LinkerView linker, List<Finding> findings)
    {
        if (linker.DeclarationList.Count == 0)
            return;

        // No count (server-side only) and a count of 0 both mean the package registers no net objects.
        var byCount = linker.DeclarationList.GroupBy(d => Math.Max(0, d.Declaration.NetObjectCount)).OrderBy(g => g.Key).ToList();
        if (byCount.Count > 1)
        {
            findings.Add(new Finding(Issues.CountMismatch, linker.Name, "",
                string.Join(" | ", byCount.Select(g => $"{g.Key} <- {DescribeDeclarations(g)}")),
                linker.DeclarationList.Select(d => d.File).Distinct().ToArray()));
        }

        int smallest = byCount[0].Key;
        if (linker.MaxNetIndex >= 0 && linker.MaxNetIndex >= smallest)
        {
            var small = byCount.Where(g => g.Key <= linker.MaxNetIndex).SelectMany(g => g).ToList();
            findings.Add(new Finding(Issues.CountTooSmall, linker.Name, "",
                $"Highest NetIndex is {linker.MaxNetIndex}, but these declare fewer net objects: " +
                string.Join(", ", small.Select(d => $"{d.Declaration.NetObjectCount} in {_files[d.File].Path}")),
                small.Select(d => d.File).Distinct().ToArray()));
        }
    }

    private void CheckNetIndexes(LinkerView linker, List<Finding> findings)
    {
        // Net index -> objects holding it, for the within-linker uniqueness check.
        var holders = new Dictionary<int, List<(string Object, int[] Files)>>();

        foreach (var (objectPath, slot) in linker.ObjectMap)
        {
            var observations = new ObjectObservations(slot).ToArray();
            var distinctIndexes = observations.Select(o => o.NetIndex).Distinct().ToList();

            if (distinctIndexes.Count > 1)
            {
                var detail = string.Join(" | ", observations
                    .GroupBy(o => o.NetIndex).OrderBy(g => g.Key)
                    .Select(g => $"{g.Key} <- {string.Join(", ", g.Select(o => _files[o.File].Path).Distinct())}"));
                findings.Add(new Finding(Issues.NetIndexMismatch, linker.Name, objectPath, detail,
                    observations.Select(o => o.File).Distinct().ToArray()));
            }

            // Unassigned (-1) objects are not net-indexed, so they cannot collide.
            foreach (int index in distinctIndexes.Where(i => i >= 0))
            {
                if (!holders.TryGetValue(index, out var list))
                    holders[index] = list = [];
                list.Add((objectPath, observations.Where(o => o.NetIndex == index).Select(o => o.File).Distinct().ToArray()));
            }
        }

        foreach (var (index, objects) in holders.Where(h => h.Value.Count > 1).OrderBy(h => h.Key))
        {
            findings.Add(new Finding(Issues.NetIndexDuplicate, linker.Name, string.Join(" ; ", objects.Select(o => o.Object).Order(StringComparer.OrdinalIgnoreCase)),
                $"NetIndex {index} is shared by {objects.Count} objects",
                objects.SelectMany(o => o.Files).Distinct().ToArray()));
        }
    }
}

public sealed record ScannedFile(string Path, string Origin, bool IsLocalization);

/// <summary>
/// A linker's objects within one file: how many, how many unassigned (NetIndex &lt; 0), how many carry
/// RF_LocalizedResource, and the assigned range.
/// </summary>
internal readonly record struct FileStats(int Count, int Unassigned, int LocalizedResources, int Min, int Max)
{
    public static FileStats Empty => new(0, 0, 0, int.MaxValue, -1);
}
