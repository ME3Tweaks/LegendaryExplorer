using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Editable static decal projector, with an uncommitted receiver-mesh preview.</summary>
public sealed class DecalActorProxy : EditorActorProxy
{
    private readonly DecalComponentProxy decal;
    internal TransformSnapshot ProjectionBaseline;
    public EditableDecal Decal { get; }
    public bool CanReproject => decal?.EditUnsupportedReason is null && decal is not null;
    public string DecalEditingStatus => decal?.EditingStatus ?? "No decal component.";
    public override string Category => "Decals";
    public int SortOrder => decal?.SortOrder ?? 0;
    public IMEPackage ProjectionPackage => decal?.ProjectionPackage ?? Export.FileRef;
    internal DecalComponentProxy DecalComponent => decal;
    public override ActorCoverage Coverage => decal?.GetCoverage() ?? base.Coverage;
    internal void BindReceivers(LevelEditorRenderContext context) => decal?.BindReceivers(context);

    public DecalActorProxy(IActorEditorContext context, ExportEntry export)
        : base(context, export, new(ActorKind.Decal, "Effects helpers", "Decal projector helper; no usable cooked surface."))
    {
        ProjectionBaseline = SnapshotTransform();
        var component = EffectiveLightingProperties.ResolveReference(export, "Decal", context.PackageCache, Properties);
        if (component is not null)
        {
            decal = new DecalComponentProxy(context.RenderContext, component, this);
            Components.Add(decal);
            Decal = new EditableDecal(this, decal.Properties);
        }
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        base.Render(context, pass);
        if (pass == RenderPass.Base && context.IsActorSelected(this)) decal?.DrawProjector(context, Matrix4x4.Identity);
        if (pass == RenderPass.Decals && (context.IsActorSelected(this)
            || context.GetCategoryState("Decals").IsVisible && (!IsHidden || context.ShowHidden)))
            decal?.Render(context, pass);
    }

    public override BoxSphereBounds GetBounds() => decal?.SurfaceBounds ?? base.GetBounds();

    public override void UpdateScene(LevelEditorRenderContext context, float deltaTime)
    {
        base.UpdateScene(context, deltaTime);
        decal?.EnsureProjection(context);
    }

    protected override bool HasUncommittedChanges() => base.HasUncommittedChanges() || Decal?.HasChanges == true
        || decal?.HasReprojectedReceivers == true || decal?.HasReceiverChanges == true;
    internal void RefreshDecalDirty()
    {
        decal?.UpdateNeededMembers();
        // Which meshes the Filter can list depends on where the projection lands
        Decal?.RefreshFilterCandidateStates();
        UpdateDirty();
        NotifyCoverageChanged();
        OnPropertyChanged(nameof(DecalEditingStatus));
    }
    internal void OnDecalEdited(DecalSettings before, DecalSettings after, string property)
    {
        RefreshDecalDirty();
        Editor?.OnDecalEdited(this, before, after, property);
    }
    public void PrepareDecalCommit()
    {
        if (IsReadOnly || decal is null) return;
        decal.EnsureProjection(Editor.RenderContext);
        if (IsDirty) decal.ValidateCommit();
    }
    public override void CommitChanges(PackageCache packageCache = null)
    {
        if (IsReadOnly || !IsDirty) return;
        PrepareDecalCommit();
        if (ProjectionPackage != Export.FileRef)
            throw new InvalidOperationException("Commit this decal through the Level Editor to move it to its receiving package.");
        decal?.CommitProjection();
        base.CommitChanges(packageCache);
    }
    public override void MarkClean()
    {
        decal?.AcceptProjection();
        ProjectionBaseline = SnapshotTransform();
        base.MarkClean();
    }

    protected override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        if (decal is not null) NotifyCoverageChanged();
    }

    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> exports, bool force = false)
    {
        if (package == Pcc && exports.Contains(Export.UIndex)
            || decal is not null && package == decal.Export.FileRef && exports.Contains(decal.Export.UIndex)) return false;
        if (decal?.Refresh(package, exports, force) == true) { NotifyCoverageChanged(); return true; }
        return false;
    }
}

