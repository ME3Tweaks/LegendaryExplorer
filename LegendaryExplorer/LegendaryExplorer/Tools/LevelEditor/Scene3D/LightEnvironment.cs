using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A light environment's properties: DynamicLightEnvironmentComponent's, and BioDynamicLightEnvironmentComponent's additions.
/// Defaults are LE3's class defaults and BioEngine.ini config values.
/// </summary>
public sealed class LightEnvironmentSettings
{
    /// <summary>
    /// BioDynamicLightEnvironmentComponent, which LE3 builds its lights for differently (FBioDynamicLightEnvironmentState)
    /// </summary>
    public bool IsBio;

    public bool CastShadows = true;
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
    public float ShadowDistance = 5;
    public int MinShadowResolution;
    public int MaxShadowResolution;
    public int ShadowFadeResolution;
    public float BouncedLightingFactor = 0.3f;
    public float BouncedLightingDesaturation = 0.5f;
    public float MinShadowAngle = 25;
    public LightingChannels OverriddenLightingChannels;
    public bool ModulatedShadows = true;

    //BioDynamicLightEnvironmentComponent
    public Vector3 KeyLightScale = Vector3.One;
    public Vector3 AmbientLightScale = Vector3.One;
    public float SHAmbientScale = 0.2f;
    public bool LightAxisEnabled = true;
    public bool IsCinematicQuality;

    /// <summary>
    /// Reads a light environment component's properties, including inherited ones
    /// </summary>
    public static LightEnvironmentSettings Read(ExportEntry export, PackageCache packageCache)
    {
        var settings = new LightEnvironmentSettings
        {
            IsBio = export.ClassName == "BioDynamicLightEnvironmentComponent"
        };
        //Default__BioDynamicLightEnvironmentComponent sets bSynthesizeSHLight
        settings.SynthesizeSHLight = settings.IsBio;
        PropertyCollection props = export.GetCondensedProperties(packageCache, resolveImports: true, mergeStructs: true);
        ReadBool(ref settings.CastShadows, "bCastShadows");
        ReadBool(ref settings.CompositeShadowsFromDynamicLights, "bCompositeShadowsFromDynamicLights");
        ReadBool(ref settings.ForceCompositeAllLights, "bForceCompositeAllLights");
        ReadBool(ref settings.SynthesizeDirectionalLight, "bSynthesizeDirectionalLight");
        ReadBool(ref settings.SynthesizeSHLight, "bSynthesizeSHLight");
        ReadBool(ref settings.ForceAllowLightEnvSphericalHarmonicLights, "bForceAllowLightEnvSphericalHarmonicLights");
        ReadBool(ref settings.TraceFromClosestBoundsPoint, "bTraceFromClosestBoundsPoint");
        ReadBool(ref settings.IsCharacterLightEnvironment, "bIsCharacterLightEnvironment");
        ReadBool(ref settings.OverrideOwnerLightingChannels, "bOverrideOwnerLightingChannels");
        ReadBool(ref settings.ShadowBouncedLight, "bShadowBouncedLight");
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
        ReadFloat(ref settings.ShadowDistance, "ShadowDistance");
        settings.MinShadowResolution = props.GetProp<IntProperty>("MinShadowResolution")?.Value ?? 0;
        settings.MaxShadowResolution = props.GetProp<IntProperty>("MaxShadowResolution")?.Value ?? 0;
        settings.ShadowFadeResolution = props.GetProp<IntProperty>("ShadowFadeResolution")?.Value ?? 0;
        ReadFloat(ref settings.BouncedLightingFactor, "BouncedLightingFactor");
        ReadFloat(ref settings.BouncedLightingDesaturation, "BouncedLightingDesaturation");
        ReadFloat(ref settings.MinShadowAngle, "MinShadowAngle");
        settings.OverriddenLightingChannels = LightingChannels.FromProperty(props.GetProp<StructProperty>("OverriddenLightingChannels"), default, isInitialized: true, default);
        //LightShadow_Modulate is the default. LightShadow_Normal shadows from a light environment aren't supported
        settings.ModulatedShadows = props.GetProp<EnumProperty>("LightShadowMode") is null or { Value.Instanced: not "LightShadow_Normal" };
        if (settings.IsBio)
        {
            ReadVector(ref settings.KeyLightScale, "KeyLightScale");
            ReadVector(ref settings.AmbientLightScale, "AmbientLightScale");
            ReadFloat(ref settings.SHAmbientScale, "SHAmbientScale");
            ReadBool(ref settings.LightAxisEnabled, "LightAxisEnabled");
            settings.IsCinematicQuality = props.GetProp<EnumProperty>("QualityType") is { Value.Instanced: "DLEST_Cinematic" };
        }
        return settings;

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
}

/// <summary>
/// A primitive lit by a light environment. The light environment's bounds are those of its primitives, and its shadow is cast by all of them
/// </summary>
public sealed class LightEnvironmentPrimitive
{
    public Func<BoxSphereBounds> GetBounds;
    public Func<Matrix4x4> GetLocalToWorld;
    /// <summary>
    /// Draws the primitive's shadow-casting sections with the shaders the caller has set, binding its vertex buffer at slot 0. Null if it doesn't cast shadows
    /// </summary>
    public Action<DeviceContext> DrawShadowCaster;
    /// <summary>
    /// Whether the primitive is in the game's scene (its actor and component aren't hidden in game)
    /// </summary>
    public Func<bool> IsInScene = () => true;
    /// <summary>
    /// Whether the primitive is shown in the editor, so that it casts its shadow (its actor's category isn't hidden)
    /// </summary>
    public Func<bool> IsShown = () => true;
    public LightingChannels LightingChannels;
}

/// <summary>
/// The lights a light environment synthesizes for its primitives (FDynamicLightEnvironmentState::CreateEnvironmentLightList)
/// </summary>
public sealed class LightEnvironmentLighting
{
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
    };
}

