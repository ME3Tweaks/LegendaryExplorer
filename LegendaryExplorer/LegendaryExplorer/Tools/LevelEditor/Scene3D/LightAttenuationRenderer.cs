using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Renders each light's attenuation across the screen, which its light passes multiply its contribution by (their LightAttenuationTexture), as UE3's light attenuation buffer:
/// <list type="bullet">
/// <item>The dynamic shadows of the movable meshes it lights (UE3's per-object projected shadows, FProjectedShadowInfo). Each caster's depth is rendered from the light
/// into a shadow map, then projected onto the scene with the game's TShadowProjectionPixelShader, masked by the stencil to the pixels inside the shadow's frustum.</item>
/// <item>Its light function (FSceneRenderer::RenderLightFunction): a material evaluated over the whole screen in the light's space with its FLightFunctionPixelShader.</item>
/// </list>
/// Both reconstruct each pixel's position from the scene's depth, which is first copied into a single-sampled texture.
/// It also renders light environments' modulated shadows (<see cref="RenderModulatedShadows"/>), which are projected the same way, but darken the lit scene directly.
/// </summary>
public sealed class LightAttenuationRenderer : IDisposable
{
    private const string ShaderCode = """
        struct FullscreenOutput
        {
            float4 ScreenPosition : TEXCOORD0;
            float4 Position : SV_Position;
        };

        //one triangle that covers the screen. Like FLightFunctionVertexShader and FShadowProjectionVertexShader, ScreenPosition is the clip space position
        FullscreenOutput VSMain(uint vertexId : SV_VertexID)
        {
            float2 position = float2((vertexId << 1) & 2, vertexId & 2) * 2 - 1;
            FullscreenOutput output;
            output.ScreenPosition = float4(position, 0, 1);
            output.Position = output.ScreenPosition;
            return output;
        }

        #if MSAA_SAMPLES > 1
        Texture2DMS<float> SceneDepth : register(t0);
        #else
        Texture2D<float> SceneDepth : register(t0);
        #endif
        Texture2D<float> SceneDepthCopy : register(t1);

        //copies the first sample of the depth buffer into a texture that can be sampled
        float PSCopyDepth(FullscreenOutput input) : SV_Target
        {
            int2 pixel = int2(input.Position.xy);
        #if MSAA_SAMPLES > 1
            return SceneDepth.Load(pixel, 0);
        #else
            return SceneDepth.Load(int3(pixel, 0));
        #endif
        }

        //fills the depth of the single-sampled depth-stencil buffer that shadows are masked with
        float PSWriteDepth(FullscreenOutput input) : SV_Depth
        {
            return SceneDepthCopy.Load(int3(int2(input.Position.xy), 0));
        }

        Texture2D<float4> Modulation : register(t0);

        //multiplied into the scene color, each of whose samples is darkened the same
        float4 PSApplyModulation(FullscreenOutput input) : SV_Target
        {
            return Modulation.Load(int3(int2(input.Position.xy), 0));
        }

        cbuffer ShadowVolumeConstants : register(b0)
        {
            row_major float4x4 ViewProjection;
            float4 FrustumCorners[8];
        };

        //the 12 triangles of a box whose corners are indexed by (x, y, z) bits, all wound the same way
        static const uint FrustumIndices[36] =
        {
            0, 2, 6, 0, 6, 4,
            1, 5, 7, 1, 7, 3,
            0, 4, 5, 0, 5, 1,
            2, 3, 7, 2, 7, 6,
            0, 1, 3, 0, 3, 2,
            4, 6, 7, 4, 7, 5,
        };

        float4 VSShadowVolume(uint vertexId : SV_VertexID) : SV_Position
        {
            return mul(float4(FrustumCorners[FrustumIndices[vertexId]].xyz, 1), ViewProjection);
        }

        cbuffer ShadowDepthConstants : register(b0)
        {
            row_major float4x4 LocalToWorld;
            row_major float4x4 WorldToShadow;
            float InvMaxSubjectDepth;
            float DepthBias;
        };

        struct ShadowDepthOutput
        {
            float4 Position : SV_Position;
            float Depth : TEXCOORD0;
        };

        //like TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>: the shadow map holds the shadow space Z, normalized by the subject's depth range
        ShadowDepthOutput VSShadowDepth(float4 Position : POSITION)
        {
            ShadowDepthOutput output;
            output.Position = mul(mul(float4(Position.xyz, 1), LocalToWorld), WorldToShadow);
            output.Depth = output.Position.z;
            return output;
        }

        float PSShadowDepth(ShadowDepthOutput input) : SV_Depth
        {
            return saturate(input.Depth * InvMaxSubjectDepth + DepthBias);
        }
        """;

    [StructLayout(LayoutKind.Sequential)]
    private struct ShadowVolumeConstants
    {
        public Matrix4x4 ViewProjection;
        public Vector4 Corner0, Corner1, Corner2, Corner3, Corner4, Corner5, Corner6, Corner7;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShadowDepthConstants
    {
        public Matrix4x4 LocalToWorld;
        public Matrix4x4 WorldToShadow;
        public float InvMaxSubjectDepth;
        public float DepthBias;
        public Vector2 Padding;
    }

