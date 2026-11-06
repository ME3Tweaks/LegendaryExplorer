using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
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

/// <summary>
/// A primitive lit by a light environment. The light environment's bounds are those of its primitives, and its shadow is cast by all of them
/// </summary>
public sealed class LightEnvironmentPrimitive
{
    public Func<BoxSphereBounds> GetBounds;
    public Func<Matrix4x4> GetLocalToWorld;
    /// <summary>
    /// Draws the primitive's shadow-casting sections into a shadow's depth (see <see cref="LightAttenuationRenderer.DrawShadowSection{TVertex}"/>). Null if it doesn't cast shadows
    /// </summary>
    public Action<DeviceContext, ShadowDepthPass> DrawShadowCaster;
    /// <summary>
    /// Draws the primitive with its materials' FModShadowMesh shaders, to mask a LightShadow_ModulateBetter shadow off it (see
    /// <see cref="LightAttenuationRenderer.DrawModShadowReceiverSection"/>)
    /// </summary>
    public Action<DeviceContext, ModShadowReceiverPass> DrawModShadowReceiver;
    /// <summary>Where the primitive masks LightShadow_ModulateBetter shadows off itself</summary>
    public ModulatedShadowCulling ModShadowCulling;
    /// <summary>
    /// bCastShadowAsTwoSided: its one-sided sections aren't culled in the shadow's depth
    /// </summary>
    public bool CastShadowAsTwoSided;
    /// <summary>
    /// Whether the primitive is in the game's scene (its actor and component aren't hidden in game)
    /// </summary>
    public Func<bool> IsInScene = () => true;
    /// <summary>
    /// Whether the primitive is shown in the editor, so that it casts its shadow (its actor's category isn't hidden)
    /// </summary>
    public Func<bool> IsShown = () => true;
    public LightingChannels LightingChannels;
    /// <summary>bAcceptsLights and bAcceptsDynamicLights, for <see cref="LightEnvironmentLighting.DirectLights"/></summary>
    public bool AcceptsLights = true;
    public bool AcceptsDynamicLights = true;
    /// <summary>Precomputed-shadow participation and grouping, for directly drawn lights' shadows</summary>
    public bool HasStaticShadowing;
    public bool HasShadowParent;
    public bool SelfShadowOnly;
    /// <summary>The primitive's component, to identify the owner's main mesh</summary>
    public ExportEntry Component;
    /// <summary>For skeletal primitives: whether the mesh has a named bone (USkeletalMeshComponent::MatchRefBone)</summary>
    public Func<string, bool> HasBone;
}

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
/// Cinematic Bio's rim light (RVAs LE2 0x18d7c0, LE3 0x1a3c50): the horizontal direction from the viewer to the environment, turned by RimLightPitch (negated)
/// about its horizontal perpendicular and then by RimLightYaw about Z, both as UE3's table-based RotateAngleAxis does. The game takes the viewer
/// from the player's camera or a camera actor; the preview takes the viewport camera. Created as the other Bio lights are
/// </summary>
public sealed class BioRimLight
{
    private readonly DynamicLightEnvironment Environment;
    private readonly BoxSphereBounds Bounds;
    private readonly Vector4 Color;
    private readonly bool CastsShadow;
    private readonly Lock CacheLock = new();
    private Vector3 CachedViewLocation;
    private SceneLight CachedLight;
    private bool HasCachedLight;

    internal BioRimLight(DynamicLightEnvironment environment, BoxSphereBounds bounds, Vector4 color, bool castsShadow)
    {
        Environment = environment;
        Bounds = bounds;
        Color = color;
        CastsShadow = castsShadow;
    }

    /// <summary>The light for a view location. Null if the viewer is (nearly) straight above or below the environment</summary>
    public SceneLight GetLight(Vector3 viewLocation)
    {
        lock (CacheLock)
        {
            if (!HasCachedLight || viewLocation != CachedViewLocation)
            {
                CachedLight = GetDirection(Bounds.Origin, viewLocation, Environment.Settings.RimLightYaw, Environment.Settings.RimLightPitch) is { } direction
                    ? Environment.CreateBioLight(direction, new Vector3(Color.X, Color.Y, Color.Z), Color.W, CastsShadow, Bounds)
                    : null;
                CachedViewLocation = viewLocation;
                HasCachedLight = true;
            }
            return CachedLight;
        }
    }

    /// <summary>The direction towards the rim light</summary>
    internal static Vector3? GetDirection(Vector3 origin, Vector3 viewLocation, float yaw, float pitch)
    {
        float x = origin.X - viewLocation.X, y = origin.Y - viewLocation.Y;
        float lengthSquared = x * x + y * y;
        if (lengthSquared != 1)
        {
            float scale = lengthSquared >= 1e-8f ? 1 / MathF.Sqrt(lengthSquared) : 0;
            x *= scale;
            y *= scale;
        }
        if (!(MathF.Max(MathF.Abs(x), MathF.Abs(y)) > 1e-4f))
        {
            return null;
        }
        //degrees to Unreal rotation units
        const double toUnreal = 65536.0 / 360;
        Vector3 direction = RotateAngleAxis(new Vector3(x, y, 0), (int)(pitch * -toUnreal), new Vector3(-y, x, 0));
        return RotateAngleAxis(direction, (int)(yaw * toUnreal), Vector3.UnitZ);
    }

    //FGlobalMath's 16384-entry sine table
    private static readonly float[] SinTable = Enumerable.Range(0, 16384).Select(i => (float)Math.Sin(i * 2 * Math.PI / 16384)).ToArray();

    /// <summary>FVector::RotateAngleAxis</summary>
    internal static Vector3 RotateAngleAxis(Vector3 v, int angle, Vector3 axis)
    {
        float s = SinTable[(angle >> 2) & 0x3fff];
        float c = SinTable[((angle + 16384) >> 2) & 0x3fff];
        float xx = axis.X * axis.X, yy = axis.Y * axis.Y, zz = axis.Z * axis.Z;
        float xy = axis.X * axis.Y, yz = axis.Y * axis.Z, zx = axis.Z * axis.X;
        float xs = axis.X * s, ys = axis.Y * s, zs = axis.Z * s;
        float omc = 1 - c;
        return new Vector3(
            (omc * xx + c) * v.X + (omc * xy - zs) * v.Y + (omc * zx + ys) * v.Z,
            (omc * xy + zs) * v.X + (omc * yy + c) * v.Y + (omc * yz - xs) * v.Z,
            (omc * zx - ys) * v.X + (omc * yz + xs) * v.Y + (omc * zz + c) * v.Z);
    }
}

/// <summary>
/// LE3's SFXLightRig, named by a cinematic Bio environment's CinematicLightRig: lighting captured for the character, whose SH and shadow replace
/// the level's lights (RVA 0x1a4840), and scales for the key, fill and ambient
/// </summary>
public sealed record BioLightRig(SHVectorRGB Shadowed, SHVectorRGB Unshadowed, Vector3 ShadowDirection, Vector3 TotalShadowIntensity,
    Vector3 KeyLightModifier, Vector3 FillLightModifier, Vector3 AmbientLightModifier)
{
    internal static BioLightRig Read(ExportEntry rig, PackageCache cache)
    {
        PropertyCollection props = EffectiveLightingProperties.Read(rig, cache);
        return new BioLightRig(ReadSH("LightEnvironment"), ReadSH("NonShadowedLightEnvironment"),
            props.GetProp<StructProperty>("ShadowDirection") is { } direction ? CommonStructs.GetVector3(direction) : Vector3.Zero,
            ReadColor("TotalShadowIntensity", Vector3.Zero), ReadColor("KeyLightModifier", Vector3.One), ReadColor("FillLightModifier", Vector3.One),
            ReadColor("AmbientLightModifier", Vector3.One));

        Vector3 ReadColor(string name, Vector3 fallback)
        {
            if (props.GetProp<StructProperty>(name) is not { } prop) return fallback;
            LinearColor color = CommonStructs.GetLinearColor(prop);
            return new Vector3(color.R, color.G, color.B);
        }

        SHVectorRGB ReadSH(string name)
        {
            var sh = new SHVectorRGB();
            if (props.GetProp<StructProperty>(name) is { } prop)
            {
                sh.R = ReadChannel(prop, "R");
                sh.G = ReadChannel(prop, "G");
                sh.B = ReadChannel(prop, "B");
            }
            return sh;
        }

        static SHVector ReadChannel(StructProperty rgb, string name)
        {
            var channel = new SHVector();
            if (rgb.Properties.GetProp<StructProperty>(name) is { } prop)
            {
                foreach (FloatProperty v in prop.Properties.OfType<FloatProperty>())
                {
                    if (v.Name == "V" && v.StaticArrayIndex < SHVector.NumCoefficients) channel[v.StaticArrayIndex] = v.Value;
                }
            }
            return channel;
        }
    }

    /// <summary>The captured SH turned by LightRigOrientation about Z</summary>
    internal static SHVectorRGB Orient(SHVectorRGB sh, float degrees)
    {
        float angle = (float)(degrees * (Math.PI / 180));
        return new SHVectorRGB { R = RotateZ(sh.R, angle), G = RotateZ(sh.G, angle), B = RotateZ(sh.B, angle) };
    }

    /// <summary>
    /// LE3's SH rotation about Z (RVA 0xb6050), as it runs: coefficients 1 and 5 take 2 and 6 where a rotation takes 3 and 7
    /// This seems like a bug, but we should match the game's behavior
    /// </summary>
    internal static SHVector RotateZ(SHVector sh, float angle)
    {
        (float s, float c) = MathF.SinCos(angle);
        (float s2, float c2) = MathF.SinCos(2 * angle);
        var rotated = new SHVector();
        rotated[0] = sh[0];
        rotated[1] = sh[1] * c - sh[2] * s;
        rotated[2] = sh[2];
        rotated[3] = sh[3] * c + sh[1] * s;
        rotated[4] = sh[4] * c2 - sh[8] * s2;
        rotated[5] = sh[5] * c - sh[6] * s;
        rotated[6] = sh[6];
        rotated[7] = sh[7] * c + sh[5] * s;
        rotated[8] = sh[8] * c2 + sh[4] * s2;
        return rotated;
    }
}

