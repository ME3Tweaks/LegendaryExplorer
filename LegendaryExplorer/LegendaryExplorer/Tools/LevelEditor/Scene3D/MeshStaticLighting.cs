using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// How a light's shadows on a primitive were precomputed. Determines which of the game's light shaders render it (the light's shadowing policy)
/// </summary>
public enum StaticShadowingType
{
    /// <summary>
    /// Not precomputed: the light is fully dynamic for this primitive. (FNoStaticShadowingPolicy)
    /// </summary>
    None,
    /// <summary>
    /// A texture of how much of the light reaches each texel (FShadowTexturePolicy)
    /// </summary>
    ShadowTexture,
    /// <summary>
    /// A texture of the distance to the shadow's edge (FSignedDistanceFieldShadowTexturePolicy)
    /// </summary>
    DistanceFieldShadowTexture,
    /// <summary>
    /// How much of the light reaches each vertex (FShadowVertexBufferPolicy)
    /// </summary>
    ShadowVertexBuffer,
}

/// <summary>
/// A light that's rendered on a primitive in its own pass, and how its shadows were precomputed. (UE3's FLightInteraction, minus the light-mapped and irrelevant kinds)
/// </summary>
/// <param name="ShadowMap">For <see cref="StaticShadowingType.ShadowTexture"/> and <see cref="StaticShadowingType.DistanceFieldShadowTexture"/></param>
/// <param name="ShadowVertexBuffer">For <see cref="StaticShadowingType.ShadowVertexBuffer"/>. Bound at <see cref="MeshStaticLighting.ShadowVertexStreamSlot"/></param>
public readonly record struct LightInteraction(SceneLight Light, StaticShadowingType Shadowing, MeshStaticLighting.ShadowMapTexture ShadowMap, Buffer ShadowVertexBuffer);

/// <summary>
/// Hemispherical ambient light from the level's sky lights, in linear color: <paramref name="Upper"/> from above (world +Z), <paramref name="Lower"/> from below
/// </summary>
public readonly record struct SkyLighting(Vector3 Upper, Vector3 Lower)
{
    public bool IsBlack => Upper == Vector3.Zero && Lower == Vector3.Zero;
}

/// <summary>
/// The precomputed lighting of one LOD of a static mesh component, in the form the game's shaders read it:
/// <list type="bullet">
/// <item>Its light-map, the baked lighting the base pass renders. Texture light-maps are atlases sampled with the mesh's light-map UVs; vertex light-maps have one sample per vertex.</item>
/// <item>Its shadow maps: the precomputed shadows of lights that are rendered dynamically, one pass per light (see <see cref="GetLightInteractions"/>).</item>
/// </list>
/// </summary>
public sealed class MeshStaticLighting : IDisposable
{
    /// <summary>
    /// The second vertex stream: the light-map UV (texture light-maps and shadow maps) and the light-map samples (vertex light-maps).
    /// Matches FLocalVertexFactory's LightMapCoordinate (COLOR0), LightMapA (TEXCOORD5) and LightMapB (TEXCOORD6) inputs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct StaticLightingVertex
    {
        public Vector2 LightMapCoordinate;
        //In the order the game stores them (B, G, R, A), read as B8G8R8A8 like the game's D3DCOLOR streams
        public uint SampleA;
        public uint SampleB;
    }

    public const int VertexStreamSlot = 1;
    public static readonly int VertexStreamStride = Marshal.SizeOf<StaticLightingVertex>();

    /// <summary>
    /// The third vertex stream, for a light with a <see cref="StaticShadowingType.ShadowVertexBuffer"/>: one float per vertex
    /// </summary>
    public const int ShadowVertexStreamSlot = 2;
    public const int ShadowVertexStreamStride = sizeof(float);

