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
}
