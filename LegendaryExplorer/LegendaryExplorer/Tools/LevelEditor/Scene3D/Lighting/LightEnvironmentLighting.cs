using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// The lights a light environment synthesizes for its primitives (FDynamicLightEnvironmentState::CreateEnvironmentLightList)
/// </summary>
public sealed class LightEnvironmentLighting
{
    /// <summary>Synthesized lights (points, and cinematic Bio's directional lights), drawn only on this environment's primitives</summary>
    public IReadOnlyList<SceneLight> PointLights = Array.Empty<SceneLight>();

    /// <summary>Cinematic Bio's rim light, which follows the viewer; drawn like <see cref="PointLights"/></summary>
    public BioRimLight RimLight;

    /// <summary>
    /// Level lights drawn directly on the environment's primitives, besides (LE1) or instead of (LE2/LE3 non-composited bypass lights)
    /// the synthesized lighting. Each primitive applies its own affects test
    /// </summary>
    public IReadOnlyList<SceneLight> DirectLights = Array.Empty<SceneLight>();

    /// <summary>Modulated shadows from synthesized points (LE1 MultiLight's first point), besides <see cref="ShadowLight"/></summary>
    public IReadOnlyList<LightEnvironmentShadowLight> PointShadowLights = Array.Empty<LightEnvironmentShadowLight>();

    /// <summary>Every modulated shadow the environment casts</summary>
    public IEnumerable<LightEnvironmentShadowLight> ShadowLights
    {
        get
        {
            if (ShadowLight is not null) yield return ShadowLight;
            foreach (LightEnvironmentShadowLight light in PointShadowLights) yield return light;
        }
    }

    /// <summary>
    /// The synthesized directional light: the direction towards it, and its linear color. Color is zero if there isn't one
    /// </summary>
    public Vector3 DirectionToLight = Vector3.UnitZ;
    public Vector3 DirectionalColor;

    /// <summary>
    /// The remaining light, as an SH light if <see cref="UsesSHLight"/>, otherwise as a sky light (<see cref="UpperSkyColor"/> and <see cref="LowerSkyColor"/>).
    /// The sky light is always computed, for materials without the shaders for an SH light
    /// </summary>
    public bool UsesSHLight;
    public SHVectorRGB SHIncidentLighting;
    public Vector3 UpperSkyColor;
    public Vector3 LowerSkyColor;

    /// <summary>
    /// The point light that casts the light environment's modulated shadow. Null if it doesn't cast one
    /// </summary>
    public LightEnvironmentShadowLight ShadowLight;

    /// <summary>
    /// The light environment casts shadows (bCastShadows), so UE3 renders its SH light after modulated shadows (the SH light's bRenderBeforeModShadows is false),
    /// whether or not it has a shadow light
    /// </summary>
    public bool RendersSHLightAfterModulatedShadows;

    private LightEnvironmentLighting directionalOnly;

    /// <summary>
    /// These lights without the SH or sky light, for the base pass of a mesh whose SH light is rendered in its own pass after the modulated shadows.
    /// UE3 does that when the light environment casts a shadow (the SH light's bRenderBeforeModShadows is false), so that the shadow doesn't darken it
    /// </summary>
    public LightEnvironmentLighting DirectionalOnly => directionalOnly ??= new LightEnvironmentLighting
    {
        DirectionToLight = DirectionToLight,
        DirectionalColor = DirectionalColor,
        ShadowLight = ShadowLight,
        PointShadowLights = PointShadowLights,
        PointLights = PointLights,
        RimLight = RimLight,
        DirectLights = DirectLights,
    };
}

/// <summary>
/// The light a light environment creates to cast its primitives' shadow: a point light of no brightness, whose shadow modulates the scene by <see cref="ModShadowColor"/>
/// </summary>
/// <param name="CullsOnSubject">LE1 bCullModulatedShadowOnSubject (CinematicPlusMod), or LE2/LE3 cinematic Bio: the shadow doesn't darken its own primitives</param>
/// <param name="ModulateBetter">LightShadow_ModulateBetter: receivers mask the shadow off where they face away from the light or are emissive</param>
public sealed record LightEnvironmentShadowLight(Vector3 Position, float Radius, float MinShadowFalloffRadius, float ShadowFalloffExponent, Vector3 ModShadowColor,
    int MinShadowResolution, int MaxShadowResolution, int ShadowFadeResolution, float ShadowRadiusMultiplier = 1.1f, bool CullsOnSubject = false, bool ModulateBetter = false);
