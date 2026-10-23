using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public enum SceneLightType
{
    Point,
    Spot,
    Directional,
    /// <summary>
    /// Hemispherical ambient light. It isn't rendered in its own pass: it's added to the sky lighting of the base pass (see <see cref="MeshStaticLighting.GetSkyLighting"/>)
    /// </summary>
    Sky,
}

/// <summary>
/// A level's light, for rendering its effect on static meshes with the game's light shaders (see <see cref="MeshStaticLighting"/>).
/// Mirrors the parts of UE3's FPointLightSceneInfo, FSpotLightSceneInfo, FDirectionalLightSceneInfo and FSkyLightSceneInfo the shaders use.
/// </summary>
public sealed class SceneLight
{
    public SceneLightType Type { get; }
    public ExportEntry Export { get; }

    /// <summary>
    /// Identifies the light in shadow maps
    /// </summary>
    public Guid LightGuid { get; }

    /// <summary>
    /// Identifies the light in light-maps
    /// </summary>
    public Guid LightmapGuid { get; }

    public Vector3 Position { get; }

    /// <summary>
    /// The direction a spot or directional light shines in
    /// </summary>
    public Vector3 Direction { get; }

    public float Radius { get; }
    public float FalloffExponent { get; }

    /// <summary>
    /// Linear color, scaled by brightness. For a sky light, the color from above
    /// </summary>
    public Vector3 Color { get; }

    /// <summary>
    /// For a sky light, the color from below. Linear, scaled by LowerBrightness
    /// </summary>
    public Vector3 LowerColor { get; }

    public float CosOuterCone { get; }
    public float SinOuterCone { get; }
    public float InvCosConeDifference { get; }

    /// <summary>
    /// For signed distance field shadow maps
    /// </summary>
    public float DistanceFieldShadowMapPenumbraSize { get; }
    public float DistanceFieldShadowMapShadowExponent { get; }

    /// <summary>
    /// Whether the light's shadows (and possibly its lighting) were precomputed. If so, primitives it isn't listed in the static lighting of aren't affected by it
    /// (unless they were never built with it). UE3's ULightComponent::HasStaticShadowing
    /// </summary>
    public bool HasStaticShadowing { get; }
    /// <summary>LE1's editor override: this light supplies baked indirect light, never a direct preview pass.</summary>
    public bool BakedGIOnly { get; }

    public LightingChannels LightingChannels { get; }

    /// <summary>
    /// The name of the package (level file) the light is in
    /// </summary>
    public string LevelName { get; }