    public static readonly InputElement[] InputElements =
    [
        new InputElement("COLOR", 0, Format.R32G32_Float, 0, VertexStreamSlot),
        new InputElement("TEXCOORD", 5, Format.B8G8R8A8_UNorm, 8, VertexStreamSlot),
        new InputElement("TEXCOORD", 6, Format.B8G8R8A8_UNorm, 12, VertexStreamSlot),
        new InputElement("BLENDWEIGHT", 0, Format.R32_Float, 0, ShadowVertexStreamSlot),
    ];

    /// <summary>
    /// LMT_None if the mesh has no light-map (or it can't be used)
    /// </summary>
    public ELightMapType LightMapType { get; private set; }

    /// <summary>
    /// For texture light-maps
    /// </summary>
    public PreviewTextureCache.TextureEntry[] Textures { get; private set; } = [];

    /// <summary>
    /// For texture light-maps: where this component's light-map is in the atlas. (CoordinateScale.X, CoordinateScale.Y, CoordinateBias.Y, CoordinateBias.X), as UE3 passes it
    /// </summary>
    public Vector4 CoordinateScaleBias { get; private set; }

    /// <summary>
    /// Decodes the light-map: sample * Scale + Bias. One per texture, or per sample coefficient for vertex light-maps.
    /// </summary>
    public Vector4[] Scales { get; private set; } = [];

    /// <summary>
    /// Only used by BioWare's light-map types (LMT_4 and LMT_6)
    /// </summary>
    public Vector4[] Biases { get; private set; } = [];

    /// <summary>
    /// Bound at <see cref="VertexStreamSlot"/>, with <see cref="VertexStreamStride"/>. Null if the mesh has neither a light-map UV channel nor a vertex light-map
    /// </summary>
    public Buffer VertexStream { get; private set; }

    public bool IsVertexLightMap => LightMapType is ELightMapType.LMT_1D or ELightMapType.LMT_3 or ELightMapType.LMT_5;

    /// <summary>
    /// A precomputed shadow texture for one light
    /// </summary>
    /// <param name="CoordinateScaleBias">Where this component's shadows are in the texture, in the same form as <see cref="MeshStaticLighting.CoordinateScaleBias"/></param>
    public sealed record ShadowMapTexture(Guid LightGuid, PreviewTextureCache.TextureEntry Texture, Vector4 CoordinateScaleBias, bool IsDistanceField);

    private readonly record struct ShadowVertexBufferEntry(Guid LightGuid, Buffer Buffer);

    private readonly List<ShadowMapTexture> ShadowMaps = [];
    private readonly List<ShadowVertexBufferEntry> ShadowVertexBuffers = [];
    //lights baked into the light-map, by their LightmapGuid
    private readonly HashSet<Guid> LightMapLightGuids = [];
    //lights that the lighting build found don't reach this primitive
    private readonly HashSet<Guid> IrrelevantLights = [];
    private LightingChannels LightingChannels;
    //bAcceptsLights: if false, no light affects the primitive
    private bool AcceptsLights = true;
    //bAcceptsDynamicLights: if false, only lights with static shadowing affect the primitive
    private bool AcceptsDynamicLights = true;
    //the level (package) the primitive is in, and whether its lighting was built without other levels' lights (PKG_SelfContainedLighting)
    private string LevelName;
    private bool HasSelfContainedLighting;

    /// <summary>
    /// The component is lit by a light environment (as dynamic actors are), which the level's lights don't affect directly
    /// </summary>
    public bool UsesLightEnvironment { get; private set; }

    /// <summary>
    /// The light environment that lights the component, if <see cref="UsesLightEnvironment"/>. Null if light environments aren't supported for the game
    /// </summary>
    public DynamicLightEnvironment LightEnvironment { get; internal set; }

    /// <summary>
    /// Whether the component casts shadows (CastShadow)
    /// </summary>
    private bool CastsShadow;

    /// <summary>
    /// The actor that owns the component. Null if its outer isn't an export
    /// </summary>
    internal ExportEntry OwnerActor { get; private set; }

