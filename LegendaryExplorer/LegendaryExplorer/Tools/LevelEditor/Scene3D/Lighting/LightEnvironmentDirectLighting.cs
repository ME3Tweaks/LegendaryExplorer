using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// The per-primitive test for <see cref="LightEnvironmentLighting.DirectLights"/>: ULightComponent::AffectsPrimitive after its environment test
/// </summary>
internal static class LightEnvironmentDirectLighting
{
    //FLightPrimitiveInteraction (RVAs LE1 0x739390, LE2 0x3228d0, LE3 0x33ca00): non-static primitives take only the composite shadow
    //from composite-shadow lights; dominant and non-composite lights keep their own shadow, and static receivers the ordinary test.
    public static bool CastsSeparateShadow(SceneLight light, LightEnvironmentPrimitive primitive) =>
        primitive.DrawShadowCaster is not null && !primitive.HasShadowParent() && !primitive.SelfShadowOnly && light.CastsDynamicShadows
        && (!primitive.HasStaticShadowing || !light.HasStaticShadowing)
        && (primitive.HasStaticShadowing || light.IsDominant || !light.CastsCompositeShadow);

    public static LightEligibility Evaluate(SceneLight light, LightEnvironmentPrimitive primitive, BoxSphereBounds bounds, IMEPackage level) =>
        light.BakedGIOnly ? LightEligibility.BakedGIOnly : LightAssignment.Evaluate(light, bounds, new LightPrimitiveSettings(primitive.LightingChannels, level.FileNameNoExtension,
            primitive.AcceptsLights, primitive.AcceptsDynamicLights, level.Flags.Has(UnrealFlags.EPackageFlags.SelfContainedLighting)),
            honorSelfContained: true);

    public static bool Affects(SceneLight light, LightEnvironmentPrimitive primitive, BoxSphereBounds bounds, IMEPackage level) =>
        Evaluate(light, primitive, bounds, level) is LightEligibility.Affects;

    //Sky lights have no additive shaders; their direct contribution is per primitive, apart from the environment's synthesized sky.
    public static SkyLighting GetSkyLighting(LightEnvironmentLighting lighting, LightEnvironmentPrimitive primitive, BoxSphereBounds bounds, IMEPackage level)
    {
        Vector3 upper = Vector3.Zero, lower = Vector3.Zero;
        foreach (SceneLight light in lighting.DirectLights)
        {
            if (light.Type is SceneLightType.Sky && Affects(light, primitive, bounds, level))
            {
                upper += light.Color;
                lower += light.LowerColor;
            }
        }
        return new SkyLighting(upper, lower);
    }
}
