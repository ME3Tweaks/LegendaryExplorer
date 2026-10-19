using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Lightmaps.Lightmass;

public enum LightmassQuality
{
    Preview,
    Medium,
    High,
    Production,
}

/// <summary>Typed reads of Lightmass's ini (UDK's BaseLightmass.ini), which fail on a missing key rather than guess a default.</summary>
public static class LightmassIni
{
    /// <summary>
    /// Applies the solver settings BioWare changed from UE3's BaseLightmass.ini for ME3. Their other changes don't apply here:
    /// bAllowLightmapCompression and DefaultStaticMeshLightingRes are the editor's (the bake keeps the levels' DXT1 atlases and
    /// allocations), and bVisualizeVolumeLightSamples only makes Lightmass return debug samples for the editor to draw.
    /// SurfaceLightSampleSpacing is for volume samples, which the bake doesn't import; a coarser spacing just makes them cheaper.
    /// </summary>
    public static void ApplyBioWareOverrides(this DuplicatingIni ini)
    {
        Set(ini, "DevOptions.StaticShadows", "bFilterShadowFactor", "False");
        Set(ini, "DevOptions.StaticShadows", "MinUnoccludedFraction", "0.01");
        Set(ini, "DevOptions.StaticShadows", "NumBounceShadowRays", "4");
        Set(ini, "DevOptions.PrecomputedDynamicObjectLighting", "SurfaceLightSampleSpacing", "750");
    }

    /// <summary>Replaces the key whatever its case: reads match it case-insensitively, but SetSingleEntry only removes an exact match.</summary>
    static void Set(DuplicatingIni ini, string section, string key, string value)
    {
        var s = ini.GetOrAddSection(section);
        s.Entries.RemoveAll(e => key.Equals(e.Key, StringComparison.OrdinalIgnoreCase));
        s.Entries.Add(new DuplicatingIni.IniEntry(key, value));
    }

    public static string Get(this DuplicatingIni ini, string section, string key) =>
        ini.GetValue(section, key)?.Value ?? throw new KeyNotFoundException($"[{section}] {key} is missing from the Lightmass ini");

    public static float Float(this DuplicatingIni ini, string section, string key) => float.Parse(ini.Get(section, key), CultureInfo.InvariantCulture);
    public static int Int(this DuplicatingIni ini, string section, string key) => int.Parse(ini.Get(section, key), CultureInfo.InvariantCulture);
    public static bool Bool(this DuplicatingIni ini, string section, string key) => ini.Get(section, key).Equals("True", StringComparison.OrdinalIgnoreCase);

    /// <summary>A struct value such as (R=0.5,G=0.5,B=0.5).</summary>
    public static Vector3 Color(this DuplicatingIni ini, string section, string key)
    {
        string v = ini.Get(section, key);
        float Part(string name)
        {
            int i = v.IndexOf(name + "=", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return 0;
            int start = i + name.Length + 1, end = start;
            while (end < v.Length && v[end] != ',' && v[end] != ')') end++;
            return float.Parse(v[start..end], CultureInfo.InvariantCulture);
        }
        return new Vector3(Part("R"), Part("G"), Part("B"));
    }
}

/// <summary>
/// WorldInfo.LightmassSettings (UE3 LightmassWorldInfoSettings). LE3 levels rarely set any of it, so the class defaults
/// (IB WorldInfo.uc, which LE3's Engine.pcc keeps) usually apply: notably DiffuseBoost 5.
/// </summary>
public sealed class LightmassWorldSettings
{
    public float StaticLightingLevelScale = 1;
    public int NumIndirectLightingBounces = 3;
    public Vector3 EnvironmentColor = Vector3.Zero;
    public float EnvironmentIntensity = 1;
    public float EmissiveBoost = 1;
    public float DiffuseBoost = 5;
    public float SpecularBoost = 1;
    public float IndirectNormalInfluenceBoost = 0.3f;
    public bool UseNormalMapsForSimpleLightMaps;
    public bool UseAmbientOcclusion;
    public float DirectIlluminationOcclusionFraction = 0.5f;
    public float IndirectIlluminationOcclusionFraction = 1;
    public float OcclusionExponent = 1;
    public float FullyOccludedSamplesFraction = 1;
    public float MaxOcclusionDistance = 200;