    /// <summary>
    /// The triangles the mesh blocks line checks with, shared with the other instances of the mesh. Set by the mesh's component, if light environments are supported for the game
    /// </summary>
    internal LevelGeometry.TriangleMesh GeometryMesh;

    /// <summary>
    /// Whether the mesh is in the game's scene: its actor and component aren't hidden in game. Set by the mesh's component
    /// </summary>
    internal Func<bool> IsInScene = () => true;

    /// <summary>
    /// Whether the mesh is shown in the editor, so that it casts dynamic shadows (its actor's category isn't hidden). Set by the mesh's component
    /// </summary>
    internal Func<bool> IsShown = () => true;

    /// <summary>
    /// Whether the mesh can block light environments' visibility traces, whichever light they're to. Those traces are TRACE_ShadowCast line checks,
    /// which only consider primitives that CastShadow and have static shadowing, and that the light affects (see <see cref="BlocksVisibilityTraceTo"/>).
    /// Hidden primitives still block them
    /// </summary>
    internal bool CanBlockVisibilityTraces => CastsShadow && HasStaticShadowing && AcceptsLights && !UsesLightEnvironment && GetBounds is not null;

    /// <summary>
    /// Whether the mesh blocks a light environment's visibility trace to <paramref name="light"/>: the light affects it (ULightComponent::AffectsPrimitive, ignoring
    /// lighting channels). It has no light environment, so the light mustn't either, and it must accept lights
    /// </summary>
    internal bool BlocksVisibilityTraceTo(SceneLight light) => CanBlockVisibilityTraces && AffectsPrimitive(light, GetBounds(), compareLightingChannels: false);

    /// <summary>
    /// CastShadow and bCastDynamicShadow, including inherited values: whether the mesh casts dynamic shadows, if something renders them (a light, or its light environment)
    /// </summary>
    internal bool CastsDynamicShadow { get; private set; }

    private readonly MeshRenderContext Context;

    /// <summary>
    /// The materials of the mesh this lighting is for. Set by the mesh's <see cref="ModelPreview{TVertex}"/>, so that their light shaders can be loaded ahead of time
    /// </summary>
    internal IEnumerable<MaterialRenderProxy> Materials = [];
    internal Func<BoxSphereBounds> GetBounds;

    /// <summary>
    /// Whether the mesh can cast dynamic (projected) shadows from the lights that reach it: it casts dynamic shadows, and isn't lit by a light environment
    /// (which casts its own). Which lights it casts them from is <see cref="CastsDynamicShadowFrom"/>; see <see cref="LightAttenuationRenderer"/>
    /// </summary>
    public bool IsDynamicShadowCaster { get; private set; }

    /// <summary>
    /// FPrimitiveSceneInfo's bStaticShadowing (bUsePrecomputedShadows, which level static meshes have): the shadows of static-shadowing lights are precomputed for it
    /// </summary>
    private bool HasStaticShadowing;

    /// <summary>
    /// FLightPrimitiveInteraction's bCastShadow, for a light that reaches the mesh: a mesh with precomputed shadows only casts dynamic shadows from lights
    /// without static shadowing (whose shadows weren't precomputed). Otherwise it casts them from any light that casts dynamic shadows
    /// </summary>
    public bool CastsDynamicShadowFrom(SceneLight light) => IsDynamicShadowCaster && light.CastsDynamicShadows && (!HasStaticShadowing || !light.HasStaticShadowing);

    /// <summary>
    /// Draws the mesh's shadow-casting sections with the shaders the caller has set, binding its vertex buffer at slot 0. Set by the mesh's <see cref="ModelPreview{TVertex}"/>
    /// </summary>
    internal Action<SharpDX.Direct3D11.DeviceContext> DrawShadowCaster;
    internal Func<Matrix4x4> GetLocalToWorld;

