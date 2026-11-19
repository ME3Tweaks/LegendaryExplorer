using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Duplicating a spline point, and committing the new point a duplicate makes</summary>
public sealed partial class SplineActorProxy
{
    // Every property on the 4,101 spline points in LE3's shipping levels (the only game whose levels have any), by what a copy does with it.
    // LE1 and LE2 have the same spline classes; their Actor classes differ, and a point with an Actor property not listed here is refused there too.
    // Any other property blocks duplication, so a copy is only made of values whose meaning is known

    /// <summary>Copied as they are: values the copy owns outright</summary>
    private static readonly string[] CopiedProperties = ["Tag", "bHidden", "bDisableDestination", "SplineColor", "bCanStepUpOn", "Physics",
        "bEdShouldSnap", "SplineVelocityOverTime", "bMovable", "bAcceptsLights", "MeshMaxDrawDistance"];

    /// <summary>
    /// Written for the copy from the source's current values (transform, spline values, mesh choices), rebuilt for it (its connections),
    /// or left out (UniqueTag, which would no longer be unique)
    /// </summary>
    private static readonly HashSet<string> RecreatedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "location", "Rotation", "DrawScale", "DrawScale3D", "PrePivot", "SplineActorTangent", "Roll", "ScaleX", "ScaleY", "Offset", "WorldXDir",
        "bSmoothInterpRollAndScale", "DeformMesh", "DeformMeshMaterials", "Connections", "LinksFrom", "SplineMeshComps", "UniqueTag"
    };

    /// <summary>Why the point can't be duplicated now, or null</summary>
    public override string DuplicateRefusal
    {
        get
        {
            if (IsDisposed || Editor?.RenderContext.DrawList_3D.Contains(this) != true) return "It isn't in the scene.";
            if (IsReadOnly) return "Its file is read-only.";
            // The spline classes are the same in all three (LE3's SplineLoftActor adds only bCastShadow), and so is how their placed actors are made
            if (!Pcc.Game.IsLEGame()) return "Only the Legendary Edition's spline points are known well enough to copy.";
            if (ClassName is not ("SplineActor" or "SplineLoftActor")) return $"It's a {ClassName}, which can't be copied yet.";
            string[] unknown = Properties.Select(p => p.Name.Instanced)
                .Where(n => !RecreatedProperties.Contains(n) && !CopiedProperties.Contains(n, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (unknown.Length > 0) return $"It has {string.Join(", ", unknown)}, which a copy can't be made with yet.";
            // Whichever way the new connection goes, its mesh is the point's: its own, or the copy's copy of it
            if (IsLoft && (deformMesh is null || !IsAsset(deformMesh, "StaticMesh")))
                return "Its connection to the copy needs a DeformMesh. Assign one first.";
            return null;
        }
    }

    /// <summary>
    /// Starts duplicating the point: a draft copy is drawn at its place and selected, and connected to it. The point keeps all its connections and
    /// its mesh; the copy has the point's mesh and no other connections. Unlike UE3's editor (FEdModeSpline::ActorsDuplicatedNotify), which breaks
    /// the point's outgoing connections and always connects the point to the copy, the new connection follows the copy (<see cref="Duplication.Follow"/>),
    /// so a spline extends either way. Nothing is written to the package. Move the copy, then complete or cancel it
    /// </summary>
    /// <param name="offset">Where the copy starts from the point, before it's selected (so it isn't an edit of the copy)</param>
    public Duplication BeginDuplicate(Vector3 offset = default) => new(this, offset);

    public sealed class Duplication : IActorDuplication
    {
        private readonly SplineActorProxy source;
        private readonly ActorProxy selection;
        //the topology of the point and the copy, the only points the duplicate changes, before it
        private readonly (SplineActorProxy Point, TopologyState State)[] before;
        private bool finished;

        /// <summary>The draft copy. Move it; it's drawn, selected and editable until the duplicate completes or is cancelled</summary>
        public SplineActorProxy Copy { get; }
        ActorProxy IActorDuplication.Copy => Copy;

        /// <summary>
        /// Whether the copy is ahead of the point along the spline (its tangent), so the point connects to it. Behind it, the copy connects to the
        /// point instead. Ahead until it's moved
        /// </summary>
        public bool CopyIsAhead { get; private set; } = true;

        internal Duplication(SplineActorProxy source, Vector3 offset)
        {
            if (source.DuplicateRefusal is { } refusal) throw new InvalidOperationException($"{source.DisplayText} can't be duplicated. {refusal}");
            this.source = source;
            var editor = source.Editor;
            selection = editor.RenderContext.SelectedActor;
            Copy = CreateDraft(editor, source, source.Registry.ProposeName(source.Pcc, source.ClassName));
            //(the connection follows it once it's completed)
            Copy.Location += offset;
            before = [(source, source.CaptureTopology()), (Copy, Copy.CaptureTopology())];
            try
            {
                using var edit = new SceneEdit(editor);
                edit.Attach(Copy);
                edit.Do(() => Connect(ahead: true), () => Restore(before));
                edit.Select(Copy);
                edit.Complete();
            }
            catch
            {
                Copy.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Points the connection the way the copy has moved: from the point while the copy is ahead of it along the spline, to the point while it's
        /// behind. Call it after moving the copy. On the plane across the spline at the point, the connection stays as it is
        /// </summary>
        public void Follow()
        {
            ObjectDisposedException.ThrowIf(finished, this);
            float along = Vector3.Dot(Copy.Location - source.Location, source.Node.WorldTangent);
            if (along != 0 && along > 0 != CopyIsAhead) Connect(along > 0);
        }

        private void Connect(bool ahead)
        {
            source.RemoveConnection(Copy);
            Copy.RemoveConnection(source);
            if (ahead) source.AddConnection(Copy);
            else Copy.AddConnection(source);
            CopyIsAhead = ahead;
            RefreshPoints([source, Copy]);
        }

        /// <summary>
        /// Finishes the duplicate as one undo step, for the caller to record, with the connection following where the copy is. A copy left where
        /// the point is (exactly) isn't a duplicate: it's cancelled, and there's no step
        /// </summary>
        public IUndoAction Complete()
        {
            ObjectDisposedException.ThrowIf(finished, this);
            if (Copy.SnapshotTransform().Equals(source.SnapshotTransform()))
            {
                Cancel();
                return null;
            }
            DuplicateAction step;
            try
            {
                Follow();
                step = new DuplicateAction(source.Editor, Copy, selection, before, [.. before.Select(s => (s.Point, s.Point.CaptureTopology()))]);
            }
            catch
            {
                Cancel();
                throw;
            }
            finished = true;
            return step;
        }

        /// <summary>Puts everything back as it was before the duplicate began, and discards the copy</summary>
        public void Cancel()
        {
            ObjectDisposedException.ThrowIf(finished, this);
            finished = true;
            var now = before.Select(s => (s.Point, s.Point.CaptureTopology())).ToArray();
            using (var edit = new SceneEdit(source.Editor))
            {
                edit.Do(() => Restore(before), () => Restore(now));
                edit.Detach(Copy);
                edit.Select(selection is { IsDisposed: false } ? selection : null);
                edit.Complete();
            }
            Copy.Dispose();
        }
    }

    private static void Restore(IEnumerable<(SplineActorProxy Point, TopologyState State)> states)
    {
        var restored = states.ToArray();
        foreach (var (point, state) in restored) point.RestoreTopology(state);
        RefreshPoints(restored.Select(s => s.Point));
    }

    /// <summary>
    /// A completed duplicate. Undoing it takes the copy out of the scene, which this step then holds, and puts the topology back; redoing it attaches
    /// the same copy again. The copy is disposed once the history drops the step, unless it's drawn (committed, or redone)
    /// </summary>
    private sealed class DuplicateAction : IUndoAction, IDisposable
    {
        private readonly IActorEditorContext editor;
        private readonly SplineActorProxy copy;
        private readonly ActorProxy selection;
        private readonly (ActorRef Point, TopologyState State)[] before, after;
        private readonly IDisposable retainedCopy;

        public DuplicateAction(IActorEditorContext editor, SplineActorProxy copy, ActorProxy selection,
            (SplineActorProxy Point, TopologyState State)[] before, (SplineActorProxy Point, TopologyState State)[] after)
        {
            this.editor = editor;
            this.copy = copy;
            this.selection = selection;
            this.before = [.. before.Select(s => (new ActorRef(s.Point), s.State))];
            this.after = [.. after.Select(s => (new ActorRef(s.Point), s.State))];
            retainedCopy = copy.RetainForHistory();
        }

        public string Description => "Duplicate spline point";
        public bool IsOrphaned => before.Any(s => s.Point.IsOrphaned);
        public IReadOnlyCollection<ActorId> Subjects => before.Select(s => s.Point.Id).ToArray();
        public IEnumerable<AssetReference> Assets => TopologyState.AssetsOf(before.Concat(after));

        public void Undo()
        {
            //every point is found before any changes, so a missing one fails the step without changing the others
            var (topology, now) = (Resolve(before), Resolve(after));
            using var edit = new SceneEdit(editor);
            edit.Do(() => Restore(topology), () => Restore(now));
            edit.Detach(copy);
            edit.Select(selection is { IsDisposed: false } ? selection : null);
            edit.Complete();
        }

        public void Redo()
        {
            var (topology, now) = (Resolve(after), Resolve(before));
            using var edit = new SceneEdit(editor);
            edit.Attach(copy);
            edit.Do(() => Restore(topology), () => Restore(now));
            edit.Select(copy);
            edit.Complete();
        }

        private static (SplineActorProxy, TopologyState)[] Resolve((ActorRef Point, TopologyState State)[] states) =>
            [.. states.Select(s => ((SplineActorProxy)s.Point.View, s.State))];

        public void Dispose()
        {
            retainedCopy.Dispose();
        }
    }
}
