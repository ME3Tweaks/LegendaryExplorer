using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// What a commit changes, besides deleting an actor, that takes away references to it: the entries it trashes with it (other pending deletions'),
/// and the properties it writes to other exports (a spline point's neighbours', without their connections to it)
/// </summary>
public sealed record DeletionRepairs(IReadOnlySet<IEntry> Trashed, IReadOnlyDictionary<ExportEntry, PropertyCollection> WrittenProperties)
{
    public static DeletionRepairs None { get; } = new(new HashSet<IEntry>(), new Dictionary<ExportEntry, PropertyCollection>());
}

/// <summary>What goes when an actor's deleted</summary>
/// <param name="Going">The actor, then what goes with it</param>
/// <param name="Plans">Their plans, in that order</param>
public sealed record DeletionChoice(IReadOnlyList<ActorProxy> Going, ActorDeletionPlan[] Plans)
{
    /// <summary>How many actors are left attached to what goes, which are detached</summary>
    public int Detached => Plans.Sum(p => p.DetachedActors);
}

/// <summary>What's attached to an actor being deleted, for deciding what goes with it (<see cref="ActorDeletionPlan.FindAttachedAsync"/>)</summary>
/// <param name="All">Everything attached that can go with it</param>
/// <param name="Least">Only what it can't be deleted without: those that refer to it other than by attachment, or to another that does</param>
/// <param name="Blockers">What those references are</param>
public sealed record AttachedDependents(DeletionChoice All, DeletionChoice Least, IReadOnlyList<string> Blockers)
{
    /// <summary>The question to ask, which says what each answer leaves attached to what goes. What's chained under a kept actor stays attached to it</summary>
    public string Question()
    {
        static string Names(IEnumerable<ActorProxy> actors) => ActorDeletionPlan.Listed(actors.Select(a => a.ActorName).ToArray());
        string actor = All.Going[0].ActorName;
        var required = Least.Going.Skip(1).ToArray();
        var optional = All.Going.Skip(1).Except(required).ToArray();
        string them = (optional.Length > 0 ? optional.Length : required.Length) == 1 ? "it" : "them";
        string message = required.Length == 0
            ? $"{(optional.Length == 1 ? "This actor is" : "These actors are")} attached to {actor}. Delete {them} too?\n\n{Names(optional)}"
            : $"{actor} can't be deleted without {(required.Length == 1 ? "this actor" : "these actors")}:\n\n{Names(required)}\n\n"
              + $"References:\n{ActorDeletionPlan.Listed(Blockers)}"
              + (optional.Length > 0 ? $"\n\n{(optional.Length == 1 ? "This is" : "These are")} attached to it too. Delete {them} as well?\n\n{Names(optional)}" : $"\n\nDelete {them} too?");
        if (All.Detached > 0)
            message += $"\n\nWith {them} deleted, {(All.Detached == 1 ? "1 other attached actor stays where it is, detached, as it can't be deleted" : $"{All.Detached} other attached actors stay where they are, detached, as they can't be deleted")}.";
        if (optional.Length > 0 && Least.Detached > 0)
            message += $"\n\nWith {them} kept, {(Least.Detached == 1 ? "1 attached actor stays where it is, detached" : $"{Least.Detached} attached actors stay where they are, detached")}.";
        return message;
    }
}

