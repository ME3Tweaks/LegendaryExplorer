using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A SplineActor: a point of a spline, with a world-space SplineComponent per connection to the next point (Connections[i]). Plain spline points are
/// tracks that actors are moved along, drawn as their curves; a SplineLoftActor also deforms a SplineMeshComponent along each connection
/// (Connections[i] is drawn by SplineMeshComps[i]). Moving a point, or editing its spline values, rebuilds its connections and the ones of the points
/// connecting to it, as UE3's editor does (ASplineActor::UpdateConnectedSplineComponents). A connection whose two points are as committed keeps its
/// stored SplineComponent and SplineParams
/// </summary>
public sealed partial class SplineActorProxy : ActorProxy
{
    /// <summary>SplineActor's SplineColor default, for the curves</summary>
    private static readonly Vector4 SplineColor = new(1, 0, 1, 0.7f);
    private const int NoHitId = 0xFFFFFF;

    public List<SplineMeshComponentProxy> SplineMeshComps = [];
    private readonly List<Connection> connections = [];
    private readonly SplineLoftSettings defaults;
    private SplineLoftSettings settings, cleanSettings;
    //UpdateLocalToWorld is first called by the base constructor, before the connections are read
    private readonly bool initialized;

    /// <summary>A SplineLoftActor, whose connections deform meshes. Otherwise only its tangent is a spline value</summary>
    public bool IsLoft { get; }
    public SplineLoftSettings Settings => settings;
    public EditableSplinePoint Point { get; private set; }
    /// <summary>Picks this point from the marker the tangent gizmo draws over the scene. Registered by the render context with the actor</summary>
    public SplinePointHitProxy PointHitProxy { get; private set; }
    public override string Category => IsLoft ? "Spline meshes" : "Splines";
    public override int HitPriority => SplineMeshComps.Any(c => c.HasPreviewMesh) ? IHitProxy.StandardPriority : IHitProxy.WireFramePriority;

    private ActorRegistry Registry => Editor.RenderContext.Registry;

    /// <summary>A connection to another point, by its actor id: a draft point has no export to refer to</summary>
    private sealed class Connection(ActorId target, ExportEntry splineComponent, SplineMeshComponentProxy mesh)
    {
        public readonly ActorId Target = target;
        public ExportEntry SplineComponent = splineComponent;
        public SplineMeshComponentProxy Mesh = mesh;
        public ExportEntry MeshExport = mesh?.Export;
        public EditableActorLighting Lighting;
        public string PreviewConfiguration;
        public bool NeedsRebake;
        /// <summary>Why the connection can't be rebuilt at all, or null</summary>
        public string Defect;
        /// <summary>Why its mesh can't be deformed along it, or null</summary>
        public string MeshDefect;
        /// <summary>Why the last rebuild didn't happen, or null</summary>
        public string Problem;
        /// <summary>An end was edited, so a commit writes the rebuilt curve</summary>
        public bool CurveRebuilt;
        /// <summary>Its mesh is drawn with rebuilt SplineParams, which a commit writes</summary>
        public bool MeshRebuilt => Mesh?.Deformation is { } d && !ReferenceEquals(d, Mesh.StoredDeformation);
        public bool IsRebuilt => CurveRebuilt || MeshRebuilt;
    }

