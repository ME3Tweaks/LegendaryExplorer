using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Finds problems in the open levels, as they're shown: uncommitted edits included, since they're what a commit writes. It reports; it changes no file
/// (copying a level brings the offsets in its exports up to date in memory, as saving it does).
/// Each check reads the game's own data (bake records, cooked receivers, cooked collision), so it says what the game does, not what the preview draws.
/// The checks of the views (lighting, decals) run on the UI thread; those of the packages alone run on copies (<see cref="LevelCopy"/>), which a worker
/// thread can read while the levels are edited
/// </summary>
public static class LevelScan
{
    public static string Title(LevelCheck check) => check switch
    {
        LevelCheck.ChangedSinceBake => "Changed since the last bake",
        LevelCheck.UnbakedMeshes => "Meshes without usable baked lighting",
        LevelCheck.SharedLightGuids => "Lights sharing a GUID",
        LevelCheck.DecalReceivers => "Decals on missing or changed surfaces",
        LevelCheck.DecalWithoutReceivers => "Decals without cooked surfaces",
        LevelCheck.DecalMaterial => "Decals drawn with the default material",
        LevelCheck.ActorList => "Level actor list",
        LevelCheck.TrashReferences => "References to trashed objects",
        LevelCheck.MissingAssets => "Missing assets",
        LevelCheck.VolumeCollision => "Volume collision",
        _ => check.ToString()
    };

    //a check that can't finish says so, rather than reporting nothing. A warning: it's the scan that failed, not the game. Its subject is the file, or for
    //a check of the open levels together, null
    private static void Guard(LevelCheck check, IMEPackage package, string subject, List<LevelProblem> problems, Action run)
    {
        try { run(); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            problems.Add(CouldntFinish(check, package, subject, e));
        }
    }

    private static LevelProblem CouldntFinish(LevelCheck check, IMEPackage package, string subject, Exception e) =>
        new(check, ProblemSeverity.Warning, package, subject ?? "The open levels", $"The check couldn't finish: {e.Message}");

    private static LevelProblem About(LevelCheck check, ProblemSeverity severity, ActorProxy actor, string message) =>
        new(check, severity, actor.Package, actor.DisplayText, message) { Actor = actor, Export = actor.Export };

    private static string List(IReadOnlyCollection<string> names, int shown = 3) =>
        string.Join(", ", names.Take(shown)) + (names.Count > shown ? $" and {names.Count - shown} more" : "");

    #region Views (UI thread)

    /// <summary>
    /// On the UI thread: the checks of the levels as they're shown, which read the editor's views
    /// </summary>
    /// <param name="records">The bakes' records of the open levels as the files have them now (<see cref="CheckCopies"/>)</param>
    /// <param name="pause">Awaited every so often while the meshes are checked, to let the UI thread run (the views can change meanwhile); null for none</param>
    public static async Task<List<LevelProblem>> CheckViewsAsync(IReadOnlyList<OpenLevelFile> files, LevelEditorRenderContext context, LightAssignmentHistory records,
        Func<Task> pause, CancellationToken token)
    {
        var problems = new List<LevelProblem>();
        if (files.Count == 0) return problems;
        var lights = files.SelectMany(f => f.Lights).ToList();
        var lightActors = new Dictionary<ExportEntry, ActorProxy>();
        foreach (var actor in files.SelectMany(f => f.Actors))
            if (actor is ILightProxy { Light.Component: { } component }) lightActors.TryAdd(component, actor);
        // (Listed under the first open file, as a row has to be)
        try { await CheckBakedLighting(files, lights, lightActors, records, context, problems, pause, token); }
        catch (Exception e) when (e is not (OutOfMemoryException or OperationCanceledException))
        {
            problems.Add(CouldntFinish(LevelCheck.ChangedSinceBake, files[0].Package, null, e));
        }
        Guard(LevelCheck.SharedLightGuids, files[0].Package, null, problems, () => CheckLightGuids(lights, lightActors, problems));
        foreach (var file in files)
        {
            Guard(LevelCheck.DecalMaterial, file.Package, file.FileName, problems, () => CheckDecalMaterials(file, context, problems));
            Guard(LevelCheck.DecalReceivers, file.Package, file.FileName, problems, () => CheckDecalReceivers(file, context, problems));
        }
        return problems;
    }