/// <summary>Reference inspection and commit repairs for supported actor ownership recipes. Preparation never changes the package.</summary>
public sealed class ActorDeletionPlan
{
    public ExportEntry Actor { get; }
    public ExportEntry Level { get; }
    /// <summary>For one of a static mesh or light collection's members: the collection, whose component list and transforms the commit removes it from</summary>
    public ExportEntry Collection { get; }
    /// <summary>What blocks it: <see cref="OtherBlockers"/>, then each reference to it, by what refers, as they were found</summary>
    public IReadOnlyList<string> Blockers => [.. otherBlockers, .. referencesByReferrer.SelectMany(r => r.Value.Select(reference => Blocker(r.Key, reference)))];
    // The blockers that aren't references, which Inspect finds
    private readonly List<string> otherBlockers;
    // What each export that blocks it refers to it by, in the order they were found
    private readonly OrderedDictionary<ExportEntry, List<string>> referencesByReferrer = [];
    private static string Blocker(ExportEntry referrer, string reference) => $"#{referrer.UIndex} {referrer.InstancedFullPath}: {reference}";
    /// <summary>The blockers that come from <paramref name="entries"/>, which it couldn't be deleted without</summary>
    public IReadOnlyList<string> BlockersFrom(IEnumerable<ExportEntry> entries) =>
        entries.SelectMany(e => (referencesByReferrer.GetValueOrDefault(e) ?? []).Select(reference => Blocker(e, reference))).ToArray();
    /// <summary>The exports whose references block it</summary>
    public IReadOnlyList<BlockingReferrer> GetBlockingReferrers() => referencesByReferrer.Select(r => new BlockingReferrer(r.Key, r.Value.ToArray())).ToArray();
    /// <summary>What blocks it other than references to it</summary>
    public IReadOnlyList<string> OtherBlockers => otherBlockers;
    public IReadOnlyList<ExportEntry> Entries { get; }
    public string Repairs => $"Committing removes it from {(Collection is null ? "the level" : Collection.ObjectName.Instanced)}"
                             + $"{(cleansDestroyedContent ? " and from the world's destroyed-actor list" : "")}, and trashes it"
                             + (Entries.Count > 1 ? $" with the {Entries.Count - 1} objects it owns." : ".")
                             + (DetachedActors > 0 ? $" {(DetachedActors == 1 ? "The actor attached to it stays where it is" : $"The {DetachedActors} actors attached to it stay where they are")}, detached." : "")
                             + (prefabs.Count > 0 ? " It stays removed from the prefab it came from." : "")
                             + (shadowGrouped.Count > 0 ? $" {(shadowGrouped.Count == 1 ? "The component shadowed with it casts its own shadow" : $"The {shadowGrouped.Count} components shadowed with it cast their own shadows")}." : "")
                             + (decals.Count > 0 ? $" It's also taken out of the {(decals.Count == 1 ? "decal that lands on it or lists it" : $"{decals.Count} decals that land on it or list it")}." : "")
                             // Light-maps and shadow-maps refer to a light by its GUIDs, not by reference
                             + (LightClasses.Contains(Actor.ClassName) || Collection is not null && IsLightComponent(Actor)
                                 ? " What it lit stays lit by it in the level's baked lighting until a rebake." : "");
    // Whether BioWorldInfo's ClientDestroyedActorContent refers to it, which TrashEntries cleans. Found by CheckReferences, which makes the plan with Inspect
    private bool cleansDestroyedContent;
    // The decals that land on it or list it in their Filters, which the commit takes it out of. Found by CheckReferences too
    private readonly List<ExportEntry> decals = [];
    private readonly List<ExportEntry> decalsLeftEmpty = [];
    // The actors attached to it, or that it's attached to, whose references the commit clears. Found by CheckReferences too
    private readonly List<ExportEntry> attachments = [];
    /// <summary>How many actors are left attached to it, which the commit detaches</summary>
    public int DetachedActors { get; private set; }
    // The prefab instances it's a member of, whose entries for it the commit blanks. Found by CheckReferences too
    private readonly List<ExportEntry> prefabs = [];
    // The components shadowed as part of one of its own, which the commit takes out of its shadow group. Found by CheckReferences too
    private readonly List<ExportEntry> shadowGrouped = [];
    /// <summary>The decals that land on it and, with it and what's already pending deletion gone, on nothing else</summary>
    public IReadOnlyList<ExportEntry> DecalsLeftEmpty => decalsLeftEmpty;
    private readonly (ExportEntry Entry, int Index, byte[] Header, byte[] Data)[] snapshots;
    // A collection member's transform in its collection when the deletion was prepared, which is its as much as its own export is
    private readonly Matrix4x4? memberTransform;
    private readonly ExportEntry worldInfo;
    private readonly bool canCleanDestroyedContent;

    private static IReadOnlyList<string> LightClasses => LightActorProxy.StandaloneClasses;
    private static readonly string[] SupportedClasses = ["Note", "TargetPoint", "Emitter", "DecalActor", "StaticMeshActor", "InterpActor", "SplineActor", "SplineLoftActor", .. LightClasses];
    private static bool Supports(ExportEntry actor) => actor.Game.IsLEGame() && (SupportedClasses.Contains(actor.ClassName) || IsVolume(actor) || CollectionOf(actor) is not null);

    /// <summary>Whether it's a volume: any class of one, as what each owns is checked (<see cref="CollectOwnedEntries"/>)</summary>
    private static bool IsVolume(ExportEntry actor) => actor.IsA("Volume");

    /// <summary>
    /// The collection <paramref name="member"/> is one of, if it's a kind whose deletion is supported: a StaticMeshCollectionActor's static mesh
    /// component, or a StaticLightCollectionActor's point, spot or directional light (as standalone lights are; not its sky lights). Null otherwise
    /// </summary>
    private static ExportEntry CollectionOf(ExportEntry member) => member.Parent is ExportEntry collection && !member.IsDefaultObject && !member.IsArchetype
        && (collection.ClassName == "StaticMeshCollectionActor" && member.ClassName == "StaticMeshComponent"
            || collection.ClassName == "StaticLightCollectionActor" && IsLightComponent(member))
            ? collection
            : null;

    /// <summary>Why an actor can't be taken out of the scene now, or null: all a draft (which has nothing in its package to check) needs</summary>
    public static string RemovalRefusal(ActorProxy actor)
    {
        if (actor is null || actor.IsDisposed) return "Select an actor first.";
        if (actor.Editor?.RenderContext.DrawList_3D.Contains(actor) != true) return "It isn't in the scene.";
        if (actor.OwningFile is not { IsReadOnly: false }) return "Its file is read-only or closed.";
        if (actor.Editor.RenderContext.IsDragging || actor.Editor.RenderContext.IsEditingScene) return "Finish the current scene operation first.";
        return null;
    }

    /// <summary>Why a committed actor can't be deleted now, or null: <see cref="RemovalRefusal"/>, and whether its class's deletion is supported</summary>
    public static string Refusal(ActorProxy actor)
    {
        if (RemovalRefusal(actor) is { } refusal) return refusal;
        if (!Supports(actor.Export))
            return $"Committed deletion supports only the Legendary Edition's {string.Join(", ", SupportedClasses)}, volumes, and static mesh and light "
                   + "collections' meshes and point, spot and directional lights.";
        return null;
    }

    /// <param name="otherBlockers">What blocks it other than references to it, which <see cref="CheckReferences"/> finds</param>
    private ActorDeletionPlan(ExportEntry actor, ExportEntry level, ExportEntry collection, List<ExportEntry> entries, List<string> otherBlockers,
        ExportEntry worldInfo, bool canCleanDestroyedContent)
    {
        Actor = actor;
        Level = level;
        Collection = collection;
        this.otherBlockers = otherBlockers;
        Entries = entries.AsReadOnly();
        snapshots = entries.Select(e => (e, e.UIndex, Header(e), e.DataReadOnly.ToArray())).ToArray();
        memberTransform = collection is not null ? MemberTransform(actor) : null;
        this.worldInfo = worldInfo;
        this.canCleanDestroyedContent = canCleanDestroyedContent;
    }