/// <summary>
/// The light a light environment creates to cast its primitives' shadow: a point light of no brightness, whose shadow modulates the scene by <see cref="ModShadowColor"/>
/// </summary>
/// <param name="CullsOnSubject">LE1 bCullModulatedShadowOnSubject (CinematicPlusMod), or LE2/LE3 cinematic Bio: the shadow doesn't darken its own primitives</param>
/// <param name="ModulateBetter">LightShadow_ModulateBetter: receivers mask the shadow off where they face away from the light or are emissive</param>
public sealed record LightEnvironmentShadowLight(Vector3 Position, float Radius, float MinShadowFalloffRadius, float ShadowFalloffExponent, Vector3 ModShadowColor,
    int MinShadowResolution, int MaxShadowResolution, int ShadowFadeResolution, float ShadowRadiusMultiplier = 1.1f, bool CullsOnSubject = false, bool ModulateBetter = false);

/// <summary>
/// The per-primitive test for <see cref="LightEnvironmentLighting.DirectLights"/>: ULightComponent::AffectsPrimitive after its environment test
/// </summary>
internal static class LightEnvironmentDirectLighting
{
    //FLightPrimitiveInteraction (RVAs LE1 0x739390, LE2 0x3228d0, LE3 0x33ca00): non-static primitives take only the composite shadow
    //from composite-shadow lights; dominant and non-composite lights keep their own shadow, and static receivers the ordinary test.
    public static bool CastsSeparateShadow(SceneLight light, LightEnvironmentPrimitive primitive) =>
        primitive.DrawShadowCaster is not null && !primitive.HasShadowParent && !primitive.SelfShadowOnly && light.CastsDynamicShadows
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

/// <summary>
/// UE3's light environment (DynamicLightEnvironmentComponent and its FDynamicLightEnvironmentState), as LE3 computes it once it has settled:
/// the level's lights that reach its primitives are composited into spherical harmonic lighting, from which it synthesizes a directional light,
/// an SH (or sky) light for the remainder, and a point light that casts the primitives' shadow.
/// LE3's BioDynamicLightEnvironmentComponent (FBioDynamicLightEnvironmentState) finds the directional light differently, and scales the lights.
/// LE1 synthesizes point lights (ComposeLE1). LE2 and LE3 draw non-composited bypass lights directly and use the owner's main mesh's
/// bounds for UseTargetBoneAsOrigin. Light probes aren't supported.
/// </summary>
public sealed class DynamicLightEnvironment
{
    private readonly MeshRenderContext Context;
    public readonly LightEnvironmentSettings Settings;
    /// <summary>
    /// The light environment component. Null for a character with its class's default light environment
    /// </summary>
    public readonly ExportEntry Export;
    /// <summary>
    /// The actor that owns the light environment
    /// </summary>
    public readonly ExportEntry Owner;

    /// <summary>The level (package) the owner supplies to level filters</summary>
    internal IMEPackage LevelPackage => (Owner ?? Export)?.FileRef;

    /// <summary>
    /// WorldInfo's CharacterLightingContrastFactor and bAllowLightEnvSphericalHarmonicLights, from the level the light environment is in
    /// </summary>
    private readonly float CharacterLightingContrastFactor;
    private readonly bool AllowLightEnvSphericalHarmonicLights;

    //guarded by CacheLock, since primitives are added on the loading thread while the render thread reads them
    private readonly List<LightEnvironmentPrimitive> Primitives = [];
    private readonly Lock CacheLock = new();
    //null if computing it failed
    private LightEnvironmentLighting CachedLighting;
    private bool HasCachedLighting;
    private int CachedLightsVersion = -1;
    private int CachedGeometryVersion = -1;
    private BoxSphereBounds CachedBounds;

    //SystemSettings are taken to be the defaults: bUseCompositeDynamicLights off, bSHSecondaryLighting and bLightEnvironmentShadows on
    //PointLightComponent's default
    private const float ShadowFalloffExponent = 2;

    internal DynamicLightEnvironment(MeshRenderContext context, ExportEntry owner, ExportEntry export, LightEnvironmentSettings settings, float characterLightingContrastFactor,
        bool allowLightEnvSphericalHarmonicLights)
    {
        Context = context;
        Owner = owner;
        Export = export;
        Settings = settings;
        CharacterLightingContrastFactor = characterLightingContrastFactor;
        AllowLightEnvSphericalHarmonicLights = allowLightEnvSphericalHarmonicLights;
    }

    /// <summary>
    /// Use <see cref="MeshRenderContext.AddLightEnvironmentPrimitive"/>, which keeps track of the light environments that have primitives
    /// </summary>
    internal void AddPrimitive(LightEnvironmentPrimitive primitive)
    {
        lock (CacheLock)
        {
            Primitives.Add(primitive);
            CachedLightsVersion = -1;
        }
    }

    /// <summary>
    /// Use <see cref="MeshRenderContext.RemoveLightEnvironmentPrimitive"/>
    /// </summary>
    /// <returns>How many primitives are left</returns>
    internal int RemovePrimitive(LightEnvironmentPrimitive primitive)
    {
        lock (CacheLock)
        {
            Primitives.Remove(primitive);
            CachedLightsVersion = -1;
            return Primitives.Count;
        }
    }

    internal LightEnvironmentPrimitive[] GetPrimitives()
    {
        lock (CacheLock)
        {
            return Primitives.ToArray();
        }
    }

    /// <summary>
    /// The light environment's lights. Cached until the scene's lights, the level geometry, or its primitives' bounds change
    /// </summary>
    /// <returns>Null if they couldn't be computed</returns>
    public LightEnvironmentLighting GetLighting()
    {
        lock (CacheLock)
        {
            (BoxSphereBounds bounds, LightingChannels channels) = GetOwnerBoundsAndChannels();
            SceneLight[] lights = Context.GetLights(out int lightsVersion);
            LevelGeometry geometry = Context.GetLevelGeometry(out int geometryVersion);
            if (HasCachedLighting && lightsVersion == CachedLightsVersion && geometryVersion == CachedGeometryVersion
                && bounds.Origin == CachedBounds.Origin && bounds.SphereRadius == CachedBounds.SphereRadius)
            {
                return CachedLighting;
            }
            //a failure is cached too, so it isn't retried every frame
            try
            {
                CachedLighting = Compute(lights, bounds, channels, geometry);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Could not compute the light environment of {(Owner ?? Export)?.InstancedFullPath}: {e.Message}");
                CachedLighting = null;
            }
            HasCachedLighting = true;
            CachedLightsVersion = lightsVersion;
            CachedGeometryVersion = geometryVersion;
            CachedBounds = bounds;
            return CachedLighting;
        }
    }

    /// <summary>
    /// The union of the bounds of the light environment's primitives that are in the scene (DLEB_ActiveComponents), and their lighting channels.
    /// If none are (they're hidden in game, and only drawn because the editor shows hidden actors), all of them are used, so they're lit where they are
    /// </summary>
    public (BoxSphereBounds bounds, LightingChannels channels) GetOwnerBoundsAndChannels()
    {
        BoxSphereBounds? bounds = null;
        uint channels = 0;
        lock (CacheLock)
        {
            //LE1 walks every primitive using this environment, without the later games' active-only filter
            AddPrimitives(onlyInScene: Settings.Game is not MEGame.LE1);
            if (bounds is null)
            {
                AddPrimitives(onlyInScene: false);
            }
            if (bounds is not null && GetTargetBoneMeshBounds() is { } meshBounds)
            {
                bounds = meshBounds;
            }
        }
        if (Settings.Game is not MEGame.LE1 && Settings.OverrideOwnerLightingChannels)
        {
            channels = Settings.OverriddenLightingChannels.Channels;
        }
        //LE1 starts from zero bounds and no channels; later games from a unit sphere
        if (bounds is null && Settings.Game is MEGame.LE1)
            return (default, default);
        //with no primitives, UpdateOwner leaves a unit sphere at the origin that every light channel reaches
        return (bounds ?? new BoxSphereBounds { Origin = Vector3.Zero, BoxExtent = Vector3.One, SphereRadius = 1 },
            bounds is null ? new LightingChannels(uint.MaxValue) : new LightingChannels(channels));

        void AddPrimitives(bool onlyInScene)
        {
            foreach (LightEnvironmentPrimitive primitive in Primitives)
            {
                if (onlyInScene && !primitive.IsInScene())
                {
                    continue;
                }
                BoxSphereBounds primitiveBounds = primitive.GetBounds();
                bounds = bounds is { } b ? Union(b, primitiveBounds) : primitiveBounds;
                channels |= primitive.LightingChannels.Channels;
            }
        }
    }