    /// <summary>
    /// What's changed since its level was last baked: what a bake reads (<see cref="BakeInputs"/>) that a commit marked or that has uncommitted changes,
    /// and a level a commit deleted one from (<see cref="StaticLightingChanges"/>). Lights changed outside the Level Editor aren't marked, so a light no bake
    /// in the open levels has any record of is listed too, as a new one is.
    /// <para>
    /// The game draws a static-shadowing light whose interaction with a statically shadowed mesh isn't baked as a dynamic light on it (UE3's uncached static
    /// lighting). Shipping bakes leave many such interactions unrecorded, since UE3's sphere test admits lights that light none of a mesh, and occluded
    /// ones; so the check asks only whether any bake knows the light. A mesh with no usable bake mapping gets a row of its own, since nothing is baked on it
    /// </para>
    /// </summary>
    private static async Task CheckBakedLighting(IReadOnlyList<OpenLevelFile> files, List<SceneLight> lights, Dictionary<ExportEntry, ActorProxy> lightActors,
        LightAssignmentHistory records, LevelEditorRenderContext context, List<LevelProblem> problems, Func<Task> pause, CancellationToken token)
    {
        var unbaked = new Dictionary<SceneLight, List<ActorProxy>>();
        var staticLights = lights.Where(l => l.HasStaticShadowing && !l.BakedGIOnly).ToList();
        // Actors with a row of their own under UnbakedMeshes, one for an actor whichever of its meshes it's for: those whose meshes have no usable
        // mapping, and those whose check failed (which can still be changed since the bake)
        var unmapped = new HashSet<ActorProxy>();
        var failed = new HashSet<ActorProxy>();
        int checkedMeshes = 0;
        foreach (var file in files)
        {
            // (A copy of the list: the views can change while the UI thread runs)
            foreach (var actor in file.Actors.ToList())
            {
                foreach (var mesh in actor.Components.OfType<StaticMeshComponentProxy>().Where(BakeInputs.IsBakedMesh).ToList())
                {
                    if (pause is not null && ++checkedMeshes % 50 == 0)
                    {
                        await pause();
                        token.ThrowIfCancellationRequested();
                    }
                    if (actor.IsDisposed || unmapped.Contains(actor) || failed.Contains(actor)) break;
                    try
                    {
                        if (UnbakedLights(actor, mesh, staticLights, context) is not var (lightsOnIt, mapped, lightMap, misfit, isNew)) continue;
                        if (mapped)
                        {
                            foreach (var light in lightsOnIt.Where(l => !records.IsKnown(l)))
                            {
                                if (!unbaked.TryGetValue(light, out var meshes)) unbaked[light] = meshes = [];
                                if (!meshes.Contains(actor)) meshes.Add(actor);
                            }
                            continue;
                        }
                        if (lightMap is { LightMapType: not ELightMapType.LMT_None })
                            problems.Add(About(LevelCheck.UnbakedMeshes, ProblemSeverity.Problem, actor,
                                $"{misfit}, so the game lights it wrongly, and a bake can't replace it."));
                        else if (lightsOnIt.Count > 0)
                            problems.Add(About(LevelCheck.UnbakedMeshes, ProblemSeverity.Warning, actor, (isNew ? "It's new, so it has no baked lighting"
                                : "It has no lightmap") + $": until the level is rebaked, {List(lightsOnIt.Select(l => LightName(l, lightActors)).ToList())} light it " +
                                "dynamically in game, and it gets no bounced light."));
                        else continue;
                        unmapped.Add(actor);
                    }
                    catch (Exception e) when (e is not OutOfMemoryException)
                    {
                        problems.Add(About(LevelCheck.UnbakedMeshes, ProblemSeverity.Warning, actor, $"Its lighting couldn't be checked: {e.Message}"));
                        failed.Add(actor);
                    }
                }
            }
        }
        // Its own row lists the lights on it
        foreach (var meshes in unbaked.Values) meshes.RemoveAll(a => unmapped.Contains(a) || failed.Contains(a));

        var changed = new HashSet<ActorProxy>();
        foreach (var file in files)
        {
            if (StaticLightingChanges.IsMarked(file.LevelExport))
                problems.Add(new(LevelCheck.ChangedSinceBake, ProblemSeverity.Warning, file.Package, file.FileName,
                    "A light or an actor the bake lights was deleted from it since it was last baked: its light or shadows stay in the baked lighting until it's rebaked."));
            // A mark is on the actor's export, or a collection member's component, which an upgrade moves under an actor of its own
            var actors = ActorsByExport(file);
            var deleting = PendingDeletionExports(file);
            var marked = new HashSet<ActorProxy>();
            foreach (var export in file.Package.Exports.Where(e => e != file.LevelExport && StaticLightingChanges.IsMarked(e)))
            {
                if (OwnerOf(export, actors) is { } owner) marked.Add(owner);
                // Its commit trashes it, which clears the mark, and marks the level if a bake reads it
                else if (IsUnder(export, deleting)) continue;
                // Not under an actor the editor shows, as an actor it couldn't read
                else problems.Add(new(LevelCheck.ChangedSinceBake, ProblemSeverity.Warning, file.Package, export.InstancedFullPath,
                    "It's been added or changed since its level was last baked, so its baked lighting is as it was before until the level is rebaked.") { Export = export });
            }
            foreach (var deletion in file.PendingDeletions)
            {
                string message = "It's pending deletion: once that's committed, its light or shadows stay in the level's baked lighting until it's rebaked.";
                try
                {
                    if (BakeInputs.Reads(deletion.Actor, context.PackageCache))
                        problems.Add(About(LevelCheck.ChangedSinceBake, ProblemSeverity.Warning, deletion.Actor, message));
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    problems.Add(About(LevelCheck.ChangedSinceBake, ProblemSeverity.Warning, deletion.Actor,
                        $"It's pending deletion, and whether the bake reads it couldn't be checked: {e.Message}"));
                }
            }
            foreach (var actor in file.Actors.Where(a => !a.IsDisposed && !unmapped.Contains(a)))
            {
                bool isMarked = marked.Contains(actor);
                if (!isMarked)
                {
                    if (!actor.IsDraft && !actor.HasChangesBesidesUpgrade) continue;
                    try
                    {
                        if (!BakeInputs.Reads(actor, context.PackageCache)) continue;
                    }
                    catch (Exception e) when (e is not OutOfMemoryException)
                    {
                        problems.Add(About(LevelCheck.ChangedSinceBake, ProblemSeverity.Warning, actor,
                            $"It has uncommitted changes, and whether the bake reads it couldn't be checked: {e.Message}"));
                        continue;
                    }
                }
                changed.Add(actor);
                string what = actor is ILightProxy ? "the baked lighting has it as it was before" : "its baked lighting, and the shadows it casts in the bake, are as they were before";
                problems.Add(About(LevelCheck.ChangedSinceBake, ProblemSeverity.Warning, actor, isMarked
                    ? $"It's been added or changed since its level was last baked, so {what} until the level is rebaked."
                    : $"It has uncommitted changes the level's bake hasn't seen: once they're committed, {what} until the level is rebaked."));
            }
        }
        foreach (var (light, meshes) in unbaked.Where(u => u.Value.Count > 0 && (!lightActors.TryGetValue(u.Key.Export, out var actor) || !changed.Contains(actor))))
        {
            string message = $"No bake in the open levels has a record of it, as for a new light, so the game draws it as a dynamic light on the " +
                $"{(meshes.Count == 1 ? "mesh" : $"{meshes.Count} meshes")} it reaches ({List(meshes.Select(m => m.DisplayText).ToList())}) until the level is rebaked.";
            problems.Add(lightActors.TryGetValue(light.Export, out var actor) ? About(LevelCheck.ChangedSinceBake, ProblemSeverity.Warning, actor, message)
                : new(LevelCheck.ChangedSinceBake, ProblemSeverity.Warning, light.Export.FileRef, light.Export.ObjectName.Instanced, message));
        }
    }