    private readonly object InteractionsLock = new();
    private LightInteraction[] Interactions = [];
    private SkyLighting SkyLight;
    private int InteractionsLightsVersion = -1;
    private BoxSphereBounds InteractionsBounds;

    private MeshStaticLighting(MeshRenderContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Reads a static mesh component's precomputed lighting for one LOD. Returns null if that isn't possible
    /// </summary>
    public static MeshStaticLighting Create(MeshRenderContext context, ExportEntry componentExport, StaticMesh mesh, int lod)
    {
        if (!componentExport.Game.IsLEGame() || lod >= mesh.LODModels.Length)
        {
            return null;
        }
        StaticMeshComponent component;
        try
        {
            component = componentExport.GetBinaryData<StaticMeshComponent>();
        }
        catch
        {
            return null;
        }
        var staticLighting = new MeshStaticLighting(context)
        {
            OwnerActor = componentExport.Parent as ExportEntry
        };
        PropertyCollection props = componentExport.GetProperties();
        if (props.GetProp<ArrayProperty<StructProperty>>("IrrelevantLights") is { } irrelevantLights)
        {
            foreach (StructProperty guidProp in irrelevantLights)
            {
                staticLighting.IrrelevantLights.Add(CommonStructs.GetGuid(guidProp));
            }
        }
        var condensedProps = componentExport.GetCondensedProperties(context.PackageCache, resolveImports: true, mergeStructs: true);
        //A disabled light environment (as InterpActors' are by default) isn't used, so the primitive is lit like any other
        staticLighting.UsesLightEnvironment = context.ResolveLightEnvironment(componentExport, condensedProps, staticLighting.OwnerActor,
            out DynamicLightEnvironment lightEnvironment);
        staticLighting.LightEnvironment = lightEnvironment;
        staticLighting.CastsShadow = condensedProps.GetProp<BoolProperty>("CastShadow") is not { Value: false };
        //Static meshes in levels have static shadowing, so UE3 initializes unset channels to Static. Light environments' primitives don't, so they get Dynamic
        staticLighting.LightingChannels = LightingChannels.FromProperty(condensedProps.GetProp<StructProperty>("LightingChannels"), default, isInitialized: false,
            staticLighting.UsesLightEnvironment ? LightingChannels.DynamicPrimitiveDefault : LightingChannels.StaticPrimitiveDefault);
        //StaticMeshComponent's archetypes set bAcceptsLights. Only an explicit false is trusted, in case the archetype chain couldn't be resolved
        staticLighting.AcceptsLights = condensedProps.GetProp<BoolProperty>("bAcceptsLights") is not { Value: false };
        staticLighting.AcceptsDynamicLights = condensedProps.GetProp<BoolProperty>("bAcceptsDynamicLights") is not { Value: false };
        staticLighting.LevelName = componentExport.FileRef.FileNameNoExtension;
        staticLighting.HasSelfContainedLighting = componentExport.FileRef.Flags.Has(UnrealFlags.EPackageFlags.SelfContainedLighting);
        //FPrimitiveSceneInfo's bStaticShadowing (bUsePrecomputedShadows) and bCastDynamicShadow (CastShadow && bCastDynamicShadow, which MeshComponent defaults to true).
        //StaticMeshActor's component template sets bUsePrecomputedShadows. If it isn't found (in case the archetype chain couldn't be resolved),
        //a mesh with any precomputed lighting, or in a StaticMeshCollectionActor, is taken to have it
        bool usesPrecomputedShadows = condensedProps.GetProp<BoolProperty>("bUsePrecomputedShadows")?.Value
                                      ?? (componentExport.Parent?.ClassName == "StaticMeshCollectionActor"
                                          || component.LODData.Any(lodData => lodData.LightMap is { LightMapType: not ELightMapType.LMT_None }
                                                                              || lodData.ShadowMaps.Length > 0 || lodData.ShadowVertexBuffers.Length > 0));
        staticLighting.HasStaticShadowing = usesPrecomputedShadows;
        staticLighting.CastsDynamicShadow = staticLighting.CastsShadow && condensedProps.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false };
        staticLighting.IsDynamicShadowCaster = staticLighting.CastsDynamicShadow
                                               //shadow groups (a shadow parent's children cast with it) aren't supported
                                               && condensedProps.GetProp<ObjectProperty>("ShadowParent") is not { Value: not 0 }
                                               && !staticLighting.UsesLightEnvironment;
        if (lod < component.LODData.Length)
        {
            staticLighting.Load(component.LODData[lod], componentExport.FileRef, mesh, lod);
        }
        context.RegisterStaticLighting(staticLighting);
        return staticLighting;
    }

