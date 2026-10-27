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
    private List<int> linksFrom = [], cleanLinksFrom = [];
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
        linksFrom = Properties.GetProp<ArrayProperty<ObjectProperty>>("LinksFrom")?.Select(p => p.Value).ToList() ?? [];
        cleanLinksFrom = linksFrom.ToList();
        deformMesh = cleanDeformMesh = Properties.GetProp<ObjectProperty>("DeformMesh")?.Value ?? 0;
        deformMaterials = Properties.GetProp<ArrayProperty<ObjectProperty>>("DeformMeshMaterials")?.Select(p => p.Value).ToArray() ?? [];
        cleanDeformMaterials = deformMaterials.ToArray();
    }

    private bool OutgoingTopologyDirty => !connections.SequenceEqual(cleanConnections);
    private bool TopologyDirty => OutgoingTopologyDirty || !linksFrom.SequenceEqual(cleanLinksFrom);
    private bool MeshConfigurationDirty => deformMesh != cleanDeformMesh || !deformMaterials.SequenceEqual(cleanDeformMaterials);
    public IReadOnlyList<int> LinksFrom => linksFrom.AsReadOnly();
    public IEnumerable<SplineConnectionItem> OutgoingConnections => connections.Select(c => new SplineConnectionItem(this, FindPoint(c.Target), c.Target));
    public IEnumerable<SplineConnectionItem> IncomingConnections => IncomingPoints.Select(p => new SplineConnectionItem(p, this, p.Export));
    public int DeformMesh => deformMesh;
    public IReadOnlyList<int> DeformMeshMaterials => Array.AsReadOnly(deformMaterials);
    public string DeformMeshName => Pcc.GetEntry(deformMesh)?.InstancedFullPath ?? "None";
    public bool CanClearDeformMesh => connections.Count == 0;
    public IEnumerable<SplineMaterialSlot> MaterialSlots => deformMaterials.Select((i, slot) => new SplineMaterialSlot(this, slot, Pcc.GetEntry(i)?.InstancedFullPath ?? "Mesh material"));

    private bool CanEditWith(SplineActorProxy other) => other is not null && other != this && !IsReadOnly && !other.IsReadOnly
        && !IsDisposed && !other.IsDisposed && Pcc == other.Pcc;

    /// <summary>Cooked lofts require a mesh for every outgoing connection; mixed point types are unsupported.</summary>
    public bool CanConnectTo(SplineActorProxy other) => CanEditWith(other) && IsLoft == other.IsLoft && (!IsLoft || deformMesh != 0);

    public bool IsConnectedTo(SplineActorProxy other) => other is not null && connections.Any(c => c.Target == other.Export);

    private void AddConnection(SplineActorProxy target)
    {
        if (IsConnectedTo(target)) return;
        var connection = new Connection(target.Export, null, null);
        connections.Add(connection);
        connectionResources.Add(connection);
        if (!target.linksFrom.Contains(Export.UIndex)) target.linksFrom.Add(Export.UIndex);
    }

    private void RemoveConnection(SplineActorProxy target)
    {
        connections.RemoveAll(c => c.Target == target.Export);
        target.linksFrom.RemoveAll(i => i == Export.UIndex);
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
        if (IsReadOnly || linksFrom.Any(i => !LoadedPoints.Any(p => p.Export.UIndex == i))
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
            || p.linksFrom.Any(i => !points.Any(n => n.Export.UIndex == i)))) return null;
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

    private sealed class State(SplineActorProxy point)
    {
        public readonly SplineActorProxy Point = point;
        private readonly Connection[] connections = point.connections.ToArray();
        private readonly int[] links = point.linksFrom.ToArray();
        private readonly SplineLoftSettings settings = point.settings;
        private readonly int mesh = point.deformMesh;
        private readonly int[] materials = point.deformMaterials.ToArray();

        public bool Matches() => connections.SequenceEqual(Point.connections) && links.SequenceEqual(Point.linksFrom)
            && settings == Point.settings && mesh == Point.deformMesh && materials.SequenceEqual(Point.deformMaterials);

        public void Restore()
        {
            Point.connections.Clear(); Point.connections.AddRange(connections);
            Point.linksFrom = links.ToList(); Point.settings = settings;
            Point.deformMesh = mesh; Point.deformMaterials = materials.ToArray();
        }
    }

    private sealed class ConnectionAction(string description, State[] before, State[] after) : IUndoAction
    {
        public string Description => description;
        public bool IsOrphaned => before.Any(s => s.Point.IsDisposed);
        public void Undo() => Restore(before);
        public void Redo() => Restore(after);
        private static void Restore(State[] states)
        {
            foreach (var state in states) state.Restore();
            RefreshPoints(states.Select(s => s.Point));
        }
    }

    private IUndoAction EditConnections(string description, IEnumerable<SplineActorProxy> points, Action edit)
    {
        var before = points.Distinct().Select(p => new State(p)).ToArray();
        edit();
        before = before.Where(s => !s.Matches()).ToArray();
        if (before.Length == 0) return null;
        RefreshPoints(before.Select(s => s.Point));
        var action = new ConnectionAction(description, before, before.Select(s => new State(s.Point)).ToArray());
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
            bool wantsMesh = IsLoft && connection.Target?.IsA("SplineLoftActor") == true
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

    private void CommitConnections()
    {
        bool referencesChanged = false;
        foreach (var connection in cleanConnections.Where(c => !connections.Contains(c)))
        {
            ReleaseConnectionMesh(connection);
            if (connection.SplineComponent is not null) EntryPruner.TrashEntryAndDescendants(connection.SplineComponent);
            if (connection.MeshExport is not null) EntryPruner.TrashEntryAndDescendants(connection.MeshExport);
            connection.SplineComponent = null; connection.MeshExport = null;
        }
        foreach (var connection in connections)
        {
            if (connection.SplineComponent is null && FindPoint(connection.Target) is { } target)
            {
                // Cooked siblings provide ReplacementPrimitive and LightingChannels, without copying a curve or references to another owner.
                var template = Pcc.Exports.FirstOrDefault(e => e.ClassName == "SplineComponent");
                var props = new PropertyCollection();
                foreach (string name in new[] { "ReplacementPrimitive", "LightingChannels" })
                    if (template?.GetProperties().GetProp<Property>(name) is { } property) props.Add(property.DeepClone());
                props.AddOrReplaceProp(new ObjectProperty(0, "ReplacementPrimitive"));
                SplineLoftGeometry.WriteSplineComponent(props, Node, target.Node, Export.Game);
                connection.SplineComponent = NewComponent("SplineComponent", props);
                referencesChanged = true;
            }
            if (connection.SplineComponent is not null && (connection.CurveRebuilt || TopologyDirty))
            {
                var props = connection.SplineComponent.GetProperties();
                props.AddOrReplaceProp(new BoolProperty(FindPoint(connection.Target)?.Properties.GetProp<BoolProperty>("bDisableDestination")?.Value ?? false, "bSplineDisabled"));
                props.AddOrReplaceProp(new BoolProperty(Properties.GetProp<BoolProperty>("bHidden")?.Value ?? false, "HiddenGame"));
                var color = Properties.GetProp<StructProperty>("SplineColor")
                    ?? EffectiveLightingProperties.ClassDefaultProperty<StructProperty>(Export, "SplineColor", Editor.PackageCache)
                    ?? CommonStructs.ColorProp(System.Drawing.Color.Magenta, "SplineColor");
                props.AddOrReplaceProp(color.DeepClone());
                connection.SplineComponent.WriteProperties(props);
            }
            if (connection.Mesh is not null && (connection.MeshExport is null || MeshConfigurationDirty))
            {
                var deformation = connection.Mesh.Deformation;
                var props = connection.MeshExport?.GetProperties() ?? NewMeshProperties();
                WriteMeshConfiguration(props);
                deformation?.Write(props);
                if (connection.MeshExport is null)
                {
                    connection.MeshExport = NewComponent("SplineMeshComponent", props, StaticMeshComponent.Create());
                    connection.NeedsRebake = true;
                    referencesChanged = true;
                }
                else connection.MeshExport.WriteProperties(props);
                ReleaseConnectionMesh(connection);
                connection.Mesh = new SplineMeshComponentProxy(Editor.RenderContext, connection.MeshExport, this, lighting: connection.Lighting);
                connection.Mesh.ActorPropertyName = $"SplineMeshComps[{connections.IndexOf(connection)}]";
                Components.Add(connection.Mesh); SplineMeshComps.Add(connection.Mesh);
                connection.PreviewConfiguration = "stored";
            }
        }
        if (TopologyDirty || referencesChanged)
        {
            Properties.AddOrReplaceProp(new ArrayProperty<StructProperty>(connections.Select(c => new StructProperty("SplineConnection",
                [new ObjectProperty(c.SplineComponent?.UIndex ?? 0, "SplineComponent"), new ObjectProperty(c.Target?.UIndex ?? 0, "ConnectTo")])), "Connections"));
            Properties.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(linksFrom.Select(i => new ObjectProperty(i)), "LinksFrom"));
        }
        if (IsLoft && (TopologyDirty || MeshConfigurationDirty || referencesChanged))
        {
            Properties.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(connections.Select(c => new ObjectProperty(c.MeshExport.UIndex)), "SplineMeshComps"));
            Properties.AddOrReplaceProp(new ObjectProperty(deformMesh, "DeformMesh"));
            Properties.AddOrReplaceProp(new ArrayProperty<ObjectProperty>(deformMaterials.Select(i => new ObjectProperty(i)), "DeformMeshMaterials"));
        }
    }

    private string ConnectionCommitProblem
    {
        get
        {
            if (connections.Any(c => c.Target is not null && c.Target.IsA("SplineLoftActor") != IsLoft))
                return "Plain and loft spline points cannot connect. Break the mixed connection first.";
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
public sealed class SplineConnectionItem(SplineActorProxy source, SplineActorProxy target, ExportEntry targetExport)
{
    public SplineActorProxy Source { get; } = source;
    public SplineActorProxy Target { get; } = target;
    public string Name => targetExport?.ObjectName.Instanced ?? "Missing destination";
    public bool IsEditable => Target is not null && !Source.IsReadOnly && !Target.IsReadOnly;
    public bool CanReverse => IsEditable && Target.CanConnectTo(Source);
    public void Break() => Source.BreakConnection(Target);
    public void Reverse() => Source.ConnectOrFlip(Target);
    public void Straighten() => Source.StraightTangents(Target);
}

public sealed record SplineMaterialSlot(SplineActorProxy Point, int Index, string Name);