    /// <summary>
    /// The static-shadowing lights whose radius reaches the mesh, as it's shown, without its bake having a record of them, whether its baked lighting
    /// fits it (<see cref="MeshStaticLighting.HasFittingMapping"/>), its LOD 0 lightmap, and if that doesn't fit, why, and whether it's a new mesh's
    /// stand-in (which has no lightmap). Null if the game doesn't bake it: it has no static shadowing, doesn't accept lights, or a light environment lights it
    /// </summary>
    private static (List<SceneLight> Lights, bool Mapped, LightMap LightMap, string Misfit, bool IsNew)? UnbakedLights(ActorProxy actor, StaticMeshComponentProxy mesh,
        List<SceneLight> lights, LevelEditorRenderContext context)
    {
        var cache = context.PackageCache;
        if (mesh.StaticMeshExport is not { } meshExport) return null;
        var props = mesh.Properties;
        // A copy's stand-in is as its commit makes it, where it's shown: with the baked lighting and light environment of the component it's drawn from
        var baked = BakeInputs.BakedComponent(mesh);
        bool isNew = baked is null;
        var component = baked ?? mesh.Export;
        var binary = baked?.GetBinaryData<StaticMeshComponent>() ?? StaticMeshComponent.Create();
        if (!LightAssignment.TakesPartInStaticLighting(component, props, binary, isNew ? actor.ClassName : null)) return null;
        if (EffectiveLightingProperties.UsesLightEnvironment(component, props, isNew ? actor.Export : component.Parent as ExportEntry, cache, out _)) return null;
        var stm = context.GetStaticMesh(meshExport);
        // As the game bounds it: its mesh's bounds (UStaticMeshComponent::UpdateBounds), or for a spline mesh, its LOD 0 deformed along its spline, as the
        // bake bounds one
        var bounds = mesh is SplineMeshComponentProxy && stm.LODModels is [var lod0, ..] && lod0.PositionVertexBuffer.VertexData.Length > 0
            ? LightmassSceneBuilder.MeshBounds(lod0.PositionVertexBuffer.VertexData, mesh.LocalToWorld, SplineMeshDeformation.Read(props, stm.Bounds)).Receiver
            : stm.Bounds.TransformBy(mesh.LocalToWorld);
        Vector3 min = bounds.Origin - bounds.BoxExtent, max = bounds.Origin + bounds.BoxExtent;
        // The game's sphere test admits lights that light none of the mesh: only those whose radius reaches its box draw anything on it. Whether the game
        // draws one as a dynamic light on it is the view's own rule (MeshStaticLighting.GetEligibility, which mirrors GetInteraction), with its pending
        // lighting edits
        var unbaked = lights.Where(l => LightmassSceneBuilder.ReachesBox(l, min, max) && mesh.GetBakedEligibility(l, bounds) == LightEligibility.Affects).ToList();
        var lightMap = binary.LODData.FirstOrDefault()?.LightMap;
        bool mapped = !isNew && MeshStaticLighting.HasFittingMapping(component, binary, stm);
        return (unbaked, mapped, lightMap, mapped || lightMap is not { LightMapType: not ELightMapType.LMT_None } ? null : Misfit(lightMap, stm), isNew);
    }

