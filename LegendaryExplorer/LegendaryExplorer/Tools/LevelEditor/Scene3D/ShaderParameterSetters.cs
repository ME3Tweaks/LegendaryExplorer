using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using SharpDX;
using SharpDX.Direct3D11;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

internal static class ShaderParameterSetters
{
    /// <summary>
    /// Writes the parameters for one of the base pass vertex shaders that <see cref="MaterialRenderProxy.SelectShaders"/> can choose
    /// </summary>
    /// <param name="lightEnvironment">The lights of the light environment that lights the mesh, which replace the preview lighting. Null if it isn't lit by one</param>
    public static void WriteBasePassVertexShaderValues(Shader vertexShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat,
        MeshStaticLighting staticLighting, LightEnvironmentLighting lightEnvironment = null)
    {
        switch (vertexShader)
        {
            case TBasePassVertexShader<FNullPolicy, FNullPolicy> noLightMapShader:
                noLightMapShader.WriteValues(buffer, context, mesh, mat);
                break;
            case TBasePassVertexShader<FLightMapTexturePolicy.VertexParametersType, FNullPolicy> lightMapTextureShader:
                lightMapTextureShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(lightMapTextureShader.LightMapVertexParams.LightmapCoordinateScaleBias, RequireLightMap(staticLighting).CoordinateScaleBias);
                break;
            case TBasePassVertexShader<FVertexLightMapPolicy.VertexParametersType, FNullPolicy> vertexLightMapShader:
                vertexLightMapShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteArray(vertexLightMapShader.LightMapVertexParams.LightMapScale, RequireLightMap(staticLighting).Scales);
                break;
            //used for both FDirectionalLightLightMapPolicy and FSHLightLightMapPolicy
            case TBasePassVertexShader<FDirectionalLightPolicy.VertexParametersType, FNullPolicy> directionalLightShader:
                directionalLightShader.WriteValues(buffer, context, mesh, mat);
                //w = 1 means directional, rather than a point light
                buffer.WriteVal(directionalLightShader.LightMapVertexParams.LightDirection,
                    new Vector4(lightEnvironment?.DirectionToLight ?? context.Lighting.GetLightDirection(context.Camera), 1));
                break;
            case null:
                break;
            default:
                throw new NotSupportedException($"{vertexShader.ShaderType} is not supported by the renderer");
        }
    }

    /// <summary>
    /// Writes the parameters for one of the base pass pixel shaders that <see cref="MaterialRenderProxy.SelectShaders"/> can choose
    /// </summary>
    /// <param name="usePreviewLighting">Light the mesh with <see cref="MeshRenderContext.Lighting"/>, rather than the level's lighting</param>
    /// <param name="skyLighting">The level's sky lighting on the mesh, for the shaders with a sky light. Unused if <paramref name="usePreviewLighting"/></param>
    /// <param name="lightEnvironment">The lights of the light environment that lights the mesh, which replace the preview lighting. Null if it isn't lit by one</param>
    public static void WriteBasePassPixelShaderValues(Shader pixelShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat,
        MeshStaticLighting staticLighting, bool usePreviewLighting, SkyLighting skyLighting, LightEnvironmentLighting lightEnvironment = null)
    {
        //A light environment's sky light. (Its SH light, if it has one, can't be rendered by the shaders without one, so they get it as the sky lighting it was converted to)
        SkyLighting lightEnvironmentSky = default;
        if (lightEnvironment is not null && !(lightEnvironment.UsesSHLight && pixelShader is TBasePassPixelShader<FSHLightLightMapPolicy.PixelParametersType>))
        {
            lightEnvironmentSky = new SkyLighting(lightEnvironment.UpperSkyColor, lightEnvironment.LowerSkyColor);
        }
        switch (pixelShader)
        {
            //also the vertex light-map policies, whose light-map parameters are all in the vertex shader.
            //For a light environment (a fallback for materials without its shaders), only its SH or sky light can be rendered, as sky lighting
            case TBasePassPixelShader<FNullPolicy> noLightMapShader:
                noLightMapShader.WriteValues(buffer, context, mesh, mat, usePreviewLighting, lightEnvironment is null ? skyLighting : lightEnvironmentSky);
                break;
            //A light-map holds all the static lighting, except for any sky lights that weren't baked into it
            case TBasePassPixelShader<FLightMapTexturePolicy.PixelParametersType> lightMapTextureShader:
                lightMapTextureShader.WriteValues(buffer, context, mesh, mat, usePreviewSky: false, skyLighting);
                SetLightMapTextures(context, lightMapTextureShader.PixelParams.LightMapTextures, RequireLightMap(staticLighting));
                buffer.WriteArray(lightMapTextureShader.PixelParams.LightMapScale, staticLighting.Scales);
                break;
            case TBasePassPixelShader<FCustomLightMapTexturePolicy.PixelParametersType> customLightMapTextureShader:
                customLightMapTextureShader.WriteValues(buffer, context, mesh, mat, usePreviewSky: false, skyLighting);
                SetLightMapTextures(context, customLightMapTextureShader.PixelParams.LightMapTextures, RequireLightMap(staticLighting));
                buffer.WriteArray(customLightMapTextureShader.PixelParams.LightMapScale, staticLighting.Scales);
                buffer.WriteArray(customLightMapTextureShader.PixelParams.LightMapBias, staticLighting.Biases);
                break;
            case TBasePassPixelShader<FDirectionalLightLightMapPolicy.PixelParametersType> directionalLightShader:
                directionalLightShader.WriteValues(buffer, context, mesh, mat, usePreviewSky: lightEnvironment is null, lightEnvironmentSky);
                WriteDirectionalLight(buffer, context, directionalLightShader.PixelParams.LightColorAndFalloffExponent, directionalLightShader.PixelParams.bReceiveDynamicShadows);
                break;
            case TBasePassPixelShader<FSHLightLightMapPolicy.PixelParametersType> shLightShader:
                shLightShader.WriteValues(buffer, context, mesh, mat, usePreviewSky: lightEnvironment is null, lightEnvironmentSky);
                WriteDirectionalLight(buffer, context, shLightShader.PixelParams.LightColorAndFalloffExponent, shLightShader.PixelParams.bReceiveDynamicShadows);
                //For the preview lighting, all 0, so that the SH contributes nothing. The sky lighting provides ambient instead.
                buffer.WriteVal(shLightShader.PixelParams.WorldIncidentLighting, lightEnvironment is { UsesSHLight: true }
                    ? lightEnvironment.SHIncidentLighting.ToWorldIncidentLighting() : new Fixed7<Vector4>());
                break;
            case null:
                break;
            default:
                throw new NotSupportedException($"{pixelShader.ShaderType} is not supported by the renderer");
        }

        void WriteDirectionalLight(Span<byte> buffer, MeshRenderContext context, FShaderParameter lightColorParam, FShaderParameter bReceiveDynamicShadowsParam)
        {
            Vector3 lightColor = lightEnvironment?.DirectionalColor ?? new Vector3(context.Lighting.LightColor.R, context.Lighting.LightColor.G, context.Lighting.LightColor.B);
            //w is the falloff exponent, which is unused for directional lights
            buffer.WriteVal(lightColorParam, new Vector4(lightColor, 0));
            buffer.WriteVal(bReceiveDynamicShadowsParam, 0);
        }
    }

