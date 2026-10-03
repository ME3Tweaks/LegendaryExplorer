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
    /// Every shader type that <see cref="SelectShaders"/> can choose from
    /// </summary>
    internal static readonly string[] ShaderTypeNames = LitShaderTypes.Concat(UnlitShaderTypes)
        .SelectMany(types => new[] { types.VertexShaderType, types.PixelShaderType })
        .Distinct().ToArray();

    /// <summary>
    /// Picks the vertex and pixel shader to render with. Lit materials use a lit pair if they have one, otherwise they fall back to an unlit pair.
    /// </summary>
    /// <param name="shaders">A material's shaders. Nulls are ignored</param>
    /// <returns>Nulls if the material has no usable pair</returns>
    internal static (Shader vertexShader, Shader pixelShader) SelectShaders(IEnumerable<Shader> shaders, bool isUnlit)
    {
        var shadersByType = new Dictionary<string, Shader>();
        foreach (Shader shader in shaders)
        {
            if (shader is not null)
            {
                shadersByType.TryAdd(shader.ShaderType.Name, shader);
            }
        }

        IEnumerable<BasePassShaderTypes> candidates = isUnlit ? UnlitShaderTypes : LitShaderTypes.Concat(UnlitShaderTypes);
        foreach (BasePassShaderTypes types in candidates)
        {
            if (shadersByType.TryGetValue(types.VertexShaderType, out Shader vertexShader)
                && shadersByType.TryGetValue(types.PixelShaderType, out Shader pixelShader))
            {
                return (vertexShader, pixelShader);
            }
        }
        return (null, null);
    }

    public EBlendMode BlendMode;
    public bool UseHairPass;
    public bool IsUnlit;
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
        else
        {
            context.AddPendingGameShaderLoad(this);
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
                ShaderTypeNames);

            (UnrealVertexShader, UnrealPixelShader) = SelectShaders(shaders, IsUnlit);
        }
        catch (Exception e)
        {
            ShaderMap = null;
            UnrealVertexShader = null;
            UnrealPixelShader = null;
            gameShaderError = $"Failed to load shaders for {mat.InstancedFullPath}: {e.Message}";
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

    public void UpdateShaderParams(Span<byte> vertexConstantBuffer, Span<byte> pixelConstantBuffer, MeshRenderContext context, Mesh<LEVertex> mesh)
    {
        //Span<byte> vertBufferBytes = [0, 80, 175, 250, 100, 2, 0, 0, 96, 241, 173, 250, 100, 2, 0, 0, 160, 112, 80, 85, 249, 127, 0, 0, 160, 112, 80, 85, 249, 127, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 24, 94, 80, 85, 249, 127, 0, 0, 24, 94, 80, 85, 249, 127, 0, 0, 128, 91, 80, 85, 249, 127, 0, 0, 128, 91, 80, 85, 249, 127, 0, 0, 168, 91, 80, 85, 249, 127, 0, 0, 168, 91, 80, 85, 249, 127, 0, 0, 56, 91, 80, 85, 249, 127, 0, 0, 56, 91, 80, 85, 249, 127, 0, 0, 160, 93, 80, 85, 249, 127, 0, 0, 160, 93, 80, 85, 249, 127, 0, 0, 200, 93, 80, 85, 249, 127, 0, 0, 200, 93, 80, 85, 249, 127, 0, 0, 240, 93, 80, 85, 249, 127, 0, 0, 240, 93, 80, 85, 249, 127, 0, 0, 112, 94, 80, 85, 249, 127, 0, 0, 112, 94, 80, 85, 249, 127, 0, 0, 152, 94, 80, 85, 249, 127, 0, 0, 152, 94, 80, 85, 249, 127, 0, 0, 192, 94, 80, 85, 249, 127, 0, 0, 192, 94, 80, 85, 249, 127, 0, 0, 88, 98, 80, 85, 249, 127, 0, 0, 88, 98, 80, 85, 249, 127, 0, 0, 192, 98, 80, 85, 249, 127, 0, 0, 192, 98, 80, 85, 249, 127, 0, 0, 8, 99, 80, 85, 249, 127, 0, 0, 8, 99, 80, 85, 249, 127, 0, 0, 80, 99, 80, 85, 249, 127, 0, 0, 80, 99, 80, 85, 249, 127, 0, 0, 192, 104, 80, 85, 249, 127, 0, 0, 192, 104, 80, 85, 249, 127, 0, 0, 248, 104, 80, 85, 249, 127, 0, 0, 248, 104, 80, 85, 249, 127, 0, 0, 48, 105, 80, 85, 249, 127, 0, 0, 48, 105, 80, 85, 249, 127, 0, 0, 208, 108, 80, 85, 249, 127, 0, 0, 208, 108, 80, 85, 249, 127, 0, 0, 8, 109, 80, 85, 249, 127, 0, 0, 8, 109, 80, 85, 249, 127, 0, 0, 48, 109, 80, 85, 249, 127, 0, 0, 48, 109, 80, 85, 249, 127, 0, 0, 104, 109, 80, 85, 249, 127, 0, 0, 104, 109, 80, 85, 249, 127, 0, 0, 144, 109, 80, 85, 249, 127, 0, 0, 144, 109, 80, 85, 249, 127, 0, 0, 200, 109, 80, 85, 249, 127, 0, 0, 200, 109, 80, 85, 249, 127, 0, 0, 240, 109, 80, 85, 249, 127, 0, 0, 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 63, 8, 91, 80, 85, 249, 127, 0, 0, 32, 91, 80, 85, 249, 127, 0, 0, 32, 91, 80, 85, 249, 127, 0, 0, 208, 91, 80, 85, 249, 127, 0, 0, 208, 91, 80, 85, 249, 127, 0, 0, 232, 91, 80, 85, 249, 127, 0, 0, 0, 0, 128, 63, 249, 127, 0, 0, 0, 92, 80, 85, 249, 127, 0, 0, 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 249, 127, 0, 0, 96, 92, 80, 85, 249, 127, 0, 0, 96, 92, 80, 85, 249, 127, 0, 0, 120, 92, 80, 85, 249, 127, 0, 0, 120, 92, 80, 85, 249, 127, 0, 0, 144, 92, 80, 85, 249, 127, 0, 0, 144, 92, 80, 85, 249, 127, 0, 0, 184, 92, 80, 85, 249, 127, 0, 0, 184, 92, 80, 85, 249, 127, 0, 0, 224, 92, 80, 85, 249, 127, 0, 0, 224, 92, 80, 85, 249, 127, 0, 0, 248, 92, 80, 85, 249, 127, 0, 0, 248, 92, 80, 85, 249, 127, 0, 0, 16, 93, 80, 85, 249, 127, 0, 0, 16, 93, 80, 85, 249, 127, 0, 0, 40, 93, 80, 85, 249, 127, 0, 0, 40, 93, 80, 85, 249, 127, 0, 0, 64, 93, 80, 85, 249, 127, 0, 0, 64, 93, 80, 85, 249, 127, 0, 0, 88, 93, 80, 85, 249, 127, 0, 0, 88, 93, 80, 85, 249, 127, 0, 0, 112, 93, 80, 85, 249, 127, 0, 0, 112, 93, 80, 85, 249, 127, 0, 0, 136, 93, 80, 85, 249, 127, 0, 0, 136, 93, 80, 85, 249, 127, 0, 0, 64, 94, 80, 85, 249, 127, 0, 0, 64, 94, 80, 85, 249, 127, 0, 0, 88, 94, 80, 85, 249, 127, 0, 0, 88, 94, 80, 85, 249, 127, 0, 0, 232, 94, 80, 85, 249, 127, 0, 0, 232, 94, 80, 85, 249, 127, 0, 0, 0, 95, 80, 85, 249, 127, 0, 0, 0, 95, 80, 85, 249, 127, 0, 0, 24, 95, 80, 85, 249, 127, 0, 0, 24, 95, 80, 85, 249, 127, 0, 0, 64, 95, 80, 85, 249, 127, 0, 0, 64, 95, 80, 85, 249, 127, 0, 0, 104, 95, 80, 85, 249, 127, 0, 0, 104, 95, 80, 85, 249, 127, 0, 0, 144, 95, 80, 85, 249, 127, 0, 0, 144, 95, 80, 85, 249, 127, 0, 0, 184, 95, 80, 85, 249, 127, 0, 0, 184, 95, 80, 85, 249, 127, 0, 0, 208, 95, 80, 85, 249, 127, 0, 0, 208, 95, 80, 85, 249, 127, 0, 0, 248, 95, 80, 85, 249, 127, 0, 0, 248, 95, 80, 85, 249, 127, 0, 0, 32, 96, 80, 85, 249, 127, 0, 0, 32, 96, 80, 85, 249, 127, 0, 0, 56, 96, 80, 85, 249, 127, 0, 0, 56, 96, 80, 85, 249, 127, 0, 0, 80, 96, 80, 85, 249, 127, 0, 0, 80, 96, 80, 85, 249, 127, 0, 0, 120, 96, 80, 85, 249, 127, 0, 0, 120, 96, 80, 85, 249, 127, 0, 0, 160, 96, 80, 85, 249, 127, 0, 0, 160, 96, 80, 85, 249, 127, 0, 0, 200, 96, 80, 85, 249, 127, 0, 0, 200, 96, 80, 85, 249, 127, 0, 0, 240, 96, 80, 85, 249, 127, 0, 0, 240, 96, 80, 85, 249, 127, 0, 0, 24, 97, 80, 85, 249, 127, 0, 0, 24, 97, 80, 85, 249, 127, 0, 0, 64, 97, 80, 85, 249, 127, 0, 0, 64, 97, 80, 85, 249, 127, 0, 0, 104, 97, 80, 85, 249, 127, 0, 0, 104, 97, 80, 85, 249, 127, 0, 0, 144, 97, 80, 85, 249, 127, 0, 0, 144, 97, 80, 85, 249, 127, 0, 0, 184, 97, 80, 85, 249, 127, 0, 0, 184, 97, 80, 85, 249, 127, 0, 0, 224, 97, 80, 85, 249, 127, 0, 0, 224, 97, 80, 85, 249, 127, 0, 0, 8, 98, 80, 85, 249, 127, 0, 0, 8, 98, 80, 85, 249, 127, 0, 0, 48, 98, 80, 85, 249, 127, 0, 0, 48, 98, 80, 85, 249, 127, 0, 0, 152, 99, 80, 85, 249, 127, 0, 0, 152, 99, 80, 85, 249, 127, 0, 0, 192, 99, 80, 85, 249, 127, 0, 0, 192, 99, 80, 85, 249, 127, 0, 0, 232, 99, 80, 85, 249, 127, 0, 0, 232, 99, 80, 85, 249, 127, 0, 0, 16, 100, 80, 85, 249, 127, 0, 0, 16, 100, 80, 85, 249, 127, 0, 0, 56, 100, 80, 85, 249, 127, 0, 0, 56, 100, 80, 85, 249, 127, 0, 0, 96, 100, 80, 85, 249, 127, 0, 0, 96, 100, 80, 85, 249, 127, 0, 0, 136, 100, 80, 85, 249, 127, 0, 0, 136, 100, 80, 85, 249, 127, 0, 0, 176, 100, 80, 85, 249, 127, 0, 0, 176, 100, 80, 85, 249, 127, 0, 0, 216, 100, 80, 85, 249, 127, 0, 0, 216, 100, 80, 85, 249, 127, 0, 0, 0, 101, 80, 85, 249, 127, 0, 0, 0, 101, 80, 85, 249, 127, 0, 0, 40, 101, 80, 85, 249, 127, 0, 0, 40, 101, 80, 85, 249, 127, 0, 0, 80, 101, 80, 85, 249, 127, 0, 0, 80, 101, 80, 85, 249, 127, 0, 0, 120, 101, 80, 85, 249, 127, 0, 0, 120, 101, 80, 85, 249, 127, 0, 0, 160, 101, 80, 85, 249, 127, 0, 0, 160, 101, 80, 85, 249, 127, 0, 0, 200, 101, 80, 85, 249, 127, 0, 0, 200, 101, 80, 85, 249, 127, 0, 0, 240, 101, 80, 85, 249, 127, 0, 0, 240, 101, 80, 85, 249, 127, 0, 0, 24, 102, 80, 85, 249, 127, 0, 0, 24, 102, 80, 85, 249, 127, 0, 0, 64, 102, 80, 85, 249, 127, 0, 0, 64, 102, 80, 85, 249, 127, 0, 0, 104, 102, 80, 85, 249, 127, 0, 0, 104, 102, 80, 85, 249, 127, 0, 0, 144, 102, 80, 85, 249, 127, 0, 0, 144, 102, 80, 85, 249, 127, 0, 0, 184, 102, 80, 85, 249, 127, 0, 0, 184, 102, 80, 85, 249, 127, 0, 0, 224, 102, 80, 85, 249, 127, 0, 0, 224, 102, 80, 85, 249, 127, 0, 0, 8, 103, 80, 85, 249, 127, 0, 0, 8, 103, 80, 85, 249, 127, 0, 0, 48, 103, 80, 85, 249, 127, 0, 0, 48, 103, 80, 85, 249, 127, 0, 0, 88, 103, 80, 85, 249, 127, 0, 0, 88, 103, 80, 85, 249, 127, 0, 0, 128, 103, 80, 85, 249, 127, 0, 0, 128, 103, 80, 85, 249, 127, 0, 0, 168, 103, 80, 85, 249, 127, 0, 0, 168, 103, 80, 85, 249, 127, 0, 0, 208, 103, 80, 85, 249, 127, 0, 0, 208, 103, 80, 85, 249, 127, 0, 0, 248, 103, 80, 85, 249, 127, 0, 0, 248, 103, 80, 85, 249, 127, 0, 0, 32, 104, 80, 85, 249, 127, 0, 0, 32, 104, 80, 85, 249, 127, 0, 0, 72, 104, 80, 85, 249, 127, 0, 0, 72, 104, 80, 85, 249, 127, 0, 0, 112, 104, 80, 85, 249, 127, 0, 0, 112, 104, 80, 85, 249, 127, 0, 0, 152, 104, 80, 85, 249, 127, 0, 0, 152, 104, 80, 85, 249, 127, 0, 0, 104, 105, 80, 85, 249, 127, 0, 0, 104, 105, 80, 85, 249, 127, 0, 0, 144, 105, 80, 85, 249, 127, 0, 0, 144, 105, 80, 85, 249, 127, 0, 0, 184, 105, 80, 85, 249, 127, 0, 0, 184, 105, 80, 85, 249, 127, 0, 0, 224, 105, 80, 85, 249, 127, 0, 0, 224, 105, 80, 85, 249, 127, 0, 0, 8, 106, 80, 85, 249, 127, 0, 0, 8, 106, 80, 85, 249, 127, 0, 0, 48, 106, 80, 85, 249, 127, 0, 0, 48, 106, 80, 85, 249, 127, 0, 0, 88, 106, 80, 85, 249, 127, 0, 0, 88, 106, 80, 85, 249, 127, 0, 0, 128, 106, 80, 85, 249, 127, 0, 0, 128, 106, 80, 85, 249, 127, 0, 0, 200, 106, 80, 85, 249, 127, 0, 0, 200, 106, 80, 85, 249, 127, 0, 0, 240, 106, 80, 85, 249, 127, 0, 0, 240, 106, 80, 85, 249, 127, 0, 0, 24, 107, 80, 85, 249, 127, 0, 0, 24, 107, 80, 85, 249, 127, 0, 0, 64, 107, 80, 85, 249, 127, 0, 0, 64, 107, 80, 85, 249, 127, 0, 0, 104, 107, 80, 85, 249, 127, 0, 0, 104, 107, 80, 85, 249, 127, 0, 0, 144, 107, 80, 85, 249, 127, 0, 0, 144, 107, 80, 85, 249, 127, 0, 0, 184, 107, 80, 85, 249, 127, 0, 0, 184, 107, 80, 85, 249, 127, 0, 0, 224, 107, 80, 85, 249, 127, 0, 0, 224, 107, 80, 85, 249, 127, 0, 0, 8, 108, 80, 85, 249, 127, 0, 0, 8, 108, 80, 85, 249, 127, 0, 0, 48, 108, 80, 85, 249, 127, 0, 0, 48, 108, 80, 85, 249, 127, 0, 0, 88, 108, 80, 85, 249, 127, 0, 0, 88, 108, 80, 85, 249, 127, 0, 0, 128, 108, 80, 85, 249, 127, 0, 0, 128, 108, 80, 85, 249, 127, 0, 0, 168, 108, 80, 85, 249, 127, 0, 0, 168, 108, 80, 85, 249, 127, 0, 0, 152, 110, 80, 85, 249, 127, 0, 0, 152, 110, 80, 85, 249, 127, 0, 0, 192, 110, 80, 85, 249, 127, 0, 0, 192, 110, 80, 85, 249, 127, 0, 0, 232, 110, 80, 85, 249, 127, 0, 0, 232, 110, 80, 85, 249, 127, 0, 0, 16, 111, 80, 85, 249, 127, 0, 0, 16, 111, 80, 85, 249, 127, 0, 0, 56, 111, 80, 85, 249, 127, 0, 0, 56, 111, 80, 85, 249, 127, 0, 0, 96, 111, 80, 85, 249, 127, 0, 0, 96, 111, 80, 85, 249, 127, 0, 0, 136, 111, 80, 85, 249, 127, 0, 0, 136, 111, 80, 85, 249, 127, 0, 0, 176, 111, 80, 85, 249, 127, 0, 0, 176, 111, 80, 85, 249, 127, 0, 0, 216, 111, 80, 85, 249, 127, 0, 0, 216, 111, 80, 85, 249, 127, 0, 0, 0, 112, 80, 85, 249, 127, 0, 0, 0, 112, 80, 85, 249, 127, 0, 0, 40, 112, 80, 85, 249, 127, 0, 0, 40, 112, 80, 85, 249, 127, 0, 0, 80, 112, 80, 85, 249, 127, 0, 0, 80, 112, 80, 85, 249, 127, 0, 0, 120, 112, 80, 85, 249, 127, 0, 0, 120, 112, 80, 85, 249, 127, 0, 0, 88, 91, 80, 85, 249, 127, 0, 0, 88, 91, 80, 85, 249, 127, 0, 0, 144, 91, 80, 85, 249, 127, 0, 0, 144, 91, 80, 85, 249, 127, 0, 0, 184, 91, 80, 85, 249, 127, 0, 0, 184, 91, 80, 85, 249, 127, 0, 0, 176, 93, 80, 85, 249, 127, 0, 0, 176, 93, 80, 85, 249, 127, 0, 0, 216, 93, 80, 85, 249, 127, 0, 0, 216, 93, 80, 85, 249, 127, 0, 0, 0, 94, 80, 85, 249, 127, 0, 0, 0, 94, 80, 85, 249, 127, 0, 0, 40, 94, 80, 85, 249, 127, 0, 0, 40, 94, 80, 85, 249, 127, 0, 0, 128, 94, 80, 85, 249, 127, 0, 0, 128, 94, 80, 85, 249, 127, 0, 0, 168, 94, 80, 85, 249, 127, 0, 0, 168, 94, 80, 85, 249, 127, 0, 0, 208, 94, 80, 85, 249, 127, 0, 0, 208, 94, 80, 85, 249, 127, 0, 0, 120, 98, 80, 85, 249, 127, 0, 0, 120, 98, 80, 85, 249, 127, 0, 0, 224, 98, 80, 85, 249, 127, 0, 0, 224, 98, 80, 85, 249, 127, 0, 0, 40, 99, 80, 85, 249, 127, 0, 0, 40, 99, 80, 85, 249, 127, 0, 0, 112, 99, 80, 85, 249, 127, 0, 0, 112, 99, 80, 85, 249, 127, 0, 0, 54, 0, 0, 54, 96, 148, 17, 0, 176, 252, 123, 251, 100, 2, 0, 0, 160, 212, 156, 93, 100, 2, 0, 0];
        ////Span<byte> pixelBufferBytes = [80, 1, 185, 91, 36, 2, 0, 0, 96, 241, 173, 250, 100, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 15, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 7, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 7, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 154, 21, 124, 189, 64, 58, 32, 189, 131, 81, 127, 191, 197, 131, 127, 191, 146, 19, 30, 59, 5, 228, 123, 61, 0, 0, 0, 0, 167, 205, 127, 63, 39, 136, 32, 189, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 63, 57, 151, 2, 62, 224, 190, 134, 62, 193, 2, 76, 63, 0, 0, 112, 65, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        //vertBufferBytes.CopyTo(vertexConstantBuffer);
        ////pixelBufferBytes.CopyTo(pixelConstantBuffer);
        //if (InstancedFullPath is "BIOG_HMM_HED_PROMorph.Sheppard.HMM_HED_PROSheppard_Face_Mat_1a")
        //{
        //    vertexConstantBuffer.Slice(672).Clear();
        //}
        vertexConstantBuffer.Clear();
        pixelConstantBuffer.Clear();
        ShaderParameterSetters.WriteBasePassVertexShaderValues(UnrealVertexShader, vertexConstantBuffer, context, mesh, this);
        ShaderParameterSetters.WriteBasePassPixelShaderValues(UnrealPixelShader, pixelConstantBuffer, context, mesh, this);
        //System.Diagnostics.Debug.WriteLine(string.Join(',', vertexConstantBuffer.ToArray()));
        //System.Diagnostics.Debug.WriteLine(string.Join(',', pixelConstantBuffer.ToArray()));
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
