using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

public sealed partial class SplineActorProxy
{
    private List<Connection> cleanConnections = [];
    //LinksFrom, in order, by actor id: the points with a connection to this one
    private List<ActorId> linksFrom = [], cleanLinksFrom = [];
    private int deformMesh, cleanDeformMesh;
    private int[] deformMaterials = [], cleanDeformMaterials = [];
    private readonly HashSet<Connection> connectionResources = [];

    private void InitializeConnections()
    {
        cleanConnections = connections.ToList();
        connectionResources.UnionWith(connections);
        foreach (var connection in connections.Where(c => c.MeshExport is not null))
        {
            try { connection.NeedsRebake = connection.MeshExport.GetBinaryData<StaticMeshComponent>().LODData.Length == 0; }
            catch { /* Older or incomplete components are reported by their rendering coverage. */ }
        }
        linksFrom = Properties.GetProp<ArrayProperty<ObjectProperty>>("LinksFrom")?.Select(p => Registry.IdForStored(Pcc, p.Value)).ToList() ?? [];
        cleanLinksFrom = linksFrom.ToList();
        deformMesh = cleanDeformMesh = Properties.GetProp<ObjectProperty>("DeformMesh")?.Value ?? 0;
        deformMaterials = Properties.GetProp<ArrayProperty<ObjectProperty>>("DeformMeshMaterials")?.Select(p => p.Value).ToArray() ?? [];
        cleanDeformMaterials = deformMaterials.ToArray();
    }

    private bool OutgoingTopologyDirty => !connections.SequenceEqual(cleanConnections);
    private bool TopologyDirty => OutgoingTopologyDirty || !linksFrom.SequenceEqual(cleanLinksFrom);
    private bool MeshConfigurationDirty => deformMesh != cleanDeformMesh || !deformMaterials.SequenceEqual(cleanDeformMaterials);
    /// <summary>LinksFrom as it would be stored: a draft point, which has no export yet, is 0</summary>
    public IReadOnlyList<int> LinksFrom => linksFrom.Select(Registry.StoredUIndexOrZero).ToList();
    public IReadOnlyList<ActorId> LinkedFrom => linksFrom.AsReadOnly();
    public IEnumerable<SplineConnectionItem> OutgoingConnections => connections.Select(c => new SplineConnectionItem(this, FindPoint(c.Target), Registry.NameOf(c.Target)));
    public IEnumerable<SplineConnectionItem> IncomingConnections => IncomingPoints.Select(p => new SplineConnectionItem(p, this, Registry.NameOf(p.Id)));
    public int DeformMesh => deformMesh;
    public IReadOnlyList<int> DeformMeshMaterials => Array.AsReadOnly(deformMaterials);
    public string DeformMeshName => Pcc.GetEntry(deformMesh)?.InstancedFullPath ?? "None";
    public bool CanClearDeformMesh => connections.Count == 0;
    public IEnumerable<SplineMaterialSlot> MaterialSlots => deformMaterials.Select((i, slot) => new SplineMaterialSlot(this, slot, Pcc.GetEntry(i)?.InstancedFullPath ?? "Mesh material"));

    private bool CanEditWith(SplineActorProxy other) => other is not null && other != this && !IsReadOnly && !other.IsReadOnly
        && !IsDisposed && !other.IsDisposed && Pcc == other.Pcc;

    /// <summary>Cooked lofts require a mesh for every outgoing connection; mixed point types are unsupported.</summary>
    public bool CanConnectTo(SplineActorProxy other) => CanEditWith(other) && IsLoft == other.IsLoft && (!IsLoft || deformMesh != 0);

    public bool IsConnectedTo(SplineActorProxy other) => other is not null && connections.Any(c => c.Target == other.Id);

    private void AddConnection(SplineActorProxy target)
    {
        if (IsConnectedTo(target)) return;
        var connection = new Connection(target.Id, null, null);
        connections.Add(connection);
        connectionResources.Add(connection);
        if (!target.linksFrom.Contains(Id)) target.linksFrom.Add(Id);
    }

    private void RemoveConnection(SplineActorProxy target)
    {
        connections.RemoveAll(c => c.Target == target.Id);
        target.linksFrom.RemoveAll(i => i == Id);
    }