    public SplineActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        IsLoft = actorExport.IsA("SplineLoftActor");
        defaults = ReadDefaults(context, actorExport.Class, actorExport);
        settings = cleanSettings = SplineLoftSettings.Read(Properties, defaults);
        ReadConnections(context);
        initialized = true;
    }

    /// <summary>A draft point, drawn from its own values alone. Its package is untouched</summary>
    private SplineActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties) : base(context, draft, properties)
    {
        IsLoft = draft.IsLoft;
        defaults = ReadDefaults(context, draft.Class, null);
        settings = cleanSettings = SplineLoftSettings.Read(Properties, defaults);
        ReadConnections(context);
        initialized = true;
        //a draft is wholly uncommitted
        UpdateDirty();
    }

    /// <summary>
    /// A draft copy of <paramref name="source"/> as it currently is (its pending values, not its export's): its transform, spline values and mesh choices,
    /// with no connections or components. Nothing is added to the package; the copy has an export only once a commit writes it
    /// </summary>
    public static SplineActorProxy CreateDraft(IActorEditorContext context, SplineActorProxy source, string name)
    {
        var props = new PropertyCollection
        {
            CommonStructs.Vector3Prop(source.location, source.Pcc.Game.IsGame3() ? "location" : "Location"),
            CommonStructs.RotatorProp(source.rotation, "Rotation"),
            new FloatProperty(source.drawScale, "DrawScale"),
            CommonStructs.Vector3Prop(source.drawScale3D, "DrawScale3D"),
            CommonStructs.Vector3Prop(source.prePivot, "PrePivot")
        };
        // Only values the copy can own outright; references into the source's components (or any other object it owns) are never copied
        foreach (string copied in new[] { "bHidden", "Tag", "bDisableDestination", "SplineColor", "bMovable", "bAcceptsLights", "MeshMaxDrawDistance" })
        {
            if (source.Properties.GetProp<Property>(copied) is { } prop) props.AddOrReplaceProp(prop.DeepClone());
        }
        source.settings.Write(props, source.defaults);
        if (source.IsLoft)
        {
            props.AddOrReplaceProp(new ObjectProperty(source.deformMesh, "DeformMesh"));
            props.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(source.deformMaterials.Select(i => new ObjectProperty(i)), "DeformMeshMaterials"));
        }
        ActorRegistry registry = context.RenderContext.Registry;
        IEntry actorClass = source.Export?.Class ?? registry[source.Id].Class;
        ActorRecord record = registry.CreateDraft(source.Pcc, actorClass, name, isSpline: true, source.IsLoft);
        return new SplineActorProxy(context, record, props) { OwningFile = source.OwningFile };
    }

    private static SplineLoftSettings ReadDefaults(IActorEditorContext context, IEntry actorClass, ExportEntry actorExport)
    {
        var cache = context.RenderContext.PackageCache;
        var defaultProps = new PropertyCollection();
        foreach (string name in new[] { "SplineActorTangent", "Roll", "ScaleX", "ScaleY", "Offset", "WorldXDir", "bSmoothInterpRollAndScale" })
            if (EffectiveLightingProperties.ClassDefaultProperty<Property>(actorClass, actorExport, name, cache) is { } prop) defaultProps.Add(prop);
        return SplineLoftSettings.Read(defaultProps, SplineLoftSettings.Defaults);
    }

    private void ReadConnections(IActorEditorContext context)
    {

        var meshes = IsLoft ? Properties.GetProp<ArrayProperty<ObjectProperty>>("SplineMeshComps")?.ToList() ?? [] : [];
        var byIndex = new SplineMeshComponentProxy[meshes.Count];
        for (int i = 0; i < meshes.Count; i++)
        {
            if (meshes[i].ResolveToEntry(Pcc) is ExportEntry meshExport
                && PrimitiveComponentProxy.Create(context.RenderContext, meshExport, this) is SplineMeshComponentProxy mesh)
            {
                mesh.ActorPropertyName = $"SplineMeshComps[{i}]";
                byIndex[i] = mesh;
                SplineMeshComps.Add(mesh);
                Components.Add(mesh);
            }
        }
        var connectionProps = Properties.GetProp<ArrayProperty<StructProperty>>("Connections")?.ToList() ?? [];
        for (int i = 0; i < connectionProps.Count; i++)
        {
            var target = Registry.IdForStored(Pcc, connectionProps[i].GetProp<ObjectProperty>("ConnectTo")?.Value ?? 0);
            var spline = connectionProps[i].GetProp<ObjectProperty>("SplineComponent")?.ResolveToEntry(Pcc) as ExportEntry;
            var connection = new Connection(target, spline, i < byIndex.Length ? byIndex[i] : null);
            connection.Defect = TargetDefect(target);
            if (connection.Defect is null && IsLoft && !Registry[target].IsLoft) connection.MeshDefect = $"It connects to a {TargetClassName(target)}, not a loft point.";
            else if (connection.Defect is null && IsLoft && connection.Mesh is null) connection.MeshDefect = "It has no SplineMeshComponent.";
            connections.Add(connection);
        }
        Point = new EditableSplinePoint(this);
        PointHitProxy = new SplinePointHitProxy(this);
        InitializeConnections();
    }

    private string TargetClassName(ActorId target) => Registry[target] is { } record ? record.Export?.ClassName ?? record.Class?.ObjectName.Name ?? "Class" : "Class";

    /// <summary>Why a connection to <paramref name="target"/> can't be rebuilt at all, or null</summary>
    private string TargetDefect(ActorId target) => Registry[target] switch
    {
        null or { IsUnresolved: true } => "It connects to nothing.",
        { IsSpline: false } => $"It connects to a {TargetClassName(target)}, not a spline point.",
        _ => null
    };

    /// <summary>This point and the loaded points connected to it, directly or through others, in either direction</summary>
    public List<SplineActorProxy> Network()
    {
        var points = LoadedPoints.ToList();
        var network = new List<SplineActorProxy> { this };
        for (int i = 0; i < network.Count; i++)
        {
            var point = network[i];
            foreach (var next in points)
            {
                if (network.Contains(next)) continue;
                if (point.connections.Any(c => c.Target == next.Id) || next.connections.Any(c => c.Target == point.Id)) network.Add(next);
            }
        }
        return network;
    }

    /// <summary>The curves of this point's connections to loaded points, as they're currently built</summary>
    internal IEnumerable<InterpCurve<Vector3>> ConnectionCurves()
    {
        SplineLoftNode node = Node;
        foreach (var connection in connections)
            if (connection.Defect is null && FindPoint(connection.Target) is { } target) yield return SplineLoftGeometry.SplineInfo(node, target.Node, Pcc.Game);
    }

    /// <summary>This point as UE3 builds the splines from it: its Location, AActor::LocalToWorld (with FGlobalMath's trig table) and spline values</summary>
    public SplineLoftNode Node => new(location, SplineLoftGeometry.ActorLocalToWorld(location, rotation, drawScale * drawScale3D, prePivot), settings);

    /// <summary>It moved or its spline values changed since the last commit, so its connections and the incoming ones are rebuilt</summary>
    private bool IsEdited => IsTransformDirty || settings != cleanSettings;

    internal IEnumerable<SplineActorProxy> LoadedPoints =>
        Editor?.RenderContext.DrawList_3D.OfType<SplineActorProxy>().Where(a => !a.IsDisposed && a.Pcc == Pcc) ?? [];

    private SplineActorProxy FindPoint(ActorId id) => id.IsEmpty ? null : LoadedPoints.FirstOrDefault(a => a.Id == id);

    /// <summary>The loaded points with a connection to this one</summary>
    public IEnumerable<SplineActorProxy> IncomingPoints => LoadedPoints.Where(a => a != this && a.connections.Any(c => c.Target == Id));

    /// <summary>
    /// ASplineActor::UpdateSplineComponents, and for a loft ASplineLoftActor::UpdateSplineParams, for each connection: rebuilt from this point and the next
    /// if either was edited, otherwise the stored curve and SplineParams
    /// </summary>
    internal void RefreshConnections()
    {
        foreach (var connection in connections)
        {
            connection.Problem = null;
            connection.CurveRebuilt = false;
            if (connection.Defect is not null) continue;
            var target = FindPoint(connection.Target);
            bool edited = IsEdited || target?.IsEdited == true || connection.SplineComponent is null || OutgoingTopologyDirty || MeshConfigurationDirty;
            if (edited && target is null) connection.Problem = "The point it connects to isn't loaded, so it can't be rebuilt.";
            connection.CurveRebuilt = edited && target is not null;
            if (connection.Mesh is null) continue;
            var deformation = connection.Mesh.StoredDeformation;
            if (connection.CurveRebuilt && connection.MeshDefect is null)
            {
                try
                {
                    deformation = SplineLoftGeometry.Segment(Node, target.Node, connection.Mesh.MeshBounds);
                }
                catch (InvalidOperationException e)
                {
                    connection.Problem = e.Message;
                }
            }
            connection.Mesh.SetDeformation(deformation);
        }
        UpdateDirty();
        NotifyCoverageChanged();
        OnPropertyChanged(nameof(EditingStatus));
    }

    private void OnPointChanged()
    {
        RefreshConnections();
        foreach (var incoming in IncomingPoints) incoming.RefreshConnections();
    }

    /// <summary>
    /// The view was added to or removed from the scene. Connections are found among the drawn points, so this point's and the ones ending here
    /// are rebuilt against whichever view of each actor is drawn now
    /// </summary>
    internal void OnSceneMembershipChanged()
    {
        if (IsDisposed || !initialized) return;
        OnPointChanged();
    }

    protected override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        if (!initialized) return;
        Point.NotifyWorldTangent();
        OnPointChanged();
    }

    /// <summary>Changes the spline values. <paramref name="property"/> names the edit for undo</summary>
    internal void ApplySettings(SplineLoftSettings value, string property)
    {
        if (IsReadOnly || value == settings) return;
        var before = settings;
        RestoreSettings(value);
        Editor?.OnSplineEdited(this, before, value, property);
    }

    /// <summary>Changes the spline values without recording an undo step (undo itself, and restoring)</summary>
    public void RestoreSettings(SplineLoftSettings value)
    {
        if (value == settings) return;
        settings = value;
        Point.NotifySettings();
        OnPointChanged();
    }

    /// <summary>Sets SplineActorTangent from a world-space tangent</summary>
    internal void SetWorldTangent(Vector3 worldTangent, string property)
    {
        if (!Matrix4x4.Invert(Node.LocalToWorld, out Matrix4x4 worldToLocal)) return;
        ApplySettings(settings with { Tangent = Vector3.TransformNormal(worldTangent, worldToLocal) }, property);
    }

    //a draft is wholly uncommitted
    protected override bool HasUncommittedChanges() => IsDraft || base.HasUncommittedChanges() || settings != cleanSettings || connections.Any(c => c.IsRebuilt)
        || TopologyDirty || MeshConfigurationDirty;

    public string EditingStatus
    {
        get
        {
            string[] problems = connections.Select(c => c.Defect ?? c.Problem ?? c.MeshDefect).Where(p => p is not null).Distinct().ToArray();
            int rebuilt = connections.Count(c => c.IsRebuilt);
            string status = rebuilt > 0
                ? $"{rebuilt} of {connections.Count} connections rebuilt. Committing writes their spline curves"
                    + (IsLoft ? " and SplineParams; their baked lighting is kept until the level is rebaked." : ".")
                : "Move or rotate this point, drag its tangent's grabbers, or edit its values to rebuild its connections and the ones ending here.";
            if (TopologyDirty) status = "Connections changed. " + status;
            if (IsLoft && connections.Any(c => c.Mesh is not null && (c.MeshExport is null || c.NeedsRebake)))
                status += " New meshes have no baked lighting and need a rebake.";
            if (MeshConfigurationDirty) status += " Reassigned meshes keep their existing baked lighting until a rebake.";
            if (IsLoft && deformMesh == 0) status += " Assign a DeformMesh before adding outgoing connections or reversing incoming ones.";
            if (ConnectionCommitProblem is { } problem) status += " Cannot commit: " + problem;
            return problems.Length > 0 ? status + " " + string.Join(" ", problems) : status;
        }
    }

    public override ActorCoverage Coverage
    {
        get
        {
            string editing = "Point, tangent and connection edits rebuild live."
                + (connections.Any(c => c.NeedsRebake || c.Mesh is not null && c.MeshExport is null) ? " New meshes have no baked lighting and need a rebake." : "");
            if (!IsLoft)
                return new(ActorRepresentation.EditorMarker, "Spline point; its connections' curves are drawn while Splines are shown. " + editing);
            int rendered = SplineMeshComps.Count(c => c.HasPreviewMesh);
            if (rendered == 0)
                return new(ActorRepresentation.EditorMarker, SplineMeshComps.Count == 0
                    ? "Loft spline point with no outgoing mesh; the connections ending here are drawn by the points they start from. " + editing
                    : "Loft spline point; no renderable spline mesh was loaded. " + editing);
            return new(rendered < SplineMeshComps.Count ? ActorRepresentation.PartialScene : ActorRepresentation.SceneRendered,
                $"{rendered} of {SplineMeshComps.Count} spline meshes deformed along their connections as the game's spline vertex factory does. " + editing);
        }
    }

    public override void CommitChanges(PackageCache packageCache = null)
    {
        if (IsDraft) throw new InvalidOperationException($"{DisplayText} is a draft: it has no export to commit to.");
        if (IsReadOnly) return;
        ValidateConnectionsForCommit();
        CommitConnections();
        settings.Write(Properties, defaults);
        base.CommitChanges(packageCache);
        foreach (var connection in connections)
        {
            //a mesh written from its preview already has its deformation
            if (connection.MeshRebuilt && !committed[connection].MeshWritten)
            {
                var meshProps = connection.Mesh.Export.GetProperties();
                connection.Mesh.Deformation.Write(meshProps);
                connection.Mesh.Export.WriteProperties(meshProps);
            }
            if (connection.CurveRebuilt && connection.SplineComponent is not null && FindPoint(connection.Target) is { } target)
            {
                var splineProps = connection.SplineComponent.GetProperties();
                SplineLoftGeometry.WriteSplineComponent(splineProps, Node, target.Node, Export.Game);
                connection.SplineComponent.WriteProperties(splineProps);
            }
        }
        CommitConnectionLighting();
    }

    /// <summary>Its connection meshes' lighting is written and accepted with its connections, to the components the commit writes</summary>
    internal override IEnumerable<MeshComponentProxy> LightingMeshes => [];

    internal override void AbandonCommit()
    {
        committed = null;
        base.AbandonCommit();
    }

    public override void MarkClean()
    {
        AcceptCommittedConnections();
        foreach (var connection in connections)
        {
            if (connection.MeshRebuilt) connection.Mesh.AcceptDeformation();
            connection.CurveRebuilt = false;
        }
        cleanSettings = settings;
        AcceptConnections();
        base.MarkClean();
        Point.NotifySettings();
        OnPropertyChanged(nameof(EditingStatus));
    }

    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> updatedExports, bool force = false)
    {
        if (package == Pcc && (!IsDraft && updatedExports.Contains(Export.UIndex)
            || connections.Any(c => c.SplineComponent is not null && updatedExports.Contains(c.SplineComponent.UIndex)
                || c.MeshExport is not null && updatedExports.Contains(c.MeshExport.UIndex) && c.MeshExport.ClassName != "SplineMeshComponent"))) return false;
        bool refreshed = base.TryRefreshPreview(package, updatedExports, force);
        //a reloaded mesh is drawn with its stored SplineParams again
        if (refreshed)
        {
            foreach (var connection in connections) connection.Mesh?.SetDeformation(connection.Mesh.StoredDeformation);
            RefreshConnections();
        }
        return refreshed;
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        base.Render(context, pass);
        if (pass != RenderPass.Base) return;
        bool selected = context.IsActorSelected(this);
        Vector3 position = location;
        if (context.TryDrawHelper(this))
        {
            float size = Math.Max(4, 6 * context.WorldUnitsPerPixel(position));
            var (right, up) = context.GetViewAxes();
            context.Sprites.Add(context.HelperSprites.Get(context.Device), position, right * size, up * size,
                selected ? LevelEditorRenderContext.HighlightColor : SplineColor with { W = 1 }, HitID);
        }
        //each point draws the curves of its own connections into the scene whenever it's shown. A selected point's network is also drawn over it by
        //the tangent gizmo
        foreach (var curve in ConnectionCurves()) DrawCurve(context, context.WorldPrimitives, curve, SplineColor);
    }

    internal static void DrawCurve(LevelEditorRenderContext context, BatchedPrimitives primitives, InterpCurve<Vector3> curve, Vector4 color)
    {
        const int steps = 32;
        Vector3 previous = curve.Eval(0, Vector3.Zero);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = curve.Eval((float)i / steps, Vector3.Zero);
            primitives.AddLine(previous, point, color, NoHitId);
            previous = point;
        }
        if (!TryGetDirectionArrow(curve, out Vector3 center, out Vector3 direction)) return;
        // A small camera-facing arrowhead, in pixels, on the same depth layer as its curve. It doesn't pick or cover the point markers.
        Vector3 side = Vector3.Cross(direction, context.Camera.CameraForward);
        side = side.LengthSquared() > 1e-8f ? Vector3.Normalize(side) : LightProxyVisuals.Perpendiculars(direction).U;
        float size = 10 * context.WorldUnitsPerPixel(center);
        Vector3 tip = center + direction * (size / 2);
        Vector3 tail = center - direction * (size / 2);
        primitives.AddLine(tip, tail + side * (size * 0.4f), color, NoHitId);
        primitives.AddLine(tip, tail - side * (size * 0.4f), color, NoHitId);
    }

    /// <summary>The position and unit direction at the parameter midpoint of a two-point UE3 Hermite spline; stationary points have no arrow.</summary>
    public static bool TryGetDirectionArrow(InterpCurve<Vector3> curve, out Vector3 center, out Vector3 direction)
    {
        center = direction = Vector3.Zero;
        if (curve.Points.Count != 2) return false;
        var start = curve.Points[0]; var end = curve.Points[1];
        center = curve.Eval((start.InVal + end.InVal) / 2, Vector3.Zero);
        // Analytic midpoint derivative avoids subtracting nearby world positions far from the origin.
        direction = (end.OutVal - start.OutVal) * 1.5f - (start.LeaveTangent + end.ArriveTangent) * ((end.InVal - start.InVal) * 0.25f);
        float length = direction.Length();
        if (!float.IsFinite(length) || length < 1e-6f) return false;
        direction /= length;
        return true;
    }

    internal static Vector4 CurveColor => SplineColor;

    public override BoxSphereBounds GetBounds()
    {
        var marker = new BoxSphereBounds { Origin = location, BoxExtent = new Vector3(32), SphereRadius = 56 };
        var drawn = SplineMeshComps.Where(c => c.HasPreviewMesh).ToList();
        return drawn.Aggregate(marker, (bounds, mesh) => bounds.Union(mesh.GetBounds()));
    }
}