    internal bool MatchesSnapshot()
    {
        var current = Actor.GetAllDescendants().Prepend(Actor).ToHashSet();
        return current.SetEquals(Entries) && snapshots.All(s => Actor.FileRef.GetEntry(s.Index) == s.Entry
            && Header(s.Entry).SequenceEqual(s.Header) && s.Entry.DataReadOnly.SequenceEqual(s.Data))
            && (Collection is null || Actor.Parent == Collection && MemberTransform(Actor) == memberTransform);
    }

    /// <summary>Why <paramref name="member"/> isn't one of <paramref name="collection"/>'s members, listed once with a transform, or null</summary>
    internal static string MembershipProblem(ExportEntry member, ExportEntry collection) =>
        member.Parent != collection ? $"It's no longer in {collection.ObjectName.Instanced}."
        : MemberTransform(member) is null ? $"{collection.ObjectName.Instanced} doesn't list it once." : null;

    /// <summary>A collection member's transform in its collection, or null if the collection doesn't list it once, with a transform</summary>
    internal static Matrix4x4? MemberTransform(ExportEntry member)
    {
        if (member.Parent is not ExportEntry collection || ObjectBinary.From(collection) is not StaticCollectionActor binary
            || collection.GetProperty<ArrayProperty<ObjectProperty>>(binary.ComponentPropName) is not { } list
            || list.Count(o => o.Value == member.UIndex) != 1 || binary.LocalToWorldTransforms.Count != list.Count)
            return null;
        return binary.LocalToWorldTransforms[list.Values.FindIndex(o => o.Value == member.UIndex)];
    }

    /// <summary>An export's header without its DataOffset, which saving the package rewrites without changing the export</summary>
    private static byte[] Header(ExportEntry export)
    {
        byte[] header = export.Header;
        Array.Clear(header, ExportEntry.OFFSET_DataOffset, sizeof(int));
        return header;
    }

    private static bool IsLightComponent(ExportEntry e) => e.ClassName is "PointLightComponent" or "SpotLightComponent" or "DirectionalLightComponent";

    private static List<ExportEntry> CollectOwnedEntries(ExportEntry actor, bool member, List<string> blockers)
    {
        var entries = new List<ExportEntry> { actor };
        bool meshActor = !member && actor.ClassName is "StaticMeshActor" or "InterpActor";
        bool splineActor = !member && actor.ClassName is "SplineActor" or "SplineLoftActor";
        bool lightActor = !member && LightClasses.Contains(actor.ClassName);
        bool emitter = !member && actor.ClassName == "Emitter";
        bool decalActor = !member && actor.ClassName == "DecalActor";
        bool volume = !member && IsVolume(actor);
        foreach (var child in actor.GetAllDescendants())
        {
            // Ownership comes from the outer chain, never from an asset/property reference. Unknown
            // children (including meshes, materials and textures) must not be swept up by recursive trash.
            bool supported = child is ExportEntry e && !e.IsDefaultObject && !e.IsArchetype && !e.IsClass
                && (meshActor && (e.Parent == actor && e.ClassName is ("StaticMeshComponent" or "DynamicLightEnvironmentComponent")
                        || e.Parent?.ClassName == "StaticMeshComponent" && e.ClassName is ("RB_BodyInstance" or "ShadowMap1D" or "ShadowMap2D"))
                    // A collection's mesh's shadow maps: all that shipping levels' members own
                    || member && e.Parent == actor && actor.ClassName == "StaticMeshComponent" && e.ClassName is ("ShadowMap1D" or "ShadowMap2D")
                    // A point's outgoing connections: their curves and loft meshes (and the meshes' light/shadow data)
                    || splineActor && (e.Parent == actor && e.ClassName is ("SplineComponent" or "SplineMeshComponent" or "DynamicLightEnvironmentComponent")
                        || e.Parent?.ClassName == "SplineMeshComponent" && e.ClassName is ("RB_BodyInstance" or "ShadowMap1D" or "ShadowMap2D"))
                    // A light's component, and its light function
                    || lightActor && (e.Parent == actor && IsLightComponent(e) || e.Parent is ExportEntry owner && IsLightComponent(owner) && e.ClassName == "LightFunction")
                    // Its component, and in a few of LE2's, the light environment that lights its particles. Its template is an asset of the package, not under it
                    || emitter && e.Parent == actor && e.ClassName is ("ParticleSystemComponent" or "ParticleLightEnvironmentComponent")
                    // Its component, and the shadow maps of its surfaces. Its material is an asset of the package, not under it
                    || decalActor && e.Parent == actor && (IsDecal(e) || e.ClassName == "ShadowMap1D")
                    // Its collision, and in some, its brush, with its polygons. Most cooked volumes' Brush is an empty model of the level's that others share too
                    || volume && (e.Parent == actor && e.ClassName is ("BrushComponent" or "Model") || e.Parent?.Parent == actor && e.Parent.ClassName == "Model" && e.ClassName == "Polys"));
            if (supported) entries.Add((ExportEntry)child);
            else blockers.Add($"#{child.UIndex} {child.InstancedFullPath}: unsupported owned subobject.");
        }
        // That a property of the actor's refers to a component of this kind that the actor owns, or to nothing
        void RequireOwned(string name, Func<ExportEntry, bool> isKind, string kind)
        {
            foreach (var prop in actor.GetProperties().Where(p => p.Name == name))
            {
                if (prop is not ObjectProperty reference || reference.Value != 0
                    && !entries.Any(e => e.Parent == actor && e.UIndex == reference.Value && isKind(e)))
                    blockers.Add($"{name} must refer to {kind} owned by this actor.");
            }
        }
        if (lightActor) RequireOwned("LightComponent", IsLightComponent, "a light component");
        if (decalActor) RequireOwned("Decal", IsDecal, "a supported component");
        if (volume)
        {
            RequireOwned("BrushComponent", e => e.ClassName == "BrushComponent", "a supported component");
            RequireOwned("CollisionComponent", e => e.ClassName == "BrushComponent", "a supported component");
        }
        if (emitter)
        {
            RequireOwned("ParticleSystemComponent", e => e.ClassName == "ParticleSystemComponent", "a supported component");
            RequireOwned("LightEnvironment", e => e.ClassName == "ParticleLightEnvironmentComponent", "a supported component");
        }
        if (meshActor)
        {
            RequireOwned("StaticMeshComponent", e => e.ClassName == "StaticMeshComponent", "a supported component");
            RequireOwned("CollisionComponent", e => e.ClassName == "StaticMeshComponent", "a supported component");
            RequireOwned("LightEnvironment", e => e.ClassName == "DynamicLightEnvironmentComponent", "a supported component");
            foreach (var prop in actor.GetProperties().Where(p => p.Name == "Components"))
            {
                if (prop is not ArrayProperty<ObjectProperty> components
                    || components.Any(c => c.Value != 0 && !entries.Any(e => e.Parent == actor && e.UIndex == c.Value)))
                    blockers.Add("Components must contain only supported components owned by this actor.");
            }
        }
        return entries;
    }