    //the owner's main skeletal mesh component, once resolved
    private (bool, ExportEntry) OwnerMainMesh;

    /// <summary>
    /// Bio UpdateOwner (RVAs LE2 0x192fd0, LE3 0x1a8eb0): with UseTargetBoneAsOrigin, a TargetBoneName and no manual bounds, the bounds of
    /// the owner's main skeletal mesh, if it has the bone. Call under CacheLock
    /// </summary>
    private BoxSphereBounds? GetTargetBoneMeshBounds()
    {
        if (Settings is not { Game: MEGame.LE2 or MEGame.LE3, IsBio: true, UseTargetBoneAsOrigin: true, ManualBounds: false }
            || string.IsNullOrEmpty(Settings.TargetBoneName) || Settings.TargetBoneName == "None" || Owner is null)
        {
            return null;
        }
        if (!OwnerMainMesh.Item1)
        {
            //LE3 asks the owner (AActor vfunc 0x358, overridden only by these three classes); LE2 tests Pawn and SkeletalMeshActor directly
            string meshProperty = GlobalUnrealObjectInfo.IsA(Owner.ClassName, "Pawn", Owner.Game) ? "Mesh"
                : GlobalUnrealObjectInfo.IsA(Owner.ClassName, "SkeletalMeshActor", Owner.Game) ? "SkeletalMeshComponent"
                : Owner.Game is MEGame.LE3 && GlobalUnrealObjectInfo.IsA(Owner.ClassName, "SFXStuntActor", Owner.Game) ? "BodyMesh" : null;
            ExportEntry mesh = null;
            if (meshProperty is not null)
            {
                try
                {
                    mesh = Owner.GetProperty<ObjectProperty>(meshProperty)?.ResolveToEntry(Owner.FileRef) as ExportEntry;
                    if (mesh is null && Context is not null)
                        mesh = EffectiveLightingProperties.ResolveReference(Owner, meshProperty, Context.PackageCache);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not resolve {Owner.InstancedFullPath}'s {meshProperty}: {e.Message}");
                }
            }
            OwnerMainMesh = (true, mesh);
        }
        if (OwnerMainMesh.Item2 is not { } main)
        {
            return null;
        }
        foreach (LightEnvironmentPrimitive primitive in Primitives)
        {
            //an inherited component template stands for the owner's own instance of it
            bool isMain = primitive.Component == main || primitive.Component is { } c && c.Parent == Owner && c.ObjectName == main.ObjectName;
            if (isMain && primitive.HasBone?.Invoke(Settings.TargetBoneName) == true)
            {
                return primitive.GetBounds();
            }
        }
        return null;
    }

    //FBoxSphereBounds' union
    private static BoxSphereBounds Union(BoxSphereBounds a, BoxSphereBounds b)
    {
        Vector3 min = Vector3.Min(a.Origin - a.BoxExtent, b.Origin - b.BoxExtent);
        Vector3 max = Vector3.Max(a.Origin + a.BoxExtent, b.Origin + b.BoxExtent);
        Vector3 origin = (min + max) / 2;
        Vector3 extent = (max - min) / 2;
        float radius = MathF.Min(extent.Length(), MathF.Max((a.Origin - origin).Length() + a.SphereRadius, (b.Origin - origin).Length() + b.SphereRadius));
        return new BoxSphereBounds { Origin = origin, BoxExtent = extent, SphereRadius = radius };
    }

    /// <summary>
    /// The light a set of lights contributes to the light environment: the shadow-casting lights' SH, the other lights' SH,
    /// and the SH the shadow's direction and darkness are computed from
    /// </summary>
    private struct Environment
    {
        public SHVectorRGB Shadowed;
        public SHVectorRGB Unshadowed;
        public SHVectorRGB ShadowEnvironment;
        public bool AnyLights;
    }

    /// <summary>
    /// The direction of the light that casts the shadow, its intensity, and the total intensity (including the light that remains in the shadow)
    /// </summary>
    private record struct ShadowInfo(Vector3 Direction, Vector3 ShadowedIntensity, Vector3 UnshadowedIntensity);

