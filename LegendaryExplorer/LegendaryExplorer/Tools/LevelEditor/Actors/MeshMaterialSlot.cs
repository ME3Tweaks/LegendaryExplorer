namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A row of a static mesh component's materials (<see cref="StaticMeshComponentProxy.MaterialSlots"/>): its slot (MaterialIndex), drawn with its override,
/// or if it has none, with the mesh's own material (<paramref name="IsOverride"/> false). An extra one is an override past the mesh's slots, which the game
/// never reads, and can only be deleted
/// </summary>
/// <param name="Path">The material's path, and for another file's, which file. Null for none</param>
/// <param name="CanChange">Whether its component's materials can be changed now (<see cref="StaticMeshComponentProxy.MaterialChangeRefusal"/>)</param>
/// <param name="Problem">Why the game won't draw the override there, if it won't (<see cref="MaterialUsage.StaticMeshProblem"/>)</param>
public sealed record MeshMaterialSlot(StaticMeshComponentProxy Mesh, int Index, string Name, string Path, bool IsOverride, bool IsExtra, bool CanChange,
    string Problem)
{
    public bool IsDefault => !IsOverride;
    /// <summary>Whether it's one of the mesh's slots, whose material can be chosen or reverted (shown whether or not it can be now)</summary>
    public bool CanEdit => !IsExtra;
    public bool CanChoose => !IsExtra && CanChange;
    public bool CanRevert => IsOverride && !IsExtra && CanChange;
    public bool CanDelete => IsExtra && CanChange;
    public string ExtraNote => IsExtra ? $"{Mesh.MeshName} has no material slot {Index}, so the game never reads this override. Delete it from the Materials list." : null;
}
