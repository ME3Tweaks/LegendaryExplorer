using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using SharpDX.Direct3D11;
using System;
using System.Diagnostics;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A terrain section drawn as FTerrainComponentSceneProxy draws it: with its cached terrain material's FTerrainVertexFactory shaders, from the game's vertex
/// format, and its light-map in the base pass; the level's lights that aren't baked into it are added in their own passes
/// </summary>
internal sealed class TerrainSectionShading : IShaderPrimitive
{
    private readonly TerrainSectionMesh Mesh;
    private readonly Func<Matrix4x4> GetLocalToWorld;
    private readonly Func<BoxSphereBounds> GetBounds;
    private readonly Func<Vector3> GetDrawScale3D;
    private readonly float TessellationDistanceScale;
    public MaterialRenderProxy Material { get; }
    public MeshStaticLighting Lighting { get; }

    /// <param name="getDrawScale3D">The terrain's DrawScale3D (without DrawScale), which the vertex factory's scale factors and the packed gradients use</param>
    public TerrainSectionShading(TerrainSectionMesh mesh, MaterialRenderProxy material, MeshStaticLighting lighting, Func<Matrix4x4> getLocalToWorld,
        Func<BoxSphereBounds> getBounds, Func<Vector3> getDrawScale3D, float tessellationDistanceScale)
    {
        Mesh = mesh;
        Material = material;
        Lighting = lighting;
        GetLocalToWorld = getLocalToWorld;
        GetBounds = getBounds;
        GetDrawScale3D = getDrawScale3D;
        TessellationDistanceScale = tessellationDistanceScale;
    }

    public Matrix4x4 LocalToWorld => GetLocalToWorld();
    public Matrix4x4 WorldToLocal => Matrix4x4.Invert(LocalToWorld, out Matrix4x4 inverse) ? inverse : Matrix4x4.Identity;
    public BoxSphereBounds Bounds => GetBounds();

    //FTerrainObject's ScaleFactorX and ScaleFactorY
    private Vector2 GradientScale
    {
        get
        {
            Vector3 scale = GetDrawScale3D();
            return new Vector2(scale.Z / scale.X, scale.Z / scale.Y);
        }
    }

    public TerrainVertexFactoryValues Terrain
    {
        get
        {
            TerrainSection section = Mesh.Section;
            TerrainHeightField heightField = Mesh.HeightField;
            Vector2 gradientScale = GradientScale;
            return new TerrainVertexFactoryValues(new Vector4(1, TerrainHeightField.ZScale, gradientScale.X, gradientScale.Y),
                new Vector4(1f / heightField.NumVerticesX, 1f / heightField.NumVerticesY, section.BaseX, section.BaseY),
                section.LightMapCoordinateScaleBias(heightField.StaticLightingResolution), TessellationDistanceScale);
        }
    }

    private static readonly RenderTargetBlendDescription OpaqueBlend = new()
    {
        //the game's base pass shaders write 0 to alpha, which mustn't reach the backbuffer
        RenderTargetWriteMask = ColorWriteMaskFlags.Red | ColorWriteMaskFlags.Green | ColorWriteMaskFlags.Blue,
        BlendOperation = BlendOperation.Add,
        AlphaBlendOperation = BlendOperation.Add,
        SourceBlend = BlendOption.One,
        DestinationBlend = BlendOption.Zero,
        SourceAlphaBlend = BlendOption.One,
        DestinationAlphaBlend = BlendOption.Zero,
        IsBlendEnabled = false
    };

    //lights add to what's already been rendered
    private static readonly RenderTargetBlendDescription AdditiveBlend = new()
    {
        RenderTargetWriteMask = ColorWriteMaskFlags.Red | ColorWriteMaskFlags.Green | ColorWriteMaskFlags.Blue,
        BlendOperation = BlendOperation.Add,
        AlphaBlendOperation = BlendOperation.Add,
        SourceBlend = BlendOption.One,
        DestinationBlend = BlendOption.One,
        SourceAlphaBlend = BlendOption.Zero,
        DestinationAlphaBlend = BlendOption.One,
        IsBlendEnabled = true
    };

    //Terrain materials are lit (MLM_Phong), and terrain is never lit by a light environment
    private static bool UsesLevelLighting(MeshRenderContext context) => context.UsesLevelLighting;