    /// <summary>ASplineActor::AddConnectionTo, without flipping an existing connection.</summary>
    public IUndoAction AddConnectionTo(SplineActorProxy target) => !CanConnectTo(target) || IsConnectedTo(target) ? null
        : EditConnections("Connect spline points", [this, target], () => AddConnection(target));

    /// <summary>UEditorEngine::SplineConnect: connect the first point to the second, or flip their existing connection.</summary>
    public IUndoAction ConnectOrFlip(SplineActorProxy target)
    {
        if (!CanEditWith(target)) return null;
        if (IsConnectedTo(target) ? !target.CanConnectTo(this) : !CanConnectTo(target)) return null;
        return EditConnections("Connect or reverse spline", [this, target], () =>
        {
            if (IsConnectedTo(target)) { RemoveConnection(target); target.AddConnection(this); }
            else if (target.IsConnectedTo(this)) { target.RemoveConnection(this); AddConnection(target); }
            else AddConnection(target);
        });
    }

    /// <summary>UEditorEngine::SplineBreak: break the first connection found in either direction.</summary>
    public IUndoAction BreakConnection(SplineActorProxy target)
    {
        if (!CanEditWith(target) || !IsConnectedTo(target) && !target.IsConnectedTo(this)) return null;
        return EditConnections("Break spline connection", [this, target], () =>
        {
            if (IsConnectedTo(target)) RemoveConnection(target);
            else target.RemoveConnection(this);
        });
    }

    /// <summary>ASplineActor::BreakAllConnections, in both directions.</summary>
    public IUndoAction BreakAllConnections()
    {
        var points = connections.Select(c => FindPoint(c.Target)).Concat(IncomingPoints).Append(this).Distinct().ToList();
        // Never partly edit a network whose other end isn't loaded or writable.
        if (IsReadOnly || linksFrom.Any(i => !LoadedPoints.Any(p => p.Id == i))
            || points.Any(p => p is null || p.IsReadOnly) || points.Count == 1 && connections.Count == 0) return null;
        return EditConnections("Break all spline connections", points, () =>
        {
            foreach (var point in points.Where(p => p != this)) { RemoveConnection(point); point.RemoveConnection(this); }
        });
    }

    /// <summary>UEditorEngine::SplineReverseAllDirections, with the selected point's Network as the selection.</summary>
    public IUndoAction ReverseWholeSpline()
    {
        var points = Network();
        if (points.Any(p => p.IsReadOnly || p.connections.Any(c => p.FindPoint(c.Target) is null)
            || p.linksFrom.Any(i => !points.Any(n => n.Id == i)))) return null;
        if (points.Any(p => p.connections.Any(c => !p.FindPoint(c.Target).CanConnectTo(p)))) return null;
        return EditConnections("Reverse whole spline", points, () =>
        {
            var edges = points.SelectMany(p => p.connections.Select(c => (From: p, To: p.FindPoint(c.Target)))).ToList();
            foreach (var point in points) point.settings = point.settings with { Tangent = -point.settings.Tangent };
            // Reverse the set together: interleaving removal and addition would collapse reciprocal edges.
            foreach (var (from, to) in edges) from.RemoveConnection(to);
            foreach (var (from, to) in edges) to.AddConnection(from);
        });
    }

    /// <summary>UEditorEngine::SplineStraightTangents: the chord transformed by each point's inverse rotation, ignoring scale.</summary>
    public IUndoAction StraightTangents(SplineActorProxy target)
    {
        if (!CanEditWith(target) || !IsConnectedTo(target) && !target.IsConnectedTo(this)) return null;
        var start = target.IsConnectedTo(this) ? target : this;
        var end = start == this ? target : this;
        return EditConnections("Straight spline tangents", [start, end], () =>
        {
            Vector3 chord = end.Location - start.Location;
            foreach (var point in new[] { start, end })
            {
                Matrix4x4.Invert(SplineLoftGeometry.ActorLocalToWorld(Vector3.Zero, point.Rotation, Vector3.One, Vector3.Zero), out var inverse);
                point.settings = point.settings with { Tangent = Vector3.TransformNormal(chord, inverse) };
            }
        });
    }

