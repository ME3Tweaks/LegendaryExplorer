using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.LevelEditor;

public interface IActorEditorContext
{
    LevelEditorRenderContext RenderContext { get; }
    bool IsApplyingUndoRedo { get; }
    PackageCache PackageCache => RenderContext.PackageCache;

    /// <summary>
    /// A light's uncommitted settings or transform changed, so the preview should rebuild it
    /// </summary>
    void OnLightEdited(ILightProxy light) { }

    /// <summary>A mesh component's pending lighting settings changed.</summary>
    void OnActorLightingEdited(MeshComponentProxy mesh, ActorLightingSettings before, ActorLightingSettings after) { }

    void OnDecalEdited(DecalActorProxy actor, DecalSettings before, DecalSettings after, string property) { }

    /// <summary>A loft point's uncommitted spline values changed</summary>
    void OnSplineEdited(SplineActorProxy actor, SplineLoftSettings before, SplineLoftSettings after, string property) { }

    /// <summary>The loaded, writable level that can receive a migrated decal.</summary>
    ExportEntry GetDecalDestinationLevel(IMEPackage package) => null;
}