/// <summary>Each receiver owns one cached GPU mesh; no per-frame vertex rebuild or package writes.</summary>
public sealed class DecalComponentProxy : PrimitiveComponentProxy
{
    private readonly DecalActorProxy owner;
    private DecalProjection projection;
    //the decal's material, and the one it's drawn with: the game's default decal material in place of one the game can't draw on a decal
    private IEntry materialEntry;
    private ExportEntry material;
    private string failure;
    private string lastCoverage;
    private List<Receiver> receivers = [];
    private List<Receiver> originalReceivers;
    private DecalProjection originalProjection;
    private string originalFailure;
    private string reprojectionError;
    private TransformSnapshot? projectedTransform;
    private DecalSettings projectedSettings;
    //reprojection reads parsed meshes on every move, long after any load has ended
    private readonly Dictionary<ExportEntry, StaticMesh> sourceMeshes = [];
    private HashSet<CollectionActorComponentProxy> neededMembers = [];
    private List<(MeshComponentProxy Source, Matrix4x4 Transform)> projectedSources = [];
    public bool HasReprojectedReceivers => originalReceivers is not null && !ReferenceEquals(receivers, originalReceivers);
    public bool HasReceiverChanges => originalReceivers?.Any(Stale) == true;
    public IMEPackage ProjectionPackage => HasReprojectedReceivers && receivers.Count > 0 ? receivers[0].Export.FileRef : Export.FileRef;
    public string EditUnsupportedReason { get; private set; }
    public string EditingStatus => EditUnsupportedReason ?? reprojectionError ?? FilterPackageProblem ?? (HasReprojectedReceivers
        ? receivers.Count == 0 ? "No receiving triangles in the projection volume. Committing removes the old projected surfaces."
        : (ProjectionPackage != Export.FileRef ? $"Committing moves this actor to {System.IO.Path.GetFileName(ProjectionPackage.FilePath)}. " : "")
          + "Reprojected onto loaded static meshes. Committing clears baked decal lighting and shadows; decal rebaking is not yet supported."
        : "Move, rotate or resize to reproject onto loaded static meshes. A projection must belong to one package.");
    private sealed class Receiver(StaticReceiverData data, ExportEntry export)
    {
        public readonly StaticReceiverData Data = data;
        public readonly ExportEntry Export = export;
        public MeshComponentProxy Source;
        public ExportEntry Geometry;
        public Matrix4x4 OriginalTransform;
        public byte[] OriginalData;
        public ModelPreview<LEVertex> Mesh;
        public ModelPreviewLOD<LEVertex> LightingSource;
        public string Failure;
        public bool Invalidated;
    }
    public int SortOrder => projection?.SortOrder ?? 0;
    public BoxSphereBounds? SurfaceBounds => receivers.Where(r => r.Mesh is not null)
        .Select(r => (BoxSphereBounds?)r.Mesh.LODs[0].Mesh.TransformedBounds).Aggregate((BoxSphereBounds?)null,
            (a, b) => a is { } bounds ? bounds.Union(b.Value) : b);

