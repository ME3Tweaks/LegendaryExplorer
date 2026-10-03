using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Classes;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
        //SH light is the directional light plus an SH ambient term. The SH is left at 0, since the sky provides ambient
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
    public bool UseHairPass;
    public bool IsUnlit;
    /// <summary>
    /// Backfaces aren't culled
    /// </summary>
    public bool IsTwoSided;

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
    /// which the level's lights are then rendered on in separate passes. Null for unlit materials, or if the material doesn't have them.
    /// </summary>
    public Shader NoLightMapVertexShader;
    /// <inheritdoc cref="NoLightMapVertexShader"/>
    public Shader NoLightMapPixelShader;

    //Light shaders are loaded as they're needed, since there are many combinations of light type and shadowing, and most are never used.
    private ExportEntry LightShaderMapOwner;
    private readonly Dictionary<(SceneLightType, StaticShadowingType), (Shader, Shader)> LightShaders = [];

    //The material chain is read from most to least derived. The first Material, or MaterialInstance with a StaticPermutationResource,
    //owns the shaders the game will use. Everything after that point only contributes parameter values.
    private bool FoundShaderMapOwner;
    private ExportEntry ShaderMapOwner;
    //Shaders aren't loaded in the constructor, since that's expensive and they aren't needed if game shaders are never turned on.
    //See MeshRenderContext.LoadPendingGameShaders
    private volatile bool AttemptedShaderLoad;
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
    /// Loads the shaders if they haven't been already.
    /// </summary>
    public bool CanRenderWithGameShaders => GameShaderError is null;

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
            Context.AddPendingGameShaderLoad(this);
        }
    }

    /// <summary>
    /// Call if rendering with game shaders fails, so that this material falls back to the LEX shader
    /// </summary>
    public void MarkGameShadersFailed(Exception e)
    {
        gameShaderError = e.Message;
    }

    /// <summary>
    /// Loads the game's shaders for this material, if they haven't been already. Thread-safe.
    /// </summary>
    public void LoadGameShaders()
    {
        if (AttemptedShaderLoad) return;
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

        //if a MIC had a StaticPermutationResource, the shaders came from that instead
        bool isShaderMapOwner = !FoundShaderMapOwner;
        if (isShaderMapOwner)
        {
            FoundShaderMapOwner = true;
            foreach (int uIndex in parsedMaterial.SM3MaterialResource.UniformExpressionTextures)
            {
                Uniform2DTextureExpressions.Add(mat.FileRef.GetEntry(uIndex)?.InstancedFullPath);
            }
        }

        UseHairPass = props.GetProp<BoolProperty>("bHairPass") is { Value: true };
        IsTwoSided = props.GetProp<BoolProperty>("TwoSided") is { Value: true };
        IsUnlit = props.GetProp<EnumProperty>("LightingModel") is {} lightingModelProp && lightingModelProp.Value == "MLM_Unlit";

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
            (ShaderMap, Shader[] shaders) = ShaderCacheManipulator.GetMaterialShaderMapAndShaders(mat, Context.GetSeekFreeShaderCache,
                GetShaderTypeNames(LightMapType));

            (UnrealVertexShader, UnrealPixelShader) = SelectShaders(shaders, IsUnlit, LightMapType, out bool usesLightMap);
            UsesLightMap = usesLightMap;
            if (!IsUnlit)
            {
                (NoLightMapVertexShader, NoLightMapPixelShader) = SelectShaders(shaders, isUnlit: true);
            }
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
        lock (LightShaders)
        {
            if (LightShaders.TryGetValue((lightType, shadowing), out (Shader, Shader) lightShaders))
            {
                return lightShaders;
            }
            string lightPolicy = lightType switch
            {
                SceneLightType.Spot => "FSpotLightPolicy",
                _ => "FPointLightPolicy",
            };
            string shadowingPolicy = shadowing switch
            {
                StaticShadowingType.ShadowTexture => "FShadowTexturePolicy",
                StaticShadowingType.DistanceFieldShadowTexture => "FSignedDistanceFieldShadowTexturePolicy",
                StaticShadowingType.ShadowVertexBuffer => "FShadowVertexBufferPolicy",
                _ => "FNoStaticShadowingPolicy",
            };
            string vertexShaderType = $"TLightVertexShader{lightPolicy}{shadowingPolicy}";
            string pixelShaderType = $"TLightPixelShader{lightPolicy}{shadowingPolicy}";
            lightShaders = (null, null);
            if (LightShaderMapOwner is not null)
            {
                try
                {
                    (_, Shader[] shaders) = ShaderCacheManipulator.GetMaterialShaderMapAndShaders(LightShaderMapOwner, Context.GetSeekFreeShaderCache,
                        [vertexShaderType, pixelShaderType]);
                    Shader vertexShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == vertexShaderType);
                    Shader pixelShader = shaders.FirstOrDefault(shader => shader?.ShaderType.Name == pixelShaderType);
                    if (vertexShader is not null && pixelShader is not null)
                    {
                        lightShaders = (vertexShader, pixelShader);
                        if (Context.Device is not null)
                        {
                            Context.GetCachedVertexShader(vertexShader.Guid, vertexShader.ShaderByteCode);
                            Context.GetCachedPixelShader(pixelShader.Guid, pixelShader.ShaderByteCode);
                        }
                    }
                }
                catch
                {
                    //the light just isn't rendered on this material
                }
            }
            LightShaders.Add((lightType, shadowing), lightShaders);
            return lightShaders;
        }
    }

    protected override void ReadMaterialInstanceConstant(ExportEntry matInst, PropertyCollection props)
    {
        base.ReadMaterialInstanceConstant(matInst, props);

        if (!Game.IsLEGame()) return;

        if (props.GetProp<ArrayProperty<StructProperty>>("ScalarParameterValues") is { } scalarValues)
        {
            foreach (StructProperty scalarValue in scalarValues)
            {
                if (scalarValue.GetProp<NameProperty>("ParameterName") is { } paramNameProp
                    && scalarValue.GetProp<FloatProperty>("ParameterValue") is { } valProp)
                {
                    ScalarParameterValues[paramNameProp.Value.Instanced] = valProp.Value;
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
                    VectorParameterValues[paramNameProp.Value.Instanced] = CommonStructs.GetLinearColor(valProp);
                }
            }
        }
        if (props.GetProp<ArrayProperty<StructProperty>>("TextureParameterValues") is { } textureValues)
        {
            foreach (StructProperty textureValue in textureValues)
            {
                if (textureValue.GetProp<NameProperty>("ParameterName") is { } paramNameProp
                    && textureValue.GetProp<ObjectProperty>("ParameterValue") is { } valProp)
                {
                    TextureParameterValues[paramNameProp.Value.Instanced] = valProp.ResolveToEntry(matInst.FileRef)?.InstancedFullPath;
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
            foreach (int uIndex in binary.SM3StaticPermutationResource.UniformExpressionTextures)
            {
                Uniform2DTextureExpressions.Add(matInst.FileRef.GetEntry(uIndex)?.InstancedFullPath);
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
    public void UpdateShaderParams(Span<byte> vertexConstantBuffer, Span<byte> pixelConstantBuffer, MeshRenderContext context, Mesh<LEVertex> mesh,
        Shader vertexShader, Shader pixelShader, MeshStaticLighting staticLighting, bool usePreviewLighting)
    {

        vertexConstantBuffer.Clear();
        pixelConstantBuffer.Clear();
        ShaderParameterSetters.WriteBasePassVertexShaderValues(vertexShader, vertexConstantBuffer, context, mesh, this, staticLighting);
        ShaderParameterSetters.WriteBasePassPixelShaderValues(pixelShader, pixelConstantBuffer, context, mesh, this, staticLighting, usePreviewLighting);

    }

    /// <summary>
    /// Writes the parameters of the shaders that render a light on this material (see <see cref="GetLightShaders"/>)
    /// </summary>
    public void UpdateLightPassShaderParams(Span<byte> vertexConstantBuffer, Span<byte> pixelConstantBuffer, MeshRenderContext context, Mesh<LEVertex> mesh,
        Shader vertexShader, Shader pixelShader, LightInteraction interaction)
    {
        vertexConstantBuffer.Clear();
        pixelConstantBuffer.Clear();
        ShaderParameterSetters.WriteLightVertexShaderValues(vertexShader, vertexConstantBuffer, context, mesh, this, interaction);
        ShaderParameterSetters.WriteLightPixelShaderValues(pixelShader, pixelConstantBuffer, context, mesh, this, interaction);
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