    private LightEnvironmentLighting Compute(SceneLight[] lights, BoxSphereBounds bounds, LightingChannels channels, LevelGeometry geometry)
    {
        if (Settings.Game is MEGame.LE1)
        {
            return ComputeLE1(lights, bounds, channels, geometry);
        }
        var visibilityTester = new LightVisibilityTester(geometry, Owner, Settings, bounds);
        //UpdateStaticEnvironment and UpdateDynamicEnvironment walk the world's static and dynamic light lists
        var staticEnvironment = new Environment();
        var dynamicEnvironment = new Environment();
        foreach (SceneLight light in SelectSources(lights))
        {
            AddLightToEnvironment(light, ref (light.HasStaticLighting ? ref staticEnvironment : ref dynamicEnvironment), bounds, channels, visibilityTester);
        }
        if (Settings.Game is MEGame.LE2)
        {
            //LE2 bounces the combined shadowed static SH, before AmbientGlow
            staticEnvironment.Shadowed += ComputeLE2StaticBounce(staticEnvironment.Shadowed, Settings.BouncedLightingFactor);
        }
        staticEnvironment.Shadowed += SHVectorRGB.FromColor(SHVector.AmbientFunction, Settings.AmbientGlow * 4);
        Vector3 ambientShadowDirection = Settings.AmbientShadowSourceDirection;
        ambientShadowDirection = ambientShadowDirection.LengthSquared() > 1e-8f ? Vector3.Normalize(ambientShadowDirection) : Vector3.Zero;
        staticEnvironment.ShadowEnvironment += SHVectorRGB.FromColor(SHVector.BasisFunction(ambientShadowDirection), Settings.AmbientShadowColor);
        ShadowInfo staticShadow = ComputeShadowInfo(staticEnvironment.ShadowEnvironment);
        ShadowInfo dynamicShadow = dynamicEnvironment.AnyLights ? ComputeShadowInfo(dynamicEnvironment.ShadowEnvironment) : default;

        //Compose (FBioDynamicLightEnvironmentState's, and the start of FDynamicLightEnvironmentState::CreateEnvironmentLightList)
        SHVectorRGB lightEnvironment = staticEnvironment.Shadowed + dynamicEnvironment.Shadowed;
        SHVectorRGB unshadowedEnvironment = staticEnvironment.Unshadowed + dynamicEnvironment.Unshadowed;
        ShadowInfo shadow = staticShadow;
        bool bioCinematic = Settings.Game is MEGame.LE2 or MEGame.LE3 && Settings.IsBio && Settings.IsCinematicQuality;
        BioLightRig rig = bioCinematic && Settings.Game is MEGame.LE3 ? Settings.CinematicLightRig : null;
        if (rig is not null)
        {
            //LE3's cinematic Compose takes the rig's lighting instead, its shadow wholly shadowed (RVA 0x1a4840)
            lightEnvironment = BioLightRig.Orient(rig.Shadowed, Settings.LightRigOrientation);
            unshadowedEnvironment = BioLightRig.Orient(rig.Unshadowed, Settings.LightRigOrientation);
            shadow = new ShadowInfo(rig.ShadowDirection, Vector3.Zero, rig.TotalShadowIntensity);
        }
        else if (Settings.Game is MEGame.LE2 && Settings.IsBio)
        {
            //LE2's Bio Compose (and its cinematic synthesis, RVA 0x18a110) adds the shadow SH before extracting the shadow
            shadow = ComputeShadowInfo(staticEnvironment.ShadowEnvironment
                + (Settings.CompositeShadowsFromDynamicLights ? dynamicEnvironment.ShadowEnvironment : default));
        }
        else if (Settings.CompositeShadowsFromDynamicLights)
        {
            if (LinearColorUtils.Luminance(staticShadow.UnshadowedIntensity) < LinearColorUtils.Luminance(dynamicShadow.UnshadowedIntensity))
            {
                shadow.Direction = dynamicShadow.Direction;
            }
            shadow.ShadowedIntensity += dynamicShadow.ShadowedIntensity;
            shadow.UnshadowedIntensity += dynamicShadow.UnshadowedIntensity;
        }
        if (shadow.Direction.LengthSquared() > 1e-8f)
        {
            shadow.Direction = Vector3.Normalize(shadow.Direction);
        }

        var lighting = new LightEnvironmentLighting
        {
            //!(bCastShadows && bAllowLightEnvironmentShadows), the latter being on by default
            RendersSHLightAfterModulatedShadows = Settings.CastShadows,
            //only lights the environment would otherwise have composited
            DirectLights = Array.FindAll(lights, light => !light.HasLightEnvironment && (!light.IsDominant || Settings.ForceCompositeAllLights)
                                                          && IsDrawnDirectlyInsteadOfComposited(light)),
        };
        float contrastFactor = Settings.IsCharacterLightEnvironment ? CharacterLightingContrastFactor : 1;
        bool useBioLightAxis = Settings.IsBio && !Settings.IsCinematicQuality;

        if (bioCinematic)
        {
            ComposeBioCinematicLights(lighting, ref lightEnvironment, bounds, contrastFactor, rig);
        }
        //the directional light
        else if (Settings.SynthesizeDirectionalLight
            && (useBioLightAxis ? ExtractBioKeyLight(ref lightEnvironment, out Vector3 direction, out Vector3 color)
                : ExtractDominantLight(ref lightEnvironment, out direction, out color))
            && (color.X > 0 || color.Y > 0 || color.Z > 0))
        {
            lighting.DirectionToLight = direction;
            //the Bio key light was desaturated before it was extracted, so that what's extracted from the SH is what's lit with. It isn't desaturated twice
            lighting.DirectionalColor = (useBioLightAxis ? color : LinearColorUtils.Desaturate(color, Settings.LightDesaturation)) * contrastFactor;
        }

        //the SH or sky light, from what remains
        if (bioCinematic && Settings.Game is MEGame.LE2)
        {
            ComposeLE2CinematicAmbient(lighting, lightEnvironment + unshadowedEnvironment, contrastFactor);
        }
        else
        {
            SHVectorRGB ambient = lightEnvironment + unshadowedEnvironment;
            //(LE3's cinematic synthesis, RVA 0x1a0c80, scales it as ordinary Bio does, and by the rig's ambient modifier)
            if (Settings.IsBio)
            {
                ambient *= rig is null ? Settings.AmbientLightScale : Settings.AmbientLightScale * rig.AmbientLightModifier;
                ambient.R[0] *= Settings.SHAmbientScale;
                ambient.G[0] *= Settings.SHAmbientScale;
                ambient.B[0] *= Settings.SHAmbientScale;
            }
            ambient *= 1 / contrastFactor;
            if (UsesSHLight)
            {
                lighting.UsesSHLight = true;
                //(a copy: the sky light is extracted from the original below)
                lighting.SHIncidentLighting = ambient;
            }
            //the sky light: the upper hemisphere's light, then the lower's from what remains
            Vector3 upper = ambient.Extract(SHVector.UpperSkyFunction);
            Vector3 lower = ambient.Extract(SHVector.LowerSkyFunction);
            lighting.UpperSkyColor = LinearColorUtils.Desaturate(upper, Settings.LightDesaturation);
            lighting.LowerSkyColor = LinearColorUtils.Desaturate(lower, Settings.LightDesaturation);
        }

        //the shadow. A cinematic Bio shadow light is at least modulated and spares its subject (RVAs LE2 0x18cbe0, LE3 0x1a31c0)
        if (Settings.CastShadows && (Settings.ModulatedShadows || bioCinematic) && shadow.Direction.LengthSquared() > 1e-5f)
        {
            //how much light remains in the shadow, as a fraction of the total
            Vector3 modShadowColor = Vector3.Min(Vector3.One, (shadow.UnshadowedIntensity - shadow.ShadowedIntensity) / Vector3.Max(shadow.UnshadowedIntensity, new Vector3(1e-5f)));
            modShadowColor = Vector3.Min(modShadowColor, Settings.MaxModulatedShadowColor);
            //the light is kept from getting too low, so the shadow isn't too long
            float minZ = MathF.Cos(Math.Clamp(90 - Settings.MinShadowAngle, 0, 180) * MathF.PI / 180);
            Vector3 shadowDirection = Vector3.Normalize(shadow.Direction with { Z = MathF.Max(shadow.Direction.Z, minZ) });
            float radius = bounds.SphereRadius;
            //LE2 uses ShadowLightDistance (component offset 0x12c) for all three distances
            float shadowLightDistance = Settings.ShadowLightDistance ?? Settings.LightDistance;
            //the shadow light takes the environment's LightShadowMode (RVAs: LE2 0x208920, LE3 0x221fa0)
            lighting.ShadowLight = new LightEnvironmentShadowLight(
                bounds.Origin + shadowDirection * radius * shadowLightDistance,
                (Settings.ShadowDistance + shadowLightDistance + 2) * radius,
                (shadowLightDistance + 1) * radius,
                ShadowFalloffExponent,
                modShadowColor,
                Settings.MinShadowResolution, Settings.MaxShadowResolution, Settings.ShadowFadeResolution,
                CullsOnSubject: bioCinematic,
                ModulateBetter: Settings.LightShadowMode is LightEnvironmentShadowMode.ModulateBetter && Settings.AllowModulateBetterShadows);
        }
        return lighting;
    }

    private bool UsesSHLight => Settings.SynthesizeSHLight && (AllowLightEnvSphericalHarmonicLights || Settings.ForceAllowLightEnvSphericalHarmonicLights);

    /// <summary>
    /// Cinematic Bio synthesis (RVAs LE2 0x18a110, LE3 0x1a0c80). Each light is its own (<see cref="CreateBioLight"/>):
    /// <list type="bullet">
    /// <item>a key (role 0, KeyLightScale);</item>
    /// <item>above one MaxCharacterCinematicLightingPasses, a fill (role 2, FillLightScale) and the view-dependent rim (<see cref="BioRimLight"/>);
    /// at two, only the brighter by luminance, and the fill isn't extracted if the rim wins.</item>
    /// </list>
    /// LE2's key has a normal shadow, and its fill and rim follow bAllowFillLightShadows and bAllowRimLightShadows. All of LE3's have one, as
    /// Engine.bKeyLightShadowsOnly (RVA 0x1a0c07) is off by default and in the installed config. LE3's rig modifiers scale the key and fill.
    /// The dominant-shadow transition is taken as finished (fraction 1), which also leaves the fill unscaled by it
    /// </summary>
    private void ComposeBioCinematicLights(LightEnvironmentLighting lighting, ref SHVectorRGB sh, BoxSphereBounds bounds, float contrastFactor, BioLightRig rig)
    {
        bool allShadowed = Settings.Game is MEGame.LE3;
        var lights = new List<SceneLight>();
        if (Settings.SynthesizeDirectionalLight)
        {
            if (ExtractBioRoleLight(ref sh, KeyAxis, Settings.KeyLightScale * (rig?.KeyLightModifier ?? Vector3.One), out Vector3 keyDirection, out Vector3 keyColor)
                && CreateBioLight(keyDirection, keyColor * contrastFactor, 0, castsShadow: true, bounds) is { } key)
            {
                lights.Add(key);
            }
            if (Settings.MaxCinematicLightingPasses > 1)
            {
                SHVectorRGB beforeFill = sh;
                //each role reports whether its color has a positive channel
                bool hasFill = ExtractBioRoleLight(ref sh, FillAxis, Settings.FillLightScale * (rig?.FillLightModifier ?? Vector3.One), out Vector3 fillDirection,
                                   out Vector3 fillColor)
                               && Max(fillColor) > 1e-8f;
                Vector4 rimColor = Settings.RimLightColor * Settings.RimLightScale;
                var rimRgb = new Vector3(rimColor.X, rimColor.Y, rimColor.Z);
                bool hasRim = MathF.Max(Max(rimRgb), rimColor.W) > 1e-4f;
                if (Settings.MaxCinematicLightingPasses == 2 && hasRim && hasFill)
                {
                    if (LinearColorUtils.Luminance(rimRgb) < LinearColorUtils.Luminance(fillColor))
                    {
                        hasRim = false;
                    }
                    else
                    {
                        hasFill = false;
                        sh = beforeFill;
                    }
                }
                if (hasFill && CreateBioLight(fillDirection, fillColor * contrastFactor, 0, allShadowed || Settings.AllowFillLightShadows, bounds) is { } fill)
                {
                    lights.Add(fill);
                }
                if (hasRim)
                {
                    lighting.RimLight = new BioRimLight(this, bounds, rimColor * contrastFactor, allShadowed || Settings.AllowRimLightShadows);
                }
            }
        }
        lighting.PointLights = lights;

        static float Max(Vector3 v) => MathF.Max(v.X, MathF.Max(v.Y, v.Z));
    }