    private void Load(StaticMeshComponentLODInfo lodInfo, IMEPackage pcc, StaticMesh mesh, int lod)
    {
        StaticMeshRenderData lodModel = mesh.LODModels[lod];
        int numVertices = (int)lodModel.NumVertices;

        uint[] samplesA = null;
        uint[] samplesB = null;
        Guid[] lightGuids = [];
        switch (lodInfo.LightMap)
        {
            case LightMap_1D lm1D:
                lightGuids = lm1D.LightGuids;
                samplesA = Array.ConvertAll(lm1D.DirectionalSamples, s => PackColor(s.Coefficient2));
                samplesB = Array.ConvertAll(lm1D.DirectionalSamples, s => PackColor(s.Coefficient3));
                Scales = [ToVector4(lm1D.ScaleVector1), ToVector4(lm1D.ScaleVector2)];
                break;
            case LightMap_3 lm3:
                lightGuids = lm3.LightGuids;
                samplesA = Array.ConvertAll(lm3.DirectionalSamples, s => PackColor(s.Coefficient2));
                samplesB = Array.ConvertAll(lm3.DirectionalSamples, s => PackColor(s.Coefficient3));
                Scales = [ToVector4(lm3.unkVector1), ToVector4(lm3.unkVector2)];
                break;
            case LightMap_5 lm5:
                lightGuids = lm5.LightGuids;
                samplesA = Array.ConvertAll(lm5.SimpleSamples, s => PackColor(s.Coefficient));
                Scales = [ToVector4(lm5.unkVector)];
                break;
            case LightMap_2D lm2D:
            {
                lightGuids = lm2D.LightGuids;
                //The two directional coefficient textures. The third texture slot holds the simple light-map, which the directional shaders don't use
                if (LoadPackageTexture(pcc, lm2D.Texture1) is { } texture1 && LoadPackageTexture(pcc, lm2D.Texture2) is { } texture2)
                {
                    Textures = [texture1, texture2];
                    Scales = [ToVector4(lm2D.ScaleVector1), ToVector4(lm2D.ScaleVector2)];
                    CoordinateScaleBias = ToScaleBias(lm2D.CoordinateScale, lm2D.CoordinateBias);
                    LightMapType = lm2D.LightMapType;
                }
                break;
            }
            case LightMap_4or6 lm4or6:
            {
                lightGuids = lm4or6.LightGuids;
                //LMT_4: color, then world-space direction. LMT_6: just color
                int numTextures = lm4or6.LightMapType is ELightMapType.LMT_4 ? 2 : 1;
                var textures = new PreviewTextureCache.TextureEntry[numTextures];
                var scales = new Vector4[numTextures];
                var biases = new Vector4[numTextures];
                bool loaded = true;
                for (int i = 0; i < numTextures; i++)
                {
                    (int texUIndex, Fixed8<float> scaleBias) = i == 0 ? (lm4or6.Texture1, lm4or6.unkFloats1) : (lm4or6.Texture2, lm4or6.unkFloats2);
                    textures[i] = LoadPackageTexture(pcc, texUIndex);
                    loaded &= textures[i] is not null;
                    scales[i] = new Vector4(scaleBias[0], scaleBias[1], scaleBias[2], scaleBias[3]);
                    biases[i] = new Vector4(scaleBias[4], scaleBias[5], scaleBias[6], scaleBias[7]);
                }
                if (loaded)
                {
                    (Textures, Scales, Biases) = (textures, scales, biases);
                    CoordinateScaleBias = ToScaleBias(lm4or6.CoordinateScale, lm4or6.CoordinateBias);
                    LightMapType = lm4or6.LightMapType;
                }
                break;
            }
        }
        //UE3 doesn't use a vertex light-map whose sample count doesn't match the mesh
        if (samplesA is not null && samplesA.Length == numVertices)
        {
            LightMapType = lodInfo.LightMap.LightMapType;
        }
        else
        {
            samplesA = samplesB = null;
        }
        //A light-map that can't be used is ignored entirely, so the lights in it are rendered dynamically instead, as UE3 would do for an unbuilt mesh
        if (LightMapType is not ELightMapType.LMT_None)
        {
            LightMapLightGuids.UnionWith(lightGuids);
        }

        foreach (int shadowMapUIndex in lodInfo.ShadowMaps)
        {
            if (pcc.TryGetUExport(shadowMapUIndex, out ExportEntry shadowMapExport))
            {
                PropertyCollection props = shadowMapExport.GetProperties();
                if (props.GetProp<ObjectProperty>("Texture") is { } textureProp && LoadPackageTexture(pcc, textureProp.Value) is { } texture
                    && props.GetProp<StructProperty>("LightGuid") is { } lightGuidProp)
                {
                    Vector2 scale = props.GetProp<StructProperty>("CoordinateScale") is { } scaleProp ? CommonStructs.GetVector2(scaleProp) : Vector2.One;
                    Vector2 bias = props.GetProp<StructProperty>("CoordinateBias") is { } biasProp ? CommonStructs.GetVector2(biasProp) : Vector2.Zero;
                    //Default__ShadowMap2D has bIsShadowFactorTexture = true. Signed distance field shadow maps set it to false
                    bool isDistanceField = props.GetProp<BoolProperty>("bIsShadowFactorTexture") is { Value: false };
                    ShadowMaps.Add(new ShadowMapTexture(CommonStructs.GetGuid(lightGuidProp), texture, ToScaleBias(scale, bias), isDistanceField));
                }
            }
        }
        foreach (int shadowVertexBufferUIndex in lodInfo.ShadowVertexBuffers)
        {
            if (pcc.TryGetUExport(shadowVertexBufferUIndex, out ExportEntry shadowMap1DExport))
            {
                ShadowMap1D shadowMap1D = shadowMap1DExport.GetBinaryData<ShadowMap1D>();
                //Samples are floats. UE3 doesn't use a shadow vertex buffer whose sample count doesn't match the mesh
                if (shadowMap1D.Samples.Length == numVertices && numVertices > 0)
                {
                    float[] samples = Array.ConvertAll(shadowMap1D.Samples, BitConverter.Int32BitsToSingle);
                    ShadowVertexBuffers.Add(new ShadowVertexBufferEntry(shadowMap1D.LightGuid, Buffer.Create(Context.Device, BindFlags.VertexBuffer, samples)));
                }
            }
        }

        //Like the game's vertex factory, light-map UVs are only available if the mesh has that UV channel
        int lightMapCoordinateIndex = mesh.Export.GetProperty<IntProperty>("LightMapCoordinateIndex")?.Value ?? 0;
        StaticMeshVertexBuffer vertexBuffer = lodModel.VertexBuffer;
        bool hasLightMapCoordinates = lightMapCoordinateIndex >= 0 && lightMapCoordinateIndex < vertexBuffer.NumTexCoords;
        if (!hasLightMapCoordinates)
        {
            if (!IsVertexLightMap)
            {
                LightMapType = ELightMapType.LMT_None;
                LightMapLightGuids.Clear();
            }
            ShadowMaps.Clear();
        }
        if (numVertices > 0 && (hasLightMapCoordinates || samplesA is not null))
        {
            var vertices = new StaticLightingVertex[numVertices];
            for (int i = 0; i < numVertices; i++)
            {
                if (hasLightMapCoordinates)
                {
                    StaticMeshVertexBuffer.StaticMeshFullVertex vertex = vertexBuffer.VertexData[i];
                    vertices[i].LightMapCoordinate = vertexBuffer.bUseFullPrecisionUVs
                        ? vertex.FullPrecisionUVs[lightMapCoordinateIndex]
                        : vertex.HalfPrecisionUVs[lightMapCoordinateIndex];
                }
                if (samplesA is not null)
                {
                    vertices[i].SampleA = samplesA[i];
                    vertices[i].SampleB = samplesB?[i] ?? 0;
                }
            }
            VertexStream = Buffer.Create(Context.Device, BindFlags.VertexBuffer, vertices);
        }
    }