    /// <summary>
    /// <see cref="Prepare(ExportEntry, ExportEntry, DeletionRepairs)"/>, with the scan of the package for references away from the dispatcher, refusing
    /// results if the package changed during the scan (<see cref="IMEPackage.ChangeCount"/>): another tool can still edit it meanwhile. A scan that
    /// failed because of such an edit (its exports changing as they're read) is refused the same way. Only the scan runs away from the dispatcher:
    /// it reads exports' headers, properties and binaries, not the package's lookup tree, which an edit invalidates and its next reader rebuilds
    /// unsynchronized (as <see cref="Inspect"/> reads it, finding the actor's owned entries). Without the shared package cache, which ChangeCount
    /// needs to see edits, the scan is made here instead
    /// </summary>
    public static Task<ActorDeletionPlan> PrepareAsync(ExportEntry actor, ExportEntry level, DeletionRepairs repairs = null) =>
        PrepareAllAsync([(actor, level)], _ => [repairs], plans => plans[0]);

    /// <summary>
    /// <see cref="PrepareAsync"/> for <paramref name="actors"/> going together, from one scan of each package: nothing needs repairing in what's going too.
    /// Each is blocked only by what stays
    /// </summary>
    public static Task<ActorDeletionPlan[]> PrepareTogetherAsync(IReadOnlyList<ActorProxy> actors)
    {
        var repairs = actors.Select(a => a.GetDeletionRepairs()).ToArray();
        return PrepareAllAsync(actors.Select(a => (a.Export, a.OwningFile.LevelExport)).ToArray(), plans =>
        {
            IReadOnlySet<IEntry> trashed = repairs.SelectMany(r => r.Trashed).Concat(plans.SelectMany(p => p.Entries)).ToHashSet();
            return repairs.Select(r => r with { Trashed = trashed }).ToArray();
        }, plans => plans);
    }

    /// <summary>
    /// What's attached to <paramref name="actor"/> that can go with it, for deciding what does. Each check assumes the others go, so it's made again without an
    /// attached actor that's blocked, and what's attached to that, which stay: otherwise what depends on it being gone would be wrong. If <paramref name="actor"/>
    /// would still be blocked with all of them gone, there's nothing to decide, so it throws
    /// </summary>
    /// <param name="plan">Its plan on its own, which says what it needs gone</param>
    public static async Task<AttachedDependents> FindAttachedAsync(ActorProxy actor, ActorDeletionPlan plan)
    {
        var refused = new HashSet<ActorProxy>();
        var attached = AttachedActors(actor);
        ActorDeletionPlan[] plans;
        while (true)
        {
            plans = await PrepareTogetherAsync([actor, .. attached]);
            var blocked = attached.Where((_, i) => plans[i + 1].Blockers.Count != 0).ToArray();
            if (blocked.Length == 0) break;
            refused.UnionWith(blocked);
            attached = AttachedActors(actor, refused);
        }
        plans[0].RequireAllowed();
        //what it needs gone, then what those need gone
        var required = new List<ActorProxy>();
        var blockers = new List<string>();
        ActorDeletionPlan[] requiredPlans = [plan];
        while (true)
        {
            var needed = attached.Select((a, i) => (Actor: a, Plan: plans[i + 1])).Where(a => !required.Contains(a.Actor))
                .Select(a => (a.Actor, Blockers: requiredPlans.SelectMany(p => p.BlockersFrom(a.Plan.Entries)).ToArray())).Where(a => a.Blockers.Length != 0).ToArray();
            if (needed.Length == 0) break;
            required.AddRange(needed.Select(n => n.Actor));
            blockers.AddRange(needed.SelectMany(n => n.Blockers));
            requiredPlans = await PrepareTogetherAsync([actor, .. required]);
        }
        return new AttachedDependents(new DeletionChoice([actor, .. attached], plans), new DeletionChoice([actor, .. required], requiredPlans), blockers);
    }