    /// <summary>
    /// LE2 cinematic Bio's remainder (RVA 0x18a110): an SH light (band 0 scaled by SHAmbientScale / contrast, then all of it by AmbientLightScale)
    /// or a sky light (desaturated and scaled by SHAmbientScale / contrast)
    /// </summary>
    private void ComposeLE2CinematicAmbient(LightEnvironmentLighting lighting, SHVectorRGB ambient, float contrastFactor)
    {
        float ambientScale = Settings.SHAmbientScale / contrastFactor;
        if (UsesSHLight)
        {
            SHVectorRGB shLight = ambient;
            shLight.R[0] *= ambientScale;
            shLight.G[0] *= ambientScale;
            shLight.B[0] *= ambientScale;
            lighting.UsesSHLight = true;
            lighting.SHIncidentLighting = shLight * Settings.AmbientLightScale;
        }
        //(the game makes the sky light only without the SH light; the preview keeps it for materials without SH light shaders)
        Vector3 upper = ambient.Extract(SHVector.UpperSkyFunction);
        Vector3 lower = ambient.Extract(SHVector.LowerSkyFunction);
        lighting.UpperSkyColor = LinearColorUtils.Desaturate(upper, Settings.LightDesaturation) * ambientScale;
        lighting.LowerSkyColor = LinearColorUtils.Desaturate(lower, Settings.LightDesaturation) * ambientScale;
    }

    //SFXPointLightComponent keeps PointLightComponent's FalloffExponent
    private const float BioPointFalloffExponent = 2;

    /// <summary>
    /// Cinematic Bio light creation (slot 16, RVAs LE2 0x189ca0, LE3 0x1a0840): none unless a channel (alpha included) exceeds 1e-4. With
    /// UseOptimizedLightingPath, an SFXPointLightComponent LightDistance bounds radii out, reaching ShadowDistance + LightDistance + 2 radii, whose
    /// color is divided by its attenuation at the point SurfaceBoundsRatio radii towards it (slot 9, RVAs 0x18f9d0, 0x1a5e30); otherwise a directional
    /// light. Its shadow is normal, and its color is stored as a brightness and 8-bit channels
    /// </summary>
    internal SceneLight CreateBioLight(Vector3 direction, Vector3 color, float alpha, bool castsShadow, BoxSphereBounds bounds)
    {
        if (!(MathF.Max(MathF.Max(color.X, color.Y), MathF.Max(color.Z, alpha)) > 1e-4f) || !float.IsFinite(color.X + color.Y + color.Z))
        {
            return null;
        }
        ExportEntry owner = Owner ?? Export;
        DynamicLightEnvironment shadowSubject = castsShadow ? this : null;
        if (!Settings.UseOptimizedLightingPath)
        {
            return SceneLight.CreateEnvironmentDirectional(owner, -direction, QuantizeLightColor(color), shadowSubject);
        }
        float boundsRadius = bounds.SphereRadius;
        Vector3 position = bounds.Origin + direction * boundsRadius * Settings.LightDistance;
        float radius = (Settings.ShadowDistance + Settings.LightDistance + 2) * boundsRadius;
        Vector3 toLight = position - bounds.Origin;
        float distanceSquared = toLight.LengthSquared();
        Vector3 towardLight = distanceSquared == 1 ? toLight : distanceSquared >= 1e-8f ? toLight / MathF.Sqrt(distanceSquared) : Vector3.Zero;
        Vector3 point = bounds.Origin + towardLight * MathF.Min(MathF.Sqrt(distanceSquared) - 1, Settings.SurfaceBoundsRatio * boundsRadius);
        float falloff = MathF.Max(1 - Vector3.DistanceSquared(point, position) / (radius * radius), 0);
        color = QuantizeLightColor(color * MathF.Pow(falloff, -BioPointFalloffExponent));
        //Keep invalid edited distances out of shader constants
        return float.IsFinite(color.X + color.Y + color.Z) && float.IsFinite(radius) && radius > 0
            ? SceneLight.CreateEnvironmentPoint(owner, position, radius, color, BioPointFalloffExponent, shadowSubject)
            : null;
    }

    /// <summary>
    /// FDynamicLightEnvironmentState::AddLightToEnvironment
    /// </summary>
    private void AddLightToEnvironment(SceneLight light, ref Environment environment, BoxSphereBounds bounds, LightingChannels ownerChannels,
        LightVisibilityTester visibilityTester)
    {
        //the light's channels, with CompositeDynamic standing in for Dynamic
        if (!DoesLightAffectOwner(light, bounds, ownerChannels) || !visibilityTester.IsLightVisible(light, out float visibility))
        {
            return;
        }
        environment.AnyLights = true;
        ref SHVectorRGB lightEnvironment = ref light.CastsCompositeShadow ? ref environment.Shadowed : ref environment.Unshadowed;
        SHVectorRGB compositeShadowLighting;
        if (light.Type is SceneLightType.Sky)
        {
            //light from the sky, and its light bounced off the ground (from below) and the ceiling (from above)
            Vector3 upper = light.Color;
            Vector3 lower = light.LowerColor;
            Vector3 bounce = light.BouncedLightColor * Settings.BouncedLightingFactor;
            SHVectorRGB skyLighting = SHVectorRGB.FromColor(SHVector.UpperSkyFunction, upper + lower * bounce)
                                      + SHVectorRGB.FromColor(SHVector.LowerSkyFunction, lower + upper * bounce);
            lightEnvironment += skyLighting;
            if (!light.CastsCompositeShadow)
            {
                return;
            }
            compositeShadowLighting = light.AffectsCompositeShadowDirection ? skyLighting
                : SHVectorRGB.FromColor(SHVector.AmbientFunction, skyLighting.CalcIntegral());
        }
        else
        {
            Vector3 lightVector = light.Type is SceneLightType.Directional ? -light.Direction : light.Position - bounds.Origin;
            lightVector = lightVector.LengthSquared() > 1e-8f ? Vector3.Normalize(lightVector) : Vector3.Zero;
            Vector3 directIntensity = light.GetDirectIntensity(bounds.Origin) * visibility;
            //LE2 and LE3 (RVAs 0x202e90, 0x21be70) leave a non-composited bypass light out of the illumination, drawing it directly;
            //its bounce and composite shadow still count
            if (!IsDrawnDirectlyInsteadOfComposited(light))
            {
                lightEnvironment += SHVectorRGB.FromColor(SHVector.BasisFunction(lightVector), directIntensity);
            }
            if (Settings.Game is not MEGame.LE2)
            {
                //LE3 bounces each light; LE2 bounces the combined static SH in Compute
                Vector3 bouncedIntensity = LinearColorUtils.Desaturate(directIntensity * light.BouncedLightColor, Settings.BouncedLightingDesaturation);
                SHVectorRGB bouncedLighting = SHVectorRGB.FromColor(SHVector.BasisFunction(-lightVector), bouncedIntensity * Settings.BouncedLightingFactor);
                if (Settings.ShadowBouncedLight)
                {
                    lightEnvironment += bouncedLighting;
                }
                else
                {
                    environment.Unshadowed += bouncedLighting;
                }
            }
            if (!light.CastsCompositeShadow)
            {
                return;
            }
            compositeShadowLighting = light.AffectsCompositeShadowDirection ? SHVectorRGB.FromColor(SHVector.BasisFunction(lightVector), directIntensity)
                : SHVectorRGB.FromColor(SHVector.AmbientFunction, directIntensity * 0.2820948f);
        }
        //the light's shadowed part keeps its direction, and the part that remains in its shadow (ModShadowColor) becomes ambient
        Vector3 integral = compositeShadowLighting.CalcIntegral();
        environment.ShadowEnvironment += SHVectorRGB.FromColor(SHVector.AmbientFunction, integral * light.ModShadowColor)
                                         + compositeShadowLighting * (Vector3.One - light.ModShadowColor);
    }

    /// <summary>
    /// LE2/LE3: dynamic && bAllowedToBypassLightEnvironments && (!CompositeDynamicLights || bForceNonCompositeDynamicLights).
    /// Both games install CompositeDynamicLights=false. Sky lights are always composited
    /// </summary>
    private bool IsDrawnDirectlyInsteadOfComposited(SceneLight light) => Settings.Game is MEGame.LE2 or MEGame.LE3 && light.Type is not SceneLightType.Sky
        && !light.HasStaticLighting && light.AllowedToBypassLightEnvironments
        && (!GameLightingProfile.ForGame(Settings.Game).CompositeDynamicLights || Settings.ForceNonCompositeDynamicLights);