    // What the game can't draw of a lightmap on its mesh (LightmapMapping.FitsMesh): only what's checked, not where it came from
    private static string Misfit(LightMap lightMap, StaticMesh mesh)
    {
        if (mesh.LODModels is not [var lod, ..]) return "Its mesh has no LODs to draw its lightmap on";
        int samples = LightmapMapping.VertexSampleCount(lightMap);
        return samples >= 0 ? $"Its vertex lightmap has {samples} samples, but its mesh has {lod.PositionVertexBuffer.VertexData.Length} vertices"
            : $"Its lightmap uses UV channel {ComponentGeometry.CoordinateIndex(mesh.Export)}, but its mesh has {lod.VertexBuffer.NumTexCoords}";
    }

    private static string LightName(SceneLight light, Dictionary<ExportEntry, ActorProxy> lightActors) =>
        lightActors.TryGetValue(light.Export, out var actor) ? actor.DisplayText : light.Export.ObjectName.Instanced;

    /// <summary>
    /// Lights the game can't tell apart: a light's baked records are matched by its GUIDs, LightGuid for shadow maps and IrrelevantLights, LightmapGuid for
    /// lightmaps' light lists (<see cref="MeshStaticLighting.GetEligibility"/>). Copying a light in another tool copies them
    /// </summary>
    private static void CheckLightGuids(List<SceneLight> lights, Dictionary<ExportEntry, ActorProxy> lightActors, List<LevelProblem> problems)
    {
        foreach (var (guidOf, field, records) in new (Func<SceneLight, Guid>, string, string)[]
                 {
                     (l => l.LightGuid, "LightGuid", "shadow maps and IrrelevantLights entries"),
                     (l => l.LightmapGuid, "LightmapGuid", "lightmap entries")
                 })
        {
            foreach (var group in lights.Where(l => guidOf(l) != Guid.Empty).GroupBy(guidOf).Where(g => g.Count() > 1))
            {
                var first = group.First();
                string message = $"It has the same {field} as {List(group.Skip(1).Select(l => LightName(l, lightActors)).ToList())}, so the game takes " +
                    $"either's baked {records} as the other's too.";
                problems.Add(lightActors.TryGetValue(first.Export, out var actor) ? About(LevelCheck.SharedLightGuids, ProblemSeverity.Problem, actor, message)
                    : new(LevelCheck.SharedLightGuids, ProblemSeverity.Problem, first.Export.FileRef, first.Export.ObjectName.Instanced, message));
            }
        }
    }

    private static void CheckDecalMaterials(OpenLevelFile file, LevelEditorRenderContext context, List<LevelProblem> problems)
    {
        foreach (var decal in file.Actors.OfType<DecalActorProxy>())
        {
            if (decal.DecalComponent?.DefaultMaterialReason(context.PackageCache) is { } reason)
                problems.Add(About(LevelCheck.DecalMaterial, ProblemSeverity.Problem, decal, $"The game draws its default decal material in its place. {reason}"));
        }
    }

    private static void CheckDecalReceivers(OpenLevelFile file, LevelEditorRenderContext context, List<LevelProblem> problems)
    {
        foreach (var decal in file.Actors.OfType<DecalActorProxy>())
        {
            if (decal.DecalComponent is not { } component) continue;
            if (component.ReceiversUnreadReason is { } unread)
            {
                problems.Add(About(LevelCheck.DecalReceivers, ProblemSeverity.Warning, decal, $"The surfaces it's projected onto couldn't be checked: {unread}"));
                continue;
            }
            if (component.CheckReceivers(context) is not { } receivers) continue;
            var issues = new List<string>();
            if (receivers.Gone.Count > 0) issues.Add($"{List(receivers.Gone)} no longer exist{(receivers.Gone.Count == 1 ? "s" : "")}");
            if (receivers.Changed.Count > 0)
                issues.Add($"{List(receivers.Changed)} moved or changed since it was projected or the level was opened, so it floats where they were");
            if (receivers.Malformed.Count > 0) issues.Add($"{List(receivers.Malformed)} have cooked triangles the game can't draw");
            if (issues.Count > 0)
                problems.Add(About(LevelCheck.DecalReceivers, ProblemSeverity.Problem, decal,
                    $"Surfaces it's projected onto: {string.Join("; ", issues)}. Moving or resizing it reprojects it onto what's there now."));
            // With no cooked receivers, a decal does not display in game (decomp suggests it projects at runtime, but actual in-game testing belies that).
            // Cooked receivers without triangles draw nothing either
            else if (receivers.Live == 0)
                problems.Add(About(LevelCheck.DecalWithoutReceivers, ProblemSeverity.Warning, decal, (receivers.Empty > 0 ? "Its cooked surfaces have no triangles,"
                    : receivers.Deleting > 0 ? "The surfaces it's projected onto are pending deletion, so once that's committed it has no cooked surfaces,"
                    : "It has no cooked surfaces,") + " and displays nothing in game. Reposition it, or change its filter."));
        }
    }