/// <summary>The selected spline point's values, for the details panel. Only the tangent applies to a plain spline point</summary>
public sealed class EditableSplinePoint(SplineActorProxy owner) : NotifyPropertyChangedBase
{
    public bool IsEditable => !owner.IsReadOnly;
    public bool IsLoft => owner.IsLoft;
    private SplineLoftSettings S => owner.Settings;
    private void Set(SplineLoftSettings value, string property) => owner.ApplySettings(value, property);

    public float TangentX { get => S.Tangent.X; set { if (float.IsFinite(value)) Set(S with { Tangent = S.Tangent with { X = value } }, "tangent"); } }
    public float TangentY { get => S.Tangent.Y; set { if (float.IsFinite(value)) Set(S with { Tangent = S.Tangent with { Y = value } }, "tangent"); } }
    public float TangentZ { get => S.Tangent.Z; set { if (float.IsFinite(value)) Set(S with { Tangent = S.Tangent with { Z = value } }, "tangent"); } }
    //the world tangent as the gizmo's grabbers change it: a length, and a heading and elevation in degrees
    private Vector3 WorldTangent => owner.Node.WorldTangent;
    public float TangentLength
    {
        get => WorldTangent.Length();
        set
        {
            if (!float.IsFinite(value) || value < 0) return;
            Vector3 tangent = WorldTangent;
            // As with the gizmo, a point without a tangent starts along its own X axis.
            if (tangent == Vector3.Zero) tangent = Vector3.TransformNormal(Vector3.UnitX, owner.Node.LocalToWorld);
            float length = tangent.Length();
            if (length > 0) owner.SetWorldTangent(tangent * (value / length), "tangent");
        }
    }
    public float TangentYaw
    {
        get => MathF.Atan2(WorldTangent.Y, WorldTangent.X) * 180 / MathF.PI;
        set { if (float.IsFinite(value)) SetTangentDirection(value, TangentPitch); }
    }
    public float TangentPitch
    {
        get => MathF.Atan2(WorldTangent.Z, new Vector2(WorldTangent.X, WorldTangent.Y).Length()) * 180 / MathF.PI;
        set { if (float.IsFinite(value)) SetTangentDirection(TangentYaw, Math.Clamp(value, -90, 90)); }
    }