    private LightEnvironmentLighting ComputeLE1(SceneLight[] lights, BoxSphereBounds bounds, LightingChannels channels, LevelGeometry geometry)
    {
        //Unsupported LE1 qualities must not fall through to LE3's synthesis
        if (GameLightingProfile.GetEnvironmentSupport(Settings) is not null)
            return null;

        SceneLight[] sources = SelectSources(lights);
        if (sources is null) return null;

        bounds.Origin += Settings.BoundsOffset;
        var visibility = new LightVisibilityTester(geometry, Owner, Settings, bounds);
        SHVectorRGB incident = default, shadow = default;
        foreach (SceneLight light in sources)
        {
            if (light.HasLightEnvironment || !DoesLightAffectOwner(light, bounds, channels)
                || !visibility.IsLightVisible(light, out float visible))
                continue;

            SHVectorRGB contribution;
            if (light.Type is SceneLightType.Sky)
            {
                contribution = SHVectorRGB.FromColor(LE1UpperSky, light.Color)
                    + SHVectorRGB.FromColor(LE1LowerSky, light.LowerColor);
            }
            else
            {
                Vector3 direction = light.Type is SceneLightType.Directional ? -light.Direction : light.Position - bounds.Origin;
                direction = direction.LengthSquared() > 1e-8f ? Vector3.Normalize(direction) : Vector3.Zero;
                //LE1 samples two thirds of the bounds radius toward the source, not at the origin.
                Vector3 intensity = light.GetDirectIntensity(bounds.Origin + direction * (bounds.SphereRadius * (2f / 3)))
                    * visible * (float.IsFinite(Settings.DynamicLightScale) && Settings.DynamicLightScale > 0 ? Settings.DynamicLightScale : 1);
                contribution = SHVectorRGB.FromColor(LE1DirectionalBasis(direction), intensity);
            }
            incident += contribution;
            if (light.CastsCompositeShadow)
                shadow += contribution;
        }
        LightEnvironmentLighting result = ComposeLE1(bounds, incident, shadow);
        if (result is not null)
        {
            //LE1 both composites and draws a bypass light: AffectsPrimitive (RVA 0x1deae0) admits it, and accumulation (0x1dc5b0) ignores the bit
            result.DirectLights = Array.FindAll(lights, light => light.BypassesLightEnvironments);
        }
        return result;
    }

    /// <summary>
    /// The lights UpdateEnvironment walks, before the affects and visibility tests. LE2/LE3 walk dominant lights only with bForceCompositeAllLights;
    /// LE1 walks the static lights, its one dominant directional, every OverriddenLightComponents entry, then the dynamic lights.
    /// Null if LE1's list can't be resolved
    /// </summary>
    private SceneLight[] SelectSources(SceneLight[] lights)
    {
        if (Settings.Game is not MEGame.LE1)
        {
            return Array.FindAll(lights, light => !light.HasLightEnvironment && (!light.IsDominant || Settings.ForceCompositeAllLights));
        }
        //Load order isn't attachment order, so several dominant directional candidates can't be resolved
        SceneLight dominant = null;
        foreach (SceneLight light in lights)
        {
            if (!light.HasLightEnvironment && light.IsDominant && light.Type is SceneLightType.Directional)
            {
                if (dominant is not null && dominant != light) return null;
                dominant = light;
            }
        }
        var selected = new List<SceneLight>();
        selected.AddRange(lights.Where(light => IsOrdinary(light) && light.HasStaticLighting));
        if (dominant is not null) selected.Add(dominant);
        foreach (ExportEntry source in Settings.LE1OverriddenLightComponents)
        {
            //Resolved in the declaring package, then mapped to the scene instance so edits apply
            SceneLight light = source is null ? null : Array.Find(lights, light => light.Export == source);
            if (light is null) return null;
            selected.Add(light);
        }
        selected.AddRange(lights.Where(light => IsOrdinary(light) && !light.HasStaticLighting));
        return selected.ToArray();

        static bool IsOrdinary(SceneLight light) => !light.HasLightEnvironment && !light.IsDominant;
    }

    /// <summary>
    /// LE1's SHBasisFunction (RVA 0xf1a10): UE3's, except the l=2, m=0 term is -0.5 - 1.5z² rather than -0.5 + 1.5z².
    /// LE2 and LE3 use UE3's basis.
    /// </summary>
    internal static SHVector LE1DirectionalBasis(Vector3 direction)
    {
        SHVector basis = SHVector.BasisFunction(direction);
        basis[6] = -0.94617593f * direction.Z * direction.Z - 0.31539199f;
        return basis * (1 / MathF.Sqrt(basis.Dot(basis)));
    }

    //InitSHFunctions (RVA 0x1e50b0) integrates the basis over each hemisphere by random sampling, normalized against the pole.
    //With LE1's basis the z² coefficient is -(0.3154 * 2π + 0.9462 * 2π/3) / π rather than zero; these are the analytic values.
    internal static readonly SHVector LE1UpperSky = LE1Sky(SHVector.UpperSkyFunction, Vector3.UnitZ);
    internal static readonly SHVector LE1LowerSky = LE1Sky(SHVector.LowerSkyFunction, -Vector3.UnitZ);
    private static readonly SHVector LE1Ambient = LE1UpperSky + LE1LowerSky;

    private static SHVector LE1Sky(SHVector hemisphere, Vector3 pole)
    {
        hemisphere[6] = -1.2615679f;
        return hemisphere * (1 / hemisphere.Dot(LE1DirectionalBasis(pole)));
    }

    /// <summary>A point created by LE1's helper (RVA 0x1e11c0)</summary>
    private sealed record LE1Point(Vector3 Position, float Radius, Vector3 Color, bool CastsShadow, Vector3 ModShadowColor);

    /// <summary>
    /// Settled LE1 synthesis (RVA 0x1dfe20): points extracted from the SH by quality, a residual sky light and a modulated-shadow point.
    /// Null for unimplemented qualities
    /// </summary>
    private LightEnvironmentLighting ComposeLE1(BoxSphereBounds bounds, SHVectorRGB incident, SHVectorRGB shadow)
    {
        LE1LightEnvironmentQuality quality = Settings.EffectiveLE1Quality;
        if (quality is LE1LightEnvironmentQuality.Unknown)
            return null;
        ExportEntry owner = Owner ?? Export;
        var result = new LightEnvironmentLighting();
        var points = new List<SceneLight>();
        var pointShadows = new List<LightEnvironmentShadowLight>();
        //The ambient term and the shadow's ambient color both project the original sum
        Vector3 ambientColor = incident.Project(LE1Ambient);
        SHVectorRGB sh = incident;
        Vector3 upper = Vector3.Zero, lower = Vector3.Zero;
        //CinematicPlusMod synthesizes as Cinematic, and also casts the environment shadow
        bool cinematic = quality is LE1LightEnvironmentQuality.Cinematic or LE1LightEnvironmentQuality.CinematicPlusMod;
        switch (quality)
        {
            case LE1LightEnvironmentQuality.Default:
                //Default extracts upper then lower sky before the lighting point.
                upper += sh.Extract(LE1UpperSky);
                lower += sh.Extract(LE1LowerSky);
                AddPoint(ExtractLE1Point(ref sh, bounds, isLight: true, castsShadow: false, Vector3.Zero, addIsBlack: true, modulate: true));
                break;
            case LE1LightEnvironmentQuality.NonFlat:
                AddPoint(ExtractLE1Point(ref sh, bounds, isLight: true, castsShadow: false, Vector3.Zero, addIsBlack: true, modulate: true));
                upper += sh.Extract(LE1UpperSky);
                lower += sh.Extract(LE1LowerSky);
                break;
            case LE1LightEnvironmentQuality.MultiLight:
                //RVA 0x1e0e40: nNumMultiLights attempts, each extracting if the strongest channel exceeds the border; only the first casts a modulated shadow
                for (int i = 0; i < Settings.NumMultiLights; i++)
                {
                    Vector3 direction = GetLuminanceDirection(sh);
                    if (direction.LengthSquared() < 1e-5f) continue;
                    Vector3 projected = sh.Project(LE1DirectionalBasis(Vector3.Normalize(direction)));
                    if (!(MathF.Max(projected.X, MathF.Max(projected.Y, projected.Z)) > Settings.MultiLightBorder)) continue;
                    AddPoint(ExtractLE1Point(ref sh, bounds, isLight: true, castsShadow: i == 0, Vector3.Zero, addIsBlack: true, modulate: true));
                }
                upper += sh.Extract(LE1UpperSky);
                lower += sh.Extract(LE1LowerSky);
                break;
            case LE1LightEnvironmentQuality.Cinematic:
            case LE1LightEnvironmentQuality.CinematicPlusMod:
            {
                //RVA 0x1dfa80: a key with a normal shadow and sky added, then an unshadowed fill capped against the key.
                //Neither added color is black, so a valid direction always creates the light.
                LE1Point key = ExtractLE1Point(ref sh, bounds, isLight: true, castsShadow: true,
                    ambientColor * Settings.CinematicKeySkyLight, addIsBlack: false, modulate: false);
                if (key is null) break;
                AddPoint(key, normalShadow: true);
                LE1Point fill = ExtractLE1Point(ref sh, bounds, isLight: true, castsShadow: false,
                    ambientColor * Settings.CinematicFillSkyLight, addIsBlack: false, modulate: true);
                if (fill is not null)
                {
                    //Brightness is the largest quantized channel. The cap replaces the fill's brightness and keeps its color.
                    float keyBrightness = Brightness(key.Color), fillBrightness = Brightness(fill.Color);
                    float cap = Settings.CinematicFillCap * keyBrightness;
                    if (cap < fillBrightness)
                        fill = fill with { Color = fill.Color * (cap / fillBrightness) };
                    AddPoint(fill);
                }
                upper += sh.Extract(LE1UpperSky);
                lower += sh.Extract(LE1LowerSky);
                break;
            }
        }

        if (Settings.CastShadows && quality is not LE1LightEnvironmentQuality.Cinematic)
        {
            //LE1 replaces the shadow accumulator when its direction is absent or nearly vertical.
            Vector3 shadowDirection = GetLuminanceDirection(shadow);
            if (shadowDirection.LengthSquared() < 1e-5f || Vector3.Normalize(shadowDirection).Z > 0.965f)
            {
                Vector3 source = Settings.AmbientShadowSourceDirection;
                source = source.LengthSquared() > 1e-8f ? Vector3.Normalize(source) : Vector3.Zero;
                shadow = SHVectorRGB.FromColor(LE1DirectionalBasis(source), Settings.AmbientShadowColor);
            }
            //The ambient color has zero alpha, so it never equals black: only a degenerate direction declines the light.
            //Only LightShadow_Modulate computes a ModShadowColor; ModulateBetter's new point keeps LightComponent's default, black
            if (Settings.ModulatedShadows
                && ExtractLE1Point(ref shadow, bounds, isLight: false, castsShadow: true, ambientColor, addIsBlack: false,
                    modulate: Settings.LightShadowMode is LightEnvironmentShadowMode.Modulate) is { } shadowPoint
                && float.IsFinite(shadowPoint.Radius) && shadowPoint.Radius > 0)
            {
                //RVA 0x1e0839 sets bCullModulatedShadowOnSubject on CinematicPlusMod's shadow light
                result.ShadowLight = ToLE1ShadowLight(shadowPoint) with
                {
                    CullsOnSubject = quality is LE1LightEnvironmentQuality.CinematicPlusMod,
                    ModulateBetter = Settings.LightShadowMode is LightEnvironmentShadowMode.ModulateBetter && Settings.AllowModulateBetterShadows
                };
            }
        }

        float ambientScalar = cinematic ? Settings.CinematicAmbientScalar : Settings.AmbientScalar;
        result.UpperSkyColor = QuantizeLightColor(upper + ambientColor * ambientScalar);
        result.LowerSkyColor = QuantizeLightColor(lower + ambientColor * ambientScalar);
        result.PointLights = points;
        result.PointShadowLights = pointShadows;
        return result;

        void AddPoint(LE1Point point, bool normalShadow = false)
        {
            if (point is null) return;
            //Keep invalid edited distances out of shader constants
            if (point.Color != Vector3.Zero && float.IsFinite(point.Color.X + point.Color.Y + point.Color.Z) && float.IsFinite(point.Radius))
            {
                points.Add(SceneLight.CreateEnvironmentPoint(owner, point.Position, point.Radius, point.Color, LE1PointFalloff,
                    normalShadow ? this : null, Settings.LE1ShadowRadiusMultiplier,
                    Settings.LE1PointMinShadowResolution, Settings.LE1PointMaxShadowResolution, Settings.LE1PointShadowFadeResolution));
            }
            if (point.CastsShadow && !normalShadow && float.IsFinite(point.Radius) && point.Radius > 0)
                pointShadows.Add(ToLE1ShadowLight(point));
        }

        static float Brightness(Vector3 color) => MathF.Max(color.X, MathF.Max(color.Y, color.Z));
    }