    public DecalComponentProxy(MeshRenderContext context, ExportEntry export, DecalActorProxy actor) : base(context, export, actor)
    {
        owner = actor;
        try
        {
            Properties = EffectiveLightingProperties.Read(export, context.PackageCache);
            if (!export.Game.IsLEGame()) throw new NotSupportedException("Cooked decal preview currently supports LE1, LE2 and LE3.");
            try { projection = DecalProjection.Read(Properties); }
            catch (NotSupportedException) { projection = DecalProjection.FromActor(Properties, DecalSettings.Read(Properties), owner.SnapshotTransform(), owner.ProjectionBaseline); }
            if (actor.Export.IsA("DecalActorMovable") || Properties.GetProp<BoolProperty>("bMovableDecal") is { Value: true })
                throw new NotSupportedException("Movable decal reprojection is not implemented.");
            if (export.Parent != actor.Export || export.Parent?.Parent?.ClassName != "Level")
                throw new NotSupportedException("Prefab/template decal receiver remapping is not implemented.");
            var (reference, package) = EffectiveLightingProperties.Reference(export, "DecalMaterial", context.PackageCache);
            UseMaterial(reference?.ResolveToEntry(package), context.PackageCache);
            if (material is null) throw new NotSupportedException("Decal material could not be resolved.");
            if (Properties.GetProp<ArrayProperty<ObjectProperty>>("ReceiverImages") is { Count: > 0 })
                EditUnsupportedReason = "Reprojection with ReceiverImages is not supported.";
            foreach (var data in export.GetBinaryData<DecalComponent>().StaticReceivers)
            {
                var target = EffectiveLightingProperties.Resolve(export.FileRef.GetEntry(data.Component), context.PackageCache);
                var receiver = new Receiver(data, target);
                receivers.Add(receiver);
                if (target?.ClassName != "StaticMeshComponent")
                {
                    receiver.Failure = $"Unsupported receiver: {target?.ClassName ?? "unresolved"}.";
                    EditUnsupportedReason = receiver.Failure + " Reprojection cannot replace this receiver type.";
                }
                else if (data.Vertices.Length == 0 || data.NumTriangles == 0) receiver.Failure = "No cooked triangles.";
                else if (!ValidGeometry(data)) receiver.Failure = "Malformed cooked triangle data.";
                else if (data.InstanceIndex is not (0 or -1)) receiver.Failure = EditUnsupportedReason = "Instanced receiver is not supported.";
            }
            if (receivers.Count == 0) failure = "No cooked static receivers. Move or resize the projector to build a projection.";
        }
        catch (Exception e) when (e is not OutOfMemoryException) { failure = EditUnsupportedReason = e.Message; }
        originalProjection = projection;
        originalReceivers = receivers;
        originalFailure = failure;
    }

    /// <summary>
    /// Draws the decal with <paramref name="entry"/>, as the game would (UDecalComponent::CaptureDecalState): a material it can't draw on a decal,
    /// or none, is drawn with the engine's DefaultDecalMaterial (<see cref="MaterialUsage.DecalDrawsDefault"/>). Receiver meshes are rebuilt with it
    /// </summary>
    private void UseMaterial(IEntry entry, PackageCache cache)
    {
        materialEntry = entry;
        var drawn = MaterialUsage.DecalDrawsDefault(entry, cache) && MaterialUsage.EngineMaterial(Export.Game, "DefaultDecalMaterial", cache) is { } fallback
            ? fallback : EffectiveLightingProperties.Resolve(entry, cache);
        if (drawn == material) return;
        material = drawn;
        foreach (var receiver in (originalReceivers ?? []).Concat(receivers).Distinct())
        {
            receiver.Mesh?.Dispose();
            receiver.Mesh = null;
        }
    }

    internal static bool ValidGeometry(StaticReceiverData data) => data.NumTriangles <= int.MaxValue / 3
        && data.Indices.Length == (long)data.NumTriangles * 3
        && data.Indices.All(i => i < data.Vertices.Length)
        && data.Vertices.All(v => DecalProjection.Finite(v.Position));

    private bool Stale(Receiver r) => r.Invalidated
        || r.Source is { } source && (source.Actor.IsDisposed || !source.Actor.Components.Contains(source)
            || source.LocalToWorld != r.OriginalTransform);

    internal void BindReceivers(LevelEditorRenderContext context)
    {
        foreach (var receiver in receivers.Where(r => r.Source is not null && (r.Source.Actor.IsDisposed || !r.Source.Actor.Components.Contains(r.Source))))
        {
            if (context.FindDecalReceiver(receiver.Export) is { } replacement)
            {
                receiver.Source = replacement;
                if (replacement.LocalToWorld != receiver.OriginalTransform || !receiver.OriginalData.SequenceEqual(receiver.Export.Data)) receiver.Invalidated = true;
            }
        }
        foreach (var r in receivers.Where(r => r.Failure is null && r.Source is null))
        {
            r.Source = context.FindDecalReceiver(r.Export);
            if (r.Source is null) continue;
            r.OriginalTransform = r.Source.LocalToWorld;
            r.OriginalData = r.Export.Data.ToArray();
            r.Geometry = EffectiveLightingProperties.ResolveReference(r.Export, "StaticMesh", context.PackageCache);
            if (r.Source.Actor.IsTransformDirty) r.Invalidated = true;
        }
    }

