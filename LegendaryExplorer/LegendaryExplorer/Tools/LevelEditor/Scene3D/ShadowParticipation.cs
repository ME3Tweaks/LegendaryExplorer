using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A primitive's shadow-casting flags, shared by the bake and the preview. Whether it takes part in static lighting at all (static shadowing and
/// bAcceptsLights) is decided by the caller.
/// </summary>
public readonly record struct ShadowParticipation(bool CastShadow, bool CastDynamicShadow, bool CastHiddenShadow, bool TwoSided,
    bool IndirectOnly, bool SelfShadowOnly)
{
    public static ShadowParticipation Read(PropertyCollection props) => new(
        props.GetProp<BoolProperty>("CastShadow") is not { Value: false },
        props.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false },
        props.GetProp<BoolProperty>("bCastHiddenShadow") is { Value: true },
        props.GetProp<BoolProperty>("bCastShadowAsTwoSided") is { Value: true },
        props.GetProp<StructProperty>("LightmassSettings")?.GetProp<BoolProperty>("bShadowIndirectOnly") is { Value: true },
        props.GetProp<BoolProperty>("bSelfShadowOnly") is { Value: true });

    /// <summary>
    /// Whether the primitive's static lighting mesh casts shadows: CastShadow | bCastHiddenShadow, whether or not it's hidden
    /// (FStaticMeshStaticLightingMesh, FTerrainComponentStaticLighting). The lighting build doesn't look at visibility
    /// </summary>
    public bool BakesShadow => CastShadow || CastHiddenShadow;

    /// <summary>
    /// Whether the primitive casts shadows at runtime: CastShadow and bCastDynamicShadow, and bCastHiddenShadow if it's hidden in game
    /// </summary>
    public bool CastsRuntimeShadow(bool hidden) => CastShadow && CastDynamicShadow && (!hidden || CastHiddenShadow);

    /// <summary>
    /// Whether the preview draws the light's direct shadow from the primitive onto others: the baked shadow, for a static shadowing primitive and a light
    /// with static shadowing, or else the runtime one. Self-shadow-only primitives shadow only themselves, which per-object shadows can't show, and
    /// indirect-only casters only block bounced light, which the preview has none of
    /// </summary>
    public bool CastsDirect(SceneLight light, bool staticShadowing, bool hidden)
    {
        if (SelfShadowOnly) return false;
        if (staticShadowing && light.HasStaticShadowing) return BakesShadow && !IndirectOnly && light.CastsStaticShadows;
        return CastsRuntimeShadow(hidden) && (light.HasStaticShadowing ? light.CastsDynamicShadowsWhenUnbuilt : light.CastsDynamicShadows);
    }
}
