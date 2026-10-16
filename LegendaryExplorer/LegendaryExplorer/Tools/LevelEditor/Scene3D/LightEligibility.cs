namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Whether a light reaches a primitive under the viewport's current light-selection rules, and if not, the first rule that excludes it.
/// Eligibility doesn't mean that every point on the primitive is lit: occlusion is decided by the renderer.
/// Preview and bake share assignment eligibility; baked output and runtime routing remain separate.
/// </summary>
public enum LightEligibility
{
    /// <summary>
    /// Rendered on the primitive in its own pass, dynamically or with its precomputed shadows
    /// </summary>
    Affects,
    /// <summary>
    /// Level lighting only: part of the primitive's light-map, so it isn't rendered separately
    /// </summary>
    BakedIntoLightMap,
    /// <summary>
    /// Composited into the light environment that lights the primitive
    /// </summary>
    LightEnvironment,
    OutOfRange,
    ExclusionVolume,
    OutsideInclusionVolumes,
    LightingChannels,
    /// <summary>
    /// bOnlyAffectSameAndSpecifiedLevels, and the primitive's level isn't one of them
    /// </summary>
    OtherLevel,
    /// <summary>
    /// Level lighting only: the primitive's level was built with only its own static lights (PKG_SelfContainedLighting)
    /// </summary>
    SelfContainedLighting,
    /// <summary>
    /// bAcceptsDynamicLights is off, and the light has no static shadowing
    /// </summary>
    DynamicLightsNotAccepted,
    /// <summary>
    /// bAcceptsLights is off
    /// </summary>
    LightsNotAccepted,
    /// <summary>
    /// The light belongs to a light environment, so it only lights that environment's primitives
    /// </summary>
    BelongsToLightEnvironment,
    /// <summary>
    /// The primitive is lit by a light environment, so the level's lights don't light it directly (ULightComponent::AffectsPrimitive)
    /// </summary>
    LitByLightEnvironment,
    /// <summary>
    /// A dominant light, which light environments only composite with bForceCompositeAllLights
    /// </summary>
    DominantNotComposited,
    /// <summary>
    /// Level lighting only: the last lighting build recorded that the light doesn't reach the primitive (IrrelevantLights)
    /// </summary>
    BakedIrrelevant,
    /// <summary>
    /// Preserve existing assignments: the last bake gave the primitive's lightmap and shadow maps other lights than this one, which it knows from other primitives
    /// </summary>
    PreservedAssignment,
}

public static class LightEligibilityExtensions
{
    public static bool IsAffecting(this LightEligibility eligibility) =>
        eligibility is LightEligibility.Affects or LightEligibility.BakedIntoLightMap or LightEligibility.LightEnvironment;

    /// <summary>
    /// Whether the light reaches the primitive at all, whether or not a rule then excludes it
    /// </summary>
    public static bool Reaches(this LightEligibility eligibility) =>
        eligibility is not (LightEligibility.OutOfRange or LightEligibility.BelongsToLightEnvironment);

    /// <summary>
    /// Whether <paramref name="eligibility"/> says more about a light than <paramref name="other"/>, of the light's results for several primitives:
    /// affecting beats excluded, which beats not reaching. Ties go to the earlier rule
    /// </summary>
    public static bool IsMoreRelevantThan(this LightEligibility eligibility, LightEligibility other)
    {
        static int Rank(LightEligibility e) => e.IsAffecting() ? 0 : e.Reaches() ? 1 : 2;
        int rank = Rank(eligibility), otherRank = Rank(other);
        return rank != otherRank ? rank < otherRank : eligibility < other;
    }

    public static string Describe(this LightEligibility eligibility) => eligibility switch
    {
        LightEligibility.Affects => "Affects",
        LightEligibility.BakedIntoLightMap => "Affects through the baked light-map",
        LightEligibility.LightEnvironment => "Affects through the light environment",
        LightEligibility.OutOfRange => "Out of range",
        LightEligibility.ExclusionVolume => "Excluded by one of the light's exclusion volumes",
        LightEligibility.OutsideInclusionVolumes => "Outside the light's inclusion volumes",
        LightEligibility.LightingChannels => "No lighting channel in common",
        LightEligibility.OtherLevel => "The light only affects its own and specified levels",
        LightEligibility.SelfContainedLighting => "The mesh's level has self-contained lighting",
        LightEligibility.DynamicLightsNotAccepted => "The mesh doesn't accept dynamic lights",
        LightEligibility.LightsNotAccepted => "The mesh doesn't accept lights",
        LightEligibility.BelongsToLightEnvironment => "The light belongs to a light environment",
        LightEligibility.LitByLightEnvironment => "The mesh is lit by a light environment",
        LightEligibility.DominantNotComposited => "Dominant lights aren't composited into this light environment",
        LightEligibility.PreservedAssignment => "Excluded by preserved lightmap/shadowmap assignments",
        LightEligibility.BakedIrrelevant => "Excluded by the last lighting build",
        _ => eligibility.ToString()
    };
}
