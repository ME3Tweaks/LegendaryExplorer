using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

public readonly record struct DecalSettings(float Width, float Height, float NearPlane, float FarPlane,
    float TileX, float TileY, float OffsetX, float OffsetY, float Rotation, float DepthBias, int SortOrder)
{
    public DecalFilterSettings Filter { get; init; }
    /// <summary>
    /// The DecalMaterial, as the component has or inherits it (so it may be another package's, an archetype's, until it's changed), or as it's changed
    /// to: an entry of the component's package, or another file's asset. Null for none. Written by the commit only when it changes
    /// </summary>
    public AssetReference Material { get; init; }
    internal static DecalSettings Read(PropertyCollection props)
    {
        float F(string name, float fallback) => props.GetProp<FloatProperty>(name)?.Value ?? fallback;
        return new(F("Width", 200), F("Height", 200), F("NearPlane", 0), F("FarPlane", 300), F("TileX", 1), F("TileY", 1),
            F("OffsetX", 0), F("OffsetY", 0), F("DecalRotation", 0), F("DepthBias", -0.00006f), props.GetProp<IntProperty>("SortOrder")?.Value ?? 0);
    }
    internal void Write(PropertyCollection props)
    {
        foreach (var (name, value) in new[] { ("Width", Width), ("Height", Height), ("NearPlane", NearPlane), ("FarPlane", FarPlane),
            ("TileX", TileX), ("TileY", TileY), ("OffsetX", OffsetX), ("OffsetY", OffsetY), ("DecalRotation", Rotation), ("DepthBias", DepthBias) })
            props.AddOrReplaceProp(new FloatProperty(value, name));
        props.AddOrReplaceProp(new IntProperty(SortOrder, "SortOrder"));
    }
}

public sealed class EditableDecal : NotifyPropertyChangedBase
{
    private readonly DecalActorProxy owner;
    private DecalSettings clean;
    public DecalSettings Settings { get; private set; }
    public bool HasChanges => Settings != clean;
    internal bool FilterChanged => !Equals(Settings.Filter, clean.Filter);
    internal bool GeometryChanged => Settings.Width != clean.Width || Settings.Height != clean.Height
        || Settings.NearPlane != clean.NearPlane || Settings.FarPlane != clean.FarPlane || Settings.Rotation != clean.Rotation
        || Settings.Filter.Mode != clean.Filter.Mode || Settings.Filter.Mode != "FM_None" && !Settings.Filter.SameActors(clean.Filter);
    public bool IsEditable => !owner.IsReadOnly && owner.CanReproject;
    internal bool MaterialChanged => Settings.Material != clean.Material;
    /// <param name="material">Its material: its component's (<see cref="MaterialOf"/>), or a new decal's, whose component is a stand-in</param>
    /// <param name="filterFrom">
    /// A draft's: the component it's made from (a class default's, whose Filter is empty, or a copy's source's), whose Filter its stand-in's values,
    /// without references, don't have. Its own component otherwise
    /// </param>
    internal EditableDecal(DecalActorProxy owner, PropertyCollection props, AssetReference material, ExportEntry filterFrom = null)
    {
        this.owner = owner;
        var cache = owner.Editor.PackageCache;
        Settings = clean = DecalSettings.Read(props) with { Filter = DecalFilterSettings.Read(filterFrom ?? owner.DecalComponent.Export, cache, props), Material = material };
    }

    /// <summary>The material <paramref name="component"/> has, or inherits, as <paramref name="package"/> (by default the component's own) refers to it</summary>
    internal static AssetReference MaterialOf(ExportEntry component, PackageCache cache, IMEPackage package = null)
    {
        var (material, materialPackage) = EffectiveLightingProperties.Reference(component, "DecalMaterial", cache);
        return AssetReference.Of(material?.ResolveToEntry(materialPackage), package ?? component.FileRef, cache);
    }

    /// <summary>The package the decal's component is in, whose materials it can be given</summary>
    public IMEPackage Package => owner.DecalComponent.Export.FileRef;
    public string MaterialName => Settings.Material?.Description ?? "None";
    /// <summary>Why the game won't draw the decal's material (<see cref="MaterialUsage.DecalProblem"/>), or null</summary>
    public string MaterialProblem => MaterialUsage.DecalProblem(Settings.Material?.Find(owner.Editor.PackageCache), owner.Editor.PackageCache);