    private static MeshStaticLighting RequireLightMap(MeshStaticLighting staticLighting) =>
        staticLighting is { LightMapType: not ELightMapType.LMT_None } ? staticLighting
            : throw new InvalidOperationException("These shaders render a static light-map, but the mesh doesn't have one");

    /// <summary>
    /// Binds a light-map's textures to the consecutive slots of an array parameter (LightMapTextures[N])
    /// </summary>
    private static void SetLightMapTextures(MeshRenderContext context, FShaderResourceParameter param, MeshStaticLighting lightMap)
    {
        for (int i = 0; i < param.NumResources; i++)
        {
            PreviewTextureCache.TextureEntry texture = i < lightMap.Textures.Length ? lightMap.Textures[i] : null;
            context.LEEffect.PixelShaderResources.Set(param.BaseIndex + i, texture?.LinearTextureView ?? context.WhiteTexView);
            //UE3 samples light-maps bilinearly with the default (wrap) addressing
            context.LEEffect.PixelShaderResources.SetSampler(param.SamplerIndex + i, context.GetSamplerState(TextureAddressMode.Wrap, TextureAddressMode.Wrap));
        }
    }

    public static void WriteValues<LightMapPolicy, DensityPolicy>(this TBasePassVertexShader<LightMapPolicy, DensityPolicy> shader,
        Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
        where LightMapPolicy : struct, IVertexParametersType where DensityPolicy : struct, IVertexShaderParametersType
    {
        if (shader.VertexFactoryParameters.Parameters is not FLocalVertexFactoryShaderParameters vertexFactoryParams)
        {
            throw new NotSupportedException($"{shader.VertexFactoryParameters.VertexFactoryType} is not supported by the renderer");
        }
        vertexFactoryParams.WriteValues(buffer, context, mesh, mat);
        shader.HeightFogParameters.WriteValues(buffer, context, mesh, mat);
        shader.MaterialParameters.WriteValues(buffer, context, mesh, mat);
        //TODO: DensityPolicy params
    }
    /// <param name="usePreviewSky">Add <see cref="MeshRenderContext.Lighting"/>'s sky lighting (if the shader has a sky light), rather than <paramref name="skyLighting"/></param>
    /// <param name="skyLighting">The level's sky lighting on the mesh (FPrimitiveSceneInfo's UpperSkyLightColor and LowerSkyLightColor)</param>
    public static void WriteValues<LightMapPolicy>(this TBasePassPixelShader<LightMapPolicy> shader,
        Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat, bool usePreviewSky, SkyLighting skyLighting)
        where LightMapPolicy : struct, IPixelParametersType
    {
        shader.MaterialParameters.WriteValues(buffer, context, mesh, mat);
        bool drawUnlit = mat.IsUnlit || context.IsUnlit;
        //Matches UE3: lit materials get their ambient from the sky, unlit ones are just their emissive
        buffer.WriteVal(shader.AmbientColorAndSkyFactor, drawUnlit ? new LinearColor(1, 1, 1, 0) : new LinearColor(0, 0, 0, 1));
        Vector3 upperSkyColor = Vector3.Zero;
        Vector3 lowerSkyColor = Vector3.Zero;
        if (!drawUnlit && usePreviewSky)
        {
            LinearColor upper = context.Lighting.UpperSkyColor;
            LinearColor lower = context.Lighting.LowerSkyColor;
            upperSkyColor = new Vector3(upper.R, upper.G, upper.B);
            lowerSkyColor = new Vector3(lower.R, lower.G, lower.B);
        }
        else if (!drawUnlit)
        {
            upperSkyColor = skyLighting.Upper;
            lowerSkyColor = skyLighting.Lower;
        }
        buffer.WriteVal(shader.UpperSkyColor, upperSkyColor);
        buffer.WriteVal(shader.LowerSkyColor, lowerSkyColor);
        buffer.WriteVal(shader.CharacterMask, 1f);
        buffer.WriteVal(shader.MotionBlurMask, 0f);
        if (shader.TranslucencyDepth.IsBound())
        {
            //no idea what this should be
            buffer.WriteVal(shader.TranslucencyDepth, Vector4.One);
        }
    }

    /// <summary>
    /// Writes the parameters for a TLightVertexShader, as UE3's TMeshLightingDrawingPolicy sets them
    /// </summary>
    public static void WriteLightVertexShaderValues(Shader vertexShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat,
        LightInteraction interaction)
    {
        SceneLight light = interaction.Light;
        //FPointLightPolicy and FSpotLightPolicy: (Position + PreViewTranslation, InvRadius). PreViewTranslation is always 0 here
        var lightPositionAndInvRadius = new Vector4(light.Position, 1 / light.Radius);
        //FDirectionalLightPolicy: the direction towards the light
        Vector3 lightDirection = -light.Direction;
        //FShadowTexturePolicy (and FSignedDistanceFieldShadowTexturePolicy, which shares its vertex parameters) binds this to LightmapCoordinateScaleBias
        Vector4 shadowCoordinateScaleBias = interaction.ShadowMap?.CoordinateScaleBias ?? Vector4.Zero;
        switch (vertexShader)
        {
            case TLightVertexShader<FPointLightPolicy.VertexParametersType, FNullPolicy> pointLightShader:
                pointLightShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(pointLightShader.LightTypeVertexParams.LightPositionAndInvRadius, lightPositionAndInvRadius);
                break;
            case TLightVertexShader<FPointLightPolicy.VertexParametersType, FShadowTexturePolicy.VertexParametersType> pointLightShadowTextureShader:
                pointLightShadowTextureShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(pointLightShadowTextureShader.LightTypeVertexParams.LightPositionAndInvRadius, lightPositionAndInvRadius);
                buffer.WriteVal(pointLightShadowTextureShader.ShadowingVertexParams.LightmapCoordinateScaleBias, shadowCoordinateScaleBias);
                break;
            case TLightVertexShader<FSpotLightPolicy.VertexParametersType, FNullPolicy> spotLightShader:
                spotLightShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(spotLightShader.LightTypeVertexParams.LightPositionAndInvRadius, lightPositionAndInvRadius);
                break;
            case TLightVertexShader<FSpotLightPolicy.VertexParametersType, FShadowTexturePolicy.VertexParametersType> spotLightShadowTextureShader:
                spotLightShadowTextureShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(spotLightShadowTextureShader.LightTypeVertexParams.LightPositionAndInvRadius, lightPositionAndInvRadius);
                buffer.WriteVal(spotLightShadowTextureShader.ShadowingVertexParams.LightmapCoordinateScaleBias, shadowCoordinateScaleBias);
                break;
            case TLightVertexShader<FDirectionalLightPolicy.VertexParametersType, FNullPolicy> directionalLightShader:
                directionalLightShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(directionalLightShader.LightTypeVertexParams.LightDirection, lightDirection);
                break;
            case TLightVertexShader<FDirectionalLightPolicy.VertexParametersType, FShadowTexturePolicy.VertexParametersType> directionalLightShadowTextureShader:
                directionalLightShadowTextureShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(directionalLightShadowTextureShader.LightTypeVertexParams.LightDirection, lightDirection);
                buffer.WriteVal(directionalLightShadowTextureShader.ShadowingVertexParams.LightmapCoordinateScaleBias, shadowCoordinateScaleBias);
                break;
            default:
                throw new NotSupportedException($"{vertexShader.ShaderType} is not supported by the renderer");
        }
    }

    /// <summary>
    /// Writes the parameters for a TLightPixelShader, as UE3's TMeshLightingDrawingPolicy sets them
    /// </summary>
    /// <param name="lightAttenuation">The light's attenuation in screen space, from its dynamic shadows and light function (see <see cref="LightAttenuationRenderer"/>). Null if it has none</param>
    public static void WriteLightPixelShaderValues(Shader pixelShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat,
        LightInteraction interaction, ShaderResourceView lightAttenuation)
    {
        SceneLight light = interaction.Light;
        //w is the falloff exponent. (UE3 also scales the color by the primitive's DominantShadowFactor for dominant lights, which LE levels don't use)
        var lightColorAndFalloffExponent = new Vector4(light.Color, light.FalloffExponent);
        switch (pixelShader)
        {
            case TLightPixelShader<FPointLightPolicy.PixelParametersType, FNullPolicy> pointLightShader:
                pointLightShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                buffer.WriteVal(pointLightShader.LightTypePixelParams.LightColorAndFalloffExponent, lightColorAndFalloffExponent);
                break;
            case TLightPixelShader<FPointLightPolicy.PixelParametersType, FShadowTexturePolicy.PixelParametersType> pointLightShadowTextureShader:
                pointLightShadowTextureShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                buffer.WriteVal(pointLightShadowTextureShader.LightTypePixelParams.LightColorAndFalloffExponent, lightColorAndFalloffExponent);
                SetShadowTexture(context, pointLightShadowTextureShader.ShadowingPixelParams.ShadowTexture, interaction);
                break;
            case TLightPixelShader<FPointLightPolicy.PixelParametersType, FSignedDistanceFieldShadowTexturePolicy.PixelParametersType> pointLightDistanceFieldShader:
                pointLightDistanceFieldShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                buffer.WriteVal(pointLightDistanceFieldShader.LightTypePixelParams.LightColorAndFalloffExponent, lightColorAndFalloffExponent);
                SetShadowTexture(context, pointLightDistanceFieldShader.ShadowingPixelParams.ShadowTexture, interaction);
                buffer.WriteVal(pointLightDistanceFieldShader.ShadowingPixelParams.DistanceFieldParameters, GetDistanceFieldParameters(light));
                break;
            case TLightPixelShader<FSpotLightPolicy.PixelParametersType, FNullPolicy> spotLightShader:
                spotLightShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                WriteSpotLight(buffer, spotLightShader.LightTypePixelParams, light, lightColorAndFalloffExponent);
                break;
            case TLightPixelShader<FSpotLightPolicy.PixelParametersType, FShadowTexturePolicy.PixelParametersType> spotLightShadowTextureShader:
                spotLightShadowTextureShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                WriteSpotLight(buffer, spotLightShadowTextureShader.LightTypePixelParams, light, lightColorAndFalloffExponent);
                SetShadowTexture(context, spotLightShadowTextureShader.ShadowingPixelParams.ShadowTexture, interaction);
                break;
            case TLightPixelShader<FSpotLightPolicy.PixelParametersType, FSignedDistanceFieldShadowTexturePolicy.PixelParametersType> spotLightDistanceFieldShader:
                spotLightDistanceFieldShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                WriteSpotLight(buffer, spotLightDistanceFieldShader.LightTypePixelParams, light, lightColorAndFalloffExponent);
                SetShadowTexture(context, spotLightDistanceFieldShader.ShadowingPixelParams.ShadowTexture, interaction);
                buffer.WriteVal(spotLightDistanceFieldShader.ShadowingPixelParams.DistanceFieldParameters, GetDistanceFieldParameters(light));
                break;
            case TLightPixelShader<FDirectionalLightPolicy.PixelParametersType, FNullPolicy> directionalLightShader:
                directionalLightShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                WriteDirectionalLight(buffer, directionalLightShader.LightTypePixelParams, light);
                break;
            case TLightPixelShader<FDirectionalLightPolicy.PixelParametersType, FShadowTexturePolicy.PixelParametersType> directionalLightShadowTextureShader:
                directionalLightShadowTextureShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                WriteDirectionalLight(buffer, directionalLightShadowTextureShader.LightTypePixelParams, light);
                SetShadowTexture(context, directionalLightShadowTextureShader.ShadowingPixelParams.ShadowTexture, interaction);
                break;
            case TLightPixelShader<FDirectionalLightPolicy.PixelParametersType, FSignedDistanceFieldShadowTexturePolicy.PixelParametersType> directionalLightDistanceFieldShader:
                directionalLightDistanceFieldShader.WriteValues(buffer, context, mesh, mat, lightAttenuation);
                WriteDirectionalLight(buffer, directionalLightDistanceFieldShader.LightTypePixelParams, light);
                SetShadowTexture(context, directionalLightDistanceFieldShader.ShadowingPixelParams.ShadowTexture, interaction);
                buffer.WriteVal(directionalLightDistanceFieldShader.ShadowingPixelParams.DistanceFieldParameters, GetDistanceFieldParameters(light));
                break;
            default:
                throw new NotSupportedException($"{pixelShader.ShaderType} is not supported by the renderer");
        }

        static void WriteDirectionalLight(Span<byte> buffer, FDirectionalLightPolicy.PixelParametersType p, SceneLight light)
        {
            buffer.WriteVal(p.LightColor, new Vector4(light.Color, 1));
            //(bReceiveDynamicShadows is set by TLightPixelShader's WriteValues.) There are no dynamic shadows to fade out with distance
            buffer.WriteVal(p.bEnableDistanceShadowFading, 0);
            buffer.WriteVal(p.DistanceFadeParameters, System.Numerics.Vector2.Zero);
        }

        static void WriteSpotLight(Span<byte> buffer, FSpotLightPolicy.PixelParametersType p, SceneLight light, Vector4 lightColorAndFalloffExponent)
        {
            buffer.WriteVal(p.SpotAngles, new Vector4(light.CosOuterCone, light.InvCosConeDifference, 0, 0));
            buffer.WriteVal(p.SpotDirection, light.Direction);
            buffer.WriteVal(p.LightColorAndFalloffExponent, lightColorAndFalloffExponent);
        }

        //FSignedDistanceFieldShadowTexturePolicy::ElementDataType, with the material's DistanceFieldPenumbraScale at its default of 1
        static Vector3 GetDistanceFieldParameters(SceneLight light)
        {
            float penumbraSize = Math.Min(light.DistanceFieldShadowMapPenumbraSize, 1);
            return new Vector3(-0.5f + penumbraSize * 0.5f, 1 / penumbraSize, light.DistanceFieldShadowMapShadowExponent);
        }

        static void SetShadowTexture(MeshRenderContext context, FShaderResourceParameter param, LightInteraction interaction)
        {
            //shadow maps hold linear values
            context.LEEffect.PixelShaderResources.Set(param.BaseIndex, interaction.ShadowMap?.Texture.TextureView ?? context.WhiteTexView);
            context.LEEffect.PixelShaderResources.SetSampler(param.SamplerIndex, context.GetSamplerState(TextureAddressMode.Wrap, TextureAddressMode.Wrap));
        }
    }

    /// <summary>
    /// Writes the parameters for an SH light's pass (TLightVertexShader and TLightPixelShader with FSphericalHarmonicLightPolicy), as FSphericalHarmonicLightPolicy sets them.
    /// It has no shadows or light function, so no attenuation
    /// </summary>
    public static void WriteSHLightPassValues(Shader vertexShader, Shader pixelShader, Span<byte> vertexBuffer, Span<byte> pixelBuffer, MeshRenderContext context,
        Mesh<LEVertex> mesh, MaterialRenderProxy mat, in SHVectorRGB incidentLighting)
    {
        switch (vertexShader)
        {
            case TLightVertexShader<FNullPolicy, FNullPolicy> shLightVertexShader:
                shLightVertexShader.WriteValues(vertexBuffer, context, mesh, mat);
                break;
            default:
                throw new NotSupportedException($"{vertexShader.ShaderType} is not supported by the renderer");
        }
        switch (pixelShader)
        {
            case TLightPixelShader<FSphericalHarmonicLightPolicy.PixelParametersType, FNullPolicy> shLightPixelShader:
                shLightPixelShader.WriteValues(pixelBuffer, context, mesh, mat, lightAttenuation: null);
                pixelBuffer.WriteVal(shLightPixelShader.LightTypePixelParams.WorldIncidentLighting, incidentLighting.ToWorldIncidentLighting());
                break;
            default:
                throw new NotSupportedException($"{pixelShader.ShaderType} is not supported by the renderer");
        }
    }

    public static void WriteValues<LightTypePolicy, ShadowingTypePolicy>(this TLightVertexShader<LightTypePolicy, ShadowingTypePolicy> shader,
        Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
        where LightTypePolicy : struct, IVertexParametersType where ShadowingTypePolicy : struct, IVertexParametersType
    {
        if (shader.VertexFactoryParameters.Parameters is not FLocalVertexFactoryShaderParameters vertexFactoryParams)
        {
            throw new NotSupportedException($"{shader.VertexFactoryParameters.VertexFactoryType} is not supported by the renderer");
        }
        vertexFactoryParams.WriteValues(buffer, context, mesh, mat);
        shader.MaterialParameters.WriteValues(buffer, context, mesh, mat);
    }

    /// <param name="lightAttenuation">The light's attenuation in screen space: its dynamic shadows and light function. Null if it has neither</param>
    public static void WriteValues<LightTypePolicy, ShadowingTypePolicy>(this TLightPixelShader<LightTypePolicy, ShadowingTypePolicy> shader,
        Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat, ShaderResourceView lightAttenuation)
        where LightTypePolicy : struct, IPixelParametersType where ShadowingTypePolicy : struct, IPixelParametersType
    {
        shader.MaterialParameters.WriteValues(buffer, context, mesh, mat);
        //UE3 renders the light's dynamic shadows and light function into this (see LightAttenuationRenderer), and only reads it if bReceiveDynamicShadows is set
        if (shader.LightAttenuationTexture.IsBound())
        {
            context.LEEffect.PixelShaderResources.Set(shader.LightAttenuationTexture.BaseIndex, lightAttenuation ?? context.WhiteTexView);
            context.LEEffect.PixelShaderResources.SetSampler(shader.LightAttenuationTexture.SamplerIndex, context.GetSamplerState(TextureAddressMode.Clamp, TextureAddressMode.Clamp));
        }
        buffer.WriteVal(shader.bReceiveDynamicShadows, lightAttenuation is null ? 0 : 1);
    }

    /// <summary>
    /// Writes the parameters for projecting a shadow, as TShadowProjectionPixelShader::SetParameters does (its 4 sample hardware PCF variant)
    /// </summary>
    /// <param name="sceneDepth">The scene's device depth, single-sampled</param>
    /// <param name="depthSampler">For <paramref name="sceneDepth"/>: point sampled, clamped</param>
    /// <param name="shadowDepth">The shadow depth buffer, with the shadow rendered at its top left</param>
    /// <param name="comparisonSampler">For <paramref name="shadowDepth"/>: a bilinear comparison sampler</param>
    public static void WriteShadowProjectionValues(TShadowProjectionPixelShader shader, Span<byte> buffer, MeshRenderContext context, ProjectedShadowInfo shadow,
        ShaderResourceView sceneDepth, SamplerState depthSampler, ShaderResourceView shadowDepth, SamplerState comparisonSampler)
    {
        LEEffect effect = context.LEEffect;
        buffer.WriteVal(shader.ScreenToShadowMatrix, shadow.GetScreenToShadowMatrix(context.Camera.ProjectionMatrix, effect.InverseViewProjectionMatrix));
        //the 4 corners of a texel, rotated 45 degrees and scaled by the filter radius
        float offset = 0.70710677f * ProjectedShadowInfo.GetShadowFilterRadius(shadow.Light.Game) * 0.5f / ProjectedShadowInfo.ShadowBufferSize;
        var sampleOffsets = new Fixed2<Vector4>();
        sampleOffsets[0] = new Vector4(-offset, 0, 0, -offset);
        sampleOffsets[1] = new Vector4(0, offset, offset, 0);
        buffer.WriteVal(shader.SampleOffsets, sampleOffsets);
        buffer.WriteVal(shader.ShadowBufferSize, new System.Numerics.Vector2(ProjectedShadowInfo.ShadowBufferSize));
        buffer.WriteVal(shader.ShadowFadeFraction, shadow.FadeAlpha);
        shader.SceneTextureParameters.WriteValues(buffer, context, null, null);
        if (shader.SceneTextureParameters.SceneDepthTexture.IsBound())
        {
            effect.PixelShaderResources.Set(shader.SceneTextureParameters.SceneDepthTexture.BaseIndex, sceneDepth);
            effect.PixelShaderResources.SetSampler(shader.SceneTextureParameters.SceneDepthTexture.SamplerIndex, depthSampler);
        }
        if (shader.ShadowDepthTexture.IsBound())
        {
            effect.PixelShaderResources.Set(shader.ShadowDepthTexture.BaseIndex, shadowDepth);
        }
        if (shader.ShadowDepthTextureComparisonSampler.IsBound())
        {
            effect.PixelShaderResources.SetSampler(shader.ShadowDepthTextureComparisonSampler.SamplerIndex, comparisonSampler);
        }
    }

    /// <summary>
    /// Writes the parameters for a light environment's modulated shadow from its shadow light, as TModShadowProjectionPixelShader&lt;FPointLightPolicy&gt;::SetParameters does
    /// </summary>
    /// <param name="sceneDepth">The scene's device depth, single-sampled</param>
    /// <param name="depthSampler">For <paramref name="sceneDepth"/>: point sampled, clamped</param>
    public static void WriteModShadowProjectionValues(TModShadowProjectionPixelShader<FPointLightPolicy.ModShadowPixelParamsType> shader, Span<byte> buffer,
        MeshRenderContext context, ProjectedShadowInfo shadow, LightEnvironmentShadowLight shadowLight,
        ShaderResourceView sceneDepth, SamplerState depthSampler, ShaderResourceView shadowDepth, SamplerState comparisonSampler)
    {
        LEEffect effect = context.LEEffect;
        Matrix4x4 projection = context.Camera.ProjectionMatrix;
        buffer.WriteVal(shader.ScreenToShadowMatrix, shadow.GetScreenToShadowMatrix(projection, effect.InverseViewProjectionMatrix));
        float offset = 0.70710677f * ProjectedShadowInfo.GetShadowFilterRadius(shadow.Light.Game) * 0.5f / ProjectedShadowInfo.ShadowBufferSize;
        var sampleOffsets = new Fixed2<Vector4>();
        sampleOffsets[0] = new Vector4(-offset, 0, 0, -offset);
        sampleOffsets[1] = new Vector4(0, offset, offset, 0);
        buffer.WriteVal(shader.SampleOffsets, sampleOffsets);
        buffer.WriteVal(shader.ShadowBufferSize, new System.Numerics.Vector2(ProjectedShadowInfo.ShadowBufferSize));
        buffer.WriteVal(shader.ShadowFadeFraction, shadow.FadeAlpha);
        //the shadow fades to no darkening as its resolution approaches the minimum
        Vector3 modulateColor = Vector3.Lerp(Vector3.One, shadowLight.ModShadowColor, shadow.FadeAlpha);
        buffer.WriteVal(shader.ShadowModulateColor, new Vector4(modulateColor, 1));
        //From (ScreenPosition.xy * SceneDepth, SceneDepth, 1) to world space (there's no pre-view translation)
        var screenToClip = new Matrix4x4(
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, projection.M33, 1,
            0, 0, projection.M43, 0);
        buffer.WriteVal(shader.ScreenToWorld, screenToClip * effect.InverseViewProjectionMatrix);
        //the shadow fades out from the minimum falloff radius to the light's radius
        float radius = shadowLight.Radius;
        float minRadius = shadowLight.MinShadowFalloffRadius;
        buffer.WriteVal(shader.ModShadowPixelParams.LightPositionParam, new Vector4(shadowLight.Position, 1 / radius));
        buffer.WriteVal(shader.ModShadowPixelParams.FalloffParameters, new Vector3(shadowLight.ShadowFalloffExponent,
            1 / MathF.Max(1 - minRadius / radius, 0.00001f), minRadius / (minRadius - radius)));
        shader.SceneTextureParameters.WriteValues(buffer, context, null, null);
        if (shader.SceneTextureParameters.SceneDepthTexture.IsBound())
        {
            effect.PixelShaderResources.Set(shader.SceneTextureParameters.SceneDepthTexture.BaseIndex, sceneDepth);
            effect.PixelShaderResources.SetSampler(shader.SceneTextureParameters.SceneDepthTexture.SamplerIndex, depthSampler);
        }
        if (shader.ShadowDepthTexture.IsBound())
        {
            effect.PixelShaderResources.Set(shader.ShadowDepthTexture.BaseIndex, shadowDepth);
        }
        if (shader.ShadowDepthTextureComparisonSampler.IsBound())
        {
            effect.PixelShaderResources.SetSampler(shader.ShadowDepthTextureComparisonSampler.SamplerIndex, comparisonSampler);
        }
    }

    /// <summary>
    /// Writes the parameters for a light function, as FLightFunctionPixelShader::SetParameters does
    /// </summary>
    /// <param name="sceneDepth">The scene's device depth, single-sampled</param>
    /// <param name="depthSampler">For <paramref name="sceneDepth"/>: point sampled, clamped</param>
    public static void WriteLightFunctionValues(FLightFunctionPixelShader shader, Span<byte> buffer, MeshRenderContext context, MaterialRenderProxy mat, SceneLight light,
        ShaderResourceView sceneDepth, SamplerState depthSampler)
    {
        //there's no mesh: the material is evaluated in the light's space
        shader.MaterialParameters.WriteValues(buffer, context, mat, default, Matrix4x4.Identity, Matrix4x4.Identity);
        //From (ScreenPosition.xy * SceneDepth, SceneDepth, 1) to the light function's coordinates: clip space, then world space,
        //then the light's space, scaled so that one unit of the material's coordinates covers LightFunctionScale
        Matrix4x4 projection = context.Camera.ProjectionMatrix;
        var screenToClip = new Matrix4x4(
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, projection.M33, 1,
            0, 0, projection.M43, 0);
        Vector3 scale = light.LightFunctionScale;
        Matrix4x4 screenToLight = screenToClip * context.LEEffect.InverseViewProjectionMatrix * light.WorldToLight
                                  * Matrix4x4.CreateScale(1 / scale.X, 1 / scale.Y, 1 / scale.Z);
        buffer.WriteVal(shader.ScreenToLight, screenToLight);
        shader.SceneTextureParameters.WriteValues(buffer, context, null, mat);
        if (shader.SceneTextureParameters.SceneDepthTexture.IsBound())
        {
            context.LEEffect.PixelShaderResources.Set(shader.SceneTextureParameters.SceneDepthTexture.BaseIndex, sceneDepth);
            context.LEEffect.PixelShaderResources.SetSampler(shader.SceneTextureParameters.SceneDepthTexture.SamplerIndex, depthSampler);
        }
    }

    public static void WriteValues(this ref FMaterialVertexShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        buffer.WriteVal(p.CameraWorldPosition, context.Camera.EyePosition);
        buffer.WriteVal(p.ObjectWorldPositionAndRadius, new Vector4(mesh.TransformedBounds.Origin, mesh.TransformedBounds.SphereRadius));
        buffer.WriteVal(p.ObjectOrientation, mesh.LocalToWorld.GetAxis(2).Normal());
        buffer.WriteVal(p.WindDirectionAndSpeed, Vector4.Zero);
        buffer.WriteVal(p.FoliageImpulseDirection, Vector3.Zero);
        buffer.WriteVal(p.FoliageNormalizedRotationAxisAndAngle, Vector4.UnitZ);

        (List<Vector4> scalarParamValues, List<Vector4> vectorParamValues) = mat.GetCachedVertexParameters(context);
        foreach (TUniformParameter<FShaderParameter> scalarParam in p.UniformVertexScalarShaderParameters)
        {
            WriteScalarUniform(buffer, scalarParam, scalarParamValues);
        }
        foreach (TUniformParameter<FShaderParameter> vectorParam in p.UniformVertexVectorShaderParameters)
        {
            buffer.WriteVal(vectorParam.Param, vectorParamValues[vectorParam.Index]);
        }
    }
    public static void WriteValues(this ref FMaterialPixelShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
        => p.WriteValues(buffer, context, mat, mesh.TransformedBounds, mesh.LocalToWorld, mesh.WorldToLocal);

    /// <param name="bounds">The world bounds of what's being drawn</param>
    /// <param name="localToWorld">The transform of what's being drawn. Identity if it isn't a mesh</param>
    public static void WriteValues(this ref FMaterialPixelShaderParameters p, Span<byte> buffer, MeshRenderContext context, MaterialRenderProxy mat,
        BoxSphereBounds bounds, Matrix4x4 localToWorld, Matrix4x4 worldToLocal)
    {
        buffer.WriteVal(p.CameraWorldPosition, context.Camera.EyePosition);
        buffer.WriteVal(p.ObjectWorldPositionAndRadius, new Vector4(bounds.Origin, bounds.SphereRadius));
        buffer.WriteVal(p.ObjectOrientation, localToWorld.GetAxis(2).Normal());
        buffer.WriteVal(p.WindDirectionAndSpeed, Vector4.Zero);
        buffer.WriteVal(p.FoliageImpulseDirection, Vector3.Zero);
        buffer.WriteVal(p.FoliageNormalizedRotationAxisAndAngle, Vector4.UnitZ);

        (List<Vector4> scalarParamValues, 
            List<Vector4> vectorParamValues, 
            List<PreviewTextureCache.TextureEntry> tex2dParamValues, 
            List<PreviewTextureCache.TextureEntry> cubeMapParamValues) = mat.GetCachedPixelParameters(context);

        foreach (TUniformParameter<FShaderParameter> scalarParam in p.UniformPixelScalarShaderParameters)
        {
            WriteScalarUniform(buffer, scalarParam, scalarParamValues);
        }
        foreach (TUniformParameter<FShaderParameter> vectorParam in p.UniformPixelVectorShaderParameters)
        {
            buffer.WriteVal(vectorParam.Param, vectorParamValues[vectorParam.Index]);
        }
        foreach (TUniformParameter<FShaderResourceParameter> texParam in p.UniformPixel2DShaderResourceParameters)
        {
            PreviewTextureCache.TextureEntry texture = tex2dParamValues[texParam.Index];
            context.LEEffect.PixelShaderResources.Set(texParam.Param.BaseIndex, texture?.LinearTextureView ?? context.WhiteTexView);
            if (texture is not null)
            {
                //materials can rely on clamping, e.g. to confine a decal-like texture to one region of the UVs
                context.LEEffect.PixelShaderResources.SetSampler(texParam.Param.SamplerIndex, context.GetSamplerState(texture.AddressU, texture.AddressV));
            }
        }
        foreach (TUniformParameter<FShaderResourceParameter> cubeParam in p.UniformPixelCubeShaderResourceParameters)
        {
            ShaderResourceView view = cubeMapParamValues[cubeParam.Index]?.LinearTextureView ?? context.WhiteTextureCubeView;
            context.LEEffect.PixelShaderResources.Set(cubeParam.Param.BaseIndex, view);
        }

        LEEffect effect = context.LEEffect;
        buffer.WriteVal(p.LocalToWorld, localToWorld);
        buffer.WriteVal(p.WorldToLocal, worldToLocal);
        //float3x3 shader parameters have each row padded to 4 floats, so a Matrix4x4's layout matches (the 4th column lands in the padding)
        buffer.WriteVal(p.WorldToView, effect.ViewMatrix);
        buffer.WriteVal(p.InvViewProjection, effect.InverseViewProjectionMatrix);
        buffer.WriteVal(p.ViewProjection, effect.ViewProjectionMatrix);

        p.SceneTextureParameters.WriteValues(buffer, context, null, mat);

        //UE3 flips this for meshes with reversed culling (see FMaterialPixelShaderParameters::SetMesh). Backfaces aren't rendered in a separate pass
        buffer.WriteVal(p.TwoSidedSign, localToWorld.GetDeterminant() < 0 ? -1f : 1f);
        buffer.WriteVal(p.InvGamma, 1f / (1f /*GammaCorrection*/ ));
        buffer.WriteVal(p.DecalFarPlaneDistance, 65536f); //actual value is stored on the BioDecalComponent

        //these are used for ParticleSystem rendering
        buffer.WriteVal(p.ObjectPostProjectionPosition, Vector3.Zero);
        buffer.WriteVal(p.ObjectMacroUVScales, Vector4.Zero);
        buffer.WriteVal(p.ObjectNDCPosition, Vector3.Zero);
        buffer.WriteVal(p.OcclusionPercentage, 0f);

        const int isFading = 0;
        buffer.WriteVal(p.EnableScreenDoorFade, isFading);
        if (isFading > 0)
        {
            buffer.WriteVal(p.ScreenDoorFadeSettings, Vector4.Zero);
            buffer.WriteVal(p.ScreenDoorFadeSettings2, Vector4.Zero);
        }
        if (p.ScreenDoorNoiseTexture.IsBound())
        {
            //only sampled when EnableScreenDoorFade is set, which it never is
            context.LEEffect.PixelShaderResources.Set(p.ScreenDoorNoiseTexture.BaseIndex, null);
        }
        //Bioware addition: (DirectScale, IndirectScale, 0, 0), see FMaterialPixelShaderParameters::SetMesh in LE3
        buffer.WriteVal(p.WrapLightingParameters, new Vector4(PreviewLighting.WrapLightingDirectScale, PreviewLighting.WrapLightingIndirectScale, 0, 0));
    }

    public static void WriteValues(this ref FSceneTextureShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        //TODO: SceneColor and SceneDepth aren't available to materials yet. Unbound textures sample as 0
        if (p.SceneColorTexture.IsBound())
        {
            context.LEEffect.PixelShaderResources.Set(p.SceneColorTexture.BaseIndex, null);
        }
        if (p.SceneDepthTexture.IsBound())
        {
            context.LEEffect.PixelShaderResources.Set(p.SceneDepthTexture.BaseIndex, null);
        }

        if (p.ScreenPositionScaleBias.IsBound())
        {
            buffer.WriteVal(p.ScreenPositionScaleBias, context.LEEffect.SharedPixelConstants.ScreenPositionScaleBias);

        }
        if (p.MinZ_MaxZRatio.IsBound())
        {
            buffer.WriteVal(p.MinZ_MaxZRatio, context.LEEffect.SharedPixelConstants.MinZ_MaxZRatio);
        }
    }

    public static void WriteValues(this ref FHeightFogVertexShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        //these values disable fog
        buffer.WriteVal(p.FogExtinctionDistance, new Vector4(float.MaxValue));
        var fogInScatteringValue = new Fixed4<LinearColor>();
        fogInScatteringValue[0] = LinearColor.Black;
        fogInScatteringValue[1] = LinearColor.Black;
        fogInScatteringValue[2] = LinearColor.Black;
        fogInScatteringValue[3] = LinearColor.Black;
        buffer.WriteVal(p.FogInScattering, fogInScatteringValue);
        buffer.WriteVal(p.FogDistanceScale, Vector4.Zero);
        buffer.WriteVal(p.FogMinHeight, Vector4.Zero);
        buffer.WriteVal(p.FogMaxHeight, Vector4.Zero);
        buffer.WriteVal(p.FogStartDistance, Vector4.Zero);
    }

    public static void WriteValues(this FLocalVertexFactoryShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
            buffer.WriteVal(p.LocalToWorld, mesh.LocalToWorld);
            buffer.WriteVal(p.WorldToLocal, mesh.WorldToLocal);
            buffer.WriteVal(p.LocalToWorldRotDeterminantFlip, mesh.LocalToWorld.GetDeterminant() >= 0 ? 1f : -1f);
    }

    //Scalar uniform expressions are packed 4 to a float4 (UniformPixelScalars_N etc.). A parameter bound to a whole float4
    //has that float4's index, so all 4 components must be written, not just the first.
    private static void WriteScalarUniform(Span<byte> buffer, TUniformParameter<FShaderParameter> scalarParam, List<Vector4> scalarValues)
    {
        if (scalarParam.Param.NumBytes > sizeof(float))
        {
            buffer.WriteVal(scalarParam.Param, scalarValues[scalarParam.Index]);
        }
        else
        {
            buffer.WriteVal(scalarParam.Param, scalarValues[scalarParam.Index / 4][scalarParam.Index % 4]);
        }
    }

    /// <summary>
    /// Writes as many elements as the parameter (an array) has room for
    /// </summary>
    private static void WriteArray(this Span<byte> buff, FShaderParameter param, ReadOnlySpan<Vector4> values)
    {
        if (!param.IsBound())
        {
            return;
        }
        ReadOnlySpan<byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(values);
        bytes[..Math.Min(bytes.Length, param.NumBytes)].CopyTo(buff[param.BaseIndex..]);
    }

    private static unsafe void WriteVal<T>(this Span<byte> buff, FShaderParameter param, T val) where T : unmanaged
    {
        if (!param.IsBound())
        {
            return;
        }
        //if (sizeof(T) != param.NumBytes 
        //    && !(typeof(T) == typeof(Matrix3x3) && param.NumBytes == 44) 
        //    && Debugger.IsAttached)
        //{
        //    Debugger.Break();
        //}
        int bytesToWrite = Math.Min(sizeof(T), param.NumBytes);
        val.AsBytes()[..bytesToWrite].CopyTo(buff[param.BaseIndex..]);
    }
}
