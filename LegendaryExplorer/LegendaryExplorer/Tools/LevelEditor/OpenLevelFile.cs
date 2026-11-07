using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Represents a single open level file in the multi-file LevelEditor.
/// Each instance is an <see cref="IPackageUser"/> registered on its own package,
/// so it receives per-package update notifications independently.
/// </summary>
public class OpenLevelFile : NotifyPropertyChangedBase, IPackageUser, IDisposable
{
    public IMEPackage Package { get; }
    public ExportEntry LevelExport { get; }
    public string FileName => Path.GetFileName(Package.FilePath);
    public string FilePath => Package.FilePath;

    public ObservableCollectionExtended<ActorProxy> Actors { get; } = [];
    private readonly List<PendingActorDeletion> pendingDeletions = [];
    /// <summary>The new actors a commit creates: the drafts in the scene (not those that undo took out of it, which history keeps)</summary>
    public IEnumerable<ActorProxy> PendingCreations => Actors.Where(a => a.IsDraft);
    public IReadOnlyList<PendingActorDeletion> PendingDeletions => pendingDeletions.AsReadOnly();
    public bool HasPendingLifecycleChanges => pendingDeletions.Count != 0 || PendingCreations.Any()
        || Actors.OfType<CollectionActorComponentProxy>().Any(member => member.UpgradePending);

    internal void AddDeletion(PendingActorDeletion deletion)
    {
        if (pendingDeletions.Contains(deletion)) throw new InvalidOperationException("Deletion is already pending.");
        deletion.Retain();
        pendingDeletions.Add(deletion);
        deletion.Actor.PendingDeletion = deletion;
        RecalculateDirty();
        Owner?.ShowPendingDeletion(deletion.Actor, true);
    }

    /// <summary>Attaches what stayed attached to <paramref name="views"/> in the package: a view that left the scene, or was rebuilt, let go of them</summary>
    public void ReattachTo(IReadOnlyCollection<ActorProxy> views)
    {
        foreach (var attached in Actors.Where(a => a.Base is null)) attached.ResolveAttachment(views);
    }

    internal void RemoveDeletion(PendingActorDeletion deletion)
    {
        if (!pendingDeletions.Remove(deletion)) return;
        deletion.Actor.PendingDeletion = null;
        //unlisted before it's let go, which disposes the view if nothing else holds it
        Owner?.ShowPendingDeletion(deletion.Actor, false);
        deletion.Release();
        RecalculateDirty();
    }

    /// <summary>
    /// Drops the pending deletions: after a commit, which made them (its drafts have exports then, so they're no longer pending), or before the file's
    /// actors are reloaded or closed (its drafts go with its actors)
    /// </summary>
    internal void ClearPendingDeletions()
    {
        foreach (var deletion in pendingDeletions.ToArray()) RemoveDeletion(deletion);
    }

    /// <summary>The Level's model and ModelComponents when they were last found to have no drawable BSP surfaces, or null</summary>
    internal (int Model, int[] Components)? UndrawnModel { get; set; }

    // Includes views absent from Actors that history can restore. Closing/reloading invalidates them too.
    internal HashSet<ActorProxy> RetainedActors { get; } = [];

    internal void DisposeRetainedActors()
    {
        foreach (var actor in RetainedActors) actor.Dispose();
        RetainedActors.Clear();
    }

    /// <summary>
    /// The level's lights, which are in the render context's lights while the file is open
    /// </summary>
    public IReadOnlyList<Scene3D.SceneLight> Lights => Lighting?.Lights ?? Array.Empty<Scene3D.SceneLight>();

    public Scene3D.LevelLightSet Lighting { get; private set; }

    /// <summary>
    /// Rereads the level's lights. Uncommitted edits of the file's light proxies are kept, and a light out of the scene (pending deletion) is left out
    /// </summary>
    internal void ReloadLights(LegendaryExplorerCore.Unreal.BinaryConverters.Level level, Scene3D.MeshRenderContext context)
    {
        Dictionary<ExportEntry, Scene3D.LightPreviewOverride> edits = [];
        // A view a refresh replaced is disposed, though history still holds it: its replacement draws its light
        foreach (ILightProxy light in Actors.Concat(RetainedActors).Distinct().Where(a => !a.IsDisposed).OfType<ILightProxy>())
        {
            if (PreviewOverride(light) is { } edit)
                edits[light.Light.Component] = edit;
        }
        var replacement = Scene3D.LevelLightSet.Load(level, context.PackageCache, edits.Count > 0 ? component => edits.GetValueOrDefault(component) : null);
        foreach (LightActorProxy draft in Actors.OfType<LightActorProxy>().Where(a => a.IsDraft))
        {
            if (DraftLight(draft, context.PackageCache) is { } light) replacement.ReplaceLight(draft.Light.Component, light);
        }
        context.ReplaceLights(Lights, replacement.Lights);
        Lighting = replacement;
    }