    /// <summary>
    /// Changes the DecalMaterial to a material in the decal's package or another file of its game's, unless the game wouldn't draw it on a decal; then
    /// returns why
    /// </summary>
    public string SetMaterial(AssetReference material)
    {
        if (!IsEditable) return "The decal can't be edited.";
        var cache = owner.Editor.PackageCache;
        if (material is null || material.Entry is { } entry && entry.FileRef != Package) return "Choose a material in the decal's package, or another file's.";
        if (material.Find(cache) is not { } found || !found.IsA(AssetPicking.ClassOf(AssetKind.DecalMaterial))) return $"{material.Description} isn't a material that can be found.";
        if (AssetPicking.ProblemOf(AssetKind.DecalMaterial, found, cache) is { } problem) return problem;
        Set(Settings with { Material = material }, "Material");
        return null;
    }
    public static IReadOnlyList<DecalFilterModeOption> FilterModes { get; } =
        [new("FM_None", "All meshes"), new("FM_Affect", "Only checked"), new("FM_Ignore", "All except checked")];
    public string FilterMode
    {
        get => Settings.Filter.Mode;
        set { if (FilterModes.Any(m => m.Value == value)) Set(Settings with { Filter = new(value, Settings.Filter.Actors) }, nameof(FilterMode)); }
    }
    private string filterSearch = "";
    private List<DecalFilterCandidate> filterCandidates = [];
    public string FilterSearch
    {
        get => filterSearch;
        set { if (SetProperty(ref filterSearch, value ?? "")) OnPropertyChanged(nameof(FilterCandidates)); }
    }
    public IEnumerable<DecalFilterCandidate> FilterCandidates => filterCandidates.Where(c => c.Matches(FilterSearch));
    public string FilterSummary => $"{Settings.Filter.Actors.Count} checked. Checked collection meshes become actors when committed.";
    /// <summary>The collection member <paramref name="entry"/> of a Filter is, or null</summary>
    internal CollectionActorComponentProxy MemberOf(IEntry entry) => entry is ExportEntry component && DecalFilterSettings.IsMember(component)
        ? owner.Editor?.RenderContext.FindDecalReceiver(component)?.Actor as CollectionActorComponentProxy : null;
    /// <summary>Why <paramref name="entry"/> can't be checked in the Filter, or null</summary>
    internal string FilterRefusal(IEntry entry)
    {
        if (MemberOf(entry) is not { } member) return null;
        if (!owner.DecalComponent.CanFilter(member)) return owner.DecalComponent.FilterPackageRefusal(entry);
        return member.UpgradePending ? null : CollectionMemberUpgrade.ActorRefusal(member);
    }
    internal void RefreshFilterCandidateStates()
    {
        foreach (var candidate in filterCandidates) candidate.Refresh();
    }
    public void RefreshFilterCandidates()
    {
        var meshes = owner.Editor.RenderContext.DrawList_3D.SelectMany(a => a.Components).OfType<StaticMeshComponentProxy>()
            .Where(c => c.Actor.Editor is not null).ToArray();
        // A collection's meshes one by one, as a Filter of actors lists them once they're actors: never the whole collection, which a shipping Filter never lists
        var loaded = meshes.Where(c => c.Actor is CollectionActorComponentProxy).Select(c => c.Export)
            .Concat(meshes.Select(c => c.Export.Parent).OfType<ExportEntry>().Where(e => e.IsA("StaticMeshActor")));
        filterCandidates = Settings.Filter.Actors.Concat(loaded).Distinct().Select(e => new DecalFilterCandidate(this, e))
            .OrderByDescending(c => c.IsChecked).ThenBy(c => c.Label).ToList();
        OnPropertyChanged(nameof(FilterCandidates));
    }
    public void SetFilterActor(IEntry actor, bool included)
    {
        if (actor is null || actor.Game != owner.Package.Game) return;
        var entries = included ? Settings.Filter.Actors.Append(actor) : Settings.Filter.Actors.Where(e => e != actor);
        Set(Settings with { Filter = new(Settings.Filter.Mode, entries) }, "Filter");
    }
    public void ClearFilter() => Set(Settings with { Filter = new(Settings.Filter.Mode, []) }, "Filter");
    public float Width { get => Settings.Width; set { if (float.IsFinite(value) && value > 0) Set(Settings with { Width = value }, nameof(Width)); } }
    public float Height { get => Settings.Height; set { if (float.IsFinite(value) && value > 0) Set(Settings with { Height = value }, nameof(Height)); } }
    public float NearPlane { get => Settings.NearPlane; set { if (float.IsFinite(value) && value >= 0 && value < FarPlane) Set(Settings with { NearPlane = value }, nameof(NearPlane)); } }
    public float FarPlane { get => Settings.FarPlane; set { if (float.IsFinite(value) && value > NearPlane) Set(Settings with { FarPlane = value }, nameof(FarPlane)); } }
    public float TileX { get => Settings.TileX; set { if (float.IsFinite(value)) Set(Settings with { TileX = value }, nameof(TileX)); } }
    public float TileY { get => Settings.TileY; set { if (float.IsFinite(value)) Set(Settings with { TileY = value }, nameof(TileY)); } }
    public float OffsetX { get => Settings.OffsetX; set { if (float.IsFinite(value)) Set(Settings with { OffsetX = value }, nameof(OffsetX)); } }
    public float OffsetY { get => Settings.OffsetY; set { if (float.IsFinite(value)) Set(Settings with { OffsetY = value }, nameof(OffsetY)); } }
    public float Rotation { get => Settings.Rotation; set { if (float.IsFinite(value)) Set(Settings with { Rotation = value }, nameof(Rotation)); } }
    public float DepthBias { get => Settings.DepthBias; set { if (float.IsFinite(value)) Set(Settings with { DepthBias = value }, nameof(DepthBias)); } }
    public int SortOrder { get => Settings.SortOrder; set => Set(Settings with { SortOrder = value }, nameof(SortOrder)); }
    private void Set(DecalSettings settings, string property)
    {
        if (!IsEditable || settings == Settings) return;
        var before = Settings; Settings = settings;
        OnPropertyChanged(property);
        NotifyFilter();
        NotifyMaterial();
        owner.OnDecalEdited(before, settings, property);
    }
    internal void Restore(DecalSettings settings)
    {
        Settings = settings;
        NotifySettings();
        owner.RefreshDecalDirty();
    }
    internal void MarkClean(DecalSettings settings) { clean = settings; Restore(settings); }
    private void NotifyMaterial()
    {
        OnPropertyChanged(nameof(MaterialName));
        OnPropertyChanged(nameof(MaterialProblem));
    }
    private void NotifySettings()
    {
        NotifyFilter();
        NotifyMaterial();
        foreach (string name in new[] { nameof(Width), nameof(Height), nameof(NearPlane), nameof(FarPlane), nameof(TileX), nameof(TileY),
            nameof(OffsetX), nameof(OffsetY), nameof(Rotation), nameof(DepthBias), nameof(SortOrder), nameof(IsEditable) }) OnPropertyChanged(name);
    }
    private void NotifyFilter()
    {
        bool added = false;
        foreach (var entry in Settings.Filter.Actors)
            if (!filterCandidates.Any(c => c.Actor == entry)) { filterCandidates.Add(new(this, entry)); added = true; }
        if (added) OnPropertyChanged(nameof(FilterCandidates));
        OnPropertyChanged(nameof(FilterMode));
        OnPropertyChanged(nameof(FilterSummary));
        foreach (var candidate in filterCandidates) candidate.Refresh();
    }
}

public sealed class DecalSettingsAction(DecalActorProxy actor, DecalSettings before, DecalSettings after, string property) : IMergeableUndoAction
{
    private readonly ActorRef subject = new(actor);
    private DecalSettings latest = after;
    private DateTime time = DateTime.UtcNow;
    public string Description => $"Edit decal {property}";
    public bool IsOrphaned => subject.IsOrphaned;
    public IReadOnlyCollection<ActorId> Subjects => [subject.Id];
    public IEnumerable<AssetReference> Assets => [before.Material, latest.Material];
    public void Undo() => ((DecalActorProxy)subject.View).Decal.Restore(before);
    public void Redo() => ((DecalActorProxy)subject.View).Decal.Restore(latest);
    public bool TryMerge(IUndoAction next)
    {
        if (next is not DecalSettingsAction action || !action.subject.RefersTo(subject) || action.Property != property
            || action.time - time > TimeSpan.FromMilliseconds(500)) return false;
        latest = action.latest; time = action.time; return true;
    }
    private string Property => property;
}