    public static LightmassWorldSettings FromWorldInfo(ExportEntry worldInfo)
    {
        var settings = new LightmassWorldSettings();
        if (worldInfo?.GetProperty<StructProperty>("LightmassSettings") is not { } s) return settings;
        float F(string name, float fallback) => s.GetProp<FloatProperty>(name)?.Value ?? fallback;
        bool B(string name, bool fallback) => s.GetProp<BoolProperty>(name)?.Value ?? fallback;
        settings.StaticLightingLevelScale = F("StaticLightingLevelScale", settings.StaticLightingLevelScale);
        settings.NumIndirectLightingBounces = s.GetProp<IntProperty>("NumIndirectLightingBounces")?.Value ?? settings.NumIndirectLightingBounces;
        if (s.GetProp<StructProperty>("EnvironmentColor") is { } color)
        {
            // FLinearColor(FColor): gamma 2.2 decode
            float C(string c) => MathF.Pow((color.GetProp<ByteProperty>(c)?.Value ?? 0) / 255f, 2.2f);
            settings.EnvironmentColor = new Vector3(C("R"), C("G"), C("B"));
        }
        settings.EnvironmentIntensity = F("EnvironmentIntensity", settings.EnvironmentIntensity);
        settings.EmissiveBoost = F("EmissiveBoost", settings.EmissiveBoost);
        settings.DiffuseBoost = F("DiffuseBoost", settings.DiffuseBoost);
        settings.SpecularBoost = F("SpecularBoost", settings.SpecularBoost);
        settings.IndirectNormalInfluenceBoost = F("IndirectNormalInfluenceBoost", settings.IndirectNormalInfluenceBoost);
        settings.UseNormalMapsForSimpleLightMaps = B("bUseNormalMapsForSimpleLightMaps", settings.UseNormalMapsForSimpleLightMaps);
        settings.UseAmbientOcclusion = B("bUseAmbientOcclusion", settings.UseAmbientOcclusion);
        settings.DirectIlluminationOcclusionFraction = F("DirectIlluminationOcclusionFraction", settings.DirectIlluminationOcclusionFraction);
        settings.IndirectIlluminationOcclusionFraction = F("IndirectIlluminationOcclusionFraction", settings.IndirectIlluminationOcclusionFraction);
        settings.OcclusionExponent = F("OcclusionExponent", settings.OcclusionExponent);
        settings.FullyOccludedSamplesFraction = F("FullyOccludedSamplesFraction", settings.FullyOccludedSamplesFraction);
        settings.MaxOcclusionDistance = F("MaxOcclusionDistance", settings.MaxOcclusionDistance);
        return settings;
    }
}

/// <summary>
/// Writes FSceneFileHeader the way FLightmassExporter::WriteToChannel and WriteSceneSettings fill it (IB UnrealEd/Src/Lightmass.cpp:660,
/// 2569): solver settings from the Lightmass ini, scaled by the quality level's section, and the level's LightmassWorldInfoSettings.
///
/// The layout is UDK-2015-02's, which grew from IB's 864 bytes to 960 without a version change. Offsets come from UDK.exe's
/// WriteSceneSettings (the ini key each field is read with) and UnrealLightmass.exe's FScene::Import (the counts, and its 0x3C0 read):
/// <list type="bullet">
/// <item>Scene constants: the linear-space simple lightmap flag is gone. 0x84 is bUseFixedScaleForSimpleLightmaps and 0x88 its value
/// (what QuantizeLightSamples takes); 0x8C is unused.</item>
/// <item>Material settings: after the environment colour, a flag (0xE4), a second colour (0xE8), a cone exponent (0xF8) and a
/// direction (0x100), all off by default.</item>
/// <item>Dynamic object settings: bUseMaxSurfaceSampleNum and MaxSurfaceLightSamples (0x16C).</item>
/// <item>A volume distance field section (0x1A0) before the shadow settings.</item>
/// <item>Two more counts: landscape instances (0x390) and landscape mappings (0x3AC).</item>
/// </list>
/// </summary>
public static class LightmassSceneHeader
{
    public const int Size = 0x3C0;

