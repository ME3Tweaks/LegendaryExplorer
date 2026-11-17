using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Each receiver owns one cached GPU mesh; no per-frame vertex rebuild or package writes.</summary>
public sealed class DecalComponentProxy : PrimitiveComponentProxy
{
    private readonly DecalActorProxy owner;
    private DecalProjection projection;
    //the decal's material, and the one it's drawn with: the game's default decal material in place of one the game can't draw on a decal
    private AssetReference materialEntry;
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
    //(collections' members, and uncommitted copies of them)
    private HashSet<ActorProxy> neededMembers = [];
    private List<(MeshComponentProxy Source, Matrix4x4 Transform, ExportEntry Mesh)> projectedSources = [];
    //a new decal's, until the commit that makes it is accepted: it has no cooked receivers, so it's projected wherever it is, and its commit writes them
    private bool isNew;
    public bool HasReprojectedReceivers => originalReceivers is not null && !ReferenceEquals(receivers, originalReceivers);
    public bool HasReceiverChanges => originalReceivers?.Any(Stale) == true;
    public IMEPackage ProjectionPackage => HasReprojectedReceivers && receivers.Count > 0 ? receivers[0].Export.FileRef : Export.FileRef;
    /// <summary>The components it lands on as it's shown, committed or not</summary>
    internal IEnumerable<ExportEntry> Receivers => receivers.Select(r => r.Export);
    public string EditUnsupportedReason { get; private set; }
    public string EditingStatus => EditUnsupportedReason ?? reprojectionError ?? FilterPackageProblem ?? ProjectionStatus;