    /// <summary>
    /// The decals that deleting <paramref name="going"/> (with <paramref name="plans"/>) would leave with nothing to project onto, and can go with them, and the
    /// plans of all of those. An emptied decal that's blocked stays. If there are none, these are <paramref name="plans"/>
    /// </summary>
    public static async Task<(IReadOnlyList<ActorProxy> Decals, ActorDeletionPlan[] Plans)> FindEmptiedDecalsAsync(IReadOnlyList<ActorProxy> going, ActorDeletionPlan[] plans)
    {
        IReadOnlyList<ActorProxy> decals = EmptiedDecals(going[0], plans);
        while (decals.Count != 0)
        {
            var together = await PrepareTogetherAsync([.. going, .. decals]);
            var blocked = decals.Where((_, i) => together[going.Count + i].Blockers.Count != 0).ToArray();
            if (blocked.Length == 0) return (decals, together);
            decals = decals.Except(blocked).ToArray();
        }
        return ([], plans);
    }

    /// <param name="repairs">One for each actor</param>
    /// <param name="result">What the caller gets. Made here, so that a caller awaiting this resumes straight after its last check for changes</param>
    private static async Task<T> PrepareAllAsync<T>(IReadOnlyList<(ExportEntry Actor, ExportEntry Level)> actors,
        Func<ActorDeletionPlan[], IReadOnlyList<DeletionRepairs>> repairs, Func<ActorDeletionPlan[], T> result)
    {
        ActorDeletionPlan[] Inspected() => actors.Select(a => Inspect(a.Actor, a.Level)).ToArray();
        if (!MEPackageHandler.GlobalSharedCacheEnabled)
        {
            var inspected = Inspected();
            CheckReferences(inspected, repairs(inspected));
            return result(inspected);
        }
        var packages = actors.Select(a => a.Actor.FileRef).Distinct().ToArray();
        int[] changes = packages.Select(p => p.ChangeCount).ToArray();
        bool Changed() => !packages.Select(p => p.ChangeCount).SequenceEqual(changes);
        var plans = Inspected();
        var repaired = repairs(plans);
        try
        {
            await Task.Run(() => CheckReferences(plans, repaired));
        }
        catch (Exception) when (Changed())
        {
            //refused below, as the package changed
        }
        if (Changed()) throw new InvalidOperationException("The package changed during reference inspection. Retry deletion.");
        return result(plans);
    }

    /// <summary>
    /// Scans the whole package, including undrawn objects. A reference is allowed only if the commit takes it away: its referrer is trashed with the
    /// actor, or the referrer as the commit leaves it (<paramref name="repairs"/>' written properties, the Level without the actor among its Actors,
    /// BioWorldInfo without it in ClientDestroyedActorContent, which TrashEntries cleans, a decal without it among its receivers and Filter, a prefab instance
    /// with its entry blanked, an actor detached from it, or a component out of its shadow group) refers to none of the actor's entries. A commit's own check passes no written properties, so
    /// it sees what those edits actually wrote
    /// </summary>
    public static ActorDeletionPlan Prepare(ExportEntry actor, ExportEntry level, DeletionRepairs repairs = null)
    {
        var plan = Inspect(actor, level);
        CheckReferences([plan], repairs);
        return plan;
    }

    /// <summary>Adds what blocks each of <paramref name="plans"/> (<see cref="Inspect"/>ed) because of references to it, from one scan of each package</summary>
    internal static void CheckReferences(IReadOnlyList<ActorDeletionPlan> plans, DeletionRepairs repairs) => CheckReferences(plans, plans.Select(_ => repairs).ToArray());

    private static void CheckReferences(IReadOnlyList<ActorDeletionPlan> plans, IReadOnlyList<DeletionRepairs> repairs)
    {
        var references = plans.SelectMany(p => p.Entries).GroupBy(e => e.FileRef)
            .SelectMany(package => package.ToList().GetEntriesThatReferenceThese())
            .ToDictionary(r => r.Key, r => r.Value);
        for (int i = 0; i < plans.Count; i++) plans[i].CheckReferences(references, repairs[i] ?? DeletionRepairs.None);
    }

    /// <summary>The actor's owned entries, and what blocks deleting it besides references to them</summary>
    internal static ActorDeletionPlan Inspect(ExportEntry actor, ExportEntry level)
    {
        var blockers = new List<string>();
        var collection = actor is null ? null : CollectionOf(actor);
        // A collection's member is in the level as its collection is
        var levelActor = collection ?? actor;
        if (actor is null || level is null || actor.FileRef != level.FileRef || levelActor.Parent != level
            || actor.IsDefaultObject || actor.IsArchetype || actor.IsClass || !Supports(actor) || level.ClassName != "Level")
            throw new InvalidOperationException("This is not a supported level actor instance.");
        if (actor.FileRef.GetEntry(actor.UIndex) != actor || level.FileRef.GetEntry(level.UIndex) != level
            || actor.IsTrash() || levelActor.IsTrash())
            throw new InvalidOperationException("The actor or its Level was removed or replaced.");
        var entries = CollectOwnedEntries(actor, collection is not null, blockers);
        if (collection is not null && MemberTransform(actor) is null)
            blockers.Add($"{collection.ObjectName.Instanced} must list it once, with a transform for each of its components.");
        var binary = level.GetBinaryData<Level>();
        if (!binary.Actors.Contains(levelActor.UIndex)) blockers.Add("The actor no longer belongs to its Level.Actors array.");
        if (binary.Actors.Take(2).Contains(levelActor.UIndex)) blockers.Add("Reserved leading Level.Actors entries cannot be deleted.");
        var worldInfo = binary.Actors.Count > 0 ? actor.FileRef.GetEntry(binary.Actors[0]) as ExportEntry : null;
        if (level.InstancedFullPath != "TheWorld.PersistentLevel" || worldInfo?.ClassName != "BioWorldInfo")
            blockers.Add("Deletion requires a PersistentLevel with a valid leading BioWorldInfo entry.");
        var destroyedContentProperties = worldInfo?.GetProperties().Where(p => p.Name == "ClientDestroyedActorContent").ToArray();
        bool canCleanDestroyedContent = destroyedContentProperties is [ArrayProperty<ObjectProperty>];
        return new ActorDeletionPlan(actor, level, collection, entries, blockers, worldInfo, canCleanDestroyedContent);
    }