    /// <summary>Changes the loft's mesh and material overrides and refreshes its components, keeping exports until Commit Changes.</summary>
    public IUndoAction SetDeformMesh(int mesh, IEnumerable<int> materials)
    {
        if (!IsLoft || IsReadOnly || mesh != 0 && Pcc.GetEntry(mesh)?.IsA("StaticMesh") != true) return null;
        if (mesh == 0 && !CanClearDeformMesh) return null;
        int[] values = materials.ToArray();
        if (values.Any(i => i != 0 && Pcc.GetEntry(i)?.IsA("MaterialInterface") != true)) return null;
        return EditConnections("Edit spline mesh", [this], () => { deformMesh = mesh; deformMaterials = values; });
    }

    /// <summary>
    /// A point's connections (the connection objects themselves, which keep their components, meshes' lighting models and defects), backlinks,
    /// spline values and mesh choices, as a topology step restores them. Not its transform
    /// </summary>
    private sealed record TopologyState(Connection[] Connections, ActorId[] LinksFrom, SplineLoftSettings Settings, int DeformMesh, int[] DeformMaterials);

    private TopologyState CaptureTopology() => new([.. connections], [.. linksFrom], settings, deformMesh, [.. deformMaterials]);

    private void RestoreTopology(TopologyState state)
    {
        connections.Clear();
        connections.AddRange(state.Connections);
        linksFrom = [.. state.LinksFrom];
        settings = state.Settings;
        deformMesh = state.DeformMesh;
        deformMaterials = [.. state.DeformMaterials];
    }

    /// <summary>The topology is as <paramref name="state"/> recorded it, so an edit that leaves it so changed nothing</summary>
    private bool TopologyMatches(TopologyState state) => state.Connections.SequenceEqual(connections)
        && state.LinksFrom.SequenceEqual(linksFrom) && state.Settings == settings && state.DeformMesh == deformMesh
        && state.DeformMaterials.SequenceEqual(deformMaterials);

    /// <summary>A topology or mesh edit of several points</summary>
    private sealed class ConnectionAction(string description, (ActorRef Point, TopologyState State)[] before, (ActorRef Point, TopologyState State)[] after) : IUndoAction
    {
        public string Description => description;
        public bool IsOrphaned => before.Any(s => s.Point.IsOrphaned);
        public IReadOnlyCollection<ActorId> Subjects => before.Select(s => s.Point.Id).ToArray();
        public void Undo() => Restore(before);
        public void Redo() => Restore(after);
        private static void Restore((ActorRef Point, TopologyState State)[] states)
        {
            //every point is found before any changes, so a missing one fails the step without changing the others
            var points = states.Select(s => (SplineActorProxy)s.Point.View).ToArray();
            for (int i = 0; i < points.Length; i++) points[i].RestoreTopology(states[i].State);
            RefreshPoints(points);
        }
    }

    private IUndoAction EditConnections(string description, IEnumerable<SplineActorProxy> points, Action edit)
    {
        var before = points.Distinct().Select(p => (Point: p, State: p.CaptureTopology())).ToArray();
        edit();
        before = before.Where(s => !s.Point.TopologyMatches(s.State)).ToArray();
        if (before.Length == 0) return null;
        RefreshPoints(before.Select(s => s.Point));
        var action = new ConnectionAction(description, before.Select(s => (new ActorRef(s.Point), s.State)).ToArray(),
            before.Select(s => (new ActorRef(s.Point), s.Point.CaptureTopology())).ToArray());
        Editor?.OnSplineConnectionsEdited(action);
        return action;
    }

    private static void RefreshPoints(IEnumerable<SplineActorProxy> points)
    {
        var changed = points.Distinct().ToList();
        foreach (var point in changed)
        {
            point.SyncConnectionMeshes();
            point.Point.NotifySettings();
            point.OnPropertyChanged(nameof(OutgoingConnections));
            point.OnPropertyChanged(nameof(IncomingConnections));
            point.OnPropertyChanged(nameof(DeformMeshName));
            point.OnPropertyChanged(nameof(CanClearDeformMesh));
            point.OnPropertyChanged(nameof(MaterialSlots));
        }
        foreach (var point in changed.Concat(changed.SelectMany(p => p.IncomingPoints)).Distinct()) point.RefreshConnections();
    }

    private void ReleaseConnectionMesh(Connection connection)
    {
        if (connection.Mesh is null) return;
        connection.Lighting = connection.Mesh.ExistingLighting ?? connection.Lighting;
        Components.Remove(connection.Mesh); SplineMeshComps.Remove(connection.Mesh);
        connection.Mesh.Dispose(); connection.Mesh = null;
    }