    private string ProjectionStatus
    {
        get
        {
            if (isNew)
            {
                return receivers.Count == 0
                    ? "No receiving triangles in the projection volume: it's committed with none, until it's moved onto a static mesh."
                    : "Projected onto loaded static meshes. Committing copies their baked vertex lighting and shadows onto it, as the game does.";
            }
            if (!HasReprojectedReceivers) return "Move, rotate or resize to reproject onto loaded static meshes. A projection must belong to one package.";
            if (receivers.Count == 0) return "No receiving triangles in the projection volume. Committing removes the old projected surfaces.";
            return (ProjectionPackage != Export.FileRef ? $"Committing moves this actor to {System.IO.Path.GetFileName(ProjectionPackage.FilePath)}. " : "")
                   + "Reprojected onto loaded static meshes. Committing copies their baked vertex lighting and shadows onto it again.";
        }
    }
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
            // Read for every decal, movable and prefab ones too: the game draws its default material in place of one it can't draw on a decal
            var (reference, package) = EffectiveLightingProperties.Reference(export, "DecalMaterial", context.PackageCache);
            UseMaterial(AssetReference.Of(reference?.ResolveToEntry(package), export.FileRef, context.PackageCache), context.PackageCache);
            if (actor.Export.IsA("DecalActorMovable") || Properties.GetProp<BoolProperty>("bMovableDecal") is { Value: true })
            {
                projectedByGame = true;
                throw new NotSupportedException("Movable decal reprojection is not implemented.");
            }
            if (export.Parent != actor.Export || export.Parent?.Parent?.ClassName != "Level")
            {
                unreadReason = "it's a prefab's decal, whose surfaces LEX doesn't read.";
                throw new NotSupportedException("Prefab/template decal receiver remapping is not implemented.");
            }
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
            receiversRead = true;
            // (Its receivers don't depend on it, so they're read first)
            if (material is null) throw new NotSupportedException("Decal material could not be resolved.");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            failure = EditUnsupportedReason = e.Message;
            unreadReason ??= e.Message;
        }
        originalProjection = projection;
        originalReceivers = receivers;
        originalFailure = failure;
    }

    /// <summary>
    /// A new decal's component: <paramref name="standIn"/>, which isn't in its package, drawn with <paramref name="previewProperties"/> (its template's values)
    /// and <paramref name="material"/>. It has no cooked receivers: it's projected wherever it's put (<see cref="EnsureProjection"/>)
    /// </summary>
    public DecalComponentProxy(MeshRenderContext context, ExportEntry standIn, DecalActorProxy actor, PropertyCollection previewProperties, AssetReference material)
        : base(context, standIn, actor, previewProperties)
    {
        owner = actor;
        isNew = true;
        UseMaterial(material, context.PackageCache);
        receiversRead = true;
        originalReceivers = receivers;
    }

    /// <summary>
    /// Draws the decal with <paramref name="entry"/>, as the game would (UDecalComponent::CaptureDecalState): a material it can't draw on a decal,
    /// or none, is drawn with the engine's DefaultDecalMaterial (<see cref="MaterialUsage.DecalDrawsDefault"/>). Receiver meshes are rebuilt with it
    /// </summary>
    /// <param name="redraw">Whether its meshes are rebuilt even if it's drawn with the same material, which was edited</param>
    private void UseMaterial(AssetReference reference, PackageCache cache, bool redraw = false)
    {
        materialEntry = reference;
        materialRead = true;
        var entry = reference?.Find(cache);
        var drawn = MaterialUsage.DecalDrawnMaterial(entry, Export.Game, cache);
        if (drawn == material && !redraw) return;
        material = drawn;
        foreach (var receiver in (originalReceivers ?? []).Concat(receivers).Distinct())
        {
            receiver.Mesh?.Dispose();
            receiver.Mesh = null;
        }
    }

    //a static decal whose cooked receivers were read: not a movable one, or one whose component couldn't be read
    private readonly bool receiversRead;

    //a movable decal, which the game projects itself: it has no cooked receivers to check
    private readonly bool projectedByGame;

    //whether the constructor got as far as reading its material: it doesn't if its properties couldn't be read
    private bool materialRead;

    private readonly string unreadReason;

    /// <summary>For the level scan: why its cooked receivers couldn't be read. Null if they were, or it's movable</summary>
    internal string ReceiversUnreadReason => receiversRead || projectedByGame ? null : unreadReason;

    /// <summary>
    /// For the level scan: its receivers as it's shown (a reprojection's, if it has one), other than those pending deletion, which a commit drops. Binds
    /// them to the views they're on, as drawing it does
    /// </summary>
    /// <param name="Live">Those the game draws it on</param>
    /// <param name="Empty">Those with no cooked triangles, which it isn't drawn on</param>
    /// <param name="Gone">Receivers that don't resolve to a component, or that are trashed</param>
    /// <param name="Changed">Receivers moved or changed since the projection was made in this session, or since the level was opened, which it no longer fits</param>
    /// <param name="Malformed">Receivers whose cooked triangles can't be drawn</param>
    /// <param name="Deleting">Receivers pending deletion, which the commit drops</param>
    internal sealed record ReceiverState(int Live, int Empty, IReadOnlyList<string> Gone, IReadOnlyList<string> Changed, IReadOnlyList<string> Malformed,
        int Deleting);

    /// <returns>Null if its receivers couldn't be read, or it's movable (the game projects those itself)</returns>
    internal ReceiverState CheckReceivers(LevelEditorRenderContext context)
    {
        if (!receiversRead) return null;
        BindReceivers(context);
        List<string> gone = [], changed = [], malformed = [];
        int live = 0, empty = 0;
        foreach (var r in receivers.Where(r => !PendingDeletion(r)))
        {
            if (r.Export is null || r.Export.IsTrash())
            {
                gone.Add(r.Export?.InstancedFullPath ?? $"#{r.Data.Component}");
                continue;
            }
            string name = r.Export.Parent?.ObjectName.Instanced ?? r.Export.ObjectName.Instanced;
            if (!ValidGeometry(r.Data)) malformed.Add(name);
            else if (r.Data.NumTriangles == 0) empty++;
            else if (Stale(r)) changed.Add(name);
            else live++;
        }
        return new(live, empty, gone, changed, malformed, receivers.Count(PendingDeletion));
    }

    /// <summary>Why the game draws its default decal material in place of the decal's, as it's shown; null if it draws the decal's own, or its material wasn't read</summary>
    internal string DefaultMaterialReason(PackageCache cache)
    {
        if (!materialRead) return null;
        var entry = materialEntry?.Find(cache);
        return MaterialUsage.DecalDrawsDefault(entry, cache) ? MaterialUsage.DecalProblem(entry, cache) : null;
    }

    internal static bool ValidGeometry(StaticReceiverData data) => data.NumTriangles <= int.MaxValue / 3
        && data.Indices.Length == (long)data.NumTriangles * 3
        && data.Indices.All(i => i < data.Vertices.Length)
        && data.Vertices.All(v => DecalProjection.Finite(v.Position));

    // A receiver pending deletion isn't drawn on, and has no say in a reprojection. It's still one until the commit that takes it out, which undoing the deletion avoids
    private static bool PendingDeletion(Receiver r) => r.Source?.Actor.PendingDeletion is not null;
    // A receiver drawing another mesh than it was projected onto (one chosen for it, StaticMeshComponentProxy.ChosenMesh) is projected onto again, as a moved
    // one is: its triangles are the other mesh's. A projection onto the chosen mesh is of the mesh drawn
    private bool Stale(Receiver r) => r.Invalidated
        || r.Source is { } source && (source.Actor.IsDisposed || !source.Actor.Components.Contains(source)
            || source.LocalToWorld != r.OriginalTransform || source is StaticMeshComponentProxy mesh && !SameMesh(mesh.StaticMeshExport, r.Geometry));

    //the same mesh as the game has it, by the path it loads it at: whichever package it was found in (a chosen mesh's own package of another file's, or the
    //level's copy its commit made), and however often the preview cache opened that again
    private static bool SameMesh(ExportEntry drawn, ExportEntry projected) => drawn == projected || drawn is not null && projected is not null
        && string.Equals(drawn.MemoryFullPath, projected.MemoryFullPath, StringComparison.OrdinalIgnoreCase);

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
        bool geometryChanged = isNew || owner.IsTransformDirty || owner.Decal.GeometryChanged || receiverChanged;
        if (!geometryChanged && !owner.Decal.HasChanges)
        {
            bool changed = projectedTransform is not null || HasReprojectedReceivers;
            RestoreOriginal();
            if (changed) owner.RefreshDecalDirty();
            return;
        }
        var transform = owner.SnapshotTransform();
        var settings = owner.Decal.Settings;
        // A mesh pending deletion has no say in a reprojection, as a receiver pending deletion has none: its deletion's commit would take it out again.
        // A new mesh actor's is drawn from a stand-in until its commit makes the component (ComponentOf)
        var sources = context.DrawList_3D.SelectMany(a => a.Components).OfType<StaticMeshComponentProxy>()
            .Where(s => (s.IsStandIn || s.Export.ClassName == "StaticMeshComponent") && s.Actor.Editor is not null && s.Actor.PendingDeletion is null).ToList();
        if (projectedTransform == transform && projectedSettings == settings && sources.Count == projectedSources.Count
            && sources.Select((source, i) => ReferenceEquals(source, projectedSources[i].Source) && source.LocalToWorld == projectedSources[i].Transform
                && SameMesh(source.StaticMeshExport, projectedSources[i].Mesh)).All(x => x)) return;
        projectedTransform = transform;
        projectedSettings = settings;
        projectedSources = sources.Select(s => ((MeshComponentProxy)s, s.LocalToWorld, s.StaticMeshExport)).ToList();
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
                    if (original.Export is not null && !PendingDeletion(original) && context.FindDecalReceiver(original.Export) is not { HasPreviewMesh: true })
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
                    // (A new one's actor is of a recipe's exact class. A copy's is as the component it's drawn from is, which its commit clones)
                    var receiving = source.CopiedFrom ?? (source.IsStandIn ? null : source.Export);
                    if (receiving is null ? source.Actor.ClassName != "StaticMeshActor"
                        : receiving.ClassName != "StaticMeshComponent" || receiving.Parent is not ExportEntry parent
                          || !(parent.IsA("StaticMeshActor") || parent.ClassName == "StaticMeshCollectionActor")) continue;
                    if (!hidden && (source.HiddenGame || source.Actor.IsHidden)) continue;
                    if (!settings.Filter.Allows(source.Export)) continue;
                    //a stand-in's are the values its commit makes the component with
                    var sourceProps = source.IsStandIn ? source.Properties : EffectiveLightingProperties.Read(source.Export, context.PackageCache);
                    if (sourceProps.GetProp<BoolProperty>("bAcceptsStaticDecals") is { Value: false }) continue;
                    var geometry = source.StaticMeshExport ?? throw new InvalidOperationException("A receiving static mesh could not be resolved.");
                    if (!sourceMeshes.TryGetValue(geometry, out var mesh)) sourceMeshes[geometry] = mesh = context.GetStaticMesh(geometry);
                    if (mesh.LODModels.Length == 0) throw new InvalidOperationException("A receiving static mesh has no render LOD.");
                    int uv = geometry.GetCondensedProperties().GetProp<IntProperty>("LightMapCoordinateIndex")?.Value ?? 0;
                    var data = DecalReprojection.Project(projection, mesh.LODModels[0], source.LocalToWorld, source.Export.UIndex, uv, backfaces, angle, noClip);
                    if (data.NumTriangles == 0) continue;
                    // A member, or a new copy of one, is upgraded for it (UpdateNeededMembers)
                    if (source.Actor is { IsCollectionMember: true } member && CollectionMemberUpgrade.ActorRefusal(member) is { } refusal)
                        throw new NotSupportedException($"{member.ActorName} needs to become an actor to receive this decal, but can't: {refusal}");
                    // Its commit makes it in the file it was placed in, which it can't then move out of (DecalPackageMigration moves an existing decal's export)
                    if (isNew && source.Export.FileRef != Export.FileRef)
                        throw new NotSupportedException($"It lands on {source.Actor.ActorName} in {source.Export.FileRef.FileNameNoExtension}. A new decal can only be "
                            + $"projected onto meshes in the file it's placed in, {Export.FileRef.FileNameNoExtension}: move it, or place it in that file.");
                    // Moving a decal to another file comes before a commit makes its new meshes' components (DecalPackageMigration)
                    if (source.IsStandIn && source.Export.FileRef != Export.FileRef)
                        throw new NotSupportedException($"It lands on {source.Actor.ActorName}, which is new in {source.Export.FileRef.FileNameNoExtension}. Commit that first, "
                            + "then move this decal onto it.");
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
        IEnumerable<ActorProxy> landedOn = HasReprojectedReceivers ? receivers.Select(r => r.Source?.Actor).Where(a => a?.IsCollectionMember == true) : [];
        // A decal pending deletion, or out of the scene, doesn't need anything, until that's undone
        // (A Filter lists saved exports, so the members it lists are never uncommitted copies)
        HashSet<ActorProxy> members = owner.PendingDeletion is not null || owner.IsOutOfScene ? []
            : [.. landedOn, .. owner.Decal.Settings.Filter.Actors.Select(owner.Decal.MemberOf).OfType<CollectionActorComponentProxy>()];
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
            throw new InvalidOperationException($"Cannot commit {owner.ActorName}: {EditUnsupportedReason ?? reprojectionError}");
        if (FilterPackageProblem is { } foreign) throw new InvalidOperationException($"Cannot commit {owner.ActorName}: {foreign}");
        foreach (var listed in owner.Decal.Settings.Filter.Actors.Where(DecalFilterSettings.IsMember))
        {
            if (owner.Decal.MemberOf(listed) is not { } member)
                throw new InvalidOperationException($"Cannot commit {owner.ActorName}: its Filter lists {listed.ObjectName.Instanced}, which isn't loaded.");
            if (CollectionMemberUpgrade.ActorRefusal(member) is { } refusal)
                throw new InvalidOperationException($"Cannot commit {owner.ActorName}: its Filter lists {member.ActorName}, which can't become an actor. {refusal}");
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
        //a moved decal's new material is carried into its new package with the rest of it (DecalPackageMigration). A copy's is written onto its clone
        if (owner.Decal.MaterialChanged && (destination == Export || owner.CloneSource is not null)) props.AddOrReplaceProp(new ObjectProperty(settings.Material?.EntryIn(destination.FileRef).UIndex ?? 0, "DecalMaterial"));
        if (HasReprojectedReceivers)
        {
            settings = settings with { Width = projection.Width, Height = projection.Height, NearPlane = projection.NearPlane, FarPlane = projection.FarPlane };
            projection.WriteFrame(props);
            //(as the actor's is named in each game)
            props.AddOrReplaceProp(CommonStructs.Vector3Prop(owner.Location, ActorProxy.LocationPropertyName(destination.Game)));
            props.AddOrReplaceProp(CommonStructs.RotatorProp(owner.Rotation, "Orientation"));
            List<Receiver> committed = [.. receivers.Select(Committed)];
            props.AddOrReplaceProp(new ArrayProperty<StructProperty>(committed.Select(r => new StructProperty("DecalReceiver",
                new PropertyCollection { new ObjectProperty(r.Export.UIndex, "Component") })), "DecalReceivers"));
            settings.Write(props);
            int[] released = destination.GetBinaryData<DecalComponent>().StaticReceivers.SelectMany(r => r.ShadowMap1D ?? []).ToArray();
            var binary = DecalComponent.Create();
            binary.StaticReceivers = committed.Select(r => r.Data).ToArray();
            destination.WritePropertiesAndBinary(props, binary);
            // As UE3 projects a decal: a lit one on a mesh with vertex lighting gets its own copy of it, interpolated at its clipped vertices
            var meshes = committed.Where(r => r.Geometry is not null && sourceMeshes.ContainsKey(r.Geometry)).DistinctBy(r => r.Export)
                .ToDictionary(r => r.Export, r => sourceMeshes[r.Geometry].LODModels[0]);
            DecalReceiverLighting.Write(destination, meshes, owner.Editor.RenderContext.PackageCache, released);
        }
        else { settings.Write(props); destination.WriteProperties(props); }
    }

    /// <summary>
    /// For a copied actor's level (<see cref="ActorCapture"/>), which has nothing for it to land on: writes onto <paramref name="destination"/>, its new
    /// component, its settings, its material if it was changed, its Filter as the copy has it (empty: a copy for another level is made without its
    /// source's, which lists its old level's actors) and no receivers, with the
    /// frame it's projected along from where it is (<see cref="DecalProjection.FromActor"/>), its size scaled with it: a paste's is scaled from there, as
    /// its baseline (<see cref="DecalActorProxy.ProjectionBaseline"/>). Not projected, which would land on its source's level's meshes
    /// </summary>
    internal void CommitUnprojected(ExportEntry destination)
    {
        if (EditUnsupportedReason is not null) throw new InvalidOperationException($"Cannot copy {owner.ActorName}: {EditUnsupportedReason}");
        var props = destination.GetProperties();
        var settings = owner.Decal.Settings;
        settings.Filter.Write(props, destination.FileRef, destination.FileRef);
        if (owner.Decal.MaterialChanged) props.AddOrReplaceProp(new ObjectProperty(settings.Material?.EntryIn(destination.FileRef).UIndex ?? 0, "DecalMaterial"));
        var frame = DecalProjection.FromActor(Properties, settings, owner.SnapshotTransform(), owner.ProjectionBaseline);
        settings = settings with { Width = frame.Width, Height = frame.Height, NearPlane = frame.NearPlane, FarPlane = frame.FarPlane };
        frame.WriteFrame(props);
        props.AddOrReplaceProp(CommonStructs.Vector3Prop(owner.Location, ActorProxy.LocationPropertyName(destination.Game)));
        props.AddOrReplaceProp(CommonStructs.RotatorProp(owner.Rotation, "Orientation"));
        props.AddOrReplaceProp(new ArrayProperty<StructProperty>("DecalReceivers"));
        settings.Write(props);
        destination.WritePropertiesAndBinary(props, DecalComponent.Create());
    }
    /// <summary>
    /// <paramref name="r"/> as its package has it once the actors written before decals are (<see cref="ActorCommit"/>): one on a new mesh actor or a
    /// copy, drawn from a stand-in, is on the component that actor's commit made (<see cref="MeshActorProxy.CommittedComponent"/>,
    /// <see cref="ActorCloneProxy.CommittedComponent"/>). Itself otherwise. Nothing is changed. A view is made again from what was written once the commit
    /// is accepted, so this view doesn't take it on
    /// </summary>
    private static Receiver Committed(Receiver r)
    {
        if (r.Source is not { IsStandIn: true } standIn) return r;
        var component = standIn.Actor switch
        {
            MeshActorProxy mesh when standIn == mesh.StaticMeshComponent => mesh.CommittedComponent,
            ActorCloneProxy clone => clone.CommittedComponent(standIn),
            _ => null
        } ?? throw new InvalidOperationException($"It lands on {standIn.Actor.ActorName}, whose component hasn't been made.");
        var data = r.Data.ShallowCopy();
        data.Component = component.UIndex;
        return new Receiver(data, component) { Source = standIn, Geometry = r.Geometry, OriginalTransform = r.OriginalTransform, Mesh = r.Mesh,
            LightingSource = r.LightingSource };
    }

    internal void ForgetDeleted(IReadOnlySet<IEntry> deleted)
    {
        neededMembers.RemoveWhere(member => deleted.Contains(member.Export));
        foreach (var receiver in originalReceivers.Concat(receivers).Distinct().Where(r => deleted.Contains(r.Export))) receiver.Mesh?.Dispose();
        bool same = ReferenceEquals(receivers, originalReceivers);
        originalReceivers.RemoveAll(r => deleted.Contains(r.Export));
        if (!same) receivers.RemoveAll(r => deleted.Contains(r.Export));
        var settings = owner.Decal.Settings;
        if (settings.Filter.Actors.Any(deleted.Contains))
            owner.Decal.MarkClean(settings with { Filter = new DecalFilterSettings(settings.Filter.Mode, settings.Filter.Actors.Where(a => !deleted.Contains(a))) });
    }
    internal void AcceptProjection()
    {
        if (owner.Decal is null || projectedTransform is null) return;
        isNew = false;
        if (HasReprojectedReceivers)
        {
            foreach (var original in originalReceivers) original.Mesh?.Dispose();
        }
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
        var lines = context.WorldPrimitives;
        var color = LevelEditorRenderContext.HighlightColor;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = Vector3.Transform(p.Location + p.Tangent * p.Width * ((i & 1) == 0 ? -0.5f : 0.5f)
                + p.Binormal * p.Height * ((i & 2) == 0 ? -0.5f : 0.5f)
                - p.Normal * ((i & 4) == 0 ? p.NearPlane : p.FarPlane), delta);
        for (int i = 0; i < 8; i++)
            for (int bit = 1; bit <= 4; bit <<= 1)
                if ((i & bit) == 0) lines.AddLine(corners[i], corners[i | bit], color, BatchedPrimitives.NoHitId);

        // Tie the actor's transform handle to the projection volume even when its origin is offset.
        for (int i = 0; i < 4; i++)
            lines.AddLine(owner.LocalToWorld.Translation, corners[i], color with { W = 0.45f }, BatchedPrimitives.NoHitId);

        Vector3 start = Vector3.Transform(p.Location, delta);
        Vector3 tip = Vector3.Transform(p.Location - p.Normal * p.FarPlane, delta);
        Vector3 direction = tip - start;
        float length = direction.Length();
        if (length > 0.001f)
        {
            direction /= length;
            float head = Math.Min(length * 0.25f, 14 * context.WorldUnitsPerPixel(tip));
            var (u, v) = LightProxyVisuals.Perpendiculars(direction);
            lines.AddLine(start, tip, color, BatchedPrimitives.NoHitId);
            foreach (var side in new[] { u, -u, v, -v })
                lines.AddLine(tip, tip - direction * head + side * (head * 0.45f), color, BatchedPrimitives.NoHitId);
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
            "Requires loaded receivers and game shaders. " + (HasReprojectedReceivers ? "Reprojected geometry gets its receivers' baked samples when it's committed. " : "")
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
            if (Stale(r) || PendingDeletion(r) || !r.Source.HasPreviewMesh) continue;
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
        // A mesh it was projected onto but not drawn on (none of its triangles were in its box) may be now
        foreach (var geometry in sourceMeshes.Keys.Where(g => g.FileRef == package && exports.Contains(g.UIndex)).ToList())
        {
            sourceMeshes.Remove(geometry);
            changed = true;
        }
        if (changed || force) { projectedTransform = null; if (force) sourceMeshes.Clear(); }
        return changed;
    }

    public override bool TestUIndexes(HashSet<int> exports) => base.TestUIndexes(exports)
        || receivers.Any(r => r.Export?.FileRef == Export.FileRef && (exports.Contains(r.Export.UIndex) || exports.Contains(r.Export.idxLink)))
        //the material drawn, and the one chosen, which it's drawn with from the next update (after a commit, the copy of another file's)
        || UsesAny(materialEntry, exports) || UsesAny(owner.Decal?.Settings.Material, exports);

    /// <summary>Redraws it with its material, read again, if <paramref name="exports"/> of <paramref name="package"/> include it</summary>
    internal bool RedrawIfMaterialChanged(IMEPackage package, HashSet<int> exports)
    {
        if (package != Export.FileRef || !UsesAny(materialEntry, exports) && !UsesAny(owner.Decal?.Settings.Material, exports)) return false;
        UseMaterial(owner.Decal?.Settings.Material ?? materialEntry, RenderContext.PackageCache, redraw: true);
        return true;
    }

    private bool UsesAny(AssetReference material, HashSet<int> exports) => material?.Entry is { } entry && entry.FileRef == Export.FileRef && exports.Contains(entry.UIndex);

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