    /// <summary>
    /// The lights rendered on this mesh in their own passes: those that reach it, and aren't baked into its light-map. Cached until the scene's lights or the bounds change.
    /// </summary>
    public LightInteraction[] GetLightInteractions(BoxSphereBounds bounds)
    {
        lock (InteractionsLock)
        {
            UpdateInteractions(bounds);
            return Interactions;
        }
    }

    /// <summary>
    /// The sky lighting the base pass adds: the sum of the sky lights that reach the mesh and aren't baked into its light-map,
    /// as FSkyLightSceneInfo::AttachPrimitive accumulates it. Cached like <see cref="GetLightInteractions"/>.
    /// </summary>
    public SkyLighting GetSkyLighting(BoxSphereBounds bounds)
    {
        lock (InteractionsLock)
        {
            UpdateInteractions(bounds);
            return SkyLight;
        }
    }

    private void UpdateInteractions(BoxSphereBounds bounds)
    {
        SceneLight[] lights = Context.GetLights(out int lightsVersion);
        if (lightsVersion == InteractionsLightsVersion && bounds.Origin == InteractionsBounds.Origin && bounds.SphereRadius == InteractionsBounds.SphereRadius)
        {
            return;
        }
        var interactions = new List<LightInteraction>();
        var skyLight = new SkyLighting();
        if (AcceptsLights)
        {
            foreach (SceneLight light in lights)
            {
                if (AffectsPrimitive(light, bounds) && GetInteraction(light) is { } interaction)
                {
                    if (light.Type is SceneLightType.Sky)
                    {
                        //its precomputed shadows (if any) are ignored
                        skyLight = new SkyLighting(skyLight.Upper + light.Color, skyLight.Lower + light.LowerColor);
                    }
                    else
                    {
                        interactions.Add(interaction);
                    }
                }
            }
        }
        Interactions = interactions.ToArray();
        SkyLight = skyLight;
        InteractionsLightsVersion = lightsVersion;
        InteractionsBounds = bounds;
    }