    /// <param name="references">For each of <see cref="Entries"/>, what refers to it and where (which is only reported)</param>
    private void CheckReferences(Dictionary<IEntry, Dictionary<IEntry, List<string>>> references, DeletionRepairs repairs)
    {
        var ownership = Entries.Cast<IEntry>().ToHashSet();
        var targets = Entries.Select(e => e.UIndex).ToHashSet();
        var referrers = Entries.SelectMany(target => references[target].Keys).Where(r => !ownership.Contains(r) && !repairs.Trashed.Contains(r)).Distinct();
        //(the scan reads exports, so only they refer to anything)
        foreach (var referrer in referrers.Cast<ExportEntry>())
        {
            if (IsRepaired(referrer, targets, repairs, out var binary, out bool attachment, out bool shadowGroup))
            {
                cleansDestroyedContent |= referrer == worldInfo;
                if (shadowGroup) shadowGrouped.Add(referrer);
                if (attachment)
                {
                    attachments.Add(referrer);
                    if (referrer.GetProperty<ObjectProperty>("Base") is { } @base && targets.Contains(@base.Value)) DetachedActors++;
                }
                if (binary is PrefabInstance) prefabs.Add(referrer);
                if (IsDecal(referrer))
                {
                    decals.Add(referrer);
                    if (LeftEmpty(referrer, targets, repairs)) decalsLeftEmpty.Add(referrer);
                }
                continue;
            }
            foreach (var target in Entries)
            {
                foreach (string field in references[target].GetValueOrDefault(referrer) ?? [])
                {
                    string reference = $"{field} references {target.InstancedFullPath}";
                    if (!referencesByReferrer.TryGetValue(referrer, out var fromReferrer)) referencesByReferrer[referrer] = fromReferrer = [];
                    fromReferrer.Add(reference);
                }
            }
        }
    }

    /// <summary>Whether the commit takes away all of <paramref name="referrer"/>'s references to <paramref name="targets"/></summary>
    /// <param name="binary">Its binary as the commit leaves it, null if that's unchanged</param>
    /// <param name="attachment">Whether that's by taking them out of its attachment</param>
    /// <param name="shadowGroup">Whether that's by taking it, a component, out of one's shadow group</param>
    private bool IsRepaired(ExportEntry referrer, IReadOnlySet<int> targets, DeletionRepairs repairs, out ObjectBinary binary, out bool attachment, out bool shadowGroup)
    {
        //no well-formed export is unreadable: one that is means the file is broken, and can't be edited safely
        try
        {
            return TryRepair(referrer, targets, repairs, out var properties, out binary, out attachment, out shadowGroup)
                   && !referrer.WouldReferenceAny(targets, properties, binary);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            throw new InvalidOperationException($"#{referrer.UIndex} {referrer.InstancedFullPath} can't be read, so this file can't be edited safely: {error.Message}", error);
        }
    }

    /// <summary>
    /// <paramref name="referrer"/> as the commit leaves it, if the commit changes it: its properties and binary, null for what's unchanged
    /// </summary>
    private bool TryRepair(ExportEntry referrer, IReadOnlySet<int> targets, DeletionRepairs repairs, out PropertyCollection properties, out ObjectBinary binary,
        out bool attachment, out bool shadowGroup)
    {
        properties = null;
        binary = null;
        attachment = shadowGroup = false;
        if (referrer == Collection)
        {
            (properties, binary) = WithoutMembers(Collection, new HashSet<int> { Actor.UIndex });
            return true;
        }
        if (referrer == Level)
        {
            var level = Level.GetBinaryData<Level>();
            level.Actors.RemoveAll(i => i == Actor.UIndex);
            binary = level;
            return true;
        }
        if (referrer == worldInfo && canCleanDestroyedContent)
        {
            properties = worldInfo.GetProperties();
            //as TrashEntries cleans it
            properties.GetProp<ArrayProperty<ObjectProperty>>("ClientDestroyedActorContent").RemoveAll(o => targets.Contains(o.Value));
            return true;
        }
        if (IsDecal(referrer))
        {
            (properties, binary) = WithoutReceivers(referrer, targets);
            return true;
        }
        //(an actor too, so it can also be attached)
        if (referrer.IsA("PrefabInstance") && referrer.GetBinaryData<PrefabInstance>().ArchetypeToInstanceMap.Any(p => targets.Contains(p.Value)))
            binary = WithoutPrefabMembers(referrer, targets);
        bool written = repairs.WrittenProperties.TryGetValue(referrer, out properties);
        properties = written ? properties.DeepClone() : referrer.GetProperties();
        if (referrer.IsA("PrimitiveComponent")) return (shadowGroup = WithoutShadowParent(properties, targets)) || written;
        //only an actor's Base and Attached are attachment: any other object that has them stays a blocker that names it
        if (!referrer.IsA("Actor")) return written;
        attachment = WithoutAttachment(properties, targets);
        return written || attachment || binary is not null;
    }

