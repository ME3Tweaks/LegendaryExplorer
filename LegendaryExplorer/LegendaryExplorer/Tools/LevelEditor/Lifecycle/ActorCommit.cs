using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Writes level files' pending actor edits (transforms, spline points, lights, decals, sculpted terrain, mesh lighting and chosen meshes) inside one <see cref="PackageEditScope"/>,
/// then accepts them. Either every write lands and the actors become clean, or the packages are restored exactly and every actor keeps its pending edits.
/// Writers change only the packages (and what <see cref="ActorProxy.AbandonCommit"/> puts back); views take on what was written in <see cref="ActorProxy.MarkClean"/>.
/// What it adds, changes or deletes that a bake reads (<see cref="BakeInputs"/>) is marked as changed since the last bake (<see cref="StaticLightingChanges"/>)
/// </summary>
public static class ActorCommit
{
    /// <param name="files">The files whose dirty actors are written, in the order their packages join the scope</param>
    /// <param name="beforeActors">Package writes made in the same scope before the actors' (a decal migration), rolled back with them on failure</param>
    /// <param name="skipped">Actors neither written nor accepted (decals the migration moved to another package)</param>
    /// <param name="sources">The cache other files' assets are copied from (<see cref="AssetPorting.CopyIn"/>), shared with the migration; by default the commit's own</param>
    /// <param name="alsoSnapshot">Packages other than the copies' sources whose assets are copied from them as they are in memory (<see cref="ActorCloning.SourceSnapshots"/>)</param>
    public static void Commit(IReadOnlyList<OpenLevelFile> files, Action beforeActors = null, IReadOnlySet<ActorProxy> skipped = null, PackageCache sources = null,
        IEnumerable<IMEPackage> alsoSnapshot = null)
    {
        using var ownSources = sources is null ? new PackageCache() : null;
        sources ??= ownSources;
        //(released once the commit is done)
        using var snapshots = new ActorCloning.SourceSnapshots(files.SelectMany(f => f.PendingCreations).Where(a => skipped?.Contains(a) != true), sources, alsoSnapshot);
        var deletions = files.SelectMany(f => f.PendingDeletions).ToArray();
        var deleted = deletions.SelectMany(d => d.Entries).ToHashSet<IEntry>();
        // Each file's own: a UIndex means nothing in another package. Taken now, since trashing can change them
        var deletedIndexes = deleted.GroupBy(e => e.FileRef).ToDictionary(g => g.Key, g => g.Select(e => e.UIndex).ToHashSet());
        var written = new List<(OpenLevelFile File, ActorProxy Actor)>();
        var created = new HashSet<ActorProxy>();
        Action useChosenAssets = null;
        using (var scope = PackageEditScope.Begin(files.Select(f => f.Package)))
        {
            try
            {
                // First, since it checks that nothing has changed the packages since it was prepared. A moved decal's material from another file is
                // copied into its new package there
                beforeActors?.Invoke();
                var committing = files.SelectMany(f => f.PendingCreations.Concat(f.Actors.Where(a => a.IsDirty))).Where(a => skipped?.Contains(a) != true).Distinct().ToList();
                // Chosen materials the game wouldn't draw aren't written: what decides that can change after they're chosen
                foreach (var actor in committing)
                {
                    if (actor.Components.OfType<StaticMeshComponentProxy>().Select(m => m.ChosenMaterialProblem).FirstOrDefault(p => p is not null) is { } problem)
                        throw new InvalidOperationException($"{actor.ActorName}'s chosen materials: {problem}");
                }
                // Other files' assets are copied in before anything refers to them: those of a pasted copy's source's file from its snapshot, as the copy is
                useChosenAssets = AssetPorting.CopyIn(committing, sources, snapshots.OfFile);
                // What a bake reads that this commit adds, changes or deletes, judged before anything is written (an edit can make one stop contributing)
                var unbaked = files.SelectMany(f => f.PendingCreations.Concat(f.Actors.Where(a => a.HasChangesBesidesUpgrade)))
                    .Where(a => skipped?.Contains(a) != true).Distinct().Where(a => BakeInputs.MayRead(a, a.Editor?.PackageCache)).ToList();
                var unbakedLevels = files.Where(f => f.PendingDeletions.Any(d => BakeInputs.MayRead(d.Actor, d.Actor.Editor?.PackageCache)))
                    .Select(f => f.LevelExport).ToList();
                // Allocate every draft across the batch before writing any references, regardless of proxy family.
                foreach (var file in files)
                {
                    var drafts = file.PendingCreations.Where(a => skipped?.Contains(a) != true).ToArray();
                    if (drafts.Length == 0) continue;
                    var level = file.LevelExport.GetBinaryData<Level>();
                    foreach (var draft in drafts)
                    {
                        draft.BeginCommit();
                        written.Add((file, draft));
                        created.Add(draft);
                        draft.Materialize(file.LevelExport, level, snapshots);
                    }
                    file.LevelExport.WriteBinary(level);
                }
                foreach (var file in files) Write(file, skipped, written, created);
                foreach (var file in files) CollectionMemberUpgrade.Apply(file);
                // Cooked collision for the meshes and scales the written actors now have, as a save in UE3's editor cooks it
                foreach (var file in files.Where(f => f.LevelExport is not null))
                {
                    var owners = written.Where(w => w.File == file).Select(w => w.Actor.Export).Where(e => e is not null).ToHashSet();
                    if (owners.Count == 0) continue;
                    var components = file.Package.Exports.Where(e => (owners.Contains(e) || e.Parent is ExportEntry parent && owners.Contains(parent)) && e.IsA("StaticMeshComponent"));
                    StaticMeshCollisionCache.AddMissing(file.LevelExport, components, written.First(w => w.File == file).Actor.Editor?.PackageCache);
                }
                // Include references written by other participants in this commit before approving any deletion.
                var deletionPlans = PendingActorDeletion.Revalidate(deletions);
                ActorDeletionPlan.Apply(deletionPlans);
                // Until a bake: a deleted actor's light or shadows are still baked into its level, which is marked for it
                foreach (var actor in unbaked) if (actor.Export is { } export) StaticLightingChanges.Mark(export);
                foreach (var level in unbakedLevels) StaticLightingChanges.Mark(level);
                scope.Complete();
            }
            catch
            {
                for (int i = written.Count - 1; i >= 0; i--) written[i].Actor.AbandonCommit();
                useChosenAssets?.Invoke();
                throw;
            }
        }
        foreach (var (_, actor) in written)
        {
            foreach (var mesh in actor.LightingMeshes) mesh.ExistingLighting?.MarkClean();
            if (actor is not DecalActorProxy) actor.MarkClean();
        }
        foreach (var file in files)
        {
            //a receiving file's decals take on the receivers written for them, whether or not they were edited themselves
            foreach (var decal in file.Actors.OfType<DecalActorProxy>().Where(d => skipped?.Contains(d) != true))
            {
                decal.MarkClean();
                decal.ForgetDeleted(deleted);
            }
            if (deletedIndexes.TryGetValue(file.Package, out var indexes))
            {
                foreach (var actor in file.Actors.Where(a => skipped?.Contains(a) != true)) actor.ForgetDeletedAttachments(indexes);
            }
            file.ClearPendingDeletions();
            file.IsDirty = false;
        }
        //after the files have let go of them, so nothing is told about a disposed view
        foreach (var deletion in deletions) deletion.Accept();
    }

