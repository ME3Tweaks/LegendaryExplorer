namespace LegendaryExplorer.Tools.LevelEditor;

public interface IActorEditorContext
{
    LevelEditorRenderContext RenderContext { get; }
    bool IsApplyingUndoRedo { get; }
}
