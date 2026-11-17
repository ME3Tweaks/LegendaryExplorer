using System;
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
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A light environment's properties: DynamicLightEnvironmentComponent's, and BioDynamicLightEnvironmentComponent's additions.
/// Reads class and archetype defaults from the component's game, falling back to stock values. Reading settings doesn't enable a game's renderer.
/// </summary>
public sealed class LightEnvironmentSettings
{
    public MEGame Game { get; init; } = MEGame.LE3;

    /// <summary>
    /// BioDynamicLightEnvironmentComponent, which LE3 builds its lights for differently (FBioDynamicLightEnvironmentState)
    /// </summary>
    public bool IsBio;

    public bool CastShadows = true;
    /// <summary>ULightEnvironmentComponent.bForceNonCompositeDynamicLights</summary>
    public bool ForceNonCompositeDynamicLights;
    public bool CompositeShadowsFromDynamicLights = true;
    public bool ForceCompositeAllLights;
    public bool SynthesizeDirectionalLight = true;
    public bool SynthesizeSHLight;
    public bool ForceAllowLightEnvSphericalHarmonicLights;
    public bool TraceFromClosestBoundsPoint;
    public bool IsCharacterLightEnvironment;
    public bool OverrideOwnerLightingChannels;
    public bool ShadowBouncedLight = true;

    public Vector3 AmbientShadowColor = new(0.001f);
    public Vector3 AmbientGlow = Vector3.Zero;
    public Vector3 MaxModulatedShadowColor = new(0.5f);
    public Vector3 AmbientShadowSourceDirection = new(0.01f, 0, 0.99f);
    public int NumVolumeVisibilitySamples = 1;
    public float LightDesaturation;
    public float LightDistance = 10;
    /// <summary>LE2's separate shadow light distance; other games use LightDistance</summary>
    public float? ShadowLightDistance;
    public float ShadowDistance = 5;
    public int MinShadowResolution;
    public int MaxShadowResolution;
    public int ShadowFadeResolution;
    public float BouncedLightingFactor = 0.3f;
    public float BouncedLightingDesaturation = 0.5f;
    public float MinShadowAngle = 25;
    public LightingChannels OverriddenLightingChannels;
    public LightEnvironmentShadowMode LightShadowMode = LightEnvironmentShadowMode.Modulate;
    /// <summary>LightShadowMode isn't LightShadow_Normal, whose shadow from a zero-brightness light is invisible</summary>
    public bool ModulatedShadows => LightShadowMode is not LightEnvironmentShadowMode.Normal;
    /// <summary>
    /// The level's WorldInfo bAllowModulateBetterShadows (default true; no installed level sets it). Without it, FScene::AddLight renders a
    /// LightShadow_ModulateBetter shadow as LightShadow_Modulate (RVAs: LE1 0x73fefa, LE2 0x32868a, LE3 0x342f30). The game reads the persistent
    /// level's WorldInfo; the preview resolves it from the environment owner's level through MeshRenderContext
    /// </summary>
    public bool AllowModulateBetterShadows = true;

    //LE2 cinematic Bio lights' normal shadows (DynamicLightEnvironmentComponent); the key always has one. LE3's all have one
    public bool AllowFillLightShadows;
    public bool AllowRimLightShadows = true;