    private void SetTangentDirection(float yaw, float pitch)
    {
        (float sinYaw, float cosYaw) = MathF.SinCos(yaw * MathF.PI / 180);
        (float sinPitch, float cosPitch) = MathF.SinCos(pitch * MathF.PI / 180);
        owner.SetWorldTangent(new Vector3(cosPitch * cosYaw, cosPitch * sinYaw, sinPitch) * TangentLength, "tangent");
    }

    public float Roll { get => S.Roll; set { if (float.IsFinite(value)) Set(S with { Roll = value }, nameof(Roll)); } }
    public float ScaleX { get => S.Scale.X; set { if (float.IsFinite(value)) Set(S with { Scale = S.Scale with { X = value } }, nameof(ScaleX)); } }
    public float ScaleY { get => S.Scale.Y; set { if (float.IsFinite(value)) Set(S with { Scale = S.Scale with { Y = value } }, nameof(ScaleY)); } }
    public float OffsetX { get => S.Offset.X; set { if (float.IsFinite(value)) Set(S with { Offset = S.Offset with { X = value } }, "offset"); } }
    public float OffsetY { get => S.Offset.Y; set { if (float.IsFinite(value)) Set(S with { Offset = S.Offset with { Y = value } }, "offset"); } }
    public float WorldXDirX { get => S.WorldXDir.X; set { if (float.IsFinite(value)) Set(S with { WorldXDir = S.WorldXDir with { X = value } }, "WorldXDir"); } }
    public float WorldXDirY { get => S.WorldXDir.Y; set { if (float.IsFinite(value)) Set(S with { WorldXDir = S.WorldXDir with { Y = value } }, "WorldXDir"); } }
    public float WorldXDirZ { get => S.WorldXDir.Z; set { if (float.IsFinite(value)) Set(S with { WorldXDir = S.WorldXDir with { Z = value } }, "WorldXDir"); } }
    public bool SmoothInterpRollAndScale { get => S.SmoothInterpRollAndScale; set => Set(S with { SmoothInterpRollAndScale = value }, "interpolation"); }

