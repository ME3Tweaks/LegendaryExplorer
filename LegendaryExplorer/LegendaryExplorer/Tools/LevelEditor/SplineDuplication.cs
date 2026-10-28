using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Duplicating a spline point, and committing the new point a duplicate makes</summary>
public sealed partial class SplineActorProxy
{
    // Every property on the 4,101 spline points in LE3's shipping levels (the only game whose levels have any), by what a copy does with it.
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
    public string DuplicateRefusal => PlanDuplicate(out _);

    /// <param name="mesh">The DeformMesh the point has once it connects to its copy</param>
    private string PlanDuplicate(out int mesh)
    {
        mesh = deformMesh;
        if (IsDisposed || Editor?.RenderContext.DrawList_3D.Contains(this) != true) return "It isn't in the scene.";
        if (IsReadOnly) return "Its file is read-only.";
        if (Pcc.Game != MEGame.LE3) return "Only LE3's levels have spline points, so only LE3's are known well enough to copy.";
        if (ClassName is not ("SplineActor" or "SplineLoftActor")) return $"It's a {ClassName}, which can't be copied yet.";
        string[] unknown = Properties.Select(p => p.Name.Instanced)
            .Where(n => !RecreatedProperties.Contains(n) && !CopiedProperties.Contains(n, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (unknown.Length > 0) return $"It has {string.Join(", ", unknown)}, which a copy can't be made with yet.";
        // Its outgoing connections are broken, which changes the backlinks of the points they go to
        foreach (var connection in connections)
        {
            if (FindPoint(connection.Target) is not { IsReadOnly: false })
                return $"Its connection to {Registry.NameOf(connection.Target) ?? "an unloaded point"} is broken by duplicating, and that point isn't loaded or is read-only.";
        }
        if (!IsLoft) return null;
        // As UE3 does: the first incoming point's mesh, if that's a loft point. Never another incoming point's
        if (linksFrom.Count > 0)
        {
            if (FindPoint(linksFrom[0]) is not { } first) return "Its first incoming point isn't loaded, and it takes that point's mesh.";
            if (first.IsLoft) mesh = first.deformMesh;
        }
        if (mesh == 0 || Pcc.GetEntry(mesh)?.IsA("StaticMesh") != true)
            return mesh == deformMesh ? "Its connection to the copy needs a DeformMesh. Assign one first."
                : "Its connection to the copy needs a DeformMesh, and its first incoming point's, which it takes, is none.";
        return null;
    }

    /// <summary>
    /// Starts duplicating the point as UE3's editor does (FEdModeSpline::ActorsDuplicatedNotify): a draft copy is drawn at its place and selected;
    /// the point's outgoing connections are broken and it connects to the copy alone; the copy has no connections; and a loft point takes its first
    /// incoming point's mesh (the copy keeps the point's own). Nothing is written to the package. Move the copy, then complete or cancel it
    /// </summary>
    public Duplication BeginDuplicate() => new(this);

    public sealed class Duplication
    {
        private readonly SplineActorProxy source;
        private readonly ActorProxy selection;
        //the topology of every point the duplicate changes, the copy's included, before it
        private readonly (SplineActorProxy Point, TopologyState State)[] before;
        private bool finished;

        /// <summary>The draft copy. Move it; it's drawn, selected and editable until the duplicate completes or is cancelled</summary>
        public SplineActorProxy Copy { get; }

        internal Duplication(SplineActorProxy source)
        {
            if (source.PlanDuplicate(out int mesh) is { } refusal) throw new InvalidOperationException($"{source.DisplayText} can't be duplicated. {refusal}");
            this.source = source;
            var editor = source.Editor;
            selection = editor.RenderContext.SelectedActor;
            var targets = source.connections.Select(c => source.FindPoint(c.Target)).ToArray();
            Copy = CreateDraft(editor, source, source.Registry.ProposeName(source.Pcc, source.ClassName));
            before = [.. targets.Prepend(source).Append(Copy).Distinct().Select(p => (p, p.CaptureTopology()))];
            try
            {
                using var edit = new SceneEdit(editor);
                edit.Attach(Copy);
                edit.Do(() =>
                {
                    foreach (var target in targets) source.RemoveConnection(target);
                    source.deformMesh = mesh;
                    source.AddConnection(Copy);
                    RefreshPoints(before.Select(s => s.Point));
                }, () => Restore(before));
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
        /// Finishes the duplicate as one undo step, for the caller to record. A copy left where the point is (exactly) isn't a duplicate:
        /// it's cancelled, and there's no step
        /// </summary>
        public IUndoAction Complete()
        {
            ObjectDisposedException.ThrowIf(finished, this);
            if (Copy.SnapshotTransform().Equals(source.SnapshotTransform()))
            {
                Cancel();
                return null;
            }
            finished = true;
            return new DuplicateAction(source.Editor, Copy, selection, before, [.. before.Select(s => (s.Point, s.Point.CaptureTopology()))]);
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

        public DuplicateAction(IActorEditorContext editor, SplineActorProxy copy, ActorProxy selection,
            (SplineActorProxy Point, TopologyState State)[] before, (SplineActorProxy Point, TopologyState State)[] after)
        {
            this.editor = editor;
            this.copy = copy;
            this.selection = selection;
            this.before = [.. before.Select(s => (new ActorRef(s.Point), s.State))];
            this.after = [.. after.Select(s => (new ActorRef(s.Point), s.State))];
        }

        public string Description => "Duplicate spline point";
        public bool IsOrphaned => before.Any(s => s.Point.IsOrphaned);
        public IReadOnlyCollection<ActorId> Subjects => before.Select(s => s.Point.Id).ToArray();

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
            if (!editor.RenderContext.DrawList_3D.Contains(copy)) copy.Dispose();
        }
    }

    // A new point's export has what every LE3 spline point in a shipping level has: these flags, its class (an import), no archetype, the Level as
    // its outer, a Class_N name, a state frame for its class, no binary after its properties, no Components, and one entry in Level.Actors
    private const EObjectFlags NewPointFlags = EObjectFlags.Transactional | EObjectFlags.LoadForClient | EObjectFlags.LoadForServer
        | EObjectFlags.LoadForEdit | EObjectFlags.HasStack;

    //a commit created this draft's export, which it gives back if the commit is abandoned
    private bool materialized;

    /// <summary>
    /// For a commit of a draft, inside its package edit: creates the export, in <paramref name="level"/> and its actors, so that every reference
    /// to the point (connections, backlinks) can be written. Its values are written by <see cref="CommitChanges"/>
    /// </summary>
    internal void Materialize(ExportEntry level)
    {
        if (!IsDraft) throw new InvalidOperationException($"{DisplayText} already has an export.");
        var name = NameReference.FromInstancedString(Registry.NameOf(Id));
        //the package may have gained the proposed name since
        if (Pcc.Exports.Any(e => e.Parent == level && e.ObjectName.Instanced == name.Instanced)) name = Pcc.GetNextIndexedName(ClassName);
        // UE3 registers an object with a NetIndex as one of its package's net objects, and reports one that's another's or out of range
        // (UObject::SetNetIndex, UPackage::AddNetObject). INDEX_NONE registers nothing, so it can't collide
        byte[] stack = [.. StateFrame.Create(ActorClass.UIndex).ToBytes(Pcc), .. BitConverter.GetBytes(-1)];
        var export = new ExportEntry(Pcc, level, name, stack) { Class = ActorClass, ObjectFlags = NewPointFlags };
        Pcc.AddExport(export);
        var levelBinary = ObjectBinary.From<Level>(level);
        levelBinary.Actors.Add(export.UIndex);
        level.WriteBinary(levelBinary);
        BindExport(export);
        materialized = true;
    }
}
