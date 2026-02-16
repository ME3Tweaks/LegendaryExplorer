using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        Owner.HandleSaveStateChange(isSaving);
    }

    #endregion

    public void Dispose()
    {
        foreach (var actor in Actors)
        {
            actor.Dispose();
        }
        Actors.Clear();
        _closedHandler?.Invoke();
        _closedHandler = null;
        Package.Release(this);
    }
}