    private const string ProjectionShaderType = "TShadowProjectionPixelShader<F4SampleHwPCF>";
    //Light environments' shadow lights are point lights. The same filter as ProjectionShaderType
    private const string ModShadowProjectionShaderType = "TModShadowProjectionPixelShaderFPointLightPolicyF4SampleHwPCF";
    //The global shader caches are small, but each is parsed once. Only complete loads are cached here, since a missing cache may appear once the game's path is set
    private static readonly ConcurrentDictionary<MEGame, (TShadowProjectionPixelShader, TModShadowProjectionPixelShader<FPointLightPolicy.ModShadowPixelParamsType>)> ProjectionShaders = new();
    //games whose projection shaders couldn't all be loaded (either may be null), so it isn't retried every frame. Forgotten by ClearMaterials
    private readonly Dictionary<MEGame, (TShadowProjectionPixelShader, TModShadowProjectionPixelShader<FPointLightPolicy.ModShadowPixelParamsType>)> IncompleteProjectionShaders = [];

    private delegate void WriteProjectionParameters(Span<byte> buffer);

    private readonly MeshRenderContext Context;

    //device resources
    private VertexShader FullscreenVertexShader;
    private PixelShader CopyDepthPixelShader;
    private PixelShader WriteDepthPixelShader;
    private PixelShader ApplyModulationPixelShader;
    private VertexShader ShadowVolumeVertexShader;
    private VertexShader ShadowDepthVertexShader;
    private InputLayout ShadowDepthInputLayout;
    private PixelShader ShadowDepthPixelShader;
    private SamplerState PointClampSampler;
    private SamplerState ShadowComparisonSampler;
    private SharpDX.Direct3D11.Buffer ShadowVolumeConstantBuffer;
    private SharpDX.Direct3D11.Buffer ShadowDepthConstantBuffer;
    private BlendState MultiplyBlendState;
    private DepthStencilState WriteDepthState;
    private DepthStencilState ShadowDepthState;
    private DepthStencilState ShadowVolumeState;
    private DepthStencilState ShadowProjectionState;
    private RasterizerState NoCullRasterizerState;
    private Texture2D ShadowDepthBuffer;
    private DepthStencilView ShadowDepthBufferView;
    private ShaderResourceView ShadowDepthBufferResourceView;

    //size-dependent
    private int Width;
    private int Height;
    private Texture2D SceneDepthCopy;
    private RenderTargetView SceneDepthCopyTarget;
    private ShaderResourceView SceneDepthCopyView;
    private (Texture2D Texture, RenderTargetView Target, ShaderResourceView View) ModulationTarget;
    private Texture2D ShadowMaskDepthStencil;
    private DepthStencilView ShadowMaskDepthStencilView;

    //Holds one light's attenuation at a time: the light passes are rendered grouped by light (see MeshRenderContext.EndLightingPass), as UE3 renders them
    private (Texture2D Texture, RenderTargetView Target, ShaderResourceView View) AttenuationTarget;

    //what's been rendered this frame
    private uint Frame = uint.MaxValue;
    private bool CopiedDepth;
    private bool FilledShadowMask;
    private bool FoundShadowCasters;
    //the light whose attenuation AttenuationTarget holds this frame
    private SceneLight AttenuationLight;
    private readonly Dictionary<SceneLight, List<(MeshStaticLighting Caster, BoxSphereBounds Bounds)>> ShadowCasters = [];
    //lights whose attenuation failed to render, so it isn't retried every frame. Forgotten by ClearMaterials
    private readonly HashSet<SceneLight> FailedLights = [];

    private static readonly ShaderResourceView[] NoShaderResources = new ShaderResourceView[CommonShaderStage.InputResourceSlotCount];

    private readonly Dictionary<ExportEntry, MaterialRenderProxy> LightFunctionMaterials = [];