    /// <summary>
    /// Takes a primitive component's <paramref name="properties"/> out of the shadow group of one of <paramref name="entries"/>, if they're in it, so that it
    /// casts its own shadow. UPrimitiveComponent::Attach puts a component of a bShadowParented actor in its Base's group, and Detach takes it out; in shipping
    /// levels, a ShadowParent outside a component's own actor is always one of its Base's components, or its BaseSkelComponent, so the commit also detaches
    /// its actor. It's written as blank, as attachment is, in case it would be inherited
    /// </summary>
    /// <returns>Whether that changed anything</returns>
    private static bool WithoutShadowParent(PropertyCollection properties, IReadOnlySet<int> entries)
    {
        if (properties.GetProp<ObjectProperty>("ShadowParent") is not { } parent || !entries.Contains(parent.Value)) return false;
        properties.AddOrReplaceProp(new ObjectProperty(0, "ShadowParent"));
        return true;
    }

    /// <summary>
    /// Takes <paramref name="entries"/> out of an actor's <paramref name="properties"/>' attachment, as AActor::SetBase does: from its Attached, and if its
    /// Base is one of them, off it. Its Location is its place in the world, so it stays where it is. They're written as blank, since a removed value could be
    /// inherited from an archetype (a prefab member's Base is its archetype's, to another archetype), and all three together, since each could be
    /// </summary>
    /// <returns>Whether that changed anything</returns>
    internal static bool WithoutAttachment(PropertyCollection properties, IReadOnlySet<int> entries)
    {
        bool changed = properties.GetProp<ArrayProperty<ObjectProperty>>("Attached")?.RemoveAll(o => entries.Contains(o.Value)) == true;
        if (properties.GetProp<ObjectProperty>("Base") is not { } @base || !entries.Contains(@base.Value)) return changed;
        properties.AddOrReplaceProp(new ObjectProperty(0, "Base"));
        properties.AddOrReplaceProp(new ObjectProperty(0, "BaseSkelComponent"));
        properties.AddOrReplaceProp(new NameProperty("None", "BaseBoneName"));
        return true;
    }

    /// <summary>
    /// A prefab instance's binary with <paramref name="members"/>' entries blanked. An entry is kept, as APrefabInstance::UpdatePrefabInstance wants it: one
    /// that's in the map but blank is a member that was deleted and stays deleted, where a missing one is a member to create
    /// </summary>
    private static PrefabInstance WithoutPrefabMembers(ExportEntry instance, IReadOnlySet<int> members)
    {
        var binary = instance.GetBinaryData<PrefabInstance>();
        var map = new UMultiMap<int, int>(binary.ArchetypeToInstanceMap.Count);
        foreach (var (archetype, member) in binary.ArchetypeToInstanceMap) map.Add(archetype, members.Contains(member) ? 0 : member);
        binary.ArchetypeToInstanceMap = map;
        return binary;
    }

    private static bool IsDecal(ExportEntry export) => export.IsA("DecalComponent");

    // A decal's surfaces are in its binary. DecalReceivers can list components it has none on, which it doesn't draw on
    private static bool LeftEmpty(ExportEntry decal, IReadOnlySet<int> targets, DeletionRepairs repairs)
    {
        var receivers = decal.GetBinaryData<DecalComponent>().StaticReceivers;
        return receivers.Any(r => targets.Contains(r.Component))
            && receivers.All(r => targets.Contains(r.Component) || repairs.Trashed.Contains(decal.FileRef.GetEntry(r.Component)));
    }

    /// <summary>
    /// The actors attached to <paramref name="actor"/>, and to them in turn, that could be deleted with it. One that can't be, or is in <paramref name="refused"/>,
    /// keeps what's attached to it
    /// </summary>
    public static IReadOnlyList<ActorProxy> AttachedActors(ActorProxy actor, IReadOnlySet<ActorProxy> refused = null)
    {
        var found = new List<ActorProxy>();
        void Add(ActorProxy parent)
        {
            //(a Base cycle would come back round to what's already found)
            foreach (var attached in parent.Attached.Where(a => a != actor && refused?.Contains(a) != true && a.DeleteRefusal is null && !found.Contains(a)))
            {
                found.Add(attached);
                Add(attached);
            }
        }
        Add(actor);
        return found;
    }

    /// <summary>
    /// The decals deleting what <paramref name="plans"/> delete, with <paramref name="actor"/>, would leave with nothing to project onto, which could be
    /// deleted too. A decal with an uncommitted reprojection lands where it shows, which isn't what its binary says: it may have moved off the actor, or
    /// onto it from another package
    /// </summary>
    public static IReadOnlyList<DecalActorProxy> EmptiedDecals(ActorProxy actor, params ActorDeletionPlan[] plans)
    {
        var deleted = plans.SelectMany(p => p.Entries).ToHashSet();
        var gone = deleted.Concat(actor.OwningFile.PendingDeletions.SelectMany(d => d.Entries)).ToHashSet();
        //(a new decal, a draft, has no receivers to lose: it's projected again without what's going)
        return actor.Editor.RenderContext.DrawList_3D.OfType<DecalActorProxy>().Where(d => !d.IsDraft && d.DecalComponent is { } component && !deleted.Contains(d.Export) && Refusal(d) is null
            && (component.HasReprojectedReceivers
                ? component.Receivers.Any(deleted.Contains) && component.Receivers.All(gone.Contains)
                : plans.Any(p => p.DecalsLeftEmpty.Contains(component.Export)))).ToArray();
    }