    internal void NotifySettings()
    {
        foreach (string name in new[] { nameof(TangentX), nameof(TangentY), nameof(TangentZ), nameof(TangentLength), nameof(TangentYaw), nameof(TangentPitch), nameof(Roll), nameof(ScaleX), nameof(ScaleY), nameof(OffsetX),
                     nameof(OffsetY), nameof(WorldXDirX), nameof(WorldXDirY), nameof(WorldXDirZ), nameof(SmoothInterpRollAndScale), nameof(IsEditable) })
            OnPropertyChanged(name);
    }

    /// <summary>Turning or scaling the point turns or scales its world tangent</summary>
    internal void NotifyWorldTangent()
    {
        foreach (string name in new[] { nameof(TangentLength), nameof(TangentYaw), nameof(TangentPitch) }) OnPropertyChanged(name);
    }
}

public sealed class SplineSettingsAction : IMergeableUndoAction
{
    private readonly ActorRef actor;
    private readonly SplineLoftSettings before;
    private readonly string property;
    private SplineLoftSettings latest;
    private DateTime time = DateTime.UtcNow;

    public SplineSettingsAction(SplineActorProxy actor, SplineLoftSettings before, SplineLoftSettings after, string property)
    {
        this.actor = new ActorRef(actor);
        this.before = before;
        this.property = property;
        latest = after;
    }

    private SplineActorProxy Point => (SplineActorProxy)actor.View;
    public string Description => $"Edit spline {property}";
    public bool IsOrphaned => actor.IsOrphaned;
    public IReadOnlyCollection<ActorId> Subjects => [actor.Id];
    public void Undo() => Point.RestoreSettings(before);
    public void Redo() => Point.RestoreSettings(latest);
    public bool TryMerge(IUndoAction next)
    {
        if (next is not SplineSettingsAction action || !action.actor.RefersTo(actor) || action.property != property
            || action.time - time > TimeSpan.FromMilliseconds(500)) return false;
        latest = action.latest; time = action.time; return true;
    }
}

/// <summary>A spline point's marker as the tangent gizmo draws it over the scene, which selects the point</summary>
public class SplinePointHitProxy(SplineActorProxy point) : IHitProxy
{
    public SplineActorProxy Point { get; } = point;
    public int HitID { get; set; }
    public int HitPriority => IHitProxy.UIPriority;
}

