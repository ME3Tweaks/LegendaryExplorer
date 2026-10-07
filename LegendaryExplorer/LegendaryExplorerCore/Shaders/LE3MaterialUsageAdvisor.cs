using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Shaders;

/// <summary>LE3 EMaterialUsage, in native order (GetUsageByFlag 0x7ff74ab24340).</summary>
public enum LE3MaterialUsage
{
    SkeletalMesh = 0,
    FracturedMeshes = 1,
    ParticleSprites = 2,
    BeamTrails = 3,
    ParticleSubUV = 4,
    Foliage = 5,
    SpeedTree = 6,
    StaticLighting = 7,
    GammaCorrection = 8,
    LensFlare = 9,
    InstancedMeshParticles = 10,
    FluidSurface = 11,
    Decals = 12,
    MaterialEffect = 13,
    MorphTargets = 14,
    FogVolumes = 15,
    RadialBlur = 16,
    InstancedMeshes = 17,
    SplineMesh = 18,
    ScreenDoorFade = 19,
    APEXMesh = 20,
    LightEnvironments = 21,
    VectorLightMaps = 22,
    SimpleLightMaps = 23,
}

/// <summary>Result of <see cref="LE3MaterialUsageAdvisor.Check(PropertyCollection, IEnumerable{LE3MaterialUsage})"/>.</summary>
/// <param name="MissingFlags">bUsedWith* properties to set to true on the base Material before compiling.</param>
/// <param name="Vetoed">Intended uses the game rejects whatever the flags say: a decal-only or fog-volume-only material.</param>
/// <param name="IsSpecialEngineMaterial">bUsedAsSpecialEngineMaterial is set, so every non-vetoed use is accepted.</param>
public sealed record LE3MaterialUsageReport(IReadOnlyList<string> MissingFlags, IReadOnlyList<LE3MaterialUsage> Vetoed, bool IsSpecialEngineMaterial)
{
    public bool IsReady => MissingFlags.Count == 0 && Vetoed.Count == 0;
}

/// <summary>
/// Mirrors the shipping UMaterial::CheckMaterialUsage worker (0x7ff74ab2e260) as a pure query. The game never
/// sets a usage flag at runtime: a use whose flag is clear either falls back to the default material (terrain
/// foliage, lightmapped static meshes, decals, fog volumes) or reaches a fatal missing-shader error at draw
/// time. The flags also select which vertex-factory shaders a complete shader map must hold, so set them
/// before compiling. Read flags from the base Material; instances defer to it.
/// </summary>
public static class LE3MaterialUsageAdvisor
{
    private static readonly string[] FlagNames =
    [
        "bUsedWithSkeletalMesh", "bUsedWithFracturedMeshes", "bUsedWithParticleSprites", "bUsedWithBeamTrails",
        "bUsedWithParticleSubUV", "bUsedWithFoliage", "bUsedWithSpeedTree", "bUsedWithStaticLighting",
        "bUsedWithGammaCorrection", "bUsedWithLensFlare", "bUsedWithInstancedMeshParticles", "bUsedWithFluidSurfaces",
        "bUsedWithDecals", "bUsedWithMaterialEffect", "bUsedWithMorphTargets", "bUsedWithFogVolumes",
        "bUsedWithRadialBlur", "bUsedWithInstancedMeshes", "bUsedWithSplineMeshes", "bUsedWithScreenDoorFade",
        "bUsedWithAPEXMeshes", "bUsedWithLightEnvironments", "bUsedWithVectorLightMaps", "bUsedWithSimpleLightMaps",
    ];

    // Uses (bit = EMaterialUsage) the worker rejects when bUsedWithDecals / bUsedWithFogVolumes is set.
    private const uint DecalVetoMask = 0x16ce7f;
    private const uint FogVolumeVetoMask = 0x165e7f;

    /// <summary>The Material property that stores <paramref name="usage"/>.</summary>
    public static string GetFlagName(LE3MaterialUsage usage) =>
        (uint)usage < FlagNames.Length ? FlagNames[(int)usage] : throw new ArgumentOutOfRangeException(nameof(usage));

    /// <summary>Usages a mesh or lighting setup needs together, for common cases.</summary>
    public static IReadOnlyList<LE3MaterialUsage> LightmappedStaticMesh(bool simpleLightMaps) =>
        [LE3MaterialUsage.StaticLighting, simpleLightMaps ? LE3MaterialUsage.SimpleLightMaps : LE3MaterialUsage.VectorLightMaps];

    /// <summary>Checks a base Material's stored properties against its intended uses.</summary>
    public static LE3MaterialUsageReport Check(PropertyCollection materialProperties, IEnumerable<LE3MaterialUsage> intendedUses)
    {
        ArgumentNullException.ThrowIfNull(materialProperties);
        bool Flag(string name) => materialProperties.GetProp<BoolProperty>(name)?.Value == true;
        bool special = Flag("bUsedAsSpecialEngineMaterial");
        uint veto = (Flag("bUsedWithDecals") ? DecalVetoMask : 0) | (Flag("bUsedWithFogVolumes") ? FogVolumeVetoMask : 0);

        var missing = new List<string>();
        var vetoed = new List<LE3MaterialUsage>();
        foreach (var usage in intendedUses.Distinct().OrderBy(u => u))
        {
            string name = GetFlagName(usage);
            if ((veto >> (int)usage & 1) != 0) vetoed.Add(usage);
            else if (!special && !Flag(name)) missing.Add(name);
        }
        return new LE3MaterialUsageReport(missing, vetoed, special);
    }
}