    private string MeshConfiguration => $"{deformMesh}:{string.Join(',', deformMaterials)}";

    private void WriteMeshConfiguration(PropertyCollection props)
    {
        props.AddOrReplaceProp(new ObjectProperty(deformMesh, "StaticMesh"));
        props.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(deformMaterials.Select(i => new ObjectProperty(i)), "Materials"));
    }

    private PropertyCollection NewMeshProperties()
    {
        var props = new PropertyCollection();
        WriteMeshConfiguration(props);
        props.Add(new ObjectProperty(Properties.GetProp<ObjectProperty>("MeshLightEnvironment")?.Value ?? 0, "LightEnvironment"));
        props.Add(new BoolProperty(!(Properties.GetProp<BoolProperty>("bMovable")?.Value ?? false), "bUsePrecomputedShadows"));
        props.Add(new BoolProperty(Properties.GetProp<BoolProperty>("bAcceptsLights")?.Value ?? true, "bAcceptsLights"));
        float distance = Properties.GetProp<FloatProperty>("MeshMaxDrawDistance")?.Value ?? 0;
        props.Add(new FloatProperty(distance, "LDMaxDrawDistance"));
        props.Add(new FloatProperty(distance, "CachedMaxDrawDistance"));
        return props;
    }

    private IEntry ComponentClass(string name) => Pcc.Exports.Cast<IEntry>().Concat(Pcc.Imports)
        .FirstOrDefault(e => e.ObjectName.Name == name && e.ClassName == "Class");

    private void SyncConnectionMeshes()
    {
        foreach (var connection in connectionResources)
        {
            if (!connections.Contains(connection)) { ReleaseConnectionMesh(connection); continue; }
            bool original = !MeshConfigurationDirty && connection.MeshExport is not null;
            bool wantsMesh = IsLoft && Registry[connection.Target]?.IsLoft == true
                && (original || deformMesh != 0 && (MeshConfigurationDirty || !cleanConnections.Contains(connection) || connection.SplineComponent is null));
            string configuration = original ? "stored" : MeshConfiguration;
            if (connection.Mesh is not null && connection.PreviewConfiguration is null) connection.PreviewConfiguration = "stored";
            if (!wantsMesh || connection.PreviewConfiguration != configuration) ReleaseConnectionMesh(connection);
            if (!wantsMesh) continue;
            if (connection.Mesh is null)
            {
                ExportEntry preview = connection.MeshExport;
                PropertyCollection pending = null;
                if (!original)
                {
                    // None already exists in every package. Pending properties bypass serialization, so even the package's name table stays untouched.
                    preview ??= new ExportEntry(Pcc, Export, "None", new byte[8], binary: StaticMeshComponent.Create());
                    pending = connection.MeshExport?.GetProperties() ?? NewMeshProperties();
                    WriteMeshConfiguration(pending);
                }
                connection.Mesh = new SplineMeshComponentProxy(Editor.RenderContext, preview, this, pending, connection.MeshExport is not null, connection.Lighting);
                Components.Add(connection.Mesh); SplineMeshComps.Add(connection.Mesh);
                connection.PreviewConfiguration = configuration;
            }
            connection.MeshDefect = null;
            connection.Mesh.ActorPropertyName = $"SplineMeshComps[{connections.IndexOf(connection)}]";
        }
    }

    private ExportEntry NewComponent(string type, PropertyCollection props, ObjectBinary binary = null)
    {
        var rop = new RelinkerOptionsPackage();
        var componentClass = ComponentClass(type) ?? EntryImporter.EnsureClassIsInFile(Pcc, type, rop);
        if (rop.RelinkReport.Any()) throw new InvalidOperationException($"Could not import {type}: {string.Join(", ", rop.RelinkReport)}");
        var component = new ExportEntry(Pcc, Export, Pcc.GetNextIndexedName(type), new byte[8], props, binary) { Class = componentClass };
        Pcc.AddExport(component);
        return component;
    }

    /// <summary>
    /// What a commit wrote for a connection, which it takes on only once the commit is accepted (<see cref="AcceptCommittedConnections"/>):
    /// the components it's written to (created by the commit if they're new), and whether its mesh was written from the preview, which is then rebuilt from them
    /// </summary>
    private readonly record struct CommittedConnection(ExportEntry SplineComponent, ExportEntry MeshExport, bool NewMesh, bool MeshWritten);
    private Dictionary<Connection, CommittedConnection> committed;

    /// <summary>Writes the connections' components and references. Changes nothing this view holds: see <see cref="CommittedConnection"/></summary>
    private void CommitConnections()
    {
        committed = [];
        bool referencesChanged = false;
        foreach (var connection in connections)
        {
            var (splineComponent, meshExport, newMesh, meshWritten) = (connection.SplineComponent, connection.MeshExport, false, false);
            if (splineComponent is null && FindPoint(connection.Target) is { } target)
            {
                // Cooked siblings provide ReplacementPrimitive and LightingChannels, without copying a curve or references to another owner.
                var template = Pcc.Exports.FirstOrDefault(e => e.ClassName == "SplineComponent");
                var props = new PropertyCollection();
                foreach (string name in new[] { "ReplacementPrimitive", "LightingChannels" })
                    if (template?.GetProperties().GetProp<Property>(name) is { } property) props.Add(property.DeepClone());
                props.AddOrReplaceProp(new ObjectProperty(0, "ReplacementPrimitive"));
                SplineLoftGeometry.WriteSplineComponent(props, Node, target.Node, Export.Game);
                splineComponent = NewComponent("SplineComponent", props);
                referencesChanged = true;
            }
            if (splineComponent is not null && (connection.CurveRebuilt || TopologyDirty))
            {
                var props = splineComponent.GetProperties();
                props.AddOrReplaceProp(new BoolProperty(FindPoint(connection.Target)?.Properties.GetProp<BoolProperty>("bDisableDestination")?.Value ?? false, "bSplineDisabled"));
                props.AddOrReplaceProp(new BoolProperty(Properties.GetProp<BoolProperty>("bHidden")?.Value ?? false, "HiddenGame"));
                var color = Properties.GetProp<StructProperty>("SplineColor")
                    ?? EffectiveLightingProperties.ClassDefaultProperty<StructProperty>(Export, "SplineColor", Editor.PackageCache)
                    ?? CommonStructs.ColorProp(System.Drawing.Color.Magenta, "SplineColor");
                props.AddOrReplaceProp(color.DeepClone());
                splineComponent.WriteProperties(props);
            }
            if (connection.Mesh is not null && (meshExport is null || MeshConfigurationDirty))
            {
                var deformation = connection.Mesh.Deformation;
                var props = meshExport?.GetProperties() ?? NewMeshProperties();
                WriteMeshConfiguration(props);
                deformation?.Write(props);
                if (meshExport is null)
                {
                    meshExport = NewComponent("SplineMeshComponent", props, StaticMeshComponent.Create());
                    newMesh = referencesChanged = true;
                }
                else meshExport.WriteProperties(props);
                meshWritten = true;
            }
            committed[connection] = new(splineComponent, meshExport, newMesh, meshWritten);
        }
        // Trashed after the new components are added, so none of them is compacted away or takes a trashed slot within this commit
        foreach (var connection in cleanConnections.Where(c => !connections.Contains(c)))
        {
            if (connection.SplineComponent is not null) EntryPruner.TrashEntryAndDescendants(connection.SplineComponent);
            if (connection.MeshExport is not null) EntryPruner.TrashEntryAndDescendants(connection.MeshExport);
        }
        if (TopologyDirty || referencesChanged)
        {
            Properties.AddOrReplaceProp(new ArrayProperty<StructProperty>(connections.Select(c => new StructProperty("SplineConnection",
                [new ObjectProperty(committed[c].SplineComponent?.UIndex ?? 0, "SplineComponent"), new ObjectProperty(Registry.StoredUIndex(c.Target), "ConnectTo")])), "Connections"));
            Properties.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(linksFrom.Select(i => new ObjectProperty(Registry.StoredUIndex(i))), "LinksFrom"));
        }
        if (IsLoft && (TopologyDirty || MeshConfigurationDirty || referencesChanged))
        {
            Properties.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(connections.Select(c => new ObjectProperty(committed[c].MeshExport.UIndex)), "SplineMeshComps"));
            Properties.AddOrReplaceProp(new ObjectProperty(deformMesh, "DeformMesh"));
            Properties.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(deformMaterials.Select(i => new ObjectProperty(i)), "DeformMeshMaterials"));
        }
    }

    /// <summary>
    /// Writes the connection meshes' pending lighting edits to the components the commit wrote them to: the preview's lighting model belongs to a
    /// preview export when the mesh is new. Written after the point's own properties, because assigning a new light environment registers it in
    /// the point's Components, which writing the point's (older) properties would undo
    /// </summary>
    private void CommitConnectionLighting()
    {
        foreach (var connection in connections)
            if (connection.Mesh?.ExistingLighting is { } lighting && committed[connection].MeshExport is { } meshExport) lighting.Write(meshExport);
    }

    /// <summary>Takes on what <see cref="CommitConnections"/> wrote: binds the connections to their components and rebuilds the meshes it wrote from them</summary>
    private void AcceptCommittedConnections()
    {
        if (committed is null) return;
        foreach (var connection in cleanConnections.Where(c => !connections.Contains(c)))
        {
            ReleaseConnectionMesh(connection);
            connection.SplineComponent = null; connection.MeshExport = null;
        }
        foreach (var connection in connections)
        {
            if (!committed.TryGetValue(connection, out var written)) continue;
            connection.SplineComponent = written.SplineComponent;
            connection.MeshExport = written.MeshExport;
            if (written.NewMesh) connection.NeedsRebake = true;
            if (written.MeshWritten)
            {
                ReleaseConnectionMesh(connection);
                connection.Mesh = new SplineMeshComponentProxy(Editor.RenderContext, connection.MeshExport, this, lighting: connection.Lighting);
                connection.Mesh.ActorPropertyName = $"SplineMeshComps[{connections.IndexOf(connection)}]";
                Components.Add(connection.Mesh); SplineMeshComps.Add(connection.Mesh);
                connection.PreviewConfiguration = "stored";
            }
            connection.Mesh?.ExistingLighting?.MarkClean();
        }
        committed = null;
    }

    private string ConnectionCommitProblem
    {
        get
        {
            if (connections.Any(c => Registry[c.Target] is { IsUnresolved: false } target && target.IsLoft != IsLoft))
                return "Plain and loft spline points cannot connect. Break the mixed connection first.";
            if (connections.Any(c => Registry[c.Target]?.IsDraft == true) || linksFrom.Any(i => Registry[i]?.IsDraft == true))
                return "It's connected to a new point that hasn't been committed, and new points can't be committed yet.";
            if (IsLoft && (connections.Any(c => c.Mesh is null || c.MeshExport is { } mesh && !mesh.IsA("SplineMeshComponent"))
                || !TopologyDirty && !MeshConfigurationDirty && Properties.GetProp<ArrayProperty<ObjectProperty>>("SplineMeshComps")?
                    .Any(p => p.ResolveToEntry(Pcc) is not ExportEntry mesh || !mesh.IsA("SplineMeshComponent")) == true))
                return "Every outgoing loft connection needs a mesh component. Assign a DeformMesh or break the connection first.";
            return null;
        }
    }

    /// <summary>Checks topology before any commit writes; LE3 loft PostLoad (RVA 0x3A2390) cannot load null mesh slots.</summary>
    public void ValidateConnectionsForCommit()
    {
        if (ConnectionCommitProblem is { } problem) throw new InvalidOperationException($"{DisplayText}: {problem}");
    }

    private void AcceptConnections()
    {
        cleanConnections = connections.ToList(); cleanLinksFrom = linksFrom.ToList();
        cleanDeformMesh = deformMesh; cleanDeformMaterials = deformMaterials.ToArray();
    }

    public override bool TestUIndexes(HashSet<int> indexes) => base.TestUIndexes(indexes)
        || connections.Any(c => c.SplineComponent is not null && indexes.Contains(c.SplineComponent.UIndex));
}

/// <summary>A directed connection in the single selected point's details panel.</summary>
public sealed class SplineConnectionItem(SplineActorProxy source, SplineActorProxy target, string targetName)
{
    public SplineActorProxy Source { get; } = source;
    public SplineActorProxy Target { get; } = target;
    public string Name => targetName ?? "Missing destination";
    public bool IsEditable => Target is not null && !Source.IsReadOnly && !Target.IsReadOnly;
    public bool CanReverse => IsEditable && Target.CanConnectTo(Source);
    public void Break() => Source.BreakConnection(Target);
    public void Reverse() => Source.ConnectOrFlip(Target);
    public void Straighten() => Source.StraightTangents(Target);
}

public sealed record SplineMaterialSlot(SplineActorProxy Point, int Index, string Name);