    /// <summary>
    /// A decal's properties and binary without <paramref name="entries"/>: their receivers, and their entries in its Filter. It keeps its other surfaces,
    /// with their baked lighting. A Filter of "only these" that's left empty lands on nothing, as a Filter listing nothing does
    /// </summary>
    private static (PropertyCollection Properties, DecalComponent Binary) WithoutReceivers(ExportEntry decal, IReadOnlySet<int> entries)
    {
        var properties = decal.GetProperties();
        properties.GetProp<ArrayProperty<StructProperty>>("DecalReceivers")?.RemoveAll(r => r.GetProp<ObjectProperty>("Component") is { } component && entries.Contains(component.Value));
        properties.GetProp<ArrayProperty<ObjectProperty>>("Filter")?.RemoveAll(o => entries.Contains(o.Value));
        var binary = decal.GetBinaryData<DecalComponent>();
        binary.StaticReceivers = binary.StaticReceivers.Where(r => !entries.Contains(r.Component)).ToArray();
        return (properties, binary);
    }

    /// <summary>A collection's properties and binary without <paramref name="members"/>: their entries in its component list, and their transforms</summary>
    private static (PropertyCollection Properties, StaticCollectionActor Binary) WithoutMembers(ExportEntry collection, IReadOnlySet<int> members)
    {
        var binary = (StaticCollectionActor)ObjectBinary.From(collection);
        var properties = collection.GetProperties();
        var list = properties.GetProp<ArrayProperty<ObjectProperty>>(binary.ComponentPropName);
        //(an empty or missing list has no binary to read: nothing to remove)
        if (list is null || binary.Components is null) return (properties, binary);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!members.Contains(list[i].Value)) continue;
            list.RemoveAt(i);
            binary.Components.RemoveAt(i);
            binary.LocalToWorldTransforms.RemoveAt(i);
        }
        return (properties, binary);
    }

    /// <summary>Takes <paramref name="members"/> out of <paramref name="collection"/>'s component list and transforms</summary>
    internal static void RemoveMembers(ExportEntry collection, IReadOnlySet<int> members)
    {
        var (properties, binary) = WithoutMembers(collection, members);
        // Its binary has a transform for each entry of its component list as its properties have it, so they're written first
        collection.WriteProperties(properties);
        collection.WriteBinary(binary);
    }

    private static int Depth(IEntry entry)
    {
        int depth = 0;
        for (IEntry outer = entry.Parent; outer is not null; outer = outer.Parent) depth++;
        return depth;
    }

    internal void RequireAllowed()
    {
        if (Blockers.Count != 0) throw new ActorDeletionBlockedException(this);
    }

    /// <summary>A list for a message, one to a line, of a length that fits in one</summary>
    internal static string Listed(IReadOnlyCollection<string> lines)
    {
        const int shown = 10;
        return string.Join("\n", lines.Take(shown)) + (lines.Count > shown ? $"\n...and {lines.Count - shown} more" : "");
    }

    /// <summary>
    /// Deletes the planned actors, with one write of each Level and one TrashEntries of each package. Called only inside ActorCommit's
    /// PackageEditScope. Saving to disk remains a separate operation.
    /// </summary>
    internal static void Apply(IReadOnlyList<ActorDeletionPlan> plans)
    {
        //(the plans are Revalidate's, which has required each to be allowed)
        foreach (var plan in plans)
        {
            if (!plan.Actor.FileRef.IsInEditScope) throw new InvalidOperationException("Actor deletion requires a package edit scope.");
        }
        foreach (var level in plans.Where(p => p.Collection is null).GroupBy(p => p.Level))
        {
            var deleted = level.Select(p => p.Actor.UIndex).ToHashSet();
            var binary = level.Key.GetBinaryData<Level>();
            binary.Actors.RemoveAll(deleted.Contains);
            level.Key.WriteBinary(binary);
        }
        foreach (var collection in plans.Where(p => p.Collection is not null).GroupBy(p => p.Collection))
            RemoveMembers(collection.Key, collection.Select(p => p.Actor.UIndex).ToHashSet());
        foreach (var decal in plans.SelectMany(p => p.decals.Select(d => (Decal: d, Plan: p))).GroupBy(d => d.Decal, d => d.Plan))
        {
            var (properties, binary) = WithoutReceivers(decal.Key, decal.SelectMany(p => p.Entries).Select(e => e.UIndex).ToHashSet());
            decal.Key.WritePropertiesAndBinary(properties, binary);
        }
        foreach (var actor in plans.SelectMany(p => p.attachments.Select(a => (Actor: a, Plan: p))).GroupBy(a => a.Actor, a => a.Plan))
        {
            var properties = actor.Key.GetProperties();
            WithoutAttachment(properties, actor.SelectMany(p => p.Entries).Select(e => e.UIndex).ToHashSet());
            actor.Key.WriteProperties(properties);
        }
        foreach (var component in plans.SelectMany(p => p.shadowGrouped.Select(c => (Component: c, Plan: p))).GroupBy(c => c.Component, c => c.Plan))
        {
            var properties = component.Key.GetProperties();
            WithoutShadowParent(properties, component.SelectMany(p => p.Entries).Select(e => e.UIndex).ToHashSet());
            component.Key.WriteProperties(properties);
        }
        foreach (var prefab in plans.SelectMany(p => p.prefabs.Select(i => (Instance: i, Plan: p))).GroupBy(i => i.Instance, i => i.Plan))
            prefab.Key.WriteBinary(WithoutPrefabMembers(prefab.Key, prefab.SelectMany(p => p.Entries).Select(e => e.UIndex).ToHashSet()));
        foreach (var package in plans.GroupBy(p => p.Actor.FileRef))
        {
            // Materialize the order before trashing changes outers; descendants must go first.
            var trash = package.SelectMany(p => p.Entries).OrderByDescending(Depth).ToArray();
            EntryPruner.TrashEntries(package.Key, trash);
        }
    }
}