    private LightEnvironmentShadowLight ToLE1ShadowLight(LE1Point point) =>
        //Native sets exponent 1/3 and copies minimum falloff radius from the point CDO (stock zero).
        new(point.Position, point.Radius, Settings.LE1MinShadowFalloffRadius, 1f / 3, point.ModShadowColor,
            Settings.LE1PointMinShadowResolution, Settings.LE1PointMaxShadowResolution, Settings.LE1PointShadowFadeResolution,
            Settings.LE1ShadowRadiusMultiplier);

    private float LE1PointFalloff =>
        float.IsFinite(Settings.LE1PointFalloffExponent) && Settings.LE1PointFalloffExponent >= 0 ? Settings.LE1PointFalloffExponent : 2;

    /// <summary>
    /// LE1's RVA 0x1e11c0: extracts the luminance-direction lobe and places a point, compensating its color for attenuation at the origin.
    /// A shadow point has no brightness; with LightShadow_Modulate its shadow color is the share of light that remains.
    /// </summary>
    /// <param name="addIsBlack">The added color equals LinearColor::Black, so an all-nonpositive projection declines the light</param>
    private LE1Point ExtractLE1Point(ref SHVectorRGB sh, BoxSphereBounds bounds, bool isLight, bool castsShadow, Vector3 added, bool addIsBlack, bool modulate)
    {
        Vector3 direction = GetLuminanceDirection(sh);
        if (direction.LengthSquared() < 1e-5f)
            return null;
        direction = Vector3.Normalize(direction);
        SHVector basis = LE1DirectionalBasis(direction);
        Vector3 color = sh.Project(basis);
        if (color.X <= 0 && color.Y <= 0 && color.Z <= 0 && addIsBlack)
            return null;
        sh.Extract(basis, color);
        float distance = bounds.SphereRadius * (Settings.LightDistance + 1);
        float radius = bounds.SphereRadius * (Settings.LightDistance + Settings.ShadowDistance + 2);
        Vector3 position = bounds.Origin + direction * distance;
        Vector3 lightColor = Vector3.Zero;
        Vector3 total = color;
        if (isLight)
        {
            total = color + added;
            float attenuation = radius > 0 ? MathF.Pow(MathF.Max(0, 1 - distance * distance / (radius * radius)), LE1PointFalloff) : 0;
            lightColor = attenuation > 0 && float.IsFinite(attenuation) ? QuantizeLightColor(total / attenuation) : Vector3.Zero;
        }
        Vector3 modShadowColor = Vector3.Zero;
        if (castsShadow && modulate)
        {
            //The remaining light: the added color relative to the extracted color, plus the added color for a lighting point
            Vector3 fraction = Vector3.Min(Vector3.One, Vector3.Min(added, total) / Vector3.Max(added + total, new Vector3(1e-5f)));
            modShadowColor = fraction + (Vector3.One - fraction) * MathF.Max(0, Settings.ModShadowIntensity);
        }
        return new LE1Point(position, radius, lightColor, castsShadow, modShadowColor);
    }

    //the unclamped luminance direction, as LE1's helpers find it
    private static Vector3 GetLuminanceDirection(in SHVectorRGB sh)
    {
        SHVector luminance = sh.GetLuminance();
        return new Vector3(-luminance[3], -luminance[1], luminance[2]);
    }

    //SetLightColor (LE1, and RVAs LE2 0x75960, LE3 0x91010) stores max RGB as brightness and truncates gamma-encoded channels to bytes.
    internal static Vector3 QuantizeLightColor(Vector3 color)
    {
        color = Vector3.Max(Vector3.Zero, color);
        float brightness = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        if (brightness <= 0 || !float.IsFinite(brightness))
            return Vector3.Zero;
        return new Vector3(Channel(color.X), Channel(color.Y), Channel(color.Z)) * brightness;

        float Channel(float value)
        {
            int encoded = Math.Clamp((int)(MathF.Pow(value / brightness, 1f / 2.2f) * 255), 0, 255);
            return MathF.Pow(encoded / 255f, 2.2f);
        }
    }

    /// <summary>
    /// LE2 UpdateStaticEnvironment: saturation 2, reversed, scaled by BouncedLightingFactor. Signed coefficients aren't clamped.
    /// </summary>
    private static SHVectorRGB ComputeLE2StaticBounce(in SHVectorRGB incident, float factor)
    {
        SHVectorRGB bounce = incident * 2 - SHVectorRGB.FromColor(incident.GetLuminance(), Vector3.One);
        //Only the l=1 band changes sign under direction reversal; l=0 and l=2 are even.
        for (int i = 1; i <= 3; i++)
        {
            bounce.R[i] = -bounce.R[i];
            bounce.G[i] = -bounce.G[i];
            bounce.B[i] = -bounce.B[i];
        }
        return bounce * factor;
    }

    /// <summary>
    /// FDynamicLightEnvironmentState::DoesLightAffectOwner. (Light functions and precomputed shadowing don't matter to light environments)
    /// </summary>
    private bool DoesLightAffectOwner(SceneLight light, BoxSphereBounds bounds, LightingChannels ownerChannels) =>
        GetOwnerExclusion(light, bounds, ownerChannels) is LightEligibility.Affects;

    /// <summary>
    /// For diagnostics: whether the light environment composites the light, under the same rules as <see cref="Compute"/>, and if not, why.
    /// Whether its visibility trace to the light is blocked isn't considered
    /// </summary>
    public LightEligibility GetEligibility(SceneLight light)
    {
        if (light.HasLightEnvironment)
        {
            return LightEligibility.BelongsToLightEnvironment;
        }
        //(without a context, LE1's list can't account for the scene's other lights)
        SceneLight[] sources = SelectSources(Context?.GetLights(out _) ?? [light]);
        if (sources is null) return LightEligibility.UnresolvedEnvironmentSources;
        if (Array.IndexOf(sources, light) < 0) return LightEligibility.DominantNotComposited;
        (BoxSphereBounds bounds, LightingChannels channels) = GetOwnerBoundsAndChannels();
        if (Settings.Game is MEGame.LE1) bounds.Origin += Settings.BoundsOffset;
        LightEligibility exclusion = GetOwnerExclusion(light, bounds, channels);
        return exclusion is LightEligibility.Affects ? LightEligibility.LightEnvironment : exclusion;
    }