    /// <summary>
    /// Draws the section with its material's base pass shaders, and into the hit proxy target. False if they can't be used now (they're still loading,
    /// or failed), in which case the caller draws the neutral preview
    /// </summary>
    public bool DrawBasePass(MeshRenderContext context, LEHitProxyConstants hitProxy)
    {
        if (!Material.CanRenderWithGameShaders) return false;
        try
        {
            LEEffect effect = context.LEEffect;
            bool usesLevelLighting = UsesLevelLighting(context);
            //sky lights that aren't baked into the light-map are added in the base pass
            SkyLighting skyLighting = usesLevelLighting && context.IsLevelLightingActive ? Lighting.GetSkyLighting(Bounds) : default;
            (Shader vertexShader, Shader pixelShader) = Material.GetBasePassShaders(context, usesLevelLighting, out bool usePreviewLighting, !skyLighting.IsBlack);
            if (vertexShader is null || pixelShader is null) return false;
            PixelShader ps = context.GetCachedPixelShader(pixelShader.Guid, pixelShader.ShaderByteCode);
            (VertexShader vs, InputLayout inputLayout) = context.GetCachedVertexShader(vertexShader.Guid, vertexShader.ShaderByteCode, TerrainGameVertex.InputElements);
            effect.PrepDraw(context.ImmediateContext, vs, ps, inputLayout, context.GetCachedBlendState(OpaqueBlend));
            context.ImmediateContext.OutputMerger.SetDepthStencilState(context.DefaultDepthState);
            context.SetMeshRasterizerState(isTwoSided: false, reverseCulling: LocalToWorld.GetDeterminant() < 0);
            try
            {
                Material.UpdateShaderParams(effect.VertexShaderConstantBuffer, effect.PixelShaderConstantBuffer, context, this, vertexShader, pixelShader,
                    Lighting, usePreviewLighting, skyLighting);
                Mesh.DrawGame(context, effect, GradientScale, hitProxy);
            }
            finally
            {
                context.ImmediateContext.OutputMerger.SetDepthStencilState(context.DefaultDepthState);
                context.RestoreRasterizerState();
                context.RestoreDefaultSamplers();
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"{Material.InstancedFullPath} will be rendered with the neutral terrain shader: {e.Message}");
            Material.MarkGameShadersFailed(e);
            return false;
        }
    }

    /// <summary>
    /// Queues the level's lights that reach the section and aren't baked into its light-map, each drawn in its own pass as TMeshLightingDrawingPolicy draws them
    /// </summary>
    public void QueueLightPasses(MeshRenderContext context)
    {
        if (!context.UseGameShaders || !context.IsLevelLightingActive || context.Wireframe || !UsesLevelLighting(context) || !Material.CanRenderWithGameShaders
            || Material.GetBasePassShaders(context, usesLevelLights: true, out _).vertexShader is null)
        {
            return;
        }
        foreach (LightInteraction interaction in Lighting.GetLightInteractions(Bounds))
        {
            (Shader vertexShader, Shader pixelShader) = Material.GetLightShaders(interaction.Light.Type, interaction.Shadowing);
            if (vertexShader is not null && pixelShader is not null)
            {
                context.QueueLightPass(interaction.Light, lightAttenuation => DrawLightPass(context, interaction, vertexShader, pixelShader, lightAttenuation));
            }
        }
    }

    private void DrawLightPass(MeshRenderContext context, LightInteraction interaction, Shader vertexShader, Shader pixelShader, ShaderResourceView lightAttenuation)
    {
        if (!Material.CanRenderWithGameShaders) return;
        LEEffect effect = context.LEEffect;
        try
        {
            context.ImmediateContext.OutputMerger.SetDepthStencilState(context.LightPassDepthState);
            context.SetMeshRasterizerState(isTwoSided: false, reverseCulling: LocalToWorld.GetDeterminant() < 0);
            PixelShader ps = context.GetCachedPixelShader(pixelShader.Guid, pixelShader.ShaderByteCode);
            (VertexShader vs, InputLayout inputLayout) = context.GetCachedVertexShader(vertexShader.Guid, vertexShader.ShaderByteCode, TerrainGameVertex.InputElements);
            effect.PrepDraw(context.ImmediateContext, vs, ps, inputLayout, context.GetCachedBlendState(AdditiveBlend));
            Material.UpdateLightPassShaderParams(effect.VertexShaderConstantBuffer, effect.PixelShaderConstantBuffer, context, this, vertexShader, pixelShader, interaction,
                lightAttenuation);
            Mesh.DrawGame(context, effect, GradientScale, hitProxy: null);
        }
        catch (Exception e)
        {
            //like a failed base pass, the material falls back to the neutral shader rather than stopping the scene from rendering
            Debug.WriteLine($"A light pass of {Material.InstancedFullPath} failed, so it will be rendered with the neutral terrain shader: {e.Message}");
            Material.MarkGameShadersFailed(e);
        }
        finally
        {
            context.ImmediateContext.OutputMerger.SetDepthStencilState(context.DefaultDepthState);
            context.RestoreRasterizerState();
            context.RestoreDefaultSamplers();
        }
    }
}
