using LegendaryExplorerCore.Packages;
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
}

/// <summary>
/// A level's light, for rendering its effect on static meshes with the game's light shaders (see <see cref="MeshStaticLighting"/>).
/// Mirrors the parts of UE3's FPointLightSceneInfo and FSpotLightSceneInfo the shaders use.
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
    /// The direction a spot light shines in
    /// </summary>
    public Vector3 Direction { get; }

    public float Radius { get; }
    public float FalloffExponent { get; }

    /// <summary>
    /// Linear color, scaled by brightness
    /// </summary>
    public Vector3 Color { get; }

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

    public LightingChannels LightingChannels { get; }

    private SceneLight(SceneLightType type, ExportEntry export, PropertyCollection props, Matrix4x4 parentToWorld, bool hasStaticShadowing)
    {
        Type = type;
        Export = export;
        HasStaticShadowing = hasStaticShadowing;
        LightGuid = props.GetProp<StructProperty>("LightGuid") is { } lightGuid ? CommonStructs.GetGuid(lightGuid) : Guid.Empty;
        LightmapGuid = props.GetProp<StructProperty>("LightmapGuid") is { } lightmapGuid ? CommonStructs.GetGuid(lightmapGuid) : Guid.Empty;
        LightingChannels = LightingChannels.FromProperty(props.GetProp<StructProperty>("LightingChannels"), LightingChannels.LightDefault, isInitialized: true, LightingChannels.LightDefault);

        float brightness = props.GetProp<FloatProperty>("Brightness")?.Value ?? 1;
        Color = (props.GetProp<StructProperty>("LightColor") is { } colorProp ? ToLinear(colorProp) : Vector3.One) * brightness;

        //UPointLightComponent::SetTransformedToWorld. Translation is relative to the parent
        Vector3 translation = props.GetProp<StructProperty>("Translation") is { } translationProp ? CommonStructs.GetVector3(translationProp) : Vector3.Zero;
        Position = Vector3.Transform(translation, parentToWorld);
        //lights shine along their parent's X axis (mirroring included)
        Direction = Vector3.Normalize(new Vector3(parentToWorld.M11, parentToWorld.M12, parentToWorld.M13));
        Radius = props.GetProp<FloatProperty>("Radius")?.Value ?? 1024;
        FalloffExponent = props.GetProp<FloatProperty>("FalloffExponent")?.Value ?? 2;

        //FSpotLightSceneInfo
        float innerConeAngle = Math.Clamp(props.GetProp<FloatProperty>("InnerConeAngle")?.Value ?? 0, 0, 89) * MathF.PI / 180;
        float outerConeAngle = Math.Clamp((props.GetProp<FloatProperty>("OuterConeAngle")?.Value ?? 44) * MathF.PI / 180, innerConeAngle + 0.001f, 89 * MathF.PI / 180 + 0.001f);
        CosOuterCone = MathF.Cos(outerConeAngle);
        SinOuterCone = MathF.Sin(outerConeAngle);
        InvCosConeDifference = 1 / (MathF.Cos(innerConeAngle) - CosOuterCone);

        //TPointLightSceneInfo: LightSourceRadius doesn't mean anything to distance field shadows, so it's converted to a penumbra size
        var lightmassSettings = props.GetProp<StructProperty>("LightmassSettings");
        float lightSourceRadius = lightmassSettings?.GetProp<FloatProperty>("LightSourceRadius")?.Value ?? 0;
        DistanceFieldShadowMapPenumbraSize = Math.Clamp(lightSourceRadius / 100, 0.001f, 1);
        DistanceFieldShadowMapShadowExponent = lightmassSettings?.GetProp<FloatProperty>("ShadowExponent")?.Value ?? 2;
    }

    /// <summary>
    /// Reads a light component. Returns null if it's not a supported type of light, or it's disabled.
    /// </summary>
    /// <param name="ownerClass">The class of the actor that owns the light</param>
    public static SceneLight Create(ExportEntry lightComponent, string ownerClass, PackageCache packageCache)
    {
        MEGame game = lightComponent.Game;
        SceneLightType type;
        if (GlobalUnrealObjectInfo.IsA(lightComponent.ClassName, "SpotLightComponent", game))
        {
            type = SceneLightType.Spot;
        }
        else if (GlobalUnrealObjectInfo.IsA(lightComponent.ClassName, "PointLightComponent", game))
        {
            type = SceneLightType.Point;
        }
        else
        {
            return null;
        }
        var props = lightComponent.GetCondensedProperties(packageCache, resolveImports: true, mergeStructs: true);
        if (props.GetProp<BoolProperty>("bEnabled") is { Value: false }
            || props.GetProp<StructProperty>("CachedParentToWorld") is not { } parentToWorldProp)
        {
            return null;
        }
        Matrix4x4 parentToWorld = CommonStructs.GetMatrix(parentToWorldProp);
        //AActor::HasStaticShadowing: bStatic || (bNoDelete && !bMovable). Of the light actors, only the movable ones are neither
        bool hasStaticShadowing = !ownerClass.Contains("Movable", StringComparison.OrdinalIgnoreCase)
                                  && props.GetProp<BoolProperty>("bForceDynamicLight") is not { Value: true };
        return new SceneLight(type, lightComponent, props, parentToWorld, hasStaticShadowing);
    }

    /// <summary>
    /// Reads the lights in a level's actors. (Stand-alone light actors, and StaticLightCollectionActors)
    /// </summary>
    public static List<SceneLight> LoadLevelLights(Level level, PackageCache packageCache)
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
                    foreach (int componentUIndex in actor.GetBinaryData<StaticLightCollectionActor>().Components)
                    {
                        if (pcc.TryGetUExport(componentUIndex, out ExportEntry lightComponent) && Create(lightComponent, actor.ClassName, packageCache) is { } light)
                        {
                            lights.Add(light);
                        }
                    }
                }
                else if (actor.GetProperty<ObjectProperty>("LightComponent")?.ResolveToEntry(pcc) is ExportEntry lightComponent
                         && Create(lightComponent, actor.ClassName, packageCache) is { } light)
                {
                    lights.Add(light);
                }
            }
            catch
            {
                //a light that can't be read just isn't rendered
            }
        }
        return lights;
    }

    /// <summary>
    /// Whether the light reaches the bounds. UPointLightComponent::AffectsBounds and FSpotLightSceneInfo::AffectsBounds
    /// </summary>
    public bool AffectsBounds(BoxSphereBounds bounds)
    {
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

    //FLinearColor(FColor) decodes with a 2.2 gamma
    private static Vector3 ToLinear(StructProperty colorProp)
    {
        static float Decode(ByteProperty b) => MathF.Pow((b?.Value ?? 255) / 255f, 2.2f);
        return new Vector3(Decode(colorProp.GetProp<ByteProperty>("R")), Decode(colorProp.GetProp<ByteProperty>("G")), Decode(colorProp.GetProp<ByteProperty>("B")));
    }
}

/// <summary>
/// UE3's FLightingChannelContainer: a light only affects primitives it shares a channel with
/// </summary>
public readonly record struct LightingChannels(uint Channels)
{
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