    public sealed record Counts(int ImportanceVolumes, int DirectionalLights, int PointLights, int SpotLights, int SkyLights,
        int StaticMeshes, int Terrains, int StaticMeshInstances, int BspMappings, int TextureMappings, int VertexMappings, int TerrainMappings);

    public static void Write(LightmassWriter w, DuplicatingIni ini, LightmassQuality quality, LightmassWorldSettings world, Counts counts)
    {
        long start = w.Position;
        const string StaticLighting = "DevOptions.StaticLighting", Constants = "DevOptions.StaticLightingSceneConstants",
            Material = "DevOptions.StaticLightingMaterial", AreaLights = "DevOptions.MeshAreaLights", Dynamic = "DevOptions.PrecomputedDynamicObjectLighting",
            Visibility = "DevOptions.PrecomputedVisibility", DistanceField = "DevOptions.VolumeDistanceField", Shadows = "DevOptions.StaticShadows", Importance = "DevOptions.ImportanceTracing",
            Photons = "DevOptions.PhotonMapping", Cache = "DevOptions.IrradianceCache";
        // Preview has no section: every scale is 1.
        string q = quality switch
        {
            LightmassQuality.Medium => "DevOptions.StaticLightingMediumQuality",
            LightmassQuality.High => "DevOptions.StaticLightingHighQuality",
            LightmassQuality.Production => "DevOptions.StaticLightingProductionQuality",
            _ => null,
        };
        float Scale(string key) => q == null ? 1f : ini.Float(q, key);
        static float Radians(float degrees) => degrees * MathF.PI / 180f;

        w.UInt(0x5343454E); // 'SCEN' as a multi-character literal
        w.Guid(LightmassFormat.FromUe3(0, 0, 0, 1)); // FormatVersion
        w.Guid(LightmassFormat.FromUe3(0, 0, 0, 1)); // Guid

        // FStaticLightingSettings (0x24)
        w.Bool(ini.Bool(StaticLighting, "bAllowMultiThreadedStaticLighting"));
        w.Int(0); // NumUnusedLocalCores
        w.Int(world.NumIndirectLightingBounces);
        w.Int(ini.Int(StaticLighting, "ViewSingleBounceNumber"));
        w.Bool(ini.Bool(StaticLighting, "bUseConservativeTexelRasterization"));
        w.Bool(ini.Bool(StaticLighting, "bAccountForTexelSize"));
        w.Bool(ini.Bool(StaticLighting, "bUseMaxWeight"));
        w.Int(ini.Int(StaticLighting, "MaxTriangleLightingSamples"));
        w.Int(ini.Int(StaticLighting, "MaxTriangleIrradiancePhotonCacheSamples"));
        w.Bool(false); // bUseErrorColoring
        w.LinearColor(Vector3.Zero); // UnmappedTexelColor

        // FStaticLightingSceneConstants (0x5C)
        w.PadTo(start, 0x5C);
        w.Float(ini.Float(Constants, "StaticLightingLevelScale") * world.StaticLightingLevelScale);
        w.Float(ini.Float(Constants, "VisibilityRayOffsetDistance"));
        w.Float(ini.Float(Constants, "VisibilityNormalOffsetDistance"));
        w.Float(ini.Float(Constants, "VisibilityNormalOffsetSampleRadiusScale"));
        w.Float(ini.Float(Constants, "VisibilityTangentOffsetSampleRadiusScale"));
        w.Float(ini.Float(Constants, "SmallestTexelRadius"));
        w.Int(ini.Int(Constants, "LightGridSize"));
        w.Float(ini.Float(Constants, "DirectionalCoefficientFalloffPower"));
        w.Float(ini.Float(Constants, "DirectionalCoefficientScale"));
        w.Float(world.IndirectNormalInfluenceBoost);
        // UE3 fixes the simple coefficient's scale at 2, which clips bright simple lighting; LMT_6 needs the full range.
        w.Bool(false); // bUseFixedScaleForSimpleLightmaps
        w.Float(2f); // UseFixedScaleValue
        w.Int(0); // 0x8C, unused

        // FSceneMaterialSettings (0x90)
        w.PadTo(start, 0x90);
        w.Bool(ini.Bool(Material, "bUseDebugMaterial"));
        w.Int(0); // ViewMaterialAttribute: VMA_None
        w.Int(ini.Int(Material, "EmissiveSampleSize"));
        w.Int(ini.Int(Material, "DiffuseSampleSize"));
        w.Int(ini.Int(Material, "SpecularSampleSize"));
        w.Int(ini.Int(Material, "TransmissionSampleSize"));
        w.Int(ini.Int(Material, "NormalSampleSize"));
        w.Bool(world.UseNormalMapsForSimpleLightMaps);
        w.LinearColor(ini.Color(Material, "DebugDiffuse"));
        w.LinearColor(ini.Color(Material, "DebugSpecular"));
        w.Float(ini.Float(Material, "DebugSpecularPower"));
        w.LinearColor(world.EnvironmentColor * world.EnvironmentIntensity);
        // UDK's additions, as it writes them for WorldInfo defaults: off, black, a cone exponent log(0.5) / log(0.5 - 0.5 cos 0°)
        // (the cosine clamped to 0.99999), +X
        w.Bool(false);
        w.LinearColor(Vector3.Zero, 0);
        w.Float(-0.30103f / MathF.Log10(0.5f - 0.5f * 0.99999f));
        w.Int(0);
        w.Vector4(Vector3.UnitX, 1);

        // FMeshAreaLightSettings (0x110)
        w.PadTo(start, 0x110);
        w.Bool(ini.Bool(AreaLights, "bVisualizeMeshAreaLightPrimitives"));
        w.Float(ini.Float(AreaLights, "EmissiveIntensityThreshold"));
        w.Int(ini.Int(AreaLights, "MeshAreaLightGridSize"));
        w.Float(MathF.Cos(Radians(Math.Clamp(ini.Float(AreaLights, "MeshAreaLightSimplifyNormalAngleThreshold"), 0, 90))));
        w.Float(ini.Float(AreaLights, "MeshAreaLightSimplifyCornerDistanceThreshold"));
        w.Float(ini.Float(AreaLights, "MeshAreaLightSimplifyMeshBoundingRadiusFractionThreshold"));
        w.Float(ini.Float(AreaLights, "MeshAreaLightGeneratedDynamicLightSurfaceOffset"));

        // FAmbientOcclusionSettings (0x12C)
        w.PadTo(start, 0x12C);
        w.Bool(world.UseAmbientOcclusion);
        w.Bool(false); // bVisualizeAmbientOcclusion
        w.Float(world.DirectIlluminationOcclusionFraction);
        w.Float(world.IndirectIlluminationOcclusionFraction);
        w.Float(world.OcclusionExponent);
        w.Float(world.FullyOccludedSamplesFraction);
        w.Float(world.MaxOcclusionDistance);

        // FDynamicObjectSettings (0x148)
        w.PadTo(start, 0x148);
        w.Bool(ini.Bool(Dynamic, "bVisualizeVolumeLightSamples"));
        w.Bool(ini.Bool(Dynamic, "bVisualizeVolumeLightInterpolation"));
        w.Float(ini.Float(Dynamic, "SurfaceLightSampleSpacing"));
        w.Float(ini.Float(Dynamic, "FirstSurfaceSampleLayerHeight"));
        w.Float(ini.Float(Dynamic, "SurfaceSampleLayerHeightSpacing"));
        w.Int(ini.Int(Dynamic, "NumSurfaceSampleLayers"));
        w.Float(ini.Float(Dynamic, "DetailVolumeSampleSpacing"));
        w.Float(ini.Float(Dynamic, "VolumeLightSampleSpacing"));
        w.Int(ini.Int(Dynamic, "MaxVolumeSamples"));
        w.Bool(ini.Bool(Dynamic, "bUseMaxSurfaceSampleNum"));
        w.Int(ini.Int(Dynamic, "MaxSurfaceLightSamples"));

        // FPrecomputedVisibilitySettings (0x174); visibility isn't built
        w.PadTo(start, 0x174);
        w.Bool(false); // bVisualizePrecomputedVisibility
        w.Bool(ini.Bool(Visibility, "bPlaceCellsOnOpaqueOnly"));
        w.Bool(true); // bPlaceCellsOnSurfaces (WorldInfo default)
        w.Float(200f); // CellSize (WorldInfo VisibilityCellSize default)
        w.Int(ini.Int(Visibility, "NumCellDistributionBuckets"));
        w.Float(ini.Float(Visibility, "PlayAreaHeight"));
        w.Float(ini.Float(Visibility, "MeshBoundsScale"));
        w.Int(ini.Int(Visibility, "MinMeshSamples"));
        w.Int(ini.Int(Visibility, "MaxMeshSamples"));
        w.Int(ini.Int(Visibility, "NumCellSamples"));
        w.Int(ini.Int(Visibility, "NumImportanceSamples"));

        // Volume distance field (0x1A0)
        w.PadTo(start, 0x1A0);
        w.Float(ini.Float(DistanceField, "VoxelSize"));
        w.Float(ini.Float(DistanceField, "VolumeMaxDistance"));
        w.Int(ini.Int(DistanceField, "NumVoxelDistanceSamples"));
        w.Int(ini.Int(DistanceField, "MaxVoxels"));

        // FStaticShadowSettings (0x1B0)
        w.PadTo(start, 0x1B0);
        w.Bool(ini.Bool(Shadows, "bUseZeroAreaLightmapSpaceFilteredLights"));
        w.Int((int)(ini.Int(Shadows, "NumShadowRays") * Scale("NumShadowRaysScale")));
        w.Int((int)(ini.Int(Shadows, "NumPenumbraShadowRays") * Scale("NumPenumbraShadowRaysScale")));
        w.Int(ini.Int(Shadows, "NumBounceShadowRays"));
        w.Bool(ini.Bool(Shadows, "bFilterShadowFactor"));
        w.Float(ini.Float(Shadows, "ShadowFactorGradientTolerance"));
        w.Bool(ini.Bool(Shadows, "bAllowSignedDistanceFieldShadows"));
        w.Float(ini.Float(Shadows, "MaxTransitionDistanceWorldSpace"));
        w.Int((int)(ini.Int(Shadows, "ApproximateHighResTexelsPerMaxTransitionDistance") * Scale("ApproximateHighResTexelsPerMaxTransitionDistanceScale")));
        w.Int(q == null ? ini.Int(Shadows, "MinDistanceFieldUpsampleFactor") : ini.Int(q, "MinDistanceFieldUpsampleFactor"));
        w.Float(ini.Float(Shadows, "DominantShadowTransitionSampleDistanceX"));
        w.Float(ini.Float(Shadows, "DominantShadowTransitionSampleDistanceY"));
        w.Int(ini.Int(Shadows, "DominantShadowSuperSampleFactor"));
        w.Int(ini.Int(Shadows, "DominantShadowMaxSamples"));
        w.Float(ini.Float(Shadows, "MinUnoccludedFraction"));

        // FImportanceTracingSettings (0x1EC)
        w.PadTo(start, 0x1EC);
        w.Bool(ini.Bool(Importance, "bUsePathTracer"));
        w.Bool(ini.Bool(Importance, "bUseCosinePDF"));
        w.Bool(ini.Bool(Importance, "bUseStratifiedSampling"));
        w.Int(ini.Int(Importance, "NumPaths"));
        w.Int((int)(ini.Int(Importance, "NumHemisphereSamples") * Scale("NumHemisphereSamplesScale")));
        w.Int(ini.Int(Importance, "NumBounceHemisphereSamples"));
        w.Float(Radians(ini.Float(Importance, "MaxHemisphereRayAngle")));

        // FPhotonMappingSettings (0x208)
        w.PadTo(start, 0x208);
        foreach (string key in (ReadOnlySpan<string>)["bUsePhotonMapping", "bUseFinalGathering", "bUsePhotonsForDirectLighting", "bUseIrradiancePhotons",
                     "bCacheIrradiancePhotonsOnSurfaces", "bOptimizeDirectLightingWithPhotons", "bVisualizePhotonPaths", "bVisualizePhotonGathers",
                     "bVisualizePhotonImportanceSamples", "bVisualizeIrradiancePhotonCalculation", "bEmitPhotonsOutsideImportanceVolume"])
            w.Bool(ini.Bool(Photons, key));
        w.Float(ini.Float(Photons, "ConeFilterConstant"));
        w.Int(ini.Int(Photons, "NumIrradianceCalculationPhotons"));
        w.Float(ini.Float(Photons, "FinalGatherImportanceSampleFraction"));
        w.Float(MathF.Cos(Radians(Math.Clamp(ini.Float(Photons, "FinalGatherImportanceSampleConeAngle"), 0, 90))));
        w.Float(ini.Float(Photons, "IndirectPhotonEmitDiskRadius"));
        w.Float(Radians(Math.Clamp(ini.Float(Photons, "IndirectPhotonEmitConeAngle"), 0, 90)));
        w.Float(ini.Float(Photons, "MaxImportancePhotonSearchDistance"));
        w.Float(ini.Float(Photons, "MinImportancePhotonSearchDistance"));
        w.Int((int)(ini.Int(Photons, "NumImportanceSearchPhotons") * Scale("NumImportanceSearchPhotonsScale")));
        w.Float(ini.Float(Photons, "OutsideImportanceVolumeDensityScale"));
        w.Float(ini.Float(Photons, "DirectPhotonDensity") * Scale("NumDirectPhotonsScale"));
        w.Float(ini.Float(Photons, "DirectIrradiancePhotonDensity") * Scale("NumDirectPhotonsScale"));
        w.Float(ini.Float(Photons, "DirectPhotonSearchDistance") * Scale("DirectPhotonSearchDistanceScale"));
        w.Float(ini.Float(Photons, "IndirectPhotonPathDensity") * Scale("NumIndirectPhotonPathsScale"));
        w.Float(ini.Float(Photons, "IndirectPhotonDensity") * Scale("NumIndirectPhotonsScale"));
        w.Float(ini.Float(Photons, "IndirectIrradiancePhotonDensity") * Scale("NumIndirectIrradiancePhotonsScale"));
        w.Float(ini.Float(Photons, "IndirectPhotonSearchDistance"));
        w.Float(ini.Float(Photons, "CausticPhotonDensity") * Scale("NumCausticPhotonsScale"));
        w.Float(ini.Float(Photons, "CausticPhotonSearchDistance"));
        w.Float(ini.Float(Photons, "PhotonSearchAngleThreshold"));
        w.Float(MathF.Cos(Radians(90f - Math.Clamp(ini.Float(Photons, "IrradiancePhotonSearchConeAngle"), 1, 90))));
        w.Float(MathF.Cos(Radians(Math.Clamp(ini.Float(Photons, "MaxDirectLightingPhotonOptimizationAngle"), 0, 90))));
        w.Float(ini.Float(Photons, "CachedIrradiancePhotonDownsampleFactor"));
        w.Float(ini.Float(Photons, "OutsideVolumeIrradiancePhotonDistanceThreshold"));

        // FIrradianceCachingSettings (0x294)
        w.PadTo(start, 0x294);
        w.Bool(ini.Bool(Cache, "bAllowIrradianceCaching"));
        w.Bool(ini.Bool(Cache, "bUseIrradianceGradients"));
        w.Bool(ini.Bool(Cache, "bShowGradientsOnly"));
        w.Bool(ini.Bool(Cache, "bVisualizeIrradianceSamples"));
        w.Float(ini.Float(Cache, "RecordRadiusScale") * Scale("RecordRadiusScaleScale"));
        // UE3 scales this by RecordRadiusScaleScale too (a copy-paste slip, still in UDK); the ini's own key is used here.
        w.Float(ini.Float(Cache, "InterpolationMaxAngle") * Scale("InterpolationMaxAngleScale"));
        w.Float(ini.Float(Cache, "PointBehindRecordMaxAngle"));
        w.Float(ini.Float(Cache, "DistanceSmoothFactor"));
        w.Float(ini.Float(Cache, "AngleSmoothFactor"));
        w.Float(ini.Float(Cache, "RecordBounceRadiusScale"));
        w.Float(ini.Float(Cache, "MinRecordRadius") * Scale("MinRecordRadiusScale"));
        w.Float(ini.Float(Cache, "MaxRecordRadius"));

        // FDebugLightingInputData (0x2D0, 16-aligned): no debug texel; the mapping GUID is one no scene contains, as UE3 initializes it
        w.PadTo(start, 0x2D0);
        long debug = w.Position;
        w.Bool(false); // bRelaySolverStats
        w.Guid(LightmassFormat.FromUe3(0x96dc6516, 0xa616421d, 0x82f0ef5b, 0x299152b5));
        w.Int(-1); // NodeIndex
        w.PadTo(debug, 32);
        w.Vector4(Vector3.Zero, 0); // Position
        w.Int(-1); w.Int(-1); // LocalX/Y
        w.Int(-1); w.Int(-1); // MappingSizeX/Y
        w.Int(-1); w.Int(-1); // LightmapX/Y
        w.Int(-1); // VertexIndex
        w.Color(0, 0, 0, 0); // OriginalColor
        w.Vector4(Vector3.Zero, 0); // CameraPosition
        w.Int(-1); // DebugVisibilityId
        w.PadTo(debug, 112);

        // CustomImportanceBoundingBox (0x340): zero, i.e. not a build of selected actors
        w.PadTo(start, 0x340);
        w.Zero(32);
        // Bitfield (0x360): bPadMappings only (pad requested sizes, as UE3 always does); no SpeedTree
        w.UInt(1u);
        w.Float(0f); // ExecutionTimeDivisor
        w.Int(counts.ImportanceVolumes);
        w.Int(0); // NumCharacterIndirectDetailVolumes
        w.Int(counts.DirectionalLights);
        w.Int(counts.PointLights);
        w.Int(counts.SpotLights);
        w.Int(counts.SkyLights);
        w.Int(counts.StaticMeshes);
        w.Int(counts.Terrains);
        w.Int(counts.StaticMeshInstances);
        w.Int(0); // NumFluidSurfaceInstances
        w.Int(0); // landscape instances
        w.Int(0); // NumSpeedTreeLightingMeshes
        w.Int(counts.BspMappings);
        w.Int(counts.TextureMappings);
        w.Int(counts.VertexMappings);
        w.Int(counts.TerrainMappings);
        w.Int(0); // NumFluidSurfaceTextureMappings
        w.Int(0); // landscape mappings
        w.Int(0); // NumSpeedTreeMappings
        w.Int(0); // NumPrecomputedVisibilityBuckets
        w.PadTo(start, Size);
    }
}