public enum SplineTangentHandle
{
    None,
    LeaveDirection,
    LeaveLength,
    ArriveDirection,
    ArriveLength,
}

public class SplineTangentHitProxy(SplineTangentHandle handle) : IHitProxy
{
    public SplineTangentHandle Handle { get; } = handle;
    public int HitID { get; set; }
    public int HitPriority => IHitProxy.UIPriority;
}

/// <summary>How the tangent gizmo turns mouse rays into a tangent's direction or length</summary>
public static class SplineTangentDrag
{
    /// <summary>
    /// The unit direction from <paramref name="center"/> to where the ray meets the sphere around it, on its near side or its far side. A ray that misses
    /// the sphere gives the direction of the closest point of its outline, so a drag past the edge stays on the edge
    /// </summary>
    public static Vector3 SphereDirection(Vector3 center, float radius, Vector3 rayOrigin, Vector3 rayDirection, bool nearSide)
    {
        Vector3 toOrigin = rayOrigin - center;
        float b = Vector3.Dot(toOrigin, rayDirection);
        float discriminant = b * b - (toOrigin.LengthSquared() - radius * radius);
        float t = -b;
        if (discriminant > 0)
        {
            float root = MathF.Sqrt(discriminant);
            //from inside the sphere, the near side is behind the ray
            t = nearSide && root < -b ? -b - root : -b + root;
        }
        Vector3 offset = rayOrigin + rayDirection * t - center;
        return offset.LengthSquared() > 0 ? Vector3.Normalize(offset) : Vector3.Zero;
    }

    /// <summary>Whether a point on a sphere is on the side of it facing a viewer looking along <paramref name="rayDirection"/></summary>
    public static bool FacesViewer(Vector3 center, Vector3 point, Vector3 rayDirection) => Vector3.Dot(point - center, rayDirection) <= 0;

    /// <summary>
    /// How far along an axis (a unit direction from <paramref name="axisOrigin"/>) its closest point to the ray is, or null when the ray runs along the axis
    /// </summary>
    public static float? AxisDistance(Vector3 axisOrigin, Vector3 axisDirection, Vector3 rayOrigin, Vector3 rayDirection)
    {
        Vector3 w = axisOrigin - rayOrigin;
        float b = Vector3.Dot(axisDirection, rayDirection);
        float denominator = 1 - b * b;
        if (denominator < 1e-4f) return null;
        return (b * Vector3.Dot(rayDirection, w) - Vector3.Dot(axisDirection, w)) / denominator;
    }

    /// <summary>The rotation turning one unit vector into another</summary>
    public static Quaternion Rotation(Vector3 from, Vector3 to)
    {
        Vector3 axis = Vector3.Cross(from, to);
        if (axis.LengthSquared() < 1e-12f) return Quaternion.Identity;
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(Vector3.Dot(from, to), -1, 1)));
    }
}

/// <summary>
/// The selected spline point's tangent, with two grabbers on each side at the Bézier control point of the curves leaving and arriving at it (the point
/// ± a third of the tangent). The sphere only turns the tangent: it moves over a sphere around the point, staying on the half it was grabbed on (the half
/// facing the viewer, unless the tangent points away), so each mouse position means one direction. The arrow beyond it only changes the length, sliding
/// along the tangent. Moving and rotating the point is the transform widget's job
/// </summary>
public class SplineTangentGizmo : UIElement
{
    public SplineActorProxy Attach;
    public SplineTangentHandle HoveredHandle;
    public SplineTangentHandle DragHandle { get; private set; }
    public bool IsDragging => DragHandle is not SplineTangentHandle.None;
    private SplineLoftSettings DragStartSettings;
    private Vector3 DragStartTangent;
    //turning: the sphere it moves on, which half, and the turn from where the mouse ray first met the sphere to the grabber, so it doesn't jump
    private float DragRadius;
    private bool DragNearSide;
    private Quaternion DragGrabOffset;
    //lengthening: where along the tangent the mouse ray first passed
    private float DragStartDistance;

    private readonly int[] HandleHitIds = new int[Enum.GetValues<SplineTangentHandle>().Length];

    /// <summary>Called when a drag ends with the settings before and after it. Wired by LevelEditor to push undo actions</summary>
    public Action<SplineActorProxy, SplineLoftSettings, SplineLoftSettings> OnDragComplete;

    private static readonly Vector4 HandleColor = new(1, 1, 1, 1);
    private static readonly Vector4 ActiveHandleColor = new(1, 1, 0, 1);
    private const int NoHitId = 0xFFFFFF;
    //sizes in pixels
    private const float SphereRadius = 5, ArrowGap = 3, ArrowLength = 14, ArrowRadius = 5, MinimumReach = 16;
    /// <summary>The shortest tangent the arrow drags to</summary>
    private const float MinimumTangent = 1;

    public void GetHandleHitProxies(ref USparseArray<IHitProxy> hitProxies)
    {
        foreach (SplineTangentHandle handle in Enum.GetValues<SplineTangentHandle>())
            HandleHitIds[(int)handle] = handle is SplineTangentHandle.None ? NoHitId : hitProxies.Add(new SplineTangentHitProxy(handle));
    }

    public int HandleHitId(SplineTangentHandle handle) => HandleHitIds[(int)handle];

    private static bool IsArrive(SplineTangentHandle handle) => handle is SplineTangentHandle.ArriveDirection or SplineTangentHandle.ArriveLength;
    private static bool IsDirection(SplineTangentHandle handle) => handle is SplineTangentHandle.LeaveDirection or SplineTangentHandle.ArriveDirection;

