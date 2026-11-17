using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
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

    /// <summary>
    /// A draft point, drawn from its own values alone: a copy, or a new point made from its class's defaults (<see cref="ActorCreation"/>), unconnected,
    /// and a loft with no mesh. Its package is untouched
    /// </summary>
    internal SplineActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties) : base(context, draft, properties)
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
        // As a commit writes them: only where they differ from the defaults
        var props = new PropertyCollection();
        if (source.location != Vector3.Zero) props.Add(CommonStructs.Vector3Prop(source.location, LocationPropertyName(source.Pcc.Game)));
        if (!source.rotation.IsZero) props.Add(CommonStructs.RotatorProp(source.rotation, "Rotation"));
        if (source.drawScale != 1) props.Add(new FloatProperty(source.drawScale, "DrawScale"));
        if (source.drawScale3D != source.defaultDrawScale3D) props.Add(CommonStructs.Vector3Prop(source.drawScale3D, "DrawScale3D"));
        if (source.prePivot != Vector3.Zero) props.Add(CommonStructs.Vector3Prop(source.prePivot, "PrePivot"));
        foreach (string copied in CopiedProperties)
        {
            if (source.Properties.GetProp<Property>(copied) is { } prop) props.AddOrReplaceProp(prop.DeepClone());
        }
        source.settings.Write(props, source.defaults);
        ActorRegistry registry = context.RenderContext.Registry;
        IEntry actorClass = source.Export?.Class ?? registry[source.Id].Class;
        ActorRecord record = registry.CreateDraft(source.Pcc, actorClass, name, isSpline: true, source.IsLoft);
        var copy = new SplineActorProxy(context, record, props) { OwningFile = source.OwningFile };
        //its mesh choices, which may be another file's assets, are pending, as everything of a draft's is, so its commit writes them
        if (source.IsLoft) copy.ChooseMeshesOf(source);
        return copy;
    }

    private protected override bool WritesAllValuesWhenCreated => true;

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
    protected override bool HasUncommittedChanges() => base.HasUncommittedChanges() || settings != cleanSettings || connections.Any(c => c.IsRebuilt)
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
            if (MeshConfigurationDirty && !IsDraft) status += " Reassigned meshes keep their existing baked lighting until a rebake.";
            if (IsLoft && deformMesh is null) status += " Assign a DeformMesh before adding outgoing connections or reversing incoming ones.";
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
            primitives.AddLine(previous, point, color, BatchedPrimitives.NoHitId);
            previous = point;
        }
        if (!TryGetDirectionArrow(curve, out Vector3 center, out Vector3 direction)) return;
        // A small camera-facing arrowhead, in pixels, on the same depth layer as its curve. It doesn't pick or cover the point markers.
        Vector3 side = Vector3.Cross(direction, context.Camera.CameraForward);
        side = side.LengthSquared() > 1e-8f ? Vector3.Normalize(side) : LightProxyVisuals.Perpendiculars(direction).U;
        float size = 10 * context.WorldUnitsPerPixel(center);
        Vector3 tip = center + direction * (size / 2);
        Vector3 tail = center - direction * (size / 2);
        primitives.AddLine(tip, tail + side * (size * 0.4f), color, BatchedPrimitives.NoHitId);
        primitives.AddLine(tip, tail - side * (size * 0.4f), color, BatchedPrimitives.NoHitId);
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

/// <summary>A spline point's marker as the tangent gizmo draws it over the scene, which selects the point</summary>
public class SplinePointHitProxy(SplineActorProxy point) : IHitProxy
{
    public SplineActorProxy Point { get; } = point;
    public int HitID { get; set; }
    public int HitPriority => IHitProxy.UIPriority;
}
