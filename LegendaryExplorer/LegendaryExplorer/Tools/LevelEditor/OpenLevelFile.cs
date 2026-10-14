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

    /// <summary>
    /// The level's lights, which are in the render context's lights while the file is open
    /// </summary>
    public IReadOnlyList<Scene3D.SceneLight> Lights => Lighting?.Lights ?? Array.Empty<Scene3D.SceneLight>();

    public Scene3D.LevelLightSet Lighting { get; private set; }

    /// <summary>
    /// Rereads the level's lights. Uncommitted edits of the file's light proxies are kept
    /// </summary>
    internal void ReloadLights(LegendaryExplorerCore.Unreal.BinaryConverters.Level level, Scene3D.MeshRenderContext context)
    {
        Dictionary<ExportEntry, Scene3D.LightPreviewOverride> edits = [];
        foreach (ILightProxy light in Actors.OfType<ILightProxy>())
        {
            if (light.Light.GetPreviewOverride(light.EditedOwnerTransform) is { } edit)
                edits[light.Light.Component] = edit;
        }
        var replacement = Scene3D.LevelLightSet.Load(level, context.PackageCache, edits.Count > 0 ? component => edits.GetValueOrDefault(component) : null);
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
            Scene3D.LightPreviewOverride edit = light.Light.GetPreviewOverride(light.EditedOwnerTransform);
            //the same owner transform the level's loader uses when the light isn't moved: a collection's is read from the level's collection by index
            Matrix4x4? ownerTransform = edit?.OwnerTransform ?? light switch
            {
                CollectionActorComponentProxy collectionLight => collectionLight.SavedLocalToWorld,
                ActorProxy { Export: var owner } when owner != component => LegendaryExplorerCore.Unreal.ActorUtils.GetLocalToWorld(owner),
                _ => (Matrix4x4?)null
            };
            rebuilt = Scene3D.SceneLight.Create(component, OwnerClass(light), context.PackageCache, ownerTransform, edit?.Properties);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not rebuild light {component.InstancedFullPath}: {e.Message}");
        }
        Scene3D.SceneLight previous = Lighting.ReplaceLight(component, rebuilt);
        if (previous is not null || rebuilt is not null)
            context.ReplaceLights(previous is null ? [] : [previous], rebuilt is null ? [] : [rebuilt]);
    }

    private static string OwnerClass(ILightProxy light) => light switch
    {
        CollectionActorComponentProxy collection => collection.CollectionActorExport.ClassName,
        ActorProxy actor => actor.Export.ClassName,
        _ => ""
    };

    private readonly LevelEditor Owner;

    private bool isDirty;
    public bool IsDirty
    {
        get => isDirty;
        set
        {
            if (SetProperty(ref isDirty, value))
            {
                Owner.UpdateGlobalDirtyState();
            }
        }
    }

    public void RecalculateDirty()
    {
        IsDirty = Actors.Any(a => a.IsDirty);
    }

    private bool isReadOnly;
    public bool IsReadOnly
    {
        get => Owner.IsBakePreview || isReadOnly;
        set { if (!Owner.IsBakePreview) SetProperty(ref isReadOnly, value); }
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
