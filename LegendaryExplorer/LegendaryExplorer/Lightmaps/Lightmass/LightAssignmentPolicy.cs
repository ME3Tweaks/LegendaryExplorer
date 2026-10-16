namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>Which lights a bake gives each primitive, and so which the Level Editor's preview lights it with</summary>
public enum LightAssignmentPolicy
{
    /// <summary>Lights the last bake recorded for other primitives reach a primitive only if its own records list them, as BioWare's bakes split them</summary>
    PreserveExistingAssignments,
    /// <summary>Lights reach primitives by their current settings and positions</summary>
    RecomputeAssignments
}

public static class LightAssignmentPolicyExtensions
{
    public static string Describe(this LightAssignmentPolicy policy) => policy == LightAssignmentPolicy.PreserveExistingAssignments
        ? "Preserve existing assignments" : "Recompute assignments";
}