    //the actors' and their components' exports, to find which actor an object belongs to
    private static Dictionary<ExportEntry, ActorProxy> ActorsByExport(OpenLevelFile file)
    {
        var actors = new Dictionary<ExportEntry, ActorProxy>();
        foreach (var actor in file.Actors.Where(a => !a.IsDraft))
        {
            actors.TryAdd(actor.Export, actor);
            foreach (var component in actor.Components)
                if (component.Export is { } export && export.FileRef == file.Package) actors.TryAdd(export, actor);
        }
        return actors;
    }

    //the exports of the actors pending deletion, which the commit trashes with what's under them
    private static HashSet<ExportEntry> PendingDeletionExports(OpenLevelFile file) =>
        file.PendingDeletions.Select(d => d.Actor.Export).Where(e => e is not null).ToHashSet();

    private static bool IsUnder(IEntry entry, HashSet<ExportEntry> exports)
    {
        for (; entry is not null; entry = entry.Parent)
            if (entry is ExportEntry export && exports.Contains(export)) return true;
        return false;
    }

    //the actor an object is, or is under
    private static ActorProxy OwnerOf(IEntry entry, Dictionary<ExportEntry, ActorProxy> actors)
    {
        for (; entry is not null; entry = entry.Parent)
            if (entry is ExportEntry export && actors.TryGetValue(export, out var actor)) return actor;
        return null;
    }

    #endregion

    #region Packages (worker thread, on copies)

    /// <summary>
    /// A copy of an open level as its package is now, serialized on the UI thread (<see cref="Copy"/>) and read on a worker thread (<see cref="Open"/>),
    /// for checks that read only the package: a worker thread can read it while the level is edited. Its export and import indices are the level's
    /// </summary>
    public sealed class LevelCopy : IDisposable
    {
        public OpenLevelFile File { get; init; }
        /// <summary>The file's path and its Level export's index, as they were when it was copied: worker threads don't read the file</summary>
        internal string FilePath { get; init; }
        internal int LevelUIndex { get; init; }
        //the file's entries when it was copied, by index, for a finding to be matched with its object (EntryAt)
        internal IReadOnlyList<ExportEntry> Exports { get; init; }
        internal IReadOnlyList<ImportEntry> Imports { get; init; }

        /// <summary>The file's entry that was at <paramref name="uIndex"/> when it was copied; null for none</summary>
        internal IEntry EntryAt(int uIndex) => uIndex switch
        {
            > 0 when uIndex <= Exports.Count => Exports[uIndex - 1],
            < 0 when -uIndex <= Imports.Count => Imports[-uIndex - 1],
            _ => null
        };
        /// <summary>Null until <see cref="Open"/></summary>
        public IMEPackage Package { get; private set; }
        /// <summary>Actors pending deletion, which the commit takes out</summary>
        internal HashSet<int> PendingDeletions { get; init; }
        /// <summary>Volumes with a pending scale change: the commit cooks the collision of those with cooked data again (<see cref="BrushProxy.CommitChanges"/>)</summary>
        internal HashSet<int> RescaledVolumes { get; init; }
        private MemoryStream saved;

        internal LevelCopy(MemoryStream saved) => this.saved = saved;

        internal void Open()
        {
            saved.Position = 0;
            Package = MEPackageHandler.OpenMEPackageFromStream(saved, associatedFilePath: FilePath, useSharedPackageCache: false);
            saved.Dispose();
            saved = null;
        }

        public void Dispose()
        {
            saved?.Dispose();
            Package?.Dispose();
        }
    }

    /// <summary>A problem found in a copy, about the object at <see cref="UIndex"/> (0 for none), which was at <see cref="Path"/></summary>
    public sealed record PackageFinding(LevelCheck Check, LevelCopy Copy, int UIndex, string Path, string Message)
    {
        public OpenLevelFile File => Copy.File;

        public ProblemSeverity Severity { get; init; } = ProblemSeverity.Problem;