    /// <summary>
    /// FLightSceneInfoCompact::AffectsPrimitive (ULightComponent::AffectsPrimitive), minus the light environment and bAcceptsLights tests, which the callers make
    /// </summary>
    /// <param name="compareLightingChannels">False for line checks, which ignore lighting channels</param>
    private bool AffectsPrimitive(SceneLight light, BoxSphereBounds bounds, bool compareLightingChannels = true)
    {
        //a light that belongs to a light environment only affects its primitives, which aren't lit by static lighting (and don't block visibility traces)
        if (light.HasLightEnvironment)
        {
            return false;
        }
        if (compareLightingChannels && !light.LightingChannels.OverlapsWith(LightingChannels) || !light.AffectsBounds(bounds) || !light.AffectsVolumes(bounds))
        {
            return false;
        }
        if (!AcceptsDynamicLights && !light.HasStaticShadowing)
        {
            return false;
        }
        //a level whose lighting was built on its own only has precomputed lighting for its own lights
        if (HasSelfContainedLighting && light.HasStaticShadowing && !light.LevelName.Equals(LevelName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (light.OnlyAffectSameAndSpecifiedLevels && !light.LevelName.Equals(LevelName, StringComparison.OrdinalIgnoreCase)
            && !light.OtherLevelsToAffect.Contains(LevelName))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// FStaticMeshSceneProxy::FLODInfo::GetInteraction, which LE3 doesn't change. Null if the light doesn't need its own pass.
    /// (FLightPrimitiveInteraction::Create also skips uncached interactions between static shadowing lights and primitives with bUsePrecomputedShadows
    /// whose owner is movable. That isn't checked: movable actors' meshes normally use a light environment instead)
    /// </summary>
    private LightInteraction? GetInteraction(SceneLight light)
    {
        if (light.HasStaticShadowing)
        {
            if (LightMapLightGuids.Contains(light.LightmapGuid))
            {
                return null;
            }
            foreach (ShadowVertexBufferEntry shadowVertexBuffer in ShadowVertexBuffers)
            {
                if (shadowVertexBuffer.LightGuid == light.LightGuid)
                {
                    return new LightInteraction(light, StaticShadowingType.ShadowVertexBuffer, null, shadowVertexBuffer.Buffer);
                }
            }
            foreach (ShadowMapTexture shadowMap in ShadowMaps)
            {
                if (shadowMap.LightGuid == light.LightGuid)
                {
                    return new LightInteraction(light, shadowMap.IsDistanceField ? StaticShadowingType.DistanceFieldShadowTexture : StaticShadowingType.ShadowTexture, shadowMap, null);
                }
            }
            if (IrrelevantLights.Contains(light.LightGuid))
            {
                return null;
            }
        }
        return new LightInteraction(light, StaticShadowingType.None, null, null);
    }

    /// <summary>
    /// Computes the light interactions and loads the light shaders they need, so that it doesn't happen during rendering. Thread-safe
    /// </summary>
    internal void PrepareLightShaders()
    {
        if (UsesLightEnvironment || GetBounds is null)
        {
            return;
        }
        foreach (LightInteraction interaction in GetLightInteractions(GetBounds()))
        {
            foreach (MaterialRenderProxy material in Materials)
            {
                material.GetLightShaders(interaction.Light.Type, interaction.Shadowing);
            }
        }
    }

    /// <summary>
    /// The primitive's lighting channels, for a light environment it's lit by
    /// </summary>
    internal LightingChannels Channels => LightingChannels;

    private static Vector4 ToVector4(Vector3 v) => new(v, 1);

    private static Vector4 ToScaleBias(Vector2 scale, Vector2 bias) => new(scale.X, scale.Y, bias.Y, bias.X);

    //Back to the order the bytes are serialized in: B, G, R, A
    private static uint PackColor(LegendaryExplorerCore.SharpDX.Color c) => (uint)(c.B | c.G << 8 | c.R << 16 | c.A << 24);

    private PreviewTextureCache.TextureEntry LoadPackageTexture(IMEPackage pcc, int uIndex)
    {
        if (!pcc.TryGetUExport(uIndex, out ExportEntry textureExport))
        {
            return null;
        }
        //Light-map and shadow-map textures are usually top-level objects with generated names, which aren't unique between levels.
        //So they're cached by file and export, rather than by path like other textures
        return Context.TextureCache.LoadTexture(textureExport, Context.PackageCache, $"{pcc.FilePath}|{uIndex}");
    }

    public void Dispose()
    {
        Context.UnregisterStaticLighting(this);
        VertexStream?.Dispose();
        VertexStream = null;
        foreach (ShadowVertexBufferEntry shadowVertexBuffer in ShadowVertexBuffers)
        {
            shadowVertexBuffer.Buffer.Dispose();
        }
        ShadowVertexBuffers.Clear();
    }
}
