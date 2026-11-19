using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.LevelEditor;

public sealed partial class SplineActorProxy
{
    public override string DeleteRefusal => base.DeleteRefusal ?? NeighbourDeletionRefusal;

    // Asked whenever the Delete command is, so the loaded points are gone through once
    private string NeighbourDeletionRefusal
    {
        get
        {
            var points = LoadedPoints.ToList();
            var loaded = points.Select(p => p.Id).ToHashSet();
            if (connections.Any(c => !loaded.Contains(c.Target)) || linksFrom.Any(id => !loaded.Contains(id)))
                return "A spline point it's connected to isn't loaded (or doesn't exist), so its side of the connection can't be removed with it.";
            if (DeletionPoints(points).Any(p => p.OwningFile is null || p.OwningFile.IsReadOnly)) return "A connected point's file is read-only or closed.";
            return null;
        }
    }

    // Scan both edges and backlinks, including one-sided links left by an external refresh.
    private SplineActorProxy[] DeletionPoints(IEnumerable<SplineActorProxy> points = null) => (points ?? LoadedPoints).Where(p => p == this
        || IsConnectedTo(p) || p.IsConnectedTo(this) || linksFrom.Contains(p.Id) || p.linksFrom.Contains(Id)).ToArray();

    /// <summary>
    /// Also the properties the commit writes to other committed points before deleting it, without their connections to it or backlinks: its
    /// neighbours', whose connections deleting it removes, and any changed point's, whose stored connection to it may already have been removed
    /// </summary>
    public override DeletionRepairs GetDeletionRepairs()
    {
        var written = new Dictionary<ExportEntry, PropertyCollection>();
        var neighbours = DeletionPoints();
        foreach (var neighbour in LoadedPoints.Where(p => p != this && p.Export is not null && (p.IsDirty || neighbours.Contains(p))))
        {
            var properties = neighbour.Export.GetProperties();
            properties.GetProp<ArrayProperty<StructProperty>>("Connections")?.RemoveAll(c => c.GetProp<ObjectProperty>("ConnectTo")?.Value == Export.UIndex);
            properties.GetProp<ArrayProperty<ObjectProperty>>("LinksFrom")?.RemoveAll(l => l.Value == Export.UIndex);
            written[neighbour.Export] = properties;
        }
        return base.GetDeletionRepairs() with { WrittenProperties = written };
    }

    /// <summary>Also removes the point's incident edges, in both directions, in the same undo step. The commit writes the neighbours, then deletes the point</summary>
    public override IUndoAction Delete(ActorDeletionPlan plan)
    {
        if (DeleteRefusal is { } refusal) throw new InvalidOperationException(refusal);
        var points = DeletionPoints();
        var deletion = IsDraft ? null : PendingActorDeletion.Create(this, plan ?? throw new ArgumentNullException(nameof(plan)), points.Where(p => p != this).ToArray());
        var before = points.Select(p => (Point: p, State: p.CaptureTopology())).ToArray();
        var selection = Editor.RenderContext.SelectedActor;
        int sceneIndex = Editor.RenderContext.DrawList_3D.IndexOf(this);
        int fileIndex = OwningFile.Actors.IndexOf(this);
        var file = OwningFile;
        DeleteAction action = null;
        try
        {
            using var edit = new SceneEdit(Editor);
            edit.Do(() =>
            {
                foreach (var point in points)
                {
                    RemoveConnection(point);
                    point.RemoveConnection(this);
                }
                RefreshPoints(points);
            }, () => Restore(before));
            //the undo step holds the point from here: the view of a pending deletion, out of the scene, until it's committed or undone
            action = new DeleteAction(this, deletion, selection, sceneIndex, fileIndex, before,
                points.Select(p => (Point: p, State: p.CaptureTopology())).ToArray());
            edit.Detach(this);
            //listed only once it's out of the scene, so the change list never shows it twice
            if (deletion is not null) edit.Do(() => file.AddDeletion(deletion), () => file.RemoveDeletion(deletion));
            edit.Complete();
            return action;
        }
        catch
        {
            //after the edit has rolled back and reattached the point: letting go of a point out of the scene would dispose it
            action?.Dispose();
            throw;
        }
    }

    private sealed class DeleteAction : IUndoAction, IDisposable
    {
        private readonly SplineActorProxy point;
        private readonly PendingActorDeletion deletion;
        private readonly OpenLevelFile file;
        private readonly ActorProxy selection;
        private readonly (ActorRef Point, TopologyState State)[] before, after;
        private readonly IDisposable retainedPoint;
        private readonly int sceneIndex, fileIndex;

        public DeleteAction(SplineActorProxy point, PendingActorDeletion deletion, ActorProxy selection, int sceneIndex, int fileIndex,
            (SplineActorProxy Point, TopologyState State)[] before, (SplineActorProxy Point, TopologyState State)[] after)
        {
            this.point = point;
            this.deletion = deletion;
            file = point.OwningFile;
            this.selection = selection;
            this.sceneIndex = sceneIndex;
            this.fileIndex = fileIndex;
            this.before = before.Select(s => (new ActorRef(s.Point), s.State)).ToArray();
            this.after = after.Select(s => (new ActorRef(s.Point), s.State)).ToArray();
            retainedPoint = point.RetainForHistory();
        }

        public string Description => deletion is null ? "Delete new spline point" : "Delete spline point";
        //(including a point whose deletion was cancelled, which is disposed)
        public bool IsOrphaned => before.Any(s => s.Point.IsOrphaned);
        public IReadOnlyCollection<ActorId> Subjects => before.Select(s => s.Point.Id).ToArray();
        public IEnumerable<AssetReference> Assets => TopologyState.AssetsOf(before.Concat(after));

        public void Undo() => Apply(deleted: false);
        public void Redo() => Apply(deleted: true);

        private void Apply(bool deleted)
        {
            // Resolve everything before changing the scene; a replaced neighbour invalidates the entire operation.
            var target = Resolve(deleted ? after : before);
            var previous = target.Select(s => (s.Point, s.Point.CaptureTopology())).ToArray();
            using var edit = new SceneEdit(point.Editor);
            // The deletion is listed only while the point is out of the scene, so the change list never shows it twice
            if (!deleted)
            {
                if (deletion is not null) edit.Do(() => file.RemoveDeletion(deletion), () => file.AddDeletion(deletion));
                edit.Attach(point, sceneIndex, fileIndex);
            }
            edit.Do(() => Restore(target), () => Restore(previous));
            if (deleted)
            {
                edit.Detach(point);
                if (deletion is not null) edit.Do(() => file.AddDeletion(deletion), () => file.RemoveDeletion(deletion));
            }
            else edit.Reselect(selection);
            edit.Complete();
        }

        private static (SplineActorProxy Point, TopologyState State)[] Resolve((ActorRef Point, TopologyState State)[] states) =>
            states.Select(s => ((SplineActorProxy)s.Point.View, s.State)).ToArray();

        public void Dispose() => retainedPoint.Dispose();
    }
}
