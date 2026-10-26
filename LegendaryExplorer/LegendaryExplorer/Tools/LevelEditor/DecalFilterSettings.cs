using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Immutable actor references: indices are translated only when committing to a package.</summary>
public sealed class DecalFilterSettings : IEquatable<DecalFilterSettings>
{
    public string Mode { get; }
    public IReadOnlyList<IEntry> Actors { get; }
    public DecalFilterSettings(string mode, IEnumerable<IEntry> actors)
    {
        Mode = mode;
        Actors = Array.AsReadOnly(actors.Where(e => e is not null).Distinct().ToArray());
    }
    public bool Equals(DecalFilterSettings other) => other is not null && Mode == other.Mode && SameActors(other);
    internal bool SameActors(DecalFilterSettings other) => other is not null && Actors.Count == other.Actors.Count && Actors.All(other.Actors.Contains);
    public override bool Equals(object obj) => obj is DecalFilterSettings other && Equals(other);
    public override int GetHashCode() => Actors.Aggregate(Mode.GetHashCode(), (hash, actor) => hash ^ actor.GetHashCode());
    internal static DecalFilterSettings Read(ExportEntry component, PackageCache cache, PropertyCollection properties)
    {
        var (filter, package) = EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(component, "Filter", cache);
        return new(properties.GetProp<EnumProperty>("FilterMode")?.Value.Name ?? "FM_None",
            filter?.Select(p => package.GetEntry(p.Value)).Select(e => EffectiveLightingProperties.Resolve(e, cache) ?? e) ?? []);
    }
    internal bool Allows(ExportEntry actor) => Mode switch
    {
        "FM_None" => true,
        "FM_Affect" => Actors.Contains(actor),
        "FM_Ignore" => !Actors.Contains(actor),
        _ => throw new InvalidOperationException("Unknown decal receiver filter mode.")
    };
    internal void Validate(IMEPackage package)
    {
        if (Mode is not ("FM_None" or "FM_Affect" or "FM_Ignore")) throw new InvalidOperationException("Unknown decal receiver filter mode.");
        if (Actors.Any(e => e.Game != package.Game)) throw new InvalidOperationException("Decal filter actors must belong to the same game.");
    }
    internal void Write(PropertyCollection properties, IMEPackage logicalPackage, IMEPackage destination)
    {
        Validate(logicalPackage);
        properties.AddOrReplaceProp(new EnumProperty(Mode, "EFilterMode", destination.Game, "FilterMode"));
        properties.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(Actors.Select(e => new ObjectProperty(
            e.FileRef == logicalPackage ? e.UIndex : ImportReference(e, destination).UIndex)), "Filter"));
    }
    private static IEntry ImportReference(IEntry source, IMEPackage destination)
    {
        // Keep foreign filter actors as references; never clone another level's meshes into this level.
        IEntry parent = source.Parent is { } outer ? ImportReference(outer, destination)
            : source is ExportEntry ? destination.GetEntryOrAddImport(source.FileRef.FileNameNoExtension, "Package") : null;
        string path = parent is null ? source.ObjectName.Instanced : parent.InstancedFullPath + "." + source.ObjectName.Instanced;
        if (destination.FindEntry(path, source.ClassName) is { } existing) return existing;
        var import = source is ExportEntry export ? new ImportEntry(export, parent?.UIndex ?? 0, destination)
            : new ImportEntry(destination, parent, source.ObjectName) { ClassName = source.ClassName, PackageFile = ((ImportEntry)source).PackageFile };
        destination.AddImport(import);
        return import;
    }
}

public sealed record DecalFilterModeOption(string Value, string Label);

public sealed class DecalFilterCandidate(EditableDecal decal, IEntry actor) : NotifyPropertyChangedBase
{
    public IEntry Actor { get; } = actor;
    public string Label => $"{Actor.ObjectName.Instanced} — {System.IO.Path.GetFileName(Actor.FileRef.FilePath)}";
    public string Detail => Actor.ClassName == "StaticMeshCollectionActor" ? "Mesh collection — applies to every mesh in this collection"
        : Actor is ImportEntry ? "Unresolved actor reference" : Actor.ClassName;
    public string Path => Actor.InstancedFullPath;
    public bool IsChecked { get => decal.Settings.Filter.Actors.Contains(Actor); set => decal.SetFilterActor(Actor, value); }
    internal void Refresh() => OnPropertyChanged(nameof(IsChecked));
}
