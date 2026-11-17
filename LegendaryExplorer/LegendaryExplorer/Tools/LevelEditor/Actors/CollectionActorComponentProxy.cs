using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

public abstract class CollectionActorComponentProxy : ActorProxy
{
    public ExportEntry CollectionActorExport { get; }
    public override ExportEntry CollectionOwner => CollectionActorExport;

    /// <summary>
    /// The transform stored in the collection's binary, as of the last load or commit
    /// </summary>
    public Matrix4x4 SavedLocalToWorld { get; private set; }

    internal override ExportEntry MemberCollection => CollectionActorExport;
    private protected override string EditKind => UpgradePending ? "Upgrade" : base.EditKind;
    // Subclasses add their own changes to the member's (HasMemberChanges), so that what's more than an upgrade is known from one place
    protected sealed override bool HasUncommittedChanges() => HasMemberChanges() || UpgradePending;
    protected virtual bool HasMemberChanges() => base.HasUncommittedChanges();
    internal sealed override bool HasChangesBesidesUpgrade => HasMemberChanges();

    protected CollectionActorComponentProxy(IActorEditorContext context, StaticCollectionActor collectionActor, ExportEntry componentActor, int index) : base(componentActor)
    {
        Editor = context;
        CollectionActorExport = collectionActor.Export;

        LocalToWorld = SavedLocalToWorld = collectionActor.LocalToWorldTransforms[index];
        (location, drawScale3D, rotation) = collectionActor.GetDecomposedTransformationForIndex(index);
        if (drawScale3D.X == drawScale3D.Y && drawScale3D.X == drawScale3D.Z)
        {
            drawScale = drawScale3D.X;
            drawScale3D = Vector3.One;
        }
        _cleanSnapshot = SnapshotTransform();
    }

    public override void CommitChanges(PackageCache packageCache = null)
    {
        throw new InvalidOperationException($"Cannot be called on a {nameof(CollectionActorComponentProxy)}.");
    }

    public void CommitChanges(StaticCollectionActor collectionActor)
    {
        if (!(collectionActor.Components.FindIndex(uIdx => uIdx == Export.UIndex) is int idx and >= 0))
        {
            throw new ArgumentException("Does not contain this component", nameof(collectionActor));
        }
        collectionActor.LocalToWorldTransforms[idx] = ComposedLocalToWorld;
    }

    private Matrix4x4 ComposedLocalToWorld => ActorUtils.ComposeLocalToWorld(Location, Rotation, DrawScale * DrawScale3D, PrePivot);

    public override void MarkClean()
    {
        //the transform the commit wrote, if it was moved
        if (IsTransformDirty) SavedLocalToWorld = ComposedLocalToWorld;
        base.MarkClean();
    }

    public override bool TestUIndexes(HashSet<int> uIndexes)
    {
        return base.TestUIndexes(uIndexes) || uIndexes.Contains(CollectionActorExport.UIndex);
    }
}

public class StaticMeshComponentActorProxy : CollectionActorComponentProxy
{
    public StaticMeshComponentActorProxy(IActorEditorContext context, ExportEntry smcExport, StaticMeshCollectionActor smca, int smcaIndex) : base(context, smca, smcExport, smcaIndex)
    {
        var staticMeshComponentProxy = PrimitiveComponentProxy.Create(context.RenderContext, smcExport, this);
        staticMeshComponentProxy.ActorPropertyName = $"{smca.ComponentPropName}[{smcaIndex}]";
        Components.Add(staticMeshComponentProxy);
        IsVolumetricMesh = (staticMeshComponentProxy as StaticMeshComponentProxy)?.IsVolumetric ?? false;
    }
}
