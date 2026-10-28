using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Writes level files' pending actor edits (transforms, spline points, lights, decals and mesh lighting) inside one <see cref="PackageEditScope"/>,
/// then accepts them. Either every write lands and the actors become clean, or the packages are restored exactly and every actor keeps its pending edits.
/// Writers change only the packages (and what <see cref="ActorProxy.AbandonCommit"/> puts back); views take on what was written in <see cref="ActorProxy.MarkClean"/>
/// </summary>
public static class ActorCommit
{
    /// <param name="files">The files whose dirty actors are written, in the order their packages join the scope</param>
    /// <param name="beforeActors">Package writes made in the same scope before the actors' (a decal migration), rolled back with them on failure</param>
    /// <param name="skipped">Actors neither written nor accepted (decals the migration moved to another package)</param>
    public static void Commit(IReadOnlyList<OpenLevelFile> files, Action beforeActors = null, IReadOnlySet<ActorProxy> skipped = null)
    {
        var written = new List<(OpenLevelFile File, ActorProxy Actor)>();
        using (var scope = PackageEditScope.Begin(files.Select(f => f.Package)))
        {
            try
            {
                beforeActors?.Invoke();
                foreach (var file in files) Write(file, skipped, written);
                scope.Complete();
            }
            catch
            {
                for (int i = written.Count - 1; i >= 0; i--) written[i].Actor.AbandonCommit();
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
            foreach (var decal in file.Actors.OfType<DecalActorProxy>().Where(d => skipped?.Contains(d) != true)) decal.MarkClean();
            file.IsDirty = false;
        }
    }

    private static void Write(OpenLevelFile file, IReadOnlySet<ActorProxy> skipped, List<(OpenLevelFile, ActorProxy)> written)
    {
        Dictionary<int, StaticCollectionActor> collectionActorMap = [];
        foreach (ActorProxy actor in file.Actors)
        {
            if (!actor.IsDirty || skipped?.Contains(actor) == true) continue;
            actor.BeginCommit();
            written.Add((file, actor));
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
                // A loft point can be dirty without moving: its spline values, or a connection rebuilt because the next point moved
                if (actor.IsTransformDirty || actor is ILightProxy or DecalActorProxy or SplineActorProxy) actor.CommitChanges();
            }
            foreach (var mesh in actor.LightingMeshes) mesh.ExistingLighting?.Write();
        }
        foreach (var collectionActor in collectionActorMap.Values)
        {
            collectionActor.Export.WriteBinary(collectionActor);
        }
    }
}