    internal void EnsureProjection(LevelEditorRenderContext context)
    {
        if (owner.Decal is null || owner.Editor is null) return;
        if (owner.Decal.Settings.Material != materialEntry) UseMaterial(owner.Decal.Settings.Material, context.PackageCache);
        // The original receiver snapshot stays available for exact undo until a commit.
        if (ReferenceEquals(receivers, originalReceivers)) BindReceivers(context);
        bool receiverChanged = originalReceivers.Any(Stale);
        bool geometryChanged = owner.IsTransformDirty || owner.Decal.GeometryChanged || receiverChanged;
        if (!geometryChanged && !owner.Decal.HasChanges)
        {
            bool changed = projectedTransform is not null || HasReprojectedReceivers;
            RestoreOriginal();
            if (changed) owner.RefreshDecalDirty();
            return;
        }
        var transform = owner.SnapshotTransform();
        var settings = owner.Decal.Settings;
        var sources = context.DrawList_3D.SelectMany(a => a.Components).OfType<StaticMeshComponentProxy>()
            .Where(s => s.Export.ClassName == "StaticMeshComponent" && s.Actor.Editor is not null).ToList();
        if (projectedTransform == transform && projectedSettings == settings && sources.Count == projectedSources.Count
            && sources.Select((source, i) => ReferenceEquals(source, projectedSources[i].Source) && source.LocalToWorld == projectedSources[i].Transform).All(x => x)) return;
        projectedTransform = transform;
        projectedSettings = settings;
        projectedSources = sources.Select(s => ((MeshComponentProxy)s, s.LocalToWorld)).ToList();
        reprojectionError = null;
        try
        {
            if (EditUnsupportedReason is not null) throw new NotSupportedException(EditUnsupportedReason);
            if (!geometryChanged)
            {
                ReleaseGenerated();
                receivers = originalReceivers;
                var properties = Properties.DeepClone();
                settings.Write(properties);
                originalProjection.WriteFrame(properties);
                projection = DecalProjection.Read(properties);
                failure = originalFailure;
            }
            else
            {
                projection = DecalProjection.FromActor(Properties, settings, transform, owner.ProjectionBaseline);
                foreach (var original in originalReceivers)
                    if (original.Export is not null && context.FindDecalReceiver(original.Export) is not { HasPreviewMesh: true })
                        throw new InvalidOperationException("Load all original receivers before reprojecting this decal.");
                List<Receiver> rebuilt = [];
                bool staticMeshes = Properties.GetProp<BoolProperty>("bProjectOnStaticMeshes") is not { Value: false };
                bool hidden = Properties.GetProp<BoolProperty>("bProjectOnHidden") is { Value: true };
                bool backfaces = Properties.GetProp<BoolProperty>("bProjectOnBackfaces") is { Value: true };
                bool noClip = Properties.GetProp<BoolProperty>("bNoClip") is { Value: true };
                float angle = Properties.GetProp<FloatProperty>("BackfaceAngle")?.Value ?? 0.01f;
                settings.Filter.Validate(Export.FileRef);
                foreach (var source in sources)
                {
                    if (!staticMeshes || !projection.Intersects(source.GetBounds())) continue;
                    if (source.Export.Parent is not ExportEntry parent || !(parent.IsA("StaticMeshActor") || parent.ClassName == "StaticMeshCollectionActor")) continue;
                    if (!hidden && (source.HiddenGame || source.Actor.IsHidden)) continue;
                    if (!settings.Filter.Allows(source.Export)) continue;
                    var sourceProps = EffectiveLightingProperties.Read(source.Export, context.PackageCache);
                    if (sourceProps.GetProp<BoolProperty>("bAcceptsStaticDecals") is { Value: false }) continue;
                    var geometry = EffectiveLightingProperties.ResolveReference(source.Export, "StaticMesh", context.PackageCache)
                        ?? throw new InvalidOperationException("A receiving static mesh could not be resolved.");
                    if (!sourceMeshes.TryGetValue(geometry, out var mesh)) sourceMeshes[geometry] = mesh = context.GetStaticMesh(geometry);
                    if (mesh.LODModels.Length == 0) throw new InvalidOperationException("A receiving static mesh has no render LOD.");
                    int uv = geometry.GetCondensedProperties().GetProp<IntProperty>("LightMapCoordinateIndex")?.Value ?? 0;
                    var data = DecalReprojection.Project(projection, mesh.LODModels[0], source.LocalToWorld, source.Export.UIndex, uv, backfaces, angle, noClip);
                    if (data.NumTriangles == 0) continue;
                    if (source.Actor is CollectionActorComponentProxy member && CollectionMemberUpgrade.ActorRefusal(member) is { } refusal)
                        throw new NotSupportedException($"{member.ActorName} needs to become an actor to receive this decal, but can't: {refusal}");
                    if (rebuilt.Count > 0 && rebuilt[0].Export.FileRef != source.Export.FileRef)
                        throw new NotSupportedException("The projection spans meshes in multiple packages. A decal must project onto meshes in a single package.");
                    rebuilt.Add(new Receiver(data, source.Export) { Source = source, Geometry = geometry,
                        OriginalTransform = source.LocalToWorld, OriginalData = source.Export.Data.ToArray() });
                }
                ReleaseGenerated();
                receivers = rebuilt;
                failure = null;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { reprojectionError = e.Message; }
        UpdateMeshProjection();
        owner.RefreshDecalDirty();
    }

    /// <summary>
    /// Whether the Filter can list <paramref name="member"/>. A commit lists it as its new actor, which has to be in the decal's own package, so that's
    /// the package it lands in. With nothing to land on yet (Only checked, with none checked), a mesh in its volume could be what takes it to another
    /// </summary>
    internal bool CanFilter(CollectionActorComponentProxy member) => member.Export.FileRef == ProjectionPackage
        || receivers.Count == 0 && projection is not null && member.Components.OfType<MeshComponentProxy>().Any(mesh => projection.Intersects(mesh.GetBounds()));

    /// <summary>Why the Filter can't list the collection member <paramref name="member"/> where the decal is ending up</summary>
    internal string FilterPackageRefusal(IEntry member) => $"{member.ObjectName.Instanced} is in {member.FileRef.FileNameNoExtension}, and this decal's projection is in "
        + $"{ProjectionPackage.FileNameNoExtension}. A decal's Filter can only list collection meshes in the package its projection is in.";

    private string FilterPackageProblem => owner.Decal.Settings.Filter.Actors.FirstOrDefault(e => DecalFilterSettings.IsMember(e) && e.FileRef != ProjectionPackage) is { } member
        ? FilterPackageRefusal(member) : null;

    /// <summary>Tells the collection members that the uncommitted projection lands on or the Filter lists that they need to be actors, and the others that they don't</summary>
    internal void UpdateNeededMembers()
    {
        IEnumerable<CollectionActorComponentProxy> landedOn = HasReprojectedReceivers ? receivers.Select(r => r.Source?.Actor).OfType<CollectionActorComponentProxy>() : [];
        HashSet<CollectionActorComponentProxy> members = [.. landedOn, .. owner.Decal.Settings.Filter.Actors.Select(owner.Decal.MemberOf).OfType<CollectionActorComponentProxy>()];
        foreach (var member in neededMembers.Except(members)) member.SetNeededBy(owner, false);
        foreach (var member in members.Except(neededMembers)) member.SetNeededBy(owner, true);
        neededMembers = members;
    }

    private void ReleaseGenerated()
    {
        if (HasReprojectedReceivers) foreach (var receiver in receivers) receiver.Mesh?.Dispose();
    }
    private void RestoreOriginal()
    {
        ReleaseGenerated();
        receivers = originalReceivers; projection = originalProjection; failure = originalFailure;
        reprojectionError = null; projectedTransform = null; projectedSources.Clear();
        UpdateMeshProjection();
    }
    private void UpdateMeshProjection()
    {
        foreach (var receiver in receivers)
            if (receiver.Mesh is { } mesh) mesh.LODs[0].Mesh.Decal = projection;
    }
    internal void ValidateCommit()
    {
        owner.Decal.Settings.Filter.Validate(Export.FileRef);
        if (EditUnsupportedReason is not null || reprojectionError is not null)
            throw new InvalidOperationException($"Cannot commit {owner.Export.ObjectName}: {EditUnsupportedReason ?? reprojectionError}");
        if (FilterPackageProblem is { } foreign) throw new InvalidOperationException($"Cannot commit {owner.Export.ObjectName}: {foreign}");
        foreach (var listed in owner.Decal.Settings.Filter.Actors.Where(DecalFilterSettings.IsMember))
        {
            if (owner.Decal.MemberOf(listed) is not { } member)
                throw new InvalidOperationException($"Cannot commit {owner.Export.ObjectName}: its Filter lists {listed.ObjectName.Instanced}, which isn't loaded.");
            if (CollectionMemberUpgrade.ActorRefusal(member) is { } refusal)
                throw new InvalidOperationException($"Cannot commit {owner.Export.ObjectName}: its Filter lists {member.ActorName}, which can't become an actor. {refusal}");
        }
        if (ProjectionPackage != Export.FileRef && owner.Editor.GetDecalDestinationLevel(ProjectionPackage) is null)
            throw new InvalidOperationException("The receiving package must be open and writable before moving the decal into it.");
    }
    internal void CommitProjection(ExportEntry destination = null)
    {
        ValidateCommit();
        if (destination is null && Export.FileRef != ProjectionPackage)
            throw new InvalidOperationException("Decal receiver references must be written in their owning package.");
        destination ??= Export;
        var props = destination.GetProperties();
        var settings = owner.Decal.Settings;
        if (owner.Decal.FilterChanged || destination != Export)
            settings.Filter.Write(props, ProjectionPackage, destination.FileRef);
        //a moved decal's new material is carried into its new package with the rest of it (DecalPackageMigration)
        if (owner.Decal.MaterialChanged && destination == Export) props.AddOrReplaceProp(new ObjectProperty(settings.Material, "DecalMaterial"));
        if (HasReprojectedReceivers)
        {
            settings = settings with { Width = projection.Width, Height = projection.Height, NearPlane = projection.NearPlane, FarPlane = projection.FarPlane };
            projection.WriteFrame(props);
            props.AddOrReplaceProp(CommonStructs.Vector3Prop(owner.Location, "location"));
            props.AddOrReplaceProp(CommonStructs.RotatorProp(owner.Rotation, "Orientation"));
            props.AddOrReplaceProp(new ArrayProperty<StructProperty>(receivers.Select(r => new StructProperty("DecalReceiver",
                new PropertyCollection { new ObjectProperty(r.Export.UIndex, "Component") })), "DecalReceivers"));
            settings.Write(props);
            var binary = DecalComponent.Create();
            binary.StaticReceivers = receivers.Select(r => r.Data).ToArray();
            destination.WritePropertiesAndBinary(props, binary);
        }
        else { settings.Write(props); destination.WriteProperties(props); }
    }
    internal void AcceptProjection()
    {
        if (owner.Decal is null || projectedTransform is null) return;
        if (HasReprojectedReceivers) foreach (var original in originalReceivers) original.Mesh?.Dispose();
        originalReceivers = receivers; originalProjection = projection; originalFailure = failure;
        foreach (var receiver in receivers) receiver.OriginalData = receiver.Export.Data.ToArray();
        var settings = owner.Decal.Settings with { Width = projection.Width, Height = projection.Height, NearPlane = projection.NearPlane, FarPlane = projection.FarPlane,
            Filter = owner.Decal.Settings.Filter.WithActors() };
        settings.Write(Properties); projection.WriteFrame(Properties);
        owner.Decal.MarkClean(settings);
        projectedTransform = null; projectedSources.Clear();
    }

    internal void DrawProjector(LevelEditorRenderContext context, Matrix4x4 delta)
    {
        if (projection is not { } p) return;
        // Keep the selection guides depth-tested so scene geometry obscures them.
        // Decals project orthographically, so retain parallel sides rather than drawing a cone.
        const int noHitId = 0xFFFFFF;
        var lines = context.WorldPrimitives;
        var color = LevelEditorRenderContext.HighlightColor;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = Vector3.Transform(p.Location + p.Tangent * p.Width * ((i & 1) == 0 ? -0.5f : 0.5f)
                + p.Binormal * p.Height * ((i & 2) == 0 ? -0.5f : 0.5f)
                - p.Normal * ((i & 4) == 0 ? p.NearPlane : p.FarPlane), delta);
        for (int i = 0; i < 8; i++)
            for (int bit = 1; bit <= 4; bit <<= 1)
                if ((i & bit) == 0) lines.AddLine(corners[i], corners[i | bit], color, noHitId);

        // Tie the actor's transform handle to the projection volume even when its origin is offset.
        for (int i = 0; i < 4; i++)
            lines.AddLine(owner.LocalToWorld.Translation, corners[i], color with { W = 0.45f }, noHitId);

        Vector3 start = Vector3.Transform(p.Location, delta);
        Vector3 tip = Vector3.Transform(p.Location - p.Normal * p.FarPlane, delta);
        Vector3 direction = tip - start;
        float length = direction.Length();
        if (length > 0.001f)
        {
            direction /= length;
            float head = Math.Min(length * 0.25f, 14 * context.WorldUnitsPerPixel(tip));
            var (u, v) = LightProxyVisuals.Perpendiculars(direction);
            lines.AddLine(start, tip, color, noHitId);
            foreach (var side in new[] { u, -u, v, -v })
                lines.AddLine(tip, tip - direction * head + side * (head * 0.45f), color, noHitId);
        }
    }

    public ActorCoverage GetCoverage()
    {
        string reason = failure;
        if (reprojectionError is not null) reason = "Reprojection failed: " + reprojectionError;
        if (reason is not null) return new(ActorRepresentation.EditorMarker, reason);
        int loaded = receivers.Count(r => r.Mesh is not null && !Stale(r)
            && r.Mesh.GetRenderProxies().All(m => m.CanRenderWithGameShaders));
        string[] issues = receivers.Select(r => Stale(r) ? "Receiver changed; cooked projection is invalid."
            : r.Source is null && r.Failure is null ? "Receiver is not loaded or has not been prepared."
            : r.Failure ?? r.Mesh?.GetRenderProxies().Select(m => m.LoadedGameShaderError).FirstOrDefault(s => s is not null))
            .Where(s => s is not null).Distinct().ToArray();
        return new(loaded > 0 ? ActorRepresentation.PartialScene : ActorRepresentation.EditorMarker,
            $"Cooked static surfaces: {loaded}/{receivers.Count} ready. " +
            (issues.Length > 0 ? string.Join(" ", issues) + " " : "") +
            "Requires loaded receivers and game shaders. " + (HasReprojectedReceivers ? "Reprojected geometry has no separate baked decal samples. " : "")
            + "Uses receiver lighting, decal lightmaps and level lights.");
    }

    public override void Render(MeshRenderContext renderContext, RenderPass pass)
    {
        if (renderContext is not LevelEditorRenderContext context) return;
        if (failure is not null || reprojectionError is not null || owner.Editor is null || !context.IsComponentVisible(this)) return;
        foreach (var r in receivers)
        {
            if (r.Failure is not null || r.Invalidated) continue;
            if (r.Source is null)
            {
                BindReceivers(context);
                if (r.Source is null || r.Invalidated) continue;
            }
            if (Stale(r) || !r.Source.HasPreviewMesh) continue;
            if (!context.IsActorSelected(owner) && !context.IsComponentVisible(r.Source)) continue;
            try
            {
                if (r.Mesh is not null && !ReferenceEquals(r.LightingSource, r.Source.DecalReceiverLOD))
                {
                    r.Mesh.Dispose();
                    r.Mesh = null;
                }
                if (r.Mesh is null) Build(r, context);
                r.Mesh?.Render(RenderPass.Decals, context, 0);
            }
            catch (Exception e) when (e is not OutOfMemoryException) { r.Failure = $"Cooked receiver preview failed: {e.Message}"; }
        }
        string current = GetCoverage().Reason;
        if (current != lastCoverage) { lastCoverage = current; owner.NotifyCoverageChanged(); }
    }

    private void Build(Receiver r, MeshRenderContext context)
    {
        var matrix = r.OriginalTransform;
        if (!Matrix4x4.Invert(matrix, out var inverse)) throw new NotSupportedException("Singular receiver transform.");
        var normalMatrix = Matrix4x4.Transpose(inverse);
        var vertices = new List<LEVertex>(r.Data.Vertices.Length);
        foreach (var vertex in r.Data.Vertices)
        {
            Vector3 position = Vector3.Transform(vertex.Position, matrix);
            Vector3 tangent = Vector3.Normalize(Vector3.TransformNormal((Vector3)vertex.TangentX, matrix));
            Vector4 n = (Vector4)vertex.TangentZ;
            Vector3 normal = Vector3.Normalize(Vector3.TransformNormal(new(n.X, n.Y, n.Z), normalMatrix));
            if (!DecalProjection.Finite(position) || !DecalProjection.Finite(tangent) || !DecalProjection.Finite(normal))
                throw new NotSupportedException("Invalid cooked vertex basis.");
            Fixed4<Vector4> uv = default;
            uv[0] = new(projection.ProjectUV(position), 0, 0);
            vertices.Add((LEVertex)LEVertex.Create(position, tangent, new(normal, n.W * MathF.Sign(matrix.GetDeterminant())), uv));
        }
        List<Triangle> triangles = [];
        for (int i = 0; i < r.Data.Indices.Length; i += 3)
        {
            uint a = r.Data.Indices[i], b = r.Data.Indices[i+1], c = r.Data.Indices[i+2];
            triangles.Add(matrix.GetDeterminant() < 0 ? new(a, c, b) : new(a, b, c));
        }
        var mesh = new Mesh<LEVertex>(context.Device, triangles, vertices) { Decal = projection };
        MeshStaticLighting lighting = null;
        try
        {
            r.LightingSource = r.Source.DecalReceiverLOD;
            lighting = MeshStaticLighting.CreateDecal(context, Export, r.Data, r.LightingSource?.StaticLighting);
            r.Mesh = ModelPreview<LEVertex>.CreateDecal(context, mesh, material, lighting);
            r.Mesh.LODs[0].LightEnvironment = r.LightingSource?.LightEnvironment;
            r.Mesh.LODs[0].LightEnvironmentPrimitive = r.LightingSource?.LightEnvironmentPrimitive;
        }
        catch { mesh.Dispose(); lighting?.Dispose(); throw; }
    }

    public bool Refresh(IMEPackage package, HashSet<int> exports, bool force)
    {
        bool changed = false;
        foreach (var r in originalReceivers.Concat(receivers).Distinct())
        {
            if (r.Export is null) continue;
            bool geometryChanged = r.Geometry?.FileRef == package && exports.Contains(r.Geometry.UIndex);
            if (force || r.Export.FileRef == package && (exports.Contains(r.Export.UIndex) || exports.Contains(r.Export.idxLink)))
            {
                if (r.OriginalData is not null && !r.OriginalData.SequenceEqual(r.Export.Data)) r.Invalidated = true;
                changed = true;
            }
            if (geometryChanged) { r.Invalidated = true; sourceMeshes.Remove(r.Geometry); changed = true; }
            if (force)
            {
                r.Mesh?.Dispose();
                r.Mesh = null;
            }
        }
        if (changed || force) { projectedTransform = null; if (force) sourceMeshes.Clear(); }
        return changed;
    }

    public override bool TestUIndexes(HashSet<int> exports) => base.TestUIndexes(exports)
        || receivers.Any(r => r.Export?.FileRef == Export.FileRef && (exports.Contains(r.Export.UIndex) || exports.Contains(r.Export.idxLink)))
        || materialEntry?.FileRef == Export.FileRef && exports.Contains(materialEntry.UIndex);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var r in originalReceivers.Concat(receivers).Distinct()) r.Mesh?.Dispose();
            receivers.Clear(); sourceMeshes.Clear();
            foreach (var member in neededMembers) member.SetNeededBy(owner, false);
            neededMembers = [];
        }
        base.Dispose(disposing);
    }
}