    //BioDynamicLightEnvironmentComponent
    public Vector3 KeyLightScale = Vector3.One;
    public Vector3 FillLightScale = Vector3.One;
    public Vector3 AmbientLightScale = Vector3.One;
    public float SHAmbientScale = 0.2f;
    public bool LightAxisEnabled = true;
    public bool IsCinematicQuality;
    /// <summary>Cinematic Bio: the synthesized lights are points (SFXPointLightComponents) rather than directional lights</summary>
    public bool UseOptimizedLightingPath = true;
    /// <summary>Cinematic Bio: how far towards a point the attenuation it's compensated for is measured, as a fraction of the bounds radius</summary>
    public float SurfaceBoundsRatio;
    //Cinematic Bio rim light: RimLightColor (FColor / 255, alpha included) scaled by RimLightScale, turned from the viewer's horizontal direction
    public Vector4 RimLightColor = new(1, 1, 1, 0);
    public float RimLightScale;
    public float RimLightYaw = -33, RimLightPitch = 30;
    /// <summary>
    /// [SystemSettings] MaxCharacterCinematicLightingPasses: cinematic Bio adds a fill and a rim above 1, the brighter of the two at 2.
    /// From the installed config (3 in LE2 and LE3), which the PC graphics options can change in game
    /// </summary>
    public int MaxCinematicLightingPasses = 3;
    /// <summary>LE3 cinematic Bio's CinematicLightRig, whose captured lighting replaces the level's. Null without one</summary>
    public BioLightRig CinematicLightRig;
    /// <summary>LE3 LightRigOrientation: degrees the rig's SH is turned about Z</summary>
    public float LightRigOrientation;
    /// <summary>UseTargetBoneAsOrigin with a TargetBoneName: LE2/LE3 Bio environments take the owner's main skeletal mesh's bounds</summary>
    public bool UseTargetBoneAsOrigin;
    public string TargetBoneName;
    /// <summary>BoundsMethod is DLEB_ManualOverride, which the viewport doesn't model and which disables the target-bone bounds</summary>
    public bool ManualBounds;
    /// <summary>WrapLightingDirectMultiplier / WrapLightingIndirectMultiplier (LE2/LE3 Bio; installed content keeps the default 1)</summary>
    public float WrapLightingDirectMultiplier = 1, WrapLightingIndirectMultiplier = 1;

    //LE1's base quality controls are unrelated to Bio's QualityType.
    public LE1LightEnvironmentQuality LE1Quality = LE1LightEnvironmentQuality.NonFlat;
    public LE1LightEnvironmentQuality LE1KismetQuality = LE1LightEnvironmentQuality.NonFlat;
    public bool IsKismetForcedQuality;
    public Vector3 BoundsOffset;
    public float AmbientScalar = 0.3f;
    public float ModShadowIntensity = 0.1f;
    //LE1 MultiLight and cinematic controls; stock values from Engine.pcc's Default__DynamicLightEnvironmentComponent
    public int NumMultiLights = 2;
    public float MultiLightBorder = 0.001f;
    public float CinematicKeySkyLight;
    public float CinematicFillSkyLight = 0.45f;
    public float CinematicFillCap = 0.9f;
    public float CinematicAmbientScalar = 0.1f;
    /// <summary>LE1 Engine.ini [LightEnvironment] DynamicLightScale; stock value 1.</summary>
    public float DynamicLightScale = 1;
    //LE1 synthesis uses the point light CDO's values, not the environment's resolution overrides.
    public float LE1PointFalloffExponent = 2;
    public float LE1MinShadowFalloffRadius;
    public float LE1ShadowRadiusMultiplier = 1.1f;
    public int LE1PointMinShadowResolution;
    public int LE1PointMaxShadowResolution;
    public int LE1PointShadowFadeResolution;
    /// <summary>LE1's additional sources. Null entries are unresolved references, which make the source list unresolvable</summary>
    public IReadOnlyList<ExportEntry> LE1OverriddenLightComponents = Array.Empty<ExportEntry>();
    public LE1LightEnvironmentQuality EffectiveLE1Quality => IsKismetForcedQuality ? LE1KismetQuality : LE1Quality;

    /// <summary>Stock fallbacks when a component or its defaults can't be resolved</summary>
    public static LightEnvironmentSettings CreateDefaults(MEGame game, bool isBio)
    {
        var settings = new LightEnvironmentSettings
        {
            Game = game,
            IsBio = isBio,
            SynthesizeSHLight = isBio || game is MEGame.LE1 or MEGame.LE2
        };
        if (game is MEGame.LE1)
        {
            settings.AmbientShadowColor = new Vector3(0.15f);
            settings.AmbientShadowSourceDirection = new Vector3(0.25f, 0.18f, 1);
        }
        else if (game is MEGame.LE2)
        {
            settings.LightDistance = 1.5f;
            settings.ShadowLightDistance = 10;
            settings.ShadowDistance = 1;
        }
        return settings;
    }