    /// <summary>Eligibility for one primitive, including direct lighting</summary>
    public LightEligibility GetEligibility(SceneLight light, LightEnvironmentPrimitive primitive, BoxSphereBounds bounds)
    {
        if (primitive is not null && LevelPackage is { } level && GetLighting() is { } lighting)
        {
            foreach (SceneLight direct in lighting.DirectLights)
            {
                if (direct != light) continue;
                LightEligibility directEligibility = LightEnvironmentDirectLighting.Evaluate(light, primitive, bounds, level);
                //LE2 and LE3 leave these lights out of synthesis, so a rejected direct light doesn't fall back to the composite
                if (directEligibility.IsAffecting() || IsDrawnDirectlyInsteadOfComposited(light))
                    return directEligibility;
            }
        }
        return GetEligibility(light);
    }

    private LightEligibility GetOwnerExclusion(SceneLight light, BoxSphereBounds bounds, LightingChannels ownerChannels)
    {
        if (light.BakedGIOnly) return LightEligibility.BakedGIOnly;
        //(UE3's AffectsBounds includes the light's inclusion and exclusion volumes)
        if (!light.AffectsBounds(bounds))
        {
            return LightEligibility.OutOfRange;
        }
        if (light.GetVolumeExclusion(bounds) is var volumeExclusion and not LightEligibility.Affects)
        {
            return volumeExclusion;
        }
        //a light's CompositeDynamic channel stands in for Dynamic: lights that only have Dynamic don't affect light environments
        uint lightChannels = light.LightingChannels.Channels & ~LightingChannels.DynamicBit;
        if ((light.LightingChannels.Channels & LightingChannels.CompositeDynamicBit) != 0)
        {
            lightChannels = (lightChannels & ~LightingChannels.CompositeDynamicBit) | LightingChannels.DynamicBit;
        }
        if ((lightChannels & ownerChannels.Channels) == 0)
        {
            return LightEligibility.LightingChannels;
        }
        //LE1's owner supplies its package's level; self-containment and the allowed levels then apply
        IMEPackage level = (Owner ?? Export).FileRef;
        return LightAssignment.EvaluateLevel(light, level.FileNameNoExtension, level.Flags.Has(UnrealFlags.EPackageFlags.SelfContainedLighting));
    }

    /// <summary>
    /// The shadow's direction and intensities, removing the shadow-casting light from the shadow environment (vfunc ExtractDominantLight)
    /// </summary>
    private static ShadowInfo ComputeShadowInfo(SHVectorRGB shadowEnvironment)
    {
        if (!ExtractDominantLight(ref shadowEnvironment, out Vector3 direction, out Vector3 intensity))
        {
            return default;
        }
        Vector3 unshadowed = intensity + shadowEnvironment.Project(SHVector.AmbientFunction);
        return new ShadowInfo(direction, intensity, unshadowed);
    }

    /// <summary>
    /// The direction of the strongest light in the environment, from the luminance of its first band, and the color of a directional light from there
    /// that best matches it. That light is removed from <paramref name="sh"/>
    /// </summary>
    private static bool ExtractDominantLight(ref SHVectorRGB sh, out Vector3 direction, out Vector3 color)
    {
        SHVector luminance = sh.GetLuminance();
        direction = new Vector3(-luminance[3], -luminance[1], luminance[2]);
        color = Vector3.Zero;
        if (direction.LengthSquared() < 1e-10f)
        {
            return false;
        }
        direction = Vector3.Normalize(direction);
        color = sh.Extract(SHVector.BasisFunction(direction));
        return true;
    }

    //The key and (cinematic) fill roles' axis states, guarded by CacheLock
    private readonly BioLightAxisState KeyAxis = new(), FillAxis = new();

    /// <summary>
    /// The Bio non-cinematic key: role 0, then LE2 desaturates again
    /// </summary>
    private bool ExtractBioKeyLight(ref SHVectorRGB sh, out Vector3 direction, out Vector3 color)
    {
        if (!ExtractBioRoleLight(ref sh, KeyAxis, Settings.KeyLightScale, out direction, out color))
        {
            return false;
        }
        if (Settings.Game is MEGame.LE2)
        {
            color = LinearColorUtils.Desaturate(color, Settings.LightDesaturation);
        }
        return true;
    }

    /// <summary>
    /// A Bio role light (RVAs LE2 0x18cf10, LE3 0x1a32d0): the light axis search (LightAxisEnabled), otherwise the luminance direction, which
    /// LE3 clamps to the upper hemisphere. The desaturated color is extracted and scaled by the role's scale. Only the non-cinematic key reuses
    /// its last search. Transitions are settled. False if there's no direction
    /// </summary>
    private bool ExtractBioRoleLight(ref SHVectorRGB sh, BioLightAxisState axis, Vector3 scale, out Vector3 direction, out Vector3 color)
    {
        SHVector luminance = sh.GetLuminance();
        color = Vector3.Zero;
        direction = !Settings.LightAxisEnabled
            ? new Vector3(-luminance[3], -luminance[1], Settings.Game is MEGame.LE2 ? luminance[2] : MathF.Max(luminance[2], 0))
            : Settings.IsCinematicQuality || axis != KeyAxis ? axis.FindDirection(luminance) : axis.FindKeyDirection(luminance);
        float lengthSquared = direction.LengthSquared();
        if (lengthSquared < 1e-5f)
        {
            return false;
        }
        if (lengthSquared > 1e-8f)
        {
            direction = Vector3.Normalize(direction);
        }
        SHVector basis = SHVector.BasisFunction(direction);
        color = LinearColorUtils.Desaturate(sh.Project(basis), Settings.LightDesaturation);
        if (MathF.Max(color.X, MathF.Max(color.Y, color.Z)) > 1e-8f)
        {
            sh.Extract(basis, color);
            color *= scale;
        }
        axis.PreviousDirection = direction;
        return true;
    }
}

/// <summary>
/// FDynamicLightEnvironmentState::IsLightVisible: traces from the light environment to each light, against the level's geometry
/// </summary>
internal sealed class LightVisibilityTester
{
    private readonly LevelGeometry Geometry;
    //LE1 passes no source actor to SingleLineCheck; the other games exclude the environment's owner.
    private readonly ExportEntry IgnoredActor;
    private readonly LightEnvironmentSettings Settings;
    private readonly BoxSphereBounds Bounds;
    private readonly Vector3[] SampleOffsets;

    public LightVisibilityTester(LevelGeometry geometry, ExportEntry owner, LightEnvironmentSettings settings, BoxSphereBounds bounds)
    {
        Geometry = geometry;
        IgnoredActor = settings.Game is MEGame.LE1 ? null : owner;
        Settings = settings;
        Bounds = bounds;
        //UpdateOwner: random points within the bounds, and the center. The center is the only one by default
        //LE1 IsLightVisible (RVA 0x1e6890) makes one center trace
        int numSamples = settings.Game is MEGame.LE1 ? 1 : Math.Max(settings.NumVolumeVisibilitySamples, 1);
        SampleOffsets = new Vector3[numSamples];
        var random = new Random(0);
        for (int i = 0; i < numSamples - 1; i++)
        {
            SampleOffsets[i] = new Vector3(random.NextSingle() * 2 - 1, random.NextSingle() * 2 - 1, random.NextSingle() * 2 - 1);
        }
    }

    public bool IsLightVisible(SceneLight light, out float visibility)
    {
        visibility = 1;
        //sky lights are always visible, as are lights that don't cast static shadows
        if (light.Type is SceneLightType.Sky || !light.CastsStaticShadows)
        {
            return true;
        }
        int unblocked = 0;
        foreach (Vector3 offset in SampleOffsets)
        {
            Vector3 start = Bounds.Origin;
            if (Settings.Game is not MEGame.LE1 && Settings.TraceFromClosestBoundsPoint)
            {
                Vector3 toLight = light.Type is SceneLightType.Directional ? -light.Direction : light.Position - Bounds.Origin;
                if (toLight.LengthSquared() > 1e-8f)
                {
                    start += Vector3.Normalize(toLight) * Bounds.SphereRadius;
                }
            }
            start += offset * Bounds.BoxExtent;
            //directional lights are traced towards their position at TraceDistance
            Vector3 end = light.Type is SceneLightType.Directional ? start - light.Direction * light.TraceDistance : light.Position;
            //LE1 traces from the light to the center; the tolerance accepts a small overshoot, so direction matters
            bool blocked = Settings.Game is MEGame.LE1
                ? Geometry.IsSegmentBlocked(end, start, light, IgnoredActor)
                : Geometry.IsSegmentBlocked(start, end, light, IgnoredActor);
            if (!blocked)
            {
                unblocked++;
            }
        }
        visibility = (float)unblocked / SampleOffsets.Length;
        return visibility > 0;
    }
}