        /// <summary>
        /// The import a missing asset's finding is about, so that several of an actor's objects that refer to it are one row for the actor. 0 for a finding
        /// about its object alone
        /// </summary>
        public int MissingImport { get; init; }
    }

    /// <summary>What <see cref="CheckCopies"/> found, and the bakes' records of the copies, for <see cref="CheckViews"/></summary>
    public sealed record PackageChecks(List<PackageFinding> Findings, LightAssignmentHistory Records);

    /// <summary>
    /// On the UI thread, while no file is being saved (a save writes the same offsets): copies of the files' packages, for <see cref="Open"/>
    /// </summary>
    public static List<LevelCopy> Copy(IReadOnlyList<OpenLevelFile> files)
    {
        var copies = new List<LevelCopy>();
        try
        {
            foreach (var file in files)
            {
                copies.Add(new LevelCopy(file.Package.SaveToStream(compress: false))
                {
                    File = file, FilePath = file.FilePath, LevelUIndex = file.LevelExport.UIndex,
                    Exports = file.Package.Exports.ToArray(), Imports = file.Package.Imports.ToArray(),
                    PendingDeletions = file.PendingDeletions.Select(d => d.Actor.Export?.UIndex ?? 0).ToHashSet(),
                    RescaledVolumes = file.Actors.OfType<BrushProxy>().Where(v => !v.IsDraft && v.IsScaleDirty).Select(v => v.Export.UIndex).ToHashSet()
                });
            }
            return copies;
        }
        catch
        {
            foreach (var copy in copies) copy.Dispose();
            throw;
        }
    }

    /// <summary>On a worker thread: reads the copies' packages, for <see cref="CheckCopies"/> and <see cref="FindMissingAssets"/></summary>
    public static void Open(IReadOnlyList<LevelCopy> copies, CancellationToken token)
    {
        foreach (var copy in copies)
        {
            token.ThrowIfCancellationRequested();
            copy.Open();
        }
    }

    /// <summary>On a worker thread: the checks of the copies' packages alone, and the bakes' records of them. Archetypes are read from disk, in a cache of its own</summary>
    public static PackageChecks CheckCopies(IReadOnlyList<LevelCopy> copies, CancellationToken token)
    {
        using var cache = new PackageCache();
        var findings = new List<PackageFinding>();
        var records = LightAssignmentHistory.Empty;
        foreach (var copy in copies)
        {
            token.ThrowIfCancellationRequested();
            var level = copy.Package.GetUExport(copy.LevelUIndex);
            var read = LightAssignmentHistory.Read(copy.Package);
            records = records.With(read);
            if (read.Problems.Count > 0)
                findings.Add(new(LevelCheck.ChangedSinceBake, copy, level.UIndex, level.InstancedFullPath,
                    $"Some of its bake records couldn't be read ({List(read.Problems.Distinct().ToList())}), so a light they record may be listed as having no " +
                    "record.") { Severity = ProblemSeverity.Warning });
            Guard(LevelCheck.ActorList, copy, level, findings, () => CheckActorList(copy, level, findings));
            Guard(LevelCheck.TrashReferences, copy, level, findings, () => CheckTrashReferences(copy, findings));
            Guard(LevelCheck.VolumeCollision, copy, level, findings, () => CheckVolumes(copy, level, cache, findings, token));
        }
        return new(findings, records);
    }

    private static void Guard(LevelCheck check, LevelCopy copy, ExportEntry level, List<PackageFinding> findings, Action run)
    {
        try { run(); }
        catch (Exception e) when (e is not (OutOfMemoryException or OperationCanceledException))
        {
            findings.Add(new(check, copy, level.UIndex, level.InstancedFullPath, $"The check couldn't finish: {e.Message}") { Severity = ProblemSeverity.Warning });
        }
    }

    /// <summary>
    /// On the UI thread: a problem for each finding of a file that's still open, about the actor the object it's about is or is under, if it's still
    /// where it was. An object that's pending deletion, or under an actor that is, is left out: the commit trashes it
    /// </summary>
    public static List<LevelProblem> ToProblems(IEnumerable<PackageFinding> findings, IReadOnlyList<OpenLevelFile> openFiles)
    {
        var problems = new List<LevelProblem>();
        var actorsOf = new Dictionary<OpenLevelFile, Dictionary<ExportEntry, ActorProxy>>();
        var missingImports = new HashSet<(ActorProxy, int)>();
        var deletingOf = new Dictionary<OpenLevelFile, HashSet<ExportEntry>>();
        foreach (var finding in findings.Where(f => openFiles.Contains(f.File)))
        {
            var package = finding.File.Package;
            // The object it was found on, if the file still has it there
            var entry = finding.Copy.EntryAt(finding.UIndex) is { } copied && package.GetEntry(finding.UIndex) == copied ? copied : null;
            if (!deletingOf.TryGetValue(finding.File, out var deleting)) deletingOf[finding.File] = deleting = PendingDeletionExports(finding.File);
            if (IsUnder(entry, deleting)) continue;
            if (!actorsOf.TryGetValue(finding.File, out var actors)) actorsOf[finding.File] = actors = ActorsByExport(finding.File);
            var actor = OwnerOf(entry, actors);
            // Several of an actor's objects that refer to the same missing import are one row for the actor
            if (actor is not null && finding.MissingImport != 0 && !missingImports.Add((actor, finding.MissingImport))) continue;
            problems.Add(actor is not null ? About(finding.Check, finding.Severity, actor, finding.Message)
                : new(finding.Check, finding.Severity, package, finding.Path, finding.Message) { Export = entry as ExportEntry });
        }
        return problems;
    }