    /// <summary>
    /// Reads a light environment component's properties, including inherited ones
    /// </summary>
    public static LightEnvironmentSettings Read(ExportEntry export, PackageCache packageCache)
    {
        var settings = CreateDefaults(export.Game, export.IsA("BioDynamicLightEnvironmentComponent"));
        PropertyCollection props = EffectiveLightingProperties.Read(export, packageCache);
        ReadBool(ref settings.CastShadows, "bCastShadows");
        ReadBool(ref settings.ForceNonCompositeDynamicLights, "bForceNonCompositeDynamicLights");
        ReadBool(ref settings.CompositeShadowsFromDynamicLights, "bCompositeShadowsFromDynamicLights");
        ReadBool(ref settings.ForceCompositeAllLights, "bForceCompositeAllLights");
        ReadBool(ref settings.SynthesizeDirectionalLight, "bSynthesizeDirectionalLight");
        ReadBool(ref settings.SynthesizeSHLight, "bSynthesizeSHLight");
        ReadBool(ref settings.ForceAllowLightEnvSphericalHarmonicLights, "bForceAllowLightEnvSphericalHarmonicLights");
        ReadBool(ref settings.TraceFromClosestBoundsPoint, "bTraceFromClosestBoundsPoint");
        ReadBool(ref settings.IsCharacterLightEnvironment, "bIsCharacterLightEnvironment");
        ReadBool(ref settings.OverrideOwnerLightingChannels, "bOverrideOwnerLightingChannels");
        ReadBool(ref settings.ShadowBouncedLight, "bShadowBouncedLight");
        ReadBool(ref settings.AllowFillLightShadows, "bAllowFillLightShadows");
        ReadBool(ref settings.AllowRimLightShadows, "bAllowRimLightShadows");
        ReadColor(ref settings.AmbientShadowColor, "AmbientShadowColor");
        ReadColor(ref settings.AmbientGlow, "AmbientGlow");
        ReadColor(ref settings.MaxModulatedShadowColor, "MaxModulatedShadowColor");
        if (props.GetProp<StructProperty>("AmbientShadowSourceDirection") is { } directionProp)
        {
            settings.AmbientShadowSourceDirection = CommonStructs.GetVector3(directionProp);
        }
        settings.NumVolumeVisibilitySamples = props.GetProp<IntProperty>("NumVolumeVisibilitySamples")?.Value ?? settings.NumVolumeVisibilitySamples;
        ReadFloat(ref settings.LightDesaturation, "LightDesaturation");
        ReadFloat(ref settings.LightDistance, "LightDistance");
        if (export.Game is MEGame.LE2)
        {
            settings.ShadowLightDistance = props.GetProp<FloatProperty>("ShadowLightDistance")?.Value ?? settings.ShadowLightDistance;
        }
        ReadFloat(ref settings.ShadowDistance, "ShadowDistance");
        settings.MinShadowResolution = props.GetProp<IntProperty>("MinShadowResolution")?.Value ?? 0;
        settings.MaxShadowResolution = props.GetProp<IntProperty>("MaxShadowResolution")?.Value ?? 0;
        settings.ShadowFadeResolution = props.GetProp<IntProperty>("ShadowFadeResolution")?.Value ?? 0;
        ReadFloat(ref settings.BouncedLightingFactor, "BouncedLightingFactor");
        ReadFloat(ref settings.BouncedLightingDesaturation, "BouncedLightingDesaturation");
        ReadFloat(ref settings.MinShadowAngle, "MinShadowAngle");
        settings.OverriddenLightingChannels = LightingChannels.FromProperty(props.GetProp<StructProperty>("OverriddenLightingChannels"), default, isInitialized: true, default);
        //LightShadow_Modulate is the environment default
        settings.ManualBounds = props.GetProp<EnumProperty>("BoundsMethod") is { Value.Instanced: "DLEB_ManualOverride" };
        settings.LightShadowMode = props.GetProp<EnumProperty>("LightShadowMode")?.Value.Instanced switch
        {
            "LightShadow_Normal" => LightEnvironmentShadowMode.Normal,
            "LightShadow_ModulateBetter" => LightEnvironmentShadowMode.ModulateBetter,
            _ => LightEnvironmentShadowMode.Modulate
        };
        if (export.Game is MEGame.LE1)
        {
            settings.LE1Quality = ReadLE1Quality("eQuality");
            settings.LE1KismetQuality = ReadLE1Quality("eKismetOverrideQuality");
            ReadBool(ref settings.IsKismetForcedQuality, "bIsKismetForcedQuality");
            ReadVector(ref settings.BoundsOffset, "vBoundsOffset");
            ReadFloat(ref settings.AmbientScalar, "fAmbientScalar");
            ReadFloat(ref settings.ModShadowIntensity, "fModShadowIntensity");
            settings.NumMultiLights = props.GetProp<IntProperty>("nNumMultiLights")?.Value ?? settings.NumMultiLights;
            ReadFloat(ref settings.MultiLightBorder, "fMultiLightBorder");
            ReadFloat(ref settings.CinematicKeySkyLight, "fCinematicKeySkyLight");
            ReadFloat(ref settings.CinematicFillSkyLight, "fCinematicFillSkyLight");
            ReadFloat(ref settings.CinematicFillCap, "fCinematicFillCap");
            ReadFloat(ref settings.CinematicAmbientScalar, "fCinematicAmbientScalar");
            var (sources, sourcePackage) = EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(export, "OverriddenLightComponents", packageCache);
            if (sources is not null)
            {
                var resolved = new List<ExportEntry>(sources.Count);
                foreach (ObjectProperty source in sources)
                    resolved.Add(EffectiveLightingProperties.Resolve(source.ResolveToEntry(sourcePackage), packageCache));
                settings.LE1OverriddenLightComponents = resolved;
            }
        }
        if (settings.IsBio)
        {
            ReadVector(ref settings.KeyLightScale, "KeyLightScale");
            ReadVector(ref settings.FillLightScale, "FillLightScale");
            ReadVector(ref settings.AmbientLightScale, "AmbientLightScale");
            ReadFloat(ref settings.SHAmbientScale, "SHAmbientScale");
            ReadBool(ref settings.LightAxisEnabled, "LightAxisEnabled");
            settings.IsCinematicQuality = props.GetProp<EnumProperty>("QualityType") is { Value.Instanced: "DLEST_Cinematic" };
            ReadBool(ref settings.UseOptimizedLightingPath, "UseOptimizedLightingPath");
            ReadFloat(ref settings.SurfaceBoundsRatio, "SurfaceBoundsRatio");
            ReadFloat(ref settings.RimLightScale, "RimLightScale");
            ReadFloat(ref settings.RimLightYaw, "RimLightYaw");
            ReadFloat(ref settings.RimLightPitch, "RimLightPitch");
            if (props.GetProp<StructProperty>("RimLightColor") is { } rimColor)
            {
                System.Drawing.Color color = CommonStructs.GetColor(rimColor);
                settings.RimLightColor = new Vector4(color.R, color.G, color.B, color.A) / 255;
            }
            ReadBool(ref settings.UseTargetBoneAsOrigin, "UseTargetBoneAsOrigin");
            settings.TargetBoneName = props.GetProp<NameProperty>("TargetBoneName")?.Value.Instanced;
            ReadFloat(ref settings.WrapLightingDirectMultiplier, "WrapLightingDirectMultiplier");
            ReadFloat(ref settings.WrapLightingIndirectMultiplier, "WrapLightingIndirectMultiplier");
            if (export.Game is MEGame.LE3)
            {
                ReadFloat(ref settings.LightRigOrientation, "LightRigOrientation");
                if (EffectiveLightingProperties.ResolveReference(export, "CinematicLightRig", packageCache) is { } rig)
                {
                    settings.CinematicLightRig = BioLightRig.Read(rig, packageCache);
                }
            }
        }
        return settings;

        LE1LightEnvironmentQuality ReadLE1Quality(string name) => props.GetProp<EnumProperty>(name)?.Value.Instanced switch
        {
            null or "ELEQ_NonFlat" => LE1LightEnvironmentQuality.NonFlat,
            "ELEQ_Default" => LE1LightEnvironmentQuality.Default,
            "ELEQ_MultiLight" => LE1LightEnvironmentQuality.MultiLight,
            "ELEQ_Cinematic" => LE1LightEnvironmentQuality.Cinematic,
            "ELEQ_CinematicPlusMod" => LE1LightEnvironmentQuality.CinematicPlusMod,
            _ => LE1LightEnvironmentQuality.Unknown
        };

        void ReadBool(ref bool value, string name) => value = props.GetProp<BoolProperty>(name)?.Value ?? value;
        void ReadFloat(ref float value, string name) => value = props.GetProp<FloatProperty>(name)?.Value ?? value;
        void ReadVector(ref Vector3 value, string name)
        {
            if (props.GetProp<StructProperty>(name) is { } prop)
            {
                value = CommonStructs.GetVector3(prop);
            }
        }
        void ReadColor(ref Vector3 value, string name)
        {
            if (props.GetProp<StructProperty>(name) is { } prop)
            {
                LinearColor color = CommonStructs.GetLinearColor(prop);
                value = new Vector3(color.R, color.G, color.B);
            }
        }
    }

