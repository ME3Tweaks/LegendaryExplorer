using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>A retained, inspectable export. A marker represents a position, never an implemented scene effect.</summary>
public class EditorActorProxy : ActorProxy
{
    private ActorCoverage coverage;
    private readonly string category;
    private bool hasPosition;
    private Vector3 extent;
    private readonly HashSet<ExportEntry> dependencies = [];
    private readonly ActorClassInfo info;
    private readonly IActorEditorContext previewContext;
    private PrimitiveComponentProxy boundsComponent;
    private bool boundsLoaded;
    private ExportEntry failedCollectionOwner;

    public override string Category => category;
    public override ActorCoverage Coverage => coverage;
    public override bool CanEditTransform => hasPosition && info is { DataOnly: false };
    public override bool HasSpatialRepresentation => hasPosition;
    public override int HitPriority => IHitProxy.WireFramePriority;
    public override ExportEntry CollectionOwner => failedCollectionOwner;
    private protected override bool WritesAllValuesWhenCreated => true;

    internal EditorActorProxy(IActorEditorContext context, ExportEntry export, ActorClassInfo info) : base(export)
    {
        Editor = context;
        category = info.Category;
        this.info = info;
        previewContext = context;
        ReadPreview();
    }

    /// <summary>A new marker actor (a draft), drawn from its own values alone. Its package is untouched until a commit writes it</summary>
    internal EditorActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, ActorClassInfo info)
        : base(context, draft, properties)
    {
        category = info.Category;
        this.info = info;
        previewContext = context;
        hasPosition = true;
        coverage = new(ActorRepresentation.EditorMarker, info.Reason);
        //a draft is wholly uncommitted
        UpdateDirty();
    }

    private void ReadPreview()
    {
        var export = Export;
        var context = previewContext;
        hasPosition = false;
        extent = location = prePivot = Vector3.Zero;
        rotation = default;
        drawScale = 1;
        drawScale3D = Vector3.One;
        dependencies.Clear();
        Components.DisposeAndClear();
        boundsComponent = null;
        boundsLoaded = false;
        if (info.DataOnly)
        {
            coverage = new(ActorRepresentation.DataOnly, info.Reason);
            return;
        }
        // Inherited transforms are values, so imported archetypes can safely supply them.
        // Do not inherit a CDO's arbitrary default Location as evidence that metadata has a useful position.
        Properties = export.GetCondensedProperties(context.PackageCache, resolveImports: true, mergeStructs: true);
        Tag = Properties.GetProp<NameProperty>("Tag")?.Value ?? default;
        for (var ancestor = EffectiveLightingProperties.Resolve(export.Archetype, context.PackageCache);
            ancestor is not null && dependencies.Add(ancestor);
            ancestor = EffectiveLightingProperties.Resolve(ancestor.Archetype, context.PackageCache)) { }
        var position = Properties.GetProp<StructProperty>("Location");
        if (position is not null)
        {
            location = CommonStructs.GetVector3(position);
            hasPosition = float.IsFinite(location.X) && float.IsFinite(location.Y) && float.IsFinite(location.Z);
            if (Properties.GetProp<StructProperty>("Rotation") is { } r) rotation = CommonStructs.GetRotator(r);
            if (Properties.GetProp<StructProperty>("DrawScale3D") is { } s) drawScale3D = CommonStructs.GetVector3(s);
            drawScale = Properties.GetProp<FloatProperty>("DrawScale")?.Value ?? 1;
            if (Properties.GetProp<StructProperty>("PrePivot") is { } pivot) prePivot = CommonStructs.GetVector3(pivot);
        }
        UpdateLocalToWorld();
        _cleanSnapshot = SnapshotTransform();
        var representation = hasPosition ? ActorRepresentation.EditorMarker : ActorRepresentation.Unsupported;
        coverage = new(representation, info.Reason + (!hasPosition
            ? " No explicit or archetyped position was found; no viewport marker is drawn."
            : info.Category == "Unsupported" ? " Generic position marker only." : ""));
    }

    private void EnsureBounds()
    {
        //a draft has only its own values, and no cylinder of its own
        if (boundsLoaded || !hasPosition || info is null || IsDraft) return;
        boundsLoaded = true;
        // Bounds are only used for selection/focus. Avoid loading class-default packages for every hidden node.
        try
        {
            // Cylinder bounds are optional and selected-only. Never substitute class-default editor meshes.
            if (hasPosition && EffectiveLightingProperties.ResolveReference(Export, "CylinderComponent", previewContext.PackageCache, Properties) is { } cylinder)
            {
                var props = EffectiveLightingProperties.Read(cylinder, previewContext.PackageCache);
                float radius = props.GetProp<FloatProperty>("CollisionRadius")?.Value ?? 0;
                float height = props.GetProp<FloatProperty>("CollisionHeight")?.Value ?? 0;
                if (float.IsFinite(radius) && float.IsFinite(height) && radius >= 0 && height >= 0)
                    extent = new Vector3(radius, radius, height);
                boundsComponent = new HelperBoundsProxy(previewContext.RenderContext, cylinder, this);
                Components.Add(boundsComponent);
                dependencies.Add(cylinder);
                TrackBoundsAncestors(cylinder);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            coverage = coverage with { Reason = coverage.Reason + $" Bounds could not be loaded: {e.Message}" };
            NotifyCoverageChanged();
        }
    }

    private void TrackBoundsAncestors(ExportEntry cylinder)
    {
        var cache = previewContext.PackageCache;
        void Track(ExportEntry ancestor)
        {
            var seen = new HashSet<ExportEntry>();
            while (ancestor is not null && seen.Add(ancestor))
            {
                dependencies.Add(ancestor);
                ancestor = EffectiveLightingProperties.Resolve(ancestor.Archetype, cache);
            }
        }
        Track(EffectiveLightingProperties.Resolve(cylinder.Archetype, cache));
        Track(EffectiveLightingProperties.ClassDefault(cylinder, cache));
    }

    private EditorActorProxy(IActorEditorContext context, ExportEntry export, string failure) : base(export)
    {
        Editor = context;
        category = "Unsupported";
        coverage = new(ActorRepresentation.Unsupported, $"Could not load actor preview: {failure}");
    }

    internal static EditorActorProxy Failed(IActorEditorContext context, ExportEntry export, string failure, ExportEntry collectionOwner = null)
        => new(context, export, failure) { failedCollectionOwner = collectionOwner };

    public override void CommitChanges(PackageCache packageCache = null)
    {
        // A new actor's own values are all it has: written whole, with its transform
        if (Materialized)
        {
            base.CommitChanges(packageCache);
            return;
        }
        if (IsReadOnly || !IsTransformDirty) return;
        // Write only edited transforms to the instance. Condensed properties contain inherited
        // gameplay values that must not be flattened into the actor when moving its helper.
        var props = Export.GetProperties();
        if (Location != _cleanSnapshot.Location)
            props.AddOrReplaceProp(CommonStructs.Vector3Prop(Location, LocationPropertyName(Export.Game)));
        if (Rotation != _cleanSnapshot.Rotation)
            props.AddOrReplaceProp(CommonStructs.RotatorProp(Rotation, "Rotation"));
        if (DrawScale != _cleanSnapshot.DrawScale)
            props.AddOrReplaceProp(new FloatProperty(DrawScale, "DrawScale"));
        if (DrawScale3D != _cleanSnapshot.DrawScale3D)
            props.AddOrReplaceProp(CommonStructs.Vector3Prop(DrawScale3D, "DrawScale3D"));
        Export.WriteProperties(props);
    }
    public override void SetAnimation(AnimSequence animSequence, float pos) { }
    public override bool TestUIndexes(HashSet<int> indices) => base.TestUIndexes(indices)
        || failedCollectionOwner is { } owner && indices.Contains(owner.UIndex)
        || dependencies.Any(e => e.FileRef == Pcc && indices.Contains(e.UIndex));

    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> updatedExports, bool force = false)
    {
        // A draft has nothing in the package to refresh from
        if (IsDraft) return false;
        // Own edits rebuild through the same factory as the initial load.
        if (package == Pcc && updatedExports.Contains(Export.UIndex)) return false;
        if (info is null || info.DataOnly || !(force || dependencies.Any(e => e.FileRef == package && updatedExports.Contains(e.UIndex)))) return false;
        var pending = SnapshotTransform();
        var previousClean = _cleanSnapshot;
        ReadPreview();
        // Refresh the inherited baseline without discarding the user's pending edits.
        if (pending.Location != previousClean.Location) location = pending.Location;
        if (pending.Rotation != previousClean.Rotation) rotation = pending.Rotation;
        if (pending.DrawScale != previousClean.DrawScale) drawScale = pending.DrawScale;
        if (pending.DrawScale3D != previousClean.DrawScale3D) drawScale3D = pending.DrawScale3D;
        UpdateLocalToWorld();
        UpdateDirty();
        NotifyCoverageChanged();
        foreach (string property in new[] { nameof(Location), nameof(XPos), nameof(YPos), nameof(ZPos),
            nameof(Rotation), nameof(PitchDegrees), nameof(YawDegrees), nameof(RollDegrees),
            nameof(DrawScale), nameof(DrawScale3D), nameof(XScale), nameof(YScale), nameof(ZScale) })
            OnPropertyChanged(property);
        OnPropertyChanged(nameof(HasSpatialRepresentation));
        OnPropertyChanged(nameof(CanEditTransform));
        return true;
    }

    public override BoxSphereBounds GetBounds()
    {
        EnsureBounds();
        return new BoxSphereBounds
        {
            BoxExtent = hasPosition ? Vector3.Max(extent, new Vector3(16)) : Vector3.Zero,
            SphereRadius = hasPosition ? Math.Max(extent.Length(), 28) : 0
        }.TransformBy(boundsComponent?.LocalToWorld ?? LocalToWorld);
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        // Prefab children have their owning prefab's pick identity, not independent outliner rows.
        bool selected = context.IsActorSelected(this);
        if (pass != RenderPass.Base || Editor is null && !selected || !hasPosition || !context.TryDrawHelper(this)) return;
        Vector3 position = LocalToWorld.Translation;
        float size = Math.Max(4, 7 * context.WorldUnitsPerPixel(position));
        var (right, up) = context.GetViewAxes();
        int hitId = Editor is null ? context.SelectedActor.HitID : HitID;
        Vector4 color = selected ? LevelEditorRenderContext.HighlightColor : category switch
        {
            "Navigation" => new(0.25f, 0.9f, 0.45f, 1),
            "Audio" => new(0.5f, 0.7f, 1, 1),
            "Cameras" => new(0.7f, 0.5f, 1, 1),
            "Effects helpers" => new(1, 0.65f, 0.3f, 1),
            _ => new(0.85f, 0.85f, 0.85f, 1)
        };
        context.Sprites.Add(context.HelperSprites.Get(context.Device), position, right * size, up * size, color, hitId);
        if (selected)
        {
            EnsureBounds();
            if (context.WorldToPixel(position, out var pixel))
                context.ScreenLabels.Add(new(pixel.X, pixel.Y - 18, IsDraft ? "new" : $"#{Export.UIndex}"));
            Vector3 direction = Vector3.TransformNormal(Vector3.UnitX, LocalToWorld);
            if (direction.LengthSquared() > 0)
                context.WorldPrimitives.AddLine(position, position + Vector3.Normalize(direction) * size * 3, color, hitId);
            if (extent != Vector3.Zero) context.DrawHighlightBounds(GetBounds());
        }
    }

    private sealed class HelperBoundsProxy(MeshRenderContext context, ExportEntry export, ActorProxy owner)
        : PrimitiveComponentProxy(context, export, owner);
}

/// <summary>Serialized skeletal mesh in its reference/static pose. Physics simulation is not a viewport feature.</summary>
public sealed class KAssetProxy : SkeletalMeshActorProxy
{
    public KAssetProxy(IActorEditorContext context, ExportEntry export) : base(context, export) { }
    public override ActorCoverage Coverage => SkeletalMeshComponent?.HasPreviewMesh == true
        ? new(ActorRepresentation.PartialScene, "Skeletal mesh in its static/reference pose; rigid-body simulation and runtime pose are not previewed. Transform editing is disabled.")
        : new(ActorRepresentation.Unsupported, "KAsset has no resolvable, renderable SkeletalMeshComponent.");
    // Moving physics assets may require body/constraint updates that the generic transform commit does not perform.
    public override bool CanEditTransform => false;
}