    /// <summary>The tangent's unit direction, or the point's X axis when it has none</summary>
    private Vector3 TangentDirection(Vector3 worldTangent)
    {
        if (worldTangent.LengthSquared() > 1e-8f) return Vector3.Normalize(worldTangent);
        Vector3 x = Vector3.TransformNormal(Vector3.UnitX, Attach.Node.LocalToWorld);
        return x.LengthSquared() > 0 ? Vector3.Normalize(x) : Vector3.UnitX;
    }

    /// <summary>A side's unit direction, and how far out its sphere is: at the control point, but never closer to the point than a few pixels</summary>
    private (Vector3 Direction, float Reach) Placement(LevelEditorRenderContext context, bool arrive)
    {
        Vector3 tangent = Attach.Node.WorldTangent;
        Vector3 direction = TangentDirection(tangent);
        return (arrive ? -direction : direction, MathF.Max(tangent.Length() / 3, MinimumReach * context.WorldUnitsPerPixel(Attach.Location)));
    }

    public override void Draw(LevelEditorRenderContext context)
    {
        if (Attach is null || !context.IsActorVisible(Attach)) return;
        //the selected point's spline network over the scene, since the points lie inside their meshes. Each marker selects its point
        foreach (var point in Attach.Network())
        {
            foreach (var curve in point.ConnectionCurves())
                SplineActorProxy.DrawCurve(context, context.UIPrimitives, curve, LevelEditorRenderContext.HighlightColor with { W = 0.75f });
        }
        foreach (var point in Attach.LoadedPoints)
        {
            if (!context.IsActorVisible(point)) continue;
            Vector4 color = point == Attach ? LevelEditorRenderContext.HighlightColor : SplineActorProxy.CurveColor with { W = 1 };
            DrawBox(context, point.Location, 5, color, point.PointHitProxy.HitID);
        }
        Vector3 position = Attach.Location;
        Vector3 toLight = ToLight(context);
        foreach (bool arrive in new[] { false, true })
        {
            (SplineTangentHandle turn, SplineTangentHandle lengthen) = arrive
                ? (SplineTangentHandle.ArriveDirection, SplineTangentHandle.ArriveLength)
                : (SplineTangentHandle.LeaveDirection, SplineTangentHandle.LeaveLength);
            (Vector3 direction, float reach) = Placement(context, arrive);
            Vector3 sphere = position + direction * reach;
            float pixel = context.WorldUnitsPerPixel(sphere);
            Vector3 arrowBase = sphere + direction * (SphereRadius + ArrowGap) * pixel;
            context.UIPrimitives.AddLine(position, arrowBase, (IsActive(turn) || IsActive(lengthen) ? ActiveHandleColor : HandleColor) with { W = 0.8f }, NoHitId);
            if (Attach.IsReadOnly) continue;
            //the sphere the turning grabber moves on
            if (IsActive(turn)) DrawCircle(context, position, IsDragging ? DragRadius : reach, ActiveHandleColor with { W = 0.35f });
            DrawSphere(context, sphere, SphereRadius * pixel, IsActive(turn) ? ActiveHandleColor : HandleColor, toLight, HandleHitIds[(int)turn]);
            DrawCone(context, arrowBase, direction, ArrowLength * pixel, ArrowRadius * pixel, IsActive(lengthen) ? ActiveHandleColor : HandleColor, toLight,
                HandleHitIds[(int)lengthen]);
        }
    }

    private bool IsActive(SplineTangentHandle handle) => DragHandle == handle || DragHandle is SplineTangentHandle.None && HoveredHandle == handle;

    /// <summary>A cube <paramref name="pixels"/> pixels across, over the scene</summary>
    private static void DrawBox(LevelEditorRenderContext context, Vector3 center, float pixels, Vector4 color, int hitId)
    {
        float size = pixels * context.WorldUnitsPerPixel(center);
        var mesh = context.UIPrimitives.BuildMesh(color, hitId, Matrix4x4.CreateScale(size) * Matrix4x4.CreateTranslation(center));
        for (int i = 0; i < 8; i++) mesh.AddVertex((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
        int[] faces = [0, 2, 3, 3, 1, 0, 4, 5, 7, 7, 6, 4, 0, 1, 5, 5, 4, 0, 2, 6, 7, 7, 3, 2, 0, 4, 6, 6, 2, 0, 1, 3, 7, 7, 5, 1];
        for (int i = 0; i < faces.Length; i += 3) mesh.AddTriangle(faces[i], faces[i + 1], faces[i + 2]);
    }

    /// <summary>Lit from over the viewer's shoulder, like the light arrows, so the grabbers read as solid</summary>
    private static Vector3 ToLight(LevelEditorRenderContext context)
    {
        (_, Vector3 up) = context.GetViewAxes();
        return Vector3.Normalize(up * 0.6f - context.Camera.CameraForward * 0.8f);
    }

    private static Vector4 Shade(Vector4 color, Vector3 normal, Vector3 toLight)
    {
        float shade = 0.45f + 0.55f * MathF.Max(0, Vector3.Dot(normal, toLight));
        return new Vector4(color.X * shade, color.Y * shade, color.Z * shade, color.W);
    }

    private static void DrawSphere(LevelEditorRenderContext context, Vector3 center, float radius, Vector4 color, Vector3 toLight, int hitId)
    {
        const int rings = 6, segments = 10;
        var mesh = context.UIPrimitives.BuildMesh(color, hitId, Matrix4x4.Identity);
        for (int ring = 0; ring <= rings; ring++)
        {
            (float sinPolar, float cosPolar) = MathF.SinCos(MathF.PI * ring / rings);
            for (int segment = 0; segment <= segments; segment++)
            {
                (float sinAzimuth, float cosAzimuth) = MathF.SinCos(MathF.Tau * segment / segments);
                var normal = new Vector3(sinPolar * cosAzimuth, sinPolar * sinAzimuth, cosPolar);
                mesh.AddVertex(center + normal * radius, Shade(color, normal, toLight));
            }
        }
        //both windings, so culling never hides a face
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = ring * (segments + 1) + segment, b = a + segments + 1;
                mesh.AddTriangle(a, b, a + 1); mesh.AddTriangle(a, a + 1, b);
                mesh.AddTriangle(a + 1, b, b + 1); mesh.AddTriangle(a + 1, b + 1, b);
            }
        }
    }

