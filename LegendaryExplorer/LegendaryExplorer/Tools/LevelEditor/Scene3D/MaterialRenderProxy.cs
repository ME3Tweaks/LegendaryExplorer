using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Classes;
using SharpDX.Direct3D11;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public class MaterialRenderProxy : MaterialInstanceConstantLevelEditor
{
    internal const string VERTEX_FACTORY_TYPE_NAME = "FLocalVertexFactory";

    /// <summary>
    /// A base pass vertex shader and pixel shader that render together. Which pair the game uses is determined by the light-map policy.
    /// </summary>
    private readonly record struct BasePassShaderTypes(string VertexShaderType, string PixelShaderType);

    /// <summary>
    /// Light-map policies that render one directional light plus sky lighting (see <see cref="PreviewLighting"/>), in order of preference
    /// </summary>
    private static readonly BasePassShaderTypes[] LitShaderTypes =
    [
        new("TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy", "TBasePassPixelShaderFDirectionalLightLightMapPolicySkyLight"),
        //SH light is the directional light plus an SH ambient term. For preview lighting the SH is left at 0, since the sky provides ambient; light environments set it
        new("TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy", "TBasePassPixelShaderFSHLightLightMapPolicySkyLight"),
    ];

    /// <summary>
    /// Light-map policies with no direct light, in order of preference. Unlit materials only have these
    /// </summary>
    private static readonly BasePassShaderTypes[] UnlitShaderTypes =
    [
        new("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "TBasePassPixelShaderFNoLightMapPolicySkyLight"),
        new("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight"),
    ];

    /// <summary>
    /// The light-map policy the game renders each type of static light-map with. BioWare added LMT_3 to LMT_6
    /// </summary>
    private static string GetLightMapPolicyName(ELightMapType lightMapType) => lightMapType switch
    {
        ELightMapType.LMT_1D => "FDirectionalVertexLightMapPolicy",
        ELightMapType.LMT_2D => "FDirectionalLightMapTexturePolicy",
        ELightMapType.LMT_3 => "FCustomVectorVertexLightMapPolicy",
        ELightMapType.LMT_4 => "FCustomVectorLightMapTexturePolicy",
        ELightMapType.LMT_5 => "FCustomSimpleVertexLightMapPolicy",
        ELightMapType.LMT_6 => "FCustomSimpleLightMapTexturePolicy",
        _ => null
    };

    /// <summary>
    /// Shaders that render a static light-map, in order of preference. The sky light is only for primitives the game lights with a dynamic sky light,
    /// so the variant without it is preferred.
    /// </summary>
    private static BasePassShaderTypes[] GetLightMapShaderTypes(ELightMapType lightMapType)
    {
        if (GetLightMapPolicyName(lightMapType) is not string policy)
        {
            return [];
        }
        string vertexShaderType = $"TBasePassVertexShader{policy}FNoDensityPolicy";
        return
        [
            new(vertexShaderType, $"TBasePassPixelShader{policy}NoSkyLight"),
            new(vertexShaderType, $"TBasePassPixelShader{policy}SkyLight"),
        ];
    }

    /// <summary>
    /// Every shader type that <see cref="SelectShaders"/> can choose from, for primitives without a static light-map
    /// </summary>
    internal static readonly string[] ShaderTypeNames = GetShaderTypeNames(ELightMapType.LMT_None);

    private static string[] GetShaderTypeNames(ELightMapType lightMapType) => GetLightMapShaderTypes(lightMapType).Concat(LitShaderTypes).Concat(UnlitShaderTypes)
        .SelectMany(types => new[] { types.VertexShaderType, types.PixelShaderType })
        .Distinct().ToArray();

    /// <summary>
    /// Picks the vertex and pixel shader to render with. Lit materials use a lit pair if they have one, otherwise they fall back to an unlit pair.
    /// </summary>
    /// <param name="shaders">A material's shaders. Nulls are ignored</param>
    /// <returns>Nulls if the material has no usable pair</returns>
    internal static (Shader vertexShader, Shader pixelShader) SelectShaders(IEnumerable<Shader> shaders, bool isUnlit) =>
        SelectShaders(shaders, isUnlit, ELightMapType.LMT_None, out _);

    /// <summary>
    /// Picks the vertex and pixel shader to render with. Lit materials use the shaders for <paramref name="lightMapType"/> if they have them,
    /// then a dynamically lit pair, then an unlit pair.
    /// </summary>
    /// <param name="shaders">A material's shaders. Nulls are ignored</param>
    /// <param name="usesLightMap">Whether the chosen shaders render the light-map</param>
    /// <returns>Nulls if the material has no usable pair</returns>
    internal static (Shader vertexShader, Shader pixelShader) SelectShaders(IEnumerable<Shader> shaders, bool isUnlit, ELightMapType lightMapType, out bool usesLightMap)
    {
        var shadersByType = new Dictionary<string, Shader>();
        foreach (Shader shader in shaders)
        {
            if (shader is not null)
            {
                shadersByType.TryAdd(shader.ShaderType.Name, shader);
            }
        }

        //Unlit materials ignore light-maps
        BasePassShaderTypes[] lightMapShaderTypes = isUnlit ? [] : GetLightMapShaderTypes(lightMapType);
        IEnumerable<BasePassShaderTypes> candidates = isUnlit ? UnlitShaderTypes : lightMapShaderTypes.Concat(LitShaderTypes).Concat(UnlitShaderTypes);
        foreach (BasePassShaderTypes types in candidates)
        {
            if (shadersByType.TryGetValue(types.VertexShaderType, out Shader vertexShader)
                && shadersByType.TryGetValue(types.PixelShaderType, out Shader pixelShader))
            {
                usesLightMap = lightMapShaderTypes.Contains(types);
                return (vertexShader, pixelShader);
            }
        }
        usesLightMap = false;
        return (null, null);
    }

    public EBlendMode BlendMode;
    /// <summary>
    /// The base material's bIsMasked, which the material compiler sets for masked and soft masked materials that have an opacity mask
    /// </summary>
    private bool HasOpacityMask;
    /// <summary>
    /// FMaterialResource::IsMasked: casts shadows with its own coverage, from its masked shadow depth shaders. A masked blend mode without an opacity mask
    /// isn't, and casts like an opaque material
    /// </summary>
    public bool IsMasked => IsMaskedMaterial(BlendMode, HasOpacityMask);
    /// <summary>
    /// Whether the material casts shadows: its blend mode isn't translucent (UE3's IsTranslucentBlendMode, which soft masked isn't), unless translucency
    /// is cast as masked
    /// </summary>
    public bool CastsShadowDepth => CastsShadowDepthWith(BlendMode, CastsLitTranslucencyShadowAsMasked);
    /// <summary>
    /// Whether the material casts with its own shadow depth shaders, which clip by its opacity mask: it's masked, or casts translucency as masked
    /// </summary>
    public bool CastsMaskedShadow => CastsMaskedShadowWith(BlendMode, HasOpacityMask, CastsLitTranslucencyShadowAsMasked);

    /// <summary>
    /// Whether a bake shadows light through the material by its transmission, partially or colored (translucent, alpha composite and modulate materials,
    /// as LightmassSceneBuilder exports them). Additive materials let all light through, so they block none, in the bake or the preview
    /// </summary>
    public bool BakesTransmission => BlendMode is EBlendMode.BLEND_Translucent or EBlendMode.BLEND_AlphaComposite or EBlendMode.BLEND_Modulate;

    public static bool IsMaskedMaterial(EBlendMode blendMode, bool bIsMasked) =>
        bIsMasked && blendMode is EBlendMode.BLEND_Masked or EBlendMode.BLEND_SoftMasked;

    // LE1/LE2/LE3 FShadowDepthDrawingPolicyFactory::DrawDynamicMesh (RVAs 0x776100, 0x36c550, 0x383040) and the drawing policy (RVAs 0x771610,
    // 0x3640a0, 0x379560) test the blend mode and the flag as UE3 does
    public static bool CastsShadowDepthWith(EBlendMode blendMode, bool castLitTranslucencyShadowAsMasked) => castLitTranslucencyShadowAsMasked
        || blendMode is EBlendMode.BLEND_Opaque or EBlendMode.BLEND_Masked or EBlendMode.BLEND_SoftMasked;

    public static bool CastsMaskedShadowWith(EBlendMode blendMode, bool bIsMasked, bool castLitTranslucencyShadowAsMasked) =>
        castLitTranslucencyShadowAsMasked || IsMaskedMaterial(blendMode, bIsMasked);
    public bool UseHairPass;
    public bool IsUnlit;
    public bool IsNonDirectional;
    /// <summary>
    /// Backfaces aren't culled
    /// </summary>
    public bool IsTwoSided;
    /// <summary>
    /// Casts shadows as if it were masked, even when translucent (bCastLitTranslucencyShadowAsMasked): installed hair materials set it
    /// </summary>
    public bool CastsLitTranslucencyShadowAsMasked;

    // materials that displace vertices disqualify the mesh from culling 
    public bool HasUndisplacedVertices { get; private set; }

    /// <summary>
    /// The type of static light-map the mesh this material is on has. Set by <see cref="QueueGameShaderLoad"/>, since it determines which shaders are loaded
    /// </summary>
    public ELightMapType LightMapType { get; private set; }

    /// <summary>
    /// Whether <see cref="UnrealVertexShader"/> and <see cref="UnrealPixelShader"/> render a static light-map (of type <see cref="LightMapType"/>).
    /// If false, they're lit by <see cref="PreviewLighting"/> instead.
    /// </summary>
    public bool UsesLightMap { get; private set; }
    private readonly Dictionary<string, float> ScalarParameterValues = [];
    private readonly Dictionary<string, LinearColor> VectorParameterValues = [];
    private readonly Dictionary<string, string> TextureParameterValues = [];
    private readonly List<string> Uniform2DTextureExpressions = [];
    public Dictionary<string, PreviewTextureCache.TextureEntry> TextureMap;
    private MaterialShaderMap ShaderMap;
    private uint CachedPixelFrameNumber = uint.MaxValue;
    private uint CachedVertexFrameNumber = uint.MaxValue;
    private readonly List<Vector4> CachedVertexScalarParameters = [];
    private readonly List<Vector4> CachedVertexVectorParameters = [];
    private readonly List<Vector4> CachedPixelScalarParameters = [];
    private readonly List<Vector4> CachedPixelVectorParameters = [];
    private readonly List<PreviewTextureCache.TextureEntry> CachedTexture2DParameters = [];
    private readonly List<PreviewTextureCache.TextureEntry> CachedCubeTextureParameters = [];

    /// <summary>
    /// A TBasePassVertexShader. See <see cref="SelectShaders"/>
    /// </summary>
    public Shader UnrealVertexShader;
    /// <summary>
    /// A TBasePassPixelShader. See <see cref="SelectShaders"/>
    /// </summary>
    public Shader UnrealPixelShader;

    /// <summary>
    /// For lit materials, base pass shaders without any lighting (FNoLightMapPolicy). The game uses these for meshes without a light-map,
    /// which the level's lights are then rendered on in separate passes. Also used for the Unlit viewport mode.
    /// </summary>
    public Shader NoLightMapVertexShader;
    /// <inheritdoc cref="NoLightMapVertexShader"/>
    public Shader NoLightMapPixelShader;

    /// <summary>
    /// The variant of <see cref="UnrealPixelShader"/> that also adds sky lighting, for meshes the level's sky lights aren't baked into the light-map of.
    /// Null if <see cref="UsesLightMap"/> is false, or the material doesn't have it.
    /// </summary>
    public Shader LightMapSkyLightPixelShader;

    //Always excludes baked lighting, even for materials whose default pair uses a light-map.
    public Shader PreviewVertexShader;
    public Shader PreviewPixelShader;

    /// <summary>
    /// The base pass shaders with a directional light and an SH light (FSHLightLightMapPolicy), which light environments with an SH light render with.
    /// Null if the material doesn't have them
    /// </summary>
    public Shader SHLightVertexShader;
    /// <inheritdoc cref="SHLightVertexShader"/>
    public Shader SHLightPixelShader;

    /// <summary>
    /// The base pass shaders for a mesh lit by a light environment: its directional light, plus its SH light or sky light
    /// </summary>
    public (Shader vertexShader, Shader pixelShader) GetLightEnvironmentShaders(MeshRenderContext context, LightEnvironmentLighting lighting)
    {
        if (context.IsUnlit || IsUnlit)
            return (NoLightMapVertexShader, NoLightMapPixelShader);
        if (lighting.UsesSHLight && SHLightVertexShader is not null && SHLightPixelShader is not null)
            return (SHLightVertexShader, SHLightPixelShader);
        return (PreviewVertexShader, PreviewPixelShader);
    }

    /// <summary>
    /// Selects a cached base-pass pair without changing the material or reloading the level.
    /// Missing variants return null so the caller can use the preview shader for this draw only.
    /// </summary>
    /// <param name="hasSkyLighting">Whether the level's sky lights add to the mesh's lighting (see <see cref="MeshStaticLighting.GetSkyLighting"/>)</param>
    public (Shader vertexShader, Shader pixelShader) GetBasePassShaders(MeshRenderContext context, bool usesLevelLights, out bool usePreviewLighting, bool hasSkyLighting = false)
    {
        usePreviewLighting = false;
        if (context.IsUnlit || IsUnlit)
            return (NoLightMapVertexShader, NoLightMapPixelShader);
        if (context.AreLightMapsActive && UsesLightMap)
            return (UnrealVertexShader, hasSkyLighting && LightMapSkyLightPixelShader is not null ? LightMapSkyLightPixelShader : UnrealPixelShader);
        if (!context.IsDynamicLightingActive || usesLevelLights)
            return (NoLightMapVertexShader, NoLightMapPixelShader);
        usePreviewLighting = true;
        return (PreviewVertexShader, PreviewPixelShader);
    }

    //Light shaders are loaded as they're needed, since there are many combinations of light type and shadowing, and most are never used.
    private ExportEntry LightShaderMapOwner;
    private readonly ConcurrentDictionary<(SceneLightType, StaticShadowingType), (Shader, Shader)> LightShaders = new();

    //The material chain is read from most to least derived. The first Material, or MaterialInstance with a StaticPermutationResource,
    //owns the shaders the game will use. Everything after that point only contributes parameter values.
    private bool FoundShaderMapOwner;
    private ExportEntry ShaderMapOwner;
    //Shaders aren't loaded in the constructor, since that's expensive and they aren't needed if game shaders are never turned on.
    //See MeshRenderContext.LoadPendingGameShaders
    private volatile bool AttemptedShaderLoad;
    //set while the material waits in its context's queue, see CanRenderWithGameShaders
    private volatile bool QueuedForGameShaderLoad;
    private readonly object ShaderLoadLock = new();
    private readonly MeshRenderContext Context;

    private string gameShaderError;
    /// <summary>
    /// Why this material can't be rendered with the game's shaders. Null if it can. Loads the shaders if they haven't been already.
    /// </summary>
    public string GameShaderError
    {
        get
        {
            LoadGameShaders();
            return gameShaderError;
        }
    }

    /// <summary>
    /// Loads the shaders if they haven't been already, unless they're queued for <see cref="MeshRenderContext.LoadPendingGameShaders"/> to load in the background:
    /// until it has, this is false, so that they aren't loaded in the middle of a frame
    /// </summary>
    public bool CanRenderWithGameShaders => !AwaitsGameShaderLoad && GameShaderError is null;

    /// <summary>Whether the shaders are queued for <see cref="MeshRenderContext.LoadPendingGameShaders"/>, and nothing has loaded them yet</summary>
    internal bool AwaitsGameShaderLoad => QueuedForGameShaderLoad && !AttemptedShaderLoad;

    /// <summary>
    /// <see cref="GameShaderError"/>, without loading the shaders. Null if they haven't been loaded yet
    /// </summary>
    public string LoadedGameShaderError => AttemptedShaderLoad ? gameShaderError : null;

    public MaterialRenderProxy(MeshRenderContext context, ExportEntry export) : base(export, context.PackageCache, true)
    {
        Context = context;
        if (!Game.IsLEGame())
        {
            gameShaderError = "Game shaders are only supported for Legendary Edition games";
            AttemptedShaderLoad = true;
        }
    }

    /// <summary>
    /// Queues this material's shaders to be loaded by <see cref="MeshRenderContext.LoadPendingGameShaders"/>.
    /// (Otherwise they're loaded the first time they're needed.)
    /// </summary>
    /// <param name="lightMapType">The type of static light-map the mesh this material is on has</param>
    public void QueueGameShaderLoad(ELightMapType lightMapType)
    {
        LightMapType = lightMapType;
        if (!AttemptedShaderLoad)
        {
            QueuedForGameShaderLoad = true;
            Context.AddPendingGameShaderLoad(this);
        }
    }

    /// <summary>Called by the context when it takes the material off its queue, to load it or not</summary>
    internal void Dequeued() => QueuedForGameShaderLoad = false;

    /// <summary>
    /// Call if rendering with game shaders fails, so that this material falls back to the LEX shader
    /// </summary>
    public void MarkGameShadersFailed(Exception e)
    {
        gameShaderError = e.Message;
    }

    internal void CancelPendingShaderLoad()
    {
        Dequeued();
        Context.RemovePendingGameShaderLoad(this);
    }

    /// <summary>
    /// Loads the game's shaders for this material, and prepares its shadow depth shaders from them, if they haven't been already. Thread-safe.
    /// </summary>
    public void LoadGameShaders()
    {
        if (!AttemptedShaderLoad)
        {
            LoadBaseShaders();
        }
        //outside ShaderLoadLock, since preparing takes the LightShaders lock. If the load above threw, they're left pending until it's tried again
        if (ShadowShadersPending)
        {
            PrepareShadowShaders();
        }
    }

    private void LoadBaseShaders()
    {
        lock (ShaderLoadLock)
        {
            if (AttemptedShaderLoad) return;
            if (gameShaderError is null)
            {
                if (ShaderMapOwner is null)
                {
                    gameShaderError = "Could not find the Material that owns this material's shaders";
                }
                else
                {
                    LoadShaders(ShaderMapOwner);
                    LightShaderMapOwner = ShaderMapOwner;
                    ShaderMapOwner = null;
                    if (gameShaderError is null && (ShaderMap is null || UnrealVertexShader is null || UnrealPixelShader is null))
                    {
                        gameShaderError = "Could not find the material's shaders";
                    }
                }
            }
            //set last, so that other threads don't see a partially loaded material
            AttemptedShaderLoad = true;
        }
    }

    protected override void ReadBaseMaterial(ExportEntry mat, PackageCache assetCache, Material parsedMaterial)
    {
        base.ReadBaseMaterial(mat, assetCache, parsedMaterial);

        if (!Game.IsLEGame()) return;

        var props = mat.GetProperties(packageCache: assetCache);
        Enum.TryParse(props.GetProp<EnumProperty>("BlendMode")?.Value ?? "BLEND_Opaque", out BlendMode);
        HasOpacityMask = props.GetProp<BoolProperty>("bIsMasked") is { Value: true };

        //if a MIC had a StaticPermutationResource, the shaders came from that instead
        bool isShaderMapOwner = !FoundShaderMapOwner;
        if (isShaderMapOwner)
        {
            HasUndisplacedVertices = !parsedMaterial.SM3MaterialResource.bUsesMaterialVertexPositionOffset;
            FoundShaderMapOwner = true;
            foreach (int uIndex in parsedMaterial.SM3MaterialResource.UniformExpressionTextures)
            {
                Uniform2DTextureExpressions.Add(mat.FileRef.GetEntry(uIndex)?.InstancedFullPath);
            }
        }

        UseHairPass = props.GetProp<BoolProperty>("bHairPass") is { Value: true };
        IsTwoSided = props.GetProp<BoolProperty>("TwoSided") is { Value: true };
        CastsLitTranslucencyShadowAsMasked = props.GetProp<BoolProperty>("bCastLitTranslucencyShadowAsMasked") is { Value: true };
        IsUnlit = props.GetProp<EnumProperty>("LightingModel") is {} lightingModelProp && lightingModelProp.Value == "MLM_Unlit";
        IsNonDirectional = props.GetProp<EnumProperty>("LightingModel") is { Value.Name: "MLM_NonDirectional" };

        var expressionsProp = props.GetProp<ArrayProperty<ObjectProperty>>("Expressions");
        if (expressionsProp is not null)
        {
            foreach (ObjectProperty expressionProp in expressionsProp)
            {
                ExportEntry expressionExport = expressionProp.ResolveToExport(mat.FileRef, assetCache);
                var expressionProps = expressionExport?.GetProperties(packageCache: assetCache);
                if (expressionProps?.GetProp<NameProperty>("ParameterName") is {} paramNameProp)
                {
                    //this will run after ReadMaterialInstanceConstant, so we don't want to overwrite any values specified there
                    Property defaultValueProp = expressionProps.GetProp<Property>("DefaultValue");
                    if (defaultValueProp is FloatProperty defaultfloatProp)
                    {
                        ScalarParameterValues.TryAdd(paramNameProp.Value.Instanced, defaultfloatProp.Value);
                    }
                    else if (defaultValueProp is StructProperty defaultVectorProp)
                    {
                        VectorParameterValues.TryAdd(paramNameProp.Value.Instanced, CommonStructs.GetLinearColor(defaultVectorProp));
                    }
                    else if (expressionProps.GetProp<ObjectProperty>("Texture") is {} textureProp)
                    {
                        if (!TextureParameterValues.ContainsKey(paramNameProp.Value.Instanced) 
                            && mat.FileRef.GetEntry(textureProp.Value) is {} texEntry)
                        {
                            Textures.Add(texEntry);
                            TextureParameterValues.Add(paramNameProp.Value.Instanced, texEntry.InstancedFullPath);
                        }
                    }
                }
            }
        }

        if (isShaderMapOwner)
        {
            ShaderMapOwner = mat;
        }
    }

    private void LoadShaders(ExportEntry mat)
    {
        try
        {
            //the shadow depth shaders are read in the same lookup, when the material has any to use (see PrepareShadowShaders)
            string[] shadowShaderTypes = !CastsShadowDepth ? [] : CastsMaskedShadow ? [ShadowDepthVertexShaderType, ShadowDepthPixelShaderType]
                : HasUndisplacedVertices ? [] : [ShadowDepthVertexShaderType];
            (ShaderMap, Shader[] shaders) = ShaderCacheManipulator.GetMaterialShaderMapAndShaders(mat, Context.GetSeekFreeShaderCache,
                [.. GetShaderTypeNames(LightMapType), .. shadowShaderTypes]);
            ShadowDepthVertexShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == ShadowDepthVertexShaderType);
            ShadowDepthPixelShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == ShadowDepthPixelShaderType);

            (UnrealVertexShader, UnrealPixelShader) = SelectShaders(shaders, IsUnlit, LightMapType, out bool usesLightMap);
            UsesLightMap = usesLightMap;
            if (usesLightMap)
            {
                string skyLightPixelShaderType = $"TBasePassPixelShader{GetLightMapPolicyName(LightMapType)}SkyLight";
                LightMapSkyLightPixelShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == skyLightPixelShaderType);
            }
            (NoLightMapVertexShader, NoLightMapPixelShader) = SelectShaders(shaders, isUnlit: true);
            (PreviewVertexShader, PreviewPixelShader) = SelectShaders(shaders, IsUnlit);
            BasePassShaderTypes shLightTypes = LitShaderTypes[1];
            SHLightVertexShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == shLightTypes.VertexShaderType);
            SHLightPixelShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == shLightTypes.PixelShaderType);
        }
        catch (Exception e)
        {
            ShaderMap = null;
            UnrealVertexShader = null;
            UnrealPixelShader = null;
            gameShaderError = $"Failed to load shaders for {mat.InstancedFullPath}: {e.Message}";
        }
    }

    /// <summary>
    /// Gets the shaders that render a light on this material in its own pass (TLightVertexShader and TLightPixelShader), loading them if they haven't been. Thread-safe.
    /// </summary>
    /// <returns>Nulls if the material doesn't have them</returns>
    public (Shader vertexShader, Shader pixelShader) GetLightShaders(SceneLightType lightType, StaticShadowingType shadowing)
    {
        if (!CanRenderWithGameShaders || IsUnlit)
        {
            return (null, null);
        }
        if (LightShaders.TryGetValue((lightType, shadowing), out (Shader, Shader) lightShaders))
        {
            return lightShaders;
        }
        lock (LightShaders)
        {
            if (LightShaders.TryGetValue((lightType, shadowing), out lightShaders))
            {
                return lightShaders;
            }
            string lightPolicy = lightType switch
            {
                SceneLightType.Spot => "FSpotLightPolicy",
                SceneLightType.Directional => "FDirectionalLightPolicy",
                SceneLightType.Point => "FPointLightPolicy",
                //sky lights are part of the base pass
                _ => throw new ArgumentOutOfRangeException(nameof(lightType), lightType, null)
            };
            string shadowingPolicy = shadowing switch
            {
                StaticShadowingType.ShadowTexture => "FShadowTexturePolicy",
                StaticShadowingType.DistanceFieldShadowTexture => "FSignedDistanceFieldShadowTexturePolicy",
                StaticShadowingType.ShadowVertexBuffer => "FShadowVertexBufferPolicy",
                _ => "FNoStaticShadowingPolicy",
            };
            lightShaders = LoadLightPassShaders($"TLightVertexShader{lightPolicy}{shadowingPolicy}", $"TLightPixelShader{lightPolicy}{shadowingPolicy}");
            LightShaders[(lightType, shadowing)] = lightShaders;
            return lightShaders;
        }
    }

    private bool LoadedSHLightPassShaders;
    private (Shader, Shader) SHLightPassShaders;

    /// <summary>
    /// The shaders that render an SH light on this material in its own pass (FSphericalHarmonicLightPolicy), loading them if they haven't been. Thread-safe.
    /// Light environments that cast a shadow render their SH light with these, after the modulated shadows
    /// </summary>
    /// <returns>Nulls if the material doesn't have them</returns>
    public (Shader vertexShader, Shader pixelShader) GetSHLightPassShaders()
    {
        if (!CanRenderWithGameShaders || IsUnlit)
        {
            return (null, null);
        }
        lock (LightShaders)
        {
            if (!LoadedSHLightPassShaders)
            {
                SHLightPassShaders = LoadLightPassShaders("TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy",
                    "TLightPixelShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy");
                LoadedSHLightPassShaders = true;
            }
            return SHLightPassShaders;
        }
    }

    private bool LoadedModShadowMeshShaders;
    private (Shader, Shader) ModShadowMeshShaders;
    private string modShadowMaskError;
    /// <summary>Why this material, as a receiver, couldn't mask a LightShadow_ModulateBetter shadow off itself, or null</summary>
    public string ModShadowMaskError => Volatile.Read(ref modShadowMaskError);
    internal void SetModShadowMaskError(string error) => Volatile.Write(ref modShadowMaskError, error);

    /// <summary>
    /// The FModShadowMeshVertexShader and FModShadowMeshPixelShader that mask LightShadow_ModulateBetter shadows off this material where it faces away
    /// from the light or is emissive, loading them if they haven't been. Thread-safe
    /// </summary>
    /// <returns>Nulls if the material doesn't have them</returns>
    public (Shader vertexShader, Shader pixelShader) GetModShadowMeshShaders()
    {
        if (!CanRenderWithGameShaders || IsUnlit)
        {
            return (null, null);
        }
        lock (LightShaders)
        {
            if (!LoadedModShadowMeshShaders)
            {
                ModShadowMeshShaders = LoadLightPassShaders("FModShadowMeshVertexShader", "FModShadowMeshPixelShader");
                LoadedModShadowMeshShaders = true;
            }
            return ModShadowMeshShaders;
        }
    }

    /// <param name="Pixel">Null for an opaque material, whose depth UE3 writes with the default material's pixel shader</param>
    /// <param name="DepthPixelShader">For an opaque material: that pixel shader, compiled for the vertex shader's outputs
    /// (<see cref="LightAttenuationRenderer.DepthPixelShaderByteCode"/>)</param>
    internal sealed record ShadowDepthShaders(TShadowDepthVertexShader Vertex, TShadowDepthPixelShader Pixel, byte[] DepthPixelShader);

    private const string ShadowDepthVertexShaderType = "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>";
    private const string ShadowDepthPixelShaderType = "TShadowDepthPixelShaderTRUEFALSE";
    //read with the base shaders, for PrepareShadowShaders. Only the shader data: their D3D shaders are made when first drawn
    private Shader ShadowDepthVertexShader, ShadowDepthPixelShader;

    //what happens to a section's shadow when the material's shadow depth shaders can't be used
    internal const string NoMaskedShadow = "masked shadow coverage is unavailable (section omitted from shadow depth).";
    internal const string UndeformedShadow = "shadow depth uses undeformed mesh geometry.";
    internal string ShadowShaderFallback => CastsMaskedShadow ? NoMaskedShadow : UndeformedShadow;

    private bool LoadedShadowShaders;
    //set once by PrepareShadowShaders, and only cleared afterwards (by the render thread), so it's read without the lock
    private ShadowDepthShaders shadowShaders;
    internal ShadowDepthShaders ShadowShaders => Volatile.Read(ref shadowShaders);
    private string shadowDepthError;
    /// <summary>Why the preview's shadow depth from this material differs from the game's, or null</summary>
    public string ShadowDepthError => Volatile.Read(ref shadowDepthError);

    // Runs once the base shaders have been loaded (LoadGameShaders), whose lookup read the shadow depth shaders
    internal void PrepareShadowShaders()
    {
        lock (LightShaders)
        {
            if (LoadedShadowShaders || !AttemptedShaderLoad) return;
            try
            {
                //translucent materials don't cast shadows in UE3, unless they're cast as masked
                if (!CastsShadowDepth) return;
                //Masked materials, and translucency cast as masked, cast with their own shaders' coverage. UE3 draws opaque ones with the default
                //material's shaders (plain geometry, as the preview's own depth shader does), unless the material moves its vertices (world position
                //offset), when it has its own vertex shader (FShadowDepthVertexShader::ShouldCache)
                if (!CastsMaskedShadow && HasUndisplacedVertices)
                {
                    //no vertex shader of its own to look for
                    return;
                }
                if (LightShaderMapOwner is null || ShaderMap is null)
                {
                    //it has no shader map to look in, which the material's own error says why
                    Volatile.Write(ref shadowDepthError, $"It has no game shaders ({gameShaderError ?? "they weren't loaded"}): {ShadowShaderFallback}");
                    return;
                }
                //its shader map may lack only the base pass shaders that error is about, so the shadow shaders were still looked for
                string baseShaderProblem = gameShaderError is null ? "" : $" (its other game shaders: {gameShaderError})";
                (Shader vertex, Shader pixel) shaders = (ShadowDepthVertexShader, ShadowDepthPixelShader);
                bool usable = shaders.vertex is TShadowDepthVertexShader { VertexFactoryParameters.Parameters: FLocalVertexFactoryShaderParameters };
                if (CastsMaskedShadow)
                {
                    if (usable && shaders.pixel is TShadowDepthPixelShader ps)
                        Volatile.Write(ref shadowShaders, new ShadowDepthShaders((TShadowDepthVertexShader)shaders.vertex, ps, null));
                    else
                        Volatile.Write(ref shadowDepthError, $"No compatible game shadow shader{baseShaderProblem}: {NoMaskedShadow}");
                }
                else if (!usable)
                {
                    Volatile.Write(ref shadowDepthError, $"No compatible game shadow vertex shader for its world position offset{baseShaderProblem}: {UndeformedShadow}");
                }
                else
                {
                    //compiled here, with the other preparation, rather than when it's first drawn
                    var vertex = (TShadowDepthVertexShader)shaders.vertex;
                    byte[] depthPixelShader;
                    try
                    {
                        depthPixelShader = Context.GetDepthPixelShaderByteCode(vertex);
                    }
                    catch (Exception e)
                    {
                        Volatile.Write(ref shadowDepthError, $"A depth pixel shader for its game shadow vertex shader couldn't be compiled ({e.Message}): {UndeformedShadow}");
                        return;
                    }
                    if (depthPixelShader is null)
                        Volatile.Write(ref shadowDepthError, $"Its game shadow vertex shader's outputs couldn't be matched by a depth pixel shader: {UndeformedShadow}");
                    else
                        Volatile.Write(ref shadowShaders, new ShadowDepthShaders(vertex, null, depthPixelShader));
                }
            }
            finally
            {
                //after the shaders, so that a material no longer pending has its final shaders and error
                Volatile.Write(ref LoadedShadowShaders, true);
            }
        }
    }

    /// <summary>Whether <see cref="PrepareShadowShaders"/> hasn't run yet (it runs once the base shaders are loaded), so the material has no shadow depth shaders for now</summary>
    internal bool ShadowShadersPending => !Volatile.Read(ref LoadedShadowShaders);

    /// <summary>The shadow depth shaders don't work for this material: its shadows use the plain depth shader from now on</summary>
    internal void DisableShadowShaders(string error)
    {
        Volatile.Write(ref shadowDepthError, error);
        Volatile.Write(ref shadowShaders, null);
    }

    //Call under the LightShaders lock
    private (Shader, Shader) LoadLightPassShaders(string vertexShaderType, string pixelShaderType)
    {
        if (LightShaderMapOwner is null)
        {
            return (null, null);
        }
        try
        {
            (_, Shader[] shaders) = ShaderCacheManipulator.GetMaterialShaderMapAndShaders(LightShaderMapOwner, Context.GetSeekFreeShaderCache,
                [vertexShaderType, pixelShaderType]);
            Shader vertexShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == vertexShaderType);
            Shader pixelShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == pixelShaderType);
            if (vertexShader is null || pixelShader is null)
            {
                return (null, null);
            }
            if (Context.Device is not null)
            {
                Context.GetCachedVertexShader(vertexShader.Guid, vertexShader.ShaderByteCode);
                Context.GetCachedPixelShader(pixelShader.Guid, pixelShader.ShaderByteCode);
            }
            return (vertexShader, pixelShader);
        }
        catch (Exception e)
        {
            //the light just isn't rendered on this material
            System.Diagnostics.Debug.WriteLine($"Could not load {pixelShaderType} for {InstancedFullPath}: {e.Message}");
            return (null, null);
        }
    }

    private bool LoadedLightFunctionShader;
    private Shader LightFunctionPixelShader;

    /// <summary>
    /// For a material used as a light function: its FLightFunctionPixelShader, loading it if it hasn't been. Null if it doesn't have one.
    /// (These materials have no base pass shaders, so <see cref="CanRenderWithGameShaders"/> is false for them.)
    /// </summary>
    public Shader GetLightFunctionPixelShader()
    {
        LoadGameShaders();
        lock (LightShaders)
        {
            if (!LoadedLightFunctionShader)
            {
                LoadedLightFunctionShader = true;
                if (LightShaderMapOwner is not null && ShaderMap is not null)
                {
                    try
                    {
                        (_, Shader[] shaders) = ShaderCacheManipulator.GetMaterialShaderMapAndMaterialShaders(LightShaderMapOwner, Context.GetSeekFreeShaderCache, "FLightFunctionPixelShader");
                        LightFunctionPixelShader = shaders[0];
                    }
                    catch (Exception e)
                    {
                        //the light function just isn't rendered
                        System.Diagnostics.Debug.WriteLine($"Could not load the light function shader of {InstancedFullPath}: {e.Message}");
                    }
                }
            }
            return LightFunctionPixelShader;
        }
    }

    protected override void ReadMaterialInstanceConstant(ExportEntry matInst, PropertyCollection props)
    {
        base.ReadMaterialInstanceConstant(matInst, props);

        if (!Game.IsLEGame()) return;

        //The most derived value wins (UMaterialInstanceConstant::Get*ParameterValue); a null texture defers to the parent
        if (props.GetProp<ArrayProperty<StructProperty>>("ScalarParameterValues") is { } scalarValues)
        {
            foreach (StructProperty scalarValue in scalarValues)
            {
                if (scalarValue.GetProp<NameProperty>("ParameterName") is { } paramNameProp
                    && scalarValue.GetProp<FloatProperty>("ParameterValue") is { } valProp)
                {
                    ScalarParameterValues.TryAdd(paramNameProp.Value.Instanced, valProp.Value);
                }
            }
        }
        if (props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues") is { } vectorValues)
        {
            foreach (StructProperty vectorValue in vectorValues)
            {
                if (vectorValue.GetProp<NameProperty>("ParameterName") is { } paramNameProp
                    && vectorValue.GetProp<StructProperty>("ParameterValue") is { } valProp)
                {
                    VectorParameterValues.TryAdd(paramNameProp.Value.Instanced, CommonStructs.GetLinearColor(valProp));
                }
            }
        }
        if (props.GetProp<ArrayProperty<StructProperty>>("TextureParameterValues") is { } textureValues)
        {
            foreach (StructProperty textureValue in textureValues)
            {
                if (textureValue.GetProp<NameProperty>("ParameterName") is { } paramNameProp
                    && textureValue.GetProp<ObjectProperty>("ParameterValue") is { } valProp
                    && valProp.ResolveToEntry(matInst.FileRef) is { } texEntry)
                {
                    TextureParameterValues.TryAdd(paramNameProp.Value.Instanced, texEntry.InstancedFullPath);
                }
            }
        }

        if (!FoundShaderMapOwner && props.GetProp<BoolProperty>("bHasStaticPermutationResource") is { Value: true })
        {
            FoundShaderMapOwner = true;
            MaterialInstance binary;
            try
            {
                binary = ObjectBinary.From<MaterialInstance>(matInst);
            }
            catch (Exception e)
            {
                gameShaderError = $"Failed to parse {matInst.InstancedFullPath}: {e.Message}";
                return;
            }
            HasUndisplacedVertices = !binary.SM3StaticPermutationResource.bUsesMaterialVertexPositionOffset;
            foreach (int uIndex in binary.SM3StaticPermutationResource.UniformExpressionTextures)
            {
                IEntry texEntry = matInst.FileRef.GetEntry(uIndex);
                Uniform2DTextureExpressions.Add(texEntry?.InstancedFullPath);
                //A static permutation can sample textures the base material's resource doesn't
                if (texEntry is not null)
                {
                    Textures.Add(texEntry);
                }
            }
            ShaderMapOwner = matInst;
        }
    }

    /// <summary>
    /// Writes the parameters of the base pass shaders
    /// </summary>
    /// <param name="vertexShader">The base pass vertex shader being rendered with: <see cref="UnrealVertexShader"/> or <see cref="NoLightMapVertexShader"/></param>
    /// <param name="pixelShader">The matching pixel shader</param>
    /// <param name="staticLighting">The mesh's precomputed lighting. Its light-map is required if <see cref="UsesLightMap"/></param>
    /// <param name="usePreviewLighting">Light the mesh with <see cref="MeshRenderContext.Lighting"/>, rather than the level's lighting</param>
    /// <param name="skyLighting">The level's sky lighting on the mesh. Unused if <paramref name="usePreviewLighting"/></param>
    /// <param name="lightEnvironment">The lights of the light environment that lights the mesh, which replace the preview lighting. Null if it isn't lit by one</param>
    public void UpdateShaderParams(Span<byte> vertexConstantBuffer, Span<byte> pixelConstantBuffer, MeshRenderContext context, Mesh<LEVertex> mesh,
        Shader vertexShader, Shader pixelShader, MeshStaticLighting staticLighting, bool usePreviewLighting, SkyLighting skyLighting = default,
        LightEnvironmentLighting lightEnvironment = null)
    {

        vertexConstantBuffer.Clear();
        pixelConstantBuffer.Clear();
        ShaderParameterSetters.WriteBasePassVertexShaderValues(vertexShader, vertexConstantBuffer, context, mesh, this, staticLighting, lightEnvironment);
        ShaderParameterSetters.WriteBasePassPixelShaderValues(pixelShader, pixelConstantBuffer, context, mesh, this, staticLighting, usePreviewLighting, skyLighting,
            lightEnvironment);

    }

    /// <summary>
    /// Writes the parameters of the shaders that render a light on this material (see <see cref="GetLightShaders"/>)
    /// </summary>
    /// <param name="lightAttenuation">The light's dynamic shadows and light function, rendered in screen space (see <see cref="LightAttenuationRenderer"/>). Null if it has neither</param>
    public void UpdateLightPassShaderParams(Span<byte> vertexConstantBuffer, Span<byte> pixelConstantBuffer, MeshRenderContext context, Mesh<LEVertex> mesh,
        Shader vertexShader, Shader pixelShader, LightInteraction interaction, ShaderResourceView lightAttenuation = null)
    {
        vertexConstantBuffer.Clear();
        pixelConstantBuffer.Clear();
        ShaderParameterSetters.WriteLightVertexShaderValues(vertexShader, vertexConstantBuffer, context, mesh, this, interaction);
        ShaderParameterSetters.WriteLightPixelShaderValues(pixelShader, pixelConstantBuffer, context, mesh, this, interaction, lightAttenuation);
    }

    /// <summary>
    /// Writes the parameters of the shaders that render an SH light on this material (see <see cref="GetSHLightPassShaders"/>)
    /// </summary>
    public void UpdateSHLightPassShaderParams(Span<byte> vertexConstantBuffer, Span<byte> pixelConstantBuffer, MeshRenderContext context, Mesh<LEVertex> mesh,
        Shader vertexShader, Shader pixelShader, in SHVectorRGB incidentLighting)
    {
        vertexConstantBuffer.Clear();
        pixelConstantBuffer.Clear();
        ShaderParameterSetters.WriteSHLightPassValues(vertexShader, pixelShader, vertexConstantBuffer, pixelConstantBuffer, context, mesh, this, incidentLighting);
    }

    public (List<Vector4> scalar, List<Vector4> vector) GetCachedVertexParameters(MeshRenderContext context)
    {
        UpdateUniformVertexParameters(context);
        return (CachedVertexScalarParameters, CachedVertexVectorParameters);
    }

    public (List<Vector4> scalar, List<Vector4> vector, 
        List<PreviewTextureCache.TextureEntry> tex2d, List<PreviewTextureCache.TextureEntry> cube)
        GetCachedPixelParameters(MeshRenderContext context)
    {
        UpdateUniformPixelParameters(context);
        return (CachedPixelScalarParameters, CachedPixelVectorParameters, CachedTexture2DParameters, CachedCubeTextureParameters);
    }

    private void UpdateUniformVertexParameters(MeshRenderContext context)
    {
        if (CachedVertexFrameNumber == context.NumFrames) return;
        CachedVertexFrameNumber = context.NumFrames;
        CachedVertexScalarParameters.Clear();
        CachedVertexVectorParameters.Clear();

        var uniformContext = new UniformExpressionRenderContext(
            ScalarParameterValues, VectorParameterValues,
            context.Time, context.Time, GetFlipBookTextureOffset, GetFlipBookTextureScale);

        UpdateExpressions(uniformContext,
            ShaderMap.UniformVertexVectorExpressions, ShaderMap.UniformVertexScalarExpressions,
            CachedVertexScalarParameters, CachedVertexVectorParameters);
    }

    private void UpdateUniformPixelParameters(MeshRenderContext context)
    {
        if (CachedPixelFrameNumber == context.NumFrames) return;
        CachedPixelFrameNumber = context.NumFrames;
        CachedPixelScalarParameters.Clear();
        CachedPixelVectorParameters.Clear();
        CachedTexture2DParameters.Clear();
        CachedCubeTextureParameters.Clear();

        var uniformContext = new UniformExpressionRenderContext(
            ScalarParameterValues, VectorParameterValues,
            context.Time, context.Time, GetFlipBookTextureOffset, GetFlipBookTextureScale);

        UpdateExpressions(uniformContext,
            ShaderMap.UniformPixelVectorExpressions, ShaderMap.UniformPixelScalarExpressions,
            CachedPixelScalarParameters, CachedPixelVectorParameters);

        UpdateTextureExpressions(ShaderMap.Uniform2DTextureExpressions, CachedTexture2DParameters);
        UpdateTextureExpressions(ShaderMap.UniformCubeTextureExpressions, CachedCubeTextureParameters);
    }

    private LinearColor GetFlipBookTextureOffset(UniformExpressionRenderContext context, int texIndex)
    {
        return GetFlipBookTexture(texIndex)?.GetTextureOffset(context) ?? LinearColor.Black;
    }

    private LinearColor GetFlipBookTextureScale(UniformExpressionRenderContext context, int texIndex)
    {
        return GetFlipBookTexture(texIndex)?.GetTextureScale() ?? LinearColor.Black;
    }

    private PreviewTextureCache.FlipBookTextureEntry GetFlipBookTexture(int texIndex)
    {
        if ((uint)texIndex < Uniform2DTextureExpressions.Count
            && Uniform2DTextureExpressions[texIndex] is { } texifp
            && TextureMap.TryGetValue(texifp, out var texture))
        {
            return texture as PreviewTextureCache.FlipBookTextureEntry;
        }
        return null;
    }

    private void UpdateTextureExpressions(MaterialUniformExpressionTexture[] textureExpressions, List<PreviewTextureCache.TextureEntry> textureCache)
    {
        foreach (MaterialUniformExpressionTexture texExpression in textureExpressions)
        {
            PreviewTextureCache.TextureEntry texture = null;
            switch (texExpression)
            {
                case MaterialUniformExpressionTextureParameter texParamExpression:
                    if (TextureParameterValues.TryGetValue(texParamExpression.ParameterName.Instanced, out string texIfp)
                        && texIfp is not null)
                    {
                        TextureMap.TryGetValue(texIfp, out texture);
                    }
                    break;
                default:
                    if ((uint)texExpression.TextureIndex < Uniform2DTextureExpressions.Count
                        && Uniform2DTextureExpressions[texExpression.TextureIndex] is {} texifp)
                    {
                        TextureMap.TryGetValue(texifp, out texture);
                    }
                    break;
            }
            textureCache.Add(texture);
        }
    }

    private void UpdateExpressions(UniformExpressionRenderContext uniformContext, 
        MaterialUniformExpression[] vectorExpressions, MaterialUniformExpression[] scalarExpressions, 
        List<Vector4> scalarCache, List<Vector4> vectorCache)
    {
        var enumerator = scalarExpressions.ChunkBySpan(4);
        foreach (ReadOnlySpan<MaterialUniformExpression> scalerExpression in enumerator)
        {
            LinearColor xVal = default;
            LinearColor yVal = default;
            LinearColor zVal = default;
            LinearColor wVal = default;
            scalerExpression[0].GetNumberValue(uniformContext, ref xVal);
            scalerExpression[1].GetNumberValue(uniformContext, ref yVal);
            scalerExpression[2].GetNumberValue(uniformContext, ref zVal);
            scalerExpression[3].GetNumberValue(uniformContext, ref wVal);
            scalarCache.Add(new Vector4(xVal.R, yVal.R, zVal.R, wVal.R));
        }
        if (enumerator.Current is { Length: > 0 } remainder)
        {
            LinearColor xVal = default;
            LinearColor yVal = default;
            LinearColor zVal = default;
            LinearColor wVal = default;
            remainder[0].GetNumberValue(uniformContext, ref xVal);
            if(remainder.Length > 1)
            {
                remainder[1].GetNumberValue(uniformContext, ref yVal);
                if (remainder.Length > 2)
                {
                    remainder[2].GetNumberValue(uniformContext, ref zVal);
                    if (remainder.Length > 3)
                    {
                        remainder[3].GetNumberValue(uniformContext, ref wVal);
                    }
                }
            }
            scalarCache.Add(new Vector4(xVal.R, yVal.R, zVal.R, wVal.R));
        }
        foreach (MaterialUniformExpression vectorExpression in vectorExpressions)
        {
            LinearColor val = default;
            vectorExpression.GetNumberValue(uniformContext, ref val);
            vectorCache.Add((Vector4)val);
        }
    }
}