    /// <summary>The game spawns what's in the Level's Actors list, once each, and nothing else under it</summary>
    private static void CheckActorList(LevelCopy copy, ExportEntry levelExport, List<PackageFinding> findings)
    {
        var package = copy.Package;
        var level = levelExport.GetBinaryData<Level>();
        var listed = new HashSet<int>();
        var twice = new HashSet<int>();
        void Add(int uIndex, string path, string message) => findings.Add(new(LevelCheck.ActorList, copy, uIndex, path, message));
        foreach (int uIndex in level.Actors)
        {
            // Empty slots are left by deleted actors in shipping levels too, and skipped
            if (uIndex == 0) continue;
            if (!listed.Add(uIndex))
            {
                if (twice.Add(uIndex) && package.GetEntry(uIndex) is { } repeated)
                    Add(uIndex, repeated.InstancedFullPath, "It's in its level's actor list more than once.");
                continue;
            }
            switch (package.GetEntry(uIndex))
            {
                case null:
                    Add(levelExport.UIndex, levelExport.InstancedFullPath, $"Its actor list has #{uIndex}, which isn't in the file.");
                    break;
                case ImportEntry import:
                    Add(levelExport.UIndex, levelExport.InstancedFullPath, $"Its actor list has {import.InstancedFullPath}, an import: a level's actors are its own exports.");
                    break;
                // References to trashed objects are their own check
                case ExportEntry export when !export.IsTrash() && !export.IsA("Actor"):
                    Add(levelExport.UIndex, levelExport.InstancedFullPath, $"Its actor list has {export.InstancedFullPath}, a {export.ClassName}, which isn't an actor.");
                    break;
            }
        }
        foreach (var export in package.Exports.Where(e => e.Parent == levelExport && !listed.Contains(e.UIndex) && !e.IsDefaultObject
                     && !e.ObjectFlags.Has(UnrealFlags.EObjectFlags.ArchetypeObject) && !e.IsTrash() && e.IsA("Actor")))
            Add(export.UIndex, export.InstancedFullPath, "It isn't in its level's actor list, so the game never spawns it.");
    }

    private static void CheckTrashReferences(LevelCopy copy, List<PackageFinding> findings)
    {
        var trash = copy.Package.Exports.Cast<IEntry>().Concat(copy.Package.Imports).Where(e => e.IsTrash()).ToList();
        if (trash.Count == 0) return;
        var referrers = new Dictionary<IEntry, List<string>>();
        foreach (var (trashed, users) in trash.GetEntriesThatReferenceThese())
        {
            foreach (var (user, where) in users.Where(u => !u.Key.IsTrash()))
            {
                if (!referrers.TryGetValue(user, out var list)) referrers[user] = list = [];
                list.AddRange(where.Select(w => $"{w} → {trashed.InstancedFullPath}"));
            }
        }
        foreach (var (user, where) in referrers)
            findings.Add(new(LevelCheck.TrashReferences, copy, user.UIndex, user.InstancedFullPath,
                $"{user.InstancedFullPath} refers to trashed objects, which are empty packages in the game: {List(where.Distinct().ToList())}."));
    }