    private static void Write(OpenLevelFile file, IReadOnlySet<ActorProxy> skipped, List<(OpenLevelFile, ActorProxy)> written, HashSet<ActorProxy> created)
    {
        Dictionary<int, StaticCollectionActor> collectionActorMap = [];
        // Decals last: one can land on a mesh actor made in this commit, whose component is made by that actor's commit
        foreach (ActorProxy actor in file.Actors.OrderBy(a => a is DecalActorProxy))
        {
            if (!created.Contains(actor))
            {
                if (!actor.IsDirty || skipped?.Contains(actor) == true) continue;
                actor.BeginCommit();
                written.Add((file, actor));
            }
            // A copy's values are its source's, which its export has (ActorCloning.Materialize made it): only its transform is the view's
            if (actor.CloneSource is not null)
            {
                actor.CommitClone();
                continue;
            }
            if (actor is CollectionActorComponentProxy cacp)
            {
                bool moved = cacp.IsTransformDirty;
                // A light whose settings alone changed must not rewrite (and so rebuild) its whole collection.
                if (moved)
                {
                    if (!collectionActorMap.TryGetValue(cacp.CollectionActorExport.UIndex, out var collectionActor))
                    {
                        collectionActor = (StaticCollectionActor)ObjectBinary.From(cacp.CollectionActorExport);
                        collectionActorMap.Add(cacp.CollectionActorExport.UIndex, collectionActor);
                    }
                    cacp.CommitChanges(collectionActor);
                }
                if (cacp is ILightProxy collectionLight)
                {
                    collectionLight.Light.Commit(moved ? cacp.LocalToWorld : null);
                }
            }
            else
            {
                // A loft point can be dirty without moving: its spline values, or a connection rebuilt because the next point moved. A terrain, by being sculpted
                if (created.Contains(actor) || actor.IsTransformDirty || actor is ILightProxy or DecalActorProxy or SplineActorProxy or TerrainProxy { IsHeightFieldDirty: true }) actor.CommitChanges();
            }
            foreach (var mesh in actor.LightingMeshes) mesh.ExistingLighting?.Write();
            // A chosen mesh is written to its saved component (a draft's new component is made with it, and its materials: MeshActorProxy.CommitChanges)
            // So are chosen materials, after it (the mesh's commit rewrites its properties)
            foreach (var mesh in actor.Components.OfType<StaticMeshComponentProxy>().Where(m => !m.IsStandIn))
            {
                if (mesh.ChosenMesh is not null) StaticMeshChange.Write(mesh.Export, mesh.ChosenMesh);
                if (mesh.ChosenMaterials is not null) StaticMeshChange.WriteMaterials(mesh.Export, mesh.ChosenMaterials);
            }
        }
        foreach (var collectionActor in collectionActorMap.Values)
        {
            collectionActor.Export.WriteBinary(collectionActor);
        }
    }
}
