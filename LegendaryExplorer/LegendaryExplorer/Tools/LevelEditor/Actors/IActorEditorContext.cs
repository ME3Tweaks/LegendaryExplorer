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

    /// <summary>A mesh component was given another mesh to draw, or its own again (<see cref="StaticMeshComponentProxy.ShowMesh"/>), by an edit or an undo step</summary>
    void OnMeshChosen(StaticMeshComponentProxy mesh) { }

    /// <summary>A loft point's uncommitted spline values changed</summary>
    void OnSplineEdited(SplineActorProxy actor, SplineLoftSettings before, SplineLoftSettings after, string property) { }

    /// <summary>A spline topology or mesh edit, already applied as one undo step.</summary>
    void OnSplineConnectionsEdited(IUndoAction action) { }

    /// <summary>The loaded, writable level that can receive a migrated decal.</summary>
    ExportEntry GetDecalDestinationLevel(IMEPackage package) => null;

    /// <summary>A <see cref="SceneEdit"/> drew a view, which is now one of the editor's actors</summary>
    void OnViewAttached(ActorProxy view) { }

    /// <summary>A <see cref="SceneEdit"/> stopped drawing a view (without disposing it), which is no longer one of the editor's actors</summary>
    void OnViewDetached(ActorProxy view) { }

    /// <summary>Selects an actor, or nothing, as the editor's own selection (with its inspector and gizmos)</summary>
    void Select(ActorProxy actor) => RenderContext.SelectedActor = actor;
}