    /// <summary>
    /// Installed config and CDO values that environments use beyond their own properties: LE1's [LightEnvironment] DynamicLightScale and
    /// point light CDO, and LE2's and LE3's [SystemSettings] MaxCharacterCinematicLightingPasses. Read when environments are created; missing or
    /// invalid inputs keep stock values
    /// </summary>
    internal sealed record InstalledDefaults
    {
        internal float DynamicLightScale { get; init; } = 1;
        internal int MaxCinematicLightingPasses { get; init; } = 3;
        internal float FalloffExponent { get; init; } = 2;
        internal float MinShadowFalloffRadius { get; init; }
        internal float ShadowRadiusMultiplier { get; init; } = 1.1f;
        internal int MinShadowResolution { get; init; }
        internal int MaxShadowResolution { get; init; }
        internal int ShadowFadeResolution { get; init; }
        internal string ConfigPath { get; init; }
        internal string PointDefaultsPath { get; init; }

        internal static InstalledDefaults Load(MEGame game, PackageCache cache)
        {
            var result = new InstalledDefaults();
            if (game is MEGame.LE2 or MEGame.LE3)
            {
                try
                {
                    string configPath = ConfigTools.GetCoalescedPath(game);
                    using var stream = File.OpenRead(configPath);
                    result = result with { MaxCinematicLightingPasses = ReadMaxCinematicLightingPasses(stream, game), ConfigPath = configPath };
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"{game} environment config uses stock fallback: {e.Message}");
                }
                return result;
            }
            if (game is not MEGame.LE1)
            {
                return result;
            }
            try
            {
                //The installed binary as it stands, without mod-manager merge deltas
                string configPath = ConfigTools.GetCoalescedPath(game);
                using var stream = File.OpenRead(configPath);
                result = result with { DynamicLightScale = ReadDynamicLightScale(stream), ConfigPath = configPath };
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"{game} environment config uses stock fallback: {e.Message}");
            }
            try
            {
                if (MELoadedFiles.GetFilesLoadedInGame(game).TryGetValue("Engine.pcc", out string path)
                    && cache.GetCachedPackage(path)?.FindExport("Default__PointLightComponent") is { } pointDefault)
                    result = result.ReadPointDefaults(pointDefault, cache);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"{game} environment point defaults use stock fallback: {e.Message}");
            }
            return result;
        }

        internal static float ReadDynamicLightScale(Stream stream) =>
            ReadEngineValue(stream, "LightEnvironment", "DynamicLightScale") is { } text
            && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && float.IsFinite(value) && value > 0 ? value : 1;

        internal static int ReadMaxCinematicLightingPasses(Stream stream, MEGame game) =>
            ReadEngineValue(stream, "SystemSettings", "MaxCharacterCinematicLightingPasses", game) is { } text
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 0 ? value : 3;

        //BioEngine.ini's only value for the key in the section, or null
        private static string ReadEngineValue(Stream stream, string section, string key, MEGame game = MEGame.LE1)
        {
            if (game is MEGame.LE3)
            {
                //(LE3's BioCompat compatibility-level overrides aren't applied)
                var assets = CoalescedConverter.DecompileGame3ToAssets(stream, "Coalesced.bin", stripExtensions: true);
                if (!assets.TryGetValue("BioEngine", out CoalesceAsset engine)) return null;
                var game3Values = engine.Sections
                    .Where(s => string.Equals(s.Key, section, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(s => s.Value)
                    .Where(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(p => p.Value)
                    .Where(v => !v.IsNull).ToArray();
                return game3Values.Length == 1 ? game3Values[0].Value : null;
            }
            var files = CoalescedConverter.DecompileLE1LE2ToMemory(stream, "Coalesced_INT.bin");
            var engineFiles = files.Where(file => Path.GetFileName(file.Key).Equals("BioEngine.ini", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (engineFiles.Length != 1) return null;
            var values = engineFiles[0].Value.Sections
                .Where(s => string.Equals(s.Header, section, StringComparison.OrdinalIgnoreCase))
                .SelectMany(s => s.Entries)
                .Where(entry => entry.HasValue && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)).ToArray();
            //Duplicate scalar precedence hasn't been established for the FConfig cache.
            return values.Length == 1 ? values[0].Value : null;
        }

        internal InstalledDefaults ReadPointDefaults(ExportEntry pointDefault, PackageCache cache)
        {
            if (pointDefault.Game is not MEGame.LE1 || pointDefault.ClassName != "PointLightComponent" || !pointDefault.IsDefaultObject)
                throw new ArgumentException("Expected LE1's PointLightComponent class default object.", nameof(pointDefault));
            PropertyCollection props = EffectiveLightingProperties.Read(pointDefault, cache);
            return this with
            {
                PointDefaultsPath = pointDefault.FileRef.FilePath,
                FalloffExponent = Nonnegative("FalloffExponent", 2),
                MinShadowFalloffRadius = Nonnegative("MinShadowFalloffRadius", 0),
                ShadowRadiusMultiplier = props.GetProp<FloatProperty>("ShadowRadiusMultiplier") is { Value: > 0 } radius && float.IsFinite(radius.Value) ? radius.Value : 1.1f,
                MinShadowResolution = Resolution("MinShadowResolution"),
                MaxShadowResolution = Resolution("MaxShadowResolution"),
                ShadowFadeResolution = Resolution("ShadowFadeResolution")
            };

            float Nonnegative(string name, float fallback) => props.GetProp<FloatProperty>(name) is { Value: >= 0 } prop && float.IsFinite(prop.Value) ? prop.Value : fallback;
            int Resolution(string name) => Math.Max(0, props.GetProp<IntProperty>(name)?.Value ?? 0);
        }

        internal void ApplyTo(LightEnvironmentSettings settings)
        {
            settings.MaxCinematicLightingPasses = MaxCinematicLightingPasses;
            if (settings.Game is not MEGame.LE1) return;
            settings.DynamicLightScale = DynamicLightScale;
            settings.LE1PointFalloffExponent = FalloffExponent;
            settings.LE1MinShadowFalloffRadius = MinShadowFalloffRadius;
            settings.LE1ShadowRadiusMultiplier = ShadowRadiusMultiplier;
            settings.LE1PointMinShadowResolution = MinShadowResolution;
            settings.LE1PointMaxShadowResolution = MaxShadowResolution;
            settings.LE1PointShadowFadeResolution = ShadowFadeResolution;
        }
    }
}

public enum LightEnvironmentShadowMode
{
    Normal,
    Modulate,
    ModulateBetter
}

/// <summary>LE1's ELightEnvironmentQuality (eQuality, eKismetOverrideQuality), which selects its synthesis</summary>
public enum LE1LightEnvironmentQuality
{
    Unknown = -1,
    Default,
    NonFlat,
    MultiLight,
    Cinematic,
    CinematicPlusMod
}