    /// <summary>
    /// Rebuilds one light from its proxy's current (possibly uncommitted) settings and transform, replacing it in the scene
    /// </summary>
    internal void UpdateLight(ILightProxy light, Scene3D.MeshRenderContext context)
    {
        if (Lighting is null) return;
        ExportEntry component = light.Light.Component;
        Scene3D.SceneLight rebuilt = null;
        try
        {
            rebuilt = light is LightActorProxy { Light.IsDraft: true } draft ? DraftLight(draft, context.PackageCache) : PackageLight(light, context.PackageCache);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not rebuild light {component.InstancedFullPath}: {e.Message}");
        }
        Scene3D.SceneLight previous = Lighting.ReplaceLight(component, rebuilt);
        if (previous is not null || rebuilt is not null)
            context.ReplaceLights(previous is null ? [] : [previous], rebuilt is null ? [] : [rebuilt]);
    }

    /// <summary>A light of the package, with its proxy's pending edits</summary>
    private Scene3D.SceneLight PackageLight(ILightProxy light, PackageCache cache)
    {
        ExportEntry component = light.Light.Component;
        Scene3D.LightPreviewOverride edit = PreviewOverride(light);
        //the same owner transform the level's loader uses when the light isn't moved: a collection's is read from the level's collection by index
        Matrix4x4? ownerTransform = edit?.OwnerTransform ?? light switch
        {
            CollectionActorComponentProxy collectionLight => collectionLight.SavedLocalToWorld,
            ActorProxy { Export: var owner } when owner != component => LegendaryExplorerCore.Unreal.ActorUtils.GetLocalToWorld(owner),
            _ => (Matrix4x4?)null
        };
        return Scene3D.SceneLight.Create(component, OwnerClass(light), cache, ownerTransform, edit?.Properties);
    }

    /// <summary>
    /// A new light's, which isn't in the package: made from its values while it's a draft in the scene. Once its commit has made its component,
    /// the package's light is the one drawn (its view is rebuilt from it), so this is null
    /// </summary>
    private Scene3D.SceneLight DraftLight(LightActorProxy draft, PackageCache cache) => draft.IsDraft && Actors.Contains(draft)
        ? Scene3D.SceneLight.CreateDraft(draft.Light.Component, draft.Light.Type, draft.Light.CopyDraftValues(), draft.ClassName, cache, draft.LocalToWorld,
            draft.Light.GetChangedProperties())
        : null;

    /// <summary>
    /// How the preview draws the light instead of as the package has it: with its pending edits, or, while it's out of the scene (pending deletion),
    /// disabled, which SceneLight leaves out
    /// </summary>
    private Scene3D.LightPreviewOverride PreviewOverride(ILightProxy light) => light is ActorProxy view && !Actors.Contains(view)
        ? new(null, [new LegendaryExplorerCore.Unreal.BoolProperty(false, "bEnabled")])
        : light.Light.GetPreviewOverride(light.EditedOwnerTransform);

    private static string OwnerClass(ILightProxy light) => light switch
    {
        CollectionActorComponentProxy collection => collection.CollectionActorExport.ClassName,
        ActorProxy actor => actor.ClassName,
        _ => ""
    };

    //null outside the window (in tests)
    private readonly LevelEditor Owner;

    private bool isDirty;
    public bool IsDirty
    {
        get => isDirty;
        set
        {
            if (SetProperty(ref isDirty, value))
            {
                Owner?.UpdateGlobalDirtyState();
            }
        }
    }

    /// <summary>
    /// A new actor (a draft) is one of the file's actors, and wholly uncommitted, so it makes the file dirty. Called whenever pending changes change
    /// </summary>
    public void RecalculateDirty()
    {
        IsDirty = pendingDeletions.Count != 0 || Actors.Any(a => a.IsDraft || a.IsDirty);
        OnPropertyChanged(nameof(CanChangeReadOnly));
    }

    /// <summary>Whether <see cref="IsReadOnly"/> can be changed now: not made read-only while the file has new or deleted actors</summary>
    public bool CanChangeReadOnly => IsReadOnly || !HasPendingLifecycleChanges;

    private bool isReadOnly;
    public bool IsReadOnly
    {
        get => Owner?.IsBakePreview == true || isReadOnly;
        set
        {
            // Not while it has new or deleted actors: a commit leaves a read-only file's out, but clears the history, which holds a pending deletion's
            // only way back
            if (value && HasPendingLifecycleChanges)
            {
                //tells a binding that set it that it's unchanged
                OnPropertyChanged();
                return;
            }
            if (Owner?.IsBakePreview != true && SetProperty(ref isReadOnly, value))
            {
                OnPropertyChanged(nameof(CanChangeReadOnly));
                Owner?.UpdatePlacementOptions();
            }
        }
    }

    public OpenLevelFile(LevelEditor owner, IMEPackage package, ExportEntry levelExport)
    {
        Owner = owner;
        Package = package;
        LevelExport = levelExport;
    }

    #region IPackageUser

    public void HandleUpdate(List<PackageUpdate> updates)
    {
        Owner.HandleFileUpdate(this, updates);
    }

    private Action _closedHandler;

    public void RegisterClosed(Action handler)
    {
        _closedHandler = handler;
    }

    public void ReleaseUse()
    {
        _closedHandler = null;
    }

    public void HandleSaveStateChange(bool isSaving)
    {
        Owner.HandleSaveStateChange(this, isSaving);
    }

    #endregion

    public void Dispose()
    {
        ClearPendingDeletions();
        DisposeRetainedActors();
        foreach (var actor in Actors)
        {
            actor.Dispose();
        }
        Actors.Clear();
        // The registered close callback releases this tool's package reference exactly once.
        _closedHandler?.Invoke();
        _closedHandler = null;
    }
}