    public LightAttenuationRenderer(MeshRenderContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Whether the light can have an attenuation: it has a light function, or casts dynamic shadows
    /// </summary>
    public static bool MayHaveAttenuation(SceneLight light) => light.LightFunctionMaterial is not null || CastsPerObjectShadows(light);

    /// <summary>
    /// InitDynamicShadows: lights without static shadowing that cast dynamic shadows get whole scene shadows instead if they're spot or directional lights,
    /// which aren't supported. Sky lights don't cast shadows
    /// </summary>
    private static bool CastsPerObjectShadows(SceneLight light) => light.CastsDynamicShadows && light.Type switch
    {
        SceneLightType.Point => true,
        SceneLightType.Spot or SceneLightType.Directional => light.HasStaticShadowing,
        _ => false
    };

    /// <summary>
    /// Renders the light's attenuation, if it isn't the one already rendered this frame. Changes the render targets, viewport, render states, pixel shader resources and samplers.
    /// The texture is only valid until the next light's attenuation is rendered
    /// </summary>
    /// <param name="sceneDepth">The scene's depth buffer (multisampled if <see cref="MeshRenderContext.SampleCount"/> is more than 1). Must not be bound as the depth target</param>
    /// <returns>The light's attenuation, the same size as the screen. Null if it has none, or it failed to render before</returns>
    public ShaderResourceView GetAttenuation(SceneLight light, ShaderResourceView sceneDepth)
    {
        BeginFrame();
        if (AttenuationLight == light)
        {
            return AttenuationTarget.View;
        }
        if (FailedLights.Contains(light))
        {
            return null;
        }
        try
        {
            return RenderAttenuation(light, sceneDepth);
        }
        catch
        {
            FailedLights.Add(light);
            throw;
        }
    }

    private ShaderResourceView RenderAttenuation(SceneLight light, ShaderResourceView sceneDepth)
    {
        List<ProjectedShadowInfo> shadows = [];
        TShadowProjectionPixelShader projectionShader = null;
        if (!FoundShadowCasters)
        {
            //once a frame, even if it fails, which isn't the light's fault
            FoundShadowCasters = true;
            try
            {
                FindShadowCasters();
            }
            catch (Exception e)
            {
                ShadowCasters.Clear();
                System.Diagnostics.Debug.WriteLine($"Could not find the meshes that cast dynamic shadows, so none are rendered this frame: {e.Message}");
            }
        }
        if (ShadowCasters.TryGetValue(light, out var casters) && (projectionShader = GetProjectionShaders(light.Export.Game).Item1) is not null)
        {
            var shadowLight = ShadowLight.FromSceneLight(light);
            foreach ((MeshStaticLighting caster, BoxSphereBounds bounds) in casters)
            {
                var subject = new[] { new ShadowCaster(caster.GetLocalToWorld, caster.DrawShadowCaster) };
                if (ProjectedShadowInfo.Create(subject, shadowLight, bounds, Context.Camera, Context.Width, Context.Height) is { } shadow)
                {
                    shadows.Add(shadow);
                }
            }
        }
        MaterialRenderProxy lightFunctionMaterial = GetLightFunctionMaterial(light.LightFunctionMaterial);
        var lightFunctionShader = lightFunctionMaterial?.GetLightFunctionPixelShader() as FLightFunctionPixelShader;

        if (shadows.Count == 0 && lightFunctionShader is null)
        {
            return null;
        }
        DeviceContext ctx = Context.ImmediateContext;
        CreateResources();
        //anything that may still be bound from the last time these targets were read
        ctx.PixelShader.SetShaderResources(0, NoShaderResources);
        EnsureDepthCopied(sceneDepth);
        if (AttenuationTarget.Texture is null)
        {
            AttenuationTarget = CreateTarget(Format.B8G8R8A8_UNorm);
        }
        //it's about to be overwritten
        AttenuationLight = null;
        RenderTargetView target = AttenuationTarget.Target;
        ctx.ClearRenderTargetView(target, new RawColor4(1, 1, 1, 1));

        if (shadows.Count > 0)
        {
            EnsureShadowMaskFilled();
            PixelShader pixelShader = Context.GetCachedPixelShader(projectionShader.Guid, projectionShader.ShaderByteCode);
            foreach (ProjectedShadowInfo shadow in shadows)
            {
                RenderShadowDepth(shadow);
                RenderProjection(shadow, target, pixelShader, buffer => ShaderParameterSetters.WriteShadowProjectionValues(projectionShader, buffer, Context, shadow,
                    SceneDepthCopyView, PointClampSampler, ShadowDepthBufferResourceView, ShadowComparisonSampler));
            }
        }
        if (lightFunctionShader is not null)
        {
            RenderLightFunction(light, target, lightFunctionMaterial, lightFunctionShader);
        }
        //the light passes bind their own resources
        Context.LEEffect.PixelShaderResources.Reset();
        ctx.Rasterizer.SetViewport(0, 0, Context.Width, Context.Height);
        AttenuationLight = light;
        return AttenuationTarget.View;
    }

    /// <summary>
    /// Renders the modulated shadows of the light environments that cast them, from their shadow lights, and multiplies them into the scene color
    /// (FSceneRenderer::RenderModulatedShadows). Changes the render targets, viewport, pixel shader resources and samplers
    /// </summary>
    /// <param name="sceneDepth">The scene's depth buffer (multisampled if <see cref="MeshRenderContext.SampleCount"/> is more than 1). Must not be bound as the depth target</param>
    /// <param name="sceneColor">The lit scene color</param>
    public void RenderModulatedShadows(IEnumerable<DynamicLightEnvironment> lightEnvironments, ShaderResourceView sceneDepth, RenderTargetView sceneColor)
    {
        BeginFrame();
        List<(ProjectedShadowInfo Shadow, LightEnvironmentShadowLight ShadowLight)> shadows = [];
        TModShadowProjectionPixelShader<FPointLightPolicy.ModShadowPixelParamsType> projectionShader = null;
        foreach (DynamicLightEnvironment lightEnvironment in lightEnvironments)
        {
            //the light environment's meshes cast its shadow together, as the shadow group of the actor's components
            var subject = new List<ShadowCaster>();
            foreach (LightEnvironmentPrimitive primitive in lightEnvironment.GetPrimitives())
            {
                if (primitive.DrawShadowCaster is not null && primitive.IsInScene() && primitive.IsShown())
                {
                    subject.Add(new ShadowCaster(primitive.GetLocalToWorld, primitive.DrawShadowCaster));
                }
            }
            if (subject.Count == 0 || lightEnvironment.GetLighting() is not { ShadowLight: { } shadowLight })
            {
                continue;
            }
            MEGame game = lightEnvironment.Owner?.Game ?? lightEnvironment.Export?.Game ?? MEGame.LE3;
            if ((projectionShader ??= GetProjectionShaders(game).Item2) is null)
            {
                return;
            }
            //a point light, with the light environment's shadow resolutions
            var light = new ShadowLight(SceneLightType.Point, shadowLight.Position, Vector3.UnitX, shadowLight.Radius, ShadowRadiusMultiplier: 1.1f,
                shadowLight.MinShadowResolution, shadowLight.MaxShadowResolution, shadowLight.ShadowFadeResolution, game);
            (BoxSphereBounds bounds, _) = lightEnvironment.GetOwnerBoundsAndChannels();
            if (ProjectedShadowInfo.Create(subject, light, bounds, Context.Camera, Context.Width, Context.Height) is { } shadow)
            {
                shadows.Add((shadow, shadowLight));
            }
        }
        if (shadows.Count == 0)
        {
            return;
        }

        DeviceContext ctx = Context.ImmediateContext;
        CreateResources();
        ctx.PixelShader.SetShaderResources(0, NoShaderResources);
        EnsureDepthCopied(sceneDepth);
        EnsureShadowMaskFilled();
        if (ModulationTarget.Texture is null)
        {
            ModulationTarget = CreateTarget(Format.B8G8R8A8_UNorm);
        }
        ctx.ClearRenderTargetView(ModulationTarget.Target, new RawColor4(1, 1, 1, 1));
        PixelShader pixelShader = Context.GetCachedPixelShader(projectionShader.Guid, projectionShader.ShaderByteCode);
        foreach ((ProjectedShadowInfo shadow, LightEnvironmentShadowLight shadowLight) in shadows)
        {
            RenderShadowDepth(shadow);
            RenderProjection(shadow, ModulationTarget.Target, pixelShader, buffer => ShaderParameterSetters.WriteModShadowProjectionValues(projectionShader, buffer, Context,
                shadow, shadowLight, SceneDepthCopyView, PointClampSampler, ShadowDepthBufferResourceView, ShadowComparisonSampler));
        }
        Context.LEEffect.PixelShaderResources.Reset();
        ctx.Rasterizer.SetViewport(0, 0, Context.Width, Context.Height);

        //the shadows darken the lit scene
        ctx.OutputMerger.SetRenderTargets((DepthStencilView)null, sceneColor);
        ctx.OutputMerger.SetDepthStencilState(null);
        ctx.OutputMerger.SetBlendState(MultiplyBlendState);
        ctx.InputAssembler.InputLayout = null;
        ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        ctx.VertexShader.Set(FullscreenVertexShader);
        ctx.PixelShader.Set(ApplyModulationPixelShader);
        ctx.PixelShader.SetShaderResource(0, ModulationTarget.View);
        Context.RestoreRasterizerState();
        ctx.Draw(3, 0);
        ctx.PixelShader.SetShaderResource(0, null);
        ctx.OutputMerger.SetBlendState(null);
    }

    /// <summary>
    /// Forgets what was rendered last frame
    /// </summary>
    private void BeginFrame()
    {
        if (Frame != Context.NumFrames)
        {
            Frame = Context.NumFrames;
            CopiedDepth = false;
            FilledShadowMask = false;
            AttenuationLight = null;
            //found when a light's attenuation is first needed this frame, since modulated shadows don't use them
            ShadowCasters.Clear();
            FoundShadowCasters = false;
        }
    }

    private void EnsureDepthCopied(ShaderResourceView sceneDepth)
    {
        if (!CopiedDepth)
        {
            CopyDepth(sceneDepth);
            CopiedDepth = true;
        }
    }

    private void EnsureShadowMaskFilled()
    {
        if (!FilledShadowMask)
        {
            FillShadowMaskDepth();
            FilledShadowMask = true;
        }
    }

    /// <summary>
    /// The meshes each light casts per-object shadows from: those in the game's scene, and shown in the editor, whose shadows from it weren't precomputed
    /// (<see cref="MeshStaticLighting.CastsDynamicShadowFrom"/>)
    /// </summary>
    private void FindShadowCasters()
    {
        foreach (MeshStaticLighting staticLighting in Context.GetStaticLightings())
        {
            if (!staticLighting.IsDynamicShadowCaster || staticLighting.DrawShadowCaster is null || staticLighting.GetBounds is null || staticLighting.GetLocalToWorld is null
                || !staticLighting.IsInScene() || !staticLighting.IsShown())
            {
                continue;
            }
            BoxSphereBounds bounds = staticLighting.GetBounds();
            foreach (LightInteraction interaction in staticLighting.GetLightInteractions(bounds))
            {
                if (staticLighting.CastsDynamicShadowFrom(interaction.Light) && CastsPerObjectShadows(interaction.Light))
                {
                    if (!ShadowCasters.TryGetValue(interaction.Light, out var casters))
                    {
                        ShadowCasters[interaction.Light] = casters = [];
                    }
                    casters.Add((staticLighting, bounds));
                }
            }
        }
    }

    /// <returns>The projection shaders for shadows that attenuate a light, and for modulated shadows from a point light. Either is null if it can't be loaded</returns>
    private (TShadowProjectionPixelShader, TModShadowProjectionPixelShader<FPointLightPolicy.ModShadowPixelParamsType>) GetProjectionShaders(MEGame game)
    {
        if (ProjectionShaders.TryGetValue(game, out var shaders))
        {
            return shaders;
        }
        if (IncompleteProjectionShaders.TryGetValue(game, out shaders))
        {
            return shaders;
        }
        string error;
        try
        {
            if (MEDirectories.GetCookedPath(game) is not { } cookedPath)
            {
                error = "the game's path isn't set";
            }
            else if (Path.Combine(cookedPath, "GlobalShaderCache-PC-D3D-SM5.bin") is var path && !File.Exists(path))
            {
                error = $"{path} doesn't exist";
            }
            else
            {
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                GlobalShaderCache shaderCache = GlobalShaderCache.ReadGlobalShaderCache(stream, game);
                shaders = (shaderCache.Shaders.Values.OfType<TShadowProjectionPixelShader>().FirstOrDefault(shader => shader.ShaderType.Instanced == ProjectionShaderType),
                    shaderCache.Shaders.Values.OfType<TModShadowProjectionPixelShader<FPointLightPolicy.ModShadowPixelParamsType>>()
                        .FirstOrDefault(shader => shader.ShaderType.Instanced == ModShadowProjectionShaderType));
                if (shaders.Item1 is not null && shaders.Item2 is not null)
                {
                    ProjectionShaders[game] = shaders;
                    return shaders;
                }
                error = $"{path} has no {(shaders.Item1 is null ? ProjectionShaderType : ModShadowProjectionShaderType)}";
            }
        }
        catch (Exception e)
        {
            error = e.Message;
        }
        System.Diagnostics.Debug.WriteLine($"Could not load all of {game}'s shadow projection shaders, so some dynamic shadows won't be rendered: {error}");
        //whichever were found are still used
        IncompleteProjectionShaders[game] = shaders;
        return shaders;
    }

    /// <summary>
    /// FProjectedShadowInfo::RenderDepth: the subject's depth from the light, in the top left of the shadow depth buffer, inside its border
    /// </summary>
    private void RenderShadowDepth(ProjectedShadowInfo shadow)
    {
        DeviceContext ctx = Context.ImmediateContext;
        ctx.OutputMerger.SetRenderTargets(ShadowDepthBufferView, (RenderTargetView)null);
        ctx.ClearDepthStencilView(ShadowDepthBufferView, DepthStencilClearFlags.Depth, 1, 0);
        ctx.Rasterizer.SetViewport(ProjectedShadowInfo.ShadowBorder, ProjectedShadowInfo.ShadowBorder, shadow.Resolution, shadow.Resolution);
        ctx.Rasterizer.State = NoCullRasterizerState;
        ctx.OutputMerger.SetDepthStencilState(ShadowDepthState);
        ctx.OutputMerger.SetBlendState(null);
        ctx.InputAssembler.InputLayout = ShadowDepthInputLayout;
        ctx.VertexShader.Set(ShadowDepthVertexShader);
        ctx.PixelShader.Set(ShadowDepthPixelShader);
        ctx.VertexShader.SetConstantBuffer(0, ShadowDepthConstantBuffer);
        ctx.PixelShader.SetConstantBuffer(0, ShadowDepthConstantBuffer);
        foreach (ShadowCaster caster in shadow.Subject)
        {
            var constants = new ShadowDepthConstants
            {
                LocalToWorld = caster.GetLocalToWorld(),
                WorldToShadow = Matrix4x4.CreateTranslation(shadow.PreShadowTranslation) * shadow.SubjectMatrix,
                InvMaxSubjectDepth = 1 / shadow.MaxSubjectDepth,
                DepthBias = shadow.DepthBias,
            };
            ctx.UpdateSubresource(ref constants, ShadowDepthConstantBuffer);
            caster.Draw(ctx);
        }
        ctx.Rasterizer.SetViewport(0, 0, Context.Width, Context.Height);
    }

    /// <summary>
    /// FProjectedShadowInfo::RenderProjection: marks the pixels inside the shadow's receiver frustum in the stencil (z-fail), then multiplies the shadow into the target
    /// </summary>
    private void RenderProjection(ProjectedShadowInfo shadow, RenderTargetView target, PixelShader pixelShader, WriteProjectionParameters writeParameters)
    {
        DeviceContext ctx = Context.ImmediateContext;
        LEEffect effect = Context.LEEffect;

        //the frustum's back faces behind the scene count +1, its front faces behind the scene -1: pixels whose scene position is inside the frustum are left nonzero.
        //(The rasterizer doesn't clip by depth, so this works with the camera inside the frustum)
        ctx.OutputMerger.SetRenderTargets(ShadowMaskDepthStencilView, (RenderTargetView)null);
        ctx.ClearDepthStencilView(ShadowMaskDepthStencilView, DepthStencilClearFlags.Stencil, 0, 0);
        Vector3[] corners = shadow.GetReceiverFrustumCorners();
        var volumeConstants = new ShadowVolumeConstants
        {
            ViewProjection = effect.ViewProjectionMatrix,
            Corner0 = new Vector4(corners[0], 1), Corner1 = new Vector4(corners[1], 1), Corner2 = new Vector4(corners[2], 1), Corner3 = new Vector4(corners[3], 1),
            Corner4 = new Vector4(corners[4], 1), Corner5 = new Vector4(corners[5], 1), Corner6 = new Vector4(corners[6], 1), Corner7 = new Vector4(corners[7], 1),
        };
        ctx.UpdateSubresource(ref volumeConstants, ShadowVolumeConstantBuffer);
        ctx.InputAssembler.InputLayout = null;
        ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        ctx.VertexShader.Set(ShadowVolumeVertexShader);
        ctx.VertexShader.SetConstantBuffer(0, ShadowVolumeConstantBuffer);
        ctx.PixelShader.Set(null);
        ctx.Rasterizer.State = NoCullRasterizerState;
        ctx.OutputMerger.SetBlendState(null);
        ctx.OutputMerger.SetDepthStencilState(ShadowVolumeState, 0);
        ctx.Draw(36, 0);

        //the shadow, only where the stencil was marked
        ctx.OutputMerger.SetRenderTargets(ShadowMaskDepthStencilView, target);
        ctx.OutputMerger.SetDepthStencilState(ShadowProjectionState, 0);
        effect.PixelShaderResources.Reset();
        effect.PrepDraw(ctx, FullscreenVertexShader, pixelShader, null, MultiplyBlendState);
        Span<byte> buffer = effect.PixelShaderConstantBuffer;
        buffer.Clear();
        writeParameters(buffer);
        effect.RenderFullscreen(ctx);
        effect.PixelShaderResources.Reset();
        //the shadow depth buffer is about to be the depth target again
        ctx.PixelShader.SetShaderResources(0, NoShaderResources);
    }

    private void RenderLightFunction(SceneLight light, RenderTargetView target, MaterialRenderProxy material, FLightFunctionPixelShader lightFunctionShader)
    {
        DeviceContext ctx = Context.ImmediateContext;
        ctx.OutputMerger.SetRenderTargets((DepthStencilView)null, target);
        LEEffect effect = Context.LEEffect;
        effect.PixelShaderResources.Reset();
        PixelShader pixelShader = Context.GetCachedPixelShader(lightFunctionShader.Guid, lightFunctionShader.ShaderByteCode);
        //multiplied with the light's shadows
        effect.PrepDraw(ctx, FullscreenVertexShader, pixelShader, null, MultiplyBlendState);
        Span<byte> buffer = effect.PixelShaderConstantBuffer;
        buffer.Clear();
        ShaderParameterSetters.WriteLightFunctionValues(lightFunctionShader, buffer, Context, material, light, SceneDepthCopyView, PointClampSampler);
        Context.RestoreRasterizerState();
        effect.RenderFullscreen(ctx);
    }

    private MaterialRenderProxy GetLightFunctionMaterial(ExportEntry materialExport)
    {
        if (materialExport is null)
        {
            return null;
        }
        if (!LightFunctionMaterials.TryGetValue(materialExport, out MaterialRenderProxy material))
        {
            try
            {
                material = new MaterialRenderProxy(Context, materialExport);
                var textureMap = new Dictionary<string, PreviewTextureCache.TextureEntry>();
                foreach (IEntry textureEntry in material.Textures)
                {
                    if (!textureMap.ContainsKey(textureEntry.InstancedFullPath)
                        && Context.TextureCache.LoadTexture(textureEntry, Context.PackageCache) is { } texture)
                    {
                        textureMap.Add(textureEntry.InstancedFullPath, texture);
                    }
                }
                material.TextureMap = textureMap;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Could not load the light function material {materialExport.InstancedFullPath}: {e.Message}");
                material = null;
            }
            LightFunctionMaterials.Add(materialExport, material);
        }
        return material;
    }

    private void CopyDepth(ShaderResourceView sceneDepth)
    {
        DeviceContext ctx = Context.ImmediateContext;
        ctx.OutputMerger.SetRenderTargets((DepthStencilView)null, SceneDepthCopyTarget);
        DrawFullscreen(CopyDepthPixelShader, 0, sceneDepth);
    }

    private void FillShadowMaskDepth()
    {
        DeviceContext ctx = Context.ImmediateContext;
        ctx.OutputMerger.SetRenderTargets(ShadowMaskDepthStencilView, (RenderTargetView)null);
        ctx.OutputMerger.SetDepthStencilState(WriteDepthState);
        DrawFullscreen(WriteDepthPixelShader, 1, SceneDepthCopyView);
    }

    private void DrawFullscreen(PixelShader pixelShader, int resourceSlot, ShaderResourceView resource)
    {
        DeviceContext ctx = Context.ImmediateContext;
        ctx.OutputMerger.SetBlendState(null);
        ctx.InputAssembler.InputLayout = null;
        ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        ctx.VertexShader.Set(FullscreenVertexShader);
        ctx.PixelShader.Set(pixelShader);
        ctx.PixelShader.SetShaderResource(resourceSlot, resource);
        Context.RestoreRasterizerState();
        ctx.Draw(3, 0);
        //the source is about to be a render target again
        ctx.PixelShader.SetShaderResource(resourceSlot, null);
    }

    private void CreateResources()
    {
        Device device = Context.Device;
        if (FullscreenVertexShader is null)
        {
            string shaderCode = $"#define MSAA_SAMPLES {Context.SampleCount}\n" + ShaderCode;
            using (ShaderBytecode bytecode = ShaderBytecode.Compile(shaderCode, "VSMain", "vs_5_0").Bytecode)
            {
                FullscreenVertexShader = new VertexShader(device, bytecode);
            }
            using (ShaderBytecode bytecode = ShaderBytecode.Compile(shaderCode, "PSCopyDepth", "ps_5_0").Bytecode)
            {
                CopyDepthPixelShader = new PixelShader(device, bytecode);
            }
            using (ShaderBytecode bytecode = ShaderBytecode.Compile(shaderCode, "PSWriteDepth", "ps_5_0").Bytecode)
            {
                WriteDepthPixelShader = new PixelShader(device, bytecode);
            }
            using (ShaderBytecode bytecode = ShaderBytecode.Compile(shaderCode, "PSApplyModulation", "ps_5_0").Bytecode)
            {
                ApplyModulationPixelShader = new PixelShader(device, bytecode);
            }
            using (ShaderBytecode bytecode = ShaderBytecode.Compile(shaderCode, "VSShadowVolume", "vs_5_0").Bytecode)
            {
                ShadowVolumeVertexShader = new VertexShader(device, bytecode);
            }
            using (ShaderBytecode bytecode = ShaderBytecode.Compile(shaderCode, "VSShadowDepth", "vs_5_0").Bytecode)
            {
                ShadowDepthVertexShader = new VertexShader(device, bytecode);
                ShadowDepthInputLayout = new InputLayout(device, bytecode, LEVertex.InputElements);
            }
            using (ShaderBytecode bytecode = ShaderBytecode.Compile(shaderCode, "PSShadowDepth", "ps_5_0").Bytecode)
            {
                ShadowDepthPixelShader = new PixelShader(device, bytecode);
            }
            PointClampSampler = new SamplerState(device, new SamplerStateDescription
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                Filter = Filter.MinMagMipPoint,
            });
            //UE3's shadow depth comparison: lit where the receiver is nearer the light than the shadow map's depth
            ShadowComparisonSampler = new SamplerState(device, new SamplerStateDescription
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                Filter = Filter.ComparisonMinMagLinearMipPoint,
                ComparisonFunction = Comparison.Less,
            });
            ShadowVolumeConstantBuffer = new SharpDX.Direct3D11.Buffer(device, Marshal.SizeOf<ShadowVolumeConstants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            ShadowDepthConstantBuffer = new SharpDX.Direct3D11.Buffer(device, Marshal.SizeOf<ShadowDepthConstants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            var multiplyDescription = new BlendStateDescription();
            multiplyDescription.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = true,
                SourceBlend = BlendOption.DestinationColor,
                DestinationBlend = BlendOption.Zero,
                BlendOperation = BlendOperation.Add,
                SourceAlphaBlend = BlendOption.Zero,
                DestinationAlphaBlend = BlendOption.One,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            };
            MultiplyBlendState = new BlendState(device, multiplyDescription);
            WriteDepthState = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Always,
            });
            ShadowDepthState = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Less,
            });
            //The scene's depth is reversed: a fragment passes the depth test if it's nearer than the scene
            var countBehindScene = new DepthStencilOperationDescription
            {
                Comparison = Comparison.Always,
                PassOperation = StencilOperation.Keep,
                FailOperation = StencilOperation.Keep,
                DepthFailOperation = StencilOperation.Increment,
            };
            ShadowVolumeState = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Greater,
                IsStencilEnabled = true,
                StencilReadMask = 0xff,
                StencilWriteMask = 0xff,
                FrontFace = countBehindScene with { DepthFailOperation = StencilOperation.Decrement },
                BackFace = countBehindScene,
            });
            var testStencil = new DepthStencilOperationDescription
            {
                Comparison = Comparison.NotEqual,
                PassOperation = StencilOperation.Keep,
                FailOperation = StencilOperation.Keep,
                DepthFailOperation = StencilOperation.Keep,
            };
            ShadowProjectionState = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Always,
                IsStencilEnabled = true,
                StencilReadMask = 0xff,
                StencilWriteMask = 0,
                FrontFace = testStencil,
                BackFace = testStencil,
            });
            NoCullRasterizerState = new RasterizerState(device, new RasterizerStateDescription
            {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = false,
            });
            ShadowDepthBuffer = new Texture2D(device, new Texture2DDescription
            {
                ArraySize = 1,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
                Format = Format.R32_Typeless,
                Width = ProjectedShadowInfo.ShadowBufferSize,
                Height = ProjectedShadowInfo.ShadowBufferSize,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            });
            ShadowDepthBufferView = new DepthStencilView(device, ShadowDepthBuffer, new DepthStencilViewDescription
            {
                Format = Format.D32_Float,
                Dimension = DepthStencilViewDimension.Texture2D
            });
            ShadowDepthBufferResourceView = new ShaderResourceView(device, ShadowDepthBuffer, new ShaderResourceViewDescription
            {
                Format = Format.R32_Float,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = { MipLevels = 1, MostDetailedMip = 0 }
            });
        }
        if (SceneDepthCopy is null || Width != Context.Width || Height != Context.Height)
        {
            DisposeSizeDependentResources();
            Width = Context.Width;
            Height = Context.Height;
            (SceneDepthCopy, SceneDepthCopyTarget, SceneDepthCopyView) = CreateTarget(Format.R32_Float);
            ShadowMaskDepthStencil = new Texture2D(device, new Texture2DDescription
            {
                ArraySize = 1,
                BindFlags = BindFlags.DepthStencil,
                Format = Format.D32_Float_S8X24_UInt,
                Width = Width,
                Height = Height,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            });
            ShadowMaskDepthStencilView = new DepthStencilView(device, ShadowMaskDepthStencil);
        }
    }

    private (Texture2D, RenderTargetView, ShaderResourceView) CreateTarget(Format format)
    {
        var texture = new Texture2D(Context.Device, new Texture2DDescription
        {
            ArraySize = 1,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            CpuAccessFlags = CpuAccessFlags.None,
            Format = format,
            Width = Width,
            Height = Height,
            MipLevels = 1,
            OptionFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default
        });
        return (texture, new RenderTargetView(Context.Device, texture), new ShaderResourceView(Context.Device, texture));
    }

    /// <summary>
    /// Forgets the light function materials, so that they're reloaded, and which games' projection shaders and lights' attenuations failed. For when the level's lights change
    /// </summary>
    public void ClearMaterials()
    {
        LightFunctionMaterials.Clear();
        IncompleteProjectionShaders.Clear();
        FailedLights.Clear();
        ShadowCasters.Clear();
        Frame = uint.MaxValue;
    }

    public void DisposeSizeDependentResources()
    {
        SceneDepthCopyView?.Dispose();
        SceneDepthCopyTarget?.Dispose();
        SceneDepthCopy?.Dispose();
        SceneDepthCopy = null;
        SceneDepthCopyTarget = null;
        SceneDepthCopyView = null;
        ShadowMaskDepthStencilView?.Dispose();
        ShadowMaskDepthStencil?.Dispose();
        ShadowMaskDepthStencilView = null;
        ShadowMaskDepthStencil = null;
        ModulationTarget.View?.Dispose();
        ModulationTarget.Target?.Dispose();
        ModulationTarget.Texture?.Dispose();
        ModulationTarget = default;
        AttenuationTarget.View?.Dispose();
        AttenuationTarget.Target?.Dispose();
        AttenuationTarget.Texture?.Dispose();
        AttenuationTarget = default;
        AttenuationLight = null;
        //the copies were in the targets just disposed
        CopiedDepth = false;
        FilledShadowMask = false;
        Frame = uint.MaxValue;
    }

    public void Dispose()
    {
        DisposeSizeDependentResources();
        FullscreenVertexShader?.Dispose();
        CopyDepthPixelShader?.Dispose();
        WriteDepthPixelShader?.Dispose();
        ApplyModulationPixelShader?.Dispose();
        ShadowVolumeVertexShader?.Dispose();
        ShadowDepthVertexShader?.Dispose();
        ShadowDepthInputLayout?.Dispose();
        ShadowDepthPixelShader?.Dispose();
        PointClampSampler?.Dispose();
        ShadowComparisonSampler?.Dispose();
        ShadowVolumeConstantBuffer?.Dispose();
        ShadowDepthConstantBuffer?.Dispose();
        MultiplyBlendState?.Dispose();
        WriteDepthState?.Dispose();
        ShadowDepthState?.Dispose();
        ShadowVolumeState?.Dispose();
        ShadowProjectionState?.Dispose();
        NoCullRasterizerState?.Dispose();
        ShadowDepthBufferResourceView?.Dispose();
        ShadowDepthBufferView?.Dispose();
        ShadowDepthBuffer?.Dispose();
        FullscreenVertexShader = null;
        LightFunctionMaterials.Clear();
    }
}
