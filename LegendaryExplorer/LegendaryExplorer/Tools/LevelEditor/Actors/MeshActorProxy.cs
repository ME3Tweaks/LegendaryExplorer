using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

public sealed class StaticMeshActorProxy : MeshActorProxy
{
    public StaticMeshActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport) { }

    internal StaticMeshActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, AssetReference mesh, ExportEntry componentTemplate)
        : base(context, draft, properties, mesh, componentTemplate) { }
}

/// <summary>An actor drawn by its StaticMeshComponent: a static mesh actor, or one that can move (<see cref="DynamicSMActorProxy"/>)</summary>
public abstract class MeshActorProxy : ActorProxy
{
    public StaticMeshComponentProxy StaticMeshComponent;
    protected MeshActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref StaticMeshComponent);
        IsVolumetricMesh = StaticMeshComponent?.IsVolumetric == true;
    }

    //a draft's: the class default's component its own is made from, and the mesh it draws
    private readonly ExportEntry componentTemplate;
    private AssetReference mesh;

    /// <summary>
    /// A new mesh actor (a draft) drawing <paramref name="mesh"/>, an entry of its package or another file's mesh. Its component is drawn as the
    /// commit makes it: from <paramref name="componentTemplate"/>'s values with the mesh, and the material overrides it inherits from the template. Its
    /// package is untouched until then. Its light environment, if it has one, is its class default's (its template's references aren't read)
    /// </summary>
    private protected MeshActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, AssetReference mesh, ExportEntry componentTemplate)
        : base(context, draft, properties)
    {
        this.componentTemplate = componentTemplate;
        this.mesh = mesh;
        var cache = context.PackageCache;
        var componentProperties = EffectiveLightingProperties.ReadValues(componentTemplate, cache);
        var (materials, materialPackage) = EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(componentTemplate, "Materials", cache);
        var assets = new PreviewAssets(mesh, [.. (materials ?? Enumerable.Empty<ObjectProperty>()).Select(m => AssetReference.Of(m.ResolveToEntry(materialPackage), Pcc, cache))]);
        // None already exists in every package. Pending properties bypass serialization, so even the package's name table stays untouched
        var preview = new ExportEntry(Pcc, null, "None", new byte[8], binary: LegendaryExplorerCore.Unreal.BinaryConverters.StaticMeshComponent.Create());
        StaticMeshComponent = new StaticMeshComponentProxy(context.RenderContext, preview, this, componentProperties, previewAssets: assets)
        {
            ActorPropertyName = nameof(StaticMeshComponent)
        };
        Components.Add(StaticMeshComponent);
        IsVolumetricMesh = StaticMeshComponent.IsVolumetric;
        //a draft is wholly uncommitted
        UpdateDirty();
    }

    private protected override bool WritesAllValuesWhenCreated => true;

    internal override AssetReference DraftAsset => componentTemplate is not null ? mesh : null;

    //a draft's: the mesh its commit makes its component with (one chosen since it was placed, or the one it was placed with)
    private AssetReference CommittedMesh => StaticMeshComponent.ChosenMesh ?? mesh;

    internal override IEnumerable<AssetReference> AssetsToCopy => IsDraft && componentTemplate is not null ? [CommittedMesh, .. ChosenMaterialAssets] : base.AssetsToCopy;

    //the preview, already drawing the asset, is left as it is
    internal override Action UseCopiedAssets(Func<AssetReference, AssetReference> copyOf)
    {
        var restore = base.UseCopiedAssets(copyOf);
        if (!IsDraft || componentTemplate is null || StaticMeshComponent.ChosenMesh is not null) return restore;
        var placed = mesh;
        mesh = copyOf(mesh);
        return () =>
        {
            mesh = placed;
            restore();
        };
    }

    //a new actor's preview draws what its commit wrote, the level's copy of another file's mesh, until its view is made again from its export
    public override void MarkClean()
    {
        if (Materialized && componentTemplate is not null) StaticMeshComponent.AcceptWrittenMesh(CommittedMesh);
        base.MarkClean();
    }

    //a draft's mesh lighting is written to the component its commit makes
    internal override IEnumerable<MeshComponentProxy> LightingMeshes => componentTemplate is null ? base.LightingMeshes : [];

    /// <summary>
    /// For a draft's commit, once it's written: the component it made, which its StaticMeshComponent now refers to. Null before, and once the commit is
    /// abandoned (its properties are put back)
    /// </summary>
    internal ExportEntry CommittedComponent => Materialized && Properties.GetProp<ObjectProperty>(nameof(StaticMeshComponent)) is { } property
        ? property.ResolveToEntry(Pcc) as ExportEntry : null;

    public override void CommitChanges(PackageCache packageCache = null)
    {
        // Its export was just made (Materialize): its component is made with it, as shipping levels' are (see NewComponent), with a StaticMeshComponent
        // binary (no light-maps until a bake). The actor refers to it as its StaticMeshComponent and CollisionComponent; neither has a Components array
        if (Materialized && componentTemplate is not null)
        {
            // Chosen materials are its own Materials (an empty list too: its template's overrides are what it differs from)
            PropertyCollection values = [new ObjectProperty(CommittedMesh.EntryIn(Pcc), "StaticMesh")];
            if (StaticMeshComponent.ChosenMaterials is { } materials) values.Add(StaticMeshChange.MaterialsProperty(materials, Pcc));
            var component = NewComponent(componentTemplate, values,
                LegendaryExplorerCore.Unreal.BinaryConverters.StaticMeshComponent.Create());
            StaticMeshComponent.ExistingLighting?.Write(component);
            Properties.AddOrReplaceProp(new ObjectProperty(component, nameof(StaticMeshComponent)));
            Properties.AddOrReplaceProp(new ObjectProperty(component, "CollisionComponent"));
        }
        base.CommitChanges(packageCache);
    }
}

//interpactor, placeables
public sealed class DynamicSMActorProxy : MeshActorProxy
{
    public DynamicSMActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport) { }

    /// <summary>
    /// A new InterpActor (a draft), made as a static mesh actor is. It has no light environment export of its own, so it has its class default's
    /// MyLightEnvironment, which is disabled: as 85-94% of LE1-LE3's shipping ones do (the rest have one of their own, mostly enabled)
    /// </summary>
    internal DynamicSMActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, AssetReference mesh, ExportEntry componentTemplate)
        : base(context, draft, properties, mesh, componentTemplate) { }
}
