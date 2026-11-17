using System;
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

/// <summary>
/// Where a primitive masks LightShadow_ModulateBetter shadows off itself (bCullModulatedShadowOnBackfaces, bCullModulatedShadowOnEmissive)
/// </summary>
[Flags]
public enum ModulatedShadowCulling
{
    None = 0,
    /// <summary>Where the surface faces away from the shadow's light</summary>
    Backfaces = 1,
    /// <summary>Where the material is emissive</summary>
    Emissive = 2
}

public static class ModulatedShadowCullingFlags
{
    /// <summary>LE1's MeshComponent defaults both on, and SkeletalMeshComponent turns back faces off</summary>
    public static ModulatedShadowCulling Read(PropertyCollection props, bool skeletal) =>
        ((props.GetProp<BoolProperty>("bCullModulatedShadowOnBackfaces")?.Value ?? !skeletal) ? ModulatedShadowCulling.Backfaces : ModulatedShadowCulling.None)
        | ((props.GetProp<BoolProperty>("bCullModulatedShadowOnEmissive")?.Value ?? true) ? ModulatedShadowCulling.Emissive : ModulatedShadowCulling.None);
}
