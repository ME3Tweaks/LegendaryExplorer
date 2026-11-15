using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Turns a collection's member back into the actor it was cooked from. UE3's cooker (UCookPackagesCommandlet::CookStaticMeshActors and
/// CookStaticLightActors) renames an actor's component into a collection and leaves the actor out of the package, so this makes an actor of the class
/// the component's archetype belongs to, and moves the same component under it, with what it owns. Its light and shadow maps, GUIDs and UIndex stay
/// as they are, so nothing needs rebaking and anything that refers to it still does. The world transform is unchanged too: a mesh's scale is on its
/// component (the cooker moved it there), and a light's in its matrix, which becomes the actor's. An uncommitted copy of a member is upgraded the same way,
/// once its commit has made it one of its collection's members (<see cref="ActorCloning.MaterializeMember"/>, which comes first)
/// </summary>
public static class CollectionMemberUpgrade
{
    /// <summary>The actor a member was cooked from, which its archetype is a subobject of: a class default, or an archetype of that class</summary>
    internal static IEntry Origin(ExportEntry member) => member.Archetype?.Parent;

    /// <summary>Why <paramref name="member"/>, a collection's member or a copy of one (<see cref="ActorProxy.IsCollectionMember"/>), can't be marked for upgrading now, or null</summary>
    internal static string Refusal(ActorProxy member)
    {
        if (!member.IsCollectionMember) return "Only a collection's member can be upgraded to an actor.";
        if (ActorDeletionPlan.RemovalRefusal(member) is { } refusal) return refusal;
        if (member.UpgradePending) return "Its upgrade is already pending.";
        return ActorRefusal(member);
    }

    /// <summary>Why <paramref name="member"/> can't become an actor at all, or null: unlike <see cref="Refusal"/>, this doesn't depend on what the scene is doing now</summary>
    internal static string ActorRefusal(ActorProxy member) => ActorRefusal(member, member.OwningFile, out _);

    /// <summary>Why <paramref name="member"/> can't become an actor in <paramref name="file"/> (its own, or another a copy of it goes in), or null</summary>
    /// <param name="actorClass">The class of the actor it would be, if it can be one</param>
    internal static string ActorRefusal(ActorProxy member, OpenLevelFile file, out IEntry actorClass)
    {
        actorClass = null;
        return OriginRefusal(member, out var origin) ?? ActorCreation.Refusal(file, origin.ClassName, member.Editor.RenderContext.PackageCache, out actorClass);
    }

    /// <summary>Why <paramref name="member"/> can't become an actor in any file, or null: the actor it was cooked from can't be made again</summary>
    internal static string OriginRefusal(ActorProxy member) => OriginRefusal(member, out _);

    private static string OriginRefusal(ActorProxy member, out IEntry origin)
    {
        // A copy's component is its source's, archetype and all, until its commit clones it
        if ((origin = Origin(member.Export ?? member.CloneSource)) is null) return "Its archetype doesn't show what actor it came from.";
        bool remade = member is ILightProxy ? LightActorProxy.StandaloneClasses.Contains(origin.ClassName) : origin.ClassName == "StaticMeshActor";
        return remade ? null : $"It came from a {origin.ClassName}, which can't be made again.";
    }

    /// <summary>Marks <paramref name="member"/> for upgrading, as one undo step for the caller to record. Its package is untouched until Commit Changes</summary>
    public static IUndoAction Upgrade(ActorProxy member)
    {
        if (Refusal(member) is { } refusal) throw new InvalidOperationException(refusal);
        var action = new UpgradeAction(member);
        action.Redo();
        return action;
    }

    /// <summary>
    /// For a commit, after moved members' transforms are written: upgrades <paramref name="file"/>'s members marked for it, with one write of its Level and
    /// of each collection. Called only inside ActorCommit's PackageEditScope
    /// </summary>
    internal static void Apply(OpenLevelFile file)
    {
        //(only members, and copies of them, can be pending: Upgrade refuses anything else, and decals need only those)
        var members = file.Actors.Where(m => m.UpgradePending).ToArray();
        if (members.Length == 0) return;
        var level = file.LevelExport.GetBinaryData<Level>();
        foreach (var member in members) MakeActor(member, file, level);
        file.LevelExport.WriteBinary(level);
        foreach (var collection in members.GroupBy(m => m.MemberCollection))
            ActorDeletionPlan.RemoveMembers(collection.Key, collection.Select(m => m.Export.UIndex).ToHashSet());
        UpdateFilters(file.Package, members);
    }

    /// <summary>
    /// Lists <paramref name="members"/> in decals' Filters as the actors they are now. A Filter the commit wrote, here or moved here, lists a member by its
    /// component, since its actor didn't exist yet (<see cref="DecalFilterSettings.Write"/>). One that lists a collection means its meshes, so those that left it stay listed
    /// </summary>
    private static void UpdateFilters(IMEPackage package, ActorProxy[] members)
    {
        foreach (var decal in package.Exports.Where(e => e.IsA("DecalComponent")))
        {
            if (decal.GetProperty<ArrayProperty<ObjectProperty>>("Filter") is not { Count: > 0 } filter) continue;
            bool changed = false;
            foreach (var member in members)
            {
                if (filter.FirstOrDefault(entry => entry.Value == member.Export.UIndex) is { } listed) listed.Value = member.Export.idxLink;
                else if (filter.Any(entry => entry.Value == member.MemberCollection.UIndex)) filter.Add(new ObjectProperty(member.Export.idxLink));
                else continue;
                changed = true;
            }
            if (changed) decal.WriteProperty(filter);
        }
    }

    private static void MakeActor(ActorProxy member, OpenLevelFile file, Level level)
    {
        var (component, collection) = (member.Export, member.MemberCollection);
        if (ActorDeletionPlan.MembershipProblem(component, collection) is { } problem)
            throw new InvalidOperationException($"{member.ActorName} can't be upgraded. {problem}");
        if (ActorCreation.Refusal(file, Origin(component).ClassName, member.Editor.RenderContext.PackageCache, out var actorClass) is { } refusal)
            throw new InvalidOperationException(refusal);
        MakeActor(component, collection, file.LevelExport, level, actorClass, member is ILightProxy, member);
    }

    /// <summary>
    /// Makes an actor of <paramref name="actorClass"/> (the class <paramref name="component"/>'s archetype belongs to) around <paramref name="component"/>,
    /// one of <paramref name="collection"/>'s members, as one of <paramref name="levelExport"/>'s actors, placed where <paramref name="transform"/> is: the
    /// member's view, or a copy of it in another file (<see cref="ActorCloning"/>), which makes it in a snapshot of the member's package. Its collection
    /// still lists it: the caller takes it out, or doesn't need to
    /// </summary>
    /// <param name="level"><paramref name="levelExport"/>'s, which the actor is added to the Actors of. The caller writes it</param>
    internal static ExportEntry MakeActor(ExportEntry component, ExportEntry collection, ExportEntry levelExport, Level level, IEntry actorClass, bool isLight,
        ActorProxy transform)
    {
        var package = component.FileRef;
        var origin = Origin(component);
        string className = origin.ClassName;
        var actor = ActorProxy.NewActorExport(package, levelExport, package.GetNextIndexedName(className), ActorCreation.ImportOf(actorClass, package), level);
        //an actor whose archetype is its class default has none in its header, as placed actors do
        bool archetyped = origin.ObjectName.Instanced != $"Default__{className}";
        if (archetyped) actor.Archetype = origin;
        // What's left out is inherited. A class default's transform is the identity, but an archetype's needn't be (LE3's BioVFX_Env_Signs.Meshes.Armax_Sign_02
        // has a DrawScale3D), and the cooker already put the actor's whole scale on its component, so an archetyped actor states its own, even the identity
        PropertyCollection properties = [CommonStructs.Vector3Prop(transform.Location, ActorProxy.LocationPropertyName(package.Game))];
        if (archetyped || !transform.Rotation.IsZero) properties.Add(CommonStructs.RotatorProp(transform.Rotation, "Rotation"));
        if (archetyped || transform.DrawScale != 1f) properties.Add(new FloatProperty(transform.DrawScale, "DrawScale"));
        if (archetyped || transform.DrawScale3D != Vector3.One) properties.Add(CommonStructs.Vector3Prop(transform.DrawScale3D, "DrawScale3D"));
        if (archetyped) properties.Add(CommonStructs.Vector3Prop(transform.PrePivot, "PrePivot"));
        // A cooked actor's own Tag is gone: it takes its archetype's, or its class's name, as a new actor does
        else properties.Add(new NameProperty(className, "Tag"));
        // The one thing shipping collections hold of their actors': the cooker keeps meshes that can't be stepped up on together
        if (collection.GetProperty<BoolProperty>("bCanStepUpOn") is { } canStepUpOn) properties.Add(canStepUpOn);
        if (isLight) properties.Add(new ObjectProperty(component, "LightComponent"));
        else
        {
            properties.Add(new ObjectProperty(component, "StaticMeshComponent"));
            properties.Add(new ObjectProperty(component, "CollisionComponent"));
        }
        actor.WriteProperties(properties);
        component.Parent = actor;
        return actor;
    }

    private sealed class UpgradeAction(ActorProxy member) : IUndoAction
    {
        public string Description => $"Upgrade {member.ActorName} to an actor";
        public bool IsOrphaned => member.IsDisposed;
        public IReadOnlyCollection<ActorId> Subjects => [member.Id];
        public void Undo() => SetPending(false);
        public void Redo() => SetPending(true);

        // Not in a read-only file: a commit leaves it out, as it does the file's new and deleted actors (see OpenLevelFile.IsReadOnly)
        private void SetPending(bool pending)
        {
            if (member.OwningFile is not { IsReadOnly: false }) throw new InvalidOperationException("Its file is read-only or closed.");
            member.UpgradeRequested = pending;
        }
    }
}