/// <summary>
/// The light a light environment creates to cast its primitives' shadow: a point light of no brightness, whose shadow modulates the scene by <see cref="ModShadowColor"/>
/// </summary>
public sealed record LightEnvironmentShadowLight(Vector3 Position, float Radius, float MinShadowFalloffRadius, float ShadowFalloffExponent, Vector3 ModShadowColor,
    int MinShadowResolution, int MaxShadowResolution, int ShadowFadeResolution);

/// <summary>
/// UE3's light environment (DynamicLightEnvironmentComponent and its FDynamicLightEnvironmentState), as LE3 computes it once it has settled:
/// the level's lights that reach its primitives are composited into spherical harmonic lighting, from which it synthesizes a directional light,
/// an SH (or sky) light for the remainder, and a point light that casts the primitives' shadow.
/// LE3's BioDynamicLightEnvironmentComponent (FBioDynamicLightEnvironmentState) finds the directional light differently, and scales the lights.
/// Light probes, cinematic light rigs, and BioDynamicLightEnvironmentComponent's UseTargetBoneAsOrigin (which BioPawns set, to their Chest2 bone) aren't supported:
/// the origin is always the center of the primitives' bounds.
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

    /// <summary>
    /// WorldInfo's CharacterLightingContrastFactor and bAllowLightEnvSphericalHarmonicLights, from the level the light environment is in
    /// </summary>
    private readonly float CharacterLightingContrastFactor;
    private readonly bool AllowLightEnvSphericalHarmonicLights;

    //guarded by CacheLock, since primitives are added on the loading thread while the render thread reads them
    private readonly List<LightEnvironmentPrimitive> Primitives = [];
    private readonly object CacheLock = new();
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
            AddPrimitives(onlyInScene: true);
            if (bounds is null)
            {
                AddPrimitives(onlyInScene: false);
            }
        }
        if (Settings.OverrideOwnerLightingChannels)
        {
            channels = Settings.OverriddenLightingChannels.Channels;
        }
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
        var visibilityTester = new LightVisibilityTester(geometry, Owner, Settings, bounds);
        //UpdateStaticEnvironment and UpdateDynamicEnvironment: static lights are in the world's StaticLightList, others in its DynamicLightList.
        //(Dominant lights are in separate lists, which are only included if bForceCompositeAllLights)
        var staticEnvironment = new Environment();
        var dynamicEnvironment = new Environment();
        foreach (SceneLight light in lights)
        {
            //lights that belong to a light environment aren't in the world's light lists
            if (light.IsDominant && !Settings.ForceCompositeAllLights || light.HasLightEnvironment)
            {
                continue;
            }
            AddLightToEnvironment(light, ref (light.HasStaticLighting ? ref staticEnvironment : ref dynamicEnvironment), bounds, channels, visibilityTester);
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
        if (Settings.CompositeShadowsFromDynamicLights)
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
            RendersSHLightAfterModulatedShadows = Settings.CastShadows
        };
        float contrastFactor = Settings.IsCharacterLightEnvironment ? CharacterLightingContrastFactor : 1;
        bool useBioLightAxis = Settings.IsBio && !Settings.IsCinematicQuality;

        //the directional light
        if (Settings.SynthesizeDirectionalLight
            && (useBioLightAxis ? ExtractBioKeyLight(ref lightEnvironment, out Vector3 direction, out Vector3 color)
                : ExtractDominantLight(ref lightEnvironment, out direction, out color))
            && (color.X > 0 || color.Y > 0 || color.Z > 0))
        {
            lighting.DirectionToLight = direction;
            //the Bio key light was desaturated before it was extracted, so that what's extracted from the SH is what's lit with. It isn't desaturated twice
            lighting.DirectionalColor = (useBioLightAxis ? color : LinearColorUtils.Desaturate(color, Settings.LightDesaturation)) * contrastFactor;
        }

        //the SH or sky light, from what remains
        SHVectorRGB ambient = lightEnvironment + unshadowedEnvironment;
        if (useBioLightAxis)
        {
            ambient *= Settings.AmbientLightScale;
            ambient.R[0] *= Settings.SHAmbientScale;
            ambient.G[0] *= Settings.SHAmbientScale;
            ambient.B[0] *= Settings.SHAmbientScale;
        }
        ambient *= 1 / contrastFactor;
        if (Settings.SynthesizeSHLight && (AllowLightEnvSphericalHarmonicLights || Settings.ForceAllowLightEnvSphericalHarmonicLights))
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

        //the shadow
        if (Settings.CastShadows && Settings.ModulatedShadows && shadow.Direction.LengthSquared() > 1e-5f)
        {
            //how much light remains in the shadow, as a fraction of the total
            Vector3 modShadowColor = Vector3.Min(Vector3.One, (shadow.UnshadowedIntensity - shadow.ShadowedIntensity) / Vector3.Max(shadow.UnshadowedIntensity, new Vector3(1e-5f)));
            modShadowColor = Vector3.Min(modShadowColor, Settings.MaxModulatedShadowColor);
            //the light is kept from getting too low, so the shadow isn't too long
            float minZ = MathF.Cos(Math.Clamp(90 - Settings.MinShadowAngle, 0, 180) * MathF.PI / 180);
            Vector3 shadowDirection = Vector3.Normalize(shadow.Direction with { Z = MathF.Max(shadow.Direction.Z, minZ) });
            float radius = bounds.SphereRadius;
            lighting.ShadowLight = new LightEnvironmentShadowLight(
                bounds.Origin + shadowDirection * radius * Settings.LightDistance,
                (Settings.ShadowDistance + Settings.LightDistance + 2) * radius,
                (Settings.LightDistance + 1) * radius,
                ShadowFalloffExponent,
                modShadowColor,
                Settings.MinShadowResolution, Settings.MaxShadowResolution, Settings.ShadowFadeResolution);
        }
        return lighting;
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
            Vector3 bouncedIntensity = LinearColorUtils.Desaturate(directIntensity * light.BouncedLightColor, Settings.BouncedLightingDesaturation);
            //In UE3, a dynamic light with bAllowedToBypassLightEnvironments lights the primitives directly in its own pass, rather than being composited.
            //Light environments' primitives don't get light passes here, so it's composited like any other light rather than lost
            lightEnvironment += SHVectorRGB.FromColor(SHVector.BasisFunction(lightVector), directIntensity);
            //the light bounced back off the surface it shines on, from the opposite direction
            SHVectorRGB bouncedLighting = SHVectorRGB.FromColor(SHVector.BasisFunction(-lightVector), bouncedIntensity * Settings.BouncedLightingFactor);
            if (Settings.ShadowBouncedLight)
            {
                lightEnvironment += bouncedLighting;
            }
            else
            {
                environment.Unshadowed += bouncedLighting;
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
    /// FDynamicLightEnvironmentState::DoesLightAffectOwner. (Light functions and precomputed shadowing don't matter to light environments)
    /// </summary>
    private bool DoesLightAffectOwner(SceneLight light, BoxSphereBounds bounds, LightingChannels ownerChannels)
    {
        //a light's CompositeDynamic channel stands in for Dynamic: lights that only have Dynamic don't affect light environments
        uint lightChannels = light.LightingChannels.Channels & ~LightingChannels.DynamicBit;
        if ((light.LightingChannels.Channels & LightingChannels.CompositeDynamicBit) != 0)
        {
            lightChannels = (lightChannels & ~LightingChannels.CompositeDynamicBit) | LightingChannels.DynamicBit;
        }
        if ((lightChannels & ownerChannels.Channels) == 0)
        {
            return false;
        }
        //ULightComponent::AffectsLevel: a level whose lighting was built on its own only has its own static-shadowing lights,
        //and lights can be limited to their own level and some others
        IMEPackage level = (Owner ?? Export).FileRef;
        string levelName = level.FileNameNoExtension;
        bool isOtherLevel = !light.LevelName.Equals(levelName, StringComparison.OrdinalIgnoreCase);
        if (isOtherLevel && light.HasStaticShadowing && level.Flags.Has(UnrealFlags.EPackageFlags.SelfContainedLighting))
        {
            return false;
        }
        if (light.OnlyAffectSameAndSpecifiedLevels && isOtherLevel && !light.OtherLevelsToAffect.Contains(levelName))
        {
            return false;
        }
        //(UE3's AffectsBounds includes the light's inclusion and exclusion volumes)
        return light.AffectsBounds(bounds) && light.AffectsVolumes(bounds);
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

    /// <summary>
    /// FBioDynamicLightEnvironmentState's key light: the direction its luminance SH is highest (LightAxisEnabled), and the light from there, removed from <paramref name="sh"/>
    /// </summary>
    private bool ExtractBioKeyLight(ref SHVectorRGB sh, out Vector3 direction, out Vector3 color)
    {
        SHVector luminance = sh.GetLuminance();
        color = Vector3.Zero;
        if (Settings.LightAxisEnabled)
        {
            direction = FindMaximumDirection(luminance, out float maximum);
            if (maximum <= 0)
            {
                direction = Vector3.Zero;
            }
        }
        else
        {
            direction = new Vector3(-luminance[3], -luminance[1], MathF.Max(luminance[2], 0));
        }
        if (direction.LengthSquared() < 1e-5f)
        {
            return false;
        }
        direction = Vector3.Normalize(direction);
        SHVector basis = SHVector.BasisFunction(direction);
        color = LinearColorUtils.Desaturate(sh.Project(basis), Settings.LightDesaturation);
        if (MathF.Max(color.X, MathF.Max(color.Y, color.Z)) > 1e-8f)
        {
            sh.Extract(basis, color);
            color *= Settings.KeyLightScale;
        }
        return true;
    }

    /// <summary>
    /// The direction the SH function is highest, by gradient ascent from each of the six axes, as LE3's light axis search does
    /// </summary>
    private static Vector3 FindMaximumDirection(in SHVector function, out float maximum)
    {
        Vector3 bestDirection = Vector3.Zero;
        maximum = 0;
        ReadOnlySpan<Vector3> axes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        foreach (Vector3 axis in axes)
        {
            Vector3 direction = axis;
            float value = SHVector.BasisFunction(direction).Dot(function);
            for (int iteration = 0; iteration < 25; iteration++)
            {
                Vector3 gradient = Gradient(function, direction);
                //projected onto the sphere's tangent plane
                gradient -= direction * Vector3.Dot(gradient, direction);
                float step = 1;
                bool improved = false;
                for (int attempt = 0; attempt < 5; attempt++, step *= 0.5f)
                {
                    Vector3 delta = gradient * step;
                    if (delta.LengthSquared() < 1e-8f)
                    {
                        break;
                    }
                    Vector3 candidate = Vector3.Normalize(direction + delta);
                    float candidateValue = SHVector.BasisFunction(candidate).Dot(function);
                    if (candidateValue > value)
                    {
                        (direction, value, improved) = (candidate, candidateValue, true);
                        break;
                    }
                }
                if (!improved)
                {
                    break;
                }
            }
            if (value > maximum)
            {
                (maximum, bestDirection) = (value, direction);
            }
        }
        return bestDirection;
    }

    //The gradient of the SH function at a point on the sphere, treating the basis polynomials as functions of x, y and z
    private static Vector3 Gradient(in SHVector f, Vector3 v)
    {
        float x = v.X, y = v.Y, z = v.Z;
        return new Vector3(
            -0.48860299f * f[3] + 1.0925480f * y * f[4] - 1.0925480f * z * f[7] + 1.0925480f * x * f[8],
            -0.48860299f * f[1] + 1.0925480f * x * f[4] - 1.0925480f * z * f[5] - 1.0925480f * y * f[8],
            0.48860299f * f[2] - 1.0925480f * y * f[5] + 1.8923519f * z * f[6] - 1.0925480f * x * f[7]);
    }
}

/// <summary>
/// FDynamicLightEnvironmentState::IsLightVisible: traces from the light environment to each light, against the level's geometry
/// </summary>
internal sealed class LightVisibilityTester
{
    private readonly LevelGeometry Geometry;
    //the light environment's owner, whose meshes don't block its traces
    private readonly ExportEntry IgnoredActor;
    private readonly LightEnvironmentSettings Settings;
    private readonly BoxSphereBounds Bounds;
    private readonly Vector3[] SampleOffsets;

    public LightVisibilityTester(LevelGeometry geometry, ExportEntry owner, LightEnvironmentSettings settings, BoxSphereBounds bounds)
    {
        Geometry = geometry;
        IgnoredActor = owner;
        Settings = settings;
        Bounds = bounds;
        //UpdateOwner: random points within the bounds, and the center. The center is the only one by default
        int numSamples = Math.Max(settings.NumVolumeVisibilitySamples, 1);
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
            if (Settings.TraceFromClosestBoundsPoint)
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
            if (!Geometry.IsSegmentBlocked(start, end, light, IgnoredActor))
            {
                unblocked++;
            }
        }
        visibility = (float)unblocked / SampleOffsets.Length;
        return visibility > 0;
    }
}
