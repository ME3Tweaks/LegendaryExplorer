using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Turns a collection's member back into the actor it was cooked from. UE3's cooker (UCookPackagesCommandlet::CookStaticMeshActors and
/// CookStaticLightActors) renames an actor's component into a collection and leaves the actor out of the package, so this makes an actor of the class
/// the component's archetype belongs to, and moves the same component under it, with what it owns. Its light and shadow maps, GUIDs and UIndex stay
/// as they are, so nothing needs rebaking and anything that refers to it still does. The world transform is unchanged too: a mesh's scale is on its
/// component (the cooker moved it there), and a light's in its matrix, which becomes the actor's
/// </summary>
public static class CollectionMemberUpgrade
{
    /// <summary>The actor a member was cooked from, which its archetype is a subobject of: a class default, or an archetype of that class</summary>
    private static IEntry Origin(ExportEntry member) => member.Archetype?.Parent;

    /// <summary>Why <paramref name="member"/> can't be marked for upgrading now, or null</summary>
    internal static string Refusal(CollectionActorComponentProxy member)
    {
        if (ActorDeletionPlan.RemovalRefusal(member) is { } refusal) return refusal;
        if (member.UpgradePending) return "Its upgrade is already pending.";
        if (Origin(member.Export) is not { } origin) return "Its archetype doesn't show what actor it came from.";
        bool remade = member is StaticLightCollectionComponentProxy ? LightActorProxy.StandaloneClasses.Contains(origin.ClassName) : origin.ClassName == "StaticMeshActor";
        if (!remade) return $"It came from a {origin.ClassName}, which can't be made again.";
        return ActorCreation.Refusal(member.OwningFile, origin.ClassName, member.Editor.RenderContext.PackageCache);
    }

    /// <summary>Marks <paramref name="member"/> for upgrading, as one undo step for the caller to record. Its package is untouched until Commit Changes</summary>
    public static IUndoAction Upgrade(CollectionActorComponentProxy member)
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
        var members = file.Actors.OfType<CollectionActorComponentProxy>().Where(m => m.UpgradePending).ToArray();
        if (members.Length == 0) return;
        var level = file.LevelExport.GetBinaryData<Level>();
        foreach (var member in members) MakeActor(member, file, level);
        file.LevelExport.WriteBinary(level);
        foreach (var collection in members.GroupBy(m => m.CollectionActorExport))
            ActorDeletionPlan.RemoveMembers(collection.Key, collection.Select(m => m.Export.UIndex).ToHashSet());
    }

    private static void MakeActor(CollectionActorComponentProxy member, OpenLevelFile file, Level level)
    {
        var (component, package) = (member.Export, file.Package);
        if (component.Parent != member.CollectionActorExport || ActorDeletionPlan.MemberTransform(component) is null)
            throw new InvalidOperationException($"{member.CollectionActorExport.ObjectName.Instanced} no longer lists {member.ActorName} once.");
        var origin = Origin(component);
        string className = origin.ClassName;
        if (ActorCreation.Refusal(file, className, member.Editor.RenderContext.PackageCache, out var actorClass) is { } refusal)
            throw new InvalidOperationException(refusal);
        var actor = ActorProxy.NewActorExport(package, file.LevelExport, package.GetNextIndexedName(className), ActorCreation.ImportOf(actorClass, package), level);
        //an actor whose archetype is its class default has none in its header, as placed actors do
        bool archetyped = origin.ObjectName.Instanced != $"Default__{className}";
        if (archetyped) actor.Archetype = origin;
        // What's left out is inherited. A class default's transform is the identity, but an archetype's needn't be (LE3's BioVFX_Env_Signs.Meshes.Armax_Sign_02
        // has a DrawScale3D), and the cooker already put the actor's whole scale on its component, so an archetyped actor states its own, even the identity
        PropertyCollection properties = [CommonStructs.Vector3Prop(member.Location, ActorProxy.LocationPropertyName(package.Game))];
        if (archetyped || !member.Rotation.IsZero) properties.Add(CommonStructs.RotatorProp(member.Rotation, "Rotation"));
        if (archetyped || member.DrawScale != 1f) properties.Add(new FloatProperty(member.DrawScale, "DrawScale"));
        if (archetyped || member.DrawScale3D != Vector3.One) properties.Add(CommonStructs.Vector3Prop(member.DrawScale3D, "DrawScale3D"));
        if (archetyped) properties.Add(CommonStructs.Vector3Prop(member.PrePivot, "PrePivot"));
        // A cooked actor's own Tag is gone: it takes its archetype's, or its class's name, as a new actor does
        else properties.Add(new NameProperty(className, "Tag"));
        // The one thing shipping collections hold of their actors': the cooker keeps meshes that can't be stepped up on together
        if (member.CollectionActorExport.GetProperty<BoolProperty>("bCanStepUpOn") is { } canStepUpOn) properties.Add(canStepUpOn);
        if (member is StaticLightCollectionComponentProxy) properties.Add(new ObjectProperty(component, "LightComponent"));
        else
        {
            properties.Add(new ObjectProperty(component, "StaticMeshComponent"));
            properties.Add(new ObjectProperty(component, "CollisionComponent"));
        }
        actor.WriteProperties(properties);
        component.Parent = actor;
    }

    private sealed class UpgradeAction(CollectionActorComponentProxy member) : IUndoAction
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
            member.UpgradePending = pending;
        }
    }
}