    private static void DrawCone(LevelEditorRenderContext context, Vector3 baseCenter, Vector3 direction, float length, float radius, Vector4 color,
        Vector3 toLight, int hitId)
    {
        const int sides = 10;
        (Vector3 u, Vector3 v) = LightProxyVisuals.Perpendiculars(direction);
        Vector3 tip = baseCenter + direction * length;
        Vector4 baseColor = Shade(color, -direction, toLight);
        var mesh = context.UIPrimitives.BuildMesh(color, hitId, Matrix4x4.Identity);
        for (int i = 0, vertex = 0; i < sides; i++, vertex += 6)
        {
            (float sinA, float cosA) = MathF.SinCos(MathF.Tau * i / sides);
            (float sinB, float cosB) = MathF.SinCos(MathF.Tau * (i + 1) / sides);
            Vector3 ringA = u * cosA + v * sinA, ringB = u * cosB + v * sinB;
            Vector3 a = baseCenter + ringA * radius, b = baseCenter + ringB * radius;
            Vector4 sideColor = Shade(color, Vector3.Normalize(Vector3.Normalize(ringA + ringB) * length + direction * radius), toLight);
            mesh.AddVertex(a, sideColor); mesh.AddVertex(b, sideColor); mesh.AddVertex(tip, sideColor);
            mesh.AddVertex(a, baseColor); mesh.AddVertex(b, baseColor); mesh.AddVertex(baseCenter, baseColor);
            for (int face = vertex; face <= vertex + 3; face += 3)
            {
                mesh.AddTriangle(face, face + 1, face + 2);
                mesh.AddTriangle(face, face + 2, face + 1);
            }
        }
    }

    /// <summary>A circle around <paramref name="center"/> facing the viewer: the outline of a sphere</summary>
    private static void DrawCircle(LevelEditorRenderContext context, Vector3 center, float radius, Vector4 color)
    {
        const int segments = 48;
        (Vector3 right, Vector3 up) = context.GetViewAxes();
        Vector3 previous = center + right * radius;
        for (int i = 1; i <= segments; i++)
        {
            (float sin, float cos) = MathF.SinCos(MathF.Tau * i / segments);
            Vector3 point = center + (right * cos + up * sin) * radius;
            context.UIPrimitives.AddLine(previous, point, color, NoHitId);
            previous = point;
        }
    }

    /// <summary>Starts dragging a grabber, from where the mouse grabbed it</summary>
    public void BeginDrag(LevelEditorRenderContext context, SplineTangentHandle handle, int x, int y)
    {
        if (Attach is null || Attach.IsReadOnly || handle is SplineTangentHandle.None) return;
        (Vector3 direction, float reach) = Placement(context, IsArrive(handle));
        (Vector3 rayOrigin, Vector3 rayDirection) = context.GetPixelRay(x, y);
        Vector3 center = Attach.Location;
        if (IsDirection(handle))
        {
            DragRadius = reach;
            DragNearSide = SplineTangentDrag.FacesViewer(center, center + direction * reach, rayDirection);
            Vector3 grabbed = SplineTangentDrag.SphereDirection(center, reach, rayOrigin, rayDirection, DragNearSide);
            DragGrabOffset = grabbed == Vector3.Zero ? Quaternion.Identity : SplineTangentDrag.Rotation(grabbed, direction);
        }
        else if (SplineTangentDrag.AxisDistance(center, direction, rayOrigin, rayDirection) is { } along)
        {
            DragStartDistance = along;
        }
        else return;
        DragHandle = handle;
        DragStartSettings = Attach.Settings;
        DragStartTangent = Attach.Node.WorldTangent;
    }

    public void Drag(LevelEditorRenderContext context, int x, int y)
    {
        if (Attach is null || DragHandle is SplineTangentHandle.None) return;
        (Vector3 rayOrigin, Vector3 rayDirection) = context.GetPixelRay(x, y);
        Vector3 center = Attach.Location;
        bool arrive = IsArrive(DragHandle);
        float length = DragStartTangent.Length();
        Vector3 side;
        if (IsDirection(DragHandle))
        {
            Vector3 hit = SplineTangentDrag.SphereDirection(center, DragRadius, rayOrigin, rayDirection, DragNearSide);
            if (hit == Vector3.Zero) return;
            side = Vector3.Normalize(Vector3.Transform(hit, DragGrabOffset));
            //a point without a tangent gets one as long as the grabber's reach
            if (length < MinimumTangent) length = DragRadius * 3;
        }
        else
        {
            Vector3 startSide = TangentDirection(DragStartTangent) * (arrive ? -1 : 1);
            if (SplineTangentDrag.AxisDistance(center, startSide, rayOrigin, rayDirection) is not { } along) return;
            side = startSide;
            length = MathF.Max(length + (along - DragStartDistance) * 3, MinimumTangent);
        }
        Attach.SetWorldTangent((arrive ? -side : side) * length, "tangent");
    }

    public void EndDrag(DragEndReason reason = DragEndReason.Completed)
    {
        if (DragHandle is not SplineTangentHandle.None && Attach is not null && Attach.Settings != DragStartSettings)
        {
            //restored while still dragging, so the editor doesn't record it as an edit
            if (reason is DragEndReason.Cancelled) Attach.RestoreSettings(DragStartSettings);
            else OnDragComplete?.Invoke(Attach, DragStartSettings, Attach.Settings);
        }
        DragHandle = SplineTangentHandle.None;
    }
}