    /// <summary>
    /// bOnlyAffectSameAndSpecifiedLevels: the light only affects primitives in its own level, and those in <see cref="OtherLevelsToAffect"/>
    /// </summary>
    public bool OnlyAffectSameAndSpecifiedLevels { get; }
    public HashSet<string> OtherLevelsToAffect { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// bUseVolumes: the light doesn't affect primitives that touch one of <see cref="ExclusionVolumes"/>,
    /// and if there are <see cref="InclusionVolumes"/>, only affects primitives that touch one of them
    /// </summary>
    public bool UseVolumes { get; }
    public ConvexVolume[] InclusionVolumes { get; } = [];
    public ConvexVolume[] ExclusionVolumes { get; } = [];

    /// <summary>
    /// The material of the light's light function, which modulates its brightness across space (see <see cref="LightAttenuationRenderer"/>). Null if it doesn't have one
    /// </summary>
    public ExportEntry LightFunctionMaterial { get; }

    /// <summary>
    /// The world size of one unit of the light function material's coordinates, along each of the light's axes
    /// </summary>
    public Vector3 LightFunctionScale { get; } = new(1024);

    /// <summary>
    /// Whether the light casts dynamic shadows (CastShadows and CastDynamicShadows), from the meshes whose shadows from it weren't precomputed
    /// (see <see cref="MeshStaticLighting.CastsDynamicShadowFrom"/>). Only normal (not modulated) shadows are supported
    /// </summary>
    public bool CastsDynamicShadows { get; }

    /// <summary>
    /// CastShadows and CastDynamicShadows before the shadow-mode filter. UE3 uses normal shadows for unbuilt static lights.
    /// </summary>
    public bool CastsDynamicShadowsWhenUnbuilt { get; }

    /// <summary>
    /// Overrides of the system settings' shadow resolutions. 0 means not overridden
    /// </summary>
    public int MinShadowResolution { get; }
    public int MaxShadowResolution { get; }
    public int ShadowFadeResolution { get; }

    /// <summary>
    /// For point and spot lights: how close the light can get to a shadow caster, relative to its radius, before the shadow's projection is moved back
    /// </summary>
    public float ShadowRadiusMultiplier { get; }

    /// <summary>
    /// Transforms world space into the light's space, without scaling. The light's +Z axis is the direction it shines in (its parent's X axis), and it's at the origin
    /// </summary>
    public Matrix4x4 WorldToLight { get; }

    /// <summary>
    /// ULightComponent::HasStaticLighting: its owner is static, it has no light function, and it isn't forced to be dynamic.
    /// Light environments composite these as static lights (the world's StaticLightList), and the others as dynamic lights
    /// </summary>
    public bool HasStaticLighting { get; private init; }

    /// <summary>ULightComponent.UseDirectLightMap: a static light's direct lighting goes in lightmaps rather than shadow maps (UseStaticLighting)</summary>
    public bool UseDirectLightMap { get; private init; }

    /// <summary>
    /// A dominant light. LE1 environments take the world's dominant directional; later games need bForceCompositeAllLights.
    /// </summary>
    public bool IsDominant { get; }

    /// <summary>
    /// CastShadows and CastStaticShadows: light environments test whether the light is blocked from them
    /// </summary>
    public bool CastsStaticShadows { get; }

    /// <summary>
    /// bCastCompositeShadow: light environments include the light in the shadow they cast
    /// </summary>
    public bool CastsCompositeShadow { get; }

    /// <summary>
    /// bAffectCompositeShadowDirection: the light's direction contributes to the direction of light environments' shadows, rather than it acting as ambient light
    /// </summary>
    public bool AffectsCompositeShadowDirection { get; }

    /// <summary>
    /// How much of the light remains in the shadows light environments cast. Linear color
    /// </summary>
    public Vector3 ModShadowColor { get; }

    /// <summary>
    /// LightEnv_BouncedModulationColor scaled by LightEnv_BouncedLightBrightness: sky bounce in LE2, every light's bounce in LE3.
    /// </summary>
    public Vector3 BouncedLightColor { get; } = Vector3.One;

    /// <summary>
    /// For directional lights: how far light environments trace towards them
    /// </summary>
    public float TraceDistance { get; }

    /// <summary>
    /// The light belongs to a light environment (its LightEnvironment is set). It only lights that light environment's primitives, directly, and isn't in the world's
    /// light lists, so it isn't composited into light environments (ULightComponent::Attach, AffectsPrimitive).
    /// Synthesized points are drawn through the environment's own list; package-owned environment lights aren't routed.
    /// </summary>
    public bool HasLightEnvironment { get; }

    /// <summary>LE1 permits this unowned light to affect primitives that belong to a light environment.</summary>
    public bool BypassesLightEnvironments { get; }

    /// <summary>bAllowedToBypassLightEnvironments on an unowned light; routing differs by game (<see cref="DynamicLightEnvironment"/>)</summary>
    public bool AllowedToBypassLightEnvironments { get; }

    /// <summary>For a synthesized point with a normal shadow: the environment whose primitives cast (and receive) it</summary>
    internal DynamicLightEnvironment ShadowSubject { get; }

    /// <summary>A point in its environment's draw list, never registered as a level light</summary>
    /// <param name="shadowSubject">Non-null if the point casts a normal shadow from the environment's primitives</param>
    internal static SceneLight CreateEnvironmentPoint(ExportEntry owner, Vector3 position, float radius, Vector3 color, float falloffExponent = 2,
        DynamicLightEnvironment shadowSubject = null, float shadowRadiusMultiplier = 1.1f, int minShadowResolution = 0, int maxShadowResolution = 0,
        int shadowFadeResolution = 0) =>
        new(owner, position, radius, color, falloffExponent, shadowSubject, shadowRadiusMultiplier, minShadowResolution, maxShadowResolution, shadowFadeResolution);

    private SceneLight(ExportEntry owner, Vector3 position, float radius, Vector3 color, float falloffExponent, DynamicLightEnvironment shadowSubject,
        float shadowRadiusMultiplier, int minShadowResolution, int maxShadowResolution, int shadowFadeResolution)
    {
        Type = SceneLightType.Point;
        Export = owner;
        HasLightEnvironment = true;
        Position = position;
        Radius = radius;
        Color = color;
        FalloffExponent = falloffExponent;
        Direction = Vector3.UnitZ;
        WorldToLight = Matrix4x4.CreateTranslation(-position);
        LevelName = owner?.FileRef.FileNameNoExtension ?? "";
        //A normal shadow attenuates only this point; modulated ones come from LightEnvironmentLighting
        ShadowSubject = shadowSubject;
        CastsDynamicShadows = CastsDynamicShadowsWhenUnbuilt = shadowSubject is not null;
        ShadowRadiusMultiplier = shadowRadiusMultiplier;
        MinShadowResolution = minShadowResolution;
        MaxShadowResolution = maxShadowResolution;
        ShadowFadeResolution = shadowFadeResolution;
    }

    private SceneLight(SceneLightType type, ExportEntry export, PropertyCollection props, Matrix4x4 parentToWorld, bool hasStaticShadowing,
        bool hasLightEnvironment, bool hasFunction, PackageCache packageCache)
    {
        Type = type;
        Export = export;
        HasStaticShadowing = hasStaticShadowing;
        BakedGIOnly = export.Game == MEGame.LE1 && props.GetProp<BoolProperty>("bBakedGIOnly") is { Value: true };
        IsDominant = export.ClassName.StartsWith("Dominant", StringComparison.Ordinal);
        //LightComponent's defaults: CastShadows, CastStaticShadows, bCastCompositeShadow, bAffectCompositeShadowDirection
        CastsStaticShadows = props.GetProp<BoolProperty>("CastShadows") is not { Value: false } && props.GetProp<BoolProperty>("CastStaticShadows") is not { Value: false };
        CastsCompositeShadow = props.GetProp<BoolProperty>("bCastCompositeShadow") is not { Value: false };
        AffectsCompositeShadowDirection = props.GetProp<BoolProperty>("bAffectCompositeShadowDirection") is not { Value: false };
        ModShadowColor = props.GetProp<StructProperty>("ModShadowColor") is { } modShadowColorProp ? CommonStructs.GetLinearColor(modShadowColorProp).ToVector3() : Vector3.Zero;
        if (export.Game.IsGame3() || export.Game is MEGame.LE2)
        {
            BouncedLightColor = (props.GetProp<StructProperty>("LightEnv_BouncedModulationColor") is { } bouncedColorProp ? ToLinear(bouncedColorProp) : Vector3.One)
                                * (props.GetProp<FloatProperty>("LightEnv_BouncedLightBrightness")?.Value ?? (export.Game is MEGame.LE2 ? 0 : 1));
        }
        TraceDistance = props.GetProp<FloatProperty>("TraceDistance")?.Value ?? 100000;
        HasLightEnvironment = hasLightEnvironment;
        AllowedToBypassLightEnvironments = !hasLightEnvironment && props.GetProp<BoolProperty>("bAllowedToBypassLightEnvironments") is { Value: true };
        BypassesLightEnvironments = export.Game is MEGame.LE1 && AllowedToBypassLightEnvironments;
        UseVolumes = props.GetProp<BoolProperty>("bUseVolumes") is { Value: true };
        if (UseVolumes)
        {
            //the volumes are in the component's binary
            LightComponent binary = export.GetBinaryData<LightComponent>();
            InclusionVolumes = binary.InclusionConvexVolumes ?? [];
            ExclusionVolumes = binary.ExclusionConvexVolumes ?? [];
        }
        LevelName = export.FileRef.FileNameNoExtension;
        OnlyAffectSameAndSpecifiedLevels = props.GetProp<BoolProperty>("bOnlyAffectSameAndSpecifiedLevels") is { Value: true };
        if (props.GetProp<ArrayProperty<NameProperty>>("OtherLevelsToAffect") is { } otherLevels)
        {
            foreach (NameProperty level in otherLevels)
            {
                OtherLevelsToAffect.Add(level.Value.Instanced);
            }
        }
        LightGuid = props.GetProp<StructProperty>("LightGuid") is { } lightGuid ? CommonStructs.GetGuid(lightGuid) : Guid.Empty;
        LightmapGuid = props.GetProp<StructProperty>("LightmapGuid") is { } lightmapGuid ? CommonStructs.GetGuid(lightmapGuid) : Guid.Empty;
        LightingChannels = LightingChannels.FromProperty(props.GetProp<StructProperty>("LightingChannels"), LightingChannels.LightDefault, isInitialized: true, LightingChannels.LightDefault);

        float brightness = props.GetProp<FloatProperty>("Brightness")?.Value ?? 1;
        Color = (props.GetProp<StructProperty>("LightColor") is { } colorProp ? ToLinear(colorProp) : Vector3.One) * brightness;
        //FSkyLightSceneInfo
        float lowerBrightness = props.GetProp<FloatProperty>("LowerBrightness")?.Value ?? 0;
        LowerColor = (props.GetProp<StructProperty>("LowerColor") is { } lowerColorProp ? ToLinear(lowerColorProp) : Vector3.One) * lowerBrightness;

        //UPointLightComponent::SetTransformedToWorld. Translation is relative to the parent
        Vector3 translation = props.GetProp<StructProperty>("Translation") is { } translationProp ? CommonStructs.GetVector3(translationProp) : Vector3.Zero;
        Position = Vector3.Transform(translation, parentToWorld);
        //lights shine along their parent's X axis (mirroring included)
        Direction = Vector3.Normalize(new Vector3(parentToWorld.M11, parentToWorld.M12, parentToWorld.M13));

        //ULightComponent::SetParentToWorld and UPointLightComponent::SetTransformedToWorld: the light's space is its parent's with X and Z swapped,
        //moved to the light's position (point and spot lights), and with scaling removed
        var swapXZ = new Matrix4x4(0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1);
        Matrix4x4 lightToWorld = type is SceneLightType.Point or SceneLightType.Spot
            ? swapXZ * Matrix4x4.CreateTranslation(translation) * parentToWorld
            : swapXZ * parentToWorld;
        lightToWorld = lightToWorld.WithoutScaling();
        WorldToLight = Matrix4x4.Invert(lightToWorld, out Matrix4x4 worldToLight) ? worldToLight : Matrix4x4.Identity;

        //LightComponent's defaults: CastShadows, CastDynamicShadows, LightShadow_Normal
        CastsDynamicShadowsWhenUnbuilt = props.GetProp<BoolProperty>("CastShadows") is not { Value: false }
                                        && props.GetProp<BoolProperty>("CastDynamicShadows") is not { Value: false };
        CastsDynamicShadows = CastsDynamicShadowsWhenUnbuilt
                              && props.GetProp<EnumProperty>("LightShadowMode") is null or { Value.Instanced: "LightShadow_Normal" };
        MinShadowResolution = props.GetProp<IntProperty>("MinShadowResolution")?.Value ?? 0;
        MaxShadowResolution = props.GetProp<IntProperty>("MaxShadowResolution")?.Value ?? 0;
        ShadowFadeResolution = props.GetProp<IntProperty>("ShadowFadeResolution")?.Value ?? 0;
        //PointLightComponent's default
        ShadowRadiusMultiplier = props.GetProp<FloatProperty>("ShadowRadiusMultiplier")?.Value ?? 1.1f;

        if (hasFunction && EffectiveLightingProperties.ResolveReference(export, "Function", packageCache, props) is { } lightFunction)
        {
            PropertyCollection lightFunctionProps = EffectiveLightingProperties.Read(lightFunction, packageCache);
            if (lightFunctionProps.GetProp<StructProperty>("Scale") is { } scaleProp)
            {
                LightFunctionScale = CommonStructs.GetVector3(scaleProp);
            }
            LightFunctionMaterial = EffectiveLightingProperties.ResolveReference(lightFunction, "SourceMaterial", packageCache);
        }
        Radius = props.GetProp<FloatProperty>("Radius")?.Value ?? 1024;
        FalloffExponent = props.GetProp<FloatProperty>("FalloffExponent")?.Value ?? 2;

        //FSpotLightSceneInfo
        float innerConeAngle = Math.Clamp(props.GetProp<FloatProperty>("InnerConeAngle")?.Value ?? 0, 0, 89) * MathF.PI / 180;
        float outerConeAngle = Math.Clamp((props.GetProp<FloatProperty>("OuterConeAngle")?.Value ?? 44) * MathF.PI / 180, innerConeAngle + 0.001f, 89 * MathF.PI / 180 + 0.001f);
        CosOuterCone = MathF.Cos(outerConeAngle);
        SinOuterCone = MathF.Sin(outerConeAngle);
        InvCosConeDifference = 1 / (MathF.Cos(innerConeAngle) - CosOuterCone);

        var lightmassSettings = props.GetProp<StructProperty>("LightmassSettings");
        if (type is SceneLightType.Directional)
        {
            //FDirectionalLightSceneInfo: the angle the sun covers in the sky determines the penumbra
            float lightSourceAngle = lightmassSettings?.GetProp<FloatProperty>("LightSourceAngle")?.Value ?? 3;
            DistanceFieldShadowMapPenumbraSize = Math.Clamp(lightSourceAngle / 3, 0.001f, 1);
        }
        else
        {
            //TPointLightSceneInfo: LightSourceRadius doesn't mean anything to distance field shadows, so it's converted to a penumbra size
            float lightSourceRadius = lightmassSettings?.GetProp<FloatProperty>("LightSourceRadius")?.Value ?? 0;
            DistanceFieldShadowMapPenumbraSize = Math.Clamp(lightSourceRadius / 100, 0.001f, 1);
        }
        DistanceFieldShadowMapShadowExponent = lightmassSettings?.GetProp<FloatProperty>("ShadowExponent")?.Value ?? 2;
    }

    /// <summary>
    /// The type of light a light component is. Null if it's not a supported type
    /// </summary>
    public static SceneLightType? GetLightType(ExportEntry lightComponent)
    {
        MEGame game = lightComponent.Game;
        if (GlobalUnrealObjectInfo.IsA(lightComponent.ClassName, "SpotLightComponent", game))
        {
            return SceneLightType.Spot;
        }
        if (GlobalUnrealObjectInfo.IsA(lightComponent.ClassName, "PointLightComponent", game))
        {
            return SceneLightType.Point;
        }
        //including DominantDirectionalLightComponent, whose dominant shadows aren't supported
        if (GlobalUnrealObjectInfo.IsA(lightComponent.ClassName, "DirectionalLightComponent", game))
        {
            return SceneLightType.Directional;
        }
        if (GlobalUnrealObjectInfo.IsA(lightComponent.ClassName, "SkyLightComponent", game))
        {
            return SceneLightType.Sky;
        }
        return null;
    }

    /// <summary>
    /// Reads a light component. Returns null if it's not a supported type of light, or it's disabled.
    /// </summary>
    /// <param name="ownerClass">The class of the actor that owns the light</param>
    /// <param name="ownerTransform">The current transform supplied by the owner. CachedParentToWorld is only a fallback when no owner transform is available.</param>
    /// <param name="overrides">Uncommitted editor values, which replace the component's (resolved) properties of the same names</param>
    public static SceneLight Create(ExportEntry lightComponent, string ownerClass, PackageCache packageCache, Matrix4x4? ownerTransform = null,
        IReadOnlyList<Property> overrides = null)
    {
        if (GetLightType(lightComponent) is not SceneLightType type)
        {
            return null;
        }
        var props = EffectiveLightingProperties.Read(lightComponent, packageCache);
        foreach (Property property in overrides ?? [])
        {
            props.AddOrReplaceProp(property);
        }
        if (props.GetProp<BoolProperty>("bEnabled") is { Value: false })
        {
            return null;
        }
        Matrix4x4 parentToWorld = Matrix4x4.Identity;
        // The cooked cache is stale after editing an actor or collection transform. Both preview and bake
        // load through this path, so use the current owner transform whenever one is available.
        if (ownerTransform is { } transform)
        {
            parentToWorld = transform;
        }
        else if (props.GetProp<StructProperty>("CachedParentToWorld") is { } parentToWorldProp)
        {
            parentToWorld = CommonStructs.GetMatrix(parentToWorldProp);
        }
        //sky lights are the same everywhere
        else if (type is not SceneLightType.Sky)
        {
            return null;
        }
        //each is an archetype and class default chain walk, so each is looked up once
        bool hasLightEnvironment = EffectiveLightingProperties.HasReference(lightComponent, "LightEnvironment", packageCache, props);
        bool hasFunction = EffectiveLightingProperties.HasReference(lightComponent, "Function", packageCache, props);
        return new SceneLight(type, lightComponent, props, parentToWorld, GetHasStaticShadowing(type, ownerClass, props, hasLightEnvironment, hasFunction),
            hasLightEnvironment, hasFunction, packageCache)
        {
            HasStaticLighting = GetHasStaticLighting(ownerClass, props, hasLightEnvironment, hasFunction),
            UseDirectLightMap = props.GetProp<BoolProperty>("UseDirectLightMap") is { Value: true }
        };
    }

    /// <summary>
    /// ULightComponent::HasStaticShadowing
    /// </summary>
    private static bool GetHasStaticShadowing(SceneLightType type, string ownerClass, PropertyCollection props, bool hasLightEnvironment, bool hasFunction)
    {
        if (props.GetProp<BoolProperty>("bForceDynamicLight") is { Value: true } || hasLightEnvironment)
        {
            return false;
        }
        //AActor::HasStaticShadowing: bStatic || (bNoDelete && !bMovable). Of the light actors, only the movable ones are neither.
        //Sky lights also need the static lighting conditions: a bStatic owner (not a toggleable one), and no light function
        return type is SceneLightType.Sky
            ? !ownerClass.Contains("Movable", StringComparison.OrdinalIgnoreCase) && !ownerClass.Contains("Toggleable", StringComparison.OrdinalIgnoreCase)
              && !hasFunction
            : !ownerClass.Contains("Movable", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ULightComponent::HasStaticLighting: a static owner (the light actors that aren't movable or toggleable), no light function, not forced to be dynamic, and no light environment
    /// </summary>
    private static bool GetHasStaticLighting(string ownerClass, PropertyCollection props, bool hasLightEnvironment, bool hasFunction) =>
        !ownerClass.Contains("Movable", StringComparison.OrdinalIgnoreCase) && !ownerClass.Contains("Toggleable", StringComparison.OrdinalIgnoreCase)
        && !hasFunction
        && props.GetProp<BoolProperty>("bForceDynamicLight") is not { Value: true }
        && !hasLightEnvironment;

    /// <summary>
    /// GetDirectIntensity: the light's linear color at a point, attenuated by its radius (and cone, for spot lights). Not for sky lights
    /// </summary>
    public Vector3 GetDirectIntensity(Vector3 point)
    {
        if (Type is SceneLightType.Directional or SceneLightType.Sky)
        {
            return Color;
        }
        if (Radius <= 0)
        {
            return Vector3.Zero;
        }
        //UPointLightComponent::GetDirectIntensity
        float distanceSquared = Vector3.DistanceSquared(Position, point) / (Radius * Radius);
        float falloff = MathF.Pow(MathF.Max(0, 1 - distanceSquared), FalloffExponent);
        if (Type is SceneLightType.Spot)
        {
            //USpotLightComponent::GetDirectIntensity
            Vector3 toPoint = point - Position;
            toPoint = toPoint.LengthSquared() > 1e-8f ? Vector3.Normalize(toPoint) : Vector3.Zero;
            float coneFalloff = Math.Clamp((Vector3.Dot(toPoint, Direction) - CosOuterCone) * InvCosConeDifference, 0, 1);
            falloff *= coneFalloff * coneFalloff;
        }
        return Color * falloff;
    }

    /// <summary>
    /// Reads the lights in a level's actors. (Stand-alone light actors, and StaticLightCollectionActors)
    /// </summary>
    public static List<SceneLight> LoadLevelLights(Level level, PackageCache packageCache) =>
        LoadLevelLights(level, packageCache, null);

    /// <param name="previewOverride">The level editor's uncommitted edits to a light component, if it has any</param>
    internal static List<SceneLight> LoadLevelLights(Level level, PackageCache packageCache, Action<ExportEntry> trackDependency,
        Func<ExportEntry, LightPreviewOverride> previewOverride = null)
    {
        IMEPackage pcc = level.Export.FileRef;
        var lights = new List<SceneLight>();
        foreach (int actorUIndex in level.Actors)
        {
            if (!pcc.TryGetUExport(actorUIndex, out ExportEntry actor))
            {
                continue;
            }
            try
            {
                if (actor.ClassName == "StaticLightCollectionActor")
                {
                    trackDependency?.Invoke(actor);
                    //StaticLightCollectionActor::UpdateComponentsInternal gives each component its stored transform
                    var collection = actor.GetBinaryData<StaticLightCollectionActor>();
                    for (int i = 0; i < collection.Components.Count; i++)
                    {
                        if (pcc.TryGetUExport(collection.Components[i], out ExportEntry lightComponent))
                        {
                            Matrix4x4? transform = i < collection.LocalToWorldTransforms.Count ? collection.LocalToWorldTransforms[i] : null;
                            TryAdd(lightComponent, actor.ClassName, transform);
                        }
                    }
                }
                else
                {
                    var lightReference = actor.GetProperty<ObjectProperty>("LightComponent");
                    // Remember even an empty/disabled light actor, so assigning or enabling its component refreshes it.
                    if (lightReference is not null || GlobalUnrealObjectInfo.IsA(actor.ClassName, "Light", pcc.Game))
                        trackDependency?.Invoke(actor);
                    if (lightReference?.ResolveToEntry(pcc) is ExportEntry lightComponent)
                        TryAdd(lightComponent, actor.ClassName, ActorUtils.GetLocalToWorld(actor));
                }
            }
            catch
            {
                //an actor that can't be read just has no lights
            }
        }
        return lights;

        void TryAdd(ExportEntry lightComponent, string ownerClass, Matrix4x4? ownerTransform)
        {
            trackDependency?.Invoke(lightComponent);
            try
            {
                LightPreviewOverride edit = previewOverride?.Invoke(lightComponent);
                if (Create(lightComponent, ownerClass, packageCache, edit?.OwnerTransform ?? ownerTransform, edit?.Properties) is { } light)
                {
                    lights.Add(light);
                }
            }
            catch
            {
                //a light that can't be read just isn't rendered
            }
        }
    }

    /// <summary>
    /// Whether the light reaches the bounds. UPointLightComponent::AffectsBounds and FSpotLightSceneInfo::AffectsBounds
    /// </summary>
    public bool AffectsBounds(BoxSphereBounds bounds)
    {
        //directional and sky lights reach everything
        if (Type is SceneLightType.Directional or SceneLightType.Sky)
        {
            return true;
        }
        if (Vector3.DistanceSquared(bounds.Origin, Position) > (Radius + bounds.SphereRadius) * (Radius + bounds.SphereRadius))
        {
            return false;
        }
        if (Type is SceneLightType.Spot)
        {
            //is the bounding sphere within the cone?
            Vector3 u = Position - bounds.SphereRadius / SinOuterCone * Direction;
            Vector3 d = bounds.Origin - u;
            float dsqr = Vector3.Dot(d, d);
            float e = Vector3.Dot(Direction, d);
            if (e > 0 && e * e >= dsqr * CosOuterCone * CosOuterCone)
            {
                d = bounds.Origin - Position;
                dsqr = Vector3.Dot(d, d);
                e = -Vector3.Dot(Direction, d);
                if (e > 0 && e * e >= dsqr * SinOuterCone * SinOuterCone)
                {
                    return dsqr <= bounds.SphereRadius * bounds.SphereRadius;
                }
                return true;
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// The bUseVolumes test of FLightSceneInfoCompact::AffectsPrimitive. Exclusion volumes take precedence
    /// </summary>
    public bool AffectsVolumes(BoxSphereBounds bounds) => GetVolumeExclusion(bounds) is LightEligibility.Affects;

    /// <summary>
    /// Why the bUseVolumes test excludes the bounds, or <see cref="LightEligibility.Affects"/> if it doesn't
    /// </summary>
    public LightEligibility GetVolumeExclusion(BoxSphereBounds bounds)
    {
        if (!UseVolumes)
        {
            return LightEligibility.Affects;
        }
        foreach (ConvexVolume volume in ExclusionVolumes)
        {
            if (volume.IntersectsBox(bounds))
            {
                return LightEligibility.ExclusionVolume;
            }
        }
        foreach (ConvexVolume volume in InclusionVolumes)
        {
            if (volume.IntersectsBox(bounds))
            {
                return LightEligibility.Affects;
            }
        }
        return InclusionVolumes.Length == 0 ? LightEligibility.Affects : LightEligibility.OutsideInclusionVolumes;
    }

    //FLinearColor(FColor) decodes with a 2.2 gamma
    private static Vector3 ToLinear(StructProperty colorProp)
    {
        static float Decode(ByteProperty b) => MathF.Pow((b?.Value ?? 255) / 255f, 2.2f);
        return new Vector3(Decode(colorProp.GetProp<ByteProperty>("R")), Decode(colorProp.GetProp<ByteProperty>("G")), Decode(colorProp.GetProp<ByteProperty>("B")));
    }
}

/// <summary>
/// The level editor's uncommitted edits to a light, which previews use in place of the saved values
/// </summary>
/// <param name="OwnerTransform">The edited owner (or collection) transform. Null if it isn't edited</param>
/// <param name="Properties">Edited component properties, which replace the resolved properties of the same names</param>
public sealed record LightPreviewOverride(Matrix4x4? OwnerTransform, IReadOnlyList<Property> Properties);

/// <summary>
/// UE3's FLightingChannelContainer: a light only affects primitives it shares a channel with
/// </summary>
public readonly record struct LightingChannels(uint Channels)
{
    /// <summary>
    /// The channels' property names, in bit order
    /// </summary>
    public static IReadOnlyList<string> Names => ChannelNames;

    private static readonly string[] ChannelNames =
    [
        "BSP", "Static", "Dynamic", "CompositeDynamic", "Skybox",
        "Unnamed_1", "Unnamed_2", "Unnamed_3", "Unnamed_4", "Unnamed_5", "Unnamed_6",
        "Cinematic_1", "Cinematic_2", "Cinematic_3", "Cinematic_4", "Cinematic_5", "Cinematic_6", "Cinematic_7", "Cinematic_8", "Cinematic_9", "Cinematic_10",
        "Gameplay_1", "Gameplay_2", "Gameplay_3", "Gameplay_4", "Crowd"
    ];

    /// <summary>
    /// Default__LightComponent's channels
    /// </summary>
    public static readonly LightingChannels LightDefault = FromNames("BSP", "Static", "Dynamic", "CompositeDynamic");

    /// <summary>
    /// UE3 initializes an unset primitive's channels to Static if it has static shadowing (as static meshes in levels do)
    /// </summary>
    public static readonly LightingChannels StaticPrimitiveDefault = FromNames("Static");

    /// <summary>
    /// UE3 initializes an unset primitive's channels to Dynamic if it doesn't have static shadowing, as light environments' primitives don't
    /// </summary>
    public static readonly LightingChannels DynamicPrimitiveDefault = FromNames("Dynamic");

    public static readonly uint DynamicBit = DynamicPrimitiveDefault.Channels;
    public static readonly uint CompositeDynamicBit = FromNames("CompositeDynamic").Channels;

    public bool OverlapsWith(LightingChannels other) => (Channels & other.Channels) != 0;

    private static LightingChannels FromNames(params string[] names)
    {
        uint channels = 0;
        foreach (string name in names)
        {
            channels |= 1u << Array.IndexOf(ChannelNames, name);
        }
        return new LightingChannels(channels);
    }

    /// <summary>
    /// Reads an object's LightingChannels after its inherited struct fields have been condensed
    /// </summary>
    /// <param name="baseChannels">The value at the root of the archetype chain</param>
    /// <param name="isInitialized">Whether <paramref name="baseChannels"/> is initialized (bInitialized)</param>
    /// <param name="uninitializedDefault">What UE3 sets the channels to if they're never initialized</param>
    internal static LightingChannels FromProperty(StructProperty lightingChannels, LightingChannels baseChannels, bool isInitialized, LightingChannels uninitializedDefault)
    {
        uint channels = baseChannels.Channels;
        if (lightingChannels is not null)
        {
            foreach (Property prop in lightingChannels.Properties)
            {
                if (prop is not BoolProperty boolProp)
                {
                    continue;
                }
                //matched by instanced name, since names like Unnamed_3 are stored as a name and a number
                string name = prop.Name.Instanced;
                if (name == "bInitialized")
                {
                    isInitialized = boolProp.Value;
                }
                else if (Array.IndexOf(ChannelNames, name) is int index and >= 0)
                {
                    channels = boolProp.Value ? channels | 1u << index : channels & ~(1u << index);
                }
            }
        }
        return isInitialized ? new LightingChannels(channels) : uninitializedDefault;
    }
}