    /// <summary>
    /// Rigid bodies collide with a volume through its cooked PhysX data, cooked at its scale. Without data of the current version, the game rebuilds its
    /// hulls from its brush when the level loads (LE3 RVA 0x6A5820, UBrushComponent::InitComponentRBPhys), which for a cooked volume's empty brush leaves it none
    /// </summary>
    private static void CheckVolumes(LevelCopy copy, ExportEntry levelExport, PackageCache cache, List<PackageFinding> findings, CancellationToken token)
    {
        var package = copy.Package;
        foreach (int uIndex in levelExport.GetBinaryData<Level>().Actors.Distinct())
        {
            token.ThrowIfCancellationRequested();
            if (copy.PendingDeletions.Contains(uIndex) || !package.TryGetUExport(uIndex, out var volume) || !volume.IsA("Brush")) continue;
            if (EffectiveLightingProperties.ResolveReference(volume, "BrushComponent", cache) is not { ClassName: "BrushComponent" } component
                || component.FileRef != package) continue;
            var props = EffectiveLightingProperties.Read(component, cache);
            if (props.GetProp<BoolProperty>("bDisableAllRigidBody")?.Value == true) continue;
            var binary = component.GetBinaryData<BrushComponent>();
            var cachedElements = binary.CachedPhysBrushData.CachedConvexElements;
            // Its commit cooks it again at its new scale
            if (cachedElements.Length > 0 && copy.RescaledVolumes.Contains(uIndex)) continue;
            void Add(string message) => findings.Add(new(LevelCheck.VolumeCollision, copy, volume.UIndex, volume.InstancedFullPath, message));
            if (cachedElements.Length > 0 && props.GetProp<IntProperty>("CachedPhysBrushDataVersion")?.Value == BrushComponent.CurrentCachedPhysBrushDataVersion)
            {
                var cooked = binary.CookCachedPhysBrushData(cache);
                bool current = cooked is not null && cooked.Length == cachedElements.Length
                    && cooked.Zip(cachedElements).All(e => e.First.ConvexElementData.AsSpan().SequenceEqual(e.Second.ConvexElementData));
                if (!current)
                    Add("Its cooked rigid-body collision doesn't match its hulls at its scale, as when a volume is scaled or reshaped in another tool, so physics " +
                        "objects collide with another shape.");
            }
            // The game discards data of another version, as if there were none
            else if (EffectiveLightingProperties.ResolveReference(volume, "Brush", cache) is { ClassName: "Model" } brush && brush.GetBinaryData<Model>().Nodes.Length == 0
                     && binary.CookCachedPhysBrushData(cache) is { Length: > 0 })
            {
                Add((cachedElements.Length > 0 ? "It has collision hulls, but its cooked rigid-body data is of another version, which the game discards," :
                    "It has collision hulls but no cooked rigid-body data,") + " and its brush has no geometry: when the level loads, the game rebuilds its " +
                    "hulls from the brush, which leaves it with no collision.");
            }
        }
    }

    /// <summary>
    /// On a worker thread: imports of objects (not packages, which the game makes as it needs them, nor native classes) that the game can't find in the
    /// files it loads, as <see cref="EntryImporter.ResolveImport(ImportEntry, PackageCache, string, bool, IEnumerable{string}, string,
    /// Func{string, PackageCache, IMEPackage}, Func{ExportEntry, bool}, string)"/> looks, and that no other of the copies exports (levels loaded together
    /// import each other's objects): a finding for each object that refers to one. The game's files are opened from disk, in a cache of its own
    /// </summary>
    /// <param name="progress">Reports how many of the copies' imports have been looked for, and how many there are, every 25</param>
    public static List<PackageFinding> FindMissingAssets(IReadOnlyList<LevelCopy> copies, IProgress<(int Done, int Total)> progress, CancellationToken token)
    {
        // Imports are mostly of a few packages: the files it opened most recently are kept
        using var cache = new PackageCache { CacheMaxSize = 20 };
        var imports = copies.SelectMany(c => c.Package.Imports.Where(i => i.ClassName != "Package"
            && !EntryImporter.IsNativeClassImport(i)).Select(i => (Copy: c, Import: i))).ToList();
        var findings = new List<PackageFinding>();
        var unresolved = new List<(LevelCopy Copy, ImportEntry Import)>();
        for (int i = 0; i < imports.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var (copy, import) = imports[i];
            if (EntryImporter.ResolveImport(import, cache, "INT", unsafeLoad: true) is null
                && !copies.Any(c => c != copy && c.Package.FindExport(import.InstancedFullPath) is not null))
                unresolved.Add((copy, import));
            if ((i + 1) % 25 == 0 || i + 1 == imports.Count) progress?.Report((i + 1, imports.Count));
        }
        foreach (var group in unresolved.GroupBy(u => u.Copy))
        {
            var users = group.Select(u => u.Import).Cast<IEntry>().ToList().GetEntriesThatReferenceThese();
            foreach (var (_, import) in group)
            {
                string what = import.ClassName == "Class" ? "objects of the class can't be made" : "references to it load as None";
                string message = $"{import.InstancedFullPath} ({import.ClassName}) isn't in the game's files or the open levels, so unless a level loaded " +
                    $"with this one has it, {what}.";
                var referrers = users[import].Keys.Where(u => !u.IsTrash()).ToList();
                if (referrers.Count == 0)
                    findings.Add(new(LevelCheck.MissingAssets, group.Key, import.UIndex, import.InstancedFullPath, message + " Nothing in the file refers to it.")
                        { Severity = ProblemSeverity.Warning });
                foreach (var referrer in referrers)
                    findings.Add(new(LevelCheck.MissingAssets, group.Key, referrer.UIndex, referrer.InstancedFullPath, message) { MissingImport = import.UIndex });
            }
        }
        return findings;
    }

    #endregion
}
