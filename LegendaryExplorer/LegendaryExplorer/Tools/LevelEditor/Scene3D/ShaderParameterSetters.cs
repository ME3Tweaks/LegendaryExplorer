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
    public static void WriteBasePassVertexShaderValues(Shader vertexShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat,
        MeshStaticLighting staticLighting)
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
                buffer.WriteVal(directionalLightShader.LightMapVertexParams.LightDirection, new Vector4(context.Lighting.GetLightDirection(context.Camera), 1));
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
    public static void WriteBasePassPixelShaderValues(Shader pixelShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat,
        MeshStaticLighting staticLighting, bool usePreviewLighting)
    {
        switch (pixelShader)
        {
            //also the vertex light-map policies, whose light-map parameters are all in the vertex shader
            case TBasePassPixelShader<FNullPolicy> noLightMapShader:
                noLightMapShader.WriteValues(buffer, context, mesh, mat, usePreviewLighting);
                break;
            //A light-map holds all the static lighting, ambient included
            case TBasePassPixelShader<FLightMapTexturePolicy.PixelParametersType> lightMapTextureShader:
                lightMapTextureShader.WriteValues(buffer, context, mesh, mat, usePreviewSky: false);
                SetLightMapTextures(context, lightMapTextureShader.PixelParams.LightMapTextures, RequireLightMap(staticLighting));
                buffer.WriteArray(lightMapTextureShader.PixelParams.LightMapScale, staticLighting.Scales);
                break;
            case TBasePassPixelShader<FCustomLightMapTexturePolicy.PixelParametersType> customLightMapTextureShader:
                customLightMapTextureShader.WriteValues(buffer, context, mesh, mat, usePreviewSky: false);
                SetLightMapTextures(context, customLightMapTextureShader.PixelParams.LightMapTextures, RequireLightMap(staticLighting));
                buffer.WriteArray(customLightMapTextureShader.PixelParams.LightMapScale, staticLighting.Scales);
                buffer.WriteArray(customLightMapTextureShader.PixelParams.LightMapBias, staticLighting.Biases);
                break;
            case TBasePassPixelShader<FDirectionalLightLightMapPolicy.PixelParametersType> directionalLightShader:
                directionalLightShader.WriteValues(buffer, context, mesh, mat, usePreviewSky: true);
                WriteDirectionalLight(buffer, context, directionalLightShader.PixelParams.LightColorAndFalloffExponent, directionalLightShader.PixelParams.bReceiveDynamicShadows);
                break;
            case TBasePassPixelShader<FSHLightLightMapPolicy.PixelParametersType> shLightShader:
                shLightShader.WriteValues(buffer, context, mesh, mat, usePreviewSky: true);
                WriteDirectionalLight(buffer, context, shLightShader.PixelParams.LightColorAndFalloffExponent, shLightShader.PixelParams.bReceiveDynamicShadows);
                //All 0, so that the SH contributes nothing. The sky lighting provides ambient instead.
                //(Layout, if this is ever used: float4[7]. [0] is the RGB constant term, then 2 float4s each of the remaining 8 coefficients for R, G, then B)
                buffer.WriteVal(shLightShader.PixelParams.WorldIncidentLighting, new Fixed7<Vector4>());
                break;
            case null:
                break;
            default:
                throw new NotSupportedException($"{pixelShader.ShaderType} is not supported by the renderer");
        }

        static void WriteDirectionalLight(Span<byte> buffer, MeshRenderContext context, FShaderParameter lightColorParam, FShaderParameter bReceiveDynamicShadowsParam)
        {
            LinearColor lightColor = context.Lighting.LightColor;
            //w is the falloff exponent, which is unused for directional lights
            buffer.WriteVal(lightColorParam, new Vector4(lightColor.R, lightColor.G, lightColor.B, 0));
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
    /// <param name="usePreviewSky">Add <see cref="MeshRenderContext.Lighting"/>'s sky lighting (if the shader has a sky light)</param>
    public static void WriteValues<LightMapPolicy>(this TBasePassPixelShader<LightMapPolicy> shader,
        Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat, bool usePreviewSky)
        where LightMapPolicy : struct, IPixelParametersType
    {
        shader.MaterialParameters.WriteValues(buffer, context, mesh, mat);
        bool drawUnlit = mat.IsUnlit;
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
            default:
                throw new NotSupportedException($"{vertexShader.ShaderType} is not supported by the renderer");
        }
    }

    /// <summary>
    /// Writes the parameters for a TLightPixelShader, as UE3's TMeshLightingDrawingPolicy sets them
    /// </summary>
    public static void WriteLightPixelShaderValues(Shader pixelShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat,
        LightInteraction interaction)
    {
        SceneLight light = interaction.Light;
        //w is the falloff exponent. (UE3 also scales the color by the primitive's DominantShadowFactor for dominant lights, which LE levels don't use)
        var lightColorAndFalloffExponent = new Vector4(light.Color, light.FalloffExponent);
        switch (pixelShader)
        {
            case TLightPixelShader<FPointLightPolicy.PixelParametersType, FNullPolicy> pointLightShader:
                pointLightShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(pointLightShader.LightTypePixelParams.LightColorAndFalloffExponent, lightColorAndFalloffExponent);
                break;
            case TLightPixelShader<FPointLightPolicy.PixelParametersType, FShadowTexturePolicy.PixelParametersType> pointLightShadowTextureShader:
                pointLightShadowTextureShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(pointLightShadowTextureShader.LightTypePixelParams.LightColorAndFalloffExponent, lightColorAndFalloffExponent);
                SetShadowTexture(context, pointLightShadowTextureShader.ShadowingPixelParams.ShadowTexture, interaction);
                break;
            case TLightPixelShader<FPointLightPolicy.PixelParametersType, FSignedDistanceFieldShadowTexturePolicy.PixelParametersType> pointLightDistanceFieldShader:
                pointLightDistanceFieldShader.WriteValues(buffer, context, mesh, mat);
                buffer.WriteVal(pointLightDistanceFieldShader.LightTypePixelParams.LightColorAndFalloffExponent, lightColorAndFalloffExponent);
                SetShadowTexture(context, pointLightDistanceFieldShader.ShadowingPixelParams.ShadowTexture, interaction);
                buffer.WriteVal(pointLightDistanceFieldShader.ShadowingPixelParams.DistanceFieldParameters, GetDistanceFieldParameters(light));
                break;
            case TLightPixelShader<FSpotLightPolicy.PixelParametersType, FNullPolicy> spotLightShader:
                spotLightShader.WriteValues(buffer, context, mesh, mat);
                WriteSpotLight(buffer, spotLightShader.LightTypePixelParams, light, lightColorAndFalloffExponent);
                break;
            case TLightPixelShader<FSpotLightPolicy.PixelParametersType, FShadowTexturePolicy.PixelParametersType> spotLightShadowTextureShader:
                spotLightShadowTextureShader.WriteValues(buffer, context, mesh, mat);
                WriteSpotLight(buffer, spotLightShadowTextureShader.LightTypePixelParams, light, lightColorAndFalloffExponent);
                SetShadowTexture(context, spotLightShadowTextureShader.ShadowingPixelParams.ShadowTexture, interaction);
                break;
            case TLightPixelShader<FSpotLightPolicy.PixelParametersType, FSignedDistanceFieldShadowTexturePolicy.PixelParametersType> spotLightDistanceFieldShader:
                spotLightDistanceFieldShader.WriteValues(buffer, context, mesh, mat);
                WriteSpotLight(buffer, spotLightDistanceFieldShader.LightTypePixelParams, light, lightColorAndFalloffExponent);
                SetShadowTexture(context, spotLightDistanceFieldShader.ShadowingPixelParams.ShadowTexture, interaction);
                buffer.WriteVal(spotLightDistanceFieldShader.ShadowingPixelParams.DistanceFieldParameters, GetDistanceFieldParameters(light));
                break;
            default:
                throw new NotSupportedException($"{pixelShader.ShaderType} is not supported by the renderer");
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

    public static void WriteValues<LightTypePolicy, ShadowingTypePolicy>(this TLightPixelShader<LightTypePolicy, ShadowingTypePolicy> shader,
        Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
        where LightTypePolicy : struct, IPixelParametersType where ShadowingTypePolicy : struct, IPixelParametersType
    {
        shader.MaterialParameters.WriteValues(buffer, context, mesh, mat);
        //The dynamic shadows of the light, in screen space. There aren't any, so it's all lit
        if (shader.LightAttenuationTexture.IsBound())
        {
            context.LEEffect.PixelShaderResources.Set(shader.LightAttenuationTexture.BaseIndex, context.WhiteTexView);
        }
        buffer.WriteVal(shader.bReceiveDynamicShadows, 0);
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
    {
        buffer.WriteVal(p.CameraWorldPosition, context.Camera.EyePosition);
        buffer.WriteVal(p.ObjectWorldPositionAndRadius, new Vector4(mesh.TransformedBounds.Origin, mesh.TransformedBounds.SphereRadius));
        buffer.WriteVal(p.ObjectOrientation, mesh.LocalToWorld.GetAxis(2).Normal());
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
        buffer.WriteVal(p.LocalToWorld, mesh.LocalToWorld);
        buffer.WriteVal(p.WorldToLocal, mesh.WorldToLocal);
        //float3x3 shader parameters have each row padded to 4 floats, so a Matrix4x4's layout matches (the 4th column lands in the padding)
        buffer.WriteVal(p.WorldToView, effect.ViewMatrix);
        buffer.WriteVal(p.InvViewProjection, effect.InverseViewProjectionMatrix);
        buffer.WriteVal(p.ViewProjection, effect.ViewProjectionMatrix);

        p.SceneTextureParameters.WriteValues(buffer, context, mesh, mat);

        //UE3 flips this for meshes with reversed culling (see FMaterialPixelShaderParameters::SetMesh). Backfaces aren't rendered in a separate pass
        buffer.WriteVal(p.TwoSidedSign, mesh.LocalToWorld.GetDeterminant() < 0 ? -1f : 1f);
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
